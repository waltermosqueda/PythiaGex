# -*- coding: utf-8 -*-
"""RONDA 21, paso 9: INVENTARIO y compactacion de las cintas de MNQ (muestra nueva para la regla congelada). Una cinta por vez,
prioridad baja. Mismo formato y misma compactacion que la R19 (r19_00_inventario.compactar), en scratchpad/r21/mnq/cinta.
Sin resultados de reglas: conteos, horarios, huecos, apertura/cierre RTH y la base contra las velas de 5 min de MNQ continuo y de
MNQZ6 (para ubicar el roll U6 -> Z6 y medir el spread). Uso: python -I r21_09_mnq_inventario.py"""
import glob, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r21_comun as K
import numpy as np, pandas as pd
import r19_comun as C19
import r19_00_inventario as INV

MNQ = os.path.join(K.SCR, "mnq"); CINTA = os.path.join(MNQ, "cinta")
os.makedirs(CINTA, exist_ok=True)
C19.CINTA = CINTA            # la compactacion de la R19 escribe en C19.ruta_compacta -> esta carpeta
VELAS = os.path.join(os.path.dirname(K.AQUI), "dom", "ronda3", "velas")


def crudas():
    out = {}
    for f in sorted(glob.glob(os.path.join(C19.FLUJO, "cinta-MNQ*.csv"))):
        p = os.path.basename(f)[:-4].split("-")
        if len(p) != 4 or not p[1].startswith("MNQ"): continue
        fin = p[3][:8]; dia = "%s-%s-%s" % (fin[:4], fin[4:6], fin[6:8])
        out[dia] = dict(archivo=f, simbolo=p[1], bytes=os.path.getsize(f), listo=os.path.exists(f + ".listo"))
    return out


def velas(nombre):
    v = pd.read_csv(os.path.join(VELAS, nombre), usecols=["t", "c"])
    v["t"] = pd.to_datetime(v["t"]).dt.tz_localize("UTC")
    return v.set_index("t")["c"]


def main():
    t0 = time.time(); filas = []
    cont = velas("velas-MNQ-m5.csv"); z6 = velas("velas-MNQZ6-m5.csv")
    for dia, info in sorted(crudas().items()):
        K.frenar_si_falta_ram(2.0)
        r = dict(dia=dia, simbolo=info["simbolo"], bytes=info["bytes"], listo=info["listo"])
        if info["bytes"] < 100000:
            r["nota"] = "vacia (solo encabezado)"; filas.append(r); print(dia, "vacia", flush=True); continue
        ruta = C19.ruta_compacta("MNQ", dia)
        if os.path.exists(ruta):
            d = pd.read_parquet(ruta); n0 = len(d)
        else:
            d, n0 = INV.compactar("MNQ", dia, info["archivo"])
        r.update(INV.resumen("MNQ", dia, d, n0))
        r["simbolo"] = info["simbolo"]
        # base contra las velas de 5 min: precio de la cinta al cierre de cada vela de la rueda menos el cierre de la vela
        ns = d["ns"].to_numpy(); ult = d["ultimo"].to_numpy()
        s = d["ny_s"].to_numpy(); rth = (s >= 9.5 * 3600) & (s < 16 * 3600)
        if rth.any():
            n_ini, n_fin = ns[rth][0], ns[rth][-1]
            for nom, serie in (("vs_continuo", cont), ("vs_Z6", z6)):
                ini = serie.index.astype("int64")
                sel = (ini >= n_ini) & (ini + 300 * 10 ** 9 <= n_fin)
                if sel.sum() >= 5:
                    fin_ns = ini[sel] + 300 * 10 ** 9          # cierre de la vela de 5 min (t = apertura de la vela)
                    k = np.searchsorted(ns, fin_ns, side="left") - 1     # ultimo precio de la cinta ANTES del cierre de la vela
                    dif = ult[k] - serie.to_numpy()[sel]
                    r[nom + "_mediana"] = round(float(np.nanmedian(dif)), 2); r[nom + "_n"] = int(np.isfinite(dif).sum())
        filas.append(r)
        print(dia, info["simbolo"], len(d), "ordenes | RTH %s-%s | base vs continuo %s, vs Z6 %s | %.1f s | RAM %.1f GB" % (
            r.get("rth_desde"), r.get("rth_hasta"), r.get("vs_continuo_mediana"), r.get("vs_Z6_mediana"), time.time() - t0, K.ram_libre_gb()), flush=True)
        del d
    inv = pd.DataFrame(filas)
    if "p_inicio" in inv:
        inv["salto_vs_previa"] = (inv["p_inicio"] - inv["p_fin"].shift(1)).round(2)
    inv.to_csv(os.path.join(K.AQUI, "salidas", "mnq_inventario.csv"), index=False)
    pd.set_option("display.width", 250); pd.set_option("display.max_columns", 40)
    cols = [c for c in ("dia", "simbolo", "ordenes", "duplicados", "desde_ny_h", "hasta_ny_h", "rth_o", "rth_c", "rth_h", "rth_l", "rth_hueco_max_s",
                        "rth_huecos_60s", "vs_continuo_mediana", "vs_Z6_mediana", "salto_vs_previa", "nota") if c in inv.columns]
    print(inv[cols].to_string())
    print("total %.1f s" % (time.time() - t0))


if __name__ == "__main__":
    main()
