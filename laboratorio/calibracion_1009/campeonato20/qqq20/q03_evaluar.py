# -*- coding: utf-8 -*-
"""q03_evaluar.py — campeonato20, familia QQQ / 2.0: juzga cada serie con juez20 (SIN cambiarlo) por ventana (noche / rueda) y por
particion (mirar / prueba). SOLO LECTURA de datos; escribe en datos/res/.

Sesiones de cada ventana = las que tienen >= 60 minutos con libro en esa ventana:
  QQQ (las 7 de la 4.1 y R20_QQQ): minutos 'ok' del libro QQQ de la 4.1 (q01 meta). La misma lista para todas, asi se comparan en las
    mismas noches (R20 tiene minutos el 24-09, pero ese dia no hay cadenas de QQQ: se deja afuera para no comparar noches distintas).
  TQQQ (T_*): minutos con alguna raya T_* en la rueda.
Particion: juez20.particion_noches sobre esa lista (PRUEBA = el ultimo ceil(n/3) en orden de fecha; nada se elige mirandola).
Ventanas: noche 22:00Z vispera -> 13:30Z (ventanas_de_noches por defecto); rueda 13:30Z -> 20:00Z (desde_h 13,5, hasta_h 20).
Placebo: el de juez20 (corridas +-11/19/31 y n_azar juegos al azar 'pool'); bootstrap por noche 2000 (IC90).
Uso: python -I q03_evaluar.py SERIE:VENTANA:PARTICION:N_AZAR [...]   (p.ej. MUROS_QQQ_oi:noche:mirar:500)"""
import ctypes
try:
    ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)   # prioridad baja: no trabar ATAS
except Exception:
    pass
import json
import os
import sys
import time

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.normpath(os.path.join(AQUI, "..", "juez20")))
import juez20 as J20  # noqa: E402

RES = os.path.join(AQUI, "datos", "res")
TQQQ = ("T_DOMS_vol", "T_MUROS_vol", "T_MUROS_oi", "T_ZERO_oi", "T_DOMS_raz")
VENT = {"noche": (-2.0, 13.5), "rueda": (13.5, 20.0)}


def _en(t, v):
    h = t.hour + t.minute / 60.0
    return (13.5 <= h < 20.0) if v == "rueda" else not (13.5 <= h < 20.0)


def sesiones(ventana, tq, Q, T):
    if tq:
        cnt = {}
        for ser in TQQQ:
            for t, d in T["series"][ser].items():
                if d and _en(t, ventana):
                    s = (t + pd.Timedelta(hours=2)).strftime("%Y-%m-%d")
                    cnt.setdefault(s, set()).add(t)
        return sorted(s for s, v in cnt.items() if len(v) >= 60)
    M = Q["meta"]
    m = M[(M["motivo"] == "ok") & M["t"].map(lambda t: _en(t, ventana))]
    c = m.groupby("sesion").size()
    return sorted(c[c >= 60].index.tolist())


def resumen_placebo(pl):
    o = {"tasa_base_azar": pl.get("tasa_base_azar"), "tasa_base_corridas": pl.get("tasa_base_corridas"), "n_azar": pl.get("n_azar")}
    o["azar"] = pl.get("azar")
    cj = pl.get("corridos_juntos") or {}
    o["corridos_juntos"] = {k: cj.get(k) for k in ("llegadas", "base", "aciertos", "falsas", "indefinidas", "pct_acierto20", "aciertos_por_noche",
                                                     "falsas_por_noche", "expectativa_pts", "rayas_por_hora")} if cj else None
    o["corridos"] = {k: {kk: v.get(kk) for kk in ("llegadas", "base", "pct_acierto20", "aciertos_por_noche", "falsas_por_noche", "expectativa_pts")}
                     for k, v in (pl.get("corridos") or {}).items()}
    return o


