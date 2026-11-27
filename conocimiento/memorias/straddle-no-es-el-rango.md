---
name: straddle-no-es-el-rango
description: "16-11: el straddle 0DTE de apertura mide la distancia apertura->cierre, NO el rango maximo-minimo; el rango del dia termina en ~1,9x el straddle (mediana, 46 ruedas). Error mio en vivo: dije 'el tamaño ya se cumplio' con 1x."
metadata:
  node_type: memory
  type: feedback
  originSessionId: b1346044-cbec-4f19-80c3-ffb1de06776c
  modified: 2026-11-17T03:11:09.440Z
---

Medido 16-11 sobre `laboratorio/informe_diario/r2_regimen/tabla_opciones_merc.csv` (46 ruedas, 2026-09-08 a 11-13, foto CBOE en la
apertura, edad mediana 0,7 min): rango RTH / straddle ATM 0DTE de apertura -> p10 1,12 · p25 1,43 · **mediana 1,86** · p75 2,46 · p90 3,27.
El rango supero 1x el straddle en 45 de 46 ruedas (98 %), 1,5x en 67 %, 2x en 43 %, 2,5x en 24 %. Teoria (paseo al azar): straddle =
sqrt(2/pi)·sigma y rango medio = 2·sqrt(2/pi)·sigma = 2x straddle: lo medido coincide. (11-05 da 335 pts de rango, razon 8,7: revisar el dato.)

El 16-11 en vivo dije a las 10:20 NY "48,5 pts = casi todo lo esperado del dia" y a las 12:00 "el tamaño ya se cumplio de sobra" con
straddle 51: el dia termino la tarde en 104,5 pts (2,05x, una rueda tipica). Corregido de frente a las 14:30.

Factor intradia (R2, `r2_regimen/tabla_faltante_wf.csv`, 201 ruedas 27-01..13-11, rango final / rango hecho, mediana y p90):
10:00 x2,31 (3,91) · 10:30 x1,76 (2,77) · 11:00 x1,49 (2,39) · 12:00 x1,21 (2,06) · 13:00 x1,09 (1,66) · 14:00 x1,00 (1,36).
Ningun modelo con escala le gano a "lo hecho x factor" de 10:00 a 14:00; desde 14:30 ayuda la posicion del precio en el rango.
El 16-11 a las 10:30: 65,75 x 1,76 = 116 contra 118,5 real. viva3 por minuto mide de menos (112,5 contra 118,5 de velas).

**Why:** el operador busca "objetivos honestos"; leer el straddle como techo del rango lo invita a dar por terminado un dia que va por la mitad.
**How to apply:** el objetivo honesto de RANGO del dia es ~1,9x el straddle de apertura (banda 1,4x-2,5x); el straddle solo dice cuanto
suele terminar lejos de la apertura (cierre dentro de apertura ± straddle 67-71 % de los dias, en r2_resultados_crudos.json maxpain_em).
Ver [[busqueda-gatillo-2026-09-17]], [[como-ensenarle-trading]].
