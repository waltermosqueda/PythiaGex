---
name: autonomia-por-sesion
description: "Qué funciona solo las 24 horas y qué no: la cadena de CBOE se congela de noche y la bitácora de ATAS solo escribe con ATAS abierto."
metadata: 
  node_type: memory
  type: project
  originSessionId: a021c566-b4fe-42c5-acd1-0a01a165a079
  modified: 2026-08-31T17:15:01.304Z
---

Medido el **2026-08-31** sobre el histórico real, no supuesto.

## La cadena de CBOE se congela de noche

En la rueda del lunes 31 de agosto, foto por foto:

- **00:00 a 04:00 UTC** (sesión asiática): el spot quedó clavado en 7711.76 y el timestamp de la cadena no avanzó ni una vez.
- **05:30 UTC**: la cadena tenía **173 minutos de atraso**, casi tres horas.
- **09:57 UTC en adelante**: refresca cada ~1 minuto, sin fallar, todo el día.

O sea: **el mapa despierta alrededor de las 09:00–10:00 UTC** (mañana de Londres) y antes de eso muestra los niveles del cierre americano anterior. No es un bug nuestro: es la API de cotizaciones de CBOE, que no publica durante el horario global de opciones.

Eso **no invalida los niveles**: el interés abierto es de ayer igual y no cambia de noche. Lo que envejece es la volatilidad implícita y el posicionamiento nuevo. El indicador ya lo dice bien graduado: entre 1 y 20 horas de atraso avisa *"sirve para ubicar niveles, no para cronometrar la entrada"*, y arriba de 20 horas *"NO OPERES CON ESTO"*.

## Qué es autónomo y qué no

- **El panel y el workflow: autónomos.** Corren solos cada 15 o 30 minutos cubriendo las 24 horas de lunes a viernes, más la reapertura de Globex del domingo (22:00 UTC en verano, 23:00 en invierno; el cron cubre las dos).
- **El indicador: autónomo mientras ATAS esté abierto.** Baja el JSON solo, recalcula la probabilidad con el precio vivo y dibuja. No depende de ninguna sesión.
- **La bitácora de contexto: SOLO escribe con ATAS abierto y el indicador puesto en un gráfico.** Si querés que el centinela mida las ruedas de Asia o Londres, ATAS tiene que quedar corriendo esas horas. Es la única pieza que necesita la máquina prendida.

## El artefacto que casi se cuela

El centinela llegó a reportar tres hallazgos "concluyentes": que los muros aguantan más en la rueda americana, menos en Londres, y menos cuando queda media rueda por delante. **Los tres eran el mismo error de medición.** El desenlace se miraba hasta el final de los datos, así que a un nivel tocado a la mañana le daba ocho horas para romperse y a uno tocado a las 15:30 media hora.

Arreglado con **horizonte fijo de 60 minutos** para todos, y los que no llegan a completar la ventana quedan censurados en vez de contarse como rotos. La tasa de aguante pasó de 21 % a 7 %.

**Why:** es el tipo de error que produce números convincentes y falsos, justo lo que este proyecto existe para no hacer. Cualquier comparación entre grupos con distinto tiempo de exposición tiene que normalizar por el tiempo.

**How to apply:** antes de creerle a un factor que separa por hora o por sesión, revisar si los dos grupos tuvieron el mismo tiempo para resolverse. Ver [[centinela-que-mide]] y [[auditoria-punta-a-punta]].
