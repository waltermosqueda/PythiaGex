# -*- coding: utf-8 -*-
"""e00_comun.py — esceptico2 (campeonato20, 09-10). Cargadores SOLO LECTURA que rearman, para cada caso, exactamente lo que juzgo cada
familia (velas, rayas por minuto, ventanas, opciones), leyendo sus archivos de datos. No escribe nada fuera de esceptico2/.
Casos:
  R20_NDX_vol|noche|mirar           (familia ndx: unico p < 0,01 de MIRAR)
  MUROS_NQ_vol|noche|mirar / prueba (familia nq: la candidata principal)
  MAJORS_NQ_vol|noche|mirar, MUROS_NQ_vol.P|noche|mirar (las otras 2 candidatas NQ)
  ZTP_NQ_vol|noche|prueba, MUROS_NQ_vol.P|rueda|prueba, ZEST_NQ_oi|noche|prueba (p ~0,04-0,06 en PRUEBA, no candidatas)
  CONF_vol|rueda|MIRAR, CONF_NDX_NQ_vol|rueda|MIRAR (familia combos: p 0,004 en MIRAR)
"""
import glob
import os
import sys

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
C20 = os.path.normpath(os.path.join(AQUI, ".."))
J20DIR = os.path.join(C20, "juez20")
if J20DIR not in sys.path:
    sys.path.insert(0, J20DIR)
import juez20 as J20  # noqa: E402
J = J20.J


def prioridad_baja():
    try:
        import ctypes
        ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)
    except Exception:
        pass


# ------------------------------------------------------------------------------------------------ NDX (copia de ndx_03_juez)
def _caso_ndx(conjunto, ventana, part):
    D = os.path.join(C20, "ndx")
    V = pd.read_csv(os.path.join(D, "datos", "velas_ndx.csv"), parse_dates=["t"])[["t", "o", "h", "l", "c"]]
    series = {}; metas = []
    for p in sorted(glob.glob(os.path.join(D, "datos", "series", "ndx_*.pkl"))):
        x = pd.read_pickle(p)
        for s, d in x["series"].items():
            series.setdefault(s, {}).update(d)
        metas.append(x["meta"])
    M = pd.concat(metas, ignore_index=True)
    tv = set(V["t"])
    M = M[M["t"].isin(tv)].copy()
    hm = M["t"].dt.strftime("%H:%M")
    M = M[(hm >= "22:00") | (hm < "13:30")] if ventana == "noche" else M[(hm >= "13:30") & (hm < "20:00")]
    g = M.groupby("sesion").agg(velas=("t", "size"), ok=("motivo", lambda x: (x == "ok").sum()))
    g["entra"] = (g["velas"] > 60) & (g["ok"] / g["velas"] >= 0.5)
    ses = sorted(g.index[g["entra"]])
    mirar, prueba = J20.particion_noches(ses)
    noches = mirar if part == "mirar" else prueba
    if ventana == "noche":
        ven = J20.ventanas_de_noches(noches)
    else:
        ven = [(pd.Timestamp(d) + pd.Timedelta(hours=13.5), pd.Timestamp(d) + pd.Timedelta(hours=20)) for d in noches]
    claves = {"R20_NDX_vol": ("D1", "D2")}[conjunto]
    return dict(V=V, rayas=series[conjunto], ventanas=ven, opciones={"desfase_min": 0, "claves": claves}, noches=noches,
                informado=None)


