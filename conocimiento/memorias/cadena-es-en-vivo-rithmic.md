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

**2026-09-08, el 0DTE se habia perdido y volvio:** desde el 07-09 a las 23:10
la cadena viva caia al micro (MESU6, solo el trimestral a 10 dias, sin 0DTE)
porque el grande no estaba en el catalogo local y SearchSecuritiesAsync con
Type+Exchange tira NullReference SIEMPRE (6 de 6 intentos, no es una carrera
de arranque); Code="ES" devuelve la raiz sin series ("no data"). Lo que
funciona: buscar por CODIGO DE CONTRATO derivado del micro local (MESU6 ->
ESU6; ESZ6 de siguiente). Resultado: ESU6 6 vencimientos 4318 contratos y,
por primera vez, NQU6 6 vencimientos 3720 contratos (antes MNQ solo tenia el
trimestral). Si el libro de Rithmic vuelve a quedar en "10 dias", mirar
"[cadena viva]" en el log: tiene que decir "(ESU6)".

**ROTO desde ATAS 8.0.14.399 (instalado 09-09 11:08; medido 10-09 noche con el diagnostico de 1.8d):**
en .399 `OFT.Rithmic.RithmicConnector` implementa SOLO IDataFeedConnector; el unico tipo que implementa
`ATAS.DataFeedsCore.IOptionsDataFeed` es `OFT.InteractiveBrokers2.IBConnector`. Por eso la cadena viva
no encuentra "el conector de opciones" desde el 09-09 11:57 y vivaActiva=False. La busqueda por
reflexion no lo va a encontrar nunca: hay que ver si RithmicConnector conserva metodos de opciones
(GetOptionSeriesAsync, etc.) sin la interfaz (invocarlos por reflexion) o si el tablero de opciones de
ATAS usa otra via. OJO: la busqueda honda + diagnostico de 1.8d se comian 2,6 nucleos y 9 GB; 1.8e la
acota (8 s, 150k nodos, cada 5 min).

**1.8f (10-09 21:51):** buscando POR FORMA (IDataFeedConnector con GetOptionSeriesAsync/GetOptionsAsync)
el conector de Rithmic SI se encuentra y responde, pero `GetOptionSeriesAsync(ESU6)`, `(ESZ6)` y
`(MESU6)` devuelven **0 vencimientos, 0 contratos** en .399 (antes del 09-09: ESU6 6 vencimientos, 4318
contratos). El metodo existe pero no trae series. Proximo paso (no hecho): sondar que pide el Options
Board de ATAS en .399 (raiz "ES", otro Security, otra llamada) con la Sonda, con el tablero abierto.
Mientras, el libro de ES sigue por CBOE (SPX) con 15 min de retraso.

**RESUELTO 2026-09-11 01:06 (Gamma Hoy 1.8h, PuenteRithmic.cs):** la .399 no "perdio" las opciones: ATAS las
APAGO a proposito en el conector de Rithmic. Prueba triple: (1) su propio log `Logs/app_*.log` dice textual
"Options are not available in the current version, the option series request for E-Mini S&P 500 is ignored"
en cada llamada desde la .399, y el 09-09 00:00 (antes de instalarla, 11:08) decia "Received 8 option series /
Received 726 options"; (2) el Options Board de ATAS abre con "Account Required" y la lista de cuentas vacia;
(3) descompilando OFT.Rithmic.dll (ilspycmd) `GetOptionSeriesAsync`/`GetOptionsAsync` son un LogWarn +
`Enumerable.Empty`. NO fue nada nuestro: el mismo DLL funcionaba a las 10:59 y fallo a las 12:00 del 09-09 sin
commits de CadenaViva en el medio. La maquinaria privada sigue entera (comando que llama a
`REngine.getInstrumentByUnderlying(subyacente, bolsa, vencimiento, contexto)`, dos structs de contexto con
TaskCompletionSource, manejador de respuesta con ProcessSecurity). PuenteRithmic.cs la reconoce POR FORMA
(firma de constructores + IL que llama a getInstrumentByUnderlying) y rehace el pedido. Medido tras reiniciar:
"6 series para ESU6@CME", "6 vencimientos, 4486 contratos (ESU6, por PUENTE)", y ATAS mismo anota "Received 6
option series ... Received 752 options for 11 Sep 26 ESU6". Subyacente que acepta Rithmic: el codigo del
contrato ("ESU6"), bolsa "CME", vencimiento "yyyyMMdd". Si una futura version renombra o quita esas piezas, el
puente lo dice en el log ("FALTAN PIEZAS") y cae a CBOE.
