# -*- coding: utf-8 -*-
"""
candidatas.py — PROBADOR 4 de calibracion_1009: los CONTROLES SIN GAMMA del grupo 4 (PREREGISTRO.md sec. 7, lineas 253-264).

  C19_RED25 / C20_RED50 / C21_RED100  redondez (Osler 2000/2003): multiplo de G mas cercano ESTRICTAMENTE por encima de F (R+) y
                                      multiplo mas cercano <= F (R-).
  C22_GRILLA_QQQ_C41                  grilla de strikes de QQQ sin gamma (practicantes P1): S = F / rho_cuatro; K+ = menor strike QQQ
                                      presente en las filas de la foto vigente (cualquier vencimiento del horizonte Hoy) con K > S;
                                      K- = mayor con K <= S; rayas K x rho_cuatro (G+, G-).
  C23_E_RANGO60                       control de precio (extremos_rebote.py:445-458 llevado a 1 min): max(h) y min(l) de las velas de
                                      1 min con apertura en [t-60, t-1] (>= 5 velas) (D1, D2).
  C24_MECHA_PREVIA                    control de precio (academia E: Garzarelli et al. 2014; Osler 2000): zigzag de 20 pts CAUSAL desde
                                      el inicio de la sesion (la maquina de juez_operador._pivotes); un giro existe desde la vela en que
                                      el precio recorrio los 20 pts en contra (<= t-1); rayas = el giro de maximo confirmado mas cercano
                                      por encima de F y el de minimo mas cercano por debajo, entre los de los ultimos 240 min (D1, D2).

Convenciones (PREREGISTRO sec. 7.0): grilla = cada minuto t de la sesion [dia-1 22:00, dia 21:00) UTC; F(t) = cierre de la vela de
1 min que abrio en t-1 (evaluar.minutos_y_precio; si falta, el ultimo cierre de los 5 min previos; si no hay, no hay raya).
La fila t vale para la vela que ABRE en t y usa solo velas cerradas antes de t y fotos con 'generado' <= t.
Salida: DataFrame t, nivel, etiqueta, [K, libro], dia. No hay parametros libres: todo esta fijado en el pre-registro.

Solo LEE el dataset (../datos/cargar.py) y el arnes (../arnes/evaluar.py). No toca ATAS ni codigo del proyecto.
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

import cargar as D          # noqa: E402
import evaluar as EV        # noqa: E402

IDS = ("C19_RED25", "C20_RED50", "C21_RED100", "C22_GRILLA_QQQ_C41", "C23_E_RANGO60", "C24_MECHA_PREVIA")

# parametros FIJADOS por el pre-registro (no se eligen; se anotan para el sello)
PARAMETROS = {
    "C19_RED25": {"G": 25.0},
    "C20_RED50": {"G": 50.0},
    "C21_RED100": {"G": 100.0},
    "C22_GRILLA_QQQ_C41": {"libro": "QQQ", "conversion": "cuatro", "horizonte": "Hoy (receta_2_0:67-70,107-120)", "radio": None},
    "C23_E_RANGO60": {"ventana_min": 60, "min_velas": 5},
    "C24_MECHA_PREVIA": {"umbral_pts": 20.0, "memoria_min": 240, "hueco_max_min": 30, "reinicio": "por tramo continuo (como _pivotes)"},
}


def _grilla(dia):
    """minutos_y_precio del arnes: t, F, ventana."""
    return EV.minutos_y_precio(dia)


def _velas_ses(dia):
    v = D.velas(dia)
    if v is None or v.empty:
        return pd.DataFrame(columns=["t", "o", "h", "l", "c"])
    ini, fin = D.ventana(dia)
    v = v[(v["t"] >= ini) & (v["t"] < fin)][["t", "o", "h", "l", "c"]].dropna()
    return v.sort_values("t").drop_duplicates("t", keep="last").reset_index(drop=True)


# ------------------------------------------------------------------------------------------------ C19-C21 redondez
def redondos(dia, G):
    g = _grilla(dia)
    F = g["F"].to_numpy(float)
    ok = np.isfinite(F)
    t = g["t"].to_numpy()[ok]
    F = F[ok]
    abajo = G * np.floor(F / G)          # multiplo mas cercano <= F
    arriba = abajo + G                   # el mas cercano ESTRICTAMENTE por encima de F (si F es multiplo, F + G)
    a = pd.DataFrame({"t": t, "nivel": arriba, "etiqueta": "R+"})
    b = pd.DataFrame({"t": t, "nivel": abajo, "etiqueta": "R-"})
    out = pd.concat([a, b], ignore_index=True).sort_values(["t", "etiqueta"]).reset_index(drop=True)
    out["dia"] = dia
    return out


# ------------------------------------------------------------------------------------------------ C22 grilla de QQQ
def _vencs_hoy(vencs_json, generado, t):
    """Horizonte Hoy (GammaHoyNucleo / receta_2_0:67-70, 107-120): env = min(2, dias desde generado hasta t); dias_env = dias - env;
    entran los vencimientos con 0 <= dias_env <= max(1, min(dias_env >= 0) + 0,01)."""
    vd = {int(k): float(v) for k, v in json.loads(vencs_json).items()}
    seg = (t - generado).total_seconds()
    env = 0.0 if seg <= 0 else min(2.0, seg / 86400.0)
    de = {k: d - env for k, d in vd.items()}
    pos = [x for x in de.values() if x >= 0]
    mas_cerca = min(pos) if pos else 0.0
    lim = max(1.0, mas_cerca + 0.01)
    return frozenset(k for k, x in de.items() if 0 <= x <= lim)


def grilla_qqq(dia, diag=None):
    g = _grilla(dia)
    f = D.fotos("QQQ", dia)
    if f.empty:
        return pd.DataFrame(columns=["t", "nivel", "etiqueta", "K", "libro", "dia"])
    conv = D.serie_conversion("QQQ", dia, "cuatro")
    conv = conv.set_index("t")
    gen = f["generado"].to_numpy()
    filas_all = D.filas("QQQ", dia)            # todas las filas de la sesion (por filas_id)
    por_id = {k: x for k, x in filas_all.groupby("filas_id")}
    cache = {}
    out = []
    n_sin_foto = n_sin_conv = n_sin_strikes = n_ok = n_foto_distinta = n_fuera_radio = 0
    for t, F in zip(g["t"], g["F"].to_numpy(float)):
        if not (F == F):
            continue
        # foto vigente (D.foto_vigente): la ultima con generado <= t y t < vigencia (C9)
        i = int(np.searchsorted(gen, np.datetime64(t), side="right")) - 1
        if i < 0:
            n_sin_foto += 1
            continue
        r = f.iloc[i]
        if not (t < r["vigencia"]):
            n_sin_foto += 1
            continue
        # conversion 'cuatro' (la misma cuenta que D.conversion, ya por minuto en conv_min)
        c = conv["valor"].get(t, float("nan"))
        fc = conv["foto"].get(t, float("nan"))
        if not (c == c) or c <= 0:
            n_sin_conv += 1
            continue
        if fc == fc and int(fc) != int(r["foto"]):
            n_foto_distinta += 1           # no deberia pasar: se informa
        clave = (int(r["filas_id"]), _vencs_hoy(r["vencs"], r["generado"], t))
        if clave not in cache:
            x = por_id.get(clave[0])
            if x is None or not len(clave[1]):
                cache[clave] = np.zeros(0)
            else:
                cache[clave] = np.unique(x.loc[x["venc"].astype(int).isin(clave[1]), "strike"].to_numpy(float))
        ks = cache[clave]
        if not len(ks):
            n_sin_strikes += 1
            continue
        S = F / c
        j = int(np.searchsorted(ks, S, side="right"))     # ks[j-1] <= S < ks[j]
        R = min(0.02 * F, 100.0)
        if j < len(ks):
            kp = float(ks[j]); out.append((t, kp * c, "G+", kp))
            n_fuera_radio += abs(kp * c - F) > R
        if j > 0:
            km = float(ks[j - 1]); out.append((t, km * c, "G-", km))
            n_fuera_radio += abs(km * c - F) > R
        n_ok += 1
    if diag is not None:
        diag.update({"minutos_con_rayas": n_ok, "sin_foto_vigente": n_sin_foto, "sin_conversion": n_sin_conv,
                     "sin_strikes_hoy": n_sin_strikes, "foto_distinta_conv_min": n_foto_distinta,
                     "rayas_fuera_del_radio_7_0": int(n_fuera_radio)})
    df = pd.DataFrame(out, columns=["t", "nivel", "etiqueta", "K"])
    df["libro"] = "QQQ"
    df["dia"] = dia
    return df


# ------------------------------------------------------------------------------------------------ C23 rango de 60 min
def rango60(dia, ventana=60, min_velas=5):
    ini, fin = D.ventana(dia)
    grid = pd.date_range(ini, fin, freq="1min", inclusive="left")
    v = _velas_ses(dia).set_index("t")
    h = v["h"].reindex(grid)
    l = v["l"].reindex(grid)
    # velas con apertura en [t-60, t-1]: ventana de 60 minutos que TERMINA en t-1 (shift 1)
    hs = h.shift(1).rolling(ventana, min_periods=min_velas).max()
    ls = l.shift(1).rolling(ventana, min_periods=min_velas).min()
    ok = hs.notna() & ls.notna()
    t = grid[ok.to_numpy()]
    a = pd.DataFrame({"t": t, "nivel": hs[ok].to_numpy(), "etiqueta": "D1"})
    b = pd.DataFrame({"t": t, "nivel": ls[ok].to_numpy(), "etiqueta": "D2"})
    out = pd.concat([a, b], ignore_index=True).sort_values(["t", "etiqueta"]).reset_index(drop=True)
    out["dia"] = dia
    return out


# ------------------------------------------------------------------------------------------------ C24 mechas previas (zigzag causal)
def pivotes_causales(v, X=20.0, hueco_max=30):
    """La maquina de estados de juez_operador._pivotes (arnes/juez_operador_v1.py:618-673), corrida hacia adelante: devuelve cada
    giro con la vela en que QUEDA CONFIRMADO (la vela k en que el precio recorrio X en contra). Por tramo continuo (hueco > 30 min
    corta, como preparar_velas/_pivotes); el primer giro del tramo se descarta si su lado izquierdo es < X (igual que _pivotes, con
    datos hasta el propio giro). Lista de dicts: tipo, k (vela del giro), precio, k_conf (vela de confirmacion)."""
    tm = v["t"].to_numpy().astype("datetime64[m]").astype(np.int64)
    h = v["h"].to_numpy(float); l = v["l"].to_numpy(float)
    n = len(v)
    if n == 0:
        return []
    gap = np.ones(n, bool)
    gap[1:] = (tm[1:] - tm[:-1]) > hueco_max
    seg_ini = list(np.flatnonzero(gap)) + [n]
    out = []
    for a, b in zip(seg_ini[:-1], seg_ini[1:]):
        if b - a < 3:
            continue
        P = []
        tr = 0; hi, hi_k, lo, lo_k = h[a], a, l[a], a

        def emitir(tp, k, kc):
            precio = h[k] if tp == "max" else l[k]
            if not P:
                if tp == "max":
                    izq = precio - l[a:k + 1].min()
                else:
                    izq = h[a:k + 1].max() - precio
                P.append((tp, k))
                if izq < X:
                    return
            else:
                P.append((tp, k))
            out.append({"tipo": tp, "k": int(k), "precio": float(precio), "k_conf": int(kc)})

        for k in range(a + 1, b):
            if tr == 0:
                if h[k] > hi: hi, hi_k = h[k], k
                if l[k] < lo: lo, lo_k = l[k], k
                if hi - lo >= X:
                    if hi_k > lo_k:
                        emitir("min", lo_k, k); tr = 1
                    else:
                        emitir("max", hi_k, k); tr = -1
            elif tr == 1:
                if h[k] > hi:
                    hi, hi_k = h[k], k
                elif hi - l[k] >= X:
                    emitir("max", hi_k, k); tr = -1; lo, lo_k = l[k], k
            else:
                if l[k] < lo:
                    lo, lo_k = l[k], k
                elif h[k] - lo >= X:
                    emitir("min", lo_k, k); tr = 1; hi, hi_k = h[k], k
    return out


def mecha_previa(dia, X=20.0, memoria=240, diag=None):
    v = _velas_ses(dia)
    g = _grilla(dia)
    piv = pivotes_causales(v, X)
    tv = v["t"].to_numpy().astype("datetime64[m]").astype(np.int64)
    if piv:
        tipo = np.array([p["tipo"] == "max" for p in piv])
        tk = tv[[p["k"] for p in piv]]
        tc = tv[[p["k_conf"] for p in piv]]
        pr = np.array([p["precio"] for p in piv])
    out = []
    tg = g["t"].to_numpy().astype("datetime64[m]").astype(np.int64)
    for t, tmn, F in zip(g["t"], tg, g["F"].to_numpy(float)):
        if not (F == F) or not piv:
            continue
        vivo = (tc <= tmn - 1) & (tk >= tmn - memoria) & (tk <= tmn - 1)
        up = vivo & tipo & (pr > F)
        dn = vivo & ~tipo & (pr < F)
        if up.any():
            out.append((t, float(pr[up].min()), "D1"))
        if dn.any():
            out.append((t, float(pr[dn].max()), "D2"))
    if diag is not None:
        diag.update({"giros_confirmados": len(piv), "de_maximo": int(sum(p["tipo"] == "max" for p in piv)),
                     "demora_mediana_confirmacion_min": float(np.median([p["k_conf"] - p["k"] for p in piv])) if piv else float("nan")})
    df = pd.DataFrame(out, columns=["t", "nivel", "etiqueta"])
    df["dia"] = dia
    return df


# ------------------------------------------------------------------------------------------------ armado
def niveles(cid, dias, diag=None):
    partes = []
    for d in dias:
        dd = {} if diag is not None else None
        if cid == "C19_RED25":
            x = redondos(d, 25.0)
        elif cid == "C20_RED50":
            x = redondos(d, 50.0)
        elif cid == "C21_RED100":
            x = redondos(d, 100.0)
        elif cid == "C22_GRILLA_QQQ_C41":
            x = grilla_qqq(d, dd)
        elif cid == "C23_E_RANGO60":
            x = rango60(d)
        elif cid == "C24_MECHA_PREVIA":
            x = mecha_previa(d, diag=dd)
        else:
            raise KeyError(cid)
        if diag is not None:
            dd["filas"] = int(len(x))
            diag[d] = dd
        if len(x):
            partes.append(x)
    if not partes:
        return pd.DataFrame(columns=["t", "nivel", "etiqueta", "dia"])
    out = pd.concat(partes, ignore_index=True)
    out["t"] = pd.to_datetime(out["t"])
    return out


def dias_de(cid, dias):
    """Sesiones donde existe el libro de la candidata (PREREGISTRO sec. 6): C22 solo con fotos de QQQ; el resto, con velas."""
    con_velas = set(D.dias())
    if cid == "C22_GRILLA_QQQ_C41":
        q = set(D.dias("QQQ"))
        return [d for d in dias if d in q and d in con_velas]
    return [d for d in dias if d in con_velas]
