# -*- coding: utf-8 -*-
"""q01_rayas_qqq.py — campeonato20, familia QQQ / 2.0 (09-10-2026). Rayas POR MINUTO, sin mirar adelante, de las series de QQQ de la
4.1 y de la 2.0, en todas las sesiones con datos: noche (22:00Z vispera -> 13:30Z) y rueda (13:30Z -> 20:00Z). SOLO LECTURA de datos.

Fuentes (todas de solo lectura):
  * velas M1 MNQZ6: juez20/velas_hasta_ahora.construir() (velas_m1.csv + centinela 'hoy' de la 2.0 sin su ultima linea). Se guarda una
    FOTO de esas velas en datos/velas_snapshot.csv: las rayas y el juez usan exactamente las mismas velas.
  * fotos QQQ de CBOE con su C8: scratchpad/v41/fotos_qqq.pkl (v41_01_fotos.py: union de PythiaGex/cadenas, PythiaGex2/cadenas,
    PythiaGex/cboe-local y PythiaGex4/cboe, dedup por cadena_ts = la bajada mas temprana, sin la pausa de CME). Si la 4.1 anoto esa foto
    en familia/muestras-QQQ-*.jsonl se usan SU razon y SU 'generado' (como v41_02). Las fotos de esta noche posteriores a la ultima del
    pkl se agregan leyendo las 4 carpetas (misma dedup, misma C8: vivo / muestra / mediana robusta de 24).
    DIFERENCIA DECLARADA con v41_02: aca se usan TODAS las fotos de la union tambien despues del 02-10 (v41_02 se quedaba solo con las
    que proceso la 4.1). Es la formula con todos los datos, no la disponibilidad de la 4.1 de esos dias.
  * niv20_m1.pkl (centinela rebobinado + hoy de la 2.0: lo que DIBUJO la 2.0 en cada vela) + extension con el centinela 'hoy' para los
    minutos posteriores (misma lectura que calibrar/c01_velas.leer_centinela).

Cuenta de la 4.1 (port de MinuteroQqq.Minuto + PerfilLado + SeleccionFam + ZeroEstandarFam + Nucleo20.Dominantes; ver v41_02):
  clave t (inicio del minuto) -> fut = cierre de la ultima m2 CERRADA antes de t (v41_02.Cierres; sesion desde las 18:00 NY, tope 4 h);
  foto = la ultima con generado <= t; vale si t < vigencia (C9) y la razon C8 no es NaN; S = fut / razon;
  filas del horizonte Hoy envejecidas (env = (t - generado)/86400 en [0, 2]); gamma Black-Scholes r 0,0375 sin dividendo;
  por K: GvC, GvP (puts -), GoC, GoP; Fut = K x razon; libro = |Fut - fut| <= 3 % (MinuteroQqq.FILTRO); R = min(2 % fut, 100).
  Series (desfase_min = 0 en el juez: la clave t ya es lo vigente en la vela t):
    MUROS_QQQ_vol / _oi   D1 = argmax GC > 0 en R, D2 = argmin GP < 0 en R (primera ocurrencia en orden de strike)
    MAJORS_QQQ_vol / _oi  D1 = argmax neto > 0 en R, D2 = argmin neto < 0 en R
    ZEST_QQQ_vol          zero estandar C5 por volumen sobre TODAS las filas del horizonte (sin el 3 %): grilla c0 = round(S) +- 7,5 USD
                          paso 0,05, cruce de signo estricto mas cercano a S, interpolado; x razon; se dibuja si |z - fut| <= 300.
                          Cache de la vista previa: clave (foto, floor((t - generado)/900 s), round(S)) dentro de la tanda de 240 min
                          desde las 22:00Z (CacheZero).
    DOMS_QQQ_vol          la SELECCION de la 2.0 sobre el libro de la 4.1: las 2 de mayor |Gv| con |Fut - fut| <= R y |Gv| > 0, orden
                          estable por strike; si no hay ninguna, lo mismo con Go (Nucleo20.Dominantes). D1 = la mayor.
    DOMS_QQQ_esc          igual que DOMS_QQQ_vol con razon = C8 - 0,0077 mientras la cadena esta CONGELADA y quieta (foto con
                          ultimo_trade NY >= 15:59 Y sin cambio de spot en los 600 s previos = la C8 ya no se actualiza). Escalon de
                          sesion de calib_conversion.md punto 4 (SIN VALIDAR con ninguna vara; estimado sobre 16 noches del 11-09 al
                          08-10, que INCLUYEN las de PRUEBA: contaminacion declarada). Fuera de eso es identica a DOMS_QQQ_vol.
  R20_QQQ (la 2.0 tal cual) = niv20 dom0/dom1 (lo dibujado por la 2.0 en la vela t con el cierre de t): desfase_min = 1 en el juez.

Salida: datos/rayas_qqq20.pkl = {'series': {nombre: {Timestamp: {'D1': (precio, etiqueta), ...}}}, 'desfase': {nombre: 0|1},
  'meta': DataFrame por minuto, 'sesiones': {...}}, datos/velas_snapshot.csv y datos/q01_salida.txt (paridades).
Uso: python -I q01_rayas_qqq.py"""
import ctypes
try:
    ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)   # prioridad baja: no trabar ATAS
