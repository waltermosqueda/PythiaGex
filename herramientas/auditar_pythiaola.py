# -*- coding: utf-8 -*-
"""AUDITORIA POR FUERA de La Ola 1.0 (PythiaOla.dll, 27-11-2026): la ola, la marea y el CLIMA (semaforo D1 de la ronda 21).

Dos usos:

1) python -I herramientas/auditar_pythiaola.py --r21 2026-11-25 2026-11-20
   El ARNES OFFLINE: corre atas/_test_pythiaola (modo r21: el MISMO codigo del indicador sobre la cinta de la sonda de esa rueda, con
   las cintas de las ruedas previas para la mediana) y compara cada csv que escribe contra la REFERENCIA EN PYTHON DE LA RONDA 21:
     - el clima minuto por minuto (los 390): rrn, maximo, minimo, precio, VWAP, apertura, mediana, f(m), tipo y lado, contra
       r21_01_regimen.rasgos_dia + estado_minuto de regla_congelada_comb_t7_v4.py (los numeros EXACTOS, sin tolerancia);
     - la ola y la marea contra los rasgos de la R19 que usa la R21 (r19_01_rasgos: pivotes de toda la jornada con su segundo, precio,
       tipo, segundo de confirmacion y delta acumulado; y al cierre de cada minuto de la rueda: direccion, extremo, origen, cantidad de
       pivotes y delta acumulado);
     - ademas, el clima RECALCULADO POR FUERA con una implementacion propia (la grilla por segundo de la R19, con numpy) desde la cinta:
       tiene que dar lo mismo que la R21 y que el indicador.
   Entradas: 'r19' = la cinta como la compacta la R19 (sin filas duplicadas exactas, orden estable por hora): la misma entrada que la
   referencia, tiene que dar 0 diferencias en todo. 'cruda' = como la entrega ATAS (lo que vera el indicador): el clima y los pivotes
   tienen que dar igual; si el delta acumulado difiere, tiene que ser exactamente por las filas duplicadas (y la ola cruda se audita
   ademas campo por campo con la Maquina de test_ola.py, via auditar_ola.auditar).

2) python -I herramientas/auditar_pythiaola.py <ola.csv> <cinta.csv> [--previas cinta ...] [--jsonl auditoria.jsonl] [--max-difs N]
   La AUDITORIA EN VIVO: el ola-<inst>-<tf>-<clave>.csv que escribe el indicador cada minuto (%APPDATA%/ATAS/PythiaGex/pythiaola) contra
   una cinta de la sonda de la MISMA jornada: la ola y la marea con auditar_ola.auditar (sin tolerancias) y el clima de cada minuto
   cerrado, recalculado por fuera desde la cinta con la definicion de la R21 (mismas ordenes que tenia el indicador). Con --previas
   (las cintas de las ruedas anteriores) recalcula tambien la mediana de 10 ruedas. Con --jsonl controla que lo que mostro el panel en
   cada minuto (clima, marea, ola, rayas) sea lo del csv.

Todo en prioridad baja; se frena si la RAM libre baja de 2 GB. Nada toca ATAS.
"""
import csv
import glob
import json
import math
import os
import re
import subprocess
import sys
import tempfile
import time
from datetime import datetime, timedelta, timezone
from zoneinfo import ZoneInfo

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(AQUI)
sys.path.insert(0, AQUI)
import test_ola as T          # noqa: E402  (baja la prioridad del proceso al importarse; ram_libre_mb, iso)
import auditar_ola as AO      # noqa: E402  (la ola y la marea contra la Maquina, sin tolerancias)
import numpy as np            # noqa: E402

R21 = os.path.join(RAIZ, "laboratorio", "ronda21_regimen")
R19 = os.path.join(RAIZ, "laboratorio", "ronda19_confluencias")
EXE = os.path.join(RAIZ, "atas", "_test_pythiaola", "bin", "Release", "net10.0", "_test_pythiaola.exe")
FUENTES = [os.path.join(RAIZ, "atas", "PythiaOla", f) for f in ("OlaMotor.cs", "OlaNucleo.cs", "OlaClima.cs")] + \
          [os.path.join(RAIZ, "atas", "_test_pythiaola", f) for f in ("Program.cs", "FlujoOla.cs", "ArnesR21.cs")]
