---
name: databento-cuenta-y-costos
description: La cuenta de Databento del operador (credito gratis, tarjeta cargada sin limite), la clave local, el techo del script y los costos exactos medidos por esquema
metadata:
  type: reference
---

Cuenta creada por el operador el 2026-09-07 (usuario 3SAPU5PW, clave
"prod-001"). La clave vive en `%APPDATA%\PythiaGex\databento.key`, fuera del
repo. Credito: USD 125, vence 2026-10-05. OJO: la cuenta tiene tarjeta
(**** 5172) y "uso sin limite" habilitado: pasado el credito, cobra. Por eso
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

**How to apply:** una rueda completa de todo cuesta ~USD 4,3; validar contra
las 706 fotos propias del 09-03 antes de comprar mas dias. Ver
[[fuentes-datos-historicos]].
