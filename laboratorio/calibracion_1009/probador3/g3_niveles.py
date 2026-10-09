# -*- coding: utf-8 -*-
"""
g3_niveles.py — PROBADOR 3 de calibracion_1009: rayas por minuto de las candidatas del GRUPO 3 (PREREGISTRO.md sec. 7, lineas 234-251).

  C13_IMAN_QQQ_vol        QQQ, Hoy, vol, 'cuatro'. Las 2 de mayor GEX_vol > 0 en R; solo si N(t) = sum_{|K rho - F| <= 1 % F} GEX_vol > 0. I1/I2.
  C14_REPEL_QQQ_vol       QQQ, Hoy, vol, 'cuatro'. Las 2 de GEX_vol mas negativo en R, sin condicion. N1/N2.
  C15_FLUJO_NQ            NQ (Rithmic), Hoy, Black-76, M = 20. G_d(K) = -sum Gamma (vol_compra - vol_venta) 20 S^2 0,01.
                          CW+ = argmax G_d con Fut(K) > F y G_d > 0 en R; PW+ = argmax G_d con Fut(K) <= F y G_d > 0 en R.
  C16_CONFLUENCIA_QQQ_NDX a = rayas de C03 (seleccion de la 2.0 sobre QQQ con 'cuatro'); b = seleccion de C02 (NDX) con base 'cuatro'.
                          Pares |a - b| <= 5 -> raya en (a+b)/2; hasta 2, los mas cercanos primero, sin repetir a ni b. F1/F2.
  C17_BANDA_EM            solo dia: t0 = primer minuto >= 10:00 NY con foto QQQ vigente con 0DTE; EM+- = F(t0)(1 +- iv sqrt(tau_c)).
  C18_CW_PW_OI_QQQ        QQQ, todos los vencimientos 0 <= dias_env <= 31 de la banda guardada, OI; CW = argmax sum Gc OIc 100 S^2 0,01;
                          PW = argmax sum Gp OIp 100 S^2 0,01; sin netear, sin radio, cada minuto, 'cuatro'.

Cuenta comun (PREREGISTRO 7.0): grilla = cada minuto t de la sesion; F(t) = evaluar.minutos_y_precio (cierre de la vela que abrio en t-1);
foto vigente y conversion 'cuatro' = D.serie_conversion (verificado igual a D.conversion/D.foto_vigente en 60 minutos al azar);
env = min(2, dias desde 'generado' (NQ: 'ts') hasta t); Hoy = 0 <= dias_env <= max(1, min(dias_env >= 0) + 0,01);
T = max(dias_env, 1/1440)/365; gamma CBOE = Black-Scholes r = 0,0375, q = 0, sin descuento (receta_2_0/extraer_paridad_2_0.py:47-55);
NQ = Black-76 (tres/nucleo.py:48-56). GEX por strike: GC_w = sum Gc w M S^2 0,01; GP_w = -sum Gp w M S^2 0,01. R = min(0,02 F, 100).
Empates: el de menor K (orden estable sobre K ascendente), como receta_2_0:148-152.

Solo LEE el dataset (datos/cargar.py). Escribe en probadores/grupo3/<ID>/niveles_<FASE>.parquet y probador3/diag_niveles_<FASE>.json.
Uso: python -I g3_niveles.py ENTRENAMIENTO | PRUEBA
"""
import os
import sys
import json
import math
import time
import hashlib
from zoneinfo import ZoneInfo

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(AQUI)
for _p in (os.path.join(RAIZ, "datos"), os.path.join(RAIZ, "arnes")):
    if _p not in sys.path:
        sys.path.insert(0, _p)
import cargar as D          # noqa: E402
import evaluar as EV        # noqa: E402  (solo minutos_y_precio y la particion; no se evalua nada aca)

NY = ZoneInfo("America/New_York")
TASA = 0.0375
PISO_DIAS = 1.0 / 1440.0
INV_SQRT_2PI = 1.0 / math.sqrt(2.0 * math.pi)
SALIDA = os.path.join(RAIZ, "probadores", "grupo3")
IDS = ("C13_IMAN_QQQ_vol", "C14_REPEL_QQQ_vol", "C15_FLUJO_NQ", "C16_CONFLUENCIA_QQQ_NDX", "C17_BANDA_EM", "C18_CW_PW_OI_QQQ")


