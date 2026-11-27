---
name: busqueda-salto-de-calidad-rondas-19-21
description: "Pedido del operador 27-11-2026 madrugada: la FORMULA FINAL mas chica (no todas las variables) con el % de acierto mas alto posible, pocas operaciones por rueda (1-5), recorridos largos; 'no escatimes recursos'. Plan en 3 etapas: R19 confluencias (niveles + order flow), R20 GEX como contexto de la ola, R21 meta-etiquetado y seleccion final"
metadata:
  node_type: memory
  type: project
  originSessionId: 233bf8d4-5113-4e95-a0d9-68bff726690d
  modified: 2026-11-27T07:32:32.058Z
---

Textual (27-11 ~02:40-03:00 AR): "ampliar el estudio y cruzar mas variables hasta encontrar la evolucion ganadora: VWAP,
imbalance, absorcion, big trades, icebergs, niveles automaticos (max/min de ayer, Asia y Londres, balance inicial, VWAP semanal y
mensual, area de valor de la semana anterior)... no quiero 30 trades por jornada, unos cuantos de calidad que acierten y se
desplacen buena cantidad de puntos"; despues sumo el GEX (strikes de mayor gamma, positivo/negativo, OI, flujo de calls/puts) y
"no tenemos que usar todas las variables sino la mejor formula final... aumentando considerablemente la cantidad de aciertos...
no escatimes recursos, tenemos que lograr el salto de calidad".

**Plan (agentes con effort max, en paralelo):**
- R19 `PythiaGex/laboratorio/ronda19_confluencias/`: contexto (marea con agresores, lado del VWAP) -> lugar (nivel) -> momento
  (absorcion/iceberg/imbalance/grandes), ESPERAR por defecto, salidas que dejan correr (stop tras el extremo; nivel siguiente,
  2-3R o raya de la marea). Exporta TODOS los eventos candidatos con rasgos a scratchpad\r19\eventos_todos.parquet.
- R20 `PythiaGex/laboratorio/ronda20_gex_contexto/`: regimen de gamma (flip por barrido de spot, GEX neto, muros, flujo sin lado)
  como CONTEXTO del largo de la ola (teoria de hedging de dealers: gamma negativa estira, positiva frena); tabla por minuto con
  antiguedad del dato en scratchpad\r20\gex_minuto.parquet. Las rayas de gamma como rebote ya fallaron (rondas 14-15): no repetir.
- R21 (despues): meta-etiquetado sobre los eventos de R19 + GEX de R20, validacion purgada por dia, seleccion de la formula mas
  chica; juicio UNA vez en la guardada MES 16-25-11; despues prueba en vivo (bitacora) y recien ahi al indicador La Ola.

**Cambio de prioridad (27-11 ~03:10 AR):** "prefiero darle con todo a la ronda 19... ahi esta el edge ganador: zonas, niveles
automaticos, VWAP y el order flow (absorcion, icebergs, imbalances, ordenes grandes, delta)". R20 (GEX) FRENADA (su carpeta y
pre-registro quedan; se retoma si el lo pide). R19 amplia por familias pre-registradas: (a) rechazo en zona con absorcion/iceberg,
(b) ruptura con iniciativa (apilados + grandes), (c) retroceso al VWAP a favor de la marea, (d) subasta fallida/barrida que vuelve,
(e) divergencia de delta en nivel; registro_variantes.csv comun + correccion por el TOTAL de variantes. Cuando exista
scratchpad\r19\eventos_todos.parquet (el agente avisa a "main"), sumar exploradores en paralelo por familia sobre esa base.

