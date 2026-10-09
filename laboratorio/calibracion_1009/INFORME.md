# INFORME FINAL — calibracion_1009: "la dominante en la punta de la mecha"

Escrito el 2026-10-09 a las 05:35 UTC (02:35 ART) por el agente de informe final del workflow. Es para el agente principal, que se
lo traduce al operador. Al operador hay que darle analogía primero, una idea por vez y nada de tablas: la única tabla de acá es para
el agente.

**Escépticos:** la lista de veredictos llegó **vacía**, así que ningún escéptico revisó estos resultados. Igual no cambia la
recomendación, porque nada pasó la vara y no hay ningún cambio que dependa de ellos. Si después un escéptico encuentra un error que
baje algún p ajustado por debajo de 0,05, esto se reabre.

**Marcas:**
- [MEDIDO]: corrido en este workflow, con archivo.
- [RECALCULADO]: lo rehice yo al escribir este informe (scripts en `informe/`).
- [LEÍDO]: viene de una fuente externa; digo quién la leyó y cómo.
- [SUPUESTO]: no está medido.

Las fechas de sesión van AAAA-MM-DD. La sesión D va de las 22:00 UTC del día anterior a las 21:00 UTC de D.

---

## 0. Respuesta corta

1. **Nada sobrevivió.** Ninguna fórmula de rayas hizo que el precio "girara en la raya" más seguido que la misma raya puesta al azar,
   en sesiones que no se usaron para elegirla. Entran acá las de la 2.0, los defaults de la 4.1 y 8 fórmulas nuevas sacadas de la
   literatura y de los proveedores.
   - Referencias (2.0 y 4.1) en PRUEBA: ganan 0 de 18 pruebas. El p más chico es 0,148 sin ajustar, que da 1,0 ajustado.
   - Nuevas: 0 de 8 pasan siquiera la vara de entrenamiento.
   - En promedio, las rayas de la 2.0 y la 4.1 quedan a 1 punto porcentual de su placebo: −1,0 pp en entrenamiento y +0,9 pp en
     PRUEBA.
2. **El falso rompimiento ya estaba contemplado, y no cambia nada.** El juez lo cuenta como acierto desde el pre-registro. Cerca de la
   mitad de los giros sostenidos pasan por un pinchazo, tanto en las rayas de gamma como en las puestas al azar. Ni siquiera el modo
   más tolerante separa a una fórmula del azar.
3. **La conversión correcta es la de la 4.1** (razón sincronizada). La de la 2.0 se corre de noche: [RECALCULADO] 27 pts de mediana
   por noche y 169 pts de p90 típico, a strike 750, en 19 noches. La noche del CASO la raya de la 2.0 cayó en la mecha y la de la 4.1
   no. Pero es una sola noche, y en las sesiones que deciden la conversión de la 2.0 nunca le ganó a la de la 4.1.
4. **Para la 4.1: no cambiar nada de lo que se dibuja por estos datos.** Ojo con la 4.1.3, que está escrita pero no instalada: prende
   por defecto tres series "como la 2.0". Ninguna tiene respaldo medido, y la capa NDX de la 2.0 fue la peor de toda la PRUEBA de noche.
5. **La muestra no alcanza para un efecto realista.** La literatura encuentra ventajas de 4 a 6 pp (Osler 2000). Con 6 sesiones de
   PRUEBA solo se ven ventajas de 15 a 24 pp.
   - Con una sola hipótesis pre-registrada, para ver 5 pp hacen falta unas 38-44 noches útiles: entre 48 y 55 sesiones desde el
     12-10, hasta fines de diciembre.
   - Con tres hipótesis, unas 55-63 noches útiles: fines de enero de 2027.

---

## 1. Qué dice la ciencia (en simple)

Analogía para el operador: el GEX es como el pronóstico "en invierno llueve más". Es cierto, y sirve para elegir la ropa: rango o
tendencia, mercado quieto o nervioso. Pero no dice en qué baldosa cae la gota. Lo que pide el operador, la raya exacta donde termina
la mecha, es la baldosa.

**1.1 Nadie midió lo que pide.** [LEÍDO; el agente de academia leyó 9 textos completos y 5 resúmenes] No encontré ningún estudio que
pruebe, contra un placebo, que el precio gira dentro del día en la dominante de GEX. La literatura mide dos cosas distintas: el pin
al cierre del vencimiento y el régimen.

**1.2 El pin existe, pero es chico, va al cierre del día de vencimiento y depende del tipo de opción.**
- Ni, Pearson y Poteshman (2005), *JFE* 78(1). El viernes de vencimiento, las acciones con opciones cierran más pegadas a los strikes,
  con un corrimiento de al menos 16,5 pb. https://ideas.repec.org/a/eee/jfinec/v78y2005i1p49-87.html (leí solo el abstract y las
  diapositivas).
- Golez y Jackwerth (2012), *JFE* 106(3).
  - El futuro del S&P es atraído al strike ATM solo el día en que vencen las opciones seriales sobre el futuro, que se entregan
    físicamente. En el índice SPX, que liquida en efectivo, no hay pin y hasta hay repulsión.
  - Tamaño: 21,3 % contra 15 % esperado en la versión JFE (±0,375; 136 vencimientos). El working paper de 2010 da 13,6 % contra 10 %
    (±0,25). Los agentes citaron versiones distintas.
  - La mitad del corrimiento ocurre en los últimos 30 min.
  - https://kops.uni-konstanz.de/entities/publication/0c68d50d-23f5-4771-bdbf-c345290a3452
