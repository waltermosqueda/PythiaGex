---
name: volumen-opciones-en-vivo
description: "El volumen de opciones por strike se acumula a mano desde Rithmic, por evento. Es el único dato del mapa que no es de ayer — ni GEXbot lo tiene."
metadata: 
  node_type: memory
  type: project
  originSessionId: 15e03ce1-51d8-43ea-8c35-a2fa1a4b8145
  modified: 2026-09-04T00:48:44.195Z
---

Implementado el 2026-09-03. **Es el punto ciego de todos los tableros de GEX.**

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

## Estado: SIN VERIFICAR

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
