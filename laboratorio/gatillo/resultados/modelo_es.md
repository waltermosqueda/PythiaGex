# Familia MODELO_ES — el unico resultado positivo previo (gatillo "modelo·es10"), juzgado con dias nuevos

Fecha: 17-09-2026. Instrumento: MES, velas de 2 minutos (el grafico donde corre el gatillo). Scripts: `modelo_es_lib.py`, `modelo_es_00_forma.py`, `modelo_es_01_corridas.py`, `modelo_es_02_juicio.py` (el juicio, corrida unica) y las auditorias posteriores `modelo_es_03_porque_difiere.py`, `modelo_es_04_arranques.py`, `modelo_es_05_titular.py`, `modelo_es_06_sensibilidad.py`, `modelo_es_07_sin_calentar.py`.

## 0. PRE-REGISTRO (escrito ANTES de mirar ningun desenlace)

Lo unico que se miro antes de escribir esto fue la FORMA de los datos (`modelo_es_00_forma.py`, `modelo_es_01_corridas.py`): formato del registro, cuantas velas y cuantos disparos hay por dia, y si el precio del disparo calza con la vela. Ningun acierto, ningun desenlace.

### Que es exactamente lo que se juzga (leido del codigo, no de memoria)
- El gatillo que corre EN VIVO en el grafico de MES de 2 min NO es el modelo del "61 % / 83 %". Hay dos modelos congelados en `GatilloModelo.cs`:
  - **M1** (`modelo_MES_10min.json`): velas de 1 min, +3/-3 en 10 velas, ajustado con 13 dias (19-08 al 04-09). Es el del titular: 61 % con p >= 0,70 y 83 % de 59 en la tarde.
  - **M2** (`modelo_MES_M2_5velas.json`): velas de 2 min, +3/-3 en 5 velas (10 min), ajustado con 16 dias (19-08 al **10-09** inclusive). Lo medido el 10-09 para este fue mas flojo: 62 % de 53 y tarde 76,7 % de 30. **Este es el que dispara en el grafico del operador** (el indicador elige los parametros por el marco del grafico).
- No hay velas de 1 min de MES con niveles posteriores al 08-09 (el rebobinado M1 de MES se dejo de escribir ese dia): **el modelo M1 no se puede juzgar fuera de muestra con lo que hay**. Se juzga el M2, que es ademas el que el operador ve.
- Fuera de muestra verdadero para M2 = **11-09, 14-09, 15-09, 16-09 y 17-09** (5 ruedas; la del 17-09 llega hasta las 19:38 UTC). Los 3 disparos en vivo del 10-09 caen en un dia que el ajuste ya habia visto: se informan aparte y no cuentan.
- El indicador corre en modo `SoloTarde` (14-16 h de Nueva York = 18:00-20:00 UTC), umbral 0,70 (largo si p >= 0,70, corto si p <= 0,30), enfriamiento 5 velas. El enfriamiento corre ANTES del filtro de la tarde.

### Formato del registro (punto 1 del encargo)
- `pythiagex-gatillos-MES-TimeFrame-M2.jsonl` (vivo, se agrega): una linea por disparo: `t` (UTC, hora del ULTIMO trade de la vela cerrada: 18:01:59 = vela 18:00-18:02), `tipo` ("modelo·es10" entre otros gatillos), `lado` (+1/-1), `precio` (cierre de esa vela = entrada), `dom` (para el modelo: el zero gamma vigente, 0 si no habia), `dz` (para el modelo: **la p**), `fuente` "vivo".
- `pythiagex-gatillos-archivo-MES-TimeFrame-M2.jsonl` (se PISA en cada rebobinado: solo queda el ultimo recorrido): igual, pero `t` es la APERTURA de la vela siguiente (18:04:00 = vela 18:02-18:04).
- `pythiagex-centinela-rebobinado-atas-MES-TimeFrame-M2.jsonl`: una linea por vela y por CORRIDA del rebobinado (140 corridas apiladas, 200 MB): o/h/l/c, vol, ops, delta y `niv` (zero_vol, mp_vol, mn_vol, dom0, dom1, mc30...). **Trampa encontrada:** en la semana del roll conviven corridas con el grafico en U6 y en Z6 (67,75 puntos de diferencia). "La ultima escritura de cada vela gana" (lo que hace `gatillo_cientifico.cargar`) mezcla velas de los dos contratos entre el 13-09 22:00 y el 14-09: 204 saltos falsos de 67 puntos. Aca se toma, por dia, UNA sola corrida entera.
- Conteo del registro en vivo (forma, sin desenlaces): 23 disparos modelo·es10, **todos CORTOS**: 10-09: 3, 11-09: 2, 14-09: 3 (con el grafico en U6), 15-09: 7, 16-09: 0, 17-09: 8. Fuera de muestra: **20**. Es poco: se reconstruye.

