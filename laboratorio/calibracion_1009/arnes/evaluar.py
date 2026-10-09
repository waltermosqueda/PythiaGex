# -*- coding: utf-8 -*-
"""
evaluar.py — ARNES DE EVALUACION de calibracion_1009 (pre-registrado en ../PREREGISTRO.md: ahi estan las definiciones que mandan).

Juzga UN conjunto de rayas (niveles por minuto, en precio del MNQ) contra placebo, con tres jueces:
  (1) CRITERIO DEL OPERADOR (motor: juez_operador_v1.py = copia CONGELADA de ../criterio_operador/juez/juez_operador.py; su sha256
      queda en JUEZ_SHA256 y se verifica al importar). Llegada a +-2 pts viniendo del otro lado; la raya AGUANTA aunque la pinchen
      mechas o velas de cuerpo chico (falso rompimiento); ROTA solo si el precio se ACEPTA del otro lado (modo TOLERANTE: un cierre
      mas alla de L+5 con cuerpo >= 4, o 3 cierres seguidos mas alla, o > 10 min de cierres mas alla); SOSTENIDO = giro de 15 pts
      a favor antes de romper; INDEFINIDA si en 60 min no se define. Precision = punta real del tramo - raya; recorrido = maximo a
      favor desde la raya hasta la re-rotura (tope 240 min); opuesta = la raya del mismo conjunto del lado del giro. Sensibilidad:
      modos ESTRICTO y MUY TOLERANTE. Cobertura de giros: zigzag de 20 y de 40 pts sobre mechas de 1 min.
  (2) PROTOCOLO COMPLEMENTARIO = el juez de toque del laboratorio (tres/extremos_rebote.py:554-605, portado sin cambios): toque a
      +-2 viniendo de un lado (cierre previo a mas de 2), una visita = un toque (identidad = bucket de 2,5 pts); rebote = 6 pts a
      favor en <= 3 velas antes de un cierre 2 pts del otro lado; extremo = punta a <= 2 y cierre del lado de origen; y ademas
      giro15 = 15 pts a favor en <= 15 velas antes de un cierre 2 pts del otro lado.
  (3) ECO: distancia mediana raya-precio; beta por pista = pendiente de (raya(t+15) - raya(t)) sobre (precio(t+15) - precio(t)).
PLACEBOS (los mismos para los tres jueces en cada juego):
  (a) CORRIDOS: la misma serie -31/-19/-11/+11/+19/+31 pts (se suman los seis).
  (b) AZAR: n_azar juegos; en cada uno cada pista (raya con identidad) conserva su vida y su forma, pero su distancia al precio al
      nacer se sortea de la distribucion de distancias de las pistas del propio conjunto, con signo al azar (juez_operador
      ._offsets_azar 'pool'): misma cantidad de rayas y misma distribucion de distancia al precio.
ESTADISTICA: porcentajes agregados (suma de numeradores / suma de denominadores); ventaja = real - placebo en puntos porcentuales;
  p del azar = (1 + #juegos >= real) / (1 + juegos validos); IC 95 % por bootstrap de SESIONES de (real - corridos) y de (real -
  media del azar); sesiones ganadas. holm(), criterios_exito() y seleccionar_finalistas() aplican las reglas del PREREGISTRO.
PARTICION SELLADA: los dias de PRUEBA y el CASO (10-09) no se evaluan sin abrir_prueba=True + sello (finalistas.json congelado);
  cada apertura queda en aperturas_prueba.log con el sha256 del sello.

FORMATO DE ENTRADA (niveles_por_minuto): DataFrame con
  t        minuto UTC (sin zona) en el que la raya esta VIGENTE: vale para la vela de 1 min que ABRE en t. Tiene que estar calculada
           solo con lo disponible en t (velas cerradas antes de t y fotos con disponibilidad <= t). Una fila por raya y por minuto
           mientras la raya este dibujada (sin fila = sin raya ese minuto: el arnes NO arrastra valores viejos).
  nivel    precio en MNQ.
  etiqueta (opcional) nombre del lugar de la raya ('D1', 'D2', 'CW', 'Z', ...). Default 'L<n>'.
  K, libro (opcionales) strike y libro de origen: si estan, se cuentan strikes distintos (la muestra son niveles, no minutos).
Tambien acepta el dict {minuto: rayas} del juez_operador (con desfase 0).

API
  evaluar(niveles_por_minuto, dias, nombre='candidata', ...) -> dict (ver la funcion)
  minutos_y_precio(dia) -> DataFrame t, F (cierre de la vela anterior), ventana: la grilla y el precio de referencia comunes
  chequeo_futuro(niveles, dias) -> tasas de 'raya pegada a la mecha de su propia vela' (detector barato de mirar adelante)
  holm(p), criterios_exito(res, ventana, p_holm), seleccionar_finalistas({nombre: res}, ventana), loo_parametro({param: res}, ventana)
CLI
  python -I evaluar.py validar [--rapido]  -> (a) oraculo, (b) rayas al azar, (c) reproduccion de numeros ya medidos;
                                             escribe validacion_arnes.json y validacion_arnes.md en esta carpeta.
No toca ATAS, ni codigo del proyecto, ni AppData: solo lee el dataset de ../datos con cargar.py.
"""
import os
import sys

os.environ.setdefault("OMP_NUM_THREADS", "1")
os.environ.setdefault("OPENBLAS_NUM_THREADS", "1")
os.environ.setdefault("MKL_NUM_THREADS", "1")

import datetime as _dt
import hashlib
import json
import math
import time

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(AQUI)
for _p in (os.path.join(RAIZ, "datos"), AQUI):
    if _p not in sys.path:
        sys.path.insert(0, _p)

import cargar as D                      # noqa: E402  el dataset unificado (solo lectura)
import juez_operador_v1 as JO           # noqa: E402  copia congelada del juez del operador

try:
    from numba import njit              # noqa: E402
except Exception:                       # sin numba anda igual, mas lento
    def njit(*a, **k):
        if a and callable(a[0]):
            return a[0]
        return lambda f: f

VERSION = "1.0"
SEMILLA = 20261009
JUEZ_ORIGINAL = os.path.join(RAIZ, "criterio_operador", "juez", "juez_operador.py")
JUEZ_COPIA = os.path.join(AQUI, "juez_operador_v1.py")
JUEZ_SHA256 = "a20a62688e888da22c36d9200bca5f63f16949ca90837545b37847a16188d5bf"   # copia congelada el 09-10 01:30 ART

# ------------------------------------------------------------------------------------------------ particion (PREREGISTRO sec. 4)
ENTRENAMIENTO_HASTA = "2026-09-30"      # todo lo <= esta sesion es ENTRENAMIENTO
PRUEBA = ("2026-10-01", "2026-10-02", "2026-10-05", "2026-10-06", "2026-10-07", "2026-10-08")
CASO = ("2026-10-09",)                  # la noche que motivo el pedido: solo descriptiva, nunca decide
ADELANTE_DESDE = "2026-10-12"           # sesiones nuevas (todavia no existen): la confirmacion que decide un default

# ------------------------------------------------------------------------------------------------ parametros fijos
VENTANAS = ("noche", "dia")             # noche 22:00-13:30 UTC, dia 13:30-20:00 UTC (hora de APERTURA de la vela)
CORRIDOS = (-31.0, -19.0, -11.0, 11.0, 19.0, 31.0)
MODO_PRINCIPAL = "tolerante"
MODOS = ("tolerante", "estricto", "muy_tolerante")
OPCIONES_JUEZ = {"desfase_min": 0, "arrastre_min": 0}      # el arnes recibe la raya YA alineada a la vela en la que vale
TOQUE = dict(tol=2.0, reb=6.0, cruz=2.0, mirar=3, gpts=15.0, gvel=15, paso=2.5)
UMBRALES_GIRO = (20.0, 40.0)

# metricas de conteo por (sesion, ventana): indice -> nombre
CNT = ("llegadas", "base", "sost", "rotas", "indef", "cens", "fr", "exactos", "sum_rec", "con_op", "llega_op", "sum_tramo",
       "piv20", "cub20", "piv40", "cub40",
       "toq", "reb", "tras", "ext", "extreb", "giro15")
IX = {k: i for i, k in enumerate(CNT)}
# metricas derivadas: nombre -> (numerador, denominador, escala, mayor_es_mejor)
METRICAS = {
    "pct_sostenidos": ("sost", "base", 100.0, True),
    "pct_rotas": ("rotas", "base", 100.0, False),
    "pct_exactos": ("exactos", "base", 100.0, True),
    "pct_falso_entre_sostenidos": ("fr", "sost", 100.0, None),
    "recorrido_por_llegada": ("sum_rec", "base", 1.0, True),
    "recorrido_medio": ("sum_rec", "sost", 1.0, True),
    "pct_llega_opuesta": ("llega_op", "con_op", 100.0, True),
    "cobertura20": ("cub20", "piv20", 100.0, True),
    "cobertura40": ("cub40", "piv40", 100.0, True),
    "pct_rebote": ("reb", "toq", 100.0, True),
    "pct_extremo": ("ext", "toq", 100.0, True),
    "pct_extremo_y_rebote": ("extreb", "toq", 100.0, True),
    "pct_giro15": ("giro15", "toq", 100.0, True),
}
METRICAS_SECUNDARIAS_MODO = ("pct_sostenidos", "pct_rotas", "pct_exactos", "pct_falso_entre_sostenidos", "recorrido_por_llegada",
                             "recorrido_medio", "pct_llega_opuesta")


