# -*- coding: utf-8 -*-
"""ei_conjuntos.py — COTAS DE REFERENCIA para el juez del operador (conjunto 'extremos_ideales', 09-10-2026). SOLO LECTURA.

Arma rayas_por_minuto {t: [(precio, etiqueta), ...]} con la misma convencion que niv20_m1.pkl: la clave t = lo que la herramienta
tendria dibujado AL CERRAR la vela t (usa velas <= t). El juez le aplica desfase 1 (raya vigente en t = clave t-1). Se escribe
una clave por CADA vela (lista vacia si no hay raya), asi el juez nunca arrastra una raya vieja.

CONJUNTOS (todos solo con precio de MNQZ6, sin opciones, salvo REF_2_0):
  ORACULO_TRAMO  MIRA EL FUTURO a proposito (= techo de la metrica). Puntas del zigzag de 20 pts sobre mechas, el MISMO que usa el
                 juez para la cobertura (verificado en ei_chequeos.py). La raya del giro q esta dibujada desde el giro anterior
                 (q-1) hasta el siguiente (q+1): se ve durante la llegada y durante el tramo que sigue. Nunca hay mas de 2 a la vez
                 (la de atras y la de adelante = la 'otra dominante').
  ORACULO_NOCHE  MIRA EL FUTURO: TODAS las puntas de giro de la noche (zigzag 20, velas con t en [N-2h, N+13:30)) dibujadas toda la
                 noche desde las 22:00Z. 'Se conocen los niveles, no el momento.'
  R25 / R50 / R100  numeros redondos de NQ: el multiplo de G inmediatamente debajo del cierre de la vela t y el de arriba (2 rayas).
  HOD_LOD        maximo y minimo de la sesion de CME hasta la vela t (sesion = desde las 22:00Z).
  PDH_PDL        maximo y minimo de la rueda RTH anterior (13:30Z-20:00Z, la de la etiqueta de sesion previa con >= 370 velas
                 RTH; si la previa esta incompleta, sin rayas).
  PIVOTES        soporte/resistencia clasico: el giro previo YA CONFIRMADO (zigzag 20 pts causal: un giro se conoce recien en la
                 vela en que el precio se alejo 20 pts) mas cercano por encima del cierre y el mas cercano por debajo, de las
                 ultimas 24 h.
  VWAP           VWAP de la sesion (desde las 22:00Z, precio tipico (h+l+c)/3 ponderado por volumen) y +-1 / +-2 desvios
                 (desvio = raiz de la varianza ponderada por volumen del precio tipico). 5 rayas.
  Agregados al ver que el placebo corrido NO es limpio para los conjuntos que 'encierran' al precio (R25/R50/R100, PIVOTES: al
  correrlos 31 pts las dos rayas quedan del mismo lado del precio y la cobertura del placebo cae por construccion):
  G25 / G50 / G100  la grilla COMPLETA de redondos a <= 100 pts (G100: <= 200) del cierre: correrla = otra fase, placebo limpio.
  PIVOTES_TODOS  todos los giros confirmados (zigzag 20 causal) de las ultimas 24 h, sin elegir.
  ORACULO_NOCHE40  MIRA EL FUTURO: como ORACULO_NOCHE pero solo los giros grandes (zigzag de 40 pts): menos rayas por noche.
"""
import math

import numpy as np
import pandas as pd

UMBRAL_ZZ = 20.0
HUECO_MAX = 30


def cargar_velas(ruta):
    V = pd.read_csv(ruta, parse_dates=["t"])
    V = V.dropna(subset=["o", "h", "l", "c"]).sort_values("t").drop_duplicates("t", keep="last").reset_index(drop=True)
    return V


def etiqueta_noche(t):
    """noche = fecha de (t + 2 h): la sesion de CME que abre a las 22:00Z (igual que el juez)."""
    return (pd.Series(t) + pd.Timedelta(hours=2)).dt.strftime("%Y-%m-%d").to_numpy()


