# Flujo Claro — el reemplazo a medida del CVD de fábrica (2026-09-17)

Pedido del operador: "uso el CVD abajo del gráfico pero es chiquito, apenas se entiende, tengo que ampliarlo
demasiado, y siento que me avisa tarde; ¿no hay algo superador?". Después de ver las maquetas eligió la
**Fusión 1 con las cinco cintas**. Archivo: `FlujoClaro.cs` (indicador aparte, mismo DLL). En ATAS aparece como
**PythiaGex - Flujo Claro**.

## Qué dibuja

1. **Cinco cintas pegadas al precio** (arriba del panel, una celda por vela). Desde la 1.4 las tres primeras hablan
   **de la vela que tienen encima**, con una sola gramática:
   **verde / rojo** = el flujo ACOMPAÑA a la vela (brillo = cuánto) · **violeta** = el flujo le lleva LA CONTRA ·
   **gris** = vela de indecisión (doji, martillo) · **fondo tenue** = fila tranquila, no hubo nada.
   - *confluencia*: la vela vota, y se le suman las lecturas de flujo que la acompañan y se le restan las que van en
     contra (delta de la vela, dónde cerró el delta, grandes, CVD de 20 velas). Más viva cuantas más la acompañan.
     Violeta solo si el delta de ESA vela o los grandes van contra ella.
   - *presión*: el delta de la vela contra los de las últimas 60 velas, descontado lo normal de esa hora del día (por
     percentil, no en sigmas). Delta a favor = color de la vela, más brillante cuanto más grande; delta en contra y
     grande = violeta; delta chico = color a media luz.
   - *grandes*: compras menos ventas de las órdenes agresoras de 50 contratos o más; violeta si fueron contra la vela;
     brillo por su lugar entre las últimas 300 velas con grandes. Fondo tenue = no hubo.
   - *cinta*: fila de alarma. Prende cuando las operaciones de la vela están entre el 30 % más alto de la hora. No dice
     dirección. Fondo tenue = tranquila (lo normal 7 de cada 10 velas).
   - *absorción*: fila de alarma. Mucho delta y el precio avanza menos de un cuarto de lo que ese delta suele mover.
   La celda de la vela en curso va llena y con un borde fino: se mueve con la vela y puede cambiar hasta el cierre.
2. **Panel**: el CVD **anclado** (el cero es el CVD de hace 60 velas: la misma curva, 3-4 veces más alta de leer)
   con línea verde/roja, y detrás las barras de presión con bandas fijas ±1σ y ±2σ. La última barra se mueve con
   cada operación. Opción: velas de delta con mecha (máximo y mínimo del delta dentro de la vela).
3. **Marcas sobre las velas del precio**: rombo ámbar = absorción (hueco mientras la vela está abierta);
   triángulo rojo/verde = divergencia precio/CVD (máximo o mínimo nuevo de 20 velas que el CVD no acompaña).
4. **AHORA, en una fila** (pedido del operador: "el bloque nos roba espacio"): va en el renglón del título del
   panel, a la derecha del nombre que escribe ATAS, así no ocupa nada extra. Cuatro celdas (5 s, 15 s, 60 s,
   5 min) con el delta como % del volumen de esa ventana, calculadas con cada operación; después FLUJO n/4,
   VELA CONTRA EL FLUJO / INDECISIÓN, ABSORCIÓN, DELTA de la vela con su percentil, GRANDES y CINTA rápida; lo que
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
  **RETIRADO en la 1.5:** con 20 ruedas da 50 % (n 2.716): era ruido de muestra chica. Ver la sección 1.5.
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

- **Antes (suma de 5 velas):** retraso mediano en los giros 3-4 velas.
- **Ahora (presión eficaz sostenida):** retraso mediano 1 vela. Esto es lo único que quedó firme después de la
  verificación (ver 1.3): se repite en 16 de 16 días y con 27 maneras distintas de definir un giro.
- **RETIRADO (1.3):** acá decía "acuerda con la vela 68-78 %" y "parpadea MENOS". Las dos frases estaban mal; la
  corrección, con el dato, está en la sección 1.3.
