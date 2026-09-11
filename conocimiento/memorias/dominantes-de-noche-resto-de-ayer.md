---
name: dominantes-de-noche-resto-de-ayer
description: "Por que las dominantes de MNQ se van 300-500 pts del precio desde las 17:00 local: no es error de cuenta, es que a las 16:00 NY vence el 0DTE que traia el 82 % del volumen y el libro 'por volumen' queda con el resto del 1DTE de ayer; la caida a OI 'de noche' que el codigo promete nunca se dispara."
metadata:
  type: project
---

Auditado el 2026-09-11 (00:20-00:50 local) con la cadena del indicador, el
archivo local minuto a minuto, el log AUDIT de tres dias, CBOE cruda bajada
aparte, Yahoo y la pantalla de ATAS. El recalculo a mano reproduce al millon
lo que dibuja ATAS (+120M en 29.440, -59M en 28.800; base 24,7 de la rueda,
NQU6 29.067). La aritmetica esta bien; el dato de entrada es el problema.

- A las 16:00 NY vence el 0DTE. En NDX ese vencimiento trae el grueso del
  volumen del dia (10-09: 69.756 de 85.074 contratos, 82 %). El indicador
  lo saca del perfil (correcto: gamma cero) y CBOE tambien lo borra de la
  cadena que servimos; la cruda de CBOE lo sigue listando (trampa conocida).
- Lo que queda como "volumen de hoy" es el 1DTE de ayer: 5.973 contratos
  desparramados (29.440 con 254 calls, 28.800 con 79 puts...). La dominante
  "por volumen" (barra mas grande a +-2 % del precio, una por lado) cae en
  esos restos. Tamano de barra: ~150 M de noche contra 1.300-2.000 M en la
  rueda. Distancia mediana en la rueda NY: +55/-38 (09-09), +92/-46 (10-09);
  noche del 10 al 11: +390/-252.
- ES no lo sufre porque el 1DTE de SPX ya trae 464.442 contratos, apilados
  en 7600/7550 cerca del cierre. Mismo mecanismo, distinto resultado.
- Donde caiga la banda de noche depende de a que strike compraron el 1DTE
  durante el dia: si el precio se movio mucho (10-09 cayo 320 pts) queda
  lejos. No es un nivel: es un rastro de ayer.
- El nucleo dice "si todavia no hay volumen (noche), las del OI, y se dice",
  pero la condicion es candDom.Count == 0 y CBOE nunca deja el volumen en
  cero hasta la manana siguiente: la caida a OI no ocurre nunca.
- Lo que dibujaria cada opcion ahora (fut 29.067): VOL Hoy 29.465/28.825;
  OI del mismo vencimiento (11-09) 29.525/28.825 (99M/-56M, tambien lejos);
  OI a 14 dias 29.300/29.025 (1.271M/-299M); "como los tableros", con el
  0DTE vencido adentro: 29.225/29.125 (mentira: contratos muertos).
- Ademas: la cadena viva de Rithmic estuvo en 0 series desde ATAS .399 (ATAS la apago a
  proposito); recuperada el 11-09 con PuenteRithmic.cs, ver [[cadena-es-en-vivo-rithmic]].

**Why:** el operador vio las bandas a 500/200 pts y pidio auditar con todo.
Sin esto se habria "corregido" la base o el multiplicador, que estan bien.

**How to apply:** de noche, decir que el libro por volumen es el resto de
ayer (cadena.ultimo_trade horas atras) y no venderlo como dominante; el
criterio robusto para detectarlo es la edad del ultimo trade de opciones,
no la hora del reloj. Que libro mostrar de noche es decision del operador
(OI Hoy tambien queda lejos; OI 14 d es otro objeto). Ver
[[vencimientos-0dte-auditados]], [[auditoria-en-vivo-2026-09-09]],
[[nq-libro-propio-pesa]], [[cadena-es-en-vivo-rithmic]].

**1.8i (2026-09-11 02:00), lo que pidio mirando la pantalla:** (1) los guiones de dominante NUEVOS en
otro color solo mientras son nuevos: ahora nacen LILA fluo y se funden al amarillo en 10 min
(EnfasisNuevasMin; antes 3 min y blanco casi igual al amarillo, por eso "eran solo una raya"). (2) La
banda SI seguia a la dominante actual: lo que veia "desfasado" era que con el libro vivo de Rithmic de
noche la dominante de abajo saltaba entre 29.049 (-84 M) y 28.800 (-91 M) por 7 M, y la banda se iba a
320 pts. Arreglo: empate tecnico (EmpateDominantesPct = 20 %): entre barras comparables gana la mas
cercana al precio. Medido tras reiniciar: D2 paso de 28.825 (-311) a 29.023 (-114) con CBOE; en ES
Rithmic doms 7613/7600 con el futuro en 7613. (3) Los guiones viejos lejanos se dejan: son la historia
del dia; bandas y rayas D1/D2 solo para las dominantes actuales.
