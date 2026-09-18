# PythiaGex 2.0 - Gamma Hoy: cambios

Regla del operador (18-09-2026): la produccion (`atas/PythiaGexNiveles`, "PythiaGex - Gamma Hoy" 1.11d) no se toca mas.
Todo arreglo y toda feature va a este clon (`PythiaGexDos.dll`, "PythiaGex 2.0 - Gamma Hoy"). Si algo se rompe, se vuelve a
agregar la original al grafico. Cada entrada dice que se arreglo, con que evidencia y donde toca el codigo.

## 2.0.2 (18-09-2026) - lo que la revision encontro mal en 2.0.1

String de arranque: `Gamma Hoy 2.0.2 (F1-F7, revisado) arranca ...`. Los revisores confirmaron ocho defectos en el clon (ninguno
en prod, que sigue sin tocarse). Cada uno con su prueba y donde toca. Compila con 0 errores.

### F1 de verdad: la causa diagnosticada en 2.0.1 era falsa (Feed.cs `Feed.Archivo`)

- Evidencia (medida en `%APPDATA%\ATAS\PythiaGex\cadenas\local-QQQ-2026-09-18.jsonl` de prod, 14:30 local): 1445 lineas, 34
  distintas; y son 34 sellos `ts|ultimo_trade|spot_idx`, 34 "generado" y 34 bases. Las repeticiones son copias IDENTICAS (mismo ts,
  misma base, mismo generado) en rachas de 10-20-60-485 lineas (485 copias de la de 04:16:24Z entre 04:16 y 12:56Z). O sea: ni la
  base ni el generado cambiaban entre lineas repetidas, y el sello viejo (`ts|base|generado`) ya era igual para todas; el casillero
  `_ultimoSello[raiz]` tendria que haber cortado y no corto porque era UNO por raiz y `BajarUltima` archiva DOS cadenas por ciclo:
  primero la de `UltimaLocal` (cboe-local/ultima-QQQ.json, generado 2026-09-17T03:55:49, un dia vieja porque cboe_local.py estaba
  parado) y despues la de la nube. Alternan A,B,A,B; cada llamada pisa el casillero y escribe. Prueba: `local-QQQ-2026-09-17.jsonl`
  tiene 4447 lineas con 2855 copias de la de 03:55:49 y fue modificado hoy a las 14:15:37, el mismo segundo que el archivo del 18.
  En 2.0.1 "parecia" arreglado solo porque `CarpetaLocalCboe` apuntaba a una carpeta vacia (ver mas abajo).
- Cambio: `_sellos` = por raiz, un diccionario sello -> hora de escritura. `GuardarLocal`: si ESE sello se escribio hace menos de
  `LatidoMinutos` (10), no escribe; si la cadena tiene mas de `SinLatidoDesdeMin` (60) y ya se escribio alguna vez, no late nunca mas
  (el latido vale solo para la cadena en curso: antes una cadena local de 24 h se apendeaba cada 10 min al archivo del DIA VIEJO para
  siempre). Poda de sellos con mas de 26 h cuando pasan de 400. El sello sigue siendo `ts|ultimo_trade|spot_idx` (no depende de la base
  ni del generado); las lineas con distinto ts pero mismo contenido de CBOE (la nube de noche, cada 8-25 min) se conservan a proposito:
  cada una lleva la base viva de su minuto, que el rebobinado usa.
- Ojo que queda: `UltimaLocal` sirve una cadena de 24 h si cboe_local.py se paro (`BajarUltima` la devuelve como respaldo cuando la nube
  falla). No se archiva repetida, pero se dibuja con su edad en la cabecera; la guardia F2 la trata por su ultimo trade.
- Toca: Feed.cs `_sellos`, `LatidoMinutos`, `SinLatidoDesdeMin`, `GuardarLocal`.

### cboe-local: el clon perdia la CBOE bajada desde esta PC (Feed.cs `CarpetaLocalCboe`)

- Evidencia: el clonador (herramientas/clonar_2_0.py) reemplazo a ciegas todo `"ATAS", "PythiaGex"` por PythiaGex2, incluida esta
  carpeta que NO es del DLL: la escribe cboe_local.py (DESTINO = `%APPDATA%\ATAS\PythiaGex\cboe-local`, linea 32) y el DLL solo la LEE.
  Con `PythiaGex2\cboe-local` (vacia) `UltimaLocal` daba null siempre y la 2.0 caia a la nube (cache 5 min + nube cada 8-25 min): cadena
  5 a 25 min mas vieja que la de prod en el mismo momento, y el rebobinado no encontraba `cadena-<raiz>-<dia>.jsonl.gz` local.
