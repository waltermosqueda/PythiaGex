# -*- coding: utf-8 -*-
"""Candidatas del explorador MODELO (ronda 19), definidas EXACTAMENTE sobre las columnas crudas de la base de eventos (para finalistas.py).
Son las reglas FINALES de cada procedimiento (el procedimiento corrido con las 26 ruedas de diseno). Sus numeros honestos de diseno son
los del procedimiento FUERA DE PLIEGUE (LORO con embargo +-1); los de la regla final sobre el diseno son dentro de muestra (inflados).
Horario: todas las vueltas de la rueda (9:30-16:00 NY, sin ventana extra). Una posicion por vez, maximo 5 por rueda.
Uso de control: python -I candidatas_modelo.py  (reproduce los numeros dentro de muestra desde las columnas crudas)"""
import numpy as np


def _netbig_rel(df):
    esc = ((df["of_u_netbig60_p80"] - df["of_u_netbig60_p20"]) / 2.0).to_numpy()
    v = df["of_netbig60_a_favor"].to_numpy()
    return np.where(esc > 0, v / np.where(esc > 0, esc, 1.0), np.nan)


def modelo_a(df):
    """A 'vuelta al valor con grandes a favor' (procedimiento SGC3-S6): precio a mas de 1,076 desvios del VWAP de la JORNADA con el VWAP del
    lado de la ganancia, grandes netos a favor > 1,339 veces su escala ((p80 - p20)/2 de la franja) en los 60 s previos, y pierna de la
    marea <= 8,75 X. Salida pre-registrada S6 (1R). Alternativas medidas sobre las mismas entradas: S2 (3R) y S4b (persigue 4X)."""
    nb = _netbig_rel(df)
    return ((df["vwap_jornada_dist_sd"].to_numpy() * df["lado"].to_numpy() <= -1.076)
            & np.nan_to_num(nb > 1.339, nan=False) & (df["ctx_pierna_marea_X"].to_numpy() <= 8.75))


def modelo_b(df):
    """B 'vuelta al valor profunda' (plantilla T1; forma elegida mirando las reglas de la etapa 1 = sesgo de seleccion declarado):
    retroceso de mas de 2,234 X desde el extremo de la marea y VWAP RTH a 4,846 X o mas del lado de la ganancia.
    Salida pre-registrada S1 (2R). Alternativas medidas: S3 (nivel siguiente) y S6 (1R)."""
    return ((df["ctx_prof_X"].to_numpy() > 2.234) & (df["vwap_rth_dist_pts"].to_numpy() / df["X"].to_numpy() <= -4.846))


CANDIDATAS = {"modelo_A": dict(base="vueltas", mascara=modelo_a, salida="S6", hora=(0, 23400),
                               diseno_oos=dict(n=38, ops_rueda=1.46, acierto=68.4, neto050=1.16, neto100=0.66, t050=1.87)),
              "modelo_B": dict(base="vueltas", mascara=modelo_b, salida="S1", hora=(0, 23400),
                               diseno_oos=dict(n=22, ops_rueda=0.85, acierto=54.5, neto050=1.45, neto100=0.95, t050=0.94))}


if __name__ == "__main__":
    import os, sys
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import m0_comun as C
    df = C.cargar(); B = C.Base(df)
    for k, c in CANDIDATAS.items():
        m = c["mascara"](df)
        for s in ("S1", "S6"):
            tk = C.simular(B, m, s); mm = C.metricas(B, tk, s)
            print("%s eventos %d | %s dentro de muestra: n %d acierto %.1f%% neto050 %+.2f t050 %+.2f" % (k, m.sum(), s, mm["n"], mm["acierto"], mm["neto050"], mm["t050"]))
