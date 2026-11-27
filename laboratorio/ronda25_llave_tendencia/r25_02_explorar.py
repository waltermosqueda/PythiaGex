# -*- coding: utf-8 -*-
"""RONDA 25, paso 2: EXPLORACION A MANO (regla "de menor a mayor"): las 15 primeras ruedas de la cinta de ES (10-09 a 30-09-2026),
una por una, con lo que se ve ANTES de las 13:00 (noche, apertura, medias horas A-G, VWAP, IB, los seis detectores) y despues lo que paso
en la tarde (TA, D, tipo de dia, NV) y lo que hizo A3 ese dia. Estas 15 ruedas quedan FUERA de la validacion en la cinta.
Uso: python -I r25_02_explorar.py -> salidas/exploracion_a_mano.txt"""
import os, pickle, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import r25_comun as K
import r25_rasgos as RS

L = []


def W(s=""):
    print(s, flush=True); L.append(s)


def apertura_dalton(b5, X):
    """tipo de apertura en los primeros 30 min con velas de 5 min (descriptivo): cuanto fue contra la apertura y donde cerro A."""
    O = b5[0, 0]; hA = b5[:6, 1].max(); lA = b5[:6, 2].min(); cA = b5[5, 3]
    arriba = (hA - O) / X; abajo = (O - lA) / X
    ub = (cA - lA) / (hA - lA) if hA > lA else 0.5
    if min(arriba, abajo) <= 0.25 and (ub >= 0.67 or ub <= 0.33): t = "drive (no volvio a la apertura)"
    elif min(arriba, abajo) <= 1.0 and (ub >= 0.67 or ub <= 0.33): t = "test-drive / rejection"
    else: t = "auction (giro alrededor de la apertura)"
    return "A: %+.1f X arriba / %+.1f X abajo de la apertura, cierra en el %.0f %% de A -> %s" % (arriba, -abajo, 100 * ub, t)


