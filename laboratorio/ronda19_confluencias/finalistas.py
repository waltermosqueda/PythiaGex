# -*- coding: utf-8 -*-
"""Candidatas al arbitraje de la ronda 19 (definiciones EXACTAS, copiadas de cada explorador) y las finalistas elegidas con la REGLA
DEL ARBITRAJE de PREREGISTRO.md (como mucho 4). Cada candidata: base ('vueltas' o 'cruces'), mascara sobre columnas de la base,
salida pre-registrada, horario de entradas (segundos desde las 9:30) y sus numeros de DISENO declarados por el explorador.
La salida 'S6mc' es la operacion CONTRARIA a la vuelta calculada desde la cinta (r19_16_fade.py)."""
import numpy as np

TK = 0.25


def _seg(h):
    if not isinstance(h, str) or h.count(":") != 2: return np.nan
    a, b, c = h.split(":"); return (int(a) - 9) * 3600 + (int(b) - 30) * 60 + int(c)


# ---------------------------------------------------------------- familia f (agente principal)
def e4(df):
    """E4 'vuelta al valor con flujo': vuelta de la ola a favor de la marea con agresores; la marea nacio de una extension fuera del ruido
    del otro lado; VWAP RTH a >= 1R del lado de la ganancia; absorcion del lado que defiende (F1) o big trades netos a favor (F6) en 60 s."""
    return ((df["a_favor_marea"] == 1) & (df["marea_con_agresores"] == 1) & (df["extension_fuera_ruido"] == 1)
            & (-df["vwap_rth_dist_pts"] >= df["R"]) & ((df["F1"] == 1) | (df["F6"] == 1))).to_numpy()


def e5b(df):
    """E5b formula minima: vuelta a favor de la marea + VWAP RTH a >= 1R del lado de la ganancia + (F1 o F6)."""
    return ((df["a_favor_marea"] == 1) & (-df["vwap_rth_dist_pts"] >= df["R"]) & ((df["F1"] == 1) | (df["F6"] == 1))).to_numpy()


# ---------------------------------------------------------------- explorador ZONAS (z09_candidatas.py, zonas_comun.py)
BANDA_2SD = {1: ["vwr-2", "vwj-2"], -1: ["vwr+2", "vwj+2"]}     # banda de 2 desvios del lado OPUESTO (opera hacia el VWAP)


def _pen_banda(df, nom):
    L = df["ext"].to_numpy() - df["d_" + nom].to_numpy()
    return (L - df["ext"].to_numpy()) * df["lado"].to_numpy()      # > 0: el extremo paso la banda


def banda_respetada(df):
    """extremo entre X/2 antes de la banda de 2 sd (RTH o jornada) del lado opuesto y la banda, sin perforarla: pen en (-X/2, 0]."""
    X = df["X"].to_numpy(); lado = df["lado"].to_numpy(); m = np.zeros(len(df), bool)
    for s_, noms in BANDA_2SD.items():
        for nom in noms:
            pen = _pen_banda(df, nom)
            m |= (lado == s_) & np.isfinite(pen) & (pen >= (-0.5 + 1e-6) * X - 1e-9) & (pen <= 0.0 + 1e-9)
    return m


def banda_toque(df):
    """extremo a +-T (T = X/8 al tick, minimo 2 ticks; columna zona_T) de la banda de 2 sd (RTH o jornada) del lado opuesto."""
    T = df["zona_T"].to_numpy(); lado = df["lado"].to_numpy(); m = np.zeros(len(df), bool)
    for s_, noms in BANDA_2SD.items():
        for nom in noms:
            pen = _pen_banda(df, nom)
            m |= (lado == s_) & np.isfinite(pen) & (pen >= -T - 1e-9) & (pen <= T + 1e-9)
    return m


def z_c1(df): return banda_respetada(df) & (df["F2"] == 1).to_numpy()
def z_c2(df): return banda_toque(df)
def z_c3(df): return z_c1(df) & ((-df["vwap_rth_dist_pts"] / df["R"]) >= 1.5).to_numpy()
def z_c4(df): return z_c1(df) & (df["seg_rueda"] >= 3600).to_numpy()


# ---------------------------------------------------------------- explorador DELTA (d11_juicio.py, d_lib.py): FADE de la vuelta
def rapida(df):
    """la vuelta tardo <= 107 s desde el segundo del extremo (tercio fijado antes de ver resultados)."""
    return ((df["seg_rueda"] - df["hora_ext"].map(_seg)) <= 107).to_numpy()


def d_w6(df):
    return rapida(df) & ((df["of_apilados_favor_3v"] >= 1) | ((df["F3"] == 1) & (df["n_niveles_en_zona"] >= 1))).to_numpy()
def d_w1(df):
    return rapida(df) & ((df["of_apilados_favor_3v"] >= 1) | (df["ctx_ret_con_agresores"] == 0)).to_numpy()
def d_a2(df): return rapida(df) & (df["ctx_ret_con_agresores"] == 0).to_numpy()
def d_a3(df): return rapida(df) & ((df["F3"] == 1) & (df["n_niveles_en_zona"] >= 1)).to_numpy()


