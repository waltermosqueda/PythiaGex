# -*- coding: utf-8 -*-
"""RONDA 19, paso 15: csv de operaciones de las finalistas (diseno, confirmacion y guardada) con TODOS sus rasgos, para la demo visual.
Sale a operaciones_finalistas.csv (carpeta de la ronda) con las columnas clave primero."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pandas as pd
import r19_comun as C

CLAVE = ["finalista", "salida_regla", "muestra", "inst", "dia", "hora_ny", "t_utc", "t_utc_ms", "lado_operacion", "lado", "entrada", "ext", "stop", "R", "X",
         "vwap_rth", "vwap_rth_dist_pts", "marea_dir", "a_favor_marea", "F1", "F6", "of_abs_lado_pasivo_rel", "of_netbig60_a_favor",
         "niveles_en_zona", "ctx_ext_ib", "ctx_en_va_ayer"]


def main():
    partes = []
    for mu in ("diseno", "confirma", "guardada"):
        f = os.path.join(C.AQUI, "salidas", "juez", "ops-juez-%s.csv" % mu)
        if os.path.exists(f): partes.append(pd.read_csv(f))
    df = pd.concat(partes, ignore_index=True)
    df["resultado_pts"] = [r[r["salida_regla"] + "_pts"] for _, r in df.iterrows()]
    df["resultado_motivo"] = [r[r["salida_regla"] + "_motivo"] for _, r in df.iterrows()]
    df["neto_c050"] = df["resultado_pts"] - 0.50
    # lado de la OPERACION: en el fade (S6mc, DELTA) se opera al reves de la vuelta de la ola
    df["lado_operacion"] = [(-r["lado"] if r["salida_regla"] == "S6mc" else r["lado"]) for _, r in df.iterrows()]
    cols = [c for c in CLAVE if c in df] + ["resultado_pts", "resultado_motivo", "neto_c050"]
    cols += [c for c in df.columns if c not in cols]
    df[cols].to_csv(os.path.join(C.AQUI, "operaciones_finalistas.csv"), index=False)
    print("operaciones_finalistas.csv:", len(df), "filas")


if __name__ == "__main__":
    main()