# ================================================================================================== utilidades
def _sha(p):
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for b in iter(lambda: f.read(1 << 20), b""):
            h.update(b)
    return h.hexdigest()


def _estado_juez():
    """sha256 de la copia congelada (tiene que coincidir con JUEZ_SHA256) y si el original cambio despues."""
    copia = _sha(JUEZ_COPIA)
    orig = _sha(JUEZ_ORIGINAL) if os.path.exists(JUEZ_ORIGINAL) else None
    return {"copia": copia, "esperado": JUEZ_SHA256, "copia_ok": JUEZ_SHA256 in ("PENDIENTE", copia),
            "original": orig, "original_igual_a_copia": orig == copia}


def fase_de(dia):
    if dia in PRUEBA:
        return "PRUEBA"
    if dia in CASO:
        return "CASO"
    if dia >= ADELANTE_DESDE:
        return "ADELANTE"
    if dia <= ENTRENAMIENTO_HASTA:
        return "ENTRENAMIENTO"
    return "SIN_FASE"


def _control_sello(dias, nombre, abrir_prueba, sello, incluir_caso):
    sellados = [d for d in dias if d in PRUEBA]
    caso = [d for d in dias if d in CASO]
    if caso and not incluir_caso:
        raise PermissionError("el CASO %s es solo descriptivo: pasar incluir_caso=True a sabiendas (no decide nada)" % caso)
    if not sellados:
        return None
    if not abrir_prueba:
        raise PermissionError("PRUEBA sellada (%s): primero congelar finalistas.json y llamar con abrir_prueba=True, sello=<ruta>"
                              % ", ".join(sellados))
    if not sello or not os.path.exists(sello):
        raise PermissionError("falta el sello: la ruta al finalistas.json congelado antes de abrir PRUEBA")
    h = _sha(sello)
    with open(os.path.join(AQUI, "aperturas_prueba.log"), "a", encoding="utf-8") as f:
        f.write(json.dumps({"utc": _dt.datetime.now(_dt.timezone.utc).isoformat(timespec="seconds"), "candidata": nombre,
                            "dias": sellados, "sello": os.path.abspath(sello), "sello_sha256": h}, ensure_ascii=False) + "\n")
    return h


def _hm_ny(tm):
    """minutos desde 1970 (UTC) -> minuto del dia en hora de Nueva York (con su horario de verano)."""
    tm = np.asarray(tm, np.int64)
    if not len(tm):
        return tm
    t = pd.DatetimeIndex(pd.to_datetime(tm * 60, unit="s")).tz_localize("UTC").tz_convert("America/New_York")
    return (t.hour * 60 + t.minute).to_numpy(np.int64)


def _cod_ventana_min(tm):
    """minutos desde 1970 (UTC) -> 0 noche (18:00-09:30 NY = 22:00-13:30 UTC en verano), 1 dia (09:30-16:00 NY = 13:30-20:00
    UTC), -1 fuera (16:00-18:00 NY)."""
    hm = _hm_ny(tm)
    out = np.full(hm.shape, -1, np.int8)
    out[(hm >= 1080) | (hm < 570)] = 0
    out[(hm >= 570) & (hm < 960)] = 1
    return out


# ================================================================================================== velas y rayas
_CACHE_VELAS = {}


def velas_sesiones(dias, marco=1):
    """Velas de MNQ (serie principal de cargar.py) de las sesiones pedidas, recortadas a [22:00, 21:00) UTC; marco=2 arma velas de
    2 min por minutos pares UTC (como la cache m2 de ATAS). Columna 'noche' = sesion (etiqueta para el bootstrap)."""
    clave = (tuple(dias), marco)
    if clave in _CACHE_VELAS:
        return _CACHE_VELAS[clave].copy()
    partes = []
    for d in dias:
        v = D.velas(d)
        if v is None or v.empty:
            continue
        ini, fin = D.ventana(d)
        v = v[(v["t"] >= ini) & (v["t"] < fin)][["t", "o", "h", "l", "c"]].copy()
        if marco == 2:
            k = v["t"].to_numpy().astype("datetime64[m]").astype(np.int64)
            v["t2"] = pd.to_datetime((k - k % 2) * 60, unit="s")
            v = v.groupby("t2", sort=True).agg(o=("o", "first"), h=("h", "max"), l=("l", "min"), c=("c", "last")).reset_index()
            v = v.rename(columns={"t2": "t"})
        elif marco != 1:
            raise ValueError("marco 1 o 2")
        v["noche"] = d
        partes.append(v)
    out = pd.concat(partes, ignore_index=True).sort_values("t").reset_index(drop=True) if partes else pd.DataFrame(
        columns=["t", "o", "h", "l", "c", "noche"])
    _CACHE_VELAS[clave] = out
    return out.copy()


def minutos_y_precio(dia):
    """La grilla comun de minutos de la sesion y el precio de referencia F(t) = cierre de la vela de 1 min que abrio en t-1 (la
    ultima cerrada al empezar el minuto t); si falta, el ultimo cierre de los 5 minutos previos; si no, NaN (sin rayas ese minuto)."""
    ini, fin = D.ventana(dia)
    t = pd.date_range(ini, fin, freq="1min", inclusive="left")
    v = D.velas(dia)
    s = pd.Series(v["c"].to_numpy(float), index=v["t"] + pd.Timedelta(minutes=1)) if len(v) else pd.Series(dtype=float)
    s = s[~s.index.duplicated(keep="last")]
    F = s.reindex(t, method="ffill", tolerance=pd.Timedelta(minutes=5))
    tm = t.to_numpy().astype("datetime64[m]").astype(np.int64)
    w = _cod_ventana_min(tm)
    return pd.DataFrame({"t": t, "F": F.to_numpy(float), "ventana": np.where(w == 0, "noche", np.where(w == 1, "dia", "fuera"))})


def a_rayas(niveles):
    """DataFrame (t, nivel, [etiqueta], [K], [libro]) -> {minuto (Timestamp): {etiqueta: (precio, etiqueta_strike)}}."""
    if isinstance(niveles, dict):
        return niveles
    df = pd.DataFrame(niveles).copy()
    if df.empty:
        return {}
    if "t" not in df or "nivel" not in df:
        raise KeyError("niveles: hacen falta las columnas t y nivel")
    t = pd.to_datetime(df["t"])
    if getattr(t.dt, "tz", None) is not None:
        t = t.dt.tz_convert("UTC").dt.tz_localize(None)
    df["t"] = t.dt.floor("min")
    df["nivel"] = pd.to_numeric(df["nivel"], errors="coerce")
    df = df[np.isfinite(df["nivel"]) & (df["nivel"] > 0)]
    if "etiqueta" not in df:
        df["etiqueta"] = "L" + df.groupby("t").cumcount().astype(str)
    df["etiqueta"] = df["etiqueta"].astype(str)
    if "K" in df:
        lib = df["libro"].astype(str) if "libro" in df else ""
        df["_et"] = lib + df["K"].map(lambda k: ("%g" % k) if k == k else "")
    else:
        df["_et"] = None
    df = df.drop_duplicates(["t", "etiqueta"], keep="last")
    out = {}
    for t_, et, nv, es in zip(df["t"], df["etiqueta"], df["nivel"].astype(float), df["_et"]):
        out.setdefault(t_, {})[et] = (nv, es)
    return out


def chequeo_futuro(niveles, dias, tol=0.25):
    """Detector barato de 'mirar adelante': fraccion de rayas a <= tol de la mecha (h o l) de la vela en la que valen, contra la
    misma fraccion con la vela anterior y la siguiente. Una raya causal no deberia pegarse mas a su propia vela que a la siguiente."""
    df = pd.DataFrame(niveles)
    if df.empty:
        return {}
    v = velas_sesiones(sorted(set(dias)))
    hv = pd.Series(v["h"].to_numpy(), index=v["t"]); lv = pd.Series(v["l"].to_numpy(), index=v["t"])
    t = pd.to_datetime(df["t"]).dt.floor("min")
    out = {}
    for nom, dt_ in (("propia", 0), ("anterior", -1), ("siguiente", 1)):
        tt = t + pd.Timedelta(minutes=dt_)
        h = hv.reindex(tt).to_numpy(); l = lv.reindex(tt).to_numpy(); x = df["nivel"].to_numpy(float)
        ok = np.isfinite(h)
        hit = (np.abs(x - h) <= tol) | (np.abs(x - l) <= tol)
        out[nom] = float(100.0 * hit[ok].mean()) if ok.any() else float("nan")
    out["alerta"] = bool(out["propia"] > 5.0 and out["propia"] > 2.0 * max(out["siguiente"], 0.5))
    return out


