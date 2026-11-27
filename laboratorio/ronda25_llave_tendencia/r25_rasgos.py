# -*- coding: utf-8 -*-
"""RONDA 25 - RASGOS de cada rueda a las 13:00 NY (los seis detectores del PREREGISTRO_R25.md, seccion 3), los OBJETIVOS (TA, D, TIPO, NV;
seccion 2) y las TARDES AL AZAR (200 tardes reflejadas por rueda, como r23_sim: cada vela de 30 min de 13:00 a 16:00 se refleja al azar
alrededor del cierre previo, mismo volumen, VWAP recalculado).
Todo lo que usan los detectores es anterior al cierre de las 13:00 (velas A..G de 30 min, la noche, el dia anterior y el calendario)."""
import math
import numpy as np, pandas as pd
import r25_comun as K

GRILLA = {"APE": (0.10, 0.15, 0.20), "OTF": (4, 5, 6), "VWA": (4, 5, 6), "ACE": (0.0, 0.5), "EXT": (0.8, 1.0, 1.2), "CAL": ("fomc", "fomc+830")}
TEORIA = {"APE": 0.15, "OTF": 5, "VWA": 5, "ACE": 0.0, "EXT": 1.0, "CAL": "fomc"}


def vwap30(b):
    p = (b[:, 1] + b[:, 2]) / 2.0; v = b[:, 4]
    return np.cumsum(p * v) / np.cumsum(v)


def rasgos_dia(r):
    b = r["b30"]; X = r["X"]; dia = r["dia"]
    o, h, l, c = b[:, 0], b[:, 1], b[:, 2], b[:, 3]
    vw = vwap30(b)
    O = o[0]; H13 = h[:7].max(); L13 = l[:7].min(); c13 = c[6]; vw13 = vw[6]; C16 = c[12]
    s13 = int(np.sign(c13 - vw13))
    H = h.max(); L = l.min()
    f = dict(dia=dia, X=X, med10=r["med10"], O=O, H13=H13, L13=L13, c13=c13, vw13=vw13, C16=C16, s13=s13, dv13=abs(c13 - vw13) / X)
    # ---- objetivos
    f["TA"] = int(s13 != 0 and s13 * (C16 - c13) > 0)
    f["D"] = s13 * (C16 - c13) / X
    e = abs(C16 - O) / (H - L); f["efic"] = e
    f["TIPO"] = int(e >= 0.6); f["tipo"] = "tendencia" if e >= 0.6 else ("balance" if e < 0.3 else "mixto")
    toca = [(l[k] <= vw[k - 1] <= h[k]) for k in range(7, 13)]
    f["NV"] = int(not any(toca))
    hpm = h[7:].max(); lpm = l[7:].min()                               # Enmienda 1: ET = tarde de tendencia en cualquier direccion
    f["efic_pm"] = abs(C16 - c13) / (hpm - lpm) if hpm > lpm else 0.0
    f["ET"] = int(f["efic_pm"] >= 0.6 and abs(C16 - c13) >= 2 * X)
    # ---- 1. APE
    q = min(O - L13, H13 - O) / (H13 - L13)
    d_ape = 1 if (O - L13) <= (H13 - O) else -1
    coh = (d_ape == s13) and (np.sign(c13 - O) == d_ape)
    f["APE_q"] = q; f["APE_d"] = d_ape; f["APE_coh"] = int(coh)
    f["APE_score"] = (0.5 - q) if coh else 0.0
    for th in GRILLA["APE"]: f["APE_%g" % th] = int(coh and q <= th)
    # ---- 2. OTF
    if s13 > 0: otf = [l[k] >= l[k - 1] for k in range(1, 7)]
    elif s13 < 0: otf = [h[k] <= h[k - 1] for k in range(1, 7)]
    else: otf = [False] * 6
    f["OTF_n"] = int(sum(otf)); f["OTF_score"] = float(f["OTF_n"])
    for th in GRILLA["OTF"]: f["OTF_%g" % th] = int(f["OTF_n"] >= th)
    # ---- 3. VWA
    if s13 > 0: aw = [l[k] > vw[k - 1] for k in range(1, 7)]
    elif s13 < 0: aw = [h[k] < vw[k - 1] for k in range(1, 7)]
    else: aw = [False] * 6
    f["VWA_n"] = int(sum(aw)); f["VWA_score"] = float(f["VWA_n"])
    for th in GRILLA["VWA"]: f["VWA_%g" % th] = int(f["VWA_n"] >= th)
    # ---- 4. ACE
    p = r["prev"]
    if p is None:
        g = 0; f["ACE_sin_dato"] = 1
    else:
        f["ACE_sin_dato"] = 0
        g = 1 if O > p["H"] else (-1 if O < p["L"] else 0)
    f["ACE_g"] = g
    for eps in GRILLA["ACE"]:
        ok = 0
        if g != 0 and g == s13:
            ok = int((L13 > p["H"] - eps * X) if g > 0 else (H13 < p["L"] + eps * X))
        f["ACE_%g" % eps] = ok
    f["ACE_score"] = float(f["ACE_0"])
    # ---- 5. EXT
    f["EXT_r"] = (H13 - L13) / r["med10"]; f["EXT_score"] = f["EXT_r"]
    for th in GRILLA["EXT"]: f["EXT_%g" % th] = int(f["EXT_r"] >= th)
    # ---- 6. CAL
    f["CAL_fomc"] = int(dia in K.FOMC); f["CAL_fomc+830"] = int(dia in K.FOMC or dia in K.DATO830); f["CAL_score"] = float(f["CAL_fomc"])
    f["dato830"] = int(dia in K.DATO830)
    # ---- descriptivos (no son detectores)
    ibh = h[:2].max(); ibl = l[:2].min(); ibr = ibh - ibl
    f["ib_X"] = ibr / X
    f["ib_ext_lado"] = (max(0.0, (H13 - ibh) / ibr) if s13 > 0 else max(0.0, (ibl - L13) / ibr)) if s13 != 0 else 0.0
    f["ib_ext_otro"] = (max(0.0, (ibl - L13) / ibr) if s13 > 0 else max(0.0, (H13 - ibh) / ibr)) if s13 != 0 else 0.0
    f["clv13"] = (c13 - L13) / (H13 - L13)
    f["clv13_lado"] = f["clv13"] if s13 > 0 else 1 - f["clv13"]        # 1 = el cierre de las 13 en el extremo del lado del VWAP
    sg = np.sign(c[:7] - vw[:7]); f["cruces30"] = int(((sg[1:] != sg[:-1]) & (sg[1:] != 0) & (sg[:-1] != 0)).sum())
    f["pend_vw"] = s13 * (vw[6] - vw[2]) / X                           # VWAP de 11:00 a 13:00, del lado del precio
    f["mov13"] = s13 * (c13 - O) / X                                   # cuanto se movio la manana (del lado del VWAP)
    if r["noche"] is not None:
        n = r["noche"]; rn = n["h"] - n["l"]
        f["noche_pos"] = (O - n["l"]) / rn if rn > 0 else np.nan        # donde abre la rueda en el rango de la noche
        f["noche_rango_X"] = rn / X
        f["noche_vs_vw_X"] = (O - n["vw"]) / X
    f["gap_X"] = (O - p["C"]) / X if p is not None else np.nan
    f["gap_lado"] = s13 * f["gap_X"] if p is not None else np.nan
    # resultado de la tarde para describir
    f["rango_tarde_X"] = (h[7:].max() - l[7:].min()) / X
    f["pm_frac"] = abs(C16 - c13) / abs(C16 - O) if abs(C16 - O) > 0 else np.nan     # parte del movimiento del dia hecha despues de las 13
    return f


