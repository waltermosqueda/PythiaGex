# Delta Vivo 1.0 — EN PRUEBA, no validado

Marca en el gráfico, **en el precio exacto y en el instante exacto**, el momento en que la
agresión firmada —compras agresoras menos ventas agresoras— se fue para un lado en pocos segundos.
Es la misma idea que Absorción Viva, pero mirando el **delta** en vez del consumo de un nivel.

**No es una señal y no dice para dónde va el precio.** Está medido que perseguir estas marcas
pierde: 0 de 36 celdas con neto positivo, de −1,17 a −9,27 puntos al tacto
(`laboratorio/dom/ronda11_delta/r11_f3_04_juez.csv`, 1.568 vueltas, 22 ruedas de MNQ). Operar en
contra tampoco paga: la mejor celda da +0,932 con t +1,85 contra un techo del azar de 3,93, y se
cae con el control apareado. Esto **describe** lo que está pasando en la cinta.

---

## Qué dibuja

| Marca | Qué es | Cuántas por sesión (medido, 23 ruedas de MNQ) |
|---|---|---|
| **Rombo lleno** | LA OLA: el delta neto de los últimos 10 s pasó la vara | mediana 10 (mín 2, máx 22) |
| **Rombo hueco** | La misma ola, pero PURA: 70 % o más del volumen fue de un solo lado | 47 de 241 olas (19,5 %); mediana 1 por sesión |

- **Azul** = empujaron las compras. **Naranja** = empujaron las ventas.
- El rombo **no tiene punta**: este indicador no dice para dónde va el precio.
- El tamaño va en **veces la vara**, no en contratos sueltos, porque la vara cambia de día a
  noche. La ola típica queda en 15 px; la pelotita típica de Absorción Viva mide 17 px.

Las otras tres familias (**contrapié**, **traba**, **nido**) se calculan y se anotan siempre en el
CSV, pero **no se dibujan de fábrica**. El nido no tiene ni siquiera un ajuste para prenderlo: ver
más abajo.

## Por qué no usa verde ni rojo

En la pantalla del operador ya conviven **dos verdes con la lectura opuesta**:

- Big Trades oficial: verde = hubo compra y **la compra ganó**.
- Absorción Viva: verde = hubo compra y **la compra perdió** (se la comieron).

Un tercer verde volvería el color inutilizable. Delta Vivo usa **azul y naranja**, que además se
distinguen con daltonismo. Hay un ajuste de un clic para darlos vuelta y otro para elegir
cualquier par.

Hay una **trampa semántica** que queda escrita también en el código: el campo `Lado` de un
`Evento` significa lo contrario en los dos indicadores. En Absorción Viva `+1` = se comieron
compras agresoras (la compra perdió); acá `+1` = delta comprador neto (la compra ganó).

## Cómo se separan de las otras marcas en pantalla

Por **forma** primero, por color después:

- Big Trades: círculo lleno, 18 a 22 px.
- Absorción Viva: círculo lleno de 10 a 26 px, y círculo con aro fino para el iceberg.
- Delta Vivo: **rombo**. Lleno la ola, hueco la ola pura, cuadrado la traba, punteado el contrapié.

Cuánto se pisan, medido: el **5,0 %** de las olas cae a ≤ 30 s y ≤ 4 ticks de una pelotita real de
Absorción Viva (las 642 que dejó su arnés con los defaults de fábrica sobre las mismas 23 ruedas),
y el 3,7 % a ≤ 5 s y ≤ 1 tick. La agresión que enciende una ola tiene 46 contratos de mediana y es
de 200 o más en el 29,5 % de los casos, así que **hasta 1 de cada 3 olas puede caer encima de una
pelotita del Big Trades** si lo tiene puesto en 200. Por eso la forma distinta no es un lujo.

Los topes por vela de los dos indicadores son **independientes**: ninguno sabe del otro.

## Lo que el dibujo NO puede prometer

El rombo marca el precio exacto de la operación que completó la cuenta y ese precio **no se mueve
nunca**. Pero la ventana entera recorrió **73 ticks (18,25 puntos) de mediana** en sus 10 segundos,
y la propia agresión que la enciende barrió el libro 5 ticks de mediana (p90 51 ticks; el 19,1 % no
barrió nada). El rombo dice **cuándo** pasó y a qué precio se completó. **No es un nivel, no es un
soporte y no es una flecha.**

