# -*- coding: utf-8 -*-
"""Explorador DELTA: JUICIO de las candidatas (no agrega variantes al registro: son reglas ya anotadas en D7/D8).
- Estadisticas completas (vara doble, t por rueda con costo 0,50 y 1,00, mejor y peor rueda, mitades, sin la mejor rueda).
- Placebo A (lado sorteado), placebo F (firma de flujo barajada entre vueltas de la misma rueda, velocidad fija; y todo barajado),
  placebo H (vuelta al azar de la misma rueda a <= 10 min y lado al azar).
- Correccion por busqueda multiple sobre TODAS las variantes de diseno de todos los registro_variantes*.csv (Bonferroni y DSR).
- Recalculo de cada operacion desde las columnas crudas de S6 (comprobacion). Simulador rapido en numpy, verificado contra d_lib."""
import os, sys, json, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import d_lib as L
df = L.cargar(); todos = L.dias(df)
assert (df.index.values == np.arange(len(df))).all()
H = ((df.seg_rueda >= 1800) & (df.seg_rueda < 21600)).values
dcode = pd.Categorical(df.dia, categories=todos).codes; ND = len(todos)
seg = df.seg_rueda.values; ssal = df.S6_seg_salida.values; p6 = df.S6_pts.values; p6m = df.S6m_pts.values
grupos = [np.nonzero(dcode == k)[0] for k in range(ND)]
COLS = {"ap": df.of_apilados_favor_3v.values, "agr": df.ctx_ret_con_agresores.values, "f3": df.F3.values, "niv": df.n_niveles_en_zona.values,
        "sx": df.seg_desde_ext.values}


def regla(k, c):
    AP = c["ap"] >= 1; SA = c["agr"] == 0; F3n = (c["f3"] == 1) & (c["niv"] >= 1); RAP = c["sx"] <= 107
    return H & {"w1": (AP | SA) & RAP, "w6": (AP | F3n) & RAP, "a2": SA & RAP, "a3": F3n & RAP, "a1": AP & RAP}[k]


def sim(mask):
    out = []; cur = -1; libre = -1; n = 0
    for i in np.nonzero(mask)[0]:
        if dcode[i] != cur: cur = dcode[i]; libre = -1; n = 0
        if seg[i] <= libre or n >= 5: continue
        out.append(i); libre = ssal[i]; n += 1
    return np.array(out, int)


def stats(idx, pts):
    x = pts[idx]; net = x - 0.5
    s = np.bincount(dcode[idx], weights=net, minlength=ND)
    t = s.mean() / s.std(ddof=1) * math.sqrt(ND) if s.std(ddof=1) > 0 else 0.0
    return float(net.mean()), float((x > 0).mean() * 100), float(t)


DESC = {"w1": "FADE si (apilados a favor de la vuelta >= 1 o retroceso sin agresores) y la vuelta tardo <= 107 s",
        "w6": "FADE si (apilados a favor >= 1 o (F3 divergencia y extremo en nivel)) y la vuelta tardo <= 107 s",
        "a2": "FADE si retroceso sin agresores y la vuelta tardo <= 107 s",
        "a3": "FADE si F3 divergencia y extremo en nivel y la vuelta tardo <= 107 s",
        "a1": "FADE si apilados a favor >= 1 y la vuelta tardo <= 107 s"}

