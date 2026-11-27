# -*- coding: utf-8 -*-
"""RONDA 26 - comun: ES contra MES sobre la MISMA pierna de la ola (X) y la marea (4X).

- Rutas, dias, prioridad baja y freno de RAM (2 GB).
- Lectura de la cinta de la sonda (orden del archivo, CON duplicados, como La Ola 1.0 y test_ola.referencia).
- Rangos RTH y X del ES: X = max(4, round(0,08 x mediana de los rangos RTH de las 10 ruedas previas / 0,25) x 0,25), con el round de
  Python (al par en el medio), como EscalaOla de OlaMotor.cs. Fuente de cada rango previo: la cinta de ES (si se leyo) > tabla diaria de la
  ronda 16 (ES 08-09..13-11, verificada por test_ola contra las cintas) > velas de 5 min de MES del cache de ATAS (antes del 08-09).
  Una rueda previa cuenta solo si su RTH cubre >= 6 h (como RuedasOla.Completa de La Ola).
- MaquinaI: COPIA de test_ola.Maquina con la misma logica y las mismas comparaciones, que ademas guarda el NUMERO DE ORDEN de cada extremo.
  correr() la maneja junto a la Maquina original y compara los pivotes uno por uno (tiene que dar identico).
- Fotos al cierre de cada minuto RTH (9:31:00 ... 16:00:00 NY): el estado con las ordenes de t < cierre (como OlaFotos y test_ola.Ventana).
"""
import csv
import glob
import json
import os
import sys
import time
from datetime import datetime, timedelta, timezone
from zoneinfo import ZoneInfo

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
LAB = os.path.dirname(AQUI)
RAIZ = os.path.dirname(LAB)
HERR = os.path.join(RAIZ, "herramientas")
FLUJO = os.path.join(os.environ.get("APPDATA", ""), "ATAS", "PythiaGex", "flujo")
DATOS_OFICIAL = os.path.join(RAIZ, "datos", "ronda26")
DATOS = os.environ.get("R26_DATOS") or DATOS_OFICIAL      # R26_DATOS: solo para la prueba de humo a ciegas (los rangos van siempre a la oficial)
R16_NIV = os.path.join(RAIZ, "datos", "ronda16", "niveles.parquet")
R17_NIV_MES = os.path.join(RAIZ, "datos", "ronda17", "niveles-MES.parquet")
VELAS_M5 = os.path.join(LAB, "dom", "ronda3", "velas", "velas-MES-m5.csv")
NY = ZoneInfo("America/New_York")
UTC = timezone.utc
TICK = 0.25
NMIN = 390                       # cierres de minuto RTH: 9:31:00 ... 16:00:00 NY
SEMILLA = 20261127

NOV = ["2026-11-12", "2026-11-13", "2026-11-16", "2026-11-17", "2026-11-18", "2026-11-19", "2026-11-20", "2026-11-23", "2026-11-24",
       "2026-11-25"]
SEP = ["2026-09-08", "2026-09-09", "2026-09-11", "2026-09-14", "2026-09-15", "2026-09-16", "2026-09-17"]
HOY = "2026-11-27"
CINTAS_HOY = {"ES": "cinta-ESZ6-202611262300-202611271649.csv", "MES": "cinta-MES-202611262300-202611271648.csv"}

# tamanos de la orden agresora (contratos del propio instrumento)
BORDES = {"ES": [2, 5, 10, 20, 50], "MES": [2, 5, 10, 20, 50, 100, 200]}
ETIQ = {"ES": ["1", "2-4", "5-9", "10-19", "20-49", "50+"], "MES": ["1", "2-4", "5-9", "10-19", "20-49", "50-99", "100-199", "200+"]}
COLS = ["t", "primero", "ultimo", "vol", "lado", "prints", "abid", "abidv", "aask", "aaskv", "dbid", "dbidv", "dask", "daskv"]


# ------------------------------------------------------------------------------------------------ prioridad y RAM
def prioridad_baja():
    try:
        import ctypes
        import ctypes.wintypes as W
        k = ctypes.windll.kernel32
        k.GetCurrentProcess.restype = W.HANDLE
        k.SetPriorityClass.argtypes = [W.HANDLE, W.DWORD]
        k.SetPriorityClass(k.GetCurrentProcess(), 0x00004000)   # BELOW_NORMAL
    except Exception:
        pass


