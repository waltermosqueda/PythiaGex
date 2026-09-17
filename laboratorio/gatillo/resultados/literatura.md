# Familia "literatura" — que usan los que saben (17-09-2026)

Esta familia NO busca un gatillo corriendo datos: revisa papers y practica profesional seria y entrega hipotesis
con fuente y receta de prueba. Los finalistas de esta familia quedan como "no corrido".

## PRE-REGISTRO de la unica prueba chica (escrito ANTES de correr nada)

Nombre: `literatura_01_vida_de_la_senal.py`. Es DESCRIPTIVA: no elige finalistas ni variantes, no toca confirmar(T).

Pregunta: la literatura dice que lo que se ve en la punta del libro (desbalance de cola, OFI) predice el proximo
movimiento a horizontes de "uno o dos cambios de precio" y despues se apaga. En MNQ, cuantos SEGUNDOS es eso, y
cuantos TICKS vale, comparado con el costo del operador (0,85 pts = 3,4 ticks ida y vuelta)?

Datos: solo B.explorar(T) (12 sesiones hasta el 04-09), solo rueda 13:30-20:00 UTC sin los 2 primeros minutos.

Rasgos en el segundo t (solo filas <= t):
  1. I      = (bidv - askv) / (bidv + askv)            desbalance de cola al cierre del segundo
  2. OFI10  = suma de ofi en [t-9, t] / profundidad media de 300 s (bidv+askv)
  3. PAS10  = suma de ofi_pas en [t-9, t] / la misma profundidad
  4. D10    = suma de delta en [t-9, t]
  5. D60    = suma de delta en [t-59, t]
Resultado: cambio del precio MEDIO (bid+ask)/2 de t a t+h, con h = 1, 2, 5, 10, 30, 60, 300, 600 s, en ticks.
Medidas: correlacion de Spearman por sesion (media y cuantas sesiones con el mismo signo); y el movimiento medio en
ticks del decil mas alto menos el decil mas bajo de cada rasgo, por horizonte.
Ademas: distribucion del spread de MNQ en ticks y cuantos segundos por minuto cambia el precio medio (para traducir
"dos cambios de precio" a segundos).
Que NO voy a hacer: no voy a buscar umbrales, ni elegir el mejor horizonte, ni armar una regla con esto.

Agregado al pre-registro (antes de correrlo): `literatura_02_spread_antes.py`. La tabla de 1 s guarda la punta de DESPUES de
la ultima orden del segundo (cuando el libro puede estar recien comido). Para clasificar a MNQ como "tick grande" o "tick chico"
(Gould y Bonart: el desbalance de cola sirve mucho mas en tick grande) hace falta el spread EN REPOSO, el que ve el que manda
la orden: aask - abid de la cinta. Se lee UNA sola sesion de explorar (2026-09-03), 3 columnas, con filtro de parquet. Solo
describe: distribucion del spread antes de cada orden, en ticks, en rueda.

---

## RESULTADO de la prueba chica (corrida una vez, 12 sesiones de explorar, 8 segundos de maquina)

Medido en esta sesion, con `literatura_01_vida_de_la_senal.py` y `literatura_02_spread_antes.py`:

- **El precio medio de MNQ cambia en 55,7 de cada 60 segundos de rueda** (minimo 54,6, maximo 57,8 entre sesiones). O sea: "dos cambios
  de precio", que es hasta donde llega la prediccion con el libro segun Kolm, Turiel y Westray (2023), en MNQ son MENOS DE 2 SEGUNDOS.
- **Desbalance de cola** (cuanto mas grande es la fila de compradores que la de vendedores en la punta): predice el proximo movimiento,
  con el signo que dice la literatura, en 12 de 12 sesiones a 1 y 2 s y en 11 de 12 a 5 y 10 s. Pero vale +0,39 ticks a 1 s y +0,25 ticks
  a 10 s (diferencia entre el decil mas cargado al bid y el mas cargado al ask), contra 3,4 ticks de costo. A los 30 s ya no queda nada
  (correlacion +0,002; 7 de 12 sesiones).
