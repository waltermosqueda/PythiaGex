# -*- coding: utf-8 -*-
"""RONDA 27 - comun: el pipeline POR MINUTO desde UNA cinta de ES de la sonda (nada de ATAS, nada de red).

Por cada jornada CME (18:00 NY de la vispera a 17:00 NY) y cada cierre de minuto RTH (9:31:00 .. 15:59:00 NY) arma, con las ordenes de
t < cierre (la "foto" de La Ola), los rasgos del PREREGISTRO.md seccion 4 y, con las ordenes de t >= cierre, los resultados de la
seccion 3 (que toca primero, c + Y o c - Y, con Y = X/2, X, 2X; plazo 60 min o las 16:00) y las salidas de la regla (bracket +-Y con el
objetivo pasado 1 tick y el stop al tocarlo).
Definiciones copiadas, no reinventadas:
  - zigzag: semantica EXACTA de herramientas/test_ola.Maquina (la referencia de La Ola 1.0; compara HORAS en el arranque), orden por orden,
    en el orden del archivo y sin sacar duplicados (como el indicador);
  - X: 0,08 x mediana del rango RTH de las 10 ruedas previas, al tick con mitad para arriba (r19_01_rasgos.X_de);
  - clima: estado_minuto de ronda21_regimen/regla_congelada_comb_t7_v4.py con la curva f(m) de curva_rango_por_minuto.json;
  - VWAP: precio medio de la orden ((primero + ultimo) / 2) por volumen; desvio con E[p^2] - E[p]^2 (r19_01_rasgos);
  - zona y barrida de un nivel: r19_02_motor.niveles_en_zona y r19_08_eventos (barrida_y_vuelta), con los niveles que el indicador tiene a
    mano (sin VWAP semanal/mensual ni perfil de la semana) y solo niveles que YA existian en el instante del extremo;
  - icebergs y big trades (p99 por franja de 30 min con las 10 ruedas previas, minimo 2): r19_01_rasgos;
  - perfil y area de valor de ayer: r19_01_rasgos.area_valor (regla de ATAS verificada 1.186/1.186).
Prioridad BELOW_NORMAL al importarse."""
import glob, hashlib, json, math, os, sys, time
from datetime import datetime, timedelta, timezone
from zoneinfo import ZoneInfo

try:   # PRIORIDAD BAJA (BELOW_NORMAL) con tipos declarados
    import ctypes, ctypes.wintypes as _W
    _k = ctypes.windll.kernel32; _k.GetCurrentProcess.restype = _W.HANDLE; _k.SetPriorityClass.argtypes = [_W.HANDLE, _W.DWORD]
    _k.SetPriorityClass(_k.GetCurrentProcess(), 0x00004000)
except Exception:
    pass

import numpy as np, pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
LAB = os.path.dirname(AQUI)
RAIZ = os.path.dirname(LAB)
DATOS = os.path.join(RAIZ, "datos", "ronda27")
FLUJO = os.path.join(os.environ.get("APPDATA", ""), "ATAS", "PythiaGex", "flujo")
SCR = r"C:\Users\wmx_7\AppData\Local\Temp\claude\6e29e09c80928880\233bf8d4-5113-4e95-a0d9-68bff726690d\scratchpad"
RESUMEN_R19 = os.path.join(SCR, "r19", "hist", "resumen.json")
CURVA = os.path.join(LAB, "ronda21_regimen", "curva_rango_por_minuto.json")
os.makedirs(DATOS, exist_ok=True)
TK = 0.25
NY = ZoneInfo("America/New_York")
UTC = timezone.utc
RTH0, RTH1 = 9 * 3600 + 30 * 60, 16 * 3600            # segundos NY de la rueda
IB_FIN = 10 * 3600 + 30 * 60
ASIA_FIN, LON_FIN = 3 * 3600, RTH0                    # Asia [18:00, 03:00), Londres [03:00, 09:30)
SPREAD_ROLL = 67.00                                   # Z6 - U6 (R19, PREREGISTRO seccion 0)
ULTIMO_U6 = "2026-09-11"
CALENTAMIENTO = ("2026-09-08", "2026-09-09")
NMIN = 389                                            # minutos con futuro (cierres 9:31 .. 15:59)
CORTE_TENDENCIA, CORTE_ESTRECHO = 1.6, 1.0
YS = {"Xm": 0.5, "X": 1.0, "X2": 2.0}                 # Y = factor x X (Xm = X/2 al tick, mitad para arriba)
PLAZO_S = 3600


