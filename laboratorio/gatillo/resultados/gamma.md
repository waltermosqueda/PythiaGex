# Familia GAMMA — regimen y niveles de gamma x flujo (MNQ, 17-09)

Scripts: `laboratorio/gatillo/gamma_*.py` (la definicion literal de cada variante esta en `gamma_lib.py`).

## 0. Lo que se miro ANTES de pre-registrar (solo conteos y alineacion, ninguna barrera)

- `gamma_00_describe.py`, `gamma_01_alineacion.py`, `gamma_02_conteo.py`, `gamma_02b_conteo_umbrales.py`: cobertura, regimen por dia,
  cantidad de disparos por variante. NINGUNO calcula un resultado: los umbrales de V06, V08 y V12 se aflojaron mirando SOLO el n.
- Marca de tiempo de los niveles: el `c` de `niveles_m1` coincide al centavo con el cierre de la cinta del MISMO minuto (19 de 20 sesiones,
  diferencia mediana 0,00). O sea la marca es el minuto de la vela; el nivel recien se conoce al cerrar esa vela y vale para el minuto siguiente.
- El 14-09 el grafico estaba en otro contrato que la cinta (298 puntos de diferencia, roll): por eso los niveles se usan como DISTANCIA y se
  llevan al segundo con el movimiento de la cinta: `dist[t] = d_nivel[m-1] + (ultimo[t] - cierre_cinta[m-1])`.
- Los niveles NO estan quietos: el zero gamma (libro por volumen) salta mas de 5 puntos de un minuto al otro en el 33 % de los minutos;
  dominantes y majors, en el 15-16 %. Max Change de 5 min: falta en el 26 % de los minutos.
- HALLAZGO DE LECTURA (sin barreras): las dos etiquetas de regimen del indicador se contradicen casi siempre. Con el precio 25+ puntos ABAJO
  del zero gamma (teoria: gamma negativa, momentum) el cuadrante dice 1 = "IMAN: rango, reversion" en 4117 de 4540 minutos (91 %). Con el
  precio 25+ puntos ARRIBA (teoria: reversion) el cuadrante dice 2 = "EXPLOSIVO: momentum" en 1989 de 2354 (84 %). Las variantes V01/V02 y
  V03/V04 son por eso casi espejos: a lo sumo una de cada par puede dar bien.

## 1. Particion ALTERNADA (excepcion de esta familia)

20 sesiones ordenadas por fecha; explorar = posiciones 0, 2, 4, ...; confirmar = 1, 3, 5, ...

- EXPLORAR: 08-20, 08-24, 08-26, 08-28, 09-01, 09-03, 09-08, 09-10, 09-14, 09-16
- CONFIRMAR: 08-21, 08-25, 08-27, 08-31, 09-02, 09-04, 09-09, 09-11, 09-15, 09-17

Regimen por zero gamma (POS = precio - zero >= +25; NEG = <= -25; en el medio no se opera), en segundos elegibles de la rueda:
- EXPLORAR: NEG puro (>= 94 % del dia) 5 dias (08-24, 08-26, 08-28, 09-01, 09-10); mezclados 3 (08-20, 09-03, 09-08); POS mayoritario 2 (09-14 66 %, 09-16 68 %).
  Dias con POS >= 20 % del tiempo: 5. Dias con NEG >= 20 %: 9.
- CONFIRMAR: NEG puro 5 (08-25, 08-27, 08-31, 09-02, 09-04); mezclados 2 (08-21, 09-09); POS mayoritario 3 (09-11 85 %, 09-15 80 %, 09-17 99 %).
  Dias con POS >= 20 %: 5. Dias con NEG >= 20 %: 6.
- Los dos regimenes tienen >= 3 dias en cada mitad, pero POS son 5 dias por mitad: la muestra efectiva para cualquier afirmacion sobre
  el regimen son DIAS, no disparos. Se reporta siempre el desglose por regimen y por dia.

## 2. PRE-REGISTRO (escrito antes de calcular ninguna barrera)

