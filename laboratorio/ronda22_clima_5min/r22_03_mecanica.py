# -*- coding: utf-8 -*-
"""RONDA 22, paso 4: MECANICA (POST HOC, descriptiva; se escribio DESPUES de ver la corrida unica). Nada de esto es prueba: lo que salga
es pista para el baul y candidato para la bitacora en vivo.
(A) H1: cuanto del "extremo antes que el VWAP" es geometria (precio cerca del extremo y lejos del VWAP) y cuanto es clima, en el ano de MES
    y, para comparar, en las 55 ruedas de ES de la R21 (cinta) y en las velas de 30 min de 2024-25.
(B) el clima heredado: con que frecuencia y que tan bien marca TENDENCIA segun las 10 previas vs las 60, y la misma lectura con la mediana de 60.
(C)-(E) TENDENCIA por tipo de dia (ex post), por tamano del rango relativo y por hora.
(F)-(G) por que pierde el esqueleto: T7 (cuanto persigue la entrada al cierre, piezas, excursiones) y V4e (la banda contra su espejo).
Uso: python -I r22_03_mecanica.py -> salidas/mecanica.txt"""
import json, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r22_comun as K
import r22_motor as MO
import numpy as np, pandas as pd

L = []


def W(s=""):
    print(s, flush=True); L.append(s)


def pct(x):
    x = np.asarray(x, float)
    return 100 * np.nanmean(x) if len(x) else np.nan


def estratificado(c, col_T="T"):
    c = c[c.lado != 0].copy()
    c["b_ext"] = pd.cut(c.dist_ext_X, [-0.01, 0.5, 1, 2, 4, 1e9], labels=False)
    c["b_vw"] = pd.cut(c.dist_vw_X, [-0.01, 1, 2, 4, 8, 1e9], labels=False)
    c["b_h"] = pd.cut(c.min_dia, [0, 690, 810, 1000], labels=False)
    c["ext"] = (c.L2 == "extiende").astype(float)
    g = c.groupby(["b_ext", "b_vw", "b_h", col_T])["ext"].agg(["mean", "size"]).unstack(col_T).dropna()
    w = g[("size", True)]
    return float((g[("mean", True)] * w).sum() / w.sum()), float((g[("mean", False)] * w).sum() / w.sum()), int(w.sum()), int(len(g))


def tabla_geo(c, etiqueta):
    c = c[c.lado != 0].copy(); c["ext"] = (c.L2 == "extiende")
    W("  %s: extiende primero segun la distancia al extremo del dia (en X), TENDENCIA contra el resto" % etiqueta)
    for a, b in ((0, 0.5), (0.5, 1), (1, 2), (2, 4), (4, 99)):
        m = (c.dist_ext_X >= a) & (c.dist_ext_X < b)
        t = c[m & c["T"]]; r = c[m & ~c["T"]]
        W("     %4.1f-%-4s X del extremo: TENDENCIA %5.1f %% (n %5d) | resto %5.1f %% (n %5d) | %% de los puntos TENDENCIA en este tramo %4.1f %%" % (
            a, ("%.0f" % b) if b < 99 else "+", pct(t.ext), len(t), pct(r.ext), len(r), 100 * len(t) / max(c["T"].sum(), 1)))


