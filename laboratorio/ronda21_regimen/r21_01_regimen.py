# -*- coding: utf-8 -*-
"""RONDA 21, paso 1: rasgos de REGIMEN por minuto (causales) y etiquetas ex post (solo para medir), para las 55 ruedas de ES.
Fuente: rasgos por segundo de la R19 (scratchpad/r19/rasgos/ES-*.npz + json) y hist/resumen.json; curvas f(m) y g(m) FIJAS medidas
en MES 5 min ANTES del 08-09-2026 (fuera de las tres muestras). Ver PREREGISTRO.md, etapa 1.
Salida: scratchpad/r21/regimen_minuto.parquet (una fila por rueda x minuto) y curvas_previas.json.
Uso: python -I r21_01_regimen.py"""
import json, math, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r21_comun as K
import numpy as np, pandas as pd
import r19_02_motor as M
import r19_08_eventos as EV

TK = K.TK
VELAS = os.path.join(os.path.dirname(K.AQUI), "dom", "ronda3", "velas", "velas-MES-m5.csv")


def curvas_previas():
    v = pd.read_csv(VELAS, usecols=["t", "h", "l"])
    ny = pd.to_datetime(v["t"]).dt.tz_localize("UTC").dt.tz_convert("America/New_York")
    v["fecha"] = ny.dt.strftime("%Y-%m-%d"); v["mi"] = ny.dt.hour * 60 + ny.dt.minute - 570
    rth = v[(v["mi"] >= 0) & (v["mi"] < 390) & (v["fecha"] < "2026-09-08")]
    F, G = [], []
    for fecha, g in rth.groupby("fecha"):
        g = g.sort_values("mi")
        if len(g) != 78 or not (g["mi"].to_numpy() == 5 * np.arange(78)).all(): continue
        hh = g["h"].to_numpy(float); ll = g["l"].to_numpy(float); rng = hh.max() - ll.min()
        if rng <= 0: continue
        F.append((np.maximum.accumulate(hh) - np.minimum.accumulate(ll)) / rng)
        G.append(np.array([hh[max(0, k - 5):k + 1].max() - ll[max(0, k - 5):k + 1].min() for k in range(78)]) / rng)
    Fm = np.median(np.array(F), axis=0); Gm = np.median(np.array(G), axis=0)
    xk = 5 * np.arange(78) + 4; m = np.arange(390)
    f = np.interp(m, np.r_[-1, xk], np.r_[0.0, Fm]); g = np.interp(m, np.r_[-1, xk], np.r_[0.0, Gm])
    return f, g, len(F)


def resumen_es():
    res = json.load(open(os.path.join(K.SCR19, "hist", "resumen.json")))
    out = {}
    for k, v in res.items():
        inst, d = k.split("|")
        if inst == "ES" and "rth_h" in v: out[d] = v
    return out


def ultimo_previo(dias_ord, dia):
    prev = [d for d in dias_ord if d < dia]
    return prev[-1] if prev else None


