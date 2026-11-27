# -*- coding: utf-8 -*-
"""RONDA 25, paso 6: MECANICA (post hoc, descriptivo). Que pesa, donde y por que.
 (1) LA ETIQUETA ES CIRCULAR? "A3 pierde en los dias que terminan en tendencia" (R23/R24) se mide con una etiqueta (|C - O| >= 0,6 R) que arma
     la MISMA tarde que decide la operacion. Prueba: cada operacion de A3 se resuelve en 200 tardes reflejadas al azar (simulador propio, no
     el de la R23: misma entrada, stop, objetivo en el VWAP que se corre, regla conservadora, cierre 16:00) y a cada tarde se le pone su
     etiqueta de dia. Si con tardes SIN memoria tambien "pierde en tendencia", esa parte es la vara, no el mercado.
 (2) A3 en los dias MARCADOS por cada detector (la otra cara de A3F), por muestra.
 (3) EL ARRASTRE DEL VWAP: que parte del volumen del dia ya paso a las 13:00 (la inercia del VWAP) y cuanto se mueve el VWAP por cada punto
     que se mueve el precio en la tarde, en tardes que se alejan (TA = 1) y que vuelven.
 (4) DE DONDE SALEN LAS PERDIDAS DE A3: motivo, hora, y en que tardes (ET = tarde de tendencia; TA).
 (5) EL REGIMEN: AUC de OTF / VWA / EXT contra TA por trimestre (2024T2 a 2026T4).
Uso: python -I r25_06_mecanica.py -> salidas/mecanica.txt"""
import os, pickle, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import r25_comun as K
import r25_rasgos as RS

TK = K.TK
L = []


def W(s=""):
    print(s, flush=True); L.append(s)


def tipo_de(e):
    return np.where(e >= 0.6, "tendencia", np.where(e < 0.3, "balance", "mixto"))


def sim_operacion(b, k, lado, stop, M=200, semilla=0):
    """b: velas (n, 5) o,h,l,c,v de la rueda; entrada al cierre de la vela k. Devuelve pts (M,), tipo de dia simulado (M,), y lo real."""
    o, h, l, c, v = (b[:, j] for j in range(5)); n = len(c)
    p = (h + l) / 2.0; ent = c[k]; O = o[0]
    rng = np.random.default_rng(semilla)
    cprev = np.full(M, ent); pv = np.full(M, float((p[:k + 1] * v[:k + 1]).sum())); vv = np.full(M, float(v[:k + 1].sum()))
    Hs = np.full(M, h[:k + 1].max()); Ls = np.full(M, l[:k + 1].min()); ab = np.ones(M, bool); pts = np.zeros(M)
    for j in range(k + 1, n):
        eps = rng.integers(0, 2, M) * 2.0 - 1.0
        do = o[j] - c[j - 1]; dh = h[j] - c[j - 1]; dl = l[j] - c[j - 1]; dc = c[j] - c[j - 1]
        o2 = cprev + eps * do; h2 = cprev + np.where(eps > 0, dh, -dl); l2 = cprev + np.where(eps > 0, dl, -dh); c2 = cprev + eps * dc
        lim = np.floor(pv / vv / TK + 0.5) * TK
        if lado > 0: st = l2 <= stop; ob = h2 >= lim + TK; gap = o2 <= stop
        else: st = h2 >= stop; ob = l2 <= lim - TK; gap = o2 >= stop
        m1 = ab & st; pts[m1] = (np.where(gap, o2, stop)[m1] - ent) * lado; ab &= ~m1
        m2 = ab & ob; pts[m2] = (lim[m2] - ent) * lado; ab &= ~m2
        Hs = np.maximum(Hs, h2); Ls = np.minimum(Ls, l2)
        pv = pv + (h2 + l2) / 2.0 * v[j]; vv = vv + v[j]; cprev = c2
    pts[ab] = (cprev[ab] - ent) * lado
    e = np.abs(cprev - O) / (Hs - Ls)
    return pts, tipo_de(e)


