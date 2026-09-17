# Familia "techo_ml" — EL TECHO: hay algo en TODOS los datos juntos? (17-09-2026)

La pregunta de esta familia no es "que regla gana" sino "cuanto se puede saber, como maximo, del lado de los proximos 10 minutos
juntando TODO lo que sale del ATAS del operador". Si una maquina que ve cien rasgos a la vez no le gana a la moneda fuera de muestra,
ninguna regla a mano con esos mismos rasgos le va a ganar. Y una segunda pregunta, no direccional: se puede saber si los proximos
5 minutos van a ser de movimiento grande o chico?

## PRE-REGISTRO (escrito ANTES de correr ningun modelo ni mirar ningun resultado)

Lo unico mirado antes de escribir esto: estructura de las tablas (columnas, horarios, % de huecos, % de niveles faltantes) y que la
vela de niveles del minuto m coincide al centavo con el ultimo precio de la cinta en m+59 s (medido en 3 sesiones de explorar: diferencia
mediana 0,00, desvio 0,00). Ningun resultado de barrera, ninguna correlacion.

### Grilla y elegibilidad
- Una fila cada 15 s, en los segundos :14, :29, :44 y :59 (la fila :59 es exactamente el cierre de la vela de 1 min).
- Solo rueda (13:30-20:00 UTC), desde 13:32:00, sin los ultimos 900 s de la tabla, y sin hueco (B.hueco) entre t-600 y t+900.
- Referencia de entrada ultimo[t]; el resultado se mira de t+1 en adelante (B.barrera).

### Rasgos (todos causales: en la fila t solo entran filas <= t; ventanas W inclusive de t). Lista exacta en techo_ml_lib.py
 A. Flujo como razon del volumen de la ventana, W = 15, 60, 300, 900 s: dr_W = suma(delta)/suma(vol).
 B. Flujo en desvios, W = 15, 60, 300, 900: zD_W, zO_W, zP_W = suma_W(x) / (desvio de x por segundo en los 1800 s previos * raiz(W)), x = delta, ofi, ofi_pas.
 C. Absorcion / rotura / barridos como razon del volumen, W = 60, 300, 900: absn (abs_bid-abs_ask), abst (abs_bid+abs_ask), romn (rompe_ask-rompe_bid),
    barn (barre_c-barre_v), bart (barre_c+barre_v).
 D. Delta por tamaño como razon del volumen, W = 60, 300, 900: dch (ordenes de 1 a 4), dme (5 a 9), dgr (10 o mas).
 E. Retornos en puntos ret_W y en desvios retz_W (= ret_W / (desvio del cambio de 1 s en 1800 s * raiz(W))), W = 15, 60, 300, 900, 1800, 3600.
 F. Volatilidad: rango alto-bajo rng_W (W = 60, 300, 900, 1800); desvio del cambio de 1 s rv_W (60, 300, 1800); compresion rng_60/rng_900 y rng_300/rng_1800.
 G. Velocidad: ordenes por segundo de la ventana contra la media de 1800 s (act_15, act_60, act_300), lo mismo con contratos (actv_60, actv_300),
    y niveles log(1+n_60), log(1+vol_300), contratos por orden en 300 s.
 H. Eficiencia del movimiento: er_W = |ret_W| / suma|cambio de 1 s|, y con signo ser_W, W = 300, 900.
 I. Posicion en el rango de 30 y 60 min (pos_1800, pos_3600), en el rango de la rueda (pos_rueda), en el de la sesion entera con la noche (pos_sesion),
    y puntos desde la apertura de las 13:30 (d_apertura).
 J. VWAP anclado a las 13:30 y a las 22:00: distancia en desvios (vwap_r_z, vwap_s_z) y en puntos (vwap_r_p, vwap_s_p).
 K. Hora: minutos desde las 13:30 (mins).
 L. Punta EN REPOSO (rbidv/raskv/rbid/rask): desbalance de cola instantaneo y medio de 15 y 60 s (qi, qi_15, qi_60), spread medio de 60 s en ticks,
    profundidad media de 60 s contra la de 1800 s.
 M. Gamma (B.niveles_m1): se usa SIEMPRE el registro del minuto YA CERRADO (el del minuto m esta disponible recien en m+60 s; caduca a los 300 s).
    Nivel en precio de cinta = ultimo[m+59] - d; rasgo = ultimo[t] - nivel. g_zero, g_dom0, g_dom1, g_mp, g_mn, g_mc15, signo de g_zero (g_sz),
    |g_zero|, distancia con signo a la dominante mas cercana (g_cerca), cuadrante (g_cuad).
 N. CVD: delta acumulado de la rueda / volumen acumulado (cvdr); divergencia flujo-precio div_300 = zD_300 - retz_300 y div_900.
