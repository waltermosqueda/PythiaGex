# -*- coding: utf-8 -*-
"""
evaluar_grupo2.py — corre el ARNES (../arnes/evaluar.py v1.0, sin cambios) sobre C07-C12 (grupo2.py), en el orden del PREREGISTRO:
  python -I evaluar_grupo2.py entrenamiento   -> resultados/ENTRENAMIENTO_<ID>.json + resultados/resumen_ENTRENAMIENTO.json
                                                (n_azar=500, n_boot=2000; chequeo_futuro; seleccionar_finalistas, LOO y mitades)
  python -I evaluar_grupo2.py sellar          -> finalistas_grupo2.json (sello: finalistas, referencias, parametros congelados, hora,
                                                sha256 de los resultados de ENTRENAMIENTO y del codigo) + su sha256
  python -I evaluar_grupo2.py prueba          -> genera niveles de PRUEBA con el codigo congelado y abre PRUEBA con el sello
                                                (n_azar=2000, n_boot=2000) -> resultados/PRUEBA_<ID>.json + resultados/resumen_PRUEBA.json
No cambia el PREREGISTRO ni el arnes. Ninguna candidata del grupo 2 tiene parametros libres (PREREGISTRO sec. 6 (3)).
"""
import os
import sys
import json
import hashlib
import pickle
import datetime as dt

os.environ.setdefault("OMP_NUM_THREADS", "1")
os.environ.setdefault("OPENBLAS_NUM_THREADS", "1")
os.environ.setdefault("MKL_NUM_THREADS", "1")

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import grupo2 as G          # noqa: E402
EV = G.EV
RES = os.path.join(AQUI, "resultados")
os.makedirs(RES, exist_ok=True)
SELLO = os.path.join(AQUI, "finalistas_grupo2.json")
ORDEN = ("C07", "C08", "C09", "C10", "C11", "C12")


def sha(p):
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for b in iter(lambda: f.read(1 << 20), b""):
            h.update(b)
    return h.hexdigest()


def ruta_niv(c, fase):
    return os.path.join(AQUI, "niveles", G.IDS[c], "niveles_%s.parquet" % fase)


def _eval_uno(args):
    c, fase, n_azar, abrir = args
    niv = pd.read_parquet(ruta_niv(c, fase))
    dias = G.dias_de(c, fase)
    kw = dict(nombre=G.IDS[c], n_azar=n_azar, n_boot=2000)
    if abrir:
        kw.update(abrir_prueba=True, sello=SELLO)
    res = EV.evaluar(niv, dias, **kw)
    res["meta"]["chequeo_futuro"] = EV.chequeo_futuro(niv, dias)
    res["meta"]["niveles_parquet"] = ruta_niv(c, fase)
    res["meta"]["niveles_sha256"] = sha(ruta_niv(c, fase))
    res["meta"]["filas"] = int(len(niv))
    with open(os.path.join(RES, "%s_%s.pkl" % (fase, G.IDS[c])), "wb") as f:
        pickle.dump(res, f)
    lim = EV.limpiar_para_json(res)
    p = os.path.join(RES, "%s_%s.json" % (fase, G.IDS[c]))
    with open(p, "w", encoding="utf-8") as f:
        json.dump(lim, f, ensure_ascii=False, indent=1)
    return c, p


def correr(fase, n_azar, abrir=False, workers=3):
    from concurrent.futures import ProcessPoolExecutor
    with ProcessPoolExecutor(max_workers=workers) as ex:
        out = dict(ex.map(_eval_uno, [(c, fase, n_azar, abrir) for c in ORDEN]))
    return out


def cargar_res(fase):
    out = {}
    for c in ORDEN:
        p = os.path.join(RES, "%s_%s.pkl" % (fase, G.IDS[c]))
        with open(p, "rb") as f:
            out[G.IDS[c]] = pickle.load(f)
    return out


def _nz(x):
    return float("nan") if x is None else x


