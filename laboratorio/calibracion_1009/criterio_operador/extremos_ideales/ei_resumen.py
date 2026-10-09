# -*- coding: utf-8 -*-
"""ei_resumen.py - junta resultados/*.json y *_eventos.pkl de ei_medir.py y arma las tablas (totales, placebo, sensibilidad,
por noche, esta noche), la comparacion PAREADA por noche de la 2.0 contra cada conjunto de precio solo (bootstrap de noches) y la
CONFLUENCIA (llegadas a una dominante de la 2.0 que coincide a +-3 pts con un nivel de precio solo, contra las que no).
SOLO LECTURA de datos; imprime y escribe resultados/resumen.json. Correr: python -I ei_resumen.py"""
import json
import os
import pickle
import sys

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
sys.path.insert(0, os.path.join(os.path.dirname(AQUI), "juez"))
import ei_conjuntos as C  # noqa: E402

CAL = os.path.join(os.environ["LOCALAPPDATA"], "Temp", "claude", "C--Users-wmx-7-OneDrive-Escritorio-ATAS-nada",
                   "f5879819-e2a4-405b-87b0-959ace090d7b", "scratchpad", "calibrar")
SAL = os.path.join(AQUI, "resultados")
ORDEN = ("ORACULO_TRAMO", "ORACULO_NOCHE", "ORACULO_NOCHE40", "REF_2_0", "R25", "R50", "R100", "G25", "G50", "G100", "HOD_LOD",
         "PDH_PDL", "PIVOTES", "PIVOTES_TODOS", "VWAP")
PRECIO = ("R25", "R50", "R100", "HOD_LOD", "PDH_PDL", "PIVOTES", "VWAP")
PRECIO_TODOS = PRECIO + ("G25", "G50", "G100", "PIVOTES_TODOS")
MET = (("pct_sostenidos", "sost%", True), ("pct_rotas", "rotas%", False), ("pct_exactos", "exact%", True),
       ("precision_mediana_abs", "prec|med|", False), ("recorrido_medio", "rec.med", True), ("recorrido_mediano", "rec.p50", True),
       ("pct_llega_opuesta", "opuesta%", True), ("pct_falso_entre_sostenidos", "falso%sost", None), ("cobertura_pct", "cobert%", True))


def f(x, d=1):
    return "   -  " if x is None or (isinstance(x, float) and not np.isfinite(x)) else ("%.*f" % (d, x))


def cargar():
    R, E = {}, {}
    for n in ORDEN:
        p = os.path.join(SAL, n + ".json")
        if os.path.exists(p):
            R[n] = json.load(open(p, encoding="utf-8"))
            E[n] = pickle.load(open(os.path.join(SAL, n + "_eventos.pkl"), "rb"))
    return R, E


def dist_opuesta_mediana(evs):
    d = [e["dist_opuesta"] for e in evs if e["resultado"] == "sostenido" and e.get("dist_opuesta") is not None]
    return float(np.median(d)) if d else float("nan")


def tabla_total(R, E, ven, clave="ventanas"):
    print("\n" + "=" * 200)
    print("TOTALES %s - ventana %s, modo TOLERANTE (principal). Formato: real [mediana del azar | percentil del real en el azar] "
          "{corridas +-11/19/31 juntas}" % ("19 NOCHES" if clave == "ventanas" else "ESTA NOCHE (N=2026-10-09)", ven.upper()))
    print("=" * 200)
    out = {}
    for n in ORDEN:
        if n not in R:
            continue
        d = R[n][clave][ven]
        m = d["modos"]["tolerante"]
        r, az, cj, bi = m["real"], m["azar"], m["corridos_juntos"], m["bootstrap_ic90"]
        nn = len(d["noches"])
        evs = E[n][ven if clave == "ventanas" else ven + "_hoy"]
        print("\n%-14s pistas %d | llegadas %d (%.1f por noche; techos %d pisos %d) base %d censuradas %d | sostenidos %d, rotas %d, "
              "indefinidas %d, falsos rompimientos %d | noches con llegadas %d de %d | niveles distintos %d | dist. mediana a la "
              "opuesta %s pts" % (n, d["n_pistas"], r["llegadas"], r["llegadas"] / max(nn, 1), r["techos"], r["pisos"], r["base"],
                                    r["censuradas"], r["sostenidos"], r["rotas"], r["indefinidas"], r["falsos_rompimientos"],
                                    r["n_noches"], nn, r["n_niveles"], f(dist_opuesta_mediana(evs))))
        fila = []
        for k, nom, _ in MET:
            a = az.get(k)
            s = "%s=%s" % (nom, f(r.get(k)))
            if a:
                s += " [%s|p%s]" % (f(a.get("p50")), f(a.get("percentil"), 0))
            if k in cj:
                s += " {%s}" % f(cj.get(k))
            if k in bi and bi[k][0] is not None:
                s += " IC90 %s-%s" % (f(bi[k][0]), f(bi[k][1]))
            fila.append(s)
        for j in range(0, len(fila), 3):
            print("      " + " ; ".join(fila[j:j + 3]))
        ca = (az.get("cobertura_pct") or {}).get("p50"); cc = cj.get("cobertura_pct")
        print("      cobertura relativa (real / mediana del azar): %s x ; (real / corridas juntas): %s x" % (
            f(r["cobertura_pct"] / ca if ca and r.get("cobertura_pct") is not None else float("nan"), 2),
            f(r["cobertura_pct"] / cc if cc and r.get("cobertura_pct") is not None else float("nan"), 2)))
        out[n] = {"real": r, "azar_p50": {k: (az.get(k) or {}).get("p50") for k, _, _ in MET},
                  "percentil": {k: (az.get(k) or {}).get("percentil") for k, _, _ in MET},
                  "corridas": {k: cj.get(k) for k, _, _ in MET}, "ic90": bi, "dist_opuesta_mediana": dist_opuesta_mediana(evs)}
    return out