# ------------------------------------------------------------------------------------------------ gamma
def gamma_bs(S, K, T, iv, r=TASA):
    """receta_2_0:47-55 (GammaHoyNucleo.GammaBs): BS sin dividendo y sin descuento en la gamma; 0 si algo no es positivo."""
    K = np.asarray(K, float); T = np.asarray(T, float); iv = np.asarray(iv, float)
    ok = (S > 0) & (K > 0) & (T > 0) & (iv > 0)
    with np.errstate(divide="ignore", invalid="ignore"):
        v = iv * np.sqrt(T)
        d1 = (np.log(S / K) + (r + 0.5 * iv * iv) * T) / v
        g = np.exp(-0.5 * d1 * d1) * INV_SQRT_2PI / (S * v)
    return np.where(ok, g, 0.0)


def gamma76(F, K, T, iv):
    """tres/nucleo.py:48-56: Black-76 (opciones sobre el futuro)."""
    K = np.asarray(K, float); T = np.asarray(T, float); iv = np.asarray(iv, float)
    ok = (F > 0) & (K > 0) & (T > 0) & (iv > 0)
    with np.errstate(divide="ignore", invalid="ignore"):
        v = iv * np.sqrt(T)
        d1 = (np.log(F / K) + 0.5 * iv * iv * T) / v
        g = np.exp(-0.5 * d1 * d1) * INV_SQRT_2PI / (F * v)
    return np.where(ok, g, 0.0)


def envejecer(gen, t):
    """GammaHoyNucleo.Envejecer: dias entre la foto y t, entre 0 y 2."""
    s = (t - gen).total_seconds()
    return max(0.0, min(2.0, s / 86400.0))


def mascara_hoy(dias_env):
    """PasaHorizonte(Hoy): 0 <= dias_env <= max(1, masCerca + 0,01), masCerca = min(dias_env >= 0) (0 si no hay)."""
    val = dias_env[dias_env >= 0]
    mas = float(val.min()) if len(val) else 0.0
    return (dias_env >= 0) & (dias_env <= max(1.0, mas + 0.01))


def agrupar(K, *cols):
    """suma por strike (K ordenado ascendente)."""
    ks, inv = np.unique(K, return_inverse=True)
    return (ks,) + tuple(np.bincount(inv, c, len(ks)) for c in cols)


def top_por(valor, orden_desc, n, K):
    """indices de los n primeros por 'orden_desc' (mayor primero), empate = menor K (estable sobre K ascendente)."""
    idx = np.argsort(K, kind="stable")
    idx = idx[np.argsort(-orden_desc[idx], kind="stable")]
    return idx[:n]


# ------------------------------------------------------------------------------------------------ libros CBOE
class LibroCboe:
    """filas de cada foto de un libro CBOE en una sesion, por foto (arrays), + la serie de conversion 'cuatro' por minuto."""

    def __init__(self, libro, dia):
        self.libro, self.dia = libro, dia
        self.fotos = D.fotos(libro, dia)
        self.ok = len(self.fotos) > 0
        self._cache = {}
        if self.ok:
            self.fx = self.fotos.set_index("foto")
            sc = D.serie_conversion(libro, dia, "cuatro")
            self.conv = pd.Series(sc["valor"].to_numpy(float), index=pd.DatetimeIndex(sc["t"]))
            self.fotomin = pd.Series(sc["foto"].to_numpy(float), index=pd.DatetimeIndex(sc["t"]))

    def filas(self, foto):
        foto = int(foto)
        if foto not in self._cache:
            y = D.filas(self.libro, self.dia, foto)
            self._cache[foto] = dict(K=y["strike"].to_numpy(float), venc=y["venc"].to_numpy(np.int64), dias=y["dias"].to_numpy(float),
                                     oic=y["oi_call"].to_numpy(float), oip=y["oi_put"].to_numpy(float),
                                     ivc=y["iv_call"].to_numpy(float), ivp=y["iv_put"].to_numpy(float),
                                     volc=y["vol_call"].to_numpy(float), volp=y["vol_put"].to_numpy(float),
                                     gen=pd.Timestamp(self.fx.loc[foto, "generado"]))
            if len(self._cache) > 64:
                self._cache.pop(next(iter(self._cache)))
        return self._cache[foto]

    def en(self, t):
        """(foto, conversion) vigentes en el minuto t (NaN/None si no hay)."""
        if not self.ok or t not in self.conv.index:
            return None, float("nan")
        fo = self.fotomin.at[t]
        v = self.conv.at[t]
        if not (fo == fo) or not (v == v):
            return None, float("nan")
        return int(fo), float(v)


