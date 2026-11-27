# -*- coding: utf-8 -*-
"""RONDA 25 - la llave del dia de tendencia - COMUN: rutas, prioridad BAJA, freno por RAM, particion (PREREGISTRO_R25.md, 09:46),
dias de cambio de contrato, calendario oficial, registro de variantes y estadisticos (AUC con permutacion, t por rueda, t agrupado).
Nada toca ATAS ni la red. La carpeta de velas (laboratorio/dom/ronda3/velas) es IRREMPLAZABLE: aca solo se LEE.
No importa nada de las rondas 21-24 (sus archivos tienen huella): lo que se reusa de ellas se LEE como datos."""
import csv, hashlib, json, math, os, sys, time
import numpy as np, pandas as pd

try:   # PRIORIDAD BAJA (BELOW_NORMAL) con tipos declarados
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
R23 = os.path.join(LAB, "ronda23_diseno")
R24 = os.path.join(LAB, "ronda24_a3_30min")
SAL = os.path.join(AQUI, "salidas")
SCR = r"C:\Users\wmx_7\AppData\Local\Temp\claude\6e29e09c80928880\233bf8d4-5113-4e95-a0d9-68bff726690d\scratchpad\r25"
SCR23 = os.path.join(os.path.dirname(SCR), "r23")
FLUJO = os.path.join(os.environ.get("APPDATA", ""), "ATAS", "PythiaGex", "flujo")
for _d in (SAL, SCR):
    os.makedirs(_d, exist_ok=True)

TK = 0.25
NY = "America/New_York"
COSTO = 1.00
# ---- particion (PREREGISTRO_R25.md, seccion 1)
DISENO = ("2024-04-29", "2025-09-08")        # MES 30 min nativas (337 ruedas)
VAL1 = ("2025-09-09", "2026-08-31")          # MES 5 min agregadas a 30 (242 ruedas)
EXPLORA = ("2026-09-10", "2026-09-30")       # cinta: las 15 primeras ruedas (exploracion a mano; fuera de la validacion)
VAL2 = ("2026-10-01", "2026-11-25")          # cinta: las 40 ruedas restantes
A3_DISENO = ("2025-09-09", "2026-02-27")     # muestras de A3 (R23)
A3_CHEQUEO = ("2026-03-02", "2026-08-31")
A3_CINTA = ("2026-09-10", "2026-11-25")
# ---- cambio de contrato del continuo (lunes de la semana del vencimiento trimestral): el dia anterior es de otro contrato
ROLLS = {"2024-06-17", "2024-09-16", "2024-12-16", "2025-03-17", "2025-06-16", "2025-09-15", "2025-12-15", "2026-03-16",
         "2026-06-15", "2026-09-14", "2026-12-14", "2027-03-15", "2027-06-14"}
CAL = json.load(open(os.path.join(AQUI, "calendario_oficial.json"), encoding="utf-8"))
FOMC = set(CAL["fomc"]); DATO830 = set(CAL["cpi"]) | set(CAL["empleo"])
REGISTRO = os.path.join(AQUI, "registro_variantes.csv")
CAMPOS = ["n_var", "fecha_hora_AR", "etapa", "detector", "variante", "parametros", "muestra", "subconjunto", "objetivo", "n_dias",
          "n_marca", "base_pct", "precision_pct", "recall_pct", "lift", "auc", "auc_ee", "p_perm", "auc_azar", "memoria",
          "D_marca", "D_resto", "t_D", "pasa", "nota"]


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


def al_tick(x):
    return math.floor(x / TK + 0.5) * TK


def X_de(med):
    return float(np.floor(0.08 * med / TK + 0.5) * TK)


def en(dia, per):
    return per[0] <= dia <= per[1]


def siguiente_n_var():
    if not os.path.exists(REGISTRO): return 1
    try:
        return int(pd.read_csv(REGISTRO)["n_var"].max()) + 1
    except Exception:
        return 1


def anotar(filas):
    if isinstance(filas, dict): filas = [filas]
    nuevo = not os.path.exists(REGISTRO)
    n = siguiente_n_var()
    with open(REGISTRO, "a", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=CAMPOS, extrasaction="ignore")
        if nuevo: w.writeheader()
        for fila in filas:
            fila = dict(fila); fila.setdefault("fecha_hora_AR", time.strftime("%Y-%m-%d %H:%M")); fila["n_var"] = n; n += 1
            w.writerow({k: fila.get(k, "") for k in CAMPOS})


# ------------------------------------------------------------------------------------------------ estadisticos
def auc(score, y):
    """AUC de Mann-Whitney con empates (rango medio). score alto = predice y = 1."""
    s = np.asarray(score, float); y = np.asarray(y, int)
    n1 = int(y.sum()); n0 = len(y) - n1
    if n1 == 0 or n0 == 0: return float("nan")
    r = pd.Series(s).rank(method="average").to_numpy()
    return float((r[y == 1].sum() - n1 * (n1 + 1) / 2.0) / (n1 * n0))


def auc_ee(a, n1, n0):
    """error de Hanley-McNeil (1982)."""
    if not (n1 and n0) or not np.isfinite(a): return float("nan")
    q1 = a / (2 - a); q2 = 2 * a * a / (1 + a)
    v = (a * (1 - a) + (n1 - 1) * (q1 - a * a) + (n0 - 1) * (q2 - a * a)) / (n1 * n0)
    return float(math.sqrt(max(v, 0.0)))


def p_perm_auc(score, y, B=2000, semilla=2525):
    """p de una cola: fraccion de permutaciones de y con AUC >= el real."""
    s = np.asarray(score, float); y = np.asarray(y, int)
    a0 = auc(s, y)
    if not np.isfinite(a0): return float("nan")
    rng = np.random.default_rng(semilla)
    r = pd.Series(s).rank(method="average").to_numpy(); n1 = int(y.sum()); n0 = len(y) - n1
    cnt = 0
    for _ in range(B):
        yp = rng.permutation(y)
        a = (r[yp == 1].sum() - n1 * (n1 + 1) / 2.0) / (n1 * n0)
        cnt += a >= a0 - 1e-12
    return float((cnt + 1) / (B + 1))


def t_rueda(serie):
    serie = np.asarray(serie, float)
    if len(serie) < 2: return 0.0
    sd = serie.std(ddof=1)
    return float(serie.mean() / (sd / math.sqrt(len(serie)))) if sd > 0 else 0.0


def t_welch(a, b):
    a = np.asarray(a, float); b = np.asarray(b, float)
    if len(a) < 2 or len(b) < 2: return float("nan")
    se = math.sqrt(a.var(ddof=1) / len(a) + b.var(ddof=1) / len(b))
    return float((a.mean() - b.mean()) / se) if se > 0 else float("nan")


def t_agrupado(x, grupos):
    """t de la media de x con error agrupado por rueda (suma de residuos por grupo)."""
    x = np.asarray(x, float); n = len(x)
    if n < 2: return float("nan")
    m = x.mean(); s = pd.Series(x - m).groupby(np.asarray(grupos)).sum()
    g = len(s)
    if g < 2: return float("nan")
    se = math.sqrt((s ** 2).sum() * g / (g - 1)) / n
    return float(m / se) if se > 0 else float("nan")
