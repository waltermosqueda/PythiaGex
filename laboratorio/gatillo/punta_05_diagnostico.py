# -*- coding: utf-8 -*-
"""punta_05_diagnostico.py — DESPUES de confirmar, sin retocar nada: por que P02 tuvo 154 disparos sin resolver en confirmar?
Sospecha: huecos de datos (cortes de Rithmic el 14-09 y 16-09) donde la punta queda congelada por el relleno hacia adelante.
Solo higiene de datos: cuenta disparos caidos en huecos y rehace la cuenta sin ellos. El veredicto NO cambia por esto."""
import sys; sys.path.insert(0, r"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\gatillo")
import numpy as np, pandas as pd, base as B, punta_lib as L

T = B.todas_las_sesiones(); S = B.confirmar(T); del T
for k, signo in (("P02_qi_30", -1), ("P10_engrosa_contra_mov", 1)):
    print("\n", k, "(invertida)" if signo < 0 else "")
    A = [0, 0]; Bb = [0, 0]
    for s, d in S.items():
        lado = L.variantes(L.rasgos(d), L.U)[k]; i = L.desagrupar(lado, L.elegible(d)); y = L.barrera_cache(s, d, 8, 600)[0][i]; ll = signo * lado[i]
        n30 = d["n"].rolling(30).sum().to_numpy()[i]; hueco = n30 < 5          # menos de 5 ordenes en los ultimos 30 s = dato cortado
        ac = (y * ll) > 0; r = y != 0
        print("  %s disparos %3d | sin resolver %3d | en hueco de datos %3d | acierto todos %.1f %% | fuera de hueco %.1f %% (n %d)" % (
            s, len(i), int((~r).sum()), int(hueco.sum()), 100 * ac[r].mean(), 100 * ac[r & ~hueco].mean(), int((r & ~hueco).sum())))
        A[0] += int(ac[r].sum()); A[1] += int(r.sum()); Bb[0] += int(ac[r & ~hueco].sum()); Bb[1] += int((r & ~hueco).sum())
    print("  TOTAL todos %.1f %% (n %d) | fuera de huecos %.1f %% (n %d)" % (100 * A[0] / A[1], A[1], 100 * Bb[0] / Bb[1], Bb[1]))
print("\nhuecos por sesion (segundos de rueda con menos de 5 ordenes en los ultimos 30 s):")
for s, d in S.items():
    r = d["rueda"].to_numpy(); n30 = d["n"].rolling(30).sum().to_numpy(); print("  %s: %5d s (%.1f %%)" % (s, int(((n30 < 5) & r).sum()), 100 * ((n30 < 5) & r).sum() / max(1, r.sum())))
