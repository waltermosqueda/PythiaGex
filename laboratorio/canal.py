# -*- coding: utf-8 -*-
"""EL CANAL: LAS DOMINANTES COMO BANDA, JUZGADAS CONTRA PLACEBO.

El operador ve que MNQ "respeta el canal" entre dos dominantes aunque no las
toque por 20-25 puntos, y pide dominantes mas permisivas. Esto lo mide en vez
de discutirlo: para varias anchuras de banda (en puntos), cuenta cuantas veces
el precio llego a la banda viniendo de lejos y freno (se alejo mas de lo que
atraveso en 15 minutos), y hace LO MISMO con la banda corrida a otro strike
(placebo). Una banda ancha hace que todo "respete", placebo incluido: por eso
lo que vale es la ventaja sobre el placebo, no el porcentaje de freno.

Uso:  python laboratorio/canal.py [MNQ] [M5] [--prefijo hoy-|rebobinado-atas-|""]
"""
import io
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from es_vs_nq import probar_velas, rango_tipico, ATAS, t_de  # noqa: E402


def velas(prefijo, inst, marco):
    p = os.path.join(ATAS, "pythiagex-centinela-%s%s-TimeFrame-%s.jsonl" % (prefijo, inst, marco))
    out = []
    if not os.path.exists(p):
        return out, p
    for l in io.open(p, encoding="utf-8", errors="replace"):
        try:
            d = json.loads(l)
        except Exception:
            continue
        if d.get("niv"):
            out.append(dict(t=t_de(d["t"]), o=d["o"], h=d["h"], l=d["l"], c=d["c"], niv=d["niv"]))
    out.sort(key=lambda v: v["t"])
    return out, p


def main():
    a = [x for x in sys.argv[1:] if not x.startswith("--")]
    inst = a[0] if a else "MNQ"
    marco = a[1] if len(a) > 1 else "M5"
    prefijo = "hoy-"
    if "--prefijo" in sys.argv:
        prefijo = sys.argv[sys.argv.index("--prefijo") + 1]
    vs, p = velas(prefijo, inst, marco)
    if len(vs) < 30:
        print("%s: %d velas, poco (%s)" % (inst, len(vs), p)); return
    es_nq = inst.startswith("MNQ") or inst.startswith("NQ")
    placebos = (-140, -100, -60, 60, 100, 140) if es_nq else (-35, -25, -15, 15, 25, 35)
    bandas = (0, 5, 10, 15, 20, 25, 35) if es_nq else (0, 1, 2, 4, 6, 8)
    doms = [k for k in ("dom0", "dom1", "dom2", "dom3") if any(v["niv"].get(k) for v in vs)]
    rt = rango_tipico(vs)
    print("%s %s %s: %d velas, %s a %s, rango tipico %.1f, dominantes %s" % (prefijo or "vivo-", inst, marco, len(vs), vs[0]["t"], vs[-1]["t"], rt, "/".join(doms)))
    sacar = lambda v: [v["niv"].get(k) for k in doms]
    for b in bandas:
        tol = max(0.6 * rt, b)
        t0, f0, ks = probar_velas(vs, sacar, tol, 2.5 * rt + b)
        tp = fp = 0
        for c in placebos:
            x, y, _ = probar_velas(vs, sacar, tol, 2.5 * rt + b, c); tp += x; fp += y
        if t0 < 8:
            print("   banda ±%2d pts: %d toques, muestra insuficiente" % (b, t0)); continue
        r, rp = 100.0 * f0 / t0, (100.0 * fp / tp if tp else float("nan"))
        print("   banda ±%2d pts: toques %3d freno %5.1f%% | placebo %4d %5.1f%% | ventaja %+5.1f pp | niveles %d" % (b, t0, r, tp, rp, r - rp, len(ks)))
    print("COMO LEERLO: si el placebo tambien frena el 100 %, la banda es tan ancha que ya no mide nada.")


if __name__ == "__main__":
    main()
