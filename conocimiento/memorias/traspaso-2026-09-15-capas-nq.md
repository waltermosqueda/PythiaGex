---
name: traspaso-2026-09-15-capas-nq
description: "Pedido del 15-09: en un grafico de NQ/MNQ ver a la vez barras y dominantes de QQQ, TQQQ, NDX y Rithmic, cada una de un color y con llave. HECHO por Fable (bloques 1-3, commits 1c09a4d3/a4a191b5/67d023ab): GammaHoyCapas.cs, DLL 1.10 instalado 13:55 con todas las capas apagadas, AUDIT de QQQ identico antes/despues. Despues, con \"hacelo vos todo\": push hecho, las 4 capas prendidas en el MNQ de QQQ y VISTAS en pantalla, capas_nq.py coincide (K 704/705); TQQQ se archiva desde las 17:05 UTC pero flaco (7 strikes, sin 0DTE los martes): ensanchar linea_flaca. Bloque 5 (14:50): SPX, SPY y ES (viva local del MES) llevados a NQ por BETA medida en la rueda (apal = 1/beta, SUPUESTA=1 hasta 13 pares), centinela por capa + capas_respeto.py (cada fuente contra placebo) + conteo de toques en pantalla; las 7 capas auditadas: strikes coinciden. Bloque 6 (16:00): el operador dejo solo NDX/SPX/SPY y dijo OLVIDATE DE OPUS, sigue Fable; perfil derecho y majors por capa en su color; beta NQ/ES con velas compartidas de los dos graficos (VelasCompartidas). Trampa central: TQQQ es 3x; un muro de SPX no es un precio de NQ."
metadata: 
  node_type: memory
  type: project
  originSessionId: 961a521e-545b-452c-9eed-32904fb03eae
  modified: 2026-09-15T16:41:00.885Z
---

El 2026-09-15, con 7 % de cuota, el operador pidio capas simultaneas de QQQ + TQQQ + NDX + Rithmic
en un solo grafico de NQ/MNQ (colores distintos, prender/apagar cada una, todo auditado). Fable lo
implemento en tres bloques (commits 1c09a4d3, a4a191b5, 67d023ab en main, SIN push) y dejo el estado
exacto y lo que falta al principio de `PythiaGex/conocimiento/traspasos/2026-09-15-capas-nq.md`.
Toda la logica de capas vive en `atas/PythiaGexNiveles/GammaHoyCapas.cs` (clase parcial de GammaHoy);
en GammaHoy.cs solo hay ganchos de una linea. DLL 1.10 instalado y ATAS reiniciado 13:55 local del 15-09:
"Gamma Hoy 1.10 (capas NQ) arranca", sin excepciones, AUDIT de MNQ#1 (QQQ) con los mismos strikes
(706/704) que antes del reinicio. Luego, con "hacelo vos todo" (14:03-14:10): push hecho, las 4 capas prendidas en el MNQ de QQQ por el dialogo
Indicators (buscador "Capa" filtra el grupo) y vistas en pantalla con leyenda, rayas y columnas; capas_nq.py
coincide con el AUDIT capa=QQQ (K 704/705, zero < 1 pt). Bug del AUDIT por capa arreglado (reloj por capa).
TQQQ: archivo flaco (7 strikes, +-1,5 % de NQ, sin 0DTE un martes): ensanchar linea_flaca para TQQQ.

Bloque 5 (14:30-14:55, pedido "traigamos SPX, ES, SPY... y ver cual dominante actua mas en NQ"): capas SPX,
SPY y ES (la viva de ES que graba el grafico de MES, leida por la cola del archivo) llevadas a NQ por beta
MEDIDA minuto a minuto (spot del libro alineado contra la vela de NQ; 13+ pares; SUPUESTA = 1 hasta entonces;
ajuste manual). Un muro de SPX NO es un precio de NQ: es "donde estaria NQ si el S&P llega a su muro y NQ lo
sigue con su beta". Cada capa anota sus dominantes en el centinela (<capa>_dom0...) y
`laboratorio/capas_respeto.py` las juzga con la regla y el placebo de rebote_niveles.py (importado, no
copiado); en pantalla solo el conteo de toques/rebotes de hoy, sin placebo. Las 7 capas auditadas con
capas_nq.py: strikes iguales, zero < 1,2 pts. Resultado de respeto: sin muestra todavia (las capas se anotan
desde las 14:50 del 15-09); la primaria de MNQ M2 da 15 toques, 73 % contra 56 %, "muestra corta".