- Cambio: `CarpetaLocalCboe` vuelve a `ATAS\PythiaGex\cboe-local` (entrada externa de solo lectura, compartida con prod sin choque) y
  clonar_2_0.py la excluye del reemplazo. Verificar en el log 2.0 que la cabecera diga "ultima local (CBOE directo)" en la rueda.
- Toca: Feed.cs linea ~114; herramientas/clonar_2_0.py lineas ~29-32.

### F2 de verdad: la cadena "vieja" se juzgaba por ts y no veia el spot congelado (GammaHoy.cs `EscalarCon`, `UltimoTradeUtc`)

- Evidencia: en la nube `cadena.ts` es la hora de la BAJADA, no la de la foto. Medido en 117 cadenas distintas de QQQ y ES (17 y
  18-09): de dia `ts - ultimo_trade` = 15,0 min exactos (el retraso de CBOE); despues de las 16:15 NY el ultimo trade queda clavado en
  16:14:59 y ts sigue avanzando (linea generado 02:31:07Z: ts 02:30:23, ultimo_trade 2026-09-17T16:14:59, spot 715.79). Con la nube
  publicando cada 8-25 min hasta las 04:16Z, `edadExtraMin` daba negativo, `vieja` = false y se entraba por la rama fresca: razon =
  NQ(ts - 902 s) / 715.79, valida = true, a la mediana y a `razon-QQQ.txt`. Exactamente NQ_de_hace_15_min / spot_viejo. En el log de
  prod (AUDIT capa=QQQ, 17-09 17:00 -> 18-09 01:30 local) la razon "vela_alineada" derivo 41,4955 -> 41,4399 (23:00) -> 41,5527 (01:30):
  0,27 % = 80 pts de NQ en toda la capa, y 2.0.1 la guardaba como "ultima valida" para el resto de la noche y hasta 4 dias. El caso de
  las 04:02-05:22 solo se cubria porque a esa hora la nube dormia y ts tenia 3 h.
- Cambio: `UltimoTradeUtc` parsea `ultimo_trade` (hora de Nueva York, como GammaVivo.cs ~5459: TryParse + "Eastern Standard Time" ->
  UTC). Con ultimo_trade la vela se alinea SIEMPRE a esa hora (es la hora real de la foto: fresca o congelada, los dos cotizaban en ese
  instante) con `BarraDeExacta` (busqueda binaria en todo el grafico: la vela de las 16:14:59 NY queda a 700 velas de 1 min a las 04:00
  y `BarraDe` miraba 600). CONGELADA = ultimo trade mas de 20 min por detras de lo que el retraso explica (`atrasoUtMin`): el origen
  dice "vela alineada al ultimo trade 16:14 NY (cadena congelada N min)", no alimenta la mediana de la rueda, `AnotarValida` recibe la
  hora del ultimo trade (de noche nunca avanza mas alla de las 16:14:59) y se loguea `razon QQQ (F2): ...` cada 5 min como maximo. La
  guardia del contrato continuo (0,6 %) solo con la cadena fresca y no vieja. El criterio por ts queda solo para cadenas sin
  ultimo_trade (Rithmic o sin campo).
- Toca: GammaHoy.cs `UltimoTradeUtc`, `EscalarCon` (~975-1080), `BarraDeExacta` (junto a `BarraDe`).

### F2 regresion: el rebobinado de la primaria ETF usaba UNA razon (la de hoy) para todo el archivo (GammaHoy.cs `CargarArchivo`)

- Evidencia: `foreach (var x in ls) Escalar(x)` llamaba a `EscalarCon` sin hora ni precio: `ahora` = UtcNow, toda cadena del archivo
  era "vieja", y como `BajarFeed` ya habia anotado la ultima valida (o `razon-QQQ.txt` venia de la corrida anterior), ninguna cadena
  del pasado la superaba: todas recibian `ult`, la razon de HOY, en vez de precio_de_ese_minuto / spot_de_esa_cadena (lo que hacia prod
  y lo que si hace el rebobinado de capas, GammaHoyCapas.cs ~735). Con NQ/QQQ moviendose 0,1-0,3 % en el dia (0,3 % = ~90 pts de NQ en
  el ex-dividendo trimestral de QQQ) el pasado quedaba 30-90 pts corrido; en la semana del roll hasta 1 % (300 pts). Deterministico en
  cada reinicio salvo el primerisimo sin archivo.
