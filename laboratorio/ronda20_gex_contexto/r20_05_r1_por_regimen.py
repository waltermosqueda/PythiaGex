# -*- coding: utf-8 -*-
"""RONDA 20, paso 5: S1 del PREREGISTRO. La regla R1 de la ronda 18 TAL CUAL (clase Maquina y logica de correr() copiadas de
ronda18_senal_ola/r18_01_senal.py, solo la regla R1), orden por orden sobre las cintas crudas de ES, con X de la ronda 19, en
todas las ruedas de ES; despues se le pega a cada operacion el regimen de gamma del minuto de la entrada (tabla por minuto,
dato <= 60 min). Entrada y salida en la raya de la ola (orden stop), una posicion por vez, entradas 9:30-15:55 NY, cierre
15:59:59. Costo 0,50 por operacion.
Uso: python -I r20_05_r1_por_regimen.py"""
import csv
import datetime as dt
import glob
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import pandas as pd

import r20_comun as C

FLUJO = os.path.join(C.APP, "ATAS", "PythiaGex", "flujo")


class Maquina:
    """Copia literal de la clase de la ronda 18 (r18_01_senal.py): zigzag en vivo, cum incluido en cada extremo."""
    def __init__(s, X):
        s.X = X; s.dir = 0; s.ext = None; s.ext_t = None; s.ext_cum = 0
        s.lo = s.hi = None; s.lo_t = s.hi_t = None; s.lo_cum = s.hi_cum = 0
        s.pivotes = []

    def paso(s, t, mn, mx, cum):
        if s.dir == 0:
            if s.lo is None or mn < s.lo: s.lo, s.lo_t, s.lo_cum = mn, t, cum
            if s.hi is None or mx > s.hi: s.hi, s.hi_t, s.hi_cum = mx, t, cum
            if s.hi - s.lo >= s.X:
                if s.hi_t > s.lo_t: s.pivotes.append((s.lo_t, s.lo, 'min', t, s.lo_cum)); s.dir = 1; s.ext, s.ext_t, s.ext_cum = s.hi, s.hi_t, s.hi_cum
                else: s.pivotes.append((s.hi_t, s.hi, 'max', t, s.hi_cum)); s.dir = -1; s.ext, s.ext_t, s.ext_cum = s.lo, s.lo_t, s.lo_cum
            return
        if s.dir == 1:
            if mx > s.ext: s.ext, s.ext_t, s.ext_cum = mx, t, cum
            elif s.ext - mn >= s.X:
                s.pivotes.append((s.ext_t, s.ext, 'max', t, s.ext_cum)); s.dir = -1; s.ext, s.ext_t, s.ext_cum = mn, t, cum
        else:
            if mn < s.ext: s.ext, s.ext_t, s.ext_cum = mn, t, cum
            elif mx - s.ext >= s.X:
                s.pivotes.append((s.ext_t, s.ext, 'min', t, s.ext_cum)); s.dir = 1; s.ext, s.ext_t, s.ext_cum = mx, t, cum


def utc_txt(dia, h, m):
    d = dt.date.fromisoformat(dia)
    return dt.datetime(d.year, d.month, d.day, h, m, tzinfo=C.NY).astimezone(C.UTC).strftime("%Y-%m-%dT%H:%M:%S")


def archivo(dia):
    for f in sorted(glob.glob(os.path.join(FLUJO, "cinta-ES-*.csv"))):
        p = os.path.basename(f)[:-4].split("-")
        if len(p) == 4 and p[3][:8] == dia.replace("-", "") and os.path.exists(f + ".listo"):
            return f
    return None