### Definiciones
- Vela = apertura (piso de 2 min). Desenlace: entrada = cierre de la vela del disparo; se miran las 5 velas de 2 min siguientes, consecutivas; +3 a favor antes que -3 en contra. Las dos en la misma vela = NO resuelto. Ninguna = no resuelto. Acierto = aciertos / resueltos.
- Costo (SUPUESTO, no medido hoy): MES 1 tick de spread (0,25) + comision ~0,24 pts = 0,49 pts. Empate con +-3: **58,2 %**.
- Reconstruccion: la p del modelo M2 congelado calculada como `GatilloModelo.cs` (ventana movil de 60 velas procesadas, sin corte por dia; rt = mediana del rango; dz = delta / desvio de las previas; niveles `zero_vol`, `mp_vol`, `mn_vol`, dominante mas cercana arriba y abajo entre dom0/dom1, `mc30`), sobre las velas y los niveles que dejo el rebobinado. Por cada dia se usa **la ULTIMA corrida que cubre la rueda entera de ese dia** (17-09: la ultima corrida). Sensibilidad: la PRIMERA corrida completa de cada dia (la mas cercana al vivo de ese dia).
- Placebo: por cada disparo, otra vela al azar del MISMO dia y la MISMA media hora, con el MISMO lado; 200 sorteos; z = (acierto - media del placebo) / desvio del placebo. Tasa base: % de las velas de esa franja y esos dias en que gana ese mismo lado.
- Ventanas: TARDE = 18:00-20:00 UTC (14-16 h NY). RUEDA = 13:32-20:00 UTC.

### Las mediciones (todas sobre 11-09 al 17-09; las mismas sobre 19-08 al 10-09 solo como control de que la reconstruccion reproduce lo que se midio el 10-09)
| # | nombre | definicion |
|---|---|---|
| A | VIVO | los 20 disparos reales del registro en vivo (tarde, umbral 0,70, enfriamiento 5) |
| R1 | reconstruido tarde | modelo congelado, umbral 0,70, enfriamiento 5, tarde (lo mismo que corre en vivo) |
| R2 | reconstruido rueda | idem, toda la rueda |
| R3 | reconstruido manana | idem, 13:32-18:00 UTC |
| R4 | tarde sin enfriamiento | cada vela con p extrema cuenta (asi midio el laboratorio el 10-09; el n esta inflado por solapamiento) |
| R5 | rueda sin enfriamiento | idem, toda la rueda |
| R6 | umbral 0,65 tarde | con enfriamiento 5 |
| R7 | umbral 0,65 rueda | con enfriamiento 5 |
| R8 | AUC | AUC de la p contra "sube primero" en todas las velas resueltas: rueda y tarde (lo medido en M1 fue 0,57) |
| E | equivalencia | cuantos de los 20 disparos en vivo reaparecen (misma vela, mismo lado) en R1, y diferencia de p |

### Finalistas y veredicto (fijado ahora)
- Finalista 1 = **R1** (es el gatillo tal cual esta instalado), con A como control de la vida real. Finalista 2 = **R2** (el titular "61 % con p >= 0,70" era de todo el dia).
- SOBREVIVE: acierto >= 60,2 % (empate + 2), z >= 2,5 contra placebo, >= 4 de 5 dias arriba de 50 % (equivalente a 6 de 8), n >= 100 resueltos.
- PISTA: acierto >= empate y z >= 1,5 contra placebo pero falla n o dias. NADA: lo demas.
- La frase pedida: "se sostiene" = le gana al placebo con z >= 2 y el acierto cae dentro del intervalo de lo medido el 10-09 (62 % rueda / 77 % tarde); "se achica" = le gana al placebo (z >= 1,5) con menos acierto; "desaparecio" = no se distingue del placebo.
- Nada se retoca despues de correr `modelo_es_02_juicio.py`.

---

## 1. RESULTADOS (corrida unica de `modelo_es_02_juicio.py`, 17-09-2026; salidas crudas en `modelo_es_cache/*.txt`)

