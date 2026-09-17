# Familia "escalas": temporalidad y delta por tamaño de orden

Pedido del operador (17-09): un complemento / gatillo que acompañe al CVD. Esta familia responde dos preguntas suyas:
"¿cambiando la temporalidad mejora?" y "¿el delta de los chicos contra el de los grandes dice algo?".

Datos: tabla de 1 segundo de MNQ (base.py), 20 sesiones. Se explora en las 12 anteriores al 08-09 y se confirma UNA vez en las 8 restantes.

---

## 1. PRE-REGISTRO (escrito ANTES de correr cualquier resultado)

Fecha y hora: 17-09-2026, antes de calcular ninguna barrera. Lo unico mirado hasta aca: estructura de las tablas (filas, volumen, saltos de
precio, spread). Ningun resultado hacia adelante.

### Notacion (todo causal: en el segundo t solo entran filas <= t)

- `S_W(x)[t]` = suma de x en los W segundos que terminan en t (incluye t).
- `D_W` = S_W(delta), `V_W` = S_W(vol), `r_W` = D_W / V_W (desbalance: que fraccion del volumen fue neta compradora; sin escala, aguanta dias de poco volumen).
- `ret_W[t]` = ultimo[t] - ultimo[t-W].
- `chicos` = d1 + d2_4 (ordenes de 1 a 4 contratos). `grandes` = d10_49 + d50 (10 o mas). d5_9 queda afuera como banda neutra.
- `rs_W` = S_W(chicos) / V_W ; `rl_W` = S_W(grandes) / V_W.
- "qNN" = percentil NN del valor absoluto del rasgo, calculado SOLO en las sesiones de exploracion, en rueda sin los 2 primeros minutos
  (solo el rasgo, sin mirar resultados). El numero queda congelado en `escalas_umbrales.json` y se usa igual en confirmacion.
- "Arranque fresco" = la condicion es verdadera en t y fue falsa en los 60 segundos anteriores (un episodio dispara una sola vez; garantiza >= 61 s entre disparos).
- Variantes por cierre de vela: disparan en el ultimo segundo de la vela, con refractario de 60 s (gana el primero).
- Entrada = ultimo[t]. Resultado = B.barrera de t+1 en adelante. Solo rueda 13:32:00 a 20:00:00 UTC.

### Las 12 variantes

Temporalidad (el delta extremo de una vela, ¿sigue?):
1. **T15s** — velas de reloj de 15 s. Dispara si |r_15| >= q95 y V_15 >= q25. Lado = signo del delta (continuacion).
2. **T5m** — velas de reloj de 5 min. Dispara si |r_300| >= q80 y V_300 >= q25. Lado = signo del delta.

Alineacion de escalas (contexto de 15 min + gatillo chico):
3. **A_align** — todas las escalas de acuerdo: S = signo(D_900) con |r_900| >= q50; signo(D_300) = signo(D_60) = signo(D_15) = S, cada una con |r| >= su q50. Arranque fresco. Lado = S.
4. **A_noturn** — tendencia + retroceso, SIN esperar el giro: S = signo(D_900), |r_900| >= q50; ret_120·S <= -4 puntos; D_60·S < 0. Arranque fresco. Lado = S.
5. **A_full** — tendencia + retroceso que se agota + giro de 15 s: S = signo(D_900[t]), |r_900[t]| >= q50; en t-15: ret_120·S <= -4 y D_60·S < 0; en t: D_15·S > 0 y ret_15·S > 0. Arranque fresco. Lado = S.
   (4 y 5 juntas miden cuanto aporta esperar el giro.)

Delta por tamaño (chicos contra grandes, ¿quien tiene razon?):
6. **S1m** — divergencia en 60 s: signo(rs_60) = -signo(rl_60), |rs_60| >= q50 y |rl_60| >= q50. Arranque fresco. Lado = signo de los GRANDES.
7. **S5m** — lo mismo con 300 s.
8. **S15m** — lo mismo con 900 s.
9. **L5m** — grandes solos: |rl_300| >= q90. Arranque fresco. Lado = signo de los grandes.