- Cambio: una `RazonEtf` temporal (sin Ticker: no persiste ni contamina el vivo) y, para cada cadena, `BarraDeExacta(x.GeneradoUtc)` y
  el cierre de esa vela: `EscalarCon(x, raiz, rr, retraso, x.GeneradoUtc, p)`. Las cadenas sin vela en el grafico (antes de la primera
  vela o en un hueco de mas de 4 h) se cuentan en el log y se DESCARTAN del rebobinado: con Escala = 1 dibujarian QQQ en 715 sobre un
  grafico de NQ. La mediana de la rueda del archivo siembra la del vivo solo si el vivo todavia no midio nada. En el arranque NO tiene
  que aparecer `razon QQQ (F2): ultima valida hace ...` disparado por el archivo (ese log es solo en vivo).
- Toca: GammaHoy.cs `CargarArchivo` (~1150).

### La razon guardada por ticker se pisaba entre graficos (GammaHoy.cs `EscalarCon`, GammaHoyCapas.cs `RazonEtf`)

- Evidencia: la razon es precio_del_futuro_del_grafico / spot_del_ticker. La capa SPY en un grafico de MNQ da NQ/SPY ~ 45,6; la misma
  capa (o la primaria con Libro=CBOE_ETF) en un grafico de MES da ES/SPY ~ 10,3. Las dos escribian `razon-SPY.txt` (idem SPX: 4,5
  contra 1,0), una vez por minuto: ganaba la ultima. Reinicio de noche con CBOE congelada: `CargarSiHaceFalta` cargaba el valor del
  otro grafico y la capa SPY del MES se dibujaba con razon 45 (niveles en ~29.900 sobre un grafico en 6.700: desaparecen), con estela,
  centinela e historia anotando basura hasta que CBOE volvia fresca.
- Cambio: `r.Ticker = Raiz() + "-" + ticker` (`razon-NQ-SPY.txt`, `razon-ES-SPY.txt`, `razon-NQ-QQQ.txt`). Ademas, guardia de cordura al
  usar la ultima razon guardada: si `|ult x spot / precio_del_grafico - 1| > 5 %` se descarta (distingue 10 de 45 sin discutir el 0,8 %
  del roll), se limpia de memoria y se loguea `razon SPY (2.0.2): descarto la ultima razon guardada ...`. La division ahi VALIDA, no
  dibuja.
- Toca: GammaHoy.cs `EscalarCon` (Ticker y guardia del 5 %); GammaHoyCapas.cs comentarios de `RazonEtf`.

### F3 que faltaba: "la suscripcion fallo" salia sin marcar ArmadoIncompleto (CadenaViva.cs `Arrancar` ~671)

- Evidencia: era la unica salida temprana de `Arrancar` con `L(...) + return` en vez de `Incompleto(...)` (revisadas las 9). Escenario:
  rearme por "el precio se alejo N pts" con Activa = true; ya se desuscribieron los contratos que salen de la ventana y despues
  `SubscribeToMarketData` tira (Rithmic cortando la conexion de datos, como el 16-09 cada 62 s). Resultado: Activa true, `_suscritos`
  apuntando a la lista vieja (parte ya soltada), sin contratos nuevos, ArmadoIncompleto false, y nadie reintentaba hasta que el precio
  se alejara otro medio radio del NUEVO centro (CentroVentana ya actualizado) o cambiara el dia.
- Cambio: `catch` -> `Incompleto("la suscripcion fallo: ...")` (rearme a los 5 min) y, antes, `_suscritos`/`_codigos` quedan solo con
  los contratos del armado anterior que siguen pedidos (los soltados ya no se leen): la foto de contratos no miente hasta el rearme.
