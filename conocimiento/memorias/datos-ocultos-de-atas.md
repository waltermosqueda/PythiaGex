---
name: datos-ocultos-de-atas
description: "Todo lo que un indicador puede sacar de ATAS y no estábamos usando: footprint por precio, delta, VWAP y value area en cada vela."
metadata: 
  node_type: memory
  type: project
  modified: 2026-08-31T07:00:22.784Z
  originSessionId: a021c566-b4fe-42c5-acd1-0a01a165a079
---

Auditado por reflexión sobre `ATAS.Indicators.dll` el 2026-08-31, con la herramienta que está en `PythiaGex/atas/_api`. Esto ya estaba pago con la licencia Ultra y no lo usábamos.

## Cada vela trae el footprint completo

`IndicatorCandle.GetAllPriceLevels()` devuelve, **por cada precio de esa vela**: `Volume`, `Bid`, `Ask`, `Ticks`, `Time`. Con eso se arma cualquier perfil sin depender de ningún indicador de la plataforma.

Y además cada vela ya trae calculado: `Delta`, `MaxDelta`, `MinDelta`, `VWAP`, `ValueArea` (VAH y VAL), `OI`, `MaxOI`, `MinOI`, `MaxVolumePriceInfo` (el POC de la vela), `MaxBidPriceInfo` y `MaxAskPriceInfo` (dónde hubo absorción), `MaxPositiveDeltaPriceInfo` y `MaxNegativeDeltaPriceInfo` (dónde estuvo la agresión).

## Lo que se puede construir con eso

Ya está hecho en `PythiaGex/atas/PythiaGexNiveles/Contexto.cs`:

- perfil de volumen compuesto: POC, VAH, VAL, nodos de alto y de bajo volumen
- VWAP acumulado con bandas de desvío a 1 y 2 sigma
- delta acumulado de la sesión con su máximo y su mínimo
- Initial Balance, apertura, máximo, mínimo
- **volumen y delta operados dentro de una banda de precio** — o sea, en cada nivel de gamma

## El hallazgo que cambia la lectura

La cadena de opciones dice **dónde** está la pared. El footprint dice **quién está ganando ahí**.

Medido el 2026-08-31 sobre el Gamma Pin en ESU6 7691,13: **24,4k contratos operados = 17 % del volumen de la sesión**, con delta de apenas **+1,0k**. Muchísimo volumen y casi ningún avance neto: alguien se está comiendo todo lo que le tiran. Eso es **absorción**, y se marca solo.

El mismo nivel con delta comprador fuerte diría lo contrario: se está rompiendo.

## Confluencia

Cada nivel se puntúa por cuántas referencias **independientes** lo confirman: POC, área de valor, nodo alto, VWAP, banda de VWAP, referencia de sesión, coincidencia entre cadena completa y 0DTE, y absorción. El Gamma Pin de ese día dio **x4**.

Es un mapa de coincidencias, **no un pronóstico**. Sirve para ordenar prioridades, no para prometer nada.

## Otras rutas abiertas

- `IIndicatorDataProvider.GetService<T>()` es un localizador de servicios. El indicador prueba en vivo si devuelve `IOptionsDataFeed` (`GetOptionsAsync`, `GetOptionSeriesAsync`). Se ve prendiendo "Incluir diagnóstico" en el tablero. Si funcionara, el GEX se calcularía sobre opciones de ES en tiempo real, sin CBOE y sin base.
- `GetTradesCache(TimeSpan)`, `GetMarketByOrdersCache(TimeSpan)` y `GetMarketDepthSnapshot()` dan cinta y libro para detectar órdenes grandes.
- `GetFixedProfile(FixedProfileRequest)` da perfiles de períodos anteriores — sirve para POC vírgenes de sesiones pasadas.

**Why:** todo esto es tiempo real de Rithmic y ya está pago. Ninguna web de GEX puede dar el order flow parado en el nivel.

**How to apply:** el contexto se recalcula en barra nueva, nunca en cada tick — recorrer el footprint de cientos de barras en el hilo de dibujo cuelga el gráfico. Ver [[compilar-indicadores-atas]] y [[atas-opciones-es]].