- **La regla:** el color de la vela solo si el precio ACOMPAÑA al delta (cuerpo del mismo signo y al menos 25 % de
  lo que ese delta suele mover); se sostiene una vela floja; neutro si aparece flujo en contra sin precio (posible
  absorción) o a la segunda vela sin efecto.
- **Su ejemplo, vela por vela:** 13:55 rojo fuerte, 13:56-13:57 tenue, 13:58-14:01 verde, 14:02 rojo, 14:03 tenue,
  14:04 verde (antes: rojo hasta las 13:59 inclusive).
- Las barras del panel pasan de sumar 5 velas a 2 (`FcPresionVelas`, nombre nuevo para pisar el 5 guardado).

## 1.3 (17-09, 14:45): lo que corrigieron los dos verificadores de la calibración

Dos revisores independientes intentaron romper la calibración 1.2. Veredicto: **se sostiene con reservas**.

**Lo que quedó en pie.** La cinta nueva llega a los giros ~2 velas antes (mediana 1 contra 3-4): 16 de 16 días, 27
definiciones de giro. Contra su propio placebo (la misma cinta con los deltas barajados) la ventaja real es de
~0,5-0,6 velas; la suma de 5 velas era PEOR que su propio placebo.

**Lo que se retira, de frente:**
- *"Acuerda con la vela 68-78 %"* era circular: la regla pinta el color solo cuando la vela ya tiene ese color, así
  que acordar con ella no prueba nada. La medida honesta es la vela SIGUIENTE: 48-51 % para las dos cintas. **La
  cinta no anticipa: describe la vela que acaba de cerrar, dos velas antes que la vieja.**
- *"Parpadea menos"* es falso en absoluto: cambia de color 17,5 veces por hora contra 10,8 (MNQ 1 min, ~60 % más), y
  los colores de una sola vela pasan de 3,41 a 3,79 por hora. Lo que bajó es la PROPORCIÓN de colores fugaces, no
  la cantidad. Es el precio de ser rápida.
- La regla fina (exigir que el precio acompañe) suma poco sobre el delta de una vela a secas: casi toda la mejora
  viene de mirar 1 vela en vez de 5.

**Lo que se arregló en el código (1.3):**
- **La celda de la vela en curso va HUECA** en confluencia y presión: a mitad de vela el color difiere del de
  cierre en el 33 % de las velas (10 % muestra el contrario); antes era 16 % (1 %). Lleno = vela cerrada.
- **La sostenida se ve a media luz y solo la de indecisión queda casi negra.** Con las dos casi negras el parpadeo
  que veía el ojo era 72-76 %, no el 19-24 % medido sobre el estado.
- **La confluencia contaba dos veces lo mismo:** la presión y "delta/volumen de la vela" coincidían en 95-99 % de
  las velas. La cuarta lectura ahora es **dónde cerró el delta dentro de su recorrido** (capta el martillo: venta
  temprana, compra al final) y la presión cuenta solo si es de esa vela. Remedido (MNQ 1 min, 950 velas): 3 o más
  lecturas del mismo lado, a favor a 5 velas 41 % (173 casos, z −1,4); las 4, 36 % (86 casos, z −1,8). Sigue
  diciendo lo mismo: extremo = tarde, **no perseguir**; y sigue sin ser prueba (|z| < 2). **RETIRADO en la 1.5** (50 % con 20 ruedas).
- El texto "PRESION xσ" del AHORA era otra cuenta que la cinta "presión": ahora dice **DELTA 2v** (lo que es).
- El banco (`cvd_calibrar.py`) descarta las filas duplicadas del archivo de 2 min quedándose con la de mayor volumen.

**Reservas que siguen abiertas:**
- La muestra es chica: MNQ 1 min son ~950 velas de rueda (unas 2,5 sesiones), casi todas del contrato U6. **La regla
  queda congelada** y se juzga con las próximas 5 sesiones completas de MNQZ6, sin retocarla en el medio.
- La primera hora después del cierre la cinta queda casi ciega (el desvío "normal" de 60 velas todavía es el de la
  rueda). No hay un arreglo validado; no se toca hasta medirlo.