Comun a todas: rueda 13:32:00-20:00:00 UTC; sin `hueco` entre t-600 y t+900; rasgos con filas <= t; umbrales = percentil movil de la
ventana previa [t-1800, t-1] s; minimo 60 s entre disparos de la misma variante (gana el primero); entrada `ultimo[t]`; barrera principal
+-8 / 600 s (empate `B.empate(8)` = 56,0 %), secundarias +-5 / 300 s (59,6 %) y +-12 / 900 s (54,0 %).
Nivel conocido en t = el de la vela de 1 min CERRADA anterior. s = signo del flujo; "seguir" = lado s; "en contra" = lado -s.

Disparos de flujo:
- RAF60: |delta de 60 s| >= percentil 90 previo Y el precio se movio >= 4 pts a favor de ese delta en los mismos 60 s. (Receta de la literatura.)
- RAF10: |delta de 10 s| >= percentil 99 previo Y el precio se movio >= 1 pt a favor en los mismos 10 s.

Interruptor de regimen:
- ZERO: seguir si d_zero <= -25 (gamma negativa), en contra si d_zero >= +25 (gamma positiva), nada en el medio.
- CUAD: en contra si cuadrante 1 o 3 (convexidad positiva: "iman"/"estable"), seguir si 2 o 4 ("explosivo"/"riesgo").

| # | Variante | Definicion exacta | Lado |
|---|---|---|---|
| V01 | RAF60_ZERO | RAF60 | interruptor ZERO |
| V02 | RAF60_CUAD | RAF60 | interruptor CUAD |
| V03 | RAF10_ZERO | RAF10 | interruptor ZERO |
| V04 | RAF10_CUAD | RAF10 | interruptor CUAD |
| V05 | ABS_DOM | A30 = suma 30 s de (abs_bid - abs_ask); abs(A30) >= percentil 95 previo; dominante mas cercana (dom0/dom1) a <= 12 pts; la absorcion DEFIENDE el nivel: precio arriba del nivel y A30 > 0, o precio abajo y A30 < 0 | signo de A30 (rebote) |
| V06 | EMPUJE_FRENADO_DOM | dominante mas cercana a <= 15 pts; delta de 30 s con abs >= percentil 70 previo empujando CONTRA el nivel (vende con el precio arriba, compra con el precio abajo); el precio NO avanzo: s x (ultimo[t] - ultimo[t-30]) <= 0,5 pts | -s (el nivel aguanta) |
| V07 | EMPUJE_AVANZA_DOM | igual que V06 pero el precio SI avanzo >= 3 pts a favor del delta en esos 30 s | s (el nivel se rompe) |
| V08 | PASIVO_GIRA_MAJOR | nivel grande mas cercano (major positivo, major negativo o zero gamma) a <= 15 pts; el precio se acerco >= 3 pts al nivel en 120 s; ofi_pas de 30 s con signo CONTRARIO a la llegada y abs >= percentil 80 previo; 30 s antes el ofi_pas de 30 s tenia el signo de la llegada (giro) | contra la llegada (rechazo) |
| V09 | RAF60_BORDE_ZERO | RAF60 que empuja HACIA una dominante a <= 15 pts | interruptor ZERO |
| V10 | MC5_NUEVO_CON_FLUJO | segundo :02 del minuto; el Max Change de 5 min conocido cambio >= 10 pts respecto del minuto anterior; queda a 10-60 pts del precio; el delta de 60 s apunta HACIA el Max Change | hacia el Max Change |
| V11 | MC5_NUEVO_SIN_FLUJO | igual que V10 pero el delta de 60 s apunta para el OTRO lado | hacia el Max Change |
| V12 | D5M_EXTREMO_ZERO | ultimo segundo de cada vela de reloj de 5 min; abs(delta/vol) de la vela >= percentil 70 de las 12 velas previas | interruptor ZERO sobre el signo del delta de la vela |

Conteo previo (sin resultados) en explorar / confirmar: V01 756/710 · V02 894/847 · V03 354/332 · V04 411/393 · V05 242/204 · V06 ~244/274 ·
V07 (no se reconto con el umbral aflojado; al correr dio 859/820) · V08 ~173/181 · V09 261/218 · V10 295/167 · V11 271/158 · V12 ~218/226.

### Regla para elegir finalistas (fijada ahora)