FLUJO = os.path.join(os.environ.get("APPDATA", ""), "ATAS", "PythiaGex", "flujo")
NY = ZoneInfo("America/New_York")
UTC = timezone.utc
BELOW_NORMAL = 0x00004000
CURVA_JSON = os.path.join(R21, "curva_rango_por_minuto.json")
F_CURVA = json.load(open(CURVA_JSON))["f"]
CORTE_T, CORTE_E = 1.6, 1.0
NM = 390


# ----------------------------------------------------------------------------------------------------------- lectura del csv
def _f(s):
    """double del csv: 'nan' -> nan; el resto con float() (el C# escribe 'R': el mas corto que se lee igual)."""
    return float("nan") if s in ("nan", "NaN", "") else float(s)


def leer_clima(ruta):
    """Filas C del csv: m -> dict (los double ya parseados exactos)."""
    out = {}
    with open(ruta, encoding="utf-8") as fh:
        for linea in fh:
            if not linea.startswith("C,"):
                continue
            c = linea.rstrip("\n").split(",")
            out[int(c[1])] = {"fin": c[2], "n": int(c[3]), "hay_rth": c[4] == "1", "h": float(c[5]), "l": float(c[6]), "c": float(c[7]),
                              "o": float(c[8]), "vw": _f(c[9]), "med": _f(c[10]), "fm": _f(c[11]), "rrn": _f(c[12]), "tipo": c[13],
                              "lado": int(c[14]), "lado_ap": int(c[15])}
    return out


def ms_utc(txt):
    """'yyyy-mm-ddTHH:MM:SS.fff' (UTC) -> milisegundos desde 1970, entero (sin redondeos)."""
    d = datetime(int(txt[0:4]), int(txt[5:7]), int(txt[8:10]), int(txt[11:13]), int(txt[14:16]), int(txt[17:19]), tzinfo=UTC)
    return int(d.timestamp()) * 1000 + int(txt[20:23])


def rth_ms(dia):
    """9:30 y 16:00 NY del dia de la rueda, en ms UTC."""
    d = datetime.strptime(dia, "%Y-%m-%d")
    a = d.replace(hour=9, minute=30, tzinfo=NY).astimezone(UTC)
    b = d.replace(hour=16, minute=0, tzinfo=NY).astimezone(UTC)
    return int(a.timestamp()) * 1000, int(b.timestamp()) * 1000


# ----------------------------------------------------------------------------------------------------------- clima por fuera
def estado(rrn, c, vw, o):
    """estado_minuto de la regla congelada de la R21, reescrito (no importado): 'T' si rrn >= 1,6 y el precio del mismo lado del VWAP y
    de la apertura; 'R' si 1,0 < rrn < 1,6; 'E' si rrn <= 1,0; 'N' (dudoso) en otro caso. Devuelve (tipo, signo(c - vw) o nan)."""
    lvw = math.copysign(1.0, c - vw) if (vw == vw and c != vw) else (0.0 if vw == vw else float("nan"))
    lo = float("nan") if o != o else (math.copysign(1.0, c - o) if c != o else 0.0)
    fin = not (math.isnan(rrn) or math.isinf(rrn))
    if fin and rrn >= CORTE_T and lvw == lo and lvw != 0 and lvw == lvw:
        return "T", lvw
    if fin and CORTE_E < rrn < CORTE_T:
        return "R", lvw
    if fin and rrn <= CORTE_E:
        return "E", lvw
    return "N", lvw


