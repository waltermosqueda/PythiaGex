# Pre-registro 3: ¿cuán temprano se puede detectar un giro? El límite estadístico, con modelos de predicción

Escrito el 2026-10-29 ~12:45 AR, **antes de entrenar ningún modelo**. Pedido: "sigue avisando muy tarde; tomarse en serio el
trabajo; calibrarla hasta los límites reales de predicción estadística". Lo que ya vi y puede sesgarme: rondas 1 y 2
(`CALIBRACION_INFORME.md`); que la 1.1 llegó 22,25 pts tarde en la suba de hoy 11:53-12:10 AR (gradual: la vuelta rápida no
la ve y la línea 60x4 está más lejos que la 10x3); y el antecedente `gatillo-cientifico` (MES 1 min, +3 antes que -3 en
10 min: logística AUC 0,57 fuera de muestra con niveles de gamma; sin niveles, nada).

## 1. La pregunta, en una ecuación

La corriente va para un lado d; E es el extremo del tramo (desde el último cambio) y c el cierre. El retroceso es
r = d (E - c). Darse vuelta ahora "acierta" si el precio sigue A puntos más en contra (llega a c - d A) **antes** de volver a
tocar el extremo E. Si el precio fuera un paseo al azar sin tendencia, la probabilidad es exactamente

  **p0 = r / (r + A)**   (ruina del jugador)

Para acertar más de la mitad de las veces hay que esperar r >= A: el retraso es el precio de la certeza y lo fija la
geometría, no el indicador. Solo se puede avisar antes si algún dato disponible en vivo predice mejor que p0.

## 2. Datos y filas

- MES 1 min DISEÑO (45 ses, <= 10-05) y EXAMEN (17 ses, 10-06..10-28) y MNQ 1 min (35 ses), con delta por vela; reglas de
  bloques y calentamiento del PREREGISTRO.md. Sesión de hoy (volcado de ATAS) solo para ilustrar.
- Filas: cada vela válida t con la corriente de referencia = tramos de ST(10,3) (E = extremo desde su último cambio, incluida
  t), con r > 0 y t distinta de la vela del cambio.
- **Etiqueta** (primaria): A = 10 pts en MES y 40 en MNQ; y = 1 si, después de t, el precio toca c_t - d A antes de tocar
  E (si en la misma vela pasan las dos, cuenta como 0); horizonte hasta 120 velas y nunca después del fin de la sesión;
  sin desenlace = 0. Secundaria: A = 2,5 ATR(60).

## 3. Rasgos (solo velas cerradas hasta t; todos se pueden calcular en ATAS en vivo)

r/ATR60; r en pts; velas desde E; velocidad (r/ATR60 por vela desde E); tamaño del tramo antes de E (E - cierre del
cambio, en ATR60) y su duración; ATR10/ATR60; ATR60 en pts; suma del delta desde E (excluida) hasta t con el signo del
retroceso, sobre el volumen medio de 60 velas; volumen desde E sobre el medio; de la vela t: cuerpo/rango con signo del
retroceso, posición del cierre en el rango, delta/volumen con signo; distancia al VWAP de sesión en ATR con signo del
retroceso; pendiente del VWAP en 10 velas; minutos desde las 18:00 NY y rueda (sí/no); distancia al máximo y al mínimo de la
sesión en ATR; cierres en contra en las últimas 5 velas.

## 4. Modelos (fijos, sin ajuste de hiperparámetros)

- **M0** geometría pura p0 = r/(r+A) (sin entrenar).
- **M1** logística sobre r/ATR60, (r/ATR60)², log(1 + r/ATR60) y velas desde E: "la geometría que tiene este mercado".
- **M2** logística con todos los rasgos (estandarizados, C = 1).
- **M3** HistGradientBoosting con todos los rasgos (max_iter 300, learning_rate 0,05, max_leaf_nodes 31,
  min_samples_leaf 200, l2 1,0, semilla 0).
- Entrenados en DISEÑO; medidos en EXAMEN y en MNQ (el modelo de MES aplicado a MNQ, todo en unidades de ATR salvo r en pts
  que se escala x 1/4; y aparte un modelo de MNQ con validación cruzada por sesiones, 5 grupos).
- Medidas: AUC y log-loss; AUC dentro de franjas de r/ATR60 ([0,1), [1,2), [2,3)) — lo que importa para avisar antes es
  separar giros reales de falsos CUANDO r todavía es chico.

## 5. De la predicción a la Corriente