Fuente de todo: archivos del propio ATAS del operador leidos hoy 17-09 (~20:45 UTC). Velas y niveles: rebobinado MES M2 (ultima escritura 19:40 UTC de hoy). Registro en vivo: ultima linea 19:37:59 UTC de hoy. El log de Gamma Hoy esta en hora local (UTC-3).

### 1.1 En una linea
**El 61 % / 83 % desaparecio.** Con el modelo congelado y 5 ruedas que el ajuste nunca vio, el AUC baja de 0,553 (dentro de muestra) a **0,520** (0,512 en la tarde), y los disparos con p extrema aciertan **50,0 % de 24** (toda la rueda, con enfriamiento) contra un placebo de 57,8 % y una tasa base de 51,1 %. Y el registro EN VIVO no sirve para juzgar nada: **17 de sus 23 disparos son un artefacto de arranque del indicador**, no el modelo leyendo el mercado.

### 1.2 A — los disparos reales del vivo (11-09 al 17-09)
- 18 disparos con desenlace medible (los 2 ultimos del 17-09 quedaron sin las 5 velas siguientes), **todos CORTOS**. Resueltos **7**: 3 aciertos, 4 fallos = **42,9 %**. Placebo (mismo dia, misma media hora, corto): 52,0 +- 14,3 -> z **-0,64**. Tasa base del corto en esas tardes: 55,7 %. Sin resolver: **11 de 18** (ni +3 ni -3 en 10 min). Por dia: 11-09 1/1, 15-09 0/3, 17-09 2/3, 14-09 0/0 (tres sin resolver), 16-09 sin disparos. Neto: -0,92 pts por operacion resuelta (-0,39 contando las no resueltas a precio de salida a los 10 min).
- El desenlace medido con las velas del rebobinado coincide con el medido con las velas que anoto el vivo en 14 de 14 comparables. El 14-09 el grafico en vivo estaba en U6 (67,75 pts abajo del Z6 del rebobinado): diferencia constante, no cambia el desenlace.
- **Por que el vivo no vale como prueba** (`modelo_es_03_porque_difiere.py`, `modelo_es_04_arranques.py`):
  1. La p que anoto el vivo esta clavada en 0,23-0,24 y la p reconstruida con los niveles del rebobinado en esas mismas velas da 0,38-0,54. Mi port del modelo NO es el problema: contra el registro de archivo que escribe el propio C# (corrida #139, 6 disparos del 16-09) la p coincide al centesimo en los 6 y salen los mismos 6 disparos.
  2. **17 de los 23 disparos del log caen en el MISMO SEGUNDO que un "Gamma Hoy ... arranca en HIBRIDO" (raiz ES)**. Todos los arranques de tarde dispararon: 10-09 3 arranques = 3 disparos, 11-09 2 = 2, 15-09 7 = 7 (uno a las 20:01), 17-09 5 = 5. Mecanismo (leido en `GammaHoy.cs` lineas 1532-1603 y en `GatilloModelo.cs`): al arrancar, `_modVivo` nace sin historia (con menos de 20 velas usa rt = rango de la propia vela y dz = 0) y los niveles todavia no cargaron (NaN -> rellenos mn = -40, mp = +40, dominantes +-40). Solo esos rellenos ponen el logit en -1,16 -> **p = 0,24 -> CORTO**, sin mirar nada. A diferencia de `_gatVivo`, al modelo no se lo calienta con las velas previas. En el registro se los reconoce por `"dom":0`.
  3. De los 6 disparos restantes, **5 salieron con el modelo todavia sin calentar** (8 a 16 velas desde el ultimo arranque; con menos de 20 las distancias a niveles quedan divididas por el rango de UNA vela). Probado en `modelo_es_07_sin_calentar.py` con los niveles que el propio vivo anoto en esa vela: la p "fria" reproduce la del registro al centesimo (0,285 / 0,183 / 0,294 / 0,221 / 0,207 contra 0,29 / 0,18 / 0,29 / 0,22 / 0,21) y la p "caliente" (60 velas de historia, como se midio el modelo) habria sido 0,40-0,45: **ninguno de los 5 habria disparado**. El unico disparo en regimen de los 23 (14-09 19:58, p 0,26 en las dos cuentas y tambien en la reconstruccion) quedo sin resolver.
  - Conclusion: en 5 tardes el modelo medido disparo en vivo UNA vez. Las otras 22 flechas "modelo" que vio el operador eran artefactos de arranque (17) o del modelo frio (5). No se toca el indicador desde aca; queda anotado como falla.

