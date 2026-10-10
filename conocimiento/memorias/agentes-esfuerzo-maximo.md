---
name: agentes-esfuerzo-maximo
description: "Regla del operador (09-10): esfuerzo MAXIMO en cualquier tarea, simple o compleja; todo agente (constructor, revisor, verificador, investigador) con effort 'max'"
metadata:
  node_type: memory
  type: feedback
  originSessionId: f5879819-e2a4-405b-87b0-959ace090d7b
  modified: 2026-10-09T19:26:15.504Z
---

Textual (09-10-2026 ~16:25 ART): "de acuerdo, implementalo por defecto siempre, poder esfuerzo al máximo sin limitarse", respondiendo a
mi propuesta de poner constructores y revisores con el esfuerzo al máximo. Y enseguida: "quiero siempre en cualquier tarea sea simple
o compleja esfuerzo poder al máximo".

**Why:** prefiere menos errores e idas y vueltas aunque cada agente tarde unos minutos más; antes se perdieron horas por agentes
que se equivocaban.

**How to apply:** en todo Workflow, `agent(..., { effort: 'max' })` para TODOS los agentes (también los "mecánicos": nada en 'low');
en la herramienta Agent, `effort: "max"`. Yo mismo: verificar y auditar a fondo aunque la tarea parezca simple. El esfuerzo de la
sesión principal no lo puedo cambiar yo (la app rechaza que una sesión se suba el esfuerzo sola; el 09-10 estaba en 'xhigh'): se lo
cambia él desde el selector de modelo/esfuerzo de la app. Igual se respetan los cuellos reales (orden obligatorio, archivos
compartidos, carga de la PC: ver [[avisar-pc-al-limite]]).
