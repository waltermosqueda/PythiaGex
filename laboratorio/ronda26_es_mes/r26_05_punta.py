# -*- coding: utf-8 -*-
"""RONDA 26, paso 5 (Enmienda 2, DESCRIPTIVO, sin pruebas): QUIEN ESTA DEL OTRO LADO, desde la punta que trae la cinta.
Cada fila de la cinta trae la punta ANTES (abid, aask) y DESPUES (dbid, dask) de la orden agresora. En cada pierna emparejada (P1 b):
  - AGUANTA (agresion EN CONTRA de la pierna absorbida): una venta agresora cuyo bid no bajo (dbid >= abid) o una compra agresora cuyo ask no
    subio (dask <= aask): el nivel tocado siguio en pie (habia mas ordenes pasivas o se recargo). Tasa = volumen que aguanto / volumen en contra.
  - MUEVE (agresion A FAVOR que corrio el precio): una compra que subio el ask (dask > aask) o una venta que bajo el bid (dbid < abid).
    Tasa = volumen que movio / volumen a favor.
  - En el MES, ademas: compras/ventas A FAVOR que se encontraron con el nivel en pie (1 - MUEVE): lo que vendieron/compraron los pasivos del MES
    al minorista agresor (lo que H-ARB necesita; no la prueba).
Por clase de la pierna (CC, SC, CS, SS) en ES y MES, ponderado por volumen (suma de la clase) y mediana por pierna. Puntas invalidas (<= 0,
cruzadas) quedan afuera. Mismo orden del archivo y mismos indices que r26_comun.procesar.
Uso: python -I r26_05_punta.py <tanda>"""
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


def acumulados_punta(inst, dia):
    d, _, _ = C.leer(C.archivo(inst, dia))
    w0, w1 = C.ventana(dia)
    d = d[(d["ms"] >= w0) & (d["ms"] < w1)].reset_index(drop=True)
    v, l = d["vol"].to_numpy(), d["lado"].to_numpy()
    ab, aa, db, da = (d[c].to_numpy() for c in ("abid", "aask", "dbid", "dask"))
    ok = (ab > 0) & (aa > ab) & (db > 0) & (da > db)
    compra, venta = ok & (l == 1), ok & (l == -1)
    out = {"compra": np.cumsum(v * compra), "venta": np.cumsum(v * venta),
           "compra_aguanta": np.cumsum(v * (compra & (da <= aa))), "venta_aguanta": np.cumsum(v * (venta & (db >= ab))),
           "compra_mueve": np.cumsum(v * (compra & (da > aa))), "venta_mueve": np.cumsum(v * (venta & (db < ab)))}
    del d
    gc.collect()
    return out


def tramo(A, k, i0, i1):
    return float(A[k][i1] - A[k][i0])


def main():
    tanda = sys.argv[1]
    pares = json.load(open(os.path.join(C.DATOS, "pares.json")))
    dias = sorted(d for d, v in pares.items() if v["tanda"] == tanda and v["alinea"])
    print("RONDA 26 - la punta (descriptivo, Enmienda 2), %s: %d ruedas %s" % (tanda, len(dias), " ".join(dias)))
    filas = []
    for dia in dias:
        C.frenar_si_falta_ram()
        A = {inst: acumulados_punta(inst, dia) for inst in ("ES", "MES")}
        for esc in ("X", "4X"):
            P = C.armar_piernas(dia, esc)
            if not len(P):
                continue
            E, M = C.cargar("ES", dia), C.cargar("MES", dia)
            pe, pm = E["piv"][esc], M["piv"][esc]
            mt = C.emparejar(pe, pm)
            for _, g in P[P["emparejada"]].iterrows():
                j = int(g["j"])
                s = int(g["dir"])
                f = {"dia": dia, "esc": esc, "clase": g["clase"], "dir": s}
                for inst, pv, ja in (("ES", pe, j), ("MES", pm, int(mt[j]))):
                    i0, i1 = int(pv["i_ext"].iloc[ja]), int(pv["i_ext"].iloc[ja + 1])
                    contra, favor = ("venta", "compra") if s == 1 else ("compra", "venta")
                    f[inst + "_contra"] = tramo(A[inst], contra, i0, i1)
                    f[inst + "_contra_aguanta"] = tramo(A[inst], contra + "_aguanta", i0, i1)
                    f[inst + "_favor"] = tramo(A[inst], favor, i0, i1)
                    f[inst + "_favor_mueve"] = tramo(A[inst], favor + "_mueve", i0, i1)
                filas.append(f)
        del A
        gc.collect()
    T = pd.DataFrame(filas)
    for esc in ("X", "4X"):
        g0 = T[T["esc"] == esc]
        print("\n%s: %d piernas emparejadas" % ("OLA (X)" if esc == "X" else "MAREA (4X)", len(g0)))
        print("   clase               n  | ES: en contra aguanta  a favor mueve  (vol. contra / favor por pierna) | MES: en contra aguanta  a favor mueve"
              "  a favor contra nivel en pie (vol. contra / favor, eq. ES)")
        for c in CLASES:
            g = g0[g0["clase"] == c]
            if not len(g):
                continue
            def r(a, b):
                return 100 * g[a].sum() / g[b].sum() if g[b].sum() > 0 else np.nan
            def med(a, b):
                x = g[a] / g[b].where(g[b] > 0)
                return 100 * x.median()
            print("   %-17s %4d  |   %5.1f %% (med %5.1f)   %5.1f %% (med %5.1f)   (%6.0f / %6.0f)        |   %5.1f %% (med %5.1f)   %5.1f %% (med %5.1f)"
                  "   %5.1f %%   (%6.0f / %6.0f)" %
                  (NOMBRE[c], len(g), r("ES_contra_aguanta", "ES_contra"), med("ES_contra_aguanta", "ES_contra"),
                   r("ES_favor_mueve", "ES_favor"), med("ES_favor_mueve", "ES_favor"), g["ES_contra"].mean(), g["ES_favor"].mean(),
                   r("MES_contra_aguanta", "MES_contra"), med("MES_contra_aguanta", "MES_contra"),
                   r("MES_favor_mueve", "MES_favor"), med("MES_favor_mueve", "MES_favor"), 100 - r("MES_favor_mueve", "MES_favor"),
                   g["MES_contra"].mean() / 10, g["MES_favor"].mean() / 10))
    T.to_parquet(os.path.join(C.DATOS, "r26_05-%s.parquet" % tanda), index=False)


if __name__ == "__main__":
    main()
