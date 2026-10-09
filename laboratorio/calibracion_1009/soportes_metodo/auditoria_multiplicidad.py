# -*- coding: utf-8 -*-
"""
auditoria_multiplicidad.py  (calibracion_1009 / soportes_metodo) -- 09-10-2026

Le aplica al veredicto ya publicado de `laboratorio/tres/extremos_dibujado.py` (resultados/extremos_dibujado.json, 08-10 05:02 UTC)
las correcciones que pide la literatura de data snooping (White 2000; Hansen 2005; Romano y Wolf 2005; Harvey, Liu y Zhu 2016) y el
efecto de los toques agrupados por sesion. NO recalcula toques: usa los conteos por sesion que el script ya guardo. Solo lee.

Para cada fila (raya) y ventana:
  z vs AZAR emparejado por sesion: E = sum_s n_s p_s ; Var = sum_s n_s p_s (1-p_s)  (p_s = % de rebote de 100 rayas al azar ESA sesion)
  p unilateral; Holm (FWER) y Benjamini-Hochberg (FDR) sobre la familia de filas con >= 30 toques en la ventana.
  Maximo-z bajo H0 por simulacion (filas tratadas como independientes -> conservador si estan correlacionadas positivamente).
  Sesiones: cuantas aportan, prueba del signo de "sesiones ganadas".
  Maldicion del ganador: contraccion empirica de Bayes de la ventaja (rebote - azar) con la dispersion entre filas
  (solo filas individuales, sin los agregados D1+D2).
  ICC entre sesiones estimado con las rayas al azar (tau^2 = var(p_s) - media(p(1-p)/n_s)) -> efecto de diseno.
Uso: python -I auditoria_multiplicidad.py  -> auditoria_multiplicidad.json / .md
"""
import os, json, math
import numpy as np
from statistics import NormalDist

AQUI = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.normpath(os.path.join(AQUI, "..", "..", "tres", "resultados", "extremos_dibujado.json"))
N = NormalDist()
RNG = np.random.default_rng(20261009)


def holm(p):
    p = np.asarray(p); m = len(p); o = np.argsort(p); adj = np.empty(m); run = 0.0
    for k, i in enumerate(o):
        run = max(run, min(1.0, (m - k) * p[i])); adj[i] = run
    return adj


def bh(p):
    p = np.asarray(p); m = len(p); o = np.argsort(p)[::-1]; adj = np.empty(m); run = 1.0
    for k, i in enumerate(o):
        rank = m - k
        run = min(run, p[i] * m / rank); adj[i] = run
    return adj


def binom_sign_p(k, n):
    """P(X >= k), X ~ Bin(n, 1/2)."""
    return sum(math.comb(n, j) for j in range(k, n + 1)) / 2 ** n if n else float("nan")


