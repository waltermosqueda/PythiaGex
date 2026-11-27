# -*- coding: utf-8 -*-
"""RONDA 19, explorador MODELO: juez de la busqueda en diseno.
- placebo de etiquetas barajadas por rueda: el MISMO procedimiento (las 64 variantes con su busqueda interna) sobre resultados barajados
  dentro de cada rueda (misma permutacion por replica en las tres tandas: semilla y orden de llamadas iguales); p por variante y p de la
  FAMILIA (maximo del t por rueda de toda la busqueda en cada replica, al estilo Westfall-Young)
- correccion por el TOTAL de variantes de todos los exploradores (todos los registro_variantes*.csv de la ronda) + candidatas internas
- Deflated Sharpe (Bailey y Lopez de Prado 2014) con la varianza de los Sharpe de todas las variantes de diseno de todos los registros
Uso: python -I m5_resumen.py   -> imprime, guarda juez_modelo.txt y scratchpad/r19/modelo/juez_p.json"""
import os, sys, json, glob, csv, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from scipy import stats
import m0_comun as C

RONDA = os.path.dirname(C.AQUI)


def cargar(prefijo):
    reps = {}
    for f in glob.glob(os.path.join(C.TMP, prefijo)):
        for linea in open(f):
            r = json.loads(linea)
            reps[(r["semilla"], r["rep"])] = r["metricas"]
    return reps


def elegible(m):
    return m["n"] >= 30 and 1 <= m["ops_por_rueda"] <= 5 and (m.get("neto050") or -9) > 0


def tval(m): return m["t050"] if m["n"] else 0.0


