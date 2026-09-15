# Auditoria de punta a punta del grafico de MNQ con capas (15-09-2026, 19:15 local)

Pedido del operador: "audita todo el indicador completo de punta a punta, calculo por calculo". Esto es lo que hay
dibujado en el grafico de MNQ (con las capas QQQ, NDX, SPX y SPY prendidas; la primaria es el libro de "Libro en vivo", que a las 19:14 era NDX en este grafico),
que cuenta hay detras de cada cosa, de donde sale el dato, y con que se verifico. Todo lo que dice "verificado" se
recalculo hoy desde la cadena cruda con `laboratorio/auditar_todo.py` (que encadena `capas_nq.py` y
`capas_respeto.py`) y coincidio. Lo que dice "supuesto" se dibuja pero NO esta medido.

## 1. Los datos crudos y su edad

- **Cadenas de opciones de CBOE** (SPX, NDX, SPY, QQQ, TQQQ): el JSON retrasado de CBOE, archivado cada minuto en
  la rama `cadenas` por el workflow (`archivar_cadena.py`). Llegan **902 segundos tarde** (medido 14 de 14,
  memoria retraso-cboe-902s). La leyenda dice la edad de cada capa ("dato de hace 16 min"); la cabecera, la de la
  primaria ("vol CBOE 16 min tarde").
- **Interes abierto**: de AYER siempre (la OCC lo consolida de noche). Intradia el GEX solo se mueve por precio,
  volatilidad y VOLUMEN del dia.
- **Precio del futuro**: la vela de este grafico (MNQ por Rithmic, tiempo real).
- **Opciones de NQ por Rithmic** (capa RITHMIC, apagada hoy): tiempo real, pero su volumen arranca en cero con cada
  reinicio de ATAS.

## 2. La cuenta, strike por strike (la misma para la primaria y para cada capa: `GammaHoyNucleo.Calcular`)

1. Para cada strike y vencimiento dentro del horizonte (Hoy = el 0DTE o el mas cercano, envejecido a la hora de la
   cuenta): gamma de Black-Scholes con la IV de la cadena (Black-76 si el libro es del futuro), T = dias / 365.
2. **GEX por volumen** del strike = (gamma_call x volumen_calls − gamma_put x volumen_puts) x 100 x S² x 1 %.
   Convencion +call −put (asuncion estandar de que lado esta la mesa, no un dato). El GEX por OI usa OI en vez de
   volumen; es la "sombra" y el respaldo de noche.
3. **Convexidad** del strike = GEX(S x 1,01) − GEX(S): cuanto cambia el GEX si el precio sube 1 %. En 0DTE es casi
   menos la gamma (a +1 % la gamma del 0DTE se muere): el perfil derecho es un espejo del izquierdo.
4. **Zero gamma**: la suma del GEX repreciada en una grilla de +-3 % (61 pasos; x apalancamiento) del precio del
   libro; donde cambia de signo, interpolado; recien despues se lleva al futuro.
