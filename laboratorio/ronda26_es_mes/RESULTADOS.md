# Ronda 26 — ES contra MES sobre la misma pierna — RESULTADOS (27-11-2026, 14:22-15:40 AR)

Pre-registro: `PREREGISTRO.md` (cierre 14:22:15 AR, SHA-256 en `PREREGISTRO.cierre.txt`; 5 enmiendas DESCRIPTIVAS, todas escritas antes
de la tanda 1). Una sola corrida por tanda; la tanda 1 corrió a las 15:16 AR, después del cierre de hoy. Los hashes de los scripts de
análisis (`r26_comun`, `r26_01`, `r26_02`, `r26_03`) se verificaron a las 15:26 iguales a los del cierre. Salidas: `r26_0x_*salida_tanda0.txt`,
`..._tanda1.txt`, `..._hoy.txt`; datos intermedios en `PythiaGex\datos\ronda26` (9,4 MB). No se tocó ATAS (el operador operaba): la
"comprobación en pantalla" de esta ronda es el recálculo desde la cinta cruda con código aparte (`r26_04`).

## (a) Resumen en criollo (para contarle al operador)

Imaginá dos ventanillas del mismo banco que venden el mismo dólar al mismo precio: la grande (ES) y la chica (MES). Un robot corre entre
las dos y las deja iguales al tick (medido: la mitad de los segundos dan exactamente el mismo precio y 95 de cada 100 a un tick o menos).
El "color" de La Ola cuenta quién llega apurado, el que compra o vende "a lo que hay", en cada ventanilla.
En la chica, los apurados compran cuando ya subió (gente o robots que copian a la grande: la cinta no dice cuál): su apuro llega uno o dos
segundos DESPUÉS de que se movió el precio de la grande. Por eso el MES dice "con compradores" en 90 a 96 de cada 100 minutos: casi
siempre es eco del movimiento, no la causa.
En la grande, cada tanto aparecen vendedores medianos (órdenes de 10 a 49 contratos, y algunas de 50 o más) que venden mientras sube. Ahí
el ES dice "sin agresores" y el MES sigue diciendo "con": pasa en 1 de cada 6 minutos de la ola y en 1 de cada 5 de la marea.
¿Quién sube el precio entonces? En la muestra grande (14 ruedas) y hoy, sobre todo los que ESPERAN: las órdenes limitadas que se corren
hacia arriba entre operación y operación hicieron unas tres cuartas partes del recorrido; los apurados quedaron empatados en cantidad de
órdenes. (En la muestra chica de 3 ruedas ese reparto no se vio: lo cuento como visto dos veces de tres.)
¿Sirve para adivinar si la pierna sigue? En 17 ruedas, no: con el mismo retroceso ya hecho, las piernas "ES sin / MES con" siguieron igual
que las demás (diferencia −0,1 puntos de 100, con un margen de ±6,6). Lo que manda es cuánto volvió ya el precio: con menos de un cuarto de
ola de vuelta sigue 55 de cada 100; con más de tres cuartos, 14. Es casi la cuenta de una moneda.
Hoy fue un día en que la divergencia coincidió con que la pierna siguiera (66 contra 26 de cada 100, en 9 piernas), pero en las otras 17
ruedas eso no se repitió. Y la diferencia de X que tenían los paneles (10,00 contra 10,25) no la explica: con la misma X el color discrepa
igual. "ES sin, MES con" describe quién pelea en cada ventanilla; no dice adónde va el precio. Nada de esto es una regla de entrada.

## (b) Los números

### Datos
- 17 pares de diseño, **los 17 alinean** (mediana |ES − MES| 0,00 y p95 0,25 en 17.700-23.400 segundos por rueda; el máximo puntual 4,75 el
  25-11). Ningún par quedó afuera: en septiembre, roll incluido, las dos cintas de cada día estaban en el mismo vencimiento. Tanda 0 =
  13-11 (mixta, X 6,75), 19-11
  (tendencia, 8,50), 23-11 (rango, 9,75). Tanda 1 = 12, 16, 17, 18, 20, 24, 25-11 (X 6,25-9,75) y 08, 09, 11, 14, 15, 16, 17-09 (X 4,00, el
  piso: rangos previos de ~45 pts). Control = hoy hasta las 11:48 NY (X 10,25, mediana 126,625, igual que el panel).
- Pivotes de ola y marea idénticos a `test_ola.Maquina` en las 36 cintas; chequeo con referencia independiente sobre las cintas de hoy y
  reproducción exacta de los números del pedido (−2.970; 1-9 +2.355; 10-19 −4.234; 20-49 −1.058; 50+ −33; MES +6.441; desde 15:47 UTC ES
  −4.537 y MES −122 eq.; cruce a negativo 16:10:49 UTC; correlación 0,61). Comprobación desde el crudo con código aparte (`r26_04`): 19-11,
  23-11, 25-11 y 09-09, **2.312 comparaciones, 0 diferencias**.
