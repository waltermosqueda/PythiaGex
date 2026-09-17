# -*- coding: utf-8 -*-
"""
gamma_lib.py — familia REGIMEN Y NIVELES DE GAMMA x FLUJO (busqueda de gatillo, 17-09).

Traduccion literal del pre-registro de resultados/gamma.md. Todo rasgo en el segundo t usa SOLO filas <= t; los umbrales son percentiles
MOVILES de la ventana previa [t-w, t-1]. Los niveles de gamma de la vela m-1 valen durante el minuto m (nunca el de la propia vela), y se
llevan al segundo con el movimiento de la cinta: dist[t] = d_nivel[m-1] + (ultimo[t] - cierre_cinta[m-1]). Asi no importa si el grafico
estaba en otro contrato que la cinta (14-09: 298 puntos de diferencia por el roll).

PARTICION ALTERNADA (excepcion de esta familia): sesiones ordenadas por fecha; explorar = posiciones pares (0, 2, ...), confirmar = impares.
"""
import os, sys
import numpy as np, pandas as pd

sys.path.insert(0, r"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\gatillo")
import base as B

AQUI = os.path.dirname(os.path.abspath(__file__))
CACHE = os.path.join(AQUI, "gamma_cache"); os.makedirs(CACHE, exist_ok=True)
INICIO_RUEDA = "13:32:00"; FIN_RUEDA = "20:00:00"
SEPARACION = 60
BARRERAS = ((8, 600), (5, 300), (12, 900))
NIV = ["d_zero", "d_dom0", "d_dom1", "d_mp", "d_mn", "d_mc5"]
ZONA = 25.0   # regimen por zero gamma: POS si precio - zero >= +25, NEG si <= -25, en el medio no se opera


def mitades(T):
    s = sorted(T); return {k: T[k] for k in s[0::2]}, {k: T[k] for k in s[1::2]}


def q_previo(x, w, q):
    return x.rolling(w, min_periods=w).quantile(q).shift(1)


_N1 = None


def niveles_corridos():
    """niveles_m1 con el indice corrido +1 min: en el minuto m se conoce lo de la vela m-1. Agrega el NIVEL de Max Change 5 min (en precio
    del grafico) de m-1 y de m-2 para detectar un Max Change nuevo."""
    global _N1
    if _N1 is None:
        N = B.niveles_m1().copy()
        mc = N["c"] - N["d_mc5"]; prev = mc.shift(1); mismo_dia = (N.index.to_series().diff() == pd.Timedelta(minutes=1)).to_numpy()
        N["mc5_salto"] = np.where(mismo_dia, (mc - prev).abs(), np.nan)
        N.index = N.index + pd.Timedelta(minutes=1); _N1 = N
    return _N1


def niveles_seg(d):
    """Distancia (precio - nivel) en cada segundo, cuadrante y salto del Max Change, todo conocido en t."""
    N1 = niveles_corridos(); m = d.index.floor("min")
    cl = d["ultimo"].resample("1min").last().ffill(); cl_prev = cl.shift(1).reindex(m).to_numpy()
    corr = d["ultimo"].to_numpy() - cl_prev
    L = N1.reindex(m)
    out = pd.DataFrame(L[NIV].to_numpy() + corr[:, None], index=d.index, columns=NIV)
    out["cuadrante"] = L["cuadrante"].to_numpy(); out["mc5_salto"] = L["mc5_salto"].to_numpy()
    return out


def elegible(d):
    hms = d.index.strftime("%H:%M:%S"); e = d["rueda"].to_numpy() & (hms >= INICIO_RUEDA) & (hms < FIN_RUEDA)
    h = d["hueco"].to_numpy().astype("float64"); c = np.concatenate([[0.0], np.cumsum(h)]); n = len(h); i = np.arange(n)
    a = np.clip(i - 600, 0, n); b = np.clip(i + 901, 0, n); sin_hueco = (c[b] - c[a]) == 0
    return e & sin_hueco


def rasgos(d):
    f = pd.DataFrame(index=d.index); p = d["ultimo"]; f["ultimo"] = p
    f["D60"] = d["delta"].rolling(60).sum(); f["M60"] = p - p.shift(60); f["D60_q90"] = q_previo(f["D60"].abs(), 1800, 0.90)
    f["D10"] = d["delta"].rolling(10).sum(); f["M10"] = p - p.shift(10); f["D10_q99"] = q_previo(f["D10"].abs(), 1800, 0.99)
    f["D30"] = d["delta"].rolling(30).sum(); f["M30"] = p - p.shift(30); f["D30_q70"] = q_previo(f["D30"].abs(), 1800, 0.70)
    f["A30"] = (d["abs_bid"] - d["abs_ask"]).rolling(30).sum(); f["A30_q95"] = q_previo(f["A30"].abs(), 1800, 0.95)
    f["P30"] = d["ofi_pas"].rolling(30).sum(); f["P30_q80"] = q_previo(f["P30"].abs(), 1800, 0.80); f["P30_antes"] = f["P30"].shift(30)
    f["M120"] = p - p.shift(120)
    # vela de reloj de 5 min: se juzga en su ULTIMO segundo (usa solo datos <= t)
    g = d.resample("5min", label="left", closed="left"); b = pd.DataFrame({"delta": g["delta"].sum(), "vol": g["vol"].sum()})
    b["r"] = b["delta"] / b["vol"].where(b["vol"] > 0); b["r_q70"] = b["r"].abs().rolling(12, min_periods=12).quantile(0.70).shift(1)
    fin = b.index + pd.Timedelta(minutes=5) - pd.Timedelta(seconds=1)
    for c in ("r", "r_q70"):
        s = pd.Series(b[c].to_numpy(), index=fin); f["b5_" + c] = s.reindex(d.index)
    f["seg"] = d.index.second
    f["elegible"] = elegible(d)
    return f


