---
name: rondas-12-14-resultados
description: "13-11 noche: rondas 12-14 (~80.000 variantes: sesion/calendario, opciones->futuros, rango, entre activos, barrido masivo 59 mil + ML, literatura, gamma a fondo, griegas, order flow, patrones de giro) + la referencia ORIGINAL 7 dias x 3 legs: NADA pasa el examen sellado; las rayas de gamma no rebotan mas que rayas corridas; lo unico estable: tamaño del movimiento y deriva alcista"
metadata:
  node_type: memory
  type: project
  originSessionId: f3419dc9-e1cb-4b1b-99aa-8707c1a3c2f4
  modified: 2026-11-14T02:57:49.302Z
---

Carpetas: `PythiaGex/laboratorio/ronda12/{A_sesion,B_opciones,C_rango,D_activos,E_literatura}`, `ronda13/{F_barrido,G_vrp,H_opex}`,
`ronda14/{I_gamma,J_griegas,K_flujo,L_patrones}`, `la referencia/` (g01 lectura por pixeles, g02 rebotes). Protocolo: `ronda12/PROTOCOLO.md`.
- A: deriva nocturna diseño t 3,27 -> examen 2020-26 t 1,61 (= beta alcista); FOMC murio post-2014. C: Zarattini (ruido/ORB) t -1,2..+0,8;
  ^GSPC da rupturas FALSAS (precios viejos del indice contado): no usarlo para momentum. D: 300 reglas entre activos, nada.
- B/G: "VRP alto -> largo 18:00->16:00" examen t 2,3-2,7, 7/7 años, pero diseño flojo; controlando VIX y caida previa no agrega
  (t 0,04-0,28); no entra en el MLL de Lucid (0/54 configs); validarla llevaria 7-21 años. Solo seguimiento en papel (PREREGISTRO_ADELANTE).
- F: 59.348 reglas + 90 ML: max t 4,06 = lo que da el ruido (SPA p 0,42). H: tercer viernes vivio 2003-21 y se dio vuelta 2022-26;
  ultima media hora REVIERTE en 2024-26; DIX/GEX = deriva.
- I (45 ruedas, 732 toques, retraso CBOE real): rebote real 54,5 % vs placebo 52,3 % en examen; limitada -0,83 pts/op t -2,29.
  Por que el operador "ve" rebotes (medido): tasa base de cualquier raya >50 %; en 15 min pasan +3 y -3 casi siempre; los rebotes
  perfectos no llenan la limitada; ~9 rayas activas, 2+ a <10 pts (precio a <=1 pt de alguna el 18-24 % del tiempo); el 85-88 % de
  los giros de 5 pts ocurre lejos de toda raya.
- J: charm siempre negativa y vanna siempre positiva (no separan dias); GEX no mejora el pronostico ATR de rango.
- K: comprar la absorcion pierde (stop 73-77 %); HVN 55,5 vs 49,8 diseño -> 47,6 vs 47,6 examen. L: tramos = azar a toda escala
  (H y VR = placebo); ML AUC 0,48-0,54; agotamiento clasico pierde antes de costo.
- la referencia ORIGINAL (leido por pixeles, 7 ruedas 05..13-11, SPX 0DTE / SPY Full / SPX Full): rebote real 54-60 % vs placebo
  56-63 %; limitada -1,5..-2,2 pts/op (peor que el placebo). History Days maximo = 7 (8 da "Value is out of range");
  en un grafico de MES el Leg ofrece solo SPX y SPY; Expiration DTE0/DTE1/Full. Prohibido tocar su CODIGO; sus AJUSTES si.

- RONDA 15 M "cuando y hasta donde" (`ronda15/M_cuando/`, 13-11, pre-registro 22:10:42, examen abierto una vez 22:11:51):
  36 rasgos del toque + 4 de X-Ray, xgboost/logistica/T-learner/cuantiles: AUC examen 0,494 CBOE / 0,538 la referencia; 0 de 6
  hipotesis pasan. Rebote fp5/5 examen real 55,0 % vs placebo 51,7 % (recalculado por mi). Hasta donde = volatilidad, no la
  raya: MFE30 mediana 8,5 real vs 8,7 placebo; ~6 pts mercado tranquilo, ~10-11 movido; MAE igual de grande. Limitada 5/5 en
  la raya 45 dias: -0,58 (t -2,40, todos los toques) / -0,72 (uno por minuto). Casi: D2 +10 pp vs su placebo en 35 dias
  (t 3,0, elegida entre 7, no paga). Muestra: 3 pp a t 3 = ~146 ruedas; +0,5 pts/op = ~89 ruedas (la referencia ~43).
