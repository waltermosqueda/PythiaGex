---
name: flujo-claro-cvd-superador
description: "Flujo Claro (17-09): indicador propio que reemplaza al CVD de fabrica: cinco cintas, CVD anclado con presion, marcas en el precio y el AHORA en fila; lo medido (el delta no adelanta, extremo = tarde), como se instalo y las trampas de ATAS que aparecieron."
metadata: 
  node_type: memory
  type: project
  originSessionId: 9345174c-7f69-4fe5-a793-976e41f2dc7c
  modified: 2026-09-17T16:47:46.235Z
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
