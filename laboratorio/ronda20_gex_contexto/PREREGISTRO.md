# Ronda 20 — El GEX como contexto del régimen (va y vuelve contra tendencia) — PRE-REGISTRO

Escrito el **27-11-2026 a las 04:48 AR** (reloj del sistema), ANTES de cruzar el GEX con cualquier resultado. Lo que ya vi, y lo
declaro: el inventario de cadenas, la alineación de la base, el chequeo del tercer viernes y una prueba del armador de la tabla
en dos ruedas de diseño (13 y 14-10). En esa prueba vi los valores de GEX del 14-10: negativo casi toda la rueda, con el precio
cayendo. Es una rueda de las 49 y no miré ninguna otra cruzada con su resultado.

## 0. Qué cambió desde el pedido original
- El pedido original (H1-H4: largo de la ola por régimen, magnitud, flujo de calls y puts, muros como tope) quedó en segundo
  plano por pedido del coordinador (27-11 ~04:35 AR). La ronda 19 terminó: sus 4 finalistas eran apuestas de VUELTA AL VALOR que
  acertaban 58-75 % en diseño y cayeron a 46-51 % en confirmación y a 29-54 % en la guardada, porque cambió el RÉGIMEN (la
  ruptura del área de ruido de la apertura volvía en diseño, −3,1 pts al cierre, t −2,45; neutra en confirmación; siguió en la
  guardada, +32 pts).
- **Pregunta nueva: ¿el GEX distingue esos regímenes ANTES o MIENTRAS pasan?**
- Del pedido original queda el S1 (R1 de la ronda 18 por régimen, con % de acierto y esperanza neta con costo), que el
  coordinador pidió explícitamente. H2-H4 no se corren en esta ronda: la tabla por minuto lleva todos sus insumos (muros, OI,
  volumen de calls y puts) para que la ronda 21 los cruce.

## 1. Datos y huecos (medidos en `r20_00_inventario.py`, `r20_00_salida.txt`)
- **Cadenas de SPX de CBOE (≈ 902 s de atraso)**, tres archivos del mismo CDN: la copia de la rama `cadenas` (ronda 14, cada
  8-25 min), `cboe-local` (cada 75 s desde el 16-09) y la de la 5.0 (cada ~75 s desde el 09-11). Se deduplican por sello de CBOE
  y se queda la PRIMERA publicación.
- **Huecos que NO se completan:** 23-09 sin cadena; 24-09 sin cadena hasta las 13:27 NY; **20, 23, 24 y 25-11 sin cadena en
  ningún archivo** (los tres y la rama de GitHub terminan el 19-11). La guardada tiene GEX en **4 de sus 8 ruedas (16-19-11)**.
  Del 08-09 al 15-09, 17, 18 y 25-09 solo hay la nube: 27-33 fotos por rueda, huecos de hasta 25-40 min (la antigüedad del
  dato lo muestra minuto a minuto).
- **Tercer viernes:** el 18-09 y el 16-10 la cadena NO trae la serie AM vencida (OI del 0DTE 480 mil y 453 mil, como cualquier
  viernes; la víspera el mismo vencimiento traía 3,05 M y 1,15 M porque la AM estaba viva). No se excluyen.
- **La 5.0 separa la serie AM** (bandera `am`, plazo a las 9:30) y los otros dos archivos la juntan con la PM (plazo a las 16:00).
  Para que no se alternen dos estructuras: **todo a las 16:00 NY y ventana uniforme de 7,75 días** desde la publicación.
- **Ventana corta:** los archivos guardan solo vencimientos de hasta 8 días. Falta el OI mensual, trimestral y lejano, salvo en
  los 8 días previos a cada tercer viernes, cuando entra entero. Eso salta el GEX (la columna `mensual_en_ventana` lo marca y
  `gex_sin_mensual` lo saca). Es el GEX de corto plazo, no el total del mercado.
