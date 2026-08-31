---
name: atas-opciones-es
description: "ATAS sí tiene la cadena de opciones de ES con vencimientos diarios, 0DTE, open interest, gamma y volumen — corrige lo que decía la memoria vieja."
metadata: 
  node_type: memory
  type: project
  originSessionId: 46e731fd-f0ba-466c-bd15-979f9343516d
  modified: 2026-08-20T01:48:13.836Z
---

Verificado en pantalla el 2026-08-19 (22:45 AR). **La memoria anterior estaba equivocada** al decir que el tablero de opciones solo traía vencimientos trimestrales: eso era solo el filtro por defecto.

## Cómo llegar
Home → **Options Board β** → elegir instrumento **ES** → poner **Series Type = "All Types"** (viene en "Regular", que es lo que ocultaba todo). Ahí aparecen los vencimientos **día por día**: 19 Ago Wednesday, 20 Ago Thursday, 21 Ago Friday, 24/25/26/27/28 Ago, 31 Ago EOM, y todo septiembre. **Incluye 0DTE.**

Las columnas **Open Interest, Gamma y Volume vienen apagadas**. Se prenden con el ícono de ajustes a la derecha del campo "Strikes". El menú ofrece: Volume, Open Interest, Last, Delta, Gamma, Theta, Vega, Bid, Bid IV, Ask, Ask IV, IV.

Ojo: si la ventana está pegada al borde derecho, ese menú se abre fuera de pantalla. Hay que mover la ventana a la izquierda primero.

## Qué trae (ES 20-ago-2026, 1DTE, con ES en 7741)
Gammas de 0,0076 a 0,0109 e **IV entre 6 % y 8,4 % — valores sanos**, nada de los solvers rotos de QuantWheel. Mayor OI de put en **7740 (1.104 contratos)**, después 7725 (842), 7730 (654), 7720 (634), 7715 (588). Mayor volumen en 7735 (312) y 7740 (261).

Un strike de put en 7740 da del orden de **USD 344 M de gamma** (Γ × OI × 50 × S² × 0,01). Comparable en magnitud a los muros de SPX del mismo vencimiento — o sea, **no es despreciable**.

## Lo que esto sí resuelve y lo que no

**Resuelve algo que ninguna de las cinco webs puede:** todas construyen el GEX sobre el interés abierto de **ayer**, porque la OCC consolida de noche. ATAS muestra la columna **Volume en vivo** — el flujo de hoy, mientras pasa. Ese es el punto ciego de todos los tableros y acá se tapa.

Además: sin caché de 30 minutos, sin banderas que mientan, sin login, sin límite de tasa. Feed Rithmic en tiempo real.

**No lo reemplaza:** la gamma que mueve al ES vive mayormente en las opciones de **SPX**, que son las que cubren los dealers vendiendo y comprando futuros. La cadena de ES es una porción real pero menor. Es **contraste, no sustituto**.

**Límites reales:** no hay vista que agregue la gamma de toda la cadena — hay que sumar a mano o exportar. El rango de strikes por defecto es 5 y el selector sube de a uno. Con la ventana angosta, la columna de OI de calls queda cortada.

**Why:** él tiene Ultra vitalicia, así que este dato ya está pago y es mejor en frescura que cualquier web gratuita.

**How to apply:** usar ATAS para el **flujo de hoy** (volumen por strike) y para confirmar niveles; usar el cálculo sobre SPX para el **régimen y el gamma flip**. Si un strike aparece cargado en las dos fuentes, ese nivel pesa doble. Ver [[calcular-gex-propio]], [[paginas-gex-auditadas]] y [[setup-atas-verificado]].
