# -*- coding: utf-8 -*-
"""ref_adaptadores.py — B1, 08-10-2026. La RESPUESTA de Python contra la que se prueban los adaptadores de la 4.1 (AdaptadoresFamilia.cs):
  1. round(x, 4) de Python sobre los dias crudos de viva3 y sobre 100.000 "medios" de 5 decimales (el caso que Math.Round de .NET erra);
  2. preview_niveles.foto_viva3 sobre lineas de viva3-NQ-2026-10-07.jsonl (PythiaGex3, SOLO LECTURA): dias y filas exactas;
  3. la cinta de la sesion 2026-10-07 (profundidad/estado/cinta, SOLO LECTURA) como la lee preview_niveles._leer_cinta_dia: velas m2
     (velas_de_ticks), velas.cierre_conocido en la grilla de minutos, backtest_familia.Precio y tq_comun.precio_en en instantes de prueba.
Se importan las funciones de la vista previa (no se copian). Prioridad BELOW_NORMAL antes de importar nada pesado.
Salida: ref_adaptadores.json al lado. Uso: python -I ref_adaptadores.py   |   otra sesion (solo cinta): python -I ref_adaptadores.py 2026-10-08"""
import ctypes, os, sys
ctypes.windll.kernel32.SetPriorityClass.restype = ctypes.c_int
ctypes.windll.kernel32.GetCurrentProcess.restype = ctypes.c_void_p
ctypes.windll.kernel32.SetPriorityClass.argtypes = [ctypes.c_void_p, ctypes.c_uint]
ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)
os.environ.setdefault("OMP_NUM_THREADS", "1"); os.environ.setdefault("OPENBLAS_NUM_THREADS", "1"); os.environ.setdefault("MKL_NUM_THREADS", "1")
import json, time
AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.abspath(os.path.join(AQUI, "..", "..", ".."))
GEN = os.path.join(RAIZ, "laboratorio", "tres", "auditoria_0810")
TQ = os.path.join(RAIZ, "laboratorio", "tres", "tqqq")
sys.path.insert(0, GEN); sys.path.insert(0, os.path.join(RAIZ, "laboratorio")); sys.path.insert(0, TQ)
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
import numpy as np, pandas as pd                                    # noqa: E402
import preview_niveles as PN                                        # noqa: E402
import backtest_familia as BF                                       # noqa: E402
from tres import velas as V                                         # noqa: E402
import tq_comun as TC                                               # noqa: E402

DIA = "2026-10-07"
VIVA = os.path.join(os.environ["APPDATA"], "ATAS", "PythiaGex3", "viva", "viva3-NQ-%s.jsonl" % DIA)


def r(x):
    return repr(float(x))


def redondeo(lineas):
    vals = set()
    for l in lineas[::5]:
        try:
            for x in json.loads(l)["filas"]:
                vals.add(float(x[1]))
        except Exception:
            pass
    sint = [float("%d.%04d5" % (e, f)) for e in range(0, 10) for f in range(0, 10000)]
    xs = sorted(vals) + sint + [0.00005, 0.12345, 1.00005, 2.50005, 0.99995, 1e-9, 13.87085]
    return [[r(x), r(round(x, 4))] for x in xs]


def fotos(lineas):
    ini, fin = pd.Timestamp(DIA) - pd.Timedelta(hours=2), pd.Timestamp(DIA) + pd.Timedelta(hours=21)
    out = []
    for i in range(0, len(lineas), 7):
        f = PN.foto_viva3(lineas[i], ini.to_pydatetime(), fin.to_pydatetime())
        if f is None:
            out.append(dict(i=i, nada=True)); continue
        c = f["cadena"]
        out.append(dict(i=i, ts=f["ts"].strftime("%Y-%m-%d %H:%M:%S"), futuro=r(f["futuro"]), n_filas=f["n_filas"],
                        dias=[r(d) for d in c.dias], filas=[[r(v) for v in row] for row in c.filas]))
    return out