- **Base ES − SPX:** muestra = precio de la cinta de ES en la hora exacta del dato − spot de la foto. El desfase 0 s da la menor
  dispersión (MAD 0,29-0,48 pts contra 0,56-1,06 a ±15 s). Mediana de las últimas 24 muestras publicadas; reinicio el 14-09
  (la cinta pasa de U6 a Z6).
- **Precios y vara del régimen:** los rasgos por rueda de la ronda 19 (`scratchpad\r19\rasgos`: X, apertura, máximo, mínimo,
  cierre, cierre por segundo, σ del área de ruido por minuto) y sus cintas compactas, solo lectura.

## 2. La tabla por minuto (`scratchpad\r20\gex_minuto.parquet`, `r20_01_tabla_gex.py`)
Una fila por minuto de la rueda (9:30-15:59 NY) y por rueda de ES con rasgos de la R19 (57); sin cadena queda `hay_cadena`
falso. Campos: `ts_utc`, `edad_s` (minuto − hora del dato), `base`, `spot_es`, `flip_es` (barrido de spot, el cruce más
cercano), `flip_abajo_es`, `flip_arriba_es`, `n_cruces`, `dist_flip_X`, `gex_neto` (miles de millones de USD por 1 %),
`gex_0dte`, `gex_sin_mensual`, `gex_bruto`, `gex_norm` (= neto / bruto), `muro_call_es`, `muro_put_es`, `mayor_pos_es`,
`mayor_neg_es`, `oi_max_es`, `oi_call_max_es`, `oi_put_max_es`, `oi_call`, `oi_put`, `vol_call`, `vol_put` (solo si el dato es
de hoy), `vol_call_frac`, `vol_call_frac_30` (fracción de calls en el volumen nuevo de los últimos 30 min de dato),
`cobertura_es_por_X` (contratos de ES que los dealers operarían por 1X, positivo = contra el movimiento) y `regimen`.

## 3. Régimen (definiciones fijas)
- **Primaria:** `reg_total(t)` = signo de `gex_neto` en el spot del minuto t, con la cadena publicada antes de t.
- **Secundaria (declarada):** `reg_estructural(t)` = signo de `gex_neto − gex_0dte` (sin el 0DTE, cuyo signo depende más del
  supuesto de quién compró).
- **Supuesto de signo:** dealers comprados en calls y vendidos en puts, la convención de la literatura de GEX (Baltussen et al.
  2021). No hay flujo firmado de opciones: las posiciones reales de los creadores de mercado no se observan. Los trabajos con
  posiciones reales encuentran a los creadores de mercado del SPX y del 0DTE comprados en gamma la mayor parte del tiempo
  (Adams et al. 2026; Amaya et al. 2025, documentos de trabajo).
- **"Al abrir" = el minuto 9:59 NY** (el último antes del primer chequeo de la R19, a las 10:00; la cadena es del dato de
  ~9:44). Secundaria: 9:30 (la foto previa a la campana, con el OI nuevo, la IV y el spot de ayer).
- **Evolución (secundaria):** fracción de los minutos desde las 9:30 hasta t con `gex_neto` < 0; se corta en 0,5.

## 4. Muestras
Diseño = ES 08-09 a 15-10; confirmación = ES 16-10 a 13-11; guardada = ES 16 a 25-11 (con GEX solo 16-19-11). **Toda esta
data ya la vio la ronda 19 (y partes las 17 y 18): no hay muestra virgen.** Por eso se valida dejando UN PERÍODO AFUERA. Se
entrena en dos y se prueba en el tercero, y se informan los tres pliegues. El juez final es en vivo.

## 5. Hipótesis y vara (la misma de la R19)
Vara por rueda, con los rasgos de la R19: eficiencia e = |cierre − apertura| / rango (RTH), tendencia si e ≥ 0,6;
`rango_rel` = rango RTH / mediana de las 10 previas (la de la R19). **Ruptura al cierre:** chequeos cada 30 min de 10:00 a 15:30.
Si |precio − apertura| > σ del minuto, el lado es el de la ruptura y se mide fc = (cierre − precio) × lado, en pts y en X.

