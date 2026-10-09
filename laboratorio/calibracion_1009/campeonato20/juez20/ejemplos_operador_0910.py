# -*- coding: utf-8 -*-
"""ejemplos_operador_0910.py — los EJEMPLOS QUE MARCO EL OPERADOR A MANO el 09-10 juzgados con juez20 (ACIERTO20). SOLO LECTURA.
Datos: velas M1 MNQZ6 hasta la ultima vela cerrada (velas_hasta_ahora.construir: velas_m1.csv + centinela 'hoy' de la 2.0) y las series
REALES que anoto la 4.1 minuto a minuto (%APPDATA%/ATAS/PythiaGex4/familia/niv-2026-10-09-MNQZ6.jsonl; clave k = vigente en la vela k
-> desfase_min 0; sensibilidad con desfase 1). Para cada ejemplo: (1) las llegadas que REGISTRA el juez en esa serie cerca de la hora
marcada, (2) la llegada FORZADA en el minuto exacto que marco el operador (evaluar_llegada: mismas reglas, sin exigir que la vela previa
este afuera de la banda). Escribe datos/ejemplos_operador_0910.json y .txt. Uso: python -I ejemplos_operador_0910.py"""
import ctypes
import json
import os
import sys

import numpy as np
import pandas as pd

try:
    ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)
except Exception:
    pass

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import juez20 as J20  # noqa: E402
from velas_hasta_ahora import construir  # noqa: E402

NIV = os.path.join(os.environ["APPDATA"], "ATAS", "PythiaGex4", "familia", "niv-2026-10-09-MNQZ6.jsonl")
D = "2026-10-09 "
SESION0 = pd.Timestamp("2026-10-08 22:00")

# (id, descripcion del operador, series donde esta esa raya, (precio_min, precio_max), (desde, hasta) para buscar llegadas,
#  minutos marcados a mano, recorrido que dijo)
EJEMPLOS = [
    ("E1", "NDX 30800 = 31040,74 piso a las 03:11Z (recorrio ~93)",
     ["MUROS_NDX_oi.D1", "MUROS_NDX_oi.D2", "MUROS_NDX_vol.D2", "MAJORS_NDX_vol.D2"], (31040.0, 31041.5), ("03:00", "03:40"),
     ["03:11"], 93),
    ("E2", "NQ muro P OI 31100 piso ~05:0x-05:1xZ (subio a ~31136)",
     ["MUROS_NQ_oi.D2"], (31099.5, 31100.5), ("04:30", "05:59"), ["05:05", "05:13"], 36),
    ("E3", "NDX cruce abajo (ZTP_NDX_vol D2) 31063,8-31064,0 pisos 02:18, 02:26, 02:40, 03:04, 03:07Z",
     ["ZTP_NDX_vol.D2"], (31063.5, 31064.5), ("02:10", "03:45"), ["02:18", "02:26", "02:40", "03:04", "03:07"], None),
    ("E4", "NDX cruce abajo 31112,6-31112,7 piso ~05:0xZ",
     ["ZTP_NDX_vol.D2"], (31112.3, 31112.9), ("04:30", "05:59"), ["05:05", "05:13"], None),
    ("E5", "NQ M- OI 31050 piso ~01:4x-02:1xZ",
     ["MAJORS_NQ_oi.D2"], (31049.5, 31050.5), ("01:40", "02:30"), ["01:45", "01:53", "02:15"], None),
]


def fmt_ev(e):
    s = "%s %-5s L=%.2f -> %-10s %-40s decide %s" % (e["t_llegada"].strftime("%H:%M"), e["tipo"], e["raya"], e["resultado"],
                                                     e["motivo"], e["t_decision"].strftime("%H:%M"))
    if e["acierto20"]:
        s += " | ACIERTO20 en %d min, punta %+.2f%s, recorrido %.2f (max a las %s, fin: %s%s), tramo %.2f" % (
            e["min_a_decision"], e["precision"], " FALSO ROMPIMIENTO" if e["falso_rompimiento"] else "", e["recorrido"],
            e["t_mfe"].strftime("%H:%M"), e["fin_recorrido"], ", CENSURADO" if e["recorrido_censurado"] else "", e["recorrido_tramo"])
    elif e["falsa"]:
        s += " | ENTRADA FALSA: rotura confirmada por cierre a %.2f pts mas alla (punta %+.2f)" % (e["dist_rotura"], e["precision"])
    else:
        s += " | %s" % ("CENSURADA" if e["censurada"] else "INDEFINIDA")
    return s


