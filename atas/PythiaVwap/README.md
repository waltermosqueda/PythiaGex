# PythiaVWAP — VWAP Anclado para ATAS

Indicador propio para ATAS 8.0.14, pensado para ES/MES y NQ/MNQ.
Fuente: `VwapAnclado.cs`. Sale un solo archivo, `PythiaVwap.dll`.

---

## Antes que nada: ATAS ya trae un VWAP anclado

Hay que decirlo de frente porque cambia lo que este indicador tiene que
justificar. El VWAP nativo de ATAS (`ATAS.Indicators.Technical.VWAP`) **sí**
ancla donde vos quieras. Verificado por reflexión sobre el ensamblado:

- `AllowCustomStartPoint`, `StartBar`, `StartDate`, `StartKey` — ancla manual,
  incluso con una tecla.
- `Type` acepta `M15, M30, Hourly, H4, Daily, Weekly, Monthly, All, Custom`.
- `StDev`, `StDev1`, `StDev2` — tres desviaciones.
- `VolumeMode`: `Total`, `Bid`, `Ask`.
- `TWAPMode`: además del VWAP hace TWAP.

O sea: si lo único que querés es "VWAP desde este punto con tres bandas", **el
nativo alcanza y no hace falta esto**.

## Entonces, ¿qué agrega este?

Cinco cosas que el nativo no hace:

**1. Anclar solo, donde importa.** Además de sesión, semana y mes, ancla en el
**máximo más alto**, el **mínimo más bajo** o la **vela de mayor volumen** de un
rango que vos definís en sesiones. Así se usa el VWAP anclado en la práctica —
"desde el techo del jueves", "desde el piso del rango"— y ninguna plataforma lo
hace sola: en todas hay que ir a buscar la vela con el mouse cada vez. Acá,
cuando aparece un máximo nuevo, el ancla se muda sola.

**2. Cuatro bandas, no tres**, con multiplicadores decimales (1.5, 2.25, lo que
sea), y medidas en desviación estándar, en porcentaje o en puntos fijos.

**3. El canal pintado** entre bandas, como el `VWAPBandsPro2` de NinjaTrader.
Cada banda pinta su rango con poca opacidad; al superponerse queda el degradado,
más denso cerca del VWAP.

**4. La sesión anterior, de dos formas.** Acá hay una distinción que se presta a
confusión y conviene tenerla clara, porque son dos cosas distintas:

- **VWAP de sesiones anteriores** (hasta cinco días atrás): el VWAP con que
  cerró el lunes, el martes, el miércoles. Son días distintos.
- **Bandas de ayer**: el VWAP de la sesión anterior **con sus propias bandas**
  (+1, +2, +3 sigma y las de abajo), todas del mismo día.

Lo que el `VWAPBandsPro2` de NinjaTrader rotula `Prev VWAP +1 Day`,
`Prev VWAP Day`, `Prev VWAP -1 Day` es **lo segundo**: las bandas de un solo día,
el anterior. No son los VWAP de tres días distintos. Acá están las dos, y se
prenden por separado.

Todas se cortan en cada apertura, para que no quede una diagonal uniendo el
valor viejo con el nuevo — una diagonal así no es ningún nivel.

**5. Las etiquetas en columna.** Todas alineadas en la misma x, con el nombre a
la izquierda y el precio a la derecha, y el nivel prolongado por el hueco que
queda a la derecha de la última vela hasta su etiqueta — como el
`VWAPBandsPro2`. Cuando dos niveles quedan a menos de un renglón, se separan lo
justo **manteniendo el orden de precio**, y queda un conector punteado que dice
de qué línea es cada una. Una etiqueta corrida sin conector mentiría sobre dónde
está el nivel.

**6. El control de exactitud.** Esto es lo que más importa y va aparte.

---

## El control de exactitud

