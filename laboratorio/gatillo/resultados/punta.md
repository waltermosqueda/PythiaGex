# Familia PUNTA — desbalance de la punta y flujo de ordenes pasivo (lo que el CVD no ve)

Fecha: 17-09-2026. Instrumento: MNQ, tabla de 1 segundo de `base.py` (20 sesiones, 20-08 al 17-09). Scripts: `punta_lib.py`, `punta_00_sanidad.py`, `punta_01_conteo.py`, `punta_02_micro.py`, `punta_03_explorar.py`, `punta_04_confirmar.py`.

## 0. PRE-REGISTRO (escrito ANTES de mirar ningun resultado futuro)

Lo unico que se miro antes de escribir esto fue la FORMA de los rasgos en las 12 sesiones de explorar (`punta_00_sanidad.py`, `punta_01_conteo.py`): cuantiles de la punta, del OFI y cuantos disparos da cada condicion. Ningun resultado (ni barrera ni movimiento futuro) se miro para fijar los umbrales.

Forma de los datos, medida en explorar, rueda:
- La punta de MNQ es flaca: mediana 7 contratos por lado (p95 = 16). Spread 0,25 a 0,50 pts. Hay ordenes en el 99,8 % de los segundos, asi que la punta "al final del segundo" es fresca.
- Por segundo: desvio del delta 50, del ofi_pas 49, del ofi 62. La correlacion ofi_pas contra delta en ventanas de 10 a 300 s es +0,17 a +0,24: el pasivo es informacion casi independiente del CVD (el ofi total, en cambio, esta pegado al delta: +0,60 a +0,65).
- Ojo con los datos de confirmar: 14-09 tiene ordenes en solo 68 % de los segundos de rueda y 16-09 en 86 % (cortes de Rithmic); 03-09 (explorar) es media rueda y 17-09 termina 19:41 UTC.

### Definiciones (todas causales: en el segundo t solo filas <= t)
- `QI = (bidv - askv) / (bidv + askv)` con la punta al final del segundo. `QI_W` = media movil de QI en W segundos.
- `zP_W`, `zD_W`, `zO_W` = suma en W segundos de `ofi_pas`, `delta`, `ofi`, dividida por (desvio por segundo de los ultimos 1800 s x raiz de W). Es un puntaje z causal.
- `ret_60 = ultimo[t] - ultimo[t-60]`.
- Disparo elegible: rueda 13:32:00 a 20:00 UTC, no en los ultimos 900 s de la tabla. Desagrupado: minimo 60 s entre disparos de la misma variante, gana el primero.
- Umbrales congelados: `qi_inst 0,80` (8 % de los segundos), `qi_30 0,17` (p95 de |QI_30|), `qi_10 0,17` (p75 de |QI_10|), `mov_60 15,75 pts` (p90 de |ret_60|), y los z de abajo.

### Las 12 variantes (lado fijado de antemano)
| # | nombre | condicion en t | lado | disparos en explorar |
|---|---|---|---|---|
| P01 | qi_inst | abs(QI) >= 0,80 | signo(QI) (hacia el lado GRUESO... es decir: bid grueso = sube) | 3537 |
| P02 | qi_30 | abs(QI_30) >= 0,17 | signo(QI_30) | 1290 |
| P03 | pas_60 | abs(zP_60) >= 2 | signo(zP_60): seguir al pasivo | 544 |
| P04 | pas_300 | abs(zP_300) >= 2 | signo(zP_300) | 263 |
| P05 | div_60 | abs(zD_60) >= 1,5 y abs(zP_60) >= 1,25 con signos OPUESTOS | el del PASIVO (los limites le ganan al agresor) | 261 |
| P06 | div_300 | abs(zD_300) >= 1 y abs(zP_300) >= 1 con signos opuestos | el del pasivo | 364 |
| P07 | conf_60 | abs(zD_60) >= 1,5 y abs(zP_60) >= 1,5 con el MISMO signo | ese signo (continuacion) | 632 |
| P08 | delta_con_pas300 | abs(zD_60) >= 2 y zP_300 a favor del delta >= 1 | el del delta | 427 |
| P09 | delta_contra_pas300 | abs(zD_60) >= 2 y zP_300 en contra del delta <= -1 | CONTRA el delta (gana el pasivo lento) | 185 |
| P10 | engrosa_contra_mov | abs(ret_60) >= 15,75 y QI_10 en contra del movimiento (>= 0,17) | contra el movimiento (rebote) | 568 |
| P11 | engrosa_a_favor_mov | abs(ret_60) >= 15,75 y QI_10 a favor del movimiento (>= 0,17) | a favor del movimiento | 596 |
| P12 | ofi_sin_respuesta | abs(zO_60) >= 1,25 y el precio NO fue para ese lado en 60 s | el del OFI (el precio se pone al dia) | 228 |

