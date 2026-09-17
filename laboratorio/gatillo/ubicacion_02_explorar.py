# -*- coding: utf-8 -*-
"""
ubicacion_02_explorar.py — las 12 variantes pre-registradas, SOLO en B.explorar(T). Tambien el placebo de lugar y el desglose descriptivo.
"""
import sys, os, json, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import base as B, ubicacion_lib as UL
import numpy as np, pandas as pd
from sklearn.linear_model import LogisticRegression
from sklearn.preprocessing import StandardScaler
from sklearn.pipeline import make_pipeline

pd.set_option("display.width", 250); pd.set_option("display.max_columns", 40)
t0 = time.time(); T = B.todas_las_sesiones(); TE = B.explorar(T); E = sorted(TE); U = UL.cargar_umbrales()
ev = UL.pegar_resultados(UL.eventos(T, E), TE)
print("toques en explorar: %d | resueltos +-8/600: %.1f %%" % (len(ev), 100 * (ev["y8"] != 0).mean()))

# ---------------- MODELO: predicciones dejando un dia afuera
ok = (ev["y8"] != 0).to_numpy(); X = UL.matriz_modelo(ev); reb = ((ev["y8"].to_numpy() * -ev["dir"].to_numpy()) > 0).astype(int)
pm = np.full(len(ev), np.nan)
for s in E:
    tr = ok & (ev["ses"] != s).to_numpy(); te = (ev["ses"] == s).to_numpy()
    m = make_pipeline(StandardScaler(), LogisticRegression(C=1.0, max_iter=500)); m.fit(X[tr], reb[tr]); pm[te] = m.predict_proba(X[te])[:, 1]
ev["p_modelo"] = pm
U["modelo_q20"] = float(np.quantile(pm, .20)); U["modelo_q80"] = float(np.quantile(pm, .80))
full = make_pipeline(StandardScaler(), LogisticRegression(C=1.0, max_iter=500)); full.fit(X[ok], reb[ok])
sc = full.named_steps["standardscaler"]; lr = full.named_steps["logisticregression"]
U["modelo"] = dict(rasgos=UL.RASGOS_MODELO, media=sc.mean_.tolist(), escala=sc.scale_.tolist(), coef=lr.coef_[0].tolist(), b=float(lr.intercept_[0]))
json.dump(U, open(UL.UMBRALES, "w", encoding="utf-8"), indent=1)
print("MODELO: cortes de quintil (dejando un dia afuera) q20 = %.3f, q80 = %.3f | rebote base en toques resueltos: %.1f %%" % (U["modelo_q20"], U["modelo_q80"], 100 * reb[ok].mean()))
print("  coeficientes estandarizados:", {k: round(v, 3) for k, v in zip(UL.RASGOS_MODELO, lr.coef_[0])})


def fila(e, nombre):
    r = dict(variante=nombre, disparos=len(e))
    for x, h in UL.BARRERAS:
        j = UL.juzgar_variante(e, x, nombre); pre = "b%d_" % x
        r[pre + "n"] = j.get("n", 0); r[pre + "ac"] = j.get("acierto"); r[pre + "z"] = j.get("z"); r[pre + "dias"] = j.get("dias_arriba"); r[pre + "peor"] = j.get("peor_dia")
        if x == 8: r["b8_tdias"] = round(UL.t_dias(e, 8), 2); r["b8_sinres"] = j.get("sin_resolver")
    for k in (10, 30, 60): r["mv%d" % k] = round(float((e["mv%d" % k] * e["lado"]).mean()), 3) if len(e) else np.nan
    s = e.loc[e["y8"] != 0, "s8"]; r["seg_med"] = float(s.median()) if len(s) else np.nan
    return r


print("\n=== LAS 12 VARIANTES (explorar) — empates: +-8 %.1f | +-5 %.1f | +-12 %.1f" % (100 * B.empate(8), 100 * B.empate(5), 100 * B.empate(12)))
filas = []; det = {}
for v in UL.VARIANTES:
    e = UL.variante(ev, v, U); det[v] = e; filas.append(fila(e, v))
