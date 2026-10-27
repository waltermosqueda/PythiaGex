# P2 — SPX, cboe y clásica en la 5.0 (27-10-2026)

Constructor P2 de PythiaGex 5.0 - Gamma ES. Paquete: `_modulos/cboe/*`, `_modulos/familia/spx/*`, `_modulos/familia/clasica/*`.
Nada fuera del paquete se tocó. Compila con `herramientas/build_serial.ps1` (DLL de las 12:01, sha256 `fde93418341c3fc0…`).
No se commiteó, no se instaló, no se tocó ATAS ni `%APPDATA%`.

Cómo leer los números: **MEDIDO** = lo corrió el arnés de esta sesión con datos reales del disco; **CÓDIGO** = leído en el código;
**SUPUESTO** = no medido.

---

## 1. Qué cambió, por archivo

### `cboe/CboeCrudo.cs`
- La raíz del símbolo OCC ya no se descarta: `Am[]` marca la raíz `"SPX"` (mensual/trimestral AM, liquida con el SOQ de la apertura).
  `SPXW` y `SPY` siguen siendo PM.
- `VencUtc(fecha, am)`: la AM vence a las **09:30 NY** de su fecha; la PM a las 16:00 NY como antes.
- Capacidad inicial de los arreglos estimada por el largo del JSON (420 B por opción; medido 445). Evita la cadena de duplicaciones
  1024 → 32768 en cada bajada.

### `cboe/CboeUtil.cs`
- `HoraNyCboe.AperturaUtc`: 09:30 NY en UTC por fecha (13:30 en verano, 14:30 en invierno).

### `cboe/CboeCadena.cs`
- **Construir (tercer viernes).** La serie es (fecha, raíz). La AM es un vencimiento propio con sus días hasta las 09:30 NY. Queda
  **afuera de la cadena** desde que sus días son ≤ max(1, PM más cercano + 0,01) + 0,05. Ese límite es el tope del horizonte Hoy de
  `FilasHoy` y de `GammaHoyNucleo`, más ~1,2 h de margen para el envejecimiento. En la práctica, desde la víspera a las ~08:20 NY.
  - Resultado: **la AM nunca entra al Hoy**, así que el 0DTE del libro SPX es la SPXW del día.
  - Antes de esa hora la AM entra con su T propio: la carga de todos los vencimientos la ve.
  - La línea flaca la marca así: `{"f":"2026-11-20","dias":..,"am":true}`.
  - Los días sin AM a ≤ 14 días (casi todos) la cadena sale **byte a byte igual** a la de la 4.1 y a la de Python (MEDIDO hoy).
- **Medir (base por forwards).**
  - Se excluyen las series vencidas a la **hora del dato** (`ultimo_trade` NY; si falta, `ts − 902 s`): SPXW/SPY a las 16:00 NY y
    SPX AM a las 09:30 NY.
  - Una sola raíz por fecha: el trimestral con la **AM** (la que liquida con el mismo SOQ que ES); el resto con la SPXW.
  - Sin la serie de hoy, el contado se evalúa sobre la recta en la hora del dato: x = dato − 16:00 NY del cercano, en días. De día x = 0,
    como Python.
  - `Medir(c, hoy, dato, corregida:false)` reproduce la 4.1/Python para el arnés.
- **base / base_confiable** se calculan; la 4.1 escribía siempre null/false.
  - El criterio es el de `pythiagex/base.py`: recta ≤ 8 ticks, dispersión del trimestral ≤ 12 ticks, ≥ 5 vencimientos, ≥ 8 strikes y
    0 ≤ q ≤ 4 %.
  - La tasa de q es `TasaConfianza` = 4,1 %. Python usa la curva del Tesoro, que es una bajada externa y está prohibida acá. El 4,1 % sale
    de las letras de 28-91 días del 23-10: 4,00 / 4,08 / 4,17 %.
  - `base` = `base_cruda` cuando es confiable, como hace `archivar_cadena`.