- **OFI de 10 s, OFI pasivo de 10 s, delta de 10 s y delta de 60 s**: ninguno predice a favor. Todos dan correlacion levemente NEGATIVA
  con lo que viene (delta de 60 s: -0,026 a 30-60 s; solo 3 a 5 de 12 sesiones con signo positivo): despues de un empujon de flujo el
  precio, en promedio, devuelve un poco. Decil alto menos decil bajo del delta de 60 s: -6 ticks a 60 s y -11 ticks a 300 s, y a 600 s
  se da vuelta (+0,8). Es inconsistente entre sesiones y coincide con lo ya medido en el proyecto (la vela siguiente revierte 52 %).
  No se arma ninguna regla con esto (asi estaba pre-registrado).
- **Spread de MNQ en reposo** (la punta ANTES de cada orden, sesion 2026-09-03, 572 mil ordenes): 1 tick el 60 % de las veces, 2 ticks el
  37 %; promedio 1,44 ticks = 0,36 pts (1,34 ticks mirando la primera orden de cada segundo). En la tabla de 1 s (punta de DESPUES de
  la ultima orden) el spread es de 2 ticks el 67 % del tiempo: esa columna exagera el spread porque el libro esta recien comido.
  **Aviso para todas las familias:** base.py supone 1 tick de spread (0,25). Con 0,36 medido el costo seria ~0,96 y el empate de la barrera
  +-8 pasaria de 55,3 % a ~56,0 %. Es UNA sesion: hay que medirlo en las 20 antes de cambiar nada.
- MNQ no es un instrumento de "tick grande" puro (spread casi siempre de 1 tick), que es donde Gould y Bonart encuentran que la cola
  predice mejor. Ademas el libro de MNQ es un espejo del de NQ: las filas de la punta de MNQ las ponen robots que arbitran contra NQ.

---

## LO QUE DICE LA LITERATURA, EN UNA IDEA

La analogia: el libro de ordenes es como mirar la fila de la caja del supermercado. Mirando la fila sabes quien paga en los proximos
segundos, no quien va a estar comprando dentro de diez minutos. Todo lo publicado sobre la punta del libro (desbalance de cola, OFI,
microprecio, lead-lag) predice el PROXIMO tick o los proximos segundos, vale una fraccion del spread, y los propios autores aclaran que
con ordenes a mercado no paga el spread. El scalper manual opera a 1-10 minutos y paga 3,4 a 4 ticks por vuelta: esta parado en otro
horizonte. Lo que SI es predecible a 10-60 minutos, segun lo publicado, no es la direccion sino el CARACTER del movimiento: cuanta
actividad va a haber (se agrupa: a una rafaga le sigue otra) y, con gamma de los market makers, si los empujones tienden a seguirse o a
devolverse. Eso es filtro, no gatillo.

Y una segunda idea que explica lo que el proyecto ya midio (delta +0,82 con la vela actual, -0,03 con la siguiente): el flujo agresor
es PERSISTENTE por construccion, porque las ordenes grandes se parten en pedacitos del mismo signo durante minutos u horas (Lillo y
Farmer 2004; Bouchaud, Farmer y Lillo 2009). Como todos lo saben, la liquidez se acomoda y el precio ya descuenta la parte previsible.
Que el CVD "venga subiendo" no dice que el precio vaya a seguir: dice que alguien sigue partiendo una orden.

---

## HIPOTESIS, ordenadas por probabilidad de servirle a un scalper manual de 1 minuto

Las probabilidades son juicio mio despues de leer, no una medicion.

### 1. La cola decide COMO entrar, no SI entrar (regla de ejecucion, baja el costo)
- Que afirma: el desbalance de cola predice la direccion del proximo movimiento del precio medio (Gould y Bonart 2016: mejora
  "considerable" en acciones de tick grande, "moderada" en tick chico); el microprecio de Stoikov (2018) lo formaliza; Cartea, Donnelly y
  Jaimungal (2018) muestran que usarlo para decidir entre orden a mercado y orden limitada mejora el resultado de una estrategia.
  Lipton, Pesavento y Sotiropoulos (2013) modelan lo mismo.
- Horizonte: el proximo tick; segundos. Tamano: fraccion del spread. Costos: no los paga como gatillo (Huth y Abergel 2014 lo dicen
  explicito para el lead-lag: 60 % de acierto y "ninguna ganancia con ordenes a mercado por el spread"). Como regla de ENTRADA si puede
  ahorrar parte del spread.
