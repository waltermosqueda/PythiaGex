# Probador 4 — controles sin gamma (C19..C24), fase ENTRENAMIENTO

Arnés `evaluar.py` v1.0 (sha256 1cd6083989f4), juez copia a20a62688e88. Sesiones por candidata en `resultados/<ID>_ENTRENAMIENTO.json` → probador4.dias_usados.
n_azar = 500, n_boot = 2000.

### noche — M1 % sostenidos (TOLERANTE) contra placebos

| id | base (llegadas, cens.) | niveles | ses. c/lleg. | M1 % | corridos % | real−corr (IC95) | azar media [p5–p95] | real−azar (IC95) | p_azar | ses. ganadas |
|---|---|---|---|---|---|---|---|---|---|---|
| C19_RED25 | 3642 (3642, 0) | 409  | 33 | 46.6 | 46.1 | 0.6 (-1.3; 2.5) | 46.2 [44.7–47.8] | 0.4 (-0.8; 1.6) | 0.351 | 18/33 |
| C20_RED50 | 1871 (1871, 0) | 209  | 33 | 47.4 | 46.6 | 0.8 (-1.7; 3.3) | 46.5 [43.9–49.0] | 0.9 (-1.1; 3.0) | 0.261 | 15/33 |
| C21_RED100 | 926 (926, 0) | 104  | 33 | 48.2 | 46.9 | 1.3 (-2.4; 5.3) | 46.3 [42.4–50.3] | 1.9 (-1.3; 4.8) | 0.222 | 17/33 |
| C22_GRILLA_QQQ_C41 | 730 (730, 0) | 47 K | 12 | 46.4 | 46.2 | 0.2 (-3.2; 3.9) | 45.8 [41.7–49.6] | 0.6 (-2.0; 3.3) | 0.397 | 5/12 |
| C23_E_RANGO60 | 1684 (1684, 0) | 989  | 33 | 45.3 | 45.0 | 0.3 (-2.2; 3.0) | 45.0 [43.2–46.9] | 0.3 (-1.7; 2.4) | 0.391 | 16/33 |
| C24_MECHA_PREVIA | 3192 (3192, 0) | 944  | 33 | 44.6 | 45.9 | -1.3 (-3.0; 0.6) | 45.1 [43.8–46.4] | -0.5 (-2.2; 1.2) | 0.727 | 14/33 |

### noche — sensibilidad del juez, M2..M4, cobertura, toque y eco

| id | M1 estricto (corr) | M1 muy tol. (corr) | M2 rec/lleg (corr) | rec. medio / mediano | % exactos (corr) | % falsos entre sost. | % llega opuesta | precisión mediana abs / con signo | cob20 real / azar (lift azar; lift corr) | cob40 real / azar (lift azar; lift corr) | toque: rebote real / azar (p) | dist. mediana | beta pista |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| C19_RED25 | 31.7 (32.7) | 53.3 (53.0) | 31.4 (30.6) | 67.3 / 38.0 | 24.2 (24.8) | 49.1 | 75.1 | 2.00 / 2.00 | 15.3 / 9.1 (1.67; 1.44) | 14.8 / 8.6 (1.72; 1.39) | 61.1 / 60.3 (0.176) | 12.5 | 0.000 |
| C20_RED50 | 31.1 (32.9) | 53.5 (53.5) | 33.9 (30.6) | 71.5 / 38.8 | 24.1 (24.9) | 50.3 | 43.5 | 2.00 / 2.00 | 7.9 / 4.4 (1.81; 0.97) | 8.5 / 4.3 (1.95; 1.05) | 61.0 / 60.6 (0.399) | 25.0 | 0.000 |
| C21_RED100 | 30.0 (33.0) | 54.1 (53.5) | 34.3 (31.2) | 71.3 / 42.8 | 23.5 (25.0) | 52.0 | 23.3 | 2.25 / 2.25 | 3.8 / 1.9 (1.98; 0.92) | 4.4 / 1.9 (2.33; 1.03) | 60.4 / 60.1 (0.451) | 50.0 | 0.000 |
| C22_GRILLA_QQQ_C41 | 31.7 (31.6) | 55.3 (52.7) | 28.3 (31.2) | 60.9 / 35.4 | 24.0 (24.2) | 49.9 | 45.4 | 1.93 / 1.80 | 7.5 / 3.9 (1.92; 1.10) | 7.6 / 4.2 (1.81; 1.12) | 60.0 / 60.8 (0.653) | 20.7 | 0.000 |
| C23_E_RANGO60 | 33.5 (31.9) | 52.0 (52.3) | 30.9 (29.0) | 68.1 / 38.2 | 24.9 (24.8) | 46.9 | 38.3 | 2.00 / 1.75 | 6.0 / 5.8 (1.04; 1.26) | 5.5 / 5.0 (1.08; 1.75) | 59.3 / 59.2 (0.433) | 27.5 | 0.005 |
| C24_MECHA_PREVIA | 31.1 (32.3) | 52.5 (53.3) | 29.9 (30.6) | 67.0 / 38.8 | 23.7 (24.8) | 48.6 | 82.5 | 2.00 / 1.75 | 12.4 / 10.3 (1.20; 1.58) | 8.0 / 7.5 (1.08; 1.19) | 58.1 / 59.4 (0.970) | 12.0 | 0.006 |

