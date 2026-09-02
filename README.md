# PythiaGex

> **Trabajo propio, todos los derechos reservados.** Este repositorio es visible
> solo por requisitos tecnicos de alojamiento. No se licencia para uso de
> terceros: ver [LICENSE](LICENSE). Que se pueda ver no significa que se pueda
> usar.

**Panel en vivo: https://waltermosqueda.github.io/PythiaGex/**

Corre solo en GitHub Actions cada 15 minutos en horario de mercado.
No depende de ninguna máquina encendida.

Exposición de opciones (GEX, DEX, vanna, charm, theta) calculada desde
**fuentes públicas, sin API key y sin costo**.

Pensado para operar futuros de índices — ES/MES y NQ/MNQ — leyendo el
posicionamiento de las opciones del índice subyacente.

## Las tres mitades del proyecto

Empezó como un panel web y se bifurcó. Hoy son tres cosas, todas acá adentro:

| Carpeta | Qué es |
|---|---|
| `pythiagex/` + `panel/` | El motor y el panel web. El **mapa**. |
| [`atas/`](atas/) | El indicador para ATAS. El **reloj**: junta los niveles con el precio en vivo de Rithmic. |
| [`conocimiento/`](conocimiento/) | Las 26 memorias, la bitácora y las instrucciones. Todo lo aprendido. |

El panel dice **dónde** está la pared. El indicador, con el footprint de ATAS,
dice **quién está ganando ahí**. Ningún tablero de GEX público puede dar lo
segundo.

---

## El radar: dominantes y BigTrades

```bash
python radar.py SPX                 # una corrida
python radar.py SPX --vigilar 60    # corre cada 60 s hasta que lo cortes
python radar.py SPX --dia 2026-08-31  # rearma esa sesión desde el cache
python radar.py SPX --rehacer       # reconstruye el historial guardado
```

Escribe `panel/datos/radar-SPX.json` (el panel), `panel/datos/atas/ES_radar.json`
(el indicador) y `panel/datos/bigtrades-SPX-<fecha>.json` (el historial del día).
La pantalla está en [`panel/radar.html`](panel/radar.html).

### Dominante

Una **dominante** no es el strike más grande del mapa: es el único que pasa
los tres filtros a la vez.

```
incentivo = tamaño × inmediatez × alcance

tamaño      cuánta gamma hay ahí, contra la mayor del mapa
inmediatez  qué fracción de esa gamma vence dentro del horizonte
alcance     probabilidad de que el precio la toque hoy
```

Los tres son necesarios y los tres se publican por separado, así que se puede
discutir cuál falla en vez de creerle a un número. Si uno da cero, la zona no
existe hoy por grande que sea el titular.

Esto es lo que corrige tres mentiras por omisión que ya estaban medidas en el
proyecto y nunca se habían juntado: que la gamma más grande suele vencer
dentro de tres semanas (el 7800 del 28 de agosto tenía el 69 % de su gamma en
el vencimiento del 18 de septiembre), que una pared a doscientos puntos no se
activa nunca, y que el interés abierto es de ayer.

El signo dice el **carácter**, nunca la dirección: gamma positiva **frena**,
gamma negativa **acelera**.

### BigTrades

CBOE no publica la cinta de operaciones, pero sí publica por contrato el
volumen **acumulado** del día, el último precio operado, las dos puntas y el
tick. Restando el volumen acumulado de dos corridas seguidas queda el volumen
que se operó entre las dos: la cinta agrupada en ventanas en lugar de tick a
tick. El lado sale de contra qué punta se cruzó.

Eso contesta la pregunta que el mapa solo no puede: **la pared que estoy
mirando, ¿la refuerzan o se la comen?** Si el cliente compra, la mesa queda
corta de gamma y el freno se afloja; si vende, se endurece.

> **El dato de CBOE llega 902 segundos tarde.** Medido, no estimado: 902
> segundos exactos en 14 de 14 corridas guardadas del 1 de septiembre, con
> desviación cero. Los BigTrades describen lo que pasó hace un cuarto de hora.
> Sirven para leer estructura y para estudiar una sesión terminada — **no
> sirven como gatillo de entrada en vivo**. Cada evento sale con su hora de
> mercado real calculada, no con la del archivo.

Lo que tampoco puede: separar una ventana donde hubo compras *y* ventas. El
delta de volumen las suma y el último precio solo describe el último cruce.
Por eso cada evento lleva su `confianza`, y cuando la ventana es larga o el
contrato operó mucho, baja y lo dice.

## Respaldo

Buena parte del trabajo vivía solo en el disco de una máquina: las memorias,
la bitácora, las guías, las instrucciones del proyecto. Ahora se sincroniza
con:

```bash
python respaldar.py
```

Tacha los identificadores de cuenta antes de copiar, porque este repositorio
es público.

## Verificación

```bash
python auditoria.py SPX        # 14 controles contra la cadena cruda
python auditoria.py --historia # la evolución de la rueda
```

El auditor **no reusa** las funciones del proyecto: recalcula cada número
desde la cadena con la fórmula escrita de nuevo, y recién después compara. Un
auditor que usa el mismo código que verifica no audita nada.

```bash
python cli.py SPX --dias 18 --panel
```

```
^SPX  spot 7711.76  (18d)  2026-08-28 23:50:48
  GEX -1.716B  DEX 180.972B  VEX 6.118B  CHEX -5.545B  TEX -110M
  regimen NEGATIVO   flip 7716.4   EM +/-41.5
  OI 1,878,668  (C 590,132 / P 1,288,536)  P/C 2.183
  call wall 7750.0  put wall 7675.0  pin 7710.0
```

---

---

## La fuente

CBOE — la bolsa que lista SPX — publica la cadena completa en un CDN abierto:

```
https://cdn.cboe.com/api/global/delayed_quotes/options/_SPX.json
```

Devuelve **28.892 contratos** con `open_interest`, `gamma`, `delta`, `iv`,
`vega`, `theta`, `rho` y `volume` por contrato, más el spot del índice.

Símbolos verificados: `_SPX` `_NDX` `_RUT` `_VIX` `SPY` `QQQ` `IWM` `DIA`
(los índices llevan guion bajo adelante, los ETF no).

**La gamma viene calculada por la bolsa.** No hay que despejar la volatilidad
implícita ni correr Black-Scholes por strike, que es donde se cuelan los
errores más caros.

---

## Qué calcula

| Métrica | Qué mide |
|---|---|
| **GEX** | exposición gamma — cuánto tiene que cubrir la mesa por cada 1 % de movimiento |
| **GEX por volumen** | lo mismo pero sobre la actividad del día, no el inventario heredado |
| **DEX** | exposición delta — el sesgo direccional del posicionamiento |
| **VEX** (vanna) | cuánto cambia la delta por cada 1 % de cambio de volatilidad |
| **CHEX** (charm) | cuánto cambia la delta por día que pasa |
| **TEX** (theta) | decaimiento temporal acumulado |
| **Gamma Flip** | el precio donde el régimen pasa de amplificar a amortiguar |
| **Call Wall / Put Wall** | los strikes que más frenan hacia arriba y hacia abajo |
| **Major Positive / Negative** | los extremos de gamma del complejo |
| **Gamma Pin** | el strike grande más cercano al precio |
| **Expected Move** | el rango de 1 sigma implícito en la cadena |
| **Max Change** | los strikes donde más se movió el GEX entre corridas |
| **Skew 0DTE** | IV de calls contra IV de puts alrededor del dinero |
| **Matriz Strike × DTE** | de qué vencimiento viene la gamma de cada strike |
| **Concentración** | qué porcentaje de un nivel pesa hoy y cuánto es inventario lejano |
| **Skew** | cuánto más caro está el seguro de baja que el de suba |
| **Term structure** | si el miedo está en el corto o en el largo plazo |
| **Superficie de IV** | volatilidad por strike y por vencimiento a la vez |
| **Hottest chains** | los contratos con más volumen sobre interés abierto |
| **Posición nueva** | strikes donde hoy se operó más de lo que había vivo |
| **Lookbacks** | el estado de hace 10, 20 y 30 minutos, para superponer |
| **Intradía** | evolución de las métricas a lo largo de la jornada |

---

## Corre solo

Dos workflows lo mantienen vivo sin intervención:

**`actualizar.yml`** — cada 15 minutos entre las 13:00 y las 21:00 UTC de
lunes a viernes (cubre 9:30–16:00 ET en verano y en invierno). Calcula SPX,
NDX y RUT, guarda el histórico y commitea. Además hay una corrida temprana
que deja el mapa del día antes de la apertura.

**`pages.yml`** — publica el panel cada vez que cambian los datos.

Los dos se pueden disparar a mano desde la pestaña Actions.

| Símbolo | Panel |
|---|---|
| SPX | https://waltermosqueda.github.io/PythiaGex/?s=SPX |
| NDX | https://waltermosqueda.github.io/PythiaGex/?s=NDX |
| RUT | https://waltermosqueda.github.io/PythiaGex/?s=RUT |

Y el JSON crudo queda servido en `datos/_SPX.json`, listo para que lo
consuma un indicador de ATAS o cualquier otra cosa.

---

## Estructura

```
pythiagex/
  fuentes.py      bajada y cache comprimido del crudo
  griegas.py      vanna y charm (lo que CBOE no entrega)
  exposicion.py   GEX, DEX, VEX, CHEX, TEX por strike y vencimiento
  niveles.py      flip, walls, pin, skew, max change, alertas
  base.py         conversión de índice a precio de futuro
cli.py            línea de comandos
panel/            tablero web
docs/             método, fuentes y hoja de ruta
```

---

## Uso

```bash
python cli.py SPX                  # vista de 18 días
python cli.py SPX --venc latest    # solo el vencimiento más cercano
python cli.py SPX --venc next      # solo el siguiente
python cli.py SPX --dias 90        # todo el complejo
python cli.py NDX --base 12.28     # convierte los niveles a precio de futuro
python cli.py SPX --panel          # además escribe panel/datos.json
python cli.py SPX --matriz         # agrega la matriz strike × vencimiento
```

Sin dependencias: corre con la librería estándar de Python 3.9+.

---

## La matriz Strike × DTE

Un strike puede tener mucha gamma y no mover nada hoy, si esa gamma vence
dentro de tres semanas. La matriz cruza cada strike contra cada vencimiento
y responde: **este nivel, ¿cuándo pesa?**

```
concentracion (donde pesa cada nivel):
      7800     4172M    69% en 2026-09-18 (21d)
      7900     3463M    65% en 2026-09-18 (21d)
      7715      774M    62% en 2026-08-31 (3d)
```

Los primeros dos son los muros más grandes del mapa, pero casi toda su
gamma está en el vencimiento trimestral: hoy son decorativos. El tercero
es mucho más chico y pesa esta semana.

---

## Lo que este proyecto NO afirma

**No es una estrategia y no predice dirección.** El GEX describe cómo se
espera que se comporte el movimiento — rango o tendencia, compresión o
expansión — nunca hacia dónde.

**Un nivel no es una entrada.** Dice dónde mirar. Si entrar o no lo decide
lo que pase cuando el precio llegue ahí.

**La convención de signo es una asunción.** La fórmula estándar supone que
los dealers están long calls y short puts. Cuando el flujo dominante se
invierte, el signo miente. Está explicado en [docs/METODO.md](docs/METODO.md).

---

## Licencia

MIT.
