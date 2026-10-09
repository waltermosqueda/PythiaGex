# -*- coding: utf-8 -*-
"""nq_comun.py — FAMILIA NQ del campeonato20 (09-10-2026): reconstruccion minuto a minuto, SIN MIRAR ADELANTE, de las series del libro
de opciones de NQ por Rithmic tal como las calcula PythiaGex 4.1 (modulo Familia), mas DOMS_NQ (la seleccion de la 2.0 sobre ese libro).
SOLO LECTURA de datos. No toca ATAS ni el codigo del indicador. Modulo: la prioridad baja la pone el script que lo llama.

FUENTE DEL LIBRO: las lineas crudas viva/viva3 de %APPDATA%/ATAS/PythiaGex{,2,3,4}/viva (la misma union que construir.construir_viva del
  dataset unificado: misma ts y mismo contenido = una foto; misma ts y contenido distinto = gana la de mas filas; orden de lectura
  (vispera, dia) x (viva, viva2, viva3, viva4)). Se leen las crudas (float64) en vez de los parquet (float32) para que la cuenta sea la
  del indicador y para poder sumar la noche en curso (10-09) hasta la ultima linea escrita. Se compara el conteo contra el dataset.
  fuentes=('viva4',) reproduce lo que vio la 4.1 (su propio archivo) para la paridad de esta noche.
  UN GRABADOR POR MINUTO (minutos_sesion(prioridad=True), el default): en cada minuto se usa la ultima foto vigente del grabador de
  mayor prioridad que tenga una (viva4 > viva3 > viva2 > viva, la de construir.PREF_VIVA). Motivo (medido ANTES de juzgar nada, 10-08):
  los grabadores tienen libros de distinto ancho (viva/viva2 ~180 filas desde el 02-10, cupo de 200 de ATAS 8.0.15; viva4 ~310) y
  la union intercalada hacia saltar el zero estandar (ZEST union contra viva4 sola: |dif| mediana 1,6 pts, p90 46, p99 80); los muros,
  majors y DOMS coincidian 96-100 %. La deteccion del salto de OI (C2) recorre la union (el OI es el mismo en todos).
FOTO (FotoNq.Armar = preview_niveles.foto_viva3 = libros.fotos_viva): dias = sorted(set(round(dias,4))) de TODAS las filas crudas; se
  descarta la fila con iv <= 0 o (oi <= 0 y vol <= 0); una fila por (K, indice de dias) en orden de primera aparicion.
  vol = la columna 'vol_hoy' (o 'vol' en archivos viejos) = el indice 7 que lee la 4.1.
MINUTO (MinuteroNq.Minuto = backtest_familia.minutos_nq, con OpcionesNq por defecto: CorrPorTanda=true, ZeroCadaMinuto=false):
  t = inicio del minuto k (clave). fut = cierre de la ultima vela m2 CERRADA (fin <= t; m2 = minutos pares UTC armadas con las de 1 m;
  NaN si la ultima cerro hace > 4 h). foto = la ultima con ts <= t; nada si t - ts >= 300 s.
  Tanda = (k - k_ini_sesion) // 240 (22:00, 02:00, 06:00, 10:00, 14:00, 18:00 UTC): se vacian el historial del corrimiento y la cache
  del zero. Corrimiento: por foto nueva, muestra = cierre de la ultima m2 con fin <= ts + 120 s (si ts - fin <= 240 s) - futuro de la
  foto; med = mediana si hay >= 5; corr = med si |med| > 30, si no 0. S = fut - corr.
  Filas Hoy (envejecidas a t; tope max(1, mas cercano + 0,01)); perfil por lado Black-76 SIN descuento; Fut = K + corr y solo strikes
  con |Fut - fut| <= 3 % de fut. Zero estandar C5 (paso 2,5, +-300, centro 50) con cache (foto, round(S/50)) dentro de la tanda.
  C2 (OiNq) EN VIVO (causal, como la 4.1 con su reloj): de 22:00 UTC hasta el salto de OI de Rithmic visto hasta t (o 03:30 UTC si no
  aparece), los niveles por OI NO existen; lunes/posferiado o primera foto de la noche >= 01:55 UTC: sin supresion.
SERIES (SeleccionFam, R = min(2 % fut, 100)): MUROS D1 = argmax GEX calls > 0, D2 = argmin GEX puts < 0; MAJORS D1/D2 = argmax/argmin
  neto; ZTP = cruces limpios (sin islas, C3) mas cercanos arriba/abajo a <= 100; ZEST = zero estandar a <= 300. OI solo si C2 ok.
  DOMS_NQ_vol (Nucleo20.Dominantes sobre el LibroMinuto NQ): las 2 barras de mayor |GEX neto por volumen| con |Fut - fut| <= R
  (orden estable: a igual |G|, el strike menor), si no hay ninguna, lo mismo con el OI (se cuenta cuantas veces pasa).
"""
import glob
import hashlib
import json
import os
import re
from datetime import datetime

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
DATOS = os.path.join(AQUI, "datos")
LAB = os.path.normpath(os.path.join(AQUI, "..", ".."))                      # calibracion_1009
DS = os.path.join(LAB, "datos")
J20_DIR = os.path.join(LAB, "campeonato20", "juez20")
APP = os.path.join(os.environ.get("APPDATA", ""), "ATAS")
P1, P2, P3, P4 = (os.path.join(APP, x) for x in ("PythiaGex", "PythiaGex2", "PythiaGex3", "PythiaGex4"))
VIVAS = [("viva", os.path.join(P1, "viva"), "viva-NQ-%s.jsonl"), ("viva2", os.path.join(P2, "viva"), "viva-NQ-%s.jsonl"),
         ("viva3", os.path.join(P3, "viva"), "viva3-NQ-%s.jsonl"), ("viva4", os.path.join(P4, "viva"), "viva3-NQ-%s.jsonl")]
