# -*- coding: utf-8 -*-
"""RONDA 23, paso 3: VARIANTES (reglas operables) sobre la base de 5 min. Cada variante = familia + ventana horaria + ubicacion + llave +
bracket. Una posicion por vez (la siguiente entrada en una vela posterior a la salida), maximo 5 por rueda, todo cerrado a las 16:00.
Para cada variante: acierto real, acierto del AZAR CON LA MISMA UBICACION (media de 200 caminos reflejados en los mismos puntos
operados), la formula geometrica P = d_stop / (d_stop + d_objetivo), bruto, neto con costo 1,00, neto del azar, t por rueda (ruedas sin
operaciones = 0), ruedas positivas, y lo mismo por sub-periodo. TODAS las variantes evaluadas se anotan en registro_variantes.csv.
Uso:
  python -I r23_03_variantes.py diseno                 -> la grilla de diseno (sec. GRILLA) en la mitad de diseno
  python -I r23_03_variantes.py chequeo elegidas.json  -> solo las variantes elegidas (congeladas en el json) en la mitad de chequeo"""
import json, os, pickle, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import r23_comun as K
import r23_sim as S

BRACKETS = {
    "F1": {"lado": "fade", "stop": ("ext", 0.5), "obj": ("vw", 0.0)},
    "F3": {"lado": "fade", "stop": ("extmult", 0.5, 1.5), "obj": ("vw", 0.0)},
    "F3b": {"lado": "fade", "stop": ("extmult", 0.5, 1.0), "obj": ("vw", 0.0)},
    "F3m125": {"lado": "fade", "stop": ("extmult", 0.5, 1.25), "obj": ("vw", 0.0)},
    "F3m200": {"lado": "fade", "stop": ("extmult", 0.5, 2.0), "obj": ("vw", 0.0)},
    "P1": {"lado": "puerta", "stop": ("vw", 0.0), "obj": ("ext", 0.5)},
    "P4": {"lado": "puerta", "stop": ("vw", 0.0), "obj": ("X", 1.0)},
    "P5": {"lado": "puerta", "stop": ("X", 1.5), "obj": ("X", 1.0)},
}


def hm(s):
    return int(s[:2]) * 60 + int(s[3:])


def filtro(pts, v):
    m = (pts["s"] != 0) & (pts["min_dia"] >= hm(v["desde"])) & (pts["min_dia"] <= hm(v["hasta"]))
    loc = v["loc"]
    if loc[0] == "dv": m &= pts["dv"] >= loc[1]
    elif loc[0] == "z": m &= pts["z"] >= loc[1]
    elif loc[0] == "dx": m &= pts["dx"] <= loc[1]
    elif loc[0] == "dxdv": m &= (pts["dx"] <= loc[1]) & (pts["dv"] >= loc[2])
    elif loc[0] == "nada": pass
    else: raise ValueError(loc)
    for ll in v["llave"]:
        if ll == "ib<=1": m &= pts["ext_lado"].fillna(0) <= 1.0
        elif ll == "ib<=1.5": m &= pts["ext_lado"].fillna(0) <= 1.5
        elif ll == "ib<=0.75": m &= pts["ext_lado"].fillna(0) <= 0.75
        elif ll == "ib>0": m &= pts["ext_lado"].fillna(0) > 0
        elif ll == "ib>0.5": m &= pts["ext_lado"].fillna(0) > 0.5
        elif ll == "ib>1": m &= pts["ext_lado"].fillna(0) > 1.0
        elif ll == "T": m &= (pts["tipo"] == "T")
        elif ll == "noT": m &= (pts["tipo"] != "T")
        elif ll == "rrn<2": m &= pts["rrn"] < 2.0
        elif ll.startswith("Pf>="): pass   # se aplica despues (necesita el stop)
        else: raise ValueError(ll)
    return m


def evaluar(sim, v, dias):
    c = sim[filtro(sim, v)].copy()
    c["dstop"] = (c["stop"] - c["c"]).abs()
    c["dobj"] = np.where(np.isfinite(c["obj"]), (c["obj"] - c["c"]).abs(), (c["c"] - c["vw"]).abs())
    c["Pf"] = c["dstop"] / (c["dstop"] + c["dobj"])
    for ll in v["llave"]:
        if ll.startswith("Pf>="):
            c = c[c["Pf"] >= float(ll[4:])]
    tk = S.operar(c.sort_values(["dia", "k"]), 5)
    return tk


