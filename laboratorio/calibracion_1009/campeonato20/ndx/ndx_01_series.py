# -*- coding: utf-8 -*-
"""ndx_01_series.py — campeonato20, familia NDX (cadena _NDX de CBOE): las series de la 4.1 reconstruidas MINUTO A MINUTO, sin mirar
adelante, en todas las sesiones del dataset con fotos de NDX. SOLO LECTURA (dataset unificado calibracion_1009/datos via cargar.py y
datos/velas_ndx.csv de ndx_00_velas.py). No toca ATAS ni el codigo del indicador.

Port (Python) de atas/PythiaGexCuatro/_modulos/familia (4.1.3), leido el 09-10:
  ndx/LibroMinuteroNdx.Calcular, comun/Perfil.cs (FilasHoy, PerfilLado), comun/ZeroEstandar.cs (+ CacheZero), comun/LibroMinutoArmado.cs
  (ArmadoLibroMinuto: filtro +-3 %, SeriesLibroFam), comun/Seleccion.cs (MUROS, MAJORS, ZTP con islas C3, ZEST), replica20/Replica20.cs
  (Nucleo20.Perfil/Dominantes, BaseNdx20.Base) = backtest_familia.minutos_cboe(libro='NDX', variante='sync').
CLAVE t (inicio del minuto, UTC) = lo VIGENTE en la vela t (como el jsonl de la 4.1): fut = cierre de la ultima vela m2 CERRADA antes de t
  (m2 alineadas a minutos pares; cierre = el de la ultima M1 de la m2; solo velas de la sesion de t desde las 18:00 NY; tope 4 h).
  El juez se corre con desfase_min = 0.
LIBRO C7 (MUROS/MAJORS/ZTP/ZEST/DOMS): foto = la ultima con generado <= t (todas las sesiones, en orden de generado); vale si
  t < vigencia (C9, columna del dataset); base = C7 del dataset (cargar._base_ndx_cuatro: de dia mediana robusta de 24 muestras
  sincronizadas; de noche mediana de la ultima rueda con >= 20 muestras x decaimiento del carry; NaN si la rueda es de otro contrato).
  S = fut - base; FilasHoy (envejecido desde 'generado', vencido afuera, tope max(1, mas cercano + 0,01)); PerfilLado (BS r 0,0375 sin
  dividendo; por lado; calls +, puts -; x100 x S^2 x 1 %); Fut = K + base; solo |Fut - fut| <= 3 % fut.
    MUROS_NDX_vol/oi  D1 = muro C (argmax C > 0 en R = min(2 % fut, 100)), D2 = muro P (argmin P < 0 en R)
    MAJORS_NDX_vol/oi D1 = M+ (argmax neto > 0 en R), D2 = M- (argmin neto < 0 en R)
    ZTP_NDX_vol       cruces de signo del neto por volumen sin islas (C3, 0,10), |z - fut| <= 100: D1 = el menor arriba, D2 = el mayor abajo
    ZEST_NDX_vol      zero estandar C5 (grilla 2,5 pts, +-300, centro round(S/50)*50; cruce mas cercano a S) + base, si |z - fut| <= 300.
                      Cache del zero: ver MODO_ZERO.
    DOMS_NDX_vol      la seleccion de la 2.0 sobre ese libro: las 2 de mayor |GEX neto vol| con |Fut - fut| <= R (orden estable por Fut);
                      si no hay ninguna con |vol| > 0, lo mismo con OI. D1 = la mayor, D2 = la segunda.
R20_NDX_vol (replica de la capa NDX de la 2.0): foto = la ultima con generado <= t (SIN vigencia, con filas); base = base_cruda (forwards)
  si |cruda - carry| <= max(0,6 |carry|, 0,0006 fut) con carry = fut x (0,0375 - 0,008) x dias al vencimiento del futuro del grafico
  (tercer viernes 13:30 UTC: U6 2026-09-18, Z6 2026-12-18) / 365; si no, el carry; si no hay carry, la cruda. Perfil de la 2.0 (sin filtro
  de 3 %; NaN -> 0; filas con gOi = gVol = 0 afuera), mismas 2 dominantes de mayor |gv| en R.
MODO_ZERO: 'tanda' = CacheZero de la 4.1 (clave (foto, floor((t - generado)/900 s), round(S/50)), valor calculado con el S del PRIMER
  minuto de la clave dentro de la tanda de 240 min alineada a las 22:00Z de la sesion: "determinista, igual en vivo y al reiniciar") o
  'fresco' (cada minuto con su S). En la sesion 2026-10-09 se calculan los dos para medir la paridad contra el jsonl real: 'fresco'
  calza mejor (74,7 % identico contra 55,3 %; los dos a <= 0,14 pts en 479 minutos, ndx_02_paridad.py) -> se usa 'fresco'.
Diferencias con la 4.1 que no se pueden evitar: la cadena es la del dataset (union de bajadas, dedup por cadena_ts, generado de la mas
  temprana) y solo trae strikes a <= 6 % del spot (el zero estandar suma solo esos); el contrato del grafico es el FRENTE (U6 hasta la
  sesion del 09-14); el precio de la C7 es el del dataset (paridad medida 100 % esta noche). Las fotos de la pausa 21-22Z no estan.
Salida: datos/series/ndx_<sesion>.pkl = {'series': {nombre: {Timestamp: {'D1': (precio, etiqueta), ...}}}, 'meta': DataFrame}.
Uso: python -I ndx_01_series.py <i> <n>   (procesa las sesiones i, i+n, ...; maximo 3 procesos en paralelo)
     python -I ndx_01_series.py <sesion>  (una sola)"""
