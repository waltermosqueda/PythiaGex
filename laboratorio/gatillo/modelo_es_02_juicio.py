# -*- coding: utf-8 -*-
"""modelo_es_02_juicio.py - EL JUICIO (se corre UNA vez, despues del pre-registro de resultados/modelo_es.md).
El gatillo modelo·es10 (logistica M2 congelada el 10-09) en los dias que el ajuste nunca vio: 11-09, 14-09, 15-09, 16-09, 17-09.
  A  = los disparos reales del registro en vivo;  R1..R7 = el mismo modelo congelado reconstruido sobre el rebobinado;  R8 = AUC;  E = equivalencia vivo/reconstruido.
Cada medicion va con su placebo (mismo dia, misma media hora, mismo lado; 200 sorteos) y su tasa base."""
import json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
from sklearn.metrics import roc_auc_score
import modelo_es_lib as M

D, E = M.corridas()
_tab = {}


def tabla(k):
    """Una corrida entera: velas + p del modelo congelado (a la manera del indicador) + desenlace de cada vela."""
    if k not in _tab:
        w = D[D["corrida"] == k].set_index("ap").sort_index()
        t = w.join(M.p_congelada(w)[["p", "previas"]]).join(M.desenlaces(w))
        t["c5"] = t["c"].shift(-5); t["dia"] = t.index.strftime("%Y-%m-%d"); _tab[k] = t
    return _tab[k]


def universo(dias, modo, desde, hasta):
    """Todas las velas de esos dias y esa franja con desenlace sin agujeros (la poblacion de la que sale el placebo y la tasa base)."""
    partes = []
    for d in dias:
        t = tabla(E[d][modo]); x = t[(t["dia"] == d) & M.en_rueda(t.index, desde, hasta) & t["completo"] & t["p"].notna()]; partes.append(x)
    return pd.concat(partes)


def disparos(dias, modo, desde, hasta, umbral, enfriamiento):
    partes = []
    for d in dias:
        t = tabla(E[d][modo]); s = M.elegir(t[["p"]].dropna(), umbral, enfriamiento)     # el enfriamiento corre sobre TODA la corrida, antes del filtro de franja (como el indicador)
        x = t.loc[s.index].copy(); x["lado"] = s.to_numpy(); x = x[(x["dia"] == d) & M.en_rueda(x.index, desde, hasta) & x["completo"]]; partes.append(x)
    return pd.concat(partes)


def informe(nombre, tiros, univ, detalle=False):
    if not len(tiros): print("  %-34s sin disparos" % nombre); return None
    j = M.juzgar(tiros["y"], tiros["lado"], tiros["dia"]);
    if j["n"] == 0: print("  %-34s %d disparos, ninguno resuelto" % (nombre, len(tiros))); return None
    pm, ps = M.placebo(tiros, univ); z = (j["acierto"] / 100 - pm) / ps if ps > 0 else float("nan")
    res = univ[univ["y"] != 0]; b_largo = float((res["y"] == 1).mean()); base = float(np.mean([b_largo if l > 0 else 1 - b_largo for l in tiros["lado"]]))
    r = tiros[tiros["y"] != 0]; gan = (r["y"] * r["lado"] > 0)
    neto_res = float(np.where(gan, M.G, -M.G).mean() - M.COSTO_ES)
    nr = tiros[(tiros["y"] == 0) & ~tiros["ambigua"] & tiros["c5"].notna()]; pn = list(np.where(gan, M.G, -M.G)) + list((nr["c5"] - nr["c"]) * nr["lado"])
    neto_todo = float(np.mean(pn) - M.COSTO_ES)
    print("  %-34s n %3d resueltos (+%d sin resolver, %d largos / %d cortos) | acierto %5.1f %% | placebo %5.1f +- %4.1f -> z %+5.2f | tasa base del lado %5.1f %% | dias arriba %s | neto %+.2f pts (resueltos) %+.2f (todos)" % (
        nombre, j["n"], j["sin_resolver"], int((tiros["lado"] > 0).sum()), int((tiros["lado"] < 0).sum()), j["acierto"], 100 * pm, 100 * ps, z, 100 * base, j["dias_arriba"], neto_res, neto_todo))
    if detalle: print("  %-34s por dia: %s" % ("", j["por_dia"]))
    return dict(nombre=nombre, n=j["n"], sin_resolver=j["sin_resolver"], acierto=j["acierto"], placebo=round(100 * pm, 1), placebo_sd=round(100 * ps, 1), z=round(z, 2), base=round(100 * base, 1),
                dias_arriba=j["dias_arriba"], por_dia=j["por_dia"], neto_resueltos=round(neto_res, 2), neto_todos=round(neto_todo, 2))