Casi todas las plataformas —NinjaTrader y TradingView incluidas— calculan el
VWAP ponderando **cada vela por un solo precio**, el típico `(H+L+C)/3`. Es una
aproximación: una vela de cinco minutos puede tener miles de contratos
repartidos en veinte precios distintos, y representarlos a todos por un número
inventado que quizá ni se operó.

ATAS guarda el **volumen negociado en cada precio adentro de cada vela**
(`IndicatorCandle.GetAllPriceLevels()`). Con eso el VWAP se puede calcular
**exacto**, idéntico al que daría contar tick por tick.

Prendiendo *Control de exactitud* aparece una caja con el mismo tramo medido por
los tres caminos al mismo tiempo, y la diferencia en ticks:

```
CONTROL DE EXACTITUD
footprint (exacto)   7644.28
vwap de vela         7644.28   +0.0 tk
precio tipico        7644.61   +1.3 tk
```

Se lee así: **el footprint es la verdad**. Si el "vwap de vela" no coincide con
él, hay un error de cálculo y hay que mirarlo. Y lo que se separe el "precio
típico" es, literalmente, **lo que está corrido el VWAP de NinjaTrader o de
TradingView** en ese mismo momento.

No es un detalle de purista: si operás un rebote contra el VWAP con stop de
cuatro ticks, un corrimiento de uno o dos ticks te cambia la operación.

---

## Los modos de cálculo

- **VWAP real de cada vela** (por defecto). Usa el VWAP que ATAS ya calculó tick
  a tick para esa vela, ponderado por su volumen. El VWAP acumulado sale
  **exacto** y es barato. La sigma queda apenas subestimada, porque representa
  la vela por un solo precio.
- **Volumen por precio / footprint.** Exacto también en la sigma, porque mide la
  dispersión real adentro de cada vela. Es el más pesado: recorre todos los
  niveles de precio de todas las velas.
- **Precio típico `(H+L+C)/3`.** Para comparar contra otras plataformas.
- **Cierre** y **ponderado `(H+L+C+C)/4`**, por completitud.

El volumen puede ser el total, o solo el ejecutado contra el bid o contra el
ask. Un VWAP de solo-ask es el precio promedio que pagó el comprador agresivo.

---

## Cómo está hecho por dentro

El truco que lo hace rápido: en vez de recalcular desde el ancla en cada barra,
guarda **sumas acumuladas desde la barra cero** de `precio×volumen`, `volumen` y
`precio²×volumen`. Cualquier tramo sale con dos restas:

```
suma(desde..hasta) = acumulado[hasta] - acumulado[desde-1]
```

Por eso **mover el ancla no cuesta nada**: es la misma resta con otro índice. Sin
esto, el modo "anclar al máximo" sería inusable, porque cada máximo nuevo obliga
a recalcular todo.

La desviación estándar sale de `σ² = Σvp²/Σv − vwap²`, todo en `decimal` (28-29
dígitos), que aguanta de sobra los ~10^15 que acumula una sesión sin perder
precisión.

Y es **idempotente por barra**: la barra en curso se recalcula en cada tick sin
duplicar nada.

---

## Instalar y recompilar

```bash
cd "PythiaGex/atas/PythiaVwap" && dotnet build -c Release
```

