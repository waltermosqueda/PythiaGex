# Gamma Hoy — el indicador nuevo (2026-09-07)

Pedido del operador: no seguir atado a Gamma Vivo; construir uno nuevo con
libertad de arquitectura, ponerlos uno al lado del otro y ver si se acerca a
GAMMAlito y si se lo puede superar en exactitud, con miles de pruebas.

## Que manda (lo aprendido de 30 videos y del laboratorio)

1. El mapa del dia es el de VOLUMEN (GAMMAlito: bloques "Volume" y "Open
   Interest" en su panel; sus barras respiran por volumen). Laboratorio:
   volumen puro +42 pp y gamma x volumen +22 pp contra placebo; gamma x OI
   (lo que dibuja Gamma Vivo) -4 pp.
2. Dominante = la barra mas larga del libro de volumen; dos por defecto.
3. Max Change: la punta de cada barra hace 15/5/1 min (pelotitas) y el
   strike con mayor cambio de GEX en 1/5/10/15/30 min. "Lo adelantado".
4. Convexity Ladder = aceleracion por strike; cuadrante de regimen (mucho o
   poco GEX cerca del precio x convexidad positiva o negativa) y alerta de
   transicion al perder el maximo GEX con convexidad negativa.
5. Big Trades = bloques de OPCIONES con tamaño; aca del tape de Rithmic.
6. Fuente y edad siempre a la vista; todo anotado por vela para el
   laboratorio (placebo afuera, nunca adentro del indicador).

## Arquitectura

- Mismo ensamblado (PythiaGexNiveles.dll): los dos indicadores conviven en
  la lista de ATAS y se pueden poner en el mismo grafico.
- Reutilizado tal cual: `CadenaViva` (opciones de ES por Rithmic; se le
  agrego la captura de bloques grandes), `Black76`, `Reloj`, `Centinela`.
- Nuevo: `Feed.cs` (cadena de CBOE via radar.py, sin dibujo ni logica) y
  `GammaHoy.cs` (cuenta, lectura y dibujo).
- Estado bajo una sola llave; `OnCalculate` reprecia por vela y por tick de
  la ultima; `OnRender` copia el estado y dibuja.

## Que calcula

- Por strike, dentro del horizonte elegido (Hoy = 0DTE o el mas cercano):
  GEX por OI, GEX por volumen (mismo gamma, ponderado por VolC/VolP), y
  convexidad (ΔGEX si S sube 1 %) del libro elegido (Auto: volumen si su
  gamma total llega al 20 % del de OI).
- Por libro: neto, zero gamma (grilla ±3 %, 60 pasos, interpolado), majors.
- Dominantes: top-N por |GEX vol| dentro del radio (2 %); si no hay volumen
  (noche), por OI y se rotula.
- Max Change: fotos por minuto del GEX por volumen (40 min de memoria) ->
  por ventana el strike con mayor cambio; y por barra la punta hace 15/5/1.
- Cuadrante: pico = max |GEX| a ±0,35 % del precio; "mucho" si >= 50 % del
  maximo del libro; convexidad en el precio = suma de la convexidad de los
  strikes en ese radio. Transicion: cambio de lado del maximo GEX con
  convexidad negativa -> alerta 10 min.
- Centinela: `pythiagex-centinela-hoy-<inst>-<marco>.jsonl` con zero_vol,
  zero_oi, mp/mn de cada libro, dom0..N, mc1/5/10/15/30, pico, q_cuadrante.
  Mismo formato que el de Gamma Vivo: los scripts del laboratorio corren
  sobre los dos y se comparan.

## Dibujo (v0.1)

Izquierda: barras del volumen (verde/rojo, raiz cuadrada) con la sombra del
OI de ayer detras y las tres pelotitas del Max Change en la punta.
Derecha: Convexity Ladder (aguamarina/purpura) y la escalera pegada al eje
con net de cada libro, los Max Change por ventana y las filas a la altura de
su precio (0Γ vol, 0Γ ayer, +Γ, −Γ, D1, D2, precio).
Rayas: zero vol (rayado), zero OI (punteado gris), majors del volumen,
dominantes (amarillo) con su estela por vela. Big trades como circulos con
el tamaño. Cabecera: cuadrante, convexidad en el precio, pico, fuentes y
edades, y la alerta de transicion.

## Compuertas (nada se declara mejor sin esto)

- `laboratorio/es_vs_nq.py` y `tape_en_nivel.py` sobre los dos jsonl:
  respeto y actividad de dom0/dom1 de Gamma Hoy contra los de Gamma Vivo,
  cada uno contra su placebo, 15 strikes distintos por instrumento.
- Cuadrante: rango de las velas siguientes y tasa de reversion por cuadrante
  contra barajado.
- Max Change: el strike con mayor cambio a 30 min termina dominante mas
  veces que uno al azar?
- Big trades: actividad del futuro en los minutos siguientes contra minutos
  sin bloque.

## Pendiente conocido

- Libro de QQQ/SPY como fuente alternativa (el feed solo sirve SPX/NDX/RUT).
- Chance de toque en las filas (esta en Gamma Vivo, falta aca).
- Calibrar el umbral de big trade en ES midiendo la distribucion de tamaños.

## Rebobina: el simulador afuera de ATAS (2026-09-07)

El operador no quiere meterle archivos de afuera a ATAS ("apenas anda"). El
backtest entonces corre en una consola aparte, `atas/Rebobina`, que compila
LOS MISMOS archivos fuente del indicador: `GammaHoyNucleo.cs` (toda la
cuenta, sin una referencia a ATAS), `Feed.cs` (lector de cadenas) y
`Centinela.cs` (la anotacion por vela). El indicador quedo reducido a elegir
cadena, precio y hora, y dibujar. Si la cuenta cambia, cambia en los dos.

Datos (gratis con el credito de Databento, ledger y techo en
`herramientas/databento_bajar.py`):
- Futuro: ES.FUT ohlcv-1m (3 meses, USD 0,55) -> `datos/simulador/velas/ESU6-1m.csv`.
- Cadena SPX/SPXW por minuto: OPRA definition + statistics (OI, stat_type 9,
  coincide 100 % con CBOE) + ohlcv-1m (volumen por contrato y minuto; al
  mismo corte 82 % exacto contra CBOE, suma 0,91) -> `herramientas/
  databento_a_cadenas.py` arma `datos/simulador/cadenas/sim-ES-<dia>-r<retraso>.jsonl.gz`
  con el mismo formato que archiva la nube. IV: del ultimo precio operado
  (Black-Scholes invertido) e interpolada por strike donde no opero. Base:
  medida del dia si la hay, si no carry teorico (tasa - 1,2 % dividendos).
- El retraso de CBOE (902 s) es un parametro: con r0 se mide lo que cuesta.

Corrida: `Rebobina --cadenas ... --velas ... --instrumento MES --marco M1`
-> `%APPDATA%\ATAS\pythiagex-centinela-rebobinado-MES-TimeFrame-M1.jsonl`,
que `laboratorio/rebobinado.py` juzga contra placebo. 2026-09-03: 451 velas
en 2,7 s; un dia son 6 strikes distintos: hacen falta 15+ ruedas.

Hallazgo del primer rebobinado, ya corregido: la alerta de TRANSICION se
disparaba a cada minuto cuando dos strikes vecinos se alternaban el maximo
GEX (7.759/7.764 el 09-03 de 19:16 a 20:16). Ahora el maximo es una zona
(strikes con >= 80 % del maximo, banda de 2,5 puntos) y se grita una vez
cada 10 minutos: el 09-03 paso de ~40 alertas a 6.

Equivalencia con ATAS: `Rebobina --prueba <cadena.json> --precio 7709` da la
misma linea AUDIT que dejo el indicador vivo con esa cadena (2026-09-07:
netVol -9.327B, netOi -4.629B, zeroVol 7718.89, zeroOi 7717.06, majors
7726.08/7706.08, doms, q=1, pico 7706.08). El DLL recompilado con el nucleo
NO se instalo en ATAS todavia: se instala con mercado cerrado y aviso.

## El panel local y Gamma Vivo en el simulador (2026-09-07, tarde)

El operador eligio "panel local en la PC" y pidio todo: parametros, carga de
archivos y Gamma Vivo al lado de Gamma Hoy.

- `herramientas/panel_local.py` (+ `.html`) sirve http://127.0.0.1:8770.
  Lanzador: `ATAS nada/Rebobina Panel.bat`. Parametros del motor (los mismos
  que muestra ATAS), ruedas disponibles, arrastrar `.dbn.zst` de Databento
  (se ubican por su metadata) o `.csv` de velas, cotizar/bajar de Databento
  con el techo del ledger, "Simular" (convierte lo que falte, corre Rebobina,
  laboratorio contra placebo, regenera el visor), visor embebido.
- `GammaVivoNucleo.cs` reproduce lo que Gamma Vivo ANOTA (zero y muros del OI
  a 7 dias, picos modo 1 = dominantes del centinela), copiado de GammaVivo.cs
  con las lineas citadas en el fuente. No es el nucleo entero de ese
  indicador. `Rebobina --indicadores hoy+vivo` escribe el centinela
  `rebobinado-vivo-<inst>`; el visor tiene la capa "Gamma Vivo" y "ambos".
- 13 ruedas (08-19 a 09-04, velas de 1 min, cadena <= 20 min): Gamma Hoy
  dominantes por volumen -10,4 pp contra placebo, majors por volumen +6,7, por
  OI +4,3, max change 0, pico -6; Gamma Vivo dominantes (8, OI) -3,4, (2, OI)
  -7,1, muros +0,4, zero casi sin toques.

## Fuente = Archivo: el rebobinado adentro de ATAS (2026-09-07, noche)

Con el OK del operador ("dale hacelo ahora") se instalo Gamma Hoy 0.3b y se
probo en vivo en su ATAS, en el grafico limpio MES 5m:

- Ajuste "Fuente" = Archivo. El indicador no baja feed ni abre la cadena viva:
  carga las cadenas por dia de `%APPDATA%\ATAS\PythiaGex\cadenas` (13 dias de
  Databento copiados como cadena-ES-<dia>.jsonl.gz, lo que grabo la maquina
  como local-ES-<dia>.jsonl y lo que baja de la nube), y con
  `RecalculateValues()` recorre toda la historia cargada: 5.472 velas de 5 min
  en 6 segundos, 3.031 con cadena. Centinela `rebobinado-atas-<inst>`.
- Con el mercado cerrado ATAS no llama a OnCalculate: la carga se dispara desde
  el temporizador (0.3b). Antes quedaba "esperando la primera vela".
- La escalera y las rayas siguen la vela bajo el mouse (BarBelowMouse): la raya
  nace en esa vela con un punto. La cabecera dice la hora de la vela y cuando
  se publico la cadena que se uso.
- "Archivo: edad maxima" = 20 h reproduce la noche (CBOE congelada, el vivo
  sigue mostrando la ultima cadena). Los 13 dias de Databento solo tienen
  cadena en la rueda americana: de noche se ve la ultima del dia. Desde el
  2026-09-07 la nube archiva cada 5 min (rueda) y cada 30 (Asia/Europa).
- Cualquier temporalidad: la cuenta es por vela, con la cadena vigente al
  cierre de esa vela. Con 1 minuto y 20 dias son ~27.000 cuentas (~1 min).
- Reloj.cs: el ReceiveAsync que vencia quedaba sin observar y ATAS mostraba
  "Unobserved task exception" al arrancar; corregido.

Cruce CBOE vs Databento del 09-03 (laboratorio/cruzar_fuentes.py, 451 min):
spot igual; +Γ vol, dom0, pico, max change 30' coinciden (mediana 0,1 pts,
62-71 % a 2,5 pts); cuadrante igual 94 %. Difieren zero vol (3,4), zero OI
(5,4), -Γ vol (10) y -Γ OI (45): la IV de los puts lejanos sin operar esta
interpolada. Mejora pendiente: quotes cbbo-1m en ventanas de 1 minuto cada 15.