def tabla_corridas(R, ven):
    print("\nCORRIDAS una por una - ventana %s, TOLERANTE (para las grillas G* son otra fase de la misma grilla = placebo limpio): "
          "llegadas / sost%% / exact%% / cobert%%" % ven)
    for n in ("REF_2_0", "R25", "G25", "R50", "G50", "R100", "G100", "PIVOTES", "PIVOTES_TODOS", "VWAP"):
        if n not in R:
            continue
        m = R[n]["ventanas"][ven]["modos"]["tolerante"]
        cel = ["real %d/%s/%s/%s" % (m["real"]["llegadas"], f(m["real"]["pct_sostenidos"]), f(m["real"]["pct_exactos"]),
                                     f(m["real"].get("cobertura_pct")))]
        for d, x in m["corridos"].items():
            cel.append("%s: %d/%s/%s/%s" % (d, x["llegadas"], f(x["pct_sostenidos"]), f(x["pct_exactos"]), f(x.get("cobertura_pct"))))
        print("  %-14s " % n + " | ".join(cel))


def tabla_sensibilidad(R, ven, clave="ventanas"):
    print("\nSENSIBILIDAD %s - ventana %s: ESTRICTO / TOLERANTE / MUY TOLERANTE (real [percentil en su azar])" % (
        "19 noches" if clave == "ventanas" else "esta noche", ven))
    print("  %-14s %-34s %-34s %-34s %-30s %-30s" % ("conjunto", "llegadas", "sost%", "exact%", "prec|med|", "rec.med"))
    for n in ORDEN:
        if n not in R:
            continue
        d = R[n][clave][ven]["modos"]
        cel = []
        for k in ("llegadas", "pct_sostenidos", "pct_exactos", "precision_mediana_abs", "recorrido_medio"):
            cel.append(" / ".join("%s[%s]" % (f(d[m]["real"][k], 0 if k == "llegadas" else 1),
                                              f(d[m]["azar"].get(k, {}).get("percentil"), 0)) for m in ("estricto", "tolerante", "muy_tolerante")))
        print("  %-14s %-34s %-34s %-34s %-30s %-30s" % (n, *cel))


def tabla_por_noche(R, ven):
    print("\nPOR NOCHE - ventana %s, TOLERANTE. Celda = llegadas/sost%%/exact%%/rec.medio/cobert%%" % ven)
    noches = R["REF_2_0"]["ventanas"][ven]["noches"] if "REF_2_0" in R else next(iter(R.values()))["ventanas"][ven]["noches"]
    pres = [n for n in ORDEN if n in R]
    print("  %-10s " % "noche" + " ".join("%-22s" % n[:22] for n in pres))
    out = {}
    for nn in noches:
        celdas = []
        for n in pres:
            m = R[n]["ventanas"][ven]["modos"]["tolerante"]["por_noche"].get(nn, {})
            celdas.append("%d/%s/%s/%s/%s" % (m.get("llegadas", 0), f(m.get("pct_sostenidos"), 0), f(m.get("pct_exactos"), 0),
                                                f(m.get("recorrido_medio"), 0), f(m.get("cobertura_pct"), 0)))
            out.setdefault(nn, {})[n] = m
        print("  %-10s " % nn + " ".join("%-22s" % c for c in celdas))
    return out


