# -*- coding: utf-8 -*-
"""RONDA 17, paso 2: cuantificacion de la anatomia (PREREGISTRO seccion 7-8). Lee puntos-*.parquet y piernas-*.parquet de los dias
pedidos y escribe en <carpeta>/: enriquecimiento_<escala>.csv, combinaciones, modelo y piernas; imprime el resumen.
Uso: python -I r17_02_resumen.py <carpeta> inst dia [inst dia ...]"""
import os, sys, warnings
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import r17_comun as C
warnings.filterwarnings("ignore")

VISIBLES = ["ABS", "VOL", "EMPUJE", "DELTA_CONTRA", "DIV_SWING", "DIV_VELA_PRE", "DIV_VELA", "AGOTAMIENTO", "SUB_TERMINADA", "SUB_SIN_TERMINAR",
            "APIL_FAVOR", "BIG_FAVOR", "BIG_CONTRA", "BIG_FAVOR999", "BIG_CONTRA999", "BIG95_FAVOR", "BIG95_CONTRA", "BARRIDA",
            "L_AYER", "L_NOCHE", "L_IB", "L_VWAP", "L_REDONDO", "L_SEMANA", "L_MES", "L_IMPORTANTE"]
REACCION = ["ABS_REAC", "AGRESION_CONTRARIA", "APIL_CONTRA", "BIG_CONTRA_REAC"]
SOLO_EXTREMOS = {"AGOTAMIENTO", "SUB_TERMINADA", "SUB_SIN_TERMINAR"}
CLAVE = ["ABS", "AGOTAMIENTO", "SUB_TERMINADA", "DIV_SWING", "BARRIDA", "BIG_FAVOR", "DELTA_CONTRA", "AGRESION_CONTRARIA", "APIL_CONTRA"]


def cargar(pedidos):
    P, PR = [], []
    for inst, dia in pedidos:
        P.append(pd.read_parquet(os.path.join(C.R17, "puntos-%s-%s.parquet" % (inst, dia))))
        PR.append(pd.read_parquet(os.path.join(C.R17, "piernas-%s-%s.parquet" % (inst, dia))))
    P = pd.concat(P, ignore_index=True); PR = pd.concat(PR, ignore_index=True)
    P["SUB_TERMINADA"] = np.where(P["SUBASTA"].eq("NA"), np.nan, P["SUBASTA"].eq("TERMINADA").astype(float))
    P["SUB_SIN_TERMINAR"] = np.where(P["SUBASTA"].eq("NA"), np.nan, P["SUBASTA"].eq("SIN_TERMINAR").astype(float))
    P["DIV_SWING"] = np.where(P["DIV_aplica"].astype(float) > 0, P["DIV_SWING"].astype(float), np.nan)
    for c in VISIBLES + REACCION:
        P[c] = P[c].astype(float)
    P["rueda"] = P["inst"] + " " + P["dia"]
    PR["rueda"] = PR["inst"] + " " + PR["dia"]
    return P, PR


def rr_katz(a, n1, c, n2):
    if n1 == 0 or n2 == 0:
        return np.nan, np.nan, np.nan
    if a == 0 or c == 0:
        a, c, n1, n2 = a + 0.5, c + 0.5, n1 + 1, n2 + 1
    rr = (a / n1) / (c / n2); se = np.sqrt(max(0.0, 1 / a - 1 / n1 + 1 / c - 1 / n2))
    return rr, rr * np.exp(-1.96 * se), rr * np.exp(1.96 * se)


