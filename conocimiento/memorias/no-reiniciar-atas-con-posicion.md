---
name: no-reiniciar-atas-con-posicion
description: REGLA NUEVA (2026-09-10 noche) - reiniciar ATAS siempre que haga falta, sin pedir permiso; la regla anterior de esperar su OK por posiciones abiertas quedo anulada por el operador
metadata:
  type: feedback
---

El 2026-09-08 habia quedado la regla de no reiniciar ATAS con posicion abierta sin su OK. El
2026-09-10 a la noche el operador la anulo de forma explicita: "regla: siempre reinicia, nunca
esperes mi permiso". Sus ordenes viven en Rithmic y el prefiere el indicador al dia antes que la
pantalla quieta.

**Why:** perdia tiempo esperando una confirmacion que el no quiere dar; un DLL instalado que no
carga es un arreglo que no existe.

**How to apply:** compilar, instalar y reiniciar con herramientas/reiniciar_atas.ps1 de corrido,
avisando en el mensaje que se reinicio. Despues del reinicio, SIEMPRE mirar la pantalla (ver
[[mirar-pantalla-antes-de-responder-atas]]). Ver [[verificar-yo-no-el-usuario]].
