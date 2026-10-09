# -*- coding: utf-8 -*-
"""
censo_mechas.py  (calibracion_1009 / soportes_metodo) -- 09-10-2026

DEFINICION OBJETIVA DE "MECHA QUE IMPORTA" y el primer control que cualquier raya tiene que superar: los NUMEROS REDONDOS.

1) Extremo = giro direccional (directional change, Guillaume et al. 1997; Glattfelder, Dupuis y Olsen 2011) con umbral THETA
   en puntos, sobre maximos/minimos de velas: un maximo es "extremo" si despues el precio cae THETA puntos ANTES de superarlo;
   un minimo, si despues sube THETA antes de perforarlo. THETA chico = "rebote"; THETA grande = "cambio de tendencia".
   No depende de la temporalidad ni de cuantas velas a los costados (no es un fractal de k velas). Se confirma tarde (mira
   adelante): sirve para JUZGAR rayas dibujadas antes, nunca como senal. Velas con rango >= THETA donde el orden maximo/minimo es
   ambiguo: se usa cierre<apertura => primero el maximo, y se cuentan.

2) Prueba de Osler en nuestro instrumento: la mecha que llega a <= TOL de un multiplo de G (redondo), es extremo mas seguido
   que la que llega a cualquier otro precio? Unidad = cada maximo (y cada minimo) de vela; Y = 1 si esa vela es el extremo
   confirmado; X = 1 si su punta cae a <= TOL de un multiplo de G. Razon de tasas RR = P(Y|X=1)/P(Y|X=0), con IC por bootstrap
   de SESIONES (las velas de una misma sesion no son independientes). RR ~ 1 => lo redondo no agrega; RR > 1 => cualquier raya
   que caiga en redondos hereda ese plus y su placebo tiene que caer en redondos tambien.
   Ademas "cobertura" (recall) de la grilla: % de extremos a <= TOL de un multiplo de G, contra la fraccion de precios que la
   grilla pinta (lift = recall / cobertura geometrica).

Datos (solo lectura):
  MNQ m1  laboratorio/dom/ronda3/velas/velas-MNQ-m1.csv            2026-08-02 .. 2026-09-17 (34 sesiones)  -> muestra principal
  MNQ m2  laboratorio/tres/datos/velas_cache_2026-10-07/...m2.csv  solo sesiones >= 2026-09-18                -> replica disjunta
  MES m1  ronda3 (08-03..09-07) + velas_cache_2026-10-07 (09-08..10-05)                                     -> ES
Uso: python -I censo_mechas.py  -> censo_mechas.json / .md
"""
import os, json, math
import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
LAB = os.path.normpath(os.path.join(AQUI, "..", ".."))
F_MNQ1 = os.path.join(LAB, "dom", "ronda3", "velas", "velas-MNQ-m1.csv")
F_MNQ2 = os.path.join(LAB, "tres", "datos", "velas_cache_2026-10-07", "velas-MNQ-m2.csv")
F_MES1a = os.path.join(LAB, "dom", "ronda3", "velas", "velas-MES-m1.csv")
F_MES1b = os.path.join(LAB, "tres", "datos", "velas_cache_2026-10-07", "velas-MES-m1.csv")
RNG = np.random.default_rng(20261009)
B = 2000


def cargar(fs, desde=None):
    d = pd.concat([pd.read_csv(f, usecols=["t", "o", "h", "l", "c"], parse_dates=["t"]) for f in fs])
    d = d.drop_duplicates("t").sort_values("t").reset_index(drop=True)
    d["ses"] = (d["t"] + pd.Timedelta(hours=2)).dt.date.astype(str)        # la sesion arranca 22:00 UTC
    if desde:
        d = d[d["ses"] >= desde].reset_index(drop=True)
    hm = d["t"].dt.hour * 60 + d["t"].dt.minute
    d["ventana"] = np.where((hm >= 13 * 60 + 30) & (hm < 20 * 60), "dia", np.where((hm >= 22 * 60) | (hm < 13 * 60 + 30), "noche", "otro"))
    return d


