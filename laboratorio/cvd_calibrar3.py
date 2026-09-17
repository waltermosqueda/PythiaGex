# -*- coding: utf-8 -*-
"""cvd_calibrar3.py — tercera vuelta: la regla elegida (eficaz sostenido) con la regla del DOJI, y la confluencia vieja contra la nueva."""
import sys, json, os, statistics as st
sys.path.insert(0, os.path.dirname(__file__))
import cvd_calibrar as C, cvd_calibrar2 as C2

def eficaz_sostenido(vs, z1, beta, doji=0.0, avance=0.25, umbral=0.4):
    """LA REGLA ELEGIDA. Color de la vela solo si el precio ACOMPAÑA al delta (cuerpo del mismo signo, que no sea una
    vela de indecision, y que avance al menos 'avance' de lo que ese delta suele mover). Se sostiene UNA vela floja.
    Pasa a neutro: en una vela de indecision (doji/martillo), si aparece flujo en contra sin precio (posible absorcion),
    o a la segunda vela seguida sin efecto."""
    n = len(vs); d = [v["d"] for v in vs]; out, efs, act = [], [], 0
    for k in range(n):
        v = vs[k]; cuerpo = v["c"] - v["o"]; rango = max(1e-9, v["h"] - v["l"])
        s = 1 if z1[k] > umbral else -1 if z1[k] < -umbral else 0
        es_doji = doji > 0 and abs(cuerpo) < doji * rango
        ef = s if (s != 0 and cuerpo * s > 0 and not es_doji and (beta[k] == 0 or abs(cuerpo) >= avance * beta[k] * abs(d[k]))) else 0
        if ef != 0: act = ef
        elif es_doji: act = 0
        elif s != 0 and s == -act: act = 0
        elif efs and efs[-1] == 0: act = 0
        efs.append(ef); out.append(act)
    return out


def main():
    inst = next((a for a in sys.argv[1:] if a in ("MNQ", "MES")), "MNQ"); marco = next((a for a in sys.argv[1:] if a in ("M1", "M2")), "M1")
    vs = [v for v in C.cargar(inst, marco) if "13:30" <= v["t"][11:16] <= "20:00"]; piv = C.pivotes(vs); L = C.lecturas(vs)
    n = len(vs); d = [v["d"] for v in vs]; z1 = L["suma1"]
    beta = []
    for k in range(n):
        w = [abs(vs[j]["c"] - vs[j]["o"]) / abs(d[j]) for j in range(max(0, k - 120), k) if abs(d[j]) >= 50]
        beta.append(st.median(w) if len(w) >= 20 else 0)
    print("%s %s: %d velas, %d giros" % (inst, marco, n, len(piv)))
    print("%-24s | retraso: med  prom  <=1 vela nunca | cambios/h duran 1 | con la vela: acuerda  en contra | neutro" % "lectura")
    E = {"suma5 (hoy)": C.estados(L["suma5"])[0]}
    for dj in (0.0, 0.2, 0.3, 0.4):
        E["eficaz sost. doji<%.0f%%" % (dj * 100)] = eficaz_sostenido(vs, z1, beta, doji=dj)
    for nombre, e in E.items(): C2.medir_estados(vs, nombre, e, piv, marco)
    if "--ejemplo" in sys.argv:
        t0 = sys.argv[sys.argv.index("--ejemplo") + 1]; idx = [k for k, v in enumerate(vs) if v["t"][:16] >= t0][:12]
        print("\nEJEMPLO (hora local, cuerpo/rango, delta):      " + "  ".join(nm[-9:] for nm in E))
        for k in idx:
            v = vs[k]; hl = "%02d:%s" % ((int(v["t"][11:13]) - 3) % 24, v["t"][14:16])
            print("%s %+6.1f /%5.1f d%+5d             " % (hl, v["c"] - v["o"], v["h"] - v["l"], v["d"]) + "  ".join("%-9s" % ("+" if E[nm][k] > 0 else "-" if E[nm][k] < 0 else ".") for nm in E))

if __name__ == "__main__":
    main()
