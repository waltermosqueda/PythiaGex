---
name: nq-libro-propio-pesa
description: "Para NQ el libro de NDX es solo 2 veces el propio, no 10 como SPX/ES: usar sólo NDX descarta un tercio del mapa, y los dos discrepan en los muros."
metadata:
  type: reference
---

Medido el 2026-09-03 con interés abierto oficial de CME, vencimientos a 7 días o
menos.

```
NDX   OI  9.650   gamma bruta 4.586 M
NQ    OI 13.224   gamma bruta 2.232 M      -> NDX es 2x, no 10x
```

NQ tiene **más interés abierto** que NDX; pesa menos porque el multiplicador de
NDX es 100 USD/punto contra 20 de NQ.

Para ES la decisión de armar el mapa desde SPX está bien porque **SPX es ~10
veces más grande** (ver [[cme-quikstrike]]). Para NQ esa proporción no se cumple
y usar sólo NDX **descarta un tercio del mapa**.

## Y los dos libros no coinciden

```
desde NQ (CME)    +wall 29600   -wall 29160   zero 29046   net +0,950 B
desde NDX (CBOE)  +wall 29100   -wall 29600   zero 29448   net +0,018 B
```

Los muros están **dados vuelta**: 29600 es el positivo en uno y el negativo en el
otro. No es error de cálculo — el signo sale de si domina el call o el put, no de
la volatilidad. Son dos clientelas con posicionamiento distinto.

## Lo que falta

Sumar los dos libros. Hoy no se puede automáticamente: a las opciones de NQ sólo
se llega con el volcado manual de CME (Akamai bloquea scripts) o abriendo un
gráfico de **NQ** —no MNQ— en ATAS para que el contrato entre al catálogo del
conector. Ver [[cadena-es-en-vivo-rithmic]].

**NQ sí tiene 0DTE y diarios en CME**, con la misma estructura que ES: futuro =
producto **146**, 7 grupos con semanales por día. El problema nunca fue que el
producto no existiera.

**Why:** se estaba por dar el mapa de NQ por equivalente al de ES, y no lo es.

**How to apply:** hasta que se sumen los dos libros, tratar los niveles de NQ
como más flojos que los de ES y decirlo. Ver [[costo-real-del-retraso]].
