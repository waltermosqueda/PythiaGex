# CHANGELOG — PythiaGex 5.0 (Gamma ES)

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