filas = L.total_variantes()
N = len(filas); srs = np.array([f[1] for f in filas]); var_sr = float(np.var(srs, ddof=1))
tb = L.bonferroni(N)
por_archivo = pd.Series([f[0] for f in filas]).value_counts().to_dict()
print("variantes de diseno en TODOS los registros: N = %d %s | t de Bonferroni (una cola 5 %%/N, gl 25) = %.2f | var Sharpe %.4f" % (N, por_archivo, tb, var_sr), flush=True)
rng = np.random.default_rng(2026)
out = {"N_total": N, "por_archivo": por_archivo, "t_bonferroni": tb}
for k in ["w6", "w1", "a2", "a3", "a1"]:
    m = regla(k, COLS)
    idx = sim(m)
    ops = L.simular(df[pd.Series(m, index=df.index)], "S6m"); r = L.resumen(ops, todos)
    assert list(ops.index) == list(idx), "el simulador rapido no coincide con d_lib"
    rec = np.where(ops.S6_motivo == "stop", ops.R - 0.25, np.where(ops.S6_motivo == "objetivo", -(ops.R + 0.25), -ops.S6_pts))
    assert np.allclose(rec, ops.pts.values)
    s050 = L.serie_diaria(ops, todos, 0.5)
    sr, dsr_p = L.dsr(s050, var_sr, N)
    sin_mejor = np.delete(s050, int(np.argmax(s050)))
    h1 = ops[ops.dia <= "2026-09-25"]; h2 = ops[ops.dia > "2026-09-25"]
    real_net, real_hit, real_t = stats(idx, p6m)
    # placebo A: mismas operaciones, lado sorteado (S6 o S6m; mismo segundo de salida)
    vA = []; hA = []; tA = []
    for it in range(2000):
        lado = rng.random(len(idx)) < 0.5
        pts = p6m.copy(); pts[idx[lado]] = p6[idx[lado]]
        a, b, c = stats(idx, pts); vA.append(a); hA.append(b); tA.append(c)
    vA = np.array(vA); hA = np.array(hA); tA = np.array(tA)
    # placebo F: firma barajada dentro de la rueda (velocidad y hora fijas) / firma + velocidad barajadas
    resF = {}
    for nomF, cols in (("firma", ["ap", "agr", "f3", "niv"]), ("firma+velocidad", ["ap", "agr", "f3", "niv", "sx"])):
        vF = []; hF = []; tF = []
        for it in range(1000):
            c2 = dict(COLS)
            perm = np.arange(len(df))
            for g in grupos: perm[g] = rng.permutation(g)
            for cc in cols: c2[cc] = COLS[cc][perm]
            ii = sim(regla(k, c2))
            if len(ii) == 0: continue
            a, b, c = stats(ii, p6m); vF.append(a); hF.append(b); tF.append(c)
        vF = np.array(vF); hF = np.array(hF); tF = np.array(tF)
        resF[nomF] = dict(p_neto=float((vF >= real_net).mean()), neto=float(vF.mean()), acierto=float(hF.mean()), p_acierto=float((hF >= real_hit).mean()),
                          p_t=float((tF >= real_t).mean()), n_medio=None)
    # placebo H: vuelta al azar de la misma rueda a <= 10 min (cualquier lado y contexto), lado de la operacion al azar
    vH = []; hH = []
    pool = [np.nonzero(H & (dcode == kk))[0] for kk in range(ND)]
    for it in range(1000):
        sel = set()
        for i in idx:
            g = pool[dcode[i]]; c = g[np.abs(seg[g] - seg[i]) <= 600]
            if len(c): sel.add(int(rng.choice(c)))
        mk = np.zeros(len(df), bool); mk[list(sel)] = True
        ii = sim(mk)
        lado = rng.random(len(ii)) < 0.5
        pts = p6m.copy(); pts[ii[lado]] = p6[ii[lado]]
        a, b, c = stats(ii, pts); vH.append(a); hH.append(b)
    vH = np.array(vH); hH = np.array(hH)
    res = dict(regla=DESC[k], **r, sharpe=sr, dsr=dsr_p, pasa_bonferroni=bool(r["t_rueda_c050"] >= tb), t_sin_mejor_rueda=L.t_rueda(sin_mejor),
               acierto_mitad1=round(100 * (h1.pts > 0).mean(), 1), n_mitad1=len(h1), neto_mitad1=round(h1.pts.mean() - 0.5, 2),
               acierto_mitad2=round(100 * (h2.pts > 0).mean(), 1), n_mitad2=len(h2), neto_mitad2=round(h2.pts.mean() - 0.5, 2),
               placeboA=dict(p_neto=float((vA >= real_net).mean()), neto=float(vA.mean()), acierto=float(hA.mean()), p_acierto=float((hA >= real_hit).mean()),
                             p_t=float((tA >= real_t).mean())),
               placeboF=resF, placeboH=dict(p_neto=float((vH >= real_net).mean()), neto=float(vH.mean()), acierto=float(hH.mean()),
                                            p_acierto=float((hH >= real_hit).mean())),
               por_lado={int(l): dict(n=int((ops.lado == l).sum()), acierto=round(100 * (ops[ops.lado == l].pts > 0).mean(), 1)) for l in (-1, 1)},
               motivos=ops.S6m_motivo.value_counts().to_dict(), gana_pts=round(ops.pts[ops.pts > 0].mean(), 2), pierde_pts=round(ops.pts[ops.pts <= 0].mean(), 2))
    out[k] = res
    print("\n=== %s: %s" % (k, DESC[k]))
    print(L.linea(k, "S6m", r))
    print("  DSR %.3f (Sharpe diario %.3f) | Bonferroni (t >= %.2f) %s | t sin la mejor rueda %+.2f | mitades %.1f %% (n %d, neto %+.2f) / %.1f %% (n %d, neto %+.2f)" % (
        dsr_p, sr, tb, "PASA" if res["pasa_bonferroni"] else "no pasa", res["t_sin_mejor_rueda"], res["acierto_mitad1"], len(h1), res["neto_mitad1"],
        res["acierto_mitad2"], len(h2), res["neto_mitad2"]))
    print("  mejor rueda %+.2f (%s) | peor %+.2f (%s) | ruedas con operaciones %d, positivas %d | ganadora media %+.2f, perdedora media %+.2f | R medio %.2f" % (
        r["mejor_rueda"], r["dia_mejor"], r["peor_rueda"], r["dia_peor"], r["ruedas_con_ops"], r["ruedas_positivas_c050"], res["gana_pts"], res["pierde_pts"], r["R_medio"]))
    pa = res["placeboA"]
    print("  placebo A (mismas entradas, lado al azar, 2000): neto %+.2f acierto %.1f %% | p(neto) %.4f p(acierto) %.4f p(t) %.4f" % (pa["neto"], pa["acierto"], pa["p_neto"], pa["p_acierto"], pa["p_t"]))
    for nomF, v in resF.items():
        print("  placebo F (%s barajada dentro de la rueda, 1000): neto %+.2f acierto %.1f %% | p(neto) %.3f p(acierto) %.3f p(t) %.3f" % (
            nomF, v["neto"], v["acierto"], v["p_neto"], v["p_acierto"], v["p_t"]))
    ph = res["placeboH"]
    print("  placebo H (vuelta al azar misma rueda <=10 min, lado al azar, 1000): neto %+.2f acierto %.1f %% | p(neto) %.4f p(acierto) %.4f" % (ph["neto"], ph["acierto"], ph["p_neto"], ph["p_acierto"]))
    print("  por lado:", res["por_lado"], "| motivos:", res["motivos"], flush=True)
json.dump(out, open(os.path.join(L.AQUI, "juicio_candidatas.json"), "w"), indent=1, default=str)