- Datos raros de las cintas: 16-11 hueco de 42 s en la rueda **en las dos cintas a la vez** (corte de la fuente, no de una cinta);
  duplicados exactos 0-151 por cinta (se procesan como La Ola, con duplicados); ninguna fila fuera de orden; la punta de después de la orden
  sale cruzada o trabada el 1,7 % (ES) y el 3,8 % (MES) de las veces.
- Con la misma X, los zigzags de ES y MES van en direcciones distintas solo el 2,3-2,5 % de los minutos (ola) y 0,1-1,2 % (marea).

### P1 — la mecánica (descriptiva)
| | Tanda 0 (3 ruedas) | Tanda 1 (14) | Hoy |
|---|---|---|---|
| Ola: minutos con color distinto | 18,9 % (SC 16,7, CS 2,2) | 18,0 % (SC 15,8, CS 2,2) | 33,3 % (SC 31,9) |
| Marea: minutos con color distinto | 23,3 % (SC 21,1, CS 2,1) | 23,0 % (SC 19,3, CS 3,6) | 30,4 % (todo SC) |
| Ola: ES con / MES con (% de minutos) | 81,7 / 96,2 | 81,5 / 95,1 | 66,7 / 97,1 |
| Marea: ES con / MES con | 70,6 / 89,6 | 75,3 / 91,0 | 69,6 / 100 |
| Piernas de ola emparejadas; al final CC / SC / CS / SS | 373: 88,5 / 9,4 / 1,9 / 0,3 | 1.291: 89,1 / 8,6 / 1,3 / 1,0 | 28: 92,9 / 7,1 |
| Ola: piernas con delta a favor ES / MES | 90,3 / 97,9 % | 90,4 / 97,7 % | 92,9 / 100 % |
- Por hora NY (ola, tanda 1): 13,3 % de discordancia de 9:30 a 10, 15-19 % de 10 a 14, 23,0 % de 14 a 15 y 20,0 % de 15 a 16 (tanda 0:
  mínimo 12-13 con 8,4 %, máximo 15-16 con 28,8 %). Marea tanda 1: 6,4 % de 11 a 12 y 31-34 % de 13 a 16. Hoy: 58 % (ola) y 77 % (marea) de
  11 a 12 NY. La tarde discrepa más en la tanda 1; el patrón por hora no es estable entre tandas.
- **Quién hace la diferencia** (piernas SC contra CC, delta con el signo de la pierna, por pierna):
  - Ola, tanda 0: ES 10-19 −344, 20-49 −148, 50+ −220; ES 1-9 −114 en total; MES entre −6 y +2 eq. ES por tramo.
  - Ola, tanda 1: ES 10-19 −242, 20-49 **−411**, 50+ −272; ES 1-9 −33; MES entre −11 y −1 eq. ES.
  - Marea, tanda 0: 10-19 −1.973, 20-49 −1.405, 50+ −1.945. Tanda 1: 10-19 −2.271, 20-49 −3.002, 50+ −989 (y los chicos +485).
  - Hoy (2 piernas SC de ola): 10-19 −717, 20-49 −509.
  - Por el criterio pre-registrado (el MISMO tramo como el más negativo en las dos tandas) **no se repite**: fue 10-19 en la tanda 0 y
    20-49 en la 1. Agrupado (post hoc) el ES de **10-49 contratos** se da vuelta en las dos tandas y hoy (ola −492 / −653 / −1.226; marea
    −3.378 / −5.273), y el MES y el ES de 1-9 casi no cambian.
- Prima del MES (punta media MES − ES al cierre, con el signo de la pierna): mediana 0,00 ticks en todas las clases de las dos tandas
  (medias entre −0,11 y +0,04; las más alejadas, en clases de 22-23 minutos). No hay un precio del MES "adelantado": la divergencia es de
  flujo, no de precio. H-ARB queda sin probar.
- Correlación minuto a minuto del delta ES contra MES/10 por rueda: 0,40-0,79 (hoy 0,53).

### Descriptivos de la punta (Enmiendas 2 a 5)
- **Enmienda 4, contratos contra órdenes** (pre-registrada para la tanda 1): en las piernas SC del ES, órdenes a favor / en contra 1,01 (tanda
  0) y 1,00 (tanda 1: 1.668 contra 1.664 por pierna de ola) con contratos 0,98 y 0,97; en las CC 1,05 / 1,11-1,13. El criterio fijado
  (> 1 en órdenes y < 1 en contratos en las dos tandas) se cumple, pero por un pelo: en cantidad de órdenes los apurados del ES quedan
  EMPATADOS, en contratos pierden. Impacto por orden (ES 13-11): 1 contrato 0,39 ticks, 2-9 0,71, 10-49 0,77, 50+ 1,05-1,13: cóncavo.
  La pierna es un residuo chico de un flujo enorme: ~3.300 órdenes y ~14.000 contratos por pierna SC de ola para ~12 pts.