except Exception:
    pass
import glob
import gzip
import json
import math
import os
import re
import sys
import time
from zoneinfo import ZoneInfo

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
J20DIR = os.path.normpath(os.path.join(AQUI, "..", "juez20"))
sys.path.insert(0, J20DIR)
from velas_hasta_ahora import construir  # noqa: E402

AP = os.path.join(os.environ["APPDATA"], "ATAS")
SCRBASE = os.path.join(os.environ["LOCALAPPDATA"], "Temp", "claude", "C--Users-wmx-7-OneDrive-Escritorio-ATAS-nada",
                       "f5879819-e2a4-405b-87b0-959ace090d7b", "scratchpad")
SCR = os.path.join(SCRBASE, "v41")
CAL = os.path.join(SCRBASE, "calibrar")
V41 = os.path.normpath(os.path.join(AQUI, "..", "..", "criterio_operador", "v41", "datos"))
HIB = os.path.normpath(os.path.join(AQUI, "..", "..", "criterio_operador", "hibrido"))
NY = ZoneInfo("America/New_York")
TASA = 0.0375
PISO_DIAS = 1.0 / 1440.0
ESCALON = 0.0077
SERIES41 = ("MUROS_QQQ_vol", "MUROS_QQQ_oi", "MAJORS_QQQ_vol", "MAJORS_QQQ_oi", "ZEST_QQQ_vol", "DOMS_QQQ_vol", "DOMS_QQQ_esc")
CARPETAS = [os.path.join(AP, "PythiaGex", "cadenas"), os.path.join(AP, "PythiaGex2", "cadenas"), os.path.join(AP, "PythiaGex", "cboe-local"),
            os.path.join(AP, "PythiaGex4", "cboe")]
RX_TS = re.compile(r'"cadena_ts"\s*:\s*"([^"]+)"')
RX_GEN = re.compile(r'"generado"\s*:\s*"([^"]+)"')
LOG = []


def out(*a):
    s = " ".join(str(x) for x in a)
    print(s, flush=True)
    LOG.append(s)


# ------------------------------------------------------------------------------------------------ tiempo
def utc_naive(s):
    t = pd.Timestamp(str(s).replace("\\u002B", "+").replace("\\u002b", "+"))
    if t.tzinfo is not None:
        t = t.tz_convert("UTC").tz_localize(None)
    return t.floor("s")


def ny_a_utc(s):
    return pd.Timestamp(s).tz_localize(NY).tz_convert("UTC").tz_localize(None)


def a_ny(t):
    return pd.Timestamp(t).tz_localize("UTC").tz_convert(NY).tz_localize(None)


def vigencia(gen, congelada):
    """C9 (v41_01.vigencia): 25 min desde generado; congelada: hasta las 09:30 NY siguientes."""
    h1 = gen + pd.Timedelta(seconds=1500)
    if not congelada:
        return h1
    ny = a_ny(gen)
    prox = ny_a_utc(ny.normalize() + pd.Timedelta(hours=9, minutes=30))
    if prox <= gen:
        prox = ny_a_utc(ny.normalize() + pd.Timedelta(days=1, hours=9, minutes=30))
    return max(prox, h1)


