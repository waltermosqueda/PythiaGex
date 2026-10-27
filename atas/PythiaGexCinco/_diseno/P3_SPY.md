# P3 — SPY y la réplica de la 2.0 en la 5.0 (27-10-2026)

Constructor P3 de PythiaGex 5.0 - Gamma ES. Paquete: `_modulos/familia/spy/*` (ConversionSpy, MinuteroSpy, SeriesSpy) y
`_modulos/familia/replica20/Replica20.cs`. No se tocó nada fuera del paquete. Compila con `herramientas/build_serial.ps1`
(0 errores; 0 avisos en estos cuatro archivos). No se commiteó, no se instaló, no se tocó ATAS ni `%APPDATA%` (solo lectura).

Cómo leer los números:
- **MEDIDO**: lo corrió el arnés de esta sesión con datos reales del disco.
- **CÓDIGO**: leído en el código.
- **SUPUESTO**: no medido.

---

## 1. Qué cambió, por archivo

### `spy/ConversionSpy.cs` (la C8: razón ES/SPY sincronizada)
La regla de la 4.1 queda igual: muestra = MES en (ts − 900 s) / spot de SPY, solo con el spot vivo; mediana robusta de las últimas 24 con
piso 0,0002 × mediana; ≥ 5; reinicio si cambia el contrato. Se le agregan tres cosas de ES, cada una con su opción en `OpcionesSpy`
(las tres prendidas por defecto):

1. **Horas** (`VentanaHorariaNy`). Solo entran muestras con t_spot NY en [04:00, 20:00).
   - A las 20:00 NY el spot de SPY vuelve al cierre oficial. Para la prueba de "spot vivo" eso es un cambio, así que la 4.1 lo tomaba como
     muestra (ver §3, prueba 3).
2. **Cota** (`CotaRazon`). Muestra y razón en [9,95, 10,25].
   - Medido: 10,081-10,127.
   - ES/SPY = (SPX/SPY) × (1 + base/SPX) va de ~10,015 (al vencimiento) a ~10,14 (recién rolado).
3. **Ex-dividendo de SPY** (`ReinicioExDividendo`, tabla `ExDividendoSpy.Fechas`, **editable**).
   - A las 04:00 NY de ese día la ventana se reinicia: se descartan las muestras de antes.
   - El corte se aplica por `generado`: la foto rige desde que se bajó. Las muestras se aceptan solo con t_spot ≥ corte.
   - Hasta juntar 5 muestras nuevas la razón es NaN y SPY **no se dibuja**. Ver §2 por qué no se usa "la base de SPX × 10,032".
   - Tabla: 18-09-2026 (MEDIDO), 18-12-2026 (mismo día que vence ESZ6), 19-03-2027, **17-06-2027**, 17-09-2027, 17-12-2027 y 2028.
     El 17-06 es jueves porque el viernes 18-06-2027 es feriado de Juneteenth observado; la fecha que fije SPY está SIN VERIFICAR.
   - Después de la tabla rige la regla: tercer viernes de mar/jun/sep/dic, o el día hábil anterior si es feriado (`TiempoFam.EsFeriadoBolsa`).
   - `FotoSpy` suma `Descarte` (por qué no dio muestra), `ExDivUtc`, `MuestrasDesdeExDiv` y `ReinicioExDiv`.
   - `LineaMuestra` agrega `"descarte"` y `"exdiv"` a `muestras-SPY-*.jsonl`. `LeerLineaMuestra` y la sembradora los ignoran.

### `spy/MinuteroSpy.cs`
- **MULT 100** (sin cambio). Zero estándar con `ZeroEstandarFam.SPY`: 0,05 USD / ±7,5 USD / centro 1,0.
  - El centro de la clave de la caché ahora sale de `ZeroEstandarFam.SPY.CentroPaso`: es el mismo 1,0, pero ya no está escrito a mano.
- El radio lo pone `SeleccionFam.RadioDe("SPY")` = 20 en las series (CÓDIGO de P1). Un strike de SPY es 1 USD ≈ 10,08 pts de ES.
- **Ex-dividendo en el minuto.** Si el corte (04:00 NY) ya pasó a la hora del minuto y la foto vigente es de antes, no hay libro SPY.
- `MotivoSinLibro(t)` y `UltimoMotivo` dicen con números por qué no hay libro. Ejemplos:
  - "ex-dividendo de SPY del 18-09: razón recién reiniciada a las 04:00 NY, 3 de 5 muestras nuevas";
  - cadena vencida (C9);
  - roll;
  - "N de 5 muestras".
