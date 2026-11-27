# -*- coding: utf-8 -*-
"""RONDA 20 - comun: rutas, prioridad baja, RAM, cadenas de CBOE (tres archivos del mismo CDN), gamma de Black-Scholes,
calendario de ruedas y muestras. Solo LEE: cadenas archivadas, cintas compactas y rasgos de la ronda 19 (scratchpad\\r19).
Nada toca ATAS. Los datos grandes van al scratchpad de la sesion."""
import datetime as dt
import glob
import gzip
import json
import os
from zoneinfo import ZoneInfo

import numpy as np
import pandas as pd

try:   # PRIORIDAD BAJA (BELOW_NORMAL) con tipos declarados (sin restype/argtypes falla en silencio, medido 06-10)
    import ctypes
    import ctypes.wintypes as _W
    _k = ctypes.windll.kernel32
    _k.GetCurrentProcess.restype = _W.HANDLE
    _k.SetPriorityClass.argtypes = [_W.HANDLE, _W.DWORD]
    _k.SetPriorityClass(_k.GetCurrentProcess(), 0x00004000)
except Exception:
    pass

NY = ZoneInfo("America/New_York")
UTC = dt.timezone.utc
AQUI = os.path.dirname(os.path.abspath(__file__))
LAB = os.path.dirname(AQUI)
RAIZ = os.path.dirname(LAB)
SCR_BASE = r"C:\Users\wmx_7\AppData\Local\Temp\claude\6e29e09c80928880\233bf8d4-5113-4e95-a0d9-68bff726690d\scratchpad"
SCR = os.path.join(SCR_BASE, "r20")
R19 = os.path.join(SCR_BASE, "r19")
RAS19 = os.path.join(R19, "rasgos")          # rasgos por rueda de la R19 (X, apertura, ruido, cierre por segundo)
CINTA19 = os.path.join(R19, "cinta")         # cintas compactas de la R19 (una fila por orden agresora, sin duplicados)
APP = os.environ.get("APPDATA", "")
FUENTES = {
    "git": os.path.join(LAB, "ronda14", "I_gamma", "datos", "cadenas"),     # copia de la rama `cadenas` (nube, cada 8-25 min)
    "local": os.path.join(APP, "ATAS", "PythiaGex", "cboe-local"),          # cboe_local.py, cada 75 s en la rueda
    "p5": os.path.join(APP, "ATAS", "PythiaGex5", "cboe"),                  # PythiaGex 5.0, cada ~75 s
}
ANIO = 365.0 * 86400.0
RQ = 0.034                 # r - q (memoria calcular-gex-propio; misma constante que la ronda 14)
MULT_SPX = 100.0           # multiplicador de las opciones de SPX (memoria gex-formula-auditada)
TICK = 0.25
for _d in (SCR,):
    os.makedirs(_d, exist_ok=True)

# ------------------------------------------------------------------ muestras (fijas, PREREGISTRO.md)
DISENO = ("2026-09-08", "2026-10-15")
CONFIRMA = ("2026-10-16", "2026-11-13")
GUARDADA = ("2026-11-16", "2026-11-25")
OPEX_CONTAMINADO = {"2026-09-18", "2026-10-16"}   # tercer viernes: la cadena junta la serie AM (vencida a las 9:30) con la PM
ROLL_RESET = "2026-09-14"                          # primera rueda de la cinta en Z6 (R19: U6 hasta el 11-09)


def muestra_de(dia):
    if DISENO[0] <= dia <= DISENO[1]:
        return "diseno"
    if CONFIRMA[0] <= dia <= CONFIRMA[1]:
        return "confirma"
    if GUARDADA[0] <= dia <= GUARDADA[1]:
        return "guardada"
    return "otra"


def dias_es():
    """Ruedas de ES con rasgos de la R19 (las 57: 08-09..13-11 y 16..25-11)."""
    return sorted(os.path.basename(f)[3:13] for f in glob.glob(os.path.join(RAS19, "ES-*.json")))


def meta19(dia):
    return json.load(open(os.path.join(RAS19, "ES-%s.json" % dia)))


