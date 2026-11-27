# Ronda 23 — Juez en vivo de la candidata congelada A3 — PRE-REGISTRO

Escrito el **27-11-2026 a las 09:03 AR (UTC−3, reloj del sistema)**. La candidata, la semilla de historia y el juez quedaron congelados a las
**09:01:30** (`huellas_r23.json`). Todo esto es **anterior a cualquier jornada de la muestra en vivo**: la primera es el lunes 30-11-2026.
Correcciones: siempre como nota nueva al final, con hora, nunca reemplazando lo escrito. Nada de esto promete rentabilidad.

## 0. Qué se juzga
- **Una sola candidata: A3, "el elástico de la tarde después del giro"** (`cand_A3_tarde_giro_amplia.py`). Se eligió mirando datos que ya
  estaban vistos (MES de 5 min de 2025-26 y 55 ruedas de ES de sep-nov 2026; ver `RESULTADOS.md`). Por eso este juez es la primera muestra
  que nadie miró. A1 y A2 **no** se juzgan: no pasaron la vara en el chequeo (A1) o en el chequeo y la cinta (A2).
- Se juzga como **lectura con registro**, nunca como orden automática.

## 1. La regla (copia literal de lo que hace el script congelado)
Todo con lo sabido al cierre de cada vela de 5 min de la rueda (9:30-16:00, hora de Nueva York):
- **Escala:** X = 0,08 × mediana del rango de la rueda (9:30-16:00) de las 10 jornadas completas previas, al tick (la mitad redondea para arriba).
- **VWAP de la rueda** desde las 9:30:00: precio medio de cada orden agresora ((primero + último) / 2) ponderado por su volumen.
- **Balance inicial (IB):** máximo y mínimo de 9:30 a 10:30. Extensión del lado del precio = cuánto pasó el máximo (o el mínimo) del día al IB,
  en veces el ancho del IB (0 si no lo pasó).
- **Cuándo se mira:** en los cierres de vela de 13:00 a 15:30.
- **Condiciones (las tres):** (a) el precio está a 1,5 X o más del VWAP; (b) del lado del precio, el IB se extendió como mucho 1 vez su ancho;
  (c) la P geométrica = d_stop / (d_stop + d_VWAP) es 0,55 o más (el precio ya volvió desde el extremo: está más cerca del VWAP que del stop).
- **Operación:** hacia el VWAP (VENTA si el precio está arriba, COMPRA si está abajo) al precio del cierre de la vela.
  **Stop:** X/2 (al tick) más allá del extremo del día de ese lado. **Objetivo:** orden limitada en el VWAP (al tick), que se corre en cada
  cierre de vela al VWAP de ese momento; cuenta llena cuando una orden opera 1 tick más allá y se cobra al precio del límite. Stop lleno al
  tocarlo (si la orden ya llegó pasada, a su precio). Lo abierto se cierra con la última orden antes de las 16:00:00.
- **Una posición por vez** (la siguiente entrada, en un cierre de vela posterior a la vela que sigue a la de la salida); máximo 5 por rueda.
- **Costo del juez:** 1,00 pt de ES por operación completa.

## 2. La muestra en vivo
- **Cintas de ES** que baja la sonda en `%APPDATA%\ATAS\PythiaGex\flujo` (`cinta-ES-<desde>-<hasta>.csv` o `cinta-ES<contrato>-...`), una por
  jornada, **desde el 30-11-2026, en orden**, sin saltear ninguna que exista. Si hay varias del mismo día, se usa la que tiene `.listo` y, entre
  ellas, la de "hasta" más tardío.
- **Exclusiones por regla (no se juzgan):** feriados y ruedas cortas de la lista `DIAS_CORTOS` del juez (24-12, 25-12, 01-01, 18-01, 15-02 y
  26-03-2027); **cinta incompleta** (la última orden de la rueda antes de las 15:59:00, la primera después de las 9:31:00, o un hueco de más
  de 300 s entre 9:30 y 16:00). Una cinta incompleta **sin** `.listo` queda pendiente y se reintenta; con `.listo`, se excluye. Una rueda
  completa siempre suma su rango a la historia, aunque no se juzgue.
- **Historia:** la semilla `historia_rangos_semilla.json` (rangos de ES del 31-07 al 25-11-2026, la de la R19) más cada jornada completa que
  procese el juez (estado en `%LOCALAPPDATA%\PythiaGex_r23_vivo`).
- **Roll de diciembre (Z6 → H7):** no afecta. La regla usa precios del día y **rangos** de jornadas previas, que no cambian con el contrato.
- **Horario de verano:** lo resuelve la zona America/New_York (no hay tablas a mano).

## 3. La vara (decide)
Unidades: el **acierto** y el **neto** se cuentan por operación; el **t** es por jornada evaluada (neto con costo 1,00 sumado por jornada; las
jornadas sin operaciones cuentan 0). k = 1 candidata y 2 lecturas decisivas: **α por prueba = 0,05 / (1 × 2) = 0,025, de una cola.**
- **Lectura descriptiva a las 15 jornadas** (la bitácora de 15 sesiones del proyecto): se informa, **no decide**.
- **Lecturas de futilidad a las 20 y a las 40 jornadas evaluadas:** se ABANDONA si hay al menos 15 operaciones, el acierto es menor que 55 %
  **y** el neto por operación es ≤ 0. Esta lectura solo puede cortar, nunca aprobar.