## Re-auditoria visual contra los videos de GAMMAlito (2026-09-07, noche)

Preguntas del operador: por que en GAMMAlito los puntos dominantes parecen
una "nube" y aca salen en filas horizontales; si las barras laterales se
mueven igual; que falta. Cuadros revisados: maxchange (NinjaTrader),
Big Trades (web ES_SPY 2m), Las Dominantes.

1. LA "NUBE" SON TRES COSAS JUNTAS, NINGUNA ES UNA DOMINANTE DISTINTA:
   - hasta 5 dominantes por vela (la web trae 2 por defecto, hasta 5), cada
     una un guion amarillo corto; cuando el ranking se reacomoda, los guiones
     saltan de altura y quedan desparramados;
   - el libro es SPY (strikes de 1 dolar = ~10 puntos de ES): cada salto se
     ve el doble de grande que con SPX (5 puntos);
   - los Big Trades: burbujas verdes/rojas con el tamaño (452, 340, 384)
     sobre la vela del momento, a la altura del precio del futuro. En el
     corto "Big Trades" eso es lo que llena la pantalla.
   Nosotros teniamos 2 dominantes, feed cada 5 min y strikes de SPX: filas.
   Hecho en 0.6: guiones por vela (primaria gruesa, secundaria fina), feed
   por minuto (rama cadenas, ultima-<raiz>.json), semillas del Max Change
   (30/5/1) y zero por vela como puntos. Falta que el operador suba
   "Dominantes" a 3-5 si quiere la nube. Los Big Trades de SPY/SPX no los
   tenemos: solo el tape de opciones de ES por Rithmic (mucho mas fino);
   la cinta de OPRA en vivo es paga.
2. LAS BARRAS LATERALES: en los cuadros de maxchange el perfil izquierdo
   (verde/rojo) cambia de largo minuto a minuto y el derecho (aguamarina/
   violeta) casi no cambia. Coincide con lo medido en la anatomia: el
   izquierdo respira por VOLUMEN del dia, el derecho es convexidad hasta 90
   DTE. Las nuestras: izquierda GEX por volumen con sombra de OI (respira
   igual, pero antes cada 5 min: ahora por minuto), derecha convexidad del
   MISMO horizonte que el mapa (Hoy). Diferencia real: su ladder derecho
   mira toda la cadena; el nuestro, el 0DTE. Pendiente: horizonte propio
   para la convexidad (Todo) en el nucleo.
3. QUE FALTA TODAVIA (critico): (a) Big Trades del libro SPX/SPY (no hay
   fuente gratis en vivo); (b) convexidad a 90 DTE; (c) medir con el
   rebobinado si "semilla alineada 45 min -> dominante" se cumple (Max
   Change predice dominante): es medible con mc30 y dom0 del centinela y no
   se hizo; (d) las zonas de dominancia como banda (la web pinta una franja
   amarilla alrededor de la dominante), hoy solo la raya.

## Medido en los videos, no mirado: como dibuja GAMMAlito (2026-09-07, noche)

El operador desconfio con razon: "varias dominantes por instante, arriba y
abajo, como una nube; y las barras se mueven en vertical". Se midio con
`herramientas/analizar_guiones.py` (OpenCV: componentes amarillas anchas y
bajas = guiones; barras verdes/rojas del perfil izquierdo; seguimiento cuadro
a cuadro) sobre cuatro videos: "Miren esto en vivo" (NinjaTrader MNQ 1 min,
145 cuadros), "Las dominantes actuan de iman" (NinjaTrader NQ, 109), "Big
Trades" (web ES_SPY 2 min, 160) y "Que es GAMMAlito powered by Gexbot" (web,
900 cuadros). Cuadros anotados en datos/simulador/guiones/.

