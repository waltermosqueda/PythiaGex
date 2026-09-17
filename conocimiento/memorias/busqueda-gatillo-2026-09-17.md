---
name: busqueda-gatillo-2026-09-17
description: "Busqueda de un complemento/gatillo para el CVD con la cinta orden por orden (34 ruedas de MNQ, 7 de MES): 9 familias + ML = NADA en direccion; las verdes de NQ rebotaron de verdad el 17-09 (dia de tunel) pero no los otros dias ni en ES; que SI se sabe (tamaño del movimiento, velocidad de la apuesta) y como seguir midiendo."
metadata: 
  node_type: memory
  type: project
  originSessionId: 9345174c-7f69-4fe5-a793-976e41f2dc7c
  modified: 2026-09-17T21:32:29.642Z
---

Pedido del operador (17-09 tarde): "un complemento al CVD que funcione de gatillo; sos libre". Despues, su pista: "las verdes fuertes (dominantes
de NQ) rebotan varias veces por jornada, yo las veo; el donde es tan importante como el que; me niego a creer que no haya nada".

**Datos nuevos (ver [[sonda-cinta-historica-atas]]):** 34 ruedas de MNQ orden por orden con la punta antes/despues (20 de trabajo desde el 20-08 +
14 de RESERVA 31-07..19-08 que nadie mira salvo validacion final), 7 ruedas de MES, tabla de 1 s (`laboratorio/gatillo/base.py`): delta por tamaño,
barridos, absorcion real en la punta, OFI total y pasivo. Costo medido: spread en reposo 1,44 ticks -> 0,96 pts ida y vuelta; empate +-8 = 56,0 %.

**Resultado de las 9 familias (12 variantes pre-registradas c/u, explorar 12 ruedas -> confirmar 8, placebo, escepticos): TODO NADA en direccion.**
Barridos/grandes/rafagas, temporalidades 5 s-30 min y delta por tamaño, punta del libro y flujo pasivo, absorcion real, ubicacion (VWAP, ayer, noche,
apertura, redondos), gamma x flujo, y el techo con ML (101 rasgos, AUC 0,47-0,51; lo aprendido en un tramo sale AL REVES en el siguiente). El modelo
de ES del 10-09 (61 %/83 %) DESAPARECIO fuera de muestra (AUC 0,52; el 83 % eran 59 cortos pegados en 11 medias horas con placebo 77 %); ademas
cada arranque de Gamma Hoy a la tarde dibuja una flecha 'modelo' CORTO falsa (modelo frio + niveles NaN): apagarlo o calentarlo. La unica pista
(ir contra el delta extremo de 5 min: 56 %/62 %) murio en la reserva: 50,0 % (n 244).

**Lo que SI se sabe y sirve:** (1) la apuesta +-8 de MNQ se resuelve en 48 s de mediana (20 s en un nivel): un gatillo al cierre de la vela de 1 min
llega tarde; (2) el TAMAÑO del movimiento de los proximos 5 min se anticipa bien (AUC 0,86-0,90) pero sale del rango previo y la hora, el flujo no
agrega; (3) barridos, rafagas y racimos de absorcion = 'cinta caliente': avisan velocidad, no lado; (4) delta + flujo pasivo explican 77-79 % de la
vela actual (delta solo 66-71 %); (5) un nivel en MNQ es zona ancha: lo pasan >= 4 pts en 73 % de los toques.

**Las verdes de NQ (su pista):** se rehacen desde `viva-NQ-<dia>.jsonl` con `capas_nq.recalcular` (93 % a <= 2 pts de la estela real del 17-09;
la estela exacta vive en `%APPDATA%\ATAS\PythiaGex\estela\` desde el 16-09). Con orden limitada en la raya y llenado real: **17-09: 58 % de 36 contra
30 % del placebo (+12/-6); los otros 6 dias 24-44 % = placebo; grilla de strikes de 25 pts en 33 ruedas: 43,3 contra 43,7 %.** El 17-09 fue un DIA
DE TUNEL: gamma neta del libro NQ 31,7B (2,6x el dia siguiente), verdes de 11B a solo 25 pts (strikes pegados), vispera del vencimiento trimestral.
La 'fuerza de la raya' como factor: gradiente lindo con el 17-09, casi nada sin el; replica pre-registrada en ES/MES: NO replica. Como ZONA ANCHA
(+30/-15) las verdes de NQ le ganan al placebo 35,9 contra 29,0 % (sin el 17-09: 35,2 contra 28,6): modesto, empata con costos, no replica en ES.
ERROR MIO A NO REPETIR: le anuncie '62 % sin el 17-09' con n = 8 y una definicion de toque que despues cambie: al recontar quedo en 37 %. Se lo corregi.

**Why:** el operador ve rebotes reales (el 17-09 lo fueron) y generaliza desde el dia que mira; la respuesta honesta es medir TODOS los dias con la
misma regla y el placebo de rayas corridas, y decirle cuales dias si y cuales no.
**How to apply:** cada dia nuevo suma muestra gratis (estela + viva + cinta por la sonda): correr `laboratorio/gatillo/verdes_06/10` tras el cierre y
llevar la cuenta; hipotesis a seguir: 'dia de tunel' = gamma neta del libro NQ muy alta para la hora + verdes a 25-50 pts (se ve ya a las 10:30-11:00
locales). No dibujar gatillos direccionales nuevos hasta que algo pase reserva o replica. Flujo Claro quedo agregado al grafico de MES (panel chico)
para poder pedir su cinta. Ver [[gatillo-cientifico-2026-09-10]], [[flujo-claro-cvd-superador]], [[regla-roja-roll-libro-vivo]].
