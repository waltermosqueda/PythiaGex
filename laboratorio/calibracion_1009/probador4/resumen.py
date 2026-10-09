# -*- coding: utf-8 -*-
"""
resumen.py — PROBADOR 4: tablas de los controles C19..C24 a partir de resultados/<ID>_<FASE>.json (salida del arnes).

  python -I resumen.py ENTRENAMIENTO   -> resumen_ENTRENAMIENTO.md / .json: M1..M4, placebos, seleccion (sec. 8, los controles no
                                          pueden ser finalistas), mitades, LOO del procedimiento "elegir la mejor" entre los 6.
  python -I resumen.py PRUEBA          -> resumen_PRUEBA.md / .json: lo mismo + E1..E6 (informativo: los controles no estan en la
                                          familia de Holm del pre-registro; se dan con el p sin ajustar y con Holm sobre los 12 tests
                                          del grupo 4).
"""
import json
import os
import sys

import numpy as np

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import candidatas as C      # noqa: E402
import evaluar as EV        # noqa: E402

RES = os.path.join(AQUI, "resultados")
VENT = ("noche", "dia")


def f(x, d=1):
    return "—" if x is None or (isinstance(x, float) and x != x) else (("%." + str(d) + "f") % x)


def cargar(fase, suf=""):
    out = {}
    for cid in C.IDS:
        p = os.path.join(RES, "%s_%s%s.json" % (cid, fase, suf))
        if os.path.exists(p):
            with open(p, encoding="utf-8") as fh:
                out[cid] = json.load(fh)
    return out


def _nan(o):
    """el JSON trae None donde habia NaN: las funciones del arnes esperan floats."""
    if isinstance(o, dict):
        return {k: _nan(v) for k, v in o.items()}
    if isinstance(o, list):
        return [_nan(x) for x in o]
    return float("nan") if o is None else o


def fila_m1(cid, r, w, modo="tolerante"):
    t = r["operador"][modo][w]
    s = t["pct_sostenidos"]; m = t["muestra"]
    niveles = m["strikes_distintos"] or m["niveles_distintos_5pts"]
    return {"id": cid, "ventana": w, "modo": modo, "base": m["base"], "llegadas": m["llegadas"], "censuradas": m["censuradas"],
            "sesiones_con_llegadas": m["sesiones_con_llegadas"], "niveles": niveles,
            "niveles_tipo": "strikes" if m["strikes_distintos"] else "niveles 5 pts por sesion",
            "M1": s.get("real"), "corridos": s.get("corridos"), "ventaja_corridos": s.get("ventaja_vs_corridos"),
            "ic95_corridos": s.get("ic95_vs_corridos"), "ic90_inf_corridos": s.get("ic90_inf_vs_corridos"),
            "azar_media": s.get("azar_media"), "azar_p5": s.get("azar_p5"), "azar_p95": s.get("azar_p95"),
            "ventaja_azar": s.get("ventaja_vs_azar"), "p_azar": s.get("p_azar"), "ic95_azar": s.get("ic95_vs_azar"),
            "sesiones_con_3": m["sesiones_con_3_o_mas"], "sesiones_ganadas": m["sesiones_ganadas"],
            "corridos_por_desplazamiento": s.get("corridos_por_desplazamiento")}


