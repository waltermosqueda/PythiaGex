# PythiaGex 5.0 — sacar TQQQ e infraestructura (27-10-2026)

Paso 2 de `DISENO_5_0.md`. Solo se tocó `atas/PythiaGexCinco` (la 4.1, `atas/PythiaGexCuatro`, no se tocó: ningún archivo suyo es más
nuevo que `clon_5_0_manifiesto.json`). Sin commit: los 6 borrados del módulo tqqq quedaron en el índice (`git rm`), el resto sin agregar.

Compila: `herramientas/build_serial.ps1 -Dir atas/PythiaGexCinco -Salida bin/Release` → "Build succeeded", 0 errores y los mismos 2 warnings
que el clon sin tocar (CS8321 `GammaHoyNucleo.cs:334` y CS0162 en `ArmadoPantalla.cs`, la rama `else` de `ESTILO_2E`, que pasó de la línea
1181 a la 1177). DLL: `bin/Release/PythiaGexCinco.dll`, sha256 `2678dc30…709c26` (sin instalar).

`git diff HEAD --stat`: 36 archivos, +286 / −2028 (de esas, 1547 son los 6 archivos del módulo tqqq). El resto son cambios chicos por archivo;
los finales de línea de cada archivo quedaron como en la 4.1 (LF o CRLF según el archivo; verificado archivo por archivo contra el origen).

## 1. Qué se borró

- **Módulo `_modulos/tqqq/` entero** (CalculoTqqq, TqqqArchivo, TqqqNoche, TqqqPerfil, TqqqSesion, TqqqUtil) y sus 6 `<Compile>` del csproj.
  Con él desaparecen el log `pythiagex5-tqqq.log` y los `PythiaGex5\familia\tqqq-*.json` (nunca llegaron a existir: la 5.0 no se instaló).
- **Catálogo** (`fam/CatalogoFamilia.cs`): las 6 series T_* (T_DOMS_vol, T_MUROS_vol, T_MUROS_oi, T_ZERO_oi, T_ZERO_vol_pm, T_DOMS_raz),
  `SeriesTqqq`, los presets "TQQQ x3" y "TQQQ vs Gammalito" y T_MUROS_oi de `PrendidasPorDefecto` (quedan 7). Quedan 35 series.
- **Contratos** (`_contratos/Contratos.cs`): `TqqqVela`, `ICalculoTqqq`, `IVersionSTqqq`, `FotoFamilia.TqS/TqC/TqDatoUtc` (el eje "TQQQ x3");
  `IMotorFamilia.Configurar` ya no recibe el cálculo de TQQQ.
- **Motor** (`fam/MotorFamilia.cs`): `OpcionesMotorFamilia.TqqqDeNoche`, `_tqqq`, `_tq` y sus versiones, `PasadasTqqqPorS`, `VelasTqqq`,
  el bloque "TQQQ por vela m2" de `Avanzar`, `MismaVela` y lo de TQQQ en `Publicar`. `VERSION` = "fam 5.0 (…, la 4.1.5d sin TQQQ)".
- **Salida** (`fam/SalidaFamilia.cs`): el parámetro de TQQQ de `Entrada`, `Actuales` y `Fuentes` y sus bloques (iban al final).
- **Sesión** (`fam/SesionFamilia.cs`): `RuedaIniUtc` (la apertura de la rueda; solo la usaban las velas de TQQQ).
- **Posiciones**: `CambiosFamilia.CalcularTqqq`, `CambiosLibro.EsTqqq`, `PerfilK.De(TqqqPerfil.Perfil)`, `OpcionesCambios.TqqqNoche`, el régimen
  de OI "TQQQ", la vela de TQQQ en la clave del cache, y en `LadoDe` el tipo "RAZON" y los roles "dom" / "dom (razon)";
  `FotosCambios.AhoraTqqq` y `VIDA_TQQQ_S`. `VERSION` = "cambios 5.0 (…)".
- **Integración** (`IntegracionFamilia.cs`): el ajuste `Tqqq41AnclaNoche` ("TQQQ de noche", grupo "7. Datos", Order 1040: era el último del
  grupo, no deja hueco), el `CalculoTqqq` del `HostFamilia` y su paso al motor y a los cambios. "Bajar las cadenas de CBOE (SPX, SPY)".
