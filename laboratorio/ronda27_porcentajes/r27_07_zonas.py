# -*- coding: utf-8 -*-
"""RONDA 27, paso 7: TABLA DE ZONAS (descriptiva, pre-registrada en la enmienda 2, sin eleccion): para zonas fijadas desde la teoria y los
expertos ya medidos, la chance de que el precio toque primero +-Y del lado que marca la zona, por tramo (diseno / confirmacion / final), con IC90
por bootstrap de ruedas (los minutos seguidos de una misma rueda no son independientes) y t por rueda contra 50 %. No entra en Bonferroni: es la
materia prima de lo que podria mostrar el panel ("en esta zona paso X % en N ruedas").
Uso: python -I r27_07_zonas.py [--tramos diseno confirmacion final] [--Y X Xm X2]"""
import argparse, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r27_comun as K
import r27_03_modelos as MD
import numpy as np, pandas as pd

SAL = os.path.join(K.AQUI, "salidas")


def zonas(T):
    lejos = np.abs(T.vw_X) >= 2
    return {
        "Z1 voto T7 (seguir la tendencia en la vuelta de la ola)": T.v_T7,
        "Z2 voto V4 (banda 2 sd con iceberg en ROTACION)": T.v_V4,
        "Z3 voto A3 (tarde, hacia el VWAP despues del giro)": T.v_A3,
        "Z4 TENDENCIA: seguir el lado de la tendencia": T.lado_T,
        "Z5 TENDENCIA con ola y marea del lado de la tendencia": np.where((T.tipo == "T") & (T.o_dir == T.lado_T) & (T.m_dir == T.lado_T), T.lado_T, 0),
        "Z6 ROTACION y lejos del VWAP (>= 2X): volver al VWAP": np.where((T.tipo == "R") & lejos, -np.sign(T.vw_X), 0),
        "Z7 ESTRECHO y lejos del VWAP (>= 2X): volver al VWAP": np.where((T.tipo == "E") & lejos, -np.sign(T.vw_X), 0),
        "Z8 seguir la marea (todo el dia)": T.m_dir,
        "Z9 seguir la ola (todo el dia)": T.o_dir,
    }


def medir(T, s, Yk, nboot=1000, semilla=5):
    y = T["y_" + Yk].to_numpy(); ok = np.isfinite(y) & (np.asarray(s) != 0)
    if ok.sum() == 0:
        return None
    a = np.where(np.asarray(s)[ok] > 0, y[ok], 1 - y[ok]); d = T["dia"].to_numpy()[ok]
    g = pd.DataFrame({"d": d, "a": a}).groupby("d")["a"]
    por = g.mean(); nn = g.size()
    dias = list(por.index); sums = g.sum().to_numpy(); cnt = nn.to_numpy()
    rng = np.random.default_rng(semilla); bs = []
    for _ in range(nboot):
        i = rng.integers(0, len(dias), len(dias)); bs.append(sums[i].sum() / cnt[i].sum())
    pq = por[nn >= 20]                       # t por rueda y conteo solo con ruedas que tienen >= 20 minutos en la zona (como r27_05)
    t = (pq.mean() - 0.5) / (pq.std(ddof=1) / math.sqrt(len(pq))) if len(pq) > 2 and pq.std(ddof=1) > 0 else float("nan")
    return {"n": int(ok.sum()), "ruedas": len(dias), "acierto": float(a.mean()), "ic90": (float(np.quantile(bs, 0.05)), float(np.quantile(bs, 0.95))),
            "ruedas_sobre_50": "%d/%d" % (int((pq > 0.5).sum()), len(pq)), "t": float(t)}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--tramos", nargs="+", default=["diseno", "confirmacion", "final"])
    ap.add_argument("--Y", nargs="+", default=["X", "Xm", "X2"])
    a = ap.parse_args()
    T = MD.preparar(pd.read_parquet(MD.TAB))
    out = ["RONDA 27 - tabla de zonas (descriptiva; acierto = toca primero +-Y del lado de la zona; IC90 por bootstrap de ruedas)"]
    for Yk in a.Y:
        out.append("\n=== Y = %s" % Yk)
        for tr in a.tramos:
            Tt = T[T.tramo == tr]
            out.append("  -- %s (%d ruedas)" % (tr, Tt.dia.nunique()))
            for nom, s in zonas(Tt).items():
                r = medir(Tt, np.asarray(s, float), Yk)
                if r is None:
                    out.append("     %-58s sin casos" % nom); continue
                out.append("     %-58s %5d min en %2d ruedas: %.1f %% (IC90 %.1f-%.1f) | ruedas con >= 20 min sobre 50 %%: %s, t %+.2f" % (
                    nom, r["n"], r["ruedas"], 100 * r["acierto"], 100 * r["ic90"][0], 100 * r["ic90"][1], r["ruedas_sobre_50"], r["t"]))
    et = "+".join(a.tramos)
    open(os.path.join(SAL, "zonas-%s.txt" % et), "w", encoding="utf-8").write("\n".join(out) + "\n")
    print("\n".join(out))


if __name__ == "__main__":
    main()