**Estado 27-11 ~04:00 AR:** base `scratchpad\r19\eventos_todos.parquet` (4.822 eventos x 154 cols; diseno = ES 10-09..15-10,
26 ruedas, 2.250; confirma = ES 16-10..13-11, 21 ruedas, 2.572; la guardada MES 16-25-11 NO esta) + `eventos_diseno.parquet`;
diccionario `ronda19_confluencias\DICCIONARIO_EVENTOS.md`. Hallazgo: el periodo es "va y vuelve" (21/26 y 21/21 ruedas);
mejor pista E4 = vuelta al valor + absorcion o big trades: 30 ops en diseño, 50 % con 2R, +1,61 neto/op, t por rueda 1,29 (no
pasa Bonferroni N=27, t 3,20). Lanzados 3 exploradores (zonas a+d, delta e, modelo meta-etiquetado) con registros propios
`registro_variantes_<x>.csv`; el agente principal sigue E3/E4 + familia (b) y hace el ARBITRAJE: abre confirmacion UNA vez
para <= 4 finalistas de todos, despues la guardada.

**27-11 ~04:00 AR, antes de irse a dormir:** "hacelo todo vos... no pares hasta lograr resultados altamente positivos, con empate
no sirve, segui hasta superar los 60 o 70 % de aciertos, se que es dificil pero no imposible y recursos nos sobran". Autorizo a
hacer todo (incluido abrir un grafico de ES con el Superindicador para bajar ES 16-25-11 por la sonda). La regla del foco sigue:
tocar ATAS solo con GetLastInputInfo >= 60 s (vigia de 180 s). ES manda en el flujo (medido: 12-15x el dinero del MES, delta por
minuto mismo signo 72-77 %): la guardada pasa a ES 16-25-11; MES queda como chequeo secundario.

**RESULTADO R19 (27-11 04:30 AR):** 1.321 variantes de diseño; 4 finalistas (una por fuente, casi sin operaciones en comun):
ZONAS C1 (banda ±2σ del VWAP del lado opuesto sin perforar + iceberg pasivo, 1R), DELTA w6 (FADE de la vuelta rapida con
apilados o divergencia en nivel, S6m), E4 (vuelta al valor + absorcion/grandes, 1R), MODELO A (a >1 desvio del VWAP de jornada +
grandes netos a favor 60 s + pierna de marea corta, 1R). Diseño 58-75 % -> CONFIRMACION (ES 16-10..13-11) 46-51 %, neto
-0,49..-0,75; GUARDADA ES 16-25-11: C1 54,5 % +0,40 (t 0,26), A 48 % -0,92, w6 38 % -2,86, E4 29 % -4,21. NINGUNA PASA.
Mecanica: las 4 eran apuestas de VUELTA AL VALOR; el diseño fue "va y vuelve" (ruptura de la zona de ruido -3,1 pts al cierre,
t -2,45), la confirmacion neutra y la guardada de TENDENCIA (ruptura +32 pts, 71 % a favor, rango mediano 138 vs 57). El borde era
el REGIMEN, no la huella. La regla de regimen del baul (#19) llega tarde. Base completa: scratchpad\r19\eventos_todos_con_guardada.parquet
(5.895 vueltas). Archivos: ronda19_confluencias\RESULTADOS.md; baul #42-46.
**SIGUIENTE (lanzado 27-11 ~04:40):** R21 = el REGIMEN como llave (que familia operar segun el clima del dia): GEX (R20 retomada) +
estructura del dia en tiempo real; validacion dejando un PERIODO afuera (diseño / confirmacion / guardada) y juez final EN VIVO.
Toda la data disponible ya fue vista: decirlo siempre.

**La vara doble (decirsela siempre):** el % de acierto solo se infla con objetivo chico y stop grande y se pierde plata; se juzga
acierto Y esperanza neta con costo, fuera de muestra, con correccion por la cantidad de variantes. Datos: 49 ruedas de ES
(07-09 a 13-11) + MES 16-25-11; mas historia de cinta no hay sin pagar ([[databento-cuenta-y-costos]]).
Ver [[ronda-18-senal-ola-y-indicador-aparte]], [[teoria-dura-y-confluencia]], [[explicar-el-movimiento-no-azar]].
