# Ronda 26 — ES contra MES sobre la misma pierna — PRE-REGISTRO

Escrito el 27-11-2026 entre las 14:00 y las 14:30 AR, **antes de correr una sola línea que mire clases, colores contra desenlaces o
rasgos contra desenlaces**. Lo único que se corrió antes:
- `r26_00_seleccion.py` (salida `r26_00_salida.txt`): la regla de la tanda 0, que lee solo apertura/máximo/mínimo/cierre de tablas diarias.
- `r26_00_chequeo.py` (salida `r26_00_chequeo_salida.txt`): chequeo MECÁNICO del código sobre las cintas de hoy (pivotes contra una
  referencia independiente y los números de la pierna de hoy que ya estaban medidos en el pedido). No mira desenlaces ni clases.
- Una prueba de humo A CIEGAS de `r26_02_p1.py` y `r26_03_p2p3.py` sobre las cintas de hoy en una carpeta temporal: solo se miró el código
  de salida (0 y 0) y las líneas "Warning"; las salidas se borraron sin leerlas. Los rangos RTH de las cintas de ES 16 a 25-11 quedaron
  en `datos\ronda26\rangos-cinta-ES.json` (es dato, no resultado).
Lo que cambie después va en "Enmiendas", con hora y motivo, antes de la corrida que lo use. Una sola corrida por tanda.

## 0. La pregunta

El 27-11 a las 13:46:31 AR el operador vio en La Ola 1.0, en el mismo instante, ES "MAREA ▲ sin agresores Δ −2.640" y MES "MAREA ▲ con
compradores Δ +6.553". La pierna arrancó en el mínimo 7.222,75 (15:05:21 UTC). Medido y reproducido en `r26_00_chequeo_salida.txt` (corte
16:48:00 UTC): delta ES de la pierna −2.970 = órdenes de 1-9 contratos +2.355, 10-19 −4.234, 20-49 −1.058, 50+ −33; MES +6.441 micro
(+644 eq. ES); desde las 15:47 UTC ES −4.537 y MES −122 eq. ES; el delta de la pierna de ES cruzó a negativo a las 16:10:49 UTC;
correlación minuto a minuto ES contra MES/10 = 0,61.
**¿La divergencia entre el flujo de ES y el de MES sobre la misma pierna trae información sobre lo que hace la pierna después? ¿Y quién
está de cada lado?**

Lo ya sabido que se usa (no se rehace): R17 — las piernas de X van con su delta 86-99 %; la absorción en contra no frena la pierna;
P(giro | ya volvió d) ≈ geometría del paseo al azar; leído a tiempo, el flujo no sabe más que el precio. R18 — las entradas con el color de
la marea empatan antes de costos.

## 1. Teoría que ancla las hipótesis

- El flujo agresor explica el precio en el mismo instante y casi nada después (Cont, Kukanov y Stoikov 2014; Cont, Cucuringu y Zhang 2023).
- Tamaño e información: las órdenes medianas fraccionadas mueven el precio (Barclay y Warner 1993; Chakravarty 2001). El signo del flujo
  tiene memoria larga porque los grandes fraccionan (Lillo y Farmer 2004).
- ES y MES son el mismo índice en dos libros: el precio lo mantiene igual el arbitraje entre libros (dato del chequeo: hoy mediana
  |ES − MES| = 0,00 y p95 = 0,25 en 8.270 segundos). Que el delta difiera dice QUIÉN toma y quién da liquidez en cada libro, no que haya
  dos precios.
- Hipótesis H-ARB (NO se prueba en esta ronda, se dice qué sería compatible): los que le venden MES al minorista agresor se cubren comprando
  ES con órdenes pasivas (en el bid); eso deja MES con delta + y ES con delta − mientras el precio sube. Lo único que se mide es descriptivo:
  la "prima" del MES (punta media MES − ES al cierre del minuto, con el signo de la pierna) por clase. No se afirma nada con eso.

## 2. Datos

