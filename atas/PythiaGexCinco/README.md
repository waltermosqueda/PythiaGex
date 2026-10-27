# PythiaGex 5.0 — Gamma ES (ES/MES, SPX, SPY) — 5.0.0

Indicador de ATAS para gráficos de **MES/ES**. Es el clon de la 4.1 (la de NQ, `atas/PythiaGexCuatro`) con los libros del S&P: calcula
**adentro** las 35 series de la familia (muros, majors, zero estándar, cruces, top-N y carga por libro; la familia sumada y la confluencia;
lo que dibuja la 3.0 por libro; las réplicas de la 2.0 y de la clásica en ES) y las dibuja sobre las velas. Un solo indicador, autocontenido:
no depende de otro indicador ni de ningún programa externo (regla del operador).

**Describe el régimen (dónde está la cobertura, de qué lado se amortigua o se acelera el movimiento); no anticipa la dirección. Nada de esto
está medido en ES** (ver "Sin medir" abajo).

- Carpeta `atas/PythiaGexCinco`, DLL `PythiaGexCinco.dll`, tipo `PythiaGexCinco.GammaCinco`, nombre "PythiaGex 5.0 - Gamma ES", categoría
  "PythiaGex 5.0", versión 5.0.0.0.
- Datos en `%APPDATA%\ATAS\PythiaGex5`, logs `%APPDATA%\ATAS\pythiagex5-*.log`. Convive con la 4.1 en el mismo ATAS (otro ensamblado,
  otros archivos y candados).
- En un gráfico que no es ES/MES no dibuja: "PythiaGex 5.0 es para ES/MES: este gráfico es X, no dibujo".

## De dónde salen los datos

- **Rithmic (dentro de ATAS):** la cinta del MES y el libro de opciones sobre el futuro ES de CME (50 USD por punto), por la API de opciones
  de ATAS (el motor 3.0 que corre adentro; los 2 vencimientos más cercanos).
- **CBOE (lo baja el propio indicador):** las cadenas de **SPX** (`_SPX`: series SPXW diarias y SPX mensuales; el archivo se llama "ES", como
  en `cboe_local`) y de **SPY**, que Rithmic no transmite (son OPRA, no CME). Pública, 15 min de atraso, comprimida, de a un ticker, sin
  ráfagas: cada 75 s en la rueda y cada 300 s fuera, con tope de 1250 KB/s. Una foto de SPX pesa ~1,7 MB comprimida (medido 27-10).
- **Conversión a precio de ES** (todo nivel de SPX/SPY se dibuja convertido y la pestaña dice la conversión y su edad):
  - SPX → ES sumando la **base ES − SPX sincronizada** (medida contra el precio del MES 15 min antes; medido 35-38 pts en Z6, 22..26-10);
  - SPY → ES multiplicando por la **razón ES/SPY sincronizada** (medido 10,08-10,13; un strike de SPY = 1 USD ≈ 10 pts de ES).
- Única condición externa: internet para CBOE. Sin internet, ES sigue y la pestaña avisa en naranja qué falta.

## Instalar

Desde la carpeta `PythiaGex`:

1. Compilar: `powershell -ExecutionPolicy Bypass -File herramientas/build_serial.ps1 -Dir atas/PythiaGexCinco -Salida bin/Release`.
2. Una sola vez, antes del primer arranque: `python -I herramientas/sembrar_5_0.py` (`--ver` solo lista). Copia, sin pisar nada, los
   últimos 8 días de cadenas de SPX y SPY de `%APPDATA%\ATAS\PythiaGex\cboe-local` a `PythiaGex5\cboe`, para que la base de SPX de noche y la
   razón de SPY tengan historia desde el primer minuto. Después la 5.0 vive de lo suyo.
3. `powershell -ExecutionPolicy Bypass -File herramientas/instalar_5_0.ps1`: cierra ATAS guardando el workspace, copia SOLO
   `PythiaGexCinco.dll` a `%APPDATA%\ATAS\Indicators` (no toca la 4.1, la 3.0, la 2.0 ni la clásica), espera 30 s (Lucid suelta la sesión),
   relanza y reconecta. Con `-AntesDeLanzar herramientas/ws_mes_cuatro_a_cinco_2710.py` cambia, con ATAS cerrado, la 4.1 del gráfico
   "MES 1m Chart" (que ahí no dibuja) por la 5.0 con sus ajustes de fábrica (respalda el `.ws` antes en `PythiaGex/respaldos_ws`).

