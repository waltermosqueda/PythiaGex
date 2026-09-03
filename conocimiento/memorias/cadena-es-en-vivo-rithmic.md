---
name: cadena-es-en-vivo-rithmic
description: "Rithmic entrega la cadena de opciones de ES completa y EN VIVO desde adentro de ATAS: 11 vencimientos con 0DTE, puntas reales, sin el retraso de 902 s de CBOE."
metadata: 
  node_type: memory
  type: project
  originSessionId: 15e03ce1-51d8-43ea-8c35-a2fa1a4b8145
  modified: 2026-09-03T17:39:45.577Z
---

Verificado el 2026-09-03. **El conector de Rithmic que ATAS ya tiene conectado
entrega la cadena de opciones de ES completa y en vivo.** Esto elimina la
dependencia de CBOE y su retraso de 902 s. Ver [[retraso-cboe-902s]].

## Lo medido

```
11 vencimientos, 6938 contratos
0DTE (vence hoy) ....... 726 contratos
manana ................. 730
diarios de 5, 6, 7 y 8 dias
```

Suscribiendo 60 contratos 0DTE al dinero: **60 con interes abierto, 60 con
punta compradora, 60 con punta vendedora**, a los quince segundos. Cotizaciones
reales y ajustadas — `E1DU6 P7750` en 5,90/6,10. `E1DU6` es la diaria de ES.

## Cómo se llega

`GetService<IOptionsDataFeed>` **no** funciona desde un indicador
(`NotSupportedException`, el servicio no esta registrado). Hay que **rastrear el
conector por reflexion** bajando por los campos privados de `DataProvider` —
aparece como `OFT.Rithmic.RithmicConnector`. De ahi:

1. `conn.Securities` → el catalogo local trae ESU6, MESU6, MNQU6
2. `GetOptionSeriesAsync(futuro)` → las 11 series con su `Type` y `Expiration`
3. `GetOptionsAsync(serie)` → los contratos con `StrikePrice` y `OptionType`
4. `SubscribeToMarketData(contratos, Prints|Best|Summary)` → **sin esto los
   precios y el OI vienen en CERO**. No estan vacios: el feed no manda datos de
   un instrumento al que nadie se suscribio.

La IV **no** viene servida: se despeja del punto medio de las puntas con
**Black-76**, que es el modelo correcto para opciones sobre futuros. Ver
`atas/PythiaGexNiveles/Black76.cs` y su arnes en `atas/_test/`.

## Las tres trampas que ya costaron una corrida cada una

**El precio de referencia tiene que ser el del subyacente DE LA CADENA.** Una
corrida salio desde un grafico de MNQ y busco los strikes de ES alrededor de
29511: cayo en 10800–12000, donde no cotiza nadie, y el resultado parecio ser
"no llegan precios" cuando se habia preguntado en el lugar equivocado.

**Hay que poner tope de contratos.** Con 40 strikes por lado en 6 vencimientos
(~960 contratos) ATAS empezo a avisar **"Market Data Latency: 7772 ms"**: le
come el ancho de banda a la cinta de FUTUROS, que es con la que se opera. El
indicador no puede degradar justo el dato que vino a mejorar. Tope duro y malla
**no uniforme**: todos los strikes cerca del dinero, uno cada cuatro mas lejos.

**Una ventana pareja de ±60 puntos no alcanza para el zero gamma**, que suele
estar ~70 puntos por debajo del precio: el tablero mostraba `--` porque la suma
nunca cruzaba cero dentro de lo observado.

## Validación independiente del despeje

El put y el call del **mismo strike** dan la misma volatilidad — 19,88 contra
19,57 en 7705; 14,89 contra 15,33 en 7725 — dentro del spread. Si el despeje
estuviera mal divergirian de forma sistematica. Y la sonrisa sale sola: 19,88 %
en 7705 bajando a 12,81 % en 7735, la asimetria clasica de indices.

## Lo que NO arregla

El **interes abierto sigue siendo de ayer**, y lo es para todo el mundo, GEXbot
incluido: la OCC lo consolida de noche. Eso no se arregla comprando nada. Y
Rithmic todavia no manda **volumen** de opciones (el ultimo negociado viene en
cero), asi que el bloque de volumen del tablero va con guiones.

**Why:** el operador estaba por descartar el proyecto entero por el retraso, y
la salida estaba adentro de la plataforma que ya tiene, sin pagar nada. Ver
[[costo-real-del-retraso]] y [[plan-gamma-gex]].

**How to apply:** vive en `atas/PythiaGexNiveles/CadenaViva.cs`. Se prende con
el ajuste "Usar la cadena EN VIVO de Rithmic"; si no esta disponible cae solo a
CBOE y **lo avisa en la cinta**. La sonda que descubrio todo esto es
`SondaOpciones.cs` y escribe su informe en `%APPDATA%\ATAS\pythiagex-sonda.txt`.