# ---------------------------------------------------------------- explorador MODELO (explorador_modelo/candidatas_modelo.py)
def _netbig_rel(df):
    esc = ((df["of_u_netbig60_p80"] - df["of_u_netbig60_p20"]) / 2.0).to_numpy()
    v = df["of_netbig60_a_favor"].to_numpy()
    return np.where(esc > 0, v / np.where(esc > 0, esc, 1.0), np.nan)


def m_a(df):
    """MODELO A 'vuelta al valor con grandes a favor': precio a mas de 1,076 desvios del VWAP de la JORNADA con el VWAP del lado de la
    ganancia + grandes netos a favor > 1,339 veces su escala ((p80 - p20)/2 de la franja) en 60 s + pierna de la marea <= 8,75 X."""
    nb = _netbig_rel(df)
    return ((df["vwap_jornada_dist_sd"].to_numpy() * df["lado"].to_numpy() <= -1.076)
            & np.nan_to_num(nb > 1.339, nan=False) & (df["ctx_pierna_marea_X"].to_numpy() <= 8.75))


def m_b(df):
    """MODELO B 'vuelta al valor profunda': retroceso > 2,234 X desde el extremo de la marea y VWAP RTH a >= 4,846 X a favor."""
    return ((df["ctx_prof_X"].to_numpy() > 2.234) & (df["vwap_rth_dist_pts"].to_numpy() / df["X"].to_numpy() <= -4.846))


H = dict(hora_ini=1800, hora_fin=21600)
CANDIDATAS = [
    dict(nombre="E4-S6", familia="f", base="vueltas", mascara=e4, salida="S6", **H, diseno=dict(n=36, acierto=58.3, neto=0.153, t=0.20)),
    dict(nombre="E4-S1", familia="f", base="vueltas", mascara=e4, salida="S1", **H, diseno=dict(n=30, acierto=50.0, neto=1.608, t=1.29)),
    dict(nombre="E5b-S1", familia="f", base="vueltas", mascara=e5b, salida="S1", **H, diseno=dict(n=36, acierto=47.2, neto=1.188, t=1.09)),
    dict(nombre="Z-C1-S6", familia="a ZONAS", base="vueltas", mascara=z_c1, salida="S6", **H, diseno=dict(n=70, acierto=70.0, neto=1.364, t=2.339)),
    dict(nombre="Z-C1-S1", familia="a ZONAS", base="vueltas", mascara=z_c1, salida="S1", **H, diseno=dict(n=69, acierto=49.3, neto=1.656, t=2.052)),
    dict(nombre="Z-C2-S6", familia="a ZONAS", base="vueltas", mascara=z_c2, salida="S6", **H, diseno=dict(n=47, acierto=70.2, neto=1.35, t=2.20)),
    dict(nombre="Z-C3-S6", familia="a ZONAS", base="vueltas", mascara=z_c3, salida="S6", **H, diseno=dict(n=56, acierto=73.2, neto=1.67, t=2.43)),
    dict(nombre="Z-C4-S6", familia="a ZONAS", base="vueltas", mascara=z_c4, salida="S6", **H, diseno=dict(n=44, acierto=75.0, neto=1.85, t=2.73)),
    dict(nombre="Z-C4-S1", familia="a ZONAS", base="vueltas", mascara=z_c4, salida="S1", **H, diseno=dict(n=None, acierto=56.8, neto=2.70, t=2.45)),
    dict(nombre="D-w6-S6m", familia="e DELTA", base="vueltas", mascara=d_w6, salida="S6mc", **H, diseno=dict(n=78, acierto=70.5, neto=1.22, t=2.27)),
    dict(nombre="D-w1-S6m", familia="e DELTA", base="vueltas", mascara=d_w1, salida="S6mc", **H, diseno=dict(n=79, acierto=70.9, neto=1.03, t=2.02)),
    dict(nombre="D-a2-S6m", familia="e DELTA", base="vueltas", mascara=d_a2, salida="S6mc", **H, diseno=dict(n=48, acierto=68.8, neto=0.82, t=1.96)),
    dict(nombre="D-a3-S6m", familia="e DELTA", base="vueltas", mascara=d_a3, salida="S6mc", **H, diseno=dict(n=44, acierto=68.2, neto=1.02, t=2.16)),
    # MODELO: numeros declarados = procedimiento FUERA DE PLIEGUE (los honestos); horario: toda la rueda
    dict(nombre="M-A-S6", familia="MODELO", base="vueltas", mascara=m_a, salida="S6", hora_ini=0, hora_fin=23400,
         diseno=dict(n=38, acierto=68.4, neto=1.16, t=1.87)),
    dict(nombre="M-B-S1", familia="MODELO", base="vueltas", mascara=m_b, salida="S1", hora_ini=0, hora_fin=23400,
         diseno=dict(n=22, acierto=54.5, neto=1.45, t=0.94)),
]

# ARBITRAJE FINAL (PREREGISTRO.md, 27-11 04:25 AR): k = 4
FINALISTAS = [c for c in CANDIDATAS if c["nombre"] in ("Z-C1-S6", "D-w6-S6m", "E4-S6", "M-A-S6")]
assert len(FINALISTAS) == 4
