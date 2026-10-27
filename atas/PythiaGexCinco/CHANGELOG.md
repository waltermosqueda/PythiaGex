# CHANGELOG — PythiaGex 5.0 (Gamma ES)

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
