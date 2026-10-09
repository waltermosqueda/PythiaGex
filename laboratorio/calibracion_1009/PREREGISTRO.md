# PRE-REGISTRO — calibracion_1009: "la dominante en la punta de la mecha"

Escrito el 09-10-2026 entre la 01:05 y las 01:50 ART (04:05–04:50 UTC) por el agente de pre-registro y arnés del workflow
calibracion_1009. **CONGELADO**: el sha256 de este archivo está en `PREREGISTRO.sha256`. Cualquier cambio posterior va en la sección
15 (DESVÍOS) con fecha, hora y motivo, y no reemplaza nada de lo de arriba.

Nada de lo que sigue es una estrategia ni promete rentabilidad. El GEX describe régimen (compresión o expansión), nunca dirección.
Lo que se mide acá es **dónde** frenó el precio y **cuánto** recorrió después, contra placebo.

---------------------------------------------------------------------------------------------------------------------------------

## 0. Qué se pregunta

El operador pide "la fórmula de equilibrio" que dibuje las dominantes **en los extremos**: la mecha del techo o del piso toca la raya,
las velas siguientes la pueden probar pero no romper, y después viene el cambio de tendencia con el mayor recorrido posible hacia la
otra dominante. No le sirven rayas en el medio del rango tocadas muchas veces.

Aclaración del operador del 09-10 (manda sobre el juez anterior): la raya **puede ser atravesada un poco** por mechazos o por velas
de cuerpo chico o de indecisión, siempre que al final el precio vuelva y cumpla el giro. Él lo llama falso rompimiento (el que deja
atrapados a los que entraron a favor de la ruptura). Ese caso cuenta como **acierto** y además se cuenta aparte.

Pregunta operativa: ¿existe alguna fórmula de rayas (de gamma o no) cuyo % de **extremos sostenidos** le gane a la misma raya puesta
al azar (misma cantidad, misma forma, misma distancia al precio), de noche o de día, en sesiones que no se usaron para elegirla?

## 1. Lo que ya se sabe y lo que ya se vio (conocimiento previo declarado)

Medido antes en este proyecto (memorias del operador y resultados del laboratorio):
- 11 rondas de búsqueda sin ninguna regla que le gane al placebo fuera de muestra (busqueda-estrategia-sin-parar.md, estrategia-ronda-*.md).
- Laboratorio del 03-09 (un solo día): gamma × OI perdió contra su placebo (68,6 % contra 72,6 %); ganaron las de volumen del día
  (laboratorio-formulas.md). En NQ las dominantes dieron 62 % contra 65 % del placebo en 29 toques (es-vs-nq-respeto.md).
- tres/resultados/extremos_rebote.md (08-10, juez de toque, 22 noches): ninguna fórmula ganó los 4 criterios; MUROS_vol fue la mejor
  por extremo de noche (60,5 % contra 52,8 % del corrido y 50,4 % del azar, 311 toques) sin rebotar más que el azar.
- tres/auditoria_0810/backtest_familia.py (08-10): MUROS/MAJORS/ZTP/ZEST de NQ, NDX y QQQ en 20 sesiones 09-11..10-08 con el juez
  de toque. **Esas sesiones incluyen las de PRUEBA de este pre-registro** (ver sección 6).
- El zero interpolado se pega al precio (sombra, no nivel). "La muestra son niveles, no minutos": hacen falta ~15 strikes distintos.
- La noche del 09-10 (sesión 10-09, el CASO) motivó el pedido: la 2.0 dibujó el strike 750 de QQQ en 31083,50 (razón 41,4447) y la
  4.1 en 31076,37 (razón 41,4352); los techos cayeron en 31083–31090. Es una observación (n = 1) y queda **fuera** de la decisión.

Lo que leí o corrí YO al armar esto (09-10, 01:05–01:50 ART):
- Leí los cinco informes de investigación (academia, proveedores, microestructura, foros/practicantes, matemática), el dataset
  (`datos/cargar.py`), el juez del operador (`criterio_operador/juez/juez_operador.py`), la receta de la 2.0
  (`receta_2_0/extraer_paridad_2_0.py`) y las definiciones de la 4.1 (`tres/extremos_rebote.py`, `tres/auditoria_0810/backtest_familia.py`,
  `atas/PythiaGexCuatro/_modulos/pantalla/SeriesPantalla.cs`).
- Corrí el arnés **solo** sobre: (a) rayas oráculo (en la mecha exacta de cada vela), (b) rayas al azar causales, ambas en sesiones de
  ENTRENAMIENTO, y (c) el juez de TOQUE viejo sobre E_rango60 y rayas fijas al azar en las sesiones de extremos_rebote.md (números ya
  publicados). **No corrí ninguna candidata con ningún juez.** No vi el juez del operador aplicado a ninguna raya de gamma.
- Posible contaminación ajena: mientras escribía esto, otros procesos del mismo workflow (`criterio_operador/r20` con r20_noches.py y
  r20_esta_noche.py, `criterio_operador/v41` con v41_02_series.py, y ei_medir.py con REF_2_0, redondos R25/R50/R100, oráculos, VWAP,
  pivotes) empezaron a medir la 2.0, la 4.1 y controles con el mismo juez, en sesiones que pueden incluir las de PRUEBA. Por eso las REFERENCIAS (2.0 y 4.1) **no cuentan como confirmación en PRUEBA**: su confirmación queda para ADELANTE (sec. 9).

## 2. Datos

