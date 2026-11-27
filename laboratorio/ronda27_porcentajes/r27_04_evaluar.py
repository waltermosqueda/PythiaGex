# -*- coding: utf-8 -*-
"""RONDA 27, paso 4: EVALUACION fuera de muestra de las predicciones de r27_03 (PREREGISTRO seccion 7).
Calibracion (resueltas): Brier skill contra 0,5 (agrupado y t por rueda), AUC, ECE cruda y simetrizada (cada fila con su espejo: saca la deriva
de la muestra, que un modelo antisimetrico no puede ni debe aprender), pendiente de calibracion, curva de confiabilidad con IC por bootstrap de
ruedas, placebo P2 (desplazamiento circular de las predicciones dentro de cada rueda). Regla R-frec (tau del entrenamiento) y R-60: acierto, R
medio, neto con 0,50 y 1,00, t por rueda, placebo P1 (lado al azar con la misma cantidad de compras y ventas). Desglose por clima y franja.
Bonferroni: 24 pruebas (4 modelos x 3 Y x {Brier skill, neto R-frec}), alfa 0,05/24 de una cola.
Uso: python -I r27_04_evaluar.py --tramo diseno|confirmacion|final [--juntar confirmacion final] [--Y X Xm X2]"""
import argparse, json, math, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r27_comun as K
import r27_03_modelos as MD
import numpy as np, pandas as pd

SAL = os.path.join(K.AQUI, "salidas")
os.makedirs(SAL, exist_ok=True)
BINS = [0.0, 0.40, 0.44, 0.47, 0.49, 0.51, 0.53, 0.56, 0.60, 1.0000001]
NBOOT, NP1, NP2 = 2000, 2000, 1000
ALFA = 0.05 / 24
rng0 = np.random.default_rng(2727)


def tcrit(gl):
    from scipy.stats import t as st
    return float(st.ppf(1 - ALFA, gl))


def t_por_rueda(x):
    x = np.asarray(x, float)
    if len(x) < 2 or x.std(ddof=1) == 0:
        return 0.0
    return float(x.mean() / (x.std(ddof=1) / math.sqrt(len(x))))


def auc(p, y):
    from sklearn.metrics import roc_auc_score
    if len(np.unique(y)) < 2 or np.ptp(p) == 0:
        return 0.5
    return float(roc_auc_score(y, p))


def ece(p, y):
    idx = np.digitize(p, BINS) - 1
    e = 0.0
    for b in range(len(BINS) - 1):
        s = idx == b
        if s.any():
            e += s.mean() * abs(p[s].mean() - y[s].mean())
    return float(e)


def ece_sim(p, y):
    return ece(np.r_[p, 1 - p], np.r_[y, 1 - y])


def pendiente(p, y):
    """b de y ~ sigma(b * logit(p)), sin intercepto (Newton); 1 = calibrado, < 1 = se agranda de mas."""
    z = np.log(np.clip(p, 1e-6, 1 - 1e-6) / np.clip(1 - p, 1e-6, 1))
    if np.allclose(z, 0):
        return float("nan")
    b = 1.0
    for _ in range(50):
        q = 1 / (1 + np.exp(-b * z)); g = np.sum((y - q) * z); h = -np.sum(q * (1 - q) * z * z)
        if h == 0: break
        nb = b - g / h
        if abs(nb - b) < 1e-8: b = nb; break
        b = nb
    return float(b)


