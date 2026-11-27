# -*- coding: utf-8 -*-
"""RONDA 23, paso 5: MECANICA Y ROBUSTEZ (POST HOC; se escribio DESPUES de ver el chequeo y la cinta; nada de esto cambia las candidatas).
Con el motor de 5 min en las TRES muestras (diseno y chequeo con velas de MES; cinta con velas de ES armadas desde la cinta):
 (1) correccion a la R22: el elastico de TODO el dia contra la referencia al azar bien hecha (mismo cierre, VWAP que se mueve) y contra
     la formula; y por hora;
 (2) el vecindario de las geometrias de la familia A en las tres muestras (cuantas variantes pasan la vara en cada una);
 (3) ablaciones de A3 (sacar la hora, la llave del IB, la condicion geometrica, el corte de distancia) -> que pieza pesa;
 (4) A3 por trimestre / mes, sin dias de la Fed, por motivo de salida, por hora de entrada; cola de perdidas;
 (5) el borde contra el azar en los puntos operados, con error agrupado por rueda.
Uso: python -I r23_05_posthoc.py -> salidas/posthoc.txt (+ registro_variantes.csv con etapa 'posthoc')"""
import json, os, pickle, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import r23_comun as K
import r23_sim as S
import r23_03_variantes as V

L = []
FED = {"2025-09-17", "2025-10-29", "2025-12-10", "2026-01-28", "2026-03-18", "2026-04-29", "2026-06-17", "2026-07-29", "2026-09-16", "2026-10-28"}


def W(s=""):
    print(s, flush=True); L.append(s)


def se_rueda(dias, x):
    df = pd.DataFrame({"d": dias, "x": x}); g = df.groupby("d")["x"].agg(["sum", "size"])
    n = g["size"].sum(); m = g["sum"].sum() / n
    if len(g) < 2: return m, np.nan
    r = g["sum"] - m * g["size"]
    return m, float(np.sqrt((r ** 2).sum() * len(g) / (len(g) - 1)) / n)


def muestras():
    B = pickle.load(open(os.path.join(K.SCR, "ruedas5.pkl"), "rb"))["ruedas"]
    P = pd.read_parquet(os.path.join(K.SCR, "puntos5.parquet"))
    Bc = pickle.load(open(os.path.join(K.SCR, "ruedas5_cinta.pkl"), "rb"))["ruedas"]
    Pc = pd.read_parquet(os.path.join(K.SCR, "puntos5_cinta.parquet"))
    out = {}
    for m in ("diseno", "chequeo"):
        out[m] = (B, P[P.muestra == m].copy())
    out["cinta"] = (Bc, Pc.copy())
    return out


def sim(ru, pts, br, nombre_cache):
    f = os.path.join(K.SCR, "post_%s.parquet" % nombre_cache)
    if os.path.exists(f): return pd.read_parquet(f)
    d = pts[(pts.s != 0) & (pts.min_dia >= 600) & (pts.min_dia <= 930)]
    r = S.correr(ru, d, V.BRACKETS[br], M=200); r.to_parquet(f, index=False); return r


