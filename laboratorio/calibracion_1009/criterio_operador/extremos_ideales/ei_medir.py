# -*- coding: utf-8 -*-
"""ei_medir.py — mide UN conjunto (o varios) con el juez del operador SIN CAMBIARLO, en las dos ventanas, los tres modos, contra
su placebo (corridas +-11/19/31 y juegos al azar) y aparte esta noche. Lee velas_m1.csv, niv20_m1.pkl y noches.pkl (solo lectura).
Escribe resultados/<CONJUNTO>.json y resultados/<CONJUNTO>_eventos.pkl en esta carpeta (salidas propias, no datos de entrada).
Uso: python -I ei_medir.py CONJUNTO [CONJUNTO ...] [--azar 500] [--azar_otros 200]
CONJUNTOS: REF_2_0 (dominantes que dibujo la 2.0: dom0, dom1, ndx_dom0, ndx_dom1) y los de ei_conjuntos.py.
VENTANAS (19 noches de noches.pkl, las mismas de demo_escala.py / c03_reacciones.py):
  congelada: N 00:35Z -> N 08:30Z (lunes: domingo 22:00Z -> 08:30Z)
  completa : N-1 22:00Z -> N 13:30Z
  esta noche (N = 2026-10-09): las mismas dos, cortadas por la ultima vela (03:22Z)."""
import json
import math
import os
import pickle
import sys
import time

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
sys.path.insert(0, os.path.join(os.path.dirname(AQUI), "juez"))
import ei_conjuntos as C  # noqa: E402
import juez_operador as J  # noqa: E402

CAL = os.path.join(os.environ["LOCALAPPDATA"], "Temp", "claude", "C--Users-wmx-7-OneDrive-Escritorio-ATAS-nada",
                   "f5879819-e2a4-405b-87b0-959ace090d7b", "scratchpad", "calibrar")
SAL = os.path.join(AQUI, "resultados")
HOY = "2026-10-09"
MODOS = ("tolerante", "estricto", "muy_tolerante")


def ventanas():
    cong, comp = [], []
    for n in pd.read_pickle(os.path.join(CAL, "noches.pkl")):
        D = pd.Timestamp(n["D"]); N = pd.Timestamp(n["N"])
        a = N - pd.Timedelta(hours=2) if D.weekday() == 4 else N + pd.Timedelta(minutes=35)
        cong.append((n["N"], a, N + pd.Timedelta(hours=8, minutes=30)))
        comp.append((n["N"], N - pd.Timedelta(hours=2), N + pd.Timedelta(hours=13, minutes=30)))
    return {"congelada": cong, "completa": comp}


def rayas(V, nombre):
    if nombre == "REF_2_0":
        niv = pd.read_pickle(os.path.join(CAL, "niv20_m1.pkl"))
        return J.rayas_desde_niv20(niv, ("dom0", "dom1", "ndx_dom0", "ndx_dom1"))
    return C.armar(V, nombre)


def limpio(x):
    if isinstance(x, dict):
        return {str(k): limpio(v) for k, v in x.items()}
    if isinstance(x, (list, tuple)):
        return [limpio(v) for v in x]
    if isinstance(x, (pd.Timestamp,)):
        return str(x)
    if isinstance(x, (np.integer,)):
        return int(x)
    if isinstance(x, (np.floating, float)):
        x = float(x)
        return None if not math.isfinite(x) else round(x, 4)
    if isinstance(x, (np.bool_,)):
        return bool(x)
    return x


def por_noche(eventos, giros, noches):
    out = {}
    for n in noches:
        E = [e for e in eventos if e["noche"] == n]
        m = J.resumir(E)
        G = [g for g in giros if g["noche"] == n]
        m["giros"] = len(G); m["giros_cubiertos"] = sum(g["cubierto"] for g in G)
        m["cobertura_pct"] = 100.0 * m["giros_cubiertos"] / len(G) if G else float("nan")
        out[n] = m
    return out