def calibracion(D, Rfull):
    """D: filas resueltas con dia, m, p, y; Rfull: todas las filas del modelo (para el placebo P2). Devuelve dict de metricas."""
    dias = sorted(D["dia"].unique())
    grupos = {d: (g["p"].to_numpy(), g["y"].to_numpy()) for d, g in D.groupby("dia")}
    p = D["p"].to_numpy(); y = D["y"].to_numpy()
    bs = 1 - np.mean((p - y) ** 2) / 0.25
    bs_d = [1 - np.mean((grupos[d][0] - grupos[d][1]) ** 2) / 0.25 for d in dias]
    out = {"n": int(len(D)), "ruedas": len(dias), "arriba": float(y.mean()), "p_media": float(p.mean()), "p_sd": float(p.std()),
           "p_min": float(p.min()), "p_max": float(p.max()), "brier_skill": float(bs), "bs_t_rueda": t_por_rueda(bs_d),
           "bs_ruedas_pos": int(sum(1 for x in bs_d if x > 0)), "auc": auc(p, y), "ece": ece(p, y), "ece_sim": ece_sim(p, y),
           "pendiente": pendiente(p, y)}
    # bootstrap por rueda
    rng = np.random.default_rng(11)
    B = {"brier_skill": [], "auc": [], "ece_sim": [], "pend": []}
    for _ in range(NBOOT):
        sel = rng.choice(len(dias), len(dias), replace=True)
        pp = np.concatenate([grupos[dias[i]][0] for i in sel]); yy = np.concatenate([grupos[dias[i]][1] for i in sel])
        B["brier_skill"].append(1 - np.mean((pp - yy) ** 2) / 0.25); B["auc"].append(auc(pp, yy)); B["ece_sim"].append(ece_sim(pp, yy))
    for k_ in ("brier_skill", "auc", "ece_sim"):
        out[k_ + "_ic90"] = [float(np.quantile(B[k_], 0.05)), float(np.quantile(B[k_], 0.95))]
    # placebo P2: desplazamiento circular de p dentro de cada rueda (60 a 329 minutos) sobre la grilla completa de 389 minutos
    nul_bs = []; nul_auc = []
    rng = np.random.default_rng(22)
    base = {}
    for d, g in Rfull.groupby("dia"):
        pf = np.full(K.NMIN, 0.5); yf = np.full(K.NMIN, np.nan)
        pf[g["m"].to_numpy()] = g["p"].to_numpy(); yf[g["m"].to_numpy()] = g["y"].to_numpy()
        base[d] = (pf, yf, np.isfinite(yf))
    for _ in range(NP2):
        pp = []; yy = []
        for d, (pf, yf, ok) in base.items():
            q = np.roll(pf, -int(rng.integers(60, 330)))
            pp.append(q[ok]); yy.append(yf[ok])
        pp = np.concatenate(pp); yy = np.concatenate(yy)
        nul_bs.append(1 - np.mean((pp - yy) ** 2) / 0.25); nul_auc.append(auc(pp, yy))
    out["P2_p_bs"] = float(np.mean(np.array(nul_bs) >= bs)); out["P2_p_auc"] = float(np.mean(np.array(nul_auc) >= out["auc"]))
    out["P2_bs_med"] = float(np.median(nul_bs)); out["P2_auc_med"] = float(np.median(nul_auc))
    # confiabilidad
    rel = []
    idx = np.digitize(p, BINS) - 1
    for b in range(len(BINS) - 1):
        s = idx == b
        if not s.any():
            continue
        Dd = D[s]
        gd = {d: g["y"].to_numpy() for d, g in Dd.groupby("dia")}
        dd = list(gd); boots = []
        rng = np.random.default_rng(33 + b)
        for _ in range(500):
            sel = rng.choice(len(dd), len(dd), replace=True)
            boots.append(np.concatenate([gd[dd[i]] for i in sel]).mean())
        rel.append({"casillero": "%.2f-%.2f" % (BINS[b], min(BINS[b + 1], 1.0)), "n": int(s.sum()), "ruedas": len(dd),
                    "p_media": float(p[s].mean()), "paso": float(y[s].mean()), "ic90": [float(np.quantile(boots, 0.05)), float(np.quantile(boots, 0.95))]})
    out["confiabilidad"] = rel
    return out