- **Enmienda 3, quién movió el precio**: tanda 1 (pre-registrada), ola: CC agresor +6,26 / pasivo +5,95 pts (51 % agresor); **SC agresor
  +2,53 / pasivo +8,73 (22 %)**; marea CC 67 % agresor, SC 33 %. Hoy SC −12,5 / +38,1. Tanda 0 (exploratoria): 56 % en las dos clases (ola).
  Visto 2 de 3 veces; la partición está contaminada por la apertura y el cierre del spread (control −0,25 pts en la ola, −2,4 a −4,3 en la
  marea). Absorción real (definición de `gatillo\base.py`): 2-3 % del volumen agresor en ES y ~1 % en MES, igual en todas las clases.
- **Enmienda 5, quién se mueve primero** (predicciones fijadas antes de la tanda 1): (a) el delta del MES acompaña al precio del minuto más
  que el del ES en **13 de 14** ruedas (mediana 0,76 contra 0,56; tanda 0 0,83 contra 0,55; hoy 0,81 contra 0,52). (b) El delta del MES en
  t+1 s acompaña al precio del ES en t más que el delta del ES en **14 de 14** ruedas (mediana +0,157 contra +0,052; +2 s: +0,097 contra
  +0,025; hoy +0,201 contra +0,040). Los precios van juntos en el mismo segundo (0,84-0,94) y el ES apenas adelante (+0,034 contra +0,016).
  Por tramo, en el ES lo que acompaña al precio del minuto son las órdenes de 10-49 (0,41) y 50+ (0,35), no las de 1-9 (0,15).

### P2 — la información (Bonferroni 22 pruebas: |t| ≥ 3,05)
Ola, tanda 1 (14 ruedas, 5.334 minutos, 1.215 piernas, 0 censurados). Tasa de SIGUE por caja de retroceso (r/X): base 55,3 / 41,7 / 26,0 /
13,8 % (paseo al azar 54,0 / 39,7 / 24,5 / 11,5 %).
| Clase | Piernas | Tasa | D (clase − base de su caja) | IC 95 % (piernas) | t | Tanda 0 (D, t) | Etiqueta |
|---|---|---|---|---|---|---|---|
| ES con / MES con | 1.066 | 44,0 % | −0,46 pp | −1,96; +0,91 | −0,54 | −0,68; −0,68 | SIN INFORMACIÓN en esta forma |
| **ES sin / MES con** | **269** | 41,0 % | **−0,08 pp** | −6,70; +6,60 | −0,02 | +0,97; +0,23 | **SIN INFORMACIÓN en esta forma** |
| ES con / MES sin | 66 | 44,9 % | +4,30 pp | −12,66; +20,05 | +0,51 | +11,9 (16 piernas) | INCONCLUSO |
| ES sin / MES sin | 53 | 47,9 % | +10,45 pp | −4,67; +26,09 | +1,33 | +7,7 (11 piernas) | INCONCLUSO |
Marea, tanda 1 (5.392 minutos, 4.317 resueltos, 1.075 censurados a las 17:00 NY, 94 piernas): base 51,1 / 32,5 / 20,9 / 8,0 %. CC 85
piernas D +1,47 pp (−2,71; +5,47), t +0,72: SIN INFORMACIÓN en esta forma. **SC 33 piernas D −1,01 pp (−18,12; +14,56), t −0,12:
INCONCLUSO** (en la tanda 0 tuvo 14 piernas, sin muestra). CS y SS: sin muestra (15 y 14 piernas).
Robustez (base por caja y rueda): mismo signo salvo las dos SC, que cambian de signo pegadas al 0 (ola +0,17; marea +2,04). Desenlace
en el zigzag del MES: igual (acuerdo del desenlace ES/MES 97,6 % en la ola y 99,4 % en la marea).

