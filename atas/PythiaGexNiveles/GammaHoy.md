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

Hallazgo del primer rebobinado: la alerta de TRANSICION se dispara a cada
minuto cuando dos strikes vecinos se alternan el maximo GEX (7.759/7.764 el
09-03 de 19:16 a 20:16). Falta histeresis: pendiente en el nucleo.
