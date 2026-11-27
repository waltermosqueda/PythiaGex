# -*- coding: utf-8 -*-
"""RONDA 19, paso 3: corre una etapa (configuracion del motor) sobre una muestra, con placebos, estadisticos por rueda y registro
de cada variante probada (para la correccion por busqueda multiple). Uso:
  python -I r19_03_correr.py <etapa> <muestra> [placeboB]
    etapa: E1, E1_sin_lugar, E1_sin_momento, E2..E4 (definidas en ETAPAS)
    muestra: tanda0 | diseno_resto | diseno | confirma | guardada"""
import json, math, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
from scipy import stats
import r19_comun as C
import r19_02_motor as M

AQUI = C.AQUI
OUT = os.path.join(AQUI, "salidas"); os.makedirs(OUT, exist_ok=True)
REG = os.path.join(AQUI, "variantes_probadas.json")
TANDA0 = ["2026-09-10", "2026-09-14", "2026-09-18", "2026-09-21", "2026-10-05"]
COSTOS = (0.50, 1.00)


def muestra(nombre):
    es = C.dias("ES")
    dis = [d for d in es if "2026-09-10" <= d <= C.DISENO_FIN]
    if nombre == "tanda0": return [("ES", d) for d in TANDA0]
    if nombre == "diseno_resto": return [("ES", d) for d in dis if d not in TANDA0]
    if nombre == "diseno": return [("ES", d) for d in dis]
    if nombre == "confirma": return [("ES", d) for d in es if C.DISENO_FIN < d <= C.CONFIRMA_FIN]
    if nombre == "guardada": return [("MES", d) for d in C.dias("MES") if C.GUARDADA[0] <= d <= C.GUARDADA[1]]
    raise SystemExit("muestra desconocida")


# ------------------------------------------------------------------ etapas (cada una pre-registrada en PREREGISTRO.md)
ETAPAS = {"E1": dict(M.E1)}
ETAPAS["E1_sin_lugar"] = dict(M.E1, nombre="E1_sin_lugar", requiere_lugar=False)
ETAPAS["E1_sin_momento"] = dict(M.E1, nombre="E1_sin_momento", min_puntaje=0, veto_caudal=False)
# ETAPA 2 (pre-registrada 27-11 03:20 AR): regimen direccional (fuera del ruido), entrada confirmada por la ola, stop detras del
# extremo final, entradas 10:00-15:30 (el setup de la etapa 1 puede armarse desde las 9:45)
ETAPAS["E2"] = dict(M.E1, nombre="E2", entrada="confirmada", c3_ruido=True, hora_ini=1800, hora_fin=21600, setup_hora_ini=900)
# ETAPA 3 (pre-registrada 27-11 03:23 AR): vuelta al valor (extension fuera del ruido + marea hacia el VWAP con agresores + vuelta
# de la ola; objetivo natural S5 = VWAP RTH)
ETAPAS["E3"] = dict(M.E1, nombre="E3", entrada="reversion", extension=True, dist_min_R=1.0, hora_ini=1800, hora_fin=21600,
                    salidas=M.SALIDAS_R)
# ETAPA 4 (pre-registrada 27-11 03:26 AR): E3 + absorcion (F1) o big trades netos a favor (F6) en los 60 s previos a la vuelta; + S6 1R
ETAPAS["E4"] = dict(ETAPAS["E3"], nombre="E4", flujo_min=1, flujo_factores=("F1", "F6"), salidas=M.SALIDAS_R6)


def registrar(etapa, muestra_n, salida, resumen):
    reg = json.load(open(REG)) if os.path.exists(REG) else []
    clave = "%s|%s|%s" % (etapa, muestra_n, salida)
    reg = [r for r in reg if r["clave"] != clave] + [dict(clave=clave, etapa=etapa, muestra=muestra_n, salida=salida, **resumen,
                                                         hora=time.strftime("%Y-%m-%d %H:%M"))]
    json.dump(reg, open(REG, "w"), indent=1)


def racha_y_caida(netos):
    r = mx = 0
    for x in netos:
        r = r + 1 if x < 0 else 0; mx = max(mx, r)
    eq = np.cumsum(netos) if len(netos) else np.array([0.0])
    dd = float((np.maximum.accumulate(np.concatenate([[0.0], eq])) - np.concatenate([[0.0], eq])).max())
    return mx, dd