def candidatos(f, nv):
    """{variante: (mascara, lado)} antes de desagrupar. s = lado del FLUJO; 'lado' = lo que apuesta el gatillo (+1 sube, -1 baja)."""
    e = f["elegible"].to_numpy(); sg = np.sign; out = {}
    z = nv["d_zero"].to_numpy(); POS = z >= ZONA; NEG = z <= -ZONA
    q = nv["cuadrante"].to_numpy(); REV = (q == 1) | (q == 3); MOM = (q == 2) | (q == 4)
    sw_zero = np.where(NEG, 1, np.where(POS, -1, 0))      # +1 = seguir el flujo, -1 = ir en contra
    sw_cuad = np.where(MOM, 1, np.where(REV, -1, 0))
    # ---- A. regimen x rafaga de delta
    D60 = f["D60"].to_numpy(); s60 = sg(D60); raf60 = e & (np.abs(D60) >= f["D60_q90"].to_numpy()) & (s60 * f["M60"].to_numpy() >= 4) & (s60 != 0)
    D10 = f["D10"].to_numpy(); s10 = sg(D10); raf10 = e & (np.abs(D10) >= f["D10_q99"].to_numpy()) & (s10 * f["M10"].to_numpy() >= 1) & (s10 != 0)
    out["V01_RAF60_ZERO"] = (raf60 & (sw_zero != 0), s60 * sw_zero)
    out["V02_RAF60_CUAD"] = (raf60 & (sw_cuad != 0), s60 * sw_cuad)
    out["V03_RAF10_ZERO"] = (raf10 & (sw_zero != 0), s10 * sw_zero)
    out["V04_RAF10_CUAD"] = (raf10 & (sw_cuad != 0), s10 * sw_cuad)
    # ---- B. dominantes x microestructura
    dd = nv[["d_dom0", "d_dom1"]].to_numpy(); ad = np.where(np.isnan(dd), np.inf, np.abs(dd)); k = ad.argmin(axis=1)
    dist = dd[np.arange(len(dd)), k]; adist = ad[np.arange(len(dd)), k]; lado_def = np.where(dist >= 0, 1, -1)   # rebote: compra si el precio esta arriba del nivel
    A30 = f["A30"].to_numpy(); sA = sg(A30)
    absor = e & (np.abs(A30) >= f["A30_q95"].to_numpy()) & (sA != 0)
    out["V05_ABS_DOM"] = (absor & (adist <= 12) & (sA == lado_def), sA)
    D30 = f["D30"].to_numpy(); s30 = sg(D30); M30 = f["M30"].to_numpy()
    empuje = e & (np.abs(D30) >= f["D30_q70"].to_numpy()) & (s30 != 0) & (adist <= 15) & (s30 == -lado_def)     # el flujo empuja CONTRA el nivel
    out["V06_EMPUJE_FRENADO_DOM"] = (empuje & (s30 * M30 <= 0.5), -s30)
    out["V07_EMPUJE_AVANZA_DOM"] = (empuje & (s30 * M30 >= 3), s30)
    # ---- C. majors / zero: el flujo pasivo gira en contra de la llegada
    dm = nv[["d_mp", "d_mn", "d_zero"]].to_numpy(); am = np.where(np.isnan(dm), np.inf, np.abs(dm)); km = am.argmin(axis=1)
    distm = dm[np.arange(len(dm)), km]; adistm = am[np.arange(len(dm)), km]; a = np.where(distm >= 0, -1, 1)   # direccion de la LLEGADA al nivel
    P30 = f["P30"].to_numpy(); Pa = f["P30_antes"].to_numpy()
    out["V08_PASIVO_GIRA_MAJOR"] = (e & (adistm <= 15) & (a * f["M120"].to_numpy() >= 3) & (sg(P30) == -a) & (np.abs(P30) >= f["P30_q80"].to_numpy()) & (sg(Pa) == a), -a)
    # ---- D. rafaga de 60 s hacia una dominante cercana, el regimen decide
    out["V09_RAF60_BORDE_ZERO"] = (raf60 & (sw_zero != 0) & (adist <= 15) & (s60 == -lado_def), s60 * sw_zero)
    # ---- E. Max Change de 5 min NUEVO (salto >= 10 pts), a 10-60 pts del precio, en el segundo :02 del minuto
    mc = nv["d_mc5"].to_numpy(); hacia = np.where(mc >= 0, -1, 1); nuevo = e & (nv["mc5_salto"].to_numpy() >= 10) & (np.abs(mc) >= 10) & (np.abs(mc) <= 60) & (f["seg"].to_numpy() == 2)
    out["V10_MC5_NUEVO_CON_FLUJO"] = (nuevo & (s60 == hacia), hacia)
    out["V11_MC5_NUEVO_SIN_FLUJO"] = (nuevo & (s60 == -hacia), hacia)
    # ---- F. delta extremo de la vela de reloj de 5 min, el regimen decide
    r = f["b5_r"].to_numpy(); sr = sg(np.nan_to_num(r))
    ext = e & (np.abs(r) >= f["b5_r_q70"].to_numpy()) & (sr != 0)
    out["V12_D5M_EXTREMO_ZERO"] = (ext & (sw_zero != 0), sr * sw_zero)
    # ---- crudos para los descriptivos (NO son variantes): el lado es el del FLUJO
    crudos = {"RAF60": (raf60, s60), "RAF10": (raf10, s10), "D5M": (ext, sr), "ABS_LEJOS": (absor & (adist > 30), sA), "ABS_TODO": (absor, sA)}
    return out, crudos


