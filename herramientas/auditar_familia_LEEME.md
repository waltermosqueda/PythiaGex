# auditar_familia.py — el auditor independiente de la Familia (4.1 NQ / 5.0 ES)

**La idea en una línea:** es un contador que rehace las cuentas con las facturas originales, sin mirar el balance que armó el otro.
El indicador anota cada minuto lo que dibujó (`familia\niv-<sesión>-<contrato>.jsonl`); el auditor toma los datos CRUDOS de ese mismo
minuto (libro de opciones de Rithmic, cadenas de CBOE, cinta del futuro), rehace la cuenta con fórmulas escritas aparte y compara raya por
raya. Si una raya no coincide, dice cuál, por cuánto y **por qué**.

## Cómo se corre (desde la carpeta `PythiaGex`)

```
python -I herramientas/auditar_familia.py --perfil ES --ultimo
python -I herramientas/auditar_familia.py --perfil ES --minuto 2026-10-27T14:45Z
python -I herramientas/auditar_familia.py --perfil ES --desde 2026-10-27T14:34Z --hasta 2026-10-27T14:51Z --cada 1
python -I herramientas/auditar_familia.py --perfil NQ --desde 2026-10-26T00:00Z --hasta 2026-10-26T20:00Z --cada 30
python -I herramientas/auditar_familia.py --autoprueba-es
python -I herramientas/auditar_familia.py --help
```

- `--perfil ES` lee `%APPDATA%\ATAS\PythiaGex5` (libros ES / SPX / SPY); `--perfil NQ` lee `PythiaGex4` (NQ / NDX / QQQ: sirve para
  validar el auditor contra la 4.1 en producción).
- Con un solo minuto (`--minuto`, `--ultimo`) muestra **todas** las rayas, indicador contra recalculado; con un rango, solo las
  diferencias (o todo con `--detalle`). `--salida archivo.txt` agrega el reporte a un archivo (nunca dentro de `%APPDATA%\ATAS`).
- Los minutos del rango que el niv **no** tiene también se miran: si el recálculo tiene libros y el indicador no anotó nada, es una
  diferencia ("no dibujó y debía").
- **Solo lee** `%APPDATA%\ATAS`, con prioridad BELOW_NORMAL y pausas cortas (ATAS en vivo). Una corrida de 20 minutos tarda 20-30 s.
- La cinta del futuro se guarda a disco cada pocos minutos: los minutos posteriores a lo guardado se dejan afuera con un AVISO
  ("reintentar en unos minutos"). Con `--ultimo` se audita el último minuto cubierto.

**Códigos de salida:** 0 = todo coincide dentro de tolerancia; 1 = hay diferencias y TODAS se explican por un insumo del minuto (con los
insumos del indicador la cuenta da igual); 2 = hay diferencias con los mismos insumos (fórmula) o sin explicar; 3 = no se pudo auditar.

## Qué compara y con qué tolerancia

| qué | regla |
|---|---|
| strike elegido (muro C/P, major +/−, top-N, cruces ZTP, zero estándar, confluencia, familia) | exacto (el niv lo guarda con 2 decimales) |
| precio convertido al futuro | ≤ 0,01 pt |
| base del índice (SPX→ES, NDX→NQ) | ≤ 0,01 pt |
| razón del ETF (ES/SPY, NQ/QQQ) | efecto ≤ 0,01 pt en el precio del futuro |
| monto (M USD por 1 %, con el multiplicador de su libro) | ≤ 0,5 % o 0,05 M (media unidad del último decimal guardado) |
| insumos: precio del futuro del minuto, foto usada de cada libro (hora del dato), OI con fecha, cadena congelada, libros presentes | iguales |

Series auditadas (las de `CatalogoFamilia.Calc`): `MUROS_{F,I,E}_{vol,oi}`, `MAJORS_{F,I,E}_{vol,oi}`, `ZEST_{F}_{vol,oi}`,
`ZEST_{I,E}_vol`, `ZTP_{F,I}_vol`, `TOPN_{F}_{vol,oi}`, `CONF_vol`, `FAM_MUROS_{vol,oi}` (F = ES/NQ, I = SPX/NDX, E = SPY/QQQ).
**No audita:** `CARGA_*` (no tiene especificación: su regla está solo en el C#), las réplicas `R20_*`, `DOMS_*`, `R10_*` (son copias de
la 2.0 y la clásica, con su propio auditor), las capas `TRES_*` del núcleo 3.0, ni el dibujo en pantalla (eso es la captura).

Cómo leer el motivo de cada fila: `igual`; `igual, dentro de tolerancia` (la diferencia existe pero no mueve nada); `igual, insumo NO
verificable` (la cuenta coincide, pero algún dato de entrada es el que guardó el indicador y no se puede contrastar contra la cinta);
`insumo` (con los insumos del indicador coincide: el dato de entrada difiere y se dice cuál); `cache C5`; `formula` (mismos insumos,
distinto resultado: es un error de cuenta); `sin explicar`. Al final están los AVISOS DE DATO y la lista de decisiones del C#.