Faltantes: xgboost los toma nativos; en la logistica se reemplazan por la media del entrenamiento.

### Objetivos
- Direccional: B.barrera +-8/600 (principal), +-5/300 y +-12/900. Clase 1 = toca arriba primero. Las filas sin resolver no entran al ajuste ni al AUC.
- No direccional: R300 = max(alto[t+1..t+300]) - min(bajo[t+1..t+300]) en puntos. Se ajusta log(R300). "Grande" = R300 por encima de la mediana de las
  sesiones de ENTRENAMIENTO de cada vuelta (un numero fijo en puntos).

### Modelos (hiperparametros FIJOS, sin busqueda)
- xgboost: 200 arboles, profundidad 3, tasa 0,03, subsample 0,5, colsample_bytree 0,5, min_child_weight 100, reg_lambda 10, hist, n_jobs=2, semilla 7.
- logistica de control: rasgos recortados a los percentiles 1-99 del entrenamiento, estandarizados, L2 con C = 0,01.
- Validacion: dejar UNA SESION entera afuera (12 vueltas) dentro de B.explorar(T). Nunca se mezclan segundos del mismo dia.

### Variantes (10 definidas + 2 reservadas = 12)
 V01 xgb, todos los rasgos, +-8/600            V02 logistica, todos, +-8/600
 V03 xgb, todos, +-5/300                       V04 logistica, todos, +-5/300
 V05 xgb, todos, +-12/900                      V06 logistica, todos, +-12/900
 V07 xgb SIN los rasgos de gamma, +-8/600      (el regimen de gamma esta confundido con el corte explorar/confirmar: bajo el zero casi todo explorar, arriba casi todo confirmar)
 V08 xgb NUCLEO de 22 rasgos, +-8/600          (dr_60, dr_300, dr_900, zD_300, zO_300, zP_300, ret_60, ret_300, ret_900, retz_1800, rng_300, rng_900, act_60,
                                                pos_1800, pos_3600, vwap_r_z, mins, qi_60, g_zero, g_dom0, g_dom1, er_900)
 V09 xgb de regresion sobre log(R300), todos los rasgos     (no direccional)
 V10 control lineal de V09: log rng_60, log rng_300, log rng_900, log(1+n_60), mins, mins^2  (persistencia + reloj)
 V11, V12 RESERVADAS: reglas simples destiladas de los 2-3 rasgos que manden por permutacion. SOLO se definen si el AUC fuera de muestra de V01, V02 o V08
          llega a 0,53; umbrales puestos mirando solo explorar. Si el AUC no llega, no se definen y quedan como "no corridas".

### Como se mide cada variante direccional en explorar (todo fuera de muestra, vuelta por vuelta)
- AUC conjunto y AUC por dia (media, desvio, dias arriba de 0,50, t = (media-0,5)/(desvio/raiz(12))).
- DISPARO del modelo: fila con confianza |p-0,5| >= U, U = percentil 90 de la confianza fuera de muestra; lado = signo(p-0,5); desagrupado 60 s (gana el primero).
  Se reporta n resuelto, % de acierto contra B.empate(x), dias arriba de 50 %, puntos netos por operacion y z contra PLACEBO (200 sorteos: misma cantidad de
  disparos en segundos elegibles al azar de la misma sesion y la misma media hora, mismos lados, mismo espaciado).
- Importancia por permutacion fuera de muestra (caida de AUC al barajar cada rasgo en la sesion dejada afuera, 5 repeticiones), para V01 y V08.