def a3_de_sim(sim):
    c = sim[(sim.s != 0) & (sim.min_dia >= 780) & (sim.min_dia <= 930) & (sim.dv >= 1.5) & (sim.ext_lado.fillna(0) <= 1.0)].copy()
    dstop = (c.stop - c.c).abs(); dobj = (c.c - c.vw).abs(); c["Pf"] = dstop / (dstop + dobj)
    c = c[c.Pf >= 0.55].sort_values(["dia", "k"]); tom = []
    for dia, g in c.groupby("dia", sort=True):
        libre = -1; n = 0
        for i, r in g.iterrows():
            if r.k <= libre or n >= 5: continue
            tom.append(i); n += 1; libre = int(r.jsal_real)
    return c.loc[tom]


def parte1():
    W("(1) LA ETIQUETA 'DIA DE TENDENCIA' CONTRA TARDES AL AZAR (cada operacion de A3 en 200 tardes reflejadas; simulador propio)")
    m5 = pickle.load(open(os.path.join(K.SCR, "dias_m5.pkl"), "rb")); ci = pickle.load(open(os.path.join(K.SCR, "dias_cinta.pkl"), "rb"))
    m30 = pickle.load(open(os.path.join(K.SCR, "dias_m30.pkl"), "rb"))
    grupos = {}
    for m, dias in (("diseno", m5), ("chequeo", m5), ("cinta", ci)):
        sim = pd.read_parquet(os.path.join(K.SCR23, "post_%s_F1.parquet" % m)); t = a3_de_sim(sim)
        filas = []
        for _, r in t.iterrows():
            b = dias[r.dia]["b5"]; lado = int(r.lado_op)
            e_real = abs(b[-1, 3] - b[0, 0]) / (b[:, 1].max() - b[:, 2].min())
            ps, ts = sim_operacion(b, int(r.k), lado, float(r.stop), semilla=int(r.dia.replace("-", "")) * 100 + int(r.k))
            filas.append(dict(dia=r.dia, pts=r.pts_real, tipo=str(tipo_de(np.array([e_real]))[0]), sim_pts=ps, sim_tipo=ts))
        grupos[m] = filas
    r24 = pd.read_csv(os.path.join(K.R24, "ops_prueba_2024_25.csv"), dtype={"fecha": str}); filas = []
    for _, r in r24.iterrows():
        b = m30[r.fecha]["b30"]; e_real = abs(b[-1, 3] - b[0, 0]) / (b[:, 1].max() - b[:, 2].min())
        ps, ts = sim_operacion(b, int(r.k), int(r.lado), float(r.stop), semilla=int(r.fecha.replace("-", "")) * 100 + int(r.k))
        filas.append(dict(dia=r.fecha, pts=r.pts, tipo=str(tipo_de(np.array([e_real]))[0]), sim_pts=ps, sim_tipo=ts))
    grupos["A3-30 2024-25"] = filas
    for m, filas in list(grupos.items()) + [("A3 juntas (diseno+chequeo+cinta)", grupos["diseno"] + grupos["chequeo"] + grupos["cinta"])]:
        R = pd.DataFrame([dict(tipo=f["tipo"], pts=f["pts"]) for f in filas])
        sp = np.concatenate([f["sim_pts"] for f in filas]); st = np.concatenate([f["sim_tipo"] for f in filas])
        lin = []
        for tp in ("tendencia", "mixto", "balance"):
            x = R[R.tipo == tp]; q = st == tp
            lin.append("%s real %3d ops %5.1f %% %+6.2f | azar %4.1f %% de las tardes %5.1f %% %+6.2f" % (
                tp[:4], len(x), 100 * (x.pts > 0).mean() if len(x) else np.nan, (x.pts - 1).mean() if len(x) else np.nan,
                100 * q.mean(), 100 * (sp[q] > 0).mean(), (sp[q] - 1).mean()))
        W("  %-34s | %s" % (m, " || ".join(lin)))
    W("  (lectura: si con tardes al azar el acierto tambien cae en los dias que el azar etiqueta 'tendencia', la etiqueta se arma con la misma")
    W("   tarde que decide la operacion: una tarde que vuelve al VWAP achica |C - O| y el dia deja de ser 'tendencia'.)")
    return grupos


