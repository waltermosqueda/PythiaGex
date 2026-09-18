# Familia "tablero" — el resumen del tablero A (velocimetros) juzgado ANTES de dibujarlo (18-09-2026)

Pregunta: el estado COMPRAN / PAREJO / VENDEN que va a mostrar Flujo Claro, describe la vela actual (lo que dice ser) y anticipa algo (lo que NO dice ser)?
Datos: tablas de 1 s de MNQ de base.py (cinta del propio ATAS del operador), 20 sesiones del 2026-08-20 al 2026-09-17. Codigo: tablero_01_placebo.py (el criterio esta en su docstring y se escribio antes de correr).
Reglas del puntaje: FLUJO 1m (x2) y FLUJO 5m = delta/vol con zona muerta |r| < 0.10; GRANDES 5m = delta de 50+ en 300 s contra 0.3 x p95 de los 30 min previos; CVD 15m = delta de 900 s contra 1 desvio de los 30 min previos; PRECIO 15m = retorno de 900 s contra la mediana de |retorno| del dia. S de -6 a +6: |S| <= 1 PAREJO, 2-3 debil, >= 4 fuerte.
Muestras: GRILLA = una fila cada 15 s (lo que se muestra); CAMBIOS = el segundo en que el estado pasa a otro distinto de PAREJO (lo que se graba en tablero-<inst>-<dia>.jsonl). Solo rueda 13:32-20:00 UTC, sin huecos, sin los ultimos 900 s.
Placebos con los MISMOS lados: AZAR = segundo elegible al azar de la misma sesion (200 sorteos); CORRIDO = el resultado 30 min despues; OTRO DIA = mismo segundo del dia en otra sesion del mismo tramo (200 sorteos). IC 90 % remuestreando dias (2000 veces). Empate de +-8 con costo 0.96: 56.0 %.


## EXPLORAR: 12 sesiones (2026-08-20 a 2026-09-04)
   reparto del tiempo (grilla, n 18516): VENDEN FUERTE 8.9 % · venden 21.6 % · PAREJO 40.0 % · compran 20.5 % · COMPRAN FUERTE 8.9 %
   lecturas que votan (grilla, % del tiempo con voto distinto de 0): FLUJO 1m 42 · FLUJO 5m 12 · GRANDES 50 · CVD 15m 50 · PRECIO 15m 63 | a favor cuando hay lado: 1/5 12 % · 2/5 38 % · 3/5 32 % · 4/5 14 % · 5/5 5 %
   por dia (grilla, % del tiempo VF/v/P/c/CF | cambios de estado VF/v/c/CF): 08-20 8/22/46/18/6 (109/341/284/91) | 08-21 10/23/40/17/10 (141/346/289/83) | 08-24 8/23/38/22/9 (108/332/295/98) | 08-25 10/25/40/18/7 (121/411/270/83) | 08-26 8/18/41/24/10 (95/303/397/130) | 08-27 9/21/40/21/9 (86/277/311/116) | 08-28 8/25/42/19/6 (76/281/226/69) | 08-31 6/21/39/25/9 (87/293/431/127) | 09-01 10/21/40/21/8 (107/314/300/84) | 09-02 9/18/41/20/11 (91/307/354/129) | 09-03 7/20/38/24/12 (105/290/318/128) | 09-04 13/22/36/18/10 (140/375/306/108)
   cambios de estado por rueda: mediana 850, min 652, max 938 (un cambio cada 27 s de rueda elegible en promedio)