- La beta del banco y la del C# difieren levemente (umbral de delta mínimo): los números del banco son aproximados
  al decimal, no al centésimo.

## 1.4 (17-09, 15:30): la celda habla de la vela que tiene encima

El operador, por tercera vez: "el heatmap avisa tarde o el color no corresponde con la vela, o aparecen muchos negros
huecos; trabajalo hasta que coincidan bien, tengan lógica y muy buen porcentaje". Tenía razón y se midió con su
criterio (`laboratorio/cvd_coherencia.py`). Para tener muestra, el indicador ahora vuelca TODAS las velas del gráfico
a `%APPDATA%\ATAS\PythiaGex\flujo\velas-<inst>-<marco>.csv` (copia del 17-09 en `datos/flujo/`): 27.209 velas de
1 minuto, 20 ruedas, en vez de las 950 del centinela.

**Lo que veía (1.3), en velas de rueda con cuerpo:** presión apagada en el **40 %** de las velas; confluencia con el
color CONTRARIO en el 8 % y apagada en el 17 %. La causa principal no era la regla fina sino la vara: el delta se
medía en desvíos estándar de 60 velas, y después de una vela enorme todo lo demás quedaba "chico" y se pintaba negro.

**La 1.4:** el tamaño del delta es su LUGAR entre los de la última hora (percentil: no lo ciega una ráfaga), y la
celda toma el color de la vela. Resultado en las mismas velas: presión coincide **96,8 %**, violeta 3,2 %, color
contrario 0, apagada 0. Confluencia coincide 81,4 %, violeta 8,5 %, gris (flujo parejo) 10,1 %, contrario 0.
Paridad C#/Python: 0 diferencias en 26.889 velas. En pantalla (control por píxeles de la captura): 38 velas con
cuerpo, 0 celdas negras y 0 contrarias en presión y confluencia.

**Lo que esto NO es (de frente):** coincidir con la vela es **por construcción**, no es acierto. La vela SIGUIENTE
sale del color de la celda el 48 % de las veces: la cinta no anticipa. Lo que le agrega a la vela es (a) cuánto flujo
la respalda (el brillo) y (b) cuándo el flujo la contradice (el violeta). La celda violeta tiene un indicio, no una
prueba: la vela siguiente fue a favor del DELTA el 56,7 % de las veces (247 casos, z +1,8); a 5 velas, nada.

## 1.5 (17-09, 16:06): lo que encontraron los tres revisores de la 1.4

Tres revisores independientes (código, estadística con scripts propios, y "el ojo del scalper" sobre la captura y
el CSV) atacaron la 1.4. Veredicto de los tres: **se sostiene con reservas**. Confirmaron lo descriptivo al decimal
(paridad 0 diferencias, sin mirada adelante, pantalla = CSV en 65 de 65 celdas) y voltearon varias cosas:

**Se retira, de frente:**
- **"EXTREMO: no perseguir".** Con 950 velas había dado 36-41 % a favor (y ya se había dicho que no era prueba). Con
  las 20 ruedas del volcado: 50,2 % (n 422 con las 4 lecturas) y 49,6 % (n 2.716 con 3): una moneda. Además estaba
  prendido el 36-40 % del tiempo: no era un extremo. Salió del tablero, de los ajustes y de las alertas. "FLUJO n/4"
  queda como cuenta descriptiva.
- **La celda violeta no es señal.** El 56,7 % a una vela (n 247) es real pero no sobrevive: contra el placebo por
  franja horaria z +1,8, el intervalo de confianza incluye el cero, en la segunda mitad de la muestra cae a 53 % y de
  noche, con 4,6 veces más casos, da 47,8 % (por debajo de su base). Es un ESTADO ("el precio le ganó al flujo").
- **El brillo no dice nada de lo que sigue**: ni dirección (47,6 % brillantes contra 48,2 % tenues) ni rango, una vez
  descontado el tamaño de la propia vela. Dice cuánto delta tuvo ESTA vela, nada más.
- **"Coincide 96,8 %" es por construcción.** El número con contenido es cuánto violeta y cuánto gris hay.

