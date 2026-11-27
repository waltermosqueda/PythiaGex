# Ronda 22 — El clima de la R21 en velas de 5 min (un año de MES) — PRE-REGISTRO

Escrito el **27-11-2026 a las 07:47 AR (UTC−3, reloj del sistema)**, con el código congelado a las **07:46** (huellas al final y en
`huellas_r22.json`), **ANTES de cargar el período independiente** (ruedas del 21-08-2025 al 31-08-2026 de `velas-MES-m5.csv`) y antes de
abrir las velas de 30 min de H1b. Correcciones: siempre como nota nueva al final, nunca reemplazando lo escrito.

Pedido (coordinador, 27-11): llevar el detector de clima D1 de la R21 ("cuentakilómetros") y el esqueleto de sus reglas a velas de 5 min,
probarlo UNA vez en un año de MES nunca usado para probar nada, partido por trimestre y por volatilidad, y explicar la mecánica. Nada de esto
promete rentabilidad.

## 0. Lo hecho ANTES de este pre-registro (y lo que NO)
- **Lectura:** R21 (RESULTADOS §2, §7, §11, §12; PREREGISTRO; `regla_congelada_comb_t7_v4.py`; `r21_01_regimen.py`; `r21_comun.py`;
  `r21_02/03/04/06`), R19 (`r19_01_rasgos.py`, `r19_02_motor.py`, `r19_08_eventos.py`, `finalistas.py`), las reglas de investigación
  (búsqueda sin parar, de menor a mayor, explicar el movimiento, baúl) y el baúl (#19, #42-57).
- **Inventario** (`r22_00_inventario.py`, `salidas/inventario_m5.csv`, `inventario_m30.csv`): solo conteos de velas por jornada, huecos,
  medias ruedas y el salto entre el cierre de una jornada y la apertura de la siguiente. Para medir el cambio de contrato del continuo comparé
  el cierre de cada bloque de 15 min del continuo con `velas-MESU6-m15.csv` (diferencia de precios entre contratos, no resultados).
  **No calculé rangos, estados, eventos ni resultados del período independiente.**
- **Calibración** (`r22_01_calibracion.py` → `salidas/calibracion.txt/json`; `r22_01b_calibracion_resultados.py` → `salidas/calibracion_b.txt`),
  SOLO en fechas con cinta (ninguna del período independiente):
  (A) las 4 fechas de `velas-MES-m5.csv` con cinta de ES: 08-09, 17-09, 18-09 y 21-09-2026 (el archivo de 5 min **no tiene** del 09 al 16-09).
  Para la mediana de 10 del 08-09 leí las velas de MES desde el 20-08-2026 **solo como historia** (rango de la rueda; la clase Historia arma
  también niveles de ayer, semana anterior y acumulados del VWAP de esos días): ningún estado, evento ni resultado del período independiente.
  Para 17/18/21-09 la historia de 5 min está cortada y usé la mediana de la R19 (la misma de la R21).
  (B) robustez de la resolución: velas de 5 min ARMADAS desde la cinta de ES en las 55 ruedas de la R21 (+ 08 y 09-09), mismo instrumento y
  misma historia: mide solo lo que se pierde por mirar velas de 5 min.
- **Prueba de plomería** de `r22_02_corrida.py` (que no se cuelgue) con fechas fuera del período (5 min: evaluadas 03..21-09-2026; 30 min: un
  tramo de abril-junio de 2024) con la salida suprimida; las salidas de esa prueba se borraron sin mirarlas.

### Lo que dejó la calibración (fidelidad, nunca resultado del período independiente)
- **Estado D1 cada 5 min:** (B) coincide **100,0 %** con la cinta en el mismo instante (3.819 puntos, 57 ruedas; TENDENCIA 662 de 662);
  contra el minuto siguiente (los puntos de control de la R21) 98,5 %. (A) con velas de MES: **99,6 %** (268 puntos, 4 ruedas). Control: el
  rango relativo por minuto recalculado desde la cinta es idéntico al de la R21 (0 diferencias en 21.450 minutos).
- **Mediana de 10:** 08-09, velas de MES 44,38 contra 43,75 de la R19 (X 3,50 en las dos).
- **VWAP:** precio por vela elegido **(máximo + mínimo) / 2** (error mediano contra la cinta 0,13 pts, p90 0,46; HLC/3 0,19; OHLC/4 0,17;
  cierre 0,55). Desvío con la varianza dentro de la vela (rango²/12): error mediano 0,6 % contra 2,8 % sin ella. Con MES: 0,28 pts.
- **Orden dentro de la vela:** regla "por color" (vela alcista: apertura → mínimo → máximo → cierre; bajista al revés) acierta **87,9 %** de
  15.732 velas de la cinta; "el extremo más cercano a la apertura primero" 82,7 %.
- **Primer paso (L2):** con el VWAP del cierre de cada vela y los empates resueltos por el camino, coincide **99,5 %** con la etiqueta de la
  R21 recalculada en el mismo instante ("extiende" 36,7 % contra 37,0 %).
- **Delta:** el delta de 5 min del MES tiene el mismo signo que el del ES en **72,4 %** de las velas (correlación 0,60).
- **Vueltas de la ola (lo que más se pierde):** las velas de 5 min ven solo **53 % de las vueltas** de la cinta (las que duran más de una vela);
  el 95 % de las vueltas de 5 min son reales. En las parejas, estado, marea, VWAP, nivel en la zona, barrida y banda coinciden 94-99 %; el
  delta "de la vela previa" contra el de 300 s de la cinta solo 72 % (55 % con el delta del MES).
- **Hallazgo que cambia el diseño — la entrada a stop dentro de la vela NO se puede traducir sin sesgo.** En las ruedas de la R21 (ya vistas),
  T7-S6 con la entrada a stop y el orden supuesto dentro de la vela da 86,6 % (n 67); con la regla conservadora (si la vela toca la entrada y el
  stop, cuenta como stop) da 29,2 % (n 48); la cinta, en las mismas ruedas, 62,8 % (n 86). Mecánica: la vela de 5 min esconde las vueltas
  que fallan dentro de la misma vela (el mínimo nuevo pisa al extremo viejo y la operación perdedora desaparece) y la regla conservadora
  cuenta como pérdida cualquier vela ancha. La única traducción sin ese sesgo es **entrar al cierre de la vela donde se dio vuelta la ola**:
  en las mismas ruedas da T7-S6 54,4 % (+0,99), T6-P1 56,0 % (−0,66), COMB 57,4 % (+1,14), V4e-S6 64,1 % (+1,77) contra 62,8 / 64,9 / 63,1 /
  69,2 % de la cinta. Por eso **las 4 variantes de H2 usan la entrada al cierre**; las dos de stop quedan como cotas descriptivas (no deciden).

## 1. Datos y muestras
- **Período independiente:** `velas-MES-m5.csv`, ruedas (18:00 NY de la víspera a 17:00 NY) del 21-08-2025 al 31-08-2026.
  - Completa = las 78 velas de 9:30 a 15:55 NY. **Cortas** (feriados con cierre 13:00 y medias ruedas: 01-09, 27-11, 28-11 y 24-12-2025;
    19-01, 16-02, 03-04, 25-05, 19-06 y 03-07-2026): fuera de todo (no se evalúan y no entran en la historia, regla de la R21 en vivo).
    **Incompletas por hueco de datos** (27-08-2025 con 77 velas, 20-02-2026 con 75, 19-03-2026 con 73): entran como historia (≥ 70 velas,
    como la R19) y no se evalúan.
  - **Calentamiento:** las 10 primeras ruedas completas (22-08 a 08-09-2025). **Evaluadas:** las ruedas completas desde el 09-09-2025 hasta el
    31-08-2026 (≈ 240).
- **H1b:** `velas-MES-m30.csv`, ruedas del 14-04-2024 al 20-08-2025 (anteriores al período de 5 min; según el baúl, nunca abiertas por ninguna
  regla); completas = 13 velas de 30 min de 9:30 a 15:30; las 10 primeras completas = calentamiento. Las velas de 60 min no sirven para la
  rueda (empiezan en hora redonda: la de 9:00 mezcla 9:00-9:30).
- **Cambio de contrato del continuo MES@CME:** el lunes de la semana del vencimiento. Spread nuevo − viejo: 15-09-2025 **+54,00 (estimado**:
  el MESU6 casi no operaba; da +53,9 con dos operaciones sueltas y +55 por proporción con la de diciembre; error posible ±5), 15-12-2025
  +57,25 (13 contra 114 velas del MESU6), 16-03-2026 +52,25 (73 velas), 15-06-2026 +62,50 (exacto: el continuo pasa a ser U6). Los niveles de
  varios días (ayer, perfil de la semana anterior, VWAP semanal y mensual) se llevan al contrato del día con esos spreads. En la semana del
  roll el VWAP semanal arranca el lunes (no lo afecta); el mensual y la semana anterior sí.

## 2. La traducción a 5 min (convenciones fijadas por la calibración; `r22_motor.py`)
- **Rango y D1:** al cierre de cada vela k de la rueda, rrn = (máximo − mínimo desde las 9:30) / (mediana de 10 × f(5k+4)), con la curva
  congelada de la R21 (`curva_rango_por_minuto.json`; en los cierres de vela f es exactamente la mediana medida). TENDENCIA si rrn ≥ 1,6 y el
  cierre está del mismo lado del VWAP de la rueda y de la apertura (ese es el lado); ROTACIÓN si 1,0 < rrn < 1,6; ESTRECHO si rrn ≤ 1,0;
  con rrn ≥ 1,6 y los lados en desacuerdo, no se opera.
- **Mediana de 10:** mediana del rango 9:30-16:00 de las 10 ruedas previas de la historia. **X** = 0,08 × mediana, al tick.
  **Mediana de 60** (solo para partir resultados): la de las 60 previas si hay ≥ 20.
- **VWAP de la rueda y de la jornada** (desde las 18:00): precio por vela (máx + mín) / 2 ponderado por volumen; desvío con la varianza
  dentro de la vela (rango²/12). Semanal (semana ISO) y mensual: acumulados de las jornadas previas de la historia + la jornada en curso.
- **Ola (X) y marea (4X):** zigzag punto por punto desde las 18:00 sobre el camino de 4 puntos de cada vela (apertura, extremo, extremo,
  cierre; orden "por color"), con la misma máquina de la R19.
- **Delta:** el de la vela de 5 min (compras agresoras − ventas agresoras del MES).
- **Niveles de la T7** (los 31 de la R19 + redondo de 50): VWAP de la rueda y de la jornada con ±1σ y ±2σ, semanal, mensual; ayer máximo /
  mínimo / cierre / POC / VAH / VAL (perfil de la rueda de ayer con el volumen de cada vela repartido parejo en su rango; área de valor con la
  regla de ATAS, copia de la R19); Asia (18:00-3:00) y Londres (3:00-9:30) máximo y mínimo; balance inicial (9:30-10:30) y sus extensiones de
  0,5 y 1 vez (solo si el extremo es de una vela que empieza a las 10:30 o después); POC/VAH/VAL de la semana anterior (perfil de todas sus
  jornadas). Los dinámicos se toman al cierre de la vela del extremo.
- **Evento = vuelta de la ola, entrada al CIERRE de la vela k donde se confirmó** (cierres de 10:00 a 15:25): lado = dirección de la ola al
  cierre; extremo = el pivote que terminó el retroceso; entrada = cierre de la vela; stop = extremo ∓ 2 ticks; R = distancia al stop.
  Todo con lo sabido a ese cierre: estado D1 de la vela k, marea al cierre de k, VWAP al cierre de k.
  - **T1:** marea a favor (dirección de la marea = lado) y VWAP de la rueda detrás (una compra por encima del VWAP, una venta por debajo).
  - **T7:** T1 en TENDENCIA del lado, y al menos una de: (a) el extremo en la zona de un nivel (de L − X/4 a L + T del lado del retroceso,
    T = X/8 al tick, mínimo 2 ticks); (b) el extremo perforó un nivel hasta X/2 y la entrada ya está del otro lado; (c) delta de la vela de la
    vuelta (los 300 s previos a la entrada) a favor.
  - **T6:** T1 en TENDENCIA del lado + delta de la vela de la vuelta a favor.
  - **V4e (esqueleto de la V4):** en ROTACIÓN, el extremo entre la banda de 2σ (VWAP de la rueda o de la jornada) del lado opuesto y X/2 antes,
    sin perforarla. **Sin iceberg: no se ve en velas de 5 min.**
- **Salidas:** S6 = objetivo 1R (lleno al pasarlo 1 tick, cobrado al precio del objetivo); P1 = mitad en 1R y el resto en 2R con el stop pasado
  a la entrada. **Regla conservadora dentro de la vela:** si el stop y el objetivo caen en la misma vela, cuenta como stop; en P1, si en la vela
  donde se llena 1R el precio pudo volver a la entrada, el resto se cierra en la entrada; un stop salteado por la apertura se llena en la
  apertura; lo abierto se cierra al cierre de la vela de 15:55-16:00.
- **Gestión:** una posición por vez (una entrada nueva solo en una vela posterior a la de la salida), máximo 5 por rueda.
- **Costos:** 0,50 y 1,00 pts por operación completa (por contrato).

## 3. Hipótesis y varas (corrida ÚNICA)
- **H1 (clima):** en los puntos de control cada 5 min de 10:00 a 15:30 (cierres de vela), cuando D1 marca TENDENCIA (con lado s), el extremo
  nuevo del día del lado s pasado por X/2 llega ANTES que un toque del VWAP de la rueda (que se mueve). Base = la misma proporción en TODOS los
  puntos de control. "Ninguno al cierre" cuenta en el denominador. **PASA si** (i) TENDENCIA ≥ 55 %, (ii) TENDENCIA ≥ base + 15 puntos y
  (iii) el IC 95 % agrupado por rueda (bootstrap de ruedas, 2.000) de la diferencia no incluye el 0. **Secundaria (se informa, no decide):**
  "sigue hasta el cierre" (cierre de 16:00 más allá del precio del punto, del lado s) en TENDENCIA contra la base, con su IC.
- **H1b (horizonte largo):** lo mismo en velas de 30 min (puntos 10:00, 10:30 … 15:30), 14-04-2024 a 20-08-2025, con la curva congelada (que
  no conoce ese período). Misma vara.
- **H2 (esqueleto operable), 4 variantes, todas con entrada al cierre:** **T7-S6**; **T6-P1**; **COMB-T7+V4e-S6** (T7 en TENDENCIA + V4e en
  ROTACIÓN, una posición entre las dos); **V4e-S6** (la rotación sola). **PASA una variante si** acierto ≥ 60 % Y neto > 0 con costo 1,00 Y
  t por rueda con costo 1,00 (ruedas sin operaciones = 0) ≥ el t de Bonferroni de una cola con k = 4 (α 5 %, gl = ruedas − 1; ≈ 2,25), con
  ≤ 5 operaciones por rueda (por construcción).
- **Particiones pre-registradas (informan, no deciden):** por trimestre (2025T3 = 09 a 30-09-2025, 2025T4, 2026T1, 2026T2, 2026T3 = julio y
  agosto de 2026); por volatilidad (tercil de la mediana de 10 entre las ruedas evaluadas); por **clima heredado** = mediana de 10 / mediana de
  60 (terciles; "las 10 previas más agitadas que las 60" es el caso de MNQ en agosto); en COMB, cada familia por separado; la distribución
  del semáforo por trimestre; y el tipo de día al cierre (eficiencia |cierre − apertura| / rango: tendencia ≥ 0,6, balance < 0,3).

## 4. Robustez y descriptivos (rotulados, NO deciden)
- **f(m) sin el trimestre que se prueba:** la curva rehecha con el procedimiento de la R21 (MES 5 min, ruedas < 08-09-2026 con 78 velas) dejando
  afuera el trimestre; H1 y H2 por trimestre con esa curva. Control: sin dejar nada afuera debe reproducir la congelada.
- **f(m) externa:** medida en las velas de 30 min de 2024-04 a 2025-08 (que la R21 no vio), interpolada a cada minuto; H1 y H2.
- **Empates del primer paso** (extremo y toque en la misma vela) contados como "no extiende".
- **Cotas descriptivas** de T7-S6 y T6-P1 con entrada a stop dentro de la vela (orden supuesto = cota alta; regla conservadora = cota baja).
- **Placebo PD** (mismas entradas con el lado al azar y el mismo R, 2.000 sorteos; p = fracción con neto medio ≥ el real).
- **Control geométrico de H1:** TENDENCIA contra el resto dentro del mismo casillero de distancia al extremo (en X) × distancia al VWAP (en X)
  × franja horaria: cuánto de H1 es clima y cuánto es geometría (en TENDENCIA el precio suele estar cerca del extremo y lejos del VWAP).
- **Mecánica** (después de la corrida, post hoc): qué separa ganadoras de perdedoras (piezas de la T7, hora, rrn, R contra el rango del día,
  clima heredado, excursiones); todo lo que salga de ahí es pista para el baúl y la bitácora, nunca probado.

## 5. Limitaciones declaradas antes de mirar
1. **La forma f(m) fue medida con estas mismas velas** (256 sesiones de MES de 5 min del 22-08-2025 al 04-09-2026, R21). Es una forma de
   rango (no usa resultados), pero conoce los días que se prueban; por eso los dos controles de la sección 4.
2. **El 5 min ve la mitad de las vueltas de la ola** y no puede reproducir la entrada en el segundo de la vuelta (sección 0): H2 prueba el
   esqueleto a la escala de la vela, no la regla de la cinta. Lo que pase o no pase en H2 no confirma ni refuta la COMB congelada en vivo.
3. **V4 sin iceberg** (V4e); **delta del MES** como proxy del ES (72 % de mismo signo).
4. El spread del roll del 15-09-2025 es estimado (±5 pts); solo toca niveles de varios días en septiembre de 2025.
5. MES y ES: mismo precio (diferencias de 0-1 tick medidas en las 4 ruedas comunes); costos en puntos de ES.

## 6. Huellas SHA-256 (verificadas por `r22_02_corrida.py` antes de correr; si alguna cambia, no corre)
- `ronda22_clima_5min/r22_comun.py` 532b917bdcd767b69c5a546f8d224b8c2a272d687f9a767d009d6da1994cc751
- `ronda22_clima_5min/r22_motor.py` ff6f27d97c0be7adacbba33478fcb2433235b82a1429ec450c4b41b7e969ea72
- `ronda22_clima_5min/r22_02_corrida.py` b5a6299223b83bbdd8359de27f44aa4ca76d296b84a847f7d82a7bcaab590500
- `ronda22_clima_5min/salidas/calibracion.json` 43e30771fec30e1d481d6bcce597c0ceada7800139c572a7b398217c209767ee (convenciones)
- `ronda21_regimen/curva_rango_por_minuto.json` 72589182d1533ccab0903e70469e8654dc7a201197d88064238dd1a2d7a6bc33 (la de la R21)
- Registrados (no los verifica la corrida): `r22_00_inventario.py` f052501d…d338, `r22_01_calibracion.py` 6ac54004…73f1,
  `r22_01b_calibracion_resultados.py` 9ce927da…2dc4.
Si hiciera falta corregir un error de plomería (un cuelgue), se anota acá con su huella nueva ANTES de mirar resultados; ningún parámetro se
toca. La corrida se hace UNA vez.

---
## Resultado de la corrida única — 27-11-2026 07:48 AR (reloj del sistema; detalle en `salidas/h1.txt`, `h2.txt`, `h1b.txt`, `robustez.txt`)
Corrida UNA vez a las 07:47, con las huellas verificadas (5 de 5) y sin errores de plomería; no se tocó ningún archivo congelado ni parámetro.
- **H1: NO PASA.** TENDENCIA 53,6 % contra 41,3 % de base, +12,2 [IC +2,6 a +21,3] (1.436 puntos; 69 de 242 ruedas). Sigue hasta el cierre
  45,2 contra 46,9 %. Ningún trimestre pasa; por volatilidad pasaría solo el tercil medio (55,2 %, +16,6).
- **H1b: NO PASA** (por 0,6 puntos del +15): 55,3 % contra 40,9 %, +14,4 [IC +7,8 a +20,8]; sigue hasta el cierre 47,3 contra 51,5 %.
- **H2: no pasa ninguna** (todas con neto negativo): T7-S6 46,2 % (−2,12 con costo 1,00), T6-P1 48,0 % (−2,87), COMB 43,2 % (−2,12, t −3,61),
  V4e-S6 41,6 % (−2,20, t −3,67).
- Robustez: con f sin el trimestre, H1 da 53,3 % (+12,0); con f externa, 52,0 % (+10,7); H2 queda igual.
- Después de esto se escribieron, POST HOC, `r22_03_mecanica.py` (mecánica) y `r22_04_verificar.py` (comprobación desde el crudo con código
  propio: 10 de 10 coinciden). Lo que salió de la mecánica es pista para el baúl (#58-#64), no prueba.