def correr_r1(dia, X):
    ola, marea = Maquina(X), Maquina(4 * X)
    ent0, ent1, cierre = utc_txt(dia, 9, 30), utc_txt(dia, 15, 55), utc_txt(dia, 16, 0)
    pos = None; ops = []; cum = 0.0; ult = None; t_ult = None; npiv = 0

    def cerrar(t, p, motivo):
        nonlocal pos
        lado, t0, p0, ctx = pos
        ops.append(dict(dia=dia, X=X, lado=lado, t_in=t0, p_in=p0, t_out=t, p_out=p, pts=(p - p0) * lado, motivo=motivo, **ctx))
        pos = None
    with open(archivo(dia), newline='') as f:
        rd = csv.reader(f); next(rd)
        for x in rd:
            t = x[0]
            if t >= cierre and t_ult is not None and t_ult < cierre:
                if pos: cerrar(t_ult, ult, 'hora')
            a, b = float(x[1]), float(x[2]); v = float(x[3]); s = int(x[4])
            mn, mx = (a, b) if a <= b else (b, a)
            cum += v * s
            ola.paso(t, mn, mx, cum); marea.paso(t, mn, mx, cum)
            ult, t_ult = b, t
            if len(ola.pivotes) == npiv: continue
            npiv = len(ola.pivotes)
            te, pp, tipo, tc, c = ola.pivotes[-1]
            giro = 1 if tipo == 'min' else -1
            nivel = pp + X if giro == 1 else pp - X
            mdir = marea.dir
            mdel = (cum - marea.pivotes[-1][4]) if marea.pivotes else 0.0
            mcon = (mdir == 1 and mdel > 0) or (mdir == -1 and mdel < 0)
            ventana = ent0 <= t < ent1
            ctx = dict(marea_dir=mdir, marea_delta=round(mdel), marea_con=mcon, origen=pp, t_origen=te)
            r1 = mdir == giro and mcon
            if pos and pos[0] == -giro: cerrar(t, nivel, 'ola')
            if pos or not ventana: continue
            if r1:
                pos = (giro, t, nivel, ctx)
    if pos: cerrar(t_ult, ult, 'fin')
    return ops


def main():
    todas = []
    for dia in C.dias_es():
        C.frenar_si_falta_ram(2.0)
        X = float(C.meta19(dia)["X"])
        if archivo(dia) is None:
            print("FALTA cinta", dia); continue
        o = correr_r1(dia, X)
        todas += o
        print("%s X %.2f R1 %3d ops bruto %+7.2f" % (dia, X, len(o), sum(q["pts"] for q in o)), flush=True)
    d = pd.DataFrame(todas)
    d["muestra"] = d.dia.map(C.muestra_de)
    d["ts_min"] = pd.to_datetime(d.t_in, utc=True).dt.floor("min")
    tab = pd.read_parquet(os.path.join(C.SCR, "gex_minuto.parquet"))
    tab["ts_min"] = pd.to_datetime(tab.ts_utc, utc=True)
    tab["gex_estr"] = tab.gex_neto - tab.gex_0dte
    tab["valido"] = tab.hay_cadena & (tab.edad_s <= 3600)
    d = d.merge(tab[["ts_min", "minuto", "valido", "gex_neto", "gex_estr", "gex_norm", "dist_flip_X", "edad_s"]], on="ts_min", how="left")
    d["neg_total"] = np.where(d.valido == True, (d.gex_neto < 0).astype(float), np.nan)
    d["neg_estr"] = np.where(d.valido == True, (d.gex_estr < 0).astype(float), np.nan)
    d["gana"] = (d.pts > 0).astype(float)
    d["neto"] = d.pts - 0.50
    d["largo_ola_X"] = np.where(d.motivo == "ola", d.pts / d.X + 2.0, np.nan)
    d.to_csv(os.path.join(C.SCR, "r20_05_r1_operaciones.csv"), index=False)
    PER = ["diseno", "confirma", "guardada"]
    for col, nom in (("neg_total", "reg_total (primaria)"), ("neg_estr", "reg_estructural (sin 0DTE)")):
        print("\n== R1 por regimen en la entrada, %s (costo 0,50) ==" % nom)
        for per in PER + ["todo"]:
            g = d if per == "todo" else d[d.muestra == per]
            s = "  %-9s todas n %4d acierto %4.1f %% neto/op %+.2f" % (per, len(g), 100 * g.gana.mean(), g.neto.mean())
            g = g[g[col].notna()]
            for v, nm in ((1.0, "NEG"), (0.0, "POS")):
                h = g[g[col] == v]
                if len(h) == 0:
                    s += " | %s n 0" % nm; continue
                pr = h.groupby("dia")["neto"].sum()
                s += " | %s n %4d (%2d r) acierto %4.1f %% bruto/op %+.2f neto/op %+.2f (t rueda %+.2f) ola %.2f X (>2X %.0f %%)" % (
                    nm, len(h), h.dia.nunique(), 100 * h.gana.mean(), h.pts.mean(), h.neto.mean(), C.t_por_rueda(pr.values),
                    h.largo_ola_X.median(), 100 * (h.largo_ola_X > 2).mean())
            if len(g) > 10:
                b, t = C.t_cluster(g["neto"], g[col], g["dia"])
                bl, tl = C.t_cluster(g["largo_ola_X"], g[col], g["dia"])
                s += " | dif NEG-POS neto %+.2f (t agr %+.2f), ola %+.2f X (t agr %+.2f)" % (b, t, bl, tl)
            print(s)


if __name__ == "__main__":
    main()
