# -*- coding: utf-8 -*-
"""REBOTES EN LAS DOMINANTES DEL DIA (el conjunto acumulado, como las rayas amarillas que ve el
operador): cada minuto, los niveles son TODAS las dominantes que hubo hoy hasta ese momento
(redondeadas al strike) mas los majors. Toque = la vela llega a +-TOL del nivel viniendo de
>= LEJOS pts (5 velas antes), con traspaso maximo PEN; rebote = REB pts a favor antes que REB
en contra en HOR min, desde el cierre de la vela del toque. Placebo = los mismos niveles
corridos +-X. Se listan los eventos de un dia (--dia) o se resume todo, con filtros.
Uso: python laboratorio/rebote_dominantes_dia.py MNQ [--dia 2026-09-10]
"""
import io
import json
import os
import sys
from collections import defaultdict
from datetime import datetime, timedelta

ATAS = os.path.join(os.environ.get("APPDATA", ""), "ATAS")


def cargar(inst, marco="M1"):
    vs = {}
    for fn in ("rebobinado-atas-", "hoy-"):
        p = os.path.join(ATAS, "pythiagex-centinela-%s%s-TimeFrame-%s.jsonl" % (fn, inst, marco))
        if not os.path.exists(p):
            continue
        for l in io.open(p, encoding="utf-8", errors="replace"):
            try:
                d = json.loads(l)
            except Exception:
                continue
            if d.get("niv") and "13:30" <= d["t"][11:16] < "20:00" and d.get("vol", 0) > 0:
                if d["t"] not in vs or fn == "rebobinado-atas-":
                    vs[d["t"]] = d
    out = [vs[t] for t in sorted(vs)]
    for v in out:
        v["dia"] = v["t"][:10]
    return out


def eventos(vs, tol, lejos, pen, reb, hor, corr=0.0, paso=10.0):
    out = []; niveles = set(); dia = None
    for i, v in enumerate(vs):
        if v["dia"] != dia:
            dia = v["dia"]; niveles = set()
        n = v["niv"]; c = v["c"]
        for k in ("dom0", "dom1", "mp_vol", "mn_vol"):
            if n.get(k):
                niveles.add(round((n[k] + corr) / paso) * paso)
        if i < 5 or vs[i - 5]["dia"] != v["dia"]:
            continue
        c0 = vs[i - 5]["c"]; p = vs[i - 1]
        for L in sorted(niveles):
            for lado in ("abajo", "arriba"):
                if lado == "abajo":
                    toca = v["l"] <= L + tol and v["l"] >= L - pen and c0 >= L + lejos
                    tocaba = p["l"] <= L + tol and p["l"] >= L - pen
                else:
                    toca = v["h"] >= L - tol and v["h"] <= L + pen and c0 <= L - lejos
                    tocaba = p["h"] >= L - tol and p["h"] <= L + pen
                if not toca or tocaba:
                    continue
                cierra_bien = c > L if lado == "abajo" else c < L
                rango = v["h"] - v["l"]
                mecha = (((min(v["o"], c) - v["l"]) if lado == "abajo" else (v["h"] - max(v["o"], c))) / rango) if rango > 0 else 0.0
                ent = c; res = None; mfe = 0.0
                for j in range(i + 1, min(len(vs), i + 1 + hor)):
                    w = vs[j]
                    if w["dia"] != v["dia"]:
                        break
                    fav = (w["h"] - ent) if lado == "abajo" else (ent - w["l"])
                    con = (ent - w["l"]) if lado == "abajo" else (w["h"] - ent)
                    mfe = max(mfe, fav)
                    if fav >= reb and con >= reb:
                        res = None; break
                    if fav >= reb:
                        res = True; break
                    if con >= reb:
                        res = False; break
                if res is None:
                    continue
                t = datetime.strptime(v["t"][:19], "%Y-%m-%dT%H:%M:%S")
                hr = (t.hour - 4) * 60 + t.minute
                zero = n.get("zero_vol")
                out.append(dict(t=t, dia=v["dia"], lado=lado, L=L, ext=v["l"] if lado == "abajo" else v["h"], c=c, delta=v["delta"],
                                cierra=cierra_bien, mecha=mecha, res=res, mfe=mfe, q=n.get("q_cuadrante"),
                                franja="09:30-10:30" if hr < 630 else "10:30-12:00" if hr < 720 else "12:00-14:00" if hr < 840 else "14:00-16:00",
                                tend="alcista" if (zero and c > zero) else "bajista"))
    return out


def regla(e):
    return ((e["lado"] == "abajo") == (e["tend"] == "alcista")) and e["cierra"]


