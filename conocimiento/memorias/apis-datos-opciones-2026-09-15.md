---
name: apis-datos-opciones-2026-09-15
description: "Investigacion del 15-09-2026 sobre que API de opciones contratar hasta USD 100/mes: lo oficial (OPRA solo por vendedores; CBOE desde 500-599; CME ya por Rithmic) queda afuera; bajo 100 entran ThetaData Standard 80 (el mas completo, 8 años de historia), MarketData.app Trader 30 anual/75 mensual (OPRA oficial, sin streaming) y el camino del broker (Tradier 0, IBKR 1,50, TradeStation 3, Schwab 0). Redistribuir OPRA en la web publica cuesta 1.500/mes. Recomendacion: no pagar todavia; probar broker; si el laboratorio lo justifica, ThetaData."
metadata:
  type: reference
---

Informe completo con precios, condiciones, reputacion y fuentes (todas consultadas el 15-09-2026):
`PythiaGex/conocimiento/investigaciones/2026-09-15-apis-datos-opciones.md`.

Lo esencial:
- Lo que falta es la cadena de SPX/SPY/QQQ SIN los 15 min de CBOE; ES/NQ ya llegan por Rithmic.
- OPRA no vende a particulares (tarifa no profesional USD 1,25/mes via vendedor; redistribucion USD 1.500/mes;
  sin pantalla USD 2.000/mes). CBOE LiveVol API USD 599/mes. Databento vivo USD 199. ORATS 199/299. Massive
  tiempo real 199. Alpaca 99 sin OI. Unusual Whales 150 (derivado). FlashAlpha nuevo y derivado.
- Bajo 100: ThetaData Standard 80 (NBBO tick, snapshots, griegas 1/2/3 orden, OI diario, 8 años; Trustpilot 4,4/14;
  sin reembolsos; empresa chica), MarketData.app Trader (30 anual / 75 mensual; REST sin streaming; Trustpilot 4,5/109),
  brokers con API (Tradier 0 con griegas ORATS por HORA; IBKR 1,50 con 100 lineas de tope; TradeStation 3; Schwab 0).
- La web publica no puede mostrar OPRA en tiempo real: privada o seguir con CBOE retrasado.

**How to apply:** si aparece el impulso de pagar, primero la regla de las 15 sesiones y la medida del costo del
retraso ([[costo-real-del-retraso]]); despues probar Tradier/IBKR gratis y medir en el laboratorio; recien ahi
ThetaData Standard mes a mes. Ver [[fuentes-datos-historicos]], [[databento-cuenta-y-costos]], [[retraso-cboe-902s]].
