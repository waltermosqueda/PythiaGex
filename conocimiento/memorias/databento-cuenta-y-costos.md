---
name: databento-cuenta-y-costos
description: "La cuenta de Databento del operador (credito gratis, tarjeta cargada sin limite), la clave local, el techo del script y los costos exactos medidos por esquema"
metadata:
  node_type: memory
  type: reference
  originSessionId: 233bf8d4-5113-4e95-a0d9-68bff726690d
  modified: 2026-11-27T12:47:00.152Z
---

Cuenta creada por el operador el 2026-09-07 (usuario <usuario de Databento>, clave
"prod-001"). La clave vive en `%APPDATA%\PythiaGex\databento.key`, fuera del
repo. Credito: USD 125, vence 2026-10-05. OJO: la cuenta tiene tarjeta
(<tarjeta>) y "uso sin limite" habilitado: pasado el credito, cobra. Por eso
`herramientas/databento_bajar.py` tiene TOPE_USD = 60 y un ledger en
`datos/databento/ledger.jsonl`; nunca bajar sin pasar por ahi. Pedirle al
operador que ponga un limite en Billing > Usage-based access > Manage (no
tocar la configuracion de su cuenta por el).

Precios medidos con get_cost (2026-09-03, una rueda):
- Futuro ES: ohlcv-1m de 3 meses USD 0,55; trades de ESU6 un dia USD 0,52;
  MESU6 trades un dia USD 0,38.
- Opciones de ES (23 padres: ES, EW, EW1-4, E1A-E4A, E1B-E5B, E1C-E4C,
  E1D-E4D; E5A/E5C/E5D no existen): definition 0,10 + statistics 0,17 +
  ohlcv-1m 0,23 + trades 0,14 = USD 0,64 por dia. bbo-1m es caro (3,26).
- Opciones de SPX (OPRA, padres SPX.OPT y SPXW.OPT): definition 0,05 +
  statistics 0,31 + ohlcv-1m 2,22 = USD 2,6 por dia. trades de SPXW USD 19,9
  por dia y tcbbo 24,9: NO. cbbo-1m SPXW 1,05 pero 565 MB.
- EODHD: plan gratis de 20 llamadas/dia, un anio de EOD, sin opciones ni
  futuros. No sirve para esto. Token en el panel del operador.

Gastado al 2026-09-07 21:00 UTC: USD 32,20 = 13 ruedas de SPX/SPXW (08-19 a 09-04, sin 09-03 que ya estaba) + opciones de ES del 09-03 + ESU6 trades + ES.FUT 1m de 3 meses. Quedan ~93 de credito; el techo del script sigue en 60.

**2026-09-08 ~03:00 UTC:** al intentar bajar NDX/NDXP (USD 0,71 por rueda, 13
ruedas ~9) y NQ.FUT (0,55), la API devolvio `402 account_insufficient_funds`
con USD 32,20 gastados de 125: el operador puso un limite de gasto en la
cuenta (lo que yo le sugeri) o el credito no es usable mas alla de eso. No
tocar Billing: pedirle que suba el limite si quiere el libro de NDX para el
grafico de MNQ. Los 13 dias de NDX quedan pendientes; el convertidor ya sabe
`--libro NDX` y rebobinar_todo `--libro NDX --solo-convertir`.

**2026-11-27 (cotizado gratis con get_cost, nada comprado):** para el juez ciego de A3 (R23) alcanza el schema **trades** (A3 usa solo hora,
precio y volumen; las filas de operaciones son las mismas que en tbbo). ES.v.0 (frente por volumen): jun-ago 2023 **USD 31,46** (1,21 GB,
~63 ruedas); mar-ago 2023 USD 65,56; todo 2023 USD 132,54; tbbo jun-ago 2023 USD 52,43 (ES.c.0 tbbo 49,95). Dia de control 25-11-2026 (tiene
cinta de la sonda) USD 1,17. El credito de 125 vencio el 05-10: lo que se compre cobra a la tarjeta, si el limite de gasto lo deja (402 el
08-09). Convertidor listo: `herramientas/databento_a_cinta.py <carpeta> <archivos>` (una fila por operacion, cintas por sesion CME con
.listo, NUNCA en la carpeta flujo de los jueces en vivo). Validado con ESU6 trades del 03-09-2026: las 78 velas de 5 min de la rueda
IDENTICAS a las de ATAS (o/h/l/c 0,00, volumen 1.143.530 = 1.143.530). Falta el control contra una cinta de la sonda (VWAP < 0,25 por cierre
y mismas operaciones de A3), que pide el PREREGISTRO_VIVO de la R23 antes de juzgar.

**How to apply:** una rueda completa de todo cuesta ~USD 4,3; validar contra
las 706 fotos propias del 09-03 antes de comprar mas dias. Ver
[[fuentes-datos-historicos]].
