# -*- coding: utf-8 -*-
"""RONDA 25, paso 7: COMPROBACION DESDE EL CRUDO con codigo propio e independiente: solo csv, json, datetime/zoneinfo y math (sin numpy, sin
pandas, sin r25_comun / r25_rasgos ni el motor de la R23). Rehace desde los CSV crudos:
  (a) clasificaciones del detector y objetivos de ruedas elegidas por regla (primera rueda marcada por cada detector y una del FOMC) en las tres
      fuentes (MES 30 min nativas, MES 5 min agregadas a 30, cinta de ES orden por orden) y las compara con salidas/rasgos_*.csv;
  (b) operaciones de A3 (las primeras y ultimas del diseno y del chequeo de la R23, en velas de 5 min de MES) y si su dia lo marca el detector
      OTF (el que A3F habria salteado).
Uso: python -I r25_07_verificar.py -> salidas/verificacion.txt"""
import csv, json, math, os, sys
from datetime import datetime, timedelta, timezone
from zoneinfo import ZoneInfo

AQUI = os.path.dirname(os.path.abspath(__file__))
VELAS = os.path.join(os.path.dirname(AQUI), "dom", "ronda3", "velas")
FLUJO = os.path.join(os.environ.get("APPDATA", ""), "ATAS", "PythiaGex", "flujo")
NYZ = ZoneInfo("America/New_York")
TK = 0.25
ROLLS = {"2024-06-17", "2024-09-16", "2024-12-16", "2025-03-17", "2025-06-16", "2025-09-15", "2025-12-15", "2026-03-16", "2026-06-15", "2026-09-14"}
CAL = json.load(open(os.path.join(AQUI, "calendario_oficial.json"), encoding="utf-8"))
SALIDA = []


def P(s=""):
    print(s, flush=True); SALIDA.append(s)


def tick(x):
    return math.floor(x / TK + 0.5) * TK


def leer_velas(nombre):
    """{jornada: {'rth': [(min, o, h, l, c, v)], 'n': n}}; jornada = fecha NY de (hora NY + 6 h)."""
    out = {}
    with open(os.path.join(VELAS, nombre), newline="") as f:
        for r in csv.DictReader(f):
            t = datetime.strptime(r["t"], "%Y-%m-%dT%H:%M:%S").replace(tzinfo=timezone.utc).astimezone(NYZ)
            jor = (t.replace(tzinfo=None) + timedelta(hours=6)).strftime("%Y-%m-%d")
            mi = t.hour * 60 + t.minute
            if 570 <= mi < 960:
                out.setdefault(jor, []).append((mi, float(r["o"]), float(r["h"]), float(r["l"]), float(r["c"]), float(r["vol"])))
    for k in out: out[k].sort()
    return out


def a30(barras5):
    out = []
    for j in range(13):
        s = [b for b in barras5 if 570 + 30 * j <= b[0] < 600 + 30 * j]
        out.append((s[0][1], max(b[2] for b in s), min(b[3] for b in s), s[-1][4], sum(b[5] for b in s)))
    return out


def rasgos(b, med10, X, prev, dia):
    """b: 13 velas (o,h,l,c,v). Devuelve los numeros del detector y los objetivos."""
    vw = []; spv = sv = 0.0
    for x in b:
        spv += (x[1] + x[2]) / 2 * x[4]; sv += x[4]; vw.append(spv / sv)
    O = b[0][0]; H13 = max(x[1] for x in b[:7]); L13 = min(x[2] for x in b[:7]); c13 = b[6][3]; C16 = b[12][3]
    s = 1 if c13 > vw[6] else (-1 if c13 < vw[6] else 0)
    q = min(O - L13, H13 - O) / (H13 - L13); d = 1 if (O - L13) <= (H13 - O) else -1
    coh = d == s and ((c13 > O) if d > 0 else (c13 < O))
    otf = sum(1 for k in range(1, 7) if (s > 0 and b[k][2] >= b[k - 1][2]) or (s < 0 and b[k][1] <= b[k - 1][1]))
    vwa = sum(1 for k in range(1, 7) if (s > 0 and b[k][2] > vw[k - 1]) or (s < 0 and b[k][1] < vw[k - 1]))
    ace = 0
    if prev is not None:
        g = 1 if O > prev[0] else (-1 if O < prev[1] else 0)
        if g != 0 and g == s: ace = int(L13 > prev[0]) if g > 0 else int(H13 < prev[1])
    H = max(x[1] for x in b); L = min(x[2] for x in b)
    e = abs(C16 - O) / (H - L)
    nv = int(not any(b[k][2] <= vw[k - 1] <= b[k][1] for k in range(7, 13)))
    hpm = max(x[1] for x in b[7:]); lpm = min(x[2] for x in b[7:])
    et = int(abs(C16 - c13) / (hpm - lpm) >= 0.6 and abs(C16 - c13) >= 2 * X)
    return dict(s13=s, APE_q=round(q, 6), APE_015=int(coh and q <= 0.15), OTF_n=otf, OTF_5=int(otf >= 5), VWA_n=vwa, VWA_5=int(vwa >= 5), ACE_0=ace,
                EXT_r=round((H13 - L13) / med10, 6), EXT_1=int((H13 - L13) / med10 >= 1.0), CAL=int(dia in CAL["fomc"]),
                TA=int(s != 0 and s * (C16 - c13) > 0), D=round(s * (C16 - c13) / X, 6), TIPO=int(e >= 0.6), NV=nv, ET=et, X=X)