**Lo que se arregló (1.5), todo medido en las 20 ruedas:**
- **La apertura salía toda brillante**: a las 13:30 UTC el 52 % de las celdas tenía lugar >= 0,8 (parejo sería 20 %),
  porque se comparaban con el premercado. Ahora el |delta| (y las operaciones) se dividen antes por **lo normal de esa
  hora** (mediana de la misma franja ±15 min de las 5 sesiones anteriores, causal): 13:30 -> 25 %, 20:00 6 -> 15 %,
  desvío entre las 46 franjas 7,2 -> 2,8 puntos.
- **La vela más grande salía con la confluencia gris o al mínimo** (el CVD de 20 velas le restaba): 35 % de las velas
  grandes. Ahora **la vela vota** -> 3,8 %. Se probó pasar el CVD a 10 velas: empeoraba (descartado). Se probó fundir
  "delta" y "dónde cerró el delta" en una sola lectura (coinciden 97 %): dejaba flojas al 35 % de las grandes; quedan
  separadas a sabiendas: hacen que el delta de la vela pese doble cuando además cerró de ese lado.
- **Violeta sin causa** (la vela con delta a favor pero violeta por culpa del fondo): 3,5 % -> 0. El violeta de la
  confluencia exige que el delta de ESA vela o los grandes vayan contra ella. Confluencia: coincide 97,2 %, violeta 2,8 %.
- **El gris tenía dos significados y ocupaba 1 de cada 4 celdas**: ahora es solo la vela de indecisión (~15 %) y más
  visible (0,45 -> 0,60).
- **"Muchos negros" en las tres filas de abajo (43 % del panel)**: la cinta pasa a percentil (prende el 30 % más
  rápido; antes se veía el 17,5 %), grandes reparte su brillo por percentil (antes 2 de cada 3 clavadas al mínimo) y
  las celdas quietas llevan un **fondo tenue**: se lee "tranquila", no "rota". Se evaluó un mapa de calor continuo:
  prende el 100 % y mata la alarma (descartado).
- **Vela en curso**: el factor de proyección saltaba de 1 a 3 en el segundo 5 y disparaba violetas tempranos (5,6 % de
  las velas; solo el 9 % llegaba al cierre). Ahora la proyección entra gradual (3 a 10 s) y se usa SOLO para el brillo:
  violeta, lecturas y absorción se deciden con el delta sin proyectar. Sin operaciones la celda quedaba congelada:
  se rederiva una vez por segundo con el reloj.
- El texto "DELTA 2v xσ" contradecía a la celda en el 10 % de las velas (era otra cuenta): ahora dice el delta de la
  vela y su percentil ("DELTA -145 p69"), lo mismo que pinta la celda.
- Código: el volcado retenía la llave 81 ms y pedía 29 MB por vela cerrada y por gráfico: ahora se arma fuera de la
  llave, una sola vez por carga, y después agrega una línea por vela en una cola ordenada. Un grande que llega con su
  vela ya cerrada rehace esa vela. Las marcas graban versión, lugar y tonos. Piso absoluto de 20 contratos (no cambia
  ninguna celda de rueda; de noche, el 1 %).

**Control:** paridad C#/Python (`laboratorio/cvd_regla15.py`) 0 diferencias en 27.243 velas y 9 columnas. En
pantalla (control por píxeles, 58 velas con cuerpo): confluencia 53 del color de la vela + 3 violetas, presión 54 + 2;
0 contrarias, 0 negras.

**Lo que hay que saber al leerla:** la celda con borde (vela en curso) puede cambiar de color hasta el cierre en al
menos 1 de cada 7 velas: es la vela misma cambiando. De noche hay el doble de violeta (mercado fino), no es señal.
Falta medir el "tarde" DENTRO de la vela: para eso habría que grabar el tono a los 15, 30 y 45 s (no se graba todavía).

## Pendiente

- Cinta de **presión del libro** (desbalance del mejor bid/ask, `OnBestBidAskChanged`): en la literatura explica
  el doble que el delta en el mismo intervalo (65 % contra 32 %, acciones 2010, contemporáneo, no predictivo).
  Se agrega midiendo primero.
- Medir las marcas grabadas contra tres placebos (azar, gemelo de precio, misma hora) con 25+ casos por tipo.
