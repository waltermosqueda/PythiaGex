---
name: sonda-cinta-historica-atas
description: "ATAS entrega, gratis, la cinta historica orden por orden CON la punta del libro antes y despues de cada orden (semanas hacia atras); como pedirla con la sonda de Flujo Claro, la trampa de la sesion entera, y que NO hay historia del libro (solo grabar en vivo)."
metadata: 
  node_type: memory
  type: reference
  originSessionId: 9345174c-7f69-4fe5-a793-976e41f2dc7c
  modified: 2026-09-17T20:04:40.223Z
---

Descubierto el 2026-09-17 buscando un complemento/gatillo para el CVD (pedido del operador).

**Lo que hay adentro de ATAS (por reflexion sobre ATAS.Indicators.dll):**
- `RequestForCumulativeTrades(new CumulativeTradesRequest(desde, hasta, minVol, 0))` -> `OnCumulativeTradesResponse`. Cada
  `CumulativeTrade` trae `Time` (ms), `FirstPrice`, `Lastprice`, `Volume`, `Direction`, `Ticks` y **`PreviousBid/PreviousAsk/NewBid/NewAsk`
  (precio y tamaño de la punta ANTES y DESPUES de la orden)**. Con eso se arma: absorcion REAL en la punta (la orden consumio todo
  el bid visible y el bid no bajo = iceberg), barridos (ultimo != primero), delta por tamaño de orden y un OFI historico (total y pasivo).
- `GetCumulativeTradesMaxDepth` dice 7 dias, pero en la practica devolvio sesiones completas de **mas de un mes atras** (MNQ, ~800 mil
  ordenes por sesion, 45 s y ~75 MB cada una).
- **TRAMPA:** para una fecha pasada ATAS ignora desde/hasta y devuelve la SESION ENTERA (22:00 UTC -> 21:00 UTC). Pedir tramos de 30 min
  trajo 414.184 ordenes por cada tramo (hubo que reiniciar ATAS para cortarlo). Se pide de a UNA sesion (tramo de 1380 min desde las
  22:00 UTC); la sonda filtra por tiempo y se corta sola si el bloque se repite.
- El grafico continuo "MNQ" devuelve el contrato que era frente en esa fecha (U6 antes del roll); "MNQZ6" devuelve Z6.
- `DataProvider.OnlineDataProvider.GetMarketDepthSnapshotsAsync` (fotos historicas del libro) existe pero devuelve **0 fotos**: no hay
  historia del libro. `GetMarketDepthSnapshot()` en vivo da ~3.300 niveles; tambien hay MBO en vivo (`SubscribeMarketByOrderData`,
  `OnMarketByOrdersChanged`), sin probar.

**Como se usa (Flujo Claro >= 1.5, `atas/PythiaGexNiveles/FlujoClaroSonda.cs`):** escribir una linea en
`%APPDATA%\ATAS\PythiaGex\flujo\sonda-<instrumento>.txt` (el indicador la toma en 2 s y la borra):
`info` | `cinta;2026-09-15T22:00:00;2026-09-16T21:00:00;1;1380` | `cancelar`. Sale `cinta-<inst>-<desde>-<hasta>.csv` + `.listo`.
Progreso en `%APPDATA%\ATAS\pythiagex-flujoclaro.log` (lineas 'sonda'). El grabador del libro en vivo escribe
`libro-vivo-<inst>-<dia>.csv` (una fila por segundo: punta, OFI, profundidad 5/10/20, orden mas grande, comprado/vendido).

**Laboratorio:** `laboratorio/gatillo/base.py` (tabla de 1 s por sesion cacheada en `datos/flujo/cache/`, fuera de git; barrera doble del
scalper; corte explorar/confirmar 2026-09-08; costo 0,85 pts). La PC es un i3 de 4 nucleos con ATAS abierto: base.py baja la prioridad de
todo proceso que la importe; nunca correr varios Python pesados a la vez.

**Why:** el cuello de botella de toda la investigacion de gatillos era no tener datos finos; estaban adentro de ATAS y gratis.
**How to apply:** antes de pensar en pagar datos (Databento tiene limite de gasto puesto por el operador), pedirle a la sonda. Ver
[[flujo-claro-cvd-superador]], [[gatillo-cientifico-2026-09-10]], [[fuentes-datos-historicos]].
