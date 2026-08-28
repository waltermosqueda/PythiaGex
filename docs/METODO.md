# El método, y dónde puede fallar

## De la cadena al nivel

**1. Bajar la cadena.** CBOE publica cada contrato con su `open_interest`,
su `gamma` y su `iv`. Se guarda el crudo comprimido antes de tocarlo: si
cambian el formato, el histórico sobrevive.

**2. Calcular la exposición de cada strike.**

```
GEX = gamma × open_interest × 100 × spot² × 0,01 × signo
```

El 100 es el multiplicador de las opciones de índice. El `spot²  × 0,01`
convierte a dólares por cada 1 % de movimiento. El signo es `+1` para calls
y `−1` para puts.

**3. Sumar y mover el precio.** El **gamma flip** no es el strike donde
cambia el signo — los signos saltan entre strikes vecinos. Es el precio
donde la **suma de todos los strikes** cruza cero. Por eso hay que repreciar
la gamma a cada nivel y volver a sumar.

---

## El punto que casi nadie explica: la convención de signo

La fórmula de arriba pone `+1` a los calls y `−1` a los puts. **Eso es una
convención, no una ley.**

De qué lado queda el dealer depende de qué hizo el cliente:

| El cliente | El dealer queda | Su gamma | Efecto |
|---|---|---|---|
| compra call | short call | short | **amplifica** |
| vende call | long call | long | **amortigua** |
| compra put | short put | short | **amplifica** |
| vende put | long put | long | **amortigua** |

El mismo call puede amortiguar o amplificar según quién lo compró.

**La fórmula estándar funciona porque asume un flujo dominante:** que los
clientes venden calls (cobran prima sobre acciones que ya tienen) y compran
puts (se protegen de caídas). Con ese supuesto los dealers quedan long calls
y short puts, y ahí sí el call amortigua y el put amplifica.

Es la convención de toda la industria y acierta la mayoría de las veces.
**Pero cuando el flujo se da vuelta** — un día de euforia donde todos compran
calls — la fórmula dice "amortigua" y la realidad amplifica. Ahí es cuando
parece que el GEX "falla". No falla: se rompió el supuesto.

**Este proyecto no puede inferir el lado.** Para eso haría falta clasificar
cada transacción por su agresor, tick a tick, y eso requiere un feed de
trades que la bolsa no publica gratis.

---

## Vanna y charm

CBOE entrega delta, gamma, vega, theta y rho. **Vanna y charm no**, así que
se derivan desde la IV de cada contrato:

```
vanna = −φ(d1) · d2 / σ          (por cada 1% de cambio de volatilidad)
charm = −φ(d1) · (2rT − d2·σ√T) / (2T·σ√T)     (por día)
```

**La normalización importa.** Sin dividir, el charm lleva un factor `1/T`
que explota cerca del vencimiento: en una prueba real dio −2.030 B contra
un GEX de −1,7 B, o sea mil veces más grande y completamente ilegible.
Normalizado por día queda en −5,5 B, comparable con el resto.

---

## Las tres verificaciones

**El sello de tiempo antes que el número.** Todo dato tiene una hora. Si
tiene más de 30 minutos, decirlo antes de usarlo, no después.

**Dos fuentes o media posición.** Si dos cálculos independientes dan el
gamma flip a menos de 15 puntos, el nivel es sólido. Si difieren más, ese
día no hay estructura clara.

**El interés abierto es de ayer.** En CBOE, en CME y en todos lados: la
cámara lo consolida de noche. Intradía el GEX se mueve solo por precio y
por volatilidad, nunca porque entró posición nueva.

---

## La base: de índice a futuro

Los niveles salen en SPX (el índice) pero se opera ES (el futuro). No son
el mismo número.

```
forward = strike + call − put        (paridad put-call)
base    = forward − spot
ES      = SPX + base
```

Se mide sobre el vencimiento que coincide con el del futuro, con doce
strikes. **Si los doce dan casi el mismo forward, la cadena es real.**

**La base se mide todos los días.** En cuatro días de agosto de 2026 se
movió de 21,6 a 12,28 — nueve puntos. Y **SPY × 10 está mal**: el ratio real
no es 10 exacto.