### 1.3 R — el modelo M2 congelado, reconstruido (corrida "ultima" de cada dia)
| medicion | n resueltos (+sin resolver) | acierto | placebo misma media hora | z | tasa base del lado | dias arriba | neto pts/op (resueltos) |
|---|---|---|---|---|---|---|---|
| **R1 tarde 0,70 enfr 5 (finalista 1)** | 5 (+2) | 80,0 % (4/5, todos del 16-09) | 64,3 +- 20,1 | +0,78 | 55,7 % | 1 de 1 | +1,31 (n = 5: no dice nada) |
| **R2 rueda 0,70 enfr 5 (finalista 2)** | 24 (+2) | **50,0 %** | 57,8 +- 9,8 | **-0,80** | 51,1 % | 1 de 2 | **-0,49** |
| R3 manana 0,70 enfr 5 | 19 | 42,1 % | 55,3 +- 11,4 | -1,15 | 49,7 % | 1 de 2 | -0,96 |
| R4 tarde 0,70 sin enfriamiento | 24 (+3) | 58,3 % | 57,8 +- 9,6 | +0,05 | 55,7 % | 1 de 1 | +0,01 |
| R5 rueda 0,70 sin enfriamiento | 76 (+18) | 51,3 % | 58,9 +- 5,2 | -1,46 | 51,1 % | 2 de 3 | -0,41 |
| R6 tarde 0,65 enfr 5 | 6 (+2) | 66,7 % | 67,4 +- 17,4 | -0,04 | 55,7 % | 1 de 2 | +0,51 |
| R7 rueda 0,65 enfr 5 | 35 (+4) | 57,1 % | 54,8 +- 7,8 | +0,29 | 51,1 % | 4 de 5 | -0,06 |
| R8 AUC rueda / manana / tarde | 755 / 572 / 183 velas | **0,520 / 0,521 / 0,512** | — | — | — | por dia: 0,562 0,436 0,546 0,485 0,568 | — |

- R2 por dia: 11-09 2/2, 16-09 10/22; 14-09, 15-09 y 17-09 sin un solo disparo. Por hora NY: 9 h 4/5, 10 h 0/2, 11 h 1/2, 12 h 2/4, 13 h 1/6, 14 h 4/5. Cero largos en 5 dias: el modelo solo dice "corto".
- **69 de las 76 velas con p <= 0,30 son del 16-09** (rueda de 117 puntos de rango: maximo 7694, minimo 7576,75, con la caida concentrada en la tarde): el modelo se pone "seguro" cuando ya esta cayendo, y en esas velas acerto 35 de 69 (51 %).
- Sensibilidad (pre-registrada), corrida "primera" de cada dia: AUC 0,525 / tarde 0,476; R1 3/4 (z +0,42); R2 5/9 = 55,6 % (placebo 62,3, z -0,44). Misma conclusion.
- Sensibilidad de la ventana (`modelo_es_06_sensibilidad.py`): rasgos calculados como el laboratorio (solo rueda, ventana del dia) en vez de como el indicador: AUC 0,512, velas extremas 48,2 % de 56 (todas del 16-09). Correlacion entre las dos p: 0,945. No depende del detalle.
- Control dentro de muestra (19-08 al 10-09, los dias del ajuste): AUC 0,553 (tarde 0,565); R5 62,0 % de 137 — reproduce el "62 %" del 10-09 — pero el placebo de la misma media hora da 60,0 +- 4,1 (z +0,49). R4 tarde: 81,2 % de 16 contra placebo 77,1 +- 8,8 (z +0,46), en 4 dias. Con enfriamiento (R1): 2 de 5.
- E (equivalencia vivo/reconstruido): de los 18 disparos del vivo solo 1 reaparece en R1 (el unico que salio en regimen); la diferencia de p mediana es 0,23. Explicado en 1.2.