PREF_VIVA = {"viva4": 0, "viva3": 1, "viva2": 2, "viva": 3}
CENTINELA = os.path.join(APP, "pythiagex2-centinela-hoy-MNQZ6-TimeFrame-M1.jsonl")

VIDA_VIVA = 300
TANDA = 240
RADIO = 100.0
ISLA = 0.10
FILTRO = 0.03
ZEST_MAX = 300.0
PISO_DIAS = 1.0 / 1440.0
POSFERIADOS = ("2026-09-08",)          # TiempoFam.EsPosferiado: martes despues del Labor Day (07-09); el unico en el rango
INV_SQRT_2PI = 1.0 / np.sqrt(2.0 * np.pi)

SERIES = ["MUROS_NQ_vol", "MUROS_NQ_vol.C", "MUROS_NQ_vol.P", "MUROS_NQ_oi", "MUROS_NQ_oi.C", "MUROS_NQ_oi.P",
          "MAJORS_NQ_vol", "MAJORS_NQ_oi", "ZTP_NQ_vol", "ZEST_NQ_vol", "ZEST_NQ_oi", "DOMS_NQ_vol"]
FUENTE_SERIE = {s: ("oi" if "_oi" in s else "vol") for s in SERIES}


def _dia_previo(dia):
    return (pd.Timestamp(dia) - pd.Timedelta(days=1)).strftime("%Y-%m-%d")


def ventana_sesion(dia):
    d = pd.Timestamp(dia)
    return d - pd.Timedelta(hours=2), d + pd.Timedelta(hours=21)


# ============================================================================================ fotos del libro (crudas)
_RX_TS = re.compile(r'"ts":"([^"]+)"')