def clima_grilla(ms, pri, ult, vol, rth0, med):
    """EL CLIMA RECALCULADO POR FUERA, con la grilla por segundo de la R19 (r19_01_rasgos.jornada) y la cuenta de r21_01_regimen:
    ms = hora UTC de cada orden en ms (en el orden en que se procesan), pri/ult/vol = arrays; rth0 = 9:30 NY en ms. La jornada son los
    82.800 segundos desde las 18:00 NY de la vispera. Devuelve una lista de 390 dicts (h, l, c, vw, o, rrn, tipo, lado)."""
    N, K0 = 82800, 21600
    RTH0 = K0 + 34200
    ms = np.asarray(ms, np.int64); pri = np.asarray(pri, float); ult = np.asarray(ult, float); vol = np.asarray(vol, float)
    med_ny = rth0 - 34200 * 1000                      # medianoche NY del dia de la rueda, en ms UTC
    s = np.floor_divide(ms - med_ny, 1000)            # segundo NY (negativo de noche), piso exacto
    k = np.clip(s + K0, 0, N - 1)
    n = len(ms)
    mn = np.minimum(pri, ult); mx = np.maximum(pri, ult)
    last = np.r_[np.nonzero(np.diff(k))[0], n - 1]
    li = np.full(N, -1, np.int64); li[k[last]] = last
    carry = np.maximum.accumulate(li)
    c_s = np.where(carry >= 0, ult[np.maximum(carry, 0)], np.nan)
    # bfill (los segundos antes de la primera orden de la jornada toman el primer precio)
    if np.isnan(c_s).any():
        primero = np.argmax(~np.isnan(c_s)); c_s[:primero] = c_s[primero]
    h_s = np.full(N, -np.inf); np.maximum.at(h_s, k, mx)
    l_s = np.full(N, np.inf); np.minimum.at(l_s, k, mn)
    sin = ~np.isfinite(h_s); h_s[sin] = c_s[sin]; l_s[sin] = c_s[sin]
    pm = (pri + ult) / 2.0
    pv_s = np.bincount(k, weights=pm * vol, minlength=N); v_s = np.bincount(k, weights=vol, minlength=N)
    idx = np.arange(N) >= RTH0
    cpv = np.cumsum(np.where(idx, pv_s, 0.0)); cv = np.cumsum(np.where(idx, v_s, 0.0))
    with np.errstate(invalid="ignore", divide="ignore"):
        vw = cpv / cv
    rthm = (s >= 34200) & (s < 57600)
    o = float(ult[np.nonzero(rthm)[0][0]]) if rthm.any() else float("nan")
    H = np.maximum.accumulate(h_s[RTH0:RTH0 + 23400]); L = np.minimum.accumulate(l_s[RTH0:RTH0 + 23400])
    out = []
    for m in range(NM):
        sec = 60 * m + 59
        hh, ll, cc, vv = float(H[sec]), float(L[sec]), float(c_s[RTH0 + sec]), float(vw[RTH0 + sec])
        rrn = (hh - ll) / (med * F_CURVA[m]) if med * F_CURVA[m] != 0 else float("nan")
        tipo, lado = estado(rrn, cc, vv, o)
        out.append({"h": hh, "l": ll, "c": cc, "vw": vv, "o": o, "rrn": rrn, "tipo": tipo, "lado": lado})
    return out


# ----------------------------------------------------------------------------------------------------------- referencias de la R21
_R21 = {}


def r21_modulos():
    if not _R21:
        sys.path.insert(0, R21)
        sys.path.insert(0, R19)
        import r21_01_regimen as RG1                     # noqa: E402
        import regla_congelada_comb_t7_v4 as RG          # noqa: E402
        import r19_02_motor as M                         # noqa: E402
        _R21.update(RG1=RG1, RG=RG, M=M)
    return _R21["RG1"], _R21["RG"], _R21["M"]


def referencia_r21(dia):
    """El clima de la R21 para la rueda: rasgos_dia (r21_01_regimen) con la curva de curva_rango_por_minuto.json, y estado_minuto de la
    regla congelada. Mas los rasgos de la R19 (pivotes y estados por segundo de la ola y la marea) y su meta (X, mediana)."""
    RG1, RG, M = r21_modulos()
    cur = json.load(open(RG.CURVA))
    reg = RG1.rasgos_dia(dia, np.array(cur["f"]), np.array(cur["g"]), {}, RG1.resumen_es())
    tipo, lvw = RG.estado_minuto(reg["rrn"].to_numpy(), reg["c"].to_numpy(), reg["vw"].to_numpy(), reg["O"].to_numpy())
    R, meta = M.cargar("ES", dia)
    return reg, tipo, lvw, R, meta


# ----------------------------------------------------------------------------------------------------------- comparaciones
def igual(a, b):
    """Igualdad EXACTA de double, con nan == nan."""
    return (a != a and b != b) or a == b