- `Fuente(null, t)` usa ese motivo.
- `ConvTexto`: "razon X (mediana de N muestras sincronizadas MES(ts − 900 s)/spot SPY de 04:00-20:00 NY[; ex-dividendo …, M muestras nuevas])".

### `spy/SeriesSpy.cs`
- Banda **±5**: medio strike de SPY en el eje de ES (en NQ era ±20: un strike de QQQ eran ~41 pts de NQ). Es la misma que puso P5.
- Textos de ES.
- `EstadoSerie` = "SIN MEDIR EN ES (en NQ, otro producto: …)".
- Solo la usan los arneses: el indicador lee `CatalogoFamilia` (P5).

### `replica20/Replica20.cs`
La 2.0 en un gráfico de ES, verificada en su código (PythiaGexDos 2.0.6, raíz "ES"):

**Qué dibuja la 2.0 en ES**
- No arma capas (`GammaHoyCapas.cs:437-438`).
- Con "Libro automático" su único libro es **CBOE_SPX**, por base aditiva (`GammaHoy.cs:88`).

**R20_SPX_vol = ese libro**
- Horizonte Hoy.
- Black-Scholes con r 0,0375, sin dividendo; ×100.
- Las 2 de mayor |GEX vol| a ≤ min(2 % del futuro, 100) = **100 pts**, sin lado ni centroide (`DominantesReferencia`, `GammaHoy.cs:1508`).
  Si en el radio no hay volumen, por OI.
- Retraso 960 s.
- No se reescala nada: es la réplica.

**Su base, la cascada entera** (`GammaHoyNucleo.cs:298-321`), en `BaseSpx20.Cascada`, en este orden:
1. **medida**: de la tabla `Cboe.IMedidaCboe` de P2 (la `base_confiable` de cada línea).
   - Si la fuente no tiene la tabla (un arnés), el respaldo es residuo ≤ 8 ticks.
2. **medida reciente** (≤ 360 min): está implementada, pero **apagada por defecto**, `OpcionesReplica20.MedidaReciente`. Ver §2.
3. **de la rueda** (≤ 24 h): `RuedaSpx20` / `BaseEs20`, port 1:1 de `MedirBaseRueda` (`GammaHoy.cs:1656-1723`).
   - Una muestra por sello nuevo, con el sello entre 13:50 y 20:10 UTC y la cadena de ≤ 30 min.
   - Vela de 1 min del MES de (ts − 960 s) − spot; últimas 24; mediana alta; tope max(3 MAD, 0,0002 × precio).
   - Con ≥ 3 buenas toma la mediana de las buenas; con ≥ 5 (o sin base) adopta.
   - La lista arranca vacía cada día UTC.
4. **CRUDA** (con la cota).
5. **TEÓRICA** (el carry).
6. **CRUDA sin cota**.

Cómo se acota cada escalón:
- Carry = fut × (0,0375 − **0,012**) × días al vencimiento / 365.
- Cota: |b − carry| ≤ max(0,6 |carry|, 0,0006 fut).
- "medida" también vale contra el trimestre siguiente si el vencimiento es SUPUESTO (código sin mes).

Lo demás:
- `BaseSpx20.DIVIDENDO_ES` pasa de **0,008 a 0,012**: es `DividendoUsado()` de la 2.0 en ES, `GammaHoy.cs:1649`.
- La vela de la muestra de la rueda (`CierreVela1mRueda`) es el último tick **de ese minuto**.
  - Sin ese tick, toma la m2 interpolada y la marca provisional: se vuelve a mirar, como las razones de SPY.
  - No acepta un tick de antes de un hueco de la cinta: así una muestra no queda corrida para siempre.
- `IExtrasRehacer`:
  - la generación sube también si cambia la rueda de SPX;
  - `Rehacer` rehace **R20_SPY_vol y R20_SPX_vol**;
  - `RehacerDesdeUtc` es el mínimo de lo cambiado desde la última lectura.
- `Expiraciones(t)` devuelve el vencimiento y el alternativo; `EstadoBaseEn(t)` sirve para diagnósticos.

**R20_SPY_vol.** No tiene original en ES: la 2.0 solo dibuja SPY con "Libro automático" apagado y CBOE_ETF a mano.
- Queda como "selección estilo 2.0 sobre SPY con la conversión estilo 2.0" (`EscalarCon` tal cual).
- El catálogo de P5 la rotula así y la deja apagada.

**DOMS_SPX_vol / DOMS_SPY_vol.** La selección de la 2.0 sobre los libros de la 5.0 (C7 / C8). Sin cambios.

