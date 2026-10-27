# Control multifuente de la PythiaGex 5.0 "Gamma ES" — martes 27-10-2026

Qué se controló: que los DATOS DE ENTRADA y los NIVELES que dibuja la 5.0 en "MES 1m Chart" coincidan con fuentes externas
independientes, a la misma hora del dato. No es la auditoría interna (esa la hizo `herramientas/auditar_familia.py --perfil ES`,
código 0 a las 15:56-15:59Z): acá se mira si lo que entra y lo que sale cuadra con CME, Yahoo, CBOE bajado aparte, Opensera,
InsiderFinance, el Tesoro y la Fed de Nueva York.

- Ventana del control: 16:21Z a 16:55Z (12:21-12:55 NY, 13:21-13:55 AR), mercado abierto.
- Solo lectura de `%APPDATA%\ATAS\PythiaGex5`. ATAS no se tocó. Bajadas comprimidas, con tope de velocidad y de a una.
- CME: UNA sola bajada (2 pedidos espaciados 3 s, desde cmegroup.com en el navegador integrado; no había cache del día).
- Opensera: su API ahora rechaza pedidos de fuera del navegador ("402 Payment Required ... Nice bot"). No se forzó nada: se leyó la
  respuesta que la propia página cargó en una pestaña de fondo (`window.gexData`), dos veces (16:29Z y 16:43Z), sin pedir nada extra.
- **No hice la captura de pantalla final** (consigna: no tocar ATAS). Lo que sí hice: casé, rótulo por rótulo, la última captura
  existente (`capturas/2026-10-27_1257_despues_5_0_final.jpg`, 15:57Z) con el niv de ese minuto (sección 4.0). La comprobación en
  pantalla de lo que se ve AHORA queda pendiente.

Convenciones: "→" = convertido a precio de ES. SPX → ES suma la base; SPY → ES multiplica por la razón. "Medido" = lo calculé en
esta sesión desde el dato crudo; "supuesto" = lo dice el texto.

---

## Veredicto corto

1. **Precio, base y razón: coinciden.** El precio del MES del indicador es igual al de Yahoo en el 89,6 % de los minutos y a un tick
   o menos en el 98,6 % (los 10 minutos restantes, en 1.1). La base ES − SPX del indicador queda a +0,13 pts (mediana) de la de Yahoo; la razón ES/SPY, a 0,05 pts de ES.
2. **Libro ES (Rithmic) contra CME oficial: 182 de 182 lados de strike iguales al contrato.** Faltan strikes en las alas: Rithmic no
   los pide (lista abajo). No tocan los muros; corren el zero por OI de ES unos +3,5 pts (prueba de sensibilidad).
3. **Libro SPX y SPY (CBOE): el OI es idéntico a InsiderFinance (80 de 80 en SPX, 40 de 40 en SPY) y los muros, majors y zeros salen
   iguales.** Contra Opensera coinciden los niveles por volumen (zero por volumen a 0,01 pts) y NO los de OI.
4. **Hallazgo externo: Opensera está usando el interés abierto de hace dos sesiones** (cierre del viernes 23-10), no el del lunes 26-10
   que publicó la OCC hoy: 120 de 120 lados del 0DTE iguales al OI que mostraba CBOE el lunes. Por eso su zero gamma (7739) y su
   régimen "negativo" no se reproducen con el OI vigente (7714-7715, régimen positivo).
5. **Errores reales en lo dibujado (sección 4):** las réplicas "2.0 SPX D1-D2" y "Clas SPX 0Γ / D1-3" (prendidas de fábrica) usan una
   base de forwards ~1,2 pts más baja que la del mercado, y la de la 2.0 salta 1,1-1,7 pts para arriba y para abajo sin que el mercado
   se mueva (19 saltos desde las 15:15Z).

---

## 1. Precios y conversión

### 1.1 El precio del futuro (MES)

