# -*- coding: utf-8 -*-
"""RONDA 17, paso 1: ANATOMIA DEL MOVIMIENTO (PREREGISTRO.md secciones 4-6 y enmiendas). Por rueda:
  - segmenta la rueda en GIROS y PIERNAS con un zigzag en vivo de umbral X (0,08 x mediana del rango de las 10 sesiones previas)
    y en la escala micro X/2;
  - controles: PAUSAS (extremos que retrocedieron >= X/2 y < X y fueron superados) y MOMENTOS al azar (grilla de 5 min);
  - mide en cada punto los 16 factores (visibles en el extremo y de reaccion) y en cada pierna sus factores;
  - escribe puntos-<inst>-<dia>.parquet, piernas-<inst>-<dia>.parquet, calibracion y la narracion mecanica de la rueda.
Uso: python -I r17_01_anatomia.py [inst dia ...]   (sin argumentos: la tanda 0)"""
import json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import r17_comun as C

TANDA0 = [("ES", "2026-11-06"), ("ES", "2026-11-09"), ("ES", "2026-11-11"), ("MES", "2026-11-23"), ("MES", "2026-11-24"), ("MES", "2026-11-25")]
TANDA1 = [("ES", d) for d in ("2026-11-02", "2026-11-03", "2026-11-04", "2026-11-10", "2026-11-12", "2026-11-13")]
TIPO_ES = {"2026-11-06": "RANGO", "2026-11-09": "TENDENCIA", "2026-11-11": "MIXTA"}
SALIDA = os.path.join(C.AQUI, "narraciones")
N_RTH = 23400
FR_PAUSA = 0.5
LUGAR_PTS = 1.5
BARRIDA_MAX = 3.0
VMIN_ES = 30.0
FRANJAS = [(0, 3600, "APERTURA"), (3600, 9000, "MANANA"), (9000, 16200, "MEDIODIA"), (16200, 19800, "TARDE"), (19800, 23400, "ULTIMA")]


# ------------------------------------------------------------------------------------------------ niveles y escala
def tabla_diaria():
    es = pd.read_parquet(os.path.join(C.R16, "niveles.parquet")); es["inst"] = "ES"
    me = pd.read_parquet(os.path.join(C.R17, "niveles-MES.parquet"))
    d = pd.concat([es, me], ignore_index=True)
    d["rango"] = d["h"] - d["l"]
    return d.sort_values("dia").reset_index(drop=True)


def escala_X(dia, diaria):
    prev = diaria[diaria["dia"] < dia].drop_duplicates("dia", keep="last").tail(10)
    med = float(prev["rango"].median())
    return max(4.0, round(0.08 * med / C.TICK) * C.TICK), med, list(prev["dia"])


def tipo_dia(inst, dia, diaria):
    if inst == "ES" and dia in TIPO_ES:
        return TIPO_ES[dia]
    r = diaria[(diaria["dia"] == dia) & (diaria["inst"] == inst)].iloc[0]
    e = abs(r["c"] - r["o"]) / (r["h"] - r["l"])
    return "TENDENCIA" if e >= 0.55 else ("RANGO" if e <= 0.25 else "MIXTA")


def niveles(inst, dia, diaria):
    hoy = diaria[(diaria["dia"] == dia) & (diaria["inst"] == inst)].iloc[0]
    ayer = diaria[(diaria["dia"] < dia)].iloc[-1]
    per = pd.read_parquet(os.path.join(C.R17, "periodos.parquet"))
    lunes = pd.Timestamp(dia) - pd.Timedelta(days=pd.Timestamp(dia).weekday())
    w0 = (lunes - pd.Timedelta(days=7)).date().isoformat()
    sem = per[(per["periodo"] == "semana") & (per["desde"] == w0)]
    mes = per[(per["periodo"] == "mes") & (per["desde"] == "2026-10-01")]
    L = {"AYER": {"PDH": ayer["h"], "PDL": ayer["l"], "PDC": ayer["c"], "PPOC": ayer["poc"], "PVAH": ayer["vah"], "PVAL": ayer["val"]},
         "NOCHE": {"ONH": hoy["on_h"], "ONL": hoy["on_l"]},
         "IB": {"IBH": hoy["ib_h"], "IBL": hoy["ib_l"]}}
    if len(sem):
        w = sem.iloc[0]; L["SEMANA"] = {"WH": w["h"], "WL": w["l"], "WC": w["c"], "WPOC": w["poc"], "WVAH": w["vah"], "WVAL": w["val"]}
    else:
        L["SEMANA"] = {}
    if len(mes):
        m = mes.iloc[0]; L["MES"] = {"MH": m["h"], "ML": m["l"], "MC": m["c"], "MPOC": m["poc"], "MVAH": m["vah"], "MVAL": m["val"]}
    else:
        L["MES"] = {}
    L = {k: {a: float(b) for a, b in v.items() if np.isfinite(b)} for k, v in L.items()}
    return L, ayer["dia"], hoy["o"]