def resumen_ops(ops, dias):
    """ops: DataFrame de una regla x salida; dias: lista de (inst, dia) de la muestra (las ruedas sin operaciones cuentan 0)."""
    n = len(ops); nd = len(dias)
    out = dict(n=n, ruedas=nd, ops_por_rueda=round(n / nd, 2) if nd else 0)
    if n == 0:
        return out
    pts = ops["pts"].to_numpy()
    g = pts[pts > 0]; p = pts[pts <= 0]
    out.update(acierto=round(100 * len(g) / n, 1), gana_media=round(float(g.mean()), 2) if len(g) else 0.0,
               pierde_media=round(float(p.mean()), 2) if len(p) else 0.0, bruto_op=round(float(pts.mean()), 3),
               R_medio=round(float(ops["R"].mean()), 2), pts_en_R=round(float((ops["pts"] / ops["R"]).mean()), 3))
    for c in COSTOS:
        net = pts - c
        por_dia = ops.assign(net=net).groupby(["inst", "dia"])["net"].sum()
        serie = np.array([por_dia.get(k, 0.0) for k in dias])
        m = serie.mean(); sd = serie.std(ddof=1) if nd > 1 else 0.0
        t = m / (sd / math.sqrt(nd)) if sd > 0 else 0.0
        orden = ops.sort_values(["dia", "t"])
        racha, caida = racha_y_caida((orden["pts"] - c).to_numpy())
        k = "c%03d" % int(c * 100)
        out.update({k + "_neto_op": round(float(net.mean()), 3), k + "_por_rueda": round(float(m), 2), k + "_t": round(float(t), 2),
                    k + "_ruedas_pos": int((serie > 0).sum()), k + "_racha": int(racha), k + "_caida": round(caida, 2),
                    k + "_sharpe_d": round(float(m / sd), 4) if sd > 0 else 0.0})
        if c == 0.50:
            out["_serie050"] = serie.tolist()
    return out


def placebo_A(dias_obj, ops_por_salida, sorteos=200, semilla=19):
    """Mismas entradas (hora, precio, R), lado al azar. Se precalculan los dos lados de cada operacion."""
    rng = np.random.default_rng(semilla)
    res = {}
    for sal, ops in ops_por_salida.items():
        if len(ops) == 0:
            res[sal] = None; continue
        dos = []
        for _, o in ops.iterrows():
            d = dias_obj[(o["inst"], o["dia"])]
            fila = []
            for lado in (1, -1):
                ent = o["entrada"]; Rp = o["R"]; stop = ent - lado * Rp
                if sal == "S1": ts, px, mot = M.salida(d, int(o["t"]), lado, ent, stop, "obj", ent + lado * 2 * Rp)
                elif sal == "S2": ts, px, mot = M.salida(d, int(o["t"]), lado, ent, stop, "obj", ent + lado * 3 * Rp)
                elif sal == "S3":
                    obj = M.siguiente_nivel(d, int(o["t"]), lado, ent, Rp); ts, px, mot = M.salida(d, int(o["t"]), lado, ent, stop, "obj", obj)
                elif sal == "S5": ts, px, mot = M.salida(d, int(o["t"]), lado, ent, stop, "obj", ent + lado * o["dist_vwap"])
                elif sal == "S6": ts, px, mot = M.salida(d, int(o["t"]), lado, ent, stop, "obj", ent + lado * Rp)
                else: ts, px, mot = M.salida(d, int(o["t"]), lado, ent, stop, "perse", X=d.X)
                fila.append((px - ent) * lado)
            dos.append(fila)
        dos = np.array(dos)   # columnas: lado +1, lado -1
        real_lado = ops["lado"].to_numpy()
        real_sim = np.where(real_lado == 1, dos[:, 0], dos[:, 1])   # la version simetrica de la real (S4b para S4)
        medias = []
        for _ in range(sorteos):
            elige = rng.integers(0, 2, len(ops))
            medias.append(float(dos[np.arange(len(ops)), elige].mean()))
        medias = np.array(medias)
        res[sal] = dict(real_simetrica=round(float(real_sim.mean()), 3), placebo_media=round(float(medias.mean()), 3),
                        placebo_p95=round(float(np.quantile(medias, 0.95)), 3),
                        p=round(float((medias >= real_sim.mean()).mean()), 3))
    return res