- **Indicador:** campo `f` del niv. Medido: es el cierre de la última vela de 2 minutos del MES (en un minuto impar repite el del par).
- **Fuente A:** Yahoo `MESZ26.CME`, velas de 1 min (CME con ~10 min de atraso; la vela de cada minuto es la buena aunque llegue tarde).
- **Fuente B:** Yahoo `ESZ26.CME` y `ES=F`. `ES=F` es HOY el mismo contrato que `ESZ26.CME` (748 minutos comunes, diferencia máxima
  0,25 en una vela parcial): no hay trampa de contrato. MES contra ES: mediana 0,00, percentiles 5-95 entre −0,25 y +0,25.
- **Diferencia:** 730 minutos con dato de Yahoo (04:10Z a 16:19Z; Yahoo no da antes de las 04:09Z): 654 iguales al tick (89,6 %), 66
  a un tick, **10 minutos (5 velas de 2 min) a más de un tick**: 08:02-03Z (7704,50 contra 7704,00), 10:20-21Z (7709,50 contra 7710,00), 14:00-01Z (7723,25 contra
  7722,75), **14:36-37Z (7733,00 contra 7736,75)** y 14:48-49Z (7740,25 contra 7739,75).
- **Explicación:** todas caen dentro del máximo-mínimo de esa vela; parece el último tick de la vela (la de 14:36 es 4 min después del
  arranque de las 14:31:55Z). Con dos fuentes no puedo decir cuál tiene el tick bueno: es ruido de cierre, no un corrimiento.
- **Hora:** bajada de Yahoo de las 16:28Z; repetida con la de las 16:47Z hasta las 16:39Z: 750 minutos, 668 iguales, 72 a un tick y
  los mismos 10 minutos a más de un tick (ninguno nuevo).

### 1.2 El contado de SPX y de SPY

- **Indicador:** el `current_price` de cada foto de CBOE que guarda (insumo; el `s` que muestra la pestaña para SPX es fut − base, el
  SPX "implícito", no el índice publicado).
- **Fuente A:** Yahoo `^GSPC` y `SPY`, vela de 1 min del minuto del último trade de esa foto.
- **Fuente B:** CBOE bajado aparte por mí (16:28:30Z, dato 12:13:28 NY): 7726,97; InsiderFinance SPX dice 7726,97 (es el mismo dato
  de CBOE de las 16:13:28Z aunque su sello diga 16:17:46Z); Opensera 7732,46 a las 16:00:26Z y 7726,94 a las 16:30:29Z.
- **Diferencia:** SPX 104 de 104 fotos dentro del máximo-mínimo del minuto en Yahoo (mediana −0,09); SPY 103 de 103 (mediana −0,02).
  Los dos spots de Opensera caen dentro de la vela de Yahoo de su minuto (16:00: 7731,42-7736,06; 16:30: 7724,61-7727,41).
- **Explicación:** coinciden. Dato nuevo: **Opensera es tiempo real en su sello** (no 15 min atrasado como CBOE); lo que atrasa es que
  la versión gratis solo cambia de foto a las :00 y :30.

### 1.3 La base ES − SPX (la que corre todos los niveles de SPX al gráfico)

- **Indicador (5.0, "sincronizada", mediana de 24 muestras MES de 15 min antes contra el spot de CBOE):** 36,08 a las 16:21Z (dato SPX
  16:03:28Z); 35,81 a las 16:30Z; 35,79 a las 16:47Z. Archivo `base-rueda-ES.json`: 36,15 a las 16:20:26Z (19 muestras).
- **Fuente A — Yahoo, cierre ESZ26 menos cierre ^GSPC en el mismo minuto:** mediana de la rueda 36,03 (186 minutos, 13:31-16:36Z);
  por media hora: 35,15 / 36,21 / 36,45 / 36,06 / 35,96 / 35,94.
  **Indicador contra la mediana de 24 min de Yahoo alineada a la hora del dato: 168 minutos (14:00-16:47Z), mediana +0,13 pts,
  percentil 10 −0,11, percentil 90 +0,69, máximo 0,92.**
- **Fuente B — forwards de la cadena (CBOE crudo 16:28:30Z, dato 16:13:28Z):** forward diciembre (SPX AM 18-12, la que liquida con
  el mismo SOQ que ES, 12 strikes, dispersión 0,45) 7762,68 menos forward 0DTE (SPXW 27-10, 12 strikes, dispersión 0,18) 7726,92 =
  **35,76** (35,94 descontando con 3,75 % × 51,9 días). El forward 0DTE es el contado: 7726,92 contra 7726,97 publicado.