### dia — M1 % sostenidos (TOLERANTE) contra placebos

| id | base (llegadas, cens.) | niveles | ses. c/lleg. | M1 % | corridos % | real−corr (IC95) | azar media [p5–p95] | real−azar (IC95) | p_azar | ses. ganadas |
|---|---|---|---|---|---|---|---|---|---|---|
| C19_RED25 | 2428 (2429, 1) | 361  | 33 | 49.8 | 51.7 | -1.8 (-3.8; 0.2) | 50.9 [49.2–52.7] | -1.1 (-3.1; 0.7) | 0.840 | 12/33 |
| C20_RED50 | 1371 (1372, 1) | 183  | 33 | 49.9 | 52.1 | -2.2 (-5.5; 1.2) | 51.8 [49.0–54.5] | -1.9 (-3.8; 0.1) | 0.870 | 12/33 |
| C21_RED100 | 716 (717, 1) | 93  | 33 | 52.8 | 53.0 | -0.2 (-4.2; 4.2) | 51.9 [48.2–56.0] | 0.9 (-1.9; 4.2) | 0.345 | 15/33 |
| C22_GRILLA_QQQ_C41 | 558 (558, 0) | 43 K | 13 | 50.7 | 49.1 | 1.6 (-3.3; 5.5) | 51.3 [46.9–55.4] | -0.5 (-3.6; 2.1) | 0.595 | 7/12 |
| C23_E_RANGO60 | 724 (725, 1) | 524  | 33 | 49.2 | 48.7 | 0.4 (-3.9; 5.0) | 50.3 [47.5–53.1] | -1.1 (-4.9; 2.9) | 0.750 | 17/33 |
| C24_MECHA_PREVIA | 2138 (2140, 2) | 733  | 33 | 50.1 | 50.6 | -0.5 (-2.3; 1.4) | 49.9 [48.3–51.6] | 0.2 (-1.2; 1.7) | 0.425 | 14/33 |

### dia — sensibilidad del juez, M2..M4, cobertura, toque y eco

