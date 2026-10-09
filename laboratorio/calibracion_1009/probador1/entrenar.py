# -*- coding: utf-8 -*-
"""
probador1/entrenar.py — ENTRENAMIENTO del grupo 1 (PREREGISTRO sec. 6-8). Solo sesiones <= 2026-09-30.
  1. Niveles por minuto de C01..C06 (+ C01s 'dos_log', sensibilidad informativa fuera de la familia) -> probadores/grupo1/<ID>/niveles_ENTRENAMIENTO.parquet
  2. chequeo_futuro (detector de mirar adelante) de cada una.
  3. evaluar(niveles, dias, nombre=ID, n_azar=500, n_boot=2000) -> resultados/entrenamiento/<ID>.json
  4. seleccionar_finalistas por ventana, loo_parametro, mitades -> resultados/entrenamiento/seleccion.json
Uso: python -I entrenar.py [niveles|evaluar|seleccion|todo]
"""
import hashlib
import json
import os
import sys
import time
from concurrent.futures import ProcessPoolExecutor

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import candidatas as C                      # noqa: E402

E = C.E
D = C.D
DIAS_ENTRENAMIENTO = ["2026-08-17", "2026-08-18", "2026-08-19", "2026-08-20", "2026-08-21", "2026-08-24", "2026-08-25", "2026-08-26",
                      "2026-08-27", "2026-08-28", "2026-08-31", "2026-09-01", "2026-09-02", "2026-09-03", "2026-09-04", "2026-09-07",
                      "2026-09-08", "2026-09-09", "2026-09-10", "2026-09-11", "2026-09-14", "2026-09-15", "2026-09-16", "2026-09-17",
                      "2026-09-18", "2026-09-21", "2026-09-22", "2026-09-23", "2026-09-24", "2026-09-25", "2026-09-28", "2026-09-29",
                      "2026-09-30"]
assert all(E.fase_de(d) == "ENTRENAMIENTO" for d in DIAS_ENTRENAMIENTO)
IDS = ["C01", "C02", "C03", "C04", "C05", "C06", "C01_doslog"]
RES = os.path.join(AQUI, "resultados", "entrenamiento")


def dias_de(cid):
    """ENTRENAMIENTO = todas las <= 09-30 donde exista el libro de la candidata (PREREGISTRO sec. 6)."""
    return [d for d in DIAS_ENTRENAMIENTO if d in D.dias(C.LIBRO[cid])]


def sha(p):
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for b in iter(lambda: f.read(1 << 20), b""):
            h.update(b)
    return h.hexdigest()


def ruta_niveles(cid, fase):
    return os.path.join(C.SALIDA, C.NOMBRES[cid], "niveles_%s.parquet" % fase)


def paso_niveles():
    t0 = time.time()
    dias_q = dias_de("C01"); dias_n = dias_de("C02")
    print("sesiones QQQ:", dias_q, "\nsesiones NDX:", dias_n, flush=True)
    todos = sorted(set(dias_q) | set(dias_n))
    res, diags = C.construir(todos, "ENTRENAMIENTO", IDS)
    resumen = {}
    for cid in IDS:
        df = res[cid]
        ds = dias_de(cid)
        df = df[df["dia"].isin(ds)] if len(df) else df
        cf = E.chequeo_futuro(df, ds) if len(df) else {}
        resumen[cid] = {"filas": len(df), "minutos": int(df["t"].nunique()) if len(df) else 0, "sesiones": ds,
                        "strikes_distintos": int(df["K"].nunique()) if len(df) else 0, "chequeo_futuro": cf,
                        "libro_dom": df["libro_dom"].value_counts().to_dict() if len(df) else {},
                        "sha256_niveles": sha(ruta_niveles(cid, "ENTRENAMIENTO"))}
        print(cid, json.dumps(resumen[cid], default=str), flush=True)
    os.makedirs(RES, exist_ok=True)
    with open(os.path.join(RES, "niveles_resumen.json"), "w", encoding="utf-8") as f:
        json.dump({"resumen": resumen, "diag": diags, "interpretaciones": C.INTERPRETACIONES, "segundos": round(time.time() - t0, 1)},
                  f, ensure_ascii=False, indent=1, default=str)