1. GUIONES POR VELA: mediana 1, pero en las velas recientes hasta 4 (Miren),
   3 (Las), 6 (Big Trades, con ruido de rotulos). O sea: un guion por cada
   ACTUALIZACION del calculo, no uno por vela. Con 1 minuto y varias
   actualizaciones por minuto, quedan varios guiones a alturas distintas.
2. LA "NUBE" ES UNA BANDA: en "Las dominantes" los guiones forman una franja
   de ~12 px de alto con el eje a 25 px por 10 puntos de NQ = ~5 puntos de NQ.
   Los strikes de NQ van de 10 en 10 y los de QQQ cada ~41: la dominante NO
   esta clavada en un strike, ondula. Coincide con lo medido antes en 284
   lineas del producto (ninguna plana). La cuenta que da eso es un promedio
   de precio ponderado por gamma alrededor del pico (centroide), no el argmax.
3. DOS COLORES: guiones de hue 29 (amarillo) cerca del precio y de hue 19
   (naranja) en la fila de abajo: primaria y secundaria (o reciente/vieja).
4. LAS BARRAS NO SE MUEVEN EN VERTICAL: movimiento propio de una barra
   (quitado el corrimiento comun del grafico) |dy| mediana 0,00-0,23 px, p90
   0,04-1,9 px en los cuatro videos; lo que cambia es el LARGO (p90 6-77 px).
   El "sube y baja" que se ve es la autoescala/scroll del grafico (corrimiento
   comun p90 hasta 8,9 px en el clip web) y barras que aparecen/desaparecen.
   Las barras llevan tres pelotitas en la punta (grande 15 min, mediana 5,
   chica 1): confirmado en el recorte de NinjaTrader.
5. Gamma Hoy 0.7 lo aplica: dominante como centroide (radio 12 pts, ajuste
   "Dominante como centroide"), un guion por cada actualizacion (hasta 24 por
   vela, solo si se movio > 0,25 pt), primaria amarilla y secundaria naranja;
   en el rebobinado, un guion por cada cadena que llego durante la vela.
   Todo medible: el centinela sigue anotando dom0/dom1 por vela.

Lo que sigue sin fuente: los Big Trades de SPY (burbujas verdes con el
tamaño, 326/340/384/452 en el clip): OPRA en vivo es paga.

## 0.8: la memoria del pasado en todos los modos (2026-09-07, noche)

El operador saco y volvio a poner el indicador y no aparecio el pasado. Causa:
al agregarlo de nuevo ATAS lo crea con los ajustes por defecto, y el defecto
era Fuente = Vivo, que no rellenaba la historia (solo Archivo e Hibrido). La
memoria existe (archivo de cadenas por dia: nube por minuto + local); ese modo
no la usaba. Ahora: Fuente por defecto = Hibrido, y Vivo tambien carga el
archivo al arrancar y recorre las velas cargadas (guiones, semillas, fotos,
centinela rebobinado-atas). Ademas cada foto guarda el perfil (las barras de
ese minuto): con el mouse sobre una vela se ven sus niveles Y sus barras.
Lo que no hay: el pasado anterior al 19 de agosto y las madrugadas de los 13
dias de Databento (solo rueda americana); esas velas quedan sin dibujo.

## 0.9: los datos adentro de las barras (2026-09-08, madrugada)

Pedido: que las barras no queden "olvidadas": cada una con su dato, abreviado,
sin desbordar, dinamico, y auditado. Hecho:
- Barra de volumen (izquierda): a la derecha de la punta, "GEX del libro" en
  M/B con signo y, si la sombra de OI esta, "oi±..."; segunda linea si la fila
  tiene lugar: "OI 7,2k v 17,0k iv12" (interes abierto, volumen del dia, IV
  media ponderada por OI+volumen, en %).
- Barra de convexidad (derecha): a la izquierda de la punta, su ΔGEX por +1 %
  ("+2,4B").
- Titulo del perfil: "GEX volumen hoy · 0DTE · sombra OI" (el vencimiento sale
  del mapa: 0DTE si el mas cercano esta a menos de un dia).
- Ajuste "Datos en las barras": Auto (solo si las filas tienen lugar; si no,
  solo dominantes y majors), Siempre, Nunca. Tamaño = letra - 1.
- Con el mouse sobre una vela pasada, los rotulos son los de esa vela (la foto
  guarda el perfil entero).
Auditoria: `Rebobina --prueba <cadena> --precio 7722 --tabla 10` imprime la
tabla por strike del nucleo; en pantalla, la fila 7706 mostraba "OI 7,2k v
17,0k iv12" y conv "+2,4B" contra la tabla OI 7183, vol 16951, iv 12,2, conv
+2460M. Mismo nucleo, misma cadena: no hay otra fuente posible.
Lo que NO esta: vanna y charm (no se calculan; ponerlos seria inventar) y la
cinta de Rithmic por strike (otro libro, strikes del futuro; pendiente).

## Por que las dominantes salen en linea recta de noche, y el libro de Rithmic (2026-09-08, madrugada)

El operador comparo un tramo con datos de Databento (nube dispersa de guiones,
"como queria") contra el tramo en vivo de la noche (lineas rectas). Medido en
el archivo propio (volumen total de la cadena, cadena a cadena):
- 09-03 (Databento, rueda americana): 60 cadenas por hora y el volumen cambio
  en 59 de cada 60. La dominante (centroide) se mueve con cada cambio: banda.
- 07 noche y 08 madrugada (nuestro archivo de CBOE): 9-10 cadenas en total y
  casi sin cambios: CBOE congela la cadena fuera de la rueda. Sin cambios no
  hay movimiento: linea recta. No es un bug ni una logica que falte; es la
  hora. En la rueda, la rama "cadenas" trae una cadena por minuto y se vera
  parecido a Databento (misma fuente de fondo: OI 100 % igual, volumen 82 %).
Diferencias que quedan aun de dia: el retraso de CBOE (902 s: los cambios
llegan tarde y en bloques) y la IV (CBOE de puntas, Databento del ultimo
precio operado: mas ruidosa, parte de la "nube" de Databento es ese ruido).

Gamma Hoy 1.0, "Libro en vivo": CBOE_SPX (como hasta ahora) o Rithmic_ES:
la cadena de opciones de ES armada desde tu ATAS cada 10 s, con el volumen
del dia por strike en tiempo real (CurrentDayTotalVolume del conector), OI
de ayer, IV despejada de las puntas, strikes del futuro (base 0) y gamma
Black-76. Es lo que se puede obtener desde ATAS sin nube ni retraso: el mapa
respira con cada operacion. Salvedades: es OTRO libro (ES, no SPX: niveles
20-30 puntos distintos, medido antes), mas fino, y de noche tambien esta
quieto porque no opera nadie. El archivo del pasado sigue siendo SPX. Si el
libro de Rithmic no llega a 12 strikes con las dos puntas, sigue con CBOE y
lo dice en la cabecera ("RITHMIC FLACO").

## 1.2b: el mouse ya no mueve las bandas (2026-09-08, madrugada)

Reporte del operador: "si pasas el mouse como que lo afecta, interfiere, se
corre un poquito el grafico solo, se rompe o pasa para abajo". Causa, leida
en OnRender: en Hibrido, con el mouse sobre CUALQUIER vela del pasado que
tuviera foto, la escalera, las rayas y las bandas pasaban a esa vela (era la
inspeccion pensada para el rebobinado), y sobre una vela sin foto volvian al
vivo. Resultado: parpadeo, banda que cambia de lado (el "arriba/abajo" se
decidia con el precio viejo de esa vela) y rayas que nacian en el mouse.

Arreglo: ajuste "Mouse sobre una vela del pasado" (3. Pantalla):
- Cabecera (default): NADA de lo dibujado se toca; al pasar el mouse aparece
  un tercer renglon en la cabecera con lo que regia en esa vela (hora, futuro,
  dominantes, zero, cuadrante, hora de la cadena) y dice "(lo dibujado sigue
  siendo el vivo)". Se apaga al salir del grafico (IsMouseLeave) y mientras
  se arrastra el grafico (IsMovingChartUsingMouse).
