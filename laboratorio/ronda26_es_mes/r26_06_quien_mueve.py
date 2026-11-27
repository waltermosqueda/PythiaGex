# -*- coding: utf-8 -*-
"""RONDA 26, paso 6 (Enmienda 3, DESCRIPTIVO, sin pruebas): QUIEN MOVIO EL PRECIO EN LA PIERNA, el que agrede o el que espera.
Con la punta antes (abid, aask) y despues (dbid, dask) de cada orden agresora de la cinta, el movimiento de la punta media de una pierna se
parte en dos sumas que se completan entre si:
  - AGRESOR: suma de (media despues - media antes) de cada orden: lo que corrio el precio la propia orden al comerse la punta;
  - PASIVO: suma de (media antes de una orden - media despues de la anterior): lo que se movio la punta ENTRE ordenes, por altas y bajas de
    ordenes limitadas (el libro que se retira o se recarga). Es lo que el delta no ve (la parte pasiva del OFI de la ronda 17).
  Con el signo de la pierna (+ = a favor), en puntos por pierna y como parte del movimiento. Puntas invalidas (<= 0 o cruzadas): ese termino
  vale 0 (se informa cuanto del recorrido queda explicado).
Y la ABSORCION REAL con la definicion del proyecto (laboratorio\\gatillo\\base.py, abs_bid/abs_ask): una orden que se comio TODO lo visible del
nivel (vol >= tamano visible antes) y el nivel NO cedio (mismo precio o mejor y tamano >= 1 despues) = iceberg o pasivo que repone. Se
informa la absorcion de la agresion EN CONTRA de la pierna (el pasivo que defiende la pierna) y de la agresion A FAVOR (el pasivo que le
pone el pecho), como % del volumen agresor de cada lado.
Por clase de la pierna emparejada (CC, SC, CS, SS), en ES y MES. Mismos indices que r26_comun.procesar.
Uso: python -I r26_06_quien_mueve.py <tanda>"""
import gc
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import pandas as pd
import r26_comun as C

C.prioridad_baja()
CLASES = ["CC", "SC", "CS", "SS"]
NOMBRE = {"CC": "ES con / MES con", "SC": "ES sin / MES con", "CS": "ES con / MES sin", "SS": "ES sin / MES sin"}


def acumulados(inst, dia):
    d, _, _ = C.leer(C.archivo(inst, dia))
    w0, w1 = C.ventana(dia)
    d = d[(d["ms"] >= w0) & (d["ms"] < w1)].reset_index(drop=True)
    v, l = d["vol"].to_numpy(), d["lado"].to_numpy()
    ab, abv, aa, aav = (d[c].to_numpy() for c in ("abid", "abidv", "aask", "aaskv"))
    db, dbv, da, dav = (d[c].to_numpy() for c in ("dbid", "dbidv", "dask", "daskv"))
    okA = (ab > 0) & (aa > ab)
    okD = (db > 0) & (da > db)
    mA = np.where(okA, (ab + aa) / 2, np.nan)
    mD = np.where(okD, (db + da) / 2, np.nan)
    agr = np.where(okA & okD, mD - mA, 0.0)
    pas = np.zeros(len(d))
    pas[1:] = np.where(okA[1:] & okD[:-1], mA[1:] - mD[:-1], 0.0)
    venta, compra = l == -1, l == 1
    agota_b, agota_a = venta & (v >= abv), compra & (v >= aav)
    abs_b = agota_b & (db >= ab) & (dbv >= 1)
    abs_a = agota_a & (da <= aa) & (dav >= 1)
    out = {"agr": np.cumsum(agr), "pas": np.cumsum(pas), "mD": mD,
           "venta": np.cumsum(v * venta), "compra": np.cumsum(v * compra),
           "abs_b": np.cumsum(v * abs_b), "abs_a": np.cumsum(v * abs_a)}
    del d
    gc.collect()
    return out


