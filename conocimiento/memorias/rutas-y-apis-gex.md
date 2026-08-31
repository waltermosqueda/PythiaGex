---
name: rutas-y-apis-gex
description: Rutas exactas y endpoints para sacarle el dato crudo a cada web de GEX sin depender de la interfaz.
metadata: 
  node_type: memory
  type: reference
  originSessionId: 46e731fd-f0ba-466c-bd15-979f9343516d
  modified: 2026-08-20T00:22:32.467Z
---

Cómo extraer la cadena de opciones de cada sitio, verificado el 2026-08-19. Todo esto se ejecuta con JavaScript dentro de la pestaña, **funciona en pestañas de fondo** y no necesita que nada esté al frente.

## InsiderFinance — la mejor materia prima (sin login)
Página: `https://www.insiderfinance.io/gamma-exposure/{TICKER}`

```js
JSON.parse(document.getElementById('__NEXT_DATA__').textContent).props.pageProps.initialData
// → { ticker, tickerDetails, spot, options[25702], timestamp, isStale }
// options[]: strike, expireYear, expireMonth, expireDay, cp('C'|'P'),
//            gamma, delta, openInterest, impliedVol, bid, ask
```
Para varios tickers **sin navegar** (mucho más rápido y evita cargar la página):
`/_next/data/{buildId}/gamma-exposure/{TICKER}.json` — el `buildId` sale de `__NEXT_DATA__.buildId`.

Ojo: SPX y NDX se congelan al cierre; los ETF siguen actualizando. `isStale` siempre dice `false`, no sirve.

## Opensera — niveles y base ES–SPX (sin login)
`https://opensera.com/api/gex/latest?t={timestamp}` (el parámetro `t` evita caché).

Trae `spotPrice`, `zeroGamma`, `callWall`, `putWall`, `activeCallWall`, `activePutWall`, `maxGammaStrike`, `gammaRegime`, **`basisSpread`** (la base ES–SPX, con signo invertido), `atmIV`, `expectedMove`, `zeroDTEGEX`, `totalNetGEX` (**100× inflado**), más `optionsChain[7094]` y `strikeGEX[508]`.

Cadencia gratis: grilla de :00 y :30. `/api/auth/me` devuelve el rol.

## QuantWheel — cadena cruda abierta (sin login, ignora el sandbox)
```
/api/options/chain?ticker=SPX&expiration=YYYY-MM-DD&optionType=call|put
/api/options/expirations?ticker=SPX
```
Devuelve `strike, bid, ask, iv, openInterest`. **No trae griegas** — la gamma la calculan ellos. Su IV está rota entre 13 % y 27 % en vencimientos vivos.

## Options Trading Toolbox — requiere login
Ruta canónica: `https://optionstradingtoolbox.com/gamma-exposure/{TICKER}/{DTE}` → `/gamma-exposure/SPX/0`
```js
const d = window.Livewire.all().find(x=>x.name==='gamma-exposure').snapshot.data;
const A = k => Array.isArray(d[k]) ? d[k][0] : d[k];   // los arrays vienen envueltos
// d: symbol, currentPrice, callWall, putWall, maxPain, asOf, selectedDte
// A('totals') → {gexCall, gexPut, gexTotal}
// A('strikePrices'), A('gexSigned'), A('gexCall'), A('gexPut'), A('callOI'), A('putOI')
```
Unidades: **millones de USD por 1 %** (el eje no lo dice). El parámetro `dte=1` sirve la cadena de hoy — no le creas. `dte=30` la deja trabada un rato pero **se recupera sola**.

## GammaLens — descartada
`https://gammalens-api.onrender.com/api/gex/SPY` y `/api/spot/SPY`. Por CORS hay que ejecutarlo desde el origen `gammalens.markets`.

**Why:** sin esto hay que redescubrir cada endpoint desde cero, y la interfaz de estos sitios oculta o deforma lo que el dato crudo dice.

**How to apply:** empezar siempre por el dato crudo, nunca por el gráfico. Ver [[paginas-gex-auditadas]], [[calcular-gex-propio]] y [[panel-navegador-no-frontear]].