- Todo: lo de antes (la pantalla entera pasa a esa vela). Con Fuente =
  Archivo, Cabecera se comporta como Todo, porque ahi es la unica forma de
  revisar el pasado.
- Nunca.
Ademas, si la vela del mouse no tiene foto (vela sin cadena) se toma la mas
cercana hacia atras (hasta 30 velas) en vez de saltar al vivo.

Verificado en pantalla (MNQ M5, CBOE): mouse sobre 07-09-2026 09:05 y 21:55
UTC-3, las bandas y rayas quedaron en 29.780 / 29.523 en las dos capturas y
solo cambio el renglon; con el mouse fuera del grafico el renglon desaparece.

## 1.3: gatillos de order flow en la banda, medidos antes de dibujar (2026-09-08, madrugada)

Pedido: un detector (delta inusual, prints grandes, tren de deltas, absorcion,
"ballena") que dispare SOLO en los extremos de las bandas dominantes, que el
precio le de la razon y que no llene el grafico de humo. Se hizo al reves de
lo habitual: primero el laboratorio (laboratorio/gatillos.py) sobre lo que el
propio indicador ya habia anotado (15 dias de ES minuto a minuto: O/H/L/C,
volumen, ops, delta y dominantes por vela), despues el dibujo.

La regla del laboratorio es simetrica y no admite interpretacion: desde el
cierre de la vela del gatillo, ¿llego G puntos a favor antes que G en contra
en H minutos? Placebo: la misma condicion de order flow con la dominante
corrida a otro strike. Control: la misma condicion en cualquier vela.

Lo que salio (ES M1, rueda americana, 6/6 pts en 30 min):
- El order flow SOLO, sin banda, es una moneda: 46-53 % en 460-2300 casos,
  para todas las condiciones (delta z, prints grandes, tren, divergencia).
- Con la banda pero SIN filtrar como se movio la dominante: nada (384
  entradas, todo entre 42 y 53 %, placebo igual). Causa: la dominante
  centroide persigue al volumen y "entra" sola en el precio.
- Con la dominante QUIETA (se movio <= 2 pts en 5 velas): 99 entradas en 14
  ruedas. La unica condicion que se repite en todas las variantes es "tres
  deltas seguidos EN CONTRA de la llegada -> rechazo": 70 % de 27 casos
  (IC 52-84) contra 37 % del placebo, y en las dos mitades (100 % de 8, 53 %
  de 17). Con 10 a favor / 5 en contra: 53 % de 15 contra 20 %.
- Consistente con eso: "tres deltas HACIA la banda -> continuacion" pierde
  contra el placebo en todas las variantes (40-48 % contra 54-57 %), y
  "ruptura con delta a favor" tambien (25 % de 20).
- De noche se diluye (60 % de 35 contra 50 %). En velas de 5 minutos hay 12
  entradas en 14 dias: no se puede decir nada.
- ADVERTENCIA: son 13 hipotesis por 10 configuraciones. Un 70 % de 27 puede
  ser azar. Es una pista, no una prueba; hacen falta 60+ casos.

El indicador (GatilloBanda.cs, la misma logica que el laboratorio):
- Ajuste "Gatillos de order flow en la banda (EXPERIMENTAL)": RechazoTren
  (default: solo la pista), Todos (tambien rechazo por delta, divergencia y
  ruptura con delta en gris), Ninguno. "Dominante quieta" 0,026 % del precio
  en 5 velas. "Solo en la rueda americana" (default si).
- Dibujo: un triangulo apuntando hacia adentro del canal pegado a la vela
  (arriba del maximo para cortos, abajo del minimo para largos) con el rotulo
  "tren". Se calcula sobre el pasado (recorrido del archivo) y en vivo.
- Registro: cada disparo a pythiagex-gatillos-<inst>-<marco>.jsonl (vivo, se
  agrega) y pythiagex-gatillos-archivo-... (se pisa en cada recorrido), con
  hora, tipo, lado, precio, dominante y dz. El laboratorio los juzga.
- Centinela: campo nuevo "of" por vela con dmax/dmin (delta maximo y minimo,
  historico) y en vivo big_n/big_max/big_buy/big_sell (operaciones acumuladas
  de OnCumulativeTrade con >= "Print grande" contratos, default 50). Es la
  materia prima para medir "ballenas" cuando haya muestra: hoy no se puede.
Primer recorrido sobre MES M5 (15 dias, todas las horas): 46 entradas a banda
quieta, 53 disparos, 16 rechazo·tren. Lo que se ve es exactamente lo que se
midio; si el laboratorio lo tira abajo, se saca.

## 1.4: los guiones nuevos resaltados y las barras pesadas cercanas (2026-09-08, mediodia)

Pedido: "hacer enfasis, cambiar de color o agrandar los puntos dominantes
nuevos que se estan dibujando en vivo, solo los nuevos; despues al mismo
color que los otros", y "una linea punteada tenue en las barras mas pesadas
cercanas al precio, arriba y abajo, solo las significativas".

- Cada guion lleva ahora la hora en que nacio. Los del vivo con menos de
  "Dominantes nuevas: resaltar los guiones de los ultimos (min)" (default 3)
  se dibujan encima, 2 px mas altos y anchos, amarillo casi blanco con borde
  oscuro. Los del archivo nunca cuentan como nuevos. El temporizador redibuja
  cada 10 s, asi que vuelven solos al amarillo normal.
- "Barras pesadas cercanas": las N barras del perfil (default 2 por lado)
  con mas GEX del libro que dibuja, dentro de un radio (0,6 % del precio),
  con raya punteada tenue del color de la barra y rotulo con su GEX y "0DTE"
  si vence hoy (el 0DTE pesa 1,5x en la jerarquia). No repite las que ya son
  dominante o major. Es una ayuda de lectura: no esta medida contra placebo.
Verificado en pantalla (MNQ M1 y M5): "+1,3B 0DTE" arriba y "-541M 0DTE /
-549M 0DTE" abajo del precio, y los triangulos "tren" en el M1.

## Auditoria de los vencimientos (0DTE) y las barras pesadas (2026-09-08, 15:30 UTC)

Pregunta: ¿los "0DTE" de pantalla estan bien calculados y son honestos en
tiempo real? ¿Las barras grandes cercanas son 0DTE aunque no lo digan?
Herramienta: laboratorio/auditar_vencimientos.py (cadena cruda, cuenta manual,
CBOE directo). Resultados, con numeros:

- DIAS AL VENCIMIENTO: la cadena trae 0,2045 d para el 08-09 a las 15:05:25
  UTC; a mano (vence 16:00 Nueva York = 20:00 UTC) da 0,2046: 6 s de
  diferencia en ES y 21 s en NQ (reloj de la nube). Los diez vencimientos
  (08, 09, 10, 11, 14, 15, 16, 17, 18, 21) coinciden con la lista de CBOE
  bajada aparte a las 15:28 (SPX 500 contratos el 08-09, NDX 764).
- HORIZONTE = HOY: entra SOLO el vencimiento de hoy (dias <= max(1, el mas
  cercano)). El perfil ENTERO es 0DTE por construccion: las cinco barras mas
  pesadas de cada lado tienen 97-100 % de su GEX en el 0DTE y el resto de la
  semana casi no suma. Una barra "sin 0DTE" no existe en este modo.
- EL "-1,3B" SIN 0DTE de la captura del operador era el rotulo de la
  CONVEXIDAD (violeta, a la izquierda de la escalera de convexidad), no una
  barra de otra fecha. Arreglo 1.4b: la convexidad lleva "Δ" adelante, y el
  rotulo de barra pesada dice siempre el vencimiento ("0DTE" o "Nd").
