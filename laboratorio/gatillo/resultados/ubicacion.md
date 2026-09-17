# Familia "ubicacion" — DONDE pasa x QUE pasa (17-09-2026)

Hipotesis de practica profesional: el flujo (delta, absorcion, pasivo, barridos) solo dice algo cuando el precio LLEGA a un lugar que
otros tambien miran. Se prueba con la barrera del scalper, neta de costo, contra dos placebos.

## PRE-REGISTRO (escrito ANTES de correr nada que mire resultados)

Datos: tablas de 1 s de MNQ (base.py corregido, 17-09 17:40). Explorar = 12 sesiones (20-08 al 04-09). Confirmar = 8 (08-09 al 17-09).
La reserva no se toca. Todo rasgo y todo nivel en el segundo t usa solo filas <= t (los niveles que cambian con el tiempo se usan con
1 s de atraso: el valor conocido al terminar t-1).

### Lugares (niveles), todos causales
Grupo REF (referencias que mira todo el mundo):
  - VWAP de la rueda (desde 13:30:00 UTC, ponderado por volumen, precio = ultimo de cada segundo) y sus bandas +-1 y +-2 desvios
    (desvio ponderado por volumen, acumulado). Validos desde 13:45:00.
  - PDH / PDL / PDC: maximo, minimo y ultimo precio (19:59:59) de la rueda 13:30-20:00 de la sesion anterior de la tabla.
    La primera sesion (20-08) no tiene (velas_m1 tampoco cubre el 19-08): queda sin esos tres niveles.
  - ONH / ONL: maximo y minimo de la noche de la propia sesion (22:00 -> 13:29:59 UTC).
  - ORH15 / ORL15 (13:30-13:44:59, validos desde 13:45) y ORH30 / ORL30 (13:30-13:59:59, validos desde 14:00).
Grupo MOV (extremos moviles): MAX30, MAX60 = maximo de `alto` en [t-1800, t-1] y [t-3600, t-1]; MIN30, MIN60 idem con `bajo`.
  MAX solo se toca desde abajo y MIN solo desde arriba (es el "retest" del extremo despues de un retroceso).
Grupo RED (redondos): todos los multiplos de 50 puntos (los de 100 incluidos).

### Llegada a un lugar ("toque")
Toque DESDE ABAJO del nivel L en el segundo t (dir = +1): alto[t] >= L[t] - 0,5 y alto[t-1] < L[t] - 0,5 (primer segundo en que entra a la
zona de 2 ticks), y ademas el precio VINO de lejos: existe un segundo tau en [t-900, t-1] con ultimo[tau] <= L[t] - 8 y desde tau hasta
t-1 el `alto` nunca llego a L[t] - 0,5. Desde ARRIBA (dir = -1) es el espejo con `bajo`. Referencia de entrada: ultimo[t].
REBOTE = apostar contra la llegada (lado = -dir). RUPTURA = apostar a favor (lado = +dir).
Segundos validos: rueda 13:30-20:00 UTC sin los 2 primeros minutos, sin `hueco` entre t-600 y t+900, y con 900 s de tabla por delante.
Si en el mismo segundo se tocan varios niveles es UN evento (se anota cuantos). Si hay toques de los dos lados en el mismo segundo, se descarta.
Desagrupado: minimo 60 s entre disparos de la misma variante, gana el primero.

### Estado del flujo en t (ventanas que terminan en t, inclusive)
  V30, V60, V300 = volumen; D30, D60, D300 = delta; fd30 = dir x D30 / V30, fd60, fd300 (positivo = el flujo agresor EMPUJA hacia el nivel).
  a_niv30 = contratos de absorcion limpia del lado del nivel en 30 s / V30 (abs_ask si dir=+1: compras que se comieron toda la punta y el
            ask no cedio; abs_bid si dir=-1).
  pas30   = dir x suma de ofi_pas en 30 s / profundidad (media de rbidv + raskv de los ultimos 300 s). Negativo = el pasivo se planta
            CONTRA la llegada (suman oferta arriba / sacan demanda abajo cuando el precio sube).
  bar30   = contratos de barridos a favor de la llegada en 30 s / V30 (barre_c si dir=+1, barre_v si dir=-1).
Umbrales: cuantiles de esos rasgos calculados sobre los toques de EXPLORAR del conjunto TODO (REF + MOV + RED), sin mirar ningun
resultado; los congela `ubicacion_01_conteo.py` en `ubicacion_umbrales.json` y se anotan abajo antes de correr la exploracion.

