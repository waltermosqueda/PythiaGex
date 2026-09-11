---
name: gatillo-cientifico-2026-09-10
description: "Banco de gatillos (11 hipotesis, train/test, placebo): nada a mano; techo con ML: en ES a 10 min si hay señal (61 % con p>=0,70, tarde 83 %), portada como gatillo MODELO 1.7b; NQ nada; el REBOTE en las rayas amarillas (1.8, pedido con sus 7 ejemplos) iguala al placebo: 106 disparos/dia al 46 %; unico bolsillo (primer toque de la dominante actual con el zero a favor, 3,2/dia, 50 vs 42 % en MNQ) no aparece en MES: hipotesis, no señal."
metadata:
  type: project
---

Pedido del operador (2026-09-10): "un gatillo long/short que acierte, sin ruido, con
dominantes, convexidad, pelotitas, 0DTE, order flow; medir cuanto acerto y subio".
Se hizo como investigacion, no como corazonada:

- **laboratorio/gatillo_cientifico.py**: 11 hipotesis definidas antes de mirar,
  objetivo a escala (NQ 25 pts, ES 6), reglas simetrica y 2:1, entrena 8-9 dias /
  prueba el resto, permutacion del lado (tasa base real) y placebo con niveles
  corridos. NINGUNA gana en prueba con 15+ casos. Pista debil: "zero cross con
  convexidad positiva -> fade" (52/60 % MES n=25; 54/54 % MNQ n=26; placebo 52 %).
- **laboratorio/ml_check.py** (techo): logistica y bosque con todos los rasgos,
  validacion por bloques de dias: AUC 0,51-0,52 MNQ, 0,48-0,51 MES. Moneda.
- **laboratorio/momentum_intradia.py**: Baltussen, Da, Lammers y Martens (JFE 2021,
  cobertura de gamma -> momentum de los ultimos 30 min) NO replica en jun-sep 2026:
  NQ 46,7 % (60 dias), ES 41,9 % (62). Con convexidad negativa 63,6 % de 11 dias.
- Regimen -> volatilidad: convexidad negativa NO agranda el rango de 30 min (NQ 62,8
  contra 71,5 pts; permutacion por dias p 0,98; ES igual).

**Why:** los datos que tenemos (cadena de CBOE a 15 min de retraso y cada 15 min, order
flow por minuto) no contienen direccion a 30 min. Prometer un gatillo "que acierte"
seria mentir; el operador pidio ciencia y esto es lo que da.

**How to apply:** no dibujar gatillos direccionales nuevos. Volver a correr los tres
scripts cuando haya 3-4 semanas de cadena viva de Rithmic (volumen real por strike) y
cuando se acumulen big trades (of.big_*). Cualquier idea nueva se juzga con
gatillo_cientifico.py antes de tocar el indicador. Ver [[gatillos-order-flow-banda]],
[[pelotitas-max-change-medidas]], [[laboratorio-formulas]].

**LO QUE SI GANO (misma tarde, 2026-09-10, Gamma Hoy 1.7b "gatillo MODELO"):** el techo
con ML a horizontes CORTOS: MES 1 min, +3 antes que -3 en 10 min, regresion logistica
con 10 rasgos (momentum corto + distancias a zero/majors/dominantes/Max Change):
AUC 0,57 fuera de muestra; con p >= 0,70, 61 % (14 por dia); avance hacia adelante
63,8 % de 370 y en la TARDE de NY (14-16 h) 83,1 % de 59 (7 por dia). Con los niveles
permutados pierde la señal: es la interaccion niveles x order flow. En 2 min (el
grafico del operador): 62 % de 53 y tarde 76,7 % de 30 (mas debil). NQ: nada. Regla
2:1 no sirve (objetivo chico, 3 pts). Portado a C# con equivalencia vela a vela
(99,94 %). Los toques-y-reversion en dominantes/zero (toques_reversion.py) dan
muestras chicas (10-30 toques en 16 dias) y sin ventaja sobre placebo. Se juzga con
cada dia nuevo (pythiagex-gatillos-*.jsonl, tipo modelo·es10).

**Toques y rebotes (rebote_niveles.py, 14:30):** medido en 1/2/5 min, MNQ y MES, por
nivel/franja/traspaso/delta/cuadrante y con "reclamo" de falsa ruptura: el rebote real
iguala al placebo (MNQ M1 53,6 vs 59,7; M2 64,4 vs 64,0; M5 67 vs 65; MES 50 vs 54, 52
vs 62, 59 vs 57). Los subgrupos ganadores no se repiten entre temporalidades. Hallazgo
lateral: en la primera hora, tras una llegada rapida a cualquier punto, el precio
retrocede 20 pts antes de seguir el 68-83 % (real y placebo): reversion a la media de la
apertura, no el nivel. No hay "formula" de rebote en dominantes con estos datos.

**REBOTE en las rayas (1.8, 2026-09-10, 15:00):** el operador mando 8 capturas de MNQ de
hoy con rebotes en rayas amarillas y pidio "el gatillo que coincida con todos, sin
excusas". Las rayas donde entra son TODAS las dominantes del dia (filas de guiones
viejas), mas zero y majors: no solo la vigente. Regla (GatilloRebote.cs =
laboratorio/gatillo_rebote.py): toque +-10 / traspaso <= 25, venia de >= 15 en 10 velas,
cierra del lado bueno, primera vela, enfriamiento 5; niveles con valor exacto e
identidad por strike (equivalencia indicador-laboratorio 28 de 30 al minuto). Los 7
ejemplos disparan. En 16 dias: 102 disparos/dia, +20/-20 en 45 % = placebo (rayas
corridas +-85/145); ningun filtro (lado, zero, apertura, momentum, delta, traspaso,
franja, cuadrante, n de toque, edad, confluencia, llegada rapida) lo separa. Unico
bolsillo: primer toque de la dominante ACTUAL a favor del zero, 3,2/dia, 52 casos, 50 %
(placebo 42), +10/-10 60 (39), mitades 50/50; pero MES 36 (40), M2/M5 igual, hoy 0/2:
hipotesis a seguir midiendo, no señal (gatillo_rebote_validar.py). Se
instalo igual como triangulo hueco "R" (SoloTendencia default, Todos, PrimerToqueActual,
Ninguno) y se registra como rebote·dom/zero/major para juzgar con dias nuevos. Lo que
gano en sus ejemplos fue el dia alcista, no la raya: decirselo de frente, con los
numeros, y no prometer.