1. Se explora SOLO en la mitad EXPLORAR, con la barrera +-8 / 600 s.
2. Candidata = n resuelto >= 150. Se ordenan por z contra placebo (200 sorteos: misma sesion, misma media hora, mismos lados).
3. Finalista PLENA = candidata con acierto >= 56,0 %, z contra placebo >= 2,0 y >= 70 % de sus dias arriba de 50 %. Maximo 2. De cada
   par espejo (V01/V02, V03/V04) entra a lo sumo una.
4. Si hay menos de 2 plenas, se completa hasta 2 con las candidatas de mayor z contra placebo, marcadas "NO cumple el piso en explorar":
   se corren igual en confirmar para dejar el dato, pero su veredicto no puede pasar de PISTA.
5. Las finalistas se corren UNA vez en CONFIRMAR. SOBREVIVE = acierto >= 58,0 % (empate + 2), z contra placebo >= 2,5, >= 7 de 10 dias
   arriba de 50 %, n >= 100. PISTA = positivo pero falla un criterio. NADA = el resto. Sin retoques despues.
6. Para toda variante con interruptor de regimen se reporta el desglose (rama "seguir" y rama "en contra" por separado) y los dias.

### Descriptivos declarados (no dan veredicto; para LEER la microestructura)

- D1. Velocidad: segundos hasta resolver +-8 desde un segundo cualquiera, por regimen (POS / NEG) y por dia. La teoria dice POS = mas lento.
- D2. Continuacion de RAF60 y RAF10 (lado = seguir) por regimen ZERO y por cuadrante, con movimiento medio firmado a 10/30/60/120/300 s.
- D3. Absorcion (mismo disparo de V05) cerca de una dominante contra lejos de todo nivel (> 30 pts).
- D4. Para finalistas con interruptor: permutacion del regimen ENTRE DIAS (diferencia de continuacion entre dias NEG y dias POS).

---
## 3. EXPLORAR (10 sesiones pares) — `gamma_03_explorar.py`

Barrera +-8 / 600 s, empate 56,0 %. "pl" = placebo (200 sorteos, mismos lados, misma sesion y media hora). "zp" = z contra placebo.

| Variante | n | acierto | pl | zp | dias > 50 % | peor dia | +-5 (pl, zp) | +-12 (pl, zp) | rama POS (n, %) | rama NEG (n, %) |
|---|---|---|---|---|---|---|---|---|---|---|
| V01 RAF60_ZERO | 750 | 51,9 | 52,0 | -0,07 | 6/10 | 43 | 48,9 (51,0, -1,14) | 49,3 (52,8, -2,10) | 204, 53,4 | 546, 51,3 |
| V02 RAF60_CUAD | 888 | 50,0 | 48,7 | +0,77 | 5/10 | 41 | 51,0 (49,1, +1,16) | 51,4 (48,2, +2,06) | 195, 47,7 | 536, 50,9 |
| V03 RAF10_ZERO | 351 | 49,3 | 52,6 | -1,24 | 5/10 | 34 | 48,3 (51,6, -1,21) | 50,1 (53,6, -1,41) | 88, 51,1 | 263, 48,7 |
| V04 RAF10_CUAD | 408 | 54,2 | 48,4 | **+2,48** | 7/10 | 44 | 54,1 (49,3, +2,12) | 51,1 (47,7, +1,55) | 82, 53,7 | 258, 55,4 |
| V05 ABS_DOM | 242 | 44,2 | 48,6 | -1,44 | 1/10 | 25 | 45,9 (49,0, -1,05) | 42,1 (48,1, -2,00) | 50, 52,0 | 151, 42,4 |
| V06 EMPUJE_FRENADO_DOM | 242 | 52,5 | 48,5 | +1,31 | 4/10 | 32 | 51,0 (50,0, +0,35) | 57,6 (49,7, +2,75) | 48, 56,2 | 162, 53,1 |
| V07 EMPUJE_AVANZA_DOM | 859 | 46,4 | 51,9 | -3,26 | 4/10 | 38 | 48,0 (51,1, -1,76) | 46,3 (51,9, -3,45) | 173, 44,5 | 586, 47,1 |
| V08 PASIVO_GIRA_MAJOR | 172 | 50,0 | 46,1 | +1,11 | 6/10 | 31 | 52,6 (48,0, +1,21) | 45,0 (43,9, +0,31) | 20, 35,0 | 95, 51,6 |
| V09 RAF60_BORDE_ZERO | 259 | 51,7 | 52,0 | -0,11 | 6/10 | 41 | 48,3 (50,7, -0,77) | 48,8 (53,1, -1,47) | 66, 56,1 | 193, 50,3 |
| V10 MC5_NUEVO_CON_FLUJO | 294 | 47,6 | 51,7 | -1,41 | 4/10 | 32 | 45,8 (51,3, -1,73) | 49,5 (52,1, -0,93) | 44, 47,7 | 202, 49,0 |
| V11 MC5_NUEVO_SIN_FLUJO | 270 | 50,7 | 51,1 | -0,13 | 4/10 | 36 | 47,0 (50,6, -1,25) | 52,4 (51,0, +0,55) | 33, 54,5 | 203, 51,2 |
| V12 D5M_EXTREMO_ZERO | 217 | 55,8 | 53,1 | +0,81 | 7/10 | 45 | 57,9 (51,7, +1,95) | 60,6 (54,3, +1,97) | 65, **67,7** | 152, 50,7 |