def main():
    r1 = json.load(open(os.path.join(C.TMP, "real.json"))); r2 = json.load(open(os.path.join(C.TMP, "real2.json")))
    sr = json.load(open(os.path.join(C.TMP, "salidas_real.json")))
    real = dict(r1["metricas"]); real.update(r2["metricas"])
    salida_de = {v["nombre"]: v["salida"] for v in r1["variantes"] + r2["variantes"]}
    for c in sr:
        for s, m in sr[c].items():
            if s != c[-2:]:
                real["%s>%s" % (c, s)] = m; salida_de["%s>%s" % (c, s)] = s
    internas = dict(r1["internas"]); internas.update(r2["internas"])
    p1 = cargar("placebo_[0-9].jsonl"); p2 = cargar("placebo2_[0-9].jsonl"); p3 = cargar("placebo_salidas.jsonl")
    def plac(k, v):
        if v in r1["metricas"]: return p1[k][v]
        if v in r2["metricas"]: return p2[k][v]
        c, s = v.split(">"); return p3[k][c][s]
    claves = sorted(set(p1) & set(p2) & set(p3))
    def disp(v):
        if v in r1["metricas"]: return list(p1)
        if v in r2["metricas"]: return list(p2)
        return list(p3)
    noms = list(real.keys())
    L = ["JUEZ DEL EXPLORADOR MODELO (diseno, 26 ruedas; todo fuera de pliegue: LORO con embargo +-1 rueda)",
         "placebo = etiquetas (y pts y duracion) barajadas DENTRO de cada rueda; el procedimiento entero se repite. Replicas: etapa 1 %d, etapa 2 %d, salidas %d; completas (las tres) %d" % (
             len(p1), len(p2), len(p3), len(claves))]
    mxt = np.array([max(tval(plac(k, v)) for v in noms) for k in claves])
    Pout = {}
    filas = []
    for v in noms:
        ks = disp(v)
        ts = np.array([tval(plac(k, v)) for k in ks])
        ac = np.array([plac(k, v)["acierto"] if plac(k, v)["n"] else -1 for k in ks], float)
        m = real[v]
        p_t = (1 + (ts >= tval(m)).sum()) / (1 + len(ts))
        p_a = (1 + (ac >= (m["acierto"] if m["n"] else 999)).sum()) / (1 + len(ac))
        p_f = (1 + (mxt >= tval(m)).sum()) / (1 + len(mxt)) if len(mxt) else float("nan")
        Pout[v] = dict(p_t=p_t, p_acierto=p_a, p_familia=p_f, nrep=len(ts))
        a_ok = ac[ac >= 0]
        filas.append((v, m, p_t, p_a, p_f, np.median(a_ok) if len(a_ok) else np.nan, np.percentile(a_ok, 95) if len(a_ok) else np.nan,
                      np.median(ts), np.percentile(ts, 95)))
    L.append("")
    L.append("%-16s %4s %5s %6s %7s %7s %6s | %6s %6s %6s | placebo: acierto p50/p95  t p50/p95" % ("variante", "n", "op/r", "acierto", "neto050", "neto100", "t050", "p_t", "p_acie", "p_fam"))
    for v, m, p_t, p_a, p_f, a50, a95, t50, t95 in sorted(filas, key=lambda z: -tval(z[1])):
        L.append("%-16s %4d %5.2f %6.1f %+7.2f %+7.2f %+6.2f | %6.3f %6.3f %6.3f | %5.1f/%5.1f  %+5.2f/%+5.2f" % (
            v, m["n"], m["ops_por_rueda"], m["acierto"] if m["n"] else np.nan, m["neto050"] if m["n"] else np.nan,
            m["neto100"] if m["n"] else np.nan, tval(m), p_t, p_a, p_f, a50, a95, t50, t95))
    L.append("")
    if len(mxt):
        L.append("maximo del t por rueda de la busqueda ENTERA (%d variantes) en el placebo: p50 %+.2f, p90 %+.2f, p95 %+.2f, max %+.2f" % (
            len(noms), np.median(mxt), np.percentile(mxt, 90), np.percentile(mxt, 95), mxt.max()))
        best = max(noms, key=lambda v: tval(real[v]))
        L.append("real: mejor t = %s %+.2f -> p de la familia = %.3f" % (best, tval(real[best]), Pout[best]["p_familia"]))
        for s in ("S1", "S6"):
            def max_ac(get):
                a = [get(v)["acierto"] for v in noms if salida_de[v] == s and elegible(get(v))]
                return max(a) if a else np.nan
            ra = max_ac(lambda v: real[v])
            a = np.array([max_ac(lambda v, k=k: plac(k, v)) for k in claves]); a_ok = a[np.isfinite(a)]
            L.append("criterio de la enmienda en %s (n>=30, 1-5 por rueda, neto050>0): real mejor acierto %s; placebo: %d de %d replicas con alguna elegible, max acierto p50 %s p95 %s; p = %s" % (
                s, "%.1f%%" % ra if np.isfinite(ra) else "ninguna", len(a_ok), len(a), "%.1f" % np.median(a_ok) if len(a_ok) else "-",
                "%.1f" % np.percentile(a_ok, 95) if len(a_ok) else "-",
                "%.3f" % ((1 + (np.nan_to_num(a, nan=-1) >= ra).sum()) / (1 + len(a))) if np.isfinite(ra) else "-"))
    # correccion por el total de variantes de todos los exploradores
    regs = sorted(set(glob.glob(os.path.join(RONDA, "registro_variantes*.csv")) + glob.glob(os.path.join(RONDA, "*", "registro_variantes*.csv"))))
    tot = 0; srs = []; por = {}
    for f in regs:
        with open(f, encoding="utf-8") as fh:
            for r in csv.DictReader(fh):
                if r.get("muestra") != "diseno" or "no cuenta" in (r.get("nota") or ""): continue
                tot += 1; por[os.path.basename(f)] = por.get(os.path.basename(f), 0) + 1
                try: srs.append(float(r["sharpe_diario_c050"]))
                except Exception: pass
    T = 26; n_int = sum(internas.values())
    L.append("")
    L.append("registros leidos: " + ", ".join("%s %d" % (k, v) for k, v in por.items()) + " -> N = %d variantes de diseno" % tot)
    for N, et in ((tot, "N del registro (todos los exploradores)"), (tot + n_int, "N + candidatas internas de mis busquedas (%d)" % n_int)):
        L.append("t de Bonferroni (una cola, 5 %%/N, gl %d) con %s = %d: %.2f" % (T - 1, et, N, stats.t.ppf(1 - 0.05 / N, T - 1)))
    if len(srs) > 1:
        var = float(np.var(srs, ddof=1)); e = 0.5772156649; N = len(srs)
        sr0 = math.sqrt(var) * ((1 - e) * stats.norm.ppf(1 - 1 / N) + e * stats.norm.ppf(1 - 1 / (N * math.e)))
        L.append("Deflated Sharpe: Sharpe diario esperado del mejor por azar entre %d variantes = %.3f" % (N, sr0))
        for v in sorted(noms, key=lambda z: -tval(real[z]))[:5]:
            m = real[v]; s = m.get("sharpe050")
            if s is None or s != s: continue
            L.append("  %-16s Sharpe diario %.3f -> DSR (normal) = %.3f" % (v, s, stats.norm.cdf((s - sr0) * math.sqrt(T - 1))))
    txt = "\n".join(L)
    open(os.path.join(C.AQUI, "juez_modelo.txt"), "w", encoding="utf-8").write(txt)
    json.dump(Pout, open(os.path.join(C.TMP, "juez_p.json"), "w"), indent=1)
    print(txt)


if __name__ == "__main__":
    main()
