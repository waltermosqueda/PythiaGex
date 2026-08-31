# PythiaGex para ATAS

El proyecto empezó como un panel web. Después se bifurcó, y esta carpeta es la
otra mitad: un indicador que dibuja los niveles de gamma sobre el gráfico en
tiempo real de ATAS.

La división del trabajo es a propósito:

- **El panel es el mapa.** Baja la cadena de opciones de CBOE, calcula la
  exposición y publica los niveles. Se actualiza cada 15 minutos.
- **ATAS es el reloj.** Tiene el precio en vivo por Rithmic, tick a tick.

El indicador junta las dos mitades. Y hace algo que ninguno de los dos puede
solo: **la cadena dice dónde está la pared, el footprint dice quién está
ganando ahí.**

## Qué hay acá

| Carpeta | Qué es |
|---|---|
| `PythiaGexNiveles/` | El indicador. Seis archivos de código. |
| `_api/` | Herramienta que lee la API de ATAS por reflexión. |

### `PythiaGexNiveles/`

- **`NivelesGamma.cs`** — el indicador: ajustes, descarga, confluencia, dibujo.
- **`Contexto.cs`** — lo que se saca del footprint de ATAS: perfil de volumen,
  VWAP con bandas, delta acumulado, y el volumen y delta parados en cada nivel.
- **`Gatillos.cs`** — el order flow como disparador: imbalances apilados,
  prints grandes y divergencia de delta. **Solo se miran parados sobre un nivel
  publicado.** En el resto del gráfico son ruido, y ese es el filtro que evita
  que la pantalla se llene de flechitas sin sentido.
- **`Disparo.cs`** — el gatillo compuesto: la flecha que dice "acá, ahora".
- **`Bitacora.cs`** — el puente con el centinela. Escribe una línea cada cinco
  minutos con lo que ATAS vio en cada nivel.
- **`Estilo.cs`** — los enums de configuración visual.

## El bucle que faltaba

Hasta ahora el indicador calculaba el perfil de volumen, el VWAP, la absorción
y el order flow parado en cada nivel… y lo tiraba en cada cuadro. El centinela,
que mide si lo que prometemos pasa de verdad, sólo podía probar hipótesis
sacadas de la cadena de opciones.

O sea: **las hipótesis con más chance de ser ciertas eran justo las únicas que
no se podían ni formular.**

`Bitacora.cs` cierra ese bucle. Escribe en
`%APPDATA%\ATAS\PythiaGex\contexto\contexto-AAAA-MM-DD.jsonl` una línea por
foto con, para cada nivel: el volumen y el delta parados ahí, si hay absorción,
si cae sobre un nodo de alto o bajo volumen, la distancia al VWAP, el puntaje
de confluencia, y los tres gatillos. `centinela.py` lo levanta, lo copia al
repositorio y con eso puede por fin preguntar cosas como *«un muro que además
cae sobre un nodo de alto volumen, ¿aguanta más?»* y contestarlas con números.

Reglas de la bitácora, para que no ensucie nada:

- **Un campo que no se pudo medir sale `null`, nunca cero.**
- No bloquea el dibujo: escribe en otro hilo y se traga cualquier error.
- No lleva ninguna ruta personal adentro: por defecto usa la carpeta de datos
  de ATAS del usuario que lo corre.

## El gatillo, sin salir de la vista de velas

El footprint da el detalle de las entrañas pero, puesto como vista, tapa el
macro. La salida no es elegir: es **leer el footprint por dentro y sacar una
sola marca afuera**. `Gatillos.cs` recorre `GetAllPriceLevels()` precio por
precio, bid contra ask; `Disparo.cs` decide si eso amerita una flecha. El
operador nunca cambia de vista.

Tres condiciones, y **la primera no suma puntos: habilita**.

1. **Ubicación.** El precio tiene que estar dentro de la zona de un nivel
   publicado. Sin esto no hay disparo, por mucho flujo que haya. Es el filtro
   que mata la mayor parte del ruido.
2. **Dirección.** Tres cosas votan: imbalances apilados, divergencia de delta,
   y el delta agredido en la ventana reciente. Si se anulan entre sí no hay
   disparo — un nivel donde nadie está ganando claramente no es un gatillo,
   es una duda.
3. **Peso.** Recién ahí suman los amplificadores: el **esfuerzo**, la
   absorción, el print grande, la confluencia, y si el delta del día acompaña.

El **esfuerzo** cubre el caso que el print grande no ve: mil órdenes chicas
amontonadas en el mismo precio no dejan ningún print visible y pesan lo mismo
que una orden grande. Se mide como qué parte de todo el flujo reciente se
concentró en ese precio.

Contra el falso positivo hay tres frenos: un **umbral** que el operador elige,
un **enfriamiento** por nivel, y un **rearme** — además de esperar, el precio
tiene que alejarse del nivel antes de que ese nivel pueda volver a disparar.

Y lo que hace que nada de esto sea una opinión: **cada disparo se anota con su
puntaje y sus componentes**, y `centinela.py` mide después cuántos acertaron,
separados por puntaje. La regla es simétrica a propósito — ¿llegó 8 ticks a
favor antes que 8 en contra? Con esa tabla el umbral se elige mirando la
cuenta, no la intuición.

## Perfiles largos

Además del perfil de la sesión, ahora calcula el del **día anterior** y el de
la **semana**, con su VWAP semanal. El del día anterior trae una marca extra:
si el precio de hoy todavía no tocó el POC de ayer, ese nivel queda señalado
como **POC virgen** (*naked POC*), que es de los imanes más usados en Market
Profile — quedaron órdenes sin ejecutar en el precio donde ayer se acordó más.

