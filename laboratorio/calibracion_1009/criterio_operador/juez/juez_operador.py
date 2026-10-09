# -*- coding: utf-8 -*-
"""
juez_operador.py — JUEZ DEL CRITERIO DEL OPERADOR (09-10-2026). Solo lee lo que le pasan; no escribe archivos.

QUE MIDE (palabras del operador, 09-10-2026):
  "lo que busco es que la dominante dibujada este en el mayor extremo de las velas y aunque aparezcan otras velas no la puedan
   romper, y que se produzca el cambio de tendencia y tenga el maximo recorrido posible hasta la siguiente dominante"
  y la aclaracion del mismo dia: puede atravesarla un poquito con mechazos o velas de cuerpo chico (indecision), pero al final
  retrocede y cumple el cambio de tendencia (FALSO ROMPIMIENTO).
  El juez anterior (% de rebotes de >= 6 pts tras cada toque) premiaba rayas en el medio del rango tocadas muchas veces: NO es esto.

NO mide direccion ni rentabilidad: describe DONDE se freno el precio y CUANTO recorrio despues, siempre contra placebo.

------------------------------------------------------------------------------------------------------------------------------------
DEFINICION OPERATIVA PRE-REGISTRADA (los numeros son los defaults de OPCIONES_DEFAULT y MODOS)
------------------------------------------------------------------------------------------------------------------------------------
RAYA VIGENTE en la vela t (t = inicio de la vela de 1 min) = la que el indicador tenia dibujada con datos hasta t-1:
  rayas_por_minuto[t - desfase_min] (desfase_min = 1). Si falta esa clave se arrastra la ultima anterior hasta arrastre_min (90)
  minutos (la raya sigue en pantalla mientras el indicador no la cambie). Nunca se mira adelante.
  Rayas a <= fusion (1,0 pt) entre si en el mismo minuto son UNA sola para las llegadas (vale la primera listada: p.ej. la de
  QQQ 31042,06 y la de NDX 31041,83 no cuentan dos llegadas); la COBERTURA mira todas las rayas crudas.
  Identidad de una raya entre minutos (PISTA): la misma si esta a <= tol_identidad (3 pts) de una pista vista en los ultimos
  memoria_min (30) minutos. Asi una dominante que parpadea (aparece/desaparece) sigue siendo la misma raya.

LLEGADA: vela i que toca la banda [L-2, L+2] (h >= L-2 y l <= L+2) cuando la vela i-1 NO la tocaba (con la misma L), sin hueco
  entre ambas (> hueco_max_min = 30 min corta). Si la vela i-1 estaba toda por debajo (h < L-2) la raya es TECHO; si estaba toda
  por encima (l > L+2), PISO. Mientras una llegada no se resuelve, esa misma raya no registra otra.

DECISION (desde la vela de llegada, hasta 60 min):
  "mas alla" = por encima de un techo / por debajo de un piso. "a favor" = el lado del giro.
  GIRO CONFIRMADO: la mecha a favor llega a 15 pts de la raya (techo: l <= L-15; piso: h >= L+15)  -> EXTREMO SOSTENIDO.
  ROTA (el precio se ACEPTA del otro lado antes del giro), segun MODO:
    TOLERANTE (principal): (a) un cierre mas alla de L+5 con cuerpo |c-o| >= 4, o (b) 3 cierres seguidos mas alla de L,
                           o (c) mas de 10 min seguidos con cierres mas alla de L. Las mechas mas alla NO rompen.
    ESTRICTO:              cualquier cierre mas alla de L+2, o cualquier mecha mas alla de L+4.
    MUY TOLERANTE:         (a) cierre mas alla de L+8 con cuerpo >= 6, o (b) 5 cierres seguidos mas alla, o (c) > 10 min.
    (Con velas de 1 min la regla (c) queda tapada por la (b); se deja implementada porque asi esta pre-registrada.)
  INDEFINIDA: ni giro ni rotura en 60 min. Si los datos se terminan o hay un hueco antes, INDEFINIDA CENSURADA ('fin de datos' /
    'hueco'): no entra en los porcentajes (se informa aparte).
  Orden dentro de una vela (no hay ticks): alcista o->l->h->c, bajista o->h->l->c. En la vela de llegada el toque es la punta
    "mas alla", asi que lo "a favor" anterior al toque no cuenta. Un cierre que ocurre despues del giro en la misma vela no rompe.
  FALSO ROMPIMIENTO (se cuenta aparte y tambien dentro de los sostenidos): sostenido con alguna mecha mas alla de L+pinchazo_min
    (2 pts, el borde de la banda) o algun cierre mas alla de L antes del giro. Se registra la profundidad maxima del pinchazo.

PRECISION DEL EXTREMO = punta real del tramo - raya, con signo (techo: max de las mechas desde la llegada hasta el giro, menos L;
  piso: L menos el min). > 0 = el precio pincho mas alla; < 0 = se freno antes de la raya. Ideal 0. Incluye la mecha del pinchazo.

RECORRIDO (solo sostenidos): MFE = maximo alejamiento a favor MEDIDO DESDE LA RAYA, desde la llegada hasta la vela anterior a la
  re-rotura (mismas reglas del modo, contadores de cierre reiniciados tras el giro), con tope 240 min desde la llegada (y corte en
  hueco/fin de datos: 'recorrido_censurado'). Tambien: recorrido_desde_punta (= MFE + pinchazo si pincho) y recorrido_tramo
  (MFE hasta que el precio VUELVE a tocar la banda de la raya: el tramo limpio de ese giro).
  OPUESTA = la raya vigente del MISMO conjunto en la vela de llegada que esta del lado a favor a >= sep_opuesta (4 pts) y es la mas
  cercana (si no hay, la vigente en la vela del giro). llega_opuesta = el MFE la alcanza a +-2 pts antes de la re-rotura/tope.
  OJO: cada re-test de la raya es otra llegada, y sus recorridos se solapan (el placebo tiene la misma mecanica).

COBERTURA DE GIROS: zigzag de 20 pts sobre mechas de 1 min (pivotes con >= 20 pts de recorrido a cada lado, solo con velas, por
  tramo continuo sin huecos). Un giro esta cubierto si en la vela de APROXIMACION (la primera del tramo que llega a +-2 pts de la
  punta) habia una raya vigente a +-2 pts de la punta: ya dibujada antes de que el precio llegara.

PLACEBO: (1) las mismas pistas corridas +-11/+-19/+-31 pts (misma densidad, misma forma, mismo parpadeo); (2) 500 juegos al azar:
  cada pista conserva vida, parpadeo y deriva, pero su distancia al precio al nacer (raya - apertura de su primera vela vigente)
  se reemplaza por |d| sorteado de la distribucion de esas distancias del conjunto real (azar_distancia='pool') o por la propia
  (azar_distancia='propia'), con signo al azar. Percentil del conjunto real dentro de los 500 (fraccion de juegos con valor menor,
  empates a medias) y bootstrap por noche (IC90, remuestreando noches de la ventana, con o sin llegadas).

------------------------------------------------------------------------------------------------------------------------------------
API
------------------------------------------------------------------------------------------------------------------------------------
velas: DataFrame (o dict de columnas) con t (inicio de la vela, UTC naive o con zona), o, h, l, c. Columna 'noche' opcional
  (etiqueta para el bootstrap); si falta, noche = fecha de (t + 2 h) (= sesion de CME en horario de verano de NY: 22:00Z abre).
rayas_por_minuto: dict {minuto -> rayas}; minuto = Timestamp/datetime/str del minuto cuyo cierre ya vio el indicador al dibujar
  (como niv20_m1.pkl: clave t = lo dibujado en la vela t; por eso desfase_min=1). Si ya viene alineado a "vigente en t", desfase 0.
  rayas = lista/tupla/array de precios, o dict {nombre: precio | (precio, etiqueta) | None}. Con dict y opciones['claves'] solo se
  usan esas claves (p.ej. ('dom0','dom1','ndx_dom0','ndx_dom1')). None/NaN/0 = sin raya. La etiqueta (p.ej. 'QQQ750') sirve para
  contar strikes distintos; sin etiqueta se cuentan niveles distintos redondeados a 0,5.
opciones: dict que pisa OPCIONES_DEFAULT. Las mas usadas: modo ('tolerante'|'estricto'|'muy_tolerante'), ventanas (lista de
  (desde, hasta): solo cuentan llegadas/giros con t en [desde, hasta); el seguimiento usa todas las velas posteriores), claves.

juzgar(velas, rayas_por_minuto, opciones=None) -> dict
  'eventos': lista de dicts, uno por llegada (ver CAMPOS_EVENTO), en orden temporal.
  'metricas': resumir(eventos) (ver METRICAS).
  'modo', 'opciones', 'n_pistas'.
sensibilidad(velas, rayas, opciones=None) -> {modo: metricas} para los tres modos.
cobertura_giros(velas, rayas_por_minuto, opciones=None) -> {'n_giros','cubiertos','pct','giros':[...]}.
placebo(velas, rayas_por_minuto, opciones=None, corrimientos=(-31,-19,-11,11,19,31), n_azar=500, semilla=20261009,
        azar_distancia='pool', con_cobertura=True, n_boot=2000) -> dict con 'real', 'bootstrap_ic90', 'corridos',
        'corridos_juntos', 'azar' {metrica: {'real','p5','p50','p95','percentil','n'}}, 'cobertura_real'.
        Tambien 'eventos_real'. Percentil = fraccion de juegos al azar con valor MENOR que el real: para precision_mediana_abs y
        pct_rotas lo bueno es un percentil BAJO; para el resto, ALTO.
resumir(eventos, noches_ventana=None, tol=2.0) -> METRICAS. bootstrap_noches(eventos, noches_ventana, n_boot, semilla, tol) -> IC90.
preparar(velas, rayas, opciones) -> (V, R): se arma una vez y se pasa como _prep=(V, R) a juzgar/cobertura_giros/placebo (las
  opciones de preparacion —ventanas, claves, desfase, arrastre, fusion, identidad, hueco— tienen que ser las mismas).
rayas_desde_niv20(niv20, claves) -> dict listo para juzgar (niv20_m1.pkl de la 2.0).
a_tabla(eventos) -> DataFrame.
Tiempos medidos (09-10, esta PC): 25.544 velas y 2.928 pistas (dominantes de la 2.0, 19 noches): preparar 0,6 s, juzgar 0,1 s,
  placebo con 500 juegos 46 s.

ADVERTENCIAS (medidas al validar, ver test_juez_operador.py):
  - Regla (b) del TOLERANTE: '3 cierres seguidos mas alla' cuenta cierres apenas mas alla (+0,25). Una raya 1-2 pts adentro de la
    punta, con el precio dudando 3 minutos arriba, queda ROTA (caso 4 sintetico); en MUY TOLERANTE (5 cierres) queda sostenida.
    Por eso siempre se reportan los tres modos. Se puede mover con opciones['cierre_mas_alla'] (fuera de lo pre-registrado).
  - El placebo al azar mantiene la cantidad de PISTAS y su distancia al nacer, pero no la cantidad de LLEGADAS: si el conjunto real
    tiene mas llegadas que el azar, las metricas de total (sostenidos, exactos, recorrido_total) salen infladas por eso; comparar
    con los porcentajes (pct_*) y las medianas.
  - Con pocas pistas (p.ej. 2 rayas que viven toda la serie) el azar 'pool' degenera: solo puede sortear esas mismas distancias.
    Usar series largas (muchas noches) y mirar tambien las corridas.
  - La COBERTURA usa +-2 pre-registrado; un falso rompimiento de +3 a +6 no cuenta como cubierto (sensibilidad: tol_cobertura).
"""
import math