# ================================================================================================== juez de toque (portado)
@njit(cache=True)
def _toque_nb(h, l, c, gap, win, ses, fin_ses, pv_ini, val, nses, tol, reb, cruz, mirar, gpts, gvel, paso):
    """tres/extremos_rebote.py juzgar() (lineas 554-605) sin cambios de criterio, sobre la vista plana de rayas por vela.
    Devuelve [sesion, ventana, (toq, reb, tras, ext, extreb, giro15)]."""
    n = len(h)
    out = np.zeros((nses, 2, 6))
    prevB = np.empty(256, np.int64)
    curB = np.empty(256, np.int64)
    seen = np.empty(256, np.int64)
    nprev = 0
    for i in range(n):
        a = pv_ini[i]
        b = pv_ini[i + 1]
        ncur = 0
        nseen = 0
        lo = l[i]
        hi = h[i]
        ci = c[i]
        w = win[i]
        for q in range(a, b):
            Lv = val[q]
            if Lv != Lv:
                continue
            bk = np.int64(np.rint(Lv / paso))
            dup = False
            for z in range(nseen):
                if seen[z] == bk:
                    dup = True
                    break
            if dup:
                continue
            if nseen < 256:
                seen[nseen] = bk
                nseen += 1
            if not (lo <= Lv + tol and hi >= Lv - tol):
                continue
            if ncur < 256:
                curB[ncur] = bk
                ncur += 1
            if w < 0 or i == 0 or gap[i]:
                continue
            enprev = False
            for z in range(nprev):
                if prevB[z] == bk:
                    enprev = True
                    break
            if enprev:
                continue
            if i + mirar >= fin_ses[i]:
                continue
            cp = c[i - 1]
            if abs(cp - Lv) <= tol:
                continue
            lado = 1.0 if cp > Lv else -1.0
            res = 0
            if (Lv - ci) * lado >= cruz:
                res = 2
            else:
                for k in range(i + 1, i + 1 + mirar):
                    lejos = (h[k] - Lv) if lado > 0 else (Lv - l[k])
                    if lejos >= reb:
                        res = 1
                        break
                    if (Lv - c[k]) * lado >= cruz:
                        res = 2
                        break
            punta = lo if lado > 0 else hi
            ext = abs(punta - Lv) <= tol and (ci - Lv) * lado > 0
            g15 = 0
            if (Lv - ci) * lado < cruz:
                kmax = i + 1 + gvel
                if kmax > fin_ses[i]:
                    kmax = fin_ses[i]
                for k in range(i + 1, kmax):
                    if gap[k]:
                        break
                    lejos = (h[k] - Lv) if lado > 0 else (Lv - l[k])
                    if lejos >= gpts:
                        g15 = 1
                        break
                    if (Lv - c[k]) * lado >= cruz:
                        break
            s = ses[i]
            out[s, w, 0] += 1
            if res == 1:
                out[s, w, 1] += 1
            elif res == 2:
                out[s, w, 2] += 1
            if ext:
                out[s, w, 3] += 1
                if res == 1:
                    out[s, w, 4] += 1
            out[s, w, 5] += g15
        for z in range(ncur):
            prevB[z] = curB[z]
        nprev = ncur
    return out


# ================================================================================================== una corrida (real o placebo)
class _Prep:
    """velas + pistas preparadas una vez por modo; indices de sesion y ventana por vela; pivotes."""

    def __init__(self, velas, rayas, modo, dias):
        self.op = JO._op(dict(OPCIONES_JUEZ, modo=modo))
        self.V = JO.preparar_velas(velas, self.op)
        self.R = JO.preparar_rayas(self.V, rayas, self.op)
        V = self.V
        self.n = V["n"]
        self.dias = list(dias)
        pos = {d: k for k, d in enumerate(self.dias)}
        self.ses = np.array([pos.get(x, -1) for x in V["noche"]], np.int64)
        self.win = _cod_ventana_min(V["tm"])
        fin = np.zeros(self.n, np.int64)
        if self.n:
            cambio = np.r_[np.nonzero(self.ses[1:] != self.ses[:-1])[0] + 1, self.n]
            ini = 0
            for e in cambio:
                fin[ini:e] = e
                ini = e
        self.fin_ses = fin
        self.P = len(self.R["pistas"])
        self.piv = {}
        self.piv_descartados = {}
        for th in UMBRALES_GIRO:
            o2 = dict(self.op); o2["umbral_giro_cobertura"] = th
            pv = _pivotes_sanos(V, JO._pivotes(V, o2))
            self.piv_descartados[th] = pv[1]
            pv = pv[0]
            self.piv[th] = (pv, np.array([self.ses[p["k"]] for p in pv], np.int64), np.array([self.win[p["k"]] for p in pv], np.int64))
        self.op_cob = dict(self.op)


def _pivotes_sanos(V, piv):
    """Filtro de sanidad (PREREGISTRO sec. 3.4): un giro de maximo tiene que ser la punta mas alta entre su vela de aproximacion y
    el (y el de minimo, la mas baja). Saca el borde del zigzag del juez al inicio de un tramo cuando la primera vela trae el maximo
    y el minimo juntos (validacion: 1 de 1055 giros). Devuelve (sanos, descartados)."""
    h, l = V["h"], V["l"]
    ok, malos = [], 0
    for p in piv:
        a, k = p["k_aprox"], p["k"]
        if p["tipo"] == "max":
            bueno = h[a:k + 1].max() <= p["precio"]
        else:
            bueno = l[a:k + 1].min() >= p["precio"]
        if bueno:
            ok.append(p)
        else:
            malos += 1
    return ok, malos


def _contar(pp, off, con_toque=True, con_cob=True, eventos=False):
    """Una corrida con los offsets por pista 'off': conteos [sesion, ventana, CNT] (y la lista de eventos si eventos=True)."""
    nses = len(pp.dias)
    M = np.zeros((nses, 2, len(CNT)))
    evs = JO._juzgar_core(pp.V, pp.R, pp.op, off) if pp.P else []
    for e in evs:
        i = e["i"]; s = pp.ses[i]; w = pp.win[i]
        if s < 0 or w < 0:
            continue
        m = M[s, w]
        m[IX["llegadas"]] += 1
        if e["censurada"]:
            m[IX["cens"]] += 1
            continue
        m[IX["base"]] += 1
        r = e["resultado"]
        if r == "sostenido":
            m[IX["sost"]] += 1
            m[IX["fr"]] += bool(e["falso_rompimiento"])
            m[IX["exactos"]] += e["precision_abs"] <= JO.OPCIONES_DEFAULT["tol"]
            m[IX["sum_rec"]] += e["recorrido"]
            m[IX["sum_tramo"]] += e["recorrido_tramo"]
            if e.get("opuesta") is not None:
                m[IX["con_op"]] += 1
                m[IX["llega_op"]] += bool(e.get("llega_opuesta"))
        elif r == "rota":
            m[IX["rotas"]] += 1
        else:
            m[IX["indef"]] += 1
    if con_cob:
        for th, nm in ((20.0, "20"), (40.0, "40")):
            pv, ps, pw = pp.piv[th]
            if not len(pv):
                continue
            if pp.P:
                _, det = JO._cobertura_core(pp.V, pp.R, pv, pp.op_cob, off)
                ok = np.array([d[0] for d in det], bool)
            else:
                ok = np.zeros(len(pv), bool)
            sel = (ps >= 0) & (pw >= 0)
            np.add.at(M, (ps[sel], pw[sel], IX["piv" + nm]), 1)
            np.add.at(M, (ps[sel], pw[sel], IX["cub" + nm]), ok[sel].astype(float))
    if con_toque and pp.P:
        R = pp.R
        val = (R["pv_val"] + off[R["pv_pid"]]) if len(R["pv_pid"]) else R["pv_val"]
        T = _toque_nb(pp.V["h"], pp.V["l"], pp.V["c"], pp.V["gap"].astype(np.bool_), pp.win, np.maximum(pp.ses, 0), pp.fin_ses,
                      R["pv_ini"].astype(np.int64), val.astype(np.float64), nses, TOQUE["tol"], TOQUE["reb"], TOQUE["cruz"],
                      TOQUE["mirar"], TOQUE["gpts"], TOQUE["gvel"], TOQUE["paso"])
        for j, k in enumerate(("toq", "reb", "tras", "ext", "extreb", "giro15")):
            M[:, :, IX[k]] += T[:, :, j]
    return (M, evs) if eventos else M


