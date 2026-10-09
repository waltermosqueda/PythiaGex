# -*- coding: utf-8 -*-
"""nq02_juez.py — FAMILIA NQ del campeonato20: cada serie juzgada con juez20 (ACIERTO20, TOLERANTE, 120 min) contra su placebo,
ENTRENAMIENTO (mirar) / PRUEBA por noches. SOLO LECTURA de datos; escribe solo en datos/.

==================================================================================================================================
PRE-REGISTRO DE LA FAMILIA (escrito el 09-10-2026 ~06:15 UTC, ANTES de correr este script; ningun ACIERTO20 de estas series se
calculo antes. No se cambia despues de ver resultados; cualquier cambio posterior va en DESVIOS con hora y motivo.)
----------------------------------------------------------------------------------------------------------------------------------
SERIES (12, rayas de nq01_rayas.py, paridad con la 4.1 de esta noche: 100 % en MUROS/MAJORS/ZTP, 98 % en ZEST con <= 0,11 pts):
  MUROS_NQ_vol (C y P), MUROS_NQ_vol.C (solo muro de calls), MUROS_NQ_vol.P (solo muro de puts), lo mismo por OI (3),
  MAJORS_NQ_vol, MAJORS_NQ_oi, ZTP_NQ_vol, ZEST_NQ_vol, ZEST_NQ_oi, DOMS_NQ_vol (seleccion de la 2.0 sobre el libro NQ de la 4.1).
  Clave k = vigente en la vela k (fut = cierre de la ultima m2 cerrada antes de k): desfase_min = 0 (como la 4.1).
VENTANAS: noche = [sesion - 2 h, sesion + 13,5 h) (22:00Z -> 13:30Z); rueda = [sesion + 13,5 h, sesion + 20 h) (13:30Z -> 20:00Z).
  Dentro de cada una, SOLO los minutos con libro NQ vigente (series por volumen) o con libro y OI valido (C2, series por OI), en
  tramos (huecos <= 10 min se unen): asi las rayas por hora miden la densidad cuando la serie existe. Las rayas fuera de esos tramos
  se borran (clave presente y vacia: no se arrastran).
NOCHES / RUEDAS ELEGIBLES: las sesiones con >= 60 minutos de libro NQ (volumen) en esa ventana. Particion de juez20
  (particion_noches): PRUEBA = ultimo ceil(n/3) en orden de fecha; aparte para noche y para rueda. Velas: las del dataset unificado
  (contrato frente: U6 hasta la sesion 09-14, Z6 desde 09-15) + el centinela 'hoy' de la 2.0 para la noche en curso (10-09).
JUEZ: juez20.evaluar sin cambios (n_azar 500, corridas +-11/+-19/+-31, bootstrap 2000 por noche, semilla 20261009, azar 'pool').
CANDIDATAS (se eligen SOLO con MIRAR, por ventana): base >= 30 Y percentil del pct_acierto20 contra el azar >= 95 (p por permutacion
  <= 0,05 sin corregir) Y pct_acierto20 > tasa base de las corridas juntas.
CONFIRMACION EN PRUEBA (solo candidatas): mismo juez con n_azar = 5000; confirma si p_Holm (Holm sobre las candidatas de esta familia,
  las dos ventanas juntas) <= 0,05 Y pct_acierto20 > tasa base de las corridas en PRUEBA. Ademas se informan TODAS las series en PRUEBA
  con 500 juegos, solo como descripcion (su p no entra en la confirmacion de la familia; el Holm del campeonato lo hace el orquestador).
DESCRIPTIVOS (no deciden): rayas por hora, aciertos/falsas por noche y por "noche equivalente" (= por 15,5 h cubiertas de noche o
  6,5 h de rueda: corrige la cobertura parcial del libro), recorrido mediano, precision, expectativa en pts por llegada (+20 / -rotura).
==================================================================================================================================
DESVIOS: (ninguno todavia)

Uso: python -I nq02_juez.py [--fase 1|2|todo]   (3 procesos, prioridad baja)
Salidas: datos/juez_nq.json (todo), datos/juez_nq_tabla.csv, datos/juez_nq.txt, datos/eventos/<serie>__<ventana>__<parte>.csv,
         datos/particion.json."""
import ctypes
import json
import math
import os
import sys
import time
from multiprocessing import Pool

try:
    ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)
except Exception:
    pass

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import nq_comun as C  # noqa: E402

sys.path.insert(0, C.J20_DIR)
import juez20 as J20  # noqa: E402