import numpy as np
import pandas as pd

MODOS = {
    "estricto": {"cierre_dist": 2.0, "cierre_cuerpo": 0.0, "mecha_dist": 4.0, "n_cierres": None, "min_seguidos": None},
    "tolerante": {"cierre_dist": 5.0, "cierre_cuerpo": 4.0, "mecha_dist": None, "n_cierres": 3, "min_seguidos": 10},
    "muy_tolerante": {"cierre_dist": 8.0, "cierre_cuerpo": 6.0, "mecha_dist": None, "n_cierres": 5, "min_seguidos": 10},
}

OPCIONES_DEFAULT = {
    "modo": "tolerante",
    "tol": 2.0,                 # banda de llegada +-2 pts
    "giro": 15.0,               # giro confirmado: 15 pts a favor desde la raya
    "ventana_min": 60,          # minutos para definirse
    "tope_recorrido_min": 240,  # tope del recorrido desde la llegada
    "desfase_min": 1,           # raya vigente en t = la de la clave t-1
    "arrastre_min": 90,         # si falta la clave, se arrastra la ultima hasta 90 min
    "pinchazo_min": 2.0,        # falso rompimiento: mecha > L+2 (o algun cierre > L)
    "cierre_mas_alla": 0.0,     # un cierre cuenta como "mas alla" si supera L por mas de esto
    "cuerpo_direccional": False,  # True: el cuerpo de la regla (a) cuenta solo si va hacia "mas alla"
    "sep_opuesta": 4.0,         # la opuesta tiene que estar al menos a 4 pts del lado a favor
    "tol_opuesta": 2.0,         # llegar a la opuesta = a +-2
    "fusion": 1.0,              # rayas a <= 1 pt en el mismo minuto = una sola
    "tol_identidad": 3.0,       # misma pista entre minutos si esta a <= 3 pts
    "memoria_min": 30,          # ... y fue vista en los ultimos 30 min
    "hueco_max_min": 30,        # mas de 30 min sin velas corta todo
    "claves": None,             # con rayas en dict: solo estas claves
    "ventanas": None,           # [(desde, hasta), ...] para las llegadas y los giros; None = todo
    "umbral_giro_cobertura": 20.0,
    "tol_cobertura": 2.0,
}

