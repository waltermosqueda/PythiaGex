# -*- coding: utf-8 -*-
"""RONDA 19, paso 1: rasgos por rueda, TODO causal (con lo anterior a cada instante). Una cinta por vez, prioridad baja.
Por cada jornada (18:00 NY de la vispera a 17:00 NY) arma y guarda en el scratchpad:
  - maquinas ola (X), marea (4X) y micro (X/2) orden por orden (clase Maquina de la R18, mismo resultado);
  - grilla de 1 s de la sesion: precio, volumen, delta, CVD, absorcion en la punta, icebergs, big trades, ventas/compras de 30 s;
  - VWAP de la jornada y RTH con desvio, VWAP semanal y mensual (con el roll U6->Z6 ajustado: +67,00 pts);
  - footprint de 1 min de la rueda con imbalances diagonales 300 % y apilados de 3+;
  - niveles del dia: ayer (H/L/C/POC/VAH/VAL RTH), Asia, Londres, balance inicial y extensiones, semana anterior (POC/VAH/VAL);
  - umbrales por franja de 30 min con las hasta 10 ruedas previas del mismo instrumento (minimo 2);
  - tipo de dia registrado (extension del balance inicial, ruido de Zarattini, area de valor de ayer).
Uso: python -I r19_01_rasgos.py ES            (todas las ES, en orden)
     python -I r19_01_rasgos.py MES-sept      (MES de septiembre: solo muestras para umbrales de la guardada)
     python -I r19_01_rasgos.py MES-guardada  (al final: la guardada)"""
import json, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import r19_comun as C

N = 82800                 # segundos de la jornada (18:00 -> 17:00 NY)
K0 = 21600                # indice del segundo = ny_s + K0
RTH0 = K0 + int(9.5 * 3600); RTH1 = K0 + 16 * 3600     # 55800 .. 79200
NR = RTH1 - RTH0          # 23400
SPREAD_ROLL = 67.00       # Z6 - U6 (ver PREREGISTRO seccion 0)
ULTIMO_U6 = "2026-09-11"  # ultima rueda U6 en las cintas
VMIN_APILADO = {"ES": 30.0, "MES": 18.0}
K_VOL_MES = 1.331         # contratos ES / contratos MES, mediana de 7 jornadas comunes (08 a 17-09), r19_00_inventario.csv
HIST = os.path.join(C.SCR, "hist")
RAS = os.path.join(C.SCR, "rasgos")
for _d in (HIST, RAS):
    os.makedirs(_d, exist_ok=True)


# ------------------------------------------------------------------ maquinas
def maquina(mn, mx, cum, X):
    """Zigzag de X orden por orden (semantica de Maquina de r18_01_senal.py). Devuelve por orden: dir, ext, idx del ext, cum del ext,
    cantidad de pivotes; y la lista de pivotes (idx_ext, precio, tipo +1 max / -1 min, idx_confirmacion, cum_ext)."""
    n = len(mn)
    D = np.zeros(n, np.int8); E = np.full(n, np.nan); EI = np.full(n, -1, np.int64); EC = np.zeros(n); NP = np.zeros(n, np.int32)
    piv = []
    dr = 0; ext = None; ei = -1; ec = 0.0
    lo = hi = None; lo_i = hi_i = -1; lo_c = hi_c = 0.0
    mnl = mn.tolist(); mxl = mx.tolist(); cl = cum.tolist()
    Dl = [0] * n; El = [np.nan] * n; EIl = [-1] * n; ECl = [0.0] * n; NPl = [0] * n
    npv = 0
    for i in range(n):
        a = mnl[i]; b = mxl[i]; c = cl[i]
        if dr == 0:
            if lo is None or a < lo: lo, lo_i, lo_c = a, i, c
            if hi is None or b > hi: hi, hi_i, hi_c = b, i, c
            if hi - lo >= X:
                if hi_i > lo_i:
                    piv.append((lo_i, lo, -1, i, lo_c)); dr = 1; ext, ei, ec = hi, hi_i, hi_c
                else:
                    piv.append((hi_i, hi, 1, i, hi_c)); dr = -1; ext, ei, ec = lo, lo_i, lo_c
                npv += 1
        elif dr == 1:
            if b > ext: ext, ei, ec = b, i, c
            elif ext - a >= X:
                piv.append((ei, ext, 1, i, ec)); dr = -1; ext, ei, ec = a, i, c; npv += 1
        else:
            if a < ext: ext, ei, ec = a, i, c
            elif b - ext >= X:
                piv.append((ei, ext, -1, i, ec)); dr = 1; ext, ei, ec = b, i, c; npv += 1
        Dl[i] = dr; NPl[i] = npv
        if ext is not None: El[i] = ext; EIl[i] = ei; ECl[i] = ec
    D[:] = Dl; E[:] = El; EI[:] = EIl; EC[:] = ECl; NP[:] = NPl
    return D, E, EI, EC, NP, piv