Lo decidido ahi, para no relitigar:
- Capas ADITIVAS con una clase nueva `CapaLibro` (cadena + nucleo + lectura + razon por capa); la
  fuente primaria (`Libro`) y todo lo que cuelga de ella (centinela, gatillos, AUDIT, archivo) no se toca.
- Todas apagadas por defecto: el DLL nuevo no cambia los tres graficos en produccion.
- TQQQ: `Fut(K) = F_alineado x (1 + (K/S_tqqq - 1)/3)`; la razon simple corre los strikes lejanos.
  Magnitudes entre capas NO comparables (factor x3/x9 supuesto): cada capa normalizada a su maximo,
  nunca sumadas.
- TQQQ no se archiva todavia: agregar `"TQQQ": "TQQQ"` en archivar_cadena.py linea 126 y en cadenas.yml linea 47.
- Rithmic: reusar `_viva`, nunca una segunda suscripcion.
- Auditar con un `laboratorio/capas_nq.py` independiente contra el AUDIT por capa antes de decir que anda.

**Why:** el operador pidio explicitamente que Opus pueda seguir "sin romper ni hacer cosas ilogicas";
el riesgo real es tocar la primaria o mapear TQQQ como si fuera lineal.

Bloque 6 (15-09 16:00): el operador APAGO QQQ, TQQQ, Rithmic y ES a proposito para enfocarse en NDX, SPX y SPY,
que a simple vista le coinciden con los rechazos (con la advertencia medida: grilla mas fina = mas rayas cerca
de cualquier precio, y el contador estricto dio 0 toques ese dia). Dijo "olvidate de Opus, seguis vos": el
archivo de traspaso queda como registro de trabajo. Agregado: perfil derecho (convexidad) y majors por capa en
su color, y la beta medida con las velas de NQ y de MES por una pizarra estatica del DLL (la beta por spot del
libro fallo: r2 0). En 0DTE la convexidad de un strike es casi menos su gamma: el perfil derecho es un espejo.

Bloque 7 (16:15-16:30): "esta todo muy caotico": rediseño. Una sola escalera de rotulos a la derecha con el
nombre de la fuente PRIMERO y un cuadrado de su color ("SPX D1 28.981"), ordenada por precio y sin solapes;
majors solo a menos del 1 % del precio; columnas al 35 %; la primaria atenuada al 40 % mientras haya capas.
Regla aprendida: los rotulos de niveles van en UNA columna, nunca desparramados por el medio del grafico. Y a
una pestaña oculta ATAS no le manda OnCalculate: lo que deba correr siempre va tambien en el temporizador.

Bloque 8 (16:30-16:55): entre tres previews (A apiladas, B columnas, C superpuestas; scratchpad/mockups_capas.py
con PIL y libros reales) eligio la C: barras de todas las capas desde los bordes, transparentes, la mas larga
atras, primaria de fantasma sin rotulos; rotulo corto adentro de la barra ("SPX D1"); y los precios de las capas
ADENTRO de la escalera primaria como cajas de color (no quiere columnas repetidas: "a lo sumo dos"). Regla
aprendida: los previews con datos reales le sirven para decidir; darle a elegir entre 2-3, no imponer.

Bloque 9 (17:00): todavia "muy caotico": perfil ralo (umbral 25 % por fuente), sin perfil derecho ni majors ni
columna de precios (ajustes renombrados para pisar lo guardado), rotulo con cuadrado de color en la punta de la
barra, y FUSION de niveles que coinciden (raya gruesa de colores alternados + "SPX·SPY D1"). Instalado con el
mercado cerrado: verificar en la rueda del 16-09. Regla: cuando algo "confunde", primero SACAR (barras chicas,
perfil espejo, columnas repetidas), despues embellecer.

Bloque 10 (17:30): control triple pantalla = log = recalculo independiente para NDX, SPX y SPY (strikes iguales,
zero < 1 pt, convexidad 0 %). Para que el script coincida hizo falta replicar dos reglas del indicador: NDX con
base ADITIVA (no razon) y el EMPATE TECNICO de las dominantes (20 %: la mas cercana entre las comparables). Bug
arreglado: la fusion juntaba "SPX D2" con "SPY 0Γ" (tipos distintos); ahora solo el mismo tipo. capas_respeto.py
ya lista las tres capas (14 velas, 0 toques): el veredicto de "cual acierta mas" es de la semana que viene.

**How to apply:** empezar por leer el traspaso y seguir su orden de trabajo (archivador -> nucleo ->
capas sin dibujo -> auditoria -> dibujo). Ver [[gamma-hoy-1-9-2026-09-14]],
[[referencia-formulas-nq-medidas]], [[dos-libros-distintos]], [[nq-rithmic-pierde-0dte]].
