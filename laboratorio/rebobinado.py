# -*- coding: utf-8 -*-
"""EL REBOBINADO, JUZGADO CON LA MISMA VARA QUE EL VIVO.

Rebobina (atas/Rebobina) corre Gamma Hoy sobre dias pasados con datos de
Databento y anota el centinela con el prefijo "rebobinado-". Este script le
aplica la prueba de respeto del laboratorio (toques con maximos y minimos,
freno = se alejo mas de lo que atraveso, cada nivel contra su placebo en otro
strike) y, si existe, pone al lado el centinela vivo de Gamma Hoy del mismo
instrumento y marco.

Uso:  python laboratorio/rebobinado.py [MES] [M1] [--nombre rebobinado]
"""
import io
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from es_vs_nq import probar_velas, rango_tipico, ATAS, t_de  # noqa: E402


def velas_de(prefijo, inst, marco):
    p = os.path.join(ATAS, "pythiagex-centinela-%s-%s-TimeFrame-%s.jsonl" % (prefijo, inst, marco))
    if not os.path.exists(p):
        return [], p
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
    return out, p


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


def bloque(nombre, vs, placebos):
    rt = rango_tipico(vs)
    tol, lejos = 0.6 * rt, 2.5 * rt
    print("\n%s: %d velas, %s a %s, rango tipico %.2f -> toque a %.2f, venir de mas de %.2f" % (nombre, len(vs), vs[0]["t"], vs[-1]["t"], rt, tol, lejos))
    print(informe("dominantes (2, volumen)", vs, lambda v: [v["niv"].get("dom0"), v["niv"].get("dom1")], tol, lejos, placebos))
    print(informe("dominante 1 sola", vs, lambda v: [v["niv"].get("dom0")], tol, lejos, placebos))
    print(informe("zero (volumen)", vs, lambda v: [v["niv"].get("zero_vol")], tol, lejos, placebos))
    print(informe("zero (OI)", vs, lambda v: [v["niv"].get("zero_oi")], tol, lejos, placebos))
    print(informe("majors (volumen)", vs, lambda v: [v["niv"].get("mp_vol"), v["niv"].get("mn_vol")], tol, lejos, placebos))
    print(informe("majors (OI)", vs, lambda v: [v["niv"].get("mp_oi"), v["niv"].get("mn_oi")], tol, lejos, placebos))
    print(informe("max change 30'", vs, lambda v: [v["niv"].get("mc30")], tol, lejos, placebos))
    print(informe("max change 5'", vs, lambda v: [v["niv"].get("mc5")], tol, lejos, placebos))
    print(informe("pico cerca del precio", vs, lambda v: [v["niv"].get("pico")], tol, lejos, placebos))
    cu = {}
    for v in vs:
        q = v["niv"].get("q_cuadrante")
        if q:
            cu[int(q)] = cu.get(int(q), 0) + 1
    print("  cuadrantes (velas): " + ", ".join("%d=%d" % (k, cu[k]) for k in sorted(cu)))


def main():
    a = [x for x in sys.argv[1:] if not x.startswith("--")]
    inst = a[0] if len(a) > 0 else "MES"
    marco = a[1] if len(a) > 1 else "M1"
    nombre = "rebobinado"
    if "--nombre" in sys.argv:
        nombre = sys.argv[sys.argv.index("--nombre") + 1]
    placebos = (-140, -100, -60, 60, 100, 140) if inst.startswith("MNQ") else (-35, -25, -15, 15, 25, 35)
    print("=" * 100)
    print("REBOBINADO (Databento, afuera de ATAS) -- %s %s" % (inst, marco))
    print("=" * 100)
    vr, pr = velas_de(nombre, inst, marco)
    if not vr:
        print("no hay centinela rebobinado en", pr); return
    bloque("REBOBINADO " + os.path.basename(pr), vr, placebos)
    vh, ph = velas_de("hoy", inst, marco)
    if vh:
        bloque("GAMMA HOY EN VIVO " + os.path.basename(ph), vh, placebos)
    print("\nCOMO LEERLO: gana el que tenga mas ventaja sobre SU placebo, no el que tenga mas freno.")
    print("Un dia son ~20 strikes distintos como mucho: anecdota. Hacen falta 15+ ruedas.")


if __name__ == "__main__":
    main()
