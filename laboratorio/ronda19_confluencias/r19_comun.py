# -*- coding: utf-8 -*-
"""RONDA 19 - comun: rutas, prioridad baja, lectura de cintas compactas, calendario de ruedas y muestras.
Nada toca ATAS: solo lee las cintas que ya bajo la sonda (%APPDATA%\\ATAS\\PythiaGex\\flujo).
Los datos intermedios grandes van al scratchpad de la sesion (fuera de carpetas sincronizadas)."""
import glob, os, sys
import numpy as np, pandas as pd

try:   # PRIORIDAD BAJA (BELOW_NORMAL) con tipos declarados (sin restype/argtypes falla en silencio, medido 06-10)
    import ctypes, ctypes.wintypes as _W
    _k = ctypes.windll.kernel32; _k.GetCurrentProcess.restype = _W.HANDLE; _k.SetPriorityClass.argtypes = [_W.HANDLE, _W.DWORD]
    _k.SetPriorityClass(_k.GetCurrentProcess(), 0x00004000)
except Exception:
    pass

AQUI = os.path.dirname(os.path.abspath(__file__))
LAB = os.path.dirname(AQUI)
RAIZ = os.path.dirname(LAB)
FLUJO = os.path.join(os.environ.get("APPDATA", ""), "ATAS", "PythiaGex", "flujo")
SCR = r"C:\Users\wmx_7\AppData\Local\Temp\claude\6e29e09c80928880\233bf8d4-5113-4e95-a0d9-68bff726690d\scratchpad\r19"
CINTA = os.path.join(SCR, "cinta")
VELAS = os.path.join(LAB, "dom", "ronda3", "velas")
TICK = 0.25
NY = "America/New_York"
for _d in (SCR, CINTA):
    os.makedirs(_d, exist_ok=True)


def ram_libre_gb():
    try:
        class M(ctypes.Structure):
            _fields_ = [("l", ctypes.c_ulong), ("p", ctypes.c_ulong), ("tot", ctypes.c_ulonglong), ("av", ctypes.c_ulonglong),
                        ("a", ctypes.c_ulonglong), ("b", ctypes.c_ulonglong), ("c", ctypes.c_ulonglong), ("d", ctypes.c_ulonglong),
                        ("e", ctypes.c_ulonglong)]
        m = M(); m.l = ctypes.sizeof(M); ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(m))
        return m.av / 2 ** 30
    except Exception:
        return 99.0


def frenar_si_falta_ram(minimo=2.0):
    g = ram_libre_gb()
    if g < minimo:
        raise SystemExit("RAM libre %.2f GB < %.1f GB: freno (regla del operador)" % (g, minimo))
    return g


def cintas_crudas(inst):
    """{dia de la rueda (AAAA-MM-DD): archivo csv} para un instrumento. El 'hasta' del nombre (UTC) cae en el dia de la rueda."""
    out = {}
    for f in sorted(glob.glob(os.path.join(FLUJO, "cinta-%s-*.csv" % inst))):
        p = os.path.basename(f)[:-4].split("-")
        if len(p) != 4 or p[1] != inst or not os.path.exists(f + ".listo") or os.path.getsize(f) < 100000:
            continue
        fin = p[3][:8]
        out["%s-%s-%s" % (fin[:4], fin[4:6], fin[6:8])] = f
    return out


def ruta_compacta(inst, dia):
    return os.path.join(CINTA, "%s-%s.parquet" % (inst, dia))


def leer(inst, dia):
    """Cinta compacta de una jornada: una fila por orden agresora, ordenada, sin duplicados exactos.
    Columnas: ns (int64, UTC epoch ns), ny_s (segundos desde la medianoche NY del dia de la rueda, float64; la noche da negativo),
    primero, ultimo, vol, lado, abid, abidv, aask, aaskv, dbid, dbidv, dask, daskv."""
    return pd.read_parquet(ruta_compacta(inst, dia))


# ---------------------------------------------------------------- calendario y muestras (fijados en PREREGISTRO.md)
def dias(inst):
    return sorted(os.path.basename(f)[len(inst) + 1:-8] for f in glob.glob(os.path.join(CINTA, "%s-*.parquet" % inst)))


DISENO_FIN = "2026-10-15"       # diseno: ES 08-09 .. 15-10 (septiembre + primera mitad de octubre)
CONFIRMA_FIN = "2026-11-13"     # confirmacion: ES 16-10 .. 13-11
GUARDADA = ("2026-11-16", "2026-11-25")   # guardada: ES 16..25-11 (enmienda 27-11 04:04); MES 16..25-11 = chequeo secundario
TANDA0 = []                     # las 5 ruedas de la tanda 0 (elegidas por regla en r19_01; se escriben en el pre-registro)


def muestra_de(inst, dia):
    if inst == "ES" and dia <= DISENO_FIN:
        return "diseno"
    if inst == "ES" and dia <= CONFIRMA_FIN:
        return "confirma"
    if inst == "ES" and GUARDADA[0] <= dia <= GUARDADA[1]:
        return "guardada"
    if inst == "MES" and GUARDADA[0] <= dia <= GUARDADA[1]:
        return "guardada_mes"
    return "otra"


def ny_offset_h(dia):
    """Horas a sumar a la hora NY para tener UTC el dia de la rueda (4 en verano, 5 en invierno). 2026: DST termina el 01-11."""
    return 4 if dia < "2026-11-01" else 5


def ts_ny(dia, hhmm):
    """Epoch ns (UTC) de una hora NY 'HH:MM' del dia de la rueda (negativo para la noche: se pasa el dia anterior con hora >= 18)."""
    return int((pd.Timestamp(dia) + pd.Timedelta(hours=int(hhmm[:2]) + ny_offset_h(dia), minutes=int(hhmm[3:5]))).value)