Esfuerzo sin resultado en marco mayor:
10. **E5m** — |r_300| >= q80 y ret_300·signo(D_300) <= 0 (mucho delta en 5 min y el precio no fue). Arranque fresco. Lado = CONTRA el delta (gana el pasivo).

Velas que no son de tiempo:
11. **VB** — velas por volumen de Vb contratos (Vb = mediana del volumen de 1 min en rueda de exploracion, redondeada a 100), acumulando desde 13:30:00.
    Cierra en el primer segundo en que el acumulado cruza un multiplo de Vb. Dispara si |delta/vol de la vela| >= q90. Lado = signo del delta. Refractario 60 s.
12. **RB** — velas por rango de 8 puntos (desde 13:30:00; cierra cuando maximo - minimo de la vela >= 8; la siguiente abre en el cierre anterior).
    Dispara si la vela cerro en una direccion con el delta en contra: signo(delta) = -direccion y |delta/vol| >= q50. Lado = la direccion del PRECIO. Refractario 60 s.

### Regla para elegir finalistas (fijada ahora)

- Se juzga con la barrera principal **+-8 puntos en 600 s** (empata con 55,3 %), solo en exploracion.
- Elegible: n resueltos >= 150, z >= 2,0 contra 50 % y >= 70 % de los dias arriba de 50 %.
- Finalistas: como mucho 2 elegibles, los de mayor z. Desempate: el que ademas quede >= 50 % en las dos barreras secundarias (+-5/300 s y +-12/900 s).
- Lectura inversa: si una variante da z <= -2,5 con n >= 150 y <= 30 % de dias arriba, su INVERSA (lado opuesto) es elegible y se cuenta como una variante mas en el total probado.
- Si ninguna es elegible: igual corro en confirmacion las 2 de mayor |z| con n >= 150, solo para dejar registro; en ese caso su veredicto maximo es PISTA.
- Si por conteo (sin mirar resultados) alguna variante no puede llegar a n >= 150, lo anoto aca abajo ANTES de correr resultados y ajusto solo el percentil.
- Ademas de z reporto un t por dias (media de aciertos diarios contra 50 %), porque disparos cercanos comparten resultado y el z simple exagera.

### Estudio descriptivo (no es gatillo, no elige nada; solo exploracion hasta que termine la confirmacion)

- D1. Por marco (5 s, 15 s, 30 s, 1, 2, 5, 15 y 30 min): correlacion delta~retorno de la MISMA vela, delta~retorno de la SIGUIENTE, y % de veces que la vela siguiente va para el lado del delta.
- D2. Lo mismo en velas por volumen y por rango.
- D3. Por tamaño de orden (d1, d2_4, d5_9, d10_49, d50) en 1, 5 y 15 min: que parte del delta aporta cada grupo, correlacion con la misma vela y con la siguiente.

---
### Ajustes por conteo (hechos ANTES de mirar ningun resultado; solo se toco el percentil, como decia la regla)

Conteo de disparos en exploracion con los umbrales originales: T5m daba 141 y E5m daba 16 (no podian llegar a n >= 150).
- **T5m**: el percentil pasa de q80 a **q70** (|r_300| >= 0,0661) -> 205 disparos.
- **E5m**: el percentil pasa de q80 a **q50** (|r_300| >= 0,0450) -> 208 disparos. Dato que ya es aprendizaje: con el delta de 5 min en su 20 % mas
  extremo, que el precio NO haya ido para ese lado paso solo 16 veces en 12 ruedas. A 5 minutos, delta fuerte y precio quieto casi no existe.

Umbrales congelados (escalas_umbrales.json): T15s |r_15| >= 0,360 con V_15 >= 433; T5m |r_300| >= 0,0661 con V_300 >= 10242; medianas
|r_15| 0,127, |r_60| 0,083, |r_300| 0,045, |r_900| 0,027; chicos/grandes: |rs_60| 0,019 / |rl_60| 0,065, |rs_300| 0,010 / |rl_300| 0,038,
|rs_900| 0,007 / |rl_900| 0,025; L5m |rl_300| >= 0,092; Vb = 3000 contratos, VB |r| >= 0,237; RB |r| >= 0,119.

