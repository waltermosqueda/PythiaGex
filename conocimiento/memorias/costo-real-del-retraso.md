---
name: costo-real-del-retraso
description: "El retraso de CBOE cuesta 0,29 puntos en el zero gamma y CERO en los muros; el libro de futuros llega en 157 ms. Medido, no supuesto."
metadata: 
  node_type: memory
  type: reference
  originSessionId: 15e03ce1-51d8-43ea-8c35-a2fa1a4b8145
  modified: 2026-09-03T16:54:01.301Z
---

Medido el 2026-09-03, el día que el operador dijo que quince minutos de
retraso eran inadmisibles para scalping y que el proyecto entero iba a ser en
vano. Tenía razón en el principio, pero **"hay retraso" y "el retraso me
mueve el nivel" son dos cosas distintas**, y la segunda se mide.

## Qué cuesta el retraso de la cadena

`medir_costo_retraso.py` toma dos fotos de la cadena separadas 902 s y calcula
los niveles con las dos **al mismo precio**, congelándolo a propósito para que
lo único que cambie sea la entrada que llega tarde. Sobre 14 pares del
2026-09-03, cubriendo la rueda entera:

```
major positive   0,00 pts   NO SE MUEVE NUNCA
major negative   0,00 pts   NO SE MUEVE NUNCA
zero gamma       0,29 pts de media, 1,38 el peor (15:08, cerca del cierre)
net gex          0,37 B de media
```

Los muros no se mueven porque son un **argmax sobre strikes**: quince minutos
de volatilidad nunca cambian cuál es el strike más grande. El zero gamma se
corre poco más de un tick de ES (0,25 pts).

## Cuánto tarda cada caño

```
libro de futuros (Rithmic)     mediana  157 ms    p95 ~1 s
cadena de opciones (CBOE)              902 000 ms
```

Tres órdenes de magnitud. El indicador mide el del libro solo y lo publica en
`lagdom_ms` dentro del renglón `AUDIT` de su log.

**Trampa al medirlo:** ATAS entrega la hora del barrido **sin zona horaria**.
Restarle la hora local da tres horas y cualquier ventana de cordura descarta
todas las muestras — el renglón sale `sinmuestra` con el mercado abierto y los
círculos dibujándose en pantalla. Hay que probar contra hora local y UTC y
quedarse con la diferencia más chica.

**Segunda trampa:** la medición cruda dio −3573 ms, negativa. No era un error
de cálculo: **el reloj de la máquina está 3,73 s atrasado**, confirmado con
`w32tm /stripchart /computer:time.windows.com`. Hay que descontarlo antes de
publicar el número. Si vuelve a dar negativo, mirar el reloj antes que el
código.

## La lectura que importa

El diseño para scalping queda al derecho: **los niveles salen de la gamma
(repreciada tick a tick contra Rithmic, con error medido de menos de un tick
por el retraso) y el gatillo sale de la cinta de futuros (157 ms).** El bloque
de volumen de opciones sigue llegando 902 s tarde y por eso lo dice en
pantalla; es apoyo, nunca gatillo.

Esto no habilita pagar nada. Ver [[autonomia-por-sesion]] y la condición de
las 15 sesiones de bitácora.

**Why:** sin este número, la conversación sobre el retraso se decide por
impresión y el operador estaba por descartar un trabajo que sí sirve. Con él,
cada caño va al cajón que le corresponde.

**How to apply:** correr `python medir_costo_retraso.py` cuando vuelva la duda,
y leer `lagdom_ms` del log del indicador para el libro. Ver
[[retraso-cboe-902s]] para el 902 s en sí y [[calcular-gex-propio]] para el
método.
