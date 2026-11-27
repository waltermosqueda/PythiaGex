# -*- coding: utf-8 -*-
"""ZONAS, paso 10: placebos de las candidatas (no son variantes nuevas; prueban las candidatas).
- Placebo B (pre-registro): todos los niveles del dia corridos un delta ~ U(-2X, +2X) con |delta| >= X/2 (uno por rueda y sorteo),
  la misma regla (flujo y contexto iguales); 300 sorteos. p = fraccion de sorteos con neto (c050) >= el real.
- Placebo A (pre-registro): mismas entradas (hora y precio) con lado al azar, mismo R, mismos objetivos en R; 500 sorteos.
  El resultado del lado contrario se calcula con el motor de la ronda (r19_02_motor.salida) sobre los rasgos ya calculados
  (scratchpad/r19/rasgos, solo ruedas de diseno; no se reprocesan cintas).
Salida: z10_placebos.json y z10_lado_contrario.parquet (cache)."""
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import json, time
import numpy as np, pandas as pd
import zonas_comun as Z
from z09_candidatas import REGLAS

sys.path.insert(0, Z.R19)
import r19_02_motor as M

df = Z.cargar()
H = ((df.seg_rueda >= Z.H_INI) & (df.seg_rueda < Z.H_FIN)).to_numpy()
CACHE = os.path.join(Z.AQUI, "z10_lado_contrario.parquet")
SAL_A = ("S6", "S1", "S2", "S4b")


def lado_contrario(idx_eventos):
    """Para cada evento: resultado de la operacion del lado contrario (misma entrada y segundo, mismo R) en S6, S1, S2, S4b."""
    filas = []
    if os.path.exists(CACHE):
        c = pd.read_parquet(CACHE)
        hechos = set(c["i"].tolist()); filas = c.to_dict("records")
    else:
        hechos = set()
    falta = [i for i in idx_eventos if i not in hechos]
    for dia, g in df.loc[falta].groupby("dia"):
        d = M.Dia("ES", dia)
        for i, r in g.iterrows():
            s = -int(r["lado"]); ent = float(r["entrada"]); Rp = float(r["R"]); t = int(r["seg_rueda"])
            stop = ent - s * Rp
            o = {"i": i}
            for nom, k in (("S6", 1), ("S1", 2), ("S2", 3)):
                ts, px, mot = M.salida(d, t, s, ent, stop, "obj", ent + s * k * Rp)
                o[nom + "_pts"] = (px - ent) * s; o[nom + "_seg_salida"] = int(ts)
            ts, px, mot = M.salida(d, t, s, ent, stop, "perse", X=d.X)
            o["S4b_pts"] = (px - ent) * s; o["S4b_seg_salida"] = int(ts)
            filas.append(o)
        print("  lado contrario", dia, len(g), flush=True)
    c = pd.DataFrame(filas)
    c.to_parquet(CACHE, index=False)
    return c.set_index("i")


def tomar_con_lados(idx, pts_real, ts_real, pts_op, ts_op, flip, max_dia=5):
    dias = df["dia"].to_numpy()[idx]; t = df["seg_rueda"].to_numpy()[idx]
    pts = np.where(flip, pts_op, pts_real); ts = np.where(flip, ts_op, ts_real)
    out_pts = []; out_dia = []
    dia_act = None; libre = -1; n = 0
    for k in range(len(idx)):
        if dias[k] != dia_act: dia_act = dias[k]; libre = -1; n = 0
        if t[k] <= libre or n >= max_dia: continue
        out_pts.append(pts[k]); out_dia.append(dias[k]); libre = ts[k]; n += 1
    return np.array(out_pts), np.array(out_dia)


def placebo_A(mask, sal, draws=500, seed=7):
    m = np.asarray(mask, bool) & H & df[sal + "_pts"].notna().to_numpy()
    idx = np.nonzero(m)[0]
    op = lado_contrario(list(idx))
    pr = df[sal + "_pts"].to_numpy()[idx]; tr = df[sal + "_seg_salida"].to_numpy()[idx]
    po = op.loc[idx, sal + "_pts"].to_numpy(); to = op.loc[idx, sal + "_seg_salida"].to_numpy()
    real_pts, real_d = tomar_con_lados(idx, pr, tr, po, to, np.zeros(len(idx), bool))
    real_net = float(np.mean(real_pts - 0.5)); real_hit = float(np.mean(real_pts > 0) * 100)
    # todo el lado contrario (la direccion opuesta completa)
    op_pts, _ = tomar_con_lados(idx, pr, tr, po, to, np.ones(len(idx), bool))
    rng = np.random.default_rng(seed); nets = []; hits = []
    for _ in range(draws):
        flip = rng.random(len(idx)) < 0.5
        p_, d_ = tomar_con_lados(idx, pr, tr, po, to, flip)
        nets.append(np.mean(p_ - 0.5)); hits.append(np.mean(p_ > 0) * 100)
    nets = np.array(nets); hits = np.array(hits)
    return dict(real_neto050=round(real_net, 3), real_acierto=round(real_hit, 1), real_n=len(real_pts),
                contrario_neto050=round(float(np.mean(op_pts - 0.5)), 3), contrario_acierto=round(float(np.mean(op_pts > 0) * 100), 1),
                azar_neto050_media=round(float(nets.mean()), 3), azar_neto050_p95=round(float(np.percentile(nets, 95)), 3),
                azar_acierto_media=round(float(hits.mean()), 1), p_neto=round(float((nets >= real_net).mean()), 4),
                p_acierto=round(float((hits >= real_hit).mean()), 4))


if __name__ == "__main__":
    t0 = time.time(); res = {}
    salida_json = os.path.join(Z.AQUI, "z10_placebos.json")
    if os.path.exists(salida_json): res = json.load(open(salida_json))
    for k, (f, par) in REGLAS.items():
        if len(sys.argv) > 1 and k not in sys.argv[1:]: continue
        res[k] = {"A": {}, "B": {}}
        for sal in SAL_A:
            res[k]["A"][sal] = placebo_A(f(df), sal)
            print(k, "placebo A", sal, res[k]["A"][sal], flush=True)
        for sal in ("S6", "S1", "S5", "S4b"):
            res[k]["B"][sal] = Z.placebo_B(lambda d_, dl: f(d_, dl), sal, draws=300)
            print(k, "placebo B", sal, res[k]["B"][sal], flush=True)
    json.dump(res, open(salida_json, "w"), indent=1)
    print("%.1f s" % (time.time() - t0))