def ram_libre_mb():
    import ctypes

    class M(ctypes.Structure):
        _fields_ = [("dwLength", ctypes.c_ulong), ("dwMemoryLoad", ctypes.c_ulong), ("ullTotalPhys", ctypes.c_ulonglong),
                    ("ullAvailPhys", ctypes.c_ulonglong), ("ullTotalPageFile", ctypes.c_ulonglong), ("ullAvailPageFile", ctypes.c_ulonglong),
                    ("ullTotalVirtual", ctypes.c_ulonglong), ("ullAvailVirtual", ctypes.c_ulonglong), ("ullAvailExtendedVirtual", ctypes.c_ulonglong)]
    m = M()
    m.dwLength = ctypes.sizeof(M)
    ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(m))
    return m.ullAvailPhys / 2 ** 20


def frenar_si_falta_ram(mb=2048):
    libre = ram_libre_mb()
    if libre < mb:
        print("FRENO: RAM libre %.0f MB (< %d MB). Lo que falta no se corrio." % (libre, mb), flush=True)
        raise SystemExit(3)
    return libre


# ------------------------------------------------------------------------------------------------ horas
def ms_ny(dia, hh, mm, mas_dias=0):
    d = datetime.strptime(dia, "%Y-%m-%d") + timedelta(days=mas_dias)
    return int(d.replace(hour=hh, minute=mm, tzinfo=NY).astimezone(UTC).timestamp() * 1000)


def ventana(dia):
    """[18:00 NY de la vispera, 17:00 NY) de la jornada de CME (el lunes: desde el domingo 18:00), como OlaNucleo.Rango."""
    return ms_ny(dia, 18, 0, -1), ms_ny(dia, 17, 0)


def rth(dia):
    return ms_ny(dia, 9, 30), ms_ny(dia, 16, 0)


def cortes_rth(dia):
    r0, _ = rth(dia)
    return [r0 + 60000 * (m + 1) for m in range(NMIN)]


def hora_ny(ms):
    return datetime.fromtimestamp(ms / 1000.0, UTC).astimezone(NY)


def txt_ny(ms):
    return hora_ny(ms).strftime("%H:%M:%S")


# ------------------------------------------------------------------------------------------------ cintas
def archivo(inst, dia):
    """La cinta de la jornada 'dia' (el 'hasta' del nombre es el dia): cinta-<inst>-<desde>-<dia><hh>00.csv con su .listo."""
    if dia == HOY:
        return os.path.join(FLUJO, CINTAS_HOY[inst])
    d = dia.replace("-", "")
    cand = [f for f in glob.glob(os.path.join(FLUJO, "cinta-%s-*-%s??00.csv" % (inst, d))) if os.path.exists(f + ".listo")]
    cand = [f for f in cand if os.path.basename(f).split("-")[1] == inst]
    return cand[0] if len(cand) == 1 else None


def leer(ruta):
    """La cinta completa, en el orden del archivo. Devuelve un DataFrame con ms (UTC) y las columnas numericas, mas los duplicados exactos
    (todas las columnas) y las filas fuera de orden (hora menor que la de la fila anterior), que solo se CUENTAN (no se sacan)."""
    d = pd.read_csv(ruta, dtype={c: "float64" for c in COLS[1:]})
    ms = pd.to_datetime(d["t"], format="%Y-%m-%dT%H:%M:%S.%f").values.astype("int64") // 1_000_000
    d.insert(0, "ms", ms)
    dup = int(d.duplicated(subset=COLS).sum())
    fuera = int((np.diff(ms) < 0).sum())
    d = d.drop(columns=["t"])
    return d, dup, fuera