- En casa: confirmado el signo en 12 de 12 sesiones a 1-2 s; +0,39 ticks; muerto a los 30 s.
- Receta: ver finalista A. Probabilidad de que ahorre >= 0,10 pts por operacion neto de seleccion adversa: ~40 %. Es chico: baja el
  empate ~0,5 a 1 punto porcentual. No es "la clave que lo cambia todo".
- Fuentes: https://arxiv.org/abs/1512.03492 · https://papers.ssrn.com/sol3/papers.cfm?abstract_id=2970694 ·
  https://www.ssrn.com/abstract=2668277 · https://arxiv.org/abs/1312.0514 · https://arxiv.org/abs/1111.7103

### 2. Cinta caliente: la ACTIVIDAD se agrupa y es predecible (filtro de CUANDO, no de hacia donde)
- Que afirma: la llegada de cambios de precio en el E-mini S&P es un proceso que se autoexcita (Hawkes) con razon de ramificacion cerca
  de 1: casi toda la actividad es hija de actividad anterior, con memoria de minutos a horas (Hardiman, Bercot y Bouchaud 2013). VPIN
  (Easley, Lopez de Prado y O'Hara 2012) dice predecir "toxicidad"; Andersen y Bondarenko (2014) muestran, en el mismo E-mini, que
  su poder viene de la relacion mecanica con la intensidad y la volatilidad: controlando por eso no agrega nada. Conclusion practica:
  usar la intensidad directa, no VPIN.
- Horizonte: minutos a una hora. Tamano: grande para actividad y volatilidad, CERO para direccion.
- Para que sirve: con barrera +-8 en 600 s, en cinta fria muchas operaciones no resuelven (pagan costo y tiempo por nada) y en cinta
  caliente resuelven en 1-3 minutos. No cambia el acierto de un gatillo sin ventaja; le saca tiempo muerto a cualquiera.
- Receta: ver finalista B. Probabilidad de que sirva como filtro de tiempo: ~80 %; de que cambie el acierto direccional: ~10 %.
- Fuentes: https://arxiv.org/abs/1302.1405 · https://papers.ssrn.com/sol3/papers.cfm?abstract_id=1695596 ·
  https://papers.ssrn.com/sol3/papers.cfm?abstract_id=1881731

### 3. Banda de ruido intradia: dia de tendencia o dia de ruido (filtro de LADO para el CVD)
- Que afirma: Zarattini, Aziz y Barbon (2024): cuando SPY sale de su "zona de ruido" (apertura +- el movimiento absoluto medio a esa
  hora en los ultimos 14 dias) el movimiento tiende a seguir hasta el cierre. 2007-2024, neto de costos: 19,6 % anual, Sharpe 1,33.
  Gao, Han, Li y Zhou (2018): la primera media hora predice la ultima, R2 1,6 %, mas fuerte en dias volatiles.
- OJO: el acierto publicado es ~43 % (resena de quantmacro): gana por dejar correr, no por acertar. Con barrera SIMETRICA +-8 mi cuenta
  gruesa da ~54 %, por debajo del empate. Sirve mas como contexto ("hoy no se fadea") que como gatillo.
- En casa: el momentum de la ultima media hora (Baltussen) NO replico en jun-sep 2026 (NQ 46,7 % de 60 dias).
- Receta: ver finalista C. Probabilidad de pasar el protocolo: ~10 %. De servir como lectura: ~40 %.
- Fuentes: https://ssrn.com/abstract=4824172 · https://quantmacro.substack.com/p/paper-review-an-effective-intraday ·
  https://papers.ssrn.com/sol3/papers.cfm?abstract_id=2440866

### 4. Gamma de los market makers como interruptor "seguir o fadear" el empujon de flujo
- Que afirma: Adams, Dim, Eraker, Fontaine, Ornthanalai y Vilkov (2025): la gamma neta REAL de los market makers (de sus posiciones, con
  datos propietarios de CBOE) predice menor volatilidad del S&P en los proximos 10 minutos (se disipa en ~1 hora), reversiones mas
  fuertes del flujo de ordenes en el E-mini y menos momentum. Barbon y Buraschi (2021): momentum con gamma negativa, reversion con
  positiva, sobre todo en lo iliquido (acciones). Baltussen y otros (2021): momentum de la ultima media hora solo en dias de gamma
  negativa; exito diario 55 %, Sharpe 1,73 SIN costos. Amaya, Garcia-Ares, Pearson y Vasquez (2025): el efecto maximo sobre la volatilidad
  es chico (3,3 puntos de volatilidad anualizada diaria; 6,4 en ventanas de 30 min).
- Es la unica literatura con el horizonte del operador (10-60 min) y que une CVD con gamma. PERO:
  (a) usan posiciones reales de market makers, no un GEX armado con interes abierto o volumen;
  (b) en casa: regimen -> rango de 30 min no dio (p 0,98), Baltussen no replico, 11 hipotesis con niveles no ganaron;
  (c) **medido hoy: NO SE PUEDE PROBAR con el protocolo.** En las 12 sesiones de explorar el precio estuvo bajo el zero gamma casi todo
      el tiempo (9 de 12 ruedas con 0-1 % de los minutos arriba; las otras tres mezcladas, 38-51 %) y en las 8 de confirmar estuvo
      arriba (7 de 8 ruedas con 68-99 %; la excepcion es el 10-09 con 0 %). Fuente: B.niveles_m1(), columna d_zero, minutos de rueda.
      El regimen esta confundido con el corte explorar/confirmar. Y de yapa: en explorar (gamma "negativa") el delta de 60 s mostro
      leve REVERSION, no momentum: al reves de lo que pediria la hipotesis.
- Receta cuando haya ruedas de los dos regimenes en los dos tramos: disparo = |delta 60 s| >= percentil 90 de los ultimos 30 min con el
  precio moviendose >= 4 pts a favor del flujo; lado = seguir si d_zero <= -25, fadear si d_zero >= +25; barrera +-8/600; placebo
  permutando el regimen ENTRE DIAS (la muestra efectiva son dias, no disparos). Probabilidad: ~10 %.
- Fuentes: https://papers.ssrn.com/sol3/papers.cfm?abstract_id=5641974 · https://quantpedia.com/do-sp500-0dtes-options-increase-market-volatility/ ·
  https://papers.ssrn.com/sol3/papers.cfm?abstract_id=3725454 · https://papers.ssrn.com/sol3/papers.cfm?abstract_id=3760365 ·
  https://cdn.cboe.com/resources/education/research_publications/gammasqueezes.pdf · https://ssrn.com/abstract=4692190

### 5. Sorpresa del flujo y deficit de impacto (la version formal de "absorcion")
- Que afirma: Cont, Kukanov y Stoikov (2014): en ventanas de 10 s el cambio de precio es lineal en el OFI (R2 medio 65 %, contra 32 %
  del desbalance de operaciones), con pendiente inversa a la profundidad: esa pendiente es el "lambda de Kyle" practico. Pero es
  CONTEMPORANEO. Hacia adelante: Cont, Cucuringu y Zhang (2023) dan R2 fuera de muestra NEGATIVO a 1 minuto (-0,37 % con OFI propio,
  -0,10 % con OFI cruzado) y su estrategia ignora costos; Kolm, Turiel y Westray (2023) encuentran alfa hasta ~2 cambios de precio;
  en el E-mini S&P los shocks de flujo "se disipan casi por completo en un segundo" (arXiv 2508.06788).
- Idea testeable: como el flujo previsible ya esta en el precio, la absorcion solo significa algo sobre el flujo INESPERADO. Residuo =
  cambio del medio en 10 s menos lambda x OFI10 (lambda = pendiente movil de 30 min). Disparo: OFI10 en su decil extremo y el precio se
  movio MENOS de la mitad de lo esperado -> lado en contra del flujo; barrera +-8/600. En casa la "absorcion por vela" ya perdio; esta
  es la version al segundo. Probabilidad: ~10 %.
- Fuentes: https://arxiv.org/abs/1011.6402 · https://arxiv.org/abs/2112.13213 · https://papers.ssrn.com/sol3/papers.cfm?abstract_id=3900141 ·
  https://arxiv.org/abs/2508.06788 · https://arxiv.org/abs/cond-mat/0311053 · https://arxiv.org/abs/0809.0822

### 6. Iceberg por MBO (ordenes individuales de CME)
- Que afirma: Zotikov y Antonov (2019, Quantitative Finance 2021): los iceberg nativos de CME se detectan comparando el tamano visible
  de la orden con lo que realmente se ejecuta contra ella, y los sinteticos por las ordenes que reaparecen enseguida despues de cada
  ejecucion; con Kaplan-Meier predicen el tamano total. Frey y Sandas: cuando el mercado detecta un iceberg le pegan con ordenes a
  mercado, y cuanto mas se ejecuta MENOR es su impacto: es liquidez, no informacion. No publican "el precio rebota en el iceberg".
- Con nuestros datos: abs_bid / abs_ask ya aproximan esto (lo corre la familia "absorcion"). Con el MBO en vivo de ATAS se podria grabar
  la identidad de la orden que se recarga. Receta: iceberg = misma punta recargada >= 5 veces en 30 s con >= 50 contratos absorbidos y
  sin ceder; lado = a favor del que absorbe; barrera +-8/600. Probabilidad: ~10 %.
- Fuentes: https://arxiv.org/abs/1909.09495 · https://papers.ssrn.com/sol3/papers.cfm?abstract_id=1108485 ·
  https://www.cmegroup.com/articles/faqs/market-by-order-mbo.html

### 7. Barridos (trade-throughs)
- Que afirma: Pomponio y Abergel (2013): las ordenes que atraviesan mas de un nivel son pocas, grandes, se agrupan en el tiempo y sirven
  para medir quien lidera a quien; su impacto es mayor que el de una orden comun. No publican una regla rentable.
- Con nuestros datos: barre_c / barre_v (lo corre la familia "barridos"). Probabilidad: ~10 %.
- Fuente: https://ideas.repec.org/a/taf/quantf/v13y2012i5p783-793.html

### 8. Delta por tamano: quien opera MNQ
- Que afirma: Kurov y Lasser (2004): en los E-mini de S&P y Nasdaq la formacion de precio arranca en el E-mini y las operaciones de los
  locales de la bolsa informan mas que las de afuera. Hasbrouck (2003): ~90 % del descubrimiento de precio esta en el E-mini. Chordia,
  Roll y Subrahmanyam (2005): el desbalance de ordenes predecia 5 minutos en NYSE de los 90 y esa ventana se fue cerrando.
- Lectura: en MNQ "grande" no es institucional; el institucional opera NQ. El d50 de MNQ es mas un arbitrajista que una ballena.
  Receta: CVD de ordenes >= 10 contra CVD de ordenes de 1, divergencia de 5 min -> lado del grande; barrera +-8/600. Probabilidad: ~5 %.
- Fuentes: https://www.ssrn.com/abstract=404500 · https://onlinelibrary.wiley.com/doi/abs/10.1046/j.1540-6261.2003.00609.x ·
  https://papers.ssrn.com/sol3/papers.cfm?abstract_id=600121

### 9. Lead-lag ES -> NQ -> MNQ ("related trade")
- Que afirma: es una de las tres senales basicas de las firmas de alta frecuencia (Max Dama, Headlands: presion del libro, impulso de
  la operacion y operacion en el instrumento relacionado), valida "por un paquete" de datos. Huth y Abergel (2014): el mas liquido
  lidera, 60 % de acierto sobre el proximo movimiento del seguidor, y no deja ganancia con ordenes a mercado por el spread. Databento
  (2024) en ES: correlacion 0,11 del desbalance de la punta con el retorno a 500 operaciones, sin analisis de costos.
- Para un manual: nada como gatillo. Lectura util: si se graba el libro en vivo, grabar la punta de NQ (y ES), no solo la de MNQ.
  Receta: correlacion cruzada de retornos de 1 s a rezagos 1-5 s con el grabador nuevo. Espero ~0 a >= 1 s. Probabilidad: ~2 %.
- Fuentes: https://blog.headlandstech.com/2017/08/03/quantitative-trading-summary/ · https://arxiv.org/abs/1111.7103 ·
  https://databento.com/blog/hft-sklearn-python

### 10. Ultima media hora (cobertura de cierre) — ya descartada en casa
- Baltussen, Da, Lammers y Martens (2021), 60+ futuros 1974-2020: el retorno hasta las 15:30 predice el de la ultima media hora; exito
  diario 55 %, Sharpe 1,73 sin costos; sin efecto significativo en dias de gamma positiva. En casa no replico (NQ 46,7 %, ES 41,9 %).
  Una operacion por dia: con 20 sesiones no hay muestra. Fuente: https://papers.ssrn.com/sol3/papers.cfm?abstract_id=3760365

---

## FINALISTAS (regla exacta; NO corridos)

### A. "Cola a favor -> a mercado; cola neutra o en contra -> limite 10 s"
Regla de entrada, se monta sobre CUALQUIER gatillo. En el segundo t, con lado s (+1 compra, -1 venta):
  I[t] = (bidv - askv) / (bidv + askv) con la punta del cierre del segundo; favor = s x I[t].
  Si favor >= +0,33 (la fila de mi lado es al menos el doble): entrar a mercado (compra en ask[t], venta en bid[t]).
  Si no: limite en bid[t] (compra) o ask[t] (venta) por 10 s. Se da por ejecutada SOLO si el precio la atraviesa (bajo[t+k] < bid[t] en
  compra, alto[t+k] > ask[t] en venta, k = 1..10); si no, a mercado en t+10. Version exacta con la cinta, de a una sesion: ejecutada
  cuando el volumen agresor acumulado a ese precio despues de t supera bidv[t].
Prueba: 2000 segundos al azar por sesion en rueda, desagrupados 60 s, lado alternado (aisla el costo de la direccion). Barrera +-8/600
desde el precio y el segundo de ENTRADA real. Se compara contra "siempre a mercado en t".
Criterio: ahorro medio >= 0,10 pts por operacion, positivo en >= 9 de 12 sesiones de explorar; confirmar una vez, >= 6 de 8.
Riesgo conocido: seleccion adversa (la limitada se ejecuta justo cuando el precio viene en contra, y se pierde las que salen a favor).

### B. "Cinta caliente"
  N60[t] = ordenes en [t-59, t] (suma de n). A[t] = N60[t] / mediana de N60 en los 30 minutos anteriores.
  Caliente: A >= 1,5. Fria: A <= 0,7. Normal: el resto.
Gatillo base para evaluar: |delta 60 s| >= percentil 90 de |delta 60 s| de los 30 min anteriores, desagrupado 60 s, leido de las dos
formas (seguir y fadear). Por estado se reporta: % sin resolver con +-8/600, mediana de segundos hasta resolver, acierto de seguir y de
fadear, y puntos netos por operacion contando las no resueltas a precio de t+600 menos 0,85.
Pre-registro de lo que espero por la literatura: caliente < 10 % sin resolver y < 180 s; fria > 30 % sin resolver; acierto ~50 % en los
tres estados (sin afirmacion de direccion). Si el acierto de "seguir" en caliente supera el empate + 2 con >= 6 de 8 dias, seria
hallazgo; no lo espero.

### C. "Banda de ruido + CVD a favor"
  sigma(m) = promedio, en las 5 ruedas anteriores, de |cierre del minuto m / apertura de esa rueda - 1| (velas_m1; el paper usa 14 dias,
  aca hay 20 ruedas en total). Arriba(m) = max(apertura de hoy, cierre de ayer) x (1 + sigma(m)); Abajo(m) = min(...) x (1 - sigma(m)).
  Estado en t: FUERA ARRIBA si ultimo[t] > Arriba(minuto anterior ya cerrado); FUERA ABAJO si ultimo[t] < Abajo; si no, DENTRO.
  Disparo: FUERA ARRIBA y delta 60 s >= +percentil 80 de |delta 60 s| de los 30 min anteriores -> compra; espejo para venta.
  Desagrupado 60 s, barrera +-8/600 (principal) y +-12/900. Control obligatorio: el mismo disparo DENTRO de la banda, siguiendo el delta.
  Muestra: 6 ruedas utiles en explorar (28-08 al 04-09) y 8 en confirmar: es poca; si n < 150 en explorar, se declara "sin muestra".

---

## VEREDICTO DE LA FAMILIA

Como GATILLO DIRECCIONAL: nada. Ningun paper serio ofrece, con datos de la punta del libro, una senal de direccion a 1-10 minutos que
pague 3,4-4 ticks de costo; los que encuentran prediccion la encuentran a segundos y aclaran que no paga el spread. Como FILTRO y como
COSTO hay pistas probables pero chicas: como entrar (cola), cuando operar (cinta caliente) y en que dias no fadear (banda de ruido). La
unica linea publicada con el horizonte del operador que junta flujo y gamma (gamma real de market makers -> el flujo se devuelve o
sigue, 10-60 min) hoy no se puede probar: el regimen de gamma esta confundido con el corte explorar/confirmar.

