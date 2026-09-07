---
name: volumen-opciones-en-vivo
description: "ARREGLADO el 2026-09-06: el volumen de opciones por strike llega por SecuritySummaryChanged y NewTrades del conector (no por Security.PropertyChanged, que nunca dispara). Es el único dato del mapa que no es de ayer — ni GEXbot lo tiene."
metadata: 
  node_type: memory
  type: project
  originSessionId: 15e03ce1-51d8-43ea-8c35-a2fa1a4b8145
  modified: 2026-09-04T00:48:44.195Z
---

Implementado el 2026-09-03. **Es el punto ciego de todos los tableros de GEX.**

## ARREGLADO Y MEDIDO (2026-09-06, 22:33 AR)

El acumulador estaba colgado de `Security.PropertyChanged`, que el conector de
Rithmic **nunca dispara** (tres noches en cero). Volcando la API por reflexion
(`atas/_api --dll OFT.Rithmic --eventos ...`) aparecieron los dos eventos por
donde SI viaja el dato, en `IDataFeedConnector`:

- **`SecuritySummaryChanged`** entrega un `SecuritySummary` con
  `CurrentDayTotalVolume`, `PrevDayTotalVolume`, `OpenInterest`,
  `SettlementPrice`, maximo/minimo del dia. Es exactamente lo que consume el
  Options Board de ATAS (`OptionModel.ProcessSummary`). Incluye lo operado
  ANTES de suscribirse.
- **`NewTrades`** entrega cada operacion con `Security`, `Price`, `Volume`,
  `Time` y **`OrderDirection`** (Buy/Sell/Between): el lado del agresor, sin
  Lee-Ready.

Los dos llegan para cualquier contrato suscrito con `Prints|Summary`.

**Medido dos minutos despues del reinicio, domingo de noche (Asia):**

```
ES  (180 contratos)  volresumenes=46363  volresconvol=114  volresconoi=175
                     flujoviva=3842 contratos del dia   voltradesprop=5  flujocinta=10
MNQ (142 contratos)  volresumenes=3114   volresconvol=4    volresconoi=99
```

`flujoviva` toma el volumen del resumen si llego y si no el de la cinta;
`flujocinta` es solo lo visto desde la suscripcion. Cada `Fila` de la cadena
viva trae ahora `VolumenDia`, `VolCinta`, `VolCompra`, `VolVenta` y `OIResumen`.

**Lo que todavia no se hizo con el dato:** dibujarlo por strike y probarlo en
el laboratorio contra placebo (la formula "volumen de hoy" fue la unica que
gano el 2026-09-03, pero con volumen de CBOE 15 min tarde). Esto es el mismo
dato en vivo: hay que registrarlo hacia adelante.

## HISTORIA: como estaba roto (2026-09-03 y 04)

## Por qué importa

Todos los tableros —este incluido, y GEXbot también— construyen sobre el
**interés abierto**, que la OCC consolida **de noche**. O sea que el mapa es
siempre el de ayer, y eso vale igual pagando lo que se pague: **interés abierto
intradía no existe**.

Lo que sí existe es el **volumen de hoy**, contrato por contrato, y llega en vivo
por Rithmic. Un strike con mucho volumen hoy es donde se están armando o cerrando
posiciones **ahora**: ahí el mapa de mañana va a ser distinto del de hoy.

No reemplaza al mapa de gamma. Le agrega lo único que le falta: el presente.

## Cómo se captura, y por qué así

`Security` **no tiene** campo de volumen acumulado de la sesión. Se volcó su API
con `atas/_api` y sólo hay `LastTradeVolume`, que es el de la **última**
operación. Hay que sumarlo a mano.

**Por evento, no por sondeo.** `Security` implementa `INotifyPropertyChanged`
(verificado en el volcado). Con sondeo cada N segundos se perderían todas las
operaciones entre dos consultas, que en un 0DTE al dinero son casi todas.

**Y con control de duplicado.** `PropertyChanged` puede dispararse más de una vez
por el mismo print —si precio y volumen cambian en avisos separados— así que se
compara contra la última operación vista (precio **y** volumen). Sin eso el
volumen del día sale inflado.

## Dónde mirarlo

En el renglón `AUDIT` del log, dos números **distintos** y por eso con nombres
distintos:

```
flujoviva=N     lo acumulado contrato por contrato desde Rithmic
flujoperfil=N   el volumen que trae la cadena en uso (CBOE ya trae el día
                entero, pero 15 min tarde)
```