def rango_rth_streaming(ruta, dia):
    """Rango RTH (9:30-16:00 NY) de la cinta sin cargarla: maximo y minimo de primero/ultimo, apertura y cierre, cobertura."""
    r0, r1 = rth(dia)
    s0 = datetime.fromtimestamp(r0 / 1000, UTC).strftime("%Y-%m-%dT%H:%M:%S.000")
    s1 = datetime.fromtimestamp(r1 / 1000, UTC).strftime("%Y-%m-%dT%H:%M:%S.000")
    hi, lo = -1e18, 1e18
    o = c = None
    t_pri = t_ult = None
    with open(ruta, newline="") as fh:
        r = csv.reader(fh)
        next(r)
        for x in r:
            t = x[0]
            if s0 <= t < s1:          # misma longitud y formato ISO: comparar texto = comparar horas
                a, b = float(x[1]), float(x[2])
                if a > hi: hi = a
                if b > hi: hi = b
                if a < lo: lo = a
                if b < lo: lo = b
                if o is None:
                    o, t_pri = a, t
                c, t_ult = b, t
    if o is None:
        return None
    f = lambda s: datetime.strptime(s[:23], "%Y-%m-%dT%H:%M:%S.%f").timestamp()
    return {"h": hi, "l": lo, "o": o, "c": c, "cubre_h": (f(t_ult) - f(t_pri)) / 3600.0, "fuente": "cinta-ES"}


# ------------------------------------------------------------------------------------------------ X del ES
def rangos_velas_mes():
    """Rango RTH por rueda de las velas de 5 min de MES (cache de ATAS, solo lectura; velas con apertura 9:30-15:55 NY)."""
    v = pd.read_csv(VELAS_M5, usecols=["t", "h", "l", "o", "c"])
    t = pd.to_datetime(v["t"], format="%Y-%m-%dT%H:%M:%S").dt.tz_localize("UTC").dt.tz_convert(NY)
    v["dia"] = t.dt.strftime("%Y-%m-%d")
    mins = t.dt.hour * 60 + t.dt.minute
    v = v[(mins >= 570) & (mins < 960)]
    g = v.groupby("dia")
    out = {}
    for dia, x in g:
        out[dia] = {"h": float(x["h"].max()), "l": float(x["l"].min()), "o": float(x["o"].iloc[0]), "c": float(x["c"].iloc[-1]),
                    "cubre_h": len(x) * 5 / 60.0, "fuente": "velas-MES-m5"}
    return out


def rangos_r16():
    d = pd.read_parquet(R16_NIV)
    return {r["dia"]: {"h": float(r["h"]), "l": float(r["l"]), "o": float(r["o"]), "c": float(r["c"]), "cubre_h": 6.5, "fuente": "r16-niveles"}
            for _, r in d.iterrows()}


def x_de(dia, rangos):
    """X del dia con las 10 ruedas previas completas (cubren >= 6 h). rangos = {dia: {...}} ya combinado por prioridad."""
    prev = sorted(d for d, r in rangos.items() if d < dia and r["cubre_h"] >= 6.0)[-10:]
    vals = [rangos[d]["h"] - rangos[d]["l"] for d in prev]
    med = float(np.median(vals))
    x = max(4.0, round(0.08 * med / TICK) * TICK)
    return x, med, [(d, rangos[d]["h"] - rangos[d]["l"], rangos[d]["fuente"]) for d in prev]


# ------------------------------------------------------------------------------------------------ el zigzag
sys.path.insert(0, HERR)
import test_ola as T   # noqa: E402  (la Maquina de referencia; al importarse pone prioridad baja)


