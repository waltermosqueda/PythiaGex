# -*- coding: utf-8 -*-
"""RONDA 19, paso 17: verificacion de las candidatas de los exploradores con MI evaluador (r19_11) sobre DISENO, antes del arbitraje:
(1) reproduce n, acierto, neto y t declarados; (2) para DELTA recalcula la operacion contraria DESDE LA CINTA (r19_16_fade) y la compara
con la derivada de S6 (S6m); (3) solapamiento de operaciones entre candidatas (dos finalistas con las mismas operaciones no son dos
confirmaciones). No abre la confirmacion. Uso: python -I r19_17_verificar_candidatas.py"""
import json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import r19_comun as C
import r19_11_evaluar_base as EB
import r19_16_fade as FD
import finalistas as FIN

TK = 0.25


def main():
    df = pd.read_parquet(os.path.join(C.SCR, "eventos_diseno.parquet"))
    assert (df["muestra"] == "diseno").all()
    df = df.sort_values(["dia", "seg_rueda", "t_utc_ms"], kind="mergesort").reset_index(drop=True)
    dias = [d for d in C.dias("ES") if "2026-09-10" <= d <= C.DISENO_FIN]
    # fade desde la cinta para todo evento que cumpla alguna regla DELTA
    md = np.zeros(len(df), bool)
    for c in FIN.CANDIDATAS:
        if c["salida"] == "S6mc": md |= c["mascara"](df)
    df = FD.agregar_fade(df, md)
    m6 = df["S6_motivo"]
    df["S6m_pts"] = np.where(m6 == "stop", df["R"] - TK, np.where(m6 == "objetivo", -(df["R"] + TK), -df["S6_pts"]))
    chk = df[md]
    iguales = np.isclose(chk["S6mc_pts"], chk["S6m_pts"]) & (chk["S6mc_seg_salida"] == chk["S6_seg_salida"])
    print("fade desde la cinta contra el derivado de S6: %d eventos, iguales en pts y segundo de salida: %d (%.1f %%)" % (len(chk), iguales.sum(), 100 * iguales.mean()))
    if (~iguales).any():
        print(chk.loc[~iguales, ["dia", "hora_ny", "lado", "entrada", "ext", "R", "S6_pts", "S6_motivo", "S6m_pts", "S6mc_pts", "S6mc_motivo", "S6_seg_salida", "S6mc_seg_salida"]].head(10).to_string())
    filas = []; tomadas = {}
    for c in FIN.CANDIDATAS:
        m = pd.Series(c["mascara"](df), index=df.index)
        ops = EB.simular(df, m, c["salida"], hora_ini=c["hora_ini"], hora_fin=c["hora_fin"])
        r = EB.juzgar(ops, c["salida"], dias)
        tomadas[c["nombre"]] = set(zip(ops["dia"], ops["seg_rueda"]))
        dec = c["diseno"]
        print("%-10s mio: n %3d acierto %5.1f %% neto %+.3f t %+.2f | declarado: n %s acierto %s neto %s t %s" % (
            c["nombre"], r["n"], r.get("acierto", np.nan), r.get("c050_neto_op", np.nan), r.get("c050_t", np.nan), dec["n"], dec["acierto"], dec["neto"], dec["t"]))
        filas.append(dict(candidata=c["nombre"], familia=c["familia"], n=r["n"], ops_rueda=r["ops_por_rueda"], acierto=r.get("acierto"),
                          neto_c050=r.get("c050_neto_op"), neto_c100=r.get("c100_neto_op"), t=r.get("c050_t"), declarado=dec))
    # solapamiento (por evento: misma rueda y mismo segundo de entrada; DELTA opera el lado contrario del mismo evento)
    noms = [c["nombre"] for c in FIN.CANDIDATAS]
    sol = pd.DataFrame(index=noms, columns=noms, dtype=float)
    for a in noms:
        for b in noms:
            A, B = tomadas[a], tomadas[b]
            sol.loc[a, b] = round(100 * len(A & B) / max(1, min(len(A), len(B))), 0)
    pd.set_option("display.width", 250)
    print("\nsolapamiento de eventos entre candidatas (% del menor):\n" + sol.to_string())
    json.dump(dict(filas=filas, solapamiento=sol.to_dict()), open(os.path.join(C.AQUI, "salidas", "verificacion_candidatas_diseno.json"), "w"), indent=1, default=str)


if __name__ == "__main__":
    main()
