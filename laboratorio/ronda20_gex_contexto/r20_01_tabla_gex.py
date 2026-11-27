# -*- coding: utf-8 -*-
"""RONDA 20, paso 1: la TABLA DE GEX POR MINUTO de la rueda (9:30-15:59 NY), con el dato que EXISTIA en cada instante.

En cada minuto t (fila = comienzo del minuto, ts_utc):
  - foto = la ultima cadena de SPX de CBOE PUBLICADA antes de t (tres archivos del mismo CDN, deduplicados por sello de CBOE,
    primera publicacion). edad_s = t - hora del dato (ultimo trade de la cadena, ~902 s antes del sello).
  - base ES - SPX causal: muestra = precio de la cinta de ES EN la hora del dato (desfase 0 s: el de menor dispersion, medido en
    r20_00) - spot de la foto, solo con dato en la rueda; base(t) = mediana de las ultimas 24 muestras PUBLICADAS antes de t
    (BaseCausal de la ronda 14), reinicio el 14-09 (la cinta pasa de U6 a Z6).
  - spot_es = ultimo trade de la cinta ANTES de t; S (SPX) = spot_es - base.
  - gamma de Black-Scholes repreciada en S con la IV de la foto y el plazo real hasta las 16:00 NY del vencimiento (la convencion
    de los tres archivos; la serie AM del tercer viernes viaja junta con la PM en dos de ellos). Ventana UNIFORME para las tres
    fuentes: vencimientos vivos con (16:00 NY del vencimiento - publicacion) <= 7,75 dias (los archivos guardan hasta 8 con
    relojes distintos; 7,75 los iguala). El 0DTE vencido no entra (plazo <= 0).
  - GEX por fila = (gamma_call x OI_call - gamma_put x OI_put) x 100 x S^2 x 0,01 (dolares por 1 % de movimiento; signo:
    dealers comprados en calls y vendidos en puts, la convencion de la literatura de GEX).
  - flip por BARRIDO DE SPOT: GEX total repreciado en una grilla S-250..S+250 (SPX, paso 2); cruces de signo interpolados;
    flip = el cruce mas cercano al precio; tambien el primero de abajo y el de arriba y la cantidad de cruces.
  - muros: call = strike de mayor GEX neto por ENCIMA del precio; put = strike de GEX neto mas negativo por DEBAJO (memoria
    gex-formula-auditada; si de un lado no hay, el global); OI maximo (calls+puts), OI maximo de calls y de puts.
  - volumen del dia de calls y puts (solo si el dato es de HOY; antes de la campana la foto trae el volumen de ayer: NaN).
  - todo nivel de SPX se pasa a ES sumando la base del minuto (columna base).
Uso: python -I r20_01_tabla_gex.py   ->  scratchpad\\r20\\gex_minuto.parquet (+ .csv.gz y base_muestras.csv)"""
import datetime as dt
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import pandas as pd

import r20_comun as C

VENTANA_D = 7.75
GRILLA = np.arange(-250.0, 251.0, 2.0)
SALIDA = os.path.join(C.SCR, "gex_minuto.parquet")


def es_tercer_viernes(f):
    d = dt.date.fromisoformat(f)
    return d.weekday() == 4 and 15 <= d.day <= 21


def preparar_foto(f, dia):
    F = f["filas"]
    vi = F[:, 1].astype(int)
    v16 = np.array([x.timestamp() for x in f["venc_utc"]])          # 16:00 NY de cada vencimiento (epoch s)
    r = {"K": F[:, 0], "oc": F[:, 2], "op": F[:, 3], "ic": F[:, 4], "ip": F[:, 5], "vc": F[:, 6], "vp": F[:, 7],
         "v16": v16[vi],
         "es0": np.array([f["vencs"][k] == dia for k in vi]),
         "mens": np.array([es_tercer_viernes(f["vencs"][k]) and f["vencs"][k] != dia for k in vi]),
         "en_vent": (v16[vi] - f["gen"].timestamp()) <= VENTANA_D * 86400.0}
    return r


def cruces(P, G):
    s = np.sign(G)
    i = np.where(s[:-1] * s[1:] < 0)[0]
    z = P[i] + (P[i + 1] - P[i]) * G[i] / (G[i] - G[i + 1])
    return z