def main():
    t0 = time.time(); K.frenar_si_falta_ram(2.0)
    M = muestras()
    W("RONDA 23 - POST HOC: MECANICA Y ROBUSTEZ %s" % time.strftime("%Y-%m-%d %H:%M"))
    sims = {}
    for m, (ru, pts) in M.items():
        for br in ("F1", "F3", "F3m125", "F3m200"):
            sims[(m, br)] = sim(ru, pts, br, "%s_%s" % (m, br))
    # ---------------------------------------------------------------- (1) correccion a la R22
    W(""); W("(1) EL ELASTICO DE TODO EL DIA (bracket F1: stop X/2 detras del extremo, objetivo el VWAP), punto por punto, 10:00-15:30")
    for m in M:
        r = sims[(m, "F1")].copy()
        r["stopea"] = r["mot_real"].isin([1, 3])
        dstop = (r["stop"] - r["c"]).abs(); r["Pf"] = dstop / (dstop + (r["c"] - r["vw"]).abs())
        b, se = se_rueda(r["dia"], (r["pts_real"] - r["azar_pts"]).to_numpy())
        W("  %-8s n %5d (%3d r) | llega antes al extremo+X/2 (stop): real %.1f %% | gana real %.1f %% contra azar %.1f %% y formula %.1f %% | pts real %+.2f azar %+.2f | borde %+.2f (ee %.2f)" % (
            m, len(r), r.dia.nunique(), 100 * r.stopea.mean(), 100 * (r.pts_real > 0).mean(), 100 * r.azar_gana.mean(), 100 * r.Pf.mean(), r.pts_real.mean(), r.azar_pts.mean(), b, se))
        for a_, b_, nom in ((600, 660, "10:00-11:00"), (661, 750, "11:05-12:30"), (751, 780, "12:35-13:00"), (781, 840, "13:05-14:00"), (841, 930, "14:05-15:30")):
            s = r[(r.min_dia >= a_) & (r.min_dia <= b_)]
            bb, se2 = se_rueda(s["dia"], (s["pts_real"] - s["azar_pts"]).to_numpy())
            W("       %s gana real %5.1f %% azar %5.1f %% (%+5.1f) | borde %+5.2f (ee %.2f) n %d" % (nom, 100 * (s.pts_real > 0).mean(), 100 * s.azar_gana.mean(),
                                                                                    100 * ((s.pts_real > 0).mean() - s.azar_gana.mean()), bb, se2, len(s)))
    W("  (la R22 comparo contra la formula sin el cierre de la rueda ni el VWAP que se mueve; aca la referencia es el mismo camino reflejado al azar)")
    # ---------------------------------------------------------------- (2) vecindario en las tres muestras
    W(""); W("(2) VECINDARIO DE LA FAMILIA A EN LAS TRES MUESTRAS (cuantas de las 216 variantes pasan: acierto >= 60 %% y neto > 0 con costo 1,00)")
    VV = V.vecindario()
    tab = []
    for m, (ru, pts) in M.items():
        dias = sorted(pts["dia"].unique())
        for v in VV:
            tk = V.evaluar(sims[(m, v["bracket"])], v, dias); r = V.resumen(tk, dias)
            tab.append(dict(muestra=m, variante=v["nombre"], geo="F1pf" if v["bracket"] == "F1" else "F3", n=r["n"], acierto=r.get("acierto_pct", np.nan),
                            azar=r.get("acierto_azar_pct", np.nan), neto=r.get("neto_op_c100", np.nan), t=r.get("t_rueda_c100", np.nan),
                            pasa=int(r.get("acierto_pct", 0) >= 60 and r.get("neto_op_c100", -9) > 0)))
    T = pd.DataFrame(tab)
    for g, x in T.groupby("geo"):
        piv = x.pivot_table(index="variante", columns="muestra", values="pasa")
        W("  %s: pasan en diseno %d, en chequeo %d, en cinta %d, en las tres %d (de %d) | neto medio diseno %+.2f chequeo %+.2f cinta %+.2f | acierto medio %.1f / %.1f / %.1f (azar %.1f / %.1f / %.1f)" % (
            g, piv["diseno"].sum(), piv["chequeo"].sum(), piv["cinta"].sum(), int((piv.sum(axis=1) == 3).sum()), len(piv),
            x[x.muestra == "diseno"].neto.mean(), x[x.muestra == "chequeo"].neto.mean(), x[x.muestra == "cinta"].neto.mean(),
            x[x.muestra == "diseno"].acierto.mean(), x[x.muestra == "chequeo"].acierto.mean(), x[x.muestra == "cinta"].acierto.mean(),
            x[x.muestra == "diseno"].azar.mean(), x[x.muestra == "chequeo"].azar.mean(), x[x.muestra == "cinta"].azar.mean()))
    T.to_csv(os.path.join(K.SAL, "posthoc_vecindario_3muestras.csv"), index=False)
    # ---------------------------------------------------------------- (3) ablaciones de A3
    W(""); W("(3) ABLACIONES DE A3 (13:00-15:30, VWAP a >= 1,5 X, IB <= 1, P geometrica >= 0,55, stop X/2 detras del extremo)")
    A3 = dict(familia="A", desde="13:00", hasta="15:30", loc=("dv", 1.5), llave=("ib<=1", "Pf>=0.55"), bracket="F1")
    abl = {"A3 completa": A3,
           "sin la hora (10:00-15:30)": dict(A3, desde="10:00"),
           "solo la manana (10:00-12:55)": dict(A3, desde="10:00", hasta="12:55"),
           "sin la llave del IB": dict(A3, llave=("Pf>=0.55",)),
           "solo dias de IB extendido > 1 (la llave al reves)": dict(A3, llave=("ib>1", "Pf>=0.55")),
           "sin la condicion geometrica (stop natural)": dict(A3, llave=("ib<=1",)),
           "sin el corte de distancia (dv >= 0)": dict(A3, loc=("dv", 0.0)),
           "distancia >= 3 X": dict(A3, loc=("dv", 3.0))}
    for nom, v in abl.items():
        fila = [nom]
        for m, (ru, pts) in M.items():
            dias = sorted(pts["dia"].unique())
            tk = V.evaluar(sims[(m, "F1")], v, dias); r = V.resumen(tk, dias)
            fila.append("%s n %3d (%.2f/r) %5.1f %% (azar %5.1f) %+6.2f" % (m, r["n"], r["ops_por_rueda"], r.get("acierto_pct", np.nan), r.get("acierto_azar_pct", np.nan), r.get("neto_op_c100", np.nan)))
            K.anotar(dict(n_var=K.siguiente_n_var(), etapa="posthoc", familia="A_ablacion", variante="A3 " + nom, muestra=m, parametros=json.dumps({k: v[k] for k in ("desde", "hasta", "loc", "llave", "bracket")}),
                          **{k: r.get(k, "") for k in ("ruedas", "n", "ops_por_rueda", "acierto_pct", "acierto_azar_pct", "P_formula_pct", "bruto_op", "neto_op_c100", "neto_azar_c100", "t_rueda_c100", "ruedas_pos_c100", "R_medio", "objetivo_medio")},
                          pasa_vara=int(r.get("acierto_pct", 0) >= 60 and r.get("neto_op_c100", -9) > 0), nota="ablacion post hoc (no decide)"))
        W("  %-48s | %s" % (fila[0], " | ".join(fila[1:])))
    # ---------------------------------------------------------------- (4) A3 por tramos (con el motor de 5 min; la cinta exacta esta en cinta.txt)
    W(""); W("(4) A3 POR TRAMOS (motor de 5 min)")
    todos = []
    for m, (ru, pts) in M.items():
        dias = sorted(pts["dia"].unique())
        tk = V.evaluar(sims[(m, "F1")], A3, dias).copy(); tk["muestra"] = m; todos.append(tk)
        r = V.resumen(tk, dias)
        rf = V.resumen(tk[~tk.dia.isin(FED)], [d for d in dias if d not in FED])
        W("  %-8s total n %d %.1f %% %+.2f | sin dias de la Fed: n %d %.1f %% %+.2f" % (m, r["n"], r["acierto_pct"], r["neto_op_c100"], rf["n"], rf.get("acierto_pct", np.nan), rf.get("neto_op_c100", np.nan)))
    A = pd.concat(todos); A["trim"] = A.dia.str[:4] + "T" + ((A.dia.str[5:7].astype(int) - 1) // 3 + 1).astype(str)
    A["gana"] = A.pts_real > 0; A["net"] = A.pts_real - 1.0
    for q, g in A.groupby("trim"):
        W("     %s n %3d acierto %5.1f %% (azar %5.1f) neto %+6.2f | ruedas %d" % (q, len(g), 100 * g.gana.mean(), 100 * g.azar_gana.mean(), g.net.mean(), g.dia.nunique()))
    A["mot"] = A.mot_real.map({0: "fin 16:00", 1: "stop", 2: "objetivo VWAP", 3: "stop con hueco"})
    for mo, g in A.groupby("mot"):
        W("     salida %-15s n %3d (%4.1f %%) pts medio %+6.2f" % (mo, len(g), 100 * len(g) / len(A), g.pts_real.mean()))
    A["hb"] = pd.cut(A.min_dia, [779, 840, 900, 931], labels=["13:00-14:00", "14:05-15:00", "15:05-15:30"])
    for h, g in A.groupby("hb", observed=True):
        W("     entrada %s n %3d acierto %5.1f %% (azar %5.1f) neto %+6.2f" % (h, len(g), 100 * g.gana.mean(), 100 * g.azar_gana.mean(), g.net.mean()))
    W("     peor operacion %+.2f | percentil 5 %+.2f | ganancia media %+.2f | perdida media %+.2f | MFE medio de las perdedoras %.2f pts" % (
        A.pts_real.min(), A.pts_real.quantile(0.05), A[A.gana].pts_real.mean(), A[~A.gana].pts_real.mean(), A[~A.gana].mfe.mean()))
    # ---------------------------------------------------------------- (5) el borde contra el azar en los puntos operados
    W(""); W("(5) BORDE CONTRA EL AZAR EN LOS PUNTOS OPERADOS (A1, A2, A3; motor de 5 min; error agrupado por rueda)")
    defs = {"A1": dict(familia="A", desde="13:30", hasta="15:30", loc=("dv", 2.0), llave=("ib<=1", "Pf>=0.55"), bracket="F1"),
            "A2": dict(familia="A", desde="14:00", hasta="15:30", loc=("dv", 2.0), llave=("ib<=1.5",), bracket="F3"), "A3": A3}
    for cid, v in defs.items():
        fila = []
        for m, (ru, pts) in M.items():
            dias = sorted(pts["dia"].unique())
            tk = V.evaluar(sims[(m, v["bracket"])], v, dias)
            b, se = se_rueda(tk["dia"], (tk["pts_real"] - tk["azar_pts"]).to_numpy())
            dg = 100 * ((tk.pts_real > 0).mean() - tk.azar_gana.mean())
            fila.append("%s %+5.2f pts (ee %.2f, t %.1f) %+5.1f pp" % (m, b, se, b / se if se else np.nan, dg))
        W("  %s: %s" % (cid, " | ".join(fila)))
    open(os.path.join(K.SAL, "posthoc.txt"), "w", encoding="utf-8").write("\n".join(L))
    W("%.1f s" % (time.time() - t0))


if __name__ == "__main__":
    main()
