# P4 — Núcleo 3.0 en ES: capas SPX/SPY, majors OI, rayas y recuadro (27-10-2026)

Paquete P4 de la 5.0 "Gamma ES". Base: commit 02e9fc606. Compila con `herramientas/build_serial.ps1` (Build succeeded).
Nada instalado, nada commiteado, nada escrito en `%APPDATA%`.

## 1. Qué hace ahora

En un gráfico de **ES/MES** (raíz "ES") el núcleo arma dos capas de CBOE:

- **SPX**
  - Lee el archivo "ES": la cadena `_SPX` que baja la 5.0.
  - Primero busca en memoria (`FuenteCapaEnMemoria("ES")`). `IntegracionFamilia` la resuelve como `UltimoRegistro("SPX")`.
  - Si no está en memoria, lee `PythiaGex5\cboe\ultima-ES.json`, solo si tiene 20 minutos o menos.
  - Lleva la cadena al precio del gráfico con una **base aditiva**: ES = K + base.
- **SPY**
  - Lee el archivo "SPY" de la misma manera.
  - La lleva con una **razón**: ES = K × razón.

Cada capa escribe su estela y su estado:

- Estela: `PythiaGex5\estela\estela-<SPX|SPY>-<día>.jsonl`.
- Al motor le postea `AlEstela("SPX"|"SPY", línea)`.
- De eso salen **TRES_SPX** y **TRES_SPY**. TRES_SPX viene prendida por defecto en la pestaña.
- Estado: `PythiaGex5\base-rueda-ES.json` y `PythiaGex5\razon-rueda-SPY.json`, con un campo nuevo `"contrato"`.
- Nunca lee ni escribe los archivos de la clásica, de la 2.0 ni de la 4.1.

**TRES_ES** sale del libro ES del núcleo: `GuardarEstela("ES")` desde Tick. Del lado de la familia ya estaba bien:

- `CatalogoFamilia.CapasTres` = {ES, SPX, SPY};
- `AlmacenTres` tiene vida ES 300 s y SPX/SPY 1500 s;
- el piso es 3750.

En **otro gráfico** (por ejemplo MNQ):

- las capas no leen, no cuentan, no guardan ni siembran memoria;
- si el gráfico deja de ser ES, se sueltan;
- el estado queda en "el grafico no es ES/MES";
- la familia tampoco arma: `AsegurarAdaptadoresFamilia` y `FamiliaAsegurarHost` exigen "ES".

## 2. Conversión: cotas MEDIDAS

Fuente: `MEDICIONES_ES.md` §3.8 y `constantes_medidas.json`, con 30 ruedas de ESZ6 y 28 de SPY.

**Toda candidata tiene que pasar `ConversionCabe`.** Si una no pasa, se prueba la siguiente y la descartada queda escrita en el origen, en el log y en el estado. El orden es el de la 4.1:

1. mediana;
2. familia C7/C8;
3. alineada;
4. cruda;
5. forwards.

**Base SPX→ES**

- Sanidad: 0 < b ≤ 150.
- Carry: |b − fut × **0,034** × días/365| ≤ max(25 % de ese valor, 3 pts).
  - (r − q) = 3,4 %: es el implícito medido. Percentil 5: 3,3 %. Percentil 95: 3,6 %.
- Los días se cuentan al vencimiento del **contrato del gráfico**: `ContratoGrafico()`. Por ejemplo, Z6 vence el 18-12-2026 a las 09:30 NY, que es el SOQ.
- Si el código no trae mes, por ejemplo "MES" en una pestaña oculta:
  - vale el último contrato conocido;
  - si nunca hubo uno, se acepta el trimestral más cercano o el siguiente, como hace la clásica.
- **Esto reemplaza la tolerancia de 40 pts de la 4.1.**

**Forwards contra la sincronizada**

- En la rueda: |d| ≤ 3.
- De noche: |d| ≤ 4, y además el residuo de la recta tiene que ser de 8 ticks o menos.
- Sin otra base que valga, alcanza con la cota del carry y, de noche, el residuo.

**Razón ES/SPY:** 9,95 ≤ razón ≤ 10,25. La cota 30-60 de QQQ rechazaba SPY siempre.

**Horario del spot** (`HoraSpotValida`, sobre t_spot = sello − 900 s, de lunes a viernes)

- SPY:
  - de 04:00 a 20:00 NY. A las 20:00 el spot vuelve al cierre oficial; se midieron 36 pts de error.
  - Fuera de ese horario el spot cuenta como quieto: no hay muestras y no se usan la alineada ni la cruda.