def regla(R, Yk, tau_fijo=None):
    """R: filas (todas, resueltas o no) de un modelo con p, tau y las columnas de salida. Devuelve operaciones y metricas."""
    ops = []
    for d, g in R.groupby("dia"):
        g = g.sort_values("m")
        tau = tau_fijo if tau_fijo is not None else float(g["tau"].iloc[0])
        for (m, s, pr, bruto, sal) in MD.operar_dia(g, g["p"].to_numpy(), tau, Yk):
            r = g[g.m == m].iloc[0]
            ops.append({"dia": d, "m": m, "hora": "%02d:%02d" % divmod(570 + m + 1, 60), "lado": s, "p": pr, "tau": tau, "bruto": bruto,
                        "Y": float(r["Y_" + Yk]), "tipo": r["tipo"], "pL": float(r["pL_" + Yk]), "pS": float(r["pS_" + Yk]), "salida_ns": sal})
    O = pd.DataFrame(ops)
    dias = sorted(R["dia"].unique())
    res = {"ruedas": len(dias), "ops": int(len(O))}
    if len(O) == 0:
        res.update({"ops_por_rueda": 0.0, "nota": "no opero (modelo nulo o sin p que pase tau)"})
        return O, res
    O["R"] = O["bruto"] / O["Y"]
    for c in (0.5, 1.0):
        O["neto_%03d" % int(c * 100)] = O["bruto"] - c
    dn = O.groupby("dia")["neto_050"].sum().reindex(dias, fill_value=0.0)
    dn1 = O.groupby("dia")["neto_100"].sum().reindex(dias, fill_value=0.0)
    res.update({"ops_por_rueda": len(O) / len(dias), "compras": int((O.lado == 1).sum()), "ventas": int((O.lado == -1).sum()),
                "acierto": float((O.bruto > 0).mean()), "R_medio": float(O.R.mean()), "bruto_op": float(O.bruto.mean()),
                "neto050_op": float(O.neto_050.mean()), "neto100_op": float(O.neto_100.mean()),
                "t_rueda_050": t_por_rueda(dn.to_numpy()), "t_rueda_100": t_por_rueda(dn1.to_numpy()),
                "ruedas_pos_050": int((dn > 0).sum()), "neto050_total": float(dn.sum()),
                "caida_max_050": float((dn.cumsum().cummax() - dn.cumsum()).max())})
    # placebo P1: lados al azar con la misma cantidad de compras y ventas en las mismas entradas
    lados = O["lado"].to_numpy(); pL = O["pL"].to_numpy(); pS = O["pS"].to_numpy()
    obs_net = res["neto050_op"]; obs_hit = res["acierto"]
    rng = np.random.default_rng(44)
    nn = []; hh = []
    for _ in range(NP1):
        ll = rng.permutation(lados)
        br = np.where(ll == 1, pL, pS)
        nn.append(br.mean() - 0.5); hh.append((br > 0).mean())
    res["P1_p_neto"] = float(np.mean(np.array(nn) >= obs_net)); res["P1_p_acierto"] = float(np.mean(np.array(hh) >= obs_hit))
    res["P1_neto_med"] = float(np.median(nn)); res["P1_acierto_med"] = float(np.median(hh))
    # desglose
    O["franja"] = np.where(O.m + 1 <= 90, "9:31-11:00", np.where(O.m + 1 <= 210, "11:00-13:00", "13:00-15:30"))
    res["por_tipo"] = {k_: {"ops": int(len(g)), "acierto": float((g.bruto > 0).mean()), "neto050_op": float(g.neto_050.mean())}
                       for k_, g in O.groupby("tipo")}
    res["por_franja"] = {k_: {"ops": int(len(g)), "acierto": float((g.bruto > 0).mean()), "neto050_op": float(g.neto_050.mean())}
                         for k_, g in O.groupby("franja")}
    return O, res


def desglose_cal(D):
    D = D.copy()
    D["franja"] = np.where(D.m + 1 <= 90, "9:31-11:00", np.where(D.m + 1 <= 210, "11:00-13:00", "13:00-16:00"))
    out = {}
    for col in ("tipo", "franja"):
        out[col] = {}
        for k_, g in D.groupby(col):
            p = g.p.to_numpy(); y = g.y.to_numpy()
            out[col][k_] = {"n": int(len(g)), "brier_skill": float(1 - np.mean((p - y) ** 2) / 0.25), "auc": auc(p, y),
                            "p_media": float(p.mean()), "arriba": float(y.mean()), "ece_sim": ece_sim(p, y)}
    return out


