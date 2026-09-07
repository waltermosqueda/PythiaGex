# -*- coding: utf-8 -*-
"""DOS FUENTES, LA MISMA CUENTA, EL MISMO DIA: CUANTO DIFIEREN LOS NIVELES.

Para el 2026-09-03 tenemos la cadena de dos lados: las 706 fotos crudas de
CBOE que radar.py bajo en esta maquina (lo que el indicador VIO ese dia) y la
cadena reconstruida desde Databento (OPRA: OI + volumen por minuto + IV del
ultimo precio operado). Rebobina corre la misma cuenta sobre las dos y anota
dos centinelas. Este script los alinea minuto a minuto y mide, por nivel, la
diferencia mediana y el porcentaje de minutos en que coinciden a menos de
medio strike. Si Databento reproduce a CBOE, el simulador vale para los dias
que CBOE ya no puede darnos.

Uso:  python laboratorio/cruzar_fuentes.py rebobinadoCBOE rebobinadoDB [MES] [M1]
"""
import io
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from es_vs_nq import ATAS  # noqa: E402


def cargar(prefijo, inst, marco):
    p = os.path.join(ATAS, "pythiagex-centinela-%s-%s-TimeFrame-%s.jsonl" % (prefijo, inst, marco))
    out = {}
    if not os.path.exists(p):
        return out, p
    for l in io.open(p, encoding="utf-8", errors="replace"):
        try:
            d = json.loads(l)
        except Exception:
            continue
        out[d["t"]] = d
    return out, p


def mediana(xs):
    xs = sorted(xs)
    return xs[len(xs) // 2] if xs else float("nan")


def main():
    a = [x for x in sys.argv[1:] if not x.startswith("--")]
    pa, pb = a[0], a[1]
    inst = a[2] if len(a) > 2 else "MES"
    marco = a[3] if len(a) > 3 else "M1"
    A, ra = cargar(pa, inst, marco)
    B, rb = cargar(pb, inst, marco)
    print("A = %s (%d velas)\nB = %s (%d velas)" % (ra, len(A), rb, len(B)))
    comunes = sorted(set(A) & set(B))
    print("minutos en comun: %d" % len(comunes))
    if not comunes:
        return
    print("\n%-14s %8s %10s %10s %10s" % ("nivel", "minutos", "dif media", "dif medna", "<=2.5 pts"))
    for k in ("zero_vol", "zero_oi", "mp_vol", "mn_vol", "mp_oi", "mn_oi", "dom0", "dom1", "pico", "mc30", "mc5"):
        difs = []
        for t in comunes:
            x, y = A[t]["niv"].get(k), B[t]["niv"].get(k)
            if x and y:
                difs.append(abs(x - y))
        if not difs:
            print("%-14s %8d" % (k, 0)); continue
        print("%-14s %8d %10.2f %10.2f %9.0f%%" % (k, len(difs), sum(difs) / len(difs), mediana(difs), 100.0 * sum(1 for d in difs if d <= 2.5) / len(difs)))
    q = sum(1 for t in comunes if A[t]["niv"].get("q_cuadrante") == B[t]["niv"].get("q_cuadrante"))
    print("\ncuadrante igual en %d de %d minutos (%.0f%%)" % (q, len(comunes), 100.0 * q / len(comunes)))
    sp = [abs(A[t]["spot"] - B[t]["spot"]) for t in comunes if A[t].get("spot") and B[t].get("spot")]
    if sp:
        print("spot (indice = futuro - base): diferencia media %.2f, mediana %.2f puntos -> es la BASE lo que difiere" % (sum(sp) / len(sp), mediana(sp)))


if __name__ == "__main__":
    main()
