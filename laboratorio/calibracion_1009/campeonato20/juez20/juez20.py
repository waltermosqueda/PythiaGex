# -*- coding: utf-8 -*-
"""
juez20.py — JUEZ DE LA VARA DEL OPERADOR PARA SCALPING (ACIERTO20), 09-10-2026. Solo lee lo que le pasan; no escribe archivos.

LA VARA (textual del operador, 09-10): "que se dibujen las dominantes y que respeten, como minimo cumplan 20 puntos de recorrido ya
  sea para arriba o para abajo ... (para scalping) y obvio no llenar el grafico de rayas o ruido o falsas entradas". Antes: "la
  dominante en el mayor extremo de las velas, aunque intenten superarla no puedan (puede atravesarla un poquito con mechazos o velas
  chicas: falso rompimiento), y despues el cambio de tendencia con el maximo recorrido hasta la otra dominante".

NO mide direccion ni rentabilidad. La "expectativa" de abajo es un DESCRIPTIVO de la vara (+20 / -rotura), no una promesa ni un
  resultado de operar: no tiene costos, ni deslizamiento, ni la decision de entrar.

------------------------------------------------------------------------------------------------------------------------------------
DEFINICION PRE-REGISTRADA (no se cambia despues de ver resultados). Reusa el nucleo de juez_operador.py (criterio_operador/juez,
validado) SIN copiarlo: se importa y se corre con giro = 20 y ventana = 120 min en modo TOLERANTE.
------------------------------------------------------------------------------------------------------------------------------------
RAYA VIGENTE en la vela t = rayas_por_minuto[t - desfase_min] (sin mirar adelante). desfase_min = 1 por defecto (niv20_m1.pkl: la
  clave t es lo que la 2.0 dibujo en la vela t con el cierre de t). Para la 4.1 (familia/niv-<sesion>-MNQZ6.jsonl) la clave k ya es
  lo vigente en la vela k (precio = cierre de la ultima m2 cerrada ANTES de k: medido 459 de 460 minutos el 09-10) -> desfase_min 0
  (rayas_desde_niv41 lo avisa). Fusion (<= 1 pt = una raya), identidad entre minutos (pista: <= 3 pts, 30 min) y arrastre (90 min)
  son los de juez_operador.
LLEGADA: la vela i toca [L-2, L+2] y la i-1 no la tocaba (con la misma L): TECHO si la i-1 estaba toda debajo, PISO si toda encima.
  Mientras una llegada no se resuelve, la misma raya no registra otra (los re-toques dentro de ese lapso son la MISMA llegada).
ROTA (modo TOLERANTE, el del operador): el precio se ACEPTA del otro lado: (a) un cierre mas alla de L+5 con cuerpo |c-o| >= 4,
  o (b) 3 cierres seguidos mas alla de L, o (c) mas de 10 min seguidos con cierres mas alla. Las mechas que pinchan y vuelven NO rompen.
ACIERTO20: la llegada NO se rompe antes de que el precio recorra >= 20 pts a favor medidos DESDE LA RAYA (techo: l <= L-20; piso:
  h >= L+20). Orden dentro de la vela (sin ticks): alcista o->l->h->c, bajista o->h->l->c; en la vela de llegada el toque es la punta
  "mas alla" (lo "a favor" previo no cuenta).
ENTRADA FALSA: se rompe antes de los 20. INDEFINIDA: 120 min sin 20 y sin rotura (se cuenta APARTE: ni acierto ni falsa).
  CENSURADA: los datos se terminan (o hay un hueco > 30 min) antes de decidirse: fuera de los porcentajes (se informa).

METRICAS (resumir20; "noche" = sesion de CME, etiqueta = fecha de t + 2 h; por noche = sobre TODAS las noches de la ventana, con o
  sin llegadas):
  llegadas, techos, pisos, censuradas, base (= llegadas - censuradas), aciertos, falsas, indefinidas,
  pct_acierto20        = aciertos / base (las indefinidas cuentan como NO acierto: es el % principal, conservador),
  pct_acierto20_decididas = aciertos / (aciertos + falsas), pct_falsas, pct_indefinidas (sobre base),
  aciertos_por_noche, falsas_por_noche, indefinidas_por_noche, llegadas_por_noche, n_noches_ventana,
  rayas_por_hora       = promedio, sobre las horas de reloj (UTC) con >= 30 velas en la ventana, de las rayas DISTINTAS (pistas)
                         vigentes en esa hora (densidad / ruido); tambien rayas_por_hora_mediana, _p90, horas_contadas,
                         rayas_simultaneas_mediana (rayas vigentes por vela), pistas_ventana,
  recorrido_mediano_acierto / recorrido_medio_acierto = MFE desde la raya tras el acierto, hasta la re-rotura (mismas reglas) con
                         tope 240 min desde la llegada (recorridos_censurados = cuantos cortaron por fin de datos/hueco),
  min_a_acierto_mediano = minutos de la llegada a los 20 pts,
  precision_mediana_abs / precision_mediana = punta real del tramo (hasta los 20) - raya, con signo (> 0 = pincho mas alla), en los
                         aciertos; falsos_rompimientos = aciertos con mecha > L+2 o algun cierre mas alla antes de los 20,
  dist_rotura_mediana  = en las falsas, distancia de la raya al CIERRE que confirmo la rotura (la "perdida" descriptiva),
  expectativa_pts      = promedio sobre aciertos+falsas de (+20 si acierto, -dist_rotura si falsa)   [DESCRIPTIVO],
  expectativa_pts_fija = idem con -perdida_fija (5 pts) en las falsas                                  [DESCRIPTIVO],
  expectativa_pts_base = (20 x aciertos - suma dist_rotura) / base (las indefinidas valen 0)          [DESCRIPTIVO].

PLACEBO (placebo20; misma mecanica que juez_operador.placebo):
  (1) CORRIDAS: las mismas pistas corridas +-11/+-19/+-31 pts (misma densidad, misma forma, mismo parpadeo).
  (2) AZAR: n_azar juegos (500): cada pista conserva vida, parpadeo y deriva (= la MISMA cantidad de rayas por minuto) y su distancia
      al precio al nacer se sortea de la distribucion de distancias del conjunto real ('pool'), con signo al azar.
  Por metrica: p5/p50/p95/media/sd del azar, PERCENTIL del real (fraccion de juegos con valor menor, empates a medias), p_valor de una
  cola por permutacion ((1 + juegos iguales o mejores) / (n + 1); "mejor" = mayor, salvo falsas_por_noche, pct_falsas y
  precision_mediana_abs donde mejor = menor), z = (real - media) / sd (con signo de "mejor") y p_normal = 1 - Phi(z).
  P PRINCIPAL PARA HOLM = placebo['azar']['pct_acierto20']['p_valor']. OJO: con n juegos el p por permutacion no baja de 1/(n+1)
  (500 -> 0,002): con m series Holm exige p <= 0,05/m en la mejor, asi que con m > 25 series NINGUNA puede pasar con 500 juegos.
  Para las candidatas, correr n_azar >= 20 x m (p.ej. 2000-5000) o informar tambien p_normal (aproximacion, dicho como tal).
  TASA BASE DEL AZAR = mediana del pct_acierto20 en los juegos al azar (y la de las corridas juntas): es lo que hace una raya
  cualquiera con la misma densidad. Bootstrap por noche (IC90, remuestreando noches de la ventana con reposicion).
  OJO: el azar conserva PISTAS, no LLEGADAS: los conteos (aciertos_por_noche...) dependen de cuantas llegadas tenga cada juego;
  comparar sobre todo los porcentajes y la expectativa.

PARTICION Y COMPARACIONES MULTIPLES: particion_noches(noches) -> (mirar, prueba): la PRUEBA es el ultimo tercio (ceil(n/3) noches, en
  orden de fecha); nada se elige mirandola. holm(p_valores) -> p ajustadas (Holm-Bonferroni) sobre TODAS las series del campeonato.

------------------------------------------------------------------------------------------------------------------------------------
API
------------------------------------------------------------------------------------------------------------------------------------
evaluar(velas, rayas_por_minuto, ventanas=None, opciones=None, n_azar=500, corrimientos=(-31,-19,-11,11,19,31), semilla=20261009,
        n_boot=2000, azar_distancia='pool', _prep=None) -> dict:
  'metricas' (resumir20 del real, con la densidad), 'eventos' (lista; los campos de juez_operador + 'acierto20', 'falsa',
  'dist_rotura', 'pts_desc'), 'bootstrap_ic90', 'placebo' (si n_azar > 0 o hay corrimientos: 'corridos', 'corridos_juntos', 'azar',
  'tasa_base_azar', 'tasa_base_corridas'), 'noches_ventana', 'n_pistas', 'opciones', 'velas' (desde, hasta, n).
  velas: DataFrame/dict con t (inicio de la vela, UTC), o, h, l, c (columna 'noche' opcional). rayas_por_minuto: como juez_operador
  (dict minuto -> lista/dict de precios; dict {clave: precio | (precio, etiqueta)}). ventanas: [(desde, hasta), ...] UTC: solo
  cuentan las llegadas y la densidad con t en [desde, hasta); el seguimiento usa todas las velas posteriores. opciones: pisa
  OPCIONES20 (claves de juez_operador: desfase_min, claves, fusion, ...; mas 'perdida_fija' y 'min_velas_hora').
preparar(velas, rayas, ventanas, opciones) -> _prep (para correr varias veces sin re-armar).
evaluar_llegada(velas, t, raya, tipo, opciones=None) -> evento: juzga UNA llegada forzada (minuto t, raya, 'techo'|'piso') con las
  mismas reglas, aunque el juez no la registre como llegada (sirve para explicar ejemplos marcados a mano).
densidad(_prep) -> dict de rayas por hora. resumir20(eventos, noches_ventana, perdida_fija=5) -> metricas sin densidad.
bootstrap20(eventos, noches_ventana, n_boot, semilla, perdida_fija) -> IC90.
ventanas_de_noches(noches, desde_h=-2.0, hasta_h=13.5) -> [(sesion + desde_h, sesion + hasta_h)] (sesion = 00:00 UTC de la fecha de
  la noche; por defecto 22:00Z de la vispera -> 13:30Z = apertura de NY).
particion_noches(noches) -> (mirar, prueba). holm(p_valores: dict) -> {clave: {'p', 'p_holm', 'rechaza_05'}}.
rayas_desde_niv41(ruta, series=None, lados=None) -> {Timestamp: {'SERIE.D1': (precio, 'NDX30800'), ...}} (usar desfase_min 0).

VALIDACION (09-10): test_juez20.py (12 casos sinteticos, todos OK: los 4 pedidos + piso, indefinida/censurada, metricas combinadas,
  rayas por hora, sin mirar adelante, llegada forzada, placebo con oraculo (percentil 100) y con rayas al azar (percentil 38,8), Holm y
  particion) y ejemplos_operador_0910.py (los ejemplos marcados a mano por el operador, contra las series reales de la 4.1).
TIEMPO medido (esta PC, prioridad baja): 13 noches, 2.928 pistas de la 2.0, placebo 500 + 6 corridas + bootstrap 2000: 42 s.
ADVERTENCIAS:
  - El juez juzga cada llegada contra la raya QUE HABIA AL LLEGAR, aunque despues la serie la mueva o la borre (las series que siguen
    al precio, como ZTP = cruce mas cercano, "saltan": una raya que el operador vio aguantar puede quedar FALSA porque la serie la
    corrio y el precio la cruzo igual). Es lo pre-registrado.
  - La identidad es por PRECIO (pista), no por nombre: si la misma raya pasa de D2 a D1 sigue siendo la misma (el operador ve una).
  - dist_rotura en TOLERANTE siempre es un cierre (> 0); en ESTRICTO (solo sensibilidad) una rotura por mecha da dist_rotura 0.
  - Un acierto por 0,06 pts cuenta igual que uno por 60: mirar recorrido_mediano_acierto y la distribucion, no solo el %.
"""
import json
import math
import os
import sys

