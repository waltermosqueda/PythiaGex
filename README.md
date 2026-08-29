# PythiaGex

Exposición de opciones (GEX, DEX, vanna, charm, theta) calculada desde
**fuentes públicas, sin API key y sin costo**.

Pensado para operar futuros de índices — ES/MES y NQ/MNQ — leyendo el
posicionamiento de las opciones del índice subyacente.

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
```

Sin dependencias: corre con la librería estándar de Python 3.9+.

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