### Regla para elegir finalistas (como mucho 2 direccionales)
- Finalista 1: V01, SIEMPRE, de la forma que de: es la medida del techo que pide la consigna (una pasada en confirmar con el modelo congelado).
- Finalista 2: entre V02-V08, V11 y V12, la de mejor z contra placebo en su propia barrera, solo si cumple a la vez n >= 150 resueltos, acierto >= empate,
  y >= 9 de 12 dias arriba de 50 %. Si ninguna cumple, no hay segundo finalista.
- V09 (no direccional) pasa UNA vez por confirmar junto con su control V10. No es gatillo: se juzga aparte. "Sirve de filtro" si en confirmar AUC >= 0,70 y
  >= 7 de 8 dias con AUC >= 0,65. "El flujo agrega sobre el reloj y la persistencia" si AUC(V09) - AUC(V10) >= 0,02 en confirmar.

### Confirmacion (una sola pasada)
Modelo ajustado con las 12 sesiones de explorar y congelado; umbral U congelado (el de explorar fuera de muestra). Se corre en B.confirmar(T) una vez.
Veredicto por finalista direccional: SOBREVIVE = acierto >= empate + 2 puntos, z >= 2,5 contra placebo, >= 6 de 8 dias arriba de 50 %, n >= 100.
PISTA = positivo pero falla un criterio. NADA = el resto. Si el AUC fuera de muestra no pasa de 0,52-0,53, ese ES el hallazgo: no hay direccion a 10 min aca.
Prohibido retocar nada despues de ver confirmar.

---

## EXPLORAR (12 sesiones, 20-08 al 04-09; 18.516 filas de grilla de 15 s; 101 rasgos) — corrido una vez, todo fuera de muestra

Scripts: `techo_ml_01_tablero.py` (arma el tablero), `techo_ml_02_explorar.py` (V01-V10), `techo_ml_03_importancia.py`, `techo_ml_04_sesgo_auc_dia.py`.
Base de la apuesta en estas filas: +-8/600 se resuelve el 99,8 % de las veces, 51,4 % hacia arriba, mediana 47 s. Empates: 56,0 / 59,6 / 54,0 %.

### Direccional: AUC fuera de muestra y disparos del decil mas confiado (desagrupados 60 s, placebo de 200 sorteos)

| variante | AUC conjunto | n | acierto | empate | placebo | z placebo | dias > 50 % | netos/op | % largos |
|---|---|---|---|---|---|---|---|---|---|
| V01 xgb todos +-8      | 0,492 | 669 | 48,1 | 56,0 | 49,5 | -0,90 | 6 de 12  | -1,26 | 77 |
| V02 logit todos +-8    | 0,507 | 691 | 53,7 | 56,0 | 52,6 | +0,66 | 9 de 12  | -0,36 | 62 |
| V03 xgb todos +-5      | 0,509 | 732 | 54,6 | 59,6 | 51,3 | +2,04 | 9 de 12  | -0,50 | 76 |
| V04 logit todos +-5    | 0,507 | 725 | 51,4 | 59,6 | 52,1 | -0,38 | 9 de 12  | -0,82 | 66 |
| V05 xgb todos +-12     | 0,498 | 535 | 47,3 | 54,0 | 51,2 | -2,98 | 2 de 12  | -1,55 | 75 |
| V06 logit todos +-12   | 0,500 | 584 | 52,2 | 54,0 | 54,1 | -1,25 | 6 de 11  | -0,41 | 72 |
| V07 xgb sin gamma +-8  | 0,494 | 680 | 47,4 | 56,0 | 49,8 | -1,46 | 5 de 12  | -1,37 | 76 |
| V08 xgb nucleo +-8     | 0,509 | 696 | 55,9 | 56,0 | 52,4 | +2,27 | 10 de 12 | -0,01 | 81 |

- El AUC fuera de muestra de las 8 variantes queda entre 0,492 y 0,509. Ninguna llega a 0,52. Error del AUC remuestreando dias: ver confirmacion (~0,01).
- Acierto de "seguir al modelo" por quintil de confianza (V01): 49,3 / 53,3 / 52,1 / 50,6 / 46,6. No crece con la confianza: el modelo no sabe cuando sabe.
- Ningun decil confiado llega al empate. La que mas se acerca es V08 (55,9 % contra 56,0 %; z +2,27 contra placebo, con 8 variantes probadas eso es lo que da el azar
  una de cada tantas; y el 81 % de sus disparos son largos en un tramo que derivo hacia arriba).
