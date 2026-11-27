---
name: tendencia-investigacion-2910
description: "29-10: 'no detecto la tendencia, creo que va a revertir': 3 investigaciones. Ningun tunel anticipa (46-50 %); SuperTrend 1m estaba EN CONTRA en el 70 % de sus promedios en contra (azar 54 %); la regla 'no agregar contra el tunel' solo paga en los 3 dias de liquidacion; lo que tiene evidencia es lo mecanico (stop automatico, tope de tamaño)"
metadata:
  node_type: memory
  type: project
  originSessionId: 404dab2f-b5f6-48fa-ba08-3ab764cbbdd8
  modified: 2026-10-29T03:56:17.407Z
---

Pedido 29-10 ~00:15 AR: "me cuesta identificar cuando esta en tendencia... me bloqueo, pienso que va a cambiar y no; algo tipo
directriz o tuneles que marquen la tendencia, o una idea mejor; investigacion profunda".

**Medido con sus datos** (`PythiaGex/laboratorio/tendencia/`, INFORME.md, PREREGISTRO.md con Enmienda 1; verificado c_resumen.csv):
- 7 detectores fijos (SuperTrend 10/3, tunel EMA20±ATR, regresion 20 R2>=0,5, Kaufman ER(10)>=0,4, ADX>=25, estructura pivotes 3, VWAP+pendiente)
  en MES 5m 274 ses, MES 1m 45, MNQ 5m 47, MNQ 1m 35: NINGUNO anticipa 15-30 min (46,4-50,1 %; contra azar -2,2 a +0,9 pp).
- Usabilidad: SuperTrend gana (1m: 35-37 cambios por sesion, tramos de 28-30 min; los otros 52-250). Llega tarde: MES 20+ pts a los
  6,5 min con 13 de 20 hechos; MNQ 80+ a los 8 min con 59 de 80.
- Su error: en los 593 agregados en contra, SuperTrend 1m marcaba en contra 70,2 % (azar 54,1 %, p 0,0025); tunel EMA 1m 65,9 vs 42,1.
  Regla "no agregar contra el tunel": +2.012 a +6.615 USD en las 14 variantes, t 1,21-2,13, NINGUNA pasa el pre-registro (mejor ER(10)
  5m +4.932, t 1,68, p placebo 0,0045 vs 0,0036 pedido). SIN los 21 ciclos de liquidacion todas quedan -1.404..+366: la plata sale de 3 dias
  (08-13 MES, 09-15 y 09-22 MNQ). "No promediar en contra" a secas (ronda 8) da +6.521 (t 1,99): ningun tunel la supera.
**Investigacion web** (informes en scratchpad de la sesion 404dab2f: informe.md, informe_borrador.md):
- Minutos = ruido (Safari y Schmidhuber 2025, borrador); la unica evidencia revisada que ata "modo tendencia" a algo medible es gamma
  negativa de los dealers (Baltussen et al. 2021, efecto chico). En ruido simulado: ADX>25 38 % del tiempo, R2>0,5 45 %, pivotes 60 %,
  SuperTrend 12,6 vueltas por rueda; ER de 30 velas > 0,55 solo 1,3 %. ATAS trae "Options X-Ray: Market State" (ER 30 min + gamma, solo
  ES/MES/SPY): sin verificar si esta en su 8.0.15.302.
- Conducta: falacia del jugador / efecto disposicion / "salir hecho". Con evidencia: stop AUTOMATICO (Weber y Camerer 1998; el aviso solo
  no sirvio: Fischbacher et al. 2017), tope de tamaño impuesto (Heimer y Simsek 2019), esconder el P/L abierto (Frydman y Rangel 2014, -25 %).
  Ariely y Wertenbroch 2002 RETRACTADO (02-09-2026): no citarlo.
**How to apply:** el tunel solo como espejo ("describe, no anticipa"), nunca señal; lo que ataca su perdida es lo mecanico. Ver
[[estrategia-ronda-8-edge-personal]], [[estrategia-ronda-9-patron]], [[estadistica-visible]].
