# CHANGELOG — PythiaGex 5.0 (Gamma ES)

## 5.0.4 (27-10-2026) — estilo Gammalito con el libro ES de Rithmic: barras a la derecha, amarillos y zero (compilada; SIN instalar)
- Pedido del operador (textual): "agregá el perfil estilo Gammalito también con el libro de ES de Rithmic (barras, amarillos y zero), en vivo las 24
  horas". La MISMA cuenta de la 5.0.3 (`GammalitoFam`) sobre el LibroMinuto ES de la 5.0 (opciones sobre el futuro ES de CME por Rithmic, una foto por
  minuto, sin el atraso de CBOE): valor de un strike = `Gv` del libro ES (gamma Black-76 sin descuento x (vol calls - vol puts), el vencimiento del
  horizonte Hoy de ESE libro) x 0,5 (50 USD por punto) / 1e6, en M USD por 1 %; precio = K + corrimiento de contrato de la 5.0 (medido 0 en los 380
  minutos: libro ESZ6, grafico MESZ6).
- **Barras ES** (casilla nueva "GL barras ES", Order 104, PRENDIDA): `PerfilGl.BarrasEs` (+ `MaxEs`, `ConvEs`, `DatoEs`; Contratos.cs), ANCLADAS A LA
  DERECHA: terminan en `colIzq`, el borde izquierdo de la columna de la derecha (etiquetas, renglones ↑/↓ y el renglon de arriba con la edad y el
  recuadro: el mismo borde que ya respetaban los rotulos de tramos) y crecen hacia la izquierda; la mas larga = 25 % del ancho visible (`GL_ANCHO_ES`),
  mismo alto (0,6 del paso de 5 pts, 3-11 px) y colores que las de SPX (#089981 / #F23645 al 65 %). Choques, resueltos asi: la columna no se tapa ni
  se corre, y el recuadro y la edad tampoco (son parte de ese borde); si la columna se ensancha (rebobinado "◀ …", "MOTOR PARADO …") las barras se
  corren con ella; sin columna (etiquetas apagadas o el grafico corrido hacia atras) terminan en el borde derecho visible; si el grafico es tan angosto
  que una barra llegaria al borde izquierdo, se corta ahi. Como `colIzq` recien se sabe despues de las etiquetas, sus rellenos se INSERTAN en la lista
  de primitivas justo despues de las barras de SPX/SPY: quedan debajo de la estela, las etiquetas y el eje, como esas. Como las de SPX/SPY, son el
  perfil del ultimo minuto calculado y no se rebobinan. Un minuto con SOLO el libro ES ahora publica perfil (`Barras` vacio): la izquierda se ve igual.
- **Series extra nuevas** AL FINAL de `CatalogoFamilia.Extra` (indices 9-10; 0-8 sin cambio): `GL_ES_dom` (las 2 barras de mayor |valor| del libro
  ES: D1 amarillo opaco, D2 al 50 %) y `GL_ES_zero` (cruce de signo interpolado mas cercano a S = fut - corrimiento, gris, 1 px); casillas "GL amar.
  ES" / "GL zero ES" (Order 105-106), PRENDIDAS (nombres nuevos); etiquetas "GL ES D1 7,42B", "GL ES 0Γ". Se calculan y se guardan en el niv cada
  minuto; la estela, como la de todas las series, va una rayita por vela de 2 min.
- **Probado** (arnes P9 = el P8 de la 5.0.3 con los datos copiados de %APPDATA% a las 21:20Z; DLL 5.0.3 contra 5.0.4, 13:01-21:00Z de a un minuto):
  (a) las 1.378 lineas del niv sin las GL_ES IDENTICAS byte a byte a la 5.0.3 (Calc y las 9 extras previas, GL SPX/SPY incluidas); la foto final
  (historia m2, actuales y fuentes sin las GL_ES) identica; 42 de 42 pantallas con las barras ES apagadas con las MISMAS primitivas; (b) recalculo
  independiente en Python desde viva3 crudo (la copia es prefijo byte a byte del archivo de %APPDATA%), 380 minutos con libro ES (320 de dia,
  14:33-19:59Z, con el 0DTE del 27-10; 60 de noche, 20:00-20:59Z, con el vencimiento del 28-10 20:00Z): 16.684 barras con diferencia relativa maxima
  2e-13, amarillos 380/380 y zero 380/380 iguales al redondeo de 2 decimales; (c) pantalla (ArmadoPantalla sin ATAS, 42 minutos 16:30-16:50Z y
  20:10-20:30Z, 1350x660, 9,7 px/pt): 578 barras ES recalculadas por fuera (color, x0, y, largo, alto y lugar en la lista de primitivas) sin
  diferencias; el borde (x = 1185-1190) es el de la columna recalculado desde lo dibujado y ninguna barra pisa una etiqueta, la edad ni el recuadro;
  sin etiquetas, 42 minutos con el borde en x = 1350 sin diferencias; 126 etiquetas GL ES con precio y monto recalculados (34 nombradas dentro de otra
  del mismo precio, p. ej. "ES C 9,21B · GL ES D1").
- **Ojo**: (1) "de noche" en estos datos es solo 20:00-21:00Z (16:00-17:00 NY, con el volumen del dia de la serie del 28-10): la primera noche de
  verdad (sesion 28-10 desde las 22:00Z, volumen desde cero) todavia no esta en el archivo; (2) la cadena de Rithmic trae todos los strikes hasta el 1 %
  y uno de cada dos hasta el 2 % (medido: mediana 45 barras por minuto, de -149 a +145,5 pts del precio): mas lejos no hay barras; (3) un minuto con
  SOLO el libro ES (sin ninguna extra de CBOE) pasa a tener `TieneExtra` y su vela m2 ya no toma las extras del minuto anterior (lo mismo que ya
  hacian las GL de SPX/SPY en la 5.0.3): en los datos del 27-10, 0 minutos asi.
- **Falta**: instalar y la comprobacion en la pantalla real de ATAS (regla 09-10). SIN MEDIR EN ES como nivel.
- Version: FileVersion 5.0.4.0, InformationalVersion 5.0.4, `VERSION` "5.0.4"; AssemblyVersion queda 5.0.0.0.

## 5.0.3 (27-10-2026) — estilo Gammalito: barras por strike, amarillos y zero (compilada; SIN instalar)
- Pedido del operador (textual): "agregá a la 5.0 las barras por strike al estilo Gammalito (izquierda gamma x volumen neto de SPX y SPY, amarillos y
  zero), bien convertidas a ES". Formula del informe `_referencia/2026-10-27-gammalito-ES/INFORME.md` (ingenieria inversa de 10 capturas): valor de un
  strike = gamma Black-Scholes x (vol calls - vol puts), cada lado con su IV = `Gv` del libro del minuto de la 5.0 (Perfil.cs). Modulo nuevo
  `_modulos/familia/gammalito/GammalitoFam.cs`.
