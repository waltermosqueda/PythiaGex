# -*- coding: utf-8 -*-
"""VALIDACION del unico bolsillo que le gano al placebo en gatillo_rebote.py: PRIMER TOQUE de la
DOMINANTE ACTUAL con el ZERO A FAVOR (largo si el precio esta sobre el zero, corto si debajo).
Pruebas: por mitades de dias (1ra/2da), por dia, sensibilidad a los parametros del toque, y el
mismo bolsillo en otro instrumento. Si no aguanta las tres, es azar de haber mirado 20 tablas.
Uso: python laboratorio/gatillo_rebote_validar.py MNQ
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gatillo_rebote as gr  # noqa: E402
from rebote_dominantes_dia import cargar  # noqa: E402


def bolsillo(e):
    return e["n_toque"] == 1 and e["actual"] and gr.a_favor(e, "sobre_zero")


def resumen(ev, nd):
    if not ev:
        return "sin casos"
    p = lambda r: 100.0 * sum(1 for e in ev if e["res"][r]) / len(ev)
    return "%3d (%4.1f/dia) +10/-10 %5.1f  +15/-15 %5.1f  +20/-20 %5.1f  +20/-10 %5.1f  +10/-20 %5.1f" % (
        len(ev), len(ev) / nd, p((10, 10)), p((15, 15)), p((20, 20)), p((20, 10)), p((10, 20)))


def main():
    inst = sys.argv[1] if len(sys.argv) > 1 else "MNQ"
    es_nq = inst.startswith("MNQ") or inst.startswith("NQ")
    gr.ESCALA[0] = 1.0 if es_nq else 0.25
    f = 1.0 if es_nq else 0.25
    paso = 10.0 if es_nq else 5.0
    placebos = (-145, -85, 85, 145) if es_nq else (-37, -22, 22, 37)
    vs = cargar(inst)
    dias = sorted(set(v["dia"] for v in vs)); nd = float(len(dias))
    print("%s: %d dias (%s .. %s)" % (inst, len(dias), dias[0], dias[-1]))
    variantes = [("base tol 10 pen 25 ret 15", 10, 25, 15), ("estricta tol 8 pen 15 ret 20", 8, 15, 20), ("laxa tol 12 pen 35 ret 10", 12, 35, 10), ("sin traspaso tol 10 pen 0 ret 15", 10, 0, 15)]
    for nombre, tol, pen, ret in variantes:
        real = [e for e in gr.fuegos(vs, tol * f, pen * f, ret * f, 5, 0.0, paso) if bolsillo(e)]
        plc = []
        for cc in placebos:
            plc += [e for e in gr.fuegos(vs, tol * f, pen * f, ret * f, 5, cc, paso) if bolsillo(e)]
        print("  %s" % nombre)
        print("    real    " + resumen(real, nd))
        print("    placebo " + resumen(plc, nd * len(placebos)))
        if nombre.startswith("base"):
            mitad = dias[len(dias) // 2]
            for tit, cond in (("1ra mitad", lambda e: e["dia"] < mitad), ("2da mitad", lambda e: e["dia"] >= mitad)):
                r = [e for e in real if cond(e)]; q = [e for e in plc if cond(e)]
                print("    %s real    %s" % (tit, resumen(r, nd / 2)))
                print("    %s placebo %s" % (tit, resumen(q, nd / 2 * len(placebos))))
            print("    por dia (+20/-20 aciertos/disparos): " + " ".join("%s %d/%d" % (d[5:], sum(1 for e in real if e["dia"] == d and e["res"][(20, 20)]), sum(1 for e in real if e["dia"] == d)) for d in dias))
            print("    por lado: " + " | ".join("%s %s" % (l, resumen([e for e in real if e["lado"] == l], nd)) for l in ("abajo", "arriba")))
            print("    por franja: " + " | ".join("%s %s" % (fr, resumen([e for e in real if e["franja"] == fr], nd)) for fr in ("09:30-10:30", "10:30-12:00", "12:00-14:00", "14:00-16:00")))
            print("    detalle (hora local, lado, nivel, cierre, MFE, MAE, +20/-20):")
            for e in real:
                print("      %s %s %-7s %8.1f c %8.2f MFE %5.1f MAE %5.1f %s" % (e["dia"][5:], (e["t"] - gr.timedelta(hours=3)).strftime("%H:%M"), e["lado"], e["L"], e["c"], e["mfe"], e["mae"], "GANA" if e["res"][(20, 20)] else "pierde"))


if __name__ == "__main__":
    main()