Nota sobre zD: el delta acumulado tiene colas gordas (p90 de |zD_60| = 2,6), asi que "abs(zD_60) >= 2" es delta FUERTE (17 % del tiempo), no extremo.

### Regla para elegir finalistas (solo con explorar)
1. Barrera principal +-8 pts en 600 s. Una variante califica si: n resueltos >= 150, acierto >= 55,3 % (el empate), y >= 70 % de los dias arriba de 50 %.
2. Entre las que califican, las 2 de mayor z. Como mucho 2 finalistas.
3. Si una variante sale INVERTIDA con z <= -2,5 y cumple lo demas del otro lado, puede ser finalista con el lado dado vuelta, declarandolo (cuenta como variante extra).
4. Si NINGUNA califica: se llevan igual a confirmar las 2 de mayor |z| con n >= 150 como "finalistas de consuelo", para dejar el numero escrito. Una de consuelo nunca puede pasar de PISTA.
5. Las barreras secundarias (+-5 / 300 s y +-12 / 900 s) se reportan pero no eligen.
6. En explorar tambien se corre un placebo corto (50 sorteos) para no elegir por deriva del dia.

### Estudio de microestructura (antes de las barreras, solo explorar)
Movimiento medio firmado del MID a 1, 5, 10, 30, 60, 300 y 600 s por decil de cada señal (QI, QI_10, QI_30, microprecio, zP, zO, zD en 10/30/60/300 s), con el error medido ENTRE DIAS (media de las medias diarias y su t con 12 dias), que es lo unico honesto con datos tan autocorrelacionados. Y la tabla cruzada delta x pasivo (terciles) para la pregunta "cuando los limites empujan contra los agresores, quien gana".

### Placebo y veredicto (confirmar, una sola vez)
Placebo: misma cantidad de disparos en segundos elegibles al azar de la misma sesion y la misma media hora de reloj, con el mismo espaciado minimo y los mismos lados; 200 sorteos. SOBREVIVE = acierto >= empate + 2 (57,3 % en +-8), z >= 2,5 contra placebo, >= 6 de 8 dias arriba de 50 %, n >= 100. PISTA = positivo pero falla un criterio. NADA = lo demas.

---
## 1. Microestructura (explorar, 260.628 segundos de rueda, 12 dias; `punta_02_micro.py`)

Movimiento medio del MID en puntos al seguir la señal en sus deciles extremos ((decil 10 - decil 1) / 2); entre parentesis la t entre dias.

| señal | 1 s | 5 s | 10 s | 30 s | 60 s | 300 s | 600 s |
|---|---|---|---|---|---|---|---|
| QI instantaneo | +0,045 (6,2) | +0,018 (0,8) | +0,012 (0,4) | -0,06 (-0,9) | -0,18 (-1,5) | -0,39 (-1,4) | -0,26 (-1,1) |
| QI media 30 s | -0,003 (-0,4) | -0,07 (-2,0) | -0,19 (-2,2) | -0,48 (-2,3) | -0,70 (-1,9) | -1,63 (-1,7) | -1,01 (-1,0) |
| microprecio - mid | +0,047 (6,6) | +0,043 (2,3) | +0,072 (2,4) | +0,02 (0,6) | -0,05 (-0,5) | -0,19 (-1,0) | +0,08 (0,5) |
| microprecio - ultimo | +0,060 (12,5) | +0,095 (5,9) | +0,142 (4,6) | +0,17 (2,9) | +0,21 (3,7) | +0,17 (1,1) | +0,09 (0,5) |
| ofi_pas 60 s (zP60) | +0,013 (1,2) | +0,03 (0,6) | +0,06 (0,7) | +0,14 (0,9) | +0,03 (0,1) | +0,22 (0,2) | +1,41 (1,3) |
| ofi_pas 300 s (zP300) | -0,002 | -0,002 | +0,01 | +0,03 (0,1) | +0,01 (0,0) | +0,81 (0,5) | +2,75 (1,6) |
| ofi total 60 s (zO60) | -0,015 (-2,0) | -0,06 (-1,9) | -0,09 (-1,6) | -0,19 (-1,3) | -0,44 (-1,5) | -1,05 (-1,6) | -0,14 (-0,1) |
| delta 30 s (zD30) | -0,004 | -0,04 (-1,7) | -0,10 (-1,9) | -0,46 (-3,3) | -0,71 (-5,5) | -1,38 (-2,9) | -0,42 (-0,5) |
| delta 60 s (zD60) | -0,019 (-4,1) | -0,10 (-4,2) | -0,16 (-3,6) | -0,50 (-4,6) | -0,69 (-5,0) | -1,23 (-2,1) | -0,31 (-0,3) |

