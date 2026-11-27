# Ronda 17 — Anatomía del movimiento — RESULTADOS (se escribe por tanda)

Estado: tandas 0, 1 y 3 completas (26-11-2026, ~23:55 AR). Pre-registro: `PREREGISTRO.md` (23:05 AR, 4 enmiendas). Todo
es explicativo/exploratorio salvo las 3 ruedas nuevas de MES (23, 24 y 25-11).

---
## 1. Lo ya medido antes (no se repite)

| Pieza | Dónde y con qué | Qué dio |
|---|---|---|
| Absorción (4 familias: muro, iceberg, volumen por tick, atrapado) | R10, 23 ruedas MNQ cinta | ninguna paga el costo; el nivel absorbido se rompe 95 % en 5 min; si algo, CONTINUACIÓN: caudal > p99,5 → sigue el agresor 67,5 vs 56,3 % (baúl #13) |
| Delta por evento (ola, contrapié, traba, nido) | R11, 23 ruedas MNQ | seguir la ola pierde 36/36; ir contra la ola +0,93 neto t 1,85 (mitades +1,71/+0,15) |
| 9 familias de cinta + ML | gatillo (busqueda 17-09), 34 ruedas MNQ + 7 MES | dirección: nada; TAMAÑO de 5 min sí (AUC 0,86-0,90) pero sale del rango previo y la hora |
| Order flow en la banda (delta z, grandes, tren, divergencia, absorción) | gatillos.py, 15 días ES 1 min | 46-53 %; "tres deltas en contra" 70 % de 27 en ES, NO replica en NQ |
| ML de ES a 10 min | gatillo_cientifico / ml_check | adentro 61 %, afuera AUC 0,52, hacia adelante 46,3 % |
| Flujo Claro (CVD) | cvd_*.py, 20 ruedas | delta y precio juntos en la vela (+0,69 a +0,82), con la vela siguiente −0,03 a −0,09 |
| Rondas 1-9 (DOM, minutos, footprint 5 min, perfil, ~12.500 combinaciones, su patrón) | dom\ronda1..9 | nada fuera de muestra; lo estable: gestión (tope, no promediar, stop) |
| Rondas 12-15 (gamma, griegas, flujo, cinta en el toque) | ronda12-15 | rayas = placebo; cinta en el toque: nada en dirección, actividad del 1.er minuto → tamaño (t 3,7) |
| X-Ray | xray | relata; no da lado ni tamaño (corregido 16-11) |
| Tendencia / Corriente | tendencia | ningún túnel anticipa (46-50 %); el "76 % vs 55 %" de la Corriente 1.3 es DESCRIPTIVO (acuerdo con un zigzag de 6 pts mirado hacia atrás), no pronóstico |
| R16 / R16b / R16c (su método, ES 49 ruedas) | ronda16_reels | absorción en el nivel C1 −3,89 (t −2,17, n 14); estallido C3 +1,36 (t 1,61, n 80, solo un tramo); retesteo del apilado C5 +1,85 (t 1,21, n 33); big trades ≥ 95 C4 −0,21 |

Baúl (`PythiaGex-privado\BAUL_DE_PISTAS.md`): banda +3 del VWAP formada (informativa en 3 tramos), contra el empujón corto
(+0,26, t 4,67), veto de llegada impulsiva, veto de caudal anómalo, delta a favor del rebote como contraindicador, mecha del
lado de origen, régimen "va y vuelve", tamaño (rango previo + hora; ATR × franja; rango ≈ 1,86 straddles), gestión.

---
## 2. Auditoría de sesgos de lo ya medido (prioridad del operador)

Hecha con dos auditores independientes de solo lectura (cinta / velas) más chequeos míos. Marca [V] = lo verifiqué yo en el
código. Veredicto general: **no apareció un error que convierta una ganancia en "nada"**, pero sí varios que hacen que el
"nada" de antes sea en muchos casos **"inconcluso"** o **"no medido a la escala correcta"**, y dos placebos que sesgaron.

**Graves**
1. **Potencia insuficiente.** Los jueces de la cinta (barridos, absorción, escalas, punta) pedían acierto ≥ 57,3 %, z ≥ 2,5
   contra placebo y n ≥ 100 en UNA confirmación de 8 ruedas: con n 100-150 eso exige ver ~61 % (65-68 % en momentum). Un borde
   real de 58 % pasaba ~20 % de las veces. Corrección: rotular "inconcluso", calcular la potencia antes y juzgar por neto con
   error agrupado por día (`gatillo\resultados\barridos.md`, `punta.md`; R11 ya lo admite en INFORME_DELTA.md).
2. **Escala equivocada para un scalper.** `gatillos.py`, `gatillo_cientifico.py`, `ml_check.py` usan ±6 pts de ES en 30 min;
   R4/R7 velas de 5 min con salidas de 6-12 velas; el propio proyecto ubicó la información a 30-60 s y DENTRO de la vela del
   toque (baúl #1, #11). Corrección: cinta, entrada a +1-2 s, objetivo 2,5-4 pts, horizonte 1-5 min.
3. **El "nada" del micro.** gatillo y rondas 1, 10 y 11 son MNQ (punta de ~7 contratos que copia a NQ; `base.py` toma solo
   `cinta-MNQ*`). El "delta por tamaño" supone orden chica = jugador chico, pero los algoritmos trocean (Barclay y Warner 1993;
   Chakravarty 2001). Corrección: rehacer sobre la cinta de ES (esta ronda).
4. **Filtro que mira adelante en `rebote_niveles.py`** [V]: clasifica "ruptura" con las velas i+1 e i+2 (líneas ~85-89) y la
   descarta (`if t["cat"] != "ruptura"`): tira justo los perdedores, infla el rebote real y el placebo, y borra lo único en que
   una raya podría diferir (romperse menos). Corrección: clasificar solo con lo anterior a la entrada.
5. **"Footprint" que no era footprint.** R4 A1-A4 son agregados de vela de 5 min; el perfil de MES repartía el volumen parejo
   de mínimo a máximo (POC con error medio 31,6 pts en MNQ); los "grandes" de K eran volumen/ticks. Corrección: footprint por
   precio desde la cinta (esta ronda).

**Medias**
6. **Placebo "misma media hora, mismo lado" mira adelante** [V en `barridos_confirmar.py`]: sortea segundos de antes y de
   después del disparo con el lado del disparo; si el lado salió del movimiento previo, esos segundos ya "conocen" el
   movimiento. Castiga el momentum y favorece lo contrario (momentum 54-59 % de placebo; contrarias 40-47 %). También en
   `absorcion_base.py`, `escalas_lib.py`, `punta_lib.py`, `r9_j_03_juez.py`. Corrección: solo segundos posteriores o lejanos
   (≥ 60 min), o lado al azar en el mismo instante.
7. **Placebos corridos un número fijo de puntos** (`gatillos.py` ±25-50, `motor.py` ±7,5/12,5): la banda +3 del baúl da 29,6-36,5 %
   de placebo cuando un paseo al azar daría ~44 % con esa vara (dato de memoria, sin recalcular): parte de "lo más firme" del
   proyecto puede ser del placebo. La limitada se llena con el toque (`motor.py`) y el umbral del régimen sale de la misma
   muestra que lo "confirma" (`regimen.py`). Corrección: placebo en desvíos con distancia al precio emparejada, 1 tick de
   penetración, error por día.
8. **Control "sin banda" vacío por construcción** [V `gatillos.py:265` `a = v["delta"] > 0`]: las hipótesis "delta en contra",
   "tren contra", divergencia y absorción + delta en contra no pueden disparar sin banda; la memoria dice "sin banda son una
   moneda": no se midió.
9. **Lectura de Flujo Claro.** La correlación del delta con la vela siguiente (−0,03 a −0,09) se leyó como "no adelanta"; con
   1.500-3.000 velas, −0,09 está a 3-5 errores estándar: es reversión de UNA vela (coherente con baúl #11), nunca medida como
   fade de 1-2 velas con costo.
10. **Confluencias que no lo eran.** `cvd_fusion.py`: las 4 lecturas son el mismo delta y `bmax` sale de toda la muestra [V]
    (mira adelante); la única fusión del baúl entra al CIERRE de la vela de 5 min y tira el momento que el baúl ubica dentro
    de la vela.
11. **Umbrales fijos o de toda la rueda.** Percentiles que arrancan con el premercado (`barridos_lib`, `punta_lib`), contratos
    fijos (AB_30 ≥ 25), R16 con p60 de toda la rueda [V `r16_01_eventos.py`, `umbrales()`]: la mitad de los disparos de
    absorción cae en la primera hora y media. Corrección: percentil por franja de 30 min de ruedas previas.
12. **Hora fija en UTC** [V `base.py` `RUEDA = ("13:30","20:00")`]: también `gatillo_cientifico.py`, `rebote_niveles.py`,
    `ml_check.py`, `gatillos.py`, `cvd_superador.py`. Desde el 01-11 corre la rueda una hora. R15 y R16 usan hora de NY (bien).
13. **t por operación y no por día** [V R16]: el "t 2,57" de C3 sale con 12 y 19 disparos del 04 y 05-11.
14. **Corriente 76 %** [V `PREREGISTRO_SCALPING.md`]: circular y descriptivo; no cobra los ~82 cambios por rueda.
15. **Explorar y confirmar no comparables** (confirmar junta roll, Fed y vencimiento trimestral); conviene alternar semanas.

**Bajas (reconocidas, no dan vuelta veredictos)**
16. Costo contado dos veces en algunas variantes (ronda 1 entra al ask y resta 0,96 que incluye el spread; R15-O suma un tick
    y resta 0,70 con 2 ticks; R6-Q). Los informes muestran también la versión solo comisión.
17. El juez de R9 (heredado por R10/R11) cotiza con la punta "recién comida" (`dbid/dask`) y no con la de reposo (`rbid/rask`).
18. R16 borra duplicados mirando 5 columnas y reparte parejo los barridos (puede fabricar apilados en C5).

**Lo que está bien medido:** zona horaria y candado de tramos de R4, causalidad de R15-O y de las ventanas de la cinta (corte
de R10 con 0 diferencias), Deflated Sharpe de R7, ronda 8 de medio a medio con t por sesión, nulo de signos en tendencia,
SuperTrend/ADX/ER con Wilder, imbalance diagonal 300 % apilado de a 3 (estándar). El "no paga" de R9-R11 no depende del
placebo (34/34, 0/24, 36/36 corridas negativas).

**Cómo cambia la lectura:** lo honesto es "no hay un borde grande y mecánico a 5-30 min con velas, ni en el micro (MNQ)";
**no** "el order flow no tiene información". La teoría dice lo mismo: el flujo explica el precio en el instante y su poder
para anticipar dura segundos a pocos minutos (sección 3). Esta ronda va justo ahí: anatomía del movimiento en el instante.

---
## 3. Teoría dura (anclas; citas verificadas por un investigador aparte)

- El flujo firmado de la punta explica ~65 % del movimiento de 10 s en el mismo intervalo (Cont, Kukanov y Stoikov 2014,
  J. Financial Econometrics); a 1 min hacia adelante el R² fuera de muestra es ~0 o negativo (Cont, Cucuringu y Zhang 2023,
  Quantitative Finance); el horizonte útil es de unos pocos cambios de precio (Kolm, Turiel y Westray 2023, Mathematical
  Finance). El desbalance de órdenes predice a 5 min y no a 60 (Chordia, Roll y Subrahmanyam 2005, JFE).
- El signo del flujo tiene memoria larga porque los grandes fraccionan, y el precio no (Lillo y Farmer 2004; Bouchaud, Farmer y
  Lillo 2009): por eso el delta ACOMPAÑA y no anticipa. El impacto crece como la raíz del volumen (Tóth et al. 2011): la
  absorción se mide como residuo contra esa ley, no como puntos por contrato lineales.
- Tamaño e información: mueven el precio las órdenes medianas fraccionadas, no las enormes (Barclay y Warner 1993; Chakravarty
  2001); en CME, grandes compras dejan más impacto permanente y grandes ventas más transitorio (Frino et al. 2008, J. Futures
  Markets). VPIN anticipa volatilidad (Easley, López de Prado y O'Hara 2012) pero no agrega sobre volumen y volatilidad actuales
  (Andersen y Bondarenko 2014): el flujo informa TAMAÑO más que dirección.
- Volumen y reversión: con volumen alto de cobertura el retorno revierte, con volumen especulativo continúa (Campbell, Grossman
  y Wang 1993; Llorente et al. 2002). Stops agrupados aceleran el precio y los take-profit lo frenan (Osler 2005).
- Footprint y market profile: definiciones de práctica (Dalton, Jones y Dalton, Mind Over Markets; ATAS Learn: subasta
  TERMINADA = bid 0 en el máximo / ask 0 en el mínimo; desequilibrio diagonal; apilados de 3+), sin pruebas académicas con
  referato. BOS/CHoCH: práctica "smart money", sin papers.
- Combinar señales débiles con pesos iguales ayuda (Bates y Granger 1969; Timmermann 2006; Rapach, Strauss y Zhou 2010), pero
  elegir la combinación es una búsqueda que se infla (Novy-Marx 2015; Harvey, Liu y Zhu 2016; Bailey y López de Prado 2014).
- Piso empírico: casi ningún day trader minorista gana de forma persistente (Barber, Lee, Liu y Odean 2014; Chague, De-Losso y
  Giovannetti 2019-20, documento de trabajo).

---
## 4. Tanda 0 — anatomía en 6 ruedas (3 de ES elegidas por regla + 3 de MES nuevas), 26-11-2026 23:27 AR

Ruedas: ES 06-11 (RANGO, X 5,00), ES 09-11 (TENDENCIA alcista, X 5,25), ES 11-11 (MIXTA, X 6,00), MES 23-11 (RANGO por la
regla, X 9,75), MES 24-11 (MIXTA, X 9,75), MES 25-11 (TENDENCIA, X 9,75). X = 0,08 × mediana del rango de las 10 sesiones
previas (en MES salió 9,75 porque la semana del 16 al 20-11 fue de rangos enormes: el máximo semanal 7.614,25 y el mínimo
6.898). Escala X: **741 giros, 739 pausas, 3.003 momentos al azar**; micro X/2: 2.991 giros, 2.574 pausas, 1.231 momentos;
grande 4X: 48 giros, 43 pausas. Narraciones rueda por rueda: `narraciones\narracion-<inst>-<dia>.md` (primero los movimientos
principales, después cada giro de X con sus factores: es la "captura de datos" de cada giro). Tablas: `tanda0\*.csv`.
Calibración medida: orden grande p99 = 25-41 contratos en ES y 18-19 en MES; p99,9 = 81-107 en ES y 55-58 en MES; V_min de
apilados en MES = 18 contratos (mismo percentil que 30 en ES; mediana de celda ES 69, MES 39).

### 4.1 Quién movió el precio en cada pierna (lo que el flujo SÍ explica)
- **El lado que agredió ganó la pierna casi siempre.** Piernas de escala X con delta acumulado a favor del movimiento:
  ES 86-98 %, MES 96-99 % según el tipo de día; micro X/2: 86-95 %. Es la relación del mismo instante de la teoría (el flujo
  explica el precio mientras pasa). La agresión neta es poca plata: |delta| / volumen ≈ 4-12 % en la mediana.
- **En algunas piernas grandes eso se rompe** (en la tanda 0 parecía "las lentas"; la prueba amplia de la sección 6 lo corrige:
  son ~1 de cada 10 y las lentas van MÁS con el delta): escala 4X, piernas con delta a favor: ES TENDENCIA 60 %, ES RANGO 77 %.
  Ejemplos: ES 09-11 11:37-14:42 subió 60,25 pts a 0,33 pts/min con delta **−6.150**; ES 06-11 09:54-10:06 bajó 39,75 pts con
  delta +155 y 11:35-12:38 bajó 56,5 pts con delta +82; MES 25-11 10:36-11:17 bajó 56 pts con delta +145 y grandes netos
  comprando (−582). Ahí el que mueve es el libro pasivo (límites que se retiran de un lado), que el delta no ve (se mide en
  4.4 con el flujo de la punta).
- **Las órdenes grandes acompañan:** en la escala X el neto de órdenes ≥ p99 va a favor de la pierna (mediana +80 a +545
  contratos); los apilados aparecen en pocas piernas (0,02-0,35 por cada 10 pts).
- **La absorción en contra NO frena la pierna:** después de un episodio de absorción real contra la pierna (p90 de 60 s), el
  precio siguió X/2 a favor el 78-87 % de las veces; un segundo cualquiera de la misma pierna, el 82-88 %. Igual.
- Velocidad: en las ruedas de RANGO las piernas de X son las más rápidas (10,4 pts/min en ES, 11,4 en MES): rango = ida y
  vuelta rápida, no quietud.

### 4.2 Qué tiene un giro que no tiene una pausa (escala X; giro contra pausa, cociente con IC 95 %, días a favor de 6)
| Factor | Giros | Pausas | RR (IC 95 %) | Días | Lectura |
|---|---|---|---|---|---|
| Agotamiento (extremo flaco, ±120 s) | 41,3 % | 13,0 % | 3,18 (2,59-3,90) | 6/6 | se repite, pero usa segundos posteriores |
| Subasta TERMINADA (vela completa) | 63,4 % | 42,2 % | 1,50 (1,36-1,66) | 5/6 | ídem |
| Vela del extremo con delta en contra (completa) | 37,5 % | 26,0 % | 1,44 (1,24-1,68) | 5/6 | ídem; **hasta el extremo: 13,1 vs 14,8 %, nada** |
| Agresión contraria fuerte en los 60 s siguientes | 27,3 % | 8,9 % | 3,05 (2,36-3,95) | 6/6 | reacción |
| Volumen alto en los 60 s previos | 21,9 % | 15,8 % | 1,38 (1,11-1,71) | 5/6 | visible; no sobrevive a la prueba causal (4.3) |
| Absorción real en la punta (60 s previos) | 14,6 % | 13,7 % | 1,07 (0,83-1,37) | 3/6 | igual; contra momentos al azar 1,64 (1,33-2,02) |
| Empuje fuerte hacia el extremo | 20,2 % | 19,5 % | 1,04 | 3/6 | igual (3,45 veces más que al azar: los extremos los hace un empuje) |
| Divergencia de CVD contra el giro previo | 25,6 % | 23,7 % | 1,08 | 3/6 | igual |
| Barrida de una referencia | 64,4 % | 66,7 % | 0,96 | 3/6 | igual (con giros cada 1-3 min casi todo extremo pasa uno previo) |
| Órdenes grandes del lado del empuje en el extremo (±60 s) | 67,5 % | 82,5 % | 0,82 | 0/6 | MENOS en giros: en la pausa el precio vuelve a pasar por ahí |
| Lugar importante (ayer, noche, IB, redondo, semana, mes) | 15,0 % | 16,2 % | 0,92 | 2/6 | igual |
| VWAP y bandas | 7,6 % | 10,3 % | 0,73 (0,53-1,02) | 2/6 | si algo, menos giros |
La micro X/2 repite el mismo cuadro (agotamiento 5,0; subasta terminada 2,56; agresión contraria 2,73; vela en contra
completa 1,46 pero hasta el extremo 0,97; volumen previo 1,20; lugares ≈ 1). La escala grande 4X (n 48/43) no separa nada.
Modelo giro contra pausa (dejando una rueda afuera): AUC 0,72 con "visibles" y 0,75 con reacción, pero lo que pesa es
agotamiento y órdenes grandes en el extremo, los dos rasgos contaminados por segundos posteriores: es circular.

### 4.3 La prueba del escéptico: ¿la firma del giro se puede LEER mientras pasa? (Enmiendas 2 y 3)
Candidatos = todo extremo con retroceso ≥ X/4; se miran a los 15, 30 y 60 s solo los que siguen en pie y sin confirmar, con
datos hasta ese segundo, y se compara P(termina en GIRO) con y sin cada rasgo dentro del mismo tramo de retroceso ya hecho.
- Base: P(GIRO) depende sobre todo de cuánto ya volvió: a los 15 s, 27-31 % si retrocedió < 50 % de X y 50 % si ya retrocedió
  más de la mitad (es casi la geometría de un paseo al azar).
- **Agotamiento: RR 0,88 / 0,90 / 0,93 (0 de 6 ruedas a favor a los 15 s). Subasta terminada: 0,94 / 0,94 / 0,96. La FIRMA
  (agotamiento + subasta terminada + reacción en contra): 0,95 / 0,95 / 1,02.** Leída a tiempo, la firma del footprint NO
  agrega nada: el enriquecimiento de 4.2 era consecuencia del giro (el precio se fue y nadie más operó en el extremo).
- Volumen alto previo 0,92-0,96; absorción previa 0,90-0,96; empuje previo 0,87-0,94; órdenes grandes en el extremo 0,95-1,04.
- ~~Lo único que agrega: el delta de la reacción en contra~~ **CORREGIDO (23:35, con el dato que lo prueba):** controlando
  solo el retroceso MÁXIMO, la vuelta del delta daba RR 1,17 / 1,15 / 1,24 (6, 5 y 6 de 6 ruedas) y el OFI de la reacción
  1,82 / 1,85 / 1,71. Pero el flujo de la reacción va pegado a dónde está el precio AHORA (correlación 0,26-0,30 con el retroceso
  actual). Agregando el retroceso ACTUAL en τ (`r17_04_control_geometria.py`): delta de la reacción RR 0,99-1,00 en la tanda 0
  y 0,90-1,03 en la tanda 1, coeficiente −0,02 a +0,05 (intervalos con el 0); OFI de la reacción 1,26 / 1,12 / 1,04 en la
  tanda 0 y 1,13 / 0,92 / 1,03 en la tanda 1 (a 30 s con signo contrario entre tandas). **Leído a tiempo, el flujo no sabe más
  que el precio:** la chance de que un extremo termine en giro la da casi toda cuánto ya volvió el precio.

### 4.4 Lectura mecánica de la tanda 0 (las 6 ruedas)
- **06-11 (ES, RANGO):** la rueda del día después del derrumbe del 05-11 (−326 pts). Arrancó en 7.392 y subió 77 pts en
  24 min con compradores agresivos (delta +8.542, 321 grandes a favor), giró en 7.469 sin nivel: subasta terminada, extremo
  flaco y la agresión vendedora entró enseguida. Después, 10 piernas de 20-56 pts entre 7.397 y 7.469; en 4 de ellas el
  precio cayó con delta neto ≈ 0 o positivo (bajadas por retiro de compradores pasivos, no por vendedores agresivos). El
  mínimo de las 12:38 en 7.401,25 (al lado del 7.400) con absorción real en el bid, volumen alto y divergencia de CVD; el de
  las 12:53 en 7.397 barrió el 7.400.
- **09-11 (ES, TENDENCIA):** de 7.455,75 a 7.564,5 en 88 min con delta +8.010 y 414 grandes a favor, atravesando PDH, ONH, el
  VAL de la semana y el mínimo del mes, 63 % del recorrido por vacíos del perfil de ayer (y más lento ahí: 2,9 s por tick
  contra 1,1). Giro arriba sin nivel por agotamiento. La segunda subida (11:37-14:42, +60 pts) fue a contramano del delta
  (−6.150): compras pasivas que absorbieron 30 episodios de venta agresiva, los 30 "fallaron" para el vendedor. Máximo del
  día en 7.600,5 (barrió el 7.600 redondo y el giro previo) con absorción real en el ask y divergencia de CVD.
- **11-11 (ES, MIXTA):** bajada inicial de 31,5 pts con vendedores, giro en 7.593,25; después subió 78 pts en 4 horas
  (0,33 pts/min, delta +4.055 pero grandes netos −205, 43 absorciones en contra de las que 36 fallaron) hasta 7.671,5, que es el
  VAL del mes anterior (MVAL 7.671,50: lo tocó justo), con subasta terminada y extremo flaco; bajó 35 pts en las últimas dos
  horas con delta −12.592.
- **23-11 (MES, RANGO por la regla):** apertura con caída de 115 pts en 11 min (10,3 pts/min, vendedores, atravesó PDH, PVAH
  y ONH), giro en 7.167 sin nivel; rebote de 70,75 pts en 10 min con delta +5.293 y 3 apilados de compra; nueva caída a 7.196,5
  (VAH de ayer, giro con subasta terminada); después 117 pts de suba hasta 7.313,5 y un serrucho de 45-90 pts el resto del día.
- **24-11 (MES, MIXTA):** 89 pts de suba en la primera media hora con delta +5.610 atravesando PDL, PVAL y el VAL de la semana;
  giro en 7.240,75 por agotamiento y agresión contraria; los dos máximos principales del día (7.240,75 y 7.238,5) quedaron a
  2 pts, el segundo justo en el PVAL; la bajada de la tarde (55,75 pts en 2 h 18 min) con delta casi cero (−554) y 28
  absorciones en contra, 25 fallidas.
- **25-11 (MES, TENDENCIA):** abrió cayendo 83,5 pts hasta 6.982,25 (giro con delta en contra al llegar, agotamiento), subió
  97 pts con delta +10.830 y 9 apilados de compra, y escalonó hasta 7.213,75 con dos subidas de 131 y 123 pts (delta +8.396 y
  +7.746, 7 y 21 apilados a favor); la bajada de 10:36-11:17 (−56 pts) fue con delta +145: retiro pasivo. Mínimo intermedio en
  7.001,5 (redondo 7.000).

### 4.5 Patrones que se repiten en la tanda 0 (6 de 6 o 5 de 6 ruedas) y su lectura
1. **El delta explica la dirección de la pierna corta mientras pasa** (86-99 % de las piernas de X): leer la ola es leer quién
   agrede ahora; no dice cuánto falta.
2. **Algunas piernas grandes van contra el delta** (60-77 % a favor en 4X de ES en esta tanda; en un año de MES, 90 % a favor:
   es la minoría): ahí manda el libro pasivo.
3. **La absorción en contra de una pierna no la frena** más que cualquier segundo (78-87 % contra 82-88 %).
4. **Un giro y una pausa son iguales en el extremo** (mismo volumen, empuje, absorción, divergencia, barrida y lugar): lo que
   los separa aparece DESPUÉS (subasta terminada, extremo flaco, la otra punta agrediendo). Leído a tiempo, nada del flujo suma
   sobre dónde está el precio (corregido en 4.3).
5. **Los lugares no hacen girar** más de lo que hacen pausar (15 % contra 16 %); algunos giros grandes cayeron justo en un
   nivel (MVAL, PVAL, PVAH, ONL, 7.000, 7.600), la mayoría no.

---
## 5. Tanda 1 — confirmación con el código congelado: 6 ruedas de ES (02, 03, 04, 10, 12 y 13-11), 26-11-2026 23:40 AR

Tipos por la regla de e: 02-11 TENDENCIA (bajista), 04-11 TENDENCIA (alcista), 03, 10, 12 y 13-11 MIXTA. X 4,75-6,75.
Escala X: **642 giros, 567 pausas, 3.173 momentos**; micro: 2.349 giros, 2.020 pausas. Salidas: `r17_02_salida_tanda1.txt`,
`r17_03_salida_tanda1.txt`, `r17_04_salida.txt`; tablas en `tanda1\`.

**Qué se repite (las dos tandas, 12 ruedas):**
| Patrón | Tanda 0 (6 ruedas) | Tanda 1 (6 ruedas) |
|---|---|---|
| Piernas de X con delta a favor | 86-99 % | 91-94 % |
| Piernas de X con OFI de la punta a favor (total / solo pasivo) | 97-100 % / 85-96 % | 99,6-99,8 % / 95-96 % |
| Piernas grandes 4X con delta a favor | ES 60-100 %, MES 89-100 % | 77-94 % |
| Piernas grandes 4X con OFI total a favor | 67-100 % | 85-94 % |
| Absorción en contra de la pierna: siguió X/2 igual (contra segundo al azar) | 78-87 % vs 82-88 % | 82-86 % vs 84-85 % |
| Giro vs pausa: agotamiento (con segundos posteriores) | 41 vs 13 %, 6/6 | 60 vs 23 %, 6/6 |
| Giro vs pausa: subasta terminada (vela completa) | 63 vs 42 %, 5/6 | 60 vs 43 %, 6/6 |
| Giro vs pausa: vela con delta en contra completa / hasta el extremo | 1,44 / 0,89 | 1,70 / 1,16 (IC 0,85-1,60) |
| Giro vs pausa: agresión contraria en los 60 s siguientes | 3,05, 6/6 | 2,28, 6/6 |
| Absorción real en el extremo: giros = pausas, pero contra momentos al azar | 1,07 / 1,64 | 0,89 / 1,53 |
| Empuje fuerte: giros = pausas, contra momentos al azar | 1,04 / 3,45 | 0,88 / 3,27 |
| Lugar importante: giros contra pausas | 0,92 | 1,06 |
| Prueba causal a τ: firma del footprint | 0,95-1,02 | 1,06-1,12 (no estable entre tandas) |
| Prueba causal a τ con el precio actual: delta / OFI de la reacción | 0,99-1,00 / 1,04-1,26 | 0,90-1,03 / 0,92-1,13 |

**Qué NO se repite (visto en una tanda, no en la otra):** volumen alto antes del extremo (giro/pausa 1,38 con 5/6 en la tanda 0;
0,77 con 1/6 en la tanda 1); múltiplo de 50 (0,82 → 1,60, IC 1,00-2,54, 5/6); absorción en la reacción (1,09 → 0,61);
agotamiento leído a τ (0,88-0,93 → 1,10-1,12). Quedan como "vistos una vez".

**Lectura de lo que se repite:**
- El precio se mueve en la dirección de quien VACÍA la punta: por agresión (el delta lo muestra en ~90 % de las piernas
  cortas) o por retiro/recarga pasiva de límites (el OFI pasivo va a favor en 85-96 %). En las piernas grandes que van contra
  el delta (la minoría, ver sección 6), el que manda es el libro pasivo: es lo que el operador ve como "sube con ventas" o
  "baja sin vendedores".
- La absorción real (iceberg en la punta) y el empuje fuerte marcan DÓNDE se forman los extremos (1,5 y 3,3 veces más que en
  un momento cualquiera), pero no CUÁL extremo gira: en giros y pausas aparecen igual. Y absorber contra una pierna no la
  frena.
- La firma del giro en el footprint (subasta terminada, extremo flaco, vela que cierra con delta en contra, la otra punta
  agrediendo) es la DESCRIPCIÓN del giro: aparece porque el precio se fue y nadie volvió a operar ahí. Sirve para leer qué pasó;
  leída a los 15-60 s no cambia la chance de giro más allá de dónde está el precio.
- Los niveles (ayer, noche, IB, VWAP, semana, mes, redondos) no hacen girar más que pausar en ninguna de las dos tandas.

---
## 6. Tanda 3 — prueba amplia con velas de MES de 5 min (263 sesiones con rueda completa, 08-09-2025 a 21-09-2026)

`r17_05_amplia_mes5.py` (salida `r17_05_salida.txt`, datos en `tanda3\`). Sin cinta ni footprint por precio: zigzag sobre el
camino o-l-h-c / o-h-l-c de cada vela, delta por vela de ATAS, perfil con el volumen repartido parejo, niveles anulados
alrededor de los 5 rolls (el continuo no ajusta). 4 bloques cronológicos de ~66 sesiones.
- **Piernas grandes 4X (n 1.341, mediana 32 pts): delta a favor 90,1 %** (bloques 88 / 92 / 89 / 92 %; tendencia 91, rango 90,
  mixta 89 %). Las piernas LENTAS (más velas que la mediana) van con el delta el 96,7 %; las rápidas 83,1 % (en velas de 5 min
  la pierna corta queda mal medida en los extremos). Escala 2X (n 4.711, 17 pts): 78,3 % (lentas 88,8 %, rápidas 62,0 %).
  **Corrección a la tanda 0:** las piernas grandes contra el delta existen pero son la minoría (~1 de cada 10 en un año de MES);
  en las 12 ruedas de ES fueron 4 de 18 y 4 de 31.
- **Lugares, giro contra pausa (4X: 1.180 giros, 956 pausas):** ayer 13,2 vs 13,1 % (RR 1,01), noche 1,18 (0,83-1,66), IB 1,19
  (0,80-1,77), VWAP ± bandas 0,93, redondo de 50 1,12 (0,81-1,56), semana anterior 1,23 (0,87-1,74; bloques −1,0 +3,5 −0,2
  +4,8 pp), mes anterior 0,88 (0,50-1,54). En 2X: ayer 1,13 (1,00-1,29; +1,5 +1,1 +3,9 −0,3 pp), el resto 0,87-1,25 con el 1
  adentro. **En un año de datos los niveles no hacen girar más que pausar**; lo de ayer en 2X es una pista de 1,5 puntos.
- **Vela del extremo con delta en contra** (vela completa, descriptivo): 45,0 vs 35,7 % en 4X (RR 1,26; los 4 bloques +7 a +13
  pp) y 43,1 vs 26,4 % en 2X (RR 1,63; +12 a +20 pp). Repite la firma descriptiva de 4.2 en 263 sesiones.

---
## 7. Respuesta a las preguntas del pedido (con lo medido en esta ronda)

**(1) Dirección.** Ningún factor del order flow, leído a tiempo, dice hacia dónde va a ir el precio más allá de dónde ya está:
en el extremo, giro y pausa son iguales; a los 15-60 s, con el precio actual en la cuenta, delta y OFI de la reacción no suman
(RR 0,90-1,03 y 0,92-1,26 cambiando de signo entre tandas). Lo que el flujo SÍ hace es explicar la dirección MIENTRAS pasa
(86-99 % de las piernas cortas van con el delta, 97-100 % con el OFI de la punta; 90 % de las grandes en un año de MES).
**(2) Tamaño.** No se midió aparte en esta ronda; la geometría sí quedó medida: con ≥ 50 % de X ya retrocedido, la mitad de los
extremos termina en giro de X (49,6-51,0 % en las dos tandas); con menos, 24-31 %. Para el tamaño del día vale lo del baúl
(rango previo + hora; rango ≈ 1,86 straddles; ATR × franja).
**(3) No entrar.** Nada de lo medido marca un "no entres" con información propia: la absorción en contra no frena la pierna
(igual que el azar en 3 muestras), los niveles no hacen girar, y la firma del giro llega cuando el giro ya pasó.
**(4) Fusiones.** La única con sustento como LUGAR es "zona de extremo" = absorción real + empuje fuerte (marcan extremos 1,5 y
3,3 veces más que el azar en las dos tandas), pero no separa giro de pausa; le falta una pieza de MOMENTO que esta ronda no
encontró. FIRMA (agotamiento + subasta terminada + reacción) a τ: 0,95-1,02 en la tanda 0 y 1,06-1,12 en la 1: vista una vez.
Lugar + momento (divergencia, absorción o firma en nivel): los niveles no aportan en ninguna tanda.
**(5) Qué mostrar en el indicador (para LEER, no para gatillar; ninguna estadística promete ganancia):**
- **La ola en curso:** dirección de la pierna (zigzag X en vivo, X = 0,08 × mediana del rango de 10 ruedas), su delta acumulado
  y si el precio va CON la agresión o CONTRA ella ("pierna pasiva"). Al lado: "las piernas van con su delta 86-99 % (ES/MES,
  12 ruedas) y 90 % de las grandes en un año de MES: describe quién manda ahora, no cuánto falta".
- **El retroceso desde el último extremo en % de X**, con la tabla medida al lado: "con < 50 % de X, 24-31 % de los extremos
  terminan en giro; con ≥ 50 %, ~50 %". Es lo único que cambia la chance de giro a tiempo, y es geometría del precio.
- **La firma del giro como lectura de lo que pasó** (subasta terminada, extremo flaco, la otra punta agrediendo), rotulada:
  "describe el giro cuando ya ocurrió; leída a 15-60 s no cambia la chance".
- **Absorción en contra de la pierna**, con: "siguió 78-87 % de las veces, igual que cualquier segundo: no frena".
- Nada de flechas LARGO/CORTO: el semáforo queda en ESPERAR por defecto (PRUEBA_EN_VIVO.md describe cómo medirlo hacia
  adelante si el operador lo quiere).