- **Fuente C — carry teórico** (SPX 7727, 51,9 días hasta el 18-12 09:30 NY): tasa = letras del Tesoro de EE.UU., "Daily Treasury Bill
  Rates" del **26-10-2026**, rendimiento equivalente: 6 semanas 4,01 %, 8 semanas 4,10 % (interpolada a 52 días ≈ 4,07 %); SOFR del
  26-10 (Fed de Nueva York) 3,93 %. Dividendo: los últimos 4 de SPY (1,993 + 1,797 + 1,904 + 1,889 = 7,583 USD, Yahoo) sobre 769,86 =
  0,985 % (≈ 1,08 % sumando el gasto de 0,0945 % de SPY: supuesto). **Carry = 29,5 a 34,3 pts; central 32,9** (4,07 % − 1,08 %).
- **Fuente D — Opensera `basisSpread`:** −36,37 (16:29Z) y −36,23 (16:31Z y 16:43Z) → +36,2 a +36,4 (en vivo; método no publicado).
- **Diferencia y explicación:** la base del indicador (35,8-36,1) coincide con Yahoo a 0,1-0,3 pts y con los forwards directos a
  0,2 pts. El mercado paga ~3 pts más que el carry simple (carry neto implícito 3,2-3,3 % anual contra 2,9-3,0 % de la fórmula), lo
  mismo que se había medido en setiembre. **Lo que NO coincide es la base "medida" por forwards del propio indicador (34,6-34,9), que
  usan las réplicas**: ver 4.1.

### 1.4 La razón ES/SPY

- **Indicador:** 10,0797 (16:21Z), 10,07968 (16:30Z), 10,0795 (16:51Z).
- **Fuente A:** Yahoo MESZ26 / SPY por minuto: mediana 10,0797 (187 min).
- **Diferencia:** contra la mediana de 24 min de Yahoo alineada a la hora del dato: mediana +0,00007 (= **+0,05 pts de ES** en el
  strike 770), máximo 0,00047 (0,36 pts). **Coincide.**

---

## 2. Libro ES (Rithmic) contra la fuente oficial de CME

Fuente oficial: CME `Volume/Options/Details`, fecha de operación **26-10-2026**, informe final (`atClose` = interés abierto al cierre
del 26-10, el vigente hoy; `change` = cuánto cambió ese día). Martes semana 4 (pid 10135, vence hoy) y miércoles semana 4 (pid 8230,
vence el 28-10), publicados por CME a las 15:21:21Z y 15:21:59Z, bajados a las 16:32:52Z. Rithmic: `viva3-ES-2026-10-27.jsonl`, 107
fotos de 14:32:55Z a 16:39:16Z.

### 2.1 Interés abierto por strike

- **Indicador (Rithmic):** foto de las 16:39:16Z, 46 strikes por vencimiento (7610 a 7910).
- **Fuente (CME):** los mismos strikes.
- **Diferencia: 182 de 182 lados (strike × call/put × vencimiento) iguales al contrato.** Ejemplos cerca del precio, 0DTE (Rithmic /
  CME): 7740 C 746/746 P 527/527 · 7750 C 1806/1806 P 813/813 · 7755 C 601/601 P 58/58 · 7760 C 3720/3720 P 103/103 · 7765 C 487/487
  P 62/62 · 7770 C 428/428 P 123/123 · 7780 C 2328/2328 P 282/282. Miércoles: 7750 C 522/522 P 594/594 · 7760 C 210/210 P 140/140 ·
  7765 C 760/760 P 96/96.
- El OI de Rithmic no cambió desde la primera foto del archivo (14:32:55Z; 0 cambios en 107 fotos): es el de CME, fijo, como debe ser.

### 2.2 Lo que falta (no se completa: se dice)

- **Strikes que la foto de Rithmic NO trae y CME sí** (en cada vencimiento, 15): 7615, 7625, 7635, 7645, 7655, 7665, 7675, 7685, 7845,
  7855, 7865, 7875, 7885, 7895, 7905 (en las alas Rithmic va de a 10; de a 5 solo entre 7690 y 7840). Los de más peso: 0DTE **7685 P
  1117**, 7625 P 810, 7675 P 648, **7875 C 1231**, 7845 C 791; miércoles **7875 C 1414**, **7885 C 1178**, 7675 P 580.