def resumen(tk, dias, costo=K.COSTO):
    nd = len(dias); n = len(tk)
    out = dict(ruedas=nd, n=n, ops_por_rueda=round(n / nd, 2) if nd else 0)
    if n == 0: return out
    net = tk["pts_real"] - costo
    por = net.groupby(tk["dia"]).sum(); serie = np.array([por.get(d, 0.0) for d in dias])
    out.update(acierto_pct=round(100 * (tk["pts_real"] > 0).mean(), 1), acierto_azar_pct=round(100 * tk["azar_gana"].mean(), 1),
               P_formula_pct=round(100 * tk["Pf"].mean(), 1), bruto_op=round(tk["pts_real"].mean(), 3),
               neto_op_c100=round(net.mean(), 3), neto_azar_c100=round((tk["azar_pts"] - costo).mean(), 3),
               t_rueda_c100=round(K.t_por_rueda(serie), 2), ruedas_pos_c100=int((serie > 0).sum()),
               R_medio=round(tk["dstop"].mean(), 2), objetivo_medio=round(tk["dobj"].mean(), 2),
               motivos="obj %d / stop %d / fin %d" % ((tk["mot_real"] == 2).sum(), tk["mot_real"].isin([1, 3]).sum(), (tk["mot_real"] == 0).sum()))
    return out


def grilla():
    V = []
    # Familia A - ELASTICO DE LA TARDE (fade hacia el VWAP)
    for desde in ("13:00", "14:00"):
        for loc in (("dv", 2.0), ("dv", 3.0), ("z", 2.0)):
            for llave in ((), ("ib<=1",)):
                for br in ("F3", "F3b", "F1"):
                    ll = llave + (("Pf>=0.55",) if br == "F1" else ())
                    V.append(dict(familia="A_elastico_tarde", desde=desde, hasta="15:30", loc=loc, llave=ll, bracket=br))
    # Familia B - PUERTA DE LA TARDE en dia de tendencia (a favor de la extension, IB extendido > 1 vez de ese lado)
    for desde in ("13:00", "14:00"):
        for llave in (("ib>1",), ("ib>1", "T")):
            for loc in (("dv", 2.0), ("dv", 3.0)):
                for br in ("P1", "P4", "P5"):
                    V.append(dict(familia="B_puerta_tarde", desde=desde, hasta="15:30", loc=loc, llave=llave, bracket=br))
    # Familia C - PUERTA DE MEDIA MANANA (11:00-12:25, a favor de la extension cerca del extremo)
    for llave in ((), ("T",), ("ib>0.5",)):
        for loc in (("dx", 1.0), ("dxdv", 1.0, 2.0)):
            for br in ("P1", "P5"):
                V.append(dict(familia="C_puerta_manana", desde="11:00", hasta="12:25", loc=loc, llave=llave, bracket=br))
    for v in V:
        v["nombre"] = nombre(v)
    return V


def nombre(v):
    return "%s|%s-%s|%s|%s|%s" % (v["familia"][0], v["desde"], v["hasta"], "".join(str(x) for x in v["loc"]), "+".join(v["llave"]) or "sin", v["bracket"])


def vecindario():
    """Vecindario de las dos geometrias de la familia A que pasaron en diseno (se mira para elegir una MESETA, no un pico)."""
    V = []
    for desde in ("13:00", "13:30", "14:00"):
        for dv in (1.5, 2.0, 2.5, 3.0):
            for llave in ((), ("ib<=1",), ("ib<=1.5",)):
                for br in ("F3m125", "F3", "F3m200"):
                    V.append(dict(familia="A_elastico_tarde", desde=desde, hasta="15:30", loc=("dv", dv), llave=llave, bracket=br))
                for pf in ("Pf>=0.5", "Pf>=0.55", "Pf>=0.6"):
                    V.append(dict(familia="A_elastico_tarde", desde=desde, hasta="15:30", loc=("dv", dv), llave=llave + (pf,), bracket="F1"))
    for v in V:
        v["nombre"] = nombre(v)
    return V


def sims_de(muestra, brs, ru, P, hasta=930):
    d = P[(P.muestra == muestra) & (P.s != 0) & (P.min_dia >= 600) & (P.min_dia <= hasta)].copy()
    out = {}
    for br in brs:
        f = os.path.join(K.SCR, "sim_%s1530_%s.parquet" % (muestra, br))
        if os.path.exists(f):
            out[br] = pd.read_parquet(f)
        else:
            K.frenar_si_falta_ram(2.0)
            out[br] = S.correr(ru, d, BRACKETS[br], M=200); out[br].to_parquet(f, index=False)
    return out


