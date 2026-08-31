---
name: cme-quikstrike
description: "CME Group: la fuente oficial de opciones de ES. Con el endpoint de settlements se despeja la IV y CME queda autónomo — no necesita ni Opensera ni conversión desde SPX."
metadata:
  node_type: memory
  type: reference
  originSessionId: 46e731fd-f0ba-466c-bd15-979f9343516d
  modified: 2026-08-21T00:00:00.000Z
---

Encontrado el 2026-08-21. **Es la bolsa misma**: fuente primaria de la que copian Opensera, InsiderFinance y el resto.

## El endpoint que lo cambia todo — Settlements

```
/CmeWS/mvc/Settlements/Options/TradeDateAndExpirations/133
   -> indice maestro: 7 grupos, 71 vencimientos, cada uno con productId y contractId

/CmeWS/mvc/Settlements/Options/Settlements/{pid}/OOF
   ?strategy=DEFAULT&optionProductId={pid}&monthYear={contractId}
   &optionExpiration={pid}-{L}{Y}&tradeDate=MM/DD/YYYY&pageSize=700
   -> settlements[]: strike, type, open, high, low, last, change, settle, volume, openInterest
```
`optionExpiration` es `productId` + guion + **letra de mes + ultimo digito del anio** (`ESU26` -> `138-U6`). Ese formato es el que faltaba en los intentos que devolvian vacio.

**Trae `settle` por strike.** Con el precio se **despeja la IV hacia atras** (Black-76 + biseccion) y CME deja de depender de nadie.

### Las dos T — el error que ya cometi una vez
El settle es del **dia anterior**. Hay que usar **dos tiempos distintos**:
- `T_precio` = desde el settlement (20:00 UTC del dia del dato) hasta el vencimiento -> **para despejar la IV**
- `T_valuacion` = desde ahora hasta el vencimiento -> **para calcular la gamma**

Usar una sola T infla la IV (dio 39 % en un 0DTE) y devuelve la gamma de ayer disfrazada de hoy. Con las dos separadas la IV ATM sale 10,2 % a 0 dias y 12,8 % a 25 -> **estructura temporal ascendente correcta**, que es la senal de que quedo bien.

### El futuro se despeja solo
Los calls muy dentro del dinero cumplen `F = K + settle`. Strike 100 -> 7562,50 y strike 200 -> 7462,50 dan **los dos F = 7662,50**. Chequeo de coherencia gratis, sin pedir el precio del futuro.

## Los 22 productos de opciones de ES (lista completa)
Antes solo tenia 10 y **me faltaba el 90 % del libro**.

- **138** ES trimestral · **136** EW (EOM)
- **Lunes:** 8292 W1 · 8293 W2 · 8294 W3 · 8295 W4
- **Martes:** 10132 W1 · 10133 W2 · 10134 W3 · 10135 W4
- **Miercoles:** 8227 W1 · 8228 W2 · 8229 W3 · 8230 W4
- **Jueves:** 10137 W1 · 10138 W2 · 10139 W3 · 10140 W4
- **Viernes:** 2915 W1 · 2916 W2 · 8019 W3 · 5222 W4
- Futuros ES = **133** · Opciones MES = 8928 (**irrelevante: 19.066 de OI total**)

## Volumen y cambio de interes abierto
```
/CmeWS/mvc/Volume/Details/O/{pid}/{YYYYMMDD}/P  -> monthData[].strikeData[]
      con strike, atClose (OI), change, totalVolume
/CmeWS/mvc/Volume/Details/F/133/{YYYYMMDD}/P    -> futuros por mes
```
**El campo `change` es lo unico que no tiene ninguna otra fuente.** Si el volumen de un strike fue mayormente `change` positivo, se **abrio** posicion nueva; si no, fue rotacion. Medido el 2026-08-20: put 7600 del lunes con volumen 5.353 y `change` +4.462 = **83 % posiciones nuevas**.

**Ojo — OI y gamma no son lo mismo.** Ese put 7600 tenia el mayor OI nuevo y aportaba **gamma casi cero**, porque a 91 puntos del precio y con vencimiento el mismo dia la gamma se apaga. El OI dice donde alguien espera problemas; la gamma dice donde el precio se pega hoy.

## ES contra SPX — la relacion real es 10 a 1
Con el libro completo (1.421.935 de OI): ES neto **-3.737 M** contra SPX **-36.900 M**. **No es 100 a 1 como dije primero** — ese numero salio de una extraccion incompleta. ES es ~9 % del total: no es marginal, y ademas viene **nativo en ES, sin error de base**.

## Retraso y por que NO importa
Publica a las **23:55 CT** del mismo dia de operacion (visto en `updateTime`). Los quotes publicos tienen 10 min de retraso.

**El interes abierto es de ayer en TODAS partes** — es una cifra de compensacion que la camara netea de noche. Opensera reprecia la gamma pero usa el mismo OI de ayer. Asi que el T+1 de CME no es desventaja: se toma su OI y se reprecia al precio en vivo. Lo unico que el T+1 pierde es el posicionamiento que se arma **hoy**, y eso solo lo tapa la columna de volumen de ATAS.

## QuikStrike (la interfaz visual)
`cmegroup.com` -> Tools -> QuikStrike -> **Open Interest Heatmap**. Corre en un iframe de `cmegroup-tools.quikstrike.net`; navegar directo falla. Su joya es la **matriz OI x Gamma** por strike y vencimiento — controles Greek (Delta · Gamma 1 Pt · Gamma 1 Pct · Vega · Theta) y Strikes en **10**, que centra bien. Con `Gamma (1 Pct)` los valores son enteros chicos: **no son dolares**, el orden relativo si sirve.

**Ya no hace falta.** La API de settlements da lo mismo y mejor, con la cuenta hecha por uno.

## Anti-bot
**Akamai Bot Manager** (`_abck`, `bm_sv`, `bm_sz`) bloquea la navegacion automatizada al **iframe de QuikStrike**. Los endpoints `/CmeWS/mvc/` **no estan bloqueados**: 40 llamadas espaciadas 700-900 ms pasaron limpias con `credentials:'include'`. Si algo rebota, pedirle que mueva el mouse y recargue.

**Why:** es la unica fuente **nativa de ES**, oficial, y ahora tambien autosuficiente para la IV. Cuando SPX y ES marcan el mismo strike, ese nivel pesa doble.

**How to apply:** cachear el crudo en `window.__raw` de una sola pasada y recalcular local cuantas veces haga falta, sin volver a pedir. Ver [[calcular-gex-propio]], [[conversion-spx-a-es]] y [[paginas-gex-auditadas]].
