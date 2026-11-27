---
name: corriente-indicador
description: "29-10: 'PythiaGex - Corriente' 1.0c (atas/Corriente, DLL aparte) en MES 1m: tunel SuperTrend 10x3 + flecha (llena si coincide con VWAP; titila con posicion en contra) + cinta; SIN TEXTO por pedido; describe, no anticipa"
metadata:
  node_type: memory
  type: project
  originSessionId: 404dab2f-b5f6-48fa-ba08-3ab764cbbdd8
  modified: 2026-10-29T05:18:32.627Z
---

Pedido 29-10 tras [[tendencia-investigacion-2910]]: maquetas sin carteles ("los profesionales son minimalistas, una flecha o un dibujo").
Maquetas sobre sus velas reales del 13-08 (dia de liquidacion, vista a las 09:56 con sus ventas marcadas): A tunel, B flecha+cinta, C fondo
de regimen (ER 30 no se prendio hasta las 10:01: inutil ese dia), D VWAP y giros. Eligio **A + B en un indicador aparte**.

**Corriente 1.0c** (`atas/Corriente/Corriente.cs`, ajustes prefijo Cor): tunel = franja cierre<->SuperTrend (ATR Wilder 10 x 3) 16 % +
linea; flecha a la derecha de la vela en curso (llena si el lado del VWAP de sesion coincide, hueca si no); con posicion en contra
(TradingManager.Position.Volume signo != direccion) tam 16 y titila (timer 500 ms), si no tam 12; cinta 5 px a 18 px del borde de abajo
(`CorCintaSobreBorde`, arriba de la barra de Lots), fuerte si coincide con el VWAP. DrawAbovePrice = true (forzado tambien en OnInitialize:
ATAS lo guarda en el .ws): debajo, la burbuja de Adaptive Big Trades tapaba la flecha. Registro `%APPDATA%\ATAS\PythiaGex\corriente\`.
Verificado 02:15-02:18: 14/14 velas (ATR, bandas, direccion) recalculadas por fuera = log; flecha sin pisar la etiqueta de la 5.0; posicion
-2 detectada "EN CONTRA". Se instalo CON posicion abierta porque el operador dijo "hacelo" (SL/TP en servidor). VWAP no verificado por fuera.
**Calibracion 29-10 ~12:15 AR** ("avisa tarde, mas en dias volatiles; sin sumar ruido, hasta el limite"): laboratorio/tendencia/
PREREGISTRO_CALIBRACION.md + CALIBRACION_INFORME.md, 80 candidatos, MES 1m DISEÑO 45 ses + EXAMEN 17 (10-06..10-28, cache exportada
29-10). Pre-registrado: NO PASA (ST(60;3,5) 15,5 vs 13,5 pts). Todas las familias caen en la MISMA curva retraso/ruido. Post-hoc
consistente: ATR 60 x 3 = mismo ruido, antes en 44/12 y 23/8 movimientos (~1 pt promedio); 60 x 2,5 = 2,7 pts antes por +3 cambios
y +2 vueltas falsas por rueda. Se cambia en vivo (ajuste "ATR (velas)"), sin reinstalar.
**Ronda 2 (29-10 ~11:45 AR, "llevalo a los limites, sin excusas")**: PREREGISTRO_CALIBRACION_2.md, 114 de 3 mecanismos + examen en
MNQ 1m. Solo la VUELTA RAPIDA rompe la curva fuera de muestra (44/54 EXAMEN MES, 54/54 MNQ). Elegida: linea ATR 60 x 4 + vuelta si
retrocede 2,5 ATR desde el extremo en <= 3 velas: mismo ruido en la sesion; MES dis 57/33 p 0,007, MNQ 115/81 p 0,009, MES examen 24/16
p 0,13 (NO alcanzo) -> pre-registro NO PASA; se instala igual como **1.1 SIN VALIDAR del todo** (Enmienda 1). En la rueda +1 cambio/dia.
Ajustes NUEVOS CorLineaAtr/CorLineaMult/CorRapida/CorRapidaAtr/CorRapidaVelas (los viejos CorAtr/CorMult quedan en el .ws).
Registro: "vueltas (3 h)" con causa L/R; volcado-*.csv al cargar; paridad: laboratorio/tendencia/paridad_corriente.py (lab: 0/84.364).
**1.2 (29-10 12:59 AR, "sigue avisando tarde; limites reales de prediccion")**: la 1.1 llego 22 pts tarde en una suba gradual.
PREREGISTRO_PREDICCION.md: P(sigue A mas antes de volver al extremo | retroceso r) = azar puro r/(r+A) en MES y MNQ (126 mil casos;
auditor independiente: la brecha en MES era por contar sin-desenlace como 0); 20 rasgos en vivo con logistica y arboles NO le ganan
a r/(r+A). => regla optima = darse vuelta con retroceso de X pts desde el extremo. X=10 MES: examen 24/10 p 0,012, p90 22->16,4,
mismo ruido en la rueda; MNQ no paso (eligio 70). Modo "Retroceso en puntos" (CorModo, CorRetroceso=10) por defecto; linea = extremo
-/+ X. Costo: X fijo serrucha mas en dias volatiles (29-10: 8 cambios en la rueda vs 4). Paridad volcado 27.224 velas exacta.
**1.3 (29-10 14:14 AR, "de 7760 bajo a 7748, subio a 7755 y seguia bajista; para scalping malisimo")**: ERROR DE FONDO de
las rondas 1-3: calibre para tendencias de 20 pts; el opera tramos de 5-7 (TP 2,5). Vara correcta (PREREGISTRO_SCALPING.md):
ACIERTO del tramo actual contra el zigzag de 6 pts mirado hacia atras, en la rueda. Elegido: "tramo en vivo 4 pts" (se da vuelta
al TOCAR 4 pts contra el extremo, sin esperar el cierre). Examen: 76,0 % vs 1.2 54,9 % y 1.0c 57,4 %; 16/16 sesiones. Su regla
del 50 % rindio menos (60-68 %). Costo: ~80 cambios por rueda. Tunel/flecha = tramo; cinta = corriente grande (10 pts al cierre);
flecha llena = tramo con la grande. Ajustes: CorModo13 (nombre nuevo: el .ws guardaba CorModo), CorTramo 4, CorTramoVivo, CorRetroceso 10.
Auditor independiente: numeros exactos; ADVIERTE que la vara no cobra los cambios (describe el tramo, no mide si se puede
operar) y es injusta con los lentos; color de vela = 65-67 %, tramo real con 3 velas de atraso = 76-79 %.
El 29-10 13:52 paso su MES a 30 s; el reinicio de las 14:10 (ATAS no cerro en 60 s, forzado) lo dejo en M1: avisarle.
Instalar: herramientas/instalar_5_0.ps1 -SinDll -AntesDeLanzar herramientas/copiar_corriente.py (solo con el operador PLANO).
Pendiente: MNQ (su otro instrumento) y el tope de contratos en Risk Management (necesita su OK). Ver [[estadistica-visible]].
