---
name: referencia-perfil-derecho-2026-09-14
description: "Barras y pelotitas de la referencia, medidas el 14-09 con 17 capturas y 5 grabaciones del operador: perfil izquierdo = gamma x volumen neto 0DTE (r 0,999 en ES); pelotitas = punta hace 15/5/1 min (confirmado por su short nuevo); perfil derecho: estable en minutos, largo ~ |gamma x volumen| (r 0,84) pero el SIGNO no es ninguna griega estatica (2016 combinaciones); hipotesis: flujo firmado comprador/vendedor (QuantData). Su nueva 'GAMMAlito Flow' usa QuantData."
metadata: 
  node_type: memory
  type: project
  originSessionId: cfe2e2e1-5319-4bee-a9ea-7507c655c3e4
  modified: 2026-09-14T16:47:12.961Z
---

Medido el 2026-09-14 (13:30-14:00 local) con `barras.py` / `perfiles.py` / `video_serie.py`
(scratchpad de la sesion) sobre las capturas de `OneDrive\Imágenes\Screenshots` y las
grabaciones de Game Bar en `Videos\Captures` (5 videos, 5,6 min, 1600x900, son un stream de
Discord: el contenido queda a ~1170x650). Con el modelo Vosk del scratchpad 9345174c se
transcribio el audio: es charla, salvo dos frases ("las barras amarillas mas largas = donde
los market makers tienen mayor exposicion, sirven de iman"; "los primeros 25 minutos calcula
el volumen del dia y despues empieza a generar").

## Perfil izquierdo (cerrado)

Captura 11:46 ET contra la cadena de SPX de CBOE bajada a las 16:01 UTC (mismo minuto de
mercado): 18 barras medidas en pixeles contra gamma x (vol calls - vol puts) del 0DTE:
**r = 0,999, signo 18 de 18**. Escala ~2,1 px por mil millones. Es la misma formula que en
NQ/QQQ. El eje se calibra con la caja roja del ultimo precio y la amarilla del VWAP
(1,278 px/pt en esa captura).

## Pelotitas (cerrado, sin novedad)

Short nuevo bajado el 14-09 ("Las pelotitas Max Change son importantes", 28 s, sin voz,
todo texto en pantalla): "pelotita mediana = 5 min atras", "pelotita + grande = 15 min
atras", "dentro de la barra = el GEX esta aumentando; fuera = disminuyendo". Es exactamente
lo que ya implementa Gamma Hoy (VerPelotitas) y lo medido el 09-10. En el video de 11:30 ET
(112 s a 1 fps) el puntito chico queda a <6 px de la punta actual y el mediano se acerca a
la punta de hace 75-85 s (error 4,5 px): compatible, no discriminante (las barras se mueven
pocos px por minuto).

## Perfil derecho (ABIERTO, pero acotado)

- **Estable**: en el video de 11:30 (112 s) ninguna barra cambio de color; entre las
  capturas de 11:46, 11:49 y 11:51 la secuencia de colores de arriba a abajo es la misma
  (P P T T P T T P T T T ...). No es un Max Change ni un delta de pocos minutos.
- **Largo**: correlaciona con |gamma x volumen 0DTE| (r 0,84). Cada perfil autoescala.
- **Signo**: alterna entre strikes vecinos (7645 T, 7640 T, 7635 P, 7630 T, 7625 T,
  7620 P, 7615-7605 T, 7600 P...). Probadas 2016 combinaciones (gamma, speed, vanna,
  charm, delta, vega x vol/OI/vol-OI/vol+OI/solo calls/solo puts x 0DTE...90d x tres
  convenciones de signo) y deltas en el tiempo de 10 a 50 min: **ninguna pasa de 16/19
  signos y las que llegan tienen r|abs| ~0,1** (ruido). Con el paso de +1 % la convexidad
  0DTE es solo -signo(GEX): el paso se pasa de largo el strike.
- **Hipotesis que queda**: volumen firmado por agresor (compras - ventas) x gamma, la
  "posicion del dealer por flujo". CBOE no lo da; QuantData si. En la grabacion de 10:46
  el streamer muestra su herramienta nueva "GAMMAlito Flow" en localhost: "Fuente:
  QuantData en vivo (net-cmf 1m + exposure-by-strike)", y una "GEX MATRIX" strike x
  vencimiento (0D..4D + resto) con "flip 7.643,86 (sin 0DTE 7.644,91) 09:45 ET" para SPX;
  nuestro zero por OI repreciado a esa hora daba 7651-7652. Para el libro de ES de Rithmic
  tenemos compra/venta por contrato (`vol_compra`/`vol_venta` en la viva): se puede
  construir un perfil firmado propio, pero no se puede contrastar con el de ellos (otro
  libro).

## Lo que se ve en el ATAS del operador (13:42 local, con posicion abierta en MNQ)

Gamma Hoy 1.8j con **Libro en vivo = Rithmic** (el default del codigo es CBOE_SPX): por
construccion no puede coincidir con la referencia, que dibuja SPX/SPY (ES) y QQQ (NQ).
Las barras propias se ven chicas y oscuras (AnchoBarras 90, sombra de OI, escalera al eje,
DOM Trader al lado) y las pelotitas se pisan con los rotulos: el problema es de lectura, no
de calculo. Para ver lo mismo que la referencia en MES: Libro en vivo = CBOE_SPX (15 min de
retraso). Para MNQ haria falta un libro QQQ en el indicador (hoy solo NDX): pendiente.

**Why:** el operador dijo que la referencia usa barras y pelotitas como lo mas importante y
que nosotros las tenemos abandonadas; habia que saber que esta resuelto y que no.

**How to apply:** no perseguir mas el perfil derecho con cadenas de CBOE: hace falta flujo
firmado. Ver [[auditoria-2026-09-14-referencia-es]], [[pelotitas-max-change-medidas]],
[[referencia-formulas-nq-medidas]], [[dos-libros-distintos]].

## 18-09: la referencia en vivo en el panel del navegador (solo mirando: capturas del escritorio + sus dialogos de ajustes)

REGLA DEL OPERADOR (18-09): NO meterse en el codigo ni en las llamadas de la pagina de la referencia ("nos detectan y me bannean"); solo mirar la
pantalla y sus menus como un usuario. Lo que se vio en sus ajustes: el estudio tiene 2 parametros (ticker QQQ | NDX, expiry 0DTE); en Estilo el
perfil izquierdo se llama **"GEX profile"** (verde/rojo, ancho 30 %, barra 10 px, izquierda, marcadores azules) y el derecho **"Gamma profile"**
(cyan/purpura, derecha, marcadores grises); la "nube" son "Dominant 1/2 history" (azul) y "Zero gamma history" (gris): la historia del dia entera.
Corren DOS instancias superpuestas (QQQ 0DTE y NDX 0DTE): por eso hay 2 zero, 2 major+, 2 major- y barras cada 5/10 pts (NDX) entre las de 41 pts (QQQ).
Cada lado esta NORMALIZADO a floor(0,3 x ancho del grafico): los largos son relativos a la barra mas larga; de una captura sola no sale la escala.
Medido con `_referencia/2026-09-18-dashboard/` (medir_pantalla.py, candidatos_qqq.py, replicar.py, resultados.md; 4 capturas validas 12:21-12:29):
izquierdo QQQ = gamma x (vol_c - vol_p) del 0DTE con la cadena de CBOE del minuto exacto: R2 0,88-0,90 (23 barras, 21/23 signos; solo 5-6 barras QQQ
visibles, pisadas por NDX; con la cadena de 15 min antes 0,44); zero = cambio de signo interpolado de SU perfil (verificado a 0,01 strike); majors = strike
de mayor/menor GEX (719/716, igual que nosotros); dominantes = las dos barras mas largas (en NDX verificado; en QQQ la 2a cae fuera del panel). Nuestro zero
queda 7-15 pts ARRIBA del suyo por UN strike al dinero (717: nosotros -0,81B, ellos ~0/+1,4B): otro volumen al dinero, o firmado. Derecho ("Gamma profile"):
1.545 candidatas (griegas x vol/OI/vol-oi/vol+oi, estaticas y repreciadas, diferencias 1/5/15/30/60 min y contra apertura, +-izquierda): NINGUNA >= 90 %
de signo; el 719 (2a mas larga a la izquierda, ~0 a la derecha) las mata a todas. Queda solo flujo firmado por agresor (tienen un estudio "HIROlito").
Fallas NUESTRAS encontradas de paso: (1) el archivador de QQQ repite la misma cadena (Feed.cs Archivo.Sello incluye la base viva): 1234 lineas pero 42
cadenas distintas en el dia, una cada 8-21 min en rueda; (2) en el log AUDIT hay dos razones NQ/QQQ intercaladas (dos graficos) y entre 04:02 y 05:22 local
una instancia uso el spot de CBOE congelado de la noche: mapeo +0,8 % (~240 pts) esa hora y pico; (3) la cadena viva de ES dejo de producir a las 06:57 UTC.
No se toco produccion. Capturas cada 5 min siguen guardandose para remedir con mas horas (solo sirven las que tienen el panel visible en el escritorio).

