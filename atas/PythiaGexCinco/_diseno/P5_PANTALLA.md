# P5 — catálogo, pantalla, posiciones e integración de la 5.0 (27-10-2026)

Paquete P5 de `DISENO_5_0.md` §2.4. Lo empezó un constructor anterior (commit `02e9fc606`: `CatalogoFamilia.cs`, `GammaCincoPantalla.cs`,
`SeriesPantalla.cs`) y quedó cortado; este documento cubre lo que había más lo que se terminó hoy. **Compila** (`build_serial.ps1`,
`Build succeeded`). Sin commit, sin instalar, sin tocar ATAS ni `%APPDATA%`.

Archivos tocados (solo los del paquete):
- `_modulos/familia/fam/CatalogoFamilia.cs`
- `_modulos/pantalla/ArmadoPantalla.cs`, `GammaCincoPantalla.cs`, `SeriesPantalla.cs`, `PantallaFamilia.cs`, `ContratosPantalla.cs`
- `_modulos/familia/posiciones/CambiosFamilia.cs` (+ la línea 1 de `FotosCambios.cs`, `RegimenesOi.cs`, `ClavesCambios.cs`)
- `IntegracionFamilia.cs` (solo textos de los ajustes y el encabezado; la lógica del host igual)
- nuevos: `README.md`, `CHANGELOG.md` y este archivo.

---

## 1. Catálogo (`CatalogoFamilia.cs`)

- **Ids, orden, colores, `Calc` (26) y `Extra` (6) NO cambian** (verificado en el arnés: las dos listas, id por id, iguales al clon).
- **Estado:** las 35 series empiezan con `SIN MEDIR EN ES`. Lo que la 4.1 midió en NQ va entre paréntesis como
  `(en NQ, otro producto: ...)` (o `(en NQ, con NDX/QQQ: ...)`). Nada medido en NQ se presenta como de ES.
- **Técnico / criollo** reescritos para ES/SPX/SPY:
  - radios dichos en el texto (`RADIO_ES_TXT` = "a <= 25 pts del precio (radio del libro ES)", `RADIO_CBOE_TXT` = "a <= 20 pts ... (radio de
    SPX y SPY)"); los valores los fija P1 en `SeleccionFam.RadioDe` (ES 25, SPX/SPY 20, familia 20): el catálogo solo los nombra como texto
    para no depender de la cuenta (lo compila también una prueba de pantalla);
  - confluencia `a <= 3 pts` (un strike de ES/SPX son 5), FAM en `baldes de 5 pts`, zero estándar `grilla de 0,5 pts; a más de 75 no se dibuja`;
  - conversión: SPX → ES `sumando la base ES - SPX sincronizada`; SPY → ES `multiplicando por la razón ES/SPY sincronizada (~10,1; un
    strike de SPY = 1 USD ~ 10 pts de ES)`;
  - **banda de SPY ±5** (medio strike de SPY ≈ 10,08 pts de ES; era ±20 en QQQ): `MAJORS_SPY_oi`, `MUROS_SPY_vol`, `MUROS_SPY_oi`,
    `MAJORS_SPY_vol`, igual en el respaldo de la pantalla;
  - **réplicas en ES:** la 2.0 y la clásica no arman capas en ES, usan UN libro SPX (radio min(2 %, 100) = 100 pts, cadena de CBOE 960 s
    atrás). `R20_SPX_vol` = el libro SPX de la 2.0 en ES; `R10_SPX_zero` / `R10_SPX_dom` = el libro SPX de la clásica en ES;
    **`R20_SPY_vol` no tiene equivalente** ("selección estilo 2.0 sobre SPY: la 2.0 no dibuja SPY en ES") y va **apagada**. La de la 2.0
    dice `SIN VERIFICAR contra la 2.0 corriendo en ES` (de la 2.0 en ES hay 5 salidas, del 05-10). Las de la clásica, con el pedido de P2,
    dicen lo que P2 comparó contra sus 131 AUDIT en ES del 05-10 (`P2_SPX.md` §3 T7): con la base y el precio de la clásica, el zero a ≤ 0,05
    pt en 96 y las D1-D3 iguales en 102; de día la misma base (mediana 0,00); esa noche +1,27 pts (ella en "de la rueda", la réplica en "medida
    hace 185 min"). Es la fidelidad de la réplica, no una medición del nivel: el estado sigue `SIN MEDIR EN ES como nivel`, sin la salvedad de
    NDX del 09-10;
  - carga: dice que el umbral y el tope son los ajustes `↳ carga` del grupo 3.0 y que en ES casi todos los strikes pasan 50 contratos (medido
    27-10 por P1).
- `PrendidasPorDefecto` (6): `MUROS_ES_oi`, `MUROS_SPX_vol`, `TRES_SPX`, `R20_SPX_vol`, `R10_SPX_zero`, `R10_SPX_dom`.
- Presets sin "Recomendadas" (era la recomendación del verificador en NQ).

## 2. Pantalla

### 2.1 Casillas del grupo "0. PRENDER / APAGAR (todo lo que se dibuja)" (`GammaCincoPantalla.cs`)

**Los `Order` son los del clon sin TQQQ (commit `6351674a8`), sin tocar.** El borrador de este paquete los había renumerado 0..41 "sin
huecos ni repetidos" y eso chocaba con el núcleo 3.0, que en el clon arranca en 37 (`"3.0 NQ dom▸"` 37, `"↳ NQ D1"` 38, `"↳ NQ D2"` 39,
`"↳ toques"` 40, `"↳ NQ M+ OI"` 41; P4 tampoco los cambia). Se volvió a los del clon: verificado propiedad por propiedad (46 de 46
iguales a `6351674a8`). Quedan los repetidos que ya tenía la 4.1: top-N/carga de ES en 7-9 y carga de SPX/SPY en 10-11 contra SPX 7-12.