class MaquinaI:
    """COPIA de test_ola.Maquina, misma logica y mismas comparaciones, que ademas guarda el numero de orden (i) de cada extremo:
    pivotes = (t_extremo, precio, tipo, t_confirmacion, cum_en_el_extremo, i_extremo, i_confirmacion)."""
    def __init__(s, X):
        s.X = X; s.dir = 0; s.ext = None; s.ext_t = None; s.ext_cum = 0; s.ext_i = -1
        s.lo = s.hi = None; s.lo_t = s.hi_t = None; s.lo_cum = s.hi_cum = 0; s.lo_i = s.hi_i = -1
        s.pivotes = []

    def paso(s, t, mn, mx, cum, i):
        if s.dir == 0:
            if s.lo is None or mn < s.lo: s.lo, s.lo_t, s.lo_cum, s.lo_i = mn, t, cum, i
            if s.hi is None or mx > s.hi: s.hi, s.hi_t, s.hi_cum, s.hi_i = mx, t, cum, i
            if s.hi - s.lo >= s.X:
                if s.hi_t > s.lo_t:
                    s.pivotes.append((s.lo_t, s.lo, 'min', t, s.lo_cum, s.lo_i, i)); s.dir = 1
                    s.ext, s.ext_t, s.ext_cum, s.ext_i = s.hi, s.hi_t, s.hi_cum, s.hi_i
                else:
                    s.pivotes.append((s.hi_t, s.hi, 'max', t, s.hi_cum, s.hi_i, i)); s.dir = -1
                    s.ext, s.ext_t, s.ext_cum, s.ext_i = s.lo, s.lo_t, s.lo_cum, s.lo_i
            return
        if s.dir == 1:
            if mx > s.ext: s.ext, s.ext_t, s.ext_cum, s.ext_i = mx, t, cum, i
            elif s.ext - mn >= s.X:
                s.pivotes.append((s.ext_t, s.ext, 'max', t, s.ext_cum, s.ext_i, i)); s.dir = -1
                s.ext, s.ext_t, s.ext_cum, s.ext_i = mn, t, cum, i
        else:
            if mn < s.ext: s.ext, s.ext_t, s.ext_cum, s.ext_i = mn, t, cum, i
            elif mx - s.ext >= s.X:
                s.pivotes.append((s.ext_t, s.ext, 'min', t, s.ext_cum, s.ext_i, i)); s.dir = 1
                s.ext, s.ext_t, s.ext_cum, s.ext_i = mx, t, cum, i


ESCALAS = (("X", 1.0), ("4X", 4.0))


def correr(ms, pri, ult, vol, lado, X, cortes):
    """Una pasada en el orden del archivo con la ola (X) y la marea (4X): MaquinaI y la Maquina original de test_ola en paralelo.
    Fotos en cada corte: estado con las ordenes de t < corte (se toma antes de pasar la primera orden de t >= corte).
    Devuelve (maquinas, fotos, L, verificacion): fotos[esc] = lista de (dir, ext, ext_i, n_pivotes); L = indice de la ultima orden de cada foto."""
    ms = ms.tolist(); pri = pri.tolist(); ult = ult.tolist(); vol = vol.tolist(); lado = lado.tolist()
    mq = {e: MaquinaI(f * X) for e, f in ESCALAS}
    rf = {e: T.Maquina(f * X) for e, f in ESCALAS}
    a1, a4 = mq["X"], mq["4X"]
    b1, b4 = rf["X"], rf["4X"]
    fotos = {"X": [], "4X": []}
    L = []
    nc = len(cortes)
    k = 0
    cum = 0.0
    n = len(ms)
    for i in range(n):
        t = ms[i]
        while k < nc and t >= cortes[k]:
            L.append(i - 1)
            fotos["X"].append((a1.dir, a1.ext, a1.ext_i, len(a1.pivotes)))
            fotos["4X"].append((a4.dir, a4.ext, a4.ext_i, len(a4.pivotes)))
            k += 1
        a, b = pri[i], ult[i]
        mn, mx = min(a, b), max(a, b)
        cum += vol[i] * lado[i]
        a1.paso(t, mn, mx, cum, i); a4.paso(t, mn, mx, cum, i)
        b1.paso(t, mn, mx, cum); b4.paso(t, mn, mx, cum)
    while k < nc:
        L.append(n - 1)
        fotos["X"].append((a1.dir, a1.ext, a1.ext_i, len(a1.pivotes)))
        fotos["4X"].append((a4.dir, a4.ext, a4.ext_i, len(a4.pivotes)))
        k += 1
    ver = {}
    for e in mq:
        m, r = mq[e], rf[e]
        iguales = len(m.pivotes) == len(r.pivotes) and all(p[:5] == q for p, q in zip(m.pivotes, r.pivotes))
        fin = (m.dir, m.ext, m.ext_t, m.ext_cum) == (r.dir, r.ext, r.ext_t, r.ext_cum)
        ver[e] = {"pivotes": len(m.pivotes), "identicos": bool(iguales and fin)}
    return mq, fotos, L, ver


