# -*- coding: utf-8 -*-
"""RONDA 19, explorador MODELO, ETAPA 2 (ver preregistro_etapa2.txt): busqueda por BLOQUES (contexto x momento x lugar/estructura) y
plantillas T1/T2 con umbrales ajustados dentro del pliegue. Misma validacion (LORO, embargo +-1), misma simulacion y mismo placebo
(mismas semillas que m2 => misma permutacion por replica, para tomar el maximo de TODA la busqueda).
Uso: python -I m3_etapa2.py real | placebo SEMILLA NREP"""
import os
for v in ("OPENBLAS_NUM_THREADS", "MKL_NUM_THREADS", "OMP_NUM_THREADS", "NUMEXPR_NUM_THREADS"):
    os.environ[v] = "1"
import sys, json, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import m0_comun as C
import m1_cv as V
import m2_grilla as G

CONTEXTO = ["vwr_dist_X", "vwr_dist_sd", "vwj_dist_X", "vwj_dist_sd", "ext_vwr_sd", "ext_sigma_favor", "va_hacia", "ib_ambas",
            "extension_fuera_ruido", "a_favor_marea", "marea_agr_favor"]
LUGAR = ["n_niveles_zona", "zona_vwap_bandas", "zona_vwap_central", "zona_dia_ant", "zona_noche", "zona_ib", "zona_r50", "barrida_fijo",
         "barrida_vwap", "n_barridas", "sig_nivel_R"]
MOMENTO = ["F1", "F2", "F3", "F4", "F6", "abs_pasivo_rel", "netbig_favor_rel", "big_favor_rel", "big_contra_rel", "icebergs_60", "delta60_rel",
           "delta60_ratio", "delta300_rel", "imb_favor_3v", "imb_contra_3v", "apilados_favor_3v"]
ESTRUCTURA = ["prof_ret_X", "pierna_marea_X", "dur_ret_log", "vel_ret_X", "hora"]
DEC = np.arange(0.1, 0.95, 0.1)


def matriz(Xtr, conds):
    return np.column_stack([(Xtr[:, c] <= th) if op == "<=" else (Xtr[:, c] > th) for c, op, th in conds]).astype(np.float32).T


def mejor_de(sop, pos, net, lo, hi, z):
    ok = (sop >= lo) & (sop <= hi) & (net > 0)
    if not ok.any(): return -1.0, None
    lb = np.where(ok, V.wilson_lb(pos, sop, z), -1)
    j = np.unravel_index(np.argmax(lb), lb.shape)
    return float(lb[j]), j


def proc_bloques(P, ytr, pn, nombres, prm, clave):
    idx = {n: j for j, n in enumerate(nombres)}
    z, lo, hi = prm["z"], prm["smin"] * P.nd, prm["smax"] * P.nd
    cA = V.condiciones(P.Xtr, [idx[n] for n in CONTEXTO], DEC)
    cM = V.condiciones(P.Xtr, [idx[n] for n in MOMENTO], DEC)
    A = matriz(P.Xtr, cA); M = matriz(P.Xtr, cM)
    y = ytr.astype(np.float32); p = pn.astype(np.float32)
    S = A @ M.T; Pp = (A * y) @ M.T; N = (A * p) @ M.T
    n_eval = S.size
    lb2, j2 = mejor_de(S, Pp, N, lo, hi, z)
    mejor = (lb2, [cA[j2[0]], cM[j2[1]]] if j2 is not None else None)
    if prm["L"] >= 3:
        cT = V.condiciones(P.Xtr, [idx[n] for n in LUGAR + ESTRUCTURA], DEC)
        T = matriz(P.Xtr, cT)
        lbp = np.where(S >= lo, V.wilson_lb(Pp, S, z), -1).ravel()
        top = np.argsort(-lbp)[:prm.get("beam", 500)]; top = top[lbp[top] > -1]
        if len(top):
            ia, im = np.unravel_index(top, S.shape)
            Mk = A[ia] * M[im]
            S3 = Mk @ T.T; P3 = (Mk * y) @ T.T; N3 = (Mk * p) @ T.T; n_eval += S3.size
            lb3, j3 = mejor_de(S3, P3, N3, lo, hi, z)
            if j3 is not None and lb3 > mejor[0]:
                mejor = (lb3, [cA[ia[j3[0]]], cM[im[j3[0]]], cT[j3[1]]])
    if mejor[1] is None: return np.zeros(len(P.Xte), bool), "ninguna", n_eval
    sel = V.aplicar_regla(P.Xte, mejor[1])
    return sel, " & ".join("%s %s %.4g" % (nombres[c], op, th) for c, op, th in mejor[1]) + "  (LB %.3f)" % mejor[0], n_eval


def proc_plantilla(P, ytr, pn, nombres, prm, clave):
    """Forma fija (rasgo1 op1 a) & (rasgo2 op2 b); a y b en deciles del entrenamiento; mismo criterio y restricciones."""
    idx = {n: j for j, n in enumerate(nombres)}
    (f1, o1), (f2, o2) = prm["forma"]
    c1 = [(idx[f1], o1, th) for th in np.unique(np.quantile(P.Xtr[:, idx[f1]], DEC))]
    c2 = [(idx[f2], o2, th) for th in np.unique(np.quantile(P.Xtr[:, idx[f2]], DEC))]
    A = matriz(P.Xtr, c1); B = matriz(P.Xtr, c2)
    y = ytr.astype(np.float32); p = pn.astype(np.float32)
    S = A @ B.T; Pp = (A * y) @ B.T; N = (A * p) @ B.T
    lb, j = mejor_de(S, Pp, N, prm["smin"] * P.nd, prm["smax"] * P.nd, prm["z"])
    if j is None: return np.zeros(len(P.Xte), bool), "ninguna", S.size
    regla = [c1[j[0]], c2[j[1]]]
    return V.aplicar_regla(P.Xte, regla), " & ".join("%s %s %.4g" % (nombres[c], op, th) for c, op, th in regla) + "  (LB %.3f)" % lb, S.size