### 1.4 Auditoria post-hoc del titular (`modelo_es_05_titular.py`; NO es veredicto, es contexto)
Reproduje el avance hacia adelante del 10-09 con el mismo codigo y los mismos dias y salen **los mismos numeros al decimal**: M1 63,8 % de 370 (umbral 0,65) y **tarde 0,70: 83,1 % de 59**. Lo que no se le pregunto ese dia:
- Esos 59 son **59 cortos, 0 largos, en 11 medias horas de 4 dias**. Placebo de la misma media hora y el mismo lado: **77,4 +- 4,6 -> z +1,22**. Con enfriamiento de 10 velas (sin contar velas pegadas de la misma caida): 19 disparos (9 del 03-09), 68,4 % contra placebo 68,6 % (z -0,02).
- Todo el dia, umbral 0,70: 62,8 % de 207 (201 cortos), placebo 62,6 -> z +0,10. Umbral 0,65: 63,8 % de 370, placebo 62,0 -> z +0,89; con enfriamiento 57,5 % de 87 contra 59,0.
- M2 (el instalado): tarde 0,70: 87,0 % de 23 (23 cortos, 8 medias horas de 5 dias), placebo 80,6 +- 7,8 (z +0,81); con enfriamiento 10 de 13 contra 77,6 %. Todo el dia 0,70: 56,2 % de 73, placebo 56,7.
- Lectura honesta: el 83 % era real como cuenta, pero eran ~11 episodios de caida contados 59 veces. Le ganaba a la tasa base (48,5 %) porque el modelo detecta que "esta cayendo" (momentum de 15 velas), no porque elija mejor el momento que cualquier otra vela de esa media hora. Salvedad: el placebo de la misma media hora es exigente (sabe de antemano que esa media hora fue de caida); por eso el juicio de fondo es el de los dias nuevos, y ahi no le gana ni a la tasa base (51,3 % contra 51,1 %).
- El modelo **M1** (el del titular) no se puede juzgar fuera de muestra: no hay velas de 1 min de MES con niveles despues del 08-09.

### 1.5 Veredicto (con la regla fijada en el pre-registro)
- **Finalista 1, R1 (tarde, como esta instalado): NADA.** n = 5 (hacian falta 100), z +0,78, un solo dia con disparos. No hay muestra, y la que hay es de una sola rueda de caida fuerte.
- **Finalista 2, R2 (toda la rueda): NADA.** 50,0 % de 24 (empate 58,2 %), z -0,80, neto -0,49 pts por operacion.
- La frase pedida: **desaparecio** (no se distingue del placebo ni de la tasa base). Con la salvedad de que 5 ruedas es poco: lo que se puede afirmar es que NO hay evidencia nueva a favor, que el AUC fuera de muestra es de moneda (0,52; 2 de 5 dias por debajo de 0,50), y que el titular original ya no le ganaba al placebo de la misma media hora el dia que se midio.

### 1.6 Lo que sirve para LEER (aunque no sea gatillo)
- **En MES la apuesta de +-3 puntos en 10 min casi no existe a la tarde.** Desde cualquier vela de 2 min (21 ruedas): a las 9-11 h NY se resuelve el 93-96 % de las veces y en el 43-75 % de los casos se define en la PRIMERA vela siguiente (2 min); a las 13-15 h NY se resuelve solo el 71-75 %: tres de cada diez veces no pasa nada en 10 minutos (rango mediano de la vela: 2,25 pts contra 4-5 a la manana). Un gatillo de "solo tarde" con objetivo de 3 puntos opera justo donde el objetivo menos llega: 11 de los 18 disparos del vivo quedaron sin resolver.
- **Un modelo "seguro" que solo dice CORTO es un detector de caida en curso, no un adivino.** Fuera de muestra, 69 de sus 76 velas seguras fueron de una sola rueda que ya venia cayendo, y acerto 51 %. Seguir una caida de 10 min en ES con objetivo de 3 puntos es moneda.
- **Cuidado con los aciertos pegados.** 59 aciertos eran 11 episodios. Cualquier estadistica por vela sin enfriamiento infla el n por 3 a 5 y arrastra el acierto.
- **Trampa de datos para todo el laboratorio:** `gatillo_cientifico.cargar("MES", "M2")` (la ultima escritura de cada vela gana) mezcla U6 y Z6 entre el 13-09 22:00 y el 14-09 UTC: 204 saltos falsos de 67 puntos entre velas consecutivas. Cualquier script que use un rebobinado en la semana del roll tiene que elegir UNA corrida por dia (`modelo_es_lib.corridas()`). Las distancias a niveles dentro de una misma linea no se ven afectadas (vela y niveles son de la misma corrida).
- **Falla del indicador para anotar (no tocada):** cada arranque de Gamma Hoy en la tarde dibuja una flecha "modelo" CORTO con p 0,24 que no salio de ningun dato. Si el gatillo se conserva: calentar `_modVivo` con las 60 velas previas (como `_gatVivo`), y no disparar si faltan niveles o hay menos de 20 velas de historia.
