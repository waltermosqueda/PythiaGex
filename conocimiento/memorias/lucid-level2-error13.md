---
name: lucid-level2-error13
description: "25-09: heatmap 'Waiting for Level 2 data' + log 'get_order_book error : 13' tras renovar la cuenta Lucid = Lucid no reactivo el market depth del login Rithmic; lo arreglo el soporte HUMANO (<agente de soporte>) reactivandolo; el bot de Lucid insistia en que era ATAS."
metadata:
  node_type: memory
  type: reference
  originSessionId: 6b882b35-31bc-4131-8cd4-4b099f670c25
  modified: 2026-09-25T07:32:07.654Z
---

Caso del 24/25-09-2026: la cuenta Lucid (prop firm, Rithmic) caduco a las 23:57 del 24-09; el operador compro otra y el
heatmap de ATAS quedo en "Waiting for Level 2 data (DOM events)..." con el Level 1 y el trading andando.

- **Firma en el log de ATAS** (`%APPDATA%\ATAS\Logs\app_<fecha>.log`): cada suscripcion "Prints, Best, Quotes, Summary"
  devuelve `get_order_book error : 13` (252 veces en el dia, cero el 22 y el 23-09). El login, el servidor
  ("LucidTrading. Chicago Area" o "Sao Paolo") y "Aggregated: False" estaban bien.
- **No era ATAS ni nada nuestro**: reinicio completo, 8 reconexiones, con y sin agregacion: mismo error. Los acuerdos de
  datos de Rithmic estaban vigentes (29-07-2026). Era el permiso de market depth del login, que Lucid da de baja al
  vencer la cuenta y no reactiva solo con la cuenta nueva.
- **Solucion**: el soporte humano de Lucid <agente de soporte> lo reactivo; logout/login y a las 04:22 del 25-09: 0 errores.
  El bot de IA de Lucid repetia que el problema era la plataforma (reiniciar ATAS, agreements, R Gateway): no sirvio.

**Why:** se perdio una noche probando cosas en ATAS por un permiso del lado del broker.
**How to apply:** si vuelve "Waiting for Level 2" con `get_order_book error : 13`, contarlos por dia en el log; si
empezo con un vencimiento o renovacion de cuenta, pedir de entrada al soporte HUMANO de Lucid que reactive el market
depth del login LT-..., sin tocar la configuracion de ATAS. Ver [[atas-lento-cortes-rithmic]].
