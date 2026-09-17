# -*- coding: utf-8 -*-
"""gamma_06_posthoc.py — POST-HOC, FUERA DEL PROTOCOLO, SIN VEREDICTO. Dos preguntas que dejo abiertas la tabla:
 A) El delta extremo de la vela de reloj de 5 min (el mismo disparo de V12, SIN interruptor de regimen): ir EN CONTRA acierta en los dos
    regimenes? le gana a ir en contra de CUALQUIER vela de 5 min? es el delta o es simplemente el movimiento del precio? a que hora?
 B) La devolucion de una rafaga de 10 s (movimiento medio firmado) es distinta en gamma POS que en NEG, contando DIAS y no disparos?
Todo con las 20 sesiones, la barrera +-8/600 s ya cacheada y los mismos rasgos de gamma_lib. Las estadisticas por DIA son las honestas."""
import sys, time; sys.path.insert(0, r"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\gatillo")
import numpy as np, pandas as pd, base as B, gamma_lib as L
from scipy import stats

t0 = time.time(); T = B.todas_las_sesiones(); EXP, CONF = L.mitades(T); filas = []; raf = []; OKd, MHd, Yd = {}, {}, {}
for s, d in T.items():
    f = L.rasgos(d); nv = L.niveles_seg(d); e = f["elegible"].to_numpy(); y = L.barrera_cache(s, d, 8, 600)[0]; p = d["ultimo"].to_numpy()
    OKd[s] = e; MHd[s] = L.media_hora(d); Yd[s] = y
    r = f["b5_r"].to_numpy(); thr = f["b5_r_q70"].to_numpy(); i = np.flatnonzero(e & ~np.isnan(r) & ~np.isnan(thr) & (r != 0))
    mv = p[i] - p[i - 300]
    g = d.resample("5min", label="left", closed="left")["ultimo"].last(); am = g.diff().abs(); q = am.rolling(12, min_periods=12).quantile(0.70).shift(1)
    fin = g.index + pd.Timedelta(minutes=5) - pd.Timedelta(seconds=1); thr_mv = pd.Series(q.to_numpy(), index=fin).reindex(d.index).to_numpy()[i]
    z = nv["d_zero"].to_numpy()[i]
    filas.append(pd.DataFrame(dict(idx=i, ses=s, mitad="EXP" if s in EXP else "CONF", hora=d.index[i].hour + d.index[i].minute / 60.0, y=y[i], sd=np.sign(r[i]), ext=np.abs(r[i]) >= thr[i],
                                   sm=np.sign(mv), ext_mv=np.abs(mv) >= thr_mv, reg=np.where(z >= L.ZONA, "POS", np.where(z <= -L.ZONA, "NEG", "MEDIO")))))
    disp, _, _ = L.disparos(d); ii, ll = disp["RAF10"]; zz = nv["d_zero"].to_numpy()[ii]
    for h in (30, 60, 120):
        ok = ii + h < len(p); raf.append(pd.DataFrame(dict(ses=s, h=h, m=(p[ii[ok] + h] - p[ii[ok]]) * ll[ok], reg=np.where(zz[ok] >= L.ZONA, "POS", np.where(zz[ok] <= -L.ZONA, "NEG", "MEDIO")))))
F = pd.concat(filas, ignore_index=True); F = F[F["y"] != 0]; R = pd.concat(raf, ignore_index=True)


def linea(nb, m, lado):
    x = F[m]; g = (x["y"] * lado[m]) > 0; n = len(x)
    if n < 20: print("  %-58s n %4d (poco)" % (nb, n)); return
    ac = g.mean(); pd_ = g.groupby(x["ses"]).agg(["mean", "size"]); pd_ = pd_[pd_["size"] >= 5]; t = stats.ttest_1samp(pd_["mean"], 0.5)
    print("  %-58s n %4d | acierto %5.1f %% | z50 %+5.2f | dias > 50 %%: %2d de %2d | media de los dias %5.1f %% (t por dias %+5.2f, p %.3f) | neto %+5.2f pts/op" % (
        nb, n, 100 * ac, (ac - 0.5) / np.sqrt(0.25 / n), int((pd_["mean"] > 0.5).sum()), len(pd_), 100 * pd_["mean"].mean(), t.statistic, t.pvalue, (2 * ac - 1) * 8 - B.COSTO_PTS))