def leer_fotos(dia, fuentes=None):
    """Union de las lineas viva de la sesion (ver cabecera). Devuelve (lista de fotos, info)."""
    ini, fin = ventana_sesion(dia)
    vistos = {}
    nlin = 0
    for d in (_dia_previo(dia), dia):
        for fuente, carp, pat in VIVAS:
            if fuentes and fuente not in fuentes:
                continue
            p = os.path.join(carp, pat % d)
            if not os.path.exists(p):
                continue
            with open(p, encoding="utf-8", errors="replace") as fh:
                for l in fh:
                    l = l.strip()
                    if len(l) < 40 or not l.endswith("}"):
                        continue
                    m = _RX_TS.search(l[:80])
                    if not m:
                        continue
                    try:
                        t = pd.Timestamp(datetime.strptime(m.group(1), "%Y-%m-%d %H:%M:%S"))
                    except Exception:
                        continue
                    if not (ini <= t <= fin):
                        continue
                    nlin += 1
                    hcont = hashlib.blake2b(l[l.find('"filas"'):].encode(), digest_size=12).hexdigest()
                    e = vistos.get(t)
                    if e is not None and e["h"] == hcont:
                        if fuente not in e["fuentes"]:
                            e["fuentes"].append(fuente)
                        if PREF_VIVA[fuente] < PREF_VIVA[e["fuente"]]:
                            e["fuente"] = fuente
                        continue
                    try:
                        r = json.loads(l)
                    except Exception:
                        continue
                    fl = r.get("filas") or []
                    fut = float(r.get("futuro") or 0)
                    if fut <= 0 or not fl:
                        continue
                    if e is not None and len(fl) <= e["n"]:
                        continue
                    vistos[t] = dict(r=r, h=hcont, n=len(fl), fuente=fuente, fuentes=[fuente] + (e["fuentes"] if e else []))
    fotos = []
    for t, e in sorted(vistos.items()):
        f = armar_foto(t, e["r"])
        if f is None:
            continue
        f["fuente"] = e["fuente"]; f["fuentes"] = ",".join(e["fuentes"])
        fotos.append(f)
    return fotos, dict(lineas=nlin, fotos=len(fotos))


def armar_foto(t, r):
    """FotoNq.Armar con la linea ya parseada."""
    campos = (r.get("campos") or "strike,dias,es_call,oi,iv,bid,ask,vol").split(",")
    iv_ = campos.index("vol_hoy") if "vol_hoy" in campos else campos.index("vol") if "vol" in campos else 7
    filas = r.get("filas") or []
    try:
        A = np.array([[float(x[0]), float(x[1]), float(x[2]), float(x[3]), float(x[4]), float(x[iv_])] for x in filas if len(x) >= 8], float)
    except Exception:
        return None
    if len(A) == 0:
        return None
    dr = np.round(A[:, 1], 4)
    # round de Python (mitad al par sobre el binario) = np.round para estos valores; se usa el set de Python para ser identico
    dias = sorted(set(round(float(x), 4) for x in A[:, 1]))
    idx = {d: i for i, d in enumerate(dias)}
    orden = []; por = {}
    for i in range(len(A)):
        K, di, call, oi, iv, vol = A[i, 0], round(float(A[i, 1]), 4), A[i, 2] >= 0.5, A[i, 3], A[i, 4], A[i, 5]
        if iv <= 0 or (oi <= 0 and vol <= 0):
            continue
        key = (K, idx[di])
        e = por.get(key)
        if e is None:
            e = [K, idx[di], 0.0, 0.0, 0.0, 0.0, 0.0, 0.0]; por[key] = e; orden.append(key)
        if call:
            e[2], e[4], e[6] = oi, iv, vol
        else:
            e[3], e[5], e[7] = oi, iv, vol
    F = np.array([por[k] for k in orden], float) if orden else np.zeros((0, 8))
    del dr
    return dict(ts=pd.Timestamp(t), futuro=float(r["futuro"]), dias=np.array(dias, float), filas=F)


# ============================================================================================ velas
def velas_sesion(dia):
    """Velas de 1 min de MNQ (contrato frente) de la sesion: dataset unificado (cargar.velas, la serie principal). Para la sesion de hoy
    (10-09) se agregan las del centinela 'hoy' de la 2.0 posteriores a la ultima del dataset, descartando su ultima linea (puede estar
    en formacion), como juez20/velas_hasta_ahora.py."""
    import sys
    if DS not in sys.path:
        sys.path.insert(0, DS)
    import cargar as D
    v = D.velas(dia)
    v = v[["t", "o", "h", "l", "c"]].copy() if len(v) else pd.DataFrame(columns=["t", "o", "h", "l", "c"])
    info = {"dataset": int(len(v)), "centinela": 0}
    ini, fin = ventana_sesion(dia)
    if dia >= "2026-10-09" and os.path.exists(CENTINELA):
        filas = []
        with open(CENTINELA, encoding="utf-8", errors="replace") as fh:
            for ln in fh:
                ln = ln.strip()
                if not ln.endswith("}"):
                    continue
                try:
                    r = json.loads(ln)
                except Exception:
                    continue
                filas.append((pd.Timestamp(r["t"]).floor("1min"), r["o"], r["h"], r["l"], r["c"]))
        H = pd.DataFrame(filas, columns=["t", "o", "h", "l", "c"]).drop_duplicates("t", keep="last").sort_values("t").iloc[:-1]
        sol = v.merge(H, on="t", suffixes=("", "_h"))
        info["solape_n"] = int(len(sol))
        info["solape_iguales"] = int(sum(((sol[k] - sol[k + "_h"]).abs() < .01) for k in "ohlc").eq(4).sum()) if len(sol) else 0
        ult = v["t"].max() if len(v) else ini - pd.Timedelta(minutes=1)
        nuevo = H[(H["t"] > ult) & (H["t"] >= ini) & (H["t"] < fin)]
        info["centinela"] = int(len(nuevo))
        v = pd.concat([v, nuevo], ignore_index=True)
    v["t"] = pd.to_datetime(v["t"])
    v = v.sort_values("t").drop_duplicates("t", keep="last").reset_index(drop=True)
    return v, info