# ------------------------------------------------------------------------------------------------ arreglos del dia
def roll(x, w=60):
    cs = np.concatenate([[0.0], np.cumsum(x, dtype="float64")])
    out = cs[w:] - cs[:-w]
    return np.concatenate([np.full(w - 1, np.nan), out])


def arreglos(inst, dia):
    s = C.tabla_1s(inst, dia)
    t0 = pd.Timestamp(dia) + pd.Timedelta(hours=9, minutes=30)
    full = s.loc[:t0 + pd.Timedelta(seconds=N_RTH - 1)]
    pv = (full["ultimo"] * full["vol"]).cumsum(); v = full["vol"].cumsum(); p2v = (full["ultimo"] ** 2 * full["vol"]).cumsum()
    vw = pv / v.replace(0, np.nan); sd = np.sqrt((p2v / v.replace(0, np.nan) - vw ** 2).clip(lower=0))
    cvd = full["delta"].astype("float64").cumsum()
    r = {"vol60": roll(full["vol"].to_numpy("float64")), "del60": roll(full["delta"].to_numpy("float64")),
         "absa60": roll(full["abs_ask"].to_numpy("float64")), "absb60": roll(full["abs_bid"].to_numpy("float64"))}
    pos = full.index.get_indexer(pd.date_range(t0, periods=N_RTH, freq="1s"))
    assert (pos >= 0).all(), "faltan segundos de la rueda en la tabla de 1 s"
    A = {"ult": full["ultimo"].to_numpy()[pos], "alto": full["alto"].to_numpy()[pos], "bajo": full["bajo"].to_numpy()[pos],
         "vol": full["vol"].to_numpy("float64")[pos], "delta": full["delta"].to_numpy("float64")[pos],
         "cvd": cvd.to_numpy()[pos], "vw": vw.to_numpy()[pos], "sd": sd.to_numpy()[pos], "hueco": full["hueco"].to_numpy()[pos],
         "ofi": full["ofi"].to_numpy("float64")[pos], "ofi_pas": full["ofi_pas"].to_numpy("float64")[pos]}
    for k, x in r.items():
        A[k] = x[pos]
    # ventana de reaccion (t, t+60]: la suma movil que termina en t+60
    for k in ("vol60", "del60", "absa60", "absb60"):
        A[k + "_reac"] = np.concatenate([A[k][60:], np.full(60, np.nan)])
    A["t0"] = t0
    return A


def umbrales_franja(A):
    """Percentiles por franja de 30 min de las ventanas de 60 s de la MISMA rueda (normalizacion descriptiva, PREREGISTRO 5)."""
    U = {}
    for b in range(13):
        sl = slice(b * 1800, min(N_RTH, (b + 1) * 1800))
        U[b] = {"q90_vol": np.nanquantile(A["vol60"][sl], 0.9), "q90_del": np.nanquantile(np.abs(A["del60"][sl]), 0.9),
                "q75_del": np.nanquantile(np.abs(A["del60"][sl]), 0.75),
                "q90_absa": np.nanquantile(A["absa60"][sl], 0.9), "q90_absb": np.nanquantile(A["absb60"][sl], 0.9)}
    return U


def footprint(inst, dia, t0):
    fp = pd.read_parquet(os.path.join(C.R17, "fps-%s-%s.parquet" % (inst, dia)))
    fp["i"] = ((pd.to_datetime(fp["t"]) - t0).dt.total_seconds()).astype(int)
    fp = fp[(fp["i"] >= 0) & (fp["i"] < N_RTH)].sort_values("i", kind="stable").reset_index(drop=True)
    fp["m"] = fp["i"] // 60
    g = fp.groupby(["m", "tick"], as_index=False)[["compra", "venta"]].sum().sort_values(["m", "tick"])
    velas = {m: (x["tick"].to_numpy(), x["compra"].to_numpy(), x["venta"].to_numpy()) for m, x in g.groupby("m")}
    return fp, velas


def grandes(inst, dia, t0, q):
    g = pd.read_parquet(os.path.join(C.R17, "ord-%s-%s.parquet" % (inst, dia)))
    g["i"] = ((pd.to_datetime(g["t"]) - t0).dt.total_seconds()).astype(int)
    return g[(g["i"] >= 0) & (g["i"] < N_RTH)].reset_index(drop=True)


def apilados(ticks, compra, venta, lado, vmin):
    """Desequilibrio diagonal >= 300 %: compra(p) >= 3 venta(p - 1 tick) y compra(p) >= vmin (espejo); 3+ precios seguidos."""
    n = len(ticks); im = np.zeros(n, bool)
    for i in range(n):
        if lado > 0:
            ab = venta[i - 1] if i > 0 and ticks[i - 1] == ticks[i] - 1 else 0.0
            im[i] = compra[i] >= 3 * ab and compra[i] >= vmin
        else:
            ar = compra[i + 1] if i + 1 < n and ticks[i + 1] == ticks[i] + 1 else 0.0
            im[i] = venta[i] >= 3 * ar and venta[i] >= vmin
    out = []; i = 0
    while i < n:
        if im[i]:
            j = i
            while j + 1 < n and im[j + 1] and ticks[j + 1] == ticks[j] + 1:
                j += 1
            if j - i + 1 >= 3:
                out.append((int(ticks[i]), int(ticks[j])))
            i = j + 1
        else:
            i += 1
    return out