- Además, en el 0DTE faltan los calls de 7900 (CME 993) y 7910 (CME 523): Rithmic trae solo el put.
- **Cuánto pesa (prueba de sensibilidad, NO un nivel):** agregando esos strikes con el OI de CME y una IV interpolada entre vecinos
  (supuesto; volumen 0 porque no se conoce), el **zero por OI de ES se corre +3,8 pts a las 16:30Z (7720,58 → 7724,42) y +3,4 a las
  16:45Z (7720,75 → 7724,11)**. Muros y majors no cambian. El "ES 0Γ est OI" viene apagado de fábrica: no afecta lo que se ve hoy.

### 2.3 Los niveles de ES recalculados con el OI oficial de CME

Con el OI de CME en lugar del de Rithmic (misma IV y volumen de Rithmic), a las 16:30Z y a las 16:40Z sale exactamente lo del niv:
muro de calls por OI 7760, muro de puts por OI 7750, major + por OI 7760, muro C por volumen 7750, muro P por volumen 7760, major +
por volumen 7750, major − por volumen 7740; zero por volumen 7738,97 (16:30Z) y 7740,71 (16:40Z); zero por OI 7720,60 y 7721,24.

### 2.4 Lo que solo CME tiene: qué se abrió ayer

El `change` del 26-10 dice cuánto de cada strike es posición NUEVA: 0DTE **7760 C +3535 de 3720 (95 % abierto el lunes)**, 7780 C
+1955 de 2328, 7750 C +495 de 1806, 7600 P +2053 de 3357. O sea: el muro de calls de ES en 7760 se armó ayer, no es viejo.

---

## 3. Libro SPX y SPY (CBOE) contra tableros públicos

Misma cuenta para todas las fuentes (la de la 5.0: gamma × contratos × 100 × S² × 0,01; calls +, puts −; Black-Scholes r 3,75 %; S =
precio del futuro − base; radio 20 pts para SPX/SPY), así la diferencia queda en los DATOS. También con la gamma que publica cada
fuente: da los mismos strikes en todos los casos.

### 3.1 Hora A — dato de las 16:00Z (12:00 NY): indicador contra Opensera

- **Indicador:** minuto 16:16Z, cadena CBOE 16:15:13Z (dato 16:00:10Z, 15 min de atraso), futuro 7762,75, base 36,14.
  Muro C por volumen 7720 → 7756,14 · muro P por volumen 7720 → 7756,14 · major + por volumen 7730 → 7766,14 · muro C por OI 7720 →
  7756,14 · muro P por OI 7725 → 7761,14 · major + por OI 7720 → 7756,14 · zero estándar por volumen 7697,48 → 7733,62.
  Recalculado por mí desde la cadena guardada: idéntico.
- **Fuente A — Opensera, foto 16:00:26Z** (leída 16:29Z: **dato de 29 min**), su cadena con la cuenta 5.0: muro C por volumen **7730**
  → 7766,14 (casi empate: en CBOE 7720 y 7730 pesan parecido y a las 16:30Z el indicador ya marcaba 7730) · muro P por volumen 7720 ✓ ·
  major + por volumen 7730 ✓ · **zero por volumen 7697,49 → 7733,63 (diferencia 0,01 pts)** · muro C por OI **7725** (≠ 7720) · major +
  por OI **7730** (≠ 7720) · zero 0DTE por OI 7728,66 contra 7702,29 con el OI de CBOE (**+26,4 pts**).
- **Su titular, convertido con 36,14:** zero gamma 7739,15 → 7775,29 · call wall 7800 → 7836,14 · put wall 7725 → 7761,14 · "active"
  call wall 7730 → 7766,14 · "active" put wall 7700 → 7736,14 · max gamma 7650 → 7686,14 · régimen "negative". Recalculado desde su
  propia cadena (todos los vencimientos, OI) su zero da 7737,78: el titular sale de sus datos. Sus "active walls" coinciden con los
  muros 0DTE sin radio que saco con CBOE (call arriba 7730, put abajo por volumen 7700): parece que eso miden (supuesto). Ojo: su `totalNetGEX` (−743 mil M) sigue 100× inflado y
  sus montos por strike también.
