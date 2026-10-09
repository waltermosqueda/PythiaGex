# -*- coding: utf-8 -*-
"""
probador1/candidatas.py — GRUPO 1 del PREREGISTRO (calibracion_1009, sec. 7.0 y "Grupo 1"): C01..C06, niveles por minuto.

Todo sale de ../datos/cargar.py (solo lectura) y de la grilla del arnes (../arnes/evaluar.minutos_y_precio). No toca ATAS, ni
AppData, ni el codigo del proyecto. Ninguna candidata tiene parametros libres (PREREGISTRO sec. 6 (3)): lo que hay aca son las
definiciones de la sec. 7 escritas en Python, con las interpretaciones declaradas en INTERPRETACIONES (abajo).

Cuenta comun (sec. 7.0), por minuto t de la sesion:
  F(t)   = cierre de la vela de 1 min que abrio en t-1 (evaluar.minutos_y_precio; si falta, ultimo cierre de los 5 min previos).
  foto   = la vigente en t (D.foto_vigente = columna 'foto' de datos/conv_min, verificada igual en 300 de 300 minutos al azar).
  conversion = D.conversion(libro, dia, t, metodo) (columna del metodo en datos/conv_min); 'deriva' se arma aca (C04).
  Hoy    = filas con 0 <= dias_env <= max(1, min(dias_env >= 0 de TODOS los vencimientos de la foto) + 0,01), dias_env = dias - env,
           env = min(2, dias desde 'generado' hasta t) (receta_2_0/extraer_paridad_2_0.py:67-70, 107-120).
  gamma  = Black-Scholes, r = 0,0375, q = 0, sin descuento (receta_2_0:47-55); tau = max(dias_env, 1/1440)/365.
  GEX_w(K) = sum_venc (Gc*w_c - Gp*w_p) * 100 * S^2 * 0,01 (w = vol u oi).
  R = min(0,02*F, 100) sobre |Fut(K) - F|.
"""
import json
import math
import os
import sys

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(AQUI)
for _p in (os.path.join(RAIZ, "datos"), os.path.join(RAIZ, "arnes")):
    if _p not in sys.path:
        sys.path.insert(0, _p)

import cargar as D            # noqa: E402
import evaluar as E           # noqa: E402

R_TASA = 0.0375
MULT = 100.0                  # CBOE (NDX y QQQ)
PISO_DIAS = 1.0 / 1440.0
RQ_DERIVA = 0.0375 - 0.0060   # SUPUESTO del PREREGISTRO (tasa de la 2.0 menos dividendo de QQQ ~0,6 %), no medido
SEMILLA_C05 = 20261009
SALIDA = os.path.join(RAIZ, "probadores", "grupo1")

INTERPRETACIONES = {
    "mas_cerca": "min(dias - env >= 0) sobre TODOS los vencimientos de la foto (fotos().vencs), como receta_2_0:109 "
                 "(cadena['vencimientos']); las filas guardadas son solo las de la banda +-6 %.",
    "iv_faltante": "iv NaN o <= 0 -> gamma 0 (receta_2_0:49 devuelve 0 con iv <= 0).",
    "vol_oi_faltante": "vol/oi NaN -> 0.",
    "ahora": "el instante de la cuenta es t (apertura de la vela en la que vale la raya): env se mide de 'generado' a t.",
    "C04_rueda": "D = la rueda (fecha NY habil) mas reciente con t >= 16:00 NY de D y >= 20 muestras vivas con t_spot 09:35-15:59 NY "
                 "y 'generado' <= t (causal); si el contrato de esa rueda no es el del grafico en t (roll) no hay raya "
                 "(igual que la base NDX 'cuatro', cargar.py:322-324). 09:30-09:34 NY usa la deriva (la 'cuatro' arranca 09:35).",
    "C05_cantidad": "en cada cambio del CONJUNTO de strikes de C03 (o primer minuto con rayas) se sortean n = |C03(t)| strikes sin "
                    "reemplazo del pozo (|K*rho_cuatro - F| <= R y sum_Hoy(vol_c + vol_p) > 0, K ordenados); si el pozo tiene menos "
                    "de n se sortean los que haya (en minutos de caida a OI de C03 el pozo puede estar vacio: sin rayas). Los "
                    "strikes se mantienen (aunque salgan del radio) mientras C03 no cambie; solo hay rayas de C05 en minutos con rayas "
                    "de C03; nivel = K * rho_cuatro(t).",
    "C06_G": "G(t) con radio 0,02*F SIN el tope de 100 pts (asi lo escribe la sec. 7 para C06), GEX_oi + GEX_vol de Hoy.",
}


