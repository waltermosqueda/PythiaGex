# -*- coding: utf-8 -*-
"""RONDA 27, paso 2: COMPROBACION de la tabla por minuto.
A) Contra lo ya auditado de rondas anteriores, en las 55 ruedas: X y mediana de 10 (r19 rasgos/*.json), clima minuto a minuto
   (r21 estados_minuto: B = ESTRECHO, D = ROTACION o lados en desacuerdo, T = TENDENCIA), precio, maximo, minimo, VWAP y desvio
   (r21 regimen_minuto) y estado de la ola y la marea (r19 rasgos/*.npz en el segundo 60m+59).
B) Desde la CINTA CRUDA con codigo propio sin numpy en el nucleo (modulo csv + la clase de referencia test_ola.Maquina): ruedas elegidas por
   regla (la primera de cada tramo) y 5 minutos fijos (cierres 9:31, 10:30, 12:01, 13:31, 15:01): ola y marea, VWAP RTH, rrn, la etiqueta
   y_X y las salidas pL_X / pS_X. Y todos los pivotes de la ola y la marea de una rueda: maquina en numba contra test_ola.Maquina.
Uso: python -I r27_02_verificar.py"""
import csv, json, math, os, sys
from datetime import datetime, timedelta, timezone
from zoneinfo import ZoneInfo
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r27_comun as K
import numpy as np, pandas as pd
sys.path.insert(0, os.path.join(K.RAIZ, "herramientas"))
import test_ola as REF

NY = ZoneInfo("America/New_York")
TAB = os.path.join(K.DATOS, "tabla_minutos.parquet")


def parte_a(T):
    print("A) contra las tablas auditadas de la R19 y la R21")
    est = pd.read_parquet(os.path.join(K.SCR, "r21", "estados_minuto.parquet"))
    reg = pd.read_parquet(os.path.join(K.SCR, "r21", "regimen_minuto.parquet"))
    mapa = {"E": "B", "R": "D", "N": "D", "T": "T"}
    tot = {"X": 0, "med": 0, "tipo": 0, "tipo_n": 0, "c": 0, "H": 0, "L": 0, "vw": 0.0, "sd": 0.0, "ola": 0, "ola_n": 0}
    malos = []
    for dia, g in T.groupby("dia"):
        meta = json.load(open(os.path.join(K.SCR, "r19", "rasgos", "ES-%s.json" % dia)))
        tot["X"] += int(g["X"].iloc[0] == meta["X"]); tot["med"] += int(abs(g["med10"].iloc[0] - meta["mediana_rango_10"]) < 1e-9)
        e = est[est.dia == dia].set_index("m").reindex(g["m"])
        r = reg[reg.dia == dia].set_index("m").reindex(g["m"])
        tg = g["tipo"].map(mapa).to_numpy()
        ok = tg == e["estado"].to_numpy()
        tot["tipo"] += int(ok.sum()); tot["tipo_n"] += len(ok)
        if not ok.all():
            malos.append((dia, int((~ok).sum())))
        for a in ("c", "H", "L"):
            tot[a] += int(np.isclose(g[a].to_numpy(), r[a].to_numpy()).sum())
        tot["vw"] = max(tot["vw"], float(np.nanmax(np.abs(g["vw"].to_numpy() - r["vw"].to_numpy()))))
        tot["sd"] = max(tot["sd"], float(np.nanmax(np.abs(g["sd"].to_numpy() - r["sd"].to_numpy()))))
        z = np.load(os.path.join(K.SCR, "r19", "rasgos", "ES-%s.npz" % dia)); sec = 60 * g["m"].to_numpy() + 59
        okz = np.ones(len(g), bool)
        for a in ("o_dir", "o_ext", "o_p0", "m_dir", "m_ext", "m_p0"):
            okz &= np.isclose(g[a].to_numpy(), z[a][sec], equal_nan=True)
        tot["ola"] += int(okz.sum()); tot["ola_n"] += len(okz)
    nd = T.dia.nunique(); n = len(T)
    print("  ruedas %d, minutos %d" % (nd, n))
    print("  X igual a la R19: %d/%d; mediana de 10 igual: %d/%d" % (tot["X"], nd, tot["med"], nd))
    print("  clima igual a la R21 minuto a minuto: %d/%d%s" % (tot["tipo"], tot["tipo_n"], (" (distintos: %s)" % malos[:10]) if malos else ""))
    print("  precio, maximo y minimo iguales a la R21: %d, %d, %d de %d" % (tot["c"], tot["H"], tot["L"], n))
    print("  VWAP RTH: diferencia maxima con la R21 %.6f pts; desvio %.6f (la R19 saca 44-ish duplicados exactos por rueda)" % (tot["vw"], tot["sd"]))
    print("  ola y marea (dir, extremo, origen) iguales a la R19: %d/%d" % (tot["ola"], tot["ola_n"]))
    return tot


