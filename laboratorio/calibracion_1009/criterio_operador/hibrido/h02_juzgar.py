# -*- coding: utf-8 -*-
"""h02_juzgar.py — el juez del operador (juez/juez_operador.py, SIN CAMBIOS) sobre el conjunto HIBRIDO (seleccion 2.0 + conversion
4.1: 'h41'; y con el escalon de sesion: 'h41c'), armado por h01_armar_rayas.py (rayas_hibrido.pkl). Referencia: lo que DIBUJO la
2.0 (niv20 dom0/dom1, solo libro QQQ: '20dib').
Ventanas: 'congelada' (D+1 00:35Z -> 08:30Z; viernes: domingo 22:00Z -> lunes 08:30Z) y 'completa' (22:00Z de la vispera -> 13:30Z),
por separado; y ESTA NOCHE (08->09-10) aparte, en las dos ventanas.
Por cada conjunto y ventana: los tres modos (ESTRICTO / TOLERANTE / MUY TOLERANTE) con su placebo (rayas corridas +-11/19/31 y 500
juegos al azar 'pool'; en TOLERANTE tambien 'propia'), bootstrap por noche IC90, cobertura de giros, y la tabla por noche.
SOLO LECTURA de datos; escribe en esta carpeta resultados_h02.json, por_noche_*.csv, eventos_*.csv, giros_*.csv.
Plan (para que entre en la PC cargada): placebo en los TRES modos para h41/h41c en las dos ventanas (+ azar 'propia' en TOLERANTE);
la referencia 20dib y ESTA NOCHE con placebo solo en TOLERANTE (los otros modos: metricas sin placebo). La comparacion pareada de
los tres modos esta en h03_pareado.py.
Correr: python -I h02_juzgar.py [n_azar] [indices del plan separados por coma]  -> resultados_h02[_indices].json"""
import json
import os
import sys
import time

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(AQUI), "juez"))
import juez_operador as J  # noqa: E402

CAL = os.path.join(os.environ["LOCALAPPDATA"], "Temp", "claude", "C--Users-wmx-7-OneDrive-Escritorio-ATAS-nada",
                   "f5879819-e2a4-405b-87b0-959ace090d7b", "scratchpad", "calibrar")
CLAVES = ("dom0", "dom1")
MODOS = ("estricto", "tolerante", "muy_tolerante")
ESTA = "2026-10-09"


def ventanas(meta, tipo, solo=None, excluir=None):
    out = []
    for m in meta:
        if solo and m["N"] != solo:
            continue
        if excluir and m["N"] == excluir:
            continue
        out.append((m["a_cong"], m["b_cong"]) if tipo == "congelada" else (m["a_full"], m["b_full"]))
    return out


def limpio(x):
    if isinstance(x, dict):
        return {str(k): limpio(v) for k, v in x.items()}
    if isinstance(x, (list, tuple)):
        return [limpio(v) for v in x]
    if isinstance(x, (np.floating, float)):
        return None if not np.isfinite(x) else float(x)
    if isinstance(x, (np.integer,)):
        return int(x)
    if isinstance(x, (pd.Timestamp,)):
        return str(x)
    return x


def por_noche(ev_por_modo, giros, noches_v):
    filas = []
    for nn in noches_v:
        f = {"noche": nn}
        for modo, evs in ev_por_modo.items():
            E = [e for e in evs if e["noche"] == nn]
            r = J.resumir(E, [nn])
            if modo == "tolerante":
                f.update({"llegadas": r["llegadas"], "techos": r["techos"], "pisos": r["pisos"], "censuradas": r["censuradas"],
                          "base": r["base"], "sostenidos": r["sostenidos"], "rotas": r["rotas"], "indefinidas": r["indefinidas"],
                          "falsos_romp": r["falsos_rompimientos"], "pct_sost": r["pct_sostenidos"], "exactos": r["exactos"],
                          "prec_med_abs": r["precision_mediana_abs"], "prec_med": r["precision_mediana"],
                          "recorrido_medio": r["recorrido_medio"], "recorrido_mediano": r["recorrido_mediano"],
                          "recorrido_tramo_medio": r["recorrido_tramo_medio"], "con_opuesta": r["con_opuesta"],
                          "llega_opuesta": r["llega_opuesta"], "strikes": r["n_etiquetas"], "niveles": r["n_niveles"]})
            else:
                f["pct_sost_" + modo] = r["pct_sostenidos"]; f["sost_" + modo] = r["sostenidos"]; f["base_" + modo] = r["base"]
        G = [g for g in giros if g["noche"] == nn]
        f["giros"] = len(G); f["giros_cubiertos"] = sum(g["cubierto"] for g in G)
        filas.append(f)
    return pd.DataFrame(filas)


