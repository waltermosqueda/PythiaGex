---
name: cambios-atas-de-a-uno
description: "Configurar indicadores de ATAS de a uno y verificando entre cada paso; nunca cargar historico pesado con el mercado abierto."
metadata:
  node_type: memory
  type: feedback
  modified: 2026-08-24T19:15:00.000Z
---

El 2026-08-24, con el mercado abierto, agregué dos indicadores en la misma tanda —un VWAP semanal y
un segundo Cluster Search— y **rompí el gráfico**. Tuvo que reiniciar ATAS en plena sesión.

## Las dos trampas concretas

**`Days look back` nunca arriba de 5** en un gráfico de volumen intradía. Quedó en 20 y, con período
semanal, ATAS cargó veinte días de ticks: perfil de volumen inflado a 54 millones, Smart Tape con
90 s de retraso, el diálogo de indicadores devolviendo `Added (0)` con los indicadores igual
dibujándose, y `Manage instruments` sin poder cerrarse.

**El `Delta filter` de Cluster Search es un piso, no un techo.** Poner −150 para capturar el lado
vendedor hace que califique casi toda celda con delta mayor a −150 → el fondo entero pintado.
Para el lado vendedor hay que probar `Bid Ask Imbalance,%` o `Calculation Mode` en Delta.

## La regla de proceso

- **Un indicador por vez, mirando el gráfico entre cada uno.** Metí los dos juntos y tardé varias
  vueltas en separar cuál causaba qué.
- **Configuración pesada solo con el mercado cerrado.** Los ajustes finos van antes de la apertura
  o después del cierre, nunca mientras opera.
- **Antes de sugerir reiniciar, verificar que no tenga posición abierta ni órdenes vivas.**

**Why:** el costo no fue el error en sí, fue que perdió minutos de sesión en vivo por algo que podía
haberse hecho a las 18:00 sin apuro.

**How to apply:** ver [[navegar-atas-sin-pedir-permiso]] (navegar sí, reconfigurar en vivo no) y
[[desplegables-atas-alt-flecha]].
