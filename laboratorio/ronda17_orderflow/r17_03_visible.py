# -*- coding: utf-8 -*-
"""RONDA 17, paso 3 (Enmiendas 2 y 3, post-hoc): ¿la FIRMA del giro se puede LEER mientras pasa, o es consecuencia del giro?
Candidatos = todos los extremos con retroceso >= X/4 (pivotes de un zigzag X/4). Desenlace = retroceso maximo antes de que el
precio supere el extremo: >= X GIRO, >= X/2 PAUSA, si no MENOR. En tau = 15, 30 y 60 s despues del extremo, solo candidatos que
siguen en pie (no superados) y todavia sin confirmar (retroceso < X), se miden con datos hasta tau: agotamiento, subasta,
ordenes grandes en el extremo, delta de la reaccion y volumen previo; y se compara P(GIRO) con y sin cada rasgo DENTRO del
mismo tramo de retroceso ya hecho (0-25 %, 25-50 %, 50-100 % de X).
Uso: python -I r17_03_visible.py <carpeta> inst dia [inst dia ...]"""
import json, os, sys, warnings
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import r17_comun as C
import r17_01_anatomia as A1
warnings.filterwarnings("ignore")
TAUS = (15, 30, 60)


def candidatos(A, X):
    giros, _, _, _ = A1.zigzag(A["alto"], A["bajo"], A["ult"], X / 4)
    out = []
    n = len(A["alto"])
    for (i, p, s, ic) in giros:
        if i < 30:
            continue
        sig = A["alto"][i + 1:] > p + 1e-9 if s > 0 else A["bajo"][i + 1:] < p - 1e-9
        j = i + 1 + int(np.argmax(sig)) if sig.any() else n
        seg = A["bajo"][i + 1:j] if s > 0 else A["alto"][i + 1:j]
        retr = (p - seg.min()) if (s > 0 and len(seg)) else ((seg.max() - p) if len(seg) else 0.0)
        out.append((i, p, s, j, retr))
    return out


