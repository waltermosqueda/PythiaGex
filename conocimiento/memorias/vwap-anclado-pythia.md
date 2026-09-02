---
name: vwap-anclado-pythia
description: ATAS SI trae VWAP anclado nativo; el indicador propio PythiaVWAP agrega lo que falta y mide su propia exactitud.
metadata: 
  node_type: memory
  type: project
  originSessionId: a8062245-bc99-4fb7-b817-ef244a0b1012
  modified: 2026-09-02T01:19:52.926Z
---

Construido y verificado en pantalla el 2026-09-01 sobre MESU6 en gráfico de 5m.
Fuente en `PythiaGex/atas/PythiaVwap/`, DLL `PythiaVwap.dll`.

## Corrección a la premisa: ATAS ya tiene VWAP anclado

Él arrancó pidiendo el indicador porque "entiendo no está disponible en ATAS".
**Sí está.** Verificado por reflexión sobre `ATAS.Indicators.Technical.VWAP`:
tiene `AllowCustomStartPoint`, `StartBar`, `StartDate` y `StartKey` (ancla por
tecla), tres desviaciones (`StDev`, `StDev1`, `StDev2`), `VolumeMode`
Total/Bid/Ask y hasta modo TWAP. El enum `Type` va de M15 a Monthly más `All` y
`Custom`.

Para "VWAP desde este punto con tres bandas", **el nativo alcanza**.

## Lo que agrega el propio

Anclar solo al **máximo**, al **mínimo** o a la **vela de mayor volumen** de un
rango medido en sesiones; cuatro bandas con multiplicadores decimales; el canal
pintado; etiquetas con nombre y precio sobre el gráfico; las bandas del VWAP de
ayer; y el control de exactitud.

## El hallazgo medido

Prendiendo *Control de exactitud* dibuja el mismo tramo por tres caminos. En
MESU6 el 2026-09-01 a las 22:09 dio:

```
footprint (exacto)   7644.75
vwap de vela         7644.75   +0.0 tk
precio tipico        7644.50   -0.6 tk
```

Los dos métodos exactos coinciden — eso valida el motor. El **precio típico
`(H+L+C)/3`, que es lo que usan NinjaTrader y TradingView, estaba 0,6 ticks
corrido**. ATAS puede hacerlo exacto porque guarda el volumen por precio adentro
de cada vela (`IndicatorCandle.GetAllPriceLevels()`); las otras plataformas no
tienen ese dato.

## Las etiquetas: columna, no amontonadas

Él marcó que amontonadas contra el eje no se leen, y mostró el `VWAPBandsPro2`
de NinjaTrader como referencia. Lo que hace que se lea igual son **tres** cosas,
no una: todas las etiquetas alineadas en la **misma x** (nombre a la izquierda,
precio a la derecha), el nivel **prolongado por el hueco** que queda a la derecha
de la última vela hasta su etiqueta, y una separación vertical que **respeta el
orden de precio**. Cuando una etiqueta se corre para no pisar a otra, queda un
conector punteado: sin él, una etiqueta corrida miente sobre dónde está el nivel.

## Lo verificado en pantalla

Anclaje por sesión, anclaje **point-and-click** (mouse sobre la vela + tecla
`A`), bandas ±1/±2/±3 abriéndose en abanico desde el ancla, canal relleno,
marca vertical del ancla con su hora, y etiquetas medidas contra el eje (a 8,34
px por punto, caían donde debían). Cero excepciones en
`%APPDATA%\ATAS\pythiavwap-errores.txt`.

**Simetría verificada en MNQ 15m el 2026-09-01:** con VWAP 29120.75 dio +1s
29139.00 / -1s 29102.50 (±18.25 exacto), +2s / -2s a ±36.50 (= 2×18.25) y +3s /
-3s a ±54.75 (= 3×18.25). Las seis bandas simétricas y proporcionales sin
desvío. Los "saltos" de 0,25 que a veces se ven son el redondeo al tick, no un
error de cálculo.

**De la captura de NinjaTrader que él mandó** (MNQ 15m, ancla 17/8/2026 11:30):
los números cierran con VWAP **29446.51** y σ **223.01** — +1 = 29669.52 y
-1 = 29223.50 dan exacto. Yo había leído 29448.51 y no cerraba; era un dígito
mal leído de la imagen. **No se puede reproducir al centavo** porque la captura
es de un instante desconocido (por el título, cerca del 30/8) y el VWAP
acumulado depende de hasta dónde se mide.

**Pendiente de verificar en pantalla:** los modos de anclar al máximo/mínimo/
mayor volumen y las bandas de ayer — el combo de "Anclar en" no se abre con mis
clics, hay que cambiarlo a mano.

## Segundo indicador del mismo DLL: Delta VWAP/TWAP

Pedido el 2026-09-01 replicando el `LUZPREMIUM-VwapTwapDelta` de NinjaTrader
(título completo: `(MNQ 09-26 (2 Minute), 6/24/2026 9:35 AM, Day, Points, true,
true, 9, true)` — ancla, período, unidad, dos interruptores, suavizado 9).
Fuente en `VwapTwapDelta.cs`, sale en el mismo `PythiaVwap.dll`.

**Qué mide:** resta VWAP menos TWAP del mismo tramo y con el mismo precio. El
VWAP pesa por volumen; el TWAP pesa por tiempo, todas las velas igual. La resta
dice **dónde estuvo el volumen respecto del recorrido**: positivo = el dinero
entró en la parte alta; negativo = entró abajo. Sirve para ver una subida sin
respaldo de volumen, que el VWAP solo no muestra.

**Las dos decisiones que lo hacen honesto:** el mismo precio alimenta los dos
promedios (si fueran distintos la resta mezclaría dos efectos), y el promedio
móvil **nunca cruza el ancla** (si no, el primer tramo de cada sesión vendría
contaminado con el de ayer).

**Trampa encontrada:** un indicador de panel propio necesita
`Panel = IndicatorDataProvider.NewPanel` en el constructor. Sin eso va al panel
de las velas y, como la resta se mide en puntos y el precio en decenas de miles,
la escala se rompe y no se ve nada. La constante existe junto a `CandlesPanel`
(ambas son campos `const string`, no propiedades: el dumper de la API hay que
correrlo con el volcado de campos para verlas).

**Estado al 2026-09-02 00:03:** compila, carga y **calcula bien** (dio -3.38
puntos en MNQ 5m), pero la instancia agregada quedó con `Panel = "Chart"`
guardado en el workspace. El arreglo está en el código y el DLL copiado;
**falta reiniciar ATAS, borrar esa instancia y volver a agregarla** para que
tome el panel propio.

**Why:** es la primera herramienta del proyecto que además de dar un número
**mide cuánto vale ese número**, que es exactamente el protocolo de verificación
aplicado a un indicador en vez de a una cadena de opciones.

**How to apply:** recompilar con `dotnet build -c Release`, copiar el DLL,
reiniciar ATAS. Ver [[compilar-indicadores-atas]], [[clics-que-no-llegan-y-loops]]
y [[datos-ocultos-de-atas]].