import numpy as np
import pandas as pd

_AQUI = os.path.dirname(os.path.abspath(__file__))
_JUEZ = os.path.normpath(os.path.join(_AQUI, "..", "..", "criterio_operador", "juez"))
if _JUEZ not in sys.path:
    sys.path.insert(0, _JUEZ)
import juez_operador as J  # noqa: E402

OPCIONES20 = {"modo": "tolerante", "giro": 20.0, "ventana_min": 120}
EXTRA_DEFAULT = {"perdida_fija": 5.0, "min_velas_hora": 30}
_NAN = float("nan")

# metricas del placebo y su sentido ("mejor" = mayor salvo las de MENOR_MEJOR)
MET_PLACEBO = ("llegadas", "aciertos", "pct_acierto20", "pct_acierto20_decididas", "pct_falsas", "aciertos_por_noche",
               "falsas_por_noche", "expectativa_pts", "expectativa_pts_fija", "expectativa_pts_base", "recorrido_mediano_acierto",
               "precision_mediana_abs")
MENOR_MEJOR = ("pct_falsas", "falsas_por_noche", "precision_mediana_abs")


def _separar(opciones):
    o = dict(opciones or {})
    extra = dict(EXTRA_DEFAULT)
    for k in list(o):
        if k in EXTRA_DEFAULT:
            extra[k] = o.pop(k)
    base = dict(OPCIONES20)
    base.update(o)
    return base, extra


