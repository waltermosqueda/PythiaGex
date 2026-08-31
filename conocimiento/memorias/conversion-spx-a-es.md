---
name: conversion-spx-a-es
description: "Cómo pasar niveles de GEX de SPX a ES sin errar: la base se mide restando dos forwards de la misma cadena, nunca contra el índice."
metadata: 
  node_type: memory
  type: reference
  originSessionId: 46e731fd-f0ba-466c-bd15-979f9343516d
  modified: 2026-08-31T04:05:00.000Z
---

Todos los tableros de GEX publican niveles en **SPX**; él opera **ES**. Dibujar un nivel de SPX directo en ES lo deja corrido decenas de puntos.

## El método bueno: forward contra forward

```
forward(V) = Strike + Call(V) − Put(V)
base       = forward(vencimiento trimestral) − forward(vencimiento más cercano)
ES         = SPX + base
```

**Nunca `forward − spot`.** El índice al contado deja de cotizar 16:15 ET pero las opciones siguen; fuera de horario `spot` es el cierre anterior y restarlo mezcla dos momentos. Los dos forwards salen de la misma cadena en el mismo instante, así que el atraso se cancela solo.

**Cómo se detectó** (2026-08-31, 02:37 ET): el forward de 0DTE daba 7679,30 con `current_price` en 7711,76. Un forward de cero días **es** el contado por definición, así que esos 32 puntos no eran base: era el índice atrasado. La curva de forwards subía +10,8 en 18 días, o sea carry limpio.

## El control que decide si creerle

La base medida se compara contra el **carry teórico** = `contado × (tasa − dividendo) × días/365`. Con tasa 4,0 % y dividendo 1,3 % (0,6 % para NDX). El 2026-08-31 SPX dio **+10,83 medido contra +10,23 teórico**. Si no coinciden dentro de ~3 puntos, la medición no sirve.

Se marca firme solo con las tres: 12+ strikes por vencimiento, dispersión < 0,05 % del índice (relativa: 3 puntos en SPX es exigente, en NDX es absurdo), y base cerca del carry.

**NDX y RUT no pasan el control.** Sus opciones casi no cotizan de noche — NDX con 6 strikes y 8,65 de dispersión, RUT con 5,45. Un nivel de NQ o RTY convertido con esa base no se dibuja.

## Regalo del método: el contado implícito

El forward del vencimiento más cercano **es** dónde está el índice ahora, aunque la pizarra siga clavada en el cierre. Ningún tablero público lo muestra.

## Trampa con niveles en SPY

SPY × 10 está mal. El ratio real SPX/SPY era 10,0223 — sobre un strike de 775 son 17 puntos de error, más la base: casi 40 puntos de desvío.

**Why:** en scalping de ES un desvío de 20–40 puntos convierte un nivel válido en una pérdida sistemática, y es un error silencioso: los niveles "casi funcionan", que es peor que no funcionar. El método viejo (`forward − spot`) daba −21,6 cuando la respuesta era +10,8: 32 puntos, 128 ticks.

**How to apply:** ya está implementado en `PythiaGex/pythiagex/base.py` y el panel lo muestra con su bandera de confianza. Cada nivel que salga de [[paginas-gex-auditadas]] pasa por esta conversión antes de dibujarse en ATAS. Ver [[calcular-gex-propio]] y [[setup-atas-verificado]].
