---
name: anatomia-gammalito
description: "Los 17 videos de GAMMAlito transcriptos y vistos cuadro por cuadro (2026-09-07): que es cada pieza, que afirman, que se midio. Max Change = las 3 pelotitas de cada barra son la punta hace 15/5/1 min; las zonas verdes/rojas del video en vivo son de otro indicador (Zenith); 'se acelera el tape' no le gana al placebo en nuestro tape."
metadata:
  type: project
---

Pedido del operador el 2026-09-07 (05:20): analizar 9 videos de GAMMAlito
"con todo tu poder" para extrapolar a ATAS sin apuro. Se transcribieron 17
(los 9 mas 8 relacionados) con Vosk y se miraron 39 pliegos de cuadros.
Pagina: "Anatomia de GAMMAlito" (artifact). Transcripciones en
PythiaGex/conocimiento/gammalito/.

## Piezas y que son
- Perfil izquierdo = GEX por strike (verde +, rojo -); "manda la mas larga".
- Perfil derecho = Gamma Aceleracion (celeste/violeta); lo leen como
  COMBUSTIBLE: "la zona morada es espacio de gasolina para continuar".
  Es nuestra aceleracion (dGEX por 1 %), nosotros no la leiamos asi.
- Major Positive / Negative = Call/Put Wall. Zero Gamma = regimen.
- Dominantes = guiones amarillos por vela (primaria/secundaria); "el
  precio las respeta consistentemente".
- MAX CHANGE = las tres pelotitas de cada barra: grande 15 min, mediana
  5, chica 1: donde estuvo la punta de la barra. Adentro = crece, afuera
  = decrece, alineadas = sostenido, desparramadas = indeciso. NO son
  operaciones grandes (eso son los circulos con numero sobre las velas,
  los Big Trades). Nuestros dos puntos en las barras son un invento
  nuestro con otro significado.
- Perfil por vencimiento: web "Configurar perfil: Metrica GEX; Hoy /
  Latest / Next"; izquierda 0DTE, derecha total. Ninja: H1 y M15.
- Las cajas verdes/rojas del corto "en vivo" son SupportResistanceZenith,
  otro indicador. Las cajas de entrada/stop/objetivo, dibujos del trader.
- Matrix = otro panel (Early Bull/Bear), calculo no visible.
- Producto: NinjaTrader 8 + Web (TradingView), powered by Gexbot, Chalito
  Trader; 90 USD/mes, pack 120; señales "Quant" por Discord.

## Plantilla que enseñan
Entrada en rechazo de dominante bajo el Major Positive; stop del otro lado
de la dominante; objetivo 1 zero gamma; objetivo 2 siguiente dominante.
Cuatro preguntas: zona de resistencia gamma?, CVD a favor?, volumen
institucional?, zona de reaccion o medio de la nada?
Ejemplo Quant: NQ corto 29.301, stop 29.383,78 (82,8 pts), objetivo
29.230,05 (71 pts): relacion 0,86, necesita >54 % de aciertos. No dicen
el porcentaje.

## Lo medido (laboratorio, con placebo)
- "Respeta las dominantes": gamma x OI pierde -4,0 pp; NQ 4 dias -2,9 pp;
  ganan volumen del dia (+42) y gamma x volumen (+22).
- "Se acelera el tape" (afirmacion literal): medido en el tape de MES por
  vela de 1 min (1.051 velas): en nivel vol x1,17 / ops x1,15 / |delta|
  x1,24, PLACEBO x1,13 / x1,11 / x1,18. No hay aceleracion atribuible al
  nivel; las velas grandes tocan mas lineas. MNQ 75 velas: exceso chico
  (delta x1,23 vs x1,03), un dia, a seguir midiendo.
- Zero gamma como regimen: NO medido todavia (entra al laboratorio).

**Why:** el hermano menor copio la pantalla sin entender las pelotitas ni
que el perfil de ellos "respira" por volumen. Lo que gana en el
laboratorio es justo eso: la actividad de hoy.

**How to apply:** orden con compuerta: (1) GEX por volumen del dia como
fuente opcional de barras y dominantes, compuerta = ventaja sobre placebo
en 3 dias nuevos; (2) Max Change 15/5/1 en nuestras barras, despues del 1;
(3) leer la aceleracion como combustible y medir rango tras entrar en
aceleracion negativa; (4) regimen del zero medido; (5) su plantilla como
hipotesis del centinela, no como sistema. No copiar: zonas de Zenith,
"siempre lo supo", señales con relacion <1. Ver [[que-afirma-gammalito]],
[[laboratorio-formulas]], [[es-vs-nq-respeto]], [[pelotitas-son-eventos]].