def parte2():
    W(""); W("(2) A3 EN LOS DIAS MARCADOS por cada detector (parametro de teoria): n, acierto (azar), neto por operacion")
    T = pd.read_csv(os.path.join(K.SAL, "a3f_tabla.csv"))
    for det in ("APE", "OTF", "VWA", "ACE", "EXT", "CAL"):
        q = T[(T.detector == det) & (T.modo == "dia")].set_index("muestra")
        lin = []
        for m in ("diseno", "chequeo", "cinta", "a3_30_2425"):
            r = q.loc[m]
            n_m = r.A3_n - r.n
            if n_m <= 0:
                lin.append("%s sin ops" % m); continue
            neto_m = (r.A3_neto * r.A3_n - r.neto * r.n) / n_m
            acierto_m = (r.A3_acierto * r.A3_n - r.acierto * r.n) / n_m
            azar_m = (r.A3_azar * r.A3_n - r.azar * r.n) / n_m
            lin.append("%s %3d ops %5.1f %% (azar %4.1f) %+6.2f" % (m[:7], n_m, acierto_m, azar_m, neto_m))
        W("  %-4s marcados: %s" % (det, " | ".join(lin)))


def parte3():
    W(""); W("(3) EL ARRASTRE DEL VWAP")
    for nom, fuente, per in (("diseno 2024-25", "m30", K.DISENO), ("val1 2025-26", "m5", K.VAL1), ("cinta 2026", "cinta", K.A3_CINTA)):
        R = pickle.load(open(os.path.join(K.SCR, "dias_%s.pkl" % fuente), "rb"))
        w = []; rat = {0: [], 1: []}; gap = {0: [], 1: []}
        for d in sorted(R):
            if not K.en(d, per): continue
            b = R[d]["b30"]; vw = RS.vwap30(b); X = R[d]["X"]
            w.append(b[:7, 4].sum() / b[:, 4].sum())
            c13 = b[6, 3]; C16 = b[12, 3]; s = np.sign(c13 - vw[6])
            if s == 0: continue
            mv = s * (C16 - c13); mvw = s * (vw[12] - vw[6]); ta = int(mv > 0)
            if abs(mv) >= 2 * X: rat[ta].append(mvw / mv)
            gap[ta].append((s * (C16 - vw[12]) - s * (c13 - vw[6])) / X)
        W("  %-15s a las 13:00 ya paso el %.0f %% del volumen del dia (mediana) -> el VWAP de la tarde se mueve, por cada punto que el precio se mueve en la tarde: %.2f (tardes que se alejan, n %d) / %.2f (tardes que vuelven, n %d); distancia precio-VWAP: %+.2f X en las que se alejan, %+.2f X en las que vuelven" % (
            nom, 100 * np.median(w), np.median(rat[1]) if rat[1] else np.nan, len(rat[1]), np.median(rat[0]) if rat[0] else np.nan, len(rat[0]),
            np.mean(gap[1]), np.mean(gap[0])))


