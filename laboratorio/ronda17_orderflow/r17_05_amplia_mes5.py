# -*- coding: utf-8 -*-
"""RONDA 17, tanda 3 (amplia, PREREGISTRO seccion 1): velas de MES de 5 min con delta (276 sesiones, 2025-08-21 a 2026-09-21,
dom/ronda3/velas/velas-MES-m5.csv). Solo lo que no necesita footprint por precio:
  - piernas grandes (zigzag 4X y 2X sobre el camino o-l-h-c / o-h-l-c de cada vela): % con delta a favor;
  - giros contra pausas: lugar (ayer H/L/C/POC/VAH/VAL, noche, IB desde 10:30, VWAP y +-1/+-2 desvios, redondo de 50, semana y
    mes anteriores) con cociente de Katz, en 4 bloques cronologicos.
El continuo de ATAS cambia de contrato en el roll sin ajustar: se anulan los niveles de AYER del dia siguiente al roll y los de
SEMANA y MES cuando el periodo anterior cruza un roll. Perfil de las velas = volumen repartido parejo entre minimo y maximo.
Uso: python -I r17_05_amplia_mes5.py"""
import os, sys, warnings
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import r17_comun as C
import r17_01_anatomia as A1
warnings.filterwarnings("ignore")
ARCH = os.path.join(C.LAB, "dom", "ronda3", "velas", "velas-MES-m5.csv")
ROLLS = [pd.Timestamp(x) for x in ("2025-09-11", "2025-12-11", "2026-03-12", "2026-06-11", "2026-09-10")]
SALIDA = os.path.join(C.AQUI, "tanda3")


def cargar():
    v = pd.read_csv(ARCH, usecols=["t", "o", "h", "l", "c", "vol", "delta"])
    v["t"] = pd.to_datetime(v["t"]).dt.tz_localize("UTC").dt.tz_convert(C.NY).dt.tz_localize(None)
    v = v.sort_values("t").drop_duplicates("t").reset_index(drop=True)
    # dia de la sesion CME: desde las 18:00 NY es el dia siguiente
    v["dia"] = (v["t"] + pd.Timedelta(hours=6)).dt.normalize()
    v = v[v["dia"].dt.weekday < 5]
    return v


def perfil(g):
    tk_lo = np.rint(g["l"].to_numpy() / C.TICK).astype(int); tk_hi = np.rint(g["h"].to_numpy() / C.TICK).astype(int); vol = g["vol"].to_numpy("float64")
    n = tk_hi - tk_lo + 1; rep = np.repeat(np.arange(len(g)), n); off = np.arange(len(rep)) - np.repeat(np.cumsum(n) - n, n)
    return C.perfil_va(tk_lo[rep] + off, vol[rep] / n[rep])


def niveles_dias(v):
    filas = []
    for d, g in v.groupby("dia"):
        hm = g["t"].dt.strftime("%H:%M")
        r = g[(g["t"].dt.normalize() == d) & (hm >= "09:30") & (hm < "16:00")]
        if len(r) < 70:
            continue
        on = g[g["t"] < d + pd.Timedelta(hours=9, minutes=30)]; ib = r[r["t"] < d + pd.Timedelta(hours=10, minutes=30)]
        poc, vah, val = perfil(r)
        filas.append({"dia": d, "o": r["o"].iloc[0], "h": r["h"].max(), "l": r["l"].min(), "c": r["c"].iloc[-1], "poc": poc, "vah": vah, "val": val,
                      "on_h": on["h"].max() if len(on) else np.nan, "on_l": on["l"].min() if len(on) else np.nan, "ib_h": ib["h"].max(), "ib_l": ib["l"].min()})
    return pd.DataFrame(filas).set_index("dia")


def periodo(v, dias):
    g = v[v["dia"].isin(dias)]
    if len(g) == 0:
        return {}
    poc, vah, val = perfil(g)
    ult = g[g["dia"] == max(dias)]; r = ult[ult["t"].dt.strftime("%H:%M") < "16:00"]
    return {"H": g["h"].max(), "L": g["l"].min(), "C": (r["c"].iloc[-1] if len(r) else np.nan), "POC": poc, "VAH": vah, "VAL": val}


def cruza_roll(dias):
    return any(min(dias) <= r <= max(dias) for r in ROLLS) if dias else True


