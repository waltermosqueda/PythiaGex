# Ronda 16 — el método del trader de los reels, PRE-REGISTRO (23-11-2026)

Escrito ANTES de correr `r16_01_eventos.py` sobre resultados. Los parámetros salen de lo que él dice y muestra (análisis
privados en `_referencia`); nada se ajusta mirando el resultado. Lo que no repite en el tramo de confirmación, no existe.

## Datos
- Cinta orden por orden de ES (sonda de Flujo Claro), 49 ruedas del 08-09 al 13-11-2026, tabla de 1 s (`datos/flujo/cache/ES`).
- Niveles por día (`r16_00_niveles.py`): máximo/mínimo/cierre, POC/VAH/VAL (área de valor de ATAS: 70 %, paso 2, con huecos),
  noche 18:00-9:30 NY, balance inicial 9:30-10:30 NY.
- Tramos: EXPLORAR hasta el 09-10-2026; CONFIRMAR desde el 13-10-2026 (una sola vez).

## Regla (una sola, la suya)
1. Niveles: PDH, PDL, PDC, PPOC, PVAH, PVAL (día anterior), ONH, ONL (noche); IBH, IBL recién desde las 10:30.
2. Toque: el precio llega al nivel ±2 ticks viniendo del lado contrario (precio de 60 s antes a más de 2 ticks): desde arriba →
   largo; desde abajo → corto. Un evento por nivel cada 30 minutos. Rueda 9:30-15:55 NY.
3. Absorción (180 s desde el toque): el precio no pasa el nivel por más de 1,5 pts y el delta EN CONTRA de la ventana es ≥ el
   percentil 60 de |delta de 3 min| de las 5 ruedas anteriores.
4. Confirmación (hasta 600 s del toque): delta de 60 s A FAVOR ≥ percentil 60 de |delta de 60 s| de las 5 ruedas anteriores Y el
   precio rompe el micro-rango de la absorción.
5. Entrada en ese segundo (último precio). Stop: extremo del micro-rango + 1 tick; se opera solo si el riesgo queda entre 2 y 8 pts.
   Objetivo: el siguiente nivel a favor a ≥ 1,5 R y ≤ 4 R; si no hay, 2 R. Salida por tiempo a los 60 min o a las 15:55.
   Si stop y objetivo caen en el mismo segundo, cuenta como stop.
6. Costo ida y vuelta: 0,50 pts.
Variante pre-registrada "grandes": además, las órdenes ≥ 50 contratos netas entre el toque y la confirmación a favor.

## Contra qué se compara
- P1 rebote ciego: entrar al tocar el nivel, stop 4 pts detrás, mismo objetivo. Mide si el order flow agrega algo al nivel solo.
- P2 dirección al azar en el mismo instante de cada entrada, con las mismas distancias espejadas.

## Qué cuenta como éxito
En CONFIRMAR: media por operación > 0 después de costo con t ≥ 2 (n chico: se informa igual con su número), por encima de P1 y de P2.
Si no pasa, se dice con los números y no se construye una "señal" sobre esto; el indicador queda como lector del método, no como
gatillo.

---
# R16b — SUS reglas de 2026 (pre-registrado el 23-11-2026, DESPUES de ver R16 base: el tramo de confirmacion ya se uso una vez,
# asi que esto es exploratorio con reglas suyas; la prueba que vale es hacia adelante)
Fuente de cada regla: analisis A (metodo), B (operaciones), E (shorts), L4 (directos ago-sep 2026), en _referencia.
- Velas de 1 minuto de ES desde la cinta. Rueda 9:45-12:00 NY (no opera la apertura; sus entradas en directo: 9:49-10:08).
- Zonas: VAH y VAL de la rueda anterior (las que prefiere); POC anterior = objetivo, nunca entrada. Nada en el medio del area.
- S1 REINGRESO AL AREA: el precio cierra una vela de 1 min fuera del area (arriba de VAH o abajo de VAL) y despues una vela cierra
  de nuevo adentro con delta a favor del reingreso (ventas al volver bajo VAH / compras al volver sobre VAL) -> entra al cierre;
  stop = extremo de la excursion afuera + 1 tick; objetivo = POC anterior.
- S2 ABSORCION EN EL BORDE: vela que toca VAL (o VAH) ±1 pt con delta EN CONTRA del rebote y cierre A FAVOR (absorcion), y en
  las 3 velas siguientes una vela con delta a favor >= percentil 70 de |delta de 1 min| de las 5 ruedas previas que cierra mas alla
  del extremo de la vela de absorcion (agresion con recompensa) -> entra al cierre; stop = extremo de la vela de absorcion + 1 tick;
  objetivo = POC anterior.
- S3 VWAP: vela que toca la 2.a desviacion del VWAP de la sesion completa (desde las 18:00 NY) con absorcion (como S2) y luego
  agresion con recompensa -> objetivo el VWAP; no se opera si el objetivo queda a menos de 2 pts.
- Gestion comun: stop entre 2 y 6 pts (si no, no se opera); el objetivo tiene que quedar a >= 1,5 R, si no se usa 2 R; salida por
  tiempo a los 60 min o 15:55; maximo 2 operaciones por dia y fin del dia tras 2 stops; costo 0,50 pts ida y vuelta.
- Placebo: la operacion contraria en el mismo instante con distancias espejadas (direccion al azar = promedio de las dos).

