---
name: pepperstone-copytrading-metodo
description: "20-11-2026: como auditar el ranking de copy trading de Pepperstone (pepperstone.copy-trade.io) desde el panel de Claude; Edge es solo lectura; endpoints crudos y trampas medidas"
metadata:
  node_type: memory
  type: reference
  originSessionId: 7ea91ac0-f68f-4e84-933e-3c8ebb778525
  modified: 2026-11-20T05:53:27.711Z
---

Pedido 20-11-2026: rankear a los candidatos del Discover de copy trading de Pepperstone sin caer en porcentajes engañosos.

**Por donde entrar:** la web real es `https://pepperstone.copy-trade.io/discover` (Blazor WebAssembly, login por identity.copy-trade.io).
En el panel de Claude carga con la sesion del operador ya iniciada (no hubo bloqueo: lo que fallo fue una URL inventada por mi).
En Edge (o la app instalada "Discover") computer-use es SOLO LECTURA: se puede leer el texto por UIA, no navegar. No insistir por ahi.

**Dato crudo (lo pide la propia pagina):**
- `/api/discover?...` (top 5 de cada lista) y `/api/discover/Strategies?wl=&&server=` (las 2.243, valor = retorno desde el inicio en fraccion).
- `/api/strategies/{id}` (Fee, NumCopiers, IsEnabled), `/api/strategies/{id}/stats?...` (History = curva de saldo realizado; el ultimo punto es el vivo),
  `/api/strategies/{id}/signals/open` y `/signals/closed/?startDate&endDate` (ultimos 30 dias).
- Un `fetch` propio da 400; lo que anda es envolver `window.fetch` para copiar las respuestas que pide la pagina y navegar con `Blazor.navigateTo`.
- `Blazor.navigateTo` entre dos rutas del mismo componente (/strategy/A -> /strategy/B, o entre categorias de /discover) NO recarga datos: pasar por otra ruta en el medio.
- `javascript_tool` corta a los 45 s: una estrategia por llamada (~10 s), 10 por batch. 67 estrategias = ~12 min.

**Trampas medidas ese dia (repetir el chequeo, no citar de memoria):**
- El % del cartel es "desde el inicio": The Holy Grail 15.434 % con x13,5 en el primer mes; ultimos 12 meses +63 %.
- "Max Drawdown (Balance)" ignora la perdida abierta: Grid King mostraba -7,39 % y "Risk: Lower" con equity 297 de 3.691 USD.
- 17 de los primeros 40 estaban deshabilitadas, sin datos o en saldo cero.
- Los proveedores operan en otros brokers (Axi, IC Markets, Libertex, TitanFX): solo 10 de 48 en servidores de Pepperstone.
- `Status.Balance` no siempre es confiable (auro/Accurate): medir la perdida abierta como (1+UnrealisedReturn)/(1+RealisedReturn)-1.

**Cuadro "Copy Strategy" (leido en pantalla 20-11, con el operador abriendolo):** Trade Type = Fixed (pide Trade Size) /
Mirror size / Mirror risk by equity / Mirror risk by balance (piden Proportion: 0,1 a 2 de a decimas, luego 3 a 10); dos si/no:
"Copy existing open trades?" y "Round up trades to minimum size?". NO hay campo de monto por estrategia: el tamaño mira la cuenta entera.
Formula (ayuda de Pelican, la plataforma detras): lote = saldo copiador / saldo maestro x lote maestro x proporcion; si da menos de 0,01 abre 0,01 igual.
Con 2.000 USD eso vuelve indivisibles a los maestros de 0,01 sobre cuentas >= 2.000 (AIT FX, Big Ben). Soft/Hard Stop se configuran DESPUES de iniciar la copia.
NUNCA tocar el boton Copy (submit) del cuadro: inicia una copia con plata real; al terminar dejar Trade Type en "Please select", cerrar con la X y
verificar Copies > Open vacio. Un recargo de pagina borra `window.__cx` (los datos crudos en memoria): exportar antes.

**How to apply:** si pide repetir o ampliar el ranking, ir directo al panel con esta receta; ordenar por ventanas recientes (1/3/6/12/24 meses),
meses positivos, perdida abierta de hoy, antiguedad de las abiertas y si agranda el tamaño. No es consejo de inversion: entregar criterios y datos.
Ver [[comprobacion-fehaciente-en-pantalla]], [[panel-navegador-no-frontear]].
