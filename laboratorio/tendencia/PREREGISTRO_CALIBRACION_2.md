# Pre-registro 2: mecanismos nuevos para que la Corriente avise antes sin sumar ruido (MES 1 min + examen en MNQ)

Escrito el 2026-10-29 ~12:35 AR, **antes de calcular ningún estado de estos candidatos**. Pedido del operador después de la
ronda 1 ("trabajá, resolvelo, llevalo a los límites, sin excusas"). Lo ya visto y que puede sesgarme, declarado:
la ronda 1 entera (`CALIBRACION_INFORME.md`): las 80 versiones de "un solo dial" caen en una misma curva retraso/ruido, la
pre-registrada no pasó, y post-hoc ST(60,3) llegó algo antes con el mismo ruido en DISEÑO y en EXAMEN. Por eso el EXAMEN de
MES ya no es virgen para mí: se agrega un **segundo examen en otro instrumento** (MNQ 1 min, 35 sesiones 08-03..09-18, nunca
usado para calibrar). No se edita después de la primera corrida; cambios en "Enmiendas" con fecha, marcados post-hoc.

## 1. La idea (por qué estos tres mecanismos)

La ronda 1 movió un solo dial: a qué distancia va la línea (multiplicador) y con qué se mide la vela (ATR). Esa distancia
es la misma para un arranque violento que para un serrucho lento, y por eso todo cae en una curva. Los arranques que el
operador se pierde son **rápidos** (20 pts en <= 30 min); el serrucho que no quiere ver es **lento o corto**. Los tres
mecanismos dan sensibilidad distinta según la forma del movimiento, no una distancia única:

- **G1 escala que no se infla con el propio movimiento**: medir la vela con datos que el arranque todavía no contaminó.
- **G2 línea que se acerca a medida que la tendencia se estira** (idea del Parabolic SAR): después de un tramo largo, la
  vuelta se detecta antes; en el serrucho (tramos cortos) la línea sigue lejos.
- **G3 vuelta rápida por velocidad**: además de la línea de siempre (que puede quedar más lejos), la Corriente se da vuelta
  si el precio retrocede v ATR desde el extremo del tramo en j velas o menos. Variante G3d: además, el delta acumulado de
  esas velas tiene el signo del retroceso (agresión real en contra).

## 2. Datos y medidas

- MES 1 min: igual que `PREREGISTRO_CALIBRACION.md` (DISEÑO <= 10-05, 45 ses; EXAMEN 10-06..10-28, 17 ses; días volátiles =
  tercio de mayor rango de la rueda en cada tramo). Delta por vela de `ronda3/velas` + caches (columna `delta`), unido por `t`.
- MNQ 1 min: `datos/velas_MNQ_m1.parquet` (ajustado por el roll; reglas del PREREGISTRO.md) + delta de
  `dom/ronda4/datos/MNQ_m1.parquet` por `t`. Movimientos grandes MNQ: 80 pts en <= 30 min (PREREGISTRO.md sección 4).
- Retraso: igual que la ronda 1 (puntos tarde desde el extremo; infinito si no en 120 min). También movimientos de 10/15.
- Ruido (por sesión con todas sus velas válidas): cambios; vueltas falsas **5 pts** (MNQ 20 pts); y vueltas falsas
  **normalizadas**: el tramo nunca se aleja 2 ATR(60) a favor (ATR(60) de la vela del cambio). La normalizada controla el
  artefacto "en momentos volátiles cualquier cambio parece bueno porque todo se mueve más".
- Además, en la rueda (9:30-16:00 NY), cambios y vueltas falsas por sesión (lo que el operador ve operando).

## 3. Candidatos (114, fijos). A = ATR de Wilder; banda y trinquete de SuperTrend como en `t_lib.det_supertrend`

- **G1** (20): A = ATR(10) de k velas atrás, k en {5, 10, 20} x m en {2,5; 3; 3,5}; A = mediana del TR de las últimas L
  velas, L en {30, 60} x m en {3; 3,5; 4; 4,5}; A = mín(ATR(10), ATR(60)) x m en {2,5; 3; 3,5}.
- **G2** (36): ATR(n), n en {10, 60}; m_t = máx(m_min, m0 - a x e_t), con e_t = (extremo a favor desde el último cambio -
  cierre de la vela del cambio) / A_t en unidades de ATR (0 en la vela del cambio); m0 en {3; 3,5; 4} x m_min en {1,5; 2}
  x a en {0,1; 0,25; 0,5}.
