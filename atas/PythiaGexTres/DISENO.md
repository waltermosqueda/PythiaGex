# PythiaGex 3.0 - cadena viva por la API publica de ATAS 8.0.15

Ensamblado APARTE (`PythiaGexTres.dll`, namespace `PythiaGexTres`, categoria "PythiaGex 3.0"), creado el 06-10-2026.
Prod (`atas/PythiaGexNiveles`) y 2.0 (`atas/PythiaGexDos`) NO se tocan: de ahi solo se copiaron piezas
(Black76.cs textual; de CadenaViva.cs la hora NY, la regla del roll y los codigos de contrato).
Datos en `%APPDATA%\ATAS\PythiaGex3\`, logs `%APPDATA%\ATAS\pythiagex3-*.log`.

## Por que existe

ATAS 8.0.15.302 (29-09) limita las suscripciones a opciones hechas por fuera de su API: 200 contratos a la vez, 30
suscripciones y 2 consultas de cadena por minuto (`OptionRequestGuard`: MaxDirectOptions 200, DirectSubscribeBurst 200
con recarga 0,5/s, DirectLookupBurst 10 con recarga 1/30 s). La 1.x y la 2.0 van por fuera (SubscribeToMarketData
directo + PuenteRithmic por reflexion); el log de la plataforma del 06-10 dice "refused: 200 options are already held
outside the options API. Use IOptionsDataProvider" y el libro vivo de NQ quedo en 84-86 strikes con huecos.
Lo que entra por la API corre dentro de `OptionRequestGuard.EnterBudgetedScope` y NO cuenta en ese cupo.

## Como se resuelve cada pieza (CadenaApi.cs)

1. **Proveedor**: `Indicator.OptionsDataProvider` (propiedad protegida, `IOptionsDataProvider`). La sonda se lo pasa a
   `CadenaApi.Arrancar` como `object`. `IsAvailable` se loguea pero no decide: el proveedor resuelve el subyacente como
   la Security DEL GRAFICO (MNQZ6), y las opciones de MNQ son iliquidas (solo trimestrales).
2. **Servicio**: `OFT.Platform.Core.Providers.Options.OptionsSubscriptionService` (clase PUBLICA de OFT.Platform.Core.dll,
   referenciada con Private=false). Es un campo privado con nombre ofuscado de `IndicatorOptionsDataProvider`: se busca
   **por tipo declarado del campo** (`FieldType == typeof(OptionsSubscriptionService)`), nunca por nombre. Los otros campos
   del proveedor tambien se reconocen por tipo: `IDataFeedConnector` (el conector cacheado), `ITradingManager`,
   `Instrument` (por nombre de tipo), `IPlatformTradingCore` (por "TradingCore" en el nombre del tipo). Ojo: el
   IPlatformTradingCore tambien implementa IDataFeedConnector, por eso se compara el tipo EXACTO del campo.
3. **Conector** (`IDataFeedConnector`, el RithmicConnector), en este orden y validando cada candidato con
   `servicio.TryGetFeed(conector, out feed)` (= implementa IOptionsDataFeed + licencia + conectado):
   a. `tradingCore.GetConnector(tradingManager.Portfolio)` por reflexion (metodo publico, mismo camino que usa el proveedor);
   b. `tradingCore.GetConnector(tradingManager.Security)`;
   c. el campo `IDataFeedConnector` cacheado del proveedor;
   d. las claves de `Instrument.SecuritiesByConnector`;
   e. ultimo recurso: rastreo de campos privados del DataProvider / TradingManager a 5 niveles (como CadenaViva 1.x),
      descartando el PlatformTradingCore.
   Verificado en el descompilado: `OFT.Rithmic.RithmicConnector : BaseConnector<...>, IOptionsDataFeed` (8.0.15).
4. **Subyacente NQ**: raiz del grafico (MNQ -> NQ, MES -> ES, M2K -> RTY). Se prefiere el codigo derivado del contrato del
   grafico (MNQZ6 -> NQZ6) en `conector.Securities`; si no esta, `SearchSecuritiesAsync(Code="NQZ6", Exchange="CME")` y el
   trimestre siguiente. Al futuro se lo suscribe con `SubscribeToMarketData(Prints|Best)` (es un futuro: el guard de
   opciones no aplica) y su precio es el punto medio bid/ask (o el ultimo). Si en 15 s no llega, se usa el cierre del
   grafico y se dice en el log. Si el grafico YA es NQ/ES y no se consigue el servicio, la puerta es el proveedor directo
   (`PuertaProveedor`) y el futuro es la Security del grafico.
5. **Roll** (copiado de CadenaViva 1.10d/1.11c): `CodigoTrimestreAnterior(NQZ6) = NQU6`; si todavia no vencio (fecha NY),
   se listan tambien SUS series y se suman las fechas que falten hasta su vencimiento. Una serie es "del anterior" si vence
   ANTES del futuro viejo, o el mismo dia y es Regular (la trimestral, 9:30 NY); la Weekly de esa tarde es del nuevo. Las
   series del anterior se piden con `UnderlyingCode = NQU6` (la serie como la listo el viejo, o un clon del struct con ese
   codigo). Sus strikes se publican corridos por el spread vivo Z6 - U6 (punto medio, cuantizado a 0,25, histeresis 1 pt).

## Que se suscribe y los limites que se respetan

- Series vivas (no vencidas: 16:00 NY weekly / 9:30 NY trimestral, +30 min de gracia), las **2 fechas mas cercanas**
  (0DTE y 1DTE; el viernes de la trimestral una fecha trae dos series y entran las dos).
- Ventana por (fecha, trimestre) en strikes CRUDOS: **todos** los strikes a +-1 % del precio y **uno de cada dos** hasta
  +-2 % (paso = mediana de las distancias entre strikes; entra el strike cuyo indice es par). Tope duro **320** contratos
  por instancia (el proveedor tira InvalidOperationException a los 512; el servicio global corta en 3000): si sobra se
  cortan primero los del vencimiento lejano y los mas lejos del dinero.
- **Recentrado**: cada 5 s el latido mira si el precio se alejo mas de 0,5 % del centro; si si, y pasaron 60 s del ultimo
  rearme, se rearma: se sueltan (Dispose) SOLO los contratos que salen de la ventana y se piden los que entran. La API
  retiene lo soltado 5 s minimo (hold ladder 5 s / 30 s / 5 min / 15 min, escala si el mismo contrato se suelta y se
  vuelve a pedir en poco tiempo): por eso no se toca lo que sigue dentro.
- **Lookups**: una tanda cada 30 s por instancia como maximo, cache propio de 10 min (la API tiene cache de 10 min,
  rafaga 20 con recarga 1/s y timeout 30 s; aca se espera 35 s por llamada).
- **Espaciador global** estatico de 10 s entre tandas de suscripcion de instancias distintas (CadenaViva usaba 75 s porque
  iba directo a Rithmic; la API ya pacea con su propia rafaga de 2000 y recarga 5/s).
- Eventos `Changed`: el manejador solo guarda el `Summary` y cuenta; la IV y los conteos se hacen en el latido de 5 s y en
  `Filas()` bajo demanda. Nada de CPU por evento.
- `Parar()` = `ReleaseOwned(dueño)` + `Dispose` de cada suscripcion. El futuro NQ **no** se desuscribe (otros indicadores
  pueden estar leyendolo).

## Lo que publica

- `CadenaApi.Filas()`: SOLO contratos no vencidos (`dias > 0`; el vencido queda afuera desde el minuto cero), strike (corrido), strike
  crudo si fue corrido (0 si no, misma convencion que la viva vieja), dias con
  hora NY, es_call, OI, IV (Black-76 del punto medio), bid, ask, volHoy (`CurrentDayTotalVolume`), volAyer
  (`PrevDayTotalVolume`), last, y las banderas ConPuntas / ConResumen.
- `CadenaApi.Foto()`: via (servicio/proveedor), conector, futuro, series, contratos, suscritos, con resumen, con puntas, con
  OI, rechazadas, errores, lookups, edad del ultimo resumen.
- `SondaApi` (indicador "PythiaGex 3.0 - Sonda API"; sus ajustes se llaman `Sonda3*` para no coincidir con ningun nombre de la 2.0):
  un renglon arriba a la izquierda; cada 60 s una linea en
  `%APPDATA%\ATAS\PythiaGex3\viva\viva3-<NQ|ES>-<yyyy-MM-dd>.jsonl` (fecha UTC, como la vieja) con el formato EXACTO de
  `viva-NQ-<dia>.jsonl`:
  `{"ts":"yyyy-MM-dd HH:mm:ss","futuro":F,"grandes":0,"campos":"strike,dias,es_call,oi,iv,bid,ask,vol_hoy,vol_cinta,vol_compra,vol_venta,strike0","filas":[[K,dias,1|0,oi,iv,bid,ask,vol_hoy,0,0,0,strike0],...]}`
  (mismos formatos numericos que `GammaHoy.VivaJson`: K `0.##`, dias `0.#####`, oi `0.#`, iv `0.######`, bid/ask `0.####`;
  solo filas con puntas e IV valida; vol_cinta/vol_compra/vol_venta en 0 porque la API no trae la cinta).