def main():
    a = [x for x in sys.argv[1:] if not x.startswith("--")]
    inst = a[0] if a else "MNQ"
    arg = lambda k, d: type(d)(sys.argv[sys.argv.index(k) + 1]) if k in sys.argv else d
    es_nq = inst.startswith("MNQ") or inst.startswith("NQ")
    tol = arg("--tol", 8.0 if es_nq else 2.0); lejos = arg("--lejos", 25.0 if es_nq else 6.0); pen = arg("--pen", 15.0 if es_nq else 4.0)
    reb = arg("--reb", 20.0 if es_nq else 5.0); hor = arg("--hor", 20); paso = 10.0 if es_nq else 5.0
    vs = cargar(inst)
    dia = arg("--dia", "")
    if dia:
        ev = [e for e in eventos(vs, tol, lejos, pen, reb, hor, 0.0, paso) if e["dia"] == dia]
        print("%s %s: %d toques a dominantes/majors del dia (niveles acumulados)" % (inst, dia, len(ev)))
        print("hora   lado    nivel   extremo   cierre  delta cierra mecha tend    q rebote  MFE  regla")
        for e in ev:
            print("%s %-7s %8.0f %8.2f %8.2f %6d %s %5.2f %-7s %s %-5s %5.1f %s" % ((e["t"] - timedelta(hours=3)).strftime("%H:%M"), e["lado"], e["L"], e["ext"], e["c"], round(e["delta"]), "SI " if e["cierra"] else "no ", e["mecha"], e["tend"], e["q"], e["res"], e["mfe"], "<-- REGLA" if regla(e) else ""))
        return
    real = eventos(vs, tol, lejos, pen, reb, hor, 0.0, paso)
    placebos = (-145, -85, 85, 145) if es_nq else (-37, -22, 22, 37)
    plc = []
    for cc in placebos:
        plc += eventos(vs, tol, lejos, pen, reb, hor, cc, paso)
    dias = sorted(set(v["dia"] for v in vs)); nd = float(len(dias))
    print("%s: %d dias | toques %d (%.1f/dia) rebota %.1f %% | placebo %d, %.1f %%" % (
        inst, len(dias), len(real), len(real) / nd, 100.0 * sum(e["res"] for e in real) / max(1, len(real)), len(plc), 100.0 * sum(e["res"] for e in plc) / max(1, len(plc))))

    def tabla(tit, clave, minimo=15):
        print("  " + tit)
        g = defaultdict(list); gp = defaultdict(list)
        for e in real: g[clave(e)].append(e["res"])
        for e in plc: gp[clave(e)].append(e["res"])
        filas = [(k, len(r), 100.0 * sum(r) / len(r), len(gp.get(k, [])), (100.0 * sum(gp[k]) / len(gp[k])) if gp.get(k) else float("nan")) for k, r in g.items() if len(r) >= minimo]
        for k, n, pr, npl, pp in sorted(filas, key=lambda f: -(f[2] - f[4] if f[4] == f[4] else -99)):
            print("    %-46s %4d (%.1f/dia) rebota %5.1f %% | placebo %4d %5.1f %% | %+5.1f pp" % (str(k), n, n / nd, pr, npl, pp, pr - pp))
    tabla("por lado:", lambda e: e["lado"])
    tabla("con la tendencia (abajo si el precio esta sobre el zero, arriba si debajo) o contra:", lambda e: "a favor de la tendencia" if (e["lado"] == "abajo") == (e["tend"] == "alcista") else "contra la tendencia")
    tabla("cierra del lado bueno:", lambda e: "cierra bien" if e["cierra"] else "cierra mal")
    tabla("mecha de rechazo >= 0,4:", lambda e: "mecha grande" if e["mecha"] >= 0.4 else "mecha chica")
    tabla("franja:", lambda e: e["franja"])
    tabla("cuadrante:", lambda e: {1: "IMAN", 2: "EXPLOSIVO", 3: "ESTABLE", 4: "RIESGO"}.get(e["q"], "?"))
    tabla("REGLA (a favor de la tendencia + cierra bien):", lambda e: "REGLA" if regla(e) else "resto")
    tabla("REGLA + mecha >= 0,4:", lambda e: "REGLA+mecha" if (regla(e) and e["mecha"] >= 0.4) else "resto")
    tabla("REGLA por franja:", lambda e: ("REGLA", e["franja"]) if regla(e) else "resto", 10)
    tabla("REGLA por cuadrante:", lambda e: ("REGLA", {1: "IMAN", 2: "EXPLOSIVO", 3: "ESTABLE", 4: "RIESGO"}.get(e["q"], "?")) if regla(e) else "resto", 10)
    pd = defaultdict(lambda: [0, 0])
    for e in real:
        if regla(e):
            pd[e["dia"]][1] += 1; pd[e["dia"]][0] += 1 if e["res"] else 0
    print("  REGLA por dia (aciertos/toques): " + " ".join("%s %d/%d" % (d[5:], v[0], v[1]) for d, v in sorted(pd.items())))


if __name__ == "__main__":
    main()