def _ops(ventanas, opciones):
    base, extra = _separar(opciones)
    if ventanas is not None:
        base["ventanas"] = list(ventanas)
    return J._op(base), extra


# ------------------------------------------------------------------------------------------------------------------------------
# preparacion
# ------------------------------------------------------------------------------------------------------------------------------
def preparar(velas, rayas_por_minuto, ventanas=None, opciones=None):
    """Arma velas y pistas una vez. Devuelve (V, R, op, extra) para pasar como _prep."""
    op, extra = _ops(ventanas, opciones)
    V = J.preparar_velas(velas, op)
    R = J.preparar_rayas(V, rayas_por_minuto, op)
    return V, R, op, extra


def _noches_ventana(V):
    return sorted(set(V["noche"][V["en_v"]].tolist()))


# ------------------------------------------------------------------------------------------------------------------------------
# eventos -> acierto20 / falsa / distancia de rotura
# ------------------------------------------------------------------------------------------------------------------------------
def _marcar(evs, V):
    """Agrega acierto20, falsa, dist_rotura (cierre que confirmo la rotura - raya, del lado 'mas alla') y pts_desc."""
    tm = V["tm"]; c = V["c"]
    for e in evs:
        e["acierto20"] = e["resultado"] == "sostenido"
        e["falsa"] = e["resultado"] == "rota"
        e["dist_rotura"] = None
        e["pts_desc"] = None
        if e["falsa"]:
            k = int(np.searchsorted(tm, J._a_min(e["t_decision"])))
            s = 1.0 if e["tipo"] == "techo" else -1.0
            e["dist_rotura"] = max(float((c[k] - e["raya"]) * s), 0.0)
            e["pts_desc"] = -e["dist_rotura"]
        elif e["acierto20"]:
            e["pts_desc"] = 20.0
    return evs