- Elms (2026), SSRN 6564078. Desde 2016 no hay pin en SPY, y con mucho OI ATM los rangos salen ~16 % más ANCHOS. [LEÍDO solo por un
  resumen de terceros; el paper no tiene revisión] https://harbourfrontquant.substack.com/p/from-pinning-to-amplification-evidence

**1.3 El signo decide si el strike atrae o repele.** Avellaneda y Lipkin (2003), *Quantitative Finance* 3(6).
- El imán existe solo si el que cubre está neto LARGO de opciones. Si está corto, el strike empuja para afuera.
- Aun con imán, el precio oscila alrededor del strike y lo cruza.
- https://math.nyu.edu/inmemoriam/avellaneda/qf3601.pdf
- [MEDIDO, C13 contra C14] Separar las rayas "imán" (GEX por volumen positivo) de las "repelentes" (negativo) no cambió nada: empatan.

**1.4 Lo que sí está medido es el régimen, no el lugar.**
- Baltussen, Da, Lammers y Martens (2021), *JFE* 142. Si la gamma neta estimada de los dealers es negativa, la última media hora sigue
  al resto del día; si es positiva, no. https://academicweb.nd.edu/~zda/intramom.pdf. En este proyecto NO replicó en NQ ni en ES
  (memoria `gatillo-cientifico-2026-09-10.md:19-21`).
- Barbon, Beckmeyer, Buraschi y Moerke (2021), WP St. Gallen 2021/14. El desbalance de gamma relativo a la liquidez trae momentum o
  reversión al cierre: un desvío equivale a ±2,87 pb, más o menos 1 pt de NQ.
  https://www.alexandria.unisg.ch/server/api/core/bitstreams/5a99db31-0d37-4f86-9502-8cb0f3bff4fe/content
- Amaya, Garcia-Ares, Pearson y Vasquez (2025), WP alojado por Cboe. La gamma real de los market makers suele ser positiva y baja un
  poco la volatilidad. https://cdn.cboe.com/resources/education/research_publications/gammasqueezes.pdf. Dim, Eraker y Vilkov (2024,
  SSRN 4692190) y Adams et al. (2025, SSRN 5641974; leído solo por resumen de Quantpedia) van en la misma dirección.
- Xu (Cboe, 2023). La gamma neta de los market makers en el 0DTE de SPX es el 0,04-0,17 % del volumen diario del futuro. En un strike
  muy operado, el neto fue ~3 % del bruto.
  https://www.cboe.com/insights/posts/volatility-insights-evaluating-the-market-impact-of-spx-0-dte-options
- FlashAlpha (2026; SPY, 1.971 días, pre-registrado). El GEX anticipa la volatilidad del día siguiente, pero no aporta nada que no
  digan ya el VIX y la volatilidad implícita: la correlación pasa de −0,36 a −0,03. [LEÍDO; el proveedor vende el dato]
  https://flashalpha.com/articles/gex-dex-vex-chex-8-year-backtest-spy-vix-control

**1.5 Cuánto pesa la cobertura, en puntos de NQ.** [MEDIDO por el agente de modelos sobre la cinta de MNQ del 2026-10-07 y del
2026-10-08, con el SUPUESTO de que MNQ es el 15-40 % del flujo]
- Cada contrato agresivo de MNQ mueve ~0,011-0,014 pts en un minuto.
- Con la gamma neta realista que mide Cboe, la cobertura acorta un impulso de 15 pts en ~0,5 pt.
- No alcanza para clavar una mecha a ±2 pts de un strike, salvo quizás en los últimos ~30 min de un 0DTE grande con los dealers largos.
- Archivo: `modelos_matematicos/resultados_ordenes.json`.

**1.6 Soportes y resistencias en general: la vara es Osler.**
- Osler (2000), *FRBNY Economic Policy Review* 6(2), leído completo.
  - Usó niveles publicados por 6 firmas de divisas, con miles de toques.
  - El precio rebotó el 60,8 % de las veces en los niveles publicados, contra 56,2 % en 10.000 juegos de niveles inventados: +4,6 pp.
  - La "fuerza" que publicaban las firmas no sirvió, y que varias coincidieran tampoco sumó.
  - https://www.newyorkfed.org/medialibrary/media/research/epr/00v06n2/0007osle.pdf
- Osler (2003, *JF* 58(5); 2005, *JIMF* 24(2)). Las tomas de ganancia se amontonan en los números redondos y hacen rebotar; los stops
  se amontonan justo después y aceleran el cruce. [MEDIDO, `soportes_metodo/`] En MNQ el redondo no cambia la chance de que una mecha
  sea extremo (razón de 0,8 a 1,1). En este workflow tampoco: los controles C19-C21 no tienen ventaja (sección 3.7).
- Proveedores: SpotGamma, MenthorQ, GEXbot, Unusual Whales y Barchart. Ninguno publica una prueba contra placebo.
  - Sus estadísticas son las que da cualquier raya a 1,4-1,6 desvíos diarios del precio. Ejemplo: según SpotGamma (2019-2024), el
    máximo del día no pasó su call wall el 83 % de las ruedas.
  - [Cuenta del agente de proveedores en `metodologias_proveedores/`] https://spotgamma.com/option-wall-stats/

**1.7 Lo que ya había medido el proyecto** (memorias):
- El 03-09, gamma × OI perdió contra su placebo: 68,6 % contra 72,6 % (`laboratorio-formulas.md:30`).
- En NQ, las dominantes dieron 62,1 % contra 65,0 % del placebo, en 29 toques y 10 strikes (`es-vs-nq-respeto.md:19-20`).
- 11 rondas de búsqueda, sin ninguna regla que sobreviva fuera de muestra.