def evaluar(tramos, Ys):
    T = MD.preparar(pd.read_parquet(MD.TAB))
    etiqueta = "+".join(tramos)
    rep = {"tramos": tramos, "generado_AR": time.strftime("%Y-%m-%d %H:%M"), "alfa_bonferroni": ALFA, "Y": {}}
    lineas = ["RONDA 27 - evaluacion fuera de muestra: %s (generado %s AR)" % (etiqueta, rep["generado_AR"])]
    todos_ops = []
    for Yk in Ys:
        P = pd.concat([pd.read_parquet(os.path.join(MD.PRED, "pred-%s-%s.parquet" % (tr, Yk))) for tr in tramos], ignore_index=True)
        cols = ["dia", "m", "T", "tipo", "y_" + Yk, "pL_" + Yk, "pS_" + Yk, "tL_" + Yk, "tS_" + Yk, "Y_" + Yk, "nada_" + Yk, "empate_" + Yk]
        A = P.merge(T[cols], on=["dia", "m"], how="left").rename(columns={"y_" + Yk: "y"})
        rep["Y"][Yk] = {}
        nd = A.dia.nunique(); tc = tcrit(nd - 1)
        lineas.append("\n=== Y = %s (ruedas %d; nada %.1f %%, empate %.2f %% de los minutos; t de Bonferroni con gl %d: %.2f)" % (
            Yk, nd, 100 * A[A.modelo == "M0"]["nada_" + Yk].mean(), 100 * A[A.modelo == "M0"]["empate_" + Yk].mean(), nd - 1, tc))
        for modelo in ["M0", "M1", "M2", "M3", "M_deriva"]:
            R = A[A.modelo == modelo]
            if len(R) == 0:
                continue
            D = R[R.y.notna()]
            cal = calibracion(D, R)
            cal["desglose"] = desglose_cal(D)
            r_ops, r_res = (None, {"nota": "M_deriva no opera"}) if modelo == "M_deriva" else regla(R, Yk)
            r60_ops, r60 = (None, {}) if modelo == "M_deriva" else regla(R, Yk, tau_fijo=0.60)
            rep["Y"][Yk][modelo] = {"calibracion": cal, "R_frec": r_res, "R_60": r60}
            if r_ops is not None and len(r_ops):
                r_ops = r_ops.assign(modelo=modelo, Yk=Yk, regla="R-frec"); todos_ops.append(r_ops)
            if r60_ops is not None and len(r60_ops):
                todos_ops.append(r60_ops.assign(modelo=modelo, Yk=Yk, regla="R-60"))
            lineas.append("-- %s: n %d | P(arriba) real %.3f | p media %.3f sd %.4f [%.3f, %.3f]" % (
                modelo, cal["n"], cal["arriba"], cal["p_media"], cal["p_sd"], cal["p_min"], cal["p_max"]))
            lineas.append("   Brier skill %+.5f (IC90 %+.5f a %+.5f; t por rueda %+.2f, %d/%d ruedas +; placebo P2 p %.3f, mediana %+.5f) | AUC %.4f (IC90 %.4f-%.4f; P2 p %.3f) | ECE %.4f, ECE simetrizada %.4f (IC90 %.4f-%.4f) | pendiente %.2f" % (
                cal["brier_skill"], cal["brier_skill_ic90"][0], cal["brier_skill_ic90"][1], cal["bs_t_rueda"], cal["bs_ruedas_pos"], cal["ruedas"],
                cal["P2_p_bs"], cal["P2_bs_med"], cal["auc"], cal["auc_ic90"][0], cal["auc_ic90"][1], cal["P2_p_auc"], cal["ece"], cal["ece_sim"],
                cal["ece_sim_ic90"][0], cal["ece_sim_ic90"][1], cal["pendiente"]))
            lineas.append("   confiabilidad: " + " | ".join("%s n%d p%.3f paso %.3f [%.3f-%.3f]" % (r["casillero"], r["n"], r["p_media"], r["paso"], r["ic90"][0], r["ic90"][1])
                                                   for r in cal["confiabilidad"]))
            lineas.append("   por clima: " + " | ".join("%s n%d BS %+.4f AUC %.3f p %.3f real %.3f" % (k_, v["n"], v["brier_skill"], v["auc"], v["p_media"], v["arriba"])
                                                for k_, v in cal["desglose"]["tipo"].items()))
            lineas.append("   por franja: " + " | ".join("%s n%d BS %+.4f AUC %.3f" % (k_, v["n"], v["brier_skill"], v["auc"]) for k_, v in cal["desglose"]["franja"].items()))
            for nom, rr in (("R-frec", r_res), ("R-60", r60)):
                if modelo == "M_deriva":
                    continue
                if rr.get("ops", 0) == 0:
                    lineas.append("   %s: no opero (%s)" % (nom, rr.get("nota", "")))
                    continue
                lineas.append("   %s: %d ops (%.2f por rueda; %d compras, %d ventas) acierto %.1f %% | R medio %+.3f | neto c0,50 %+.2f (t por rueda %+.2f, %d ruedas +) c1,00 %+.2f (t %+.2f) | P1 p neto %.3f acierto %.3f (mediana %.1f %%) | caida max %.1f" % (
                    nom, rr["ops"], rr["ops_por_rueda"], rr["compras"], rr["ventas"], 100 * rr["acierto"], rr["R_medio"], rr["neto050_op"],
                    rr["t_rueda_050"], rr["ruedas_pos_050"], rr["neto100_op"], rr["t_rueda_100"], rr["P1_p_neto"], rr["P1_p_acierto"],
                    100 * rr["P1_acierto_med"], rr["caida_max_050"]))
                lineas.append("      por clima: " + " | ".join("%s %d ops %.0f %% %+.2f" % (k_, v["ops"], 100 * v["acierto"], v["neto050_op"]) for k_, v in rr["por_tipo"].items())
                              + " || por franja: " + " | ".join("%s %d ops %.0f %% %+.2f" % (k_, v["ops"], 100 * v["acierto"], v["neto050_op"]) for k_, v in rr["por_franja"].items()))
    txt = os.path.join(SAL, "eval-%s.txt" % etiqueta)
    open(txt, "w", encoding="utf-8").write("\n".join(lineas) + "\n")
    json.dump(rep, open(os.path.join(SAL, "eval-%s.json" % etiqueta), "w"), indent=1, default=float)
    if todos_ops:
        pd.concat(todos_ops, ignore_index=True).to_csv(os.path.join(SAL, "ops-%s.csv" % etiqueta), index=False)
    print("\n".join(lineas))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--tramo", nargs="+", required=True)
    ap.add_argument("--Y", nargs="+", default=["X", "Xm", "X2"])
    a = ap.parse_args()
    evaluar(a.tramo, a.Y)


if __name__ == "__main__":
    main()
