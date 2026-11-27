# La Ola en ES y en MES a la vez: la divergencia del color, contra las cintas (27-11-2026, media rueda)

Lo que vio el operador a las 13:46:31 AR, con los dos gráficos lado a lado: ES "MAREA ▲ sin agresores Δ −2.640 · faltan 36,75 /
OLA ▲ sin agresores Δ −399 · faltan 6,00" (fondo ámbar) y MES "MAREA ▲ con compradores Δ +6.553 · faltan 35,75 / OLA ▲ con
compradores Δ +976 · faltan 5,75" (fondo verde). Cintas de la sonda: `cinta-ESZ6-202611262300-202611271649.csv` (239.641 órdenes) y
`cinta-MES-202611262300-202611271648.csv` (323.222); para la mediana del MES se pidieron además a la sonda las cintas MES del 12 y
13-11 (las otras 8 ya estaban).

## 1. Los dos paneles de la captura son la cinta, al milisegundo
`salida_instante_dos_paneles.txt`: la Máquina de referencia (`herramientas/test_ola.py`) da EXACTO lo de la captura en la orden
**238.231 del ES** y en la **322.352 del MES**, las dos a las **13:46:31.075 AR**: marea (dirección, delta, faltan, raya), ola (ídem) y
el delta acumulado de la jornada = el "total" del Superindicador (**−3.491** y **+8.068**).

## 2. Auditoría completa de los dos indicadores
- **ES** (`salida_auditar_pythiaola_ES.txt`): 50 pivotes, 1.068 fotos cerradas, 139 minutos de clima y la mediana 126,625: 0
  diferencias. Las 16 "difs" son todas de la foto en curso (16:49:10 UTC), posterior al corte de la cinta (falsa conocida).
- **MES** (`salida_auditar_pythiaola_MES.txt`): mediana de 10 ruedas recalculada **126,50 = la del indicador**; las fotos 1 a 1.067,
  todos los pivotes cubiertos y los minutos de clima 0 a 137: 0 diferencias. Las 224 "difs" son todas de lo posterior al corte
  (fotos 1.068-1.076, pivotes confirmados después de las 16:48 UTC, minutos de clima 138-145).
- **Panel MES** (`salida_auditar_panel_MES.txt`): todas las líneas cubiertas por la cinta, **0 distintos**.
- **Panel ES** (`salida_auditar_panel_ES.txt`): 92 distintos, todos entre 15:22 y 16:36 UTC: ver la sección 4.
- Captura propia pasiva (sin foco ni mouse) a las 13:57:54 AR: `captura_1357_cuentas_tapadas.png` (cuentas y conexiones tapadas).

## 3. La divergencia del color es del mercado, no del indicador (`salida_divergencia_es_mes.txt`)
- Misma pierna de marea en los dos: origen **7.222,75 a las 12:05:21 AR**, extremo 7.297,50 a las 13:45:23. En los 137 minutos de la
  rueda la dirección de la marea coincidió el **100 %** y el color el **70 %** (difieren 11:30-11:34 y 13:11-13:46 AR).
- Delta de esa pierna hasta las 13:48 AR: **ES −2.970** (compras agresoras 125.734, ventas 128.704). Por tamaño de la orden agresora:
  1-9 contratos **+2.355**, 10-19 **−4.234**, 20-49 **−1.058**, 50+ −33. **MES +6.441** micro (= +644 equivalentes ES), positivo en
  todos los tamaños salvo 200+ (−345).
- Minuto a minuto los dos deltas van juntos (correlación 0,61), pero desde las 12:47 AR el ES acumuló −4.537 y el MES −122 eq ES.
  El delta de la pierna del ES cruzó a negativo a las **13:11 AR**: desde ahí, ámbar en ES y verde en MES.
- Mecánica (lo medido, no un pronóstico): en ES la última hora de suba se hizo con más ventas agresoras que compras, sobre todo de
  órdenes de 10 a 49 contratos; el precio subió igual porque compradores con órdenes límite absorbieron esas ventas y/o la oferta de
  arriba se retiró. La ronda 17 midió que eso **no frena la pierna** (siguió X/2 a favor 78-87 % contra 82-88 % de un segundo
  cualquiera). Hoy, a las 13:53:51 AR, hizo máximo nuevo 7.307,75 con el ES todavía en ámbar: un caso, no una prueba.
