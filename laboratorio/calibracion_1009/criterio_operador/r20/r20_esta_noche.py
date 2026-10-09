# -*- coding: utf-8 -*-
"""r20_esta_noche.py — ESTA NOCHE (sesion 10-09: 08-10 22:00Z -> ...) con el juez del operador (sin cambios) y la 2.0 tal como dibujo.
Dos tramos de datos:
  A) velas_m1.csv tal cual (hasta la vela 03:22Z, lo mismo que usan los demas investigadores);
  B) A + las velas cerradas posteriores del centinela VIVO de la 2.0 (mismo archivo del que salio velas_m1.csv para esta noche),
     validadas contra la cinta por segundo de la 4.1.
Conjuntos: r20_doms (dom0, dom1 de QQQ), r20_doms+ndx (+ ndx_dom0, ndx_dom1: la capa NDX solo existe esta noche, desde ~01:35Z),
r20_zero (zero_vol de QQQ) y r20_zero_ndx (ndx_zero_vol: los rombos de la capa NDX).
Ventanas: CONGELADA 10-09 00:35Z-08:30Z y COMPLETA 10-08 22:00Z-10-09 13:30Z. Las velas de las noches anteriores se cargan igual (solo
para que el azar 'pool' sortee distancias de la distribucion de TODAS las noches); solo cuentan llegadas y giros de esta noche.
SOLO LECTURA. Escribe r20_esta_noche.json y r20_esta_noche.txt. Correr: python -I r20_esta_noche.py [n_azar]"""
import json
import os
import sys

import pandas as pd

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r20_comun as C  # noqa: E402

J = C.J
SEM = 20261009
N_HOY = pd.Timestamp("2026-10-09")
LINEAS = []


def p(*a):
    s = " ".join(str(x) for x in a)
    print(s)
    LINEAS.append(s)