### EXPLORAR — GRILLA (n 18516)
   COMPRAN FUERTE   n  1655 (12 dias) | describe: ultimo minuto del mismo lado 94.0 % (+10.06 pts) | +-8/600: 50.3 % [IC 46.0;54.0] dias>50 % 8 de 12, sin resolver  0 %
                      placebos: AZAR 51.7 +- 1.2 (z -1.2, lo iguala 175/200) | CORRIDO 30 min 49.5 [46.2;52.9] n 1464 | OTRO DIA 51.2 +- 1.2 (z -0.7, lo iguala 152/200)
                      retorno firmado 60 s -0.35 [-0.70;-0.03] (azar +0.11, corrido -0.34, otro dia +0.10) | 300 s -0.01 [-2.03;+1.60] (azar +0.69, corrido -1.32, otro dia +0.21)
   compran          n  3794 (12 dias) | describe: ultimo minuto del mismo lado 70.5 % (+3.76 pts) | +-8/600: 51.9 % [IC 49.3;54.7] dias>50 % 6 de 12, sin resolver  0 %
                      placebos: AZAR 51.7 +- 0.8 (z +0.2, lo iguala 86/200) | CORRIDO 30 min 50.3 [48.8;52.0] n 3449 | OTRO DIA 51.1 +- 0.8 (z +1.0, lo iguala 31/200)
                      retorno firmado 60 s -0.07 [-0.45;+0.33] (azar +0.09, corrido -0.08, otro dia +0.07) | 300 s +0.40 [-1.13;+1.90] (azar +0.57, corrido -0.05, otro dia +0.25)
   venden           n  4008 (12 dias) | describe: ultimo minuto del mismo lado 67.5 % (+3.23 pts) | +-8/600: 48.7 % [IC 46.7;50.9] dias>50 % 4 de 12, sin resolver  0 %
                      placebos: AZAR 48.5 +- 0.9 (z +0.2, lo iguala 86/200) | CORRIDO 30 min 47.1 [44.6;49.4] n 3710 | OTRO DIA 48.4 +- 0.8 (z +0.4, lo iguala 73/200)
                      retorno firmado 60 s -0.24 [-0.59;+0.07] (azar -0.06, corrido -0.35, otro dia -0.06) | 300 s -0.85 [-2.42;+0.60] (azar -0.37, corrido -1.17, otro dia -0.69)
   VENDEN FUERTE    n  1656 (12 dias) | describe: ultimo minuto del mismo lado 93.4 % (+10.44 pts) | +-8/600: 50.4 % [IC 47.3;53.3] dias>50 % 7 de 12, sin resolver  0 %
                      placebos: AZAR 48.5 +- 1.3 (z +1.5, lo iguala 13/200) | CORRIDO 30 min 45.0 [42.3;47.9] n 1572 | OTRO DIA 47.0 +- 1.2 (z +2.9, lo iguala 1/200)
                      retorno firmado 60 s -0.30 [-0.84;+0.24] (azar -0.08, corrido -0.49, otro dia -0.13) | 300 s +0.11 [-1.29;+1.68] (azar -0.34, corrido -0.47, otro dia -0.73)
   TODOS con lado   n 11113 (12 dias) | describe: ultimo minuto del mismo lado 76.3 % (+5.51 pts) | +-8/600: 50.3 % [IC 48.4;52.0] dias>50 % 6 de 12, sin resolver  0 %
                      placebos: AZAR 50.1 +- 0.5 (z +0.3, lo iguala 79/200) | CORRIDO 30 min 48.2 [46.6;49.9] n 10195 | OTRO DIA 49.5 +- 0.5 (z +1.7, lo iguala 7/200)
                      retorno firmado 60 s -0.21 [-0.45;+0.01] (azar +0.03, corrido -0.28, otro dia -0.01) | 300 s -0.15 [-1.36;+1.00] (azar +0.11, corrido -0.70, otro dia -0.26)

