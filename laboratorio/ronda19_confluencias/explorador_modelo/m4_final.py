# -*- coding: utf-8 -*-
"""RONDA 19, explorador MODELO: la regla FINAL de cada procedimiento candidato (el mismo procedimiento corrido con las 26 ruedas de
diseno), su estabilidad entre pliegues, su vecindario (umbrales corridos un decil) y las operaciones reales. Los numeros de la regla
final sobre el diseno son DENTRO DE MUESTRA (se informan como descripcion); la medida honesta es la del procedimiento fuera de pliegue.
Uso: python -I m4_final.py"""
import os
for v in ("OPENBLAS_NUM_THREADS", "MKL_NUM_THREADS", "OMP_NUM_THREADS", "NUMEXPR_NUM_THREADS"):
    os.environ[v] = "1"
import sys, json, re
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import m0_comun as C
import m1_cv as V
import m2_grilla as G
import m3_etapa2 as E

CAND = ["SGC3-S6", "T1-S6", "T1-S1", "SGC3-S1", "T2-S6"]


def regla_de_desc(desc):
    out = []
    for parte in desc.split("  (")[0].split(" & "):
        m = re.match(r"(\S+) (<=|>) (\S+)", parte.strip())
        out.append((m.group(1), m.group(2), float(m.group(3))))
    return out


def mascara(F, regla):
    m = np.ones(len(F), bool)
    for f, op, th in regla:
        m &= (F[f].to_numpy() <= th) if op == "<=" else (F[f].to_numpy() > th)
    return m


def main():
    C.prioridad_baja()
    df = C.cargar(); F = C.rasgos(df); B = C.Base(df); nombres = list(F.columns)
    X = F.to_numpy(float)
    todos = list(range(B.nd))
    P = V.Pliegue(X, B.di, todos, todos)
    Y = C.etiquetas(df)
    vs = {v["nombre"]: v for v in E.variantes2()}
    r2 = json.load(open(os.path.join(C.TMP, "real2.json")))
    out = {}
    lineas = []
    for nom in CAND:
        v = vs[nom]; sal = v["salida"]
        s, desc, ne = V.PROCS[v["proc"]](P, Y[sal], B.pts[sal] - 0.5, nombres, v["prm"], clave=sal)
        regla = regla_de_desc(desc)
        sel = mascara(F, regla)
        res = {}
        for ss in ("S1", "S6"):
            tk = C.simular(B, sel, ss); res[ss] = C.metricas(B, tk, ss)
        # estabilidad: cuantas reglas de pliegue comparten los mismos rasgos (y operadores)
        firma = tuple(sorted((f, op) for f, op, _ in regla))
        fold = [tuple(sorted((f, op) for f, op, _ in regla_de_desc(d))) if d != "ninguna" else () for _, d in r2["descripciones"][nom]]
        iguales = sum(1 for x in fold if x == firma)
        # vecindario: cada umbral corrido un decil para cada lado (dentro de muestra, descriptivo)
        vec = []
        for i, (f, op, th) in enumerate(regla):
            qs = np.quantile(F[f].dropna(), np.arange(0.05, 1.0, 0.05))
            j = int(np.argmin(np.abs(qs - th)))
            for dj in (-2, 2):
                if 0 <= j + dj < len(qs):
                    rr = list(regla); rr[i] = (f, op, float(qs[j + dj]))
                    tk = C.simular(B, mascara(F, rr), sal); mm = C.metricas(B, tk, sal)
                    vec.append(dict(cambio="%s %s %.3g" % (f, op, qs[j + dj]), n=mm["n"], acierto=mm["acierto"], neto050=mm["neto050"], t050=mm["t050"]))
        out[nom] = dict(regla=regla, desc=desc, en_muestra=res, estabilidad="%d de 26 pliegues con los mismos rasgos" % iguales, vecindario=vec,
                        oos=r2["metricas"][nom])
        lineas.append("=" * 100)
        lineas.append("%s  regla final (26 ruedas): %s" % (nom, desc))
        lineas.append("  estabilidad: %d de 26 pliegues eligieron los mismos rasgos y sentidos" % iguales)
        o = r2["metricas"][nom]
        lineas.append("  FUERA DE PLIEGUE (procedimiento): n %d, %.2f por rueda, acierto %.1f%%, bruto %+.2f, neto050 %+.2f, neto100 %+.2f, t050 %+.2f, ruedas+ %d" % (
            o["n"], o["ops_por_rueda"], o["acierto"], o["bruto_op"], o["neto050"], o["neto100"], o["t050"], o["ruedas_pos"]))
        for ss in ("S1", "S6"):
            m = res[ss]
            lineas.append("  dentro de muestra con %s: n %d, %.2f por rueda, acierto %.1f%%, bruto %+.2f, neto050 %+.2f, neto100 %+.2f, t050 %+.2f" % (
                ss, m["n"], m["ops_por_rueda"], m["acierto"], m["bruto_op"], m["neto050"], m["neto100"], m["t050"]))
        for x in vec:
            lineas.append("    vecino %-38s n %3d acierto %5.1f%% neto050 %+5.2f t %+5.2f" % (x["cambio"], x["n"], x["acierto"], x["neto050"], x["t050"]))
        tk = C.simular(B, sel, sal)
        t = df.loc[tk, ["dia", "hora_ny", "lado", "entrada", "stop", "R", sal + "_pts", sal + "_motivo", "mfe_R"]].copy()
        for f, _, _ in regla: t[f] = F.loc[tk, f].round(3).values
        lineas.append(t.to_string())
    open(os.path.join(C.AQUI, "reglas_finales.txt"), "w", encoding="utf-8").write("\n".join(lineas))
    json.dump(out, open(os.path.join(C.TMP, "finales.json"), "w"), indent=1, default=float)
    print("\n".join(lineas))


if __name__ == "__main__":
    main()