def una(V, niv, nombre, claves, tipo, n_azar, etiqueta):
    R = C.rayas(niv, claves)
    a, b = C.ventana(N_HOY, tipo)
    op = {"ventanas": [(a, b)]}
    prep = J.preparar(V, R, op)
    res = {m: J.juzgar(V, R, dict(op, modo=m), _prep=prep) for m in ("tolerante", "estricto", "muy_tolerante")}
    W = V[(V["t"] >= a) & (V["t"] < b)]
    p("\n" + "-" * 140)
    p("ESTA NOCHE [%s] %s (%s) ventana %s %s -> %s | velas en la ventana %d (%s -> %s) rango %.2f-%.2f" % (
        etiqueta, nombre, "+".join(claves), tipo.upper(), a, b, len(W), W["t"].min(), W["t"].max(), W["l"].min(), W["h"].max()))
    otros = {m: {(e["i"], round(e["raya"], 2)): e for e in res[m]["eventos"]} for m in ("estricto", "muy_tolerante")}
    for e in res["tolerante"]["eventos"]:
        k = (e["i"], round(e["raya"], 2))
        rec = ("rec %5.2f (desde punta %5.2f, tramo %5.2f, fin %s) opuesta %s llega %s" % (
            e["recorrido"], e["recorrido_desde_punta"], e["recorrido_tramo"], e["fin_recorrido"],
            ("%.2f" % e["opuesta"]) if e["opuesta"] else "-", e["llega_opuesta"])) if e["resultado"] == "sostenido" else ""
        p("  %s %-5s %.2f %-26s -> %-10s %-40s prec %+6.2f %s| ESTRICTO %-10s MUY TOL %-10s %s" % (
            e["t_llegada"].strftime("%m-%d %H:%M"), e["tipo"], e["raya"], "+".join(e["nombres"]), e["resultado"], e["motivo"], e["precision"],
            "FALSO ROMP. " if e["falso_rompimiento"] else "", otros["estricto"].get(k, {}).get("resultado", "(no llega)"),
            otros["muy_tolerante"].get(k, {}).get("resultado", "(no llega)"), rec))
    for m in ("tolerante", "estricto", "muy_tolerante"):
        x = res[m]["metricas"]
        p("  %-13s llegadas %d (base %d, censuradas %d) sostenidos %d (%s %%; falso romp. %d) rotas %d indef %d | exactos %d | precision |med| %s "
          "(signo %s) | recorrido medio %s mediano %s tramo %s | llega opuesta %d/%d" % (
              m.upper(), x["llegadas"], x["base"], x["censuradas"], x["sostenidos"], C.r(x["pct_sostenidos"]), x["falsos_rompimientos"], x["rotas"],
              x["indefinidas"], x["exactos"], C.r(x["precision_mediana_abs"], 2), C.r(x["precision_mediana"], 2), C.r(x["recorrido_medio"]),
              C.r(x["recorrido_mediano"]), C.r(x["recorrido_tramo_medio"]), x["llega_opuesta"], x["con_opuesta"]))
    cobs = {}
    for tc in (2.0, 4.0, 6.0):
        cg = J.cobertura_giros(V, R, dict(op, tol_cobertura=tc), _prep=prep)
        cobs["%g" % tc] = cg
    p("  COBERTURA giros zigzag 20: " + "; ".join("+-%s: %d/%d" % (k, v["cubiertos"], v["n_giros"]) for k, v in cobs.items()))
    for g in cobs["2"]["giros"]:
        p("     %s %s %.2f (izq %.1f der %.1f) aprox %s -> cubierto %s (raya mas cercana %s, a %s pts)" % (
            g["t"].strftime("%H:%M"), g["tipo"], g["precio"], g["izq"], g["der"], g["t_aprox"].strftime("%H:%M"), g["cubierto"],
            ("%.2f" % g["raya_mas_cercana"]) if g["raya_mas_cercana"] else "-",
            ("%.2f" % abs(g["raya_mas_cercana"] - g["precio"])) if g["raya_mas_cercana"] else "-"))
    pls = {}
    if res["tolerante"]["metricas"]["llegadas"]:
        for dist in ("pool", "propia"):
            pl = J.placebo(V, R, dict(op, modo="tolerante"), n_azar=n_azar, semilla=SEM, azar_distancia=dist, n_boot=0, _prep=prep)
            pl.pop("eventos_real")
            pls[dist] = pl
            p("  PLACEBO TOLERANTE azar '%s' (%d juegos; UNA noche: muestra chica, leer como descripcion):" % (dist, n_azar))
            for k in ("llegadas", "pct_sostenidos", "pct_exactos", "pct_rotas", "precision_mediana_abs", "recorrido_medio", "recorrido_mediano",
                      "pct_llega_opuesta", "cobertura_pct"):
                az = pl["azar"][k]
                p("     %-24s real %8s | azar p5 %8s p50 %8s p95 %8s | percentil %s (n %d)" % (
                    k, C.r(az["real"], 2), C.r(az["p5"], 2), C.r(az["p50"], 2), C.r(az["p95"], 2), C.r(az["percentil"], 1), az["n"]))
            if dist == "pool":
                cj = pl["corridos_juntos"]
                p("     corridas +-11/19/31 juntas: llegadas %d sostenidos %s %% exactos %s %% recorrido medio %s llega opuesta %s %% cobertura %s %%" % (
                    cj["llegadas"], C.r(cj["pct_sostenidos"]), C.r(cj["pct_exactos"]), C.r(cj["recorrido_medio"]), C.r(cj["pct_llega_opuesta"]),
                    C.r(cj.get("cobertura_pct"))))
                p("     por corrimiento: " + " | ".join("%+d: %d lleg, %s %% sost" % (int(d), x["llegadas"], C.r(x["pct_sostenidos"], 0)) for d, x in pl["corridos"].items()))
    return {"etiqueta": etiqueta, "conjunto": nombre, "claves": claves, "ventana": tipo, "desde": str(a), "hasta": str(b), "velas": len(W),
            "modos": {m: res[m]["metricas"] for m in res}, "eventos_tolerante": res["tolerante"]["eventos"],
            "eventos_estricto": res["estricto"]["eventos"], "eventos_muy_tolerante": res["muy_tolerante"]["eventos"],
            "cobertura": {k: {"n_giros": v["n_giros"], "cubiertos": v["cubiertos"], "pct": v["pct"]} for k, v in cobs.items()},
            "giros": cobs["2"]["giros"], "placebo": pls}


def main():
    n_azar = int(sys.argv[1]) if len(sys.argv) > 1 else 500
    V, niv = C.cargar()
    V2, niv2, val = C.extender_esta_noche(V, niv)
    p("A) velas_m1.csv: ultima vela %s UTC." % V["t"].max())
    p("B) extension con el centinela vivo de la 2.0: %s" % json.dumps(val, ensure_ascii=False))
    out = {"validacion_extension": val, "res": []}
    conj = dict(C.CONJUNTOS); conj.update(C.CONJUNTOS_HOY)
    for etiqueta, (VV, nn) in (("A hasta %s" % V["t"].max().strftime("%H:%M"), (V, niv)), ("B hasta %s" % V2["t"].max().strftime("%H:%M"), (V2, niv2))):
        for nombre, claves in conj.items():
            for tipo in ("congelada", "completa"):
                out["res"].append(una(VV, nn, nombre, claves, tipo, n_azar, etiqueta))
    json.dump(C.limpio(out), open(os.path.join(C.AQUI, "r20_esta_noche.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=0)
    open(os.path.join(C.AQUI, "r20_esta_noche.txt"), "w", encoding="utf-8").write("\n".join(LINEAS) + "\n")


if __name__ == "__main__":
    main()
