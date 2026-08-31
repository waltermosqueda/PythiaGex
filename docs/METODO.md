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
el mismo número. Los separa la **base**, que es carry puro: tasa corta
menos dividendos, por el tiempo que falta hasta el vencimiento.

### El método obvio falla fuera de horario

Lo natural es:

```
forward = strike + call − put        (paridad put-call)
base    = forward − spot              <-- acá está el agujero
```

El problema: **el índice al contado deja de cotizar 16:15 ET, las opciones
no.** Fuera de horario `spot` es el cierre anterior, mientras las opciones
ya descuentan todo lo que pasó después. Restar uno del otro mezcla dos
momentos distintos y devuelve cualquier cosa.

Medido el 2026-08-31 a las 02:37 ET sobre SPX, sobre los mismos doce
strikes: por punto medio de bid/ask daba −21,6 y por último operado +9,5.
Treinta y un puntos de diferencia. La primera lectura fue culpar a los
market makers por correr las cotizaciones de noche. Era falso.

### Cómo se descubrió

El forward de un vencimiento de **cero días es el contado, por definición**:
no hay tiempo, no hay carry. Ese es el control.

| vencimiento | días | forward medido | vs `current_price` 7711,76 | carry teórico |
|---|---|---|---|---|
| 2026-08-31 | 0 | 7679,30 | −32,46 | 0 |
| 2026-09-03 | 3 | 7680,20 | −31,56 | +1,71 |
| 2026-09-10 | 10 | 7683,85 | −27,91 | +5,70 |
| 2026-09-18 | 18 | 7690,13 | −21,63 | +10,23 |

El forward de cero días daba 7679,30 contra un `current_price` de 7711,76.
Como ese forward **tiene** que ser el contado, los 32 puntos no eran base:
era el índice atrasado. Y la curva de forwards subía +10,8 en 18 días,
que es exactamente el carry teórico. La cadena estaba perfecta; el
índice era el que mentía.

### El método que se usa

No tocar el índice. Los dos forwards salen de la misma cadena, cotizada
en el mismo instante, así que el atraso se cancela solo:

```
base = forward(vencimiento trimestral) − forward(vencimiento más cercano)
```

Verificado el 2026-08-31 sobre SPX: **+10,83 medido contra +10,23 teórico**
(tasa 4,0 % menos dividendo 1,3 %, a 18 días). Sesenta centésimos de
diferencia entre una medición de mercado y un cálculo de pizarrón.

De paso queda gratis un dato que ningún tablero público muestra: el
**contado implícito**. Con el mercado cerrado, la cadena sabe dónde está
el índice de verdad aunque la pizarra siga clavada en el cierre.

### Cuándo NO creerle

La medición se marca firme solo si se cumplen las tres:

1. Doce strikes o más en cada vencimiento.
2. Dispersión entre strikes menor al 0,05 % del índice — relativa, porque
   tres puntos en SPX (7.700) es exigente y en NDX (29.000) es absurdo.
3. La base medida cae a menos de 3 puntos del carry teórico.

Medido el 2026-08-31: SPX pasa las tres. **NDX y RUT no** — NDX con seis
strikes utilizables y 8,65 puntos de dispersión, RUT con 5,45. Sus
opciones casi no cotizan de noche. El panel los marca en rojo y dice
por qué. Un nivel de NQ convertido con esa base no se dibuja.

**La base se mide todos los días.** Y **SPY × 10 está mal**: el ratio real
no es 10 exacto.
