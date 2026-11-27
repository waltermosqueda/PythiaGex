# -*- coding: utf-8 -*-
"""RONDA 19, paso 14: anatomia de las operaciones de una finalista sobre las columnas de la base de eventos: que tenian las ganadoras
que no tenian las perdedoras (acierto con y sin cada rasgo, resultado medio en R, cociente con IC 95 %), por muestra.
Uso: python -I r19_14_anatomia_base.py <csv de operaciones del juez> <finalista> [muestra]"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import r19_comun as C


def fila(nombre, m, o):
    a, b = o[m], o[~m]
    if len(a) == 0 or len(b) == 0: return None
    pa, pb = a["gana"].mean(), b["gana"].mean(); ka, kb = a["gana"].sum(), b["gana"].sum()
    rr = pa / pb if pb > 0 else np.nan
    if ka > 0 and kb > 0:
        se = math.sqrt(1 / ka - 1 / len(a) + 1 / kb - 1 / len(b)); ic = "%.2f-%.2f" % (math.exp(math.log(rr) - 1.96 * se), math.exp(math.log(rr) + 1.96 * se))
    else: ic = "-"
    return dict(rasgo=nombre, n_con=len(a), acierto_con=round(100 * pa, 1), n_sin=len(b), acierto_sin=round(100 * pb, 1), cociente=round(rr, 2) if rr == rr else np.nan,
                ic=ic, R_con=round(a["enR"].mean(), 3), R_sin=round(b["enR"].mean(), 3))


def anatomia(o, salida):
    o = o.copy(); o["gana"] = (o[salida + "_pts"] > 0).astype(int); o["enR"] = o[salida + "_pts"] / o["R"]
    F = []
    binarios = [("F1", "absorcion del lado que defiende"), ("F2", "iceberg"), ("F3", "divergencia"), ("F4", "agotamiento"),
                ("F5", "apilado a favor"), ("F6", "big trades netos a favor"), ("a_favor_marea", "a favor de la marea"),
                ("marea_con_agresores", "marea con agresores"), ("extension_fuera_ruido", "extension fuera del ruido"),
                ("ctx_ret_con_agresores", "retroceso con agresores"), ("regimen_va_y_vuelve", "regimen va y vuelve")]
    for c, nom in binarios:
        if c in o: r = fila(nom, o[c] == 1, o); F.append(r) if r else None
    if "n_niveles_en_zona" in o:
        r = fila("extremo en zona de >= 1 nivel", o["n_niveles_en_zona"] >= 1, o); F.append(r) if r else None
    if "n_barridas_y_vuelta" in o:
        r = fila("barrio un nivel y volvio", o["n_barridas_y_vuelta"] >= 1, o); F.append(r) if r else None
    for c in ("ctx_ext_ib", "ctx_en_va_ayer"):
        if c in o:
            for v in sorted(o[c].astype(str).unique()):
                r = fila("%s = %s" % (c, v), o[c].astype(str) == v, o); F.append(r) if r else None
    o["franja"] = pd.cut(o["seg_rueda"], [0, 3600, 9000, 14400, 23400], labels=["10:00-10:30", "10:30-12:00", "12:00-13:30", "13:30-16:00"], right=False)
    for v in o["franja"].cat.categories:
        r = fila("hora " + v, o["franja"] == v, o); F.append(r) if r else None
    r = fila("compra (contra venta)", o["lado"] == 1, o); F.append(r) if r else None
    for c in ("of_abs_lado_pasivo_rel", "of_netbig60_a_favor", "of_delta_60", "ctx_prof_X", "ctx_dur_ret_s", "vwap_rth_dist_sd", "R_en_X", "mov_desde_apertura"):
        if c not in o: continue
        x = o[c].astype(float)
        if x.nunique() < 3: continue
        q1, q2 = x.quantile([1 / 3, 2 / 3])
        for nom, m in (("bajo", x <= q1), ("medio", (x > q1) & (x <= q2)), ("alto", x > q2)):
            r = fila("%s %s (%.2f..%.2f)" % (c, nom, x[m].min() if m.any() else np.nan, x[m].max() if m.any() else np.nan), m, o); F.append(r) if r else None
    return pd.DataFrame(F)


if __name__ == "__main__":
    o = pd.read_csv(sys.argv[1]); fin = sys.argv[2]; o = o[o["finalista"] == fin]
    if len(sys.argv) > 3: o = o[o["muestra"] == sys.argv[3]]
    sal = o["salida_regla"].iloc[0]
    pd.set_option("display.width", 250); pd.set_option("display.max_rows", 200)
    print("%s: %d operaciones, acierto %.1f %%, %.3f R" % (fin, len(o), 100 * (o[sal + "_pts"] > 0).mean(), (o[sal + "_pts"] / o["R"]).mean()))
    print(anatomia(o, sal).to_string(index=False))
