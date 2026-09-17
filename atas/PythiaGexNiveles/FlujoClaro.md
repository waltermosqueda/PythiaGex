# Flujo Claro — el reemplazo a medida del CVD de fábrica (2026-09-17)

Pedido del operador: "uso el CVD abajo del gráfico pero es chiquito, apenas se entiende, tengo que ampliarlo
demasiado, y siento que me avisa tarde; ¿no hay algo superador?". Después de ver las maquetas eligió la
**Fusión 1 con las cinco cintas**. Archivo: `FlujoClaro.cs` (indicador aparte, mismo DLL). En ATAS aparece como
**PythiaGex - Flujo Claro**.

## Qué dibuja

1. **Cinco cintas pegadas al precio** (arriba del panel, una celda por vela):
   - *confluencia*: cuántas de las 4 lecturas de flujo están del mismo lado (intensa = las 4).
   - *presión*: delta de las últimas 5 velas contra lo normal de la última hora, en desvíos (σ).
   - *grandes*: compras menos ventas de las órdenes agresoras de 50 contratos o más.
   - *cinta*: cantidad de operaciones de la vela contra lo normal (hay apuro ahora; no dice dirección).
   - *absorción*: mucho delta y el precio avanza menos de un cuarto de lo que ese delta suele mover.
2. **Panel**: el CVD **anclado** (el cero es el CVD de hace 60 velas: la misma curva, 3-4 veces más alta de leer)
   con línea verde/roja, y detrás las barras de presión con bandas fijas ±1σ y ±2σ. La última barra se mueve con
   cada operación. Opción: velas de delta con mecha (máximo y mínimo del delta dentro de la vela).
3. **Marcas sobre las velas del precio**: rombo ámbar = absorción (hueco mientras la vela está abierta);
   triángulo rojo/verde = divergencia precio/CVD (máximo o mínimo nuevo de 20 velas que el CVD no acompaña).
4. **AHORA, en una fila** (pedido del operador: "el bloque nos roba espacio"): va en el renglón del título del
   panel, a la derecha del nombre que escribe ATAS, así no ocupa nada extra. Cuatro celdas (5 s, 15 s, 60 s,
   5 min) con el delta como % del volumen de esa ventana, calculadas con cada operación; después FLUJO n/4,
   el aviso **NO PERSEGUIR** (3 o 4 lecturas del mismo lado), ABSORCIÓN, PRESIÓN, GRANDES y CINTA rápida; lo que
   no entra se saca por prioridad. Opciones: fila abajo, bloque (el cuadro grande) u oculto.

Todo se prende, se apaga y cambia de color/tamaño en los ajustes (grupos 1 a 7). Los nombres de los ajustes
empiezan con `Fc` porque ATAS guarda los ajustes por nombre en el workspace.

## Lo que está medido (y por qué el tablero dice lo que dice)

Datos: velas propias de 1 y 2 minutos del 08 al 17-09 (`laboratorio/cvd_superador.py`, `cvd_fusion.py`).

- **Legibilidad**: con el CVD del día entero, la última hora ocupa el 27 % (MNQ) y el 35 % (MES) del alto del
  panel. Anclado, el 100 %. Además el panel del operador estaba en modo *Bars* con escala desde cero
  (0 a 12.000 en 140 px = 86 contratos por píxel: un giro de 300 contratos son 3-4 píxeles).
- **El delta no adelanta**: la correlación entre el delta de una vela y el retorno de esa misma vela es +0,82
  (MNQ 1 min), +0,77 (MNQ 2 min), +0,69 (MES 2 min); con la vela siguiente, −0,09, −0,04 y −0,03. La literatura
  dice lo mismo (Cont, Kukanov y Stoikov 2014: el flujo explica el movimiento del MISMO intervalo).
- **Ninguna señal de vela le ganó al azar** a 5 velas (cruce de presión, presión extrema, mecha de delta).
- **Confluencia 4 de 4: a favor solo el 40 %** de las veces en los 5 minutos siguientes (96 casos, z −1,4; con 3
  o más, 48 %). Tendencia débil, no prueba. Por eso no dice "compra": dice *no perseguir*.