def _core(V, R, op, off=None):
    return _marcar(J._juzgar_core(V, R, op, off), V)


# ------------------------------------------------------------------------------------------------------------------------------
# metricas
# ------------------------------------------------------------------------------------------------------------------------------
def _med(x):
    return float(np.median(x)) if len(x) else _NAN


def _media(x):
    return float(np.mean(x)) if len(x) else _NAN


def _pct(a, b):
    return 100.0 * a / b if b else _NAN


def resumir20(eventos, noches_ventana, perdida_fija=5.0):
    E = eventos
    N = len(noches_ventana) if noches_ventana is not None else 0
    cens = sum(bool(e["censurada"]) for e in E)
    base = len(E) - cens
    ac = [e for e in E if e["acierto20"]]
    fa = [e for e in E if e["falsa"]]
    ind = sum(e["resultado"] == "indefinida" and not e["censurada"] for e in E)
    rec = np.array([e["recorrido"] for e in ac], float)
    pa = np.array([e["precision_abs"] for e in ac], float)
    ps = np.array([e["precision"] for e in ac], float)
    dr = np.array([e["dist_rotura"] for e in fa], float)
    dec = len(ac) + len(fa)
    na, nf = len(ac), len(fa)
    return {
        "llegadas": len(E), "techos": sum(e["tipo"] == "techo" for e in E), "pisos": sum(e["tipo"] == "piso" for e in E),
        "censuradas": cens, "base": base, "aciertos": na, "falsas": nf, "indefinidas": ind,
        "pct_acierto20": _pct(na, base), "pct_acierto20_decididas": _pct(na, dec), "pct_falsas": _pct(nf, base),
        "pct_indefinidas": _pct(ind, base),
        "n_noches_ventana": N,
        "aciertos_por_noche": na / N if N else _NAN, "falsas_por_noche": nf / N if N else _NAN,
        "indefinidas_por_noche": ind / N if N else _NAN, "llegadas_por_noche": len(E) / N if N else _NAN,
        "recorrido_mediano_acierto": _med(rec), "recorrido_medio_acierto": _media(rec),
        "recorridos_censurados": sum(bool(e.get("recorrido_censurado")) for e in ac),
        "min_a_acierto_mediano": _med([e["min_a_decision"] for e in ac]),
        "precision_mediana_abs": _med(pa), "precision_mediana": _med(ps),
        "falsos_rompimientos": sum(bool(e["falso_rompimiento"]) for e in ac),
        "dist_rotura_mediana": _med(dr),
        "expectativa_pts": (20.0 * na - float(dr.sum())) / dec if dec else _NAN,
        "expectativa_pts_fija": (20.0 * na - perdida_fija * nf) / dec if dec else _NAN,
        "expectativa_pts_base": (20.0 * na - float(dr.sum())) / base if base else _NAN,
        "n_noches_con_llegadas": len({e["noche"] for e in E}),
        "n_niveles": len({round(e["raya"] * 2) / 2 for e in E}),
    }