def fila_resumen(r, w):
    t = r["operador"]["tolerante"][w]
    s = t["pct_sostenidos"]; m = t["muestra"]
    est = r["operador"].get("estricto", {}).get(w, {}).get("pct_sostenidos", {})
    mt = r["operador"].get("muy_tolerante", {}).get(w, {}).get("pct_sostenidos", {})
    cob = r["cobertura"].get(w, {})
    tq = r["toque"].get(w, {})
    eco = r["eco"].get(w, {})
    return {
        "M1_real": s.get("real"), "M1_corridos": s.get("corridos"), "M1_azar_media": s.get("azar_media"),
        "azar_p5": s.get("azar_p5"), "azar_p95": s.get("azar_p95"),
        "ventaja_vs_corridos": s.get("ventaja_vs_corridos"), "ic95_vs_corridos": s.get("ic95_vs_corridos"),
        "ic90_inf_vs_corridos": s.get("ic90_inf_vs_corridos"), "ventaja_vs_azar": s.get("ventaja_vs_azar"),
        "ic95_vs_azar": s.get("ic95_vs_azar"), "p_azar": s.get("p_azar"),
        "corridos_por_desplazamiento": s.get("corridos_por_desplazamiento"),
        "estricto_ventaja_vs_corridos": est.get("ventaja_vs_corridos"), "muy_tolerante_ventaja_vs_corridos": mt.get("ventaja_vs_corridos"),
        "M2_rec_por_llegada": t["recorrido_por_llegada"].get("real"), "M2_corridos": t["recorrido_por_llegada"].get("corridos"),
        "M2_azar": t["recorrido_por_llegada"].get("azar_media"),
        "recorrido_medio": t["recorrido_medio"].get("real"), "pct_rotas": t["pct_rotas"].get("real"),
        "pct_exactos": t["pct_exactos"].get("real"), "pct_exactos_corridos": t["pct_exactos"].get("corridos"),
        "pct_exactos_azar": t["pct_exactos"].get("azar_media"),
        "pct_falso_entre_sost": t["pct_falso_entre_sostenidos"].get("real"),
        "pct_falso_corridos": t["pct_falso_entre_sostenidos"].get("corridos"),
        "pct_llega_opuesta": t["pct_llega_opuesta"].get("real"), "pct_llega_opuesta_corridos": t["pct_llega_opuesta"].get("corridos"),
        "cob20_real": cob.get("cobertura20", {}).get("real"), "cob20_azar": cob.get("cobertura20", {}).get("azar_media"),
        "cob20_lift_azar": cob.get("cobertura20", {}).get("lift_vs_azar"), "cob20_p_azar": cob.get("cobertura20", {}).get("p_azar"),
        "cob40_real": cob.get("cobertura40", {}).get("real"), "cob40_lift_azar": cob.get("cobertura40", {}).get("lift_vs_azar"),
        "toque_n": tq.get("toques"), "toque_rebote": tq.get("pct_rebote", {}).get("real"),
        "toque_rebote_azar": tq.get("pct_rebote", {}).get("azar_media"), "toque_rebote_corridos": tq.get("pct_rebote", {}).get("corridos"),
        "toque_extremo": tq.get("pct_extremo", {}).get("real"), "toque_extremo_azar": tq.get("pct_extremo", {}).get("azar_media"),
        "toque_extremo_corridos": tq.get("pct_extremo", {}).get("corridos"),
        "toque_giro15": tq.get("pct_giro15", {}).get("real"), "toque_giro15_azar": tq.get("pct_giro15", {}).get("azar_media"),
        "eco_dist_mediana": eco.get("dist_mediana_al_precio"), "eco_beta": eco.get("beta_pista_15"),
        "eco_minutos_con_rayas": eco.get("minutos_con_rayas"), "eco_pistas_por_hora": eco.get("pistas_nuevas_por_hora"),
        "llegadas": m["llegadas"], "base": m["base"], "censuradas": m["censuradas"], "sostenidos": s.get("num"),
        "sesiones_con_llegadas": m["sesiones_con_llegadas"], "sesiones_con_3": m["sesiones_con_3_o_mas"],
        "sesiones_ganadas": m["sesiones_ganadas"], "strikes_distintos": m["strikes_distintos"],
        "niveles_5pts": m["niveles_distintos_5pts"], "medianas": t["medianas"], "por_sesion": m["por_sesion"],
        "estratos": r["estratos"].get(w)}


def entrenamiento():
    rutas = correr("ENTRENAMIENTO", 500)
    R = cargar_res("ENTRENAMIENTO")
    resumen = {"generado_utc": dt.datetime.now(dt.timezone.utc).isoformat(timespec="seconds"), "candidatas": {}, "seleccion": {},
               "loo": {}, "sha256_resultados": {k: sha(v) for k, v in rutas.items()}}
    for nom, r in R.items():
        resumen["candidatas"][nom] = {"dias": r["meta"]["dias"], "chequeo_futuro": r["meta"]["chequeo_futuro"], "filas": r["meta"]["filas"],
                                      "segundos": r["meta"]["segundos"],
                                      "ventanas": {w: fila_resumen(r, w) for w in EV.VENTANAS}}
    for w in EV.VENTANAS:
        resumen["seleccion"][w] = EV.seleccionar_finalistas(R, w)
        resumen["loo"][w] = EV.loo_parametro(R, w)
    with open(os.path.join(RES, "resumen_ENTRENAMIENTO.json"), "w", encoding="utf-8") as f:
        json.dump(EV.limpiar_para_json(resumen), f, ensure_ascii=False, indent=1)
    imprimir(resumen)


