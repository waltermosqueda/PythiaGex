---
name: flujo-claro-cvd-superador
description: "Flujo Claro (17-09, v1.5): indicador propio que reemplaza al CVD de fabrica; la celda habla de la vela que tiene encima (verde/rojo acompaña, violeta contra, gris doji); medido: nada anticipa (ni extremo, ni violeta, ni brillo); volcado de velas, paridad C#/Python y trampas de ATAS."
metadata: 
  node_type: memory
  type: project
  originSessionId: 9345174c-7f69-4fe5-a793-976e41f2dc7c
  modified: 2026-09-17T18:39:56.631Z
---

Pedido del operador (17-09): "el CVD de ATAS abajo del grafico es chiquito, apenas se entiende, y siento que me avisa
tarde; construime algo superador, investiga a fondo y dame previews". Eligio la Fusion 1 de las maquetas con las
cinco cintas, y despues pidio el AHORA "en fila, que el bloque nos roba espacio". Quedo como indicador aparte en el
mismo DLL: `atas/PythiaGexNiveles/FlujoClaro.cs` ("PythiaGex - Flujo Claro"), doc en `FlujoClaro.md`, maquetas en
`conocimiento/cvd-superador/`, banco en `laboratorio/cvd_superador.py` y `cvd_fusion.py`. Instalado y visto en
pantalla 13:45 en el MNQZ6 1m (version 1.1), sin excepciones.

**Lo medido (velas propias de 1 y 2 min, 08 al 17-09), que manda sobre el diseño:**
- El CVD de fabrica ya se mueve dentro de la vela: lo que llega tarde son los PIXELES. Estaba en modo Bars con escala
  desde cero (0 a 12K en 140 px = 86 contratos por pixel). Anclado a 60 velas se lee 3-4 veces mas alto.
- Delta y precio se mueven JUNTOS: correlacion +0,69 a +0,82 en la misma vela, -0,03 a -0,09 con la siguiente.
  Ninguna lectura de flujo adelanta (coincide con Cont-Kukanov-Stoikov 2014 y con [[gatillos-order-flow-banda]]).
- Ninguna señal de vela le gano al azar. Confluencia 4 de 4: a favor 40 % (n 96, z -1,4) => el tablero dice
  "NO PERSEGUIR", nunca "compra". Absorcion: 46 % (n 24), sin evidencia; un 57 % anterior era un artefacto de un
  percentil global que miraba adelante.
- Cada marca en vivo se graba en `%APPDATA%\ATAS\PythiaGex\flujo\marcas-<inst>-<dia>.jsonl` para medirla.

- **Calibracion de la cinta de presion (1.2 -> 1.3, 17-09):** el operador vio que 'avisaba tarde' y tenia razon: la suma
  de 5 velas giraba 3-4 velas despues del pivote. La regla 'eficaz sostenida' (color de la vela solo si el precio
  acompaña al delta, sostenida una vela) gira a 1 vela: firme en 16 de 16 dias y 27 definiciones de giro; contra su
  propio placebo la ventaja real es ~0,5-0,6 velas. **Dos verificadores me hicieron RETIRAR dos frases que le habia
  dicho al operador:** 'acuerda con la vela 68-78 %' era circular (con la vela SIGUIENTE da 48-51 %, igual que la
  vieja: NO anticipa) y 'parpadea menos' era falso en absoluto (17,5 cambios de color por hora contra 10,8, ~60 % mas;
  solo bajo la proporcion de fugaces). Casi toda la mejora viene de mirar 1 vela en vez de 5, no de la regla fina.
  1.3: celda de la vela en curso HUECA (a mitad de vela el color difiere del cierre en 33 % de las velas, 10 % al
  contrario), sostenida a media luz e indecision casi negra, confluencia con cuarta lectura independiente (donde cerro
  el delta en su recorrido; antes presion y delta/volumen coincidian 95-99 %), texto 'DELTA 2v'. Extremo remedido:
  >=3 lecturas 41 % a favor (n 173, z -1,4), las 4: 36 % (n 86, z -1,8) => sigue 'NO PERSEGUIR', sin ser prueba.
  **Regla CONGELADA:** muestra chica (~950 velas MNQ M1, casi todo U6); juzgar con las proximas 5 sesiones completas
  de MNQZ6 sin retocar. Abierto: la hora posterior al cierre queda casi ciega (sd de 60 velas), sin arreglo validado.
  Leccion de metodo: una metrica que compara la cinta con la MISMA vela que la define es circular; medir siempre
  contra la vela siguiente y contra el placebo propio (deltas barajados).