def velas_m2(v1):
    """m2 de minutos pares UTC desde las de 1 m: t (apertura par), c (cierre de la ultima 1 m presente), fin = t + 120 s."""
    if not len(v1):
        return np.array([], "datetime64[ns]"), np.array([])
    tm = v1["t"].to_numpy().astype("datetime64[m]").astype(np.int64)
    par = tm - (tm % 2)
    df = pd.DataFrame({"par": par, "tm": tm, "c": v1["c"].to_numpy(float)}).sort_values("tm")
    g = df.groupby("par", sort=True).agg(c=("c", "last"))
    t = (g.index.to_numpy() * 60).astype("datetime64[s]").astype("datetime64[ns]")
    return t + np.timedelta64(120, "s"), g["c"].to_numpy(float)


def cierre_conocido(fin, c, ts):
    """velas.cierre_conocido: cierre de la ultima m2 con fin <= ts (NaN si no hay o si cerro hace > 4 h) y su fin."""
    ts = np.asarray(ts, dtype="datetime64[ns]")
    i = np.searchsorted(fin, ts, side="right") - 1
    ok = i >= 0
    out = np.full(len(ts), np.nan); f = np.full(len(ts), np.datetime64("NaT"), dtype="datetime64[ns]")
    out[ok] = c[i[ok]]; f[ok] = fin[i[ok]]
    viejo = ok & ((ts - f) > np.timedelta64(4, "h"))
    out[viejo] = np.nan
    return out, f


# ============================================================================================ la cuenta
def _g76(F, K, T, iv):
    F = np.asarray(F, float); K = np.asarray(K, float); T = np.asarray(T, float); iv = np.asarray(iv, float)
    ok = (F > 0) & (K > 0) & (T > 0) & (iv > 0)
    with np.errstate(divide="ignore", invalid="ignore"):
        v = iv * np.sqrt(T)
        d1 = (np.log(F / K) + 0.5 * iv * iv * T) / v
        g = np.exp(-0.5 * d1 * d1) * INV_SQRT_2PI / (F * v)
    return np.where(ok, g, 0.0)


def filas_hoy(foto, t):
    env = max(0.0, min(2.0, (t - foto["ts"]).total_seconds() / 86400.0))
    de = foto["dias"] - env
    val = de[de >= 0]
    mas = float(val.min()) if len(val) else 0.0
    tope = max(1.0, mas + 0.01)
    F = foto["filas"]
    if len(F) == 0:
        return None
    v = F[:, 1].astype(int)
    ok = (v >= 0) & (v < len(de))
    d = np.where(ok, de[np.clip(v, 0, max(0, len(de) - 1))], -1.0)
    ok &= (d >= 0) & (d <= tope)
    if not ok.any():
        return None
    F = F[ok]; d = d[ok]
    return dict(K=F[:, 0], T=np.maximum(d, PISO_DIAS) / 365.0, oic=F[:, 2], oip=F[:, 3], ivc=F[:, 4], ivp=F[:, 5], volc=F[:, 6], volp=F[:, 7])