- Sobre las 06:57 UTC del 18-09 (viva de ES de prod): se vuelve a confirmar en el log lo dicho en 2.0.1. La instancia de ES arranco a
  las 03:54:19 local, armo bien (5400 contratos por puente, "EN VIVO"), escribio AUDIT a las 03:55:24, 03:56:28 y 03:57:30 local y
  despues NADA (ni AUDIT del temporizador, ni viva, ni error) hasta el siguiente `arranca ... raiz=ES` a las 11:37:42, mientras las dos
  de NQ siguieron logueando toda la mañana. La instancia desaparecio entera (grafico cerrado o indicador quitado): ningun rearme
  cubre eso; F3 cubre los armados que fallan (ahora tambien la suscripcion) y el latido de 15 min deja rastro de a que hora murio.
- Toca: CadenaViva.cs ~635-690.

### Convivencia con prod: la 2.0 no suelta contratos que prod puede estar leyendo (CadenaViva.cs `ProdPresente`)

- Evidencia: el conector de Rithmic es UNO por proceso y prod y 2.0 suscriben los MISMOS Security de opciones por la maquinaria privada
  (no por el DataFeed de los graficos, que si cuenta referencias). La 2.0 desuscribia en cada rearme (contratos que salen de la ventana,
  ~642), en el nivel 2 (~682) y al quitarse del grafico (~1434/1436); con F3 rearmando cada 5 min mientras el armado quede incompleto
  (series vacias de noche, conector sin conectar), cada rearme soltaba contratos. NO esta medido si el conector cuenta referencias por
  Security: si no las cuenta, prod queda "RITHMIC FLACO" o con bid/ask congelados sin un solo error en su log, justo cuando el operador
  agrega la original al lado de la 2.0.
- Cambio: `ProdPresente()` = el ensamblado `PythiaGexNiveles` esta cargado Y su `PythiaGex.CadenaViva._proximoTurnoGlobal` (estatico,
  por reflexion) ya no es MinValue, o sea prod reservo un turno de suscripcion en este proceso (ATAS carga todos los DLL de Indicators
  al arrancar: "cargado" solo no alcanza). Se relee cada 30 s y queda pegajoso hasta reiniciar ATAS. Si la reflexion falla, se asume
  presente. Con prod presente la 2.0 NO llama a `UnsubscribeFromMarketData` en ninguno de los cuatro lugares: limpia solo sus
  `_bids/_asks/_suscritos` y loguea "prod presente: no suelto N contratos". Costo: las suscripciones que salen de la ventana quedan
  vivas hasta reiniciar (Prints/Best/Summary; el nivel 2 esta apagado por defecto).