# ------------------------------------------------------------------ historia previa (velas de 5 min de ES continuo, U6)
def velas_m5():
    v = pd.read_csv(os.path.join(C.VELAS, "velas-ES-m5.csv"))
    v["t"] = pd.to_datetime(v["t"])
    off = v["t"].apply(lambda x: 4 if x < pd.Timestamp("2026-11-01 06:00") else 5)
    v["ny"] = v["t"] - pd.to_timedelta(off, unit="h")
    v["rueda"] = (v["ny"] + pd.Timedelta(hours=6)).dt.strftime("%Y-%m-%d")   # 18:00 NY -> dia siguiente
    v["hm"] = v["ny"].dt.hour + v["ny"].dt.minute / 60.0
    return v


def explotar_ticks(pri, ult, vol, lado=None):
    """Reparte cada orden parejo entre los ticks que recorrio (aproximacion declarada de la R17). Devuelve tick, v (y lado)."""
    p0 = np.rint(pri / C.TICK).astype(np.int64); p1 = np.rint(ult / C.TICK).astype(np.int64)
    n = np.abs(p1 - p0) + 1
    rep = np.repeat(np.arange(len(pri)), n)
    paso = np.where(p1 >= p0, 1, -1)
    off = np.arange(len(rep)) - np.repeat(np.cumsum(n) - n, n)
    tick = p0[rep] + off * paso[rep]
    v = vol[rep] / n[rep]
    if lado is None:
        return tick, v, rep
    return tick, v, lado[rep], rep


def perfil(tick, v):
    """dict tick0, arr (volumen por tick contiguo)."""
    if len(tick) == 0:
        return None
    t0 = int(tick.min()); arr = np.zeros(int(tick.max()) - t0 + 1)
    np.add.at(arr, tick - t0, v)
    return {"t0": t0, "arr": arr}


def sumar_perfiles(ps, corrimiento_ticks=None):
    ps = [p for p in ps if p is not None]
    if corrimiento_ticks is None: corrimiento_ticks = [0] * len(ps)
    t0 = min(p["t0"] + c for p, c in zip(ps, corrimiento_ticks)); t1 = max(p["t0"] + c + len(p["arr"]) for p, c in zip(ps, corrimiento_ticks))
    arr = np.zeros(t1 - t0)
    for p, c in zip(ps, corrimiento_ticks):
        a = p["t0"] + c - t0; arr[a:a + len(p["arr"])] += p["arr"]
    return {"t0": t0, "arr": arr}


def area_valor(vol, pct=70, paso=2):
    """Copia exacta de r17_comun.area_valor (regla de ATAS verificada 1.186/1.186)."""
    con = np.nonzero(vol > 0)[0]
    if len(con) == 0:
        return -1, -1, -1
    bajo, alto = con[0], con[-1]
    vmax = vol.max(); poc = int(np.nonzero(vol == vmax)[0][-1])
    obj = vol[bajo:alto + 1].sum() * pct / 100.0
    acum = vol[poc]; hi = lo = poc
    while acum <= obj and not (hi >= alto and lo <= bajo):
        up = dn = 0.0; hiN, loN = hi, lo
        for _ in range(paso):
            if hiN + 1 <= alto: hiN += 1; up += vol[hiN]
        for _ in range(paso):
            if loN - 1 >= bajo: loN -= 1; dn += vol[loN]
        if up == 0 and dn == 0: hi, lo = hiN, loN
        elif up >= dn: hi = hiN; acum += up
        else: lo = loN; acum += dn
        if acum >= obj: break
    return poc, hi, lo


def va_de(p):
    poc, vah, val = area_valor(p["arr"])
    return {"poc": (p["t0"] + poc) * C.TICK, "vah": (p["t0"] + vah) * C.TICK, "val": (p["t0"] + val) * C.TICK}


