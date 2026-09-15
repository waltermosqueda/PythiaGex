# Investigacion: que API de datos de opciones contratar (hasta USD 100/mes)

Fecha de consulta de todos los precios: **15 de septiembre de 2026** (paginas oficiales de cada proveedor,
salvo donde se dice otra cosa). Los precios cambian: antes de pagar, volver a mirar la pagina del proveedor.

## La respuesta corta

- **Lo que te falta no es "mas datos": es la cadena de SPX/SPY/QQQ en tiempo real.** Los futuros (ES/NQ y sus
  opciones) ya los tenes en tiempo real por Rithmic dentro de ATAS. Lo unico que hoy llega 15 minutos tarde es
  el libro de los indices y los ETF de CBOE, que es gratis.
- **Las fuentes oficiales no venden a un particular a este precio.** OPRA (el consolidador de las 17 bolsas de
  opciones) solo vende a traves de vendedores; CBOE vende directo (DataShop / LiveVol) desde USD 500 por mes
  el fin de dia y USD 599 por mes la API, y CME ya la tenes pagada por Rithmic.
- **Con menos de USD 100 hay tres caminos serios**, de mas barato a mas completo:
  1. **Una cuenta de broker con API** (Tradier USD 0, IBKR USD 1,50/mes, TradeStation USD 3/mes, Schwab USD 0):
     cadena en tiempo real con volumen, interes abierto y griegas, para uso personal.
  2. **MarketData.app, plan Trader**: USD 30/mes pagando el año (USD 360) o USD 75 mes a mes. Vendedor oficial
     de OPRA, cadena completa con OI, volumen, IV y griegas. Solo REST (sin streaming).
  3. **ThetaData, plan Standard: USD 80/mes.** El mas poderoso bajo los 100: cada cotizacion NBBO, cada
     operacion, snapshots de la cadena, griegas de primer, segundo y tercer orden, OI diario y **8 años de
     historia tick a tick**, que es lo que el laboratorio necesita y hoy pagamos por gigabyte a Databento.
- **Mi recomendacion**: ThetaData Standard si se va a pagar; antes, probar el camino del broker (USD 0-3) para
  medir si el tiempo real cambia algo. Y la regla del proyecto sigue vigente: **no se paga hasta que la bitacora
  muestre operaciones concretas perdidas por el retraso.** Lo que medimos hasta ahora dice lo contrario
  (memoria costo-real-del-retraso: 0,29 pts en el zero gamma y cero en los muros).
- **Ojo con la web publica**: con datos de OPRA en tiempo real bajo contrato de no profesional, mostrar la
  cadena o sus derivados a terceros es "redistribucion" y cuesta USD 1.500 por mes. La pagina de GitHub tendria
  que quedar privada, o seguir con el dato retrasado de CBOE que ya usa.

## Que tenes y que te falta (para no comprar de mas)

Pensalo como una cocina: ya tenes la heladera llena de lo que es del futuro (ES, NQ, sus opciones por Rithmic,
con volumen y puntas en tiempo real). Lo que llega en delivery, y llega 15 minutos tarde, es el libro de los
indices (SPX, NDX) y de los ETF (SPY, QQQ, TQQQ), que baja gratis de CBOE cada minuto a la rama cadenas.

Lo que un proveedor pago te agregaria, en orden de importancia para lo que hacemos:

1. **La cadena de SPX/SPY/QQQ sin los 15 minutos de retraso**, con volumen por strike en vivo (la referencia
   dibuja gamma por volumen: sin volumen en vivo no hay barras que respiren).
2. **Interes abierto**: es de ayer siempre (la OCC lo publica de madrugada); ningun proveedor lo tiene mas fresco.
3. **Griegas**: las calculamos nosotros con Black-Scholes desde la IV; que el proveedor las traiga es comodidad,
   no necesidad. Lo que si importa es que traiga **IV o las puntas bid/ask** para calcularla.
4. **Historia intradia de la cadena** para el laboratorio: hoy solo tenemos lo que grabamos desde el 03-09 mas
   lo que compramos a Databento (~USD 4,3 por rueda). Un proveedor con años de historia por minuto cambia el
   laboratorio mas que el tiempo real.

## Lo oficial e institucional (y por que queda afuera)