# ================================================================================================== eco
def _eco(pp):
    V, R = pp.V, pp.R
    out = {}
    if not pp.P:
        return {w: {"minutos_con_rayas": 0} for w in VENTANAS}
    cprev = np.r_[np.nan, V["c"][:-1]]
    cprev[V["gap"]] = np.nan
    fila = np.repeat(np.arange(V["n"]), np.diff(R["pv_ini"]))
    dist = np.abs(R["pv_val"] - cprev[fila])
    nray = np.diff(R["pv_ini"])
    # beta por pista a 15 velas
    num = {0: 0.0, 1: 0.0}; den = {0: 0.0, 1: 0.0}; npar = {0: 0, 1: 0}
    nacen = {0: 0, 1: 0}
    for p in R["pistas"]:
        idx, val = p["idx"], p["val"]
        if not len(idx):
            continue
        w0 = pp.win[idx[0]]
        if w0 >= 0:
            nacen[int(w0)] += 1
        j2 = np.searchsorted(idx, idx + 15)
        ok = j2 < len(idx)
        ok[ok] = idx[j2[ok]] == idx[ok] + 15
        if not ok.any():
            continue
        a, b = idx[ok], idx[j2[ok]]
        dL = val[j2[ok]] - val[ok]
        dP = cprev[b] - cprev[a]
        ww = pp.win[a]
        g = np.isfinite(dP)
        for w in (0, 1):
            s = g & (ww == w)
            num[w] += float((dL[s] * dP[s]).sum()); den[w] += float((dP[s] ** 2).sum()); npar[w] += int(s.sum())
    for w, nom in ((0, "noche"), (1, "dia")):
        selv = pp.win == w
        selr = pp.win[fila] == w
        d = dist[selr & np.isfinite(dist)]
        horas = selv.sum() / 60.0
        out[nom] = {"minutos_ventana": int(selv.sum()), "minutos_con_rayas": int((nray[selv] > 0).sum()),
                    "rayas_por_minuto_con_rayas": float(nray[selv][nray[selv] > 0].mean()) if (nray[selv] > 0).any() else float("nan"),
                    "dist_mediana_al_precio": float(np.median(d)) if len(d) else float("nan"),
                    "dist_p10": float(np.percentile(d, 10)) if len(d) else float("nan"),
                    "beta_pista_15": num[w] / den[w] if den[w] > 0 else float("nan"), "pares_beta": npar[w],
                    "pistas_nuevas_por_hora": nacen[w] / horas if horas > 0 else float("nan")}
    return out


# ================================================================================================== estadistica
def _razon(num, den, esc):
    return esc * num / den if den > 0 else float("nan")


def _boot(nr, dr, npl, dpl, esc, B, rng):
    """bootstrap por sesion de (num_r/den_r - num_p/den_p) * esc: (ic95_lo, ic95_hi, ic90_lo, p_unilateral_dif<=0)."""
    S = len(nr)
    if S == 0 or dr.sum() <= 0 or dpl.sum() <= 0:
        return (float("nan"),) * 4
    idx = rng.integers(0, S, size=(B, S))
    a = nr[idx].sum(1); b = dr[idx].sum(1); c = npl[idx].sum(1); d = dpl[idx].sum(1)
    with np.errstate(divide="ignore", invalid="ignore"):
        x = esc * (a / b - c / d)
    x = x[np.isfinite(x)]
    if not len(x):
        return (float("nan"),) * 4
    return (float(np.percentile(x, 2.5)), float(np.percentile(x, 97.5)), float(np.percentile(x, 5)),
            float((1 + (x <= 0).sum()) / (len(x) + 1)))


def _resumen_metrica(nombre, Mreal, Mcor, Maz, w, ses_ok, B, rng):
    numk, denk, esc, mejor = METRICAS[nombre]
    i_n, i_d = IX[numk], IX[denk]
    nr = Mreal[ses_ok, w, i_n]; dr = Mreal[ses_ok, w, i_d]
    real = _razon(nr.sum(), dr.sum(), esc)
    out = {"real": real, "num": float(nr.sum()), "den": float(dr.sum())}
    if Mcor is not None:
        nc = Mcor[:, ses_ok, w, i_n].sum(0); dc = Mcor[:, ses_ok, w, i_d].sum(0)
        out["corridos"] = _razon(nc.sum(), dc.sum(), esc)
        out["corridos_por_desplazamiento"] = {("%+g" % k): _razon(Mcor[j, ses_ok, w, i_n].sum(), Mcor[j, ses_ok, w, i_d].sum(), esc)
                                              for j, k in enumerate(CORRIDOS)}
        out["ventaja_vs_corridos"] = real - out["corridos"]
        lo, hi, lo90, pb = _boot(nr, dr, nc, dc, esc, B, rng)
        out.update({"ic95_vs_corridos": [lo, hi], "ic90_inf_vs_corridos": lo90, "p_boot_vs_corridos": pb})
    if Maz is not None and len(Maz):
        na = Maz[:, ses_ok, w, i_n]; da = Maz[:, ses_ok, w, i_d]
        with np.errstate(divide="ignore", invalid="ignore"):
            pr = esc * na.sum(1) / da.sum(1)
        v = pr[np.isfinite(pr)]
        if len(v) and real == real:
            out["azar_media"] = float(v.mean()); out["azar_p5"] = float(np.percentile(v, 5)); out["azar_p95"] = float(np.percentile(v, 95))
            out["azar_n"] = int(len(v))
            out["ventaja_vs_azar"] = real - out["azar_media"]
            if mejor is not None:
                ge = (v >= real).sum() if mejor else (v <= real).sum()
                out["p_azar"] = float((1 + ge) / (1 + len(v)))
            lo, hi, lo90, pb = _boot(nr, dr, na.mean(0), da.mean(0), esc, B, rng)
            out.update({"ic95_vs_azar": [lo, hi], "p_boot_vs_azar": pb})
            out["_azar_reps"] = [round(float(x), 4) for x in pr]
    return out


def holm(p):
    """Holm-Bonferroni: lista de p -> lista de p ajustados (NaN se deja NaN y no cuenta en m)."""
    p = np.asarray(p, float)
    ok = np.isfinite(p)
    m = int(ok.sum())
    out = np.full(len(p), np.nan)
    if m == 0:
        return out.tolist()
    idx = np.nonzero(ok)[0][np.argsort(p[ok], kind="stable")]
    acum = 0.0
    for r, i in enumerate(idx):
        acum = max(acum, min(1.0, (m - r) * p[i]))
        out[i] = acum
    return out.tolist()