def preparar_historia_m5():
    """Resumen por rueda de las velas de 5 min (antes del 08-09): rango RTH, H/L/C RTH, perfil RTH y ETH, sumas de VWAP, ruido."""
    v = velas_m5()
    out = {}
    for r, g in v.groupby("rueda"):
        if r >= "2026-09-08":
            continue
        rth = g[(g["hm"] >= 9.5) & (g["hm"] < 16.0)]
        tp = (g["h"] + g["l"] + g["c"]) / 3.0
        # perfil: volumen de cada vela parejo en su rango
        def perf(gg):
            if len(gg) == 0: return None
            lo = np.rint(gg["l"].to_numpy() / C.TICK).astype(np.int64); hi = np.rint(gg["h"].to_numpy() / C.TICK).astype(np.int64)
            n = hi - lo + 1; rep = np.repeat(np.arange(len(gg)), n); off = np.arange(len(rep)) - np.repeat(np.cumsum(n) - n, n)
            return perfil(lo[rep] + off, (gg["vol"].to_numpy("float64") / n)[rep])
        d = {"eth_pv": float((tp * g["vol"]).sum()), "eth_p2v": float((tp * tp * g["vol"]).sum()), "eth_v": float(g["vol"].sum()),
             "perfil_eth": perf(g)}
        if len(rth) >= 70:      # rueda completa (el 07-09, feriado, no tiene)
            o = float(rth["o"].iloc[0]); m = ((rth["hm"] - 9.5) * 60).round().astype(int).to_numpy()
            ruido = np.full(390, np.nan)
            for mi, cc in zip(m, rth["c"].to_numpy()):
                ruido[min(389, mi + 4):min(390, mi + 5)] = abs(cc - o)
            ruido = pd.Series(ruido).bfill().ffill().to_numpy()
            d.update({"rth_h": float(rth["h"].max()), "rth_l": float(rth["l"].min()), "rth_c": float(rth["c"].iloc[-1]), "rth_o": o,
                      "perfil_rth": perf(rth), "ruido": ruido})
        out[r] = d
    return out