def imprimir(resumen):
    f = lambda x, d=1: "  -  " if x is None or (isinstance(x, float) and x != x) else ("%." + str(d) + "f") % x
    for nom, c in resumen["candidatas"].items():
        for w, x in c["ventanas"].items():
            print("%-24s %-5s base %4s sost %5s%% corr %5s%% azar %5s%% (p5 %s p95 %s) v_corr %6s IC95 [%s, %s] v_azar %6s p %s | M2 %s/%s | "
                  "cob20 %s lift %s | strikes %s niv5 %s ses %s gan %s/%s | dist %s beta %s | estr %s muyt %s | toque %s reb %s/%s" % (
                      nom, w, x["base"], f(x["M1_real"]), f(x["M1_corridos"]), f(x["M1_azar_media"]), f(x["azar_p5"]), f(x["azar_p95"]),
                      f(x["ventaja_vs_corridos"]), f((x["ic95_vs_corridos"] or [None])[0]), f((x["ic95_vs_corridos"] or [None, None])[1]),
                      f(x["ventaja_vs_azar"]), f(x["p_azar"], 3), f(x["M2_rec_por_llegada"]), f(x["M2_corridos"]), f(x["cob20_real"]),
                      f(x["cob20_lift_azar"], 2), x["strikes_distintos"], x["niveles_5pts"], x["sesiones_con_llegadas"], x["sesiones_ganadas"],
                      x["sesiones_con_3"], f(x["eco_dist_mediana"]), f(x["eco_beta"], 2), f(x["estricto_ventaja_vs_corridos"]),
                      f(x["muy_tolerante_ventaja_vs_corridos"]), x["toque_n"], f(x["toque_rebote"]), f(x["toque_rebote_azar"])), flush=True)


def sellar():
    if os.path.exists(SELLO):
        raise SystemExit("el sello ya existe: no se reescribe (%s)" % SELLO)
    with open(os.path.join(RES, "resumen_ENTRENAMIENTO.json"), encoding="utf-8") as f:
        rs = json.load(f)
    sel = {w: rs["seleccion"][w]["finalistas"] for w in EV.VENTANAS}
    s = {"grupo": 2, "probador": "probador2",
         "hora_utc": dt.datetime.now(dt.timezone.utc).isoformat(timespec="seconds"),
         "hora_art": (dt.datetime.now(dt.timezone.utc) - dt.timedelta(hours=3)).strftime("%Y-%m-%d %H:%M:%S ART"),
         "nota": ("Sello del GRUPO 2 (no es el arnes/finalistas.json comun: cada probador escribe en su subcarpeta). Las 6 candidatas "
                  "del grupo son REFERENCIAS (PREREGISTRO sec. 8.3: siempre van a PRUEBA) y no tienen parametros libres (sec. 6 (3)); "
                  "la seleccion de abajo es la regla 8.1-8.2 aplicada SOLO a este grupo, informativa."),
         "finalistas_por_ventana_regla_8_solo_grupo2": sel,
         "referencias_a_prueba": [G.IDS[c] for c in ORDEN],
         "parametros_congelados": {
             "comun": "PREREGISTRO 7.0: grilla 1 min, F = evaluar.minutos_y_precio, foto/conversion D.conversion(...,'cuatro'), Hoy con "
                      "envejecimiento, r=0,0375 q=0, M CBOE 100 / NQ 20, R = min(0,02 F, 100), empates al menor K",
             "C07": "MAJORS QQQ OI: argmax GEX_oi>0 / argmin GEX_oi<0 en R; etiquetas D1/D2; K = strike QQQ",
             "C08": "MUROS QQQ OI: argmax GC_oi>0 / argmin GP_oi<0 en R; D1/D2; K = strike QQQ",
             "C09": "MUROS NDX vol: argmax GC_vol>0 / argmin GP_vol<0 en R; base 'cuatro'; D1/D2; K = strike NDX",
             "C10": "ZEST QQQ vol: grilla c0=round(S) +-7,5 USD paso 0,05, cruce mas cercano a S interpolado, exacto cada minuto (sin cache "
                    "por cubos), x rho; |Z-F|<=300; etiqueta Z; sin K",
             "C11": "MUROS NQ OI Black-76, Fut = K + corr; OI con fecha: salto >=50 % de contratos comunes (>=20) entre 22:00 y 03:30 UTC, "
                    "lunes y 2026-09-08 sin supresion, sin salto suprimido hasta 02:00 UTC; D1/D2; K = strike del libro (en 09-16..09-18 "
                    "viene corrido por el spread U6->Z6, como lo dibujaba el indicador)",
             "C12": "FAM MUROS OI: minutos con NQ, NDX y QQQ vigentes y OI de NQ valido; |Fut-F|<=3 % por libro; multiplicador FINAL "
                    "NQ 20 / NDX y QQQ 100 USD/pt (backtest_familia C1); np.round(Fut/5)*5 y suma; argmax GC / argmin GP en R; sin K"},
         "arnes": {"evaluar_py_sha256": sha(os.path.join(G.RAIZ, "arnes", "evaluar.py")), "juez_sha256": sha(os.path.join(G.RAIZ, "arnes", "juez_operador_v1.py")),
                   "preregistro_sha256": sha(os.path.join(G.RAIZ, "PREREGISTRO.md"))},
         "codigo": {n: sha(os.path.join(AQUI, n)) for n in ("grupo2.py", "evaluar_grupo2.py", "paridad_41.py")},
         "resultados_entrenamiento_sha256": {os.path.basename(p): sha(os.path.join(RES, p)) for p in sorted(os.listdir(RES)) if p.startswith("ENTRENAMIENTO_") or p == "resumen_ENTRENAMIENTO.json"},
         "prueba": {"dias": list(EV.PRUEBA), "n_azar": 2000, "n_boot": 2000, "modos": list(EV.MODOS), "marco": 1}}
    with open(SELLO, "w", encoding="utf-8") as f:
        json.dump(s, f, ensure_ascii=False, indent=1)
    h = sha(SELLO)
    with open(SELLO.replace(".json", ".sha256"), "w", encoding="utf-8") as f:
        f.write("%s  finalistas_grupo2.json\n" % h)
    print("sello", SELLO, h)


