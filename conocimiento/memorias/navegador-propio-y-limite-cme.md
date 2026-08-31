---
name: navegador-propio-y-limite-cme
description: "Usar siempre el navegador integrado antes que Edge, y una sola descarga de CME por día: dos deslogearon al usuario por anti-bot."
metadata:
  node_type: memory
  type: feedback
  modified: 2026-08-26T18:15:00.000Z
---

## La regla del navegador

**El navegador integrado (`mcp__Claude_Browser__*`) va siempre primero.** Ahí se ejecuta
JavaScript, se lee el DOM, se guarda cache — control total.

**Edge sirve para una sola cosa: capturas de pantalla**, cuando el panel integrado no está
desplegado y `screenshot` da timeout por no componer frames. Se pide con `request_access` y se
concede en tier `read`: se ve y se hace zoom, **no se scrollea ni se clickea**.

Nunca abrir algo en Edge que se pueda resolver adentro.

## El límite de CME — lo que pasó el 2026-08-26

**Akamai Bot Manager deslogueó al usuario de todas sus sesiones.** Causa: **dos descargas completas
en el mismo día** (23 vencimientos cada una) más llamadas sueltas — más de 50 pedidos en pocas horas.

**Aclaración importante:** el usuario atribuyó el bloqueo a Edge. **No fue Edge.** Todas las
llamadas salieron del navegador integrado; Edge solo capturó pantalla. **Lo que dispara el bloqueo
es la frecuencia, no la herramienta.** Cambiar de navegador no resuelve nada.

**El error concreto:** al cerrarse el panel se perdió el cache de `localStorage` y volví a bajar
todo en vez de aguantar con lo que tenía. El panel se cerró solo tres veces ese día.

## Lo que quedó arreglado en `herramientas/mapa_gamma_cme.js`

1. **Guarda antibloqueo al inicio:** si existe `gexES_YYYYMMDD`, usa el cache y **no pide nada**.
2. **Espaciado de 2 segundos** entre pedidos (era 800 ms).
3. **Cache persistido al final** de cada corrida.

**Si se pierde el cache, NO volver a bajar el mismo día.** Recalcular con lo que haya o esperar
al día siguiente. Un mapa de hace horas sirve; quedar bloqueado, no.

**Why:** le costó el deslogueo de todas sus cuentas en medio de la jornada. Ver
[[cme-quikstrike]], [[rutas-y-apis-gex]] y [[panel-navegador-no-frontear]].