- Log `%APPDATA%\ATAS\pythiagex3-sonda.log`: arranque con ajustes, campos del proveedor por tipo, puerta y camino del
  conector, futuro y precio, roll, series listadas (fecha, tipo, subyacente), contratos por serie, suscripciones
  aceptadas/rechazadas con el mensaje de la excepcion, PRIMER Summary campo por campo (y las puntas del Security al lado,
  para saber por donde llegan), una linea por minuto con los conteos, y toda excepcion con su pila.

## Lo que falta verificar con ATAS corriendo (no se pudo sin la plataforma)

1. Que `OptionsDataProvider` no sea null en un indicador de usuario y que el campo de tipo `OptionsSubscriptionService`
   este (el log dice "campos del proveedor: ..."). Si no esta, la via es el proveedor directo y hay que abrir la sonda en
   un grafico de NQ (no MNQ).
2. Que `TryGetFeed` acepte al RithmicConnector: depende de `IAccountInfoProvider.IsSupportedOptionsApi()` (la licencia
   "full" que le activaron). Si devuelve false, `GetOptionSeriesAsync` vuelve vacio sin excepcion: el log lo mostraria como
   "0 series para NQZ6".
3. **Las puntas**: la API suscribe SOLO `SubscriptionType.Summary`. Hay que mirar en el log el "PRIMER resumen": si
   `bid/ask` vienen en el Summary o solo en el Security (la linea por minuto cuenta "conPuntas=N (resumen R, soloSecurity
   S)"). Si ninguno trae puntas, no hay IV y el viva3 sale vacio: habria que sumar una suscripcion `Best` al conector
   dentro del cupo de 200 "outside" (solo al dinero) o pedirle a ATAS el campo.