Lectura:
- **El desbalance de la punta existe pero es microscopico.** Con el QI en el decil extremo el mid se mueve +0,045 pts (menos de un quinto de tick) en el segundo siguiente, y a los 5 s ya no queda nada. El medio spread es 0,22 pts y el costo del protocolo 0,85: no paga ni la entrada. La probabilidad de que el mid este arriba a 5 s pasa de 47,7 % (punta cargada al ask) a 50,1 % (cargada al bid). MNQ es de tick chico, como avisaba la literatura.
- **La punta cargada de forma PERSISTENTE (QI medio de 30 s) apunta al reves**: el precio tiende a ir para el lado FLACO en los 30 a 300 s siguientes (-0,5 a -1,6 pts, t cerca de -2). Motivo probable: mientras el precio sube, el ask se va consumiendo y atras se apilan bids nuevos; la punta cargada al bid es la HUELLA de la suba que ya paso, y hereda la reversion corta del delta.
- **El flujo pasivo acumulado (ofi_pas 10 a 300 s) no predice nada** a ningun horizonte (todas las t menores que 2). Es casi independiente del delta (correlacion +0,2) pero esa informacion extra no anticipa el precio.
- **Lo unico fuerte de la tabla ya era conocido**: despues de delta fuerte en 30-60 s el mid REVIERTE 0,5 a 0,7 pts en el minuto siguiente (t = -5) y 1,2 a 1,4 en cinco minutos. Es real pero mide menos que el costo (0,85).

"Los limites contra los agresores, quien gana" (delta fuerte |zD60| >= 1,5; movimiento del mid en la direccion del delta):

| que hace el pasivo | segundos | 30 s | 60 s | 300 s | 600 s |
|---|---|---|---|---|---|
| MUY en contra del delta | 2.836 | -1,43 (-2,1) | -2,16 (-2,0) | -1,75 (-0,6) | +0,68 (0,2) |
| en contra | 7.778 | -0,84 (-2,8) | -0,93 (-1,7) | -1,58 (-0,9) | -1,38 (-0,8) |
| neutro | 25.558 | -0,26 (-1,3) | -0,41 (-2,1) | -0,53 (-0,7) | +0,34 (0,3) |
| a favor | 19.515 | -0,10 (-0,5) | +0,11 (0,3) | -1,06 (-2,2) | -0,06 (-0,1) |
| MUY a favor | 12.496 | -0,96 (-2,6) | -1,55 (-2,3) | -2,56 (-1,4) | -0,58 (-0,3) |

Gana la reversion en los DOS extremos: tanto si el pasivo empuja muy en contra del delta (-2,2 pts a 60 s) como muy a favor (-1,6). No hay "confluencia que confirma": cuando agresores y limites empujan juntos con fuerza, el minuto siguiente tambien devuelve. A 600 s no queda nada en ninguna fila.

Punta flaca = el precio viaja mas (NO direccion): |movimiento del mid a 60 s| con punta flaca 6,9 / 7,4 / 10,4 pts (mercado calmo / medio / movido) contra 4,9 / 5,2 / 6,3 con punta gruesa. Con spread >= 0,75 el movimiento medio a 60 s es 11,7 pts contra 6,3 con spread 0,25.

