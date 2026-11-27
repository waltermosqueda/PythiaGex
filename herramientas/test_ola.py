# -*- coding: utf-8 -*-
"""Prueba INDEPENDIENTE del MOTOR DE LA OLA del Superindicador (SuperCalculos.cs, 27-11-2026) contra la referencia en Python,
sobre cintas REALES de la sonda.

Por cada cinta (MES 16 a 25-11 y las 9 ruedas de ES de la ronda 17):
  1. corre el arnes de C# (atas/_test_super, modo 'ola'): la ola (X) y la marea (4X) en dos ventanas, la rueda COMPLETA desde las
     18:00 NY de la vispera hasta las 17:00 NY y la RTH 9:30-16:00 NY, cada una arrancada de cero, con fotos cada 30 s; y las mismas
     dos ventanas en modo VIVO: cada orden de varios prints entra creciendo (Orden + CorregirUltima, como OnUpdateCumulativeTrade
     de ATAS) y con fotos tomadas mientras crece, que CorregirUltima tiene que rehacer;
  2. corre la referencia: la clase Maquina de ola_demo_datos.py COPIADA TAL CUAL (si el original esta a mano, se verifica que la
     copia sea identica), manejada igual que en ese archivo, sobre las mismas filas, y arma las fotos con su estado;
  3. compara TODO, sin tolerancias: cada pivote (hora del extremo, precio, tipo, confirmado, hora y numero de orden de la
     confirmacion, cum en el extremo), el extremo final sin confirmar, y cada foto (ordenes, hora y ultimo precio, cum, direccion,
     pivotes, origen, extremo, delta de la pierna, con/sin agresores, retroceso, faltan, raya y recorrido). Las ventanas VIVO se
     comparan contra la MISMA referencia: tienen que dar identico.
Ademas: la X de C# (modo 'x': rango RTH por rueda desde las cintas y 0,08 x mediana de las 10 previas) contra la tabla diaria y
escala_X de la ronda 17 y contra sus cal-*.json; y los giros de la ronda 17 (puntos-*.parquet) contra los pivotes RTH (informativo:
la ronda 17 mide por segundo y no confirma en el mismo segundo del extremo).
Todo en prioridad baja y de a un proceso pesado por vez; se frena si la RAM libre baja de 2 GB.
Uso: python -I herramientas/test_ola.py [--salida DIR] [--original ola_demo_datos.py] [--solo MES-2026-11-25 ...]
"""
import ast
import csv
import glob
import inspect
import json
import os
import subprocess
import sys
import tempfile
import time
from datetime import datetime, timedelta, timezone
from zoneinfo import ZoneInfo

try:
    import ctypes
    import ctypes.wintypes as _W
    _k = ctypes.windll.kernel32
    _k.GetCurrentProcess.restype = _W.HANDLE
    _k.SetPriorityClass.argtypes = [_W.HANDLE, _W.DWORD]
    _k.SetPriorityClass(_k.GetCurrentProcess(), 0x00004000)   # BELOW_NORMAL
except Exception:
    pass

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(AQUI)
FLUJO = os.path.join(os.environ.get("APPDATA", ""), "ATAS", "PythiaGex", "flujo")
EXE = os.path.join(RAIZ, "atas", "_test_super", "bin", "Release", "net10.0", "_test_super.exe")
FUENTES_CS = [os.path.join(RAIZ, "atas", "Superindicador", "SuperCalculos.cs"), os.path.join(RAIZ, "atas", "_test_super", "Program.cs")]
R17 = os.path.join(RAIZ, "laboratorio", "ronda17_orderflow")
DATOS_R17 = os.path.join(RAIZ, "datos", "ronda17")
NY = ZoneInfo("America/New_York")
UTC = timezone.utc
PASO = 30
TICK = 0.25
MES_DIAS = ["2026-11-16", "2026-11-17", "2026-11-18", "2026-11-19", "2026-11-20", "2026-11-23", "2026-11-24", "2026-11-25"]
ES_DIAS = ["2026-11-02", "2026-11-03", "2026-11-04", "2026-11-06", "2026-11-09", "2026-11-10", "2026-11-11", "2026-11-12", "2026-11-13"]
ES_DESDE_X = "2026-10-19"   # la X de ES del 02-11 usa las ruedas desde el 19-10
BELOW_NORMAL = 0x00004000
# los tres giros de la marea que cita la ronda 17 para MES 25-11 (hora NY, precio, tipo)
CONTROL_R17 = [("10:36:32", 7057.50, 1), ("11:17:47", 7001.50, -1), ("12:18:29", 7132.50, 1)]