- LA FORMULA, contra la gamma que publica CBOE (misma hora, misma IV, mismo
  spot): mediana 0,974 en SPX (p10 0,87, p90 1,04) y 1,005 en NDX (p10 0,89,
  p90 1,32: CBOE redondea la gamma de NDX a 4 decimales, 0,0033, y eso es
  ruido de +-15 %). El OI coincide 100 % strike por strike.
- NUCLEO CONTRA CUENTA MANUAL, misma cadena y mismo spot (7694,28): 7700
  +36,09B en Rebobina; a mano da lo mismo (ver linea de abajo). La
  diferencia que se ve contra CBOE directo (+53B a las 15:28) es volumen que
  crecio en 23 minutos y el indice 10 puntos mas arriba (las calls de arriba
  se acercan al dinero y su gamma sube; las puts de abajo se alejan y baja):
  el signo de las diferencias es exactamente ese.
- HONESTIDAD EN TIEMPO REAL, lo que faltaba: los dias de la cadena se
  calculaban al generarla y quedaban congelados hasta la siguiente. Arreglo
  1.4b en el nucleo: se envejecen con la hora de la cuenta (ahora menos la
  hora de la cadena), asi la gamma del 0DTE usa el tiempo que de verdad
  queda y a las 16:00 de Nueva York el vencimiento del dia sale solo del
  perfil aunque la cadena no se renueve (la trampa de Opensera e
  InsiderFinance que siguen contando el 0DTE vencido). Antes dependia de que
  la nube generara una cadena nueva (cada 5 min fuera de la rueda).
- RITHMIC: desde el 07-09 a las 23:10 la busqueda del contrato grande
  (ES/NQ) en el servidor tira NullReference al arrancar ATAS y el mapa caia
  al micro, que solo lista el trimestral (18-09, 10 dias): SIN 0DTE. Con el
  libro de Rithmic elegido, "0DTE" era imposible. Arreglo 1.4b: se reintenta
  6 veces cada 15 s antes de caer al micro.
Lo que NO se pudo cotejar con una fuente independiente: el volumen y la IV
intradia (CBOE es la unica fuente gratis; Databento sin credito). La gamma
si, contra la de CBOE.

Cierre de la auditoria (12:52 local): el reintento solo no alcanzo (NullReference
6 de 6 con Type+Exchange; Code = "ES" devuelve la raiz sin series). Lo que
funciona: buscar POR CODIGO DE CONTRATO derivado del micro local (MESU6 ->
ESU6, y ESZ6 como siguiente). Resultado en el log: "6 vencimientos, 4318
contratos (ESU6)" y, por primera vez, "6 vencimientos, 3720 contratos (NQU6)":
el libro de Rithmic vuelve a tener 0DTE en ES y lo tiene tambien en NQ.

## Auditoria en vivo del 2026-09-09 (noche): la base rota y todo lo demas bien

Pedido: auditar en vivo, con varias fuentes y cuenta manual, dominantes, gammas,
0DTE, convexidad, OI, Max Change; no corregir nada sin verificarlo varias veces.
Herramienta nueva: laboratorio/auditar_vivo.py (rehace todo en strike y lo
compara con la linea AUDIT del indicador).

LO QUE ESTABA BIEN (MNQ, cadena de las 01:58 UTC, comparado en strike para no
depender de la base): net vol -870M contra -875M del indicador; net OI -257M
contra -259M; zero gamma vol K 29.352 contra 29.353; zero OI 29.364 contra
29.366; +Γ K 29.510 y -Γ K 29.000 iguales; dominante de abajo con centroide
K 29.000,54 identico; OI strike por strike igual a CBOE directo; dias al
vencimiento con 6-21 s de error; la gamma contra la que publica CBOE, mediana
0,97-1,00 (auditoria del 08-09).

LO QUE ESTABA MAL, verificado cinco veces: la BASE. A las 21:08 UTC del 09-09
la base "CRUDA" de la nube salto de 28,7 a 322,2 en NQ y de 7,1 a 72,7 en ES
(lineas AUDIT de cada hora; los dos archivos de cadenas; los dos instrumentos;
el carry teorico da 21 pts para NQ y 5,6 para ES; y 322-29 = 294 es el carry
de 91 dias, o sea la cotizacion de la nube rolo al contrato de diciembre
mientras los graficos siguen en septiembre; la medicion de la nube viene de
pythiagex.base.medir con confiable=False). Consecuencia: todos los niveles de
MNQ dibujados desde las 17:08 (hora de Nueva York) quedaron ~294 pts arriba;
la "dominante de arriba" (K 29.200) estaba en realidad 156 pts DEBAJO del
precio. ES se salvo hasta las 03:13 UTC por la "medida hace <= 360 min" y
despues iba a caer en lo mismo (+67 pts).

ARREGLO (Gamma Hoy 1.5 a 1.5d), en el nucleo: la base se acota con el carry
teorico del contrato del grafico, precio x (tasa - dividendo) x dias / 365,
con el vencimiento real: Security.Expiration de ATAS, o el codigo, o (ATAS da
la raiz sola en pestañas ocultas) el trimestral mas cercano SUPUESTO aceptando
tambien el siguiente. Orden: medida > medida reciente > de la rueda (nueva:
el indicador mide en la rueda el cierre del grafico a la hora real del spot,
902 s antes del ts de la cadena, menos ese spot; mediana de 30; guardada en
%APPDATA%\ATAS\PythiaGex\base-rueda-<raiz>.json) > cruda > TEORICA. Lo que
no cabe en la cota (60 % del carry o 0,06 % del precio) se descarta y la
cabecera lo dice: "base TEORICA carry 20,1 (cruda 322,2 descartada)".
Verificado en el log y en pantalla: NQ base 20,07, zero 29.374 (a mano
29.352 + 20), precio 29.414 entre D2 29.021 y D1 29.532.
Pendiente: entender por que la medicion de la nube da eso de noche
(pythiagex/base.py) y por que NDX nunca sale "confiable".

## 1.5f: el Max Change estaba roto por la base (2026-09-10, madrugada)

Verificado tres veces antes de tocar: (1) mi recomputo del Max Change con las
cadenas archivadas coincidia con el del indicador solo en 9 de 30 casos a 30
min; (2) en el centinela vivo de MNQ M1 de la rueda del 09-09, mc30 era
EXACTAMENTE un major o una dominante en 237 de 237 minutos, mc5 en el 80 %,
mc1 en el 53 %, y la base cambio en 148 de 310 minutos; (3) en el nucleo, las
fotos por minuto del GEX estaban indexadas por precio del FUTURO (strike +
base): cuando la base cambia (en NQ con cada cadena, en ES en centesimos)
ninguna clave coincide, "antes" vale 0 y el cambio es la barra entera: el
Max Change se vuelve "la barra mas grande". Las semillas y el Δ1' de la
escalera mostraban eso. Arreglo: fotos indexadas por strike.
Medido en la pasada del archivo, misma rueda, antes y despues: MNQ 2 min
mc30 = barra grande 99 % -> 70 %, mc5 86 % -> 56 %, mc1 61 % -> 50 %; MNQ
5 min mc1 71 % -> 46 %. Lo que queda de coincidencia es legitimo: cuando la
cadena no cambia, el cambio viene solo de repreciar con el spot y las barras
mas grandes son las que mas se mueven (igual que las puntas de GAMMAlito).
Y la cadena de NDX en la nube cambia cada ~4 min (93 cadenas en el dia):
entre cadena y cadena el Max Change es repreciado, no operaciones nuevas.

## 1.6 / 1.6b: las pelotitas del Max Change, como las define el creador (2026-09-10, madrugada)

Pedido: "las pelotitas y las barras laterales y sus movimientos: el verdadero significado,
por que se mueven, hacia donde cuando sube o baja; el creador les da mucha importancia".