def enriquecimiento(P, escala):
    X = P[P["escala"] == escala]; filas = []
    for f in VISIBLES + REACCION:
        g = X.loc[X["clase"] == "giro", ["rueda", f]].dropna(); p = X.loc[X["clase"] == "pausa", ["rueda", f]].dropna()
        z = X.loc[X["clase"] == "azar", ["rueda", f]].dropna()
        a, n1 = g[f].sum(), len(g); c, n2 = p[f].sum(), len(p); e, n3 = z[f].sum(), len(z)
        rr, lo, hi = rr_katz(a, n1, c, n2)
        rrz, loz, hiz = rr_katz(a, n1, e, n3) if f not in SOLO_EXTREMOS else (np.nan, np.nan, np.nan)
        arriba = abajo = medibles = 0; por_dia = []
        for r in sorted(X["rueda"].unique()):
            gg = g[g["rueda"] == r][f]; pp = p[p["rueda"] == r][f]
            if len(gg) >= 5 and len(pp) >= 5 and (gg.sum() + pp.sum()) > 0:
                medibles += 1; dg, dp = gg.mean(), pp.mean()
                arriba += dg > dp; abajo += dg < dp; por_dia.append("%+.0f" % (100 * (dg - dp)))
            else:
                por_dia.append("·")
        repite = (medibles >= 3) and ((lo > 1 and arriba >= max(medibles - 1, 3)) or (hi < 1 and abajo >= max(medibles - 1, 3)))
        filas.append({"factor": f, "tipo": "reaccion" if f in REACCION else "visible", "giros_%": 100 * a / n1 if n1 else np.nan, "n_giros": n1,
                      "pausas_%": 100 * c / n2 if n2 else np.nan, "n_pausas": n2, "azar_%": 100 * e / n3 if n3 else np.nan, "n_azar": n3,
                      "RR_vs_pausa": rr, "IC95_lo": lo, "IC95_hi": hi, "RR_vs_azar": rrz, "IC95z_lo": loz, "IC95z_hi": hiz,
                      "dias_giro>pausa": arriba, "dias_giro<pausa": abajo, "dias_medibles": medibles, "dif_pp_por_dia": " ".join(por_dia),
                      "SE_REPITE": bool(repite)})
    return pd.DataFrame(filas)


def auc(a, b):
    a = np.asarray(a, float); b = np.asarray(b, float); a = a[np.isfinite(a)]; b = b[np.isfinite(b)]
    if len(a) == 0 or len(b) == 0:
        return np.nan
    r = pd.Series(np.concatenate([a, b])).rank().to_numpy()
    return (r[:len(a)].sum() - len(a) * (len(a) + 1) / 2) / (len(a) * len(b))


def continuos(P, escala):
    X = P[P["escala"] == escala].copy()
    X["agot_ratio"] = X["v_ext"] / X["v_in"].replace(0, np.nan)
    filas = []
    for c in ("abs_pre", "vol_pre", "delta_pre_s", "abs_reac", "delta_reac_s", "agot_ratio"):
        # rango dentro de la rueda (ES y MES tienen escalas distintas)
        X[c + "_rk"] = X.groupby("rueda")[c].rank(pct=True)
        g = X.loc[X["clase"] == "giro", c + "_rk"]; p = X.loc[X["clase"] == "pausa", c + "_rk"]
        aucs = [auc(X.loc[(X["rueda"] == r) & (X["clase"] == "giro"), c], X.loc[(X["rueda"] == r) & (X["clase"] == "pausa"), c]) for r in sorted(X["rueda"].unique())]
        filas.append({"variable": c, "mediana_giros": X.loc[X["clase"] == "giro", c].median(), "mediana_pausas": X.loc[X["clase"] == "pausa", c].median(),
                      "AUC_giro_vs_pausa_rangos": auc(g, p), "AUC_por_rueda": " ".join("%.2f" % v for v in aucs)})
    return pd.DataFrame(filas)


def combinaciones(P, escala):
    X = P[(P["escala"] == escala) & (P["clase"].isin(["giro", "pausa"]))].copy()
    for c in CLAVE:
        X[c] = X[c].fillna(0).astype(int)
    X["combo"] = X[CLAVE].apply(lambda r: "+".join(k for k in CLAVE if r[k]) or "(ninguno)", axis=1)
    out = []
    for lug in (True, False):
        Y = X[X["L_IMPORTANTE"].fillna(0).astype(bool) == lug]
        ng = (Y["clase"] == "giro").sum(); npau = (Y["clase"] == "pausa").sum()
        t = Y.groupby(["combo", "clase"]).size().unstack(fill_value=0)
        for k in ("giro", "pausa"):
            if k not in t: t[k] = 0
        t["giros_%"] = 100 * t["giro"] / max(1, ng); t["pausas_%"] = 100 * t["pausa"] / max(1, npau)
        t = t.sort_values("giro", ascending=False).head(12).reset_index()
        t["lugar_importante"] = lug; t["n_giros_total"] = ng; t["n_pausas_total"] = npau
        out.append(t)
    return pd.concat(out, ignore_index=True)