def densidad(_prep):
    """Rayas distintas (pistas) vigentes por hora de reloj dentro de la ventana; rayas simultaneas por vela."""
    V, R = _prep[0], _prep[1]
    extra = _prep[3] if len(_prep) > 3 else EXTRA_DEFAULT
    en = np.flatnonzero(V["en_v"])
    if not len(en):
        return {"rayas_por_hora": _NAN, "rayas_por_hora_mediana": _NAN, "rayas_por_hora_p90": _NAN, "horas_contadas": 0,
                "rayas_simultaneas_mediana": _NAN, "pistas_ventana": 0}
    ini, pid = R["pv_ini"], R["pv_pid"]
    hora = V["tm"][en] // 60
    por_hora = {}
    velas_hora = {}
    simult = np.empty(len(en), np.int64)
    pistas = set()
    for q, i in enumerate(en):
        ps = pid[ini[i]:ini[i + 1]]
        simult[q] = len(ps)
        hh = int(hora[q])
        por_hora.setdefault(hh, set()).update(ps.tolist())
        velas_hora[hh] = velas_hora.get(hh, 0) + 1
        pistas.update(ps.tolist())
    llenas = [len(por_hora[hh]) for hh in por_hora if velas_hora[hh] >= extra["min_velas_hora"]]
    a = np.array(llenas, float)
    return {"rayas_por_hora": _media(a), "rayas_por_hora_mediana": _med(a),
            "rayas_por_hora_p90": float(np.percentile(a, 90)) if len(a) else _NAN, "horas_contadas": len(a),
            "rayas_simultaneas_mediana": _med(simult), "pistas_ventana": len(pistas)}