### Las 12 variantes
Solo lugar (sin flujo):
  1. REF_toque_R   todo toque de un nivel REF -> rebote.
  2. MOV_toque_R   todo toque de MAX30/MAX60/MIN30/MIN60 -> rebote (el espejo es "seguir la ruptura").
  3. RED_toque_R   todo toque de un multiplo de 50 -> rebote.
  4. CONF_R        toque (cualquier grupo) con CONFLUENCIA: al menos otro nivel de precio distinto a 3 puntos o menos del tocado -> rebote.
Lugar x flujo, sobre TODO (REF + MOV + RED; el desglose por grupo es solo descriptivo):
  5. ABS_R         a_niv30 >= cuantil 67 y fd30 > 0 (empujan contra el nivel y los absorben) -> rebote.
  6. ABS_PAS_R     lo de 5 y ademas pas30 < 0 (el pasivo se planta contra la llegada) -> rebote.
  7. SINABS_B      a_niv30 <= cuantil 33 y fd30 > 0 (empujan y nadie defiende) -> ruptura.
  8. EMPUJE_B      fd60 >= cuantil 67 y bar30 >= cuantil 67 (flujo fuerte con barridos hacia el nivel) -> ruptura.
  9. AGOTA_R       fd60 <= cuantil 33 y fd60 < 0 (llega al nivel con el delta de 60 s EN CONTRA de la llegada: sin nafta) -> rebote.
 10. PAS_sigue     |pas30| >= cuantil 67 de |pas30| -> lado = signo del pasivo en precio (pas30 < 0: rebote; pas30 > 0: ruptura).
 11. MOV_div_R     solo toques MOV: fd300 < 0 y |fd300| >= mediana de |fd300| (retest del extremo con el CVD de 5 min en contra) -> rebote.
 12. MODELO        regresion logistica L2 (C = 1, rasgos estandarizados con explorar) sobre los toques de TODO resueltos a +-8/600;
                   objetivo = rebote. Rasgos: grupo (REF, MOV), confluencia (0/1), fd30, fd60, fd300, a_niv30, absorcion del lado opuesto/V30,
                   pas30, bar30, barridos en contra/V30, rompe del lado del nivel/V30, velocidad de llegada (dir x [ultimo[t]-ultimo[t-60]]
                   y lo mismo a 300 s), minutos desde 13:30, distancia al VWAP en desvios x dir.
                   En explorar se juzga con predicciones dejando un DIA afuera por vez. Disparo: quintil mas alto de probabilidad -> rebote,
                   quintil mas bajo -> ruptura. Para confirmar: modelo ajustado con todo explorar y los cortes de quintil de esas predicciones.

### Barreras y medidas
Principal +-8 / 600 s. Secundarias +-5 / 300 y +-12 / 900. Empates con B.empate(x) (costo 0,96): 56,0 / 59,6 / 54,0 %.
Descriptivo: movimiento medio firmado a 10/30/60 s, segundos hasta resolver, desglose por grupo y por nivel (sin elegir nada de ahi).

### Dos placebos
  a) PLACEBO DE LUGAR (descriptivo, para TODAS las variantes 5-10 y las de solo lugar): la misma definicion de toque y las mismas condiciones
     de flujo sobre una grilla de precios sin significado: multiplos de 50 corridos 13, 21 y 37 puntos (tres replicas), descartando los toques
     que tengan un nivel real a 3 puntos o menos. Contesta la pregunta de la familia: el flujo dice mas EN el lugar que en cualquier otro precio?
  b) PLACEBO DEL PROTOCOLO (para cada finalista, en confirmar): misma cantidad de disparos en segundos al azar de la misma sesion y la misma
     media hora, con los mismos lados; 200 sorteos -> media, desvio, z.

### Regla para elegir finalistas (como mucho 2)
Sobre +-8/600 en explorar. Una variante (o su espejo: el lado queda congelado) es ELEGIBLE si: n resuelto >= 150, acierto >= 56,0 % (empate)
y >= 70 % de los dias arriba de 50 %. Entre las elegibles, las 2 de mayor z. Si ninguna es elegible se llevan igual a confirmar las 2 de mayor
|z| con n >= 150 (para documentar), pero su veredicto maximo es PISTA. Nada se retoca despues de ver confirmar.