**Texto de la fuente "2.0 SPX".** Es `base 2.0 X (ORIGEN; cota: carry Y +-60 % (dividendo 1.2 %))`.
- ORIGEN empieza con `medida`, `medida hace`, `de la rueda`, `CRUDA`, `TEORICA` o `… (sin cota)`.
- P5 ya lo lee con paréntesis balanceados (`ArmadoPantalla.OrigenBase20`): verificado en su código.

---

## 2. Decisiones y por qué

### Ex-dividendo: no dibujar SPY hasta juntar 5 muestras nuevas, en vez de "SPX × 10,032"
SPX/SPY es justamente lo que salta el día del ex-dividendo:
- **MEDIDO** (por MEDICIONES_ES §3.7 y DISENO_5_0 §4, no por este arnés): 10,0155 el 17-09 contra 10,0401 el 18-09, y 10,032 el 22-10, que
  es el medio del ciclo.
- El 18-12, con SPX/SPY en ~10,040-10,045 (**SUPUESTO**, por analogía con septiembre), usar 10,032 dejaría los niveles de SPY ~6-10 pts
  corridos, sin medir.
- Además hace falta la base C7 del módulo SPX (paquete P2), que llegaría por la integración (P5).

Lo seguro es no inventar el nivel. La pestaña dice por qué falta y cuántas muestras van. El hueco esperado: desde las 04:00 NY hasta la
5.ª muestra nueva.
- El 18-09 fue de 04:00 a 07:49 NY, con fotos ralas (**MEDIDO**, §3 prueba 5).
- Con la bajada propia cada 300 s y el spot vivo en el 25-75 % de las fotos del pre-mercado serían ~35-100 min (**SUPUESTO**, por cuenta).

### "medida reciente" apagada en la réplica de la 2.0
La 2.0 la lee del json del radar de la nube (`base_ultima_buena`).
- Con `FeedMinuto` (default true) gana la cadena de `generado` más nuevo (`GammaHoy.cs:950-953`).
- Con `cboe_local.py` corriendo, que es lo normal hoy (cboe-local actualizado el 27-10), gana el `ultima-ES.json` local, que **no trae ese
  campo**: la 2.0 pasa de "medida" directo a "de la rueda".

**MEDIDO 26-10** con el escalón prendido:
- 355 de 390 minutos de la rueda salían de "medida hace N";
- quedaban 1,46 pts por debajo de la rueda (mediana; p05 −3,24; |máx| 3,24).

**Ojo:** P2 dejó ese escalón **prendido** en la réplica de la clásica. Su propia medición del 05-10 a la noche lo pone peor: +1,27 con el
escalón contra −0,29 sin él. Si el coordinador quiere las dos réplicas iguales, es una línea en `IntegracionFamilia` (P5):
`MedidaReciente = true` aquí, o lo contrario allá.

### La ventana de la rueda de la 2.0 queda en UTC fijo (13:50-20:10), como la 2.0
- Hasta el 31-10 es 09:50-16:10 NY.
- **Desde el 01-11 (invierno) es 08:50-15:10 NY.** Las muestras de 08:50-09:30 NY comparan el MES con el spot de SPX de la víspera (el
  índice abre a las 09:30), y la rueda sale corrida lo que se movió la noche (**SUPUESTO** ~±20 pts en los días que se mueve).
  - Pega solo cuando la cadena no es "medida": la 2.0 tiene ese defecto, y la clásica de P2 también (misma regla).
- `OpcionesReplica20.RuedaVentanaNy = true` lo corrige a 09:50-16:10 NY.
- **Decisión pendiente del coordinador antes del 01-11.**

### Lo que no se reescala (y por qué)
- **Réplica de la 2.0:** radio 100, 2 dominantes, retraso 960 s, guardia del 0,6 %, cordura 5 %, VIEJA_MIN 20. Es lo que la 2.0 hace en ES.
- **C8:** piso 0,0002 × mediana. Es relativo y medido: 3 MAD = 0,0018, casi igual que el piso de 0,0020 (≈ 1,5 pts).
- **Minutero:** el FILTRO de 3 % es relativo.

---

## 3. Lo probado (arnés `scratchpad\p3\arnes`, contra la DLL compilada `bin\Release\PythiaGexCinco.dll`)