def comparar_clima(C, ref, etiqueta, difs, tolerar_vw=False):
    """C: filas C del csv; ref: lista de 390 dicts (h, l, c, vw, o, rrn, tipo, lado, med?, fm?). Exacto salvo tolerar_vw (la cinta cruda
    trae filas duplicadas que la R19 saca: el VWAP puede diferir en los ultimos decimales; se mide aparte)."""
    n = 0; dvw = 0; max_vw = 0.0
    for m in range(NM):
        if m not in C:
            difs.append("%s: falta el minuto %d" % (etiqueta, m)); continue
        a = C[m]; b = ref[m]; n += 1
        for campo in ("h", "l", "c", "o", "rrn"):
            if not igual(a[campo], b[campo]):
                difs.append("%s minuto %d %s: indicador %r, referencia %r" % (etiqueta, m, campo, a[campo], b[campo]))
        if not igual(a["vw"], b["vw"]):
            dvw += 1; max_vw = max(max_vw, abs(a["vw"] - b["vw"]))
            if not tolerar_vw:
                difs.append("%s minuto %d vw: indicador %r, referencia %r" % (etiqueta, m, a["vw"], b["vw"]))
        if a["tipo"] != b["tipo"]:
            difs.append("%s minuto %d tipo: indicador %s, referencia %s (rrn %r)" % (etiqueta, m, a["tipo"], b["tipo"], b["rrn"]))
        la, lb = a["lado"], b["lado"]
        # el indicador escribe 0 donde la referencia tiene nan (sin VWAP todavia): es el mismo "sin lado"
        if not (igual(la, lb) or (lb != lb and la == 0) or (la != la and lb == 0)):
            difs.append("%s minuto %d lado: indicador %r, referencia %r" % (etiqueta, m, la, lb))
        if "med" in b and not igual(a["med"], b["med"]):
            difs.append("%s minuto %d mediana: indicador %r, referencia %r" % (etiqueta, m, a["med"], b["med"]))
        if not igual(a["fm"], F_CURVA[m]):
            difs.append("%s minuto %d f(m): indicador %r, curva %r" % (etiqueta, m, a["fm"], F_CURVA[m]))
    return n, dvw, max_vw


def ref_dicts(reg, tipo, lvw):
    out = []
    for i in range(NM):
        r = reg.iloc[i]
        out.append({"h": float(r["H"]), "l": float(r["L"]), "c": float(r["c"]), "vw": float(r["vw"]), "o": float(r["O"]), "rrn": float(r["rrn"]),
                    "tipo": str(tipo[i]), "lado": float(lvw[i]), "med": float(r["med10"])})
    return out


def seg_rth(txt, rth0):
    """Segundo de una hora UTC del csv contado desde las 9:30 NY (como los seg de la R19: piso de los segundos NY - 34200)."""
    return (ms_utc(txt) - rth0) // 1000


def comparar_ola_r19(P, F, R, rth0, etiqueta, difs, con_cum=True):
    """Pivotes confirmados y estado al cierre de cada minuto de la rueda contra los rasgos de la R19 (o_piv/m_piv, o_dir/o_ext/o_p0/o_np,
    m_dir/m_ext/m_p0/m_np, cum)."""
    npiv = nest = 0
    for esc, clave in (("X", "o"), ("4X", "m")):
        ref = R[clave + "_piv"]
        cs = [p for (_, p) in P[esc] if p[3] == 1]
        if len(cs) != len(ref):
            difs.append("%s %s: %d pivotes en el indicador, %d en la R19" % (etiqueta, esc, len(cs), len(ref)))
        for i, (p, r) in enumerate(zip(cs, ref)):
            npiv += 1
            se, pr, tp, sc, cu = int(r[0]), float(r[1]), int(r[2]), int(r[3]), float(r[4])
            mio = (seg_rth(p[0], rth0), p[1], p[2], seg_rth(p[4], rth0), p[6])
            suyo = (se, pr, tp, sc, cu)
            if mio[:4] != suyo[:4] or (con_cum and mio[4] != suyo[4]):
                difs.append("%s %s pivote %d: indicador %s, R19 %s" % (etiqueta, esc, i, mio, suyo))
    # al cierre de cada minuto de la rueda: la vela de 1 min que abre a las 9:30 + m tiene corte 9:30 + m + 1
    fotos = {"X": {}, "4X": {}}
    for esc in ("X", "4X"):
        for (k, bar, corte, tomada, fila) in F[esc]:
            if corte:
                fotos[esc][ms_utc(corte)] = fila
    for m in range(NM):
        corte = rth0 + (m + 1) * 60000
        sec = 60 * m + 59
        for esc, clave in (("X", "o"), ("4X", "m")):
            fila = fotos[esc].get(corte)
            if fila is None:
                difs.append("%s %s: no hay vela con corte al cierre del minuto %d" % (etiqueta, esc, m)); continue
            nest += 1
            d_, ext, p0, npv = int(R[clave + "_dir"][sec]), float(R[clave + "_ext"][sec]), float(R[clave + "_p0"][sec]), int(R[clave + "_np"][sec])
            # fila: n, t_ultima, ultimo, cum, dir, pivotes, origen_t, origen_precio, origen_tipo, origen_cum, extremo_t, extremo_precio, ...
            mio = (fila[4], fila[11], fila[7], fila[5])
            suyo = (d_, ext, p0, npv)
            if mio != suyo:
                difs.append("%s %s minuto %d: indicador (dir, extremo, origen, pivotes) %s, R19 %s" % (etiqueta, esc, m, mio, suyo))
            if con_cum and fila[3] != float(R["cum"][sec]):
                difs.append("%s minuto %d cum: indicador %r, R19 %r" % (etiqueta, m, fila[3], float(R["cum"][sec])))
    return npiv, nest