# ------------------------------------------------------------------------------------------------ gamma y perfil
def gamma_bs(S, K, T, iv, r=R_TASA):
    """GammaHoyNucleo.GammaBs vectorizada (receta_2_0:47-55): phi(d1) / (S * iv * sqrt(T)), sin dividendo ni descuento."""
    iv = np.where(np.isfinite(iv), iv, 0.0)
    ok = (S > 0) & (K > 0) & (T > 0) & (iv > 0)
    v = np.where(ok, iv * np.sqrt(np.where(T > 0, T, 1.0)), 1.0)
    d1 = (np.log(np.where(ok, S / np.where(K > 0, K, 1.0), 1.0)) + (r + 0.5 * iv * iv) * T) / v
    g = np.exp(-0.5 * d1 * d1) / math.sqrt(2.0 * math.pi) / (S * v)
    return np.where(ok, g, 0.0)


class Foto:
    """Filas de una foto (float64) + generado + dias de todos sus vencimientos."""
    __slots__ = ("K", "dias", "oic", "oip", "ivc", "ivp", "volc", "volp", "gen", "vencs")

    def __init__(self, libro, dia, foto, cab):
        x = D.filas(libro, dia, foto)
        f = lambda c: np.nan_to_num(x[c].to_numpy(np.float64), nan=0.0)
        self.K = x["strike"].to_numpy(np.float64)
        self.dias = x["dias"].to_numpy(np.float64)
        self.oic, self.oip, self.volc, self.volp = f("oi_call"), f("oi_put"), f("vol_call"), f("vol_put")
        self.ivc = x["iv_call"].to_numpy(np.float64)
        self.ivp = x["iv_put"].to_numpy(np.float64)
        self.gen = pd.Timestamp(cab["generado"])
        self.vencs = np.array([float(v) for v in json.loads(cab["vencs"]).values()], np.float64)


def perfil(fo, t, S):
    """Perfil por strike del horizonte Hoy en el instante t con spot S: (K, GC_vol, GP_vol, GC_oi, GP_oi, vol_tot).
    GP ya va con signo negativo. K ordenados (np.unique)."""
    env = (t - fo.gen).total_seconds() / 86400.0
    env = min(2.0, env) if env > 0 else 0.0
    vv = fo.vencs - env
    vv = vv[vv >= 0]
    mas = float(vv.min()) if len(vv) else 0.0
    de = fo.dias - env
    m = (de >= 0) & (de <= max(1.0, mas + 0.01))
    if not m.any():
        return None
    K = fo.K[m]
    T = np.maximum(de[m], PISO_DIAS) / 365.0
    gc = gamma_bs(S, K, T, fo.ivc[m])
    gp = gamma_bs(S, K, T, fo.ivp[m])
    fac = MULT * S * S * 0.01
    u, inv = np.unique(K, return_inverse=True)
    b = lambda w: np.bincount(inv, weights=w, minlength=len(u))
    return (u, b(gc * fo.volc[m] * fac), -b(gp * fo.volp[m] * fac), b(gc * fo.oic[m] * fac), -b(gp * fo.oip[m] * fac),
            b(fo.volc[m] + fo.volp[m]))


def dominantes_2_0(K, Fut, gv, go, F, n=2):
    """receta_2_0:150-158: las n de mayor |GEX_vol| con |Fut - F| <= R (orden estable por K en empate); si ninguna tiene
    |GEX_vol| > 0, las n de mayor |GEX_oi|. Devuelve (indices, 'vol'|'oi')."""
    rad = min(F * 2.0 / 100.0, 100.0)
    inr = np.abs(Fut - F) <= rad
    for w, nom in ((gv, "vol"), (go, "oi")):
        c = np.nonzero(inr & (np.abs(w) > 0))[0]
        if len(c):
            o = np.lexsort((K[c], -np.abs(w[c])))
            return c[o[:n]], nom
    return np.zeros(0, np.int64), None


# ------------------------------------------------------------------------------------------------ conversion 'deriva' (C04)
_MUESTRAS_QQQ = None