def rasgos_minuto(fp, ts, S, gen, dato, dia):
    T = (fp["v16"] - ts) / C.ANIO
    m = (T > 0) & fp["en_vent"]
    if not m.any():
        return None
    K, oc, op, ic, ip = fp["K"][m], fp["oc"][m], fp["op"][m], fp["ic"][m], fp["ip"][m]
    Tm, es0, mens = T[m], fp["es0"][m], fp["mens"][m]
    fac = C.MULT_SPX * S * S * 0.01
    gc = C.gamma_bs(S, K, Tm, ic) * oc * fac
    gp = C.gamma_bs(S, K, Tm, ip) * op * fac
    net = gc - gp
    o = {}
    o["gex_neto"] = net.sum() / 1e9
    o["gex_0dte"] = net[es0].sum() / 1e9
    o["gex_sin_mensual"] = net[~mens].sum() / 1e9
    o["gex_bruto"] = (gc + gp).sum() / 1e9
    o["gex_norm"] = o["gex_neto"] / o["gex_bruto"] if o["gex_bruto"] > 0 else np.nan
    o["mensual_en_ventana"] = bool(mens.any())
    o["n_vencs"] = int(len(np.unique(fp["v16"][m])))
    o["hay_0dte"] = bool(es0.any())
    # perfil por strike
    Ku, inv = np.unique(K, return_inverse=True)
    nk = np.bincount(inv, weights=net) / 1e9
    oik = np.bincount(inv, weights=oc + op)
    oick = np.bincount(inv, weights=oc)
    oipk = np.bincount(inv, weights=op)
    arr, aba = Ku > S, Ku < S
    if arr.any() and nk[arr].max() > 0:
        j = np.argmax(np.where(arr, nk, -np.inf)); o["muro_call_spx"], o["muro_call_gex"] = Ku[j], nk[j]
    else:
        j = np.argmax(nk); o["muro_call_spx"], o["muro_call_gex"] = Ku[j], nk[j]
    if aba.any() and nk[aba].min() < 0:
        j = np.argmin(np.where(aba, nk, np.inf)); o["muro_put_spx"], o["muro_put_gex"] = Ku[j], nk[j]
    else:
        j = np.argmin(nk); o["muro_put_spx"], o["muro_put_gex"] = Ku[j], nk[j]
    j = np.argmax(nk); o["mayor_pos_spx"], o["mayor_pos_gex"] = Ku[j], nk[j]
    j = np.argmin(nk); o["mayor_neg_spx"], o["mayor_neg_gex"] = Ku[j], nk[j]
    j = np.argmax(oik); o["oi_max_spx"], o["oi_max"] = Ku[j], oik[j]
    j = np.argmax(oick); o["oi_call_max_spx"], o["oi_call_max"] = Ku[j], oick[j]
    j = np.argmax(oipk); o["oi_put_max_spx"], o["oi_put_max"] = Ku[j], oipk[j]
    o["oi_call"], o["oi_put"] = float(oc.sum()), float(op.sum())
    o["oi_call_0dte"], o["oi_put_0dte"] = float(oc[es0].sum()), float(op[es0].sum())
    hoy = dato is not None and dato.astimezone(C.NY).date().isoformat() == dia
    if hoy:
        vc, vp = fp["vc"][m], fp["vp"][m]
        o["vol_call"], o["vol_put"] = float(vc.sum()), float(vp.sum())
        o["vol_call_0dte"], o["vol_put_0dte"] = float(vc[es0].sum()), float(vp[es0].sum())
    else:
        o["vol_call"] = o["vol_put"] = o["vol_call_0dte"] = o["vol_put_0dte"] = np.nan
    # barrido de spot
    P = S + GRILLA
    G = ((C.gamma_bs(P[:, None], K[None, :], Tm[None, :], ic[None, :]) * oc[None, :]
          - C.gamma_bs(P[:, None], K[None, :], Tm[None, :], ip[None, :]) * op[None, :]).sum(axis=1)
         * C.MULT_SPX * P * P * 0.01 / 1e9)
    o["chequeo_barrido"] = G[len(GRILLA) // 2] - o["gex_neto"]
    z = cruces(P, G)
    o["n_cruces"] = int(len(z))
    if len(z):
        o["flip_spx"] = float(z[np.argmin(np.abs(z - S))])
        ab = z[z < S]; ar = z[z > S]
        o["flip_abajo_spx"] = float(ab.max()) if len(ab) else np.nan
        o["flip_arriba_spx"] = float(ar.min()) if len(ar) else np.nan
    else:
        o["flip_spx"] = o["flip_abajo_spx"] = o["flip_arriba_spx"] = np.nan
    o["gex_barrido_min"] = float(G.min()); o["gex_barrido_max"] = float(G.max())
    return o


def main():
    t_ini = time.time()
    dias = C.dias_es()
    bc = C.BaseCausal()
    filas, mues_log = [], []
    for dia in dias:
        C.frenar_si_falta_ram(2.0)
        t0 = time.time()
        if dia == C.ROLL_RESET:
            bc = C.BaseCausal()
        meta = C.meta19(dia)
        X = float(meta["X"])
        ci = C.cinta19(dia)
        ns = ci["ns"].to_numpy(); px = ci["ultimo"].to_numpy()
        a, b = C.rth_utc(dia)
        fotos = C.fotos_para_rueda(dia)
        fp_cache = {}
        # muestras de base (publicadas en su 'gen'), solo dato en la rueda
        ms = []
        for f in fotos:
            d = f["dato"]
            if d is None or not f["spot_idx"] or not (a <= d < b):
                continue
            i = np.searchsorted(ns, int(d.timestamp() * 1e9), side="right") - 1
            if i < 0:
                continue
            ms.append((f["gen"], float(px[i] - f["spot_idx"]), f["base_arch"]))
            mues_log.append({"dia": dia, "gen_utc": f["gen"], "dato_utc": d, "muestra": ms[-1][1], "base_arch": f["base_arch"],
                             "fuente": f["fuente"]})
        ms.sort(key=lambda z: z[0])
        k = 0
        j = -1
        for mi in range(390):
            ts = a + dt.timedelta(minutes=mi)
            while k < len(ms) and ms[k][0] <= ts:
                bc.agregar(ms[k][1]); k += 1
            while j + 1 < len(fotos) and fotos[j + 1]["gen"] <= ts:
                j += 1
            i = np.searchsorted(ns, int(ts.timestamp() * 1e9), side="left") - 1     # ultimo trade ANTES de t
            spot_es = float(px[i]) if i >= 0 else np.nan
            base = bc.valor()
            fila = {"ts_utc": pd.Timestamp(ts), "dia": dia, "muestra": C.muestra_de(dia), "minuto": mi,
                    "hora_ny": ts.astimezone(C.NY).strftime("%H:%M"), "X": X, "spot_es": spot_es, "base": base,
                    "base_n": bc.n(), "hay_cadena": False}
            if j >= 0 and np.isfinite(base) and np.isfinite(spot_es):
                f = fotos[j]
                if j not in fp_cache:
                    fp_cache = {j: preparar_foto(f, dia)}
                S = spot_es - base
                r = rasgos_minuto(fp_cache[j], ts.timestamp(), S, f["gen"], f["dato"], dia)
                if r is not None:
                    fila.update(r)
                    fila["hay_cadena"] = True
                    fila["fuente"] = f["fuente"]
                    fila["foto_gen_utc"] = pd.Timestamp(f["gen"])
                    fila["dato_utc"] = pd.Timestamp(f["dato"]) if f["dato"] else pd.NaT
                    fila["edad_s"] = (ts - f["dato"]).total_seconds() if f["dato"] else np.nan
                    fila["edad_publicacion_s"] = (ts - f["gen"]).total_seconds()
                    fila["base_arch"] = f["base_arch"]
                    fila["spot_spx"] = S
                    for nom in ("flip", "flip_abajo", "flip_arriba", "muro_call", "muro_put", "mayor_pos", "mayor_neg",
                                "oi_max", "oi_call_max", "oi_put_max"):
                        fila[nom + "_es"] = r[nom + "_spx"] + base
                    fila["dist_flip_pts"] = spot_es - fila["flip_es"]
                    fila["dist_flip_X"] = fila["dist_flip_pts"] / X
                    fila["dist_muro_call_X"] = (fila["muro_call_es"] - spot_es) / X
                    fila["dist_muro_put_X"] = (spot_es - fila["muro_put_es"]) / X
                    fila["dist_oi_max_X"] = (fila["oi_max_es"] - spot_es) / X
                    fila["regimen"] = "pos" if r["gex_neto"] > 0 else "neg"
                    # contratos de ES que los dealers operarian por un movimiento de 1X (positivo = contra el movimiento)
                    fila["cobertura_es_por_X"] = r["gex_neto"] * 1e9 * (100.0 * X / spot_es) / (50.0 * spot_es)
                    vt = (r["vol_call"] + r["vol_put"]) if np.isfinite(r["vol_call"]) else np.nan
                    fila["vol_call_frac"] = r["vol_call"] / vt if vt and np.isfinite(vt) and vt > 0 else np.nan
            filas.append(fila)
        print("%s %-8s X %.2f fotos %3d muestras_base %3d base %.2f | %.1f s" % (dia, C.muestra_de(dia), X, len(fotos), len(ms),
              bc.valor(), time.time() - t0), flush=True)
    df = pd.DataFrame(filas)
    # flujo de 30 min (del dato): fraccion de calls en el volumen NUEVO de los ultimos 30 min, por rueda
    df["vol_call_frac_30"] = np.nan
    for dia, g in df.groupby("dia"):
        g = g.sort_values("minuto")
        vc, vp, da = g["vol_call"].to_numpy(), g["vol_put"].to_numpy(), g["dato_utc"]
        out = np.full(len(g), np.nan)
        for ii in range(len(g)):
            if not np.isfinite(vc[ii]) or pd.isna(da.iloc[ii]):
                continue
            # la fila cuyo dato es >= 30 min mas viejo
            lim = da.iloc[ii] - pd.Timedelta(minutes=30)
            prev = np.where((da <= lim).to_numpy() & np.isfinite(vc))[0]
            if len(prev):
                p = prev[-1]
                dc, dp = vc[ii] - vc[p], vp[ii] - vp[p]
                if dc + dp > 0:
                    out[ii] = dc / (dc + dp)
        df.loc[g.index, "vol_call_frac_30"] = out
    df.to_parquet(SALIDA, index=False)
    df.to_csv(SALIDA.replace(".parquet", ".csv.gz"), index=False, compression="gzip")
    pd.DataFrame(mues_log).to_csv(os.path.join(C.SCR, "base_muestras.csv"), index=False)
    print("filas", len(df), "con cadena", int(df["hay_cadena"].sum()), "| %.0f s" % (time.time() - t_ini))
    print("chequeo barrido (GEX del barrido en S - GEX directo), max abs:", float(np.nanmax(np.abs(df["chequeo_barrido"]))))


if __name__ == "__main__":
    main()
