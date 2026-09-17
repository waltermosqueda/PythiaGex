# -*- coding: utf-8 -*-
"""Paso 6 (metodologico): el placebo 'misma media hora, mismos lados' ¿esta sesgado cuando el lado depende del movimiento que ya paso?
Se compara, para T5m (continuacion) y su inversa, el placebo completo contra el placebo que solo sortea segundos POSTERIORES al disparo
(dentro de los 30 min siguientes), que no pueden caer adentro del movimiento que genero la señal."""
import numpy as np, pandas as pd
import escalas_lib as L
B = L.B
T = B.todas_las_sesiones(); U = L.cargar_umbrales(); rng = np.random.default_rng(5)
for v in ("T5m", "S15m"):
    R, det = L.juzgar_todo(T, U, variantes=[v]); d = det[v]; ses = np.array(d["ses"]); idx = np.array(d["idx"]); lado = np.array(d["lado"])
    Y = {s: L.barreras_sesion(s, T[s])[(8, 600)] for s in sorted(set(ses))}; F = {s: L.rasgos_min(T[s]) for s in Y}
    y = np.array(d["y"][(8, 600)]); ok = y != 0; ac = ((y[ok] * lado[ok]) > 0).mean()
    m_full, sd_full, _ = L.placebo(T, d, (8, 600))
    acs_a, acs_d = [], []
    for _ in range(200):
        ya = np.zeros(len(idx)); yd = np.zeros(len(idx))
        for j, (s, i) in enumerate(zip(ses, idx)):
            val = F[s]["valido"]; n = len(val)
            a = rng.integers(max(0, i - 1800), i); ya[j] = Y[s][a] if val[a] else 0            # 30 min ANTES del disparo
            b = rng.integers(i + 1, min(n, i + 1800)); yd[j] = Y[s][b] if val[b] else 0          # 30 min DESPUES
        for arr, dest in ((ya, acs_a), (yd, acs_d)):
            k = arr != 0; dest.append(((arr[k] * lado[k]) > 0).mean())
    print("%s (lado pre-registrado) 20 sesiones: señal %.1f %% | placebo misma media hora %.1f %% | sorteando solo ANTES del disparo %.1f %% | solo DESPUES %.1f %%" % (
        v, 100 * ac, 100 * m_full, 100 * np.mean(acs_a), 100 * np.mean(acs_d)))