def _muestras_rueda_qqq():
    """Todas las muestras vivas de QQQ con t_spot 09:35-15:59 NY en dia habil: DataFrame (fecha_ny, t_spot, generado, muestra)."""
    global _MUESTRAS_QQQ
    if _MUESTRAS_QQQ is not None:
        return _MUESTRAS_QQQ
    partes = []
    for d in D.dias("QQQ"):
        f = D.fotos("QQQ", d)
        if f.empty:
            continue
        f = f[f["vivo"].astype(bool) & np.isfinite(f["muestra"].astype(float))][["t_spot", "generado", "muestra"]].copy()
        partes.append(f)
    M = pd.concat(partes, ignore_index=True)
    ny = pd.DatetimeIndex(M["t_spot"]).tz_localize("UTC").tz_convert("America/New_York")
    hm = ny.hour * 60 + ny.minute
    ok = (hm >= 575) & (hm <= 959) & (ny.weekday < 5)
    M = M[ok].copy()
    M["fecha_ny"] = ny[ok].strftime("%Y-%m-%d")
    M = M.sort_values("generado").reset_index(drop=True)
    _MUESTRAS_QQQ = M
    return M


def _t_cierre_ny(fecha):
    """16:00 NY de la fecha -> Timestamp UTC sin zona."""
    return D.ny_a_utc(fecha + "T16:00:00")


def serie_deriva(dia, tt, cuatro):
    """rho 'deriva' por minuto (tt: Timestamps UTC; cuatro: la 'cuatro' de esos minutos). Devuelve (rho, origen)."""
    M = _muestras_rueda_qqq()
    ny = pd.DatetimeIndex(tt).tz_localize("UTC").tz_convert("America/New_York")
    hm = np.asarray(ny.hour * 60 + ny.minute)
    rho = np.full(len(tt), np.nan)
    orig = np.empty(len(tt), object)
    fechas = sorted(M["fecha_ny"].unique())
    cierres = {f: _t_cierre_ny(f) for f in fechas}
    por_fecha = {}
    for fch, s in M.groupby("fecha_ny"):
        s = s.sort_values("generado")
        por_fecha[fch] = (s["generado"].to_numpy("datetime64[ns]"), s["muestra"].to_numpy(float))
    cache = {}
    for i, t in enumerate(tt):
        if 575 <= hm[i] <= 959:
            rho[i] = cuatro[i]
            orig[i] = "cuatro (09:35-15:59 NY)"
            continue
        t = pd.Timestamp(t)
        elegido = None
        for fch in reversed(fechas):
            if cierres[fch] > t:
                continue
            gen_, mu_ = por_fecha[fch]
            nn = int(np.searchsorted(gen_, np.datetime64(t), side="right"))   # solo las muestras ya disponibles en t
            if nn >= 20:
                elegido = (fch, nn)
                break
        if elegido is None:
            orig[i] = "sin rueda completa con >= 20 muestras"
            continue
        fch, nn = elegido
        if D.contrato_de_sesion(fch) != D.contrato_de(t):
            orig[i] = "rueda %s de %s, grafico %s: sin raya (roll)" % (fch, D.contrato_de_sesion(fch), D.contrato_de(t))
            continue
        clave = (fch, nn)
        if clave not in cache:
            x = por_fecha[fch][1][:nn]
            cache[clave] = D.robusta(x, 0.0002 * float(np.median(x)))[0]
        rD = cache[clave]
        sub = range(nn)
        dd = (t - cierres[fch]).total_seconds() / 86400.0
        rho[i] = rD * math.exp(-RQ_DERIVA * dd / 365.0)
        orig[i] = "deriva rueda %s (%d muestras) %.6f, %.3f dias" % (fch, len(sub), rD, dd)
    return rho, orig


