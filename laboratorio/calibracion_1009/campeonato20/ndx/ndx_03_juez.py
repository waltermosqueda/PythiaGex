# -*- coding: utf-8 -*-
"""ndx_03_juez.py — campeonato20, familia NDX: cada conjunto juzgado con juez20 (SIN CAMBIOS: se importa; su archivo no se toca) y su
placebo, por ventana (noche 22:00Z-13:30Z; rueda 13:30Z-20:00Z) y por particion (MIRAR = 2/3 primeras sesiones, PRUEBA = ultimo tercio,
juez20.particion_noches). SOLO LECTURA de datos/series/*.pkl (ndx_01_series.py) y datos/velas_ndx.csv (ndx_00_velas.py).

CONJUNTOS (definidos ANTES de ver un solo resultado del juez; la lista no se cambia despues):
  primarios: MUROS_NDX_vol, MUROS_NDX_oi, MAJORS_NDX_vol, MAJORS_NDX_oi (D1 + D2), ZTP_NDX_vol (cruce arriba D1 + cruce abajo D2),
             ZEST_NDX_vol (Z), DOMS_NDX_vol (D1 + D2), R20_NDX_vol (D1 + D2).
  lados (pedido): MUROS_NDX_<f>:P        = solo la raya del muro P (D2), llegadas de los dos lados ("el muro P solo"),
                  MUROS_NDX_<f>:P_pisos  = solo la raya del muro P y SOLO las llegadas como piso (desde arriba),
                  MUROS_NDX_<f>:C_techos = solo la raya del muro C (D1) y SOLO las llegadas como techo (desde abajo);  f = vol, oi.
  Filtro por tipo de llegada: se envuelve juez20._core en ESTE proceso para quedarse con las llegadas de ese tipo, en el real, en las
  corridas, en los 500 juegos al azar y en el bootstrap por igual (las rayas, sus pistas y la densidad no cambian). El archivo del juez
  no se modifica. Sin filtro, la salida es la de juez20.evaluar tal cual.
SESIONES (criterio de DATOS, fijado antes de juzgar): una noche (o rueda) entra si tiene > 60 velas en la ventana y el libro NDX C7 de la
  4.1 existe ('ok') en >= 50 % de los minutos con vela de esa ventana. El mismo juego de sesiones para todas las series de la familia.
juez20: desfase_min = 0 (la clave t ya es lo vigente en la vela t, igual que el jsonl de la 4.1: paridad medida en ndx_02_paridad.py),
  giro 20, ventana 120 min, modo TOLERANTE, perdida_fija 5, min_velas_hora 30, placebo corridas +-11/+-19/+-31 y azar 'pool',
  semilla 20261009, bootstrap 2000 por noche.
SENSIBILIDAD fijada de antemano: las 8 primarias en las noches de MIRAR que son de CBOE (sesion >= 2026-09-08; antes la cadena NDX es
  de Databento, solo rueda, y de noche vale su ultima foto de la rueda), particion 'mirar_cboe' (corrio con 2000 juegos: main da n_mirar
  solo a la particion 'mirar').
n_azar: MIRAR 500; PRUEBA 2000 (con 500 el p por permutacion no baja de 0,002 y Holm no podria rechazar con muchas series).
Uso: python -I ndx_03_juez.py <i> <n> [n_azar_mirar] [n_azar_prueba]   (trabajos i, i+n, ...; maximo 3 procesos)
Salida: datos/juez/<conjunto>__<ventana>__<particion>.json (metricas, IC90, placebo resumido, por tipo, por noche) y _eventos.pkl."""
import ctypes
import glob
import json
import math
import os
import sys
import time
from contextlib import contextmanager

try:
    ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)   # prioridad baja: no trabar ATAS
except Exception:
    pass
os.environ.setdefault("OMP_NUM_THREADS", "1"); os.environ.setdefault("OPENBLAS_NUM_THREADS", "1")

import numpy as np  # noqa: E402
import pandas as pd  # noqa: E402

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.normpath(os.path.join(AQUI, "..", "juez20")))
import juez20 as J20  # noqa: E402