_BOOT = ("pct_acierto20", "pct_acierto20_decididas", "aciertos_por_noche", "falsas_por_noche", "llegadas_por_noche",
         "expectativa_pts", "expectativa_pts_fija", "recorrido_mediano_acierto")


def bootstrap20(eventos, noches_ventana, n_boot=2000, semilla=20261009, perdida_fija=5.0):
    """IC90 remuestreando NOCHES (con reposicion) de noches_ventana (incluye las noches sin llegadas)."""
    noches = sorted(noches_ventana)
    if not noches or not n_boot:
        return {}
    pos = {nn: k for k, nn in enumerate(noches)}
    E = [e for e in eventos if e["noche"] in pos]
    ni = np.array([pos[e["noche"]] for e in E], np.int64)
    ac = np.array([e["acierto20"] for e in E], bool)
    fa = np.array([e["falsa"] for e in E], bool)
    bs = np.array([not e["censurada"] for e in E], bool)
    dr = np.array([e["dist_rotura"] if e["falsa"] else 0.0 for e in E], float)
    rec = np.array([e.get("recorrido", _NAN) if e["acierto20"] else _NAN for e in E], float)
    rng = np.random.default_rng(semilla)
    N = len(noches)
    out = {k: [] for k in _BOOT}
    for _ in range(n_boot):
        w = np.bincount(rng.integers(0, N, N), minlength=N).astype(float)
        we = w[ni] if len(ni) else np.zeros(0)
        b = (we * bs).sum(); a = (we * ac).sum(); f = (we * fa).sum(); d = a + f
        perd = (we * dr).sum()
        out["pct_acierto20"].append(100 * a / b if b else _NAN)
        out["pct_acierto20_decididas"].append(100 * a / d if d else _NAN)
        out["aciertos_por_noche"].append(a / N)
        out["falsas_por_noche"].append(f / N)
        out["llegadas_por_noche"].append(we.sum() / N)
        out["expectativa_pts"].append((20 * a - perd) / d if d else _NAN)
        out["expectativa_pts_fija"].append((20 * a - perdida_fija * f) / d if d else _NAN)
        r = np.repeat(rec[ac], we[ac].astype(int)) if len(ni) else np.zeros(0)
        out["recorrido_mediano_acierto"].append(float(np.median(r)) if len(r) else _NAN)
    res = {}
    for k, v in out.items():
        v = np.array(v, float)
        res[k] = (float(np.nanpercentile(v, 5)), float(np.nanpercentile(v, 95))) if np.isfinite(v).any() else (_NAN, _NAN)
    return res


# ------------------------------------------------------------------------------------------------------------------------------
# placebo
# ------------------------------------------------------------------------------------------------------------------------------
def _p_valor(a, x, menor_mejor):
    if menor_mejor:
        return (1.0 + (a <= x).sum()) / (len(a) + 1.0)
    return (1.0 + (a >= x).sum()) / (len(a) + 1.0)