def historia(dias_rth, completas, n_hist_min):
    """rango por jornada de historia (completas o con >= n_hist_min velas, salvo cortas)."""
    rango = {}
    for d, bs in dias_rth.items():
        if d in completas or (n_hist_min and len(bs) >= n_hist_min and max(b[0] for b in bs) > 13 * 60 + 15):
            rango[d] = max(b[2] for b in bs) - min(b[3] for b in bs)
    return rango


def contexto(dia, rango, dias_rth):
    prev = sorted(x for x in rango if x < dia)[-10:]
    v = sorted(rango[x] for x in prev); med = (v[4] + v[5]) / 2.0
    X = math.floor(0.08 * med / TK + 0.5) * TK
    pa = sorted(x for x in dias_rth if x < dia)
    ant = None
    if pa and dia not in ROLLS and (datetime.strptime(dia, "%Y-%m-%d") - datetime.strptime(pa[-1], "%Y-%m-%d")).days <= 5:
        bs = dias_rth[pa[-1]]; ant = (max(b[2] for b in bs), min(b[3] for b in bs))
    return med, X, ant


def comparar(nombre, dia, mio, ref):
    claves = ["s13", "APE_q", "APE_015", "OTF_n", "OTF_5", "VWA_n", "VWA_5", "ACE_0", "EXT_r", "EXT_1", "CAL", "TA", "D", "TIPO", "NV", "ET"]
    mapa = {"APE_015": "APE_0.15", "OTF_5": "OTF_5", "VWA_5": "VWA_5", "EXT_1": "EXT_1", "CAL": "CAL_fomc"}
    dif = []
    for k in claves:
        rv = float(ref[mapa.get(k, k)]); mv = float(mio[k])
        if abs(rv - mv) > 1e-6: dif.append("%s propio %s / motor %s" % (k, mv, rv))
    P("  %-8s %s | s13 %+d APE q %.3f marca %d | OTF %d marca %d | VWA %d marca %d | ACE %d | EXT %.3f marca %d | FOMC %d || TA %d D %+.2f TIPO %d NV %d ET %d -> %s" % (
        nombre, dia, mio["s13"], mio["APE_q"], mio["APE_015"], mio["OTF_n"], mio["OTF_5"], mio["VWA_n"], mio["VWA_5"], mio["ACE_0"], mio["EXT_r"],
        mio["EXT_1"], mio["CAL"], mio["TA"], mio["D"], mio["TIPO"], mio["NV"], mio["ET"], "COINCIDE" if not dif else "DIFIERE: " + "; ".join(dif)))
    return not dif


def leer_ref(nombre):
    with open(os.path.join(AQUI, "salidas", "rasgos_%s.csv" % nombre), newline="", encoding="utf-8") as f:
        return {r["dia"]: r for r in csv.DictReader(f)}


def elegir(ref, desde, hasta):
    """por regla: la primera rueda marcada por cada detector en el periodo y la primera del FOMC (sin mirar resultados)."""
    ds = sorted(d for d in ref if desde <= d <= hasta)
    sel = []
    for col in ("APE_0.15", "OTF_5", "VWA_5", "ACE_0", "EXT_1", "CAL_fomc"):
        x = [d for d in ds if ref[d][col] == "1"]
        if x and x[0] not in sel: sel.append(x[0])
    return sel