# ---------------------------------------------------------------------------------------------------------------- utilidades
def ram_libre_gb():
    try:
        class M(ctypes.Structure):
            _fields_ = [("l", ctypes.c_ulong), ("p", ctypes.c_ulong), ("tot", ctypes.c_ulonglong), ("av", ctypes.c_ulonglong),
                        ("a", ctypes.c_ulonglong), ("b", ctypes.c_ulonglong), ("c", ctypes.c_ulonglong), ("d", ctypes.c_ulonglong),
                        ("e", ctypes.c_ulonglong)]
        m = M(); m.l = ctypes.sizeof(M); ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(m))
        return m.av / 2 ** 30
    except Exception:
        return 99.0


def esperar_ram(minimo=2.5, espera_s=30, intentos=40):
    """Regla del operador: si la RAM libre baja de 2,5 GB se frena. Espera hasta 20 min; si no se libera, corta."""
    for _ in range(intentos):
        g = ram_libre_gb()
        if g >= minimo:
            return g
        print("RAM libre %.2f GB < %.1f GB: espero %d s" % (g, minimo, espera_s), flush=True)
        time.sleep(espera_s)
    raise SystemExit("RAM libre por debajo de %.1f GB durante %d intentos: freno (regla del operador)" % (minimo, intentos))


def sha256(ruta):
    h = hashlib.sha256()
    with open(ruta, "rb") as f:
        for b in iter(lambda: f.read(1 << 20), b""):
            h.update(b)
    return h.hexdigest()


def dtick(x):
    return math.floor(x / TK + 0.5) * TK


def Y_de(X, clave):
    return dtick(YS[clave] * X)


def contrato(dia):
    return "U6" if dia <= ULTIMO_U6 else "Z6"


def cintas(inst="ES"):
    """{dia de la rueda: ruta} de las cintas terminadas (.listo) cinta-ES-<desde>-<hasta>.csv; el dia es el 'hasta' UTC del nombre."""
    out = {}
    for f in sorted(glob.glob(os.path.join(FLUJO, "cinta-%s-*.csv" % inst))):
        p = os.path.basename(f)[:-4].split("-")
        if len(p) != 4 or p[1] != inst or not os.path.exists(f + ".listo"):
            continue
        fin = p[3][:8]
        out["%s-%s-%s" % (fin[:4], fin[4:6], fin[6:8])] = f
    return out


def curva_f():
    return np.asarray(json.load(open(CURVA))["f"], float)


# ---------------------------------------------------------------------------------------------------------------- cinta
def leer_cinta(ruta, dia):
    """Una fila por orden agresora, EN EL ORDEN DEL ARCHIVO y sin sacar duplicados (como La Ola). t en ns UTC; s = segundos desde la
    medianoche NY del dia de la rueda (la noche da negativo)."""
    d = pd.read_csv(ruta, dtype={"primero": "float64", "ultimo": "float64", "vol": "float64", "lado": "int64", "abid": "float64",
                                 "abidv": "float64", "aask": "float64", "aaskv": "float64", "dbid": "float64", "dbidv": "float64",
                                 "dask": "float64", "daskv": "float64"})
    t = pd.to_datetime(d["t"], format="%Y-%m-%dT%H:%M:%S.%f").to_numpy("datetime64[ns]").astype(np.int64)
    m0 = int(pd.Timestamp(dia + " 00:00:00", tz=NY).tz_convert("UTC").value)
    out = {"t": t, "s": (t - m0) / 1e9, "m0": m0}
    for c in ("primero", "ultimo", "vol", "abid", "abidv", "aask", "aaskv", "dbid", "dbidv", "dask", "daskv"):
        out[c] = d[c].to_numpy("float64")
    out["lado"] = d["lado"].to_numpy("int64")
    out["retrocesos"] = int((np.diff(t) < 0).sum())
    return out


def ns_ny(dia, seg):
    """ns UTC de un instante 'seg' segundos desde la medianoche NY del dia."""
    return int(pd.Timestamp(dia + " 00:00:00", tz=NY).tz_convert("UTC").value) + int(round(seg * 1e9))


# ---------------------------------------------------------------------------------------------------------------- numba
try:
    from numba import njit
except Exception:          # sin numba corre igual, lento
    def njit(*a, **k):
        def deco(f):
            return f
        return deco if not (len(a) == 1 and callable(a[0])) else a[0]


