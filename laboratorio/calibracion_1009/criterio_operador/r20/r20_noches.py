# -*- coding: utf-8 -*-
"""r20_noches.py — la 2.0 TAL COMO DIBUJO (niv20_m1.pkl: dom0/dom1 = sus dos dominantes de QQQ convertidas con su razon; zero_vol aparte)
juzgada con el criterio del operador (juez/juez_operador.py, sin cambios) en TODAS las sesiones de velas_m1.csv (09-14 .. 10-09), en la
ventana CONGELADA (00:35Z-08:30Z; lunes desde domingo 22:00Z) y en la noche COMPLETA (22:00Z-13:30Z) por separado.
Por conjunto x ventana: los tres modos (estricto / tolerante / muy tolerante), placebo oficial del juez en los tres modos (corridas
+-11/19/31 y n_azar juegos al azar 'pool'), en TOLERANTE ademas 'propia', IC90 bootstrap por noche, numeros POR NOCHE (real, corridas y
azar por noche con los mismos sorteos que el placebo oficial: misma semilla), cobertura de giros (+-2 pre-registrada; +-4 y +-6 como
sensibilidad) y diferencias pareadas por noche real - corridas y real - azar (bootstrap de noches).
SOLO LECTURA de los datos. Escribe r20_noches.json y r20_noches.txt en esta carpeta. Correr: python -I r20_noches.py [n_azar]"""
import json
import os
import sys
import time

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r20_comun as C  # noqa: E402

J = C.J
SEM = 20261009
MODOS = ("tolerante", "estricto", "muy_tolerante")
CLAVE_MET = ("llegadas", "base", "sostenidos", "pct_sostenidos", "rotas", "pct_rotas", "indefinidas", "falsos_rompimientos",
             "pct_falso_entre_sostenidos", "exactos", "pct_exactos", "precision_mediana_abs", "precision_mediana", "recorrido_medio",
             "recorrido_mediano", "recorrido_tramo_medio", "con_opuesta", "llega_opuesta", "pct_llega_opuesta", "n_noches",
             "n_noches_ventana", "n_niveles", "censuradas")

LINEAS = []


def p(*a):
    s = " ".join(str(x) for x in a)
    print(s)
    LINEAS.append(s)


def cont_noche(evs):
    """contadores sumables por noche (para bootstrap pareado)."""
    base = sum(not e["censurada"] for e in evs)
    sost = [e for e in evs if e["resultado"] == "sostenido"]
    cop = [e for e in sost if e["opuesta"] is not None]
    return np.array([base, len(sost), sum(e["recorrido"] for e in sost), len(cop), sum(bool(e["llega_opuesta"]) for e in cop),
                     sum(e["precision_abs"] <= 2 for e in sost)], float)


def ratios(c):
    """c = contadores sumados -> (pct_sost, recorrido_medio, pct_llega_opuesta, pct_exactos)"""
    b, s, rs, co, lo, ex = c
    return (100 * s / b if b else np.nan, rs / s if s else np.nan, 100 * lo / co if co else np.nan, 100 * ex / b if b else np.nan)