Datos (solo lectura):
- Cadenas de SPY y SPX archivadas: cboe-local, `PythiaGex\cadenas`, `PythiaGex2\cadenas` y `PythiaGex4\cboe`.
- Velas m1 del MES de la cache de ATAS (`scratchpad\mediciones_es\velas_cache_hoy\velas-MES-m1.csv`, del 07-09 al 26-10).
- Salidas en `scratchpad\p3\salidas\`.

**1. La C8 de la DLL contra la de MEDICIONES (Python, m31), foto por foto** (MEDIDO). Con la regla de la 4.1 (las tres opciones nuevas
apagadas), sobre las mismas 9.587 fotos de SPY del 14-09 al 27-10:
- **9.569 idénticas** (< 1e-9);
- 18 NaN en las dos;
- 0 NaN en una sola;
- máx |dif| 0.

**2. La C8 de la 5.0 en 28 días de Z6** (MEDIDO):
- razón de la rueda: mín **10,08060** (26-10), máx **10,12688** (21-09), mediana **10,10354**;
- MAD de las muestras de la rueda: **0,45 pts de ES** (p95 0,70);
- after-hours − rueda: **+0,24** (p05 −0,58, p95 +0,86; n 27);
- **0 muestras fuera de cota.**

Coincide con MEDICIONES_ES §3.7.

**3. La trampa de las 20:00** (MEDIDO). La 4.1 aceptaba **23** muestras con t_spot entre 20:00 y 04:00 NY. Su |error| contra la rueda del
día:
- mediana 8,8 pts;
- p95 **35,4**;
- máx **37,2** (16-09 20:05 NY).

La 5.0 las descarta. Razón 5.0 − 4.1, foto por foto: mediana 0,00, p95 0,03, p99 0,12 pts; el máx, 16,28, es la mañana del ex-dividendo.

**4. MinuteroSpy + SeriesSpy de punta a punta, 26-10** (MEDIDO):
- **1.378 minutos con libro SPY**. 2 sin libro (sin precio del MES), y el motivo lo dice.
- máx |Fut − K × razón| = 0.
- Recalculados por fuera, con radio min(2 %, 20): el muro C (argmax GvC), el muro P (argmin GvP), M+ y M−. **0 minutos distintos.**
- ~46 strikes en ±3 %.

**5. El ex-dividendo del 18-09** (MEDIDO).
- La razón de la rueda pasó de **10,10645** (17-09) a **10,12614** (18-09): +0,01970.
- **Regla de la 4.1:** −15,5 pts de error desde las 04:00 hasta las 11:43 NY, −7,8 a las 11:59, bien desde las 12:35.
- **5.0 a nivel foto:** se reinicia en la primera foto después de las 04:00 NY (05:11 NY: no había fotos de 23:47 a 05:11). Sin razón
  hasta la 5.ª muestra nueva (07:49 NY); desde ahí **+0,6 pts**, y +0,2..+0,8 el resto de la mañana.
- **5.0 en el minuto (MinuteroSpy):**
  - 04:00 NY: "SIN LIBRO: ex-dividendo de SPY del 18-09: la razón se reinicia a las 04:00 NY y la cadena vigente es de antes (03:47Z)";
  - después "0/1/2/3/4 de 5 muestras nuevas";
  - **07:50 NY razón 10,12692**, con el texto del ex-dividendo.
- Sin reinicio quedaba 10,1057 hasta las 12:00 NY.

**6. Tabla de ex-dividendos** (prueba de código). Los cortes caen a las 08:00Z en verano y a las 09:00Z en invierno (04:00 NY), por ejemplo
el 18-12-2026 a las 09:00Z. La regla da los mismos días que la tabla, y Juneteenth 2027 cae el jueves 17. Después de la tabla:
16-03-2029, etc.

**7. "medida": el respaldo por residuo contra `base_confiable`** (MEDIDO, 1.979 cadenas de SPX del 20 al 27-10).

| | confiable y ≤ 8 | confiable y > 8 | no confiable y ≤ 8 | no confiable y > 8 | acuerdo |
|---|---|---|---|---|---|
| Rueda | 1.180 | **0** | 18 | 371 | **98,9 %** |
| Noche | 56 | 0 | 2 | 352 | **99,5 %** |

Con la tabla de P2 (el camino por defecto en vivo), en 22-10 y 26-10:
- la base distinta del respaldo en **2 y 12 minutos** de ~1.380 (máx 4,25 y 1,81 pts);
- D1/D2: **los mismos strikes siempre**.

**8. R20_SPX recalculada por fuera** (`scratchpad\p3\verificar_r20_spx.py`: Python, sin el código C#, desde las cadenas crudas y las velas).
Cada 7 min, en los dos modos (tabla y residuo):
- 22-10: **198 de 198** minutos con la misma base y el mismo escalón, y **198 de 198** con las mismas D1/D2 (strike y GEX a 0,05 M);
  máx |dif base| 0.
- 26-10: **197 de 197** y **197 de 197**.

**9. La rueda de la 2.0 contra la rueda C7 de MEDICIONES** (MEDIDO; la regla de la 2.0 alinea a 960 s con el cierre de la vela, y C7 a
900 s interpolado):
- 26-10: 36,50 (adoptada a las 20:11Z con 22 buenas) contra 36,38.
- 22-10: 40,92 contra 40,29 (última hora 40,57).

El 22-10 los forwards confiables daban 37,72: ese día la 2.0 ("medida") y la 5.0 (sincronizada) quedaron a ~2,6 pts. Es real, no un
error de la réplica (MEDICIONES: dif_fwd − rueda −2,57).

**10. El camino del motor, 26-10** (`Replica20.Calcular` + `Rehacer`, con SPX y SPY y la tabla) (MEDIDO):
- 1.378 minutos con R20_SPX, R20_SPY y DOMS_SPY;
- `Rehacer` cambió **0** minutos (tiene que ser 0); generación 0 → 0.
- De noche, R20_SPY (estilo 2.0) queda a ~5,5 pts de DOMS_SPY en K 763: la 2.0 junta el MES de las 16:14 del viernes con el spot de SPY de
  ahora. Es la falla conocida de la 2.0 y el texto lo dice.

**Cómo repetirlo** (dotnet 10; ~10 min en total; prioridad BelowNormal):
```
cd scratchpad\p3\arnes && dotnet build -c Release -o bin
bin\arnes_p3.exe c8 <m30_serie_SPY.csv> <velas-MES-m1.csv> <salida.csv> <m31_spy_fotos.csv>
bin\arnes_p3.exe exdiv
bin\arnes_p3.exe minutero 2026-10-26 <velas> <salida.csv>
bin\arnes_p3.exe minutero-exdiv <velas>
bin\arnes_p3.exe spx 2026-10-22 <velas> <salida.csv> [tabla] [reciente] [ny]
bin\arnes_p3.exe extras 2026-10-26 <velas>
python -I scratchpad\p3\verificar_r20_spx.py 2026-10-22 <velas> <salida spx.csv> [tabla]
```

---

## 4. Riesgos y lo que queda abierto

1. **Hueco de SPY el día del ex-dividendo** (04:00 NY → 5.ª muestra nueva). Medido ~3 h 50 min el 18-09, con fotos ralas; con la bajada
   propia, ~35-100 min (SUPUESTO). Es deliberado: no se dibuja un nivel que se sabe corrido ~16 pts.
2. **Fechas de ex-dividendo después del 18-12-2026**: por regla. La de junio 2027 (Juneteenth) está SIN VERIFICAR. Es una tabla editable
   en `ConversionSpy.cs`.
3. **Ventana UTC de la rueda de la 2.0 en invierno (desde el 01-11)**: ver §2. Hay que decidir antes del 01-11; afecta también a la clásica.
4. **"medida reciente"**: la 2.0 a veces la tiene (cuando gana el radar). Con el escalón apagado, esos ratos la réplica usa la rueda.
   Diferencia medida: ~1,5 pts.
5. **"medida" de las cadenas propias** usa `TasaConfianza` 4,1 % fijo (P2), no la curva del Tesoro. Si las letras se alejan más de ~0,5 pp,
   puede cambiar "medida" ↔ "rueda" (riesgo de P2).
6. **Sin original con que comparar**: la 2.0 no corre en ES desde el 05-10 (5 AUDIT ese día). La réplica está verificada contra su
   CÓDIGO y recalculada por fuera, no contra su salida en vivo.
7. **R20_SPY_vol** hereda las fallas de la 2.0 con un ETF (la razón de noche, la guardia del 0,6 %, que en ES queda cerca del salto del
   roll). Está apagada por defecto (P5).
8. **Nada de esto valida un nivel como operable en ES**: fija la conversión y la réplica. Cada serie dice "SIN MEDIR EN ES".

## 5. Para otros paquetes (no los toqué)
- **P5 / coordinador:**
  - `OpcionesReplica20.MedidaReciente` (aquí false) contra la clásica de P2 (true): decidir si se igualan.
  - `RuedaVentanaNy` en las dos réplicas antes del 01-11.
  - Opcional: mostrar `MinuteroSpy.UltimoMotivo` en el aviso cuando falta SPY. Hoy el motor dice genérico "sin cadena vigente de CBOE o
    sin conversion"; el motivo del ex-dividendo queda en `MotivoSinLibro`.
- **P2:** nada. Se usa su `IMedidaCboe` tal cual. La clásica ya no depende de `Nucleo20` / `BaseSpx20`.