import ctypes
import json
import math
import os
import sys
import time

try:
    ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)   # prioridad baja: no trabar ATAS
except Exception:
    pass
os.environ.setdefault("OMP_NUM_THREADS", "1"); os.environ.setdefault("OPENBLAS_NUM_THREADS", "1"); os.environ.setdefault("MKL_NUM_THREADS", "1")

import numpy as np  # noqa: E402
import pandas as pd  # noqa: E402

AQUI = os.path.dirname(os.path.abspath(__file__))
CAL1009 = os.path.normpath(os.path.join(AQUI, "..", ".."))
sys.path.insert(0, os.path.join(CAL1009, "datos"))
import cargar as D  # noqa: E402

TASA = 0.0375
PISO_DIAS = 1.0 / 1440.0
DIV20 = 0.008
EXP20 = {"U6": pd.Timestamp("2026-09-18 13:30:00"), "Z6": pd.Timestamp("2026-12-18 13:30:00"), "H7": pd.Timestamp("2027-03-19 13:30:00")}
SERIES = ("MUROS_NDX_vol", "MUROS_NDX_oi", "MAJORS_NDX_vol", "MAJORS_NDX_oi", "ZTP_NDX_vol", "ZEST_NDX_vol", "DOMS_NDX_vol", "R20_NDX_vol")
SALIDA = os.path.join(AQUI, "datos", "series")
MIN_NS = 60_000_000_000


# ------------------------------------------------------------------ gamma
def gamma_bs(S, K, T, iv):
    S = np.asarray(S, float); K = np.asarray(K, float); T = np.asarray(T, float); iv = np.asarray(iv, float)
    ok = (S > 0) & (K > 0) & (T > 0) & (iv > 0)
    ivs = np.where(ok, iv, 1.0); Ts = np.where(ok, T, 1.0); Ks = np.where(ok, K, 1.0); Ss = np.where(ok, S, 1.0)
    v = ivs * np.sqrt(Ts)
    d1 = (np.log(Ss / Ks) + (TASA + 0.5 * ivs * ivs) * Ts) / v
    g = np.exp(-0.5 * d1 * d1) / math.sqrt(2 * math.pi) / (Ss * v)
    return np.where(ok, g, 0.0)


# ------------------------------------------------------------------ fut = CierreConocido
def ini_sesion_ns(t):
    """inicio de la sesion de CME de t (18:00 NY) en ns UTC."""
    ny = pd.Timestamp(t).tz_localize("UTC").tz_convert(D.NY)
    d = ny.normalize()
    ini = d + pd.Timedelta(hours=18) if ny.hour >= 18 else d - pd.Timedelta(days=1) + pd.Timedelta(hours=18)
    return int(ini.tz_convert("UTC").tz_localize(None).value)