def giros(o, h, l, c, theta):
    """Directional change sobre OHLC. Devuelve arrays booleanos (es_pico, es_valle) por vela y la cantidad de ambiguas."""
    n = len(h)
    pico = np.zeros(n, bool); valle = np.zeros(n, bool)
    amb = 0
    modo = 0
    ihi = ilo = 0
    for i in range(1, n):
        if modo >= 0 and h[i] > h[ihi]:
            ihi = i
            if c[i] < o[i] and h[i] - l[i] >= theta:
                amb += 1
        if modo <= 0 and l[i] < l[ilo]:
            ilo = i
            if c[i] > o[i] and h[i] - l[i] >= theta:
                amb += 1
        conf_p = conf_v = False
        if modo >= 0:
            lo_desp = l[ihi + 1:i + 1].min() if i > ihi else np.inf
            if c[ihi] < o[ihi]:
                lo_desp = min(lo_desp, l[ihi])
            conf_p = h[ihi] - lo_desp >= theta
        if modo <= 0:
            hi_desp = h[ilo + 1:i + 1].max() if i > ilo else -np.inf
            if c[ilo] > o[ilo]:
                hi_desp = max(hi_desp, h[ilo])
            conf_v = hi_desp - l[ilo] >= theta
        if modo == 0 and conf_p and conf_v:          # arranque: gana el que ocurrio primero
            if ihi <= ilo:
                conf_v = False
            else:
                conf_p = False
        if conf_p:
            pico[ihi] = True
            a = ihi if c[ihi] < o[ihi] else ihi + 1
            ilo = a + int(np.argmin(l[a:i + 1]))
            modo = -1
        elif conf_v:
            valle[ilo] = True
            a = ilo if c[ilo] > o[ilo] else ilo + 1
            ihi = a + int(np.argmax(h[a:i + 1]))
            modo = 1
    return pico, valle, amb


def cerca(p, G, tol):
    r = np.mod(p, G)
    return np.minimum(r, G - r) <= tol + 1e-9


def rr_boot(Y, X, S):
    """RR = P(Y|X)/P(Y|~X) con bootstrap por sesion. Y, X booleanos; S etiquetas de sesion."""
    ses = np.unique(S)
    idx = {s: np.nonzero(S == s)[0] for s in ses}
    agg = np.array([[Y[idx[s]][X[idx[s]]].sum(), X[idx[s]].sum(), Y[idx[s]][~X[idx[s]]].sum(), (~X[idx[s]]).sum()] for s in ses], float)

    def rr(a):
        y1, n1, y0, n0 = a.sum(0)
        return (y1 / n1) / (y0 / n0) if n1 > 0 and y0 > 0 else np.nan

    est = rr(agg)
    bs = np.array([rr(agg[RNG.integers(0, len(ses), len(ses))]) for _ in range(B)])
    bs = bs[np.isfinite(bs)]
    y1, n1, y0, n0 = agg.sum(0)
    return dict(rr=round(float(est), 3), ic90=[round(float(np.percentile(bs, 5)), 3), round(float(np.percentile(bs, 95)), 3)],
                tasa_en_redondo_pct=round(100 * y1 / n1, 3), tasa_fuera_pct=round(100 * y0 / n0, 3), puntas_en_redondo=int(n1),
                extremos_en_redondo=int(y1), sesiones=len(ses))