- `DeLinea` lee `"base"`, `"base_confiable"` y `"am"`, en las líneas propias y en las de cboe-local.
  - Nuevos campos: `LineaCboe.BaseMedida`, `BaseConfiable` y `DiasAm`.
  - Nuevos campos: `CadenaCboe.VencAm`, `AmFuera` y `LimiteAm`.
  - Nuevos campos de `BaseCboe`: `Confiable`, `CarryNeto`, `Q`, `XRef`, `DatoUtc`, `Vencidas` y `FutAm`.

### `cboe/BajadorCboe.cs`
- `MedidaCboe` / `IMedidaCboe`: una tabla aparte por foto, con la base medida, si es confiable y la AM. Va aparte porque `FotoCadena`
  (en `_contratos`, que no es de este paquete) no tiene esos campos.
  - La implementan `BajadorCboe` y `ManijaCboe`.
  - Se copia a las fotos livianas al podar.
- El log de cada bajada agrega:
  - `parseo N ms (json M)`;
  - la AM de la cadena (en la cadena y afuera);
  - las series vencidas excluidas, el cercano y x;
  - el trimestral (AM o PM) y si la base es CONFIABLE, con q, dispersión, vencimientos y strikes.
- Se mantienen: tope 1250 KB/s, trozos de 64 KB, un ticker por vez, 75 s / 300 s, y el hilo propio BelowNormal.

### `familia/spx/ConversionSpx.cs`
Cotas nuevas (la 4.1 no tenía ninguna):
- **Sanidad:** 0 < base ≤ 150.
- **Carry:** |base − fut × 0,034 × días/365| ≤ max(25 %, 3 pts). Los días se cuentan hasta el vencimiento del contrato del gráfico
  (ESZ6: 18-12-2026, 09:30 NY).
- Las dos cotas se aplican a **cada muestra**: si no pasa, no entra a la rueda ni a las 24 (`EstadoFotoSpx.FueraDeCota` y `Cota`).
  También se aplican a **la base del minuto**: fuera de cota, SPX no se dibuja.
- **Forwards** (`base_cruda` de la foto): sirven solo con error ≤ 8 ticks, del contrato del gráfico y dentro de las dos cotas. El contrato
  se saca con `ContratoVigente` de la fecha local de `generado`, igual que `base.medir`.
  - **Con base sincronizada:** es un control. Tope de 3 pts en la rueda y 4 de noche; si difieren, la pestaña lo dice y manda la
    sincronizada.
  - **Sin base sincronizada** (sin rueda, roll o fuera de cota): la base es la de forwards, con el texto "de FORWARDS".
- Retraso 900 s, PISO 0,5 y la lógica de noche quedan (ver §2).
- Opciones nuevas:
  - `Cotas = true`: en `false` apaga las cotas, para paridad con `backtest_familia`.
  - `HoyLocal`.
  - `Base(e, t, fut, out texto)`: el overload con cotas.

### `familia/spx/LibroMinuteroSpx.cs`
- Usa `Base(e, t, fut, …)` con las cotas.
- Lo demás lo dejó P1: zero `ZeroEstandarFam.SPX` (0,5 / ±75 / 12,5) y radio `RadioDe("SPX")` = 20 (lo aplica `SeriesLibroFam` por
  `b.Libro`). MULT 100.
- Comentarios de 5.0; ya no quedan constantes de NDX sin justificar (§2).

### `familia/spx/CalendarioSpx.cs`
- Solo la nota de 5.0: mismo calendario que NDX. El índice se congela a las 16:00 NY (MEDIDO por el agente de mediciones, 23 y 27-10).

### `familia/clasica/ClasicaSpx.cs` (réplica de la clásica en ES: R10_SPX_zero / R10_SPX_dom)
- **NucleoClasica:** copia 1:1 de `PythiaGexNiveles/GammaHoyNucleo.cs` (GammaBs :171, Gex :183, Envejecer :219, ×100, piso 1/1440), más
  carry, `ExpiracionDeCodigo` y `TrimestralDesde` de la clásica. Ya no depende de `Nucleo20` / `BaseSpx20` (replica20, paquete P3), ni
  de su dividendo de la 2.0 (0,008).
