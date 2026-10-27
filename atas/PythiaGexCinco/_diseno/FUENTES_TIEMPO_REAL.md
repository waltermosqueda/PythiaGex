# Fuentes de la cadena de SPX/SPXW y SPY en tiempo real (o con menos de 15 min)

Investigación del **27-10-2026** para PythiaGex 5.0. Todas las páginas se consultaron ese día, salvo donde dice otra cosa.
No se contrató nada, no se abrió ninguna cuenta y no se tocó ATAS.

Marcas que se usan en todo el documento:
- **[oficial]**: página del proveedor.
- **[secundaria]**: agregador o tercero. Hay que confirmarla con el proveedor antes de decidir.
- **[medido]**: medido hoy con los archivos de la 5.0 (`%APPDATA%\ATAS\PythiaGex5\cboe`).
- **[memoria]**: viene de una investigación anterior (15-09) y **no** se re-verificó hoy.

Las definiciones de ATAS y de los proveedores están **parafraseadas** en castellano, con su URL. No se copian textos.

Regla vigente del proyecto: **no pagar suscripciones** hasta las 15 sesiones de bitácora. Por eso cada vía dice si es gratis, gratis con cuenta o paga.

---

## 0. La idea en dos minutos

Pensalo como el diario de la mañana. CBOE te lo deja gratis en la puerta, pero con 15 minutos de demora. Esa demora no es
desidia de CBOE: es la regla del dueño del diario, OPRA (el consolidador de las bolsas de opciones de EE. UU.), que **solo cobra
por la edición en vivo**. Por debajo de 15 minutos corre su tarifa; por encima es gratis
([guía de tarifas OPRA de ThetaData, 29-05-2026](https://thetadata.net/articles/2026-05-29-opra-fee-guide-for-options-market-data)).

Para leerlo en vivo hay solo dos caminos legales:

1. **Ser cliente de un broker que paga esa tarifa por vos** (tastytrade, Tradier, Schwab, IBKR). Es "gratis con cuenta": no hay
   suscripción, pero hay que abrir y fondear una cuenta de bolsa en EE. UU. Esa es una decisión tuya, no técnica.
2. **Pagarle a un vendedor de datos** (ThetaData, MarketData.app, Massive, Databento). Desde USD 40 por mes.

**No existe una fuente pública, gratis y legítima con menos de 15 minutos.** Las que parecen serlo son derivadas, tienen muro de pago
o no son el dato de OPRA (ver §5).

Lo que **ya tenés pagado**:
- El **X-Ray** de ATAS trae SPX minuto a minuto, pero solo para mirar en pantalla: no tiene API ni exportación documentada, y su dato es
  licencia de un tercero (OptionsDepth). Sirve de **testigo** en vivo para la bitácora; **no** sirve de **insumo** para la 5.0 (§7).
- Las **opciones de ES por Rithmic** llegan en vivo y la 5.0 ya las usa (libro ES, `CadenaApi.cs`).

---

## 1. Lo medido hoy, para dimensionar

**Atraso real de CBOE hoy [medido]**
- SPX: la foto bajada a las 17:00:49Z traía `ultimo_trade` 12:45:30 NY (16:45:30Z): **919 s** de atraso al llegar a la PC.
  - El sello propio de CBOE (17:00:31Z) ya venía 901 s detrás del dato; el resto es la espera hasta la vuelta de 75 s.
- SPY: foto de las 17:00:15Z, dato 12:44:16 NY (16:44:16Z): **959 s** (sello de CBOE 16:59:17Z, también 901 s detrás del dato).
- Coincide con los 902 s de la memoria `retraso-cboe-902s`.
- Archivo: `cadena-ES-2026-10-27.jsonl.gz`. Es la cadena `_SPX` de CBOE; se llama "ES" por herencia de cboe_local, igual que NDX se guardaba como "NQ".

**Cuántos contratos hay que seguir [medido, foto de las 17:00Z; SPX 7.719,09, SPY 769,35]**
- SPX 0DTE: ±1 % son 31 strikes (62 contratos); ±2 % son 62 (124); ±5 % son 142 (284). El paso es de 5 puntos cerca del dinero.
- SPY 0DTE: ±1 % son 16 strikes (32 contratos); ±2 % son 31 (62); ±5 % son 77 (154). El paso es de 1 dólar.
- Todo lo que guarda hoy la 5.0 (10 vencimientos hasta 14 días, ±5 %, filas hasta 8 días):
  - SPX: 836 filas, o sea 1.672 contratos;
  - SPY: 462 filas, o sea 924 contratos;
  - **total: 2.596 contratos**.
- **El 0DTE de ±2 % de los dos libros suma 186 contratos.** Es el mínimo razonable para una cadena viva. Contra ese número se miden los topes de cada vía.

**Lo que cuesta el retraso [memoria `costo-real-del-retraso`, no re-medido]:** 0,29 pts en el zero gamma y cero en los muros.
Es la razón por la que todavía no se paga nada.

---

## 2. ATAS y Rithmic: lo que ya está en la PC

### Tabla A

| Vía | Qué da | Atraso real | Costo | Requisitos | Términos | Esfuerzo en la 5.0 |
|---|---|---|---|---|---|---|
| Rithmic (Lucid) por `IOptionsDataProvider` | Opciones de CME (ES, NQ…) con puntas, OI y volumen del día | Vivo | Ya pagado | Nada nuevo | Uso dentro de ATAS | **Ya hecho** (libro ES) |
| Rithmic para SPX/SPY (OPRA) | **No existe** | — | — | — | — | — |
| ATAS Options Chain ("Options Suite") | GEX, OI y volumen por strike, Key Levels, Expected Move, sobre lo que traiga el conector | El del conector | Ya pagado | Conector con opciones | Indicador de pantalla | No es fuente: dibuja, no exporta |
| ATAS Options X-Ray ("Advanced Options Suite") | Posicionamiento de SPX por grupo de participantes, en gráficos de ES, MES, SPY y US 500 | Cadencia 1 min; atraso **no publicado** | Ya pagado (vitalicio) | Paquete aparte habilitado | Licencia de OptionsDepth; sin API ni exportación documentada | **No es fuente** (§7) |
| IB como 2.º conector de ATAS | Opciones de acciones y ETF (SPY); SPX índice **sin confirmar** | Vivo (nivel 1 de OPRA) | USD 1,50/mes de OPRA no profesional [secundaria] + USD 500 de patrimonio mínimo [oficial] | Cuenta IBKR PRO fondeada; TWS o IB Gateway abiertos | Uso personal | Poco código, mucha operación; **100 líneas no alcanzan** |

### Rithmic no trae OPRA

- **Medido el 08-10** (`DISENO_4_1.md` §3.1): el catálogo de Rithmic de este ATAS (`Security.cdb`) tiene 67.059 instrumentos de CME y
  cero acciones o índices de EE. UU.
- **Ninguna fuente pública** menciona OPRA por Rithmic, ni para cuentas de fondeo. La tabla de tarifas de AMP muestra Rithmic con
  CME y Eurex, y sin ICE ni CFE ([AMP, tarifas por plataforma](https://ampfutures.com/trading-info/exchange-data-fees), tabla de
  01-01-2024).
- **ATAS dice lo mismo con otras palabras:**
  - su Options Chain es para ES, NQ, CL, GC y demás futuros de CME, si el conector trae opciones
    ([Options Chain vs Options X-Ray](https://help.atas.net/en/support/solutions/articles/72000662644-options-chain-vs-options-x-ray-which-to-use), mod. 12-10);
  - el análisis de opciones "sobre futuros y acciones" se anunció **vía Interactive Brokers**
    ([changelog de ATAS, 04-08-2026](https://feedback.atas.net/changelog/new-mbo-bundle-and-options-analysis-are-now-in-atas)).
- **Conclusión:** por Rithmic no hay SPX ni SPY, ni hoy ni en ningún plan documentado.

### Exportación o API de ATAS

- Ningún artículo oficial de Options Chain ni de X-Ray menciona exportación ni API.
- La API pública que sí existe es `IOptionsDataProvider` y `OptionsSubscriptionService` [memoria `atas-8-0-15-api-opciones`].
  - Le deja a **nuestro** indicador suscribirse a los mismos contratos que ofrece el conector. Es lo que hace la 5.0 con ES.
  - El X-Ray **no** pasa por ahí: usa un servidor propio y una caché cifrada que no se toca.

### Interactive Brokers como segundo conector

**Lo que hay**
- `OFT.InteractiveBrokers2.dll` implementa `IOptionsDataFeed` [medido 08-10, `DISENO_4_1.md` §3.1].
- La ayuda de ATAS pide Rithmic o IB para el Option Board ([Option Board](https://help.atas.net/en/support/solutions/articles/72000661117-how-to-use-option-board-in-atas)).
- En teoría, la 5.0 pediría la cadena de SPY por IB con el **mismo** `CadenaApi.cs`, cambiando el conector.

**Lo que no se sabe**
- Si el conector de IB de ATAS expone el **índice** SPX y sus opciones SPXW. Los documentos solo hablan de futuros y acciones.
- Hay que probarlo antes de contar con eso.

**Requisitos de IBKR**
- Patrimonio mínimo de **USD 500** para tener cualquier dato de mercado, y la suscripción activa recién al día hábil siguiente al
  fondeo ([IBKR, mínimos para datos](https://www.interactivebrokers.com/campus/ibkr-api-page/market-data-subscriptions/) [oficial, leído vía buscador; la página da 403 al bot]).
- Para la API hace falta la cuenta **IBKR PRO** (misma fuente).
- OPRA nivel 1 no profesional: **USD 1,50 por mes** [secundaria: tabla de precios de IBKR vista por el buscador].
  - La comisión que lo condona no quedó clara: una tabla dice USD 20, otra USD 30 o 60.
- Residentes argentinos: aceptados según comparadores [secundaria] ([BrokerChooser](https://brokerchooser.com/argentina),
  [CompareBroker](https://comparebroker.io/brokers/is-interactive-brokers-legal-in-argentina)).

**El tope que decide [medido + secundaria]**
- IBKR da **100 líneas** de datos simultáneas por cuenta.
- Hoy, el 0DTE de ±1 % de SPX y SPY son 94 contratos: entra justo. El de ±2 % son 186: **no entra**.
- Cada "Quote Booster" suma 100 líneas por **USD 30 por mes**, hasta 10 [secundaria].
- El tope propio de ATAS (512 por instancia, 3.000 en total [memoria]) no es el que muerde: muerde el de IB.

**Convivencia con Rithmic**
- ATAS ya corre varias conexiones a la vez: 3 Rithmic, dxFeed Delayed y Sim, según `Connectors.cnf` (`DISENO_4_1.md` §3.1).
- **Riesgo:** ATAS elige un "Quote Provider" por conexión
  ([ayuda IB de ATAS](https://help.atas.net/en/support/solutions/articles/72000602587), mod. 26-08).
  - IB nunca debe quedar como proveedor de ES o MES: IB no trae el lado de cada operación (ATAS lo calcula) y rompería el order flow.
  - La misma ayuda dice que IB da tick a tick de hasta 3 instrumentos a la vez.
- Lucid no interviene si IB se usa solo para datos.
- TWS o IB Gateway es un **programa externo** abierto todo el día. Choca en parte con "todo dentro del indicador".

**Veredicto:** es la vía de menos código, pero choca con "no pagar" (USD 1,50/mes más USD 500 inmovilizados) y con el tope de
100 líneas. Solo valdría para SPY ±1 % o como experimento corto.

---

## 3. Brokers con API: "gratis con cuenta"

Todos piden abrir una cuenta real, firmar el contrato **no profesional** de OPRA y, en general, fondearla.
Ninguno da tiempo real en su entorno de prueba (sandbox).

### Tabla B

| Vía | Qué da | Atraso real | Costo | Requisitos | Términos | Esfuerzo en la 5.0 |
|---|---|---|---|---|---|---|
| **tastytrade (DXLink)** | Streaming por WebSocket: `Trade` con `dayVolume`, `Summary` con `openInterest`, `Greeks` con `volatility` y `gamma`, `Quote` | Vivo en producción | USD 0 para cuentas propias fondeadas [secundaria] | Cliente "completamente dado de alta" [oficial]; Argentina en la lista [secundaria] | Programa Open API, uso personal | Medio-alto (WebSocket + OAuth), todo dentro del indicador |
| **Tradier (REST)** | Cadena por vencimiento con volumen, OI, puntas; griegas de ORATS **cada hora** | Vivo para titulares [oficial] | USD 0 de datos para titulares [oficial]; costo de cuenta sin confirmar | Cuenta de Tradier Brokerage; Argentina en la lista [secundaria] | Uso personal; sin redistribuir | **Bajo** (HTTP como `BajadorCboe`) |
| Schwab Trader API | Cadenas, streaming `LEVELONE_OPTIONS` | Vivo | USD 0 para clientes [memoria] | Cuenta Schwab + app aprobada; **Argentina sin confirmar** | "Individual", uso personal | Medio, con re-login manual cada 7 días |
| IBKR (TWS API directa) | Igual que por ATAS | Vivo | Igual que §2 | Igual que §2 | Igual | Igual que §2, sin ATAS de por medio |
| TradeStation | Cadena en streaming (`/v3/marketdata/stream/options/chains`) | Vivo | "Datos gratis si calificás como no profesional" [secundaria]; USD 3/mes [memoria] | Cuenta; Argentina en la lista [secundaria] | Uso personal | Medio |
| Webull OpenAPI | OPRA en vivo en algunas regiones (Singapur, Hong Kong) | — | — | **Sin evidencia de Argentina** | — | Descartado |
| Alpaca, feed gratis "indicativo" | Cotizaciones **derivadas** (no son OPRA) y trades con 15 min | ≥ 15 min | USD 0 | Cuenta | — | **No sirve** |

### tastytrade, el más completo de los gratis

**Lo oficial** ([conceptos de streaming](https://developer.tastytrade.com/docs/concepts/streaming/),
[guía de streaming](https://developer.tastytrade.com/docs/guides/stream-market-data/))
- Hay que ser cliente completamente dado de alta. Un registro con solo usuario y clave se rechaza.
- El token de cotizaciones vence a las 24 h y el de acceso OAuth a los 15 min.
- DXLink corta la conexión si no recibe un "keepalive" en 60 s. Hay que mandarlo cada 30 s.
- Topes: 5 sesiones, **25.000 suscripciones por sesión** y 10.000 cambios por minuto. Con eso entran de sobra los 2.596 contratos de la 5.0.
- Campos de cada evento:
  - `Trade`: precio, `dayVolume` y tamaño;
  - `Summary`: `openInterest`;
  - `Greeks`: `volatility`, `delta`, `gamma`, `theta`, `vega` y `rho`.
- **Ojo:** el volumen del día por contrato sale de `Trade.dayVolume`.

**Lo secundario**
- "Gratis para cuentas personales fondeadas" ([QuantConnect](https://www.quantconnect.com/docs/v2/cloud-platform/datasets/tastytrade)).
- Producción en vivo y sandbox demorado ([SDK comunitario](https://tastyworks-api.readthedocs.io/en/latest/market-data.html)).
- Argentina figura en la lista de países de tastytrade de [Option Alpha](https://docs.optionalpha.com/technical-documentation/platform/supported-countries) (sin fecha).
  - La página oficial de cuentas internacionales (actualizada el 19-03-2026) remite a una lista que no se pudo leer
    ([tastytrade international](https://tastytrade.com/learn/accounts/account-types/international-account/)).
  - Solo admite cuentas individuales y conjuntas, no sociedades.

**Sin verificar**
- El símbolo exacto de las SPXW en el streamer. El SDK recomienda tomar `streamer-symbol` de la cadena en vez de armarlo a mano.
- El monto mínimo de fondeo para clientes internacionales.

### Tradier, el más simple de enchufar

**Lo oficial** ([market data](https://docs.tradier.com/docs/market-data), [límites](https://docs.tradier.com/docs/rate-limiting))
- El tiempo real es solo para titulares de Tradier Brokerage. Sin cuenta no hay ninguna solución en vivo.
- El sandbox viene demorado y sin índices ni griegas.
- Las griegas e IV son de ORATS y se actualizan **una vez por hora**, desde las 10:15 aprox. Para el 0DTE eso es viejo:
  la IV hay que sacarla de las puntas, con nuestro Black-Scholes.
- Los datos de mercado tienen 120 pedidos por minuto. El exceso devuelve 400, no 429.

**La cuenta** [medido + cálculo]
- Una cadena por vencimiento y por símbolo es 1 pedido.
- 10 vencimientos × 2 libros = 20 pedidos por vuelta: a una vuelta por minuto, sobra margen.

**Lo secundario**
- Argentina figura en la lista "TradeStation y Tradier" de [Option Alpha](https://docs.optionalpha.com/technical-documentation/platform/supported-countries).
  - El comunicado propio de Tradier (75 países, 06-07-2021) no lista países
    ([Tradier](https://www.tradier.com/company/press/tradier-brokerage-announces-a-new-digital-onboarding-process-for-customers-of-75-countries-to-enhance-the-experience-for-the-global-active-trading-community-and-its-marketplace-of-platforms)).
- Un agregador menciona un plan "API Developer" de USD 10/mes (no confirmado en tradier.com).

**Sin verificar hoy**
- Los nombres exactos de los campos de la cadena. La página de referencia no los muestra.
  - El informe del 15-09 dice volumen, OI, puntas y griegas [memoria].
- Si el símbolo `SPX` devuelve también las SPXW.

### Schwab

- Hace falta cuenta Schwab y una app aprobada en el portal de desarrolladores.
- El token de renovación vence cada 7 días: hay que volver a loguearse a mano en el navegador una vez por semana.
  No es "todo dentro del indicador".
- 120 pedidos por minuto [secundaria: [schwabdev](https://docsearch.algolia.com/mcp/docs/repo/tylerebowers/schwabdev)].
- **La elegibilidad de Argentina no se pudo confirmar.**
  - international.schwab.com da 403 al bot.
  - Option Alpha no lista a Schwab.
  - Schwab International publica USD 0 de depósito mínimo, pero exige mantener USD 1.000 [secundaria].

---

## 4. Feeds pagos (solo si la bitácora lo justifica)

### Tabla C

| Vía | Qué da | Atraso | Precio mensual hoy | Requisitos | Términos | Esfuerzo en la 5.0 |
|---|---|---|---|---|---|---|
| **MarketData.app Trader** | Cadena OPRA con volumen, OI, IV y griegas por REST | Vivo (no profesional) | **USD 75** mes a mes, o USD 30/mes pagando el año [oficial] | Contrato no profesional de OPRA | Personal; sin streaming salvo Commercial (USD 333+) | Bajo, todo dentro |
| **ThetaData Value / Standard** | Value: tiempo real a 1 min, 2 pedidos a la vez. Standard: tick, fotos de cadena, streams | Vivo | **USD 40 / USD 80** (Pro 160) [oficial] | Cuenta | Personal, sin redistribuir | Bajo en código, pero **exige el Theta Terminal (Java) abierto** |
| Massive (ex Polygon) | Advanced: vivo, WebSocket, griegas, OI diario, fotos de cadena | Vivo solo en Advanced; Starter (29) y Developer (79) van 15 min tarde | **USD 199** [oficial] | Solo no profesionales | Personal | Medio |
| Databento OPRA Standard | Vivo por TCP binario; historia L0 13 años | Vivo; el histórico va **1 h** atrasado | **USD 199** mes a mes [oficial] | Licencia en vivo incluida | Personal, hasta 2 equipos | **Alto**: sin cliente C# oficial |
| Cboe DataShop / LiveVol | Lo más institucional; Open-Close intradía con quién operó | — | All Access API USD 599 [memoria; 403 hoy]; Open-Close sin precio publicado | Contrato | Comercial | Alto |
| Intrinio real-time | OPRA por WebSocket con OI, IV y griegas | Vivo | Solo plan Enterprise, a consultar [oficial] | Contrato | Empresas | — |
| OptionsDepth Pro Max | Lo mismo que alimenta al X-Ray (posicionamiento de SPX) | Actualiza cada 1 min [oficial] | **USD 249**; API extra USD 99 por 6.000 unidades [oficial] | Cuenta | Personal | — (ya lo ves en X-Ray) |

### MarketData.app: los créditos

- Cada contrato que devuelve la cadena consume créditos
  ([límites](https://www.marketdata.app/docs/account/plan-limits); [precios](https://www.marketdata.app/pricing/)).
- Trader trae 100.000 créditos por día.
- Con el 0DTE de ±2 % (186 contratos) a una consulta por minuto, 390 minutos dan **72.540 créditos por día**: entra.
- Los 2.596 contratos de la 5.0 por minuto **no entran**: harían falta Quant (USD 125, 10.000 por minuto).
- **La prueba gratis de 30 días NO incluye tiempo real** para no profesionales. Durante la prueba, las opciones vienen con 24 h de atraso
  ([Trader Trial](https://www.marketdata.app/docs/account/plans/trader-trial/)).
  Probar gratis no sirve para medir el tiempo real.

### ThetaData: el programa aparte

- La documentación dice que hay que tener el Theta Terminal corriendo para acceder a cualquier dato
  ([suscripciones](https://thetadata.net/docs/Articles/Getting-Started/Subscriptions.html)).
  Es un programa externo, y choca con "todo dentro del indicador".
- El streaming de contratos arranca en Standard: 10.000 cotizaciones y 15.000 trades.
- Las fotos de cadena entera aparecen en Standard en la [página de precios](https://www.thetadata.net/pricing).
  Value tiene fotos de baja latencia a 1 min, pero no quedó claro si de la cadena entera.
- El plan gratis es solo de fin de día, con 1 día de atraso.

### Databento: qué costaría por día de uso (la cuenta del operador ya existe)

**El precio** [oficial, [catálogo OPRA.PILLAR](https://databento.com/catalog/opra/OPRA.PILLAR)]
- Lo vivo exige el plan **Standard de USD 199 por mes**, mes a mes. Incluye licencia, uso personal y hasta 2 equipos.
- **No hay pase diario.** El uso por consumo es solo histórico, y el histórico va **una hora atrasado**: peor que CBOE para esto.

**La cuenta**
- Prorrateado sobre 21 ruedas da **~USD 9,5 por rueda**, pero se paga el mes entero.
- Supera el techo de USD 60 del script y choca con "no pagar".

**El cliente**
- No hay cliente C# oficial: está pedido en su hoja de ruta.
  - Hoy C# va por el protocolo TCP crudo o por paquetes NuGet de terceros
    ([roadmap](https://roadmap.databento.com/b/n0o5prm6/feature-ideas/opra)).

**Ojo con la cuenta [memoria `databento-cuenta-y-costos`]**
- El crédito de USD 125 venció el 05-10.
- La cuenta tiene tarjeta cargada: cualquier bajada histórica nueva se cobra.
- **Veredicto: no viable bajo la regla actual.**

---

## 5. ¿Hay algo gratis con menos de 15 minutos?

**Legítimo, público y sin cuenta: no.** Lo que aparece y por qué no sirve:

- **CBOE, Yahoo, MarketChameleon, Barchart:** 15 min de atraso, por la regla de OPRA de arriba.
- **Opensera, versión gratis:** su sello es en tiempo real, pero la foto solo cambia a las :00 y a las :30 [medido 27-10,
  `CONTROL_MULTIFUENTE_2710.md` §1.2]. Igual queda afuera:
  - vende el **resultado** (el GEX ya calculado), no la cadena, y el protocolo exige recalcular desde lo crudo;
  - fuera del navegador responde 402 (muro de pago), así que leerla con un programa iría contra sus términos.
- **Alpaca, feed gratis:** no es OPRA. Son cotizaciones derivadas y trades con 15 min de atraso
  ([foro de Alpaca](https://forum.alpaca.markets/t/what-is-the-indicative-pricing-feed-for-options/14595)).
- **Prueba de MarketData.app:** sin tiempo real (§4).
- **Sandboxes de tastytrade y Tradier:** demorados.
- **Lo que sí es gratis porque ya está pagado:**
  - X-Ray: SPX minuto a minuto, solo en pantalla;
  - opciones de ES por Rithmic, en vivo.

---

## 6. Cómo se enchufaría cada vía a la 5.0

**El enchufe ya existe**
- La 5.0 consume cadenas por `IFuenteCboe`: `Arrancar`, `Parar`, `Fotos`, `Ultima`, `Version` y `Estado` (`_contratos/Contratos.cs:47`).
  - Recibe `FotoCadena` con `FilaCadena` = [K, V, OiC, OiP, IvC, IvP, VolC, VolP] más `DatoUtc` (la hora del dato).
- Una fuente viva sería **otra implementación de esa misma interfaz**, en `_modulos/vivo/`, sin tocar la cuenta. Es lo que preveía `DISENO_4_1.md` §3.1.

**Diseño común a cualquier vía**
- Mismo patrón que `BajadorCboe`: hilo propio, `BelowNormal`, nunca el pool de ATAS, gzip y lectura en trozos cuando sea HTTP, y un mutex entre procesos.
- **El OI se sigue tomando de CBOE.** El interés abierto es de ayer siempre (lo consolida la OCC de noche) y no cambia en la rueda.
  - La fuente viva solo tiene que aportar **volumen del día por contrato y precio o IV**. Eso relaja mucho la elección: un feed sin OI también sirve.
- **Control de paridad, obligatorio por el protocolo:**
  - cada minuto, el volumen vivo de hace 15 min tiene que coincidir con el que trae CBOE ahora, strike por strike;
  - un strike que falte en la viva y esté en CBOE se anota por nombre (regla 5).
- **Base SPX → ES:** con la cadena viva, la base por forwards se mide en vivo. Desaparece el desfase de "dato de hace 15 min contra futuro de ahora".
- **Claves y tokens** fuera del repo y fuera del `.ws`: en `%APPDATA%\PythiaGex\`, como `databento.key`. Nunca en los ajustes del indicador.
- **Sin ráfagas** (trampa conocida con Rithmic):
  - streaming solo de `Trade`, `Summary` y `Greeks`;
  - `Quote` solo del 0DTE ±2 %;
  - REST como mucho una vuelta por minuto.

### Por vía (de menos a más esfuerzo)

| Vía | Qué hay que construir | ¿Todo dentro del indicador? | Riesgos |
|---|---|---|---|
| IB por ATAS | Reusar `CadenaApi.cs` con el conector de IB y la Security de SPY (y de SPX, si existe) | No: TWS o Gateway aparte | Tope de 100 líneas; Quote Provider; logins de IB |
| Tradier | `FuenteTradier : IFuenteCboe`: GET de la cadena por vencimiento cada 60 s; IV propia desde las puntas | **Sí** (token de la cuenta; falta confirmar si vence) | Griegas viejas; campos sin verificar |
| MarketData.app | Igual que Tradier, con token en cabecera; cuidar los créditos | **Sí** | Pago; créditos |
| ThetaData | HTTP a 127.0.0.1 contra el Terminal | No: Terminal Java | Pago; un programa más |
| tastytrade | `ClientWebSocket` (DXLink: SETUP, AUTH, FEED_SETUP, suscripciones, keepalive 30 s), REST de cadenas y OAuth (renovar a los 15 min; token de cotización a las 24 h) | **Sí** | Reconexión, días feriados, símbolos SPXW; ancho de banda |
| Schwab | REST + streamer + OAuth | No del todo: re-login cada 7 días | Argentina sin confirmar |
| Databento | Protocolo TCP binario (DBN) a mano | Sí, pero mucho código | USD 199; sin cliente C# |

---

## 7. X-Ray de ATAS: qué es y qué muestra

Sección pedida para construir **nuestra propia versión** a partir de su lógica visible, sin copiarlo. Todo sale de la ayuda oficial de
ATAS (17 artículos en la carpeta [Options](https://help.atas.net/en/support/solutions/folders/72000576479)).
No se descompiló nada ni se leyó su caché ni su tráfico. **Hoy no se miró la pantalla** (queda propuesto en §9).

### 7.1 Qué es, de dónde sale el dato y con qué atraso

**Qué es**
- Un juego de indicadores construido sobre la analítica de **OptionsDepth**, derivada de datos de mercado de **CBOE** para opciones de **SPX**.
- Las posiciones salen **calculadas**, no estimadas: el dato dice quién operó.
- Se reparten en cinco grupos: minoristas, clientes profesionales, firmas, brokers y creadores de mercado
  ([Options Chain vs Options X-Ray](https://help.atas.net/en/support/solutions/articles/72000662644-options-chain-vs-options-x-ray-which-to-use), mod. 12-10).

**Subyacentes**
- Solo la familia del S&P 500: gráficos de **ES, MES, SPY y US 500**. ATAS calcula solo el corrimiento entre el gráfico y el índice.
- En NQ, CL, GC, acciones o cripto no dibuja nada. **No cubre NDX ni QQQ.**

**Cadencia y atraso**
- Recalcula **una vez por minuto**.
- **El atraso no está publicado** en ningún artículo.
  - El panel Market State muestra cuántos minutos tiene el último dato y se pone amarillo cuando llega tarde.
  - Las tiras dejan blanco el tramo que pasa más de 10 min sin dato fresco
    ([Market State](https://help.atas.net/en/support/solutions/articles/72000662226-options-x-ray-market-state), mod. 06-10).

**La fuente de la fuente** (inferido, ATAS no lo nombra)
- OptionsDepth dice que usa datos de CBOE marcados por tipo de participante, no inferidos de puntas ni de la cinta.
  - Cubre SPX y VIX y muestra fotos intradía de 1 min ([FAQ](https://optionsdepth.com/faq), [inicio](https://optionsdepth.com/)).
- El producto de CBOE con esa marca es el **Open-Close**
  ([DataShop](https://datashop.cboe.com/open-close-cboe-c2-exchange)):
  - fotos intradía de 1 o 10 min;
  - se entregan "minutos después" de cerrar cada intervalo;
  - por tipo de participante (cliente, cliente profesional, broker-dealer, firma, creador de mercado);
  - CBOE no lo describe como tiempo real.
- **Es razonable esperar unos minutos de atraso, no segundos.** Hay que medirlo en pantalla.

**Matiz crítico de un tercero**
- DeepCharts (septiembre de 2026) sostiene que el signo de la "posición del dealer" reconstruida con Open-Close depende de cómo se
  agrupan los participantes ([DeepCharts](https://www.deepcharts.com/blog/99-percent-accuracy-cboe-options-positioning)).
- Lo "medido" del X-Ray también tiene supuestos.

### 7.2 Panel por panel (X-Ray)

Unidades comunes: todo va en **contratos de ES**, no en contratos de opciones. Todo se recalcula cada minuto, salvo que se diga otra cosa.

1. **Key Levels**
   - Muros de compra y de venta: la zona más fuerte arriba y abajo del precio donde la cobertura del creador de mercado va **contra** el movimiento.
   - **Slippery:** zona cercana donde la cobertura va **a favor** y el precio puede pasar más rápido.
   - **Flip:** el límite de régimen. De un lado amortigua y del otro amplifica.
   - **Pin (0DTE):** el strike fuerte cercano donde las opciones que vencen hoy sostienen el precio. Viene apagado.
   - La fuerza de cada muro son los contratos de ES que el creador de mercado tendría que operar por cada punto.
   - Hasta 3 muros por lado y distancia máxima de 200 pts. Fuera de horario muestra una proyección ("outlook") para la apertura
     ([Key Levels](https://help.atas.net/en/support/solutions/articles/72000662643-options-x-ray-key-levels), mod. 11-10).
2. **Strike Profile (GEX/DEX/VEX/CEX)**
   - Perfil por strike al costado del gráfico. Modos:
     - gamma (amortigua o amplifica);
     - delta (los ES que el creador de mercado tiene contra cada strike);
     - vanna (lo que tendría que operar si cambia la volatilidad);
     - charm (lo que tendría que operar por el paso del tiempo);
     - posición neta de cada grupo de clientes.
   - Vencimientos a elegir: 0DTE (por defecto), el próximo, E+2 a E+4, la semana, el resto de la semana, OPEX, fin de mes, hasta 7 días o todos.
   - Unos 390 perfiles por rueda. Aclara que **no es interés abierto**
     ([Strike Profile](https://help.atas.net/en/support/solutions/articles/72000662228-options-x-ray-strike-profile-gex-dex-vex-cex-), mod. 12-10).
3. **Strike Heatmap**
   - Los mismos cinco modos y vencimientos, como mapa de tiempo por strike
     ([Strike Heatmap](https://help.atas.net/en/support/solutions/articles/72000662227-options-x-ray-strike-heatmap), mod. 12-10).
4. **GEX Heatmap**
   - Tiempo en el eje horizontal y precio en el vertical. Verde amortigua, rojo amplifica.
   - Una línea amarilla punteada marca el límite de régimen.
   - Muros con línea llena (tienden a sostener) o punteada (tienden a dejar pasar).
   - Usa todos los vencimientos juntos; las unidades son ES por punto
     ([GEX Heatmap](https://help.atas.net/en/support/solutions/articles/72000662225-options-x-ray-gex-heatmap), mod. 06-10).
5. **Charm Heatmap**
   - Dónde y cuándo el creador de mercado tendría que comprar (verde) o vender (rojo) ES solo porque pasa el tiempo.
   - Proyecta hasta el cierre con las últimas posiciones y agrega una columna para el día siguiente.
   - Es más fuerte cerca del cierre
     ([Charm Heatmap](https://help.atas.net/en/support/solutions/articles/72000662221-options-x-ray-charm-heatmap), mod. 06-10).
6. **Surface Profile**
   - Una foto numérica de los dos mapas en cada nivel de precio: gamma, vanna o charm.
   - Un modo "respuesta de cobertura" acumula la gamma si el precio se mueve N puntos (10 por defecto, mínimo 4).
     Usa todos los vencimientos
     ([Surface Profile](https://help.atas.net/en/support/solutions/articles/72000662229-options-x-ray-surface-profile), mod. 06-10).
7. **Hedge Pressure**
   - El cambio del delta del creador de mercado desde el inicio de la rueda. Cada rueda arranca de cero.
   - Si sube, el creador de mercado vende futuros; si baja, compra.
   - **Una delta de opción de SPX equivale a 2 contratos de ES.**
   - Se puede partir en "movimiento del precio" y "operaciones de clientes más el paso del tiempo".
   - 0DTE por defecto; promedio de 15 barras; cubre de 04:00 a 16:00 NY
     ([Hedge Pressure](https://help.atas.net/en/support/solutions/articles/72000662645-options-x-ray-hedge-pressure), mod. 14-10).
8. **Flow**
   - Cuánta exposición se reacomodó en cada barra y qué grupo lo hizo. Es como el volumen, pero partido por grupo y sin dirección.
   - Distingue el **neto** (el cambio final) del **reacomodado** (el movimiento en los dos sentidos). Marca los picos.
   - 0DTE por defecto; creadores de mercado ocultos
     ([Flow](https://help.atas.net/en/support/solutions/articles/72000662223-options-x-ray-flow), mod. 06-10).
9. **Flow Delta**
   - La dirección neta en la que se corrieron las posiciones en cada barra, hacia exposición alcista o bajista.
   - La mecha muestra cuánto se movió dentro de la barra.
   - Tiene modo acumulado y se puede partir por vencimiento. Por defecto suma minoristas y profesionales
     ([Flow Delta](https://help.atas.net/en/support/solutions/articles/72000662224-options-x-ray-flow-delta), mod. 06-10).
10. **Big Trades**
    - Los mayores cambios de posición (el 2 % más grande de la rueda; ajustable de 0,01 % a 20 %), con hora, strike y grupo.
    - Burbuja llena si el grupo sumó exposición; hueca si la redujo. El tamaño es el reacomodado.
    - No son operaciones de la cinta
      ([Big Trades](https://help.atas.net/en/support/solutions/articles/72000662220-options-x-ray-big-trades), mod. 12-10).
11. **Expected Move**
    - El corredor de cuánto puede moverse el precio hasta el vencimiento según las opciones.
    - Se dibuja minuto a minuto y deja ver la historia del ancho.
    - Vence a las 16:00 NY (diarias y semanales), 09:15 (mensuales AM) o 13:00 (medias ruedas). 1 sigma por defecto y una segunda banda opcional
      ([Expected Move](https://help.atas.net/en/support/solutions/articles/72000662222-options-x-ray-expected-move), mod. 12-10).
12. **Market State:** cinco conclusiones separadas, sin puntaje único ([Market State](https://help.atas.net/en/support/solutions/articles/72000662226-options-x-ray-market-state)):
    - **BACKDROP:** amortigua, amplifica o mixto. Un estado nuevo tiene que durar **3 minutos seguidos** y pasar **≥ 5 min** desde el último cambio. Es una histéresis, como la V22 de la 3.0.4.
    - **IMPULSE:** los clientes cargados al alza, a la baja o de los dos lados. Ventana de 15 min, umbral de **50 ES**, y los últimos 5 min tienen que coincidir.
    - **DECAY:** lo que el paso del tiempo obligaría a operar en los próximos 30 min, con precio plano, subiendo lento y bajando lento. Siempre condicional; sin valores si faltan menos de 45 min para el vencimiento.
    - **ROOM:** las distancias a las zonas, en "movimientos típicos de 30 min" (EM30).
    - **PRICE:** **no usa opciones**. Compara el desplazamiento neto con el recorrido total de las últimas 30 min de velas: más de 55 % es direccional, menos de 45 % es rotación.

**Fórmulas publicadas:** ninguna en forma de ecuación. ATAS da definiciones en palabras, unidades y umbrales: los de arriba y la
equivalencia de 1 delta de SPX = 2 ES.

### 7.3 Options Chain ("Options Suite"): el otro juego, sobre el conector propio

1. **GEX Profile**
   - El aporte de cada strike sale de la gamma, el tamaño de la posición, el multiplicador y el precio del subyacente.
   - Las barras van en **dólares por 1 % de movimiento**, con la convención de signo estándar.
   - **Los muros son siempre por OI**, aunque las barras se vean por volumen del día.
   - El zero gamma está definido solo como "la zona donde cambia el régimen", sin método.
   - "Wide range (sparse)" se suscribe a un strike sí y uno no cuando no alcanza el cupo
     ([GEX Profile](https://help.atas.net/en/support/solutions/articles/72000661536-options-gex-profile), mod. 19-08).
2. **Key Levels**
   - **Max Pain:** el strike donde es mínimo el valor intrínseco total que se paga al vencer, por OI.
   - **Muros de compra y de venta:** el strike con la gamma más positiva o más negativa en la ventana. Es OI ponderado por gamma, neto del otro lado, nunca por volumen.
   - **Niveles de volumen** (opcional): los de mayor volumen del día.
   - Analiza una sola serie
     ([Key Levels](https://help.atas.net/en/support/solutions/articles/72000661534-options-key-levels), mod. 19-08).
3. **Expected Move**
   - IV sacada de las cotizaciones vivas, sobre el tiempo que queda.
   - Banda interna de ~1 sigma (68 %) y externa de ~2 sigma (95 %). Si no hay IV, usa el straddle ATM.
   - Se dibuja como cono o corredor; se ancla al precio actual o al de cuando se agregó
     ([Expected Move](https://help.atas.net/en/support/solutions/articles/72000661535-options-expected-move), mod. 26-08).
4. **OI Profile**
   - OI o volumen del día por strike, espejado.
   - El modo "Net OI" es call menos put. Recomiendan el volumen del día para el 0DTE
     ([OI Profile](https://help.atas.net/en/support/solutions/articles/72000661533-options-oi-profile), mod. 19-08).
5. **Fuente**
   - ATAS recibe del conector las puntas, el último precio, el OI y el volumen del día, y calcula IV y griegas
     ([cómo usarlos](https://help.atas.net/en/support/solutions/articles/72000661129-how-to-use-option-analytics-indicators-in-atas), mod. 07-09).
   - En este ATAS eso es **solo CME por Rithmic**.

### 7.4 Qué podemos hacer nosotros, y con qué dato (sin copiar nada)

**Replicable hoy con lo que ya hay** (CBOE con 15 min, ES vivo y velas de MES)
- **Unidades en ES por punto (cuenta nuestra):**
  - SPX: GEX = 2 · Σ Γ·N. Sale de la equivalencia que publica ATAS (1 delta de SPX = 2 ES).
  - SPY: (2 / razón²) · Σ Γ·N, con la razón ES/SPY que ya mide la 5.0.
- **Expected Move minuto a minuto** desde la IV ATM del 0DTE o el straddle, con historia del ancho.
- **Pin 0DTE**: el strike de mayor gamma 0DTE cerca del precio. Es pariente de las dominantes.
- **Slippery y Flip** desde el signo del perfil.
  - Ojo con la trampa conocida del `ZeroPorSigno`: muchos cruces en el 0DTE. La regla de persistencia de BACKDROP (3 min + 5 min) es la idea a imitar.
- **El PRICE de Market State**: no usa opciones; es una cuenta sobre las velas de MES.
- **Charm y vanna por precio y hora**, con el OI de ayer y una convención de signo **supuesta**. Hay que rotularlo como supuesto.

**Mejora con una cadena viva**
- Todo lo anterior sin los 15 min, y la base SPX → ES medida en vivo.

**No replicable legítimamente gratis**
- Lo que depende de **quién** operó: los grupos, los modos "MM" con inventario medido, Flow, Flow Delta, Big Trades, IMPULSE, y la parte de "clientes" de Hedge Pressure.
- Necesita el Open-Close de CBOE (pago, precio presentado ante la SEC y no publicado) o OptionsDepth (USD 249/mes).
- Aproximación posible: el **lado agresor** de cada operación de opciones, comparando precio contra puntas.
  - Requiere la cinta con puntas: Databento `tcbbo` de SPXW cuesta USD 24,9 por día [memoria].
  - El `TimeAndSale` de tastytrade podría servir, pero **no se verificó** que esté disponible para opciones.
  - Es una estimación y se rotula como tal.

---

## 8. Riesgos y letra chica

- **Clasificación no profesional de OPRA.** Se declara bajo juramento al abrir cualquier cuenta o vendedor.
  - Un no profesional es una persona física que usa el dato para uso personal, no de negocio (guía de ThetaData).
  - **Operar capital de una firma de fondeo (Lucid) puede hacer dudar esa casilla.**
  - Preguntarle por escrito al broker o vendedor antes de firmar. Si saliera profesional, la tarifa OPRA pasa a ~USD 31,50 por mes por vendedor.
- **La web pública no puede mostrar nada derivado de OPRA en vivo.** Sería redistribución: USD 1.500 por mes (guía de ThetaData).
  - `subir_vivo.py` y github.io tienen que seguir con el dato retrasado de CBOE, aunque la 5.0 local use el vivo.
- **Uso "sin pantalla" (non-display).** Un programa propio en la PC propia no encaja en la definición de varias máquinas y usuarios de la guía, pero la guía no lo resuelve.
  - Con un broker, su contrato de API cubre el uso personal. Con un vendedor, preguntar.
- **Rithmic y Lucid.** Nada de esto toca sus credenciales.
  - El riesgo real es el ancho de banda dentro del mismo proceso que el socket de Rithmic: sin ráfagas, como `BajadorCboe`.
  - Con IB, que nunca quede como Quote Provider de ES o MES.
- **Términos de uso.** Todas las vías listadas son APIs o feeds oficiales para clientes.
  - Quedan descartados el scraping de tableros (Opensera y similares), cualquier lectura de X-Ray por fuera de su pantalla y las credenciales ajenas.
- **Datos secundarios.** Lo marcado [secundaria] se confirma con el proveedor antes de abrir nada: la elegibilidad de Argentina, el fondeo mínimo y la comisión que condona OPRA en IBKR.

---

## 9. Recomendación, en orden (gratis primero)

1. **Ahora, gratis y sin código: usar el X-Ray como testigo en la bitácora.**
   - Ponerle "Options X-Ray: Market State" a un gráfico de MES y anotar los minutos que marca su punto de estado. Es el atraso real de su dato, que no está publicado.
   - En cada sesión, anotar cuándo los muros o el flip de la 5.0 (CBOE, 15 min) quedaron corridos respecto del X-Ray, y si eso tocó una operación.
   - Es exactamente la evidencia que pide la regla de las 15 sesiones antes de gastar un peso. La pantalla la mira el operador; nosotros no leemos su dato.
2. **Seguir con CBOE + ES vivo como hoy** (gratis), y con el control de paridad.
3. **Si la bitácora muestra operaciones perdidas por el retraso, primero "gratis con cuenta":**
   - **tastytrade**: el más completo. Streaming con volumen del día, OI y griegas; 25.000 suscripciones; todo dentro del indicador. Argentina listada [secundaria]. Esfuerzo medio-alto.
   - **Tradier**: el más simple. REST como CBOE, 120 por minuto; la IV la calculamos nosotros. Argentina listada [secundaria]. Esfuerzo bajo.
   - Antes de construir, confirmar con el broker la elegibilidad, el fondeo mínimo y la clasificación no profesional con Lucid.
   - Después, una semana grabando en paralelo con CBOE, y que el laboratorio mida contra placebo.
4. **IBKR por ATAS** solo como experimento corto de SPY: USD 1,50/mes más USD 500 inmovilizados, 100 líneas, TWS abierto.
5. **Pago, solo si 3 no se puede:**
   - **MarketData.app Trader** a USD 75 mes a mes: REST, todo dentro, alcanza para el 0DTE ±2 % por minuto. No pagar el anual.
   - O **ThetaData Value/Standard** (USD 40/80): más barato y con historia para el laboratorio, pero con el Terminal Java afuera.
   - Massive y Databento (USD 199), LiveVol (USD 599+) y OptionsDepth (USD 249) no se justifican: lo de OptionsDepth ya se ve en el X-Ray.

---

## 10. Fuentes (todas consultadas el 27-10-2026)

**ATAS**
- [Option Board](https://help.atas.net/en/support/solutions/articles/72000661117-how-to-use-option-board-in-atas)
- [Indicadores de opciones](https://help.atas.net/en/support/solutions/articles/72000661129-how-to-use-option-analytics-indicators-in-atas)
- [GEX Profile](https://help.atas.net/en/support/solutions/articles/72000661536-options-gex-profile)
- [Key Levels](https://help.atas.net/en/support/solutions/articles/72000661534-options-key-levels)
- [Expected Move](https://help.atas.net/en/support/solutions/articles/72000661535-options-expected-move)
- [OI Profile](https://help.atas.net/en/support/solutions/articles/72000661533-options-oi-profile)
- [Chain vs X-Ray](https://help.atas.net/en/support/solutions/articles/72000662644-options-chain-vs-options-x-ray-which-to-use)
- X-Ray: [Key Levels](https://help.atas.net/en/support/solutions/articles/72000662643-options-x-ray-key-levels),
  [Strike Profile](https://help.atas.net/en/support/solutions/articles/72000662228-options-x-ray-strike-profile-gex-dex-vex-cex-),
  [Strike Heatmap](https://help.atas.net/en/support/solutions/articles/72000662227-options-x-ray-strike-heatmap),
  [GEX Heatmap](https://help.atas.net/en/support/solutions/articles/72000662225-options-x-ray-gex-heatmap),
  [Charm Heatmap](https://help.atas.net/en/support/solutions/articles/72000662221-options-x-ray-charm-heatmap),
  [Surface Profile](https://help.atas.net/en/support/solutions/articles/72000662229-options-x-ray-surface-profile),
  [Hedge Pressure](https://help.atas.net/en/support/solutions/articles/72000662645-options-x-ray-hedge-pressure),
  [Flow](https://help.atas.net/en/support/solutions/articles/72000662223-options-x-ray-flow),
  [Flow Delta](https://help.atas.net/en/support/solutions/articles/72000662224-options-x-ray-flow-delta),
  [Big Trades](https://help.atas.net/en/support/solutions/articles/72000662220-options-x-ray-big-trades),
  [Expected Move](https://help.atas.net/en/support/solutions/articles/72000662222-options-x-ray-expected-move),
  [Market State](https://help.atas.net/en/support/solutions/articles/72000662226-options-x-ray-market-state)
- [Conexión IB](https://help.atas.net/en/support/solutions/articles/72000602587)
- [Changelog 04-08-2026](https://feedback.atas.net/changelog/new-mbo-bundle-and-options-analysis-are-now-in-atas)

**OptionsDepth y CBOE**
- OptionsDepth: [FAQ](https://optionsdepth.com/faq), [inicio y precios](https://optionsdepth.com/)
- CBOE: [Open-Close en DataShop](https://datashop.cboe.com/open-close-cboe-c2-exchange); All Access API: 403 hoy, dato del 15-09 [memoria]
- Crítica: [DeepCharts sobre Open-Close](https://www.deepcharts.com/blog/99-percent-accuracy-cboe-options-positioning)

**OPRA**
- [Guía de tarifas de ThetaData (29-05-2026)](https://thetadata.net/articles/2026-05-29-opra-fee-guide-for-options-market-data)

**Brokers**
- tastytrade: [streaming](https://developer.tastytrade.com/docs/concepts/streaming/),
  [guía](https://developer.tastytrade.com/docs/guides/stream-market-data/),
  [cuentas internacionales](https://tastytrade.com/learn/accounts/account-types/international-account/),
  [QuantConnect](https://www.quantconnect.com/docs/v2/cloud-platform/datasets/tastytrade),
  [SDK](https://tastyworks-api.readthedocs.io/en/latest/market-data.html)
- Tradier: [market data](https://docs.tradier.com/docs/market-data),
  [límites](https://docs.tradier.com/docs/rate-limiting),
  [cadenas](https://docs.tradier.com/reference/brokerage-api-markets-get-options-chains)
- IBKR: [datos de mercado y mínimos](https://www.interactivebrokers.com/campus/ibkr-api-page/market-data-subscriptions/)
  (las páginas de precios dan 403 al bot: precios vía buscador)
- Países: [Option Alpha](https://docs.optionalpha.com/technical-documentation/platform/supported-countries)
- Schwab: [schwabdev](https://docsearch.algolia.com/mcp/docs/repo/tylerebowers/schwabdev);
  [Schwab International](https://international.schwab.com/brokerage-account) (403 al bot)
- [Alpaca, feed indicativo](https://forum.alpaca.markets/t/what-is-the-indicative-pricing-feed-for-options/14595)
- [Webull, API de mercado (Singapur)](https://developer.webull.com.sg/apis/docs/market-data-api/overview)

**Vendedores**
- MarketData.app: [precios](https://www.marketdata.app/pricing/),
  [límites](https://www.marketdata.app/docs/account/plan-limits),
  [prueba](https://www.marketdata.app/docs/account/plans/trader-trial/)
- ThetaData: [precios](https://www.thetadata.net/pricing),
  [suscripciones](https://thetadata.net/docs/Articles/Getting-Started/Subscriptions.html)
- [Massive](https://massive.com/options)
- Databento: [catálogo OPRA](https://databento.com/catalog/opra/OPRA.PILLAR),
  [roadmap C#](https://roadmap.databento.com/b/n0o5prm6/feature-ideas/opra)
- [Intrinio](https://intrinio.com/options/options-realtime)

**Locales**
- `PythiaGex/atas/PythiaGexCuatro/DISENO_4_1.md` §3.1
- `_diseno/CONTROL_MULTIFUENTE_2710.md` §1.2
- `_contratos/Contratos.cs`
- `%APPDATA%\ATAS\PythiaGex5\cboe\cadena-ES-2026-10-27.jsonl.gz` y `cadena-SPY-2026-10-27.jsonl.gz`
- Memorias `apis-datos-opciones-2026-09-15`, `databento-cuenta-y-costos`, `atas-8-0-15-api-opciones`, `costo-real-del-retraso`, `retraso-cboe-902s`