def placebo20(_prep, real=None, corrimientos=(-31, -19, -11, 11, 19, 31), n_azar=500, semilla=20261009, azar_distancia="pool"):
    V, R, op, extra = _prep
    nv = _noches_ventana(V)
    P = len(R["pistas"])
    pf = extra["perdida_fija"]
    if real is None:
        real = resumir20(_core(V, R, op), nv, pf)
    out = {"n_azar": n_azar, "azar_distancia": azar_distancia, "corrimientos": list(corrimientos)}
    cor = {}; todos = []
    for d in corrimientos:
        e = _core(V, R, op, np.full(P, float(d)))
        cor[d] = resumir20(e, nv, pf); todos.extend(e)
    out["corridos"] = cor
    out["corridos_juntos"] = resumir20(todos, nv, pf) if corrimientos else {}
    if corrimientos:
        # los conteos por noche de las corridas juntas suman 6 juegos: se dejan los porcentajes y se promedian los conteos
        for k in ("aciertos_por_noche", "falsas_por_noche", "indefinidas_por_noche", "llegadas_por_noche"):
            out["corridos_juntos"][k] = float(np.mean([cor[d][k] for d in corrimientos]))
    out["tasa_base_corridas"] = out["corridos_juntos"].get("pct_acierto20", _NAN) if corrimientos else _NAN
    acc = {m: [] for m in MET_PLACEBO}
    if n_azar and P:
        rng = np.random.default_rng(semilla)
        for _ in range(n_azar):
            off = J._offsets_azar(V, R, rng, azar_distancia)
            r = resumir20(_core(V, R, op, off), nv, pf)
            for m in MET_PLACEBO:
                acc[m].append(r[m])
    az = {}
    for m in MET_PLACEBO:
        a = np.array(acc[m], float); a = a[np.isfinite(a)]
        x = real.get(m, _NAN)
        if len(a) and x == x:
            sd = float(a.std(ddof=1)) if len(a) > 1 else 0.0
            z = (x - float(a.mean())) / sd if sd > 0 else _NAN
            if m in MENOR_MEJOR and z == z:
                z = -z
            az[m] = {"real": x, "p5": float(np.percentile(a, 5)), "p50": float(np.percentile(a, 50)),
                     "p95": float(np.percentile(a, 95)), "media": float(a.mean()), "sd": sd,
                     "percentil": 100.0 * ((a < x).sum() + 0.5 * (a == x).sum()) / len(a),
                     "p_valor": _p_valor(a, x, m in MENOR_MEJOR), "z": z,
                     "p_normal": 0.5 * math.erfc(z / math.sqrt(2.0)) if z == z else _NAN,
                     "mejor": "menor" if m in MENOR_MEJOR else "mayor", "n": int(len(a))}
        else:
            az[m] = {"real": x, "p5": _NAN, "p50": _NAN, "p95": _NAN, "media": _NAN, "sd": _NAN, "percentil": _NAN,
                     "p_valor": _NAN, "z": _NAN, "p_normal": _NAN, "mejor": "menor" if m in MENOR_MEJOR else "mayor",
                     "n": int(len(a))}
    out["azar"] = az
    out["tasa_base_azar"] = az["pct_acierto20"]["p50"]
    return out


# ------------------------------------------------------------------------------------------------------------------------------
# API publica
# ------------------------------------------------------------------------------------------------------------------------------
def evaluar(velas, rayas_por_minuto, ventanas=None, opciones=None, n_azar=500, corrimientos=(-31, -19, -11, 11, 19, 31),
            semilla=20261009, n_boot=2000, azar_distancia="pool", _prep=None):
    """ACIERTO20 de un conjunto de rayas, con densidad, expectativa descriptiva, bootstrap por noche y placebo. Ver docstring."""
    if _prep is None:
        _prep = preparar(velas, rayas_por_minuto, ventanas, opciones)
    V, R, op, extra = _prep
    nv = _noches_ventana(V)
    evs = _core(V, R, op)
    met = resumir20(evs, nv, extra["perdida_fija"])
    met.update(densidad(_prep))
    out = {"metricas": met, "eventos": evs, "noches_ventana": nv, "n_pistas": len(R["pistas"]), "opciones": dict(op, **extra),
           "velas": {"desde": str(pd.Timestamp(V["t"][0])) if V["n"] else None,
                     "hasta": str(pd.Timestamp(V["t"][-1])) if V["n"] else None, "n": int(V["n"])},
           "bootstrap_ic90": bootstrap20(evs, nv, n_boot, semilla, extra["perdida_fija"])}
    if n_azar or corrimientos:
        out["placebo"] = placebo20(_prep, met, corrimientos, n_azar, semilla, azar_distancia)
    return out