def main():
    muestra = sys.argv[1]; t0 = time.time(); K.frenar_si_falta_ram(2.0)
    B = pickle.load(open(os.path.join(K.SCR, "ruedas5.pkl"), "rb")); ru = B["ruedas"]
    P = pd.read_parquet(os.path.join(K.SCR, "puntos5.parquet"))
    dias = sorted(P[P.muestra == muestra]["dia"].unique())
    if muestra == "diseno" and len(sys.argv) > 2 and sys.argv[2] == "vecindario":
        V = vecindario(); etapa = "vecindario"
    elif muestra == "diseno":
        V = grilla(); etapa = "diseno"
    else:
        V = json.load(open(sys.argv[2], encoding="utf-8"))["variantes"]; etapa = "chequeo"
    sims = sims_de(muestra, sorted({v["bracket"] for v in V}), ru, P)
    sub_de = (lambda d: "S1" if d <= "2025-11-30" else "S2") if muestra == "diseno" else (lambda d: "S1" if d <= "2026-05-31" else "S2")
    filas = []; L = []
    nv = K.siguiente_n_var()
    for v in V:
        tk = evaluar(sims[v["bracket"]], v, dias)
        r = resumen(tk, dias)
        subs = {}
        for sname in ("S1", "S2"):
            dd = [d for d in dias if sub_de(d) == sname]
            subs[sname] = resumen(tk[tk["dia"].isin(dd)], dd)
        pasa = (r.get("acierto_pct", 0) >= 60) and (r.get("neto_op_c100", -9) > 0) and (r["ops_por_rueda"] <= 5)
        nota = "S1 %s%% %+.2f (n %d) | S2 %s%% %+.2f (n %d) | %s" % (subs["S1"].get("acierto_pct", "-"), subs["S1"].get("neto_op_c100", 0), subs["S1"]["n"],
                                                                 subs["S2"].get("acierto_pct", "-"), subs["S2"].get("neto_op_c100", 0), subs["S2"]["n"], r.get("motivos", ""))
        fila = dict(n_var=nv, etapa=etapa + os.environ.get("R23_SUF", ""), familia=v["familia"], variante=v["nombre"], parametros=json.dumps({k: v[k] for k in ("desde", "hasta", "loc", "llave", "bracket")}),
                    muestra=muestra, pasa_vara=int(pasa), nota=nota, **{k: r.get(k, "") for k in K.CAMPOS if k in r})
        K.anotar(fila); nv += 1
        filas.append(dict(fila, **{"S1_acierto": subs["S1"].get("acierto_pct", np.nan), "S1_neto": subs["S1"].get("neto_op_c100", np.nan),
                                   "S2_acierto": subs["S2"].get("acierto_pct", np.nan), "S2_neto": subs["S2"].get("neto_op_c100", np.nan)}))
        tk.assign(variante=v["nombre"]).to_csv(os.path.join(K.SCR, "ops_%s_%s.csv" % (muestra, v["nombre"].replace("|", "_").replace(":", "").replace("<", "le").replace(">", "gt").replace("=", ""))), index=False)
    T = pd.DataFrame(filas)
    cols = ["n_var", "variante", "n", "ops_por_rueda", "acierto_pct", "acierto_azar_pct", "P_formula_pct", "bruto_op", "neto_op_c100", "neto_azar_c100",
            "t_rueda_c100", "ruedas_pos_c100", "R_medio", "objetivo_medio", "S1_acierto", "S1_neto", "S2_acierto", "S2_neto", "pasa_vara"]
    pd.set_option("display.width", 300); pd.set_option("display.max_columns", 30); pd.set_option("display.max_colwidth", 60)
    txt = T[cols].to_string(index=False)
    print(txt)
    sufijo = "_vecindario" if etapa == "vecindario" else ""
    open(os.path.join(K.SAL, "variantes_%s%s.txt" % (muestra, sufijo)), "w", encoding="utf-8").write(
        "RONDA 23 - variantes en %s (%d ruedas) %s\n%s\n" % (muestra, len(dias), time.strftime("%Y-%m-%d %H:%M"), txt))
    T.to_csv(os.path.join(K.SAL, "variantes_%s%s.csv" % (muestra, sufijo)), index=False)
    print("%d variantes, %.1f s" % (len(V), time.time() - t0))


if __name__ == "__main__":
    main()