- Dataset unificado `datos/` (construir.py; API cargar.py). Velas de 1 min de MNQ (contrato frente: U6 hasta la sesión 09-14, Z6
  desde la 09-15), sesión `dia` = [día−1 22:00, día 21:00) UTC. Libros: NQ (Rithmic), NDX, QQQ (CBOE, 15 min de retraso, fotos con
  hora de disponibilidad `generado`). Conversiones a MNQ: 'cuatro' (la 4.1), 'dos' (la 2.0), 'dos_log', 'cuatro_log' (ver cargar.py:30-50).
- Ventanas por hora de **Nueva York** de la APERTURA de la vela: **noche** 18:00–09:30 NY (22:00–13:30 UTC en horario de verano),
  **día** 09:30–16:00 NY (13:30–20:00 UTC). 16:00–18:00 NY no cuenta llegadas (sí se usa para seguir eventos).
- Marco principal: velas de **1 minuto** (el gráfico del operador es MNQZ6 1m). Sensibilidad: velas de 2 min (marco=2).
- Límites conocidos (datos/indice.json y el informe del dataset): la viva de NQ tiene huecos nocturnos de 4–8 h casi todas las
  noches; CBOE no baja entre ~04:00 y 09:00 UTC (la cadena congelada vale hasta las 13:30 UTC, regla C9); CBOE caído del 09-23 01:11
  al 09-24 17:42 UTC; 09-23..10-06 sin ticks (el precio de muestra de la conversión sale de la vela m2 interpolada).

## 3. El juez del operador (criterio principal)

Motor: `arnes/juez_operador_v1.py` = copia **byte a byte** de `criterio_operador/juez/juez_operador.py` tomada el 09-10 a la 01:30
ART, sha256 `a20a62688e888da22c36d9200bca5f63f16949ca90837545b37847a16188d5bf`. El arnés verifica ese hash al importar y se niega a
correr si la copia cambió; si el original cambia después, se informa pero se sigue usando la copia (un cambio de juez es un DESVÍO).

### 3.1 Raya vigente
La raya de la fila `t` vale para la vela de 1 min que **abre** en `t` y tiene que estar calculada solo con lo disponible en `t`
(velas cerradas antes de `t`, fotos con disponibilidad ≤ `t`). El arnés llama al juez con `desfase_min = 0` y `arrastre_min = 0`:
sin fila no hay raya (no se arrastran valores viejos). Rayas a ≤ 1 pt en el mismo minuto se funden en una. Identidad de una raya
entre minutos ("pista"): la misma si reaparece a ≤ 3 pts dentro de 30 min.

### 3.2 Llegada, aguante, falso rompimiento, rotura (modo TOLERANTE = el principal)
- **LLEGADA**: la vela `i` toca la banda [L−2, L+2] y la vela `i−1` no la tocaba (si estaba toda abajo, la raya es TECHO; toda arriba,
  PISO). Mientras una llegada no se resuelve, esa raya no registra otra.
- **"Más allá"** = por encima de un techo / por debajo de un piso. **"A favor"** = del lado del giro.
- **ROTA** (el precio se **acepta** del otro lado antes del giro): (a) un cierre de 1 min más allá de L+5 con cuerpo |c−o| ≥ 4, o
  (b) 3 cierres seguidos más allá de L, o (c) más de 10 min seguidos con cierres más allá de L (en velas de 1 min, (c) queda tapada por (b)).
  Las **mechas** más allá **no** rompen.
- **EXTREMO SOSTENIDO**: antes de romper, la mecha a favor llega a 15 pts de la raya (techo: l ≤ L−15; piso: h ≥ L+15).
- **FALSO ROMPIMIENTO**: sostenido con alguna mecha más allá de L+2 o algún cierre más allá de L antes del giro. Se cuenta aparte y
  **también** dentro de los sostenidos; se registra la profundidad máxima del pinchazo.
- **INDEFINIDA**: ni giro ni rotura en 60 min. Si los datos se cortan antes (hueco > 30 min o fin de datos): CENSURADA, fuera de los
  porcentajes (se informa aparte).
- Orden dentro de una vela (no hay ticks): alcista o→l→h→c, bajista o→h→l→c; en la vela de llegada lo "a favor" previo al toque no cuenta.

### 3.3 Precisión, recorrido, opuesta
- **PRECISIÓN** = punta real del tramo − raya, con signo (techo: máximo de las mechas desde la llegada hasta el giro, menos L; piso:
  L menos el mínimo). Incluye la mecha del pinchazo. **Exacto** = sostenido con |precisión| ≤ 2.
- **RECORRIDO** = máximo alejamiento a favor medido desde la raya, desde la llegada hasta la vela anterior a la re-rotura (mismas reglas
  del modo), tope 240 min desde la llegada.
- **OPUESTA** = la raya vigente del mismo conjunto del lado a favor, a ≥ 4 pts, la más cercana. Llega a la opuesta = el recorrido la
  alcanza a ±2 pts.

### 3.4 Cobertura de giros
Giros = zigzag de 20 pts (y de 40 pts, "cambio de tendencia") sobre mechas de 1 min, por tramo continuo. Un giro está **cubierto** si
en la vela de aproximación (la primera del tramo que llega a ±2 de la punta) ya había una raya vigente a ±2 de la punta.
**Filtro de sanidad del arnés** (declarado, `evaluar._pivotes_sanos`): se descarta el giro si entre su vela de aproximación y él hay una
punta más extrema que la suya. Corrige un borde del zigzag del juez al inicio de un tramo cuando la primera vela trae máximo y mínimo
juntos (en la validación: 1 de 1055 giros de 20 pts). Se aplica igual a lo real y a los placebos; el arnés informa cuántos descarta.