- **CBOE**: "TQQQ" de `OpcionesCboe.Libros` y de `Describir` (`BajadorCboe.cs`); el regex de poda `RxDia` queda `^cadena-(ES|SPY)-…`
  (`CboeArchivo.cs`).
- **Pantalla**: las 6 casillas `S_T_*` (Order 18-23 del grupo "0. PRENDER / APAGAR") y sus `case` en `SerieVisible4`; las 6 de
  `SeriesPantalla.Casillas` y del `Respaldo`; T_MUROS_oi de `SeriesPantalla.PrendidasPorDefecto`; el valor `TQQQ` de `EjeFamilia4` (queda
  Ninguno/SPX/SPY, con sus números 0/1/2 de siempre); el eje "TQQQ x3"; `StrikeTqqq`; la línea "TQQQ sin dato…"; el "por 1 % de TQQQ" del
  detalle; la excepción de TQQQ del strike del detalle; la rama "TQQQ <strike>" de la etiqueta; "RAZON"/"dom"/"dom (razon)" de `TieneLado` y
  "RAZON" y el " 0G" de TQQQ de `NombreDeTramo`. `ColTqqq` se llama `ColEje` (el MISMO color: lo usan las marcas del eje de SPX y SPY).
  `PantallaFamilia.VERSION` = "pantalla 5.0 (…)".
- **Textos que nombraban TQQQ** en comentarios que describen lo que hace el código hoy: corregidos con nota "5.0 (27-10-2026)".

## 2. El anillo de velas del gráfico (NQ_1600 / ES_1600)

- `ES_1600` (el `NQ_1600` de la 4.1) vivía solo adentro del módulo tqqq (`TqqqNoche` / `CalculoTqqq`: el MES al cierre para el ancla de
  noche) y se fue con él. Su respaldo `OpcionesTqqq.CierreVelaGrafico` nunca estuvo conectado (el integrador no lo asignaba). En
  `GammaHoyTres.cs` solo quedaba el comentario "GANCHO-4.1 TQQQ", reemplazado por una nota.
- El anillo de velas del gráfico es `CintaFamilia._velasGraf` (lo llena `GammaHoyTres.AlimentarVelasGraficoFamilia` cada 5 s) y **no es solo
  de TQQQ: queda**. Lo usan: `MotorFamilia` (`CierreConocido` → el fut de cada minuto de TODAS las series), `MinuteroEs` (`VelasM2` →
  corrimiento de ES), `LibroMinuteroSpx` (`Precio` / `CierreConocido` → base C7), `MinuteroSpy` (`Precio` → razón C8), `Replica20` (`Precio`)
  y `ClasicaSpx` (`VelasM2` y `VelaM2Completa` → base de la rueda).

## 3. Qué quedó a propósito

- `FotoCadena.CierreAnterior`, `CierresCboe` / `cierres.json`, `BajadorCboe.Cierre` y `ManijaCboe.Cierre`: los leía solo la s de TQQQ. Quedan
  porque son dato del descargador (se sigue anotando el cierre de SPX y SPY), no cambian ningún cálculo. Hoy no los lee nadie.
- `ICinta.PrecioSoloTick`: lo usan C7, la 2.0 y la clásica.
- La regla de "libro flaco" de `RegimenesOi` (≥ 8 claves y ≥ 80 %) y `OpcionesCambios.PublicacionCboeHastaNy` = 09:30 NY: se midieron en la
  4.1 con TQQQ adentro, pero se aplican a cualquier libro; cambiarlas cambiaría ES/SPX/SPY. No se tocaron (nota 5.0 en los comentarios: lo
  medido es de la familia del Nasdaq, el clon renombró los tickers en el texto).
- `SesionFamilia.SiguienteIniUtc` (lo usa la historia de todas las series) y `ClavesCambios.Indexar(f, null)` (API general).
- Comentarios con mediciones hechas sobre TQQQ (RegimenesOi, GammaHoyTresCapas, cabecera del catálogo): quedan como historia.

## 4. Por qué ninguna otra serie cambia

- **Índices guardados**: las T_* estaban en las posiciones 24-29 de la lista, DESPUÉS del `Take(24)` que arma `Calc`; `Extra` no las tenía.
  Verificado compilando aparte el catálogo de antes (HEAD) y el de ahora y volcando todo: `Calc` (26 ids, índices 0-25) y `Extra` (6 ids,
  con su `MetaExtraDe`) idénticos; las 35 series restantes idénticas campo por campo (id, corto, libro, fuente, tipo, grupo, color, banda,
  técnico, criollo, estado); `Rol()` idéntico; casillas y respaldo de la pantalla idénticos salvo los 6 de TQQQ. Las únicas diferencias del
  volcado son las líneas de TQQQ, los 2 presets y T_MUROS_oi de las dos listas de prendidas.