QUE SON (audio de "Te explico en vivo el Max Change", 5:59, transcripto): tres circulos
por barra, grande = donde estaba la punta hace 15 min, mediana = hace 5, chica = hace 1.
Adentro de la barra = ese strike crece en exposicion; afuera = decrece; juntas en la
punta = nada paso en 15 min; alineadas = crecimiento sostenido; desparramadas = mucho
momento; desordenadas = indeciso. "Lo mas importante no es la dominante sino la
pelotita: es adelantado". En los cuadros 1080p se ven en los DOS perfiles (GEX y
convexidad). Las barras son el volumen de opciones del dia por strike (0DTE a la
izquierda; el ladder derecho a 90 dias).

POR QUE SE MUEVEN: la barra es gamma x volumen y la gamma es maxima en el strike donde
esta el precio: al acercarse el precio la barra crece (pelotitas adentro), al alejarse se
achica (afuera); ademas crece por operaciones nuevas. El sube-y-baja vertical de 1,5 pts
medido en sus videos es la razon QQQ->NQ cambiando, igual que nuestra base: no informa.

LAS NUESTRAS ESTABAN ROTAS: VerPelotitas existia, pero buscaba la foto vieja por precio
del futuro; con la base cambiando (cada cadena) no encontraba nada, y desde 1.5f (fotos
por strike) directamente no dibujaba. 1.6: (a) por strike; (b) al arrancar se siembran
con los perfiles de las ultimas 35 velas del archivo, asi existen desde el primer minuto;
(c) con el mouse sobre una vela del pasado (modo Todo) salen de los perfiles de las velas
anteriores; (d) log "PELOTITAS" cada 5 min con ahora / hace 1 / 5 / 15 y el veredicto,
para auditar contra la pantalla. 1.6b: grises con borde del color de la barra (15 > 5 > 1)
y tambien en la escalera de convexidad (las fotos guardan la convexidad por strike).
Verificado en pantalla (MNQ 5 min) y en el log: K7700 de ES "DECRECE (las 3 afuera)" con
el precio alejandose; K29400 de NQ "CRECE"; el resto "quieto" en la noche.

MEDIDO ("es adelantado"), laboratorio/pelotitas_predicen.py, cadenas por minuto de
Databento, rueda americana: ver numeros en la seccion siguiente.

VIDEOS NUEVOS (13, bajados con yt-dlp cliente android, transcriptos con Vosk):
conocimiento/gammalito/nuevos-2026-09-10/. Ninguno agrega mecanica nueva sobre las
pelotitas; confirman: perfil izquierdo 0DTE / derecho 90 dias, la convexidad como
"combustible sea del color que sea", y "sigo con las pelotitas, confirmacion".
La medicion cuadro a cuadro del video (herramientas/medir_pelotitas.py) no fue confiable:
el presentador hace zoom, scroll y dibuja encima; la definicion sale del audio.

### Medido: "la pelotita es adelantada" (13 dias por minuto, rueda americana, 45 min)
Un strike a menos de 2 % del precio, sin ser dominante, pasa a ser la dominante de su lado
en los 45 minutos siguientes:
- ES: CRECE (las tres adentro, +20 % en 15 min) 10,6 % de 52.498 casos | QUIETO 4,9 % de
  178.716 | DECRECE 7,6 % de 31.919. El strike de al lado con la misma etiqueta: 8,0 / 4,0
  / 6,1 %.
- NQ: CRECE 6,0 % de 109.086 | QUIETO 2,7 % de 413.210 | DECRECE 4,2 % de 69.350. Vecino:
  3,0 / 1,4 / 2,1 %.