def parte4():
    W(""); W("(4) DE DONDE SALEN LAS PERDIDAS DE A3 (diseno + chequeo + cinta; motor de 5 min para diseno y chequeo, orden por orden para la cinta)")
    F = pd.concat([pd.read_csv(os.path.join(K.SAL, "rasgos_%s.csv" % n), dtype={"dia": str}) for n in ("val1", "explora", "val2")]).drop_duplicates("dia").set_index("dia")
    filas = []
    for m in ("diseno", "chequeo"):
        t = a3_de_sim(pd.read_parquet(os.path.join(K.SCR23, "post_%s_F1.parquet" % m)))
        for _, r in t.iterrows():
            filas.append(dict(muestra=m, dia=r.dia, pts=r.pts_real, mot={0: "fin", 1: "stop", 2: "objetivo", 3: "stop"}[int(r.mot_real)], hora=int(r.min_dia)))
    c = pd.read_csv(os.path.join(K.R23, "salidas", "ops-cinta-A3.csv"), dtype={"fecha": str})
    for _, r in c.iterrows():
        filas.append(dict(muestra="cinta", dia=r.fecha, pts=r.pts, mot=r.motivo, hora=int(r.hora_entrada[:2]) * 60 + int(r.hora_entrada[3:5])))
    A = pd.DataFrame(filas)
    A["ET"] = A.dia.map(lambda d: int(F.loc[d, "ET"]) if d in F.index else np.nan)
    A["TA"] = A.dia.map(lambda d: int(F.loc[d, "TA"]) if d in F.index else np.nan)
    A["tipo"] = A.dia.map(lambda d: F.loc[d, "tipo"] if d in F.index else "?")
    per = A[A.pts <= 0]; gan = A[A.pts > 0]
    W("  %d operaciones: ganan %d (media %+.2f), pierden %d (media %+.2f); la perdida total viene %.0f %% de stops y %.0f %% de cierres de las 16:00" % (
        len(A), len(gan), gan.pts.mean(), len(per), per.pts.mean(), 100 * per[per.mot == "stop"].pts.sum() / per.pts.sum(), 100 * per[per.mot == "fin"].pts.sum() / per.pts.sum()))
    for et in (1, 0):
        x = A[A.ET == et]
        W("  tarde de tendencia ET=%d: %3d ops, acierto %5.1f %%, neto %+6.2f, stops %4.1f %% | de ellas en dias que terminaron tendencia %d, mixtos %d, balance %d" % (
            et, len(x), 100 * (x.pts > 0).mean(), (x.pts - 1).mean(), 100 * (x.mot == "stop").mean(), (x.tipo == "tendencia").sum(), (x.tipo == "mixto").sum(), (x.tipo == "balance").sum()))
    W("  el %.0f %% de lo que pierden las perdedoras cae en tardes de tendencia (ET=1), que son el %.0f %% de las operaciones" % (
        100 * per[per.ET == 1].pts.sum() / per.pts.sum(), 100 * (A.ET == 1).mean()))
    for ta in (1, 0):
        x = A[A.TA == ta]
        W("  TA=%d (la tarde %s): %3d ops, acierto %5.1f %%, neto %+6.2f" % (ta, "se alejo del VWAP de las 13" if ta else "volvio hacia el VWAP de las 13", len(x), 100 * (x.pts > 0).mean(), (x.pts - 1).mean()))


def parte5():
    W(""); W("(5) EL REGIMEN: AUC contra TA por trimestre (todas las ruedas con s13 != 0; diseno, val1 y cinta)")
    F = pd.concat([pd.read_csv(os.path.join(K.SAL, "rasgos_%s.csv" % n), dtype={"dia": str}) for n in ("diseno", "val1", "explora", "val2")]).drop_duplicates("dia")
    F["trim"] = F.dia.str[:4] + "T" + ((F.dia.str[5:7].astype(int) - 1) // 3 + 1).astype(str)
    for q, g in F.groupby("trim"):
        W("  %s %3d ruedas | TA %4.1f %% | AUC TA: OTF %.2f  VWA %.2f  EXT %.2f  APE %.2f | dias de tendencia %4.1f %% | rango mediano %.0f pts" % (
            q, len(g), 100 * g.TA.mean(), K.auc(g.OTF_score, g.TA), K.auc(g.VWA_score, g.TA), K.auc(g.EXT_score, g.TA), K.auc(g.APE_score, g.TA),
            100 * g.TIPO.mean(), np.median(g.med10)))


def main():
    t0 = time.time(); K.frenar_si_falta_ram(2.0)
    W("RONDA 25 - MECANICA (post hoc, descriptivo) %s" % time.strftime("%Y-%m-%d %H:%M"))
    parte1(); parte2(); parte3(); parte4(); parte5()
    open(os.path.join(K.SAL, "mecanica.txt"), "w", encoding="utf-8").write("\n".join(L))
    W("%.1f s" % (time.time() - t0))


if __name__ == "__main__":
    main()
