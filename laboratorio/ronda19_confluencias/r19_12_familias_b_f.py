# -*- coding: utf-8 -*-
"""RONDA 19, paso 12: familia (b) ruptura con iniciativa (base de cruces) y familia (f) vecindario/ablaciones de E4 (base de
vueltas), en DISENO, tal como se pre-registraron el 27-11 a las 03:38 AR. Todo al registro comun.
Uso: python -I r19_12_familias_b_f.py b | f | control"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import r19_comun as C
import r19_11_evaluar_base as EB

DIAS_DIS = [d for d in C.dias("ES") if "2026-09-10" <= d <= C.DISENO_FIN]
BANDAS = {"vwr+1", "vwr-1", "vwr+2", "vwr-2", "vwj+1", "vwj-1", "vwj+2", "vwj-2"}


def familia_b():
    df = pd.read_parquet(os.path.join(C.SCR, "eventos_cruces_diseno.parquet"))
    print("cruces de diseno:", len(df))
    ini = (df["of_apilados_favor_3v"] >= 1) & (df["of_big_favor_fuerte"] == 1)
    reglas = [("B0", "todos los cruces (linea de base)", np.ones(len(df), bool)),
              ("B1", "cruce + apilados a favor (3 velas) + big trades netos a favor fuertes", ini.to_numpy()),
              ("B2", "B1 + a favor de la marea con agresores", (ini & (df["a_favor_marea"] == 1) & (df["marea_con_agresores"] == 1)).to_numpy()),
              ("B3", "B1 en niveles estructurales (sin bandas de VWAP)", (ini & ~df["nivel"].isin(BANDAS)).to_numpy())]
    for nom, par, m in reglas:
        f = EB.correr_regla(df, nom, "b (ruptura con iniciativa)", par, pd.Series(m, index=df.index), ("S1", "S3", "S4", "S6"), DIAS_DIS,
                            hora_ini=900, hora_fin=21600)
        EB.imprimir(nom, f)


def base_vueltas():
    return pd.read_parquet(os.path.join(C.SCR, "eventos_diseno.parquet"))


def mascara_E4(df, flujo=("F1", "F6"), dist=1.0, extension=True, agresores=True, con_flujo=True):
    m = (df["a_favor_marea"] == 1) & (-df["vwap_rth_dist_pts"] >= dist * df["R"])
    if agresores: m &= (df["marea_con_agresores"] == 1)
    if extension: m &= (df["extension_fuera_ruido"] == 1)
    if con_flujo:
        mf = np.zeros(len(df), bool)
        for f in flujo: mf |= (df[f] == 1).to_numpy()
        m &= mf
    return m


def control():
    df = base_vueltas()
    f = EB.correr_regla(df, "E4_base", "f control", "E4 sobre la base", mascara_E4(df), ("S1", "S6"), DIAS_DIS, hora_ini=1800, hora_fin=21600,
                        placebo=False, anotar=False)
    EB.imprimir("E4_base", f)


def familia_f():
    df = base_vueltas()
    variantes = []
    for nf, fl in (("F1", ("F1",)), ("F6", ("F6",)), ("F1oF6", ("F1", "F6"))):
        for dist in (0.5, 1.0, 1.5):
            if nf == "F1oF6" and dist == 1.0: continue
            variantes.append(("E4v_%s_d%.1f" % (nf, dist), "E3 + flujo %s, distancia al VWAP >= %.1fR" % (nf, dist), mascara_E4(df, fl, dist)))
    variantes += [("E4a_sin_ext", "E4 sin extension fuera del ruido", mascara_E4(df, extension=False)),
                  ("E4a_sin_agr", "E4 sin marea con agresores", mascara_E4(df, agresores=False)),
                  ("E4a_sin_flujo", "E4 sin flujo (= E3 sobre la base)", mascara_E4(df, con_flujo=False))]
    for nom, par, m in variantes:
        f = EB.correr_regla(df, nom, "f (vuelta al valor) vecindario/ablacion", par, m, ("S1", "S6"), DIAS_DIS, hora_ini=1800, hora_fin=21600)
        EB.imprimir(nom, f)


if __name__ == "__main__":
    {"b": familia_b, "f": familia_f, "control": control}[sys.argv[1]]()
