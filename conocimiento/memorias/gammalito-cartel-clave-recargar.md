---
name: la referencia-cartel-clave-recargar
description: "13-11: el cartel 'Pon tu usuario y contraseña' de la referencia al agregar una instancia nueva es un error CONOCIDO del indicador (no cierra la sesion): se arregla con un clic en una temporalidad del grafico (recarga); las claves NUNCA las escribo yo"
metadata:
  node_type: memory
  type: feedback
  originSessionId: f3419dc9-e1cb-4b1b-99aa-8707c1a3c2f4
  modified: 2026-11-14T01:12:02.727Z
---

13-11 ~22:05: al agregar una instancia nueva de la referencia (pestaña nueva MES 5m), esa instancia y la del MNQ 1m del
operador mostraron "Pon tu usuario y contraseña en los ajustes del indicador". Yo lo lei como "se cerro la sesion
compartida" y frene todo. El operador: "es un error conocido del indicador, pero esta funcionando; dale a refrescar o
recargar, click a alguna temporalidad". Un clic en el boton M5 de la barra del grafico recargo y las dos instancias
volvieron con datos (SPX 0DTE spot 7561; MNQ Full spot 27923.75) sin tocar credenciales.

**Why:** perdi tiempo y lo alarme por un cartel que no era un corte de sesion.
**How to apply:** despues de agregar una instancia de la referencia (o si aparece el cartel), clic en la temporalidad
activa del grafico y esperar ~8 s antes de concluir nada. Las credenciales de la referencia (o de cualquier servicio de
terceros) no las escribo yo aunque me las pase: se las pido a el; no guardarlas en archivos ni memoria. Ver
[[clics-que-no-llegan-y-loops]], [[rondas-12-14-resultados]].
