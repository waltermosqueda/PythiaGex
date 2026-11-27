# -*- coding: utf-8 -*-
"""RONDA 19, explorador MODELO: la grilla de variantes (declarada ANTES de correr con las etiquetas reales) y su placebo.
Uso:
  python -I m2_grilla.py real                 -> corre las 46 variantes con los resultados reales, guarda y anota en el registro
  python -I m2_grilla.py placebo SEMILLA NREP -> repite la grilla entera con los resultados barajados DENTRO de cada rueda
Salidas: scratchpad/r19/modelo/real.json, real_masks.npz, placebo_<semilla>.jsonl"""
import os
for v in ("OPENBLAS_NUM_THREADS", "MKL_NUM_THREADS", "OMP_NUM_THREADS", "NUMEXPR_NUM_THREADS"):
    os.environ[v] = "1"
import sys, json, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import m0_comun as C
import m1_cv as V

MULTIDIA = {"vws_dist_X", "vwm_dist_X", "d_ps_poc_X", "d_pd_poc_X", "d_pdc_X"}


def variantes(nombres):
    todo = list(range(len(nombres)))
    intra = [j for j, n in enumerate(nombres) if n not in MULTIDIA]
    out = []
    for sal in ("S1", "S6"):
        for q in (0.02, 0.04, 0.08):
            qq = int(q * 100)
            out.append(dict(nombre="A2-q%d-%s" % (qq, sal), salida=sal, proc="ARBOL", prm=dict(k=6, depth=2, min_hoja_dia=1.0, q=q, permit=todo)))
            out.append(dict(nombre="A3-q%d-%s" % (qq, sal), salida=sal, proc="ARBOL", prm=dict(k=8, depth=3, min_hoja_dia=1.0, q=q, permit=todo)))
            out.append(dict(nombre="L1k10-q%d-%s" % (qq, sal), salida=sal, proc="L1", prm=dict(k=10, q=q, permit=todo)))
            out.append(dict(nombre="L1all-q%d-%s" % (qq, sal), salida=sal, proc="L1", prm=dict(k=0, q=q, permit=todo)))
            out.append(dict(nombre="RF-q%d-%s" % (qq, sal), salida=sal, proc="RULEFIT", prm=dict(k=15, q=q, arboles=40, permit=todo)))
        out.append(dict(nombre="SG2-%s" % sal, salida=sal, proc="SUBGRUPO", prm=dict(k=15, L=2, z=1.645, smin=1, smax=6, permit=todo)))
        out.append(dict(nombre="SG3-%s" % sal, salida=sal, proc="SUBGRUPO", prm=dict(k=15, L=3, z=1.645, smin=1, smax=6, permit=todo)))
        out.append(dict(nombre="SG3all-%s" % sal, salida=sal, proc="SUBGRUPO", prm=dict(k=0, L=3, z=1.645, smin=1, smax=6, permit=todo)))
        out.append(dict(nombre="SG3intra-%s" % sal, salida=sal, proc="SUBGRUPO", prm=dict(k=0, L=3, z=1.645, smin=1, smax=6, permit=intra)))
        out.append(dict(nombre="A3intra-q4-%s" % sal, salida=sal, proc="ARBOL", prm=dict(k=8, depth=3, min_hoja_dia=1.0, q=0.04, permit=intra)))
        out.append(dict(nombre="DL1d2-q4-%s" % sal, salida=sal, proc="DESTILA", prm=dict(q=0.04, depth=2, min_hoja_dia=1.0, maestro="L1", permit=todo)))
        out.append(dict(nombre="DL1d3-q4-%s" % sal, salida=sal, proc="DESTILA", prm=dict(q=0.04, depth=3, min_hoja_dia=1.0, maestro="L1", permit=todo)))
        out.append(dict(nombre="DRFd2-q4-%s" % sal, salida=sal, proc="DESTILA", prm=dict(q=0.04, depth=2, min_hoja_dia=1.0, maestro="RULEFIT",
                                                                                     prm_maestro=dict(k=15, arboles=40), permit=todo)))
    return out