El DLL sale en `bin/Release/PythiaVwap.dll`. Copiarlo a
`%APPDATA%\ATAS\Indicators\` y reiniciar ATAS.

El archivo **no queda bloqueado** aunque ATAS esté abierto, así que se puede
pisar en caliente. ATAS lo detecta y avisa que se pueden recargar los
indicadores desde la barra de estado — el botón todavía no lo encontré, así que
por ahora el camino seguro sigue siendo reiniciar.

Los ensamblados de ATAS se referencian con `<Private>false</Private>`. Si se
copian, la plataforma carga dos veces los mismos tipos y no reconoce el
indicador.

### Si no dibuja

ATAS **se traga las excepciones de los indicadores** sin dejar nada en su log.
Por eso `OnCalculate` y `OnRender` escriben cualquier error a:

```
%APPDATA%\ATAS\pythiavwap-errores.txt
```

Si el indicador no dibuja, ese archivo es el primer lugar donde mirar.

---

## Lo que todavía no hace

- **No tiene alertas** cuando el precio toca una banda.
- **Una instancia = un ancla.** Para tener tres VWAP anclados a la vez hay que
  agregar el indicador tres veces y usar el campo *Prefijo* para distinguirlos
  en las etiquetas.
- Las bandas de los días anteriores solo se dibujan para **el día anterior
  inmediato**. Más atrás se vuelve una maraña que tapa el precio.
- El modo de anclar al extremo **repinta** cuando aparece un máximo o mínimo
  nuevo: el VWAP entero se muda. Es lo correcto conceptualmente, pero significa
  que la línea de ayer no es la que vas a ver mañana.

## Lo que este indicador NO dice

Dónde está el precio promedio ponderado y cuánto se aleja de él. **Nada más.**
No dice dirección, no dice si va a volver al VWAP, y una banda no es una señal.
Igual que el GEX describe el comportamiento esperado y no el rumbo.

---

# Segundo indicador: Delta VWAP/TWAP

Fuente: `VwapTwapDelta.cs`. Sale en el mismo `PythiaVwap.dll`.

Réplica del `LUZPREMIUM-VwapTwapDelta` de NinjaTrader, cuyo título completo es
`(MNQ 09-26 (2 Minute), 6/24/2026 9:35 AM, Day, Points, true, true, 9, true)` —
o sea: ancla, período, unidad, dos interruptores, suavizado 9, y otro
interruptor.

## Qué mide, en una frase

Resta dos promedios **del mismo tramo y del mismo precio**, que solo se
diferencian en cómo pesan cada vela:

- **VWAP**: pesa cada precio por el **volumen** que se operó ahí.
- **TWAP**: pesa cada vela por **tiempo** — todas valen lo mismo, haya operado
  mucho o nada.

La resta dice entonces una sola cosa, pero que ningún indicador de precio
muestra: **dónde estuvo el volumen respecto del recorrido**.

- **Positivo (verde)**: el volumen se hizo en la parte **alta** del recorrido.
  El dinero entró arriba.
- **Negativo (rojo)**: el volumen se hizo **abajo**.

Lo interesante es cuando no coinciden con el precio. Un tramo puede **subir** y
tener la resta **negativa**: el precio sube, pero el volumen se quedó atrás,
abajo. Eso es una subida sin respaldo de volumen, y no se ve mirando el VWAP
solo.

## Las dos decisiones de diseño que importan

**El mismo precio para los dos lados.** Si el VWAP usara el precio típico y el
TWAP el cierre, la resta mezclaría dos efectos —el cambio de precio y el cambio
de peso— y no se sabría cuál es cuál. Acá lo único que cambia entre los dos
promedios es el peso. Por eso el ajuste *Volumen* aclara que solo mueve el lado
VWAP: el TWAP no mira volumen, por definición.

**El suavizado nunca cruza el ancla.** El promedio móvil de N velas se corta en
la barra del ancla. Si no, el primer tramo de cada sesión vendría contaminado
con la sesión de ayer y mostraría un arrastre que no existió.

## Ajustes

Mismos modos de ancla que el VWAP anclado: sesión, semana, mes, fecha fija
(o tecla `D` + mouse), máximo / mínimo / vela de mayor volumen del rango,
N barras, todo el histórico.

- **Unidad**: puntos, ticks o porcentaje del TWAP.
- **Suavizado**: 9 velas por defecto, igual que la captura de referencia. 1 lo
  deja crudo.
- **Colores**, y se pueden apagar por separado el área y el contorno.

## Lo que NO dice

Dirección. Igual que el GEX y que el VWAP anclado, describe **cómo se comportó
el flujo**, no para dónde va el precio. Que la resta sea verde no es una señal
de compra.