def prueba():
    if not os.path.exists(SELLO):
        raise SystemExit("sin sello")
    s = json.load(open(SELLO, encoding="utf-8"))
    for n, h in s["codigo"].items():
        if sha(os.path.join(AQUI, n)) != h:
            raise SystemExit("el codigo %s cambio despues del sello" % n)
    G.generar("PRUEBA")
    rutas = correr("PRUEBA", 2000, abrir=True)
    R = cargar_res("PRUEBA")
    resumen = {"generado_utc": dt.datetime.now(dt.timezone.utc).isoformat(timespec="seconds"), "sello_sha256": sha(SELLO),
               "candidatas": {}, "sha256_resultados": {k: sha(v) for k, v in rutas.items()}}
    for nom, r in R.items():
        resumen["candidatas"][nom] = {"dias": r["meta"]["dias"], "chequeo_futuro": r["meta"]["chequeo_futuro"], "filas": r["meta"]["filas"],
                                      "ventanas": {w: fila_resumen(r, w) for w in EV.VENTANAS}}
    # Holm: (i) dentro del grupo 2 (12 pruebas, cota INFERIOR del p ajustado de la familia real), (ii) cota SUPERIOR con la familia
    # maxima posible (9 referencias + hasta 6 finalistas = 15 candidatas x 2 ventanas = 30 pruebas): Bonferroni min(1, 30 p).
    claves = [(nom, w) for nom in R for w in EV.VENTANAS]
    ps = [_nz(R[nom]["operador"]["tolerante"][w]["pct_sostenidos"].get("p_azar")) for nom, w in claves]
    h12 = EV.holm(ps)
    resumen["holm"] = {}
    for (nom, w), p, a in zip(claves, ps, h12):
        sup = min(1.0, 30 * p) if p == p else float("nan")
        c_inf = EV.criterios_exito(R[nom], w, a)
        c_sup = EV.criterios_exito(R[nom], w, sup)
        resumen["holm"]["%s|%s" % (nom, w)] = {"p_azar": p, "p_holm_grupo2_m12": a, "p_bonferroni_m30": sup,
                                               "criterios_con_holm_m12": c_inf, "criterios_con_bonf_m30": c_sup}
    with open(os.path.join(RES, "resumen_PRUEBA.json"), "w", encoding="utf-8") as f:
        json.dump(EV.limpiar_para_json(resumen), f, ensure_ascii=False, indent=1)
    imprimir(resumen)
    for k, v in resumen["holm"].items():
        print(k, json.dumps(EV.limpiar_para_json(v)))


if __name__ == "__main__":
    que = sys.argv[1] if len(sys.argv) > 1 else ""
    {"entrenamiento": entrenamiento, "sellar": sellar, "prueba": prueba}.get(que, lambda: print(__doc__))()