### EXPLORAR — CAMBIOS (n 10163)
   COMPRAN FUERTE   n  1246 (12 dias) | describe: ultimo minuto del mismo lado 92.4 % (+8.97 pts) | +-8/600: 49.1 % [IC 46.7;51.3] dias>50 % 7 de 12, sin resolver  0 %
                      placebos: AZAR 51.8 +- 1.5 (z -1.8, lo iguala 193/200) | CORRIDO 30 min 49.9 [47.4;52.5] n 1129 | OTRO DIA 51.6 +- 1.4 (z -1.8, lo iguala 194/200)
                      retorno firmado 60 s -0.55 [-0.93;-0.11] (azar +0.11, corrido -0.02, otro dia -0.01) | 300 s +0.08 [-2.01;+2.16] (azar +0.65, corrido -1.02, otro dia +0.11)
   compran          n  3781 (12 dias) | describe: ultimo minuto del mismo lado 74.4 % (+4.12 pts) | +-8/600: 50.9 % [IC 48.3;53.4] dias>50 % 7 de 12, sin resolver  0 %
                      placebos: AZAR 51.7 +- 0.9 (z -0.9, lo iguala 168/200) | CORRIDO 30 min 51.2 [49.5;52.7] n 3408 | OTRO DIA 51.5 +- 0.8 (z -0.7, lo iguala 151/200)
                      retorno firmado 60 s -0.00 [-0.33;+0.36] (azar +0.10, corrido -0.16, otro dia +0.09) | 300 s +0.11 [-0.95;+1.28] (azar +0.55, corrido -0.41, otro dia +0.25)
   venden           n  3870 (12 dias) | describe: ultimo minuto del mismo lado 73.4 % (+4.09 pts) | +-8/600: 48.8 % [IC 46.3;51.3] dias>50 % 4 de 12, sin resolver  0 %
                      placebos: AZAR 48.5 +- 0.8 (z +0.3, lo iguala 84/200) | CORRIDO 30 min 46.7 [44.5;49.3] n 3541 | OTRO DIA 47.9 +- 0.8 (z +1.1, lo iguala 27/200)
                      retorno firmado 60 s -0.17 [-0.38;+0.03] (azar -0.05, corrido -0.47, otro dia -0.11) | 300 s -0.71 [-1.31;-0.05] (azar -0.29, corrido -1.19, otro dia -0.56)
   VENDEN FUERTE    n  1266 (12 dias) | describe: ultimo minuto del mismo lado 92.8 % (+8.96 pts) | +-8/600: 49.1 % [IC 45.8;52.3] dias>50 % 7 de 12, sin resolver  0 %
                      placebos: AZAR 48.4 +- 1.5 (z +0.5, lo iguala 63/200) | CORRIDO 30 min 40.9 [38.6;43.3] n 1186 | OTRO DIA 47.4 +- 1.4 (z +1.3, lo iguala 20/200)
                      retorno firmado 60 s -0.55 [-1.08;-0.03] (azar -0.09, corrido -1.20, otro dia -0.05) | 300 s -1.11 [-2.48;+0.35] (azar -0.41, corrido -1.89, otro dia -0.79)
   TODOS con lado   n 10163 (12 dias) | describe: ultimo minuto del mismo lado 78.6 % (+5.31 pts) | +-8/600: 49.7 % [IC 47.9;51.3] dias>50 % 5 de 12, sin resolver  0 %
                      placebos: AZAR 50.2 +- 0.5 (z -1.1, lo iguala 174/200) | CORRIDO 30 min 48.0 [46.5;49.5] n 9264 | OTRO DIA 49.6 +- 0.5 (z +0.2, lo iguala 86/200)
                      retorno firmado 60 s -0.20 [-0.41;+0.03] (azar +0.02, corrido -0.39, otro dia -0.02) | 300 s -0.36 [-1.23;+0.47] (azar +0.10, corrido -0.97, otro dia -0.20)

   VELOCIDAD (rango crudo de los ultimos 5 min, grilla): DORMIDO (< 16): 15 % del tiempo, mediana hasta +-8 97 s, sin resolver a 180 s 24 % (techo_ml: 97-125 s / 24-36 %) | tranquilo (16-24): 26 % del tiempo, mediana hasta +-8 75 s, sin resolver a 180 s 17 % (techo_ml: 75-86 s / 17-26 %) | normal (24-32): 21 % del tiempo, mediana hasta +-8 52 s, sin resolver a 180 s 11 % (techo_ml: 52 s / 9-11 %) | movido (32-48): 21 % del tiempo, mediana hasta +-8 31 s, sin resolver a 180 s 3 % (techo_ml: 29-31 s / 3 %) | DESATADO (>= 48): 16 % del tiempo, mediana hasta +-8 13 s, sin resolver a 180 s 1 % (techo_ml: 13-14 s / 1 %)