# ----------------------------------------------------------------------------------------------------------------- la referencia
# COPIA TAL CUAL de la clase Maquina de ola_demo_datos.py (la demo animada del 26/27-11). No tocar: main() verifica que sea identica.
class Maquina:
    """Zigzag en vivo: dir +1 sube / -1 baja; ext = extremo de la pierna en curso; al volver X desde ext se confirma el
    pivote (ext pasa a ser el origen de la pierna nueva). cum = delta acumulado al momento de cada extremo (inclusive)."""
    def __init__(s, X):
        s.X = X; s.dir = 0; s.ext = None; s.ext_t = None; s.ext_cum = 0
        s.lo = s.hi = None; s.lo_t = s.hi_t = None; s.lo_cum = s.hi_cum = 0
        s.pivotes = []   # (t_extremo, precio, tipo, t_confirmacion, cum_en_el_extremo)
    def paso(s, t, mn, mx, cum):
        if s.dir == 0:
            if s.lo is None or mn < s.lo: s.lo, s.lo_t, s.lo_cum = mn, t, cum
            if s.hi is None or mx > s.hi: s.hi, s.hi_t, s.hi_cum = mx, t, cum
            if s.hi - s.lo >= s.X:
                if s.hi_t > s.lo_t: s.pivotes.append((s.lo_t, s.lo, 'min', t, s.lo_cum)); s.dir = 1; s.ext, s.ext_t, s.ext_cum = s.hi, s.hi_t, s.hi_cum
                else: s.pivotes.append((s.hi_t, s.hi, 'max', t, s.hi_cum)); s.dir = -1; s.ext, s.ext_t, s.ext_cum = s.lo, s.lo_t, s.lo_cum
            return
        if s.dir == 1:
            if mx > s.ext: s.ext, s.ext_t, s.ext_cum = mx, t, cum
            elif s.ext - mn >= s.X:
                s.pivotes.append((s.ext_t, s.ext, 'max', t, s.ext_cum)); s.dir = -1; s.ext, s.ext_t, s.ext_cum = mn, t, cum
        else:
            if mn < s.ext: s.ext, s.ext_t, s.ext_cum = mn, t, cum
            elif mx - s.ext >= s.X:
                s.pivotes.append((s.ext_t, s.ext, 'min', t, s.ext_cum)); s.dir = 1; s.ext, s.ext_t, s.ext_cum = mx, t, cum


def verificar_copia(original):
    """La Maquina de arriba contra la del archivo original: mismo arbol sintactico (misma semantica) y mismo texto."""
    if not original or not os.path.exists(original):
        return "original NO encontrado: se usa la copia embebida sin poder verificarla"
    src = open(original, encoding="utf-8").read()
    nodo = next((n for n in ast.parse(src).body if isinstance(n, ast.ClassDef) and n.name == "Maquina"), None)
    if nodo is None:
        return "ERROR: el original no tiene una clase Maquina"
    propio = ast.parse(inspect.getsource(Maquina)).body[0]
    if ast.dump(nodo) != ast.dump(propio):
        return "ERROR: la copia de Maquina NO es identica a la del original (" + original + ")"
    texto = ast.get_source_segment(src, nodo).strip() == inspect.getsource(Maquina).strip()
    return "copia de Maquina identica al original (arbol sintactico%s) %s" % (" y texto" if texto else "; el texto difiere en blancos", original)


def iso(t):
    return None if t is None else t.isoformat(timespec="milliseconds")


