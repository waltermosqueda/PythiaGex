---
name: ronda-18-senal-ola-y-indicador-aparte
description: "27-11-2026: la señal de entrada/salida de la ola (R1 a favor de la marea con agresores) empata antes de costos (0,00/op, 808 ops, 34 % ganadoras, +7,84/-4,07) y pierde 0,50/op con costo; 8 variantes, ninguna pasa. Decisión del operador: la ola va en un INDICADOR APARTE, no en el Superindicador"
metadata:
  node_type: memory
  type: project
  originSessionId: 233bf8d4-5113-4e95-a0d9-68bff726690d
  modified: 2026-11-27T05:27:43.829Z
---

**Pedido (27-11 ~02:30 AR):** "una señal o figura que indique el mejor momento para entrar y salir, bien calculado, si no me
mareo". Se pre-registró y midió: `PythiaGex/laboratorio/ronda18_senal_ola/` (PREREGISTRO, RESULTADOS, r18_01_senal.py).
- R1 (compra cuando la ola vuelve a favor de una marea CON agresores; stop y salida = la raya de la ola): 808 ops en 17 ruedas,
  34,2 % ganadoras, +7,84 / −4,07, bruto 0,00 por op, neto −0,50 con 2 ticks (2/17 ruedas +). R4 (contra la marea) −0,54 bruto:
  ir a favor es medio punto mejor, no alcanza. V1-V4 (marea pura, primer retroceso, retroceso sin agresores, la mañana): ninguna
  pasa (V4 casi empata en MES −0,06 y no confirma en ES −0,62).
- Mecánica: entra X después del giro y sale X antes del extremo; gana solo si la ola mide > 2X; mediana 1,68X → ruina del jugador.
- Está en la demo (show_widget) como "señal en prueba" con sus números; NO presentarla como estrategia.

**Decisión del operador (27-11 ~03:00 AR):** "mejor construir un indicador aparte, no lo mezcles con el Superindicador (ese es
delta, footprint y volumen; este es más general con más features)". Plan: indicador nuevo autocontenido (ver
[[indicador-unico-independiente]]) con el motor de la ola (SuperCalculos: Ola/OlaFotos/EscalaOla/RangosRth + SuperOlaNucleo)
copiado adentro; el Superindicador vuelve a 1.9 (con la sonda). En ATAS quedó instalado el 2.0a (la ola solo como datos, sin
dibujo, verificado en vivo: 52.410 órdenes de la jornada en 2,7 s, costura sin hueco, X 10,00 auto) hasta mudar.
Agregar el indicador nuevo al gráfico es un clic suyo (Indicators → Add) o UIA cuando no esté en la PC ([[foco-atas-atajos-trading]]).

**How to apply:** capas que le resultaron más claras: el FONDO DE MAREA (colores). Defaults a confirmar con él; señal "en prueba"
con registro en vivo para la bitácora. Ver [[ronda-17-anatomia-del-movimiento]], [[superindicador-velas-delta-perfil]].