def rasgos19(dia, claves=None):
    z = np.load(os.path.join(RAS19, "ES-%s.npz" % dia))
    ks = z.files if claves is None else claves
    return {k: z[k] for k in ks}


def cinta19(dia):
    return pd.read_parquet(os.path.join(CINTA19, "ES-%s.parquet" % dia), columns=["ns", "ny_s", "ultimo", "vol", "lado"])


def rth_utc(dia):
    """(inicio, fin) de la rueda 9:30-16:00 NY del dia, en UTC aware."""
    d = dt.date.fromisoformat(dia)
    a = dt.datetime(d.year, d.month, d.day, 9, 30, tzinfo=NY).astimezone(UTC)
    b = dt.datetime(d.year, d.month, d.day, 16, 0, tzinfo=NY).astimezone(UTC)
    return a, b


def ram_libre_gb():
    try:
        class M(ctypes.Structure):
            _fields_ = [("l", ctypes.c_ulong), ("p", ctypes.c_ulong), ("tot", ctypes.c_ulonglong), ("av", ctypes.c_ulonglong),
                        ("a", ctypes.c_ulonglong), ("b", ctypes.c_ulonglong), ("c", ctypes.c_ulonglong), ("d", ctypes.c_ulonglong),
                        ("e", ctypes.c_ulonglong)]
        m = M()
        m.l = ctypes.sizeof(M)
        ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(m))
        return m.av / 2 ** 30
    except Exception:
        return 99.0


def frenar_si_falta_ram(minimo=2.0):
    g = ram_libre_gb()
    if g < minimo:
        raise SystemExit("RAM libre %.2f GB < %.1f GB: freno (regla del operador)" % (g, minimo))
    return g


# ------------------------------------------------------------------ cadenas
def _leer_gz(fn):
    out = []
    try:
        with gzip.open(fn, "rt", encoding="utf-8") as f:
            for l in f:
                l = l.strip()
                if l:
                    try:
                        out.append(json.loads(l))
                    except Exception:
                        pass
    except (EOFError, OSError):
        pass        # miembro gzip cortado al final (archivo que se estaba escribiendo): se queda lo leido
    return out


def _venc_utc(f):
    d = dt.date.fromisoformat(f)
    return dt.datetime(d.year, d.month, d.day, 16, 0, tzinfo=NY).astimezone(UTC)   # el archivo no distingue AM: todo a las 16:00


def fotos_de_fechas(fechas_utc, solo_ultima_antes=None):
    """Fotos de cadena SPX de los archivos de esas fechas UTC (las tres fuentes), deduplicadas por sello de CBOE, quedandose con
    la PRIMERA publicacion (el 'generado' mas temprano = cuando el dato estuvo disponible por primera vez)."""
    L = []
    for fe in fechas_utc:
        for fu, carpeta in FUENTES.items():
            p = os.path.join(carpeta, "cadena-ES-%s.jsonl.gz" % fe)
            if os.path.exists(p):
                L += [(x, fu) for x in _leer_gz(p)]
    L.sort(key=lambda z: z[0].get("generado") or "")
    vistos, fotos = set(), []
    for x, fu in L:
        c = x.get("cadena") or {}
        sello = x.get("cadena_ts") or c.get("ts")
        if not sello or sello in vistos:
            continue
        filas = c.get("filas") or []
        if not filas:
            continue
        try:
            gen = dt.datetime.fromisoformat(x["generado"]).astimezone(UTC)
            ut = c.get("ultimo_trade")
            dato = dt.datetime.fromisoformat(ut).replace(tzinfo=NY).astimezone(UTC) if ut else None
        except Exception:
            continue
        if solo_ultima_antes is not None and gen > solo_ultima_antes:
            continue
        vistos.add(sello)
        vencs = [v["f"] for v in c.get("vencimientos") or []]
        fotos.append({"gen": gen, "dato": dato, "sello": sello, "spot_idx": c.get("spot_idx") or x.get("spot"),
                      "base_arch": x.get("base"), "base_conf": x.get("base_confiable"), "vencs": vencs,
                      "venc_utc": [_venc_utc(f) for f in vencs], "filas": np.asarray(filas, dtype=float), "fuente": fu})
    fotos.sort(key=lambda z: z["gen"])
    return fotos