---

## Los números que eligieron los defaults

| Ajuste | Valor | De dónde sale |
|---|---|---|
| Ventana | 10 s | 5, 10 y 30 s dan la misma conclusión; 10 s es la más pareja en densidad |
| Vara en la rueda | 1.500 contratos netos | percentil 98,908 (1,0923 % de 11.675.318 filas) — `r11_dib_01_salida.txt` bloque B |
| Vara fuera de la rueda | 800 | percentil 99,490 (0,5100 % de 6.257.614 filas) |
| Razón pura | 0,70 | razón p10 0,357 / p50 0,558 / p90 0,804 — `r11_dib_05_salida.txt` bloque B |
| Diámetro 12→26 px | 1,0 a 2,5 veces la vara | la ola crece hasta 1,30 veces la vara de mediana (p90 1,97, p99 3,10) |
| Juntar 2 ticks, tope 6 por vela | heredado de Absorción Viva | máximo medido: 2 olas por vela en M1 y M5, 6 en M30 |

**Por qué la vara va partida día/noche:** con 800 para las 23 horas salen 6,64 marcas por hora de
día (una sopa); con 1.500 para las 23 horas queda una marca cada 33 horas de noche (un desierto).
Partida da 1,06/h en la rueda y 0,25/h afuera.

**La vara se eligió por densidad, no por resultado.** En esta familia no hay resultado en ninguna
celda medida, así que no había dónde elegir a favor.

**Riesgo que hay que saber:** con la escala en veces la vara, una ola de noche de 1.000 contratos
se ve igual de grande que una de día de 1.950. Es a propósito, pero es una decisión, no una
medición.

**Los umbrales son de MNQ.** En MES no hay **nada** medido de esta familia. Puesto en un gráfico de
MES sin recalibrar va a sacar una cantidad de marcas que nadie contó.

## Las tres familias que no se dibujan

- **Contrapié** (rombo punteado, apagado): la ola con el precio yendo para el otro lado. Medido:
  **8 marcas en 23 ruedas** con 2 ticks en contra, 15 ruedas sin ninguna. Sobre las 241 olas el
  signo del avance coincide con el del delta en el **100,00 %** de los casos. Es una anécdota.
- **Traba** (cuadrado, apagado): mucho delta y el precio quieto. Medido: **0 marcas en 23 ruedas**
  con 800/400 y rango ≤ 8 ticks. **La trampa:** si en vez del RANGO se mide el movimiento NETO
  parece que existe (7 por sesión), pero esas ventanas tienen 52 ticks de rango de mediana, o sea
  13 puntos de ida y vuelta. El precio no estuvo quieto: se fue y volvió. La absorción medida **al
  nivel** sí existe y ya está en pantalla: es la pelotita de Absorción Viva.
- **Nido** (no se dibuja y **no hay ajuste para prenderlo**): todo el delta de la ventana
  concentrado en un solo precio. No se puede dibujar porque **no se sabe en qué precio ponerlo**:
  la cinta no dice a qué precio se operó cada contrato de una agresión que caminó el libro.
  Cambiando esa convención, el precio dominante cambia en el **95,9 %** de los casos y se corre 12
  ticks de mediana, p90 22 (`r11_dib_05_salida.txt` bloque E, n = 363). Dibujarlo rompería el
  requisito número uno.

Las cuatro se anotan **siempre** en el CSV, se dibujen o no. Si el registro dependiera de lo que
está prendido, arruinaría la prueba hacia adelante.

---

## Archivos

```
atas/DeltaVivo/
  DeltaNucleo.cs      lógica PURA: no conoce ATAS, no dibuja, no escribe archivos
  DeltaVivo.cs        la capa de ATAS: OnCumulativeTrade, latido de 250 ms, OnRender, CSV
  DeltaVivo.csproj    DLL propio, aparte de PythiaGexNiveles, de PythiaGexDos y de AbsorcionViva
  arnes/              consola: compila el MISMO DeltaNucleo.cs y lo corre sobre cinta grabada
```

