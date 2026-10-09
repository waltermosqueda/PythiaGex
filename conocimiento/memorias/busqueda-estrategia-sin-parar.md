---
name: busqueda-estrategia-sin-parar
description: "Orden del operador (21-09): seguir buscando una estrategia/indicador con ventaja real sobre el azar combinando DOM, heatmap, footprint, VWAP anclado y rayas de gamma, sin escatimar recursos, hasta lograrlo; con protocolo anti-sobreajuste para no fabricar un ganador."
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 6b882b35-31bc-4131-8cd4-4b099f670c25
  modified: 2026-09-22T05:19:05.552Z
---

Dicho por el operador el 21-09 (varias veces, cada vez mas firme): "no pares hasta encontrar una buena
estrategia rentable", "tokens ilimitados, sin escatimar razonamiento, computo, backtest, investigacion",
"lo unico prohibido es detenerte o fracasar". Quiere situaciones y numeros concretos para mirar en SU pantalla
(grafico + heatmap + DOM + footprint + VWAP anclado + rayas de gamma), noche y rueda por separado, con
chances mejores que el azar; el decide si entra y maneja el riesgo. No pide algo infalible.

**Correccion de alcance (22-09):** "enfocate en lo que te pedi, nada de investigaciones por fuera de lo que
utilizamos". SOLO sus herramientas de ATAS: grafico, perfil de volumen, footprint, VWAP/VWAP anclado, rayas de gamma,
heatmap y DOM. Frene la ronda 5 (anomalias de calendario/papers externos) por eso. No volver a desviarse.

**Critica del operador (22-09):** "descartas todo muy rapido; lo prometedor hay que juntarlo, equilibrarlo, agregar
variables y evolucionar". Respuesta adoptada: ronda 7 evolutiva (`laboratorio\dom\ronda7`): biblioteca causal de
todas las piezas prometedoras + genetico / ensamble ponderado / selector de regimen, con aptitud en el PEOR
sub-periodo, penalizacion por complejidad y Deflated Sharpe; solo 5 finalistas abren los tramos sellados. Combinar si,
pero el juez sellado no se negocia.

**Pedido del 22-09 (madrugada):** revisar los ~294 indicadores de ATAS como piezas de la cria, y NO descartar sus
indicadores propios: PythiaVWAP anclado (probar anclajes sesion, rueda y MES calendario) y Gamma Hoy ORIGINAL (la de
produccion; para el es mas estable que la 2.0). Su hipotesis: "aunque sea 1 a 3 aciertos por sesion seria
grandioso". Gamma Hoy solo tiene historia desde ~04-09: estudio preliminar + grabacion hacia adelante. Las DLL de
terceros del marketplace no se descompilan (licencia).

**Why:** tiene mucha informacion propia (cinta orden por orden de 34+ ruedas con la punta antes/despues, libro
vivo, niveles gamma, VWAP anclado) y siente que no se esta aprovechando.

**How to apply:**
- Seguir con rondas sucesivas (workflow estrategia-dom-medida, laboratorio\dom) hasta que algo aguante, sin
  cortar por cansancio ni por costo.
- NUNCA fabricar el ganador: pre-registro de cada regla, confirmar en muestra no usada, exigir mas (t >= 3 o
  correccion por cantidad de reglas probadas) a medida que se prueban mas, tercer tramo intacto (reserva de 14
  ruedas antes del 20-08 y ruedas nuevas desde el 18-09), y prueba en vivo antes de llamarlo estrategia.
- Si una ronda no deja nada, decirlo con los numeros y lanzar la siguiente con datos nuevos y otras familias
  (lead-lag NQ/ES->MNQ, regimen de gamma, horario, ML walk-forward como el MODELO 1.7b de ES a 10 min).
- Lo que sobreviva se construye en el clon PythiaGex 2.0 (prod no se toca) y se le enseña con capturas reales.
- Sigue valiendo CLAUDE.md: no prometer rentabilidad, no pagar suscripciones antes de las 15 sesiones.
Ver [[busqueda-gatillo-2026-09-17]], [[gatillo-cientifico-2026-09-10]], [[libro-dom-mnq-armado]], [[salidas-atas-tp-sl-trailing]].