R = pd.DataFrame(filas); print(R.to_string(index=False))

# ---------------- placebo de LUGAR
pl = pd.concat([UL.eventos(T, E, placebo_corr=c, etiqueta="pla%d" % int(c)).assign(rep=int(c)) for c in UL.PLACEBO_CORRIMIENTOS], ignore_index=True)
pl["ses_real"] = pl["ses"]; pl = UL.pegar_resultados(pl, TE)
Xp = UL.matriz_modelo(pl); pl["p_modelo"] = full.predict_proba(Xp)[:, 1]
print("\n=== PLACEBO DE LUGAR (grilla corrida 13/21/37, mismas reglas; %d toques) ===" % len(pl))
fp = []
for v in ["TODO_toque_R"] + [v for v in UL.VARIANTES if v not in ("REF_toque_R", "MOV_toque_R", "RED_toque_R", "CONF_R", "MOV_div_R")]:
    partes = []
    for c in UL.PLACEBO_CORRIMIENTOS:
        g = pl[pl["rep"] == int(c)]
        if v == "TODO_toque_R": e = g.copy(); e["lado"] = -e["dir"]; e = UL.desagrupar(e)
        else: e = UL.variante(g, v, U)
        partes.append(e)
    e = pd.concat(partes, ignore_index=True); fp.append(fila(e, "PLA:" + v))
print(pd.DataFrame(fp)[["variante", "disparos", "b8_n", "b8_ac", "b8_z", "b8_dias", "b5_ac", "b12_ac", "mv10", "mv30", "mv60", "seg_med"]].to_string(index=False))
e = ev.copy(); e["lado"] = -e["dir"]; e = UL.desagrupar(e); print("\nreal TODO_toque_R:", {k: v for k, v in fila(e, "TODO_toque_R").items() if k in ("disparos", "b8_n", "b8_ac", "b8_z", "b8_dias", "b5_ac", "b12_ac", "seg_med")})

# ---------------- desglose descriptivo (no se elige nada de aca)
print("\n=== DESCRIPTIVO: rebote por nivel (+-8/600, cada toque del nivel, desagrupado 60 s por nivel) ===")
ex = ev.assign(nv=ev["niveles"].str.split("+")).explode("nv"); ex["nv"] = ex["nv"].str.replace(r"^R\d*00$", "R100", regex=True).str.replace(r"^R\d*50$", "R50", regex=True)
fd = []
for nv, g in ex.groupby("nv"):
    g = g.copy(); g["lado"] = -g["dir"]; g = UL.desagrupar(g); j = UL.juzgar_variante(g, 8, nv)
    fd.append(dict(nivel=nv, n=j.get("n", 0), rebote=j.get("acierto"), z=j.get("z"), dias=j.get("dias_arriba")))
print(pd.DataFrame(fd).sort_values("n", ascending=False).to_string(index=False))

print("\n=== DESCRIPTIVO: rebote por tercil de cada rasgo de flujo (TODO, +-8/600) ===")
for c in ("fd30", "fd60", "fd300", "a_niv30", "pas30", "bar30", "rompe30", "vel60", "vel300", "V30"):
    q = pd.qcut(ev[c].rank(method="first"), 3, labels=["bajo", "medio", "alto"]); out = []
    for k in ("bajo", "medio", "alto"):
        g = ev[(q == k) & (ev["y8"] != 0)]; out.append("%s %.1f %% (n %d)" % (k, 100 * ((g["y8"] * -g["dir"]) > 0).mean(), len(g)))
    print("  %-8s" % c, " | ".join(out))
print("\n%.0f s" % (time.time() - t0))
R.to_csv(os.path.join(UL.CACHE, "explorar_resultados.csv"), index=False)