# ------------------------------------------------------------------------------------------------ una sesion
def sesion(dia, que=("C01", "C02", "C03", "C04", "C05", "C06", "C01_doslog")):
    """Niveles por minuto de las candidatas pedidas en la sesion `dia`. Devuelve {id: DataFrame} y diagnostico."""
    g = E.minutos_y_precio(dia)
    tt = g["t"].to_numpy("datetime64[ns]")
    F = g["F"].to_numpy(float)
    out = {k: [] for k in que}
    diag = {"dia": dia, "minutos": len(g), "minutos_con_F": int(np.isfinite(F).sum())}
    # ---------------- QQQ
    qqq_ids = [k for k in que if k in ("C01", "C03", "C04", "C05", "C06", "C01_doslog")]
    if qqq_ids and dia in D.dias("QQQ"):
        cv = pd.read_parquet(os.path.join(RAIZ, "datos", "conv_min", "QQQ-%s.parquet" % dia))
        assert len(cv) == len(g) and (cv["t"].to_numpy("datetime64[ns]") == tt).all()
        cab = D.fotos("QQQ", dia).set_index("foto")
        fot = cv["foto"].to_numpy()
        rho = {"dos": cv["dos"].to_numpy(float), "cuatro": cv["cuatro"].to_numpy(float), "dos_log": cv["dos_log"].to_numpy(float)}
        if "C04" in que:
            rho["deriva"], orig_der = serie_deriva(dia, [pd.Timestamp(x) for x in tt], rho["cuatro"])
            diag["deriva_origenes"] = pd.Series(orig_der).map(lambda s: s.split(" (")[0] if s else s).value_counts().head(8).to_dict()
        metodos = {"C01": "dos", "C03": "cuatro", "C04": "deriva", "C01_doslog": "dos_log"}
        fotos_cache = {}
        rng = np.random.default_rng([SEMILLA_C05, int(dia.replace("-", ""))])
        c05_set_prev = None
        c05_hold = []
        n_c05 = {"cambios": 0, "pozo_corto": 0}
        for i in range(len(g)):
            if not np.isfinite(F[i]) or fot[i] < 0:
                c05_set_prev = None if "C05" in que else c05_set_prev
                continue
            fo = fotos_cache.get(fot[i])
            if fo is None:
                fo = fotos_cache[fot[i]] = Foto("QQQ", dia, int(fot[i]), cab.loc[int(fot[i])])
                if len(fotos_cache) > 8:
                    fotos_cache.pop(next(iter(fotos_cache)))
            t = pd.Timestamp(tt[i])
            c03_ks = None
            for cid in ("C01", "C03", "C04", "C01_doslog", "C05", "C06"):
                if cid not in que:
                    continue
                if cid in ("C05", "C06"):
                    continue
                r = rho[metodos[cid]][i]
                if not (r == r) or r <= 0:
                    continue
                S = F[i] / r
                pf = perfil(fo, t, S)
                if pf is None:
                    continue
                K, gcv, gpv, gco, gpo, vtot = pf
                gv, go = gcv + gpv, gco + gpo
                Fut = K * r
                idx, lib = dominantes_2_0(K, Fut, gv, go, F[i])
                for j, q in enumerate(idx):
                    out[cid].append((tt[i], Fut[q], "D%d" % (j + 1), K[q], "QQQ", dia, r, F[i], S, (gv if lib == "vol" else go)[q], lib, int(fot[i])))
                if cid == "C03":
                    c03_ks = [K[q] for q in idx]
                    c03_rows = [(Fut[q], "D%d" % (j + 1), K[q], (gv if lib == "vol" else go)[q], lib) for j, q in enumerate(idx)]
                    c03_ctx = (r, S, K, Fut, gv, go, vtot)
            # C03 hace falta para C05 y C06
            if ("C05" in que or "C06" in que) and c03_ks is None:
                r = rho["cuatro"][i]
                if r == r and r > 0:
                    S = F[i] / r
                    pf = perfil(fo, t, S)
                    if pf is not None:
                        K, gcv, gpv, gco, gpo, vtot = pf
                        gv, go = gcv + gpv, gco + gpo
                        Fut = K * r
                        idx, lib = dominantes_2_0(K, Fut, gv, go, F[i])
                        c03_ks = [K[q] for q in idx]
                        c03_rows = [(Fut[q], "D%d" % (j + 1), K[q], (gv if lib == "vol" else go)[q], lib) for j, q in enumerate(idx)]
                        c03_ctx = (r, S, K, Fut, gv, go, vtot)
            if c03_ks is None:
                c03_ks = []
            # ---- C06: rayas de C03 solo si G(t) > 0 (radio 0,02*F, sin tope)
            if "C06" in que and c03_ks:
                r, S, K, Fut, gv, go, vtot = c03_ctx
                G = float((go + gv)[np.abs(Fut - F[i]) <= 0.02 * F[i]].sum())
                if G > 0:
                    for (fu, et, k, gx, lib) in c03_rows:
                        out["C06"].append((tt[i], fu, et, k, "QQQ", dia, r, F[i], S, G, lib, int(fot[i])))
            # ---- C05: strikes al azar con el ritmo de C03
            if "C05" in que:
                cur = frozenset(c03_ks)
                if not cur:
                    c05_set_prev = None
                    c05_hold = []
                else:
                    r, S, K, Fut, gv, go, vtot = c03_ctx
                    if cur != c05_set_prev:
                        R_ = min(0.02 * F[i], 100.0)
                        pozo = K[(np.abs(Fut - F[i]) <= R_) & (vtot > 0)]
                        nn = min(len(cur), len(pozo))
                        if nn < len(cur):
                            n_c05["pozo_corto"] += 1
                        c05_hold = list(rng.choice(pozo, size=nn, replace=False)) if nn else []
                        c05_set_prev = cur
                        n_c05["cambios"] += 1
                    for j, k in enumerate(c05_hold):
                        out["C05"].append((tt[i], float(k) * r, "A%d" % (j + 1), float(k), "QQQ", dia, r, F[i], S, float("nan"), "azar", int(fot[i])))
        diag["C05"] = n_c05
    # ---------------- NDX (C02)
    if "C02" in que and dia in D.dias("NDX"):
        cv = pd.read_parquet(os.path.join(RAIZ, "datos", "conv_min", "NDX-%s.parquet" % dia))
        assert len(cv) == len(g) and (cv["t"].to_numpy("datetime64[ns]") == tt).all()
        cab = D.fotos("NDX", dia).set_index("foto")
        fot = cv["foto"].to_numpy()
        base = cv["dos"].to_numpy(float)
        fotos_cache = {}
        for i in range(len(g)):
            if not np.isfinite(F[i]) or fot[i] < 0 or not np.isfinite(base[i]):
                continue
            fo = fotos_cache.get(fot[i])
            if fo is None:
                fo = fotos_cache[fot[i]] = Foto("NDX", dia, int(fot[i]), cab.loc[int(fot[i])])
                if len(fotos_cache) > 8:
                    fotos_cache.pop(next(iter(fotos_cache)))
            t = pd.Timestamp(tt[i])
            B = base[i]
            S = F[i] - B
            pf = perfil(fo, t, S)
            if pf is None:
                continue
            K, gcv, gpv, gco, gpo, vtot = pf
            gv, go = gcv + gpv, gco + gpo
            Fut = K + B
            idx, lib = dominantes_2_0(K, Fut, gv, go, F[i])
            for j, q in enumerate(idx):
                out["C02"].append((tt[i], Fut[q], "D%d" % (j + 1), K[q], "NDX", dia, B, F[i], S, (gv if lib == "vol" else go)[q], lib, int(fot[i])))
    cols = ["t", "nivel", "etiqueta", "K", "libro", "dia", "conv", "F", "S", "gex", "libro_dom", "foto"]
    res = {k: pd.DataFrame(v, columns=cols) for k, v in out.items()}
    for k, v in res.items():
        diag["filas_" + k] = len(v)
        diag["minutos_" + k] = int(v["t"].nunique()) if len(v) else 0
    return res, diag