Al principio salían con el mismo nombre y parecía que se contradecían —
`flujohoy=0` con `strikesconflujo=232`— cuando son cosas distintas de fuentes
distintas.

## Como saber DONDE se corta (agregado el 2026-09-04)

Dio cero **dos noches seguidas**, y sin operaciones no se puede distinguir "de
noche no se opera" de "esto esta roto". Se instrumento cada escalon del camino;
el renglon `AUDIT` ahora trae:

```
volenganchados=N   contratos con el enganche puesto
volavisos=N        avisos de PropertyChanged que llegaron
volconvol=N        los que traian LastTradeVolume > 0
volcontados=N      los que efectivamente se sumaron
```

Como leerlo, en una sola mirada:

- `volavisos=0` -> no llega nada: el problema es la **suscripcion**, no el mercado
- `volavisos>0` y `volconvol=0` -> **LastTradeVolume viene vacio**: campo equivocado
- `volconvol>0` y `volcontados=0` -> el **filtro de duplicado se come todo**
- `volcontados>0` y `flujoviva=0` -> la **clave de guardado no coincide** con la de lectura

Mirarlo en la rueda americana, no de noche.

**De esto dependen las dos hipotesis** sobre las dominantes que se mueven (ver
[[pelotitas-son-eventos]]): las dos se alimentan del volumen, asi que si esto no
cuenta, las dos quedan en negro y no se pueden probar.

## Estado del 2026-09-04: ROTO, Y <cuenta> (ya arreglado, ver arriba)

**El enganche por eventos NO funciona.** No es el horario -- esa fue mi excusa
comoda dos noches seguidas, y el operador la puso en duda con razon.

La prueba, sobre dos volcados de la cadena separados **57 segundos**:

```
contratos en las dos fotos : 169
cambio la punta COMPRADORA : 131 de 169  (78 %)
cambio la punta VENDEDORA  : 130 de 169  (77 %)
cambio el interes abierto  : 0     <- esperado, la OCC lo congela
cambio el volumen del dia  : 0
```

Y al mismo tiempo, en el renglon AUDIT:

```
volenganchados=140   los enganches ESTAN puestos
volavisostodos=0     cero avisos de CUALQUIER tipo
volcampos=ninguno    ni un nombre de campo visto
volconultimo=0       ningun contrato tiene LastTradeVolume > 0 leido directo
```

**Los objetos Security se actualizan constantemente y nunca levantan
PropertyChanged.** Implementar INotifyPropertyChanged no obliga a dispararlo.

## Lo que NO sirve como arreglo

**Sondear `LastTradeVolume`** tampoco: leido directo da 0 en los 140 contratos
(`volconultimo=0`). O sea que ese campo no lo llena este feed, ni por evento ni
por lectura. Hay puntas frescas y no hay dato de operaciones.

## Por donde seguir (esto fue lo que se hizo el 2026-09-06)

Buscar el evento REAL de operaciones del conector -- no en `Security` sino en
`IDataFeedConnector` o en lo que entregue `SubscribeToMarketData(..., Prints)`.
Volcar por reflexion los eventos disponibles de las dos clases, como se hizo con
`atas/_api` para descubrir la cadena.

**De esto dependen las hipotesis 1 y 2** sobre las dominantes que se mueven (ver
[[pelotitas-son-eventos]]). Mientras esto no ande, las dos quedan en negro. Las
hipotesis 3 y 4 no dependen del volumen y si dibujan.

## Trampa de medicion, para no repetirla

Al comparar dos volcados de la cadena, **la clave NO puede incluir los dias al
vencimiento**: desde que el tiempo lleva la hora, cambian a cada segundo y el
cruce da CERO pares. La primera comparacion "probo" que las puntas no se movian
sobre 0 contratos comparados. Clave estable: strike, call/put, y la parte ENTERA
de los dias.

Compila, no tira excepciones y la cadena está viva con 89 de 142 contratos
cotizando, pero el acumulador **dio cero** porque se probó en sesión nocturna,
donde las opciones casi no operan. **El mecanismo está; que cuente bien no está
probado.** Hay que mirar `flujoviva` crecer en una rueda americana antes de
darlo por bueno.

**Why:** es la única ventaja de datos que ninguna web gratuita ni paga puede
igualar, y sale de una licencia que el operador ya pagó.

**How to apply:** vive en `EngancharVolumen()` de `CadenaViva.cs`. Los niveles
se dibujan con el grupo de ajustes "Flujo". Ver [[cadena-es-en-vivo-rithmic]] y
[[costo-real-del-retraso]].