def main():
    trabajos = sys.argv[1:]
    V = pd.read_csv(os.path.join(AQUI, "datos", "velas_snapshot.csv"), parse_dates=["t"])
    Q = pd.read_pickle(os.path.join(AQUI, "datos", "rayas_qqq20.pkl"))
    T = pd.read_pickle(os.path.join(AQUI, "datos", "rayas_tqqq20.pkl")) if any(x.split(":")[0] in TQQQ for x in trabajos) else None
    G = pd.read_pickle(os.path.join(AQUI, "datos", "rayas_grilla20.pkl")) if any(x.split(":")[0] == "GRILLA_QQQ" for x in trabajos) else None
    os.makedirs(RES, exist_ok=True)
    for tr in trabajos:
        ser, ven, part, na = tr.split(":")
        na = int(na)
        tq = ser in TQQQ
        P = T if tq else (G if ser == "GRILLA_QQQ" else Q)
        R = P["series"][ser]
        ses = sesiones(ven, tq, Q, T)
        mirar, prueba = J20.particion_noches(ses)
        noches = mirar if part == "mirar" else prueba
        a, b = VENT[ven]
        vent = J20.ventanas_de_noches(noches, a, b)
        t0 = time.time()
        res = J20.evaluar(V, R, ventanas=vent, opciones={"desfase_min": P["desfase"][ser]}, n_azar=na)
        dt = time.time() - t0
        m = res["metricas"]
        # cobertura: minutos de la ventana con alguna raya y strikes/etiquetas distintas
        mins = tot = 0; etqs = set()
        for (x0, x1) in vent:
            for t in pd.date_range(x0, x1, freq="1min", inclusive="left"):
                if t > V["t"].max():
                    break
                tot += 1
                d = R.get(t)
                if d:
                    mins += 1
                    for v in d.values():
                        etqs.add(v[1] if isinstance(v, tuple) else None)
        ev = pd.DataFrame(res["eventos"])
        cols = [c for c in ("t_llegada", "tipo", "raya", "censurada", "motivo", "precision", "resultado", "acierto20", "falsa", "dist_rotura", "pts_desc", "mfe", "t_decision", "nombres",
                            "etiquetas", "noche") if c in ev.columns]
        nom = "%s__%s__%s" % (ser, ven, part)
        if len(ev):
            ev[cols].to_csv(os.path.join(RES, nom + "__eventos.csv"), index=False)
        outd = {"serie": ser, "ventana": ven, "particion": part, "n_azar": na, "desfase_min": P["desfase"][ser], "tiempo_s": dt,
                "sesiones_ventana": ses, "mirar": mirar, "prueba": prueba, "noches_evaluadas": noches, "noches_ventana": res["noches_ventana"],
                "n_pistas": res["n_pistas"], "velas": res["velas"], "cobertura_min": mins, "minutos_ventana": tot,
                "etiquetas_distintas": len(etqs - {None}), "metricas": m, "bootstrap_ic90": res["bootstrap_ic90"],
                "placebo": resumen_placebo(res.get("placebo") or {})}
        with open(os.path.join(RES, nom + ".json"), "w", encoding="utf-8") as fh:
            json.dump(outd, fh, ensure_ascii=False, indent=1, default=str)
        az = outd["placebo"]["azar"]["pct_acierto20"]
        print("%-14s %-5s %-6s n_azar %4d | %5.1f s | llegadas %4d base %4d | ACIERTO20 %5.1f %% (azar p50 %5.1f, corridas %5.1f) pct %5.1f p %.4f "
              "pN %.4f | acier/n %.2f falsas/n %.2f | rayas/h %.2f" % (
                  ser, ven, part, na, dt, m["llegadas"], m["base"], m["pct_acierto20"], outd["placebo"]["tasa_base_azar"],
                  outd["placebo"]["tasa_base_corridas"], az["percentil"], az["p_valor"], az["p_normal"], m["aciertos_por_noche"],
                  m["falsas_por_noche"], m["rayas_por_hora"]), flush=True)


if __name__ == "__main__":
    main()