> CORRECCION (seccion 6, L4): esa diferencia era casi toda LA HORA DEL DIA (en la apertura y el cierre la punta es flaca y el precio se mueve mas, las dos cosas a la vez). Comparando la profundidad contra la de su MISMA media hora, la punta flaca da apenas 3 % mas de movimiento en explorar y 16 % en confirmar. No sirve como aviso.
> CORRECCION (seccion 6, L6): la fila "MUY a favor" NO se repitio en confirmar (-1,55 paso a +0,60). Solo la fila "MUY en contra" mantuvo el signo (-2,2 y -1,7), sin llegar a ser significativa.

## 2. Las 12 variantes en explorar (`punta_03_explorar.py`, 12 sesiones, placebo corto de 50 sorteos)

Barrera principal +-8 pts / 600 s (empate 55,3 %):

| variante | disparos | acierto | z | placebo | z vs placebo | dias arriba | pts netos |
|---|---|---|---|---|---|---|---|
| P01 qi_inst | 3537 | 51,2 | +1,48 | 50,1 | +1,3 | 9 de 12 | -0,65 |
| P02 qi_30 | 1290 | 46,8 | -2,32 | 50,6 | -2,9 | 2 de 12 | -1,37 |
| P03 pas_60 | 544 | 53,0 | +1,42 | 51,3 | +0,8 | 7 de 12 | -0,13 |
| P04 pas_300 | 263 | 53,1 | +0,99 | 54,5 | -0,5 | 6 de 12 | -0,38 |
| P05 div_60 | 261 | 53,6 | +1,18 | 47,8 | +2,1 | 7 de 12 | -0,27 |
| P06 div_300 | 364 | 50,7 | +0,26 | 46,7 | +1,6 | 5 de 12 | -0,87 |
| P07 conf_60 | 632 | 49,8 | -0,08 | 52,8 | -1,5 | 6 de 12 | -0,86 |
| P08 delta_con_pas300 | 427 | 47,3 | -1,11 | 55,1 | -3,6 | 4 de 12 | -1,28 |
| P09 delta_contra_pas300 | 185 | 50,5 | +0,15 | 50,1 | +0,1 | 5 de 12 | -0,79 |
| P10 engrosa_contra_mov | 568 | 54,6 | +2,18 | 48,2 | +2,7 | 8 de 12 | -0,12 |
| P11 engrosa_a_favor_mov | 596 | 48,1 | -0,94 | 51,9 | -1,8 | 3 de 12 | -1,18 |
| P12 ofi_sin_respuesta | 228 | 51,3 | +0,40 | 47,6 | +1,0 | 6 de 12 | -0,64 |

Secundarias: en +-5/300 s la mejor es P09 con 55,1 % (empate 58,5 %); en +-12/900 s P03 llega a 54,6 % (empate 53,5 %, z +2,1 pero z contra placebo +1,0 y 8 de 12 dias). Ninguna se acerca a su empate con consistencia. La salida completa esta en la consola del script.

**Ninguna variante califica**: ninguna llega al empate de 55,3 % en la barrera principal. Las doce pierden plata netas de costo (-0,12 a -1,37 pts por operacion).

## 3. Finalistas (decidido ANTES de correr confirmar)

Por la regla 4 van dos finalistas DE CONSUELO (tope de veredicto: PISTA), las de mayor |z| en la barrera principal:
1. **P02_qi_30 INVERTIDA** (z = -2,32 -> dada vuelta 53,2 %, 10 de 12 dias, z contra placebo +3,0 con 200 sorteos): punta cargada 30 s hacia un lado -> operar para el OTRO lado.
2. **P10_engrosa_contra_mov** (54,6 %, z +2,18, 8 de 12 dias, z contra placebo +2,7): tras un movimiento de >= 15,75 pts en 60 s, con la punta de los ultimos 10 s cargada en contra del movimiento -> rebote.

P08 tiene el z contra placebo mas grande (-3,6) pero es el mismo efecto ya conocido (el delta fuerte revierte) y su acierto invertido (52,7 %) queda lejos del empate; no entra por la regla escrita (|z| contra 50 % = 1,1).

## 4. CONFIRMACION (una sola vez; `punta_04_confirmar.py`, 8 sesiones del 08-09 al 17-09, placebo de 200 sorteos)