def ny_segundo(utc_txt):
    """Hora NY (sin zona) al segundo de una hora UTC escrita por el arnes."""
    return datetime.fromisoformat(utc_txt).replace(tzinfo=UTC).astimezone(NY).replace(tzinfo=None, microsecond=0)


class Ventana:
    """Una ventana [desde, hasta) con su ola y su marea arrancadas de cero, manejadas como en ola_demo_datos.py (mn, mx, cum += v * s
    antes del paso), mas lo que hace falta para las fotos: la cuenta de ordenes, la ultima orden y el numero de orden de cada
    confirmacion. Las fotos van en cortes desde + 30 s, desde + 60 s, ... <= hasta; la del corte c es el estado con las ordenes
    de t < c (se toma antes de pasar la primera orden de t >= c; al final, los cortes que quedan)."""

    def __init__(s, nombre, desde_utc, hasta_utc, X):
        s.nombre, s.X = nombre, X
        s.T0, s.T1 = desde_utc.strftime("%Y-%m-%dT%H:%M:%S"), hasta_utc.strftime("%Y-%m-%dT%H:%M:%S")   # como en la referencia
        s.m = {"X": Maquina(X), "4X": Maquina(4 * X)}
        s.cum = 0
        s.n = 0
        s.ult = None
        s.t_ult = None
        s.cortes = []
        c = desde_utc + timedelta(seconds=PASO)
        while c <= hasta_utc:
            s.cortes.append(c)
            c += timedelta(seconds=PASO)
        s.fotos = {"X": [], "4X": []}
        s.nconf = {"X": [], "4X": []}

    def _fotear(s):
        for esc, m in s.m.items():
            s.fotos[esc].append(foto(m, s))

    def orden(s, t, a, b, v, lado):
        while len(s.fotos["X"]) < len(s.cortes) and t >= s.cortes[len(s.fotos["X"])]:
            s._fotear()
        mn, mx = min(a, b), max(a, b)
        s.cum += v * lado
        s.n += 1
        s.ult, s.t_ult = b, t
        for esc, m in s.m.items():
            antes = len(m.pivotes)
            m.paso(t, mn, mx, s.cum)
            if len(m.pivotes) != antes:
                s.nconf[esc].append(s.n)

    def cerrar(s):
        while len(s.fotos["X"]) < len(s.cortes):
            s._fotear()


def foto(m, s):
    """La foto desde el estado de la Maquina (definicion del pedido): delta de la pierna = cum ahora - cum del origen; con agresores
    si su signo es la direccion; retroceso = |extremo - ultimo|; faltan = max(0, X - retroceso); raya = extremo - dir X;
    recorrido = |extremo - origen|."""
    f = [s.n, iso(s.t_ult), s.ult, s.cum, m.dir, len(m.pivotes)]
    if m.dir == 0:
        return f + [None] * 13
    o = m.pivotes[-1]
    delta = s.cum - o[4]
    con = 1 if (m.dir == 1 and delta > 0) or (m.dir == -1 and delta < 0) else 0
    retro = abs(m.ext - s.ult)
    return f + [iso(o[0]), o[1], 1 if o[2] == "max" else -1, o[4], iso(m.ext_t), m.ext, m.ext_cum,
                delta, con, retro, max(0.0, m.X - retro), m.ext - m.dir * m.X, abs(m.ext - o[1])]


def pivotes_ref(v, esc):
    m = v.m[esc]
    out = [(iso(te), p, 1 if tipo == "max" else -1, 1, iso(tc), n, c) for (te, p, tipo, tc, c), n in zip(m.pivotes, v.nconf[esc])]
    if m.dir != 0:
        out.append((iso(m.ext_t), m.ext, m.dir, 0, None, 0, m.ext_cum))
    return out


