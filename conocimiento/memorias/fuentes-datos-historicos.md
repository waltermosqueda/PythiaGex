---
name: fuentes-datos-historicos
description: Que datos crudos existen para backtestear (futuros y cadenas de opciones), que cuesta cada uno, que tenemos guardado y por que el cuello de botella es la cadena intradia, no el futuro
metadata:
  type: reference
---

Relevado el 2026-09-07 (Labor Day, mercado cerrado). Verificado en las paginas
de cada proveedor, no de memoria.

**Lo que ya tenemos, gratis:**
- ATAS + Rithmic ya tiene anos de velas de MES/MNQ en `%APPDATA%\ATAS\Cache_v2`
  (m30/m60/diario desde 2024-04, m5 desde 2026-07, m1 24 dias). Ticks no
  (Rithmic limita a 40 GB por semana). Lo del futuro NO es lo escaso.
- Una sola jornada completa de cadena SPX intradia: 2026-09-03, 706 fotos cada
  ~70 s (`datos/cache/_SPX-20260903-*.json.gz`, 1,6 MB c/u, 28.492 contratos
  con bid/ask/IV/OI/volumen). Otros dias: 72 (08-31), 35 (09-01), 3 (09-02).
- `datos/historico/_SPX-<dia>.jsonl` (desde 08-29, ~66 lineas/dia cada 15 min
  por GitHub Actions): GEX y OI por strike, PERO SIN VOLUMEN por strike.
- Centinela de Gamma Vivo desde 2026-09-04 (MES M1/M5, MNQ M5).

**ATAS no importa archivos.** No hay conector custom publico (las clases
BaseConnector existen en ATAS.DataFeedsCore pero los conectores viven en
`%APPDATA%\ATAS\Connectors` y son de ATAS). Market Replay: "Ticks + DOM" 1
dia, "Ticks + DOM generado" 1 semana, "generado" ilimitado pero inventa. Al
usuario se le tilda. Salida: correr el indicador sobre la HISTORIA del grafico
(OnCalculate corre por cada vela al cargar) leyendo fotos de cadena por hora.
Puente posible sin verificar: NinjaTrader 8 (gratis) Playback (90 dias de
replay tick gratis con cuenta de datos) + indicador ADataFeeder8 -> ATAS.

**Opciones intradia (el cuello de botella):**
- Databento GLBX.MDP3 opciones de ES (el mismo libro que CadenaViva por
  Rithmic): desde 2010-06, esquemas Trades/Definition/Statistics (OI y
  volumen por strike), CSV/JSON/Parquet, precio por GB solo con cuenta; USD
  125 de credito gratis al registrarse, vence a los 6 meses.
- ThetaData "Options Value" USD 40/mes: 1 minuto, 4 anos, indices (SPX)
  incluidos, endpoint de OI historico en el tier Value. "Standard" USD 80 tick.
- Cboe DataShop: EOD USD 400/mes por pedido, intradia 10 min USD 750/mes,
  1 min USD 2.500/mes; bid/ask del indice requiere licencia CGI USD 1.000/mes.
- optionsDX: cadenas SPX 1 min pero 2010-2023, USD 0-50.
- HistoricalData.net: EOD SPX con volumen y OI desde 2002; 2013 gratis.
- Kaggle: SPX derivados 2017-2025 (sin cadena cruda); CME ES 2000-2022.

**Futuros (por si hiciera falta fuera de ATAS):** FirstRate ES 1 min desde
2008 (USD 99,95/ano tras el primer mes, muestra gratis); Databento; QuantPlace
2 meses gratis de 1 min; Barchart Premier.

**Why:** cada dia sin archivar la cadena completa es un dia de backtest
perdido; y la regla del proyecto es no pagar suscripciones todavia.
**How to apply:** primero archivar la cadena flaca (strike, venc, OI, vol, IV)
cada corrida de Actions; despues modo "fuente = archivo" en Gamma Hoy; pagar
solo si el operador lo decide con el dato de cuantos dias compra.
Ver [[market-replay-que-reproduce]], [[indicador-que-cuelga-atas]],
[[cadena-es-en-vivo-rithmic]], [[retraso-cboe-902s]].
