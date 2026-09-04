---
name: cme-settlements-un-dia-atras
description: "El endpoint Settlements de CME da el interés abierto del día ANTERIOR. Hay que sumarle el change de Volume/Details, o usar directamente el atClose de ahí."
metadata: 
  node_type: memory
  type: reference
  originSessionId: 15e03ce1-51d8-43ea-8c35-a2fa1a4b8145
  modified: 2026-09-04T00:08:20.249Z
---

Medido el 2026-09-03 sobre `EW1U26` (semanal viernes de ES, productId 2915),
`tradeDate=09/02/2026`, en 23 strikes de 7700 a 7810.

```
Settlements + change  =  Volume/Details      23 de 23 en calls
                                             23 de 23 en puts
```

Sin una sola excepción. O sea:

- **`/CmeWS/mvc/Settlements/Options/Settlements/{pid}/OOF`** entrega el interés
  abierto del **día ANTERIOR** al `tradeDate` pedido.
- **`/CmeWS/mvc/Volume/Details/O/{pid}/{YYYYMMDD}/P`** entrega el **actual** en
  su campo `atClose`, y el `change` es exactamente la diferencia entre los dos.

## Por qué importa, con el caso que lo destapó

Se comparó el interés abierto de Rithmic contra el de `Settlements` y salieron
diferencias de hasta 75 % en algún strike. Con eso se calculó que **el major
positive se corría 50 puntos** según la fuente — 7800 con Rithmic contra 7750
con "CME oficial" — y se llegó a plantear que había que cambiar de fuente.

**Era al revés.** Rithmic coincide **exacto** con `Volume/Details` en los 23
strikes (una sola diferencia de un contrato). El que estaba viejo era el número
de `Settlements`, y por lo tanto el muro correcto es el de ATAS.

En 7750 el `change` del call era **−559**: 2456 de `Settlements` menos 559 da
los 1897 que reportan tanto `Volume/Details` como Rithmic. Ese solo strike era
el que daba vuelta cuál muro ganaba.

## Dos trampas más de esa misma respuesta

**`monthData` no son meses, son mes + tipo.** Vienen `SEP 26 Calls`,
`SEP 26 Puts`, `OCT 26 Calls`, `OCT 26 Puts`. El tipo sale del *label*, no de un
campo en `strikeData`. Juntarlos todos en un diccionario por strike hace que
octubre pise a septiembre y que todo quede clasificado como put.

**`lastTradeDate` del índice maestro no es el vencimiento**, es la última fecha
con datos de settlement — ayer, para todo contrato vivo. Filtrar por «fecha
futura» deja cero resultados y parece que no hay nada vivo.

**Why:** se estuvo a punto de cambiar la fuente de interés abierto del proyecto
—y de decirle al operador que su plataforma paga le mentía— por comparar contra
un dato de un día antes.

**How to apply:** para interés abierto de CME usar `Volume/Details` y su
`atClose`. `Settlements` sirve para el `settle` por strike (que es lo que
despeja la IV), pero su OI está un día atrás. Ver [[cme-quikstrike]] y
[[cadena-es-en-vivo-rithmic]].