- **La cuenta de un minuto** (`RegistroMinuto`) nunca tuvo TQQQ: iba en sus propias velas y se AGREGABA AL FINAL de la historia, de los
  actuales y de las fuentes. Lo de las demás series sale igual y en el mismo orden.
- **Cambios por nivel**: TQQQ se calculaba DESPUÉS de ES, SPX y SPY y con su propio resultado (y su propio régimen de OI); FAM solo suma
  ES/SPX/SPY. La clave del cache pierde la vela de TQQQ: antes una vela nueva de TQQQ forzaba un recálculo de ES/SPX/SPY que daba lo mismo.
- **Historia por vela m2**: antes una vela de TQQQ que cambiaba marcaba esa vela como "sucia" y se rehacía la entrada de TODAS las series de
  esa vela con los mismos datos. Sin TQQQ se rehacen las de siempre (minutos nuevos o rehechos, TRES, las últimas velas, todo al abrir la
  sesión o con muchas líneas nuevas de TRES). Cambia cuándo se refresca, no qué valor sale.
- Los roles y tipos borrados de `LadoDe` / `TieneLado` / `NombreDeTramo` ("dom", "dom (razon)", "RAZON", el " 0G" de ZTP) solo los producía
  TQQQ (grep: ninguna otra serie usa esas etiquetas).
- **CBOE**: cada ticker conserva su cadencia (75 s rueda / 300 s fuera) y la pausa de 3 s entre SPX y SPY; solo desaparece la tercera
  bajada de cada vuelta. Los "generado" de SPX/SPY pueden caer unos segundos distinto que con TQQQ (no es una fórmula, es el reloj).

## 5. Cambios que se ven (hay que avisarle al operador)

1. **Eje doble izquierdo: default SPX** (`Eje4Libro` y `AjustesPantalla.Eje`). Era TQQQ, que ya no existe. Con SPX se dibujan marcas del eje
   izquierdo (base sincronizada) y la fuente "SPX CBOE" aparece siempre en la pestaña. Es una decisión mía para que compile con algo
   sensato: la tiene que confirmar el paquete P5 (pantalla) y avisarse con captura antes/después, como pide la regla del operador.
2. Grupo "0. PRENDER / APAGAR": sin los 6 renglones de TQQQ. Order 0..98 **sin huecos**: los Order ≥ 23 corrieron −5 (84 propiedades en
   GammaCincoPantalla y 8 archivos del núcleo: GammaHoyTres, Apoyo, Atravesadas, Capas, Formula, MajorOi, Rayas, Recuadro; solo el número
   del `[Display]`). Mismo orden relativo; los Order repetidos que ya tenía la 4.1 siguen repetidos (7-11 y los que eran 98-100, ahora
   93-95). 107 propiedades en el grupo (antes 113). El primer Order sigue siendo 0, así que el grupo sigue arriba de todo.
3. Carteles y pestaña sin TQQQ: "PythiaGex 5.0: esperando el primer calculo (todo adentro: libro ES por Rithmic, SPX/SPY bajados de CBOE)",
   "calculado adentro: libro ES por Rithmic + SPX/SPY bajados de CBOE…", avisos "SPX/SPY no se actualizan" / "SPX/SPY congelados".

## 6. Infraestructura

### 6a. Espaciador de CadenaApi a +50 s
`CadenaApi.DESFASE_PRIMER_ARMADO_S` = 50 (la 3.0 arma en 0 s y la 4.1 a +20 s: es estático POR ENSAMBLADO y no se ven entre sí).
`_primerArmadoDesdeUtc` y `_proximoTurnoGlobal` arrancan de ese valor. El estado dice "5.0: espero N s antes del primer armado (desfase de
50 s: al reiniciar ATAS la 3.0 arma enseguida y la 4.1 a los 20 s)". `ESPACIO_GLOBAL_S` (10 s entre tandas de la misma DLL) no cambió.