---
# R16c — SUS gatillos de oct-nov 2026 (pre-registrado el 23-11-2026 ANTES de escribir el codigo que los mide)
# Exploratorio: las 49 ruedas ya se usaron en R16 y R16b. Lo que vale es lo que repita hacia adelante (ruedas desde el 16-11 y el
# registro del indicador). Fuente: L5 (directos 30-09 a 18-11, siete entradas cruzadas con la cinta al segundo), seccion 5.
Datos: tabla de 1 s de ES (49 ruedas, 08-09 a 13-11) + columnas nuevas por segundo desde la cinta cruda: compras y ventas de
ordenes agresivas >= 95 contratos (su filtro de big trades en ES). Niveles del plan: PVAH, PVAL, PPOC, PDH, PDL (rueda anterior,
area de valor de ATAS), ONH, ONL (18:00-9:30), OPEN (apertura 9:30), IBH/IBL (recien desde las 10:30), VWAP +-2 desv. de la sesion
completa (desde las 18:00, por segundo).
Comun: entradas de 9:35:00 a 11:59:59 NY (no opera los primeros 5 min); entra al ultimo precio del segundo que dispara; barrera
doble con alto/bajo de 1 s desde el segundo siguiente (mismo segundo = stop), 60 min o 15:55; costo 0,50. Cada gatillo se mide solo
("crudo": una operacion a la vez por gatillo) y despues todos juntos ("gestionado": una posicion a la vez, fin del dia tras 2 stops).
Objetivo "nivel siguiente" = el proximo nivel del plan a favor a >= 1,5 R y <= 4 R; si no hay, 2 R.
Plantilla challenge (la de sus 04-11): stop 8,25 pts, objetivo 7,5 pts.
- C1 ABSORCION EN EL NIVEL (28-10). Bloques de 15 s alineados (:00 :15 :30 :45). Largo: un bloque cuyo minimo cae en [N-1,5 ; N+0,5]
  de un nivel N, con el precio de 60 s antes del bloque > N+0,5 (viene de arriba), volumen >= 2 x la media de los 20 bloques previos
  y delta <= -400; en los 6 bloques siguientes (90 s) no hace un minimo mas bajo y suma 3 bloques SEGUIDOS con delta > 0 -> entra al
  cierre del 3.o. Stop 6 pts, objetivo nivel siguiente. Corto espejado.
- C2 BARRIDA Y RECUPERACION (04-11 T1). Referencias: largo OPEN, ONL, PDL, PVAL, IBL; corto OPEN, ONH, PDH, PVAH, IBH. Largo: el precio
  perfora la referencia entre 0,25 y 4 pts y, dentro de los 15 s desde el segundo del minimo, sube >= 8 pts desde ese minimo con delta
  acumulado desde ese segundo >= +1.000 -> entra en ese segundo. Plantilla challenge. Corto espejado.
- C3 ESTALLIDO DE CONTINUACION (04-11 T2). Largo solo si la apertura quedo arriba del PVAH y el precio esta sobre el VWAP: un bloque de
  10 s alineado con delta >= +600 cuyo maximo supera el maximo de los 120 s previos -> entra al cierre del bloque. Plantilla challenge.
  Corto espejado (apertura bajo el PVAL, precio bajo el VWAP, delta <= -600, rompe el minimo de 120 s). Secundario: sin el filtro de apertura.
- C4 BURBUJAS Y RECHAZO (04-11 T4). Corto: tendencia corta bajista (precio < precio de 15 min antes); el maximo de los ultimos 120 s
  tuvo compras >= 95 contratos en ordenes agresivas a <= 2 ticks de ese maximo en los 30 s previos al maximo; dentro de los 30 s
  despues del maximo el precio cae >= 2 pts bajo el maximo con delta acumulado < 0 desde el maximo -> entra. Stop = maximo + 3 pts;
  objetivo 7,5 pts. Largo espejado (ventas grandes en el minimo de un retroceso de tendencia corta alcista).
- C5 RUPTURA Y RETESTEO DEL APILADO (14-10, 30-09, 21-10). Velas de 1 min con su footprint por tick (de la cinta; las ordenes que
  barren se reparten parejo, como r16_00). Desequilibrio de compra en p: compras(p) >= 3 x ventas(p - 1 tick) y compras(p) >= 30; 3+
  precios seguidos = apilado. Largo: una vela cierra arriba de un nivel N (su minimo <= N < cierre) con un apilado de compra; en los
  10 min siguientes una vela toca el borde superior del apilado (minimo <= techo del apilado) sin que ninguna vela cierre bajo su piso,
  y cierra de nuevo arriba del techo con delta > 0 -> entra al cierre. Stop = piso del apilado - 1 tick, llevado a 6 pts si queda mas
  cerca y descartado si queda a mas de 9; objetivo nivel siguiente. Corto espejado.
Placebos: P2 = la operacion contraria en el mismo instante con distancias espejadas (azar = promedio de las dos); para C1 y C5
ademas "lugar" = el mismo gatillo SIN exigir nivel, informando aparte los disparos a > 4 pts de todo nivel.
Criterio: se informan n, media despues de costo, t, % ganadoras, contra P2 (y contra "lugar" en C1 y C5). Nada se construye como
gatillo con t < 3; aunque pase, es exploratorio y tiene que repetir hacia adelante.