## Qué dibuja de fábrica

La 5.0 es un tipo nuevo en ATAS: ningún workspace guarda sus ajustes, así que en el primer gráfico mandan estos defaults
(grupo "0. PRENDER / APAGAR (todo lo que se dibuja)"):

- **prendidas (6):** `ES muros OI`, `SPX muros vol`, `3.0 SPX rayas`, `2.0 SPX D1-D2`, `Clas SPX 0Γ` y `Clas SPX D1-3`;
- todo lo demás apagado; en particular `ES top-N vol` y `ES carga OI` (en la 4.1 venían prendidas) y `2.0 SPY D1-D2` (la 2.0 no dibuja SPY en
  un gráfico de ES: es su fórmula aplicada a SPY, sin original que replicar);
- **doble eje** a la izquierda en **SPX** (Ninguno / SPX / SPY; cotas: base ES − SPX en (0, 150] y razón ES/SPY entre 9,95 y 10,25; fuera de
  eso no se dibuja y la pestaña dice por qué);
- estela (rayitas por vela) y etiquetas a la derecha con monto y cambio ▲▼; los niveles a ≤ 0,25 pt (un tick) van en una sola etiqueta;
- la pestaña "PythiaGex 5.0 ▸" con las fuentes, sus edades, las conversiones y el detalle de cada nivel con su strike;
- lo propio del motor 3.0 (dominantes, túnel, zero, capas, recuadro, cabecera, fórmulas) calcula pero no dibuja (llaves `Tres41*` apagadas).

Radios de la selección (escala, no validación): ES 25 pts, SPX y SPY 20, la familia 20; confluencia a ≤ 3 pts y baldes de la familia de 5
(el paso de strike de ES y SPX); banda de SPY ±5 (medio strike). Las réplicas de la 2.0 y de la clásica usan lo que esas hacen en ES: un
solo libro SPX, radio min(2 %, 100 pts) = 100 y la cadena 960 s atrás.

## Sin medir

- **Ninguna serie está medida en ES.** Cada descripción y cada estado empieza con "SIN MEDIR EN ES"; lo que la 4.1 midió en NQ va entre
  paréntesis como dato de otro producto.
- Los radios, la grilla del zero (0,5 pts, a ≤ 75) y las tolerancias de pantalla solo fijan la ESCALA (medida: precio NQ/ES 3,86, rangos
  6,1-6,6, cobertura del 0DTE; `_diseno/MEDICIONES_ES.md`); no dicen que el nivel sirva.
- La réplica de la 2.0 está **sin verificar contra la 2.0 corriendo en ES** (de la 2.0 en ES hay 5 salidas, del 05-10). La de la clásica se
  comparó **un solo día** contra sus 131 salidas en ES del 05-10 (`_diseno/P2_SPX.md`): de día la misma base y el mismo zero (mediana 0,00);
  esa noche +1,27 pts (la clásica en su base de la rueda, la réplica en la medida de hace 185 min). Eso es fidelidad de la réplica, no que el
  nivel sirva.
- Las capas SPX/SPY del motor 3.0 son aproximadas (sin arnés propio en ES).
- La carga: con el umbral de 50 contratos, en ES marca el radio entero (medido 27-10: los strikes cercanos tienen > 500).
- SPY el día del ex-dividendo (el próximo, por analogía, el 18-12-2026, el mismo día que vence ESZ6) y la base de SPX de noche (con el índice
  congelado) son los puntos débiles conocidos de la conversión.

## Documentos

`_diseno/DISENO_5_0.md` (qué es y cómo se construyó), `_diseno/MEDICIONES_ES.md` + `constantes_medidas.json` (las mediciones de escala),
`_diseno/INVENTARIO_4_1.md` (todo lo específico de NQ en la 4.1), `_diseno/QUITAR_TQQQ.md`, `_diseno/P5_PANTALLA.md` (catálogo, pantalla,
casillas y defaults). `CHANGELOG.md` dice qué cambió contra la 4.1.