def perfil_cboe(fl, t, S, al_fut, horizonte="Hoy", max_dias=31.0):
    """GEX por strike de una foto CBOE en el minuto t. Devuelve dict K, Fut, GCv, GPv, GCo, GPo, gv, go (sumados sobre los
    vencimientos que pasan el horizonte) o None."""
    env = envejecer(fl["gen"], t)
    de = fl["dias"] - env
    if horizonte == "Hoy":
        m = mascara_hoy(de)
    else:                                       # C18: todos los vencimientos con 0 <= dias_env <= 31
        m = (de >= 0) & (de <= max_dias)
    if not m.any() or S <= 0:
        return None
    K = fl["K"][m]; T = np.maximum(de[m], PISO_DIAS) / 365.0
    gc = gamma_bs(S, K, T, fl["ivc"][m]); gp = gamma_bs(S, K, T, fl["ivp"][m])
    esc = 100.0 * S * S * 0.01
    GCv = gc * fl["volc"][m] * esc; GPv = -gp * fl["volp"][m] * esc
    GCo = gc * fl["oic"][m] * esc; GPo = -gp * fl["oip"][m] * esc
    ks, GCv, GPv, GCo, GPo = agrupar(K, GCv, GPv, GCo, GPo)
    return dict(K=ks, Fut=al_fut(ks), GCv=GCv, GPv=GPv, GCo=GCo, GPo=GPo, gv=GCv + GPv, go=GCo + GPo)


def seleccion_dos(p, F):
    """C01/C02/C03 (receta_2_0:150-158): las 2 de mayor |GEX_vol| con |Fut - F| <= R (sin lado); si ninguna tiene |GEX_vol| > 0,
    las 2 de mayor |GEX_oi|. Devuelve ([(Fut, K)], 'vol'|'OI'|None)."""
    R = min(0.02 * F, 100.0)
    enR = np.abs(p["Fut"] - F) <= R
    for clave, nom in (("gv", "vol"), ("go", "OI")):
        a = np.abs(p[clave])
        sel = enR & (a > 0)
        if sel.any():
            ii = np.nonzero(sel)[0]
            jj = ii[top_por(a[ii], a[ii], 2, p["K"][ii])]
            return [(float(p["Fut"][j]), float(p["K"][j])) for j in jj], nom
    return [], None


# ------------------------------------------------------------------------------------------------ por sesion
def _hora_utc(dia, hh, mm):
    return pd.Timestamp(pd.Timestamp(dia + " %02d:%02d" % (hh, mm)).tz_localize(NY).tz_convert("UTC").tz_localize(None))


