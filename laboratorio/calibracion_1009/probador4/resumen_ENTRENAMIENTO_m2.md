# Probador 4 — controles sin gamma (C19..C24), fase ENTRENAMIENTO (velas de 2 min, sensibilidad)

Arnés `evaluar.py` v1.0 (sha256 1cd6083989f4), juez copia a20a62688e88. Sesiones por candidata en `resultados/<ID>_ENTRENAMIENTO.json` → probador4.dias_usados.
n_azar = 500, n_boot = 2000.

### noche — M1 % sostenidos (TOLERANTE) contra placebos

| id | base (llegadas, cens.) | niveles | ses. c/lleg. | M1 % | corridos % | real−corr (IC95) | azar media [p5–p95] | real−azar (IC95) | p_azar | ses. ganadas |
|---|---|---|---|---|---|---|---|---|---|---|
| C19_RED25 | 2803 (2803, 0) | 403  | 33 | 50.5 | 50.5 | 0.0 (-2.6; 2.6) | 50.5 [48.6–52.2] | 0.0 (-1.7; 1.8) | 0.491 | 13/33 |
| C20_RED50 | 1476 (1476, 0) | 207  | 33 | 50.9 | 50.4 | 0.5 (-2.7; 3.8) | 50.8 [47.9–53.7] | 0.1 (-2.7; 2.6) | 0.489 | 13/33 |
| C21_RED100 | 738 (738, 0) | 103  | 33 | 50.9 | 50.7 | 0.3 (-4.6; 5.4) | 50.6 [46.0–55.1] | 0.3 (-3.9; 4.6) | 0.435 | 17/33 |
| C22_GRILLA_QQQ_C41 | 554 (554, 0) | 47 K | 12 | 52.0 | 50.2 | 1.7 (-1.5; 4.8) | 50.6 [46.0–55.0] | 1.4 (-0.7; 2.9) | 0.279 | 8/12 |
| C23_E_RANGO60 | 1319 (1319, 0) | 847  | 33 | 49.9 | 49.9 | 0.0 (-3.3; 3.5) | 49.5 [47.3–51.6] | 0.4 (-2.5; 3.2) | 0.375 | 19/33 |
| C24_MECHA_PREVIA | 2226 (2226, 0) | 828  | 33 | 49.1 | 50.6 | -1.5 (-3.3; 0.2) | 49.4 [47.6–51.1] | -0.3 (-1.8; 1.3) | 0.613 | 11/33 |

### noche — sensibilidad del juez, M2..M4, cobertura, toque y eco

| id | M1 estricto (corr) | M1 muy tol. (corr) | M2 rec/lleg (corr) | rec. medio / mediano | % exactos (corr) | % falsos entre sost. | % llega opuesta | precisión mediana abs / con signo | cob20 real / azar (lift azar; lift corr) | cob40 real / azar (lift azar; lift corr) | toque: rebote real / azar (p) | dist. mediana | beta pista |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| C19_RED25 | 31.7 (32.9) | 57.0 (56.8) | 35.8 (35.1) | 70.9 / 41.8 | 23.7 (24.9) | 53.6 | 78.0 | 2.50 / 2.50 | 14.4 / 9.7 (1.49; 1.38) | 13.5 / 8.9 (1.51; 1.31) | 62.8 / 62.6 (0.401) | 12.5 | 0.000 |
| C20_RED50 | 31.2 (32.4) | 57.9 (57.1) | 38.4 (34.7) | 75.5 / 44.0 | 23.6 (24.5) | 53.9 | 47.5 | 2.50 / 2.50 | 7.7 / 4.3 (1.78; 0.95) | 8.2 / 4.4 (1.86; 1.04) | 63.6 / 63.0 (0.395) | 25.0 | 0.000 |
| C21_RED100 | 30.6 (32.6) | 57.0 (57.2) | 38.0 (35.5) | 74.6 / 48.8 | 23.4 (24.8) | 54.5 | 24.2 | 2.75 / 2.75 | 3.8 / 2.0 (1.93; 0.91) | 4.6 / 2.1 (2.18; 1.07) | 64.4 / 62.4 (0.228) | 50.0 | 0.000 |
| C22_GRILLA_QQQ_C41 | 32.8 (32.4) | 57.8 (56.9) | 33.9 (35.4) | 65.3 / 37.5 | 24.0 (24.5) | 54.2 | 48.3 | 2.42 / 2.42 | 7.3 / 4.2 (1.76; 1.10) | 7.0 / 4.4 (1.59; 1.08) | 65.2 / 63.7 (0.277) | 20.7 | 0.001 |
| C23_E_RANGO60 | 33.1 (32.4) | 56.2 (56.6) | 33.9 (34.5) | 68.0 / 38.8 | 24.1 (25.0) | 52.4 | 39.1 | 2.25 / 2.25 | 6.1 / 6.0 (1.02; 1.20) | 5.4 / 5.0 (1.07; 1.41) | 61.5 / 61.5 (0.507) | 27.8 | 0.003 |
| C24_MECHA_PREVIA | 31.3 (32.4) | 56.6 (57.1) | 33.8 (36.1) | 68.8 / 39.2 | 23.5 (24.6) | 53.1 | 81.9 | 2.25 / 2.25 | 11.2 / 9.6 (1.17; 1.38) | 6.8 / 6.8 (1.00; 0.96) | 61.0 / 60.8 (0.401) | 12.0 | 0.006 |