- **OPRA** (opraplan.com): no vende a particulares; fija las tarifas que los vendedores pasan. No profesional:
  **USD 1,25 por usuario por mes**; profesional USD 31,50; uso "sin pantalla" (calculos automaticos en un servidor)
  USD 2.000 por mes por categoria; redistribucion USD 1.500 por mes. Fuente: guias de ThetaData (29-05-2026) y
  de MarketData.app.
- **CBOE DataShop / LiveVol**: All Access API Tier 1 **USD 599 por mes** (150.000 puntos), prueba gratis de 14
  dias; redistribucion +USD 1.499. Suscripciones intradia: cada 10 minutos USD 1.500 por mes, cada minuto USD 6.000
  por mes; fin de dia USD 500 por mes. Es la fuente mas seria y la mas cara. El JSON retrasado gratis que ya usamos
  es de la misma casa.
- **CME**: sus opciones sobre ES/NQ ya te llegan por Rithmic dentro de ATAS (PuenteRithmic las lee). Contratar
  R|API+ aparte cuesta ~USD 100 por mes de API mas USD 25 de usuario, y no te daria nada que no tengas.
- **Databento**: el plan con datos en vivo (Standard) cuesta **USD 199 por mes**; el uso por gigabyte en vivo para
  OPRA se discontinuo en junio de 2025. Sigue siendo la mejor opcion para historia puntual pagando por uso.
- **ORATS**: API retrasada USD 199, en vivo USD 299 por mes. Muy bueno en griegas y volatilidad; fuera de precio.
- **Intrinio**: opciones en tiempo real solo en plan Enterprise, precio a consultar. Fuera.
- **Unusual Whales API**: USD 150 por mes el basico. Vende analisis (flujo, GEX ya calculado), no la cadena
  cruda, y el protocolo del proyecto es recalcular desde lo crudo. Fuera.
- **FlashAlpha**: USD 79 el basico (GEX por strike de ETF e indices, frescura 15 s, 250 pedidos por dia) y USD
  299 con la cadena completa. Empresa nueva, sin trayectoria verificable; vende el resultado, no el dato. No.

## Los que entran en USD 100 (con lo bueno y lo malo de cada uno)

### 1. ThetaData (thetadata.net) — el mas completo bajo los 100

- **Value USD 40 por mes**: "acceso en tiempo real", 3 tipos de pedido, intervalos de 1 minuto, 4 años de historia.
  Es el plan chico: no trae snapshots de cadena ni streams; sirve para bajar datos, no para un tablero vivo.
- **Standard USD 80 por mes**: "cada cotizacion NBBO reportada por OPRA", snapshots de la cadena, 7 tipos de
  pedido, tick a tick, 8 años de historia, 15.000 streams de operaciones.
- **Pro USD 160 por mes**: todo, 12 años. Fuera de presupuesto.
- Lo que da: griegas de primer orden (delta, vega, theta, rho), segundo (gamma, vanna, charm) y tercer orden
  (speed, zomma, color) por Black-Scholes; IV; OI actualizado a diario; historia desde junio de 2012.
- La tarifa OPRA de no profesional la paga ThetaData por vos.
- Como funciona: corres un programa suyo en tu PC (el "Theta Terminal") y tu codigo le pide por HTTP local.
  Funciona bien con Python y con C#, o sea entra en ATAS y en la web.
- Reputacion: Trustpilot 4,4 sobre 5 con 14 reseñas (86 % de cinco estrellas); elogian el soporte y el valor;
  la unica queja seria (24-10-2025) es que "siempre hay otro nivel escondido" y que **no hacen reembolsos**.
  Empresa chica (fundada en 2020, 2 a 10 empleados): es el riesgo. Integrada en QuantConnect.
- Veredicto: si se paga uno solo, es este, en Standard. El Value no alcanza para un tablero en vivo.

### 2. MarketData.app — el mas barato con OPRA oficial

- **Starter USD 12 por mes pagando el año / USD 30 mes a mes**: cadenas con 15 minutos de retraso (no mejora lo
  que ya tenes gratis).
- **Trader USD 30 por mes pagando el año (USD 360) / USD 75 mes a mes**: cadenas en tiempo real para no
  profesionales, 100.000 creditos por dia.
- Lo que da por cadena: bid, ask, tamaños, ultimo, volumen, **interes abierto**, IV, griegas completas. Es
  vendedor oficial de OPRA (te hacen firmar el contrato de no profesional).