def referencia(ruta, ventanas):
    """Una pasada por la cinta en el orden del archivo, sin sacar duplicados (como la referencia)."""
    muestra = 0
    with open(ruta, newline="") as fh:
        r = csv.reader(fh)
        next(r)
        for i, x in enumerate(r):
            t = datetime.fromisoformat(x[0])
            if i % 1000 == 0:   # la referencia parsea con strptime(x[0][:23]): se controla que de lo mismo
                assert len(x[0]) == 23 and t == datetime.strptime(x[0][:23], "%Y-%m-%dT%H:%M:%S.%f"), x[0]
                muestra += 1
            a, b = float(x[1]), float(x[2])
            v = float(x[3])
            lado = int(x[4])
            for w in ventanas:
                if x[0] < w.T0 or x[0] >= w.T1:
                    continue
                w.orden(t, a, b, v, lado)
    for w in ventanas:
        w.cerrar()
    return muestra


# ----------------------------------------------------------------------------------------------------------------- lo de C#
def _num(s):
    return None if s == "" else float(s)


def _ent(s):
    return None if s == "" else int(s)


def _txt(s):
    return None if s == "" else s


def leer_cs(ruta):
    """Salida del modo 'ola': V (ventana), P (pivote) y F (foto), separados por ';'."""
    V, P, F = {}, {}, {}
    with open(ruta, encoding="utf-8") as fh:
        for linea in fh:
            c = linea.rstrip("\n").split(";")
            if c[0] == "V":
                V[c[1]] = {"desde": c[2], "hasta": c[3], "ordenes": int(c[4]), "correcciones": int(c[5]), "tarde": int(c[6]) + int(c[7])}
            elif c[0] == "P":
                P.setdefault((c[1], c[2]), []).append((c[4], float(c[5]), int(c[6]), int(c[7]), _txt(c[8]), int(c[9]), float(c[10])))
            elif c[0] == "F":
                fila = ([int(c[5]), _txt(c[6]), _num(c[7]), float(c[8]), int(c[9]), int(c[10])]
                        + [_txt(c[11]), _num(c[12]), _ent(c[13]), _num(c[14]), _txt(c[15]), _num(c[16]), _num(c[17]),
                           _num(c[18]), _ent(c[19]), _num(c[20]), _num(c[21]), _num(c[22]), _num(c[23])])
                F.setdefault((c[1], c[2]), []).append((c[4], fila))
    return V, P, F


CAMPOS_P = ["t_extremo", "precio", "tipo", "confirmado", "t_conf", "n_conf", "cum"]
CAMPOS_F = ["n", "t_ultima", "ultimo", "cum", "dir", "pivotes", "origen_t", "origen_precio", "origen_tipo", "origen_cum", "extremo_t",
            "extremo_precio", "extremo_cum", "delta_pierna", "con_agresores", "retroceso", "faltan", "raya", "recorrido"]


def comparar(etiqueta, cs, py, campos, difs, con_clave=False):
    """Compara dos listas de filas campo por campo, exacto. Devuelve (filas comparadas, campos comparados)."""
    if len(cs) != len(py):
        difs.append("%s: %d filas en C# contra %d en la referencia" % (etiqueta, len(cs), len(py)))
    nf = nc = 0
    for k, (a, b) in enumerate(zip(cs, py)):
        if con_clave:
            (ca, a), (cb, b) = a, b
            if ca != cb:
                difs.append("%s fila %d: corte %s en C# contra %s" % (etiqueta, k, ca, cb))
        nf += 1
        for nombre, x, y in zip(campos, a, b):
            nc += 1
            if x != y:
                difs.append("%s fila %d campo %s: C# %r, referencia %r" % (etiqueta, k, nombre, x, y))
    return nf, nc


# ----------------------------------------------------------------------------------------------------------------- utilidades
def ram_libre_mb():
    class M(ctypes.Structure):
        _fields_ = [("dwLength", ctypes.c_ulong), ("dwMemoryLoad", ctypes.c_ulong), ("ullTotalPhys", ctypes.c_ulonglong),
                    ("ullAvailPhys", ctypes.c_ulonglong), ("ullTotalPageFile", ctypes.c_ulonglong), ("ullAvailPageFile", ctypes.c_ulonglong),
                    ("ullTotalVirtual", ctypes.c_ulonglong), ("ullAvailVirtual", ctypes.c_ulonglong), ("ullAvailExtendedVirtual", ctypes.c_ulonglong)]
    m = M()
    m.dwLength = ctypes.sizeof(M)
    ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(m))
    return m.ullAvailPhys / 2 ** 20


