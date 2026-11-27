# -*- coding: utf-8 -*-
"""REGLA CONGELADA de la ronda 21: COMB-T7+V4 (27-11-2026). NO SE TOCA: su SHA-256 y el de cada pieza del pipeline estan en
PREREGISTRO.md (enmienda de congelamiento) y en regla_congelada_huellas.json. Cualquier cambio invalida la prueba.

Que es (seccion 7 de RESULTADOS.md):
- Escala (la arma el pipeline de la R19, r19_01_rasgos.py): med10 = mediana del rango RTH (9:30-16:00) de las 10 ruedas previas del
  mismo instrumento; X = 0,08 x med10 al tick; ola = zigzag de X; marea = zigzag de 4X, orden por orden desde las 18:00 NY.
- Semaforo al cierre de cada minuto m (r21_01_regimen.py): rrn = (maximo - minimo RTH hasta ese minuto) / (med10 x f(m)), con f(m) de
  curva_rango_por_minuto.json. TENDENCIA si rrn >= 1,6 y el precio del minuto esta del mismo lado del VWAP RTH y de la apertura (ese es
  el lado); si rrn >= 1,6 y los lados no coinciden: NO SE OPERA. ROTACION si 1,0 < rrn < 1,6. ESTRECHO si rrn <= 1,0: no se opera.
  Una senal en el segundo t usa el estado del ultimo minuto CERRADO (m = t // 60 - 1).
- Eventos = vueltas de la ola (r19_08_eventos.py): el segundo en que el precio volvio X desde el extremo del retroceso.
- TENDENCIA -> T7: vuelta a favor de la marea, del lado de la TENDENCIA, con el VWAP RTH detras (vwap_rth_dist_pts > 0), y al menos una
  de: extremo del retroceso en la zona de un nivel (n_niveles_en_zona >= 1), barrida de un nivel y vuelta (n_barridas_y_vuelta >= 1),
  delta de los 300 s previos a favor (of_delta_300 > 0).
- ROTACION -> V4 (= Z-C1 de la R19): extremo del retroceso entre la banda de 2 desvios (VWAP RTH o de jornada) del lado opuesto y X/2
  antes de ella, sin perforarla, + iceberg del lado que defiende a <= X/2 del extremo en los 60 s previos (F2).
- Gestion: entrada = ultimo precio del segundo de la vuelta; stop = extremo del retroceso -/+ 2 ticks; objetivo 1R pasado por 1 tick
  (columnas S6_* de la base de eventos); 10:00:00-15:29:59 NY; una posicion por vez; maximo 5 por rueda; todo cerrado 15:59:59."""
import hashlib, json, os
import numpy as np, pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
CURVA = os.path.join(AQUI, "curva_rango_por_minuto.json")
CORTE_TENDENCIA = 1.6
CORTE_ESTRECHO = 1.0
HORA_INI, HORA_FIN = 1800, 21600          # segundos desde las 9:30 (10:00:00 a 15:29:59 NY)
MAX_POR_RUEDA = 5
TICK = 0.25
BANDA_2SD = {1: ["vwr-2", "vwj-2"], -1: ["vwr+2", "vwj+2"]}


def sha256(ruta):
    h = hashlib.sha256()
    with open(ruta, "rb") as f:
        for b in iter(lambda: f.read(1 << 20), b""): h.update(b)
    return h.hexdigest()


def estado_minuto(rrn, c, vw, apertura):
    """Devuelve (tipo, lado): tipo 'T' TENDENCIA, 'R' ROTACION, 'E' ESTRECHO, 'N' no se opera (rrn >= 1,6 con lados en desacuerdo o sin dato)."""
    rrn = np.asarray(rrn, float); c = np.asarray(c, float); vw = np.asarray(vw, float); apertura = np.asarray(apertura, float)
    lvw = np.sign(c - vw); lo = np.sign(c - apertura)
    tipo = np.full(len(rrn), "N", dtype=object)
    ok = np.isfinite(rrn)
    tipo[ok & (rrn >= CORTE_TENDENCIA) & (lvw == lo) & (lvw != 0)] = "T"
    tipo[ok & (rrn > CORTE_ESTRECHO) & (rrn < CORTE_TENDENCIA)] = "R"
    tipo[ok & (rrn <= CORTE_ESTRECHO)] = "E"
    return tipo, lvw