# ----------------------------------------------------------------------------------------------------------- cintas
def cintas_es():
    out = {}
    for f in sorted(glob.glob(os.path.join(FLUJO, "cinta-ES-*.csv"))):
        p = os.path.basename(f)[:-4].split("-")
        if len(p) != 4 or not os.path.exists(f + ".listo"):
            continue
        fin = p[3][:8]
        out["%s-%s-%s" % (fin[:4], fin[4:6], fin[6:8])] = f
    return out


def leer_ordenes(ruta, compactar):
    """Las ordenes de la cinta: como las entrega ATAS (orden del archivo, con duplicados) o como la R19 (sin filas duplicadas exactas,
    orden estable por hora). Devuelve listas ms, pri, ult, vol, lado y la cantidad de duplicadas sacadas."""
    filas = []
    with open(ruta, newline="") as fh:
        r = csv.reader(fh)
        next(r)
        for x in r:
            filas.append(x)
    dup = 0
    if compactar:
        vistas = set(); f2 = []
        for x in filas:
            t = tuple(x)
            if t in vistas:
                dup += 1; continue
            vistas.add(t); f2.append(x)
        f2.sort(key=lambda x: x[0])   # estable (sorted de Python lo es); la hora ISO ordena igual que el tiempo
        filas = f2
    ms = [ms_utc(x[0]) for x in filas]
    return ms, [float(x[1]) for x in filas], [float(x[2]) for x in filas], [float(x[3]) for x in filas], [int(x[4]) for x in filas], dup


def mediana_previas(rutas, dia):
    """np.median del rango RTH (maximo - minimo de primero/ultimo de las ordenes entre 9:30 y 16:00 NY) de las ultimas 10 ruedas previas."""
    rangos = []
    for f in sorted(rutas):
        p = os.path.basename(f)[:-4].split("-")
        fin = p[3][:8]; d = "%s-%s-%s" % (fin[:4], fin[4:6], fin[6:8])
        if d >= dia:
            continue
        a, b = rth_ms(d)
        hi, lo = -1e18, 1e18
        with open(f, newline="") as fh:
            r = csv.reader(fh); next(r)
            for x in r:
                t = ms_utc(x[0])
                if a <= t < b:
                    p1, p2 = float(x[1]), float(x[2])
                    hi = max(hi, p1, p2); lo = min(lo, p1, p2)
        if hi > lo:
            rangos.append((d, hi - lo))
    rangos.sort()
    ult = [x for _, x in rangos[-10:]]
    return (float(np.median(ult)) if ult else 0.0), rangos[-10:]


# ----------------------------------------------------------------------------------------------------------- modo --r21
def correr_arnes(args):
    r = subprocess.run([EXE] + args, capture_output=True, text=True, creationflags=BELOW_NORMAL)
    print(r.stdout.rstrip(), flush=True)
    if r.returncode != 0:
        raise RuntimeError("el arnes fallo (%d): %s" % (r.returncode, r.stderr[-2000:]))