### 6b. Recursos con nombre del sistema operativo (búsqueda de "PythiaGex4", "pythiagex4", "Local\\", "Global\\", "new Mutex", "Thread(")
- "PythiaGex4" / "pythiagex4" / "PythiaGexCuatro" / "FamiliaCuatro" en el código: **0** (solo un comentario histórico del csproj que dice
  "namespace PythiaGexCuatro.Familia").
- `Local\`: un solo mutex, `Local\PythiaGex5.BajadorCboe` (`_modulos/cboe/BajadorCboe.cs:56`, `new Mutex` en la 389). La 4.1 usa
  `Local\PythiaGex4.BajadorCboe`: no se pisan. `Global\`: ninguno en la DLL (el único del repo es `Global\PythiaGex4.BuildSerial` de
  `herramientas/build_serial.ps1`, el candado de compilación, compartido a propósito y fuera de la DLL).
- EventWaitHandle / Semaphore / MemoryMappedFile / NamedPipe con nombre: ninguno. Los AutoResetEvent / ManualResetEventSlim no tienen nombre.
- Hilos: "PythiaGex5 cinta-familia" (`AdaptadoresFamilia.cs:637`), "PythiaGex5 libro-familia" (`AdaptadoresFamilia.cs:1056`),
  "PythiaGex5 cinta" (`GammaHoyTresCinta.cs:363`), "PythiaGex5 familia" (`IntegracionFamilia.cs:242`), "PythiaGex5 cboe"
  (`BajadorCboe.cs:156`). El módulo tqqq no tenía hilo propio.
- Carpetas: todo bajo `%APPDATA%\ATAS\PythiaGex5` (`Registro.CarpetaDatos`, `AdaptadoresFamilia.Carpeta`, `OpcionesMotorFamilia.Carpeta`,
  `OpcionesCboe.Carpeta` = `PythiaGex5\cboe`, `Cinta4Carpeta` = `PythiaGex5\cinta`, `Estado4Ruta` = `PythiaGex5\estado\indicador.json`;
  además `familia\`, `estela\`, `viva\`, `caja\`, `rebobinado\` y `base-rueda-*.json` / `razon-rueda-*.json` en la raíz).
- Logs: `%APPDATA%\ATAS\pythiagex5-<nombre>.log` (gammahoy, pantalla, viva3, adaptadores, integracion, familia, cambios, cboe) y
  `pythiagex5-centinela-*.jsonl`. Dentro de la 5.0, `Registro` ("pantalla") y `BitacoraPantalla` escriben el mismo `pythiagex5-pantalla.log`
  (ya pasaba en la 4.1 con el suyo; no se cruza con otra versión).
- **Lo único fuera de PythiaGex5**: `OpcionesCboe.CarpetaExtra` = `%APPDATA%\ATAS\PythiaGex\cboe-local` (`BajadorCboe.cs:41`), de **solo
  lectura**: la escribe `cboe_local.py` (afuera de ATAS) y sirve para rellenar las horas con ATAS apagado (4.1.9). Medido hoy: ahí hay
  `cadena-ES-*` (es `_SPX`: spot 7676,77) y `cadena-SPY-*`, además de NQ/QQQ/TQQQ. La 4.1 lee NQ/QQQ/TQQQ y la 5.0 ES/SPY: no comparten
  archivo y ninguna escribe ahí. Lo dejé. Si se quiere aislamiento total, `CarpetaExtra = ""` (se pierde ese relleno).
- Identidad en ATAS: ensamblado `PythiaGexCinco`, tipo `PythiaGexCinco.GammaCinco`, "PythiaGex 5.0 - Gamma ES", categoría "PythiaGex 5.0":
  los ajustes que ATAS guarda por nombre en el .ws no se mezclan con los de la 4.1.

### 6c. Textos visibles con "4.0" / "4.1" / "Familia" como nombre del producto
Cambiados a "PythiaGex 5.0" / "la 5.0":
- Cartel de arranque (sin "de la Familia") y renglón "el motor de PythiaGex 5.0 no publica hace …" (`ArmadoPantalla.cs`).
- Descripciones de ajustes: "Pestaña de detalle" ('PythiaGex 5.0 ▸'), "Recuadro abierto", "SPY dom sel2.0" y "SPX dom sel2.0" ("el libro …
  de la 5.0"), "Clas SPX 0Γ" ("las cadenas y la cinta de la 5.0") en `GammaCincoPantalla.cs`; "3.0 libro▸" ("la pestaña de la 5.0") y
  "3.0 cabecera▸" ("La 5.0 tiene su propia pestaña") en `GammaHoyTres.cs`; "Tunel sombreado" ("en la 5.0 ya no cambia nada") en
  `GammaHoyTresRayas.cs`.
- Estado de las capas del núcleo: origen "cboe-5.0" / "cboe-5.0 (memoria)" y "la baja el descargador de la 5.0" (`GammaHoyTresCapas.cs`;
  nadie compara esos textos).
- Fuente "Clasica SPX" de la pestaña: "ahora: 5.0 sincronizada X" y "(sin base de la rueda en las cadenas de la 5.0)" (`ClasicaSpx.Texto`),
  con su lector `ArmadoPantalla.ReAhoraClasica` cambiado a `5\.0 sincronizada` en el mismo paso (probado: lee 37.18, −1.50, 228.62, 0.00).
  Los números y el resto del texto no cambian. Como la 5.0 nunca corrió, no hay archivos viejos con "4.1 sincronizada".

No cambiados (no son el nombre del producto o no se ven):
- Marcas de versión con fecha en descripciones y logs ("4.1:", "4.1.5d:", "4.1.7 (12-10-2026)", "casillas 4.1.5e", "4.1: dibujo 3.0").
- `VERSION` de módulos que no toqué (`ClasicaSpx` "clasica 4.1.5d": va dentro del archivo de muestras; `Replica20` "replica20 4.1.4") y la
  marca de formato `"v":"fam-4.1"` de los archivos `niv-*.jsonl`.
- Textos técnico/criollo/estado del catálogo y las notas de `CambiosFamilia` (`NOTA_REPLICA20`, `NOTA_REPLICA_CLASICA`): dicen "de la 4.1"
  pero hoy no se muestran en ningún lado (la pantalla no lee Técnico/Criollo/Estado y esas notas solo van a niveles sin lado). Los presets
  "2.0 contra 4.1" / "Clasica contra 4.1" tampoco se usan. Quedan para P5 (textos honestos).
- "Familia" como nombre del conjunto de series (descripciones "es una serie de la Familia", grupo "2. Familia", FAM): no es el producto.

## 7. Riesgos y hallazgos fuera de esta tarea (no los toqué)

1. **El núcleo sigue atado a NQ**: `GammaHoyTres.AsegurarAdaptadoresFamilia` hace `if (raiz != "NQ") return;`, `Viva3.Guardar` alimenta el
   ILibroEs solo `if (raiz == "NQ")` y `GammaHoyTresCapas` hace `if (raiz != "NQ" …) return;` con capas NDX/QQQ que buscan
   `ultima-NQ.json` / `ultima-QQQ.json` (la 5.0 escribe `ultima-ES.json` / `ultima-SPY.json`). Así como está, en un gráfico de MES la 5.0 no
   arma la familia (sin ICinta ni ILibroEs no hay host) ni TRES_SPX/TRES_SPY. Es del paso semántico (P1/P4), no de TQQQ.
2. **Clase de meses rota por el clon**: `[FGHJKMESUVXZ]` en `AdaptadoresFamilia._reContrato` y en `ArmadoPantalla.ReVencEs/ReVencSolo` (era
   `[FGHJKMNQUVXZ]`: el renombre NQ→ES cambió las letras N y Q por E y S). Para ES (H, M, U, Z) no molesta, pero está mal.
3. **Eje SPY**: hereda la cota de la 4.1 (razón entre 30 y 60, la de QQQ ≈ 41). La de SPY anda por 10: en cboe-local (último trade
   16:14:59 NY del 26-10) SPX 7676,77 / SPY 766,28 = 10,02, y ES/SPY es del mismo orden (ES = SPX + unas decenas de puntos). Con esa cota el
   eje SPY dice "sin conversion vigente" y no dibuja. Es un número: P5.
4. El default del eje (punto 5.1) es provisorio.
5. Las descripciones y los estados del catálogo siguen citando mediciones de NQ (porcentajes, Holm, "candidata principal"): P5.
6. `herramientas/grupo_arriba_415e.py` (la "fuente de verdad" del `[Display]` del grupo de arriba en la 4.1) apunta a `atas/PythiaGexCuatro`
   y trae las S_T_* de TQQQ y los nombres NQ/NDX/QQQ: no correrlo sobre la 5.0 sin adaptarlo, porque volvería a numerar el grupo con TQQQ.