### 3.5 Sensibilidad del juez (se informa siempre, decide solo en E4)
- **ESTRICTO**: rompe cualquier cierre más allá de L+2 o cualquier mecha más allá de L+4.
- **MUY TOLERANTE**: rompe un cierre más allá de L+8 con cuerpo ≥ 6, o 5 cierres seguidos más allá (o > 10 min).

## 4. Métricas

Por candidata, ventana (noche / día) y modo:
- **M1 (principal) = % SOSTENIDOS** = sostenidos / (llegadas − censuradas), modo TOLERANTE.
- M2 = **recorrido por llegada** = suma de recorridos de los sostenidos / (llegadas − censuradas), en pts (une "cuántas aguantan" y
  "cuánto recorren"). También recorrido medio y mediano de los sostenidos.
- M3 = **cobertura de giros** de 20 y de 40 pts (sec. 3.4) y su lift contra el placebo.
- M4 = % exactos (sostenidos con |precisión| ≤ 2) sobre la base; % falsos rompimientos entre los sostenidos; % llega a la opuesta;
  precisión mediana (abs y con signo); profundidad mediana del pinchazo.
- Muestra: llegadas, base, censuradas, sesiones con llegadas, **niveles distintos** (strikes distintos si la candidata trae K; si no,
  niveles redondeados a 5 pts por sesión), sesiones ganadas.
- Estratos descriptivos (no deciden): franja horaria NY (noche, apertura 9:30–10:30, media 10:30–14:00, tarde 14:00–15:30, cierre
  15:30–16:00) y primera visita de la sesión contra las siguientes (la P4 de practicantes y la F de academia quedan cubiertas así).

### 4.1 Protocolo complementario (juez de toque del laboratorio, sin cambios)
Portado de tres/extremos_rebote.py:554-605 a `evaluar._toque_nb`: toque = la vela llega a ±2 viniendo de un lado (cierre previo a más
de 2) y la anterior no tocaba (identidad = bucket de 2,5 pts); **rebote** = 6 pts a favor en ≤ 3 velas antes de un cierre 2 pts del
otro lado; **extremo** = punta a ≤ 2 y cierre del lado de origen; **giro15** = 15 pts a favor en ≤ 15 velas antes de un cierre 2 pts
del otro lado. Se informa % rebote, % extremo, % extremo y rebote, % giro15, cada uno contra los mismos placebos. Es informativo:
**no decide**. Advertencia medida por microestructura (soportes_metodo/base_y_potencia.md): este juez mide rebote por mecha y traspaso
por cierre, y un paseo al azar ya "rebota" ~57–58 %; el 61–64 % de las rayas al azar es la vara.

### 4.2 Eco
Distancia mediana raya–precio (precio = cierre de la vela anterior) y **beta por pista** = pendiente de (raya(t+15) − raya(t)) sobre
(precio(t+15) − precio(t)) dentro de cada pista, agregada por ventana. Una raya que sigue al precio (beta cerca de 1) o que vive a menos
de 5 pts de él es sombra del precio, no nivel.

## 5. Placebos (los mismos para los tres jueces en cada juego)

- **(a) CORRIDOS**: la misma serie desplazada −31, −19, −11, +11, +19, +31 pts; se suman los seis.
- **(b) AZAR**: `n_azar` juegos; en cada uno, cada pista conserva su vida, su parpadeo y su forma, pero su distancia al precio al nacer
  se reemplaza por un |d| sorteado de la distribución de distancias de las pistas del propio conjunto, con signo al azar
  (juez_operador._offsets_azar, modo 'pool'). Misma cantidad de rayas y misma distribución de distancia al precio.
- **Controles como candidatas** (sec. 7): números redondos de NQ, la grilla de strikes de QQQ sin gamma, strikes al azar del mismo
  libro con el mismo ritmo de cambio que la 2.0, y rayas hechas con el precio (rango de 60 min, mechas previas).
- Las rayas hechas con el precio (C23, C24) tienen ventaja mecánica frente a un juez de extremos (microestructura: E_rango60 sale
  ganador en un paseo al azar). Para ellas el placebo de azar **no alcanza**: no pueden "ganar" (son controles).

## 6. Partición por días

| fase | sesiones | para qué |
|---|---|---|
| ENTRENAMIENTO | todas las ≤ 2026-09-30 donde exista el libro de la candidata | elegir finalistas (sec. 8) |
| PRUEBA (sellada) | 2026-10-01, 10-02, 10-05, 10-06, 10-07, 10-08 (6 sesiones) | confirmar finalistas (sec. 9) |
| CASO | 2026-10-09 | solo descriptivo (motivó el pedido); nunca decide |
| ADELANTE | sesiones nuevas desde el 2026-10-12, ≥ 10 | la confirmación que habilita tocar un default |

Sesiones de ENTRENAMIENTO por libro (datos/indice.json): QQQ 09-11 (parcial), 09-14 … 09-30 (11 noches y 11 días completos); NDX
08-19 … 09-30 (08-19..09-04 = reconstrucción Databento, solo rueda); NQ 09-08 … 09-30 (casi solo días: 3 noches completas);
familia completa (C12) días 09-14, 09-16, 09-17, 09-18, 09-21, 09-22 y noche 09-22; sin libro (C19–C21, C23, C24) 08-17 … 09-30.
En PRUEBA QQQ y NDX están completos noche y día; NQ solo día 10-01, 10-06, 10-07, 10-08 y noche 10-07, 10-08.