def _segmentos(V):
    tm = V["t"].to_numpy().astype("datetime64[m]").astype(np.int64)
    gap = np.ones(len(V), bool)
    gap[1:] = (tm[1:] - tm[:-1]) > HUECO_MAX
    ini = list(np.flatnonzero(gap)) + [len(V)]
    return list(zip(ini[:-1], ini[1:]))


def zigzag(h, l, a, b, X=UMBRAL_ZZ):
    """El MISMO zigzag que juez_operador._pivotes (sin el filtro del primer giro ni el de ventana), pero devuelve tambien la vela
    de CONFIRMACION (la vela en que el precio ya se alejo X de la punta). -> lista de (tipo, k_punta, k_confirmacion)."""
    P = []
    if b - a < 3:
        return P
    tr = 0; hi, hi_k, lo, lo_k = h[a], a, l[a], a
    for k in range(a + 1, b):
        if tr == 0:
            if h[k] > hi: hi, hi_k = h[k], k
            if l[k] < lo: lo, lo_k = l[k], k
            if hi - lo >= X:
                if hi_k > lo_k:
                    P.append(("min", lo_k, k)); tr = 1
                else:
                    P.append(("max", hi_k, k)); tr = -1
        elif tr == 1:
            if h[k] > hi:
                hi, hi_k = h[k], k
            elif hi - l[k] >= X:
                P.append(("max", hi_k, k)); tr = -1; lo, lo_k = l[k], k
        else:
            if l[k] < lo:
                lo, lo_k = l[k], k
            elif h[k] - lo >= X:
                P.append(("min", lo_k, k)); tr = 1; hi, hi_k = h[k], k
    return P


def _vacio(V):
    return {t: [] for t in V["t"]}


def _a_dict(V, listas):
    ts = V["t"].tolist()
    return {ts[i]: listas[i] for i in range(len(ts))}


# ----------------------------------------------------------------------------------------------------------------------------
# ORACULOS (miran el futuro a proposito)
# ----------------------------------------------------------------------------------------------------------------------------
def pivotes_todos(V, X=UMBRAL_ZZ):
    h = V["h"].to_numpy(float); l = V["l"].to_numpy(float)
    out = []
    for a, b in _segmentos(V):
        P = zigzag(h, l, a, b, X)
        out.append((a, b, P))
    return out


def oraculo_tramo(V, X=UMBRAL_ZZ):
    h = V["h"].to_numpy(float); l = V["l"].to_numpy(float)
    listas = [[] for _ in range(len(V))]
    for a, b, P in pivotes_todos(V, X):
        for q, (tp, k, _kc) in enumerate(P):
            precio = h[k] if tp == "max" else l[k]
            desde = P[q - 1][1] if q > 0 else a          # clave en la vela del giro anterior = vigente desde la vela siguiente
            hasta = P[q + 1][1] - 1 if q + 1 < len(P) else b - 1
            et = "ORA_%s_%s" % (tp, V["t"].iloc[k].strftime("%m%d_%H%M"))
            for j in range(desde, hasta + 1):
                listas[j].append((float(precio), et))
    return _a_dict(V, listas)


def oraculo_noche(V, X=UMBRAL_ZZ):
    h = V["h"].to_numpy(float); l = V["l"].to_numpy(float)
    t = V["t"]
    noche = etiqueta_noche(t)
    listas = [[] for _ in range(len(V))]
    por_noche = {}
    for a, b, P in pivotes_todos(V, X):
        for tp, k, _kc in P:
            N = pd.Timestamp(noche[k])
            if not (N - pd.Timedelta(hours=2) <= t.iloc[k] < N + pd.Timedelta(hours=13, minutes=30)):
                continue
            precio = float(h[k] if tp == "max" else l[k])
            por_noche.setdefault(noche[k], []).append((precio, "ORAN_%s_%s" % (tp, t.iloc[k].strftime("%m%d_%H%M"))))
    for i in range(len(V)):
        N = pd.Timestamp(noche[i])
        if N - pd.Timedelta(hours=2) <= t.iloc[i] < N + pd.Timedelta(hours=13, minutes=30):
            listas[i] = list(por_noche.get(noche[i], []))
    return _a_dict(V, listas)