def modo_r21(dias, max_difs):
    if not os.path.exists(EXE):
        raise SystemExit("falta el arnes: dotnet build -c Release en atas/_test_pythiaola")
    viejo = [os.path.basename(f) for f in FUENTES if os.path.getmtime(f) > os.path.getmtime(EXE)]
    if viejo:
        print("OJO: el arnes es mas viejo que " + ", ".join(viejo) + ": recompilar", flush=True)
    cin = cintas_es()
    salida = os.path.join(tempfile.gettempdir(), "pythia_auditar_pythiaola")
    os.makedirs(salida, exist_ok=True)
    total_malos = 0
    for dia in dias:
        if T.ram_libre_mb() < 2048:
            print("FRENO: RAM libre %.0f MB (< 2 GB)." % T.ram_libre_mb()); return 1
        if dia not in cin:
            print("FALTA la cinta de ES %s" % dia); total_malos += 1; continue
        previas = [cin[d] for d in sorted(cin) if d < dia][-13:]
        t0 = time.time()
        correr_arnes(["r21", salida, dia, cin[dia]] + previas)
        reg, tipo, lvw, R, meta = referencia_r21(dia)
        ref = ref_dicts(reg, tipo, lvw)
        rth0, rth1 = rth_ms(dia)
        print("\n%s | referencia R21: X %s, mediana de 10 %s (vispera %s) | tendencia %d, rotacion %d, estrecho %d, dudoso %d minutos"
              % (dia, meta["X"], meta["mediana_rango_10"], meta["dia_previo"], sum(tipo == "T"), sum(tipo == "R"), sum(tipo == "E"), sum(tipo == "N")))
        # mediana por fuera desde las cintas previas
        med_fuera, usadas = mediana_previas(previas, dia)
        print("  mediana por fuera (cintas previas %s .. %s): %r -> %s" % (usadas[0][0], usadas[-1][0], med_fuera,
              "IGUAL a la R21" if med_fuera == meta["mediana_rango_10"] else "DISTINTA de la R21 %r" % meta["mediana_rango_10"]))
        if med_fuera != meta["mediana_rango_10"]:
            total_malos += 1
        # el clima por fuera (grilla por segundo) con las dos entradas
        grillas = {}
        for variante in ("r19", "cruda"):
            ms, pri, ult, vol, _, dup = leer_ordenes(cin[dia], variante == "r19")
            grillas[variante] = clima_grilla(ms, pri, ult, vol, rth0, meta["mediana_rango_10"])
            d0 = []
            n, dvw, mvw = comparar_clima({m: dict(g, fm=F_CURVA[m], med=meta["mediana_rango_10"], lado_ap=0) for m, g in enumerate(grillas[variante])},
                                         ref, "grilla %s vs R21" % variante, d0, tolerar_vw=(variante == "cruda"))
            print("  clima POR FUERA (grilla por segundo, entrada %s, %d duplicadas sacadas) vs R21: %d minutos, %d diferencias%s"
                  % (variante, dup, n, len(d0), "" if not dvw else "; VWAP distinto en %d minutos (max %.3g pts, por los duplicados)" % (dvw, mvw)))
            for x in d0[:max_difs]:
                print("    DIF", x)
            total_malos += len(d0)
        # cada csv del indicador
        lista = os.path.join(salida, "r21-csvs-%s.txt" % dia)
        for linea in open(lista, encoding="utf-8"):
            if not linea.strip():
                continue
            ruta, cinta, variante, escenario = linea.rstrip("\n").split("\t")
            meta_c, P, F = AO.leer_ola(ruta)
            C = leer_clima(ruta)
            difs = []
            # clima contra la R21 (exacto; con la cruda el VWAP puede moverse en los decimales por los duplicados)
            n, dvw, mvw = comparar_clima(C, ref, "%s %s clima" % (variante, escenario), difs, tolerar_vw=(variante == "cruda"))
            # y contra el clima por fuera con la MISMA entrada: exacto, VWAP incluido
            d_g = []
            comparar_clima(C, [dict(g, med=meta["mediana_rango_10"]) for g in grillas[variante]], "%s %s clima vs grilla" % (variante, escenario), d_g)
            difs += d_g
            if float(meta_c["med_clima"]) != meta["mediana_rango_10"]:
                difs.append("mediana del indicador %s, R21 %s" % (meta_c["med_clima"], meta["mediana_rango_10"]))
            if float(meta_c["x"]) != meta["X"]:
                difs.append("X del indicador %s, R21 %s" % (meta_c["x"], meta["X"]))
            # ola y marea contra la R19 (con la cruda, sin el delta acumulado: lo cambian los duplicados)
            npiv, nest = comparar_ola_r19(P, F, R, rth0, "%s %s" % (variante, escenario), difs, con_cum=(variante == "r19"))
            extra = ""
            if variante == "cruda":
                # la ola cruda, campo por campo (delta incluido) contra la Maquina sobre la misma cinta cruda
                d_o, _, res = AO.auditar(ruta, cinta, max_difs=max_difs, callado=True)
                difs += ["ola cruda vs Maquina: " + x for x in d_o]
                extra = " | ola y marea vs Maquina de test_ola (cinta cruda, delta incluido): %d pivotes, %d fotos, %d campos, %d difs" % (
                    res.get("pivotes", 0), res.get("fotos", 0), res.get("campos", 0), len(d_o))
            tipos = {}
            for m in C:
                tipos[C[m]["tipo"]] = tipos.get(C[m]["tipo"], 0) + 1
            print("  %-6s %-7s %s | clima %d minutos vs R21 (T %d R %d E %d N %d)%s | ola+marea vs R19: %d pivotes, %d estados%s%s | DIFERENCIAS %d"
                  % (variante, escenario, os.path.basename(ruta), n, tipos.get("T", 0), tipos.get("R", 0), tipos.get("E", 0), tipos.get("N", 0),
                     "" if not dvw else " (VWAP con duplicados: distinto en %d minutos, max %.3g pts)" % (dvw, mvw),
                     npiv, nest, "" if variante == "r19" else " sin el delta", extra, len(difs)), flush=True)
            for x in difs[:max_difs]:
                print("    DIF", x)
            total_malos += len(difs)
        print("  %s: %.1f s" % (dia, time.time() - t0))
    print("\nTOTAL: %s" % ("0 diferencias en ola, marea y clima contra la referencia de la R21" if total_malos == 0 else "%d diferencias" % total_malos))
    return 0 if total_malos == 0 else 1


