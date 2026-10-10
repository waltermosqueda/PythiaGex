---
name: atas-orden-grupos-ajustes
description: "Como ordena ATAS 8.0.15 los grupos del panel de ajustes de un indicador (medido 09-10): por el MENOR Display.Order de sus casillas; los Order NEGATIVOS van al final; el nombre del grupo no importa"
metadata:
  node_type: memory
  type: reference
  originSessionId: f5879819-e2a4-405b-87b0-959ace090d7b
  modified: 2026-10-09T20:31:22.092Z
---

Medido en el dialogo de ajustes de la 4.1 (09-10-2026 17:2x ART):
- Los grupos (GroupName) aparecen ordenados por el **menor `Display.Order`** de sus propiedades. Con "9.7" (Order 1) y "9.9" (Order 1),
  "9.7" salia primero; al sacarle las casillas a "9.7", paso a salir "9.9".
- Un **Order negativo NO va primero: va al final** (el grupo con Order −900..−831 aparecio despues de los de Order 1 y 10).
- El nombre del grupo ("0. ...") no decide nada.
- Para dejar un grupo arriba de todo: sus casillas con Order 0..N y TODOS los demas `[Display]` de la clase corridos (+1000). Eso hace
  `herramientas/grupo_arriba_415c.py` (idempotente).
- El cambio de una casilla bool se aplica EN VIVO, sin Apply, y se ve en menos de 1 s mientras llegan ticks. Apply solo lo guarda.
  El buscador del panel filtra por nombre de casilla y muestra el grupo de cada una. La columna de nombres muestra unos 13 caracteres.
- Si el dialogo de ajustes queda abierto, `instalar_4_0.ps1` no encuentra "Save and close" y mata ATAS a los 60 s sin guardar el
  workspace: cerrar el dialogo (Apply por UIA lo cierra) antes de reinstalar.

Ver [[atas-dialogo-indicadores-clics]], [[pythiagex-4-0-familia]].