def analizar(nombre, d, thetas, grillas, tols, tick=0.25):
    out = dict(sesiones=int(d["ses"].nunique()), velas=len(d), desde=str(d["t"].min()), hasta=str(d["t"].max()), thetas={})
    o, h, l, c = (d[k].values.astype(float) for k in "ohlc")
    S = d["ses"].values; W = d["ventana"].values
    for th in thetas:
        P = np.zeros(len(d), bool); V = np.zeros(len(d), bool); amb = 0
        for s in np.unique(S):                       # un giro por sesion: se reinicia (evita saltos del roll y del corte diario)
            ix = np.nonzero(S == s)[0]
            p, v, a = giros(o[ix], h[ix], l[ix], c[ix], th)
            P[ix] = p; V[ix] = v; amb += a
        res = dict(ambiguas=amb, ventanas={})
        for wn in ("noche", "dia"):
            m = W == wn
            nses = len(np.unique(S[m]))
            ext = int(P[m].sum() + V[m].sum())
            horas = m.sum() * (d["t"].diff().dt.total_seconds().median()) / 3600.0
            r = dict(extremos=ext, por_sesion=round(ext / max(nses, 1), 1), por_hora=round(ext / horas, 2), grillas={})
            # mecha: el extremo tiene mecha si el cierre queda lejos de la punta (>= 25 % del rango)
            rng_ = np.maximum(h - l, tick)
            mecha_p = (h - np.maximum(o, c)) / rng_ >= 0.25
            mecha_v = (np.minimum(o, c) - l) / rng_ >= 0.25
            r["con_mecha_pct"] = round(100 * (mecha_p[m & P].sum() + mecha_v[m & V].sum()) / max(ext, 1), 1)
            for G in grillas:
                for tol in tols:
                    # unidad = cada punta de vela (maximos y minimos), Y = extremo
                    Yh, Xh = P[m], cerca(h[m], G, tol); Yl, Xl = V[m], cerca(l[m], G, tol)
                    Y = np.concatenate([Yh, Yl]); X = np.concatenate([Xh, Xl]); SS = np.concatenate([S[m], S[m]])
                    rr = rr_boot(Y, X, SS)
                    ticks_vent = int(round(2 * tol / tick)) + 1
                    cobertura = ticks_vent / (G / tick)
                    pe = np.concatenate([h[m & P], l[m & V]])
                    recall = float(cerca(pe, G, tol).mean()) if len(pe) else float("nan")
                    ocup = float(X.mean())
                    rr.update(recall_pct=round(100 * recall, 2), cobertura_geom_pct=round(100 * cobertura, 2),
                              ocupacion_puntas_pct=round(100 * ocup, 2), lift_geom=round(recall / cobertura, 3),
                              lift_ocupacion=round(recall / ocup, 3) if ocup > 0 else None)
                    r["grillas"]["G%g_tol%g" % (G, tol)] = rr
            # perfil de sobrepaso (Osler 2005: los stops se apilan JUSTO DESPUES del redondo): offset firmado de la punta respecto del
            # redondo mas cercano, positivo = la punta paso el redondo en la direccion del movimiento. Extremos contra todas las puntas.
            Gp = grillas[0]
            perf = {}
            for nom_b, lo_b, hi_b in (("antes (-2..-0.25)", -2.0, -0.25), ("justo en el redondo (0)", 0.0, 0.0),
                                      ("pasado (+0.25..+2)", 0.25, 2.0), ("pasado (+2.25..+5)", 2.25, 5.0)):
                def en_banda(off):
                    return (off >= lo_b - 1e-9) & (off <= hi_b + 1e-9)
                off_h = h - np.round(h / Gp) * Gp
                off_l = np.round(l / Gp) * Gp - l
                ext_n = en_banda(off_h[m & P]).sum() + en_banda(off_l[m & V]).sum()
                tip_n = en_banda(off_h[m]).sum() + en_banda(off_l[m]).sum()
                tot_ext = (m & P).sum() + (m & V).sum(); tot_tip = 2 * m.sum()
                perf[nom_b] = dict(extremos=int(ext_n), pct_extremos=round(100 * ext_n / max(tot_ext, 1), 2),
                                   pct_puntas=round(100 * tip_n / max(tot_tip, 1), 2),
                                   razon=round((ext_n / max(tot_ext, 1)) / (tip_n / max(tot_tip, 1)), 3) if tip_n else None)
            r["sobrepaso_G%g" % Gp] = perf
            res["ventanas"][wn] = r
        out["thetas"][str(th)] = res
    return out