def medir(V, niv, Ns, nombre, claves, tipo, n_azar):
    R = C.rayas(niv, claves)
    op = {"ventanas": C.ventanas(Ns, tipo)}
    t0 = time.time()
    prep = J.preparar(V, R, op)
    Vp, Rp = prep
    nv = J._noches_ventana(Vp)
    P = len(Rp["pistas"])
    out = {"conjunto": nombre, "claves": claves, "ventana": tipo, "n_pistas": P, "noches_ventana": nv,
           "ventanas": [(str(a), str(b)) for a, b in op["ventanas"]]}
    # ---- sensibilidad (los tres modos) ----
    out["modos"] = {m: J.juzgar(V, R, dict(op, modo=m), _prep=prep)["metricas"] for m in MODOS}
    # ---- placebo oficial en los tres modos ----
    out["placebo"] = {}
    for m in MODOS:
        pl = J.placebo(V, R, dict(op, modo=m), n_azar=n_azar, semilla=SEM, _prep=prep)
        ev = pl.pop("eventos_real")
        if m == "tolerante":
            ev_tol = ev
        out["placebo"][m] = pl
    pl = J.placebo(V, R, dict(op, modo="tolerante"), n_azar=n_azar, semilla=SEM, azar_distancia="propia", n_boot=0, _prep=prep)
    pl.pop("eventos_real")
    out["placebo_propia_tolerante"] = pl
    # ---- cobertura: sensibilidad de la tolerancia ----
    out["cobertura"] = {}
    giros_tol2 = None
    for tc in (2.0, 4.0, 6.0):
        cg = J.cobertura_giros(V, R, dict(op, tol_cobertura=tc), _prep=prep)
        out["cobertura"]["%g" % tc] = {"n_giros": cg["n_giros"], "cubiertos": cg["cubiertos"], "pct": cg["pct"]}
        if tc == 2.0:
            giros_tol2 = cg["giros"]
    # ---- por noche: real, corridas y azar con los mismos sorteos que el placebo oficial ----
    opx = J._op(dict(op, modo="tolerante"))
    piv = J._pivotes(Vp, opx)
    piv_noche = [Vp["noche"][q["k"]] for q in piv]
    real_n = C.por_noche(ev_tol)
    cub_r, _ = J._cobertura_core(Vp, Rp, piv, opx, np.zeros(P))
    det_r = J._cobertura_core(Vp, Rp, piv, opx, np.zeros(P))[1]
    cub_real_n = {n: [0, 0] for n in nv}
    for nn, (ok, _) in zip(piv_noche, det_r):
        cub_real_n[nn][0] += 1; cub_real_n[nn][1] += ok
    cor_n = {n: [] for n in nv}
    cor_cont = {n: np.zeros(6) for n in nv}
    cor_cub = {n: 0.0 for n in nv}
    corr = (-31, -19, -11, 11, 19, 31)
    for d in corr:
        e = J._juzgar_core(Vp, Rp, opx, np.full(P, float(d)))
        for nn, evs in C.por_noche(e).items():
            if nn in cor_n:
                cor_n[nn].extend(evs); cor_cont[nn] += cont_noche(evs)
        _, det = J._cobertura_core(Vp, Rp, piv, opx, np.full(P, float(d)))
        for nn, (ok, _) in zip(piv_noche, det):
            cor_cub[nn] += ok / len(corr)
    rng = np.random.default_rng(SEM)
    az_met = {n: {k: [] for k in ("pct_sostenidos", "precision_mediana_abs", "recorrido_medio", "pct_llega_opuesta", "cob")} for n in nv}
    az_cont = {n: np.zeros(6) for n in nv}
    az_cub = {n: 0.0 for n in nv}
    tot_chk = []
    for _ in range(n_azar):
        off = J._offsets_azar(Vp, Rp, rng, "pool")
        e = J._juzgar_core(Vp, Rp, opx, off)
        tot_chk.append(J.resumir(e, nv, opx["tol"])["pct_sostenidos"])
        en = C.por_noche(e)
        _, det = J._cobertura_core(Vp, Rp, piv, opx, off)
        cn = {n: [0, 0] for n in nv}
        for nn, (ok, _) in zip(piv_noche, det):
            cn[nn][0] += 1; cn[nn][1] += ok
        for nn in nv:
            evs = en.get(nn, [])
            rr = J.resumir(evs, None, opx["tol"])
            for k in ("pct_sostenidos", "precision_mediana_abs", "recorrido_medio", "pct_llega_opuesta"):
                az_met[nn][k].append(rr[k])
            az_met[nn]["cob"].append(100 * cn[nn][1] / cn[nn][0] if cn[nn][0] else np.nan)
            az_cont[nn] += cont_noche(evs) / n_azar
            az_cub[nn] += cn[nn][1] / n_azar
    # chequeo: mi bucle reproduce el placebo oficial (misma semilla)
    a = np.array(tot_chk, float); a = a[np.isfinite(a)]
    out["chequeo_bucle_vs_oficial"] = {"p50_mio": float(np.percentile(a, 50)) if len(a) else None,
                                       "p50_oficial": out["placebo"]["tolerante"]["azar"]["pct_sostenidos"]["p50"]}
    filas = []
    for nn in nv:
        evs = real_n.get(nn, [])
        rm = J.resumir(evs, None, opx["tol"])
        cm = J.resumir(cor_n[nn], None, opx["tol"])
        g, cb = cub_real_n[nn]
        fila = {"noche": nn, **{k: rm[k] for k in CLAVE_MET if k in rm}, "giros": g, "giros_cubiertos": cb,
                "cobertura_pct": 100 * cb / g if g else None,
                "corridas_pct_sostenidos": cm["pct_sostenidos"], "corridas_llegadas_por_corrimiento": cm["llegadas"] / len(corr),
                "corridas_recorrido_medio": cm["recorrido_medio"], "corridas_cobertura_pct": 100 * cor_cub[nn] / g if g else None}
        for k in ("pct_sostenidos", "precision_mediana_abs", "recorrido_medio", "pct_llega_opuesta", "cob"):
            x = np.array(az_met[nn][k], float); x = x[np.isfinite(x)]
            real_k = fila["cobertura_pct"] if k == "cob" else rm[k]
            fila["azar_%s_p50" % k] = float(np.percentile(x, 50)) if len(x) else None
            fila["azar_%s_percentil" % k] = (float(100 * ((x < real_k).sum() + 0.5 * (x == real_k).sum()) / len(x))
                                            if len(x) and real_k is not None and real_k == real_k else None)
            fila["azar_%s_n" % k] = int(len(x))
        filas.append(fila)
    out["por_noche"] = filas
    # ---- diferencias pareadas por noche (bootstrap de noches) ----
    Rc = np.array([cont_noche(real_n.get(nn, [])) for nn in nv])
    Cc = np.array([cor_cont[nn] / len(corr) for nn in nv])
    Ac = np.array([az_cont[nn] for nn in nv])
    G = np.array([cub_real_n[nn][0] for nn in nv], float)
    Cr = np.array([cub_real_n[nn][1] for nn in nv], float)
    Cco = np.array([cor_cub[nn] / len(corr) for nn in nv], float)
    Caz = np.array([az_cub[nn] for nn in nv], float)
    rngb = np.random.default_rng(SEM)
    difs = {"vs_corridas": [], "vs_azar": []}
    for _ in range(2000):
        w = np.bincount(rngb.integers(0, len(nv), len(nv)), minlength=len(nv)).astype(float)
        rr = np.array(ratios((Rc * w[:, None]).sum(0)) + (100 * (Cr * w).sum() / (G * w).sum() if (G * w).sum() else np.nan,))
        cc = np.array(ratios((Cc * w[:, None]).sum(0)) + (100 * (Cco * w).sum() / (G * w).sum() if (G * w).sum() else np.nan,))
        aa = np.array(ratios((Ac * w[:, None]).sum(0)) + (100 * (Caz * w).sum() / (G * w).sum() if (G * w).sum() else np.nan,))
        difs["vs_corridas"].append(rr - cc); difs["vs_azar"].append(rr - aa)
    nom_d = ("pct_sostenidos", "recorrido_medio", "pct_llega_opuesta", "pct_exactos", "cobertura_pct")
    out["dif_pareada_ic90"] = {}
    for k, L in difs.items():
        L = np.array(L, float)
        pt_r = np.array(ratios(Rc.sum(0)) + (100 * Cr.sum() / G.sum() if G.sum() else np.nan,))
        pt_o = np.array(ratios((Cc if k == "vs_corridas" else Ac).sum(0)) +
                        (100 * (Cco if k == "vs_corridas" else Caz).sum() / G.sum() if G.sum() else np.nan,))
        out["dif_pareada_ic90"][k] = {nm: {"dif": float(pt_r[j] - pt_o[j]), "ic90": (float(np.nanpercentile(L[:, j], 5)), float(np.nanpercentile(L[:, j], 95))),
                                           "p_dif_menor_igual_0": float(np.nanmean(L[:, j] <= 0))} for j, nm in enumerate(nom_d)}
    out["giros_tol2"] = giros_tol2
    out["eventos_tolerante"] = ev_tol
    out["segundos"] = time.time() - t0
    return out