- SPX: de 09:35 a 16:00 NY, la regla C7.

**SPY, ex-dividendo**

- Se reinicia el tercer viernes de marzo, junio, septiembre y diciembre a las 04:00 NY: se borran las muestras y se invalida la mediana anterior, también la del archivo.
- Medido el 18-09: la razón saltó unos 15,6 pts de ES.
- El de diciembre (18-12) es un SUPUESTO por analogía.

**Roll**

- Si cambia el contrato del gráfico, se borran las muestras y la mediana.
- El archivo de otro contrato no se carga.
- Medido el 10 y el 11-09: con U6, 6,4 y 3,6; con Z6, 70,9 y 73,0.

**Mediana de SPX de noche**

- Se decae con el carry: × (vencimiento − ahora) / (vencimiento − hora de la mediana). Es la regla C7.
- Medido contra la rueda siguiente: mediana −0,09, MAD 1,07.
- SPY no se decae.

**Vencimiento ya vencido** (`VencimientoYaVencido`)

- Se excluye:
  - una fecha `f` anterior a hoy NY;
  - la de hoy, desde las 16:00 NY;
  - las 09:30 NY si la línea trae `"am": true` (la SPX mensual; P2 la está marcando así en `CboeCadena`).
- Se le pone `dias = -1` y sus filas salen. Así el horizonte "Hoy" no queda clavado en el 0DTE muerto.

**Default `Capa3BaseSpx` = Forwards**, igual que la 4.1.

- Medido: forwards − sincronizada da mediana −0,72.
- En la rueda, la capa SPX se dibuja unos 0,7 pts por debajo de lo que daría la base de la familia (C7).
- Ver el riesgo 2.

## 3. Constantes reescaladas (NQ → ES, ÷4 a ticks de 0,25; `PtsRaiz`/`EscalaRaiz`: ES 0,25, RTY 0,2)

**Capas**

| Dónde | 4.1 (NQ) | 5.0 (ES) | Por qué |
|---|---|---|---|
| `Capa3RadioDibujoPts` | 60 | 15 | ÷4 (los rótulos siguen hasta `RadioDibujo` ES = 40) |
| Capas: radio de dominantes | `RadioDominantes` | 25 | Ya era por raíz: se copia del primario |
| Capas: centroide | 12 | 12 | Nació para SPX (unos 2 strikes por lado; la clásica usa 12 en ES) |
| `Capa3FusionPts` | 5 | 5 | Es un strike (paso 5 en ES y en SPX). Con "Todas independientes", el default, no actúa |

**Muros y fórmulas**

| Dónde | 4.1 (NQ) | 5.0 (ES) | Por qué |
|---|---|---|---|
| F5/F6 muros (`RadioFormula`) | ±100 fijo | ±25 (`RadioMuros`) | ÷4 |
| `Formulas3RadioPts` | 150 | 0 = `RadioDibujo` (ES 40) | Ya estaba por raíz |
| `Recuadro3RadioMuros` | 100 | 0 = auto (ES 25) | ÷4 |

**Cruces**

| Dónde | 4.1 (NQ) | 5.0 (ES) | Por qué |
|---|---|---|---|
| `Zero3CrucesRadioPts` | 20 | 0 = auto (ES 5) | ÷4 |
| `CrucesOiRadio3Pts` | 60 | 0 = auto (ES 15) | ÷4 |
| Relleno "sin cortes": 1,0 y 0,5 | 1,0 / 0,5 | Igual | Son posiciones interpoladas entre strikes (paso 5 igual) |
| Recuadro: strike cercano a 7,5 y a 2,5 | 7,5 / 2,5 | Igual | Sale del paso de strike |

**Caja negra**

| Dónde | 4.1 (NQ) | 5.0 (ES) | Por qué |
|---|---|---|---|
| Placebos de la caja | ±12,5 / ±25 | ±3,25 / ±6,25 | ÷4 |
| Anti-repetición de la caja | 3 pts | 0,75 | ÷4 |
| Familia del primario en la caja | "NQ" fijo | La raíz ("ES") | |
| `CargaRadioPts` (rango) | 0-300 | 0-75 | = `ReglasFam.CARGA_RADIO_TECHO` |

## 4. Ajustes renombrados (DLL nueva: nada guardado)

Nombres de 12 caracteres o menos, mismo `Order`. La primera frase de la descripción dice qué llave necesita.

**Capas SPX** (antes NDX)