# ------------------------------------------------------------------------------------------------ zigzag
def zigzag(alto, bajo, ult, X, fr=FR_PAUSA):
    """Zigzag en vivo. Devuelve giros [(i_extremo, precio, tipo, i_confirmacion)], pausas [(i, precio, tipo)], estado final y
    extremo final sin confirmar. tipo +1 maximo, -1 minimo. El segundo del extremo es el PRIMERO que toco ese precio."""
    n = len(alto); E = 1e-9; giros = []; pausas = []
    hi, ihi, lo, ilo = alto[0], 0, bajo[0], 0
    i = 1; est = 0; M = iM = m = im = None; dd = np.inf; du = -np.inf
    while i < n and est == 0:
        if alto[i] > hi: hi, ihi = alto[i], i
        if bajo[i] < lo: lo, ilo = bajo[i], i
        if hi - lo >= X - E:
            if ilo < ihi or (ilo == ihi and ult[i] >= ult[0]):
                giros.append((ilo, lo, -1, i)); est = 1; M, iM = hi, ihi
                dd = bajo[ihi + 1:i + 1].min() if i > ihi else np.inf
            else:
                giros.append((ihi, hi, 1, i)); est = -1; m, im = lo, ilo
                du = alto[ilo + 1:i + 1].max() if i > ilo else -np.inf
        i += 1
    while i < n:
        if est == 1:
            if alto[i] > M + E:
                if M - dd >= fr * X - E: pausas.append((iM, M, 1))
                M, iM = alto[i], i; dd = np.inf
            else:
                dd = min(dd, bajo[i])
            if i > iM and bajo[i] <= M - X + E:
                giros.append((iM, M, 1, i)); est = -1; m, im = bajo[i], i; du = -np.inf
        else:
            if bajo[i] < m - E:
                if du - m >= fr * X - E: pausas.append((im, m, -1))
                m, im = bajo[i], i; du = -np.inf
            else:
                du = max(du, alto[i])
            if i > im and alto[i] >= m + X - E:
                giros.append((im, m, -1, i)); est = 1; M, iM = alto[i], i; dd = np.inf
        i += 1
    final = (iM, M) if est == 1 else (im, m)
    return giros, pausas, est, final


# ------------------------------------------------------------------------------------------------ factores de un punto
def lugar(p, i, L, A):
    out = {}; cerca = []
    for clase, d in L.items():
        if clase == "IB" and i < 3600:
            continue
        hit = [k for k, v in d.items() if abs(p - v) <= LUGAR_PTS]
        out["L_" + clase] = bool(hit); cerca += hit
    r50 = round(p / 50.0) * 50.0
    out["L_REDONDO"] = abs(p - r50) <= LUGAR_PTS
    if out["L_REDONDO"]: cerca.append("R%d" % r50)
    vw, sd = A["vw"][i], A["sd"][i]
    vws = {"VWAP": vw, "VWAP+1": vw + sd, "VWAP-1": vw - sd, "VWAP+2": vw + 2 * sd, "VWAP-2": vw - 2 * sd}
    hv = [k for k, v in vws.items() if np.isfinite(v) and abs(p - v) <= LUGAR_PTS]
    out["L_VWAP"] = bool(hv); cerca += hv
    out["L_IMPORTANTE"] = any(out[k] for k in ("L_AYER", "L_NOCHE", "L_IB", "L_REDONDO", "L_SEMANA", "L_MES") if k in out)
    out["niveles_cerca"] = ",".join(cerca)
    return out


def refs_barrida(s, i, L, giros_previos):
    R = {}
    if s > 0:
        for k in ("PDH",): R[k] = L["AYER"].get(k)
        R["ONH"] = L["NOCHE"].get("ONH")
        if i >= 3600: R["IBH"] = L["IB"].get("IBH")
        R["WH"] = L["SEMANA"].get("WH"); R["MH"] = L["MES"].get("MH")
    else:
        R["PDL"] = L["AYER"].get("PDL"); R["ONL"] = L["NOCHE"].get("ONL")
        if i >= 3600: R["IBL"] = L["IB"].get("IBL")
        R["WL"] = L["SEMANA"].get("WL"); R["ML"] = L["MES"].get("ML")
    for j, (ig, pg, tg, _) in enumerate(giros_previos):
        if tg == s and ig < i:
            R["giro%d" % j] = pg
    return {k: v for k, v in R.items() if v is not None and np.isfinite(v)}


