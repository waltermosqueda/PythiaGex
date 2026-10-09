# Absorcion Viva 1.0 — EN PRUEBA, no validado

Indicador de ATAS que dibuja **pelotitas sobre el precio exacto donde el mercado se esta comiendo
agresion sin moverse**. Es el pedido del operador: "algo similar o mejor a Big Trades pero para
absorcion en tiempo real", con la forma de la captura que mando
(`laboratorio/dom/ronda10_absorcion/referencia-visual-bigtrades.png`).

**No es una senal y no dice para donde va el precio.** Ver el apartado "Lo que esto NO afirma".

---

## 1. Que dibuja, en criollo

**La pelotita llena** — la marca que vas a mirar. Se prende cuando en UN MISMO precio el mercado
se comio una pila de contratos agresores sin dejar que el precio pase de ahi: hay alguien parado
en limite y va reponiendo. El **tamano** te dice cuanto se comio y el **color** te dice a quien:

- **verde** = se comieron COMPRAS agresoras (hay un vendedor aguantando ahi)
- **rojo** = se comieron VENTAS agresoras (hay un comprador aguantando)

Mientras el nivel siga aguantando, la pelotita **sigue creciendo en vivo**. Eso es justo lo que el
footprint solo no te puede mostrar: el footprint te dice cuanto se opero en ese precio, no si la
oferta se repuso.

**El aro fino** — iceberg. El nivel mostraba tres o cuatro contratos en el DOM y termino
comiendose cuarenta veces eso. Lo que se ve en el libro no era lo que habia. Sale poco, unas
cuatro veces por sesion, y cuando el mismo nivel ademas es pelotita, el aro se dibuja **alrededor
de la pelotita**, no como una marca aparte. El `x46` al lado es cuantas veces lo repuso.

**El circulo punteado** — agresor atrapado. **Apagado de fabrica.** Marca el barrido que salio
mal: alguien se llevo trescientos contratos por delante, corrio el precio cuatro puntos, y en
menos de medio minuto el precio estaba de vuelta donde arranco. El circulito se pone en el precio
**extremo** del barrido, que es donde quedaron colgados los ultimos que entraron.

### El aviso del color, que hay que leer antes de ponerlo

En el **Big Trades** que ya tenes puesto, verde tambien quiere decir "hubo compra" — pero ahi la
compra **se salio con la suya**. Aca el verde es una compra que **perdio**. Son dos pelotitas del
mismo color en la misma pantalla con la lectura al reves. Por eso los ajustes no se llaman "color
verde" y "color rojo" sino con la frase entera, y hay un **"Dar vuelta los colores"** en un clic
por si te resulta mas comodo pintar por el lado pasivo (que es como pinta buena parte del mundo).

---

## 2. Lo que esto NO afirma

- **No predice nada.** La ronda 10 midio estas mismas familias con el juez de la ronda 9 (entrada
  el segundo siguiente cruzando el spread real, sin solapes, stop de 100 USD, costo verificado de
  0,948 puntos la vuelta) y dio **0 de 24 combinaciones con neto positivo**, la mejor -0,154.
- El mejor `t` de toda la familia fue **+2,59**, por debajo del techo **2,65** que regala el azar
  solo por haber probado 143 veces; y se cae a **+0,29** en MNQZ6, que es el contrato que se opera
  hoy.
- **El nivel no aguanta**: 0 de 18 variantes cruzan menos que un nivel al azar a la misma
  distancia a 5 minutos, y a 30 minutos el 98 % ya se rompio. La pelotita es una **foto del
  momento**, no un soporte.