## Segunda pasada (los 8 relacionados completos, 2026-09-07 06:10)
- La web dibuja NQ con el libro de QQQ ("NQ_QQQ") y ES con SPY o SPX
  ("ES_SPY", "ES_SPX"). Matrix rotula "718.5 GZ 29260.00", "720 29651",
  "715 29445.25": strikes de QQQ llevados a NQ por razon ~40,7. Nuestro
  MNQ usa NDX (2x el libro propio, muros dados vuelta). Hipotesis medible:
  dominantes de MNQ desde QQQ contra NDX, con placebo.
- Perfil derecho hasta 90 DTE ("gamma absoluto teoricamente"); izquierdo
  0DTE. Nosotros 7d barras / 45d radar.
- CVD en panel propio y big trades con tamaño (251, 211, 433 contratos)
  son los confirmadores de la entrada: "el CVD confirma la caida".
- Matrix: Early Bull Zone / Early Bear Zone, "weekly level", VWAP. Calculo
  no visible.
- "Asi funciona" (sin audio, solo texto): "Mayor volumen en Gamma Positiva",
  "Rechazo en Max. Gamma Negativa", "Take profit en dominante", "Pullback a
  dominante", "Big Trade".

## El canal (2026-09-07 06:40)
Mapa en PythiaGex/conocimiento/gammalito/canal.md: 332 videos; Programa
Educativo (0 gamma, 1 contexto tendencia/rango, 2 dominantes, proximo zero
gamma); largo de Convexidad ("5 escenarios GEX + convexidad, resorte vs
tobogan") y webinar de 1 h; 55 cortos de operativa con titulos que anticipan
funciones: "Max Change predice dominante, indicador adelantado", "NDX, QQQ,
NQ: la informacion decisiva", "Big Trades: que significan", "Donde esta la
cobertura". En el navegador integrado NO se pueden leer las transcripciones
de YouTube (panel girando, get_transcript 400): hay que transcribir el audio.

## Programa educativo y convexidad (transcriptos 2026-09-07 07:00, 4 videos)
- CONVEXITY LADDER = el perfil derecho. Convexidad positiva (aguamarina) =
  colchon/trampolin, dealers frenan, reversiones; negativa (purpura) =
  tobogan, dealers amplifican, tendencia. Tabla textual: mucho GEX + positiva
  = "iman colchon" (mean reversion/fade, riesgo bajo); mucho + negativa =
  "nivel explosivo" (breakout/momentum, alto); poco + positiva = "mercado
  estable" (rangos amplios/reversion, bajo); poco + negativa = "salvese quien
  pueda" (tendencial/trailing, muy alto, reducir tamaño). Transicion: por
  encima del maximo GEX con conv. positiva = rango; perdido el maximo GEX y
  conv. negativa = tendencia. Flujo: 1 pico de GEX cerca? 2 ladder positiva
  o negativa? 3 regimen 4 estrategia.
- Dominantes (video 2): tres condiciones para el rebote: contexto del dia
  (balanceado respeta, expansivo rompe, mas si el GEX cae), distancia al
  zero gamma (lejos = rebote; recien cruzado = rupturas), CVD (divergencia =
  frena; confirma = rompe). Iman: MMs compran al bajar hacia ella, venden al
  subir hacia ella.
- Video 1: gamma neta positiva y alta = rango; negativa = direccional.
  "El contexto no lo define el precio, lo define la gamma".
- Web: SPX primero; clasificacion Gexbot con UMBRALES de gamma +/-; perfil
  por metrica (exposicion/aceleracion) y vencimiento (90d, 0DTE, 1DTE;
  "trabajamos en 0DTE"); 2 dominantes por defecto (hasta 5); Big Trades
  con umbral en contratos (~180 en NQ, mas en ES).
- Consecuencia: nuestra aceleracion ES la convexidad; falta el cuadrante
  de regimen y la alerta de transicion; y las tres condiciones de la
  dominante son medibles con el centinela.

## Video 0 y los dos del Max Change (2026-09-07 07:20)
- LOS SIETE COMPONENTES, en su orden: 1 GEX de Gexbot (termometro: cuanta
  gamma tienen que cubrir los MMs ahora); 2 Zero Gamma (el nivel mas
  importante del dia); 3 Major Positive (presion vendedora de cobertura
  maxima) y Major Negative (compradora maxima): "zonas extremas del dia";
  4 Dominantes (alta concentracion entre los majors, imanes); 5 Convexidad
  ("no mide cuanta gamma hay sino que tan rapido cambia con el precio;
  precede a los movimientos fuertes"); 6 BIG TRADES = "alertas en tiempo
  real de operaciones institucionales EN EL MERCADO DE OPCIONES" (no son
  prints del futuro: son bloques de opciones, dibujados sobre la vela del
  momento con su tamaño en contratos; umbral ~180 en NQ/QQQ); 7 CVD del
  futuro ("si el precio cae y el CVD sube, divergencia, posible reversion").
- "Como leer el Max Change": "estas barras marcan EL VOLUMEN que tiene este
  strike en opciones... cuando logra ser la mas larga de todas empieza a
  ser la dominante; lo mas importante no es la dominante sino la pelotita,
  es adelantado". CONFIRMADO: el perfil de GAMMAlito respira por VOLUMEN
  del dia; la dominante es la barra mas larga; el Max Change (15/5/1 min)
  la anticipa. Es justo la formula D (volumen puro) que gano +42 pp.
- "Max Change predice dominante": una dominante nace como "semillita" en
  el Max Change, con las pelotitas alineadas minuto a minuto, y 45 min
  despues es la dominante.
- Gamma positivo vs negativo: zero gamma "como un trailing stop del
  mercado", dinamico; positiva = volatilidad suprimida, MM vende maximos y
  compra minimos, estructura en escalera; negativa = el MM tiene que vender
  cuando le venden, aceleracion.
- PANEL "GAMMA ESTADISTICA" de la web (visto en el corto del Max Change):
  bloque VOLUME (zero gamma, major +/-, net gex) y bloque OPEN INTEREST
  (major +/-, net gex), con signos opuestos en ese momento (-4,99 vs
  +1,80); y "MAX CHANGE GEX IN OPTION": strike con mayor cambio de GEX en
  1/5/10/15/30 min con magnitud en Bn (723: -3,35 Bn a 30 min -> fue la
  dominante 45 min despues). Strikes de QQQ. Nuestro AUDIT ya tiene
  netgexvol/majorposvol/majornegvol/zerovol: es el mismo par de libros.
- Web: NDX, QQQ y NQ "sincronizados en una sola pantalla".
- Cortos "Donde esta la cobertura" y "sesion de movimiento o rango"
  (07:40): a "ES o NQ?" contestan "da exactamente igual si operas a
  ciegas; el activo no importa, importa el contexto: dominantes, GEX y
  CVD". Y: tras una caida el mercado no siempre sigue; se armo un rango
  entre max gamma negativo y positivo con dominantes de soporte y
  resistencia; "no todas las sesiones son para perseguir movimiento".
- Cortos "Gamma en tiempo real" (trade en vivo: "la gamma apoyo el contexto alcista, la usamos de piso") y "Asi se arma una dominante" (mismo audio que "Miren esto en vivo"): sin mecanica nueva.
- "Que es GAMMAlito" (27 min, 08:00): Gexbot es el proveedor ("options
  analytics, informacion en vivo de las opciones"; muestra OI y GEX por
  volumen); la suscripcion de GAMMAlito incluye el paquete de datos por
  licencia comercial. "Las lineas rojas y verdes son donde los market
  makers tienen que cubrir: hacen de resistencia y soporte, atrae el precio
  y lo repele"; Backtesting Market las llama "sopladores". Big Trades:
  "llego a esa zona y aparecieron los colores: algo paso, tuvieron que
  hacer una cobertura" (incluido en la suscripcion, antes habia que tener
  Ninja o ATAS). QQQ -> NQ: "transformarlo para que la informacion del QQQ
  se muestre en el grafico del NQ, mucho mas limpio". Order flow "se va a
  incluir proximamente"; version 13.
- Instagram (08:30): las señales Quant tienen familias por regimen: "las WFD
  solo funcionan cuando NDX esta en gamma negativa; las otras cuando gamma
  positiva; se mira en el Analizador de la web (NQ_NDX): numero en rojo =
  gamma negativa, verde = positiva". Y un clip de operativa: "las pelotitas
  como confirmacion, entrar con el stop debajo; por arriba no tengo
  dominantes".
- El de 51 min ("Deja de adivinar") es el stream completo del que salio el
  de 27: mismas afirmaciones (volumen, pelotitas 1/5/15, QQQ->NQ, Big Trades
  en cobertura, order flow proximo, version 13).
