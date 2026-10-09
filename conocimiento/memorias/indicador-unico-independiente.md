---
name: indicador-unico-independiente
description: "El operador quiere UN solo indicador independiente; toda dependencia (otro indicador, un generador Python) se dice ANTES de construir"
metadata:
  node_type: memory
  type: feedback
  originSessionId: f5879819-e2a4-405b-87b0-959ace090d7b
  modified: 2026-10-08T22:13:14.811Z
---

08-10-2026: la 4.0 "visor" (lee json de la vista previa, que a su vez depende de la 3.0) dejo de dibujar cuando el operador cerro la pestaña de
la 3.0. Su reaccion: "debe ser independiente siempre… quiero uno solo, o modificas la 3.0 o creas el 4.0 nuevo independiente; se profesional;
nunca me dijiste que dependia de otro indicador".

**Why:** construir rapido un visor y avisar de la dependencia DESPUES rompio la confianza; para el, un indicador que depende de otro no es
profesional. Ademas el cierra/reordena pestañas y no sabe que hay cadenas ocultas de datos.

**How to apply:** antes de construir cualquier cosa, decir en el mismo mensaje de que depende (otro indicador, un proceso Python, un archivo)
y ofrecer la version autocontenida. Un solo indicador por funcion. Tampoco acepta programas externos (19:2x: "que este todo dentro del indicador"): ni cboe_local. Lo de CME sale de ATAS/Rithmic; NDX/QQQ/TQQQ
(OPRA/CBOE, que Rithmic no transmite) los baja el propio indicador con gzip y sin rafagas. Ver [[pythiagex-4-0-familia]].