## CONFIRMAR: 8 sesiones (2026-09-08 a 2026-09-17)
   reparto del tiempo (grilla, n 12283): VENDEN FUERTE 9.0 % · venden 21.4 % · PAREJO 41.1 % · compran 19.0 % · COMPRAN FUERTE 9.6 %
   lecturas que votan (grilla, % del tiempo con voto distinto de 0): FLUJO 1m 49 · FLUJO 5m 19 · GRANDES 50 · CVD 15m 45 · PRECIO 15m 56 | a favor cuando hay lado: 1/5 14 % · 2/5 38 % · 3/5 27 % · 4/5 14 % · 5/5 7 %
   por dia (grilla, % del tiempo VF/v/P/c/CF | cambios de estado VF/v/c/CF): 09-08 8/22/44/18/8 (95/352/282/88) | 09-09 11/20/38/21/10 (101/305/304/113) | 09-10 7/21/48/18/6 (94/362/284/83) | 09-11 10/25/39/19/7 (134/388/296/81) | 09-14 10/17/37/19/17 (95/316/377/134) | 09-15 12/25/40/16/7 (169/461/301/78) | 09-16 6/23/38/22/11 (73/261/322/88) | 09-17 8/17/45/20/11 (83/274/348/114)
   cambios de estado por rueda: mediana 823, min 744, max 1009 (un cambio cada 27 s de rueda elegible en promedio)

### CONFIRMAR — GRILLA (n 12283)
   COMPRAN FUERTE   n  1178 ( 8 dias) | describe: ultimo minuto del mismo lado 92.4 % (+9.74 pts) | +-8/600: 47.6 % [IC 43.5;50.2] dias>50 % 2 de 8, sin resolver  1 %
                      placebos: AZAR 49.7 +- 1.5 (z -1.5, lo iguala 184/200) | CORRIDO 30 min 42.8 [36.3;48.7] n 1126 | OTRO DIA 48.5 +- 1.3 (z -0.7, lo iguala 156/200)
                      retorno firmado 60 s +0.08 [-0.87;+0.85] (azar +0.08, corrido -0.49, otro dia +0.23) | 300 s +0.16 [-3.45;+3.03] (azar +0.44, corrido -1.69, otro dia +0.21)
   compran          n  2331 ( 8 dias) | describe: ultimo minuto del mismo lado 69.8 % (+3.55 pts) | +-8/600: 44.8 % [IC 41.3;48.4] dias>50 % 2 de 8, sin resolver  1 %
                      placebos: AZAR 49.0 +- 1.0 (z -4.1, lo iguala 200/200) | CORRIDO 30 min 48.6 [45.3;51.9] n 2159 | OTRO DIA 48.2 +- 1.0 (z -3.5, lo iguala 200/200)
                      retorno firmado 60 s -0.25 [-0.62;+0.14] (azar +0.01, corrido -0.17, otro dia +0.07) | 300 s -0.55 [-1.82;+0.82] (azar -0.01, corrido -0.95, otro dia +0.24)
   venden           n  2625 ( 8 dias) | describe: ultimo minuto del mismo lado 72.5 % (+3.74 pts) | +-8/600: 51.0 % [IC 49.3;52.7] dias>50 % 2 de 8, sin resolver  2 %
                      placebos: AZAR 51.5 +- 1.0 (z -0.6, lo iguala 143/200) | CORRIDO 30 min 52.1 [48.8;54.8] n 2359 | OTRO DIA 51.9 +- 0.9 (z -1.1, lo iguala 172/200)
                      retorno firmado 60 s -0.07 [-0.45;+0.30] (azar +0.02, corrido +0.37, otro dia -0.16) | 300 s -0.94 [-2.35;+0.25] (azar +0.14, corrido +0.03, otro dia -0.15)
   VENDEN FUERTE    n  1105 ( 8 dias) | describe: ultimo minuto del mismo lado 94.3 % (+9.51 pts) | +-8/600: 46.9 % [IC 43.6;50.5] dias>50 % 2 de 8, sin resolver  2 %
                      placebos: AZAR 51.4 +- 1.6 (z -2.8, lo iguala 200/200) | CORRIDO 30 min 54.2 [48.3;59.5] n 1000 | OTRO DIA 53.0 +- 1.3 (z -4.6, lo iguala 200/200)
                      retorno firmado 60 s -0.75 [-1.41;-0.01] (azar -0.02, corrido -0.52, otro dia +0.37) | 300 s -1.99 [-4.08;+0.03] (azar -0.06, corrido -0.83, otro dia +0.29)
   TODOS con lado   n  7239 ( 8 dias) | describe: ultimo minuto del mismo lado 78.2 % (+5.54 pts) | +-8/600: 47.8 % [IC 46.2;49.4] dias>50 % 3 de 8, sin resolver  2 %
                      placebos: AZAR 50.3 +- 0.6 (z -3.9, lo iguala 200/200) | CORRIDO 30 min 49.7 [47.5;52.2] n 6644 | OTRO DIA 50.3 +- 0.6 (z -4.5, lo iguala 200/200)
                      retorno firmado 60 s -0.21 [-0.50;+0.09] (azar +0.02, corrido -0.08, otro dia +0.06) | 300 s -0.80 [-2.13;+0.47] (azar +0.12, corrido -0.71, otro dia +0.08)