- Si cruzar ES con MES agrega información se mide aparte, pre-registrado: `laboratorio/ronda26_es_mes/`.

## 4. Hallazgo: los cortes de Rithmic le hacen perder órdenes en vivo
- Registro de ATAS: se cortaron **las dos conexiones de datos a la vez** a las 12:47:17-19 AR, una de nuevo a las 12:50:50, y las dos
  a las 13:06:17-19 (latencia de 8,8 s de mediana y 16 s de pico). Entre 11:30 y 11:59 hubo latencias de hasta 117 s.
- Hasta las **12:46:53.865 AR** La Ola ES tenía exactamente las mismas órdenes que la cinta (197.772). A las 12:48:09 le faltaban
  **533**; a las 12:52:09, 760; a las 13:36:06, **965**. Las órdenes del pico de las 12:46:54-57 (hasta 7.293,50) nunca le llegaron:
  el panel tomó el techo en **7.292,00** y las rayas y deltas quedaron corridos (rayas 1,50 pts) hasta las 13:37.
- A las 13:37:58 el gráfico se recargó (cuando se agregó La Ola al MES), pidió la jornada entera al historial de ATAS y quedó igual a
  la cinta. Por eso el csv de hoy audita con 0 diferencias: se reescribió con el historial. El MES entró a las 13:36: no pasó por
  ningún corte.
- Arreglo propuesto (no hecho): que La Ola detecte el hueco de cada reconexión y vuelva a pedir ese tramo al historial para coserlo.

## 5. La X distinta entre ES y MES (parámetro, no mercado)
- Medianas de 10 ruedas: ES 126,625 y MES 126,50 (los extremos de cada libro no coinciden al tick todos los días). 0,08 × mediana da
  10,13 y 10,12; redondeado al tick queda **10,25 y 10,00** (el límite está en 126,5625). Marea 41 contra 40.
- Hoy eso le dio al MES dos giros de ola de más (12:01-12:03 y 12:15-12:16 AR) y 3 minutos con la ola en dirección distinta. Con la
  X del ES (10,25), los pivotes del MES caen en los mismos minutos que los del ES, a 1-2 ticks de precio.
- Arreglo posible: "X fija en puntos" = 10,25 en el gráfico del MES (ajuste "2. Escala de la ola"), o que la X del S&P salga del ES.

## 6. Gráficos que dejan de pintarse: ATAS tapado por otra ventana
- 14:05:39 AR, alarma del vigía: el registro de dibujo (auditoria-*.jsonl) de los DOS gráficos se cortó a la vez (últimas líneas
  14:02:48 ES y 14:02:49 MES) mientras el csv de La Ola seguía al día; la cámara pasiva (PrintWindow cada 30 s) devolvió fotos
  idénticas desde 14:03:32.
- Geometría en ese momento: un solo monitor (0,0,1600,900); ventana al frente = Claude, maximizada (−8,−8,1608,860); debajo, ATAS
  maximizada en el mismo rectángulo: ATAS completamente tapada. Operador activo (0-2 s sin tocar nada): me estaba escribiendo.
- Los huecos del registro de dibujo de hoy (13:41-13:44, 13:57-14:00, 14:00-14:02 AR) caen cuando el operador me escribía.
- Lectura: ATAS no pinta los gráficos que no se ven (ahorra trabajo); La Ola calcula igual (el csv no se corta) y al volver a ATAS
  repinta. Consecuencia para las herramientas: el registro de dibujo y la foto pasiva solo valen con ATAS a la vista.

## 7. La Ola 1.0b en vivo (14:44 AR): re-pide sola tras un corte y recarga el gráfico sola
- Instalada en caliente a las 14:44 y 14:46-14:48 (1.0.2 → 1.0.4). El primer intento (14:30, ATAS minimizado) apretó por error la flecha
  izquierda del scroll del gráfico MES (sin órdenes ni paneles cerrados); los instaladores ahora identifican el botón de recarga por
  DIFERENCIA (el que aparece nuevo en la esquina tras copiar el DLL) y no aprietan nada si hay duda.
