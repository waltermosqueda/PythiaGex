---
name: la referencia-web-vs-gexbot-2011
description: "20-11-2026: la app web de la referencia trae las mismas pantallas y campos que GEXBot Classic, la convexidad de State y las 8 metricas de Orderflow; como entrar y que quedo sin probar"
metadata:
  node_type: memory
  type: project
  originSessionId: f223fe47-37a3-4f31-9f95-4d26b0bbe4d6
  modified: 2026-11-20T20:32:28.758Z
---

Pedido del operador (20-11-2026): antes de contratar GEXBot, ver si la app web de la referencia que ya tiene es un
equivalente mas barato. Ver [[videos-trader-gexbot-2011]] y [[la referencia-es-formula]].

**Como se entra (importante):** la app de escritorio "Dashboard - la referencia trader" es una PWA de Edge: con control
de pantalla solo se puede MIRAR (clic y scroll rechazados, nivel "read" de navegadores; no saltarlo con scripts).
Para manejarla hay que abrir `<sitio de la referencia> en el navegador de Claude (panel) y que el
operador inicie sesion ahi una vez (la clave nunca la escribo yo). Los botones de la leyenda aparecen al pasar el
mouse; el buscador de indicadores y "Ir a" (fecha o rango) funcionan bien. No toque "Guardar".

**Medido en la app (20-11, ~15:10-15:18 NY, QQQ):**
- la referencia: ticker + vencimiento 0DTE / 1DTE / Full; "GEX profile" (izquierda) y "Gamma profile" (derecha), con
  marcadores; Dominant 1/2 history, Zero gamma history/level, Major positive/negative level.
- Max Change: panel con los MISMOS campos y orden que el panel de GEXBot Classic (update date/time/spot; volume:
  zero gamma, major positive, major negative, net gex; open interest: major positive, major negative, net gex;
  max change gex 1/5/10/15/30 min). Rotula "Bn" donde GEXBot pone MM.
- Orderflow: vencimiento + las 8 metricas de GEXBot Orderflow con los mismos nombres y orden (Aggregate DEX, Net
  GEX, Net Convexity, Net Vanna, Net Charm, DEX/GEX/Convexity Orderflow); mismas tres curvas y colores.
- Otros: <estudio de flujo de la referencia> (guarda historia; el frontend pide `/v2/market_tide`), Momentum, Unusual Flow (estrellas sobre
  el grafico), Flow, Monitoring Flow, Doble eje; en futuros ademas Big Trades, CVD y Heatmap. 44 simbolos.
- El frontend solo habla con `api.<sitio de la referencia> (no se ve el proveedor desde el navegador). El sitio de
  GEXBot no nombra a la referencia como socio. Proveedor = GEXBot: muy probable por los campos, NO confirmado.
- La historia de las rayas de la referencia no llego al 12-11 (8 dias atras); <estudio de flujo de la referencia> si.

**Comparacion numerica hecha:** niveles de GEXBot Classic leidos en los videos (QQQ 10, 11 y 12-11) contra
gamma x (volC - volP) 0DTE sobre `%APPDATA%\ATAS\PythiaGex\cboe-local\cadena-QQQ-*.jsonl.gz` a la misma hora:
major positive y major negative iguales 3 de 3 (tambien por OI), zero gamma a 0,12-0,17 USD. Script:
`_referencia\2026-11-20-videos-gexbot\scripts\classic_vs_cboe.py` (si no esta, quedo en el scratchpad).

**Precios publicos de la referencia (20-11):** Classic Web 82 USD/mes (49,20 con <cupon de la referencia>, promo hasta 29-11);
Pro Web o Pro Ninja 150 (90 con cupon). El operador ya tiene Pro (ve <estudio de flujo de la referencia>, Orderflow, Flow).

**Segunda pasada (20-11, 15:24-15:30 NY), todos los indicadores probados:**
- Menu (arriba izq.) > "la referencia Classic" abre `/la referencia-classic`: pares NQ/NDX, NQ/QQQ, NQ/TQQQ, ES/SPX, ES/SPY, GC/GLD;
  la referencia 0DTE + Big Trades (filtro 100) + Doble eje (eje de precio del indice) + Max Change + CVD.
- Heatmap (solo en graficos de futuros): MATRIZ de GEX strike x vencimiento (5 vencimientos + "Resto"), vista Agrupada o
  Strike a strike, marcas MC/MP. Sin separacion calls/puts.
- Flow: histograma de flujo en dolares por minuto (alcista/bajista, promedio, minutos inusuales con estrellas).
  Monitoring Flow: tabla por ACTIVO (SPX, SPY, QQQ, NDX, DIA, IWM, TQQQ, VIX, GLD) con flujo $, fuerza, hora y conteo.
- CONCLUSION FIRME: en los 20 indicadores y las dos vistas NO hay Options Profile (calls/puts compradas y vendidas por
  strike) ni DEX por strike. Lo mas cercano es el "Gamma profile" neto (derecha de la referencia), cuyo origen no se confirmo.

**El Options Profile propio en ATAS (verificado con datos del disco):** la Gamma Hoy de septiembre-octubre enganchaba
`IDataFeedConnector.NewTrades` (OrderDirection Buy/Sell = lado del agresor que da el exchange, mejor que la clasificacion
inferida de GEXBot) y grababa `vol_compra`/`vol_venta` por opcion en `%APPDATA%\ATAS\PythiaGex\viva\viva-NQ-*.jsonl`.
Con eso se armo el perfil (calls compradas/vendidas, puts compradas/vendidas por strike) del 07, 08 y 13-10:
`PythiaGex/laboratorio/perfil_opciones/perfil_opciones_rithmic.py <archivo> <HH:MM UTC>`. LIMITE: la cinta vio entre 0,1 %
y 67 % del volumen del dia (pocas suscripciones, tope 200 fuera de la API). La 5.0 actual (CadenaApi.cs, por la API)
NO engancha NewTrades: `viva3-ES-*` tiene vol_cinta/compra/venta en 0 todos los dias. Falta probar si las opciones
suscriptas por la API disparan NewTrades (codigo de referencia: `atas/PythiaGexDos/CadenaViva.cs` lineas 726-733 y 969-974).

**Falta:** comparar el "Gamma profile" de la referencia contra la convexidad de GEXBot State en el MISMO minuto (hace falta
acceso a GEXBot); probar el enganche de NewTrades sobre opciones suscriptas por la API (indicador sonda en ATAS).

**How to apply:** la referencia Pro cubre Classic y Orderflow de GEXBot; NO tiene Options Profile ni DEX por strike. Antes de
pagar State, intentar el Options Profile propio en ATAS (dato del exchange, gratis). Ver [[atas-8-0-15-api-opciones]],
[[volumen-opciones-en-vivo]].