# ================================================================================================== evaluar
def evaluar(niveles_por_minuto, dias, nombre="candidata", modos=MODOS, n_azar=200, n_boot=2000, semilla=SEMILLA,
            con_toque=True, abrir_prueba=False, sello=None, incluir_caso=False, devolver_eventos=False, marco=1):
    """Juzga un conjunto de rayas contra placebo en las sesiones `dias`, por ventana (noche / dia).
    n_azar juegos al azar solo para el modo principal (TOLERANTE); los otros modos van solo contra los corridos.
    Devuelve dict:
      meta: nombre, dias, fases, juez (sha), arnes (sha), parametros, tiempo
      operador[modo][ventana][metrica]: real, corridos, ventaja_vs_corridos, ic95_vs_corridos, p_boot_vs_corridos, azar_media,
          ventaja_vs_azar, p_azar, ic95_vs_azar (las de azar solo en el modo principal) + 'muestra' (llegadas, base, sesiones con
          llegadas, niveles distintos (5 pts), strikes distintos, sesiones ganadas) + 'medianas' (precision y recorrido, real y corridos)
      cobertura[ventana][cobertura20|cobertura40], toque[ventana][pct_rebote|pct_extremo|pct_extremo_y_rebote|pct_giro15]
      eco[ventana]: distancia mediana al precio, beta por pista, rayas por minuto, minutos con rayas
      estratos[ventana]: % sostenidos por franja horaria NY y primera visita contra siguientes (real y corridos; descriptivo)
    """
    t0 = time.time()
    dias = sorted(set(dias))
    sello_sha = _control_sello(dias, nombre, abrir_prueba, sello, incluir_caso)
    ej = _estado_juez()
    if not ej["copia_ok"]:
        raise RuntimeError("la copia congelada del juez cambio despues del pre-registro: %s" % ej)
    rayas = a_rayas(niveles_por_minuto)
    velas = velas_sesiones(dias, marco)
    rng_az = np.random.default_rng([semilla, 1])
    rng_b = np.random.default_rng([semilla, 2])
    res = {"meta": {"nombre": nombre, "version_arnes": VERSION, "arnes_sha256": _sha(os.path.abspath(__file__)), "juez": ej,
                    "dias": dias, "fases": {d: fase_de(d) for d in dias}, "sello_sha256": sello_sha, "marco_min": marco,
                    "n_azar": n_azar, "n_boot": n_boot, "semilla": semilla, "corridos": list(CORRIDOS), "toque": TOQUE,
                    "minutos_con_algo": len(rayas)},
           "operador": {}, "cobertura": {}, "toque": {}, "eco": {}, "estratos": {}}
    eventos_real = None
    for modo in modos:
        pp = _Prep(velas, rayas, modo, dias)
        principal = modo == MODO_PRINCIPAL
        Mreal, evs = _contar(pp, np.zeros(pp.P), con_toque=principal and con_toque, con_cob=principal, eventos=True)
        Mcor = np.stack([_contar(pp, np.full(pp.P, k), con_toque=principal and con_toque, con_cob=principal) for k in CORRIDOS])
        Maz = None
        if principal and n_azar and pp.P:
            Maz = np.stack([_contar(pp, JO._offsets_azar(pp.V, pp.R, rng_az, "pool"), con_toque=con_toque, con_cob=True)
                            for _ in range(n_azar)])
        res["operador"][modo] = {}
        for w, wn in enumerate(VENTANAS):
            ses_ok = np.array([pp.win[pp.ses == s].tolist().count(w) > 0 for s in range(len(dias))], bool) if pp.n else np.zeros(0, bool)
            blk = {m: _resumen_metrica(m, Mreal, Mcor, Maz, w, ses_ok, n_boot, rng_b) for m in METRICAS_SECUNDARIAS_MODO}
            # muestra
            ev_w = [e for e in evs if pp.win[e["i"]] == w]
            base_s = Mreal[:, w, IX["base"]]; sost_s = Mreal[:, w, IX["sost"]]
            cb_s = Mcor[:, :, w, IX["base"]].sum(0); cs_s = Mcor[:, :, w, IX["sost"]].sum(0)
            con3 = base_s >= 3
            gan = int(((sost_s / np.maximum(base_s, 1)) > (cs_s / np.maximum(cb_s, 1)))[con3].sum())
            etiq = set()
            for e in ev_w:
                etiq.update(e.get("etiquetas") or [])
            blk["muestra"] = {"llegadas": int(Mreal[:, w, IX["llegadas"]].sum()), "base": int(base_s.sum()),
                              "censuradas": int(Mreal[:, w, IX["cens"]].sum()), "sesiones_ventana": int(ses_ok.sum()),
                              "sesiones_con_llegadas": int((Mreal[:, w, IX["llegadas"]] > 0).sum()),
                              "niveles_distintos_5pts": len({(e["noche"], round(e["raya"] / 5.0)) for e in ev_w}),
                              "strikes_distintos": len(etiq), "sesiones_con_3_o_mas": int(con3.sum()), "sesiones_ganadas": gan,
                              "por_sesion": {d: [int(base_s[k]), int(sost_s[k]), int(cb_s[k]), int(cs_s[k])]
                                             for k, d in enumerate(dias) if ses_ok[k]}}
            sost = [e for e in ev_w if e["resultado"] == "sostenido"]
            blk["medianas"] = {"precision_mediana_abs": float(np.median([e["precision_abs"] for e in sost])) if sost else float("nan"),
                               "precision_mediana_con_signo": float(np.median([e["precision"] for e in sost])) if sost else float("nan"),
                               "recorrido_mediano": float(np.median([e["recorrido"] for e in sost])) if sost else float("nan"),
                               "pinchazo_max_mediano_en_falsos": float(np.median([e["pinchazo_max"] for e in sost if e["falso_rompimiento"]]))
                               if any(e["falso_rompimiento"] for e in sost) else float("nan")}
            res["operador"][modo][wn] = blk
            if principal:
                res["cobertura"][wn] = {m: _resumen_metrica(m, Mreal, Mcor, Maz, w, ses_ok, n_boot, rng_b) for m in ("cobertura20", "cobertura40")}
                for m in ("cobertura20", "cobertura40"):
                    c = res["cobertura"][wn][m]
                    c["lift_vs_azar"] = c["real"] / c["azar_media"] if c.get("azar_media") else float("nan")
                    c["lift_vs_corridos"] = c["real"] / c["corridos"] if c.get("corridos") else float("nan")
                if con_toque:
                    res["toque"][wn] = {m: _resumen_metrica(m, Mreal, Mcor, Maz, w, ses_ok, n_boot, rng_b)
                                        for m in ("pct_rebote", "pct_extremo", "pct_extremo_y_rebote", "pct_giro15")}
                    res["toque"][wn]["toques"] = int(Mreal[ses_ok, w, IX["toq"]].sum())
                res["estratos"][wn] = _estratos(evs, pp, w, Mcor_evs=None)
        if principal:
            res["eco"] = _eco(pp)
            res["meta"]["pivotes_descartados_por_sanidad"] = {str(int(k)): v for k, v in pp.piv_descartados.items()}
            eventos_real = evs
    res["meta"]["segundos"] = round(time.time() - t0, 1)
    if devolver_eventos:
        res["_eventos_real"] = eventos_real
    return res


def _franja(tm):
    """franja horaria en hora de Nueva York."""
    hm = int(_hm_ny(np.array([tm], np.int64))[0])
    if hm < 570 or hm >= 1080:
        return "noche_ny"
    if hm < 630:
        return "apertura_0930_1030"
    if hm < 840:
        return "media_1030_1400"
    if hm < 930:
        return "tarde_1400_1530"
    if hm < 960:
        return "cierre_1530_1600"
    return "post_1600_1800"


def _estratos(evs, pp, w, Mcor_evs=None):
    """% sostenidos por franja horaria y primera visita de la sesion (raya a +-2,5) contra las siguientes. Descriptivo."""
    out = {"franja": {}, "visita": {}}
    vistos = {}
    for e in sorted((e for e in evs if pp.win[e["i"]] == w and not e["censurada"]), key=lambda e: e["i"]):
        fr = _franja(pp.V["tm"][e["i"]])
        a = out["franja"].setdefault(fr, [0, 0])
        a[0] += 1; a[1] += e["resultado"] == "sostenido"
        clave = e["noche"]
        prev = vistos.setdefault(clave, [])
        prim = all(abs(e["raya"] - x) > 2.5 for x in prev)
        prev.append(e["raya"])
        b = out["visita"].setdefault("primera" if prim else "siguientes", [0, 0])
        b[0] += 1; b[1] += e["resultado"] == "sostenido"
    for g in ("franja", "visita"):
        out[g] = {k: {"base": v[0], "sostenidos": v[1], "pct": 100.0 * v[1] / v[0] if v[0] else float("nan")} for k, v in out[g].items()}
    return out


# ================================================================================================== reglas del pre-registro
CANDIDATAS = {   # id -> (grupo, rol) del PREREGISTRO sec. 7; rol: 'referencia' (siempre a PRUEBA), 'candidata', 'control' (no compite)
    "C01": (1, "referencia"), "C02": (1, "referencia"), "C03": (1, "referencia"), "C04": (1, "candidata"), "C05": (1, "control"),
    "C06": (1, "candidata"), "C07": (2, "referencia"), "C08": (2, "referencia"), "C09": (2, "referencia"), "C10": (2, "referencia"),
    "C11": (2, "referencia"), "C12": (2, "referencia"), "C13": (3, "candidata"), "C14": (3, "candidata"), "C15": (3, "candidata"),
    "C16": (3, "candidata"), "C17": (3, "candidata"), "C18": (3, "candidata"), "C19": (4, "control"), "C20": (4, "control"),
    "C21": (4, "control"), "C22": (4, "control"), "C23": (4, "control"), "C24": (4, "control")}
MITAD_ENTRENAMIENTO = "2026-09-22"      # mitades del entrenamiento: <= esta sesion contra > esta sesion
CRITERIOS = {"base_min": 30, "niveles_min": 15, "sesiones_min": 4, "ventaja_min_pp": 10.0, "alfa": 0.05, "dist_min": 5.0,
             "beta_max": 0.5}


def _rol(nombre):
    """'C04_DOS_QQQ_DERIVA' o 'C04' -> rol del registro (desconocida = 'candidata')."""
    return CANDIDATAS.get(str(nombre)[:3], (0, "candidata"))[1]
SELECCION = {"base_min": 30, "niveles_min": 10, "ventaja_azar_min_pp": 5.0, "p_azar_max": 0.05, "max_finalistas": 3}


def criterios_exito(res, ventana, p_holm):
    """E1..E6 del PREREGISTRO (seccion 8) para una candidata en una ventana de PRUEBA (o ADELANTE). p_holm = p del azar ajustado por
    Holm sobre la familia de PRUEBA. Devuelve dict con cada criterio y 'gana'."""
    op = res["operador"]
    t = op[MODO_PRINCIPAL][ventana]
    m = t["muestra"]; s = t["pct_sostenidos"]
    niveles = m["strikes_distintos"] if m["strikes_distintos"] else m["niveles_distintos_5pts"]
    e1 = m["base"] >= CRITERIOS["base_min"] and niveles >= CRITERIOS["niveles_min"] and m["sesiones_con_llegadas"] >= CRITERIOS["sesiones_min"]
    e2 = (s.get("ventaja_vs_corridos", -1e9) >= CRITERIOS["ventaja_min_pp"]) and (s.get("ventaja_vs_azar", -1e9) >= CRITERIOS["ventaja_min_pp"])
    lo = s.get("ic95_vs_corridos", [float("nan")])[0]
    e3 = (p_holm == p_holm and p_holm < CRITERIOS["alfa"]) and (lo == lo and lo > 0)
    e4a = m["sesiones_con_3_o_mas"] > 0 and m["sesiones_ganadas"] > m["sesiones_con_3_o_mas"] / 2.0
    e4b = all(op.get(md, {}).get(ventana, {}).get("pct_sostenidos", {}).get("ventaja_vs_corridos", -1) > 0 for md in ("estricto", "muy_tolerante"))
    rec = t["recorrido_por_llegada"]
    cob = res["cobertura"].get(ventana, {}).get("cobertura20", {})
    e5 = (rec.get("ventaja_vs_corridos", -1) >= 0) and (cob.get("lift_vs_azar", 0) >= 1.0)
    eco = res["eco"].get(ventana, {})
    e6 = (eco.get("dist_mediana_al_precio", 0) >= CRITERIOS["dist_min"]) and (abs(eco.get("beta_pista_15", 9)) <= CRITERIOS["beta_max"])
    c = {"E1_muestra": bool(e1), "E2_tamano": bool(e2), "E3_significancia": bool(e3), "E4_consistencia": bool(e4a and e4b),
         "E5_no_empeora": bool(e5), "E6_no_es_eco": bool(e6)}
    c["gana"] = all(c.values())
    return c