def rasgos_tau(i, p, s, tau, A, fp_i, fp, G, U, q99):
    e = i + tau
    tk = int(round(p / C.TICK))
    lo_i, hi_i = np.searchsorted(fp_i, [i - 120, e + 1]); w = fp.iloc[lo_i:hi_i]
    tot = (w["compra"] + w["venta"]).groupby(w["tick"]).sum()
    v_ext = float(tot.get(tk, 0.0)); v_in = float(np.mean([tot.get(tk - s * k, 0.0) for k in range(1, 5)]))
    m0 = (i // 60) * 60; m1 = min(e, m0 + 59)
    lo2, hi2 = np.searchsorted(fp_i, [m0, m1 + 1]); w2 = fp.iloc[lo2:hi2]; w2 = w2[w2["tick"] == tk]
    c_e = float(w2["compra"].sum()); v_e = float(w2["venta"].sum())
    cero = v_e if s > 0 else c_e; otro = c_e if s > 0 else v_e
    sub = "SIN_TERMINAR" if (c_e > 1e-9 and v_e > 1e-9) else ("TERMINADA" if (cero <= 1e-9 and otro > 1e-9) else "OTRA")
    gi = G["i"].to_numpy(); sel = (gi >= i - 60) & (gi <= e) & (G["pmax"].to_numpy() >= p - 0.5) & (G["pmin"].to_numpy() <= p + 0.5) & (G["vol"].to_numpy() >= q99)
    ld = G["lado"].to_numpy()
    d_reac = s * A["delta"][i + 1:e + 1].sum()
    o_reac = s * A["ofi"][i + 1:e + 1].sum(); op_reac = s * A["ofi_pas"][i + 1:e + 1].sum()
    b = min(12, i // 1800)
    return {"AGOT": bool(v_in > 0 and v_ext <= 0.25 * v_in), "agot_ratio": v_ext / v_in if v_in > 0 else np.nan,
            "SUB_TERMINADA": sub == "TERMINADA", "SUB_SIN": sub == "SIN_TERMINAR",
            "BIG_FAVOR": bool((sel & (ld == s)).any()), "BIG_CONTRA": bool((sel & (ld == -s)).any()),
            "REAC_CONTRA": bool(d_reac < 0), "d_reac": d_reac, "REAC_OFI": bool(o_reac < 0), "REAC_OFI_PAS": bool(op_reac < 0), "VOL_PRE": bool(A["vol60"][i] >= U[b]["q90_vol"]),
            "ABS_PRE": bool(A[("absa60" if s > 0 else "absb60")][i] > 0 and A[("absa60" if s > 0 else "absb60")][i] >= U[b]["q90_absa" if s > 0 else "q90_absb"]),
            "EMPUJE_PRE": bool(s * A["del60"][i] >= U[b]["q90_del"])}


def procesar(inst, dia, diaria, cuant):
    X, _, _ = A1.escala_X(dia, diaria)
    A = A1.arreglos(inst, dia); t0 = A["t0"]; U = A1.umbrales_franja(A)
    fp, _ = A1.footprint(inst, dia, t0); fp_i = fp["i"].to_numpy()
    q = cuant["%s %s" % (inst, dia)]; G = A1.grandes(inst, dia, t0, q)
    filas = []
    for (i, p, s, j, retr) in candidatos(A, X):
        des = "GIRO" if retr >= X - 1e-9 else ("PAUSA" if retr >= X / 2 - 1e-9 else "MENOR")
        for tau in TAUS:
            e = i + tau
            if e >= len(A["alto"]) or j <= e:          # superado antes de tau: el operador ya vio un extremo nuevo
                continue
            seg = A["bajo"][i + 1:e + 1] if s > 0 else A["alto"][i + 1:e + 1]
            hecho = (p - seg.min()) if s > 0 else (seg.max() - p)
            if hecho >= X - 1e-9:                    # ya confirmado en tau: no hay nada que leer
                continue
            f = rasgos_tau(i, p, s, tau, A, fp_i, fp, G, U, q["q99"])
            f.update({"inst": inst, "dia": dia, "rueda": inst + " " + dia, "i": i, "tipo": s, "tau": tau, "desenlace": des,
                      "GIRO": des == "GIRO", "retr_final": retr / X, "hecho_tau": hecho / X,
                      "tramo": "0-25" if hecho < X / 4 else ("25-50" if hecho < X / 2 else "50-100"), "X": X})
            filas.append(f)
    return pd.DataFrame(filas)


def mh(df, f):
    """Riesgo relativo de Mantel-Haenszel de GIRO con/sin el rasgo, estratificando por rueda x tramo; y diferencias por rueda."""
    num = den = 0.0; dif = []
    for (r, t), g in df.groupby(["rueda", "tramo"]):
        a = g.loc[g[f], "GIRO"].sum(); n1 = g[f].sum(); c = g.loc[~g[f], "GIRO"].sum(); n0 = (~g[f]).sum(); N = len(g)
        if n1 == 0 or n0 == 0:
            continue
        num += a * n0 / N; den += c * n1 / N
    for r, g in df.groupby("rueda"):
        if g[f].sum() >= 5 and (~g[f]).sum() >= 5:
            dif.append(100 * (g.loc[g[f], "GIRO"].mean() - g.loc[~g[f], "GIRO"].mean()))
    return (num / den if den > 0 else np.nan), dif


def boot_mh(df, f, reps=400, seed=17):
    rng = np.random.default_rng(seed); ruedas = df["rueda"].unique(); out = []
    grupos = {r: g for r, g in df.groupby("rueda")}
    for _ in range(reps):
        m = pd.concat([grupos[r] for r in rng.choice(ruedas, len(ruedas))], ignore_index=True)
        v, _ = mh(m, f); out.append(v)
    out = np.array([x for x in out if np.isfinite(x)])
    return (np.percentile(out, 5), np.percentile(out, 95)) if len(out) > 20 else (np.nan, np.nan)


def main():
    carpeta = sys.argv[1]; pedidos = [(sys.argv[k], sys.argv[k + 1]) for k in range(2, len(sys.argv) - 1, 2)]
    diaria = A1.tabla_diaria(); cuant = json.load(open(os.path.join(C.R17, "cuantiles_ordenes.json")))
    D = pd.concat([procesar(inst, dia, diaria, cuant) for inst, dia in pedidos], ignore_index=True)
    D.to_parquet(os.path.join(carpeta, "visible_tau.parquet"))
    pd.set_option("display.width", 250)
    for tau in TAUS:
        T = D[D["tau"] == tau]
        print("\n==== tau = %d s: candidatos en pie y sin confirmar = %d (GIRO %d, PAUSA %d, MENOR %d)" % (tau, len(T), (T["desenlace"] == "GIRO").sum(),
              (T["desenlace"] == "PAUSA").sum(), (T["desenlace"] == "MENOR").sum()))
        base = T.groupby("tramo")["GIRO"].agg(["mean", "size"]); print("P(GIRO) de base por tramo de retroceso ya hecho:", ", ".join("%s: %.1f %% (n %d)" % (k, 100 * v["mean"], v["size"]) for k, v in base.iterrows()))
        filas = []
        for f in ("AGOT", "SUB_TERMINADA", "SUB_SIN", "BIG_FAVOR", "BIG_CONTRA", "REAC_CONTRA", "REAC_OFI", "REAC_OFI_PAS", "VOL_PRE", "ABS_PRE", "EMPUJE_PRE"):
            rr, dif = mh(T, f); lo, hi = boot_mh(T, f)
            filas.append({"rasgo": f, "con_%": 100 * T[f].mean(), "P(GIRO|con)": 100 * T.loc[T[f], "GIRO"].mean(), "P(GIRO|sin)": 100 * T.loc[~T[f], "GIRO"].mean(),
                          "RR_MH": rr, "IC90_ruedas": "%.2f-%.2f" % (lo, hi), "ruedas_arriba": int(np.sum(np.array(dif) > 0)), "ruedas": len(dif),
                          "dif_pp_por_rueda": " ".join("%+.0f" % v for v in dif)})
        print(pd.DataFrame(filas).round(2).to_string(index=False))
        # combinacion de lectura: agotamiento + subasta terminada + reaccion en contra
        T = T.copy(); T["FIRMA"] = T["AGOT"] & T["SUB_TERMINADA"] & T["REAC_CONTRA"]
        rr, dif = mh(T, "FIRMA"); lo, hi = boot_mh(T, "FIRMA")
        print("FIRMA (AGOT + SUB_TERMINADA + REAC_CONTRA): con %.1f %% | P(GIRO|con) %.1f %% vs sin %.1f %% | RR_MH %.2f (IC90 %.2f-%.2f) | ruedas arriba %d de %d"
              % (100 * T["FIRMA"].mean(), 100 * T.loc[T["FIRMA"], "GIRO"].mean(), 100 * T.loc[~T["FIRMA"], "GIRO"].mean(), rr, lo, hi, np.sum(np.array(dif) > 0), len(dif)))
        for tr, g in T.groupby("tramo"):
            print("   tramo %s: P(GIRO|FIRMA) %.1f %% (n %d) vs sin %.1f %% (n %d)" % (tr, 100 * g.loc[g["FIRMA"], "GIRO"].mean(), g["FIRMA"].sum(),
                  100 * g.loc[~g["FIRMA"], "GIRO"].mean(), (~g["FIRMA"]).sum()))


if __name__ == "__main__":
    main()