def cinta_a_velas(dia):
    arch = None
    for f in sorted(os.listdir(FLUJO)):
        if f.startswith("cinta-ES-") and f.endswith(".csv") and os.path.exists(os.path.join(FLUJO, f + ".listo")):
            fin = f[:-4].split("-")[3][:8]
            if "%s-%s-%s" % (fin[:4], fin[4:6], fin[6:8]) == dia: arch = os.path.join(FLUJO, f)
    filas = set(); ords = []
    with open(arch, newline="") as fh:
        rd = csv.reader(fh); cab = next(rd); it = cab.index("t"); ip = cab.index("primero"); iu = cab.index("ultimo"); iv = cab.index("vol")
        for r in rd:
            tup = tuple(r)
            if tup in filas: continue
            filas.add(tup)
            t = datetime.strptime(r[it], "%Y-%m-%dT%H:%M:%S.%f").replace(tzinfo=timezone.utc).astimezone(NYZ)
            if (t.replace(tzinfo=None) + timedelta(hours=6)).strftime("%Y-%m-%d") != dia: continue
            seg = t.hour * 3600 + t.minute * 60 + t.second + t.microsecond / 1e6
            if 34200 <= seg < 57600: ords.append((seg, float(r[ip]), float(r[iu]), float(r[iv])))
    ords.sort(key=lambda x: x[0])
    b = []
    for j in range(13):
        s = [x for x in ords if 34200 + 1800 * j <= x[0] < 36000 + 1800 * j]
        b.append((s[0][1], max(max(x[1], x[2]) for x in s), min(min(x[1], x[2]) for x in s), s[-1][2], sum(x[3] for x in s)))
    return b


def a3_dia(bs5, X):
    """A3 en velas de 5 min (regla de la R23, escrita de nuevo): devuelve [(hora, lado, entrada, stop, salida, motivo, pts)]."""
    o = [x[1] for x in bs5]; h = [x[2] for x in bs5]; l = [x[3] for x in bs5]; c = [x[4] for x in bs5]; v = [x[5] for x in bs5]
    vw = []; spv = sv = 0.0
    for k in range(78):
        spv += (h[k] + l[k]) / 2 * v[k]; sv += v[k]; vw.append(spv / sv)
    ibh = max(h[:12]); ibl = min(l[:12]); ibr = ibh - ibl
    ops = []; libre = -1
    for k in range(41, 72):                                   # cierres de 13:00 (k = 41) a 15:30 (k = 71)
        if k <= libre or len(ops) >= 5: continue
        s = 1 if c[k] > vw[k] else (-1 if c[k] < vw[k] else 0)
        if s == 0 or abs(c[k] - vw[k]) / X < 1.5: continue
        Hk = max(h[:k + 1]); Lk = min(l[:k + 1])
        ext = max(0.0, (Hk - ibh) / ibr) if s > 0 else max(0.0, (ibl - Lk) / ibr)
        if ext > 1.0: continue
        stop = (Hk + tick(0.5 * X)) if s > 0 else (Lk - tick(0.5 * X))
        dst = abs(stop - c[k])
        if dst / (dst + abs(c[k] - vw[k])) < 0.55: continue
        lado = -s; ent = c[k]; sal = None
        for j in range(k + 1, 78):
            lim = tick(vw[j - 1])
            st = (l[j] <= stop) if lado > 0 else (h[j] >= stop)
            gap = (o[j] <= stop) if lado > 0 else (o[j] >= stop)
            ob = (h[j] >= lim + TK) if lado > 0 else (l[j] <= lim - TK)
            if st: sal = (j, o[j] if gap else stop, "stop"); break
            if ob: sal = (j, lim, "objetivo"); break
        if sal is None: sal = (77, c[77], "fin")
        j, px, mot = sal
        ops.append(("%02d:%02d" % divmod(575 + 5 * k, 60), "COMPRA" if lado > 0 else "VENTA", ent, stop, px, mot, (px - ent) * lado))
        libre = j
    return ops


