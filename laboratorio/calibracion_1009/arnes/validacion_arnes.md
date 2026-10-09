# Validacion del arnes (evaluar.py)

Generado 2026-10-09T04:36:03+00:00 UTC. arnes sha256 `1cd6083989f4ac7c870b96801a128cb8bae25f5c98efa023537d676cf44d991c`; juez copia `a20a62688e888da22c36d9200bca5f63f16949ca90837545b37847a16188d5bf` (original igual a la copia: True).

Dias de ENTRENAMIENTO usados en (a) y (b): 2026-09-14, 2026-09-15, 2026-09-16, 2026-09-17, 2026-09-18, 2026-09-21, 2026-09-22, 2026-09-23, 2026-09-24, 2026-09-25, 2026-09-28, 2026-09-29, 2026-09-30 (13 sesiones). Ninguna candidata se corrio.

## (a) Oraculo: rayas en la mecha exacta de cada vela (miran adelante a proposito)

| rayas | ventana | cobertura giros 20 | giros 20 | cobertura giros 40 | giros 40 | % sostenidos |
|---|---|---|---|---|---|---|
| hl | noche | 100.0 % | 1054 | 100.0 % | 256 | 53.2 % |
| hl | dia | 100.0 % | 963 | 100.0 % | 349 | 62.8 % |
| h | noche | 49.9 % | 1054 | 49.6 % | 256 | 52.9 % |
| h | dia | 50.1 % | 963 | 50.1 % | 349 | 62.0 % |

## (b) Rayas al azar causales (20 conjuntos, 150 juegos de placebo cada uno)

| ventana | % sostenidos medio | llegadas medias | ventaja vs azar (media ± sd) | ventaja vs corridos (media ± sd) | p_azar < 0,05 | IC95 vs corridos cubre 0 | rebote toque real / azar |
|---|---|---|---|---|---|---|---|
| noche | 44.9 | 498 | -0.68 ± 1.81 pp | -0.63 ± 1.85 pp | 0 % | 95 % | 59.7 / 60.3 |
| dia | 48.5 | 324 | -0.87 ± 3.61 pp | -0.44 ± 4.49 pp | 5 % | 75 % | 60.4 / 61.1 |

noche: conjuntos al azar que cumplen E1-E6 (con p sin ajustar): 0 de 20; elegibles por la regla de seleccion: 0; por criterio: E1_muestra 20, E2_tamano 0, E3_significancia 0, E4_consistencia 1, E5_no_empeora 6, E6_no_es_eco 20.

dia: conjuntos al azar que cumplen E1-E6 (con p sin ajustar): 0 de 20; elegibles por la regla de seleccion: 1; por criterio: E1_muestra 20, E2_tamano 0, E3_significancia 1, E4_consistencia 4, E5_no_empeora 5, E6_no_es_eco 20.

## (c) Reproduccion de numeros ya medidos (juez de toque, velas m2)

Referencia: tres/resultados/extremos_rebote.md:19,64 (noche, 22 sesiones) y :118,152 (dia, 21 sesiones), corrida del 08-10.

| serie | toques esperados / medidos | % rebote esperado / medido | % extremo esperado / medido |
|---|---|---|---|
| E_rango60_noche | 1667 / 1716 | 61.3 / 61.5 | 47.5 / 47.7 |
| E_rango60_dia | 644 / 643 | 56.7 / 56.9 | 32.8 / 32.8 |
| azar_fijo_noche | 16962 / 16982 | 63.5 / 63.1 | 50.4 / 49.9 |
| azar_fijo_dia | 12279 / 12433 | 61.6 / 62.6 | 36.6 / 37.4 |
| E_rango60_noche sin la noche 10-08 (la corrida vieja la vio incompleta) | 1667 / 1626 | 61,3 / 61.3 | 47,5 / 47.2 |

Censo de giros (zigzag del juez + filtro de sanidad, m1, 23 sesiones 08-17..09-17) contra soportes_metodo/censo_mechas.md (34 sesiones 08-02..09-17, otro algoritmo de giro): 20 pts noche 87.3 (esperado 90,2), dia 70.4 (esperado 80,2); 40 pts noche 22.2 (esperado 23,3), dia 23.6.