V.PROCS["BLOQUES"] = proc_bloques
V.PROCS["PLANTILLA"] = proc_plantilla
_correr_orig = V.correr_grilla


def correr_grilla(base, pliegues, nombres, variantes, Y, PTS, guardar_desc=True, solo=None):
    res = {v["nombre"]: [np.zeros(len(base.t), bool), [], 0] for v in variantes}
    for fi, P in enumerate(pliegues):
        idx_te = np.nonzero(P.mte)[0]
        for v in variantes:
            sal = v["salida"]; ytr = Y[sal][P.mtr]; pn = PTS[sal][P.mtr] - 0.5
            s, desc, ne = V.PROCS[v["proc"]](P, ytr, pn, nombres, v["prm"], clave=sal)
            res[v["nombre"]][2] = max(res[v["nombre"]][2], ne)
            res[v["nombre"]][0][idx_te[s]] = True
            if guardar_desc: res[v["nombre"]][1].append((base.dias[fi], desc))
    return res


def variantes2():
    out = []
    base = dict(z=1.645, smin=1, smax=6)
    for sal in ("S1", "S6"):
        out.append(dict(nombre="SGC2-%s" % sal, salida=sal, proc="BLOQUES", prm=dict(base, L=2)))
        out.append(dict(nombre="SGC3-%s" % sal, salida=sal, proc="BLOQUES", prm=dict(base, L=3, beam=500)))
        out.append(dict(nombre="T1-%s" % sal, salida=sal, proc="PLANTILLA", prm=dict(base, forma=[("prof_ret_X", ">"), ("vwr_dist_X", "<=")])))
        out.append(dict(nombre="T2-%s" % sal, salida=sal, proc="PLANTILLA", prm=dict(base, forma=[("vwj_dist_sd", "<="), ("netbig_favor_rel", ">")])))
    return out


def param_txt(v):
    p = {k: x for k, x in v["prm"].items()}
    return "%s %s; etiqueta %s>0; LORO embargo +-1 rueda; 1 posicion por vez, max 5/rueda; bloques de preregistro_etapa2.txt" % (v["proc"], json.dumps(p), v["salida"])


def main_real():
    df, F, B, nombres, pl = G.preparar()
    vs = variantes2()
    Y = C.etiquetas(df); PTS = {s: B.pts[s] for s in ("S1", "S6")}; SAL = {s: B.sal[s] for s in ("S1", "S6")}
    t0 = time.time()
    res = correr_grilla(B, pl, nombres, vs, Y, PTS)
    print("etapa 2 real %.1f s" % (time.time() - t0), flush=True)
    met = G.evaluar_todo(B, res, vs, Y, PTS, SAL)
    json.dump({"metricas": met, "descripciones": {k: r[1] for k, r in res.items()}, "internas": {k: r[2] for k, r in res.items()},
               "variantes": [dict(nombre=v["nombre"], salida=v["salida"], proc=v["proc"], parametros=param_txt(v)) for v in vs]},
              open(os.path.join(C.TMP, "real2.json"), "w"), indent=1, default=float)
    np.savez_compressed(os.path.join(C.TMP, "real2_masks.npz"), **{k: r[0] for k, r in res.items()})
    for v in vs:
        m = met[v["nombre"]]
        print("%-10s n %4d ops/r %4.2f acierto %5.1f%% bruto %+6.2f neto050 %+6.2f neto100 %+6.2f t050 %+5.2f | prec_ev %5.1f%% ev/r %5.2f | int %d" % (
            v["nombre"], m["n"], m["ops_por_rueda"], m["acierto"] if m["n"] else np.nan, m["bruto_op"] if m["n"] else np.nan,
            m["neto050"] if m["n"] else np.nan, m["neto100"] if m["n"] else np.nan, m["t050"], m["precision_eventos"] if m["n"] else np.nan,
            m["eventos_elegidos_por_rueda"], res[v["nombre"]][2]), flush=True)
        nota = "etapa 2 (declarada en preregistro_etapa2.txt antes de correr); OOS fuera de pliegue; precision por evento %.1f%%, eventos por rueda %.2f; busqueda interna %d candidatas por pliegue" % (
            m.get("precision_eventos") or float("nan"), m["eventos_elegidos_por_rueda"], res[v["nombre"]][2])
        if v["proc"] == "PLANTILLA": nota += "; forma elegida mirando las reglas de la etapa 1 (sesgo de seleccion declarado)"
        C.anotar(v["nombre"], v["salida"], param_txt(v), m, nota=nota)


def main_placebo(semilla, nrep):
    df, F, B, nombres, pl = G.preparar()
    vs = variantes2()
    rng = np.random.default_rng(semilla)
    ruta = os.path.join(C.TMP, "placebo2_%d.jsonl" % semilla)
    for rep in range(nrep):
        t0 = time.time()
        perm = V.permutar(B, rng)
        Y, PTS, SAL = V.resultados_permutados(B, perm)
        res = correr_grilla(B, pl, nombres, vs, Y, PTS, guardar_desc=False)
        met = G.evaluar_todo(B, res, vs, Y, PTS, SAL)
        with open(ruta, "a") as f:
            f.write(json.dumps({"semilla": semilla, "rep": rep, "metricas": met}, default=float) + "\n")
        print("placebo2 semilla %d rep %d %.1f s" % (semilla, rep, time.time() - t0), flush=True)


if __name__ == "__main__":
    if sys.argv[1] == "real": main_real()
    else: main_placebo(int(sys.argv[2]), int(sys.argv[3]))
