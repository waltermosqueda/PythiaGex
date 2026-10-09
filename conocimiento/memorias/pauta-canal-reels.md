---
name: pauta-canal-reels
description: "24-09: 'la pauta' = canal de reels de SP500/NQ (19+ videos en _sra); calce con el dia real en velas propias: v1=04-09, v4=14-09, v9=HOY 24-09 (ES 1 min, entrada 7733,50 a las 09:30, +10); reloj de sus graficos en hora argentina; medias EMA 200/30/~27 de 1 min (el operador acerto 20/30/200); entradas en extremos en los primeros 5-13 min de NY con limite en lineas/zonas, stop 7-10 ticks, objetivo 10-15 pts, BE rapido; gamma no explica nada."
metadata:
  node_type: memory
  type: project
  originSessionId: 6b882b35-31bc-4131-8cd4-4b099f670c25
  modified: 2026-09-25T04:46:14.213Z
---

Pedido 24-09 (noche): "analiza... todos los videos y creame el indicador estrategia que utiliza ese canal y construilo
en mi atas". Canal de Instagram (handle fuera del repo; en repo/memorias "la pauta"). Material FUERA de los repos en
`Escritorio\ATAS nada\_sra\` (mapa.json v1..vN, cuadros a 2/s, hojas, audio, subs con faster-whisper, analisis\, velas_hoy\
con ES m1/m5 hasta hoy exportadas de la cache de ATAS; preparar_nuevos.py ingiere lo que el operador deja en Downloads).
Laboratorio en `PythiaGex\laboratorio\pauta\` (calzar\ con digitalizar.py, calzar.py, niveles.py: la tecnica de calzar
un video con el dia real; METODO.md, INFORME_PAUTA.md; indicador atas\Pauta cuando salga).

Lo MEDIDO hasta ahora:
- Plataforma NinjaTrader 8 (Chart Trader), ES con 5 contratos (saltos de 62,50 USD en el P/L), NQ con 3.
- El reloj de sus graficos es hora ARGENTINA (NY + 1 h): "la apertura" cae en la vela de las 10:30 del eje.
- Calces: v1 = 2026-09-04 (entrada 7743,50 en 09:30-09:35, objetivo 7750,25 = EMA200 5m - 0,15); v4 = 2026-09-14
  (entrada 7669,25 = minimo del OR de 5 min = VAL nocturno = VWAP nocturno -1s; objetivo 7683,75); v9 = 2026-09-24
  (grafico de 1 min, entrada 7733,50 +-2 ticks a las 09:30-09:31, salida 7743,50 = +10, reloj +60,6 min).
- Medias (v9 vs cierres reales): EMA 200 (rmse 0,05), EMA 30 (0,08), EMA 19-35 mejor 27 (0,13). EMA, no SMA.
- "Marcaciones": lineas de tendencia/canales por pivotes del pre-mercado y la madrugada (v2 azul descendente, v9 canal con
  la superior por los maximos de 04:21-06:49 NY), zonas horizontales de otras temporalidades (v2 amarilla), niveles
  objetivo dibujados (v18 celeste, v19 azul). Las naranjas de v9: 7748,9 = max de Londres = EMA200 5m = 7750 = VAH 20d.
- Operativa: ordenes limite ya colocadas antes de la apertura en extremos/lineas, stop 7-10 ticks (2-2,5 pts), objetivo
  10-15 pts o el nivel dibujado, breakeven rapido ("stop loss en cero"), reingreso si lo saca por un tick, cierre manual
  si "se queda sin fuerza". Entradas en los primeros 5-13 min de NY (a veces antes de la apertura).
- v7 = lunes 2026-09-21, NQ 1 min: short 30248,25 a las 09:30 (12 pts bajo el techo; sin nivel, sin media, sin marca de
  Absorcion Viva: su "nos absorbio" fue un episodio sub-umbral de 129 comidos 3 s despues del techo); long 30250,50 a las
  09:34:50 -> +45,5 explicado por una ola compradora (delta +994 MNQ en 60 s), no por nivel; la linea azul 30267 = maximo
  de Londres reprobado a las 08:04-08:13 (1 tick). Las flechas de sus videos son ejecuciones de NinjaTrader.
- v6 = viernes 2026-09-18, ES 1 min: short 7709,25 en la vela 09:30-09:31 -> 7699,25 (-10 fijos) a las 09:50; la zona azul
  de arriba (7711,6-7715,0) = maximo del pre-market 7715 = VWAP nocturno arriba y VAH de ayer 7711,75 abajo; la entrada
  quedo 2,4 pts bajo la zona, a 0,6 tick de la EMA 200 de 1 min (como v9); zona inferior fuera del encuadre.
- v5 = jueves 2026-09-17, ES 5 min: short 7715,50 entre 09:26 y 09:29 (antes de la apertura), objetivo 7705 = EMA 30 de
  5 min (su linea naranja) = origen del impulso de las 08:00; el nivel no fue respetado despues (96 ticks); la extension
  sobre la EMA 200 5m (p91,5) no es regla (43 % vuelve vs 75 % placebo); vendio contra delta comprador.
- Gamma (zero/dominantes/muros de nuestras cadenas): no explica entradas ni objetivos en los 5 calces (o sin dato;
  el zero 0DTE de v6 coincide con baja confianza). Bug colateral: estela-SPX-2026-09-18.jsonl trae valores de NDX.
- v1 y v9: la entrada no cae en el p10 de 35-38 familias de niveles (ruptura simple de los maximos previos en el minuto
  de apertura); v4 si (OR5 low + valor nocturno). Muestra chica: no es regla.

- Primera pasada (24-09, 22 agentes): METODO.md, P_00_PREREGISTRO.md, 4 formalizaciones P1-P4 medidas: NINGUNA pasa
  (0 de 7 criterios en MNQ; aciertos 8-11 % vs 16-17 % necesarios; iguales o peores que un bracket ciego a las 09:30:01);
  4 escepticos no refutan el "no pasa"; INFORME_PAUTA.md entregado. Indicador `atas\Pauta` (Pauta.dll 66.048 B, md5
  429569b1..., paridad C#/Python 100 % recorrida por mi: 42+34 ops MNQ, 41+27 MES, 0 diferencias) COPIADO a
  %APPDATA%\ATAS\Indicators y en reiniciar_e_instalar.ps1, pero TODAVIA NO agregado a ningun grafico (00:05 25-09: el
  dialogo de indicadores devolvia 0 items por UIA mientras el operador usaba la PC; retomar cuando el avise).
- Fechas de publicacion leidas de Instagram (datetime exacto, `_sra\analisis\fechas.csv`): los 22 videos unicos van del
  24-07 al 24-09-2026 y 20 de 22 se publicaron entre 09:41 y 10:30 NY del mismo dia de la operacion; el shortcode del
  reel codifica el timestamp (pk = base64url; ms = (pk>>23)+<numero>), sirve para fechar sin abrir.
- 25-09 00:05: la cuenta Lucid (Rithmic) del operador caduco; hasta que compre otra no hay datos en vivo (Absorcion
  Viva, Delta Vivo y Pauta no van a marcar nada). Segunda pasada wf_3337e6f8-1d7 (13 videos nuevos + calce de 16) corriendo.

**Why:** el operador quiere replicar la estrategia del canal; sin fecha real ni precio real todo era estimacion; el calce
convirtio los videos en datos.
**How to apply:** toda afirmacion sobre el canal con video+segundo o archivo de calce; ampliar el calce a los 19 videos
(wf_pauta2.js en _sra) antes de formalizar niveles; no presentar nada como rentable sin la medicion. Ver
[[directriz-replica-tradingview]], [[busqueda-estrategia-sin-parar]].