Los tres perfiles se recalculan **sólo cuando arranca una sesión nueva**. Hasta
entonces no pueden cambiar, y recorrer miles de footprints en cada barra sería
tirar tiempo.

### `_api/`

Un programa chico que carga los ensamblados de ATAS con `AssemblyLoadContext`
y vuelca tipos y miembros. Se escribió para no adivinar la API. Con él se
descubrió que cada vela trae el footprint completo por precio, que es de donde
sale todo el contexto.

## Cómo compilar e instalar

ATAS 8.0.14.397 apunta a **net10.0-windows**. Trae el runtime pero no el SDK.

```bash
winget install Microsoft.DotNet.SDK.10
dotnet build atas/PythiaGexNiveles -c Release
```

Después:

1. Cerrar ATAS.
2. Copiar `bin/Release/PythiaGexNiveles.dll` a `%APPDATA%\ATAS\Indicators\`.
3. Abrir ATAS. En el log aparece `Indicators: Created library '...dll'`.
4. Gráfico → `Indicators` → buscarlo → **un solo clic** para seleccionarlo →
   el botón de abajo cambia a **`Add to chart`** → apretarlo → `Apply`.
   Doble clic no alcanza y arrastrarlo tampoco.
5. `Workspaces` → `Save`, si no se pierde al reiniciar.

Las referencias van con `<Private>false</Private>`: si se copian los DLL de
ATAS, la plataforma carga dos veces los mismos tipos y no lo reconoce.

## La regla que ordena todo

> **Si un número depende del precio, se recalcula en el indicador.
> Del feed solo viene lo que depende de la cadena de opciones.**

Se aprendió a los golpes. Tres veces apareció el mismo error en campos
distintos: la probabilidad de toque, la distancia del nivel cercano y el
gamma flip mostraban valores congelados del momento de la última corrida
mientras el precio ya se había movido. Llegó a verse un nivel a 160 ticks
diciendo "toque 100%".

## De dónde sale la probabilidad

No de un modelo. Bajo no arbitraje, la derivada del precio del call respecto
del strike **es** la probabilidad de terminar más arriba:

```
P(S_T > K) = -dC/dK
```

Se aproxima por diferencias centradas sobre precios reales de la cadena, y se
controla contra tres caminos más: el delta del contrato, Black-Scholes con la
IV del propio strike y con la IV at-the-money. La dispersión entre los cuatro
se publica: hasta 3 pp firme, hasta 8 razonable, más floja.

El indicador la **recalcula en vivo** por Black-Scholes contra el precio de
ahora y el tiempo que queda, y la multiplica por el cociente mercado/modelo
que publica el feed. El modelo aporta la dinámica; el cociente aporta el nivel
que paga el mercado. Sin eso, Black-Scholes solo daba 31,6 % donde el mercado
pagaba 42,0 %.

**Es probabilidad riesgo neutral, no del mundo real.** El precio de las
opciones lleva adentro la prima que se paga por cubrirse, así que las
probabilidades de caída salen más altas de lo que después ocurre. Sirve para
comparar niveles y leer qué descuenta el mercado. No es un pronóstico.

## Lo que está en la vista por defecto, y por qué

Va lo que **fuerza a alguien a operar**:

- **Régimen y Net GEX** — de qué lado del flip estás.
- **Cobertura por 1 %** — cuántos contratos tiene que operar la mesa.
- **Convexidad** — cuánto *más* va a operar si el precio se mueve 10 puntos.
  Dice si un movimiento se acelera o se apaga.
- **Charm pendiente y por hora** — el delta que hay que cubrir solo por el
  paso del tiempo. Es el motor del arrastre de la tarde en días de 0DTE.
- **Vanna por 1 % de IV** — lo que hay que cubrir si se mueve la volatilidad
  sin que se mueva el precio.
- **Los niveles con su probabilidad de toque**, recalculada en vivo.
- **El peso del 0DTE** sobre el total: si vence hoy el 40 % del libro, el imán
  tira fuerte y a las 17:00 desaparece de golpe.

Queda fuera, a propósito, lo que es contexto y no gatillo: theta es la caída
del valor de la opción y no obliga a nadie a operar; vega dice cuánto le
importa la volatilidad al libro pero tampoco fuerza nada; DEX es el stock de
delta, no el flujo; skew y term structure cambian lento. Los cuatro siguen
publicados y se ven en el modo **Completo**.

## Horarios

Todo el cálculo interno va en UTC. Lo que se muestra va en el reloj de la
máquina, convertido con la zona del sistema.

Con el operador en Argentina (UTC−3, sin horario de verano) y el mercado en
Chicago (UTC−5 en verano, **UTC−6 en invierno**), la diferencia es de dos
horas hoy y de **tres a partir de noviembre**. Por eso nunca se escribe a
mano.

En hora argentina, con el desfase de agosto: apertura **10:30**, liquidación
del 0DTE **17:00**, cierre del cash **17:15**, cierre de Globex **18:00**.

## Diagnóstico

Prendiendo "Incluir diagnóstico" en el tablero se ve, entre otras cosas, si un
indicador puede alcanzar el feed de opciones de ATAS. **No puede:** devuelve
`NotSupportedException`. Calcular el GEX sobre opciones de ES en tiempo real,
sin CBOE, no sale por ese camino.