- **Volumen:** a la misma hora, el de Opensera es 4-11 % más alto en calls y 0-5 % en puts (7730 C: 84.852 contra 80.784). No lo pude
  explicar con estos datos (supuesto: otra fuente de volumen o sesión nocturna incluida). No cambia los strikes de volumen.

### 3.2 Hora B — dato de las 16:13Z: indicador contra InsiderFinance y CBOE bajado aparte

- **Indicador:** minuto 16:30Z, cadena 16:29:14Z (dato 16:14:12Z), futuro 7762,75, base 35,81. Muro C por volumen 7730 → 7765,81 · muro
  P por volumen 7720 → 7755,81 · major + por volumen 7730 → 7765,81 · muro C por OI 7720 → 7755,81 · muro P por OI 7725 → 7760,81 ·
  major + por OI 7720 → 7755,81 · zero por volumen 7699,60 → 7735,41.
- **Fuente A — CBOE crudo bajado por mí (16:28:30Z, dato 16:13:28Z):** mismos muros y majors (con nuestra gamma y con la de CBOE); zero
  por volumen 7699,75 (+0,15: la versión es 44 s distinta). OI 0DTE en ±100 pts: 80 de 80 iguales a lo guardado.
- **Fuente B — InsiderFinance** (sello 16:17:46Z, spot = CBOE 16:13:28Z; leído 16:29Z: **dato de ~16 min**; `isStale` dice false, como
  siempre): **OI 80 de 80 iguales al de CBOE**; sin volumen (no lo publica), así que solo se comparan los de OI: muro C 7720 ✓, muro P
  7725 ✓, major + 7720 ✓, zero 0DTE por OI 7702,03 contra 7702,01 ✓. **Hoy no le faltan strikes** en ±100 pts (la trampa de agosto no
  apareció).
- **Integridad del insumo:** la cadena de SPY guardada por el indicador es la MISMA versión de CBOE que bajé yo (16:28:16Z): todo igual
  salvo los lados que CBOE manda con IV 0, que el indicador descarta a propósito (23 en SPY, p. ej. calls 747-752 USD; 65 en SPX, calls
  7530-7580): están 18-25 USD / 150-200 pts adentro del dinero, gamma ~0; solo restan en la "carga" de esos strikes lejanos.

### 3.3 Hora C — dato de las 16:30Z: segunda foto de Opensera

- **Indicador:** minuto 16:46Z, cadena 16:45:30Z (dato 16:30:29Z, el mismo segundo que la foto de Opensera), base 35,79.
- **Opensera 16:30:29Z** (leída 16:43Z, dato de 13 min): **por volumen, todo igual**: muro C 7720 → 7755,79, muro P 7720 → 7755,79,
  major + 7730 → 7765,79, major − 7700 → 7735,79. Por OI, distinto otra vez (muro C 7725, major + 7730) por la causa de 3.4. Su "active
  put wall" pasó de 7700 a 7720, igual que el muro P por volumen del indicador.

### 3.4 Por qué Opensera no cuadra en OI: usa el interés abierto de hace dos sesiones

- El OI del 0DTE de Opensera es **idéntico (120 de 120 lados) al que mostraba CBOE el lunes 26-10 para el vencimiento del 27**, o sea el
  cierre del viernes 23-10. Con el OI vigente (cierre del 26-10, el de CBOE, InsiderFinance y CME) coincide en 2 de 80. En otros
  vencimientos (28-10, 30-10 y 06-11, 7 strikes): 28 de 28 comparables iguales al del lunes (el 06-11 no estaba en la foto del lunes) y
  2 de 42 al de hoy. Seguía igual en su foto de las 16:30Z.
- **Prueba de causa:** con los MISMOS datos de hoy (IV de CBOE, dato 16:13Z) y solo cambiando el OI: zero 0DTE por OI 7702,29 con el OI
  de hoy y **7727,37 con el del lunes** (Opensera da 7728,66); zero de toda la cadena (hasta el 06-11) 7714,15 con el de hoy y **7730,28
  con el del lunes**. El OI viejo explica todo el corrimiento del 0DTE y ~16 de los ~25 pts del zero de toda la cadena; el resto sale
  de su modelo (una sola IV por strike para call y put) y la hora.