def perfil_cp(fl, S):
    if fl is None or S <= 0:
        return None
    gc, gp = _g76(S, fl["K"], fl["T"], fl["ivc"]), _g76(S, fl["K"], fl["T"], fl["ivp"])
    esc = 100.0 * S * S * 0.01
    a_vc, a_vp = gc * fl["volc"] * esc, -gp * fl["volp"] * esc
    a_oc, a_op = gc * fl["oic"] * esc, -gp * fl["oip"] * esc
    aporta = (a_vc + a_vp != 0) | (a_oc + a_op != 0)
    if not aporta.any():
        return None
    ks, inv = np.unique(fl["K"][aporta], return_inverse=True)
    n = len(ks)
    s = lambda x: np.bincount(inv, x[aporta], n)
    r = dict(K=ks, gvC=s(a_vc), gvP=s(a_vp), goC=s(a_oc), goP=s(a_op))
    r["gv"] = r["gvC"] + r["gvP"]; r["go"] = r["goC"] + r["goP"]
    return r


def zero_estandar(fl, S, paso=2.5, semi=300.0, centro=50.0):
    if fl is None or S <= 0:
        return float("nan"), float("nan")
    c0 = round(S / centro) * centro
    n = int(round(semi / paso))
    xs = c0 + paso * np.arange(-n, n + 1)
    X = xs[:, None]
    gc = _g76(X, fl["K"][None, :], fl["T"][None, :], fl["ivc"][None, :]); gp = _g76(X, fl["K"][None, :], fl["T"][None, :], fl["ivp"][None, :])
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


class OiNqVivo:
    """C2 en vivo (OiNq con el reloj = t): causal."""

    def __init__(self, dia):
        d = pd.Timestamp(dia)
        self.ini = d - pd.Timedelta(hours=2); self.lim = d + pd.Timedelta(hours=3.5)
        self.sin = d.weekday() == 0 or dia in POSFERIADOS
        self.idx = 0; self.prev = None; self.salto = None; self.primera = None

    @staticmethod
    def _mapa(f):
        o = {}
        for x in f["filas"]:
            v = int(x[1])
            if v < 0 or v >= len(f["dias"]):
                continue
            venc = (f["ts"] + pd.Timedelta(days=float(f["dias"][v]))).strftime("%m-%d")
            o[(x[0], venc, 1)] = x[2]; o[(x[0], venc, 0)] = x[3]
        return o

    def avanzar(self, fotos, hasta_idx):
        """recorre las fotos [idx, hasta_idx] (ts <= t)."""
        if self.sin or self.salto is not None:
            return
        while self.idx <= hasta_idx:
            f = fotos[self.idx]; self.idx += 1
            t = f["ts"]
            if t < self.ini or t > self.lim:
                continue
            if self.primera is None:
                self.primera = t; self.prev = self._mapa(f); continue
            cur = self._mapa(f)
            com = dist = 0
            for k, v in cur.items():
                pv = self.prev.get(k)
                if pv is None or not (v > 0 or pv > 0):
                    continue
                com += 1
                if v != pv:
                    dist += 1
            if com >= 20 and dist >= 0.5 * com:
                self.salto = t; self.prev = None; return
            self.prev = cur

    def hasta(self, ahora):
        if self.sin:
            return None, "lunes o posferiado"
        if self.primera is None:
            return None, "sin fotos de noche"
        if self.salto is not None:
            return self.salto, "salto %s" % self.salto.strftime("%H:%M:%S")
        hm = self.primera.strftime("%H:%M")
        if "01:55" <= hm < "13:30":
            return None, "primera foto %s" % hm
        if ahora < self.lim:
            return self.lim, "esperando el salto"
        return self.ini + pd.Timedelta(hours=4), "sin salto visible (02:00)"

    def ok(self, t):
        h, _ = self.hasta(t)
        return not (h is not None and self.ini <= t < h)


def _arg(m, v, mayor):
    idx = np.nonzero(m)[0]
    if len(idx) == 0:
        return None
    return idx[np.argmax(v[idx])] if mayor else idx[np.argmin(v[idx])]


def muros(Fut, C, P, fut):
    m = np.abs(Fut - fut) <= min(fut * 0.02, 100.0)
    o = []
    i = _arg(m & (C > 0), C, True)
    if i is not None: o.append((float(Fut[i]), "D1"))
    j = _arg(m & (P < 0), P, False)
    if j is not None: o.append((float(Fut[j]), "D2"))
    return o


