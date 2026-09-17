# -*- coding: utf-8 -*-
"""punta_04_confirmar.py — los finalistas, UNA sola vez, en las 8 sesiones de confirmar, con placebo de 200 sorteos.
Ninguna variante califico en explorar (ninguna llego al empate de 55,3 %). Por la regla 4 del pre-registro van dos 'finalistas de consuelo',
las de mayor |z| en la barrera principal:
  P02_qi_30 con el lado INVERTIDO (z = -2,32 en explorar: punta cargada 30 s hacia un lado -> el precio va para el OTRO)
  P10_engrosa_contra_mov con el lado original (z = +2,18: tras un movimiento fuerte, punta cargada en contra -> rebote)
Una de consuelo nunca pasa de PISTA. Deja un testigo (punta_cache/confirmado.txt) para no correrse dos veces por error."""
import os, sys; sys.path.insert(0, r"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\gatillo")
import numpy as np, pandas as pd, base as B, punta_lib as L, punta_03_explorar as X

FINALISTAS = ["P02_qi_30", "P10_engrosa_contra_mov"]; INVERTIR = ("P02_qi_30",)
testigo = os.path.join(L.CACHE, "confirmado.txt")
if os.path.exists(testigo) and "--repetir-identico" not in sys.argv:
    raise SystemExit("la confirmacion YA se corrio (%s). No se repite." % open(testigo).read().strip())
T = B.todas_las_sesiones(); S = B.confirmar(T); del T
print("CONFIRMAR: %d sesiones %s | placebo 200 sorteos" % (len(S), list(S)))
out = X.correr(S, sorteos=200, solo=FINALISTAS, invertir=INVERTIR); df = X.imprimir(out)
open(testigo, "w").write(pd.Timestamp.now().isoformat())

# detalle por dia en la barrera principal
print("\npor dia, barrera +-8/600 s:")
for k in FINALISTAS:
    signo = -1 if k in INVERTIR else 1; fila = []
    for s, d in S.items():
        lado = L.variantes(L.rasgos(d), L.U)[k]; i = L.desagrupar(lado, L.elegible(d)); y = L.barrera_cache(s, d, 8, 600)[0][i]; ll = signo * lado[i]
        r = y != 0; fila.append("%s %d/%d=%.0f%%" % (s[5:], int(((y[r] * ll[r]) > 0).sum()), int(r.sum()), 100 * float(((y[r] * ll[r]) > 0).mean()) if r.sum() else float("nan")))
    print(" ", k, "|", " | ".join(fila))