def fotos_para_rueda(dia):
    """Todas las fotos publicadas el dia UTC de la rueda, mas la ULTIMA publicada antes (de los 4 dias previos), para la apertura."""
    d = dt.date.fromisoformat(dia)
    previas = [(d - dt.timedelta(days=k)).isoformat() for k in (4, 3, 2, 1)]
    a, _ = rth_utc(dia)
    medianoche = dt.datetime(d.year, d.month, d.day, tzinfo=UTC)
    prev = [f for f in fotos_de_fechas(previas) if f["gen"] < medianoche]
    hoy = fotos_de_fechas([dia])
    sellos = {f["sello"] for f in hoy}
    prev = [f for f in prev if f["sello"] not in sellos]
    out = (prev[-1:] if prev else []) + hoy
    out.sort(key=lambda z: z["gen"])
    return out


# ------------------------------------------------------------------ Black-Scholes
def gamma_bs(S, K, T, sig):
    """Gamma de Black-Scholes por unidad de subyacente (vectorizada; T en anios; piso de 2 min como la ronda 14)."""
    T = np.maximum(T, 120.0 / ANIO)
    sig = np.where((sig > 0.005) & (sig < 3.0), sig, np.nan)      # IV invalida afuera (memoria calcular-gex-propio)
    v = sig * np.sqrt(T)
    d1 = (np.log(S / K) + (RQ + 0.5 * sig * sig) * T) / v
    g = np.exp(-0.5 * d1 * d1) / (np.sqrt(2 * np.pi) * S * v)
    return np.nan_to_num(g, nan=0.0)


class BaseCausal:
    """Base ES - SPX causal (igual que la ronda 14): mediana de las ultimas 24 muestras publicadas; una muestra que se aparta mas de
    8 pts de la mediana queda pendiente y si llegan 3 coherentes (rango < 4) se reinicia (roll o muestra rota)."""
    def __init__(s):
        s.buf, s.pend = [], []

    def agregar(s, x):
        if len(s.buf) >= 3:
            med = np.median(s.buf)
            if abs(x - med) > 8:
                s.pend.append(x)
                if len(s.pend) >= 3 and np.ptp(s.pend[-3:]) < 4:
                    s.buf, s.pend = list(s.pend[-3:]), []
                return
        s.pend = []
        s.buf.append(x)
        s.buf = s.buf[-24:]

    def valor(s):
        return float(np.median(s.buf)) if s.buf else np.nan

    def n(s):
        return len(s.buf)


def t_por_rueda(valores_por_rueda):
    v = np.asarray([x for x in valores_por_rueda if np.isfinite(x)], dtype=float)
    if len(v) < 3 or v.std(ddof=1) == 0:
        return np.nan
    return float(v.mean() / (v.std(ddof=1) / np.sqrt(len(v))))


def t_cluster(y, x, g):
    """t de la pendiente de y ~ a + b x con error agrupado por rueda (CR1). x binario o continuo."""
    y = np.asarray(y, float); x = np.asarray(x, float); g = np.asarray(g)
    m = np.isfinite(y) & np.isfinite(x)
    y, x, g = y[m], x[m], g[m]
    if len(y) < 5 or np.unique(g).size < 3 or np.std(x) == 0:
        return np.nan, np.nan
    Xm = np.column_stack([np.ones_like(x), x])
    XtX = np.linalg.inv(Xm.T @ Xm)
    b = XtX @ Xm.T @ y
    e = y - Xm @ b
    meat = np.zeros((2, 2))
    gs = np.unique(g)
    for k in gs:
        i = g == k
        u = Xm[i].T @ e[i]
        meat += np.outer(u, u)
    G, N = len(gs), len(y)
    V = XtX @ meat @ XtX * (G / (G - 1)) * ((N - 1) / (N - 2))
    return float(b[1]), float(b[1] / np.sqrt(V[1, 1]))