| id | M1 estricto (corr) | M1 muy tol. (corr) | M2 rec/lleg (corr) | rec. medio / mediano | % exactos (corr) | % falsos entre sost. | % llega opuesta | precisión mediana abs / con signo | cob20 real / azar (lift azar; lift corr) | cob40 real / azar (lift azar; lift corr) | toque: rebote real / azar (p) | dist. mediana | beta pista |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| C19_RED25 | 30.1 (31.8) | 55.6 (57.4) | 32.6 (35.0) | 65.4 / 43.1 | 22.2 (23.8) | 56.0 | 77.4 | 2.75 / 2.75 | 13.8 / 8.9 (1.55; 1.38) | 13.5 / 8.4 (1.61; 1.43) | 61.6 / 61.9 (0.619) | 12.5 | 0.000 |
| C20_RED50 | 28.9 (31.2) | 55.6 (57.6) | 33.9 (35.0) | 67.9 / 45.1 | 21.0 (23.8) | 58.5 | 45.6 | 3.00 / 3.00 | 7.7 / 4.4 (1.74; 0.94) | 8.3 / 4.5 (1.87; 1.09) | 60.8 / 61.9 (0.752) | 25.0 | 0.000 |
| C21_RED100 | 29.5 (31.4) | 58.1 (58.5) | 36.9 (36.5) | 69.9 / 47.1 | 21.6 (24.1) | 59.3 | 17.7 | 3.25 / 3.25 | 3.8 / 2.3 (1.67; 0.87) | 4.2 / 2.1 (1.95; 1.00) | 61.9 / 63.0 (0.697) | 50.0 | 0.000 |
| C22_GRILLA_QQQ_C41 | 30.6 (29.1) | 56.8 (55.3) | 39.7 (34.4) | 78.3 / 41.0 | 22.6 (20.8) | 55.5 | 54.4 | 2.63 / 2.63 | 8.3 / 4.2 (1.95; 1.34) | 7.4 / 4.0 (1.86; 1.35) | 62.3 / 62.1 (0.487) | 20.7 | 0.000 |
| C23_E_RANGO60 | 30.5 (29.6) | 55.2 (55.2) | 35.1 (33.5) | 71.3 / 41.6 | 20.0 (22.7) | 61.0 | 25.0 | 3.00 / 3.00 | 2.7 / 4.8 (0.57; 0.81) | 2.5 / 4.1 (0.60; 0.85) | 59.2 / 60.7 (0.882) | 43.2 | 0.002 |
| C24_MECHA_PREVIA | 31.2 (30.5) | 55.9 (57.1) | 31.6 (33.8) | 63.1 / 40.8 | 23.4 (23.3) | 54.2 | 88.8 | 2.50 / 2.50 | 11.5 / 10.2 (1.13; 1.29) | 6.7 / 7.8 (0.86; 0.84) | 60.4 / 60.5 (0.579) | 7.5 | 0.006 |

### Estratos (descriptivos, no deciden): % sostenidos por franja NY y primera visita contra siguientes

| id | ventana | franjas (base: %) | primera visita (base: %) | siguientes (base: %) |
|---|---|---|---|---|
| C19_RED25 | noche | noche_ny 3642: 46.6 | 409: 44.7 | 3233: 46.9 |
| C19_RED25 | dia | apertura_0930_1030 609: 51.9; media_1030_1400 1253: 49.1; tarde_1400_1530 390: 47.4; cierre_1530_1600 176: 53.4 | 361: 51.0 | 2067: 49.6 |
| C20_RED50 | noche | noche_ny 1871: 47.4 | 209: 45.5 | 1662: 47.7 |
| C20_RED50 | dia | apertura_0930_1030 387: 52.5; media_1030_1400 664: 49.1; tarde_1400_1530 219: 46.1; cierre_1530_1600 101: 53.5 | 183: 47.0 | 1188: 50.3 |
| C21_RED100 | noche | noche_ny 926: 48.2 | 104: 46.2 | 822: 48.4 |
| C21_RED100 | dia | apertura_0930_1030 204: 52.9; media_1030_1400 363: 53.4; tarde_1400_1530 99: 49.5; cierre_1530_1600 50: 54.0 | 93: 50.5 | 623: 53.1 |
| C22_GRILLA_QQQ_C41 | noche | noche_ny 730: 46.4 | 93: 53.8 | 637: 45.4 |
| C22_GRILLA_QQQ_C41 | dia | tarde_1400_1530 99: 44.4; cierre_1530_1600 42: 33.3; apertura_0930_1030 132: 53.0; media_1030_1400 285: 54.4 | 120: 42.5 | 438: 53.0 |
| C23_E_RANGO60 | noche | noche_ny 1684: 45.3 | 974: 45.7 | 710: 44.8 |
| C23_E_RANGO60 | dia | apertura_0930_1030 163: 54.0; media_1030_1400 345: 47.2; tarde_1400_1530 141: 50.4; cierre_1530_1600 75: 45.3 | 525: 48.6 | 199: 50.8 |
| C24_MECHA_PREVIA | noche | noche_ny 3192: 44.6 | 935: 45.2 | 2257: 44.4 |
| C24_MECHA_PREVIA | dia | apertura_0930_1030 456: 55.3; media_1030_1400 1167: 49.5; tarde_1400_1530 371: 45.8; cierre_1530_1600 144: 50.0 | 714: 53.8 | 1424: 48.3 |