- Quitar la gamma (V07) no cambia nada (0,494 contra 0,492): los niveles de gamma no le agregan direccion al modelo.
- V11 y V12 NO se definen: el AUC no llego a 0,53 (condicion del pre-registro). Quedan como "no corridas".

### Importancia por permutacion fuera de muestra (V01 y V08)
Con un AUC de 0,49-0,51 la importancia es ruido: la caida maxima de AUC al barajar un rasgo es 0,003 (absn_900, zP_900) en V01 y 0,005 (dr_900) en V08.
Por grupos en V01: flujo +0,004; punta, precio, volatilidad, gamma y reloj entre -0,001 y -0,004 (barajarlos MEJORA el AUC: son ruido aprendido).

### Una trampa de metodo que encontre (sirve a todas las familias)
Mirando cada rasgo SOLO contra la barrera, los rasgos "de nivel" (puntos desde la apertura, posicion en el rango de la rueda, distancia al VWAP, CVD de la rueda,
distancia al zero gamma) daban AUC POR DIA de 0,44-0,46 en 12 de 12 dias (t de -5 a -6). Parece un hallazgo enorme ("fadea la distancia a la apertura"). NO LO ES:
con un precio SIN memoria (los mismos cambios de 1 s de cada sesion con el signo a cara o cruz, 8 sorteos) la misma cuenta da lo mismo:
d_apertura 0,442 (real 0,441), vwap_r_p 0,454 (real 0,451), pos_rueda 0,462 (real 0,453), ret_3600 0,486 (real 0,476). El AUC conjunto del precio sin memoria da 0,497-0,503.
Causa: al comparar momentos DEL MISMO DIA, el resultado de las 11:00 mueve el rasgo de las 11:10 (si subio, despues el precio esta "mas lejos de la apertura").
Regla: para rasgos de nivel, el AUC o la correlacion "por dia" esta sesgado hacia la reversion; vale el conjunto entre dias, o el placebo por disparos (que no compara pares).
El unico rasgo que no es de nivel y sobresale es absn_900 (absorcion neta de 15 min): AUC conjunto 0,518, 10 de 12 dias. Es 1 de 101 rasgos mirados y la familia
"absorcion" ya midio que el efecto es de 0,2 pts. No se arma regla (el pre-registro no lo permite).

### No direccional (R300 = rango de los proximos 300 s; "grande" = mas de ~26,5 pts, la mediana del entrenamiento)

| predictor | AUC fuera de muestra | Spearman | AUC por dia (media / peor) |
|---|---|---|---|
| V09 xgb, todos los rasgos          | 0,862 | 0,734 | 0,848 / 0,692 |
| V10 control: rangos previos + reloj | 0,844 | 0,697 | 0,834 / 0,676 |
| solo el rango de los 300 s previos | 0,798 | 0,608 | 0,777 / 0,635 |
| solo el reloj (media hora)         | 0,798 | 0,600 | 0,839 / 0,661 |

Importancia por permutacion de V09: el grupo volatilidad + velocidad se lleva TODO (caida de AUC 0,270), el reloj 0,025, y flujo, punta, precio y gamma CERO
(-0,0001 a -0,0007). Rasgos: rv_300 (0,049), rv_1800 (0,038), mins (0,026). El CVD, el delta, la absorcion y la gamma no dicen nada sobre el tamaño de lo que viene.

Traduccion a la apuesta +-8 (por quintil del rango PREVISTO por V09, fuera de muestra):

| quintil | R300 mediano | % con R300 >= 16 | mediana hasta resolver +-8 | resuelto en <= 60 s | en <= 120 s |
|---|---|---|---|---|---|
| q1 (calmo)  | 16,8 pts | 57 %  | 115 s | 24 % | 51 % |
| q2          | 20,5     | 76 %  | 77 s  | 40 % | 70 % |
| q3          | 25,2     | 93 %  | 52 s  | 56 % | 83 % |
| q4          | 31,5     | 99 %  | 33 s  | 76 % | 94 % |
| q5 (movido) | 50,2     | 100 % | 12 s  | 96 % | 100 % |

