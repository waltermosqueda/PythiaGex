# Familia ABSORCION REAL EN LA PUNTA (iceberg / reposicion) — MNQ, 17-09-2026

Pregunta: cuando alguien vende contra TODO el bid visible y el bid no baja (alguien repuso), ¿eso sirve de gatillo para un scalp de
8 a 15 puntos? ¿Y mejora al CVD como acompañante?

## 0. Antes de medir nada: lo que salio de mirar los datos SIN resultados

(Todo esto se hizo sin mirar que paso despues de ningun evento. Scripts `absorcion_00` a `absorcion_05`.)

**Hallazgo de integridad (afecta a todas las familias, no solo a esta).** `base.segundos_ricos` descarta las ordenes cuyo
`px_grupo` (precio // 150) queda a mas de 1 del grupo modal. La cinta de cada dia es de UN solo contrato (verificado: sin saltos
entre ordenes consecutivas, 0 segundos con dos precios a mas de 60 puntos), asi que en un dia de rango grande ese filtro corta
las colas del dia y la tabla de 1 s queda con el PRECIO CONGELADO:
- 2026-09-03 (explorar): la tabla termina a las 15:02 UTC; le falta el 51 % del volumen de la rueda.
- 2026-09-14 (confirmar): precio congelado en 29549,50 de 16:45 a 19:00 UTC; falta el 17 %.
- 2026-09-16 (confirmar, dia de la Fed): congelado en 29250,00 de 18:45 a 19:50 UTC; falta el 22 %.
- 2026-08-24: faltan 192 ordenes (nada).
No toque `base.py`. En `absorcion_base.tablas()` rehago esas cuatro tablas con LA MISMA cuenta, sin el filtro, en mi propio cache
(`absorcion_cache/`). Ademas descarto disparos con un hueco de datos (> 30 s sin ordenes) entre t-600 y t+900 (el 28-08 tiene uno de 159 s).

**La foto "despues" a veces sale antes de que el libro se actualice.** En el 16 % de las absorciones de base.py la punta de
despues tiene el mismo precio pero tamaño 0: no se sabe si repusieron o si el nivel se cayo un instante despues. Uso absorcion
LIMPIA: la orden consumio toda la punta visible, la punta quedo en el mismo precio y con tamaño >= 1.

**Como es la absorcion en MNQ (medido en las 12 sesiones de explorar):**
- La punta de MNQ es finita: 7 contratos de mediana por lado, spread de 1 o 2 ticks. El 53 % de las ventas consume TODO el bid
  visible, y en el 99,4 % de esos casos el bid baja. Que aguante es 1 caso cada 160.
- La absorcion real es el 0,2 % del volumen por lado (0,4 % sumando los dos). Son ordenes chicas: mediana 3 contratos, 1 de cada 100 pasa de 14.
- Pasa ~1.770 veces por rueda (una cada 13 s): raro en volumen, NO raro en cantidad.
- **No existen las "plantadas" tipo iceberg de manual.** Segui cada precio contra el que se vende hasta que el bid baja de ahi
  (`absorcion_03_plantadas.py`): un precio con 20+ contratos vendidos vive 0,3 s de mediana y el 99,5 % termina roto. Plantadas con
  2 o mas reposiciones y 20+ contratos: UNA por rueda. Con 3 o mas: una cada cinco ruedas. No hay muestra para medirlas, y eso solo ya
  dice algo: en MNQ el que aguanta no esta en la punta de MNQ (la punta de MNQ copia a NQ).

## 1. PRE-REGISTRO (escrito ANTES de correr ningun resultado)

Reglas comunes: disparo en el segundo t con datos <= t; entrada en `ultimo[t]`; resultado desde t+1 (`B.barrera`); solo rueda
13:32-20:00 UTC; minimo 60 s entre disparos de la misma variante (los dos lados juntos, gana el primero); se explora SOLO en las 12
sesiones de `B.explorar`. Notacion: AB_w / AA_w = contratos de absorcion limpia en el bid / en el ask en los ultimos w segundos
(incluye t). Los umbrales son percentiles de los segundos validos de explorar, congelados como numero.