- Ejemplos (Opensera = CBOE del lunes / CBOE hoy): 7720 C 519 / 3587 · 7715 C 172 / 2126 · 7730 C 883 / 1961 · 7750 C 1441 / 2917 ·
  7650 P 3427 / 5959. Es lo que la memoria de agosto llamó "OI propio no reconciliable": es OI de un día antes.

### 3.5 Zero "de tablero" (toda la cadena, como el titular de Opensera)

- No lo dibuja la 5.0; sirve para leer el régimen grande. Con OI vigente y todos los vencimientos hasta el 10-11: **CBOE 7714,52 (dato
  16:13Z) e InsiderFinance 7715,15** → con base 35,81: **7750,33 y 7750,96 en ES**. Opensera: 7739,14 → 7774,95 (OI viejo).

### 3.6 SPY

- **Indicador (16:30Z, razón 10,07968, dato SPY 16:13:15Z):** muro C por volumen 770 → 7761,36 · muro P por volumen 769 → 7751,28 · major
  + por volumen 770 → 7761,36 · muros C y P por OI los dos en 770 → 7761,36 · major + por OI 770 · zero por volumen 766,89 → 7730,04.
- **CBOE crudo (misma versión):** idéntico, con nuestra gamma y con la de CBOE.
- **InsiderFinance SPY** (sello 16:06:01Z, leído 16:29Z: **dato de 23 min**): OI 40 de 40 iguales en ±10 USD; muros y major por OI
  iguales (770); zero 0DTE por OI 768,19 → 7743,15 contra 768,27 → 7743,95 con CBOE (0,8 pts: su IV).

---

## 4. Errores reales en lo que se dibuja

### 4.0 La última captura, rótulo por rótulo (15:57Z = 12:57 AR)

Casé cada rótulo visible de `capturas/2026-10-27_1257_despues_5_0_final.jpg` con el niv de las 15:57Z: "ES C 2,04B" = muro C OI de ES
7760 (2037,8 M) · "ES P −346M" = muro P OI 7750 (−346 M) · "SPX C/P 85,7B/−17,6B" = muros por volumen de SPX, los dos en 7720 → 7756,12
(base 36,12) · "2.0 SPX D1 71,5B / D2 63,5B" = 7730 → 7764,57 y 7720 → 7754,57 (**base 34,57**) · "Clasica SPX D1 49,4B / D2 71,5B / D3
63,5B" = centroides 7767,80 / 7763,84 / 7757,24 (**base 34,57**) · "3.0 SPX D1 71,8B / D2 62,8B" = 7765,16 / 7761,79 (base 36,12). Todo
lo que se ve sale de los datos; el problema es la base de las réplicas.

### 4.1 Las réplicas dibujan con una base ~1,2 pts más baja que la del mercado

- **Qué:** "2.0 SPX D1-D2", "Clas SPX 0Γ" y "Clas SPX D1-3" (tres de las seis prendidas de fábrica) convierten SPX → ES con la base
  "medida" por forwards del indicador (34,5-34,9 hoy), mientras la 5.0 usa la sincronizada (35,8-36,1).
- **Números:** en toda la rueda (51 fotos de CBOE desde 13:45Z) la base "medida" quedó **1,17 pts por debajo de Yahoo** (mediana; p10
  −1,49, p90 −0,92). En el niv, la base de la 5.0 menos la de las réplicas: mediana **+1,50 pts** (hasta +2,45). Ejemplo 15:57Z: el
  strike SPX 7730 está en 7764,57 (2.0, base 34,57) y en 7766,12 con la base de la 5.0 (36,12); Yahoo a esa hora ~36,0.
- **Causa (medida):** la "medida" no es forward trimestral − forward 0DTE (el método de `conversion-spx-a-es`), sino forward
  trimestral − el corte en cero de una RECTA ajustada a 11 vencimientos (`CboeCadena.Medir`). Hoy la curva de forwards tiene escalones
  (viernes 30-10 y 06-11 con +2,2 pts), la recta corta en 7727,98 cuando el forward 0DTE es 7726,92 (= el índice, 7726,97), y la base
  sale 34,70 en lugar de 35,76.