Lectura de explorar:
- NINGUNA variante llega al empate (56,0 %) en la barrera principal. No hay finalista PLENA.
- El interruptor por zero gamma no hace nada sobre las rafagas: V01 51,9 % contra placebo 52,0 %; V03 49,3 % contra 52,6 %.
- Por la regla (punto 4) pasan a confirmar las dos candidatas de mayor z contra placebo: **V04 RAF10_CUAD** (+2,48) y **V06 EMPUJE_FRENADO_DOM** (+1,31),
  las dos marcadas "NO cumple el piso en explorar": su veredicto no puede pasar de PISTA.
- Anotado ANTES de confirmar, como observaciones fuera de la regla (no son finalistas):
  (a) V12, rama POS (ir EN CONTRA del delta extremo de 5 min con el precio 25+ pts arriba del zero gamma): 67,7 % con n 65, 5 de 5 dias. Rama NEG (seguir): 50,7 %.
      Es una rama, no una variante: n chico y 5 dias.
  (b) V07 dio AL REVES de lo registrado: seguir un empuje que avanza hacia una dominante pierde (46,4 %, z -3,26 contra placebo). El espejo (ir en contra) seria 53,6 %: bajo el empate.
  (c) V05 dio al reves: la absorcion "defendiendo" una dominante acerto 44,2 %, 1 de 10 dias arriba de 50 %.

## 4. CONFIRMAR (10 sesiones impares), UNA corrida — `gamma_04_confirmar.py CONF V04,V06`

| Finalista | barrera | n | acierto | empate | placebo | z contra placebo | dias > 50 % | peor dia | neto por operacion |
|---|---|---|---|---|---|---|---|---|---|
| V04 RAF10_CUAD | +-8 / 600 | 387 | **46,3 %** | 56,0 | 47,0 | -0,27 | 3 de 10 | 40 % | -1,55 pts |
| | +-5 / 300 | 389 | 49,4 % | 59,6 | 48,1 | +0,51 | 6 de 10 | 39 % | -1,02 pts |
| | +-12 / 900 | 376 | 46,8 % | 54,0 | 45,8 | +0,40 | 3 de 10 | 40 % | -1,73 pts |
| V06 EMPUJE_FRENADO_DOM | +-8 / 600 | 267 | **47,2 %** | 56,0 | 50,0 | -0,90 | 4 de 10 | 19 % | -1,41 pts |
| | +-5 / 300 | 272 | 48,9 % | 59,6 | 49,7 | -0,28 | 4 de 10 | 12 % | -1,07 pts |
| | +-12 / 900 | 261 | 50,2 % | 54,0 | 49,8 | +0,13 | 3 de 10 | 20 % | -0,91 pts |

- V04 por rama en confirmar: cuadrante 1/3 (en contra) 45,9 % (n 246, 1 de 7 dias); cuadrante 2/4 (seguir) 46,8 % (n 141, 2 de 4 dias). Las dos ramas se dieron vuelta.
- V06 por regimen en confirmar: POS 49,5 % (n 91), NEG 48,3 % (n 143). El +-12 que en explorar habia dado 57,6 % dio 50,2 %.

**Veredicto de las dos finalistas: NADA.** Lo que asomaba en explorar (z +2,48 y +1,31 contra placebo) era ruido.

