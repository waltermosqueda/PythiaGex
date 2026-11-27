# -*- coding: utf-8 -*-
"""RONDA 27, paso 5: EXPLORACION descriptiva SOLO en las 33 ruedas de diseno (PREREGISTRO enmienda 2): para cada pieza firmada, la chance de que
el precio toque primero +-Y DEL LADO QUE MARCA la pieza (acierto alineado), agrupada y rueda por rueda (t por rueda contra 50 %), en todo el dia,
por clima y por franja. Sirve para la mecanica (que pesa, donde) y para ver si alguna pieza tiene signo estable; no elige modelos.
Uso: python -I r27_05_explorar.py [--tramo diseno] [--Y X]"""
import argparse, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r27_comun as K
import r27_03_modelos as MD
import numpy as np, pandas as pd

SAL = os.path.join(K.AQUI, "salidas")


def piezas(T):
    P = {
        "ola (seguir su direccion)": T.o_dir,
        "marea (seguir su direccion)": T.m_dir,
        "ola a favor de la marea (seguir)": np.where(T.o_dir == T.m_dir, T.o_dir, 0),
        "ola contra la marea (seguir la ola)": np.where(T.o_dir != T.m_dir, T.o_dir, 0),
        "retroceso de la ola >= 1/2 X (seguir la ola)": np.where(T.o_ret >= 0.5, T.o_dir, 0),
        "retroceso de la ola < 1/4 X (seguir la ola)": np.where(T.o_ret < 0.25, T.o_dir, 0),
        "pierna de la ola >= 2X (seguir)": np.where(T.o_lar >= 2, T.o_dir, 0),
        "pierna de la ola con agresores (seguir)": np.where(T.o_con > 0, T.o_dir, 0),
        "pierna de la ola sin agresores (seguir)": np.where(T.o_con == 0, T.o_dir, 0),
        "marea con agresores (seguir)": np.where(T.m_con > 0, T.m_dir, 0),
        "marea sin agresores (seguir)": np.where(T.m_con == 0, T.m_dir, 0),
        "lado del VWAP (seguir)": T.lado_vw,
        "lejos del VWAP >= 2X (volver al VWAP)": np.where(np.abs(T.vw_X) >= 2, -np.sign(T.vw_X), 0),
        "lado de la apertura (seguir)": T.lado_ap,
        "lado de la TENDENCIA (seguir)": T.lado_T,
        "IB extendido de un lado (seguir)": np.sign(T.ib_net),
        "delta 1 min (seguir)": np.sign(T.d1),
        "delta 5 min (seguir)": np.sign(T.d5),
        "delta 15 min (seguir)": np.sign(T.d15),
        "delta 5 min fuerte |d5| >= 0,10 (seguir)": np.where(np.abs(T.d5) >= 0.10, np.sign(T.d5), 0),
        "ordenes de 10-49 en 15 min (seguir)": np.sign(T.med15),
        "big trades 5 min (seguir)": np.sign(T.big5),
        "icebergs 60 s bid - ask (seguir el lado que defiende)": np.sign(T.ice1),
        "nivel o barrida en el origen de la ola (seguir la ola)": T.zona_o_s,
        "voto T7": T.v_T7, "voto V4": T.v_V4, "voto A3": T.v_A3,
    }
    return P


def alineado(T, s, Yk):
    y = T["y_" + Yk].to_numpy(); ok = np.isfinite(y) & (s != 0)
    a = np.where(s[ok] > 0, y[ok], 1 - y[ok])
    d = T["dia"].to_numpy()[ok]
    if ok.sum() == 0:
        return None
    por = pd.Series(a).groupby(d).mean()
    por = por[pd.Series(a).groupby(d).size() >= 20]
    t = (por.mean() - 0.5) / (por.std(ddof=1) / math.sqrt(len(por))) if len(por) > 2 and por.std(ddof=1) > 0 else 0.0
    return {"n": int(ok.sum()), "acierto": float(a.mean()), "ruedas": int(len(por)), "ruedas_sobre_50": int((por > 0.5).sum()),
            "t_rueda": float(t), "media_ruedas": float(por.mean()) if len(por) else float("nan")}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--tramo", default="diseno")
    ap.add_argument("--Y", nargs="+", default=["X"])
    a = ap.parse_args()
    T = MD.preparar(pd.read_parquet(MD.TAB))
    T = T[T.tramo == a.tramo]
    out = ["RONDA 27 - exploracion descriptiva en %s (%d ruedas). Acierto alineado = toca primero +-Y del lado que marca la pieza; "
           "t por rueda contra 50 %% (ruedas con >= 20 minutos con la pieza)." % (a.tramo, T.dia.nunique())]
    T["franja"] = np.where(T.m + 1 <= 90, "9:31-11:00", np.where(T.m + 1 <= 210, "11:00-13:00", "13:00-16:00"))
    for Yk in a.Y:
        out.append("\n=== Y = %s | arriba primero en todo el tramo %.3f" % (Yk, T["y_" + Yk].mean()))
        P = piezas(T)
        for nom, s in P.items():
            s = np.asarray(s, float)
            r = alineado(T, s, Yk)
            if r is None:
                continue
            linea = "%-55s n %6d acierto %.3f | ruedas %2d/%2d sobre 50 %%, t %+.2f" % (nom, r["n"], r["acierto"], r["ruedas_sobre_50"], r["ruedas"], r["t_rueda"])
            sub = []
            for col in ("tipo", "franja"):
                for k_, idx in T.groupby(col).groups.items():
                    msk = T.index.isin(idx)
                    rr = alineado(T[msk], s[msk], Yk)
                    if rr and rr["n"] >= 200:
                        sub.append("%s %.3f (%d, t %+.1f)" % (k_, rr["acierto"], rr["n"], rr["t_rueda"]))
            out.append(linea + "\n      " + " | ".join(sub))
    txt = os.path.join(SAL, "explorar-%s.txt" % a.tramo)
    open(txt, "w", encoding="utf-8").write("\n".join(out) + "\n")
    print("\n".join(out))


if __name__ == "__main__":
    main()
