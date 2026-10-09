---
name: estrategia-ronda-7
description: "22-09: ronda 7 evolutiva: 321 indicadores inventariados, 198 de ATAS implementados fiel (~970 senales), PythiaVWAP replica exacta (168 celdas), Gamma Hoy original vs rayas falsas, ~12.500 combinaciones (genetico/ensamble/regimen): nada pasa; mejor DSR 0,64; finalistas RS1 -2,64 y GA-C1 -3,81 en CONFIRMAR."
metadata: 
  node_type: memory
  type: project
  originSessionId: 6b882b35-31bc-4131-8cd4-4b099f670c25
  modified: 2026-09-22T09:00:36.124Z
---

`PythiaGex\laboratorio\dom\ronda7` (r7_piezas.py, piezas_*.parquet con 206 columnas auditadas causales: 0 diferencias en
378 M celdas; r7_registro_ensayos.csv lleva el N real para el Deflated Sharpe).

- Inventario: 321 = 296 clases de ATAS (294 visibles) + 15 propias + 10 tipos de terceros (sin descompilar). Fuente de las
  261 Technical en github.com/AtasPlatform/Indicators. Solo-en-vivo (no testeables hacia atras): DOM, DOM Power/Strength,
  MBO DOM, DOM Levels/Heatmap, Iceberg/Stops/Sweeps trackers.
- Filtro robusto (peor de 4 sub-periodos, placebo, DSR): tendencia/volatilidad 86 ind. 452 senales, osciladores 72/460,
  volumen/order flow 40/56: 0 pasan. Mejores: ADR ruptura, divergencia CCI/WAD, zonas de volumen al reves: se dan vuelta en EXTRA.
- PythiaVWAP (4 instancias del operador: 2 Sesion, 2 Mes; VWAP de vela del footprint): primer toque de linea/bandas en
  sesion/rueda/mes/semana/ayer = raya falsa a medio desvio, en DISENO y EXTRA. Sirve para ubicarse, no como gatillo.
- Gamma Hoy original: D1/D2/D3/ZERO no le ganan a rayas falsas; rebote-dom de produccion empata con el azar. La
  historia reconstruida (rebobinado) coincide con lo visto entre 4 y 94 % segun libro. Pista ZERO x PythiaVWAP (+1,81 t
  2,67 n 77 en MES) sale SOLO de lo no fiel; con lo que estuvo en pantalla da 0. Grabador hacia adelante listo:
  r7_gx_04_grabador.py (desde 23-09, lee solo -hoy-/-hoyrithmic-).
- Evolucion: genetico 6.736, ensamble 2.445, regimen 2.111 combinaciones (N acumulado ~12.500). Mejor DSR 0,64 (RS1).
  CONFIRMAR: RS1 -2,64 n 30; GA-C1 -3,81 n 72. FINAL sigue sellado para MES 5 min.

**Why:** 7 rondas y ~13 mil ensayos sin nada fuera de muestra = no hay ventaja mecanica intradia con estas herramientas y
costos de minorista; lo que aparece en diseno es ruido del tamano esperado por la cantidad de intentos.
**How to apply:** no seguir minando la misma historia (solo fabricaria falsos positivos); lo honesto que queda: validacion
HACIA ADELANTE (grabadores), ejecucion (limitada ahorra 0,15-0,36), mapa de costo/movimiento y pronostico de rango, y la
bitacora de SUS operaciones. Ver [[estrategia-ronda-6]], [[busqueda-estrategia-sin-parar]].