SAL = os.path.join(AQUI, "datos", "juez")
CONJUNTOS = {
    "MUROS_NDX_vol": ("MUROS_NDX_vol", ("D1", "D2"), None),
    "MUROS_NDX_oi": ("MUROS_NDX_oi", ("D1", "D2"), None),
    "MAJORS_NDX_vol": ("MAJORS_NDX_vol", ("D1", "D2"), None),
    "MAJORS_NDX_oi": ("MAJORS_NDX_oi", ("D1", "D2"), None),
    "ZTP_NDX_vol": ("ZTP_NDX_vol", ("D1", "D2"), None),
    "ZEST_NDX_vol": ("ZEST_NDX_vol", ("Z",), None),
    "DOMS_NDX_vol": ("DOMS_NDX_vol", ("D1", "D2"), None),
    "R20_NDX_vol": ("R20_NDX_vol", ("D1", "D2"), None),
    "MUROS_NDX_vol:P": ("MUROS_NDX_vol", ("D2",), None),
    "MUROS_NDX_vol:P_pisos": ("MUROS_NDX_vol", ("D2",), "piso"),
    "MUROS_NDX_vol:C_techos": ("MUROS_NDX_vol", ("D1",), "techo"),
    "MUROS_NDX_oi:P": ("MUROS_NDX_oi", ("D2",), None),
    "MUROS_NDX_oi:P_pisos": ("MUROS_NDX_oi", ("D2",), "piso"),
    "MUROS_NDX_oi:C_techos": ("MUROS_NDX_oi", ("D1",), "techo"),
}
VENTANAS = ("noche", "rueda")
PARTICIONES = ("mirar", "prueba")
PRIMARIOS = ("MUROS_NDX_vol", "MUROS_NDX_oi", "MAJORS_NDX_vol", "MAJORS_NDX_oi", "ZTP_NDX_vol", "ZEST_NDX_vol", "DOMS_NDX_vol", "R20_NDX_vol")
PRIMERA_CBOE = "2026-09-08"     # antes, el libro NDX sale de Databento (solo rueda: de noche vale su ultima foto de la rueda, congelada)
MIN_COBERTURA = 0.5
MIN_VELAS = 60


def cargar_series():
    series = {}
    metas = []
    for p in sorted(glob.glob(os.path.join(AQUI, "datos", "series", "ndx_*.pkl"))):
        x = pd.read_pickle(p)
        for s, d in x["series"].items():
            series.setdefault(s, {}).update(d)
        metas.append(x["meta"])
    return series, pd.concat(metas, ignore_index=True)


def sesiones(V, M, ventana):
    """Sesiones que entran (criterio de datos) y su cobertura."""
    tv = set(V["t"])
    M = M[M["t"].isin(tv)].copy()
    hm = M["t"].dt.strftime("%H:%M")
    if ventana == "noche":
        M = M[(hm >= "22:00") | (hm < "13:30")]
    else:
        M = M[(hm >= "13:30") & (hm < "20:00")]
    g = M.groupby("sesion").agg(velas=("t", "size"), ok=("motivo", lambda x: (x == "ok").sum()),
                                r20=("r20_motivo", lambda x: (x == "ok").sum()))
    g["cobertura"] = g["ok"] / g["velas"]
    g["cobertura_r20"] = g["r20"] / g["velas"]
    g["entra"] = (g["velas"] > MIN_VELAS) & (g["cobertura"] >= MIN_COBERTURA)
    return g


def ventanas_de(ses, ventana):
    if ventana == "noche":
        return J20.ventanas_de_noches(ses)
    return [(pd.Timestamp(d) + pd.Timedelta(hours=13.5), pd.Timestamp(d) + pd.Timedelta(hours=20)) for d in sorted(ses)]


@contextmanager
def filtro_tipo(tipo):
    """Envuelve juez20._core para quedarse con las llegadas de un tipo ('techo'|'piso'); None = sin cambios."""
    if tipo is None:
        yield
        return
    orig = J20._core

    def _core_f(V, R, op, off=None):
        return [e for e in orig(V, R, op, off) if e["tipo"] == tipo]
    J20._core = _core_f
    try:
        yield
    finally:
        J20._core = orig


def _limpio(o):
    if isinstance(o, dict):
        return {str(k): _limpio(v) for k, v in o.items()}
    if isinstance(o, (list, tuple)):
        return [_limpio(v) for v in o]
    if isinstance(o, (np.floating, float)):
        return float(o) if math.isfinite(float(o)) else None
    if isinstance(o, (np.integer,)):
        return int(o)
    if isinstance(o, (pd.Timestamp, np.datetime64)):
        return str(o)
    return o


def por_noche(evs, noches):
    out = {}
    for n in noches:
        e = [x for x in evs if x["noche"] == n]
        out[n] = {"llegadas": len(e), "aciertos": sum(x["acierto20"] for x in e), "falsas": sum(x["falsa"] for x in e),
                  "indefinidas": sum(x["resultado"] == "indefinida" and not x["censurada"] for x in e),
                  "censuradas": sum(bool(x["censurada"]) for x in e)}
    return out