Disparos en exploracion (sin resultados): T15s 621, T5m 205, A_align 565, A_noturn 499, A_full 499, S1m 932, S5m 308, S15m 174, L5m 276, E5m 208, VB 493, RB 503.

---

## 2. EXPLORACION (12 sesiones, 20-08 al 04-09) — escalas_1_explorar.py

Empates: +-8 -> 55,3 % | +-5 -> 58,5 % | +-12 -> 53,5 %. "t dias" = t de los aciertos diarios contra 50 % (mas honesto que z cuando los disparos se pisan).

Barrera principal +-8 puntos / 600 s:

| variante | disparos | n resueltos | acierto % | z | t dias | dias > 50 % | peor dia % |
|---|---|---|---|---|---|---|---|
| T15s | 621 | 621 | 48,6 | -0,68 | -0,65 | 4 de 12 | 37,0 |
| T5m | 205 | 204 | 43,6 | -1,82 | -1,66 | 2 de 12 | 16,7 |
| A_align | 565 | 562 | 48,8 | -0,59 | -0,29 | 7 de 12 | 29,8 |
| A_noturn | 499 | 496 | 53,2 | 1,44 | 0,62 | 7 de 12 | 35,7 |
| A_full | 499 | 497 | 53,1 | 1,39 | 1,04 | 7 de 12 | 41,7 |
| S1m | 932 | 929 | 50,5 | 0,30 | -0,61 | 6 de 12 | 25,0 |
| S5m | 308 | 306 | 50,7 | 0,23 | 0,26 | 5 de 12 | 33,3 |
| **S15m** | 174 | 173 | **58,4** | **2,20** | 0,48 | **9 de 12** | 0,0 |
| L5m | 276 | 275 | 52,0 | 0,66 | 1,36 | 8 de 12 | 40,9 |
| E5m | 208 | 207 | 53,6 | 1,04 | 0,29 | 8 de 12 | 23,8 |
| VB | 493 | 492 | 49,8 | -0,09 | -0,33 | 8 de 12 | 36,0 |
| RB | 503 | 501 | 51,1 | 0,49 | 1,26 | 8 de 12 | 43,2 |

Secundarias (acierto % / z / dias): 
+-5/300 s: T15s 51,0/0,48/5 · T5m 43,6/-1,83/3 · A_align 49,7/-0,13/6 · A_noturn 53,7/1,66/8 · A_full 49,8/-0,09/4 · S1m 51,1/0,69/8 · S5m 50,0/0,00/5 · S15m 52,0/0,53/5 de 11 · L5m 55,6/1,87/9 · E5m 54,3/1,25/7 · VB 50,1/0,05/7 · RB 54,9/2,19/9.
+-12/900 s: T15s 47,5/-1,22/4 · T5m 50,0/0,00/4 · A_align 48,8/-0,55/5 · A_noturn 55,1/2,25/8 · A_full 52,7/1,22/7 · S1m 52,5/1,52/8 · S5m 50,7/0,23/6 · S15m 55,9/1,53/8 · L5m 52,4/0,79/7 · E5m 52,2/0,63/6 · VB 50,1/0,05/6 · RB 49,3/-0,31/7.

Movimiento medio firmado a 10 / 30 / 60 s (puntos, a favor del lado del disparo):
T15s -0,09/-0,16/-0,45 · T5m -0,43/-0,47/-0,73 · A_align -0,30/-0,53/-0,34 · A_noturn +0,30/+0,54/+0,65 · A_full -0,29/0,00/+0,17 · S1m -0,03/+0,12/+0,28 ·
S5m -0,36/-0,16/-0,25 · S15m -0,52/-0,28/+0,29 · L5m +0,03/+0,23/-0,10 · E5m +0,11/-0,15/+0,26 · VB -0,47/-1,19/-1,72 · RB +0,24/+0,70/+0,63.

### Eleccion de finalistas (aplicando la regla escrita arriba, sin tocarla)