- Detector de cortes probado con el log real del día en el arnés (`_test_pythiaola cortes`): re-pide en los 5 cortes (04:00, 12:47,
  12:50, 13:06 y 14:07 AR) y en ningún otro momento.
- Recarga del gráfico por dentro (prueba por archivo `recargar-ESZ6.txt`): La Ola encontró el ReloadDataCommand del panel 'ESZ6 1m Chart'
  (y distinguió el de 'MES 1m Chart'), lo ejecutó y ATAS recargó el gráfico ("blank for 4920 ms", 14:48:58); el cuadro quedó con la nota
  "corte de datos 14:48: gráfico recargado solo" (se ve en `foto_145209_cuentas_tapadas.png`).

## 8. Lo mostrado en pantalla contra la cinta, de 14:00 a 14:55 AR (cintas hasta 17:55 UTC)
- Fotos por minuto (csv) contra la cinta: ES 1.134 fotos y 53 pivotes, MES 1.134 fotos y 57 pivotes: 0 diferencias en lo cubierto (solo la
  foto en curso, posterior al corte de la cinta); clima 205 minutos cada uno: 0 diferencias; medianas 126,625 y 126,50 IGUALES.
- Lo que MOSTRÓ el panel (jsonl) contra la cinta en el mismo instante: ES 218 líneas iguales y 114 distintas; MES 86 iguales y 16 distintas.
  Las distintas caen TODAS en los cortes de Rithmic (atraso de la orden n de 20 a 115 s): ES 12:48-13:36 (cortes 12:47/12:50/13:06, hasta la
  recarga de 13:37) y ES y MES 14:08-14:17 (corte 14:07:40, hasta el "Reload all historical data" del operador a las 14:18). Más 5 líneas
  sueltas de ES con atraso 0,1 s donde solo el delta difiere en 1 a 5 contratos: la última orden se estaba llenando cuando se tomó la línea
  (la cinta la guarda completa).
- Foto de la cámara de las 14:52:09 (la pantalla mostraba las 14:51:47): los dos cuadros = cinta al milisegundo (ES orden 272.564 de las
  14:51:47.238: marea ▲ sin agresores −5.645 faltan 19,00, ola ▼ con vendedores −2.447 faltan 6,25, rayas 7.292,00 y 7.266,75; MES orden
  359.143 de las 14:51:47.678: marea ▲ con compradores +5.604 faltan 18,00, ola ▼ con vendedores −1.310 faltan 5,25, rayas 7.291,00 y
  7.267,75). Total del Superindicador: MES +7.119 = cinta; ES −6.497 contra −6.496 (la orden siguiente, una venta de 6, tomada con 1).

## 9. La jornada entera (cintas completas pedidas a las 15:17 AR, tras el cierre de 13:15 NY)
- `cinta-ESZ6-202611262300-202611272200.csv` (293.536 órdenes; enlazada como `cinta-ES-...` para el juez de la R21) y
  `cinta-MES-202611262300-202611272200.csv` (373.020 órdenes).
- ES: 58 pivotes, 2.310 fotos (1.154 tomadas), 44.296 campos, 224 minutos de clima y la mediana: TODO IGUAL (la única marca es
  informativa: 1 actualización vieja que llegó después de la recarga de prueba de 14:48:53). MES: 62 pivotes, 2.310 fotos, 44.324 campos,
  224 minutos de clima y la mediana: TODO IGUAL.
- Lo que MOSTRÓ el panel: ES 252 líneas iguales y 116 distintas, MES 122 y 16. Las distintas son las de los cortes (sección 8) más 6 líneas
  sueltas de ES con 1-5 contratos de delta (la última orden llenándose; la última, 15:11 AR, +2.852 contra +2.851). Desde la 1.0b
  (14:44) no hubo cortes (log de ATAS: 0 líneas de corte), así que el arreglo automático solo se probó con la prueba por archivo.
