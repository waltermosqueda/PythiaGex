# -*- coding: utf-8 -*-
"""Chequeos de la implementacion de los controles del grupo 4 (solo ENTRENAMIENTO): C24 contra juez_operador._pivotes, C23 por fuerza
bruta, C22 contra D.foto_vigente / D.conversion / D.filas minuto a minuto (muestra al azar), C19-C21 por definicion."""
import sys, os, json
import numpy as np, pandas as pd
AQUI = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, AQUI)
import candidatas as C
import evaluar as EV
import juez_operador_v1 as JO
import cargar as D

rng = np.random.default_rng(4)
dias = ["2026-08-20", "2026-09-03", "2026-09-16", "2026-09-24", "2026-09-30"]
res = {}
# C24: mismos giros que _pivotes (antes de los filtros en_v / sanidad) y confirmacion causal
ok24 = []
for d in dias:
    v = C._velas_ses(d)
    piv = C.pivotes_causales(v, 20.0)
    op = JO._op({"desfase_min": 0, "arrastre_min": 0, "umbral_giro_cobertura": 20.0})
    V = JO.preparar_velas(v.assign(noche=d), op)
    pj = JO._pivotes(V, op)
    a = [(p["tipo"], p["k"], p["precio"]) for p in piv]
    b = [(p["tipo"], p["k"], p["precio"]) for p in pj]
    h = v["h"].to_numpy(); l = v["l"].to_numpy()
    causal = True
    for p in piv:
        k, kc = p["k"], p["k_conf"]
        if kc < k:
            causal = False
        if p["tipo"] == "max":
            if not (h[k:kc + 1].max() <= p["precio"] + 1e-9 and p["precio"] - l[kc] >= 20 - 1e-9):
                causal = False
        else:
            if not (l[k:kc + 1].min() >= p["precio"] - 1e-9 and h[kc] - p["precio"] >= 20 - 1e-9):
                causal = False
    ok24.append({"dia": d, "giros_causal": len(a), "giros_juez": len(b), "iguales": a == b, "confirmacion_coherente": causal})
res["C24_vs_juez_pivotes"] = ok24
# C24: la raya en t no usa ninguna vela >= t
x = C.mecha_previa("2026-09-16")
v = C._velas_ses("2026-09-16")
viol = 0
for _ in range(200):
    r = x.iloc[int(rng.integers(len(x)))]
    previas = v[v["t"] < r["t"]]
    viol += not ((previas["h"] == r["nivel"]).any() or (previas["l"] == r["nivel"]).any())
res["C24_nivel_es_mecha_de_vela_previa_violaciones_de_200"] = int(viol)
# C23 por fuerza bruta
x = C.rango60("2026-09-24")
v = C._velas_ses("2026-09-24")
mal = 0
for _ in range(300):
    r = x.iloc[int(rng.integers(len(x)))]
    w = v[(v["t"] >= r["t"] - pd.Timedelta(minutes=60)) & (v["t"] <= r["t"] - pd.Timedelta(minutes=1))]
    esp = w["h"].max() if r["etiqueta"] == "D1" else w["l"].min()
    mal += (len(w) < 5) or abs(esp - r["nivel"]) > 1e-9
res["C23_fuerza_bruta_errores_de_300"] = int(mal)
# C22 contra la API publica de cargar.py
mal22 = []; n = 0
for d in ["2026-09-14", "2026-09-22", "2026-09-29"]:
    x = C.grilla_qqq(d)
    g = EV.minutos_y_precio(d).set_index("t")
    for _ in range(80):
        r = x.iloc[int(rng.integers(len(x)))]
        t = r["t"]
        fv = D.foto_vigente("QQQ", d, t)
        c = D.conversion("QQQ", d, t, "cuatro")
        fl = D.filas("QQQ", d, int(fv["foto"]))
        seg = (t - fv["generado"]).total_seconds(); env = min(2.0, max(0.0, seg / 86400.0))
        de = fl["dias"] - env
        mc = de[de >= 0].min()
        hoy = fl[(de >= 0) & (de <= max(1.0, mc + 0.01))]
        ks = np.unique(hoy["strike"].to_numpy(float))
        S = g.loc[t, "F"] / c["valor"]
        esp = ks[ks > S].min() if r["etiqueta"] == "G+" else ks[ks <= S].max()
        n += 1
        if abs(esp - r["K"]) > 1e-9 or abs(esp * c["valor"] - r["nivel"]) > 1e-6:
            mal22.append((d, str(t), r["etiqueta"], float(r["K"]), float(esp)))
res["C22_api_publica"] = {"chequeados": n, "errores": mal22[:10], "n_errores": len(mal22)}
# C19-C21
mal19 = 0
for G, cid in ((25, "C19_RED25"), (50, "C20_RED50"), (100, "C21_RED100")):
    x = C.niveles(cid, ["2026-09-10"])
    g = EV.minutos_y_precio("2026-09-10").set_index("t")
    F = g.loc[x["t"], "F"].to_numpy()
    up = x["etiqueta"].to_numpy() == "R+"
    L = x["nivel"].to_numpy()
    mal19 += int((~((L[up] > F[up]) & (L[up] - F[up] <= G) & (L[up] % G == 0))).sum())
    mal19 += int((~((L[~up] <= F[~up]) & (F[~up] - L[~up] < G) & (L[~up] % G == 0))).sum())
res["C19_C21_errores_definicion"] = mal19
print(json.dumps(res, indent=1, ensure_ascii=False))
with open(os.path.join(AQUI, "verificacion_implementacion.json"), "w", encoding="utf-8") as f:
    json.dump(res, f, indent=1, ensure_ascii=False)
