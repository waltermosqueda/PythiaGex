# PythiaGex 3.0 - cambios

Regla del operador (18-09-2026): la produccion (`atas/PythiaGexNiveles`, 1.11d) no se toca; la 2.0 (`atas/PythiaGexDos`) tampoco
desde que existe este ensamblado: de las dos solo se LEE para copiar. Todo lo de 3.0 vive en `atas/PythiaGexTres` (`PythiaGexTres.dll`,
categoria "PythiaGex 3.0", datos en `%APPDATA%\ATAS\PythiaGex3\`, logs `pythiagex3-*.log`). Cada entrada dice que se hizo, con que
evidencia y que archivo toca.

## 3.6.9 (08-10-2026, ~03:25 ART) - techo/piso de NDX con los cruces del perfil entero

La lista de cruces del nucleo llega solo a +-0,2 % del precio de ahora: con el precio en 31.304 el techo 31.375,4 de la noche quedaba afuera y
la reconstruccion pintaba 31.350 toda la noche. Ahora los cruces salen del perfil entero de NDX (strike por strike, interpolados igual).

## 3.6.8 (08-10-2026, ~03:35 ART) - zero de NDX con techo y piso (como se ve la 2.0)

Con la 3.6.7 aparecio la fila de abajo (31.350,7) pero no la de arriba (31.375,4): la 2.0 guarda el zero mas cercano muchas veces por vela y como
salta entre los dos cruces que encierran al precio quedan las dos filas; la 3.0 guardaba uno por vela. Raya3NdxZeroCerco (prendido): por vela,
el cruce de signo por volumen mas cercano arriba y el mas cercano abajo (rombos con enfasis) y rotulos 'NDX 0Γ techo' / 'NDX 0Γ piso'.
Historia: para las velas posteriores al ultimo trade de opciones de CBOE (cadena congelada) el techo y el piso se reconstruyen con la
lista de cruces de ahora contra el cierre de cada vela (reconstruccion con la cadena congelada; no vale para velas con la cadena viva).

## 3.6.7 (08-10-2026, ~03:30 ART) - zero de NDX como la 2.0 (prendido, con enfasis); atravesadas APAGADO (medido: no suma)

- Raya3NdxZeroB (nombre nuevo, el operador la tenia apagada por el corrimiento de base) + Raya3NdxZeroEnfasis: rombos grandes como la fila de la
  2.0. Con la base de forwards (3.6.5) y la estela corrida (3.6.6) cae en 31.375,43 / 31.350,73 en la ventana 00:55-01:25 del 08-10.
- Atravesadas3AtenuarB = false. Medido (atravesadas.py, 22 sesiones, verificado): las atravesadas rinden igual (F1) o mejor (F5, rebote 77 vs
  64 % de noche) y el filtro llega 16 min tarde en el caso 31.372. La 3.6.4 lo dejo prendido por defecto: fue un error, corregido.
- Ojo (medido en extremos_rebote.md): el zero de NDX de la 2.0 en 22 sesiones rebota 70,1 % contra 71,3 % de su raya corrida: enmarca bien
  noches como la del 08-10 pero no le gana al placebo.

## 3.6.6 (08-10-2026, ~03:20 ART) - la estela de NDX se corre a la base de ahora

Con la 3.6.5 el NDX vivo quedo donde la 2.0 (zero 31.329,87), pero la estela de la noche estaba guardada con la base 248,15. Cada linea de
estela guarda ahora 'bs' (base usada); al sembrar, los puntos sin 'bs' posteriores a la mediana persistida toman esa base; al dibujar, cada punto
de la capa NDX se corre (base de ahora - base del punto). Esa noche: -9,79 pts, y las filas de 00:55-01:25 caen en 31.375,4 / 31.350,7.

## 3.6.5 (08-10-2026, ~03:20 ART) - base de NDX = recta de forwards de la cadena (la de la 2.0)

Pedido: 'replica la formula de la 2.0 y que se dibuje ahi la raya como en la 2.0'. Las filas de rombos de la 2.0 que enmarcaron 00:55-01:25
(31.375,43 arriba y 31.350,73 abajo) son el ZERO DE LA CAPA NDX. La 3.0 calcula el mismo libro y el mismo zero (doms de NDX iguales a los de
la 2.0 corridos 9,79 pts), pero con base 248,15 (mediana de 24 muestras vela-contra-spot; muestras de 225 a 256 por el atraso de CBOE) en vez de
238,36 (recta de forwards del bajador, base_cruda). Control independiente: futuro a las 16:00:00 NY del 07-10 (31.396,00 a 31.402,50) contra
el NDX congelado 31.160,08 = 235,9 a 242,4; carry implicito con 238,36 = 3,9 % anual. Capa3BaseNdx (6. Capas) = Forwards por defecto;
Rueda = lo anterior. Toda la capa NDX baja ~9,8 pts. Cambio visual avisado con captura antes/despues.

## 3.6.4 (08-10-2026, ~03:00 ART) - rayas atravesadas por los cuerpos, tenues

Pedido (02:50): 'en 31.372 no esta graficado bien; las rayas del medio estan bien dibujadas pero no sirven ahi; la 2.0 con menos ruido'.
Medido 00:50-01:30 (laboratorio/tres/cruces_fuerza_31372.py + CRUCES3): el techo 31.371-31.376 = cruce por volumen 31.372,6 (estable toda
la noche) y zero de QQQ 31.375,7; la 3.0 tenia D1 = 31.372,7 de 01:08 a 01:21 y el zero de QQQ siempre, pero entre filas del medio
(31.359,6 y 31.364,5 por OI, 31.366,3 por volumen = el cruce MAS FUERTE, 86 M contra 60 M) que los cuerpos cruzaron una y otra vez.
Atravesadas3Atenuar (prendido): en la vela b una raya va al 20 % si en las 20 velas anteriores hubo 3 o mas con el nivel adentro del cuerpo.
Solo velas pasadas. Se aplica a la estela de NQ (D1/D2/zero), capas, formulas F* y majors por OI. Limpieza visual; el acierto se mide en
laboratorio/tres/atravesadas.py.

## 3.6.3 (08-10-2026, ~02:45 ART) - majors por OI de QQQ/NDX aunque la cadena de CBOE sea vieja (con su edad), pestanita del libro mas abajo

La cadena de QQQ de la nube tenia 95 min (> 25 min de VIDA_CAPA_S) y sus M+/M- OI no salian; el resto de la capa si se dibuja con su edad (·9h).
Ahora igual. La pestanita 'LIBRO NQ ▸' baja 22 px para no tapar el titulo de la cabecera.

## 3.6.2 (08-10-2026, ~02:45 ART) - majors por OI siempre (NQ, NDX, QQQ); recuadro plegable; ruido apagado; solo F1 y F5

Pedidos: 'el NQ M+ OI debe estar siempre y su negativo tambien, y lo mismo para los otros derivados asi juzgo si sirven'; 'desactive el cuadro,
me molestaba: algo mas chico o desplegable'; 'esto agrega ruido, desactivalo por defecto' (Apoyo, estela del zero por OI, todos los cruces);
'de las formulas solo la F1 y F5 me convencen'.
- Majors por OI sin limite de distancia (MajorOi3RadioMaxPts 3000; fuera de pantalla siempre con flecha en el borde) y para NDX y QQQ
  (color de la capa con filo verde M+ / rojo M−, edad del dato en el rotulo). Estela guardada en estela-*.jsonl ('m':[M+,M−]) y sembrada al arrancar.
- RecuadroLibro3Ver (nuevo nombre): plegado como 'LIBRO NQ ▸'; clic abre, clic en el titulo cierra (ProcessMouseClick). ● en vez de ◆.
- Defaults en false: Ver3Apoyo, Ver3EstelaZeroOi, Zero3TodosLosCruces (el operador ya los tenia apagados en el .ws).
- Formulas: F1 y F5 prendidas; F2/F3/F4/F6/F8 con nombre nuevo (FormulaXb) en false.
- Medido (laboratorio/tres/resultados/extremos_rebote.md, 22 sesiones, criterio extremo+rebote pre-registrado): ninguna formula le gana al azar
  en rebote; F5 (muros por volumen) es la mejor en punta de mecha de noche (60,5 % vs 50,4 azar, 52,8 corrido) y la que menos rayas pone en el
  cuerpo (2,8 c/100 velas); F1 (cruces, el tunel de noche de hoy) es la que mas pone (13,8) y cae en la punta como el azar (49,5 %).
  Doms3Formula sigue en Cruces: el cambio de D1/D2 a F5 queda propuesto al operador.

## 3.6.1 (08-10-2026, ~01:50 ART) - formulas de dominantes a la vista (F1..F8) y majors por OI como dominantes

Pedidos: (1) 'la formula para que las dominantes caigan en los extremos de las velas y rebote, no en el medio': de noche D1/D2 eran el cruce
de signo mas cercano (tunel 3.3.3), que por construccion se pega al precio (estela 01:23: 31.364,54 / 31.359,55 con 1 M de GEX).
Doms3Formula (2. Lectura) elige D1/D2 donde corre el tunel: Cruces (default, sin cambio hasta medir), una por lado vol / OI / vol+OI, muros
vol / OI, majors OI, dos mas grandes. (2) 'todo esto quiero verlo y juzgarlo ya': grupo '9. Formulas a juzgar', cada formula a la vez con
su color (F1 blanco cruce, F2 naranja vol, F3 magenta OI, F4 violeta vol+OI, F5 lima muros vol, F6 rosa muros OI, F8 celeste 2.0), estela
por vela sembrada desde TODAS las fotos de viva3 al arrancar y rotulo chico (no entran al recuadro). (3) sobre 31.350 (major + por OI,
+688 M a las 01:23, donde freno el precio): 'NQ M+ OI' / 'NQ M− OI' dibujados como dominantes (estela gruesa verde/roja + rotulo), con ◆
en el recuadro; los majors por volumen pasan a rotularse 'NQ M+ vol' / 'NQ M− vol'. Nucleo sin cambios de calculo.

## 3.5.7 (08-10-2026, ~01:00 ART) - rotulos Minimal sin precio + recuadro del libro (vol y OI)

Pedido sobre las maquetas: "la 1 pero sacale el precio y agregale este recuadro ... muro de calls y puts, los majors, la gamma o gex" y
"separalo por volumen y por OI: ven cual baja o sube en tiempo real".
- Rotulos3Estilo (Minimal por defecto): el nombre del nivel en su color al lado de la ultima vela, sin caja, sin raya y sin precio; la edad de
  las capas de CBOE sigue (·7h) porque pasa de 30 min. Columna = los chips de la 3.5.5.
- Recuadro del libro (grupo 8, prendido, arriba a la izquierda): los 3 niveles dibujados mas cercanos arriba y abajo del precio con precio,
  distancia, strike de NQ mas cercano y su GEX por volumen y por OI con flecha de cambio en 5 min; abajo muro de calls y de puts (mayor GEX de
  un lado a +-100 pts, como motor.py), majors, neto y regimen (colchon/tobogan), cada uno por VOL (hoy) y por OI (ayer). '*' = el muro cambio
  de strike en la ventana. Nucleo: Strike.GexVolC/GexVolP/GexOiC/GexOiP y GexLado().
- Cambio visual avisado con captura antes/despues.

## 3.5.6 (08-10-2026, 00:45 ART) - sin las rayitas sueltas al lado de los rotulos (Rotulos3Conectores = false)

## 3.5.5 (08-10-2026, 00:40 ART) - sin tunel sombreado, sin rayas largas, rotulos cortos y mas chicos (pedido del operador)

Tunel3Sombreado (false), Rayas3Largas (false: majors y zero de lado a lado, y lineas de la ultima vela al eje de capas, APOYO, cruces OI y
zero de QQQ), Rotulos3Cortos (true: 'NQ D2 31.381,50', 'QQQ 0Γ 31.375,75 ·7h', '0Γ OI 31.432,25', 'APOYO 31.420 ·21'), Rotulos3TamPct (85).
Quedan las estelas por vela y los rotulos. Cambio visual avisado con captura antes/despues.

## 3.5.4 (08-10-2026, 00:25 ART) - linea CRUCES3 en el log por minuto (S, cruces OI y VOL, GEX por OI por strike a +-50 pts)

Para auditar que la 3.0 ve los mismos cruces que la pagina. 00:23: oi=[31359.56 31364.43 31432.32 31441.75], 31430 +5,8 M / 31440 -19,1 M:
el cruce por OI de 31.432 dibujado en turquesa con rotulo '0Γ OI ▲ 31.432,25' y su estela desde las 22:30 (rebobinado).

## 3.5.3 (08-10-2026, 00:30 ART) - cruces por OI hasta 60 pts (31.432 quedaba en el borde de 40 con el precio en 31.392)

Lista del nucleo a +-0,2 %; ajuste renombrado CrucesOiRadio3Pts = 60.

## 3.5.2 (08-10-2026, 00:25 ART) - cruces por OI: radio propio de 40 pts y estela rehecha desde la memoria al arrancar

Con la 3.5.1 instalada (00:13) el precio estaba en 31.402 y el cruce por OI de 31.432 (30 pts) quedaba afuera de los 20 pts de
Zero3CrucesRadioPts, y la estela de cruces arrancaba vacia en cada reinicio de ATAS. Ahora: la lista de cruces del nucleo llega a +-0,15 %
(el conteo 'N cruces' sigue a +-0,1 %), los cruces por OI se dibujan hasta CrucesOi3RadioPts (40) y el rebobinado del arranque siembra su
estela vela por vela desde las fotos de viva3 (como la de D1/D2/zero).

## 3.5.1 (08-10-2026, 00:45 ART) - los cruces de signo por interés abierto identificados y continuos (31.432)

Pedido (00:35, captura): "quienes son estas dominantes en 31.432, no estan identificadas, aciertan pero son intermitentes". Medido en las fotos
de viva3 (scratchpad cruce_31432.py): 31.432 es un CRUCE DE SIGNO DE LA GAMMA POR INTERES ABIERTO entre 31.430 (+5 M) y 31.440 (-19 M), presente
desde las 22:34 ART en 10 de 10 fotos entre 31.431,96 y 31.432,25 (ningun cruce por volumen ahi). Se veia cortado porque la 3.0 dibujaba
solo el cruce MAS CERCANO al precio (zero por OI) y el tunel de noche tambien elige el mas cercano (325 cambios en la noche), y todos los
cruces se pintaban con rombos verdes iguales al APOYO.
- Nucleo: Lectura.ZeroCrucesVolLista y ZeroCrucesOiLista separados (ademas de ZeroCrucesLista, la union).
- Cruces por INTERES ABIERTO: rombos turquesa (mas grandes), sin cortes, y los vigentes con raya hasta el eje y rotulo '0Γ OI ▲/▼ precio'.
- Cruces por VOLUMEN: rombos gris claro chicos. El zero por OI (el mas cercano) tambien en turquesa. APOYO queda solo en verde.

## 3.5.0 (08-10-2026, 00:15 ART) - cada raya se prende y apaga sola; majors con nombre claro; menos ruido; enfasis en el zero de QQQ

Pedido del operador (00:00): investigacion visual de los toques para refinar la 3.0, sacar ruido, poder desactivar una por una (no toda la
familia), majors que no se sabe de que activo son, y enfasis en 31.375 (QQQ 0Γ) y 31.380 (raya intermitente). Medido con su criterio
(laboratorio/tres/rayas_hoy.py, velas de 1 min de la cinta, resultados/rayas_hoy_2026-10-07.md), noche 19:00-23:51 ART (raya al azar 64 %):
3.0 NQ zero 22/27 (mecha a 0,2 pt de mediana), QQQ D1 18/23 (0,7 pt, la mas estable: 13 cambios en 4 h), NQ D2 19/28, NQ D1 13/20, clasica
NQ M- 6/6 y 2.0 NQ M- 5/5; NDX D2 y QQQ D2 de la 3.0: 0 toques. Rueda (raya al azar 63 %): 3.0 NQ M- 9/12, NDX D2 5/6 pero con la mecha
9 pts mas alla; QQQ D2 6/6 con la mecha 6 pts mas alla. Es UNA sesion: descriptivo.
- Nuevo GammaHoyTresRayas.cs, grupo '7. Rayas una por una': NQ D1, NQ D2, NQ zero, NQ M+, NQ M−, NDX D1/D2/D3/zero, QQQ D1/D2/D3/zero,
  QQQ zero con enfasis, cruces sin cortes. Afecta raya, estela por vela y rotulo de cada una.
- Defaults (cambio de lo que se dibuja, avisado con captura antes/despues): NDX D2, NDX D3, QQQ D2, QQQ D3 APAGADAS.
- Majors de NQ: rotulos 'NQ M+' / 'NQ M−' (antes 'NQ MP' / 'NQ MN', que no se entendian).
- QQQ zero: rombo mas grande y opaco + linea de 2 px desde la ultima vela al eje.
- Cruces del zero (rombos chicos): un cruce que estaba y vuelve dentro de 3 velas se dibuja tambien en el hueco (sin cortes).
- 'Todas independientes' (true, pedido 00:20): sin fusion de rotulos (NQ·QQQ D1), rotulos de capa aunque coincidan con NQ, sin tope de
  rotulos, y D1/D2 en el strike EXACTO (sin el centroide 12/6 que promediaba strikes cercanos; tambien en la histeresis y el rebobinado).

## 3.4.1 (07-10-2026, 14:30) - la cinta, revisada: escritor despierto por cada operacion, relleno tras un hueco, un instrumento por archivo

Revision adversaria de la 3.4.0 (nunca instalada). Solo `GammaHoyTresCinta.cs` y `VERSION`; los hooks de la caja y de OnInitialize/OnDispose
no cambian. Compilado (0 errores, la unica advertencia es la vieja CS8321 de GammaHoyNucleo.cs); SIN instalar. Arnes: el
`GammaHoyTresCinta.cs` REAL contra una base falsa (scratchpad `cinta_prueba`, 18 escenarios, 76 chequeos en verde).
- Latencia (lo pedido: "milisegundos de ser posible"): el hilo de ATAS despierta al escritor cuando la cola estaba vacia
  (`Cinta3Despertar`, nuevo, true). Evento -> archivo: mediana 0,044 ms, p95 0,083, maximo 1,2 (antes 28,8 / 33,6 / 62,6 con la tanda de
  25 ms, que Windows estira a ~31). Costo: 3-14 us por evento en el hilo de ATAS (el SetEvent; sin despertador 0,4-2,8 us) y el escritor
  0-5 % de un nucleo a 200 y a 2000 operaciones/s (tiempos de hilo, reloj de 15,6 ms: ruidoso). `Cinta3FlushMs` queda como espera maxima.
- Relleno tras un hueco: si el vivo tuvo un hueco de mas de 60 s (al reabrir el archivo tras un reinicio de ATAS, o sin operaciones a mitad
  de la rueda) y el relleno que hay (su `.listo`, campo `hasta`) no llega al final del hueco, se vuelve a pedir y se reemplaza entero
  (.tmp -> renombre; el `.listo` viejo queda hasta que lo pisa el nuevo: el lector nunca se queda sin relleno). Igual que 60 s de
  `--hueco-vivo-seg` de tiempo_real.py; un hueco menor no se puede completar (el vivo lo da por seguido) y queda anotado en el log. Maximo 3
  pedidos por sesion y por instancia, 2 min entre uno bueno y el siguiente.
- El relleno espera mientras haya posicion abierta u orden activa (TradingManager.Position / Orders, desde el reloj de ATAS) y lo dice en el
  log cada 5 min: bajar la sesion entera carga a ATAS ~45 s (sonda de Flujo Claro). El impacto real en ATAS sigue SIN MEDIR: mirarlo en la
  primera instalacion (working set de OFT.Platform y fluidez del grafico durante el primer relleno).
- Un pedido por sesion en todo ATAS: si la instancia se quita con el pedido en vuelo, la nueva espera esa respuesta (hasta 300 s) y la
  respuesta tardia se escribe igual aunque el indicador ya no este. Antes: doble carga.
- Un instrumento por archivo: `cinta-<raiz>-<dia>.csv.instrumento` (una linea, p. ej. MNQZ6) se escribe al crear el archivo; otro instrumento
  (NQ en vez de MNQ, H7 en vez de Z6 en el roll) no escribe en ese archivo hasta la sesion siguiente y lo dice en el log.
- El dueño es el que recibe la cinta: otro grafico se la saca solo si el dueño no da señales hace 3 s (hilo muerto) o si este recibe
  operaciones que el dueño no ve hace mas de 10 s, durante 2 s seguidos (la ultima operacion del dueño se lee en vivo de su campo). Con la
  misma cinta no hay vaiven (probado con 20 s de mercado quieto fingido).
- Hora: ticks crudos siempre (antes ToUniversalTime si Kind==Local: con una PC a -03:00 tiraba el 100 % por fuera de hora). La ventana -15/+5 min
  se mide en el hilo de ATAS con DateTime.UtcNow (~25 ns), no con el reloj que dejaba el escritor (escritor trabado 6 min = todo tirado).
- Error de disco: sin archivo NO se saca nada de la cola (la operacion queda retenida, la cola detras hasta el tope de 1M) y se reintenta a
  los 5 s sin girar en vacio (0,0 % de CPU con el archivo trabado 2 s). Al reabrir, cada operacion conserva su id (un anillo id interno ->
  id del archivo por dia, 3 dias): las actualizaciones no se parten en dos ids, tampoco al cruzar la rotacion. Se pierde solo la tanda que
  estaba escribiendo cuando fallo el disco (no se sabe cuanto llego; repetirla duplicaria).
- Linea cortada por un cierre de ATAS a mitad de escritura: la instancia siguiente le agrega `x` antes del salto (`...,1x`): no es numerica,
  todo lector la descarta (antes un corte a mitad del id dejaba una linea valida con OTRO id: el id 1 sumaba 16 en vez de 7). Se pierde el
  volumen de esa operacion.
- Ajuste nuevo (nombre nuevo, ningun valor guardado en el .ws lo pisa): "6. Cinta" -> `Cinta3Despertar` (true). Los demas, iguales.
- tiempo_real.py 1.1.0 (mismo dia, profundidad/): validacion estricta de lineas (5 campos numericos, t en la sesion del archivo), velas a
  medida ancladas a las 22:00 UTC, recorte por `--max-ops`, archivo recreado, fila tardia tras rotar, /salud con `estado`/`aviso`,
  HEAD /stream, desde=inf -> 400, Ctrl+C aunque el padre lo ignore.

## 3.4.0 (07-10-2026, 13:00) - la cinta en vivo al milisegundo, para velas de 30 s, 1 min y a eleccion en Profundidad

Pedido textual: "inclui temporalidad 1 m, 30 seg y que se pueda colocar custom; ademas sincroniza lo maximo posible todo en tiempo real, ya
sea milisegundos de ser posible o lo mas cerca". Profundidad 3.0 recibia el precio a lo sumo una vez por segundo (indicador.json) y velas m2:
sin operaciones no hay velas de 30 s. La unica fuente de operaciones en vivo es ATAS; la 3.0 ahora las exporta tal cual llegan.
- Archivo NUEVO `GammaHoyTresCinta.cs`. Hooks de una linea: `CintaEvento(trade, true/false)` al principio de OnCumulativeTrade y
  OnUpdateCumulativeTrade (la caja sigue igual), `CintaArrancar()` en OnInitialize, `CintaParar()` en OnDispose.
- Hilo de ATAS: sin I/O, sin LINQ, sin UtcNow; una llave corta sobre un anillo de 16 operaciones (para el dv de las actualizaciones) y un
  struct a una ConcurrentQueue. Medido fuera de ATAS con el archivo real (arnes en el scratchpad): ~380 ns por evento.
- Hilo escritor propio cada `Cinta3FlushMs` (25; Windows redondea a 15,6 ms: ~31-35 ms reales) anexa a
  `profundidad\estado\cinta\cinta-<raiz>-<dia de la sesion>.csv` (22:00 UTC abre la sesion del dia siguiente), Flush por tanda,
  FileShare ReadWrite|Delete. Cabecera `t,precio,dv,lado,id`; t = ms UTC de la operacion, dv = volumen nuevo del evento, lado 1/-1/0,
  id creciente por operacion (sigue el ultimo del archivo tras un reinicio). Un solo grafico escribe por raiz (dueño con latido).
  Operaciones a mas de -15/+5 min del reloj (Market Replay, historia) no se escriben.
- Relleno (`Cinta3Rellenar`): una vez por sesion, 60 s despues del arranque y con el vivo ya grabando, si no hay
  `cinta-<raiz>-<dia>-relleno.csv` + `.csv.listo`, pide RequestForCumulativeTrades desde las 22:00 UTC hasta ahora (minimo 1). La respuesta
  se filtra al rango (trampa de la sesion entera), se ordena y se escribe en un Task (.tmp -> renombre), ids negativos; el `.listo` es un
  JSON con n, desde, hasta, primero, ultimo, vivoDesde. Una segunda respuesta se ignora. Costura: relleno para t <= hasta, vivo para t > hasta.
- Log cada 60 s: `cinta: N operaciones en 60 s, cola max M, flush medio X ms, archivo Y MB | ... retraso medio R ms`.
- Ajustes nuevos (grupo "6. Cinta"): Cinta3Exportar (true), Cinta3FlushMs (25), Cinta3Carpeta (profundidad\estado\cinta), Cinta3Rellenar (true).
- No dibuja nada, no cambia ningun default de lo que se dibuja. Compilado (0 errores, sin advertencias nuevas); SIN instalar.

## 3.3.3 (07-10-2026, 04:15) - el tunel se arma con los cruces de signo de la gamma (las filas de la 2.0), tambien en el rebobinado

Pedido textual (04:10): "ver el tunel con dominantes sobre la zona 31484 y 31456, esa formula aplicada a todo el grafico; 31490/31450 estan muy
arriba o abajo". `Tunel3Fuente` = Cruces (default, lo pedido): techo = cruce de signo (vol u OI, +-0,1 % del precio) mas cercano ARRIBA del ultimo
cierre, piso = el mas cercano ABAJO, histeresis mientras el precio cierre adentro; sin cruces cae a Strikes (3.3.2). Se aplica tambien foto a foto
en el rebobinado del arranque (estado propio), asi la estela D1/D2 cubre todo el grafico. Lo medido para el cruce mas cercano (criterio del
operador, 20 sesiones, noche): 62,1 % (vol) y 59,2 % (OI) contra 65,9 % del tunel de strikes y 65 % de rayas al azar: se instala porque lo pidio
asi dos veces, con el dato dicho en el mensaje; la caja negra lo mide (fam NQ D1/D2 con placebos). Cambio de default avisado con captura antes/despues.

## 3.3.2 (07-10-2026, 04:05) - de noche, con la regla Tres, las dominantes son el Tunel (medido con el criterio del operador)

Estudio pre-registrado laboratorio/tres/criterio_operador.py (toque +-2, rebote 6 pts en 3 velas m2 antes de un cierre 2 pts del otro lado),
20 sesiones 08-09..06-10, libro vivo de NQ, velas m2 de MNQ, resultados/criterio_operador_{base,fino,ancho}.md:
- NOCHE (22:00-13:30 UTC): NH_sig_25 (techo/piso = strike mas cercano por lado con |gex| >= 25 % del mayor, con histeresis) 65,9 % en 587 toques;
  N_sig_25 65,5 % (681); TS_25 66,4 % (589); NH_sig_50 68,2 % (343). Filas de la 2.0: Z2_vol 62,1 % (752), Z2_oi 59,2 % (515). Clasica (lo que la
  3.0 dibujaba de noche) 63,3 % (439). Rango de la ultima hora (sin opciones) 60,8 %. Rayas fijas al azar 65 % (banda 35-98). NH_sig_25 gana a
  Z2_vol en 9/17 sesiones y a Z2_oi en 12/17; su placebo corrido 63,6.
- RUEDA (13:30-20:00 UTC): todas las reglas 56-61 % contra rayas al azar 63 %: nada se distingue de nada. Sigue la histeresis de la rueda (3.0.4).
- Lectura honesta: de noche el tunel le gana a las filas de la 2.0 en el criterio del operador, por 4 a 7 puntos y en la mayoria de las
  sesiones; contra una raya puesta al azar la ventaja es de 1 punto. Se instala porque es lo mejor que hay y porque lo pidio asi; la caja negra
  lo sigue midiendo hacia adelante (fam NQ D1/D2 con placebos).
- `Tunel3DeNoche` (true): con la regla Tres, fuera de la rueda las dominantes son el Tunel (umbral `Tunel3UmbralPct` 25, histeresis). Cabecera:
  "regla Tres (tunel de noche)". Apagado = clasica a secas (3.2.2). Cambio de default avisado con captura antes (03:07, 3.2.5) y despues.
- Verificacion adversaria del estudio en curso (workflow verificar-criterio-operador); si encuentra un sesgo que cambie el orden, se revierte.

## 3.3.1 (07-10-2026, 03:55) - las tres filas de la 2.0 dibujadas igual en la 3.0, y todos los cruces cercanos (pedido textual)

Pedido (03:45): "calcula como la 2.0 grafico las dominantes en 31485/31484 y 31474/31472, replicala en la v3 y mejorala: que toque aun mas las
mechas cercanas". Esas filas son el zero gamma por cruce de signo mas cercano al precio (GammaHoyNucleo.ZeroPorSigno, identica en 2.0 y 3.0)
de tres libros: NQ por volumen (31471), NQ por interes abierto (31473) y NDX por volumen (31485,6 con la base cruda de la 2.0; 31476,7 con la
mediana de la rueda de la 3.0). La 3.0 ya calculaba los tres; faltaba dibujarlos como la 2.0:
- `Capa3ZeroRombos` (true; renombrado desde Capa3Zero para pisar el false guardado en el workspace): el zero de NDX y QQQ como rombos por vela
  del color de la capa, con rotulo. Con `Zero3Estilo = GuionGris` queda la estela de 1 px.
- El zero por OI con el MISMO rombo verde que el zero por volumen (antes mas chico y oscuro).
- LA MEJORA pedida: `Zero3TodosLosCruces` (true) y `Zero3CrucesRadioPts` (20): todos los cruces de signo (vol y OI) a +-0,1 % del precio como
  rombos chicos por vela (`Lectura.ZeroCrucesLista`). Mas filas tocan mas mechas; lo medido (criterio_operador.py, en curso) dice que el % de
  rebote por toque no sube con eso: se dibuja porque lo pidio el operador, y la caja negra lo mide (fam ZERO/ZEROOI).
- Base de NDX: NO se cambio (mediana de la rueda, documentada en CLAUDE.md); la 2.0 usa la cruda de ticks (base_confiable=false). Diferencia
  3,6 pts; el cruce elegido puede ser el vecino (31476,7 vs 31485,6).
- Regla Tunel (3.3.0) sigue como opcion, no default: espera el resultado del estudio.

## 3.2.5 (07-10-2026, 03:10) - el zero dice cuantos cruces tiene; la caja graba el criterio visual del operador

Pedido (02:55): "la 2.0 acierta mas, algo esta mal calibrado". Medido (laboratorio, scratchpad visual_noche.py, velas m2 reconstruidas de
las muestras de precio de los tres indicadores, error tipico 1 pt): las tres filas de rombos de la 2.0 (31458 / 31472 / 31486) son UN mismo
zero gamma (el de NDX, y el de NQ por OI) que SALTA al cruce de signo mas cercano al precio: en el 0DTE del 07-10 02:20 hay 5 cruces por
volumen y 17 por OI en +-120 pts (31444, 31457, 31461, 31473, 31488, 31490...). La raya se pega al precio por construccion (distancia
mediana al precio 5-6 pts contra 12-19 de las dominantes). Con el criterio visual del operador (toque +-2, rebote 6 pts en 3 velas antes de
un cierre 2 pts del otro lado), noche 22:00-02:54: 2.0 zero NDX 18 toques 61 %, 2.0 zero OI 16 toques 62 %, 2.0 zero vol 12 toques 42 %;
3.0 zero OI 19 toques 58 %, QQQ D1 10 toques 60 %, D1 4 toques 50 %; 400 rayas fijas al azar: 50 % (banda 5-95: 14-75 %). Con tolerancia
3/8/3 la fila NDX de la 2.0 baja a 40 %. Nada de esto sale de la banda del azar. Figura: capturas/2026-10-07_0300_medicion_visual_2.0_vs_3.0.png.
La base de NDX: la 2.0 usa la cruda de ticks (247,47, base_confiable=false, error 77 ticks); la 3.0 la mediana de la rueda (251,10): por eso
su zero de NDX cae en 31485,6 y el de la 3.0 en 31476,7 (mismo libro, mismo metodo, otro cruce vecino).
- `GammaHoyNucleo.ZeroPorSigno` devuelve ademas cuantos cruces hay a +-0,1 % del precio: `Lectura.ZeroCrucesVol/Oi`, AUDIT3 `zeroCruces=v/o`,
  rotulo "NQ 0Γ 31.471 ·5 cruces" y cabecera. Mas de 1 cruce = el zero es una eleccion entre vecinos, no un nivel.
- Caja negra: por toque, `sV6` (segundos hasta alejarse 6 pts hacia el lado del que vino), `sC2` (segundos hasta un CIERRE 2 pts del otro
  lado) y `vis6` (G = rebote visual, S = traspaso, null = ni una ni otra en 6 min). Es el criterio del operador, grabado hacia adelante con
  placebos; el lector lo toma en min=15 (ventana completa).
- Nada cambia en lo que se dibuja ni en la cuenta.

## 3.2.4 (07-10-2026, 02:50) - el zero gamma como rombos verdes por vela (el estilo de la 2.0), apagable

El operador ve en la 2.0 "las dominantes a la altura frenando" en 31485/31472/31458: son los rombos del zero por OI y por volumen. La 3.0
tenia el mismo zero como guion gris fino y no se veia. `Zero3Estilo` (RomboVerde | GuionGris), default RomboVerde: zero por volumen como rombo
verde, zero por OI como rombo mas chico y oscuro. Nada cambia en la cuenta ni en lo medido (zero = placebo como nivel de rebote; la caja lo
graba como ZERO y ZEROOI). Cambio visual avisado con captura antes/despues.

## 3.2.3 (07-10-2026, 02:50) - APOYO: el libro profundo de las opciones (rombos), zero por OI, tamaños de puntas en el estado

Pedido (02:40): "fijate como en 31485 esta graficado y lo frenan; supera a la 2.0 en exactitud". Lo que la 2.0 dibuja ahi (GammaHoyCapas 2.0,
leido): rombos = zero gamma por vela de cada capa (por volumen Y por interes abierto: la noche del 07-10 zeroVol 31460/31470 y zeroOi
31473/31488 en su log) y los 3 strikes con mas contratos APOYADOS en las puntas ("PROFUNDIDAD", ApoyoPorStrikeLados). Ninguno es una
dominante por gamma. Medicion de esta noche (22:00-05:30 UTC, 217 velas m2, regla del PRE: zona 2,5 / lejos 15 en 30 min / rebote 12 antes
de 6 en 15 min): 3.0 D1+D2+zero 12 toques, 7 rebotes (58 %) vs placebo 59 %; 2.0 D1+D2+zeroVol 10 toques, 4 rebotes (40 %) vs placebo 52 %;
zeroOi de la 2.0 0 toques (sus placebos 15, 40 %). n chico: descriptivo.
- **Nuevo `GammaHoyTresApoyo.cs`**: `Ver3Apoyo` (true) y `Apoyo3Cuantos` (3): por strike del vencimiento mas cercano, contratos apoyados =
  bid + ask del call + bid + ask del put (BestBidVolume / BestAskVolume del Summary: nuevos `CadenaApi.Fila.BidVol/AskVol` y
  `Feed.Fila.BidVolC/AskVolC/BidVolP/AskVolP`); los N mayores a <= radio de dibujo como rombos verdes por vela, linea fina del actual hasta el
  eje y rotulo chico "APOYO ▲ 31.480 ·1.240 ctos". `Ver3EstelaZeroOi` (true): el zero por OI como puntos gris oscuro por vela. Las dos
  estelas viven en memoria (no se persisten: arrancan vacias al reiniciar).
- Caja negra: fams nuevas APOYO (A1..AN, origen = contratos) y ZEROOI, para medirlos contra placebo con la misma regla.
- indicador.json: bidVolC/askVolC/bidVolP/askVolP por strike (la pagina Profundidad puede mostrar el apoyo por lado).
- Dicho con el pedido: NADA de esto esta medido como nivel (ni laboratorio ni caja). Se dibuja porque el operador lo ve reaccionar y para
  que la caja lo mida con muestra nueva; las dominantes por gamma siguen siendo D1/D2.

## 3.2.2 (07-10-2026, 02:30) - el portero no se reinicia a medianoche y fuera de la rueda la regla Tres es la clasica a secas

Pedido (02:15): "la 2.0 esta acertando mucho mas las dominantes; recalcula todo". Con los logs de las dos:
- DEFECTO REAL en la 3.0: a las 01:00 ART (00:00 NY) el log dice `hist: estado vacio: tramo nuevo (madrugada, 10-07)`: el tramo usaba la
  FECHA de NY y el cambio de dia reinicio la histeresis. Al rearmar desde vacio con el precio en 31475, D1 = 31490 (arriba) y D2 = 31450
  (-100M, la unica de abajo con fuerza) y ahi quedaron 2 h (31490/31450) mientras el precio iba 31465-31486. Hasta las 01:00 la 3.0 tenia
  31490/31520, lo mismo que la capa NQ de la 2.0 (log 2.0: capa=NQ doms=31520/31490 toda la noche).
- Arreglo: `Histeresis3.TramoDe` tiene dos tramos por sesion: dia (09:30-18:00 NY) y noche (18:00 -> 09:30 del dia siguiente, fecha de la
  sesion que arranca). La medianoche no corta nada.
- Regla Tres fuera de la rueda de NY (antes de las 09:30 y desde las 16:00): SIN histeresis, clasica a secas (una por lado + empate 20 % +
  centroide 12). Motivo medido: la histeresis se midio solo en rueda (PRE_REGISTRO); la unica medicion de noche que existe es la de la clasica
  (lineas_base: R0 noche n 101, 53,5 % vs 44,3 %, +9,1 pp, z 1,64, informativa; R1 noche 0,0). Log: `hist: fuera de la rueda de NY: regla
  Tres = clasica a secas` / `hist: rueda de NY: la histeresis vuelve a decidir`. La histeresis arranca vacia a las 09:30 NY (tramo nuevo).
- Lo que el operador vio "acertar" en la 2.0 son los rombos verdes de su capa NQ: no son sus dominantes (sus doms de la noche fueron
  31520/31490 por NQ y 31502/31419 por QQQ congelada); ver el analisis en el mensaje del 07-10 02:30 y la memoria pelotitas-son-eventos.

## 3.2.1 (07-10-2026, 01:50) - el pulso POR CAMBIO (1 s) para "Profundidad 3.0": COMPILADA, NO INSTALADA

Pedido del operador (01:35): "si cierro ATAS deja de funcionar la pagina, quiero que sea independiente y lo mas en tiempo real posible,
de segundos". La unica puerta licenciada al libro de opciones de NQ en esta PC es ATAS (Rithmic por ATAS Ultra + OptionsApi): una
segunda sesion con las mismas credenciales choca con la de ATAS (log de ATAS 06-10 18:16: "A connector with the same credentials is
already connected") y NO se extraen credenciales de ningun lado. La independencia se arma asi: ATAS como servidor encendido y vigilado,
el indicador escribiendo el estado POR CAMBIO (1-2 s) y el motor/pagina leyendolo a 2 s. Esta entrada es la parte del indicador.
**Compilada con 0 errores (CS8321 heredada), NO instalada: el operador tenia posicion y no se reinicia ATAS. Se instala con
`herramientas/instalar_3_0.ps1` cuando este sin posicion.** El DLL que corre (o corria: ATAS se cerro a las 01:39) sigue siendo 3.1.2.
- `GammaHoyTresEstado.cs`: segundo temporizador LIVIANO de 1 s (`SubscribeToTimer(_pulsoPeriodo, _pulso)` en OnInitialize via
  `EstadoPulsoArrancar`, `UnsubscribeFromTimer` en OnDispose). En cada vuelta: si `CadenaApi.Cambios` (el contador de Changed que ya
  existia) se movio desde la ULTIMA ESCRITURA, o el precio del grafico paso a otro tick (`InstrumentInfo.TickSize`), rehace SOLO la
  cuenta del nucleo (`DesdeApi` + `Calcular`, igual que el Tick) sobre un `_nucleoPulso` propio (Calcular guarda historia para el Max
  Change: no se mezcla con la del Tick; ajustes copiados del nucleo del Tick como hace la memoria) y, con la regla Tres, pasa la lectura
  por LA MISMA `Hist` con `decidir = false` (3.0.7: no hay paso, las vigentes solo se reubican; ningun paso de la histeresis se adelanta).
  Se llama a `Hist.Aplicar` directo y no a `AplicarRegla` para no alternar el aviso "libro a medio armar / completo" del Tick. Escribe
  el json atomico; si no cambio nada, no escribe. Tope: una escritura cada `Estado3PulsoSeg` segundos (1-5, default 1). NO toca `_L`,
  `_c` ni el dibujo: el Tick de 5 s sigue siendo el dueño de la estela, la caja, las capas y la histeresis, y sigue escribiendo su
  latido (origen "tick") con la cuenta compartida.
- Una sola llamada a `Filas()` por pulso: `DesdeApi(raiz, foto, fs, out utiles)` (sobrecarga nueva en `GammaHoyTres.cs`) y `EstadoArmar`
  reciben las filas ya despejadas (IV incluida); la 3.2.0 pedia `Filas()` dos veces por latido.
- JSON nuevo: `origen` ("tick" | "pulso"), `cambios` (el contador), `pulso_s` (segundos desde que el contador se movio por ultima vez),
  `pulso_seg`, `escritos_pulso`, `saltados_tope`. Lo demas igual que 3.2.0.
- Ajustes (grupo 5. Profundidad): `Estado3Pulso` (true, renombrado el rotulo), `Estado3PulsoSeg` (1-5, default 1), `Estado3Ruta`.
- Costo (dicho, no medido todavia): en la rueda el contador se mueve varias veces por segundo, asi que en la practica es UNA cuenta
  completa por segundo (~320 contratos con despeje de IV + nucleo) mas un archivo de ~60 KB/s, en vez de una cada 5 s. En un i3 con ATAS
  abierto hay que mirar el consumo al instalar; si molesta, `Estado3PulsoSeg` = 2 o 3. Si la escritura falla, un log por minuto
  (`estado: ...`), nunca una excepcion fuera del temporizador. Guarda de reentrada con Interlocked y una llave para que Tick y pulso no
  escriban a la vez.
- Lo que NO hace: no reemplaza a ATAS (sin ATAS no hay libro), no suscribe nada nuevo, no dibuja nada. La vigilancia de ATAS (vivo /
  caido / Rithmic conectado) va del lado del motor (profundidad/fuentes.py `atas_estado`, con el log de la 3.0 y el de la plataforma).

## 3.2.0 (07-10-2026, 01:10) - el pulso de 5 s para "Profundidad 3.0" (indicador.json): COMPILADA, NO INSTALADA

Pedido del operador (06/07-10: "hacelo todo"): una pagina local en tiempo real con el libro de opciones de NQ en profundidad. Esta
entrada es la parte del indicador (fase 5 del proyecto `profundidad/`). **Compilada con 0 errores, NO instalada: el operador tiene
posicion y no se reinicia ATAS. Se instala con `herramientas/instalar_3_0.ps1` cuando este sin posicion.**
- **Nuevo `GammaHoyTresEstado.cs`** (parcial). En cada Tick (5 s), despues de `CajaLatido`, escribe de forma ATOMICA (archivo temporal
  `.<pid>.tmp` + `File.Move` con pisado; `File.Replace` de respaldo) `profundidad\estado\indicador.json` con: `generado` (ISO UTC),
  `raiz`, `precio` (PrecioGrafico), `precio_cuenta` y `cuenta_utc`, `regla`, `horizonte`, `strikes_utiles`; `libro` (CadenaApi.Estado:
  via, suscritos, con_puntas, con_oi, rechazadas, errores, vencidas_fuera, `edad_s`, hora del ultimo resumen y del ultimo armado);
  `zonas` (doms con fut/gex/etiqueta/`vig_desde`, zero por volumen y por OI con su modo, net, max abs, cuadrante, `hist` = resumen +
  vig/cont/pasos estructurados si la regla es Tres); `escalera` = SOLO el vencimiento mas cercano ('Hoy'): `dias` y una fila por strike
  del perfil con K, fut, K0 (si esta corrido por el roll), gexVol, gexOi, conv, oi, volHoy, ivMedia del nucleo + por lado desde la cadena
  (`oiC/oiP`, `ivC/ivP`, `volC/volP`) + por contrato desde `CadenaApi.Filas()` (`volAyerC/P`, `lastC/P`, `bidC/askC/bidP/askP`,
  `codC/codP`). Lo que no existe va null. Unidades: gex crudo como el nucleo (la pagina muestra M).
- Ajustes (grupo 5. Profundidad): `Estado3Pulso` (true) y `Estado3Ruta` (default la ruta de arriba). Si la escritura falla se loguea
  `estado: no pude escribir ...` una vez por minuto y nunca sale una excepcion del Tick.
- `Histeresis3.Foto()`: accesor de solo lectura (vigentes, contadores, pasos) para no parsear `Resumen()`.
- Costo: una llamada extra a `CadenaApi.Filas()` por latido (~320 contratos, despeje de IV incluido) y un archivo de ~60 KB cada 5 s.
  No dibuja nada, no suscribe nada, no toca la cuenta ni la caja negra.

## 3.1.2 (06-10-2026, 23:15) - estela del zero gamma (pedido del operador), apagable

Pedido (23:10): "que se grafiquen y se puedan desactivar los cero gamma flip como estela; veo que son zonas de rebote".
- `Ver3EstelaZero` (SegunPerfil = si): el zero gamma de cada vela cerrada (ya guardado en la estela como tercer valor) se dibuja como
  guion gris de 2 px, igual que D1/D2 (alfa al 80 %, sesion anterior al 45 %). La linea punteada del zero actual sigue como estaba.
- Capas: con `Capa3Zero` prendido, ademas del chip, la estela del zero de la capa en 1 px en su color.
- Lo medido, dicho con el pedido: en la fase 2 el zero como nivel de rebote dio 36,8 % contra 36,8 % de placebo (19 toques en 9 dias)
  y en H2 (06-10) tampoco gano, con 26-31 cambios por hora; esta noche fue biestable (31.480 <-> 31.525). Se dibuja porque el operador
  lo pide para juzgarlo a ojo; la caja negra lo graba como fam ZERO para medirlo con muestra nueva. Nada cambia en la regla.

## 3.1.1 (06-10-2026, 23:05) - caja negra: orden G/S, 'viene de lejos' como el juez, 'otras' para el ping-pong

Dudas del lector (laboratorio/tres/caja_negra.py, CAJA_NEGRA_PRE.md seccion 7) resueltas en el C#:
- Resultado: `sG12`, `sS6`, `sG20`, `sS8` = segundos desde el toque hasta la primera vela que alcanzo a favor 12 / 20 y en contra 6 / 8
  (regla chica y grande del PRE_REGISTRO; en ES se escalan por 0,25), y `primero` / `primero20_8` = "G" | "S" | "=" (misma vela) | null.
  Sin esto, aFavor >= 12 y enContra >= 6 en la misma ventana quedaban indeterminados. La vela del toque esta INCLUIDA en alto/bajo/enContra.
- Toque solo si VIENE DE LEJOS (como el juez, PRE_REGISTRO 3): en los 30 min previos hubo un cierre a >= LEJOS (15 pts NQ) del lado de
  llegada y ese alejamiento no se gasto en un toque anterior. La 3.1.0 armaba los dos lados al crear el nivel y contaba cruces (el 07-10
  01:45 y 01:47 UTC registro el 31.490 desde arriba y desde abajo con 2,5 min de diferencia). Al crear un nivel se miran los cierres de los
  30 min previos. Nuevo campo `minDesdeLejos` en la linea de toque.
- `otras`: [[fam, etq, nivel], ...] con los niveles vigentes de las demas familias (sin placebos) en el instante del toque: la pregunta del
  ping-pong (INFORME_HIPOTESIS 4.2) se responde con eso.
- `op.dist` = distancia en puntos entre el nivel y el Fut del strike elegido (0 en NQ; > 0 en PLACEBO/ZERO, donde se toma el strike mas
  cercano). `salto5` = volumen total (calls + puts) del strike ahora menos hace 5 min; null los primeros 5 min tras arrancar.

## 3.1.0 (06-10-2026, 22:50) - LA CAJA NEGRA: cada toque graba opciones + cinta + libro, y el resultado a 5/15/30 min

Pedido (22:45): "si, arma". Pre-registro: laboratorio/tres/CAJA_NEGRA_PRE.md; lector y juez: laboratorio/tres/caja_negra.py.
- **Nuevo `GammaHoyTresCaja.cs`** (parcial). Niveles vigilados: NQ D1/D2, sus PLACEBOS (+-12,5 y +-25: el control), ZERO de NQ, capas
  NDX/QQQ D1..D3 con la edad del dato. Toque = la vela VIVA (cada 5 s) entra en [nivel - zona, nivel + zona] y la cerrada anterior no
  estaba; un toque por nivel y lado hasta que un cierre se aleje LEJOS (misma regla del indicador y del PRE_REGISTRO).
- En el instante del toque se graba (PythiaGex3\caja\caja-<raiz>-<dia>.jsonl, una linea JSON): la vela, el strike del nivel con OI,
  volumen, salto de volumen en 5 min, IV, gex (vol y OI), VANNA Black-76 del dealer (calls +, puts -; sin escalar) y los 2 vecinos por
  lado; la cinta de los 60 s previos (OnCumulativeTrade: delta, volumen, operaciones, prints >= `Caja3LoteGrande` 20 por lado); la
  foto Level 2 del libro (`MarketDepthInfo.GetMarketDepthSnapshot`, `Caja3Profundidad` 10 niveles por lado, sumas, desequilibrio,
  orden mayor, spread; NO MBO); desde cuando es vigente la dominante (histeresis); edad del libro vivo y de la capa.
- A los 5, 15 y 30 min: linea resultado con alto, bajo, cierre, a favor y en contra (segun el lado de llegada) y cuantas velas.
- Ajustes (grupo 4. Caja negra): `Caja3Activa` (true), `Caja3Placebos` (true), `Caja3Capas` (true), `Caja3Profundidad` (10),
  `Caja3LoteGrande` (20). Log: `caja: toque ...` por evento y `caja: N toques y M resultados hoy ...` cada 5 min.
- No dibuja nada. Los toques pendientes de resultado se pierden si ATAS se reinicia (el lector los cuenta como toques sin resultado).

## 3.0.9 (06-10-2026, 22:05) - D1 = la dominante MAS CERCANA al precio (en vez de la mas fuerte / la de arriba)

La 3.0.8 (D1 = la mas fuerte) arreglo QQQ pero empeoro NDX: su mas fuerte es el muro 31.300 (194M) a +61 y paso a llamarse NDX D1, con la
de 31.511 como D3. La clasica rotula [arriba, abajo, resto]: por eso en su grafico QQQ D1 era el 760 cuando el precio estaba DEBAJO de
31.497 y hubiera sido el 762 lejano con el precio arriba. Ningun criterio de fuerza o de lado deja D1 cerca en las tres familias a la vez.
- Ahora, en NQ (con y sin histeresis) y en las capas: D1 = la mas cercana al precio, D2 la siguiente, D3 la mas lejana. Los niveles y su
  fuerza no cambian; cambia solo el numero del rotulo. El juez no usa el numero (identifica por precio), asi que nada medido se altera.
- Aviso: los nombres pueden NO coincidir con los de la clasica para el mismo strike (ella numera por lado); los precios si coinciden.

## 3.0.8 (06-10-2026, 21:45) - D1 es la dominante MAS FUERTE, no la de arriba (etiquetas de QQQ/NDX/NQ)

Pedido (21:40): "sigo sin ver a QQQ D1, deberia estar cerca de los otros". Quirurgico, con la cadena de QQQ (ultima-QQQ.json, 0DTE 10-07):
  760: 44.869 calls / 81.633 puts -> neto -36.764 (muro de PUTS pegado al precio, 31.500)  |  761: 58.689 / 50.906 -> +7.783 (parejo, no es
  nivel en gamma x volumen NETO)  |  762: 55.716 / 28.142 -> +27.574 (muro de CALLS, 31.582, +83)  |  758: -19.957 (31.417).
  La 3.0 SI dibujaba el 760 en 31.500, pero rotulado QQQ D2, y el 762 lejano como QQQ D1, porque el nucleo devuelve las dominantes en el
  orden [arriba, abajo] y la etiqueta D1 se la llevaba la de arriba. Lo mismo pasaba con NDX (D1 = la de arriba) y con NQ sin histeresis.
- Ahora D1 = la de mayor |gex|, D2 la siguiente, D3 la tercera, en las capas y en la primaria con Clasica/DosMasGrandes (la histeresis ya
  ordenaba por fuerza). Niveles identicos, etiquetas distintas: QQQ D1 pasa a ser 31.500 (muro de puts) y QQQ D2 el 31.582 (+83).
- Lo que NO cambia: el 762 a +83 es real y la clasica tambien lo tiene (31.576-31.578); entre 760 y 762 no hay muro en QQQ porque el 761
  tiene calls y puts parejos. Con Capa3RadioDibujoPts = 60 el +83 queda como chip en el borde superior (flecha) sin linea.

## 3.0.7 (06-10-2026, 21:35) - las capas siguen el reloj del spot (QQQ derivaba de noche), radio propio de capas, portero sin decidir con el libro incompleto

Pedido (21:25): "QQQ D1 +83 y NDX D3 +50, defasados; ¿error de codigo o variable?". Analisis con el archivo de CBOE de hoy:
- NDX D3 31.551,75 = strike 31.300 + 251 (178 calls operados, el mayor volumen de calls del 0DTE de NDX): nivel real, lejos. QQQ D1 = strike
  762 (OI 12.953 calls, 55.716 operados: el mayor de toda la cadena de QQQ) = muro de calls a +80: nivel real, lejos. No estan mal
  dibujados; la clasica los tiene en el mismo lugar (31.551,82 y 31.576-31.578). El laboratorio midio que los niveles lejanos de las capas
  rara vez se tocan: por eso ahora `Capa3RadioDibujoPts` (60): guiones y linea solo a <= 60 pts del precio; mas lejos, rotulo y estela tenue.
- PERO habia un error real en la razon de QQQ: el ETF sigue cotizando en el after-hours (spot 759,98 -> 760,57 entre las 16:15 y las 20:00 NY
  con el ultimo trade de opciones clavado en 16:14:59), y la 3.0 anclaba la vela del MNQ al ultimo trade: la razon derivaba con el spot
  (41,4407 -> 41,4467 -> 41,4505 en 10 minutos) y los niveles de QQQ se movian 7-8 pts sin que cambiara nada en la cadena. NDX es al reves
  (3.0.6): el indice se congela a las 16:00 y el ultimo trade sigue. La regla general (`Mapear`): la vela del MNQ va a la HORA DEL SPOT
  (generado - 16 min; validado hoy en la rueda: MAD 0,0030 vs 0,0035 anclando al ultimo trade; NDX 2,72 vs 2,72); con el spot VIVO se toma
  una muestra por foto y el mapa es la mediana robusta de las ultimas 24 (3 MAD); con el spot CONGELADO (igual >= 6 min) o dato viejo, la
  ultima mediana (persistida: PythiaGex3\razon-rueda-QQQ.json / base-rueda-NQ.json; NDX lee la de la clasica si no tiene propia).
  El log por minuto de cada capa dice el origen real (en 3.0.6 seguia diciendo 'vela alineada').
- El portero (histeresis) ya no decide con el libro a medio armar: 90 s desde el ultimo armado y >= 60 % de los contratos con puntas
  (`LibroCompleto`); hasta entonces solo reubica las vigentes. Motivo: al reiniciar a las 21:08 mato a 31.520 ("no esta en el perfil") y
  metio 31.500 porque el strike todavia no tenia puntas. Log: `hist: libro a medio armar ... / libro completo`.

## 3.0.6 (06-10-2026, 21:20) - la capa NDX estaba 20 pts corrida: base de la rueda como la clasica; capas con la regla de la clasica

Pedido (21:05): "la clasica grafico la dominante en 31.511 y por algo es; eso quiero replicado en 3.0". Forense con archivos:
- Clasica, capa NDX, 20:47-20:59: base 251,10 'de la rueda hace 215 min' (PythiaGex\base-rueda-NQ.json: 17 muestras, 20:12Z), doms
  31.510,96 / 31.477,27 / 31.551,82 (una por lado + centroide 12). 3.0, misma cadena CBOE congelada: base 271,06 'vela alineada (ultimo
  trade 20:14Z)', doms 31.501,06 / 31.491,06 / 31.571,06 (strike exacto, regla de la primaria).
- La base REAL de la rueda (cadena-NQ-2026-10-06.jsonl.gz contra las velas MNQ m2 del centinela): 241-258 entre las 15:13 y las 15:55 NY,
  mediana ~250. Desde las 16:00 NY el spot_idx se CONGELA en 31.224,47 (16:00, 16:05, 16:10, 16:14 identicos) mientras el futuro siguio
  (31.485 -> 31.496): la 'alineada' a las 16:14 da 271 porque compara un futuro de las 16:14 con un indice de las 16:00. Sesgo de +20
  pts por construccion, toda la noche. No es que la clasica acerto de suerte: tenia la base bien.
- Arreglo: `AsignarBaseAditiva`: con el indice vivo (9:30-16:00 NY, dato fresco) vela alineada + una muestra por foto para la mediana
  robusta de la rueda (como MedirBaseRueda de la clasica: 24 muestras, 3 MAD, desde las 9:50); con el indice congelado o el dato viejo,
  la mediana (persistida en PythiaGex3\base-rueda-NQ.json; si no hay propia se LEE la de la clasica). Log: `capa NDX: base de la rueda
  cargada ...`, `base muestra ...`, y en la linea por minuto `base 251.10 (base de la rueda ...; indice congelado, la alineada daria 271.06)`.
- `Capa3Regla` (default Clasica: una por lado + empate 20 % + centroide 12) para que las capas dibujen lo mismo que la clasica;
  DosMasGrandes y ComoPrimaria disponibles. Con esto la NDX D1 de la 3.0 cae en ~31.511 como la de la clasica.
- Nota para el laboratorio: la 'base alineada' de libros.py (vela al ultimo_trade - spot) tiene el MISMO sesgo despues de las 16:00 NY:
  los niveles NDX de noche de la fase 2 y de INFORME_NOCHE estan corridos ~20 pts en las noches; medir la variante 'base de la rueda'.
- Lo que no cambia: es n = 1 noche con dos rechazos en 31.511-31.515 (22:25->22:27 y 23:25->23:35 UTC); el arreglo se justifica por el
  dato congelado, no por el resultado de esa noche.

## 3.0.5 (06-10-2026, 20:55) - capas QQQ/NDX visibles, D3 de capa, sesion anterior atenuada (cambio visual AVISADO con captura)

Pedido (20:45): "nos falta graficar y activar la NDX y la QQQ correctamente porque no la veo; en la clasica la grafico y reacciono".
Diagnostico en pantalla (capturas/2026-10-06_2043_antes_3.0.4_capas_tenues.jpg): las capas ESTABAN (rotulos NDX D1 31.501 ·208m, QQQ D2
31.483 ·208m) pero su estela era de 1 px, gris 158 / azul #2962ff con alfa 120-190 sobre fondo oscuro, sin linea del nivel actual, y la
estela anterior a las 18:00 NY no se dibujaba (inicio de sesion). Cambios:
- Colores: NDX turquesa (38,198,218), QQQ celeste (130,177,255); guiones de 2 px (como NQ) con alfa 230/160 (70 fuera del radio).
- `Capa3LineaActual` (true): cada dominante de capa dentro del radio de dibujo lleva una linea de 1 px desde la ultima vela hasta el eje.
- `Capa3Cuantas` (3, rango 2-3): D1/D2/D3 por capa como la clasica; la estela guardada de las capas sigue con D1/D2 (formato intacto).
- `Estela3SesionAnterior` (true): guiones y toques de antes del inicio de sesion se dibujan al 45 % en vez de esconderse (NQ y capas).
- Lo medido no cambia: NDX y QQQ por CBOE no le ganan al placebo (fase 2) y de noche estan congeladas desde las 16:15 NY (la edad va en
  el rotulo). Son capas a eleccion del operador; su pedido es verlas para juzgarlas. Pendiente de medir: "rebota en NQ y va hasta NDX"
  (H5, ping-pong entre familias) con el juez.

## 3.0.4 (06-10-2026, 18:45) - regla Tres: la clasica con histeresis (F3 V22), default nuevo AVISADO

Veredicto de la fase 2 del laboratorio (laboratorio/tres/resultados/INFORME_FASE2.md, confirmacion.md): 109 variantes en diseño
(08-09..21-09) y 8 finalistas en confirmacion (22-09..05-10) con el juez pre-registrado. NINGUNA cumple el punto 9 completo. Lo unico
que mejoro en las DOS muestras es la histeresis (F3): V22 = una por lado + empate 20 % + histeresis 25 %/5 min + centroide 6 pts.
  diseño        V22 +12,9 pp (z 2,53) pen 9,4 camb/h 3,63 | clasica +8,8 (z 1,76) pen 14,1 camb/h 8,2 | 2.0 +7,4 (z 1,34) pen 17,0 camb/h 5,9
  confirmacion  V22 +7,1 pp (z 1,47)  pen 12,9 camb/h 2,78 | clasica +1,2 (z 0,27) pen 22,7 camb/h 10,0 | 2.0 +2,4 (z 0,51) pen 16,5 camb/h 7,95
  (pide z >= 1,5: NO VALIDADA; mitades 49/53, dias+ 8/10, mismo signo). V18 (NQ+QQQ), V08 (volumen 120 min) y E4 (banda IV) INVIRTIERON.
- **Nuevo `GammaHoyTresHisteresis.cs`**: clase `Histeresis3`, calco de `F3_estabilidad.hacer_regla(modo=lado, radio=100, empate=20,
  hist_x=25, hist_n=5, perm_min=0, pos=c6)`. Un paso por minuto de reloj (el laboratorio tenia una foto por minuto); entre pasos solo
  reubica las vigentes. Vigente sigue si su strike esta en el perfil, a <= radio y con fuerza > 0 (si no, muere ya). Plaza libre: entra
  la retadora mas fuerte. Plaza ocupada: la retadora compite con la vigente MAS DEBIL y la reemplaza si la supera X % durante N pasos
  seguidos (contador a cero si falla uno). Salida D1/D2 por fuerza actual, en el centroide de 6 pts del perfil de ahora. Clave estable
  del strike (Strike.Clave) para la semana del roll. Estado vacio al cambiar de tramo: 09:30 NY (asi corrio el juez) y 18:00 NY.
  Log: `hist: entra ... (plaza libre)`, `hist: X reemplaza a Y tras 5 min con mas de 25 %`, `hist: muere ...`, `hist: retadora ... 3/5`,
  `hist: estado vacio (...)`; el AUDIT3 lleva `hist[vig=...@HH:mmZ cont=... pasos=N X= N= c=]`.
- Ajustes nuevos (grupo 2. Lectura): `Histeresis3Pct` (25), `Histeresis3Min` (5), `Histeresis3CentroidePts` (6; 0 = strike exacto).
  Con 50 / 10 / 0 es la V14, indistinguible de la V22 en confirmacion (z 1,33).
- **DEFAULT NUEVO, AVISADO EN EL MISMO MENSAJE CON CAPTURA ANTES/DESPUES (regla del 17-09)**: la regla pasa de DosMasGrandes a Tres.
  Como ATAS persiste los ajustes POR NOMBRE en el .ws, la propiedad se RENOMBRO `Regla3` -> `Regla3Dominantes` para que el default
  entre de verdad (el valor viejo guardado queda huerfano). Volver atras: Regla de dominantes -> DosMasGrandes o Clasica en el dialogo.
- Memoria: el rebobinado de viva3 pasa foto a foto por una `Histeresis3` propia (`HistMemoria`) y al terminar el vivo ADOPTA ese estado
  si casi no camino (<= 3 pasos), la memoria termino hace < 60 min y es el mismo tramo (log `memoria: histeresis adoptada ...` / `NO`
  `adoptada`). Asi un reinicio no vuelve a la clasica a secas. Las capas QQQ/NDX NO llevan histeresis (no se midio).
- Cabecera: `regla Tres (histeresis) · SIN VALIDAR`. Arranque: `regla=Tres (clasica + histeresis 25 %/5 min, centroide 6 pts; SIN VALIDAR)`.
- Pintar SIN cuenta viva (arranque, pausa de Globex 17:00-18:00 NY, libro caido) ya dibuja la estela y los toques de la memoria y las
  capas, con el precio del grafico de referencia; antes volvia antes de la estela y el grafico quedaba vacio (visto 19:02 tras el
  reinicio de ATAS en la pausa: 225 velas sembradas y nada en pantalla). Los rotulos de D1/D2/0Γ/majors siguen pidiendo cuenta.
- El rebobinado deja `PythiaGex3\rebobinado\hist-<raiz>-<dia>-<HHmmss>.jsonl` (una linea por foto: t, f, d=[D1,D2], b, n) para la
  paridad con `laboratorio/tres/paridad_hist.py <dia>` (Python = F3 V22 sobre las mismas fotos viva3, un paso por foto).
- PARIDAD MEDIDA (19:12, paridad_hist.py 2026-10-06, velas de hoy desde el centinela de la clasica porque la cache de ATAS aun no
  tiene la sesion): 94 fotos viva3 con el mismo ts; las dos rayas a <= 2 pts en 90 = 95,7 % (criterio >= 90 %: CUMPLE). Las 4 que
  divergen son el mismo D2 (31470-31480) con el precio del minuto distinto en 2 pts entre los dos lados.
- Pendiente: paridad C#/Python de la histeresis sobre viva3 (>= 90 % a <= 2 pts) y medir hacia adelante con el juez sobre las tres
  estelas del mismo dia; si en ~20 ruedas la ventaja baja de +5 pp o invierte, volver a la clasica y decirlo con el numero.

## 3.0.3 (06-10-2026, 18:00) - letra chica en las capas, edad al final, rearme al vencer

Pedido (17:55): "mas pequeñas las etiquetas de NDX y QQQ" y "confirmame que funcionara tambien en Asia y Londres".
- `Capa3LetraPct` (75): los rotulos de capa van con letra del 75 % y altura propia en la escalera anti-solape; la edad del dato pasa al
  FINAL del rotulo ("QQQ D1 ▼ 31.489 -5 ·28m"). Toca `Rotulos` (filas con letra y altura por fila) y `PintarCapas`.
- REARME POR VENCIMIENTO: las series se elegian solo al armar/recentrar (0,5 % de precio). A las 16:00 NY el 0DTE sale del libro y, sin
  rearme, quedaba UNA sola serie hasta que el precio se moviera. Ahora, con filas vencidas en el libro, se rearma cada 15 min (la API
  lista las vencidas 30 min mas) y siempre una vez cada 6 h (refresco de la lista de series). Log: `rearmo: N fila(s) de una serie
  vencida...` / `refresco periodico`. Toca `Tick`.
- Lo que NO cambia de noche (dicho al operador): el libro vivo sigue, con volumen de la sesion de Globex (desde las 18:00 NY arranca en
  cero: dominantes por volumen con poco volumen = pueden quedar lejos o saltar; regla del 17-09, sin cambiar); las capas CBOE quedan
  congeladas desde las 16:15 NY con su edad creciendo; el cartel dice MERCADO CERRADO en la pausa 17:00-18:00 NY.

## 3.0.2 (06-10-2026, 17:50) - capas QQQ y NDX, a eleccion del operador

Pedido (17:35): "habilitame igualmente el QQQ y el NDX y que yo pueda juzgar o elegir desactivarlos". Lo medido antes (laboratorio/tres,
diseño, rueda): NQ vivo +8,8 / +7,4 pp contra placebo; NDX por CBOE -2,2 / -1,0; QQQ por CBOE -1,2 / -0,8; combinaciones no le ganan a
NQ solo. La primaria sigue siendo NQ vivo; estas son CAPAS.
- **Nuevo `GammaHoyTresCapas.cs`** (clase parcial): `Capa3QQQ` y `Capa3NDX` (enum `CapaModo3` {No, Fusion, Propia}; default **Propia**
  porque el operador lo pidio para juzgarlas; el default limpio del pliego seria No), `Capa3FusionPts` (5), `Capa3Zero` (false).
  Dato: `%APPDATA%\ATAS\PythiaGex\cboe-local\ultima-<QQQ|NQ>.json` (cboe_local.py, cada 75 s; NQ = NDX); si tiene mas de 20 min, la nube
  (rama cadenas, una bajada cada 5 min como maximo). Misma cuenta (GammaHoyNucleo con los ajustes de la primaria). Al futuro: QQQ por
  razon y NDX por base aditiva, las dos con la vela del MNQ alineada al `ultimo_trade` (hora NY -> UTC); sin vela a <= 30 min, el precio
  actual y se dice "CRUDA". Rotulos con la edad REAL del dato (desde el ultimo trade de CBOE, 15 min de retraso incluidos). Estela de
  guiones de 1 px en su color (azul #2962ff QQQ, gris NDX), mas tenue que la de NQ. Fusion: si un nivel de capa coincide a <= 5 pts con
  D1/D2 de NQ, el rotulo de NQ dice "NQ·QQQ D1" y la capa no repite el chip. Los rotulos de capa no comen el tope de rotulos de NQ. Cada
  capa escribe `PythiaGex3\estela\estela-<QQQ|NDX>-<dia>.jsonl` (mismo formato) y vuelve al arrancar desde ese archivo (vida 25 min).
  Log por minuto: `capa QQQ: cboe-local generado hace N min, ultimo trade hace M min, razon X (vela alineada ...), strikes, doms, zero`.
- Solo en graficos de NQ/MNQ (en ES irian SPX/SPY: pendiente). Toca: `GammaHoyTres.cs` (Tick -> ActualizarCapas; RegistrarVelaCerrada ->
  RegistrarCapasVela; Pintar -> PintarCapas + Rotulos con extra y fusion), `GammaHoyTresMemoria.cs` (SembrarMemoriaCapas), csproj.

## 3.0.1 (06-10-2026, 17:00) - la memoria: estela guardada + rebobinado de viva3

Pedido del operador (16:55): "cuando reinicio o recargo el grafico o lo cambio de temporalidad (le puse 30 s) se borra todo lo
anterior dibujado y los anteriores si tenian memoria". Causa: la 3.0 escribia su estela en disco pero no la releia, y ATAS crea el
indicador de cero en cada cambio de marco; la clasica/2.0 releen su estela (GammaHoyCapas.CargarEstelaGuardada) y rebobinan el archivo.

- **Nuevo `GammaHoyTresMemoria.cs`** (clase parcial; `GammaHoyTres` pasa a `partial`): en el temporizador, cuando el grafico tiene velas,
  `SembrarMemoriaSiHaceFalta` toma una foto de las velas cerradas (hasta 6000) y en un hilo aparte: (1) lee `estela-<raiz>-<hoy|ayer>.jsonl`
  (lo que se dibujo); (2) rebobina `viva3-<raiz>-<hoy|ayer>.jsonl` con un segundo `GammaHoyNucleo` con los MISMOS ajustes (una cadena por
  foto, armada como `DesdeApi`; el futuro es el cierre de la vela del grafico abierta a esa hora, o el de la foto si no hay vela), solo
  donde no hay punto de archivo a menos de 90 s; (3) a cada vela cerrada sin guion le asigna el ultimo punto ANTERIOR a su cierre si tiene
  menos de 5 min (vida del nivel, como el juez del laboratorio) y rehace las marcas de toque con la misma regla del vivo (primera vela que
  entra en la banda; rearme a LEJOS). Si el vivo todavia no anoto nada, hereda el estado de rearme y sigue desde la ultima vela sembrada.
  Dos pasadas: al arrancar y 2 min despues (ATAS termina de cargar historia). Log: `memoria (pasada N): A puntos de la estela guardada +
  B rebobinados de C fotos viva3 -> D velas sembradas (desde .. hasta), T toques, S velas sin dato`.
- Nada mira adelante (la vela recibe solo puntos anteriores a su cierre); lo que no tiene dato queda sin guion antes que con uno falso.
- Toca: `GammaHoyTres.cs` (declaracion `partial`, una linea en `Tick` tras `RecalcularInicioSesion`), `PythiaGexTres.csproj`.
- Pendiente de ver en pantalla: la estela en el marco de 30 s del operador y en 2 m tras el reinicio; que los toques rebobinados coincidan
  con los que se habian dibujado en vivo.

## 3.0.0 (06-10-2026, segunda vuelta) - lo que encontro la revision adversarial, corregido

Compila `dotnet build -c Release`: 0 errores, 1 advertencia heredada (CS8321 `Cota`). Cada punto se verifico en el codigo antes de
tocarlo; los nueve eran reales. Archivos: `GammaHoyTres.cs`, `CadenaApi.cs`, `RelojNy.cs`, `SondaApi.cs`, `DISENO.md`.

- **(alta) OnCalculate sin la guarda del borde vivo.** Anotaba la estela, los toques y el centinela en CADA barra con indice mayor a la
  ultima vista: en un recorrido historico posterior a la primera cuenta (reconexion de Rithmic, cambio de marco, pestaña que vuelve)
  escribia velas viejas con los niveles de AHORA (dato falso para el laboratorio). Ahora `OnCalculate` solo actua con
  `bar == CurrentBar - 1` (la guarda de la 2.0, GammaHoy.cs) y delega en `RegistrarVelaCerrada`. La guarda interna es por HORA de la
  vela (`_ultimaVelaUtc`) y la estela/toques van indexados por hora de vela, no por numero de barra: una reindexacion no repite ni
  corre los guiones; `Pintar` traduce las barras visibles a su hora con `GetCandle`. El centinela recibe un contador monotono en vez
  del numero de barra (solo lo usaba como guarda). Si la cuenta tiene mas de 3 min, la vela va al centinela con `niv` vacio y `spot`
  null, sin guion.
- **(alta) El 0DTE vencido se contaba 30 min con `dias = 0` y T = 1 min** (heredado letra por letra de CadenaViva.cs:1158-1160): entre
  16:00 y 16:30 NY las dominantes y el zero salian de un vencimiento muerto, la trampa de CLAUDE.md. `CadenaApi.Filas` deja afuera
  toda fila con `dias <= 0` (la suscripcion conserva la gracia de 30 min; la trimestral de la mañana sale a las 9:30 por la misma
  regla). Se cuentan en `Foto().Vencidas` y en la linea `minuto:` del log (`vencidasFuera=`). Consecuencia buscada: en esa media hora
  la 3.0 y la 2.0 difieren a proposito, y el viva3 tampoco lleva esas filas.
- **(media) Estela y centinela solo por OnCalculate** (pestaña oculta = nada anotado, medido 15-09): el temporizador llama
  `RegistrarVelaCerrada(CurrentBar - 2)` cada 5 s; la guarda por hora evita el doble registro con OnCalculate. Si hubo un hueco largo,
  las velas del medio quedan SIN guion antes que con uno falso: solo se anota la ultima cerrada.
- **(media) Marca de toque en toda vela dentro de la zona** (fila de bloques con el precio apoyado en D1). Ahora marca SOLO la primera
  vela que ENTRA en la banda (la anterior no estaba) y un toque por (dominante, lado) hasta que una vela cierre a >= LEJOS del nivel
  (rearme), como el pre-registro (`laboratorio/tres/PRE_REGISTRO.md` seccion 3). Ajuste nuevo `Lejos3Pts` (0 = auto 15 NQ / 4 ES /
  3 RTY). La ventana de 30 min del "viniendo de lejos" del juez no esta en el dibujo (dicho en DISENO).
- **(baja) Trazabilidad de la ZONA.** La descripcion decia "ZONA medida (PLAN_3_0 F3)" y F3 mide otra cosa (`zona_sugerida_p75` 45,
  penetracion p75 de noche 20,5 en NQ vivo). Los 2,5 / 0,75 son la TOL del pre-registro: ahora el ajuste y el DISENO lo dicen asi.
- **(baja) "LIBRO VIVO CAIDO" sin cotizacion.** Con el mercado cerrado segun el reloj de NY (sabado, domingo < 18:00, pausa
  17:00-18:00; `RelojNy.MercadoAbierto`, feriados NO contemplados) el cartel dice `MERCADO CERRADO (NY dia hh:mm) · ultimo dato hace N`
  en gris, no CAIDO en naranja. Y el Summary inicial de `Subscribe` ya no marca `UltimoUtc` (puede ser cache): la edad del dato queda
  en `--` hasta el primer `Changed` real. La edad en horas/dias se lee (`3 h 20 min`, `2 d 5 h`).
- **(baja) Nombres repetidos de la 2.0 en la sonda** (`RaizManual`, `TopeContratos`, `GuardarViva`, ...): todos los ajustes de
  `SondaApi` pasan a `Sonda3*`. Lo guardado hoy en el .ws para la sonda vieja no se hereda (eran los defaults, no se perdio nada).
- **(baja) Chip de flecha mas angosto que su texto:** el ancho de la columna se mide con el prefijo de flecha incluido.
- **(baja) Cabecera dibujada con `Ver3Cabecera = No`** mientras no habia cuenta: ahora en No solo se dibuja el cartel de estado (es
  lo que explica el vacio), nunca el renglon.
- Pendiente de ver con ATAS corriendo (lo instala el orquestador): que el guion por hora caiga en la vela correcta en 1m/2m/5m, que el
  rearme del toque deje UNA marca con el precio apoyado en D1, y que el centinela tenga filas de los minutos en pestaña oculta.

## 3.0.0 (06-10-2026) - "PythiaGex 3.0 - Gamma Hoy": la misma cuenta, la cadena por la API, el dibujo minimo

String de arranque: `Gamma Hoy 3.0.0 arranca. instrumento=... raiz=... regla=... perfil=...`. `<Version>3.0.0</Version>`.
Compila con `dotnet build -c Release`: 0 errores, 1 advertencia heredada (CS8321 `Cota` sin usar en GammaHoyNucleo.cs, copia textual
de la 2.0). Pedido del operador (06-10): "que quede funcionando ya ... para contrastar en tiempo real" contra la clasica y la 2.0.

- **Por que**: ATAS 8.0.15.302 limita las suscripciones a opciones hechas por fuera de su API (200 contratos, 2 consultas/min) y el
  libro vivo de la 1.x/2.0 quedo en 84-86 strikes con huecos (log de la plataforma del 06-10). La via nueva (`CadenaApi.cs`, sonda
  verificada el mismo dia) no cuenta en ese cupo. El censo de 273 capturas (`laboratorio/tres/resultados/censo_capturas.md`) midio
  ruido 3,93 en la 2.0 contra 2,5 en la referencia y pidio "primero sacar, despues embellecer".
- **Nuevo** `GammaHoyTres.cs` (indicador), `Feed.cs` (Fila/Cadena minimas), `GammaHoyNucleo.cs` (copia textual de 2.0, namespace
  PythiaGexTres), `Centinela.cs` (copia de 2.0, prefijo `pythiagex3-centinela-`), `Viva3.cs` (el archivo viva3 factorizado de la sonda,
  escritor unico por raiz). `SondaApi.cs` pasa a escribir el viva3 por `Viva3.Guardar` (misma linea, sin duplicar con Gamma Hoy).
  `PythiaGexTres.csproj` lista los archivos nuevos. `DISENO.md` suma la seccion "Gamma Hoy 3.0".
- **La cuenta**: GammaHoyNucleo sin tocar; cadena desde `CadenaApi.Filas()` (call y put por strike y vencimiento, IV Black-76 del punto
  medio, OI, vol_hoy; Dias con hora NY; EsFuturo; sin base); futuro = precio del grafico (MNQ = NQ en puntos); Horizonte Hoy; radio
  100 NQ / 25 ES; empate 20; DominantesDeNoche = Volumen (regla del operador, no se toca); ZeroInterpolado. Regla de dominantes
  `ReglaDominantes3 { Clasica, DosMasGrandes (default), Tres (reservada = DosMasGrandes, lo dice el log) }`. `AUDIT3 regla=<regla> ...`
  una vez por minuto en `pythiagex3-gammahoy.log`, con la linea `Audit` del nucleo mas los conteos de la API.
- **El dibujo** (pliego seccion 5, perfil Limpio): estela de guiones por vela para D1/D2 (2 px, amarillo, alpha 100/60/30 %), un zero
  punteado gris, tunel al 12 % entre D1 y D2 si estan cerca, marca de toque (una vela de ancho, 2 ticks de alto) en la vela que llego a
  <= ZONA (2,5 NQ / 0,75 ES), columna de rotulos `NQ D1 ▲ 31.545 +12` en escalera con tope 5 (hasta 7) y flechas para lo que esta fuera
  de pantalla, cabecera de un renglon con la edad del dato ANTES de los numeros (naranja si > 30 min) y cartel de estado de una linea
  con color (SIN CADENA rojo / LIBRO VIVO CAIDO naranja). Majors apagados en NQ y prendidos en ES (1 px, sin raya fuera del radio).
  Barras del perfil: ninguna por defecto, opcion izquierda (12 % del lienzo, alpha 50 %, sin pelotitas ni montos). Perfil visual
  `Limpio | ConMajors | Todo` que fija los defaults de las casillas (tri-estado SegunPerfil/Si/No) sin tocarlas una por una. Nada de
  pelotitas, gatillos, capas, rombos, nubes, perfil derecho ni textos de depuracion.
- **Archivos propios** con los formatos de siempre: `PythiaGex3\estela\estela-<raiz>-<dia>.jsonl` (como GuardarGuionCapa, d =
  [D1,D2,zero]), `pythiagex3-centinela-hoy-<instrumento>-<marco>.jsonl` (una linea por vela cerrada, formato 2.0),
  `PythiaGex3\viva\viva3-<raiz>-<dia>.jsonl` (cada 60 s). Escritor unico por raiz para estela y viva.
- **Nombres**: todas las propiedades llevan "3" (`Regla3`, `Perfil3Visual`, `Ver3Estela`, ...) porque ATAS persiste los ajustes por
  nombre en el .ws y un nombre repetido heredaria valores de la 2.0.
- **Lo que no hace**: CBOE de respaldo, regla Tres, MES sin opciones de ES por API, capas, tooltip didactico, regimen en la cabecera.
  Falta verlo en pantalla con ATAS corriendo (lo instala el orquestador): ruido del default con el mismo juez, estela a 2 px en 1m/2m/5m.