### Tabla espejo del resto en confirmar (FUERA de protocolo, sin veredicto, corrida despues de cerrar el veredicto de arriba)

Se deja para que nadie tenga que adivinar: ninguna de las otras diez hubiera pasado. +-8 / 600 s, acierto (placebo, z contra placebo, dias):
V01 49,4 (52,1, -1,41, 5/10) · V02 51,7 (48,2, +2,33, 5/10) · V03 53,5 (53,4, +0,05, 6/10) · V05 48,8 (49,1, -0,08, 4/10) · V07 50,5 (50,8, -0,18, 5/10) ·
V08 47,5 (46,7, +0,25, 3/10) · V09 50,5 (51,1, -0,19, 5/10) · V10 45,8 (51,1, -1,44, 4/10) · V11 53,2 (50,2, +0,80, 6/10) · V12 45,3 (51,9, -1,91, 4/10).
- Lo que en explorar habia dado AL REVES no se repitio: V07 (seguir el empuje que avanza hacia una dominante) 46,4 % -> 50,5 %; V05 (absorcion defendiendo una dominante) 44,2 % -> 48,8 %. Era ruido para los dos lados.
- V12 se rompe por la rama NEG: con gamma negativa el delta extremo de 5 min NO continua (seguir acerto 36,7 %, n 139). La rama POS (ir en contra) repitio: 59,5 % (n 84, 4 de 5 dias).

## 5. Descriptivos declarados (20 sesiones) — `gamma_05_descriptivos.py`

- **D1 velocidad.** Desde un segundo cualquiera de la rueda, el +-8 se resuelve en 60 s de mediana con gamma POS (10 dias; 50 % en <= 60 s) contra 47 s con gamma NEG
  (16 dias; 59 % en <= 60 s). Va para el lado de la teoria (positiva = mas lento) pero NO esta probado: entre dias p = 0,11, y en los 6 dias que tuvieron
  los dos regimenes POS fue mas lento en 4 y mas rapido en 2.
- **D2 continuacion de las rafagas** (lado = seguir, +-8/600): RAF60 49,2 % (n 1730); con POS 47,6 %, con NEG 49,7 %; cuadrante 1/3 48,8 %, cuadrante 2/4 50,0 %.
  RAF10 49,9 % (n 795); POS 48,0 %, NEG 51,3 %; cuadrante 1/3 49,7 %, 2/4 50,4 %. Ninguna etiqueta de regimen mueve la aguja mas de 2-3 puntos y nunca cruza el empate.
- **D3 absorcion** (abs_bid/abs_ask de 30 s, percentil 95, lado = rebote): todas 50,4 % (n 1485); lejos de toda dominante 47,4 %; defendiendo una dominante 46,3 %. El nivel no le agrega nada a la absorcion.
- **D4 por dias** (la muestra honesta: 20 dias): correlacion de Spearman entre "% del dia en gamma POS" y "% de continuacion de las rafagas": RAF60 +0,06 (p 0,79), RAF10 -0,02 (p 0,93).
  Para el delta extremo de 5 min: -0,39 (p 0,09).

## 6. Post-hoc, fuera de protocolo — `gamma_06_posthoc.py` (sin veredicto; para el coordinador)

El unico numero de toda la tabla que se repitio en las dos mitades es una RAMA de V12 y toca la pista que ya tenia el coordinador (ir en contra del delta extremo
de la vela de reloj de 5 min). Aca esta definida distinto y sin mirar resultados: abs(delta/vol) >= percentil 70 de las 12 velas previas, sin piso de volumen, particion alternada.

- Ir en contra, las 20 sesiones: **57,5 %** (n 504), 15 de 20 dias arriba de 50 %, media de los dias 57,3 % (t por dias +2,80, p 0,011). Neto +0,25 pts por operacion: apenas arriba del empate (56,0 %).
  Por mitad: EXPLORAR 54,4 % (6 de 10 dias) · CONFIRMAR 60,6 % (9 de 10 dias).