### Finalistas (regla del pre-registro, aplicada ANTES de tocar confirmar)
- Finalista 1: V01 (obligatorio: es la medida del techo). Va a confirmar con U = 0,1084 congelado, sabiendo que en explorar dio AUC 0,492.
- Finalista 2: NINGUNO. Ninguna variante cumple a la vez acierto >= empate, n >= 150 y >= 9 de 12 dias (V08 queda 0,1 punto abajo del empate; V03 5 puntos abajo).
- V09 + V10 pasan una vez por confirmar (no direccional).

---

## CONFIRMAR (8 sesiones, 08-09 al 17-09; 12.283 filas) — UNA pasada, todo congelado (`techo_ml_05_confirmar.py`, que se niega a correr dos veces)

### Finalista direccional V01 (xgb, 101 rasgos, +-8/600, U = 0,1084 congelado)
- AUC en confirmar: **0,470 +- 0,013** (error remuestreando dias). En explorar fuera de muestra habia dado 0,492 +- 0,010.
- Disparos del decil confiado: 348 (342 resueltos, 53 % largos). **Acierto 37,7 %** contra 56,0 % de empate. Placebo 48,3 +- 2,5 % -> **z = -4,3**.
  Dias arriba de 50 %: **1 de 8**. Por dia: 10/30, 25/61, 6/20, 12/42, 18/59, 23/49, 19/36, 16/45. Netos: **-2,92 pts por operacion**.
- Acierto por quintil de confianza: 52,0 / 52,4 / 50,6 / 48,7 / 42,6: cuanto mas seguro estaba el modelo, MAS se equivoco.
- **Veredicto: NADA.** Y no es "ruido alrededor de 50": lo que el modelo aprendio en las 12 sesiones de explorar salio AL REVES en las 8 de confirmar.
  (Ir en contra del modelo habria dado 62,3 % en confirmar, pero en explorar fuera de muestra daba 51,9 %: no es una regla, es una relacion que cambia de signo.)

### No direccional V09 y su control V10 (congelados; "grande" = R300 > 26,5 pts, la mediana de explorar)

| predictor | AUC confirmar | Spearman | AUC por dia |
|---|---|---|---|
| V09 xgb, todos los rasgos           | **0,899** | 0,779 | 0,85 a 0,93 (8 de 8 dias >= 0,65) |
| V10 control: rangos previos + reloj | 0,880 | 0,738 | 0,83 a 0,92 |
| solo el rango de los 300 s previos  | 0,838 | 0,669 | 0,78 a 0,87 |

- Criterio pre-registrado "sirve de filtro" (AUC >= 0,70 y >= 7 de 8 dias con AUC >= 0,65): **CUMPLE** (0,899; 8 de 8).
- Criterio "el flujo agrega sobre el reloj y la persistencia" (V09 - V10 >= 0,02): **NO CUMPLE** (0,019). Lo que predice el tamaño del movimiento es la
  volatilidad reciente y la hora; CVD, delta, OFI, absorcion, barridos, punta y gamma no agregan nada medible.
- Por quintil de rango previsto (cortes congelados de explorar: 19,9 / 23,4 / 28,6 / 38,1 pts):

| quintil | % de las filas | R300 mediano | % con R300 >= 16 | mediana hasta resolver +-8 | resuelto en <= 60 s | en <= 120 s |
|---|---|---|---|---|---|---|
| q1 (calmo)  | 28,7 | 14,8 pts | 42 %  | 144 s | 17 % | 40 % |
| q2          | 16,0 | 20,8     | 79 %  | 81 s  | 36 % | 67 % |
| q3          | 17,4 | 24,0     | 90 %  | 58 s  | 51 % | 79 % |
| q4          | 17,6 | 32,8     | 99 %  | 32 s  | 75 % | 94 % |
| q5 (movido) | 20,3 | 48,8     | 100 % | 14 s  | 95 % | 99 % |

Repite casi calcado lo de explorar. Veredicto de V09: no es gatillo (no dice para que lado); como FILTRO de cuando y de que tamaño, validado.