def seleccionar_finalistas(resultados, ventana):
    """Regla de ENTRENAMIENTO -> PRUEBA (PREREGISTRO seccion 7): elegibles con base >= 30, niveles >= 10, ventaja contra el azar
    >= +5 pp y p_azar < 0,05; orden por el limite inferior del IC90 de (real - corridos) (> 0 obligatorio); hasta 3."""
    filas = []
    for nom, r in resultados.items():
        t = r["operador"][MODO_PRINCIPAL][ventana]
        m = t["muestra"]; s = t["pct_sostenidos"]
        niveles = m["strikes_distintos"] if m["strikes_distintos"] else m["niveles_distintos_5pts"]
        ok = (_rol(nom) != "control" and m["base"] >= SELECCION["base_min"] and niveles >= SELECCION["niveles_min"]
              and s.get("ventaja_vs_azar", -1e9) >= SELECCION["ventaja_azar_min_pp"] and s.get("p_azar", 1.0) < SELECCION["p_azar_max"]
              and s.get("ic90_inf_vs_corridos", -1e9) > 0)
        filas.append((nom, ok, s.get("ic90_inf_vs_corridos", float("nan")), s.get("p_azar", float("nan")), _rol(nom),
                      mitades(r, ventana)))
    eleg = sorted([f for f in filas if f[1]], key=lambda f: (-f[2], f[3]))
    return {"finalistas": [f[0] for f in eleg[:SELECCION["max_finalistas"]]],
            "referencias": [f[0] for f in filas if f[4] == "referencia"],
            "tabla": [{"candidata": f[0], "rol": f[4], "elegible": f[1], "ic90_inf_vs_corridos": f[2], "p_azar": f[3], "mitades": f[5]}
                      for f in filas]}


def mitades(res, ventana, corte=MITAD_ENTRENAMIENTO):
    """M1 real - corridos (pp) en cada mitad del entrenamiento (sesiones <= corte y > corte), desde 'por_sesion'."""
    ps = res["operador"][MODO_PRINCIPAL][ventana]["muestra"]["por_sesion"]
    out = {}
    for nom, sel in (("primera", lambda d: d <= corte), ("segunda", lambda d: d > corte)):
        v = [x for d, x in ps.items() if sel(d)]
        br = sum(x[0] for x in v); sr = sum(x[1] for x in v); bc = sum(x[2] for x in v); sc = sum(x[3] for x in v)
        out[nom] = {"base": br, "ventaja_vs_corridos_pp": (100 * sr / br - 100 * sc / bc) if br and bc else float("nan")}
    a, b = out["primera"]["ventaja_vs_corridos_pp"], out["segunda"]["ventaja_vs_corridos_pp"]
    out["cambia_de_signo"] = bool(a == a and b == b and a * b < 0)
    return out


def loo_parametro(resultados_por_param, ventana):
    """Validacion cruzada dejando una sesion afuera (ENTRENAMIENTO): en cada sesion retenida se elige el parametro con mayor
    (% sostenidos - corridos) en las demas; devuelve la eleccion por sesion, el parametro mas elegido y la ventaja fuera de muestra
    (sumando las sesiones retenidas con el parametro elegido en cada una)."""
    params = list(resultados_por_param)
    ps = {p: resultados_por_param[p]["operador"][MODO_PRINCIPAL][ventana]["muestra"]["por_sesion"] for p in params}
    sesiones = sorted(set().union(*[set(v) for v in ps.values()]))
    elec = {}; num_r = den_r = num_c = den_c = 0.0
    for s in sesiones:
        mejor, mv = None, -1e18
        for p in params:
            br = sum(v[0] for k, v in ps[p].items() if k != s); sr = sum(v[1] for k, v in ps[p].items() if k != s)
            bc = sum(v[2] for k, v in ps[p].items() if k != s); sc = sum(v[3] for k, v in ps[p].items() if k != s)
            val = (100 * sr / br if br else -1e9) - (100 * sc / bc if bc else 0)
            if val > mv:
                mejor, mv = p, val
        elec[s] = mejor
        v = ps[mejor].get(s)
        if v:
            den_r += v[0]; num_r += v[1]; den_c += v[2]; num_c += v[3]
    moda = max(params, key=lambda p: list(elec.values()).count(p)) if elec else None
    return {"eleccion_por_sesion": elec, "parametro": moda,
            "ventaja_fuera_de_muestra_pp": (100 * num_r / den_r if den_r else float("nan")) - (100 * num_c / den_c if den_c else float("nan"))}


def limpiar_para_json(o):
    if isinstance(o, dict):
        return {str(k): limpiar_para_json(v) for k, v in o.items() if not str(k).startswith("_eventos")}
    if isinstance(o, (list, tuple)):
        return [limpiar_para_json(x) for x in o]
    if isinstance(o, (np.floating, float)):
        return None if not np.isfinite(o) else float(o)
    if isinstance(o, (np.integer,)):
        return int(o)
    if isinstance(o, (np.bool_,)):
        return bool(o)
    if isinstance(o, (pd.Timestamp, _dt.datetime)):
        return str(o)
    return o


# ================================================================================================== VALIDACION (CLI)
def _rayas_oraculo(dias, que=("h", "l")):
    """(a) rayas en la mecha EXACTA de cada vela, vigentes en esa misma vela (miran adelante a proposito)."""
    v = velas_sesiones(dias)
    filas = []
    for k in que:
        filas.append(pd.DataFrame({"t": v["t"], "nivel": v[k], "etiqueta": k}))
    return pd.concat(filas, ignore_index=True)


def _rayas_azar_causal(dias, semilla, cada_min=60, lejos=(5.0, 60.0)):
    """(b) dos rayas al azar por sesion, una arriba y otra abajo del precio F(t) (cierre previo), a una distancia uniforme en
    'lejos', sorteadas de nuevo cada 'cada_min' minutos: causales y sin ninguna informacion."""
    rng = np.random.default_rng([semilla, 7])
    filas = []
    for d in dias:
        g = minutos_y_precio(d)
        F = g["F"].to_numpy(); t = g["t"]
        arr = abj = None
        for i in range(len(g)):
            if i % cada_min == 0 or arr is None:
                if F[i] == F[i]:
                    arr = F[i] + rng.uniform(*lejos); abj = F[i] - rng.uniform(*lejos)
            if arr is not None and F[i] == F[i]:
                filas.append((t.iat[i], arr, "A")); filas.append((t.iat[i], abj, "B"))
    return pd.DataFrame(filas, columns=["t", "nivel", "etiqueta"])


def _rango60_m2(dias):
    """E_rango60 exactamente como tres/extremos_rebote.py:445-458 (velas m2: max(h)/min(l) de las m2 CERRADAS con apertura en
    [t-62, t-2] min, >= 5 velas), en la apertura de cada vela m2."""
    v = velas_sesiones(dias, 2)
    filas = []
    for d, g in v.groupby("noche"):
        t0 = g["t"].to_numpy().astype("datetime64[m]").astype(np.int64)
        h = g["h"].to_numpy(); l = g["l"].to_numpy()
        for j, tn in enumerate(t0):
            k = (t0 + 2 <= tn) & (t0 >= tn - 62)
            if k.sum() < 5:
                continue
            tt = pd.Timestamp(int(tn) * 60, unit="s")
            filas.append((tt, float(h[k].max()), "D1")); filas.append((tt, float(l[k].min()), "D2"))
    return pd.DataFrame(filas, columns=["t", "nivel", "etiqueta"]), v


def _toque_matriz(v, niv):
    """juez de toque sobre velas v (con 'noche') y niveles por minuto de apertura -> conteos por ventana (sin placebo)."""
    op = JO._op(dict(OPCIONES_JUEZ))
    V = JO.preparar_velas(v, op)
    R = JO.preparar_rayas(V, a_rayas(niv), op)
    dias = sorted(set(V["noche"]))
    pos = {d: k for k, d in enumerate(dias)}
    ses = np.array([pos[x] for x in V["noche"]], np.int64)
    win = _cod_ventana_min(V["tm"])
    fin = np.zeros(V["n"], np.int64)
    cambio = np.r_[np.nonzero(ses[1:] != ses[:-1])[0] + 1, V["n"]]
    ini = 0
    for e in cambio:
        fin[ini:e] = e; ini = e
    T = _toque_nb(V["h"], V["l"], V["c"], V["gap"].astype(np.bool_), win, ses, fin, R["pv_ini"].astype(np.int64),
                  R["pv_val"].astype(np.float64), len(dias), TOQUE["tol"], TOQUE["reb"], TOQUE["cruz"], TOQUE["mirar"], TOQUE["gpts"],
                  TOQUE["gvel"], TOQUE["paso"])
    return T, dias