- **Defaults de ES** (CÓDIGO de la clásica, verificado hoy):
  - `VelaMin` 2 → **1**: la clásica en ES corre en M1.
  - `CongelarEnLaRueda` true → **false**: en ES su primaria ES SPX y mide en la rueda.
  - `Dividendo` 0,008 → **0,012**: `DividendoUsado`, `GammaHoy.cs:1411`.
- **Cascada completa** (`GammaHoyNucleo.cs:282-287`, verificado): medida > medida reciente (≤ 360 min) > de la rueda (≤ 24 h) > cruda >
  teórica > cruda sin cota.
  - "medida" y "medida reciente" aceptan el carry o el del trimestral alternativo (solo si el código no trae mes, `_expAlt`).
  - La medida sale de `IMedidaCboe`.
  - Texto: `= medida (forwards de la cadena de las HH:mm:ssZ, N ticks de error, confiable)` / `= medida hace N min (…)`.
- Se mantienen: radio min(2 %, 100) = 100, empate 20 %, centroide ±12, 3 dominantes, retraso 960 s y ×100. No se reescalan: es una réplica.

### `familia/clasica/MuestrasClasica.cs`
- Solo el comentario. Con `VelaMin` 1 las muestras son otra medición que las de 2 min: la clave incluye el marco y no se mezclan.

---

## 2. Constantes: viejo → nuevo y por qué

| Constante | 4.1 | 5.0 | Por qué |
|---|---|---|---|
| Vencimiento de la raíz `SPX` (AM) | 16:00 NY | **09:30 NY** | liquida con el SOQ de la apertura del tercer viernes |
| AM en la cadena | sumada a la SPXW | **aparte; afuera si días ≤ max(1, PM + 0,01) + 0,05** | el 0DTE es la SPXW; el margen cubre el envejecimiento (C9: 25 min) |
| Medir: series vencidas | incluidas | **excluidas a la hora del dato** | noche del 26-10: 51,9 → 8,6 ticks (MEDIDO, §3) |
| Medir: trimestral | AM y PM pisándose | **AM (si no da, PM)** | ES liquida con el SOQ de la AM; 5,98 M de OI contra 144 mil (18-12) |
| Medir: contado | recta en x = 0 | **recta en la hora del dato** (x = 0 de día) | sin esto la base queda baja ~0,7 pts por día de carry de noche |
| `base_confiable` | siempre false | **criterio de base.py con `TasaConfianza` 4,1 %** | q ≈ 0,6-0,9 % con el carry medido; lejos de los bordes |
| Cota de sanidad C7 | ninguna | **0 < b ≤ 150** | Z6 medido 35-72 |
| Cota de carry C7 | ninguna | **\|b − fut×0,034×d/365\| ≤ max(25 %, 3)** | r − q implícito medido 3,4 % (3,3-3,6); la fórmula 3,75 − 1,2 queda 25-43 % corta |
| Forwards contra la sincronizada | no se usaban | **≤ 3 rueda / ≤ 4 noche, error ≤ 8 tk, mismo contrato** | MEDICIONES §3.8 |
| `RETRASO_S` | 900 | 900 | mínimo de ruido medido (0,49 contra 0,86 con 960) |
| `PISO` (MAD de la robusta) | 0,5 | 0,5 | 3 MAD medido = 1,45: el piso no muerde |
| `MIN_DIA` / `MIN_RUEDA` / `VENTANA` / `VIVO_S` / `VIDA_CBOE_S` | 5 / 20 / 24 / 600 / 1500 | iguales | cantidades y tiempos: misma cadencia de CBOE en SPX |
| `MULT` SPX | 100 | 100 | producto |
| Zero estándar | 2,5 / ±300 / 50 | 0,5 / ±75 / 12,5 (P1) | distancias ÷ 4, paso a 2 ticks |
| Radio del libro SPX | 100 | 20 (P1, `RadioDe`) | cobertura medida SPX↔NDX |
| Tope de bajada / trozo / cadencia | 1250 KB/s / 64 KB / 75-300 s | iguales | SPX 1,73 MB gz bajan en ~2,4 s; ráfaga máxima de un trozo |
| Clásica: `VelaMin` | 2 | **1** | M1 en ES (MEDICIONES §6) |
| Clásica: `CongelarEnLaRueda` | true | **false** | primaria SPX en ES |
| Clásica: dividendo | 0,008 | **0,012** | `DividendoUsado` en ES |
| Clásica: medida / medida reciente | no estaban | **están** | cascada del código; en ES pasan (4.858 + 860 AUDIT) |
| Clásica: radio / empate / centroide / dominantes / retraso | 100 / 20 % / 12 / 3 / 960 | iguales | es una réplica: los de la clásica en ES |

