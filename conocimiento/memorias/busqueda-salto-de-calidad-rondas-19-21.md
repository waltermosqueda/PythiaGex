---
name: busqueda-salto-de-calidad-rondas-19-21
description: "Pedido del operador 27-11-2026 madrugada: la FORMULA FINAL mas chica (no todas las variables) con el % de acierto mas alto posible, pocas operaciones por rueda (1-5), recorridos largos; 'no escatimes recursos'. Plan en 3 etapas: R19 confluencias (niveles + order flow), R20 GEX como contexto de la ola, R21 meta-etiquetado y seleccion final"
metadata:
  node_type: memory
  type: project
  originSessionId: 233bf8d4-5113-4e95-a0d9-68bff726690d
  modified: 2026-11-27T12:20:19.946Z
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

**RESULTADO R21 (27-11 05:06 AR, `ronda21_regimen/RESULTADOS.md`):** detector D1 "cuentakilometros" = rango de la rueda
hasta ahora / (mediana del rango de 10 ruedas x fraccion tipica a esa hora; curva fija de MES antes del 08-09); TENDENCIA si >= 1,6
(precio del mismo lado del VWAP y de la apertura), ROTACION 1,0-1,6, ESTRECHO <= 1,0 (esperar). En vivo: con TENDENCIA el extremo
nuevo llega antes que el VWAP 60-66 % vs base 37-40 %. Lo PRE-REGISTRADO no llego (53,8 %, +0,05). La evolucion POST HOC
COMB-T7+V4 (TENDENCIA -> continuacion T7: ola a favor de la marea del lado de la tendencia, VWAP detras, + nivel o barrida o delta
5 min a favor; ROTACION -> C1 banda 2σ + iceberg; 1R) da 60,4 / 63,8 / 66,7 % por periodo, juntas 63,1 % +1,20/op t 2,12, 2,2 ops
por rueda; elegida mirando las 55 ruedas, filo de cuchillo, Bonferroni 3,45 no pasa -> CANDIDATA para juez en vivo, no probada.
Alternativa T6-P1 (solo tendencia, mitad a 1R y resto a 2R con stop a la entrada): 64,9 % +1,88 t 2,28. Baul #47-53.
Siguiente: congelar (SHA-256) y probar en MNQ 28-07..13-09 (33 ruedas, otro instrumento y periodo anterior); bitacora en vivo con
cintas de ES desde el lunes 30-11 (27-11 media rueda excluida).

**RESULTADO R20 (GEX, 27-11 ~05:25):** el GEX (SPX, CBOE con retraso real) anticipa el ANCHO de la rueda, no si tiende o vuelve:
gamma negativa a las 9:59 -> rango 1,45x la mediana vs 0,95x (t 2,67; la mitad ya esta en el rango de los primeros 30 min);
llega mas seguido y antes al TENDENCIA del D1 (15/31 vs 3/18, minuto 55 vs 101). No rescata a las finalistas de la R19 ni a R1
(33,7 vs 33,2 %). Mecanica: con gamma negativa hay el doble de piernas por rueda con el mismo largo ("subir el volumen de la radio").
Tabla `scratchpad\r20\gex_minuto.parquet` (57 ruedas; sin cadena 20-25-11 por el corte de CBOE). Baul #54-57.