def tabla(res, fase):
    L = []
    for w in VENT:
        L += ["", "### %s — M1 %% sostenidos (TOLERANTE) contra placebos" % w, "",
              "| id | base (llegadas, cens.) | niveles | ses. c/lleg. | M1 % | corridos % | real−corr (IC95) | azar media [p5–p95] | real−azar (IC95) | p_azar | ses. ganadas |",
              "|---|---|---|---|---|---|---|---|---|---|---|"]
        for cid, r in res.items():
            x = fila_m1(cid, r, w)
            ic = x["ic95_corridos"] or [None, None]; ia = x["ic95_azar"] or [None, None]
            L.append("| %s | %d (%d, %d) | %d %s | %d | %s | %s | %s (%s; %s) | %s [%s–%s] | %s (%s; %s) | %s | %d/%d |" % (
                cid, x["base"], x["llegadas"], x["censuradas"], x["niveles"], "K" if x["niveles_tipo"] == "strikes" else "",
                x["sesiones_con_llegadas"], f(x["M1"]), f(x["corridos"]), f(x["ventaja_corridos"]), f(ic[0]), f(ic[1]),
                f(x["azar_media"]), f(x["azar_p5"]), f(x["azar_p95"]), f(x["ventaja_azar"]), f(ia[0]), f(ia[1]), f(x["p_azar"], 3),
                x["sesiones_ganadas"], x["sesiones_con_3"]))
        L += ["", "### %s — sensibilidad del juez, M2..M4, cobertura, toque y eco" % w, "",
              "| id | M1 estricto (corr) | M1 muy tol. (corr) | M2 rec/lleg (corr) | rec. medio / mediano | % exactos (corr) | % falsos entre sost. | % llega opuesta | precisión mediana abs / con signo | cob20 real / azar (lift azar; lift corr) | cob40 real / azar (lift azar; lift corr) | toque: rebote real / azar (p) | dist. mediana | beta pista |",
              "|---|---|---|---|---|---|---|---|---|---|---|---|---|---|"]
        for cid, r in res.items():
            op = r["operador"]
            t = op["tolerante"][w]
            es = op.get("estricto", {}).get(w, {}).get("pct_sostenidos", {})
            mt = op.get("muy_tolerante", {}).get(w, {}).get("pct_sostenidos", {})
            rp = t["recorrido_por_llegada"]; rm = t["recorrido_medio"]; ex = t["pct_exactos"]; fr = t["pct_falso_entre_sostenidos"]
            lo = t["pct_llega_opuesta"]; md = t["medianas"]
            c20 = r["cobertura"][w]["cobertura20"]; c40 = r["cobertura"][w]["cobertura40"]
            tq = r.get("toque", {}).get(w, {}).get("pct_rebote", {})
            eco = r["eco"][w]
            L.append("| %s | %s (%s) | %s (%s) | %s (%s) | %s / %s | %s (%s) | %s | %s | %s / %s | %s / %s (%s; %s) | %s / %s (%s; %s) | %s / %s (%s) | %s | %s |" % (
                cid, f(es.get("real")), f(es.get("corridos")), f(mt.get("real")), f(mt.get("corridos")), f(rp.get("real")),
                f(rp.get("corridos")), f(rm.get("real")), f(md.get("recorrido_mediano")), f(ex.get("real")), f(ex.get("corridos")),
                f(fr.get("real")), f(lo.get("real")), f(md.get("precision_mediana_abs"), 2), f(md.get("precision_mediana_con_signo"), 2),
                f(c20.get("real")), f(c20.get("azar_media")), f(c20.get("lift_vs_azar"), 2), f(c20.get("lift_vs_corridos"), 2), f(c40.get("real")),
                f(c40.get("azar_media")), f(c40.get("lift_vs_azar"), 2), f(c40.get("lift_vs_corridos"), 2), f(tq.get("real")), f(tq.get("azar_media")), f(tq.get("p_azar"), 3),
                f(eco.get("dist_mediana_al_precio")), f(eco.get("beta_pista_15"), 3)))
    return L


def estratos(res):
    L = ["", "### Estratos (descriptivos, no deciden): % sostenidos por franja NY y primera visita contra siguientes", "",
         "| id | ventana | franjas (base: %) | primera visita (base: %) | siguientes (base: %) |", "|---|---|---|---|---|"]
    for cid, r in res.items():
        for w in VENT:
            e = r["estratos"].get(w, {})
            fr = "; ".join("%s %d: %s" % (k, v["base"], f(v["pct"])) for k, v in e.get("franja", {}).items())
            pv = e.get("visita", {}).get("primera", {}); sg = e.get("visita", {}).get("siguientes", {})
            L.append("| %s | %s | %s | %s: %s | %s: %s |" % (cid, w, fr, pv.get("base", 0), f(pv.get("pct")), sg.get("base", 0), f(sg.get("pct"))))
    return L