# ----------------------------------------------------------------------------------------------------------------------------
# PRECIO SOLO (causales: la clave t usa solo velas <= t)
# ----------------------------------------------------------------------------------------------------------------------------
def redondos(V, G):
    c = V["c"].to_numpy(float)
    b = np.floor(c / G) * G
    listas = [[(float(x), "R%d_%d" % (G, x)), (float(x + G), "R%d_%d" % (G, x + G))] for x in b]
    return _a_dict(V, listas)


def hod_lod(V):
    noche = etiqueta_noche(V["t"])
    df = pd.DataFrame({"n": noche, "h": V["h"].to_numpy(float), "l": V["l"].to_numpy(float)})
    hi = df.groupby("n")["h"].cummax().to_numpy(); lo = df.groupby("n")["l"].cummin().to_numpy()
    listas = [[(float(hi[i]), "HOD"), (float(lo[i]), "LOD")] for i in range(len(V))]
    return _a_dict(V, listas)


def pdh_pdl(V, min_velas_rth=370):
    t = V["t"]
    noche = etiqueta_noche(t)
    hh = V["h"].to_numpy(float); ll = V["l"].to_numpy(float)
    hora = t.dt.hour * 60 + t.dt.minute
    rth = ((hora >= 13 * 60 + 30) & (hora < 20 * 60)).to_numpy()
    # la rueda RTH de la sesion N cae el mismo dia N (13:30Z-20:00Z)
    rueda = {}
    for n in sorted(set(noche)):
        m = (noche == n) & rth & (t.dt.strftime("%Y-%m-%d").to_numpy() == n)
        if m.sum() >= min_velas_rth:
            rueda[n] = (float(hh[m].max()), float(ll[m].min()), int(m.sum()))
        else:
            rueda[n] = None
    orden = sorted(set(noche))
    previa = {n: (rueda[orden[j - 1]] if j > 0 else None) for j, n in enumerate(orden)}
    listas = []
    for i in range(len(V)):
        p = previa[noche[i]]
        listas.append([] if p is None else [(p[0], "PDH"), (p[1], "PDL")])
    return _a_dict(V, listas), {n: previa[n] for n in orden}


def pivotes_previos(V, X=UMBRAL_ZZ, memoria_h=24):
    """S/R clasico causal: giros confirmados (zigzag X) de las ultimas memoria_h horas; el mas cercano arriba y abajo del cierre."""
    h = V["h"].to_numpy(float); l = V["l"].to_numpy(float); c = V["c"].to_numpy(float)
    tm = V["t"].to_numpy().astype("datetime64[m]").astype(np.int64)
    conf = []                     # (k_confirmacion, tm_punta, precio, etiqueta)
    for a, b, P in pivotes_todos(V, X):
        for tp, k, kc in P:
            conf.append((kc, tm[k], float(h[k] if tp == "max" else l[k]), "PIV_%s_%s" % (tp, V["t"].iloc[k].strftime("%m%d_%H%M"))))
    conf.sort()
    listas = []
    vivos = []
    j = 0
    for i in range(len(V)):
        while j < len(conf) and conf[j][0] <= i:
            vivos.append(conf[j]); j += 1
        lim = tm[i] - memoria_h * 60
        vivos = [p for p in vivos if p[1] >= lim]
        arriba = [p for p in vivos if p[2] > c[i]]
        abajo = [p for p in vivos if p[2] < c[i]]
        out = []
        if arriba:
            p = min(arriba, key=lambda p: (p[2] - c[i], -p[1])); out.append((p[2], p[3]))
        if abajo:
            p = min(abajo, key=lambda p: (c[i] - p[2], -p[1])); out.append((p[2], p[3]))
        listas.append(out)
    return _a_dict(V, listas)