---

## POST-HOC (despues de ver confirmar; NO cambia ningun veredicto y NADA de esto esta validado)

`techo_ml_06_posthoc.py`: por que el modelo salio al reves?
- En confirmar el modelo COMPRO cuando el precio estaba bajo en el dia (posicion en el rango de la rueda 0,45, debajo del VWAP, -20 pts en la ultima hora) con el flujo
  de 15 min girando a favor (zD_900 +0,24), y VENDIO cuando estaba alto (posicion 0,70, +0,85 desvios sobre el VWAP) con el flujo de 15 min en contra (zD_900 -0,63,
  ret_900 -11). O sea: aprendio "el flujo de 15 min SIGUE".
- AUC CONJUNTO de los rasgos de 15 min contra +-8/600, explorar | confirmar (error por dias remuestreados):
  dr_900 0,520 +- 0,013 | **0,466 +- 0,012** · zD_900 0,518 +- 0,013 | **0,469 +- 0,010** · ret_900 0,511 +- 0,013 | **0,472 +- 0,012**.
  En las 12 sesiones viejas el delta de 15 min tiraba levemente a SEGUIR; en las 8 nuevas tira a DEVOLVERSE. Los de 5 min (dr_300 0,494 | 0,484, ret_300 0,493 | 0,476)
  tiran a devolverse en los dos tramos, mas en confirmar (coincide con la pista sin validar del coordinador: fadear el delta extremo de 5 min, 56 % -> 62 %).
  Los de 15 s y 60 s, la punta (qi_60 0,498 | 0,493) y el pasivo: nada en ninguno de los dos tramos.

`techo_ml_07_posthoc_regimen.py`: es el regimen de gamma (explorar casi todo bajo el zero, confirmar casi todo arriba)? **No.**
- dr_900 en explorar: bajo el zero 0,525 +- 0,015 (12 dias) y ARRIBA 0,512 +- 0,021 (3 dias). En confirmar: BAJO 0,461 +- 0,017 (6 dias) y arriba 0,468 +- 0,015 (7 dias).
  Las celdas cruzadas se parecen a su PERIODO, no a su lado del zero gamma. Lo que cambio fue el mercado entre un tramo y el otro, no de que lado del zero estaba el precio.
- Con 2 tramos no se puede decir mas. Si el coordinador quiere cerrar esto, la reserva (31-07 al 19-08) dice si el signo del flujo de 15 min es estable alguna vez; mi apuesta es que no.

`techo_ml_08_lectura_rango.py`: la parte no direccional SIN modelo (lo que el operador ya ve en el grafico). Segun el rango de los ULTIMOS 5 min:

| rango ultimos 5 min | % del tiempo (exp / conf) | rango mediano PROXIMOS 5 min | % con proximo rango >= 16 | mediana hasta resolver +-8 | sin resolver a los 180 s |
|---|---|---|---|---|---|
| < 16 pts  | 15 / 22 | 18,5 / 16,0 | 67 / 50 % | 97 / 125 s | 24 / 36 % |
| 16-24     | 26 / 27 | 20,8 / 19,2 | 76 / 70 % | 75 / 86 s  | 17 / 26 % |
| 24-32     | 21 / 17 | 25,5 / 25,5 | 86 / 86 % | 52 / 52 s  | 11 / 9 %  |
| 32-48     | 21 / 19 | 32,8 / 35,2 | 96 / 97 % | 31 / 29 s  | 3 / 3 %   |
| >= 48     | 16 / 16 | 50,0 / 48,8 | 99 / 99 % | 13 / 14 s  | 1 / 1 %   |

Y por reloj (hora de Nueva York, rango mediano de 5 min, explorar / confirmar): 09:30 63 / 50 · 10:00 47 / 43 · 10:30 39 / 34 · 11:00 31 / 33 · 11:30 30 / 26 ·
12:00 25 / 25 · 12:30 24 / 23 · 13:00 22 / 19 · 13:30 20 / 17 · 14:00 19 / 17 · 14:30 19 / 17 · 15:00 18 / 16 · 15:30 26 / 23.
Despues de las 13:00 NY el rango mediano de 5 min (16-19 pts) es apenas el ancho completo de una apuesta +-8: la apuesta tarda ~100 s de mediana y 1 de cada 2-3 veces
el mercado ni siquiera recorre 16 puntos en 5 minutos.