# ------------------------------------------------------------------ una jornada
def franja_de_seg(k):
    return np.clip((k - RTH0) // 1800, 0, 12)


def rolling(a, w):
    cs = np.concatenate([[0.0], np.cumsum(a, dtype="float64")])
    r = cs[w:] - cs[:-w]
    return np.concatenate([cs[1:w], r])        # ventana que termina en t (inclusive); al principio, lo que hay


def umbral(muestras_prev, clave, q):
    """Percentil q por franja (13) con las muestras de las ruedas previas (lista de dicts con arrays de 23.400 s)."""
    out = np.full(13, np.nan)
    if len(muestras_prev) < 2:
        return out
    fr = franja_de_seg(np.arange(RTH0, RTH1))
    for f in range(13):
        vals = np.concatenate([m[clave][fr == f] for m in muestras_prev])
        if len(vals): out[f] = np.quantile(vals, q)
    return out


def umbral_tamano(muestras_prev, q=0.99):
    out = np.full(13, np.nan)
    if len(muestras_prev) < 2:
        return out
    H = sum(m["hist_tam"] for m in muestras_prev)
    for f in range(13):
        c = np.cumsum(H[f]); tot = c[-1]
        if tot > 0: out[f] = float(np.searchsorted(c, q * tot) )
    return out


def jornada(inst, dia, X, prev_res, muestras_prev, carry_sem, carry_mes, ruido_prev, sem_prev_perfil, k_vol=1.0):
    d = C.leer(inst, dia)
    s = d["ny_s"].to_numpy(); pri = d["primero"].to_numpy(); ult = d["ultimo"].to_numpy()
    vol = d["vol"].to_numpy("float64"); lado = d["lado"].to_numpy().astype(np.int64)
    abid, abidv, aask, aaskv = (d[c].to_numpy("float64") for c in ("abid", "abidv", "aask", "aaskv"))
    dbid, dbidv, dask, daskv = (d[c].to_numpy("float64") for c in ("dbid", "dbidv", "dask", "daskv"))
    mn = np.minimum(pri, ult); mx = np.maximum(pri, ult)
    sv = vol * lado; cum = np.cumsum(sv)
    n = len(d)
    k = np.clip(np.floor(s).astype(np.int64) + K0, 0, N - 1)

    # --- maquinas
    oD, oE, oEI, oEC, oNP, opiv = maquina(mn, mx, cum, X)
    mD, mE, mEI, mEC, mNP, mpiv = maquina(mn, mx, cum, 4 * X)
    uD, uE, uEI, uEC, uNP, upiv = maquina(mn, mx, cum, X / 2.0)

    # --- grilla de 1 s
    last = np.r_[np.nonzero(np.diff(k))[0], n - 1]
    li = np.full(N, -1, np.int64); li[k[last]] = last
    carry = np.maximum.accumulate(li)
    ok = carry >= 0
    c_s = np.where(ok, ult[np.maximum(carry, 0)], np.nan)
    c_s = pd.Series(c_s).bfill().to_numpy()
    h_s = np.full(N, -np.inf); np.maximum.at(h_s, k, mx); l_s = np.full(N, np.inf); np.minimum.at(l_s, k, mn)
    sin = ~np.isfinite(h_s); h_s[sin] = c_s[sin]; l_s[sin] = c_s[sin]
    bc = lambda w: np.bincount(k, weights=w, minlength=N)
    vol_s = bc(vol); delta_s = bc(sv); compra_s = bc(np.where(lado == 1, vol, 0.0)); venta_s = bc(np.where(lado == -1, vol, 0.0))
    cum_s = np.cumsum(delta_s)
    # absorcion en la punta: agresivas que NO movieron la punta (bid despues >= bid antes para ventas; ask despues <= ask antes para compras)
    absb_s = bc(np.where((lado == -1) & (dbid >= abid), vol, 0.0)); absa_s = bc(np.where((lado == 1) & (dask <= aask), vol, 0.0))
    # iceberg: se comio mas de lo visible y la punta quedo en el MISMO precio con tamano >= 1 (se recargo)
    iceb = (lado == -1) & (vol > abidv) & (dbid == abid) & (dbidv >= 1)
    icea = (lado == 1) & (vol > aaskv) & (dask == aask) & (daskv >= 1)
    iceb_px = np.full(N, np.nan); icea_px = np.full(N, np.nan)
    if iceb.any():
        tmp = np.full(N, np.inf); np.minimum.at(tmp, k[iceb], ult[iceb]); iceb_px = np.where(np.isfinite(tmp), tmp, np.nan)
    if icea.any():
        tmp = np.full(N, -np.inf); np.maximum.at(tmp, k[icea], ult[icea]); icea_px = np.where(np.isfinite(tmp), tmp, np.nan)
    # big trades: umbral p99 de tamano por franja (ruedas previas)
    ubig = umbral_tamano(muestras_prev, 0.99)
    rth_o = (k >= RTH0) & (k < RTH1)
    fr_o = franja_de_seg(k)
    thr_o = np.where(rth_o, ubig[fr_o], np.inf)
    thr_o = np.where(np.isnan(thr_o), np.inf, thr_o)
    big = vol >= thr_o
    bigc_s = bc(np.where(big & (lado == 1), vol, 0.0)); bigv_s = bc(np.where(big & (lado == -1), vol, 0.0))

    # --- VWAP (precio medio de la orden)
    pm = (pri + ult) / 2.0
    pv_s = bc(pm * vol); p2v_s = bc(pm * pm * vol)
    def vw(cpv, cp2v, cv):
        with np.errstate(invalid="ignore", divide="ignore"):
            w = cpv / cv; sd = np.sqrt(np.maximum(cp2v / cv - w * w, 0.0))
        return w, sd
    cpv = np.cumsum(pv_s); cp2v = np.cumsum(p2v_s); cvv = np.cumsum(vol_s)
    vw_j, sd_j = vw(cpv, cp2v, cvv)
    r_pv = np.cumsum(np.where(np.arange(N) >= RTH0, pv_s, 0)); r_p2v = np.cumsum(np.where(np.arange(N) >= RTH0, p2v_s, 0))
    r_v = np.cumsum(np.where(np.arange(N) >= RTH0, vol_s, 0))
    vw_r, sd_r = vw(r_pv, r_p2v, r_v)
    # semanal y mensual: el volumen de MES se lleva a "volumen equivalente de ES" (k_vol = ES/MES medido el 08-17-09) para no
    # mezclar escalas cuando el mes junta jornadas de ES (02-13-11) y de MES (16-25-11); en ES k_vol = 1
    vw_sem, _ = vw(carry_sem["pv"] + k_vol * cpv, carry_sem["p2v"] + k_vol * cp2v, carry_sem["v"] + k_vol * cvv)
    vw_mes, _ = vw(carry_mes["pv"] + k_vol * cpv, carry_mes["p2v"] + k_vol * cp2v, carry_mes["v"] + k_vol * cvv)

    # --- niveles estaticos
    asia = (s < 3 * 3600); londres = (s >= 3 * 3600) & (s < 9.5 * 3600)
    rthm = (s >= 9.5 * 3600) & (s < 16 * 3600)
    ibm = (s >= 9.5 * 3600) & (s < 10.5 * 3600)
    niv = {"asia_h": float(mx[asia].max()), "asia_l": float(mn[asia].min()), "lon_h": float(mx[londres].max()), "lon_l": float(mn[londres].min()),
           "ib_h": float(mx[ibm].max()), "ib_l": float(mn[ibm].min())}
    ibr = niv["ib_h"] - niv["ib_l"]
    niv.update({"ib_h05": niv["ib_h"] + 0.5 * ibr, "ib_h10": niv["ib_h"] + ibr, "ib_l05": niv["ib_l"] - 0.5 * ibr, "ib_l10": niv["ib_l"] - ibr})
    niv.update({"pdh": prev_res["rth_h"], "pdl": prev_res["rth_l"], "pdc": prev_res["rth_c"], "pd_poc": prev_res["poc"], "pd_vah": prev_res["vah"],
                "pd_val": prev_res["val"]})
    if sem_prev_perfil is not None:
        va = va_de(sem_prev_perfil); niv.update({"ps_poc": va["poc"], "ps_vah": va["vah"], "ps_val": va["val"]})
    # resumen de hoy (para manana)
    tick_r, v_r, _ = explotar_ticks(pri[rthm], ult[rthm], vol[rthm])
    p_rth = perfil(tick_r, v_r); va_r = va_de(p_rth)
    tick_e, v_e, _ = explotar_ticks(pri, ult, vol); p_eth = perfil(tick_e, v_e)
    rth_idx = np.nonzero(rthm)[0]
    hoy = {"rth_h": float(mx[rthm].max()), "rth_l": float(mn[rthm].min()), "rth_c": float(ult[rth_idx[-1]]), "rth_o": float(ult[rth_idx[0]]),
           "poc": va_r["poc"], "vah": va_r["vah"], "val": va_r["val"], "perfil_eth": p_eth,
           "eth_pv": float(pv_s.sum()), "eth_p2v": float(p2v_s.sum()), "eth_v": float(vol_s.sum())}
    # ruido (Zarattini): |cierre del minuto - apertura RTH|
    ks = RTH0 + 60 * np.arange(1, 391) - 1
    hoy["ruido"] = np.abs(c_s[ks] - hoy["rth_o"])

    # --- footprint de 1 min de la rueda: imbalances diagonales y apilados
    rr = np.nonzero(rthm)[0]
    tk, vv, ll, rep = explotar_ticks(pri[rr], ult[rr], vol[rr], lado[rr])
    bar = ((s[rr][rep] - 9.5 * 3600) // 60).astype(np.int64)
    fp = pd.DataFrame({"bar": bar, "tick": tk, "c": np.where(ll == 1, vv, 0.0), "v": np.where(ll == -1, vv, 0.0)}).groupby(["bar", "tick"]).sum()
    vmin = VMIN_APILADO[inst]
    ap_c_lo = np.full(390, np.nan); ap_v_hi = np.full(390, np.nan); n_ap_c = np.zeros(390, np.int32); n_ap_v = np.zeros(390, np.int32)
    imb_c = np.zeros(390, np.int32); imb_v = np.zeros(390, np.int32)
    for b, g in fp.groupby(level=0):
        if b < 0 or b >= 390: continue
        t = g.index.get_level_values(1).to_numpy(); t0 = t.min(); L = t.max() - t0 + 1
        A = np.zeros(L + 2); B = np.zeros(L + 2)
        A[t - t0 + 1] = g["c"].to_numpy(); B[t - t0 + 1] = g["v"].to_numpy()
        # compra en p: ask[p] >= 3 * bid[p-1] ; venta en p: bid[p] >= 3 * ask[p+1]
        bc_ = (A[1:L + 1] >= 3.0 * B[0:L]) & (A[1:L + 1] >= vmin)
        bv_ = (B[1:L + 1] >= 3.0 * A[2:L + 2]) & (B[1:L + 1] >= vmin)
        imb_c[b] = int(bc_.sum()); imb_v[b] = int(bv_.sum())
        for arr, cual in ((bc_, "c"), (bv_, "v")):
            run = 0; lo_run = []
            for j, x in enumerate(arr):
                run = run + 1 if x else 0
                if run == 3: lo_run.append((j - 2, j))
                elif run > 3: lo_run[-1] = (lo_run[-1][0], j)
            if lo_run:
                if cual == "c":
                    n_ap_c[b] = len(lo_run); ap_c_lo[b] = (t0 + min(a for a, _ in lo_run)) * C.TICK
                else:
                    n_ap_v[b] = len(lo_run); ap_v_hi[b] = (t0 + max(e for _, e in lo_run)) * C.TICK

    # --- estados por segundo (rueda) y ordenes de la rueda (para salidas)
    cr = carry[RTH0:RTH1]
    def est(arr):
        return arr[np.maximum(cr, 0)]
    # ola: pivote que inicio la pierna actual (precio e indice de orden)
    def pivote_inicio(piv, NP_):
        pp = np.array([p[1] for p in piv] + [np.nan]); pi = np.array([p[0] for p in piv] + [-1], np.int64)
        idx = NP_[np.maximum(cr, 0)] - 1
        return np.where(idx >= 0, pp[idx], np.nan), np.where(idx >= 0, pi[idx], -1), np.array([p[4] for p in piv] + [0.0])[idx]
    o_p0, o_i0, o_c0 = pivote_inicio(opiv, oNP)
    m_p0, m_i0, m_c0 = pivote_inicio(mpiv, mNP)
    seg_de_orden = k   # segundo de cada orden (indice de grilla)
    R = {
        "c": c_s[RTH0:RTH1], "h": h_s[RTH0:RTH1], "l": l_s[RTH0:RTH1], "vol": vol_s[RTH0:RTH1], "delta": delta_s[RTH0:RTH1],
        "cum": cum_s[RTH0:RTH1], "compra": compra_s[RTH0:RTH1], "venta": venta_s[RTH0:RTH1],
        "absb60": rolling(absb_s, 60)[RTH0:RTH1], "absa60": rolling(absa_s, 60)[RTH0:RTH1],
        "iceb_px": iceb_px[RTH0:RTH1], "icea_px": icea_px[RTH0:RTH1],
        "netbig60": rolling(bigc_s - bigv_s, 60)[RTH0:RTH1], "bigc": bigc_s[RTH0:RTH1], "bigv": bigv_s[RTH0:RTH1],
        "venta30": rolling(venta_s, 30)[RTH0:RTH1], "compra30": rolling(compra_s, 30)[RTH0:RTH1],
        "vw_j": vw_j[RTH0:RTH1], "sd_j": sd_j[RTH0:RTH1], "vw_r": vw_r[RTH0:RTH1], "sd_r": sd_r[RTH0:RTH1],
        "vw_sem": vw_sem[RTH0:RTH1], "vw_mes": vw_mes[RTH0:RTH1],
        "o_dir": est(oD), "o_ext": est(oE), "o_ext_seg": seg_de_orden[est(oEI)] - RTH0, "o_ext_cum": est(oEC), "o_np": est(oNP),
        "o_p0": o_p0, "o_seg0": np.where(o_i0 >= 0, seg_de_orden[np.maximum(o_i0, 0)] - RTH0, -999999),
        "m_dir": est(mD), "m_ext": est(mE), "m_np": est(mNP), "m_p0": m_p0, "m_c0": m_c0,
        "u_np": est(uNP),
        "ap_c_lo": ap_c_lo, "ap_v_hi": ap_v_hi, "n_ap_c": n_ap_c, "n_ap_v": n_ap_v, "imb_c": imb_c, "imb_v": imb_v,
    }
    # micro pivotes (X/2) de toda la jornada: segundo del extremo, precio, tipo, segundo de confirmacion, cum
    R["u_piv"] = np.array([(seg_de_orden[p[0]] - RTH0, p[1], p[2], seg_de_orden[p[3]] - RTH0, p[4]) for p in upiv], dtype="float64").reshape(-1, 5)
    R["m_piv"] = np.array([(seg_de_orden[p[0]] - RTH0, p[1], p[2], seg_de_orden[p[3]] - RTH0, p[4]) for p in mpiv], dtype="float64").reshape(-1, 5)
    R["o_piv"] = np.array([(seg_de_orden[p[0]] - RTH0, p[1], p[2], seg_de_orden[p[3]] - RTH0, p[4]) for p in opiv], dtype="float64").reshape(-1, 5)
    # ordenes de la rueda (y hasta las 16:00) para simular salidas: segundo, max, min, ultimo, marea dir/ext
    oi = np.nonzero((k >= RTH0) & (k < RTH1))[0]
    R.update({"or_seg": (k[oi] - RTH0).astype(np.int32), "or_hi": mx[oi], "or_lo": mn[oi], "or_ult": ult[oi], "or_mdir": mD[oi], "or_mext": mE[oi]})
    # umbrales por franja (ruedas previas)
    fr = franja_de_seg(np.arange(RTH0, RTH1))
    for clave, q, nombre in (("absb60", 0.90, "u_absb60"), ("absa60", 0.90, "u_absa60"), ("netbig60", 0.80, "u_netbig60_p80"),
                             ("netbig60", 0.20, "u_netbig60_p20"), ("venta30", 0.995, "u_venta30"), ("compra30", 0.995, "u_compra30")):
        R[nombre] = umbral(muestras_prev, clave, q)[fr]
    R["u_big"] = ubig[fr]
    # tipo de dia
    R["ruido_sigma"] = (np.nanmean(np.vstack(ruido_prev), axis=0) if len(ruido_prev) else np.full(390, np.nan))
    meta = {"inst": inst, "dia": dia, "X": X, "niv": niv, "n_ordenes": int(n), "n_previas_umbral": len(muestras_prev),
            "rth_o": hoy["rth_o"], "rth_h": hoy["rth_h"], "rth_l": hoy["rth_l"], "rth_c": hoy["rth_c"]}
    # muestras de hoy para los umbrales de manana
    hist = np.zeros((13, 3001))
    vr = np.clip(vol[rth_o].astype(np.int64), 0, 3000); fo = fr_o[rth_o]
    np.add.at(hist, (fo, vr), 1)
    mues = {"hist_tam": hist}
    for clave in ("absb60", "absa60", "venta30", "compra30"):
        mues[clave] = R[clave].astype("float32")
    # netbig de las muestras con el p99 de la PROPIA rueda (es pasado para las ruedas que lo usan; evita umbrales en 0 al arrancar)
    upropio = np.full(13, np.nan)
    for f in range(13):
        cc = np.cumsum(hist[f]); tot = cc[-1]
        if tot > 0: upropio[f] = float(np.searchsorted(cc, 0.99 * tot))
    thr_p = np.where(rth_o, upropio[fr_o], np.inf); thr_p = np.where(np.isnan(thr_p), np.inf, thr_p)
    bigp = vol >= thr_p
    nb_p = bc(np.where(bigp & (lado == 1), vol, 0.0)) - bc(np.where(bigp & (lado == -1), vol, 0.0))
    mues["netbig60"] = rolling(nb_p, 60)[RTH0:RTH1].astype("float32")
    return R, meta, hoy, mues


def guardar(inst, dia, R, meta, mues):
    np.savez_compressed(os.path.join(RAS, "%s-%s.npz" % (inst, dia)), **R)
    json.dump(meta, open(os.path.join(RAS, "%s-%s.json" % (inst, dia)), "w"))
    np.savez_compressed(os.path.join(HIST, "mues-%s-%s.npz" % (inst, dia)), **mues)


def cargar_muestras(inst, dia_list):
    out = []
    for dd in dia_list:
        f = os.path.join(HIST, "mues-%s-%s.npz" % (inst, dd))
        if os.path.exists(f):
            z = np.load(f); out.append({k: z[k] for k in z.files})
    return out


def X_de(rangos_previos):
    m = float(np.median(rangos_previos[-10:]))
    return float(np.floor(0.08 * m / C.TICK + 0.5) * C.TICK), m


def main(modo):
    t_ini = time.time()
    m5 = preparar_historia_m5()
    # resumen de ruedas por instrumento (se completa en orden)
    res_f = os.path.join(HIST, "resumen.json")
    resumen = {}       # (inst|dia) -> dict sin perfiles
    perfiles = {}      # (inst|dia) -> perfil_eth
    ruidos = {}
    for r, dd in m5.items():
        if "rth_h" in dd:
            resumen["ES|" + r] = {kk: dd[kk] for kk in ("rth_h", "rth_l", "rth_c", "rth_o")}
            pr = dd["perfil_rth"]; va = va_de(pr); resumen["ES|" + r].update(va)
            ruidos["ES|" + r] = dd["ruido"]
        perfiles["ES|" + r] = dd["perfil_eth"]
        resumen.setdefault("ES|" + r, {}).update({"eth_pv": dd["eth_pv"], "eth_p2v": dd["eth_p2v"], "eth_v": dd["eth_v"], "contrato": "U6"})
    if modo == "ES":
        lista = [("ES", x) for x in C.dias("ES")]
    elif modo == "MES-sept":
        lista = [("MES", x) for x in C.dias("MES") if x < "2026-10-01"]
    elif modo == "MES-guardada":
        lista = [("MES", x) for x in C.dias("MES") if x >= "2026-11-16"]
    elif modo == "ES-guardada":      # juicio final (enmienda 27-11 03:55): solo las jornadas nuevas, con la historia ya calculada
        lista = [("ES", x) for x in C.dias("ES") if x >= "2026-11-16"]
    else:
        raise SystemExit("modo desconocido")
    # para MES-guardada hacen falta los resumenes de ES (dias previos) ya calculados
    if os.path.exists(res_f) and modo != "ES":
        viejo = json.load(open(res_f))
        for kk, vv in viejo.items():
            resumen.setdefault(kk, {}).update(vv)
        for kk in viejo:
            pf = os.path.join(HIST, "perfil-%s.npz" % kk.replace("|", "-"))
            if os.path.exists(pf):
                z = np.load(pf); perfiles[kk] = {"t0": int(z["t0"]), "arr": z["arr"]}
            rf = os.path.join(HIST, "ruido-%s.npy" % kk.replace("|", "-"))
            if os.path.exists(rf): ruidos[kk] = np.load(rf)
    for inst, dia in lista:
        C.frenar_si_falta_ram(2.0)
        t0 = time.time()
        # ruedas previas (con rango RTH) de cualquier fuente del mismo precio (ES/MES son el mismo indice)
        previas = sorted(set(kk.split("|")[1] for kk, vv in resumen.items() if "rth_h" in vv and kk.split("|")[1] < dia))
        if inst == "MES" and dia < "2026-10-01":
            previas = [p for p in previas if p < dia]
        rangos = [resumen[("ES|" if ("ES|" + p) in resumen and "rth_h" in resumen["ES|" + p] else "MES|") + p]["rth_h"] -
                  resumen[("ES|" if ("ES|" + p) in resumen and "rth_h" in resumen["ES|" + p] else "MES|") + p]["rth_l"] for p in previas]
        X, med = X_de(rangos)
        pd_dia = previas[-1]
        clave_prev = ("ES|" if ("ES|" + pd_dia) in resumen and "rth_h" in resumen["ES|" + pd_dia] else "MES|") + pd_dia
        prev = dict(resumen[clave_prev])
        def contrato(dd): return "U6" if dd <= ULTIMO_U6 else "Z6"
        aj = SPREAD_ROLL if (contrato(pd_dia) == "U6" and contrato(dia) == "Z6") else 0.0
        for kk in ("rth_h", "rth_l", "rth_c", "poc", "vah", "val"):
            prev[kk] = prev[kk] + aj
        # semana anterior (ISO) y carries de semana y mes
        sem = pd.Timestamp(dia).isocalendar()[:2]
        todas = sorted(set(kk.split("|")[1] for kk in resumen) | set(kk.split("|")[1] for kk in perfiles))
        def clave_de(dd):
            for pref in ("ES|", "MES|"):
                if (pref + dd) in perfiles: return pref + dd
            return None
        sem_prev = [dd for dd in todas if dd < dia and tuple(pd.Timestamp(dd).isocalendar()[:2]) == (sem[0], sem[1] - 1)]
        if sem[1] == 1: sem_prev = []
        ps = [perfiles[clave_de(dd)] for dd in sem_prev if clave_de(dd)]
        sh = [int(round((SPREAD_ROLL if (contrato(dd) == "U6" and contrato(dia) == "Z6") else 0.0) / C.TICK)) for dd in sem_prev if clave_de(dd)]
        sem_prev_perfil = sumar_perfiles(ps, sh) if ps else None
        def carry(dd_list):
            pv = p2v = v = 0.0
            for dd in dd_list:
                kk = clave_de(dd) or ("ES|" + dd)
                rr = resumen.get(kk) or resumen.get("ES|" + dd)
                if not rr or "eth_v" not in rr: continue
                a = SPREAD_ROLL if (contrato(dd) == "U6" and contrato(dia) == "Z6") else 0.0
                escala = rr.get("escala_vol", 1.0)
                pv += escala * (rr["eth_pv"] + a * rr["eth_v"]); p2v += escala * (rr["eth_p2v"] + 2 * a * rr["eth_pv"] + a * a * rr["eth_v"]); v += escala * rr["eth_v"]
            return {"pv": pv, "p2v": p2v, "v": v}
        sem_act = [dd for dd in todas if dd < dia and tuple(pd.Timestamp(dd).isocalendar()[:2]) == tuple(sem)]
        mes_act = [dd for dd in todas if dd < dia and dd[:7] == dia[:7]]
        c_sem = carry(sem_act); c_mes = carry(mes_act)
        # umbrales: hasta 10 ruedas previas del MISMO instrumento con muestras
        prev_mismo = sorted(os.path.basename(f)[len("mues-%s-" % inst):-4] for f in
                            __import__("glob").glob(os.path.join(HIST, "mues-%s-*.npz" % inst)))
        prev_mismo = [p for p in prev_mismo if p < dia][-10:]
        mp = cargar_muestras(inst, prev_mismo)
        rp = [ruidos[kk] for kk in sorted(ruidos, key=lambda z: z.split("|")[1]) if kk.split("|")[1] < dia][-10:]
        k_vol = K_VOL_MES if inst == "MES" else 1.0
        R, meta, hoy, mues = jornada(inst, dia, X, prev, mp, c_sem, c_mes, rp, sem_prev_perfil, k_vol)
        meta.update({"mediana_rango_10": med, "dia_previo": pd_dia, "ajuste_roll_previo": aj, "semana_previa": sem_prev,
                     "ajuste_roll_semana": sorted(set(sh)), "carry_sem_dias": sem_act, "carry_mes_dias": mes_act, "umbral_dias": prev_mismo,
                     "contrato": contrato(dia)})
        guardar(inst, dia, R, meta, mues)
        kk = inst + "|" + dia
        resumen[kk] = {x: hoy[x] for x in ("rth_h", "rth_l", "rth_c", "rth_o", "poc", "vah", "val", "eth_pv", "eth_p2v", "eth_v")}
        resumen[kk]["contrato"] = contrato(dia); resumen[kk]["escala_vol"] = k_vol
        perfiles[kk] = hoy["perfil_eth"]; ruidos[kk] = hoy["ruido"]
        np.savez_compressed(os.path.join(HIST, "perfil-%s-%s.npz" % (inst, dia)), t0=hoy["perfil_eth"]["t0"], arr=hoy["perfil_eth"]["arr"])
        np.save(os.path.join(HIST, "ruido-%s-%s.npy" % (inst, dia)), hoy["ruido"])
        json.dump({kk2: {x: y for x, y in vv.items() if not isinstance(y, (dict, np.ndarray))} for kk2, vv in resumen.items()}, open(res_f, "w"))
        print("%s %s X %.2f (mediana %.2f) prev %s aj %.2f | ordenes %d | umbral con %d ruedas | %.1f s | RAM libre %.1f GB" %
              (inst, dia, X, med, pd_dia, aj, meta["n_ordenes"], len(mp), time.time() - t0, C.ram_libre_gb()), flush=True)
    print("total %.1f s" % (time.time() - t_ini))


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "ES")