**MNQ (27-11 05:24):** COMB-T7+V4 CONGELADA (SHA-256 dd8e18ad...) en MNQ 14-08..21-09 (25 ruedas): 52,2 %, -0,13/op; en las 18
sin fechas de ES 40,7 %, -4,94; lo bueno (68 %) salio de los 7 dias que coinciden con el diseño de ES. Mecanica: el D1 compara con
la mediana de 10 ruedas; si fueron de otro clima (fines de julio gigantes) lee mal. Bitacora en vivo con ES lista
(`ronda21_regimen/r21_11_vivo.py`, desde el 30-11; 26 y 27-11 excluidos).
**Datos independientes (cotizado gratis 27-11):** Databento GLBX.MDP3 tbbo ES.c.0: jun-ago 2026 USD 59,21 (~63 ruedas, 2,3 GB);
mar-ago USD 125,25 (~125 ruedas). Ya gastado USD 41,30; la cuenta tenia tope/sin credito (402 el 08-09). COMPRA = decision suya.
**La Ola 1.0** (indicador aparte, `atas/PythiaOla`) en construccion por un agente: fondo de marea + ola + rayas + rombos + panel
con CLIMA D1; sin señales (ninguna valido); Superindicador vuelve a 1.9b.

**La vara doble (decirsela siempre):** el % de acierto solo se infla con objetivo chico y stop grande y se pierde plata; se juzga
acierto Y esperanza neta con costo, fuera de muestra, con correccion por la cantidad de variantes. Datos: 49 ruedas de ES
(07-09 a 13-11) + MES 16-25-11; mas historia de cinta no hay sin pagar ([[databento-cuenta-y-costos]]).
Ver [[ronda-18-senal-ola-y-indicador-aparte]], [[teoria-dura-y-confluencia]], [[explicar-el-movimiento-no-azar]].

**RONDA 22 (27-11 07:47 AR, `laboratorio/ronda22_clima_5min/`): CORRIGE lo que le dije al operador a la noche.** Prueba pre-registrada (SHA,
corrida unica) del semaforo D1 en 242 ruedas de MES de 5 min (09-09-2025..31-08-2026, nunca usadas para probar) + 325 de 30 min (2024-25):
H1 NO PASA: TENDENCIA 53,6 % "extremo +X/2 antes que el VWAP" vs base 41,3 % (+12,2; vara >=55 y +15); 30 min 55,3 vs 40,9. Recalculado por
mi con codigo propio: 53,5 vs 41,2 (+12,2), mismos 16.214 puntos. Con la GEOMETRIA controlada (misma distancia al extremo y al VWAP) el semaforo
agrega +2,4 pts por trimestre: el "60-67 vs 37-40" de la R21 era sobre todo UBICACION DEL PRECIO, no clima. H2 (T7, T6-P1, COMB, V4 en 5 min,
entrada al cierre de la vela de la vuelta) pierden todas: 42-48 %, -2,1 a -2,9 pts/op. Mecanica: el "ELASTICO del VWAP" (vuelve 7-10 pts mas que
el azar) RESULTO SER LA VARA (ver R23 abajo: la formula de dos metas fijas; contra un azar bien hecho no hay elastico de todo el dia); la vara de 10 ruedas da falsas alarmas tras semanas calmas; la T7 en 5 min persigue (entra a 2X
del extremo). Pistas post hoc al baul privado #58-64 (doble vara 10+60: 57,5 %; franja 11:00-12:25: 67 vs 50 %; ruptura de la banda 2 sigma
despues de las 14 h: 64,5 % en 31). Muestras GASTADAS para el clima D1: m5 sep-2025..ago-2026 y m30 abr-2024..ago-2025. Calibracion: entrada a
stop dentro de la vela de 5 min infla (86,6 %) o hunde (29,2 %) contra la cinta (62,8 %): con velas, entrar al CIERRE.
**Para decirselo:** el panel CLIMA de La Ola describe cuanto recorrio el dia, no es una llave estable; la COMB congelada se sigue juzgando en vivo
(su escala nativa no la juzga la R22) pero con expectativa baja.
**Databento re-cotizado 27-11 08:05 (gratis, get_cost):** jun-ago 2026 YA NO es ciego (esos dias los vio la R22 en 5 min). Ciego = antes de
2024-04-29 (la m30 de la H1b arranca ahi): ES.c.0 tbbo jun-ago 2023 USD 49,95 (1,9 GB, ~63 ruedas); mar-ago 2023 USD 103,04; todo 2023 USD 210,36.
Hace falta un convertidor tbbo -> cinta (agrupar fills por orden agresora) validado con 1 dia que tambien tenga la sonda. Compra = decision suya.

