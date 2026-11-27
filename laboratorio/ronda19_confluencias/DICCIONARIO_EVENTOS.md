# Base de eventos de la ronda 19 — diccionario (27-11-2026 03:32 AR)

**Archivo:** `C:\Users\wmx_7\AppData\Local\Temp\claude\6e29e09c80928880\233bf8d4-5113-4e95-a0d9-68bff726690d\scratchpad\r19\eventos_todos.parquet`
(4.822 eventos × 154 columnas; ~103 por rueda). Copia solo de diseño: `...\scratchpad\r19\eventos_diseno.parquet` (2.250).
Lo arma `r19_08_eventos.py` desde los rasgos de `r19_01_rasgos.py` (cinta orden por orden de ES, todo causal).

**Muestras (columna `muestra`):** `diseno` = ES 10-09 a 15-10 (26 ruedas; 08 y 09-09 son calentamiento de umbrales, sin eventos);
`confirma` = ES 16-10 a 13-11 (21 ruedas). **Explorar y elegir SOLO con `muestra == "diseno"`.** La confirmación se abre UNA vez,
para las finalistas. La guardada (MES 16-25-11) no está en la base y no se toca.

**Qué es un evento:** cada VUELTA DE LA OLA (zigzag de X = 0,08 × mediana del rango RTH de las 10 ruedas previas) confirmada entre
las 9:30 y las 16:00 NY, en las dos direcciones. `lado` = +1 si la ola se dio vuelta para arriba (compra), −1 para abajo (venta).
`a_favor_marea` = 1 si va a favor de la marea (zigzag de 4X). La entrada es el último precio del segundo de la vuelta (`entrada`);
el stop está detrás del extremo del retroceso que terminó (`stop` = `ext` ∓ 2 ticks); `R` = |entrada − stop| (≈ X + 2 ticks).

**Columnas por grupo**
- Identidad y tiempo: `inst, dia, muestra, X, t_utc_ms` (ms UTC de la orden que confirmó la vuelta), `t_utc`, `hora_ny`,
  `seg_rueda` (segundos desde las 9:30), `minuto`, `px_vuelta` (precio de esa orden), `entrada, ext, hora_ext, stop, R, R_en_X`,
  `coherente` (1 si la grilla de 1 s coincide con el pivote; 99 %).
- Marea: `marea_dir, a_favor_marea, marea_con_agresores` (delta de su pierna con su signo), `marea_delta, marea_ext, marea_p0`,
  `extension_fuera_ruido` (el pivote que inició la marea quedó fuera del área de ruido del lado opuesto al evento: la base de la
  "vuelta al valor") y `extension_fuera_ruido_contra`.
- Retroceso que terminó (medido en el segundo anterior a la vuelta), prefijo `ctx_`: `prof_X` (desde el extremo de la marea),
  `del_ret` (delta del retroceso), `ret_con_agresores`, `vel_ret` (pts/s), `dur_ret_s`, `ext_ib` (balance inicial: en_formacion,
  ninguna, arriba, abajo, ambas), `ruido` (dentro / fuera_a_favor / fuera_en_contra), `en_va_ayer`, `dist_vwap_sd`, `pierna_marea_X`.
- Régimen y día: `ruido_a_favor_en_vuelta, ruido_en_contra_en_vuelta, ruido_sigma, mov_desde_apertura, eficiencia_10_previas,
  regimen_va_y_vuelve` (baúl #19: eficiencia mediana de las 10 ruedas previas < 0,465).
- VWAP en la vuelta: `vwap_rth, vwap_rth_sd, vwap_rth_dist_pts` (firmada a favor del lado: negativa = el VWAP queda del lado de la
  ganancia), `vwap_rth_dist_sd`, ídem `vwap_jornada_*`, `vwap_semanal(_dist_pts)`, `vwap_mensual(_dist_pts)`, `ext_dist_vwap_rth_sd`.
- Niveles en el segundo del extremo: `d_<nivel>` = extremo − nivel (pts; NaN si el nivel no existía: IB antes de las 10:30) para
  vwr, vwr±1, vwr±2 (VWAP RTH y bandas), vwj, vwj±1, vwj±2 (jornada), vws, vwm, pdh, pdl, pdc, pd_poc, pd_vah, pd_val, asia_h/l,
  lon_h/l, ib_h/l, ib_h05/h10/l05/l10, ps_poc/vah/val (semana anterior), r50 (redondo de 50 más cercano).
  `niveles_en_zona` (lista; zona = L − X/4 .. L + T del lado del retroceso, T = X/8 al tick, mínimo 2 ticks), `n_niveles_en_zona`,
  `barrida_y_vuelta` (niveles que el extremo perforó hasta X/2 y el precio ya volvió del otro lado; "nivel:penetración") y su cuenta.
- Order flow del retroceso (ventana de 60 s que termina un segundo antes de la vuelta; umbrales = percentiles por franja de 30 min
  de las 10 ruedas previas): `F1` absorción del lado que defiende (p90), `F2` iceberg a ≤ X/2 del extremo, `F3` divergencia de delta
  (micro X/2), `F4` agotamiento (volumen por tick del último X/4 ≤ mitad del retroceso), `F5` apilado de 3+ imbalances diagonales
  300 % a favor en la vela de 1 min cerrada, `F6` big trades netos a favor ≥ p80; `veto_caudal` (venta/compra de 30 s ≥ p99,5 en el
  extremo), `puntaje_F1_F6`. Crudos `of_*`: `absb60/absa60` (agresión que no movió el bid/ask), umbrales `u_*`, `abs_lado_pasivo_60`
  y `_rel` (sobre su p90), `netbig60(_a_favor)`, `icebergs_60` (segundos con recarga del lado que defiende) y `_contra`, `delta_60`
  y `delta_300` (firmados a favor del lado), `vol_60`, `bigc_60/bigv_60`, `apilados_favor/contra_3v` e `imb_favor/contra_3v` (3 velas de
  1 min), `cvd`.
- Resultados desde la entrada (pts de ES por contrato, SIN costo; restar 0,50 y 1,00): `S1_*` objetivo 2R, `S2_*` 3R, `S3_*` nivel
  siguiente entre 1R y 6R (o 3R; `S3_objetivo`), `S4_*` raya de la marea (solo si va a favor de la marea; si no, NaN), `S4b_*` stop que
  persigue a 4X del mejor precio, `S5_*` VWAP RTH (solo si queda del lado de la ganancia), `S6_*` objetivo 1R. Cada uno con `_pts`,
  `_motivo` (stop / objetivo / marea / persigue / vwap / fin) y `_seg_salida` (segundo de la rueda en que sale). Los objetivos exigen
  pasar 1 tick; el stop sale al tocar; todo se cierra a las 15:59:59. `mfe_R, mae_R` (excursión máxima a favor y en contra hasta el
  stop o el cierre) y `seg_hasta_stop`.

**Para simular una familia sobre la base:** filtrar en diseño, recorrer por rueda en orden de `seg_rueda`, una posición por vez
(la siguiente entrada debe ser posterior a `<salida>_seg_salida` de la anterior), máximo 5 por rueda, y juzgar con t POR RUEDA
(ruedas sin operaciones = 0), acierto y esperanza neta con costo 0,50 y 1,00. Cada variante probada va a `registro_variantes.csv`.

**Línea de base del diseño (todas las vueltas, sin costo):** S1 31,0 % de acierto, −0,09 R; S6 46,2 %, −0,07 R; S5 38,8 %, −0,02 R;
S4 (a favor de la marea) 22,8 %, +0,01 R. Entrar en la vuelta de la ola pierde un poco en este período (régimen "va y vuelve").