## De dónde sale cada fórmula (independencia)

Escrita desde la **especificación**, no desde el C#:

- `laboratorio/tres/auditoria_0810/backtest_familia.py`: C1 multiplicador por libro, C2 OI del futuro con fecha, C3 cruces sin islas,
  C5 zero estándar en grilla, C7 base del índice sincronizada, C8 razón del ETF sincronizada, C9 cadena congelada, C10 vencido excluido
  y horizonte Hoy; reglas MUROS / MAJORS / ZTP / ZEST / CONF / FAM y el precio del futuro en un instante.
- `laboratorio/tres/auditoria_0810/preview_niveles.py`: armado de la foto de Rithmic y de CBOE, montos, strike mostrado, y el OI con fecha
  **en vivo** ("esperando el salto" hasta las 03:30 UTC si la noche no tuvo salto).
- `laboratorio/tres/extremos_rebote.py` (`f_muros`, `f_majors`, R = min(2 %, radio)), `laboratorio/tres/nucleo.py` (gamma Black-76 y
  Black-Scholes con tasa 3,75 %, envejecimiento), `laboratorio/tres/libros.py` (corrimiento de contrato 30 pts NQ / 8 ES).
- ES: las constantes **decididas** en `atas/PythiaGexCinco/_diseno/DISENO_5_0.md` §4.

Del C# solo se leyeron formatos de archivo (Viva3.cs, `_modulos/cboe/CboeArchivo.cs`, `_modulos/familia/fam/AlmacenNiveles.cs`, la cinta
`seg-*.bin` de AdaptadoresFamilia.cs), la lista de series (CatalogoFamilia.cs) y lo que la especificación no dice (abajo).

### Decisiones del C# (copiadas porque la especificación no las dice)

- Qué sabía el indicador en cada minuto: la carpeta extra `PythiaGex\cboe-local` se lee **solo al arrancar**; los arranques salen del
  log `pythiagex<4|5>-cboe.log` ("historia <libro>:"). El minuto T en vivo se calcula a T + 6 s y espera hasta T + 60 s el tick que cierra
  la vela m2. Cinta compacta: tick válido si su tramo se escuchó; si no, el indicador usó la vela del gráfico (no se guarda).
- Zero estándar recalculado cada minuto (sin la caché de la vista previa; si no coincide se prueba la caché y se dice).
- Sesión y horas fijas en hora de Nueva York (modo corregido); vencimiento del futuro tercer viernes 09:30 NY; posferiado con el
  calendario de NYSE.
- TOPN: N = 6, radio del libro, desempate por el strike menor.
- ES: radio de la familia (FAM_MUROS y CONF) = 20 pts (`SeleccionFam.RADIO_FAMILIA`; el DISEÑO fija el radio por libro, no el de la familia).
- 5.0: la cinta lleva el nombre del instrumento del gráfico (`seg-MES-<día>.bin`) y el niv el contrato sacado por fecha (`MESZ6`).

### Interpretación del auditor de las reglas de ES (el DISEÑO dice la regla, no dónde se aplica)

- Cota de la base C7: 0 < base ≤ 150 y |base − fut × 3,4 % × días al vencimiento del futuro / 365| ≤ max(25 % de eso, 3 pts).
- C8 de SPY: muestras solo con el spot (hora del dato = cadena − 900 s) entre 04:00 y 20:00 NY; razón entre 9,95 y 10,25; la ventana de
  24 se reinicia a las 04:00 NY del tercer viernes de marzo, junio, septiembre y diciembre (ex-dividendo).
- Las tres se aplican **a cada muestra** (la que no cumple no entra en ninguna mediana) y a la base/razón del minuto.
- Vencido excluido: además de los "días" envejecidos de la especificación, la serie tiene que vencer después del minuto según su fecha
  (16:00 NY). Si el indicador cuenta la serie ya vencida, el diagnóstico lo dice.
- Si la 5.0 aplica alguna de estas reglas distinto, aparece como diferencia de CONV o de libro, con el motivo.

## Constantes por perfil