### dia — M1 % sostenidos (TOLERANTE) contra placebos

| id | base (llegadas, cens.) | niveles | ses. c/lleg. | M1 % | corridos % | real−corr (IC95) | azar media [p5–p95] | real−azar (IC95) | p_azar | ses. ganadas |
|---|---|---|---|---|---|---|---|---|---|---|
| C19_RED25 | 1562 (1563, 1) | 336  | 33 | 52.4 | 53.5 | -1.1 (-2.9; 0.9) | 53.4 [51.2–55.5] | -0.9 (-2.6; 0.8) | 0.750 | 13/33 |
| C20_RED50 | 994 (995, 1) | 179  | 33 | 52.2 | 53.4 | -1.2 (-5.2; 2.9) | 53.4 [49.7–56.5] | -1.2 (-3.8; 1.3) | 0.756 | 13/33 |
| C21_RED100 | 531 (532, 1) | 92  | 33 | 53.9 | 54.0 | -0.2 (-4.3; 4.1) | 54.3 [50.0–58.7] | -0.4 (-3.0; 2.3) | 0.577 | 13/33 |
| C22_GRILLA_QQQ_C41 | 412 (412, 0) | 42 K | 13 | 50.5 | 49.8 | 0.7 (-4.5; 5.2) | 50.6 [46.3–55.0] | -0.2 (-4.2; 3.9) | 0.499 | 6/12 |
| C23_E_RANGO60 | 543 (544, 1) | 411  | 33 | 50.1 | 51.3 | -1.3 (-5.9; 3.6) | 52.6 [49.5–55.6] | -2.6 (-6.3; 1.5) | 0.912 | 15/33 |
| C24_MECHA_PREVIA | 1192 (1194, 2) | 562  | 33 | 52.6 | 53.1 | -0.5 (-2.6; 1.5) | 52.9 [50.7–55.2] | -0.3 (-2.3; 1.5) | 0.589 | 17/33 |

### dia — sensibilidad del juez, M2..M4, cobertura, toque y eco