- RONDA 15 N (`ronda15/N_confluencia/`, examen 22:37:38): raya + nivel de volumen (POC/VAH/VAL ayer, VWAP±σ, ONH/ONL,
  IB) parecia +8..+12 pp en diseño y dio -1..-4 pp en examen. IMAN (llega a la dominante/zero antes de alejarse lo mismo,
  60 min): crudo real 35,0 % vs placebo 33,0 % (+2 pp, t 2,3 por dia) PERO es mezcla de distancias (las rayas reales estan
  mas seguido a 5-10 pts); dentro del mismo tramo de distancia +0,4 pp, t 0,33, 9/18 dias (recalculado por mi). Por eso
  el operador "ve" atraccion: la dominante se arma cerca de donde se opera. Siguiente nivel: cero; el ATR predice mejor.
- RONDA 15 O (`ronda15/O_cinta/`, examen 22:48:22): cinta del ES orden por orden (49/49 ruedas 08-09..13-11, desfase 0 s)
  en el toque afinado al ms: absorcion, recarga, barridos, grandes, aceleracion, esperar 30 s = nada da direccion en
  examen; reglas H3/H4 se dieron vuelta (-1,37 pts/op t -2,9). Solo TAMAÑO: actividad del 1er minuto -> tamaño del
  movimiento (t 3,7). Trampa medida: medir desde la raya tras esperar W s inventa t de 7-13 por geometria; medir desde el
  precio en el instante de decision. A mercado 5/5 examen: -1,22 con llenado real de cinta vs -0,67 con la base de la M
  (recalculado; la diferencia es el modelo de llenado). Rasgos por evento: `O_cinta/datos/rasgos_cinta_por_evento.parquet`.
- RONDA 15 Q (`ronda15/Q_tipos/`, examen 23:38:09): tipos de toque por la forma de la llegada (k-means/DTW, 34.330
  ensayos). Un solo eje: de donde viene el precio y que tan rapido. CBOE examen: nada (B3 "estacionado cerca" +0,9 pp).
  UNICA PISTA: la referencia en B3 (30 min quieto a ~0,7 ATR/~5 pts de la raya y la toca) rebota +9,6 pp sobre su placebo,
  5/5 dias (recalculado por mi: 60,4 % vs 50,8 %, por dia +9,3 pp t 4,3) PERO solo 5 dias, las rayas CBOE esos dias dan
  -3,7, y la plata es chica: a mercado 5/5 +0,39 pts/op (t 0,6, n 92). Validarla pide ~10-37 dias NUEVOS de la referencia
  grabado hacia adelante. Llegar de muy lejos y rapido (B5) rebota MENOS en cualquier raya (-8 pp, 45 dias t -3) pero la
  ruptura tampoco paga.
- RONDA 15 P (`ronda15/P_anatomia/`, examen 23:43:04): anatomia del STRIKE tocado: GEX/DEX/vanna/charm + vomma/speed/
  zomma/color (verificadas por diferencias finitas), flujo calls/puts, IV/skew, order blocks, cinta "antes", X-Ray por
  participante, y dominantes de ES EN VIVO (viva3, 13 ruedas). 172 variables: AUC examen 0,551 (IC 0,49-0,62); ranking
  de rebote NO estable entre mitades (corr -0,10); teoria de gamma al reves; 0/49 reglas de rebote positivas en examen.
  Ruta promedio -30..+30 real = placebo (a +30: +0,88 vs +0,46 pts, recalculado por mi desde velas crudas). Ranking de
  TAMAÑO si estable: velocidad del ultimo minuto, ATR, grandes/barridos en contra 5 min, IV del strike. Casi post-hoc: parte
  del volumen 0DTE reciente en el strike achica el recorrido a favor 15 min (t -3,8 en 45 dias) sin cambiar el rebote.
  No hay flujo firmado de opciones (vol_compra/venta = 0). Incidente declarado: la prueba en seco escribio trayectorias del
  examen y se borraron sin mirar (adenda en el PREREGISTRO antes de abrir).
- X-Ray por participante: `xray/paginas/barras.csv`. Indicadores de terceros protegidos (<estudio de flujo de la referencia>, "Flow") no se agregan
  si no estan activados; el equivalente de HIRO en ATAS = X-Ray Hedge Pressure + Flow Delta/Flow por participante. Indicadores de terceros protegidos (<estudio de flujo de la referencia>, "Flow") no se agregan si no
  estan activados; el equivalente de HIRO en ATAS = X-Ray Hedge Pressure + Flow Delta/Flow por participante.

**How to apply:** no presentar ninguna raya/griega/patron como gatillo; lo que sirve es TAMAÑO (stops/objetivos) y gestion
(guardian). Para seguir con la referencia/X-Ray hace falta GRABAR hacia adelante (la referencia solo guarda 7 dias). Ver
[[xray-historia-medida]], [[pythiabot-1-0]], [[estrategia-ronda-8-edge-personal]], [[modo-cortes-mouse]].
