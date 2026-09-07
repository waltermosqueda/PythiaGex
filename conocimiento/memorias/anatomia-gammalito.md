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
