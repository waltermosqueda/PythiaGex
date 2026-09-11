# La referencia: lo tecnico que sirve, consolidado

Este documento reune lo que se aprendio estudiando el tablero externo que se tomo
como referencia para diseñar Gamma Hoy (videos vistos cuadro a cuadro y transcriptos
entre el 2026-09-02 y el 2026-09-10). Es la copia de resguardo del conocimiento: las
transcripciones ya no estan en ningun repositorio. Todo lo que sigue esta ademas
medido o desmentido en el laboratorio de PythiaGex; cuando algo esta medido, se dice.

## 1. Las siete piezas de su pantalla y que significan

1. **Perfil izquierdo, GEX por strike** (verde positivo, rojo negativo). Las barras
   son **gamma por VOLUMEN de opciones del dia**: "respiran" con cada operacion. La
   dominante es "la barra mas larga". Perfil del 0DTE a la izquierda; a la derecha,
   la version total (hasta 90 dias).
2. **Zero Gamma**: el nivel que separa regimen positivo (rango, volatilidad
   suprimida, la mesa vende maximos y compra minimos) del negativo (tendencia, la
   mesa amplifica). Lo describen como "un trailing stop del mercado".
3. **Major Positive / Major Negative**: el mayor GEX positivo y negativo del dia,
   "las zonas extremas". Son nuestro call wall y put wall.
4. **Dominantes**: uno o dos guiones amarillos por vela (primaria gruesa, secundaria
   fina; hasta cinco configurables). Se dibujan como un guion por ACTUALIZACION, no
   uno por vela: la fila de guiones muestra donde nacio y cuando salto. Medido en
   video: la dominante es una banda de ~5 puntos de NQ que ondula (centroide
   ponderado por gamma), no una raya plana en un strike; amarillo hue 29 la
   primaria, naranja hue 19 la secundaria.
5. **Convexity Ladder (perfil derecho)**: cuanto cambia la gamma con el precio.
   Aguamarina = convexidad positiva (colchon, la mesa frena, reversiones); purpura =
   negativa (tobogan, la mesa amplifica, tendencia). Es nuestra "aceleracion".
6. **Big Trades**: bloques grandes de OPCIONES (no prints del futuro) dibujados sobre
   la vela del momento con el tamaño en contratos. Umbral recomendado ~180 contratos
   en QQQ/NQ, mas en ES.
7. **CVD del futuro**: divergencia = posible reversion; confirmacion = ruptura.

Ademas: **Max Change** = tres pelotitas por barra: grande = donde estaba la punta de
la barra hace 15 minutos, mediana = hace 5, chica = hace 1. Adentro de la barra =
ese strike crece; afuera = decrece; alineadas = crecimiento sostenido;
desparramadas = mucho momento; desordenadas = indeciso. "Lo mas importante no es la
dominante sino la pelotita: es adelantado": una dominante nace como semilla en el
Max Change y unos 45 minutos despues es la barra mas larga.

Datos: el proveedor de datos sirve OI y GEX por volumen; NQ se dibuja con el libro
de QQQ (razon ~40,7) y ES con SPY o SPX; NDX, QQQ y NQ "sincronizados en una sola
pantalla".

## 2. El cuadrante de regimen (GEX cerca del precio x convexidad)

- Mucho GEX + convexidad positiva = **iman colchon**: rango y reversion alrededor
  del pico; muchas rupturas terminan falsas; riesgo bajo.
- Mucho GEX + negativa = **nivel explosivo**: zona de activacion; si rompe, seguir
  la ruptura con stop ajustado; riesgo alto.
- Poco GEX + positiva = **mercado estable**: rangos amplios, rebotes limpios, sin
  movimientos explosivos.
- Poco GEX + negativa = **salvese quien pueda**: tendencia, sin frenos; reducir
  tamaño o no operar.
- Transicion: por encima del maximo GEX con convexidad positiva, pensar rango; perdido
  ese nivel y con convexidad negativa, pensar tendencia. "No confundir la exposicion
  gamma con soporte o resistencia: depende de la convexidad, puede frenar o
  acelerar."

