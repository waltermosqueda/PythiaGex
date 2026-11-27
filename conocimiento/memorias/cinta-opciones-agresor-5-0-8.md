---
name: cinta-opciones-agresor-5-0-8
description: "ATAS+Rithmic da el lado del agresor de cada operacion de opciones de ES (5.0.7/5.0.8, 20-11); regla de cotizacion 96 %; X-Ray cohortes vacias."
metadata:
  node_type: memory
  type: project
  originSessionId: f223fe47-37a3-4f31-9f95-4d26b0bbe4d6
  modified: 2026-11-20T21:21:29.164Z
---

Medido el 20-11-2026 (viernes, 16:00-16:20 NY, muestra chica):

- **La 5.0 (PythiaGexCinco) desde 5.0.7 pide Prints directo** a los ~84 contratos mas cercanos al dinero del vencimiento vivo
  y engancha `IDataFeedConnector.NewTrades`: cada `Trade` trae `OrderDirection` Buy/Sell = lado del agresor real de Rithmic.
  Cupo oficial de ATAS: 200 directos por proceso (TopeCinta 180). viva3 ahora llena vol_cinta/vol_compra/vol_venta.
- **Diario crudo**: `%APPDATA%\ATAS\PythiaGex5\cinta-opciones\diario-<dia>.jsonl` (lineas `tr` operacion y `su` cambio de
  volumen del Summary con puntas antes/despues). `Trade.Id` llega SIEMPRE 0; el agresor se identifica por `ag`.
- **5.0.8**: al arrancar relee el diario del dia (antes una recarga del indicador lo ponia en cero: 891 -> 419) y `Parar()`
  cierra el archivo. Verificado: log 144 ops / 48 contratos / compra 436 / venta 204 = recalculo del crudo.
- Rithmic NO reenvia operaciones viejas al resuscribir (127/127 con < 3 s de demora). Las "repetidas" por agresor son barridas
  de dos precios, legitimas (el Summary suma lo mismo).
- **Regla de la cotizacion** (precio >= ask previo = compra, <= bid = venta) contra el lado real: 103/107 = 96,3 % de eventos,
  90,3 % del volumen. Sirve para extender la clasificacion a los 512 contratos de la API sin pasar el cupo de 200. Muestra de
  un solo tramo de un dia: re-medir con una rueda RTH entera antes de afirmarlo.
- ~30 % del volumen del Summary no aparece en Prints (ej. C7040: +74 en el Summary, 16 en Prints). Causa NO medida
  (sospecha: patas de spreads). No inventar la explicacion.
- **X-Ray Strike Profile**: Positioning "Delta (DEX) — MM" + Style "Calls | Puts" dibuja calls y puts por strike (modelo de
  ellos, no verificable desde el crudo). Las cohortes (retail, pros, firms, brokers, all customers) vinieron VACIAS a las
  16:10 NY de un viernes; no se sabe si es por horario o por plan. Ajuste del operador: Gamma (GEX) — MM, Net (one-sided).

Ver [[la referencia-web-vs-gexbot-2011]], [[atas-8-0-15-api-opciones]], [[pythiagex-5-0-gamma-es]].