def _uno(cid):
    t0 = time.time()
    df = pd.read_parquet(ruta_niveles(cid, "ENTRENAMIENTO"))
    r = E.evaluar(df, dias_de(cid), nombre=C.NOMBRES[cid], n_azar=500, n_boot=2000)
    r["meta"]["niveles_sha256"] = sha(ruta_niveles(cid, "ENTRENAMIENTO"))
    r["meta"]["candidatas_py_sha256"] = sha(os.path.join(AQUI, "candidatas.py"))
    p = os.path.join(RES, "%s.json" % C.NOMBRES[cid])
    with open(p, "w", encoding="utf-8") as f:
        json.dump(E.limpiar_para_json(r), f, ensure_ascii=False, indent=1)
    return cid, round(time.time() - t0, 1)


def paso_evaluar(ids=IDS, workers=3):
    os.makedirs(RES, exist_ok=True)
    with ProcessPoolExecutor(max_workers=workers) as ex:
        for cid, s in ex.map(_uno, ids):
            print("evaluada", cid, s, "s", flush=True)


def _uno_m2(cid):
    """Sensibilidad del PREREGISTRO sec. 2 (velas de 2 min, marco=2): informativa, solo modo tolerante, n_azar=200."""
    t0 = time.time()
    df = pd.read_parquet(ruta_niveles(cid, "ENTRENAMIENTO"))
    r = E.evaluar(df, dias_de(cid), nombre=C.NOMBRES[cid] + "_m2", modos=("tolerante",), n_azar=200, n_boot=1000, marco=2)
    with open(os.path.join(RES, "%s_m2.json" % C.NOMBRES[cid]), "w", encoding="utf-8") as f:
        json.dump(E.limpiar_para_json(r), f, ensure_ascii=False, indent=1)
    return cid, round(time.time() - t0, 1)