### Veredicto por finalista (en confirmar)
SOBREVIVE: acierto >= empate + 2 puntos (58,0 %), z >= 2,5 contra el placebo (b), >= 6 de 8 dias arriba de 50 %, n >= 100.
PISTA: positivo pero falla un criterio. NADA: el resto.

---

## ENMIENDA AL PRE-REGISTRO (hecha despues del CONTEO y ANTES de mirar ningun resultado)

`ubicacion_01_conteo.py` cuenta toques y reparte rasgos; no calcula barreras ni mira el precio futuro. Mostro tres cosas que obligan a
corregir definiciones (si no, dos variantes quedaban sin muestra por un defecto de diseño, no por el mercado):
  - pas30 casi nunca es negativo (22 % de los toques; mediana +15,6): cuando el precio sube hacia un nivel, el pasivo de la punta lo ACOMPAÑA
    por construccion (los bids suben de escalon). "Pasivo en contra" en valor absoluto casi no existe; hay que medirlo en RELATIVO.
  - En el retest de un maximo/minimo de 30-60 min el delta de 5 min acompaña el 89 % de las veces (fd300 < 0 solo 11 %; en contra y fuerte, 0,6 %):
    la "divergencia del CVD en el maximo" con signo casi no existe como evento (5 disparos en 12 dias). Tambien va en relativo.
  - La absorcion limpia del lado del nivel es minima: mediana 0,12 % del volumen de esos 30 s y 32 % de los toques con cero.
Cambios (los numeros de variante no cambian):
  6. ABS_PAS_R  = lo de 5 y pas30 <= cuantil 33 de pas30 (el pasivo acompaña POCO o va en contra) -> rebote.
 10. PAS_sigue  = pas30 <= cuantil 33 -> rebote; pas30 >= cuantil 67 -> ruptura (terciles de pas30, ya no de |pas30|).
 11. MOV_div_R  = solo toques MOV con fd300 <= cuantil 33 de fd300 entre los toques MOV (el retest con MENOS flujo de 5 min detras) -> rebote.
Tambien cosmetico: las bandas se llaman VWAPa1/VWAPa2 (arriba) y VWAPb1/VWAPb2 (abajo).

Umbrales congelados (toques de explorar, conjunto TODO; `ubicacion_umbrales.json`):
  a_niv30: q33 = 0,00036, q67 = 0,00208 | fd60: q33 = +0,0142, q67 = +0,1184 | bar30: q67 = 0,2512 | pas30: q33 = +6,50, q67 = +24,58 |
  fd300 entre toques MOV: q33 = +0,0353.

Conteo en explorar (12 sesiones, sin resultados): 3.605 toques (REF 1.853, MOV 783, RED 1.167; con confluencia 987; 221 a 458 por dia).
Disparos desagrupados por variante: REF_toque_R 1.087 · MOV_toque_R 627 · RED_toque_R 814 · CONF_R 536 · ABS_R 661 · ABS_PAS_R 213 ·
SINABS_B 795 · EMPUJE_B 559 · AGOTA_R 662 · PAS_sigue 1.253 · MOV_div_R 227. Placebo de lugar: 838 / 776 / 726 toques (corrimientos 13 / 21 / 37).

---

## EXPLORACION (12 sesiones, `ubicacion_02_explorar.py`, corrida una vez; 11 s de maquina)

3.605 toques; el 99,9 % resuelve la barrera +-8 en 600 s. Empates: +-8 56,0 % | +-5 59,6 % | +-12 54,0 %.
Columnas: disparos desagrupados · acierto +-8/600 (z contra 50 %, dias arriba de 50 %, peor dia, t entre dias) · acierto +-5/300 · acierto +-12/900 ·
movimiento medio a favor del lado a 10/30/60 s en puntos · mediana de segundos hasta resolver el +-8.