class Cierres:
    """CierreConocido sobre velas m2 armadas con las M1 (c = la ultima M1 de la m2)."""

    def __init__(self, V):
        tn = V["t"].to_numpy().astype("datetime64[ns]").astype(np.int64)
        b = (tn // (2 * MIN_NS)) * (2 * MIN_NS)
        df = pd.DataFrame({"b": b, "c": V["c"].to_numpy(float), "tn": tn}).sort_values("tn")
        g = df.groupby("b")["c"].last()
        self.b = g.index.to_numpy(np.int64); self.c = g.to_numpy(float)
        self.ultimo = int(tn.max()) + MIN_NS

    def __call__(self, t):
        tn = int(pd.Timestamp(t).value)
        ini = ini_sesion_ns(t)
        b0 = ((tn - 2 * MIN_NS) // (2 * MIN_NS)) * (2 * MIN_NS)
        j = int(np.searchsorted(self.b, b0, side="right")) - 1
        while j >= 0:
            b = int(self.b[j])
            if b < ini:
                return float("nan")
            if tn - (b + 2 * MIN_NS) > 4 * 3600 * 10**9:
                return float("nan")
            if b + 2 * MIN_NS <= self.ultimo:
                return float(self.c[j])
            j -= 1
        return float("nan")


# ------------------------------------------------------------------ fotos y filas
def todas_las_fotos():
    partes = []
    for d in D.dias("NDX"):
        f = D.fotos("NDX", d)
        if len(f):
            f = f.copy(); f["dia"] = d
            partes.append(f)
    F = pd.concat(partes, ignore_index=True)
    F["generado"] = F["generado"].dt.floor("s")
    F = F.sort_values("generado", kind="stable").reset_index(drop=True)
    return F


class Filas:
    def __init__(self, F):
        self.F = F
        self.cache = {}

    def __call__(self, k):
        if k in self.cache:
            return self.cache[k]
        r = self.F.iloc[k]
        x = D.filas("NDX", r["dia"], int(r["foto"]))
        vd = {int(a): float(b) for a, b in json.loads(r["vencs"]).items()}
        z = lambda c: np.nan_to_num(x[c].to_numpy(float), nan=0.0)
        out = dict(K=x["strike"].to_numpy(float), dias=x["dias"].to_numpy(float), oic=z("oi_call"), oip=z("oi_put"),
                   ivc=np.nan_to_num(x["iv_call"].to_numpy(float), nan=0.0), ivp=np.nan_to_num(x["iv_put"].to_numpy(float), nan=0.0),
                   volc=z("vol_call"), volp=z("vol_put"), vdias=np.array(sorted(vd.values()), float), gen=r["generado"])
        if len(self.cache) > 6:
            self.cache.pop(next(iter(self.cache)))
        self.cache[k] = out
        return out


def filas_hoy(c, t):
    """FilasHoy.Armar: (K, T, oic, oip, ivc, ivp, volc, volp) del horizonte Hoy envejecido; None si no entra ninguna."""
    if len(c["K"]) == 0:
        return None
    env = max(0.0, min(2.0, (pd.Timestamp(t) - c["gen"]).total_seconds() / 86400.0))
    de = c["vdias"] - env
    v = de[de >= 0]
    mas = float(v.min()) if len(v) else 0.0
    tope = max(1.0, mas + 0.01)
    d = c["dias"] - env
    ok = (d >= 0) & (d <= tope)
    if not ok.any():
        return None
    return dict(K=c["K"][ok], T=np.maximum(d[ok], PISO_DIAS) / 365.0, oic=c["oic"][ok], oip=c["oip"][ok], ivc=c["ivc"][ok],
                ivp=c["ivp"][ok], volc=c["volc"][ok], volp=c["volp"][ok])


def perfil_lado(fl, S):
    if fl is None or not (S > 0):
        return None
    gc = gamma_bs(S, fl["K"], fl["T"], fl["ivc"]); gp = gamma_bs(S, fl["K"], fl["T"], fl["ivp"])
    esc = 100.0 * S * S * 0.01
    avc, avp = gc * fl["volc"] * esc, -gp * fl["volp"] * esc
    aoc, aop = gc * fl["oic"] * esc, -gp * fl["oip"] * esc
    ap = (avc + avp != 0) | (aoc + aop != 0)
    if not ap.any():
        return None
    ks, inv = np.unique(fl["K"][ap], return_inverse=True)
    n = len(ks)
    s = lambda x: np.bincount(inv, x[ap], n)
    r = dict(K=ks, gvC=s(avc), gvP=s(avp), goC=s(aoc), goP=s(aop))
    r["gv"] = r["gvC"] + r["gvP"]; r["go"] = r["goC"] + r["goP"]
    return r


def zero_estandar(fl, S, paso=2.5, semi=300.0, cp=50.0):
    """C5 en el eje del libro: (zv, zo); NaN si no hay cruce."""
    if fl is None or not (S > 0):
        return float("nan"), float("nan")
    c0 = round(S / cp) * cp
    n = int(round(semi / paso))
    xs = c0 + paso * np.arange(-n, n + 1)
    X = xs[:, None]
    gc = gamma_bs(X, fl["K"][None, :], fl["T"][None, :], fl["ivc"][None, :])
    gp = gamma_bs(X, fl["K"][None, :], fl["T"][None, :], fl["ivp"][None, :])
    x2 = xs * xs
    cv = (gc * fl["volc"][None, :] - gp * fl["volp"][None, :]).sum(1) * x2
    co = (gc * fl["oic"][None, :] - gp * fl["oip"][None, :]).sum(1) * x2
    out = []
    for c in (cv, co):
        s = np.sign(c)
        idx = np.nonzero((s[:-1] * s[1:]) < 0)[0]
        if len(idx) == 0:
            out.append(float("nan")); continue
        z = xs[idx] + (xs[idx + 1] - xs[idx]) * (-c[idx]) / (c[idx + 1] - c[idx])
        out.append(float(z[np.argmin(np.abs(z - S))]))
    return out[0], out[1]


# ------------------------------------------------------------------ seleccion
def arg(Fut, x, fut, signo):
    r = min(0.02 * fut, 100.0)
    m = np.abs(Fut - fut) <= r
    m &= (x > 0) if signo > 0 else (x < 0)
    idx = np.flatnonzero(m)
    if not len(idx):
        return -1
    return int(idx[np.argmax(x[idx])]) if signo > 0 else int(idx[np.argmin(x[idx])])


def cruces(Fut, g, isla=0.10):
    nz = np.nonzero(g != 0)[0]
    if len(nz) < 2:
        return []
    if isla > 0 and len(nz) >= 3:
        gg = g[nz]; s = np.sign(gg); a = np.abs(gg)
        mid = (s[1:-1] != s[:-2]) & (s[1:-1] != s[2:]) & (a[1:-1] < isla * np.minimum(a[:-2], a[2:]))
        keep = np.ones(len(nz), bool); keep[1:-1] = ~mid
        nz = nz[keep]
    out = []
    for i0, i1 in zip(nz[:-1], nz[1:]):
        if g[i0] * g[i1] < 0:
            out.append(float(Fut[i0] + (Fut[i1] - Fut[i0]) * (-g[i0]) / (g[i1] - g[i0])))
    return out


def dominantes(K, Fut, gv, go, fut):
    """Nucleo20.Dominantes: las 2 de mayor |gv| en R (orden estable por K/Fut); si no hay, con go."""
    r = min(fut * 0.02, 100.0)
    for g in (gv, go):
        m = (np.abs(Fut - fut) <= r) & (np.abs(g) > 0)
        idx = np.flatnonzero(m)
        if len(idx):
            o = idx[np.argsort(-np.abs(g[idx]), kind="stable")][:2]
            return [(float(Fut[i]), float(g[i]), float(K[i])) for i in o]
    return []


def et(K):
    return "NDX%g" % K


# ------------------------------------------------------------------ una sesion
def procesar(dia, V, CC, F, filas, modos_zero=("tanda",)):
    gen = F["generado"].to_numpy().astype("datetime64[ns]").astype(np.int64)
    vig = F["vigencia"].to_numpy().astype("datetime64[ns]").astype(np.int64)
    N = pd.Timestamp(dia)
    a = N - pd.Timedelta(hours=2); b = N + pd.Timedelta(hours=21)
    vv = V[(V["t"] >= a) & (V["t"] < b)]
    if vv.empty:
        return None
    tlast = min(b, vv["t"].max() + pd.Timedelta(minutes=1))
    contr = D.contrato_de_sesion(dia)
    exp20 = EXP20[contr]
    ini_ses = a
    series = {s: {} for s in SERIES}
    zest_alt = {m: {} for m in modos_zero}
    caches = {m: {} for m in modos_zero}
    tanda_act = None
    meta = []
    for t in pd.date_range(a, tlast, freq="1min", inclusive="left"):
        fut = CC(t)
        res = {s: {} for s in SERIES}
        m = dict(t=t, sesion=dia, fut=fut, foto=None, gen=None, base=np.nan, base_modo="", S=np.nan, n_strikes=0, motivo="",
                 r20_foto=None, r20_base=np.nan, r20_origen="", r20_motivo="")
        tanda = int((t - ini_ses).total_seconds() // 60) // 240
        if tanda != tanda_act:
            tanda_act = tanda
            for mz in caches:
                if mz == "tanda":
                    caches[mz].clear()
        for mz in caches:
            if mz == "fresco":
                caches[mz].clear()
        k = int(np.searchsorted(gen, t.value, side="right")) - 1
        if fut != fut:
            m["motivo"] = "sin fut"; m["r20_motivo"] = "sin fut"
        elif k < 0:
            m["motivo"] = "sin foto"; m["r20_motivo"] = "sin foto"
        else:
            r = F.iloc[k]
            # ---------------- libro C7 de la 4.1
            m["foto"] = int(k); m["gen"] = r["generado"]
            if t.value >= vig[k]:
                m["motivo"] = "foto vencida (C9)"
            else:
                base, txt = D._base_ndx_cuatro(r, t)
                m["base"] = base; m["base_modo"] = str(r["modo"])
                if not (base == base):
                    m["motivo"] = "sin base: " + txt[:60]
                else:
                    c = filas(k)
                    S = fut - base
                    m["S"] = S
                    fl = filas_hoy(c, t)
                    p = perfil_lado(fl, S)
                    if p is None:
                        m["motivo"] = "sin gamma en Hoy"
                    else:
                        FutA = p["K"] + base
                        sel = np.abs(FutA - fut) <= fut * 0.03
                        K = p["K"][sel]; Fu = FutA[sel]
                        G = {x: p[x][sel] for x in ("gvC", "gvP", "gv", "goC", "goP", "go")}
                        m["n_strikes"] = int(len(K))
                        for nom, (A, B) in (("MUROS_NDX_vol", ("gvC", "gvP")), ("MUROS_NDX_oi", ("goC", "goP")),
                                            ("MAJORS_NDX_vol", ("gv", "gv")), ("MAJORS_NDX_oi", ("go", "go"))):
                            s = {}
                            i = arg(Fu, G[A], fut, +1)
                            if i >= 0:
                                s["D1"] = (float(Fu[i]), et(K[i]))
                            j = arg(Fu, G[B], fut, -1)
                            if j >= 0:
                                s["D2"] = (float(Fu[j]), et(K[j]))
                            res[nom] = s
                        zs = [z for z in cruces(Fu, G["gv"]) if abs(z - fut) <= 100.0]
                        ar = [z for z in zs if z > fut]; ab = [z for z in zs if z < fut]
                        s = {}
                        if ar:
                            s["D1"] = (min(ar), "NDXz%.1f" % (min(ar) - base))
                        if ab:
                            s["D2"] = (max(ab), "NDXz%.1f" % (max(ab) - base))
                        res["ZTP_NDX_vol"] = s
                        doms = dominantes(K, Fu, G["gv"], G["go"], fut)
                        res["DOMS_NDX_vol"] = {"D%d" % (q + 1): (d[0], et(d[2])) for q, d in enumerate(doms)}
                        # zero estandar (C5) con la cache elegida
                        env_b = int((t - r["generado"]).total_seconds() // 900)
                        ck = (k, env_b, round(S / 50.0))
                        for mz in caches:
                            if ck not in caches[mz]:
                                caches[mz][ck] = zero_estandar(fl, S)
                            zv = caches[mz][ck][0]
                            zz = {}
                            if zv == zv and abs(zv + base - fut) <= 300.0:
                                zz = {"Z": (zv + base, "NDXz%.1f" % zv)}
                            zest_alt[mz][t] = zz
                        res["ZEST_NDX_vol"] = zest_alt[modos_zero[0]][t]
                        m["motivo"] = "ok"
            # ---------------- R20 (la capa NDX de la 2.0)
            c = filas(k)
            if len(c["K"]) == 0:
                m["r20_motivo"] = "foto sin filas"
            else:
                carry = fut * (TASA - DIV20) * (exp20 - t).total_seconds() / 86400.0 / 365.0 if exp20 > t else float("nan")
                cruda = float(r["base_cruda"]) if r["base_cruda"] == r["base_cruda"] else 0.0
                err = r["base_error_ticks"]
                if cruda != 0 and (not (carry == carry) or abs(cruda - carry) <= max(abs(carry) * 0.6, fut * 0.0006)):
                    b20, org = cruda, "CRUDA %s ticks" % ("%.0f" % err if err == err else "?")
                elif carry == carry:
                    b20, org = carry, "TEORICA carry" + (" (cruda %.1f descartada)" % cruda if cruda != 0 else "")
                elif cruda != 0:
                    b20, org = cruda, "CRUDA sin cota"
                else:
                    b20, org = float("nan"), "sin base"
                m["r20_foto"] = int(k); m["r20_base"] = b20; m["r20_origen"] = org
                S20 = fut - b20 if b20 == b20 else float("nan")
                if not (S20 > 0):
                    m["r20_motivo"] = "sin base"
                else:
                    env = 0.0 if t <= c["gen"] else min(2.0, (t - c["gen"]).total_seconds() / 86400.0)
                    de = c["vdias"] - env
                    v = de[de >= 0]
                    mas = float(v.min()) if len(v) else 0.0
                    d = c["dias"] - env
                    ok = (d >= 0) & (d <= max(1.0, mas + 0.01))
                    if not ok.any():
                        m["r20_motivo"] = "sin filas en Hoy"
                    else:
                        K0 = c["K"][ok]; T0 = np.maximum(d[ok], PISO_DIAS) / 365.0
                        gC = gamma_bs(S20, K0, T0, c["ivc"][ok]); gP = gamma_bs(S20, K0, T0, c["ivp"][ok])
                        esc = 100.0 * S20 * S20 * 0.01
                        gvo = (gC * c["volc"][ok] - gP * c["volp"][ok]) * esc
                        goi = (gC * c["oic"][ok] - gP * c["oip"][ok]) * esc
                        ap = (gvo != 0) | (goi != 0)
                        if not ap.any():
                            m["r20_motivo"] = "sin gamma"
                        else:
                            ks, inv = np.unique(K0[ap], return_inverse=True)
                            Gv = np.bincount(inv, gvo[ap], len(ks)); Go = np.bincount(inv, goi[ap], len(ks))
                            doms = dominantes(ks, ks + b20, Gv, Go, fut)
                            res["R20_NDX_vol"] = {"D%d" % (q + 1): (d_[0], et(d_[2])) for q, d_ in enumerate(doms)}
                            m["r20_motivo"] = "ok"
        for s in SERIES:
            series[s][t] = res[s]
        meta.append(m)
    return {"series": series, "meta": pd.DataFrame(meta), "zest_modos": zest_alt}


def main():
    t0 = time.time()
    V = pd.read_csv(os.path.join(AQUI, "datos", "velas_ndx.csv"), parse_dates=["t"])
    CC = Cierres(V)
    F = todas_las_fotos()
    filas = Filas(F)
    ses = D.dias("NDX")
    if len(sys.argv) == 2:
        mias = [sys.argv[1]]
    else:
        i, n = int(sys.argv[1]), int(sys.argv[2])
        mias = ses[i::n]
    os.makedirs(SALIDA, exist_ok=True)
    for d in mias:
        t1 = time.time()
        modos = ("fresco", "tanda") if d == "2026-10-09" else ("fresco",)
        out = procesar(d, V, CC, F, filas, modos)
        if out is None:
            print("%s: sin velas" % d, flush=True)
            continue
        pd.to_pickle(out, os.path.join(SALIDA, "ndx_%s.pkl" % d))
        M = out["meta"]
        print("%s: %d min, C7 ok %d, R20 ok %d, %.0f s | %s" % (d, len(M), (M["motivo"] == "ok").sum(), (M["r20_motivo"] == "ok").sum(),
                                                               time.time() - t1, M["motivo"].value_counts().head(4).to_dict()), flush=True)
    print("listo %.0f s" % (time.time() - t0))


if __name__ == "__main__":
    main()