def cintas(inst):
    """{dia de la rueda: archivo} de las cintas terminadas (.listo); el dia es el 'hasta' UTC del nombre (r17_comun.archivo_cinta)."""
    out = {}
    for f in sorted(glob.glob(os.path.join(FLUJO, "cinta-%s-*.csv" % inst))):
        p = os.path.basename(f).split("-")
        if len(p) < 4 or not os.path.exists(f + ".listo"):
            continue
        fin = p[3][:8]
        out["%s-%s-%s" % (fin[:4], fin[4:6], fin[6:8])] = f
    return out


def ventanas_utc(dia):
    d = datetime.strptime(dia, "%Y-%m-%d")
    def u(x):
        return x.replace(tzinfo=NY).astimezone(UTC).replace(tzinfo=None)
    return {"completa": (u(d - timedelta(days=1) + timedelta(hours=18)), u(d + timedelta(hours=17))),
            "rth": (u(d + timedelta(hours=9, minutes=30)), u(d + timedelta(hours=16)))}


def correr_cs(args):
    r = subprocess.run([EXE] + args, capture_output=True, text=True, creationflags=BELOW_NORMAL)
    if r.returncode != 0:
        raise RuntimeError("el arnes de C# fallo (%d): %s %s" % (r.returncode, r.stdout[-2000:], r.stderr[-2000:]))
    return r.stdout


def cargar_r17():
    sys.path.insert(0, R17)
    import r17_01_anatomia as A17   # noqa: E402  (la tabla diaria y escala_X EXACTAS de la ronda 17)
    return A17, A17.tabla_diaria()


# ----------------------------------------------------------------------------------------------------------------- pruebas
def prueba_x(salida, A17, diaria):
    """La X de C# contra la tabla diaria de la ronda 17 (rango RTH por rueda), escala_X y los cal-*.json."""
    es, me = cintas("ES"), cintas("MES")
    lista = [es[d] for d in sorted(es) if ES_DESDE_X <= d <= "2026-11-13"] + [me[d] for d in MES_DIAS if d in me]
    js = os.path.join(salida, "x.json")
    print(correr_cs(["x", js, "0.25"] + lista).strip(), flush=True)
    cs = json.load(open(js))
    difs, filas = [], []
    tabla = {r["dia"]: r for _, r in diaria.iterrows()}
    for r in cs:
        d = r["dia"]
        t = tabla.get(d)
        if t is None:
            difs.append("%s: la rueda no esta en la tabla diaria de la ronda 17" % d)
            continue
        if r["alto"] != float(t["h"]) or r["bajo"] != float(t["l"]):
            difs.append("%s: rango RTH C# %s-%s, ronda 17 %s-%s" % (d, r["alto"], r["bajo"], t["h"], t["l"]))
        if r["usadas"] < 10:
            continue
        x17, med17, prev17 = A17.escala_X(d, diaria)
        cal = os.path.join(R17, "narraciones", "cal-%s-%s.json" % (t["inst"], d))
        xc = json.load(open(cal)) if os.path.exists(cal) else None
        if r["x"] != x17 or r["mediana"] != med17:
            difs.append("%s: X C# %s (mediana %s), escala_X %s (mediana %s)" % (d, r["x"], r["mediana"], x17, med17))
        if xc is not None and (r["x"] != xc["X"] or r["mediana"] != xc["mediana_rango_10"]):
            difs.append("%s: X C# %s (mediana %s), cal-*.json %s (mediana %s)" % (d, r["x"], r["mediana"], xc["X"], xc["mediana_rango_10"]))
        filas.append((t["inst"], d, r["rango"], r["mediana"], r["x"], x17, xc["X"] if xc else None))
    print("\nX por rueda (C# desde las cintas contra la ronda 17):")
    print("  inst  rueda       rango RTH  mediana10  X C#   X escala_X  X cal-*.json")
    for inst, d, rg, med, x, x17, xc in filas:
        print("  %-4s  %s  %9.2f  %9.3f  %5.2f  %10.2f  %s" % (inst, d, rg, med, x, x17, "%.2f" % xc if xc is not None else "-"))
    print("  ruedas con rango comparado: %d; X comparadas: %d; contra cal-*.json: %d; diferencias: %d"
          % (len(cs), len(filas), sum(1 for f in filas if f[6] is not None), len(difs)))
    for x in difs[:20]:
        print("  DIF", x)
    return difs, {(f[0], f[1]): f[5] for f in filas}