def correr(etapa, mu, placeboB=False, cfg=None):
    cfg = cfg or ETAPAS[etapa]
    dias = muestra(mu)
    t0 = time.time()
    SAL = cfg.get("salidas", M.SALIDAS)
    dias_obj = {}; todas = {s: [] for s in SAL}
    for inst, dia in dias:
        C.frenar_si_falta_ram(2.0)
        d = M.Dia(inst, dia); dias_obj[(inst, dia)] = d
        r = M.simular(d, cfg)
        for s in SAL: todas[s] += r[s]
    ops_df = {s: pd.DataFrame(todas[s]) for s in SAL}
    res = {s: resumen_ops(ops_df[s], dias) for s in SAL}
    pA = placebo_A(dias_obj, ops_df)
    pB = None
    if placeboB:
        rng = np.random.default_rng(1919); pB = {s: [] for s in SAL}
        for k in range(40):
            tot = {s: [] for s in SAL}
            for (inst, dia), d in dias_obj.items():
                while True:
                    dl = float(rng.uniform(-2 * d.X, 2 * d.X))
                    if abs(dl) >= d.X / 2: break
                dl = round(dl / C.TICK) * C.TICK
                r = M.simular(d, cfg, delta=dl, guardar_factores=False)
                for s in SAL: tot[s] += [o["pts"] for o in r[s]]
            for s in SAL:
                pB[s].append((float(np.mean(tot[s])) if tot[s] else np.nan, len(tot[s])))
        pB = {s: dict(media_por_op=round(float(np.nanmean([a for a, _ in v])), 3), p95=round(float(np.nanquantile([a for a, _ in v], 0.95)), 3),
                      ops_medias=round(float(np.mean([b for _, b in v])), 1),
                      p=round(float(np.mean([a >= res[s].get("bruto_op", -9) for a, _ in v if np.isfinite(a)])), 3)) for s, v in pB.items()}
    for s in SAL:
        rr = {k: v for k, v in res[s].items() if not k.startswith("_")}
        if pA.get(s): rr["placeboA"] = pA[s]
        if pB: rr["placeboB"] = pB[s]
        registrar(etapa, mu, s, rr)
    allops = pd.concat([ops_df[s] for s in SAL if len(ops_df[s])], ignore_index=True) if any(len(v) for v in ops_df.values()) else pd.DataFrame()
    allops.to_csv(os.path.join(OUT, "ops-%s-%s.csv" % (etapa, mu)), index=False)
    json.dump({"etapa": etapa, "muestra": mu, "res": res, "placeboA": pA, "placeboB": pB, "seg": round(time.time() - t0, 1)},
              open(os.path.join(OUT, "res-%s-%s.json" % (etapa, mu)), "w"), indent=1)
    return res, pA, pB, ops_df, dias_obj


def imprimir(etapa, mu, res, pA, pB):
    print("\n== %s en %s ==" % (etapa, mu))
    for s in res:
        r = res[s]
        if r["n"] == 0:
            print("  %s: sin operaciones" % s); continue
        lin = ("  %s: %d ops (%.2f/rueda) | acierto %.1f %% | +%.2f / %.2f | bruto %+.3f (R %.2f, %.2f R) | c0,50 neto %+.3f, rueda %+.2f t %+.2f (%d/%d +) "
               "racha %d caida %.1f | c1,00 neto %+.3f t %+.2f") % (
            s, r["n"], r["ops_por_rueda"], r["acierto"], r["gana_media"], r["pierde_media"], r["bruto_op"], r["R_medio"], r["pts_en_R"],
            r["c050_neto_op"], r["c050_por_rueda"], r["c050_t"], r["c050_ruedas_pos"], r["ruedas"], r["c050_racha"], r["c050_caida"],
            r["c100_neto_op"], r["c100_t"])
        if pA and pA.get(s):
            lin += " | placebo A (lado al azar): real sim %+.3f vs %+.3f (p95 %+.3f, p %.3f)" % (pA[s]["real_simetrica"], pA[s]["placebo_media"], pA[s]["placebo_p95"], pA[s]["p"])
        if pB:
            lin += " | placebo B (niveles corridos): %+.3f/op (p95 %+.3f, %.0f ops, p %.3f)" % (pB[s]["media_por_op"], pB[s]["p95"], pB[s]["ops_medias"], pB[s]["p"])
        print(lin)


if __name__ == "__main__":
    etapa = sys.argv[1]; mu = sys.argv[2]; pb = len(sys.argv) > 3 and sys.argv[3] == "placeboB"
    res, pA, pB, ops, _ = correr(etapa, mu, pb)
    imprimir(etapa, mu, res, pA, pB)