def acumulados(vol, lado, inst):
    """Delta acumulado total y por tamano de orden (contratos del instrumento), en el orden del archivo."""
    sv = vol * lado
    k = np.digitize(vol, BORDES[inst])
    tot = np.cumsum(sv)
    por = [np.cumsum(np.where(k == j, sv, 0.0)) for j in range(len(ETIQ[inst]))]
    return tot, por


def procesar(inst, dia, X, ruta=None, hasta_ms=None):
    """Lee la cinta, corre ola y marea, arma fotos por minuto RTH, pivotes y lo de la cinta. Devuelve un dict serializable a disco."""
    ruta = ruta or archivo(inst, dia)
    d, dup, fuera = leer(ruta)
    w0, w1 = ventana(dia)
    if hasta_ms is not None:
        w1 = min(w1, hasta_ms)
    d = d[(d["ms"] >= w0) & (d["ms"] < w1)].reset_index(drop=True)
    ms = d["ms"].to_numpy(); pri = d["primero"].to_numpy(); ult = d["ultimo"].to_numpy()
    vol = d["vol"].to_numpy(); lado = d["lado"].to_numpy()
    cortes = cortes_rth(dia)
    if hasta_ms is not None:                 # cinta cortada (control de hoy): solo los cierres de minuto que la cinta cubre entera
        cortes = [c for c in cortes if c <= hasta_ms]
    t0 = time.time()
    mq, fotos, L, ver = correr(ms, pri, ult, vol, lado, X, cortes)
    t_motor = time.time() - t0
    tot, por = acumulados(vol, lado, inst)
    L = np.array(L, dtype=np.int64)
    Lc = np.clip(L, 0, None)
    bid_d, ask_d = d["dbid"].to_numpy(), d["dask"].to_numpy()
    mid_d = np.where((bid_d > 0) & (ask_d > bid_d), (bid_d + ask_d) / 2.0, np.nan)   # punta de despues de la orden; sin punta valida: NaN
    fot = pd.DataFrame({"m": np.arange(len(cortes)), "corte_ms": np.array(cortes, dtype=np.int64), "L": L,
                        "ultimo": np.where(L >= 0, ult[Lc], np.nan), "cum": np.where(L >= 0, tot[Lc], 0.0),
                        "mid": np.where(L >= 0, mid_d[Lc], np.nan), "t_ult": np.where(L >= 0, ms[Lc], 0)})
    for j, et in enumerate(ETIQ[inst]):
        fot["cum_" + et] = np.where(L >= 0, por[j][Lc], 0.0)
    for e, _ in ESCALAS:
        f = np.array(fotos[e], dtype=object)
        fot["dir_" + e] = f[:, 0].astype(int)
        fot["ext_" + e] = np.array([np.nan if x is None else float(x) for x in f[:, 1]])
        fot["exti_" + e] = f[:, 2].astype(np.int64)
        fot["npiv_" + e] = f[:, 3].astype(np.int64)
    piv = {}
    for e, _ in ESCALAS:
        m = mq[e]
        p = pd.DataFrame(m.pivotes, columns=["t_ext", "precio", "tipo", "t_conf", "cum", "i_ext", "i_conf"])
        p["tipo"] = np.where(p["tipo"] == "max", 1, -1)
        ie = p["i_ext"].to_numpy(dtype=np.int64)
        for j, et in enumerate(ETIQ[inst]):
            p["cum_" + et] = por[j][ie] if len(ie) else []
        p["cum_np"] = tot[ie] if len(ie) else []
        piv[e] = p
        piv[e + "_final"] = {"dir": m.dir, "ext": m.ext, "ext_t": m.ext_t, "ext_i": m.ext_i}
    # segundos RTH: ultimo precio de cada segundo (para alinear ES con MES)
    r0, r1 = rth(dia)
    en = (ms >= r0) & (ms < r1)
    seg = ((ms[en] - r0) // 1000).astype(np.int64)
    ult_seg = np.full(23400, np.nan)
    if len(seg):
        rev = seg[::-1]
        u, primero_rev = np.unique(rev, return_index=True)
        ult_seg[u] = ult[en][::-1][primero_rev]
    # cobertura RTH
    t_rth = ms[en]
    gap = float(np.max(np.diff(t_rth)) / 1000.0) if len(t_rth) > 1 else np.nan
    meta = {"inst": inst, "dia": dia, "archivo": os.path.basename(ruta), "X": X, "ordenes_ventana": int(len(d)), "duplicados_exactos": dup,
            "fuera_de_orden": fuera, "verificacion": ver, "seg_motor": round(t_motor, 1),
            "rth_primera": txt_ny(int(t_rth[0])) if len(t_rth) else None, "rth_ultima": txt_ny(int(t_rth[-1])) if len(t_rth) else None,
            "rth_hueco_max_s": gap, "rth_h": float(np.max(np.maximum(pri[en], ult[en]))) if len(t_rth) else None,
            "rth_l": float(np.min(np.minimum(pri[en], ult[en]))) if len(t_rth) else None,
            "rth_o": float(pri[en][0]) if len(t_rth) else None, "rth_c": float(ult[en][-1]) if len(t_rth) else None,
            "vol_rth": float(vol[en].sum()), "ordenes_rth": int(en.sum())}
    if len(t_rth):
        meta["rth_cubre_h"] = (t_rth[-1] - t_rth[0]) / 3.6e6
    return {"meta": meta, "fotos": fot, "piv": piv, "ult_seg": ult_seg}


def guardar(res):
    m = res["meta"]
    base = os.path.join(DATOS, "%s-%s" % (m["inst"], m["dia"]))
    res["fotos"].to_parquet(base + "-fotos.parquet", index=False)
    for e, _ in ESCALAS:
        res["piv"][e].to_parquet(base + "-piv-%s.parquet" % e, index=False)
        m["final_" + e] = {k: (float(v) if isinstance(v, (float, np.floating)) else (int(v) if v is not None else None))
                           for k, v in res["piv"][e + "_final"].items()}
    np.save(base + "-seg.npy", res["ult_seg"])
    json.dump(m, open(base + "-meta.json", "w"), indent=1, default=float)


def cargar(inst, dia):
    base = os.path.join(DATOS, "%s-%s" % (inst, dia))
    if not os.path.exists(base + "-meta.json"):
        return None
    m = json.load(open(base + "-meta.json"))
    return {"meta": m, "fotos": pd.read_parquet(base + "-fotos.parquet"),
            "piv": {e: pd.read_parquet(base + "-piv-%s.parquet" % e) for e, _ in ESCALAS}, "ult_seg": np.load(base + "-seg.npy")}


FACTOR = dict(ESCALAS)
CAJAS = [0.0, 0.25, 0.5, 0.75]          # r/Xs: [0,0.25) [0.25,0.5) [0.5,0.75) [0.75, inf)
ETIQ_CAJA = ["0-0,25", "0,25-0,5", "0,5-0,75", "0,75-1"]
CHICOS = ["1", "2-4", "5-9"]
MEDIANOS = ["10-19", "20-49"]


def _lado(R, esc, Xs, pref):
    """Columnas de un instrumento al cierre de cada minuto: direccion, origen, delta de la pierna (total y por tamano), color, retroceso y
    desenlace de SU zigzag ('sigue' = la pierna hace un extremo >= Xs/2 mas alla del extremo actual antes de confirmar el giro)."""
    F, P, fin = R["fotos"], R["piv"][esc], R["meta"]["final_" + esc]
    inst = R["meta"]["inst"]
    dirr = F["dir_" + esc].to_numpy()
    npiv = F["npiv_" + esc].to_numpy()
    ext = F["ext_" + esc].to_numpy()
    ult = F["ultimo"].to_numpy()
    hay = (dirr != 0) & (npiv >= 1)
    idx = np.clip(npiv - 1, 0, max(len(P) - 1, 0))
    o = {}
    o[pref + "dir"] = np.where(hay, dirr, 0)
    o[pref + "npiv"] = npiv
    o[pref + "o_t"] = P["t_ext"].to_numpy()[idx] if len(P) else np.zeros(len(F), np.int64)
    o[pref + "o_p"] = P["precio"].to_numpy()[idx] if len(P) else np.full(len(F), np.nan)
    o[pref + "ext"] = ext
    o[pref + "ult"] = ult
    o[pref + "mid"] = F["mid"].to_numpy()
    delta = F["cum"].to_numpy() - (P["cum"].to_numpy()[idx] if len(P) else 0.0)
    o[pref + "delta"] = np.where(hay, delta, np.nan)
    o[pref + "con"] = hay & (np.sign(delta) == dirr)
    for et in ETIQ[inst]:
        o[pref + "d_" + et] = np.where(hay, F["cum_" + et].to_numpy() - (P["cum_" + et].to_numpy()[idx] if len(P) else 0.0), np.nan)
    o[pref + "r"] = np.where(hay, np.abs(ext - ult) / Xs, np.nan)
    # desenlace: el pivote que se confirma despues de la foto es el extremo FINAL de la pierna en curso
    precios = P["precio"].to_numpy()
    hay_sig = npiv < len(P)
    final = np.where(hay_sig, precios[np.clip(npiv, 0, max(len(P) - 1, 0))] if len(P) else np.nan, fin["ext"] if fin["ext"] is not None else np.nan)
    mas = dirr * (final - ext)
    sig = mas >= Xs / 2.0
    o[pref + "sigue"] = sig
    o[pref + "resuelto"] = hay & (hay_sig | sig)
    o[pref + "mas"] = np.where(hay, mas / Xs, np.nan)
    o[pref + "final_conf"] = hay_sig
    return o


def armar_minutos(dia, esc, X):
    """Un renglon por cierre de minuto RTH con lo del ES (e_) y lo del MES (m_) en la escala esc (X o 4X), con la X del ES."""
    E, M = cargar("ES", dia), cargar("MES", dia)
    n = min(len(E["fotos"]), len(M["fotos"]))
    E["fotos"] = E["fotos"].iloc[:n].reset_index(drop=True)
    M["fotos"] = M["fotos"].iloc[:n].reset_index(drop=True)
    Xs = FACTOR[esc] * X
    d = pd.DataFrame({"dia": dia, "esc": esc, "m": E["fotos"]["m"].to_numpy(), "corte_ms": E["fotos"]["corte_ms"].to_numpy()})
    d["min_ny"] = 570 + d["m"]                        # minuto del dia NY en que EMPIEZA el minuto que cierra en el corte
    for k, v in _lado(E, esc, Xs, "e_").items():
        d[k] = v
    for k, v in _lado(M, esc, Xs, "m_").items():
        d[k] = v
    d["Xs"] = Xs
    d["elegible"] = (d["e_dir"] != 0) & (d["e_dir"] == d["m_dir"])
    d["clase"] = np.where(d["e_con"], np.where(d["m_con"], "CC", "CS"), np.where(d["m_con"], "SC", "SS"))
    d["caja"] = np.clip(np.searchsorted(CAJAS, d["e_r"].fillna(0).to_numpy(), side="right") - 1, 0, 3)
    d["pierna"] = dia + "|" + esc + "|" + d["e_npiv"].astype(str)
    d["mismo_origen"] = (np.abs(d["e_o_t"] - d["m_o_t"]) <= 120000) & (np.abs(d["e_o_p"] - d["m_o_p"]) <= 0.5)
    s = d["e_dir"].to_numpy()
    d["e_d_chicos"] = sum(d["e_d_" + k] for k in CHICOS)
    d["e_d_medianos"] = sum(d["e_d_" + k] for k in MEDIANOS)
    d["comb"] = d["e_delta"] + d["m_delta"] / 10.0
    # rasgos de la P3 (fijados en el PREREGISTRO), todos con datos hasta el cierre del minuto
    d["R0"] = d["e_con"]
    d["V1_comb"] = s * d["comb"] > 0
    d["V2_medianos"] = s * d["e_d_medianos"] > 0
    d["V3_chicos"] = s * d["e_d_chicos"] > 0
    d["V4_mes"] = d["m_con"]
    d["V5_brecha"] = s * (d["m_delta"] / 10.0 - d["e_delta"]) > 0
    d["V6_patron"] = (s * d["e_d_chicos"] > 0) & (s * d["e_d_medianos"] < 0)
    d["prima_ticks"] = s * (d["m_mid"] - d["e_mid"]) / TICK
    d["geo"] = np.clip(1.0 - d["e_r"], 0, None) * np.exp(-0.5)
    return d


def emparejar(pe, pm, tol_ms=120000, tol_p=0.5):
    """Cada pivote del ES con el del MES del mismo tipo, a <= 120 s y <= 2 ticks, el mas cercano en hora; uno a uno, en el orden del ES."""
    te, tm = pe["t_ext"].to_numpy(), pm["t_ext"].to_numpy()
    ke, km = pe["tipo"].to_numpy(), pm["tipo"].to_numpy()
    xe, xm = pe["precio"].to_numpy(), pm["precio"].to_numpy()
    usado = np.zeros(len(pm), bool)
    out = np.full(len(pe), -1, np.int64)
    for i in range(len(pe)):
        c = np.where(~usado & (km == ke[i]) & (np.abs(tm - te[i]) <= tol_ms) & (np.abs(xm - xe[i]) <= tol_p))[0]
        if len(c):
            j = c[np.argmin(np.abs(tm[c] - te[i]))]
            out[i] = j
            usado[j] = True
    return out


def armar_piernas(dia, esc):
    """Piernas CONFIRMADAS del ES (de un pivote al siguiente) que terminan en la rueda (extremo final 9:30-16:00 NY), con la del MES si los
    dos pivotes emparejan con dos pivotes SEGUIDOS del MES. Delta por tamano desde el extremo de origen (sin contarlo) hasta el final."""
    E, M = cargar("ES", dia), cargar("MES", dia)
    pe, pm = E["piv"][esc], M["piv"][esc]
    r0, r1 = rth(dia)
    mt = emparejar(pe, pm)
    filas = []
    for j in range(len(pe) - 1):
        a, b = pe.iloc[j], pe.iloc[j + 1]
        if not (r0 <= b["t_ext"] < r1):
            continue
        s = -int(a["tipo"])
        f = {"dia": dia, "esc": esc, "j": j, "dir": s, "t0": int(a["t_ext"]), "t1": int(b["t_ext"]), "p0": a["precio"], "p1": b["precio"],
             "recorrido": abs(b["precio"] - a["precio"]), "min_fin": int((hora_ny(int(b["t_ext"])).hour * 60 + hora_ny(int(b["t_ext"])).minute)),
             "e_delta": b["cum"] - a["cum"]}
        for et in ETIQ["ES"]:
            f["e_d_" + et] = b["cum_" + et] - a["cum_" + et]
        ja, jb = mt[j], mt[j + 1]
        f["emparejada"] = bool(ja >= 0 and jb == ja + 1)
        if f["emparejada"]:
            x, y = pm.iloc[ja], pm.iloc[jb]
            f["m_delta"] = y["cum"] - x["cum"]
            for et in ETIQ["MES"]:
                f["m_d_" + et] = y["cum_" + et] - x["cum_" + et]
            f["m_dt0_s"] = (x["t_ext"] - a["t_ext"]) / 1000.0
            f["m_dt1_s"] = (y["t_ext"] - b["t_ext"]) / 1000.0
        filas.append(f)
    d = pd.DataFrame(filas)
    if len(d):
        d["e_con"] = np.sign(d["e_delta"]) == d["dir"]
        if "m_delta" in d:
            d["m_con"] = np.sign(d["m_delta"]) == d["dir"]
            d["clase"] = np.where(~d["emparejada"], "--", np.where(d["e_con"], np.where(d["m_con"], "CC", "CS"),
                                                                    np.where(d["m_con"], "SC", "SS")))
        else:
            d["clase"] = "--"
    return d


def alineacion(seg_es, seg_mes):
    """ES - MES en los segundos RTH en que operaron los dos (ultimo precio de cada segundo)."""
    ok = ~np.isnan(seg_es) & ~np.isnan(seg_mes)
    if ok.sum() < 100:
        return {"segundos": int(ok.sum()), "alinea": False}
    d = seg_es[ok] - seg_mes[ok]
    a = np.abs(d)
    return {"segundos": int(ok.sum()), "mediana_dif": float(np.median(d)), "mediana_abs": float(np.median(a)),
            "p95_abs": float(np.percentile(a, 95)), "max_abs": float(a.max()),
            "alinea": bool(np.median(a) <= 0.50 and np.percentile(a, 95) <= 1.00)}