def correr(nombre, V, R, meta, tipo, n_azar, solo=None, excluir=None, con_propia=True, modos_placebo=MODOS):
    op = {"ventanas": ventanas(meta, tipo, solo, excluir), "claves": CLAVES}
    t0 = time.time()
    prep = J.preparar(V, R, op)
    nv = J._noches_ventana(prep[0])
    out = {"conjunto": nombre, "ventana": tipo, "solo": solo, "n_pistas": len(prep[1]["pistas"]), "noches_ventana": nv, "modos": {}}
    evs = {}
    for modo in MODOS:
        if modo in modos_placebo:
            pl = J.placebo(V, R, dict(op, modo=modo), n_azar=n_azar, _prep=prep)
            evs[modo] = pl.pop("eventos_real")
        else:   # sin placebo: solo las metricas del conjunto real (la sensibilidad)
            jz = J.juzgar(V, R, dict(op, modo=modo), _prep=prep)
            evs[modo] = jz["eventos"]
            pl = {"real": jz["metricas"], "corridos_juntos": None, "corridos": {}, "azar": {}, "bootstrap_ic90": {}}
        out["modos"][modo] = pl
    if con_propia:
        pl = J.placebo(V, R, dict(op, modo="tolerante"), n_azar=n_azar, azar_distancia="propia", n_boot=0, _prep=prep)
        pl.pop("eventos_real")
        out["tolerante_azar_propia"] = pl["azar"]
    cg = J.cobertura_giros(V, R, op, _prep=prep)
    out["cobertura"] = {k: cg[k] for k in ("n_giros", "cubiertos", "pct")}
    cg6 = J.cobertura_giros(V, R, dict(op, tol_cobertura=6.0), _prep=prep)
    out["cobertura_tol6_sensibilidad"] = {k: cg6[k] for k in ("n_giros", "cubiertos", "pct")}
    tag = "%s_%s%s" % (nombre, tipo, ("_" + solo) if solo else ("_sin_" + excluir if excluir else ""))
    pn = por_noche(evs, cg["giros"], nv)
    pn.to_csv(os.path.join(AQUI, "por_noche_%s.csv" % tag), index=False, float_format="%.2f")
    tab = J.a_tabla(evs["tolerante"])
    if len(tab):
        tab.to_csv(os.path.join(AQUI, "eventos_%s_tolerante.csv" % tag), index=False, float_format="%.2f")
    pd.DataFrame(cg["giros"]).to_csv(os.path.join(AQUI, "giros_%s.csv" % tag), index=False, float_format="%.2f")
    out["por_noche"] = pn.to_dict("records")
    out["segundos"] = time.time() - t0
    return out, evs


def imprimir(o):
    print("\n=== %s | ventana %s%s | pistas %d | noches en ventana %d | %.0f s" % (
        o["conjunto"], o["ventana"], (" | SOLO " + o["solo"]) if o["solo"] else "", o["n_pistas"], len(o["noches_ventana"]), o["segundos"]))
    for modo in MODOS:
        pl = o["modos"][modo]; x = pl["real"]; cj = pl["corridos_juntos"]
        print("  %-13s llegadas %4d (techo %d piso %d) base %4d | sostenidos %4d = %5.1f %% (falso romp %d = %.0f %% de los sost.) | rotas %5.1f %% "
              "indef %5.1f %% | exactos %d = %.1f %% | prec med |%.2f| (signo %+.2f, p75 %.2f) | recorrido medio %.1f med %.1f tramo %.1f | "
              "llega opuesta %s/%d = %s %% | noches %d strikes %d" % (
                  modo.upper(), x["llegadas"], x["techos"], x["pisos"], x["base"], x["sostenidos"], x["pct_sostenidos"], x["falsos_rompimientos"],
                  x["pct_falso_entre_sostenidos"], x["pct_rotas"], x["pct_indefinidas"], x["exactos"], x["pct_exactos"],
                  x["precision_mediana_abs"], x["precision_mediana"], x["precision_p75_abs"], x["recorrido_medio"], x["recorrido_mediano"],
                  x["recorrido_tramo_medio"], x["llega_opuesta"], x["con_opuesta"],
                  ("%.0f" % x["pct_llega_opuesta"]) if x["pct_llega_opuesta"] == x["pct_llega_opuesta"] else "-", x["n_noches"], x["n_etiquetas"]))
        if not cj:
            continue
        print("      corridas +-11/19/31 juntas: sost %.1f %% exactos %.1f %% prec med |%.2f| recorrido medio %.1f llega opuesta %s %% cobertura %.1f %%" % (
            cj["pct_sostenidos"], cj["pct_exactos"], cj["precision_mediana_abs"], cj["recorrido_medio"],
            ("%.0f" % cj["pct_llega_opuesta"]) if cj["pct_llega_opuesta"] == cj["pct_llega_opuesta"] else "-", cj.get("cobertura_pct", float("nan"))))
        for m in ("pct_sostenidos", "pct_exactos", "pct_rotas", "precision_mediana_abs", "recorrido_medio", "recorrido_mediano",
                  "recorrido_tramo_medio", "pct_llega_opuesta", "cobertura_pct", "llegadas"):
            a = pl["azar"].get(m)
            if a:
                print("      azar %-22s real %8.2f | p5 %8.2f p50 %8.2f p95 %8.2f | percentil %5.1f" % (m, a["real"], a["p5"], a["p50"], a["p95"],
                                                                                                    a["percentil"]))
        if pl["bootstrap_ic90"]:
            b = pl["bootstrap_ic90"]
            print("      IC90 por noche: sost %.1f-%.1f %% | exactos %.1f-%.1f %% | prec med %.2f-%.2f | recorrido medio %.1f-%.1f | llega opuesta %.0f-%.0f %%" % (
                b["pct_sostenidos"][0], b["pct_sostenidos"][1], b["pct_exactos"][0], b["pct_exactos"][1], b["precision_mediana_abs"][0],
                b["precision_mediana_abs"][1], b["recorrido_medio"][0], b["recorrido_medio"][1], b["pct_llega_opuesta"][0], b["pct_llega_opuesta"][1]))
    if "tolerante_azar_propia" in o:
        a = o["tolerante_azar_propia"]
        print("  TOLERANTE azar 'propia': percentiles sost %.1f exactos %.1f prec %.1f recorrido %.1f cobertura %.1f" % (
            a["pct_sostenidos"]["percentil"], a["pct_exactos"]["percentil"], a["precision_mediana_abs"]["percentil"],
            a["recorrido_medio"]["percentil"], a["cobertura_pct"]["percentil"]))
    c = o["cobertura"]; c6 = o["cobertura_tol6_sensibilidad"]
    print("  cobertura de giros (zigzag 20, +-2): %d de %d = %.1f %% | sensibilidad +-6: %d de %d" % (c["cubiertos"], c["n_giros"], c["pct"],
                                                                                                    c6["cubiertos"], c6["n_giros"]))


