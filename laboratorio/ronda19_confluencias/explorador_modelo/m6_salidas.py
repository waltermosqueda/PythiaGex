# -*- coding: utf-8 -*-
"""RONDA 19, explorador MODELO: las MISMAS entradas fuera de pliegue de las dos candidatas (SGC3-S6 y T1-S1), operadas con cada salida
pre-registrada de la base (S1 2R, S2 3R, S3 nivel siguiente, S4b persigue 4X, S5 VWAP RTH, S6 1R) para ver cuanto se desplazan.
Cada combinacion entrada x salida nueva es una VARIANTE (va al registro). Placebo: mismas semillas; se baraja la fila entera de resultados
(todas las salidas juntas) dentro de la rueda, se repite la eleccion con la etiqueta de la candidata y se evalua cada salida.
Uso: python -I m6_salidas.py real | placebo"""
import os
for v in ("OPENBLAS_NUM_THREADS", "MKL_NUM_THREADS", "OMP_NUM_THREADS", "NUMEXPR_NUM_THREADS"):
    os.environ[v] = "1"
import sys, json, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import m0_comun as C
import m1_cv as V
import m2_grilla as G
import m3_etapa2 as E

SALIDAS = ("S1", "S2", "S3", "S4b", "S5", "S6")
CANDS = ("SGC3-S6", "T1-S1")
NR = 23400


def tablas(df, B):
    pts = {}; sal = {}
    for s in SALIDAS:
        p = df[s + "_pts"].to_numpy(float)
        ss = df[s + "_seg_salida"].to_numpy()
        pts[s] = p; sal[s] = ss
    return pts, sal


def evaluar_salidas(B, sel, pts, sal):
    out = {}
    for s in SALIDAS:
        ok = sel & np.isfinite(pts[s])            # S5 solo existe si el VWAP queda del lado de la ganancia
        tk = C.simular(B, ok, s, pts=np.nan_to_num(pts[s]), sal=sal[s])
        out[s] = C.metricas(B, tk, s, pts=np.nan_to_num(pts[s]))
        out[s]["mfe_R_med"] = None
    return out


def main_real():
    df, F, B, nombres, pl = G.preparar()
    pts, sal = tablas(df, B)
    mk = np.load(os.path.join(C.TMP, "real2_masks.npz"))
    res = {}
    for c in CANDS:
        res[c] = evaluar_salidas(B, mk[c], pts, sal)
        tk = C.simular(B, mk[c], c[-2:])
        mfe = df.loc[tk, "mfe_R"].to_numpy()
        print("== %s (entradas fuera de pliegue); MFE de sus operaciones (en R): mediana %.2f, p25 %.2f, p75 %.2f; >=2R %.0f%%, >=3R %.0f%%" % (
            c, np.median(mfe), np.percentile(mfe, 25), np.percentile(mfe, 75), 100 * (mfe >= 2).mean(), 100 * (mfe >= 3).mean()))
        for s in SALIDAS:
            m = res[c][s]
            print("   %-4s n %3d op/r %.2f acierto %5.1f%% gana %+5.2f pierde %+5.2f bruto %+5.2f neto050 %+5.2f neto100 %+5.2f t050 %+5.2f ruedas+ %d" % (
                s, m["n"], m["ops_por_rueda"], m["acierto"], m["gana_media"], m["pierde_media"], m["bruto_op"], m["neto050"], m["neto100"], m["t050"], m["ruedas_pos"]))
    json.dump(res, open(os.path.join(C.TMP, "salidas_real.json"), "w"), indent=1, default=float)


def main_placebo():
    df, F, B, nombres, pl = G.preparar()
    pts, sal = tablas(df, B)
    vs = [v for v in E.variantes2() if v["nombre"] in CANDS]
    ruta = os.path.join(C.TMP, "placebo_salidas.jsonl")
    if os.path.exists(ruta): os.remove(ruta)
    for semilla in (1, 2, 3):
        rng = np.random.default_rng(semilla)
        for rep in range(34):
            t0 = time.time()
            perm = V.permutar(B, rng)
            ptsP = {s: pts[s][perm] for s in SALIDAS}
            salP = {s: np.where(sal[s][perm] >= 0, np.minimum(B.t + (sal[s] - B.t)[perm], NR - 1), -1) for s in SALIDAS}
            Y = {s: (np.nan_to_num(ptsP[s]) > 0).astype(int) for s in ("S1", "S6")}
            res = E.correr_grilla(B, pl, nombres, vs, Y, {s: np.nan_to_num(ptsP[s]) for s in ("S1", "S6")}, guardar_desc=False)
            out = {c: evaluar_salidas(B, res[c][0], ptsP, salP) for c in CANDS}
            with open(ruta, "a") as f:
                f.write(json.dumps({"semilla": semilla, "rep": rep, "metricas": out}, default=float) + "\n")
        print("placebo salidas semilla %d listo" % semilla, flush=True)


def resumen():
    real = json.load(open(os.path.join(C.TMP, "salidas_real.json")))
    reps = [json.loads(l)["metricas"] for l in open(os.path.join(C.TMP, "placebo_salidas.jsonl"))]
    print("replicas", len(reps))
    for c in CANDS:
        for s in SALIDAS:
            m = real[c][s]
            t = np.array([r[c][s]["t050"] for r in reps]); a = np.array([r[c][s]["acierto"] if r[c][s]["n"] else -1 for r in reps], float)
            print("%-8s %-4s real acierto %5.1f%% t %+5.2f | placebo acierto p50 %5.1f p95 %5.1f, t p50 %+5.2f p95 %+5.2f | p_t %.3f p_acierto %.3f" % (
                c, s, m["acierto"], m["t050"], np.median(a[a >= 0]), np.percentile(a[a >= 0], 95), np.median(t), np.percentile(t, 95),
                (1 + (t >= m["t050"]).sum()) / (1 + len(t)), (1 + (a >= m["acierto"]).sum()) / (1 + len(a))))


if __name__ == "__main__":
    {"real": main_real, "placebo": main_placebo, "resumen": resumen}[sys.argv[1]]()