CAMPOS_EVENTO = """
  t_llegada (Timestamp), i (indice de vela), noche, pista (id), raya (L), nombres (claves que la dibujaban), etiquetas,
  tipo ('techo'|'piso'), resultado ('sostenido'|'rota'|'indefinida'), censurada (bool: indefinida por fin de datos/hueco),
  motivo (texto: por que se decidio), t_decision, min_a_decision, falso_rompimiento (bool, solo sostenidos),
  pinchazo_max (pts mas alla de la raya hasta la decision; = precision con signo), cierres_mas_alla (n antes de decidir),
  precision (= pinchazo_max, con signo), precision_abs,
  -- solo sostenidos --: recorrido (MFE desde la raya), recorrido_desde_punta, recorrido_tramo, t_mfe, min_a_mfe, fin_recorrido
  ('re-rotura'|'tope'|'fin de datos'|'hueco'), recorrido_censurado (bool), t_rerotura, opuesta (precio o None),
  dist_opuesta, llega_opuesta (bool o None si no habia opuesta), t_llega_opuesta.
"""

METRICAS = """
  llegadas, techos, pisos, censuradas, base (= llegadas - censuradas), sostenidos, rotas, indefinidas, falsos_rompimientos,
  pct_sostenidos / pct_rotas / pct_indefinidas (sobre base), pct_falso_entre_sostenidos, exactos (sostenidos con |precision| <= 2),
  pct_exactos (sobre base), precision_mediana_abs, precision_mediana (con signo), precision_p75_abs, recorrido_medio,
  recorrido_mediano, recorrido_tramo_medio, recorrido_desde_punta_medio, recorrido_total (suma de MFE de los sostenidos),
  con_opuesta, llega_opuesta, pct_llega_opuesta (sobre sostenidos con opuesta), n_noches (con llegadas), n_noches_ventana,
  n_niveles (niveles distintos con llegadas, redondeo 0,5), n_etiquetas (strikes/etiquetas distintas con llegadas).
"""

_NAN = float("nan")


def _op(opciones):
    op = dict(OPCIONES_DEFAULT)
    if opciones:
        desconocidas = set(opciones) - set(OPCIONES_DEFAULT)
        if desconocidas:
            raise KeyError("opciones desconocidas: %s" % sorted(desconocidas))
        op.update(opciones)
    if op["modo"] not in MODOS:
        raise KeyError("modo desconocido: %r" % op["modo"])
    return op