### CONFIRMAR — CAMBIOS (n 6856)
   COMPRAN FUERTE   n   779 ( 8 dias) | describe: ultimo minuto del mismo lado 89.6 % (+7.65 pts) | +-8/600: 48.7 % [IC 44.5;52.7] dias>50 % 3 de 8, sin resolver  1 %
                      placebos: AZAR 49.2 +- 1.9 (z -0.3, lo iguala 125/200) | CORRIDO 30 min 49.0 [45.9;52.0] n 722 | OTRO DIA 48.0 +- 1.8 (z +0.4, lo iguala 63/200)
                      retorno firmado 60 s -0.38 [-1.13;+0.40] (azar +0.05, corrido -0.55, otro dia -0.12) | 300 s -1.15 [-3.47;+0.93] (azar +0.35, corrido -0.10, otro dia +0.03)
   compran          n  2514 ( 8 dias) | describe: ultimo minuto del mismo lado 73.3 % (+3.81 pts) | +-8/600: 46.6 % [IC 43.5;49.9] dias>50 % 2 de 8, sin resolver  1 %
                      placebos: AZAR 49.2 +- 1.0 (z -2.6, lo iguala 199/200) | CORRIDO 30 min 50.0 [46.0;54.0] n 2299 | OTRO DIA 49.2 +- 0.9 (z -3.0, lo iguala 200/200)
                      retorno firmado 60 s -0.38 [-0.76;+0.01] (azar +0.04, corrido -0.28, otro dia -0.07) | 300 s -0.87 [-2.65;+1.04] (azar +0.16, corrido -0.49, otro dia -0.17)
   venden           n  2719 ( 8 dias) | describe: ultimo minuto del mismo lado 75.1 % (+3.81 pts) | +-8/600: 48.9 % [IC 47.6;50.1] dias>50 % 2 de 8, sin resolver  2 %
                      placebos: AZAR 51.6 +- 1.0 (z -2.8, lo iguala 199/200) | CORRIDO 30 min 53.1 [49.9;56.6] n 2406 | OTRO DIA 51.6 +- 1.0 (z -2.8, lo iguala 199/200)
                      retorno firmado 60 s -0.05 [-0.48;+0.37] (azar +0.01, corrido +0.30, otro dia +0.14) | 300 s +0.54 [-0.75;+1.79] (azar +0.05, corrido +0.28, otro dia +0.04)
   VENDEN FUERTE    n   844 ( 8 dias) | describe: ultimo minuto del mismo lado 91.4 % (+8.21 pts) | +-8/600: 46.2 % [IC 42.9;49.9] dias>50 % 2 de 8, sin resolver  2 %
                      placebos: AZAR 51.8 +- 1.8 (z -3.2, lo iguala 200/200) | CORRIDO 30 min 54.9 [49.3;60.5] n 750 | OTRO DIA 51.0 +- 1.4 (z -3.4, lo iguala 199/200)
                      retorno firmado 60 s -0.30 [-1.30;+0.71] (azar +0.01, corrido +0.56, otro dia +0.15) | 300 s +0.11 [-2.08;+2.11] (azar +0.16, corrido -0.70, otro dia -0.18)
   TODOS con lado   n  6856 ( 8 dias) | describe: ultimo minuto del mismo lado 78.2 % (+4.79 pts) | +-8/600: 47.7 % [IC 46.1;49.3] dias>50 % 2 de 8, sin resolver  1 %
                      placebos: AZAR 50.4 +- 0.6 (z -4.4, lo iguala 200/200) | CORRIDO 30 min 51.7 [49.5;53.9] n 6177 | OTRO DIA 50.2 +- 0.6 (z -4.0, lo iguala 200/200)
                      retorno firmado 60 s -0.24 [-0.60;+0.13] (azar +0.01, corrido +0.02, otro dia +0.05) | 300 s -0.22 [-1.35;+0.84] (azar +0.09, corrido -0.17, otro dia -0.03)

   VELOCIDAD (rango crudo de los ultimos 5 min, grilla): DORMIDO (< 16): 21 % del tiempo, mediana hasta +-8 128 s, sin resolver a 180 s 36 % (techo_ml: 97-125 s / 24-36 %) | tranquilo (16-24): 27 % del tiempo, mediana hasta +-8 87 s, sin resolver a 180 s 25 % (techo_ml: 75-86 s / 17-26 %) | normal (24-32): 17 % del tiempo, mediana hasta +-8 53 s, sin resolver a 180 s 9 % (techo_ml: 52 s / 9-11 %) | movido (32-48): 19 % del tiempo, mediana hasta +-8 29 s, sin resolver a 180 s 3 % (techo_ml: 29-31 s / 3 %) | DESATADO (>= 48): 16 % del tiempo, mediana hasta +-8 14 s, sin resolver a 180 s 1 % (techo_ml: 13-14 s / 1 %)