---

## 3. Lo probado (arnés `scratchpad\p2\arnes`, DLL compilada copiada a `scratchpad\p2\dll`; salida en `scratchpad\p2\salida_todo.txt`)

**T1 — parseo** (MEDIDO). Crudo único de la medición: `_SPX`, 27-10 03:29 UTC, 1.725.155 B gz / 13.088.116 B JSON.
- Contratos: **29.424** (AM 9.394, PM 20.030).
- `CrudoCboe.Leer`: mediana **100-121 ms** según la corrida, con 1,9 MB asignados (capacidad inicial 31.418: sin duplicar).
- Ciclo entero (leer + construir + medir + línea + relectura) en un hilo BelowNormal: **104-133 ms** y 5,3 MB asignados.
- Proceso al final: 60 MB privados, 16,6 MB vivos en el GC. No bloquea: corre en el hilo propio del descargador.

**T2 — noche del 26-10, 23:29 NY** (MEDIDO).
- La cadena de la 5.0 es **idéntica** a la de la 4.1: 37.994 B, 1.371 filas; ninguna AM a ≤ 14 días.
- Base por forwards:
  - 4.1/Python: **37,17**, 51,9 ticks, cercano 26-10 vencido. cboe-local dio 37,18 para el mismo sello; es otra bajada del mismo sello
    (la diferencia es de redondeo/insumo).
  - 5.0: **35,21**, 8,6 ticks, cercano 27-10, trimestral AM, 1 serie vencida fuera, x = −0,99 d, no confiable (8,6 > 8).
  - Contra la rueda del 26-10 decaída (36,17): 5.0 −0,96; 4.1 +1,00 (acertaba de casualidad, con la recta rota).

**T3 — trimestral AM** (03-09, 18:08 NY, crudo de `datos\cache`; MEDIDO).
- 4.1: 7,39, 8,7 ticks, no confiable.
- 5.0: **8,24**, 7,4 ticks, trimestral U6 AM, **confiable** (q 2,10 %).
- Referencia: la rueda U6 del 08-09 fue 5,45; llevada por carry a 15 días da ~8,2.

**T4 — tercer viernes simulado.** Se usan raíces, OI y vencimientos reales; las cotizaciones son las del crudo y lo que cambia es la hora
"ahora".

Noviembre (mensual AM 20-11: AM 724.432 + PM 87.835 de OI en ±5 %):
- Hasta la víspera a las 08:00 NY la AM está en la cadena como vencimiento propio (1,06 d) y el Hoy es igual en las dos versiones.
- Desde las 09:00 NY de la víspera queda afuera.
- **Víspera, 17:00 NY:**
  - el Hoy de la 4.1 tiene **812.267** de OI; el de la 5.0, **87.835** (9,2×);
  - zero estándar por volumen en el índice: 4.1 7694,79 contra 5.0 7713,61 (**18,8 pts**).