| finalista | barrera | disparos | n resueltos | acierto | empate | z | placebo | z vs placebo | dias arriba | peor dia | pts netos |
|---|---|---|---|---|---|---|---|---|---|---|---|
| P02 qi_30 INVERTIDA | +-8/600 s | 975 | 821 | 51,3 % | 55,3 | +0,73 | 50,1 +- 1,65 | **+0,75** | 5 de 8 | 47,3 | -0,67 |
| P02 qi_30 INVERTIDA | +-5/300 s | 975 | 820 | 51,2 % | 58,5 | +0,70 | 50,1 +- 1,62 | +0,68 | 6 de 8 | 42,3 | -0,74 |
| P02 qi_30 INVERTIDA | +-12/900 s | 975 | 798 | 50,8 % | 53,5 | +0,42 | 49,6 +- 1,74 | +0,67 | 4 de 8 | 44,0 | -0,75 |
| P10 engrosa_contra_mov | +-8/600 s | 304 | 303 | 49,8 % | 55,3 | -0,06 | 49,1 +- 2,87 | **+0,25** | 3 de 8 | 37,2 | -0,88 |
| P10 engrosa_contra_mov | +-5/300 s | 304 | 302 | 50,0 % | 58,5 | 0,00 | 49,5 +- 2,89 | +0,16 | 3 de 8 | 41,2 | -0,89 |
| P10 engrosa_contra_mov | +-12/900 s | 304 | 303 | 50,8 % | 53,5 | +0,29 | 48,1 +- 2,90 | +0,92 | 4 de 8 | 44,1 | -0,65 |

Por dia (+-8/600 s): P02 inv. 49 / 47 / 53 / 51 / 57 / 49 / 54 / 51 %. P10 62 / 49 / 56 / 37 / 47 / 50 / 44 / 53 %.

**Veredicto: NADA las dos.** Lo que en explorar parecia 53-55 % (z contra placebo +2,7 a +3,0) en confirmar es una moneda (z contra placebo +0,25 y +0,75). Las dos pierden plata netas de costo (-0,67 y -0,88 pts por operacion).

## 5. Hallazgo de DATOS que le sirve a todas las familias (`punta_05_diagnostico.py`)

P02 tuvo 154 disparos sin resolver en confirmar (en explorar, 5). Causa: **huecos de datos por los cortes de Rithmic: el 14-09 el 30,0 % de los segundos de rueda y el 16-09 el 13,1 % tienen menos de 5 ordenes en los 30 s previos.** En un hueco la tabla de 1 s rellena hacia adelante: el precio queda plano y la punta CONGELADA, asi que cualquier rasgo de nivel (QI, punta, spread) queda clavado en un valor y dispara en falso. 117 de los 207 disparos de P02 del 14-09 y 44 de los 132 del 16-09 cayeron en huecos. Sacandolos, P02 invertida da 50,1 % (n 796): sigue siendo NADA, el veredicto no cambia. Recomendacion para cualquiera que use `base.py`: excluir los segundos con `n.rolling(30).sum() < 5`.

## 6. Los aprendizajes, medidos por separado en explorar y en confirmar (`punta_06_replica.py`, sin huecos)

| aprendizaje | explorar (12 dias) | confirmar (8 dias) | se repite? |
|---|---|---|---|
| L1 QI instantaneo -> mid a 1 s | +0,046 pts (t +6,9; 12/12 dias) | +0,034 (t +6,5; 8/8 dias) | SI, en los 20 dias. Pero es un sexto de tick |
| L1 QI instantaneo -> mid a 5 s | +0,024 (t +1,1) | +0,051 (t +6,4) | mas o menos; igual es un quinto de tick |
| L2 microprecio - ultimo -> mid a 10 s | +0,142 (t +4,6) | +0,055 (t +2,5) | se achica a un tercio |
| L2 idem a 60 s | +0,205 (t +3,7) | +0,011 (t +0,2) | NO |
| L3 delta fuerte 60 s -> reversion a 60 s | -0,68 (t -5,0; 12/12 dias) | -0,16 (t -0,4; 6/8 dias) | el signo si, el tamaño NO (un cuarto) |
| L5 QI medio 30 s -> al reves a 30 s | -0,44 (t -2,3) | -0,17 (t -1,5) | signo si, debil |
| L6 delta fuerte + pasivo MUY en contra -> mid a 60 s | -2,16 (t -2,0) | -1,67 (t -1,2) | mismo signo y tamaño, nunca significativo |
| L6 delta fuerte + pasivo MUY a favor -> mid a 60 s | -1,55 (t -2,3) | +0,60 (t +1,2) | NO, se dio vuelta |
| L4 punta flaca -> mas movimiento (misma media hora) | x1,03 (8/12 dias) | x1,16 (8/8 dias) | casi nada una vez que se saca la hora |