- **Absorción**: 46 % en contra del delta con 24 casos (z +0,3): sin evidencia todavía. Una primera medición dio
  57 % con 21 casos, pero usaba un percentil global que miraba adelante: era un artefacto (lo encontró la revisión).

## Revisión antes de instalar (17-09, tres revisores)

Corregido antes de tocar el ATAS en vivo: las divergencias marcaban de más (no exigían que el precio superara al
extremo anterior: más de la mitad eran falsas); todo el dibujo quedaba corrido media vela (`GetXByBar(bar, false)`
devuelve el centro, no el borde); el ancla del CVD fallaba con el gráfico corrido; "4 lecturas" eran 3 (presión y
CVD de 5 velas son la misma cuenta: ahora el CVD mira 20 velas); el percentil de la absorción con 300 velas dejaba
pasar el 70 % de las velas en la apertura; el extremo se evaluaba con la vela a medio hacer; los grandes históricos
podían apilarse en una sola vela si la respuesta llegaba en medio de un recálculo; y cambiar un ajuste de cálculo
no recalculaba la historia.

Por eso todo lo que se marca es **lectura**. Cada marca en vivo queda en
`%APPDATA%\ATAS\PythiaGex\flujo\marcas-<instrumento>-<día>.jsonl` (hora, precio, tipo, sentido y los números de
la vela) para medirla contra placebo con muestra suficiente antes de llamarla señal.

## 1.2 (17-09, 14:20): la cinta de presión, calibrada para scalping

El operador: "los colores avisaron tarde: a las 13:57 había un martillo y la presión seguía roja; a las 13:58 vela
verde y la presión roja y la confluencia negra". Tenía razón y se midió (`laboratorio/cvd_calibrar.py`, `…2.py`,
`…3.py`; giros = pivotes de ±3 velas con recorrido ≥ 0,05 %; MNQ 1 min / MNQ 2 min / MES 2 min):

- **Antes (suma de 5 velas):** retraso mediano en los giros 3-4 velas; acuerda con la vela actual 44-49 %; va EN
  CONTRA de la vela 20-22 %; 26-32 % de los colores nuevos duran una sola vela.
- **Ahora (presión eficaz sostenida):** retraso mediano 1 vela; acuerda 68-78 %; en contra 8-10 %; colores de una
  vela 19-24 % (parpadea MENOS que la anterior). El delta de la vela a secas era igual de rápido pero parpadeaba el
  triple (63 %).
- **La regla:** el color de la vela solo si el precio ACOMPAÑA al delta (cuerpo del mismo signo y al menos 25 % de
  lo que ese delta suele mover); se sostiene una vela floja; neutro si aparece flujo en contra sin precio (posible
  absorción) o a la segunda vela sin efecto. La vela de indecisión (cuerpo < 20 % del rango) y la sostenida se
  dibujan **tenues, casi negras**: hacerlas neutras del todo subía el parpadeo de 22 % a 40 %.
- **Su ejemplo, vela por vela:** 13:55 rojo fuerte, 13:56-13:57 tenue, 13:58-14:01 verde, 14:02 rojo, 14:03 tenue,
  14:04 verde (antes: rojo hasta las 13:59 inclusive).
- **Confluencia** con esta presión: retraso 3 → 2 velas, "nunca gira" 25 → 17 %. Y el aviso de extremo salió
  reforzado: 4 de 4 lecturas, a favor a 5 velas solo 36 % (94 casos, z −2,6); 3 o más, 42 %.
- Las barras del panel pasan de sumar 5 velas a 2 (`FcPresionVelas`, nombre nuevo para pisar el 5 guardado).

## Pendiente

- Cinta de **presión del libro** (desbalance del mejor bid/ask, `OnBestBidAskChanged`): en la literatura explica
  el doble que el delta en el mismo intervalo (65 % contra 32 %, acciones 2010, contemporáneo, no predictivo).
  Se agrega midiendo primero.
- Medir las marcas grabadas contra tres placebos (azar, gemelo de precio, misma hora) con 25+ casos por tipo.