- Viernes 10:00 NY: la 4.1 sigue sumando la AM si CBOE la manda (pasó con NDX el 18-09); la 5.0 no.

Septiembre (trimestral AM 18-09: AM 2.407.692 + PM 168.718):
- **Víspera, 17:00 NY:** Hoy 4.1 **2.576.410** contra 5.0 **168.718** (15×); zero 7744,56 contra 7739,14.

**T5 — la víspera real del 16-10 en cboe-local** (Python, MEDIDO).
- El "16-10" sumado tuvo 1,31-1,34 M de OI todo el jueves y entró al Hoy desde las **16:10 NY con 1,15 M**.
- Bajó a 0,33 M recién cuando CBOE retiró la AM, entre las 18:40 y las 21:10 NY.
- El viernes subió a 0,46 M con el OI nuevo de la OCC.
- Es lo que la 4.1 dibujaba esa noche. Las líneas de Python no traen la raíz: este error queda en la historia de cboe-local (§4).

**T6 — C7 con cotas sobre un día real** (23-10 y su noche; cboe-local + velas MES m1 de la cache de ATAS; gráfico MESZ6).
- 2.469 fotos, 1.943 muestras, **0 fuera de cota**.
- 1.380 minutos: 376 de día y 1.004 de noche, **0 sin base**. Base entre 36,71 y 40,23 (de día ~37; la rueda medida fue 37,20).
- Forwards contra sincronizada: n 951; p05 −2,29, p50 −2,08, p95 −0,39; **0 fuera del tope**.
- Con el gráfico equivocado (MESH7): **1.943 de 1.943 muestras fuera de cota** (a 60,3 pts del carry de 111,7). Los forwards se descartan
  por contrato y **SPX no se dibuja**.

**T7 — réplica de la clásica contra sus 131 AUDIT de ES del 05-10** (`pythiagex-gammahoy.log`, clásica 1.11d con CBOE_SPX).
- **Solo la fórmula** (base y S de la clásica; cadena = la última de cboe-local con generado ≤ t): zero a 0,05 pt en **96/131** y D1-D3
  en **102/131**. El resto es la cadena de ese instante: la clásica leía el radar o el `ultima` con hasta ~1 min de desfase.
- **De día** (la clásica en "medida", 65 AUDIT): la réplica también usa "medida".
  - Diferencia de base: mediana **0,00** (p05 −0,13, p95 +0,06).
  - Diferencia de zero: mediana **0,00** (p05 −0,14, p95 +0,30).
- **De noche** (la clásica en "de la rueda" 52,42, adoptada 17:20 UTC; 54 AUDIT):
  - La réplica usa "medida hace 185 min", 53,69 (**+1,27**). Esa noche la clásica leía el `ultima-ES.json` de cboe-local, que no trae
    `base_ultima_buena`.
  - En los 13.472 AUDIT de ES la clásica usó "medida hace N" **860 veces**, así que el escalón queda.
  - Sin ese escalón la réplica da su rueda, 52,13 (−0,29): la clásica dejó de muestrear a las 13:20 NY.
  - Los orígenes en todo el log de ES: medida 4.858, de la rueda 1.419, medida hace N 860, teórica 1.001, cruda 441, Rithmic 4.893.

**Cómo repetirlo** (10 minutos o más):
- `arnes.exe clasica` lee los AUDIT del 05-10 (`" AUDIT fut=7"` sin `"capa="`) y las cadenas de cboe-local del 28-09 al 06-10.
- Arma un `IFuenteCboe` + `IMedidaCboe` fijo (con la `base` / `base_confiable` de Python de cada línea) y una `ICinta` con las velas m1.
- Compara base, origen, zero y D1-D3 minuto a minuto.
- Para otro día: cambiar las fechas en `T8Clasica`. Con CBOE_SPX solo hay 04-10 (185) y 05-10 (131).

---

## 4. Riesgos y lo que queda abierto