- PENDIENTE (no se puede hacer sin ATAS): medir UNA vez, con los dos DLL en el mismo grafico de MES, si las puntas de prod ("EN VIVO: N
  de M") sobreviven a un rearme del clon; recien con eso dejarlos convivir de rutina.
- Toca: CadenaViva.cs `ProdPresente` (junto a `_proximoTurnoGlobal`), ~640-650, ~690, `Dispose`; README.

## 2.0.1 (18-09-2026) - F1 a F7

String de arranque: `Gamma Hoy 2.0.1 (F1-F7) arranca ...` (GammaHoy.cs, OnInitialize; tambien en REBOBINADO).
Propiedades nuevas con nombre nuevo (ATAS persiste los ajustes por nombre de propiedad): `ZeroGammaInterpolado`,
`HistoriaDelDia`, `BarrasRelativas`, `ModoGatilloModelo`. Nada pesado en OnRender; escrituras a disco fuera de los locks.

### F1 - Archivador de cadenas repetidas (Feed.cs, `Feed.Archivo`)

- Evidencia: `%APPDATA%\ATAS\PythiaGex\cadenas\local-QQQ-2026-09-18.jsonl` tenia 1234 lineas con 42 cadenas distintas
  (medido a la mañana); a las 14:10 local, 1411 lineas y 33 cadenas distintas por `cadena_ts` (y las mismas 33 por
  `ts|ultimo_trade|spot_idx`). CORREGIDO EN 2.0.2: la causa que decia aca ("la base viva y el generado cambian cada minuto") era
  FALSA: entre lineas repetidas ni la base ni el generado cambiaban (33 bases y 33 generados para 33 sellos). La causa real es el
  casillero unico por raiz con dos cadenas por ciclo (local + nube), ver 2.0.2.
- Cambio: `Sello(c)` = `ts|ultimo_trade|spot_idx` (NO depende de la base ni del generado). `GuardarLocal` archiva cuando el
  sello cambia y, si no cambio, un latido cada `LatidoMinutos` = 10 como maximo (`_ultimoSello` guarda sello + hora de
  escritura). Para LEER sin repetir (`Cargar`, nube + local del mismo minuto) se usa `Clave(c)` = sello + generado: dos
  publicaciones distintas de la misma cadena se conservan las dos porque el rebobinado usa la base de cada una.
- Toca: Feed.cs lineas ~262-292 (sello, clave, GuardarLocal) y ~481 (Cargar).

### F2 - Razon NQ/QQQ con el spot congelado (GammaHoyCapas.cs `RazonEtf`, GammaHoy.cs `EscalarCon`)

- Evidencia (log de prod `pythiagex-gammahoy.log`, 18-09 04:02-05:22 local): una instancia de MNQ con la capa QQQ uso
  `S=715.78` (spot de CBOE congelado desde la noche) y `origen=...razon_41.8045_(CRUDA: la vela alineada es de otro contrato
  (29743 vs 29923))`, mientras la otra instancia daba `S=720.01 ... razon_41.5527_(vela_alineada)`. La guardia del contrato
  continuo (15-09) vio la vela alineada (29743, de la hora de la cadena vieja) a mas de 0,6 % del precio de ahora (29923) y
  concluyo "otro contrato", cuando en realidad la cadena era vieja: 41,80 contra 41,55 = +0,8 % = ~240 pts de NQ en todos
  los niveles de esa capa durante una hora y pico.
- Cambio: la cadena se considera VIEJA si su foto tiene mas de `RazonCadenaViejaMin` = 20 min por encima del retraso normal
  (`RetrasoCboeSeg`). Con cadena vieja o sin vela alineada NUNCA se divide precio_de_ahora / spot_viejo. Orden: la vela
  alineada de esa misma cadena (valida en SU momento: los dos cotizaban) si es mas nueva que la ultima guardada; si no, la
  ULTIMA razon valida (memoria + archivo chico `%APPDATA%\ATAS\PythiaGex2\razon-<ticker>.txt`, hasta 4 dias: cubre el fin
  de semana); despues la mediana de la rueda; y recien al final la cruda, dicha como tal. La guardia del contrato continuo
  sigue, pero SOLO con la cadena fresca (ese si es el caso del roll). Cada razon valida se anota con
  `RazonEtf.AnotarValida` (una escritura por minuto como maximo, fuera del lock; nunca hacia atras en el tiempo). Cuando
  la guardia aplica, el log dice `razon QQQ (F2): <origen> => 41.5527 (spot ..., precio ...; la cruda hubiera sido 41.80..)`
  a lo sumo cada 5 min, y el `origen` de la cadena (AUDIT y cabecera) lleva el motivo.
- Rebobinado de capas (`CargarEstelaCapa`): `EscalarCon` recibe la hora y el precio de ESE minuto (`c.GeneradoUtc`,
  `futuro`), asi la edad de la cadena se juzga contra su propio momento y no contra ahora.
- Toca: GammaHoyCapas.cs (`RazonEtf`: Ultima/UltimaUtc/Ticker, CargarSiHaceFalta, AnotarValida; linea ~683 la llamada del
  rebobinado), GammaHoy.cs `EscalarCon` (~970-1040).

### F3 - Rearme cuando el armado de la cadena viva falla (CadenaViva.cs `Arrancar`, GammaHoy.cs `RearmarVivaSiHaceFalta`)

- Problema: si un rearme (recentrado, cambio de dia, falta el cercano) salia por una rama temprana ("las series vinieron
  vacias", conector no encontrado o desconectado, futuro fuera del catalogo, precio que no llego, "suscrito pero solo N con
  las dos puntas", excepcion), `Activa` seguia en true por el armado anterior y nadie volvia a intentar hasta que el precio
  se alejara medio radio del centro (o nunca).
- Cambio: `CadenaViva.ArmadoIncompleto` (+ `MotivoIncompleto`, `IncompletoUtc`). Se limpia al empezar cada armado y al
  llegar a "EN VIVO"; cada salida temprana lo enciende (`Incompleto(...)`, con el motivo en el log: "... (armado
  INCOMPLETO: se rearma en 5 min)"). `RearmarVivaSiHaceFalta` lo revisa primero dentro de la rama de 5 min y rearma con
  motivo "el ultimo armado quedo incompleto (...)". Apagada del todo sigue reintentando cada 3 min como antes.
- Latido: cada 15 min un renglon `latido: viva ACTIVA/apagada · estado ... · cadena ... · vela N` para distinguir
  "Rithmic callado" de "instancia muerta".
- Lo de las 06:57 UTC del 18-09 (viva de ES): `viva-ES-2026-09-18.jsonl` tiene un hueco de 06:57:10 a 14:38:20 UTC
  (461 min). En el log de prod la instancia de ES arranco a las 03:54:19 local (06:54 UTC), armo bien ("EN VIVO: 102 de
  320", despues 167 y 238 de 320), escribio AUDIT a las 03:55:24, 03:56:28 y 03:57:30 local, y despues NO escribio NADA
  MAS: ni AUDIT (que sale del temporizador cada 60 s), ni viva, ni errores, mientras las dos instancias de NQ siguieron
  logueando toda la mañana. El siguiente `arranca ... raiz=ES` es a las 11:37:42 local (14:37 UTC), justo donde el viva
  vuelve a escribir. No hay ni un "no data", ni un rearme, ni un error de Rithmic en la instancia de ES: la instancia
  desaparecio entera (grafico de MES cerrado o indicador quitado), no fallo el armado. Ningun rearme cubre una instancia que
  no existe; F3 cubre los armados que fallan, y el latido deja rastro para que la proxima vez se vea en el log a que hora
  murio la instancia.
- Toca: CadenaViva.cs (~178-186 propiedades; `Arrancar` ~278-282, 302, 306-307, 369, 545, 748-757), GammaHoy.cs
  (~486 `_ultimoLatido`, ~757-766 latido en el tick, ~860-864 rearme).

### F4 - Gatillo "modelo·es10" con flecha CORTO falsa al arrancar (GatilloModelo.cs, GammaHoy.cs)

- Problema: en cada arranque a la tarde el modelo corria FRIO (sin las 60 velas previas de rango tipico y desvio de deltas:
  `rt` era el rango de UNA vela y `dz` 0) y con los niveles en NaN (zero/majors/dominantes sin cadena todavia, que entran al
  modelo como "sin nivel" = -40/+40), y salia un rombo CORTO que no era del mercado.
- Cambio: `GatilloModelo.Calentamiento` = 60 velas cerradas y `ExigirNiveles` = true (zero, major+, major- y al menos una
  dominante validos); hasta entonces `Lado` = 0 y `UltimoMotivo` dice por que. Gamma Hoy loguea una vez "gatillo modelo
  (F4): no dispara todavia: ...". La propiedad `ModoGatilloModelo` (nombre nuevo) arranca en `Ninguno` (APAGADO) con la
  explicacion en la descripcion; prenderlo es decision del operador.
- Toca: GatilloModelo.cs (~57-66 campos, ~117-126 en `Procesar`), GammaHoy.cs (~241-243 propiedad, `GatilloModeloVela`
  ~1690).

### F5 - Zero gamma interpolado como la referencia (GammaHoyNucleo.cs, GammaHoy.cs)

- Evidencia: la referencia ubica el zero en el cambio de signo del perfil POR STRIKE interpolado linealmente entre los dos
  strikes vecinos (medido 11-09 en QQQ: 715 -3,7B y 716 +2,2B => 715,62 => 29.407, contra 716,17 => 29.429 del cruce
  repreciado; y 14-09/18-09 en SPX/SPY). Nosotros lo sacabamos con `Cruce()`: la suma de gamma repreciada en una grilla de
  +-3 % (61 pasos), interpolada entre pasos de la grilla.
- Cambio: `Ajustes.ZeroInterpolado` (default true) y `ZeroPorSigno(perfil, S, porVolumen)`: entre strikes vecinos (sin los
  de GEX cero) con signo distinto, `K0 + (K1 - K0) x (-G0) / (G1 - G0)`; si hay varios cruces, el mas cercano a S. Vale
  para el zero por volumen y el de OI. Si el perfil no cambia de signo, cae a `Cruce()` y lo dice. `Lectura.ZeroModo`
  ("interp" / "cruce"), y se dice en: el AUDIT (`zeroModo=`), la cabecera ("zero interp"), la escalera ("0Γ vol·interp") y
  la estela del zero (rotulo "0Γ·interp" en la ultima vela visible). Propiedad `ZeroGammaInterpolado` (2. Lectura), la
  alternativa de siempre sigue disponible apagandola.
- Toca: GammaHoyNucleo.cs (~82-86 ajuste, ~231-254 `ZeroPorSigno`, ~366-378 eleccion, ~543 Lectura, ~559 Audit),
  GammaHoy.cs (~405-409 propiedad, `_zeroModo`/`ZeroModoCorto`, `RepreciarCon`, cabecera l2, `Escalera`, estela).

### F6 - Historia del dia para dominantes y zero (GammaHoy.cs)

- Evidencia: la referencia dibuja "Dominant 1/2 history" (azul) y "Zero gamma history" (gris): puntitos de donde estuvieron
  las dos dominantes y el zero durante TODA la sesion, por minuto (medido 14-09). Nuestra estela por vela es otra cosa (un
  guion por actualizacion y por vela, podada por cantidad de velas).
- Cambio: propiedad `HistoriaDelDia` (default false). Buffer `_historia` (SortedDictionary minuto -> zero, D1, D2), un punto
  por minuto (la ultima cuenta del minuto), tope `HistoriaTope` = 1500 minutos (25 h). Se llena en vivo desde `RepreciarCon`
  y con el archivo en HIBRIDO (`RecorrerArchivo`, apertura de cada vela). Dibujo liviano (`PintarHistoria`): una pasada por
  las velas visibles para el mapa minuto -> x, busqueda binaria por punto, cuadraditos de 2-3 px con alfa 90-120 (D1 ambar,
  D2 mas tenue, zero gris), solo desde las 18:00 de Nueva York (`InicioSesionUtc`).
- Toca: GammaHoy.cs (~447-453 propiedad, ~530-600 buffer/AnotarHistoria/InicioSesionUtc/PintarHistoria, `RepreciarCon`,
  `RecorrerArchivo` ~1200, `Pintar` antes de los big trades).

### F7 - Barras relativas (GammaHoy.cs `Pintar`)

- Evidencia: la referencia normaliza cada lado del perfil a floor(0,30 x ancho del grafico): la barra mas larga siempre mide
  eso (medido en sus capturas).
- Cambio: propiedad `BarrasRelativas` (default false). Prendida, `ancho = floor(0,30 x (xr - area.Left))` a la izquierda y
  el MISMO ancho para el perfil derecho (`anchoDer`); apagada, `AnchoBarras` (px) y el 70 % para la derecha, como siempre.
  La escala dentro del ancho sigue siendo la raiz cuadrada de |GEX| / max, como estaba.
- Toca: GammaHoy.cs (~418-422 propiedad, ~1934-1940 `ancho`/`anchoDer`, y los tres usos de `ancho * 0.7`).

### F8 - Version y este archivo

- `<Version>2.0.1</Version>` en PythiaGexDos.csproj; string de arranque `Gamma Hoy 2.0.1 (F1-F7)` (desde 2.0.2: `Gamma Hoy 2.0.2
  (F1-F7, revisado)` y `<Version>2.0.2</Version>`, para que el log diga que build corre).
- Compila con 0 errores (`dotnet build -c Release`), produccion sin tocar (`git status -- atas/PythiaGexNiveles` vacio).

### Pendiente (no incluido en 2.0.1)

- F6 solo para la primaria: las capas extra (QQQ/TQQQ/SPX/SPY/NDX/Rithmic) siguen con su estela de guiones; si se quiere la
  historia por capa, va con el mismo buffer por `CapaLibro`.
- F2: el umbral de 20 min y los 4 dias de validez de la ultima razon son elegidos, no medidos; auditar con el log
  (`razon ... (F2)`) unas ruedas y ajustarlos si hace falta.
- F5: el laboratorio (`capas_nq.py`, `auditar_vivo.py`) sigue calculando el zero por `Cruce()`; para que la paridad C#/Python
  se mantenga hay que agregarles la variante interpolada (comparar contra `zeroModo=` del AUDIT).
- No instalado ni probado en ATAS (lo hace el orquestador): falta la captura antes/despues de cada opcion.
