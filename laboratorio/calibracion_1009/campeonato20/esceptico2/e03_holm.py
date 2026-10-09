# -*- coding: utf-8 -*-
"""e03_holm.py — esceptico2: Holm de TODO el campeonato (las 4 familias), leyendo los json de cada una (solo lectura).
p = placebo.azar.pct_acierto20.p_valor (permutacion). Se arma:
  (1) MIRAR, todas las series x ventana de las 4 familias (lo que se miro para elegir);
  (2) PRUEBA, idem (la confirmacion honesta a nivel campeonato);
  (3) todo junto (MIRAR + PRUEBA + sensibilidades).
Controles (GRILLA_QQQ, MUROS_oi_UNION) se informan aparte y NO entran. -> datos/e03_holm.json y .txt"""
import glob
import json
import os
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import e00_comun as E  # noqa: E402
E.prioridad_baja()
J20 = E.J20
C20 = E.C20


def filas():
    F = []
    # NDX
    for p in glob.glob(os.path.join(C20, "ndx", "datos", "juez", "*.json")):
        x = json.load(open(p, encoding="utf-8"))
        a = x["placebo"]["azar"]["pct_acierto20"]
        F.append(dict(fam="ndx", serie=x["conjunto"], ventana=x["ventana"], parte=x["particion"], n_azar=x["n_azar"],
                      pct=x["metricas"]["pct_acierto20"], base=x["metricas"]["base"], p=a["p_valor"], p_normal=a["p_normal"],
                      p50=a["p50"]))
    # NQ
    x = json.load(open(os.path.join(C20, "familia_nq", "datos", "juez_nq.json"), encoding="utf-8"))
    for r in x["resultados"]:
        if r.get("vacio"):
            continue
        a = r["azar"]["pct_acierto20"]
        F.append(dict(fam="nq", serie=r["serie"], ventana=r["ventana"], parte=r["parte"] + ("" if r["n_azar"] == 500 else "_conf"),
                      n_azar=r["n_azar"], pct=r["metricas"]["pct_acierto20"], base=r["metricas"]["base"], p=a["p_valor"],
                      p_normal=a["p_normal"], p50=a["p50"]))
    # QQQ
    for p in glob.glob(os.path.join(C20, "qqq20", "datos", "res", "*.json")):
        x = json.load(open(p, encoding="utf-8"))
        a = x["placebo"]["azar"]["pct_acierto20"]
        F.append(dict(fam="qqq", serie=x["serie"], ventana=x["ventana"], parte=x["particion"], n_azar=x["n_azar"],
                      pct=x["metricas"]["pct_acierto20"], base=x["metricas"]["base"], p=a.get("p_valor"), p_normal=a.get("p_normal"),
                      p50=a.get("p50")))
    # combos
    for p in glob.glob(os.path.join(C20, "familia_conf", "resultados", "*__*__*.json")):
        x = json.load(open(p, encoding="utf-8"))
        if "placebo" not in x:
            continue
        a = x["placebo"]["azar"]["pct_acierto20"]
        F.append(dict(fam="combos", serie=x["serie"], ventana=x["ventana"], parte=x["parte"].lower(), n_azar=x["placebo"].get("n_azar"),
                      pct=x["metricas"]["pct_acierto20"], base=x["metricas"]["base"], p=a.get("p_valor"), p_normal=a.get("p_normal"),
                      p50=a.get("p50")))
    return F


CONTROLES = ("GRILLA_QQQ", "MUROS_oi_UNION")


def main():
    F = filas()
    lin = []
    out = {}
    for nombre, filtro in (("MIRAR", lambda f: f["parte"] == "mirar"), ("PRUEBA", lambda f: f["parte"] == "prueba"),
                           ("TODO", lambda f: True)):
        X = [f for f in F if filtro(f) and f["serie"] not in CONTROLES and f["p"] is not None]
        ps = {"%s|%s|%s|%s" % (f["fam"], f["serie"], f["ventana"], f["parte"]): f["p"] for f in X}
        H = J20.holm(ps)
        pn = {"%s|%s|%s|%s" % (f["fam"], f["serie"], f["ventana"], f["parte"]): f["p_normal"] for f in X if f["p_normal"] is not None}
        Hn = J20.holm(pn)
        orden = sorted(H.items(), key=lambda kv: kv[1]["p"])
        out[nombre] = dict(m=len(ps), m_normal=len(pn), mejores=[dict(clave=k, p=v["p"], p_holm=v["p_holm"],
                                                                        p_normal=pn.get(k), p_holm_normal=Hn.get(k, {}).get("p_holm"))
                                                                   for k, v in orden[:12]],
                           rechazan=[k for k, v in H.items() if v["rechaza_05"]],
                           rechazan_normal=[k for k, v in Hn.items() if v["rechaza_05"]])
        lin.append("== %s: m = %d pruebas (p por permutacion), %d con p_normal" % (nombre, len(ps), len(pn)))
        for d in out[nombre]["mejores"]:
            lin.append("  %-55s p %.4f -> Holm %.3f | p_normal %s -> Holm %s" % (
                d["clave"], d["p"], d["p_holm"], "%.4f" % d["p_normal"] if d["p_normal"] is not None else "-",
                "%.3f" % d["p_holm_normal"] if d["p_holm_normal"] is not None else "-"))
        lin.append("  rechazan al 5 %% (perm): %s | (normal): %s" % (out[nombre]["rechazan"], out[nombre]["rechazan_normal"]))
        # cuantas p < 0,05 crudas y cuantas se esperan por azar
        crudas = sum(1 for v in ps.values() if v <= 0.05)
        lin.append("  p crudas <= 0,05: %d de %d (por puro azar se esperan ~%.1f)" % (crudas, len(ps), 0.05 * len(ps)))
        out[nombre]["p_crudas_05"] = crudas
    # controles
    for f in F:
        if f["serie"] in CONTROLES:
            lin.append("control %s %s %s %s: %.1f %% p %.3f" % (f["fam"], f["serie"], f["ventana"], f["parte"], f["pct"], f["p"]))
    with open(os.path.join(AQUI, "datos", "e03_holm.json"), "w", encoding="utf-8") as fh:
        json.dump(dict(resultados=out, filas=F), fh, ensure_ascii=False, indent=1, default=str)
    with open(os.path.join(AQUI, "datos", "e03_holm.txt"), "w", encoding="utf-8") as fh:
        fh.write("\n".join(lin) + "\n")
    print("\n".join(lin))


if __name__ == "__main__":
    main()