**Son pocos días** (≈ 11–14 de entrenamiento y 6 de prueba por libro). Por eso: (1) el arnés **sella** PRUEBA y el CASO (no se
pueden evaluar sin `abrir_prueba=True` y un `finalistas.json` congelado; cada apertura queda en `arnes/aperturas_prueba.log` con su
hash); (2) en ENTRENAMIENTO se informa además la validación cruzada dejando una sesión afuera del procedimiento "elegir la mejor"
(`evaluar.loo_parametro` sobre el dict de candidatas), como estimación honesta de cuánto se infla la mejor; (3) ninguna candidata tiene
parámetros libres (todo está fijado en la sec. 7); (4) la confirmación que decide un cambio es ADELANTE.

**Contaminación declarada de PRUEBA**: esas 6 sesiones ya se usaron con OTRO juez en backtest_familia.py (MUROS/MAJORS/ZTP/ZEST de los
tres libros) y en extremos_rebote.py; y quizás con este juez por los agentes r20/v41 (sec. 1). PRUEBA es ciega solo para las
candidatas nuevas con este juez; para las referencias, no.

## 7. Candidatas (24, en 4 grupos para probadores en paralelo)

### 7.0 Cuenta común (vale para todas las de gamma salvo que se diga otra cosa)
- **Grilla**: cada minuto `t` de la sesión. **F(t)** = cierre de la vela de 1 min que abrió en t−1 min (`evaluar.minutos_y_precio`; si
  falta, el último cierre de los 5 min previos; si no hay, no hay raya).
- **Foto**: `D.foto_vigente(libro, dia, t)` (CBOE: la última con `generado` ≤ t y vigente según C9 = 25 min, o cadena congelada hasta
  las 13:30 UTC; NQ: la última con `ts` ≤ t de menos de 300 s). Filas: `D.filas(libro, dia, foto)` con los `dias` de esa foto.
- **Envejecimiento y horizonte Hoy** (GammaHoyNucleo, receta_2_0/extraer_paridad_2_0.py:67-70 y 107-120): env = min(2, días desde
  `generado` (NQ: `ts`) hasta t); dias_env = dias − env; entran las filas con 0 ≤ dias_env ≤ max(1, (mínimo dias_env ≥ 0) + 0,01);
  τ = max(dias_env, 1/1440)/365 años. Lo vencido queda afuera.
- **Gamma**: CBOE (NDX, QQQ) Black-Scholes con r = 0,0375, q = 0, sin descuento en la gamma (receta_2_0:47-55); NQ Black-76 con el
  futuro (tres/nucleo.gamma76).
- **GEX por strike** (USD por 1 %): GC_w(K) = Σ_venc Γc·w_c·M·S²·0,01; GP_w(K) = −Σ_venc Γp·w_p·M·S²·0,01; GEX_w = GC_w + GP_w;
  w = vol (volumen del día de la foto) u oi; M = 100 (CBOE) o 20 (NQ).
- **Conversión** (`D.conversion(libro, dia, t, método)`): QQQ Fut(K) = K·ρ, S = F/ρ; NDX Fut(K) = K + B, S = F − B; NQ Fut(K) =
  K + corr, S = F − corr. Sin conversión válida no hay raya.
- **Radio** R = min(0,02·F, 100) pts sobre |Fut(K) − F|.
- **Salida** (formato del arnés): una fila por raya y por minuto con t, nivel = Fut(K), etiqueta (la indicada), K, libro, dia.
  Archivo `probadores/grupo<g>/<ID>/niveles_<FASE>.parquet`. Todas se evalúan con
  `evaluar(niveles, dias, nombre=ID, n_azar=500, n_boot=2000)` en ENTRENAMIENTO y con `n_azar=2000` en PRUEBA.
- **Conversión 'deriva'** (solo C04): de 09:35 a 15:59 NY es la 'cuatro'. Fuera de eso ρ(t) = ρ_D·exp(−(r−q)·(t − t_D)/365 días), con
  ρ_D = mediana robusta (3 MAD, piso 0,0002·mediana, `cargar.robusta`) de **todas** las muestras vivas `muestra` de `D.fotos('QQQ', ·)`
  con `vivo` = True y `t_spot` entre 09:35 y 15:59 NY de la última rueda D completa anterior a t (≥ 20 muestras, mismo contrato que
  el gráfico en t), t_D = 16:00 NY de D, y r − q = 0,0375 − 0,0060 = 0,0315 por año (SUPUESTO: tasa de la 2.0 y dividendo de QQQ ≈ 0,6 %,
  no medido). Es "la razón de la rueda, sincronizada, corrida por el costo de mantener el futuro" (Golez-Jackwerth 2012 traducen
  strikes entre mercados con mediana de pares sincrónicos de la rueda; proveedores: MenthorQ/GEXbot/SpotGamma usan precios
  sincronizados y ninguno corrige el carry).

### Grupo 1 — la 2.0 y la conversión (libros QQQ y NDX)
- **C01 DOS_QQQ** (REFERENCIA: la 2.0 tal como dibuja su libro primario). QQQ, Hoy, w = vol. Las 2 de mayor |GEX_vol| con
  |Fut − F| ≤ R, sin lado, strike exacto (empate: el de menor K, orden estable); si ninguna tiene |GEX_vol| > 0, las 2 de mayor
  |GEX_oi| (DominantesDeNoche = Volumen con caída a OI). Conversión **'dos'** (también para S). Etiquetas D1 (mayor), D2. Es
  `receta_2_0/extraer_paridad_2_0.calcular(...)['doms']` (líneas 96-158) con escala = ρ_dos. Sensibilidad informativa (fuera de la
  familia): 'dos_log' en los minutos en que existe.