def sesion_qqq_ndx(dia, diag):
    """C13, C14, C16, C17, C18 de una sesion. Devuelve {ID: [filas]}."""
    out = {k: [] for k in IDS if k != "C15_FLUJO_NQ"}
    g = EV.minutos_y_precio(dia)
    Q = LibroCboe("QQQ", dia)
    N = LibroCboe("NDX", dia)
    dq = diag.setdefault(dia, {})
    dq.update({"qqq_fotos": len(Q.fotos), "ndx_fotos": len(N.fotos), "min_qqq_valido": 0, "min_ndx_valido": 0, "c13_condicion_si": 0,
               "c13_condicion_no": 0, "c16_qqq_fallback_oi": 0, "c16_ndx_fallback_oi": 0, "c16_min_con_pares": 0, "c17": None})
    if not Q.ok:
        return out
    t10 = _hora_utc(dia, 10, 0); t1559 = _hora_utc(dia, 15, 59); t16 = _hora_utc(dia, 16, 0)
    yyyymmdd = int(dia.replace("-", ""))
    em = None                                     # (t0, F0, sigma, ...)
    for t, F in zip(g["t"], g["F"].to_numpy(float)):
        if not (F == F):
            continue
        fo, rho = Q.en(t)
        if fo is None:
            continue
        dq["min_qqq_valido"] += 1
        fl = Q.filas(fo)
        S = F / rho
        al = (lambda k, z=rho: k * z)
        p = perfil_cboe(fl, t, S, al, "Hoy")
        R = min(0.02 * F, 100.0)
        if p is not None:
            enR = np.abs(p["Fut"] - F) <= R
            # ---- C13 iman
            cerca = np.abs(p["Fut"] - F) <= 0.01 * F
            Nt = float(p["gv"][cerca].sum())
            if Nt > 0:
                dq["c13_condicion_si"] += 1
                ii = np.nonzero(enR & (p["gv"] > 0))[0]
                if len(ii):
                    jj = ii[top_por(p["gv"][ii], p["gv"][ii], 2, p["K"][ii])]
                    for k, j in enumerate(jj):
                        out["C13_IMAN_QQQ_vol"].append((t, float(p["Fut"][j]), "I%d" % (k + 1), float(p["K"][j]), "QQQ", dia))
            else:
                dq["c13_condicion_no"] += 1
            # ---- C14 repelente
            ii = np.nonzero(enR & (p["gv"] < 0))[0]
            if len(ii):
                jj = ii[top_por(-p["gv"][ii], -p["gv"][ii], 2, p["K"][ii])]
                for k, j in enumerate(jj):
                    out["C14_REPEL_QQQ_vol"].append((t, float(p["Fut"][j]), "N%d" % (k + 1), float(p["K"][j]), "QQQ", dia))
            # ---- C16 confluencia: a = C03 (QQQ 'cuatro'), b = C02 con base 'cuatro'
            a, qa = seleccion_dos(p, F)
            dq["c16_qqq_fallback_oi"] += qa == "OI"
            fn, base = N.en(t)
            if fn is not None and a:
                dq["min_ndx_valido"] += 1
                pn = perfil_cboe(N.filas(fn), t, F - base, (lambda k, b=base: k + b), "Hoy")
                if pn is not None:
                    b, qb = seleccion_dos(pn, F)
                    dq["c16_ndx_fallback_oi"] += qb == "OI"
                    pares = sorted(((abs(x[0] - y[0]), i, j) for i, x in enumerate(a) for j, y in enumerate(b) if abs(x[0] - y[0]) <= 5.0),
                                   key=lambda z: z[0])
                    ua, ub, k = set(), set(), 0
                    for dd, i, j in pares:
                        if i in ua or j in ub or k >= 2:
                            continue
                        ua.add(i); ub.add(j); k += 1
                        out["C16_CONFLUENCIA_QQQ_NDX"].append((t, 0.5 * (a[i][0] + b[j][0]), "F%d" % k, a[i][1], "QQQ", dia))
                    dq["c16_min_con_pares"] += k > 0
        # ---- C18 CW/PW por OI, todos los vencimientos <= 31 dias, sin radio
        p31 = perfil_cboe(fl, t, S, al, "31")
        if p31 is not None:
            cw = p31["GCo"]; pw = -p31["GPo"]          # Sigma Gp OIp 100 S^2 0,01 (positivo)
            if (cw > 0).any():
                j = top_por(cw, cw, 1, p31["K"])[0]
                out["C18_CW_PW_OI_QQQ"].append((t, float(p31["Fut"][j]), "CW", float(p31["K"][j]), "QQQ", dia))
            if (pw > 0).any():
                j = top_por(pw, pw, 1, p31["K"])[0]
                out["C18_CW_PW_OI_QQQ"].append((t, float(p31["Fut"][j]), "PW", float(p31["K"][j]), "QQQ", dia))
        # ---- C17 banda EM: t0 = primer minuto >= 10:00 NY con foto vigente con 0DTE (y conversion valida, 7.0)
        if em is None and t10 <= t <= t1559:
            env = envejecer(fl["gen"], t)
            m0 = (fl["venc"] == yyyymmdd) & ((fl["dias"] - env) >= 0)
            if m0.any():
                K0 = fl["K"][m0]; ic = fl["ivc"][m0]; ip = fl["ivp"][m0]
                orden = np.lexsort((K0, np.abs(K0 - S)))           # mas cercano a S; empate menor K
                j = orden[0]
                a_, b_ = ic[j], ip[j]
                vals = [x for x in (a_, b_) if x == x and x > 0]
                if vals:
                    iv = float(np.mean(vals))
                    tauc = (t16 - t).total_seconds() / 60.0 / 525600.0
                    sig = iv * math.sqrt(tauc)
                    em = dict(t0=t, F0=F, K_atm=float(K0[j]), iv=iv, iv_call=float(a_), iv_put=float(b_), tau_c_min=(t16 - t).total_seconds() / 60.0,
                              sigma=sig, em_mas=F * (1 + sig), em_menos=F * (1 - sig), S=S, rho=rho, foto=fo)
                else:
                    dq.setdefault("c17_minutos_sin_iv_atm", 0)
                    dq["c17_minutos_sin_iv_atm"] += 1
        if em is not None and em["t0"] <= t <= t1559:
            out["C17_BANDA_EM"].append((t, em["em_mas"], "EM+", float("nan"), "QQQ", dia))
            out["C17_BANDA_EM"].append((t, em["em_menos"], "EM-", float("nan"), "QQQ", dia))
    if em is not None:
        dq["c17"] = {k: (str(v) if isinstance(v, pd.Timestamp) else v) for k, v in em.items()}
    return out