El registro va a `%APPDATA%\ATAS\PythiaGex2\delta\delta-<fecha UTC>.csv`, con una fila cuando el
evento nace y otra cuando se rearma (con el máximo alcanzado).

## Por qué es una copia y no un enlace a AbsorcionViva

Estaban las dos opciones permitidas. Se copió, y las razones son estas:

1. **AbsorcionViva ya está instalado y corriendo** en la pantalla del operador. Un archivo
   enlazado invita a "arreglar" ahí adentro, y eso está prohibido en esta ronda. Copiando, es
   imposible por construcción.
2. Enlazado, `DeltaVivo.dll` exportaría tipos con el nombre completo `AbsorcionViva.Evento`,
   `AbsorcionViva.Franja`, iguales a los de la DLL ya instalada. Es legal en .NET, pero **nadie lo
   probó** contra el escáner de indicadores de ATAS.

**El precio de la copia, dicho de frente:** `Reloj` y `Franja` quedan en dos archivos. Un arreglo
del horario de verano hay que hacerlo en los dos, y si se arregla uno solo el indicador y su arnés
dejan de coincidir sin que nadie se entere. Por eso el arnés **vuelve a correr** la prueba de la
franja en vez de darla por heredada: `r11_p_02_franja.py`, 1.051.200 instantes contra
`America/New_York`, **100,000 %**.

**AbsorcionViva no se tocó**: ni un archivo, ni el README (que propone una ruta —agregar el núcleo
del delta a la batería del indicador de absorción— que hoy está prohibida y quedó vieja).

## Lo que se heredó de Absorción Viva y lo que se arregló

**Heredado tal cual:** el orden causal de los tres pasos por fila, el `OnCalculate` vacío (la
defensa contra colgar ATAS), el latido de 250 ms desacoplado, la búsqueda binaria de la vela por
hora (anda igual en M1, M5, M30, ticks, rango y footprint), el margen de 62 px del eje de precios,
la fusión a 2 ticks que **nunca corre una marca de su precio**, el tope por vela con aviso en el
rótulo, el freno del log, y el registro de todos los eventos se dibujen o no.

**Heredado y sin arreglar (va dicho, no escondido):** la fusión de marcas **se encadena** — si A y
B están a 2 ticks y B y C también, se juntan las tres, así que una marca fusionada puede abarcar
más de los 2 ticks que promete el nombre. No se corrigió porque cambiaría lo que se ve, y eso va
con captura antes y después.

**Arreglado acá y no allá:** el recorte de fichas. `AbsorcionViva.cs:410` hace un
`RemoveRange(0, 2000)` a ciegas al pasar de 6.000: con 24 marcas por sesión no muerde en 150
sesiones, pero con el delta muerde antes si alguien baja la vara (con 150 contratos una sola sesión
sacó 519 marcas), y lo que se perdería es la fila de "cierra" de eventos vivos, justo la que hace
falta para medir. Acá se poda por antigüedad y **solo lo que ya tiene sus dos filas escritas**.

**Lo que encontró la auditoría adversarial del 24-09 y quedó arreglado** (los cuatro son del
arreglo de arriba para adelante; los tres primeros no cambian ni un evento, y la paridad se volvió
a correr entera después de tocarlos):

1. *La poda no podaba con el registro apagado, y eso sí colgaba ATAS.* `Anotado` sólo se pone en
   `Registrar()`, y `Registrar()` sólo corre con `DltRegistrar` en true. Con el registro apagado
   ninguna ficha llegaba nunca a "completa", la poda no sacaba **ni una**, la lista crecía sin
   techo y —peor— a partir de las 6.000 fichas **cada operación de la cinta pagaba un recorrido
   entero de la lista con la llave tomada**, en MNQ hasta 426 por segundo. Ahora, con el registro
   apagado, la ficha se considera completa (nadie va a escribir esas filas nunca), hay un **techo
   duro de 40.000** que tira las más viejas y lo anota una vez en el log, y el barrido no se
   repite hasta que entren otras mil fichas.