def md(R):
    L = ["# Censo de mechas (giro direccional) y prueba de numeros redondos", "",
         "Generado por `censo_mechas.py` (09-10-2026). RR = tasa de extremo cuando la punta de la vela cae a <= TOL de un multiplo de G, "
         "dividida por la tasa cuando cae en cualquier otro precio (IC 90 % por bootstrap de sesiones). Recall = % de extremos a <= TOL de "
         "la grilla; cobertura geometrica = fraccion de precios pintada; ocupacion = % de puntas de vela que caen en la grilla.", ""]
    for nombre, r in R.items():
        L.append("## %s  (%d sesiones, %s a %s)" % (nombre, r["sesiones"], r["desde"][:16], r["hasta"][:16]))
        L.append("")
        for th, rt in r["thetas"].items():
            for wn, rv in rt["ventanas"].items():
                L.append("**THETA %s pts, %s**: %d extremos (%.1f por sesion, %.2f por hora), con mecha >= 25 %% del rango: %.1f %%; velas ambiguas (todas las sesiones y ventanas): %d" %
                         (th, wn, rv["extremos"], rv["por_sesion"], rv["por_hora"], rv["con_mecha_pct"], rt["ambiguas"]))
                L.append("")
                L.append("| grilla | RR [IC90] | tasa en redondo % | tasa fuera % | puntas en redondo | recall % | cobertura % | ocupacion % | lift geom | lift ocup |")
                L.append("|---|---|---|---|---|---|---|---|---|---|")
                for g, v in rv["grillas"].items():
                    L.append("| %s | %.3f [%.3f-%.3f] | %.3f | %.3f | %d | %.2f | %.2f | %.2f | %.3f | %s |" % (
                        g, v["rr"], v["ic90"][0], v["ic90"][1], v["tasa_en_redondo_pct"], v["tasa_fuera_pct"], v["puntas_en_redondo"],
                        v["recall_pct"], v["cobertura_geom_pct"], v["ocupacion_puntas_pct"], v["lift_geom"], v["lift_ocupacion"]))
                L.append("")
                for k, perf in rv.items():
                    if k.startswith("sobrepaso_"):
                        L.append("Sobrepaso respecto de %s (razon = %% de extremos en la banda / %% de todas las puntas en la banda): " % k.split("_")[1] +
                                 "; ".join("%s %.2f %% vs %.2f %% -> %.3f (n %d)" % (b, v["pct_extremos"], v["pct_puntas"], v["razon"] or float("nan"), v["extremos"]) for b, v in perf.items()))
                        L.append("")
    return "\n".join(L) + "\n"


def main():
    R = {}
    R["MNQ m1 (08-02..09-17)"] = analizar("MNQ m1", cargar([F_MNQ1]), thetas=(10, 20, 40), grillas=(100, 50, 25), tols=(1, 2))
    R["MNQ m2 replica (>= 09-18)"] = analizar("MNQ m2", cargar([F_MNQ2], desde="2026-09-18"), thetas=(10, 20, 40), grillas=(100, 50, 25), tols=(1, 2))
    R["MES m1 (08-03..10-05)"] = analizar("MES m1", cargar([F_MES1a, F_MES1b]), thetas=(3, 5, 10), grillas=(25, 10, 5), tols=(0.5, 1))
    with open(os.path.join(AQUI, "censo_mechas.json"), "w", encoding="utf-8") as f:
        json.dump(R, f, ensure_ascii=False, indent=1)
    with open(os.path.join(AQUI, "censo_mechas.md"), "w", encoding="utf-8") as f:
        f.write(md(R))
    print(md(R))


if __name__ == "__main__":
    main()