- Unica elegible: **S15m** (n 173, z 2,20, 9 de 12 dias = 75 %). Ninguna otra llega a z >= 2,0 en la barrera principal.
- Ninguna inversa elegible (la mas negativa es T5m con z -1,82; la regla pedia <= -2,5).
- Finalista unico: **S15m**. Aviso previo y honesto: su t por dias es 0,48 y tuvo un dia en 0 %; con 12 variantes probadas, una en z 2,2 es lo que se espera
  por puro azar (probabilidad de ~1 en 6 de que aparezca al menos una). No espero que repita; la confirmacion lo dira.
- A_noturn (53,2 %, z 1,44; en +-12 z 2,25) NO cumple la regla y NO va a confirmacion como finalista.

---

## 3. CONFIRMACION (8 sesiones, 08-09 al 17-09) — escalas_2_confirmar.py, corrida UNA vez

Finalista unico: **S15m** (en los ultimos 15 min los grandes netean para un lado y los chicos para el otro, ambos arriba de su mediana; arranque fresco; lado = los grandes).

| barrera | n | acierto % | z vs 50 | placebo (200 sorteos) | z vs placebo | empate % | neto por operacion |
|---|---|---|---|---|---|---|---|
| +-8 / 600 s | 124 | 55,6 | 1,26 | 53,8 +- 4,4 | **0,42** | 55,3 | +0,05 pts |
| +-5 / 300 s | 124 | 51,6 | 0,36 | 52,0 +- 4,6 | -0,07 | 58,5 | -0,69 pts |
| +-12 / 900 s | 118 | 50,0 | 0,00 | 55,7 +- 4,3 | -1,32 | 53,5 | -0,85 pts |

Dias arriba de 50 % (+-8): 7 de 8 (56, 57, 27, 56, 64, 52, 73, 54 %; ~15 disparos por dia). t por dias 1,07.

**Veredicto S15m: NADA.** Pide acierto >= 57,3 % y dio 55,6; pide z >= 2,5 contra placebo y dio 0,42. Repite el signo (58,4 -> 55,6 %) pero
neto de costos es cero (+0,05 puntos por operacion) y no se distingue de entrar al azar con el mismo lado. Falla dos criterios, no uno.

**Veredicto de la familia: NADA.** Ni cambiar la temporalidad ni separar el delta por tamaño de orden da un gatillo.

---

## 4. POST-HOC (despues de cerrar la confirmacion; NO cambia ningun veredicto, NO elige nada)

### 4a. Las 12 variantes en confirmacion, solo para ver si lo de exploracion repite — escalas_4_posthoc.py

Acierto +-8/600 s, exploracion -> confirmacion:
T15s 48,6 -> 49,3 · T5m 43,6 -> 37,8 · A_align 48,8 -> 49,0 · A_noturn 53,2 -> **45,0** · A_full 53,1 -> **42,9** · S1m 50,5 -> 48,2 · S5m 50,7 -> 51,9 ·
S15m 58,4 -> 55,6 · L5m 52,0 -> 51,3 · E5m 53,6 -> 48,6 · VB 49,8 -> 52,8 · RB 51,1 -> 49,0.

Lectura: la hipotesis literal del operador (CVD de 15 min a favor + retroceso de 1-2 min + giro de 15 s) paso de 53 % a 43-45 %: se dio vuelta.
Eso es ruido puro, y es la razon por la que se pre-registra. Esperar el giro de 15 s no aporto nada en ninguna de las dos muestras.

### 4b. Una pista fuera de protocolo: ir CONTRA el delta extremo de la vela de 5 min — escalas_5_t5m_inversa.py

T5m estaba registrada como continuacion. Dio al reves en las dos muestras, pero en exploracion su z fue -1,82 y la regla pedia <= -2,5 para habilitar la inversa:
**no fue finalista y no tiene veredicto.** Se anota para pre-registrarla contra sesiones NUEVAS (posteriores al 17-09).

Regla: al cierre de cada vela de reloj de 5 min (13:32-20:00 UTC), si |delta/vol| de la vela >= 0,0661 y vol >= 10242 -> entrar CONTRA el signo del delta.

