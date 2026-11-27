# -*- coding: utf-8 -*-
"""RONDA 19, paso 7: correccion por busqueda multiple sobre TODO lo corrido en diseno (cada regla x salida = una variante):
t por rueda, t de Bonferroni (una cola, 5 % / N, gl = ruedas - 1) y Deflated Sharpe (Bailey y Lopez de Prado 2014) con la
varianza de los Sharpe diarios de todas las variantes. Uso: python -I r19_07_busqueda.py"""
import glob, json, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from scipy import stats
import r19_comun as C

EULER = 0.5772156649


def dsr(serie, sr_trials_var, N):
    x = np.asarray(serie, float); T = len(x)
    sd = x.std(ddof=1)
    if sd == 0 or T < 3: return np.nan, np.nan
    sr = x.mean() / sd
    g3 = stats.skew(x); g4 = stats.kurtosis(x, fisher=False)
    sr0 = math.sqrt(sr_trials_var) * ((1 - EULER) * stats.norm.ppf(1 - 1 / N) + EULER * stats.norm.ppf(1 - 1 / (N * math.e)))
    z = (sr - sr0) * math.sqrt(T - 1) / math.sqrt(max(1e-12, 1 - g3 * sr + (g4 - 1) / 4 * sr * sr))
    return sr, float(stats.norm.cdf(z))


def main():
    filas = []
    for f in sorted(glob.glob(os.path.join(C.AQUI, "salidas", "res-*-diseno.json"))):
        j = json.load(open(f))
        for sal, r in j["res"].items():
            if r.get("n", 0) == 0 or "_serie050" not in r: continue
            filas.append(dict(var="%s-%s" % (j["etapa"], sal), serie=r["_serie050"], n=r["n"], acierto=r["acierto"], neto=r["c050_neto_op"],
                              t=r["c050_t"], opr=r["ops_por_rueda"]))
    N = len(filas)
    srs = [np.mean(f["serie"]) / np.std(f["serie"], ddof=1) for f in filas if np.std(f["serie"], ddof=1) > 0]
    v = float(np.var(srs, ddof=1))
    T = len(filas[0]["serie"])
    tcrit = stats.t.ppf(1 - 0.05 / N, T - 1)
    print("variantes en diseno N = %d | ruedas %d | t de Bonferroni (una cola, 5 %%/N) = %.2f | varianza de Sharpe diarios %.4f" % (N, T, tcrit, v))
    out = []
    for f in sorted(filas, key=lambda z: -z["t"]):
        sr, d = dsr(f["serie"], v, N)
        out.append(dict(variante=f["var"], n=f["n"], ops_rueda=f["opr"], acierto=f["acierto"], neto_050=f["neto"], t=f["t"], sharpe_d=round(sr, 3),
                        dsr=round(d, 3), pasa_bonferroni=bool(f["t"] >= tcrit)))
        print("  %-18s n %3d (%.2f/rueda) acierto %5.1f %% neto %+.3f t %+.2f Sharpe diario %+.3f DSR %.3f %s" % (
            f["var"], f["n"], f["opr"], f["acierto"], f["neto"], f["t"], sr, d, "PASA" if f["t"] >= tcrit else ""))
    json.dump({"N": N, "ruedas": T, "t_bonferroni": round(float(tcrit), 3), "var_sharpe": v, "variantes": out},
              open(os.path.join(C.AQUI, "salidas", "busqueda_multiple_diseno.json"), "w"), indent=1)


if __name__ == "__main__":
    main()