- Velas NO extremas: 50,3 % (n 1029). Todas las velas: 52,7 %. Lo que aporta es el extremo.
- Es el delta, no solo el precio: contra el movimiento extremo de precio 55,8 %; "delta extremo sin movimiento extremo" 57,9 % (n 190) contra "movimiento extremo sin delta extremo" 53,1 % (n 175). Diferencia no significativa con este n.
- Hora UTC: 13:30-15:00 52,6 % (n 114) · 15:00-18:00 59,2 % (n 267) · 18:00-20:00 58,5 % (n 123).
- **Por regimen de gamma:** POS 63,1 % (n 149, 9 de 10 dias, media de dias 63,4 %, t +3,91) · NEG 56,0 % (n 291, pero por dias es una moneda: 7 de 14 dias, media de dias 51,8 %, p 0,72) · MEDIO 51,6 % (n 64).
  POS por mitad: explorar 67,7 % (n 65, 5 de 5 dias) -> confirmar 59,5 % (n 84, 4 de 5). NEG por mitad: 49,3 % -> 63,3 % (lo cargan dos dias: 02-09 con 83 % y 27-08 con 69 %).
  Dia por dia en POS: 20-08 70 % (10) · 21-08 43 % (7) · 03-09 62 % (8) · 08-09 79 % (14) · 09-09 75 % (8) · 11-09 54 % (24) · 14-09 60 % (15) · 15-09 55 % (22) · 16-09 67 % (18) · 17-09 70 % (23).
- TRES FRENOS antes de creerlo: (1) son 10 dias POS y 149 velas; (2) los dias POS son casi todos del 08-09 en adelante: "gamma positiva" y "la ultima semana y media" son la misma cosa en esta muestra,
  no se pueden separar; (3) **la reserva (31-07 al 19-08) NO tiene niveles de gamma** (`niveles_m1` arranca el 19-08 13:29 UTC): el filtro por regimen no se puede validar ahi; lo unico validable en la reserva es la regla SIN filtro.
- **Nota de metodo sobre el placebo:** para una señal de "ir en contra" de una vela recien cerrada, el placebo del protocolo (segundo al azar de la misma media hora, mismo lado) da 44-48 %, no 50 %: muchos de esos
  segundos caen ANTES o DENTRO de la vela extrema y pierden contra su propio movimiento. El z contra placebo sale inflado (~ +1,5). Para señales contra-vela conviene juzgar contra 50 %, contra el empate y por dias.
- B) Devolucion de la rafaga de 10 s: con POS devuelve -1,49 pts a 60 s y con NEG -0,21, pero contando dias no hay diferencia (Mann-Whitney p 0,41 a 60 s; 0,28 a 30 s; 0,94 a 120 s).

## 7. Veredicto de la familia

**NADA como gatillo.** 12 variantes pre-registradas, 2 finalistas corridas una vez en confirmar: V04 46,3 % y V06 47,2 % contra un empate de 56,0 %. Ni el signo del zero gamma ni el cuadrante
deciden si conviene seguir o ir en contra de una rafaga de delta; ni dominantes, ni majors, ni Max Change mejoran la absorcion, el empuje frenado o el giro del flujo pasivo.

Lo que SI deja para leer (no para disparar):
1. Las dos etiquetas de regimen de la pantalla se contradicen el 84-91 % del tiempo (precio bajo el zero gamma = cuadrante "IMAN, reversion"; precio arriba = "EXPLOSIVO, momentum"). Y ninguna de las dos predice
   que hace el precio despues de una rafaga. No usar ninguna para elegir entre seguir o fadear.
2. Los niveles no son paredes quietas: el zero gamma por volumen se corre mas de 5 puntos de un minuto al otro en 1 de cada 3 minutos; dominantes y majors en 1 de cada 6-7.
3. Con gamma positiva el mercado parece algo mas lento (60 s contra 47 s para resolver +-8), sin prueba estadistica (p 0,11).
4. Despues de una rafaga de delta (10 s o 60 s) seguirla acierta 49-50 % en cualquier regimen: la rafaga NO continua. El movimiento medio devuelve 0,5-1 punto en 1-2 minutos: menos que el costo (0,96).
5. La pista del delta extremo de 5 min (del coordinador) se replica con otra definicion y otra particion (57,5 %, 15 de 20 dias, neto +0,25 pts): sigue siendo marginal. Filtrada por gamma positiva sube a 63 % (9 de 10 dias),
   pero son 10 dias pegados en el calendario y la reserva no tiene niveles para validarlo. Queda como PISTA del coordinador con una pregunta nueva, no como hallazgo de esta familia.