def sesion_nq(dia, diag):
    """C15 de una sesion."""
    out = []
    g = EV.minutos_y_precio(dia)
    f = D.fotos("NQ", dia)
    dq = diag.setdefault(dia, {})
    dq.update({"nq_fotos": len(f), "min_nq_valido": 0, "c15_min_con_flujo": 0, "c15_filas_flujo_nan": 0,
               "c15_fuentes_en_minutos": {}})
    if f.empty:
        return out
    x = D.filas("NQ", dia)
    nan_flujo = int((x["vol_compra"].isna() | x["vol_venta"].isna()).sum())
    dq["c15_filas_flujo_nan"] = nan_flujo
    grupos = {int(k): v for k, v in x.groupby("foto")}
    fx = f.set_index("foto")
    sc = D.serie_conversion("NQ", dia, "cuatro")
    conv = pd.Series(sc["valor"].to_numpy(float), index=pd.DatetimeIndex(sc["t"]))
    fmin = pd.Series(sc["foto"].to_numpy(float), index=pd.DatetimeIndex(sc["t"]))
    cache = {}
    for t, F in zip(g["t"], g["F"].to_numpy(float)):
        if not (F == F) or t not in conv.index:
            continue
        fo = fmin.at[t]; corr = conv.at[t]
        if not (fo == fo) or not (corr == corr):
            continue
        fo = int(fo)
        dq["min_nq_valido"] += 1
        fu = str(fx.loc[fo, "fuente"])
        dq["c15_fuentes_en_minutos"][fu] = dq["c15_fuentes_en_minutos"].get(fu, 0) + 1
        if fo not in cache:
            y = grupos.get(fo)
            if y is None:
                cache[fo] = None
            else:
                cache[fo] = dict(K=y["strike"].to_numpy(float), dias=y["dias"].to_numpy(float), iv=y["iv"].to_numpy(float),
                                 flu=(y["vol_compra"].fillna(0.0) - y["vol_venta"].fillna(0.0)).to_numpy(float),
                                 ts=pd.Timestamp(fx.loc[fo, "ts"]))
            if len(cache) > 16:
                cache.pop(next(iter(cache)))
        fl = cache[fo]
        if fl is None:
            continue
        S = F - corr
        env = envejecer(fl["ts"], t)
        de = fl["dias"] - env
        m = mascara_hoy(de)
        if not m.any() or S <= 0:
            continue
        K = fl["K"][m]; T = np.maximum(de[m], PISO_DIAS) / 365.0
        gd = -gamma76(S, K, T, fl["iv"][m]) * fl["flu"][m] * 20.0 * S * S * 0.01
        ks, G = agrupar(K, gd)
        if not (G != 0).any():
            continue
        dq["c15_min_con_flujo"] += 1
        Fut = ks + corr
        R = min(0.02 * F, 100.0)
        enR = np.abs(Fut - F) <= R
        for lado, et in (((Fut > F), "CW+"), ((Fut <= F), "PW+")):
            ii = np.nonzero(enR & lado & (G > 0))[0]
            if len(ii):
                j = ii[top_por(G[ii], G[ii], 1, ks[ii])[0]]
                out.append((t, float(Fut[j]), et, float(ks[j]), "NQ", dia))
    return out