Nombre (Order, default). La primera frase de cada descripción dice qué llave necesita; después técnico, criollo, radio, conversión y estado:

- `ES muros vol` (0, no) · `ES muros OI` (1, **SÍ**) · `ES majors vol` (2, no) · `ES majors OI` (3, no) · `ES 0Γ est vol` (4, no) ·
  `ES 0Γ est OI` (5, no) · `ES cruces vol` (6, no) · `ES top-N vol` (7, no; en la 4.1 venía prendida) · `ES top-N OI` (8, no) ·
  `ES carga OI` (9, no; en la 4.1 venía prendida)
- `SPX muros vol` (7, **SÍ**) · `SPX muros OI` (8, no) · `SPX majors vol` (9, no) · `SPX majors OI` (10, no) · `SPX 0Γ est vol` (11, no) ·
  `SPX cruces vol` (12, no) · `SPX carga OI` (10, no)
- `SPY muros vol` (13, no) · `SPY muros OI` (14, no) · `SPY majors vol` (15, no) · `SPY majors OI` (16, no) · `SPY 0Γ est vol` (17, no) ·
  `SPY carga OI` (11, no)
- `FAM muros vol` (18, no) · `FAM muros OI` (19, no) · `CONF vol` (20, no)
- `Clas SPX D1-3` (21, **SÍ**) · `Clas SPX 0Γ` (22, **SÍ**)
- `2.0 SPY D1-D2` (23, no: la 2.0 no dibuja SPY en ES) · `2.0 SPX D1-D2` (24, **SÍ**) · `SPY dom (2.0)` (25, no) · `SPX dom (2.0)` (26, no)
- `3.0 ES rayas` (27, no) · `3.0 SPX rayas` (28, **SÍ**) · `3.0 SPY rayas` (29, no)
- `Doble eje izq.` (30, **SPX**) · `Estela▸` (31, sí) · `↳ rot. tramos` (32, no) · `Etiquetas▸` (33, sí) · `↳ montos` (34, sí) ·
  `↳ cambios ▲▼` (35, sí) · `Pestaña 5.0` (36, sí)
- grupo "6. Pantalla": `Ventana del cambio por volumen` (1026, 15 min), `Recuadro abierto` (1035), `Separacion del borde de arriba (px)`
  (1036, 56), `Tamano de letra` (1040, 9).

Ninguna propiedad se renombró ni cambió de tipo. La 5.0 es un tipo nuevo en ATAS (ningún `.ws` guarda sus `S_*`): **en el primer gráfico
mandan estos defaults**. Nombres medidos por el borrador con el método de `grupo_arriba_415e.py --anchos` (entran hasta 78,9 DIP; solo
"FAM muros vol" 80,1, igual que en la 4.1). `SeriesPantalla.Casillas` es el espejo (Orden = Display.Order del clon).

### 2.2 Eje doble (`Eje4Libro`, default **SPX**)

