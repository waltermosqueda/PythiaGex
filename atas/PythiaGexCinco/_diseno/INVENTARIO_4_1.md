# Inventario 4.1 → 5.0 "Gamma ES": todo lo específico de NQ/NDX/QQQ/TQQQ en `PythiaGexCuatro`

27-10-2026, madrugada (ART). Solo lectura del código: no se editó nada fuera de este archivo.

**Qué cubre.**
- Los 75 `.cs` que compila `atas/PythiaGexCuatro/PythiaGexCuatro.csproj`: 23 del núcleo 3.0 y 52 de la familia, contando `_contratos/Contratos.cs`. Quedan afuera bin/, obj/, _visor_4_0_6/, _previews* y capturas/.
- Además: lo que hacen la 2.0 (`atas/PythiaGexDos`) y la clásica (`atas/PythiaGexNiveles`) en un gráfico de ES, y los arneses de `atas/_test_cuatro_paridad`.
- Seis agentes leyeron los archivos enteros, cada uno su bloque. Las líneas críticas las volví a leer una por una (lista en B.5).

**Versión real del código.**
- `GammaHoyTres.cs:97` dice `VERSION = "4.1.6g"`, pero el código ya trae piezas de la 4.1.7 y la 4.1.8 (12-10: TOPN, CARGA y rebobinado; por ejemplo `GammaHoyTresRayas.cs:54-78` y `_contratos/Contratos.cs:96`) y de la 4.1.9 (13-10: la carpeta extra de cboe-local, `_modulos/cboe/BajadorCboe.cs:32`).
- README, CHANGELOG y csproj están atrasados (`Version 4.1.5.5`, `InformationalVersion 4.1.5e`).
- El último `.cs` se tocó el 13-10 a las 15:46: las líneas que cita este inventario son estables. **El clon sale del código, no del README.**

**Las líneas son de la 4.1 (`PythiaGexCuatro`), no del clon.**
- `atas/PythiaGexCinco` ya tiene el clon mecánico: el borrador `clonar_5_0.py` lo armó a las 00:07, con la misma cantidad de líneas por archivo.
- Después de las 00:23 se editaron algunos archivos del clon, por ejemplo `GammaHoyTres.cs`, `Viva3.cs`, `AdaptadoresFamilia.cs`, `GammaHoyTresCapas.cs`, `CadenaApi.cs` e `IntegracionFamilia.cs`. En esos archivos las líneas pueden estar corridas.
- En `_diseno` ya hay `DISENO_5_0.md`, `MEDICIONES_ES.md`, `QUITAR_TQQQ.md` y `constantes_medidas.json`, de otros agentes. Este inventario no los leyó ni los concilió.

**Leyenda.**
- **ESCALA**: depende del precio o la volatilidad del producto. Se reescala con el factor que mide otro agente.
- **PRODUCTO**: hecho del producto (multiplicador, paso de strike, horario, dividendo). Lleva el valor de ES/SPX/SPY cuando se sabe con certeza; si no, SIN VERIFICAR.
- **IGUAL**: no depende del producto.
- **MEDIDO**: sacado hoy de archivos o logs de `%APPDATA%\ATAS`, con la fecha del dato.
- **SIN VERIFICAR**: no se pudo confirmar.
- En I aparecen además **DATO** (fecha, archivo o número fijo de un arnés) y **TQQQ** (se borra).

**Rutas.**
- En A–G son relativas a `PythiaGex\atas\PythiaGexCuatro\`; en H, a `PythiaGex\atas\`; en I, a `PythiaGex\`.
- `%APPDATA%` = `C:\Users\wmx_7\AppData\Roaming`. "scratchpad" = `C:\Users\wmx_7\AppData\Local\Temp\claude\C--Users-wmx-7-OneDrive-Escritorio-ATAS-nada\404dab2f-b5f6-48fa-ba08-3ab764cbbdd8\scratchpad`.

**Dónde está cada punto pedido.**
- Punto 1 (constantes): los bloques "(1)" de C (familia), D (cboe, ndx, qqq), E (pantalla) y G (núcleo).
- Punto 2 (producto y calendario): los bloques "(2)" de C, D y G, y "e)" de G.
- Punto 3 (rutas, archivos, mutex, hilos, logs, claves y símbolos): B.4 y los bloques "(3)" de C, D, E y G.
- Punto 4 (TQQQ): F.
- Punto 5 (núcleo 3.0 y capas): G.
- Punto 6 (ajustes [Display]):
  - C: `IntegracionFamilia.cs`.
  - E: `FamiliaCuatroPantalla.cs`, todas las `S_*` con su default.
  - G: "g)" y los bloques "(6)" del núcleo.
- Punto 7 (réplicas de la 2.0 y de la clásica): H.
- Punto 8 (arneses): I.
- **Lo más riesgoso: Z, al final.**

---

## A. Contexto medido hoy (para reescalar y para no confundir libros)

> **Antigüedad del dato, antes que los números.**
> - Los precios de CBOE (SPX, NDX, SPY, QQQ) son el spot congelado del cierre del 26-10: tienen **más de 30 minutos**.
> - Las fotos de Rithmic (ES, NQ) son del 27-10 a las 03:39Z.
> - Las bases de la rueda de ES de las originales son del 05-10: **22 días**.

**Precios y razones**
- ES 7.725,125 (`%APPDATA%\ATAS\PythiaGex4\viva\viva3-ES-2026-10-27.jsonl`, foto de las 03:39:21Z) y NQ 29.457,125 (`viva3-NQ`, 03:39:09Z). NQ/ES = **3,81**.
- SPX 7.676,77 (`PythiaGex\cboe-local\ultima-ES.json`, generado 02:49Z) y NDX 29.273,40 (`ultima-NQ.json`). NDX/SPX = **3,81**.
- SPY 766,28 (`ultima-SPY.json`, 00:19Z, after-hours) y QQQ 714,44 (02:49Z). SPY/QQQ = **1,07**.
- Razones: MES/SPY ≈ **10,07-10,08**; MNQ/QQQ ≈ 41,19-41,34.
- Base cruda ES−SPX: **37,18** (27-10 02:48Z); entre 33,9 y 35,5 a lo largo del 26-10; entre 31,9 y 47,7 el 23-10.
- Para comparar: base cruda NQ−NDX 152,74; base de la rueda de NQ en la 4.1, 163,57 (`PythiaGex4\base-rueda-NQ.json`, 26-10 20:17:55Z).
- Bases de la rueda de ES de las originales (dato de 22 días):
  - la clásica, 52,42 (`PythiaGex\base-rueda-ES.json`, 05-10 17:20:10Z, 23 muestras);
  - la 2.0, 52,1 (`PythiaGex2\base-rueda-ES.json`, 05-10 16:15:52Z, 5 muestras).

**Paso de strike cerca del precio**
- ES (Rithmic): 5, con huecos de 10 fuera de la ventana densa. NQ: 5 a 10.
- SPX: 5 (29 de 29 pasos a ±1 %; 90 de 90 entre 2 y 5 %). NDX: 10, y 5 en el 0DTE y el 1DTE.
- SPY: 1 USD. QQQ: 1 USD.
- **ES y SPX tienen el mismo paso EN PUNTOS que NQ y NDX, aunque el precio es 3,81 veces menor.**
- En el eje del futuro, un strike de SPY son ≈ 10 pts de MES; uno de QQQ son ≈ 41 pts de NQ.

**OI por strike cerca del precio** (medianas; es el OI de la OCC de anoche)
- ES 1.052, SPX 4.872, SPY 16.351.
- NQ 38, NDX 22, QQQ 6.594.

**CBOE**
- Retraso ts − ultimo_trade: SPX **902 s**, SPY **901 s** (medianas del 26-10).
- De noche (medido dos noches):
  - SPX clava el spot desde las 16:00 NY y el `ultimo_trade` en 16:14:59; la sesión GTH de CBOE no aparece en el dato.
  - SPY se sigue moviendo en el after-hours y en el premarket.
  - Es el mismo patrón que NDX y QQQ.
- Peso por bajada: SPX 1,83 MB gz (12,7 MB de JSON) contra NDX 0,81-0,90 MB; SPY 0,83 MB contra QQQ 0,72-0,74 MB.
- Contratos por crudo: SPX 28.492, NDX 15.196, SPY 13.160.
- Raíces en el crudo: SPX (19 fechas, la mensual AM) y SPXW (42); NDX (15) y NDXP (36).
- Tercer viernes:
  - el 18-09, la SPX AM tenía 5.207.673 de OI contra 423.612 de la SPXW;
  - la víspera del 16-10, el vencimiento "mañana" traía 1,34 M de OI contra 0,33 M reales de la PM.

**Publicación del OI** (primer instante en que cambian ≥ 30 % de las claves)
- ES (CME, por Rithmic): 01:29-01:59Z (15, 17 y 22-09). Es la franja de NQ.
- SPX: 07:01Z o 13:03-13:07Z. SPY: 07:46Z o 13:02-13:07Z (del 20 al 23-10).
- NDX: 06:36-06:43Z. QQQ: 13:02-13:07Z (del 20 al 26-10).

**El núcleo en ES**
- La ventana densa de ±1 % (`CadenaApi.cs:129`) es ±77 pts en ES y ±295-309 pts en NQ.
- Hoy la 4.1.6g corre en MESZ6. `%APPDATA%\ATAS\pythiagex4-gammahoy.log`:
  - arranques del 26-10 a las 22:49 y 23:37 ART y del 27-10 a las 00:34 ART, con `raiz=ES … radioDom=25 radioDibujo=40`;
  - minuto de las 01:09 ART: `suscritos=184/1454 strikesUtiles=46`;
  - 133 renglones `familia: cinta - | libro -`.
- En pantalla solo está el cartel naranja "los niveles estan en precio de NQ/MNQ … no dibujo nada".
- Ya escribe `PythiaGex4\estela\estela-ES-2026-10-27.jsonl` (d ≈ 7.727, `"gm":[null,null]`) y `viva\viva3-ES-2026-10-27.jsonl`.

---

## B. Trampas del renombre mecánico, contra el borrador que ya existe

**B.0 Leer primero.** Ya hay un borrador del clonador: `PythiaGex\herramientas\clonar_5_0.py` (27-10, 00:07). No hace exactamente lo que dice el plan de la tarea:
- Las reglas de la familia son por subcadena con lookarounds, no con `\b` (`clonar_5_0.py:51-62`).
- Mete `_contratos/Contratos.cs` en la familia (`:106-108`).
- En el núcleo renombra los identificadores compuestos que también usa la familia (`:70-83` y `:120-134`).
- Cubre el nombre visible y el VERSION privado (`:86-104`).
- Protege TQQQ (`:59-61`).

En C–I las trampas se evaluaron contra dos variantes, `\b` y subcadena. **Contra este borrador valen las conclusiones de B.** Si el script final pasa a `\b`, vuelven las que C–I marcan "con \b": `"_NDX"` sin renombrar, IDs `_NQ_` que no cambian mientras los literales sí, `ILibroNq`/`LibroNqFamilia` que no compilan.

Las reglas del borrador las simulé sobre el código con `scratchpad\inv\colisiones.py` (solo lectura): cambian 144 identificadores de la familia.

**B.1 Lo que el borrador deja bien** (C–I lo marcan como riesgo para otra variante)
- `_modulos/cboe/BajadorCboe.cs:141` queda `case "SPX": case "ES": Nombre "SPX", Archivo "ES", Simbolo "_SPX"`: la regla `NDX` no tiene límites y entra en `_NDX`. Coincide con cboe-local (`archivar_cadena.py:126`, `"ES": "_SPX"`) y con `IntegracionFamilia.cs:250` renombrado (`arch == "ES" ? "SPX"`).
- Los IDs con guion bajo (`MUROS_NQ_vol` → `MUROS_ES_vol`, `TRES_NQ` → `TRES_ES`, `R10_NDX_zero` → `R10_SPX_zero`, `DIVIDENDO_NQ` → `DIVIDENDO_ES`) cambian igual que los literales `"NQ"`. Las claves armadas en ejecución siguen coincidiendo: `ReglasFam.cs:302,311-316`, `SalidaFamilia.cs:55,103`, `LibroMinutoArmado.cs:97-101`, las `S_*` y `CatalogoFamilia`.
- `Contratos.cs` se renombra con la familia: `ILibroNq` → `ILibroEs` compila.
- En el núcleo, el mapa de identificadores renombra los compuestos que comparte con la familia. Así compilan `GammaHoyTres.cs:1168,1175,1205` y `Viva3.cs:63`.
  - MEDIDO, con la cantidad de usos: `Capa3NDX` (2), `Capa3QQQ` (2), `ILibroNq` (13), `LibroNqFamilia` (4), `MNQZ6` (5), `NQZ6` (4), `NQ_1600` (1), `TRES_NDX` (3), `TRES_NQ` (1), `TRES_QQQ` (3) y `_NDX` (1).
- Nombre visible: pasan a "PythiaGex 5.0 …" `GammaHoyTres.cs:92-93` y los textos que empiezan con `"PythiaGex 4.0`: `GammaHoyTres.cs:346,380,818,1110` y `_modulos/pantalla/ArmadoPantalla.cs:751,783,1180`. El `VERSION` privado de `GammaHoyTres.cs:97` queda "5.0.0".
- Sin colisiones:
  - ningún par de identificadores viejos termina en el mismo nuevo;
  - en la familia no había literales previos `SPX`, `SPY`, `"ES"`, `NDXP` ni `SPXW`;
  - `es` solo aparece como palabra en castellano y no hay ninguna variable `es` declarada (verificado). Por eso `bool nq` → `bool es` compila (`MotorFamilia.cs:456`, `CambiosFamilia.cs:464`);
  - `LINQ`/`System.Linq` y "aunque"/"AUNQUE" quedan intactos.

**B.2 Lo que el borrador rompe** (protegerlo antes de correrlo)
- La regla `MNQ` → `MES` (`clonar_5_0.py:52`) es por subcadena: convierte la cadena de meses `"FGHJKMNQUVXZ"` en `"FGHJKMESUVXZ"`. Se pierden N (julio) y Q (agosto), y E y S pasan a ser meses válidos.
- Pasa en 8 lugares:
  - `AdaptadoresFamilia.cs:104`;
  - `_modulos/familia/ndx/CalendarioNdx.cs:112,146`;
  - `_modulos/familia/qqq/ConversionQqq.cs:70`;
  - `_modulos/familia/replica20/Replica20.cs:291,293`;
  - `_modulos/pantalla/ArmadoPantalla.cs:262,263`, que además queda `ReVencEs = @"ES\s*([FGHJKMESUVXZ])…"`.
- Con MESZ6 anda por casualidad, porque H, M, U y Z conservan su posición. Pero el regex queda roto: **hay que excluir esa cadena.**

**B.3 Lo que el borrador no toca y hay que hacer a mano** (en orden de lo que rompe)
1. **Literales del núcleo que son ganchos de la familia.** `SUELTOS` los excluye (`clonar_5_0.py:71`).
   - `GammaHoyTres.cs:1187` y `Viva3.cs:63`: sin estos dos no arranca nada de la familia en ES (ni CBOE, ni motor, ni rayas). Es lo que se ve hoy en MESZ6.
   - `GammaHoyTresCapas.cs:323`: corta las capas.
   - `GammaHoyTresCapas.cs:122-123`, la lista `_capas`: en ES, `{Nombre "SPY", Archivo "SPY", por razón}` y `{Nombre "SPX", Archivo "ES", por base}`. El `Nombre` no puede ser "ES".
   - `GammaHoyTresCapas.cs:135` (`ModoDe`), `:153` (`CercoCapa`), `GammaHoyTresRayas.cs:98-113` (`RayaCapa*`) y `GammaHoyTresMajorOi.cs:48-53` (`MajOiVisible`).
   - El detalle está en G → "b)".
2. **TQQQ queda entero** (`clonar_5_0.py:59-61`). Detalle en F.
   - `BajadorCboe.cs:56` queda `{"SPX","SPY","TQQQ"}`: sigue bajando TQQQ cada 75 s.
   - `CboeArchivo.cs:24` queda `(ES|SPY|TQQQ)`.
   - El eje por defecto sigue siendo TQQQ.
   - El ajuste dice "Bajar las cadenas de CBOE (SPX, SPY, TQQQ)".
3. **Los números.** El borrador los deja a propósito (`clonar_5_0.py:15`). Ver Z.
4. **Textos "PythiaGex 4.x" sin comilla doble adelante:**
   - `_modulos/pantalla/FamiliaCuatroPantalla.cs:119` (`'PythiaGex 4.0 ▸'`) y `:120` ("la pestaña PythiaGex 4.0.");
   - los `VERSION` públicos de los módulos, que solo van al log: `PantallaFamilia.cs:20`, `MotorFamilia.cs:115`, `CambiosFamilia.cs:121`, `Replica20.cs:360`, `ClasicaNdx.cs:337`;
   - `AlmacenNiveles.cs:122` (`"v":"fam-4.1"`): no molesta, la carpeta es nueva;
   - la línea 1 de cada archivo, que es un comentario.
5. **Lo que queda con nombre de NQ.** Es cosmético y compila.
   - `FutMnq`/`futMnq` (`Contratos.cs:85,106,199`): no hay regla para `Mnq`.
   - `Raya3Ndx*`, `Raya3Qqq*`, `Capa3BaseNdx` y `Tres41*`: no aparecen en la familia y conviven con `Capa3SPX`/`Capa3SPY`, que sí se renombran.
   - Los comentarios `CadenaApi.cs:901,921,942` quedan diciendo `"MESZ6" … "ESZ6" -> "NQ"`, que confunde.
6. **Frases con números de NQ bajo nombres ya renombrados** (descripciones, catálogo, pestaña). Según el protocolo, en ES tienen que decir SIN MEDIR o SIN VALIDAR.
   - "un strike de SPY son 41 pts de ES" (`CatalogoFamilia.cs:104,125`; `SeriesQqq.cs:79,81`);
   - "(ES x20, …)" (`FamiliaCuatroPantalla.cs:54`);
   - el "~13 pts" de `AVISO_CLASICA` (`ArmadoPantalla.cs:487-488`);
   - las estadísticas de NQ (`CatalogoFamilia.cs:101,105,109…`; `SeriesQqq.cs:91-101`).
7. **Lo que lee los nombres desde afuera:**
   - `herramientas/grupo_arriba_415e.py`: 101 entradas, 56 con NQ/NDX/QQQ/TQQQ; apunta a PythiaGexCuatro;
   - los arneses ws_compat (W6-W8) y pantalla (A21-A23).
   - Hacen falta versiones 5.0. GammaCinco es un tipo nuevo: ningún `.ws` guarda sus propiedades, así que renombrar es gratis, pero **en el primer gráfico mandan los defaults del código** (Z.12).

**B.4 Convención de archivos que tiene que salir igual en todos lados** (la de cboe-local: "ES" = SPX, "SPY" = SPY)
- CBOE (`CboeArchivo.cs:27-30`): `cadena-ES-<día>.jsonl.gz`, `ultima-ES.json` y `cadena-ES.ultimo` son SPX. Los de SPY llevan "SPY".
- Núcleo: `base-rueda-ES.json` (capa SPX) y `razon-rueda-SPY.json` en `PythiaGex5` (`GammaHoyTresCapas.cs:157`). No chocan con `PythiaGex\base-rueda-ES.json` ni con `PythiaGex2\base-rueda-ES.json`.
- Estelas:
  - `estela-ES`: ya la escribe el núcleo con su raíz;
  - `estela-SPX` y `estela-SPY`: tienen que salir del `Nombre` de las capas.
- Familia: `muestras-SPX-`, `muestras-SPY-` y `muestras-clasica-SPX-<día>.jsonl`.
- `AlmacenTres.VidaS` (`:29`) y `CatalogoFamilia.CapasTres` (`:38`) tienen que ser exactamente {"ES","SPX","SPY"}.

**B.5 Líneas críticas que re-leí y coinciden con lo que dicen las secciones**
- Núcleo:
  - `GammaHoyTres.cs:92-98,401-404,1187`;
  - `Viva3.cs:63`;
  - `GammaHoyTresCapas.cs:122-123,135,153,255,283,323`;
  - `GammaHoyNucleo.cs:53,149`.
- CBOE: `BajadorCboe.cs:56,141-143`, `CboeArchivo.cs:24`, `CboeCrudo.cs:188-217`, `CboeCadena.cs:140-160`.
- Familia:
  - `AlmacenTres.cs:29,136,180-185`, `ReglasFam.cs:72-83,118`, `CambiosFamilia.cs:246`;
  - `MinuteroNq.cs:47`, `OiNq.cs:35,90`, `ZeroEstandar.cs:29-31`;
  - `IntegracionFamilia.cs:60,250`, `AdaptadoresFamilia.cs:104`;
  - `CalendarioNdx.cs:112,146`, `ConversionQqq.cs:70`;
  - `Replica20.cs:65-66,262,291,293`, `ClasicaNdx.cs:135,241,249,255`;
  - `Contratos.cs:57,85,199`.
- Pantalla: `ArmadoPantalla.cs:84,262-263,750-751,841,846`, y los defaults `= true` de `FamiliaCuatroPantalla.cs` (11 series y el eje TQQQ).
- La 2.0 y la clásica:
  - `PythiaGexDos/GammaHoyCapas.cs:395-397,437-438`, `PythiaGexNiveles/GammaHoyCapas.cs:380`;
  - `PythiaGexDos/GammaHoy.cs:88,403-408,1649-1651`, `PythiaGexNiveles/GammaHoy.cs:1411-1413`;
  - `PythiaGexDos/GammaHoyNucleo.cs:314-319`.
- Log de la 4.1 en MES: `pythiagex4-gammahoy.log:65809,66485,66936`.

---

## C. Puntos 1, 2, 3 y 6 por archivo: familia (Contratos, Adaptadores, Integración, comun, fam, nq, posiciones)

> Las "trampas del renombre" de esta sección se evaluaron contra dos variantes, `\b` y subcadena. Contra el borrador `herramientas/clonar_5_0.py` valen las conclusiones de B.

Alcance: `_contratos/Contratos.cs`, `AdaptadoresFamilia.cs`, `IntegracionFamilia.cs`, `_modulos/familia/comun/*`, `_modulos/familia/fam/*`,
`_modulos/familia/nq/*`, `_modulos/familia/posiciones/*`. Rutas relativas a `PythiaGex\atas\PythiaGexCuatro\`. Leí todos los archivos enteros.
El código está en la 4.1.6g (`GammaHoyTres.cs:97`) y tiene partes de la 4.1.7/4.1.8 (TOPN, CARGA, rebobinado, `Contratos.cs:96`, `:184-205`).
Los documentos (README, CHANGELOG) dicen 4.1.5e.

Clasificación: **ESCALA** = depende del precio o la volatilidad (el factor lo mide otro agente; solo por precio sería ×0,26). **PRODUCTO** = hecho del
producto (multiplicador, paso de strike, horario, liquidez). **IGUAL** = no depende del producto. "renombre OK" quiere decir que el renombre planeado lo deja bien.

### Datos medidos hoy (27-10, madrugada, ~03:40-03:50 UTC, con la rueda cerrada)

- **Precio de ES:** 7.725,125, libro vivo de Rithmic (`%APPDATA%\ATAS\PythiaGex4\viva\viva3-ES-2026-10-27.jsonl`, foto de las 03:39:21Z). NQ: 29.457,125 (`viva3-NQ`, 03:39:09Z). NQ/ES = **3,81**.
- **Precios de CBOE (cboe-local):** `ultima-*.json`, spot congelado al cierre del 26-10. Son **datos de más de 30 min**.
  - SPX: 7.676,77 (`ultima-ES.json`, generado 02:49Z).
  - NDX: 29.273,40 (`ultima-NQ.json`). NDX/SPX = **3,81**.
  - SPY: 766,28 (00:19Z).
  - QQQ: 714,44 (02:49Z).
- **Paso de strike cerca del precio:**
  - ES (Rithmic): 5, con huecos de 10.
  - NQ: 5 a 10.
  - SPX: 5 (29 de 29 pasos).
  - NDX: 10, con 5.
  - SPY: 1 USD.
  - QQQ: 1 USD.
  - **ES y SPX tienen el mismo paso EN PUNTOS que NQ/NDX, aunque el precio sea 3,81 veces menor.**
- **OI por strike:** suma de calls y puts de todos los vencimientos de la foto (es el OI de la OCC de anoche).
  - NQ a ±100 pts: 24 strikes, mediana 38; 11 tienen ≥ 50.
  - NDX a ±100: 14 strikes, mediana 22.
  - ES a ±26 pts: 11 strikes, mediana 1.052, todos ≥ 50.
  - SPX a ±26: 10 strikes, mediana 4.872.
  - SPY a ±2,58 USD: 5 strikes, mediana 16.351.
  - QQQ a ±2,43 USD: 4 strikes, mediana 6.594.
  - Script: `scratchpad\inv\carga_oi.py`.
- **Ventana densa del núcleo:** `CadenaApi.cs:129`, `VentanaDensaPct` = 1 %. En ES son ±77 pts y en NQ ±295 a 309 pts. Fuera de eso entra un strike de cada dos (`CadenaApi.cs:701`).
- **Estela 3.0 de ES:** hoy la 4.1 está corriendo sobre MES y el núcleo ya escribe `PythiaGex4\estela\estela-ES-2026-10-27.jsonl` (156 KB).
  - Ejemplo (03:39:46Z): `"d":[7727.93,7720.89,7727.90]`, `"gm":[null,null]`.
  - No existe ningún `viva3`/`estela` de SPX ni de SPY: en ES el núcleo no tiene capas.

---

### `_contratos/Contratos.cs`
**(1) Constantes**
- `Contratos.cs:93` — `Mult`, comentario "C1: NQ 20, NDX 100, QQQ 100" — multiplicador por libro (el valor real lo pone cada minutero) — PRODUCTO (ES **50**, SPX 100, SPY 100).
- `Contratos.cs:80` — "|Fut − fut| ≤ 3 % de fut" — filtro del libro del minuto — IGUAL (relativo).
- `Contratos.cs:70` — `PrecioSoloTick(..., maxEdadS = 120)` — edad máxima del tick (solo la usa TQQQ) — IGUAL.
- `Contratos.cs:154` — `CambiosVentanas.Min = {5, 15, 30}` — ventanas de cambio por volumen (min) — IGUAL.
- `Contratos.cs:33/36/37` — comentarios: dato = ts − 902 s; CBOE ±5 %, ≤ 14 días, flaca ≤ 8; Black-Scholes con r = 0,0375 — viven en el módulo cboe (otro inventario) — IGUAL.

**(2) Producto/calendario**
- `Contratos.cs:37` — `EsFuturo` "true solo NQ (Black-76)" — en ES es el libro de Rithmic (opciones sobre el futuro ES): Black-76, igual que NQ — IGUAL.
- `Contratos.cs:171` — `FotoFamilia.Sesion` "22:00 UTC a 21:00 UTC" (comentario) — sesión de CME, la misma para ES — IGUAL.

**(3) Nombres que cruzan el borde del renombre** (este archivo NO está en la lista)
- `Contratos.cs:57` — `interface ILibroNq` — se usa en `AdaptadoresFamilia`, `IntegracionFamilia`, `MotorFamilia`, `MinuteroNq`, `CambiosFamilia`, `FotosCambios` y en el núcleo (`GammaHoyTres.cs:1175`). Si `Nq→Es` toca los identificadores de la familia y no este archivo, **no compila**.
- `Contratos.cs:85`, `:199` — `LibroMinuto.FutMnq`, `RebobinadoFam.FutMnq` — se usan en 10 archivos de `_modulos` (`LibroMinutoArmado`, `Seleccion`, `AlmacenNiveles`, `MotorFamilia`, `ReglasFam`, `CambiosFamilia`, `MinuteroQqq`, `Replica20`, `ClasicaNdx`, `ArmadoPantalla`). Si `Mnq→Mes` no toca Contratos, **no compila**. El parámetro `futMnq` (`:106`) es solo un nombre: no rompe.
- `Contratos.cs:30/83` — comentarios con los valores de `Libro` ("NQ" | "NDX" | "QQQ" | "TQQQ"): los valores reales salen de los literales de cada minutero.

**TQQQ** (si se borra `_modulos/tqqq`, estos tipos pueden quedar sin uso; no rompen)
- `Contratos.cs:216-223` `TqqqVela`, `:225-229` `ICalculoTqqq`, `:234-237` `IVersionSTqqq`, `:176` `TqS/TqC/TqDatoUtc` (los lee `ArmadoPantalla`), `:243` parámetro `ICalculoTqqq tqqq` de `IMotorFamilia.Configurar`, `:40` `CierreAnterior` "para TQQQ", `:162` texto de ejemplo "x3: NQ = 124.21 x TQQQ".

### `AdaptadoresFamilia.cs`
**(1) Constantes**
- `AdaptadoresFamilia.cs:215` `VELA_MS = 120000` (vela de 2 min), `:216` `EDAD_TICK_MS = 120000`, `:217` `TOPE_CIERRE_MS = 4 h`, `:218` `HUECO_VIVO_MS = 120000`, `:219` `DIAS_HISTORIA = 8`, `:220` `PERSISTIR_CADA_S = 300` — tiempos de la cinta — IGUAL.
- `AdaptadoresFamilia.cs:373` — `400 * VELA_MS` — tope de baldes invalidados — IGUAL.
- `AdaptadoresFamilia.cs:1026` — `RetencionHoras = 30` — fotos del libro en memoria — IGUAL.
- `AdaptadoresFamilia.cs:1199/1224/1225` — mínimo 8 columnas; call si ≥ 0,5; descarta iv ≤ 0 o (oi ≤ 0 y vol ≤ 0) — lectura de viva3 — IGUAL.

**(2) Producto/calendario**
- `AdaptadoresFamilia.cs:49-51`, `:145-166` — sesión de CME de 18:00 NY de la víspera a 17:00 NY (`:159` día = fecha NY de t + 6 h); en modo fijo [día − 2 h, día + 21 h) UTC (`:153-154`) — ES tiene el mismo horario de Globex — IGUAL.
- `AdaptadoresFamilia.cs:118-121`, `:128`, `:135` — zona "Eastern Standard Time"; si no está, UTC−4 fijo — IGUAL.
- `AdaptadoresFamilia.cs:104-112` — `ContratoDeCodigo`, regex `([FGHJKMNQUVXZ])(\d{1,2})$`: "MESZ6" → "Z6" — así identifica el contrato del gráfico (lo usan `GammaHoyTres.cs:1178` e `IntegracionFamilia.cs:165`) — IGUAL. **Trampa:** ver "Trampas del renombre" (el "NQ" de la cadena de meses).

**(3) Rutas, archivos, hilos, logs**
- `AdaptadoresFamilia.cs:44` — `Carpeta` = `%APPDATA%\ATAS\PythiaGex4` — renombre OK → `PythiaGex5`. Coincide con `Registro.CarpetaDatos` (`Registro.cs:26`), que también se renombra.
- `AdaptadoresFamilia.cs:47`, `:177` — `pythiagex4-adaptadores.log` — renombre OK.
- `AdaptadoresFamilia.cs:637` — hilo "PythiaGex4 cinta-familia"; `:1056` — hilo "PythiaGex4 libro-familia" — renombre OK.
- `AdaptadoresFamilia.cs:692` — `cinta\seg-<instrumento>-<día UTC>.bin` — instrumento = código del gráfico (MESZ6) — OK.
- `AdaptadoresFamilia.cs:695` — `MAGIA = "PG4SEG1"` — el renombre no lo toca. No hace falta: la carpeta es nueva — IGUAL.
- `AdaptadoresFamilia.cs:864`, `:868`, `:873` — importa `cinta\cinta-<Raiz>-<sesión>.csv` (+ `-relleno.csv`, `.instrumento`, `.listo`) y marca `seg-<instr>-csv-<sesión>.hecho`. Lo escribe el núcleo (`GammaHoyTresCinta`) con su raíz: "ES" para MES. Coincide si se le pasa la raíz del núcleo, como hace `GammaHoyTres.cs:1198`.
- `AdaptadoresFamilia.cs:1071` — siembra el libro desde `viva\viva3-<Raiz>-<día>.jsonl`. Lo escribe `Viva3.Guardar` del núcleo con raíz "ES": existe hoy, 925 KB. Coincide.
- `AdaptadoresFamilia.cs:227`, `:242`, `:1016`, `:1240` — los defaults `raiz = "NQ"`, `?? "NQ"` y `Libro = ... "NQ"` — solo se usan si no llega raíz — renombre OK → "ES".
- `AdaptadoresFamilia.cs:53-54` — `AlEstela(capa, línea)`: las capas vienen del núcleo ("NQ" | "NDX" | "QQQ" en el comentario). En ES, el núcleo manda "ES" y nada más (no tiene capas).
- `AdaptadoresFamilia.cs:1009` — `class LibroNqFamilia` — también lo usan `GammaHoyTres.cs:1168`, `:1205` y `Viva3.cs:63` (el núcleo, que no se renombra). Si se renombra el identificador, **no compila**.

### `IntegracionFamilia.cs`
**(1) Constantes**
- `IntegracionFamilia.cs:44` `Cboe41TopeKBps = 1250` `[Range(16, 20000)]`; `:94` `(true, 1250)`; `:95` `t = 16` — tope de la bajada — IGUAL. **SIN VERIFICAR** que alcance para `_SPX`: el JSON crudo de SPX pesa unos 20 MB (CLAUDE.md). La cadencia de 75/300 s está en el módulo cboe.
- `IntegracionFamilia.cs:67` 30 s entre reintentos del host; `:268` espera de 300/5000 ms; `:317` aviso si CBOE pasa 10 min sin bajar en la rueda; `:324` aviso si el libro pasa 5 min sin foto; `:391` se guardan 14 sesiones de muestras; `:392` se siembran las últimas 2 — IGUAL.

**(2) Producto/calendario**
- `IntegracionFamilia.cs:56-60` — `FamiliaAsegurarHost`: **`if (raiz != "NQ") return;`** — renombre OK → "ES". Hace falta que se renombre: sin eso la familia no arranca nunca en ES.
- `IntegracionFamilia.cs:62` — sin `_famCinta`/`_famLibro` vuelve sin hacer nada. Esos dos los arma el núcleo, y el núcleo los corta en ES (`GammaHoyTres.cs:1187`, ver "Ganchos del núcleo").
- `IntegracionFamilia.cs:165` — `ContratoDeCodigo(codigo)` "MNQZ6 → Z6": con MESZ6 da Z6 — IGUAL.
- `IntegracionFamilia.cs:175-179` — QQQ: `ContratoDe` = `ContratoFijo(ContratoDeCodigo(codigo))` en modo NY, o `ContratoTablaPython` en paridad (`qqq/ConversionQqq.cs:52-58`: U6 hasta 2026-09-14, Z6 hasta 2026-12-17, después H7). Son fechas de roll del laboratorio; ES rola el mismo día que NQ — IGUAL, y solo pesa en modo paridad.
- `IntegracionFamilia.cs:292-296` — `EnRueda`: lun-vie de 09:30 a 16:00 NY (solo para los avisos de CBOE) — IGUAL (SPX como NDX).
- `IntegracionFamilia.cs:297-305` — `CmeAbierto`: de domingo 18:00 a viernes 17:00 NY, con pausa de 17 a 18 — IGUAL (ES).

**(3) Rutas, archivos, hilos, logs, claves**
- `IntegracionFamilia.cs:246-253` — `FuenteCapaEnMemoria`: **`arch == "NQ" ? "NDX" : arch`**. El archivo "NQ" es el libro NDX, la misma convención que cboe-local (`cadena-NQ` = NDX, `cadena-ES` = SPX; `herramientas/sembrar_4_0.py:11` copia cboe-local byte a byte).
  - Con el renombre queda `"ES" ? "SPX"`, que es correcto, pero **solo** si las capas del núcleo, ya generalizadas, piden "ES" para SPX y "SPY" para SPY.
  - Hoy el núcleo pide `k.Archivo` = "QQQ" | "NQ" (`GammaHoyTresCapas.cs:130-133`, `:336`).
- `IntegracionFamilia.cs:255` — hilo "PythiaGex4 familia" — renombre OK.
- `IntegracionFamilia.cs:365`, `:389` — `familia\muestras-QQQ-<sesión>.jsonl` (y su comodín) — renombre OK → `muestras-SPY-`. Solo lo escribe y lo lee este archivo.
- `IntegracionFamilia.cs:408` — `pythiagex4-integracion.log` — renombre OK.
- `IntegracionFamilia.cs:173`, `:200`, `:208`, `:211` — categorías de log "ndx", "replica20", "clasica", "extras" — es cosmético: las minúsculas solo cambian si el renombre las incluye.
- `IntegracionFamilia.cs:314` `{"NDX", "QQQ", "TQQQ"}`; `:315`, `:318` textos "NDX/QQQ/TQQQ"; `:325` "libro de opciones de NQ (Rithmic)" — renombre OK para NQ/NDX/QQQ. **"TQQQ" hay que sacarlo a mano**: con `\b` no se toca; reemplazado como subcadena queda "TSPY".

**(6) Ajustes [Display]** (GroupName "7. Datos"; la 5.0 es un tipo nuevo, así que no hay valores guardados que choquen)
- `IntegracionFamilia.cs:33-35` — `Familia41Corregida`, "Sesión y ventanas en hora de Nueva York", Order 1010, default **true** — sin tokens — IGUAL.
- `IntegracionFamilia.cs:37-39` — `Cboe41Bajar`, Name "Bajar las cadenas de CBOE (NDX, QQQ, TQQQ)", Order 1020, default **true**. La descripción menciona `PythiaGex4\\cboe` y 75/300 s. Para ES: "(SPX, SPY)".
- `IntegracionFamilia.cs:41-44` — `Cboe41TopeKBps`, "Tope de la bajada (KB/s)", Order 1030, default **1250**, rango 16-20000 — IGUAL.
- `IntegracionFamilia.cs:46-48` — `Tqqq41AnclaNoche`, "TQQQ de noche (anclaje al cierre, SIN VALIDAR)", Order 1040, default **false** — **BORRAR**.

**TQQQ**: `:46-48` (propiedad), `:70` (pasa `Tqqq41AnclaNoche`), `:142` (campo `_tqqq`), `:158` (parámetro `tqqqNoche`), `:184-196` (`new CalculoTqqq`, `OpcionesTqqq`, `CierresTqqqDia`: necesitan el módulo tqqq para compilar), `:214` (`TqqqDeNoche`), `:218` (pasar `null` en vez de `_tqqq`), `:222` (`TqqqNoche`), `:314-318` (avisos), `:339-342` (`VelasTqqq` → `_cambios.Anotar(..., null)`).

### `_modulos/familia/comun/Gamma.cs`
- `Gamma.cs:15` — `TASA = 0.0375` — tasa del Black-Scholes de CBOE — IGUAL (la misma para SPX y SPY).
- `Gamma.cs:4`, `:32-38` — Black-Scholes **sin dividendo**: SPX/SPY rinden cerca de 1,2 % y NDX/QQQ cerca de 0,6-0,8 % (SIN VERIFICAR). Con T ≤ 2 días el efecto en la gamma es despreciable — IGUAL.
- `Gamma.cs:17` — `PISO_DIAS = 1/1440` — IGUAL.
- `Gamma.cs:41` — Black-76 para el futuro (el libro de Rithmic) y Black-Scholes para CBOE — IGUAL (en ES el libro es el futuro ES).

### `_modulos/familia/comun/LibroMinutoArmado.cs`
- `LibroMinutoArmado.cs:26-27`, `:50-51` — conserva los strikes a ≤ `FILTRO` (3 %) de fut, en precio del gráfico — IGUAL (relativo).
- `LibroMinutoArmado.cs:78` — `OiVale`: `b.Libro != "NQ" || b.OiOk` (la regla C2 solo aplica al libro de Rithmic) — renombre OK → "ES".
- `LibroMinutoArmado.cs:84` — `mult = b.Mult / 100.0` — el producto entra por `Mult` (ver `MinuteroNq.cs:47`) — PRODUCTO.
- `LibroMinutoArmado.cs:97-101` — claves dinámicas `"MUROS_" + b.Libro + "_" + fu` (también MAJORS_, ZTP_, ZEST_, TOPN_). **Trampa del renombre**, ver abajo. Hoy solo lo usa `SeriesNq.Visibles`, que nadie llama en el indicador. El camino vivo es `ReglasFam`.

### `_modulos/familia/comun/NumFam.cs`
- Sin nada atado al producto. `Robusta(..., piso)` recibe el piso de quien la llama (módulos ndx/qqq) — IGUAL.

### `_modulos/familia/comun/Perfil.cs`
- `Perfil.cs:30` — envejecimiento de la cadena acotado entre 0 y 2 días — IGUAL.
- `Perfil.cs:52` — horizonte "Hoy" = vencimientos con días_env ≤ máx(1,0; el más cercano + 0,01), sin los vencidos (`:49`, `:62`). Así se arma el "0DTE" — IGUAL.
  - ES por Rithmic, SPX y SPY tienen vencimientos diarios de lunes a viernes, igual que NQ/NDX/QQQ.
  - Medido hoy: viva3-ES 0,681/1,681 días; SPX 0,716/1,716/2,716.
- `Perfil.cs:52` + cboe — **riesgo del tercer viernes (2026-11-20 y 2026-12-18)**. Si el parser de CBOE le pone a la SPX mensual AM (sin W) el mismo día que a la SPXW PM, su OI enorme puede entrar al "Hoy" del jueves y del viernes. Esa serie deja de negociarse el jueves y liquida con la apertura del viernes. **SIN VERIFICAR**: depende del módulo cboe.
- `Perfil.cs:76` — T = máx(d, 1/1440)/365 — IGUAL.
- `Perfil.cs:116-133` — carga: OI de **todos** los vencimientos de la foto. Hoy el libro de NQ trae 2 vencimientos, igual que el de ES — IGUAL.
- `Perfil.cs:139` — `esc = 100 · S² · 0,01` (multiplicador interno 100; el producto entra después por Mult/100) — IGUAL.

### `_modulos/familia/comun/Seleccion.cs`
(Solo la usan `SeriesLibroFam`/`SeriesNq`/`SeriesQqq`, que corren en los arneses. Las constantes vivas del motor son las de `ReglasFam`, pero están **duplicadas** y hay que cambiar las dos.)
- `Seleccion.cs:31` — `RADIO = 100.0` — radio de ZTP en pts — ESCALA.
- `Seleccion.cs:32` — `ISLA = 0.10` — isla = menos del 10 % del vecino menor — IGUAL (relativo). El "2-4 contratos" del catálogo es solo texto.
- `Seleccion.cs:33` — `ZEST_MAX = 300.0` — se descarta el zero estándar a más de 300 pts — ESCALA.
- `Seleccion.cs:34` — `FILTRO = 0.03` — **esta sí es viva**: la usan `LibroMinutoArmado.cs:27/51` y `CambiosFamilia.cs:593` — IGUAL (relativo).
- `Seleccion.cs:37` — `R(fut) = min(fut·0,02; 100)` — en NQ (29.457) y en ES (7.725) **manda el tope de 100** (el 2 % da 589 y 154) — ESCALA (el 100).
- `Seleccion.cs:82` — `TopNCantidad = 6` — IGUAL.
- `Seleccion.cs:167` — monto en el strike con |Fut − p| < 1e-6: round(v·mult/1e6, 1); `:12` dice "NQ 0.2" — PRODUCTO por `mult` (ES 0,5).

### `_modulos/familia/comun/TiempoFam.cs`
- `TiempoFam.cs:4`, `:35-41` — sesión de paridad de 22:00 a 21:00 UTC — IGUAL.
- `TiempoFam.cs:52-65` — NY; si no hay zona, UTC−4 — IGUAL.
- `TiempoFam.cs:68` — `HM` para comparar "01:55"/"13:30" (lo usa OiNq) — IGUAL.
- `TiempoFam.cs:77-119` — feriados de NYSE/CME-equity y posferiados (`:74`: la vista previa solo tenía 2026-09-08; `:75`: SUPUESTO no medido de que el OI de Rithmic llegue al día después de cualquier feriado) — IGUAL (mismo calendario para ES). No modela los cierres tempranos (por ejemplo el día después de Acción de Gracias), igual que en NQ.

### `_modulos/familia/comun/ZeroEstandar.cs`
- `ZeroEstandar.cs:29` — `NQ = ParamZero(2.5, 300, 50)` — paso de la grilla 2,5 pts, semiancho ±300, centro y balde de caché 50 — ESCALA, los tres valores. El centro `c0 = round(S/50)·50` (`:40`) corre la grilla hasta 25 pts: con un semiancho chico en ES también hay que achicar el 50.
- `ZeroEstandar.cs:30` — `NDX = (2.5, 300, 50)` — para SPX — ESCALA.
- `ZeroEstandar.cs:31` — `QQQ = (0.05, 7.5, 1.0)` en USD del ETF — para SPY: el SPY (766,28) cotiza cerca del QQQ (714,44), así que en relativo queda ±0,98 % contra ±1,05 % (factor 1,07) — ESCALA leve.
- `ZeroEstandar.cs:86` — `MINUTOS_TANDA = 240` — IGUAL.
- Python: `backtest_familia.py:498-500` (NQ 2,5/300/50, caché round(S/50)) y `:546-549` (NDX 2,5/300/50; QQQ 0,05/7,5/1; balde de envejecimiento de 900 s).

### `_modulos/familia/fam/AlmacenNiveles.cs`
- `AlmacenNiveles.cs:45` — `familia\niv-<sesión>-<contrato>[-paridad].jsonl`; contrato = código completo del gráfico (MESZ6); `:41` "sin-contrato" — renombre OK (con la carpeta).
- `AlmacenNiveles.cs:122` — cabecera `"v":"fam-4.1"` — no molesta (carpeta nueva) — IGUAL.
- `AlmacenNiveles.cs:137` — poda de `niv-*.jsonl` (los días los pone el motor: 14) — IGUAL.
- `AlmacenNiveles.cs:162/190/218/226-227` — "b" = nombres de libro; las claves de "s" = IDs del catálogo, buscados **por nombre** (`IndiceCalc`/`IndiceExtra`). Un ID que no coincide se descarta en silencio. Los fixtures `niv-*-MNQZ6.jsonl` de los arneses no sirven para ES.

### `_modulos/familia/fam/AlmacenTres.cs`
- `AlmacenTres.cs:29` — `VidaS = {NQ: 300, NDX: 1500, QQQ: 1500}` s — vida de una línea de estela. Los valores son IGUAL (cadencia de Rithmic/CBOE); las claves tienen que ser las raíces/capas que escriba el núcleo. Python: `preview_niveles.py:67`.
- `AlmacenTres.cs:77` — `estela\estela-<capa>-<día>.jsonl`: lo escribe el núcleo en `GammaHoyTres.cs:756` con su raíz ("ES" hoy). Para SPX/SPY no hay escritor.
- `AlmacenTres.cs:113`, **`:136`** — `v = (v != 0 && v > 15000) ? v : NaN` — **ESCALA, CRÍTICO.** Con ES cerca de 7.725 descarta **todas** las D1/D2 de la estela: TRES_ES, TRES_SPX y TRES_SPY quedan vacías. Medido: la estela-ES de hoy trae `d` = 7727,93/7720,89. Python: `extremos_rebote.py:476`.
  - De paso tapa el KeyNotFound de `SalidaFamilia.cs:103`, explicado en las trampas.
- `AlmacenTres.cs:176` — `RellenoTunel3 = 1e6` — marca del túnel, no es un precio — IGUAL.
- `AlmacenTres.cs:180-185` — `FactorMonto3`: NQ 20/100 = 0,2; NDX y QQQ 1; cualquier otra raíz NaN. Pasa de la escala de la 3.0 (`GammaHoyNucleo.cs:149`, `MULT_INDICE = 100`) a las unidades de la familia — **PRODUCTO, CRÍTICO.** Para ES va **50/100 = 0,5** (el E-mini S&P vale 50 USD por punto); SPX y SPY quedan en 1.
  - Si el renombre convierte "NQ" en "ES", devuelve 0,2: montos de las rayas 3.0 de ES ×0,4.
  - Hoy devuelve NaN para ES (medido: `"gm":[null,null]`).
  - El núcleo lo llama con su raíz en `GammaHoyTres.cs:774-781`.
- `AlmacenTres.cs:234` — vida por defecto 300 s para una capa desconocida — IGUAL.

### `_modulos/familia/fam/CatalogoFamilia.cs`
- `CatalogoFamilia.cs:37` — `Libros = {"NQ", "NDX", "QQQ"}` (orden de bk, `MotorFamilia.cs:187`) → {"ES", "SPX", "SPY"}. Python: `backtest_familia.py:158`.
- `CatalogoFamilia.cs:38` — `CapasTres = {"NQ", "NDX", "QQQ"}` → tienen que ser exactamente los nombres que use el núcleo en `estela-<x>` y `AlEstela`. Hoy en ES solo existe "ES".
- `CatalogoFamilia.cs:39` — `SeriesTqqq` (6) — **BORRAR**.
- `CatalogoFamilia.cs:42` — `Extra = {R20_QQQ_vol, R20_NDX_vol, DOMS_QQQ_vol, DOMS_NDX_vol, R10_NDX_zero, R10_NDX_dom}` — el índice es posición en `RegistroMinuto.Extra`.
- `CatalogoFamilia.cs:48` — `MetaExtraDe` → "2.0 QQQ" / "2.0 NDX" / "Clasica NDX". Tienen que coincidir con el `MetaLibro.Libro` que ponen `Replica20` y `ClasicaNdx` (ver la sección 7).
- `CatalogoFamilia.cs:55-56` — `PrendidasPorDefecto` (8, incluye `T_MUROS_oi`): sacar TQQQ.
  - No coincide con los defaults reales de la pantalla (`_modulos/pantalla/FamiliaCuatroPantalla.cs:40-97`), que además prenden `S_TOPN_NQ_vol`, `S_CARGA_NQ` y `S_T_ZERO_vol_pm`.
  - Qué se prende en ES lo decide el operador (SIN VERIFICAR).
- `CatalogoFamilia.cs:58-69` — presets; los de `:65-66` ("TQQQ x3", "TQQQ vs Gammalito") se **borran**.
- `CatalogoFamilia.cs:102/124/127/136` — `Banda = 20` en las series de QQQ ("un strike de QQQ son 41 pts de NQ", `:104`, `:125`) = medio strike del ETF × razón — PRODUCTO.
  - SPY: 1 USD × razón ES/SPY (≈ 10,08 con 7.725/766,28, SIN VERIFICAR) / 2 ≈ **±5**.
  - Banda 4,0 de TQQQ (`:170-187`): se borra.
- `CatalogoFamilia.cs:246` — `Calc` = las 24 primeras sin las TRES, más TOPN_NQ_vol, TOPN_NQ_oi, CARGA_NQ, CARGA_NDX y CARGA_QQQ, por ID literal. Los índices 0-20 no cambian si se sacan las T_ (van después de la 24).
- **Textos con escala o estadística de NQ** (en ES son falsos; el protocolo pide marcarlos SIN MEDIR):
  - Escala: "+-100 pts" `:100`; "41 pts de NQ" `:104`, `:125`; "<= 3 pts" `:107`; "NQ x20, NDX y QQQ x100, grilla de 5 pts" `:139`; "islas de 2-4 contratos" `:158`; "spot al sello − 900 s" `:166`, `:168`; "min(2 %, 100 pts)" `:193`, `:201`, `:215`, `:222`; "61 precios en ±3 %" `:210`; "centroide ±12 pts" `:215`; "124 pts por dólar" `:172`.
  - Estadísticas de NQ/NDX/QQQ: `:101`, `:105`, `:109`, `:113`, `:118`, `:129`, `:141`, `:144`, `:151`, `:159`, `:195`, `:199`, `:203`, `:212`, `:217`, `:224`.

### `_modulos/familia/fam/ExtrasCompuestos.cs`
- Nada atado al producto (solo interfaces) — IGUAL.

### `_modulos/familia/fam/MotorFamilia.cs`
- `MotorFamilia.cs:90` — `Carpeta` PythiaGex4; `:107` — `pythiagex4-familia.log` — renombre OK.
- `MotorFamilia.cs:93` `TandaMinutos = 240`, `:94` `RetrasoMinutoS = 6`, `:98` `EsperaCierreS = 60`, `:99` `PresupuestoMs = 2500`, `:100` `EsperaArranqueS = 90`, `:106` `RetencionDias = 14`, `:243` `SESIONES_PREVIAS = 2`, `:253` hasta 10 días hacia atrás, `:570` sin libros > 600 s, `:582` sin ticks > 120 s, `:638` rota el log a los 4 MB — IGUAL.
- `MotorFamilia.cs:191-195` — si cambia el contrato (roll), se abre la sesión de nuevo con el archivo de ese contrato — IGUAL.
- `MotorFamilia.cs:455` — busca "leyendo historia" en el `Estado` de CBOE; tiene que coincidir con el texto del módulo cboe — IGUAL.
- `MotorFamilia.cs:576` — `lb == "NQ" ? "NQ: sin foto vigente del libro de opciones de Rithmic"`; `:581`, `:583` "MNQ" — renombre OK (ES/MES).
- **TQQQ:** `:105` `TqqqDeNoche`; `:119`, `:130-132`, `:154`, `:168` (estado y `VelasTqqq`); `:183`, `:197`, `:221`; `:366-402` (bloque por vela de 2 min, protegido por `_tqqq != null`: con `null` queda inerte); `:434-435`; `:481-495` (`MismaVela`); `:553-555`, `:587-589` (`tqU`, `TqS/TqC`). `SesionFamilia.RuedaIniUtc` (`:368`) solo lo usa TQQQ.

### `_modulos/familia/fam/ReglasFam.cs` (las constantes VIVAS del motor)
- `ReglasFam.cs:72` — `RADIO = 100.0` — radio de ZTP y de CONF (`:214`, `:251`) — ESCALA. Python: `backtest_familia.py:147`.
- `ReglasFam.cs:73` — `ISLA = 0.10` — IGUAL. Python: `:148`.
- `ReglasFam.cs:74` — `CONF_PTS = 3.0` — confluencia de 2 o más libros a ≤ 3 pts (`:243`) — ESCALA o PRODUCTO.
  - Con strikes cada 5 pts, en ES igual que en NQ, 3 pts casi siempre junta dos libros vecinos.
  - Reescalarlo por precio (0,8 pts) haría la confluencia rara. Decidirlo con datos (SIN VERIFICAR).
  - Python: `:150`.
- `ReglasFam.cs:75` — `FAM_GRILLA = 5.0` — baldes de 5 pts para sumar la familia (`:263`, y `CambiosFamilia.cs:594`) — **PRODUCTO (paso de strike), no ESCALA.**
  - ES y SPX tienen strikes cada 5 pts (medido); los del SPY caen cada ~10 pts de ES.
  - Reescalarla por precio (≈ 1,3) separaría los libros en baldes distintos: la suma dejaría de sumar.
- `ReglasFam.cs:76` — `ZEST_MAX = 300.0` (`:228`) — ESCALA.
- `ReglasFam.cs:79-83` — `Escala(b)` = Mult/100; **si Mult no viene: `Libro == "NQ" ? 0,2 : 1`** — PRODUCTO, **CRÍTICO.**
  - `CambiosFamilia.cs:246` lo llama con `new LibroMinuto { Libro = lb }`, sin Mult: siempre cae en este caso por defecto.
  - Con el renombre, ES daría 0,2 en vez de **0,5**.
  - `Replica20.cs:659` también lo llama.
- `ReglasFam.cs:86` — `OiVale`: "NQ" → "ES" — renombre OK.
- `ReglasFam.cs:89`, `:112` — `R`/`Radio` = min(fut·0,02; 100) — ESCALA (lo que manda es el 100).
  - En ES, ±100 pts pasa la ventana densa del núcleo (±77, `CadenaApi.cs:129`): entra la zona de "un strike de cada dos".
  - Python: `extremos_rebote.py:196-197`.
- `ReglasFam.cs:111` — `TopNCantidad = 6` — IGUAL.
- `ReglasFam.cs:118` — `CargaMinima = 50.0` contratos — **PRODUCTO (liquidez).** En ES, SPX y SPY no filtra nada: todos los strikes cercanos tienen ≥ 50 (medianas medidas hoy: ES 1.052, SPX 4.872, SPY 16.351, contra NQ 38 y NDX 22). Lo pisa el ajuste del núcleo `CargaMinimaOi` (`GammaHoyTresRayas.cs:60-63`, default 50, rango 0-5000).
- `ReglasFam.cs:119` — `CargaTope = 12` — IGUAL. Lo pisa `CargaTopeRayas` (`GammaHoyTresRayas.cs:65-68`).
- `ReglasFam.cs:125`, `:140` — `CargaRadioPts = 0` (0 = `Radio(fut)`), techo `Math.Min(..., 300)` — ESCALA. El techo de 300 está atado a la ventana densa del NQ (±309); en ES sería cerca de 77. Lo pisa `CargaRadioPts` del núcleo (`GammaHoyTresRayas.cs:75-78`, `[Range(0, 300)]`).
- `ReglasFam.cs:132` — `CargaMinimaRayas = 10` — medido sobre un caso de NDX — IGUAL. Lo pisa `GammaHoyTresRayas.cs:70-73`.
- `ReglasFam.cs:302`, `:311-316` — claves dinámicas `"CARGA_"`/`"MUROS_"`/`"MAJORS_"`/`"ZTP_"`/`"ZEST_"`/`"TOPN_"` + `b.Libro` — **trampa del renombre.**
- `ReglasFam.cs:331-333` — FAM exige los tres libros "NQ", "NDX" y "QQQ" → ES, SPX y SPY — renombre OK si es consistente.
- `ReglasFam.cs:367` — strike = precio/Conv (razón) o precio − Conv (base), round 2 — IGUAL.

### `_modulos/familia/fam/SalidaFamilia.cs`
- `SalidaFamilia.cs:55` `"TRES_" + capa`; `:103` `CatalogoFamilia.PorId["TRES_" + capa]` — **trampa.** Si `CapasTres` y los IDs no coinciden, lanza `KeyNotFoundException`: falla `Avanzar` entero (`MotorFamilia.cs:443`) y la foto no se publica más.
- `SalidaFamilia.cs:100` — busca TRES hasta 29 min hacia atrás — IGUAL.
- `SalidaFamilia.cs:104` — `capa == "NQ" ? null : MetaDe(capa)` (la edad de la capa del futuro sale de la estela) — renombre OK.
- `SalidaFamilia.cs:175-177` — `TextoLibro`: "NQ" → "mismo contrato"/"corrimiento"; "NDX" → base "0.00"; si no, razón "0.0000" — renombre OK. La razón del SPY (≈ 10,08) con 4 decimales está bien.
- `SalidaFamilia.cs:183-185` — `Edad` — IGUAL.
- **TQQQ:** `:24` (parámetro `TqqqVela tq`), `:57-63`, `:68`, `:112-126` (`SeriesTqqq`, `"T_DOMS_raz"`), `:153`, `:163-164` (texto "x3: NQ = … x TQQQ + … (+-4)").

### `_modulos/familia/fam/SesionFamilia.cs`
- `SesionFamilia.cs:47-55` — paridad: día = fecha(t + 2 h) en UTC, [día − 2 h, día + 21 h), rueda 13:30 UTC; `:57-71` corregida: de 18:00 NY a 17:00 NY, rueda 09:30 NY; `:64-65` el fin de semana se muestra la sesión del viernes — IGUAL (Globex de ES). `RuedaIniUtc` (`:54`, `:70`) solo lo usa TQQQ.

### `_modulos/familia/nq/FotoNq.cs`
- `FotoNq.cs:28` — formatos de texto de viva3 (K "0.##", días "0.#####"…) — IGUAL.
- `FotoNq.cs:37`, `:53` — descarta la foto sin futuro y las filas con iv ≤ 0 o sin oi ni vol — IGUAL.
- `FotoNq.cs:71` — `Libro = "NQ"` — renombre OK → "ES".

### `_modulos/familia/nq/MinuteroNq.cs`
- **`MinuteroNq.cs:47` — `MULT = 20.0`** (C1, usado en `:158`) — **PRODUCTO, CRÍTICO.** ES = **50**. Si queda en 20, todos los montos de la familia en ES salen ×0,4 y la suma FAM queda mal pesada. Python: `backtest_familia.py:152`.
- `MinuteroNq.cs:39` — `VidaVivaS = 300` (la foto de Rithmic vale 5 min) — IGUAL. Python: `:154`.
- `MinuteroNq.cs:40` — `CorrMinimo = 30.0` pts — umbral para corregir el corrimiento de contrato cuando el libro está sobre otro trimestre que el gráfico — ESCALA.
  - La diferencia entre trimestres en NQ es cercana a 230 pts; en ES, cercana a 49 (cuenta con r = 3,75 %, q ≈ 1,2 % y 3 meses, SIN VERIFICAR con datos del roll).
  - 30 sigue detectándolo. Reescalarlo por precio (≈ 8) también funcionaría.
- `MinuteroNq.cs:41` — `CorrMuestrasMin = 5` — IGUAL.
- `MinuteroNq.cs:38` — `ZeroCadaMinuto = true` — IGUAL.
- `MinuteroNq.cs:152-153` — `ZeroEstandarFam.NQ` y caché `round(S/50)` — ESCALA (ver ZeroEstandar).
- `MinuteroNq.cs:46` `Libro => "NQ"`; `:158` `Armar("NQ", ...)` — renombre OK.
- `MinuteroNq.cs:168` — velas de −10 a +2 min; `:175` 240 s — IGUAL.
- `MinuteroNq.cs:7` — "MNQZ6 / NQZ6 → corr = 0": con el gráfico MES y el libro ES el precio es el mismo, así que corr = 0 — IGUAL.

### `_modulos/familia/nq/OiNq.cs` (regla C2: OI con fecha del libro de Rithmic)
- `OiNq.cs:35` — ventana `Ini` = día − 2 h (22:00 UTC), `Lim` = día + 3,5 h (03:30 UTC), `DosAm` = día + 2 h — PRODUCTO (horario del salto de OI de Rithmic). NQ: "medido 01:24-01:52 UTC en 11 noches" (`:3`, `:92`). ES: otro agente midió hoy de 01:29Z a 01:59Z (SIN VERIFICAR por mí); entra en la ventana.
- **`OiNq.cs:90` — si la primera foto de la noche es de [01:55, 13:30) UTC, se supone que el OI ya está al día** — PRODUCTO. Si en ES el salto puede llegar hasta las 01:59Z, una primera foto de 01:55 a 01:59 da por bueno el OI viejo (SIN VERIFICAR; convendría ~02:05). Python: `backtest_familia.py:462`.
- `OiNq.cs:37` — lunes y posferiados sin supresión — IGUAL.
- `OiNq.cs:61` — salto = ≥ 20 claves comunes y ≥ 50 % distintas — IGUAL (el libro de ES trae unas 179 filas, medido).
- `OiNq.cs:92` — texto "medido 01:24-01:52 UTC en 11 noches" — es una medición de NQ; en ES hay que cambiarla.
- Horas fijas en UTC: desde el 01-11 (horario de invierno) no sé si el salto se corre una hora (SIN VERIFICAR). Pasa igual en ES y en NQ.

### `_modulos/familia/nq/SeriesNq.cs`
- `SeriesNq.cs:4` — "20 USD/pt → ×0,2" (comentario) — ES 50 → ×0,5.
- `SeriesNq.cs:11` — `VISIBLES` con IDs `*_NQ_*` — nadie la llama en el indicador. Sin uso vivo.

### `_modulos/familia/posiciones/CambiosFamilia.cs`
- `CambiosFamilia.cs:47` — `pythiagex4-cambios.log` — renombre OK.
- `CambiosFamilia.cs:49-56` — presupuesto de 25 ms; umbrales 20 / 0,20 / 8 / 0,80 / 0,50 (iguales a `RegimenesOi`) — IGUAL. SIN VERIFICAR en SPX, que tiene una cadena mucho más ancha.
- `CambiosFamilia.cs:57-58` — `PublicacionCboeHastaNy = 09:30 NY` — PRODUCTO.
  - Medido en NDX de 02:26 a 02:36 NY, QQQ de 03:31 a 05:12 y TQQQ de 05:02 a 08:40 (`:400`).
  - SPX y SPY: otro agente midió 07:0x o 13:0x UTC, o sea 03:0x o 09:0x NY en horario de verano. Entra, con 20-30 min de margen (SIN VERIFICAR).
- `CambiosFamilia.cs:60` `TolStrike = 0.006`; `:124` `MIRAR_ATRAS_CONGELADA_S = 7200`; `:294` tolerancia W + máx(120; W/2); `:633` `VENTANAS_ALINEADAS_S = 90` — IGUAL.
- **`CambiosFamilia.cs:246`** — `PorRazon = lb == "QQQ"`; `Escala = ReglasFam.Escala(new LibroMinuto { Libro = lb })`: Mult = 0, así que **siempre** cae en el valor por defecto de `ReglasFam.cs:82`. En ES daría 0,2 en vez de 0,5 — **CRÍTICO.**
- `CambiosFamilia.cs:248`, `:252`, `:255`, `:285` (Black-76 si es el libro del futuro), `:321`, `:332`, `:347`, `:363`, `:425`, `:464`, `:702` — `lb == "NQ"` separa Rithmic de CBOE — renombre OK.
- `CambiosFamilia.cs:122` `Libros = {"NQ", "NDX", "QQQ"}`; `:160` `{"NQ", "NDX", "QQQ", "TQQQ"}`; `:231`, `:724` lo mismo — renombre OK y sacar TQQQ.
- `CambiosFamilia.cs:159` — `ConversionQqq(new OpcionesQqq{...})` (reglas de ETF para SPY: `FueraDeSesion`/`Vigencia`) — IGUAL de concepto.
- `CambiosFamilia.cs:593-594` — FAM: filtro del 3 % y balde de 5 pts (`FAM_GRILLA`) — ver ReglasFam.
- `CambiosFamilia.cs:635-653` — `MismaNoche` por sesión de CME (22:00 UTC): ES publica cerca de las 01:30Z y SPX/SPY a las 07/13Z, la misma sesión — IGUAL.
- **TQQQ** (dependencias de **compilación** del módulo tqqq): `:71` `PerfilK.De(TqqqPerfil.Perfil)`, `:284` `TqqqPerfil.Calcular`, `:266-277` `CalcularTqqq` (usa `FotosCambios.AhoraTqqq`). Además: `:45-46` `TqqqNoche`, `:107` `EsTqqq`, `:167`, `:179`, `:183`, `:208`, `:226-230` (parámetro `tq`).

### `_modulos/familia/posiciones/ClavesCambios.cs`
- `ClavesCambios.cs:15-21` — K en milésimas, vencimiento redondeado a 30 min (`:42`) — IGUAL (los strikes del SPY van de a 1 USD).

### `_modulos/familia/posiciones/FotosCambios.cs`
- `FotosCambios.cs:19` — `VIDA_NQ_S = 300`, `VIDA_CBOE_S = 1500` — IGUAL. `VIDA_TQQQ_S = 1500` se borra.
- `FotosCambios.cs:45-46` — textos "sin foto del libro NQ…" — renombre OK.
- `FotosCambios.cs:55` — `esQqq = libro == "QQQ"`: el SPY con las reglas del ETF y el SPX con las del índice (`CalendarioNdx.EntreSesiones`/`Congelada`/`ProximaApertura`, `:64`, `:70`, `:76`) — IGUAL de concepto (SPX = índice como NDX; SPY = ETF como QQQ).
- `FotosCambios.cs:69` — QQQ queda congelada si el dato NY es ≥ 15:59 — SPY: IGUAL (SIN VERIFICAR que se comporte igual que el QQQ en el after-hours).
- `FotosCambios.cs:107` — `libro == "NQ"` — renombre OK; `:110` −tol −60 s; `:115` −tol −1200 s — IGUAL.
- **TQQQ:** `:84-101` `AhoraTqqq`, que usa `TqqqSesion`: **dependencia de compilación**, hay que borrarla.

### `_modulos/familia/posiciones/RegimenesOi.cs`
- `RegimenesOi.cs:69` — salto con 20 claves y 0,20; libro flaco 8 y 0,80; dudoso 0,50 — IGUAL. Están medidos en NQ, NDX, QQQ y TQQQ (`:3-6`, `:10-16`). En SPX y SPY están SIN MEDIR: en NDX el salto cambia el 52-59 % de las claves, y en SPX no se sabe.
- `RegimenesOi.cs:3-4` — horarios medidos (NQ 01:24-01:48Z; NDX 06:26-06:36Z; QQQ 07:31-09:12Z). Es solo comentario: el detector no usa horarios.

---

### Trampas del renombre (transversales)

1. **IDs con guion bajo contra claves armadas en tiempo de ejecución.**
   - El catálogo tiene IDs literales: `MUROS_NQ_vol`, `TRES_NQ`, `R20_QQQ_vol`, `R10_NDX_zero`, `CARGA_NQ`… Las propiedades `S_*` de la pantalla se llaman igual, con `S_` adelante.
   - El código arma claves con el nombre del libro: `ReglasFam.cs:302/311-316`, `LibroMinutoArmado.cs:97-101`, `SalidaFamilia.cs:55/103`.
   - Con un regex `\bNQ\b`, el `_` cuenta como letra: **los IDs no cambian**, pero sí cambian los literales sueltos `"NQ"` (`MinuteroNq.cs:46/158`, `CapasTres`, `Libros`).
   - Entonces "MUROS_ES_vol" no está en el catálogo: `IndiceCalc` da −1 y la serie se tira en silencio. Con TRES salta un `KeyNotFoundException` (`SalidaFamilia.cs:103`) y se cae la foto entera. Hoy eso lo tapa el filtro de 15000.
   - **Hace falta que el límite trate `_` como separador** (por ejemplo `(?<![A-Za-z0-9])NQ(?![A-Za-z0-9])`), o no renombrar ningún literal.
2. **`AdaptadoresFamilia.cs:104` `"FGHJKMNQUVXZ"`.**
   - Reemplazado como subcadena queda "FGHJKMESUVXZ". H, M, U y Z siguen en la misma posición, así que el gráfico MESZ6 todavía da "Z6"; se pierden N y Q (julio y agosto).
   - Con `\b` o con el límite alfanumérico no se toca. Conviene excluirlo a propósito.
   - Otro agente vio la misma cadena en `CalendarioNdx`, `ConversionQqq`, `Replica20` y `ArmadoPantalla`.
3. **Identificadores camelCase que cruzan el borde** (están definidos o usados en archivos que NO se renombran):
   - `ILibroNq` (`Contratos.cs:57`, `GammaHoyTres.cs:1175`).
   - `FutMnq` (`Contratos.cs:85/199`).
   - `LibroNqFamilia` (definido en `AdaptadoresFamilia.cs:1009`; usado en `GammaHoyTres.cs:1168/1205` y `Viva3.cs:63`).
   - `ICalculoTqqq` e `IVersionSTqqq` (`Contratos.cs`).
   - Si las variantes `Nq`/`Mnq` se renombran solo en la familia, **no compila**. O se renombran también en `Contratos.cs`, `GammaHoyTres.cs` y `Viva3.cs`, o no se renombran.
   - `MinuteroNq`, `OpcionesNq`, `FotoNq`, `OiNq`, `ConversionQqq`, `CalendarioNdx` y los namespaces `Familia.Ndx`/`Familia.Qqq` viven solo en la familia, así que renombrarlos es consistente. Los arneses que los usan hay que adaptarlos.
4. **"TQQQ"**: reemplazado como subcadena queda "TSPY". Con `\b` queda "TQQQ". En los dos casos hay que borrarlo a mano.
5. **Minúsculas**: nunca reemplazar "nq" como subcadena, porque aparece en castellano ("aunque", "conquista"). Las minúsculas que importan son solo categorías de log ("ndx", "qqq").
6. **Números**: el renombre no toca `MULT = 20`, `FactorMonto3` (0,2), el valor por defecto de `ReglasFam.Escala` (0,2) ni `v > 15000`. Los cuatro siguen siendo de NQ: ver Lo más riesgoso.
7. **Coincidencias con otros componentes:**
   - `viva3-<raíz>`, `estela-<raíz>` y `cinta-<raíz>-<sesión>.csv` los escribe el núcleo con "ES": coinciden.
   - `estela-SPX`/`estela-SPY` no los escribe nadie todavía.
   - El archivo de CBOE "ES" = SPX (convención de cboe-local) coincide con `FuenteCapaEnMemoria` renombrado, siempre que el núcleo pida "ES" y "SPY".
   - El laboratorio es solo de NQ: `backtest_familia.py:152` (MULT) y `:158` (LIBROS); `preview_niveles.py:64` (`TICKER = {"NDX": "NQ", "QQQ": "QQQ"}`) y `:67` (VIDA_TRES); `extremos_rebote.py:476` (d > 15000). No hay referencia de ES para la paridad.
   - `herramientas/sembrar_4_0.py:10-14`, `:35`, `:41`, `:52` siembra solo NQ, NDX, QQQ y TQQQ en PythiaGex4.

### Ganchos del núcleo 3.0 que dejan a la familia sin datos en ES (fuera de mis archivos; sin esto no corre nada)
- **`GammaHoyTres.cs:1187`** — `AsegurarAdaptadoresFamilia`: `if (raiz != "NQ") return;`. En ES nunca se crean `CintaFamilia` ni `LibroNqFamilia`, y por eso `IntegracionFamilia.cs:62` vuelve sin hacer nada. El núcleo no se renombra: hay que cambiarlo a mano.
- **`Viva3.cs:63`** — `if (raiz == "NQ") Familia.LibroNqFamilia.Para(raiz).AgregarLinea(json, "vivo")`. En ES la foto de cada minuto nunca llega al libro de la familia; solo queda la siembra desde el archivo al arrancar.
- `GammaHoyTres.cs:755-762` — la estela con la raíz "ES" ya funciona. Las estelas de las capas tendrían que llamarse "SPX" y "SPY".
- `GammaHoyTres.cs:774-781` — llama a `FactorMonto3` con la raíz del núcleo; `GammaHoyNucleo.cs:149` tiene `MULT_INDICE = 100`.
- `GammaHoyTresRayas.cs:60-78` — ajustes `[Display]` del núcleo que pisan `ReglasFam.CargaMinima` (50, rango 0-5000), `CargaTope` (12), `CargaMinimaRayas` (10) y `CargaRadioPts` (0, rango 0-300). Los defaults y las descripciones son de NQ.
- `CadenaApi.cs:129/131` — ventana densa 1 % y rala 2 % (en ES, ±77 y ±154 pts); `CadenaApi.cs:905-906` — MES/ES → "ES", correcto.

### TQQQ: qué borrar o desenganchar en estos archivos para que compile sin `_modulos/tqqq`
- **Rompen la compilación:**
  - `IntegracionFamilia.cs:184-196` (`CalculoTqqq`/`OpcionesTqqq`/`CierresTqqqDia`).
  - `CambiosFamilia.cs:71` y `:284` (`TqqqPerfil`), `:266-277`.
  - `FotosCambios.cs:84-101` (`TqqqSesion`).
- **Quedan inertes si se pasa `tqqq = null`** (conviene limpiarlos igual):
  - `MotorFamilia.cs:366-402` y el resto que se listó.
  - `SalidaFamilia.cs:57-63`, `:112-126`, `:163-164`.
  - `IntegracionFamilia.cs:339-342`.
  - `CatalogoFamilia.cs:39`, `:55`, `:65-66`, `:169-190` (las 6 T_*).
  - `IntegracionFamilia.cs:46-48` (el ajuste `Tqqq41AnclaNoche`).
- Los tipos `TqqqVela`/`ICalculoTqqq`/`IVersionSTqqq` y `FotoFamilia.TqS/TqC` de `Contratos.cs` pueden quedar: no dependen del módulo. Los usa `ArmadoPantalla` (el doble eje TQQQ, otro inventario).

### Catálogo de series (`CatalogoFamilia.cs`, 41 series; default = propiedad `S_<id>` de `FamiliaCuatroPantalla.cs:40-97`)
Formato: id — corto — libro/fuente/tipo — grupo — color — banda — default — qué sería en ES.
- `:98` MUROS_NQ_vol — "NQ" — NQ/vol/MUROS — Recomendadas — #4db5e4 — 0 — apagada — muros del 0DTE de **ES** (Rithmic) por volumen.
- `:102` MAJORS_QQQ_oi — "QQQ" — QQQ/oi/MAJORS — Recomendadas — #e2b872 — **20** — apagada — majors del **SPY** por OI (banda ≈ ±5).
- `:106` CONF_vol — "CONF" — familia/vol/CONF — Recomendadas — #f3d35a — 0 — apagada — confluencia ES/SPX/SPY.
- `:110` MAJORS_NQ_vol — "NQ" — NQ/vol/MAJORS — Recomendadas — #6cc3ff — 0 — apagada — majors del 0DTE de ES por volumen.
- `:115` MUROS_NQ_oi — "NQ" — NQ/oi/MUROS — Familia — #9a98d6 — 0 — **prendida** — muros de ES por OI.
- `:119` MUROS_NDX_vol — "NDX" — NDX/vol/MUROS — Familia — #2fb3a8 — 0 — **prendida** — muros del **SPX** por volumen (con base).
- `:122` MUROS_NDX_oi — NDX/oi/MUROS — Familia — #8de031 — 0 — apagada — SPX por OI.
- `:124` MUROS_QQQ_vol — QQQ/vol/MUROS — Familia — #dabe6f — **20** — apagada — SPY por volumen.
- `:127` MUROS_QQQ_oi — QQQ/oi/MUROS — Familia — #ff9f43 — **20** — apagada — SPY por OI.
- `:130` MAJORS_NQ_oi — NQ/oi/MAJORS — Familia — #b9a4ff — 0 — apagada — ES por OI.
- `:132` MAJORS_NDX_vol — NDX/vol/MAJORS — Familia — #4fd1c5 — 0 — apagada — SPX por volumen.
- `:134` MAJORS_NDX_oi — NDX/oi/MAJORS — Familia — #a3e635 — 0 — apagada — SPX por OI.
- `:136` MAJORS_QQQ_vol — QQQ/vol/MAJORS — Familia — #f6c177 — **20** — apagada — SPY por volumen.
- `:138` FAM_MUROS_vol — "FAM" — familia/vol/MUROS — Familia — #d04ad0 — 0 — apagada — familia ES+SPX+SPY sumada (ES ×0,5, SPX/SPY ×1; grilla de 5).
- `:142` FAM_MUROS_oi — familia/oi/MUROS — Familia — #e879f9 — 0 — apagada — lo mismo por OI.
- `:146` ZEST_NQ_vol — NQ/vol/ZEST — Zero — #c9d4e3 — 0 — apagada — zero estándar de ES por volumen.
- `:150` ZEST_NQ_oi — NQ/oi/ZEST — Zero — #94a3b8 — 0 — apagada — zero estándar de ES por OI.
- `:152` ZEST_NDX_vol — NDX/vol/ZEST — Zero — #7dd3fc — 0 — apagada — zero estándar del SPX.
- `:154` ZEST_QQQ_vol — QQQ/vol/ZEST — Zero — #fcd34d — 0 — apagada — zero estándar del SPY.
- `:156` ZTP_NQ_vol — NQ/vol/ZTP — Zero — #f472b6 — 0 — apagada — cruces limpios de ES.
- `:160` ZTP_NDX_vol — NDX/vol/ZTP — Zero — #fb7185 — 0 — apagada — cruces limpios del SPX.
- `:163` TRES_NQ — "3.0" — NQ/3.0/TRES — 3.0 — #8a96a8 — 0 — apagada — D1/D2 de la 3.0 interna de ES (estela-ES): **hoy vacía por el filtro de 15000**.
- `:165` TRES_NDX — "3.0 NDX" — NDX/3.0/TRES — 3.0 — #5f6b7d — 0 — **prendida** — capa SPX de la 3.0: **no existe en ES** (el núcleo no tiene capas).
- `:167` TRES_QQQ — "3.0 QQQ" — QQQ/3.0/TRES — 3.0 — #7c6f57 — 0 — apagada — capa SPY de la 3.0: no existe.
- `:170` T_DOMS_vol, `:174` T_MUROS_vol, `:177` T_MUROS_oi (**prendida**), `:180` T_ZERO_oi, `:183` T_ZERO_vol_pm (**prendida**), `:187` T_DOMS_raz — TQQQ ×3 (banda 4,0, salvo T_DOMS_raz con 0) — **se borran**.
- `:192` R20_QQQ_vol — "2.0 QQQ" — QQQ/vol/DOMS — 2.0 — #cc8800 — 0 — **prendida** — "2.0 SPY": según la sección 7, la 2.0 **no dibuja SPY en ES** (no hay original contra el que comparar).
- `:196` R20_NDX_vol — "2.0 NDX" — NDX/vol/DOMS — 2.0 — #3ddc97 — 0 — **prendida** — "2.0 SPX": en ES la 2.0 usa el SPX como libro primario, con base aditiva (sección 7).
- `:200` DOMS_QQQ_vol — "QQQ dom" — QQQ/vol/DOMS — 2.0 — #5b8cff — 0 — apagada — selección de la 2.0 sobre el libro SPY de la 5.0.
- `:204` DOMS_NDX_vol — "NDX dom" — NDX/vol/DOMS — 2.0 — #a9c4ff — 0 — apagada — selección de la 2.0 sobre el libro SPX.
- `:209` R10_NDX_zero — "Clasica NDX 0Γ" — NDX/vol/ZEST — Clasica — #ffffff — 0 — **prendida** — 0Γ de la clásica sobre SPX (sección 7).
- `:214` R10_NDX_dom — "Clasica NDX" — NDX/vol/DOMS — Clasica — #b8f8d8 — 0 — **prendida** — D1-D3 de la clásica sobre SPX.
- `:221` TOPN_NQ_vol — "NQ top" — NQ/vol/TOPN — Recomendadas — #7cf5c4 — 0 — **prendida** — los 6 strikes de ES con más gamma por volumen.
- `:225` TOPN_NQ_oi — "NQ top OI" — NQ/oi/TOPN — Recomendadas — #4fb98a — 0 — apagada — lo mismo por OI.
- `:230` CARGA_NQ — "NQ" — NQ/oi/TOPN — Recomendadas — #ffb74d — 0 — **prendida** — carga de OI de ES. **Con el umbral de 50 marca todo, hasta el tope de 12.**
- `:234` CARGA_NDX — "NDX" — NDX/oi/TOPN — Recomendadas — #ce93d8 — 0 — apagada — carga del SPX.
- `:238` CARGA_QQQ — "QQQ" — QQQ/oi/TOPN — Recomendadas — #80cbc4 — 0 — apagada — carga del SPY.

### Lo más riesgoso
1. **`AlmacenTres.cs:136` (`v > 15000`)**: con ES cerca de 7.725 tira todas las rayas TRES. Hoy, aunque el núcleo ya escribe `estela-ES`, TRES_ES saldría vacía.
2. **`MinuteroNq.cs:47` (`MULT = 20`), `ReglasFam.cs:82` (0,2 por defecto) y `AlmacenTres.cs:182` (`FactorMonto3` 0,2)**: en ES van 50, 0,5 y 0,5.
   - El renombre no toca números. Los montos, la suma FAM y los cambios por nivel (que **siempre** usan el valor por defecto, `CambiosFamilia.cs:246`) saldrían ×0,4.
3. **La forma del renombre**: con `\b`, los IDs con guion bajo no cambian y los literales `"NQ"` sí. Las series se pierden en silencio y TRES tira `KeyNotFound`, lo que mata la foto entera (`SalidaFamilia.cs:103`).
   - Además, `ILibroNq`, `FutMnq` y `LibroNqFamilia` cruzan hacia `Contratos.cs` y el núcleo: si se renombran solo de un lado, no compila.
4. **Los ganchos del núcleo** `GammaHoyTres.cs:1187` y `Viva3.cs:63` (`raiz == "NQ"`): sin cambiarlos a mano, la familia nunca arranca en ES.
5. **Radios en puntos** (`ReglasFam.cs:72/76/89/112`, `Seleccion.cs:31/33/37`, `ZeroEstandar.cs:29-30`): el tope de 100 pts manda tanto en NQ como en ES. En ES pasa la ventana densa de ±77 del núcleo, donde la cadena trae un strike de cada dos.
   - En cambio, `FAM_GRILLA` (5) y quizás `CONF_PTS` (3) dependen del **paso de strike**, que en ES/SPX es el mismo que en NQ (5 pts). Esos no se reescalan por precio.
6. **`CargaMinima` = 50 contratos (`ReglasFam.cs:118` y `GammaHoyTresRayas.cs:60-63`)**: en ES, SPX y SPY no filtra nada (medianas de 1.052, 4.872 y 16.351 contratos por strike, contra 38 en NQ).
7. **`OiNq.cs:90` ("01:55")**: si el salto de OI de ES llega hasta las 01:59Z, una primera foto de las 01:55-01:59 da por bueno el OI de anteayer.
8. **Los textos del catálogo con estadísticas de NQ** (por ejemplo `CatalogoFamilia.cs:101`, `:105`, `:109`): en ES tienen que decir SIN MEDIR.

---

## D. Puntos 1, 2, 3 y 6 por archivo: cboe, ndx (→ SPX) y qqq (→ SPY)

> Las "trampas del renombre" de esta sección se evaluaron contra dos variantes, `\b` y subcadena. Contra el borrador `herramientas/clonar_5_0.py` valen las conclusiones de B.

Alcance: `_modulos/cboe/*.cs` (6), `_modulos/familia/ndx/*.cs` (3) y `_modulos/familia/qqq/*.cs` (3), leídos enteros. Rutas relativas a
`PythiaGex\atas\PythiaGexCuatro\`. Ojo: el código ya es posterior a la 4.1.5e del README (tiene notas 4.1.7, 4.1.8 y 4.1.9 del 12/13-10).
Clasificación: **ESCALA** (depende del precio o la volatilidad: se reescala con el factor que mide otro agente), **PRODUCTO** (hecho del
producto), **IGUAL** (no depende del producto). **MEDIDO** = lo saqué hoy (27-10) de los archivos con `python -I`, scripts en
`scratchpad\inv\m_*.py`. Lo no confirmado dice SIN VERIFICAR.
Contexto medido para escalar: ES/NQ ≈ 0,262 (MES 7.713,95 contra MNQ 29.426,14: spot + base_cruda, cboe-local 27-10 02:49-03:39 UTC);
SPY/QQQ ≈ 1,073 (766,28 contra 714,44); MES/SPY ≈ 10,07 (contra MNQ/QQQ ≈ 41,19).

---

### `_modulos/cboe/BajadorCboe.cs`
**(1) Constantes**
- `BajadorCboe.cs:42` — `TopeKBps = 1250` — tope de bajada (pausa = trozo / tope) — IGUAL (pero ver (3): la cadena de SPX pesa ~2,3× la de NDX).
- `BajadorCboe.cs:43-44` — `TrozoBytes = 64 KB`, `BufferSocketBytes = 64 KB` — trozo y SO_RCVBUF anti-ráfaga — IGUAL.
- `BajadorCboe.cs:46` — `CadenciaRuedaS = 75, CadenciaFueraS = 300` — cadencia por ticker — IGUAL (MEDIDO: SPX llega a 47-48 fotos/h en la rueda, igual que NDX, 23-10).
- `BajadorCboe.cs:47-50` — `PausaEntreTickersMs 3000`, `FallosParaEsperar 3 / EsperaTrasFallosS 300`, `EsperaCloudflareS 900`, `RetrasoInicialS 15` — IGUAL.
- `BajadorCboe.cs:51-53` — `DiasHistoria 7`, `HorasConFilas 30`, `RetencionDias 14` — historia, poda de filas y de archivos — IGUAL (MEDIDO: una línea flaca de SPX tiene 750-893 filas contra 929 de NDX, y SPY 462 contra 363 de QQQ: la memoria de "~170 MB por 8 días" de L12 no cambia de orden).
- `BajadorCboe.cs:55` — `SeguirCadaS 30` — IGUAL.
- `BajadorCboe.cs:438` — `cr.N <= 100` → rechaza la cadena — IGUAL (SPX trae 28.492 contratos, SPY 13.160: MEDIDO).
- `BajadorCboe.cs:734` — `edad > 30 min → "(VIEJO)"` — protocolo — IGUAL.

**(2) Producto/calendario**
- `BajadorCboe.cs:242-251` — `EnRueda`: 13:20-20:10 UTC (paridad) / 09:20-16:10 NY lun-vie — IGUAL (SPX y SPY tienen la misma rueda que NDX/QQQ).
- `BajadorCboe.cs:595-602` — `Corte`: 09:00 NY del día hábil anterior, sin feriados — IGUAL.
- `BajadorCboe.cs:60` — `HoyLocal` = fecha LOCAL de la PC (Python `date.today()`) para elegir el trimestral en `Medir` — IGUAL.

**(3) Rutas/archivos/mutex/hilos/logs/símbolos**
- `BajadorCboe.cs:31` — `Carpeta = %APPDATA%\ATAS\PythiaGex4\cboe` → `PythiaGex5\cboe` con el renombre — queda BIEN.
- `BajadorCboe.cs:39` — `CarpetaExtra = %APPDATA%\ATAS\PythiaGex\cboe-local` (4.1.9, solo lectura; la escribe `herramientas/cboe_local.py`) — NO hay que renombrarlo (no dice "PythiaGex4", el renombre no lo toca: BIEN). Ahí YA están SPX y SPY: `cadena-ES-*` (=SPX) y `cadena-SPY-*`, 38 archivos de cada uno (MEDIDO); `cboe_local.py:34` baja `["ES","NQ","QQQ","SPY","TQQQ"]` cada 75/300 s.
- `BajadorCboe.cs:40` — `Log = %APPDATA%\ATAS\pythiagex4-cboe.log` → `pythiagex5-cboe.log` — BIEN.
- `BajadorCboe.cs:54` — `NombreMutex = Local\PythiaGex4.BajadorCboe` → `PythiaGex5` — BIEN (mutex propio: la 4.1 y la 5.0 bajan cada una lo suyo). Riesgo operativo SIN VERIFICAR: con las dos en el mismo ATAS hay DOS descargadores (cada uno con su tope de 1250 KB/s) más `cboe_local.py` afuera bajando los cinco; CLAUDE.md liga las ráfagas a cortes de Rithmic. Por bajada (MEDIDO): SPX gz 1.830.968 B / JSON 12,7 MB; NDX gz 0,81-0,90 MB; SPY gz 834.119 B; QQQ gz 0,72-0,74 MB.
- `BajadorCboe.cs:56` — `Libros = { "NDX", "QQQ", "TQQQ" }` — con el renombre queda `{ "SPX", "SPY", "TQQQ" }` (con \b) o `{ "SPX","SPY","TSPY" }` (subcadena) — MAL en los dos casos: tiene que quedar `{ "SPX", "SPY" }` (TQQQ se borra). IntegracionFamilia.cs:167 usa este default.
- `BajadorCboe.cs:141` — `case "NDX": case "NQ": Nombre "NDX", Archivo "NQ", Simbolo "_NDX"` — con \b queda `Nombre "SPX", Archivo "ES", Simbolo "_NDX"`: **MAL, el más peligroso**: `\bNDX\b` no entra en `_NDX` (el `_` es letra de palabra). La 5.0 bajaría NDX de CBOE y lo guardaría como SPX en `cadena-ES-*` propios, mezclado con los `cadena-ES-*` de cboe-local (SPX real) que lee al arrancar. Tiene que quedar `Simbolo = "_SPX"` (es el de `archivar_cadena.py:126`, `INDICE = {"ES": "_SPX", ... "SPY": "SPY"}`). `Archivo = "ES"` sí es el correcto para leer cboe-local.
- `BajadorCboe.cs:142` — `QQQ → Nombre/Archivo/Simbolo "QQQ"` → `"SPY"` — BIEN.
- `BajadorCboe.cs:143` — `TQQQ` — borrar la rama.
- `BajadorCboe.cs:132` — `_porNombre` indexa Nombre, Archivo y Símbolo: después del renombre acepta "SPX", "ES", "_NDX"(!) y "SPY". Cualquier módulo que pida "NQ"/"NDX"/"QQQ" sin renombrar recibe vacío (ver Trampas).
- `BajadorCboe.cs:155` — hilo `"PythiaGex4 cboe"` → `"PythiaGex5 cboe"` — BIEN.
- `BajadorCboe.cs:2-5, 226` — comentarios con `_NDX`, NDX/QQQ/TQQQ y `TRES_NDX / TRES_QQQ` — cosmético.

**Trampas del renombre**
- `"_NDX"` (L141) no lo toca un regex con \b: arreglarlo a mano. No hay ninguna cota de base que ataje el error (ver ConversionNdx): la raya saldría dibujada y corrida.
- `"TQQQ"` (L56, L143): con reemplazo de subcadena da `"TSPY"`; con \b queda TQQQ. Borrarlo, no renombrarlo.

---

### `_modulos/cboe/CboeArchivo.cs`
**(1) Constantes**
- `CboeArchivo.cs:193` — `last_trade_time >= 15:59 NY` = "tras el cierre" en `cierres.json` — IGUAL.
- `CboeArchivo.cs:199-200` — valida `prev_day_close` con `|current - pdc - change| < 0,01` — IGUAL.
- `CboeArchivo.cs:217` — guarda 40 fechas en `cierres.json` — IGUAL.

**(3) Rutas/archivos**
- `CboeArchivo.cs:27-30` — `cadena-<Archivo>-<día UTC>.jsonl.gz`, `ultima-<Archivo>.json`, `cadena-<Archivo>.ultimo`, `cierres.json` — con Archivo "ES"/"SPY" quedan los mismos nombres que cboe-local (`archivar_cadena.py:160,163,166`) — BIEN.
- `CboeArchivo.cs:24` — `RxDia = ^cadena-(NQ|QQQ|TQQQ)-(\d{4}-\d{2}-\d{2})\.jsonl\.gz$` (la poda) — con \b queda `(ES|SPY|TQQQ)`: BIEN si el script entra a los regex; si no los toca, la 5.0 NUNCA poda sus SPX/SPY propios (~4,5 MB/día SPX + ~2,4 MB/día SPY, MEDIDO en cboe-local). Sacar `TQQQ`.
- `CboeArchivo.cs:2-3` — comentarios `PythiaGex4\cboe\` y `ultima-<NQ|QQQ|TQQQ>.json` — cosmético.

---

### `_modulos/cboe/CboeCadena.cs`
**(1) Constantes**
- `CboeCadena.cs:115` — `Ancho = 0.05` — strikes a ±5 % del spot en `construir` — IGUAL (relativo; MEDIDO: SPX deja 138-155 filas por vencimiento con strikes de 5 pts).
- `CboeCadena.cs:116` — `DiasMax = 14` — horizonte de construir — IGUAL.
- `CboeCadena.cs:117` — `DiasMaxArchivo = 8.0` — la línea flaca guarda 0 ≤ días ≤ 8 — IGUAL.
- `CboeCadena.cs:118` — `RetrasoS = 902` — t_dato de respaldo = ts − 902 s — IGUAL (mismo retraso de CBOE para SPX/SPY).
- `CboeCadena.cs:222` — `Medir(n = 12, maxVencimientos = 10, tick = 0.25)` — 12 strikes más cercanos por forward, hasta 10 vencimientos, residuo en ticks — IGUAL (PRODUCTO: tick de ES = 0,25, igual que NQ: `pythiagex/tablero.py:19-20`). Con strikes de 5 pts, los 12 de SPX cubren ±30 pts (≈0,4 %) contra ±60 de NDX: SIN VERIFICAR si cambia la calidad del forward.
- `CboeCadena.cs:264` — `< 3 puntos → sin base` — IGUAL.

**(2) Producto/calendario**
- `CboeCadena.cs:147` — días = (vencimiento a las 16:00 NY − ahora) para TODOS los contratos (vía `CboeCrudo.VencUtc`) — PRODUCTO: las mensuales AM (SPX del tercer viernes, y NDX) vencen a la apertura, no 16:00. Ver CboeCrudo.
- `CboeCadena.cs:158` — la fila es `(K, fecha)`: suma OI y volumen de TODAS las raíces del mismo día y la IV queda la del último contrato leído — PRODUCTO: el tercer viernes junta SPX (AM, mensual) y SPXW (PM). MEDIDO en el crudo `datos/cache/_SPX-20260903-220806.json.gz`, vencimiento 18-09: SPX n=1142 OI 5.207.673 + SPXW n=904 OI 423.612, 444 strikes en común. MEDIDO en cboe-local, vencimiento 16-10 (±5 %): jueves 15-10 19:00 UTC OI 1.341.550; 23:30 UTC 1.139.457; viernes 16-10 03:00 UTC 329.164 (la AM ya no está). O sea: la víspera, hasta ~medianoche UTC, el "mañana" de SPX trae ~3,5× el OI real de la PM y con 6,5 h de vida de más. (En NDX pasa lo mismo, 18-09: NDX AM OI 49.476 contra NDXP 812; ahí la AM se fue entre 13:30 y 14:00 UTC del viernes 16-10: un solo caso, no concluyente.)
- `CboeCadena.cs:198-216` — `TercerViernes` y `ContratoVigente` = trimestral cuyo roll (tercer viernes − 8 días) no llegó — IGUAL (ES y NQ ruedan el mismo día; `pythiagex/base.py:53-69`).
- `CboeCadena.cs:231` — sin vencimiento el tercer viernes del trimestral en la cadena → sin base — IGUAL (SPX lo tiene: SPX AM + SPXW).
- `CboeCadena.cs:237` — "(K, lado) repetido pisa": en el forward del trimestral la AM y la PM se pisan entre sí — PRODUCTO, efecto chico SIN VERIFICAR.
- `CboeCadena.cs:8, 349` — escribe `"base":null,"base_confiable":false` siempre (la confianza pide la curva del Tesoro) — IGUAL (también en ES).

---

### `_modulos/cboe/CboeCrudo.cs`
**(1) Constantes**
- `CboeCrudo.cs:3` — comentario "NDX trae ~16.000" contratos — MEDIDO: NDX 15.196, SPX 28.492, SPY 13.160 — cosmético.
- `CboeCrudo.cs:207` — strike = int(8 dígitos) / 1000 — IGUAL.

**(2) Producto/calendario**
- `CboeCrudo.cs:188-208` — `ParseOcc` a mano (`^([A-Z]+)(\d{2})(\d{2})(\d{2})([CP])(\d{8})$`): acepta cualquier raíz de mayúsculas, así que SPX, SPXW y SPY entran (MEDIDO: 61 raíz-fecha en el crudo de SPX, 0 sin parsear). La raíz se DESCARTA: no hay forma de separar AM de PM ni de excluir la mensual — PRODUCTO.
- `CboeCrudo.cs:217` — `VencUtc` = 16:00 NY de la fecha del símbolo para todo — PRODUCTO (ver CboeCadena:147,158). El Python hace lo mismo a sabiendas (`pythiagex/base.py:339-355`): la paridad se mantiene, el error en SPX es mayor.

---

### `_modulos/cboe/CboeUtil.cs`
**(1) Constantes**
- `CboeUtil.cs:223` — log con tope de 10 MB y un respaldo `.1` — IGUAL.
**(2) Producto/calendario**
- `CboeUtil.cs:158-176` — horario de verano de EE. UU. por fecha y cierre 16:00 NY = 20/21 UTC — IGUAL.
**(3) Rutas**
- `CboeUtil.cs:216` — comentario `pythiagex4-cboe.log` — cosmético (la ruta real es BajadorCboe.cs:40).

---

### `_modulos/cboe/HttpCboe.cs`
**(1) Constantes**
- `HttpCboe.cs:48-49` — `TopeSegundos 60`, `TopeGz 64 MB`, `TopeJson 256 MB` — IGUAL (SPX: 1,8 MB gz / 12,7 MB JSON, MEDIDO).
- `HttpCboe.cs:53-54` — buffers iniciales 1 MB gz / 8 MB JSON (crecen solos) — IGUAL (SPX hace crecer los dos una vez).
- `HttpCboe.cs:66` — `ConnectTimeout 15 s` — IGUAL.
**(3) Símbolos/URL**
- `HttpCboe.cs:45-46` — `https://cdn-api.cboe.com` (respaldo `cdn.cboe.com`) + `/api/global/delayed_quotes/options/{0}.json` — IGUAL; el `{0}` para ES tiene que ser `_SPX` y `SPY` (lo pone BajadorCboe.cs:141-142).

---

### `_modulos/familia/ndx/CalendarioNdx.cs`
**(2) Producto/calendario**
- `CalendarioNdx.cs:70-74` — sesión de CME: día = fecha(t + 2 h), 22:00-21:00 UTC (paridad) / 18:00-17:00 NY — IGUAL (Globex de ES = Globex de NQ).
- `CalendarioNdx.cs:78-82` — `EntreSesiones` 21:00-22:00 del reloj (pausa de CME) — IGUAL.
- `CalendarioNdx.cs:86-92` — `ContratoParidad`: U6 hasta la sesión del 14-09-2026, Z6 hasta la del 17-12-2026, después H7 (tabla fija del Python, solo el arnés) — IGUAL (mismos códigos de ES); SIN VERIFICAR que la continua de MES de ATAS ruede la misma sesión que la de MNQ (la de MNQ rodó el 15-09).
- `CalendarioNdx.cs:95-104` — `ExpiraParidad`: U6 18-09-2026 13:30 UTC, Z6 18-12-2026 14:30 UTC, H7 19-03-2027 14:30 UTC (error del Python: ya es horario de verano) — IGUAL (ES vence igual que NQ: tercer viernes, SOQ de la apertura).
- `CalendarioNdx.cs:108-116` — `ExpiraContrato`: tercer viernes 09:30 NY — IGUAL. `:114` corrige el año con +10 si queda 5 años atrás — IGUAL.
- `CalendarioNdx.cs:121-132` — `ContratoPorFecha` (continua sin mes): roll = tercer viernes − 8 días; `"HMUZ"` (L128) — IGUAL.
- `CalendarioNdx.cs:152-158` — `ProximaApertura` 13:30 UTC / 09:30 NY — IGUAL.
- `CalendarioNdx.cs:161` — `CierreRueda` 20:00 UTC / 16:00 NY (decaimiento del carry de C7) — IGUAL.
- `CalendarioNdx.cs:165-171` — `EnHora` 09:35-15:59 NY lun-vie; feriados solo si se pasa `esHabil` — IGUAL. `IntegracionFamilia.cs:170-174` NO pasa `EsHabilNy`: sin feriados (igual en ES).
- `CalendarioNdx.cs:174` — `Congelada` = t_dato NY ≥ 15:59 — IGUAL: MEDIDO 23-10 y 27-10, SPX de noche tiene `ultimo_trade` clavado en 16:14:59 NY y `spot_idx` clavado desde las 16:00 NY (no aparece la sesión GTH de CBOE), igual que NDX.
**Trampas del renombre**
- `CalendarioNdx.cs:112` y `:146` — `"FGHJKMNQUVXZ"` (códigos de mes) contiene `NQ`: con reemplazo de subcadena queda `"FGHJKMESUVXZ"`. H/M/U/Z siguen en su lugar (los trimestrales se leen bien), pero julio y agosto se rompen y "E"/"S" pasan a ser meses válidos. Con \b no se toca. Excluirlo del renombre.
- Comentarios con `MNQZ6` (L106, L141) — cosmético.

---

### `_modulos/familia/ndx/ConversionNdx.cs`
**(1) Constantes**
- `ConversionNdx.cs:113` — `RETRASO_S = 900` (t_spot = ts − 900 s), `VIVO_S = 600`, `VIDA_CBOE_S = 1500` — IGUAL.
- `ConversionNdx.cs:114` — `MIN_DIA = 5`, `MIN_RUEDA = 20`, `VENTANA = 24` (cantidades de fotos) — IGUAL (la cadencia de SPX es la misma).
- `ConversionNdx.cs:115` — `PISO = 0.5` pts — piso del MAD de la mediana robusta de la base (`backtest_familia.py:277,285-286`) — ESCALA (el ruido de la muestra de base en puntos es menor en ES; factor SIN VERIFICAR).
- `ConversionNdx.cs:186-195` — oi_fresco: franjas 13:30 / 22:00 / 03:00 del reloj, hueco > 6 h, salto de OI total > 5 % (L188, L190, L192) — IGUAL (solo rotula). MEDIDO: el OI de SPX cambió entre 00:25 y 13:03 UTC el 23-10 (1.102.349 → 1.340.433): adentro de la franja.
- `ConversionNdx.cs:42` — `DiasHistoria = 6` (preview_niveles.DIAS_CBOE) — IGUAL.
- `ConversionNdx.cs:45, 48, 52, 61` — `TandaZeroMin 240`, `CalentarCacheZero true`, `ZeroCacheConHorizonte false`, `ZeroCadaMinuto true` — IGUAL.
- Cotas de base: NO HAY ninguna (ni mínimo ni máximo, ni contra `base_cruda`). Si entra la cadena equivocada (p. ej. NDX rotulada SPX, ver BajadorCboe:141) la base sale ≈ −21.600 y se usa igual. Para ES convendría una cota (SIN VERIFICAR el valor). La base_cruda de SPX medida el 23-10 fue de 31,9 a 47,7 pts (casi siempre 36-38) y el 27-10 de 37,2, contra 152,7 de NDX.

**(2) Producto/calendario**
- `ConversionNdx.cs:144, 156` — muestra = precio del MNQ en t_spot − spot del índice (base aditiva) — IGUAL en ES (MES − SPX).
- `ConversionNdx.cs:159-170` — modo día: vivo, en hora (09:35-15:59) y ≥ 5 muestras → mediana robusta de las últimas 24 — IGUAL.
- `ConversionNdx.cs:173-184, 221-234` — modo noche: la rueda más reciente con ≥ 20 muestras × (EXP − t) / (EXP − cierre de esa rueda); se descarta si el contrato de la rueda no es el del gráfico (roll) — IGUAL (sin tasa ni dividendo: el carry baja proporcional al tiempo).
- `ConversionNdx.cs:197-204` — C9: vale 1500 s; congelada → hasta la próxima apertura — IGUAL (MEDIDO: SPX congela igual que NDX).
- `ConversionNdx.cs:68` — contrato: el del código del gráfico (`MESZ6` → `Z6`) o, en la continua, por fecha — IGUAL.

**(3) Rutas/archivos**
- `ConversionNdx.cs:258` y `:325` — `familia\muestras-NDX-<sesión>.jsonl` (escribe y poda) → `muestras-SPX-` con \b (el `-` corta palabra) — BIEN y consistente; `:328` exige largo ≥ 23 y "muestras-SPX-AAAA-MM-DD" también mide 23 — BIEN. Sin historia hasta que se junte (el `sembrar_4_0.py` del README es de NQ: SIN VERIFICAR qué haría la 5.0).
- `ConversionNdx.cs:219, 223, 235` — textos "NDX no se dibuja", "(roll)", "mediana de la rueda" — cosmético.

---

### `_modulos/familia/ndx/LibroMinuteroNdx.cs`
**(1) Constantes**
- `LibroMinuteroNdx.cs:38` — `MULT = 100.0` — USD por punto del índice — PRODUCTO: SPX = 100 (seguro).
- `LibroMinuteroNdx.cs:39` — `TANDA_MIN = 240` — IGUAL.
- `LibroMinuteroNdx.cs:190` — tramo de 900 s de la clave de la caché del zero — IGUAL.
- `LibroMinuteroNdx.cs:191` — `round(S / ZeroEstandarFam.NDX.CentroPaso)` = round(S/50) (clave de la caché; solo pesa con `ZeroCadaMinuto = false`) — ESCALA.
- `LibroMinuteroNdx.cs:194-195` — `ZeroEstandarFam.NDX` = paso 2,5 / semi ±300 / centro 50 (definido en `_modulos/familia/comun/ZeroEstandar.cs:30`) — ESCALA. Referencia: ±300 de NDX ≈ ±1,02 % del precio (≈ ±79 pts de SPX); el paso de strike real de SPX es 5 contra 10/5 de NDX (ver Mediciones).
- `LibroMinuteroNdx.cs:200` — filtro de strikes a ≤ 3 % del futuro (en `comun/LibroMinutoArmado.cs`) — IGUAL (relativo).
- `LibroMinuteroNdx.cs:237, 284, 306` — 300 s, 120 s, 120 s (reintentos y asentado de la cinta) — IGUAL.
- `LibroMinuteroNdx.cs:250` — poda de muestras a 14 días — IGUAL.
- `LibroMinuteroNdx.cs:5` (comentario) — Black-Scholes con r = 3,75 % sin dividendo (`comun/Gamma.cs:15`) — PRODUCTO: misma tasa en USD; el dividendo de SPX (~1,2-1,3 %, SIN VERIFICAR) es mayor que el de NDX y sigue sin entrar.

**(2) Producto/calendario**
- `LibroMinuteroNdx.cs:183, 197` — S = fut − base y zero + base (base aditiva) — IGUAL en ES.
- `LibroMinuteroNdx.cs:269` — descarta fotos de la pausa de CME — IGUAL.

**(3) Rutas/símbolos**
- `LibroMinuteroNdx.cs:37` — `LIBRO = "NDX"` → `"SPX"` — BIEN siempre que BajadorCboe tenga `Nombre = "SPX"` (L141): es la clave de `_cboe.Fotos(LIBRO)` (L255).
- `LibroMinuteroNdx.cs:1-2` — comentario "cadena _NDX … archivo 'NQ' en cboe-local" — cosmético (con \b quedaría "_NDX … archivo 'ES'").
- `LibroMinuteroNdx.cs:168, 171, 176, 188, 319-323` — textos "MNQ", "NDX" de la pestaña — cosmético (se renombran).

---

### `_modulos/familia/qqq/ConversionQqq.cs`
**(1) Constantes**
- `ConversionQqq.cs:100` — `RETRASO_S 900`, `VIVO_S 600`, `VIDA_CBOE_S 1500` — IGUAL; `PISO_REL = 0.0002` (piso del MAD = 0,02 % de la mediana) — IGUAL (relativo).
- `ConversionQqq.cs:101` — `VENTANA = 24`, `MIN_N = 5` — IGUAL.
- `ConversionQqq.cs:122` — tope de 20.000 precios sembrados ("6 días de QQQ son ~7000 fotos") — IGUAL.
- Cotas de razón: NO HAY. MES/SPY ≈ 10,07 (MEDIDO) contra MNQ/QQQ ≈ 41,19.

**(2) Producto/calendario**
- `ConversionQqq.cs:131, 151` — muestra = precio del MNQ en ts − 900 s / spot del ETF, a CUALQUIER hora (incluye after-hours y premarket) — IGUAL en SPY. MEDIDO 23-10: el spot de SPY se mueve después de las 16:00 NY (760,56 a las 20:42 UTC → 761,13 a las 21:22 UTC) y en el premarket (760,75 a las 13:03 UTC, 09:03 NY), con el `ultimo_trade` de las opciones clavado en 16:14:59, igual que QQQ.
- `ConversionQqq.cs:155` — la ventana de 24 se reinicia al cambiar el contrato (roll) — IGUAL.
- `ConversionQqq.cs:163-169, 200-205` — oi_fresco: día [09:30,18:00) / noche [18:00,23:00) / madrugada [23:00,09:30) NY (corregida) o 13:30/22:00/03:00 UTC — IGUAL.
- `ConversionQqq.cs:172` — congelada = t_dato NY ≥ 15:59 — IGUAL.
- `ConversionQqq.cs:181-198` — C9 (1500 s; congelada → 09:30 NY) — IGUAL.
- `ConversionQqq.cs:209-215` — descarta (17:00,18:00) NY / (21:00,22:00) UTC — IGUAL.
- `ConversionQqq.cs:52-58` — tabla de contratos del Python (U6/Z6/H7) — IGUAL (solo paridad).
- Ex-dividendo: la razón no lo contempla (el Python tampoco: `backtest_familia.py:55`). PRODUCTO SIN VERIFICAR: SPY paga trimestral (~1,2 % anual, más del doble que QQQ) y su ex-dividendo cae, creo, el tercer viernes de mar/jun/sep/dic. Ese día la razón saltaría ~0,25 % (~19 pts de MES) hasta que entren muestras nuevas.

**(3) Rutas/archivos**
- `ConversionQqq.cs:221-232` — formato de `muestras-QQQ-<sesión>.jsonl`; la ruta está en `IntegracionFamilia.cs:365` y `:389` (familia: se renombran juntas a `muestras-SPY-` → BIEN).

**Trampas del renombre**
- `ConversionQqq.cs:70` — `"FGHJKMNQUVXZ"` (igual que CalendarioNdx).
- `ConversionQqq.cs:61` — comentario `"MNQZ6" / "NQZ6"` — cosmético.

---

### `_modulos/familia/qqq/MinuteroQqq.cs`
**(1) Constantes**
- `MinuteroQqq.cs:20` — `MULT = 100.0` — PRODUCTO: SPY = 100 (seguro).
- `MinuteroQqq.cs:21` — `FILTRO = 0.03` (strikes a ≤ 3 % del futuro) — IGUAL.
- `MinuteroQqq.cs:22` — `PODA_H = 36` h — IGUAL.
- `MinuteroQqq.cs:23` — `TICKS_M2` = 120 s (vela m2 de la cinta) — IGUAL.
- `MinuteroQqq.cs:43` — `EsperaCintaMaxS = 1200` — IGUAL.
- `MinuteroQqq.cs:107` — 2000 sellos recordados — IGUAL.
- `MinuteroQqq.cs:146` — tramo de 900 s de la caché — IGUAL.
- `MinuteroQqq.cs:147` — `round(S / 1.0)`: centro de la clave de la caché, 1 USD, escrito a mano (no sale de `ZeroEstandarFam.QQQ`) — ESCALA (SPY/QQQ ≈ 1,07 en precio); solo pesa con `ZeroCadaMinuto = false`.
- `MinuteroQqq.cs:149-150` — `ZeroEstandarFam.QQQ` = paso 0,05 / semi ±7,5 / centro 1,0 USD (`comun/ZeroEstandar.cs:31`) — ESCALA. Referencia: ±7,5 USD es ±1,05 % de QQQ (≈ ±309 pts de NQ) y ±0,98 % de SPY (≈ ±75 pts de MES): por precio casi no cambia; por volatilidad SIN VERIFICAR.
- `MinuteroQqq.cs:162` — razón con 4 decimales en la pestaña — IGUAL.

**(2) Producto/calendario**
- `MinuteroQqq.cs:135-137, 155, 160` — S = fut / razón, Fut = K × razón, zero × razón (conversión por razón) — IGUAL en SPY.
- `MinuteroQqq.cs:5` (comentario) — Black-Scholes r 0,0375 sin dividendo — PRODUCTO SIN VERIFICAR: opciones de SPY americanas y con dividendo mayor que QQQ; sigue sin entrar.

**(3) Rutas/símbolos**
- `MinuteroQqq.cs:19` — `LIBRO = "QQQ"` → `"SPY"` — BIEN si BajadorCboe tiene `Nombre = "SPY"` (L142); es la clave de `_cboe.Fotos` (L72).
- `MinuteroQqq.cs:197` — texto "QQQ: sin cadena vigente" — cosmético.

---

### `_modulos/familia/qqq/SeriesQqq.cs`
**(1) Constantes**
- `SeriesQqq.cs:77-87` — `Banda = 20` en las 4 series de muros/majors (0 en ZEST) — "un strike de QQQ son 41 pts de NQ, por eso es una banda de ±20" — ESCALA/PRODUCTO: la banda es medio strike en el eje del futuro; en SPY 1 strike = 1 USD ≈ 10,07 pts de MES (MEDIDO) → ±5 aprox.
- `SeriesQqq.cs:3` (comentario) — radio R = min(2 % fut, 100) (`comun/Seleccion.cs`) — ESCALA: con ES ≈ 7.714 el 2 % es 154, así que corta el 100, que en ES es 1,3 % del precio contra 0,34 % en NQ.
- `SeriesQqq.cs:7` (comentario) — ZEST solo si |z − fut| ≤ 300 (`comun/Seleccion.cs:33`) — ESCALA (300 pts de ES ≈ 3,9 %: en la práctica no filtra nada).
- `SeriesQqq.cs:8, 25` — monto = round(v × MULT/100 / 1e6, 1) → ×1 con MULT 100 — IGUAL (SPY 100).
- `SeriesQqq.cs:77-87` — colores — IGUAL.

**(3) Ids y textos**
- `SeriesQqq.cs:18, 30, 34, 38, 42, 44, 77-87, 95-99` — ids `MUROS_QQQ_vol`, `MUROS_QQQ_oi`, `MAJORS_QQQ_vol`, `MAJORS_QQQ_oi`, `ZEST_QQQ_vol` — ver Trampas.
- `SeriesQqq.cs:79, 81` — criollo "41 pts de NQ" — reescribir con el número de SPY (≈10 pts de MES).
- `SeriesQqq.cs:91-101` — `EstadoSerie`: "rebote 68,2 % contra 63,9 % del azar … Holm 0,84", "68,0 % … Holm 1,0" — son mediciones de NQ/QQQ. En ES serían una afirmación falsa (protocolo del proyecto): reemplazar por "SIN VALIDAR en ES".

**Trampas del renombre**
- Los ids tienen guion bajo: `\bQQQ\b` no entra en `_QQQ_`. Con \b quedan `MUROS_QQQ_vol` mientras `Corto`/`Libro` pasan a "SPY": ids con QQQ y rótulos con SPY. Si se renombran, tienen que cambiar a la vez en todos los que los usan (catálogo, pantalla, propiedades `S_*`, `niv-*.jsonl`), fuera de estos archivos.

---

### (6) Ajustes [Display]
- Ninguno en estos 12 archivos: no referencian ATAS. Los ajustes que los mueven están en `IntegracionFamilia.cs:39` `Cboe41Bajar` (default `true`), `:44` `Cboe41TopeKBps` (`1250`) y `Familia41Corregida` (→ `VentanaUtcPython = !corregida`, `IntegracionFamilia.cs:167`). Ninguno nombra NQ/NDX/QQQ.

### Dependencias de claves con otros módulos (fuera de mi parte, una línea cada una)
- Las capas de la 3.0 piden la cadena con `Archivo` `"QQQ"|"NQ"` (`GammaHoyTresCapas.cs:130,336`), y `IntegracionFamilia.cs:250` traduce `arch == "NQ" ? "NDX" : arch`. Renombrado queda `arch == "ES" ? "SPX" : arch`. Eso anda solo si la 3.0 generalizada pide `"ES"`/`"SPY"` (como cboe-local). Si sigue pidiendo `"NQ"`/`"QQQ"`, `_porNombre` no los encuentra y TRES_SPX/TRES_SPY quedan vacías.
- Hay literales `Fotos("NDX")`/`Fotos("QQQ")` en `ClasicaNdx.cs:454` y `Replica20.cs:446, 495, 547`; con el renombre pasan a "SPX"/"SPY" y encuentran el libro. Los `Fotos("TQQQ")` (`FotosCambios.cs:90`, `CalculoTqqq.cs`) se borran con TQQQ.
- `LibroNqFamilia` está en `AdaptadoresFamilia.cs:1009` (familia) y lo usan `GammaHoyTres.cs` y `Viva3.cs` (núcleo, no se renombra). Un reemplazo de subcadena "Nq"→"Es" en la familia rompe la compilación; con \b no se toca.

---

### Mediciones de paso de strike (MEDIDO 27-10 sobre `ultima-*.json`; cboe-local y PythiaGex4\cboe dan lo mismo)
- **SPX** (`cboe-local\ultima-ES.json`, generado 02:49 UTC, spot 7.676,77): paso **5,0** en ±1 % (29 de 29 diferencias, iguales en los vencimientos 0 a 3) y en 2-5 % (90 de 90). Son 154 strikes únicos en ±5 % y 138-155 filas por vencimiento. Vencimientos diarios lun-vie: 27, 28, 29 y 30-10, y 02-11 a 06-11 y 09-11.
- **SPY** (`ultima-SPY.json`, 00:19 UTC, spot 766,28): paso **1,0** en ±1 % (14 de 14) y en 2-5 %; 77 strikes únicos; mismos vencimientos diarios.
- **NDX** (`ultima-NQ.json`, 03:39 UTC, spot 29.273,4): en ±1 %, 46 diferencias de **10** y 22 de **5** (los de 5 en el 0DTE y el 1DTE); en 2-5 %, de 10, 5, 25 y 20. Son 314 strikes únicos.
- **QQQ** (`ultima-QQQ.json`, 02:49 UTC, spot 714,44): paso **1,0** en ±1 % (13 de 13); 72 strikes únicos.
- En el eje del futuro: 1 strike de SPX son 5 pts de ES (la base es aditiva); de SPY ≈ 10,07 pts de MES; de NDX, 10 (o 5) pts de NQ; de QQQ ≈ 41,2 pts de NQ.
- Otras mediciones, una línea cada una:
  - Contratos por crudo: SPX 28.492 (03-09), NDX 15.196 (31-08), SPY 13.160 (29-08).
  - Peso del gz por bajada: SPX 1,83 MB; NDX 0,87-0,90 MB; QQQ 0,72-0,74 MB; SPY 0,83 MB.
  - Raíces del crudo de SPX: SPX (19 fechas) y SPXW (42). En NDX: NDX (15) y NDXP (36).
  - El tercer viernes junta AM y PM: el 18-09, SPX AM tenía 5.207.673 de OI contra 423.612 de SPXW.
  - SPX de noche no refleja la sesión GTH (dos noches medidas).

### Lo más riesgoso
1. **`BajadorCboe.cs:141` `Simbolo = "_NDX"`.** Con \b no se renombra: la 5.0 bajaría NDX y lo archivaría como SPX en `cadena-ES-*`. Ese archivo lo mezcla con el SPX real de cboe-local. No hay cota de base que lo ataje (`ConversionNdx`). Tiene que quedar `"_SPX"` a mano.
2. **`BajadorCboe.cs:56` y `:141-143`, `CboeArchivo.cs:24`.** Las claves tienen que quedar exactamente `Nombre "SPX"/Archivo "ES"/Simbolo "_SPX"` y `"SPY"/"SPY"/"SPY"`, sin TQQQ. "ES" y "SPY" son los nombres de cboe-local (`archivar_cadena.py:126`). Si la 3.0 sigue pidiendo "NQ"/"QQQ", las capas quedan vacías (`IntegracionFamilia.cs:250`).
3. **Tercer viernes de cada mes (`CboeCrudo.cs:188-217` y `CboeCadena.cs:147,158`).** La raíz se descarta y todo vence 16:00 NY: SPX AM y SPXW PM se suman en la misma fila. MEDIDO: la víspera del 16-10, el "mañana" traía 1,34 M de OI contra 0,33 M reales y con 6,5 h de vida de más. En SPX pesa mucho más que en NDX.
4. **ESCALA.** `ZeroEstandarFam.NDX` (2,5 / ±300 / 50) que usan `LibroMinuteroNdx.cs:191-195`, el PISO de 0,5 de `ConversionNdx.cs:115`, la `Banda = 20` de `SeriesQqq.cs:77-87` (en SPY ≈ ±5) y la clave de caché `S/1.0` de `MinuteroQqq.cs:147`. Los de QQQ→SPY casi no cambian por precio (1,07); los de NDX→SPX van ×0,26 por precio.
5. **Textos de validación de NQ (`SeriesQqq.cs:91-101`, criollo "41 pts de NQ").** En ES serían afirmaciones falsas: reemplazar por "SIN VALIDAR en ES".
6. **`"FGHJKMNQUVXZ"` (`CalendarioNdx.cs:112,146`, `ConversionQqq.cs:70`) e ids con guion bajo (`SeriesQqq.cs`).** Un renombre por subcadena corrompe los meses; uno con \b deja ids `_QQQ_` con rótulos SPY. Decidir la regla antes de correr el script.
7. **Sin verificar:**
   - El ancho de banda: la 4.1 y la 5.0 bajan a la vez, más `cboe_local.py`, y SPX pesa 2,3× NDX.
   - El ex-dividendo trimestral de SPY en la razón.
   - El dividendo de SPX/SPY, que no entra en Black-Scholes.

---

## E. Puntos 1, 3 y 6 por archivo: pantalla

> Las "trampas del renombre" de esta sección se evaluaron contra dos variantes, `\b` y subcadena. Contra el borrador `herramientas/clonar_5_0.py` valen las conclusiones de B.

Alcance: solo lectura del código de `PythiaGex\atas\PythiaGexCuatro` tal como está hoy (27-10). `GammaHoyTres.cs:97` dice `VERSION = "4.1.6g"`, pero ya tiene piezas de la 4.1.7 y la 4.1.8: TOPN, CARGA, rebobinado y punteadas. Los docs (README, CHANGELOG) siguen en la 4.1.5e. Las rutas son relativas a `PythiaGex\atas\PythiaGexCuatro\`, salvo cuando digo otra cosa.

El borrador `PythiaGex\herramientas\clonar_5_0.py` también lo tuve en cuenta: está fechado hoy 00:07 y no lo toqué.
- Sus `REGLAS_FAM` (líneas 51-61) protegen `TQQQ`, `Tqqq` y `tqqq` con los patrones `(?<![Tt])QQQ`, `(?<![Tt])Qqq` y `(?<![TtA-Za-z])qqq`. O sea que **TQQQ sobrevive intacto al paso mecánico y hay que sacarlo a mano después**.
- Además, la regla `MNQ`→`MES` (línea 52) es por subcadena, y eso tiene consecuencias (ver las trampas más abajo).

Mediciones que hice para esta sección (dato viejo, de la rueda del 26-10):
- Fuente: `%APPDATA%\ATAS\PythiaGex\cboe-local\ultima-{ES,SPY,NQ,QQQ}.json`. La cadena está congelada: `ultimo_trade` 16:14:59 NY.
- SPX: spot 7.676,77, base cruda ES−SPX 37,18, strikes de 5 pts en el primer vencimiento a ±2 % (61 de 61 pasos).
- SPY: spot 766,28. Es el after-hours: `cadena_ts` 00:04:56Z. Strikes de 1 USD (30 de 30 pasos).
- NDX: 29.273,40, base cruda 152,74, strikes de 10 pts (82 pasos) y de 5 (42).
- QQQ: 714,44, strikes de 1 USD.
- Razones que salen de ahí: **ES/SPY ≈ 10,07** y **NQ/QQQ ≈ 41,19**. Precio NDX/SPX ≈ 3,81.

### `_modulos/pantalla/ArmadoPantalla.cs`
**(1) Constantes**
- `ArmadoPantalla.cs:84` — `Eje = EjeFamilia4.TQQQ` — default del doble eje — PRODUCTO: TQQQ no existe en ES → `Ninguno` o `SPX` (decisión que se avisa).
- `ArmadoPantalla.cs:86` — `Letra = 9, MargenSup = 56` — letra y franja de arriba en px — IGUAL.
- `ArmadoPantalla.cs:92` — `5/15/30` — ventanas del cambio por volumen en minutos — IGUAL.
- `ArmadoPantalla.cs:231` — `CAMBIO_MIN = 0.05` — un monto (M USD por 1 %) por debajo de esto se escribe "0M" — IGUAL (unidad USD).
- `ArmadoPantalla.cs:235` — `VIEJO_S = 1800` — protocolo de 30 min: la edad va antes del número — IGUAL.
- `ArmadoPantalla.cs:238-239` — `ESTILO_2E = true`, `BARRA_2E = 44` px — IGUAL.
- `ArmadoPantalla.cs:243` — `PUNTEADAS_N = 3` — las 3 CARGA más cercanas al precio llevan raya punteada — IGUAL.
- `ArmadoPantalla.cs:244` — `RECUADRO_Y = 24` px — IGUAL.
- `ArmadoPantalla.cs:247` — `MOTOR_PARADO_S = 150` s — IGUAL.
- `ArmadoPantalla.cs:249` — `TRAMO_TOL = 0.5` pt — tolerancia para decir que dos rayas son "el mismo tramo". Absorbe la deriva de la conversión: 31.041,47 → 31.040,42 en 7 h en NDX (comentario en `:647-648`) — **ESCALA**. En puntos del gráfico, ES se mueve unas 3,8 veces menos que NQ (NDX/SPX ≈ 3,81, medido). El factor lo mide el otro agente.
- `ArmadoPantalla.cs:250-251`, `:254` — `TRAMO_HUECO = 3`, `TRAMO_MIN = 15`, `TRAMO_MIN_M2 = 8` — velas — IGUAL.
- `ArmadoPantalla.cs:255` — `TRAMO_JUNTAR_PTS = 1.5` pt — rayas a menos de esto con el final a ≤ 40 px van en un solo rótulo — **ESCALA**.
- `ArmadoPantalla.cs:256-258`, `:261` — `TRAMO_JUNTAR_PX = 40`, `TRAMO_TOPE = 14`, `TRAMO_PIEZAS = 4`, `TRAMO_CAND = 200` — IGUAL.
- `ArmadoPantalla.cs:262` — `ReVencNq = @"NQ\s*([FGHJKMNQUVXZ])\s*(\d{1,4})"` — saca el vencimiento del código del gráfico ("MNQZ6" → "Z6") — PRODUCTO: la raíz tiene que pasar a "ES" (el borrador la cambia). **La clase de meses tiene que quedar `FGHJKMNQUVXZ`: ver trampas.**
- `ArmadoPantalla.cs:263` — `ReVencSolo = @"^([FGHJKMNQUVXZ])(\d{1,4})$"` — código del motor ("Z6") — IGUAL (misma trampa).
- `ArmadoPantalla.cs:288` — `P()`: `"#,##0"` o `"#,##0.00"` es-AR — formato de precio ("31.073,31") — IGUAL (ES "7.713,95").
- `ArmadoPantalla.cs:291` — `StrikeTqqq` (`"0.#"`/`"0.00"`) — BORRAR (TQQQ).
- `ArmadoPantalla.cs:294` — `Strike()` `"#,##0.##"` — IGUAL (SPX "7.675", SPY "766").
- `ArmadoPantalla.cs:355-360` — cortes de `MontoAbs` (`9.95`, `999.5`, `9.995`, `99.95`) — formato M/B — IGUAL.
- `ArmadoPantalla.cs:423`, `:428`, `:430`, `:431` — `"0.0000"` (razón) y `"0.00"` (base) — formato de la conversión — IGUAL (ES/SPY ≈ 10,07 con 4 decimales anda).
- `ArmadoPantalla.cs:745` — letra entre 6 y 14 — IGUAL.
- `ArmadoPantalla.cs:750` — `instr.IndexOf("NQ", OrdinalIgnoreCase) < 0` → cartel "no dibujo nada" — PRODUCTO: tiene que ser "ES". El borrador lo deja `"ES"`, que acepta MESZ6 y ESZ6 y rechaza MNQZ6.
  - Ojo, sin distinguir mayúsculas: "ES" aparece en otras palabras; riesgo bajo.
- `ArmadoPantalla.cs:791` — `0.9` — ancho de la rayita en fracción de vela — IGUAL.
- `ArmadoPantalla.cs:836` — `F.TqS >= 20 && F.TqS <= 1000` — cota de la s de TQQQ — BORRAR.
- **`ArmadoPantalla.cs:841` — `fQqq.ConvValor > 30 && fQqq.ConvValor < 60`** — cota de la razón NQ/QQQ (medida hoy 41,19) para dibujar el eje QQQ — **PRODUCTO/ESCALA**.
  - Con ES/SPY ≈ 10,07 (medido, dato del 26-10) **la cota deja el eje SPY sin dibujar nunca** ("sin conversión vigente").
  - Hay que cambiarla, por ejemplo a `(8, 12)`. El margen está SIN VERIFICAR.
- `ArmadoPantalla.cs:846` — `Math.Abs(fNdx.ConvValor) < 2000` — cota de la base NQ−NDX — ESCALA, pero no rompe: ES−SPX ≈ 37,18 (medido) pasa igual.
- `ArmadoPantalla.cs:848` — `fmtEje = "#,##0"; fuerte = 100` — en el eje NDX las marcas cada 100 puntos del índice van fuertes (≈ 0,34 % de NDX) — ESCALA (cosmético). En SPX lo equivalente es ~25-26 (100 × 7.677/29.273); SPX tiene strikes de 5 (medido). Sugerido 25, SIN VERIFICAR.
- `ArmadoPantalla.cs:834`, `:865` — `fuerte = 1` en el eje QQQ (dólares enteros = strikes de QQQ) — IGUAL para SPY, que tiene strikes de 1 USD (medido: 30 de 30 pasos).
- `ArmadoPantalla.cs:856-857` — pasos candidatos `0.01 … 250` y `≥ 34` px entre marcas — IGUAL (automático).
- `ArmadoPantalla.cs:861` — tope de 300 marcas — IGUAL.
- `ArmadoPantalla.cs:957` — `<= 1.0` pt — niveles a menos de 1 pt van en una sola etiqueta (grupo) — **ESCALA**.
- `ArmadoPantalla.cs:989`, `:1299`, `:1495` — `P(Math.Round(...))` — con banda (QQQ ±20) el precio se redondea al punto — IGUAL. La banda viene del catálogo.
- `ArmadoPantalla.cs:1261` — `e > 120` s — cinta vieja en naranja — IGUAL.
- `ArmadoPantalla.cs:1349`, `:1353` — `20` letras mínimo y `40` px — IGUAL.
- `ArmadoPantalla.cs:1305` — texto `"centroide ±12"` — ESCALA: es el número de `DominantesClasica.CENTROIDE` (ver punto 7). Si la constante se reescala para ES, este texto la tiene que seguir.

**(3) Rutas, textos y símbolos.** Para cada uno digo qué hace el renombre planeado (el del borrador, `clonar_5_0.py`).
- `ArmadoPantalla.cs:751` — "PythiaGex 4.0: los niveles estan en precio de NQ/MNQ; este grafico es ..." — queda bien: "PythiaGex 5.0 ... ES/MES" (regla `"PythiaGex 4.0` y luego `MNQ`/`NQ`).
- `ArmadoPantalla.cs:753` y `:1210` — "libro NQ por Rithmic, NDX/QQQ/TQQQ bajados de CBOE" — queda "libro ES ..., SPX/SPY/TQQQ ...": **mal (sobra TQQQ)**. Editar a mano.
- `ArmadoPantalla.cs:757` — cartel de otro vencimiento ("los niveles son de Z6 y este gráfico es ...") — queda bien.
- `ArmadoPantalla.cs:783`, `:1180-1181` — "PythiaGex 4.0" del cartel y de la pestaña — queda bien (`"PythiaGex 4.0` → `"PythiaGex 5.0`).
- `ArmadoPantalla.cs:420-435` — fuentes `"2.0 QQQ"`, `"2.0 NDX"`, `"QQQ"`, `"NDX"`, `"Clasica NDX"` y series `R20_QQQ_vol`, `R20_NDX_vol`, `DOMS_*`, `R10_NDX_*` — quedan bien, siempre que `Replica20`, `ClasicaNdx`, `CatalogoFamilia.MetaExtraDe` (`fam/CatalogoFamilia.cs:48`) y `SalidaFamilia` se renombren con las mismas reglas. Todos están en `_modulos`.
- `ArmadoPantalla.cs:462-474` — busca `" = MNQ "`, "MNQ del ultimo trade / QQQ spot" y "CRUDA: MNQ de ahora ..." en el texto que arma `Replica20` — queda bien solo si se renombran a la vez el productor (`replica20/Replica20.cs`) y el consumidor; si uno queda sin renombrar, el origen sale siempre "origen sin dato" en naranja.
- **`ArmadoPantalla.cs:487-488` — `AVISO_CLASICA` = "Clasica NDX: copia a la clasica ... (09-10 16:48-17:11 ART: ~13 pts)"** — queda "Clasica SPX ... ~13 pts": **mal**. Es una medición de NDX que en ES no existe. Reescribir o sacar.
- `ArmadoPantalla.cs:490-492` — regex `"= de la rueda ..."`, `"4\.1 sincronizada ..."`, `"rueda de hoy ... muestras"` — leen el texto de `ClasicaNdx.Texto` — IGUAL. Ojo: si el clon cambia "4.1 sincronizada" por "5.0" en `ClasicaNdx`, hay que cambiarlo acá también (el borrador no lo toca).
- `ArmadoPantalla.cs:839`, `:844`, `:849` — títulos del eje: "eje TQQQ x3: NQ = ...", "eje QQQ: NQ = QQQ x ...", "eje NDX: NQ = NDX + ..." — `:839` se borra y los otros dos quedan bien ("eje SPY: ES = SPY x ...").
- `ArmadoPantalla.cs:1226-1230` — `case "NQ": case "NDX": case "QQQ":` y `fu.Libro == "NQ" ? " Rithmic" : " CBOE"` — quedan bien.
- `ArmadoPantalla.cs:1239`, `:1245`, `:1249`, `:1316`, `:1325`, `:1329` — `"2.0 QQQ"`, `"2.0 NDX"`, `"Clasica NDX"` — quedan bien.
  - `:1245`: "replica a la 2.0 en un grafico de 1 min" — según el punto 7, en ES la 2.0 no dibuja SPY: el texto describe algo sin original.
- `ArmadoPantalla.cs:1261` "cinta MNQ", `:1291` y `:1342` "— precio MNQ ... —" — quedan bien (MES).
- `ArmadoPantalla.cs:1304-1305` — `R10_NDX_zero`/`R10_NDX_dom` y "(en el indice NDX ...)", "(strike NDX ..., centroide ±12)" — quedan bien, salvo el ±12 (ver (1)).

**Trampas del renombre**
- **`ArmadoPantalla.cs:262-263`, la cadena de meses `FGHJKMNQUVXZ`.**
  - Contiene la subcadena `MNQ`. La regla del borrador `re.compile(r"MNQ")` → `"MES"` (`herramientas/clonar_5_0.py:52`) la convierte en `FGHJKMESUVXZ`: pierde N (julio) y Q (agosto) y acepta E y S como meses.
  - La regla `(?<![A-Z])NQ` no la toca, porque la N va detrás de una M.
  - Para ES sigue andando por casualidad (H, M, U y Z quedan), pero es un regex roto. **Hay que protegerla** antes del renombre (y los otros lugares que lista el punto 8: `AdaptadoresFamilia.cs:104`, `CalendarioNdx.cs:112,146`, `ConversionQqq.cs:70`, `Replica20.cs:291,293`).
  - Resultado esperado en la 5.0: `@"ES\s*([FGHJKMNQUVXZ])\s*(\d{1,4})"`.
- **Los nombres del enum son nombres de libro.** `aj.Eje.ToString() == fu.Libro` (`:1228`) y `"eje " + aj.Eje` (`:851`, `:878`) suponen que el enum se llama igual que los libros.
  - El borrador renombra las dos cosas (todo está en `_modulos`), así que queda `SPX`/`SPY` en los dos lados.
  - Si alguien protege el enum o lo deja sin renombrar, el eje no encuentra su fuente y el detalle no lista "SPX CBOE"/"SPY CBOE".
- El `ColTqqq` que sobrevive (protegido) es el color de los ejes: no borrarlo al sacar TQQQ.

### `_modulos/pantalla/SeriesPantalla.cs`
**(1) Constantes**
- `SeriesPantalla.cs:102`, `:108`, `:109`, `:113` — `Banda = 20` en `MAJORS_QQQ_oi`, `MUROS_QQQ_vol`, `MUROS_QQQ_oi` y `MAJORS_QQQ_vol` (catálogo de respaldo) — medio strike de QQQ en puntos de NQ (1 USD × 41,19 ≈ 41 → ±20) — **ESCALA/PRODUCTO**.
  - Para SPY: strikes de 1 USD (medido) × 10,07 ≈ 10 pts de ES → **±5**, si se mantiene el criterio "medio strike" (SIN VERIFICAR que ese sea el criterio buscado).
  - **Tiene que ser el mismo valor que en `CatalogoFamilia`**, que es donde manda; el respaldo solo se usa si el motor no publicó su catálogo, y la prueba los compara campo por campo.
- `SeriesPantalla.cs:125-127`, `:133-135` — `Banda = 4.0` de las T_* (`T_DOMS_raz` 0) — BORRAR.
- `SeriesPantalla.cs:36-81` — campos `Orden` y `Grupo` de las casillas — el producto no los usa (solo `Id` y `Default`) — IGUAL.
- `SeriesPantalla.cs:101-141` — colores `#rrggbb` — IGUAL.

**(3) Textos y símbolos**
- `SeriesPantalla.cs:38-80` — los ids (`"MUROS_NQ_vol"`, ...) y los nombres ("NQ muros vol", "3.0 NDX", "2.0 QQQ (dominantes y conversion de la 2.0)", ...) — quedan bien (`(?<![A-Z])NQ` toma el NQ que va detrás de `_`). `Casilla.Propiedad => "S_" + Id` (`:33`) queda coherente con las propiedades renombradas.
  - Los grupos "1. NQ", "2. NDX", "3. QQQ" (`:65-69`) pasan a "1. ES" etc.: solo texto.
- `SeriesPantalla.cs:84`, `:86` — `GRUPO_20 = "4b. Dominantes como la 2.0"` y `GRUPO_CLASICA = "4c. Como la clasica"` — IGUAL.
- `SeriesPantalla.cs:92-93` — `PrendidasPorDefecto` = 8 ids (con `T_MUROS_oi`) — quedan 7 sin TQQQ.
  - **Inconsistencia ya existente:** los `Default = true` de `Casillas` son 11, porque además están `CARGA_NQ`, `TOPN_NQ_vol` y `T_ZERO_vol_pm`. El producto usa `Default`; el arreglo solo lo miran las pruebas.

### `_modulos/pantalla/FamiliaCuatroPantalla.cs` (pasa a `GammaCincoPantalla.cs` en el borrador)
**(1) Constantes**
- `FamiliaCuatroPantalla.cs:101` — `Eje4Libro = EjeFamilia4.TQQQ` — PRODUCTO → `Ninguno`/`SPX` (decisión que se avisa).
- `FamiliaCuatroPantalla.cs:113` — `Cambio41Ventana = VentanaCambio41.M15` — IGUAL.
- `FamiliaCuatroPantalla.cs:123-124` — `MargenSup4 = 56`, `[Range(0, 400)]` — IGUAL.
- `FamiliaCuatroPantalla.cs:126-127` — `Letra4 = 9`, `[Range(6, 14)]` — IGUAL.
- `FamiliaCuatroPantalla.cs:137`, `:264` — época 1970 y `|p| > 1e12` — IGUAL.

**(3) Textos y símbolos**
- `FamiliaCuatroPantalla.cs:119` — Description de `Cabecera4`: "La pestaña 'PythiaGex 4.0 ▸' ..." — **el borrador NO lo cambia**: la regla es `"PythiaGex 4.0` con comilla doble y acá va comilla simple. Editar a mano.
- `FamiliaCuatroPantalla.cs:120` — Description de `Recuadro4Abierto`: "... la pestaña PythiaGex 4.0." — **el borrador NO lo cambia**. Editar a mano.
- `FamiliaCuatroPantalla.cs:241` — comentario que cita `PythiaGexDos/GammaHoy.cs:1469` — solo texto.
- Las descripciones con números de la escala de NQ: el renombre cambia los nombres de libro pero no los números, y **quedan frases falsas** ("un strike de SPY son 41 pts de ES"; "ES x20"). La lista está en (6).

**(6) Ajustes [Display]**

Todos están en el grupo "0. PRENDER / APAGAR (todo lo que se dibuja)" salvo que diga otra cosa. Los nombres se renombran con la familia (S_MUROS_NQ_vol → S_MUROS_ES_vol, etc.).

| Línea | Propiedad | Name | Order | Default | Lo que menciona la Description |
|---|---|---|---|---|---|
| 40 | `S_MUROS_NQ_vol` | "NQ muros vol" | 0 | false | "0DTE de NQ", "a +-100 pts" y estadísticas de NQ (60,6 % contra 50,9 %, 20 sesiones) |
| 41 | `S_MAJORS_QQQ_oi` | "QQQ majors OI" | 16 | false | "banda +-20", "un strike de QQQ son 41 pts de NQ" y estadísticas |
| 42 | `S_CONF_vol` | "CONF vol" | 25 | false | "a <= 3 pts", "(NQ, NDX, QQQ)" |
| 43 | `S_MAJORS_NQ_vol` | "NQ majors vol" | 2 | false | estadísticas de NQ |
| 45 | `S_MUROS_NQ_oi` | "NQ muros OI" | 1 | **true** | "0DTE de NQ" |
| 46 | `S_MUROS_NDX_vol` | "NDX muros vol" | 7 | **true** | "a precio de NQ por la base sincronizada", "indice NDX de CBOE" |
| 47 | `S_MUROS_NDX_oi` | "NDX muros OI" | 8 | false | NDX |
| 48 | `S_MUROS_QQQ_vol` | "QQQ muros vol" | 13 | false | "banda +-20", "strikes cada 41 pts de NQ" |
| 49 | `S_MUROS_QQQ_oi` | "QQQ muros OI" | 14 | false | "banda +-20" |
| 50 | `S_MAJORS_NQ_oi` | "NQ majors OI" | 3 | false | NQ |
| 51 | `S_MAJORS_NDX_vol` | "NDX majors vol" | 9 | false | NDX |
| 52 | `S_MAJORS_NDX_oi` | "NDX majors OI" | 10 | false | NDX |
| 53 | `S_MAJORS_QQQ_vol` | "QQQ majors vol" | 15 | false | "banda +-20" |
| 54 | `S_FAM_MUROS_vol` | "FAM muros vol" | 23 | false | **"(NQ x20, NDX y QQQ x100), grilla de 5 pts"**: en ES el multiplicador es 50 |
| 55 | `S_FAM_MUROS_oi` | "FAM muros OI" | 24 | false | "los tres libros" |
| 57 | `S_ZEST_NQ_vol` | "NQ 0Γ est vol" | 4 | false | NQ |
| 58 | `S_ZEST_NQ_oi` | "NQ 0Γ est OI" | 5 | false | "53 toques en 20 sesiones" |
| 59 | `S_ZEST_NDX_vol` | "NDX 0Γ est vol" | 11 | false | NDX |
| 60 | `S_ZEST_QQQ_vol` | "QQQ 0Γ est vol" | 17 | false | QQQ |
| 61 | `S_ZTP_NQ_vol` | "NQ cruces vol" | 6 | false | "islas de 2-4 contratos", "41,3 % contra 43,6 %" |
| 62 | `S_ZTP_NDX_vol` | "NDX cruces vol" | 12 | false | NDX |
| 64 | `S_TRES_NQ` | "3.0 NQ rayas" | 32 | false | "(estela-NQ)" |
| 65 | `S_TRES_NDX` | "3.0 NDX rayas" | 33 | **true** | "spot al sello - 900 s, base por forwards" |
| 66 | `S_TRES_QQQ` | "3.0 QQQ rayas" | 34 | false | "spot al sello - 900 s" |
| 68 | `S_TOPN_NQ_vol` | "NQ top-N vol" | **7** | **true** | "radio (min(2 %, 100 pts))", "medido el 12-10 ... +-100 pts ... 24 strikes ... (30800, 30860, ...)" |
| 69 | `S_TOPN_NQ_oi` | "NQ top-N OI" | **8** | false | NQ |
| 70 | `S_CARGA_NQ` | "NQ carga (strikes abiertos)" | **9** | **true** | "el libro NQ suscribe solo los 2 vencimientos mas cercanos (CadenaApi.Vencimientos = 2) y CBOE recorta a 14 dias en vivo y 8 desde el archivo, con strikes a +-5 % del spot" |
| 71 | `S_CARGA_NDX` | "NDX carga (strikes abiertos)" | **10** | false | el mismo texto |
| 72 | `S_CARGA_QQQ` | "QQQ carga (strikes abiertos)" | **11** | false | el mismo texto |
| 73-78 | `S_T_*` | (ver punto 4) | | | BORRAR |
| 81-82 | `S_R20_QQQ_vol` | "2.0 QQQ D1-D2" | 28 | **true** | "a menos de 2 % (tope 100 pts)", "MNQ de la vela de 1 min del ultimo trade / spot de QQQ", "MNQ de las 16:14 NY con el QQQ del after-hours", "(de noche ~3 pts)" |
| 83-84 | `S_R20_NDX_vol` | "2.0 NDX D1-D2" | 29 | **true** | "base cruda de forwards de la cadena (si pasa la cota del carry)" |
| 85-86 | `S_DOMS_QQQ_vol` | "QQQ dom sel2.0" | 30 | false | "Medida en 18 noches" (NQ) |
| 87-88 | `S_DOMS_NDX_vol` | "NDX dom sel2.0" | 31 | false | "Sin medir" |
| 92-93 | `S_R10_NDX_zero` | "Clas NDX 0Γ" | 27 | **true** | "grilla de 61 precios en +-3 %", "13-15 pts (09-10)", "de 16:48 a 17:11 ART quedo ~13 pts corrida" |
| 96-97 | `S_R10_NDX_dom` | "Clas NDX D1-D3" | 26 | **true** | "radio min(2 %, 100 pts)", "empate 20 %", "centroide de +-12 pts", "~13 pts" |
| 100-101 | `Eje4Libro` (enum) | "Doble eje izq." | 35 | **`EjeFamilia4.TQQQ`** | "NDX = NQ − base; QQQ = NQ / razon; TQQQ = (NQ − c)/s" |
| 103 | `Estela4` | "Estela▸" | 36 | true | — |
| 104 | `Rotulos4` | "Etiquetas▸" | 38 | true | — |
| 107-108 | `Monto41Rotulos` | "↳ montos" | 39 | true | "USD por 1 %": IGUAL |
| 109-110 | `Cambio41Rotulos` | "↳ cambios ▲▼" | 40 | true | — |
| 111-113 | `Cambio41Ventana` (grupo "6. Pantalla") | "Ventana del cambio por volumen" | 1026 | `M15` | — |
| 116-117 | `Tramos41Rotulos` | "↳ rot. tramos" | 37 | false | ejemplos "NDX muro V 31.040,74" y "NDX muro V/OI · 2.0 NDX 31.041" (precios de NQ) |
| 119 | `Cabecera4` | "Pestaña de detalle" | 41 | true | "'PythiaGex 4.0 ▸'" |
| 120-121 | `Recuadro4Abierto` (grupo "6. Pantalla") | "Recuadro abierto" | 1035 | false | "pestaña PythiaGex 4.0" |
| 122-124 | `MargenSup4` (grupo "6. Pantalla") | "Separacion del borde de arriba (px)" | 1036 | 56 | "(la 3.0 usa 56)": IGUAL |
| 125-127 | `Letra4` (grupo "6. Pantalla") | "Tamano de letra" | 1040 | 9 | — |
| 131-132 | `Carpeta4` (`[Browsable(false)]`) | — | — | `""` | carpeta del visor 4.0.6, sin efecto |

- `Order` repetidos que ya existen hoy (no son de NQ, pero importan para renumerar): 7 (`S_MUROS_NDX_vol` y `S_TOPN_NQ_vol`), 8, 9, 10, 11 (MUROS/MAJORS/ZEST NDX contra TOPN/CARGA) y 23 (`S_FAM_MUROS_vol` y `S_T_ZERO_vol_pm`). Las TOPN/CARGA y `S_T_ZERO_vol_pm` no están en la `LISTA` de `grupo_arriba_415e.py`: se agregaron después sin pasar por el script.
- `Carpeta4` (`:131-132`) es solo para que un `.ws` del visor 4.0.6 tenga dónde caer. En un tipo nuevo no hace falta. Si se borra, también `AjustesPantalla4Texto` (`:207`).

**Trampas del renombre**
- **Los nombres de propiedad persistidos.** La 5.0 es un tipo nuevo, así que ningún `.ws` guarda `S_MUROS_ES_vol` y los defaults del código son los que mandan en el primer gráfico.
  - Hoy esos defaults son los 11 `true` del operador en NQ, entre ellos `S_TRES_NDX`→`S_TRES_SPX`, `S_R10_*`, `S_R20_*`, `S_TOPN_*`/`S_CARGA_*` de ES, y el eje.
  - Hay que **decidirlos antes de instalar y avisarle** (regla del operador del 17-09).
- Los nombres "Raya3Nq…"/"Tres41…" del grupo de arriba son del núcleo (`GammaHoyTresRayas.cs` etc.), no de esta clase. El borrador no los renombra (sus nombres visibles siguen "↳ NQ D1"): es parte del punto 5/6 del inventario principal.

### `_modulos/pantalla/PantallaFamilia.cs`
**(1) Constantes**
- `PantallaFamilia.cs:21` — `M2 = 120_000` ms — IGUAL.
- `PantallaFamilia.cs:22` — `TOPE_VELAS = 20000` — IGUAL.
- `PantallaFamilia.cs:84` — ancho de vela por defecto 6 px — IGUAL.
- `PantallaFamilia.cs:112` — `> 5` cuadros sin mouse — IGUAL.
- `PantallaFamilia.cs:181`, `:188` — fuente "Consolas" y tope de 512 plumas — IGUAL.

**(3) Rutas, textos y símbolos**
- `PantallaFamilia.cs:225` — `%APPDATA%\ATAS\pythiagex4-pantalla.log` — queda bien (`pythiagex4` → `pythiagex5`).
- `PantallaFamilia.cs:20` — `VERSION = "pantalla 4.1.5d (09-10-2026, ... la clasica NDX 0Γ y D1-D3)"` — el borrador solo cambia `private const string VERSION`, y esta es `public`: queda "4.1.5d ... SPX". Va solo al log; editar a mano para que no confunda.
- `PantallaFamilia.cs:138-143` — log del primer render con instrumento y sesión — IGUAL.

### `_modulos/pantalla/ContratosPantalla.cs`
- Nada de producto. `:14`: comentario "29 series".

### `_modulos/pantalla/prueba/` (arnés B5: qué NQ/TQQQ tiene)
- `prueba/datos/niv-2026-10-09-MNQZ6-hasta-0544Z.jsonl` (738 KB) — la historia real de NQ del 09-10. Lo lee la prueba T12 (`Program.cs:1687-1692`). Para ES hace falta un `niv-*-MESZ6` de la 5.0.
- `prueba/Program.cs` y `prueba/Dobles.cs` — todo en escala NQ:
  - precios 30.9xx-31.5xx, strikes QQQ 750/760,2, base 243,06, razón 41,4447;
  - TQQQ en C6, C10, C16, C19, C20 (`124.21*K + 21012.06`), C23, C28, C44, C54b y C68-C69, y en los dobles `Dobles.cs:21,125,143,150,161,170-171`;
  - `"MNQZ6"` en todos lados.
- Pruebas que se **invierten** en ES:
  - **C1** (`Program.cs:566-568`): `Venc("MESZ6") == ""`. En la 5.0 tiene que dar "Z6".
  - **C36** (`Program.cs:644-645`): un gráfico MESZ6 muestra el cartel "precio de NQ/MNQ". En la 5.0 el que tiene que mostrar el cartel es el de MNQZ6.
- Pruebas que ya quedaron viejas con el código de hoy:
  - **A8** (`:300`): 35 casillas; hoy hay 41.
  - **A10** (`:328`): `PrendidasPorDefecto` igual a los defaults, y `Eje == TQQQ`.
  - **A21a/A22b** (`:174`, `:365`): 101 casillas en la `LISTA`.
- `prueba/PruebaPantalla.csproj:19-29` compila `Contratos.cs`, los 5 de pantalla y `CatalogoFamilia.cs`: hereda todo el renombre.

### `PythiaGex\herramientas\grupo_arriba_415e.py` (la fuente de verdad del grupo de arriba)
- `:38` — `P4 = ...\atas\PythiaGexCuatro`, con la ruta fija. `:39` — `CATALOGO` de la 4.1.
  - Corrido así no toca la 5.0.
  - Si se copia a la 5.0 sin cambiar la `LISTA`, vuelve a escribir Name/Order con los nombres de la 4.1.
- La `LISTA` tiene **101 entradas, y 56 llevan NQ/NDX/QQQ/TQQQ en la propiedad o en el nombre visible**:
  - índices 0-17: `S_*_NQ_*`, `S_*_NDX_*`, `S_*_QQQ_*` ("NQ muros vol" ... "QQQ 0Γ est vol");
  - **18-22: `S_T_*` (TQQQ)**;
  - 26-34: `S_R10_NDX_*`, `S_R20_*`, `S_DOMS_*`, `S_TRES_*`;
  - 42-79: los del núcleo, `Tres41Dominantes` "3.0 NQ dom▸", `Raya3NqD1` "↳ NQ D1" ... `Raya3QqqZeroEnfasis` "↳ QQQ 0Γ enf.".
- Lo que el renombre tiene que acompañar en un `grupo_arriba_5_0.py`:
  - las propiedades de la familia (`S_MUROS_NQ_vol` → `S_MUROS_ES_vol`, ...) y sus nombres visibles;
  - sacar los 5 de TQQQ;
  - agregar los que faltan (TOPN, CARGA);
  - renumerar los Order 0..N-1;
  - **y NO renombrar los del núcleo si el núcleo no se renombra**. Si no, `--verificar` y las pruebas W6-W8/A21-A23 fallan o el script escribe propiedades que no existen.
- Con el código de hoy, `--verificar` probablemente ya da "DISTINTO": TOPN, CARGA y `S_T_ZERO_vol_pm` están fuera de la `LISTA`, con Order repetidos. SIN VERIFICAR: no lo corrí.

---

## F. Punto 4: TQQQ (qué borrar o desenganchar para que compile sin él)

### Qué es TQQQ en la 4.1
- El descargador de CBOE baja TQQQ como tercer ticker.
- `CalculoTqqq` arma por vela m2 la conversión x3 `NQ = s·K + c` y 6 series: T_DOMS_vol, T_MUROS_vol, T_MUROS_oi, T_ZERO_oi, T_ZERO_vol_pm y T_DOMS_raz.
- El doble eje TQQQ es el **default** del eje izquierdo.
- Hay un ancla de noche opcional.
- Los cambios por nivel de TQQQ viven en el módulo posiciones.
- Tiene archivos y log propios.
- Nada de TQQQ se guarda en `niv-*.jsonl`: la historia de TQQQ se rehace de las fotos de CBOE.

### Dependencias cruzadas (lo que preguntaste)
- **Ningún código que no sea de TQQQ usa clases de `_modulos/tqqq`.** Afuera de la carpeta solo aparecen dos usos, y los dos están dentro de ramas que existen solo para TQQQ y se van con él:
  - `TqqqSesion`, en `_modulos/familia/posiciones/FotosCambios.cs:89`, adentro de `AhoraTqqq`.
  - `TqqqPerfil`, en `_modulos/familia/posiciones/CambiosFamilia.cs:71` y `:284`, en las ramas `EsTqqq`.
  - No hay que mudar nada antes de borrar la carpeta. `TqqqHora`, `TqqqNum` y `TqqqGamma` no los usa nadie de afuera: los feriados de la familia viven en `TiempoFam`.
- **`ICinta.PrecioSoloTick` NO es solo de TQQQ, aunque su comentario diga "Solo tick (TQQQ)"** (`_contratos/Contratos.cs:69-70`). Lo usan `_modulos/familia/ndx/LibroMinuteroNdx.cs:306`, `_modulos/familia/replica20/Replica20.cs:414,425,512,640` y `_modulos/familia/clasica/ClasicaNdx.cs:406,717`. Lo implementa `AdaptadoresFamilia.cs:410`. **QUEDA.**
- `FotoCadena.CierreAnterior` (`_contratos/Contratos.cs:40`) también QUEDA:
  - lo llena `_modulos/cboe/CboeCadena.cs:106`;
  - lo copian `_modulos/cboe/BajadorCboe.cs:608`, `ndx/LibroMinuteroNdx.cs:297`, `qqq/MinuteroQqq.cs:101`, `posiciones/ClavesCambios.cs:109` y `posiciones/FotosCambios.cs:30`;
  - el único que lo lee para calcular es TQQQ, pero sacarlo obliga a tocar 6 lugares sin ganar nada.
- **`cierres.json` y `BajadorCboe.Cierre()` tienen un solo consumidor**: la lambda `OpcionesTqqq.Cierres` de `IntegracionFamilia.cs:187-195`. Sin TQQQ quedan como código muerto que igual escribe `cboe\cierres.json` para SPX y SPY. No rompe nada. Se puede dejar (opcional borrarlo, ver `_modulos/cboe` abajo).

### `_modulos/tqqq/` — BORRAR la carpeta entera (6 archivos, 1.547 líneas)
- `_modulos/tqqq/CalculoTqqq.cs:1-790` — tiene `CierresTqqqDia`, `OpcionesTqqq`, `TqqqDetalle`, `TqqqMuestra` y `CalculoTqqq` (implementa `ICalculoTqqq` e `IVersionSTqqq`).
  - Rutas: `Carpeta = ...\PythiaGex4\familia` (:44) y `RutaLog = ...\pythiagex4-tqqq.log` (:45).
  - Constantes TQQQ: `RETRASO_S 900`, `VIDA_S 1500`, `VIVO_S 600`, `VIVO_FOTOS 12`, `VENTANA 24`, `MIN_N 5`, `EDAD_TICK_S 120` (:93), `BANDA 4/8/5`, `PISO 1` (:95).
  - Pide `cboe.Fotos("TQQQ")` y `cboe.Fotos("NDX")` (:279, :335, :356, :363, :642, :680, :773).
  - BORRAR.
- `_modulos/tqqq/TqqqArchivo.cs:1-136` — `TqqqLog` (tope 10 MB) y `TqqqArchivo`, que escriben `tqqq-muestras-<sesion>-<contrato>.csv` y `tqqq-cierres.json` en `familia\`. BORRAR.
- `_modulos/tqqq/TqqqNoche.cs:1-146` — `TqqqAnclaDia` y `TqqqAnclas`: las anclas B/C de noche, las últimas 40 en `tqqq-cierres.json`. BORRAR.
- `_modulos/tqqq/TqqqPerfil.cs:1-181` — el perfil del vencimiento más cercano: `RADIO 0.05`, `RADIO_ZERO 0.06`, y `TqqqNivelK`/`TqqqNivelesFoto`. BORRAR, junto con sus dos usos de posiciones (abajo).
- `_modulos/tqqq/TqqqSesion.cs:1-99` — la sesión de TQQQ en paridad y corregida (rueda 09:33-cierre NY). BORRAR, junto con su uso en `FotosCambios.cs:89`.
- `_modulos/tqqq/TqqqUtil.cs:1-195` — `TqqqNum`, `TqqqGamma` (r 0,0375) y `TqqqHora` (feriados y medio día NYSE). Nadie de afuera los usa. BORRAR.

### `PythiaGexCuatro.csproj`
- `PythiaGexCuatro.csproj:104-109` — los 6 `<Compile Include="_modulos\tqqq\...">`. BORRAR. El borrador copia la lista de `Compile` del csproj de la 4.1, así que hay que sacarlos del `PythiaGexCinco.csproj` que genera.

### `_contratos/Contratos.cs` (ojo: se compila en la DLL, en la prueba de la pantalla y en varios arneses)
- `:214-223` — `TqqqVela`, con `S`, `C`, `Series`, `Strikes`, `DatoUtc` y `Congelada`. BORRAR, después de sacar sus usos en fam, posiciones, Integracion y los arneses.
- `:225-229` — interfaz `ICalculoTqqq.Vela(aperturaM2Ms, cinta, cboe)`. BORRAR.
- `:231-237` — interfaz `IVersionSTqqq.VersionS`. BORRAR.
- `:243` — `IMotorFamilia.Configurar(..., ICalculoTqqq tqqq, string contrato)`. DESENGANCHAR: sacar el parámetro de la interfaz, de `MotorFamilia.Configurar` (`fam/MotorFamilia.cs:179`), de la llamada en `IntegracionFamilia.cs:218` y de los arneses que lo llaman.
  - Variante mínima que también compila: dejar el tipo y pasar `null`. El motor ya tolera `_tqqq == null` (`MotorFamilia.cs:371`). Pero entonces `TqqqVela` e `ICalculoTqqq` quedan vivos en Contratos.
- `:176` — `FotoFamilia.TqS`, `TqC` y `TqDatoUtc` (el eje TQQQ x3). BORRAR, junto con su escritura en `fam/MotorFamilia.cs:589` y su lectura en `pantalla/ArmadoPantalla.cs:836,838,1268`.
- `:69` (comentario "Solo tick (TQQQ)") — corregir el texto. El método queda (ver dependencias).
- `:2`, `:16`, `:30` (`"TQQQ"` como libro), `:40`, `:53`, `:162` y `:207` ("29 series ... + TQQQ") — son comentarios. Corregir el texto.

### `IntegracionFamilia.cs`
- `:37` — `[Display(Name = "Bajar las cadenas de CBOE (NDX, QQQ, TQQQ)")]` de `Cboe41Bajar` (default `true`, Order 1020). Después del renombre queda "(SPX, SPY, TQQQ)". Editar a mano para que diga "(SPX, SPY)".
- `:46-48` — la propiedad `Tqqq41AnclaNoche` ("TQQQ de noche (anclaje al cierre, SIN VALIDAR)", grupo "7. Datos", Order 1040, default `false`). BORRAR.
- `:70` — `new HostFamilia(..., Tqqq41AnclaNoche)`. Sacar el argumento.
- `:142` — campo `CalculoTqqq _tqqq`. BORRAR.
- `:158` — parámetro `bool tqqqNoche` del constructor de `HostFamilia`. BORRAR.
- `:183-196` — la construcción de `CalculoTqqq`, con la lambda `Cierres` que lee `cboe.Cierre(lb, f)`. BORRAR. La línea `var cboe = _cboe;` (:183) se usa también en :215 y :223: QUEDA.
- `:214` — `TqqqDeNoche = tqqqNoche` en `OpcionesMotorFamilia`. BORRAR.
- `:218` — `_motor.Configurar(..., _tqqq, ...)`. Sacar el argumento, o pasar `null` si queda la firma.
- `:222` — `TqqqNoche = tqqqNoche` en `OpcionesCambios`. BORRAR.
- `:314` — `foreach (var lb in new[] { "NDX", "QQQ", "TQQQ" })`, la edad de CBOE para el aviso. Sacar `"TQQQ"`; el renombre deja `"SPX","SPY","TQQQ"`.
- `:315` y `:318` — los textos "NDX/QQQ/TQQQ no se actualizan" y "NDX/QQQ/TQQQ congelados". Sacar "/TQQQ".
- `:339-342` — `Anotar()` busca la última `TqqqVela` en `_motor.VelasTqqq` y la pasa a `_cambios.Anotar(f, ultimo, tq)`. BORRAR la búsqueda y llamar sin `tq`.
- `:2`, `:105`, `:276` y `:332` — son comentarios.

### `_modulos/familia/fam/MotorFamilia.cs`
- `:105` — `OpcionesMotorFamilia.TqqqDeNoche`. BORRAR.
- `:119` — campo `ICalculoTqqq _tqqq`. BORRAR.
- `:130-132` — `_tq` (`Dictionary<long, TqqqVela>`), `_tqVersionCboe`, `_tqPasadaUtc` y `_tqVersionS`. BORRAR.
- `:154` — `PasadasTqqqPorS`. BORRAR; lo leen los arneses.
- `:168` — `VelasTqqq`. BORRAR; lo lee `IntegracionFamilia.cs:340`.
- `:179`, `:183` y `:197` — `Configurar(..., ICalculoTqqq tqqq, ...)`, `_tqqq = tqqq` y el log `", TQQQ"`. Sacar el parámetro y el texto.
- `:221` — `_tq.Clear(); _tqVersionCboe = -1; ...` en `AbrirSesion`. BORRAR.
- **`:366-402`** — el bloque "TQQQ por vela m2". BORRAR desde `:368` (`tqIni`) hasta `:402`. **Ojo: `:367 long formando` y `:370 var sucias` QUEDAN**, porque se usan después: `formando` en `:419` (`histFin`) y `sucias` en `:421-435`.
- `:434-435` — `_tq.TryGetValue(T, out var tv); SalidaFamilia.Entrada(T, _alm.Get, _tres, tv)`. Pasar sin la vela de TQQQ.
- `:481-495` — `MismaVela(TqqqVela, TqqqVela)`. BORRAR.
- `:553-555` — `tqU`, la última vela de TQQQ. BORRAR. En `:587-588` llamar a `SalidaFamilia.Actuales`/`Fuentes` sin ella. `:589` — `TqS`/`TqC`/`TqDatoUtc`: BORRAR.
- `:3`, `:23`, `:25`, `:161` y `:241` — comentarios ("sin TQQQ" en `:241` es correcto).

### `_modulos/familia/fam/SalidaFamilia.cs`
- `:24` y `:57-63` — `Entrada(..., TqqqVela tq)` y el bloque que agrega las series de la vela TQQQ a la historia. BORRAR el parámetro y el bloque.
- `:68` y `:112-126` — `Actuales(..., TqqqVela tqUltima)` y el bloque que arma `NivelActual` con `Libro = "TQQQ"` y `rol "dom (razon)"`. BORRAR el parámetro y el bloque.
- `:153` y `:163-164` — `Fuentes(..., TqqqVela tq, ...)` y la fuente `Libro = "TQQQ"` con el texto `"x3: NQ = {0:0.00} x TQQQ + {1:0.0} (+-4)"`. BORRAR.
- `:2`, `:4` y `:8` — comentarios.

### `_modulos/familia/fam/CatalogoFamilia.cs`
- `:39` — `SeriesTqqq` (6 ids). BORRAR; lo usa `SalidaFamilia.cs:113`, que también se va.
- `:55` — `"T_MUROS_oi"` en `PrendidasPorDefecto`. BORRAR. Ese arreglo hoy no lo usa el producto, solo los arneses.
- `:65-66` — los presets "TQQQ x3" y "TQQQ vs Gammalito". BORRAR. `Presets` no lo usa el producto.
- `:169-190` — las 6 `S("T_...")` (grupo "TQQQ x3", banda 4,0 salvo `T_DOMS_raz`, que tiene 0; colores `#ffd54f #26c6da #ba68c8 #b0bec5 #e8eaed #ff7043`). BORRAR.
  - **Índices fijos verificados: no se corren.** Las T_* están en las posiciones 25-30 de la lista y `Calc = l.Take(24)...` (`:246`) solo mira las primeras 24. `RegistroMinuto.Series` usa los índices de `Calc` y `Extra` usa los suyos, así que el formato de `niv-*.jsonl` no cambia.
- `:2-3`, `:25` y `:31` — comentarios ("29/35 series", "las 5 de TQQQ").

### `_modulos/familia/fam/SesionFamilia.cs`
- `:17`, `:54` y `:70` — `RuedaIniUtc`. Su único consumidor es `MotorFamilia.cs:368`, que es de TQQQ. Sin TQQQ queda sin uso: se puede dejar (no daña) o borrar.
- `:3` y `:16` — comentarios. `SiguienteIniUtc` QUEDA, porque lo usan `MotorFamilia.cs:263` y `:419`.

### `_modulos/familia/posiciones/CambiosFamilia.cs`
- `:45-46` — `OpcionesCambios.TqqqNoche`. BORRAR.
- `:71` — `PerfilK.De(TqqqPerfil.Perfil)`. BORRAR: si no, no compila sin la carpeta.
- `:107` — `CambiosLibro.EsTqqq`. BORRAR.
- `:160` — `foreach (var lb in new[] { "NQ", "NDX", "QQQ", "TQQQ" })` crea los `RegimenesOi`. Sacar `"TQQQ"`.
- `:167`, `:179`, `:183` y `:208` — el parámetro `TqqqVela tq` de `Anotar`/`Recalcular`, que entra en la clave de recálculo (`:179`). Sacar el parámetro y los dos campos de la tupla `clave`.
- `:226-230` — el bloque `_res["TQQQ"] = CalcularTqqq(...)`. BORRAR.
- `:231` y `:724` — los arreglos `{ "NQ", "NDX", "QQQ", "TQQQ" }` del estado y del log. Sacar `"TQQQ"`.
- `:266-277` — `CalcularTqqq`. BORRAR.
- `:284` — `if (r.EsTqqq) return PerfilK.De(TqqqPerfil.Calcular(f));` en `Valuar`. BORRAR.
- `:52-54` — `MinClavesSaltoFlaco = 8` y `FraccionSaltoFlaco = 0.80`: el "libro flaco", calibrado con TQQQ. Lo aplica `RegimenesOi` a TODOS los libros, así que QUEDA. Para SPY no estorba, que tiene muchas claves. Clasificación: IGUAL.
- `:9-10`, `:18`, `:20`, `:57`, `:104`, `:165`, `:279`, `:319`, `:344`, `:399-400` y `:410` — comentarios.
  - El de `:400` trae horas de publicación del OI de NQ, QQQ y TQQQ.
  - Según el informe del punto 8, la publicación de SPX y SPY va por otro horario.

### `_modulos/familia/posiciones/FotosCambios.cs`, `RegimenesOi.cs` y `ClavesCambios.cs`
- `FotosCambios.cs:19` — `VIDA_TQQQ_S = 1500`. BORRAR (`VIDA_NQ_S` y `VIDA_CBOE_S` quedan).
- `FotosCambios.cs:83-101` — `AhoraTqqq(...)`, que usa `TqqqSesion.De` (:89) y `cboe.Fotos("TQQQ")` (:90). BORRAR.
- `FotosCambios.cs:7`, `RegimenesOi.cs:10,13-14` y `ClavesCambios.cs:8,15` — comentarios.

### `_modulos/cboe/BajadorCboe.cs` y `CboeArchivo.cs`
- `BajadorCboe.cs:56` — `public string[] Libros = { "NDX", "QQQ", "TQQQ" };`. Es el default que usa `IntegracionFamilia.cs:167`, porque no pasa `Libros`. Sacar `"TQQQ"`: el renombre deja `{ "SPX", "SPY", "TQQQ" }`.
  - **Si queda, la 5.0 sigue bajando la cadena de TQQQ cada 75 s**: gasta bajada, que es justo lo que corta Rithmic según CLAUDE.md.
- `BajadorCboe.cs:143` — `case "TQQQ": return new LibroCboe { Nombre = "TQQQ", Archivo = "TQQQ", Simbolo = "TQQQ" };`. BORRAR.
- `BajadorCboe.cs:2` y `:4` — comentarios ("NDX -> QQQ -> TQQQ con 3 s entre tickers").
- `CboeArchivo.cs:24` — `RxDia = @"^cadena-(NQ|QQQ|TQQQ)-(\d{4}-\d{2}-\d{2})\.jsonl\.gz$"`. Sacar `|TQQQ`. El renombre lo deja `(ES|SPY|TQQQ)`, que coincide con el nombre de archivo de `cboe-local` (`cadena-ES-*` = SPX).
- `CboeArchivo.cs:3` y `:6` — comentarios.
- Opcional: el mecanismo `cierres.json`, cuyo único consumidor era TQQQ. Está en `BajadorCboe.cs:105,232-237,263,505-509,672,681,700,806` y `CboeArchivo.cs:30,156-289` (`CierreCboe`/`CierresCboe`). DEJAR (no rompe, sigue anotando cierres de SPX/SPY) o BORRAR todo junto. Si se borra, también la lambda `Cierres` de Integracion, que ya se va con TQQQ.

### `_modulos/pantalla/ArmadoPantalla.cs`
- `:67` — `public enum EjeFamilia4 { Ninguno, NDX, QQQ, TQQQ }`. Sacar `TQQQ`.
  - Valores numéricos hoy: Ninguno 0, NDX 1, QQQ 2, TQQQ 3. Quedan 0-2.
  - El tipo es nuevo (`PythiaGexCinco.GammaCinco`): ningún `.ws` lo tiene guardado, así que no importa el número.
  - Después del renombre: `{ Ninguno, SPX, SPY }`.
- `:84` — `AjustesPantalla.Eje = EjeFamilia4.TQQQ` (default del armado). Cambiar.
  - **DECISIÓN que se avisa:** `Ninguno` o `SPX`. Va con `FamiliaCuatroPantalla.cs:101` y con la prueba A10.
  - Regla del operador: ningún default que toque lo que se dibuja sin avisarle y sin captura antes/después.
- `:225` — `ColTqqq = (255, 213, 79)`. **NO borrar**: es el color de TODAS las marcas del doble eje (`:866`, `:868`, `:879`). Se puede renombrar a `ColEje`; el borrador lo deja `ColTqqq` porque lo protege.
- `:290-291` — `StrikeTqqq(k)`. BORRAR, junto con su uso en `:918`.
- `:530` — `case "ZTP": bas = lib + (lib == "TQQQ" ? " 0G" : " cruce")`. Simplificar a `" cruce"`.
- `:753` — cartel "esperando el primer cálculo de la Familia (todo adentro: libro NQ por Rithmic, NDX/QQQ/TQQQ bajados de CBOE)". El renombre deja ".../TQQQ". Editar.
- `:836-840` — el eje TQQQ x3: cota `F.TqS >= 20 && <= 1000`, `aLibro = (q − c)/s` y el título. BORRAR el bloque. El `else if` de `:841` pasa a `if`.
- `:878` — `aj.Eje == EjeFamilia4.TQQQ ? "TQQQ x3" : aj.Eje.ToString()`. Dejar `aj.Eje.ToString()`.
- `:918` — `a.Libro == "TQQQ" ? ("TQQQ " + StrikeTqqq(...))`. BORRAR la rama.
- `:1210` — "libro NQ por Rithmic + NDX/QQQ/TQQQ bajados de CBOE". Editar.
- `:1217` — `bool pideTq = aj.Eje == EjeFamilia4.TQQQ || libros.Contains("TQQQ");`. BORRAR, y sacar `pideTq` de `:1218` (`usaCboe`).
- `:1226` y `:1228` — `case "TQQQ":` y `fu.Libro == "TQQQ" ? pideTq : ...`. BORRAR.
- `:1268-1269` — "TQQQ sin dato en la sesión ...". BORRAR.
- `:1296` — `(x.Libro == "TQQQ" ? " por 1 % de TQQQ" : "")`. BORRAR.
- `:1301` — `a0.Libro != "TQQQ" &&`. BORRAR la condición.
- `:9`, `:478`, `:516`, `:914` y `:1294` — comentarios.

### `_modulos/pantalla/SeriesPantalla.cs`
- `:62-64` y `:70-72` — las 6 casillas `C("T_...")` del grupo "5. TQQQ x3". BORRAR.
- `:92` — `"T_MUROS_oi"` en `PrendidasPorDefecto`. BORRAR.
- `:125-127` y `:133-135` — las 6 `R("T_...")` del catálogo de respaldo (banda 4,0; `T_DOMS_raz` 0). BORRAR. Tiene que seguir igual a `CatalogoFamilia`: la prueba de la pantalla los compara campo por campo.
- `:83`, `:85` y `:98` — comentarios con el orden de grupos ("... y '5. TQQQ x3'").

### `_modulos/pantalla/FamiliaCuatroPantalla.cs`
- `:73-78` — las 6 propiedades `S_T_*`:

  | Propiedad | Nombre visible | Order | Default |
  |---|---|---|---|
  | `S_T_DOMS_vol` | "TQQQ dom vol" | 18 | false |
  | `S_T_MUROS_vol` | "TQQQ muros vol" | 19 | false |
  | `S_T_MUROS_oi` | "TQQQ muros OI" | 20 | **true** |
  | `S_T_ZERO_oi` | "TQQQ 0Γ OI" | 21 | false |
  | `S_T_ZERO_vol_pm` | "TQQQ 0Γ vol punto medio" | 23 | **true** |
  | `S_T_DOMS_raz` | "TQQQ razon (comparar)" | 22 | false |

  BORRAR. `S_T_ZERO_vol_pm` comparte el Order 23 con `S_FAM_MUROS_vol`.
- `:185-190` — los 6 `case "T_...": return S_T_...;` de `SerieVisible4`. BORRAR: si no se borran, no compila.
- `:100-101` — `Eje4Libro` ("Doble eje izq.", Order 35, **default `EjeFamilia4.TQQQ`**, descripción "...; TQQQ = (NQ − c) / s (x3 diario, NO regla de tres)..."). Cambiar el default (misma decisión que `ArmadoPantalla.cs:84`) y sacar la frase de TQQQ de la descripción.
- `:4` — comentario ("29 casillas S_<id>").

### `_modulos/pantalla/ContratosPantalla.cs`
- `:14` — comentario "El catálogo de las 29 series". Solo texto.

### Núcleo 3.0 (solo comentarios; ninguno toca el cálculo)
- `GammaHoyTres.cs:443` (comentario en la línea de `FamiliaAsegurarHost(raiz);`) y `:530-532`.
- `GammaHoyTresCapas.cs:129` y `:159-160`.
- `AdaptadoresFamilia.cs:18` (`tq_comun.precio_en`) y `:23`.
- `_modulos/familia/comun/Gamma.cs:4` y `_modulos/familia/ndx/LibroMinuteroNdx.cs:256`.

### Fuera de la DLL (rutas relativas a `PythiaGex\`)
- `herramientas/sembrar_4_0.py:8,11,35,37` — copia `cadena-{NQ,QQQ,TQQQ}` de `cboe-local`. La versión ES (si se hace un `sembrar_5_0`) copia `cadena-{ES,SPY}`, sin TQQQ.
- `herramientas/instalar_4_0.ps1` — sin TQQQ.
- `herramientas/grupo_arriba_415e.py` — en la `LISTA` (101 entradas), los índices 18-22 son `S_T_DOMS_vol`, `S_T_MUROS_vol`, `S_T_MUROS_oi`, `S_T_ZERO_oi` y `S_T_DOMS_raz`. Hay que sacarlos de una versión 5.0 del script; detalle en la sección de la pantalla.
- Arneses (detalle en el punto 8): compilan `_modulos/tqqq/*.cs` los csproj de `atas/_test_cuatro_paridad/{clasica,clasica_dom,e2e,posiciones,replica20,tqqq,tqqq/modulo_solo}`; hay que sacarles esos `Compile Include`. También usan TQQQ en el código:
  - `cboe/Program.cs`, `e2e/HostE2E.cs` y `e2e/Program.cs`;
  - `fam/Dobles.cs` y `fam/Program.cs`;
  - `posiciones/Program.cs` y `replica20/Program.cs`;
  - `tqqq/*`, que se borra;
  - `PythiaGexCuatro/_modulos/pantalla/prueba/Program.cs` (34 líneas con TQQQ) y `prueba/Dobles.cs` (9).
- Datos en disco de la 4.x, que la 5.0 no necesita ni migra:
  - en `familia\`: `tqqq-cierres.json` y 12 `tqqq-muestras-*-Z6.csv`;
  - en `cboe\`: `cadena-TQQQ-*.jsonl.gz`, `cadena-TQQQ.ultimo`, `ultima-TQQQ.json` y las entradas TQQQ de `cierres.json`;
  - el log `%APPDATA%\ATAS\pythiagex4-tqqq.log`.
- Docs (README, DISENO_4_1 anexo B, CHANGELOG): TQQQ en todos lados. Se reescriben para la 5.0.

### Orden para sacarlo sin romper la compilación
1. **Primero el renombre mecánico**, que deja TQQQ intacto. Después, todo esto a mano, en el clon.
2. **Pantalla:**
   - borrar las `S_T_*` y sus `case` (`FamiliaCuatroPantalla.cs:73-78`, `:185-190`) y las `C(...)`/`R(...)` T_* de `SeriesPantalla.cs`;
   - borrar `EjeFamilia4.TQQQ` y cambiar los dos defaults (`ArmadoPantalla.cs:84`, `FamiliaCuatroPantalla.cs:101`);
   - limpiar las ramas TQQQ de `ArmadoPantalla.cs` (`:291`, `:530`, `:836-840`, `:878`, `:918`, `:1217-1228`, `:1268-1269`, `:1296`, `:1301`) y los textos (`:753`, `:1210`);
   - el compilador marca cualquier uso olvidado del enum o de `F.TqS`.
3. **fam:** `SalidaFamilia` (3 firmas y 3 bloques), `MotorFamilia` (campos, bloque `:368-402` sin tocar `formando` ni `sucias`, `MismaVela`, `Publicar`, `Configurar`) y `CatalogoFamilia` (`:39`, `:55`, `:65-66`, `:169-190`).
4. **posiciones:** `CambiosFamilia` (`TqqqNoche`, `PerfilK.De(TqqqPerfil)`, `EsTqqq`, `CalcularTqqq`, el parámetro `tq`, los 3 arreglos) y `FotosCambios` (`AhoraTqqq`, `VIDA_TQQQ_S`).
5. **Integracion:** `Tqqq41AnclaNoche`, `_tqqq`, el parámetro `tqqqNoche`, la construcción `:184-196`, `:214`, `:218`, `:222`, `Anotar` `:339-342`, `AvisoFuentes` `:314-318` y el nombre de `Cboe41Bajar`.
6. **cboe:** `Libros` sin `"TQQQ"` (`BajadorCboe.cs:56`), el `case "TQQQ"` (`:143`) y el regex `CboeArchivo.cs:24`. Si se quiere, `cierres.json` también.
7. **Contratos:** borrar `TqqqVela`, `ICalculoTqqq`, `IVersionSTqqq`, `FotoFamilia.TqS`/`TqC`/`TqDatoUtc` y el parámetro de `IMotorFamilia.Configurar`; corregir los comentarios. Antes de esto, compilar los pasos 2-6 con los tipos todavía presentes: así cada error que tira el compilador es un uso real.
8. **csproj:** sacar `:104-109`. Borrar la carpeta `_modulos/tqqq`. Compilar.
9. **Arneses y herramientas:** borrar `_test_cuatro_paridad/tqqq` y sacar `Compile Include` y casos TQQQ del resto; la versión 5.0 de `grupo_arriba` sin las `S_T_*`.

### Conteos que cambian al sacar TQQQ
- `CatalogoFamilia.Series` pasa de 41 a 35 y `SeriesPantalla.Casillas` de 41 a 35. La prueba A8 de la pantalla espera 35, pero esa prueba ya quedó vieja: hoy hay 41, porque la 4.1.7/4.1.8 agregó 5 TOPN/CARGA.
- Casillas del grupo "0. PRENDER / APAGAR": la última cuenta del log es **108** (`pythiagex4-*.log`, "casillas 4.1.5e: 108 en el grupo...", 128 veces). Sin las 6 `S_T_*` quedan **102**.
- La `LISTA` de `grupo_arriba_415e.py` pasa de 101 a 96. Las pruebas A21a/A22b de la pantalla esperan 101 con Order 0..100 sin huecos.

### Lo más riesgoso de TQQQ y de la pantalla

1. **El eje SPY no se va a dibujar nunca.** La cota `30 < razón < 60` de `ArmadoPantalla.cs:841` está pensada para NQ/QQQ ≈ 41. ES/SPY ≈ 10,07 (medido, dato del 26-10) cae afuera siempre. Hay que cambiarla.
2. **La cadena de meses `FGHJKMNQUVXZ`** (`ArmadoPantalla.cs:262-263`, y 6 lugares más fuera de la pantalla). La regla `MNQ`→`MES` del borrador (`clonar_5_0.py:52`) la deja `FGHJKMESUVXZ`. En ES anda por casualidad, porque los meses H/M/U/Z quedan, pero el regex queda roto. Protegerla antes del renombre.
3. **TQQQ queda intacto después del paso mecánico, porque el borrador lo protege.**
   - Si no se saca a mano, la 5.0 sigue bajando TQQQ cada 75 s (`BajadorCboe.cs:56`).
   - Calcula una conversión x3 contra el precio de MES, que no tiene sentido. Sus T_* quedarían en precio de ES totalmente corridas.
   - El eje default sigue siendo TQQQ.
   - Hay que hacerlo en el orden de arriba. Contratos va al final; antes conviene compilar con los tipos todavía presentes, para que cada error del compilador sea un uso real.
4. **Al borrar el bloque TQQQ de `MotorFamilia.cs:366-402` no hay que llevarse `formando` (`:367`) ni `sucias` (`:370`).** Los usa la historia (`:419-435`). Si se borran, no compila. Y si se los "arregla" a ojo, la estela puede dejar de actualizarse.
5. **Los defaults de la 5.0 no los guarda ningún `.ws`: el código manda.** Hay que decidir el eje (`Ninguno` o `SPX`) y las casillas prendidas antes de instalar, y avisarle al operador con captura.
6. **Las descripciones y algunos textos quedan con números de NQ debajo de nombres de ES.** Por ejemplo: "un strike de SPY son 41 pts de ES, banda +-20", "ES x20", "~13 pts 16:48-17:11 ART", `AVISO_CLASICA`, "centroide ±12".
   - Mienten por omisión, que es justo lo que el proyecto quiere evitar.
   - Y la banda ±20 de las series SPY (`SeriesPantalla.cs:102,108,109,113` y `CatalogoFamilia`) es un número de la escala de NQ. Con el criterio de medio strike, en SPY sería ±5.
7. **Las pruebas de la pantalla y `grupo_arriba_415e.py` están clavadas en la 4.1.** Usan 35 y 101 casillas, nombres NQ, TQQQ, MNQZ6, y C1/C36 al revés. Además ya están viejas con el código de hoy: hay 41 casillas, y TOPN/CARGA tienen Order repetidos y están fuera de la `LISTA`. No sirven para validar la 5.0 sin rehacerlas.

---

## G. Punto 5: el núcleo 3.0 y sus capas (y los puntos 1, 2, 3 y 6 del núcleo, por archivo)

> Las "trampas del renombre" de esta sección se evaluaron contra dos variantes, `\b` y subcadena. Contra el borrador `herramientas/clonar_5_0.py` valen las conclusiones de B.

Este documento cubre el punto 5 entero y los puntos 1, 2, 3 y 6 restringidos a los archivos del núcleo.

**Rutas:** relativas a `PythiaGex\atas\PythiaGexCuatro\` salvo que se diga otra cosa.

**Versión leída:** el código es la 4.1.6g (`GammaHoyTres.cs:97`). Además trae piezas 4.1.7 y 4.1.8 del 12-10: `GammaHoyTresRayas.cs:54-78` y `GammaHoyTres.cs:1050`. README y CHANGELOG dicen 4.1.5e.

**Qué quiere decir "medido":** que lo saqué hoy, 27-10, de `%APPDATA%\ATAS`:
- de `pythiagex4-gammahoy.log`;
- de `PythiaGex4\...`;
- de `PythiaGex\cboe-local\...`, leído con `python -I`;
- de la captura `antes_MES_4_1.jpg` del scratchpad.

**Clasificación de cada número:**
- **ESCALA:** depende del precio o de la volatilidad del producto; se reescala.
- **PRODUCTO:** es un hecho del producto (multiplicador, paso de strike, horario).
- **IGUAL:** no depende del producto.

**Recordatorio del plan:** en el núcleo NO se renombran los tokens NQ. Solo se renombran:
- `PythiaGexCuatro` → `PythiaGexCinco`;
- `FamiliaCuatro` → `GammaCinco`;
- `PythiaGex4` → `PythiaGex5`;
- `pythiagex4-` → `pythiagex5-`;
- los mutex e hilos `PythiaGex4 …`.

---

### Punto 5: núcleo 3.0 y capas

#### a) Cómo detecta la raíz, todas las ramas por raíz y qué pasa hoy en un gráfico de MES

**Detección**
- `CadenaApi.cs:902-909` `RaizGrande`:
  - `MNQ*`/`NQ*` → "NQ";
  - `MES*`/`ES*` → "ES";
  - `M2K*`/`RTY*` → "RTY";
  - cualquier otro → "".
- `GammaHoyTres.cs:387-391` `Raiz()`: usa `Raiz3Manual` si no está vacío. Si no, usa `TradingManager.Security.Code` o `InstrumentInfo.Instrument`.
- `CadenaApi.cs:283,286`: la cadena solo arma con ES/MES, NQ/MNQ o RTY/M2K.
- `CadenaApi.cs:923-940` `CodigosGrande`: de MESZ6 saca ESZ6 y ESH7. Medido: "futuro del servidor: ESZ6".

**Ramas por raíz.** Son todas las que hay en el núcleo.
- `GammaHoyTres.cs:401` `RadioDominantes`, auto: ES 25, RTY 20, NQ 100 pts. Ya escala en ES.
- `GammaHoyTres.cs:402` `RadioDibujo`: ES 40, RTY 30, NQ 150.
- `GammaHoyTres.cs:403` `ZonaToque`: ES 0,75, RTY 0,6, NQ 2,5.
- `GammaHoyTres.cs:404` `Lejos`: ES 4, RTY 3, NQ 15.
- `GammaHoyTres.cs:413` `VerMajorsEf`: en ES los majors por volumen se ven siempre, con cualquier perfil.
- `GammaHoyTres.cs:435-439` y `:816-819`: raíz "" → no hace nada.
- **`GammaHoyTres.cs:1187`** `AsegurarAdaptadoresFamilia`: `if (raiz != "NQ") return;`. **En ES nunca se toman ICinta ni ILibroNq.**
- **`Viva3.cs:63`** `if (raiz == "NQ") Familia.LibroNqFamilia.Para(raiz).AgregarLinea(...)`. La foto viva3-ES va al archivo pero no a la familia.
- **`GammaHoyTresCapas.cs:323`** `if (raiz != "NQ" || fut <= 0) return;   // ... en ES irian SPX/SPY (pendiente)`. **Sin capas en ES.**
- `GammaHoyTresCaja.cs:367` `esc = raiz == "ES" ? 0.25 : 1.0`. La caja ya escala sus umbrales de resultado.
- Literales "NQ" que no son ramas pero quedan fijos en ES:
  - `GammaHoyTresMajorOi.cs:119`: es la clave interna del libro primario. El rótulo sale de `Candidatos` con la raíz, así que en pantalla queda bien.
  - `GammaHoyTresCaja.cs:125,128,163,232`: la familia "NQ" queda grabada en `caja-ES-*.jsonl`. Es cosmético, pero el lector `caja_negra.py` lo usa como etiqueta.
- `GammaHoyTresMemoria.cs:86` `SembrarMemoriaCapas` no mira la raíz.
  - En la 4.1 sobre MES siembra las estelas NDX/QQQ guardadas en escala NQ (~29.000). Quedan fuera de pantalla: es inocuo.
  - En `PythiaGex5` esas estelas no van a existir.
- Fuera del núcleo, pero en la misma cadena de efectos:
  - `IntegracionFamilia.cs:60-62` (familia, que sí se renombra): con `raiz != "NQ"`, o sin cinta o sin libro, no arranca el host.
  - `_modulos/pantalla/ArmadoPantalla.cs:750-751`: si el instrumento no contiene "NQ", muestra el cartel "no dibujo nada".

**Qué hace hoy la 4.1.6g en MESZ6 (medido).**
- La fuente es `pythiagex4-gammahoy.log`, entre el 26-10 22:49 y el 27-10 00:43 ART. El log lo comparte con la instancia de MNQ que corre al mismo tiempo.
- **Arranque**, tres veces: `raiz=ES ... radioDom=25 radioDibujo=40 zona=0.75 lejos=4 tope=320 ventana=1.0/2.0 % vencimientos=2`.
- **Cadena:**
  - futuro ESZ6 en 7724, vía servicio con RithmicConnector;
  - 31-32 series listadas: Weekly de cada día y "10-30 EndOfMonth";
  - 2 elegidas: "10-27 Weekly sobre ESZ6" con 726 contratos y "10-28 Weekly" con 728, las dos "vence 16:00 NY";
  - **184 suscritos de 1454**: no llega al tope 320 (en MNQ: 320 de 1814);
  - 181 con puntas;
  - **46 strikes útiles** (en MNQ: 89).
- **Cuenta:** `AUDIT3 fut=7724.50 origen=libro_ES_vivo_(API),_sin_base strikes=46 zeroVol=7727.46 zeroOi=7720.95 mpVol=7750.00 mnVol=7710.00 doms=7727.93=1M/7720.89=-1M`.
  - De noche, con la regla Tres, las dominantes son el túnel por cruces.
  - El "1M" es el relleno ±1e6 de `GammaHoyTresTunel.cs:122-123`.
- **Strikes de ES cada 5 pts:** perfilOI de CRUCES3 = 7675, 7680 … 7770.
- **Archivos:**
  - `PythiaGex4\estela\estela-ES-2026-10-27.jsonl`: 173 KB, con `"gm":[null,null]` (ES no tiene unidad de familia);
  - `PythiaGex4\viva\viva3-ES-*`.
- **Familia:** cada minuto de la instancia ES dice `| familia: cinta - | libro -`. No hay adaptadores, host, descarga CBOE, motor, FotoFamilia ni `AlEstela`, así que no hay TRES_*.
- **Pantalla:** en la captura `antes_MES_4_1.jpg` solo está el cartel naranja de la 4.0: "PythiaGex 4.0: los niveles estan en precio de NQ/MNQ; este grafico es MES: no dibujo nada". De la 3.0 no se ve nada, porque las casillas Tres41* vienen apagadas.

**Qué queda vacío o mal en ES con el renombre planeado y el núcleo sin tocar**
1. **La familia entera queda vacía.**
   - `GammaHoyTres.cs:1187` y `Viva3.cs:63` siguen pidiendo "NQ".
   - La familia renombrada sí espera "ES" (`IntegracionFamilia.cs:60`).
   - Pero `IntegracionFamilia.cs:62` corta con `cinta == null || libro == null`.
   - Resultado: ni CBOE, ni motor, ni rayas.
2. **No hay capas SPX/SPY:** las cortan `GammaHoyTresCapas.cs:323` y la lista de `GammaHoyTresCapas.cs:120-124`.
3. **No hay majors OI de SPX/SPY:** salen de las capas (ver c).
4. **TRES_ES, TRES_SPX y TRES_SPY quedan vacíos aunque se arregle el punto 1.** El motor tira toda línea con d ≤ 15000: `_modulos/familia/fam/AlmacenTres.cs:136`, documentado en `GammaHoyTres.cs:760`. ES cotiza en ~7.700.
5. **Recuadro y cabecera funcionan,** porque usan la raíz:
   - "LIBRO ES ▸" en `GammaHoyTresRecuadro.cs:175,225`;
   - "PythiaGex 4.0 · ES vivo (API)" en `GammaHoyTres.cs:1110`.
   - Lo único malo es que dicen "4.0" (ver f).
6. **"LIBRO NQ", "3.0 NQ dom▸", "NQ D1", etc. solo aparecen en Name/Description de los ajustes** (ver g). Los rótulos del lienzo usan la raíz ("ES D1", "ES 0Γ", "ES M+ OI"): `GammaHoyTres.cs:984-994`.
7. **Caja (apagada por defecto):** los placebos ±12,5/±25 no se escalan (`GammaHoyTresCaja.cs:127`).

#### b) Capas (`GammaHoyTresCapas.cs`): qué generalizar para que en ES dibuje TRES_SPX y TRES_SPY

**De dónde lee la cadena.** El núcleo no baja nada.
- Lo que lee:
  - Primero, la memoria que le pasa el integrador: `FuenteCapaEnMemoria(k.Archivo)` (`:133`, `:336`), siempre que tenga 20 min o menos (`:337`).
    - La asigna `IntegracionFamilia.cs:247-254` con `cboe.UltimoRegistro(arch == "NQ" ? "NDX" : arch)`.
  - Si no, el archivo `%APPDATA%\ATAS\PythiaGex4\cboe\ultima-<Archivo>.json` (`:132`, `:334`), con la misma frescura de 20 min (`:338`).
    - Lo escribe el descargador de la familia con `CboeArchivo.cs:28`, en formato de línea flaca.
    - Las claves salen de `_modulos/cboe/BajadorCboe.cs:141-143`:
      - NDX → `Archivo = "NQ"`, `Simbolo = "_NDX"`;
      - QQQ → `"QQQ"`;
      - TQQQ → `"TQQQ"`.
- Lo que no lee: `cboe-local` (la 3.0 sí lo leía, ver `:127-131`, `:297-300`), la nube ni la base de la clásica.
  - Dato útil: `PythiaGex\cboe-local` (lo escribe `cboe_local.py`, un programa externo) usa la misma convención. Allí `ultima-ES.json` es SPX y `ultima-SPY.json` es SPY, ambos presentes hoy.
- Formato que parsea, en `:356-404`:
  - `cadena.ts`, `cadena.ultimo_trade` (hora NY), `cadena.spot_idx`, `generado`, `base_cruda`, `base_error_ticks`;
  - `vencimientos[].dias`;
  - `filas` de 8 números: strike, venc, oi_call, oi_put, iv_call, iv_put, vol_call, vol_put.
  - Medido: las líneas de SPX ("ES") y de SPY en cboe-local traen exactamente esas claves y esos campos.

**Archivos de estado**

Rutas:
- `:157` `RutaBaseRueda` = `CarpetaDatos\base-rueda-<Archivo>.json` para base aditiva, o `razon-rueda-<Archivo>.json` para razón.
  - Hoy: `PythiaGex4\base-rueda-NQ.json` (capa NDX) y `razon-rueda-QQQ.json`.

Quién escribe y quién lee:
- Escribe `Mapear`, en `:224-228`, cuando hay 5 o más muestras buenas.
  - Formato: `{"base":…, "utc":"yyyy-MM-ddTHH:mm:ss", "muestras":N, "tipo":"base"|"razon"}`.
  - Medido: base-rueda-NQ = `{"base":163.57,"utc":"2026-10-26T20:17:55","muestras":22,"tipo":"base"}` y razon-rueda-QQQ = `{"base":41.344455,…,"muestras":23,"tipo":"razon"}`.
- Lee `CargarBaseRuedaSiHaceFalta` (`:292-317`), solo si el archivo tiene menos de 24 h (`:310`). Desde la 4.1 ya no lee la de la clásica.

Cómo quedarían en la 5.0:
- `PythiaGex5\base-rueda-ES.json` (capa SPX, con Archivo = "ES") y `PythiaGex5\razon-rueda-SPY.json`.
- No chocan con `PythiaGex\base-rueda-ES.json` de la clásica ni con `PythiaGex2\base-rueda-ES.json` de la 2.0: tienen el mismo nombre pero están en otra carpeta.

**Conversión** (`Mapear`, `:195-268`). Es la regla 3.0.7/4.1.1.
- **Hora de la muestra:** la vela del gráfico a la hora del spot = sello `cadena.ts` − 900 s (`:377-378`). Si el sello no se puede leer, usa `generado − 960 s`.
  - La 3.0 usaba `generado − 16 min`; la 4.1 ancla al sello.
  - Si no hay esa vela, usa la del último trade (`:375`, `:209-210`).
- **Valor de la muestra:**
  - con `PorRazon`: vela / spot;
  - con base: vela − spot.
- **Estado del spot:**
  - historial `SpotHist` de 60 (`:201`);
  - "vivo" = cambió en los últimos 10 min (`:202-203`);
  - "congelado" = 2 o más iguales durante 6 min o más (`:204-205`);
  - "fresca" = 20 min o menos (`:206`).
- **Mediana:** de las últimas 24 muestras (`:216`), con corte en max(3 MAD, 0,02 %) (`:220`); se usa con 3 o más buenas (`:222`) y se persiste con 5 o más (`:223-228`).
- **Orden de prioridad:**
  1. La mediana, si tiene 24 h o menos y además el spot está quieto, el dato es viejo o hay 5 o más muestras (`:231-240`).
  2. Si no, y el spot está quieto: la conversión de la familia, C7 para la base de NDX o C8 para la razón de QQQ (`:241-246`, `ConversionDeLaFamilia` `:272-290`).
  3. Si no, la muestra alineada (`:247-251`).
  4. Si no, la cruda, con el precio actual (`:252`).
- **Solo NDX:** la base de forwards (`base_cruda` del archivo) manda si `Capa3BaseNdx == Forwards` (el default, `:93`) y además `|forwards − mapa| ≤ 40` o la otra es dudosa (`:255-259`).
- Si queda dudosa o NaN, no dibuja (`:260-264`).
- **Medido el 26-10 en cboe-local:**
  - SPX: ts − ultimo_trade tiene mediana **902 s** (n=240, p10 901, p90 902).
  - SPY: mediana **901 s** (p10 900, p90 901).
  - Conclusión: el retraso de 900 s sirve para los dos.
  - El spot de SPX queda **congelado** después de las 16:15 NY: 7676,77 de 22:50Z a 23:10Z, con ultimo_trade 16:14:59.
  - El de SPY **se mueve en el after-hours**: 765,31 → 765,76 → 765,43 entre 20:45Z y 23:10Z.
  - Es el mismo patrón que NDX y QQQ, así que la regla del reloj del spot aplica tal cual.
  - `base_cruda` de SPX: 33,9 a 35,5 a lo largo del 26-10, y 37,18 en `ultima-ES.json` el 27-10 a las 02:48Z. NDX hoy da 152,74.
  - Razón MES/SPY ≈ 7724/766,28 ≈ **10,08**. MNQ/QQQ es 41,34.

**Constantes de NDX/QQQ adentro de las capas.** El detalle por línea está en el bloque del archivo.
- `:283` cotas de `ConversionDeLaFamilia`:
  - razón entre 30 y 60: **rechaza SPY (~10,1)**, así que con el spot quieto nunca habría conversión de la familia;
  - base entre 0 y 2000.
- `:255` tolerancia de 40 pts entre la base de forwards y la rueda: **es más que toda la base de SPX (~35)**, así que como control no sirve.
- `:89` radio de dibujo 60; `:64` fusión 5; `:423` centroide 12; `:126` vida 25 min; `:164-165` retraso 900/960 s.

**Nombres de series y estelas.** Hoy: `Nombre "QQQ"/"NDX"` y `Archivo "QQQ"/"NQ"` (`:122-123`).
- `Nombre` define:
  - la estela `estela-QQQ`/`estela-NDX` (`:434` → `GammaHoyTres.cs:756`);
  - `AlEstela(Nombre, línea)` (`GammaHoyTres.cs:762`);
  - el rótulo "`<Nombre>` D1" (`:623`) y "`<Nombre>` 0Γ techo/piso" (`:629-630`, `:634`);
  - `Fuente "<Nombre> CBOE"` (`:403`);
  - la búsqueda en `FamiliaFoto.Fuentes` (`:281`);
  - las casillas por nombre (`ModoDe :135`, `CercoCapa :153`, `RayaCapa*` en Rayas.cs, `MajOiVisible` en MajorOi.cs).
- `Archivo` define `ultima-<Archivo>.json`, `FuenteCapaEnMemoria(Archivo)` y `base-rueda-`/`razon-rueda-<Archivo>.json`.

**Qué hay que cambiar a mano en el núcleo para la 5.0.** Ninguno de estos cambios lo hace el renombre planeado.
1. `GammaHoyTresCapas.cs:120-124`: la lista `_capas` pasa a ser:
   - `{ Nombre "SPY", Archivo "SPY", PorRazon true }`;
   - `{ Nombre "SPX", Archivo "ES", PorRazon false }`.
   - `Archivo` tiene que ser la clave con la que el descargador renombrado escriba `ultima-<X>.json`. Con el renombre `BajadorCboe.cs:141` pasa de "NQ" a "ES", que es además la convención de cboe-local.
   - Ojo: `Nombre` no puede ser "ES", porque chocaría con la estela y el `AlEstela` del libro primario.
2. `GammaHoyTresCapas.cs:323`: `raiz != "NQ"` → `raiz != "ES"`.
3. `GammaHoyTresCapas.cs:135` `ModoDe`: `k.Nombre == "QQQ"` → "SPY" (o mejor `k.PorRazon`). Si no, la capa SPY queda gobernada por `Capa3NDX`.
4. `GammaHoyTresCapas.cs:153` `CercoCapa`: "NDX" → "SPX". Si no, no hay techo ni piso del 0Γ.
5. `GammaHoyTresCapas.cs:283`: cotas de razón SPY y de base SPX. Valores medidos: 10,08 y 34-37. El rango final lo fija quien mida.
6. `GammaHoyTresCapas.cs:255`: la tolerancia de 40 pts se pasa a la escala de SPX.
7. `GammaHoyTresRayas.cs:98-113` (`RayaCapa`, `RayaCapaZero`, `EnfasisCapaZero`): "NDX"/"QQQ" → "SPX"/"SPY". Si no, SPX y SPY caen en `return true`/false: sin casilla por raya y sin énfasis.
8. `GammaHoyTresMajorOi.cs:48-53` `MajOiVisible`: lo mismo. Si no, los gobiernan `Raya3NqMasOi`/`MenosOi`.
9. `GammaHoyTres.cs:1187` y `Viva3.cs:63`: "NQ" → "ES". Sin esto no corre nada de la familia, tampoco el descargador CBOE, así que no habría ni siquiera `ultima-*.json`.
10. **Lo que tiene que coincidir del lado de la familia,** fuera del núcleo:
    - `IntegracionFamilia.cs:250`: el mapeo `"NQ" → "NDX"`; tras el renombre queda `"ES" → "SPX"`, que coincide con el `Archivo` "ES".
    - `BajadorCboe.cs:141`: el `"_NDX"` **no lo toca un regex con `\b`**, porque "_" es carácter de palabra; hay que poner `"_SPX"` a mano.
    - `BajadorCboe.cs:142`: el símbolo "QQQ" → "SPY".
    - `AlmacenTres.cs:29`: las claves de `VidaS`.
    - `AlmacenTres.cs:136`: el filtro d > 15000.
    - `AlmacenTres.cs:182-183`: unidades de gm (ver c).
    - `FotoFamilia.Fuentes[].Libro`: tiene que decir "SPX"/"SPY" para que `:281` la encuentre.
11. Name y Description de `Capa3QQQ`, `Capa3NDX`, `Capa3BaseNdx`, `Capa3FusionPts`, `Capa3LetraPct`, `Capa3RadioDibujoPts`, `Capa3Regla` y de las Raya3Ndx*/Raya3Qqq* (ver g). Los colores (`:122-123`) pueden quedar.
12. Las capas toman el radio de dominantes de la primaria (`:418`, en ES 25 pts) y el centroide 12 (`:423`).
    - Con `Capa3Regla = Clasica` (default, `:84`) el centroide queda prendido.
    - En SPX, 12 pts son 2,4 strikes. El comentario de `GammaHoyNucleo.cs:69` dice que el 12 nació para SPX.
13. **No hay contra qué comparar TRES_SPX ni TRES_SPY en ES.** La 2.0 y la clásica no dibujan capas en un gráfico de ES (dato del punto 7, de otro agente). Además, el radio de dominantes del núcleo en ES (25 pts) **no es el de la 2.0 ni el de la clásica**: las dos usan min(2 %, 100 pts) sin rama ES, según `PythiaGexDos/GammaHoy.cs:403,408` y `PythiaGexNiveles/GammaHoy.cs:371,376`, verificado.

#### c) Majors OI de NDX/QQQ y rayas TRES_*

**Majors OI de las capas**
- **Cálculo:** con el mismo núcleo de cada capa, `GammaHoyNucleo.cs:409-418`: M+ y M− OI sobre todo el perfil, sin radio.
- **Por vela:** `GammaHoyTresCapas.cs:462` (`AnotarMajorOiCapa`).
- **Persistencia:** el campo `"m":[M+,M−]` de la estela de la capa (`GammaHoyTres.cs:742`), que se vuelve a sembrar en `GammaHoyTresCapas.cs:494`.
- **Dibujo:** `PintarMajorOi` (`GammaHoyTresMajorOi.cs:101-171`). Lo llama `GammaHoyTres.cs:963` con la llave **`Tres41Dominantes`, no `Tres41Capas`**.
- **Visibilidad por nombre:** `GammaHoyTresMajorOi.cs:48-53`.
- **Radio:** `MajorOi3RadioMaxPts` 3000, con un piso de 20 (`:43`, `:111`). Significa "siempre", también en ES.
- **Rótulo:** "`<Nombre>` M+ OI" más la edad del dato (`:166`, `:131`).
- **En ES:** salen solos en cuanto existan las capas SPX/SPY y se cambie `MajOiVisible`.

**Rayas TRES_*.** Son de la familia, pero nacen en el núcleo.
- **Dónde nacen:** `GammaHoyTres.cs:756-762`. Es la misma línea que va a `estela-<raiz>-<día>.jsonl` y solo la postea el dueño de la raíz (`:744-752`, 180 s).
  - Raíz "NQ" para el libro primario.
  - "NDX"/"QQQ" para las capas (`GammaHoyTresCapas.cs:434`).
- **Quién la recibe:** `AdaptadoresFamilia.AlEstela`, que se arma en `IntegracionFamilia.cs:245`.
- **Quién la consume:** `_modulos/familia/fam/AlmacenTres.cs`:
  - vida 300 s para NQ y 1500 s para NDX/QQQ (`:29`);
  - `d > 15000` (`:136`);
  - unidades de gm (`:182-183`).
- **El campo `"gm"`** lo calcula el núcleo con `AlmacenTres.MontosDominantes3(raiz, …)` (`GammaHoyTres.cs:774-785`).
  - Hoy ES da null (medido).
  - Tras el renombre, ES quedaría ×0,2 (20/100, el de NQ). Lo correcto es 0,5 (50/100). NDX/SPX y QQQ/SPY quedan en ×1.
- **Con el núcleo arreglado (b.1-b.9),** ES postea "ES", "SPX" y "SPY", que coinciden con las claves renombradas de la familia. Pero `AlmacenTres.cs:136` las sigue tirando todas hasta que se corrija.
- `GammaHoyTresRayas.cs` no tiene TRES. Solo tiene las casillas de dibujo de la 3.0 (Raya3*) y las de "carga" (4.1.8), que escriben en `Familia.ReglasFam` (`:60-78`).

#### d) Constantes de escala NQ en el núcleo

Están completas en el bloque de cada archivo. Las ramas que ya escalan por raíz son solo cuatro:
- `GammaHoyTres.cs:401-404`: ES 25/40/0,75/4, valores del PRE_REGISTRO, sin validar;
- `GammaHoyTres.cs:413`;
- `GammaHoyTresCaja.cs:367`;
- los porcentajes del núcleo, que son IGUAL.

**Constantes ESCALA fijas en puntos NQ que hoy no miran la raíz:**
- `GammaHoyTresFormula.cs:68`: F5/F6 con ±100 pts fijos.
- `GammaHoyTresFormula.cs:58`: 150.
- `GammaHoyTresRecuadro.cs:78`: muros ±100.
- `GammaHoyTresApoyo.cs:38`: 20.
- `GammaHoyTresApoyo.cs:51`: 60.
- `GammaHoyTresCapas.cs:89`: 60.
- `GammaHoyTresCapas.cs:64`: 5.
- `GammaHoyTresCapas.cs:255`: 40.
- `GammaHoyTresCaja.cs:127`: ±12,5/±25.
- `GammaHoyTresCaja.cs:192`: 3 pts.
- `GammaHoyTresHisteresis.cs:289`: 6 pts. Con `Rayas3Independientes = true`, que es el default, queda en 0.
- `GammaHoyTres.cs:558`: 12.
- `GammaHoyTresCapas.cs:423`: 12.
- `GammaHoyTresRayas.cs:78`: carga, techo de 300.
- `GammaHoyTresMajorOi.cs:43`: 3000.
- `GammaHoyTres.cs:500`: ±50 pts, solo en el log.

#### e) Calendario y producto del núcleo (opciones de ES por la API de ATAS)

**Cómo arma el libro**
- `CadenaApi.cs:306-324`: busca el futuro grande y lo suscribe.
  - Su precio es el mid de las puntas (`:746-758`).
  - Nunca se desuscribe (`:876-877`).
- `CadenaApi.cs:355-383`: series del futuro, con una tanda de consultas cada 30 s como máximo y caché de 10 min.
  - Se queda con las **2 fechas** vivas más cercanas (`:379`, `Vencimientos3 = 2`, `GammaHoyTres.cs:164-166`).
  - El viernes trimestral la misma fecha trae dos series: Regular 9:30 y Weekly 16:00.
  - Si falta el vencimiento de hoy, deja el aviso "OJO: sin el vencimiento de HOY" (`:383`).
  - Medido en ES: tipos "Weekly" (una por día, de lunes a viernes) y "EndOfMonth" (10-30).

**Hora de vencimiento**
- `CadenaApi.cs:373` `VenceAm` = Regular **y** el mismo día que vence el futuro o el anterior → 9:30 NY.
- Todo lo demás (Weekly, EndOfMonth) vence a las 16:00 NY (`RelojNy.cs:47-49`).
- Medido en ES: "vence 16:00 NY" en las Weekly.
- **SIN VERIFICAR:** si ATAS lista como "Regular" una serie mensual de ES de un mes no trimestral (tercer viernes), el núcleo la trata como 16:00. El log solo imprime las primeras 8 series.

**Series vencidas**
- Quedan fuera del libro desde el minuto cero (`CadenaApi.cs:851`).
- La suscripción tiene 30 min de gracia (`RelojNy.cs:58-61`).
- Hay rearme por vencidas cada 15 min y uno periódico cada 6 h (`GammaHoyTres.cs:454-465`).

**Roll trimestral** (regla 1.10d/1.11c)
- `CadenaApi.cs:326-353`: busca el trimestre anterior con `CodigoTrimestreAnterior` (`:943-954`, ciclo "HMUZ"), por ejemplo ESZ6 → ESU6. Lo suscribe y mide el spread.
- `CadenaApi.cs:361-371`: agrega las series que solo lista el viejo y pide con la serie del viejo (`UnderlyingCode = U6`) las que le pertenecen.
- `CadenaApi.cs:898-899` `EsDelAnterior`: una serie es del viejo si vence antes que él, o el mismo día y es Regular. La Weekly de la tarde de ese viernes es del nuevo.
- `CadenaApi.cs:842-844`: los strikes del viejo se corren por `DesplazamientoAnterior` (`:760-777`):
  - se calcula en el mid del viejo;
  - se redondea a 0,25 (el tick de ES y de NQ);
  - tiene histéresis de 1 pt;
  - se congela desde las 9:30 del día en que vence el viejo.
- `CadenaApi.cs:769-770`.
- Es igual para ES: mismo ciclo y misma bolsa (CME, `:337`).

**Ventana de strikes**
- `CadenaApi.cs:683-710`: todos los strikes a ±1 % y uno de cada dos hasta ±2 %. El paso del strike es la mediana de las diferencias (default 5, `:695`). El tope es 320.
- Medido: los strikes de ES van cada 5 pts. ±1 % son ~77 pts en ES y ~295 en NQ.

**Horarios.** Son todos iguales en ES.
- CME de domingo 18:00 a viernes 17:00, con pausa de 17 a 18 (`RelojNy.cs:63-74`).
- Rueda de 9:30 a 16:00 NY (`GammaHoyNucleo.cs:34-43`, `GammaHoyTresHisteresis.cs:73-80`).
- Tramos de la histéresis: 9:30-18:00 y 18:00-9:30 (`GammaHoyTresHisteresis.cs:64-69`).
- Inicio de sesión de la estela a las 18:00 NY (`GammaHoyTres.cs:607`).
- Día de sesión de la cinta: 22:00 UTC (`GammaHoyTresCinta.cs:175-176`). Con horario de invierno cae en la pausa de las 17:00, así que no pasa nada.
- Feriados: el núcleo no los conoce (`RelojNy.cs:64-65`, `GammaHoyTres.cs:1138-1139`).

**Multiplicadores**
- `GammaHoyNucleo.cs:149` `MULT_INDICE = 100` para todos los libros (`:205`, `:214`, `:226`). Los reales son NQ 20, **ES 50**, SPX 100 y SPY 100.
- Solo cambia magnitudes: los montos del recuadro (`GammaHoyTresRecuadro.cs:129-130`) y los campos "g"/"n" de la estela (`GammaHoyTres.cs:753-754`).
- No mueve rayas, porque todas las selecciones del núcleo son relativas (máximo, % del mayor, 5 %, 80 %).
- La conversión a unidades de familia está en `AlmacenTres.cs:182`: con el renombre quedaría ES ×0,2, cuando debería ser 0,5.

**Capas CBOE**
- El núcleo confía en los `vencimientos[].dias` del archivo. Que el tercer viernes conviva la SPX AM con la SPXW PM es tema del calendario de la familia (CboeCadena/CalendarioNdx), no del núcleo.
- Los días se envejecen desde `generado` (`GammaHoyNucleo.cs:244-248`).
- Horizonte "Hoy": días ≤ max(1, masCerca + 0,01) (`:233`).

#### f) Rutas, archivos, mutex, hilos y logs del núcleo

**Carpeta y logs**
- `Registro.cs:26` `CarpetaDatos` = `%APPDATA%\ATAS\PythiaGex4` → PythiaGex5. El renombre lo deja bien.
- `Registro.cs:28` logs `%APPDATA%\ATAS\pythiagex4-<nombre>.log` → pythiagex5-. Queda bien.
  - Nombres que usa el núcleo: "gammahoy" (`GammaHoyTres.cs:96`), "pantalla" (`GammaHoyTresCasillas.cs:34`, compartido con la pantalla de la familia) y "viva3" como respaldo (`Viva3.cs:63,66`).
  - Medido: hoy las instancias de MNQ y de MES escriben el mismo `pythiagex4-gammahoy.log`. En la 5.0 queda aparte.
- `Centinela.cs:53` `%APPDATA%\ATAS\pythiagex4-centinela-hoy-<instr>-<marco>.jsonl` → pythiagex5-. Queda bien. Está apagado por defecto (`GammaHoyTres.cs:280`).

**Archivos bajo CarpetaDatos.** Los que dependen de la raíz quedan bien solos en ES; los que dependen de la capa necesitan el arreglo b.1.
- `estela\estela-<raiz|Nombre>-<díaUTC>.jsonl`: lo escribe `GammaHoyTres.cs:756` y lo lee `GammaHoyTresMemoria.cs:188`. En ES quedan estela-ES, estela-SPX y estela-SPY.
- `viva\viva3-<raiz>-<día>.jsonl`: lo escribe `Viva3.cs:59` y lo lee `GammaHoyTresMemoria.cs:226`. En ES, viva3-ES.
- `caja\caja-<raiz>-<día>.jsonl`: `GammaHoyTresCaja.cs:406`.
- `rebobinado\hist-<raiz>-<día>-<sello>.jsonl`: `GammaHoyTresMemoria.cs:281`.
- `base-rueda-<Archivo>.json` y `razon-rueda-<Archivo>.json`: `GammaHoyTresCapas.cs:157`.
- `cboe\ultima-<Archivo>.json`, que el núcleo lee: `GammaHoyTresCapas.cs:132,334`.
- `cinta\cinta-<raiz>-<día>.csv`, con `.instrumento`, `-relleno.csv` y `.listo`: `GammaHoyTresCinta.cs:63,177,541,734,807`. Está apagado por defecto.
- `estado\indicador.json`: `GammaHoyTresEstado.cs:49`. Apagado por defecto.

**Choques entre versiones**
- No hay choques mientras `CarpetaDatos` sea PythiaGex5: la clásica usa PythiaGex, la 2.0 PythiaGex2, la 3.0 PythiaGex3 y la 4.1 PythiaGex4.
- El nombre `base-rueda-ES.json` se repite con la clásica, pero en otra carpeta, y la 4.1 ya no la lee.

**Hilos, mutex y estáticos**
- Hilo `"PythiaGex4 cinta"` (`GammaHoyTresCinta.cs:363`) → "PythiaGex5 cinta". Queda bien.
- **Mutex: ninguno en el núcleo.**
- Estáticos por ensamblado. La 5.0 es otro ensamblado, así que no los comparte con la 4.1:
  - escritor único de la estela: `GammaHoyTres.cs:139-143`;
  - escritor único de viva3: `Viva3.cs:29-30`;
  - escritor único de la cinta: `GammaHoyTresCinta.cs:146`;
  - espaciador de suscripciones: `CadenaApi.cs:177-187`.
  - Para los archivos eso está bien. Para la API, la 4.1 y la 5.0 van a armar a la vez al reiniciar ATAS: las dos esperan `DESFASE_PRIMER_ARMADO_S = 20` s (`CadenaApi.cs:182`). Conviene 40 s en la 5.0. Clasificación IGUAL; el efecto real está SIN VERIFICAR.

**Nombre visible del indicador**
- `[DisplayName("PythiaGex 4.0 - Gamma Familia")]` y `[Category("PythiaGex 4.0")]` en `GammaHoyTres.cs:92-93`.
- Textos "PythiaGex 4.0 " en `:346`, `:380`, `:818` y `:1110`.
- **El renombre planeado no los toca,** porque llevan espacio y "4.0". Hay que cambiarlos a mano por algo como "PythiaGex 5.0 - Gamma ES". Si no, el diálogo Indicators muestra dos "PythiaGex 4.0 - Gamma Familia".
- Lo mismo con `VERSION` (`:97`) y `MOTOR_TRES` (`:98`). En el csproj, `Product` y `AssemblyVersion` 4.0.0.0 (ese truco de compatibilidad con el .ws del visor 4.0.6 no aplica a un tipo nuevo).

**Símbolos CBOE**
- El núcleo no tiene ninguno. Los fija `BajadorCboe.cs:141-143`, en la familia.

#### g) Ajustes públicos del núcleo que mencionan NQ, NDX, QQQ o TQQQ

La lista completa, con línea y default, está en el bloque (6) de cada archivo. Ninguno menciona TQQQ.

**Persistencia:** ATAS persiste por nombre de propiedad y por tipo. GammaCinco es un tipo nuevo y ningún .ws tiene valores guardados para él. **Renombrar identificadores es gratis para el operador**, pero cuesta otra cosa: rompe lo que lee los nombres desde el código fuente:
- `herramientas/grupo_arriba_415e.py`, la fuente de verdad del grupo "0." (`--verificar`);
- el arnés `ws_compat`;
- el arnés de pantalla A23f;
- `GammaHoyTresCasillas.cs:52`, que enumera por `GroupName` y por eso sigue andando.

**Mínimo obligatorio: re-describir.** Hay que cambiar Name y/o Description de todos los que dicen "NQ", "NDX", "QQQ", "MNQ", "LIBRO NQ" o "31.xxx":
- en Name:
  - "3.0 NQ dom▸", "3.0 NQ 0Γ▸";
  - "↳ NQ D1"…"↳ NQ M− OI", "↳ NDX D1"…"↳ NDX techo/piso", "↳ QQQ D1"…"↳ QQQ M− OI";
  - "Capa QQQ (… por razon NQ/QQQ)", "Capa NDX (…)", "Capas: fusion con NQ", "Capas: tamaño de letra (% del de NQ)", "Capas: radio de dibujo (pts, 0 = el de NQ)";
  - "Base de NDX: de donde sale";
  - "Caja negra: capas NDX/QQQ y zero";
- en Description: unas 30 más que citan "'3.0 NQ dom▸'" o números medidos en NQ.

**Renombrar el identificador:** es opcional y lo recomiendo solo en las capas, para que una propiedad "Ndx" no gobierne SPX:
- `Capa3QQQ` → `Capa3SPY`;
- `Capa3NDX` → `Capa3SPX`;
- `Capa3BaseNdx` → `Capa3BaseSpx` (y el enum `BaseNdx3`);
- `Raya3Ndx*` → `Raya3Spx*`;
- `Raya3Qqq*` → `Raya3Spy*`.

Si se renombran, en el mismo cambio van `RayaCapa*`, `MajOiVisible`, `CercoCapa`, `ModoDe` y la fuente de verdad del grupo de arriba. Las `Raya3Nq*` pueden quedar, porque gobiernan el libro primario cualquiera sea la raíz; solo cambia su Name ("↳ ES D1").

---

### Puntos 1, 2, 3 y 6 en el núcleo, por archivo

#### `GammaHoyTres.cs`
**(1) Constantes**
- `GammaHoyTres.cs:99` — `MIN_STRIKES_UTILES = 8`: strikes con IV en call y put para que la cuenta valga. En ES hoy hay 46 → IGUAL.
- `GammaHoyTres.cs:113` — `_periodo = 5 s`: latido → IGUAL.
- `GammaHoyTres.cs:155-156` — `Tope3Contratos = 320`, rango [20, 480]: suscripciones por instancia. En ES se usan 184 → IGUAL.
- `GammaHoyTres.cs:159` — `Ventana3DensaPct = 1.0`: todos los strikes a ±1 % → IGUAL, porque es un %.
- `GammaHoyTres.cs:162` — `Ventana3RalaPct = 2.0`: uno de cada dos hasta ±2 % → IGUAL.
- `GammaHoyTres.cs:166` — `Vencimientos3 = 2`: fechas 0DTE y 1DTE → IGUAL.
- `GammaHoyTres.cs:169` — `Recentrar3Pct = 0.5`: rearme si el precio se va 0,5 % → IGUAL.
- `GammaHoyTres.cs:181` — `Radio3DominantesPts = 0` (auto): radio de dominantes, ver `:401` → ESCALA, ya por raíz.
- `GammaHoyTres.cs:185` — `Empate3Pct = 20`: empate técnico → IGUAL.
- `GammaHoyTres.cs:231` — `Radio3DibujoPts = 0` (auto): radio de dibujo, ver `:402` → ESCALA, ya por raíz.
- `GammaHoyTres.cs:235` — `Zona3ToquePts = 0` (auto): banda de toque, ver `:403` → ESCALA, ya por raíz.
- `GammaHoyTres.cs:240` — `Lejos3Pts = 0` (auto): rearme del toque, ver `:404` → ESCALA, ya por raíz.
- `GammaHoyTres.cs:244,248,255,259,263,267,272` — tope de rótulos 5, estela 0, alpha del túnel 12 %, barras 12 %/50 %, letra 9, margen 48 px: pantalla → IGUAL.
- `GammaHoyTres.cs:401` — `ES 25 / RTY 20 / NQ 100` pts: radio auto de dominantes → ESCALA, ya resuelto para ES. **La 2.0 y la clásica en ES usan 100** (`PythiaGexDos/GammaHoy.cs:408`, `PythiaGexNiveles/GammaHoy.cs:376`).
- `GammaHoyTres.cs:402` — `ES 40 / 30 / 150` pts: radio auto de dibujo → ESCALA, ya resuelto.
- `GammaHoyTres.cs:403` — `ES 0,75 / 0,6 / 2,5` pts: zona de toque. Es la TOL del PRE_REGISTRO, no un valor medido → ESCALA, ya resuelto.
- `GammaHoyTres.cs:404` — `ES 4 / 3 / 15` pts: LEJOS del PRE_REGISTRO → ESCALA, ya resuelto.
- `GammaHoyTres.cs:446` — 120 s: reintento de arranque de la cadena → IGUAL.
- `GammaHoyTres.cs:458-460` — 15 min, 6 h y 60 s: rearme por vencidas, rearme periódico y espacio entre rearmes → IGUAL.
- `GammaHoyTres.cs:500` — `|K − S| ≤ 50` pts: perfil OI en el log CRUCES3 → ESCALA, pero solo es log.
- `GammaHoyTres.cs:548` — `CuantasDominantes = 2` → IGUAL.
- `GammaHoyTres.cs:549` — `RadioDominantesPct = 2.0` %: el radio es el menor entre este % y `:401` → IGUAL.
- `GammaHoyTres.cs:558` — `RadioCentroidePts = 12.0`: centroide de la regla Clásica y de la Tres fuera de la rueda. Solo actúa con `Rayas3Independientes = false`; el default es true y lo apaga (`:557`). En ES, 12 pts son 2,4 strikes; nació en SPX → ESCALA.
- `GammaHoyTres.cs:664` — 3 min: cuenta fresca para anotar la vela → IGUAL.
- `GammaHoyTres.cs:702-703` — 6000/5000 velas en memoria → IGUAL.
- `GammaHoyTres.cs:746,750` — 180 s y 60 s: escritor único de la estela, y escritura de la estela cuando cambia o cada 60 s → IGUAL.
- `GammaHoyTres.cs:851`, `:1103` — tick por defecto 0,25 → PRODUCTO; en ES es 0,25, igual.
- `GammaHoyTres.cs:992` — `rMaj = max(20, MajorOi3RadioMaxPts)`: radio de los majors OI de NQ en Candidatos → ESCALA, pero 3000 es "siempre".
- `GammaHoyTres.cs:998` — `max(0,25, Capa3FusionPts)`: piso de un tick → PRODUCTO; ES 0,25.
- `GammaHoyTres.cs:1105`, `:1135` — 30 min y 5 min: dato viejo, y cartel CAIDO/CERRADO → IGUAL.
- `GammaHoyTres.cs:1231`, `:1248` — 20000 velas y balde de 2 min: respaldo de velas para el ICinta → IGUAL.

**(2) Producto/calendario**
- `GammaHoyTres.cs:387-391`, `:401-404`, `:413`, `:435-439`, `:816-819` — raíz y defaults por raíz → ya manejan ES.
- `GammaHoyTres.cs:454-465` — rearme por vencimiento (15 min) y periódico (6 h) → IGUAL.
- `GammaHoyTres.cs:474-475` — el precio del gráfico se usa como futuro, porque MNQ y NQ cotizan los mismos puntos → MES y ES también → IGUAL.
- `GammaHoyTres.cs:507` — `ActualizarCapas(raiz, …)`, ver b.
- `GammaHoyTres.cs:526` — `Viva3.Guardar(…, raiz, …)`, ver `Viva3.cs:63`.
- `GammaHoyTres.cs:607` — la sesión arranca a las 18:00 NY → IGUAL.
- `GammaHoyTres.cs:1138-1139` — los feriados de CME no se conocen → IGUAL.
- **`GammaHoyTres.cs:1187`** — `if (raiz != "NQ") return;` (adaptadores de la familia) → **cambiar a "ES" a mano**: el núcleo no se renombra.

**(3) Rutas, archivos, logs y nombres**
- `GammaHoyTres.cs:92-93` — `DisplayName "PythiaGex 4.0 - Gamma Familia"`, `Category "PythiaGex 4.0"` → **el renombre no los toca**: a mano.
- `GammaHoyTres.cs:94`, `:328` — `class FamiliaCuatro` y su constructor → queda bien con el renombre (`GammaCinco`).
- `GammaHoyTres.cs:96` — `LOG = "gammahoy"` → pythiagex5-gammahoy.log. Bien.
- `GammaHoyTres.cs:97-98` — `VERSION "4.1.6g"`, `MOTOR_TRES "3.6.9"` → a mano.
- `GammaHoyTres.cs:275,279,283` — las descripciones citan `%APPDATA%\ATAS\PythiaGex4\...` y `pythiagex4-centinela` → el renombre las deja bien.
- `GammaHoyTres.cs:346,380,818,1110` — textos "PythiaGex 4.0 " en log, cartel y cabecera → a mano.
- `GammaHoyTres.cs:756` — `estela\estela-<raiz>-<díaUTC>.jsonl` → automático por raíz: estela-ES.
- `GammaHoyTres.cs:762` — `Familia.AdaptadoresFamilia.AlEstela?.Invoke(raiz, linea)`: raíz "NQ"/"NDX"/"QQQ" hoy; en ES será "ES"/"SPX"/"SPY", siempre que b.1 quede hecho y la familia renombre sus claves.

**(6) Ajustes [Display]** — propiedad, Name, lo que menciona en Description, default, línea. Grupo "0. PRENDER / APAGAR" salvo que se diga otro.
- `Raiz3Manual` (`:149-151`), grupo 9.1: "NQ o ES. Vacío: MNQ → NQ, MES → ES…", default "". Sirve en ES.
- `Radio3DominantesPts` (`:178-181`), grupo 9.2: Name "(0 = auto: 100 NQ / 25 ES)", default 0. Re-describir es opcional.
- `Perfil3Visual` (`:194-195`): Desc "Necesita la llave '3.0 NQ dom▸'… NQ: Limpio = no… ES: siempre si", default Limpio. Re-describir.
- `Ver3Estela` (`:197-198`): Desc "'3.0 NQ dom▸', '3.0 NQ 0Γ▸'… D1/D2 de NQ, del 0Γ de NQ", default SegunPerfil. Re-describir.
- `Ver3Zero` (`:200-201`): Desc "'3.0 NQ 0Γ▸'… zero de NQ", default SegunPerfil. Re-describir.
- `Ver3EstelaZero` (`:203-204`): Desc "'3.0 NQ 0Γ▸'… '↳ NQ 0Γ'… zero de NQ", default SegunPerfil. Re-describir.
- `Ver3Toques` (`:213-214`): Desc "'3.0 NQ dom▸'", default SegunPerfil. Re-describir.
- `Ver3Majors` (`:216-217`): Desc "'3.0 NQ dom▸'… majors por volumen de NQ… no en NQ con Limpio… en ES siempre", default SegunPerfil. Re-describir.
- `Ver3Rotulos` (`:219-220`): Desc "'3.0 NQ dom▸', '3.0 NQ 0Γ▸'…", default SegunPerfil. Re-describir.
- `Barras3Lado` (`:225-226`): Desc "'3.0 NQ dom▸'", default SegunPerfil. Re-describir.
- `Radio3DibujoPts` (`:228-231`), grupo 9.3: Name "(0 = auto: 150 NQ / 40 ES)", default 0. Opcional.
- `Zona3ToquePts` (`:233-235`), grupo 9.3: Name "(0 = auto: 2,5 NQ / 0,75 ES)", Desc "NQ vivo ~20 pts de noche", default 0. Opcional.
- `Lejos3Pts` (`:237-240`), grupo 9.3: Name "(0 = auto: 15 NQ / 4 ES)", default 0. Opcional.
- `Estela3SesionAnterior` (`:250-251`): Desc "'3.0 NQ dom▸', '3.0 NQ 0Γ▸'…", default true. Re-describir.
- `Guardar3Estela` (`:274-276`), grupo 9.4: Desc con ruta PythiaGex4, default true. La deja bien el renombre.
- `Guardar4Centinela` (`:278-280`), grupo 9.4: Desc con pythiagex4-, default false. La deja bien el renombre.
- `Guardar3Viva` (`:282-284`), grupo 9.4: Desc con ruta PythiaGex4, default true. La deja bien el renombre.
- **`Tres41Dominantes`** (`:299-300`): Name **"3.0 NQ dom▸"**; Desc "Lo de NQ… majors por OI de NDX y QQQ", default false. Re-describir; renombrar el Name.
- `Tres41Tunel` (`:302-303`): Desc "D1 y D2 de NQ", default false. Re-describir.
- **`Tres41Zero`** (`:305-306`): Name **"3.0 NQ 0Γ▸"**; Desc "zero gamma de NQ", default false. Re-describir.
- `Tres41Capas` (`:308-309`): Desc "capas NDX y QQQ… TRES_NDX y TRES_QQQ… 'Capa NDX' y 'Capa QQQ'… '3.0 NQ dom▸'", default false. Re-describir.
- `Tres41Recuadro` (`:311-312`): Desc "'LIBRO NQ ▸'", default false. Re-describir.
- `Tres41Cabecera` (`:314-315`): Desc "estado del libro NQ", default false. Re-describir.
- `Tres41Formulas` (`:317-318`): Desc "'↳ NQ M+ OI'… '3.0 NQ dom▸'", default false. Re-describir.

**Trampas del renombre**
- `:1168`, `:1175`, `:1205` — `Familia.LibroNqFamilia`, `Familia.ILibroNq`, `FamiliaLibroNq`.
  - Si la familia renombra `LibroNqFamilia` → `LibroEsFamilia` (`AdaptadoresFamilia.cs:1009`), el núcleo no compila.
  - Si renombra `ILibroNq`, también se rompe `_contratos/Contratos.cs:57`, donde está definido y que el plan no renombra.
  - Hay que decidir: o se excluyen esos identificadores del renombre, o se renombran en todos lados (núcleo + _contratos).
- `:774-785` — `AlmacenTres.MontosDominantes3/CampoGm(raiz)`: la clave de raíz tiene que coincidir con las renombradas en `AlmacenTres.cs:182-183`.
- `:760` — el comentario documenta el filtro "d[0..1] > 15000" de la familia: hay que cambiarlo junto con `AlmacenTres.cs:136`.

#### `GammaHoyTresCapas.cs`
**(1) Constantes**
- `GammaHoyTresCapas.cs:64` — `Capa3FusionPts = 5`, rango [0,25; 50]: distancia para que un nivel de capa "coincida" con D1/D2 → ESCALA.
- `GammaHoyTresCapas.cs:72` — `Capa3LetraPct = 75` → IGUAL.
- `GammaHoyTresCapas.cs:77` — `Capa3Cuantas = 3`, rango [2, 3]: D1-D3 por capa → IGUAL.
- `GammaHoyTresCapas.cs:89` — `Capa3RadioDibujoPts = 60`, rango [0, 500]: guiones y línea de capa hasta 60 pts → ESCALA.
- `GammaHoyTresCapas.cs:126` — `VIDA_CAPA_S = 25·60`: el nivel de capa existe hasta 25 min sin cadena fresca → IGUAL. Tiene que coincidir con `AlmacenTres` VidaS 1500.
- `GammaHoyTresCapas.cs:164` — `RETRASO_CBOE_S = 900`: hora del spot = sello − 900 s → IGUAL. Medido en SPX 902 s y en SPY 901 s.
- `GammaHoyTresCapas.cs:165` — `RETRASO_CBOE_GENERADO_S = 960`: respaldo desde 'generado' → IGUAL.
- `GammaHoyTresCapas.cs:176` — 30 min: distancia máxima a la vela alineada → IGUAL.
- `GammaHoyTresCapas.cs:201,202,205,206` — historial de 60, 10 min de "vivo", 6 min de "congelado", 20 min de "fresca" → IGUAL. Medido: SPX se congela y SPY sigue en el after-hours, igual que NDX y QQQ.
- `GammaHoyTresCapas.cs:216,220,222,223-224` — 24 muestras, corte max(3 MAD, 0,02 %), 3 o más buenas, persiste con 5 o más → IGUAL.
- `GammaHoyTresCapas.cs:231`, `:310` — 24 h de validez de la mediana o del archivo → IGUAL.
- **`GammaHoyTresCapas.cs:255`** — `|BaseForwards − mapa| ≤ 40` pts: control entre la base de forwards y la rueda (NDX). La base NDX ronda 150-240 → **ESCALA. SPX ~35, así que 40 no controla nada.**
- `GammaHoyTresCapas.cs:278` — 10 min: frescura de la foto de la familia → IGUAL.
- **`GammaHoyTresCapas.cs:283`** — razón `[30, 60]`, base `(0, 2000]`: cotas de la conversión de la familia → **PRODUCTO/ESCALA: la razón SPY es ~10,08 (medido), así que la cota la rechaza siempre. Base SPX 34-37 (medido).**
- `GammaHoyTresCapas.cs:329` — 60 s entre cargas de cada capa → IGUAL.
- `GammaHoyTresCapas.cs:337-338` — 20 min: frescura de la cadena en memoria y en archivo → IGUAL.
- `GammaHoyTresCapas.cs:393-395` — 8 campos por fila → IGUAL. Medido: el mismo formato para ES/SPY en cboe-local.
- `GammaHoyTresCapas.cs:404` — `Apalancamiento = 1.0` → PRODUCTO. SPY = 1, bien.
- `GammaHoyTresCapas.cs:418` — `CuantasDominantes = 2..3` y radio copiado de la primaria (ES 25 pts) → ESCALA, ya por raíz vía `:401`.
- `GammaHoyTresCapas.cs:423` — `RadioCentroidePts = 12.0`: centroide de las capas. Con la regla Clásica (default) queda prendido → ESCALA. Nació para SPX.
- `GammaHoyTresCapas.cs:568` — alpha 60 fuera del radio de la capa → IGUAL.

**(2) Producto/calendario**
- **`GammaHoyTresCapas.cs:323`** — `raiz != "NQ"` → a mano "ES".
- `GammaHoyTresCapas.cs:362-367` — `ultimo_trade` en hora NY ("Eastern Standard Time") → UTC → IGUAL.
- `GammaHoyTresCapas.cs:377-378` — hora del spot = sello − 900 s → IGUAL. Verificado para _SPX y SPY.
- `GammaHoyTresCapas.cs:403` — `EsFuturo = false`: Black-Scholes con r = 0,0375 y sin dividendo en la gamma, S = fut − base o fut/razón → IGUAL al tratamiento de NDX y QQQ.
- `GammaHoyTresCapas.cs:181-194`, `:195-268` — regla del reloj del spot: índice congelado a las 16:00/16:15 y ETF que sigue hasta las 20:00 → IGUAL. Medido en SPX y SPY.

**(3) Rutas, archivos, logs y nombres**
- **`GammaHoyTresCapas.cs:122`** — `{ Nombre "QQQ", Archivo "QQQ", PorRazon true }` → a mano `"SPY"/"SPY"`.
- **`GammaHoyTresCapas.cs:123`** — `{ Nombre "NDX", Archivo "NQ", PorRazon false }` → a mano `"SPX"/"ES"`. Archivo = clave del descargador; cboe-local también usa "ES" para SPX.
- `GammaHoyTresCapas.cs:132` — `CarpetaDatos\cboe`. El renombre lo deja bien.
- `GammaHoyTresCapas.cs:133`, `:336` — `FuenteCapaEnMemoria(k.Archivo)`: coincide con `IntegracionFamilia.cs:250`.
- `GammaHoyTresCapas.cs:157` — `base-rueda-<Archivo>.json` / `razon-rueda-<Archivo>.json` → base-rueda-ES.json y razon-rueda-SPY.json con b.1.
- `GammaHoyTresCapas.cs:334` — `cboe\ultima-<Archivo>.json` → ultima-ES.json y ultima-SPY.json.
- `GammaHoyTresCapas.cs:403` — `Fuente = k.Nombre + " CBOE"` → automático.
- `GammaHoyTresCapas.cs:434` — `GuardarEstela(k.Nombre…)` → estela-SPX y estela-SPY; `AlEstela("SPX"|"SPY")`.
- `GammaHoyTresCapas.cs:135`, `:153` — `ModoDe` ("QQQ") y `CercoCapa` ("NDX") → a mano.
- `GammaHoyTresCapas.cs:281` — `fe.Libro == k.Nombre`: tiene que coincidir con el nombre que la familia renombrada da a sus libros.
- `GammaHoyTresCapas.cs:41-49`, `:97-98`, `:127-131`, `:297-300` — comentarios con ultima-<QQQ|NQ>, base-rueda-NQ y razon-rueda-QQQ → actualizar.

**(6) Ajustes [Display]**
- `Capa3QQQ` (`:53-55`), grupo 9.3: Name "Capa QQQ (CBOE, 15 min tarde, por razon NQ/QQQ)"; Desc "D1/D2 de NQ… QQQ por CBOE", default Propia. Re-describir; renombrar recomendado.
- `Capa3NDX` (`:57-59`), grupo 9.3: Name "Capa NDX (CBOE, 15 min tarde, por base aditiva)"; Desc "cadena 'NQ' de la nube… llevado a MNQ… -2,2 pp", default Propia. Re-describir; renombrar recomendado.
- `Capa3FusionPts` (`:61-64`), grupo 9.3: Name "Capas: fusion con NQ (pts)"; Desc "NQ·QQQ D1", default 5. Re-describir.
- `Capa3ZeroRombos` (`:66-67`): Name "↳ capas 0Γ▸"; Desc "NDX y QQQ… fila de 31485 de la 2.0… base de NDX", default true. Re-describir.
- `Capa3LetraPct` (`:69-72`), grupo 9.3: Name "Capas: tamaño de letra (% del de NQ)"; Desc "QQQ y NDX", default 75. Re-describir.
- `Capa3Cuantas` (`:74-77`), grupo 9.3: no menciona productos; default 3.
- `Capa3LineaActual` (`:79-80`): "'3.0 capas▸'", default true. No hace falta tocarla.
- `Capa3Regla` (`:82-84`), grupo 9.3: Desc "NDX D1 en 31.511… la regla de NQ", default Clasica. Re-describir. El enum `CapaRegla3` (`:23-28`) dice "la regla elegida para NQ" en `:27`.
- `Capa3RadioDibujoPts` (`:86-89`), grupo 9.3: Name "(pts, 0 = el de NQ)"; Desc "muro de calls 762 de QQQ, +83 pts", default 60. Re-describir.
- `Capa3BaseNdx` (`:91-93`), grupo 9.3: Name "Base de NDX: de donde sale"; Desc "238,36… 248,15… 236-242", default Forwards. Re-describir; renombrar recomendado, junto con el enum `BaseNdx3` (`GammaHoyTresTunel.cs:23-28`).

**Trampas del renombre**
- "QQQ", "NDX" y "NQ" en `:122-123`, `:135`, `:153` son literales del núcleo: **el renombre no los toca**. Pero sus pares en la familia sí se renombran: `IntegracionFamilia.cs:250`, `BajadorCboe.cs:141-142`, `AlmacenTres.cs:29,182-183` y los libros de `FotoFamilia`. Sin el cambio a mano, las capas no encuentran su cadena ni su conversión, y TRES_SPX/TRES_SPY quedan vacíos.
- `"_NDX"` (`BajadorCboe.cs:141`, familia) sobrevive a un `\bNDX\b`. Si queda, la capa "SPX" se arma con la cadena de NDX (strikes ~29.000) → rayas absurdas o vacías.

#### `GammaHoyTresRayas.cs`
**(1) Constantes**
- `GammaHoyTresRayas.cs:49` — `Rotulos3TamPct = 85` → IGUAL.
- `GammaHoyTresRayas.cs:60-63` — `CargaMinimaOi = 50` contratos, rango [0, 5000]: OI total mínimo para la serie "carga", que escribe en `Familia.ReglasFam` → PRODUCTO/ESCALA. El OI de ES por strike no es el de NQ; SIN VERIFICAR.
- `GammaHoyTresRayas.cs:65-68` — `CargaTopeRayas = 12`, rango [1, 40] → IGUAL.
- `GammaHoyTresRayas.cs:70-73` — `CargaMinimaRayas = 10`, rango [0, 20] → IGUAL.
- `GammaHoyTresRayas.cs:75-78` — `CargaRadioPts = 0` (0 = el de las dominantes), techo 300 → ESCALA. El techo 300 se razona con "la cadena de NQ llega con un strike de cada dos" pasado el ±1 %; en ES el ±1 % son ~77 pts.
- `GammaHoyTresRayas.cs:82` — `Rotulos3CorridoPx = 7` → IGUAL.

**(2) Producto/calendario** — nada.

**(3) Rutas y nombres**
- `GammaHoyTresRayas.cs:63,68,73,78` — `PythiaGexCuatro.Familia.ReglasFam.*`, totalmente calificado → el renombre de namespace lo deja bien.
- **`GammaHoyTresRayas.cs:98-113`** — `RayaCapa`/`RayaCapaZero`/`EnfasisCapaZero` comparan "NDX"/"QQQ" → a mano "SPX"/"SPY".

**(6) Ajustes [Display]**
- `Raya3NqD1` (`:14`): Name "↳ NQ D1", default true. Name → "↳ ES D1".
- `Raya3NqD2` (`:15`): "↳ NQ D2", default true.
- `Raya3NqZero` (`:16`): "↳ NQ 0Γ", default true.
- `Raya3NqMas` (`:17`): "↳ NQ M+ vol"; Desc "en NQ con Limpio, el default, no se ve", default true.
- `Raya3NqMenos` (`:18`): "↳ NQ M− vol", default true.
- `Raya3NdxD1` (`:19`): "↳ NDX D1", default true.
- `Raya3NdxD2` (`:20`): "↳ NDX D2"; Desc "07-10: 0 toques; mecha 9 pts", default **false**.
- `Raya3NdxD3` (`:21`): "↳ NDX D3", default false.
- `Raya3NdxZeroB` (`:22`): "↳ NDX 0Γ"; Desc "base de NDX… 31.375,43 / 31.350,73", default true.
- `Raya3NdxZeroEnfasis` (`:23`): "↳ NDX 0Γ enf.", default true.
- `Raya3NdxZeroCerco` (`:24-25`): "↳ NDX techo/piso"; Desc "zero de NDX… 31.375,43 y 31.350,73", default true.
- `Raya3QqqD1` (`:26`): "↳ QQQ D1", default true.
- `Raya3QqqD2` (`:27`): "↳ QQQ D2"; Desc "mecha 6 pts", default false.
- `Raya3QqqD3` (`:28`): "↳ QQQ D3", default false.
- `Raya3QqqZero` (`:29`): "↳ QQQ 0Γ", default true.
- `Raya3QqqZeroEnfasis` (`:30`): "↳ QQQ 0Γ enf.", default true.
- Las demás de este archivo citan "'3.0 NQ dom▸'" o NQ en la Description; todas se re-describen:
  - `Cruces3SinCortes` (`:31-32`), default true;
  - `Rayas3Largas` (`:41-42`), default false;
  - `Rotulos3Cortos` (`:44-45`), default true;
  - `Rotulos3Conectores` (`:51-52`), default false;
  - `CargaMinimaOi` (`:61`): "El libro NQ suscribe…";
  - `CargaTopeRayas` (`:66`): "futuro en 31.350";
  - `CargaMinimaRayas` (`:71`): "strike NDX 30675";
  - `CargaRadioPts` (`:76`): "MNQ en 31.000… cadena de NQ".

**Trampas del renombre:** ninguna propia. Las de nombres de capa están en `GammaHoyTresCapas.cs`.

#### `GammaHoyTresMajorOi.cs`
**(1) Constantes**
- `GammaHoyTresMajorOi.cs:43` — `MajorOi3RadioMaxPts = 3000`, rango [20, 5000]: "siempre" → ESCALA. En ES, 3000 también es "siempre".
- `GammaHoyTresMajorOi.cs:111` — `max(20, …)` → ESCALA leve.

**(3) Nombres**
- `GammaHoyTresMajorOi.cs:48-53` — `MajOiVisible` compara "NDX"/"QQQ"; lo demás cae en `Raya3Nq*` → a mano "SPX"/"SPY".
- `GammaHoyTresMajorOi.cs:119` — clave `"NQ"` del libro primario (interna) → puede quedar.
- `GammaHoyTresMajorOi.cs:166` — rótulo `"<Nombre> M+ OI"` → automático.

**(6) Ajustes [Display]**
- `Raya3NqMasOi` (`:25-26`): "↳ NQ M+ OI"; Desc "31.350 el 08-10", default true.
- `Raya3NqMenosOi` (`:28-29`): "↳ NQ M− OI", default true.
- `Raya3NdxMasOi` (`:31-32`): "↳ NDX M+ OI", default true.
- `Raya3NdxMenosOi` (`:33-34`): "↳ NDX M− OI", default true.
- `Raya3QqqMasOi` (`:35-36`): "↳ QQQ M+ OI", default true.
- `Raya3QqqMenosOi` (`:37-38`): "↳ QQQ M− OI", default true.
- Todas: re-describir. Renombrar las de NDX/QQQ es opcional.

#### `GammaHoyTresCaja.cs` (apagada por defecto, `Caja4Activa = false`, `:28`)
**(1) Constantes**
- `GammaHoyTresCaja.cs:41` — `Caja3Profundidad = 10` niveles L2 → IGUAL.
- `GammaHoyTresCaja.cs:46` — `Caja3LoteGrande = 20` contratos: print grande en la cinta del gráfico (MNQ) → PRODUCTO. Con MES los tamaños son otros; SIN VERIFICAR.
- `GammaHoyTresCaja.cs:90`, `:116`, `:170`, `:191`, `:316`, `:360`, `:400` — 180 s, 7 min, 400 velas/30 min, 30 min, 60 s, 5/15/30 min, 40 min → IGUAL.
- **`GammaHoyTresCaja.cs:127`** — placebos `±12,5` y `±25` pts → **ESCALA, sin escalar por raíz.** Para ES el propio comentario de `:365` dice /4.
- `GammaHoyTresCaja.cs:164` — clave redondeada al punto → ESCALA leve.
- `GammaHoyTresCaja.cs:192` — `≤ 3` pts y 300 s: no repetir un toque de la misma familia → ESCALA.
- `GammaHoyTresCaja.cs:365-367` — `esc = ES 0,25 / NQ 1`: umbrales 12/20 a favor y 6/8 en contra (`:379-382`), 6 visual y 2 de cierre (`:386-387`) → ya escala por raíz.

**(2) Producto/calendario**
- `GammaHoyTresCaja.cs:367` — rama ES.
- `GammaHoyTresCaja.cs:279` — piso T de 1 min → IGUAL.

**(3) Rutas y nombres**
- `GammaHoyTresCaja.cs:406` — `caja\caja-<raiz>-<día>.jsonl` → automático (caja-ES).
- `GammaHoyTresCaja.cs:125,128,163,232` — `Fam = "NQ"` y origen "NQ D1 ±…" fijos → quedarían "NQ" en caja-ES: conviene la raíz.

**(6) Ajustes [Display]**
- `Caja4Activa` (`:26-28`): Desc con ruta PythiaGex4\caja, default false. La deja bien el renombre.
- `Caja3Placebos` (`:30-32`): Desc "dominantes de NQ corridas ±12,5 y ±25 pts", default true. Re-describir y escalar.
- `Caja3Capas` (`:34-36`): Name **"Caja negra: capas NDX/QQQ y zero"**; Desc "zero gamma de NQ", default true. Re-describir.

#### `GammaHoyTresCinta.cs` (exportar y rellenar apagados por defecto: `:50`, `:67`)
**(1) Constantes**
- `GammaHoyTresCinta.cs:59` — `Cinta3FlushMs = 25` → IGUAL.
- `GammaHoyTresCinta.cs:80,81,84,107,130` — cola de 1e6, hueco de 60 s, anillo de 16, reloj de 2 s, 256 ids → IGUAL.

**(2) Producto/calendario**
- `GammaHoyTresCinta.cs:175-176` — día de sesión desde las 22:00 UTC → IGUAL. CME es igual para ES; en invierno cae en la pausa.

**(3) Rutas, hilos y nombres**
- `GammaHoyTresCinta.cs:63`, `:177` — default `…\ATAS\PythiaGex4\cinta`, con "PythiaGex4" como literal suelto → el renombre lo deja bien. Además es un tipo nuevo, sin valor guardado en el .ws.
- `GammaHoyTresCinta.cs:363` — hilo `"PythiaGex4 cinta"` → el renombre lo deja bien.
- `GammaHoyTresCinta.cs:541`, `:734`, `:807` — `cinta-<raiz>-<día>.csv` (y relleno) → automático (cinta-ES).
- Los ticks pasan al ICinta de la familia solo si `_famCinta` existe (`:199`, `:828`). Ver `GammaHoyTres.cs:1187`.

**(6) Ajustes [Display]**
- `Cinta4Exportar` (`:48-50`), `Cinta3Despertar` (`:52-54`), `Cinta3FlushMs` (`:56-59`), `Cinta4Carpeta` (`:61-63`) y `Cinta4Rellenar` (`:65-67`): no mencionan NQ en Name ni en Description. Algunas citan PythiaGex4: las deja bien el renombre.

#### `GammaHoyTresRecuadro.cs`
**(1) Constantes**
- `GammaHoyTresRecuadro.cs:65,69,74,82` — margen 56 px, 3 niveles, flechas a 5 min, tamaño 85 % → IGUAL.
- **`GammaHoyTresRecuadro.cs:78`** — `Recuadro3RadioMuros = 100` pts, rango y piso 20 en `:149`: muro de calls/puts "a ±100 como la página" → **ESCALA.**
- `GammaHoyTresRecuadro.cs:157` — 45 fotos → IGUAL.
- `GammaHoyTresRecuadro.cs:170` — `max(2 % del máximo, 1e3)`: umbral de flecha en unidades 3.0 (MULT 100) → IGUAL. El piso 1e3 es despreciable.

**(3) Nombres**
- `GammaHoyTresRecuadro.cs:175,225` — `"LIBRO " + raiz` → automático ("LIBRO ES").

**(6) Ajustes [Display]**
- `RecuadroLibro3Ver` (`:42-43`): Name "↳ ver libro"; Desc **"pestañita 'LIBRO NQ ▸'"**, default true. Re-describir.
- `Rotulos3Estilo` (`:38-40`), default Minimal; `Recuadro3Esquina` (`:60-61`); `Recuadro3MargenPx` (`:63-65`); `Recuadro3Niveles` (`:67-69`); `Recuadro3CambioMin` (`:71-74`); `Recuadro3RadioMuros` (`:76-78`), default 100; `Recuadro3TamPct` (`:80-82`): sin NQ en el texto.

#### `GammaHoyTresApoyo.cs`
**(1) Constantes**
- `GammaHoyTresApoyo.cs:28` — `Apoyo3Cuantos = 3` → IGUAL.
- **`GammaHoyTresApoyo.cs:38`** — `Zero3CrucesRadioPts = 20` pts, rango [5, 40]: radio de dibujo de todos los cruces → **ESCALA.**
- **`GammaHoyTresApoyo.cs:51`** — `CrucesOiRadio3Pts = 60` pts, rango [10, 80]: radio de los cruces por OI → **ESCALA.**
- `GammaHoyTresApoyo.cs:77` — entre 1 y 6 strikes de apoyo → IGUAL.
- `GammaHoyTresApoyo.cs:160`, `:168` — "sin cortes": hasta 3-4 velas → IGUAL.
- `GammaHoyTresApoyo.cs:170`, `:173` — tolerancia de 1,0 pt para "el mismo cruce" → ESCALA leve.
- `GammaHoyTresApoyo.cs:175` — 0,5 pt → ESCALA leve.
- El radio del apoyo lo pone `GammaHoyTres.cs:490` (`RadioDibujo(raiz)`) → ya por raíz.

**(6) Ajustes [Display]**
- `Ver3Apoyo` (`:23-24`), default false; `Ver3EstelaZeroOi` (`:30-31`), default false; `Zero3TodosLosCruces` (`:33-34`), default false: Desc "Necesita la llave '3.0 NQ dom▸'" → re-describir.
- `Apoyo3Cuantos` (`:26-28`), `Zero3CrucesRadioPts` (`:36-38`) y `CrucesOiRadio3Pts` (`:48-51`): sin NQ en el texto.

#### `GammaHoyTresTunel.cs`
**(1) Constantes**
- `GammaHoyTresTunel.cs:39` — `Tunel3UmbralPct = 25`, rango [5, 90] → IGUAL.
- `GammaHoyTresTunel.cs:54,59` — 0,01: igualdad de precio → IGUAL.
- `GammaHoyTresTunel.cs:75` — `radio = min(2 %, RadioDominantes(raiz))` → ya por raíz.
- `GammaHoyTresTunel.cs:79` — umbral acotado entre 0,05 y 0,9 → IGUAL.
- `GammaHoyTresTunel.cs:122-123` — relleno ±1e6 del túnel por cruces → IGUAL. Por eso "gm" sale null.

**(2) Producto/calendario**
- `GammaHoyTresTunel.cs:71` — túnel de noche con la regla Tres fuera de la rueda (`EsRueda`) → IGUAL.

**(6) Ajustes [Display]**
- `Tunel3Fuente` (`:32-34`), Cruces; Desc "31484 / 31456" → re-describir si se quiere.
- `Tunel3UmbralPct` (`:36-39`), `Tunel3Histeresis` (`:41-43`) y `Tunel3DeNoche` (`:45-47`): sin NQ explícito.
- El enum `BaseNdx3` (`:23-28`) dice "base de NDX (indice -> futuro NQ)". Lo usa `Capa3BaseNdx`.

#### `GammaHoyTresAtravesadas.cs`
**(1) Constantes**
- `GammaHoyTresAtravesadas.cs:24,28,32` — 20 velas, 3 cuerpos, alpha 20 % → IGUAL.

**(6) Ajustes [Display]**
- `Atravesadas3AtenuarB` (`:19-20`): Desc "'3.0 NQ dom▸'… caso 31.372", default false. Re-describir.

#### `GammaHoyTresMemoria.cs`
**(1) Constantes**
- `GammaHoyTresMemoria.cs:29` — `VIDA_MEMORIA_S = 300` → IGUAL. Coincide con VidaS de NQ/ES en la familia.
- `GammaHoyTresMemoria.cs:47`, `:52`, `:106`, `:271` — 120 s, 6000 velas, 90 s y 30 min → IGUAL.

**(2) Producto/calendario**
- `GammaHoyTresMemoria.cs:276`, `:293` — usa `Raiz()` → ya por raíz.

**(3) Rutas y nombres**
- `GammaHoyTresMemoria.cs:188` — lee `estela-<raiz>-<día>.jsonl` → automático.
- `GammaHoyTresMemoria.cs:226` — lee `viva3-<raiz>-<día>.jsonl` → automático.
- `GammaHoyTresMemoria.cs:281` — escribe `rebobinado\hist-<raiz>-…` → automático.
- `GammaHoyTresMemoria.cs:86` — `SembrarMemoriaCapas` no mira la raíz → con las capas renombradas lee estela-SPX y estela-SPY de PythiaGex5. Bien.

#### `GammaHoyTresHisteresis.cs`
**(1) Constantes**
- `GammaHoyTresHisteresis.cs:34` — `HistX = 25` %: la retadora tiene que superar a la vigente → IGUAL.
- `GammaHoyTresHisteresis.cs:37` — `RadioCentroidePts = 6.0` pts → ESCALA. Queda en 0 con `Rayas3Independientes` (`:319`).
- `GammaHoyTresHisteresis.cs:279` — `Histeresis3Pct = 25`, rango [0, 500] → IGUAL.
- `GammaHoyTresHisteresis.cs:284` — `Histeresis3Min = 5`, rango [1, 120] → IGUAL.
- `GammaHoyTresHisteresis.cs:289` — `Histeresis3CentroidePts = 6`, rango [0, 50] → ESCALA.
- `GammaHoyTresHisteresis.cs:326` — libro completo: 90 s después del armado y 60 % con puntas → IGUAL.

**(2) Producto/calendario**
- `GammaHoyTresHisteresis.cs:64-69` — tramos 9:30-18:00 y 18:00-9:30 NY → IGUAL.
- `GammaHoyTresHisteresis.cs:73-80` — `EsRueda` 9:30-16:00 NY, de lunes a viernes → IGUAL. SPX/ES igual.

**(6) Ajustes [Display]** — los tres de histéresis (`:276-289`) no mencionan productos.

#### `GammaHoyTresFormula.cs` (F1-F8, apagadas por defecto con `Tres41Formulas`)
**(1) Constantes**
- `GammaHoyTresFormula.cs:44` — `Doms3MantenerPct = 80` → IGUAL.
- **`GammaHoyTresFormula.cs:58`** — `Formulas3RadioPts = 150` pts, con piso 20 en `:165` → **ESCALA.**
- **`GammaHoyTresFormula.cs:68`** — `MurosVol/MurosOi → 100.0` pts fijos; el resto `min(2 %, RadioDominantes(raiz))` → **ESCALA: los muros F5/F6 no escalan por raíz.**
- `GammaHoyTresFormula.cs:29-30` — los enums F5/F6 dicen "±100 pts (como la pagina)".

**(6) Ajustes [Display]**
- `Formulas3Ver` (`:47-48`): Desc "'↳ NQ M+ OI'… '3.0 NQ dom▸'", default true. Re-describir.
- `Formula3F1`…`Formula3F8b` (`:49-55`): F1 true, F5 true, el resto false; Desc "F5… a ±100 pts" → re-describir la de F5/F6 si se escala.
- `Doms3Formula` (`:37-39`) y `Formulas3RadioPts` (`:56-58`): sin NQ en el texto.

#### `GammaHoyTresCasillas.cs`
**(3) Logs y nombres**
- `GammaHoyTresCasillas.cs:33` — `GRUPO_CASILLAS = "0. PRENDER / APAGAR (todo lo que se dibuja)"`: enumera por reflexión las propiedades del grupo → genérico.
- `GammaHoyTresCasillas.cs:34` — `LOG_CASILLAS = "pantalla"` → pythiagex5-pantalla.log. Bien.

**Trampas:** el log cuenta casillas, 108 en la 4.1.6g según el punto 8. Si se borran las series TQQQ, el número cambia, y los arneses que esperan 101 tienen que regenerarse.

#### `GammaHoyTresEstado.cs` (apagado por defecto, `Estado4Pulso = false`, `:40`)
**(1) Constantes**
- `GammaHoyTresEstado.cs:45` — `Estado3PulsoSeg = 1` → IGUAL.

**(3) Rutas**
- `GammaHoyTresEstado.cs:49` — `Estado4Ruta` default `…\ATAS\PythiaGex4\estado\indicador.json` → el renombre lo deja bien. El JSON lleva `"raiz"` (`:229`) → automático.

**(6) Ajustes [Display]** — `Estado4Pulso` (`:38-40`), `Estado3PulsoSeg` (`:42-45`) y `Estado4Ruta` (`:47-49`): sin NQ.

#### `GammaHoyNucleo.cs` (copia textual del núcleo de la 2.0; maneja ES desde la clásica)
**(1) Constantes**
- `GammaHoyNucleo.cs:47` — `Tasa = 0.0375` → IGUAL. Depende de la fecha, no del producto.
- `GammaHoyNucleo.cs:51-52` — `ExpiracionFuturoUtc`/`AltUtc`: **nunca se asignan en la 4.1**, así que el carry es NaN y no hay cota de base (`:323-334`) → IGUAL.
- `GammaHoyNucleo.cs:53` — `Dividendo = 0.012` → PRODUCTO. **Es el de SPX (1,2 %), pero está muerto:** sin `ExpiracionFuturo` no se usa. El dividendo de NDX 0,6 % no está en el núcleo, está en la familia.
- `GammaHoyNucleo.cs:54` — `BaseRueda = NaN`: no se asigna en la 4.1 (las capas pasan `c.Base` "medida") → IGUAL.
- `GammaHoyNucleo.cs:55,56` — 2 dominantes y 2 % → IGUAL.
- `GammaHoyNucleo.cs:59` — `RadioDominantesMaxPts = 100`: default pisado por `GammaHoyTres.cs:550` → ESCALA, ya por raíz.
- `GammaHoyNucleo.cs:60,61` — pico a ±0,35 % y "mucho" 50 % → IGUAL.
- `GammaHoyNucleo.cs:69` — `RadioCentroidePts = 12.0` ("~2 strikes de SPX a cada lado") → ESCALA. Pisado por `GammaHoyTres.cs:558` y `Capas.cs:423`.
- `GammaHoyNucleo.cs:81` — empate 20 % → IGUAL.
- `GammaHoyNucleo.cs:148` — Max Change {1, 5, 10, 15, 30} min → IGUAL.
- **`GammaHoyNucleo.cs:149`** — `MULT_INDICE = 100` → PRODUCTO. Reales: NQ 20, **ES 50**, SPX 100, SPY 100. Solo afecta magnitudes; ninguna selección.
- `GammaHoyNucleo.cs:150` — `PISO_DIAS = 1/1440` → IGUAL.
- `GammaHoyNucleo.cs:174`, `:544` — 40 fotos → IGUAL.
- `GammaHoyNucleo.cs:233-234` — Hoy ≤ max(1 día, masCerca + 0,01); Semana 7 días → IGUAL.
- `GammaHoyNucleo.cs:247` — envejecer hasta 2 días → IGUAL.
- `GammaHoyNucleo.cs:271-272` — cruces contados a ±0,1 %, lista a ±0,2 % → IGUAL. El comentario de `:255` dice "31 pts en NQ"; en ES serían ~7,7.
- `GammaHoyNucleo.cs:283` — grilla del cruce ±3 % × apalancamiento, 60 pasos → IGUAL.
- `GammaHoyNucleo.cs:329` — cota de base `max(60 % del carry, 0,06 % del futuro)` → IGUAL, y muerta sin carry.
- `GammaHoyNucleo.cs:339`, `:340` — base "medida hace" ≤ 360 min; de la rueda ≤ 24 h → IGUAL.
- `GammaHoyNucleo.cs:348` — `Sup = S·1,01` (convexidad) → IGUAL.
- `GammaHoyNucleo.cs:382`, `:489` — 0,2: elección del libro de convexidad y del cuadrante → IGUAL.
- `GammaHoyNucleo.cs:523` — zona del máximo ≥ 80 % → IGUAL.
- **`GammaHoyNucleo.cs:526`** — `banda = 2.5` pts, "media distancia entre strikes" → PRODUCTO. ES 2,5 (strikes de 5, medido). Nació en ES: el comentario de `:513-514` cita 7.759/7.764. En ES sirve tal cual.
- `GammaHoyNucleo.cs:536` — alerta de 10 min → IGUAL.
- `GammaHoyNucleo.cs:560` — Max Change con piso del 5 % del máximo → IGUAL.

**(2) Producto/calendario**
- `GammaHoyNucleo.cs:34-43` — `FueraDeRueda` 9:30-16:00 NY y fines de semana → IGUAL. SPX/ES igual.
- `GammaHoyNucleo.cs:199-206` — opciones sobre futuro: Black-76; sobre índice o ETF: Black-Scholes con r y sin q → IGUAL. Sirve para ES, SPX y SPY.
- `GammaHoyNucleo.cs:336-346` — conversión del libro: `PorRazon` (ETF), `EsFuturo` (sin base) o base "medida" > "medida hace" > rueda > cruda > teórica → genérico.

**(3) Rutas** — nada. Solo `namespace` (`:10`) y el comentario de `:1-4`, que dice "namespace PythiaGexTres" → el renombre lo deja bien.

#### `CadenaApi.cs`
**(1) Constantes**
- `CadenaApi.cs:127,129,131,133,135` — 320, 1 %, 2 %, 2 fechas, 0,5 % → IGUAL. Los pisa `GammaHoyTres.cs:540-544`.
- `CadenaApi.cs:137,138,139` — lookups cada 30 s, caché de 10 min, espaciador global de 10 s → IGUAL.
- `CadenaApi.cs:182` — `DESFASE_PRIMER_ARMADO_S = 20` → IGUAL. Con la 4.1 y la 5.0 en el mismo ATAS coinciden; ver f.
- `CadenaApi.cs:317`, `:345` — 15 s para el precio del futuro y del anterior → IGUAL.
- `CadenaApi.cs:455` — corta después de 5 rechazos sin aceptadas → IGUAL.
- `CadenaApi.cs:651`, `:672` — timeouts de 35 s → IGUAL.
- `CadenaApi.cs:695` — paso de strike por defecto 5,0 → PRODUCTO. ES 5, medido; igual.
- `CadenaApi.cs:769` — spread del roll redondeado a 0,25 → PRODUCTO. Tick de ES 0,25; igual.
- `CadenaApi.cs:770` — histéresis de 1 pt del spread → ESCALA leve.

**(2) Producto/calendario**
- `CadenaApi.cs:286`, `:902-909` — raíces soportadas → ES ya está.
- `CadenaApi.cs:337` — `Exchange = "CME"` → PRODUCTO. ES está en CME; igual.
- `CadenaApi.cs:373` — `VenceAm`: Regular el día que vence el futuro → 9:30 NY → PRODUCTO, igual en ES. Que la trimestral de ES liquide AM está SIN VERIFICAR contra la ficha de CME; es lo que hace el código para NQ y ES desde la 1.11c.
- `CadenaApi.cs:379` — 2 fechas → IGUAL.
- `CadenaApi.cs:383` — aviso "sin el vencimiento de HOY" antes de las 16:30 NY → IGUAL.
- `CadenaApi.cs:763` — el anterior vence a las 9:30 de su fecha → PRODUCTO, igual.
- `CadenaApi.cs:842-851` — strikes del viejo corridos; vencido fuera desde el minuto cero → IGUAL.
- `CadenaApi.cs:856` — IV de Black-76 del mid → IGUAL.
- `CadenaApi.cs:895-899` — `EsRegular`, `LlaveSerie`, `EsDelAnterior` → IGUAL. Medido en ES: Weekly y EndOfMonth (este último PM, sin rama especial).
- `CadenaApi.cs:931`, `:947` — ciclo trimestral `"HMUZ"` → PRODUCTO, igual en ES.

**(3) Rutas y nombres**
- No escribe archivos. Los logs van con el prefijo "[cadena api] " al log del indicador.

**Trampas del renombre**
- **No tocar `"NQ"`/`"MNQ"` de `:905-907`, `:927-928`.** Si el renombre de tokens de la familia se corriera por error sobre este archivo, `RaizGrande("MNQ…")` daría "ES" y `("MES…")` también.
- El plan ya lo excluye, pero `CadenaApi.cs` no está en `_modulos`: ojo con un glob amplio.

#### `Viva3.cs`
**(1) Constantes**
- `Viva3.cs:39` — escritor único con dueño por 180 s → IGUAL.

**(2) Producto/calendario**
- **`Viva3.cs:63`** — `if (raiz == "NQ")`: la foto va a la familia solo en NQ → a mano "ES".

**(3) Rutas y nombres**
- `Viva3.cs:32`, `:59` — `CarpetaDatos\viva\viva3-<raiz>-<díaUTC>.jsonl` → automático (viva3-ES, medido hoy en PythiaGex4).
- `Viva3.cs:15` — el comentario dice "viva3-<NQ|ES>" → ya contempla ES.

**Trampas del renombre**
- `Viva3.cs:63` — `Familia.LibroNqFamilia.Para(raiz)`: el mismo conflicto de identificador que en `GammaHoyTres.cs:1168/1205`.

#### `Feed.cs`
**(1) Constantes**
- `Feed.cs:52,54` — `Escala = 1.0`, `Apalancamiento = 1.0` → IGUAL.
- `Feed.cs:55-66` — `AlFuturo`/`AlLibro`: por razón k × Escala; con apalancamiento, la fórmula de TQQQ (no se usa en el núcleo) → IGUAL.

**(2) Producto/calendario**
- `Feed.cs:47-48` — `EsFuturo` "NQ/ES por la API" → ya contempla ES.

#### `Black76.cs`
**(1) Constantes**
- `Black76.cs:21` — `R = 0.0375`: tasa del descuento al despejar la IV → IGUAL. Depende de la fecha.
- `Black76.cs:72`, `:80` — IV acotada entre 1e-4 y 5,0 y válida entre 1e-3 y 4,99 → IGUAL.

#### `RelojNy.cs`
**(1) Constantes**
- `RelojNy.cs:39,41` — respaldo UTC−4 si no está la zona horaria → IGUAL. En invierno queda 1 h corrido, pero solo si falta la zona.
- `RelojNy.cs:56` — piso T de 1 min → IGUAL.
- `RelojNy.cs:61` — gracia de 30 min → IGUAL.

**(2) Producto/calendario**
- `RelojNy.cs:47-49` — Weekly/diaria vence 16:00 NY; trimestral de la mañana 9:30 NY → PRODUCTO, igual en ES.
- `RelojNy.cs:63-74` — CME de domingo 18:00 a viernes 17:00, con pausa de 17 a 18; sin feriados → PRODUCTO, igual en ES.

#### `Registro.cs`
**(1) Constantes**
- `Registro.cs:32` — tope de log de 20 MiB más el .1 → IGUAL. Medido: `pythiagex4-gammahoy.log` pesa 20,7 MB, cerca del tope, y hoy lo escriben las dos instancias (MNQ y MES).

**(3) Rutas**
- `Registro.cs:26` — `CarpetaDatos = …\ATAS\PythiaGex4` → el renombre lo deja bien.
- `Registro.cs:28` — `RutaLog = …\ATAS\pythiagex4-<nombre>.log` → el renombre lo deja bien.
- `Registro.cs:15`, `:25` — comentarios → el renombre los deja bien.

#### `Centinela.cs` (apagado por defecto)
**(3) Rutas**
- `Centinela.cs:53` — `…\ATAS\pythiagex4-centinela-<instr>-<marco>.jsonl` → el renombre lo deja bien. Está en la raíz de `%APPDATA%\ATAS`, no en CarpetaDatos.
- `Centinela.cs:1-2` — comentario.

---

### Lo más riesgoso

1. **El núcleo no se renombra, pero tiene tres "NQ" que deciden si la 5.0 hace algo:**
   - `GammaHoyTres.cs:1187` (adaptadores);
   - `Viva3.cs:63` (libro a la familia);
   - `GammaHoyTresCapas.cs:323` (capas).
   - Medido hoy en MESZ6: `familia: cinta - | libro -`. Si se olvidan, la 5.0 en MES queda muda: sin CBOE, sin motor, sin rayas, solo el cartel.
2. **Las capas cruzan la frontera núcleo/familia por nombre:**
   - `Nombre`/`Archivo` = "QQQ"/"NDX"/"NQ" en `GammaHoyTresCapas.cs:122-123`, que no se renombran;
   - del otro lado, la familia, que sí se renombra: `IntegracionFamilia.cs:250`, `BajadorCboe.cs:141-142`, `AlmacenTres.cs:29,182-183` y los libros de `FotoFamilia`.
   - Además: `ModoDe` `:135`, `CercoCapa` `:153`, `RayaCapa*` (Rayas.cs:98-113) y `MajOiVisible` (MajorOi.cs:48-53).
   - Si se olvidan, TRES_SPX y TRES_SPY quedan vacías o gobernadas por la casilla equivocada.
3. **`"_NDX"` en `BajadorCboe.cs:141` sobrevive a un regex con `\b`.** La capa "SPX" se armaría con la cadena de NDX (strikes ~29.000): rayas absurdas o descartadas.
4. **Cotas de la conversión pensadas para NQ:**
   - razón entre 30 y 60 en `GammaHoyTresCapas.cs:283`: SPY mide 10,08, así que la conversión de la familia nunca entra con el spot quieto;
   - tolerancia de 40 pts en `:255`: la base SPX entera mide ~35, así que el control no controla.
5. **TRES_* se pierden en la familia:** `AlmacenTres.cs:136` descarta toda línea con d ≤ 15000, y ES cotiza en ~7.700. Además, el monto gm de ES quedaría ×0,2 en vez de ×0,5 (`AlmacenTres.cs:182`).
6. **Conflicto de identificadores núcleo/familia que puede romper la compilación:**
   - `Familia.LibroNqFamilia` y `Familia.ILibroNq` en `GammaHoyTres.cs:1168,1175,1205` y `Viva3.cs:63`;
   - `ILibroNq` está definido en `_contratos/Contratos.cs:57`, que el plan no renombra.
7. **El nombre visible "PythiaGex 4.0 - Gamma Familia"** (`GammaHoyTres.cs:92-93`, más textos en `:346,380,818,1110`) no lo cubre el renombre. Sin cambio a mano, ATAS lista dos indicadores con el mismo nombre.
8. **Radios en puntos NQ que no miran la raíz:**
   - muros de F5/F6 a 100 (`GammaHoyTresFormula.cs:68`);
   - muros del recuadro a 100 (`GammaHoyTresRecuadro.cs:78`);
   - fórmulas a 150 (`:58`);
   - cruces a 20 y 60 (`GammaHoyTresApoyo.cs:38,51`);
   - capas a 60 y 5 (`GammaHoyTresCapas.cs:89,64`);
   - placebos de la caja a ±12,5/±25 (`GammaHoyTresCaja.cs:127`).
   - Las cuatro ramas que sí escalan, en `GammaHoyTres.cs:401-404` (ES 25/40/0,75/4), son del PRE_REGISTRO y **no coinciden con la 2.0 ni la clásica en ES** (100 pts).
9. **No hay contra qué validar TRES_SPX ni TRES_SPY en ES.** Ninguna versión anterior dibujó capas sobre un gráfico de ES.

---

## H. Punto 7: las réplicas de la 2.0 y de la clásica, y qué hacen esas dos en un gráfico de ES

> Las "trampas del renombre" de esta sección se evaluaron contra dos variantes, `\b` y subcadena. Contra el borrador `herramientas/clonar_5_0.py` valen las conclusiones de B.

Rutas relativas a `PythiaGex\atas\`. Clasificación: **ESCALA** (depende del nivel o la volatilidad del producto), **PRODUCTO** (hecho del producto o ajuste que el original usa en su gráfico de ES), **IGUAL** (no depende del producto). **MEDIDO** = lo leí hoy (27-10) en archivos o logs crudos del disco, con la fecha del dato. **SIN VERIFICAR** = no lo pude confirmar.

### 7.0 Lo que cambia la conclusión (leer antes que el detalle)
- En un gráfico de ES ni la 2.0 ni la clásica dibujan **capas**: `CapaActiva` devuelve false si la raíz no es NQ (`PythiaGexDos/GammaHoyCapas.cs:437-438`, `PythiaGexNiveles/GammaHoyCapas.cs:379-380`). En ES solo dibujan su **libro primario**. Una "capa SPX/SPY sobre ES" no existe en ninguna de las dos: lo que hay que replicar es la PRIMARIA.
- La 2.0 en ES con "Libro automático" (default `true`, `PythiaGexDos/GammaHoy.cs:79-81`) usa **CBOE_SPX**: el libro de SPX por **base aditiva** (Fut = K + base), no SPY ni "por beta" (`PythiaGexDos/GammaHoy.cs:88`). El `EtfPorRazon porBeta: true` de `PythiaGexDos/GammaHoyCapas.cs:395-396` es la capa SPX/SPY **dibujada en un gráfico de NQ**; en ES nunca corre. MEDIDO: `%APPDATA%\ATAS\pythiagex2-gammahoy.log` línea 5096, 05-10 13:11:12 ART: "raiz=ES horizonte=Hoy libro efectivo=CBOE_SPX/ES (automático)".
- La clásica no tiene "automático": usa `Libro` (default CBOE_SPX, `PythiaGexNiveles/GammaHoy.cs:78-81`). MEDIDO en `%APPDATA%\ATAS\pythiagex-gammahoy.log` (AUDIT de la primaria con fut de 4 cifras, versión 1.11d): el operador la tuvo en **Rithmic_ES** la mitad del 17-09 y casi todo el 18 al 25-09 (492 / 614 / 290 / 917 / 669 AUDIT "libro_Rithmic_ES,_sin_base" el 17, 18, 20, 21 y 22-09, contra 508 / 97 / 5 / 33 / 9 con CBOE_SPX) y en CBOE_SPX el 04-10 y casi todo el 05-10. **Con Rithmic_ES la clásica en ES no muestra SPX en absoluto**: la réplica "Clásica SPX" no tendría original con que compararse esos días.
- La cascada de base de las dos originales arranca por **"medida"** (la base de forwards de la cadena con `base_confiable=true`) y sigue por **"medida reciente"** (`base_ultima_buena` del radar, ≤ 360 min) antes de "de la rueda" (`PythiaGexDos/GammaHoyNucleo.cs:314-316`, `PythiaGexNiveles/GammaHoyNucleo.cs:282-284`). En NDX esos dos escalones no se activan nunca; **en SPX sí**. MEDIDO en el log de la clásica (ES, CBOE_SPX): 05-10 → 65 AUDIT "medida", 54 "de_la_rueda" (de noche); 04-10 → 111 "medida", 49 "medida_hace_N", 25 "CRUDA_12_ticks"; 17-09 → 247 "medida", 102 "medida_hace_N". La 4.1 escribe siempre `base: null, base_confiable: false` (`PythiaGexCuatro/_modulos/cboe/CboeCadena.cs:8` y `:349`) y sus réplicas no tienen esos escalones (`Replica20.cs:269-283`, `ClasicaNdx.cs:613-629`). **Es la diferencia más riesgosa del punto 7.**
- En ES las dos originales **miden la base de la rueda con su propia primaria SPX** (MedirBaseRueda solo corre con primaria de índice: `PythiaGexDos/GammaHoy.cs:1687`, `PythiaGexNiveles/GammaHoy.cs:1449`). MEDIDO: `%APPDATA%\ATAS\PythiaGex2\base-rueda-ES.json` = `{"base":52.1,"utc":"2026-10-05T16:15:52","muestras":5}` (la 2.0) y `%APPDATA%\ATAS\PythiaGex\base-rueda-ES.json` = `{"base":52.42,"utc":"2026-10-05T17:20:10","muestras":23}` (la clásica); dato de hace 22 días. La réplica de la clásica en ES tiene que ir con `CongelarEnLaRueda=false` (la 4.1 congela porque en NQ la primaria era Rithmic) y la de la 2.0 necesita un escalón "de la rueda" que `BaseNdx20` no tiene.
- Radios y reglas de dominantes en el gráfico de ES = **los mismos números** que en NQ (2 % / 100 pts, empate 20 %, centroide ±12, 3 dominantes la clásica y 2 la 2.0). Para REPLICAR no se reescalan, aunque 100 pts de ES son ~1,3 % del precio y 100 de NQ ~0,32 %. Si el factor de ESCALA del otro agente se aplica a estos dos módulos, la réplica deja de serlo.
- Dividendo del carry: 0,8 % en NQ → **1,2 % en ES** (`DividendoUsado`, `PythiaGexDos/GammaHoy.cs:1649`, `PythiaGexNiveles/GammaHoy.cs:1411`). Vela con que se mide la base: **M1 en ES** (MEDIDO 05-10, ver 7.3) contra `VelaMin = 2` que la 4.1 usa para NQ (`ClasicaNdx.cs:249`).
- "2.0 QQQ" → "2.0 SPY" no tiene original: con Libro automático la 2.0 en ES nunca dibuja SPY. MEDIDO: en `%APPDATA%\ATAS\PythiaGex2\` hay `razon-NQ-QQQ.txt`, `razon-NQ-SPX.txt`, `razon-NQ-SPY.txt` y **no** hay `razon-ES-SPY.txt` (la clave que escribiría la 2.0 en ES con CBOE_ETF, `PythiaGexDos/GammaHoy.cs:1050`).

### 7.1 `PythiaGexCuatro/_modulos/familia/replica20/Replica20.cs` (leído entero)
Qué replica: `R20_QQQ_vol` = la PRIMARIA de la 2.0 en NQ (QQQ por razón, Libro automático); `R20_NDX_vol` = la CAPA NDX de la 2.0 en NQ (base aditiva); `DOMS_QQQ_vol` / `DOMS_NDX_vol` = la selección de la 2.0 sobre los libros de la 4.1 (comentario `:5-22`).

Constantes y lógica de cuenta (Nucleo20):
- `Replica20.cs:62` — `TASA = 0.0375` — tasa del .ws de la 2.0 (default `PythiaGexDos/GammaHoy.cs:363`) — IGUAL.
- `Replica20.cs:63` — `MULT = 100` — multiplicador de la opción de índice/ETF — PRODUCTO; SPX y SPY también 100 (mismo valor, igual que `MULT_INDICE` de la 2.0, `PythiaGexDos/GammaHoyNucleo.cs:139`).
- `Replica20.cs:64` — `PISO_DIAS = 1/1440` — piso de T — IGUAL.
- `Replica20.cs:65` — `RADIO_PCT = 2.0, RADIO_MAX_PTS = 100` — radio de dominantes min(2 % del fut, 100 pts) — para replicar: PRODUCTO = el ajuste de la 2.0 en el gráfico de ES (defaults 2 y 100, `PythiaGexDos/GammaHoy.cs:403` y `:408`); en ES (fut ~7.700) 2 % = 154 → manda el tope de 100. NO reescalar en la réplica; ESCALA solo si se buscara la misma ventana relativa que en NQ (~25 pts). Valor del .ws del gráfico de ES de la 2.0: SIN VERIFICAR (el AUDIT del 05-10 es compatible: doms a 1 y 11 pts del precio).
- `Replica20.cs:66` — `CUANTAS = 2` — dominantes D1/D2 — IGUAL (MEDIDO: AUDIT de la 2.0 en ES 05-10 13:11:14 ART con 2 doms).
- `Replica20.cs:71-78` — Black-Scholes sin dividendo, r solo en d1 — IGUAL.
- `Replica20.cs:84-90` — GEX = (gC·volC − gP·volP)·100·S²·0,01 (y con OI) — IGUAL.
- `Replica20.cs:93-97` — envejecer desde `generado`, tope 2 días — IGUAL.
- `Replica20.cs:118` — horizonte Hoy: `0 <= dias <= max(1, más cercano + 0,01)` — IGUAL como regla; depende de cómo vengan los `Dias` de la cadena de SPX (ver 7.5, viernes trimestral: SIN VERIFICAR).
- `Replica20.cs:123` — `Fut = K x conv` (QQQ, razón) o `K + conv` (NDX, base) — IGUAL en estructura: SPY por razón, SPX por base.
- `Replica20.cs:132-146` — las 2 de mayor |GEX vol| en el radio, orden estable por K; si ninguna, por OI ("dominantes por OI") — IGUAL (la 2.0 en ES con `DominantesReferencia=true`: sin una por lado ni centroide, `PythiaGexDos/GammaHoy.cs:1508`).
- `Replica20.cs:155` — usa `b.FutMnq` del `LibroMinuto` (definido en `_contratos/Contratos.cs:85` y `:199`) — renombre: ver 7.5.

Razón de QQQ (RazonQqq20 = EscalarCon de la 2.0, `PythiaGexDos/GammaHoy.cs:1040-1131`):
- `Replica20.cs:174` — `VIEJA_MIN = 20` — cadena "vieja" o "congelada" si pasa 20 min el retraso normal — IGUAL.
- `Replica20.cs:175` y `:323` — `RetrasoSeg / RetrasoCboeSeg = 960` — retraso de CBOE del .ws — IGUAL (el mismo feed retrasado para SPY; según el comentario de la 2.0, `PythiaGexDos/GammaHoy.cs:1015-1017`, medido el 17/18-09 en QQQ y ES: ts − ultimo_trade = 15,0 min).
- `Replica20.cs:206` — vela alineada al `ultimo_trade` (o ts − 960 s) — IGUAL; la vela es la de 1 min del MNQ → en ES la del MES.
- `Replica20.cs:216` y `:222` — guardia del contrato continuo `|pAl/pAhora − 1| > 0,006` → cruda — PRODUCTO/ESCALA: depende del salto del roll respecto del precio; en ES el spread trimestral es ~0,6-0,8 % (teórico (3,75 − 1,2) % × 91/365 ≈ 0,64 %; con la base medida el 05-10, 52,4 en 74 días, ≈ 0,83 %): queda al borde. SIN VERIFICAR en un roll real de ES.
- `Replica20.cs:231` — cordura 5 %: la última razón puesta sobre el spot tiene que dar el precio a menos de 5 % — IGUAL (distingue ES/SPY ~10 de NQ/QQQ ~41).
- `Replica20.cs:238` — última razón válida hasta 4 días — IGUAL.
- `Replica20.cs:250-251` — mediana de las últimas 24 razones frescas — IGUAL.
- `Replica20.cs:253` — la última válida nunca hacia atrás — IGUAL.
- Archivo de estado: ninguno (`:34` "Nada de E/S"; la 2.0 guarda `razon-<raíz>-<ticker>.txt`, `PythiaGexDos/GammaHoyCapas.cs:22-35`, que la réplica no lee). En ES la 2.0 escribiría `PythiaGex2\razon-ES-SPY.txt` solo con CBOE_ETF manual.

Base de NDX (BaseNdx20 = rama índice de CalcularAdentro reducida):
- `Replica20.cs:262` y `:322` — `DIVIDENDO_NQ = 0.008` — dividendo para el carry — PRODUCTO: en ES **0,012** (`PythiaGexDos/GammaHoy.cs:1649`, default del núcleo `PythiaGexDos/GammaHoyNucleo.cs:49`; el valor exacto del rendimiento de SPX no hace falta: es la constante de la 2.0). El NOMBRE `DIVIDENDO_NQ` no lo toca un renombre con `\b` (ver 7.5) y el VALOR hay que cambiarlo a mano.
- `Replica20.cs:265-266` — carry = fut·(tasa − div)·días al vencimiento/365 — IGUAL en fórmula.
- `Replica20.cs:275` — cota `|b − carry| <= max(0,6·|carry|, 0,0006·fut)` — IGUAL (relativa).
- `Replica20.cs:279-282` — cascada CRUDA → TEÓRICA → CRUDA sin cota — **en ES no alcanza**: la 2.0 en ES (primaria SPX) usa medida → medida reciente → de la rueda → cruda → teórica → cruda sin cota (`PythiaGexDos/GammaHoyNucleo.cs:314-319`). El comentario `:32-33` ("con la primaria en QQQ la 2.0 tampoco mide la rueda") es cierto en NQ y FALSO en ES.
- `Replica20.cs:287-300` — vencimiento del futuro: tercer viernes del mes del código, 13:30 UTC — IGUAL (ES y NQ vencen el mismo día; `MESZ6` lo parsea el mismo regex `:291`). En diciembre (EST) 9:30 NY son 14:30 UTC: 1 h de error en el carry, despreciable.
- `Replica20.cs:303-315` — trimestral más cercano si el código no trae mes — IGUAL.
- `Replica20.cs:321` — `Contrato = ""` (lo pone el integrador: MNQZ6) — PRODUCTO: MESZ6.
- `Replica20.cs:325` `DiasHistoria = 6`, `:330` `SondeoCadaS = 20`, `:337` `FutDelInstante = true` — IGUAL.

El motor de la réplica:
- `Replica20.cs:361` — `LIBRO_QQQ = "2.0 QQQ"`, `LIBRO_NDX = "2.0 NDX"` — nombres de libro (mecánico → "2.0 SPY"/"2.0 SPX"; ojo semántica, 7.4).
- `Replica20.cs:412-417` — cierre de la vela de 1 min = último tick antes del fin del minuto, ventana 4 h (`:414`, `:425`); si no, la m2 interpolada — IGUAL.
- `Replica20.cs:436` — ventana de cadenas desde el inicio de sesión − 6 días — IGUAL (sesión CME igual en ES).
- `Replica20.cs:446`, `:547` — `_cboe.Fotos("QQQ", …)`; `:495` — `_cboe.Fotos("NDX", …)` — claves de libro (mecánico → "SPY"/"SPX"; dependen del mapeo de `_modulos/cboe/BajadorCboe.cs:141-142`, ver 7.5).
- `Replica20.cs:566` — QQQ: `S = fut / razón` — IGUAL.
- `Replica20.cs:587-590` — NDX: vencimiento del futuro, base, `S = fut − b` — IGUAL en estructura; cambia la cascada (arriba).
- `Replica20.cs:595` — "congelada" si el `ultimo_trade` es ≥ 15:59 NY — IGUAL (SPX se congela como NDX según la tarea; en datos de SPX: SIN VERIFICAR).
- `Replica20.cs:616-619` — textos con "MNQ", "spot QQQ" — mecánico.
- `Replica20.cs:637-640` — fut del instante: último tick del MNQ ≤ t, a lo sumo 120 s — IGUAL.
- `Replica20.cs:656` — `b.Libro == "QQQ"` → `DOMS_QQQ_vol`, `"NDX"` → `DOMS_NDX_vol` — mecánico; `:659` usa `ReglasFam.Escala(b)` (escala del monto por libro: fuera de esta sección).
- `Replica20.cs:666, 669, 677, 680` — `r.FutMnq` — renombre: ver 7.5.
- `Replica20.cs:740-741` — precio round 2, monto GEX/1e6 round 1, strike round 2 — IGUAL.

### 7.2 `PythiaGexCuatro/_modulos/familia/clasica/ClasicaNdx.cs` y `MuestrasClasica.cs` (leídos enteros)
Qué replica: el 0Γ y las D1-D3 de la **capa NDX** de la clásica en NQ, con la primaria de la clásica en Rithmic (por eso congela la base en la rueda, `:23-37`).

Zero (CruceClasica = `PythiaGexNiveles/GammaHoyNucleo.cs:225-244`):
- `ClasicaNdx.cs:77` — `PASOS = 60` (61 precios) — IGUAL.
- `ClasicaNdx.cs:78` — `AMP = 0.03` (±3 % de S; en la clásica `0.03 · max(1, Apalancamiento)`, `PythiaGexNiveles/GammaHoyNucleo.cs:227`, 1 en un índice) — IGUAL (relativa: en SPX ±231 pts con paso ~7,7; en NDX paso ~31).
- `ClasicaNdx.cs:100` — horizonte Hoy — IGUAL (ver 7.5 viernes trimestral).
- `ClasicaNdx.cs:110-111` — el PRIMER cambio de signo de abajo hacia arriba, interpolado — IGUAL: la clásica en ES usa este mismo Cruce para su primaria (`PythiaGexNiveles/GammaHoyNucleo.cs:335`; no tiene `ZeroGammaInterpolado`). La 2.0 en ES NO: usa el cruce por strike más cercano al precio (`PythiaGexDos/GammaHoyNucleo.cs:370-374`; MEDIDO `zeroModo=interp` en su AUDIT de ES del 05-10).

Dominantes D1-D3 (DominantesClasica = `PythiaGexNiveles/GammaHoyNucleo.cs:353-414`):
- `ClasicaNdx.cs:135` — `RADIO_PCT = 0.02, RADIO_MAX = 100` — min(2 %, 100 pts) — para replicar: PRODUCTO = ajuste de la clásica en ES (defaults `PythiaGexNiveles/GammaHoy.cs:371` y `:376`); en ES manda el tope 100. NO reescalar (igual que en 7.1).
- `ClasicaNdx.cs:135` — `EMPATE = 0.20` — dentro del 80 % del mayor del lado gana el más cercano al fut — IGUAL (default `PythiaGexNiveles/GammaHoy.cs:385`).
- `ClasicaNdx.cs:135` — `CENTROIDE = 12` pts — centroide ponderado por |GEX| a ±12 del ganador — IGUAL para replicar: el 12 nació para SPX ("~2 strikes de SPX a cada lado", `PythiaGexNiveles/GammaHoyNucleo.cs:65`; default `PythiaGexNiveles/GammaHoy.cs:393`). MEDIDO: en ES la clásica dibujó centroides fuera de strike (05-10 20:31 ART `doms=7853.24/7831.11/7860.00`).
- `ClasicaNdx.cs:137` — `cuantas = 3` — D1 arriba, D2 abajo, D3 relleno — PRODUCTO = ajuste del gráfico de ES de la clásica: MEDIDO 2 dominantes el 17-09, cambio a 3 durante el 18-09 y 3 en todos los AUDIT de ES desde el 20-09 (131 de 131 el 05-10). El default del código es 2 (`PythiaGexNiveles/GammaHoy.cs:368`): el operador lo subió a 3; valor en el .ws: SIN VERIFICAR.
- `ClasicaNdx.cs:163-165` — libro por volumen; si ningún strike del radio tiene volumen, OI — IGUAL.
- `ClasicaNdx.cs:186-191` — D3 = la de más peso que no repite Fut — IGUAL.

Base de la rueda (ReglaBaseClasica = MedirBaseRueda, idéntica en la 2.0 `PythiaGexDos/GammaHoy.cs:1656-1723` y en la clásica `PythiaGexNiveles/GammaHoy.cs:1418-1485`):
- `ClasicaNdx.cs:210` — `VENTANA = 24`, `MIN_BUENAS = 5` — últimas 24 muestras; adopta con ≥5 buenas — IGUAL.
- `ClasicaNdx.cs:211` — ventana 13:50-20:10 UTC fija ("9:50-16:10 NY" en verano) — IGUAL (bug-compatible con las dos originales; desde el 01-11, horario de invierno, es 8:50-15:10 NY y las muestras de antes de las 9:30 comparan contra un spot sin abrir: afecta a NQ y ES por igual).
- `ClasicaNdx.cs:212` — cadena de ≤ 30 min — IGUAL.
- `ClasicaNdx.cs:228` — tope = max(3·MAD, 0,0002·precio) — IGUAL (relativo: 1,5 pts en ES, 6,2 en NQ). MEDIDO: MAD de las muestras de ES 0,6-0,8 pts (05-10).
- `ClasicaNdx.cs:231-233` — mediana de las buenas con ≥3; adopta con ≥5 o sin base — IGUAL.
- `ClasicaNdx.cs:515` — vela que contiene (sello − 960 s) — IGUAL.
- `ClasicaNdx.cs:544` — la lista de 24 se vacía al cambiar el día UTC (diferencia documentada con la clásica) — IGUAL.
- `ClasicaNdx.cs:545` — muestra = cierre de la vela − spot del índice — IGUAL.

Opciones (OpcionesClasicaNdx):
- `ClasicaNdx.cs:240` — `Contrato` (MNQZ6) — PRODUCTO: MESZ6.
- `ClasicaNdx.cs:241` — `Dividendo = DIVIDENDO_NQ` (0,008) — PRODUCTO: **0,012** en ES (`PythiaGexNiveles/GammaHoy.cs:1411`).
- `ClasicaNdx.cs:243` — `RetrasoCboeSeg = 960` — IGUAL (default `PythiaGexNiveles/GammaHoy.cs:253`).
- `ClasicaNdx.cs:249` — `VelaMin = 2` — marco del gráfico de la clásica en NQ el 08-10 — PRODUCTO/CONFIG: en ES **1** (MEDIDO: las muestras "base muestra" de la clásica en ES del 05-10 caen en velas de minutos consecutivos 15:58, 15:59, 16:01, 16:02…; las de la 2.0 en ES también: 15:53, 15:55, 15:56…). Si el gráfico de ES del operador sigue en M1: SIN VERIFICAR.
- `ClasicaNdx.cs:251` — base de la rueda vale 24 h — IGUAL.
- `ClasicaNdx.cs:255` — `CongelarEnLaRueda = true` — copia a la clásica con primaria Rithmic — en ES **false**: con primaria CBOE_SPX la clásica mide y cambia la base dentro de la rueda (y además "medida" le gana a la rueda, 7.0).
- `ClasicaNdx.cs:259` — `CarpetaDatos` (%APPDATA%\ATAS\PythiaGex4) — mecánico → PythiaGex5.
- `ClasicaNdx.cs:265` `RetencionDias = 21`, `:267` `DiasHistoria = 6`, `:270` `FutDelInstante`, `:273` `SondeoCadaS = 20` — IGUAL.
- `ClasicaNdx.cs:379` y `:398-407` — rama de 2 min (balde m2 de la cinta, `VelaCompleta`) contra rama de 1 min (último tick del minuto) — con VelaMin=1 en ES se usa la rama de 1 min; la guarda de "vela incompleta" no se aplica (`:379`): IGUAL, pero es la rama MENOS probada (los arneses midieron con 2).

Cascada y resultado:
- `ClasicaNdx.cs:454` — `_cboe.Fotos("NDX", …)` — mecánico → "SPX" (depende del mapeo de BajadorCboe, 7.5).
- `ClasicaNdx.cs:513` — muestras solo con sello en la ventana y cadena ≤ 30 min — IGUAL.
- `ClasicaNdx.cs:593` y `:598-603` — congelada en [13:50, 20:11) UTC — IGUAL como regla; en ES apagarla (arriba).
- `ClasicaNdx.cs:619` — cota `max(0,6·|carry|, 0,0006·fut)` — IGUAL.
- `ClasicaNdx.cs:624-627` — cascada rueda → CRUDA → TEÓRICA → CRUDA sin cota — **en ES falta "medida" y "medida reciente" delante** (`PythiaGexNiveles/GammaHoyNucleo.cs:282-283`). El comentario `:14-15` ("las cadenas … traen base = null y base_confiable = false, 910 de 910") es cierto para NDX y falso para SPX (MEDIDO: radar de ES del 05-10 15:51Z `base 53.26, base_confiable true, base_error_ticks 6.3, contrato ESZ6`, caché `%APPDATA%\ATAS\pythiagex-gammahoy-cadena-ES.json`).
- `ClasicaNdx.cs:648` — `S = fut − base` — IGUAL.
- `ClasicaNdx.cs:654` — congelada si `ultimo_trade` ≥ 15:59 NY — IGUAL (SIN VERIFICAR en datos de SPX).
- `ClasicaNdx.cs:696` — texto "zero sin cruce en +-3 %" — IGUAL.
- `ClasicaNdx.cs:714-717` — fut del instante, 120 s — IGUAL.
- `ClasicaNdx.cs:731, 732, 748, 757` — `r.FutMnq` — renombre: 7.5.
- `ClasicaNdx.cs:765` — `r.MetaDe("NDX")` (base sincronizada de la 4.1, solo texto) — mecánico → "SPX".
- `ClasicaNdx.cs:785-786`, `:799` — 0Γ round 2; D1-D3 con GEX/1e6 ("×100 de NDX", también ×100 en SPX) — IGUAL.
- Nombres: `ClasicaNdx.cs:312` `LIBRO = "Clasica NDX"`, `:338` `SERIE = "R10_NDX_zero"`, `:339` `SERIE_DOM = "R10_NDX_dom"`, `:337` `VERSION` — mecánico (ojo `\b` y guion bajo, 7.5).

`MuestrasClasica.cs`:
- `MuestrasClasica.cs:5` y `:30` — archivo `familia\muestras-clasica-NDX-<día UTC>.jsonl`, `PREFIJO = "muestras-clasica-NDX-"` — mecánico → `muestras-clasica-SPX-` en `%APPDATA%\ATAS\PythiaGex5\familia`.
- `MuestrasClasica.cs:41` — subcarpeta `familia` — IGUAL.
- `MuestrasClasica.cs:73`, `:83`, `:95` — clave (sello, vela_min, retraso_s): con VelaMin=1 en ES son "otra medición" que las de 2 min — IGUAL (no mezcla).
- `MuestrasClasica.cs:116-130` — poda por fecha del nombre — IGUAL.

### 7.3 La 2.0 (`PythiaGexDos`) en un gráfico de ES
- Raíz: `GammaHoy.cs:966-973` (RaizManual manda; MNQ/NQ → "NQ"; M2K/RTY → "RTY"; todo lo demás → "ES"); soportadas `:977-983` (MES/ES/MNQ/NQ/M2K/RTY). MESZ6 → "ES".
- Libro: `GammaHoy.cs:88` — automático: NQ → CBOE_ETF (QQQ por razón), **ES → CBOE_SPX**; `Libro` manual (default CBOE_SPX, `:103-105`) solo vale con el automático apagado, Rithmic_ES incluido (código de la 2.0.4 en `:88`; el comentario `:83-87` y la descripción `:80` todavía dicen que Rithmic_ES se respeta siempre: están viejos, manda el código).
- Tickers en ES con CBOE_SPX: `GammaHoy.cs:949-954` → `Feed.Bajar(Url, "ES")` = `ES_radar.json` de `https://waltermosqueda.github.io/PythiaGex/datos/atas/` (`Feed.cs:178-185`, caché `%APPDATA%\ATAS\pythiagex2-gammahoy-cadena-ES.json`, `Feed.cs:193`) y `Feed.BajarUltima(UrlArchivo, "ES")` = `ultima-ES.json` de la rama cadenas o de `%APPDATA%\ATAS\PythiaGex\cboe-local\` si tiene < 3 min (`Feed.cs:109-122`, `:134-165`); gana la de `generado` más nuevo. "ES" es la clave del libro de **SPX** (igual que `cadena-ES-*` en cboe-local). Con CBOE_ETF manual: `ultima-SPY.json` (`GammaHoy.cs:941-946`, `RaizLibro` `:999-1004`). Con Rithmic_ES: opciones de ES por Rithmic, Black-76, sin base (`GammaHoyNucleo.cs:191-193`, `:313`).
- Conversión en ES (CBOE_SPX): base aditiva, `S = fut − base`, `Fut = K + base` (`GammaHoyNucleo.cs:322`, `:343`). Cascada `GammaHoyNucleo.cs:314-319`: medida (base_confiable, cota con carry o carry del trimestre siguiente `:308`) → medida reciente (`base_ultima_buena` ≤ 360 min) → de la rueda (≤ 24 h) → CRUDA (cota) → TEÓRICA (carry) → CRUDA sin cota. Cota `:305`. Carry `:298-304` con dividendo `DividendoUsado` = 1,2 % en ES (`GammaHoy.cs:1649`; ajuste `Dividendo` 0 = automático, `:348-350`). MEDIDO en ES el 05-10 13:11 ART: `base=53.42 origen=medida`.
- Base de la rueda en ES: `GammaHoy.cs:1656-1723` (solo con primaria de índice `:1687`; ventana `:1691`; vela del gráfico que contiene ts − 960 s `:1694-1695` con `BarraDe`, 600 velas, `:2561`; 24 muestras `:1704`; tope `:1709`; adopta `:1712`; escribe `:1716`). Archivo: `%APPDATA%\ATAS\PythiaGex2\base-rueda-ES.json` (`:1651`). MEDIDO: 52,1 del 05-10 16:15:52 UTC con 5 muestras; gráfico M1.
- "Por beta" (NO aplica en ES): `GammaHoyCapas.cs:105-113` (Fut = F·(1 + β·(K/S − 1)), apalancamiento 1/β), `:395-397` (capas SPX, SPY, ES con `porBeta: true`), `:495-522` (β de NQ sobre ES con velas compartidas de los dos gráficos, 90 min, ≥20 pares, r² ≥ 0,2, acotada a [0,4; 3], si no 1 "SUPUESTA"), `:793-797`; `Feed.cs:68-81`. Todo esto solo existe en un gráfico de NQ (`:437-438`).
- Vela/hora de la cuenta: reprecia cada segundo con el cierre de la vela actual (`GammaHoy.cs:1486-1494`; `MedirBaseRueda` en cada repreciado `:1493`).
- Dominantes en ES (defaults del .ws; MEDIDO consistentes con el AUDIT del 05-10): `CuantasDominantes = 2` (`GammaHoy.cs:400`), radio `RadioDominantesPct = 2` (`:403`) con tope `RadioDominantesMaxPts = 100` (`:405-408`; la descripción dice "100 en NQ"), `DominantesReferencia = true` (`:413-415`: las dos más grandes en valor absoluto, strike exacto, sin "una por lado" ni centroide, `:1508`), `DominantesDeNoche = Volumen` (`:502`). Selección `GammaHoyNucleo.cs:394-410`. MEDIDO: `doms=7813.42/7823.42` = strikes 7760 y 7770 + base 53,42.
- Zero en ES: por strike, el cruce más cercano al precio (`ZeroGammaInterpolado = true`, `GammaHoy.cs:442-444`; `GammaHoyNucleo.cs:234-255`, `:370-378`). La réplica R20 no lo replica (solo dominantes).
- Estado y archivos en ES: `PythiaGex2\base-rueda-ES.json`; `razon-ES-SPY.txt` solo con CBOE_ETF (no existe: MEDIDO); estelas de capa `PythiaGex2\estela\estela-<capa>-<día>.jsonl` (`GammaHoyCapas.cs:600`, `:627`) no se escriben en ES (no hay capas); log `%APPDATA%\ATAS\pythiagex2-gammahoy.log` con AUDIT de la PRIMARIA ("AUDIT fut=…", sin "capa="), hora ART.
- Referencia disponible para un arnés: MEDIDO solo 5 AUDIT de la 2.0 en ES (05-10). No alcanza para medir paridad.

### 7.4 La clásica (`PythiaGexNiveles`) en un gráfico de ES
- Raíz igual que la 2.0 (`GammaHoy.cs:834-841`; `RaizLibro` `:867-872`). Libro: `Libro` (default CBOE_SPX, `:78-81`), sin automático; `BajarFeed` `:802-832` (CBOE_ETF `:809`, radar + ultima `:817`).
- Capas en ES: ninguna (`GammaHoyCapas.cs:376-391`, corte en `:380`).
- Conversión de la primaria SPX: la misma cascada que la 2.0 (`GammaHoyNucleo.cs:282-287`: medida → medida reciente → de la rueda → cruda → teórica → cruda sin cota; cota `:273`, `:276`), `S = fut − base` (`:290`), `Fut = K + base` (`:311`), dividendo 1,2 % (`GammaHoy.cs:1411`).
- Base de la rueda en ES: el mismo MedirBaseRueda (`GammaHoy.cs:1418-1485`, vela del gráfico con `BarraDe` `:1457`, `:2210`), archivo `%APPDATA%\ATAS\PythiaGex\base-rueda-ES.json` (`:1413`). MEDIDO: 52,42 del 05-10 17:20:10 UTC con 23 muestras; gráfico M1 (velas 15:58, 15:59, 16:01…).
- Zero: Cruce en grilla ±3 %, 61 precios, primer cruce de abajo hacia arriba (`GammaHoyNucleo.cs:225-244`, `:335`) = lo que hace `CruceClasica`.
- Dominantes: radio min(2 %, 100) (`GammaHoy.cs:371`, `:376`; `GammaHoyNucleo.cs:353-354`), una por lado con empate 20 % (`GammaHoy.cs:380`, `:385`; `GammaHoyNucleo.cs:371-396`), centroide ±12 (`GammaHoy.cs:389`, `:393`; `GammaHoyNucleo.cs:398-414`), cuántas = 3 en el gráfico de ES desde el 18/20-09 (MEDIDO; default 2 en `GammaHoy.cs:368`), de noche por volumen (`:451`). Sin `DominantesReferencia`: `Centroide = DominanteCentroide` (`GammaHoy.cs:1038`, `:1272`).
- Qué base usó en ES (MEDIDO, AUDIT de la primaria con fut de 4 cifras, hora ART): 05-10 → "medida" 65 en la rueda, "de_la_rueda" 54 de noche, Rithmic 12; 04-10 → medida 111, medida_hace_N 49, CRUDA_12_ticks 25; 17-09 (semana del roll) → medida 247, medida_hace_N 102, de_la_rueda 106, TEÓRICA "carry 0.8 (cruda 74.x descartada)" 53 (el gráfico seguía en U6 a un día del vencimiento: la cota del carry tiró la cruda de diciembre). Ejemplo de la diferencia medida/rueda: 05-10 13:16:18 ART base 53,63 "medida" contra 52,42 de la rueda adoptada más tarde = 1,21 pts.
- Referencia para un arnés: la clásica 1.11d corrió en ES el 17, 18, 20, 21, 22 y 25-09 y el 04 y 05-10 (~1.000 AUDIT por día), pero con CBOE_SPX de primaria solo el 04-10 (185) y el 05-10 (119) y sueltos; el resto es Rithmic_ES. El log mezcla instancias de NQ y ES: filtrar por "AUDIT fut=" sin "capa=" y fut < 10.000.

### 7.5 Qué hace falta para que las dos réplicas copien en ES lo que esas dos hacen en ES
1. **"2.0 NDX" → "2.0 SPX" no es un renombre.** En NQ copia una CAPA; en ES tiene que copiar la PRIMARIA CBOE_SPX de la 2.0: agregar a `BaseNdx20.Base` (`Replica20.cs:269-283`) los escalones medida (con su cota que acepta el carry del trimestre siguiente, `PythiaGexDos/GammaHoyNucleo.cs:308`), medida reciente y de la rueda (la 2.0 la mide sola en ES: reusar `ReglaBaseClasica` con su propia lista, VelaMin 1, sin congelar), dividendo 0,012, contrato MESZ6. Sigue siendo base aditiva: **nada de beta** (eso es la capa SPX sobre NQ).
2. **"Medida" necesita el criterio de confianza**, que la 4.1 dejó afuera (`_modulos/cboe/CboeCadena.cs:8`: "la confianza necesita la curva del Tesoro"). El criterio está en `PythiaGex\pythiagex\base.py:250-255`: residuo ≤ 8 ticks, dispersión ≤ 12 ticks, ≥ 5 vencimientos, ≥ 8 muestras y dividendo implícito 0 ≤ q ≤ 0,04 (q necesita la curva de tasas externa). El tick es 0,25 en ES y NQ (`PythiaGex\pythiagex\tablero.py:19-20`): por eso NDX (4× más grande) casi nunca pasa y SPX sí. Sin ese escalón: (a) la réplica de la 2.0 coincide en la rueda solo cuando la cruda es confiable y pasa la cota (la "medida" ES la cruda, MEDIDO: radar del 05-10 `base = base_cruda = 53.26`), pero usa una cruda NO confiable donde la 2.0 usa medida reciente o rueda (MEDIDO de noche: `ultima-ES.json` de cboe-local del 27-10 02:49Z `base null, base_cruda 37.18, base_error_ticks 51.9` → 51,9 ticks ≈ 13 pts de error posible); (b) la réplica de la clásica usaría la rueda en plena rueda, donde la clásica usa "medida" (~1-2 pts de diferencia el 05-10). Aproximación sin q: SIN VERIFICAR cuánto difiere.
3. **"Medida reciente"** (`base_ultima_buena` ≤ 360 min) solo viene en el radar de la nube (`PythiaGexDos/Feed.cs:225-226`); `ultima-ES.json` de cboe-local no trae el campo (MEDIDO). Con la regla de independencia no se puede leer: o se reconstruye ("la última base confiable propia y su edad", equivalencia SIN VERIFICAR) o se declara la diferencia. Pesa: 49 a 102 AUDIT por día en la clásica (MEDIDO 04-10 y 17-09).
4. **Clásica en ES**: `CongelarEnLaRueda = false` (`ClasicaNdx.cs:255`), `VelaMin = 1` (`:249`), dividendo 0,012 (`:241`), contrato MESZ6 (`:240`), escalones medida y medida reciente delante de la rueda en `BaseCascada` (`:613-629`). Zero (`:77-115`) y D1-D3 (`:135-204`) quedan igual, con 3 dominantes. Si el operador tiene la clásica de ES en Rithmic_ES, la serie no tiene original: decirlo en la pestaña.
5. **"2.0 QQQ" → "2.0 SPY"**: con Libro automático la 2.0 en ES no dibuja SPY (MEDIDO: ni log ni `razon-ES-SPY.txt`). O se borra, o se rotula "la 2.0 con CBOE_ETF manual" sin referencia medida. Lo único atado al producto adentro: la guardia 0,6 % del roll (`Replica20.cs:216`, `:222`) y la vela de 1 min (del MES).
6. **No reescalar** en estos dos módulos el radio 2 %/100 (`Replica20.cs:65`, `ClasicaNdx.cs:135`), el empate 20 %, el centroide ±12 ni las cuántas (2 y 3): son los ajustes de las originales en ES. Lo único que cambia de número: dividendo 0,008 → 0,012 y VelaMin 2 → 1.
7. **Horizonte "Hoy" el viernes trimestral (calendario, lo heredan las réplicas)**: MEDIDO en `%APPDATA%\ATAS\PythiaGex\cboe-local\cadena-ES-2026-10-16.jsonl.gz` (tercer viernes): un solo vencimiento "2026-10-16" con días hasta las 16:00 NY (0,812 a las 00:30Z; 0,0169 a las 19:35Z). El parser de Python ignora la raíz (`PythiaGex\pythiagex\exposicion.py:13`, `:44-57`): la SPX mensual AM (liquida 9:30) y la SPXW PM quedarían juntas todo el viernes. Si las filas de la AM están adentro: SIN VERIFICAR. Como las originales usan esas mismas cadenas, para REPLICAR es IGUAL; para la verdad del nivel, no.
8. **Renombre mecánico que toca estos tres archivos** (para el script):
   - `FutMnq` está definido en `_contratos/Contratos.cs:85` y `:199`, fuera del conjunto "familia" del plan, y se usa en `Replica20.cs:155, 666, 669, 677, 680` y `ClasicaNdx.cs:731, 732, 748, 757`: si MNQ→MES lo cambia en `_modulos` y no en `_contratos`, no compila.
   - Los tokens pegados a guion bajo (`R10_NDX_zero`, `R10_NDX_dom`, `R20_QQQ_vol`, `R20_NDX_vol`, `DOMS_QQQ_vol`, `DOMS_NDX_vol`, `DIVIDENDO_NQ`) no los toca un regex con `\b` (el guion bajo es carácter de palabra): tienen que cambiar todos o ninguno, a la vez que `CatalogoFamilia`, `ExtrasCompuestos`, `ArmadoPantalla` y las `S_*`.
   - Las clases CamelCase (`ClasicaNdx`, `OpcionesClasicaNdx`, `BaseNdx20`, `RazonQqq20`, `FotoQqq`) necesitan la regla de variantes Ndx/Qqq.
   - Las dos réplicas piden `Fotos("NDX")`/`Fotos("QQQ")`; el mapeo está en `_modulos/cboe/BajadorCboe.cs:141-142` (`Nombre "NDX", Archivo "NQ", Simbolo "_NDX"`). El símbolo `"_NDX"` tampoco lo toca un `\b`: si queda, la "SPX" bajaría NDX y las rayas saldrían vacías o a ~23.000 pts (fuera de esta sección, avisado al padre).
9. **Arneses** (el detalle es del punto 8): los de `replica20`, `clasica` y `clasica_dom` comparan contra "AUDIT capa=NDX"; en ES la referencia son los AUDIT de la PRIMARIA ("AUDIT fut=", sin "capa=") de la clásica 1.11d, con CBOE_SPX solo el 04 y 05-10. De la 2.0 en ES hay 5 AUDIT.

---

## I. Punto 8: los arneses de paridad (`atas/_test_cuatro_paridad`)

Rutas relativas a `PythiaGex\`. `%APPDATA%` = `C:\Users\wmx_7\AppData\Roaming`. Clasificación de cada constante: **ESCALA** (depende del precio o la volatilidad: reescalar), **PRODUCTO** (hecho del producto), **IGUAL** (no depende del producto), **DATO** (fecha, archivo o número fijo de la prueba: cambia con los datos de ES), **TQQQ** (se borra).

### 8.0 Estado y piezas comunes

**Estado de la batería (medido el 27-10)**
- `atas/_test_cuatro_paridad/resultados_todo.txt:1`: la última corrida completa es del 2026-10-09 20:37, sobre la 4.1.5e. Todo dio verde salvo e2e, que es informativo.
- El código de hoy es la 4.1.6g (`atas/PythiaGexCuatro/GammaHoyTres.cs:97`, `VERSION = "4.1.6g"`). El log de la pantalla del 26-10 dice "108 en el grupo '0. PRENDER / APAGAR'" (`%APPDATA%\ATAS\pythiagex4-pantalla.log`, alrededor de la línea 21721). En el fuente hay 113 apariciones de ese `GroupName`.
- `herramientas/grupo_arriba_415e.py` todavía lista 101 casillas. Por eso ws_compat (W6-W8) y pantalla (A21-A23) esperan 101. **SIN VERIFICAR que hoy pasen**: no los corrí. La diferencia de conteo sugiere que no.
- `correr_todo.ps1` corre los pasos en este orden:
  - 28: build con `herramientas/build_serial.ps1 -Dir ..\PythiaGexCuatro`
  - 29: adaptadores · 30: cboe · 31: nq · 32: ndx · 33: qqq
  - 35: fam · 36: fam integrado · 37: tqqq · 39: posiciones
  - 42: replica20 · 46: clasica · 51: clasica_dom
  - 52: pantalla (`..\PythiaGexCuatro\_modulos\pantalla\prueba`) · 57: ws_compat · 58: e2e
  - Para el clon: hay que apuntar todo a `PythiaGexCinco` y sacar la línea 37 (tqqq).

**La referencia de la vista previa (`referencia/`; correr_todo.ps1 no la corre, pero la consumen nq, ndx, qqq, fam, fam/integrado y e2e)**
- `referencia/generar_referencia.py:20-23`: corre `preview_niveles.Motor(día, vivo=False, hasta=día+21 h).historico()`.
- `referencia/generar_referencia.py:28-29`: escribe `niv`, `tres`, `salida` y `oi_nq` (clave NQ).
- `referencia/generar_referencia.py:37`: días por defecto 2026-10-07 y 2026-10-08. Salida: `referencia/ref-2026-10-07.json` y `ref-2026-10-08.json` (2,7 MB cada uno).
- La ventana de sesión 22:00 UTC de la víspera → 21:00 UTC es la de CME Globex: IGUAL para ES.

**La cuenta Python que hace de verdad está atada a NQ/NDX/QQQ.** Para ES hay que hacerle una variante antes de poder generar cualquier referencia.

`laboratorio/tres/auditoria_0810/backtest_familia.py`:
- 141-143: `DIAS` (DATO)
- 145: `VIVA3 = ("2026-10-07","2026-10-08")` (DATO)
- 146: `POSFERIADO`
- 147: `RADIO = 100.0` (ESCALA)
- 148: `ISLA = 0.10` (IGUAL)
- 150: `CONF_PTS = 3.0` (ESCALA)
- 151: `FAM_GRILLA = 5.0` (ESCALA)
- 152: `MULT = {"NQ": 20.0, "NDX": 100.0, "QQQ": 100.0}` (PRODUCTO; ES = 50, SPX = 100, SPY = 100)
- 153: `RETRASO = 900` (IGUAL)
- 154: `VIDA_VIVA, VIDA_CBOE = 300, 1500` (IGUAL)
- 155: `TUNEL = 40.0` (ESCALA)
- 158: `LIBROS = ("NQ","NDX","QQQ")`
- 164: `V.cargar("MNQ", 2)`
- 267-269: base aditiva si el libro es NDX, razón en otro caso
- 435: `fotos_viva3("NQ")` y `fotos_viva("NQ")`
- 504: `BM("NQ", …)`
- 644: `MULT/100`

`laboratorio/tres/auditoria_0810/preview_niveles.py`:
- 64: `TICKER = {"NDX": "NQ", "QQQ": "QQQ"}`
- 67: `VIDA_TRES`
- 73-145: `SERIES` (los ids `*_NQ_*/*_NDX_*/*_QQQ_*`; las de QQQ con `banda=20`, ESCALA)
- 329: `V.cargar("MNQ", 2)`
- 333, 349, 355, 375, 392: `cinta-NQ-<día>.csv`
- 475: `viva3-NQ`
- 623: "MNQZ6 = NQZ6"
- 648, 702, 715: capas NQ, NDX, QQQ
- 771: strike por base (NQ, NDX) o por razón

Otras piezas del laboratorio:
- `laboratorio/tres/extremos_rebote.py:476`: `leer_estela` descarta `d <= 15000` (ESCALA crítica: con ES cerca de 7.700 tira todas las rayas TRES).
- `laboratorio/tres/fuentes_3_0.py:36-37, 126`: `velas_cinta` lee `cinta-NQ`.
- Ya parametrizado:
  - `laboratorio/tres/libros.py:82-90`: `fotos_cboe(ticker, día)`.
  - `laboratorio/tres/libros.py:133-140`: `fotos_viva(raiz, día)`. En la 267 ya tiene un umbral de corrimiento propio del ES: 30 para NQ, 8 para el resto.
  - `fuentes_3_0.py:69-74`: `fotos_viva3(raiz, día)`.
  - `laboratorio/tres/auditoria_0810/_prio.py:17-18`: rutas de CBOE y de la cinta, genéricas.
- La parte de CBOE del Python de producción **ya acepta SPX y SPY**:
  - `archivar_cadena.py:126`: `INDICE = {"ES": "_SPX", "NQ": "_NDX", …, "SPY": "SPY", "TQQQ": "TQQQ"}`.
  - `pythiagex/base.py:363-365`: `FUTURO = {"SPX": "ES", …}` y `MICRO = {"ES": "MES", …}`.

**Rutas de datos de NQ que se repiten en casi todos los arneses**
- `profundidad/estado/cinta/cinta-NQ-<día>.csv` y `-relleno.csv`: la cinta de ticks del MNQ.
- `%APPDATA%\ATAS\PythiaGex3\viva\viva3-NQ-<día>.jsonl`: el libro NQ de la 3.0.
- `%APPDATA%\ATAS\PythiaGex3\estela\estela-{NQ,NDX,QQQ}-<día>.jsonl`.
- `%APPDATA%\ATAS\PythiaGex\cboe-local\cadena-{NQ,QQQ,TQQQ}-<día>.jsonl.gz`. Ojo: "NQ" es el archivo de `_NDX`.
- `%APPDATA%\ATAS\PythiaGex\cadenas\{cadena,local}-NQ-*` y `%APPDATA%\ATAS\PythiaGex2\cadenas\`.
- `%APPDATA%\ATAS\PythiaGex4\{cboe,cinta,familia,viva,estela}`.
- Velas MNQ m2: `laboratorio/dom/ronda3/velas/velas-MNQ-m2.csv` y `laboratorio/tres/datos/velas_cache_2026-10-07/velas-MNQ-m2.csv`.
- Copias temporales: `%TEMP%\pg4_{clasica,clasica_dom,e2e,posiciones,replica20}`, `%TEMP%\pythiagex4_cboe_prueba_hilo` y `%TEMP%\fam_integrado_<día>`. Hoy existen las cinco `pg4_*` y la `pythiagex4_cboe_prueba_hilo`. Cada arnés las borra al arrancar: la versión ES tiene que usar `pg5_*` para no pisarlas.

---

### 8.1 adaptadores (B1)

**Qué prueba.** Compila `_contratos/Contratos.cs` y `AdaptadoresFamilia.cs` (`adaptadores/adaptadores.csproj:12-13`). Prueba siete cosas:
1. `RedondeoPy` contra `round(x,4)` de Python.
2. `LibroNqFamilia.ParsearViva3` contra `preview_niveles.foto_viva3`.
3. `CintaFamilia`: velas m2, `CierreConocido`, `Precio` y `PrecioSoloTick`, contra `velas_de_ticks`, `cierre_conocido`, `backtest_familia.Precio` y `tq_comun.precio_en`.
4. La persistencia `seg-*.bin`.
5. Las velas del gráfico como respaldo (casos sintéticos).
6. `ILibroNq` (dedup y orden).
7. El ciclo de vida.

Compara exacto (`Program.cs:21`, `Igual`), salvo los instantes fraccionarios, que difieren por diseño. La referencia es `ref_adaptadores.json` / `ref_adaptadores_2026-10-08.json`, que genera `ref_adaptadores.py`. Ese script importa `preview_niveles`, `backtest_familia`, `tres.velas` y `tq_comun` (`ref_adaptadores.py:18-26`).

**Datos de NQ**
- `ref_adaptadores.py:28-29`: `%APPDATA%\ATAS\PythiaGex3\viva\viva3-NQ-2026-10-07.jsonl` (DATO).
- `ref_adaptadores.py:63` y `Program.cs:90-93, 123-124`: `profundidad/estado/cinta/cinta-NQ-<día>[-relleno].csv`, días 2026-10-07 y 2026-10-08 (DATO).
- `ref_adaptadores.py:19-20, 26, 81`: importa `laboratorio/tres/tqqq/tq_comun`, solo por `precio_en`. Es del laboratorio, no del clon, así que se puede seguir usando.

**Constantes**
- `Program.cs:99, 107, 127-128, 145, 152, 156, 165, 179`: `new CintaFamilia("MNQZ6")` (DATO → "MESZ6").
- `Program.cs:58, 81, 196, 198, 226`: libro `"NQ"`.
- `Program.cs:223-224`: escribe `viva3-NQ-*.jsonl` en `tmp`.
- `Program.cs:158`: `>= 686` velas m2. Es la cuenta de la sesión NQ del 07-10 (DATO).
- `ref_adaptadores.py:50, 69`: ventana [día−2 h, día+21 h) (IGUAL).
- Precios sintéticos `Program.cs:154/159` (12345,25), 168 (100+k), 181 (500+…), 237 (20000+i): IGUAL, sin escala de producto.

**Para ES**
- Las partes 1, 5 y 7 se reusan cambiando "NQ" por "ES".
- Partes 2 y 6: se pueden alimentar con `%APPDATA%\ATAS\PythiaGex4\viva\viva3-ES-2026-10-27.jsonl` (78 líneas desde 01:51Z, de la 4.1.6g cargada en MESZ6 el 26-10). Otra opción es `%APPDATA%\ATAS\PythiaGex\viva\viva-ES-*.jsonl`: tiene el mismo encabezado `campos` que viva3 (`strike,dias,es_call,oi,iv,bid,ask,vol_hoy,vol_cinta,vol_compra,vol_venta,strike0`). **SIN VERIFICAR** que `ParsearViva3` lo acepte.
- Partes 3 y 4 necesitan **ticks de MES, y no hay**: `profundidad/estado/cinta` solo tiene 46 `cinta-NQ`. Hay que grabarlos con el clon (`PythiaGex5\cinta\seg-MESZ6-*.bin`) y hacer una `ref_adaptadores` de ES sobre una variante ES de `preview_niveles`.

### 8.2 cboe (B2)

**Qué prueba.** Compila `_modulos/cboe/*.cs` y los contratos (`cboe/PruebaCboe.csproj:15-16`). Modos:
- `py`: `round`/`repr` de Python.
- `crudos`: construir, medir y armar la línea flaca, igual byte a byte que `pythiagex.cadena_atas`, `base.medir` y `archivar_cadena.linea_flaca` (`comparar_cboe.py:22-23`).
- `lineas`: el lector `CboeArchivo.LeerDia` contra la copia de `foto_cboe_de_json`.
- `bajada`: re-comparación de la bajada real del 08-10 a las 23:20 UTC (`resultados/bajada`; la marca `BAJADA_HECHA.txt` impide repetirla).
- `hilo`: HTTP falso (dedup, archivo, relectura, seguidor por mutex, 3 fallos, 403, parar en ≤ 2 s).
- `historia`: opcional.

**Datos**
- `cboe/crudos/`: `_NDX-20260831-095812`, `_NDX-20261008-064032`, `_NDX-20261008-222535`, `QQQ-20261008-222540`, `TQQQ-20261008-{172310,222545}` (`.json.gz`).
- `cboe/archivo/`: `cadena-NQ-2026-10-{04,07,08}`, `cadena-QQQ-2026-10-{07,08}`, `cadena-TQQQ-2026-10-{07,08}` (copias de cboe-local).
- `cboe/crudos_hilo/{_NDX,QQQ,TQQQ}.json.gz`.
- Carpeta de trabajo `%TEMP%\pythiagex4_cboe_prueba_hilo` (`cboe/correr.ps1:18`; se borra si termina en `_prueba_hilo`, `Program.cs:228-229`).

**Constantes**
- `Program.cs:233`: los tickers `("_NDX","QQQ","TQQQ")` del HTTP falso.
- `Program.cs:249, 251, 408`: los libros NDX, QQQ y TQQQ.
- `Program.cs:281`: alias `"NQ"` y `"_NDX"` = NDX.
- `Program.cs:341, 360-361`: `ultima-NQ.json`, `cadena-NQ.ultimo`, `cadena-NQ-<día>`.
- `Program.cs:468-469`: espera los 3 tickers (`Hechos.Count == 3`, `Ultima("TQQQ")`).
- Mutex `Local\PythiaGex4.Prueba*` y `PruebaBajada` (`Program.cs:243, 310, 397, 457`; renombre a PythiaGex5).
- `Program.cs:265`: tope de 1250 KB/s con trozos de 64 KB (IGUAL).
- `comparar_cboe.py:159`: `RETRASO_S = 902` (IGUAL).
- `comparar_cboe.py:293`: `arch = {"_NDX": "NQ", "QQQ": "QQQ", "TQQQ": "TQQQ"}`.
- `Program.cs:175, 180`: números de `py` (30725,81; 238,36; aleatorios hasta 40000). IGUAL: cubren ES (~7.700) y SPY (~760).

**Para ES**
- `py`, `crudos` y `lineas` son genéricos: se reusan cambiando solo las entradas.
- Crudos disponibles:
  - `datos/cache/_SPX-*.json.gz`: 824 crudos de CBOE, del 2026-08-28 al 2026-09-03.
  - `datos/cache/SPY-20260831-052648.json.gz`: un solo crudo de SPY. Alcanza para el byte a byte; para variedad conviene bajar 2-3 más, fuera de la rueda.
- Archivos: `%APPDATA%\ATAS\PythiaGex\cboe-local\cadena-{ES,SPY}-*.jsonl.gz` (ver 8.17).
- `hilo`, `bajar`, `historia` y `modo_bajada` hay que rehacerlos:
  - Tickers `_SPX` y `SPY`; la cuenta pasa de 3 a 2.
  - Alias `"ES"`/`"_SPX"` = SPX.
  - Archivos `ultima-ES.json`, `cadena-ES.ultimo`, `cadena-ES-<día>`.
  - Mapa `{"_SPX": "ES", "SPY": "SPY"}`.
  - Una bajada real nueva por ticker, fuera de la rueda.
  - Re-medir tiempo y memoria de `historia`: SPX es más pesado (88 MB de NQ contra 112 MB de ES en los mismos 38 días de cboe-local; en la última foto del 26-10 hay 886 filas de SPX).

### 8.3 nq (B3a)

**Qué prueba.** `MinuteroNq` + `SeriesNq` (`familia/comun` y `familia/nq`), minuto a minuto de 22:00 a 20:59 UTC. Compara las 7 series visibles de NQ con `referencia/ref-<día>.json`. `--autoprueba` verifica `NumFam` contra `casos_num.txt` y las ramas de `OiNq`.

**Datos**
- `nq/Programa.cs:28`: `Raiz` absoluta.
- `nq/Programa.cs:34`: días 2026-10-07 y 2026-10-08.
- `nq/Programa.cs:43` y `LibroNqViva3.cs:31`: `%APPDATA%\ATAS\PythiaGex3\viva\viva3-NQ-<víspera|día>.jsonl`.
- `nq/Programa.cs:44` y `CintaCsv.cs:26`: `cinta-NQ`.
- Tolerancias (`Programa.cs:228, 230`): precio ≤ 0,01; monto exacto (1e-9).

**Constantes**
- `generar_casos_num.py:32`: `rs.normal(240, 3)`, la base de NDX (ESCALA: base ES−SPX ≈ 50).
- `generar_casos_num.py:34`: piso `0.0002*41.4`, la razón de QQQ (ESCALA: ES/SPY ≈ 10,0-10,1).
- `Programa.cs:109-110`: strikes sintéticos `31000+10·i` y fut `31100`. IGUAL: la lógica del OI no mira la escala.
- `Programa.cs:114-129`: salto del OI a las 01:30:10, supresión hasta las 02:00, espera hasta las 03:30, primera foto a las 02:10 (PRODUCTO, CME). **Medido hoy en `viva-ES`**: el OI de las opciones de ES saltó a las 01:58:51Z (15-09), 01:33:24Z (17-09) y 01:29:42Z (22-09). Es la misma franja que NQ (01:24-01:52, `posiciones/Program.cs:193`): **IGUAL** (3 noches).
- `Programa.cs:130-137`: posferiados 2026-09-08 y 2026-11-27 (IGUAL, calendario CME/NY).

**Para ES**
- La autoprueba se reusa tal cual. Solo cambian los dos pisos de `generar_casos_num.py`.
- La paridad necesita:
  - una referencia ES, que no existe;
  - libro ES: `viva-ES` 2026-09-07 a 2026-10-05, o `viva3-ES` del 27-10;
  - el `fut` por minuto (`Programa.cs:198`, `cinta.CierreConocido`). Sin cinta de MES, se puede armar un doble con velas MES m1 (`velas_cache_2026-10-07`, 09-07 a 10-05) o m2 (`ronda3`, hasta el 17-09).
- Sesiones recomendadas: 2026-09-21 y 2026-09-22 (después del roll; `viva-ES` de 11,8 MB cada una). Evitar la semana del 14 al 18-09: las weeklies van sobre U6.

### 8.4 ndx (B3b)

**Qué prueba.** `LibroMinuteroNdx` (comun + fam + ndx) contra la referencia, en las 6 series NDX (`ndx/Program.cs:21`). Modos y casos:
- paridad y corregido (`ContratoGrafico = "MNQZ6"`, 82);
- reinicio con `muestras-NDX-<día>.jsonl` (86-100);
- replay con reinicios a las 03:00 y 15:00 UTC (184);
- protocolo `NuevaTanda`;
- sensibilidad;
- prueba G de invierno (243-274).

**Datos**
- `ndx/entrada/cboe-NDX-2026-10-0{7,8}.jsonl.gz`: 11,5 MB cada uno, congelados (`DobleCboe.cs:46`). Se armaron como `cargar_cboe('NQ', día−6..día)` desde:
  - `%APPDATA%\ATAS\PythiaGex\cadenas`, `PythiaGex2\cadenas` y `PythiaGex\cboe-local` (`DobleCboe.cs:65-66`);
  - `local-NQ-<día>.jsonl` (`DobleCboe.cs:90-91`);
  - ticker de archivo `"NQ"` (`DobleCboe.cs:72`);
  - corte `generado ≤ día 21:00Z` (`DobleCboe.cs:78`).
- Cinta: `cinta-NQ` de 6 días previos más la sesión (`DobleCinta.cs:44, 159-160`).
- Velas MNQ m2 (`Program.cs:51-52`).

**Tolerancias** (`Program.cs:364-371`): precio ≤ 0,01; monto ≤ 0,1. "Strike distinto" si |dp| > 1 en MUROS/MAJORS, porque "los strikes de NDX están a ≥ 5 pts". **Medido hoy**: SPX va de 5 en 5 pts dentro de ±3 % (92 strikes en la foto del 26-10), así que el criterio sirve para SPX.

**Constantes**
- Libro `"NDX"`: `DobleCboe.cs:38-40, 165, 227` y `Replay.cs:15, 21, 26`.
- `DobleCboe.cs:26`: `RETRASO_S = 902` (IGUAL).
- `Replay.cs:23`: `HorasConFilas = 30` (IGUAL).
- `DobleCboe.cs:85`: ventana [d−2 h, d+21 h] (IGUAL).
- `DobleCinta.cs:76, 82, 105`: `Precio` con tick ≤ 120 s o vela m2 interpolada; `CierreConocido` NaN si pasan más de 4 h (IGUAL).
- `Program.cs:243-274`: vencimientos `Z6` 2026-12-18 14:30Z, `H7` 2027-03-19 13:30Z, `U6` 2026-09-18 13:30Z; roll = tercer viernes − 8 días. IGUAL para ES (mismo vencimiento trimestral de CME a las 9:30 NY), **SIN VERIFICAR** en el código del clon.
- Cotización congelada a las 15:59 EST (266-267): SPX igual, según el pedido.
- `Program.cs:270-274`: códigos "MNQZ6"/"MNQ" (DATO → "MESZ6"/"MES").

**Para ES (arnés "spx")**
- `SERIES` con `*_SPX_*`; `DobleCboe` con ticker `"ES"` y libro `"SPX"`; congelar `cboe-SPX-<día>`.
- `DobleCinta` necesita el precio del MES en `ts − 900 s` para la base sincronizada. Sin ticks de MES, hay que adaptarla a velas m1 o m2 de MES.
- La prueba G se reusa cambiando "MNQZ6" por "MESZ6".

### 8.5 qqq (B3c)

**Qué prueba.** `MinuteroQqq` + `SeriesQqq` contra la referencia (`MAJORS_QQQ_vol/oi`, `ZEST_QQQ_vol`), en modo paridad y modo corregida. Además:
- la fuente del último minuto: razón 41,4332, dato 20:14:59Z, vigente hasta las 13:30Z, congelada, OI fresco (`Programa.cs:165-178`);
- VIVO: fotos de a una, cinta hasta t+6 s, también con +400 ms (`ExtrasQqq.cs:74-91`);
- REINICIO con muestras (`ExtrasQqq.cs:93-123`);
- HORA (`ExtrasQqq.cs:125-166`).

**Datos**
- `FuenteCboeArchivoQqq.cs:40, 52-53`: QQQ de `PythiaGex\cadenas`, `PythiaGex2\cadenas`, `cboe-local` y `local-QQQ`, de los 6 días previos más el día (44).
- `CintaArchivoQqq.cs:47, 108-109`: `cinta-NQ`.
- `CintaArchivoQqq.cs:39-40`: velas MNQ m2.
- Tolerancias: precio ≤ 0,01 con redondeo a 2; monto 1e-9 (`Programa.cs:133-138`).

**Constantes**
- `Programa.cs:69` y `ExtrasQqq.cs:76`: libro y ticker `"QQQ"`.
- `FuenteCboeArchivoQqq.cs:141`: retraso 902 (IGUAL).
- `Programa.cs:73` y `ExtrasQqq.cs:142`: "MNQZ6" (DATO).
- `ExtrasQqq.cs:143`: `ContratoTablaPython`, U6 hasta la sesión del 14-09, después Z6, después H7 (en el módulo: `ConversionQqq.cs:52-58`). Es la continua MNQ de ATAS; la de MES está **SIN VERIFICAR**.
- `ExtrasQqq.cs:133-136`: C9 congelada hasta las 13:30Z (verano) o 14:30Z (invierno) (IGUAL).
- `ExtrasQqq.cs:137`: vigencia de +25 min si no está congelada (IGUAL).
- `ExtrasQqq.cs:139-140`: pausa de CME 21:00-22:00Z (IGUAL).
- `ExtrasQqq.cs:149-152, 160-162, 172`: spot QQQ 700 y NQ 29000/29100 (ESCALA sintética). Para SPY ~760 y ES ~7.700 la razón realista es ~10,1, no ~41.

**Para ES (arnés "spy")**
- SPY en cboe-local (16-09 a 27-10), en `cadenas` (14-09 a 17-09) y en `PythiaGex2` (17 y 18-09).
- Mismo comportamiento after-hours que QQQ (pedido). La razón necesita el precio del MES en el último trade de SPY (16:14:59 NY): sin ticks, sale de velas.
- Hace falta la referencia ES.

### 8.6 fam (B3d)

**Qué prueba (siete modos)**
- `reglas`: `ReglasFam` contra `fam/datos/casos-reglas.json` (12,6 MB), que arma `gen_casos_reglas.py` con 600 casos y el Python real.
- `tres`: `AlmacenTres` contra `ref.tres`.
- `historia`: `SalidaFamilia` contra `ref.salida`.
- `motor`: `MotorFamilia` con dobles y las estelas reales copiadas.
- `invierno`.
- `finde`: viernes 17:00 NY a domingo 18:00 NY (`Program.cs:487-611`).
- `gm`: unidad, lectura, real (`datos/gm`) y nq (`Gm.cs`).

**Datos**
- `Program.cs:45`: `%APPDATA%\ATAS\PythiaGex3\estela`.
- `Program.cs:35`: días 07 y 08-10.
- `fam/datos/gm/{cadena-NQ,cadena-QQQ}-2026-10-09.jsonl.gz`, `estela-{NDX,QQQ}-2026-10-09.jsonl` y `niv-2026-10-09-MNQZ6.jsonl`. Son copias de `%APPDATA%\ATAS\PythiaGex4` con corte 02:40Z (`gen_datos_gm.py:15-18, 30, 36, 44, 50`).
- `Gm.cs:369`: `estela-NQ-2026-10-08`.
- `verificar_leer_estela_gm.py:10, 13, 16`.

**Constantes de `gen_casos_reglas.py`**
- 38-48: paso de strike NQ {5, 10, 25, 50}, NDX {5, 10, 25}, QQQ {0,5; 1} (PRODUCTO; ES 5, SPX 5, SPY 1, medido el 26-10).
- 39: corrimiento NQ 0 o {−35,5; 41,25; 33}.
- 44: base NDX {241,5; 242,5; 238,75} o U(150, 300) (ESCALA; ES−SPX ≈ 50).
- 47: razón QQQ U(41,20; 41,60) (ESCALA; ES/SPY ≈ 10,1).
- 49: ventana ±4,5 %.
- 52: escalas de GEX `{"NQ": 2e8, "NDX": 4e7, "QQQ": 6e8}` (ESCALA).
- 68: islas con umbral del 10 % (IGUAL).
- 79-80: zero ±180/±250 pts (ESCALA).
- 81: `oi_ok` solo para NQ.
- 92: fut U(24000, 32000), tick 0,25 (ESCALA).

**Constantes de `Dobles.cs`**
- 28: `Mult = lb == "NQ" ? 20 : 100` (PRODUCTO: ES = 50).
- 24: `PorRazon = lb == "QQQ"` (→ SPY).
- 54: `DatoUtc` −20 s para NQ y −930 s para CBOE (IGUAL).
- 29: `ConvTexto`.
- 80: el `Estado` falso menciona TQQQ.
- 90-106: `TqqqFalso` con s 124,213 y c 21012,058 (TQQQ).

**Constantes de `fam/Program.cs`**
- 300: strike de los actuales: QQQ por razón (`x/conv`), el resto por base (`x − conv`). → SPY por razón; SPX y ES por base.
- 173-175: bordes de `RoundPy` (31444.025…) (IGUAL).
- 347, 380, 554, 607: "MNQZ6" (DATO).
- TQQQ: 344, 430, 445-451 (exige 4 actuales de TQQQ: `tqAct != 4`), 549, 584.
- 471-479 y 507-541: horario de CME Globex (sesión 22:00-21:00Z en verano, 23:00-22:00Z en invierno, rueda 13:30/14:30Z, cierre del viernes 17:00 NY, apertura del domingo 18:00 NY) (IGUAL para ES).

**Constantes de `Gm.cs`**
- 49-65: `MontoDominante3`/`FactorMonto3`: NQ ×0,2, NDX y QQQ ×1, **"ES" → NaN (61, 65)**. En el clon ES = 50/100 = 0,5 (PRODUCTO; el núcleo 3.0 usa `MULT = 100` también para el futuro, `extraer_dominantes_clasica.py:91`; **SIN VERIFICAR** en el código del clon). Estos tests se invierten.
- 103: `Parsear` descarta `d < 15000` (ESCALA crítica; en el módulo, `AlmacenTres.cs:136`).
- 91: `LINEA_NDX` con 31038,23 y `bs` 241,83 (ESCALA, dato de prueba).
- 236: capa, archivo y porRazón: `("NDX","NQ",false)` y `("QQQ","QQQ",true)`.
- 245-246: núcleo de las capas con 3 dominantes, radio min(2 %, 100 pts), empate del 20 %, centroide ±12 pts, noche por volumen (ESCALA).
- 283, 311, 315: tolerancias Fut ≤ 0,011, g ±1, strike 0,006, monto ≤ 5 % o ±0,5 M.
- 380, 386, 399: modo nq con rueda 13:30-20:00Z, g ≥ 5, ≥ 100 pares, mediana en [0,95; 1,05].

**Para ES**
- `reglas`: hay que rehacer `gen_casos_reglas.py` con libros ES/SPX/SPY sobre el Python de la familia ya portado a ES (`backtest_familia.py:152, 158`).
- `motor`, `invierno` y `finde`: se reusan con dobles, cambiando a "MESZ6" y sacando TQQQ.
- `gm` unidad y lectura: se reusan con números de ES (factor 0,5 y umbral nuevo).
- `tres`, `historia`, `gm` real y `gm` nq: **bloqueados**. No hay referencia ES ni estelas 3.0 de ES, salvo `PythiaGex4\estela\estela-ES-2026-10-27.jsonl` (895 líneas desde 01:50Z).

### 8.7 fam/integrado

**Qué prueba.** El motor fam con los minuteros reales de NQ, NDX y QQQ y sus dobles (compila `nq/CintaCsv.cs`, `nq/LibroNqViva3.cs`, `ndx/DobleCinta.cs`, `ndx/DobleCboe.cs`, `qqq/CintaArchivoQqq.cs` y `qqq/FuenteCboeArchivoQqq.cs`; `fam/integrado/fam_integrado.csproj:25-27`). Compara contra la referencia las 21 series. Es la única prueba de `FAM_MUROS_*` y `CONF_vol` sobre datos reales (`Integrado.cs:3`). También compara la historia de la foto (24 series) y los actuales, sin TQQQ (198).

**Datos**
- Los de nq, ndx y qqq.
- `ndx/entrada` (`Integrado.cs:37, 60-61`).
- Copias de `PythiaGex3\estela` a `%TEMP%\fam_integrado_<día>` (79-87).

**Constantes**
- `Integrado.cs:89`: "MNQZ6".
- `Integrado.cs:73`: "QQQ".
- `Integrado.cs:137-140`: tolerancias 0,01 y 0,1.
- `Integrado.cs:41`: días.

**Para ES.** Lo mismo que 8.3-8.5 juntos. Es la prueba más valiosa una vez que existan la referencia ES y los tres minuteros de ES.

### 8.8 tqqq (B4): se borra entero

**Qué tiene.** Compila `_modulos/tqqq/*.cs` (`ArnesTqqq.csproj:13`) y `modulo_solo/TqqqSolo.csproj`.
- `Program.cs:39, 48-51` y `DoblesTq.cs:4, 145`: cboe-local `cadena-TQQQ` y `cadena-NQ` de los días 2026-10-05 a 2026-10-08.
- `Program.cs:40, 53-54`: `cinta-NQ` de los días 07 y 08.
- `Program.cs:58-59`: `referencia/tqqq_vivo-*.json` (`gen_ref_tqqq.py:9, 34`).
- `Program.cs:190, 232`: NQ_1600 = 31396,5 y 30976,25.
- `Program.cs:167`: "MNQZ6".
- `Program.cs:238-239`: `tqqq-cierres.json`.

**Qué hay que hacer.** Borrar la carpeta y `correr_todo.ps1:37`.

### 8.9 posiciones (B-pos 4.1.2)

**Qué prueba.** `CambiosFamilia` con datos reales:
- P1: saltos de OI. P1b: libro flaco (TQQQ).
- P2: monto reconstruido. P3: linealidad. P3c: lados que se anulan.
- P4: cadena congelada. P5: volumen reiniciado. P6: un caso a mano.
- P7: FAM. P8: TQQQ. P9: tiempo. P10: la hora del dato. P11: la apertura.
- SIM "esta noche".

Compila TQQQ: `posiciones.csproj:28-30` (`TqqqPerfil`, `TqqqSesion`, `TqqqUtil`).

**Datos.** Copias de `%APPDATA%\ATAS\PythiaGex4\{familia\niv-2026-10-09-MNQZ6.jsonl, viva\viva3-NQ-2026-10-0*.jsonl, cboe\cadena-*/ultima-*/cierres.json}` a `%TEMP%\pg4_posiciones` (`Program.cs:73-92`). El corte es `hasta = 2026-10-09 08:16Z` (65).

**Constantes**
- `Program.cs:193`: ventanas de publicación del OI, en horas UTC: NQ (0,5; 3,0), NDX (6,0; 7,0), QQQ (7,0; 9,5), TQQQ (5,0; 13,5) (PRODUCTO; ver 8.18).
- `Program.cs:197`: saltos esperados de NQ a las 01:24, 01:30 y 01:48 (DATO).
- `Program.cs:266-267`: "uno por día hábil de NY antes de las 09:30 NY".
- `Program.cs:527, 603, 617, 649`: `Escala = 0.2` para NQ (PRODUCTO; ES 0,5).
- `Program.cs:695`: aporte `g·ΔVol·100·S²·0,01·0,2/1e6` (PRODUCTO).
- `Program.cs:736`: `Mult = lb == "NQ" ? 20 : 100` (ES = 50).
- `Program.cs:733, 741, 830, 870`: QQQ ×conv, el resto +conv; `PorRazon` QQQ.
- `Program.cs:525-532, 662-666`: strikes sintéticos 31160 y 31150, S 31146,5, muro C 31100 (ESCALA).
- TQQQ: `Program.cs:120, 199, 289-346, 856-923`.

**Para ES**
- Necesita datos del clon corriendo al menos una noche: `PythiaGex5\{familia\niv-<día>-MESZ6, viva\viva3-ES, cboe\cadena-ES/SPY}`.
- Ventanas: ES (0,5; 3,0) igual que NQ, medido; SPX y SPY nuevas, ver 8.18.
- Escala 0,5; P6 rehecho con números de ES; sacar P1b, P8 y la parte TQQQ de P11.

### 8.10 replica20 (4.1.3)

**Qué prueba.** `Replica20`, con las variantes A-E, contra `laboratorio/calibracion_1009/receta_2_0/paridad_2_0_minuto.csv`: los AUDIT de la 2.0 del 08-10 22:00 al 09-10 03:46 UTC (`Program.cs:144, 249-251`). Además: sin efectos, persistencia, completar, cambios, etiqueta, razón rehecha y rótulos.

**Datos.** Copias a `%TEMP%\pg4_replica20` (108, 138-142) de:
- `PythiaGex4\cboe` y `PythiaGex\cboe-local` (`cadena-NQ`/`QQQ`, días 07 a 09-10);
- `PythiaGex4\cinta\seg-MNQZ6-2026-10-0?.bin`;
- `PythiaGex4\familia\muestras-*.jsonl`.

Compila TQQQ: `replica20.csproj:30-32`.

**Constantes**
- `Program.cs:46`: `("NDX","NQ")` y `("QQQ","QQQ")`.
- `Program.cs:92`: `SESION = 2026-10-09`. `Program.cs:96`: `DIAS_CBOE` (DATO).
- `Program.cs:147, 376, 411`: `CintaFamilia("MNQZ6","NQ")`.
- "MNQZ6": `Program.cs:155-156, 381, 400, 514, 521, 527`.
- Tolerancias: 0,02 pt y 1 M (241); razón 5e-7 (485); base 0,005 (492).
- `Program.cs:392-395, 667`: razón 41,44466733533935 (= 30990,25/747,75) y K750 (DATO).
- Receta `extraer_paridad_2_0.py`: `AUDIT capa=NDX/QQQ` y PRIMARIA de `pythiagex2-gammahoy.log` (27, 211-219), cadenas QQQ/NQ (252), niv MNQZ6 (271), radio min(2 %, 100) (150), pico 0,35 % (160), `MULT = 100` (39), tolerancias 0,02/1 M (302).

**Para ES: no hay contra qué comparar.**
- La 2.0 en un gráfico de ES dejó solo 5 AUDIT primarios (2026-10-05, `origen=medida`) y un arranque `raiz=ES` ("libro efectivo=CBOE_SPX/ES").
- Sus 5 "capa=SPX" y 1 "capa=SPY" (18 y 20-09) son sobre NQ: fut 29.7xx, razón 3,90-3,92.
- La paridad 1 exige correr la 2.0 en un gráfico de MES al menos una sesión, con su log de AUDIT. Las partes 2-8 se reusan con datos del clon.

### 8.11 clasica (4.1.5)

**Qué prueba.** `ClasicaNdx` (`R10_NDX_zero`) contra los AUDIT `capa=NDX` de la clásica (`receta_clasica/paridad_clasica_ndx_minuto.csv`, 910 renglones, 03:00-19:02 UTC del 09-10) y contra las líneas "base muestra" de `%APPDATA%\ATAS\pythiagex-gammahoy.log`. Ese log está en hora ART = UTC−3: el regex está en `Program.cs:197-198` y el ajuste de +3 h en la 220.

**Datos**
- `Program.cs:238-242`: `PythiaGex4\cboe\cadena-NQ` (02 a 09-10) y `cadena-QQQ` (07 a 09-10), `seg-MNQZ6` y muestras.
- `Program.cs:252-262`: `cboe-local\cadena-NQ` y `PythiaGex\cadenas\local-NQ` (08 y 09-10).
- `Program.cs:244-245, 300, 302`: la receta (`paridad_clasica_ndx_minuto.csv`, `base_rueda_por_dia.csv`, `velas_clasica_ndx_m1.csv`).
- Todo se copia a `%TEMP%\pg4_clasica` (122).

**Constantes**
- `Program.cs:105`: `TOL = 0.05`.
- `Program.cs:348-349, 388-391`: base 243,06 adoptada el 2026-10-08 a las 20:12:06 UTC (`base-rueda-NQ.json`), con tope de 1 pt (DATO).
- `Program.cs:110`: `T_OPERADOR = 2026-10-09 17:10Z`.
- `Program.cs:362`: `CierreVela(ts − 960 s)` (IGUAL).
- `Program.cs:401`: variante `VelaMin = 1`.
- "MNQZ6": `Program.cs:288, 297, 511, 517-518, 525, 657-658`.
- `Program.cs:704-705`: rótulo "Clasica NDX".
- Receta `extraer_paridad_clasica.py`: `capa=NDX` (260), `cadena-NQ` (298-299, 328), centinela MNQZ6 (50-51), `MULT = 100` (59), radio 2 %/100 (62), centroide ±12 (63).

**Para ES.** La clásica en un gráfico de ES no tenía capa SPX. Su libro primario era el ES de Rithmic o, de respaldo, SPX de CBOE con base: `AUDIT fut=… S=… base=… origen=medida|medida_hace|de_la_rueda_hace|TEORICA_carry|CRUDA`.
- Con la clásica congelada (1.11d desde el 18-09) hay unos 450 AUDIT con libro SPX y base en escala ES: 18-09: 97; 20-09: 5; 21-09: 33; 22-09: 9; 25-09: 2; 04-10: 185; 05-10: 119.
- Hay 54 "base muestra" de SPX el 05-10.
- `%APPDATA%\ATAS\PythiaGex\base-rueda-ES.json` = 52,42 (2026-10-05T17:20:10Z, 23 muestras).
- Material disponible: `local-ES` (07-09 a 10-10), cboe-local `ES` (desde el 16-09) y velas MES m1 hasta el 05-10 a las 20:59Z. No hay ticks.
- Se puede hacer una versión ES para el 04 y 05-10, pero hay que rehacer el lector de AUDIT (formato PRIMARIA) y la receta. La muestra es chica.

### 8.12 clasica_dom (4.1.5b)

**Qué prueba.**
- A: cada minuto de `niv-*-MNQZ6` que tenga `R10_NDX_dom`: el C# contra el port Python.
- B: la rueda simulada con base 243,06 contra los AUDIT `capa=NDX` del 09-10.
- C: casos borde.

**Datos**
- `Program.cs:153`: `PythiaGex4\familia\niv-*-MNQZ6.jsonl`.
- `Program.cs:173`: `PythiaGex4\cboe\cadena-NQ`.
- `Program.cs:178`: el log de la clásica.
- `Program.cs:195, 202`: cboe-local `cadena-NQ` y `local-NQ`.
- `%TEMP%\pg4_clasica_dom` (88).
- `Program.cs:222`: port `laboratorio/calibracion_1009/receta_clasica_dominantes/dominantes/extraer_dominantes_clasica.py`.
- `Program.cs:315`: `audit_dominantes_lineas.csv`.

**Constantes**
- `Program.cs:75`: `DIA_AUDIT = 2026-10-09`.
- `BASE_RUEDA = 243.06`: `Program.cs:76` y `ref_dominantes.py:37`; filtros en `Program.cs:188` y `ref_dominantes.py:139`.
- `Program.cs:324`: `v[iB] == "NDX"`.
- `Program.cs:412`: rueda 13:30-20:00Z.
- `Program.cs:541` y `ref_dominantes.py:208-210`: C7 con radio min(2 %, 100 pts), ±100 adentro y ±105 afuera (ESCALA).
- `ref_dominantes.py:190`: C1, +150 fuera del radio.
- `ref_dominantes.py:163-164`: `B = 243.0`, `FUT = 31093.0` (ESCALA).
- Encabezado de `Program.cs:12-17`: tolerancias 0,006 pt / 0,51 M y C# = Python a 1e-6.
- Port: `RADIO_PCT 2.0` y `RADIO_MAX_PTS 100` (75-76), `RADIO_CENTROIDE 12` (80), `PICO_RADIO_PCT 0.35` (81), `EXP_FUT_UTC` MNQZ6 2026-12-18 13:30Z (86), `CAPAS_MAJORS_RADIO_PCT 1.0` (89), `MULT 100` (91).

**Para ES.** El mismo bloqueo que la clásica para A y B. C se reusa con números de ES (B ≈ 52, FUT ≈ 7.700). Con ES, el 2 % ≈ 154 pts, así que el tope de 100 manda: el test C7 depende del radio que decida el clon (ESCALA, otro agente).

### 8.13 pantalla (B5): `atas/PythiaGexCuatro/_modulos/pantalla/prueba`

**Qué prueba.**
- A: propiedades contra el visor 4.0.6 (`_visor_4_0_6/FamiliaCuatro.cs`, `Program.cs:154`), contra `grupo_arriba_415e.py` (120) y contra la DLL real `bin/Release/PythiaGexCuatro.dll` (135).
- B: catálogo.
- C: armado (etiquetas, pestaña, doble eje, vencimiento, carteles).
- D: render. E: rendimiento. T: rótulos. R: revisión.

**Datos.** `datos/niv-2026-10-09-MNQZ6-hasta-0544Z.jsonl` (738 KB, real).

**Constantes**
- `Program.cs:566-568`: `Venc("MESZ6") == ""`. El parser `ReVencNq` del módulo solo entiende NQ (`ArmadoPantalla.cs:262`).
- `Program.cs:644-645`: C36, "gráfico de ES: cartel naranja y nada más" (en el módulo, `ArmadoPantalla.cs:750-751`, `instr.IndexOf("NQ")`). En el clon se invierte.
- TQQQ:
  - `Program.cs:328`: `EjeFamilia4.TQQQ` por defecto.
  - `Program.cs:593-597`: eje TQQQ ×3 ("NQ = 124,21 × TQQQ + 21.012,1").
  - `Program.cs:606-607`: fuentes "NQ Rithmic, NDX/QQQ/TQQQ CBOE".
  - `Program.cs:614`: "libro NQ por Rithmic + NDX/QQQ/TQQQ".
  - `Program.cs:674-678`: el eje TQQQ en dos pruebas más.
- `Program.cs:576-587, 727-730, 746-776`: precios y montos NQ (31.0xx) (ESCALA, datos de prueba).
- `Program.cs:175, 365`: A21/A22 esperan 101 casillas: desactualizado frente a la 4.1.6g.

**Para ES**
- Rehacer A: no hay visor ES; la referencia pasa a ser la primera GammaCinco instalada.
- Rehacer C1 y C33-C36: MESZ6/ESZ6 dibujan; NQ da cartel.
- Sacar TQQQ, también de `Dobles.cs` (8 referencias).
- Regenerar `datos/niv-<día>-MESZ6` con el clon.

### 8.14 ws_compat (4.1.5c/e)

**Qué prueba.** Compara por reflexión la clase `PythiaGexCuatro.FamiliaCuatro` (`Program.cs:101`) de `atas/PythiaGexCuatro/bin/Release/PythiaGexCuatro.dll` (78) contra `ws_compat/referencia/PythiaGexCuatro-4.1.5.dll` (79).
- `SHA_415 = 88023542542e250e0766f3144d35a5954d9c85d3e930f8b5376faac9f4dd83b0` (47). Lo verifiqué hoy con `sha256sum`: coincide.
- `VERSION "4.1.5"` (205).
- W2 = la 4.1.5 más `S_R10_NDX_dom` (218).
- W5: `S_R10_NDX_dom` es bool y por defecto True (238-240).
- W6-W8 contra `herramientas/grupo_arriba_415e.py` (80). Ese script tiene una lista de 101 nombres `S_*_NQ_*`, `S_*_NDX_*`, `S_*_QQQ_*` y `S_T_*` (45-57), apunta a `atas/PythiaGexCuatro` y a `CatalogoFamilia.cs` (38-39) y busca `"partial class FamiliaCuatro"` (395).

**Por qué no aplica tal cual al tipo nuevo `PythiaGexCinco.GammaCinco`**
- Ningún .ws del operador tiene guardado ese tipo: en la primera instalación no hay compatibilidad que proteger. Comparar contra la 4.1.5 marcaría cada propiedad renombrada (NQ→ES, NDX→SPX, QQQ→SPY) y cada borrada (`S_T_*`), y `GetType("PythiaGexCuatro.FamiliaCuatro")` falla.
- La referencia tiene que ser la DLL de la primera GammaCinco instalada, con su propio sha256. Desde ahí vale la misma guarda: no renombrar nada después de instalar.
- `grupo_arriba_415e.py` está atado a PythiaGexCuatro y a los nombres de NQ: el clon necesita su propia copia, sin TQQQ.
- La 4.1 fija `AssemblyVersion 4.0.0.0` para cargar el .ws del visor (`PythiaGexCuatro.csproj:14-17`). El clon puede elegir la suya, pero tiene que quedar fija desde la primera instalación, porque el .ws guarda "Tipo, Ensamblado, Version=".

### 8.15 e2e (punta a punta, informativo)

**Qué prueba.** Encadena las piezas reales como `HostFamilia` (`HostE2E.cs`) sobre `%TEMP%\pg4_e2e` (`Program.cs:73`).
- Siembra: `cinta-NQ` del 07 y 08-10 (113), `viva3-NQ` de `PythiaGex3` (116), cboe-local NQ/QQQ/TQQQ de 8 días más `ultima` y `.ultimo` (117-126), estelas de la 3.0 (128-131).
- Reloj: 2026-10-08 21:59:12Z (46). `INI`/`FIN` en la 47. `CODIGO = "MNQZ6"` en la 48.
- Compara contra `ref-2026-10-08.json` y `tqqq_vivo-2026-10-08T215912Z.json` (329-341).
- Escenarios (`correr.ps1:4`): ny, ny-reinicio, ny-recalculo, utc, ny-union, ny-vivo, ny-extras.
- `Main` siempre devuelve 0 (`Program.cs:425`). En la última corrida, 'ny' dio 7.604 series-minuto distintas, todas explicadas por la siembra: en 'ny-union' las 21 series salen IGUALES (`e2e/resultados/e2e-ny-union.txt:161`).

**Renombres.** Logs `pythiagex4-*.log` (`HostE2E.cs:83, 105, 126, 226`), mutex `Local\PythiaGex4.e2e.<pid>` (`Program.cs:191`) e hilo `"PythiaGex4 familia"` (`HostE2E.cs:141`).

**Para ES.** Sirve como humo una vez que el clon haya grabado sus propios datos en `PythiaGex5`. Sin referencia ES solo puede comparar corridas entre sí.

### 8.16 referencia: cómo se genera para ES

No existe. Antes hace falta una variante ES de `backtest_familia` / `preview_niveles` / `extremos_rebote.leer_estela` / `fuentes_3_0.velas_cinta` (ver 8.0), con:
- libros ES/SPX/SPY y `MULT` {ES 50, SPX 100, SPY 100};
- `V.cargar("MES", …)`;
- cinta MES;
- `viva3-ES`;
- estelas ES/SPX/SPY;
- umbral de `leer_estela` en escala ES.

Después, `generar_referencia.py` se reusa cambiando los días.

### 8.17 Datos de ES disponibles hoy (verificado el 27-10 con ls y lectura de archivos)

**SPX (clave de archivo "ES")**
- `%APPDATA%\ATAS\PythiaGex\cboe-local\cadena-ES-*.jsonl.gz`: 38 archivos, del 2026-09-16 al 2026-10-27, 112 MB.
  - Faltan el **18-09 (viernes del vencimiento trimestral U6)** y los sábados 19-09, 26-09 y 24-10.
  - El 26-10 tiene 377 líneas, de 00:04Z a 23:11Z; spot_idx 7623,68 en la primera línea.
- `%APPDATA%\ATAS\PythiaGex\cadenas\cadena-ES-*.jsonl.gz`: 42 archivos, del 2026-08-19 al 2026-10-07, 127 MB. El del 19-08 tiene `fuente "databento OPRA"` y `retraso_s`.
- `cadenas\local-ES-*.jsonl`: 25 archivos, del 07-09 al 10-10, 2,1 GB sin comprimir.
- `%APPDATA%\ATAS\PythiaGex2\cadenas`: `cadena-ES` del 17, 18 y 20-09 y del 01, 02, 04 y 05-10; 4 `local-ES`.
- Crudos: `datos/cache/_SPX-*.json.gz`, 824 archivos del 28-08 al 03-09.

**SPY**
- cboe-local `cadena-SPY`: 38 archivos, mismas fechas que ES, 64 MB.
- `PythiaGex\cadenas\cadena-SPY`: 14 a 17-09.
- `local-SPY`: 15, 16, 17 y 29-09.
- `PythiaGex2`: `cadena-SPY` del 17 y 18-09.
- Crudo: `datos/cache/SPY-20260831-052648.json.gz`, uno solo.

**Libro ES de Rithmic**
- `%APPDATA%\ATAS\PythiaGex\viva\viva-ES-*.jsonl`: 15 archivos, 83 MB.
  - Días: 07, 08, 09, 11, 13, 14, 15, 16, 17, 18, 20, 21, 22 y 25-09, y 05-10.
  - Los grandes, de 5 MB o más: 08, 11 y 14 a 18-09, y 21 y 22-09.
  - Lo escribió la clásica 1.x, antes de la 8.0.15. Tiene el mismo `campos` que viva3.
- `%APPDATA%\ATAS\PythiaGex4\viva\viva3-ES-2026-10-27.jsonl`: 78 líneas, de la 4.1.6g cargada en MESZ6 (`pythiagex4-gammahoy.log:65809, 66485`).
- No hay `viva3-ES` en `PythiaGex3`.

**Precio del MES**
- **No hay cinta de ticks de ES ni de MES.**
- `laboratorio/dom/ronda3/velas/velas-MES-m2.csv`: del 2026-08-06T22:00 al 2026-09-17T20:58.
- `velas-ES-m2.csv`: del 08-06 al 09-02.
- `velas-MES-m1.csv`: del 08-03 al 09-07.
- `laboratorio/tres/datos/velas_cache_2026-10-07/velas-MES-m1.csv`: del 09-07T22:00 al 10-05T20:59. No hay m2 de MES ahí.
- `laboratorio/dom/ronda6/S_gamma_libro/medicion/cache/seg-MES-*.parquet`: 7 días, del 08 al 17-09.

**Estelas en escala ES**
- Solo `%APPDATA%\ATAS\PythiaGex4\estela\estela-ES-2026-10-27.jsonl` (895 líneas).
- Las `estela-ES/SPX/SPY` de `PythiaGex` y `PythiaGex2` están en **escala NQ** (29.xxx): son capas dibujadas sobre NQ.

**Logs que podrían servir de referencia**
- La clásica en gráfico de ES, del 07-09 al 05-10: 13.522 AUDIT primarios con fut < 15000. Por origen: medida 4858, libro Rithmic ES 3818 + 1075, de la rueda 1419, carry 1051, medida_hace 860, CRUDA 441.
- Sus 9.649 `capa=SPX` y 1.134 `capa=SPY` tienen todos fut > 15000: son de NQ y no sirven.

**Sesiones con todo junto (libro ES + SPX + SPY + precio MES)**
- 16-09 y 17-09: semana del roll, trampa.
- 21-09 y 22-09: sin ticks; precio solo de velas m1.
- Parcial: 25-09 y 05-10.
- Ninguna sesión tiene estela 3.0 de ES. Para TRES/historia hay que grabar con el clon.

### 8.18 Horario de publicación del OI, medido hoy

Sirve para `OiNq` (8.3) y para la P1 de posiciones (8.9). Criterio: el primer instante en que ≥ 30 % de las claves (strike, vencimiento) cambian de OI entre fotos seguidas.
- **ES, opciones de CME** (`viva-ES`): 01:58Z el 15-09, 01:33Z el 17-09 y 01:29Z el 22-09. Es igual que NQ: la ventana (0,5; 3,0) sirve.
- **SPX** (cboe-local "ES"): 13:07Z el 20-10, 07:01Z el 21-10, 07:01Z el 22-10 y 13:03Z el 23-10; el 26-10, nada. Ventana de unas (6,5; 13,5).
- **SPY:** 13:07Z el 20-10, 13:02Z el 21-10, 07:46Z el 22-10 y 13:03Z el 23-10. Ventana de unas (7,0; 13,5).
- **NDX, como control:** de 06:36Z a 06:43Z del 20 al 26-10. Coincide con (6,0; 7,0).
- **QQQ:** de 13:02Z a 13:07Z del 20 al 26-10. Queda **fuera** de la ventana (7,0; 9,5) de `posiciones/Program.cs:193`, que se midió del 01 al 08-10 (07:31-09:12). **SIN VERIFICAR** qué usa hoy el módulo en vivo. Hay que avisarlo al inventario principal.

### 8.19 Resumen: qué se conserva, qué se adapta y qué se tira

**Imprescindibles para el clon ES**
- cboe: con SPX y SPY.
- adaptadores: con MESZ6 y ES.
- nq→es, ndx→spx, qqq→spy y fam/integrado: son la paridad de cada libro y de la familia. Todos esperan una referencia ES que no existe.
- fam: reglas, motor, finde, invierno, gm unidad y lectura.
- pantalla.
- ws_compat: con la referencia nueva.

**Se adaptan cambiando datos y nombres (lógica genérica)**
- cboe: `py`, `crudos` y `lineas`.
- adaptadores: partes 1, 5 y 7.
- La autoprueba de nq.
- La prueba G de ndx y la HORA de qqq: horario de CME, igual.
- fam: `motor`, `invierno`, `finde` y `gm` unidad y lectura.
- clasica_dom: la parte C.
- Las partes 2-8 de replica20, cuando haya datos del clon.
- e2e, como humo.

**Bloqueados hasta grabar datos ES o correr la 2.0 en MES**
- Las referencias de nq, ndx, qqq y fam/integrado: falta la cuenta Python de ES y la cinta MES.
- fam: `tres`, `historia` y `gm` real/nq.
- posiciones.
- replica20, parte 1: no hay AUDIT de la 2.0 en ES.
- clasica y clasica_dom A/B: solo unos 450 AUDIT SPX+base de la clásica en ES, del 18-09 al 05-10.

**Se tiran**
- tqqq, entero.
- Lo TQQQ de cboe: `hilo`, `bajar`, `historia` y `modo_bajada`.
- Lo TQQQ de fam: `TqqqFalso`, `T_DOMS` y los 4 actuales.
- posiciones: P1b, P8 y la parte TQQQ de P11, más `csproj:28-30`.
- Las tres líneas TQQQ de `replica20.csproj:30-32` y `clasica.csproj:32-34`.
- e2e: `_modulos/tqqq/*.cs` (`e2e.csproj:30`) y la sección 5.
- pantalla: el eje TQQQ.
- La lista `S_T_*` de `grupo_arriba`.
- `correr_todo.ps1:37`.

### Fuera de este punto (para el inventario principal)

- **El renombre mecánico rompe la clase de meses `"FGHJKMNQUVXZ"`** si reemplaza "NQ" o "MNQ" como subcadena: queda `"FGHJKMESUVXZ"`, se pierden N (julio) y Q (agosto) y entran E y S. Aparece en:
  - `AdaptadoresFamilia.cs:104`
  - `_modulos/familia/ndx/CalendarioNdx.cs:112, 146`
  - `_modulos/familia/qqq/ConversionQqq.cs:70`
  - `_modulos/familia/replica20/Replica20.cs:291, 293`
  - `_modulos/pantalla/ArmadoPantalla.cs:262-263`
  - El script tiene que reemplazar por token entero o excluir esa cadena.
- **`AlmacenTres.cs:182`: `FactorMonto3("NQ") = 20/100`.** Con el renombre quedaría `"ES" = 0,2`, que está mal: para ES es 0,5.
- **`AlmacenTres.cs:136`: `v > 15000`.** Tira todas las rayas TRES en ES.
- **`ArmadoPantalla.cs:262` y `750-751`:** el renombre los arregla de casualidad ("MESZ6" contiene "ES"), pero el arnés de pantalla espera lo contrario (8.13).

---

## Z. Resumen: lo más riesgoso (lo que, si se olvida, deja las rayas vacías o corridas)

**Rayas vacías**
1. **Ganchos "NQ" del núcleo que el renombre no toca:** `GammaHoyTres.cs:1187` y `Viva3.cs:63`. Si quedan así, en MES no arranca la familia: ni CBOE, ni motor, ni una sola raya. MEDIDO hoy en MESZ6: 133 renglones `familia: cinta - | libro -` y el cartel naranja.
2. **`AlmacenTres.cs:136` (`v > 15000`)** descarta todas las D1/D2 de la estela con ES ≈ 7.700. TRES_ES, TRES_SPX y TRES_SPY quedan vacías aunque el núcleo ya escriba `estela-ES`. El laboratorio tiene el mismo filtro (`extremos_rebote.py:476`).
3. **Las capas SPX/SPY no existen en ES.**
   - Hay que generalizar `GammaHoyTresCapas.cs:122-123,135,153,323`, `GammaHoyTresRayas.cs:98-113` y `GammaHoyTresMajorOi.cs:48-53`.
   - Y hacerlas coincidir con `IntegracionFamilia.cs:250`, `BajadorCboe.cs:141-142`, `AlmacenTres.cs:29` y `CatalogoFamilia.cs:38`. El archivo "ES" es el de SPX.
4. **La razón entre 30 y 60 está pensada para QQQ** (`GammaHoyTresCapas.cs:283` y `ArmadoPantalla.cs:841`). SPY da ≈ 10,07: con el spot quieto la conversión de la familia no entra nunca, y el eje SPY no se dibuja nunca.

**Rayas corridas o mal pesadas**
5. **El multiplicador de ES es 50, no 20.**
   - `MinuteroNq.cs:47`: 20 → 50.
   - `ReglasFam.cs:82`: 0,2 → 0,5. `CambiosFamilia.cs:246` cae siempre en ese valor por defecto.
   - `AlmacenTres.cs:182`: 0,2 → 0,5.
   - Si quedan así, los montos, la suma FAM y los ▲▼ de ES salen ×0,4.
6. **El tercer viernes junta dos series.** `CboeCrudo.cs:188-217` descarta la raíz; `CboeCadena.cs:147,158` vence todo a las 16:00 y suma por (K, fecha).
   - La SPX mensual AM y la SPXW PM caen en la misma fila. MEDIDO la víspera del 16-10: "mañana" con 1,34 M de OI contra 0,33 M reales.
   - Corre los muros y las dominantes del jueves y el viernes. Los próximos son el 19/20-11 y el 17/18-12.
7. **La base de SPX no tiene control útil.** La tolerancia de 40 pts de `GammaHoyTresCapas.cs:255` es mayor que toda la base (≈ 35-37), y `ConversionNdx` no tiene ninguna cota: una cadena equivocada se dibujaría igual.
8. **Las réplicas no se arreglan con un renombre.**
   - En ES, la 2.0 y la clásica no tienen capas: dibujan su PRIMARIA SPX con la cascada medida → medida reciente → rueda (`PythiaGexDos/GammaHoyNucleo.cs:314-319`). La 4.1 no tiene `base_confiable` (`CboeCadena.cs:8`).
   - Hay que cambiar el dividendo 0,008 → 0,012 (`Replica20.cs:262`, `ClasicaNdx.cs:241`; el código usa 0,8 %, no 0,6 %), `VelaMin` 2 → 1 (`ClasicaNdx.cs:249`) y `CongelarEnLaRueda` true → false (`:255`).
   - "2.0 SPY" no tiene original.
   - Acá NO se reescalan el 2 %/100, el empate 20 %, el centroide ±12 ni las cuántas.
9. **Radios en puntos de NQ (ESCALA).**
   - El tope de 100 de min(2 %, 100), duplicado en `ReglasFam.cs:72,89,112` y `Seleccion.cs:31,37`.
   - ZEST_MAX 300 (`ReglasFam.cs:76`, `Seleccion.cs:33`) y el zero estándar 2,5/±300/50 (`ZeroEstandar.cs:29-30`).
   - En el núcleo: `GammaHoyTresFormula.cs:58,68`, `GammaHoyTresRecuadro.cs:78`, `GammaHoyTresApoyo.cs:38,51`, `GammaHoyTresCapas.cs:64,89` y `GammaHoyTresCaja.cs:127`.
   - En ES, 100 pts pasan la ventana densa de ±77.
   - La banda de SPY baja de ±20 a ≈ ±5 (`CatalogoFamilia.cs:102,124,127,136` = `SeriesPantalla.cs:102,108,109,113`).
   - **No reescalar por precio** `FAM_GRILLA` 5 ni `CONF_PTS` 3 (`ReglasFam.cs:74-75`): van atados al paso de strike, que en ES y SPX es 5, como en NQ.
10. **La cadena de meses `"FGHJKMNQUVXZ"`** (8 lugares, B.2): el borrador la corrompe.

**Ruido y engaño**
11. **TQQQ sigue vivo con el borrador.**
    - Baja cada 75 s (`BajadorCboe.cs:56`), es el eje por defecto (`ArmadoPantalla.cs:84`, `FamiliaCuatroPantalla.cs:101`) y calcula la x3 contra MES.
    - Al borrar `MotorFamilia.cs:366-402` hay que dejar `formando` (`:367`) y `sucias` (`:370`).
12. **Defaults.** El tipo es nuevo, así que mandan los 11 `= true` del código y el eje. Hay que decidirlos y avisarle al operador con captura antes y después (regla del 17-09).
13. **`CargaMinima` 50** (`ReglasFam.cs:118`, `GammaHoyTresRayas.cs:60-63`) no filtra nada en ES, SPX ni SPY: CARGA marca siempre hasta el tope de 12.
14. **`OiNq.cs:90` ("01:55").** En ES el OI llegó a saltar a las 01:58:51Z (15-09): si la primera foto cae entre 01:55 y 01:59Z, el OI viejo pasa por fresco.
15. **Textos con estadísticas y escalas de NQ bajo nombres de ES** (B.3.6): mienten por omisión.
16. **Dos descargadores.** Con la 4.1 y la 5.0 en el mismo ATAS bajan dos (cada uno con su mutex, `BajadorCboe.cs:54`), más `cboe_local.py`. SPX pesa 2,3 veces NDX y las dos arman la API a los 20 s del arranque (`CadenaApi.cs:182`). Efecto sobre Rithmic: SIN VERIFICAR.
17. **Validación.** Hoy no se puede armar ninguna paridad ES.
    - La referencia Python está atada a NQ (`backtest_familia.py:152,158,164`; `preview_niveles.py:64,329,333,475`) y no hay cinta de ticks de MES.
    - ws_compat necesita una referencia nueva.
    - La batería no se corre desde el 09-10, y el código ya va por la 4.1.9.
    - De la 2.0 en ES hay 5 AUDIT; de la clásica, unos 450 con SPX y base.

