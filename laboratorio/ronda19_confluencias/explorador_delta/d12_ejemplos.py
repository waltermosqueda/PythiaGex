# -*- coding: utf-8 -*-
"""Explorador DELTA: operaciones reales de las candidatas (fecha, hora NY, precios, firma del flujo, resultado) y anatomia mecanica."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import d_lib as L
pd.set_option("display.width", 260); pd.set_option("display.max_columns", 40); pd.set_option("display.max_rows", 200)
df = L.cargar()
H = (df.seg_rueda >= 1800) & (df.seg_rueda < 21600)
AP = df.of_apilados_favor_3v >= 1; SA = df.ctx_ret_con_agresores == 0; F3n = (df.F3 == 1) & (df.n_niveles_en_zona >= 1); RAP = df.seg_desde_ext <= 107


def hora(s):
    s = int(s); return "%02d:%02d:%02d" % (9 + (30 + s // 60) // 60, (30 + s // 60) % 60, s % 60)


todas = []
for k, m in (("w1", H & (AP | SA) & RAP), ("w6", H & (AP | F3n) & RAP)):
    ops = L.simular(df[m], "S6m")
    ops["regla"] = k
    ops["vuelta"] = np.where(ops.lado == 1, "arriba", "abajo"); ops["fade"] = np.where(ops.lado == 1, "VENTA", "COMPRA")
    ops["objetivo_fade"] = ops.ext - ops.lado * 0.25; ops["stop_fade"] = ops.entrada + ops.lado * (ops.R + 0.25)
    ops["sale"] = ops.S6_seg_salida.map(hora); ops["dur_s"] = ops.S6_seg_salida - ops.seg_rueda
    ops["firma"] = np.where(AP.loc[ops.index], "apilados=%s" % "", "")
    ops["firma"] = [("apilados %d" % a if a >= 1 else "") + (" sin_agresores(del_ret %+d)" % dr if s else "") + (" F3+nivel(%s)" % nz if f else "")
                    for a, s, dr, f, nz in zip(ops.of_apilados_favor_3v, SA.loc[ops.index], ops.ctx_del_ret, F3n.loc[ops.index], ops.niveles_en_zona)]
    todas.append(ops)
    print("\n=== %s: %d operaciones" % (k, len(ops)))
    print(ops[["dia", "hora_ny", "vuelta", "fade", "ext", "entrada", "R", "objetivo_fade", "stop_fade", "seg_desde_ext", "firma", "d60_ratio",
               "imb_neto_3v", "of_netbig60_a_favor", "vwap_rth_dist_pts", "S6m_motivo", "pts", "sale", "dur_s", "mfe_R"]].to_string(index=False))
    g = ops.pts > 0
    print("  anatomia: ganadoras %d (duran mediana %.0f s), perdedoras %d (duran mediana %.0f s)" % (g.sum(), ops.dur_s[g].median(), (~g).sum(), ops.dur_s[~g].median()))
    print("  la vuelta llego (antes de volver) a una mediana de %.2f R a favor en las ganadoras del fade y %.2f R en las perdedoras" % (
        ops.mfe_R[g].median(), ops.mfe_R[~g].median()))
    print("  d60_ratio mediana %.3f (todas las vueltas 10-15:30: %.3f); imb_neto_3v mediana %.1f (todas: %.1f); big netos a favor mediana %.0f (todas: %.0f)" % (
        ops.d60_ratio.median(), df[H].d60_ratio.median(), ops.imb_neto_3v.median(), df[H].imb_neto_3v.median(), ops.of_netbig60_a_favor.median(), df[H].of_netbig60_a_favor.median()))
    print("  marea a favor de la vuelta %.0f %% | lejos del valor %.0f %% | vuelta lado compra %.0f %%" % (
        100 * ops.a_favor_marea.mean(), 100 * (ops.vwap_rth_dist_pts > 0).mean(), 100 * (ops.lado == 1).mean()))
pd.concat(todas).to_csv(os.path.join(L.AQUI, "ops_candidatas_w1_w6.csv"), index=False)
# mecanica general: tasa de 'vuelta falsa' (la vuelta vuelve a su extremo antes de 1R) por firma, en TODAS las vueltas 10:00-15:30 (evento por evento)
h = df[H].copy()
h["falsa"] = (h.S6_motivo == "stop").astype(int)
h["rap"] = h.seg_desde_ext <= 107
for nom, mm in (("todas", h.index == h.index), ("rapidas <=107 s", h.rap), ("lentas >107 s", ~h.rap),
                ("apilados", h.of_apilados_favor_3v >= 1), ("apilados y rapidas", (h.of_apilados_favor_3v >= 1) & h.rap),
                ("sin agresores", h.ctx_ret_con_agresores == 0), ("sin agresores y rapidas", (h.ctx_ret_con_agresores == 0) & h.rap),
                ("F3 en nivel", (h.F3 == 1) & (h.n_niveles_en_zona >= 1)), ("F3 en nivel y rapidas", (h.F3 == 1) & (h.n_niveles_en_zona >= 1) & h.rap)):
    s = h[mm]
    print("evento por evento %-26s n %4d | vuelta falsa (vuelve al extremo antes de 1R) %.1f %%" % (nom, len(s), 100 * s.falsa.mean()))
