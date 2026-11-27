# -*- coding: utf-8 -*-
"""RONDA 19, paso 11: evaluador GENERICO de reglas sobre las bases de eventos (vueltas de la ola o cruces de nivel).
Una regla = mascara sobre las filas + salida pre-registrada. Simula una posicion por vez por rueda (la siguiente entrada despues del
segundo de salida de la anterior), maximo 5 por rueda, y juzga con la vara doble (acierto y esperanza neta con costo 0,50 y 1,00),
t POR RUEDA (ruedas sin operaciones = 0), racha y caida. Placebo C: misma cantidad de eventos por rueda elegidos AL AZAR entre todos
los eventos de esa rueda (mide si el filtro agrega algo sobre la linea de base). Anota cada variante en registro_variantes.csv."""
import math, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import r19_comun as C
import r19_09_registro as REG

COSTOS = (0.50, 1.00)


def simular(df, mask, salida, max_dia=5, hora_ini=0, hora_fin=23400):
    sub = df[mask & (df["seg_rueda"] >= hora_ini) & (df["seg_rueda"] < hora_fin) & df[salida + "_pts"].notna()]
    claves = ["dia", "seg_rueda"] + (["t_utc_ms"] if "t_utc_ms" in sub.columns else [])
    sub = sub.sort_values(claves, kind="mergesort")
    tomadas = []
    for dia, g in sub.groupby("dia", sort=True):
        libre = -1; n = 0
        for idx, r in g.iterrows():
            if r["seg_rueda"] <= libre or n >= max_dia: continue
            tomadas.append(idx); n += 1; libre = r[salida + "_seg_salida"]
    return df.loc[tomadas]


def juzgar(ops, salida, dias):
    n = len(ops); nd = len(dias)
    out = dict(n=n, ruedas=nd, ops_por_rueda=round(n / nd, 2) if nd else 0)
    if n == 0: return out
    pts = ops[salida + "_pts"].to_numpy(); R = ops["R"].to_numpy()
    g = pts[pts > 0]; p = pts[pts <= 0]
    out.update(acierto=round(100 * len(g) / n, 1), gana_media=round(float(g.mean()), 2) if len(g) else 0.0,
               pierde_media=round(float(p.mean()), 2) if len(p) else 0.0, bruto_op=round(float(pts.mean()), 3),
               R_medio=round(float(R.mean()), 2), en_R=round(float((pts / R).mean()), 3))
    for c in COSTOS:
        net = pts - c
        por = pd.Series(net, index=ops.index).groupby(ops["dia"]).sum()
        serie = np.array([por.get(d, 0.0) for d in dias])
        m = serie.mean(); sd = serie.std(ddof=1) if nd > 1 else 0
        t = m / (sd / math.sqrt(nd)) if sd > 0 else 0.0
        orden = net  # ya ordenadas por dia y segundo
        r = mx = 0
        for x in orden:
            r = r + 1 if x < 0 else 0; mx = max(mx, r)
        eq = np.concatenate([[0.0], np.cumsum(orden)]); dd = float((np.maximum.accumulate(eq) - eq).max())
        k = "c%03d" % int(c * 100)
        out.update({k + "_neto_op": round(float(net.mean()), 3), k + "_por_rueda": round(float(m), 2), k + "_t": round(float(t), 2),
                    k + "_ruedas_pos": int((serie > 0).sum()), k + "_racha": int(mx), k + "_caida": round(dd, 2),
                    k + "_sharpe_d": round(float(m / sd), 4) if sd > 0 else 0.0})
    return out


def placebo_C(df, ops, salida, sorteos=200, semilla=1919, max_dia=5):
    """Mismo numero de operaciones por rueda, tomadas al azar entre TODOS los eventos de esa rueda (una posicion por vez)."""
    rng = np.random.default_rng(semilla)
    cuantas = ops.groupby("dia").size()
    medias = []
    base = df[df[salida + "_pts"].notna()]
    grupos = {d: g.sort_values("seg_rueda") for d, g in base.groupby("dia")}
    for _ in range(sorteos):
        tot = []
        for d, k in cuantas.items():
            g = grupos.get(d)
            if g is None or len(g) == 0: continue
            orden = rng.permutation(len(g)); elegidas = []; ocup = []
            for j in orden:
                r = g.iloc[j]; s0 = r["seg_rueda"]; s1 = r[salida + "_seg_salida"]
                if any(not (s1 < a or s0 > b) for a, b in ocup): continue
                elegidas.append(r[salida + "_pts"]); ocup.append((s0, s1))
                if len(elegidas) >= k: break
            tot += elegidas
        if tot: medias.append(np.mean(tot))
    medias = np.array(medias)
    real = ops[salida + "_pts"].mean()
    return dict(placebo_media=round(float(medias.mean()), 3), placebo_p95=round(float(np.quantile(medias, 0.95)), 3),
                p=round(float((medias >= real).mean()), 3))


def correr_regla(df, nombre, familia, parametros, mask, salidas, dias, agente="r19-principal", hora_ini=0, hora_fin=23400,
                 placebo=True, anotar=True, muestra="diseno"):
    filas = []
    for sal in salidas:
        ops = simular(df, mask, sal, hora_ini=hora_ini, hora_fin=hora_fin)
        r = juzgar(ops, sal, dias)
        if placebo and r["n"] > 0:
            r["placeboC"] = placebo_C(df, ops, sal)
        filas.append((sal, r, ops))
        if anotar:
            REG.anotar(dict(fecha_hora_AR=time.strftime("%Y-%m-%d %H:%M"), agente=agente, familia=familia, variante=nombre, salida=sal,
                            parametros=parametros, muestra=muestra, ruedas=r.get("ruedas"), n=r.get("n"), ops_por_rueda=r.get("ops_por_rueda"),
                            acierto_pct=r.get("acierto"), gana_media=r.get("gana_media"), pierde_media=r.get("pierde_media"),
                            bruto_op=r.get("bruto_op"), neto_op_c050=r.get("c050_neto_op"), neto_op_c100=r.get("c100_neto_op"),
                            t_rueda_c050=r.get("c050_t"), ruedas_positivas_c050=r.get("c050_ruedas_pos"), racha_perdedora=r.get("c050_racha"),
                            caida_max_c050=r.get("c050_caida"), sharpe_diario_c050=r.get("c050_sharpe_d"),
                            placeboA_p=(r.get("placeboC") or {}).get("p", ""), nota="placebo C (eventos al azar de la misma rueda) en placeboA_p"))
    return filas


def imprimir(nombre, filas):
    for sal, r, _ in filas:
        if r["n"] == 0:
            print("  %s %s: sin operaciones" % (nombre, sal)); continue
        pc = r.get("placeboC", {})
        print("  %-10s %-3s n %3d (%.2f/rueda) acierto %5.1f %% | +%.2f/%.2f | bruto %+.3f (%+.3f R) | c0,50 %+.3f t %+.2f (%d/%d +) racha %d caida %.1f | c1,00 %+.3f | placebo C %+.3f (p95 %+.3f, p %.3f)" % (
            nombre, sal, r["n"], r["ops_por_rueda"], r["acierto"], r["gana_media"], r["pierde_media"], r["bruto_op"], r["en_R"],
            r["c050_neto_op"], r["c050_t"], r["c050_ruedas_pos"], r["ruedas"], r["c050_racha"], r["c050_caida"], r["c100_neto_op"],
            pc.get("placebo_media", np.nan), pc.get("placebo_p95", np.nan), pc.get("p", np.nan)))
