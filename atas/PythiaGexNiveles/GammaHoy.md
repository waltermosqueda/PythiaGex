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