def main():
    t0 = time.time(); f = K.curva_f()
    W("RONDA 22 - MECANICA (post hoc, descriptiva) %s" % time.strftime("%Y-%m-%d %H:%M"))
    c5 = pd.read_parquet(os.path.join(K.SCR, "control_5min.parquet"))
    c5["T"] = c5.tipo == "T"; c5["min_dia"] = c5.hora.str[:2].astype(int) * 60 + c5.hora.str[3:].astype(int)
    info = pd.read_csv(os.path.join(K.SAL, "ruedas_5min.csv"))
    c5 = c5.merge(info[["dia", "med60", "e_dia", "rango_rel"]], on="dia")
    c30 = pd.read_parquet(os.path.join(K.SCR, "control_30min.parquet"))
    c30["T"] = c30.tipo == "T"; c30["min_dia"] = c30.hora.str[:2].astype(int) * 60 + c30.hora.str[3:].astype(int)
    # ------------------------------------------------------------ (A) geometria contra clima
    W(""); W("(A) H1: GEOMETRIA CONTRA CLIMA")
    base_v = pct(c5.L2 == "vuelve"); W("  base 5 min: extiende primero %.1f %%, vuelve primero %.1f %%, ninguno %.1f %%" % (
        pct(c5.L2 == "extiende"), base_v, pct(c5.L2 == "ninguno")))
    tabla_geo(c5, "MES 5 min, 242 ruedas")
    a, b, n, nc = estratificado(c5)
    W("  5 min estratificado (extremo x VWAP x franja): TENDENCIA %.1f %% contra el resto con la misma geometria %.1f %% (%+.1f puntos; %d puntos TENDENCIA en %d casilleros)" % (100 * a, 100 * b, 100 * (a - b), n, nc))
    a, b, n, nc = estratificado(c30)
    W("  30 min 2024-25 estratificado: TENDENCIA %.1f %% contra %.1f %% (%+.1f puntos; %d puntos, %d casilleros)" % (100 * a, 100 * b, 100 * (a - b), n, nc))
    # la R21 (cinta de ES, 55 ruedas): puntos de control m = 30, 35, ..., 360
    reg = pd.read_parquet(os.path.join(K.SCR21, "regimen_minuto.parquet"))
    rr = reg[reg.m.isin(range(30, 361, 5))].copy()
    tipo, lvw = MO.estado(rr.rrn.to_numpy(), rr.c.to_numpy(), rr.vw.to_numpy(), 0)
    tt = []
    for _, g in rr.groupby("dia", sort=False):
        t_, l_ = MO.estado(g.rrn.to_numpy(), g.c.to_numpy(), g.vw.to_numpy(), float(g.O.iloc[0])); tt.append(pd.DataFrame({"tipo": t_, "lado": l_}, index=g.index))
    tt = pd.concat(tt); rr["tipo"] = tt.tipo; rr["lado"] = tt.lado; rr["T"] = rr.tipo == "T"
    rr["dist_ext_X"] = np.where(rr.lado > 0, (rr.H - rr.c) / rr.X, (rr.c - rr.L) / rr.X)
    rr["dist_vw_X"] = (rr.c - rr.vw).abs() / rr.X; rr["min_dia"] = 570 + rr.m + 1
    W("  R21 (cinta de ES, 55 ruedas, sus puntos de control): TENDENCIA %.1f %% de los puntos, extiende primero %.1f %% contra base %.1f %%" % (
        pct(rr["T"]), pct(rr[rr["T"]].L2 == "extiende"), pct(rr.L2 == "extiende")))
    tabla_geo(rr, "R21 cinta ES")
    a, b, n, nc = estratificado(rr)
    W("  R21 estratificado: TENDENCIA %.1f %% contra el resto con la misma geometria %.1f %% (%+.1f puntos; %d puntos TENDENCIA, %d casilleros)" % (100 * a, 100 * b, 100 * (a - b), n, nc))
    for pp in ("diseno", "confirma", "guardada"):
        s = rr[rr.periodo == pp]; a, b, n, nc = estratificado(s)
        W("     R21 %-9s TENDENCIA %5.1f %% contra misma geometria %5.1f %% (%+.1f; n %d)" % (pp, 100 * a, 100 * b, 100 * (a - b), n))
    W("  (ojo: el estratificado de la R21 'juntas' mezcla periodos con bases distintas; la comparacion limpia es periodo por periodo)")
    acum = []
    for q in sorted(c5.trimestre.unique()):
        s = c5[c5.trimestre == q]
        try:
            a, b, n, nc = estratificado(s); acum.append((a - b, n))
            W("     MES %s TENDENCIA %5.1f %% contra misma geometria %5.1f %% (%+.1f; n %d)" % (q, 100 * a, 100 * b, 100 * (a - b), n))
        except Exception:
            W("     MES %s: sin casilleros comparables" % q)
    if acum:
        W("     MES, promedio de los trimestres ponderado por puntos TENDENCIA: %+.1f puntos" % (100 * sum(x * n for x, n in acum) / sum(n for _, n in acum)))
    for a_, b_, nom in (("2024-04-01", "2024-12-31", "30 min 2024"), ("2025-01-01", "2025-08-31", "30 min 2025")):
        s = c30[(c30.dia >= a_) & (c30.dia <= b_)]
        a, b, n, nc = estratificado(s)
        W("     %s TENDENCIA %5.1f %% contra misma geometria %5.1f %% (%+.1f; n %d)" % (nom, 100 * a, 100 * b, 100 * (a - b), n))
    # ------------------------------------------------------------ (B) clima heredado y la mediana de 60
    W(""); W("(B) EL CLIMA HEREDADO: la vara de 10 ruedas contra la de 60")
    info["r"] = info.med10 / info.med60
    for t_, g in info.groupby("clima_tercil"):
        cc = c5[c5.clima_tercil == t_]
        tT = cc[cc["T"]]
        W("  tercil %2d (r = med10/med60 %.2f-%.2f; %d ruedas): TENDENCIA %4.1f %% de los puntos | rango del dia / med10 %.2f, / med60 %.2f | "
          "ruedas con TENDENCIA: rango/med60 mediano %.2f, terminaron balance %d de %d" % (
              t_, g.r.min(), g.r.max(), len(g), pct(cc["T"]), g.rango_rel.median(), (g.rango / g.med60).median(),
              (g[g.frac_T > 0].rango / g[g.frac_T > 0].med60).median() if (g.frac_T > 0).any() else np.nan,
              int(((g.frac_T > 0) & (g.L3 == "balance")).sum()), int((g.frac_T > 0).sum())))
    c5["rrn60"] = c5.rrn * c5.med10 / c5.med60
    lo_ok = np.sign(c5.c - c5.vw) == c5.lado
    T60 = (c5.rrn60 >= 1.6) & (c5.lado != 0) & c5.med60.notna()
    # mismo criterio de lado que D1: precio del mismo lado del VWAP y de la apertura -> no tengo la apertura por punto; se usa el estado T de
    # D1 como chequeo de lado cuando rrn >= 1,6; para rrn60 se exige el lado del VWAP (aproximacion declarada)
    sub = c5[T60]
    W("  POST HOC 'D1 con la mediana de 60' (rrn60 = rango / (med60 x f) >= 1,6; lado = el del VWAP): %4.1f %% de los puntos | extiende primero %.1f %% | sigue al cierre %.1f %% (base %.1f / %.1f)" % (
        pct(T60), pct(sub.L2 == "extiende"), pct(sub.y1 > 0), pct(c5.L2 == "extiende"), pct(c5.y1 > 0)))
    a = c5[c5["T"] & T60]; b = c5[c5["T"] & ~T60]; d = c5[~c5["T"] & T60 & (c5.tipo != "N")]
    W("     TENDENCIA con las dos varas: n %d, extiende %.1f %%, sigue %.1f %% | TENDENCIA solo con la de 10: n %d, extiende %.1f %%, sigue %.1f %% | solo con la de 60: n %d, extiende %.1f %%, sigue %.1f %%" % (
        len(a), pct(a.L2 == "extiende"), pct(a.y1 > 0), len(b), pct(b.L2 == "extiende"), pct(b.y1 > 0), len(d), pct(d.L2 == "extiende"), pct(d.y1 > 0)))
    c5["T2"] = c5["T"] & T60
    acum = []
    for q in sorted(c5.trimestre.unique()):
        s = c5[(c5.trimestre == q) & c5.med60.notna()]
        if s["T2"].sum() < 20: continue
        a_, b_, n_, nc_ = estratificado(s, "T2"); acum.append((a_ - b_, n_))
    if acum:
        W("     las dos varas, con la geometria controlada (por trimestre, ponderado): %+.1f puntos sobre el resto con la misma geometria" % (
            100 * sum(x * n for x, n in acum) / sum(n for _, n in acum)))
    # ------------------------------------------------------------ (C) tipo de dia (ex post), (D) rango relativo, (E) hora
    W(""); W("(C) TENDENCIA segun como TERMINO el dia (ex post: explica, no se puede usar en vivo)")
    for l3 in ("tendencia", "mixto", "balance"):
        s = c5[c5["T"] & (c5.L3 == l3)]
        W("  dia %-9s puntos TENDENCIA %4d | extiende primero %5.1f %% | sigue al cierre %5.1f %% | ruedas %d" % (l3, len(s), pct(s.L2 == "extiende"), pct(s.y1 > 0), s.dia.nunique()))
    W(""); W("(D) TENDENCIA segun el tamano del rango relativo (rrn) en el punto")
    for a_, b_ in ((1.6, 2.0), (2.0, 2.5), (2.5, 3.5), (3.5, 99)):
        s = c5[c5["T"] & (c5.rrn >= a_) & (c5.rrn < b_)]
        W("  rrn %.1f-%-4s puntos %4d (%d ruedas) | extiende %5.1f %% | sigue %5.1f %% | distancia mediana al extremo %.2f X, al VWAP %.2f X" % (
            a_, ("%.1f" % b_) if b_ < 99 else "+", len(s), s.dia.nunique(), pct(s.L2 == "extiende"), pct(s.y1 > 0), s.dist_ext_X.median(), s.dist_vw_X.median()))
    W(""); W("(E) TENDENCIA por franja horaria")
    for a_, b_, nom in ((600, 660, "10:00-10:55"), (660, 750, "11:00-12:25"), (750, 840, "12:30-13:55"), (840, 931, "14:00-15:30")):
        s = c5[c5["T"] & (c5.min_dia >= a_) & (c5.min_dia < b_)]; bb = c5[(c5.min_dia >= a_) & (c5.min_dia < b_)]
        W("  %s puntos TENDENCIA %4d | extiende %5.1f %% (base de la franja %5.1f %%) | sigue %5.1f %% (base %5.1f %%)" % (
            nom, len(s), pct(s.L2 == "extiende"), pct(bb.L2 == "extiende"), pct(s.y1 > 0), pct(bb.y1 > 0)))
    W("  por trimestre, TENDENCIA: extiende / sigue / distancia mediana al extremo")
    for q, s in c5[c5["T"]].groupby("trimestre"):
        W("     %s n %4d | extiende %5.1f %% | sigue %5.1f %% | %.2f X del extremo | %d ruedas" % (q, len(s), pct(s.L2 == "extiende"), pct(s.y1 > 0), s.dist_ext_X.median(), s.dia.nunique()))
    # ------------------------------------------------------------ (F) T7 y (G) V4e
    W(""); W("(F) POR QUE PIERDE LA T7 CON ENTRADA AL CIERRE")
    t7 = pd.read_csv(os.path.join(K.SAL, "ops-r22-T7-S6.csv"))
    t7 = t7.merge(info[["dia", "L3", "e_dia"]], on="dia")
    t7["persigue_X"] = (t7.entrada - t7.ext) * t7.lado / t7.X; t7["gana"] = t7.pts > 0
    W("  n %d | acierto %.1f %% | R medio %.2f (%.2f X) | entrada a %.2f X del extremo (mediana; la R21 entraba a ~1 X)" % (
        len(t7), pct(t7.gana), t7.R.mean(), (t7.R / t7.X).mean(), t7.persigue_X.median()))
    for a_, b_ in ((0, 1.5), (1.5, 2.0), (2.0, 3.0), (3.0, 99)):
        s = t7[(t7.persigue_X >= a_) & (t7.persigue_X < b_)]
        W("     entrada a %.1f-%-3s X del extremo: n %3d acierto %5.1f %% neto c1,00 %+.2f" % (a_, ("%.1f" % b_) if b_ < 99 else "+", len(s), pct(s.gana), (s.pts - 1).mean() if len(s) else np.nan))
    for nom, m in (("nivel en la zona", t7.n_zona >= 1), ("barrida", t7.n_barr >= 1), ("delta de la vela a favor", t7.delta_fav > 0)):
        W("     con %-26s n %3d acierto %5.1f %% | sin: n %3d acierto %5.1f %%" % (nom, int(m.sum()), pct(t7[m].gana), int((~m).sum()), pct(t7[~m].gana)))
    for l3 in ("tendencia", "mixto", "balance"):
        s = t7[t7.L3 == l3]; W("     dia que termino %-9s n %3d acierto %5.1f %%" % (l3, len(s), pct(s.gana)))
    per = t7[~t7.gana]; gan = t7[t7.gana]
    W("     perdedoras: llegaron a +0,5 R antes del stop %.1f %% | ganadoras: bajaron a -0,5 R antes del objetivo %.1f %%" % (pct(per.mfe >= 0.5), pct(gan.mae >= 0.5)))
    W("     lado contrario con el mismo R (espejo): acierto %.1f %%" % pct(t7.pts_o > 0))
    W(""); W("(G) LA BANDA DE 2 SIGMA (V4e) EN ROTACION, CONTRA SU ESPEJO")
    v4 = pd.read_csv(os.path.join(K.SAL, "ops-r22-V4e-S6.csv")).merge(info[["dia", "L3"]], on="dia")
    v4["gana"] = v4.pts > 0; v4["gana_o"] = v4.pts_o > 0
    W("  n %d | desvanecer la banda (V4e) %.1f %% | seguir la ruptura con el mismo R (espejo) %.1f %% | R medio %.2f (%.2f X)" % (
        len(v4), pct(v4.gana), pct(v4.gana_o), v4.R.mean(), (v4.R / v4.X).mean()))
    for a_, b_ in ((1.0, 1.2), (1.2, 1.4), (1.4, 1.6)):
        s = v4[(v4.rrn > a_) & (v4.rrn <= b_)]
        W("     rrn %.1f-%.1f: n %3d desvanecer %5.1f %% | espejo %5.1f %%" % (a_, b_, len(s), pct(s.gana), pct(s.gana_o)))
    for l3 in ("tendencia", "mixto", "balance"):
        s = v4[v4.L3 == l3]; W("     dia que termino %-9s n %3d desvanecer %5.1f %% | espejo %5.1f %%" % (l3, len(s), pct(s.gana), pct(s.gana_o)))
    v4["hh"] = v4.hora.str[:2].astype(int)
    for a_, b_ in ((10, 12), (12, 14), (14, 16)):
        s = v4[(v4.hh >= a_) & (v4.hh < b_)]; W("     %02d-%02d h: n %3d desvanecer %5.1f %% | espejo %5.1f %%" % (a_, b_, len(s), pct(s.gana), pct(s.gana_o)))
    # calibracion: los mismos numeros en las ruedas de la R21 (velas de la cinta), para ver si cambio el ano o la traduccion
    W(""); W("(H) EL MISMO ESQUELETO EN LAS RUEDAS DE LA R21 (velas armadas desde la cinta; de calibracion_b.txt): T7-S6 54,4 %, T6-P1 56,0 %, COMB 57,4 %, V4e 64,1 %.")
    W("    En el ano independiente: T7-S6 46,2 %, T6-P1 48,0 %, COMB 43,2 %, V4e 41,6 %. Con la entrada a stop y el orden supuesto (cota alta): 86,6 % en las ruedas de")
    W("    la R21 contra 66,7 % en el ano independiente.")
    open(os.path.join(K.SAL, "mecanica.txt"), "w", encoding="utf-8").write("\n".join(L))
    W("%.1f s" % (time.time() - t0))


if __name__ == "__main__":
    main()