- **Lecturas decisivas a las 60 y a las 120 jornadas evaluadas** (cada una con las primeras 60 / 120 jornadas, no con las que vengan después):
  - **PASA** si se cumplen las cuatro: (1) acierto ≥ 60 %; (2) neto > 0 por operación con costo 1,00; (3) t por jornada ≥ t crítico de
    Student con α = 0,025 y gl = jornadas − 1 (**2,00 a las 60; 1,98 a las 120**); (4) al menos 40 operaciones.
  - **NO PASA** si falla (1) o (2) en una lectura decisiva (se termina ahí).
  - **SIN DECIDIR** si (1) y (2) están bien pero falta (3) o (4) a las 60: sigue hasta 120. **A las 120 es final:** PASA o NO PASA.
- Se anotan y se informan, **sin decidir**: el acierto de la referencia al azar con la misma ubicación (200 caminos reflejados con velas de
  5 min de la misma cinta), la P geométrica de cada operación, el recorrido a favor y en contra, y la extensión del IB.

## 4. Lo que se puede esperar (potencia, medida antes de mirar el vivo)
El neto por jornada tuvo un desvío de 11-14 pts en las tres muestras y una media de +2,34 (diseño), +0,65 (chequeo) y +3,57 (cinta de ES).
Con esos números, la probabilidad de PASAR el t a las 60 jornadas es ~30 % si el efecto real es como el del diseño, ~6 % si es como el del
chequeo y ~60 % si es como el de la cinta; a las 120, ~54 %, ~8 % y ~88 %. **Un "SIN DECIDIR" a las 60 es lo más probable aunque la regla
funcione**; un "NO PASA" por acierto o neto es información fuerte en contra.

## 5. El juez ciego histórico (ES de 2023, si el operador lo compra)
- **Muestra:** tbbo de Databento de ES (contrato del frente) en 2023, nunca vista por ninguna ronda. Comprarla es decisión del operador.
- **Conversión al formato de la sonda:** una fila por operación de tbbo: `t` = ts_event en UTC con milisegundos (`AAAA-MM-DDTHH:MM:SS.mmm`),
  `primero` = `ultimo` = price, `vol` = size, `lado` = +1 si el agresor compró y −1 si vendió. La regla usa solo hora, precio y volumen (no usa
  `lado` ni la punta), así que alcanza. **Antes de juzgar**, el convertidor se valida con una jornada que tenga las dos fuentes (por ejemplo,
  comprar el 25-11-2026, ~1 USD): rango de la rueda idéntico, VWAP a menos de 0,25 pts en cada cierre de vela y las mismas operaciones de A3.
  Una cinta por rueda: `cinta-ES-<desde>-<hasta>.csv` con su `.listo`.
- **Cómo se corre:** `python -I r23_vivo.py --carpeta <carpeta de cintas 2023> --estado <carpeta nueva> --desde 2023-01-01 --sembrar-desde-cintas`.
  Las 10 primeras jornadas completas son calentamiento (arman la mediana de 10); las ruedas cortas y feriados de 2023 se excluyen solos por la
  regla de cinta incompleta.
- **Vara:** una sola lectura con todas las jornadas evaluadas del período comprado: las mismas cuatro condiciones, con α = 0,05 de una cola
  (k = 1, una lectura). Potencia con el desvío medido: 63 ruedas (jun-ago) ~44 % con el efecto del diseño; 125 ruedas ~68 %; 250 ruedas ~91 %.
- **Si existen los dos jueces**, la candidata queda **confirmada** solo si PASA en los dos. Si existe uno solo, ese decide.

## 6. Cómo se corre el vivo
1. Después del cierre, bajar la cinta del día con la sonda (como para la R21).
2. `python -I r23_vivo.py` en `PythiaGex\laboratorio\ronda23_diseno`: procesa todas las jornadas nuevas en orden, anota las operaciones en
   `bitacora_r23_vivo.csv` y la jornada en `bitacora_r23_jornadas.csv` (en esta carpeta), y muestra el estado del juez.
3. `python -I r23_vivo.py --resumen` muestra solo el estado. `python -I r23_vivo.py --verificar 2026-11-24` recorre una jornada ya conocida y la
   compara con el diseño (no escribe nada): sirve para comprobar que el juez sigue andando igual.
Si cambió cualquier archivo con huella, el juez se niega a correr.

## 7. Lo que no se puede hacer
Cambiar el script de la candidata, sus parámetros, el costo, las lecturas, las exclusiones o la semilla; agregar o sacar jornadas a mano;
"arreglar" una operación. Cualquier cambio es **otra candidata** y necesita su propio pre-registro con hora. Si aparece un error de plomería
(el juez se cuelga o lee mal una cinta), se corrige con nota fechada **antes** de mirar resultados nuevos y se recalcula todo desde el 30-11.

## 8. Huellas SHA-256 (las verifica `r23_vivo.py` antes de correr)
- `cand_A3_tarde_giro_amplia.py` 7e4e8a0ea36908b748c33371b4117d53e417ce77f5f20ed66526808509b640c5
- `historia_rangos_semilla.json` f02a0834b6a28bd4c9a311da93c2eb435176d88f13df200529544331340c5200
- `r23_vivo.py` 55dc6cfc4e36943fe60d4078df8766aba7ecae35ed698e3ba2e7bc03cce53945
- Comprobado antes de escribir esto: con estas huellas, `--verificar` reproduce exacto las operaciones de A3 del 16-09, 28-10, 20-11, 24-11 y
  25-11; una corrida simulada del 16 al 25-11 con historia hasta el 13-11 reproduce las 5 operaciones; y el modo histórico
  (`--sembrar-desde-cintas`) sobre las 57 cintas reproduce las 52 operaciones desde el 22-09 (las 10 primeras jornadas fueron calentamiento).