| variante | disp. | +-8 acierto | z | dias | peor dia | t dias | +-5 | +-12 | mov 10/30/60 s | seg |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 REF_toque_R | 1.087 | 53,0 % | +2,00 | 8 de 12 | 41,0 | +1,72 | 51,2 | 54,6 | +0,39 / +0,72 / +1,44 | 27 |
| 2 MOV_toque_R | 627 | 52,3 % | +1,16 | 7 de 12 | 40,4 | +0,92 | 53,1 | 51,9 | +0,46 / +0,72 / +1,09 | 37 |
| 3 RED_toque_R | 814 | 48,2 % | -1,02 | 5 de 12 | 36,5 | -0,81 | 49,8 | 50,9 | +0,15 / +0,04 / +0,38 | 21 |
| 4 CONF_R | 536 | 50,2 % | +0,09 | 5 de 12 | 33,3 | +0,04 | 49,9 | 51,2 | +0,35 / +0,33 / +0,71 | 32 |
| 5 ABS_R | 661 | 49,3 % | -0,35 | 7 de 12 | 39,4 | -0,25 | 48,9 | 49,7 | +0,37 / -0,01 / +0,14 | 28 |
| 6 ABS_PAS_R | 213 | 55,0 % | +1,45 | 7 de 12 | 41,2 | +1,38 | 51,0 | 52,6 | +0,52 / +1,24 / +1,51 | 22 |
| 7 SINABS_B | 795 | 46,4 % | -2,02 | 3 de 12 | 32,0 | -1,88 | 47,0 | 46,4 | -0,36 / -0,43 / -0,97 | 37 |
| 8 EMPUJE_B | 559 | 52,4 % | +1,14 | 8 de 12 | 38,5 | +1,19 | 50,0 | 48,7 | -0,59 / -0,49 / -0,68 | 34 |
| 9 AGOTA_R | 662 | 50,5 % | +0,23 | 5 de 12 | 41,9 | +0,42 | 52,0 | 52,4 | +0,40 / +0,37 / +0,65 | 18 |
| 10 PAS_sigue | 1.253 | 54,0 % | +2,86 | 9 de 12 | 43,2 | +2,46 | 51,5 | 53,5 | +0,32 / +0,37 / +0,49 | 24 |
| 11 MOV_div_R | 227 | 53,1 % | +0,93 | 7 de 12 | 33,3 | +0,61 | 50,2 | 52,2 | +0,11 / +0,82 / +0,73 | 42 |
| 12 MODELO (dia afuera) | 884 | 50,5 % | +0,27 | 6 de 12 | 36,0 | +0,34 | 48,6 | 53,2 | +0,02 / +0,32 / +0,71 | 24 |

Placebo de LUGAR (grilla de multiplos de 50 corrida 13/21/37 puntos, mismas reglas, 2.340 toques):
todos los toques -> rebote 50,0 % (n 1.649; el real, todos los niveles juntos: 52,2 %, n 1.762, z +1,86) · ABS_R 50,9 % · ABS_PAS_R 58,1 % (n 148) ·
SINABS_B 47,7 % · EMPUJE_B 50,4 % · AGOTA_R 47,6 % · PAS_sigue 50,4 % (n 1.153) · MODELO 50,7 %.

Lectura de la exploracion:
  - NINGUNA variante llega al empate de 56,0 % con n >= 150. La mejor es PAS_sigue con 54,0 %: no paga el costo.
  - La absorcion en el nivel da AL REVES de lo que dice el manual: con absorcion del lado del nivel el rebote es 49,3 %; SIN absorcion, 53,6 %
    (espejo de SINABS_B). Y en precios sin significado pasa lo mismo (52,3 % sin absorcion, 50,9 % con): no es cosa del lugar.
  - ABS_PAS_R (el ejemplo del pedido: absorcion + pasivo plantado) dio 55,0 % en los niveles... y 58,1 % en la grilla placebo. No es del lugar.
  - El modelo con todo junto (16 rasgos, dejando un dia afuera) da 50,5 %: combinando lugar y flujo no aparece nada que generalice ni de un dia a otro.
  - Por nivel (descriptivo, 20 niveles = 20 oportunidades de que el azar de un 'hallazgo'): VWAPb1 59,0 % (10 de 12 dias), ONH 63,9 % (n 72, 3 de 5),
    R50 44,3 %, PDH 39,2 % (n 51). No se elige nada de aca.

### Finalistas segun la regla escrita
Ninguna variante es ELEGIBLE (todas por debajo de 56,0 %). Se aplica la clausula: van a confirmar, para documentar, las 2 de mayor |z| con n >= 150,
con veredicto maximo PISTA:
  F1 = PAS_sigue (z +2,86): pas30 <= +6,50 -> rebote; pas30 >= +24,58 -> ruptura.
  F2 = SINABS_B en ESPEJO (z -2,02 -> +2,02; le gana por un pelo a REF_toque_R, +2,00): a_niv30 <= 0,00036 y fd30 > 0 -> REBOTE. Lado congelado.

---