**Conclusión:** si el efecto existe, es de pocos puntos porcentuales, y para verlo hacen falta cientos o miles de toques. Ninguna
fuente respalda una raya que "toque todas las mechas".

---

## 2. Qué se probó

**Datos** [MEDIDO, `datos/`]
- Velas de 1 min de MNQ de 40 sesiones, del 2026-08-17 al 2026-10-09. La última quedó cortada a las 03:53 UTC.
- Las velas salen de 6 fuentes cruzadas, que coinciden en el 99,9-100 % de las velas.
- Libros: QQQ 21 sesiones, NDX 37 (del 08-19 al 09-04 son reconstrucción Databento, solo de rueda) y NQ Rithmic 28.
- Siete conversiones a NQ por minuto:
  - la exacta de la 4.1: base NDX idéntica y razón QQQ a 0,34 pts de mediana;
  - la de la 2.0: calza en el 79-100 % de los minutos, según el día y el gráfico.

**Juez** = el criterio del operador, pre-registrado (`arnes/juez_operador_v1.py`, sha `a20a6268…`; reglas en
`PREREGISTRO.md:75-87` y `juez_operador_v1.py:31-42, 114-117`).
- **Llegada:** la vela toca la banda de ±2 pts viniendo de un lado.
- **Sostenido:** antes de que el precio se acepte del otro lado, la mecha a favor se aleja 15 pts de la raya.
- **Rota:** un cierre más allá de L+5 con cuerpo de 4 pts o más, o 3 cierres seguidos del otro lado. Las mechas solas nunca rompen.
- **M1** (la métrica principal) = % de llegadas sostenidas.

**Placebos**
- (a) La misma serie corrida ±11, ±19 y ±31 pts.
- (b) Entre 500 y 2000 juegos de rayas al azar, con la misma vida, la misma forma y la misma distancia al precio.
- Vara [MEDIDO, `arnes/validacion_arnes.md`]: una raya sin información sostiene ≈ 45 % de noche y ≈ 49 % de día.
- El p del azar está calibrado. El IC bootstrap por sesión es optimista con pocas sesiones: con rayas al azar de día cubrió el 0
  solo el 75 % de las veces.

**Partición**
- ENTRENAMIENTO: hasta el 2026-09-30. Son 33 sesiones en total; 14 tienen QQQ y 30 tienen NDX.
- PRUEBA (sellada): 2026-10-01, 10-02, 10-05, 10-06, 10-07 y 10-08. Se abrió recién después de congelar el código y las finalistas
  con hash (`arnes/aperturas_prueba.log`).
- CASO: 2026-10-09, solo descriptivo.
- Ventanas por hora de Nueva York: noche 18:00-09:30, día 09:30-16:00.

**Candidatas** (24; `PREREGISTRO.md:170-264`)
- 9 **referencias**: la selección de la 2.0, con su conversión y con la de la 4.1, más los 6 defaults de la 4.1.
- 8 **nuevas**: conversión con carry, filtro de régimen positivo, imán contra repelente, flujo firmado de NQ, confluencia QQQ+NDX,
  banda de movimiento esperado y call/put wall al estilo SpotGamma.
- 7 **controles sin gamma**: redondos de 25, 50 y 100, grilla de strikes de QQQ, strikes al azar, máximo y mínimo de la última hora,
  y mechas previas.

**Para ganar en PRUEBA** (`PREREGISTRO.md:284-301`) hacía falta todo esto junto:
- ≥ +10 pp contra los corridos y también contra el azar;
- p del azar < 0,05 ajustado por Holm;
- ≥ 15 strikes distintos;
- que también gane en los tres modos del juez;
- que no recorra menos que los corridos;
- que no sea sombra del precio.

---

## 3. Qué sobrevivió y qué no

### 3.1 Veredicto: nada [MEDIDO]
- **Referencias** en PRUEBA: 0 de 18 pruebas ganan (9 series, de noche y de día).
  - Ninguna cumple ni el tamaño ni la significancia.
  - El p más chico es 0,148 (C08 de noche); ajustado por Holm sobre 18, da 1,0.
- **Nuevas:** 0 de 8 llegan a la vara de entrenamiento (+5 pp, p < 0,05 y límite inferior del IC90 > 0), así que no se les abrió PRUEBA.
- **Controles:** 0 de 12 (son informativos).
- **Promedio** de las 18 referencias contra sus corridos [RECALCULADO de los resultados de los probadores]:
  - −1,0 pp en entrenamiento y +0,9 pp en PRUEBA.
  - En PRUEBA, 12 de 18 dan positivo. Tirando monedas, 12 o más salen el 12 % de las veces, y además estas pruebas no son
    independientes.
- **"Elegir la mejor" con validación dejando una sesión afuera** (es lo que ganaría quien elige la mejor de entrenamiento):
  - grupo 1: −1,6 pp de noche y −2,9 de día;
  - grupo 2: de día elige C10, que fuera de muestra da −3,5;
  - grupo 4: −0,4 y +1,6.

### 3.2 Referencias en PRUEBA (6 sesiones; tabla para el agente)

Fuentes: `probador1/resultados/prueba/resumen_prueba.json` y `probador2/resultados/resumen_PRUEBA.json`. "Base" = llegadas no
censuradas. El IC95 es bootstrap por sesión y es optimista.