## Regla de selección (sec. 8) aplicada a los 6 controles

**noche**: finalistas = ninguna (los controles C19–C24 no pueden ser finalistas por regla). Elegibles si no fueran controles: ninguno.

| id | IC90 inf (real−corr) | p_azar | mitad 1 (≤ 09-22): base, real−corr | mitad 2 (> 09-22): base, real−corr | cambia de signo |
|---|---|---|---|---|---|
| C19_RED25 | -1.06 | 0.351 | 2925, 0.4 | 717, 1.2 | no |
| C20_RED50 | -1.38 | 0.261 | 1488, 0.2 | 383, 2.8 | no |
| C21_RED100 | -1.95 | 0.222 | 711, 1.9 | 215, -1.1 | SÍ |
| C22_GRILLA_QQQ_C41 | -2.68 | 0.397 | 371, -0.8 | 359, 1.3 | SÍ |
| C23_E_RANGO60 | -1.81 | 0.391 | 1368, 0.8 | 316, -1.8 | SÍ |
| C24_MECHA_PREVIA | -2.72 | 0.727 | 2552, -1.6 | 640, -0.2 | no |

LOO por sesión del procedimiento “elegir el mejor control por M1 − corridos” (noche): elegido por sesión C21_RED100 ×31, C22_GRILLA_QQQ_C41 ×2; ventaja fuera de muestra -0.40 pp.

**dia**: finalistas = ninguna (los controles C19–C24 no pueden ser finalistas por regla). Elegibles si no fueran controles: ninguno.

| id | IC90 inf (real−corr) | p_azar | mitad 1 (≤ 09-22): base, real−corr | mitad 2 (> 09-22): base, real−corr | cambia de signo |
|---|---|---|---|---|---|
| C19_RED25 | -3.52 | 0.840 | 1885, -2.6 | 543, 0.7 | SÍ |
| C20_RED50 | -4.98 | 0.870 | 1058, -3.1 | 313, 0.9 | SÍ |
| C21_RED100 | -3.63 | 0.345 | 558, -1.5 | 158, 4.4 | SÍ |
| C22_GRILLA_QQQ_C41 | -2.45 | 0.595 | 288, 4.8 | 270, -1.9 | SÍ |
| C23_E_RANGO60 | -3.21 | 0.750 | 609, -0.2 | 115, 2.5 | SÍ |
| C24_MECHA_PREVIA | -2.02 | 0.425 | 1659, -0.6 | 479, -0.1 | no |

LOO por sesión del procedimiento “elegir el mejor control por M1 − corridos” (dia): elegido por sesión C22_GRILLA_QQQ_C41 ×33; ventaja fuera de muestra 1.61 pp.


Chequeo de mirar adelante (`chequeo_futuro`, % de rayas a ≤ 0,25 de la mecha de su propia vela / anterior / siguiente): C19_RED25 2.92/2.93/2.84; C20_RED50 1.45/1.45/1.44; C21_RED100 0.71/0.71/0.71; C22_GRILLA_QQQ_C41 1.27/1.27/1.28; C23_E_RANGO60 1.00/7.27/1.01; C24_MECHA_PREVIA 3.62/3.68/3.26
