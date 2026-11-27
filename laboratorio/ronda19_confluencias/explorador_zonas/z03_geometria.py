# -*- coding: utf-8 -*-
"""ZONAS, paso 3: geometria del contacto x grupo de nivel. pen = cuanto paso el extremo mas alla del nivel (en X; > 0 = perforo).
Ventanas: toque sin perforar [-T,0], (0,X/4], (X/4,X/2], (X/2,3X/4], (3X/4,X], (X/2,X]; las que perforan exigen que la entrada
quede de vuelta del otro lado del nivel. Cada combinacion ventana x grupo se simula (S6, S1) y se registra; se imprime ademas el
nivel evento (todas las vueltas, sin una-posicion-por-vez) para ver la informacion sin el tope."""
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import zonas_comun as Z

df = Z.cargar()
H = ((df.seg_rueda >= Z.H_INI) & (df.seg_rueda < Z.H_FIN)).to_numpy()


def ev(mask, sal):
    m = np.asarray(mask, bool) & H & df[sal + "_pts"].notna().to_numpy()
    pts = df[sal + "_pts"].to_numpy()[m]; R = df["R"].to_numpy()[m]
    if len(pts) < 5: return dict(n=len(pts))
    d = pd.Series(pts - 0.5, index=df["dia"].to_numpy()[m]).groupby(level=0).sum().reindex(Z.DIAS, fill_value=0)
    return dict(n=len(pts), acierto=round(100 * (pts > 0).mean(), 1), R_medio=round(float((pts / R).mean()), 3), t_todo=round(Z.t_rueda(d), 2))


VENT = {"toque[-T,0]": (0, 0.0, True, False), "pen(0,X/4]": (1e-6, 0.25, False, True), "pen(X/4,X/2]": (0.25 + 1e-6, 0.5, False, True),
        "pen(X/2,3X/4]": (0.5 + 1e-6, 0.75, False, True), "pen(3X/4,X]": (0.75 + 1e-6, 1.0, False, True), "pen(X/2,X]": (0.5 + 1e-6, 1.0, False, True)}
GR = ["todos", "dinamicos", "vwap_rth_bandas", "vwap_rth_2sd", "estaticos_sin_r50", "extremos", "ayer", "noche", "ib", "r50"]
filas = []
for g in GR:
    for vn, (lo, hi, loT, vu) in VENT.items():
        m = Z.contacto(df, g, lo, hi, vuelve=vu, lo_T=loT) > 0
        sim = Z.evaluar("Z3_%s_%s" % (g, vn), "a" if vn.startswith("toque") else "d", "contacto %s con un nivel del grupo %s" % (vn, g), m,
                        ("S6", "S1"), nota="geometria x grupo; tambien a nivel evento", imprimir=False)
        for sal in ("S6", "S1"):
            r = ev(m, sal); r.update(grupo=g, ventana=vn, salida=sal, sim_n=sim[sal]["n"], sim_acierto=sim[sal].get("acierto_pct"),
                                     sim_neto050=sim[sal].get("neto_op_c050"), sim_t050=sim[sal].get("t_rueda_c050"))
            filas.append(r)
o = pd.DataFrame(filas)
o.to_csv(os.path.join(Z.AQUI, "z03_geometria.csv"), index=False)
for sal in ("S6", "S1"):
    print("== %s: nivel evento (n / acierto / R medio) por grupo x ventana" % sal)
    q = o[o.salida == sal].copy()
    q["celda"] = q.apply(lambda r: "%4d %5.1f %+.2f" % (r["n"], r.get("acierto", np.nan), r.get("R_medio", np.nan)) if r["n"] >= 5 else "%4d" % r["n"], axis=1)
    print(q.pivot(index="grupo", columns="ventana", values="celda")[list(VENT)].to_string())
    print("   simulado (n / acierto / neto050 / t):")
    q["sim"] = q.apply(lambda r: "%3d %5s %+.2f %+.2f" % (r["sim_n"], r["sim_acierto"], r["sim_neto050"] if r["sim_n"] else 0, r["sim_t050"]), axis=1)
    print(q.pivot(index="grupo", columns="ventana", values="sim")[list(VENT)].to_string())