- El antecedente es peor todavia: la absorcion POR VELA que ya tenia Flujo Claro dio 46 % en
  contra del delta con n=24 (memoria `produccion-en-vivo-2026-09-06`, "la absorcion es una prueba
  degenerada"). Esta ronda mide al nivel de PRECIO y por EVENTO, que es otra cosa, pero el
  antecedente obliga a ser duro.

El rotulo **"EN PRUEBA: no validado"** esta pegado arriba del grafico y **no se puede apagar**.

---

## 3. Los tres archivos

- **`AbsorcionNucleo.cs`** — el nucleo. Codigo puro: no conoce ATAS, no dibuja, no escribe
  archivos. Come la cinta orden por orden y escupe eventos `{hora, precio, tamano, lado, tipo}`.
- **`AbsorcionViva.cs`** — la capa de ATAS. Recibe en vivo por `OnCumulativeTrade` y pinta.
  Solo sabe de `Evento`: no conoce ninguna regla de absorcion.
- **`arnes/`** — consola, sin ATAS. Compila **el mismo** `AbsorcionNucleo.cs` y lo corre sobre las
  cintas grabadas, para comparar contra el Python que produjo las mediciones.

### Para que despues salga el delta sin reescribir nada

El operador pidio despues "algo similar tambien para el delta". La arquitectura ya esta:

```
IFuenteDeEventos.Agregar(in Orden)  ->  Evento { Tipo, Ns, PrecioTk, Lado, Tamano, ... }
```

`Bateria` junta varios detectores como si fueran uno. Para el delta alcanza con escribir
`DeltaNucleo : IFuenteDeEventos` que devuelva eventos con `Tipo = 10, 11...`, agregarlo a la
bateria en el constructor del indicador y darle un color y una escala. **La capa de dibujo no se
toca.** El delta es una ronda aparte y NO se hizo ahora.

---

## 4. De donde sale el dato, y por que no puede colgar ATAS

De `OnCumulativeTrade`: la misma via por la que el Big Trades oficial de ATAS recibe las
operaciones, y **la misma con la que la sonda grabo las 37 sesiones de MNQ**. Cada
`CumulativeTrade` trae adentro la mejor punta ANTES (`PreviousBid`/`PreviousAsk`) y DESPUES
(`NewBid`/`NewAsk`), que es todo lo que la definicion necesita.

- **No se suscribe a profundidad ni a MarketByOrder.** Eso fue lo que corto la conexion de
  Rithmic 166 veces el 16-09; aca no puede pasar porque no hay ninguna suscripcion nueva.
- **No lee historia al arrancar.** Esta medido que ATAS, para una fecha pasada, devuelve la sesion
  COMPLETA aunque se le pida media hora (414.184 ordenes por tramo). Pedir eso al abrir el grafico
  es colgar la plataforma. El indicador arranca vacio, se llena hacia adelante, y el rotulo dice
  **"CALENTANDO"** los primeros 120 segundos.
- **Nada pesado en `OnCalculate`**: esta vacio a proposito (ATAS lo llama por cada vela en cada
  recalculo; ahi adentro se cuelga la plataforma). La vela de cada marca se resuelve al dibujar,
  por busqueda binaria sobre la hora.
- **Nada pesado en `OnCumulativeTrade`**: en MNQ llegan hasta 426 ordenes por segundo. Ahi adentro
  no hay archivos, ni LINQ, ni repintados. El repintado va desacoplado, cada 250 ms, y solo si hay
  algo nuevo. El CSV lo escribe el latido, no el hilo de la cinta.
- **Nada que crezca con los dias abiertos.** Los niveles del otro lado del precio no se borran
  hasta que el mercado les pasa por encima, asi que el diccionario de niveles vivos se llena si
  ATAS queda abierto toda la semana. MEDIDO sobre 11 sesiones seguidas de MNQ sin reiniciar
  (7.374.727 filas de cinta): los niveles vivos pasaron de 377 a 6.660. Con el barrido viejo, que
  recorria el diccionario entero, el trabajo por operacion crecia con el: de 87 entradas por fila
  de cinta el primer dia a 201 el ultimo, todas en el hilo de la cinta. Ahora los niveles estan
  ademas en una cola ordenada y el barrido se lleva la cola sin mirar el resto, asi que el costo
  por operacion **ya no crece**. La memoria sigue subiendo y no importa: 1,3 MB a los once dias.

### Esto se cambio DESPUES de la paridad, y se volvio a probar

El cambio del barrido toca el nucleo, que es el archivo que la paridad compara. Se volvio a correr
entera y ademas se compararon los censos **byte por byte** contra los de antes del cambio: los tres
SHA-256 dan igual, o sea que se emiten exactamente los mismos episodios, con las mismas 16 columnas
y en el mismo orden. Se probo tambien en una cuarta sesion que no esta en la paridad (2026-09-21,
la de 959 puntos de rango, que es la que mas niveles vivos junta): censo identico y 1,38 s -> 1,12 s.

---

## 5. Como se ubica la marca (los cinco requisitos del pedido)

1. **Precio exacto.** La `y` es `GetYByPrice(precio del nivel)`. No es el cierre de la vela, ni el
   maximo, ni el minimo: es el precio de la punta que aguanto.
2. **Cualquier temporalidad.** No se guarda el indice de vela en ningun lado — guardar el indice
   es un bug latente porque ATAS corre los indices cuando carga mas historia. Se guarda la HORA y
   la vela se busca en cada dibujo, entre las visibles, por busqueda binaria sobre
   `GetCandle(i).Time`. Anda igual en M1, M5, M30, ticks, rango y footprint, porque no supone que
   las velas duren lo mismo.
3. **Dentro de la vela, en el instante exacto.** `x = borde izquierdo + fraccion x ancho de la
   vela`, donde la fraccion sale de comparar la hora del evento con la apertura de la vela
   SIGUIENTE (no con una duracion nominal: por eso anda en graficos de rango y de ticks). Para la
   vela que todavia se esta formando se usa el ancho tipico de las ultimas diez cerradas. Hay un
   ajuste para ponerlas al medio de la vela.
4. **No se pisan.** Dos marcas del mismo tipo, mismo lado, misma vela y a 2 ticks o menos se
   dibujan como UNA sumando los contratos. Y hay un tope de 6 por vela que se queda con las mas
   grandes, avisando en el rotulo cuantas escondio. **Nunca se corre una marca de su precio**:
   desplazar en `y` mentiria sobre el precio y desplazar en `x` mentiria sobre el momento.
5. **Todo editable**, con nombres en espanol y con el numero medido adentro de la descripcion de
   cada ajuste.

### La hora: no convertir nunca

`CumulativeTrade.Time` y `IndicatorCandle.Time` **ya son UTC**, aunque el segundo venga con
`Kind=Unspecified`. Se comparan crudas. Si alguien "arregla" el codigo llamando a
`ToUniversalTime()`, las pelotitas se van tres horas a la izquierda. Ya paso
(`GammaHoyCapas.cs:630`).

---

## 6. La definicion, exacta

### Pelotita y aro: EPISODIO DE NIVEL

Se acumula toda la agresion que pega contra **el mismo precio de punta**. El nivel **muere**
cuando el mercado lo atraviesa por mas de **4 ticks** (1 punto de MNQ) a favor del agresor, o
cuando pasan **60 segundos** sin que la punta vuelva a pararse ahi. Que la punta se vaya y vuelva
**no lo mata**: esa es toda la diferencia con la "posta estricta" y es lo que permite que la
pelotita crezca.

Orden de cada fila, y esto es lo que la hace **causal**:

1. llega la foto de la punta ANTES: mueren los niveles atravesados, se abre o continua el nivel de
   ese precio, se suma la liquidez que de verdad es nueva;
2. **se chequea el encendido**, con todo lo anterior y **nada** de esta fila;
3. recien ahi se suma el consumo de esta fila.

Las agresiones que **caminaron el libro** (el primer print y el ultimo a distinto precio) suman al
nivel solo hasta el tamano que habia ahi: no se sabe cuanto del volumen quedo en cada precio. Son
el 10,45 % de las filas en MNQ y el 0,98 % en MES.

**Por que no alcanza con mirar una sola fila** (medido, `r10_02b_salida.txt`): adentro de una misma
agresion el tamano de la punta DESPUES es exactamente el de ANTES menos el volumen, en el 100,0 %
de 129.896 filas de MNQ y el 99,8 % de 55.278 de MES. La foto "despues" no agrega nada. Y una
agresion sola mas grande que la punta casi nunca deja el precio quieto: 0,2 % de 84.105 casos en
MNQ, 0,0 % de 10.789 en MES. **La absorcion solo aparece encadenando filas.**

**Por que no alcanza con la posta estricta** (cortar el episodio apenas la punta se mueve): da
1.432.489 postas por sesion pero solo 20 con 100 contratos comidos o mas y 2 con 200 o mas. Con
esa definicion la pelotita **no puede crecer nunca**. El episodio de nivel da 110 con 100 o mas.

### Circulo punteado: AGRESOR ATRAPADO

Racha de agresion de un solo lado con hueco de 2 s o menos entre operaciones. Se acumula el
volumen y los ticks que corrio el medio desde antes de arrancar. Si la racha llega a **300
contratos** y corrio **16 ticks** (4 puntos), queda armada. La marca se enciende recien cuando el
medio **vuelve** al punto de partida o mas atras, y solo si eso pasa dentro de los **30 s**.

---

## 7. Los defaults, y de donde salen

Todo lo de abajo es **MNQ**. En **MES** el mismo numero saca 10 a 20 veces mas marcas: hay que
subirlo, y hay que avisarselo al operador en el mismo mensaje en que se le instale.

**Umbral partido dia/noche: 150 contratos en la rueda (09:30–16:00 NY), 90 fuera.** Con un solo
numero para las 23 h, 100 contratos dan 17,85 marcas por hora de dia y 0,73 de noche: una sopa y
un desierto. El reloj de la franja usa hora de Nueva York, no la de la PC, y la regla del horario
de verano esta escrita a mano en el codigo (no depende de la zona del sistema operativo).

**Escala del diametro: 10 px a 75 contratos, 26 px a 250 o mas, lineal en el medio.** Lineal y no
proporcional al area a proposito: el rango real dibujado son unas 3 veces en contratos, y con raiz
cuadrada quedarian 1,8 veces de diametro, que a ojo no se distingue. Las pelotitas del Big Trades
de su captura miden entre 18 y 22 px; con esta escala la marca tipica (153 contratos medidos) cae
en 17 px.

**Opacidad 60 %, sin borde, encima de las velas** — es lo que se ve en la captura: la vela se ve
por abajo.

**El numero de contratos solo en las marcas de 200 o mas.** Asi el grafico queda limpio y el numero
aparece justo cuando la pelotita ya se clavo en el diametro maximo y el tamano dejo de informar.
El aro siempre lleva su `x46`.

**Tope de 6 marcas por vela y fusion a 2 ticks.**

**Iceberg: aguante 40 veces, con el nivel mostrando 3 contratos o mas.** Con 20 salen 107 marcas
por sesion (ilegible), con 30 salen 19, con 40 salen 4.

**El agresor atrapado arranca APAGADO**, por tres razones medidas y ninguna de gusto:

1. la marca sale cuando el precio vuelve, con demora mediana de 3 s y p99 de 29 s;
2. el informe que la proponia publicaba "t 2,30 y 70 % de sesiones" y **esos numeros eran del
   bruto**: el neto tiene t 0,77 y 47,8 % en su propio archivo (`r10_f6_04_salida.txt`), y en
   MNQZ6 el bruto es -0,606;
3. duplica la densidad en pantalla.

Lo unico que aguanta de ella es descriptivo: el 83,1 % de los barridos que califican vuelven al
punto de partida en menos de 60 s, contra el 74,4 % desde un momento cualquiera con el mismo
movimiento previo. O sea que es **casi la regla, no una rareza**.

---

## 8. Cuanto se ve en pantalla — medido con EL CODIGO QUE SE ENTREGA

Corriendo el arnes con los defaults de fabrica sobre las 23 sesiones de trabajo de MNQ
(`laboratorio/dom/ronda10_absorcion/medicion/r10_p_03_salida.txt`, 21 cintas enteras):

- **24 marcas por sesion** de mediana (21 pelotitas + 4 aros), minimo 17 y maximo 137.
- **2,15 por hora en la rueda** y **0,67 por hora fuera**.
- Contratos de la marca tipica: **153**.
- Una pantalla de M5 muestra hora y media: **3,2 marcas en la rueda, 1,0 de noche**. La captura
  que mando el operador tiene 4 pelotitas del Big Trades en 1 h 40.
- Amontonamiento: maximo **5** marcas en una vela de M1, **8** en M5, **13** en M30. La fusion a
  2 ticks muerde el 4,6 % / 6,2 % / 8,3 %, y el tope de 6 esconde el 0,0 % / 0,3 % / **6,0 %**.
  O sea: en M1 y M5 practicamente no tapa nada, y en M30 esconde 6 de cada 100.
- Las dos cintas raras van aparte: 2026-09-22 (cinta cortada de 5,2 h, solo Asia) da 4 marcas y
  2026-09-14 (el roll, con el libro partido en dos contratos) da 3.

**Por que estos numeros son un 10 % mas bajos que los del informe previo.** El informe conto los
episodios por su tamano FINAL (`comido >= 150` al cerrar). El indicador solo puede encender la
marca con los contratos **ya comidos**, nunca con los del mismo instante. Medido en 2026-09-18:
284 episodios terminan con 100 o mas contratos, pero solo **255 se encienden**. La diferencia es
causalidad, no un bug, y esta demostrada en la paridad.

---

## 9. La paridad C# contra Python

Exigencia: **100,000 %**. Salidas en `laboratorio/dom/ronda10_absorcion/paridad/`.

**`r10_p_01_paridad.py`** — el mismo `AbsorcionNucleo.cs` compilado en el arnes, corrido sobre la
cinta CRUDA, contra `r10_f4_lib.py:_niveles` y `r10_f6_lib.py:barridos`, que son el Python con el
que se midio toda la ronda. Tres sesiones, **una de ellas de la RESERVA** (2026-08-05, que nunca
se miro al elegir nada), mas 2026-09-02 y 2026-09-18:

- **34.326 episodios de nivel**, comparando las **16 columnas** de cada uno (hora de apertura y de
  cierre, indices de fila, tick, tamano visible al empezar, contratos comidos, las dos cuentas de
  reposicion, el control del lado pasivo, las tres cuentas de filas y los dos instantes de
  encendido): **100,000 %**.
- **47 eventos de agresor atrapado**: **100,000 %**.

**`r10_p_02_franja.py`** — el corte dia/noche es una funcion pura de la hora, asi que se prueba
solo y se prueba entero: **1.051.200 instantes** (un minuto, dos anios, los cuatro cambios de
horario adentro) contra `America/New_York`: **100,000 %**.

### La trampa que aparecio esta vez, y donde estaba

En la ronda 9 la unica diferencia de paridad la producia el **exportador**, que escribia 6 cifras
significativas con precios de 7. Esta vez aparecio del otro lado: **pandas lee los float con un
parser rapido que se equivoca en el ultimo bit**. De 5.905 episodios, 832 volvian distintos *solo
por leerlos del CSV*. Se arregla con `float_precision="round_trip"`. El nucleo no tenia nada.

Por eso las dos puntas leen **el CSV crudo de la sonda** y no un export intermedio.

---

## 10. El registro, y como se mide despues

El indicador anota **todos** los eventos, se dibujen o no, en

```
%APPDATA%\ATAS\PythiaGex2\absorcion\absorcion-<fecha UTC>.csv
```

Una fila cuando la marca **nace** (con los contratos que tenia al encender) y otra cuando el nivel
**muere** (con el total). Columnas: hora UTC, instrumento, marco temporal, version, momento, tipo,
nombre, lado, precio, precio en ticks, contratos, contratos al encender, tamano visible inicial,
aguante, filas, ticks del barrido, espera, umbral vigente, si estaba en la rueda, hora de inicio
del episodio y si se dibujo.

Para medirlo:

```
python laboratorio/dom/ronda10_absorcion/r10_p_medir.py
```

Pasa el registro por el juez de la ronda 9 (entrada el segundo siguiente cruzando el spread real,
sin solapes, stop de 50 puntos, costo 0,948) y por los dos controles que corresponden: **placebo
apareado** por sesion, franja de 2 h y lado, y **correr el resultado en circulo** dentro de cada
sesion — rotar, no barajar: barajar rompe la autocorrelacion y por eso infla la significacion (la
trampa de la ronda 9). Mide las dos tesis, a favor del pasivo y a favor del agresor, porque el
order flow describe el comportamiento del movimiento, nunca la direccion.

Con el mercado cerrado se puede probar la maquinaria sobre cinta grabada:

```
python r10_p_medir.py --grabado 2026-09-02 2026-09-18
```

Eso **no es** la prueba hacia adelante: esas sesiones ya se miraron. Y con menos de 8 sesiones el
`t` no significa nada, por grande que salga — el script lo avisa solo.

---

## 11. Lo que NO se puede, y hay que decirlo

- **No se puede saber quien repuso.** Sin MarketByOrder no se distingue un iceberg de una orden
  limite nueva. Lo unico que la cinta prueba es "volvio a haber tamano en ese precio". El aro dice
  eso y nada mas.
- **No se ve mas alla del nivel 1.** Lo que trae el `CumulativeTrade` es el mejor bid y el mejor
  ask. Un iceberg escondido detras no aparece.
- **No es instantaneo del todo.** Un `CumulativeTrade` se sabe cerrado recien cuando llega el
  siguiente: mediana 31 ms en MNQ, p99 1,2 s, maximo 9,5 s. Es tiempo real en el sentido util,
  pero el numero va declarado, no escondido.
- **No hay historia al abrir el grafico**, por lo del apartado 4.
- **Lo que se midio es sobre cinta GRABADA** (pedido historico) y el indicador lee **streaming**.
  Que `PreviousBid`/`PreviousAsk` vengan poblados igual por las dos vias es un **supuesto
  razonable pero NO medido**: mismo tipo, mismo conector. Hay que confirmarlo con el mercado
  abierto, grabando un rato en vivo y comparando contra el mismo tramo pedido como historico.
  Hasta entonces, ninguna densidad de este README se puede presentar como "asi se va a ver".

---

## 12. Trampas conocidas de este proyecto que aplican aca

- **ATAS guarda los ajustes POR NOMBRE en el workspace (.ws).** Todas las propiedades llevan el
  prefijo `Abv` por eso. Para pisar un default que ya quedo guardado **no alcanza con cambiarlo en
  el codigo: hay que RENOMBRAR la propiedad.** Es la trampa del 16-09 que dejo a Rithmic cortando
  cada 62 segundos.
- **Regla del operador (17-09):** ningun cambio de default que toque lo que se dibuja sin avisarle
  en el mismo mensaje y sin captura antes/despues.
- **ATAS se traga las excepciones de los indicadores**: un fallo se ve solo como un indicador que
  no dibuja. Todo va envuelto en `try/catch` que escribe a
  `%APPDATA%\ATAS\pythiagex2-absorcion.log`.
- **El DLL no se instala desde el laboratorio.** Se compila con
  `dotnet build -c Release` y queda en `bin/Release/AbsorcionViva.dll`. Copiarlo a
  `%APPDATA%\ATAS\Indicators` lo hace el agente principal, con el OK del operador y con captura
  antes y despues.

---

## 13. Lo que falta

1. **Mirarlo en pantalla con el mercado abierto**, con captura antes y despues. El posicionamiento
   esta calculado contra la API real pero **no visto**.
2. **Confirmar que la punta llega igual en streaming que en el pedido historico** (apartado 11).
3. **Calibrar los umbrales para MES** antes de ponerlo en un grafico de MES.
4. **Diez sesiones de registro** y despues `r10_p_medir.py`. Recien ahi hay algo que discutir.
5. El **delta**, como ronda aparte, reusando `IFuenteDeEventos`.

---

## Como se compila

```
cd atas/AbsorcionViva
dotnet build -c Release            # el indicador  -> bin/Release/AbsorcionViva.dll
cd arnes
dotnet build -c Release            # el arnes      -> arnes/bin/Release/net10.0/AbsorcionArnes.exe
```

Los ensamblados de ATAS **no se copian** (`Private=false`): la plataforma ya los tiene cargados y
copiarlos haria que el indicador use una copia distinta de los mismos tipos.

Proyecto **nuevo y aparte**: la produccion `atas/PythiaGexNiveles` no se toca (regla 18-09) y el
clon `atas/PythiaGexDos` tampoco.
