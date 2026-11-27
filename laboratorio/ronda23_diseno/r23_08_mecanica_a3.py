# -*- coding: utf-8 -*-
"""RONDA 23, paso 8 (POST HOC, descriptivo): la ANATOMIA de las operaciones de A3 - que separa las que llegan al VWAP de las que no.
Muestras: diseno y chequeo (motor de 5 min, velas de MES) y cinta (operaciones orden por orden del script, con los rasgos de su vela).
Uso: python -I r23_08_mecanica_a3.py -> salidas/mecanica_a3.txt"""
import os, pickle, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import r23_comun as K
import r23_03_variantes as V

L = []
def W(s=""):
    print(s, flush=True); L.append(s)

A3 = dict(familia="A", desde="13:00", hasta="15:30", loc=("dv", 1.5), llave=("ib<=1", "Pf>=0.55"), bracket="F1")

def tipo_dia(a):
    o, h, l, c = a["o"][0], a["h"].max(), a["l"].min(), a["c"][-1]
    e = abs(c - o) / (h - l)
    return "tendencia" if e >= 0.6 else ("balance" if e < 0.3 else "mixto")

def main():
    B = pickle.load(open(os.path.join(K.SCR, "ruedas5.pkl"), "rb"))["ruedas"]; Bc = pickle.load(open(os.path.join(K.SCR, "ruedas5_cinta.pkl"), "rb"))["ruedas"]
    Pc = pd.read_parquet(os.path.join(K.SCR, "puntos5_cinta.parquet"))
    tks = []
    for m in ("diseno", "chequeo"):
        s = pd.read_parquet(os.path.join(K.SCR, "post_%s_F1.parquet" % m)); dias = sorted(s.dia.unique())
        tk = V.evaluar(s, A3, dias).copy(); tk["muestra"] = m; tk["gana"] = tk.pts_real > 0; tk["pts"] = tk.pts_real
        tk["dia_tipo"] = tk.dia.map(lambda d: tipo_dia(B[d])); tks.append(tk)
    c = pd.read_csv(os.path.join(K.SAL, "ops-cinta-A3.csv")).rename(columns={"fecha": "dia"})
    c = c.merge(Pc, on=["dia", "k"], how="left", suffixes=("", "_v")); c["muestra"] = "cinta"; c["gana"] = c.pts > 0
    c["dia_tipo"] = c.dia.map(lambda d: tipo_dia(Bc[d])); c["min_dia"] = 575 + 5 * c.k
    tks.append(c)
    A = pd.concat(tks, ignore_index=True)
    W("RONDA 23 - ANATOMIA DE A3 (post hoc, descriptivo): %d operaciones (%s)" % (len(A), A.muestra.value_counts().to_dict()))
    def corte(col, bins, nombre):
        W("  por %s:" % nombre)
        b = pd.cut(A[col], bins)
        for iv, g in A.groupby(b, observed=True):
            fila = []
            for m in ("diseno", "chequeo", "cinta"):
                x = g[g.muestra == m]
                fila.append("%s %3d %5.1f %% %+6.2f" % (m[:3], len(x), 100 * x.gana.mean() if len(x) else np.nan, (x.pts - 1).mean() if len(x) else np.nan))
            W("    %-14s todas %3d %5.1f %% neto %+6.2f | %s" % (str(iv), len(g), 100 * g.gana.mean(), (g.pts - 1).mean(), " | ".join(fila)))
    corte("min_dia", [779, 810, 840, 870, 900, 931], "hora de entrada")
    corte("dv", [1.5, 2, 2.5, 3, 99], "distancia al VWAP (X)")
    corte("dx", [0, 1.5, 2, 2.5, 3, 99], "distancia al extremo del dia (X)")
    corte("bext", [-1, 3, 6, 12, 24, 99], "velas desde que se marco el extremo")
    corte("ret3", [-99, -1.5, -0.75, 0, 99], "movimiento de los ultimos 15 min (X; negativo = ya viene hacia el VWAP)")
    corte("ext_lado", [-0.01, 0.0001, 0.5, 1.01], "extension del IB del lado del precio")
    corte("rrn", [0, 1.0, 1.3, 1.6, 2.0, 99], "rango del dia contra lo normal a esa hora (rrn)")
    corte("n_toques", [-1, 3, 8, 15, 99], "toques del VWAP en el dia hasta la entrada")
    W("  por como TERMINO el dia (ex post: explica, no se puede usar en vivo):")
    for t_, g in A.groupby("dia_tipo"):
        W("    %-10s n %3d acierto %5.1f %% neto %+6.2f" % (t_, len(g), 100 * g.gana.mean(), (g.pts - 1).mean()))
    W("  perdedoras: cuanto fueron a favor antes de perder (mfe, pts) mediana %.2f | ganadoras: cuanto fueron en contra antes de ganar (mae) mediana %.2f" % (
        A[~A.gana].mfe.median(), A[A.gana].mae.median()))
    open(os.path.join(K.SAL, "mecanica_a3.txt"), "w", encoding="utf-8").write("\n".join(L))

if __name__ == "__main__":
    main()