print("A) IR EN CONTRA al cierre de la vela de reloj de 5 min, barrera +-8/600 s (empate %.1f %%). n = velas resueltas." % (100 * B.empate(8)))
todo = pd.Series(True, index=F.index); cd = -F["sd"]; cm = -F["sm"]
linea("contra el DELTA, todas las velas", todo, cd); linea("contra el DELTA, velas NO extremas", ~F["ext"], cd); linea("contra el DELTA, velas de delta EXTREMO (p70 de 12 previas)", F["ext"], cd)
for mt in ("EXP", "CONF"): linea("   idem, mitad %s" % mt, F["ext"] & (F["mitad"] == mt), cd)
for rg in ("POS", "NEG", "MEDIO"):
    linea("   idem, regimen zero %s" % rg, F["ext"] & (F["reg"] == rg), cd)
    for mt in ("EXP", "CONF"): linea("      %s en %s" % (rg, mt), F["ext"] & (F["reg"] == rg) & (F["mitad"] == mt), cd)
for a, b in ((13.5, 15), (15, 18), (18, 20)): linea("   idem, hora UTC %.1f-%.1f" % (a, b), F["ext"] & (F["hora"] >= a) & (F["hora"] < b), cd)
linea("contra el PRECIO, todas las velas", F["sm"] != 0, cm); linea("contra el PRECIO, velas de movimiento EXTREMO (p70 de 12 previas)", F["ext_mv"] & (F["sm"] != 0), cm)
linea("delta extremo Y precio para el MISMO lado -> en contra", F["ext"] & (F["sd"] == F["sm"]), cd); linea("delta extremo y precio para el OTRO lado -> contra el delta", F["ext"] & (F["sd"] == -F["sm"]), cd)
linea("delta extremo SIN movimiento extremo de precio -> en contra", F["ext"] & ~F["ext_mv"], cd); linea("movimiento extremo SIN delta extremo -> contra el precio", ~F["ext"] & F["ext_mv"] & (F["sm"] != 0), cm)

print("\nC) La rama 'delta extremo de 5 min, EN CONTRA' por regimen, con el placebo del protocolo (misma sesion y media hora, mismos lados) y el control 'velas NO extremas del mismo regimen'")
for rg in ("POS", "NEG"):
    for mt in ("EXP", "CONF", None):
        m = F["ext"] & (F["reg"] == rg) & ((F["mitad"] == mt) if mt else True); x = F[m]
        disp = {k: (v["idx"].to_numpy(), (-v["sd"]).to_numpy().astype(int)) for k, v in x.groupby("ses")}
        pm, ps = L.placebo(disp, OKd, MHd, Yd); ac = 100 * float(((x["y"] * -x["sd"]) > 0).mean())
        c = F[~F["ext"] & (F["reg"] == rg) & ((F["mitad"] == mt) if mt else True)]; acc = 100 * float(((c["y"] * -c["sd"]) > 0).mean())
        print("  %s %-5s n %4d | acierto %5.1f %% | placebo %5.1f +- %.2f -> z %+5.2f | velas NO extremas del mismo regimen: %5.1f %% (n %d)" % (rg, mt or "todas", len(x), ac, pm, ps, (ac - pm) / ps, acc, len(c)))

print("\nD) La misma rama, DIA POR DIA (acierto de ir en contra del delta extremo de 5 min; entre parentesis las velas)")
for rg in ("POS", "NEG"):
    x = F[F["ext"] & (F["reg"] == rg)]; g = ((x["y"] * -x["sd"]) > 0).groupby(x["ses"]).agg(["mean", "size"])
    print("  %s: " % rg + " | ".join("%s%s %2.0f %% (%d)" % (k[5:], "e" if k in EXP else "c", 100 * r_["mean"], r_["size"]) for k, r_ in g.iterrows()))

print("\nB) DEVOLUCION de la rafaga de 10 s (RAF10): movimiento medio firmado (puntos, + = sigue, - = devuelve), por regimen, contando DIAS")
for h in (30, 60, 120):
    x = R[R["h"] == h]; dia = x.groupby(["ses", "reg"])["m"].agg(["mean", "size"]).reset_index(); dia = dia[dia["size"] >= 8]
    a = dia.loc[dia["reg"] == "POS", "mean"]; b = dia.loc[dia["reg"] == "NEG", "mean"]; u = stats.mannwhitneyu(a, b)
    print("  a %3d s: POS %+5.2f pts por disparo (media de %2d dias %+5.2f) | NEG %+5.2f (media de %2d dias %+5.2f) | Mann-Whitney entre dias p = %.3f" % (
        h, x.loc[x["reg"] == "POS", "m"].mean(), len(a), a.mean(), x.loc[x["reg"] == "NEG", "m"].mean(), len(b), b.mean(), u.pvalue))
print("\n%.0f s" % (time.time() - t0))