- **G3** (54): SuperTrend(n, m) con n en {10, 60} x m en {3; 3,5; 4}, más la vuelta rápida: con la corriente arriba, si
  (máximo desde el último cambio, incluida la vela actual) - cierre >= v x A_t y ese máximo se hizo hace j velas o menos,
  cambia a abajo (al espejo con el mínimo); v en {1,5; 2; 2,5} x j en {3, 5, 8}. Al cambiar por cualquier vía, el extremo
  se reinicia con la vela del cambio; las bandas siguen su trinquete normal.
- **G3d** (4): G3 con n 60, m 3,5, v en {1,5; 2}, j en {5, 8}, y la vuelta rápida exige además que la suma del delta desde
  la vela del extremo (excluida) hasta la actual tenga el signo del retroceso (negativa para cambiar a abajo).
- Referencias (no compiten): B = ST(10,3) y ST(60,3) y la curva F1 de la ronda 1 (ST(n,m), n en {10, 60}).

## 4. Elección en DISEÑO

- **Domina a B** si en DISEÑO: mediana de puntos tarde (20 pts) <= B - 1,0 pts, y cambios, vueltas falsas 5 pts y vueltas
  falsas normalizadas por sesión <= B (las tres).
- **Elegido C** = el dominante de menor mediana de puntos tarde; empate: menor en días volátiles; luego menos vueltas
  falsas normalizadas. Si no hay dominante: se informa "ningún mecanismo rompe la curva por 1 pt" y no se cambia nada por
  esta ronda.
- Informativo: **ganancia contra la curva** = (retraso de la curva F1 de DISEÑO interpolado linealmente en la cantidad
  de cambios del candidato, usando la envolvente inferior de ST(n,m) n en {10,60}) - retraso del candidato. Los 10 de más
  ganancia se informan con todas sus medidas.

## 5. Examen doble (C contra B)

- **MES EXAMEN** (17 ses): prueba de signos unilateral por movimiento (puntos tarde B - C, mismos movimientos; infinito >
  número; empates afuera) p <= 0,05 a favor de C, y cambios, vueltas falsas 5 pts y normalizadas <= 1,10 x B.
- **MNQ 1 min** (35 ses, mismos parámetros de C, sin tocar nada): lo mismo (signos p <= 0,05; ruido <= 1,10 x B).
- Pasa si cumple los dos. Si pasa: Corriente 1.1 con C por defecto (propiedad nueva para que el .ws no lo pise), avisando
  y con captura antes/después. Si no pasa: no se cambia el defecto por esta ronda.

## 6. Lo que NO mide

Ninguno anticipa la dirección (INFORME.md parte a). Mide cuán rápido se pone del lado de un movimiento que ya ocurrió y
cuántas veces cambia de color sin motivo. No es una estrategia ni promete ganancia.

## Enmiendas

(ninguna al momento de escribir)

**Enmienda 1 (2026-10-29 11:45 AR, DESPUÉS de los resultados; decisión post-hoc, declarada).**
- Veredicto pre-registrado (`cal_03b_salida.txt`): C = G3 ST(60,4) v2,5 j3 **NO PASA** el examen doble: MNQ sí (115 contra
  81 movimientos, p 0,009; ruido dentro del 110 %), MES EXAMEN no (24 contra 16, p 0,13; ruido dentro del 110 %).
- Examen post-hoc de lo informativo pre-registrado (`cal_03c_salida.txt`): la familia G3 entera le gana a la curva F1 fuera
  de muestra (44 de 54 versiones en MES EXAMEN, 54 de 54 en MNQ; media +0,81 y +1,28 pts de MES); G1 y G2 no (cerca de 0).
- Decisión: el operador pidió "resolvelo, llevalo a los límites, sin excusas". Se instala Corriente 1.1 con C por defecto,
  **marcada SIN VALIDAR del todo** (como la 3.0.4), con todos los números dichos en el mismo mensaje, captura antes/después,
  y un registro de cada vuelta con su causa (línea o rápida) para medirla hacia adelante. Paridad: `paridad_corriente.py lab`
  (0 velas distintas de 84.364) y `paridad_corriente.py` contra el volcado de ATAS.
- Costo declarado: en la rueda suma ~1 cambio por día en MES (10,1 -> 11,1) y ~2 en MNQ (9,1 -> 11,3), aunque en la sesión
  entera el ruido queda igual.