def modelo(P, escala):
    from sklearn.linear_model import LogisticRegression
    from sklearn.tree import DecisionTreeClassifier
    X = P[(P["escala"] == escala) & (P["clase"].isin(["giro", "pausa"]))].copy()
    X["DIV_aplica"] = X["DIV_aplica"].astype(float); X["DIV_SWING0"] = X["DIV_SWING"].fillna(0)
    vis = ["ABS", "VOL", "EMPUJE", "DELTA_CONTRA", "DIV_aplica", "DIV_SWING0", "DIV_VELA_PRE", "AGOTAMIENTO", "SUB_TERMINADA", "SUB_SIN_TERMINAR",
           "APIL_FAVOR", "BIG_FAVOR", "BIG_CONTRA", "BARRIDA", "L_IMPORTANTE", "L_VWAP"]
    out = {}
    for nom, cols in (("visibles", vis), ("con_reaccion", vis + REACCION)):
        M = X[cols].fillna(0).astype(float).to_numpy(); y = (X["clase"] == "giro").astype(int).to_numpy(); r = X["rueda"].to_numpy()
        mu, sd = M.mean(0), M.std(0); sd[sd == 0] = 1; Z = (M - mu) / sd
        preds_l = np.full(len(y), np.nan); preds_t = np.full(len(y), np.nan)
        for rr in np.unique(r):
            tr, te = r != rr, r == rr
            if len(np.unique(y[tr])) < 2:
                continue
            lr = LogisticRegression(C=1.0, max_iter=2000).fit(Z[tr], y[tr]); preds_l[te] = lr.predict_proba(Z[te])[:, 1]
            dt = DecisionTreeClassifier(max_depth=3, min_samples_leaf=10, random_state=17).fit(M[tr], y[tr]); preds_t[te] = dt.predict_proba(M[te])[:, 1]
        lr = LogisticRegression(C=1.0, max_iter=2000).fit(Z, y)
        dt = DecisionTreeClassifier(max_depth=3, min_samples_leaf=10, random_state=17).fit(M, y)
        coef = pd.Series(lr.coef_[0], index=cols).sort_values(key=np.abs, ascending=False)
        imp = pd.Series(dt.feature_importances_, index=cols).sort_values(ascending=False)
        auc_l = auc(preds_l[y == 1], preds_l[y == 0]); auc_t = auc(preds_t[y == 1], preds_t[y == 0])
        from sklearn.tree import export_text
        out[nom] = {"auc_lodo_logistica": auc_l, "auc_lodo_arbol": auc_t, "coef": coef, "importancia_arbol": imp,
                    "arbol": export_text(dt, feature_names=cols, decimals=2), "n": len(y), "giros": int(y.sum())}
    return out


def piernas(PR, escala):
    Y = PR[(PR["escala"] == escala) & (~PR["final"].astype(bool))].copy()
    Y["ratio_vacio"] = Y["seg_por_tick_vacio"] / Y["seg_por_tick_resto"]
    Y["fallo_abs"] = Y["abs_contra_fallidas"] / Y["abs_contra_n"].replace(0, np.nan)
    agg = Y.groupby(["inst", "tipo_dia"]).agg(piernas=("pts", "size"), pts_med=("pts", "median"), min_med=("min", "median"), vel_med=("pts_min", "median"),
                                    delta_a_favor_pct=("delta_a_favor", lambda v: 100 * np.mean(v.astype(float))),
                                    ofi_a_favor_pct=("ofi_a_favor", lambda v: 100 * np.mean(v.astype(float))),
                                    ofi_pas_a_favor_pct=("ofi_pas_a_favor", lambda v: 100 * np.mean(v.astype(float))),
                                    delta_vol_med=("delta_vol", lambda v: np.nanmedian(np.abs(v))),
                                    pts_1000_med=("pts_por_1000_netos", "median"), pts_raiz_med=("pts_por_raiz_vol", "median"),
                                    apil_favor_10=("apil_favor_10pts", "mean"), apil_contra=("apil_contra", "mean"),
                                    big_neto_favor_med=("big_vol_neto_favor", "median"), frac_vacio_med=("frac_vacio", "median"),
                                    ratio_seg_vacio_med=("ratio_vacio", "median"), abs_contra_por_pierna=("abs_contra_n", "mean"),
                                    fallan_pct=("fallo_abs", lambda v: 100 * np.nanmean(v)),
                                    abs_ep=("abs_contra_n", "sum"), abs_siguio=("abs_contra_fallidas", "sum"),
                                    ctrl_n=("ctrl_seg_n", "sum"), ctrl_siguio=("ctrl_seg_siguio", "sum"))
    agg["abs_siguio_%"] = 100 * agg["abs_siguio"] / agg["abs_ep"].replace(0, np.nan)
    agg["ctrl_siguio_%"] = 100 * agg["ctrl_siguio"] / agg["ctrl_n"].replace(0, np.nan)
    tot = Y.agg({"pts": "size"})
    return agg, Y