## VEREDICTO (regla escrita antes de correr)
- Describe: cuando el tablero tiene lado, el ultimo minuto fue del mismo lado el 76 % (explorar) y el 78 % (confirmar) de las veces en la grilla; en el segundo del cambio de estado, 79 % y 78 %.
- Anticipa: +-8 en 10 min del lado del tablero (todos los estados con lado): grilla 50.3 % -> 47.8 % (azar 50.1 / 50.3, corrido 48.2 / 49.7, otro dia 49.5 / 50.3); cambios 49.7 % -> 47.7 % (azar 50.2 / 50.4, corrido 48.0 / 51.7, otro dia 49.6 / 50.2). Empate 56.0 %.
- NINGUN estado pasa la regla en los dos tramos: el tablero describe la vela actual y NO anticipa el +-8 de los proximos 10 minutos.

**Para el operador, en criollo:** el tablero A te dice quien esta mandando en este momento y de donde viene el precio, y eso lo dice bien (cuando marca COMPRAN, el ultimo minuto subio la mayoria de las veces). Pero hacia adelante no sabe nada: apostar +-8 puntos para el lado que marca acierta lo mismo que apostar al azar, que apostar con el estado de hace media hora, o que apostar en otro dia a la misma hora. Es un espejo retrovisor prolijo, no un parabrisas. Sirve para no operar EN CONTRA de lo que esta pasando, nunca como razon para entrar.