def auc(univ):
    r = univ[univ["y"] != 0]
    return (float(roc_auc_score((r["y"] == 1).astype(int), r["p"])), len(r), float((r["y"] == 1).mean())) if len(r) > 50 and r["y"].nunique() > 1 else (float("nan"), len(r), float("nan"))


TODOS = sorted(E); OOS = [d for d in TODOS if d >= M.CORTE_OOS]; INS = [d for d in TODOS if d < M.CORTE_OOS]
print("dias fuera de muestra:", OOS); print("dias dentro de muestra (control):", INS[0], "->", INS[-1], "(%d)" % len(INS))
print("corrida usada por dia (ultima / primera completa, velas de rueda):", {d: (E[d]["ultima"], E[d]["primera"], E[d]["velas"]) for d in OOS})
print("empate con +-3 y costo %.2f: %.1f %%" % (M.COSTO_ES, 100 * M.EMPATE))
for d in OOS:    # control de integridad: que la corrida elegida no mezcle contratos dentro de la rueda
    t = tabla(E[d]["ultima"]); x = t[(t["dia"] == d) & M.en_rueda(t.index)]; s = (x["o"] - x["c"].shift(1)).abs().max()
    print("   %s corrida #%d: %d velas de rueda, mayor salto apertura-cierre previo %.2f pts, cierre %.2f -> %.2f" % (d, E[d]["ultima"], len(x), s, x["c"].iloc[0], x["c"].iloc[-1]))

salida = {}
for modo in ("ultima", "primera"):
    for etiqueta, dias in (("FUERA DE MUESTRA 11-09 al 17-09", OOS), ("CONTROL dentro de muestra 19-08 al 10-09", INS)):
        if modo == "primera" and dias is INS: continue
        print("\n================ %s | corrida: %s ================" % (etiqueta, modo))
        U_t = universo(dias, modo, "18:00", "20:00"); U_r = universo(dias, modo, "13:32", "20:00"); U_m = universo(dias, modo, "13:32", "18:00")
        for nom, u in (("rueda", U_r), ("manana", U_m), ("tarde", U_t)):
            a, n, b = auc(u); print("  R8 AUC %-7s %.3f (n %d velas resueltas; sube primero %.1f %%) | velas con p extrema (>=0,70 o <=0,30): %d largas, %d cortas" % (nom, a, n, 100 * b, int((u["p"] >= 0.70).sum()), int((u["p"] <= 0.30).sum())))
        bloque = []
        for nom, (de, ha, um, enf, u) in (("R1 tarde 0,70 enfr 5", ("18:00", "20:00", 0.70, 5, U_t)), ("R2 rueda 0,70 enfr 5", ("13:32", "20:00", 0.70, 5, U_r)), ("R3 manana 0,70 enfr 5", ("13:32", "18:00", 0.70, 5, U_m)),
                                      ("R4 tarde 0,70 sin enfriamiento", ("18:00", "20:00", 0.70, 1, U_t)), ("R5 rueda 0,70 sin enfriamiento", ("13:32", "20:00", 0.70, 1, U_r)),
                                      ("R6 tarde 0,65 enfr 5", ("18:00", "20:00", 0.65, 5, U_t)), ("R7 rueda 0,65 enfr 5", ("13:32", "20:00", 0.65, 5, U_r))):
            tiros = disparos(dias, modo, de, ha, um, enf); r = informe(nom, tiros, u, detalle=nom.startswith(("R1", "R2")))
            if r: bloque.append(r)
            if nom.startswith("R2") and len(tiros):   # por franja horaria (NY) y por lado
                tiros = tiros.copy(); tiros["hNY"] = (tiros.index.hour + 20) % 24; tiros["ok"] = (tiros["y"] * tiros["lado"] > 0)
                rr = tiros[tiros["y"] != 0]
                print("  %-34s por hora NY: %s" % ("", " ".join("%dh %d/%d" % (h, int(g["ok"].sum()), len(g)) for h, g in rr.groupby("hNY"))))
                print("  %-34s por lado: %s" % ("", " ".join("%s %d/%d" % ("largos" if l > 0 else "cortos", int(g["ok"].sum()), len(g)) for l, g in rr.groupby("lado"))))
        salida["%s|%s" % (etiqueta, modo)] = bloque