## CONFIRMACION (8 sesiones, 08-09 al 17-09; `ubicacion_03_confirmar.py`, corrida UNA vez; salida cruda en `ubicacion_cache/confirmar_salida.txt`)

Antes se ensayo el MISMO script sobre explorar (variable UBIC_ENSAYO=1) para no gastar la unica corrida en un error de tipeo. 2.086 toques en confirmar.

### F1 = PAS_sigue (pas30 <= +6,50 -> rebote; pas30 >= +24,58 -> ruptura) — 741 disparos
  +-8/600 : n 738 · acierto 44,9 % (empate 56,0) · z contra 50 % = -2,80 · dias arriba 2 de 8 · peor dia 37,9 % · placebo del protocolo 50,0 +- 1,9 % -> z = -2,69 · neto -1,78 pts por operacion
  +-5/300 : n 740 · 49,5 % (empate 59,6) · z -0,29 · 5 de 8 · placebo 49,8 +- 1,8 -> z -0,14 · neto -1,02
  +-12/900: n 730 · 47,0 % (empate 54,0) · z -1,63 · 3 de 8 · placebo 50,3 +- 1,9 -> z -1,75 · neto -1,66
  Por rama: rebote (pasivo flojo) 49,5 % (n 368, 4 de 8 dias) · ruptura (pasivo fuerte) 40,3 % (n 370, 1 de 8 dias).
  Por dia: 51 · 53 · 38 · 45 · 38 · 50 · 45 · 39 %. Movimiento medio a favor a 10/30/60 s: -0,23 / -0,50 / -0,26 pts.
  Placebo de lugar (misma regla en precios sin significado): 50,6 % (n 705).
  En explorar habia dado 54,0 % (z +2,86, 9 de 12 dias, neto -0,31). Se dio VUELTA. **VEREDICTO: NADA.**

### F2 = SINABS_B en espejo (a_niv30 <= 0,00036 y fd30 > 0 -> REBOTE) — 570 disparos
  +-8/600 : n 566 · acierto 50,0 % (empate 56,0) · z 0,00 · dias arriba 4 de 8 · peor dia 44,3 % · placebo del protocolo 47,9 +- 2,0 % -> z = +1,03 · neto -0,98 pts por operacion
  +-5/300 : n 569 · 50,4 % (empate 59,6) · z +0,21 · 4 de 8 · placebo 48,8 +- 2,1 -> z +0,77 · neto -0,92
  +-12/900: n 557 · 51,7 % (empate 54,0) · z +0,81 · 4 de 8 · placebo 47,2 +- 2,0 -> z +2,24 · neto -0,53
  Por dia: 59 · 45 · 51 · 45 · 47 · 58 · 44 · 54 %. Movimiento medio a favor a 10/30/60 s: -0,02 / -0,02 / +0,10 pts.
  Placebo de lugar: 55,2 % (n 422): la misma regla anduvo MEJOR en precios sin significado que en los niveles.
  En explorar habia dado 53,6 % (z +2,02, 8 de 12 dias, neto -0,39). **VEREDICTO: NADA.**

Contexto: todos los toques -> rebote, en confirmar: 51,4 % (n 1.044, 4 de 8 dias).

## VEREDICTO DE LA FAMILIA: NADA

Cruzar DONDE (VWAP y bandas, maximo/minimo/cierre de ayer, noche, rango de apertura, extremos de 30/60 min, redondos) con QUE (delta 30/60/300 s,
absorcion limpia en la punta, flujo pasivo, barridos) no dio ningun gatillo. En explorar nada llego al empate (lo mejor, 54,0 %) y lo poco que
asomaba se dio vuelta o se aplano en confirmar. El modelo que junta los 16 rasgos tampoco generaliza ni de un dia al otro (50,5 %).

---

## LO QUE SE APRENDIO (descriptivo, 20 sesiones, corrido despues de confirmar: `ubicacion_04_micro.py`, `ubicacion_06_freno.py`, `ubicacion_05_posthoc.py`)

1. **El lugar solo es una moneda.** Rebote de 8 puntos antes que ruptura de 8: referencias (VWAP, ayer, noche, apertura) 51,7 % (n 2.988; 12 de 20 dias arriba
   de 50 %), extremos de 30/60 min 51,3 %, redondos 50,0 %, precios SIN significado 48,6 / 50,2 / 51,0 % (tres replicas). Hace falta 56,0 %.
