# -*- coding: utf-8 -*-
"""esc_01_reproducir.py — ESCEPTICO 1: reproduce con el JUEZ PROPIO (esc_comun, sin codigo del juez oficial) los numeros clave que
las familias dieron como 'le gana al placebo' (MIRAR) o como lo mejor de PRUEBA, sobre las MISMAS rayas, velas y ventanas.
SOLO LECTURA. Escribe datos/reproduccion.json y datos/reproduccion.txt. Uso: python -I esc_01_reproducir.py"""
import ctypes
try:
    ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)
except Exception:
    pass
import json
import os
import sys
import time

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import numpy as np  # noqa: E402
import pandas as pd  # noqa: E402
import esc_comun as E  # noqa: E402

OUT = os.path.join(AQUI, "datos")
os.makedirs(OUT, exist_ok=True)
LOG = []


def out(s):
    print(s, flush=True); LOG.append(s)


def comparar_eventos(mios, oficiales):
    """coincidencia por (minuto de llegada, raya redondeada a 0,01) y resultado."""
    A = {(e["t"], round(e["raya"], 2)): e["res"] for e in mios}
    mapa = {"sostenido": "acierto", "rota": "falsa", "indefinida": "indefinida"}
    B = {}
    for e in oficiales:
        r = "censurada" if e.get("censurada") else mapa.get(e["resultado"], e["resultado"])
        B[(pd.Timestamp(e["t_llegada"]), round(float(e["raya"]), 2))] = r
    com = set(A) & set(B)
    igual = sum(A[k] == B[k] for k in com)
    return dict(mios=len(A), oficiales=len(B), en_comun=len(com), mismo_resultado=igual)


def main():
    t0 = time.time()
    res = {}
    # ---------------- NDX
    V, S = E.ndx_cargar()
    VV = E.preparar_velas(V)
    for ven in ("noche",):
        mirar, prueba = E.ndx_sesiones(ven)
        for nombre, serie in (("R20_NDX_vol", "R20_NDX_vol"), ("DOMS_NDX_vol", "DOMS_NDX_vol"), ("MUROS_NDX_vol", "MUROS_NDX_vol")):
            for parte, ses in (("mirar", mirar), ("prueba", prueba)):
                r, evs = E.juzgar(VV, S[serie], E.ventanas_noche(ses), desfase=0, claves=("D1", "D2"))
                ofi = json.load(open(os.path.join(E.NDX, "datos", "juez", "%s__%s__%s.json" % (nombre, ven, parte)), encoding="utf-8"))
                evo = pd.read_pickle(os.path.join(E.NDX, "datos", "juez", "%s__%s__%s_eventos.pkl" % (nombre, ven, parte)))
                cmp_ = comparar_eventos(evs, evo)
                mo = ofi["metricas"]
                k = "NDX|%s|%s|%s" % (nombre, ven, parte)
                res[k] = dict(propio=r, oficial={x: mo[x] for x in ("llegadas", "aciertos", "falsas", "indefinidas", "censuradas", "pct_acierto20")},
                              eventos=cmp_, n_sesiones=len(ses))
                out("%-40s propio: %4d llegadas %5.1f %% (ac %d fa %d ind %d cen %d) | oficial: %4d %5.1f %% | eventos en comun %d/%d, mismo resultado %d" % (
                    k, r["llegadas"], r["pct_acierto20"], r["aciertos"], r["falsas"], r["indefinidas"], r["censuradas"],
                    mo["llegadas"], mo["pct_acierto20"], cmp_["en_comun"], cmp_["oficiales"], cmp_["mismo_resultado"]))
    # ---------------- NQ
    V, reg, part = E.nq_cargar()
    VV = E.preparar_velas(V)
    TJ = pd.read_csv(os.path.join(E.NQD, "datos", "juez_nq_tabla.csv"))
    for serie, vt, parte in (("MUROS_NQ_vol", "noche", "mirar"), ("MUROS_NQ_vol", "noche", "prueba"), ("MAJORS_NQ_vol", "noche", "mirar"),
                             ("MUROS_NQ_vol.P", "noche", "mirar"), ("ZTP_NQ_vol", "noche", "prueba"), ("ZTP_NQ_vol", "noche", "mirar")):
        dias = part[vt][parte]
        rayas, vent = E.nq_rayas_y_ventanas(reg, dias, serie, vt)
        r, evs = E.juzgar(VV, rayas, vent, desfase=0)
        o = TJ[(TJ.serie == serie) & (TJ.ventana == vt) & (TJ.parte == parte) & (TJ.n_azar == 500)].iloc[0]
        p_ev = os.path.join(E.NQD, "datos", "eventos", "%s__%s__%s.csv" % (serie, vt, parte))
        cmp_ = {}
        if os.path.exists(p_ev):
            eo = pd.read_csv(p_ev, parse_dates=["t_llegada"])
            cmp_ = comparar_eventos(evs, eo.to_dict("records"))
        k = "NQ|%s|%s|%s" % (serie, vt, parte)
        res[k] = dict(propio=r, oficial=dict(llegadas=int(o.llegadas), aciertos=int(o.aciertos), falsas=int(o.falsas),
                                             pct_acierto20=float(o.pct_acierto20)), eventos=cmp_, n_sesiones=len(dias))
        out("%-40s propio: %4d llegadas %5.1f %% (ac %d fa %d ind %d cen %d) | oficial: %4d %5.1f %% | eventos %s" % (
            k, r["llegadas"], r["pct_acierto20"], r["aciertos"], r["falsas"], r["indefinidas"], r["censuradas"],
            int(o.llegadas), float(o.pct_acierto20), cmp_))
    # ---------------- CONF
    for serie in ("CONF_vol", "CONF_NDX_NQ_vol"):
        V, R = E.conf_cargar(serie)
        for ven, parte in (("rueda", "MIRAR"), ("rueda", "PRUEBA")):
            Vs, Rs, vent, sel = E.conf_parte(V, R, ven, parte)
            VV = E.preparar_velas(Vs)
            r, evs = E.juzgar(VV, Rs, vent, desfase=0)
            ofi = json.load(open(os.path.join(E.CONF, "resultados", "%s__%s__%s.json" % (serie, ven, parte)), encoding="utf-8"))
            mo = ofi["metricas"]
            eo = pd.read_csv(os.path.join(E.CONF, "resultados", "%s__%s__%s_eventos.csv" % (serie, ven, parte)), parse_dates=["t_llegada"])
            cmp_ = comparar_eventos(evs, eo.to_dict("records"))
            k = "CONF|%s|%s|%s" % (serie, ven, parte)
            res[k] = dict(propio=r, oficial={x: mo[x] for x in ("llegadas", "aciertos", "falsas", "indefinidas", "censuradas", "pct_acierto20")},
                          eventos=cmp_, n_sesiones=len(sel))
            out("%-40s propio: %4d llegadas %5.1f %% (ac %d fa %d ind %d cen %d) | oficial: %4d %5.1f %% | eventos en comun %d/%d, mismo resultado %d" % (
                k, r["llegadas"], r["pct_acierto20"], r["aciertos"], r["falsas"], r["indefinidas"], r["censuradas"],
                mo["llegadas"], mo["pct_acierto20"], cmp_["en_comun"], cmp_["oficiales"], cmp_["mismo_resultado"]))
    json.dump(res, open(os.path.join(OUT, "reproduccion.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1, default=str)
    open(os.path.join(OUT, "reproduccion.txt"), "w", encoding="utf-8").write("\n".join(LOG) + "\n")
    out("listo %.0f s" % (time.time() - t0))


if __name__ == "__main__":
    main()