| serie | ventana | M1 % | corridos % | azar % | real − corridos, pp (IC95) | p azar | base | strikes |
|---|---|---|---|---|---|---|---|---|
| C01 2.0 QQQ, conversión 2.0 | noche | 46,9 | 47,4 | 47,4 | −0,5 (−6,4; 4,5) | 0,557 | 241 | 20 |
| C01 | día | 51,6 | 50,8 | 51,4 | +0,9 (−8,8; 10,5) | 0,478 | 186 | 26 |
| C02 2.0 NDX | noche | 41,0 | 50,6 | 47,3 | −9,6 (−13,3; −5,8) | 0,936 | 183 | 18 |
| C02 | día | 44,6 | 50,5 | 51,1 | −6,0 (−17,4; 3,0) | 0,921 | 175 | 33 |
| C03 selección 2.0, conversión 4.1 | noche | 48,4 | 46,3 | 47,3 | +2,1 (−5,2; 7,6) | 0,396 | 221 | 18 |
| C03 | día | 54,6 | 50,5 | 51,6 | +4,1 (1,4; 6,8) | 0,256 | 185 | 23 |
| C07 4.1 MAJORS_QQQ_oi | noche | 51,8 | 44,3 | 47,1 | +7,5 (4,6; 12,4) | 0,166 | 110 | 10 |
| C07 | día | 54,1 | 52,2 | 50,9 | +1,9 (−11,4; 12,4) | 0,308 | 85 | 17 |
| C08 4.1 MUROS_QQQ_oi | noche | 53,6 | 45,4 | 47,8 | +8,2 (1,5; 14,7) | 0,148 | 125 | 9 |
| C08 | día | 54,1 | 51,5 | 50,8 | +2,6 (−3,2; 7,7) | 0,282 | 98 | 17 |
| C09 4.1 MUROS_NDX_vol | noche | 42,8 | 49,0 | 47,0 | −6,2 (−9,4; −0,5) | 0,834 | 145 | 17 |
| C09 | día | 51,0 | 50,8 | 52,3 | +0,1 (−7,1; 8,7) | 0,620 | 155 | 26 |
| C10 4.1 ZEST_QQQ_vol | noche | 47,2 | 43,9 | 47,2 | +3,3 (−7,9; 11,8) | 0,506 | 36 | 15 |
| C10 | día | 43,1 | 48,9 | 51,1 | −5,8 (−17,9; 7,3) | 0,856 | 65 | s/d |
| C11 4.1 MUROS_NQ_oi | noche | 51,5 | 51,6 | 48,6 | −0,1 (−13,6; 9,6) | 0,324 | 68 | 12 |
| C11 | día | 54,3 | 49,8 | 50,6 | +4,4 (−4,8; 15,2) | 0,243 | 94 | 17 |
| C12 4.1 FAM_MUROS_oi | noche | 55,3 | 47,2 | 48,8 | +8,1 (−3,1; 18,1) | 0,173 | 76 | 15 |
| C12 | día | 52,5 | 50,6 | 52,0 | +1,9 (−8,7; 8,8) | 0,462 | 99 | 35 |

**Por qué ni lo mejor alcanza:**
- **C08 de noche** (+8,2 pp, el número más alto):
  - tiene 9 strikes distintos en 6 noches, y la vara pide 15;
  - el p es 0,148;
  - contra el azar da +5,8, lejos de +10;
  - en entrenamiento dio 0,0.
- **C07 de noche** (+7,5): 10 strikes y p 0,166. En entrenamiento dio −1,7, con signo opuesto entre las dos mitades.
- **C12 de noche** (+8,1): en entrenamiento había dado −7,3. Cambió de signo.
- **C03 de día** (+4,1): en entrenamiento dio +0,2, y en modo estricto −5,2.

### 3.3 El patrón de la semana de PRUEBA es la grilla de QQQ, no la gamma
- **Lo que se vio.** En las noches de PRUEBA, varias rayas puestas sobre strikes de QQQ y convertidas con la razón de la 4.1 salieron
  arriba de su placebo corrido: C07 +7,5, C08 +8,2, C12 +8,1 y C03 +2,1.
- **El control sin gamma dio lo mismo.** C22 dibuja los dos strikes de QQQ más cercanos al precio, sin ninguna gamma. Dio +6,4 pp
  (IC95 de 3,0 a 10,0), +4,0 contra el azar con p 0,087, en 431 llegadas, 21 strikes y 5 de 6 noches ganadas
  [MEDIDO, `probador4/resumen_PRUEBA.md`].
- **En entrenamiento no estaba.** Todas daban ≈ 0: C22 +0,2, C08 0,0, C07 −1,7, C12 −7,3.
- **Strikes sorteados contra gamma.** En entrenamiento, el control C05 (2 strikes de QQQ sorteados al azar, al mismo ritmo que la 2.0)
  sostuvo de noche MÁS (47,8 %) que la selección por gamma (C01 43,1 %, C03 45,9 %).
- **Los "exactos"** (sostenidos con la punta a ≤ 2 pts de la raya), en las noches de PRUEBA:
  - C12 38,2 % contra 25,8 % (p 0,022); C08 32,0 contra 23,9; C07 30,9 contra 23,1;
  - pero la grilla sin gamma C22 también: 29,2 contra 24,2;
  - y en entrenamiento no apareció (C12 24,6 contra 26,2).
  Es una métrica secundaria, entre muchas comparaciones: exploratorio.
- **Lectura.** Si esa semana hubo algo, fue "estar sobre un strike de QQQ", que en NQ es una grilla cada ~41,4 pts, y no la gamma.
  En las semanas anteriores no aparece.