- **C02 DOS_NDX** (REFERENCIA: capa NDX de la 2.0). Igual que C01 sobre NDX con base **'dos'**.
- **C03 DOS_QQQ_C41** (REFERENCIA: selección de la 2.0 con la conversión de la 4.1). Igual que C01 con conversión **'cuatro'**.
- **C04 DOS_QQQ_DERIVA**. Igual que C01 con conversión **'deriva'** (7.0).
- **C05 STRIKE_AZAR_QQQ** (control de selección). En cada minuto en que cambia el conjunto de strikes de C03 (y en el primero de la
  sesión) se sortean 2 strikes distintos, sin reemplazo, entre los K de QQQ con |K·ρ_cuatro − F| ≤ R y vol_c + vol_p > 0 en el
  horizonte Hoy; se mantienen hasta el próximo cambio de C03. Generador `np.random.default_rng([20261009, int(dia sin guiones)])`.
  Conversión 'cuatro'. Etiquetas A1, A2. Misma cantidad, mismo ritmo de cambio y misma grilla que C03, sin gamma.
- **C06 REGIMEN_POS_QQQ** (academia D, proveedores C5, practicantes P7). Las rayas de C03, dibujadas solo si
  G(t) = Σ_{K: |K·ρ − F| ≤ 0,02·F} (GEX_oi(K) + GEX_vol(K)) > 0 (QQQ, Hoy, 'cuatro'). Etiquetas D1, D2.

### Grupo 2 — las series por defecto de la 4.1 (REFERENCIAS)
Por defecto la 4.1 enciende MAJORS_QQQ_oi, MUROS_NQ_oi, MUROS_NDX_vol, MUROS_QQQ_oi, FAM_MUROS_oi, ZEST_QQQ_vol, TRES_NDX y
T_MUROS_oi (atas/PythiaGexCuatro/_modulos/pantalla/SeriesPantalla.cs:25-53, el último campo de cada fila es el default). TRES_NDX (lo que dibujó la 3.0) y T_MUROS_oi (TQQQ:
su conversión no está armada en el dataset) quedan afuera. Definiciones de tres/extremos_rebote.py:222-241 y
tres/auditoria_0810/backtest_familia.py (minutos_cboe 510-556, minutos_nq 467-507, fusion 638-648, zest 595-597), conversión 'cuatro'.
- **C07 C41_MAJORS_QQQ_oi**. QQQ, Hoy, OI. D1 = argmax GEX_oi > 0 en R; D2 = argmin GEX_oi < 0 en R.
- **C08 C41_MUROS_QQQ_oi**. QQQ, Hoy, OI. D1 = argmax GC_oi (> 0) en R (muro de calls); D2 = argmin GP_oi (< 0) en R (muro de puts).
  (Es la serie que la noche del CASO puso el 750 en ~31076.)
- **C09 C41_MUROS_NDX_vol**. Igual que C08 sobre NDX con w = vol, base 'cuatro'.
- **C10 C41_ZEST_QQQ_vol**. Zero estándar: Σ de todo el libro Hoy de (Γc·vol_c − Γp·vol_p)·x² evaluada con IV y τ fijos en la grilla
  x = c0 + 0,05·k USD, c0 = S redondeado a 1 USD, |x − c0| ≤ 7,5 USD; el cambio de signo más cercano a S, interpolado
  (backtest_familia.py:394-420 con paso 0,05, semi 7,5, centro 1,0); se dibuja si |Z·ρ − F| ≤ 300. Etiqueta Z.
- **C11 C41_MUROS_NQ_oi**. Libro NQ (Rithmic), Black-76, Hoy, OI, Fut(K) = K + corr. **OI con fecha** (backtest_familia.py:438-466,
  corrección C2): no hay rayas por OI desde las 22:00 UTC hasta el salto de OI de Rithmic de esa noche (primer par de fotos con ≥ 50 %
  de los contratos comunes con OI distinto entre 22:00 y 03:30 UTC); lunes y posferiados no se suprime; sin salto visible, se suprime
  hasta las 02:00 UTC. D1/D2 como C08.
- **C12 C41_FAM_MUROS_oi**. Solo minutos con los tres libros (NQ, NDX, QQQ) vigentes y OI válido (C11). Cada libro limitado a
  |Fut − F| ≤ 3 %; GC_oi y GP_oi de cada libro × (M_libro/100) (NQ 0,2; NDX y QQQ 1); Fut redondeado a una grilla de 5 pts; se suman
  por punto de grilla (backtest_familia.fusion). D1 = argmax de la suma de GC_oi (> 0) en R; D2 = argmin de la suma de GP_oi (< 0) en R.

### Grupo 3 — signo, flujo, proveedores, confluencia, movimiento esperado
- **C13 IMAN_QQQ_vol** (academia A: Avellaneda-Lipkin 2003, Golez-Jackwerth 2012). QQQ, Hoy, vol, 'cuatro'. Las 2 de mayor
  GEX_vol **> 0** en R; se dibujan solo si N(t) = Σ_{K: |K·ρ − F| ≤ 0,01·F} GEX_vol(K) > 0. Etiquetas I1, I2.
- **C14 REPEL_QQQ_vol** (el espejo). Las 2 de GEX_vol **más negativo** en R (sin condición). Etiquetas N1, N2.
  Predicción de la literatura: imán > su placebo y > repelente; repelente con más roturas y pinchazos más hondos. Si empatan, el signo no rescata nada.