def _juzgar_fijo(o, h, l, c, Lv, mirar=3, tol=2.0, reb=6.0, cruz=2.0):
    """tres/extremos_rebote.py:608-636 (una raya fija en una subsecuencia de velas) sin cambios."""
    n = len(h)
    toca = (l <= Lv + tol) & (h >= Lv - tol)
    prev = np.r_[False, toca[:-1]]
    t_ = r_ = e_ = 0
    for i in np.nonzero(toca & ~prev)[0]:
        if i == 0 or i + mirar >= n:
            continue
        cp = c[i - 1]
        if abs(cp - Lv) <= tol:
            continue
        lado = 1 if cp > Lv else -1
        res = "indef"
        if (Lv - c[i]) * lado >= cruz:
            res = "traspaso"
        else:
            for k in range(i + 1, i + 1 + mirar):
                if ((h[k] - Lv) if lado > 0 else (Lv - l[k])) >= reb:
                    res = "rebote"; break
                if (Lv - c[k]) * lado >= cruz:
                    res = "traspaso"; break
        punta = l[i] if lado > 0 else h[i]
        ext = abs(punta - Lv) <= tol and (c[i] - Lv) * lado > 0
        t_ += 1; r_ += res == "rebote"; e_ += bool(ext)
    return t_, r_, e_


def _azar_fijo_m2(v, reps, semilla):
    """tres/extremos_rebote.py:672-688: 100 rayas fijas por sesion y ventana, uniformes en el rango de la ventana (velas m2)."""
    rng = np.random.default_rng(semilla)
    tm = v["t"].to_numpy().astype("datetime64[m]").astype(np.int64)
    win = _cod_ventana_min(tm)
    out = {0: [0, 0, 0], 1: [0, 0, 0]}
    for d, g in v.assign(_w=win).groupby("noche"):
        for w in (0, 1):
            s = g[g["_w"] == w]
            if len(s) < 30:
                continue
            o, h, l, c = (s[k].to_numpy(float) for k in ("o", "h", "l", "c"))
            lo, hi = float(l.min()), float(h.max())
            for _ in range(reps):
                a = _juzgar_fijo(o, h, l, c, float(rng.uniform(lo, hi)))
                for j in range(3):
                    out[w][j] += a[j]
    return out


def _uno_azar(args):
    dias, k, n_azar, n_boot = args
    niv = _rayas_azar_causal(dias, SEMILLA + 1000 + k)
    r = evaluar(niv, dias, nombre="azar_causal_%02d" % k, modos=MODOS, n_azar=n_azar, n_boot=n_boot, semilla=SEMILLA + k)
    out = {"k": k}
    for w in VENTANAS:
        s = r["operador"][MODO_PRINCIPAL][w]["pct_sostenidos"]
        crit = criterios_exito(r, w, s.get("p_azar", float("nan")))
        crit["elegible_seleccion"] = bool(seleccionar_finalistas({"azar_causal_%02d" % k: r}, w)["finalistas"])
        rp = r["operador"][MODO_PRINCIPAL][w]["recorrido_por_llegada"]
        cb = r["cobertura"][w]["cobertura20"]; tq = r["toque"][w]["pct_rebote"]
        out[w] = {"base": r["operador"][MODO_PRINCIPAL][w]["muestra"]["base"], "pct_sost": s["real"],
                  "corridos": s.get("corridos"), "azar_media": s.get("azar_media"), "ventaja_corr": s.get("ventaja_vs_corridos"),
                  "ventaja_azar": s.get("ventaja_vs_azar"), "p_azar": s.get("p_azar"), "ic95_corr": s.get("ic95_vs_corridos"),
                  "rec_ventaja_corr": rp.get("ventaja_vs_corridos"), "cob20_real": cb["real"], "cob20_azar": cb.get("azar_media"),
                  "cob20_p": cb.get("p_azar"), "rebote_real": tq["real"], "rebote_azar": tq.get("azar_media"), "rebote_p": tq.get("p_azar"),
                  "criterios": crit}
    return out


def validar(rapido=False):
    t0 = time.time()
    salida = {"generado_utc": _dt.datetime.now(_dt.timezone.utc).isoformat(timespec="seconds"), "juez": _estado_juez(),
              "arnes_sha256": _sha(os.path.abspath(__file__))}
    entren = [d for d in D.dias() if "2026-09-14" <= d <= ENTRENAMIENTO_HASTA]
    print("dias de entrenamiento usados en (a) y (b):", entren, flush=True)

    # ---------------- (a) oraculo
    ora = {}
    for que in (("h", "l"), ("h",)):
        r = evaluar(_rayas_oraculo(entren, que), entren, nombre="oraculo_" + "".join(que), modos=(MODO_PRINCIPAL,), n_azar=0,
                    n_boot=200, con_toque=False)
        ora["".join(que)] = {w: {"cobertura20": r["cobertura"][w]["cobertura20"]["real"], "pivotes20": r["cobertura"][w]["cobertura20"]["den"],
                                  "cobertura40": r["cobertura"][w]["cobertura40"]["real"], "pivotes40": r["cobertura"][w]["cobertura40"]["den"],
                                  "pct_sostenidos": r["operador"][MODO_PRINCIPAL][w]["pct_sostenidos"]["real"]} for w in VENTANAS}
        print("(a) oraculo", que, json.dumps(limpiar_para_json(ora["".join(que)]), ensure_ascii=False), flush=True)
    # en la version 'h' solo los giros de maximo pueden quedar cubiertos: cobertura de los MAXIMOS
    salida["a_oraculo"] = ora

    # ---------------- (b) rayas al azar causales (calibracion del placebo y del error tipo I)
    n_cand = 8 if rapido else 20
    n_az = 60 if rapido else 150
    from concurrent.futures import ProcessPoolExecutor
    with ProcessPoolExecutor(max_workers=4) as ex:
        filas = list(ex.map(_uno_azar, [(entren, k, n_az, 500) for k in range(n_cand)]))
    resb = {"n_candidatas": n_cand, "n_azar": n_az, "filas": filas}
    for w in VENTANAS:
        x = [f[w] for f in filas if f[w]["pct_sost"] is not None and f[w]["pct_sost"] == f[w]["pct_sost"]]
        if not x:
            continue
        va = np.array([f["ventaja_azar"] for f in x], float); vc = np.array([f["ventaja_corr"] for f in x], float)
        pz = np.array([f["p_azar"] for f in x], float)
        ic_cubre = np.array([(f["ic95_corr"][0] <= 0 <= f["ic95_corr"][1]) for f in x if f["ic95_corr"] and f["ic95_corr"][0] == f["ic95_corr"][0]])
        resb[w] = {"pct_sost_medio": float(np.mean([f["pct_sost"] for f in x])), "base_media": float(np.mean([f["base"] for f in x])),
                   "ventaja_vs_azar_media_pp": float(va.mean()), "ventaja_vs_azar_sd_pp": float(va.std(ddof=1)) if len(va) > 1 else float("nan"),
                   "ventaja_vs_corridos_media_pp": float(vc.mean()), "ventaja_vs_corridos_sd_pp": float(vc.std(ddof=1)) if len(vc) > 1 else float("nan"),
                   "frac_p_azar_menor_005": float((pz < 0.05).mean()), "frac_p_azar_menor_010": float((pz < 0.10).mean()),
                   "frac_ic95_corridos_cubre_0": float(ic_cubre.mean()) if len(ic_cubre) else float("nan"),
                   "cob20_real_media": float(np.nanmean([f["cob20_real"] for f in x])), "cob20_azar_media": float(np.nanmean([f["cob20_azar"] or np.nan for f in x])),
                   "rebote_real_media": float(np.nanmean([f["rebote_real"] for f in x])), "rebote_azar_media": float(np.nanmean([f["rebote_azar"] or np.nan for f in x])),
                   "ganan_E1_a_E6": int(sum(f["criterios"]["gana"] for f in x)),
                   "pasan_por_criterio": {c: int(sum(f["criterios"][c] for f in x)) for c in x[0]["criterios"] if c not in ("gana", "elegible_seleccion")},
                   "elegibles_regla_seleccion": int(sum(f["criterios"]["elegible_seleccion"] for f in x))}
        print("(b) azar", w, json.dumps(limpiar_para_json(resb[w]), ensure_ascii=False), flush=True)
    salida["b_azar"] = resb

    # ---------------- (c) reproduccion de numeros ya medidos (juez de TOQUE, velas m2, sesiones de extremos_rebote.md)
    noches_er = ["2026-09-08", "2026-09-09", "2026-09-11", "2026-09-14", "2026-09-15", "2026-09-16", "2026-09-17", "2026-09-18",
                 "2026-09-21", "2026-09-22", "2026-09-23", "2026-09-24", "2026-09-25", "2026-09-28", "2026-09-29", "2026-09-30",
                 "2026-10-01", "2026-10-02", "2026-10-05", "2026-10-06", "2026-10-07", "2026-10-08"]
    dias_er = noches_er[:-1]
    rep = {"referencia": "tres/resultados/extremos_rebote.md:19,64 (noche, 22 sesiones) y :118,152 (dia, 21 sesiones), corrida del 08-10",
           "esperado": {"E_rango60_noche": {"toques": 1667, "pct_rebote": 61.3, "pct_extremo": 47.5},
                        "E_rango60_dia": {"toques": 644, "pct_rebote": 56.7, "pct_extremo": 32.8},
                        "azar_fijo_noche": {"toques": 16962, "pct_rebote": 63.5, "pct_extremo": 50.4},
                        "azar_fijo_dia": {"toques": 12279, "pct_rebote": 61.6, "pct_extremo": 36.6}}}
    niv, v2 = _rango60_m2(noches_er)
    T, ds = _toque_matriz(v2, niv)
    ix_n = [ds.index(d) for d in noches_er if d in ds]; ix_d = [ds.index(d) for d in dias_er if d in ds]
    for nom, w, ix in (("E_rango60_noche", 0, ix_n), ("E_rango60_dia", 1, ix_d)):
        t_ = T[ix, w, 0].sum(); r_ = T[ix, w, 1].sum(); e_ = T[ix, w, 3].sum()
        rep[nom] = {"toques": int(t_), "pct_rebote": 100 * r_ / t_ if t_ else None, "pct_extremo": 100 * e_ / t_ if t_ else None}
    # sin la sesion 10-08 de noche (la corrida vieja fue a las 05:00 UTC del 10-08: esa noche estaba incompleta)
    ix_n2 = [ds.index(d) for d in noches_er[:-1] if d in ds]
    t_ = T[ix_n2, 0, 0].sum()
    rep["E_rango60_noche_sin_1008"] = {"toques": int(t_), "pct_rebote": 100 * T[ix_n2, 0, 1].sum() / t_, "pct_extremo": 100 * T[ix_n2, 0, 3].sum() / t_}
    az = _azar_fijo_m2(v2[v2["noche"].isin(noches_er)], 100, SEMILLA)
    azd = _azar_fijo_m2(v2[v2["noche"].isin(dias_er)], 100, SEMILLA + 1)
    rep["azar_fijo_noche"] = {"toques": az[0][0], "pct_rebote": 100 * az[0][1] / az[0][0], "pct_extremo": 100 * az[0][2] / az[0][0]}
    rep["azar_fijo_dia"] = {"toques": azd[1][0], "pct_rebote": 100 * azd[1][1] / azd[1][0], "pct_extremo": 100 * azd[1][2] / azd[1][0]}
    print("(c1) reproduccion extremos_rebote:", json.dumps(limpiar_para_json(rep), ensure_ascii=False), flush=True)
    # (c2) censo de giros de 20 pts (soportes_metodo/censo_mechas.md: m1, 34 sesiones 08-02..09-17: 90,2 por noche y 80,2 por dia)
    d_cen = [d for d in D.dias() if "2026-08-17" <= d <= "2026-09-17" and d != "2026-09-07"]
    vc = velas_sesiones(d_cen)
    opc = JO._op(dict(OPCIONES_JUEZ))
    Vc = JO.preparar_velas(vc, opc)
    cen = {}
    for th in (20.0, 40.0):
        o2 = dict(opc); o2["umbral_giro_cobertura"] = th
        pv, _ = _pivotes_sanos(Vc, JO._pivotes(Vc, o2))
        wv = _cod_ventana_min(np.array([Vc["tm"][p["k"]] for p in pv], np.int64))
        cen[str(int(th))] = {"sesiones": len(d_cen), "por_sesion_noche": float((wv == 0).sum() / len(d_cen)),
                             "por_sesion_dia": float((wv == 1).sum() / len(d_cen))}
    rep["censo_giros"] = {"esperado": {"20": {"noche": 90.2, "dia": 80.2}, "40": {"noche": 23.3, "dia": None}},
                          "referencia": "soportes_metodo/censo_mechas.md (THETA 20 y 40, MNQ m1, 08-02..09-17)", "medido": cen}
    print("(c2) censo de giros:", json.dumps(cen), flush=True)
    salida["c_reproduccion"] = rep
    salida["segundos"] = round(time.time() - t0, 1)
    with open(os.path.join(AQUI, "validacion_arnes.json"), "w", encoding="utf-8") as f:
        json.dump(limpiar_para_json(salida), f, ensure_ascii=False, indent=1)
    _md_validacion(limpiar_para_json(salida), entren)
    print("listo en %.0f s -> validacion_arnes.json / .md" % (time.time() - t0), flush=True)
    return salida