@njit(cache=False)
def maquina_nb(t, mn, mx, cum, X):
    """Zigzag de X orden por orden, semantica EXACTA de test_ola.Maquina. Devuelve por orden: dir, ext, indice del ext, cantidad de
    pivotes; y los pivotes (indice del extremo, precio, tipo +1 max / -1 min, indice de la confirmacion, cum en el extremo)."""
    n = len(mn)
    D = np.zeros(n, np.int8); E = np.full(n, np.nan); EI = np.full(n, -1, np.int64); NP = np.zeros(n, np.int64)
    pei = np.empty(n, np.int64); pp = np.empty(n); pt = np.empty(n, np.int8); pci = np.empty(n, np.int64); pc = np.empty(n)
    npv = 0; dr = 0; ext = 0.0; ei = -1; ec = 0.0
    lo = 0.0; hi = 0.0; lo_i = -1; hi_i = -1; lo_c = 0.0; hi_c = 0.0; ini = False
    for i in range(n):
        a = mn[i]; b = mx[i]; c = cum[i]
        if dr == 0:
            if (not ini) or a < lo:
                lo = a; lo_i = i; lo_c = c
            if (not ini) or b > hi:
                hi = b; hi_i = i; hi_c = c
            ini = True
            if hi - lo >= X:
                if t[hi_i] > t[lo_i]:
                    pei[npv] = lo_i; pp[npv] = lo; pt[npv] = -1; pci[npv] = i; pc[npv] = lo_c; npv += 1
                    dr = 1; ext = hi; ei = hi_i; ec = hi_c
                else:
                    pei[npv] = hi_i; pp[npv] = hi; pt[npv] = 1; pci[npv] = i; pc[npv] = hi_c; npv += 1
                    dr = -1; ext = lo; ei = lo_i; ec = lo_c
        elif dr == 1:
            if b > ext:
                ext = b; ei = i; ec = c
            elif ext - a >= X:
                pei[npv] = ei; pp[npv] = ext; pt[npv] = 1; pci[npv] = i; pc[npv] = ec; npv += 1
                dr = -1; ext = a; ei = i; ec = c
        else:
            if a < ext:
                ext = a; ei = i; ec = c
            elif b - ext >= X:
                pei[npv] = ei; pp[npv] = ext; pt[npv] = -1; pci[npv] = i; pc[npv] = ec; npv += 1
                dr = 1; ext = b; ei = i; ec = c
        D[i] = dr; NP[i] = npv
        if dr != 0:
            E[i] = ext; EI[i] = ei
    return D, E, EI, NP, pei[:npv].copy(), pp[:npv].copy(), pt[:npv].copy(), pci[:npv].copy(), pc[:npv].copy()


@njit(cache=False)
def resolver_nb(hi, lo, ult, i0, i1, c, Y):
    """Para cada punto j (entrada c[j] al cierre; ordenes i0[j] .. i1[j]-1): etiqueta (1 arriba primero, 0 abajo, -1 nada, 2 empate) e
    indice de la orden que resuelve; y la regla: compra (objetivo c+Y pasado 1 tick, stop c-Y al tocar; misma orden -> stop) y venta
    en espejo: bruto en pts e indice de salida (si no resuelve: ultimo precio antes del plazo, indice i1-1)."""
    n = len(c)
    lab = np.full(n, -1, np.int8); ilab = np.full(n, -1, np.int64)
    pl = np.full(n, np.nan); il = np.full(n, -1, np.int64); ps = np.full(n, np.nan); is_ = np.full(n, -1, np.int64)
    for j in range(n):
        up = c[j] + Y; dn = c[j] - Y; upt = up + 0.25; dnt = dn - 0.25
        dl = False; dL = False; dS = False
        a = i0[j]; b = i1[j]
        for i in range(a, b):
            h = hi[i]; l = lo[i]
            if not dl:
                tu = h >= up; td = l <= dn
                if tu and td:
                    lab[j] = 2; ilab[j] = i; dl = True
                elif tu:
                    lab[j] = 1; ilab[j] = i; dl = True
                elif td:
                    lab[j] = 0; ilab[j] = i; dl = True
            if not dL:
                if l <= dn:
                    pl[j] = -Y; il[j] = i; dL = True
                elif h >= upt:
                    pl[j] = Y; il[j] = i; dL = True
            if not dS:
                if h >= up:
                    ps[j] = -Y; is_[j] = i; dS = True
                elif l <= dnt:
                    ps[j] = Y; is_[j] = i; dS = True
            if dl and dL and dS:
                break
        if b > a:
            if not dL:
                pl[j] = ult[b - 1] - c[j]; il[j] = b - 1
            if not dS:
                ps[j] = c[j] - ult[b - 1]; is_[j] = b - 1
    return lab, ilab, pl, il, ps, is_


