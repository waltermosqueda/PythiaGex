# -*- coding: utf-8 -*-
"""RONDA 19, paso 4: narracion operacion por operacion (de menor a mayor) de una etapa en una muestra chica.
Uso: python -I r19_04_narrar.py <etapa> <muestra>   -> narraciones/<etapa>-<muestra>.md"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pandas as pd
import r19_comun as C

NOMBRE_F = {"F1": "absorcion", "F2": "iceberg", "F3": "divergencia", "F4": "agotamiento", "F5": "apilado", "F6": "big trades"}


def main(etapa, mu):
    o = pd.read_csv(os.path.join(C.AQUI, "salidas", "ops-%s-%s.csv" % (etapa, mu)))
    os.makedirs(os.path.join(C.AQUI, "narraciones"), exist_ok=True)
    L = ["# %s en %s — operacion por operacion" % (etapa, mu), "",
         "Cada fila: hora NY, lado, entrada (extremo del retroceso), R, niveles en la zona, factores de momento presentes, contexto",
         "(retroceso en X desde el extremo de la marea, con o sin agresores, duracion, tipo de dia) y el resultado con cada salida",
         "(S1 2R, S2 3R, S3 nivel siguiente, S4 raya de la marea). Las entradas pueden diferir entre salidas porque la posicion",
         "abierta bloquea las siguientes.", ""]
    for dia, g in o.groupby("dia"):
        L.append("## %s (X %.2f)" % (dia, g["X"].iloc[0]))
        base = g.drop_duplicates(["t", "lado"]).sort_values("t")
        for _, r in base.iterrows():
            f = [NOMBRE_F[k] for k in ("F1", "F2", "F3", "F4", "F5", "F6") if r.get(k, 0) == 1]
            res = []
            for s in ("S1", "S2", "S3", "S4"):
                q = g[(g["t"] == r["t"]) & (g["lado"] == r["lado"]) & (g["salida"] == s)]
                res.append("%s %s" % (s, ("%+.2f (%s %s)" % (q["pts"].iloc[0], q["motivo"].iloc[0], q["hora_sal"].iloc[0])) if len(q) else "no tomada"))
            L.append("- **%s %s** a %.2f (extremo %.2f, R %.2f) | niveles: %s | momento (%d): %s | retroceso %.2f X %s, %d s, delta %+d |"
                     " dia: IB %s, ruido %s, area de ayer %s, VWAP %+.2f desvios | %s" % (
                         r["hora"], "COMPRA" if r["lado"] == 1 else "VENTA", r["entrada"], r["ext"], r["R"], r["niveles"].replace("|", ", "),
                         r["puntaje"], ", ".join(f), r["prof_X"], "con agresores" if r["ret_con_agresores"] == 1 else "SIN agresores",
                         r["dur_ret_s"], r["del_ret"], r["ext_ib"], r["ruido"], r["en_va_ayer"], r["dist_vwap_sd"], " · ".join(res)))
        L.append("")
    open(os.path.join(C.AQUI, "narraciones", "%s-%s.md" % (etapa, mu)), "w", encoding="utf-8").write("\n".join(L))
    print("ok", len(o))


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
