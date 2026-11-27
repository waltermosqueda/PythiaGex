# -*- coding: utf-8 -*-
"""RONDA 19, paso 13: EL JUEZ. Evalua las finalistas (como mucho 4, definidas en finalistas.py) con el mismo evaluador sobre:
  diseno (verificacion de los numeros declarados), confirmacion (ES 16-10..13-11, UNA vez) y guardada (MES 16-25-11, UNA vez,
  despues de la confirmacion). Vara: neto > 0 (costo 0,50) en las tres; t por rueda de confirmacion + guardada >= Bonferroni para las
  finalistas juzgadas (una cola, 5 % / k); mejor que el placebo C (eventos al azar de la misma rueda) en confirmacion + guardada.
Uso: python -I r19_13_juez.py diseno | confirma | guardada"""
import json, math, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
from scipy import stats
import r19_comun as C
import r19_11_evaluar_base as EB
import r19_16_fade as FD
import finalistas as FIN


def cargar_base(base, mu, con_fade=False):
    df = pd.read_parquet(os.path.join(C.SCR, BASES[(base, mu)]))
    df = df[df["muestra"] == mu]
    if "t_utc_ms" in df.columns:
        df = df.sort_values(["dia", "seg_rueda", "t_utc_ms"], kind="mergesort")
    df = df.reset_index(drop=True)
    if con_fade:                      # la operacion contraria desde la cinta para TODOS los eventos (la usa tambien el placebo C)
        df = FD.agregar_fade(df, np.ones(len(df), bool))
    return df

BASES = {("vueltas", "diseno"): "eventos_todos.parquet", ("vueltas", "confirma"): "eventos_todos.parquet",
         ("vueltas", "guardada"): "eventos_guardada.parquet", ("vueltas", "guardada_mes"): "eventos_guardada_mes.parquet",
         ("cruces", "diseno"): "eventos_cruces.parquet", ("cruces", "confirma"): "eventos_cruces.parquet",
         ("cruces", "guardada"): "eventos_cruces_guardada.parquet", ("cruces", "guardada_mes"): "eventos_cruces_guardada_mes.parquet"}


def dias_de(mu):
    if mu == "diseno": return [d for d in C.dias("ES") if "2026-09-10" <= d <= C.DISENO_FIN]
    if mu == "confirma": return [d for d in C.dias("ES") if C.DISENO_FIN < d <= C.CONFIRMA_FIN]
    if mu == "guardada": return [d for d in C.dias("ES") if C.GUARDADA[0] <= d <= C.GUARDADA[1]]          # juicio final: ES 16-25-11
    return [d for d in C.dias("MES") if C.GUARDADA[0] <= d <= C.GUARDADA[1]]                             # chequeo secundario: MES


def evaluar(fin, mu):
    df = cargar_base(fin["base"], mu, con_fade=(fin["salida"] == "S6mc"))
    m = pd.Series(fin["mascara"](df), index=df.index)
    ops = EB.simular(df, m, fin["salida"], hora_ini=fin.get("hora_ini", 0), hora_fin=fin.get("hora_fin", 23400))
    dias = dias_de(mu)
    r = EB.juzgar(ops, fin["salida"], dias)
    if r["n"] > 0:
        r["placeboC"] = EB.placebo_C(df, ops, fin["salida"])
    ops = ops.assign(finalista=fin["nombre"], salida_regla=fin["salida"])
    return r, ops


def main(mu):
    salida = os.path.join(C.AQUI, "salidas", "juez"); os.makedirs(salida, exist_ok=True)
    res = {}
    todas = []
    for fin in FIN.FINALISTAS:
        r, ops = evaluar(fin, mu)
        res[fin["nombre"]] = r; todas.append(ops)
        pc = r.get("placeboC", {})
        if r["n"]:
            print("%-12s %-9s n %3d (%.2f/rueda) acierto %5.1f %% | +%.2f/%.2f | bruto %+.3f | neto c0,50 %+.3f (t %+.2f, %d/%d ruedas +) | c1,00 %+.3f (t %+.2f, %d/%d +) | placebo C %+.3f p %.3f" % (
                fin["nombre"], mu, r["n"], r["ops_por_rueda"], r["acierto"], r["gana_media"], r["pierde_media"], r["bruto_op"], r["c050_neto_op"],
                r["c050_t"], r["c050_ruedas_pos"], r["ruedas"], r["c100_neto_op"], r["c100_t"], r["c100_ruedas_pos"], r["ruedas"],
                pc.get("placebo_media", np.nan), pc.get("p", np.nan)))
        else:
            print("%-26s %s sin operaciones" % (fin["nombre"], mu))
    json.dump(res, open(os.path.join(salida, "juez-%s.json" % mu), "w"), indent=1, default=float)
    pd.concat(todas, ignore_index=True).to_csv(os.path.join(salida, "ops-juez-%s.csv" % mu), index=False)
    if mu == "guardada":
        veredicto()


