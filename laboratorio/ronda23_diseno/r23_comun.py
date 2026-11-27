# -*- coding: utf-8 -*-
"""RONDA 23 (diseno) - comun: rutas, prioridad BAJA, freno por RAM, particiones, registro de variantes y estadisticos.
Nada toca ATAS ni la red. La carpeta de velas (laboratorio/dom/ronda3/velas) es IRREMPLAZABLE: aca solo se LEE.
Particion temporal interna (fijada ANTES de mirar resultados de operaciones de esta ronda):
  DISENO  = ruedas completas de MES 5 min del 09-09-2025 al 27-02-2026 (se explora y se elige aca)
  CHEQUEO = ruedas completas de MES 5 min del 02-03-2026 al 31-08-2026 (se mira despues de elegir)
  CINTA   = 55 ruedas de ES orden por orden (10-09 a 25-11-2026), a la escala del segundo (se mira al final)
TODO este material ya fue visto por rondas anteriores (R19-R22): esto es SELECCION sobre datos vistos, no prueba ciega."""
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
R21 = os.path.join(LAB, "ronda21_regimen")
CURVA = os.path.join(R21, "curva_rango_por_minuto.json")
CURVA_SHA = "72589182d1533ccab0903e70469e8654dc7a201197d88064238dd1a2d7a6bc33"
SAL = os.path.join(AQUI, "salidas")
SCR = r"C:\Users\wmx_7\AppData\Local\Temp\claude\6e29e09c80928880\233bf8d4-5113-4e95-a0d9-68bff726690d\scratchpad\r23"
SCR19 = os.path.join(os.path.dirname(SCR), "r19")
FLUJO = os.path.join(os.environ.get("APPDATA", ""), "ATAS", "PythiaGex", "flujo")
for _d in (SAL, SCR):
    os.makedirs(_d, exist_ok=True)

TK = 0.25
NY = "America/New_York"
DISENO = ("2025-09-09", "2026-02-27")
CHEQUEO = ("2026-03-02", "2026-08-31")
CINTA = ("2026-09-10", "2026-11-25")
COSTO = 1.00
REGISTRO = os.path.join(AQUI, "registro_variantes.csv")
CAMPOS = ["n_var", "fecha_hora_AR", "etapa", "familia", "variante", "parametros", "muestra", "ruedas", "n", "ops_por_rueda",
          "acierto_pct", "acierto_azar_pct", "P_formula_pct", "bruto_op", "neto_op_c100", "neto_azar_c100", "t_rueda_c100",
          "ruedas_pos_c100", "R_medio", "objetivo_medio", "pasa_vara", "nota"]


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
    if sha256(CURVA) != CURVA_SHA:
        raise SystemExit("la curva f(m) de la R21 cambio: no se corre")
    return np.asarray(json.load(open(CURVA))["f"], float)


def muestra_de(dia):
    if DISENO[0] <= dia <= DISENO[1]: return "diseno"
    if CHEQUEO[0] <= dia <= CHEQUEO[1]: return "chequeo"
    if CINTA[0] <= dia <= CINTA[1]: return "cinta"
    return "otra"


def siguiente_n_var():
    if not os.path.exists(REGISTRO): return 1
    try:
        return int(pd.read_csv(REGISTRO)["n_var"].max()) + 1
    except Exception:
        return 1


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
    from scipy import stats
    return float(stats.t.ppf(1 - alfa / max(k, 1), max(ruedas - 1, 1)))
