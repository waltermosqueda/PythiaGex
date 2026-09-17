# -*- coding: utf-8 -*-
"""
ubicacion_04_micro.py — DESCRIPTIVO, corrido DESPUES de la confirmacion, sobre las 20 sesiones. No elige nada ni cambia ningun veredicto.
Pregunta: el precio se comporta distinto al llegar a un nivel "que todos miran" que al llegar a un precio cualquiera?
"""
import sys, os, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import base as B, ubicacion_lib as UL
import numpy as np, pandas as pd

t0 = time.time(); T = B.todas_las_sesiones(); S = sorted(T)
ev = UL.pegar_resultados(UL.eventos(T, S), T); ev["G"] = ev["grupo"]
pl = pd.concat([UL.eventos(T, S, placebo_corr=c, etiqueta="pla%d" % int(c)).assign(rep=int(c)) for c in UL.PLACEBO_CORRIMIENTOS], ignore_index=True)
pl = UL.pegar_resultados(pl, T); pl["G"] = "PLA"


def pasada(e, T_, H=120):
    """Cuanto PASA el precio del nivel en los H s siguientes (puntos mas alla del nivel, en el sentido de la llegada) y cuanto DEVUELVE."""
    ps = np.zeros(len(e)); dv = np.zeros(len(e)); k = 0
    for s, g in e.groupby("ses", sort=False):
        hi = T_[s]["alto"].to_numpy("float64"); lo = T_[s]["bajo"].to_numpy("float64")
        for i, (t, d, L) in zip(g.index, zip(g["t"].to_numpy(), g["dir"].to_numpy(), g["L"].to_numpy())):
            a = hi[t:t + H + 1].max(); b = lo[t:t + H + 1].min()
            ps[e.index.get_loc(i)] = (a - L) if d > 0 else (L - b); dv[e.index.get_loc(i)] = (L - b) if d > 0 else (a - L)
    return ps, dv


todo = pd.concat([ev, pl[pl["rep"] == 13]], ignore_index=True)   # una sola replica del placebo para las distribuciones (las otras dos, para el rebote)
todo["pasa120"], todo["devuelve120"] = pasada(todo, T)
print("=== 20 sesiones. Toques por grupo (grupo principal del evento) y que hace el precio despues ===")
for G, g in list(todo.groupby("G")):
    y = g["y8"].to_numpy(); d = g["dir"].to_numpy(); ok = y != 0; reb = (y[ok] * -d[ok]) > 0
    pd_ = pd.Series(reb).groupby(g["ses"].to_numpy()[ok]).mean(); tt = (pd_.mean() - .5) / (pd_.std(ddof=1) / np.sqrt(len(pd_)))
    print("%-4s n %5d | rebote +-8/600 %.1f %% (t entre dias %+.2f, dias > 50 %%: %d de %d) | seg. hasta resolver: mediana %.0f | pasa el nivel en 120 s: mediana %.2f pts, >=2: %.0f %%, >=4: %.0f %%, >=8: %.0f %% | devuelve en 120 s: mediana %.2f, >=8: %.0f %% | mov. contra la llegada 10/30/60 s: %+.2f / %+.2f / %+.2f"
          % (G, len(g), 100 * reb.mean(), tt, (pd_ > .5).sum(), len(pd_), np.median(g.loc[g["s8"] > 0, "s8"]), g["pasa120"].median(), 100 * (g["pasa120"] >= 2).mean(), 100 * (g["pasa120"] >= 4).mean(),
             100 * (g["pasa120"] >= 8).mean(), g["devuelve120"].median(), 100 * (g["devuelve120"] >= 8).mean(), *(float((g["mv%d" % k] * -g["dir"]).mean()) for k in (10, 30, 60))))

print("\nrebote del placebo de lugar, las tres replicas, 20 sesiones:")
for c in UL.PLACEBO_CORRIMIENTOS:
    g = pl[pl["rep"] == int(c)]; ok = g["y8"] != 0; print("  corrida %d: n %d rebote %.1f %%" % (c, ok.sum(), 100 * ((g.loc[ok, "y8"] * -g.loc[ok, "dir"]) > 0).mean()))

print("\n=== segundos hasta resolver el +-8, a IGUAL actividad (terciles de V30 de todos los toques) ===")
q = pd.qcut(todo["V30"].rank(method="first"), 3, labels=["cinta lenta", "media", "rapida"])
print(todo[todo["s8"] > 0].groupby([q[todo["s8"] > 0], "G"])["s8"].median().unstack().round(0).to_string())
print("\npasa el nivel >= 4 pts en 120 s, a igual actividad (%):")
print((100 * todo.assign(p4=todo["pasa120"] >= 4).groupby([q, "G"])["p4"].mean().unstack()).round(0).to_string())

print("\n=== el delta ACOMPAÑA la llegada? (%% de toques con el delta a favor de la llegada) ===")
for G, g in todo.groupby("G"):
    print("  %-4s delta 30 s a favor: %.0f %% | 60 s: %.0f %% | 5 min: %.0f %% | pasivo 30 s a favor: %.0f %% | toques con ALGO de absorcion limpia del lado del nivel: %.0f %% (mediana %.2f %% del volumen)"
          % (G, 100 * (g["fd30"] > 0).mean(), 100 * (g["fd60"] > 0).mean(), 100 * (g["fd300"] > 0).mean(), 100 * (g["pas30"] > 0).mean(), 100 * (g["a_niv30"] > 0).mean(), 100 * g["a_niv30"].median()))

print("\n=== rebote por nivel, explorar contra confirmar (para ver si lo 'lindo' de explorar repite) ===")
ex = ev.assign(nv=ev["niveles"].str.split("+")).explode("nv"); ex["nv"] = ex["nv"].str.replace(r"^R\d*00$", "R100", regex=True).str.replace(r"^R\d*50$", "R50", regex=True)
ex["tramo"] = np.where(ex["ses"] < B.CORTE_CONFIRMAR, "explorar", "confirmar"); filas = []
for (nv, tr), g in ex.groupby(["nv", "tramo"]):
    g = g.copy(); g["lado"] = -g["dir"]; g = UL.desagrupar(g); j = UL.juzgar_variante(g, 8, nv); filas.append(dict(nivel=nv, tramo=tr, n=j.get("n", 0), rebote=j.get("acierto")))
P = pd.DataFrame(filas).pivot(index="nivel", columns="tramo", values=["n", "rebote"]); print(P.to_string())
a = P["rebote"]["explorar"]; b = P["rebote"]["confirmar"]; m = a.notna() & b.notna(); print("correlacion del rebote por nivel entre explorar y confirmar: %.2f (n niveles %d)" % (np.corrcoef(a[m], b[m])[0, 1], m.sum()))
print("%.0f s" % (time.time() - t0))
