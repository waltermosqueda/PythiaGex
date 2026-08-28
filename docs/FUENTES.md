# Fuentes de datos evaluadas

Investigación del 28 de agosto de 2026.

## La elegida: CDN público de CBOE

```
https://cdn.cboe.com/api/global/delayed_quotes/options/_SPX.json
```

Sin API key, sin login, sin tarjeta. HTTP 200 directo.
Medido: **28.892 contratos de SPX en 2,39 segundos**.

**Campos por contrato:** `option` (símbolo OCC), `open_interest`, `gamma`,
`delta`, `vega`, `theta`, `rho`, `iv`, `volume`, `bid`, `ask`, `theo`,
`last_trade_price`, `last_trade_time`, `open`, `high`, `low`, `change`.

**En la raíz:** `data.current_price` (spot del índice) y `timestamp`.

**Símbolos verificados**

| Símbolo | Contratos | Para |
|---|---|---|
| `_SPX` | 28.892 | ES / MES |
| `_NDX` | 15.322 | NQ / MNQ |
| `_RUT` | 12.144 | RTY |
| `_VIX` | 1.520 | volatilidad |
| `SPY` `QQQ` `IWM` | 13.514 / 11.882 / 5.572 | ETF |

### Limitaciones

**Es `delayed_quotes`** — 15 minutos de retraso en horario de mercado.
Importa poco: el open interest es de ayer en todas las fuentes. Lo único
atrasado es el spot.

**No es un contrato público con SLA.** Es el CDN que CBOE usa para su propia
web y puede cambiar sin aviso. Por eso el motor guarda siempre el crudo.

**No da ES nativo.** Da SPX. La conversión necesita la base.

---

## Validación cruzada contra CME

El settlement oficial de CME para opciones de ES es una fuente
independiente. Comparación del 28 de agosto:

| Vencimiento | CBOE (SPX) | CME (ES) |
|---|---|---|
| 31-ago | OI 290.952 · GEX +1.714 M | OI 302.870 · GEX +1.863 M |
| 4-sep | OI 238.750 · GEX −1.613 M | OI 176.816 · GEX −346 M |

Dos bolsas distintas, misma estructura. La fuente es buena.

**CME sirve como control, no como reemplazo:** publica una vez por día a
las 23:55 CT, corre Akamai Bot Manager y bloquea por IP si se le pide de
más. Una descarga diaria como máximo.

---

## Las descartadas

| Fuente | Costo | ¿SPX? | Por qué no |
|---|---|---|---|
| **Tradier** | cuenta gratis / Pro USD 10 | sí, con SPXW | **plan B real** — griegas por ORATS, requiere cuenta de brokerage |
| EODHD | USD 29,99/mes | **no** | solo acciones y solo cierre del día |
| FlashAlpha | free 5 req/día · SPX desde USD 63 | en plan pago | el free tier no incluye índices |
| Polygon / Massive | USD 79–99/mes | sí | pagar por lo que CBOE regala |
| InsightSentry | USD 25/mes | sin confirmar | documentación insuficiente |
| Intrinio | enterprise | sí | fuera de escala |

---

## Sobre ATAS

`github.com/AtasPlatform/Indicators` es para escribir indicadores en C#
(.NET 10) que corren **dentro** de la plataforma. No es una API de datos
hacia afuera.

Eso sirve para lo contrario, que es mejor: **un indicador que consuma este
proyecto por HTTP y dibuje las líneas solo sobre el gráfico.**
