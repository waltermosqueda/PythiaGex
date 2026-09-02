---
name: radar-dominantes-bigtrades
description: "El radar: qué es una dominante, cómo salen los BigTrades del volumen acumulado, y qué quedó sin verificar en pantalla."
metadata: 
  node_type: memory
  type: project
  originSessionId: 5abdaa85-a198-45f5-bf5a-ffcdc763812f
  modified: 2026-09-02T21:26:24.135Z
---

Construido el 2026-09-02 a partir de siete videos de GAMMAlito (chalitotrader),
que compra datos a GEXbot y dibuja sobre TradingView y NinjaTrader. Acá el
dato se recalcula desde la cadena cruda y sale gratis.

## Qué es una dominante

No es el strike más grande. Es el que pasa **los tres filtros a la vez**:

```
incentivo = tamaño × inmediatez × alcance
```

Los tres se publican por separado para poder discutir cuál falla. Si uno da
cero, la zona no existe hoy.

Corrige tres mentiras por omisión que el proyecto ya tenía medidas sueltas:
la gamma más grande suele vencer en tres semanas, una pared lejana no se
activa nunca, y el interés abierto es de ayer. Ver [[calcular-gex-propio]].

El signo dice el **carácter**, nunca la dirección: positiva frena, negativa
acelera. Que una de las cuatro casillas venga vacía es un dato del día, no un
fallo: el 1 de septiembre no había ninguna zona de freno debajo del precio, o
sea que nada amortiguaba una caída. Eso se dice con palabras.

## Cómo salen los BigTrades

Restando el `volume` acumulado de dos corridas de la cadena. El lado sale de
comparar el `last_trade_price` contra las puntas. Es la cinta agrupada en
ventanas en vez de tick a tick.

`docs/METODO.md` decía que el lado del agresor era imposible sin un feed
pago. Era cierto para la cinta; no para el volumen acumulado.

**Control de que el clasificador no está sesgado:** sobre el 31 de agosto
entero dieron 108 compras contra 87 ventas y 34 sin lado. Un reparto parejo.
Si saliera 200 a 5, el clasificador estaría roto.

**El límite duro es el retraso:** [[retraso-cboe-902s]]. Quince minutos. No es
gatillo en vivo y el archivo lo dice en cada corrida.

## Dónde está cada cosa

- `pythiagex/dominantes.py`, `pythiagex/bigtrades.py` — el motor
- `radar.py` — el ejecutable. `--dia AAAA-MM-DD` rearma una sesión terminada
  desde el cache, que es justo para lo que el dato retrasado sirve
- `panel/radar.html` — la pantalla
- `atas/PythiaGexNiveles/RadarDominantes.cs` — el indicador

El mapa de la página es una **mariposa alrededor del eje de precio**: a la
izquierda la estructura (gamma por strike), a la derecha el flujo (prima
grande de hoy en ese mismo strike). Empezó siendo una dispersión sobre un eje
de tiempo, como la dibuja todo el mundo, y con muestreo a rachas eso retrata
el muestreo, no el día.

## Lo que quedó SIN verificar

**El indicador nunca se vio dibujando en pantalla.** Compila limpio, el
contrato del JSON está verificado campo por campo (35 campos, ninguno
faltante) y la misma lógica y los mismos datos se verificaron en el navegador.
Pero el código de dibujo de ATAS no se ejecutó nunca.

Falta: reiniciar ATAS, `Indicators` → buscar **PythiaGex - Radar de
Dominantes** → un solo clic → **Add to chart** → `Apply` → guardar workspace.
Ver [[compilar-indicadores-atas]].

## Dos bugs que solo apareció el cruce del contrato

Los dos dejaban el JSON viéndose perfecto y el indicador dibujando nada:

1. `amplifica` y `amortigua` se codificaban las dos con su letra inicial — la
   misma letra para efectos opuestos.
2. Los BigTrades salían con el precio de futuro en `null`, porque el historial
   lo escriben corridas que no miden la base, y el indicador descarta los
   eventos sin precio de futuro.

**Why:** verificar que un archivo "se ve bien" no verifica nada. Hay que
cruzar lo que el productor escribe contra lo que el consumidor lee, campo por
campo.

**How to apply:** antes de dar por terminado cualquier puente entre dos
programas, extraer los nombres de campo que uno pide y compararlos contra los
que el otro emite. Ver [[verificar-yo-no-el-usuario]] y
[[auditoria-punta-a-punta]].
