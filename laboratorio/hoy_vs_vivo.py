# -*- coding: utf-8 -*-
"""GAMMA HOY CONTRA GAMMA VIVO, EN EL MISMO GRAFICO, CON LA MISMA VARA.

Los dos indicadores anotan una linea por vela (centinela) con sus niveles
vigentes. Este script corre la prueba de respeto del laboratorio (toques con
maximos y minimos, cada nivel contra su placebo en otro strike) sobre los dos
archivos, para el mismo instrumento y marco, y pone los resultados uno al lado
del otro. La unica diferencia entre los dos es de donde salen los niveles:
Gamma Vivo del interes abierto de ayer; Gamma Hoy del volumen de hoy.

Uso:  python laboratorio/hoy_vs_vivo.py [MES] [M5]
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from es_vs_nq import velas as velas_vivo, probar_velas, rango_tipico, ATAS, t_de  # noqa
import io, json


def velas_hoy(inst, marco):
    p = os.path.join(ATAS, "pythiagex-centinela-hoy-%s-TimeFrame-%s.jsonl" % (inst, marco))
    if not os.path.exists(p):
        return []
    out = []
    for l in io.open(p, encoding="utf-8", errors="replace"):
        try:
            d = json.loads(l)
        except Exception:
            continue
        if not d.get("niv"):
            continue
        out.append(dict(t=t_de(d["t"]), o=d["o"], h=d["h"], l=d["l"], c=d["c"], niv=d["niv"]))
    out.sort(key=lambda v: v["t"])
    return out


def informe(titulo, vs, sacar, tol, lejos, placebos):
    t0, f0, ks = probar_velas(vs, sacar, tol, lejos)
    tp = fp = 0
    for c in placebos:
        a, b, _ = probar_velas(vs, sacar, tol, lejos, c)
        tp += a; fp += b
    if t0 < 12:
        return "  %-26s toques %3d: muestra insuficiente (strikes %d)" % (titulo, t0, len(ks))
    r, rp = 100.0 * f0 / t0, (100.0 * fp / tp if tp else float("nan"))
    return "  %-26s toques %3d  freno %5.1f%%  | placebo %4d  %5.1f%%  | ventaja %+5.1f pp | strikes %d" % (titulo, t0, r, tp, rp, r - rp, len(ks))


def main():
    inst = sys.argv[1] if len(sys.argv) > 1 else "MES"
    marco = sys.argv[2] if len(sys.argv) > 2 else "M5"
    placebos = (-140, -100, -60, 60, 100, 140) if inst.startswith("MNQ") else (-35, -25, -15, 15, 25, 35)
    vv, vh = velas_vivo(inst, marco), velas_hoy(inst, marco)
    print("=" * 96)
    print("GAMMA HOY (volumen de hoy) CONTRA GAMMA VIVO (interes abierto de ayer)  --  %s %s" % (inst, marco))
    print("=" * 96)
    if not vh:
        print("Gamma Hoy todavia no anoto velas en", inst, marco); return
    if not vv:
        print("Gamma Vivo no anoto velas en", inst, marco); return
    t0 = max(vv[0]["t"], vh[0]["t"]); t1 = min(vv[-1]["t"], vh[-1]["t"])
    vv = [v for v in vv if t0 <= v["t"] <= t1]; vh = [v for v in vh if t0 <= v["t"] <= t1]
    print("misma ventana: %s a %s  (Vivo %d velas, Hoy %d velas)" % (t0, t1, len(vv), len(vh)))
    rt = rango_tipico(vh) if vh else rango_tipico(vv)
    tol, lejos = 0.6 * rt, 2.5 * rt
    print("rango tipico %.2f -> toque a %.2f, venir de mas de %.2f" % (rt, tol, lejos))
    print("\nGAMMA VIVO")
    print(informe("dominantes (8, OI)", vv, lambda v: [v["niv"].get("dom%d" % i) for i in range(8)], tol, lejos, placebos))
    print(informe("zero (OI)", vv, lambda v: [v["niv"].get("zero")], tol, lejos, placebos))
    print(informe("muros (OI)", vv, lambda v: [v["niv"].get("wall_pos"), v["niv"].get("wall_neg")], tol, lejos, placebos))
    print("\nGAMMA HOY")
    print(informe("dominantes (2, volumen)", vh, lambda v: [v["niv"].get("dom0"), v["niv"].get("dom1")], tol, lejos, placebos))
    print(informe("zero (volumen)", vh, lambda v: [v["niv"].get("zero_vol")], tol, lejos, placebos))
    print(informe("zero (OI)", vh, lambda v: [v["niv"].get("zero_oi")], tol, lejos, placebos))
    print(informe("majors (volumen)", vh, lambda v: [v["niv"].get("mp_vol"), v["niv"].get("mn_vol")], tol, lejos, placebos))
    print(informe("max change 30'", vh, lambda v: [v["niv"].get("mc30")], tol, lejos, placebos))
    print("\nCOMO LEERLO: gana el que tenga mas ventaja sobre SU placebo, no el que tenga mas freno.")
    print("Menos de ~15 strikes distintos es anecdota. Hace falta rueda regular, no Labor Day.")


if __name__ == "__main__":
    main()