| # | nombre | regla exacta (lado +1 = compra; el espejo da -1) |
|---|---|---|
| 1 | R10 | AB_10 >= 14 (p99) y AB_10 > AA_10 -> +1. Espejo: AA_10 >= 14 y AA_10 > AB_10 -> -1 |
| 2 | R30 | AB_30 >= 25 (p99) y AB_30 > AA_30 -> +1. Espejo con AA_30 >= 25 |
| 3 | R60N | AB_60 - AA_60 >= 34 (p99 del neto) -> +1; <= -34 -> -1 |
| 4 | R30_EXT | AB_30 >= 15 (p95) y ultimo <= minimo de 600 s + 2 pts -> +1. Espejo: AA_30 >= 15 y ultimo >= maximo de 600 s - 2 |
| 5 | R30_MED | AB_30 >= 15 y posicion en el rango de 600 s entre 0,35 y 0,65 -> +1. Espejo igual con AA_30 (contraste de la 4) |
| 6 | R30_OFI | AB_30 >= 15 y suma de ofi_pas de 30 s >= +150 (p75) -> +1. Espejo: AA_30 >= 15 y ofi_pas 30 s <= -130 (p25) |
| 7 | RATIO60 | AB_60 / (rompe_bid_60 + 1) >= 0,035 (p99) y rompe_bid_60 >= 940 (mediana: hay presion vendedora) -> +1. Espejo con ask |
| 8 | FALLA | AB_60 >= 24 (p95), L = precio mas bajo de esas absorciones; dispara -1 en el primer segundo con ultimo <= L - 1,0 (la absorcion fallo -> continuacion). Espejo: AA_60 >= 24, H = precio mas alto, ultimo >= H + 1,0 -> +1 |
| 9 | ABS_CVD | delta de 60 s <= -584 (p10: venta fuerte) y el precio NO cayo mas de 1 pt en 60 s y AB_60 >= 24 -> +1. Espejo: delta 60 s >= +584, precio no subio mas de 1 pt, AA_60 >= 24 -> -1 |
| 10 | CVD_SOLO | lo mismo que la 9 SIN pedir absorcion (control: cuanto agrega la absorcion a la divergencia del CVD) |
| 11 | ABS_GRANDE | una sola orden de venta >= 10 contratos absorbida limpia en el bid -> +1. Espejo en el ask -> -1 |
| 12 | REPONE_FUERTE | absorcion limpia con tamaño visible antes >= 3 y tamaño despues >= el de antes (repusieron igual o mas) -> +1. Espejo -> -1 |

**Regla para elegir finalistas (fijada antes de mirar):** barrera principal +-8 en 600 s. Candidata = n resueltos >= 150, acierto >= 53 %
y >= 8 de 12 dias arriba de 50 %. Entre las candidatas, las 2 de mayor z. Una variante con z <= -3 puede entrar INVERTIDA (se declara).
Si ninguna califica, se corre en confirmacion igual la de mayor z, rotulada "sin candidata", para dejar el numero.
Las variantes 5 y 10 son controles: no pueden ser finalistas.

**Veredicto (en las 8 sesiones de confirmar, una sola corrida):** SOBREVIVE = acierto >= 57,3 % (empate 55,3 + 2), z >= 2,5 contra
placebo (200 sorteos, misma sesion y media hora, mismos lados, separados 60 s), >= 6 de 8 dias arriba de 50 %, n >= 100. PISTA = positivo
pero falla un criterio. NADA = el resto.

## 2. EXPLORACION (12 sesiones, 20-08 al 04-09) — `absorcion_06_explorar.py`

Barrera principal +-8 pts en 600 s (empata con 55,3 %). "z dia" = el mismo test pero tomando cada DIA como una sola observacion
(no se infla por disparos solapados). L = disparos de compra, C = de venta. Neto = puntos por operacion despues de 0,85 de costo.