def majors(Fut, g, fut):
    m = np.abs(Fut - fut) <= min(fut * 0.02, 100.0)
    o = []
    i = _arg(m & (g > 0), g, True)
    if i is not None: o.append((float(Fut[i]), "D1"))
    j = _arg(m & (g < 0), g, False)
    if j is not None: o.append((float(Fut[j]), "D2"))
    return o


def cruces(Fut, g, isla=ISLA):
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


def ztp(Fut, g, fut):
    zs = [z for z in cruces(Fut, g) if abs(z - fut) <= RADIO]
    ar = [z for z in zs if z > fut]; ab = [z for z in zs if z < fut]
    return ([(min(ar), "D1")] if ar else []) + ([(max(ab), "D2")] if ab else [])


def doms20(Fut, gv, go, fut):
    """Nucleo20.Dominantes: 2 barras de mayor |gv| en el radio (orden estable por Fut); si no hay, con go."""
    radio = min(fut * 0.02, 100.0)
    for g, cual in ((gv, "vol"), (go, "OI")):
        m = (np.abs(Fut - fut) <= radio) & (np.abs(g) > 0)
        idx = np.nonzero(m)[0]
        if len(idx):
            orden = idx[np.argsort(-np.abs(g[idx]), kind="stable")][:2]
            return [(float(Fut[i]), "D%d" % (q + 1)) for q, i in enumerate(orden)], cual
    return [], ""


