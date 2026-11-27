# -*- coding: utf-8 -*-
"""RONDA 19, paso 5: anatomia de las operaciones de una etapa: que tenian las ganadoras que no tenian las perdedoras.
Para cada factor (binario o por tramos): acierto con y sin, resultado medio en R con y sin, cociente de aciertos con IC 95 %
(Wilson / log), cuantas ruedas tienen el factor. Uso: python -I r19_05_anatomia.py <etapa> <muestra> <salida> [<etapa2> <muestra2>...]"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import r19_comun as C

FAMILIA = {"vwr": "vwap_rth", "vwr+1": "banda1_rth", "vwr-1": "banda1_rth", "vwr+2": "banda2_rth", "vwr-2": "banda2_rth",
           "vwj": "vwap_jornada", "vwj+1": "banda1_jornada", "vwj-1": "banda1_jornada", "vwj+2": "banda2_jornada", "vwj-2": "banda2_jornada",
           "vws": "vwap_semanal", "vwm": "vwap_mensual", "pdh": "ayer_hl", "pdl": "ayer_hl", "pdc": "ayer_cierre",
           "pd_poc": "ayer_va", "pd_vah": "ayer_va", "pd_val": "ayer_va", "asia_h": "asia", "asia_l": "asia", "lon_h": "londres",
           "lon_l": "londres", "ib_h": "ib", "ib_l": "ib", "ib_h05": "ib_ext", "ib_h10": "ib_ext", "ib_l05": "ib_ext", "ib_l10": "ib_ext",
           "ps_poc": "semana_va", "ps_vah": "semana_va", "ps_val": "semana_va", "r50": "redondo50"}


def wilson(k, n, z=1.96):
    if n == 0: return (np.nan, np.nan)
    p = k / n; d = 1 + z * z / n; c = p + z * z / (2 * n); r = z * math.sqrt(p * (1 - p) / n + z * z / (4 * n * n))
    return ((c - r) / d, (c + r) / d)


def fila(nombre, m, o):
    """m: mascara booleana del factor sobre las operaciones o (con columnas gana, enR, dia)."""
    a = o[m]; b = o[~m]
    if len(a) == 0 or len(b) == 0:
        return None
    pa, pb = a["gana"].mean(), b["gana"].mean()
    rr = (pa / pb) if pb > 0 else np.nan
    # IC del cociente de proporciones (log)
    ka, na, kb, nb = a["gana"].sum(), len(a), b["gana"].sum(), len(b)
    if ka > 0 and kb > 0:
        se = math.sqrt(1 / ka - 1 / na + 1 / kb - 1 / nb); lo, hi = math.exp(math.log(rr) - 1.96 * se), math.exp(math.log(rr) + 1.96 * se)
    else:
        lo = hi = np.nan
    return dict(factor=nombre, n_con=na, acierto_con=round(100 * pa, 1), n_sin=nb, acierto_sin=round(100 * pb, 1), cociente=round(rr, 2),
                ic=("%.2f-%.2f" % (lo, hi)), enR_con=round(a["enR"].mean(), 3), enR_sin=round(b["enR"].mean(), 3),
                ruedas_con=a["dia"].nunique())


def anatomia(o):
    o = o.copy()
    o["gana"] = (o["pts"] > 0).astype(int); o["enR"] = o["pts"] / o["R"]
    filas = []
    for f, nom in (("F1", "absorcion"), ("F2", "iceberg"), ("F3", "divergencia"), ("F4", "agotamiento"), ("F5", "apilado"), ("F6", "big trades"),
                   ("ret_con_agresores", "retroceso con agresores")):
        r = fila(nom, o[f] == 1, o)
        if r: filas.append(r)
    for p in sorted(o["puntaje"].unique()):
        r = fila("puntaje = %d" % p, o["puntaje"] == p, o)
        if r: filas.append(r)
    r = fila("2+ niveles en la zona", o["n_niveles"] >= 2, o); filas.append(r) if r else None
    fam = o["niveles"].fillna("").apply(lambda s: set(FAMILIA.get(x, x) for x in s.split("|") if x))
    for fm in sorted(set().union(*fam.tolist())):
        r = fila("nivel: " + fm, fam.apply(lambda z: fm in z), o)
        if r: filas.append(r)
    nofam = fam.apply(lambda z: len(z - {"banda1_rth", "banda1_jornada", "banda2_rth", "banda2_jornada", "vwap_rth", "vwap_jornada"}) > 0)
    r = fila("algun nivel NO intradia (ayer, noche, IB, semana, mes, redondo)", nofam, o); filas.append(r) if r else None
    for col in ("ext_ib", "ruido", "en_va_ayer"):
        for v in sorted(o[col].astype(str).unique()):
            r = fila("%s = %s" % (col, v), o[col].astype(str) == v, o)
            if r: filas.append(r)
    o["hora_h"] = (o["t"] // 1800) * 0.5 + 9.5
    for a, b, nom in ((0, 1800, "9:45-10:00"), (1800, 3600, "10:00-10:30"), (3600, 7200, "10:30-11:30"), (7200, 14400, "11:30-13:30"), (14400, 21600, "13:30-15:30")):
        r = fila("hora " + nom, (o["t"] >= a) & (o["t"] < b), o)
        if r: filas.append(r)
    o["lado_s"] = np.where(o["lado"] == 1, "compra", "venta")
    r = fila("compra (vs venta)", o["lado"] == 1, o); filas.append(r) if r else None
    for col in ("prof_X", "vel_ret", "dur_ret_s", "dist_vwap_sd", "pierna_marea_X", "R"):
        x = o[col].astype(float)
        q1, q2 = x.quantile([1 / 3, 2 / 3])
        for nom, m in (("bajo", x <= q1), ("medio", (x > q1) & (x <= q2)), ("alto", x > q2)):
            r = fila("%s %s (%.2f-%.2f)" % (col, nom, x[m].min(), x[m].max()), m, o)
            if r: filas.append(r)
    return pd.DataFrame(filas)


def main(args):
    partes = []
    for i in range(0, len(args) - 2, 3):
        etapa, mu, sal = args[i], args[i + 1], args[i + 2]
        o = pd.read_csv(os.path.join(C.AQUI, "salidas", "ops-%s-%s.csv" % (etapa, mu)))
        partes.append(o[o["salida"] == sal])
    o = pd.concat(partes, ignore_index=True)
    a = anatomia(o)
    pd.set_option("display.width", 250); pd.set_option("display.max_rows", 300)
    print("operaciones %d | acierto %.1f %% | media %.3f R" % (len(o), 100 * (o["pts"] > 0).mean(), (o["pts"] / o["R"]).mean()))
    print(a.to_string(index=False))
    return a


if __name__ == "__main__":
    main(sys.argv[1:])