def main():
    P("RONDA 25 - COMPROBACION DESDE EL CRUDO (codigo propio: csv + zoneinfo + math; sin numpy, pandas ni el motor)")
    ok = 0; tot = 0
    # ---- (a1) MES 30 min nativas, diseno
    d30 = leer_velas("velas-MES-m30.csv")
    comp30 = {d for d, bs in d30.items() if [b[0] for b in bs] == [570 + 30 * i for i in range(13)]}
    rango30 = historia(d30, comp30, 0)
    ref = leer_ref("diseno")
    P(""); P("(a) CLASIFICACIONES (ruedas elegidas por regla: la primera marcada por cada detector y la primera del FOMC)")
    for dia in elegir(ref, "2024-04-29", "2025-09-08"):
        med, X, ant = contexto(dia, rango30, d30)
        b = [(x[1], x[2], x[3], x[4], x[5]) for x in d30[dia]]
        tot += 1; ok += comparar("m30", dia, rasgos(b, med, X, ant, dia), ref[dia])
    # ---- (a2) MES 5 min agregadas a 30, validacion 1
    d5 = leer_velas("velas-MES-m5.csv")
    comp5 = {d for d, bs in d5.items() if [b[0] for b in bs] == [570 + 5 * i for i in range(78)]}
    rango5 = historia(d5, comp5, 70)
    ref = leer_ref("val1")
    for dia in elegir(ref, "2025-09-09", "2026-08-31"):
        med, X, ant = contexto(dia, rango5, d5)
        tot += 1; ok += comparar("m5->30", dia, rasgos(a30(d5[dia]), med, X, ant, dia), ref[dia])
    # ---- (a3) cinta de ES (orden por orden), validacion 2: dos ruedas
    sem = json.load(open(os.path.join(os.path.dirname(AQUI), "ronda23_diseno", "historia_rangos_semilla.json"), encoding="utf-8"))["rangos"]
    res19 = json.load(open(os.path.join(r"C:\Users\wmx_7\AppData\Local\Temp\claude\6e29e09c80928880\233bf8d4-5113-4e95-a0d9-68bff726690d\scratchpad\r19\hist", "resumen.json")))
    ref = leer_ref("val2")
    for dia in ("2026-10-01", "2026-11-05"):
        prev = sorted(x for x in sem if x < dia)[-10:]; v = sorted(float(sem[x]) for x in prev); med = (v[4] + v[5]) / 2.0
        X = math.floor(0.08 * med / TK + 0.5) * TK
        pa = sorted(k.split("|")[1] for k, x in res19.items() if k.startswith("ES|") and x.get("rth_h") is not None and k.split("|")[1] < dia)
        x = res19["ES|" + pa[-1]]; ant = (x["rth_h"], x["rth_l"])
        tot += 1; ok += comparar("cinta", dia, rasgos(cinta_a_velas(dia), med, X, ant, dia), ref[dia])
    P("  -> clasificaciones: %d de %d coinciden" % (ok, tot))
    # ---- (b) operaciones de A3 en velas de 5 min de MES, con la marca OTF de su dia
    P(""); P("(b) OPERACIONES DE A3 (velas de 5 min de MES, regla escrita de nuevo) y la marca del detector OTF de su dia")
    refs = {}
    sc = r"C:\Users\wmx_7\AppData\Local\Temp\claude\6e29e09c80928880\233bf8d4-5113-4e95-a0d9-68bff726690d\scratchpad\r23"
    for m in ("diseno", "chequeo"):
        with open(os.path.join(sc, "ops_%s_A_1300-1530_dv1.5_ible1+Pfgt0.55_F1.csv" % m), newline="") as f:
            refs[m] = list(csv.DictReader(f))
    r_val = leer_ref("val1"); ok2 = tot2 = 0
    for m in ("diseno", "chequeo"):
        rr = refs[m]
        for fila in (rr[0], rr[1], rr[-2], rr[-1]):
            dia = fila["dia"]; med, X, _ = contexto(dia, rango5, d5)
            mias = a3_dia(d5[dia], X)
            hora = "%02d:%02d" % divmod(int(fila["min_dia"]), 60)
            mia = [x for x in mias if x[0] == hora]
            tot2 += 1
            if mia and abs(mia[0][6] - float(fila["pts_real"])) < 1e-6:
                ok2 += 1; res = "COINCIDE"
            else:
                res = "DIFIERE (motor %+.2f)" % float(fila["pts_real"])
            x = mia[0] if mia else ("-",) * 7
            P("  %-7s %s %s %s a %.2f stop %.2f -> %s %.2f = %+.2f | OTF %s ese dia (A3F-dia %s) -> %s" % (
                m, dia, x[0], x[1], x[2], x[3], x[5], x[4], x[6], r_val[dia]["OTF_n"] + "/6" if dia in r_val else "?",
                "la saltea" if dia in r_val and r_val[dia]["OTF_5"] == "1" else "la toma", res))
    P("  -> operaciones de A3: %d de %d coinciden" % (ok2, tot2))
    open(os.path.join(AQUI, "salidas", "verificacion.txt"), "w", encoding="utf-8").write("\n".join(SALIDA))


if __name__ == "__main__":
    main()