def minutos_sesion(dia, fotos, v1, prioridad=True):
    """Recorre la sesion minuto a minuto. Devuelve (registros por minuto, info). Registro: k (minuto entero), fut, corr, foto_ts, fuente,
    libro (bool), oi_ok, series {nombre: [(precio, rol, K o None)]}, doms_con."""
    ini, fin = ventana_sesion(dia)
    fin_m2, c_m2 = velas_m2(v1)
    grilla = pd.date_range(ini, fin, freq="1min", inclusive="left")
    cierres, _ = cierre_conocido(fin_m2, c_m2, grilla.values)
    t_f = np.array([np.datetime64(f["ts"]) for f in fotos], dtype="datetime64[ns]") if fotos else np.array([], "datetime64[ns]")
    # un grabador por minuto (prioridad viva4 > viva3 > viva2 > viva, el primero con foto vigente): la union intercalada mezcla libros
    # de distinto ancho (viva/viva2 ~180 filas desde 10-02 contra ~310 de viva4) y hace saltar el zero estandar (medido 10-08: ZEST
    # union contra viva4 sola, |dif| mediana 1,6 pts, p90 46 pts); MUROS/MAJORS/DOMS coinciden 96-100 %.
    recs = sorted({x for f in fotos for x in f["fuentes"].split(",")}, key=lambda r: PREF_VIVA.get(r, 9))
    por_rec = {}
    for r in recs:
        ii = np.array([q for q, f in enumerate(fotos) if r in f["fuentes"].split(",")], np.int64)
        por_rec[r] = (ii, t_f[ii])
    k_ini = int(ini.value // 60_000_000_000)
    oiv = OiNqVivo(dia)
    tanda = None; corr_hist = []; cache_z = {}
    out = []
    n_corr = 0; n_zero = 0; n_doms_oi = 0
    for i, t in enumerate(grilla):
        k = int(t.value // 60_000_000_000)
        rec = dict(k=k, fut=float(cierres[i]), libro=False, oi_ok=None, corr=None, foto_ts=None, fuente=None, series={}, doms_con="")
        out.append(rec)
        ta = (k - k_ini) // TANDA
        if ta != tanda:
            tanda = ta; cache_z = {}; corr_hist = []
        j = int(np.searchsorted(t_f, np.datetime64(t), side="right")) - 1 if len(t_f) else -1
        if j >= 0:
            oiv.avanzar(fotos, j)
        oi_ok = oiv.ok(t)
        rec["oi_ok"] = oi_ok
        fut = cierres[i]
        if np.isnan(fut) or j < 0:
            continue
        if prioridad:
            j = -1; t64 = np.datetime64(t)
            for r in recs:
                ii, tt = por_rec[r]
                q = int(np.searchsorted(tt, t64, side="right")) - 1
                if q >= 0 and (t64 - tt[q]) < np.timedelta64(VIDA_VIVA, "s"):
                    j = int(ii[q]); break
            if j < 0:
                continue
        f = fotos[j]
        if (t - f["ts"]).total_seconds() >= VIDA_VIVA:
            continue
        if not corr_hist or corr_hist[-1][0] != j:
            cv, fv = cierre_conocido(fin_m2, c_m2, [np.datetime64(f["ts"]) + np.timedelta64(120, "s")])
            if not np.isnan(cv[0]) and (np.datetime64(f["ts"]) - fv[0]) <= np.timedelta64(240, "s"):
                corr_hist.append((j, float(cv[0]) - f["futuro"]))
        med = float(np.median([x[1] for x in corr_hist])) if len(corr_hist) >= 5 else 0.0
        corr = med if abs(med) > 30.0 else 0.0
        if corr != 0.0:
            n_corr += 1
        S = float(fut) - corr
        fl = filas_hoy(f, t)
        r = perfil_cp(fl, S)
        if r is None:
            continue
        ck = (j, round(S / 50.0))
        if ck not in cache_z:
            cache_z[ck] = zero_estandar(fl, S); n_zero += 1
        zv, zo = cache_z[ck]
        zv += corr; zo += corr
        Fut = r["K"] + corr
        m = np.abs(Fut - fut) <= fut * FILTRO
        Fut = Fut[m]; K = r["K"][m]
        gvC, gvP, gv, goC, goP, go = (r[x][m] for x in ("gvC", "gvP", "gv", "goC", "goP", "go"))
        rec.update(libro=True, corr=corr, foto_ts=str(f["ts"]), fuente=f["fuente"] if not prioridad else next(r for r in recs if r in f["fuentes"].split(",")))
        kd = {float(a): float(b) for a, b in zip(Fut, K)}
        S_ = rec["series"]

        def put(nom, lst):
            if lst:
                S_[nom] = [(p, e, kd.get(p)) for p, e in lst]
        put("MUROS_NQ_vol", muros(Fut, gvC, gvP, fut))
        put("MAJORS_NQ_vol", majors(Fut, gv, fut))
        put("ZTP_NQ_vol", ztp(Fut, gv, fut))
        if zv == zv and abs(zv - fut) <= ZEST_MAX:
            S_["ZEST_NQ_vol"] = [(float(zv), "Z", None)]
        if oi_ok:
            put("MUROS_NQ_oi", muros(Fut, goC, goP, fut))
            put("MAJORS_NQ_oi", majors(Fut, go, fut))
            if zo == zo and abs(zo - fut) <= ZEST_MAX:
                S_["ZEST_NQ_oi"] = [(float(zo), "Z", None)]
        d, cual = doms20(Fut, gv, go, fut)
        put("DOMS_NQ_vol", d)
        rec["doms_con"] = cual
        if cual == "OI":
            n_doms_oi += 1
    salto, como = oiv.hasta(ini + pd.Timedelta(hours=23))
    info = dict(minutos=len(out), con_libro=sum(r["libro"] for r in out), minutos_corr=n_corr, zeros=n_zero, doms_por_oi=n_doms_oi,
                oi_salto=str(oiv.salto) if oiv.salto is not None else None, oi_como=como, oi_primera=str(oiv.primera) if oiv.primera is not None else None)
    return out, info


def rayas_de_serie(registros, serie):
    """{Timestamp minuto: {'SERIE.ROL': (precio, etiqueta)}} con TODOS los minutos de los registros (vacio = sin raya, no se arrastra).
    'MUROS_NQ_vol.C' = solo el muro de calls (D1), '.P' = solo el de puts (D2)."""
    base, _, lado = serie.partition(".")
    rol = {"C": "D1", "P": "D2"}.get(lado)
    out = {}
    for r in registros:
        d = {}
        for p, e, K in r["series"].get(base, []):
            if rol and e != rol:
                continue
            et = "NQ%s" % (("%g" % K) if K is not None else ("%.1f" % p))
            d["%s.%s" % (base, e)] = (float(p), et)
        out[pd.Timestamp(r["k"] * 60, unit="s")] = d
    return out
