---
name: auditoria-2026-09-14-referencia-es
description: "Auditoria del 14-09 contra capturas en vivo de la referencia: el panel de ES es SPX (o SPY) 0DTE gamma x volumen NETO por strike, reproducido en 5 horarios con la cadena cruda de CBOE; zero por cambio de signo; conversion ES = K + (ES - SPX); el indicador de ATAS coincide consigo mismo pero dibuja OTRO libro (Rithmic ES); el multiplicador 100 infla 2x el titular del libro de ES."
metadata: 
  node_type: memory
  type: project
  originSessionId: cfe2e2e1-5319-4bee-a9ea-7507c655c3e4
  modified: 2026-09-14T16:11:19.626Z
---

Medido el 2026-09-14 (12:50-13:10 local) sobre 12 capturas del operador de la
referencia (09:33 a 11:51 ET) y la cadena cruda de CBOE (archivo de la nube +
dos bajadas directas), con la viva de Rithmic para el precio del futuro.

## Como alinear una captura con la cadena (costo entenderlo)

- El log del indicador (`pythiagex-gammahoy.log`) esta en hora LOCAL Argentina
  (UTC-3); las capturas de la referencia dicen "UTC-4". Captura 11:51 = log 12:51.
- CBOE llega 15 min tarde: la cadena que refleja el mercado de una captura a las
  HH:MM ET es la de `cadena_ts` = HH:MM + 4 h + 15 min (UTC). La referencia es
  en tiempo real: a las 09:36 ET ya dibujaba barras que en CBOE recien aparecen
  en la cadena de 13:51 UTC.
- El precio de ES de la referencia es el contrato de SEPTIEMBRE (7631,25 a las
  11:51 ET, con ESU6 en Rithmic a 7630,12). Yahoo ES=F ya rodo a diciembre.

## Lo que reproduce (ES, cinco horarios: 09:36, 10:03, 10:21, 11:12, 11:46 ET)

- **Perfil izquierdo "SPX 0DTE"** = gamma Black-Scholes x (vol calls - vol
  puts) del 0DTE de SPX, por strike, llevado a ES sumando la base medida
  (ES - SPX = +5,8 / +2,8 / +1,0 / +2,8 / +3,3 hoy). Coinciden el bloque verde
  7615-7660, las mas largas (7625-7645), el bloque rojo 7555-7605 y sus mas
  largas (7600-7605 y 7555-7565 a la apertura; 7590-7605 despues). Igual que
  el NQ con QQQ medido el 11-09 ([[referencia-formulas-nq-medidas]]).
- **Panel "SPY 0DTE"**: mismo calculo con SPY y strike x (ES/SPY = 10,021).
  Un strike de SPY = 10 pts de ES: 762 -> 7636, 758 -> 7596.
- **Zero gamma (linea amarilla)**: cambio de signo del perfil por strike
  interpolado: 7609,7 calculado contra ~7610 en la captura de 09:36. El cruce
  repreciado daba 7614,4: no es ese.
- **Panel "NDX 0DTE"**: el perfil izquierdo casi no se ve porque el volumen
  0DTE de NDX es minusculo (barras de 0,08 B contra 0,46 B de QQQ). Correcto.
- **Perfil derecho (purpura/cyan)**: sigue SIN formula. La convexidad por
  volumen a +1 % en 0DTE es solo -signo(GEX) (el paso se pasa de largo el
  strike): no reproduce la alternancia de colores. Abierto.

## Auditoria interna del indicador (misma rueda)

Rehecho en Python desde `viva-ES-2026-09-14.jsonl` (15:51:38 UTC) con las
mismas formulas: netVol +8,35 B contra +8,05 B del AUDIT 30 s antes, netOi
-9,54 contra -9,51, zero por volumen 7624,6 contra 7624,9, zero por OI 7666,5
contra 7666,8, major+ 7635, major- 7600, dominantes 7635 (4690 M, identico) y
7600 (-5108 contra -5096). **Coincide.** Usa `vol_hoy` (con `vol_cinta` el zero
se va a 7615: no es ese).

**Ojo con el multiplicador:** `GammaHoyNucleo.MULT_INDICE = 100` se aplica
tambien al libro de Rithmic de ES, cuyo multiplicador real es 50. El titular
(netVol/netOi) del libro de ES esta inflado 2x; **ningun nivel se mueve**
(constante pareja). Ya paso en GammaVivo ([[gex-formula-auditada]]).

## Son dos libros y por eso no coinciden con la referencia

Mismo minuto (11:36 ET): ATAS (libro Rithmic ES por volumen) zero 7622,
major+ 7635, major- 7600, dominantes 7635/7600; SPX 0DTE (lo que dibuja la
referencia) zero 7607,8, major+ 7622, major- 7597, dominantes 7622/7627.
A la apertura (09:36 ET) el zero de ATAS estaba en 7627 y el de la referencia
en 7610: 17 puntos. No es error de cuenta de ninguno de los dos: es
[[dos-libros-distintos]]. Si el operador quiere ver lo mismo que la
referencia en MES, la fuente es SPX 0DTE por volumen + base medida (ya esta
todo en el archivo de CBOE, con 15 min de retraso).

**Why:** el operador pidio auditar "todo, varias veces, contra fuentes externas
e internas" con capturas de hoy; lo que faltaba era la prueba de ES (NQ ya
estaba medido).

**How to apply:** el script `perfil_ref.py` (scratchpad de la sesion; se rehace
desde `estado_nube.perfil_k` + cambio de signo interpolado) sirve para cualquier
captura: cadena de la nube en HH:MM+4h+15, viva de Rithmic en HH:MM+4h para el
futuro. Ver [[anatomia-referencia]], [[nube-roll-yahoo-2026-09-14]].