def leer_crudo(ruta):
    filas = []
    with open(ruta, newline="") as fh:
        r = csv.reader(fh); next(r)
        for x in r:
            filas.append((datetime.strptime(x[0][:23], "%Y-%m-%dT%H:%M:%S.%f").replace(tzinfo=timezone.utc), float(x[1]), float(x[2]),
                          float(x[3]), int(x[4])))
    return filas


def parte_b(T):
    print("\nB) desde la cinta cruda, codigo propio (csv + test_ola.Maquina)")
    cin = K.cintas("ES")
    f = K.curva_f()
    dias = [T[T.tramo == tr]["dia"].min() for tr in ("diseno", "confirmacion", "final")]
    minutos = [0, 59, 150, 240, 330]
    tot = ok = 0
    for dia in dias:
        g = T[T.dia == dia].set_index("m")
        X = float(g["X"].iloc[0]); med = float(g["med10"].iloc[0]); Y = X
        filas = leer_crudo(cin[dia])
        d0 = datetime.strptime(dia, "%Y-%m-%d").replace(tzinfo=NY)
        cortes = {m: (d0 + timedelta(hours=9, minutes=31 + m)).astimezone(timezone.utc) for m in minutos}
        rth0 = (d0 + timedelta(hours=9, minutes=30)).astimezone(timezone.utc); rth1 = (d0 + timedelta(hours=16)).astimezone(timezone.utc)
        ola = REF.Maquina(X); marea = REF.Maquina(4 * X)
        cum = 0.0; ult = None; pv = 0.0; p2v = 0.0; vv = 0.0; H = -1e18; L = 1e18; O = None
        estado = {}
        pend = sorted(minutos)
        for i, (t, a, b, v, lado) in enumerate(filas):
            while pend and t >= cortes[pend[0]]:
                m = pend.pop(0)
                vw = pv / vv; sd = math.sqrt(max(p2v / vv - vw * vw, 0.0))
                estado[m] = dict(c=ult, o_dir=ola.dir, o_ext=ola.ext, o_p0=ola.pivotes[-1][1], m_dir=marea.dir, m_ext=marea.ext,
                                 m_p0=marea.pivotes[-1][1], vw=vw, sd=sd, rrn=(H - L) / (med * f[m]), i=i)
            cum += v * lado; ult = b
            ola.paso(t, min(a, b), max(a, b), cum); marea.paso(t, min(a, b), max(a, b), cum)
            if rth0 <= t < rth1:
                pm = (a + b) / 2.0; pv += pm * v; p2v += pm * pm * v; vv += v
                H = max(H, a, b); L = min(L, a, b)
                if O is None: O = b
        # resultados: recorrer las ordenes posteriores al corte hasta min(corte + 60 min, 16:00)
        for m in minutos:
            e = estado[m]; c = e["c"]; fin = min(cortes[m] + timedelta(minutes=60), rth1)
            lab = None; pl = ps = None
            for (t, a, b, v, lado) in filas[e["i"]:]:
                if t >= fin: break
                h, l = max(a, b), min(a, b)
                if lab is None:
                    if h >= c + Y and l <= c - Y: lab = 2
                    elif h >= c + Y: lab = 1
                    elif l <= c - Y: lab = 0
                if pl is None:
                    if l <= c - Y: pl = -Y
                    elif h >= c + Y + 0.25: pl = Y
                if ps is None:
                    if h >= c + Y: ps = -Y
                    elif l <= c - Y - 0.25: ps = Y
                ultimo = b
                if lab is not None and pl is not None and ps is not None: break
            if pl is None: pl = ultimo - c
            if ps is None: ps = c - ultimo
            r = g.loc[m]
            yref = np.nan if lab in (None, 2) else float(lab)
            comp = [("c", c, r["c"]), ("o_dir", e["o_dir"], r["o_dir"]), ("o_ext", e["o_ext"], r["o_ext"]), ("o_p0", e["o_p0"], r["o_p0"]),
                    ("m_dir", e["m_dir"], r["m_dir"]), ("m_ext", e["m_ext"], r["m_ext"]), ("m_p0", e["m_p0"], r["m_p0"]),
                    ("vw", e["vw"], r["vw"]), ("sd", e["sd"], r["sd"]), ("rrn", e["rrn"], r["rrn"]), ("y_X", yref, r["y_X"]),
                    ("pL_X", pl, r["pL_X"]), ("pS_X", ps, r["pS_X"])]
            bien = all((isinstance(x, float) and isinstance(y, float) and math.isnan(x) and math.isnan(y)) or abs(float(x) - float(y)) < 1e-6
                       for _, x, y in comp)
            tot += 1; ok += int(bien)
            print("  %s cierre %s | c %.2f ola %+d ext %.2f origen %.2f marea %+d ext %.2f | VWAP %.3f rrn %.3f | y_X %s pL %+.2f pS %+.2f | %s"
                  % (dia, (d0 + timedelta(hours=9, minutes=31 + m)).strftime("%H:%M"), c, e["o_dir"], e["o_ext"], e["o_p0"], e["m_dir"],
                     e["m_ext"], e["vw"], e["rrn"], yref, pl, ps, "IGUAL" if bien else "DISTINTO " + str([(n_, x, y) for n_, x, y in comp])))
    print("  minutos comprobados desde la cinta cruda: %d de %d iguales" % (ok, tot))
    # todos los pivotes de una rueda: numba contra la referencia
    dia = dias[1]; X = float(T[T.dia == dia]["X"].iloc[0])
    C = K.leer_cinta(cin[dia], dia)
    mn = np.minimum(C["primero"], C["ultimo"]); mx = np.maximum(C["primero"], C["ultimo"]); cum = np.cumsum(C["vol"] * C["lado"])
    filas = leer_crudo(cin[dia])
    for esc, XX in (("ola", X), ("marea", 4 * X)):
        _, _, _, _, pei, pp, pt, pci, pc = K.maquina_nb(C["t"], mn, mx, cum, XX)
        q = REF.Maquina(XX); cc = 0.0
        for (t, a, b, v, lado) in filas:
            cc += v * lado; q.paso(t, min(a, b), max(a, b), cc)
        mio = [(int(pei[j]), float(pp[j]), int(pt[j]), int(pci[j]), float(pc[j])) for j in range(len(pei))]
        idx = {}
        ref = [(p[1], 1 if p[2] == "max" else -1, p[4]) for p in q.pivotes]
        iguales = sum(1 for a_, b_ in zip(mio, ref) if a_[1] == b_[0] and a_[2] == b_[1] and abs(a_[4] - b_[2]) < 1e-6)
        print("  pivotes de la %s del %s: numba %d, referencia %d, iguales (precio, tipo, cum) %d" % (esc, dia, len(mio), len(ref), iguales))
    return ok, tot


def main():
    T = pd.read_parquet(TAB)
    parte_a(T)
    parte_b(T)


if __name__ == "__main__":
    main()