def tardes_azar(r, M=200, semilla=25):
    """M tardes reflejadas desde el cierre de las 13:00: devuelve dict de arrays (M,) con TA, TIPO, NV y ET simulados."""
    b = r["b30"]; o, h, l, c, v = (b[:, j] for j in range(5))
    vw = vwap30(b); X = r["X"]
    O = o[0]; H13 = h[:7].max(); L13 = l[:7].min(); c13 = c[6]; s13 = np.sign(c13 - vw[6])
    rng = np.random.default_rng((semilla * 1000003 + int(r["dia"].replace("-", ""))) % (2 ** 32))
    cprev = np.full(M, c13); p = (h + l) / 2.0
    pv = np.full(M, float((p[:7] * v[:7]).sum())); vv = np.full(M, float(v[:7].sum()))
    Hs = np.full(M, H13); Ls = np.full(M, L13); toca = np.zeros(M, bool)
    Hpm = np.full(M, -np.inf); Lpm = np.full(M, np.inf)
    for k in range(7, 13):
        eps = rng.integers(0, 2, M) * 2.0 - 1.0
        dh = h[k] - c[k - 1]; dl = l[k] - c[k - 1]; dc = c[k] - c[k - 1]
        h2 = cprev + np.where(eps > 0, dh, -dl); l2 = cprev + np.where(eps > 0, dl, -dh); c2 = cprev + eps * dc
        vprev = pv / vv
        toca |= (l2 <= vprev) & (vprev <= h2)
        Hs = np.maximum(Hs, h2); Ls = np.minimum(Ls, l2); Hpm = np.maximum(Hpm, h2); Lpm = np.minimum(Lpm, l2)
        pv = pv + (h2 + l2) / 2.0 * v[k]; vv = vv + v[k]; cprev = c2
    mv = np.abs(cprev - c13)
    return dict(TA=(s13 * (cprev - c13) > 0).astype(int), TIPO=(np.abs(cprev - O) / (Hs - Ls) >= 0.6).astype(int), NV=(~toca).astype(int),
                ET=((mv / np.where(Hpm > Lpm, Hpm - Lpm, np.inf) >= 0.6) & (mv >= 2 * X)).astype(int))


def tabla(R, desde=None, hasta=None, M=200):
    filas = []; az = {}
    for d in sorted(R):
        if desde and d < desde: continue
        if hasta and d > hasta: continue
        f = rasgos_dia(R[d]); filas.append(f)
        az[d] = tardes_azar(R[d], M=M)
    return pd.DataFrame(filas), az
