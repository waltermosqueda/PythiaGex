# -*- coding: utf-8 -*-
"""TRES DIAS REALES DE A3 DIBUJADOS (27-11-2026, para el operador). Desde la cinta cruda de la sonda (ES orden por orden): velas de 5 min
de la rueda, el VWAP de la regla ((primero+ultimo)/2 x volumen de cada orden, desde las 9:30), el balance inicial y la franja donde mira A3;
encima, TODAS las operaciones de A3 de ese dia tal como las anoto la R23 (salidas/ops-cinta-A3.csv, verificadas 62 de 62 con mi codigo).
Controla que el VWAP de la cinta al cierre de la vela de entrada sea el de la operacion. Uso: python -I dibujar_ejemplos_A3.py"""
import sys, os, glob
sys.stdout.reconfigure(encoding="utf-8")
import numpy as np, pandas as pd
import matplotlib; matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle
AQUI = os.path.dirname(os.path.abspath(__file__)); R23 = os.path.dirname(AQUI)
FLUJO = os.path.join(os.environ["APPDATA"], "ATAS", "PythiaGex", "flujo")
DIAS = [("2026-11-24", "Gana: llega al VWAP"), ("2026-09-18", "Pierde: salta el stop"), ("2026-10-01", "Pierde: no llega y se cierra a las 16:00")]
def ar(x, d=2): return ("{:,.%df}" % d).format(x).replace(",", "X").replace(".", ",").replace("X", ".")

porfecha = {}
for f in glob.glob(os.path.join(FLUJO, "cinta-ES*.csv")):
    hasta = os.path.basename(f)[:-4].split("-")[-1]
    dia = pd.Timestamp(hasta[:8] + "T" + hasta[8:], tz="UTC").tz_convert("America/New_York").strftime("%Y-%m-%d")
    clave = (os.path.exists(f[:-4] + ".listo"), hasta)
    if dia not in porfecha or clave > porfecha[dia][0]: porfecha[dia] = (clave, f)
ops = pd.read_csv(os.path.join(R23, "salidas", "ops-cinta-A3.csv"))