- Lo que no da: streaming. Es REST puro, hay que preguntar cada tantos segundos. Un usuario se queja de
  "demasiadas desconexiones, sin streaming" (10-04-2026).
- Reputacion: Trustpilot 4,5 sobre 5 con 109 reseñas; responden el 100 % de las negativas.
- Veredicto: la opcion economica seria si se quiere OPRA oficial sin abrir cuenta de broker. Para un tablero
  que refresca cada minuto alcanza de sobra; para ver "cada operacion" no.

### 3. Massive (ex Polygon.io) — serio, pero el tiempo real cuesta 199

- Options Starter USD 29, Options Developer USD 79 (retrasado 15 minutos, con griegas e IV, websockets, 4 años),
  Options Advanced **USD 199** (el unico en tiempo real).
- Muy buena reputacion y documentacion; es la recomendacion por defecto de la mayoria de las guias de 2026.
- Veredicto: bajo los 100 solo te vende lo mismo que CBOE gratis, pero mejor empaquetado y con historia. No.

### 4. Alpaca — Algo Trader Plus USD 99 por mes

- OPRA en tiempo real con websockets sin limite de simbolos, griegas e IV por Black-Scholes.
- **No trae interes abierto** (confirmado en su documentacion del endpoint de cadena). Habria que seguir
  bajando el OI de CBOE. Griegas solo cuando hay bid y ask distintos de cero.
- Veredicto: bueno para streaming, incompleto para nuestro perfil (sin OI). No.

### 5. El camino del broker: cadena en tiempo real por USD 0 a 3

Todos exigen abrir una cuenta (sin obligacion de operar) y firmar el contrato de no profesional de OPRA.

- **Tradier** (USD 0): cuenta sin minimo para cuentas de contado, API incluida en el plan gratis; cotizaciones de
  opciones en tiempo real para titulares; la cadena trae volumen, **interes abierto**, bid/ask y griegas e IV de
  ORATS, pero **las griegas se actualizan cada hora** (para un 0DTE eso es viejo: usar la IV solo como
  referencia y calcular gamma con las puntas). Streaming por websocket de quotes y trades. Acepta clientes de
  mas de 250 paises. Reputacion como broker: mediana; como API: muy usada (QuantConnect la integra).
- **Interactive Brokers** (USD 1,50 por mes): OPRA nivel 1 no profesional, se condona con USD 20 de comisiones.
  Griegas e IV calculadas por IBKR en la API (TWS), OI diario. La traba: **100 lineas de datos simultaneas** por
  cuenta; una cadena 0DTE de SPX tiene cientos de contratos, asi que hay que usar "snapshots" (100 gratis por
  mes, despues USD 0,01 a 0,03 cada uno) o comprar boosters de 100 lineas a USD 30 por mes. Y hay que tener
  TWS o Gateway abierto. Es el broker mas institucional de la lista.
- **TradeStation** (USD 3 por mes de OPRA no profesional; gratis si la cuenta califica): API con cadenas y
  griegas, streaming. Acepta clientes internacionales.
- **Schwab Trader API** (USD 0): cadenas con griegas e IV, streaming de opciones, 120 pedidos por minuto,
  solo cuentas reales; hay que registrar una app y esperar la aprobacion; los tokens vencen cada 7 dias. Es
  el sucesor de la API de TD Ameritrade. Para residentes fuera de EE. UU. la elegibilidad no esta clara.
- Veredicto: es la forma mas barata de **probar** si el tiempo real cambia algo, antes de pagar. Tradier es el
  mas simple de abrir; IBKR el mas serio pero el mas trabado por las 100 lineas.

## La letra chica que puede arruinar la web

- "No profesional" de OPRA = uso personal. Si un dia operas para terceros o una empresa, sos profesional
  (USD 31,50 por mes por vendedor, y varios te lo van a preguntar).
- **Mostrar datos de OPRA en tiempo real, o cosas derivadas de ellos, en una pagina que otros pueden ver, es
  redistribucion** (USD 1.500 por mes) y ningun plan de autoservicio la incluye. La web actual
  (waltermosqueda.github.io/PythiaGex) es publica. Con datos pagos tendria que quedar privada (con clave) o
  seguir alimentandose solo del dato retrasado de CBOE, como hoy.
