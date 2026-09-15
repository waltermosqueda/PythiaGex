---
name: traspaso-2026-09-15-capas-nq
description: "Pedido del 15-09: en un grafico de NQ/MNQ ver a la vez barras y dominantes de QQQ, TQQQ, NDX y Rithmic, cada una de un color y con llave. Fable dejo la arquitectura escrita (capas aditivas, primaria intacta) en PythiaGex/conocimiento/traspasos/2026-09-15-capas-nq.md; nada de codigo tocado. Trampa central: TQQQ es 3x, el mapeo por razon simple esta mal."
metadata: 
  node_type: memory
  type: project
  originSessionId: 961a521e-545b-452c-9eed-32904fb03eae
  modified: 2026-09-15T16:41:00.885Z
---

El 2026-09-15, con 7 % de cuota, el operador pidio capas simultaneas de QQQ + TQQQ + NDX + Rithmic
en un solo grafico de NQ/MNQ (colores distintos, prender/apagar cada una, todo auditado). No se
escribio codigo: se dejo el diseño completo en
`PythiaGex/conocimiento/traspasos/2026-09-15-capas-nq.md` (sin commitear; el push lo hace el operador).

Lo decidido ahi, para no relitigar:
- Capas ADITIVAS con una clase nueva `CapaLibro` (cadena + nucleo + lectura + razon por capa); la
  fuente primaria (`Libro`) y todo lo que cuelga de ella (centinela, gatillos, AUDIT, archivo) no se toca.
- Todas apagadas por defecto: el DLL nuevo no cambia los tres graficos en produccion.
- TQQQ: `Fut(K) = F_alineado x (1 + (K/S_tqqq - 1)/3)`; la razon simple corre los strikes lejanos.
  Magnitudes entre capas NO comparables (factor x3/x9 supuesto): cada capa normalizada a su maximo,
  nunca sumadas.
- TQQQ no se archiva todavia: agregar `"TQQQ": "TQQQ"` en archivar_cadena.py linea 126 y en cadenas.yml linea 47.
- Rithmic: reusar `_viva`, nunca una segunda suscripcion.
- Auditar con un `laboratorio/capas_nq.py` independiente contra el AUDIT por capa antes de decir que anda.

**Why:** el operador pidio explicitamente que Opus pueda seguir "sin romper ni hacer cosas ilogicas";
el riesgo real es tocar la primaria o mapear TQQQ como si fuera lineal.

**How to apply:** empezar por leer el traspaso y seguir su orden de trabajo (archivador -> nucleo ->
capas sin dibujo -> auditoria -> dibujo). Ver [[gamma-hoy-1-9-2026-09-14]],
[[referencia-formulas-nq-medidas]], [[dos-libros-distintos]], [[nq-rithmic-pierde-0dte]].