VENT = {"noche": (-2.0, 13.5), "rueda": (13.5, 20.0)}
HORAS_VENT = {"noche": 15.5, "rueda": 6.5}
MIN_COB = 60
GAP_UNION = 10
N_AZAR, N_CONF = 500, 5000
EVD = os.path.join(C.DATOS, "eventos")

_G = {}


def _prio():
    try:
        ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)
    except Exception:
        pass


def cargar():
    V = pd.read_csv(os.path.join(C.DATOS, "velas_m1_nq.csv"), parse_dates=["t"])
    info = json.load(open(os.path.join(C.DATOS, "nq01_info.json"), encoding="utf-8"))
    reg = {d: pd.read_pickle(os.path.join(C.DATOS, "registros-%s.pkl" % d)) for d in info["sesiones"]}
    return V, reg


def _init():
    _prio()
    V, reg = cargar()
    _G["V"] = V; _G["reg"] = reg
    _G["tv"] = set(V["t"].to_numpy().astype("datetime64[m]").astype(np.int64).tolist())


def cobertura(reg, dia, vtipo, fuente):
    """minutos (enteros) de la ventana vtipo de la sesion dia con libro (y OI valido si fuente == 'oi')."""
    a, b = VENT[vtipo]
    d = pd.Timestamp(dia)
    ka = int((d + pd.Timedelta(hours=a)).value // 60_000_000_000); kb = int((d + pd.Timedelta(hours=b)).value // 60_000_000_000)
    return [r["k"] for r in reg[dia] if ka <= r["k"] < kb and r["libro"] and (fuente == "vol" or r["oi_ok"])]


def tramos(ks):
    if not ks:
        return []
    ks = sorted(ks); out = [[ks[0], ks[0]]]
    for k in ks[1:]:
        if k - out[-1][1] <= GAP_UNION:
            out[-1][1] = k
        else:
            out.append([k, k])
    return [(pd.Timestamp(a * 60, unit="s"), pd.Timestamp((b + 1) * 60, unit="s")) for a, b in out]


def particion(reg):
    out = {}
    for vt in VENT:
        eleg = [d for d in sorted(reg) if len(cobertura(reg, d, vt, "vol")) >= MIN_COB]
        mirar, prueba = J20.particion_noches(eleg)
        out[vt] = dict(elegibles=eleg, mirar=mirar, prueba=prueba,
                       excluidas=[d for d in sorted(reg) if d not in eleg],
                       minutos_vol={d: len(cobertura(reg, d, vt, "vol")) for d in sorted(reg)},
                       minutos_oi={d: len(cobertura(reg, d, vt, "oi")) for d in sorted(reg)})
    return out


def _f(x):
    try:
        x = float(x)
        return x if math.isfinite(x) else None
    except Exception:
        return None


def trabajo(args):
    serie, vt, parte, dias, n_azar = args
    t0 = time.time()
    reg = _G["reg"]; V = _G["V"]; tv = _G["tv"]
    fuente = C.FUENTE_SERIE[serie]
    vent = []; cub = set()
    for d in dias:
        ks = cobertura(reg, d, vt, fuente)
        vent += tramos(ks)
    for a, b in vent:
        ka = int(a.value // 60_000_000_000); kb = int(b.value // 60_000_000_000)
        cub.update(range(ka, kb))
    rayas = {}
    for d in dias:
        rs = C.rayas_de_serie(reg[d], serie)
        for t, v in rs.items():
            rayas[t] = v if int(t.value // 60_000_000_000) in cub else {}
    horas = sum(1 for k in cub if k in tv) / 60.0
    if not vent:
        return dict(serie=serie, ventana=vt, parte=parte, n_azar=n_azar, dias=dias, horas_cubiertas=0.0, vacio=True)
    res = J20.evaluar(V, rayas, ventanas=vent, opciones={"desfase_min": 0}, n_azar=n_azar)
    ev = J20.a_tabla(res["eventos"])
    os.makedirs(EVD, exist_ok=True)
    if len(ev):
        ev.drop(columns=[c for c in ev.columns if c.startswith("_")], errors="ignore").to_csv(
            os.path.join(EVD, "%s__%s__%s%s.csv" % (serie, vt, parte, "" if n_azar == N_AZAR else "_n%d" % n_azar)), index=False)
    m = res["metricas"]; pl = res.get("placebo", {}); az = pl.get("azar", {})
    por_noche = {}
    for e in res["eventos"]:
        p = por_noche.setdefault(e["noche"], dict(llegadas=0, aciertos=0, falsas=0, indefinidas=0, censuradas=0))
        p["llegadas"] += 1; p["aciertos"] += int(e["acierto20"]); p["falsas"] += int(e["falsa"])
        p["censuradas"] += int(bool(e["censurada"])); p["indefinidas"] += int(e["resultado"] == "indefinida" and not e["censurada"])
    eq = HORAS_VENT[vt] / horas if horas > 0 else float("nan")
    out = dict(serie=serie, ventana=vt, parte=parte, n_azar=n_azar, dias=dias, noches_ventana=res["noches_ventana"],
               horas_cubiertas=round(horas, 2), tramos=len(vent), n_pistas=res["n_pistas"],
               metricas={k: (_f(v) if not isinstance(v, (int, np.integer)) else int(v)) for k, v in m.items()},
               aciertos_por_noche_equiv=_f(m["aciertos"] * eq), falsas_por_noche_equiv=_f(m["falsas"] * eq),
               llegadas_por_noche_equiv=_f(m["llegadas"] * eq),
               ic90={k: [_f(a), _f(b)] for k, (a, b) in res["bootstrap_ic90"].items()},
               tasa_base_azar=_f(pl.get("tasa_base_azar")), tasa_base_corridas=_f(pl.get("tasa_base_corridas")),
               corridas={str(d): dict(pct_acierto20=_f(r["pct_acierto20"]), llegadas=int(r["llegadas"]), expectativa_pts=_f(r["expectativa_pts"]))
                         for d, r in pl.get("corridos", {}).items()},
               corridas_juntas=dict(pct_acierto20=_f(pl.get("corridos_juntos", {}).get("pct_acierto20")),
                                    expectativa_pts=_f(pl.get("corridos_juntos", {}).get("expectativa_pts"))),
               azar={k: {kk: _f(vv) for kk, vv in d.items() if kk != "mejor"} for k, d in az.items()},
               por_noche=por_noche, seg=round(time.time() - t0, 1))
    return out


def tabla(resultados):
    filas = []
    for r in resultados:
        if r.get("vacio"):
            filas.append(dict(serie=r["serie"], ventana=r["ventana"], parte=r["parte"], n_azar=r["n_azar"], horas=0)); continue
        m = r["metricas"]; a = r["azar"].get("pct_acierto20", {}); ic = r["ic90"]
        filas.append(dict(
            serie=r["serie"], ventana=r["ventana"], parte=r["parte"], n_azar=r["n_azar"], noches=len(r["noches_ventana"]),
            horas=r["horas_cubiertas"], llegadas=m["llegadas"], base=m["base"], censuradas=m["censuradas"], aciertos=m["aciertos"],
            falsas=m["falsas"], indefinidas=m["indefinidas"], pct_acierto20=m["pct_acierto20"],
            ic90_lo=(ic.get("pct_acierto20") or [None, None])[0], ic90_hi=(ic.get("pct_acierto20") or [None, None])[1],
            pct_decididas=m["pct_acierto20_decididas"], azar_p50=a.get("p50"), azar_p5=a.get("p5"), azar_p95=a.get("p95"),
            corridas=r["corridas_juntas"]["pct_acierto20"], percentil=a.get("percentil"), p_perm=a.get("p_valor"), z=a.get("z"),
            p_normal=a.get("p_normal"), aciertos_noche=m["aciertos_por_noche"], falsas_noche=m["falsas_por_noche"],
            aciertos_noche_eq=r["aciertos_por_noche_equiv"], falsas_noche_eq=r["falsas_por_noche_equiv"],
            rayas_hora=m["rayas_por_hora"], rayas_hora_p90=m["rayas_por_hora_p90"], simultaneas=m["rayas_simultaneas_mediana"],
            recorrido_med=m["recorrido_mediano_acierto"], min_a_acierto=m["min_a_acierto_mediano"], precision_abs=m["precision_mediana_abs"],
            precision=m["precision_mediana"], falsos_romp=m["falsos_rompimientos"], dist_rotura=m["dist_rotura_mediana"],
            expectativa=m["expectativa_pts"], expectativa_azar_p50=(r["azar"].get("expectativa_pts") or {}).get("p50"),
            expectativa_percentil=(r["azar"].get("expectativa_pts") or {}).get("percentil"),
            expectativa_fija=m["expectativa_pts_fija"], expectativa_base=m["expectativa_pts_base"], pistas=r["n_pistas"]))
    return pd.DataFrame(filas)


def candidatas(T):
    M = T[(T["parte"] == "mirar") & (T["n_azar"] == N_AZAR)]
    c = M[(M["base"] >= 30) & (M["percentil"] >= 95) & (M["pct_acierto20"] > M["corridas"])]
    return [(r.serie, r.ventana) for r in c.itertuples()]


def main():
    fase = sys.argv[sys.argv.index("--fase") + 1] if "--fase" in sys.argv else "todo"
    t0 = time.time()
    V, reg = cargar()
    P = particion(reg)
    with open(os.path.join(C.DATOS, "particion.json"), "w", encoding="utf-8") as fh:
        json.dump(P, fh, ensure_ascii=False, indent=1)
    for vt in VENT:
        print("%s: elegibles %d (mirar %d: %s..%s | prueba %d: %s..%s); excluidas %s" % (
            vt, len(P[vt]["elegibles"]), len(P[vt]["mirar"]), P[vt]["mirar"][0], P[vt]["mirar"][-1], len(P[vt]["prueba"]),
            P[vt]["prueba"][0], P[vt]["prueba"][-1], P[vt]["excluidas"]), flush=True)
    ruta = os.path.join(C.DATOS, "juez_nq.json")
    previos = json.load(open(ruta, encoding="utf-8"))["resultados"] if os.path.exists(ruta) and fase == "2" else []
    resultados = list(previos)
    if fase in ("1", "todo"):
        jobs = [(s, vt, parte, P[vt][parte], N_AZAR) for s in C.SERIES for vt in VENT for parte in ("mirar", "prueba")]
        with Pool(3, initializer=_init) as pool:
            for r in pool.imap_unordered(trabajo, jobs):
                resultados.append(r)
                m = r.get("metricas") or {}
                print("%-15s %-5s %-6s llegadas %4s acierto20 %5s %% | azar p50 %5s | percentil %5s | rayas/h %5s | %ss" % (
                    r["serie"], r["ventana"], r["parte"], m.get("llegadas"), None if m.get("pct_acierto20") is None else round(m["pct_acierto20"], 1),
                    None if r.get("tasa_base_azar") is None else round(r["tasa_base_azar"], 1),
                    None if not r.get("azar") else round(r["azar"]["pct_acierto20"]["percentil"] or -1, 1),
                    None if m.get("rayas_por_hora") is None else round(m["rayas_por_hora"], 2), r.get("seg")), flush=True)
    T = tabla(resultados)
    cand = candidatas(T)
    print("CANDIDATAS (solo MIRAR, regla pre-registrada): %s" % cand, flush=True)
    if fase in ("2", "todo") and cand:
        jobs = [(s, vt, "prueba", P[vt]["prueba"], N_CONF) for s, vt in cand]
        with Pool(min(3, len(jobs)), initializer=_init) as pool:
            for r in pool.imap_unordered(trabajo, jobs):
                resultados.append(r)
                print("CONFIRMACION %s %s: acierto20 %.1f %% percentil %.2f p %.5f" % (
                    r["serie"], r["ventana"], r["metricas"]["pct_acierto20"], r["azar"]["pct_acierto20"]["percentil"],
                    r["azar"]["pct_acierto20"]["p_valor"]), flush=True)
        T = tabla(resultados)
    # Holm: (a) confirmacion de la familia (candidatas, PRUEBA, n_conf); (b) descriptivo: todas las series x ventana en MIRAR y en PRUEBA (500)
    holm = {}
    C2 = T[(T["parte"] == "prueba") & (T["n_azar"] == N_CONF)]
    if len(C2):
        holm["confirmacion"] = J20.holm({"%s|%s" % (r.serie, r.ventana): r.p_perm for r in C2.itertuples()})
    for parte in ("mirar", "prueba"):
        X = T[(T["parte"] == parte) & (T["n_azar"] == N_AZAR) & T["p_perm"].notna()]
        holm["todas_%s_perm" % parte] = J20.holm({"%s|%s" % (r.serie, r.ventana): r.p_perm for r in X.itertuples()})
        holm["todas_%s_normal" % parte] = J20.holm({"%s|%s" % (r.serie, r.ventana): r.p_normal for r in X.itertuples()})
    T.to_csv(os.path.join(C.DATOS, "juez_nq_tabla.csv"), index=False)
    with open(ruta, "w", encoding="utf-8") as fh:
        json.dump(dict(particion={vt: {k: P[vt][k] for k in ("elegibles", "mirar", "prueba", "excluidas")} for vt in P},
                       candidatas=cand, holm=holm, resultados=resultados, seg=round(time.time() - t0, 1)),
                  fh, ensure_ascii=False, indent=1, default=str)
    print("listo %.0f s" % (time.time() - t0))


if __name__ == "__main__":
    main()