- **Ojo con la comparación.** Los corridos (±11, 19 y 31) caen fuera de esa grilla. Por eso, de acá en más, toda serie hecha con
  strikes de QQQ se compara también contra C22, no solo contra su placebo.

### 3.4 Lo que se dio vuelta entre entrenamiento y PRUEBA (la firma del ruido)
- **C02** (la capa NDX de la 2.0), de noche: fue la mejor del grupo 1 en entrenamiento (+3,6) y la peor en PRUEBA (−9,6; 0 de 6
  noches ganadas).
- **C11** (MUROS_NQ_oi), de noche: +3,7 → −0,1.
- **C16** (confluencia QQQ+NDX), de noche: +28,2 pp en entrenamiento, pero con solo 16 llegadas, 4 strikes y 3 sesiones, y con signo
  opuesto entre las mitades (+36,3 y −3,8). Es una anécdota: hacen falta unos 15 strikes (`la-muestra-son-niveles-no-minutos.md`).

### 3.5 Las nuevas (entrenamiento; ninguna elegible) [MEDIDO, `probador1/` y `probador3/`]
- **C13 imán** (GEX por volumen > 0): +1,7 pp de noche, +1,1 de día.
- **C14 repelente:** −4,4 y +2,1. No rompe más ni pincha más hondo que el imán. El signo no rescata nada, que es justo lo que decía la
  regla escrita en `PREREGISTRO.md:238`.
- **C15 flujo firmado de NQ:** +0,2 y −3,1. Coincide con lo que ya decía la memoria `gamma-hoy-1-9`.
- **C06, solo con régimen de gamma positiva:** +2,9 y +1,9. El signo es estable, pero la muestra es chica (21 strikes de noche).
- **C04, conversión con carry:** −1,4 y −0,9. Corregir la razón en 2 a 7 pts no cambia el resultado.
- **C17, banda de movimiento esperado:** −15,6 pp de día. Cuando se toca, se atraviesa más seguido que una raya cualquiera. Esta raya
  apunta al tamaño del día, no al giro.
- **C18, call/put wall al estilo SpotGamma:** +5,9 y +2,8, con solo 7 strikes de noche. Queda lejos del precio: 212 pts de mediana de
  noche.

### 3.6 Controles [MEDIDO, `probador4/`]
- **Redondos de 25, 50 y 100:** sin ventaja (PRUEBA de noche +1,4, −1,0 y −3,0 pp contra corridos).
- **Los giros no se juntan en los redondos.** Los giros de 20 pts caen a ±2 pts de un múltiplo de 25 el 16,1 % de las veces de
  noche; una grilla uniforme da 17 % (`probador4/extra_redondez_giros.json`).
- **Máximo y mínimo de la última hora (C23) y mechas previas (C24):** quedan igual que su placebo (−0,5 y −0,2 pp de noche). Con este
  juez, "las mechas anteriores frenan" tampoco se sostiene.

### 3.7 El CASO 2026-10-09 (n = 1, fuera de toda decisión)
**Aviso de antigüedad:** son datos históricos de las 03:00 UTC del 2026-10-09, de 2 h 30 min antes de escribir esto. No son niveles
para operar ahora.

**Dónde quedó el strike 750 de QQQ:**
- La 2.0 escribió razón 41,4447, o sea 31083,52 (fuente: el AUDIT de la 2.0, `dos_log`).
- La 4.1 grabó 41,4352, o sea 31076,37 (fuente: el niv de la 4.1, `cuatro_log`).
- La reconstrucción independiente da 31083,50 y 31076,03 [MEDIDO, `probador1/resultados/caso/caso_1009.json`].

**Juzgada esa noche, hasta las 03:53 UTC:**
- **La raya de la 2.0** sostuvo 14 de 19 llegadas (74 %), contra 36 % de sus corridos.
  - De esas, hubo 8 llegadas de techo entre las 02:00 y las 03:35 UTC: 7 sostenidas, 3 de ellas con falso rompimiento.
  - La de las 03:35 se rompió. Después el precio hizo 31092 a las 03:44 y 31098,75 a las 03:53.
- **La de la 4.1 (C08)** sostuvo 4 de 13 (31 %): 9 rotas, con pinchazos de 6,5 a 13,2 pts.

**Por qué no prueba nada:**
1. **Las llegadas no son independientes.** Varias llegadas al mismo 31083,50 comparten el mismo giro: 6 tienen un recorrido idéntico
   de 43,5 pts.
2. **Era una congestión.** Entre las 02:00 y las 02:46 UTC el precio anduvo entre 31067 y 31089. En una congestión, la raya que cae en
   el borde de arriba "aguanta" y la que cae en el medio "se rompe", y eso no dice nada de la fórmula.
3. **La raya de la 2.0 venía moviéndose.** Esa misma noche, antes de quedar fija, la raya 750 de la 2.0 se movió de 31000,6 a 31091,4
   [MEDIDO, agente de datos].
4. **En las sesiones que deciden pasa lo contrario.** Con la misma selección, conversión 2.0 contra conversión 4.1, de noche: 43,1 %
   contra 45,9 % en entrenamiento y 46,9 % contra 48,4 % en PRUEBA.

---

## 4. El falso rompimiento (el pedido de hoy del operador)

Analogía: es una puerta con resorte. La empujás, se abre un poco y vuelve a cerrarse sola. El juez cuenta eso como "la puerta aguantó".