Tiempo de corrida: 15 s.

## LECTURA DESPUES DE CORRER (post-hoc: observaciones, no reglas; nada de esto se probo por separado)

1. **Describe bien, y mas cuanto mas fuerte el estado.** COMPRAN FUERTE / VENDEN FUERTE coinciden con el signo del ultimo minuto el 92-94 % de las veces
   (ultimo minuto +9 a +10 pts a favor); los debiles (compran / venden) el 68-75 %. Es lo esperable: el voto doble del minuto ES el minuto.
2. **Hacia adelante, nada, y en confirmar hasta un poco EN CONTRA.** Todos los estados con lado: 50,3 % en explorar (z +0,3 contra azar) y 47,8 % en
   confirmar (z -3,9 contra azar, -4,5 contra otro dia). Es el mismo giro que vio techo_ml (dr_900 AUC 0,52 -> 0,47): en las 8 ruedas nuevas el flujo
   de 5-15 min tendio a devolverse. Con dos tramos que se contradicen no hay ni gatillo ni anti-gatillo; solo hay "no anticipa".
3. **El retorno firmado a 60 s es apenas negativo** (-0,2 pts, IC rozando cero en los dos tramos): la reversion minuscula de la vela siguiente que ya
   midio escalas.md (correlacion -0,02 a -0,05). No es operable: es un cuarto del costo de ida y vuelta.
4. **El resumen parpadea: ~850 cambios de estado por rueda, uno cada 27 s.** Con las zonas muertas tal cual (|r| < 0,10 en el minuto, que vota el
   42-49 % del tiempo), el estado cambia mas seguido de lo que dura una apuesta de +-8 (mediana 30-130 s segun la velocidad). Para el C# esto significa:
   (a) el registro tablero-<inst>-<dia>.jsonl va a tener ~850 lineas por rueda (liviano, ~100 KB), y (b) si el operador dice "cambia todo el tiempo",
   es verdad y es la regla, no un bug; una histeresis (p. ej. exigir el estado nuevo 2-3 segundos seguidos) se decide con el, no de oficio.
5. **FLUJO 5m casi no vota**: con la zona muerta 0,10 vota el 12 % (explorar) y 19 % (confirmar) del tiempo, porque la mediana de |r300| en rueda es 0,045
   (escalas_umbrales.json). En la practica el resumen es FLUJO 1m x2 + GRANDES + CVD + PRECIO. Si se quisiera que el 5 min pese, la zona muerta deberia
   ser ~0,05; no se toco porque la spec dice 0,10 y cambiarla despues de ver los numeros seria justo lo que el criterio prohibe.
6. **"A favor 5/5" es raro**: 5-7 % del tiempo con lado; 4/5 el 14 %. La mayoria de los estados con lado son 2/5 o 3/5 (65-70 %). El numero en pantalla va a
   ser casi siempre chico, y esta bien que se vea.
7. **La tabla de VELOCIDAD del tablero queda verificada con esta misma grilla**: mediana hasta +-8 = 97/75/52/31/13 s (explorar) y 128/87/53/29/14 s
   (confirmar); sin resolver a 180 s = 24/17/11/3/1 % y 36/25/9/3/1 %. Coincide con techo_ml.md 210-214. Los redondeos del C# (110 s / 30 %, 80 / 20,
   52 / 10, 30 / 3, 14 / 1) son promedios honestos de los dos tramos; el escalon DORMIDO es el que mas varia entre tramos (97 contra 128 s).

Archivos: tablero_01_placebo.py (todo), tablero_cache/tab-<sesion>-v1_z0.10_g0.3_c1.0.parquet (lecturas, puntaje y objetivos por segundo, 20 sesiones).