- **C15 FLUJO_NQ** (proveedores C3: Unusual Whales source=vol, TRACE, GEXbot; GexFlujo de GammaHoyNucleo.cs:221 según el informe de
  proveedores, no lo leí). Libro NQ, Hoy, Black-76, M = 20. G_d(K) = −Σ_contratos Γ·(vol_compra − vol_venta)·20·S²·0,01 (el que cubre
  es la contraparte del agresor). CW+ = argmax G_d con K > F, G_d > 0, en R; PW+ = argmax G_d con K ≤ F, G_d > 0, en R.
- **C16 CONFLUENCIA_QQQ_NDX** (proveedores C8, practicantes P10). a ∈ rayas de C03; b ∈ las 2 de mayor |GEX_vol| de NDX en R con base
  'cuatro' (la selección de C02 con la conversión de la 4.1). Pares con |a − b| ≤ 5 pts → raya en (a+b)/2; a lo sumo 2 (los pares más
  cercanos primero, sin repetir a ni b). Etiquetas F1, F2.
- **C17 BANDA_EM** (proveedores C2: SpotGamma implied move, MenthorQ 1D Min/Max; practicantes P9; solo **día**). En el primer minuto
  t0 ≥ 10:00 NY con foto de QQQ vigente que tenga 0DTE: K_atm = strike 0DTE más cercano a S; iv = promedio de iv_call e iv_put de K_atm
  (si una falta, la otra); τc = (16:00 NY − t0)/525 600 min; σ = iv·√τc. Rayas EM+ = F(t0)·(1+σ) y EM− = F(t0)·(1−σ), fijas de t0 a
  las 15:59 NY. Apunta al TAMAÑO del día (lo único ya medido como anticipable), no a dónde rebota.
- **C18 CW_PW_OI_QQQ** (proveedores C1, SpotGamma Call/Put Wall). QQQ, **todos** los vencimientos con 0 ≤ dias_env ≤ 31 presentes en la
  banda guardada (±6 % del spot), OI. CW = argmax_K Σ_venc Γc·OI_c·100·S²·0,01; PW = argmax_K Σ_venc Γp·OI_p·100·S²·0,01 (sin netear,
  sin radio). Recalculadas cada minuto. Conversión 'cuatro'. Etiquetas CW, PW.

### Grupo 4 — controles sin gamma (grilla, redondez, precio)
- **C19 RED25 / C20 RED50 / C21 RED100** (control de redondez; Osler 2000/2003). Múltiplo de G (25, 50, 100) más cercano estrictamente
  por encima de F y múltiplo más cercano ≤ F. Etiquetas R+, R−.
- **C22 GRILLA_QQQ_C41** (practicantes P1, "escaneo de fase"). S = F/ρ_cuatro; K+ = menor strike de QQQ presente en las filas de la
  foto (cualquier vencimiento Hoy) con K > S; K− = mayor con K ≤ S; rayas K·ρ_cuatro. Etiquetas G+, G−. Sin gamma: ¿las mechas se
  juntan sobre la grilla de QQQ pasada a NQ?
- **C23 E_RANGO60** (control de precio, extremos_rebote.py:445-458 llevado a 1 min). Máximo de h y mínimo de l de las velas de 1 min
  con apertura en [t−60, t−1] (≥ 5 velas). Etiquetas D1, D2. Tiene ventaja mecánica (sec. 5): no puede ganar.
- **C24 MECHA_PREVIA** (academia E: Garzarelli et al. 2014; Osler 2000). Zigzag de 20 pts **causal** sobre velas de 1 min desde el
  inicio de la sesión: misma máquina de estados que juez_operador._pivotes, pero un giro existe recién desde la vela en que el precio
  recorrió los 20 pts en contra (≤ t−1). Rayas: el giro de máximo confirmado más cercano por encima de F y el de mínimo más cercano por
  debajo, entre los de los últimos 240 min. Etiquetas D1, D2. Control de precio: no puede ganar.

Fuera de la familia (no se prueban ahora, por potencia o por datos): pin al cierre de QQQ contra NDX (academia C, proveedores C6,
practicantes P3: con ~20 ruedas solo se vería la dirección), β de fuerza por strike (academia B: con selección por |GEX| el ranking es
el mismo, solo serviría de filtro), pared de liquidez repreciada L(p) y recorrido del impulso agotado (matemática F1, F5: piden
supuestos de liquidez no medidos), anillo σ√τ (F3), TQQQ (sin conversión de la 4.1 en el dataset), pared del DOM (sin historia).

## 8. Regla de ENTRENAMIENTO → PRUEBA (fijada antes de correr nada)

Por ventana (noche y día por separado), con el modo TOLERANTE:
1. Elegibles: base ≥ 30, niveles distintos ≥ 10, ventaja de M1 contra el azar ≥ +5 pp, p_azar < 0,05 (sin ajustar) y límite inferior
   del IC 90 % bootstrap (por sesión) de M1 − corridos > 0. C05, C19–C24 son controles: se informan pero **no** pueden ser finalistas.
2. Orden: mayor límite inferior del IC 90 % contra corridos; empate, menor p_azar. **Hasta 3 finalistas por ventana.**
3. Siempre van a PRUEBA (en la familia de Holm, como referencias): C01, C02, C03 y C07–C12.
4. Antes de abrir PRUEBA se escribe `arnes/finalistas.json` (finalistas por ventana, referencias, hora, sha256 de los resultados de
   entrenamiento) y se calcula su sha256; recién entonces `evaluar(..., abrir_prueba=True, sello='arnes/finalistas.json')`.