COLS = ["t", "nivel", "etiqueta", "K", "libro", "dia"]


def construir(fase):
    if fase == "ENTRENAMIENTO":
        dq = [d for d in D.dias("QQQ") if d <= EV.ENTRENAMIENTO_HASTA]
        dn = [d for d in D.dias("NQ") if d <= EV.ENTRENAMIENTO_HASTA]
    elif fase == "PRUEBA":
        dq = [d for d in D.dias("QQQ") if d in EV.PRUEBA]
        dn = [d for d in D.dias("NQ") if d in EV.PRUEBA]
    else:
        raise SystemExit("fase ENTRENAMIENTO | PRUEBA")
    t0 = time.time()
    diag = {}
    acc = {k: [] for k in IDS}
    for d in dq:
        r = sesion_qqq_ndx(d, diag)
        for k, v in r.items():
            acc[k].extend(v)
        print(d, "QQQ/NDX", {k[:3]: len(v) for k, v in r.items()}, "%.0f s" % (time.time() - t0), flush=True)
    for d in dn:
        r = sesion_nq(d, diag)
        acc["C15_FLUJO_NQ"].extend(r)
        print(d, "NQ", len(r), "%.0f s" % (time.time() - t0), flush=True)
    resumen = {"fase": fase, "dias_qqq": dq, "dias_nq": dn, "segundos": round(time.time() - t0, 1), "por_sesion": diag, "candidatas": {}}
    for k in IDS:
        df = pd.DataFrame(acc[k], columns=COLS)
        if k == "C17_BANDA_EM":
            # sin strikes: sin columnas K/libro, asi el arnes cuenta niveles distintos a 5 pts por sesion (con K = NaN contaba
            # una sola 'etiqueta' 'QQQ' como strike distinto; corregido el 09-10 05:0x UTC antes de abrir nada)
            df = df.drop(columns=["K", "libro"])
        os.makedirs(os.path.join(SALIDA, k), exist_ok=True)
        ruta = os.path.join(SALIDA, k, "niveles_%s.parquet" % fase)
        df.to_parquet(ruta, index=False)
        h = hashlib.sha256(open(ruta, "rb").read()).hexdigest()
        resumen["candidatas"][k] = {"filas": len(df), "minutos": int(df["t"].nunique()) if len(df) else 0,
                                    "sesiones": sorted(df["dia"].unique().tolist()) if len(df) else [],
                                    "strikes_distintos": int(df["K"].nunique()) if (len(df) and "K" in df) else 0, "ruta": ruta, "sha256": h}
    with open(os.path.join(AQUI, "diag_niveles_%s.json" % fase), "w", encoding="utf-8") as fh:
        json.dump(resumen, fh, ensure_ascii=False, indent=1, default=str)
    print(json.dumps({k: (v["filas"], v["minutos"], v["strikes_distintos"]) for k, v in resumen["candidatas"].items()}), flush=True)
    return resumen


if __name__ == "__main__":
    construir(sys.argv[1] if len(sys.argv) > 1 else "ENTRENAMIENTO")
