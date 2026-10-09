# -*- coding: utf-8 -*-
"""h03_pareado.py — comparacion PAREADA por noche entre conversiones con la MISMA seleccion de la 2.0 (libro QQQ, dom0+dom1):
  h41  = conversion 4.1 (C8)            h41c = C8 con escalon de sesion (-0,0077 congelada)
  20dib = lo que dibujo la 2.0 (niv20; en la parte congelada es la misma seleccion con la razon de la 2.0: paridad 8633/8633 min)
Bootstrap remuestreando NOCHES (las mismas para los dos conjuntos): IC90 de la diferencia A - B. Juez sin cambios, los tres modos.
SOLO LECTURA. Imprime; escribe pareado_h03.json. Correr: python -I h03_pareado.py"""
import json
import os
import sys

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(AQUI), "juez"))
import juez_operador as J  # noqa: E402

CAL = os.path.join(os.environ["LOCALAPPDATA"], "Temp", "claude", "C--Users-wmx-7-OneDrive-Escritorio-ATAS-nada",
                   "f5879819-e2a4-405b-87b0-959ace090d7b", "scratchpad", "calibrar")
CLAVES = ("dom0", "dom1")


def metricas_noche(evs, giros, noches):
    """por noche: contadores para poder sumar en el bootstrap."""
    out = {}
    for nn in noches:
        E = [e for e in evs if e["noche"] == nn]
        S = [e for e in E if e["resultado"] == "sostenido"]
        base = sum(not e["censurada"] for e in E)
        cop = [e for e in S if e.get("opuesta") is not None]
        G = [g for g in giros if g["noche"] == nn]
        out[nn] = {"base": base, "sost": len(S), "exactos": sum(e["precision_abs"] <= 2 for e in S),
                   "prec": [e["precision_abs"] for e in S], "rec": [e["recorrido"] for e in S],
                   "cop": len(cop), "lleg": sum(bool(e["llega_opuesta"]) for e in cop), "giros": len(G), "cub": sum(g["cubierto"] for g in G)}
    return out


def agregar(M, noches_w):
    base = sum(M[n]["base"] * w for n, w in noches_w); sost = sum(M[n]["sost"] * w for n, w in noches_w)
    exa = sum(M[n]["exactos"] * w for n, w in noches_w)
    prec = [x for n, w in noches_w for _ in range(w) for x in M[n]["prec"]]
    rec = [x for n, w in noches_w for _ in range(w) for x in M[n]["rec"]]
    cop = sum(M[n]["cop"] * w for n, w in noches_w); ll = sum(M[n]["lleg"] * w for n, w in noches_w)
    gi = sum(M[n]["giros"] * w for n, w in noches_w); cu = sum(M[n]["cub"] * w for n, w in noches_w)
    f = lambda a, b: 100.0 * a / b if b else np.nan
    return {"pct_sostenidos": f(sost, base), "pct_exactos": f(exa, base), "precision_mediana_abs": float(np.median(prec)) if prec else np.nan,
            "recorrido_medio": float(np.mean(rec)) if rec else np.nan, "pct_llega_opuesta": f(ll, cop), "cobertura_pct": f(cu, gi)}


def main():
    V = pd.read_csv(os.path.join(CAL, "velas_m1.csv"), parse_dates=["t"])
    RH = pd.read_pickle(os.path.join(AQUI, "rayas_hibrido.pkl"))
    meta = RH["meta"]
    niv = pd.read_pickle(os.path.join(CAL, "niv20_m1.pkl"))
    conj = {"h41": RH["h41"], "h41c": RH["h41c"], "20dib": J.rayas_desde_niv20(niv, CLAVES)}
    pares = (("h41", "20dib"), ("h41c", "20dib"), ("h41c", "h41"))
    rng = np.random.default_rng(20261009)
    salida = {}
    for tipo in ("congelada", "completa"):
        vent = [(m["a_cong"], m["b_cong"]) if tipo == "congelada" else (m["a_full"], m["b_full"]) for m in meta]
        op = {"ventanas": vent, "claves": CLAVES}
        M = {}
        for nom, R in conj.items():
            prep = J.preparar(V, R, op)
            nv = J._noches_ventana(prep[0])
            giros = J.cobertura_giros(V, R, op, _prep=prep)["giros"]
            for modo in ("estricto", "tolerante", "muy_tolerante"):
                evs = J.juzgar(V, R, dict(op, modo=modo), _prep=prep)["eventos"]
                M[(nom, modo)] = metricas_noche(evs, giros, nv)
        noches = nv
        N = len(noches)
        idx = [rng.integers(0, N, N) for _ in range(2000)]
        for modo in ("estricto", "tolerante", "muy_tolerante"):
            for a, b in pares:
                ra = agregar(M[(a, modo)], [(n, 1) for n in noches]); rb = agregar(M[(b, modo)], [(n, 1) for n in noches])
                difs = {k: [] for k in ra}
                gana = {k: 0 for k in ra}
                for ix in idx:
                    w = np.bincount(ix, minlength=N)
                    nw = [(noches[k], int(w[k])) for k in range(N) if w[k]]
                    xa = agregar(M[(a, modo)], nw); xb = agregar(M[(b, modo)], nw)
                    for k in ra:
                        difs[k].append(xa[k] - xb[k])
                # noches donde A le gana a B en % sostenidos (solo noches con base en los dos)
                g_s = sum(1 for n in noches if M[(a, modo)][n]["base"] and M[(b, modo)][n]["base"] and
                          M[(a, modo)][n]["sost"] / M[(a, modo)][n]["base"] > M[(b, modo)][n]["sost"] / M[(b, modo)][n]["base"])
                e_s = sum(1 for n in noches if M[(a, modo)][n]["base"] and M[(b, modo)][n]["base"] and
                          M[(a, modo)][n]["sost"] / M[(a, modo)][n]["base"] == M[(b, modo)][n]["sost"] / M[(b, modo)][n]["base"])
                n_s = sum(1 for n in noches if M[(a, modo)][n]["base"] and M[(b, modo)][n]["base"])
                key = "%s|%s|%s-%s" % (tipo, modo, a, b)
                salida[key] = {}
                print("\n%s %s: %s - %s  (noches %d; %s gana en %% sostenidos %d, empata %d, de %d con llegadas en los dos)" % (
                    tipo.upper(), modo.upper(), a, b, N, a, g_s, e_s, n_s))
                for k in ra:
                    d = np.array(difs[k], float); d = d[np.isfinite(d)]
                    lo, hi = (np.percentile(d, 5), np.percentile(d, 95)) if len(d) else (np.nan, np.nan)
                    print("   %-22s %s %7.2f | %s %7.2f | dif %+7.2f  IC90 [%+.2f, %+.2f]%s" % (
                        k, a, ra[k], b, rb[k], ra[k] - rb[k], lo, hi, "  <- no cruza 0" if (lo > 0 or hi < 0) else ""))
                    salida[key][k] = {"a": ra[k], "b": rb[k], "dif": ra[k] - rb[k], "ic90": [lo, hi]}
    json.dump(salida, open(os.path.join(AQUI, "pareado_h03.json"), "w"), indent=1, default=float)


if __name__ == "__main__":
    main()