2. *El freno del log se podía saltear con dos fallas distintas a la vez.* Escribía si el texto
   cambiaba **o** si habían pasado 60 s; con una excepción en el dibujo y otra en el latido
   alternándose, cada una llegaba con un texto distinto del anterior y las dos pasaban. Se agregó
   un piso de 2 s que no depende del texto.
3. *Una marca podía caer en la vela equivocada con el gráfico corrido hacia atrás.* `BarraDe()`
   busca sólo entre las velas visibles y devuelve la última cuando el instante cae después de
   ella, así que con `LastVisibleBarNumber` por detrás de la última vela un evento posterior
   quedaba pegado a la última visible. Casi siempre lo tapaba el recorte del eje — casi siempre no
   es nunca, y el requisito número uno no admite "casi". Ahora se descarta explícitamente.
4. *El núcleo no se defendía de una ventana de cero segundos.* Con `VentanaSeg = 0` el límite
   queda igual al instante de la fila, la propia fila se echa a sí misma y el índice se va más
   allá de lo escrito: bucle que no termina. La capa de ATAS ya recortaba el ajuste a 1 s como
   mínimo, pero el núcleo no puede depender de que quien lo llame se acuerde (el arnés lo llama
   directo). Dos candados en `DeltaNucleo.cs`, los dos no-operación con cualquier ventana válida.

**Nuevo y propio del delta:** el acumulador por precio con purga (en MNQ se visitan 3.828 precios
por sesión y el diccionario crecería mientras ATAS quede abierto; sacar una celda vacía no cambia
ningún resultado porque su acumulador vale exactamente 0), las colas monótonas del rango, y la
escala del diámetro en veces la vara.

---

## Paridad — el mismo código que va a correr en el gráfico

`laboratorio/dom/ronda11_delta/r11_p_01_paridad.py`

| Sesión | Filas de cinta | Eventos | Celdas iguales |
|---|---|---|---|
| 2026-08-05 (MNQ, **reserva**) | 1.025.615 | 1.680 | 31.920 / 31.920 |
| 2026-09-02 (MNQ) | 782.930 | 1.821 | 34.599 / 34.599 |
| 2026-09-18 (MNQZ6) | 683.299 | 1.795 | 34.105 / 34.105 |
| **TOTAL** | **2.491.844** | **5.296** | **100.624 / 100.624 → 100,000 %** |

Se corre con **dos juegos de parámetros**: los de fábrica y uno *sensible* con las varas bajas.
Con los de fábrica la traba saca **cero** eventos (está medido) y el contrapié saca cero o casi:
una paridad de "0 contra 0" no probaría nada. El juego sensible saca miles de eventos de las cuatro
familias por la misma maquinaria. **Lo que se prueba es el código, no la calibración.**

La sesión de la reserva se usa **solo para verificar código**, que es lo único para lo que la
reserva se puede tocar sin quemarla.

## Cómo se compila

```
dotnet build "atas\DeltaVivo\DeltaVivo.csproj" -c Release
dotnet build "atas\DeltaVivo\arnes\DeltaArnes.csproj" -c Release
```

Las dos: **0 errores, 0 advertencias**. La DLL queda en `atas\DeltaVivo\bin\Release\DeltaVivo.dll`.

**NO está instalado.** No se copió nada a `%APPDATA%\ATAS\Indicators`. La instalación la hace el
agente principal, y cuando la haga tiene que decir en el mismo mensaje que los umbrales son de MNQ
y que en MES no hay nada medido.

**Lo primero que hay que verificar en pantalla** no es el sentido de la marca sino la **ubicación**:
un rombo tiene que caer adentro de la vela del minuto en que pasó, no montado sobre la siguiente.
Prenderlo con Absorción Viva ya puesta, en MNQ, en M5, y sacar captura antes y después.

## Lo que sigue sin verificarse

Todo lo medido acá es **cinta grabada**. El indicador va a leer *streaming*. Que la punta y el
`Direction` lleguen igual por las dos vías es razonable —mismo tipo, mismo conector— pero **nadie
lo verificó todavía**, y no se puede hasta tener el mercado abierto. Es el mismo agujero que dejó
Absorción Viva. Y este dibujo nunca se vio en pantalla.