# ----------------------------------------------------------------------------------------------------------- modo en vivo
def modo_vivo(ruta_ola, ruta_cinta, previas, jsonl, max_difs):
    malos = 0
    d_o, avisos, res = AO.auditar(ruta_ola, ruta_cinta, max_difs=max_difs)
    malos += len(d_o)
    meta, P, F = AO.leer_ola(ruta_ola)
    C = leer_clima(ruta_ola)
    clave = meta.get("clave", "")
    dia = "%s-%s-%s" % (clave[0:4], clave[4:6], clave[6:8])
    rth0, _ = rth_ms(dia)
    med = float(meta.get("med_clima") or 0)
    if meta.get("curva_huella") and meta["curva_huella"] != __import__("hashlib").sha256(open(CURVA_JSON, "rb").read()).hexdigest():
        print("  DIF la curva del indicador no es la de curva_rango_por_minuto.json"); malos += 1
    if previas:
        mf, us = mediana_previas(previas, dia)
        print("  mediana por fuera con %d ruedas previas: %r (indicador %r) %s" % (len(us), mf, med, "IGUAL" if mf == med else "DISTINTA"))
        malos += 0 if mf == med else 1
    if meta.get("clima_completo") != "1":
        print("  el clima del indicador esta INCOMPLETO (sin la cinta desde las 9:30): no se compara")
    elif C:
        # las mismas ordenes que tenia el indicador (la misma seleccion que auditar_ola: desde el inicio del motor hasta t_ultima y n)
        inicio = AO._hora(meta["inicio_motor"]) or AO._hora(meta["desde"])
        hasta = AO._hora(meta["hasta"]); t_fin = AO._hora(meta["t_ultima"]); n_ind = int(meta["n"])
        cinta = AO.leer_cinta(ruta_cinta)
        sel = []
        for (t, a, b, v, lado) in cinta:
            if t < inicio or t >= hasta or (t_fin is not None and t > t_fin):
                continue
            if t_fin is not None and t == t_fin and len(sel) >= n_ind:
                continue
            sel.append((t, a, b, v))
        ms = [ms_utc(T.iso(t)) for (t, _, _, _) in sel]
        g = clima_grilla(ms, [x[1] for x in sel], [x[2] for x in sel], [x[3] for x in sel], rth0, med)
        difs = []
        for m in sorted(C):
            a, b = C[m], dict(g[m], med=med)
            for campo in ("h", "l", "c", "o", "vw", "rrn"):
                if not igual(a[campo], b[campo]):
                    difs.append("minuto %d %s: indicador %r, por fuera %r" % (m, campo, a[campo], b[campo]))
            if a["tipo"] != b["tipo"]:
                difs.append("minuto %d tipo: indicador %s, por fuera %s" % (m, a["tipo"], b["tipo"]))
        print("  clima: %d minutos cerrados en el csv, recalculados por fuera desde la cinta: %d diferencias" % (len(C), len(difs)))
        for x in difs[:max_difs]:
            print("    DIF", x)
        malos += len(difs)
    if jsonl:
        malos += controlar_jsonl(jsonl, meta, F, C, max_difs)
    print("RESULTADO: %s" % ("todo igual" if malos == 0 else "%d diferencias" % malos))
    return 0 if malos == 0 else 1