5. **Majors** +Γ / −Γ: la barra positiva mas grande y la negativa mas grande del libro.
6. **Dominantes**: dentro del radio del 2 % del precio, la barra mas fuerte ARRIBA y la mas fuerte ABAJO ("una por
   lado"); con empate tecnico (a menos del 20 % de la mas fuerte) gana la mas cercana al precio; y con
   "centroide" la dominante se corre al centro de masa del GEX a +-12 pts del strike (por eso se ve 29.256,30 y no
   29.256,17). Si no hay volumen (noche), dominantes por OI, y se dice.
7. **Como se lleva cada strike al precio de NQ**:
   - QQQ, SPY, TQQQ (ETF): Fut = K x razon, razon = vela de NQ de hace 902 s / spot del ETF en la cadena ("vela
     alineada"); si no hay vela, la mediana de la rueda; si no, la cruda, dicha como tal. TQQQ ademas con
     apalancamiento 3: Fut = F x (1 + (K/S − 1)/3).
   - NDX: Fut = K + base (aditiva); la base medida por paridad si es confiable, si no la de la rueda, si no la
     TEORICA por carry (hoy: 6,17; la cruda 307 es el roll a diciembre y se descarta).
   - SPX, SPY como "otro subyacente": ademas de la razon, la distancia porcentual al spot se multiplica por la beta
     NQ/S&P (apalancamiento = 1/beta). Ver "supuesto".

## 3. Lo dibujado, elemento por elemento

| Elemento en pantalla | De quien es | Cuenta | Verificacion de hoy |
|---|---|---|---|
| Barras de la izquierda, en colores | cada capa (celeste QQQ, gris NDX, violeta SPX, turquesa SPY) | GEX por volumen del strike, normalizado al maximo de SU fuente, solo si pesa mas del 25 % (dominantes siempre); largo en raiz cuadrada | strikes dominantes = recalculo: QQQ 700/708, NDX 28600/29250, SPX 7580/7620, SPY 755/760 |
| Sigla en la punta de cada barra | la fuente de esa barra ("SPX", "SPX D1") | — | visual 19:09 |
| Rayas discontinuas | D1 (gruesa) y D2 (fina) de cada capa, en su color; punteadas: zero (si esta prendido) y majors (si estan prendidos) | items 5-6 | idem |
| Raya gruesa de dos colores | dos fuentes con el MISMO tipo de nivel a menos del 0,03 % del precio (fusion) | agrupacion por tolerancia | visual 17:20 (SPX·SPY) |
| Guiones sobre las velas, en colores | la estela: donde estaba la dominante de esa capa en esa vela (D1 grueso, D2 fino) | un guion por cada vez que la dominante se movio mas de 0,25 pt; el pasado rebobinado desde el archivo por minuto | log 18:06: QQQ 127/127, SPY 98/98, NDX 231/231, SPX 238/238 cadenas con guion, 0 sin vela |
| Escalera pegada al eje | los niveles de las capas visibles en pantalla: "SPX D2 ▼ 28.965 −5" (fuente, tipo, sentido, precio, distancia) | jerarquia por tamaño (D1 normal, D2 chica, zero/majors minima), orden por precio, marquita al precio exacto | visual 19:00 y 19:09 |
| Barras de la derecha (si esta prendido "perfil derecho") | convexidad de cada capa | item 3 | conv en el precio: QQQ +242M vs +243M, NDX +49 vs +50, SPX +1433 vs +1444, SPY +295 vs +293 (0-1 %) |
| Rayas y barras AMBAR / puntitos blancos / raya gris punteada | la PRIMARIA: el libro que diga "Libro en vivo" del grafico (a las 19:14 este grafico tenia NDX; el otro MNQ, QQQ). La leyenda lo dice: "primaria (ámbar) = NDX" | la misma cuenta que la capa de ese libro | AUDIT primaria = AUDIT capa del mismo libro y la misma vela (auditar_todo.py, seccion 2). Desde el bloque 18, si esa capa esta prendida la primaria se oculta por duplicado |
| Leyenda de abajo | edad del dato, beta, dominantes y "reboto N de M toques" por capa; y que es la primaria | — | visual |
| "rebota N de M toques" | conteo del dia con la regla del laboratorio (venir de 30 pts, banda 8, R 20 en 20 min) | sin placebo: es el numerador | hoy 0 en todas (la regla no se disparo) |
| Cabecera (regimen, conv, pico) | la primaria | cuadrante por pico y convexidad | auditado en sesiones anteriores (memoria auditoria-punta-a-punta) |

## 4. Verificado hoy, con el numero

- Cuatro de cuatro libros coinciden strike por strike con el recalculo independiente (`auditar_todo.py`, 19:12):
  zero a menos de 0,7 pts, convexidad en el precio a 0-1 %, pico |GEX| identico.
- La primaria y la capa de su mismo libro, en la misma vela, dan lo mismo (auditar_todo.py, seccion 2).
- TQQQ con apalancamiento 3 (14:11): K 70 -> 29.254 y K 67 -> 28.829; con razon lineal habrian caido 500 pts
  corridos.
- La estela: todas las cadenas del archivo produjeron guion en su vela, ninguna sin vela ni sin base.

## 5. Supuesto (se dibuja, pero NO esta medido) y limites conocidos

- **Beta NQ/S&P = 1** en SPX y SPY (SUPUESTA): hacen falta 20 pares de velas de NQ y MES en la misma hora y
  r² >= 0,2; hoy llego a 17 pares antes de un reinicio y a 3 despues. Hasta que se mida, un muro de SPX se dibuja a
  la distancia porcentual pura; si la beta real es 1,2, los muros de arriba quedan un poco mas lejos de lo dibujado.
- **Magnitud de TQQQ**: la posicion de sus strikes esta bien; el tamaño de sus barras no es comparable con QQQ
  (factor x3 o x9, supuesto). Ademas su archivo es flaco (7 strikes, sin 0DTE los martes).
- **"Cual acierta mas"**: sin muestra. Las capas se anotan en el centinela desde las 14:50 de hoy; el contador
  estricto dio 0 toques; la primaria, en 6 dias, 15 toques con 73 % contra 56 % de placebo, muestra corta. Hacen
  falta 25+ toques por fuente para decir algo.
- **El archivo de hoy no cubre la rueda entera** (96-238 cadenas por ticker en vez de ~390): revisar el dedupe
  del archivador.
- **La convexidad en 0DTE es un espejo** del perfil izquierdo: no agrega informacion hasta que haya vencimientos
  mas largos en el horizonte.
- El sentido ▲/▼ y las palabras techo/piso describen el comportamiento esperado por la gamma, no una prediccion.

## 6. Como repetir esta auditoria

```bash
cd "C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex" && python laboratorio/auditar_todo.py MNQ M2
```
Con ATAS abierto y las capas prendidas (lee el log del indicador y baja las cadenas de la nube).