| variante | disparos | acierto +-8 | z | z dia | dias > 50 % | neto +-8 | acierto L / C | +-5 (58,5) | +-12 (53,5) | mov. medio a 10/30/60/300 s |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 R10 | 634 | 53,1 % | 1,55 | 1,42 | 7 de 12 | -0,08 | 53,9 / 52,4 | 51,0 | 51,8 | +0,11 / +0,46 / +0,68 / +1,96 |
| 2 R30 | 304 | **55,6 %** | 1,96 | 2,11 | 9 de 12 | -0,03 | 52,8 / 58,1 | 49,3 | 54,3 | +0,25 / +0,46 / +0,99 / +1,91 |
| 3 R60N | 119 | 58,8 % | 1,93 | 1,67 | 7 de 12 | +0,56 | 54,5 / 64,2 | 47,1 | 55,5 | +0,03 / +0,64 / -0,19 / +3,40 |
| 4 R30_EXT | 224 | 51,6 % | 0,47 | 0,00 | 8 de 12 | +0,18 | 50,5 / 52,4 | 45,3 | 48,4 | -0,06 / +1,13 / +1,78 / +3,34 |
| 5 R30_MED (control) | 340 | 48,5 % | -0,54 | -0,39 | 5 de 12 | -1,09 | 50,3 / 47,0 | 51,9 | 49,4 | +0,22 / +0,54 / +0,66 / -1,51 |
| 6 R30_OFI | 687 | 51,6 % | 0,84 | 0,48 | 5 de 12 | -0,60 | 52,7 / 50,6 | 52,1 | 50,7 | +0,10 / +0,49 / +0,04 / +1,44 |
| 7 RATIO60 | 24 | 58,3 % | 0,82 | 0,07 | 4 de 9 | +0,48 | 30,0 / 78,6 | 54,2 | 58,3 | (24 casos: no se lee) |
| 8 FALLA | 330 | 43,9 % | -2,21 | -2,73 | 2 de 12 | -2,33 | 41,3 / 47,2 | 47,6 | 45,2 | -0,49 / -0,93 / -1,40 / -2,63 |
| 9 ABS_CVD | 61 | 52,5 % | 0,38 | -0,47 | 5 de 11 | -0,46 | 54,8 / 50,0 | 50,8 | 55,7 | +0,29 / +2,86 / +2,00 / +4,60 |
| 10 CVD_SOLO (control) | 286 | 49,5 % | -0,18 | -0,07 | 5 de 12 | -0,93 | 49,4 / 49,6 | 46,7 | 47,4 | -0,36 / +0,42 / +0,35 / -0,32 |
| 11 ABS_GRANDE | 604 | 51,3 % | 0,65 | 0,93 | 8 de 12 | -0,35 | 51,3 / 51,4 | 50,6 | 49,4 | -0,07 / -0,11 / -0,13 / +1,44 |
| 12 REPONE_FUERTE | 744 | 49,5 % | -0,26 | -0,22 | 5 de 12 | -0,92 | 54,5 / 44,7 | 48,7 | 49,1 | +0,11 / -0,11 / -0,26 / -1,10 |

**Finalistas, por la regla escrita arriba (decidido ANTES de abrir la confirmacion):**
- **R30** es la unica candidata (302 resueltos, 55,6 %, 9 de 12 dias). Ojo desde ya: en exploracion su neto es -0,03 puntos por
  operacion, o sea que ni en la muestra donde se la eligio paga el costo; y en +-5 no tiene nada (49,3 %).
- R60N tiene mejor acierto (58,8 %) pero 119 casos: no llega al minimo de 150. RATIO60 y ABS_CVD casi no disparan (24 y 61).
- **FALLA dio al reves** de la hipotesis: cuando el precio pierde por 1 punto el nivel donde absorbieron, NO continua: vuelve
  (43,9 % de continuacion = 56,1 % de vuelta, 10 de 12 dias). Pero z = -2,21 no llega al -3 que pedia la regla para entrar invertida,
  y es el |z| mas grande de 12 variantes (que es lo que uno espera ver por puro azar al probar 12). NO es finalista. La corro UNA vez en
  confirmacion FUERA DE CONCURSO, invertida, con techo de veredicto PISTA, solo para dejar el numero.

## 3. CONFIRMACION (8 sesiones, 08-09 al 17-09) — una sola corrida, `absorcion_07_confirmar.py`

| | disparos | acierto +-8 | empate | z contra 50 % | placebo (media ± desvio) | z contra placebo | dias > 50 % | neto por operacion | +-5 | +-12 |
|---|---|---|---|---|---|---|---|---|---|---|
| **R30** (finalista) | 115 | **49,6 %** | 55,3 % | -0,09 | 49,4 ± 4,7 % | **+0,03** | **2 de 8** | **-0,92 pts** (placebo -0,95) | 42,1 % | 46,5 % |
| FALLA invertida (fuera de concurso) | 148 | 52,7 % | 55,3 % | +0,66 | 49,4 ± 4,1 % | +0,82 | 3 de 8 | -0,42 pts (placebo -0,94) | 54,1 % | 47,9 % |

Por dia, R30: 38 % de 21, 57 % de 23, 42 % de 12, 50 % de 14, 50 % de 8, 36 % de 11, 50 % de 12, 71 % de 14.
De contexto, el mismo R30 en exploracion contra SU placebo daba z = +2,59 (55,6 % contra 48,7 ± 2,7 %). Parecia algo. En datos nuevos
quedo clavado en el placebo. Es el espejismo tipico de elegir la mejor de 12.

Robustez (`absorcion_08`, punto D): con las columnas crudas de base.py (sin limpiar el 16 % dudoso) R30 da 53,0 % en explorar y
49,6 % en confirmar. Con cualquiera de las dos definiciones, nada.

## 4. VEREDICTO