- **1.4 (17-09 15:30), tercera queja del operador: "avisa tarde / el color no corresponde con la vela / muchos negros
  huecos".** Tenia razon OTRA VEZ: medido con SU criterio (`laboratorio/cvd_coherencia.py`), la 1.3 dejaba APAGADO el 40 %
  de las velas con cuerpo y la confluencia iba del color CONTRARIO en el 8 %. Causa: el delta se media en sigmas de 60 velas
  y una vela enorme cegaba todo lo demas. Gramatica nueva: **la celda habla de la vela que tiene encima** — verde/rojo = el
  flujo acompaña (brillo = percentil del |delta| en la ultima hora), VIOLETA = el flujo va contra la vela, GRIS = doji o
  flujo parejo, negro = nada que medir. Presion coincide 96,8 % / violeta 3,2 % / contrario 0 / apagado 0 (7.530 velas de
  rueda, 20 dias). Es coincidencia POR CONSTRUCCION, no prediccion (vela siguiente 48 %); se lo dije asi. Leccion: cuando el
  operador dice "no corresponde", medir con SU criterio (lo que el ojo ve: color, apagado, contrario) antes que con el mio
  (parpadeo, retraso); y normalizar por PERCENTIL, nunca por sigma, en series con rafagas.
- **1.5 (17-09 16:06), tres revisores sobre la 1.4 (codigo / estadistico / ojo del scalper):** (a) **RETIRADO 'NO PERSEGUIR'**:
  con 20 ruedas da 50 % (n 2.716) y estaba prendido el 36-40 % del tiempo; el 36-41 % que le habia dicho al operador era ruido
  de 950 velas. La violeta NO es señal (56,7 % a 1 vela no sobrevive al placebo por franja ni a la replica nocturna) y el brillo
  no anticipa nada. (b) El percentil de 60 velas se SATURABA en la apertura (52 % de celdas brillantes a las 13:30 UTC): ahora se
  divide antes por lo normal de esa hora (mediana de la franja +-15 min de las 5 sesiones previas) -> 25 %. (c) Confluencia: la
  vela VOTA (vela grande con celda floja 35 % -> 3,8 %), violeta solo con causa propia, gris solo doji. (d) Filas de abajo por
  percentil y con fondo tenue en las celdas quietas (43 % del panel era negro). (e) Vela en curso: proyeccion gradual y solo
  para el brillo; se rederiva con el reloj. (f) Texto 'DELTA -145 p69'. La regla vive UNA vez en `laboratorio/cvd_regla15.py`
  y la paridad con el C# es 0 diferencias en 27.243 velas x 9 columnas. **Leccion:** toda 'señal' medida con menos de ~1.000
  casos se vuelve a medir cuando hay muestra grande ANTES de dejarla escrita en el tablero; el volcado da esa muestra gratis.
- **El volcado:** Flujo Claro escribe todas las velas del grafico (crudas + estados) en
  `%APPDATA%\ATAS\PythiaGex\flujo\velas-<inst>-<marco>.csv` (27 mil velas de 1 min = 20 ruedas): muestra grande para
  cualquier banco, y paridad C#/Python vela por vela (0 diferencias). Control VISUAL automatico de la captura:
  `herramientas/ver_celdas.py captura.png x0 x1 yPrecio0 yPrecio1 "yFila1,yFila2,yFila3" nombres` (color de la vela contra color de la celda, por pixeles).