| Propiedad 4.1 | 5.0 | Nombre visible | Order |
|---|---|---|---|
| Raya3NdxD1 | Raya3SpxD1 | ↳ SPX D1 | 63 |
| Raya3NdxD2 | Raya3SpxD2 | ↳ SPX D2 | 64 |
| Raya3NdxD3 | Raya3SpxD3 | ↳ SPX D3 | 65 |
| Raya3NdxZeroB | Raya3SpxZero | ↳ SPX 0Γ | 70 |
| Raya3NdxZeroEnfasis | Raya3SpxZeroEnfasis | ↳ SPX 0Γ enf | 71 |
| Raya3NdxZeroCerco | Raya3SpxZeroCerco | ↳ SPX 0Γ t/p (techo/piso) | 72 |

**Capas SPY** (antes QQQ)

| Propiedad 4.1 | 5.0 | Nombre visible | Order |
|---|---|---|---|
| Raya3QqqD1 | Raya3SpyD1 | ↳ SPY D1 | 66 |
| Raya3QqqD2 | Raya3SpyD2 | ↳ SPY D2 | 67 |
| Raya3QqqD3 | Raya3SpyD3 | ↳ SPY D3 | 68 |
| Raya3QqqZero | Raya3SpyZero | ↳ SPY 0Γ | 73 |
| Raya3QqqZeroEnfasis | Raya3SpyZeroEnfasis | ↳ SPY 0Γ enf | 74 |

**Majors OI**

| Propiedad 4.1 | 5.0 | Nombre visible | Order |
|---|---|---|---|
| Raya3NdxMasOi | Raya3SpxMasOi | ↳ SPX M+ OI | 48 |
| Raya3NdxMenosOi | Raya3SpxMenosOi | ↳ SPX M− OI | 49 |
| Raya3QqqMasOi | Raya3SpyMasOi | ↳ SPY M+ OI | 50 |
| Raya3QqqMenosOi | Raya3SpyMenosOi | ↳ SPY M− OI | 51 |

**Configuración de las capas**

- `Capa3BaseNdx`/`BaseNdx3` pasan a `Capa3BaseSpx`/`BaseSpx3`, con el nombre "Base de SPX: de donde sale".
- `Capa3SPX`/`Capa3SPY` ya existían. Ahora `ModoDe` usa `PorRazon`.

**Las Raya3Nq\* conservan el nombre de la propiedad.** Gobiernan el libro primario y solo cambia el texto:

- "↳ ES D1", "↳ ES D2", "↳ ES 0Γ";
- "↳ ES M+ vol", "↳ ES M− vol";
- "↳ ES M+ OI", "↳ ES M− OI".

**Llaves:** "3.0 NQ dom▸" pasa a "3.0 ES dom▸" y "3.0 NQ 0Γ▸" a "3.0 ES 0Γ▸". Todas las descripciones del grupo que las nombran quedaron actualizadas, incluida una línea de `GammaHoyTresAtravesadas.cs`, sin lógica. El lector de llaves de `GammaHoyTresCasillas` las encuentra todas.

**Textos:** "LIBRO NQ" pasa a "LIBRO ES" y lo medido en NQ dice "SIN MEDIR EN ES".

**Verificado por reflexión sobre la DLL** (`scratchpad\p4_arnes\grupo`):

- 107 casillas en el grupo 0;
- ningún nombre repetido;
- todas las llaves citadas existen;
- no queda texto "3.0 NQ", "LIBRO NQ", "↳ NDX" ni "↳ QQQ".

## 5. Cómo verificar en pantalla cada raya (comprobación fehaciente)

Todo sale del log `pythiagex5-gammahoy.log`.

**Una vez por minuto, línea `capa SPX:`** Trae:

- origen ("cboe-5.0 (memoria)" o archivo);
- edad del dato;
- **base B** (con su origen: mediana / familia / alineada / forwards, y las descartadas con su motivo);
- strikes, doms d1/d2/d3 y zero.

**Por cada muestra, líneas `capa SPX: base muestra`.** Traen:

- la muestra;
- la vela a la hora del spot;
- el spot;
- la mediana, de n/N muestras;
- el contrato.

**Con SPY pasa lo mismo:** `capa SPY: ... razon R`.

**Cuando no se convierte:** `capa X: sin conversion confiable: … · descartadas: …`.

Para cada raya visible:

