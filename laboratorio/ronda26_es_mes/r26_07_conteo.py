# -*- coding: utf-8 -*-
"""RONDA 26, paso 7 (Enmienda 4, DESCRIPTIVO, sin pruebas): CONTRATOS CONTRA ORDENES. En cada pierna emparejada y por clase, en ES y MES:
  - cantidad de ordenes agresoras A FAVOR y EN CONTRA de la pierna, y su volumen (contratos);
  - cuantas de esas ordenes corrieron la punta media en su direccion (media despues - media antes, con el signo de la orden, > 0);
  - impacto medio por orden (ticks) a favor y en contra.
Hipotesis mecanica fijada antes de la tanda 1 (vista en la tanda 0 de la Enmienda 3: el impacto es por orden, concavo en el tamano): en las
piernas "ES sin agresores" hay MAS contratos en contra pero MAS ORDENES a favor; el precio lo corre el lado con mas ordenes que comen la punta.
Uso: python -I r26_07_conteo.py <tanda>"""
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
    ab, aa, db, da = (d[c].to_numpy() for c in ("abid", "aask", "dbid", "dask"))
    ok = (ab > 0) & (aa > ab) & (db > 0) & (da > db)
    imp = np.where(ok, ((db + da) - (ab + aa)) / 2 * l / C.TICK, 0.0)     # ticks que corrio la media, con el signo de la orden
    out = {}
    for lado, nom in ((1, "c"), (-1, "v")):
        m = l == lado
        out["n_" + nom] = np.cumsum(m.astype(float))
        out["vol_" + nom] = np.cumsum(v * m)
        out["mueve_" + nom] = np.cumsum((m & (imp > 0)).astype(float))
        out["imp_" + nom] = np.cumsum(np.where(m, imp, 0.0))
    del d
    gc.collect()
    return out


def main():
    tanda = sys.argv[1]
    pares = json.load(open(os.path.join(C.DATOS, "pares.json")))
    dias = sorted(d for d, v in pares.items() if v["tanda"] == tanda and v["alinea"])
    print("RONDA 26 - contratos contra ordenes (descriptivo, Enmienda 4), %s: %d ruedas %s" % (tanda, len(dias), " ".join(dias)))
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
                fav, con = ("c", "v") if s == 1 else ("v", "c")
                for inst, pv, ja in (("ES", pe, j), ("MES", pm, int(mt[j]))):
                    a = A[inst]
                    i0, i1 = int(pv["i_ext"].iloc[ja]), int(pv["i_ext"].iloc[ja + 1])
                    for k in ("n", "vol", "mueve", "imp"):
                        f["%s_%s_favor" % (inst, k)] = a["%s_%s" % (k, fav)][i1] - a["%s_%s" % (k, fav)][i0]
                        f["%s_%s_contra" % (inst, k)] = a["%s_%s" % (k, con)][i1] - a["%s_%s" % (k, con)][i0]
                filas.append(f)
        del A
        gc.collect()
    T = pd.DataFrame(filas)
    for esc in ("X", "4X"):
        g0 = T[T["esc"] == esc]
        print("\n%s: %d piernas emparejadas (medias por pierna)" % ("OLA (X)" if esc == "X" else "MAREA (4X)", len(g0)))
        for inst in ("ES", "MES"):
            print("   %s  clase               n | ordenes a favor / en contra (cociente) | contratos a favor / en contra (cociente) | contratos por orden"
                  " a favor / en contra | ordenes que corrieron la punta a favor / en contra | impacto por orden (ticks) a favor / en contra" % inst)
            for c in CLASES:
                g = g0[g0["clase"] == c]
                if not len(g):
                    continue
                nf, nc = g[inst + "_n_favor"].sum(), g[inst + "_n_contra"].sum()
                vf, vc = g[inst + "_vol_favor"].sum(), g[inst + "_vol_contra"].sum()
                mf, mc = g[inst + "_mueve_favor"].sum(), g[inst + "_mueve_contra"].sum()
                jf, jc = g[inst + "_imp_favor"].sum(), g[inst + "_imp_contra"].sum()
                print("       %-17s %4d | %7.0f / %7.0f (%4.2f)          | %8.0f / %8.0f (%4.2f)           |  %5.2f / %5.2f              |"
                      "  %7.0f / %7.0f (%4.2f)                       |  %5.3f / %5.3f" %
                      (NOMBRE[c], len(g), nf / len(g), nc / len(g), nf / nc if nc else np.nan, vf / len(g), vc / len(g), vf / vc if vc else np.nan,
                       vf / nf if nf else np.nan, vc / nc if nc else np.nan, mf / len(g), mc / len(g), mf / mc if mc else np.nan,
                       jf / nf if nf else np.nan, jc / nc if nc else np.nan))
            g = g0[g0["clase"] == "SC"]
            if len(g):
                print("       en las piernas SC: %.0f %% con MAS ordenes a favor que en contra; %.0f %% con MAS ordenes que corrieron la punta a favor" %
                      (100 * (g[inst + "_n_favor"] > g[inst + "_n_contra"]).mean(), 100 * (g[inst + "_mueve_favor"] > g[inst + "_mueve_contra"]).mean()))
    T.to_parquet(os.path.join(C.DATOS, "r26_07-%s.parquet" % tanda), index=False)


if __name__ == "__main__":
    main()