# ---------------------------------------------------------------------------------------------------------------- perfil (R19)
def explotar_ticks(pri, ult, vol):
    """Reparte cada orden parejo entre los ticks que recorrio (aproximacion declarada de la R17; copia de r19_01_rasgos)."""
    p0 = np.rint(pri / TK).astype(np.int64); p1 = np.rint(ult / TK).astype(np.int64)
    n = np.abs(p1 - p0) + 1
    rep = np.repeat(np.arange(len(pri)), n)
    paso = np.where(p1 >= p0, 1, -1)
    off = np.arange(len(rep)) - np.repeat(np.cumsum(n) - n, n)
    return p0[rep] + off * paso[rep], vol[rep] / n[rep]


def area_valor(vol, pct=70, paso=2):
    """Copia exacta de r19_01_rasgos.area_valor (= r17_comun.area_valor, regla de ATAS verificada 1.186/1.186)."""
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


def poc_vah_val(pri, ult, vol):
    tk, v = explotar_ticks(pri, ult, vol)
    t0 = int(tk.min()); arr = np.zeros(int(tk.max()) - t0 + 1)
    np.add.at(arr, tk - t0, v)
    poc, vah, val = area_valor(arr)
    return (t0 + poc) * TK, (t0 + vah) * TK, (t0 + val) * TK


# ---------------------------------------------------------------------------------------------------------------- historia
def historia_semilla():
    """Ruedas de ES ANTERIORES al 08-09-2026 desde la historia de la R19 (velas de 5 min de ES): rango, H/L/C/O y POC/VAH/VAL RTH."""
    res = json.load(open(RESUMEN_R19))
    out = {}
    for k, v in res.items():
        inst, d = k.split("|")
        if inst == "ES" and "rth_h" in v and d < "2026-09-08":
            out[d] = {kk: float(v[kk]) for kk in ("rth_h", "rth_l", "rth_c", "rth_o", "poc", "vah", "val")}
            out[d]["contrato"] = "U6"; out[d]["fuente"] = "r19_velas_m5"
    return out


def X_de(hist, dia):
    previas = sorted(d for d, v in hist.items() if d < dia and "rth_h" in v)[-10:]
    if len(previas) < 10:
        raise ValueError("faltan ruedas previas para la X de %s (%d)" % (dia, len(previas)))
    med = float(np.median([hist[d]["rth_h"] - hist[d]["rth_l"] for d in previas]))
    return float(np.floor(0.08 * med / TK + 0.5) * TK), med, previas


def umbral_big(hist_tam_prev, q=0.99):
    """p99 del tamano de orden por franja de 30 min de la rueda (13), con hasta 10 ruedas previas (minimo 2) (r19 umbral_tamano)."""
    out = np.full(13, np.inf)
    if len(hist_tam_prev) < 2:
        return out
    H = sum(hist_tam_prev[-10:])
    for f in range(13):
        c = np.cumsum(H[f]); tot = c[-1]
        if tot > 0:
            out[f] = float(np.searchsorted(c, q * tot))
    return out


# ---------------------------------------------------------------------------------------------------------------- clima
def estado_minuto(rrn, c, vw, apertura):
    """Copia de regla_congelada_comb_t7_v4.estado_minuto: 'T' TENDENCIA, 'R' ROTACION, 'E' ESTRECHO, 'N' rrn >= 1,6 con lados distintos."""
    rrn = np.asarray(rrn, float); c = np.asarray(c, float); vw = np.asarray(vw, float); apertura = np.asarray(apertura, float)
    lvw = np.sign(c - vw); lo = np.sign(c - apertura)
    tipo = np.full(len(rrn), "N", dtype=object)
    ok = np.isfinite(rrn)
    tipo[ok & (rrn >= CORTE_TENDENCIA) & (lvw == lo) & (lvw != 0)] = "T"
    tipo[ok & (rrn > CORTE_ESTRECHO) & (rrn < CORTE_TENDENCIA)] = "R"
    tipo[ok & (rrn <= CORTE_ESTRECHO)] = "E"
    return tipo, lvw


# ---------------------------------------------------------------------------------------------------------------- una jornada
def primero_desde(tmax, T):
    """Indice de la PRIMERA orden (en el orden del archivo) con t >= T (tmax = maximo acumulado de t)."""
    return np.searchsorted(tmax, T, side="left")