- Ninguno / SPX / SPY. Default SPX: es el índice que publican los tableros de GEX; la base ES − SPX es la misma que usan las rayas de SPX.
- **Cotas de ES** (`MEDICIONES_ES.md` §3.8, sanidad): razón ES/SPY entre **9,95 y 10,25** (medido 10,081-10,127); base ES − SPX en
  **(0, 150]** (medido 35-72 en Z6). La 4.1 tenía 30-60 para la razón (QQQ ~41): **con SPY ~10,08 el eje no se dibujaba nunca** (verificado
  con el código commiteado: "eje SPY: sin conversion vigente", 0 marcas). Fuera de cota, el título del eje dice el valor, la cota y la edad
  ("eje SPX: base ES - SPX 212,40 fuera de su cota (0-150 pts): no dibujo el eje · dato 16 min").
- Marcas fuertes: SPX cada **25** pts del índice (NDX cada 100: ~0,33 % del índice en los dos); SPY cada dólar entero (un strike).

### 2.3 Cartel en un gráfico que no es ES/MES

`PythiaGex 5.0 es para ES/MES: este gráfico es X, no dibujo` (naranja). La prueba es la MISMA regla de raíz del núcleo
(`CadenaApi.RaizGrande`: el código empieza con `MES` o `ES` después de sacar el `#` de la continua), en `ArmadoPantalla.EsGraficoEs`. El clon
buscaba "ES" en cualquier lugar del código: aceptaba, por ejemplo, `FESXZ6` (verificado antes/después).

### 2.4 Constantes en PUNTOS (las de píxeles, velas, segundos y letras no cambian)

ES se mueve ~4 veces menos en puntos que NQ (precio NQ/ES 3,86; rangos 6,1-6,6: `MEDICIONES_ES.md` §1). Con el zoom de siempre 1 pt de NQ son
~2 px y 1 pt de ES ~10 px.

| constante (`ArmadoPantalla.cs`) | 4.1 | 5.0 | por qué |
|---|---|---|---|
| `GRUPO_PTS` (antes literal `<= 1.0`): niveles en UNA etiqueta | 1 pt | **0,25** | misma altura en pantalla (~2-3 px) = un tick; con 1 pt se escondían niveles que en ES se ven separados (verificado: dos niveles a 0,8 pt daban 1 etiqueta) |
| `TRAMO_JUNTAR_PTS`: rótulos de tramos juntos | 1,5 | **0,25** | ídem (~3 px) |
| precio del rótulo que junta series distintas | al punto | **al tick** (`AlTick`) | el punto son 4 ticks y la tolerancia ahora es 1 |
| `TRAMO_TOL`: misma raya | 0,5 | **0,5 (igual)** | no es distancia de pantalla: sigue la deriva de la conversión (ES 0,45-0,48 pts de ruido contra NDX 1,79) y un salto de strike en ES es >= 5 pts: nunca junta dos strikes |
| `RAZON_SPY_MIN/MAX` (cota del eje SPY) | 30-60 | **9,95-10,25** | §2.2 |
| `BASE_SPX_MAX` (cota del eje SPX) | \|b\| < 2000 | **(0, 150]** | §2.2 |
| `SPX_FUERTE` (marcas fuertes) | 100 | **25** | §2.2 |
| precio con banda (SPY ±5) | al punto | al punto (igual) | 4 ticks, menos que la banda de 20 ticks (en NQ: 1 pt contra ±20) |

### 2.5 Textos de la pantalla

- `AVISO_CLASICA` es el de ES (pedido de P2): "Clasica SPX: replica a la clasica en ES (libro SPX, radio 100, cadena 960 s atras, su cascada
  de base). Contra la clasica en ES solo se comparo el 05-10 (131 salidas): de dia la misma base; esa noche +1,27 pts (ella en la de la rueda,
  la replica en la medida de hace 185 min). Como nivel, SIN MEDIR EN ES". Ya no habla de la base de la rueda anterior ni de Rithmic ni de los
  ~13 pts de NDX del 09-10. Las descripciones de "Clas SPX 0Γ" y "Clas SPX D1-3" dicen la cascada de ES y lo comparado el 05-10.
- Fuente "2.0 SPY": "(estilo 2.0, CBOE: la 2.0 no dibuja SPY en ES)"; "2.0 SPX": "(réplica de la 2.0 en ES, CBOE)"; "Clasica SPX": "(réplica de
  la clásica en ES, CBOE)".