def rasgos_dia(dia, f, g, ef10_map, res):
    R, meta = M.cargar("ES", dia)
    X = meta["X"]; med = meta["mediana_rango_10"]; O = meta["rth_o"]; niv = meta["niv"]; C_ = meta["rth_c"]
    c = R["c"]; h = R["h"]; l = R["l"]; vw_s = R["vw_r"]; sd_s = R["sd_r"]
    sec = 60 * np.arange(390) + 59
    H = np.maximum.accumulate(h); L = np.minimum.accumulate(l)
    cm = c[sec]; Hm = H[sec]; Lm = L[sec]
    d = pd.DataFrame({"dia": dia, "periodo": K.periodo_de(dia), "m": np.arange(390), "X": X, "med10": med, "O": O,
                      "c": cm, "H": Hm, "L": Lm})
    d["hora"] = [M.seg_hora(60 * m) [:5] for m in range(390)]
    # rango y velocidad
    d["rrn"] = (Hm - Lm) / (med * f)
    rmax = pd.Series(h).rolling(1800, min_periods=1).max().to_numpy()[sec]
    rmin = pd.Series(l).rolling(1800, min_periods=1).min().to_numpy()[sec]
    d["vel30n"] = (rmax - rmin) / (med * g)
    # ruido de Zarattini
    sig = np.asarray(R["ruido_sigma"], float)
    z = (cm - O) / sig; d["sigma"] = sig; d["z"] = z
    fuera = np.abs(z) > 1; sg = np.sign(z); sost = np.zeros(390)
    for m in range(390):
        w = slice(max(0, m - 29), m + 1); sost[m] = float(np.mean(fuera[w] & (sg[w] == sg[m])))
    d["sost"] = sost
    # balance inicial (desde las 10:30 = minuto 60)
    ibh, ibl = niv["ib_h"], niv["ib_l"]; ibr = max(ibh - ibl, TK)
    eu = np.maximum(0, Hm - ibh) / ibr; ed = np.maximum(0, ibl - Lm) / ibr
    eu[:60] = np.nan; ed[:60] = np.nan
    d["ib_ext_arriba"] = eu; d["ib_ext_abajo"] = ed; d["ib_rango_X"] = ibr / X
    # apertura contra ayer
    if niv["pd_val"] <= O <= niv["pd_vah"]: ap = "dentro_va"
    elif O > niv["pdh"] or O < niv["pdl"]: ap = "fuera_rango"
    else: ap = "fuera_va"
    d["apertura"] = ap; d["apertura_lado"] = 1 if O > niv["pd_poc"] else -1
    # VWAP RTH
    vw = vw_s[sec]; sd = sd_s[sec]
    d["vw"] = vw; d["sd"] = sd
    lado = np.sign(cm - vw); d["lado_vw"] = lado
    d["vw_dist_sd"] = np.where(sd > 0, (cm - vw) / np.where(sd > 0, sd, 1), np.nan)
    d["vw_pend60_X"] = (vw - vw[np.maximum(np.arange(390) - 60, 0)]) / X
    l60 = np.zeros(390)
    for m in range(390):
        w = slice(max(0, m - 59), m + 1); l60[m] = float(np.mean(np.sign(cm[w] - vw[w]) == lado[m]))
    d["vw_lado60"] = l60
    est = np.zeros(K.NR, np.int8); s_ = 0
    arriba = c >= vw_s + TK; abajo = c <= vw_s - TK
    for t in range(K.NR):
        if arriba[t]: s_ = 1
        elif abajo[t]: s_ = -1
        est[t] = s_
    cambio = np.r_[0, (np.diff(est) != 0) & (est[1:] != 0) & (est[:-1] != 0)].astype(int)
    cc = np.cumsum(cambio)
    d["vw_cruces60"] = cc[sec] - cc[np.maximum(sec - 3600, 0)]
    toque = (l <= vw_s + TK) & (h >= vw_s - TK)
    ult = np.maximum.accumulate(np.where(toque, np.arange(K.NR), -1))
    d["min_desde_toque"] = np.where(ult[sec] >= 0, (sec - ult[sec]) / 60.0, (sec + 1) / 60.0)
    # marea (4X): pierna en curso, ultimas dos completas, pivotes desde las 9:30
    P = R["m_piv"]; mdir = R["m_dir"][sec]; mext = R["m_ext"][sec]; mp0 = R["m_p0"][sec]
    d["marea_dir"] = mdir
    d["marea_pierna_X"] = np.abs(mext - mp0) / X
    ult2 = np.full(390, np.nan); npiv = np.zeros(390, int)
    for m in range(390):
        ok = P[:, 3] <= sec[m] if len(P) else np.zeros(0, bool)
        Q = P[ok] if len(P) else P
        npiv[m] = int(((Q[:, 3] >= 0)).sum()) if len(Q) else 0
        if len(Q) >= 3:
            ult2[m] = max(abs(Q[-1, 1] - Q[-2, 1]), abs(Q[-2, 1] - Q[-3, 1])) / X
    d["marea_ult2_max_X"] = ult2; d["marea_pivotes_rth"] = npiv
    # delta acumulado de la rueda contra el precio
    cum = R["cum"]; cvd = cum[sec] - (cum[0] - R["delta"][0])
    d["cvd"] = cvd
    corr = np.full(390, np.nan)
    for m in range(14, 390):
        w = slice(max(0, m - 59), m + 1); a = cm[w]; b = cvd[w]
        if a.std() > 0 and b.std() > 0: corr[m] = float(np.corrcoef(a, b)[0, 1])
    d["cvd_corr60"] = corr
    d["cvd_mismo_signo"] = (np.sign(cvd) == np.sign(cm - O)).astype(int)
    # de la vispera y la apertura
    d["ef10"] = ef10_map.get(dia, np.nan)
    dias_ord = sorted(res.keys()); pdia = ultimo_previo(dias_ord, dia)
    d["rango_ayer_rel"] = (res[pdia]["rth_h"] - res[pdia]["rth_l"]) / med if pdia else np.nan
    d["noche_rel"] = (max(niv["asia_h"], niv["lon_h"]) - min(niv["asia_l"], niv["lon_l"])) / med
    d["hueco_rel"] = (O - niv["pdc"]) / med
    # ---------------- etiquetas ex post (solo para medir)
    s1 = np.where(lado != 0, lado, np.nan)
    d["y1"] = (C_ - cm) * s1 / X
    sz = np.where(np.abs(z) > 1, np.sign(z), np.nan)
    d["y1z"] = (C_ - cm) * sz / X
    l2 = np.array([""] * 390, dtype=object)
    for m in range(30, 361, 5):
        S = sec[m]; s = lado[m]
        if s == 0: l2[m] = "ninguno"; continue
        hh = h[S + 1:]; ll = l[S + 1:]; vv = vw_s[S + 1:]
        if s > 0: tq = np.nonzero(ll <= vv)[0]; ex = np.nonzero(hh >= Hm[m] + X / 2)[0]
        else: tq = np.nonzero(hh >= vv)[0]; ex = np.nonzero(ll <= Lm[m] - X / 2)[0]
        a = tq[0] if len(tq) else 10 ** 9; b = ex[0] if len(ex) else 10 ** 9
        l2[m] = "ninguno" if a == b == 10 ** 9 else ("vuelve" if a < b else ("extiende" if b < a else "empate"))
    d["L2"] = l2
    rng_dia = meta["rth_h"] - meta["rth_l"]
    e = abs(C_ - O) / rng_dia if rng_dia > 0 else np.nan
    d["e_dia"] = e; d["L3"] = "tendencia" if e >= 0.6 else ("balance" if e < 0.3 else "mixto")
    d["rango_dia_rel"] = rng_dia / med
    return d


def main():
    t0 = time.time()
    f, g, nses = curvas_previas()
    json.dump({"f": f.tolist(), "g": g.tolist(), "sesiones": nses, "fuente": "MES 5 min antes del 08-09-2026"},
              open(os.path.join(K.SCR, "curvas_previas.json"), "w"))
    print("curvas previas con %d sesiones de MES; f(10:00)=%.3f f(11:00)=%.3f f(12:30)=%.3f f(15:30)=%.3f | g(10:00)=%.3f g(12:30)=%.3f" % (
        nses, f[29], f[89], f[179], f[359], g[29], g[179]), flush=True)
    ef = EV.eficiencia_previa(); res = resumen_es()
    filas = []
    for dia in K.dias_es():
        K.frenar_si_falta_ram(2.0)
        filas.append(rasgos_dia(dia, f, g, ef, res))
        print(dia, K.periodo_de(dia), "%.1f s" % (time.time() - t0), flush=True)
    df = pd.concat(filas, ignore_index=True)
    df.to_parquet(os.path.join(K.SCR, "regimen_minuto.parquet"), index=False)
    print("filas", len(df), "ruedas", df.dia.nunique(), "| %.1f s" % (time.time() - t0))


if __name__ == "__main__":
    main()