def mfe_antes(V, i, k_fin, L, s):
    """maximo a favor desde la raya entre la llegada y la decision (para explicar las falsas)."""
    sl = V.iloc[i:k_fin + 1]
    return float((L - sl["l"]).max()) if s > 0 else float((sl["h"] - L).max())


def main():
    Vfull, info = construir()
    V = Vfull[Vfull["t"] >= SESION0].reset_index(drop=True)
    ult = V["t"].iloc[-1]
    rayas = J20.rayas_desde_niv41(NIV)
    kmin, kmax = min(rayas), max(rayas)
    lin = ["EJEMPLOS DEL OPERADOR (09-10) con juez20 = ACIERTO20 TOLERANTE (giro 20, ventana 120 min, banda +-2).",
           "Velas MNQZ6 M1 %s -> %s (%d; ultima vela cerrada, %s UTC). Huecos > 1 min en las ultimas 10 h: %s." % (
               V["t"].iloc[0], ult, len(V), ult.strftime("%H:%M"), info["huecos_mayores_1min_ult_10h"]),
           "Series de la 4.1 (niv-2026-10-09-MNQZ6.jsonl) %s -> %s; clave k = vigente en la vela k (desfase 0; sensibilidad desfase 1)." % (
               kmin, kmax)]
    out = {"velas": {"desde": str(V["t"].iloc[0]), "hasta": str(ult), "n": len(V)}, "ejemplos": []}
    vent = [(SESION0, ult + pd.Timedelta(minutes=1))]
    cache = {}

    def correr(serie_lado, desfase):
        key = (serie_lado, desfase)
        if key not in cache:
            serie = serie_lado.split(".")[0]
            R = {t: {k: v for k, v in d.items() if k.split(".")[0] == serie} for t, d in rayas.items()}
            cache[key] = J20.evaluar(V, R, ventanas=vent, opciones={"desfase_min": desfase}, n_azar=0, corrimientos=(), n_boot=0)
        return cache[key]

    Vi = V.set_index("t")
    for eid, desc, series, (pa, pb), (ha, hb), marcados, rec_dicho in EJEMPLOS:
        lin.append("\n" + "=" * 130 + "\n%s  %s" % (eid, desc))
        ta, tb = pd.Timestamp(D + ha), pd.Timestamp(D + hb)
        rep = {"id": eid, "descripcion": desc, "registradas": [], "forzadas": [], "sensibilidad_desfase1": []}
        vistos = set()
        for sl in series:
            for desf in (0, 1):
                res = correr(sl, desf)
                evs = [e for e in res["eventos"] if pa <= e["raya"] <= pb and ta <= e["t_llegada"] <= tb]
                for e in evs:
                    clave = (e["t_llegada"], round(e["raya"], 2), desf)
                    if clave in vistos:
                        continue
                    vistos.add(clave)
                    if desf == 0:
                        lin.append("  [registrada %s] %s" % (sl.split(".")[0], fmt_ev(e)))
                        rep["registradas"].append({"serie": sl, "t": str(e["t_llegada"]), "raya": e["raya"], "tipo": e["tipo"],
                                                   "resultado": e["resultado"], "acierto20": e["acierto20"], "motivo": e["motivo"],
                                                   "t_decision": str(e["t_decision"]), "precision": e["precision"],
                                                   "recorrido": e.get("recorrido"), "t_mfe": str(e.get("t_mfe")),
                                                   "fin_recorrido": e.get("fin_recorrido"), "dist_rotura": e["dist_rotura"]})
                    else:
                        rep["sensibilidad_desfase1"].append({"serie": sl, "t": str(e["t_llegada"]), "raya": e["raya"],
                                                             "resultado": e["resultado"]})
        if rep["sensibilidad_desfase1"]:
            lin.append("  [desfase 1, sensibilidad] " + "; ".join("%s %.2f %s" % (x["t"][11:16], x["raya"], x["resultado"])
                                                                for x in rep["sensibilidad_desfase1"]))
        # forzadas en los minutos marcados a mano, con la raya que la 4.1 tenia dibujada ESE minuto en la primera serie
        sl = series[0]
        for hh in marcados:
            t = pd.Timestamp(D + hh)
            d = rayas.get(t, {})
            L = d.get(sl, (None,))[0]
            if L is None or not (pa - 20 <= L <= pb + 20):
                L = next((d[k][0] for k in series if k in d and pa <= d[k][0] <= pb), None)
            if t not in Vi.index:
                lin.append("  [forzada %s] sin vela" % hh); continue
            v = Vi.loc[t]
            if L is None:
                lin.append("  [forzada %s] la 4.1 no tenia esa raya dibujada en ese minuto" % hh); continue
            toca = (v["h"] >= L - 2) and (v["l"] <= L + 2)
            dist = (v["l"] - L) if v["l"] > L else (L - v["h"] if v["h"] < L else 0.0)
            txt = "  [forzada %s, L=%.2f] vela o %.2f h %.2f l %.2f c %.2f: %s" % (
                hh, L, v["o"], v["h"], v["l"], v["c"], "TOCA la banda +-2" if toca else "NO toca la banda (minimo %.2f pts %s)" % (
                    abs(dist), "arriba" if v["l"] > L else "abajo"))
            item = {"t": str(t), "raya": L, "toca": bool(toca), "vela": {k: float(v[k]) for k in "ohlc"}}
            if toca:
                e = J20.evaluar_llegada(V, t, L, "piso")
                txt += "\n        -> " + fmt_ev(e)
                if e["falsa"]:
                    i = int(V.index[V["t"] == t][0]); kf = int(V.index[V["t"] == e["t_decision"]][0])
                    txt += " | maximo a favor antes de romper %.2f" % mfe_antes(V, i, kf, L, -1)
                item.update({"resultado": e["resultado"], "acierto20": e["acierto20"], "motivo": e["motivo"],
                             "t_decision": str(e["t_decision"]), "recorrido": e.get("recorrido"), "dist_rotura": e["dist_rotura"]})
            elif v["l"] > L and v["l"] - L <= 8:
                # SENSIBILIDAD FUERA DE LO PRE-REGISTRADO: como si la banda fuera mas ancha (no hubo llegada real con +-2)
                e = J20.evaluar_llegada(V, t, L, "piso")
                txt += "\n        -> SENSIBILIDAD (fuera de lo pre-registrado, como si tocara): " + fmt_ev(e)
                item["sensibilidad_sin_toque"] = {"resultado": e["resultado"], "recorrido": e.get("recorrido"),
                                                  "t_decision": str(e["t_decision"])}
            lin.append(txt)
            rep["forzadas"].append(item)
        # contexto: minimo / maximo de la ventana marcada y distancia a la raya
        sub = V[(V["t"] >= ta) & (V["t"] <= tb)]
        k_lo = sub["l"].idxmin(); k_hi = sub["h"].idxmax()
        lin.append("  contexto %s-%s: minimo %.2f (%s), maximo %.2f (%s); ultima vela %s c %.2f" % (
            ha, hb, sub.loc[k_lo, "l"], sub.loc[k_lo, "t"].strftime("%H:%M"), sub.loc[k_hi, "h"], sub.loc[k_hi, "t"].strftime("%H:%M"),
            ult.strftime("%H:%M"), V["c"].iloc[-1]))
        out["ejemplos"].append(rep)
    txt = "\n".join(lin)
    print(txt)
    os.makedirs(os.path.join(AQUI, "datos"), exist_ok=True)
    with open(os.path.join(AQUI, "datos", "ejemplos_operador_0910.txt"), "w", encoding="utf-8") as fh:
        fh.write(txt + "\n")
    with open(os.path.join(AQUI, "datos", "ejemplos_operador_0910.json"), "w", encoding="utf-8") as fh:
        json.dump(out, fh, ensure_ascii=False, indent=1, default=str)


if __name__ == "__main__":
    main()
