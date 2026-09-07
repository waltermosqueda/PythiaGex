---
name: produccion-en-vivo-2026-09-06
description: "Revision de los tres graficos de ATAS en vivo el domingo 2026-09-06 (Asia, vispera de Labor Day): siete errores de codigo con linea, el rotulo de regimen mezcla escalas, las flechas de absorcion son una prueba degenerada, y la cadena viva hoy no alimenta nada."
metadata: 
  node_type: memory
  type: project
  originSessionId: 9345174c-7f69-4fe5-a793-976e41f2dc7c
  modified: 2026-09-07T00:23:09.170Z
---

Medido el 2026-09-06 21:10-21:20 AR sobre la pantalla real (MES 1m, MES 5m,
MNQ 5m) y el renglon AUDIT de `%APPDATA%\ATAS\pythiagex-gammavivo.log`.
ATAS esta en **8.0.14.398** (CLAUDE.md dice .397).

## Estado de produccion

- Los niveles de MES salen del **libro de SPX + base 6,17** (`LibroViva` = false,
  por diseño despues de [[dos-libros-distintos]]). No alterna mas. Bien.
- La cadena viva de Rithmic esta suscrita (180 contratos ES, 142 MNQ) pero
  **no alimenta nada**: LibroViva apagado y volumen roto (`volavisos=0`,
  `volconvol=0`, tercera noche). Solo gasta ancho de banda.
- NivelesGamma (el de los gatillos/Disparo.cs) **ya no esta en ningun grafico**:
  `contexto/` y `disparos-*.jsonl` no se escriben desde el 2026-09-03.

## Siete errores encontrados, con linea

1. **Rotulo de regimen mezcla escalas.** `GammaVivo.cs:2266-2270` guarda
   `_zeroGamma = zero + base` (futuro) y `_spotUsado = S` (indice);
   `GammaVivo.cs:3365` los compara. Resultado: dice "GAMMA - expansion" con
   net **+3,06 B** en pantalla. Miente en toda la franja de 6 puntos entre el
   zero y zero+base, que es justo donde estuvo el precio toda la noche.
   Arreglo: usar `_gammaPositiva` (linea 2255, que compara bien).
2. **Flechas de absorcion: prueba degenerada.** `NodosDeVolumen.cs:446-450`
   compara el volumen de los `TicksExtremo+1 = 3` niveles del extremo contra
   `volVela/niveles`. Una vela con volumen parejo da razon **exactamente 3 =
   FuerzaAbsorcion**. En MES 1m dispararon 13 flechas en ~84 velas dentro de
   un rango de 5 puntos. Arreglo: promedio por nivel del extremo contra
   promedio por nivel del resto de la vela, y rango minimo de vela.
3. **Cadena viva de NQ muerta.** `CadenaViva.cs:199` atrapa
   `Object reference not set` al buscar NQ; cae a MNQU6 que solo tiene el
   trimestral (12 dias). Se necesita un grafico de NQ abierto o arreglar la
   busqueda.
4. **"flujo hoy 622.725"** (`GammaVivo.cs:3418`) es el volumen del VIERNES:
   la cadena de CBOE del domingo es la del cierre anterior. El rotulo tiene
   que llevar la fecha de la cadena.
5. **lagdom negativo** (`-3158 ms` en NQ): el desfase del reloj de la maquina
   (ver [[costo-real-del-retraso]]) nunca se implemento en el codigo.
6. **Dos motores, dos muros.** Pantalla: `-wall 7681` (majorneg=7675 idx,
   `minglobal=7675`). El archivo `pythiagex-cadena-usada-ES.json` que escribe
   el mismo indicador: 7700 con **-3.085 M** contra 7675 con -2.834 M, y lo
   marca como "acelerador abajo, relevante". Hay que unificar la fuente.
7. **Ventana de nodos en velas, no en tiempo.** El mismo precio 7721,25 dice
   `26,7K d -479` en 1m y `69,2K d +248` en 5m. Mismo indicador, mismo nivel,
   signo de delta opuesto en dos pestañas. La ventana debe ser en horas.

UI menor: la caja NODOS DE VOLUMEN pisa la leyenda de indicadores; la etiqueta
`+wall` queda tapada por el boton ▶ de ATAS arriba a la derecha.

## Lo que se vio del mercado (sin conclusion, es domingo)

Precio clavado 3 horas en 5 puntos, sobre el zero gamma (7719,7) y sobre el
nodo de mayor volumen del viernes (7718-7721). Mediana 47 operaciones/min.
El call wall se disputa: la cadena refresco a las 23:19 UTC y `wall_pos`
salto de 7831 a 7756 (75 puntos). El mayor positivo global es 7806 (7,8 B) y
el radar lo descarta por alcance. Debajo, 7706 concentra -3 B y el archivo
dice "no hay piso de freno abajo".

## Labor Day

**Lunes 2026-09-07 es Labor Day.** CBOE confirmado: sin rueda regular, solo
GTH (20:15 dom a 11:30 lun, 20:15 lun a 09:25 mar). La cadena ya no tiene
vencimiento del lunes: el primer 0DTE es el **martes 8**. ES opera con cierre
anticipado (confirmar hora en CME; la pagina no cargo).