- Cintas de la sonda en `%APPDATA%\ATAS\PythiaGex\flujo\`: una fila por orden agresora (`t` UTC, `primero`, `ultimo`, `vol`, `lado`, punta
  antes y después). Se procesan **en el orden del archivo y CON duplicados**, como La Ola 1.0 y `test_ola.referencia` (los duplicados
  exactos y las filas fuera de orden solo se cuentan y se informan).
- Pares de diseño: noviembre 12, 13, 16, 17, 18, 19, 20, 23, 24 y 25-11 (`cinta-ES-...2300-...2200.csv` y `cinta-MES-...`); septiembre 08, 09,
  11, 14, 15, 16 y 17-09 (`...2200-...2100.csv`). Ventana de cada jornada: 18:00 NY de la víspera a 17:00 NY (como `OlaNucleo.Rango`).
- **Alineación (mismo vencimiento):** último precio de cada segundo RTH en los dos; el par alinea si la mediana de |ES − MES| ≤ 0,50 y el
  percentil 95 ≤ 1,00 en los segundos en que operaron los dos. Si no alinea, el par queda AFUERA y se dice (sin reemplazo). En septiembre hubo
  roll U6 → Z6 (la ronda 19 ubica la última rueda U6 de las cintas de ES el 11-09; el MES puede haber seguido en U6).
- **Control en vivo (aparte, no es diseño):** `cinta-ESZ6-202611262300-202611271649.csv` y `cinta-MES-202611262300-202611271648.csv`, las dos
  cortadas a las **16:48:00 UTC** (11:48 NY; la del MES termina 16:47:59,741). Se mira AL FINAL.

## 3. Tandas

- **Tanda 0 (regla escrita antes; `r26_00_seleccion.py`):** entre los 10 pares de noviembre, eficiencia e = |cierre − apertura| / (máximo −
  mínimo) de la rueda (ES 12 y 13-11 de la tabla de la ronda 16; 16 a 25-11 de la tabla de MES de la ronda 17, mismo índice, solo para elegir).
  TENDENCIA = mayor e, RANGO = menor e, MIXTA = la más cercana a la mediana de e. Aplicada: **19-11 (TENDENCIA, e 0,888), 23-11 (RANGO,
  e 0,133), 13-11 (MIXTA, e 0,463; mediana 0,464)**. Si un par no alinea entra el siguiente del mismo criterio.
- **Tanda 1 (confirmación):** el resto, 7 de noviembre (12, 16, 17, 18, 20, 24, 25-11) y los de septiembre que alineen. Corrida después de
  las 15:15 AR, con el código CONGELADO (los mismos hashes de la sección 10).
- **Control:** las cintas de hoy (sección 2), al final.

## 4. Parámetro y zigzag

- **X = la del ES de cada día, la MISMA para ES y MES** (separa parámetro de mercado; hoy el MES tuvo 10,00 y el ES 10,25 por el borde de
  redondeo 126,5625). X = max(4, round(0,08 × mediana de los rangos RTH de las 10 ruedas previas / 0,25) × 0,25), round de Python (al par;
  con medianas múltiplo de 0,125 nunca cae en el medio), como `EscalaOla`. Rango RTH = máximo − mínimo de primero/último de las órdenes
  9:30-16:00 NY. Fuente de cada rueda previa: cinta de ES (16-25-11) > tabla de la ronda 16 (ES 08-09..13-11, verificada por `test_ola`
  contra las cintas) > velas de 5 min de MES del cache de ATAS (antes del 08-09; en los 4 días comunes con la ronda 16 dan rangos idénticos).
  Cuenta una rueda previa solo si su RTH cubre ≥ 6 h (el 07-09, feriado, cubre 3,5 h y no cuenta). Verificado: X de hoy = 10,25 (mediana
  126,625), igual que el panel.
- Ola = zigzag de X, marea = zigzag de 4X: `MaquinaI` (copia de `test_ola.Maquina` que guarda el número de orden de cada extremo), corrida
  junto a la `Maquina` original en cada cinta; los pivotes tienen que dar idénticos (si no, la salida lo dice y el día no se usa).
- **Foto** al cierre de cada minuto RTH (9:31:00 … 16:00:00 NY, 390 cierres): estado con las órdenes de t < cierre (como `OlaFotos`).
- **Color** (como `OlaMotor.ConAgresores`): CON si signo(delta acumulado ahora − delta acumulado en el extremo de origen) = dirección; si no
  (o si da 0), SIN.

## 5. P1 — la mecánica (descriptiva, `r26_02_p1.py`)

1. Minutos RTH con pierna en curso en los dos y misma dirección: % con color distinto (ES sin / MES con = SC; ES con / MES sin = CS), por
   rueda y por hora NY (9:30-10, 10-11, …, 15-16); además el % con el mismo origen (±120 s, ±2 ticks) y el % de minutos en que los zigzags
   van en direcciones distintas con la misma X.
2. Piernas CONFIRMADAS del ES que terminan en la rueda, emparejadas con una pierna del MES (sus dos pivotes emparejan, mismo tipo, ±120 s,
   ±2 ticks, con dos pivotes seguidos del MES): color final de cada una, clase (CC, SC, CS, SS) y % de discordancia por hora del final.
3. **Quién hace la diferencia:** delta por tamaño de la orden agresora con el signo de la pierna (+ = a favor). ES: 1, 2-4, 5-9, 10-19,
   20-49, 50+ contratos. MES en su propio tamaño (1, 2-4, 5-9, 10-19, 20-49, 50-99, 100-199, 200+) y en equivalente ES (/10). Media por
   pierna, cada 10 pts de recorrido y % de piernas con cada tramo a favor, por clase; y la diferencia SC − CC por tramo. El tramo que más se
   da vuelta (SC − CC más negativo) es "el que hace la diferencia". **Se repite** si es el mismo tramo, con el mismo signo, en las dos tandas.
4. Contexto: correlación minuto a minuto del delta ES contra MES/10 por rueda; prima del MES por clase (H-ARB, descriptivo).

## 6. P2 — la información (`r26_03_p2p3.py`)

- **Minutos:** cierres de minuto RTH con pierna en curso en ES y MES y la misma dirección.
- **Desenlace (en el zigzag del ES):** SIGUE = la pierna hace un extremo ≥ Xs/2 más allá del extremo actual antes de que el zigzag confirme
  el giro (retroceso de Xs desde el extremo); GIRA = se confirma antes. Xs = X (ola) o 4X (marea). Si la pierna sigue abierta a las 17:00 NY
  sin haber llegado a Xs/2, el minuto queda **censurado** (afuera; se informa cuántos).
- **Cajas** de retroceso ya hecho r/Xs al cierre: [0; 0,25), [0,25; 0,5), [0,5; 0,75), [0,75; …) (r = |extremo − último precio|).
- **Clases:** CC (ES con, MES con), SC (ES sin, MES con), CS (ES con, MES sin), SS (sin, sin).
- **Estadístico:** D(clase) = promedio, ponderado por los minutos de la clase en cada caja, de (tasa de SIGUE de la clase en la caja − tasa
  de TODOS los minutos elegibles de esa caja). Robustez: la misma cuenta con la base por caja Y rueda (tiene que dar el mismo signo).
- **Incertidumbre:** la muestra efectiva son las piernas. Bootstrap por bloques = la pierna del ES a la que pertenece el minuto (B = 2000) y
  por ruedas (B = 2000); t = D / max(EE piernas, EE ruedas) con ≥ 7 ruedas (en la tanda 0, con 3 ruedas, EE piernas). IC 95 % percentil del
  bootstrap por piernas. **Placebo:** 2000 barajadas de la clase dentro de la misma caja y rueda; p de dos colas. (Baraja minutos: es más
  angosto de lo que debería porque ignora que los minutos de una pierna van juntos; por eso la decisión se apoya en el t por bloques.)
- Referencia teórica (descriptiva): paseo al azar sin deriva, P(SIGUE | r) ≈ (1 − r/Xs) × e^(−1/2).
- Secundario (descriptivo, no cuenta para decidir): el mismo D con el desenlace en el zigzag del MES y cuánto MÁS siguió la pierna.

## 7. P3 — combinaciones (fijadas acá; ninguna más)

Rasgo binario al cierre del minuto, con datos hasta ese instante; s = dirección de la pierna; deltas desde el origen de cada pierna (el
del ES para lo del ES, el del MES para lo del MES):
- **R0** (referencia): color del ES (s × ΔES > 0).
- **V1** delta combinado a favor: s × (ΔES + ΔMES/10) > 0.
- **V2** tramo ES de 10-49 contratos a favor: s × ΔES[10-19 + 20-49] > 0.
- **V3** tramo ES de 1-9 contratos a favor: s × ΔES[1 + 2-4 + 5-9] > 0.
- **V4** color del MES: s × ΔMES > 0.
- **V5** brecha: el MES más a favor que el ES: s × (ΔMES/10 − ΔES) > 0.
- **V6** el patrón de hoy: s × ΔES[1-9] > 0 y s × ΔES[10-49] < 0.
Estadístico: S = diferencia de riesgo de Mantel-Haenszel (tasa de SIGUE con el rasgo − sin el rasgo, dentro de cada caja, pesos
n1·n0/(n1+n0)); robustez con caja × rueda. Mismo bootstrap y placebo (se baraja el rasgo dentro de caja y rueda). "Mejor que el color del
ES" = |S| − |S(R0)| con IC del bootstrap por piernas pareado (descriptivo; solo se interpreta si el rasgo PASA).

## 8. Decisión (Bonferroni) y etiquetas

- 22 pruebas: P2 (4 clases × 2 escalas) + P3 (7 rasgos × 2 escalas). Alfa por prueba 0,05/22 = 0,00227; **|t| ≥ 3,05** y p del placebo
  < 0,00227. Una prueba con menos de 20 piernas (en la clase, o de cada lado del rasgo) no se juzga ("SIN MUESTRA").
- Etiqueta (con la tanda 1, `--etiquetar`, usando la tanda 0):
  - **PASA**: tanda 1 con |t| ≥ 3,05, placebo p < 0,00227 y el mismo signo que en la tanda 0.
  - **PISTA (no aprueba)**: tanda 1 con |t| ≥ 2 y el mismo signo que en la tanda 0.
  - **SIN INFORMACIÓN en esta forma**: el IC 95 % incluye el 0 en las dos tandas y (|D| < 2 pp en las dos o signos opuestos).
  - **INCONCLUSO (no aprueba)**: el resto.
- Regla del baúl (textual del operador): se separan "no aprueba / no paga sola" de "no tiene información"; nunca "descartado" ni "muere".
  Lo que quede como PISTA o PASA, o como patrón mecánico que se repite (P1), va al final de `PythiaGex-privado\BAUL_DE_PISTAS.md` en
  "## RONDA 26 - ...".
- Lo que NO se afirma: nada de esto es una regla de entrada; un PASA diría que el estado del flujo cambia la chance de que la pierna siga
  más allá de la geometría, no que pague costos (eso sería otra ronda, pre-registrada).

## 9. Orden de las corridas

1. Tanda 0 (ya, mercado abierto, prioridad baja, de a una cinta): `r26_01_datos.py tanda0 2026-11-13 2026-11-19 2026-11-23`,
   `r26_02_p1.py tanda0`, `r26_03_p2p3.py tanda0`.
2. Tanda 1 (después de las 15:15 AR): `r26_01_datos.py tanda1 <7 de noviembre> <7 de septiembre>`, `r26_02_p1.py tanda1`,
   `r26_03_p2p3.py tanda1 --etiquetar`.
3. Control: `r26_01_datos.py hoy 2026-11-27`, `r26_02_p1.py hoy`, `r26_03_p2p3.py hoy`.
4. Comprobación fehaciente desde el crudo: recalcular a mano, desde la cinta y con código aparte, varios minutos (color, caja, desenlace) y
   una pierna por tamaño, y compararlos con lo que dieron los scripts.

## 10. Hashes SHA-256 de los scripts (congelados a la hora del cierre)

Cierre 27-11-2026 ~14:25 AR. Cualquier cambio a un script después de esta hora es una enmienda (con su nuevo hash).
- `r26_comun.py`: `64f85c7b1f7785a5d7de22f3d58a6e437375dfdab3f68ed24e49c7082d922002`
- `r26_00_seleccion.py`: `0eaad2b175a93e8f61cde82ae59ca25e991b594a033ab8642a79ba6caa368598`
- `r26_00_chequeo.py`: `0648422c177cb262ba150dcdc276699ef55baee872b5794b66af70880f91911b`
- `r26_01_datos.py`: `f164a18e02771eb7157174ecc868ba0e070a67c8d0c202dc493f8ccf40d55161`
- `r26_02_p1.py`: `1c0c040e3e157271d0d5069e27e3024c24bcf3053de46ae64823ec6dfc63373c`
- `r26_03_p2p3.py`: `a562f1ca79b67403126e29100d855a222138c7fc332730536c75dee6289d5682`
- referencia externa `herramientas\test_ola.py` (no se toca): `d60dd81d9c10d97dd14194f60ecca771162de77c332ebc2b6a92075530fc138e`
El archivo de cierre `PREREGISTRO.cierre.txt` guarda el SHA-256 de este PREREGISTRO.md tal como quedó al cerrar.

## Enmiendas

**Enmienda 1 (27-11-2026 14:25 AR, después de la tanda 0 y ANTES de la tanda 1; no cambia ningún análisis).** Se agrega el script de
comprobación desde el crudo del paso 4 de la sección 9, `r26_04_comprobacion.py` (SHA-256
`052bfa70b7a31a3446b196023a14c82e2bf2b62f9931b793d344e91f2986e562`): parseo como `test_ola.referencia`, la `Maquina` de `test_ola` y
lazos simples; compara campo por campo con la tabla de minutos y con una pierna de la marea. Corrido sobre 19-11 y 23-11 de la tanda 0:
1.156 comparaciones, 0 diferencias (`r26_04_salida_tanda0.txt`). Los scripts de análisis siguen con los hashes de la sección 10 (verificado
a las 14:25). Lo visto en la tanda 0 (salidas `r26_0x_salida_tanda0.txt`) no cambia nada de lo pre-registrado para la tanda 1.

**Enmienda 2 (27-11-2026 14:27 AR, después de la tanda 0 y ANTES de la tanda 1; DESCRIPTIVO, no entra en Bonferroni ni en ninguna
etiqueta).** La P1 de la tanda 0 mostró que la discordancia es casi toda "ES sin / MES con" y que la hacen las órdenes de ES de 10-49
contratos. Para decir quién está del OTRO lado (el pasivo), se agrega `r26_05_punta.py` (SHA-256
`eab118f3fef1e6300f4e7726bf20ba874f042d2a3a89c1654abd1bd406f91722`), con la punta antes/después de cada orden que trae la cinta, en cada
pierna emparejada y por clase: AGUANTA = volumen agresor EN CONTRA de la pierna cuyo nivel tocado siguió en pie (venta con dbid ≥ abid;
compra con dask ≤ aask) / volumen en contra; MUEVE = volumen agresor A FAVOR que corrió el precio (compra con dask > aask; venta con
dbid < abid) / volumen a favor; en MES además 1 − MUEVE (lo que los pasivos del MES le dieron al agresor a favor). Lectura fijada antes:
si en las piernas SC el ES "aguanta" más que en las CC, hubo compradores (o vendedores) PASIVOS en ES absorbiendo a los medianos; si el
MES "mueve" menos en SC, hubo pasivos del MES dándole al minorista. Eso es lo COMPATIBLE con H-ARB, no su prueba (cualquier pasivo da lo
mismo). Se corre en la tanda 0 y en la tanda 1.

**Enmienda 3 (27-11-2026 14:29 AR, después de ver la salida de la Enmienda 2 en la tanda 0 y ANTES de la tanda 1; DESCRIPTIVO).** La
medida de la Enmienda 2 no exige que la orden se coma todo lo visible: con puntas de 15-25 contratos, el 84-86 % del volumen agresor de
CUALQUIER lado corre el precio en el instante, así que mide sobre todo eso (se deja como corrió, `r26_05_salida_tanda0.txt`). Se agrega
`r26_06_quien_mueve.py` (SHA-256 `b0d25f1ec38533d1362110173291a89f44850861e5b92ddc5a4d17568b5c13ed`): el movimiento de la punta media de
cada pierna emparejada se parte en AGRESOR (suma de media después − media antes de cada orden) y PASIVO (suma de media antes de una orden −
media después de la anterior: altas y bajas de límites entre órdenes), con el signo de la pierna; más la ABSORCIÓN REAL con la definición
del proyecto (`gatillo\base.py`: la orden se comió todo lo visible y el nivel no cedió), de la agresión en contra y a favor. Lectura fijada
antes de la tanda 1: si en las piernas SC del ES la parte PASIVA es la que lleva el precio (y en las CC la agresora), la pierna "sin
agresores" la mueve el libro que se retira o se recarga, no los que agreden. En la tanda 0 esta medida es exploratoria (se diseñó después
de ver la tanda 0); en la tanda 1 es la pre-registrada.

**Enmienda 4 (27-11-2026 14:30 AR, después de la Enmienda 3 en la tanda 0 y ANTES de la tanda 1; DESCRIPTIVO).** Lo visto: la parte
"agresora" de la Enmienda 3 sale a favor también en las piernas SC (+8,6 pts por pierna en la ola, igual que en las CC), y en una cinta
(ES 13-11, RTH) cada orden corre la punta media según su tamaño de forma muy cóncava: 1 contrato 0,39 ticks, 2-9 0,71, 10-49 0,77, 50+
1,05-1,13 (MES 0,32 / 0,54 / 0,68 / 1,1); la punta de después está cruzada o trabada el 1,7 % (ES) y el 3,8 % (MES) de las veces, lo que
descuadra el control de la Enmienda 3 (−0,25 pts en la ola, −2,4 en la marea). Además la partición agresor/pasivo está dominada por la
apertura del spread después de cada orden (antes 1 tick el 98,8 %, después el 78,7 %) y su cierre entre órdenes: no es una medida limpia
de "quién movió". Hipótesis mecánica fijada ANTES de la tanda 1: **en las piernas "ES sin agresores" hay más CONTRATOS en contra pero más
ÓRDENES a favor, y el precio lo corre el lado con más órdenes que comen la punta.** Se agrega `r26_07_conteo.py` (SHA-256
`60b5b961d6638ac428e2bb8f6ca5b51bc1859108737eaa1c2e97642d26e4c896`): órdenes y contratos a favor y en contra por pierna, cuántas órdenes
corrieron la punta en su dirección e impacto medio por orden, por clase. Se repite (va al baúl como EXPLICATIVA) si en las piernas SC del ES
el cociente de órdenes a favor/en contra es > 1 mientras el de contratos es < 1, en las dos tandas.

**Enmienda 5 (27-11-2026 14:38 AR, después de mirar dos descriptivos nuevos en la tanda 0 y ANTES de la tanda 1; DESCRIPTIVO).** Agregados
después del pre-registro y rotulados así: `r26_09_tramos_minuto.py` (SHA-256 `47ba5a37d3f15def5b5b5cfd2d05be26d9650ed1f299243c0cc61a35a4851484`;
la versión que corrió la tanda 0 era `eceeee72...d015`, sin el conteo por rueda): correlación de la variación del precio del minuto con el
delta del minuto por tramo de tamaño; `r26_10_quien_lidera.py` (SHA-256 `c9b2c8961c907bd562e6fa3086c4e1bc782462f32b302a7ce3df1847fc659d12`;
en la tanda 0 corrió `b8a2e46a...f8cd`, sin el conteo por rueda): por segundo, precio del ES contra precio y delta del MES (y al revés)
con desfases de −2 a +2 s; y para el control de hoy `r26_08_hoy_parametro.py` (SHA-256
`8df51e6efe5803d87299212cf00cb96e5025f4a024b1eaaa015f96afe5520bdc`): el MES con su propia X (10,00) contra la del ES (10,25).
Lo visto en la tanda 0: el delta del MES acompaña al precio del minuto con correlación +0,83 (ES +0,55; ES de 1-9 contratos +0,16); por
segundo los precios van juntos (0,94) y el delta del MES sigue llegando a favor del movimiento del ES 1 y 2 s DESPUÉS (+0,20 y +0,15)
cuatro veces más que el del ES (+0,05 y +0,04). Predicciones fijadas para la tanda 1 (se cumplen o no; no entran en Bonferroni):
(a) correlación minuto a minuto precio-delta del MES > la del ES en al menos el 80 % de las ruedas; (b) correlación del precio del ES en t
con el delta del MES en t+1 s > la del precio del ES en t con el delta del ES en t+1 s en al menos el 80 % de las ruedas. Si las dos se
cumplen, va al baúl como EXPLICATIVA: "el MES con agresores es en buena parte eco del movimiento (la agresión del MES llega detrás del
precio)".