**Ya está incluido desde el pre-registro.** La aclaración del operador está en `PREREGISTRO.md:18-20` y el juez la aplica
(`juez_operador_v1.py:8-9, 31-42, 114-117`):
- Una mecha que atraviesa la raya **no** la rompe, sea del largo que sea.
- Las velas de cuerpo chico o de indecisión que cierran del otro lado **tampoco** la rompen. Rompe solo si pasa una de estas dos cosas:
  - 3 cierres seguidos del otro lado;
  - un cierre a más de 5 pts con un cuerpo de 4 pts o más, o sea una vela con convicción.
- Si después el precio gira 15 pts a favor, cuenta como **acierto**. Además se anota aparte como "falso rompimiento", con la
  profundidad del pinchazo.
- También se corrieron un modo MUY TOLERANTE (rompe recién con un cierre a más de 8 pts con cuerpo de 6 o más, o con 5 cierres
  seguidos) y un modo ESTRICTO.

**Resultado [MEDIDO]:**
- **Le pasa a cualquier raya.** Cerca de la mitad de los giros sostenidos pasaron por un falso rompimiento, sin importar la raya. Con
  los redondos de 25 en PRUEBA dio 49 % de noche y 56 % de día; sus placebos, 47-48 % y 55-56 % (`probador4/resumen_PRUEBA.md`). El
  pinchazo mediano ronda 4-6 pts (en entrenamiento: C13 4,7, C14 3,9, C03 5,7). Pinchar y volver no es una marca de las rayas de gamma.
- **El modo muy tolerante tampoco rescata nada.** En PRUEBA de noche, contra corridos: C08 +7,0 pp, C07 +3,9, C12 +6,7 y la grilla
  sin gamma C22 +4,4. Todas quedan debajo de +10 y ninguna es significativa.
- **Las rayas de OI de QQQ de la 4.1 tuvieron MENOS falsos rompimientos que sus corridos** en las noches de PRUEBA: C07 40 % contra
  49 %, C08 42 % contra 49 %, C12 36 % contra 47 %. O sea, esa semana aguantaron más limpio. Pero en entrenamiento no pasó, y es la
  misma semana del efecto grilla (sección 3.3). Exploratorio.

**Para el operador:** la tolerancia que pidió es la correcta para juzgar, y es la que se usó. El problema no es que el juez sea
estricto: con su propio criterio, una raya cualquiera aguanta casi lo mismo.

**"Todas las mechas posibles":**
- Las rayas que de verdad dibujaron la clásica, la 2.0 y la 3.0 estaban presentes en solo el 2-10 % de los giros de 20 pts
  (`soportes_metodo/`, 17 sesiones). En este workflow, C03 llegó al 5,8 % en PRUEBA.
- La grilla de QQQ cae cada ~41,4 pts de NQ. Con una tolerancia de ±4 pts, cubre el 19 % de cualquier punto al azar (cuenta del
  agente de practicantes).
- Tocar todas las mechas solo se logra con rayas tan densas que tocan todo, y justamente eso es lo que el placebo desenmascara.

---

## 5. La conversión QQQ/NDX a NQ correcta según los datos

Analogía: para pasar un precio de dólares a pesos hay que usar la cotización del mismo momento. Si usás el dólar de las 16:14 con un
precio de las 2 de la mañana, el número sale corrido aunque la cuenta esté bien hecha.

### 5.1 QQQ → NQ: nivel = K × razón
**La razón correcta es la sincronizada**, que es la de la 4.1 (`atas/PythiaGexCuatro/_modulos/familia/qqq/ConversionQqq.cs:3-8`):
- cada muestra divide el MNQ y el QQQ del MISMO instante (el MNQ de 900 s antes de la marca de la cadena, por el retraso de CBOE);
- solo se toman muestras con el spot vivo;
- la razón es la mediana robusta de las últimas 24 muestras.

Así traducen strikes entre mercados Golez y Jackwerth (2012), con la mediana de pares sincrónicos por minuto durante la rueda, y
MenthorQ, con precios de cuando cotizan los dos [LEÍDO].

**Medido el 2026-10-08** [MEDIDO por el agente de modelos]: MNQ a las 16:00:00 NY = 30976,25. Dividido por el cierre de QQQ (747,58)
da 41,4354, que es la razón que grabó la 4.1 (41,4352).

**Por qué se corre la de la 2.0.** Divide el MNQ de la vela del último trade de opciones (16:14:59 NY) por el spot de QQQ, que sigue
moviéndose en el after-hours y en la pre-apertura. Por eso deriva de noche. [RECALCULADO, `informe/deriva_conversion.py`, 19 noches
con QQQ] A strike 750, la diferencia entre la 2.0 y la 4.1 es:
- de noche: mediana 27 pts por noche, p90 típico 169 pts, y 530 pts de p90 en la peor noche;
- de día: mediana 3,3 pts y p90 15,4.

El agente de datos, que agrupó distinto, midió 31 y 197; el CHANGELOG de la 4.1.3 dice 24 pts en 16 noches. Es el mismo orden de
magnitud.

**El carry no cambia nada.** Corregir la razón de noche por el costo de mantener el futuro (C04, con r − q = 0,0315 SUPUESTO) mueve la
raya 2-2,4 pts y no cambió el resultado.

### 5.2 NDX → NQ: nivel = K + base
**La base correcta** también es la de la 4.1 (`_modulos/familia/ndx/ConversionNdx.cs:8-14`):
- de día, la mediana sincronizada de la rueda (09:35-15:59 NY);
- de noche, la última mediana de la rueda, achicada por el carry.