def main():
    n_azar = int(sys.argv[1]) if len(sys.argv) > 1 else 500
    V = pd.read_csv(os.path.join(CAL, "velas_m1.csv"), parse_dates=["t"])
    RH = pd.read_pickle(os.path.join(AQUI, "rayas_hibrido.pkl"))
    meta = RH["meta"]
    niv = pd.read_pickle(os.path.join(CAL, "niv20_m1.pkl"))
    conjuntos = {"h41": RH["h41"], "h41c": RH["h41c"], "20dib": J.rayas_desde_niv20(niv, CLAVES)}
    res = []
    plan = []
    for tipo in ("congelada", "completa"):
        for nom in ("h41", "h41c"):
            plan.append((nom, tipo, None, True, MODOS))
    for tipo in ("congelada", "completa"):
        plan.append(("20dib", tipo, None, False, ("tolerante",)))      # referencia: la sensibilidad pareada esta en h03
    for tipo in ("congelada", "completa"):
        for nom in ("h41", "h41c", "20dib"):
            plan.append((nom, tipo, ESTA, False, ("tolerante",)))
    filtro = set(sys.argv[2].split(",")) if len(sys.argv) > 2 else None
    for k, (nom, tipo, solo, propia, mp) in enumerate(plan):
        if filtro and str(k) not in filtro:
            continue
        o, evs = correr(nom, V, conjuntos[nom], meta, tipo, n_azar, solo=solo, con_propia=propia, modos_placebo=mp)
        imprimir(o); res.append(o)
        if solo:
            print("  LLEGADAS de esta noche (%s, %s):" % (nom, tipo))
            otros = {m: {(e["i"], round(e["raya"], 2)): e for e in evs[m]} for m in ("estricto", "muy_tolerante")}
            for e in evs["tolerante"]:
                kk = (e["i"], round(e["raya"], 2))
                rec = ("rec %5.2f (tramo %5.2f, %s) opuesta %s llega %s" % (
                    e["recorrido"], e["recorrido_tramo"], e["fin_recorrido"], ("%.2f" % e["opuesta"]) if e["opuesta"] else "-",
                    e["llega_opuesta"])) if e["resultado"] == "sostenido" else ""
                print("    %s %-5s %.2f %-10s -> %-10s %-36s prec %+6.2f %s| ESTR %-10s MUYTOL %-10s %s" % (
                    e["t_llegada"].strftime("%m-%d %H:%M"), e["tipo"], e["raya"], "+".join(e["etiquetas"]) or "-", e["resultado"],
                    e["motivo"], e["precision"], "FALSO ROMP. " if e["falso_rompimiento"] else "",
                    otros["estricto"].get(kk, {}).get("resultado", "(no llega)"), otros["muy_tolerante"].get(kk, {}).get("resultado", "(no llega)"), rec))
        sufijo = ("_" + sys.argv[2].replace(",", "-")) if filtro else ""
        json.dump(limpio(res), open(os.path.join(AQUI, "resultados_h02%s.json" % sufijo), "w", encoding="utf-8"), indent=1, ensure_ascii=False)
        sys.stdout.flush()
    print("\nFIN h02")


if __name__ == "__main__":
    main()