2. **Un nivel en MNQ es una zona ancha, no una raya.** Despues de tocarlo, en los 2 minutos siguientes el precio lo PASA por 4 puntos o mas en el 73 % de
   los toques de referencias (78 % en un precio cualquiera) y por 8 o mas en el 55 % (61 %). Un stop de 4 puntos detras del nivel salta 3 de cada 4 veces,
   aguante o no aguante el nivel despues. La diferencia con el placebo existe (t pareada entre dias -2,8 a -3,1) pero el precio tambien DEVUELVE menos
   en los niveles (65 % contra 68 %): es cinta mas tranquila de los dos lados, no un freno que de ventaja.
3. **El delta "confirma" la llegada casi siempre, asi que no informa.** Al tocar un nivel, el delta de 30 s va a favor de la llegada en el 78 % de los toques
   (referencias), 89 % en los retests de maximos/minimos... y 72 % en un precio cualquiera. El pasivo de la punta tambien acompaña (77-82 %). La famosa
   "divergencia del CVD en el maximo" a 5 minutos casi no existe: 11 % de los retests, y fuerte 0,6 % (5 casos en 12 dias).
4. **La absorcion real en la punta no separa rebote de ruptura.** Es minima (mediana 0,10 % del volumen de esos 30 s; un tercio de los toques con cero). Con
   absorcion del lado del nivel el rebote fue 49,3 %; sin absorcion 53,6 % en explorar y 50,0 % en confirmar. En precios sin significado, lo mismo.
   El ejemplo del pedido (absorcion + pasivo plantado) dio 55,0 % en los niveles y 58,1 % en la grilla placebo: no es del lugar, y no tenia muestra (n 211).
5. **Al llegar a un nivel la apuesta se juega en segundos.** El +-8 se resuelve en 20 s de mediana en referencias, 17 s en redondos, 29 s en extremos moviles
   (9 s con cinta rapida, 50-70 s con cinta lenta). Desde un segundo cualquiera son 48 s. Esperar el cierre de la vela de 1 minuto en un nivel es llegar tarde.
6. **Lo lindo de explorar no repite.** Rebote por nivel, explorar -> confirmar: ONH 63,9 -> 47,4 % · MIN60 56,1 -> 48,8 · PDH 39,2 -> 46,5 · ONL 44,4 -> 58,1 ·
   VWAP 51,4 -> 45,3. Correlacion entre los dos tramos sobre los 20 niveles: 0,12.
7. **Curiosidad POST-HOC, sin validar (no es finalista ni pista del protocolo):** la banda VWAP -1 desvio fue el unico nivel parecido en los dos tramos:
   rebote 59,0 % (n 222, 10 de 12 dias) y 56,9 % (n 102, 6 de 6 dias); las 20 juntas 58,3 % (n 324; placebo del protocolo 49,2 +- 2,7 -> z 3,3); llegando desde
   arriba 55,9 %, desde abajo 61,2 %. En contra: su gemela VWAP +1 da 51,6 %, el VWAP 48,9 %, VWAP -2 no repite (58,3 -> 50,0), y con 20 niveles mirados
   uno con z ~3 por azar es esperable (p ~0,05 corregido). Regla exacta por si el coordinador quiere gastarle la reserva: toque de VWAPb1 segun el
   pre-registro (viene de >= 8 pts, zona de 0,5, desde 13:45 UTC, desagrupado 60 s) -> rebote, +-8/600.
8. **Aviso de metodo para el coordinador: el placebo del protocolo esta sesgado cuando el lado depende del movimiento reciente.** Para reglas de REBOTE da
   46-48 %, no 50 % (para las de ruptura, mas de 50): los segundos al azar de la misma media hora incluyen segundos ANTERIORES al movimiento que definio
   el lado, asi que ese placebo "conoce" la deriva con el signo cambiado. F2 tenia z +2,0 contra 50 % pero +4,6 contra ese placebo en explorar... y en
   confirmar dio 50,0 %. Para reglas de lugar el control correcto es el placebo de LUGAR (misma regla, mismo tipo de llegada, precio sin significado).

## ARCHIVOS
  ubicacion_lib.py · ubicacion_01_conteo.py · ubicacion_02_explorar.py · ubicacion_03_confirmar.py · ubicacion_04_micro.py · ubicacion_05_posthoc.py ·
  ubicacion_06_freno.py · ubicacion_umbrales.json · ubicacion_cache/ (toques por sesion, barreras, salidas crudas) · resultados/ubicacion.md