- **Efecto en pantalla:** las rayas de la clásica (zero y D1-D3) quedan ~1-1,5 pts (4-6 ticks) más abajo que donde las pone la base del
  mercado; por eso el mismo strike de SPX aparece en dos o tres alturas distintas (ver 5.4). Las réplicas son fieles a cómo dibujaban
  la 2.0 y la clásica; el corrimiento es de la base que heredan.

### 4.2 La raya "2.0 SPX D1-D2" salta sin que el mercado se mueva

- **Qué:** la base de la réplica de la 2.0 alterna entre la "medida" (~34,3-34,9) y "de la rueda" (~35,8-36,4). Medido minuto a
  minuto entre 16:18Z y 16:50Z: usa "medida" cuando la ÚLTIMA foto de CBOE trae base de forwards confiable, y "de la rueda" cuando esa
  foto sale sin base (16:20:29Z, 16:24:14Z, 16:30:30Z-16:37:30Z, 16:41:30Z, 16:45:30Z-16:48:31Z); con la foto siguiente vuelve. La
  clásica, en cambio, se queda con la última "medida" ("medida hace N min") y no salta. Antes de las 15:17Z la 2.0 estuvo en "CRUDA"
  (34,38, respaldo).
- **Números: 19 saltos de más de 0,5 pt con el mismo strike desde las 15:15Z**: 17 de 1,1 a 1,7 pts (4-7 ticks) y 2 de 0,6. Con el
  precio quieto:
  15:21Z (precio 7758 → 7758) D1 7756,37 → 7754,97 · 15:25Z (7757,75 → 7757,75) 7756,37 → 7754,69 · 16:23Z (7763 → 7763) 7766,10 →
  7764,50 · 16:31Z (7762,75 → 7762,75) 7764,63 → 7765,96. La D2 salta igual.
- **Es el mismo defecto que `dos-libros-distintos`:** alternar fuentes hace saltar el nivel sin que el mercado se mueva. La clásica
  no salta desde las 15:18Z (quedó fija en "medida"), pero está corrida (4.1).

### 4.3 Menores (no cambian lo dibujado)

- Huecos del libro ES de Rithmic en las alas (2.2): +3,4-3,8 pts en el zero por OI de ES, que viene apagado.
- 5 velas de 2 min con el precio a más de un tick de Yahoo (1.1).
- Los lados con IV 0 de CBOE se descartan (3.2): sin efecto en gamma.

No encontré errores de cuenta: todo lo que recalculé desde las fuentes externas con la fórmula de la 5.0 dio los mismos strikes.

---

## 5. Lectura del régimen (para vos, sin dirección)

Foto: **16:51Z (12:51 NY, 13:51 AR)**, futuro **7751,75**. Fuentes y edad: libro ES de Rithmic (dato de 0 min), SPX de CBOE (dato de
16 min, base 35,81), SPY de CBOE (dato de 17 min, razón 10,0795).

**5.1 La idea: un auto con amortiguadores.** Los que venden opciones (los "dealers") se cubren comprando y vendiendo futuros. Arriba
del "zero gamma" (la raya donde la cobertura cambia de signo) se cubren comprando cuando el precio baja y vendiendo cuando sube: son
amortiguadores, el precio tiende a rebotar entre los strikes pesados como una pelota entre dos resortes. Abajo del zero hacen lo
contrario: empujan para el mismo lado del movimiento, y los movimientos se estiran. No dice para dónde: dice si el camino es con
amortiguador o sin él.

**5.2 Dónde está el precio respecto de los zeros (hoy, solo el 0DTE, que es lo que dibuja la 5.0).** Los tres libros dicen lo mismo:
el precio está ARRIBA de su zero, o sea con amortiguador.
- ES (Rithmic): zero por volumen 7740,93 (11 pts abajo); por OI 7721,11 (31 abajo).
- SPX (CBOE): zero por volumen SPX 7702,25 → **7738,06** (14 abajo); por OI 7701,99 → 7737,80.
- SPY (CBOE): zero por volumen 767,16 → **7732,65** (19 abajo); por OI 768,26 → 7743,67 (8 abajo).
- La clásica dibuja su zero en 7737,09 (el mismo SPX 7702,25 con base 34,84): ~1 pt más abajo por 4.1.