# ------------------------------------------------------------------ A: los disparos REALES del vivo
print("\n================ A. LOS DISPAROS REALES DEL REGISTRO EN VIVO ================")
L = M.disparos_log(False); L["dia"] = L["ap"].dt.strftime("%Y-%m-%d"); hoy = M.velas_hoy(); yh = M.desenlaces(hoy)
filas = []
for r in L.itertuples(index=False):
    if r.dia not in E: continue
    t = tabla(E[r.dia]["ultima"])
    if r.ap not in t.index: print("   sin vela en el rebobinado:", r.ap); continue
    v = t.loc[r.ap]; yv = yh.loc[r.ap] if r.ap in yh.index else None
    t1 = tabla(E[r.dia]["primera"]); p1 = t1.loc[r.ap, "p"] if r.ap in t1.index else np.nan
    filas.append(dict(ap=r.ap, dia=r.dia, lado=r.lado, precio=r.precio, p_vivo=r.p, c_reb=v["c"], p_reb_ultima=v["p"], p_reb_primera=p1, y=int(v["y"]), ambigua=bool(v["ambigua"]), completo=bool(v["completo"]), c=v["c"], c5=v["c5"],
                      y_hoy=(int(yv["y"]) if yv is not None and bool(yv["completo"]) else None)))
A = pd.DataFrame(filas).set_index("ap")
A["resultado"] = np.where(A["y"] == 0, "sin resolver", np.where(A["y"] * A["lado"] > 0, "ACIERTO", "fallo"))
print(A[["lado", "precio", "c_reb", "p_vivo", "p_reb_ultima", "p_reb_primera", "y", "y_hoy", "resultado"]].to_string())
dif = A.dropna(subset=["y_hoy"]); print("desenlace con las velas del rebobinado contra las velas que anoto el vivo: %d comparables, %d distintos" % (len(dif), int((dif["y"] != dif["y_hoy"]).sum())))
A_oos = A[(A["dia"] >= M.CORTE_OOS) & A["completo"]]; A_in = A[A["dia"] < M.CORTE_OOS]
U_t = universo(OOS, "ultima", "18:00", "20:00")
salida["A"] = informe("A VIVO tarde (11-09 al 17-09)", A_oos, U_t, detalle=True)
if len(A_in): print("  (aparte, dia visto por el ajuste) 10-09: %s" % " ".join(A_in["resultado"]))

# ------------------------------------------------------------------ E: equivalencia vivo / reconstruido
print("\n================ E. EQUIVALENCIA: lo que disparo en vivo contra lo que reconstruye el rebobinado ================")
for modo in ("ultima", "primera"):
    R1 = disparos(OOS, modo, "18:00", "20:00", 0.70, 5); com = A_oos.index.intersection(R1.index)
    col = "p_reb_" + modo; dd = (A_oos["p_vivo"] - A_oos[col]).abs()
    print("  corrida %-8s: R1 tiene %d disparos; de los %d del vivo reaparecen %d (misma vela y lado: %d) | |p vivo - p reconstruida|: mediana %.3f, maxima %.3f | p reconstruida <= 0,30 en %d de %d" % (
        modo, len(R1), len(A_oos), len(com), int((A_oos.loc[com, "lado"] == R1.loc[com, "lado"]).sum()), dd.median(), dd.max(), int((A_oos[col] <= 0.30).sum()), len(A_oos)))
    por = R1.groupby("dia").size().to_dict(); print("     R1 por dia:", por, "| vivo por dia:", A_oos.groupby("dia").size().to_dict())

json.dump(salida, open(os.path.join(M.CACHE, "juicio.json"), "w"), indent=1, default=str)
A.to_csv(os.path.join(M.CACHE, "vivo_juzgado.csv"))