def control_r17(inst, dia, P):
    """Los giros de la ronda 17 (por segundo, desde la tabla de 1 s) contra los pivotes RTH confirmados de C#: informativo."""
    ruta = os.path.join(DATOS_R17, "puntos-%s-%s.parquet" % (inst, dia))
    if not os.path.exists(ruta):
        return None
    import pandas as pd
    d = pd.read_parquet(ruta, columns=["i", "precio", "tipo", "clase", "escala"])
    t0 = datetime.strptime(dia, "%Y-%m-%d") + timedelta(hours=9, minutes=30)
    out = []
    for esc in ("X", "4X"):
        g = d[(d["clase"] == "giro") & (d["escala"] == esc)]
        r17 = {(int(i), float(p), int(s)) for i, p, s in zip(g["i"], g["precio"], g["tipo"])}
        cs = set()
        for (te, p, tipo, conf, tc, n, c) in P.get(("rth", esc), []):
            if not conf:
                continue
            i = int((ny_segundo(te) - t0).total_seconds())
            if i >= 30:   # la ronda 17 no cuenta los giros de los primeros 30 s
                cs.add((i, p, tipo))
        out.append("%s %d/%d" % (esc, len(r17 & cs), len(r17)) + ("" if len(cs) == len(r17) else " (C# %d)" % len(cs)))
    return ", ".join(out)