### P3 — combinaciones (S = diferencia de "sigue" con y sin el rasgo, dentro de la caja; pp; t de la tanda 1)
| Rasgo | Ola tanda 0 | Ola tanda 1 (t) | Etiqueta ola | Marea tanda 0 | Marea tanda 1 (t) | Etiqueta marea |
|---|---|---|---|---|---|---|
| R0 color del ES | −1,92 | −1,81 (−0,38) | SIN INFORMACIÓN | −16,1* | +4,54 (+0,48) | INCONCLUSO |
| V1 ES + MES/10 a favor | −1,43 | −1,55 (−0,31) | SIN INFORMACIÓN | −13,5* | +4,36 (+0,45) | INCONCLUSO |
| V2 ES 10-49 a favor | +0,94 | −0,82 (−0,23) | SIN INFORMACIÓN | −15,0* | +10,33 (+1,13) | INCONCLUSO |
| V3 ES 1-9 a favor | −0,94 | +4,88 (+1,82) | SIN INFORMACIÓN | −19,0* | −15,12 (−1,42) | INCONCLUSO |
| V4 color del MES | −10,59 | −8,14 (−1,21) | INCONCLUSO | −53,1* | +11,93 (+1,21) | INCONCLUSO |
| V5 brecha MES/10 − ES | +2,06 | +1,89 (+0,44) | INCONCLUSO | +16,7* | −5,68 (−0,63) | INCONCLUSO |
| V6 chicos a favor + medianos en contra | −1,00 | +1,54 (+0,47) | SIN INFORMACIÓN | −18,9* | −13,53 (−1,40) | INCONCLUSO |
(*) menos de 20 piernas de un lado en la tanda 0: no se juzga. **Ninguno PASA ni es PISTA por la regla pre-registrada**; ninguno separa
"sigue" de "gira" mejor que el color del ES (|S| − |S(R0)| con el 0 adentro en todos), y el propio color del ES tampoco separa.
El placebo de minutos sale p 0,0005 en varias filas de la marea con t de 1: confirma que barajar minutos ignora que van en bloque por pierna
(lo advertido en el pre-registro); la decisión se apoyó en el t por bloques.

### Control de hoy (aparte; 138 minutos, 28 piernas de ola, 1 de marea abierta)
- Parámetro contra mercado (`r26_08`): MES con 10,00 = 49 giros de ola en la jornada (33 en la rueda) contra 45 (29) con 10,25; dirección
  igual al ES el 97,8 % de los minutos con 10,00 y el 100 % con 10,25; **mismo color el 66,7 % con las dos X** (marea 69,6 % con las dos, el
  "70 %" del pedido). La X distinta agregó giros; la divergencia de color es toda de mercado.
- Ola: SC 41 minutos en 9 piernas siguió 65,9 % contra 25,6 % de CC (D +29,8 pp, t +3,05 con EE de piernas, una rueda, menos de 20
  piernas). Hoy la divergencia acompañó a una pierna que siguió; en las 17 ruedas de diseño eso no se repite (tanda 1 D −0,08 pp).

## (c) Qué va al baúl y por qué
Anotado al final de `PythiaGex-privado\BAUL_DE_PISTAS.md`, sección "## RONDA 26 - ES CONTRA MES EN LA MISMA PIERNA":
- **EXPLICATIVA (se repite, 2 tandas + hoy):** la discordancia es casi toda "ES sin / MES con" (16-21 % de los minutos), la hace el ES de
  10-49 contratos (no un tramo fijo), y el MES "con" es eco: su agresión llega 1-2 s detrás del precio (predicciones a y b cumplidas en
  13/14 y 14/14 ruedas). Va porque explica lo que el operador ve en el panel.
- **EXPLICATIVA vista 2 de 3 veces:** en esas piernas el precio lo corre sobre todo el libro pasivo (78 % en la tanda 1, también hoy; no en
  la tanda 0) y los agresores del ES quedan empatados en cantidad de órdenes. Para fusión con la #37 (piernas pasivas).
- **SIN INFORMACIÓN en esta forma (2 muestras):** el color ES contra MES como pronóstico de que la pierna de ola siga (CC y SC, y los rasgos
  R0, V1, V2, V3, V6 de la ola). No es "descartado": es "no cambia la chance de seguir en la ola con este armado"; la marea queda
  INCONCLUSA (33 piernas SC).
- **PISTA DÉBIL (no aprueba la regla; mismo signo en las dos tandas, t −1,0 a −1,8):** en la marea, las piernas empujadas solo por los chicos
  del ES (V3 −19,0 / −15,1 pp; V6, el patrón de hoy, −18,9 / −13,5 pp) siguieron MENOS; en la ola, cuando el MES queda "sin" (5 % de los
  minutos) la pierna siguió MÁS (V4 −10,6 / −8,1 pp para "MES con"). Hoy (1 pierna de marea) fue al revés: anécdota.
- **Lo que no se afirma:** H-ARB (los que le venden MES al minorista y compran ES pasivo) queda sin probar: la prima del MES es 0 y la
  absorción real es igual en todas las clases; lo compatible es que haya pasivos absorbiendo en las dos ventanillas, no quiénes son.