def construir(dias, fase, que=("C01", "C02", "C03", "C04", "C05", "C06", "C01_doslog"), escribir=True):
    """Niveles de todas las sesiones de `dias`; escribe probadores/grupo1/<ID>/niveles_<FASE>.parquet. Devuelve {id: df}, diag."""
    acc = {k: [] for k in que}
    diags = []
    for d in dias:
        r, dg = sesion(d, que)
        diags.append(dg)
        for k in que:
            if len(r[k]):
                acc[k].append(r[k])
        print("  %s %s" % (d, {k: dg.get("filas_" + k) for k in que}), flush=True)
    res = {}
    for k in que:
        df = pd.concat(acc[k], ignore_index=True) if acc[k] else pd.DataFrame(columns=["t", "nivel", "etiqueta", "K", "libro", "dia"])
        res[k] = df
        if escribir:
            carpeta = os.path.join(SALIDA, NOMBRES.get(k, k))
            os.makedirs(carpeta, exist_ok=True)
            df.to_parquet(os.path.join(carpeta, "niveles_%s.parquet" % fase), index=False)
    return res, diags


NOMBRES = {"C01": "C01_DOS_QQQ", "C02": "C02_DOS_NDX", "C03": "C03_DOS_QQQ_C41", "C04": "C04_DOS_QQQ_DERIVA",
           "C05": "C05_STRIKE_AZAR_QQQ", "C06": "C06_REGIMEN_POS_QQQ", "C01_doslog": "C01s_DOS_QQQ_doslog"}
LIBRO = {"C01": "QQQ", "C02": "NDX", "C03": "QQQ", "C04": "QQQ", "C05": "QQQ", "C06": "QQQ", "C01_doslog": "QQQ"}
