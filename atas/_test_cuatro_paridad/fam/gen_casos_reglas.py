# -*- coding: utf-8 -*-
"""gen_casos_reglas.py — 08-10-2026 (B3d, PythiaGex 4.1). Casos SINTETICOS para la paridad de las reglas del modulo fam (MUROS, MAJORS,
ZTP con islas, ZEST, CONF, FAM C1 y montos) contra el codigo REAL de Python: se IMPORTAN backtest_familia.py y preview_niveles.py (no se
copian) y se les pasan libros BM armados al azar. No lee ningun dato del mercado (no carga velas, ni cadenas, ni cinta): es liviano.
La cuenta pesada de la vista previa NO se corre aca (la referencia de sesiones completas ya la genero el agente principal).

Salida: datos/casos-reglas.json: por caso, los libros de entrada (ya filtrados al +-3 % por BM) y lo que devuelve
preview_niveles.Motor.calcular para ese minuto (reg = {serie: [[precio redondeado, etiqueta, monto]]}) mas los precios sin redondear.
Uso (desde cualquier carpeta): python -I gen_casos_reglas.py [n_casos=600] [semilla=20261008]
Prioridad BELOW_NORMAL (la pone _prio al importar preview_niveles; ademas se pone aca primero)."""
import ctypes, os, sys, json, math
_k = ctypes.windll.kernel32
_k.SetPriorityClass.restype = ctypes.c_int; _k.GetCurrentProcess.restype = ctypes.c_void_p
_k.SetPriorityClass.argtypes = [ctypes.c_void_p, ctypes.c_uint]
_k.SetPriorityClass(_k.GetCurrentProcess(), 0x4000)
os.environ.setdefault("OMP_NUM_THREADS", "1"); os.environ.setdefault("OPENBLAS_NUM_THREADS", "1")
AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.abspath(os.path.join(AQUI, "..", "..", ".."))
GEN = os.path.join(RAIZ, "laboratorio", "tres", "auditoria_0810")
sys.path.insert(0, GEN); sys.path.insert(0, os.path.join(RAIZ, "laboratorio"))
import numpy as np                                                    # noqa: E402
import preview_niveles as PN                                          # noqa: E402  la cuenta real (importada)
import backtest_familia as BF                                         # noqa: E402

N = int(sys.argv[1]) if len(sys.argv) > 1 else 600
SEM = int(sys.argv[2]) if len(sys.argv) > 2 else 20261008
rng = np.random.default_rng(SEM)


def cola(n, escala, p_cero):
    """magnitudes de cola pesada (como el GEX por strike: unos pocos strikes enormes), con ceros."""
    x = np.abs(rng.standard_t(2.0, n)) * escala * (1.0 + 4.0 * (rng.random(n) < 0.08))
    x[rng.random(n) < p_cero] = 0.0
    return np.round(x)                       # enteros: el json queda chico y la cuenta es la misma


def libro(lb, fut, modo):
    if lb == "NQ":
        paso = float(rng.choice([5.0, 10.0, 25.0, 25.0, 50.0])); conv = 0.0 if rng.random() < 0.85 else float(rng.choice([-35.5, 41.25, 33.0]))
        centro = fut - conv
    elif lb == "NDX":
        paso = float(rng.choice([5.0, 10.0, 25.0]))
        # bases con fraccion .5/.25 exacta a veces (para que el redondeo a la grilla de 5 de la familia caiga en un empate x.5)
        conv = float(rng.choice([241.5, 242.5, 238.75])) if rng.random() < 0.25 else float(np.round(rng.uniform(150.0, 300.0), 8))
        centro = fut - conv
    else:
        paso = float(rng.choice([0.5, 1.0])); conv = float(np.round(rng.uniform(41.20, 41.60), 10))
        centro = fut / conv
    lo = math.floor(centro * 0.955 / paso) * paso; hi = centro * 1.045
    K = np.round(np.arange(lo, hi, paso), 6)
    n = len(K)
    esc = {"NQ": 2e8, "NDX": 4e7, "QQQ": 6e8}[lb]
    gvC = cola(n, esc, 0.35); gvP = -cola(n, esc, 0.35)
    goC = cola(n, esc * 0.3, 0.25); goP = -cola(n, esc * 0.3, 0.25)
    # empates exactos (argmax: gana la primera ocurrencia)
    if modo % 5 == 0 and n > 10:
        i, j = rng.choice(n, 2, replace=False); gvC[j] = gvC[i] = max(gvC.max(), 1.0)
        i, j = rng.choice(n, 2, replace=False); goP[j] = goP[i] = min(goP.min(), -1.0)
    gv = gvC + gvP; go = goC + goP
    # islas (C3): un strike chico de signo contrario entre dos grandes del mismo signo
    if modo % 3 == 0 and n > 12:
        for _ in range(3):
            m = int(rng.integers(2, n - 2))
            s = 1.0 if rng.random() < 0.5 else -1.0
            gvC[m - 1] = gvC[m + 1] = 0.0; gvP[m - 1] = gvP[m + 1] = 0.0
            if s > 0: gvC[m - 1] = gvC[m + 1] = esc * 3
            else: gvP[m - 1] = gvP[m + 1] = -esc * 3
            v = esc * 3 * float(rng.choice([0.05, 0.099, 0.1, 0.101, 0.2]))   # bordes del umbral 10 % (esc*3*0.1 es entero: borde exacto)
            gvC[m] = 0.0 if s > 0 else v; gvP[m] = -v if s > 0 else 0.0
        gv = gvC + gvP
    # ceros exactos de gv (un cero exacto no cruza)
    if modo % 7 == 0 and n > 6:
        m = int(rng.integers(1, n - 1)); gvC[m] = 5e6; gvP[m] = -5e6; gv = gvC + gvP
    r = dict(K=K, gvC=gvC, gvP=gvP, goC=goC, goP=goP, gv=gv, go=go)
    if lb == "QQQ":
        al_fut = (lambda k, z=conv: k * z); S = fut / conv
    else:
        al_fut = (lambda k, c=conv: k + c); S = fut - conv
    zv = float(fut + rng.normal(0, 180)) if rng.random() < 0.85 else float("nan")
    zo = float(fut + rng.normal(0, 250)) if rng.random() < 0.75 else float("nan")
    oi_ok = bool(rng.random() < 0.8) if lb == "NQ" else True
    oi_fresco = bool(rng.random() < 0.8)
    return BF.BM(lb, fut, 0, r, al_fut, zv, zo, oi_ok, oi_fresco, S, conv)