def main():
    tanda = sys.argv[1]
    pares = json.load(open(os.path.join(C.DATOS, "pares.json")))
    dias = sorted(d for d, v in pares.items() if v["tanda"] == tanda and v["alinea"])
    print("RONDA 26 - quien movio el precio (descriptivo, Enmienda 3), %s: %d ruedas %s" % (tanda, len(dias), " ".join(dias)))
    filas = []
    for dia in dias:
        C.frenar_si_falta_ram()
        A = {inst: acumulados(inst, dia) for inst in ("ES", "MES")}
        for esc in ("X", "4X"):
            P = C.armar_piernas(dia, esc)
            if not len(P):
                continue
            pe, pm = C.cargar("ES", dia)["piv"][esc], C.cargar("MES", dia)["piv"][esc]
            mt = C.emparejar(pe, pm)
            for _, g in P[P["emparejada"]].iterrows():
                j, s = int(g["j"]), int(g["dir"])
                f = {"dia": dia, "esc": esc, "clase": g["clase"], "dir": s, "recorrido": g["recorrido"]}
                for inst, pv, ja in (("ES", pe, j), ("MES", pm, int(mt[j]))):
                    a = A[inst]
                    i0, i1 = int(pv["i_ext"].iloc[ja]), int(pv["i_ext"].iloc[ja + 1])
                    f[inst + "_agr"] = s * (a["agr"][i1] - a["agr"][i0])
                    f[inst + "_pas"] = s * (a["pas"][i1] - a["pas"][i0])
                    f[inst + "_media"] = s * (a["mD"][i1] - a["mD"][i0]) if np.isfinite(a["mD"][i1]) and np.isfinite(a["mD"][i0]) else np.nan
                    contra, favor = ("venta", "compra") if s == 1 else ("compra", "venta")
                    abs_contra, abs_favor = ("abs_b", "abs_a") if s == 1 else ("abs_a", "abs_b")
                    f[inst + "_v_contra"] = a[contra][i1] - a[contra][i0]
                    f[inst + "_v_favor"] = a[favor][i1] - a[favor][i0]
                    f[inst + "_abs_contra"] = a[abs_contra][i1] - a[abs_contra][i0]
                    f[inst + "_abs_favor"] = a[abs_favor][i1] - a[abs_favor][i0]
                filas.append(f)
        del A
        gc.collect()
    T = pd.DataFrame(filas)
    for esc in ("X", "4X"):
        g0 = T[T["esc"] == esc]
        print("\n%s: %d piernas emparejadas" % ("OLA (X)" if esc == "X" else "MAREA (4X)", len(g0)))
        print("   clase               n  recorr | ES: agresor  pasivo (pts por pierna)  parte agresor | absorcion real: en contra  a favor"
              " | MES: agresor  pasivo  parte agresor | absorcion real: en contra  a favor")
        for c in CLASES:
            g = g0[g0["clase"] == c]
            if not len(g):
                continue
            def parte(inst):
                t = g[inst + "_agr"].sum() + g[inst + "_pas"].sum()
                return 100 * g[inst + "_agr"].sum() / t if t else np.nan
            def ab(inst, lado):
                den = g[inst + "_v_" + lado].sum()
                return 100 * g[inst + "_abs_" + lado].sum() / den if den else np.nan
            print("   %-17s %4d  %6.1f |    %+7.2f %+7.2f                   %5.0f %%    |          %5.2f %%   %5.2f %%   |   %+7.2f %+7.2f    %5.0f %%     |"
                  "          %5.2f %%   %5.2f %%" %
                  (NOMBRE[c], len(g), g["recorrido"].mean(), g["ES_agr"].mean(), g["ES_pas"].mean(), parte("ES"), ab("ES", "contra"), ab("ES", "favor"),
                   g["MES_agr"].mean(), g["MES_pas"].mean(), parte("MES"), ab("MES", "contra"), ab("MES", "favor")))
        print("   control: agresor + pasivo contra el movimiento de la punta media de la pierna (ES): mediana de la diferencia %.3f pts" %
              float(np.nanmedian(g0["ES_agr"] + g0["ES_pas"] - g0["ES_media"])))
    T.to_parquet(os.path.join(C.DATOS, "r26_06-%s.parquet" % tanda), index=False)


if __name__ == "__main__":
    main()