def main():
    R = pickle.load(open(os.path.join(K.SCR, "dias_cinta.pkl"), "rb"))
    ops = pd.read_csv(os.path.join(K.R23, "salidas", "ops-cinta-A3.csv"), dtype={"fecha": str})
    dias = [d for d in sorted(R) if K.en(d, K.EXPLORA)]
    W("RONDA 25 - EXPLORACION A MANO: %d ruedas de la cinta de ES (%s a %s). Velas de 30 min; VWAP de las velas de 30 min." % (len(dias), dias[0], dias[-1]))
    resumen = []
    for d in dias:
        r = R[d]; f = RS.rasgos_dia(r); b = r["b30"]; vw = RS.vwap30(b); X = r["X"]
        p = r["prev"]; n = r["noche"]
        W(""); W("=" * 150)
        W("%s (%s) | X %.2f (mediana 10 = %.1f) | ayer %s | noche %s" % (
            d, pd.Timestamp(d).day_name()[:3], X, r["med10"],
            ("%s H %.2f L %.2f C %.2f" % (p["dia"], p["H"], p["L"], p["C"])) if p else "SIN DATO (cambio de contrato)",
            ("%.2f-%.2f (%.1f X), cierra %.2f, VWAP %.2f" % (n["l"], n["h"], (n["h"] - n["l"]) / X, n["c"], n["vw"])) if n else "-"))
        W("  apertura %.2f: %s del rango de ayer%s | en el %.0f %% del rango de la noche | %s" % (
            f["O"], ("ARRIBA" if p and f["O"] > p["H"] else ("ABAJO" if p and f["O"] < p["L"] else "dentro")) if p else "?",
            (" (hueco %+.1f X contra el cierre)" % f["gap_X"]) if p else "", 100 * f.get("noche_pos", np.nan), apertura_dalton(r["b5"], X)))
        W("   per  hora    apert    max      min      cierre   VWAP     lado  OTF  sin tocar VWAP")
        for k in range(13):
            lado = "+" if b[k, 3] > vw[k] else ("-" if b[k, 3] < vw[k] else "=")
            otf = ""; sv = ""
            if 1 <= k <= 6 and f["s13"] != 0:
                otf = "si" if ((f["s13"] > 0 and b[k, 2] >= b[k - 1, 2]) or (f["s13"] < 0 and b[k, 1] <= b[k - 1, 1])) else "no"
                sv = "si" if ((f["s13"] > 0 and b[k, 2] > vw[k - 1]) or (f["s13"] < 0 and b[k, 1] < vw[k - 1])) else "no"
            if k == 7: W("   ---- 13:00 ----")
            W("   %s   %02d:%02d  %8.2f %8.2f %8.2f %8.2f %8.2f   %s   %-3s  %s" % ("ABCDEFGHIJKLM"[k], (570 + 30 * k) // 60, (570 + 30 * k) % 60,
                                                                           b[k, 0], b[k, 1], b[k, 2], b[k, 3], vw[k], lado, otf, sv))
        W("  A LAS 13:00: precio %s del VWAP a %.1f X | APE q %.2f (%s) | OTF %d/6 | VWA %d/6 | ACE %s | EXT %.2f | FOMC %s, dato 8:30 %s | IB %.1f X, extendido %.2f del lado / %.2f del otro | cierre en el %.0f %% de la manana (del lado) | cruces %d | VWAP 11-13 %+.1f X | manana %+.1f X" % (
            "ARRIBA" if f["s13"] > 0 else "ABAJO", f["dv13"], f["APE_q"], "coherente" if f["APE_coh"] else "no", f["OTF_n"], f["VWA_n"],
            "sin dato" if f["ACE_sin_dato"] else ("SI" if f["ACE_0"] else "no (g %+d)" % f["ACE_g"]), f["EXT_r"], "SI" if f["CAL_fomc"] else "no",
            "SI" if f["dato830"] else "no", f["ib_X"], f["ib_ext_lado"], f["ib_ext_otro"], 100 * f["clv13_lado"], f["cruces30"], f["pend_vw"], f["mov13"]))
        W("  DESPUES: TA %d (D %+.2f X: la tarde %s) | tipo %s (eficiencia %.2f) | NV %d (la tarde %s el VWAP) | %.0f %% del movimiento del dia despues de las 13" % (
            f["TA"], f["D"], "se alejo del VWAP" if f["TA"] else "volvio hacia el VWAP", f["tipo"], f["efic"], f["NV"],
            "no toco" if f["NV"] else "toco", 100 * f["pm_frac"] if np.isfinite(f["pm_frac"]) else np.nan))
        o = ops[ops.fecha == d]
        for _, x in o.iterrows():
            W("  A3: %s %s a %.2f (VWAP %.2f, %.2f X; stop %.2f) -> %s %s %+.2f" % (x.hora_entrada[:5], x.lado, x.entrada, x.vwap, x.dist_vwap_X, x.stop, x.motivo, x.hora_salida[:5], x.pts))
        if len(o) == 0: W("  A3: sin operaciones")
        resumen.append(dict(f, a3_n=len(o), a3_pts=o.pts.sum() if len(o) else 0.0))
    S = pd.DataFrame(resumen)
    W(""); W("RESUMEN de las %d ruedas (lo que se ve a las 13:00 contra como termino el dia):" % len(S))
    cols = ["dia", "tipo", "TA", "D", "NV", "s13", "dv13", "APE_q", "APE_coh", "OTF_n", "VWA_n", "ACE_0", "EXT_r", "CAL_fomc", "dato830",
            "ib_ext_lado", "clv13_lado", "cruces30", "pend_vw", "mov13", "gap_lado", "pm_frac", "a3_n", "a3_pts"]
    pd.set_option("display.width", 250); pd.set_option("display.max_columns", 40)
    W(S[cols].round(2).to_string(index=False))
    open(os.path.join(K.SAL, "exploracion_a_mano.txt"), "w", encoding="utf-8").write("\n".join(L))


if __name__ == "__main__":
    main()
