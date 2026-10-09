# -*- coding: utf-8 -*-
"""v41_04_juez.py — el JUEZ DEL OPERADOR (juez/juez_operador.py, SIN CAMBIOS) sobre las 4 series de QQQ de la 4.1 reconstruidas
(datos/rayas_v41.pkl), en todas las noches con datos, en dos ventanas por noche:
  CONGELADA: N 00:35Z -> N 08:30Z (los lunes: domingo 22:00Z -> lunes 08:30Z), como demo_escala.ventanas()
  COMPLETA : N-1 22:00Z -> N 13:30Z
Por serie y ventana: placebo (corridas +-11/19/31, 500 juegos al azar 'pool', bootstrap por noche IC90, cobertura), los tres modos,
desfase 1 como sensibilidad, y las metricas por noche. SOLO LECTURA. Escribe datos/juez_v41.json, datos/juez_v41_por_noche.csv y
datos/juez_v41.txt. Uso: python -I v41_04_juez.py [n_azar]"""
import json
import os
import sys
import time

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(AQUI), "juez"))
import juez_operador as J  # noqa: E402

SERIES = ("MUROS_QQQ_vol", "MAJORS_QQQ_vol", "MUROS_QQQ_oi", "MAJORS_QQQ_oi")
MET = ("llegadas", "base", "censuradas", "sostenidos", "rotas", "indefinidas", "falsos_rompimientos", "pct_sostenidos", "pct_rotas",
       "pct_indefinidas", "pct_falso_entre_sostenidos", "exactos", "pct_exactos", "precision_mediana_abs", "precision_mediana",
       "recorrido_medio", "recorrido_mediano", "recorrido_tramo_medio", "recorrido_desde_punta_medio", "con_opuesta", "llega_opuesta",
       "pct_llega_opuesta", "n_noches", "n_niveles", "n_etiquetas")


def ventanas(meta, tipo, min_ok=30):
    out = []
    for s in sorted(meta["sesion"].unique()):
        N = pd.Timestamp(s)
        if tipo == "congelada":
            a = N - pd.Timedelta(hours=2) if N.weekday() == 0 else N + pd.Timedelta(minutes=35)
            b = N + pd.Timedelta(hours=8, minutes=30)
        else:
            a = N - pd.Timedelta(hours=2); b = N + pd.Timedelta(hours=13, minutes=30)
        m = meta[(meta["t"] >= a) & (meta["t"] < b)]
        n_ok = int((m["motivo"] == "ok").sum())
        if n_ok >= min_ok:
            out.append((a, b, s, n_ok, len(m)))
    return out


def limpio(x):
    if isinstance(x, dict):
        return {str(k): limpio(v) for k, v in x.items()}
    if isinstance(x, (list, tuple)):
        return [limpio(v) for v in x]
    if isinstance(x, (np.floating, float)):
        return None if not np.isfinite(x) else round(float(x), 4)
    if isinstance(x, (np.integer,)):
        return int(x)
    if isinstance(x, pd.Timestamp):
        return str(x)
    return x


def por_noche(evs, giros, noches):
    filas = []
    for nn in noches:
        e = [x for x in evs if x["noche"] == nn]
        r = J.resumir(e, [nn])
        g = [x for x in giros if x["noche"] == nn]
        r["giros"] = len(g); r["giros_cubiertos"] = sum(x["cubierto"] for x in g)
        r["noche"] = nn
        filas.append(r)
    return filas


