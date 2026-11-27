# -*- coding: utf-8 -*-
"""RONDA 26, paso 0: la regla de la TANDA 0 (PREREGISTRO seccion 3), aplicada SOLO con las tablas diarias (apertura, maximo, minimo y cierre de
la rueda), sin leer cintas ni mirar flujo ni desenlaces.
Candidatas: los 10 pares ES/MES de noviembre. Eficiencia e = |cierre - apertura| / (maximo - minimo) de la rueda 9:30-16:00 NY.
Fuente: ES 12 y 13-11 de la tabla de la ronda 16; 16 a 25-11 de la tabla de MES de la ronda 17 (mismo indice; solo para elegir dias).
TENDENCIA = mayor e; RANGO = menor e; MIXTA = e mas cercana a la mediana de las 10 (empate: la mas vieja). Si un par elegido no alinea
(misma cinta, mismo vencimiento), entra el siguiente del mismo criterio.
Uso: python -I r26_00_seleccion.py"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import pandas as pd
import r26_comun as C


def main():
    es = pd.read_parquet(C.R16_NIV)
    me = pd.read_parquet(C.R17_NIV_MES)
    filas = []
    for d in C.NOV:
        src = es if d <= "2026-11-13" else me
        r = src[src["dia"] == d]
        if len(r) != 1:
            print("FALTA la rueda", d)
            continue
        r = r.iloc[0]
        e = abs(r["c"] - r["o"]) / (r["h"] - r["l"])
        filas.append((d, "ES r16" if d <= "2026-11-13" else "MES r17", r["o"], r["h"], r["l"], r["c"], r["h"] - r["l"], e))
    t = pd.DataFrame(filas, columns=["dia", "fuente", "o", "h", "l", "c", "rango", "e"])
    print(t.to_string(index=False, float_format=lambda x: "%.3f" % x))
    med = float(np.median(t["e"]))
    tend = t.sort_values(["e", "dia"], ascending=[False, True])["dia"].tolist()
    rang = t.sort_values(["e", "dia"], ascending=[True, True])["dia"].tolist()
    t["dist"] = (t["e"] - med).abs()
    mixt = t.sort_values(["dist", "dia"])["dia"].tolist()
    print("\nmediana de e: %.3f" % med)
    print("orden TENDENCIA (mayor e):", tend)
    print("orden RANGO (menor e):", rang)
    print("orden MIXTA (mas cerca de la mediana):", mixt)
    elegidos = []
    for lista, nombre in ((tend, "TENDENCIA"), (rang, "RANGO"), (mixt, "MIXTA")):
        d = next(x for x in lista if x not in elegidos)
        elegidos.append(d)
        print("TANDA 0 %-9s -> %s (e %.3f)" % (nombre, d, float(t.loc[t["dia"] == d, "e"].iloc[0])))
    print("TANDA 0:", sorted(elegidos))


if __name__ == "__main__":
    main()
