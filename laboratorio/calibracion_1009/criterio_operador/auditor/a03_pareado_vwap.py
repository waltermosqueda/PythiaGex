# -*- coding: utf-8 -*-
"""a03_pareado_vwap.py — AUDITOR. SOLO LECTURA de los eventos de extremos_ideales/resultados (REF_2_0 y conjuntos de precio solo).
Rehace el pareado por noche '2.0 menos conjunto' en % sostenidos (TOLERANTE) con y sin 10-09 / 10-05, con un bootstrap propio de
noches. Tambien cuenta re-tests: llegadas de la misma raya a <= 15 min de la decision anterior de esa raya.
Correr: python -I a03_pareado_vwap.py"""
import os
import pickle

import numpy as np

AQUI = os.path.dirname(os.path.abspath(__file__))
SAL = os.path.join(os.path.dirname(AQUI), "extremos_ideales", "resultados")


def por_noche(evs):
    d = {}
    for e in evs:
        x = d.setdefault(e["noche"], [0, 0])
        if not e["censurada"]:
            x[0] += 1
        x[1] += e["resultado"] == "sostenido"
    return d


def pareado(A, B, noches, n_boot=4000, sem=7):
    a = np.array([A.get(n, [0, 0]) for n in noches], float); b = np.array([B.get(n, [0, 0]) for n in noches], float)
    def dif(w):
        return 100 * (w @ a[:, 1]) / (w @ a[:, 0]) - 100 * (w @ b[:, 1]) / (w @ b[:, 0])
    rng = np.random.default_rng(sem)
    v = [dif(np.bincount(rng.integers(0, len(noches), len(noches)), minlength=len(noches)).astype(float)) for _ in range(n_boot)]
    v = np.array(v); v = v[np.isfinite(v)]
    return dif(np.ones(len(noches))), np.percentile(v, 5), np.percentile(v, 95)


def main():
    E = {n: pickle.load(open(os.path.join(SAL, n + "_eventos.pkl"), "rb")) for n in
         ("REF_2_0", "VWAP", "G25", "G50", "G100", "PIVOTES", "HOD_LOD", "ORACULO_NOCHE40")}
    for ven in ("congelada", "completa"):
        A = por_noche(E["REF_2_0"][ven])
        noches = sorted(set(A) | set(por_noche(E["VWAP"][ven])))
        print("\n== ventana %s: %d noches" % (ven, len(noches)))
        for nom in ("VWAP", "G25", "G50", "G100", "PIVOTES", "HOD_LOD", "ORACULO_NOCHE40"):
            B = por_noche(E[nom][ven])
            for excl in ((), ("2026-10-09",), ("2026-10-09", "2026-10-05")):
                nn = [n for n in noches if n not in excl]
                d, lo, hi = pareado(A, B, nn)
                print("   2.0 - %-16s sin %-22s dif %+5.1f  IC90 %+5.1f a %+5.1f" % (nom, "+".join(excl) or "-", d, lo, hi))
    # re-tests de la 2.0
    ev = sorted(E["REF_2_0"]["congelada"], key=lambda e: (e["pista"], e["t_llegada"]))
    prev = {}; cerca = 0; tras_rota = 0; tras_sost = 0
    for e in ev:
        p = prev.get(e["pista"])
        if p is not None and (e["t_llegada"] - p["t_llegada"]).total_seconds() - 60 * p["min_a_decision"] <= 15 * 60:
            cerca += 1
            tras_rota += p["resultado"] == "rota"; tras_sost += p["resultado"] == "sostenido"
        prev[e["pista"]] = e
    print("\nREF_2_0 congelada: %d llegadas; %d llegan <= 15 min despues de decidirse la anterior de la misma raya (%d tras rota, %d tras sostenido)"
          % (len(ev), cerca, tras_rota, tras_sost))
    pn = {}
    for e in ev:
        pn.setdefault((e["noche"], e["pista"]), []).append(e)
    tam = sorted((len(v) for v in pn.values()), reverse=True)
    print("raya-noche con llegadas: %d; llegadas por raya-noche: max %d, top5 %s; las 5 rayas-noche mas testeadas suman %d de %d"
          % (len(pn), tam[0], tam[:5], sum(tam[:5]), len(ev)))
    # una sola llegada por vela (dos rayas tocadas en la misma vela)
    porvela = {}
    for e in ev:
        porvela.setdefault(e["t_llegada"], []).append(e)
    print("velas con 2+ llegadas simultaneas: %d" % sum(len(v) > 1 for v in porvela.values()))


if __name__ == "__main__":
    main()