5. Se informa además el LOO por sesión del procedimiento "elegir la mejor" (sec. 6) y, para cada finalista, la ventaja en cada mitad
   del entrenamiento (09-11..09-22 contra 09-23..09-30): si cambia de signo, se dice.
Si ninguna candidata es elegible en una ventana, se escribe "ninguna" y en esa ventana PRUEBA solo mide las referencias.

## 9. Criterio de éxito (PRUEBA) y decisión

Familia de Holm en PRUEBA = todas las candidatas evaluadas en PRUEBA (finalistas + referencias) × 2 ventanas; p = p_azar de M1
(TOLERANTE) con `n_azar = 2000`. Una candidata **GANA** en una ventana si cumple las seis (`evaluar.criterios_exito`):
- **E1 muestra**: base ≥ 30, niveles distintos ≥ 15 y ≥ 4 sesiones con llegadas.
- **E2 tamaño**: M1 − corridos ≥ +10 pp **y** M1 − media del azar ≥ +10 pp.
- **E3 significancia**: p_azar ajustado por Holm < 0,05 **y** límite inferior del IC 95 % bootstrap por sesión de (M1 − corridos) > 0.
- **E4 consistencia**: gana (M1 > corridos de esa sesión) en más de la mitad de las sesiones con ≥ 3 llegadas, y la ventaja contra
  corridos es > 0 también en ESTRICTO y en MUY TOLERANTE.
- **E5 no empeora lo demás**: M2 (recorrido por llegada) ≥ el de los corridos y lift de cobertura de giros de 20 pts contra el azar ≥ 1,0.
- **E6 no es eco**: distancia mediana al precio ≥ 5 pts y |beta por pista| ≤ 0,5.

Decisión: una candidata **nueva** que gana en PRUEBA pasa a ADELANTE. Una REFERENCIA (2.0/4.1) que "gana" en PRUEBA no cuenta como
confirmada (contaminación, sec. 1 y 6) y también pasa a ADELANTE. **ADELANTE**: ≥ 10 sesiones nuevas (desde el 10-12), una sola prueba
por candidata sobreviviente: M1 − corridos ≥ +10 pp, M1 − azar ≥ +10 pp y p_azar < 0,05 (con tantas pruebas como sobrevivientes,
Holm). Solo con eso se propone tocar lo que se dibuja, y siempre: en el clon vigente (nunca en producción), con aviso al operador en el
mismo mensaje y captura antes/después (CLAUDE.md). Si nada gana, se dice tal cual y se recomienda pocas rayas, sin presentarlas como
ventaja. Ninguna regla de acá habilita hablar de rentabilidad.

## 10. Potencia (honesta)

Vara medida en la validación (sec. 13, rayas al azar en ENTRENAMIENTO): % sostenidos de rayas sin información ≈ 45 % de noche y
≈ 49 % de día. Con p0 ≈ 0,47, la mínima ventaja detectable en PRUEBA (80 % de potencia, Holm con hasta 24 pruebas: α ≈ 0,002 para
la primera) es ≈ 3,7·√(p0·q0/N): **≈ 24 pp con 60 llegadas, ≈ 15 pp con 150, ≈ 10 pp con 340**. La literatura encuentra efectos de
4–6 pp en soportes y resistencias (Osler 2000: 60,8 % contra 56,2 % con miles de toques). **Lo esperable es que nada pase PRUEBA aunque
exista un efecto real chico**; un "no gana" con estas muestras significa "no hay un efecto grande", no "no hay efecto".

## 11. Qué se informa siempre (gane o no)

Para cada candidata y ventana: M1 a M4 real, corridos (y cada desplazamiento), azar (media, p5, p95, p), ventajas con IC, muestra,
estratos, eco, y el protocolo complementario. También las que pierden. Para el CASO 10-09, descriptivo: la 2.0 contra la 4.1 en la noche
que motivó el pedido, con los techos reales (31083–31090, y después 31092 y 31098,75) y la aclaración de que es n = 1.

## 12. El arnés

`arnes/evaluar.py` (versión 1.0, sha256 `1cd6083989f4ac7c870b96801a128cb8bae25f5c98efa023537d676cf44d991c`). API: `evaluar(niveles_por_minuto, dias, nombre, ...)`;
`minutos_y_precio(dia)`; `chequeo_futuro(niveles, dias)` (detector barato de mirar adelante: % de rayas pegadas a la mecha de su propia
vela contra la siguiente); `holm(p)`; `criterios_exito(res, ventana, p_holm)`; `seleccionar_finalistas({nombre: res}, ventana)`;
`loo_parametro({nombre: res}, ventana)`. CLI: `python -I evaluar.py validar`. Importa `datos/cargar.py` y la copia congelada del juez.

## 13. Validación del arnés (corrida el 09-10, antes de congelar; detalle en arnes/validacion_arnes.md y .json)

**(a) Oráculo** (rayas en el máximo y el mínimo exactos de cada vela, vigentes en esa misma vela; 13 sesiones de ENTRENAMIENTO 09-14..09-30): cobertura de giros de 20 pts **100.0 %** de noche (1054 giros) y **100.0 %** de día (963); de 40 pts 100.0 % y 100.0 %. Solo con la raya en el máximo: 49.9 % y 50.1 % (cubre los giros de máximo, la mitad). Antes del filtro de sanidad daba 99,9 % de noche (1 giro mal marcado por el borde del zigzag, sec. 3.4).