- Un servidor que calcula con datos en tiempo real sin mostrarlos puede caer en "uso sin pantalla"
  (USD 2.000 por mes por categoria). Para uso personal en tu PC no aplica; para una nube que calcula sola,
  preguntar al vendedor por escrito antes.

## Que haria yo, en orden

1. **No pagar nada todavia** (decision del 15 de sesiones). Lo que se midio dice que el retraso cuesta 0,29
   puntos en el zero gamma y nada en los muros.
2. **Abrir Tradier (USD 0) o IBKR (USD 1,50) como experimento**: bajar la cadena de SPX/SPY/QQQ en tiempo real
   una semana, grabarla al lado de la de CBOE, y que el laboratorio mida si los niveles en vivo le ganan a los
   retrasados contra placebo. Es la unica manera de saber si vale la pena.
3. Si el laboratorio dice que si, **ThetaData Standard (USD 80)**: es el unico bajo los 100 que ademas trae la
   historia que el laboratorio necesita. Pagar mes a mes, nunca anual, y acordarse de que no reembolsan.
4. La web: mantenerla con el dato retrasado de CBOE (publico y legal) y, si algun dia va con datos pagos,
   ponerle clave.

## Fuentes consultadas (15-09-2026)

- ThetaData: [precios](https://www.thetadata.net/pricing), [opciones](https://www.thetadata.net/options-data),
  [guia de tarifas OPRA](https://www.thetadata.net/articles/2026-05-29-opra-fee-guide-for-options-market-data),
  [Trustpilot](https://www.trustpilot.com/review/thetadata.net).
- MarketData.app: [precios](https://www.marketdata.app/pricing/), [tarifas OPRA explicadas](https://www.marketdata.app/education/options/opra-fees/),
  [vendedor oficial OPRA](https://www.marketdata.app/news/opra/), [Trustpilot](https://www.trustpilot.com/review/www.marketdata.app).
- Massive (ex Polygon): [precios de opciones](https://massive.com/pricing?product=options), [API de opciones](https://massive.com/options).
- Databento: [precios](https://databento.com/pricing), [nuevos planes OPRA](https://databento.com/blog/introducing-new-opra-pricing-plans),
  [dataset OPRA](https://databento.com/datasets/OPRA.PILLAR).
- CBOE: [All Access API precios](https://datashopcert.livevol.com/all-access-apis-pricing), [DataShop](https://datashop.cboe.com/).
- ORATS: [Data API](https://orats.com/data-api).
- Alpaca: [datos](https://alpaca.markets/data), [cadena de opciones (sin OI)](https://docs.alpaca.markets/reference/optionchain).
- Tradier: [datos de mercado](https://docs.tradier.com/docs/market-data), [cadenas](https://docs.tradier.com/reference/brokerage-api-markets-get-options-chains),
  [streaming](https://docs.tradier.com/reference/websocket-market-data-streaming), [precios](https://tradier.com/pricing).
- Interactive Brokers: [precios de datos](https://www.interactivebrokers.com/en/pricing/market-data-pricing.php),
  [guia 2026 de suscripciones](https://dev.to/xqliu/interactive-brokers-market-data-subscription-which-one-do-i-actually-need-in-2026-164g).
- TradeStation: [precios](https://www.tradestation.com/pricing/), [API](https://developer.tradestation.com/trading-api/).
- Schwab: [Schwab Trader API (resumen)](https://grokipedia.com/page/Schwab_Trader_API), [limites y requisitos](https://mylinedchart.com/resources/articles/schwab-api-for-technical-traders-workflow-fit-checklist).
- Unusual Whales: [API](https://unusualwhales.com/public-api). FlashAlpha: [precios](https://flashalpha.com/pricing).
- Intrinio: [precios](https://intrinio.com/pricing). OPRA: [FAQ](https://www.opraplan.com/faqs).
- Rithmic: [tarifas de API por broker](https://faq.ampfutures.com/hc/en-us/articles/360060137993-Rithmic-Pricing).
- Foros: [Elite Trader sobre APIs de griegas](https://www.elitetrader.com/et/threads/looking-for-data-api-feed-for-options-greeks-to-replace-ib-api.254353/),
  [Elite Trader: Databento OPRA](https://www.elitetrader.com/et/threads/databento-launches-real-time-and-historical-apis-for-us-equity-options-at-1-month.376021/).
  Reddit no se pudo leer directo (bloquea el acceso automatizado): lo de reputacion sale de Trustpilot y Elite Trader.