def cinta(DIA=DIA):
    rel = os.path.join(PN.CINTA, "cinta-NQ-%s-relleno.csv" % DIA); viv = os.path.join(PN.CINTA, "cinta-NQ-%s.csv" % DIA)
    vt, vp = PN.ticks_csv(open(viv, encoding="utf-8", errors="replace").read().splitlines())
    rt, rp = PN.ticks_csv(open(rel, encoding="utf-8", errors="replace").read().splitlines())
    m = rt < vt.min(); rt, rp = rt[m], rp[m]
    tt, tp = np.r_[rt, vt], np.r_[rp, vp]
    o = np.argsort(tt, kind="stable"); tt, tp = tt[o], tp[o]
    ini = int(pd.Timestamp(DIA).value // 1_000_000 - 2 * 3600_000); fin = int(pd.Timestamp(DIA).value // 1_000_000 + 21 * 3600_000)
    m = (tt >= ini) & (tt < fin); tt, tp = tt[m], tp[m]
    vs, viva = PN.velas_de_ticks(tt, tp)
    grilla = pd.date_range(pd.Timestamp(DIA) - pd.Timedelta(hours=2), pd.Timestamp(DIA) + pd.Timedelta(hours=21) - pd.Timedelta(minutes=1), freq="1min")
    cierres, _ = V.cierre_conocido(vs, grilla.values)
    PR = BF.Precio.__new__(BF.Precio)
    PR.t = vs["t"].to_numpy().astype("datetime64[ns]"); PR.o = vs["o"].to_numpy(float); PR.c = vs["c"].to_numpy(float)
    PR.tt = tt.astype("datetime64[ms]").astype("datetime64[ns]"); PR.tp = tp; PR.n_tick = 0; PR.n_vela = 0
    inst = list(range(ini, fin, 37_000)) + list(range(ini + 345, fin, 61_300))     # enteros y fraccionarios (los fraccionarios no son exactos por diseño)
    ts = np.array(inst, dtype="int64").astype("datetime64[ms]").astype("datetime64[ns]")
    pr = PR(ts)
    tk = pd.DataFrame(dict(tt=pd.to_datetime(tt, unit="ms"), precio=tp))
    pt = TC.precio_en(tk, ts, 120)
    listo = None
    try:
        j = json.load(open(rel + ".listo", encoding="utf-8")); listo = [int(j["desde"]), int(j["hasta"])]
    except Exception:
        pass
    vms = (vs["t"].astype("int64") // 1_000_000).tolist()
    return dict(dia=DIA, ini=ini, fin=fin, n_ticks=int(len(tt)), n_relleno=int(len(rt)), n_vivo=int(len(vt)), listo=listo,
                viva=viva, velas=[[int(a), r(b), r(c), r(d), r(e)] for a, b, c, d, e in zip(vms, vs["o"], vs["h"], vs["l"], vs["c"])],
                grilla=[int(x) for x in (grilla.astype("int64") // 1_000_000)], cierres=[r(x) for x in cierres],
                instantes=inst, precio=[r(x) for x in pr], precio_tick=[r(x) for x in pt])


if __name__ == "__main__" and len(sys.argv) > 1:
    # otra sesion, solo la cinta: ref_adaptadores_<dia>.json (el arnes la compara si existe)
    for d in sys.argv[1:]:
        t0 = time.time()
        out = dict(generado=time.strftime("%Y-%m-%d %H:%M:%S"), cinta=cinta(d))
        with open(os.path.join(AQUI, "ref_adaptadores_%s.json" % d), "w", encoding="utf-8") as f:
            json.dump(out, f)
        print("%s: velas %d, grilla %d, instantes %d, %.0f s" % (d, len(out["cinta"]["velas"]), len(out["cinta"]["grilla"]), len(out["cinta"]["instantes"]), time.time() - t0), flush=True)
elif __name__ == "__main__":
    t0 = time.time()
    lineas = open(VIVA, encoding="utf-8").read().splitlines()
    out = dict(generado=time.strftime("%Y-%m-%d %H:%M:%S"), viva=VIVA, redondeo=redondeo(lineas), fotos=fotos(lineas))
    time.sleep(0.5)
    out["cinta"] = cinta()
    with open(os.path.join(AQUI, "ref_adaptadores.json"), "w", encoding="utf-8") as f:
        json.dump(out, f)
    print("listo en %.0f s: redondeo %d, fotos %d, velas %d, grilla %d, instantes %d" % (time.time() - t0, len(out["redondeo"]), len(out["fotos"]),
          len(out["cinta"]["velas"]), len(out["cinta"]["grilla"]), len(out["cinta"]["instantes"])), flush=True)