**La "alineada" se corre de noche.** El spot de NDX se congela a las 16:00 NY, así que una base "alineada" medida después queda
corrida: 17,7 pts de mediana de noche [MEDIDO, agente de datos]. Es el mismo problema del 06-10 en el CLAUDE.md (271 contra 251).

### 5.3 Qué dicen los resultados
- Con la misma selección, la conversión no fue el factor: C01 contra C03 dan dentro del ruido en las dos fases, y C04 igual.
- La de la 4.1 es la correcta por construcción y no deriva. La de la 2.0 solo "ganó" la noche del CASO.

### 5.4 Qué no se sabe
- No se probó si el settlement de CME de las 16:00 ET es mejor ancla que la mediana de la rueda.
- El dato de que el settlement es a las 16:00 ET viene de un resumen de buscador: la página de CME no cargó.

---

## 6. Recomendación para la 4.1

**Regla aplicada** (`PREREGISTRO.md:296-301` y CLAUDE.md): solo se propone tocar lo que se dibuja si algo gana en PRUEBA y después en
ADELANTE. Nada ganó. Los escépticos no mandaron veredicto, pero eso no cambia nada: no hay ninguna ganadora que defender.

### 6.1 Qué NO cambiar
- **Ningún default de dibujo de la 4.1.** MAJORS_QQQ_oi, MUROS_QQQ_oi, MUROS_NDX_vol, MUROS_NQ_oi, FAM_MUROS_oi y ZEST_QQQ_vol
  (`_modulos/pantalla/SeriesPantalla.cs:25-53`) quedan **sin validar**. No ganan, pero tampoco hay otra fórmula que les gane.
- **La conversión:** se queda la sincronizada (ConversionQqq.cs y ConversionNdx.cs). No volver a la razón de la 2.0.
- **Nada de filtros** por signo, por régimen, por confluencia ni por flujo: ninguno rescató nada.

### 6.2 La 4.1.3 (escrita, NO instalada)
**Por qué digo que no está instalada:** a las 02:32 ART, el DLL de `%APPDATA%/ATAS/Indicators` era de las 01:11, y la entrada 4.1.3
del CHANGELOG es de las 02:16.

La pidió el operador ("copia la formula de la 2.0 y agregala a lo que ya tenemos"): verla es decisión suya. Lo que dicen los datos de
cada serie, para decírselo en el mismo mensaje y con captura antes y después (regla del 17-09):
- **R20_QQQ_vol = C01.**
  - Sin ventaja: en PRUEBA, −0,5 de noche y +0,9 de día.
  - Su conversión deriva de noche (5.1).
  - El CHANGELOG ya la presenta como "réplica para comparar", y está bien así.
- **R20_NDX_vol = C02.**
  - Fue la peor de toda la PRUEBA de noche: −9,6 pp contra corridos, IC95 de −13,3 a −5,8, 0 de 6 noches. En entrenamiento había dado
    +3,6.
  - No prueba que "perjudique": la cola de abajo da 0,064 y el IC es optimista.
  - Pero no hay ninguna base para prenderla por defecto. **Sugerencia:** apagada por defecto, o rotulada "sin respaldo medido".
- **DOMS_QQQ_vol = C03.** La mejor parada de las tres (PRUEBA +2,1 de noche y +4,1 de día, p 0,26-0,40), pero sin validar.

**Cuántas rayas suma.** Prender tres series más agrega hasta 6 rayas. Eso va contra "pocas rayas" (`PREREGISTRO.md:300`) y contra la
memoria `como-dibujan-los-pros` (3-5 líneas). No medí el efecto visual [SUPUESTO].

### 6.3 Qué SÍ hacer (datos, no dibujo)
- **Seguir grabando** los `niv-*.jsonl` de la 4.1 en todas las sesiones: son lo que dibujó, minuto a minuto. Hoy solo existe la
  sesión 10-09, y es la muestra de ADELANTE.
- **Tapar los huecos de escucha** [MEDIDO, agente de datos]. Una noche con huecos no cuenta.
  - La noche del 10-09, la 4.1 no escuchó de 22:01 a 01:04 UTC.
  - La viva de NQ no graba casi ninguna noche entre ~05:30 y 13:00 UTC.
- **Completar la sesión 10-09:** correr `python -I datos/construir.py todo` después de las 21:00 UTC del 2026-10-09, porque la caché
  de ATAS escribe la sesión al cerrarla.

### 6.4 Cómo juntar muestra (fase ADELANTE)
**Cuántas sesiones hacen falta** [RECALCULADO, `informe/potencia_adelante.py` → `potencia_adelante.json`]. Supuestos de la cuenta:
- base de 47 %, potencia de 80 % y prueba de una cola contra el azar;
- efecto de diseño de 1,3 a 1,5, medido de la dispersión del placebo en PRUEBA;
- unas 21 llegadas por noche para una serie como C08, y unas 16 de día;
- solo el ~80 % de las noches tiene QQQ completo.

Resultados:
- **Para ver +10 pp** (la vara de ADELANTE): 200-330 llegadas, o sea 10-16 noches útiles (12-20 sesiones): principios de noviembre.
- **Para ver +5 pp** (el tamaño de Osler) **con UNA hipótesis**: 800-920 llegadas, o sea 38-44 noches útiles (48-55 sesiones desde el
  2026-10-12): entre el 17 y el 29 de diciembre.
- **Con TRES hipótesis** (Holm): 1.140-1.320 llegadas, o sea 55-63 noches útiles (69-79 sesiones): entre fines de enero y
  principios de febrero de 2027.
