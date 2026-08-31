---
name: auditoria-punta-a-punta
description: "El auditor que recalcula todo desde la cadena cruda, y los tres errores que encontró: el flip sin interpolar, el hueco del workflow y el deploy que nunca se disparaba."
metadata: 
  node_type: memory
  type: project
  modified: 2026-08-31T10:02:57.468Z
  originSessionId: a021c566-b4fe-42c5-acd1-0a01a165a079
---

`PythiaGex/auditoria.py`. **No reusa las funciones del proyecto para verificarlas**: recalcula cada número desde la cadena cruda con la fórmula escrita de nuevo, y recién después compara. Si las dos cuentas coinciden, el número es real. Si no, eso *es* el hallazgo.

```bash
python auditoria.py            # SPX
python auditoria.py NDX RUT
python auditoria.py --historia # evolución de las corridas del día
```

Once controles: base, contado implícito, coherencia con la tasa del Tesoro, Net GEX, Net DEX, call wall, put wall, gamma flip, expected move, probabilidad de toque y cobertura. Cada corrida se guarda en `datos/auditoria/YYYY-MM-DD.jsonl`.

## Los tres errores que encontró el 2026-08-31

**1. El gamma flip estaba corrido hasta 18 ticks.** La curva se calcula en 200 pasos sobre ±6 %, o sea pasos de 4,63 puntos de SPX. El cruce por cero casi nunca cae justo en un punto de la grilla, y se devolvía el punto de la grilla en vez del cruce. Se interpola linealmente entre los dos puntos que lo encierran: el error pasó de 1,96 puntos a 0,003. **Era el nivel que define el régimen.**

**2. Había un hueco de catorce horas en el workflow.** Corría de 13:00 a 21:00 UTC. Auditado a las 09:52 UTC: CBOE refrescaba la cadena de SPX **cada un minuto** y nosotros sin publicar desde las 21:00 del día anterior. ES opera casi 24 horas. Nuevo horario: cada 15 min alrededor de la rueda americana, cada 30 el resto, más la reapertura de Globex.

**3. El más grave: el deploy de Pages nunca se disparaba solo.** GitHub **no dispara workflows desde los push hechos con `GITHUB_TOKEN`**, para evitar bucles. El workflow de datos commitea con ese token, así que sus commits nunca disparaban la publicación: el dato entraba al repositorio y ahí quedaba hasta que alguien pusheara a mano. Verificado: el bot commiteó 09:57 y el último deploy era de las 07:45.

Los números estaban bien. **Lo que fallaba era la entrega.** Se arregló con `workflow_run` en `pages.yml`, escuchando cuando termina el de datos, con guarda para no publicar si falló.

## Lo que también quedó verificado

- **CBOE refresca SPX y NDX cada minuto en horario nocturno.** La duda que quedó abierta cuando la cadena tenía 201 minutos a las 02:37 ET: la cadencia varía, no es fija. A las 09:52 UTC tenía 0,9 minutos.
- **RUT no refresca.** 54 horas de atraso el mismo día. Sus niveles de índice sirven, los de futuro no.
- El control de coherencia del carry **falla en NDX**, que es lo esperado: su base ya está marcada floja por 25 ticks de error. El sistema se contradice solo y lo dice.

**Why:** el proyecto entero existe porque los tableros públicos mienten por omisión. Un auditor que reusa el mismo código que verifica no audita nada.

**How to apply:** correrlo varias veces por rueda y mirar `--historia`. Si aparece una falla, el número que discrepa es el que hay que revisar, no el auditor. Ver [[calcular-gex-propio]] y [[conversion-spx-a-es]].

## Lo que apareció siguiendo la evolución (23 corridas, 3 horas)

**Las paredes estaban casi empatadas y se reportaba una sola.** El Call Wall saltó de 7750 a 7800 a las 11:23 UTC — cincuenta puntos, doscientos ticks de ES. No era un error de cálculo: el 7800 tenía 1.200 M y el 7750 tenía 1.066 M, el **89 % del líder**. El Put Wall estaba peor: 7650 con −2.687 M contra 7675 con −2.622 M, el **97,6 %**.

Con esa diferencia un movimiento mínimo del precio da vuelta cuál gana. Decir "el techo está en 7800" es una moneda al aire presentada como dato. Ahora se calculan los cuatro candidatos con su peso relativo, y si el segundo llega al 85 % la pared queda marcada **DISPUTADA**: el indicador sombrea la zona y dibuja las dos rayas.

**Qué se mueve y qué no, medido.** En tres horas de premercado: put wall quieto, call wall un salto discreto, gamma flip 2,68 puntos, base 0,15 puntos, GEX −13,7 a −23,3 B. Lo único que se mueve de verdad es el GEX, porque depende del precio — y por eso el indicador lo reinterpola en vivo en vez de mostrar el de la cadena.

## Faltaba la capa intermedia

Las paredes grandes estaban a 117 y 33 puntos. Para scalpear MES eso es lejos. Se agregaron:

- **Niveles cercanos**: los strikes cargados dentro de ±0,6 % del precio, ordenados por **cercanía** y no por tamaño, porque importa cuál se toca primero. Cada uno con su peso relativo y el signo traducido: gamma positiva **frena**, negativa **empuja**.
- **Niveles por vencimiento**: techo, piso e imán de cada uno de los tres vencimientos más cercanos por separado. El 0DTE tenía el piso en 7661 y el del día siguiente en 7686.

## Respuesta a una pregunta que estaba abierta

**Un indicador NO puede alcanzar `IOptionsDataFeed`.** Probado en vivo por reflexión desde el indicador: `GetService<IOptionsDataFeed>()` tira excepción. El GEX sobre opciones de ES en tiempo real, sin CBOE y sin base, no sale por ese camino. Si alguna vez se quiere, hay que exportar desde el tablero de opciones.