Moraleja de metodo: hasta el efecto mas fuerte de explorar (L3, t = -5 con 12 de 12 dias) se achico a un cuarto en las 8 sesiones siguientes. En este mercado una t de 5 en doce dias NO alcanza para creerle al tamaño de un efecto.

## 7. Exploratorio fuera del pre-registro (NO cuenta para el veredicto)

- **Pasivo LENTO, "CVD de los limites"** (`punta_07_lento.py`; ofi_pas acumulado 15 y 30 min contra el mid a 10 y 30 min, las 20 sesiones): nada. P900 -> 600 s da +5,9 pts en explorar y -2,8 en confirmar. El anclado a la apertura no se puede medir bien con este diseño (cada dia cae entero en un extremo).
- **Los limites defienden el nivel de gamma?** (`punta_08_niveles.py`, SOLO explorar, no se corrio en confirmar): precio a <= 5 pts de zero / dominantes / majors, rebote con barrera +-8/600 s. Todos: 50,7 % (n 1472). Con el pasivo defendiendo (zP60 >= 1,25): 50,5 % (n 402). Con el pasivo en contra: 49,3 %. Con la punta cargada a favor del rebote: 49,1 %. La defensa de un nivel NO se delata en la punta ni en el flujo pasivo de 60 s.
- **Sirve para leer el PRESENTE?** (`punta_09_presente.py`): si. El delta solo explica 66-71 % del movimiento de la vela actual de 30-60 s; delta + ofi_pas explica 77-79 % (en confirmar 63-68 % -> 76-78 %). Es el unico resultado estable de la familia ademas de L1: unos 10 puntos del movimiento actual los explican los limites que se corren, y eso el CVD no lo ve. Pero la vela siguiente devuelve lo mismo (4-5 centavos por punto) venga el movimiento de los agresores o de los limites: no hay lectura predictiva ahi.

## 8. Que significa para el scalper (en criollo)

Imaginate la punta del libro como la fila de la caja de un kiosco: 7 personas de cada lado. En ES la fila es de cientos y por eso mirar "de que lado hay mas gente" avisa para donde sale el proximo tick. En MNQ la fila es tan corta que se arma y se desarma varias veces por segundo: avisa el proximo SEGUNDO (se repitio en los 20 dias, eso es real) pero lo que avisa es un sexto de tick, y a los 5 segundos ya no queda nada. El costo de entrar y salir es 0,85 pts: veinte veces mas que la ventaja.

- La punta y el flujo pasivo NO son el gatillo que busca. Ninguna de las 12 formas de mirarlos llego al empate, y las dos mejores se volvieron moneda en la confirmacion.
- Lo que SI deja: cuando el precio se mueve y el delta no acompaña, no es magia: son los limites que se corrieron (eso explica ~10 puntos de cada 100 del movimiento actual). Sirve para no confundirse leyendo la vela que esta pasando; no para anticipar la que viene.
- "Punta cargada al bid = va a subir" es falso a la escala del scalper. Sostenida 30 s apunta apenas al reves (es la huella de la suba que ya paso).
- "Los limites le ganan a los agresores" (delta fuerte contra pasivo fuerte): el minuto siguiente devolvio unos 2 pts en las dos mitades de la muestra, pero nunca significativo y no llega a la barrera (P05: 53,6 % en explorar, bajo el empate). Queda anotado, no mas que eso.

## 9. Si se quisiera seguir por este lado (sin prometer nada)

La literatura dice que en activos de tick chico la informacion no esta en la PUNTA sino en VARIOS niveles del libro (desbalance de 5 a 10 niveles). Eso hoy no se graba: la sonda solo guarda el mejor bid/ask alrededor de cada orden. Es la unica pregunta de esta familia que queda abierta, y necesita datos nuevos (profundidad de MNQ a 1 Hz desde el mismo grafico, NO de opciones), no mas vueltas sobre estos.