- "— precio MES —" y "cinta MES" con la raíz del gráfico (MES o ES).
- Pie de la pestaña: "SIN MEDIR EN ES: describe el régimen, no anticipa la dirección".
- `NOTA_REPLICA20` (detalle del cambio): "estilo 2.0: sin cambio por nivel (... del libro de la 5.0)".

### 2.6 Textos de OTROS paquetes que la pantalla lee (contrato con P2/P3: si cambian, el origen sale "sin dato" en naranja)

- de `replica20/Replica20.cs` (P3), fuente "2.0 SPY": `" = MES "`, `"vela m2 interpolada"`, `"vela alineada"`, `"CRUDA: la vela alineada es de
  otro contrato"`, `"ultima valida "`, `"mediana de la rueda"`, `"CRUDA sin alinear"`.
- fuente "2.0 SPX" (P3, `BaseSpx20.Cascada`, ya editado por P3 a las ~12:00): `"base 2.0 X (ORIGEN...; cota ...)"` con ORIGEN que empieza con
  `medida` / `medida hace` / `de la rueda` (**normal**: la cascada de la 2.0 en ES) o `CRUDA` / `TEORICA` / `... (sin cota)` (**respaldo**,
  naranja). La pantalla extrae ORIGEN con los paréntesis balanceados (`ArmadoPantalla.OrigenBase20`). En la 4.1 la CRUDA era lo normal (en
  NDX la cadena nunca trae "medida"); en ES pasa a respaldo.
- de `clasica/ClasicaSpx.cs` (P2, ya editado por P2), fuente "Clasica SPX": `"= medida (forwards de la cadena de las HH:MM:SSZ[, X ticks de
  error], confiable)"` y `"= medida hace N min (...)"` (**normal**, nuevos en ES), `"= de la rueda yyyy-mm-dd"` + `"congelada"` (normal),
  `"5.0 sincronizada X"`, `"rueda de hoy X (n muestras)"`, `"vencio a las 24 h"`, `"no paso la cota del carry"`, `"= TEORICA"`, `"(sin cota)"`,
  `"= CRUDA"` (respaldo), `"dominantes por OI"` (`ResultadoClasica.MARCA_DOM_OI`).
- `"centroide ±12"` del detalle de `R10_SPX_dom` = `DominantesClasica.CENTROIDE` (hoy 12, lo que usa la clásica en ES).
- A las ~12:05 los dos productores (ya editados por P2 y P3) escriben exactamente eso: el arnés prueba 7 textos de la 2.0 SPX y 5 de la
  clásica copiados de su formato actual. **Sin este arreglo, la base "medida" (lo normal en ES) salía "origen sin dato" en naranja.**

## 3. Posiciones (`CambiosFamilia.cs`)

- **Multiplicador explícito** (`CambiosFamilia.EscalaDe`): ES x50 (`MinuteroEs.MULT`) → 0,5; SPX y SPY x100 → 1. En la 4.1 la llamada iba sin
  `Mult` y caía siempre en el default del libro (NQ 0,2); si en la 5.0 ese default hubiera quedado en 0,2, los ▲▼ y los montos del cambio de ES
  saldrían x0,4 contra el `GexM` del nivel. P1 ya había corregido el default (`ReglasFam.Escala("ES")` = 50/100); con el Mult explícito no
  depende de eso. Buscado todo `0,2`/`20` del libro del futuro en `posiciones/` y `pantalla/`: no hay otro.
- **`CargaMinima` (50) no vive en mis archivos:** está en `fam/ReglasFam.cs` (P1, `CargaMinima`, `CargaTope`, `CargaMinimaRayas`) y la pisa el
  ajuste `CargaMinimaOi` de `GammaHoyTresRayas.cs` (P4). P1 midió el 27-10 04:00 UTC que en ES los 10 strikes del radio de 25 tienen >= 528
  contratos (mediana 1.008): con 50 la carga de ES marca el radio entero hasta el tope de 12. Las descripciones de las casillas de carga lo
  dicen. El umbral de ES lo tiene que decidir P4/P1 con el operador.
- Roles (muro C/P, M+/M−, D1/D2) y textos sin NQ; comentarios con mediciones de la 4.1 marcados como de NQ/NDX/QQQ.

## 4. Integración (`IntegracionFamilia.cs`)

Solo textos de "7. Datos": la sesión de CME del ES/MES, la bajada de SPX (`_SPX`: SPXW diarias y SPX mensuales) y SPY con su peso medido
(~1,7 MB comprimida por foto de SPX, unas 2 veces NDX), y el tope de la bajada. Propiedades, defaults y la lógica del host sin cambios.