def desagrupar(mask, sep=SEPARACION):
    idx = np.flatnonzero(mask); keep = []; ult = -10**9
    for i in idx:
        if i - ult >= sep: keep.append(i); ult = i
    return np.asarray(keep, dtype=int)


def disparos(d):
    f = rasgos(d); nv = niveles_seg(d); c, crudos = candidatos(f, nv); out = {}
    for grupo in (c, crudos):
        for v, (m, lado) in grupo.items():
            lado = np.nan_to_num(np.asarray(lado, dtype="float64")); m = np.asarray(m, bool) & (lado != 0)
            i = desagrupar(m); out[v] = (i, lado[i].astype(int))
    return out, f, nv


def barrera_cache(ses, d, x, h):
    """B.barrera sobre el tramo 13:20-20:20 UTC (alcanza para la rueda + 900 s), devuelto en el largo de la tabla entera."""
    fn = os.path.join(CACHE, "y_%s_%d_%d_%d.npz" % (ses, x, h, len(d)))
    if os.path.exists(fn): z = np.load(fn); return z["y"], z["seg"]
    hm = d.index.strftime("%H:%M"); m = (hm >= "13:20") & (hm < "20:20"); ii = np.flatnonzero(m)
    y = np.zeros(len(d), int); sg_ = np.full(len(d), np.inf)
    if len(ii):
        sub = d.iloc[ii[0]:ii[-1] + 1]; yy, ss = B.barrera(sub, x, h); y[ii[0]:ii[-1] + 1] = yy; sg_[ii[0]:ii[-1] + 1] = ss
    np.savez_compressed(fn, y=y, seg=sg_); return y, sg_


def placebo(disp, OK, MH, Y, sorteos=200, semilla=7):
    """disp = {ses: (idx, lado)}; OK = {ses: mascara elegible}; MH = {ses: media hora de cada segundo}; Y = {ses: y de la barrera}.
    Mismos lados, misma sesion y misma media hora, segundo elegible al azar. Devuelve media y desvio del % de acierto sobre resueltos."""
    rng = np.random.default_rng(semilla); bolsas = []
    for s, (idx, lado) in disp.items():
        if len(idx) == 0: continue
        mh = MH[s]; ok = OK[s]
        for b in np.unique(mh[idx]):
            pool = np.flatnonzero(ok & (mh == b)); sel = mh[idx] == b
            if len(pool) == 0: continue
            bolsas.append((Y[s][pool], lado[sel]))
    if not bolsas: return np.nan, np.nan
    res = np.empty(sorteos)
    for k in range(sorteos):
        g = 0; n = 0
        for ypool, lados in bolsas:
            yy = ypool[rng.integers(0, len(ypool), len(lados))]; r = yy != 0
            g += int(((yy[r] * lados[r]) > 0).sum()); n += int(r.sum())
        res[k] = 100.0 * g / max(n, 1)
    return float(res.mean()), float(res.std(ddof=1))


def media_hora(d):
    return (d.index.hour * 2 + d.index.minute // 30).to_numpy()


def movimiento_firmado(d, idx, lado, hs=(10, 30, 60, 120, 300)):
    p = d["ultimo"].to_numpy(); n = len(p); r = {}
    for h in hs:
        ok = idx + h < n
        r[h] = float(np.mean((p[idx[ok] + h] - p[idx[ok]]) * lado[ok])) if ok.any() else np.nan
    return r