**5.3 La zona gris.** Mirando TODA la cadena (no solo lo que vence hoy), el cambio de signo con el OI vigente está en SPX 7714,5-7715,2
→ **~7750,3-7751,0 en ES** (dato de 16:13Z): el precio está prácticamente encima. Entre ~7738 y ~7751 el 0DTE todavía amortigua pero el
libro grande ya no. Y a medida que avanza la tarde el 0DTE pierde peso (vence a las 16:00 NY): esa zona gris pesa cada vez más.
Opensera dice "negativo" con el zero en 7774,95 en ES: **hoy no le creas** (OI de dos sesiones atrás, 3.4).

**5.4 Las paredes alrededor (una por vez).**
- **7750-7751:** ES 7750 (muro de puts por OI, muro de calls por volumen) + SPY 769 → 7751,15 (muro de puts por volumen). La
  confluencia de la familia marca 7750,38.
- **7755,8:** SPX 7720 → 7755,81 (muro de calls por volumen y por OI de SPX). Ahí caen también la "Clásica D1" (7757,71) y la "2.0 D2"
  (7755,82): es el mismo strike 7720 con otra base o centroide, no tres niveles.
- **7760-7761: donde coinciden los tres libros.** ES 7760 (muro de calls por OI, 3720 contratos, 95 % abiertos ayer según CME) + SPX
  7725 → 7760,81 (muro de puts por OI) + SPY 770 → 7761,23 (muros por OI). Confluencia de la familia: 7760,82. Cuando ES y SPX marcan
  el mismo strike, pesa doble.
- **7765,8:** SPX 7730 → 7765,81 (major + por volumen). Las rayas "2.0 D1" (7765,82) y "Clásica D3" (7763,32) son ese mismo strike.
- **Abajo:** SPX 7700 → 7735,81 (muro de puts y major − por volumen) y el zero de SPX en 7738.

**5.5 En una frase.** A las 16:51Z el precio está adentro de un sándwich de paredes (7750 / 7756 / 7761) con amortiguador del 0DTE en
los tres libros, apoyado justo sobre el cambio de signo de la cadena completa (~7750-7751). Eso describe un mercado que, mientras siga
arriba de ~7738-7751, tiende a frenar en las paredes; si lo pierde, el amortiguador se apaga y los movimientos pueden estirarse. **Nada
de esto dice para qué lado, nada de esto está medido en ES** (las 15 sesiones de bitácora siguen pendientes), y la entrada la decide el
order flow en ATAS, no la raya.

---

## 6. Trazabilidad

Carpeta de trabajo: `C:\Users\wmx_7\AppData\Local\Temp\claude\C--Users-wmx-7-OneDrive-Escritorio-ATAS-nada\404dab2f-b5f6-48fa-ba08-3ab764cbbdd8\scratchpad\multifuente\`
- `scripts\`: `cargar.py`, `libro.py` (la cuenta de la 5.0 aplicada a cualquier fuente), `c1_cboe_integridad.py`, `c2_precios_base.py`,
  `c3_es_vs_cme.py`, `c4_spx.py`, `c5_spy_es.py`, `c8_regimen.py`, `c9_opensera_1630.py`, `c10_es_huecos.py`, `c11_oi_viejo.py`, `foto.sh`.
- Salidas: `c1_salida.txt` … `c11_salida.txt`, `c8_salida_ultima.txt`.
- Bajadas: `bajadas\f1630\` (Yahoo ×5, CBOE SPX y SPY, InsiderFinance SPX y SPY, con `_bajadas.log` de horas y bytes), `bajadas\f1650\`
  (Yahoo), `bajadas\tasas\` (Tesoro, SOFR, dividendos de SPY), `bajadas\cme_20261026_oi.json`, `bajadas\opensera_1600_0dte.json`,
  `bajadas\opensera_1630_0dte.json`.