def camino(r):
    """Precios en orden supuesto dentro de cada vela: alcista o-l-h-c, bajista o-h-l-c. Devuelve precios e indice de vela."""
    o, h, l, c = (r[k].to_numpy() for k in ("o", "h", "l", "c"))
    sube = c >= o
    p = np.column_stack([o, np.where(sube, l, h), np.where(sube, h, l), c]).ravel()
    return p, np.repeat(np.arange(len(r)), 4)


def main():
    os.makedirs(SALIDA, exist_ok=True)
    v = cargar(); N = niveles_dias(v); dias = list(N.index)
    N["rango"] = N["h"] - N["l"]
    print("sesiones con rueda completa:", len(dias), dias[0].date(), "->", dias[-1].date(), flush=True)
    pts, legs = [], []
    for k, d in enumerate(dias):
        if k < 10:
            continue
        X = max(4.0, round(0.08 * N["rango"].iloc[k - 10:k].median() / C.TICK) * C.TICK)
        ayer = dias[k - 1]
        roll_ayer = any(ayer < r <= d for r in ROLLS)
        L = {"AYER": {} if roll_ayer else {"PDH": N.at[ayer, "h"], "PDL": N.at[ayer, "l"], "PDC": N.at[ayer, "c"], "PPOC": N.at[ayer, "poc"],
                                            "PVAH": N.at[ayer, "vah"], "PVAL": N.at[ayer, "val"]},
             "NOCHE": {"ONH": N.at[d, "on_h"], "ONL": N.at[d, "on_l"]}, "IB": {"IBH": N.at[d, "ib_h"], "IBL": N.at[d, "ib_l"]}}
        lunes = d - pd.Timedelta(days=d.weekday()); sem = [x for x in dias if lunes - pd.Timedelta(days=7) <= x < lunes]
        L["SEMANA"] = {} if (cruza_roll(sem) or any(lunes <= r <= d for r in ROLLS)) else {"W" + a: b for a, b in periodo(v, sem).items()}
        m0 = (d.replace(day=1) - pd.Timedelta(days=1)).replace(day=1); mes = [x for x in dias if m0 <= x < d.replace(day=1)]
        L["MES"] = {} if (cruza_roll(mes) or any(d.replace(day=1) <= r <= d for r in ROLLS)) else {"M" + a: b for a, b in periodo(v, mes).items()}
        L = {a: {x: float(y) for x, y in b.items() if np.isfinite(y)} for a, b in L.items()}
        g = v[v["dia"] == d].reset_index(drop=True)
        tp = (g["h"] + g["l"] + g["c"]) / 3; cv = g["vol"].cumsum()
        vw = (tp * g["vol"]).cumsum() / cv; sd = np.sqrt(((tp ** 2) * g["vol"]).cumsum() / cv - vw ** 2).clip(lower=0)
        hm = g["t"].dt.strftime("%H:%M"); rr = g[(g["t"].dt.normalize() == d) & (hm >= "09:30") & (hm < "16:00")]
        idx = rr.index.to_numpy(); p, vi = camino(rr)
        e = abs(N.at[d, "c"] - N.at[d, "o"]) / N.at[d, "rango"]
        tipo = "TENDENCIA" if e >= 0.55 else ("RANGO" if e <= 0.25 else "MIXTA")
        for esc, XX in (("2X", 2 * X), ("4X", 4 * X)):
            giros, pausas, est, final = A1.zigzag(p, p, p, XX)
            dl = rr["delta"].to_numpy()
            pv = [(gq[0], gq[1], gq[2]) for gq in giros] + [(final[0], final[1], est)]
            for a, b in zip(pv[:-1], pv[1:]):
                k0, k1 = vi[a[0]], vi[b[0]]; s = 1 if b[1] > a[1] else -1
                dsum = dl[k0 + 1:k1 + 1].sum() if k1 > k0 else dl[k0]
                legs.append({"dia": d, "escala": esc, "tipo_dia": tipo, "pts": abs(b[1] - a[1]), "velas": k1 - k0, "delta": dsum, "a_favor": s * dsum > 0})
            for clase, lst in (("giro", [(gq[0], gq[1], gq[2]) for gq in giros]), ("pausa", pausas)):
                for (ip, pp, s) in lst:
                    kv = vi[ip]; j = idx[kv]
                    if kv == 0 and clase == "giro":
                        continue
                    f = {"dia": d, "escala": esc, "clase": clase, "tipo": s, "tipo_dia": tipo, "hora": rr["t"].iloc[kv].strftime("%H:%M")}
                    for cl, dd in L.items():
                        if cl == "IB" and rr["t"].iloc[kv] < d + pd.Timedelta(hours=10, minutes=30):
                            f["L_IB"] = np.nan; continue
                        f["L_" + cl] = float(any(abs(pp - y) <= 1.5 for y in dd.values())) if dd else np.nan
                    f["L_REDONDO"] = float(abs(pp - round(pp / 50) * 50) <= 1.5)
                    vws = [vw.iloc[j] + q * sd.iloc[j] for q in (-2, -1, 0, 1, 2)]
                    f["L_VWAP"] = float(any(abs(pp - y) <= 1.5 for y in vws))
                    f["DELTA_VELA_CONTRA"] = float(s * dl[kv] < 0)
                    pts.append(f)
    P = pd.DataFrame(pts); LG = pd.DataFrame(legs)
    P.to_parquet(os.path.join(SALIDA, "puntos_mes5.parquet")); LG.to_parquet(os.path.join(SALIDA, "piernas_mes5.parquet"))
    bloques = np.array_split(np.array(sorted(P["dia"].unique())), 4)
    P["bloque"] = P["dia"].map({d: i + 1 for i, b in enumerate(bloques) for d in b}); LG["bloque"] = LG["dia"].map({d: i + 1 for i, b in enumerate(bloques) for d in b})
    print("bloques:", " | ".join("%d: %s a %s" % (i + 1, pd.Timestamp(b[0]).date(), pd.Timestamp(b[-1]).date()) for i, b in enumerate(bloques)))
    for esc in ("4X", "2X"):
        Y = LG[LG["escala"] == esc]
        print("\n==== PIERNAS %s: n %d, mediana %.1f pts; delta a favor %.1f %% | por bloque %s | por tipo %s" % (
            esc, len(Y), Y["pts"].median(), 100 * Y["a_favor"].mean(), " ".join("%.0f" % (100 * x) for x in Y.groupby("bloque")["a_favor"].mean()),
            ", ".join("%s %.0f %%" % (k, 100 * x) for k, x in Y.groupby("tipo_dia")["a_favor"].mean().items())))
        Q = Y.assign(lenta=Y["velas"] >= Y["velas"].median())
        print("   piernas lentas (>= mediana de velas) delta a favor %.1f %% / rapidas %.1f %%" % (100 * Q.loc[Q["lenta"], "a_favor"].mean(), 100 * Q.loc[~Q["lenta"], "a_favor"].mean()))
        X = P[P["escala"] == esc]
        print("---- giros %d / pausas %d; cociente giro/pausa (IC 95 %%) y por bloque (dif. en puntos porcentuales)" % ((X["clase"] == "giro").sum(), (X["clase"] == "pausa").sum()))
        for f in ("L_AYER", "L_NOCHE", "L_IB", "L_VWAP", "L_REDONDO", "L_SEMANA", "L_MES", "DELTA_VELA_CONTRA"):
            g = X.loc[X["clase"] == "giro", f].dropna(); q = X.loc[X["clase"] == "pausa", f].dropna()
            rr_, lo, hi = A1.np.nan, A1.np.nan, A1.np.nan
            from r17_02_resumen import rr_katz
            rr_, lo, hi = rr_katz(g.sum(), len(g), q.sum(), len(q))
            pb = []
            for b in (1, 2, 3, 4):
                gb = X.loc[(X["clase"] == "giro") & (X["bloque"] == b), f].dropna(); qb = X.loc[(X["clase"] == "pausa") & (X["bloque"] == b), f].dropna()
                pb.append("%+.1f" % (100 * (gb.mean() - qb.mean())) if len(gb) > 20 and len(qb) > 20 else ".")
            print("   %-18s giros %5.1f %% (n %5d) | pausas %5.1f %% (n %5d) | RR %.2f (%.2f-%.2f) | bloques %s" % (
                f, 100 * g.mean(), len(g), 100 * q.mean(), len(q), rr_, lo, hi, " ".join(pb)))


if __name__ == "__main__":
    main()