4. El roll con la serie clonada (`UnderlyingCode = U6`): verificado en el descompilado de OFT.Rithmic 8.0.15 que
   `GetOptionsAsync(series)` manda `series.UnderlyingCode`, `series.Exchange` y `series.Expiration` (un solo formato de
   fecha; el PuenteRithmic viejo probaba yyyyMMdd y despues yyyyMM). Falta ver en una semana de roll que devuelva los
   contratos y no "no data". Hasta diciembre no se puede medir.
5. La hora de las series: `OptionSeries.Expiration` es fecha; la convencion 16:00 / 9:30 NY es nuestra (copiada de 2.0).
6. Rechazos: si aparece InvalidOperationException "... 512 ..." o "drained" en el log de suscripcion, el tope hay que
   bajarlo o el indicador se esta instanciando dos veces (ATAS crea instancias extra al abrir la ventana de ajustes).
7. Que `RedrawChart` y `OnRender` dibujen el renglon (ChartArea mas alto que lo visible: ver memoria).
8. Convivencia con prod/2.0 en el mismo ATAS: 3.0 no toca `SubscribeToMarketData` de opciones, asi que no les corta
   puntas; pero las suscripciones de 1.x/2.0 por fuera siguen contando en el cupo de 200 y se seguiran rechazando.

## Instalacion (la hace el orquestador, no este paso)