---

## VEREDICTO DE LA FAMILIA

**Como gatillo direccional: NADA, y es el hallazgo.** Con los 101 rasgos juntos (delta, CVD, OFI, pasivo, absorcion, barridos, delta por tamaño, velocidad, volatilidad,
rango, VWAP, hora, punta y niveles de gamma), un xgboost chico y una logistica, validando por sesiones enteras: AUC fuera de muestra 0,492 a 0,509 en explorar (8 variantes,
error ~0,010) y 0,470 en confirmar. El decil mas confiado nunca llego al empate; en confirmar acerto 37,7 %. No hay direccion a 10 minutos en estos datos, y lo poco que
un tramo parece enseñar, el tramo siguiente lo da vuelta. Ninguna regla a mano con estos mismos rasgos puede estar mejor parada que esto.

**Como filtro no direccional: si, validado, pero es trivial.** El tamaño del movimiento de los proximos 5 min se anticipa bien (AUC 0,86 en explorar y 0,90 en confirmar,
8 de 8 dias), y sale ENTERO de dos cosas que el operador ya tiene a la vista: cuanto se movio en los ultimos 5-30 min y que hora es. El flujo no agrega (+0,019 de AUC).

Variantes probadas: 10 (V01-V10). V11 y V12 no se definieron (condicion del pre-registro: AUC >= 0,53; no se cumplio).

### Que aprendi de la microestructura, para LEER mejor
1. El techo es la moneda. No es que falte "la variable": estan todas juntas y la maquina no le gana al azar ni adentro del mismo tramo (fuera de muestra, 20-08 al 04-09). El CVD sigue sirviendo para lo que ya
   se midio (explica la vela que se esta formando), no para la que viene.
2. El significado del flujo de 15 min CAMBIA de signo entre tramos de pocas semanas (seguir del 20-08 al 04-09, devolver del 08-09 al 17-09) y no lo explica el lado del zero gamma.
   Un sesgo direccional armado con "el CVD de 15 min viene subiendo" va a andar un mes y a romperse el otro.
3. La confianza del modelo no ordena el acierto (quintiles planos en explorar, invertidos en confirmar): no existe un "momento en que se sabe".
4. El tamaño si se sabe: rango de los ultimos 5 min + reloj. Con menos de 16 pts en los ultimos 5 min, la apuesta +-8 tarda ~2 min de mediana y un cuarto a un tercio sigue
   abierta a los 3 min; con mas de 32 pts se resuelve en ~30 s. Sirve para elegir el tamaño del objetivo y para no esperar un +-8 rapido a las 14:00 NY.
5. Trampa de metodo (vale para todo el proyecto): con rasgos DE NIVEL (distancia a la apertura, al VWAP, a un nivel de gamma, posicion en el rango, CVD del dia), las cuentas
   "por dia" (AUC o correlacion dentro de cada dia, "12 de 12 dias") estan sesgadas hacia la reversion: un precio sin memoria da 0,44 en 12 de 12 dias. Hay que juzgar con
   el conjunto entre dias o con el placebo por disparos, nunca con el promedio de cuentas intradia.
6. Los niveles de gamma (distancia a zero, dominantes, majors, Max Change, cuadrante) no le agregan direccion al modelo (sacarlos no cambia nada) ni tamaño (cero en V09).

### Archivos
Scripts en `laboratorio/gatillo/`: techo_ml_lib.py, techo_ml_01_tablero.py, techo_ml_02_explorar.py, techo_ml_03_importancia.py, techo_ml_04_sesgo_auc_dia.py,
techo_ml_05_confirmar.py, techo_ml_06_posthoc.py, techo_ml_07_posthoc_regimen.py, techo_ml_08_lectura_rango.py. Cache propio en `laboratorio/gatillo/techo_ml_cache/`
(tableros, objetivos por segundo, predicciones fuera de muestra, explorar_resumen.json, importancia_explorar.json, confirmar_resumen.json).