Lectura honesta: las pelotitas adentro duplican la chance de que ESE strike sea la proxima
dominante (y le ganan al vecino, sobre todo en NQ: el dato es del strike, no solo de la
zona), pero nueve de cada diez no llegan, y "decrece" tambien sube la chance (lo que
adelanta es que la exposicion de ese strike esta cambiando). Son contexto ("donde se esta
moviendo la cobertura"), no un gatillo. Igual que dice el creador: "sigo con las pelotitas,
confirmacion", nunca "entro por la pelotita".

## Auditoria en la rueda del 2026-09-10 (11:27-11:45 local, MNQ)

- NUCLEO CONTRA CUENTA MANUAL, MISMA CADENA (la del feed de las 14:09 UTC que usaba el
  indicador a las 14:25): neto vol +2,25B contra +2,29B; zero vol K 29.095,9 contra
  29.096,6; zero OI 29.340,5 contra 29.343,4; +Γ K 29.430 y -Γ K 28.890 iguales;
  dominantes con centroide 29.429,43 / 29.200,26 IDENTICAS; pico K 29.200. Coincide.
  (Comparar con la cadena de la nube de las 14:25 daba diferencias: eran DOS cadenas
  distintas, no un error: el feed por minuto y el archivo llegan cada ~15 min porque el
  cron de GitHub Actions corre cada ~15 min aunque diga "cada minuto".)
- BASE: cruda de la nube 317-331 (rolo a diciembre, ver auditoria del 09-09), descartada;
  TEORICA 18,8. Yahoo por minuto: NQU26 - ^NDX = 12 a 28 pts en la manana (25 a las
  14:09-14:20 UTC), NQZ26 - ^NDX = 290: la cota hace lo correcto.
- "BASE DE LA RUEDA" (la medicion propia del indicador) SALIO 271 en NQ y 55 en ES:
  BarraDe(horaUtc) convertia la hora de la vela con ToUniversalTime(), que trata la hora
  "Unspecified" de ATAS como local y suma 3 h: devolvia la vela de tres horas antes. La
  cota la rechazo (por eso no hizo daño) y quedo arreglado en 1.6c (usa Utc()). El mismo
  error corria 3 h las burbujas de Big Trades de la cadena viva. Como el feed cambia cada
  ~15 min, las 5 muestras de la mediana tardan ~75 min en juntarse.
- MAX CHANGE (arreglo 1.5f) en vivo: mc30 = barra grande 38 % de los minutos (ayer 100 %).
- PELOTITAS en vivo: K 29.200 "CRECE" (294 -> 293 -> 344 -> 349 M), K 29.300 "mezclado"
  (422 -> 485 -> 570 -> 557 M: crecio 15 min y afloja el ultimo minuto); en pantalla se
  ven separadas de la punta (zoom_rueda2).
- GATILLO en vivo: 11:22 local "rechazo·tren LARGO en 7603" (MES, dominante 7592,6):
  despues maximo 7620 (+17), minimo 7601,25 (-1,75): gano con la regla 6/6. Un caso.
- Archivo local del dia: se baja al arrancar y no se refresca durante la sesion (la
  pantalla no lo necesita: el pasado del dia lo lleva el vivo); al reiniciar se completa.
  Para auditar hay que usar el archivo de la nube (rama cadenas, raiz del repo).

## El banco de pruebas de gatillos (2026-09-10, tarde): resultado negativo, con numeros

Pedido: "un gatillo long/short que acierte, sin ruido". Se armo laboratorio/
gatillo_cientifico.py: 11 hipotesis definidas antes de mirar (zero cross con
convexidad negativa/positiva, ruptura y rechazo del major con delta, Max Change
alineado, tren + delta + convexidad, divergencia CVD en la banda, rechazo en
dominante quieta/con delta), objetivo a escala (NQ 25 pts, ES 6), regla simetrica
y 2:1, entrenamiento/prueba por dias, permutacion del lado y placebo con niveles
corridos. 16 dias de MNQ y 14 de MES por minuto con delta real.
- NINGUNO supera claramente al azar y al placebo en prueba con 15+ casos. Lo mas
  cercano: "zero cross + convexidad positiva -> fade" (MES 52 %/60 % con 25 casos,
  MNQ 54 %/54 % con 26; placebo 52 %). Pista debil, se sigue anotando, no se dibuja.
- laboratorio/ml_check.py (techo): logistica y bosque con TODOS los rasgos, validacion
  por bloques de dias, AUC fuera de muestra 0,51-0,52 (MNQ) y 0,48-0,51 (MES) para
  "+G antes que -G en 30 min". Es una moneda: no hay regla a mano que pueda mas.
- laboratorio/momentum_intradia.py (Baltussen, Da, Lammers y Martens, JFE 2021: el
  resto del dia predice los ultimos 30 min por la cobertura de gamma): en nuestros 60
  dias de NQ y 62 de ES (jun-sep 2026) NO replica (46,7 % y 41,9 % de acierto de
  signo); con convexidad negativa en NQ 63,6 % de 11 dias (t 1,5): sin evidencia.
- Regimen -> volatilidad (lo que si esta publicado): tampoco: con convexidad negativa
  el rango de 30 min es MENOR en NQ (62,8 contra 71,5 pts, p 0,98 con permutacion por
  dias) e igual en ES.
Conclusion honesta: con cadena de CBOE a 15 min de retraso y cada 15 min, y order
flow por minuto, no hay señal direccional a 30 min en estos datos. Lo que queda:
(1) acumular la cadena viva de Rithmic (volumen real por strike, desde el 08-09) y
volver a correr el banco en 3-4 semanas; (2) la pelotita como contexto (x2, ver 1.6);
(3) el laboratorio queda listo para juzgar cualquier idea nueva en minutos.

## 1.7: el gatillo MODELO (2026-09-10, tarde). Lo unico que gano fuera de muestra

Tras el resultado negativo del banco de hipotesis a mano, la prueba de techo con
aprendizaje automatico (laboratorio/ml_check.py: logistica y bosque con todos los
rasgos, validacion por bloques de DIAS) encontro señal en ES a horizontes cortos:
- MES 1 min, objetivo +3 antes que -3 en 10 min: AUC 0,56-0,57 fuera de muestra (5 de 5
  bloques por encima de 0,50); operando solo con p >= 0,70: 61 % de acierto, 14 por
  dia. Avance hacia adelante (entrena con los dias anteriores, opera el siguiente): 370
  disparos 63,8 %; en la TARDE de Nueva York (14-16 h) 83,1 % de 59 (7 por dia).
- Atribucion: con los niveles permutados entre filas el modelo pierde la señal (AUC
  0,53, casi nunca seguro); solo order flow + hora, AUC 0,54; solo niveles, 0,52. La
  ventaja es la INTERACCION de momentum corto (ret15, delta acumulado) con la
  geometria de los niveles (zero, majors, dominantes, Max Change). Placebo por
  corrimiento constante NO sirve para modelos lineales (la escala lo absorbe).
- La regla 2:1 no funciona (31,6 %): es una ventaja chica en objetivo chico, no un
  movimiento grande. Costos de MES ~0,3-0,5 pts por vuelta: neto positivo pero fino;
  en la tarde, +1,9 pts brutos por operacion.
- NQ: nada (AUC 0,51-0,52 en todos los horizontes). No se usa.
- MES 2 min (el grafico del operador), horizonte 5 velas: mas debil: AUC 0,54, p >= 0,70
  62 % de 53; avance hacia adelante en la tarde 76,7 % de 30 (2,7 por dia). Dos dias
  (03-09 y 08-09) concentran la mitad de los disparos: ojo.
Implementacion: GatilloModelo.cs (10 rasgos, media/escala/coeficientes fijos por
temporalidad M1 y M2; equivalencia C#-Python verificada vela a vela: diferencia
mediana 0,0000, mismo veredicto 99,94 %, Rebobina --modelo + modelo_equivalencia.py).
Ajustes: "Gatillo MODELO" (SoloTarde por defecto / TodoElDia / Ninguno) y umbral
(0,70). Rombo verde/rojo con "M p"; registro en pythiagex-gatillos-*.jsonl (tipo
modelo·es10, Dz = p); el laboratorio lo juzga con los dias nuevos, que nunca se usaron
para ajustar. Solo raiz ES y graficos de 1 o 2 minutos.

## Toques y rebotes en dominantes / zero / majors, a fondo (2026-09-10, 14:30 local)

Pedido: "cuantas veces por sesion se acerca, toca o traspasa un poco y rebota; que
temporalidad; que franja horaria; un par de señales por sesion". laboratorio/
rebote_niveles.py: toque = entra a +-8 pts (NQ) / +-2 (ES) viniendo de >= 30 / 7 pts;
traspaso chico / medio / ruptura; rebote = 20 / 5 pts hacia donde venia antes que lo
mismo en contra, en 20 min; por nivel, franja, traspaso, delta, cuadrante; placebo con
el nivel corrido; y variante "reclamo" (falsa ruptura: entrar cuando vuelve a cerrar del
lado de la llegada). En 1, 2 y 5 minutos, MNQ y MES, 14-16 dias.
- Toques por dia: 15 (M1), 10 (M2), 5,5 (M5) en MNQ; 7 / 6 / 4,6 en MES.
- Rebote real contra placebo (todo junto): MNQ 53,6 / 59,7 % (M1), 64,4 / 64,0 (M2),
  67,0 / 65,0 (M5); MES 50,0 / 53,8 (M1), 52,2 / 62,5 (M2), 59,4 / 57,1 (M5). Con
  reclamo: 46-49 %, tambien igual al placebo.
- Los subgrupos que "ganan" (zero en M2 +15 pp con 25 toques; traspaso medio en MNQ M1
  +15 con 70; IMAN con reclamo +14; 14-15 h +21 con 26) no se repiten entre
  temporalidades ni instrumentos: son lo que se espera de 5 niveles x 5 franjas x 4
  traspasos x 2 deltas x 2 regimenes x 3 temporalidades con muestras de 15-70.
- Lo que si se ve, y no es el nivel: en la primera hora (9:30-10:30 NY) el precio que
  llega rapido a CUALQUIER punto retrocede 20 pts antes de seguir en el 68-83 % (real y
  placebo por igual). Es reversion a la media de la primera hora; pista para medir
  aparte, no un gatillo de nivel.
Conclusion: con 16 dias, el rebote en dominantes/zero medido de todas las formas que
pidio el operador NO le gana al mismo nivel corrido. Lo unico con ventaja fuera de
muestra sigue siendo el gatillo MODELO (1.7b, ES, tarde). Todo queda anotandose para
volver a correr con mas dias y con el libro vivo de Rithmic.

## 1.8: el gatillo REBOTE en las rayas, pedido con ejemplos (2026-09-10, 15:00 local)

Pedido: 8 capturas de MNQ de hoy (10:50, 11:18, 11:33, 11:52, 12:12, 12:42, 13:35 hora
local) donde el precio choca o traspasa un poco una raya amarilla y rebota: "encontra el
gatillo que coincida con todos esos ejemplos, sin excusas; de las salidas me encargo yo".

Lo primero que salio de las capturas contra los datos: las rayas amarillas donde el entra
NO son solo la dominante vigente. Son todas las dominantes que hubo en el dia (cada una
queda como fila de guiones aunque la dominante ya se haya movido), mas el zero y los
majors. Reconstruido minuto a minuto (laboratorio/rebote_dominantes_dia.py --dia y
laboratorio/gatillo_rebote.py --dia):
- 10:52-10:56 toca la dominante 29.094 viniendo de 29.185, cierra arriba, sube 40-70.
- 11:16-11:17 toca el zero 29.115 y la fila vieja 29.118 (dominante de las 10:53): sube
  60, y 130 en 20 min.
- 11:27-11:29 traspasa la dominante nueva 29.219 hasta 29.195 y vuelve; 11:33-11:34 la
  vuelve a tocar (cierra 29.219,25 contra 29.218) y sube 55.
- 11:50, 12:09, 12:39-12:47: toca 29.218 (la dominante de abajo) y sube 50, 33 y 10-20.
- 13:34-13:35: vela de -2.506 de delta hasta 29.133, sobre la fila vieja 29.129, la
  dominante 29.122 y el major 29.118; sube 60.
Todos largos, en un dia que abrio en 29.0xx y paso la tarde en 29.2xx: comprar el
retroceso a cualquier raya funciono.

La regla (atas/PythiaGexNiveles/GatilloRebote.cs = laboratorio/gatillo_rebote.py):
nivel = fila de dominante de la rueda (identidad por strike, valor exacto), zero o major;
toque = el minimo llega a +-10 pts (0,035 % del precio) o traspasa hasta 25 (0,085 %);
venia de arriba (maximo de las 10 velas previas >= nivel + 15); cierra por encima del
nivel; la vela anterior no cumplia; enfriamiento 5 velas por nivel y lado; si varios
niveles cumplen, el mas cercano al extremo. Corto espejo. Entrada = cierre de la vela.

Medido en 16 dias de MNQ por minuto contra las mismas rayas corridas +-85 y +-145 pts
(acierto = +20 antes que -20 en 20 min desde el cierre; tambien +10/-10, +15/-15, +20/-10,
+10/-20 y la MFE mediana; niveles con el valor exacto e identidad por strike, igual que
el indicador; la equivalencia con el archivo del indicador dio 28 de 30 disparos iguales
al minuto, laboratorio/rebote_equivalencia.py):
- Todos: 102 disparos por dia, 45,2 % (placebo 45,5). Con +10/-10: 47,0 (47,0). MFE
  mediana 24 pts (24,5).
- Ningun filtro solo lo separa del placebo: lado, a favor del zero (43,9 / 42,0), de la
  apertura del dia, del momentum de 30 min, delta de la vela (rechazo o absorcion),
  tamaño del delta, traspaso, franja, cuadrante, numero de toque, fila vieja o actual,
  edad de la dominante, confluencia de niveles, llegada rapida.
- El unico bolsillo: PRIMER toque de la dominante ACTUAL a favor del zero: 52 casos
  (3,2 por dia), 50,0 % (placebo 41,9); +10/-10 59,6 (39,2); +20/-10 42,3 (25,7).
  Validacion (laboratorio/gatillo_rebote_validar.py): primera mitad de los dias 50 (40),
  segunda mitad 50 (44); largos 46 % con 41 casos, cortos 64 con 11; parametros
  estrictos 48 (43), laxos 47 (35), sin traspaso 51 (33); MES 36 (40) con 36 casos;
  MNQ 2 min 45 (34), 5 min 42 (29). Hoy 0 de 2 (11:25 y 14:10). Con 52 casos y 50 % a
  1:1 no es una ventaja probada, y en ES no aparece: queda como hipotesis a seguir
  midiendo, no como señal.
- Temporalidad: 1 min da la entrada mas cerca del nivel; 2 y 5 min no mejoran el acierto
  (43 y 40 %, igual al placebo).

Lo que se instalo igual (1.8), porque lo pidio y porque hay que verlo en vivo:
- Ajuste "Gatillo REBOTE en las rayas": SoloTendencia (default: largo con el precio sobre
  el zero, corto debajo; 51 por dia), Todos (102 por dia; los 7 ejemplos disparan),
  PrimerToqueActual (3,2 por dia), Ninguno. "Enfriamiento por nivel (velas)": 5.
- Triangulo hueco verde bajo el minimo (largo) o rojo sobre el maximo (corto), con "R"
  ("R1" = primer toque de ese nivel en el dia). Se dibuja sobre el archivo (recorrido) y
  en vivo; el vivo arranca sembrado con los guiones de la rueda y los disparos previos.
- Cada disparo va a pythiagex-gatillos-<inst>.jsonl como rebote·dom, rebote·zero o
  rebote·major, con el nivel y el numero de toque, para juzgarlo con dias nuevos.

Conclusion honesta: el gatillo reproduce sus entradas, y sus entradas ganaron porque el
dia fue alcista, no porque la raya tenga algo que un nivel corrido 85 puntos no tenga.
Lo unico que vale seguir midiendo con mas dias es el primer toque de la dominante actual
con el zero a favor (3 a 4 por dia), y para eso queda grabando.

## 1.8b: el marcador del REBOTE en el punto exacto (2026-09-10, 15:20 local)

Pedido: "que aparezca en el punto exacto de la vela donde se activo, no arriba o abajo;
mas chiquito; que no tape las velas; que se note a simple vista". Ahora el disparo es un
circulo hueco chico (4 px, ajustable en "tamaño del circulo") centrado en la raya que
toco, en la vela del disparo: verde largo, rojo corto. Hueco para que la mecha se vea a
traves. La letra (R, o R1 en el primer toque de esa raya en el dia) va pegada al circulo
por fuera de la vela: debajo en los largos, encima en los cortos. Los triangulos grandes
desplazados del 1.8 se fueron.

## Auditoria de la noche del 2026-09-10 (19:15-19:40 local), tras el reinicio del operador

ATAS estaba "tildado" y el operador lo reinicio a las 19:15. Lo que se revisó, con que y que dio:
- **El indicador no tenia errores**: el log escribia cada minuto hasta las 19:12 y no hay excepciones.
  La causa del tildado es la memoria de la PC: 16 GB con 2 MB libres en el momento de medir; ATAS
  con 7,6 GB privados (llego a 8,7 al abrir el Options Board), Edge ~3 GB, la app del chat ~1,7 GB.
  De ATAS, ~1 GB era del propio indicador: guardaba el perfil (150-211 strikes) de CADA vela con
  cadena (19.000 velas de 1 min, mas 2 y 5 min, mas MES). 1.8c poda ese perfil a las ultimas 2.500
  velas (lo que usa el mouse sobre el pasado). Carga en el proximo reinicio.
- **Cadena viva de Rithmic apagada desde el 09-09 a las 11:57**: ATAS se actualizo a 8.0.14.399 ese dia
  a las 11:08 y la busqueda del conector por campos privados (3 niveles) dejo de encontrarlo; el log
  repite "no se encontro el conector de opciones" cada pocos segundos y vivaActiva=False. 1.8c busca
  a 5 niveles sin ciclos, adentro de colecciones y en los estaticos de ATAS/OFT, y anota el camino
  donde lo encuentra. Se verifica en el proximo reinicio; si tampoco, hay que correr la Sonda.
- **Valores**: la linea AUDIT del indicador (NQ 19:22: fut 29150, base 17,98 TEORICA, zero 29041,35,
  majors 29517,98/29147,98, doms 29517,89/29145,90, q3) reproducida por el laboratorio con codigo
  independiente (auditar_vivo.py): net vol +455M vs +454M, zero a 0,6 puntos de strike, majors,
  dominantes, pico y barras pesadas iguales. ES idem (zero 7596,94 vs 7596,97, doms 7598,04/7549,34).
- **Calculo a mano desde la cadena CRUDA de CBOE** (NDX y NDXP del 11-09, sin codigo del proyecto):
  los 10 strikes mas pesados dan lo MISMO que la nube (0,00 %); con la gamma que publica CBOE, 5-15 %
  de diferencia (otro T y otra tasa: esperado). Net GEX: el feed corta los strikes a +-5 % del spot
  (ANCHO = 0,05 en cadena_atas.py): con todos los strikes crudos el neto da 0,344B en vez de 0,435B,
  porque puts muy lejanos con IV absurdas suman -90M. Los niveles cerca del precio no cambian; el
  "net" del titular es el de +-5 %. Anotado, no corregido: es decision de diseño.
- **Web contra indicador**: JS = nube = ATAS (Auditoria y fuentes, en strike); el gatillo rebote del
  archivo rehecho coincide con el laboratorio en 38 de 41 disparos (3 a una vela de distancia).
- **El subidor abria una consola por cada llamada a gh** (bajo pythonw): el operador no podia usar la
  PC. Corregido con CREATE_NO_WINDOW; relanzado. Ademas la web decia "PC sin señal" 5 min: el bucle
  quedo trabado en una llamada; con la vuelta manual subio los 4 archivos en 10 s.