Copiar `bin\Release\PythiaGexTres.dll` a `%APPDATA%\ATAS\Indicators\` y reiniciar ATAS. Agregar "PythiaGex 3.0 - Sonda API"
en UN grafico (MNQ o NQ) y leer `pythiagex3-sonda.log` a los 2 minutos.

## Verificado 06-10 16:10 (orquestador) en OFT.Rithmic 8.0.15 descompilado

`RithmicConnector.SubscribeToMarketData`: `SubscriptionType.Summary` se traduce a `SubscriptionFlags.Prints | Best | Close | Settlement | Open | HighLow | TradeVolume | OpenInterest`, y el manejador de BBO escribe `BestBidPrice/BestBidVolume/BestAskPrice/BestAskVolume` en el `SecuritySummary`. O sea: la suscripcion por la API (solo Summary) TRAE las puntas, el ultimo, el volumen del dia y el OI. El riesgo 3 de arriba queda resuelto en papel; falta verlo en el log.

## Gamma Hoy 3.0 (GammaHoyTres.cs, 06-10-2026) - "PythiaGex 3.0 - Gamma Hoy"

Corre AL LADO de la clasica (1.11d) y la 2.0: otro ensamblado (`PythiaGexTres.dll`), otros nombres de ajustes (todos con "3"),
otros archivos. La CUENTA es la misma: `GammaHoyNucleo.cs` es copia textual del de la 2.0 (y `Feed.cs` lo minimo que el nucleo
necesita, `Centinela.cs` copia con prefijo `pythiagex3-`); lo nuevo es la FUENTE (CadenaApi, la API publica de ATAS 8.0.15) y el
DIBUJO (pliego de `laboratorio/tres/resultados/censo_capturas.md`, seccion 5). Version 3.0.0.

### Que calcula

- La cadena del nucleo se arma desde `CadenaApi.Filas()`: una `Feed.Fila` por (strike corrido, vencimiento) con call y put juntos
  (IV de cada lado despejada con Black-76 del punto medio, OI, `vol_hoy` = CurrentDayTotalVolume), `Dias` por vencimiento con hora NY,
  `EsFuturo = true`, sin base (el libro es del propio futuro). Entran solo las filas con puntas e IV valida y con OI o volumen; hacen
  falta 8 strikes con IV en call Y put (`MIN_STRIKES_UTILES`) o no hay cuenta ("SIN CADENA · N strikes con IV").
- El precio del futuro para la cuenta es el DEL GRAFICO (ultima vela): MNQ cotiza los mismos puntos que NQ, igual que la capa NQ de
  la 2.0. El precio que la API da para NQ queda en el AUDIT (`futuroApi=`) para compararlos. Si la raiz del grafico no es NQ/ES/RTY
  no hace nada y lo dice en pantalla.
- Ajustes del nucleo: Horizonte Hoy (0DTE), 2 dominantes, radio 2 % con tope en puntos 100 NQ / 25 ES / 20 RTY, empate 20 %,
  DominantesDeNoche = Volumen (REGLA DEL OPERADOR 17-09, el default no se cambia sin aviso y captura), ZeroInterpolado = true.
- Regla de dominantes (`Regla3Dominantes`, enum `ReglaDominantes3`): **Clasica** = una por lado + empate 20 % + centroide 12 pts
  (1.11d); **DosMasGrandes** = las dos barras mas largas en valor absoluto, strike exacto (2.0.5); **Tres** (default desde 3.0.4,
  06-10) = la clasica con HISTERESIS: la retadora reemplaza a la vigente mas debil solo si la supera `Histeresis3Pct` (25 %) durante
  `Histeresis3Min` (5) minutos seguidos; raya en el centroide de `Histeresis3CentroidePts` (6). Es la V22 de F3 del laboratorio
  (`GammaHoyTresHisteresis.cs`, calco de `F3_estabilidad.hacer_regla`): confirmacion +7,1 pp z 1,47 (pide 1,5): SIN VALIDAR; lo que si
  mejoro en las dos muestras es el ruido (2,8 cambios/h contra 10) y la penetracion (12,9 contra 22,7 pts). Estado vacio a las 09:30 y
  18:00 NY; un paso por minuto; la memoria rebobina viva3 con la misma histeresis y el vivo adopta ese estado al arrancar.
- Repreciado cada 5 s desde el temporizador (con el mercado cerrado no hay ticks). La vela CERRADA (estela, toque, centinela) se anota
  en `RegistrarVelaCerrada`, que llaman `OnCalculate` SOLO en el borde vivo (`bar == CurrentBar - 1`, la guarda de la 2.0: en un
  recorrido historico tras una recarga/reconexion/cambio de marco no se anotan velas viejas con los niveles de ahora) y el
  temporizador con `CurrentBar - 2` (en una pestaña oculta ATAS no manda OnCalculate, medido 15-09). La guarda es por HORA de la vela,
  no por numero de barra, y la estela esta indexada por hora: una reindexacion no repite ni corre los guiones. Si hubo un hueco, las
  velas del medio quedan sin guion (nunca con uno falso). Con la cuenta de mas de 3 min, el centinela anota la vela con `niv` vacio y
  `spot` null, y no hay guion.
- El 0DTE VENCIDO sale del libro desde el minuto cero (`CadenaApi.Filas`: `dias <= 0` queda afuera; la suscripcion sigue 30 min de
  gracia). La 2.0 lo dejaba 30 min con `dias = 0` y T = 1 min (trampa de CLAUDE.md); entre 16:00 y 16:30 NY la 3.0 y la 2.0 van a
  diferir A PROPOSITO. El conteo va en el log (`vencidasFuera=`) y en `Foto().Vencidas`.
- Log `%APPDATA%\ATAS\pythiagex3-gammahoy.log`: arranque con ajustes efectivos, una linea por minuto con los conteos de la API, y una
  vez por minuto `AUDIT3 regla=<regla> fut=... S=... strikes=... zeroVol=... doms=... libroDom=... ` (la misma linea `Audit` del
  nucleo que escriben la clasica y la 2.0, mas `strikesUtiles`, `suscritos`, `conPuntas`, `conOI`, `edadDato`, `dias`,
  `precioGrafico`, `futuroApi`, `via`). Toda excepcion con pila.

### Que dibuja (perfil Limpio, el default)

1. **D1 y D2**: estela de guiones cortos por vela, uno por vela CERRADA en el strike dominante al cierre de esa vela; 2 px de alto,
   largo = ancho de vela - 1 px, amarillo #E8C547; alpha 100 % las ultimas 30 velas, 60 % el resto, 30 % si el nivel esta a mas del
   radio de dibujo (150 pts NQ / 40 ES) del precio. Nada proyectado a la derecha del precio. Largo hacia atras: la sesion desde las
   18:00 NY de la vispera (`Estela3VelasAtras` = 0) o N velas.
2. **Zero Gamma**: UNA linea punteada fina (1 px, 3 px raya / 3 px hueco, dibujada a mano porque RenderPen no trae ese patron) gris
   claro #C8C8C8 que cruza el grafico.
3. **Tunel**: rectangulo amarillo al 12 % entre D1 y D2 vigentes, desde la ultima vela hasta el eje, solo si las dos estan a <= radio
   de dibujo.
4. **Marca de toque**: bloque de una vela de ancho y 2 ticks de alto, amarillo 100 %, en la PRIMERA vela cerrada que ENTRA en la banda
   [D - ZONA, D + ZONA] de una dominante vigente (la anterior no estaba en la banda; una vela apoyada en la banda no vuelve a marcar).
   Un toque por (dominante, lado) hasta que una vela cierre a >= LEJOS del nivel del lado por donde llego (rearme). Es la definicion
   del pre-registro del laboratorio (`laboratorio/tres/PRE_REGISTRO.md`, seccion 3), para que lo que se ve coincida con lo que el juez
   cuenta. ZONA = la TOL del pre-registro (2,5 pts NQ / 0,75 ES / 0,6 RTY), NO un valor medido: la penetracion p75 medida en F3
   (`resultados/familias/F3.json`) es ~20 pts de noche en NQ vivo y la zona sugerida ~45; se eligio la TOL para que el juez y el
   dibujo hablen de lo mismo. LEJOS = 15 NQ / 4 ES / 3 RTY (`Lejos3Pts`). La ventana de 30 min del "viniendo de lejos" del juez NO
   esta en el dibujo (el estado del rearme no caduca).
5. **Rotulos**: una columna pegada al borde derecho del lienzo, formato `NQ D1 ▲ 31.545 +12` (precio al tick con separador de miles,
   distancia en puntos con signo; `NQ 0Γ 31.500` sin distancia), chips con una pestaña del color del nivel, anti-solape en escalera
   con un pelito al precio real, tope 5 (hasta 7); niveles fuera de pantalla como rotulo con flecha en el borde, uno por lado (el mas
   cercano). OJO (medido 07-09, memoria `atas-tabs-por-uia-y-eje`): el lienzo del indicador NO llega al eje de precio nativo (queda
   fuera del recorte), asi que "dentro del margen del eje" no se puede desde OnRender: la columna va pegada al borde derecho del
   lienzo, como la escalera de la 2.0.
6. **Cabecera**: UN renglon arriba a la izquierda, debajo del OHLC nativo (y = Top + 26):
   `PythiaGex 3.0 · NQ vivo (API) · 86 strikes · dato 0:04 · 0Γ 31.500 · D1 31.545 +12 · D2 31.500 -33 · regla DosMasGrandes`.
   La edad del dato va ANTES de los numeros y en naranja si pasa de 30 min (es el tiempo desde el ultimo Summary que MANDO la API; el
   snapshot inicial de `Subscribe` no cuenta, puede ser cache). Debajo, cuando hace falta, un cartel de UNA linea con color: `SIN CADENA
   · ...` (rojo, con el motivo de la API o el conteo de strikes), `LIBRO VIVO CAIDO hace N min · dibujo la ultima cuenta` (naranja: sin
   resumen hace mas de 5 min CON el mercado abierto segun el reloj de NY), `MERCADO CERRADO (NY dia hh:mm) · ultimo dato hace N ·
   dibujo la ultima cuenta` (gris: sabado, domingo antes de las 18:00, pausa 17:00-18:00; los feriados de CME no los conoce el reloj y
   ese dia dice CAIDO), `SIN CUENTA hace N min` (naranja). El cartel se dibuja aunque la cabecera este en No; el renglon no.
7. **Majors** (Major Positive verde #089981 / Major Negative rojo #f23645, 1 px): apagados por defecto en NQ, prendidos en ES; la
   raya solo dentro del radio de dibujo, fuera solo el rotulo.
8. **Barras del perfil**: `Barras3Lado` ninguna (default) / izquierda: ancho maximo 12 % del lienzo, alpha 50 %, alto 10 px (o el
   espacio entre strikes menos 1), sin pelotitas ni montos.

NADA de pelotitas, gatillos, capas, rombos, nubes, perfil derecho, Max Change en el lienzo ni textos de depuracion.

### Ajustes (nombre en ATAS -> propiedad, default)

Grupo 1. Libro: Raiz manual (`Raiz3Manual`, vacio), Tope de contratos (`Tope3Contratos`, 320), Ventana densa % (`Ventana3DensaPct`, 1),
Ventana rala % (`Ventana3RalaPct`, 2), Vencimientos (`Vencimientos3`, 2), Recentrar % (`Recentrar3Pct`, 0,5).
Grupo 2. Lectura: Regla de dominantes (`Regla3`, DosMasGrandes), Horizonte (`Horizonte3`, Hoy), Radio dominantes pts
(`Radio3DominantesPts`, 0 = auto 100/25/20), Empate % (`Empate3Pct`, 20), Dominantes de noche (`Noche3Dominantes`, Volumen), Zero
interpolado (`Zero3Interpolado`, true).
Grupo 3. Pantalla: Perfil visual (`Perfil3Visual`, Limpio | ConMajors | Todo; fija los defaults de las casillas sin tocarlas), y
las casillas tri-estado SegunPerfil/Si/No: `Ver3Estela`, `Ver3Zero`, `Ver3Tunel`, `Ver3Toques`, `Ver3Majors` (Limpio: ES si, NQ no),
`Ver3Rotulos`, `Ver3Cabecera`; `Barras3Lado` (SegunPerfil: solo Todo = izquierda); Radio de dibujo (`Radio3DibujoPts`, 0 = auto
150/40/30); Zona de toque (`Zona3ToquePts`, 0 = auto 2,5/0,75/0,6 = TOL del pre-registro); Rearme del toque (`Lejos3Pts`, 0 = auto
15/4/3 = LEJOS del pre-registro); Tope de rotulos (`Tope3Rotulos`, 5, max 7); Estela velas atras
(`Estela3VelasAtras`, 0 = sesion); Tunel alpha (`Tunel3AlphaPct`, 12); Barras ancho % (`Barras3AnchoPct`, 12); Barras alpha
(`Barras3AlphaPct`, 50); Tamaño de letra (`Tam3Letra`, 9); Margen inferior (`Margen3Inferior`, 48).
Grupo 4. Archivo: `Guardar3Estela` (true), `Guardar3Centinela` (true), `Guardar3Viva` (true). Grupo 5. Accion: `Rearmar3Ahora`.

### Archivos (formatos EXACTOS, para que el laboratorio mida 3.0 contra la clasica y la 2.0)

- Estela: `%APPDATA%\ATAS\PythiaGex3\estela\estela-<NQ|ES>-<yyyy-MM-dd UTC>.jsonl`, una linea cuando cambian D1/D2/zero o cada 60 s:
  `{"t":"yyyy-MM-ddTHH:mm:ssZ","d":[D1,D2,zero],"g":[M,M],"n":netVolM,"b":"vol"|"OI","f":futuro}` (d y f con `0.00`, g = |GEX|/1e6
  de D1 y D2 con `0`, n = net GEX por volumen /1e6; identico a `GammaHoyCapas.GuardarGuionCapa`). Un solo escritor estatico por raiz:
  si otro grafico (u otra instancia) escribio hace < 180 s, no se escribe.
- Centinela: `%APPDATA%\ATAS\pythiagex3-centinela-hoy-<instrumento>-<marco>.jsonl`, una linea por vela CERRADA:
  `{"t":"yyyy-MM-ddTHH:mm:ss"(UTC),"o","h","l","c","vol","ops","delta","spot":<precio del grafico>,"niv":{"dom0","dom1","zero_vol",
  "zero_oi","mp_vol","mn_vol"}}` (Centinela.cs de la 2.0 tal cual; `ops` = Ticks de la vela).
- Viva: `%APPDATA%\ATAS\PythiaGex3\viva\viva3-<raiz>-<dia UTC>.jsonl` cada 60 s, el mismo renglon que la sonda (factorizado en
  `Viva3.cs`; un solo escritor por raiz: si Gamma Hoy 3.0 escribe, la sonda no duplica, y al reves).

### Dos instancias en el mismo grafico

ATAS instancia el indicador de nuevo al abrir la ventana de ajustes. Cada instancia arma su propia CadenaApi (el espaciador global
de 10 s entre tandas ya existe) y los archivos de estela y viva tienen escritor unico por raiz; el centinela es por archivo
(instrumento + marco) y cada instancia tiene su guarda por hora de vela, pero dos instancias vivas en el mismo grafico escribirian la
misma vela dos veces: si pasa, se ve en el archivo y se arregla con un escritor unico tambien ahi.

### Que NO hace todavia

- CBOE de respaldo cuando el libro vivo se cae: hoy dibuja la ultima cuenta y lo dice en naranja ("LIBRO VIVO CAIDO hace N min").
- La regla `Tres`: reservada, = DosMasGrandes. Vendra de la fase 2 del laboratorio como otra opcion del mismo ajuste.
- MES sin API: si en un grafico de MES no hay opciones de ES por la API (sin puerta, sin series, o menos de 8 strikes con IV), queda
  "SIN CADENA" en rojo; no hay otra fuente en 3.0.
- Capas secundarias (NDX/QQQ/SPX/SPY), fusion de rotulos entre libros, tooltip didactico, estela del zero historico, gatillos,
  Max Change en la cabecera, estado de regimen (rango/expansion): nada de eso existe en 3.0.0.
- Verificar en pantalla (no se pudo sin ATAS): que la estela a 2 px se lea igual en 1m/2m/5m, que el ruido del default salga en 2 con
  el mismo juez del censo, y que `FirstVisibleBarNumber`/`LastVisibleBarNumber` esten bien en el primer render.