def main():
    d = json.load(open(SRC, encoding="utf-8"))
    base = d["base"]
    out = {"fuente": os.path.relpath(SRC, AQUI), "generado_fuente": d.get("generado_utc"), "parametros": d["parametros"], "ventanas": {}}
    for wn in ("noche", "dia"):
        filas = []
        for nom, r in base.items():
            v = r.get(wn)
            if not v or v.get("toques", 0) == 0:
                continue
            E = Var = 0.0; Ed = 0.0; nd_tot = 0
            ses = []
            for s, ps in v.get("por_sesion", {}).items():
                n_s = ps["toques"]; pa = ps["azar_pct"] / 100.0
                E += n_s * pa; Var += n_s * pa * (1 - pa)
                ses.append((s, n_s, ps["rebotes"], pa, ps.get("ganada")))
            R = v["rebotes"]; n = v["toques"]
            z = (R - E) / math.sqrt(Var) if Var > 0 else float("nan")
            ses3 = [x for x in ses if x[1] >= 3]
            gan = sum(1 for x in ses3 if x[2] / x[1] > x[3])
            filas.append(dict(fila=nom, toques=n, rebotes=R, rebote_pct=round(100 * R / n, 1), azar_esperado_pct=round(100 * E / n, 1),
                              ventaja_pp=round(100 * (R - E) / n, 1), se_pp=round(100 * math.sqrt(Var) / n, 2), z=round(z, 3),
                              p_unilateral=1 - N.cdf(z) if z == z else float("nan"), sesiones=len(ses), sesiones_3_toques=len(ses3),
                              sesiones_ganadas_vs_azar=gan, p_signo=binom_sign_p(gan, len(ses3)),
                              despl_pct=v.get("placebo_desplazado_pct"), despl_toques=v.get("placebo_desplazado_toques"),
                              agregado=("(D1+D2)" in nom or "(techo+piso)" in nom),
                              veredicto_original=d["veredicto"][wn].get(nom, {}).get("cumple")))
        fam = [f for f in filas if f["toques"] >= 30]
        ps = [f["p_unilateral"] for f in fam]
        for f, ph, pb in zip(fam, holm(ps), bh(ps)):
            f["p_holm"] = float(ph); f["q_bh"] = float(pb)
        # maximo z bajo H0 (simulado con los n_s y p_s de cada fila)
        sims = 20000
        zmax = np.full(sims, -np.inf)
        for f in fam:
            v = base[f["fila"]][wn]
            ns = np.array([ps["toques"] for ps in v["por_sesion"].values()]); pa = np.array([ps["azar_pct"] / 100 for ps in v["por_sesion"].values()])
            Rsim = RNG.binomial(ns[None, :], pa[None, :], size=(sims, len(ns))).sum(1)
            E = (ns * pa).sum(); sd = math.sqrt((ns * pa * (1 - pa)).sum())
            zmax = np.maximum(zmax, (Rsim - E) / sd)
        for f in fam:
            f["p_max_z"] = float((zmax >= f["z"]).mean())
        # contraccion empirica de Bayes (solo filas individuales)
        ind = [f for f in fam if not f["agregado"]]
        e = np.array([f["ventaja_pp"] for f in ind]); se = np.array([f["se_pp"] for f in ind])
        tau2 = max(0.0, float(e.var(ddof=1) - (se ** 2).mean())) if len(e) > 2 else 0.0
        mu = float(e.mean()) if len(e) else 0.0
        for f in fam:
            k = tau2 / (tau2 + f["se_pp"] ** 2) if tau2 > 0 else 0.0
            f["ventaja_contraida_pp"] = round(mu + k * (f["ventaja_pp"] - mu), 2)
        # ICC con las rayas al azar
        az = d["azar"][wn]
        p_s = np.array([x["rebote_pct"] / 100 for x in az.values() if x["toques"] > 0]); n_s = np.array([x["toques"] for x in az.values() if x["toques"] > 0])
        pbar = float((p_s * n_s).sum() / n_s.sum())
        tau2_az = max(0.0, float(p_s.var(ddof=1) - (pbar * (1 - pbar) / n_s).mean()))
        icc = tau2_az / (pbar * (1 - pbar))
        fam.sort(key=lambda f: f["p_unilateral"])
        out["ventanas"][wn] = dict(filas_familia=len(fam), filas_total=len(filas), azar_pbar_pct=round(100 * pbar, 2),
                                   azar_sd_entre_sesiones_pp=round(100 * float(p_s.std(ddof=1)), 2), azar_sesiones=len(p_s),
                                   icc_estimado=round(icc, 4), ventaja_media_pp=round(mu, 2), tau_ventaja_pp=round(math.sqrt(tau2), 2),
                                   zmax_h0_p95=round(float(np.percentile(zmax, 95)), 3), filas=fam)
    # ICC por NIVEL ("la muestra son niveles"): toques reales agrupados por (sesion, bucket de 2,5 pts del nivel). Estimador ANOVA.
    import pandas as pd
    tq = pd.read_csv(os.path.join(os.path.dirname(SRC), "extremos_dibujado_toques.csv"))
    tq = tq[tq["resultado"] != "indef"]
    tq["y"] = (tq["resultado"] == "rebote").astype(float)
    tq["grupo"] = tq["sesion"].astype(str) + "|" + (tq["nivel"] / 2.5).round().astype(int).astype(str)
    icc_niv = {}
    for (fila, wn), g in tq.groupby(["fila", "ventana"]):
        if len(g) < 100:
            continue
        gs = g.groupby("grupo")["y"]
        n_i = gs.size().values.astype(float); m_i = gs.mean().values; N_ = n_i.sum(); a = len(n_i)
        if a < 3:
            continue
        ybar = g["y"].mean()
        msb = (n_i * (m_i - ybar) ** 2).sum() / (a - 1)
        msw = ((g["y"] - g["grupo"].map(gs.mean())) ** 2).sum() / (N_ - a)
        k0 = (N_ - (n_i ** 2).sum() / N_) / (a - 1)
        icc = (msb - msw) / (msb + (k0 - 1) * msw) if (msb + (k0 - 1) * msw) > 0 else float("nan")
        icc_niv["%s | %s" % (fila, wn)] = dict(toques=int(N_), niveles_distintos=a, toques_por_nivel=round(N_ / a, 2), icc=round(float(icc), 4),
                                              efecto_diseno=round(1 + (N_ / a - 1) * max(icc, 0), 3), n_efectivo=round(N_ / (1 + (N_ / a - 1) * max(icc, 0)), 1))
    out["icc_por_nivel"] = icc_niv
    with open(os.path.join(AQUI, "auditoria_multiplicidad.json"), "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=1)
    L = ["# Auditoria de multiplicidad del veredicto de extremos_dibujado (08-10)", "",
         "Fuente: `%s` (generado %s). Juez: TOL %s / REB %s / CRUZ %s / MIRAR %s velas m2." % (out["fuente"], out["generado_fuente"], d["parametros"]["TOL"],
                                                                                      d["parametros"]["REB"], d["parametros"]["CRUZ"], d["parametros"]["MIRAR"]), ""]
    for wn, W in out["ventanas"].items():
        L.append("## %s: %d filas con >= 30 toques (de %d)" % (wn.upper(), W["filas_familia"], W["filas_total"]))
        L.append("")
        L.append("Azar: %.2f %% de rebote medio, desvio entre %d sesiones %.2f pp, ICC estimado %.4f. Ventaja media de las filas individuales %.2f pp, "
                 "dispersion verdadera estimada (tau) %.2f pp. Percentil 95 del maximo z bajo H0: %.2f." % (
                     W["azar_pbar_pct"], W["azar_sesiones"], W["azar_sd_entre_sesiones_pp"], W["icc_estimado"], W["ventaja_media_pp"], W["tau_ventaja_pp"], W["zmax_h0_p95"]))
        L.append("")
        L.append("| fila | toques | rebote % | azar emparejado % | ventaja pp | z | p | p Holm | q BH | p max-z | ventaja contraida pp | sesiones (>=3 toq) | ganadas | p signo | GANA original |")
        L.append("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|")
        for f in W["filas"][:15]:
            L.append("| %s | %d | %.1f | %.1f | %+.1f | %.2f | %.4f | %.3f | %.3f | %.3f | %+.2f | %d (%d) | %d | %.3f | %s |" % (
                f["fila"], f["toques"], f["rebote_pct"], f["azar_esperado_pct"], f["ventaja_pp"], f["z"], f["p_unilateral"], f["p_holm"], f["q_bh"],
                f["p_max_z"], f["ventaja_contraida_pp"], f["sesiones"], f["sesiones_3_toques"], f["sesiones_ganadas_vs_azar"], f["p_signo"],
                "si" if f["veredicto_original"] else "no"))
        L.append("")
    L.append("## ICC por nivel (toques de la misma raya en la misma sesion, bucket 2,5 pts; filas con >= 100 toques)")
    L.append("")
    L.append("| fila / ventana | toques | niveles distintos | toques por nivel | ICC | efecto de diseno | n efectivo |")
    L.append("|---|---|---|---|---|---|---|")
    for k, v in sorted(out["icc_por_nivel"].items()):
        L.append("| %s | %d | %d | %.2f | %.4f | %.3f | %.1f |" % (k, v["toques"], v["niveles_distintos"], v["toques_por_nivel"], v["icc"], v["efecto_diseno"], v["n_efectivo"]))
    L.append("")
    with open(os.path.join(AQUI, "auditoria_multiplicidad.md"), "w", encoding="utf-8") as f:
        f.write("\n".join(L) + "\n")
    print("\n".join(L))


if __name__ == "__main__":
    main()
