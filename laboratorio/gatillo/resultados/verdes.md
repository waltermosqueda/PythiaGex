# Las verdes fuertes: la FUERZA de la raya (pista del operador del 17-09)

## Pre-registro de la replica en ES/MES (escrito 2026-09-17 18:29, ANTES de calcular nada de ES)

**De donde viene:** en MNQ, 7 dias, 247 ordenes limitadas ejecutadas en las dominantes del libro de opciones de NQ: el rebote (+12 antes que -6) sube con la fuerza
de la raya (gamma x volumen del strike): 32 % -> 36 % -> 44 % -> 61 %, y las mismas rayas corridas hacen lo contrario (39 -> 28 %). Exploratorio, cortes puestos a mano.

**Replica (otro mercado, mismo calculo):** dominantes del libro de opciones de ES (viva-ES) contra la cinta de MES. Parametros a escala de ES (1/4 de NQ): tolerancia 0,5,
venia de >= 2,5 pts, regla principal +3 / -1,5 desde la raya, orden limitada con llenado real, solo rueda, raya con foto de menos de 5 min.
- **H1 (fuerza):** con los toques partidos en CUARTILES de fuerza (calculados sobre la propia muestra de ES, sin mirar resultados), el cuartil mas fuerte rebota
  >= 10 puntos porcentuales mas que el mas debil, Y el cuartil mas fuerte de las rayas reales rebota >= 10 puntos mas que su gemelo placebo (rayas corridas +-2,5 y +-1,25).
- **H2 (velocidad):** el tercio que llega mas LENTO (|precio - precio de hace 60 s|) rebota >= 8 puntos mas que el tercio mas rapido.
- **Veredicto:** REPLICA si se cumple H1 completa; PARCIAL si se cumple solo una de sus dos mitades; NO REPLICA si ninguna. H2 se informa aparte. No se retoca nada despues de ver el resultado.

## Corrida de ES (2026-09-17 18:29)

    ES, regla +3/-1.5 desde la raya, orden limitada: VERDES 35.5 % de 166 | placebo 34.5 % de 692 | azar puro 33.3 %
       cuartiles de fuerza (cortes 2503 / 4475 / 7657 M):
          VERDES : Q1 debil 43 % (n 42) | Q2 19 % (n 42) | Q3 39 % (n 41) | Q4 fuerte 41 % (n 41)
          placebo: Q1 debil 36 % (n 194) | Q2 32 % (n 164) | Q3 33 % (n 180) | Q4 fuerte 38 % (n 154)
       velocidad de llegada (cortes 1.50 / 3.17 pts en 60 s):
          VERDES : lento 40 % (n 60) | medio 32 % (n 50) | rapido 34 % (n 56)
          placebo: lento 33 % (n 238) | medio 36 % (n 239) | rapido 34 % (n 215)
       por dia: 09-08 38 % (24) | 09-09 33 % (3) | 09-11 40 % (35) | 09-14 28 % (39) | 09-15 100 % (1) | 09-16 36 % (47) | 09-17 35 % (17)
    ES, regla +5/-2 desde la raya, orden limitada: VERDES 26.3 % de 152 | placebo 25.0 % de 643 | azar puro 28.6 %
       cuartiles de fuerza (cortes 2503 / 4475 / 7657 M):
          VERDES : Q1 debil 28 % (n 40) | Q2 21 % (n 42) | Q3 38 % (n 37) | Q4 fuerte 18 % (n 33)
          placebo: Q1 debil 25 % (n 190) | Q2 22 % (n 163) | Q3 26 % (n 168) | Q4 fuerte 29 % (n 122)
       velocidad de llegada (cortes 1.50 / 3.17 pts en 60 s):
          VERDES : lento 33 % (n 55) | medio 25 % (n 44) | rapido 21 % (n 53)
          placebo: lento 28 % (n 218) | medio 24 % (n 221) | rapido 24 % (n 204)
       por dia: 09-08 38 % (21) | 09-09 33 % (3) | 09-11 27 % (30) | 09-14 27 % (37) | 09-15 0 % (1) | 09-16 24 % (45) | 09-17 13 % (15)

## H3: la raya como zona ancha en ES (pre-registrada y corrida 2026-09-17 18:31)

    ES +7.5/-3.75: VERDES 24.1 % (n 112, neto -1.38 pts) | placebo 26.3 % (n 513, neto -1.13) | diferencia -2.2, z -0.49 | azar 33.3 %
    ES +6.25/-3: VERDES 28.6 % (n 133, neto -0.69 pts) | placebo 26.2 % (n 568, neto -0.91) | diferencia +2.3, z +0.54 | azar 32.4 %
    ES +3/-1.5: VERDES 35.5 % (n 166, neto -0.22 pts) | placebo 34.5 % (n 692, neto -0.27) | diferencia +1.0, z +0.24 | azar 33.3 %