def main():
    args = sys.argv[1:]
    salida = os.path.join(tempfile.gettempdir(), "pythia_test_ola")
    original = None
    solo = []
    while args:
        a = args.pop(0)
        if a == "--salida":
            salida = args.pop(0)
        elif a == "--original":
            original = args.pop(0)
        elif a == "--solo":
            while args and not args[0].startswith("--"):
                solo.append(args.pop(0))
        else:
            raise SystemExit(__doc__)
    os.makedirs(salida, exist_ok=True)
    if original is None:
        cand = sorted(glob.glob(os.path.join(tempfile.gettempdir(), "claude", "*", "*", "scratchpad", "ola_demo_datos.py")), key=os.path.getmtime)
        original = cand[-1] if cand else None
    print(verificar_copia(original), flush=True)
    if not os.path.exists(EXE):
        raise SystemExit("falta el arnes: dotnet build -c Release en atas/_test_super")
    viejo = [f for f in FUENTES_CS if os.path.getmtime(f) > os.path.getmtime(EXE)]
    if viejo:
        print("OJO: el arnes es mas viejo que " + ", ".join(os.path.basename(f) for f in viejo) + ": recompilar", flush=True)
    A17, diaria = cargar_r17()
    t_inicio = time.time()
    difs_x, _ = prueba_x(salida, A17, diaria)

    pedidos = [("MES", d) for d in MES_DIAS] + [("ES", d) for d in ES_DIAS]
    if solo:
        pedidos = [(i, d) for i, d in pedidos if "%s-%s" % (i, d) in solo]
    archivos = {"MES": cintas("MES"), "ES": cintas("ES")}
    tot = {"ruedas": 0, "pivotes": 0, "fotos": 0, "campos": 0, "difs": 0}
    print("\nOla y marea: C# contra la referencia (ventanas completa 18:00-17:00 NY y RTH 9:30-16:00 NY, normal y VIVO)")
    for inst, dia in pedidos:
        libre = ram_libre_mb()
        if libre < 2048:
            print("FRENO: RAM libre %.0f MB (< 2 GB). Lo que falta no se corrio." % libre, flush=True)
            break
        ruta = archivos[inst].get(dia)
        if ruta is None:
            print("FALTA la cinta de %s %s" % (inst, dia), flush=True)
            tot["difs"] += 1
            continue
        X, _, _ = A17.escala_X(dia, diaria)
        vu = ventanas_utc(dia)
        out_cs = os.path.join(salida, "ola-%s-%s.txt" % (inst, dia))
        t0 = time.time()
        args_cs = ["ola", ruta, out_cs, repr(float(X)), str(PASO)]
        for nombre, (d0, d1) in vu.items():
            args_cs += [nombre, d0.strftime("%Y-%m-%dT%H:%M:%S"), d1.strftime("%Y-%m-%dT%H:%M:%S")]
        correr_cs(args_cs)
        t_cs = time.time() - t0
        t0 = time.time()
        vs = [Ventana(nombre, d0, d1, X) for nombre, (d0, d1) in vu.items()]
        muestra = referencia(ruta, vs)
        t_py = time.time() - t0
        V, P, F = leer_cs(out_cs)
        difs = []
        npiv = nfot = ncam = 0
        for w in vs:
            for variante in (w.nombre, w.nombre + "_vivo"):
                if variante not in V:
                    difs.append("%s: falta la ventana en la salida de C#" % variante)
                    continue
                if V[variante]["ordenes"] != w.n:
                    difs.append("%s: %d ordenes en C# contra %d" % (variante, V[variante]["ordenes"], w.n))
                if V[variante]["tarde"]:
                    difs.append("%s: %d cortes tarde" % (variante, V[variante]["tarde"]))
                for esc in ("X", "4X"):
                    et = "%s %s %s %s" % (inst, dia, variante, esc)
                    a, b = comparar(et + " pivotes", P.get((variante, esc), []), pivotes_ref(w, esc), CAMPOS_P, difs)
                    npiv += a
                    ncam += b
                    a, b = comparar(et + " fotos", F.get((variante, esc), []),
                                    [(iso(c), f) for c, f in zip(w.cortes, w.fotos[esc])], CAMPOS_F, difs, con_clave=True)
                    nfot += a
                    ncam += b
        ctl = ""
        if inst == "MES" and dia == "2026-11-25":
            piv = P.get(("rth", "4X"), [])
            ok = 0
            for hh, p, s in CONTROL_R17:
                ny = datetime.strptime(dia + "T" + hh, "%Y-%m-%dT%H:%M:%S")
                if any(conf and tipo == s and pr == p and ny_segundo(te) == ny for (te, pr, tipo, conf, tc, n, c) in piv):
                    ok += 1
            ctl = " | giros de la marea citados por la ronda 17: %d de 3" % ok
            if ok != 3:
                difs.append("MES 25-11: la marea no reproduce los 3 giros citados por la ronda 17")
        r17 = control_r17(inst, dia, P)
        corr = V.get("completa_vivo", {}).get("correcciones", 0)
        print("%-3s %s X %5.2f | ordenes %7d (RTH %6d) | pivotes %5d | fotos %5d | campos %8d | correcciones vivo %7d | C# %4.1f s, ref %5.1f s | difs %d%s%s"
              % (inst, dia, X, vs[0].n, vs[1].n, npiv, nfot, ncam, corr, t_cs, t_py, len(difs), ctl,
                 (" | ronda 17 (informativo, giros RTH al segundo): " + r17) if r17 else ""), flush=True)
        for x in difs[:15]:
            print("    DIF", x)
        tot["ruedas"] += 1
        tot["pivotes"] += npiv
        tot["fotos"] += nfot
        tot["campos"] += ncam
        tot["difs"] += len(difs)
        del vs, V, P, F
    print("\nTOTAL: %d ruedas, %d pivotes y %d fotos comparados (%d campos), diferencias %d; X: %d diferencias; %.0f s"
          % (tot["ruedas"], tot["pivotes"], tot["fotos"], tot["campos"], tot["difs"], len(difs_x), time.time() - t_inicio))
    return 0 if tot["difs"] == 0 and not difs_x else 1


if __name__ == "__main__":
    sys.exit(main())