def evaluar_llegada(velas, t, raya, tipo, opciones=None, _prep=None):
    """Juzga UNA llegada forzada en la vela t (inicio, UTC) contra la raya L, como 'techo' o 'piso', con las reglas del juez20.
    No exige que la vela anterior este afuera de la banda (sirve para los ejemplos marcados a mano)."""
    if _prep is None:
        op, extra = _ops(None, opciones)
        V = J.preparar_velas(velas, op)
        R = J.preparar_rayas(V, {}, op)
    else:
        V, R, op, extra = _prep
    i = int(np.searchsorted(V["tm"], J._a_min(t)))
    if i >= V["n"] or V["tm"][i] != J._a_min(t):
        raise KeyError("no hay vela en %s" % t)
    s = 1 if tipo == "techo" else -1
    ev = J._evaluar(V, R, i, float(raya), s, -1, J.MODOS[op["modo"]], op, np.zeros(len(R["pistas"])))
    ev.pop("_k_fin", None)
    ev["nombres"] = []; ev["etiquetas"] = []
    return _marcar([ev], V)[0]


def ventanas_de_noches(noches, desde_h=-2.0, hasta_h=13.5):
    out = []
    for n in sorted(noches):
        d = pd.Timestamp(n)
        out.append((d + pd.Timedelta(hours=desde_h), d + pd.Timedelta(hours=hasta_h)))
    return out


def particion_noches(noches):
    """(mirar, prueba): la prueba es el ultimo tercio en orden de fecha (ceil(n/3) noches)."""
    ns = sorted(noches)
    k = int(math.ceil(len(ns) / 3.0))
    return ns[:len(ns) - k], ns[len(ns) - k:]


def holm(p_valores, alfa=0.05):
    """Holm-Bonferroni sobre un dict {clave: p}. Devuelve {clave: {'p', 'p_holm', 'rechaza_05'}} (NaN = no se cuenta)."""
    items = [(k, float(p)) for k, p in p_valores.items() if p == p]
    m = len(items)
    orden = sorted(items, key=lambda x: x[1])
    out = {}
    run = 0.0
    for j, (k, p) in enumerate(orden):
        adj = min(1.0, (m - j) * p)
        run = max(run, adj)
        out[k] = {"p": p, "p_holm": run, "rechaza_05": run <= alfa}
    for k, p in p_valores.items():
        if k not in out:
            out[k] = {"p": _NAN, "p_holm": _NAN, "rechaza_05": False}
    return out


def rayas_desde_niv41(ruta, series=None, lados=None):
    """familia/niv-<sesion>-MNQZ6.jsonl de la 4.1 -> {Timestamp minuto k: {'SERIE.LADO': (precio, etiqueta)}}.
    La clave k ya es lo vigente en la vela k: juzgar con opciones {'desfase_min': 0}. series/lados filtran (p.ej. ['ZTP_NDX_vol'],
    ['D2']). Etiqueta = libro + strike (p.ej. 'NDX30800'); para Z/ZTP/CONF sin strike, el precio redondeado."""
    out = {}
    with open(ruta, encoding="utf-8", errors="replace") as fh:
        for ln in fh:
            ln = ln.strip()
            if not ln.endswith("}"):
                continue
            try:
                r = json.loads(ln)
            except Exception:
                continue
            if "k" not in r:
                continue
            t = pd.Timestamp(r["k"] * 60, unit="s")
            d = {}
            for s, lst in (r.get("s") or {}).items():
                if series and s not in series:
                    continue
                for e in lst:
                    if lados and e[1] not in lados:
                        continue
                    libro = s.split("_")[1] if "_" in s else s
                    et = "%s%s" % (libro, e[3] if e[3] is not None else round(float(e[0]), 1))
                    d["%s.%s" % (s, e[1])] = (float(e[0]), et)
            out[t] = d
    return out


def a_tabla(eventos):
    return pd.DataFrame(eventos)