**Trampas de ATAS que salieron (utiles para cualquier indicador de panel):**
- `GetXByBar(bar, false)` devuelve el CENTRO de la vela; `true` el borde izquierdo.
- `Container.Region` de un panel propio ya viene SIN el eje de precios (el margen de ~64 px vale para `ChartArea`).
- ATAS no recorta el dibujo de un indicador de panel: puede marcar sobre las velas (`ChartInfo.PriceChartContainer`),
  pero hay que recortar a mano (`g.SetClip(reg)` para el panel, `cont.Region` para las marcas).
- ATAS escribe el nombre del indicador arriba a la izquierda del panel (~15 px x ~250 px): ese renglon sirve para
  poner una fila de datos a la derecha sin gastar alto.
- Cambiar un ajuste NO recalcula la historia: los setters de calculo tienen que llamar `RecalculateValues()`.
- `RequestForCumulativeTrades(new CumulativeTradesRequest(desde, hasta, minVol, 0))` + `OnCumulativeTradesResponse`
  trae las operaciones grandes historicas (3.000 en 2 dias de MNQ con umbral 50); validar que cada operacion caiga
  DENTRO de su vela (guardar `LastTime`) y descartar la respuesta si llega en medio de un recalculo.
- Agregar un indicador por UI: boton Indicators (id `BarButtonItemLinkIndicatorsButton`) -> buscador (`PART_Editor`,
  ValuePattern) -> UN clic en el resultado -> `Add to chart` -> `Apply`. El panel nuevo nace de ~55 px: arrastrar el
  separador de ARRIBA (precio / panel) hacia arriba; arrastrar el de abajo le da el espacio al precio.
- Al agrandar el panel nuevo el CVD de fabrica del operador quedo aplastado/oculto: avisarle; es suyo borrarlo.

**Why:** el operador scalpea en 1 minuto y quiere leer presion, absorcion y divergencia de un vistazo sin agrandar
paneles; y el proyecto no afirma nada sin medir.

**How to apply:** antes de prometer "avisa antes", releer los numeros de arriba. Proximo paso acordado: cinta de
presion del LIBRO (`OnBestBidAskChanged`), midiendo primero; y medir las marcas grabadas con 25+ casos por tipo.
Ver [[auditoria-2026-09-16-0dte-pelotitas-carga]] (profundidad de opciones acotada a 24 contratos, 1.11b).

**1.8 (18-09, madrugada): TABLERO AHORA.** El operador pidio sacar de la fila las celdas 5s/15s/60s/5m y FLUJO n/4 ("no las uso") y poner en el
hueco derecho del panel algo que diga "si estamos en short o long" con delta + momentum + gamma. Se investigo (inventario + laboratorio + afuera), se
le mostraron 3 previews (velocimetros / termometros / flechas) y eligio los VELOCIMETROS. Quedo `FlujoClaroTablero.cs`: FLUJO 60s y TENDENCIA con
aguja; GAMMA (frenan / sin lectura / empujan, de la estela de Gamma Hoy, guardia de libro flaco > 150 pts o > 150 s) y VELOCIDAD (5 escalones por
rango de 5 min, tabla de techo_ml.md 210-214) sin aguja y SIN VOTO; resumen en palabras de FLUJO (COMPRAN FUERTE / compran / PAREJO / venden /
VENDEN FUERTE), nunca LARGO/CORTO, el minuto pesa doble, "a favor n/5", cabecera fija "AHORA estado, no pronostico". Geometria adaptable: 286x123 en
fila si hay lugar a la derecha de la vela, o dos filas de dos (150-200 px) si no (el grafico de 5 min deja ~190 px). Cada cambio de estado se graba en
flujo/tablero-<inst>-<marco>-<dia>.jsonl (~850 por rueda). **Medido antes de instalar (tablero_01_placebo.py, 20 ruedas):** describe el ultimo minuto
el 76-79 % (92-94 % en los fuertes) y NO anticipa (+-8 a 600 s: 50,3 % explorar / 47,8 % confirmar contra placebos 48-52, empate 56 %). Frase para el:
"espejo retrovisor prolijo, no parabrisas: sirve para no operar EN CONTRA de lo que pasa, nunca como razon para entrar". Trampa de UI: las
propiedades nuevas con nombre nuevo (FcTablero); el ancho libre a la derecha de la vela depende del offset del grafico del operador.

