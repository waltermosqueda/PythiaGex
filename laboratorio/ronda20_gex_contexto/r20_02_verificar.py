# -*- coding: utf-8 -*-
"""RONDA 20, paso 2: VERIFICACION A MANO de la tabla por minuto, por un camino independiente.
Para tres minutos (uno por periodo) se relee la linea CRUDA del archivo de cadena (sin pasar por r20_comun), se recalcula la gamma
con scipy (norm.pdf) fila por fila en Python puro, el GEX = gamma x OI x 100 x S^2 x 0,01 (memoria gex-formula-auditada), los
muros, el OI maximo, el volumen y el flip buscando la raiz con brentq en el barrido; se compara contra la tabla. Ademas: contra
los niveles de la ronda 14 en el mismo minuto, y la base causal contra la base archivada (forward contra forward).
Uso: python -I r20_02_verificar.py"""
import datetime as dt
import gzip
import json
import math
import os
import pickle
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import pandas as pd
from scipy.optimize import brentq
from scipy.stats import norm
from zoneinfo import ZoneInfo

import r20_comun as C

NYZ = ZoneInfo("America/New_York")
tab = pd.read_parquet(os.path.join(C.SCR, "gex_minuto.parquet"))
CASOS = [("2026-10-21", 90), ("2026-10-14", 200), ("2026-11-17", 60)]


def linea_cruda(gen_utc):
    """Busca la linea con ese 'generado' en los tres archivos del dia UTC (camino independiente)."""
    for carpeta in C.FUENTES.values():
        p = os.path.join(carpeta, "cadena-ES-%s.jsonl.gz" % gen_utc.strftime("%Y-%m-%d"))
        if not os.path.exists(p):
            continue
        with gzip.open(p, "rt", encoding="utf-8") as f:
            for l in f:
                try:
                    x = json.loads(l)
                except Exception:
                    continue
                g = dt.datetime.fromisoformat(x["generado"]).astimezone(dt.timezone.utc)
                if abs((g - gen_utc).total_seconds()) < 1:
                    return x, carpeta
    return None, None


def gamma_a_mano(S, K, T, s):
    if not (0.005 < s < 3.0):
        return 0.0
    T = max(T, 120.0 / (365 * 86400))
    d1 = (math.log(S / K) + (0.034 + 0.5 * s * s) * T) / (s * math.sqrt(T))
    return norm.pdf(d1) / (S * s * math.sqrt(T))