def controlar_jsonl(ruta, meta, F, C, max_difs):
    """Lo que mostro el panel cada minuto (seccion 'pantalla' del jsonl) contra el csv: el clima del minuto, y la ola y la marea de la vela
    del momento si su foto ya estaba tomada cuando se escribio el csv."""
    clave = int(meta["clave"]); primer = int(meta["primer_bar"])
    fx = {k: (tom, fila) for (k, bar, corte, tom, fila) in F["X"]}
    f4 = {k: (tom, fila) for (k, bar, corte, tom, fila) in F["4X"]}
    n = malos = 0
    difs = []
    for linea in open(ruta, encoding="utf-8"):
        try:
            j = json.loads(linea)
        except Exception:
            continue
        p = j.get("pantalla")
        if not p or p.get("clave") != clave:
            continue
        k = p.get("momento", -1) - primer
        cl = p.get("clima") or {}
        if cl.get("hay") and cl.get("m") in C:
            n += 1
            c = C[cl["m"]]
            if c["tipo"] != cl.get("tipo") or not igual(c["rrn"], float(cl.get("rrn"))):
                difs.append("%s: clima del panel %s %s, csv %s %r" % (j.get("utc"), cl.get("tipo"), cl.get("rrn"), c["tipo"], c["rrn"]))
        for esc, tabla, nom in (("X", fx, "ola"), ("4X", f4, "marea")):
            q = p.get(nom) or {}
            if k in tabla and tabla[k][0] and q.get("hay"):
                fila = tabla[k][1]
                n += 1
                mio = (q.get("dir"), q.get("raya"), q.get("faltan"), q.get("delta"))
                suyo = (fila[4], fila[17], fila[16], fila[13])
                if mio != suyo:
                    difs.append("%s: %s del panel (dir, raya, faltan, delta) %s, csv %s" % (j.get("utc"), nom, mio, suyo))
    print("  panel (jsonl): %d valores mostrados comparados con el csv, %d diferencias" % (n, len(difs)))
    for x in difs[:max_difs]:
        print("    DIF", x)
    return len(difs)


def main():
    args = sys.argv[1:]
    max_difs = 20
    if "--max-difs" in args:
        i = args.index("--max-difs"); max_difs = int(args[i + 1]); del args[i:i + 2]
    if args and args[0] == "--r21":
        return modo_r21(args[1:] or ["2026-11-25", "2026-11-20"], max_difs)
    previas, jsonl = [], None
    if "--jsonl" in args:
        i = args.index("--jsonl"); jsonl = args[i + 1]; del args[i:i + 2]
    if "--previas" in args:
        i = args.index("--previas"); previas = args[i + 1:]; del args[i:]
    if len(args) != 2:
        raise SystemExit(__doc__)
    return modo_vivo(args[0], args[1], previas, jsonl, max_difs)


if __name__ == "__main__":
    sys.exit(main())