- **G1 (ANTES):** con `reg_total` negativo a las 9:59, la rueda tiene más `rango_rel`, más e y una ruptura al cierre más
  positiva (sigue) que con positivo. Unidad = rueda. Diferencia de medias por período y juntas; t de Welch con ruedas como
  unidad; p por permutación exacta de etiquetas entre ruedas del mismo período (5.000). Continua: ρ de Spearman entre
  `gex_norm` a las 9:59 y cada resultado (se espera negativa).
- **G2 (MIENTRAS):** en los chequeos fuera del ruido, fc según `reg_total` EN el minuto del chequeo: se espera fc > 0 con
  negativa (sigue) y fc < 0 con positiva (vuelve). Se juzga la diferencia (neg − pos). La t va agrupada por rueda (CR1), con la
  t por rueda de cada régimen. El placebo le pone a cada rueda el recorrido de régimen de OTRA rueda del mismo período, en el
  mismo minuto (2.000 sorteos), y da una p. Control descriptivo: tercios del tamaño de la ruptura (|mov| / σ). Así se separa el
  régimen del "ya se movió mucho".
- **G3 (finalistas de la R19):** por operación (`operaciones_finalistas.csv`), régimen en el minuto de la entrada. Acierto
  (`resultado_pts` > 0) y neto con costo 0,50 (`neto_c050`) por régimen, por finalista y las 4 juntas, por período. La hipótesis
  es que la vuelta al valor gana con positiva y pierde con negativa. Se mide la diferencia (pos − neg) con t agrupada por rueda,
  más el placebo de permutación entre ruedas.
- **Dejar un período afuera (G3 como filtro):** "operar solo con `reg_total` positivo" tiene umbral fijo (0) y no se entrena.
  Para `gex_norm` y `dist_flip_X` se elige el umbral en dos períodos (máximo neto por operación conservando ≥ 30 % de las
  operaciones) y se aplica al tercero. Se informan los tres pliegues.
- **S1 (R1 de la ronda 18 por régimen):** la regla R1 tal cual (`ronda18_senal_ola/r18_01_senal.py`, clase `Maquina`, X de la
  R19) en las ruedas de ES con cadena. Por régimen en la entrada: operaciones, % de acierto, bruto, neto con 0,50 y el largo de
  la ola operada en X, por período; t agrupada por rueda.

## 6. Cuándo "separa" y cuándo "pasa"
- Un régimen **separa** si la diferencia tiene el signo esperado en los TRES períodos con dato, y además, en conjunto, la t
  agrupada por rueda es ≥ 2 y la p de permutación ≤ 0,05.
- Un filtro **pasa** si la finalista (o R1) filtrada da neto > 0 con costo 0,50 en el período dejado afuera, en los tres
  pliegues. Con 4 ruedas de GEX en la guardada eso es una prueba débil y se dice.
- Variantes: G1 son 3 resultados × 2 definiciones × 2 horas = 12, y la primaria es `reg_total` a las 9:59. G2 son 2
  definiciones + la evolución. G3 son 5 (4 finalistas + juntas) × 2 definiciones + 2 continuas. S1 son 2 definiciones. Todo se
  informa, gane o pierda.
- Lo que supere al placebo en algún período, o anda en un régimen y no en otro, va al baúl con sus números (regla del operador).
- Se explica MECÁNICAMENTE lo que se vea: quién cubre qué y cuándo (`cobertura_es_por_X` contra el volumen por X). Nunca se
  cierra con "nada le gana al azar". Explicar no es predecir, y nada de esto promete rentabilidad.