def f(x):
    x = float(x)
    return None if x != x else x


def caso(i):
    fut = float(np.round(rng.uniform(24000.0, 32000.0) / 0.25) * 0.25)
    u = rng.random()
    libros = list(BF.LIBROS) if u < 0.7 else [lb for lb in BF.LIBROS if rng.random() < 0.6]
    if not libros:
        libros = ["NQ"]
    bk = {lb: libro(lb, fut, i) for lb in BF.LIBROS if lb in libros}
    # lo mismo que preview_niveles.Motor.calcular para un minuto
    conj = BF.conjuntos_minuto(bk, {})
    fus = BF.fusion([bk[lb] for lb in BF.LIBROS], next(iter(bk.values())).fut) if all(lb in bk for lb in BF.LIBROS) else None
    reg, crudo = {}, {}
    for sid in PN.CALC:
        lv = conj.get(sid)
        if not lv:
            continue
        s = PN.SID[sid]
        b = fus if s["libro"] == "familia" and s["tipo"] != "CONF" else bk.get(s["libro"])
        reg[sid] = [[round(float(p), 2), e, PN.Motor._monto(b, s, float(p), e)] for p, e in lv]
        crudo[sid] = [float(p) for p, e in lv]
    ztp_qqq = BF.ztp(bk["QQQ"], False) if "QQQ" in bk else []
    ent = {}
    for lb, b in bk.items():
        # gv y go no van: son gvC + gvP y goC + goP elemento a elemento (BF.BM las filtra igual); el C# las rehace con la misma suma
        assert np.array_equal(b.gv, b.gvC + b.gvP) and np.array_equal(b.go, b.goC + b.goP)
        ent[lb] = dict(fut=b.fut, Fut=[float(x) for x in b.Fut], gvC=[float(x) for x in b.gvC], gvP=[float(x) for x in b.gvP],
                       goC=[float(x) for x in b.goC], goP=[float(x) for x in b.goP],
                       zv=f(b.zv), zo=f(b.zo), oi_ok=bool(b.oi_ok), oi_fresco=bool(b.oi_fresco), S=float(b.S), conv=float(b.conv))
    return dict(i=i, fut=fut, libros=ent, reg=reg, crudo=crudo, ztp_qqq=[[float(p), e] for p, e in ztp_qqq])


def main():
    casos = [caso(i) for i in range(N)]
    out = dict(origen="gen_casos_reglas.py: preview_niveles.Motor.calcular (backtest_familia.conjuntos_minuto/fusion + Motor._monto) sobre libros sinteticos",
               semilla=SEM, n=N, calc=PN.CALC, casos=casos)
    os.makedirs(os.path.join(AQUI, "datos"), exist_ok=True)
    ruta = os.path.join(AQUI, "datos", "casos-reglas.json")
    with open(ruta, "w", encoding="utf-8") as fh:
        json.dump(out, fh, ensure_ascii=False, allow_nan=False, default=str)
    from collections import Counter
    c = Counter(s for x in casos for s in x["reg"])
    print("escrito %s: %d casos; series %s" % (ruta, N, dict(sorted(c.items()))))


if __name__ == "__main__":
    main()