def _a_min(x):
    """Timestamp/datetime/str/np.datetime64 -> minutos enteros desde 1970 (UTC naive)."""
    ts = pd.Timestamp(x)
    if ts.tzinfo is not None:
        ts = ts.tz_convert("UTC").tz_localize(None)
    return int(ts.value // 60_000_000_000)


# ------------------------------------------------------------------------------------------------------------------------------
# preparacion
# ------------------------------------------------------------------------------------------------------------------------------
def preparar_velas(velas, op):
    df = pd.DataFrame(velas).copy()
    t = pd.to_datetime(df["t"])
    if getattr(t.dt, "tz", None) is not None:
        t = t.dt.tz_convert("UTC").dt.tz_localize(None)
    df["t"] = t.dt.floor("min")
    df = df.dropna(subset=["o", "h", "l", "c"]).sort_values("t").drop_duplicates("t", keep="last").reset_index(drop=True)
    tm = df["t"].to_numpy().astype("datetime64[m]").astype(np.int64)
    gap = np.ones(len(df), bool)
    gap[1:] = (tm[1:] - tm[:-1]) > op["hueco_max_min"]
    if "noche" in df.columns:
        noche = df["noche"].astype(str).to_numpy()
    else:
        noche = (df["t"] + pd.Timedelta(hours=2)).dt.strftime("%Y-%m-%d").to_numpy()
    en_v = np.ones(len(df), bool)
    if op["ventanas"]:
        en_v[:] = False
        for a, b in op["ventanas"]:
            en_v |= (tm >= _a_min(a)) & (tm < _a_min(b))
    V = {"t": df["t"].to_numpy(), "tm": tm, "gap": gap, "noche": noche, "en_v": en_v}
    for k in ("o", "h", "l", "c"):
        V[k] = df[k].to_numpy(float)
        V[k + "L"] = V[k].tolist()   # listas: el bucle de decision es mas rapido que con escalares de numpy
    V["tmL"] = tm.tolist()
    V["gapL"] = gap.tolist()
    V["n"] = len(df)
    return V


def _parse_rayas(val, claves):
    """-> lista de (precio, nombre, etiqueta) validos, en el orden dado."""
    out = []
    if val is None:
        return out
    if isinstance(val, dict):
        it = [(k, val.get(k)) for k in claves] if claves else list(val.items())
    elif isinstance(val, (list, tuple, np.ndarray)):
        it = [(None, x) for x in val]
    else:
        it = [(None, val)]
    for nom, x in it:
        et = None
        if isinstance(x, dict):
            et = x.get("etiqueta"); x = x.get("precio")
        elif isinstance(x, (list, tuple)):
            x, et = (x[0], x[1]) if len(x) > 1 else (x[0], None)
        try:
            x = float(x)
        except (TypeError, ValueError):
            continue
        if not math.isfinite(x) or x == 0.0:
            continue
        out.append((x, nom, et))
    return out


def preparar_rayas(V, rayas_por_minuto, op):
    """Alinea las rayas a cada vela (vigente = clave t-desfase, con arrastre) y arma las pistas."""
    claves = op["claves"]
    items = sorted(((_a_min(k), v) for k, v in rayas_por_minuto.items()), key=lambda x: x[0])
    km = np.array([k for k, _ in items], np.int64)
    vals = [v for _, v in items]
    n = V["n"]; tm = V["tm"]
    pistas = []          # cada una: {'idx': [], 'val': [], 'nombres': set(), 'etiquetas': set()}
    activas = {}         # pid -> (ultimo valor, ultimo tm)
    por_vela = [[] for _ in range(n)]   # (pid, valor fusionado): llegadas y opuestas
    crudas_vela = [[] for _ in range(n)]  # (pid, valor crudo): cobertura (todas las rayas dibujadas, sin fusionar)
    cache = {}
    for i in range(n):
        objetivo = tm[i] - op["desfase_min"]
        j = int(np.searchsorted(km, objetivo, side="right")) - 1 if len(km) else -1
        if j < 0 or objetivo - km[j] > op["arrastre_min"]:
            lineas = []
        else:
            if j not in cache:
                crudas = _parse_rayas(vals[j], claves)
                fus = []
                for x, nom, et in crudas:
                    for f in fus:
                        if abs(f[0] - x) <= op["fusion"]:
                            f[1].add(nom); f[2].add(et); f[3].append(x); break
                    else:
                        fus.append([x, {nom}, {et}, [x]])
                cache[j] = fus
                if len(cache) > 4:
                    cache.pop(next(iter(cache)))
            lineas = cache[j]
        # caducar pistas viejas
        for pid in [p for p, (_, ut) in activas.items() if tm[i] - ut > op["memoria_min"]]:
            activas.pop(pid)
        pares = sorted((abs(v0 - ln[0]), pid, k) for k, ln in enumerate(lineas) for pid, (v0, _) in activas.items()
                       if abs(v0 - ln[0]) <= op["tol_identidad"])
        usados_p, asignada = set(), {}
        for _, pid, k in pares:
            if pid in usados_p or k in asignada:
                continue
            usados_p.add(pid); asignada[k] = pid
        for k, (x, noms, ets, xs) in enumerate(lineas):
            pid = asignada.get(k)
            if pid is None:
                pid = len(pistas)
                pistas.append({"idx": [], "val": [], "nombres": set(), "etiquetas": set()})
            p = pistas[pid]
            p["idx"].append(i); p["val"].append(x)
            p["nombres"].update(nm for nm in noms if nm is not None)
            p["etiquetas"].update(e for e in ets if e is not None)
            activas[pid] = (x, tm[i])
            por_vela[i].append((pid, x))
            crudas_vela[i].extend((pid, xx) for xx in xs)
    for p in pistas:
        p["idx"] = np.array(p["idx"], np.int64)
        p["val"] = np.array(p["val"], float)
    # vista plana para buscar opuestas y cobertura rapido
    pv_ini = np.zeros(n + 1, np.int64)
    pv_ini[1:] = np.cumsum([len(x) for x in por_vela])
    pv_pid = np.array([pid for x in por_vela for pid, _ in x], np.int64)
    pv_val = np.array([v for x in por_vela for _, v in x], float)
    cr_ini = np.zeros(n + 1, np.int64)
    cr_ini[1:] = np.cumsum([len(x) for x in crudas_vela])
    cr_pid = np.array([pid for x in crudas_vela for pid, _ in x], np.int64)
    cr_val = np.array([v for x in crudas_vela for _, v in x], float)
    return {"pistas": pistas, "pv_ini": pv_ini, "pv_pid": pv_pid, "pv_val": pv_val,
            "cr_ini": cr_ini, "cr_pid": cr_pid, "cr_val": cr_val}


# ------------------------------------------------------------------------------------------------------------------------------
# nucleo
# ------------------------------------------------------------------------------------------------------------------------------
def _rayas_en(R, i, off, crudas=False):
    p = "cr_" if crudas else "pv_"
    a, b = R[p + "ini"][i], R[p + "ini"][i + 1]
    pid = R[p + "pid"][a:b]
    return pid, R[p + "val"][a:b] + off[pid]


def _opuesta(R, i, pid_propia, L, s, off, op):
    pid, val = _rayas_en(R, i, off)
    d = (L - val) * s           # distancia del lado a favor
    m = (pid != pid_propia) & (d >= op["sep_opuesta"])
    if not m.any():
        return None
    k = np.flatnonzero(m)[np.argmin(d[m])]
    return float(val[k])


def _primera_rotura(V, a, b, L, s, mp, op):
    """Primer indice en [a, b] donde el precio se acepta mas alla de L segun el modo (vectorizado). None si no hay."""
    if a > b:
        return None
    h = V["h"][a:b + 1]; l = V["l"][a:b + 1]; c = V["c"][a:b + 1]; o = V["o"][a:b + 1]; tm = V["tm"][a:b + 1]
    if s > 0:
        mm = h - L; cb = c - L
    else:
        mm = L - l; cb = L - c
    cuerpo = (c - o) * s if op["cuerpo_direccional"] else np.abs(c - o)
    r = (cb > mp["cierre_dist"]) & (cuerpo >= mp["cierre_cuerpo"])
    if mp["mecha_dist"] is not None:
        r |= mm > mp["mecha_dist"]
    mas = cb > op["cierre_mas_alla"]
    if mp["n_cierres"] or mp["min_seguidos"]:
        idx = np.arange(len(mas))
        ult_no = np.maximum.accumulate(np.where(~mas, idx, -1))
        run = idx - ult_no
        if mp["n_cierres"]:
            r |= run >= mp["n_cierres"]
        if mp["min_seguidos"]:
            ini = np.clip(idx - run + 1, 0, len(mas) - 1)     # donde run = 0 no importa (se enmascara con 'mas')
            r |= mas & ((tm - tm[ini] + 1) > mp["min_seguidos"])
    w = np.flatnonzero(r)
    return a + int(w[0]) if len(w) else None


def _evaluar(V, R, i, L, s, pid, mp, op, off):
    h, l, c, o, tm, gap = V["hL"], V["lL"], V["cL"], V["oL"], V["tmL"], V["gapL"]
    n = V["n"]
    tol, giro = op["tol"], op["giro"]
    t_lim = tm[i] + op["ventana_min"]
    punta = -math.inf; ncm = 0; run = 0; run_t0 = None
    res = motivo = None; k = i; k_fin = i; k_giro = None
    md, cd, cc, nc, ms = mp["mecha_dist"], mp["cierre_dist"], mp["cierre_cuerpo"], mp["n_cierres"], mp["min_seguidos"]
    while True:
        if k >= n:
            res, motivo, k_fin = "indefinida", "fin de datos", n - 1; break
        if k > i and gap[k]:
            res, motivo, k_fin = "indefinida", "hueco", k - 1; break
        if tm[k] > t_lim:
            res, motivo, k_fin = "indefinida", "%d min sin definir" % op["ventana_min"], k - 1; break
        hk, lk, ok_, ck = h[k], l[k], o[k], c[k]
        alcista = ck >= ok_
        if s > 0:
            mm, fav, cb = hk - L, L - lk, ck - L
            mas_alla_primero = not alcista
        else:
            mm, fav, cb = L - lk, hk - L, L - ck
            mas_alla_primero = alcista
        if k == i:
            mas_alla_primero = True                       # el toque ES la punta "mas alla" de la vela de llegada
            if (s > 0 and alcista) or (s < 0 and not alcista):
                fav = (L - ck) if s > 0 else (ck - L)     # despues del toque solo queda el cierre
        rompe_mecha = md is not None and mm > md
        if fav >= giro and not (rompe_mecha and mas_alla_primero):
            if mas_alla_primero:
                punta = max(punta, mm)
            res, motivo, k_giro, k_fin = "sostenido", "giro de %g pts" % giro, k, k
            break
        punta = max(punta, mm)
        if rompe_mecha:
            res, motivo, k_fin = "rota", "mecha mas alla de +%g" % md, k; break
        if cb > op["cierre_mas_alla"]:
            run += 1; ncm += 1
            if run == 1:
                run_t0 = tm[k]
        else:
            run = 0
        cuerpo = (ck - ok_) * s if op["cuerpo_direccional"] else abs(ck - ok_)
        if cb > cd and cuerpo >= cc:
            res, motivo, k_fin = "rota", "cierre mas alla de +%g con cuerpo %.2f" % (cd, cuerpo), k; break
        if nc and run >= nc:
            res, motivo, k_fin = "rota", "%d cierres seguidos mas alla" % nc, k; break
        if ms and run > 0 and (tm[k] - run_t0 + 1) > ms:
            res, motivo, k_fin = "rota", "mas de %d min con cierres mas alla" % ms, k; break
        k += 1
    ev = {"i": i, "t_llegada": pd.Timestamp(V["t"][i]), "noche": V["noche"][i], "pista": pid, "raya": float(L),
          "tipo": "techo" if s > 0 else "piso", "resultado": res, "censurada": res == "indefinida" and motivo in ("fin de datos", "hueco"),
          "motivo": motivo, "t_decision": pd.Timestamp(V["t"][k_fin]), "min_a_decision": int(tm[k_fin] - tm[i]),
          "pinchazo_max": float(punta), "precision": float(punta), "precision_abs": abs(float(punta)), "cierres_mas_alla": ncm,
          "falso_rompimiento": False, "_k_fin": k_fin}
    if res != "sostenido":
        return ev
    ev["falso_rompimiento"] = bool(punta > op["pinchazo_min"] or ncm > 0)
    # ---- recorrido ----
    t_tope = tm[i] + op["tope_recorrido_min"]
    kt_tope = int(np.searchsorted(V["tm"], t_tope, side="right")) - 1      # ultima vela con t <= tope
    g = np.flatnonzero(V["gap"][k_giro + 1:kt_tope + 1])
    if len(g):
        kt, fin = k_giro + int(g[0]), "hueco"                               # la vela anterior al hueco
    elif kt_tope == n - 1 and tm[n - 1] < t_tope:
        kt, fin = kt_tope, "fin de datos"
    else:
        kt, fin = kt_tope, "tope"
    kb = _primera_rotura(V, k_giro + 1, kt, L, s, mp, op)
    k_ult = (kb - 1) if kb is not None else kt
    if kb is not None:
        fin = "re-rotura"
    sl = slice(i, k_ult + 1)
    favs = (L - V["l"][sl]) if s > 0 else (V["h"][sl] - L)
    favs = favs.copy()
    if (s > 0 and c[i] >= o[i]) or (s < 0 and c[i] < o[i]):
        favs[0] = (L - c[i]) if s > 0 else (c[i] - L)
    j = int(np.argmax(favs)); mfe = float(favs[j])
    # tramo: hasta que el precio vuelve a tocar la banda de la raya despues del giro
    if k_giro + 1 <= k_ult:
        mms = (V["h"][k_giro + 1:k_ult + 1] - L) if s > 0 else (L - V["l"][k_giro + 1:k_ult + 1])
        w = np.flatnonzero(mms >= -tol)
        k_tr = k_giro + int(w[0]) if len(w) else k_ult      # la vela del re-toque no suma (su lado a favor puede ser previo)
    else:
        k_tr = k_giro
    tramo = float(np.max(favs[:k_tr - i + 1]))
    O = _opuesta(R, i, pid, L, s, off, op)
    if O is None:
        O = _opuesta(R, k_giro, pid, L, s, off, op)
    llega = None; t_llega = None; dO = None
    if O is not None:
        dO = (L - O) * s
        w = np.flatnonzero(favs >= dO - op["tol_opuesta"])
        llega = bool(len(w))
        if llega:
            t_llega = pd.Timestamp(V["t"][i + int(w[0])])
    ev.update({"recorrido": mfe, "recorrido_desde_punta": mfe + max(punta, 0.0), "recorrido_tramo": tramo,
               "t_mfe": pd.Timestamp(V["t"][i + j]), "min_a_mfe": int(tm[i + j] - tm[i]), "fin_recorrido": fin,
               "recorrido_censurado": fin in ("fin de datos", "hueco"),
               "t_rerotura": pd.Timestamp(V["t"][kb]) if kb is not None else None,
               "opuesta": O, "dist_opuesta": dO, "llega_opuesta": llega, "t_llega_opuesta": t_llega})
    return ev


def _juzgar_core(V, R, op, off=None):
    mp = MODOS[op["modo"]]
    pistas = R["pistas"]
    if off is None:
        off = np.zeros(len(pistas))
    h, l, gap, en_v = V["h"], V["l"], V["gap"], V["en_v"]
    tol = op["tol"]
    evs = []
    for pid, p in enumerate(pistas):
        idx = p["idx"]
        if not len(idx):
            continue
        m = idx >= 1
        idx = idx[m]; v = p["val"][m] + off[pid]
        toca = (h[idx] >= v - tol) & (l[idx] <= v + tol)
        prev = (h[idx - 1] >= v - tol) & (l[idx - 1] <= v + tol)
        cand = np.flatnonzero(toca & ~prev & ~gap[idx] & en_v[idx])
        ocupado = -1
        for q in cand:
            i = int(idx[q])
            if i <= ocupado:
                continue
            L = float(v[q])
            s = 1 if h[i - 1] < L - tol else -1
            ev = _evaluar(V, R, i, L, s, pid, mp, op, off)
            ocupado = ev.pop("_k_fin")
            ev["nombres"] = sorted(p["nombres"]); ev["etiquetas"] = sorted(p["etiquetas"])
            evs.append(ev)
    evs.sort(key=lambda e: (e["i"], e["raya"]))
    return evs


# ------------------------------------------------------------------------------------------------------------------------------
# metricas
# ------------------------------------------------------------------------------------------------------------------------------
def _med(x):
    return float(np.median(x)) if len(x) else _NAN


def _media(x):
    return float(np.mean(x)) if len(x) else _NAN


def _pct(a, b):
    return 100.0 * a / b if b else _NAN


def resumir(eventos, noches_ventana=None, tol=2.0):
    E = eventos
    sost = [e for e in E if e["resultado"] == "sostenido"]
    cens = sum(e["censurada"] for e in E)
    base = len(E) - cens
    rot = sum(e["resultado"] == "rota" for e in E)
    ind = sum(e["resultado"] == "indefinida" and not e["censurada"] for e in E)
    fr = sum(e["falso_rompimiento"] for e in sost)
    pa = np.array([e["precision_abs"] for e in sost]); ps = np.array([e["precision"] for e in sost])
    rec = np.array([e["recorrido"] for e in sost])
    ex = int((pa <= tol).sum()) if len(pa) else 0
    cop = [e for e in sost if e["opuesta"] is not None]
    ll = sum(bool(e["llega_opuesta"]) for e in cop)
    etiq = set()
    for e in E:
        etiq.update(e.get("etiquetas") or [])
    return {
        "llegadas": len(E), "techos": sum(e["tipo"] == "techo" for e in E), "pisos": sum(e["tipo"] == "piso" for e in E),
        "censuradas": cens, "base": base, "sostenidos": len(sost), "rotas": rot, "indefinidas": ind, "falsos_rompimientos": fr,
        "pct_sostenidos": _pct(len(sost), base), "pct_rotas": _pct(rot, base), "pct_indefinidas": _pct(ind, base),
        "pct_falso_entre_sostenidos": _pct(fr, len(sost)), "exactos": ex, "pct_exactos": _pct(ex, base),
        "precision_mediana_abs": _med(pa), "precision_mediana": _med(ps),
        "precision_p75_abs": float(np.percentile(pa, 75)) if len(pa) else _NAN,
        "recorrido_medio": _media(rec), "recorrido_mediano": _med(rec),
        "recorrido_tramo_medio": _media([e["recorrido_tramo"] for e in sost]),
        "recorrido_desde_punta_medio": _media([e["recorrido_desde_punta"] for e in sost]),
        "recorrido_total": float(rec.sum()) if len(rec) else 0.0,
        "con_opuesta": len(cop), "llega_opuesta": ll, "pct_llega_opuesta": _pct(ll, len(cop)),
        "n_noches": len({e["noche"] for e in E}), "n_noches_ventana": len(noches_ventana) if noches_ventana is not None else None,
        "n_niveles": len({round(e["raya"] * 2) / 2 for e in E}), "n_etiquetas": len(etiq),
    }


_BOOT = ("pct_sostenidos", "pct_exactos", "pct_falso_entre_sostenidos", "precision_mediana_abs", "recorrido_medio",
         "recorrido_mediano", "pct_llega_opuesta", "recorrido_total_por_noche", "llegadas_por_noche")


def bootstrap_noches(eventos, noches_ventana, n_boot=2000, semilla=20261009, tol=2.0):
    """IC90 remuestreando NOCHES (con reposicion) de noches_ventana (incluye noches sin llegadas)."""
    noches = sorted(noches_ventana)
    if not noches:
        return {}
    pos = {nn: k for k, nn in enumerate(noches)}
    E = [e for e in eventos if e["noche"] in pos]
    ni = np.array([pos[e["noche"]] for e in E], np.int64)
    sost = np.array([e["resultado"] == "sostenido" for e in E], bool)
    basev = np.array([not e["censurada"] for e in E], bool)
    fr = np.array([e["falso_rompimiento"] for e in E], bool)
    pa = np.array([e["precision_abs"] for e in E], float)
    exa = sost & (pa <= tol)
    rec = np.array([e.get("recorrido", _NAN) if e["resultado"] == "sostenido" else _NAN for e in E], float)
    cop = np.array([e["resultado"] == "sostenido" and e.get("opuesta") is not None for e in E], bool)
    lle = np.array([bool(e.get("llega_opuesta")) for e in E], bool)
    rng = np.random.default_rng(semilla)
    N = len(noches)
    out = {k: [] for k in _BOOT}
    for _ in range(n_boot):
        w = np.bincount(rng.integers(0, N, N), minlength=N).astype(float)
        we = w[ni] if len(ni) else np.zeros(0)
        b = (we * basev).sum(); s_ = (we * sost).sum()
        out["pct_sostenidos"].append(100 * s_ / b if b else _NAN)
        out["pct_exactos"].append(100 * (we * exa).sum() / b if b else _NAN)
        out["pct_falso_entre_sostenidos"].append(100 * (we * (fr & sost)).sum() / s_ if s_ else _NAN)
        rep = we.astype(int)
        pa_s = np.repeat(pa[sost], rep[sost]); re_s = np.repeat(rec[sost], rep[sost])
        out["precision_mediana_abs"].append(float(np.median(pa_s)) if len(pa_s) else _NAN)
        out["recorrido_medio"].append(float(re_s.mean()) if len(re_s) else _NAN)
        out["recorrido_mediano"].append(float(np.median(re_s)) if len(re_s) else _NAN)
        c_ = (we * cop).sum()
        out["pct_llega_opuesta"].append(100 * (we * (cop & lle)).sum() / c_ if c_ else _NAN)
        out["recorrido_total_por_noche"].append(float(np.nansum(np.where(sost, rec, 0) * we)) / N)
        out["llegadas_por_noche"].append(float(we.sum()) / N)
    return {k: (float(np.nanpercentile(v, 5)), float(np.nanpercentile(v, 95))) if np.isfinite(v).any() else (_NAN, _NAN)
            for k, v in ((k, np.array(v, float)) for k, v in out.items())}


def _noches_ventana(V):
    return sorted(set(V["noche"][V["en_v"]].tolist()))


# ------------------------------------------------------------------------------------------------------------------------------
# API publica
# ------------------------------------------------------------------------------------------------------------------------------
def juzgar(velas, rayas_por_minuto, opciones=None, _prep=None):
    """Juzga un conjunto de rayas con el criterio del operador. Ver docstring del modulo."""
    op = _op(opciones)
    V, R = _prep if _prep is not None else (None, None)
    if V is None:
        V = preparar_velas(velas, op)
        R = preparar_rayas(V, rayas_por_minuto, op)
    evs = _juzgar_core(V, R, op)
    return {"modo": op["modo"], "opciones": op, "n_pistas": len(R["pistas"]), "eventos": evs,
            "metricas": resumir(evs, _noches_ventana(V), op["tol"])}


def sensibilidad(velas, rayas_por_minuto, opciones=None):
    """Las metricas en los tres modos (ESTRICTO / TOLERANTE / MUY TOLERANTE) con las mismas rayas."""
    op = _op(opciones)
    V = preparar_velas(velas, op); R = preparar_rayas(V, rayas_por_minuto, op)
    out = {}
    for m in ("estricto", "tolerante", "muy_tolerante"):
        o2 = dict(op); o2["modo"] = m
        out[m] = resumir(_juzgar_core(V, R, o2), _noches_ventana(V), op["tol"])
    return out


def _pivotes(V, op):
    """Zigzag de umbral X sobre mechas, por tramo continuo. Pivote = punta con >= X de recorrido a cada lado."""
    X = op["umbral_giro_cobertura"]; tol = op["tol_cobertura"]
    h, l, gap = V["h"], V["l"], V["gap"]
    n = V["n"]
    seg_ini = list(np.flatnonzero(gap)) + [n]
    piv = []
    for a, b in zip(seg_ini[:-1], seg_ini[1:]):
        if b - a < 3:
            continue
        P = []                                   # (tipo, k)
        tr = 0; hi, hi_k, lo, lo_k = h[a], a, l[a], a
        for k in range(a + 1, b):
            if tr == 0:
                if h[k] > hi: hi, hi_k = h[k], k
                if l[k] < lo: lo, lo_k = l[k], k
                if hi - lo >= X:
                    if hi_k > lo_k:
                        P.append(("min", lo_k)); tr = 1
                    else:
                        P.append(("max", hi_k)); tr = -1
            elif tr == 1:
                if h[k] > hi:
                    hi, hi_k = h[k], k
                elif hi - l[k] >= X:
                    P.append(("max", hi_k)); tr = -1; lo, lo_k = l[k], k
            else:
                if l[k] < lo:
                    lo, lo_k = l[k], k
                elif h[k] - lo >= X:
                    P.append(("min", lo_k)); tr = 1; hi, hi_k = h[k], k
        # todo pivote de P ya tiene >= X a su derecha (el zigzag lo agrega recien cuando el precio recorrio X en contra);
        # a su izquierda tambien, salvo el primero del tramo, que se chequea aparte.
        pp = lambda tp_, k_: h[k_] if tp_ == "max" else l[k_]
        for q, (tp, k) in enumerate(P):
            precio = pp(tp, k)
            if q == 0:
                if tp == "max":
                    k0 = a + int(np.argmin(l[a:k + 1])); izq = precio - l[k0]
                else:
                    k0 = a + int(np.argmax(h[a:k + 1])); izq = h[k0] - precio
                if izq < X:
                    continue
            else:
                k0 = P[q - 1][1]; izq = abs(precio - pp(*P[q - 1]))
            if q + 1 < len(P):
                der = abs(precio - pp(*P[q + 1]))
            else:
                der = (precio - l[k:b].min()) if tp == "max" else (h[k:b].max() - precio)
            seg = slice(k0 + 1, k + 1)
            w = np.flatnonzero(h[seg] >= precio - tol) if tp == "max" else np.flatnonzero(l[seg] <= precio + tol)
            k_ap = (seg.start + int(w[0])) if len(w) else k
            piv.append({"tipo": tp, "k": int(k), "k_aprox": int(k_ap), "precio": float(precio), "izq": float(izq), "der": float(der)})
    piv = [p for p in piv if V["en_v"][p["k"]]]
    piv.sort(key=lambda p: p["k"])
    return piv


def _cobertura_core(V, R, piv, op, off):
    tol = op["tol_cobertura"]
    cub = 0; det = []
    for p in piv:
        pid, val = _rayas_en(R, p["k_aprox"], off, crudas=True)
        d = np.abs(val - p["precio"])
        ok = bool(len(d) and d.min() <= tol)
        cub += ok
        det.append((ok, float(val[np.argmin(d)]) if len(d) else None))
    return cub, det


def cobertura_giros(velas, rayas_por_minuto, opciones=None, _prep=None):
    """Que fraccion de los giros grandes (zigzag 20 pts) tenia una raya a +-2 de la punta YA dibujada al aproximarse el precio."""
    op = _op(opciones)
    V, R = _prep if _prep is not None else (None, None)
    if V is None:
        V = preparar_velas(velas, op); R = preparar_rayas(V, rayas_por_minuto, op)
    piv = _pivotes(V, op)
    cub, det = _cobertura_core(V, R, piv, op, np.zeros(len(R["pistas"])))
    giros = []
    for p, (ok, raya) in zip(piv, det):
        giros.append({"t": pd.Timestamp(V["t"][p["k"]]), "t_aprox": pd.Timestamp(V["t"][p["k_aprox"]]), "tipo": p["tipo"],
                      "precio": p["precio"], "izq": p["izq"], "der": p["der"], "cubierto": ok, "raya_mas_cercana": raya,
                      "noche": V["noche"][p["k"]]})
    return {"n_giros": len(piv), "cubiertos": cub, "pct": _pct(cub, len(piv)), "giros": giros}


_MET_PLACEBO = ("llegadas", "sostenidos", "pct_sostenidos", "exactos", "pct_exactos", "pct_rotas", "precision_mediana_abs",
                "recorrido_medio", "recorrido_mediano", "recorrido_tramo_medio", "recorrido_total", "pct_llega_opuesta",
                "pct_falso_entre_sostenidos")


def _offsets_azar(V, R, rng, modo):
    pistas = R["pistas"]
    d0 = np.array([p["val"][0] - V["o"][p["idx"][0]] for p in pistas], float)
    ad = np.abs(d0)
    if modo == "pool":
        nuevo = rng.choice(ad, len(ad), replace=True)
    elif modo == "propia":
        nuevo = ad.copy()
    else:
        raise KeyError(modo)
    sg = rng.choice([-1.0, 1.0], len(ad))
    return sg * nuevo - d0


def placebo(velas, rayas_por_minuto, opciones=None, corrimientos=(-31, -19, -11, 11, 19, 31), n_azar=500, semilla=20261009,
            azar_distancia="pool", con_cobertura=True, n_boot=2000, _prep=None):
    """Conjunto real contra (1) las mismas rayas corridas y (2) n_azar juegos al azar con la misma cantidad, vida y distribucion de
    distancia al precio. Devuelve percentiles del real dentro del azar y el IC90 bootstrap por noche del real."""
    op = _op(opciones)
    V, R = _prep if _prep is not None else (None, None)
    if V is None:
        V = preparar_velas(velas, op); R = preparar_rayas(V, rayas_por_minuto, op)
    nv = _noches_ventana(V)
    P = len(R["pistas"])
    ev_real = _juzgar_core(V, R, op)
    real = resumir(ev_real, nv, op["tol"])
    piv = _pivotes(V, op) if con_cobertura else []
    if con_cobertura:
        cub, _ = _cobertura_core(V, R, piv, op, np.zeros(P))
        real["cobertura_pct"] = _pct(cub, len(piv)); real["cobertura_n"] = len(piv)
    out = {"modo": op["modo"], "n_pistas": P, "real": real, "eventos_real": ev_real,
           "bootstrap_ic90": bootstrap_noches(ev_real, nv, n_boot, semilla, op["tol"]) if n_boot else {}}
    cor = {}; todos = []
    for d in corrimientos:
        e = _juzgar_core(V, R, op, np.full(P, float(d)))
        cor[d] = resumir(e, nv, op["tol"]); todos.extend(e)
        if con_cobertura:
            cub, _ = _cobertura_core(V, R, piv, op, np.full(P, float(d)))
            cor[d]["cobertura_pct"] = _pct(cub, len(piv))
    out["corridos"] = cor
    out["corridos_juntos"] = resumir(todos, nv, op["tol"])
    if con_cobertura and corrimientos:
        out["corridos_juntos"]["cobertura_pct"] = float(np.mean([cor[d]["cobertura_pct"] for d in corrimientos]))
    rng = np.random.default_rng(semilla)
    mets = list(_MET_PLACEBO) + (["cobertura_pct"] if con_cobertura else [])
    acc = {m: [] for m in mets}
    for _ in range(n_azar):
        off = _offsets_azar(V, R, rng, azar_distancia)
        r = resumir(_juzgar_core(V, R, op, off), nv, op["tol"])
        if con_cobertura:
            cub, _ = _cobertura_core(V, R, piv, op, off)
            r["cobertura_pct"] = _pct(cub, len(piv))
        for m in mets:
            acc[m].append(r[m])
    az = {}
    for m in mets:
        a = np.array(acc[m], float); a = a[np.isfinite(a)]
        x = real.get(m, _NAN)
        if len(a) and x == x:
            pc = 100.0 * ((a < x).sum() + 0.5 * (a == x).sum()) / len(a)
            az[m] = {"real": x, "p5": float(np.percentile(a, 5)), "p50": float(np.percentile(a, 50)),
                     "p95": float(np.percentile(a, 95)), "percentil": pc, "n": int(len(a))}
        else:
            az[m] = {"real": x, "p5": _NAN, "p50": _NAN, "p95": _NAN, "percentil": _NAN, "n": int(len(a))}
    out["azar"] = az
    out["azar_distancia"] = azar_distancia
    return out


def preparar(velas, rayas_por_minuto, opciones=None):
    """Prepara una vez (velas + pistas) para reusar en juzgar/cobertura_giros/placebo via _prep=(V, R)."""
    op = _op(opciones)
    V = preparar_velas(velas, op)
    return V, preparar_rayas(V, rayas_por_minuto, op)


def rayas_desde_niv20(niv20, claves=("dom0", "dom1")):
    """niv20_m1.pkl (minuto -> dict de niveles dibujados por la 2.0) -> {minuto: {clave: precio}} solo con esas claves."""
    return {t: {k: (d or {}).get(k) for k in claves} for t, d in niv20.items()}


def a_tabla(eventos):
    return pd.DataFrame(eventos)