| id | M1 estricto (corr) | M1 muy tol. (corr) | M2 rec/lleg (corr) | rec. medio / mediano | % exactos (corr) | % falsos entre sost. | % llega opuesta | precisión mediana abs / con signo | cob20 real / azar (lift azar; lift corr) | cob40 real / azar (lift azar; lift corr) | toque: rebote real / azar (p) | dist. mediana | beta pista |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| C19_RED25 | 28.5 (30.3) | 57.9 (58.4) | 35.5 (39.1) | 67.8 / 46.0 | 21.2 (22.5) | 59.8 | 80.3 | 3.50 / 3.50 | 12.1 / 9.2 (1.31; 1.28) | 10.8 / 8.4 (1.29; 1.24) | 60.9 / 60.7 (0.433) | 12.5 | 0.000 |
| C20_RED50 | 28.5 (29.7) | 58.0 (58.7) | 35.6 (39.2) | 68.1 / 45.8 | 21.0 (22.5) | 59.7 | 47.0 | 3.50 / 3.50 | 7.6 / 4.5 (1.71; 0.98) | 8.3 / 4.5 (1.82; 1.12) | 60.4 / 61.2 (0.681) | 25.0 | 0.000 |
| C21_RED100 | 29.6 (29.5) | 59.3 (59.0) | 38.2 (41.1) | 71.0 / 47.9 | 21.3 (22.1) | 60.5 | 19.2 | 3.50 / 3.50 | 4.0 / 2.3 (1.74; 0.95) | 4.5 / 2.1 (2.09; 1.06) | 62.4 / 62.3 (0.489) | 50.0 | 0.000 |
| C22_GRILLA_QQQ_C41 | 28.8 (28.1) | 55.9 (56.7) | 40.6 (38.0) | 80.5 / 44.2 | 20.9 (19.9) | 58.7 | 58.2 | 2.90 / 2.90 | 7.2 / 4.3 (1.67; 1.18) | 6.7 / 4.1 (1.62; 1.22) | 60.2 / 60.9 (0.631) | 20.7 | -0.001 |
| C23_E_RANGO60 | 28.3 (29.1) | 57.0 (56.9) | 38.4 (38.5) | 76.6 / 50.1 | 18.8 (22.6) | 63.6 | 29.0 | 3.38 / 3.38 | 3.1 / 5.0 (0.63; 0.85) | 2.7 / 4.0 (0.68; 0.88) | 59.3 / 60.4 (0.778) | 43.2 | 0.001 |
| C24_MECHA_PREVIA | 30.3 (29.5) | 58.8 (58.6) | 34.5 (37.8) | 65.6 / 43.0 | 22.2 (22.5) | 58.1 | 87.8 | 3.00 / 3.00 | 9.6 / 9.2 (1.04; 1.08) | 5.0 / 7.0 (0.72; 0.64) | 60.2 / 60.4 (0.535) | 7.5 | 0.005 |

### Estratos (descriptivos, no deciden): % sostenidos por franja NY y primera visita contra siguientes

| id | ventana | franjas (base: %) | primera visita (base: %) | siguientes (base: %) |
|---|---|---|---|---|
| C19_RED25 | noche | noche_ny 2803: 50.5 | 403: 45.2 | 2400: 51.4 |
| C19_RED25 | dia | apertura_0930_1030 317: 48.9; media_1030_1400 844: 53.1; tarde_1400_1530 280: 49.6; cierre_1530_1600 121: 63.6 | 336: 48.8 | 1226: 53.4 |
| C20_RED50 | noche | noche_ny 1476: 50.9 | 207: 45.9 | 1269: 51.7 |
| C20_RED50 | dia | apertura_0930_1030 250: 51.2; media_1030_1400 497: 51.9; tarde_1400_1530 167: 50.3; cierre_1530_1600 80: 61.2 | 179: 49.2 | 815: 52.9 |
| C21_RED100 | noche | noche_ny 738: 50.9 | 103: 44.7 | 635: 52.0 |
| C21_RED100 | dia | apertura_0930_1030 145: 50.3; media_1030_1400 268: 55.6; tarde_1400_1530 78: 50.0; cierre_1530_1600 40: 62.5 | 92: 53.3 | 439: 54.0 |
| C22_GRILLA_QQQ_C41 | noche | noche_ny 554: 52.0 | 90: 58.9 | 464: 50.6 |
| C22_GRILLA_QQQ_C41 | dia | tarde_1400_1530 79: 41.8; cierre_1530_1600 32: 40.6; apertura_0930_1030 87: 50.6; media_1030_1400 214: 55.1 | 114: 46.5 | 298: 52.0 |
| C23_E_RANGO60 | noche | noche_ny 1319: 49.9 | 835: 50.8 | 484: 48.3 |
| C23_E_RANGO60 | dia | apertura_0930_1030 115: 50.4; media_1030_1400 260: 48.8; tarde_1400_1530 111: 55.0; cierre_1530_1600 57: 45.6 | 413: 51.6 | 130: 45.4 |
| C24_MECHA_PREVIA | noche | noche_ny 2226: 49.1 | 821: 49.8 | 1405: 48.7 |
| C24_MECHA_PREVIA | dia | apertura_0930_1030 204: 56.4; media_1030_1400 664: 53.5; tarde_1400_1530 226: 47.8; cierre_1530_1600 98: 50.0 | 544: 55.3 | 648: 50.3 |

