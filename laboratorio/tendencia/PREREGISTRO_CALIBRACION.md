# Pre-registro: calibrar "PythiaGex - Corriente" para que avise antes sin sumar ruido (MES 1 min)

Escrito el 2026-10-29 ~11:25 AR, **antes de calcular ningún estado de ningún candidato** sobre estas velas. Lo único
mirado antes: inventario de la exportación nueva (37 sesiones, 09-08..10-28, idéntica a lo anterior en el solape) y lo ya
publicado en `INFORME.md` sobre SuperTrend(10,3) en MES 1 min (114 movimientos, 6,5 min, 13 de 20 pts; 37 cambios por
sesión). Pedido del operador: "avisa demasiado tarde y te perdés las tendencias, más en días volátiles; calibralo sin
sumar ruido ni muchos falsos positivos, hasta los límites posibles". Esto no se edita después de la primera corrida;
cambios van en "Enmiendas" con fecha y se marcan post-hoc.

## 1. Datos

- Serie única MES 1 min, continua como en ATAS: `velas_MES_m1.parquet` de este laboratorio (ronda3 + cache 10-07, 45
  sesiones 08-04..10-05) + `tres/datos/velas_cache_2026-10-29/velas-MES-m1.csv` con `t` posterior. Reglas de sesión,
  bloques y calentamiento (100 velas) = PREREGISTRO.md sección 1.
- **DISEÑO** = sesiones <= 2026-10-05 (45). **EXAMEN** = sesiones 2026-10-06..2026-10-28 (17), nunca usadas para
  tendencia. Hoy (10-29, incompleta) no entra.
- **Días volátiles** = el tercio de sesiones con mayor rango de la rueda (máximo - mínimo de las velas enteras entre
  9:30 y 16:00 NY), calculado dentro de cada tramo (15 de 45 en DISEÑO, 6 de 17 en EXAMEN).

## 2. Medidas (todas al cierre de vela, causales)

- **Retraso** = movimientos grandes de PREREGISTRO.md sección 4 (MES >= 20 pts en <= 30 min, inicio = extremo local
  +-30 min, separación 30 min). Por movimiento: primera vela desde el inicio (incluida) con el estado a favor, hasta 120
  min; **puntos tarde** = |cierre de esa vela - extremo de inicio|; si no se da vuelta en 120 min, puntos tarde = infinito.
  Resumen: mediana de puntos tarde (con los infinitos), mediana de minutos (censura 120), % que se da vuelta antes del
  umbral. **Secundario**: movimientos de >= 10 pts en <= 15 min (inicio +-15 min, separación 15 min).
- **Ruido**, por sesión (vela del cambio válida y la anterior también, mismo bloque):
  - cambios = velas con estado distinto al de la anterior;
  - **vueltas falsas** = cambios cuyo tramo (desde la vela siguiente al cambio hasta la vela que vuelve a cambiar,
    incluida, o el fin del bloque) nunca se aleja 5 pts a favor del cierre de la vela del cambio (5 pts = su stop de 20
    ticks). Sensibilidad: 2,5 pts (su TP de 10 ticks) y 10 pts.
  - Se informa media por sesión.

## 3. Candidatos (80, fijos)

Base **B** = SuperTrend(ATR 10, mult 3) tal cual `t_lib.det_supertrend` (= Corriente 1.0c salvo la semilla del ATR).

- **F1 SuperTrend(n, m)**: n en {5, 7, 10, 14, 20, 30, 60} x m en {1,5; 2; 2,5; 3; 3,5; 4} (42).
- **F2 SuperTrend con fuente = cierre** (bandas = cierre +- m ATR(10), mismo trinquete): m en {2; 2,5; 3; 3,5; 4; 4,5} (6).
- **F3 multiplicador adaptado por eficiencia** (Kaufman ER(k) del cierre, 0 si el denominador es 0):
  m_t = m_bajo + (m_alto - m_bajo) x (1 - ER_t), banda con ATR(n): k en {10, 20} x (m_bajo, m_alto) en {(1,5; 3,5),
  (2; 4), (1,5; 4,5), (2; 3,5)} x n en {10, 20} (16). Mismo trinquete que SuperTrend.
- **F4 Chandelier**: largo: tope = máximo de los últimos L máximos - m ATR(10), solo sube mientras sigue alcista; corto
  al espejo; cambia cuando el cierre cruza el tope vigente: L en {10, 22} x m en {2; 2,5; 3; 3,5; 4} (10).
- **F5 SuperTrend con dos cierres**: cambia recién con 2 cierres seguidos más allá de la banda final (con la banda de
  cada vela): n en {10, 20} x m en {1,5; 2; 2,5} (6).

## 4. Elección (en DISEÑO) y examen (en EXAMEN)

- **Admisible sin ruido**: cambios por sesión <= B y vueltas falsas (5 pts) por sesión <= B, en DISEÑO.
- **Elegido C** = el admisible de menor mediana de puntos tarde (movimientos de 20); empate: menor mediana en días
  volátiles; después, menos vueltas falsas. Si ningún admisible mejora a B en al menos 1 pt de mediana, se informa que
  sin ruido no hay mejora y la frontera (abajo) queda para que el operador elija.
- **Frontera** (informativa): el de menor mediana de puntos tarde con ruido hasta +10 %, +25 % y +50 % sobre B.
- **Examen de C contra B en EXAMEN** (pasa si se cumplen las dos):
  1. Retraso: diferencia por movimiento (puntos tarde B - C, mismos movimientos) con prueba de signos unilateral
     p <= 0,05 a favor de C (infinito > cualquier número; empates afuera) y mediana de C menor que la de B.
  2. Ruido: cambios por sesión y vueltas falsas por sesión de C <= 1,10 x B.
  Se informa además los días volátiles del EXAMEN (descriptivo).
- Si pasa: Corriente 1.1 con C por defecto, avisando al operador y con captura antes/después. Si no pasa: no se cambia
  el defecto y se le dice con los números.

## 5. Lo que esto NO mide

Ninguno de estos detectores anticipa la dirección (INFORME.md parte a). Esto mide solo cuán rápido se pone del lado de un
movimiento que YA ocurrió y cuántas veces cambia de color sin motivo. No es una estrategia ni promete ganancia.

## Enmiendas

(ninguna al momento de escribir)

**Enmienda 1 (2026-10-29 ~12:10 AR, DESPUÉS de la primera corrida; todo lo que sigue es post-hoc).**
- F5 "dos cierres", tal como quedó escrito, casi nunca cambia (0,07 cambios por sesión): después del primer cierre más allá
  de la banda, el trinquete de SuperTrend resetea la banda a la básica y el segundo cierre ya no la cruza. Es un defecto de
  la definición, no un resultado: F5 no cuenta.
- `cal_01.py` calculó todo y se cortó al imprimir (tipo de dato); `cal_01b_informe.py` imprime con la misma lógica leyendo
  `cal_resumen.csv` y `cal_movimientos.csv`, sin recalcular.
- Veredicto pre-registrado: C = ST(60; 3,5) (mejora en DISEÑO 0,38 pts, menos de 1) NO PASA el examen (15,5 contra 13,5
  pts; antes en 9 de 26 movimientos distintos, p 0,96).
- `cal_02_frontera.py` (post-hoc): ruido medido en la rueda y comparación movimiento por movimiento de ST(60,3), ST(60;2,5)
  y ST(60,2) contra la base. Ver `CALIBRACION_INFORME.md`.
