---
name: nq-rithmic-pierde-0dte
description: "La cadena viva de NQ por Rithmic perdio el 0DTE a las 10:16 ET del 2026-09-11 (timeout de 25 s + 'no data' en la serie del dia) y siguio todo el dia con lunes/martes sin reintentar; Gamma Hoy 1.8j reintenta la fecha exacta dos veces, desuscribe el armado viejo y rearma cada 5 min mientras falte el vencimiento mas cercano."
metadata: 
  node_type: memory
  type: project
  originSessionId: 65e7848c-844c-4b37-8346-096df7f51a44
  modified: 2026-09-11T15:42:23.726Z
---

Encontrado el 2026-09-11 al comparar ATAS contra capturas de la referencia: el
MNQ dibujaba dominantes en 29.500 / 29.000 mientras la referencia tenia 714/715
de QQQ (29.340 / 29.381). Parte de la diferencia era el libro (ver
[[referencia-formulas-nq-medidas]]); la otra parte era un bug propio.

## Que paso (log `pythiagex-gammahoy.log` y `viva-NQ-2026-09-11.jsonl`)

- 04:09 a 14:17 UTC: la viva de NQ traia el 0DTE (vencimiento 11-09, 190 filas,
  4.139 contratos operados a las 08:00).
- 11:16:56 local (10:16 ET): al rearmar, `[puente] Rithmic no contesto en 25 s
  los contratos de NQU6 20260911`, y el segundo pedido (formato mes) volvio
  `no data`. La serie quedo vacia, la cadena siguio con 09-14 y 09-15 (183+6
  filas), Activa=true, y como Gamma Hoy solo rearma cuando `!Activa`, nadie
  volvio a pedir el 0DTE en toda la rueda. ES no lo sufrio (0,2 dias, 138 filas
  a las 14:55).
- Los rearmes de las 10:42-11:19 local eran instancias distintas (MES 2m, MNQ
  2m, MNQ 5m): no existe rearme periodico; cada uno suma 200 suscripciones sin
  soltar las anteriores. Con 400+ contratos suscritos ATAS mostraba "Market
  Data Latency: 6086 ms" en el grafico y el DOM mientras el operador operaba.

## El arreglo (Gamma Hoy 1.8j, compilado e instalado el 11-09 12:40 local)

- `PuenteRithmic.OpcionesAsync`: pide la fecha exacta DOS veces antes de caer al
  mes; el log dice el numero de intento.
- `CadenaViva`: expone `FaltaCercano` (activa pero la serie mas cercana devolvio
  0 contratos) y lo anota: "FALTA EL VENCIMIENTO MAS CERCANO". Antes de suscribir
  desuscribe `_suscritos` del armado anterior ("desuscritos N contratos"). El
  enganche de volumen por contrato es unico (HashSet): un rearme no cuenta el
  volumen dos veces.
- `GammaHoy.RearmarVivaSiHaceFalta`: apagada cada 3 min (como antes); activa con
  `FaltaCercano` cada 5 min.
- Rotulos: "libro ES (Rithmic)" salia en MNQ; ahora usa la raiz del grafico
  (`Fuente = "Rithmic " + Raiz()`).

## Verificado tras reiniciar (11-09 12:42 y 12:47 local)

- 12:45:28: "6 vencimientos, 4056 contratos (NQU6, por PUENTE)"; la viva de NQ
  volvio con el 0DTE: 136 filas a 0,2 dias y 23.182 contratos operados hoy.
  El AUDIT paso de netVol 0,65 B (sin 0DTE) a 19 B (con el 0DTE): el 0DTE ES el
  libro intradia de NQ. Rotulo nuevo: `origen=libro Rithmic NQ, sin base`.
- Segundo hallazgo: `_ultimoIntento` (cerrojo de 60 s del arranque) era
  `static` y se compartia entre MES, MNQ 2m y MNQ 5m: las de NQ volvian en
  silencio y arrancaban 3 min despues en el reintento (ES 10:39:10 / NQ
  10:42:15; ES 12:42:24 / NQ 12:45:25). Ahora es por instancia (segundo commit).

- Tercer hallazgo, provocado por el segundo: con el cerrojo por instancia las
  tres instancias suscribieron 600 contratos en 12 s y **Rithmic se ahoga con
  rafagas**: NQ quedo en 0 de 200 puntas (12:51) y 8 de 200 (12:54), cuando a
  las 12:45, con 3 min de separacion, llegaron 116 en 1 s; en los 23 armados
  anteriores del dia no hubo ni un "se reintenta". El cerrojo static era, sin
  querer, el espaciador. Arreglo (tercer commit): espaciado GLOBAL de 75 s entre
  suscripciones de cualquier instancia ("espero N s" en el log) y la
  desuscripcion del rearme suelta solo lo que salio de la ventana (soltar y
  volver a pedir el mismo contrato en 2 s lo dejaba sin puntas; las instancias
  del mismo grafico comparten los objetos Security).

- Cuarto commit: cada instancia RESERVA su turno (ES, +75 s NQ, +150 s NQ; los
  rearmes se encolan detras). Verificado tras el cuarto reinicio (13:02): ES
  135 de 200 a los 9 s, NQ 78 de 200 al minuto 1:40, NQ 183 de 200 al 2:55,
  cero "se reintenta"; viva-NQ 16:05 UTC con 0DTE (164 filas, 28.767 contratos,
  168 con las dos puntas); AUDIT `libro Rithmic NQ` doms 29.500 / 29.400. El
  aviso "Could not resolve type PythiaGex.GammaHoy" del log de ATAS sale en
  todos los arranques del dia: es de siempre, no de estos cambios.

**Why:** un libro sin su 0DTE es el peor de los mundos: parece vivo, dibuja, y
esta midiendo el vencimiento equivocado sin decirlo.

**How to apply:** si las dominantes de MNQ caen en strikes redondos lejanos
durante la rueda, mirar en el log si el `[cadena viva] N vencimientos` de NQU6
incluye el dia de hoy y si aparece "FALTA EL VENCIMIENTO MAS CERCANO". Ver
[[cadena-es-en-vivo-rithmic]], [[vencimientos-0dte-auditados]],
[[dominantes-de-noche-resto-de-ayer]].