**Why:** el operador creia que el lunes era "el gran movimiento" y que los
indicadores estaban listos; ni una cosa ni la otra.

**How to apply:** los siete quedaron arreglados y verificados esa misma noche (ver
abajo). La cadena viva NO se apaga: quedo alimentada por los eventos del conector. Ver [[retrospectiva-2026-09-06]],
[[volumen-opciones-en-vivo]] y [[indicador-que-cuelga-atas]].

## Resultado de las correcciones (misma noche, tres reinicios verificados)

Todo probado antes con calculo independiente y despues en pantalla:

- Rotulo de regimen: con precio 7717,50 y zero 7717,41 (dentro de la franja de
  6 puntos donde mentia) el panel dijo "GAMMA + rango" y el AUDIT
  `regimen=positivo`. El codigo viejo habria dicho "expansion".
- Reloj: `reloj_ms=3278..3288` contra +3.270 ms de `w32tm`. El atraso del libro
  paso de -3158 a **+111 ms** (NQ), coherente con los 157 ms medidos el 03-09.
- Absorcion: de 13 flechas en 84 velas a 20 en TODA la historia del grafico de
  1 minuto, cada una con sus numeros en `pythiaflow-absorcion.log`. La primera
  prueba del arnes (`atas/_test_flow`) demuestra que la regla vieja disparaba
  con una vela pareja.
- Ventana en horas de mercado: el mismo nodo 7721,25 dice **69,2K d +265** en
  las pestañas de 1 y 5 minutos (antes 26,7K d -479 contra 69,2K d +248).
  Primera version usaba horas de reloj y el domingo daba 37 velas: corregido.
- Muro disputado: aparecio solo, `mnratio=1.00 mndisp=7656`, panel
  "-wall 7.681,16 disp 7.656,16". Y al refrescar la cadena a las 00:38 UTC el
  call wall paso a 7825, lo mismo que dio la recalculacion manual desde el
  crudo de CBOE con esa cadena.
- Zonas del radar contra el indicador, en el AUDIT: `7750:+6193Mpy/+2628Mcs;
  7700:-3184Mpy/-1723Mcs` (45 d contra 7 d). En MNQ `29550:+466Mpy/+29Mcs`:
  la zona es casi toda gamma lejana.
- Flujo: el panel dice "flujo cadena 06/09 21:38"; dira "flujo vie 04/09
  16:14 NY" cuando el feed publicado traiga `ultimo_trade` (Python ya lo
  emite; falta que corra el workflow).
- Cadena viva: ver [[volumen-opciones-en-vivo]]. ARREGLADA.

Commit `1ec1f69` y siguientes. ATAS es **8.0.14.398**.

## Estado al cierre de la noche (23:20 AR)

- **Instalado en ATAS** (build 22:48): las siete correcciones, la cadena viva
  por eventos del conector, nodos con ranking (4 maximo), muros atenuados por
  distancia y vencimiento, circulos de volumen vivo por strike.
- **Compilado pero NO instalado**: la ESCALERA (peldanos cercanos con
  probabilidad de toque por vol realizada) y el puente AppDomain entre los dos
  indicadores. El operador pidio elegir el modelo visual primero: seis
  maquetas en https://claude.ai/code/artifact/44ed6b61-8f24-47d3-86a6-2f3e26b207f0
  (1 escalera lateral, 2 etiquetas en el eje, 3 peso por cercania, 4 columna
  de calor, 5 semaforo minimo, 6 tarjetas). Instalar lo que elija y verificar.
- Pendiente: NQ live chain (NullReference al buscar NQ) sigue sin arreglar.
- Regla nueva de seguridad en [[verificar-yo-no-el-usuario]]: nunca ENTER a
  ciegas a ATAS (aparecio un dialogo de orden de compra a mercado).

## Madrugada del 7 (01:50-02:40): modelo 3 instalado y auditado

- Instalado (build 02:20): chips formato B de una linea con la parte desde
  la griega atenuada, nunca sobre su raya ni sobre la vecina, apilados en el
  borde, corridos a la izquierda del tablero si chocan; cercanos por lado
  (el mas cercano primero, piso 15 %); nodos punteados con distancia y
  chance; barras laterales en raiz cuadrada.
- Auditoria independiente (Black-Scholes propio, misma cadena): 7706 ->
  gex -1,4B / oi 20,2k / ac +876M / v52; 7711 -> -473M / 4,0k / +276M /
  v184; 7686 -> -1,3B / 6,7k / +973M / v137. Coinciden con los chips. Zero
  propio 7716,58 contra 7715,89 del indicador (0,7 pts, convenciones).
- Compilado y NO instalado (commit c86d213): puntitos por vela, estela y
  zonas del radar apagados por defecto. El operador pidio elegir por
  maqueta: https://claude.ai/code/artifact/bf61ee06-8f39-4e4b-b4eb-9b185aa3e0a7
  (etiqueta) y la del vocabulario (que se dibuja) publicada a las 02:40.
- Regla que pidio: nuevas versiones = maquetas primero, el elige, recien
  ahi se instala.
