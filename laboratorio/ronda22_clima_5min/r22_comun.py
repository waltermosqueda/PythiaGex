# -*- coding: utf-8 -*-
"""RONDA 22 - comun: rutas, prioridad BAJA, freno por RAM, periodos, curva f(m) congelada de la R21, registro de variantes y
estadisticos (t por rueda, bootstrap agrupado por rueda). Nada toca ATAS ni la red.
La carpeta de velas (laboratorio/dom/ronda3/velas) es IRREMPLAZABLE: aca solo se LEE (nunca se abre en modo escritura)."""
import csv, hashlib, json, math, os, sys, time
import numpy as np, pandas as pd

try:   # PRIORIDAD BAJA (BELOW_NORMAL) con tipos declarados (sin restype/argtypes falla en silencio, medido 06-10)
    import ctypes, ctypes.wintypes as _W
    _k = ctypes.windll.kernel32; _k.GetCurrentProcess.restype = _W.HANDLE; _k.SetPriorityClass.argtypes = [_W.HANDLE, _W.DWORD]
    _k.SetPriorityClass(_k.GetCurrentProcess(), 0x00004000)
except Exception:
    pass

AQUI = os.path.dirname(os.path.abspath(__file__))
LAB = os.path.dirname(AQUI)
VELAS = os.path.join(LAB, "dom", "ronda3", "velas")                 # SOLO LECTURA
M5 = os.path.join(VELAS, "velas-MES-m5.csv")
M30 = os.path.join(VELAS, "velas-MES-m30.csv")
R21 = os.path.join(LAB, "ronda21_regimen")
CURVA = os.path.join(R21, "curva_rango_por_minuto.json")
CURVA_SHA = "72589182d1533ccab0903e70469e8654dc7a201197d88064238dd1a2d7a6bc33"
SAL = os.path.join(AQUI, "salidas")
SCR = r"C:\Users\wmx_7\AppData\Local\Temp\claude\6e29e09c80928880\233bf8d4-5113-4e95-a0d9-68bff726690d\scratchpad\r22"
SCR19 = os.path.join(os.path.dirname(SCR), "r19")
SCR21 = os.path.join(os.path.dirname(SCR), "r21")
for _d in (SAL, SCR):
    os.makedirs(_d, exist_ok=True)

TK = 0.25
NY = "America/New_York"
INDEP = ("2025-08-21", "2026-08-31")       # periodo independiente (las 10 primeras ruedas completas = calentamiento)
CALIB = ("2026-09-07", "2026-09-21")       # solapamiento con las cintas de ES (calibracion de la traduccion)
H1B = ("2024-04-14", "2025-08-20")         # H1b: velas de 30 min ANTERIORES al periodo de 5 min (nunca usadas)
REGISTRO = os.path.join(AQUI, "registro_variantes.csv")
CAMPOS = ["fecha_hora_AR", "etapa", "familia", "variante", "salida", "parametros", "muestra", "corte", "ruedas", "n",
          "ops_por_rueda", "acierto_pct", "neto_op_c050", "neto_op_c100", "t_rueda_c050", "t_rueda_c100", "t_bonferroni",
          "ruedas_positivas_c100", "placebo_p", "pasa", "nota"]


def ram_libre_gb():
    try:
        class Mem(ctypes.Structure):
            _fields_ = [("l", ctypes.c_ulong), ("p", ctypes.c_ulong), ("tot", ctypes.c_ulonglong), ("av", ctypes.c_ulonglong),
                        ("a", ctypes.c_ulonglong), ("b", ctypes.c_ulonglong), ("c", ctypes.c_ulonglong), ("d", ctypes.c_ulonglong),
                        ("e", ctypes.c_ulonglong)]
        m = Mem(); m.l = ctypes.sizeof(Mem); ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(m))
        return m.av / 2 ** 30
    except Exception:
        return 99.0


def frenar_si_falta_ram(minimo=2.0):
    g = ram_libre_gb()
    if g < minimo:
        raise SystemExit("RAM libre %.2f GB < %.1f GB: freno (regla del operador)" % (g, minimo))
    return g


def sha256(ruta):
    h = hashlib.sha256()
    with open(ruta, "rb") as f:
        for b in iter(lambda: f.read(1 << 20), b""): h.update(b)
    return h.hexdigest()


def curva_f():
    """f(m), m = 0..389 (minuto desde las 9:30; el valor de m es el del CIERRE de ese minuto). Congelada en la R21."""
    if sha256(CURVA) != CURVA_SHA:
        raise SystemExit("la curva f(m) de la R21 cambio: no se corre")
    return np.asarray(json.load(open(CURVA))["f"], float)


def trimestre(dia):
    return "%sQ%d" % (dia[:4], (int(dia[5:7]) - 1) // 3 + 1)


def anotar(fila):
    nuevo = not os.path.exists(REGISTRO)
    with open(REGISTRO, "a", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=CAMPOS, extrasaction="ignore")
        if nuevo: w.writeheader()
        fila = dict(fila); fila.setdefault("fecha_hora_AR", time.strftime("%Y-%m-%d %H:%M"))
        w.writerow({k: fila.get(k, "") for k in CAMPOS})


def t_por_rueda(serie):
    serie = np.asarray(serie, float)
    if len(serie) < 2: return 0.0
    sd = serie.std(ddof=1)
    return float(serie.mean() / (sd / math.sqrt(len(serie)))) if sd > 0 else 0.0


def t_bonferroni(k, ruedas, alfa=0.05):
    """t critico de una cola con correccion de Bonferroni por k variantes (gl = ruedas - 1)."""
    from scipy import stats
    return float(stats.t.ppf(1 - alfa / k, max(ruedas - 1, 1)))


def boot_ruedas(dias, num, den, num2=None, den2=None, B=2000, semilla=2201):
    """IC 95 % agrupado por rueda (remuestreo de ruedas con reposicion) de num/den (y de la diferencia num/den - num2/den2).
    dias: etiqueta de rueda por fila; num/den: arrays por fila (p. ej. exito y 1). Devuelve (lo, hi) de la proporcion o de la diferencia."""
    rng = np.random.default_rng(semilla)
    u, inv = np.unique(np.asarray(dias), return_inverse=True)
    S1 = np.bincount(inv, weights=np.asarray(num, float), minlength=len(u)); D1 = np.bincount(inv, weights=np.asarray(den, float), minlength=len(u))
    if num2 is not None:
        S2 = np.bincount(inv, weights=np.asarray(num2, float), minlength=len(u)); D2 = np.bincount(inv, weights=np.asarray(den2, float), minlength=len(u))
    out = []
    for _ in range(B):
        ix = rng.integers(0, len(u), len(u))
        a = S1[ix].sum() / max(D1[ix].sum(), 1e-12)
        if num2 is not None:
            a -= S2[ix].sum() / max(D2[ix].sum(), 1e-12)
        out.append(a)
    return float(np.quantile(out, 0.025)), float(np.quantile(out, 0.975))