def procesar_jornada(dia, ruta, hist, hist_tam_prev, f=None, con_resultados=True):
    """Tabla por minuto de una jornada. hist: {dia: resumen} con las ruedas previas; hist_tam_prev: histogramas de tamano (13 x 3001) de las
    ruedas previas en orden. Devuelve (tabla, resumen_de_hoy, hist_tam_de_hoy, info)."""
    f = curva_f() if f is None else f
    X, med10, previas = X_de(hist, dia)
    C = leer_cinta(ruta, dia)
    t = C["t"]; s = C["s"]; pri = C["primero"]; ult = C["ultimo"]; vol = C["vol"]; lado = C["lado"]
    n = len(t)
    tmax = np.maximum.accumulate(t)
    mn = np.minimum(pri, ult); mx = np.maximum(pri, ult)
    sv = vol * lado
    cum = np.cumsum(sv); cvol = np.cumsum(vol)
    # --- maquinas: ola (X) y marea (4X), desde las 18:00 NY
    oD, oE, oEI, oNP, o_pei, o_pp, o_pt, o_pci, o_pc = maquina_nb(t, mn, mx, cum, X)
    mD, mE, mEI, mNP, m_pei, m_pp, m_pt, m_pci, m_pc = maquina_nb(t, mn, mx, cum, 4 * X)
    # --- cortes de minuto
    T = np.array([ns_ny(dia, RTH0 + 60 * (m + 1)) for m in range(390)], np.int64)
    k = primero_desde(tmax, T) - 1                                             # ultima orden antes del cierre
    T16 = ns_ny(dia, RTH1)
    rth = (s >= RTH0) & (s < RTH1)
    if not rth.any():
        raise ValueError("%s: la cinta no tiene rueda" % dia)
    i_rth0 = int(np.argmax(rth))
    i_rth_fin = int(np.nonzero(rth)[0][-1])
    O = float(ult[i_rth0])
    # --- VWAP de jornada y RTH por orden (precio medio de la orden)
    pm = (pri + ult) / 2.0
    cpv = np.cumsum(pm * vol); cp2v = np.cumsum(pm * pm * vol)
    rmask = (s >= RTH0).astype(float)
    rpv = np.cumsum(pm * vol * rmask); rp2v = np.cumsum(pm * pm * vol * rmask); rv = np.cumsum(vol * rmask)
    with np.errstate(invalid="ignore", divide="ignore"):
        vwj = cpv / cvol; sdj = np.sqrt(np.maximum(cp2v / cvol - vwj * vwj, 0.0))
        vwr = np.where(rv > 0, rpv / np.where(rv > 0, rv, 1), np.nan)
        sdr = np.where(rv > 0, np.sqrt(np.maximum(rp2v / np.where(rv > 0, rv, 1) - vwr * vwr, 0.0)), np.nan)
    # --- maximo y minimo de la rueda hasta cada orden
    hR = np.where(rth, mx, -np.inf); lR = np.where(rth, mn, np.inf)
    Hc = np.maximum.accumulate(hR); Lc = np.minimum.accumulate(lR)
    # --- balance inicial, Asia, Londres
    ibm = (s >= RTH0) & (s < IB_FIN)
    ib_h = float(mx[ibm].max()) if ibm.any() else np.nan; ib_l = float(mn[ibm].min()) if ibm.any() else np.nan
    asia = s < ASIA_FIN; lon = (s >= ASIA_FIN) & (s < LON_FIN)
    niv = {"asia_h": float(mx[asia].max()) if asia.any() else np.nan, "asia_l": float(mn[asia].min()) if asia.any() else np.nan,
           "lon_h": float(mx[lon].max()) if lon.any() else np.nan, "lon_l": float(mn[lon].min()) if lon.any() else np.nan,
           "ib_h": ib_h, "ib_l": ib_l}
    ibr = max(ib_h - ib_l, TK) if np.isfinite(ib_h) else np.nan
    niv.update({"ib_h05": ib_h + 0.5 * ibr, "ib_h10": ib_h + ibr, "ib_l05": ib_l - 0.5 * ibr, "ib_l10": ib_l - ibr})
    # ayer (con el roll)
    pdia = sorted(d for d in hist if d < dia and "rth_h" in hist[d])[-1]
    aj = SPREAD_ROLL if (hist[pdia].get("contrato", contrato(pdia)) == "U6" and contrato(dia) == "Z6") else 0.0
    for kk, nom in (("rth_h", "pdh"), ("rth_l", "pdl"), ("rth_c", "pdc"), ("poc", "pd_poc"), ("vah", "pd_vah"), ("val", "pd_val")):
        niv[nom] = float(hist[pdia][kk]) + aj
    # --- flujo por orden
    franja = np.clip(((s - RTH0) // 1800).astype(np.int64), 0, 12)
    ub = umbral_big(hist_tam_prev)
    thr = np.where(rth, ub[franja], np.inf)
    big = vol >= thr
    med = (vol >= 10) & (vol <= 49)
    c_med = np.cumsum(np.where(med, sv, 0.0)); c_big = np.cumsum(np.where(big, sv, 0.0))
    iceb = (lado == -1) & (vol > C["abidv"]) & (C["dbid"] == C["abid"]) & (C["dbidv"] >= 1)
    icea = (lado == 1) & (vol > C["aaskv"]) & (C["dask"] == C["aask"]) & (C["daskv"] >= 1)
    c_ice = np.cumsum(iceb.astype(float) - icea.astype(float))

    def suma(cs, a, b):          # suma de cs sobre ordenes a .. b (inclusive), b >= a - 1
        return np.where(b >= a, cs[np.maximum(b, 0)] - np.where(a > 0, cs[np.maximum(a - 1, 0)], 0.0), 0.0)

    def ventana(w):
        a = primero_desde(tmax, T - int(w * 1e9))
        vv = suma(cvol, a, k)
        return a, vv
    rows = {}
    m_ = np.arange(390)
    c = ult[k]
    rows["dia"] = np.full(390, dia); rows["m"] = m_; rows["X"] = np.full(390, X); rows["med10"] = np.full(390, med10)
    rows["c"] = c
    # La Ola
    for pref, D, E, NP, pei, pp, pt, pci, pc, tope_lar, tope_min in (
            ("o", oD, oE, oNP, o_pei, o_pp, o_pt, o_pci, o_pc, 4.0, 60.0), ("m", mD, mE, mNP, m_pei, m_pp, m_pt, m_pci, m_pc, 16.0, 240.0)):
        d_ = D[k].astype(float); e_ = E[k]; j = NP[k] - 1
        if len(pei) == 0:
            raise ValueError("%s: la maquina %s no tiene pivotes" % (dia, pref))
        ok = j >= 0
        jj = np.maximum(j, 0)
        p0 = np.where(ok, pp[jj], np.nan); i0 = np.where(ok, pei[jj], 0); c0 = np.where(ok, pc[jj], 0.0); ic = np.where(ok, pci[jj], 0)
        rows[pref + "_dir"] = d_
        rows[pref + "_ext"] = e_
        rows[pref + "_p0"] = p0
        rows[pref + "_ret"] = np.abs(e_ - c) / X
        rows[pref + "_lar"] = np.minimum(np.abs(e_ - p0) / X, tope_lar)
        rows[pref + "_min"] = np.minimum((T - t[i0]) / 6e10, tope_min)
        dl = cum[k] - c0; vl = cvol[k] - cvol[i0]
        rows[pref + "_delta"] = dl
        rows[pref + "_dnorm"] = np.where(vl > 0, dl / np.where(vl > 0, vl, 1), 0.0)
        rows[pref + "_con"] = ((np.sign(dl) == d_) & (d_ != 0)).astype(float)
        rows[pref + "_conf_min"] = (T - t[ic]) / 6e10
        rows[pref + "_ipiv"] = j
    # clima R21
    Hm = np.where(np.isfinite(Hc[k]), Hc[k], c); Lm = np.where(np.isfinite(Lc[k]), Lc[k], c)
    hay_rth = k >= i_rth0
    Hm = np.where(hay_rth, Hm, c); Lm = np.where(hay_rth, Lm, c)
    rows["H"] = Hm; rows["L"] = Lm; rows["O"] = np.full(390, O)
    rrn = (Hm - Lm) / (med10 * f)
    vw = vwr[k]; sd = sdr[k]
    vw = np.where(hay_rth, vw, np.nan); sd = np.where(hay_rth, sd, np.nan)
    tipo, lvw = estado_minuto(rrn, c, vw, np.where(hay_rth, O, np.nan))
    rows["rrn"] = rrn; rows["tipo"] = tipo
    rows["lado_vw"] = np.nan_to_num(lvw); rows["lado_ap"] = np.sign(c - O)
    rows["lado_T"] = np.where(tipo == "T", np.nan_to_num(lvw), 0.0)
    rows["vw"] = vw; rows["sd"] = sd
    rows["vw_X"] = np.clip(np.nan_to_num((c - vw) / X), -8, 8)
    rows["vw_sd"] = np.clip(np.nan_to_num(np.where(sd > 0, (c - vw) / np.where(sd > 0, sd, 1), 0.0)), -4, 4)
    # IB (desde el minuto 60, como la R21)
    eu = np.maximum(0, Hm - ib_h) / ibr; ed = np.maximum(0, ib_l - Lm) / ibr
    rows["ib_ext_arr"] = np.where(m_ >= 60, eu, 0.0); rows["ib_ext_aba"] = np.where(m_ >= 60, ed, 0.0)
    rows["ib_net"] = np.clip(rows["ib_ext_arr"] - rows["ib_ext_aba"], -3, 3)
    rows["mins"] = (m_ + 1).astype(float); rows["tarde"] = ((m_ + 1) >= 210).astype(float)
    # flujo
    for w, nom in ((60, "d1"), (300, "d5"), (900, "d15")):
        a, vv = ventana(w)
        rows[nom] = np.where(vv > 0, suma(cum, a, k) / np.where(vv > 0, vv, 1), 0.0)
        if w in (300, 900):
            rows["med" + nom[1:]] = np.where(vv > 0, suma(c_med, a, k) / np.where(vv > 0, vv, 1), 0.0)
        if w == 300:
            rows["big5"] = np.where(vv > 0, suma(c_big, a, k) / np.where(vv > 0, vv, 1), 0.0)
            rows["delta300"] = suma(cum, a, k)
        if w == 60:
            rows["ice1"] = np.clip(suma(c_ice, a, k), -5, 5)
    # zona y barrida en el extremo de ORIGEN de la pierna de la ola en curso
    Tz = max(2 * TK, math.floor(X / 8 / TK + 0.5) * TK)
    zona = np.zeros(390); barr = np.zeros(390); bandaV4 = np.zeros(390)
    for m in range(390):
        j = rows["o_ipiv"][m]
        if j < 0:
            continue
        ie = int(o_pei[j]); Ee = float(o_pp[j]); dd = -int(o_pt[j]); se = s[ie]
        Ls = []
        for vwa, sda, desde in ((vwr, sdr, RTH0), (vwj, sdj, -1e18)):
            if se >= desde and np.isfinite(vwa[ie]):
                for q in (-2, -1, 0, 1, 2):
                    Ls.append(vwa[ie] + q * sda[ie])
        for nom in ("pdh", "pdl", "pdc", "pd_poc", "pd_vah", "pd_val"):
            Ls.append(niv[nom])
        if se >= ASIA_FIN: Ls += [niv["asia_h"], niv["asia_l"]]
        if se >= LON_FIN: Ls += [niv["lon_h"], niv["lon_l"]]
        if se >= IB_FIN: Ls += [niv[x] for x in ("ib_h", "ib_l", "ib_h05", "ib_h10", "ib_l05", "ib_l10")]
        Ls.append(50.0 * round(Ee / 50.0))
        Ls = np.array([x for x in Ls if np.isfinite(x)])
        if dd == 1:
            z = (Ls - X / 4 <= Ee) & (Ee <= Ls + Tz); pen = Ls - Ee
        else:
            z = (Ls - Tz <= Ee) & (Ee <= Ls + X / 4); pen = Ee - Ls
        zona[m] = float(z.any())
        barr[m] = float(((pen > 0) & (pen <= X / 2) & ((c[m] - Ls) * dd > 0)).any())
        # banda de 2 sigma respetada (V4 de la R21): extremo entre la banda y X/2 adentro, sin perforarla
        okb = False
        for vwa, sda, desde in ((vwr, sdr, RTH0), (vwj, sdj, -1e18)):
            if se >= desde and np.isfinite(vwa[ie]):
                Lb = vwa[ie] - 2 * sda[ie] if dd == 1 else vwa[ie] + 2 * sda[ie]
                pb = (Lb - Ee) * dd
                if -X / 2 - 1e-9 <= pb <= 1e-9:
                    okb = True
        bandaV4[m] = float(okb)
    rows["zona_o"] = zona; rows["barr_o"] = barr
    rows["zona_o_s"] = np.maximum(zona, barr) * rows["o_dir"]
    # iceberg del lado que defiende en los 60 s previos a la vuelta (F2 de la R19), por pivote de la ola
    ice_piv = np.zeros(len(o_pei))
    for j in range(len(o_pei)):
        ic = int(o_pci[j]); a = int(primero_desde(tmax, t[ic] - int(60e9))); Ee = o_pp[j]
        if o_pt[j] == -1:          # minimo: la ola se dio vuelta para arriba, defiende el bid
            sel = iceb[a:ic]
            ice_piv[j] = float(sel.any() and ult[a:ic][sel].min() <= Ee + X / 2)
        else:
            sel = icea[a:ic]
            ice_piv[j] = float(sel.any() and ult[a:ic][sel].max() >= Ee - X / 2)
    jj = np.maximum(rows["o_ipiv"], 0)
    ice_v4 = np.where(rows["o_ipiv"] >= 0, ice_piv[jj], 0.0)
    # expertos al grano del minuto
    reciente = rows["o_conf_min"] <= 5.0
    lt = rows["lado_T"]
    v_t7 = np.where((tipo == "T") & (rows["o_dir"] == lt) & (rows["m_dir"] == lt) & (lt != 0) & reciente &
                    ((zona > 0) | (barr > 0) | (rows["delta300"] * lt > 0)), lt, 0.0)
    v_v4 = np.where((tipo == "R") & reciente & (bandaV4 > 0) & (ice_v4 > 0), rows["o_dir"], 0.0)
    sA = np.sign(c - vw)
    ext_lado = np.where(sA > 0, eu, ed)
    stopA = np.where(sA > 0, Hm + dtick(X / 2), Lm - dtick(X / 2))
    dst = np.abs(stopA - c); dvw = np.abs(c - vw)
    with np.errstate(invalid="ignore", divide="ignore"):
        PA = dst / (dst + dvw)
    okA = (m_ + 1 >= 210) & (m_ + 1 <= 360) & np.isfinite(vw) & (dvw >= 1.5 * X) & (ext_lado <= 1.0) & (PA >= 0.55) & (sA != 0)
    v_a3 = np.where(okA, -sA, 0.0)
    rows["v_T7"] = v_t7; rows["v_V4"] = v_v4; rows["v_A3"] = v_a3
    rows["es_mes"] = np.full(390, np.nan)          # lugar reservado para la R26 (fusionar_r26)
    # resultados
    if con_resultados:
        i0 = primero_desde(tmax, T)
        Tend = np.minimum(T + PLAZO_S * 10 ** 9, T16)
        i1 = primero_desde(tmax, Tend)
        for kY in YS:
            Y = Y_de(X, kY)
            lab, ilab, pl, il, ps, is_ = resolver_nb(mx, mn, ult, i0, i1, c, Y)
            rows["y_" + kY] = np.where(lab <= 1, lab, -1).astype(float)
            rows["y_" + kY][lab == -1] = np.nan
            rows["empate_" + kY] = (lab == 2).astype(float)
            rows["nada_" + kY] = (lab == -1).astype(float)
            rows["y_" + kY][lab == 2] = np.nan
            rows["tres_" + kY] = np.where(ilab >= 0, (t[np.maximum(ilab, 0)] - T) / 6e10, np.nan)
            rows["pL_" + kY] = pl; rows["pS_" + kY] = ps
            rows["tL_" + kY] = np.where(il >= 0, t[np.maximum(il, 0)], -1)
            rows["tS_" + kY] = np.where(is_ >= 0, t[np.maximum(is_, 0)], -1)
            rows["Y_" + kY] = np.full(390, Y)
    rows["T"] = T
    tab = pd.DataFrame(rows)
    tab = tab[tab["m"] < NMIN].reset_index(drop=True)
    # resumen de hoy (para manana) e histograma de tamanos de la rueda
    rr = np.nonzero(rth)[0]
    poc, vah, val = poc_vah_val(pri[rr], ult[rr], vol[rr])
    hoy = {"rth_h": float(mx[rr].max()), "rth_l": float(mn[rr].min()), "rth_c": float(ult[rr[-1]]), "rth_o": O, "poc": poc, "vah": vah,
           "val": val, "contrato": contrato(dia), "fuente": "cinta"}
    hist_tam = np.zeros((13, 3001))
    np.add.at(hist_tam, (franja[rth], np.clip(vol[rth].astype(np.int64), 0, 3000)), 1)
    huecos = np.diff(t[rr]) / 1e9 if len(rr) > 1 else np.array([0.0])
    info = {"dia": dia, "ordenes": n, "retrocesos_t": C["retrocesos"], "X": X, "med10": med10, "previas": previas,
            "ultima_rth_s": float(s[i_rth_fin]), "hueco_rth_max_s": float(huecos.max()), "umbral_big": ub.tolist(),
            "pivotes_ola": int(len(o_pei)), "pivotes_marea": int(len(m_pei)), "niv": niv, "O": O}
    return tab, hoy, hist_tam, info


def fusionar_r26(tabla, ruta):
    """Lugar para la R26 (cruce ES/MES): llena la columna es_mes desde una tabla (parquet o csv) con columnas dia, m, valor."""
    if not ruta or not os.path.exists(ruta):
        return tabla
    x = pd.read_parquet(ruta) if ruta.endswith(".parquet") else pd.read_csv(ruta)
    x = x[["dia", "m", "valor"]].rename(columns={"valor": "es_mes_nuevo"})
    t = tabla.merge(x, on=["dia", "m"], how="left")
    t["es_mes"] = t["es_mes_nuevo"]
    return t.drop(columns=["es_mes_nuevo"])