def trabajo(nombre, ventana, part, V, series, ses_part, n_azar):
    serie, claves, tipo = CONJUNTOS[nombre]
    ray = series[serie]
    ven = ventanas_de(ses_part, ventana)
    ops = {"desfase_min": 0, "claves": claves}
    t0 = time.time()
    prep = J20.preparar(V, ray, ven, ops)
    with filtro_tipo(tipo):
        res = J20.evaluar(V, ray, ventanas=ven, opciones=ops, n_azar=n_azar, _prep=prep)
    dt = time.time() - t0
    evs = res["eventos"]
    m = res["metricas"]; pl = res["placebo"]
    # desglose por tipo de llegada (solo descriptivo, sin placebo)
    tipos = {}
    for tp in ("techo", "piso"):
        e = [x for x in evs if x["tipo"] == tp]
        r = J20.resumir20(e, res["noches_ventana"], 5.0)
        tipos[tp] = {k: r[k] for k in ("llegadas", "base", "aciertos", "falsas", "indefinidas", "pct_acierto20", "pct_acierto20_decididas",
                                       "recorrido_mediano_acierto", "precision_mediana_abs", "expectativa_pts")}
    az = pl["azar"]
    out = {"conjunto": nombre, "serie": serie, "claves": claves, "tipo_llegada": tipo, "ventana": ventana, "particion": part,
           "sesiones": sorted(ses_part), "n_sesiones": len(ses_part), "n_azar": n_azar, "tiempo_s": dt, "n_pistas": res["n_pistas"],
           "metricas": m, "bootstrap_ic90": res["bootstrap_ic90"],
           "placebo": {"tasa_base_azar": pl["tasa_base_azar"], "tasa_base_corridas": pl["tasa_base_corridas"],
                       "corridos_pct_acierto20": {str(d): pl["corridos"][d]["pct_acierto20"] for d in pl["corridos"]},
                       "corridos_juntos": {k: pl["corridos_juntos"].get(k) for k in ("llegadas", "base", "aciertos", "falsas",
                                                                                      "pct_acierto20", "pct_falsas", "expectativa_pts",
                                                                                      "aciertos_por_noche", "falsas_por_noche")},
                       "azar": az},
           "por_tipo": tipos, "por_noche": por_noche(evs, res["noches_ventana"])}
    base = os.path.join(SAL, "%s__%s__%s" % (nombre.replace(":", "-"), ventana, part))
    with open(base + ".json", "w", encoding="utf-8") as fh:
        json.dump(_limpio(out), fh, ensure_ascii=False, indent=1)
    pd.to_pickle(evs, base + "_eventos.pkl")
    a = az["pct_acierto20"]
    print("%-24s %-5s %-6s n=%2d llegadas %4d acierto20 %5.1f%% (azar p50 %5.1f, corr %5.1f) pctl %5.1f p %.4f pN %.4f | falsas/n %.2f "
          "aciertos/n %.2f rayas/h %.1f | %.0f s" % (
              nombre, ventana, part, len(ses_part), m["llegadas"], m["pct_acierto20"] if m["pct_acierto20"] == m["pct_acierto20"] else -1,
              pl["tasa_base_azar"] if pl["tasa_base_azar"] == pl["tasa_base_azar"] else -1,
              pl["tasa_base_corridas"] if pl["tasa_base_corridas"] == pl["tasa_base_corridas"] else -1,
              a["percentil"] if a["percentil"] == a["percentil"] else -1, a["p_valor"] if a["p_valor"] == a["p_valor"] else -1,
              a["p_normal"] if a["p_normal"] == a["p_normal"] else -1,
              m["falsas_por_noche"], m["aciertos_por_noche"], m["rayas_por_hora"] if m["rayas_por_hora"] == m["rayas_por_hora"] else -1, dt),
          flush=True)


def main():
    i, n = int(sys.argv[1]), int(sys.argv[2])
    n_mirar = int(sys.argv[3]) if len(sys.argv) > 3 else 500
    n_prueba = int(sys.argv[4]) if len(sys.argv) > 4 else 2000
    os.makedirs(SAL, exist_ok=True)
    V = pd.read_csv(os.path.join(AQUI, "datos", "velas_ndx.csv"), parse_dates=["t"])[["t", "o", "h", "l", "c"]]
    series, M = cargar_series()
    parts = {}
    for ven in VENTANAS:
        g = sesiones(V, M, ven)
        ses = sorted(g.index[g["entra"]])
        mirar, prueba = J20.particion_noches(ses)
        parts[ven] = {"mirar": mirar, "prueba": prueba, "mirar_cboe": [x for x in mirar if x >= PRIMERA_CBOE]}
        if i == 0:
            g.to_csv(os.path.join(SAL, "sesiones_%s.csv" % ven))
    trabajos = [(c, v, p) for v in VENTANAS for p in PARTICIONES for c in CONJUNTOS]
    trabajos += [(c, "noche", "mirar_cboe") for c in PRIMARIOS]
    # los de PRUEBA (2000 juegos) primero repartidos, para equilibrar
    trabajos.sort(key=lambda x: (x[2] != "prueba", x[1], x[0]))
    mios = trabajos[i::n]
    print("trabajos %d de %d | noche mirar %d prueba %d | rueda mirar %d prueba %d" % (
        len(mios), len(trabajos), len(parts["noche"]["mirar"]), len(parts["noche"]["prueba"]), len(parts["rueda"]["mirar"]),
        len(parts["rueda"]["prueba"])), flush=True)
    for c, v, p in mios:
        trabajo(c, v, p, V, series, parts[v][p], n_mirar if p == "mirar" else n_prueba)


if __name__ == "__main__":
    main()