| | NQ (4.1, especificación) | ES (5.0, DISEÑO §4) |
|---|---|---|
| radio por libro (R = min(2 % del futuro, radio)) | 100 / 100 / 100 | ES 25, SPX 20, SPY 20 |
| radio de la familia (FAM, CONF) | 100 | 20 (decisión del C#) |
| zero estándar (paso / semiancho / centro, en el eje del libro) | NQ y NDX 2,5 / ±300 / 50; QQQ 0,05 / ±7,5 / 1 | ES y SPX 0,5 / ±75 / 12,5; SPY 0,05 / ±7,5 / 1 |
| ZEST_MAX | 300 | 75 |
| CONF_PTS / grilla FAM / ISLA / filtro | 3 / 5 / 0,10 / 3 % | 3 / 5 / 0,10 / 3 % |
| multiplicador | NQ 20, NDX 100, QQQ 100 | ES 50, SPX 100, SPY 100 |
| corrimiento de contrato | > 30 pts | > 8 pts |
| retraso C7/C8, vida viva / CBOE | 900 s, 300 s / 1500 s | igual |
| cota de base, rango de razón, horas de SPY, reinicio trimestral | no existen | sí (arriba) |

`--constantes archivo.json` pisa claves exactas (p. ej. `{"conf_pts": 0.75, "fam_grilla": 2.5}`) para probar otra regla; el reporte
lo avisa. No se lee `constantes_medidas.json` (eran propuestas; varias difieren de lo decidido).

## Validación contra la 4.1 en producción (NQ, sesión 26-10, corrida 27-10)

23 minutos pedidos de noche y de día, incluidos 19:58-20:01 UTC (vence el 0DTE): 20 anotados en el niv y 3 que el niv no tiene.
Resultado: **100 % en todas las series** (MUROS_NQ_oi 18/18, MUROS_NDX_vol 20/20, MUROS_NQ_vol 18/18, ZEST_NQ_vol 11/11, ZEST_NQ_oi 13/13,
ZEST_NDX_vol 20/20, ZEST_QQQ_vol 20/20, y el resto igual), insumos 100 %, y los 3 minutos ausentes también ausentes en el recálculo
(ninguna vela del futuro en las 4 h previas: ATAS no corría). Dos cosas para saber:

- La base de NDX de noche difiere **0,0048 pts** (bajo la tolerancia) en los arranques de las 04:45 y 14:04 UTC: la 4.1 usó una mediana de
  la rueda del 23-10 de 164,415 contra 164,41 que dan sus propias muestras guardadas (297 de 327 tras 3 MAD). Un precio de muestra rearmado
  al reiniciar distinto del guardado: no es la fórmula del carry y no mueve ningún strike.
- Parte de las conversiones usan precios de muestra que guardó la 4.1 (velas del gráfico en un reinicio, que la cinta no tiene): la cuenta
  coincide, pero ese insumo **no es verificable**; el reporte lo rotula.

Archivo de la corrida: `C:\Users\wmx_7\AppData\Local\Temp\claude\C--Users-wmx-7-OneDrive-Escritorio-ATAS-nada\404dab2f-b5f6-48fa-ba08-3ab764cbbdd8\scratchpad\auditor_validacion_nq.txt`.

## Primera corrida sobre la 5.0 en vivo (27-10, 14:32-14:51 UTC)

18-20 minutos cubiertos por la cinta: **100 % en todas las series** (los de 14:32 solo tienen SPX y SPY: el libro ES arrancó a las 14:33).
Lo que NO se pudo verificar todavía: la base SPX y la razón SPY usan 21-22 de 24 muestras con el precio del MES que guardó la 5.0 al
arrancar (velas del gráfico: su cinta empieza a las 14:32 y cada muestra mira el MES de 15 min antes). Las 7 de SPX y 6 de SPY que la cinta
sí cubre coinciden con lo guardado. Hacia las 15:30 UTC la ventana de 24 de día ya debería ser toda verificable. Sensibilidad: con CONF
0,75 y grilla FAM 2,5 (lo que dice la línea `[configurar]` de `pythiagex5-familia.log`) el auditor da CONF_vol 0 % y FAM_MUROS 26 %: la 5.0
calcula con 3 y 5 (lo decidido) y ese texto del log está desactualizado.

## Límites conocidos

- **Tercer viernes (SPX AM):** la cadena guardada suma la SPX mensual AM-settled (vence 09:30 NY) a la SPXW del mismo día y le pone las
  16:00 NY (`cadena_atas.construir`, una fila por strike y fecha). No se puede separar: el reporte lo marca como AVISO_AM desde las 16:00
  NY de la víspera hasta las 16:00 NY del viernes. Próximo: 20-11 (mensual) y 18-12 (trimestral).
- Los precios que el indicador tomó de las velas del gráfico (reinicios, huecos de la cinta) no se pueden verificar: se rotulan.
- El libro de ES de Rithmic no trae la fecha de cada vencimiento (solo los "días"): para ES el vencido se excluye solo por los días.
- `--paridad` reproduce la sesión en UTC fijo de la especificación (solo para contrastar con la vista previa del laboratorio).
- Desde el 01-11 (horario de invierno de EE.UU.) el modo corregido y la especificación difieren una hora en las horas fijas: el auditor
  sigue al indicador (hora de NY).