def paso_m2(ids=("C01", "C02", "C03", "C04", "C06"), workers=3):
    with ProcessPoolExecutor(max_workers=workers) as ex:
        for cid, s in ex.map(_uno_m2, ids):
            print("m2 evaluada", cid, s, "s", flush=True)
    out = {}
    for cid in ids:
        r = json.load(open(os.path.join(RES, "%s_m2.json" % C.NOMBRES[cid]), encoding="utf-8"))
        out[C.NOMBRES[cid]] = {w: {k: r["operador"]["tolerante"][w]["pct_sostenidos"].get(k) for k in
                                   ("real", "corridos", "azar_media", "ventaja_vs_corridos", "ventaja_vs_azar", "p_azar")}
                               | {"base": r["operador"]["tolerante"][w]["muestra"]["base"]} for w in E.VENTANAS}
        print(C.NOMBRES[cid], json.dumps(out[C.NOMBRES[cid]]))
    with open(os.path.join(RES, "sensibilidad_m2.json"), "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=1)


def cargar_res(cid):
    with open(os.path.join(RES, "%s.json" % C.NOMBRES[cid]), encoding="utf-8") as f:
        return json.load(f)


def paso_seleccion():
    fam = {C.NOMBRES[c]: cargar_res(c) for c in ("C01", "C02", "C03", "C04", "C05", "C06")}
    out = {"generado_utc": pd.Timestamp.now("UTC").isoformat(timespec="seconds"), "regla": "PREREGISTRO sec. 8 (evaluar.seleccionar_finalistas)",
           "nota": "seleccion DENTRO del grupo 1 (6 de las 24); el tope global de 3 finalistas por ventana lo decide quien junte los 4 grupos",
           "ventanas": {}}
    for w in E.VENTANAS:
        sel = E.seleccionar_finalistas(fam, w)
        no_ctrl = {k: v for k, v in fam.items() if E._rol(k) != "control"}
        loo = E.loo_parametro(no_ctrl, w)
        loo_todas = E.loo_parametro(fam, w)
        out["ventanas"][w] = {"seleccion": sel, "loo_sin_controles": loo, "loo_con_control_C05": loo_todas,
                              "resumen": {k: _fila(v, w) for k, v in fam.items()}}
    out["sensibilidad_C01_doslog"] = {w: _fila(cargar_res("C01_doslog"), w) for w in E.VENTANAS}
    with open(os.path.join(RES, "seleccion.json"), "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=1)
    for w in E.VENTANAS:
        print(w, "finalistas:", out["ventanas"][w]["seleccion"]["finalistas"], "| loo:", out["ventanas"][w]["loo_sin_controles"])
        for k, v in out["ventanas"][w]["resumen"].items():
            print("  ", k, json.dumps(v))
    return out


def _fila(r, w):
    t = r["operador"]["tolerante"][w]
    s = t["pct_sostenidos"]; m = t["muestra"]
    g = lambda d, k: d.get(k) if d else None
    return {"base": m["base"], "llegadas": m["llegadas"], "censuradas": m["censuradas"], "strikes": m["strikes_distintos"],
            "niveles5": m["niveles_distintos_5pts"], "sesiones_con_llegadas": m["sesiones_con_llegadas"],
            "M1": s.get("real"), "corridos": s.get("corridos"), "azar": s.get("azar_media"), "v_corr": s.get("ventaja_vs_corridos"),
            "v_azar": s.get("ventaja_vs_azar"), "p_azar": s.get("p_azar"), "ic95_corr": s.get("ic95_vs_corridos"),
            "ic90_inf_corr": s.get("ic90_inf_vs_corridos"), "ic95_azar": s.get("ic95_vs_azar"),
            "M2_rec_lleg": t["recorrido_por_llegada"].get("real"), "M2_corr": t["recorrido_por_llegada"].get("corridos"),
            "M2_azar": t["recorrido_por_llegada"].get("azar_media"),
            "falsos_pct": t["pct_falso_entre_sostenidos"].get("real"), "exactos_pct": t["pct_exactos"].get("real"),
            "rotas_pct": t["pct_rotas"].get("real"), "llega_opuesta": t["pct_llega_opuesta"].get("real"),
            "estricto_v_corr": r["operador"].get("estricto", {}).get(w, {}).get("pct_sostenidos", {}).get("ventaja_vs_corridos"),
            "muy_tol_v_corr": r["operador"].get("muy_tolerante", {}).get(w, {}).get("pct_sostenidos", {}).get("ventaja_vs_corridos"),
            "cob20": g(r["cobertura"].get(w), "cobertura20") and r["cobertura"][w]["cobertura20"].get("real"),
            "cob20_lift_azar": g(r["cobertura"].get(w), "cobertura20") and r["cobertura"][w]["cobertura20"].get("lift_vs_azar"),
            "toque_rebote": r["toque"].get(w, {}).get("pct_rebote", {}).get("real"),
            "toque_rebote_azar": r["toque"].get(w, {}).get("pct_rebote", {}).get("azar_media"),
            "toques": r["toque"].get(w, {}).get("toques"),
            "eco_dist": r["eco"].get(w, {}).get("dist_mediana_al_precio"), "eco_beta": r["eco"].get(w, {}).get("beta_pista_15"),
            "mitades": E.mitades(r, w)}


if __name__ == "__main__":
    que = sys.argv[1] if len(sys.argv) > 1 else "todo"
    if que in ("niveles", "todo"):
        paso_niveles()
    if que in ("evaluar", "todo"):
        paso_evaluar()
    if que in ("seleccion", "todo"):
        paso_seleccion()
    if que in ("m2", "todo"):
        paso_m2()