**RONDA 23 (27-11 08:09-09:10 AR, `laboratorio/ronda23_diseno/`): diseño SOBRE DATOS VISTOS -> 1 candidata congelada (A3) + juez en vivo.**
**Corrige a la R22 (y a lo que le dije al operador a las 09:0x):** el elastico de todo el dia era la VARA: la formula de dos metas fijas
d_V/(d_E+d_V) ignora el corte de las 16:00 y el VWAP que se mueve hacia el precio. Recalculado por mi con dos azares propios (reflejo y
permutacion de velas, `ronda23_diseno/verificacion_por_fuera/elastico_propio.py`), 242 ruedas, 13.310 puntos: "extremo +X/2 primero" real 44,6 %
vs azar 44,3 / 43,6 vs formula 48,9. Queda solo horario y chico: mañana estira (+2 sobre el azar), tarde vuelve (+1,6 contra el reflejo), no robusto.
**A3 "elastico de la tarde despues del giro":** cierres de 5 min 13:00-15:30 NY; precio a >= 1,5 X del VWAP de la rueda; IB extendido <= 1 vez su
ancho del lado del precio; P geometrica = d_stop/(d_stop + d_VWAP) >= 0,55 (ya volvio desde el extremo). Hacia el VWAP al cierre de la vela;
stop X/2 tras el extremo del dia; objetivo limite en el VWAP que se corre cada 5 min (lleno con 1 tick mas alla); cierre 16:00; 1 a la vez, max 5.
~1 op/rueda, objetivo ~10 pts, stop ~23 (NO captura piernas largas). Acierto 74,3 / 67,5 / 74,2 % (diseño MES 116 r / chequeo MES 126 r /
cinta ES 55 r), neto +2,49 / +0,66 / +3,17 con costo 1,00, t por rueda 2,30 / 0,52 / 2,17. **El azar con la MISMA ubicacion acierta ~63 % y
pierde ~1 pt/op**: casi todo el acierto es geometria (stop lejos, objetivo cerca); lo propio de la regla es +5 a +12 pts de acierto y +1,5 a +4,4
pts/op. 272 variantes en diseño (Bonferroni 3,67 > mejor t 2,71): CANDIDATA, no probada; chequeo flojo (abr-jun 2026 = azar). Pierde en dias que
terminan en tendencia (55,8 %, -2,84), gana en balance (77,5 %, +3,24): falta reconocer el dia de tendencia antes de las 13:00.
**Verificado por mi desde el PREREGISTRO, sin el codigo del agente (`verificacion_por_fuera/`):** cinta 62 de 62 operaciones identicas (entrada,
stop, salida, puntos); MES diseño identico (109, 74,3 %, +2,49); chequeo 122 vs 123 (la rueda 19-03-2026 tiene 5 velas faltantes y el agente la
cuenta en la historia de rangos: X 6,75 vs 7,00 el 24-03); azar propio 63,2 / 62,6 / 61,7 % (agente 62,9 / 62,8 / 62,5). Huellas IGUAL.
**Juez en vivo:** tras bajar la cinta del dia, `python -I r23_vivo.py` (carpeta de la ronda; estado en %LOCALAPPDATA%\PythiaGex_r23_vivo, vacio
hasta el 30-11). Decide a las 60 y 120 jornadas (acierto >= 60, neto > 0, t >= 2,00 / 1,98, >= 40 ops); futilidad 20 y 40; descriptiva a las 15.
Potencia a las 60: 6-60 % segun el efecto: lo probable es "sin decidir". ES 2023 (si compra): mismo juez con --carpeta --estado
--sembrar-desde-cintas, una lectura con alfa 0,05. A1 y A2 descartadas. Baul privado #65-71.