def medir_ventana(V, R, ven, n_azar, n_azar_otros, etiqueta):
    op = {"ventanas": [(a, b) for _, a, b in ven]}
    t0 = time.time()
    prep = J.preparar(V, R, op)
    res = {"n_pistas": len(prep[1]["pistas"]), "noches": [n for n, _, _ in ven], "modos": {}}
    for modo in MODOS:
        na = n_azar if modo == "tolerante" else n_azar_otros
        pl = J.placebo(V, R, dict(op, modo=modo), n_azar=na, _prep=prep)
        d = {k: pl[k] for k in ("real", "azar", "corridos", "corridos_juntos", "bootstrap_ic90", "n_pistas")}
        d["n_azar"] = na
        if modo == "tolerante":
            cg = J.cobertura_giros(V, R, op, _prep=prep)
            d["por_noche"] = por_noche(pl["eventos_real"], cg["giros"], sorted({n for n, _, _ in ven}))
            d["giros"] = [{k: g[k] for k in ("t", "tipo", "precio", "cubierto", "raya_mas_cercana", "noche")} for g in cg["giros"]]
            eventos = pl["eventos_real"]
        res["modos"][modo] = d
        print("   [%s %s %s] llegadas %d sost %.1f %% rotas %.1f %% exactos %.1f %% prec |%.2f| rec medio %.1f cob %.1f %% | "
              "percentiles azar: sost %.0f exactos %.0f prec %.0f rec %.0f cob %.0f (%.0f s)" % (
                  etiqueta, modo, "", pl["real"]["llegadas"], pl["real"]["pct_sostenidos"],
                  pl["real"]["pct_rotas"], pl["real"]["pct_exactos"],
                  pl["real"]["precision_mediana_abs"], pl["real"]["recorrido_medio"], pl["real"].get("cobertura_pct", float("nan")),
                  pl["azar"]["pct_sostenidos"]["percentil"], pl["azar"]["pct_exactos"]["percentil"],
                  pl["azar"]["precision_mediana_abs"]["percentil"], pl["azar"]["recorrido_medio"]["percentil"],
                  pl["azar"]["cobertura_pct"]["percentil"], time.time() - t0), flush=True)
    return res, eventos


def main():
    args = sys.argv[1:]
    n_azar, n_azar_otros = 500, 200
    if "--azar" in args:
        k = args.index("--azar"); n_azar = int(args[k + 1]); del args[k:k + 2]
    if "--azar_otros" in args:
        k = args.index("--azar_otros"); n_azar_otros = int(args[k + 1]); del args[k:k + 2]
    os.makedirs(SAL, exist_ok=True)
    V = C.cargar_velas(os.path.join(CAL, "velas_m1.csv"))
    Vj = V[["t", "o", "h", "l", "c"]]
    VEN = ventanas()
    for nombre in args:
        t0 = time.time()
        R = rayas(V, nombre)
        out = {"conjunto": nombre, "velas": [str(V["t"].iloc[0]), str(V["t"].iloc[-1]), len(V)], "ventanas": {}, "esta_noche": {}}
        evs = {}
        for vn, ven in VEN.items():
            print("%s / %s" % (nombre, vn), flush=True)
            out["ventanas"][vn], evs[vn] = medir_ventana(Vj, R, ven, n_azar, n_azar_otros, vn)
            hoy = [x for x in ven if x[0] == HOY]
            print("%s / %s / esta noche" % (nombre, vn), flush=True)
            out["esta_noche"][vn], evs[vn + "_hoy"] = medir_ventana(Vj, R, hoy, n_azar, n_azar_otros, vn + " hoy")
        with open(os.path.join(SAL, nombre + ".json"), "w", encoding="utf-8") as f:
            json.dump(limpio(out), f, ensure_ascii=False, indent=1)
        cols = ("t_llegada", "noche", "pista", "raya", "etiquetas", "tipo", "resultado", "censurada", "motivo", "min_a_decision",
                "falso_rompimiento", "pinchazo_max", "precision", "precision_abs", "cierres_mas_alla", "recorrido",
                "recorrido_desde_punta", "recorrido_tramo", "fin_recorrido", "recorrido_censurado", "opuesta", "dist_opuesta",
                "llega_opuesta")
        with open(os.path.join(SAL, nombre + "_eventos.pkl"), "wb") as f:
            pickle.dump({k: [{c: e.get(c) for c in cols} for e in v] for k, v in evs.items()}, f)
        print("%s listo en %.0f s" % (nombre, time.time() - t0), flush=True)


if __name__ == "__main__":
    main()