def vwap_bandas(V, desvios=(1.0, 2.0)):
    noche = etiqueta_noche(V["t"])
    tp = ((V["h"] + V["l"] + V["c"]) / 3.0).to_numpy(float)
    vol = V["vol"].to_numpy(float)
    df = pd.DataFrame({"n": noche, "pv": tp * vol, "p2v": tp * tp * vol, "v": vol})
    g = df.groupby("n")
    spv = g["pv"].cumsum().to_numpy(); sp2v = g["p2v"].cumsum().to_numpy(); sv = g["v"].cumsum().to_numpy()
    listas = []
    for i in range(len(V)):
        if sv[i] <= 0:
            listas.append([]); continue
        vw = spv[i] / sv[i]
        sd = math.sqrt(max(sp2v[i] / sv[i] - vw * vw, 0.0))
        out = [(vw, "VWAP")]
        for d in desvios:
            out.append((vw + d * sd, "VWAP+%gs" % d)); out.append((vw - d * sd, "VWAP-%gs" % d))
        listas.append(out)
    return _a_dict(V, listas)


def grilla(V, G, W):
    """Grilla COMPLETA de numeros redondos: todos los multiplos de G a <= W pts del cierre de la vela t. A diferencia de R25/R50/R100
    (las 2 que encierran al precio), aca correr la grilla +-11/19/31 es un placebo limpio (otra fase de la misma grilla, misma
    densidad, misma relacion con el precio): mide si los redondos son especiales o si solo 'siguen al precio'."""
    c = V["c"].to_numpy(float)
    listas = []
    for x in c:
        a = math.ceil((x - W) / G) * G
        listas.append([(float(v), "G%d_%d" % (G, v)) for v in np.arange(a, x + W + 1e-9, G)])
    return _a_dict(V, listas)


def pivotes_previos_todos(V, X=UMBRAL_ZZ, memoria_h=24):
    """Todos los giros YA confirmados (zigzag X causal) de las ultimas memoria_h horas, sin elegir: rayas fijas que nacen al confirmarse."""
    h = V["h"].to_numpy(float); l = V["l"].to_numpy(float)
    tm = V["t"].to_numpy().astype("datetime64[m]").astype(np.int64)
    conf = []
    for a, b, P in pivotes_todos(V, X):
        for tp, k, kc in P:
            conf.append((kc, tm[k], float(h[k] if tp == "max" else l[k]), "PIV_%s_%s" % (tp, V["t"].iloc[k].strftime("%m%d_%H%M"))))
    conf.sort()
    listas = []; vivos = []; j = 0
    for i in range(len(V)):
        while j < len(conf) and conf[j][0] <= i:
            vivos.append(conf[j]); j += 1
        lim = tm[i] - memoria_h * 60
        vivos = [p for p in vivos if p[1] >= lim]
        listas.append([(p[2], p[3]) for p in vivos])
    return _a_dict(V, listas)


CAUSALES = ("R25", "R50", "R100", "HOD_LOD", "PDH_PDL", "PIVOTES", "VWAP", "G25", "G50", "G100", "PIVOTES_TODOS")


def armar(V, nombre):
    if nombre == "ORACULO_TRAMO":
        return oraculo_tramo(V)
    if nombre == "ORACULO_NOCHE":
        return oraculo_noche(V)
    if nombre == "ORACULO_NOCHE40":
        return oraculo_noche(V, 40.0)
    if nombre == "G25":
        return grilla(V, 25, 100)
    if nombre == "G50":
        return grilla(V, 50, 100)
    if nombre == "G100":
        return grilla(V, 100, 200)
    if nombre == "PIVOTES_TODOS":
        return pivotes_previos_todos(V)
    if nombre == "R25":
        return redondos(V, 25)
    if nombre == "R50":
        return redondos(V, 50)
    if nombre == "R100":
        return redondos(V, 100)
    if nombre == "HOD_LOD":
        return hod_lod(V)
    if nombre == "PDH_PDL":
        return pdh_pdl(V)[0]
    if nombre == "PIVOTES":
        return pivotes_previos(V)
    if nombre == "VWAP":
        return vwap_bandas(V)
    raise KeyError(nombre)