- **R30: NADA.** 49,6 % con 115 disparos, z = 0,03 contra placebo, 2 de 8 dias, pierde 0,92 puntos por operacion (lo mismo que tirar la moneda y pagar el costo).
- **FALLA invertida: NADA** (y no era finalista). 52,7 %, por debajo del empate, 3 de 8 dias.
- **Familia entera: NADA.** La absorcion real en la punta de MNQ no es gatillo y no mejora al CVD: la variante armada para eso
  (ABS_CVD: venta fuerte + precio que no cae + absorcion en el bid) casi no dispara (61 veces en 12 ruedas) y dio 52,5 %; su control sin
  absorcion (CVD_SOLO) dio 49,5 %.

## 5. LO QUE SE APRENDIO DE LA MICROESTRUCTURA (sirve para LEER, no para disparar)

Todo lo de abajo es descriptivo y se muestra solo si repite en los dos bloques (explorar y confirmar). Scripts `absorcion_08`, `_09`, `_10`.

1. **En MNQ la punta es de papel.** 7 contratos de mediana por lado. Mas de la mitad de las ventas se llevan todo el bid visible y 159
   de cada 160 veces el bid baja un tick. Ver "rompieron el bid" en MNQ no es noticia: es lo normal, pasa 20 mil veces por rueda.
2. **El iceberg de manual no vive en MNQ.** Un precio que aguanta 2 o mas reposiciones con 20+ contratos: uno por rueda. La pelea
   de verdad esta en NQ; la punta de MNQ la copia. Si se quiere medir absorcion en serio, el lugar es la cinta de NQ, no la de MNQ.
3. **Un racimo de absorcion marca "cinta caliente", no "pared".** Despues de un R30 la barrera de +-8 se toca en 16-20 segundos de
   mediana; en un segundo cualquiera tarda 46-54. Y la mitad de los disparos caen entre las 13:30 y las 15:00 UTC. El racimo aparece
   porque hay mucha orden pegando, y de ahi sale para cualquiera de los dos lados: 50/50.
4. **La absorcion aparece cuando el precio viene EN CONTRA del que absorbe** (-2,4 puntos en los 30 s previos en explorar, -1,0 en
   confirmar). Es la huella de que hay presion, no de que se termino.
5. **El efecto direccional existe y mide menos de un tick.** Despues de un segundo con absorcion, el precio medio (bid+ask)/2 va a favor
   del que absorbio +0,2 pts a 30-60 s (emparejado contra segundos con la misma venta agresora y sin absorcion: +0,15 a +0,19 pts a
   60 s, en los dos bloques). El costo de entrar y salir es 0,85. Es cuatro a cinco veces mas chico que el costo: no se puede cobrar.
6. **Por minuto, la absorcion neta no explica ni la vela actual** (correlacion -0,06 / -0,01 con el retorno del mismo minuto) **ni la
   siguiente** (+0,01). El delta, de nuevo: +0,83 / +0,79 con la vela actual, -0,04 / -0,02 con la siguiente (igual que lo ya medido).
7. **Si algo dice, habla de COMPORTAMIENTO y no de direccion, y muy bajito.** A igual volumen y a igual rango previo, cuanta mas parte
   del volumen fue absorbida en los ultimos 30 s, un poco MENOS se mueve el precio en los 60 s siguientes: -0,19 pts por desvio en
   explorar (11 de 12 dias) y -0,13 en confirmar (7 de 8 dias), sobre un movimiento tipico de 6,5 a 7 pts. Es un freno del 2-3 %:
   real, repetible e inutil para operar. Volumen y rango previo pesan diez veces mas.
8. **Dos avisos de datos para el resto del laboratorio.** (a) El filtro de `px_grupo` de base.py congela el precio en dias de rango
   grande (09-03, 09-14, 09-16): cualquier familia que use esas tablas tiene barreras mal medidas en esos tramos. (b) El 16 % de las
   abs_bid / abs_ask de base.py tiene tamaño 0 despues: son fotos tomadas antes de que el libro se actualice, no reposiciones vistas.

## 6. Archivos

Scripts (todos en `laboratorio/gatillo/`): `absorcion_base.py` (tablas corregidas, validez, desagrupado, barreras, placebo),
`absorcion_variantes.py` (las 12 reglas), `absorcion_00_describe.py`, `absorcion_01_cinta_mirar.py`, `absorcion_02_integridad.py`,
`absorcion_02b_huecos.py`, `absorcion_03_plantadas.py`, `absorcion_04_umbrales.py`, `absorcion_04b_umbrales_limpios.py`,
`absorcion_05_eventos.py`, `absorcion_06_explorar.py`, `absorcion_07_confirmar.py`, `absorcion_08_microestructura.py`,
`absorcion_09_velocidad.py`, `absorcion_10_freno.py`. Cache propio: `laboratorio/gatillo/absorcion_cache/` (se regenera solo).
Orden para reproducir: 03 y 05 (pasadas por la cinta, ~2 min cada una), despues 06, 07, 08, 09, 10.