def _por_noche_arrays(evs, giros, noches):
    pos = {n: i for i, n in enumerate(noches)}
    N = len(noches)
    base = np.zeros(N); sost = np.zeros(N); exa = np.zeros(N); recs = [[] for _ in range(N)]; pabs = [[] for _ in range(N)]
    for e in evs:
        if e["noche"] not in pos:
            continue
        i = pos[e["noche"]]
        if not e["censurada"]:
            base[i] += 1
        if e["resultado"] == "sostenido":
            sost[i] += 1; recs[i].append(e["recorrido"]); pabs[i].append(e["precision_abs"])
            if e["precision_abs"] <= 2.0:
                exa[i] += 1
    gn = np.zeros(N); gc = np.zeros(N)
    for g in giros:
        if g["noche"] in pos:
            gn[pos[g["noche"]]] += 1; gc[pos[g["noche"]]] += g["cubierto"]
    return base, sost, exa, recs, pabs, gn, gc


def _met(w, A):
    base, sost, exa, recs, pabs, gn, gc = A
    b = (w * base).sum(); s = (w * sost).sum()
    rr = [x for i in np.flatnonzero(w) for _ in range(int(w[i])) for x in recs[i]]
    pp = [x for i in np.flatnonzero(w) for _ in range(int(w[i])) for x in pabs[i]]
    g = (w * gn).sum()
    return {"pct_sostenidos": 100 * s / b if b else np.nan, "pct_exactos": 100 * (w * exa).sum() / b if b else np.nan,
            "recorrido_medio": float(np.mean(rr)) if rr else np.nan, "precision_mediana_abs": float(np.median(pp)) if pp else np.nan,
            "cobertura_pct": 100 * (w * gc).sum() / g if g else np.nan}


def pareado(R, E, ven, n_boot=2000, semilla=20261009):
    if "REF_2_0" not in R:
        return {}
    noches = R["REF_2_0"]["ventanas"][ven]["noches"]
    A0 = _por_noche_arrays(E["REF_2_0"][ven], R["REF_2_0"]["ventanas"][ven]["modos"]["tolerante"]["giros"], noches)
    print("\nPAREADO POR NOCHE (bootstrap de las mismas noches, %d) - ventana %s, TOLERANTE: diferencia 2.0 MENOS conjunto, IC90" % (n_boot, ven))
    out = {}
    rng = np.random.default_rng(semilla)
    W = [np.bincount(rng.integers(0, len(noches), len(noches)), minlength=len(noches)).astype(float) for _ in range(n_boot)]
    uno = np.ones(len(noches))
    for n in ("ORACULO_TRAMO", "ORACULO_NOCHE", "ORACULO_NOCHE40") + PRECIO_TODOS:
        if n not in R:
            continue
        A1 = _por_noche_arrays(E[n][ven], R[n]["ventanas"][ven]["modos"]["tolerante"]["giros"], noches)
        m0, m1 = _met(uno, A0), _met(uno, A1)
        dif = {k: [] for k in m0}
        for w in W:
            a, b = _met(w, A0), _met(w, A1)
            for k in dif:
                dif[k].append(a[k] - b[k])
        fila = {}
        txt = []
        for k in dif:
            v = np.array(dif[k], float); v = v[np.isfinite(v)]
            lo, hi = (np.percentile(v, 5), np.percentile(v, 95)) if len(v) else (np.nan, np.nan)
            fila[k] = {"dif": m0[k] - m1[k], "ic90": (float(lo), float(hi))}
            txt.append("%s %s (%s a %s)" % (k.replace("pct_", "").replace("_medio", "").replace("_mediana_abs", "|med|"),
                                            f(m0[k] - m1[k]), f(lo), f(hi)))
        print("  2.0 - %-14s " % n + " ; ".join(txt))
        out[n] = fila
    return out


