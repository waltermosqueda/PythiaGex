---
name: no-mover-mouse-si-opera
description: "Si el operador tiene posicion u ordenes en ATAS (panel de trading abierto), los controles automaticos NO mueven el mouse, no clickean, no reinician ni instalan: solo capturas pasivas"
metadata:
  node_type: memory
  type: feedback
  originSessionId: 404dab2f-b5f6-48fa-ba08-3ab764cbbdd8
  modified: 2026-10-28T02:03:10.216Z
---

27-10 23:02 ART: en un control automatico de cada 15 min moví el mouse sobre el MES 1m (para sacar el renglon de rebobinado) mientras el
operador tenia una posicion de 4 lotes y tres BUY STP abiertas con el Trading Panel desplegado.

**Why:** mover el cursor mientras el operador opera puede hacer que su clic caiga en otro lado (BUY/SELL MKT del panel); un reinicio de ATAS con
ordenes vivas corta su gestion (BE/trailing corren en la PC: ver [[salidas-atas-tp-sl-trailing]]).
**How to apply:** en controles y loops, antes de tocar nada mirar la captura: si hay posicion, ordenes o panel de trading visibles, solo
screenshot/zoom pasivos; arreglos quedan compilados y se instalan cuando no haya posicion (avisando). El renglon naranja de rebobinado con su
mouse encima es normal: no "arreglarlo". Ver [[no-reiniciar-atas-con-posicion]], [[pythiagex-5-0-gamma-es]].
