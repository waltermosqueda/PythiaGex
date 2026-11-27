# -*- coding: utf-8 -*-
"""RONDA 27, paso 6: CONGELAR los modelos para el vivo (PREREGISTRO seccion 6): el MISMO procedimiento (seleccion anidada con 4 bloques contiguos
y tau fuera de pliegue) sobre las ruedas que se le pidan (por defecto las 55), y exportacion a JSON escribible en C#:
  - logistica: rasgos, rms, coeficientes (p = 1 / (1 + exp(-sum(coef_i * x_i / rms_i)))) o "nulo" (p = 0,5);
  - XGBoost: arboles en JSON (get_dump) + orden de rasgos + cuales se espejan + la regla de simetrizado, o "nulo".
Escribe modelo_congelado.json y huellas_r27.json (SHA-256 de los scripts del pipeline, del juez y del modelo).
Uso: python -I r27_06_congelar.py [--hasta AAAA-MM-DD] [--salida modelo_congelado.json]"""
import argparse, json, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r27_comun as K
import r27_03_modelos as MD
import numpy as np, pandas as pd

DEF_RASGOS = {
    "M0": "o_dir, o_dir*o_ret, o_dir*o_lar, m_dir, m_dir*m_ret, m_dir*m_lar",
    "M1": "m_dir, m_dir*[T], o_dir, o_dir*[T], lado_T, o_dir*o_ret, vw_X, vw_X*tarde, d5, d5*[T], med15, zona_o_s*[T]",
    "M2": ", ".join(MD.M2_RASGOS),
    "M3": "v_T7, v_V4, v_A3",
}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--hasta", default="2026-11-25")
    ap.add_argument("--salida", default="modelo_congelado.json")
    ap.add_argument("--modelos", nargs="+", default=["M0", "M1", "M2", "M3"], help="solo para pruebas de plomeria; el congelado real usa los 4")
    a = ap.parse_args()
    T = MD.preparar(pd.read_parquet(MD.TAB))
    T = T[T.dia <= a.hasta]
    dias = sorted(T.dia.unique())
    out = {"version": "r27-1", "generado_AR": time.strftime("%Y-%m-%d %H:%M"), "ruedas": [dias[0], dias[-1], len(dias)],
           "definicion": "P(arriba primero) del cierre de minuto con meta simetrica +-Y (Y = X/2, X, 2X), plazo 60 min o 16:00; modelos antisimetricos",
           "rasgos": DEF_RASGOS, "impares_M2": sorted(MD.M2_IMPARES), "monotonas_M2": MD.M2_MONO,
           "regla": {"desde_m": MD.M_DESDE, "hasta_m": MD.M_HASTA, "max_por_rueda": MD.MAX_DIA,
                     "senal": "compra si p >= tau y p > 0,5; venta si p <= 1 - tau y p < 0,5; una posicion por vez; bracket +-Y (objetivo pasado 1 tick, stop al tocar)"},
           "modelos": {}}
    for Yk in K.YS:
        for modelo in ("M0", "M1", "M2", "M3"):
            if modelo not in a.modelos:
                out["modelos"]["%s_%s" % (modelo, Yk)] = {"tipo": "xgboost" if modelo == "M2" else "logistica", "nulo": True, "Y": Yk,
                                                          "tau_R_frec": 0.5, "nota": "no ajustado (prueba de plomeria)"}
                continue
            por_dia = {}
            for d, g in T.groupby("dia"):
                g = g.sort_values("m"); y = g["y_" + Yk].to_numpy(); X, nombres = MD.matriz(g, modelo)
                por_dia[d] = {"X": X, "y": np.nan_to_num(y), "ok": np.isfinite(y), "g": g}
            hp, tabla_ll, tau, _ = MD.seleccionar(modelo, dias, Yk, por_dia)
            Xtr = np.vstack([por_dia[x]["X"][por_dia[x]["ok"]] for x in dias]); ytr = np.concatenate([por_dia[x]["y"][por_dia[x]["ok"]] for x in dias])
            mdl = MD.nuevo(modelo, hp).ajustar(Xtr, ytr)
            if modelo == "M2":
                if hp is None:
                    exp = {"tipo": "xgboost", "nulo": True}
                else:
                    exp = {"tipo": "xgboost", "nulo": False, "hp": hp, "arboles": [json.loads(x) for x in mdl.bst.get_booster().get_dump(dump_format="json")],
                           "rasgos": MD.M2_RASGOS, "base_score": 0.5,
                           "prediccion": "p(x) = sigmoide(suma de hojas); p_final = (p(x) + 1 - p(espejo(x))) / 2, espejo = cambiar el signo de los impares"}
            else:
                exp = mdl.exportar(nombres); exp["nulo"] = hp is None
            exp.update({"Y": Yk, "tau_R_frec": tau, "logloss_interna": tabla_ll})
            out["modelos"]["%s_%s" % (modelo, Yk)] = exp
            print("%s %s: hp %s tau %.3f" % (modelo, Yk, json.dumps(hp), tau), flush=True)
    ruta = os.path.join(K.AQUI, a.salida)
    json.dump(out, open(ruta, "w"), indent=1)
    if a.salida != "modelo_congelado.json":
        print("escrito %s (prueba: no se escriben huellas)" % ruta); return
    hu = {"generado_AR": time.strftime("%Y-%m-%d %H:%M"), "sha256": {}}
    for f in ("r27_comun.py", "r27_03_modelos.py", "r27_vivo.py", a.salida):
        p = os.path.join(K.AQUI, f)
        if os.path.exists(p):
            hu["sha256"][f] = K.sha256(p)
    hu["sha256"]["curva_rango_por_minuto.json"] = K.sha256(K.CURVA)
    json.dump(hu, open(os.path.join(K.AQUI, "huellas_r27.json"), "w"), indent=1)
    print("escrito %s y huellas_r27.json" % ruta)


if __name__ == "__main__":
    main()