1. **Historia de cboe-local.** `cboe_local.py`, `archivar_cadena.py` y `pythiagex/base.py` siguen sumando AM+PM y metiendo el 0DTE
   vencido en la base de noche. La 5.0 lee esas líneas solo como historia, al arrancar (`CarpetaExtra`). Las horas con ATAS apagado de
   una víspera de tercer viernes llegan con el error (T5).
   - Las cotas de C7 atajan la base: error > 8 ticks y cota de carry.
   - La AM sumada **no** se puede separar sin la raíz. Arreglarlo en el Python está fuera de este paquete.
2. **Margen de la AM.** Si de noche no entra ninguna foto nueva por más de ~1,2 h con el tope del Hoy en 1,0, una AM que estaba adentro
   (días entre 1,0 y 1,05) podría envejecer hasta entrar. Necesita que la bajada se corte de noche más de una hora justo la antevíspera.
3. **`TasaConfianza` fija en 4,1 %.** Si las letras se alejan más de ~0,5 pp, el "confiable" propio diverge del de Python.
   - En la réplica cambia "medida" ↔ "medida reciente" / "rueda".
   - En C7 no pesa: los forwards entran por el error ≤ 8 ticks, no por "confiable".
4. **Contado de noche.** Se evalúa con la hora del `ultimo_trade` (clavado en 16:14:59). Con cotizaciones GTH vivas, la hora real es más
   tarde: ≤ 0,2 pts de sesgo (SUPUESTO por cuenta, sin medir).
5. **Hallazgo, sin cambiar** (es la regla medida de la 4.1 y en ES no está medida). El 23-10 a las 10:00 NY la base sincronizada de día
   daba 39,16 contra forwards 36,27 (−2,89, tope 3), cuando la rueda del día cerró en 37,20.
   - Las muestras de las 09:35-10:00 NY venían ~2 pts altas: el índice abre con acciones sin operar.
   - La clásica empieza a las 09:50 NY. Si se repite, conviene medir arrancar C7 a las 09:50.
6. **La réplica de la clásica en las vísperas de tercer viernes.** La original usa cadenas de Python, con la AM sumada. La réplica, con la
   cadena corregida, no la va a copiar esos días (a propósito).
7. **NDX en la 4.1** (solo lectura, no se tocó) tiene los mismos dos errores: tercer viernes y 0DTE vencido en la base de noche.

## 5. Pedidos a otros paquetes (no los toqué)

**P5 — `ArmadoPantalla`:**
- `OrigenClasica` tiene que reconocer como **normales** `"= medida ("` y `"= medida hace N min"`. Hoy caen en "origen sin dato" y se
  pintan en naranja.
- `AVISO_CLASICA` habla de "base de la rueda anterior" y de Rithmic: es de NQ. En ES la primaria de la clásica es SPX.

**P5 — `GammaCincoPantalla`:**
- La descripción de "Clas SPX 0Γ" cita números de NQ y la "base de la rueda anterior".

**P5 — `IntegracionFamilia`:**
- No hace falta nada: los defaults nuevos (`VelaMin` 1, `CongelarEnLaRueda` false, dividendo 1,2 %, `Cotas`) están en mis opciones.
- Opcional: pasar `EsHabilNy` a `OpcionesSpx` para los feriados. Ya faltaba en la 4.1.

**P3 — `Replica20`:**
- `BaseSpx20.DIVIDENDO_ES` = 0,008 es el de NQ: la 2.0 en ES usa 1,2 %.
- La primaria SPX de la 2.0 en ES también arranca por medida > medida reciente > rueda. Puede usar `_cboe as Cboe.IMedidaCboe` como
  `ClasicaSpx`.
- La clásica ya no usa nada de replica20.

**P4 — capas 3.0:**
- La línea flaca puede traer vencimientos `"am":true`. El parser de las capas solo lee `dias`: no se rompe, y la AM nunca está al alcance
  del Hoy.
- La `base_cruda` de noche ahora viene sin la serie vencida.
- `"base"` / `"base_confiable"` propios ya no son null; las capas no los leen.
