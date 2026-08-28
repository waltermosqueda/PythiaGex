# Panel

Tablero web. Lee `datos.json`; si no lo encuentra, cae a `ejemplo.json`.

```bash
python cli.py SPX --panel     # genera panel/datos.json
cd panel && python -m http.server 8899
```

Abrir http://127.0.0.1:8899

## Qué muestra

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