def imprimir(o):
    p("\n" + "=" * 150)
    p("CONJUNTO %s (%s) | ventana %s | pistas %d | noches en la ventana %d | %.0f s" % (o["conjunto"], "+".join(o["claves"]), o["ventana"].upper(),
                                                                                       o["n_pistas"], len(o["noches_ventana"]), o["segundos"]))
    for m in MODOS:
        x = o["modos"][m]
        p("  %-13s llegadas %4d (techo %s piso %s, censuradas %d) base %4d | sostenidos %4d = %5.1f %% (falso romp. %d = %.0f %% de ellos) | rotas %5.1f %% | indef %d | "
          "exactos %d = %.1f %% | precision |mediana| %.2f (con signo %+.2f) | recorrido medio %.1f mediano %.1f tramo %.1f | llega opuesta %d/%d = %s %% | "
          "noches con llegadas %d, niveles %d" % (
              m, x["llegadas"], x["techos"], x["pisos"], x["censuradas"], x["base"], x["sostenidos"], x["pct_sostenidos"], x["falsos_rompimientos"],
              x["pct_falso_entre_sostenidos"] if x["pct_falso_entre_sostenidos"] == x["pct_falso_entre_sostenidos"] else 0, x["pct_rotas"],
              x["indefinidas"], x["exactos"], x["pct_exactos"], x["precision_mediana_abs"], x["precision_mediana"], x["recorrido_medio"],
              x["recorrido_mediano"], x["recorrido_tramo_medio"], x["llega_opuesta"], x["con_opuesta"], C.r(x["pct_llega_opuesta"]), x["n_noches"], x["n_niveles"]))
    for m in MODOS:
        pl = o["placebo"][m]
        p("  PLACEBO %s (azar 'pool', %d juegos; percentil = %% de juegos por debajo del real; precision y pct_rotas: bajo = mejor)" % (m.upper(), pl["azar"]["llegadas"]["n"]))
        for k, a in pl["azar"].items():
            p("     %-28s real %8.2f | azar p5 %8.2f p50 %8.2f p95 %8.2f | percentil %5.1f" % (k, a["real"], a["p5"], a["p50"], a["p95"], a["percentil"]))
        cj = pl["corridos_juntos"]
        p("     corridas +-11/19/31 juntas: llegadas %d sostenidos %.1f %% exactos %.1f %% precision |med| %.2f recorrido medio %.1f llega opuesta %s %% cobertura %.1f %%" % (
            cj["llegadas"], cj["pct_sostenidos"], cj["pct_exactos"], cj["precision_mediana_abs"], cj["recorrido_medio"], C.r(cj["pct_llega_opuesta"]), cj["cobertura_pct"]))
        p("     por corrimiento: " + " | ".join("%+d: %d lleg %.0f %% sost" % (int(d), x["llegadas"], x["pct_sostenidos"]) for d, x in pl["corridos"].items()))
        if pl["bootstrap_ic90"]:
            p("     IC90 bootstrap por noche del real: " + "; ".join("%s %.1f-%.1f" % (k, v[0], v[1]) for k, v in pl["bootstrap_ic90"].items()))
    pp = o["placebo_propia_tolerante"]["azar"]
    p("  PLACEBO TOLERANTE con azar 'propia' (cada pista conserva su propia distancia, signo al azar): " + "; ".join(
        "%s real %.1f p50 %.1f pct %.0f" % (k, a["real"], a["p50"], a["percentil"]) for k, a in pp.items() if k in ("pct_sostenidos", "pct_exactos", "precision_mediana_abs", "recorrido_medio", "pct_llega_opuesta", "cobertura_pct")))
    p("  COBERTURA de giros (zigzag 20): " + "; ".join("+-%s: %d/%d = %.1f %%" % (k, v["cubiertos"], v["n_giros"], v["pct"]) for k, v in o["cobertura"].items()))
    p("  chequeo: mi bucle por noche reproduce el placebo oficial: p50 pct_sostenidos mio %.2f oficial %.2f" % (o["chequeo_bucle_vs_oficial"]["p50_mio"], o["chequeo_bucle_vs_oficial"]["p50_oficial"]))
    p("  DIFERENCIA PAREADA POR NOCHE (TOLERANTE; bootstrap de noches, IC90):")
    for k, d in o["dif_pareada_ic90"].items():
        p("     real %s: " % k.replace("_", " ") + "; ".join("%s %+.1f (IC90 %+.1f a %+.1f, P(<=0) %.2f)" % (nm, v["dif"], v["ic90"][0], v["ic90"][1], v["p_dif_menor_igual_0"]) for nm, v in d.items()))
    p("  POR NOCHE (TOLERANTE): noche | lleg base sost% (azar p50, pctil | corridas) rotas falso exactos | prec|med| | rec medio (azar p50) | opuesta | giros cub (azar p50 | corr)")
    for f in o["por_noche"]:
        p("     %s | %3d %3d %5s%% (%5s, %5s | %5s) %3d %2d %2d | %5s | %6s (%6s) | %s/%s | %2d %2d = %5s%% (%5s | %5s)" % (
            f["noche"], f["llegadas"], f["base"], C.r(f["pct_sostenidos"]), C.r(f["azar_pct_sostenidos_p50"]), C.r(f["azar_pct_sostenidos_percentil"], 0),
            C.r(f["corridas_pct_sostenidos"]), f["rotas"], f["falsos_rompimientos"], f["exactos"], C.r(f["precision_mediana_abs"], 2),
            C.r(f["recorrido_medio"]), C.r(f["azar_recorrido_medio_p50"]), f["llega_opuesta"], f["con_opuesta"], f["giros"], f["giros_cubiertos"],
            C.r(f["cobertura_pct"]), C.r(f["azar_cob_p50"]), C.r(f["corridas_cobertura_pct"])))


def main():
    n_azar = int(sys.argv[1]) if len(sys.argv) > 1 else 500
    V, niv = C.cargar()
    Ns = C.sesiones(V)
    p("velas %d: %s -> %s | sesiones %d: %s" % (len(V), V["t"].min(), V["t"].max(), len(Ns), ", ".join(str(n.date()) for n in Ns)))
    p("AVISO: el dato mas nuevo es la vela %s UTC (velas_m1.csv); las cadenas de la 2.0 de noche son las del cierre del dia anterior." % V["t"].max())
    res = []
    for nombre, claves in C.CONJUNTOS.items():
        for tipo in ("congelada", "completa"):
            o = medir(V, niv, Ns, nombre, claves, tipo, n_azar)
            imprimir(o)
            res.append(o)
    json.dump(C.limpio(res), open(os.path.join(C.AQUI, "r20_noches.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=0)
    open(os.path.join(C.AQUI, "r20_noches.txt"), "w", encoding="utf-8").write("\n".join(LINEAS) + "\n")


if __name__ == "__main__":
    main()
