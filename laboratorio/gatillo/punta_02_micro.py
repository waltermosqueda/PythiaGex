# -*- coding: utf-8 -*-
"""punta_02_micro.py — microestructura en EXPLORAR: cuanto se mueve el MID despues (1 s a 600 s) segun el decil de cada señal de punta / flujo pasivo.
El error se mide ENTRE DIAS (media de medias diarias y t con 12 dias). Nada de esto elige finalistas: es para entender."""
import sys; sys.path.insert(0, r"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\gatillo")
import numpy as np, pandas as pd, base as B, punta_lib as L

HS = (1, 5, 10, 30, 60, 300, 600)
T = B.todas_las_sesiones(); E = B.explorar(T); del T
partes = []
for s, d in E.items():
    f = L.rasgos(d); ok = L.elegible(d)
    for h in HS: f["f%d" % h] = f["mid"].shift(-h) - f["mid"]
    f["fu10"] = d["ultimo"].shift(-10) - d["ultimo"]; f["dia"] = s
    partes.append(f[ok])
R = pd.concat(partes); del partes
print("segundos elegibles en explorar:", len(R), "| dias:", R["dia"].nunique())


def t_entre_dias(v):
    v = np.asarray(v, float); v = v[~np.isnan(v)]
    return (v.mean(), v.mean() / (v.std(ddof=1) / np.sqrt(len(v))) if len(v) > 2 and v.std(ddof=1) > 0 else np.nan)


print("\n=== 1. Seguir la señal en los deciles extremos: (media decil 10 - media decil 1) / 2, en PUNTOS del mid. Entre parentesis la t entre dias ===")
print("%-8s" % "señal" + "".join("%16s" % ("%d s" % h) for h in HS))
SEN = ["qi", "qi10", "qi30", "mp_mid", "mp_ult"] + ["z%s%d" % (k, W) for k in "POD" for W in (10, 30, 60, 300)]
for c in SEN:
    x = R[c]; lo, hi = x.quantile(0.1), x.quantile(0.9); fila = "%-8s" % c
    for h in HS:
        g = R.assign(top=x >= hi, bot=x <= lo)
        dd = g[g["top"]].groupby("dia")["f%d" % h].mean() - g[g["bot"]].groupby("dia")["f%d" % h].mean()
        m, t = t_entre_dias(dd / 2.0); fila += "%9.3f (%4.1f)" % (m, t)
    print(fila)

print("\n=== 2. QI instantaneo: perfil completo por decil, movimiento medio del mid en puntos ===")
R["dq"] = pd.qcut(R["qi"].rank(method="first"), 10, labels=False) + 1
tab = R.groupby("dq")[["qi"] + ["f%d" % h for h in (1, 5, 10, 30, 60)]].mean().round(3); print(tab.to_string())
print("prob. de que el mid a 5 s este ARRIBA / ABAJO / igual, por decil de QI:")
for q in (1, 2, 5, 9, 10):
    v = R.loc[R["dq"] == q, "f5"]; print("  decil %2d: sube %.1f %% | baja %.1f %% | igual %.1f %%" % (q, 100 * (v > 0).mean(), 100 * (v < 0).mean(), 100 * (v == 0).mean()))

print("\n=== 3. Delta fuerte (|zD60| >= 1,5): movimiento del mid EN LA DIRECCION DEL DELTA segun que hace el PASIVO (zP60 alineado con el delta) ===")
a = np.sign(R["zD60"]); fuerte = R["zD60"].abs() >= 1.5; pal = R["zP60"] * a
cortes = [-np.inf, -1.25, -0.5, 0.5, 1.25, np.inf]; nombres = ["pasivo MUY en contra", "pasivo en contra", "pasivo neutro", "pasivo a favor", "pasivo MUY a favor"]
G = R[fuerte].assign(b=pd.cut(pal[fuerte], cortes, labels=nombres), a=a[fuerte])
print("%-22s %8s" % ("", "seg") + "".join("%16s" % ("%d s" % h) for h in (10, 30, 60, 300, 600)))
for nb in nombres:
    g = G[G["b"] == nb]; fila = "%-22s %8d" % (nb, len(g))
    for h in (10, 30, 60, 300, 600):
        dd = (g["f%d" % h] * g["a"]).groupby(g["dia"]).mean(); m, t = t_entre_dias(dd); fila += "%9.3f (%4.1f)" % (m, t)
    print(fila)
print("idem con ventana de 300 s (|zD300| >= 1,5, pasivo zP300):")
a = np.sign(R["zD300"]); fuerte = R["zD300"].abs() >= 1.5; pal = R["zP300"] * a
G = R[fuerte].assign(b=pd.cut(pal[fuerte], cortes, labels=nombres), a=a[fuerte])
for nb in nombres:
    g = G[G["b"] == nb]; fila = "%-22s %8d" % (nb, len(g))
    for h in (10, 30, 60, 300, 600):
        dd = (g["f%d" % h] * g["a"]).groupby(g["dia"]).mean(); m, t = t_entre_dias(dd); fila += "%9.3f (%4.1f)" % (m, t)
    print(fila)

print("\n=== 4. Tras un movimiento de >= 15,75 pts en 60 s: movimiento del mid EN LA DIRECCION DEL MOVIMIENTO segun como quedo cargada la punta (QI_10 alineado) ===")
a = np.sign(R["ret60"]); mov = R["ret60"].abs() >= L.U["mov_60"]; qal = R["qi10"] * a
cortes2 = [-np.inf, -0.17, -0.05, 0.05, 0.17, np.inf]; nombres2 = ["punta cargada EN CONTRA", "algo en contra", "pareja", "algo a favor", "punta cargada A FAVOR"]
G = R[mov].assign(b=pd.cut(qal[mov], cortes2, labels=nombres2), a=a[mov])
for nb in nombres2:
    g = G[G["b"] == nb]; fila = "%-24s %8d" % (nb, len(g))
    for h in (10, 30, 60, 300, 600):
        dd = (g["f%d" % h] * g["a"]).groupby(g["dia"]).mean(); m, t = t_entre_dias(dd); fila += "%9.3f (%4.1f)" % (m, t)
    print(fila)

print("\n=== 5. Punta flaca = se viene movimiento? |movimiento del mid a 60 s| por decil de profundidad (bidv+askv, media 10 s), dentro de cada tercil de volatilidad reciente ===")
R["prof10"] = R["prof"]  # la profundidad instantanea es ruidosa; se usa tal cual y tambien la relacion con el rango pasado
R["volat"] = R["ret60"].abs(); R["tv"] = pd.qcut(R["volat"].rank(method="first"), 3, labels=["calmo", "medio", "movido"])
R["dp"] = pd.qcut(R["prof"].rank(method="first"), 5, labels=["flaca", "2", "3", "4", "gruesa"])
print(R.assign(am=R["f60"].abs()).pivot_table(index="dp", columns="tv", values="am", aggfunc="mean", observed=True).round(2).to_string())
print("spread: |mov a 60 s| con spread 0,25: %.2f | 0,50: %.2f | >= 0,75: %.2f" % tuple(R.loc[m, "f60"].abs().mean() for m in (R["spread"] <= 0.25, R["spread"] == 0.5, R["spread"] >= 0.75)))

print("\n=== 6. Costo de referencia: medio spread %.3f pts | costo ida y vuelta del protocolo %.2f pts ===" % (R["spread"].mean() / 2, B.COSTO_PTS))