def _md_validacion(s, entren):
    f = lambda x, d=1: "—" if x is None else (("%." + str(d) + "f") % x)
    L = ["# Validacion del arnes (evaluar.py)", "",
         "Generado %s UTC. arnes sha256 `%s`; juez copia `%s` (original igual a la copia: %s)." % (
             s["generado_utc"], s["arnes_sha256"], s["juez"]["copia"], s["juez"]["original_igual_a_copia"]), "",
         "Dias de ENTRENAMIENTO usados en (a) y (b): %s (%d sesiones). Ninguna candidata se corrio." % (", ".join(entren), len(entren)), "",
         "## (a) Oraculo: rayas en la mecha exacta de cada vela (miran adelante a proposito)", "",
         "| rayas | ventana | cobertura giros 20 | giros 20 | cobertura giros 40 | giros 40 | % sostenidos |", "|---|---|---|---|---|---|---|"]
    for k, v in s["a_oraculo"].items():
        for w in VENTANAS:
            x = v[w]
            L.append("| %s | %s | %s %% | %s | %s %% | %s | %s %% |" % (k, w, f(x["cobertura20"]), f(x["pivotes20"], 0), f(x["cobertura40"]),
                                                                      f(x["pivotes40"], 0), f(x["pct_sostenidos"])))
    b = s["b_azar"]
    L += ["", "## (b) Rayas al azar causales (%d conjuntos, %d juegos de placebo cada uno)" % (b["n_candidatas"], b["n_azar"]), "",
          "| ventana | % sostenidos medio | llegadas medias | ventaja vs azar (media ± sd) | ventaja vs corridos (media ± sd) | p_azar < 0,05 | IC95 vs corridos cubre 0 | rebote toque real / azar |",
          "|---|---|---|---|---|---|---|---|"]
    for w in VENTANAS:
        x = b.get(w)
        if not x:
            continue
        L.append("| %s | %s | %s | %s ± %s pp | %s ± %s pp | %s %% | %s %% | %s / %s |" % (
            w, f(x["pct_sost_medio"]), f(x["base_media"], 0), f(x["ventaja_vs_azar_media_pp"], 2), f(x["ventaja_vs_azar_sd_pp"], 2),
            f(x["ventaja_vs_corridos_media_pp"], 2), f(x["ventaja_vs_corridos_sd_pp"], 2), f(100 * x["frac_p_azar_menor_005"], 0),
            f(100 * x["frac_ic95_corridos_cubre_0"], 0), f(x["rebote_real_media"]), f(x["rebote_azar_media"])))
    for w in VENTANAS:
        x = b.get(w)
        if x:
            L.append("")
            L.append("%s: conjuntos al azar que cumplen E1-E6 (con p sin ajustar): %d de %d; elegibles por la regla de seleccion: %d; "
                     "por criterio: %s." % (w, x["ganan_E1_a_E6"], b["n_candidatas"], x["elegibles_regla_seleccion"],
                                            ", ".join("%s %d" % (k, v) for k, v in x["pasan_por_criterio"].items())))
    c = s["c_reproduccion"]
    L += ["", "## (c) Reproduccion de numeros ya medidos (juez de toque, velas m2)", "", "Referencia: %s." % c["referencia"], "",
          "| serie | toques esperados / medidos | % rebote esperado / medido | % extremo esperado / medido |", "|---|---|---|---|"]
    for k in ("E_rango60_noche", "E_rango60_dia", "azar_fijo_noche", "azar_fijo_dia"):
        e = c["esperado"][k]; m = c[k]
        L.append("| %s | %s / %s | %s / %s | %s / %s |" % (k, e["toques"], m["toques"], f(e["pct_rebote"]), f(m["pct_rebote"]),
                                                       f(e["pct_extremo"]), f(m["pct_extremo"])))
    m = c["E_rango60_noche_sin_1008"]
    L.append("| E_rango60_noche sin la noche 10-08 (la corrida vieja la vio incompleta) | 1667 / %s | 61,3 / %s | 47,5 / %s |" % (
        m["toques"], f(m["pct_rebote"]), f(m["pct_extremo"])))
    cg = c["censo_giros"]["medido"]
    L += ["", "Censo de giros (zigzag del juez + filtro de sanidad, m1, %d sesiones 08-17..09-17) contra soportes_metodo/censo_mechas.md "
          "(34 sesiones 08-02..09-17, otro algoritmo de giro): 20 pts noche %s (esperado 90,2), dia %s (esperado 80,2); 40 pts noche %s "
          "(esperado 23,3), dia %s." % (cg["20"]["sesiones"], f(cg["20"]["por_sesion_noche"]), f(cg["20"]["por_sesion_dia"]),
                                         f(cg["40"]["por_sesion_noche"]), f(cg["40"]["por_sesion_dia"])), ""]
    with open(os.path.join(AQUI, "validacion_arnes.md"), "w", encoding="utf-8") as fh:
        fh.write("\n".join(L))


if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "validar":
        validar(rapido="--rapido" in sys.argv)
    else:
        print(__doc__)