fig, ejes = plt.subplots(len(DIAS), 1, figsize=(13, 5.2 * len(DIAS)))
for ax, (dia, titulo) in zip(ejes, DIAS):
    df = pd.read_csv(porfecha[dia][1], usecols=["t", "primero", "ultimo", "vol"])
    t = pd.to_datetime(df["t"], format="%Y-%m-%dT%H:%M:%S.%f").dt.tz_localize("UTC").dt.tz_convert("America/New_York")
    ini = pd.Timestamp(dia + " 09:30:00", tz="America/New_York"); s = (t - ini).dt.total_seconds()
    m = (s >= 0) & (s < 23400); x = df[m].copy(); x["s"] = s[m]; x["b"] = (x["s"] // 300).astype(int)
    x["lo"] = x[["primero", "ultimo"]].min(axis=1); x["hi"] = x[["primero", "ultimo"]].max(axis=1)
    x["pv"] = (x.primero + x.ultimo) / 2 * x.vol
    g = x.groupby("b").agg(o=("primero", "first"), h=("hi", "max"), l=("lo", "min"), c=("ultimo", "last"), pv=("pv", "sum"), v=("vol", "sum"))
    vw = g.pv.cumsum() / g.v.cumsum()  # VWAP al cierre de cada vela
    hora = lambda b: 9.5 + b * 5 / 60  # eje en horas NY
    for b, r in g.iterrows():
        col = "#2e7d32" if r.c >= r.o else "#c62828"
        ax.plot([hora(b) + 2.5 / 60] * 2, [r.l, r.h], color=col, lw=0.8, zorder=2)
        ax.add_patch(Rectangle((hora(b) + 0.6 / 60, min(r.o, r.c)), 3.8 / 60, max(abs(r.c - r.o), 0.12), color=col, zorder=3))
    ax.step([hora(b) + 5 / 60 for b in vw.index], vw.values, where="post", color="#1565c0", lw=2, zorder=4, label="VWAP (precio promedio del dia)")
    ib = x[x.s < 3600]; ax.axhspan(ib.lo.min(), ib.hi.max(), xmin=0, xmax=1 / 6.5, color="#9e9e9e", alpha=0.18, zorder=1)
    ax.axhline(ib.hi.max(), color="#9e9e9e", lw=0.8, ls=":", zorder=1); ax.axhline(ib.lo.min(), color="#9e9e9e", lw=0.8, ls=":", zorder=1)
    ax.axvspan(13.0, 15.5, color="#fff59d", alpha=0.35, zorder=0, label="donde mira A3 (13:00 a 15:30)")
    total = 0.0
    for _, op in ops[ops.fecha == dia].iterrows():
        he = int(op.hora_entrada[:2]) + int(op.hora_entrada[3:5]) / 60; hs = int(op.hora_salida[:2]) + int(op.hora_salida[3:5]) / 60 + int(op.hora_salida[6:8]) / 3600
        venta = op.lado == "VENTA"
        b_ent = int(round((he - 9.5) * 12)) - 1
        print("%s %s %s: VWAP de la cinta al cierre de la vela de entrada %.4f | el de la operacion %.4f | %s" % (dia, op.hora_entrada, op.lado, vw.loc[b_ent], op.vwap, "IGUAL" if abs(vw.loc[b_ent] - op.vwap) < 0.01 else "DISTINTO"))
        ax.plot([he, hs], [op.stop, op.stop], color="#c62828", lw=1.6, ls="--", zorder=5)
        ax.annotate("stop " + ar(op.stop), (he, op.stop), xytext=(4, 4 if venta else -12), textcoords="offset points", color="#c62828", fontsize=9, bbox=dict(boxstyle="round,pad=0.25", fc="white", ec="none", alpha=0.85), zorder=10)
        ax.scatter([he], [op.entrada], marker="v" if venta else "^", s=150, color="black", zorder=7)
        ax.annotate("%s %s\n%s" % ("VENTA" if venta else "COMPRA", ar(op.entrada), op.hora_entrada[:5]), (he, op.entrada),
                    xytext=(-70, 12 if venta else -34), textcoords="offset points", fontsize=9, fontweight="bold", bbox=dict(boxstyle="round,pad=0.25", fc="white", ec="none", alpha=0.85), zorder=10)
        col = "#2e7d32" if op.pts > 0 else "#c62828"
        ax.scatter([hs], [op.salida], marker="o", s=110, facecolor="white", edgecolor=col, lw=2.5, zorder=7)
        motivo = {"objetivo": "llego al VWAP", "stop": "stop", "fin": "cierre 16:00"}[op.motivo]
        ax.annotate("salida %s (%s)\n%s puntos" % (ar(op.salida), motivo, ("+" if op.pts > 0 else "") + ar(op.pts)), (hs, op.salida),
                    xytext=((-150 if hs > 15.4 else 8), (-34 if venta else 10)), textcoords="offset points", fontsize=9, color=col, fontweight="bold", bbox=dict(boxstyle="round,pad=0.25", fc="white", ec="none", alpha=0.85), zorder=10)
        total += op.pts
    ax.set_title("%s  |  %s  |  %s puntos ese dia (antes del costo)" % (pd.Timestamp(dia).strftime("%d-%m-%Y"), titulo, ("+" if total > 0 else "") + ar(total)), fontsize=12, loc="left")
    ax.set_xlim(9.5, 16.0); ax.set_xticks([9.5 + i * 0.5 for i in range(14)]); ax.set_xticklabels(["%d:%02d" % (int(h), int(round((h % 1) * 60))) for h in [9.5 + i * 0.5 for i in range(14)]], fontsize=8)
    ax.yaxis.set_major_formatter(matplotlib.ticker.FuncFormatter(lambda v, p: ar(v, 0)))
    ax.grid(alpha=0.25); ax.set_ylabel("ES (puntos)")
    ax.text(9.55, ib.hi.max(), " balance inicial\n (9:30-10:30)", fontsize=8, color="#616161", va="bottom")
ejes[0].legend(loc="lower right", fontsize=9)
fig.text(0.01, 0.003, "A3 en prueba (NO validada): despues de la 1, si el precio se alejo del VWAP y ya volvio la mitad desde el extremo, ir hacia el VWAP;\n"
         "stop detras del extremo; salida en el VWAP o a las 16:00. Hora de Nueva York. Datos: cinta orden por orden de la sonda (ES).", fontsize=8.5, color="#424242")
fig.tight_layout(rect=(0, 0.022, 1, 1))
salida = os.path.join(R23, "salidas", "ejemplos_A3_cinta.png"); fig.savefig(salida, dpi=105); print("guardado", salida)