1. **SPX D1/D2/D3**
   - Tomá `PythiaGex5\cboe\ultima-ES.json` con el mismo `cadena.ts` que la línea del log.
   - Sacá las filas cuyo vencimiento esté vencido: `f` anterior a hoy NY, la de hoy después de las 16:00 NY, o la marcada `am` después de las 09:30 NY.
   - Quedate con el horizonte "Hoy": días envejecidos ≤ max(1, el más cercano + 0,01).
   - GEX por volumen = Black-Scholes con r 0,0375, S = fut − B, multiplicador 100.
   - Regla Clásica:
     - una dominante por lado dentro de 25 pts del precio;
     - empate al 20 %;
     - centroide de 12 pts.
   - Precio en el gráfico = K + B. La D1 es la más cercana al precio.
   - La raya tiene que caer ahí ±0,25.
2. **SPX 0Γ / techo y piso**
   - Es el cruce de signo del perfil por strike, por volumen, interpolado y llevado a ES (+B).
   - Con "↳ SPX 0Γ t/p" son los dos cruces que encierran al precio.
3. **SPY D1-D3 / 0Γ**: lo mismo con `ultima-SPY.json` y S = fut / R. Precio en el gráfico = K × R.
4. **SPX/SPY M± OI**
   - Es el strike con mayor GEX neto por OI positivo (o negativo) sobre todo el perfil.
   - Se lleva igual que el resto (+B o ×R).
5. **La conversión misma**
   - B tiene que estar en (0, 150] y a max(25 %, 3) de fut × 0,034 × días/365, con los días al vencimiento del contrato del gráfico.
   - Hoy, 27-10, con el futuro en unos 7.715: unos 37 ± 9 contra el 18-12 a las 09:30 NY.
   - R tiene que estar en [9,95; 10,25].
   - Si dice "forwards", |forwards − la otra| ≤ 3 en la rueda, o ≤ 4 con residuo de 8 ticks o menos de noche.
6. **TRES_SPX / TRES_SPY (pestaña de la familia)**
   - Sus D1/D2 son `d[0]`/`d[1]` de la última línea de `estela-SPX-<día>.jsonl`, en precio de MES.
   - Tienen que coincidir con las rayas SPX D1/D2 de la 3.0.
7. **ES D1/D2/0Γ/M±**: libro ES por Rithmic. Sin conversión: strike del futuro, Black-76, multiplicador 100 en la cuenta.
8. **En MNQ**
   - Ninguna capa.
   - En el log, "memoria capas: el grafico no es ES/MES".
   - El estado de las capas dice "el grafico no es ES/MES".

**Arnés de las funciones puras** (`scratchpad\p4_arnes`, por reflexión sobre la DLL, sin escribir nada): **45 de 45 OK**. Cubre:

- el vencimiento de Z6, U6, H7 y Z26, con DST;
- el reinicio por ex-dividendo de SPY;
- el horario del spot;
- las 12 ruedas de Z6 de la tabla 3.2: rueda y forwards caben;
- el roll: con U6 caben 6,4 y 3,6, y 70,9 no; con Z6, al revés; sin mes, vale cualquiera de los dos;
- las cotas de SPY;
- el decaimiento: 36,378 → 36,062 de las 16 a las 03 NY;
- el vencido: PM 16:00, AM 09:30;
- `PtsRaiz` y los muros: ES 25, NQ 100, RTY 20.

## 6. Riesgos y pendientes

1. **Ya resuelto por P5 (verificado en su copia de trabajo):** el eje doble de la pantalla aceptaba la razón de SPY solo entre 30 y 60. Ahora usa 9,95-10,25 y (0, 150], las mismas cotas que las capas.
2. **Base por defecto:** Forwards, como la 4.1. Queda unos 0,7 pts por debajo de la sincronizada de la familia (C7), así que una raya SPX de la 3.0 y una de la familia sobre el mismo strike pueden verse a unos 3 ticks de distancia. Si se prefiere coherencia con la familia, `Capa3BaseSpx = Rueda`. Lo decide el coordinador con el operador; no lo cambié.
3. **Carga:** los defaults siguen en 50 / 12 / 10, los de NQ. Medido: en ES, con 50 se marca el radio entero (todos los strikes tienen 528 contratos o más). Pendiente con el operador; P1 propone 1.000 / 6 / 5.
4. **SPX AM del tercer viernes:**
   - Que salga a las 09:30 depende de que la línea flaca traiga `"am": true` (P2).
   - Sin la marca, la AM queda sumada a la PM hasta las 16:00, porque no se puede separar.
5. **Primera noche sin mediana propia:**
   - La capa SPX usa la familia C7, o forwards si el residuo es de 8 ticks o menos.
   - Si no hay ninguna de las dos, no dibuja y el estado dice por qué.
   - Es preferible no dibujar a dibujar corrido.
6. **TODO lo que dibujan las capas está SIN MEDIR EN ES** como nivel operable. Las cotas solo garantizan que la conversión es la medida.