| muestra | n | acierto +-8 % | z vs 50 | t dias | dias > 50 % | neto +-8 | +-5 % | +-12 % |
|---|---|---|---|---|---|---|---|---|
| exploracion | 204 | 56,4 | 1,82 | 1,66 | 9 de 12 | +0,17 | 56,4 | 50,0 |
| confirmacion (post-hoc) | 111 | 62,2 | 2,56 | 1,47 | 5 de 8 | +1,10 | 56,8 | 60,9 |
| las 20 juntas | 315 | 58,4 | 2,99 | 2,24 | 14 de 20 | +0,49 | 56,5 | 53,8 |

Intervalo de 95 % remuestreando DIAS (20 sesiones, +-8): 51,9 a 65,0 %. Incluye el empate (55,3 %): todavia puede ser cero neto.
Por franja (+-8, exploracion / confirmacion): 13:30-15:00 UTC 52,5 % / 52,6 % (la apertura no sirve); 15:00-18:00 57,6 % / 67,7 %; 18:00-20:00 56,5 % / 62,5 % (n 8).
Si hubiera sido finalista igual habria quedado en PISTA (5 de 8 dias, pedia 6). Es coherente con lo que el proyecto ya sabia ("extremo = no perseguir"),
pero salio de mirar 12 variantes por los dos lados: hay que verla repetir en datos que todavia no existen antes de creerle.

### 4c. El placebo "misma media hora, mismos lados" esta sesgado — escalas_6_sesgo_placebo.py

Medido en las 20 sesiones, +-8/600 s, con el lado pre-registrado de cada señal:
- T5m (continuacion): placebo misma media hora 55,5 % | sorteando solo segundos ANTERIORES al disparo 55,2 % | solo POSTERIORES 50,1 %.
- S15m (seguir a los grandes): 55,8 % | solo anteriores 58,7 % | solo posteriores 49,7 %.

Causa: la mitad de los segundos sorteados cae ANTES del disparo, adentro del movimiento que genero la señal, y el lado de la señal ya "sabe" como termino
ese movimiento. Consecuencia para todas las familias: a una señal de CONTINUACION el placebo le sube la vara (~55 %) y a una señal de FADE se la baja (~45 %,
infla su z en unos 2 desvios). El z contra placebo de una señal de fade hay que leerlo contra 50 %, o sortear solo segundos posteriores al disparo.

### 4d. Velocidad

Segundos hasta tocar +-8 (mediana, 20 sesiones), señal contra azar de la misma media hora: T15s 65/64 · T5m 38/43 · A_align 52/53 · A_noturn 49/52 · A_full 48/51 ·
S1m 58/58 · S5m 55/58 · S15m 50/55 · L5m 59/65 · E5m 82/79 · VB 24/28 · RB 17/22. El delta extremo tampoco avisa que viene mas velocidad que la normal de esa media hora.
Dato de contexto: en rueda, un +-8 de MNQ se resuelve en alrededor de UN minuto de mediana.

---

## 5. DESCRIPTIVO — escalas_3_descriptivo.py (explorar / confirmar / todas; los tres dicen lo mismo)

### ¿En que marco el delta deja de ser pura descripcion? En ninguno. (20 sesiones, rueda)

| marco | velas | delta ~ MISMA vela | delta ~ vela SIGUIENTE | +-2 errores | la siguiente va para el lado del delta |
|---|---|---|---|---|---|
| 5 s | 89776 | +0,64 | -0,017 | 0,007 | 49,1 % |
| 15 s | 29900 | +0,74 | -0,021 | 0,012 | 49,2 % |
| 30 s | 14930 | +0,79 | -0,051 | 0,016 | 47,9 % |
| 1 min | 7445 | +0,82 | -0,033 | 0,023 | 49,5 % |
| 2 min | 3703 | +0,85 | -0,049 | 0,033 | 50,2 % |
| 5 min | 1458 | +0,88 | -0,048 | 0,052 | 48,3 % |
| 15 min | 460 | +0,88 | +0,046 | 0,093 | 53,8 % |
| 30 min | 211 | +0,88 | -0,062 | 0,138 | 43,7 % |
| por volumen (3000) | 9158 | +0,79 | -0,006 | 0,021 | 51,9 % |
| por rango (8 pts) | 22047 | +0,62 | -0,007 | 0,013 | 49,5 % |