## 5. Verificación

- `powershell -ExecutionPolicy Bypass -File herramientas/build_serial.ps1 -Dir atas/PythiaGexCinco -Salida bin/Release` → `Build succeeded`.
- Arnés de pantalla fuera del árbol (scratchpad `arnes_p5`, compila `Contratos.cs` + `SeriesPantalla.cs` + `ArmadoPantalla.cs` +
  `CatalogoFamilia.cs` de la 5.0, sin ATAS): **47 de 47 verdes** (33 de pantalla y catálogo + 14 de los textos de origen de las réplicas de
  §2.6, con la pestaña sin naranja para la base "medida" de la clásica y con naranja para la CRUDA): cartel en MNQZ6/FESXZ6 y no en MESZ6/ESZ6/#MES/MES; eje SPY 10,0812 con 16
  marcas, cada una en Y(K × 10,0812) ±1 px, fuertes = dólares enteros; eje SPY 41,19 fuera de cota sin marcas; eje SPX 37,18 con cada marca
  en Y(K + 37,18), fuertes = múltiplos de 25; base 212,4 y −3 fuera de cota; grupo a 0,20 y 0,25 pt = 1 etiqueta, a 0,50 = 2; pestaña con
  "precio MES"/"precio ES", el pie SIN MEDIR EN ES y la base de la clásica con su origen y la diferencia (+0,32 pts); respaldo = catálogo en
  las 35 series (arreglado de paso: el respaldo de TOPN decía "ES" y el catálogo "ES top"/"ES top OI", ya pasaba en la 4.1); estado SIN MEDIR
  EN ES en las 35; defaults = `PrendidasPorDefecto`; `Calc`/`Extra` iguales al clon.
- **Antes** (los mismos casos contra el código commiteado `02e9fc606`): eje SPY "sin conversion vigente", 0 marcas; SPX con 1 marca fuerte
  (cada 100) de 16; FESXZ6 sin cartel y con etiquetas; dos niveles a 0,8 pt en 1 etiqueta.
- **No hecho (no me toca hoy):** captura en ATAS ni comprobación fehaciente en pantalla (no se instala en este paquete).

## 6. Qué avisarle al operador (en su idioma, antes de instalar, con captura antes/después)

- Al poner la 5.0 en el gráfico de MES se prenden seis cosas solas: los muros de ES por interés abierto, los muros de SPX por volumen, las
  rayas de la capa SPX del motor 3.0, las dominantes de la 2.0 de SPX y las dos de la clásica (su zero y sus D1-D3). Todo lo demás arranca
  apagado. Ninguna de esas seis está medida en ES: es lo que se ve, no lo que funciona.
- A diferencia de la 4.1, arrancan APAGADAS las de "top-N" y "carga" de ES (en NQ venían prendidas) y la "2.0 SPY", porque la 2.0 en un
  gráfico de ES no dibuja SPY.
- El eje de la izquierda muestra SPX por defecto (el índice que publican los tableros). En la 4.1 el eje de SPY no aparecía nunca en un
  gráfico de ES; ahora sí.
- Dos niveles que caen a un tick o menos van en una sola etiqueta (en NQ era un punto entero, que en ES son cuatro ticks).
- La carga de ES casi no filtra con 50 contratos: en ES casi todos los strikes cercanos tienen más de 500.

## 7. Riesgos y pendientes

- Los `Order` repetidos de la 4.1 (7-11) siguen: el diálogo los muestra intercalados como en la 4.1.
- Si P2/P3 vuelven a cambiar los textos de §2.6, el detalle de las réplicas sale "origen sin dato" en naranja (no rompe el dibujo): volver a
  correr el arnés con sus textos nuevos.
- Los textos de carga citan el horizonte de CBOE (14 días en vivo, 8 del archivo, ±5 %) y `CadenaApi.Vencimientos = 2`: si P2 cambia esos
  valores hay que tocar las descripciones.
- En un gráfico de MNQ la pantalla de la 5.0 pone el cartel, pero el núcleo 3.0 de la 5.0 reconoce la raíz NQ y calcula igual (sin dibujar:
  las llaves Tres41* vienen apagadas). Es del paquete P4.
- `herramientas/grupo_arriba_415e.py` apunta a la 4.1: no hay un `grupo_arriba_5_0.py` (ni lo necesita este paquete: no se renumeró nada).