for dia, mi in CASOS:
    r = tab[(tab.dia == dia) & (tab.minuto == mi)].iloc[0]
    ts = r["ts_utc"].to_pydatetime()
    gen = r["foto_gen_utc"].to_pydatetime()
    x, carpeta = linea_cruda(gen)
    c = x["cadena"]
    S = r["spot_es"] - r["base"]
    dato = dt.datetime.fromisoformat(c["ultimo_trade"]).replace(tzinfo=NYZ).astimezone(dt.timezone.utc)
    vencs = [v["f"] for v in c["vencimientos"]]
    tot = {"neto": 0.0, "0dte": 0.0, "vc": 0.0, "vp": 0.0}
    porK, oiK = {}, {}
    filas_vivas = []
    for fila in c["filas"]:
        K, vi, oc, op, ic, ip, vc, vp = fila
        f = vencs[int(vi)]
        y, mo, dd = map(int, f.split("-"))
        v16 = dt.datetime(y, mo, dd, 16, 0, tzinfo=NYZ).astimezone(dt.timezone.utc)
        T = (v16 - ts).total_seconds() / (365 * 86400)
        if T <= 0 or (v16 - gen).total_seconds() > 7.75 * 86400:
            continue
        g = (gamma_a_mano(S, K, T, ic) * oc - gamma_a_mano(S, K, T, ip) * op) * 100 * S * S * 0.01
        tot["neto"] += g
        if f == dia:
            tot["0dte"] += g
        tot["vc"] += vc; tot["vp"] += vp
        porK[K] = porK.get(K, 0.0) + g
        oiK[K] = oiK.get(K, 0.0) + oc + op
        filas_vivas.append((K, T, oc, op, ic, ip))

    def gex_en(P):
        return sum((gamma_a_mano(P, K, T, ic) * oc - gamma_a_mano(P, K, T, ip) * op) * 100 * P * P * 0.01
                   for K, T, oc, op, ic, ip in filas_vivas)
    # flip: raiz mas cercana a S en el barrido (paso 2, refinada con brentq)
    P = np.arange(S - 250, S + 251, 2.0)
    G = np.array([gex_en(p) for p in P])
    raices = []
    for i in range(len(P) - 1):
        if G[i] * G[i + 1] < 0:
            raices.append(brentq(gex_en, P[i], P[i + 1], xtol=1e-6))
    flip = min(raices, key=lambda z: abs(z - S)) if raices else float("nan")
    arriba = {k: v for k, v in porK.items() if k > S}
    abajo = {k: v for k, v in porK.items() if k < S}
    mc = max(arriba, key=arriba.get); mp = min(abajo, key=abajo.get)
    om = max(oiK, key=oiK.get)
    print("\n=== %s minuto %d (%s NY) | foto %s (%s) | dato %s NY | edad a mano %.0f s, tabla %.0f s" % (
        dia, mi, r["hora_ny"], gen.strftime("%H:%M:%S UTC"), os.path.basename(carpeta), dato.astimezone(NYZ).strftime("%H:%M:%S"),
        (ts - dato).total_seconds(), r["edad_s"]))
    print("  S (SPX) = spot_es %.2f - base %.3f = %.3f" % (r["spot_es"], r["base"], S))
    print("  GEX neto   a mano %+.4f mil M | tabla %+.4f" % (tot["neto"] / 1e9, r["gex_neto"]))
    print("  GEX 0DTE   a mano %+.4f mil M | tabla %+.4f" % (tot["0dte"] / 1e9, r["gex_0dte"]))
    print("  flip (SPX) a mano %.3f (%d raices) -> ES %.3f | tabla %.3f (%d cruces)" % (flip, len(raices), flip + r["base"], r["flip_es"], r["n_cruces"]))
    print("  muro call  a mano %.0f -> ES %.2f | tabla %.2f" % (mc, mc + r["base"], r["muro_call_es"]))
    print("  muro put   a mano %.0f -> ES %.2f | tabla %.2f" % (mp, mp + r["base"], r["muro_put_es"]))
    print("  OI maximo  a mano %.0f -> ES %.2f | tabla %.2f" % (om, om + r["base"], r["oi_max_es"]))
    print("  volumen    a mano calls %.0f puts %.0f | tabla %.0f / %.0f" % (tot["vc"], tot["vp"], r["vol_call"], r["vol_put"]))
    print("  regimen    a mano %s | tabla %s | dist al flip %.2f X (a mano %.2f)" % (
        "pos" if tot["neto"] > 0 else "neg", r["regimen"], r["dist_flip_X"], (r["spot_es"] - (flip + r["base"])) / r["X"]))
    # contra la ronda 14 (mismo minuto; su precio es el cierre de la vela anterior del MES y su base, la suya)
    p14 = os.path.join(C.LAB, "ronda14", "I_gamma", "datos", "niveles", "niv-%s.pkl" % dia)
    if os.path.exists(p14):
        n14 = pickle.load(open(p14, "rb"))
        q = n14[n14.i == mi]
        if len(q):
            q = q.iloc[0]
            print("  RONDA 14 mismo minuto: S %.2f base %.2f | zero_oi %.2f (yo %.2f) | mayor + %.0f (yo %.0f) | mayor - %.0f (yo %.0f) |"
                  " OI calls max %.0f (yo %.0f) | OI puts max %.0f (yo %.0f) | signo %+d (yo %s)" % (
                      q.S, q.base, q.zero_oi, r["flip_es"] - r["base"], q.mpos, r["mayor_pos_es"] - r["base"], q.mneg,
                      r["mayor_neg_es"] - r["base"], q.cw, r["oi_call_max_es"] - r["base"], q.pw, r["oi_put_max_es"] - r["base"],
                      q.regimen_oi, r["regimen"]))

# base causal contra la archivada (forward contra forward de la nube), por rueda
b = tab[tab.hay_cadena & tab.base_arch.notna()].copy()
b["dif"] = b["base"] - b["base_arch"]
res = b.groupby("dia")["dif"].median()
print("\nBase causal - base archivada (forward-forward), mediana por rueda: mediana %.2f, p10 %.2f, p90 %.2f, max abs %.2f (n ruedas %d)" % (
    res.median(), res.quantile(0.1), res.quantile(0.9), res.abs().max(), len(res)))
print(res.round(2).to_string())