def _pen_banda(df, nom):
    L = df["ext"].to_numpy() - df["d_" + nom].to_numpy()
    return (L - df["ext"].to_numpy()) * df["lado"].to_numpy()


def banda_respetada(df):
    X = df["X"].to_numpy(); lado = df["lado"].to_numpy(); m = np.zeros(len(df), bool)
    for s_, noms in BANDA_2SD.items():
        for nom in noms:
            pen = _pen_banda(df, nom)
            m |= (lado == s_) & np.isfinite(pen) & (pen >= (-0.5 + 1e-6) * X - 1e-9) & (pen <= 0.0 + 1e-9)
    return m


def mascaras(ev):
    t1 = ((ev["a_favor_marea"] == 1) & (ev["vwap_rth_dist_pts"] > 0)).to_numpy()
    t7 = t1 & ((ev["n_niveles_en_zona"] >= 1) | (ev["n_barridas_y_vuelta"] >= 1) | (ev["of_delta_300"] > 0)).to_numpy()
    v4 = banda_respetada(ev) & (ev["F2"] == 1).to_numpy()
    return t7, v4


def senales(ev, reg):
    """ev: vueltas de la ola (columnas de r19_08_eventos) de una o varias ruedas; reg: rasgos por minuto de r21_01_regimen (dia, m, rrn,
    c, vw, O). Devuelve TODAS las senales elegibles (antes de la regla de una posicion por vez), con su familia y estado."""
    ev = ev.sort_values(["dia", "seg_rueda", "t_utc_ms"], kind="mergesort").reset_index(drop=True)
    r = reg.set_index(["dia", "m"])
    m_prev = np.clip(ev["seg_rueda"].to_numpy() // 60 - 1, 0, 389)
    st = r.loc[list(zip(ev["dia"], m_prev)), ["rrn", "c", "vw", "O"]]
    tipo, lvw = estado_minuto(st["rrn"].to_numpy(), st["c"].to_numpy(), st["vw"].to_numpy(), st["O"].to_numpy())
    t7, v4 = mascaras(ev)
    e_t7 = t7 & (tipo == "T") & (ev["lado"].to_numpy() == lvw)
    e_v4 = v4 & (tipo == "R")
    hora = (ev["seg_rueda"] >= HORA_INI).to_numpy() & (ev["seg_rueda"] < HORA_FIN).to_numpy() & ev["S6_pts"].notna().to_numpy()
    ok = (e_t7 | e_v4) & hora
    out = ev[ok].copy()
    out["familia"] = np.where(e_t7[ok], "T7", "V4")
    out["estado"] = np.where(e_t7[ok], "TENDENCIA", "ROTACION")
    out["rrn_minuto"] = st["rrn"].to_numpy()[ok]
    return out


def operar(sen):
    """Una posicion por vez por rueda, maximo 5. Devuelve las operaciones con entrada, stop, objetivo, resultado (pts, sin costo) y motivo."""
    tomadas = []
    for dia, g in sen.groupby("dia", sort=True):
        libre = -1; n = 0
        for i, s, so in zip(g.index, g["seg_rueda"].to_numpy(), g["S6_seg_salida"].to_numpy()):
            if s <= libre or n >= MAX_POR_RUEDA: continue
            tomadas.append(i); n += 1; libre = so
    op = sen.loc[tomadas].copy()
    op["objetivo"] = op["entrada"] + op["lado"] * op["R"]
    op["resultado_pts"] = op["S6_pts"]; op["motivo"] = op["S6_motivo"]; op["seg_salida"] = op["S6_seg_salida"]
    return op


def correr(ev, reg):
    return operar(senales(ev, reg))
