# -*- coding: utf-8 -*-
"""RONDA 20, paso 0: inventario de cadenas por rueda de ES (fuentes, fotos con dato en la rueda, huecos), chequeo del tercer
viernes (OI del vencimiento del dia contra los otros viernes) y calibracion de la alineacion de la base (ES - SPX):
muestra = precio de la cinta en (hora del dato + d) - spot de la foto, para d en -120..+120 s; se elige la d de menor dispersion.
No mira resultados de ninguna hipotesis. Uso: python -I r20_00_inventario.py"""
import datetime as dt
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import pandas as pd

import r20_comun as C

filas, alin = [], []
for dia in C.dias_es():
    C.frenar_si_falta_ram(2.0)
    fotos = C.fotos_para_rueda(dia)
    a, b = C.rth_utc(dia)
    en = [f for f in fotos if f["dato"] is not None and a <= f["dato"] < b]
    gen_rth = [f for f in fotos if a <= f["gen"] < b]
    gaps = np.diff([f["gen"].timestamp() for f in gen_rth]) if len(gen_rth) > 1 else np.array([np.nan])
    por_f = {fu: sum(1 for f in gen_rth if f["fuente"] == fu) for fu in C.FUENTES}
    # OI del vencimiento del dia (0DTE) en la primera foto con dato en la rueda
    oi0 = np.nan
    if en:
        f = en[0]
        if dia in f["vencs"]:
            k = f["vencs"].index(dia)
            F = f["filas"]
            oi0 = float(F[F[:, 1] == k][:, 2:4].sum())
    # alineacion de la base: precio de la cinta a dato + d contra el spot de la foto
    if en and dia in ("2026-09-10", "2026-10-07", "2026-10-28", "2026-11-12", "2026-11-18"):
        ci = C.cinta19(dia)
        ns = ci["ns"].to_numpy(); px = ci["ultimo"].to_numpy()
        for d in (-120, -60, -30, -15, 0, 15, 30, 60, 120):
            m = []
            for f in en:
                t = int((f["dato"].timestamp() + d) * 1e9)
                i = np.searchsorted(ns, t, side="right") - 1
                if i >= 0 and f["spot_idx"]:
                    m.append(px[i] - f["spot_idx"])
            m = np.asarray(m)
            if len(m) > 5:
                med = np.median(m)
                alin.append({"dia": dia, "desfase_s": d, "n": len(m), "mediana": med, "mad": float(np.median(np.abs(m - med)))})
    primera = min((f["dato"] for f in en), default=None)
    filas.append({"dia": dia, "muestra": C.muestra_de(dia), "fotos": len(fotos), "fotos_gen_en_rueda": len(gen_rth),
                  "fotos_dato_en_rueda": len(en), **{"n_" + k: v for k, v in por_f.items()},
                  "hueco_max_min": float(np.nanmax(gaps) / 60) if len(gaps) else np.nan,
                  "hueco_mediano_s": float(np.nanmedian(gaps)) if len(gaps) else np.nan,
                  "primer_dato_ny": primera.astimezone(C.NY).strftime("%H:%M:%S") if primera else None,
                  "oi_0dte_primera": oi0, "viernes": dt.date.fromisoformat(dia).weekday() == 4,
                  "base_arch_mediana": float(np.nanmedian([f["base_arch"] for f in en if f["base_arch"] is not None]))
                  if any(f["base_arch"] is not None for f in en) else np.nan})
    print(filas[-1], flush=True)
inv = pd.DataFrame(filas)
inv.to_csv(os.path.join(C.AQUI, "r20_00_inventario.csv"), index=False)
print(inv.to_string())
print("\nViernes: OI del 0DTE en la primera foto de la rueda (tercer viernes = 18-09 y 16-10)")
print(inv[inv.viernes][["dia", "oi_0dte_primera"]].to_string())
if alin:
    al = pd.DataFrame(alin)
    al.to_csv(os.path.join(C.AQUI, "r20_00_alineacion_base.csv"), index=False)
    print("\nAlineacion de la base (MAD por desfase, mediana entre dias):")
    print(al.groupby("desfase_s")[["mad", "mediana"]].median().to_string())
    print(al.pivot(index="dia", columns="desfase_s", values="mad").round(3).to_string())