def main(fase, suf=""):
    res = cargar(fase, suf)
    if not res:
        raise SystemExit("sin resultados de " + fase + suf)
    out = {"fase": fase + suf, "candidatas": list(res), "m1": {}, "seleccion": {}, "loo": {}, "mitades": {}, "criterios": {}}
    for cid, r in res.items():
        for w in VENT:
            out["m1"]["%s/%s" % (cid, w)] = fila_m1(cid, r, w)
    L = ["# Probador 4 — controles sin gamma (C19..C24), fase %s%s" % (fase, " (velas de 2 min, sensibilidad)" if suf else ""), "",
         "Arnés `evaluar.py` v1.0 (sha256 %s), juez copia %s. Sesiones por candidata en `resultados/<ID>_%s.json` → probador4.dias_usados." % (
             next(iter(res.values()))["meta"]["arnes_sha256"][:12], next(iter(res.values()))["meta"]["juez"]["copia"][:12], fase),
         "n_azar = %s, n_boot = %s." % (next(iter(res.values()))["meta"]["n_azar"], next(iter(res.values()))["meta"]["n_boot"])]
    L += tabla(res, fase)
    L += estratos(res)
    rn = {k: _nan(v) for k, v in res.items()}
    if fase == "ENTRENAMIENTO":
        L += ["", "## Regla de selección (sec. 8) aplicada a los 6 controles", ""]
        for w in VENT:
            s = EV.seleccionar_finalistas(rn, w)
            out["seleccion"][w] = s
            lo = EV.loo_parametro(rn, w)
            out["loo"][w] = lo
            L.append("**%s**: finalistas = %s (los controles C19–C24 no pueden ser finalistas por regla). Elegibles si no fueran controles: %s." % (
                w, s["finalistas"] or "ninguna",
                ", ".join(t["candidata"] for t in s["tabla"] if (t["ic90_inf_vs_corridos"] == t["ic90_inf_vs_corridos"]
                          and t["ic90_inf_vs_corridos"] > 0 and t["p_azar"] < 0.05)) or "ninguno"))
            L.append("")
            L.append("| id | IC90 inf (real−corr) | p_azar | mitad 1 (≤ 09-22): base, real−corr | mitad 2 (> 09-22): base, real−corr | cambia de signo |")
            L.append("|---|---|---|---|---|---|")
            for t in s["tabla"]:
                m = t["mitades"]
                L.append("| %s | %s | %s | %d, %s | %d, %s | %s |" % (t["candidata"], f(t["ic90_inf_vs_corridos"], 2), f(t["p_azar"], 3),
                                                                   m["primera"]["base"], f(m["primera"]["ventaja_vs_corridos_pp"]),
                                                                   m["segunda"]["base"], f(m["segunda"]["ventaja_vs_corridos_pp"]),
                                                                   "SÍ" if m["cambia_de_signo"] else "no"))
            L.append("")
            el = lo["eleccion_por_sesion"]
            cuenta = {k: list(el.values()).count(k) for k in set(el.values())}
            L.append("LOO por sesión del procedimiento “elegir el mejor control por M1 − corridos” (%s): elegido por sesión %s; "
                     "ventaja fuera de muestra %s pp." % (w, ", ".join("%s ×%d" % (k, v) for k, v in sorted(cuenta.items(), key=lambda z: -z[1])),
                                                          f(lo["ventaja_fuera_de_muestra_pp"], 2)))
            L.append("")
    else:
        ps = []; claves = []
        for cid, r in rn.items():
            for w in VENT:
                ps.append(r["operador"]["tolerante"][w]["pct_sostenidos"].get("p_azar", float("nan"))); claves.append((cid, w))
        ph = EV.holm(ps)
        L += ["", "## E1..E6 (informativo: los controles no están en la familia de Holm de la sec. 9)", "",
              "| id | ventana | p_azar | p Holm (12 tests del grupo 4) | E1 | E2 | E3 | E4 | E5 | E6 | gana (Holm grupo) | gana (p sin ajustar) |",
              "|---|---|---|---|---|---|---|---|---|---|---|---|"]
        for (cid, w), p, q in zip(claves, ps, ph):
            ca = EV.criterios_exito(rn[cid], w, q)
            cb = EV.criterios_exito(rn[cid], w, p)
            out["criterios"]["%s/%s" % (cid, w)] = {"p_azar": p, "p_holm_grupo4": q, "con_holm": ca, "sin_ajustar": cb}
            L.append("| %s | %s | %s | %s | %s | %s | %s | %s | %s | %s | %s | %s |" % (
                cid, w, f(p, 4), f(q, 4), *["sí" if ca[k] else "no" for k in ("E1_muestra", "E2_tamano", "E3_significancia", "E4_consistencia",
                                                                            "E5_no_empeora", "E6_no_es_eco")], "SÍ" if ca["gana"] else "no",
                "SÍ" if cb["gana"] else "no"))
    fut = {cid: r.get("probador4", {}).get("chequeo_futuro") for cid, r in res.items()}
    out["chequeo_futuro"] = fut
    L += ["", "Chequeo de mirar adelante (`chequeo_futuro`, % de rayas a ≤ 0,25 de la mecha de su propia vela / anterior / siguiente): " +
          "; ".join("%s %s/%s/%s%s" % (k, f(v["propia"], 2), f(v["anterior"], 2), f(v["siguiente"], 2), " ALERTA" if v["alerta"] else "")
                    for k, v in fut.items() if v), ""]
    nom = "resumen_%s%s" % (fase, suf)
    with open(os.path.join(AQUI, nom + ".md"), "w", encoding="utf-8") as fh:
        fh.write("\n".join(L))
    with open(os.path.join(AQUI, nom + ".json"), "w", encoding="utf-8") as fh:
        json.dump(EV.limpiar_para_json(out), fh, ensure_ascii=False, indent=1)
    sys.stdout.buffer.write(("\n".join(L) + "\n").encode("utf-8"))


if __name__ == "__main__":
    main(sys.argv[1], "_m2" if "--marco2" in sys.argv else "")