def veredicto():
    """Confirmacion y guardada POR SEPARADO (acierto, neto 0,50 y 1,00, t por rueda, ruedas positivas) y recien despues juntas.
    Si una finalista pasa en la confirmacion y se cae en la guardada, se informa asi (no se promedia)."""
    salida = os.path.join(C.AQUI, "salidas", "juez")
    k = len(FIN.FINALISTAS)
    oos_dias = dias_de("confirma") + dias_de("guardada")
    tcrit = stats.t.ppf(1 - 0.05 / k, len(oos_dias) - 1)
    print("")
    print("VEREDICTO (k = %d finalistas; t de Bonferroni fuera de muestra, una cola 5 %%/k, %d ruedas = %.2f)" % (k, len(oos_dias), tcrit))
    filas = []
    for fin in FIN.FINALISTAS:
        rs = {mu: json.load(open(os.path.join(salida, "juez-%s.json" % mu)))[fin["nombre"]] for mu in ("diseno", "confirma", "guardada")}
        print("  %s (%s, salida %s)" % (fin["nombre"], fin["familia"], fin["salida"]))
        for mu in ("diseno", "confirma", "guardada"):
            r = rs[mu]
            if r.get("n", 0) == 0:
                print("     %-9s sin operaciones" % mu); continue
            print("     %-9s n %3d (%.2f/rueda) acierto %5.1f %% | neto c0,50 %+.3f t %+.2f (%d/%d ruedas +) | c1,00 %+.3f t %+.2f (%d/%d +) | placebo C p %.3f" % (
                mu, r["n"], r["ops_por_rueda"], r["acierto"], r["c050_neto_op"], r["c050_t"], r["c050_ruedas_pos"], r["ruedas"],
                r["c100_neto_op"], r["c100_t"], r["c100_ruedas_pos"], r["ruedas"], r.get("placeboC", {}).get("p", np.nan)))
        ops = pd.concat([pd.read_csv(os.path.join(salida, "ops-juez-%s.csv" % mu)) for mu in ("confirma", "guardada")], ignore_index=True)
        ops = ops[ops["finalista"] == fin["nombre"]]
        pts = ops[fin["salida"] + "_pts"]
        net = pts - 0.50
        por = net.groupby(ops["dia"]).sum()
        serie = np.array([por.get(d, 0.0) for d in oos_dias])
        t = serie.mean() / (serie.std(ddof=1) / math.sqrt(len(serie))) if serie.std(ddof=1) > 0 else 0
        bases = [cargar_base(fin["base"], mu, con_fade=(fin["salida"] == "S6mc")) for mu in ("confirma", "guardada")]
        dfo = pd.concat(bases, ignore_index=True)
        pc = EB.placebo_C(dfo, ops, fin["salida"]) if len(ops) else {"p": np.nan}
        pls = pc["p"]
        netos = [rs[mu].get("c050_neto_op", np.nan) for mu in ("diseno", "confirma", "guardada")]
        pasa_conf = netos[1] == netos[1] and netos[1] > 0
        pasa_guard = netos[2] == netos[2] and netos[2] > 0
        pasa = all(x == x and x > 0 for x in netos) and t >= tcrit and (pls == pls and pls < 0.05)
        print("     JUNTAS (29 ruedas): n %d acierto %.1f %% neto c0,50 %+.3f c1,00 %+.3f | t por rueda %+.2f (umbral %.2f) | placebo C p %.3f -> %s%s" % (
            len(ops), 100 * (pts > 0).mean() if len(ops) else np.nan, net.mean() if len(ops) else np.nan, (pts - 1.0).mean() if len(ops) else np.nan,
            t, tcrit, pls, "PASA" if pasa else "no pasa",
            "  [pasa en confirmacion y se cae en la guardada]" if (pasa_conf and not pasa_guard) else ""))
        filas.append(dict(finalista=fin["nombre"], familia=fin["familia"], salida=fin["salida"], diseno=rs["diseno"], confirma=rs["confirma"],
                          guardada=rs["guardada"], juntas=dict(n=int(len(ops)), acierto=float(100 * (pts > 0).mean()) if len(ops) else None,
                                                               neto_c050=float(net.mean()) if len(ops) else None, t=float(t), placeboC_p=pls),
                          t_umbral=float(tcrit), PASA=bool(pasa)))
    json.dump(filas, open(os.path.join(salida, "veredicto.json"), "w"), indent=1, default=float)


if __name__ == "__main__":
    main(sys.argv[1])