def main():
    n_azar = int(sys.argv[1]) if len(sys.argv) > 1 else 500
    t0 = time.time()
    V = pd.read_csv(os.path.join(AQUI, "datos", "velas_v41.csv"), parse_dates=["t"])
    X = pd.read_pickle(os.path.join(AQUI, "datos", "rayas_v41.pkl"))
    meta = X["meta"]
    res = {"generado": str(pd.Timestamp.utcnow()), "n_azar": n_azar, "ultima_vela": str(V["t"].max()), "series": {}}
    filas_noche = []
    lin = []
    for tipo in ("congelada", "completa"):
        W = ventanas(meta, tipo)
        lin.append("\n==== VENTANA %s: %d noches con datos de QQQ (>= 30 min dibujados): %s" % (
            tipo.upper(), len(W), ", ".join("%s(%d/%d)" % (s[5:], k, n) for _, _, s, k, n in W)))
        for nom in SERIES:
            R = X["series"][nom]
            op = {"ventanas": [(a, b) for a, b, _, _, _ in W], "desfase_min": 0}
            prep = J.preparar(V, R, op)
            noches = J._noches_ventana(prep[0])
            t1 = time.time()
            pl = J.placebo(V, R, op, n_azar=n_azar, _prep=prep)
            sens = {}
            for m in ("estricto", "tolerante", "muy_tolerante"):
                sens[m] = J.resumir(J._juzgar_core(prep[0], prep[1], J._op(dict(op, modo=m))), noches)
            op1 = dict(op, desfase_min=1)
            d1 = J.juzgar(V, R, op1)["metricas"]
            cg = J.cobertura_giros(V, R, op, _prep=prep)
            pn = por_noche(pl["eventos_real"], cg["giros"], noches)
            for r in pn:
                r.update(serie=nom, ventana=tipo)
                filas_noche.append(r)
            res["series"]["%s|%s" % (nom, tipo)] = limpio({
                "n_pistas": pl["n_pistas"], "noches": noches, "real": pl["real"], "bootstrap_ic90": pl["bootstrap_ic90"],
                "corridos": pl["corridos"], "corridos_juntos": pl["corridos_juntos"], "azar": pl["azar"], "sensibilidad": sens,
                "desfase_1": d1, "cobertura": {k: cg[k] for k in ("n_giros", "cubiertos", "pct")},
                "eventos": [{k: e.get(k) for k in ("t_llegada", "noche", "raya", "etiquetas", "tipo", "resultado", "motivo", "precision",
                                                   "falso_rompimiento", "recorrido", "recorrido_tramo", "opuesta", "llega_opuesta",
                                                   "censurada")} for e in pl["eventos_real"]]})
            r = pl["real"]; a = pl["azar"]; cj = pl["corridos_juntos"]; ic = pl["bootstrap_ic90"]
            i0 = len(lin)
            lin.append("\n-- %s | %s | pistas %d | %.0f s" % (nom, tipo, pl["n_pistas"], time.time() - t1))
            for m in ("estricto", "tolerante", "muy_tolerante"):
                x = sens[m]
                lin.append("   %-13s llegadas %3d base %3d sost %3d (%5.1f %%) falso %3d rotas %3d (%5.1f %%) indef %3d | exactos %3d (%5.1f %%) | prec med |%.2f| | "
                           "rec medio %5.1f med %5.1f tramo %5.1f | opuesta %s/%d" % (
                               m, x["llegadas"], x["base"], x["sostenidos"], x["pct_sostenidos"], x["falsos_rompimientos"], x["rotas"],
                               x["pct_rotas"], x["indefinidas"], x["exactos"], x["pct_exactos"], x["precision_mediana_abs"],
                               x["recorrido_medio"], x["recorrido_mediano"], x["recorrido_tramo_medio"], x["llega_opuesta"], x["con_opuesta"]))
            lin.append("   desfase 1 (tolerante): llegadas %d sost %.1f %% exactos %.1f %% rec medio %.1f" % (
                d1["llegadas"], d1["pct_sostenidos"], d1["pct_exactos"], d1["recorrido_medio"]))
            lin.append("   cobertura de giros: %d de %d (%.1f %%) | niveles distintos %d strikes %d noches con llegadas %d de %d" % (
                cg["cubiertos"], cg["n_giros"], cg["pct"], r["n_niveles"], r["n_etiquetas"], r["n_noches"], len(noches)))
            lin.append("   PLACEBO (tolerante)        real    azar p5   p50   p95  pctil | corridas juntas | IC90 bootstrap noche")
            for m in ("pct_sostenidos", "pct_exactos", "pct_rotas", "precision_mediana_abs", "recorrido_medio", "recorrido_mediano",
                      "recorrido_tramo_medio", "pct_llega_opuesta", "pct_falso_entre_sostenidos", "cobertura_pct", "llegadas"):
                aa = a.get(m, {}); icv = ic.get(m) if m in ic else None
                lin.append("   %-26s %7.2f | %6.2f %6.2f %6.2f %5.1f | %7.2f | %s" % (
                    m, aa.get("real", np.nan), aa.get("p5", np.nan), aa.get("p50", np.nan), aa.get("p95", np.nan), aa.get("percentil", np.nan),
                    cj.get(m, np.nan) if cj.get(m) is not None else np.nan, ("%.1f - %.1f" % icv) if icv else "-"))
            lin.append("   corridas: " + "; ".join("%+d: lleg %d sost %.0f%% exact %.0f%% cob %.0f%%" % (
                d, x["llegadas"], x["pct_sostenidos"], x["pct_exactos"], x.get("cobertura_pct", np.nan)) for d, x in pl["corridos"].items()))
            print("\n".join(lin[i0:]), flush=True)
    pd.DataFrame(filas_noche).to_csv(os.path.join(AQUI, "datos", "juez_v41_por_noche.csv"), index=False)
    json.dump(res, open(os.path.join(AQUI, "datos", "juez_v41.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    open(os.path.join(AQUI, "datos", "juez_v41.txt"), "w", encoding="utf-8").write("\n".join(lin) + "\n")
    print("listo %.0f s" % (time.time() - t0))


if __name__ == "__main__":
    main()
