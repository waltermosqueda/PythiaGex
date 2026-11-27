# -*- coding: utf-8 -*-
"""ZONAS, paso 4: grupos que dependen del lado. Bandas del VWAP del lado opuesto (la operacion va HACIA el VWAP: reversion) contra
las del lado de la extension (continuacion); niveles estaticos 'naturales' (minimos como soporte) contra 'inversion' (maximo roto
como soporte); centros y VWAP lentos. Ventanas: toque [-T,0], zona [-T,X/4], barrida (0,X/2] y vuelve, barrida profunda (X/2,X].
Todo se simula (S6, S1) y se registra; tambien el nivel evento."""
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


VENT = {"toque[-T,0]": (0, 0.0, True, False), "zona[-T,X/4]": (0, 0.25, True, False), "barr(0,X/2]": (1e-6, 0.5, False, True),
        "barr(X/2,X]": (0.5 + 1e-6, 1.0, False, True)}
GR = ["bandas_rev", "bandas_rev_2sd", "bandas_rev_1sd", "bandas_cont", "vwap_centro", "vwap_lento", "est_naturales", "est_inversion", "valor"]
filas = []
for g in GR:
    for vn, (lo, hi, loT, vu) in VENT.items():
        m = Z.contacto(df, g, lo, hi, vuelve=vu, lo_T=loT) > 0
        sim = Z.evaluar("Z4_%s_%s" % (g, vn), "a" if not vn.startswith("barr") else "d", "contacto %s con un nivel del grupo (por lado) %s" % (vn, g), m,
                        ("S6", "S1"), nota="grupo por lado; tambien a nivel evento", imprimir=False)
        for sal in ("S6", "S1"):
            r = ev(m, sal); r.update(grupo=g, ventana=vn, salida=sal, sim_n=sim[sal]["n"], sim_acierto=sim[sal].get("acierto_pct"),
                                     sim_neto050=sim[sal].get("neto_op_c050"), sim_t050=sim[sal].get("t_rueda_c050"))
            filas.append(r)
o = pd.DataFrame(filas)
o.to_csv(os.path.join(Z.AQUI, "z04_lado.csv"), index=False)
for sal in ("S6", "S1"):
    print("== %s: nivel evento (n / acierto / R medio)" % sal)
    q = o[o.salida == sal].copy()
    q["celda"] = q.apply(lambda r: "%4d %5.1f %+.2f" % (r["n"], r.get("acierto", np.nan), r.get("R_medio", np.nan)) if r["n"] >= 5 else "%4d" % r["n"], axis=1)
    print(q.pivot(index="grupo", columns="ventana", values="celda")[list(VENT)].to_string())
    print("   simulado (n / acierto / neto050 / t):")
    q["sim"] = q.apply(lambda r: "%3d %5s %+.2f %+.2f" % (r["sim_n"], r["sim_acierto"], r["sim_neto050"] if r["sim_n"] else 0, r["sim_t050"]), axis=1)
    print(q.pivot(index="grupo", columns="ventana", values="sim")[list(VENT)].to_string())