**(b) Rayas al azar causales, noche** (20 conjuntos de 2 rayas a 5–60 pts del precio, re-sorteadas cada 60 min; 150 juegos de placebo cada uno): % sostenidos medio **44.9 %** (498 llegadas por conjunto); ventaja contra el azar -0.68 ± 1.81 pp (media ± sd entre conjuntos); contra los corridos -0.63 ± 1.85 pp; p_azar < 0,05 en 0 % de los conjuntos; el IC 95 % bootstrap contra corridos cubre el 0 en **95 %** (debería ser ~95 %); cumplen E1–E6 con p sin ajustar: 0 de 20; elegibles por la regla de selección: 0. Juez de toque: rebote 59.7 % real contra 60.3 % del azar.

**(b) Rayas al azar causales, día** (20 conjuntos de 2 rayas a 5–60 pts del precio, re-sorteadas cada 60 min; 150 juegos de placebo cada uno): % sostenidos medio **48.5 %** (324 llegadas por conjunto); ventaja contra el azar -0.87 ± 3.61 pp (media ± sd entre conjuntos); contra los corridos -0.44 ± 4.49 pp; p_azar < 0,05 en 5 % de los conjuntos; el IC 95 % bootstrap contra corridos cubre el 0 en **75 %** (debería ser ~95 %); cumplen E1–E6 con p sin ajustar: 0 de 20; elegibles por la regla de selección: 1. Juez de toque: rebote 60.4 % real contra 61.1 % del azar.

Lectura de (b): el placebo de azar y su p están calibrados (sin información la ventaja es ~0 y p < 0,05 sale ~5 %% de las veces o menos). El IC bootstrap por sesión es **optimista** con 13 sesiones (cubre el 0 menos que el 95 %% nominal): por eso no decide solo; E3 exige además el p del azar ajustado por Holm.

**(c) Reproducción de números ya medidos** (juez de toque, velas m2, sesiones de tres/resultados/extremos_rebote.md): E_rango60 noche 1716 toques / 61.5 % rebote / 47.7 % extremo (publicado 1667 / 61,3 / 47,5; sin la noche 10-08, que la corrida vieja vio incompleta: 1626 / 61.3 / 47.2); E_rango60 día 643 / 56.9 / 32.8 (publicado 644 / 56,7 / 32,8); rayas fijas al azar noche 16982 / 63.1 / 49.9 (publicado 16962 / 63,5 / 50,4) y día 12433 / 62.6 / 37.4 (publicado 12279 / 61,6 / 36,6; las rayas al azar se sortean de nuevo, la diferencia es ruido de sorteo). Censo de giros de 20 pts: 87.3 por sesión de noche y 70.4 de día (soportes_metodo/censo_mechas.md: 90,2 y 80,2 con otro algoritmo y otras sesiones); de 40 pts 22.2 de noche (23,3 publicado).

Ninguna de estas corridas usó una candidata. Las rayas al azar y los oráculos se corrieron solo en ENTRENAMIENTO; (c) usa sesiones de PRUEBA pero solo con el juez de toque viejo sobre controles cuyos números ya estaban publicados.

## 14. Fuentes

Académicas (enlaces en academica/fuentes_academicas.json; lectura según ese archivo): Avellaneda & Lipkin (2003), Quantitative Finance
3(6), https://math.nyu.edu/inmemoriam/avellaneda/PinningSlides.pdf · Golez & Jackwerth (2012), JFE 106(3),
https://kops.uni-konstanz.de/entities/publication/0c68d50d-23f5-4771-bdbf-c345290a3452 · Ni, Pearson & Poteshman (2005), JFE 78(1),
https://ideas.repec.org/a/eee/jfinec/v78y2005i1p49-87.html · Baltussen, Da, Lammers & Martens (2021), JFE 142,
https://academicweb.nd.edu/~zda/intramom.pdf · Barbon, Beckmeyer, Buraschi & Moerke (2021), SBF HSG WP 2021/14,
https://www.alexandria.unisg.ch/server/api/core/bitstreams/5a99db31-0d37-4f86-9502-8cb0f3bff4fe/content · Dim, Eraker & Vilkov (2024),
SSRN 4692190, https://ssrn.com/abstract=4692190 · Amaya, Garcia-Ares, Pearson & Vasquez (2025),
https://cdn.cboe.com/resources/education/research_publications/gammasqueezes.pdf · Xu (Cboe, 2023),
https://www.cboe.com/insights/posts/volatility-insights-evaluating-the-market-impact-of-spx-0-dte-options · Osler (2000), FRBNY EPR 6(2),
https://www.newyorkfed.org/medialibrary/media/research/epr/00v06n2/0007osle.pdf · Garzarelli et al. (2014), Scientific Reports 4,
https://arxiv.org/abs/1110.5197 · Baule, Schlie & Zhou (2025), https://www.fernuni-hagen.de/igas/docs/cesa_wp_14.pdf · Elms (2026,
leído solo por un resumen de terceros), https://harbourfrontquant.substack.com/p/from-pinning-to-amplification-evidence.
Proveedores y practicantes (SpotGamma, MenthorQ, GEXbot, Unusual Whales, TRACE de Cboe, FlashAlpha, Skylit): sin enlaces en los
informes de los agentes; definiciones en metodologias_proveedores/formulas_proveedores.json y practicantes/formulas_practicantes.json.
Ninguno publica una prueba contra placebo de sus niveles. Código del proyecto citado con archivo:línea en cada candidata.

## 15. DESVÍOS (cambios después del congelamiento)

(ninguno todavía)