- Agrandar el marco hace al delta MAS parecido al precio de esa misma vela (0,64 -> 0,88), no mas adelantado.
- La vela siguiente: cero o apenas EN CONTRA (-0,02 a -0,05 entre 5 s y 5 min, negativo en 11 a 17 de 20 dias). No es rebote entre bid y ask: con el precio
  medio da igual (-0,014 / -0,019 / -0,050 / -0,032 / -0,049 / -0,048). Es una reversion real pero minuscula: explica 0,1-0,25 % de la vela siguiente.
- Velas por volumen o por rango: lo mismo. Cambiar el tipo de vela no cambia nada.

### Delta por tamaño de orden (20 sesiones; en exploracion y confirmacion por separado da igual)

Velas de 1 min: parte del |delta| que aporta cada grupo y correlacion con la misma vela / con la siguiente:
1 contrato 8 % (+0,09 / -0,02) · 2-4 11 % (+0,42 / -0,02) · 5-9 13 % (+0,52 / -0,02) · 10-49 **37 %** (**+0,79** / -0,03) · 50 o mas 30 % (+0,56 / -0,02).

- El CVD lo escriben las ordenes de 10 contratos o mas: dos tercios del delta y casi toda la relacion con el precio.
- Las ordenes de 1 contrato van A CONTRAMANO del movimiento en marcos largos: correlacion con la propia vela -0,17 a 5 min y -0,27 a 15 min, y contra el delta
  de los de 50+ -0,29 a 5 min y -0,46 / -0,51 a 15 min. El chico compra la caida y vende la suba mientras pasa.
- ¿Quien tiene razon despues? NINGUNO. Cuando chicos y grandes tiran opuesto (39 % de las velas de 1 min, 45 % de las de 5, ~55 % de las de 15), la MISMA vela va con
  los grandes 67 % / 77 % / 82 %, pero la SIGUIENTE va con los grandes 50,5 % / 47,2 % / 46-53 %. Los grandes SON el movimiento; no lo anticipan.
- Regresion de la vela siguiente sobre los cinco grupos: R2 0,001 (1 min), 0,004 (5 min), 0,01-0,05 (15 min, 190-270 velas: ruido).

---

## 6. QUE SE LLEVA EL OPERADOR

1. **Cambiar la temporalidad no mejora el CVD como gatillo.** En 15 o 30 min el CVD es un retrato mas nitido del precio, no un pronostico. Ni 15 s, ni 5 min,
   ni velas por volumen, ni por rango.
2. **Separar chicos de grandes cambia el dibujo, no el futuro.** El CVD ya es, de hecho, el CVD de los grandes. El de 1 contrato es un espejo invertido del precio.
3. **Alinear escalas llega tarde.** "Todo alineado" acerto 48,8 % / 49,0 %. Tendencia de 15 min + retroceso + giro: 53 % en una muestra, 43 % en la otra.
4. **Extremo de delta = no perseguir**, con numeros: despues de una vela de 5 min muy cargada para un lado, el siguiente +-8 fue en contra 58 % de las veces
   (pista sin validar). Despues de una vela de volumen con delta extremo el precio devolvio en promedio 1,7 pts (exploracion) y 0,7 pts (confirmacion) en 60 s.
5. **Ocho puntos de MNQ son un minuto de ruido.** Mediana de ~1 min para tocar +-8 en rueda, con señal o sin ella.

Archivos: escalas_lib.py (rasgos y disparos), escalas_0_umbrales.py, escalas_umbrales.json (umbrales congelados), escalas_1_explorar.py, escalas_2_confirmar.py,
escalas_3_descriptivo.py, escalas_4_posthoc.py, escalas_5_t5m_inversa.py, escalas_6_sesgo_placebo.py.