Regla P(θ): con la corriente d y el extremo E del tramo propio, cambia cuando el modelo da p >= θ. θ en {0,35; 0,4; 0,45;
0,5; 0,55; 0,6; 0,65; 0,7}. Se simula vela a vela con sus propios tramos (E desde su último cambio) y se mide igual que en las
rondas 1-2, agregando las colas: puntos tarde medio, p75 y p90 en movimientos grandes (los que no se dan vuelta en 120
velas cuentan con el recorrido completo de esas 120 velas).

**Éxito** (en EXAMEN y en MNQ, los dos): con θ elegido en DISEÑO como el de cambios por sesión más cercano a la base
ST(10,3) sin pasarla, la regla con M2 o M3 (el de mejor AUC condicional en DISEÑO por validación cruzada) tiene, contra la
base, prueba de signos por movimiento p <= 0,05, p90 de puntos tarde menor, y cambios y vueltas falsas <= 1,10 x base. Si
además le gana a la misma regla con M1 (solo geometría) por >= 0,5 pts de media, hay **predicción**; si no, lo que se gana es
solo forma de la regla, no predicción.

Si nada pasa: se informa que el límite es el de la geometría (p0) con los datos que hay, con los números, y la Corriente se
ajusta solo a lo que sí pasó.

## Enmiendas

(ninguna al momento de escribir)

**Enmienda 1 (2026-10-29 ~13:05 AR, después de ver `pred_01_salida.txt`; post-hoc, más estricta a favor de los modelos).**
M0 (geometría pura, sin entrenar) tuvo el mejor AUC y log-loss en DISEÑO, EXAMEN y MNQ. Para no confundir "los rasgos no
predicen" con "el modelo no aprendió la geometría", se agregan M2+ y M3+: los mismos modelos con logit(p0) como rasgo
adicional (la geometría servida). Hay predicción si M2+ o M3+ mejoran el log-loss de M0 fuera de muestra en EXAMEN y en MNQ.

**Veredicto pre-registrado (13:15 AR):** M2+/M3+ no le ganan a M0 en MNQ (log-loss 0,4828 y 0,5090 contra 0,4821): **no hay
predicción** más allá de la geometría. La regla P(θ) con M2 (θ 0,4 elegido en DISEÑO) pasa en MES EXAMEN pero NO en MNQ
(86 cambios por sesión contra 35,5): **NO PASA**. Ver `pred_01_salida.txt`, `pred_01b_salida.txt`, `pred_01c_salida.txt`.

**Enmienda 2 (13:15 AR, post-hoc, escrita ANTES de correrla).** Lo que mostró la tabla: la regla M0 = "cambiar cuando el
retroceso desde el extremo del tramo llega a X puntos" (r/(r+A) >= θ equivale a r >= θA/(1-θ)) es la decisión óptima si
nada predice más que la geometría, y en la tabla tiene colas de retraso mucho menores que el SuperTrend; pero hace más
cambios en la rueda y menos de noche, y el criterio de ruido por sesión entera lo esconde. Prueba fijada:
- Familia RX: MES X en {6, 7, 8, 9, 10, 11, 12, 14} pts; MNQ X en {30, 35, 40, 45, 50, 55, 60, 70, 80}.
- Ruido = **en la rueda** (cambios y vueltas falsas de 5 pts / 20 pts MNQ por sesión, 9:30-16:00 NY), que es cuando opera.
- X elegido en DISEÑO = el menor X con cambios en la rueda <= base ST(10,3). Examen en EXAMEN con ese X; en MNQ el X se elige
  igual dentro de MNQ (misma regla, es otro instrumento) y se examina con validación por mitades de sesiones (elegido en la
  primera mitad cronológica, examinado en la segunda).
- Pasa si en cada examen: signos por movimiento p <= 0,05 a favor, p90 de puntos tarde menor que la base, y cambios y
  vueltas falsas en la rueda <= 1,10 x base.

**Veredicto Enmienda 2 (13:25 AR, `pred_02_salida.txt`):** MES PASA (X = 10 elegido en DISEÑO; EXAMEN: antes en 24, después
en 10, p 0,012; p90 16,35 contra 22,0; rueda: cambios 10,50 contra 10,06, falsas 3,31 contra 3,62). MNQ NO PASA (X = 70
elegido en la primera mitad; segunda mitad: antes en 37, después en 50). Se instala Corriente 1.2 con retroceso 10 pts como
modo por defecto en MES; en MNQ no se usa este X. Paridad del C# contra la regla del laboratorio: 0 de 84.364 velas.