Flujo de decision que enseñan: 1) hay un pico de GEX cerca? 2) la ladder es positiva
o negativa? 3) regimen 4) estrategia.

## 3. Cuando una dominante frena y cuando se rompe (sus tres condiciones)

1. Contexto del dia: balanceado (gamma neta positiva y alta) respeta; expansivo rompe,
   mas si el GEX cae. "El contexto no lo define el precio, lo define la gamma".
2. Distancia al zero gamma: lejos = rebote mas probable; recien cruzado = rupturas.
3. CVD: divergencia frena; confirmacion rompe.

Mecanica que afirman: la mesa compra cuando el precio baja hacia la dominante y vende
cuando sube hacia ella (iman). Y sobre sus lineas en general: NO dicen que el precio
rebote; dicen que ahi "va a aumentar la velocidad del tape" porque la mesa rehedgea.

## 4. La plantilla operativa que enseñan (hipotesis, no sistema)

Entrada en rechazo de dominante debajo del Major Positive; stop del otro lado de la
dominante; objetivo 1 el zero gamma, objetivo 2 la siguiente dominante. Cuatro
preguntas antes de entrar: zona de resistencia gamma?, CVD a favor?, volumen
institucional (Big Trades)?, zona de reaccion o "medio de la nada"? Ejemplo de señal
publicada: NQ corto 29.301, stop 29.383,78 (82,8 pts), objetivo 29.230,05 (71 pts):
relacion 0,86, necesita mas del 54 % de aciertos; el porcentaje no lo publican.

## 5. Lo que medimos en PythiaGex (contra placebo)

- "Respeta las dominantes": gamma x OI pierde contra el placebo (-4,0 pp; NQ 4 dias
  -2,9 pp); ganan el volumen del dia (+42 pp) y gamma x volumen (+22 pp). Por eso
  Gamma Hoy usa el libro de volumen para las dominantes.
- "Se acelera el tape": medido en 1.051 velas de 1 min de MES: en nivel vol x1,17 /
  ops x1,15 / |delta| x1,24 contra placebo x1,13 / x1,11 / x1,18. No hay aceleracion
  atribuible al nivel: las velas grandes tocan mas rayas.
- Max Change "adelantado": un strike con las tres pelotitas adentro pasa a dominante
  en 45 min el 9,5 % de las veces en ES (NQ 5,5 %) contra 4,4 % (2,8 %) quieto; pero
  el que decrece tambien (8,2 %). Duplica la chance, y nueve de cada diez no llegan.
  Es contexto, no gatillo.
- Las barras no se mueven en vertical (|dy| p90 <= 1,9 px): cambian de largo; el
  sube y baja aparente es la autoescala del grafico.
- Zero gamma como regimen: pendiente de medir.
- Lo que NO se copia: zonas de otro indicador (cajas verdes/rojas de
  soportes/resistencias de un tercero), señales con relacion < 1, "siempre lo supo".

## 6. Ajustes de su web que sirven de guia

Perfil por metrica (exposicion o aceleracion) y por vencimiento (90 dias, 0DTE, 1DTE;
"trabajamos en 0DTE"); dos dominantes por defecto (hasta cinco); pelotitas de 1, 5 y
15 minutos con tamaño creciente; Big Trades con umbral en contratos; clasificacion
del proveedor con umbrales de gamma positiva y negativa; panel de "gamma
estadistica" con bloque VOLUME (zero gamma, majors, net GEX), bloque OPEN INTEREST y
"max change GEX" a 1/5/10/15/30 minutos con magnitud.

En Gamma Hoy esto ya esta: libro por volumen con sombra de OI, dominantes por vela con
centroide y empate tecnico, Max Change 1/5/10/15/30 con pelotitas por strike,
convexity ladder, cuadrante de regimen, Big Trades desde la cadena viva de Rithmic, y
el centinela que mide cada afirmacion antes de creerla.