- **Noche y día como una sola hipótesis**, pre-registrada así: 22-25 sesiones útiles para +5 pp (27-31 sesiones): hacia fines de
  noviembre.
- **Vara de 15 strikes distintos:** C08 vio 9 en 6 noches, así que hacen falta ~10 noches solo para eso.
- **El roll de diciembre** (tercer viernes, 2026-12-18) cae adentro. Es una trampa conocida: ver la regla roja del roll en el
  CLAUDE.md.

**Plan concreto:**
1. **Antes del 2026-10-12, pre-registrar y sellar con hash 2 o 3 hipótesis NUEVAS, como mucho.** Se eligen con lo visto en PRUEBA,
   pero se juzgan solo con sesiones nuevas. Mismo juez (sha `a20a6268…`), mismo arnés (sha `1cd60839…`), sin parámetros libres, y con
   C05 (strikes al azar) como control.
   - H1: MUROS_QQQ_oi (C08) de noche, contra el azar.
   - H2: la grilla de QQQ sin gamma (C22) de noche, contra el azar.
   - H3: C08 contra C22, para saber si la gamma agrega algo a "estar en un strike".
2. **No decidir nada mirando resultados parciales** antes de la fecha fijada. Mirar y parar "cuando da" infla los falsos positivos.
3. **Decidir en la fecha fijada.** Con ~12-20 noches útiles solo se puede afirmar o descartar un efecto de 10 pp o más; con ~40-60,
   uno de ~5 pp.
4. **Si a esa altura no gana nada, cerrar la pregunta "dónde gira la mecha".** La 4.1 puede quedar como mapa del régimen y del tamaño
   esperado del movimiento, que es lo único que el proyecto ya midió como anticipable (memoria `busqueda-gatillo-2026-09-17`), y
   nunca de la dirección.

---

## 7. Límites (lo que este informe NO dice)
- **"No gana" quiere decir "no hay un efecto grande".** Con 6 sesiones de PRUEBA solo se ven efectos de 15 pp o más; uno de 5 pp es
  invisible. No quiere decir "no hay efecto".
- **PRUEBA no era ciega para las referencias de la 2.0 y la 4.1.** Esas sesiones ya se habían usado con otros jueces
  (backtest_familia, extremos_rebote), y quizás con este (los procesos r20/v41). Por eso una referencia que ganara tampoco habría
  contado.
- **Velas de 1 min sin ticks.** Dentro de la vela se supone el orden o→l→h→c si es alcista y o→h→l→c si es bajista. Del 09-23 al
  10-06 no hay ticks: el precio de las muestras de conversión sale de la vela de 2 min interpolada.
- **La conversión 'dos' de QQQ es una reconstrucción**, porque la 2.0 alternó gráficos de 1 y 2 min. La exacta ('dos_log') solo
  existe desde el 09-18 y cuando la 2.0 estaba corriendo: 471 minutos en entrenamiento, que no alcanzan.
- **CBOE llega con 15 min de retraso**, y el OI siempre es el de ayer.
- **No se probó:** el pin al cierre de QQQ contra NDX, TQQQ (sin conversión de la 4.1 en el dataset), la pared del libro de órdenes
  (no hay historia del DOM) ni los modelos de liquidez repreciada.
- **Fuentes leídas solo por resumen:** Ni-Pearson-Poteshman 2005, Adams 2025, Elms 2026, Kavajecz-Odders-White 2004 y
  Cont-Kukanov-Stoikov 2014; Dim-Eraker-Vilkov 2024, en parte.
- **Cifras que no coinciden entre agentes:** las de Golez, porque citan versiones distintas (JFE 2012 y WP 2010), y las de Amaya et
  al. a 30 min (+7,0 contra +6,4 pp). No cambian nada de lo de arriba.
- **Dirección y rentabilidad:** nada de esto anticipa dirección ni habla de rentabilidad. El GEX describe régimen.

---

## 8. Archivos

Todos bajo `C:/Users/wmx_7/OneDrive/Escritorio/ATAS nada/PythiaGex/laboratorio/calibracion_1009/`.

**Pre-registro y arnés**
- `PREREGISTRO.md`: sha256 en `PREREGISTRO.sha256`, sin desvíos.
- `arnes/evaluar.py`: sha `1cd6083989f4…`.
- `arnes/juez_operador_v1.py`: sha `a20a62688e88…`.
- `arnes/validacion_arnes.md` y `arnes/aperturas_prueba.log`.

**Resultados**
- `probador1/resultados/{entrenamiento,prueba,caso}/`: grupo 1 y el CASO.
- `probador2/resultados/resumen_{ENTRENAMIENTO,PRUEBA}.json` y `probador2/resultados/CASO_1009.json`: grupo 2.
- `probador3/tabla_ENTRENAMIENTO.json` y `probador3/seleccion_ENTRENAMIENTO.json`: grupo 3.
- `probador4/resumen_{ENTRENAMIENTO,PRUEBA}.md` y `probador4/extra_redondez_giros.json`: grupo 4.

**Datos e investigación**
- `datos/`: dataset, `cargar.py`, `indice.json`, `conv_min/`.
- `academica/fuentes_academicas.json`, `metodologias_proveedores/`, `practicantes/formulas_practicantes.json`,
  `modelos_matematicos/` y `soportes_metodo/`.

**De este informe**
- `informe/deriva_conversion.py` → `informe/deriva_conversion.json`.
- `informe/potencia_adelante.py` → `informe/potencia_adelante.json`.