def factores_punto(i, p, s, kind, ctx):
    A, U, L, fp, velas, G, giros, vmin, q99, q999 = (ctx[k] for k in ("A", "U", "L", "fp", "velas", "G", "giros", "vmin", "q99", "q999"))
    b = min(12, i // 1800); u = U[b]; f = {"i": i, "precio": p, "tipo": s, "clase": kind}
    f["franja"] = next(n for a, z, n in FRANJAS if a <= i < z)
    col = "absa60" if s > 0 else "absb60"; qa = u["q90_absa"] if s > 0 else u["q90_absb"]
    x = A[col][i]; f["abs_pre"] = x; f["ABS"] = bool(x > 0 and x >= qa)
    xr = A[col + "_reac"][i]; f["abs_reac"] = xr; f["ABS_REAC"] = bool(np.isfinite(xr) and xr > 0 and xr >= qa)
    f["vol_pre"] = A["vol60"][i]; f["VOL"] = bool(A["vol60"][i] >= u["q90_vol"])
    dp = s * A["del60"][i]; f["delta_pre_s"] = dp
    f["EMPUJE"] = bool(dp >= u["q90_del"]); f["DELTA_CONTRA"] = bool(dp < 0)
    dr = s * A["del60_reac"][i]; f["delta_reac_s"] = dr
    f["AGRESION_CONTRARIA"] = bool(np.isfinite(dr) and dr <= -u["q75_del"])
    # divergencia contra el giro previo del mismo tipo
    prev = [(ig, pg) for (ig, pg, tg, _) in giros if tg == s and ig < i]
    if prev and s * (p - prev[-1][1]) > 0:
        f["DIV_aplica"] = True; f["DIV_SWING"] = bool(s * (A["cvd"][i] - A["cvd"][prev[-1][0]]) < 0)
    else:
        f["DIV_aplica"] = False; f["DIV_SWING"] = np.nan
    m = i // 60; sl = slice(m * 60, m * 60 + 60)
    f["DIV_VELA"] = bool(s * A["delta"][sl].sum() < 0); f["DIV_VELA_PRE"] = bool(s * A["delta"][m * 60:i + 1].sum() < 0)
    tk = int(round(p / C.TICK))
    # footprint del extremo (solo giros y pausas: los momentos no son extremos)
    if kind != "azar":
        lo_i, hi_i = np.searchsorted(fp["i"].to_numpy(), [i - 120, i + 121])
        w = fp.iloc[lo_i:hi_i]
        tot = (w["compra"] + w["venta"]).groupby(w["tick"]).sum()
        v_ext = float(tot.get(tk, 0.0)); v_in = float(np.mean([tot.get(tk - s * k, 0.0) for k in range(1, 5)]))
        f["v_ext"] = v_ext; f["v_in"] = v_in; f["AGOTAMIENTO"] = bool(v_in > 0 and v_ext <= 0.25 * v_in)
        f["barrido_en_extremo"] = bool(w.loc[w["tick"] == tk, "barr"].any())
        tks, cm, vt = velas.get(m, (np.array([], int), np.array([]), np.array([])))
        j = np.nonzero(tks == tk)[0]
        c_e = float(cm[j[0]]) if len(j) else 0.0; v_e = float(vt[j[0]]) if len(j) else 0.0
        f["sub_compra"] = c_e; f["sub_venta"] = v_e
        lado_cero = v_e if s > 0 else c_e; otro = c_e if s > 0 else v_e
        f["SUBASTA"] = "SIN_TERMINAR" if (c_e > 1e-9 and v_e > 1e-9) else ("TERMINADA" if (lado_cero <= 1e-9 and otro > 1e-9) else "OTRA")
    else:
        for k in ("v_ext", "v_in", "AGOTAMIENTO", "barrido_en_extremo", "sub_compra", "sub_venta", "SUBASTA"):
            f[k] = np.nan
    # apilados del lado del empuje en la vela del extremo y del lado contrario en esa vela y la siguiente
    def cerca_ap(lst, lado):
        if lado > 0:
            return any(hi_ >= tk - 12 and lo_ <= tk + 12 for lo_, hi_ in lst)
        return any(lo_ <= tk + 12 and hi_ >= tk - 12 for lo_, hi_ in lst)
    tks, cm, vt = velas.get(m, (np.array([], int), np.array([]), np.array([])))
    f["APIL_FAVOR"] = cerca_ap(apilados(tks, cm, vt, s, vmin), s) if len(tks) else False
    ac = False
    for mm in (m, m + 1):
        tks2, cm2, vt2 = velas.get(mm, (np.array([], int), np.array([]), np.array([])))
        if len(tks2) and cerca_ap(apilados(tks2, cm2, vt2, -s, vmin), -s):
            ac = True
    f["APIL_CONTRA"] = ac
    # ordenes grandes
    gi = G["i"].to_numpy(); sel = (gi >= i - 60) & (gi <= i + 60) & (G["pmax"].to_numpy() >= p - 0.5) & (G["pmin"].to_numpy() <= p + 0.5)
    lado = G["lado"].to_numpy(); vv = G["vol"].to_numpy()
    for nom, q in (("", q99), ("999", q999)):
        ok = sel & (vv >= q)
        f["BIG_FAVOR" + nom] = bool((ok & (lado == s)).any()); f["BIG_CONTRA" + nom] = bool((ok & (lado == -s)).any())
    f["BIG95_FAVOR"] = bool((sel & (vv >= 95) & (lado == s)).any()); f["BIG95_CONTRA"] = bool((sel & (vv >= 95) & (lado == -s)).any())
    reac = (gi > i) & (gi <= i + 60) & (vv >= q99) & (lado == -s)
    f["BIG_CONTRA_REAC"] = bool(reac.any())
    # barrida de una referencia
    R = refs_barrida(s, i, L, giros)
    lo_w = max(0, i - 600)
    if i > lo_w:
        ext_prev = A["bajo"][lo_w:i].min() if s > 0 else A["alto"][lo_w:i].max()
    else:
        ext_prev = np.nan
    r50 = (np.floor(p / 50.0) * 50.0) if s > 0 else (np.ceil(p / 50.0) * 50.0)
    R["R50"] = r50
    hit = [k for k, v in R.items() if 0 < s * (p - v) <= BARRIDA_MAX and np.isfinite(ext_prev) and s * (ext_prev - v) < 0]
    f["BARRIDA"] = bool(hit); f["barrida_de"] = ",".join(hit)
    f.update(lugar(p, i, L, A))
    return f


# ------------------------------------------------------------------------------------------------ piernas
def lvn_perfil(inst, ayer):
    """Ticks de vacio (LVN) del perfil de la rueda anterior: volumen < 20 % de la mediana por tick dentro de su rango."""
    if inst == "ES":
        fp = pd.read_parquet(os.path.join(C.R16, "fp-%s.parquet" % ayer))
    else:
        fp = pd.read_parquet(os.path.join(C.R17, "fps-%s-%s.parquet" % (inst, ayer)))
    tot = (fp["compra"] + fp["venta"]).groupby(fp["tick"]).sum()
    rng = np.arange(int(tot.index.min()), int(tot.index.max()) + 1)
    v = tot.reindex(rng, fill_value=0.0).to_numpy()
    med = np.median(v)
    return set(rng[v < 0.2 * med].tolist()), int(rng[0]), int(rng[-1])


def factores_pierna(k, a, z, ctx, X):
    """a, z: (i, precio, tipo) de los extremos de la pierna (z puede ser el extremo final sin confirmar)."""
    A, G, velas, vmin, q99, L, lvn = (ctx[x] for x in ("A", "G", "velas", "vmin", "q99", "L", "lvn"))
    i0, p0 = a[0], a[1]; i1, p1 = z[0], z[1]; s = 1 if p1 > p0 else -1
    sl = slice(i0 + 1, i1 + 1); dp = abs(p1 - p0); dur = max(1, i1 - i0) / 60.0
    dl = float(A["delta"][sl].sum()); vl = float(A["vol"][sl].sum())
    f = {"pierna": k, "i0": i0, "i1": i1, "p0": p0, "p1": p1, "dir": s, "pts": dp, "min": dur, "pts_min": dp / dur,
         "delta": dl, "vol": vl, "delta_vol": dl / vl if vl else np.nan, "delta_a_favor": bool(s * dl > 0),
         "pts_por_1000_netos": (dp / (s * dl) * 1000.0) if s * dl > 0 else np.nan, "pts_por_raiz_vol": dp / np.sqrt(vl) if vl else np.nan}
    af = ac = 0
    for m in range(i0 // 60, i1 // 60 + 1):
        tks, cm, vt = velas.get(m, (np.array([], int), np.array([]), np.array([])))
        if len(tks):
            af += len(apilados(tks, cm, vt, s, vmin)); ac += len(apilados(tks, cm, vt, -s, vmin))
    f["apil_favor"] = af; f["apil_contra"] = ac; f["apil_favor_10pts"] = af / dp * 10 if dp else np.nan
    gi = G["i"].to_numpy(); ok = (gi > i0) & (gi <= i1) & (G["vol"].to_numpy() >= q99); ld = G["lado"].to_numpy(); vv = G["vol"].to_numpy()
    f["big_favor"] = int((ok & (ld == s)).sum()); f["big_contra"] = int((ok & (ld == -s)).sum())
    f["big_vol_neto_favor"] = float((vv[ok] * ld[ok] * s).sum())
    lo_, hi_ = min(p0, p1), max(p0, p1); cruz = []
    for clase, d in L.items():
        for nom, v in d.items():
            if lo_ < v < hi_ and not (clase == "IB" and i0 < 3600):
                cruz.append(nom)
    f["niveles_atravesados"] = ",".join(cruz)
    lvn_set, t_lo, t_hi = lvn
    tk_path = np.rint(A["ult"][sl] / C.TICK).astype(int)
    camino = np.arange(int(round(lo_ / C.TICK)), int(round(hi_ / C.TICK)) + 1)
    dentro = camino[(camino >= t_lo) & (camino <= t_hi)]
    f["ticks_en_rango_ayer"] = int(len(dentro)); f["frac_vacio"] = float(np.mean([t in lvn_set for t in dentro])) if len(dentro) else np.nan
    if len(tk_path):
        cnt = pd.Series(tk_path).value_counts()
        en_v = [cnt.get(t, 0) for t in dentro if t in lvn_set]; fuera = [cnt.get(t, 0) for t in dentro if t not in lvn_set]
        f["seg_por_tick_vacio"] = float(np.mean(en_v)) if en_v else np.nan; f["seg_por_tick_resto"] = float(np.mean(fuera)) if fuera else np.nan
    # absorciones EN CONTRA de la pierna que fallaron (el precio siguio >= X/2 a favor de la pierna)
    col = "absa60" if s > 0 else "absb60"; U = ctx["U"]; n_abs = n_fallo = 0; ult_ep = -10 ** 9
    for j in range(i0 + 1, i1 + 1):
        qa = U[min(12, j // 1800)]["q90_absa" if s > 0 else "q90_absb"]
        x = A[col][j]
        if x > 0 and x >= qa and j - ult_ep > 60:
            n_abs += 1; ult_ep = j
            fut = A["alto"][j:i1 + 1].max() if s > 0 else A["bajo"][j:i1 + 1].min()
            if s * (fut - A["ult"][j]) >= X / 2: n_fallo += 1
    f["abs_contra_n"] = n_abs; f["abs_contra_fallidas"] = n_fallo
    n_c = n_c_sig = 0
    for j in range(i0 + 1, i1 + 1, 15):
        fut = A["alto"][j:i1 + 1].max() if s > 0 else A["bajo"][j:i1 + 1].min()
        n_c += 1; n_c_sig += s * (fut - A["ult"][j]) >= X / 2
    f["ctrl_seg_n"] = n_c; f["ctrl_seg_siguio"] = n_c_sig
    f["DIV_PIERNA"] = bool(s * dl < 0)
    # flujo de ordenes de la punta (Cont, Kukanov y Stoikov 2014): total y parte PASIVA (altas y bajas de limites entre ordenes)
    of = float(A["ofi"][sl].sum()); op = float(A["ofi_pas"][sl].sum())
    f["ofi"] = of; f["ofi_pas"] = op; f["ofi_a_favor"] = bool(s * of > 0); f["ofi_pas_a_favor"] = bool(s * op > 0)
    return f


# ------------------------------------------------------------------------------------------------ narracion
def hhmmss(t0, i):
    return (t0 + pd.Timedelta(seconds=int(i))).strftime("%H:%M:%S")


NOMBRE = {"ABS": "absorcion real en la punta", "VOL": "volumen alto", "EMPUJE": "empuje fuerte hacia el extremo",
          "DELTA_CONTRA": "delta en contra al llegar", "DIV_SWING": "divergencia de CVD contra el giro previo", "DIV_VELA": "vela con delta en contra",
          "AGOTAMIENTO": "agotamiento (poco volumen en el extremo)", "APIL_FAVOR": "apilado a favor en el extremo", "BIG_FAVOR": "orden grande a favor en el extremo",
          "BIG_CONTRA": "orden grande en contra en el extremo", "BARRIDA": "barrida", "ABS_REAC": "absorcion sigue despues",
          "AGRESION_CONTRARIA": "agresion contraria fuerte despues", "APIL_CONTRA": "apilado en contra", "BIG_CONTRA_REAC": "orden grande en contra despues"}


def prendido(v):
    if isinstance(v, (bool, np.bool_)):
        return bool(v)
    try:
        return (not pd.isna(v)) and float(v) == 1.0
    except (TypeError, ValueError):
        return False


def limpiar(P):
    """Columnas con bool y NaN mezclados -> float (1/0/NaN); textos con NaN -> 'NA' (parquet no acepta mezclas)."""
    for c in P.columns:
        if P[c].dtype == object:
            vals = [v for v in P[c].tolist() if not (isinstance(v, float) and np.isnan(v))]
            if vals and all(isinstance(v, (bool, np.bool_)) for v in vals):
                P[c] = P[c].map(lambda v: np.nan if (isinstance(v, float) and np.isnan(v)) else float(bool(v)))
            elif vals and all(isinstance(v, str) for v in vals):
                P[c] = P[c].fillna("NA")
    return P


def narrar(inst, dia, tipo, X, giros, piernas, P, t0, L, esc="X", titulo=True):
    out = (["# %s %s — rueda de %s (X = %.2f pts)" % (inst, dia, tipo, X), "",
            "Niveles: " + "; ".join("%s %s" % (c, ", ".join("%s %.2f" % (k, v) for k, v in d.items())) for c, d in L.items() if d), ""]
           if titulo else [])
    out += ["", "## Escala %s (%s)" % (esc, "movimientos principales" if esc == "4X" else "detalle: giros de al menos X"), ""]
    Pg = P[(P["clase"] == "giro") & (P["escala"] == esc)].set_index("i")
    for _, pr in piernas.iterrows():
        verbo = "subio" if pr["dir"] > 0 else "bajo"
        txt = ("- **%s-%s %s %.2f pts** (%.2f -> %.2f) en %.1f min (%.2f pts/min). Delta %+.0f (%+.0f %% del volumen; %s). "
               "Apilados %d a favor / %d en contra; grandes %d a favor / %d en contra (neto %+.0f). "
               % (hhmmss(t0, pr["i0"]), hhmmss(t0, pr["i1"]), verbo, pr["pts"], pr["p0"], pr["p1"], pr["min"], pr["pts_min"], pr["delta"],
                  100 * pr["delta_vol"], "a favor" if pr["delta_a_favor"] else "EN CONTRA: el precio se movio contra la agresion neta",
                  pr["apil_favor"], pr["apil_contra"], pr["big_favor"], pr["big_contra"], pr["big_vol_neto_favor"]))
        if pr["niveles_atravesados"]:
            txt += "Atraveso %s. " % pr["niveles_atravesados"]
        if np.isfinite(pr.get("frac_vacio", np.nan)):
            txt += "%.0f %% del recorrido por vacios del perfil de ayer" % (100 * pr["frac_vacio"])
            if np.isfinite(pr.get("seg_por_tick_vacio", np.nan)) and np.isfinite(pr.get("seg_por_tick_resto", np.nan)):
                txt += " (%.1f s por tick en el vacio contra %.1f afuera)" % (pr["seg_por_tick_vacio"], pr["seg_por_tick_resto"])
            txt += ". "
        if pr["abs_contra_n"]:
            txt += "Absorciones en contra: %d, fallaron %d." % (pr["abs_contra_n"], pr["abs_contra_fallidas"])
        out.append(txt)
        if pr["i1"] in Pg.index and not bool(pr["final"]):
            g = Pg.loc[pr["i1"]]
            if isinstance(g, pd.DataFrame): g = g.iloc[0]
            pres = [NOMBRE[k] for k in NOMBRE if prendido(g.get(k))]
            lug = g["niveles_cerca"] if g["niveles_cerca"] else "ningun nivel a 1,5 pts"
            sub = g["SUBASTA"]
            out.append("  - **Giro %s en %.2f a las %s.** Lugar: %s. Subasta %s (compras %.0f / ventas %.0f en el extremo). %s%s"
                       % ("MAXIMO" if g["tipo"] > 0 else "MINIMO", g["precio"], hhmmss(t0, pr["i1"]), lug, sub, g["sub_compra"], g["sub_venta"],
                          ("Presentes: " + "; ".join(pres) + ". ") if pres else "Ningun factor de la lista. ",
                          ("Barrio " + g["barrida_de"] + ".") if g["BARRIDA"] else ""))
    return "\n".join(out) + "\n"


# ------------------------------------------------------------------------------------------------ main
def procesar(inst, dia, diaria, cuant, vmin_mes):
    X, med, prev = escala_X(dia, diaria)
    tipo = tipo_dia(inst, dia, diaria)
    L, ayer, apertura = niveles(inst, dia, diaria)
    A = arreglos(inst, dia); t0 = A["t0"]; U = umbrales_franja(A)
    fp, velas = footprint(inst, dia, t0)
    q = cuant["%s %s" % (inst, dia)]
    G = grandes(inst, dia, t0, q)
    vmin = VMIN_ES if inst == "ES" else vmin_mes
    ctx = {"A": A, "U": U, "L": L, "fp": fp, "velas": velas, "G": G, "vmin": vmin, "q99": q["q99"], "q999": q["q999"],
           "lvn": lvn_perfil(inst, ayer)}
    filas, piernas_t = [], []
    for esc, XX in (("X", X), ("X/2", X / 2), ("4X", 4 * X)):
        giros, pausas, est, final = zigzag(A["alto"], A["bajo"], A["ult"], XX)
        ctx["giros"] = giros
        tg = [g for g in giros if g[0] >= 30]                          # el primer print de la apertura no es un giro
        for (ig, pg, s, ic) in tg:
            f = factores_punto(ig, pg, s, "giro", ctx); f["i_conf"] = ic; f["escala"] = esc; filas.append(f)
        for (ip, pp, s) in pausas:
            f = factores_punto(ip, pp, s, "pausa", ctx); f["escala"] = esc; filas.append(f)
        ext = sorted([g[0] for g in giros] + [p_[0] for p_ in pausas])
        ext_a = np.array(ext)
        for gsec in range(300, 23101, 30):
            if len(ext_a) and np.min(np.abs(ext_a - gsec)) <= 20:
                continue
            antes = [g for g in giros if g[0] <= gsec]; despues = [g for g in giros if g[0] > gsec]
            if not antes:
                continue
            s = (1 if despues[0][1] > antes[-1][1] else -1) if despues else est
            f = factores_punto(gsec, float(A["ult"][gsec]), s, "azar", ctx); f["escala"] = esc; filas.append(f)
        # piernas
        pts = [(g[0], g[1], g[2]) for g in giros] + [(final[0], final[1], est)]
        for k in range(len(pts) - 1):
            pr = factores_pierna(k, pts[k], pts[k + 1], ctx, XX); pr["escala"] = esc; pr["final"] = (k == len(pts) - 2)
            piernas_t.append(pr)
    P = limpiar(pd.DataFrame(filas)); P["inst"] = inst; P["dia"] = dia; P["tipo_dia"] = tipo
    PR = limpiar(pd.DataFrame(piernas_t)); PR["inst"] = inst; PR["dia"] = dia; PR["tipo_dia"] = tipo
    os.makedirs(SALIDA, exist_ok=True)
    P.to_parquet(os.path.join(C.R17, "puntos-%s-%s.parquet" % (inst, dia))); PR.to_parquet(os.path.join(C.R17, "piernas-%s-%s.parquet" % (inst, dia)))
    txt = narrar(inst, dia, tipo, X, None, PR[PR["escala"] == "4X"], P, t0, L, esc="4X")
    txt += narrar(inst, dia, tipo, X, None, PR[PR["escala"] == "X"], P, t0, L, esc="X", titulo=False)
    open(os.path.join(SALIDA, "narracion-%s-%s.md" % (inst, dia)), "w", encoding="utf-8").write(txt)
    cal = {"inst": inst, "dia": dia, "tipo": tipo, "X": X, "mediana_rango_10": med, "previas": prev, "vmin": vmin, **{k: q[k] for k in q},
           "giros_X": int(((P["clase"] == "giro") & (P["escala"] == "X")).sum()), "pausas_X": int(((P["clase"] == "pausa") & (P["escala"] == "X")).sum()),
           "azar_X": int(((P["clase"] == "azar") & (P["escala"] == "X")).sum()),
           "giros_micro": int(((P["clase"] == "giro") & (P["escala"] == "X/2")).sum()), "pausas_micro": int(((P["clase"] == "pausa") & (P["escala"] == "X/2")).sum()),
           "umbrales_franja": {str(b): {k: float(v) for k, v in U[b].items()} for b in U}}
    json.dump(cal, open(os.path.join(SALIDA, "cal-%s-%s.json" % (inst, dia)), "w"), indent=1, default=float)
    print(inst, dia, tipo, "X %.2f" % X, "giros %d pausas %d azar %d | micro giros %d pausas %d" % (cal["giros_X"], cal["pausas_X"], cal["azar_X"],
          cal["giros_micro"], cal["pausas_micro"]), flush=True)
    return cal


def calibrar_vmin_mes(dias_es, dias_mes):
    """V_min de MES = el valor con el mismo percentil que 30 contratos en las celdas (precio x lado, vela de 1 min) de ES."""
    def celdas(inst, dia):
        fp = pd.read_parquet(os.path.join(C.R17, "fps-%s-%s.parquet" % (inst, dia)))
        fp["m"] = pd.to_datetime(fp["t"]).dt.floor("60s")
        g = fp.groupby(["m", "tick"])[["compra", "venta"]].sum()
        v = np.concatenate([g["compra"].to_numpy(), g["venta"].to_numpy()])
        return v[v > 0]
    es = np.concatenate([celdas("ES", d) for d in dias_es]); me = np.concatenate([celdas("MES", d) for d in dias_mes])
    p = float(np.mean(es < VMIN_ES))
    return float(np.quantile(me, p)), p, float(np.median(es)), float(np.median(me))


def main():
    pedidos = TANDA0 if len(sys.argv) < 3 else [(sys.argv[k], sys.argv[k + 1]) for k in range(1, len(sys.argv) - 1, 2)]
    diaria = tabla_diaria()
    cuant = json.load(open(os.path.join(C.R17, "cuantiles_ordenes.json")))
    vmin_mes, pct, med_es, med_mes = calibrar_vmin_mes([d for i, d in TANDA0 if i == "ES"], [d for i, d in TANDA0 if i == "MES"])
    print("V_min MES = %.1f (percentil %.3f de 30 en ES; mediana de celda ES %.1f, MES %.1f)" % (vmin_mes, pct, med_es, med_mes), flush=True)
    cals = [procesar(inst, dia, diaria, cuant, vmin_mes) for inst, dia in pedidos]
    json.dump({"vmin_mes": vmin_mes, "percentil_30_es": pct, "mediana_celda_es": med_es, "mediana_celda_mes": med_mes},
              open(os.path.join(SALIDA, "vmin_mes.json"), "w"), indent=1, default=float)


if __name__ == "__main__":
    main()