- **Barras** (casillas nuevas "GL barras SPX" / "GL barras SPY", Order 99-100): SPX 0DTE en K + base C7 del minuto y SPY en K x razon C8 (los mismos
  precios que MUROS_SPX_vol / MUROS_SPY_vol: 15.481 de 15.481 comparaciones iguales), pegadas al borde izquierdo, verde #089981 / rojo #F23645 al 65 %,
  la mas larga de cada libro = 30 % del ancho, alto 11 px (medido 10-11 px en las capturas a 9,7 y ~32 px/pt; tope 0,6 del paso de 5 pts de SPX, minimo
  3), SPY debajo y SPX encima, solo las que caen en pantalla. El motor arma el perfil del ultimo minuto con libros (`FotoFamilia.PerfilGl`, tipos nuevos
  `PerfilGl`/`BarraGl` en Contratos.cs) y la pantalla solo pone coordenadas. Sin barras hasta el primer minuto calculado despues de abrir ATAS (un minuto
  del archivo no trae los libros); no se rebobinan.
- **Series extra nuevas** AL FINAL de `CatalogoFamilia.Extra` (tipo nuevo "GL", sin cambio ▲▼), casillas "GL amar. SPX", "GL amar. SPY", "GL zero SPX"
  (Order 101-103), todas PRENDIDAS (nombres nuevos): `GL_SPX_dom` / `GL_SPY_dom` = las 2 barras de mayor |valor| de cada libro (D1 amarillo #FFD700
  opaco, D2 al 50 % en la estela); `GL_SPX_zero` = cruce de signo interpolado del perfil SPX, el mas cercano al precio (gris #D1D4DC, 1 px). Se guardan
  en el niv con sus ids; etiquetas "GL SPX D1 95,0B", "GL SPX 0Γ"; en un grupo que no encabezan van nombradas (" · GL SPX D1"). Sin cambiar ningun
  default ni nombre existente.
- **Probado** (arnes P8: el motor entero con los datos reales del 27-10, DLL 5.0.2 contra 5.0.3, misma secuencia de pasos): (a) las 1.319 lineas del niv
  sin las GL y la foto final (historia m2, actuales y fuentes sin las GL) IDENTICAS byte a byte a la 5.0.2; (b) recalculo independiente en Python desde la
  cadena cruda, 250 minutos 13:01-17:10Z: 34.598 barras con error 0, zero 250/250, amarillos SPY 250/250 y SPX 250/250 (20 con 0,01 pt de diferencia de
  redondeo del precio); (c) contra la captura de Gammalito de las 12:33 NY, a la misma hora (16:30-16:35Z): R2 0,987-0,991 y 14/14 colores en SPX,
  0,977-0,983 y 7/7 en SPY (la foto del MISMO dato que Gammalito, que la 5.0 recien tiene a las 16:49Z, da 0,876: la gamma al precio y la hora del minuto
  pesa mas que los 15 min de volumen); (d) contra las capturas por minuto (192 min 09:30-12:42 NY, a la misma hora): amarillos 80 % de los visibles a
  <= 0,9 pt (brillo D1/D2 59 %; la regla con el dato en vivo daba 92 %); zero mediana 1,55 pts, 8 % a <= 0,5 pt (con la misma foto de CBOE: 0,51 pt):
  el zero llega 15 min tarde; (e) la pantalla (ArmadoPantalla sin ATAS, 21 minutos 16:30-16:50Z): 441 barras recalculadas por fuera (color, x, largo,
  alto, y) iguales a lo dibujado, primero las barras y SPY antes que SPX; estela GL D1 alfa 255, D2 128, zero 1 px; las demas series sin cambios.
- **Falta**: instalar y la comprobacion en la pantalla real de ATAS (regla 09-10); el auditor (`herramientas/auditar_familia.py`) no tiene las GL en su
  catalogo (no las audita); los amarillos y el zero se calculan tambien fuera de la rueda (como las demas series por volumen: de noche con la cadena
  congelada y su edad en naranja; Gammalito empieza a las 09:30 NY). SIN MEDIR EN ES como nivel.
- Version: FileVersion 5.0.3.0, InformationalVersion 5.0.3, `VERSION` "5.0.3"; AssemblyVersion queda 5.0.0.0.

## 5.0.2 (27-10-2026) — sin la pestaña de detalle
- Pedido del operador (textual): "elimina este cuadro, es poco profesional, no hace falta" / "no lo escondas, elimina". Se ELIMINO del codigo la pestaña
  "PythiaGex 5.0 ▸/▾" (titulo, panel desplegable con fuentes y detalle por nivel, su clic y la casilla "Pestaña 5.0" / Cabecera4). Queda la edad del dato
  arriba a la derecha (protocolo), que ahora lleva tambien los avisos que solo decia la pestaña: "MOTOR PARADO <edad>" en rojo y "mercado cerrado".

## 5.0.1 (27-10-2026) — replicas con la conversion de la 5.0
- Pedido del control multifuente (CONTROL_MULTIFUENTE_2710.md): "2.0 SPX D1-D2", "Clas SPX 0Γ" y "Clas SPX D1-3" dibujaban ~1,2 pts DEBAJO (base "medida"
  por la recta de forwards) y la 2.0 saltaba 1,1-1,7 pts al alternar medida/rueda. Ahora (ajuste nuevo `Replicas50Conversion`, 7. Datos, default true) la
  raya va en K + base de la 5.0 (C7, la del eje y de MUROS_SPX); la seleccion, la gamma, el radio y el zero siguen con la base de cada original (mismos
  strikes y montos). Probado 15:18-19:30Z: strikes 253/253, precio = K + base 5.0 en 506/506, saltos de R20 66 → 0. Con false: identico a la 5.0.0.

## 5.0.1 (27-10-2026, compilada; SIN instalar) — cierres anticipados, histéresis del roll por raíz, recuadro en USD de ES

Base: commit `deb23344a` (la 5.0.0 instalada, DLL sha256 `b06c59c3…`). FileVersion 5.0.1.0, InformationalVersion 5.0.1, `VERSION`
"5.0.1"; **AssemblyVersion queda fija en 5.0.0.0** (el .ws guarda el tipo con su versión, como la 4.1 con 4.0.0.0). Ninguna propiedad
pública renombrada; ningún default de lo que se dibuja cambiado.

### Cierres anticipados (27-11-2026 y 24-12-2026)
- **Qué pasaba.** El vencimiento PM estaba clavado en 16:00 NY en los tres lugares que lo fijan (`HoraNyCboe.CierreUtc`, `RelojNy.Cierre`,
  `GammaHoyTresCapas.VencimientoYaVencido`). El viernes después de Thanksgiving y en Nochebuena la bolsa cierra a las 13:00 y las opciones que
  vencen ese día liquidan a esa hora: el 0DTE quedaba en el horizonte Hoy de 13:00 a 16:00 ya liquidado (la trampa "siguen contando el 0DTE
  ya vencido", con T en el piso de 1 minuto) y a la mañana su T salía 3 h más larga (12:30 NY: 3,5 h en vez de 0,5 h). Medido en el arnés con
  la 5.0.0: a las 13:30 NY la serie del día seguía adentro con 2,5 h por delante y la base por forwards tomaba la serie liquidada como cercana.
- **Arreglo.** Tabla editable nueva `CierresAnticipados.cs` (fecha → hora NY por producto: SPXW PM, SPY, ES PM de CME; una fecha que no está
  = 16:00; solo ADELANTA el cierre; lo que no se entiende se ignora y lo dice el log de arranque). La usan `HoraNyCboe.CierreUtc` (con
  producto), `CrudoCboe.VencUtc` + `CrudoCboe.Producto` (el producto sale de las raíces OCC de la cadena: SPY o SPX/SPXW),
  `CboeCadena.Construir` (los días de la serie hasta esa hora: sale del Hoy a esa hora y su T cuenta hasta ahí), `CboeCadena.Medir` (la
  excluye de la base desde esa hora; el x del contado sigue anclado a las 16:00 del cercano, `HoraNyCboe.CierreNormalUtc`: la recta va en días
  enteros entre fechas), `RelojNy.Cierre` (libro ES de Rithmic: `DiasAlVencimiento`, `Filas()` y `YaVencio`) y las capas 3.0
  (`VencimientoYaVencido` → `YaVencioCapa`, con SPY o SPX según la capa). Logs: la línea de arranque lista la tabla
  ("5.0.1: cierres anticipados …"), la de cada serie de ES dice "vence 13:00 NY (cierre anticipado …, verificada)" y la de cada bajada de
  CBOE agrega "serie 2026-11-27 vence 13:00 NY (…)" cuando la cadena la trae.
- **Tabla.** 27-11-2026 y 24-12-2026: SPXW 13:00 (verificada), ES 13:00 (verificada), SPY 13:00 **SIN VERIFICAR** (el cierre de SPY es a las
  13:00, pero no encontré en fuente oficial si las SPY que vencen operan hasta 13:15 como las "eligible options" de NYSE: 13:00 es el valor
  conservador). 26-11-2027, 03-07-2028 y 24-11-2028: la fecha la publica NYSE; Cboe y CME no publicaron esos años: **SIN VERIFICAR**, misma regla.
- **Fuentes (consultadas el 27-10-2026).** NYSE `https://www.nyse.com/markets/hours-calendars` (cada mercado cierra a la 1:00 p.m., 1:15 las
  "eligible options", el 27-11-2026 y el 24-12-2026); Cboe `https://www.cboe.com/about/hours/us-options/` (tabla 2026: "Thanksgiving Early
  Close" y "Christmas Early Close", RTH 09:30:00 - 13:00:00) y regla 5.1(b)(2)(C) (Exhibit 5 de SR-CBOE-2026-005: el último día las PM operan
  hasta el cierre de las acciones); CME: regla de las semanales europeas sobre el S&P (presentación CME 26-264 a la CFTC, capítulo 353B, que
  remite a la 358A: terminan a las 3:00 p.m. CT o al mediodía CT con cierre anticipado programado de la bolsa principal; fixing 11:59:30-12:00
  CT), `https://www.cftc.gov/filings/ptc/ptc0611265993.pdf`. La 358A del E-mini y los PDF de liquidación 2026 de CME (equity 12:00 CT el 27-11
  y el 24-12) se vieron SOLO por el índice del buscador: cmegroup.com no deja leer (WebFetch: timeout; desde la PC: 403 "IP blocked").
- **No cambia:** los días normales (823 fechas 2026-10-01..2028-12-31 iguales a la 5.0.0 salvo las 5 de la tabla), la AM (09:30), las líneas
  de cboe-local (Python: siguen a las 16:00), la sesión del futuro (`MercadoAbierto`) y las ventanas de rueda (09:30-16:00) de la 3.0 y la familia.

### Histéresis del spread del roll por raíz (`CadenaApi.HisteresisRollPts`)
- `RefrescarDesplazamiento` redondea el spread (futuro del gráfico − trimestre que vence, por puntos medios, cada 5 s desde `Latido`) a 0,25 y
  solo lo reemplaza si cambió al menos la histéresis; ese spread (`DesplazamientoAnterior`) corre el strike de las series sobre el trimestre
  viejo en `Filas()` (K = strike + spread) y centra su ventana en `Ventana()`. Con el 1 pt heredado de NQ, en ES (ESZ6 → ESH7, vence el 18-12)
  las weeklies y la Regular sobre Z6 podían quedar 0,75 pt (3 ticks; casi 4 con el redondeo) corridas. Ahora ES 0,25 (un tick: error máximo,
  el redondeo); NQ y RTY 1 pt como antes. Medido en el arnés con el método real: ES 0,00 pts de error máximo y 10 cambios en 11 pasos de un
  tick; NQ y la 5.0.0 en ES, 0,75 pts y 2 cambios.

### Recuadro 3.0 "LIBRO ES" en USD de ES (`FactorRecuadro3` / `GxRecuadro`)
- El núcleo cuenta con ×100 para todo (`GammaHoyNucleo.MULT_INDICE`, copia letra por letra: no se toca) y el contrato de ES es ×50: el
  recuadro mostraba el DOBLE que las etiquetas de la familia para el mismo strike. Ahora cada monto del recuadro (GEX vol/OI por nivel, muros,
  majors, neto) va × `AlmacenTres.FactorMonto3` (ES 0,5; SPX/SPY 1; NQ 0,2 como la 4.1; otra raíz 1). Las flechas se siguen calculando con el
  valor del núcleo (el factor es positivo). Arnés: strike 7700 por OI −69,2M → −34,6M = etiqueta de la familia −34,6M = monto TRES_ES.

### Comentarios rotos por el clon (`CadenaApi.cs`, solo comentarios)
- `RaizGrande`: "MNQZ6"/"#MNQ"/"NQZ6" → "NQ"; "MESZ6"/"#MES"/"ESZ6" → "ES". `CodigoTrimestreAnterior`: ESZ6 → ESU6, ESH7 → ESZ6.
  También `CodigosGrande` (decía MESZ6 → ESZ6, NQH7; es ESH7).

### Verificado (sin instalar, sin ATAS)
- Compila (`herramientas/build_serial.ps1`). Arnés por reflexión fuera del árbol (`scratchpad\p6\arnes`, carga la DLL nueva y la 5.0.0 en dos
  AssemblyLoadContext): 141 de 141. `python -I herramientas/auditar_familia.py --perfil ES --autoprueba-es`: TODO OK.
- Sin captura en ATAS: falta la comprobación en pantalla (regla 8) después de instalar, y el 27-11 en vivo (leer en el log la línea
  "5.0.1: cierres anticipados" y "vence 13:00 NY").

## 5.0.0 (27-10-2026, compilada; SIN instalar) — la 4.1 de NQ clonada para ES/MES con SPX y SPY

Pedido del operador (textual): "copia y creame un indicador phitiagex gamma v5 como el de nq pero esta vez para es mes spx spy todo bien
analizado auditado dibujado". Diseño en `_diseno/DISENO_5_0.md`. Estado de partida medido: la 4.1 puesta en "MES 1m Chart" no dibujaba
nada ("los niveles estan en precio de NQ/MNQ; este grafico es MES").

### Identidad y clon (commits `f953afe83`, `ca2846f1b`)
- Indicador NUEVO: `atas/PythiaGexCinco`, `PythiaGexCinco.dll`, tipo `PythiaGexCinco.GammaCinco`, "PythiaGex 5.0 - Gamma ES", categoría
  "PythiaGex 5.0", AssemblyVersion 5.0.0.0; datos `%APPDATA%\ATAS\PythiaGex5`, logs `pythiagex5-*`. Convive con la 4.1 (estáticos, mutex y
  archivos propios).
- Clon mecánico de la 4.1 (`herramientas/clonar_5_0.py`): NQ → ES, NDX → SPX, QQQ → SPY en la familia; el núcleo 3.0 solo con los
  identificadores compuestos para compilar. CRLF como la 4.1.

### Sin TQQQ y enganche a ES (commit `6351674a8`)
- Fuera el módulo TQQQ entero (series `T_*`, eje "TQQQ x3", bajada de TQQQ, ajuste "TQQQ de noche"): `_diseno/QUITAR_TQQQ.md`. Quedan 35
  series. Los `Order` del grupo de arriba corrieron −5 desde el 23 (familia 0..36, núcleo 37..98).
- La familia arranca con la raíz ES del gráfico; la cadena de meses `FGHJKMNQUVXZ` restaurada (el renombre `MNQ`→`MES` la había roto).
- Instalador `herramientas/instalar_5_0.ps1` (solo `PythiaGexCinco.dll`) y sembrador `herramientas/sembrar_5_0.py`.

### P1 — libro ES y cuenta de la familia (commit `02e9fc606`; detalle en los encabezados de `comun/Seleccion.cs` y `fam/ReglasFam.cs`)
- Radio por libro (`SeleccionFam.RadioDe`): ES 25 pts, SPX y SPY 20, familia 20 (la 4.1: 100 para todo); cobertura medida en
  `MEDICIONES_ES.md` §1.3.
- Zero estándar en grilla de 0,5 pts, ±75 (la 4.1: 2,5 y ±300); `ZEST_MAX` 75; techo del radio de la carga 75.
- Multiplicador de ES 50 (la 4.1: NQ 20); `ReglasFam.Escala("ES")` = 0,5.
- `CONF_PTS` 3 y `FAM_GRILLA` 5 SIN cambiar: van atados al paso de strike, que en ES y SPX es 5 como en NQ.
- Piso de la estela de la 3.0 3.750 (la 4.1: 15.000, que con ES en ~7.700 tiraba todas las rayas TRES_*).
- OI de ES con la sesión en hora de NY (salto medido una noche, 27-10: 01:53 UTC).

### P2 (SPX, CBOE, réplica de la clásica), P3 (SPY, réplica de la 2.0) y P4 (capas 3.0 en ES)
- Los completan sus constructores; sus notas van en sus archivos y en `_diseno`. Este CHANGELOG no las adelanta.

### P5 — catálogo, pantalla, posiciones e integración (detalle: `_diseno/P5_PANTALLA.md`)
- **Catálogo:** técnico, criollo y estado de ES/SPX/SPY para las 35 series; el estado de TODAS empieza con "SIN MEDIR EN ES" y lo medido en NQ
  va entre paréntesis como dato de otro producto. Radios, confluencia (≤ 3 pts), baldes de la familia (5 pts) y grilla del zero dichos en el
  texto. Banda de SPY ±5 (medio strike de SPY ≈ 10,08 pts de ES; era ±20 en QQQ). Ids, orden, colores, `Calc` y `Extra` iguales.
- **Réplicas en ES:** la 2.0 y la clásica no arman capas en ES: un solo libro SPX, radio min(2 %, 100) = 100, cadena 960 s atrás.
  `R20_SPX_vol` = el libro SPX de la 2.0; `R10_SPX_zero`/`R10_SPX_dom` = el libro SPX de la clásica; `R20_SPY_vol` = selección estilo 2.0
  sobre SPY (la 2.0 no dibuja SPY en ES), apagada. La de la 2.0, sin verificar contra la 2.0 corriendo en ES; las de la clásica dicen lo que
  P2 comparó contra sus 131 salidas en ES del 05-10 (de día la misma base, esa noche +1,27 pts) y la salvedad de la clásica (`AVISO_CLASICA`)
  ya es la de ES, sin la medición de NDX del 09-10.
- **Defaults** (tipo nuevo: mandan en el primer gráfico): prendidas `ES muros OI`, `SPX muros vol`, `3.0 SPX rayas`, `2.0 SPX D1-D2`,
  `Clas SPX 0Γ` y `Clas SPX D1-3`; apagadas también `ES top-N vol` y `ES carga OI` (en la 4.1 venían prendidas) y `2.0 SPY D1-D2`.
- **Casillas:** nombres cortos con lo que distingue al principio ("ES carga OI", "Clas SPX D1-3", "SPY dom (2.0)", "Pestaña 5.0"); la primera
  frase de cada descripción dice qué llave necesita. **Los `Order` son los del clon** (un borrador los había renumerado 0..41 y chocaba con
  los 37-41 del núcleo: se volvió atrás). Ninguna propiedad renombrada.
- **Doble eje:** Ninguno/SPX/SPY, default **SPX**. Cotas de ES: razón ES/SPY 9,95-10,25 (la 4.1 tenía 30-60 de QQQ y **el eje SPY no se
  dibujaba nunca**), base ES − SPX en (0, 150]; fuera de cota el título dice el valor, la cota y la edad. Marcas fuertes de SPX cada 25.
- **Cartel** en un gráfico que no es ES/MES: "PythiaGex 5.0 es para ES/MES: este gráfico es X, no dibujo", con la regla de raíz del núcleo
  (antes aceptaba cualquier código con "ES" adentro, p. ej. FESX).
- **Pantalla en puntos de ES:** grupo de etiquetas 1 → 0,25 pt y rótulos de tramos juntos 1,5 → 0,25 pt (la misma altura en pantalla: ES se
  mueve ~4 veces menos en puntos que NQ), su precio al tick; `TRAMO_TOL` 0,5 igual (sigue la deriva de la conversión; un strike de ES son 5).
  Lo que está en píxeles, velas, segundos o letras no cambia.
- **Textos:** la salvedad de la clásica sin la medición de NDX presentada como de SPX; "2.0 SPY" dice que la 2.0 no dibuja SPY en ES; "precio"
  y "cinta" con la raíz del gráfico (MES o ES); pie "SIN MEDIR EN ES: describe el régimen, no anticipa la dirección".
- **Posiciones:** el multiplicador del cambio por nivel va explícito (ES x50 → 0,5; SPX/SPY x100 → 1): en la 4.1 caía en el default del libro
  (NQ 0,2), que con ES hubiera dado los ▲▼ y los montos x0,4. El umbral de la carga (50 contratos) es de P1/P4: casi no filtra en ES.
- **Integración:** textos de "7. Datos" para ES/SPX/SPY; la lógica del host igual.
- **Origen de la base de las réplicas en ES:** la pestaña entiende la cascada de la 2.0 y de la clásica en ES ("medida", "medida hace N min",
  "de la rueda" = normal; CRUDA, TEORICA y sin cota = respaldo, en naranja). Sin esto la base "medida" (lo normal en ES) salía "origen sin dato".
- Respaldo de la pantalla alineado con el catálogo (los cortos de TOPN decían "ES" y el catálogo "ES top"/"ES top OI"; venía de la 4.1).
- Verificado: compila; arnés de pantalla fuera del árbol, 47 de 47 (cartel, cotas y marcas de los dos ejes recalculadas por fuera, grupo de
  etiquetas, pestaña, textos de origen de las réplicas, catálogo y defaults). Sin captura en ATAS: no se instaló.
