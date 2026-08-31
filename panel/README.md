# Panel

Tablero web. Lee `datos.json`; si no lo encuentra, cae a `ejemplo.json`.

```bash
python cli.py SPX --panel     # genera panel/datos.json
cd panel && python -m http.server 8899
```

Abrir http://127.0.0.1:8899

## Qué muestra

**Franja de 12 indicadores** — Net GEX, Gamma Flip, Expected Move, Call Wall,
Put Wall, Net DEX, Net VEX, Net CHEX, Net TEX, Open Interest, Put/Call y Skew.

**Precio y exposición, con el eje vertical compartido.** A la izquierda el
recorrido intradía del subyacente; a la derecha el histograma por strike.
Los dos usan la misma escala de precio, así que un nivel se lee contra el
precio sin tener que traducir nada.

Ocho vistas del histograma — GEX, GEX por volumen, DEX, VEX, CHEX, TEX,
0DTE y OI — y tres modos: absoluto, calls contra puts, y cambio respecto
de la corrida anterior. Los lookbacks de 10, 20 y 30 minutos se superponen
como puntos.

**Curva de gamma** con el cruce por cero.

**Skew y term structure** lado a lado, cada uno con su lectura en una línea.

**Matriz strike × vencimiento**, con las celdas coloreadas por intensidad.

**Cadena completa** de catorce columnas, incluida la relación volumen/OI
que marca dónde entró posición nueva.

**Panel lateral** — niveles clave, concentración, posición nueva del día,
hottest chains, vencimientos e intradía.

## Detalle de lo anterior

**Franja de KPIs** — Net GEX, Gamma Flip, Expected Move, Net DEX, Net VEX,
Net CHEX, Net TEX, Open Interest y Put/Call ratio.

**Histograma por strike** con siete vistas: GEX · DEX · VEX · CHEX · TEX ·
OI · 0DTE. Verde amortigua, rojo amplifica. Línea ámbar el spot, punteada
el flip.

**Curva de gamma** con el cruce por cero.

**Cadena completa** — strike, distancia, GEX, GEX del 0DTE, DEX, VEX, CHEX,
OI de calls y puts, ratio e IV.

**Niveles clave** — Call Wall, Gamma Pin y Put Wall.

**Vencimientos** — los próximos con su OI, su GEX y barra proporcional.

Funciona en tema claro y oscuro según el sistema.
