# -*- coding: utf-8 -*-
"""RONDA 26, paso 9 (DESCRIPTIVO, agregado DESPUES del pre-registro; no entra en ninguna decision): QUIEN ACOMPANA AL PRECIO MINUTO A MINUTO.
Con las fotos al cierre de cada minuto RTH (las de r26_01): por rueda, correlacion entre la variacion del precio del minuto (ultimo - ultimo
anterior) y el delta del minuto de cada tramo de tamano (ES: 1-9, 10-49, 50+; MES: 1-9, 10-49, 50+ en sus contratos), y la del delta total.
Un tramo con correlacion alta EMPUJA o acompana el precio en el mismo minuto; uno con correlacion baja o negativa le pone el pecho (agrede
contra lo que el precio hace en ese minuto). Mediana de las ruedas y rango.
Uso: python -I r26_09_tramos_minuto.py <tanda>"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import r26_comun as C

C.prioridad_baja()
GRUPOS = {"ES": {"1-9": ["1", "2-4", "5-9"], "10-49": ["10-19", "20-49"], "50+": ["50+"]},
          "MES": {"1-9": ["1", "2-4", "5-9"], "10-49": ["10-19", "20-49"], "50+": ["50-99", "100-199", "200+"]}}


def main():
    tanda = sys.argv[1]
    pares = json.load(open(os.path.join(C.DATOS, "pares.json")))
    dias = sorted(d for d, v in pares.items() if v["tanda"] == tanda and v["alinea"])
    print("RONDA 26 - quien acompana al precio minuto a minuto (descriptivo, post hoc), %s: %d ruedas" % (tanda, len(dias)))
    res = {}
    for dia in dias:
        for inst in ("ES", "MES"):
            F = C.cargar(inst, dia)["fotos"]
            dp = np.diff(F["ultimo"].to_numpy())
            fila = {"total": np.corrcoef(dp, np.diff(F["cum"].to_numpy()))[0, 1]}
            for g, ks in GRUPOS[inst].items():
                x = sum(F["cum_" + k].to_numpy() for k in ks)
                fila[g] = np.corrcoef(dp, np.diff(x))[0, 1]
                fila["vol_" + g] = None
            res[(dia, inst)] = fila
    for inst in ("ES", "MES"):
        print("   %s: correlacion de la variacion del precio del minuto con el delta del minuto (mediana de %d ruedas [min; max])" % (inst, len(dias)))
        for g in ["total"] + list(GRUPOS[inst]):
            v = np.array([res[(d, inst)][g] for d in dias])
            print("      %-6s %+.2f  [%+.2f; %+.2f]" % (g, np.median(v), v.min(), v.max()))
    a = np.array([res[(d, "MES")]["total"] for d in dias])
    b = np.array([res[(d, "ES")]["total"] for d in dias])
    print("   PREDICCION (a): correlacion precio-delta del MES > la del ES en %d de %d ruedas (%.0f %%): %s" %
          ((a > b).sum(), len(a), 100 * (a > b).mean(), "SE CUMPLE (>= 80 %)" if (a > b).mean() >= 0.8 else "NO se cumple"))
    print("   por rueda: " + " | ".join("%s MES %+.2f / ES %+.2f" % (d[5:], x, y) for d, x, y in zip(dias, a, b)))


if __name__ == "__main__":
    main()