def main():
    carpeta = sys.argv[1]; pedidos = [(sys.argv[k], sys.argv[k + 1]) for k in range(2, len(sys.argv) - 1, 2)]
    os.makedirs(carpeta, exist_ok=True)
    P, PR = cargar(pedidos)
    pd.set_option("display.width", 250); pd.set_option("display.max_columns", 40); pd.set_option("display.max_colwidth", 60)
    print("ruedas:", ", ".join(sorted(P["rueda"].unique())))
    print(P.groupby(["escala", "clase"]).size().unstack())
    for esc in ("X", "X/2"):
        E = enriquecimiento(P, esc); E.to_csv(os.path.join(carpeta, "enriquecimiento_%s.csv" % esc.replace("/", "_")), index=False)
        print("\n==== ENRIQUECIMIENTO escala %s (giros contra pausas y contra momentos al azar; dif_pp_por_dia = giro - pausa por rueda)" % esc)
        print(E.drop(columns=["tipo"]).round(2).to_string(index=False))
        K = continuos(P, esc); K.to_csv(os.path.join(carpeta, "continuos_%s.csv" % esc.replace("/", "_")), index=False)
        print("\n---- continuos escala %s" % esc); print(K.round(3).to_string(index=False))
    E4 = enriquecimiento(P, "4X"); E4.to_csv(os.path.join(carpeta, "enriquecimiento_4X.csv"), index=False)
    print("\n==== ENRIQUECIMIENTO escala 4X (movimientos principales; n chico, descriptivo)")
    print(E4[["factor", "giros_%", "n_giros", "pausas_%", "n_pausas", "azar_%", "RR_vs_pausa", "IC95_lo", "IC95_hi", "dif_pp_por_dia"]].round(2).to_string(index=False))
    Cb = combinaciones(P, "X"); Cb.to_csv(os.path.join(carpeta, "combinaciones_X.csv"), index=False)
    print("\n==== COMBINACIONES (escala X; giros y pausas; en lugar importante y fuera)"); print(Cb.round(1).to_string(index=False))
    for esc in ("X", "X/2"):
        Mo = modelo(P, esc)
        for nom, m in Mo.items():
            print("\n==== MODELO giro contra pausa, escala %s, %s: n %d (giros %d) | AUC dejando una rueda afuera: logistica %.3f, arbol %.3f"
                  % (esc, nom, m["n"], m["giros"], m["auc_lodo_logistica"], m["auc_lodo_arbol"]))
            print("coeficientes (estandarizados):", ", ".join("%s %+.2f" % (k, v) for k, v in m["coef"].items()))
            print("importancia del arbol:", ", ".join("%s %.2f" % (k, v) for k, v in m["importancia_arbol"].items() if v > 0))
            if esc == "X": print(m["arbol"])
    # tendencia contra rango (escala X): proporciones en giros y pausas por tipo de dia
    X = P[(P["escala"] == "X") & (P["clase"].isin(["giro", "pausa"]))]
    T = X.groupby(["tipo_dia", "clase"])[CLAVE + ["L_IMPORTANTE", "L_VWAP", "VOL", "EMPUJE"]].mean().mul(100).round(0)
    T.to_csv(os.path.join(carpeta, "por_tipo_de_dia.csv")); print("\n==== POR TIPO DE DIA (% de puntos con el factor, escala X)"); print(T.to_string())
    for esc in ("4X", "X", "X/2"):
        agg, Y = piernas(PR, esc); agg.to_csv(os.path.join(carpeta, "piernas_%s.csv" % esc.replace("/", "_")))
        print("\n==== PIERNAS escala %s (sin la ultima sin cerrar)" % esc); print(agg.round(2).T.to_string())
    # franjas: giros por franja contra pausas
    F = P[(P["escala"] == "X") & (P["clase"].isin(["giro", "pausa"]))].groupby(["franja", "clase"]).size().unstack(fill_value=0)
    print("\n==== giros y pausas por franja (escala X)"); print(F.to_string())


if __name__ == "__main__":
    main()