def confluencia(R, E, ven, tol=3.0):
    """Llegadas a la 2.0: con un nivel de precio solo a +-tol (vigente en esa vela) contra sin ninguno."""
    if "REF_2_0" not in E:
        return {}
    V = C.cargar_velas(os.path.join(CAL, "velas_m1.csv"))
    rayas = {n: C.armar(V, n) for n in PRECIO}
    evs = [e for e in E["REF_2_0"][ven] if not e["censurada"]]
    print("\nCONFLUENCIA - ventana %s, TOLERANTE: llegadas a una dominante de la 2.0 con un nivel de precio solo a +-%.0f pts "
          "(vigente en esa vela = clave t-1) contra sin ninguno" % (ven, tol))
    out = {}

    def resumen(sub):
        s = [e for e in sub if e["resultado"] == "sostenido"]
        return {"n": len(sub), "sost": len(s), "pct_sost": 100 * len(s) / len(sub) if sub else np.nan,
                "pct_exactos": 100 * sum(e["precision_abs"] <= 2 for e in s) / len(sub) if sub else np.nan,
                "rec_medio": float(np.mean([e["recorrido"] for e in s])) if s else np.nan}

    def cerca(n, e):
        t = pd.Timestamp(e["t_llegada"]) - pd.Timedelta(minutes=1)
        return any(abs(x - e["raya"]) <= tol for x, _ in rayas[n].get(t, []))

    flags = {n: [cerca(n, e) for e in evs] for n in PRECIO}
    for n in PRECIO:
        con = [e for e, fl in zip(evs, flags[n]) if fl]; sin = [e for e, fl in zip(evs, flags[n]) if not fl]
        a, b = resumen(con), resumen(sin)
        out[n] = {"con": a, "sin": b}
        print("  %-8s con: n %4d sost %s %% exact %s %% rec %s | sin: n %4d sost %s %% exact %s %% rec %s" % (
            n, a["n"], f(a["pct_sost"]), f(a["pct_exactos"]), f(a["rec_medio"]), b["n"], f(b["pct_sost"]), f(b["pct_exactos"]),
            f(b["rec_medio"])))
    alguno = [any(flags[n][j] for n in PRECIO) for j in range(len(evs))]
    a = resumen([e for e, fl in zip(evs, alguno) if fl]); b = resumen([e for e, fl in zip(evs, alguno) if not fl])
    out["ALGUNO"] = {"con": a, "sin": b}
    print("  %-8s con: n %4d sost %s %% exact %s %% rec %s | sin: n %4d sost %s %% exact %s %% rec %s" % (
        "ALGUNO", a["n"], f(a["pct_sost"]), f(a["pct_exactos"]), f(a["rec_medio"]), b["n"], f(b["pct_sost"]), f(b["pct_exactos"]),
        f(b["rec_medio"])))
    return out


def esta_noche_eventos(E, n, ven):
    evs = E[n][ven + "_hoy"]
    print("\n  %s - eventos de esta noche (%s, TOLERANTE):" % (n, ven))
    for e in evs:
        print("     %s %-5s %.2f %-26s -> %-10s %-40s prec %+6.2f %s rec %s opuesta %s llega %s" % (
            pd.Timestamp(e["t_llegada"]).strftime("%H:%M"), e["tipo"], e["raya"], ",".join(e["etiquetas"] or [])[:26], e["resultado"],
            e["motivo"], e["precision"], "FALSO-ROMP" if e["falso_rompimiento"] else "          ",
            f(e.get("recorrido")), f(e.get("opuesta"), 2), e.get("llega_opuesta")))


def main():
    R, E = cargar()
    print("conjuntos con resultados: %s" % ", ".join(R))
    res = {"totales": {}, "esta_noche": {}, "por_noche": {}, "pareado": {}, "confluencia": {}}
    for ven in ("congelada", "completa"):
        res["totales"][ven] = tabla_total(R, E, ven)
        tabla_corridas(R, ven)
        tabla_sensibilidad(R, ven)
        res["por_noche"][ven] = tabla_por_noche(R, ven)
        res["pareado"][ven] = pareado(R, E, ven)
        res["confluencia"][ven] = confluencia(R, E, ven)
    for ven in ("congelada", "completa"):
        res["esta_noche"][ven] = tabla_total(R, E, ven, clave="esta_noche")
        tabla_sensibilidad(R, ven, clave="esta_noche")
        for n in ("REF_2_0", "R25", "G25", "PIVOTES", "VWAP", "HOD_LOD"):
            if n in E and ven == "congelada":
                esta_noche_eventos(E, n, ven)

    def limpio(x):
        if isinstance(x, dict):
            return {str(k): limpio(v) for k, v in x.items()}
        if isinstance(x, (list, tuple)):
            return [limpio(v) for v in x]
        if isinstance(x, (np.floating, float)):
            return None if not np.isfinite(x) else round(float(x), 4)
        if isinstance(x, np.integer):
            return int(x)
        return x
    with open(os.path.join(SAL, "resumen.json"), "w", encoding="utf-8") as fh:
        json.dump(limpio(res), fh, ensure_ascii=False, indent=1)


if __name__ == "__main__":
    main()