## Regla de selección (sec. 8) aplicada a los 6 controles

**noche**: finalistas = ninguna (los controles C19–C24 no pueden ser finalistas por regla). Elegibles si no fueran controles: ninguno.

| id | IC90 inf (real−corr) | p_azar | mitad 1 (≤ 09-22): base, real−corr | mitad 2 (> 09-22): base, real−corr | cambia de signo |
|---|---|---|---|---|---|
| C19_RED25 | -2.18 | 0.491 | 2262, -0.3 | 541, 1.2 | SÍ |
| C20_RED50 | -2.17 | 0.489 | 1179, -0.5 | 297, 4.3 | SÍ |
| C21_RED100 | -3.86 | 0.435 | 576, 0.1 | 162, 0.7 | no |
| C22_GRILLA_QQQ_C41 | -0.94 | 0.279 | 285, 0.9 | 269, 2.6 | no |
| C23_E_RANGO60 | -2.77 | 0.375 | 1065, 0.6 | 254, -2.3 | SÍ |
| C24_MECHA_PREVIA | -3.02 | 0.613 | 1795, -2.0 | 431, 0.5 | SÍ |

LOO por sesión del procedimiento “elegir el mejor control por M1 − corridos” (noche): elegido por sesión C22_GRILLA_QQQ_C41 ×33; ventaja fuera de muestra 1.75 pp.

**dia**: finalistas = ninguna (los controles C19–C24 no pueden ser finalistas por regla). Elegibles si no fueran controles: ninguno.

| id | IC90 inf (real−corr) | p_azar | mitad 1 (≤ 09-22): base, real−corr | mitad 2 (> 09-22): base, real−corr | cambia de signo |
|---|---|---|---|---|---|
| C19_RED25 | -2.62 | 0.750 | 1207, -1.6 | 355, 0.8 | SÍ |
| C20_RED50 | -4.43 | 0.756 | 744, -1.7 | 250, 0.6 | SÍ |
| C21_RED100 | -3.61 | 0.577 | 405, -1.1 | 126, 2.7 | SÍ |
| C22_GRILLA_QQQ_C41 | -3.57 | 0.499 | 222, 4.2 | 190, -3.4 | SÍ |
| C23_E_RANGO60 | -5.16 | 0.912 | 452, -1.8 | 91, 0.6 | SÍ |
| C24_MECHA_PREVIA | -2.27 | 0.589 | 925, -1.0 | 267, 1.5 | SÍ |

LOO por sesión del procedimiento “elegir el mejor control por M1 − corridos” (dia): elegido por sesión C22_GRILLA_QQQ_C41 ×32, C24_MECHA_PREVIA ×1; ventaja fuera de muestra -0.48 pp.


Chequeo de mirar adelante (`chequeo_futuro`, % de rayas a ≤ 0,25 de la mecha de su propia vela / anterior / siguiente): C19_RED25 2.92/2.93/2.84; C20_RED50 1.45/1.45/1.44; C21_RED100 0.71/0.71/0.71; C22_GRILLA_QQQ_C41 1.27/1.27/1.28; C23_E_RANGO60 1.00/7.27/1.01; C24_MECHA_PREVIA 3.62/3.68/3.26