def param_txt(v, nombres):
    p = {k: x for k, x in v["prm"].items() if k not in ("permit", "prm_maestro")}
    if len(v["prm"]["permit"]) < len(nombres): p["sin"] = "multidia(" + ",".join(sorted(MULTIDIA)) + ")"
    if "prm_maestro" in v["prm"]: p["maestro_prm"] = v["prm"]["prm_maestro"]
    return "%s %s; etiqueta %s>0; LORO embargo +-1 rueda; 1 posicion por vez, max 5/rueda; 71 rasgos a priori" % (v["proc"], json.dumps(p), v["salida"])


def preparar():
    C.prioridad_baja()
    df = C.cargar(); F = C.rasgos(df); B = C.Base(df)
    nombres = list(F.columns)
    X = F.to_numpy(float)
    t0 = time.time()
    pl = V.armar_pliegues(B, X, embargo=1)
    print("pliegues armados %.1f s" % (time.time() - t0), flush=True)
    return df, F, B, nombres, pl


def evaluar_todo(B, res, vs, Y, PTS, SAL):
    out = {}
    for v in vs:
        if v["nombre"] not in res: continue
        sel = res[v["nombre"]][0]
        m, tk = V.evaluar(B, sel, v["salida"], PTS[v["salida"]], SAL[v["salida"]], Y[v["salida"]])
        out[v["nombre"]] = m
    return out


def main_real():
    df, F, B, nombres, pl = preparar()
    vs = variantes(nombres)
    Y = C.etiquetas(df); PTS = {s: B.pts[s] for s in ("S1", "S6")}; SAL = {s: B.sal[s] for s in ("S1", "S6")}
    t0 = time.time()
    res = V.correr_grilla(B, pl, nombres, vs, Y, PTS)
    print("grilla real %.1f s" % (time.time() - t0), flush=True)
    met = evaluar_todo(B, res, vs, Y, PTS, SAL)
    json.dump({"metricas": met, "descripciones": {k: r[1] for k, r in res.items()}, "internas": {k: r[2] for k, r in res.items()},
               "variantes": [dict(nombre=v["nombre"], salida=v["salida"], proc=v["proc"], parametros=param_txt(v, nombres)) for v in vs]},
              open(os.path.join(C.TMP, "real.json"), "w"), indent=1, default=float)
    np.savez_compressed(os.path.join(C.TMP, "real_masks.npz"), **{k: r[0] for k, r in res.items()})
    for v in vs:
        m = met[v["nombre"]]
        print("%-16s n %4d ops/r %4.2f acierto %5.1f%% bruto %+6.2f neto050 %+6.2f neto100 %+6.2f t050 %+5.2f | prec_ev %5.1f%% ev/r %5.2f" % (
            v["nombre"], m["n"], m["ops_por_rueda"], m["acierto"] if m["n"] else np.nan, m["bruto_op"] if m["n"] else np.nan,
            m["neto050"] if m["n"] else np.nan, m["neto100"] if m["n"] else np.nan, m["t050"], m["precision_eventos"] if m["n"] else np.nan,
            m["eventos_elegidos_por_rueda"]), flush=True)


def main_placebo(semilla, nrep, solo=None):
    df, F, B, nombres, pl = preparar()
    vs = variantes(nombres)
    if solo: vs = [v for v in vs if v["nombre"] in solo]
    rng = np.random.default_rng(semilla)
    ruta = os.path.join(C.TMP, "placebo_%d.jsonl" % semilla)
    for rep in range(nrep):
        t0 = time.time()
        perm = V.permutar(B, rng)
        Y, PTS, SAL = V.resultados_permutados(B, perm)
        res = V.correr_grilla(B, pl, nombres, vs, Y, PTS, guardar_desc=False)
        met = evaluar_todo(B, res, vs, Y, PTS, SAL)
        with open(ruta, "a") as f:
            f.write(json.dumps({"semilla": semilla, "rep": rep, "metricas": met}, default=float) + "\n")
        print("placebo semilla %d rep %d %.1f s" % (semilla, rep, time.time() - t0), flush=True)


if __name__ == "__main__":
    if sys.argv[1] == "real":
        main_real()
    elif sys.argv[1] == "placebo":
        solo = sys.argv[4].split(",") if len(sys.argv) > 4 else None
        main_placebo(int(sys.argv[2]), int(sys.argv[3]), solo)