def ini_sesion_ms(t):
    ny = pd.Timestamp(t).tz_localize("UTC").tz_convert(NY)
    d = ny.normalize()
    ini = d + pd.Timedelta(hours=18) if ny.hour >= 18 else d - pd.Timedelta(days=1) + pd.Timedelta(hours=18)
    return int(ini.tz_convert("UTC").tz_localize(None).value // 1_000_000)


class Cierres:
    """v41_02.Cierres: CierreConocido sobre m2 armadas con las M1 (c = ultima M1 de la m2)."""

    def __init__(self, V):
        tm = (V["t"].to_numpy().astype("datetime64[s]").astype(np.int64) * 1000)
        b = (tm // 120_000) * 120_000
        df = pd.DataFrame({"b": b, "c": V["c"].to_numpy(float), "tm": tm}).sort_values("tm")
        g = df.groupby("b")["c"].last()
        self.b = g.index.to_numpy(np.int64); self.c = g.to_numpy(float)
        self.ultimo = int(tm.max()) + 60_000

    def __call__(self, t):
        ts = int(pd.Timestamp(t).value // 1_000_000)
        ini = ini_sesion_ms(t)
        b0 = ((ts - 120_000) // 120_000) * 120_000
        j = int(np.searchsorted(self.b, b0, side="right")) - 1
        while j >= 0:
            b = int(self.b[j])
            if b < ini:
                return float("nan")
            if ts - (b + 120_000) > 4 * 3600_000:
                return float("nan")
            if b + 120_000 <= self.ultimo:
                return float(self.c[j])
            j -= 1
        return float("nan")


# ------------------------------------------------------------------------------------------------ fotos
def robusta(x, piso):
    x = np.asarray(x, float)
    if len(x) == 0:
        return float("nan"), 0
    med = float(np.median(x)); mad = float(np.median(np.abs(x - med)))
    b = x[np.abs(x - med) <= max(3.0 * mad, piso)]
    return (float(np.median(b)), len(b)) if len(b) else (med, len(x))


def fotos_nuevas(desde_gen, ts_vistos):
    """Fotos QQQ con generado > desde_gen de las 4 carpetas (solo archivos de los 2 ultimos dias), dedup por cadena_ts."""
    hoy = pd.Timestamp(desde_gen).normalize()
    vistos = {}
    for c in CARPETAS:
        ps = glob.glob(os.path.join(c, "cadena-QQQ-*.jsonl.gz")) + glob.glob(os.path.join(c, "local-QQQ-*.jsonl"))
        for p in ps:
            m = re.search(r"QQQ-(\d{4}-\d{2}-\d{2})", os.path.basename(p))
            if not m or pd.Timestamp(m.group(1)) < hoy - pd.Timedelta(days=1):
                continue
            op = gzip.open if p.endswith(".gz") else open
            try:
                with op(p, "rt", encoding="utf-8", errors="replace") as fh:
                    for ln in fh:
                        cab = ln[:600]
                        a = RX_TS.search(cab); g = RX_GEN.search(cab)
                        if not a or not g:
                            continue
                        ts = a.group(1)
                        try:
                            gen = utc_naive(g.group(1))
                        except Exception:
                            continue
                        if gen <= desde_gen or pd.Timestamp(ts).floor("s") in ts_vistos:
                            continue
                        if ts in vistos and vistos[ts]["gen"] <= gen:
                            continue
                        try:
                            j = json.loads(ln)
                        except Exception:
                            continue
                        cd = j.get("cadena") or {}
                        filas = [x[:8] for x in cd.get("filas", []) if len(x) >= 8]
                        if not filas:
                            continue
                        ut = cd.get("ultimo_trade")
                        vistos[ts] = dict(gen=gen, ts=pd.Timestamp(ts).floor("s"), spot=float(cd.get("spot_idx") or j.get("spot") or 0),
                                          ut=ut, dato=ny_a_utc(ut) if ut else pd.NaT, src=os.path.basename(c),
                                          dias=np.array([float(v.get("dias") or 0) for v in cd.get("vencimientos", [])], float),
                                          fvenc=[v.get("f") for v in cd.get("vencimientos", [])],
                                          filas=np.array(filas, float))
            except (OSError, EOFError) as e:
                out("  aviso: %s ilegible (%s)" % (os.path.basename(p), e))
    res = []
    for f in sorted(vistos.values(), key=lambda f: (f["gen"], f["ts"])):
        ny = a_ny(f["gen"]); sec = ny.hour * 3600 + ny.minute * 60 + ny.second
        if 17 * 3600 < sec < 18 * 3600:
            continue
        res.append(f)
    return res


def cargar_fotos(V):
    t0 = time.time()
    F = pd.read_pickle(os.path.join(SCR, "fotos_qqq.pkl"))
    out("fotos_qqq.pkl: %d fotos %s -> %s (%.0f s)" % (len(F), F[0]["gen"], F[-1]["gen"], time.time() - t0))
    m41 = {}
    for p in glob.glob(os.path.join(AP, "PythiaGex4", "familia", "muestras-QQQ-*.jsonl")):
        for ln in open(p, encoding="utf-8"):
            if ln.strip():
                r = json.loads(ln); m41[pd.Timestamp(r["ts"])] = r
    n41 = ngen = 0
    for f in F:
        r = m41.get(f["ts"])
        f["nueva"] = False
        if r is not None and r.get("razon") is not None:
            f["rz"] = float(r["razon"]); f["rz_origen"] = "4.1"; n41 += 1
        else:
            f["rz"] = f["razon"]; f["rz_origen"] = "C8 reconstruida"
        if r is not None:
            g = pd.Timestamp(r["generado"])
            g = g.tz_convert("UTC").tz_localize(None) if g.tzinfo else g
            if g != f["gen"]:
                ngen += 1
                f["gen"] = g; f["vig"] = vigencia(g, f["congelada"])
    out("razon de la 4.1 (muestras-QQQ) en %d fotos; generado de la 4.1 en %d; el resto C8 reconstruida (v41_01)" % (n41, ngen))
    # extension: fotos de las carpetas con generado > la ultima del pkl
    ult = max(f["gen"] for f in F)
    nuevas = fotos_nuevas(ult, {f["ts"] for f in F})
    if nuevas:
        roll = [f["muestra"] for f in F if f.get("muestra") == f.get("muestra")][-24:]
        tm = V["t"].to_numpy().astype("datetime64[s]").astype(np.int64)
        vo = V["o"].to_numpy(float); vc = V["c"].to_numpy(float)
        todas = sorted(F, key=lambda f: f["gen"])
        for f in nuevas:
            gen = f["gen"]
            vivo = any((gen - g["gen"]).total_seconds() <= 600 and abs(g["spot"] - f["spot"]) > 1e-9
                       for g in todas[-40:] if g["gen"] <= gen)
            s = int((f["ts"] - pd.Timedelta(seconds=900)).value // 1_000_000_000)
            i = int(np.searchsorted(tm, s, side="right")) - 1
            precio = float(vo[i] + (vc[i] - vo[i]) * (s - tm[i]) / 60.0) if i >= 0 and 0 <= s - tm[i] < 60 else float("nan")
            val = precio / f["spot"] if (vivo and precio == precio and f["spot"] > 0) else float("nan")
            if val == val:
                roll.append(val); roll = roll[-24:]
            r, n = robusta(roll, 0.0002 * (float(np.median(roll)) if roll else 0.0))
            dny = a_ny(f["dato"]) if f["dato"] is not pd.NaT and f["dato"] == f["dato"] else None
            f.update(precio=precio, origen="vela", vivo=vivo, muestra=val, razon=r if n >= 5 else float("nan"), razon_n=n,
                     congelada=bool(dny is not None and dny.hour * 60 + dny.minute >= 15 * 60 + 59), nueva=True)
            f["vig"] = vigencia(gen, f["congelada"])
            f["rz"] = f["razon"]; f["rz_origen"] = "C8 extension"
            todas.append(f)
        F = todas
        out("extension: %d fotos nuevas con generado > %s: %s" % (len(nuevas), ult, [(str(f["gen"]), str(f["ts"]), f["spot"], f["vivo"], round(f["rz"], 5)) for f in nuevas[:6]]))
    else:
        out("extension: ninguna foto nueva despues de %s" % ult)
    F.sort(key=lambda f: (f["gen"], f["ts"]))
    # estado C8 coherente: una foto que NO es de la 4.1 y no trae muestra nueva (spot quieto) no cambia la razon -> hereda la de la foto
    # anterior de ESTA secuencia (si no, quedaria la de la cadena de v41_01, que arrastra otras muestras: 0,34 pts esta noche)
    nher = 0
    for i in range(1, len(F)):
        f = F[i]
        if f["rz_origen"] != "4.1" and not (f.get("muestra") == f.get("muestra")) and F[i - 1]["rz"] == F[i - 1]["rz"]:
            if f["rz"] != F[i - 1]["rz"]:
                nher += 1
            f["rz"] = F[i - 1]["rz"]; f["rz_origen"] = "heredada (sin muestra nueva)"
    out("fotos sin muestra nueva que heredan la razon de la anterior (y cambiaron): %d" % nher)
    nnan = sum(int(np.isnan(f["filas"][:, 2:8]).any()) for f in F)
    out("fotos con algun NaN en OI/IV/vol: %d de %d (se propagan como en la 4.1: un NaN en la fila apaga esa fila / ese zero)" % (nnan, len(F)))
    return F


# ------------------------------------------------------------------------------------------------ cuentas
def gamma_bs(S, K, T, iv):
    """GammaFam.BS vectorizada (S puede ser vector de la misma forma o escalar)."""
    with np.errstate(all="ignore"):
        ok = (S > 0) & (K > 0) & (T > 0) & (iv > 0)
        ivs = np.where(ok, iv, 1.0); Ts = np.where(ok, T, 1.0); Ks = np.where(ok, K, 1.0); Ss = np.where(ok, S, 1.0)
        v = ivs * np.sqrt(Ts)
        d1 = (np.log(Ss / Ks) + (TASA + 0.5 * ivs * ivs) * Ts) / v
        g = np.exp(-0.5 * d1 * d1) / math.sqrt(2 * math.pi) / (Ss * v)
    return np.where(ok, g, 0.0)


def filas_hoy(f, t):
    """FilasHoy.Armar: (K, T, oic, oip, ivc, ivp, volc, volp) del horizonte Hoy envejecido; None si nada entra."""
    env = max(0.0, min(2.0, (t - f["gen"]).total_seconds() / 86400.0))
    de = f["dias"] - env
    val = de[de >= 0]
    mas = float(val.min()) if len(val) else 0.0
    tope = max(1.0, mas + 0.01)
    F = f["filas"]
    v = F[:, 1].astype(int)
    ok = (v >= 0) & (v < len(de))
    d = np.where(ok, de[np.clip(v, 0, max(0, len(de) - 1))], -1.0)
    ok &= (d >= 0) & (d <= tope)
    if not ok.any():
        return None
    F = F[ok]; d = d[ok]
    return dict(K=F[:, 0], T=np.maximum(d, PISO_DIAS) / 365.0, oic=F[:, 2], oip=F[:, 3], ivc=F[:, 4], ivp=F[:, 5], volc=F[:, 6], volp=F[:, 7],
                env=env)


def perfil(fl, S):
    """PerfilLado.Armar: por K ascendente GvC, GvP, GoC, GoP (puts con signo -). None si ninguna fila aporta."""
    K = fl["K"]
    gc = gamma_bs(S, K, fl["T"], fl["ivc"]); gp = gamma_bs(S, K, fl["T"], fl["ivp"])
    esc = 100.0 * S * S * 0.01
    with np.errstate(invalid="ignore"):
        avc = gc * fl["volc"] * esc; avp = -gp * fl["volp"] * esc; aoc = gc * fl["oic"] * esc; aop = -gp * fl["oip"] * esc
        aporta = (avc + avp != 0) | (aoc + aop != 0)
    if not aporta.any():
        return None
    ks, inv = np.unique(K[aporta], return_inverse=True)
    sm = lambda x: np.bincount(inv, x[aporta], len(ks))
    GvC, GvP, GoC, GoP = sm(avc), sm(avp), sm(aoc), sm(aop)
    return dict(K=ks, GvC=GvC, GvP=GvP, GoC=GoC, GoP=GoP, Gv=GvC + GvP, Go=GoC + GoP)


def arg(Fut, x, fut, r, signo):
    """SeleccionFam.Arg: primera ocurrencia estricta del max (signo +, x > 0) / min (signo -, x < 0) dentro de R."""
    with np.errstate(invalid="ignore"):
        m = (np.abs(Fut - fut) <= r) & ((x > 0) if signo > 0 else (x < 0))
    if not m.any():
        return -1
    idx = np.flatnonzero(m)
    j = int(np.argmax(x[idx])) if signo > 0 else int(np.argmin(x[idx]))     # primera ocurrencia (comparacion estricta)
    return int(idx[j])


def dominantes20(Fut, Gv, Go, fut):
    """Nucleo20.Dominantes: las 2 de mayor |Gv| (> 0) a <= min(2 % fut, 100), orden estable por strike; si no hay, con Go."""
    radio = min(fut * 2.0 / 100.0, 100.0)
    cerca = np.abs(Fut - fut) <= radio
    with np.errstate(invalid="ignore"):
        c = np.flatnonzero(cerca & (np.abs(Gv) > 0)); g = Gv; lib = "vol"
        if not len(c):
            c = np.flatnonzero(cerca & (np.abs(Go) > 0)); g = Go; lib = "OI"
    if not len(c):
        return [], lib
    orden = c[np.argsort(-np.abs(g[c]), kind="stable")][:2]
    return [int(j) for j in orden], lib


def zero_estandar(fl, S):
    """ZeroEstandarFam.Calcular(QQQ): grilla c0 = round(S) +- 7,5 paso 0,05; Cv(x) = sum(g(x) volc - g(x) volp) x^2; cruce mas cercano a S."""
    c0 = float(np.round(S / 1.0)) * 1.0
    n = int(round(7.5 / 0.05))
    xs = c0 + 0.05 * (np.arange(2 * n + 1) - n)
    X = xs[:, None]
    K = fl["K"][None, :]; T = fl["T"][None, :]
    gc = gamma_bs(X, K, T, fl["ivc"][None, :]); gp = gamma_bs(X, K, T, fl["ivp"][None, :])
    with np.errstate(invalid="ignore"):
        cv = (gc * fl["volc"][None, :] - gp * fl["volp"][None, :]).sum(axis=1) * xs * xs
    mejor = float("nan"); dmin = float("inf")
    sg = np.sign(cv)
    for i in range(len(cv) - 1):
        if np.isnan(cv[i]) or np.isnan(cv[i + 1]) or sg[i] * sg[i + 1] >= 0:
            continue
        z = xs[i] + (xs[i + 1] - xs[i]) * (-cv[i]) / (cv[i + 1] - cv[i])
        d = abs(z - S)
        if d < dmin:
            dmin = d; mejor = float(z)
    return mejor


def selecciones(f, t, fut, rz):
    """Todas las series de la 4.1 de un minuto con razon rz. -> dict serie -> {'D1': (precio, etq), ...}, perfil, fl."""
    S = fut / rz
    fl = filas_hoy(f, t)
    if fl is None:
        return None, None, None
    pf = perfil(fl, S)
    if pf is None:
        return None, None, fl
    Fut_all = pf["K"] * rz
    m3 = np.abs(Fut_all - fut) <= fut * 0.03
    K = pf["K"][m3]; Fut = Fut_all[m3]
    sel = {k: pf[k][m3] for k in ("GvC", "GvP", "GoC", "GoP", "Gv", "Go")}
    r = min(0.02 * fut, 100.0)
    res = {}
    for nom, (A, B) in (("MUROS_QQQ_vol", ("GvC", "GvP")), ("MAJORS_QQQ_vol", ("Gv", "Gv")), ("MUROS_QQQ_oi", ("GoC", "GoP")),
                        ("MAJORS_QQQ_oi", ("Go", "Go"))):
        s = {}
        i = arg(Fut, sel[A], fut, r, +1)
        if i >= 0:
            s["D1"] = (float(Fut[i]), "QQQ%g" % K[i])
        j = arg(Fut, sel[B], fut, r, -1)
        if j >= 0:
            s["D2"] = (float(Fut[j]), "QQQ%g" % K[j])
        res[nom] = s
    idx, lib = dominantes20(Fut, sel["Gv"], sel["Go"], fut)
    res["DOMS_QQQ_vol"] = {"D%d" % (q + 1): (float(Fut[j]), "QQQ%g" % K[j]) for q, j in enumerate(idx)}
    res["_lib"] = lib
    return res, pf, fl


# ------------------------------------------------------------------------------------------------ 2.0 (niv20 + centinela hoy)
def niv20_extendido():
    niv = pd.read_pickle(os.path.join(CAL, "niv20_m1.pkl"))
    ult = max(niv)
    n0 = len(niv)
    p = os.path.join(AP, "pythiagex2-centinela-hoy-MNQZ6-TimeFrame-M1.jsonl")
    filas = []
    with open(p, encoding="utf-8", errors="replace") as fh:
        for ln in fh:
            ln = ln.strip()
            if not ln.endswith("}"):
                continue
            try:
                r = json.loads(ln)
            except Exception:
                continue
            filas.append((pd.Timestamp(r["t"]).floor("1min"), r.get("niv", {})))
    filas = filas[:-1]          # la ultima puede ser la vela en formacion (misma regla que las velas)
    nuevos = 0; iguales = tot = 0
    for t, d in filas:
        if t in niv:
            a = niv[t] or {}
            if a.get("dom0") is not None:
                tot += 1
                iguales += (abs((a.get("dom0") or 0) - (d.get("dom0") or 0)) < 0.01 and abs((a.get("dom1") or 0) - (d.get("dom1") or 0)) < 0.01)
            continue
        if t > ult:
            niv[t] = d; nuevos += 1
    out("niv20_m1.pkl: %d minutos hasta %s; centinela hoy: +%d minutos nuevos (hasta %s); solape dom0/dom1 identicos %d de %d" % (
        n0, ult, nuevos, max(niv), iguales, tot))
    return niv


# ------------------------------------------------------------------------------------------------ principal
def main():
    t00 = time.time()
    V, info = construir()
    V = V.drop_duplicates("t").sort_values("t").reset_index(drop=True)
    os.makedirs(os.path.join(AQUI, "datos"), exist_ok=True)
    V.to_csv(os.path.join(AQUI, "datos", "velas_snapshot.csv"), index=False)
    out("velas: %d %s -> %s; centinela hoy %s -> %s; solape %d/%d identicas; nuevas %d" % (
        len(V), V["t"].min(), V["t"].max(), info["centinela_desde"], info["centinela_hasta"], info["solape_iguales"], info["solape_n"],
        info["nuevas"]))
    F = cargar_fotos(V)
    gen = np.array([f["gen"].value for f in F], np.int64)
    CC = Cierres(V)
    tmax = V["t"].max() + pd.Timedelta(minutes=1)
    ses = sorted({(t + pd.Timedelta(hours=2)).strftime("%Y-%m-%d") for t in V["t"]})
    series = {s: {} for s in SERIES41}
    meta = []
    cache_z = {}; tanda_z = None; zc = {"calc": 0, "hit": 0}
    for s in ses:
        N = pd.Timestamp(s)
        a = N - pd.Timedelta(hours=2); b = N + pd.Timedelta(hours=20)
        tl = min(b, tmax)
        if tl <= a:
            continue
        for t in pd.date_range(a, tl, freq="1min", inclusive="left"):
            fut = CC(t)
            k = int(np.searchsorted(gen, t.value, side="right")) - 1
            m = dict(t=t, sesion=s, fut=fut, foto=None, motivo="", rz=float("nan"), esc=False, lib="", vivo=None, congelada=None)
            res = {}
            if fut != fut:
                m["motivo"] = "sin fut"
            elif k < 0:
                m["motivo"] = "sin foto"
            else:
                f = F[k]
                m["foto"] = f["gen"]; m["rz_origen"] = f["rz_origen"]; m["vivo"] = bool(f.get("vivo")); m["congelada"] = bool(f["congelada"])
                if t >= f["vig"]:
                    m["motivo"] = "foto vencida (C9)"
                elif not (f["rz"] == f["rz"]):
                    m["motivo"] = "sin razon"
                else:
                    rz = f["rz"]; m["rz"] = rz
                    r1, pf, fl = selecciones(f, t, fut, rz)
                    if r1 is None:
                        m["motivo"] = "sin strikes"
                    else:
                        m["motivo"] = "ok"; m["lib"] = r1.pop("_lib")
                        res.update(r1)
                        # ZEST con la cache de la vista previa
                        tanda = (s, int((t - a).total_seconds() // 60) // 240)
                        if tanda != tanda_z:
                            cache_z = {}; tanda_z = tanda
                        S = fut / rz
                        clave = (k, int(math.floor((t - f["gen"]).total_seconds() / 900.0)), int(np.round(S / 1.0)))
                        if clave in cache_z:
                            zv = cache_z[clave]; zc["hit"] += 1
                        else:
                            zv = zero_estandar(fl, S); cache_z[clave] = zv; zc["calc"] += 1
                        if zv == zv and abs(zv * rz - fut) <= 300.0:
                            res["ZEST_QQQ_vol"] = {"Z": (float(zv * rz), "QQQz%.2f" % zv)}
                        # DOMS con el escalon de sesion
                        if f["congelada"] and not f.get("vivo"):
                            m["esc"] = True
                            r2, _, _ = selecciones(f, t, fut, rz - ESCALON)
                            res["DOMS_QQQ_esc"] = r2["DOMS_QQQ_vol"] if r2 else {}
                        else:
                            res["DOMS_QQQ_esc"] = res["DOMS_QQQ_vol"]
            for nom in SERIES41:
                series[nom][t] = res.get(nom, {})
            meta.append(m)
        out("sesion %s lista (%.0f s)" % (s, time.time() - t00))
    M = pd.DataFrame(meta)
    out("minutos %d en %d sesiones; zero estandar: %d calculos, %d de la cache" % (len(M), len(ses), zc["calc"], zc["hit"]))
    out("motivos:\n" + M["motivo"].value_counts().to_string())
    M["tramo"] = np.where(((M["t"].dt.hour + M["t"].dt.minute / 60.0) >= 13.5) & ((M["t"].dt.hour + M["t"].dt.minute / 60.0) < 20), "rueda", "noche")
    cob = M.assign(ok=M["motivo"] == "ok").groupby(["sesion", "tramo"])["ok"].agg(["sum", "count"]).unstack()
    out("minutos con libro QQQ por sesion y tramo (ok / total):\n" + cob.to_string())
    out("minutos con escalon (cadena congelada y quieta): %d; por sesion:\n%s" % (int(M["esc"].sum()), M.groupby("sesion")["esc"].sum().to_string()))

    # ---------------- la 2.0 tal cual
    niv = niv20_extendido()
    r20 = {}
    for t, d in niv.items():
        d = d or {}
        r20[t] = {k: (float(d[k]), "R20") for k in ("dom0", "dom1") if d.get(k) is not None}
    series["R20_QQQ"] = r20

    # ---------------- paridades
    paridades(series, M, V)
    pd.to_pickle({"series": series, "desfase": dict({s: 0 for s in SERIES41}, R20_QQQ=1), "meta": M,
                  "velas": {"desde": str(V["t"].min()), "hasta": str(V["t"].max()), "n": len(V)}},
                 os.path.join(AQUI, "datos", "rayas_qqq20.pkl"))
    out("escrito datos/rayas_qqq20.pkl (%.0f s)" % (time.time() - t00))
    open(os.path.join(AQUI, "datos", "q01_salida.txt"), "w", encoding="utf-8").write("\n".join(LOG) + "\n")


def _par(a, b, tol=0.05):
    """mismo conjunto de precios (ordenados) a <= tol."""
    x = sorted(v[0] for v in a.values()); y = sorted(v[0] for v in b.values())
    return len(x) == len(y) and all(abs(p - q) <= tol for p, q in zip(x, y))


def paridades(series, M, V):
    # (a) contra la reconstruccion de v41_02 (noches, MUROS/MAJORS)
    p = os.path.join(V41, "rayas_v41.pkl")
    if os.path.exists(p):
        R = pd.read_pickle(p)["series"]
        for nom in ("MUROS_QQQ_vol", "MAJORS_QQQ_vol", "MUROS_QQQ_oi", "MAJORS_QQQ_oi"):
            tot = ig = solo_a = solo_b = 0
            for t, d in R[nom].items():
                e = series[nom].get(t)
                if e is None:
                    continue
                if not d and not e:
                    continue
                tot += 1
                if d and e:
                    ig += _par(d, e)
                elif d:
                    solo_a += 1
                else:
                    solo_b += 1
            out("PARIDAD %s contra v41_02 (noches): %d minutos con raya en alguno; identicas %d (%.1f %%); solo v41 %d; solo aca %d" % (
                nom, tot, ig, 100.0 * ig / tot if tot else float("nan"), solo_a, solo_b))
    # (b) contra lo que GRABO la 4.1 esta noche (familia/niv-2026-10-09-MNQZ6.jsonl), clave k = minuto
    p = os.path.join(AP, "PythiaGex4", "familia", "niv-2026-10-09-MNQZ6.jsonl")
    if os.path.exists(p):
        reales = {}
        for ln in open(p, encoding="utf-8", errors="replace"):
            ln = ln.strip()
            if not ln.endswith("}"):
                continue
            try:
                r = json.loads(ln)
            except Exception:
                continue
            if "k" in r:
                reales[pd.Timestamp(r["k"] * 60, unit="s")] = r
        for nom in ("MUROS_QQQ_vol", "MAJORS_QQQ_vol", "MUROS_QQQ_oi", "MAJORS_QQQ_oi", "ZEST_QQQ_vol", "DOMS_QQQ_vol"):
            tot = ig = 0; difs = []; ejemplo = None
            for t, r in reales.items():
                e = series[nom].get(t)
                if e is None:
                    continue
                d = {x[1]: (float(x[0]), None) for x in (r.get("s") or {}).get(nom, [])}
                if not d and not e:
                    continue
                if nom == "DOMS_QQQ_vol" and "DOMS_QQQ_vol" not in (r.get("s") or {}) and t < pd.Timestamp("2026-10-09 05:37"):
                    continue
                tot += 1
                ok = _par(d, e, 0.02)
                ig += ok
                if d and e and len(d) == len(e):
                    difs += [abs(a - b) for a, b in zip(sorted(v[0] for v in d.values()), sorted(v[0] for v in e.values()))]
                if not ok and ejemplo is None:
                    ejemplo = (str(t), d, e)
            out("PARIDAD %s contra la 4.1 REAL (niv-2026-10-09, %d minutos comparables): identicas %d (%.1f %%); |dif| mediana %.3f p95 %.3f; "
                "primer distinto %s" % (nom, tot, ig, 100.0 * ig / tot if tot else float("nan"), np.median(difs) if difs else float("nan"),
                                        np.percentile(difs, 95) if difs else float("nan"), ejemplo))
        # la replica 2.0 de la 4.1 contra el centinela de la 2.0 (desde 05:37Z)
        tot = ig = 0
        for t, r in reales.items():
            x = (r.get("s") or {}).get("R20_QQQ_vol")
            e = series["R20_QQQ"].get(t)
            if not x or e is None:
                continue
            tot += 1
            ig += _par({q[1]: (float(q[0]), None) for q in x}, e, 0.05)
        out("INFO R20_QQQ_vol (replica de la 4.1) contra el centinela de la 2.0 en la misma clave: %d minutos, identicas %d" % (tot, ig))
    # (c) DOMS contra el hibrido h41 (otra convencion de tiempo: fut = cierre de la vela t, desfase 1) — informativo
    p = os.path.join(HIB, "rayas_hibrido.pkl")
    if os.path.exists(p):
        H = pd.read_pickle(p)
        for nom, hk in (("DOMS_QQQ_vol", "h41"), ("DOMS_QQQ_esc", "h41c")):
            tot = ig = 0
            for t, d in H[hk].items():
                e = series[nom].get(t + pd.Timedelta(minutes=1))     # h41[t] vale en la vela t+1; la mia en la clave t+1
                if e is None:
                    continue
                dd = {k: v for k, v in d.items() if k.startswith("dom")}
                if not dd and not e:
                    continue
                tot += 1
                ig += _par(dd, e, 0.5)
            out("INFO %s contra el hibrido %s (fut distinto: cierre M1 vs m2): %d minutos, mismo par a <= 0,5 pts %d (%.1f %%)" % (
                nom, hk, tot, ig, 100.0 * ig / tot if tot else float("nan")))


if __name__ == "__main__":
    main()