# ------------------------------------------------------------------------------------------------ NQ (copia de nq02_juez.trabajo)
def _caso_nq(serie, vt, parte):
    D = os.path.join(C20, "familia_nq")
    if D not in sys.path:
        sys.path.insert(0, D)
    import nq_comun as C  # noqa: E402
    import json
    VENT = {"noche": (-2.0, 13.5), "rueda": (13.5, 20.0)}
    V = pd.read_csv(os.path.join(C.DATOS, "velas_m1_nq.csv"), parse_dates=["t"])
    info = json.load(open(os.path.join(C.DATOS, "nq01_info.json"), encoding="utf-8"))
    reg = {d: pd.read_pickle(os.path.join(C.DATOS, "registros-%s.pkl" % d)) for d in info["sesiones"]}

    def cobertura(dia, vtipo, fuente):
        a, b = VENT[vtipo]
        d = pd.Timestamp(dia)
        ka = int((d + pd.Timedelta(hours=a)).value // 60_000_000_000); kb = int((d + pd.Timedelta(hours=b)).value // 60_000_000_000)
        return [r["k"] for r in reg[dia] if ka <= r["k"] < kb and r["libro"] and (fuente == "vol" or r["oi_ok"])]

    def tramos(ks):
        if not ks:
            return []
        ks = sorted(ks); out = [[ks[0], ks[0]]]
        for k in ks[1:]:
            if k - out[-1][1] <= 10:
                out[-1][1] = k
            else:
                out.append([k, k])
        return [(pd.Timestamp(a * 60, unit="s"), pd.Timestamp((b + 1) * 60, unit="s")) for a, b in out]

    eleg = [d for d in sorted(reg) if len(cobertura(d, vt, "vol")) >= 60]
    mirar, prueba = J20.particion_noches(eleg)
    dias = mirar if parte == "mirar" else prueba
    fuente = C.FUENTE_SERIE[serie]
    vent = []; cub = set()
    for d in dias:
        vent += tramos(cobertura(d, vt, fuente))
    for a, b in vent:
        ka = int(a.value // 60_000_000_000); kb = int(b.value // 60_000_000_000)
        cub.update(range(ka, kb))
    rayas = {}
    for d in dias:
        for t, v in C.rayas_de_serie(reg[d], serie).items():
            rayas[t] = v if int(t.value // 60_000_000_000) in cub else {}
    return dict(V=V, rayas=rayas, ventanas=vent, opciones={"desfase_min": 0}, noches=dias, informado=None)


# ------------------------------------------------------------------------------------------------ combos (copia de evaluar_familia.juzgar_uno)
def _caso_conf(serie, ventana, parte):
    D = os.path.join(C20, "familia_conf")
    VENT = {"noche": (-2.0, 13.5), "rueda": (13.5, 20.0)}
    R = pd.read_pickle(os.path.join(D, "datos", "rayas", "%s.pkl" % serie))
    V = pd.read_pickle(os.path.join(D, "datos", "velas_familia.pkl"))
    a, b = VENT[ventana]

    def ses(t):
        return (pd.Timestamp(t) + pd.Timedelta(hours=2)).strftime("%Y-%m-%d")
    con = set()
    for t, v in R.items():
        if not v:
            continue
        d = ses(t); N = pd.Timestamp(d)
        if N + pd.Timedelta(hours=a) <= t < N + pd.Timedelta(hours=b):
            con.add(d)
    noches = []
    tvv = V["t"]
    for d in sorted(con):
        N = pd.Timestamp(d)
        if int(((tvv >= N + pd.Timedelta(hours=a)) & (tvv < N + pd.Timedelta(hours=b))).sum()) >= 60:
            noches.append(d)
    mirar, prueba = J20.particion_noches(noches)
    sel = mirar if parte == "MIRAR" else prueba
    ss = set(sel)
    Rs = {t: v for t, v in R.items() if ses(t) in ss}
    V2 = V[V["noche"].isin(ss)].reset_index(drop=True)
    return dict(V=V2, rayas=Rs, ventanas=J20.ventanas_de_noches(sel, a, b), opciones={"desfase_min": 0}, noches=sel, informado=None)


def caso(nombre):
    fam, serie, ventana, parte = nombre.split("|")
    if fam == "ndx":
        return _caso_ndx(serie, ventana, parte)
    if fam == "nq":
        return _caso_nq(serie, ventana, parte)
    if fam == "conf":
        return _caso_conf(serie, ventana, parte)
    raise KeyError(nombre)


CASOS = {
    # nombre: (archivo con lo informado, clave)
    "ndx|R20_NDX_vol|noche|mirar": ("ndx", "R20_NDX_vol__noche__mirar"),
    "nq|MUROS_NQ_vol|noche|mirar": ("nq", ("MUROS_NQ_vol", "noche", "mirar", 500)),
    "nq|MUROS_NQ_vol|noche|prueba": ("nq", ("MUROS_NQ_vol", "noche", "prueba", 500)),
    "nq|MAJORS_NQ_vol|noche|mirar": ("nq", ("MAJORS_NQ_vol", "noche", "mirar", 500)),
    "nq|MUROS_NQ_vol.P|noche|mirar": ("nq", ("MUROS_NQ_vol.P", "noche", "mirar", 500)),
    "nq|ZTP_NQ_vol|noche|prueba": ("nq", ("ZTP_NQ_vol", "noche", "prueba", 500)),
    "nq|MUROS_NQ_vol.P|rueda|prueba": ("nq", ("MUROS_NQ_vol.P", "rueda", "prueba", 500)),
    "nq|ZEST_NQ_oi|noche|prueba": ("nq", ("ZEST_NQ_oi", "noche", "prueba", 500)),
    "conf|CONF_vol|rueda|MIRAR": ("conf", "CONF_vol__rueda__MIRAR"),
    "conf|CONF_NDX_NQ_vol|rueda|MIRAR": ("conf", "CONF_NDX_NQ_vol__rueda__MIRAR"),
}


def informado(nombre):
    """Lo que la familia informo (pct, llegadas, p) para comparar."""
    import json
    fam, clave = CASOS[nombre]
    if fam == "ndx":
        x = json.load(open(os.path.join(C20, "ndx", "datos", "juez", clave + ".json"), encoding="utf-8"))
        m = x["metricas"]; a = x["placebo"]["azar"]["pct_acierto20"]
        return dict(llegadas=m["llegadas"], aciertos=m["aciertos"], pct=m["pct_acierto20"], p50=a["p50"], p=a["p_valor"],
                    pctl=a["percentil"], n_azar=x["n_azar"])
    if fam == "nq":
        x = json.load(open(os.path.join(C20, "familia_nq", "datos", "juez_nq.json"), encoding="utf-8"))
        s, v, p, n = clave
        for r in x["resultados"]:
            if r["serie"] == s and r["ventana"] == v and r["parte"] == p and r["n_azar"] == n:
                m = r["metricas"]; a = r["azar"]["pct_acierto20"]
                return dict(llegadas=m["llegadas"], aciertos=m["aciertos"], pct=m["pct_acierto20"], p50=a["p50"], p=a["p_valor"],
                            pctl=a["percentil"], n_azar=n)
    if fam == "conf":
        x = json.load(open(os.path.join(C20, "familia_conf", "resultados", clave + ".json"), encoding="utf-8"))
        m = x["metricas"]; a = x["placebo"]["azar"]["pct_acierto20"]
        return dict(llegadas=m["llegadas"], aciertos=m["aciertos"], pct=m["pct_acierto20"], p50=a["p50"], p=a["p_valor"],
                    pctl=a["percentil"], n_azar=x["placebo"].get("n_azar"))
    return None