## Enmienda 1 (27-11 04:53 AR, antes de correr G1-G3 y S1; la tabla todavía se estaba armando)
- **Dato válido:** el régimen de un minuto se usa si el dato tiene ≤ 60 min (`edad_s` ≤ 3.600). La excepción es la variante de
  las 9:30, que por construcción es la foto previa a la campana (OI nuevo, precio y IV de ayer, ~17 h de antigüedad). Así el
  23-09 (sin cadena en la rueda) y los minutos con huecos largos de la nube quedan afuera, en vez de entrar con datos de ayer.
- **Control descriptivo agregado a G2:** el lado de la ruptura (arriba / abajo). Una caída cae debajo del flip más seguido que
  una suba, y eso podría confundir "gamma negativa" con "es una caída". No cambia la hipótesis ni la vara.

## Enmienda 2 (27-11 04:56 AR, con la tabla terminada y ANTES de correr G1-G3 y S1)
- El 20-11 y el 23-11 la tabla tiene "cadena", pero es la foto del 19-11 a la noche (1 a 4 días de antigüedad, OI viejo). Quedan
  inválidas por la regla de los 60 min. **La variante de las 9:30 exige además `foto_hoy`:** la foto tiene que estar publicada el
  mismo día de la rueda después de las 6:30 NY, con el OI nuevo. Sin eso, la rueda no entra en esa variante.
- Columnas agregadas a la tabla para la ronda 21: `valido` (hay cadena y el dato tiene ≤ 60 min) y `foto_hoy`.

## Enmienda 3 (27-11 05:01 AR): controles POST HOC, declarados DESPUÉS de ver G1, G2 y G3 (no son pre-registro)
Lo que ya vi: con gamma negativa a las 9:59 el rango del día es mayor (juntas +0,50 veces la mediana, t 2,67) y la eficiencia
no cambia. En G2, una ruptura PARA ABAJO es casi siempre con gamma negativa (138 de 139) [corregido a las 05:12: lo había
escrito invertido; de las 198 rupturas con gamma negativa, 60 son para arriba]. En G3 el régimen no separa dentro de cada
período. En el pliegue de G2 se entrenó tres veces el mismo umbral de `gex_norm` (−0,41). Para no leer de más se agregan, como
exploración marcada:
- **C1:** G1 controlando el agrupamiento de la volatilidad: rango_rel ~ negativo a las 9:59 + rango_rel de AYER, por rueda.
- **C2:** "gamma muy negativa" (`gex_norm` < −0,41) en los chequeos de G2, rueda por rueda y por período, solo las rupturas para
  abajo, contra las rupturas para abajo con `gex_norm` ≥ −0,41.
- **C3:** finalistas por tramos de `gex_norm` (descriptivo).
Nada de esto puede "pasar": es material para el baúl y la ronda 21.

## Enmienda 4 (27-11 05:06 AR): la prueba directa del trabajo citado, exploratoria y POST HOC
Baltussen et al. (2021) no dicen "la primera parte del día": dicen que el retorno del RESTO del día (cierre de ayer → 15:30)
predice la última media hora (15:30 → 16:00), y solo cuando la gamma neta es negativa (verificado en el texto publicado).
Se mide así, con el régimen a las 15:30 y la tabla por minuto, en las ruedas con dato (no hay 14-09: el roll corre el cierre de
ayer). Pasa a llamarse **C4**. Con unas 50 ruedas no tiene potencia para nada fino; se informa igual.

## Enmienda 5 (27-11 05:15 AR): C5, cruce POST HOC con la llave de la ronda 21
La R21 terminó usando esta tabla, y su llave de régimen es rrn (rango hasta ahora / rango esperado a esa hora; TENDENCIA si
≥ 1,6). Se mide si la gamma a las 9:59 anticipa que la rueda llegue a ese estado después de las 10:00, y qué hace la ruptura al
cierre en esas ruedas. Solo lectura de `scratchpad\r21\estados_minuto.parquet`. Exploratorio.
