# Ronda 17 — Anatomía del movimiento (order flow de adentro) — PRE-REGISTRO

Escrito el 26-11-2026 a las 23:05 AR, **antes de correr una sola línea que mire factores o resultados**. Lo único que se
calculó antes es la regla de elección de ruedas (sección 3), que solo lee apertura/máximo/mínimo/cierre del día y huecos de
grabación. Lo que cambie después va en "Enmiendas", con hora y motivo, y antes de abrir la tanda siguiente.

## 0. Qué cambió y qué se mide

El objetivo del operador (26-11, noche) manda sobre el pedido original: **explicar mecánicamente por qué se movió el precio**,
desde adentro. En cada giro y en cada pierna: quién agredió, quién absorbió, dónde, contra qué nivel; qué factor pesó más y qué
combinación lo produjo. La teoría lo respalda: el flujo firmado explica la mayor parte del movimiento **en el mismo instante**
(Cont, Kukanov y Stoikov 2014: el desbalance de la punta explica ~65 % de la varianza de la variación de 10 s) y casi nada
después (Cont, Cucuringu y Zhang 2023; Chordia, Roll y Subrahmanyam 2005). Por eso esta ronda es **EXPLICATIVA**, no un
pronóstico: describe lo que pasó en el giro y lo compara contra extremos que NO fueron giro. Cada factor se rotula como
**visible en el extremo** (con datos hasta ese segundo) o **reacción** (después del extremo: explica, no anticipa).

Las familias del pedido original (divergencia, apilados como zona, big trades, absorción, footprint, estructura, contexto,
semáforo de tres capas) entran acá como factores del giro y de la pierna. El semáforo y la prueba en vivo quedan como
complemento (PRUEBA_EN_VIVO.md).

## 1. Método de muestra: de menor a mayor (pedido del operador)

- **Tanda 0 (muestra chica, en profundidad):** 3 ruedas de ES elegidas por regla (sección 3) + las 3 ruedas NUEVAS de MES
  (23, 24 y 25-11-2026, cinta orden por orden nunca usada). Rueda por rueda: segmentación, factores, narración.
- **Tanda 1 (si algo se repite):** las otras 6 ruedas de ES de noviembre (02, 03, 04, 10, 12 y 13-11), como confirmación
  de lo que se haya visto. Lo que no repita queda escrito como "visto en tanda 0, no repitió".
- **Tanda 2:** 10 ruedas de ES de octubre (19 al 30-10). **Tanda 3 (amplia):** velas de MES de 5 min (276 sesiones,
  2025-08-21 a 2026-09-21, `dom\ronda3\velas`) solo para lo que no necesita footprint por precio (lugar, estructura,
  delta por vela, contexto).
- Todo es exploratorio salvo las 3 ruedas de MES (nuevas). Las 49 ruedas de ES ya se usaron en R15-O, R16, R16b y R16c.

## 2. Datos

- ES: cinta de la sonda `%APPDATA%\ATAS\PythiaGex\flujo\cinta-ES-*.csv` (con .listo), tabla de 1 s ya armada
  (`datos\flujo\cache\ES\seg-*.parquet`, con `abs_bid/abs_ask` = absorción REAL en la punta de `laboratorio\gatillo\base.py`).
- MES: `cinta-MES-202611222300-202611232200.csv`, `...232300-...242200.csv`, `...242300-...252200.csv` (con .listo). Tabla de
  1 s con la MISMA función `base._tabla_1s`.
- Duplicados: se borran filas idénticas en TODAS las columnas (como `base.py`; R16 usaba 5 columnas).
- Footprint por tick desde la cinta: una orden que barre varios precios se reparte parejo entre los ticks que recorrió
  (aproximación declarada) y el tick queda marcado "tocado por barrido" para chequear sensibilidad.
- Hora: Nueva York por zona horaria (el cambio de horario del 01-11 no corre nada). Rueda 9:30:00-15:59:59.

## 3. Elección de las ruedas de ES (regla fijada y aplicada antes de mirar factores)

Candidatas: las 10 ruedas de ES del 02 al 13-11-2026 (tienen semana y mes anteriores completos en la cinta, todo Z6). Se
excluye una rueda si tiene un hueco de grabación > 5 min en la rueda o un rango > 2,5 veces la mediana de las candidatas
(85,75 pts). Eficiencia del día e = |cierre − apertura| / (máximo − mínimo) de la rueda.
- 05-11 queda afuera: rango 335,25 pts (3,9 veces la mediana).
- **TENDENCIA = mayor e: 09-11** (e 0,736, alcista, rango 144,75).
- **RANGO = menor e: 06-11** (e 0,068, rango 77,0).
- **MIXTA = e más cercana a la mediana (0,383): 11-11** (e 0,383, rango 78,25).
Ninguna de las tres tiene huecos en la rueda.

## 4. Segmentación en giros y piernas (regla fija)

- Zigzag "en vivo" sobre alto/bajo de cada segundo de la rueda, con umbral **X = 0,08 × mediana del rango de la rueda de
  las 10 sesiones previas disponibles**, redondeado al tick y nunca menor a 4 pts. Un máximo queda confirmado como GIRO
  cuando el precio baja X desde él (espejo para mínimos). El tramo inicial arranca en la apertura de las 9:30; la última
  pierna sin confirmar se informa aparte.
- Escala secundaria ("micro", la del scalper): **X/2**. Se informa para ver si lo que aparece en la escala principal se
  sostiene en la chica.
- **PIERNA** = recorrido entre dos giros consecutivos.
- Controles (puntos que NO son giro):
  - **PAUSA** (control principal, emparejado): dentro de una pierna, un extremo que retrocedió al menos X/2 pero menos de X
    y después fue superado (la pierna siguió). Es "un giro que no fue": mismo tipo de punto, otro desenlace.
  - **MOMENTO AL AZAR** (control de base): grilla fija cada 5 min (:00, :05...) dentro de las piernas, a más de 2 min de
    cualquier giro o pausa; el lado es el de la pierna (en una pierna alcista se lo trata como un "posible máximo").

## 5. Factores en cada punto (giro, pausa o momento), con s = +1 si es un máximo y −1 si es un mínimo

Ventanas: **PRE** = [t − 60 s, t] (visible en el extremo); **REAC** = (t, t + 60 s] (reacción). t = primer segundo en que
se tocó el extremo. Normalización (Andersen y Bollerslev 1997: la periodicidad intradía domina): percentil contra todas las
ventanas de 60 s de la MISMA rueda y la MISMA franja de 30 min. Es una normalización descriptiva (usa la rueda completa) y
vale para explicar, no para operar en vivo; para la versión en vivo se calibra con ruedas previas (PRUEBA_EN_VIVO.md).

Visibles en el extremo:
1. **ABS** absorción real en la punta: suma en PRE de `abs_ask` (en un máximo: compras agresivas que se comieron todo el
   ask visible y el ask no subió = vendedor pasivo que repone/iceberg) o de `abs_bid` (en un mínimo) ≥ percentil 90.
2. **VOL** volumen de PRE ≥ percentil 90.
3. **EMPUJE** s × delta de PRE ≥ percentil 90 de |delta de 60 s| (agresión fuerte HACIA el extremo).
4. **DELTA_CONTRA** s × delta de PRE < 0 (el extremo se hizo con agresión neta contraria).
5. **DIV_SWING** divergencia contra el giro previo del mismo tipo: precio más allá del giro previo (máximo más alto) y CVD
   (acumulado desde las 18:00) en el extremo del lado contrario al del giro previo. Solo se evalúa en puntos que superaron el
   giro previo; el resto queda "no aplica".
6. **DIV_VELA** la vela de 1 min que contiene el extremo tiene delta de signo contrario a s.
7. **AGOTAMIENTO** volumen total en el tick del extremo (ventana t ± 120 s) ≤ 25 % del promedio de los 4 ticks de adentro.
8. **SUBASTA** en la vela de 1 min del extremo (regla de ATAS Learn): TERMINADA si en el máximo el bid (ventas agresivas)
   es 0 / en el mínimo el ask (compras agresivas) es 0; SIN_TERMINAR si los dos lados son > 0; OTRA el resto.
9. **APIL_FAVOR** apilado del lado del empuje (de compra en un máximo) en la vela del extremo, dentro de los 12 ticks del
   extremo. Apilado = 3+ precios seguidos con desequilibrio diagonal ≥ 300 % (compras(p) ≥ 3 × ventas(p − 1 tick); espejo)
   y volumen mínimo por celda V_min. V_min ES = 30 contratos (R16c); MES = el valor con el mismo percentil en MES (se mide).
10. **BIG_FAVOR / BIG_CONTRA** órdenes agresoras ≥ percentil 99 del tamaño de orden de la rueda del instrumento (y 99,9),
    en t ± 60 s y a ≤ 2 ticks del extremo; FAVOR = del lado del empuje (compras en un máximo). En ES también ≥ 95 (su filtro).
11. **BARRIDA** el extremo superó por 0,25 a 3 pts una referencia que en los 10 min previos estaba del otro lado: giro previo
    del mismo tipo, máximo/mínimo de ayer, de la noche, del balance inicial (desde 10:30), de la semana y del mes anteriores,
    múltiplo de 50.
12. **LUGAR** a ≤ 1,5 pts de: AYER (máximo, mínimo, cierre, POC, VAH, VAL de la rueda anterior; área de valor de ATAS 70 %),
    NOCHE (máximo y mínimo 18:00-9:30), IB (máximo y mínimo 9:30-10:30, desde las 10:30), VWAP (de la sesión desde las 18:00
    y ±1 y ±2 desvíos), REDONDO (múltiplo de 50), SEMANA y MES anteriores (máximo, mínimo, cierre, POC, VAH, VAL; perfil
    aproximado con el volumen de cada segundo en su último precio, sesión completa). "Lugar importante" = cualquiera salvo VWAP.

Reacción (después del extremo; explica el giro):
13. **ABS_REAC** como 1 pero en REAC.
14. **AGRESION_CONTRARIA** s × delta de REAC ≤ − percentil 75 de |delta de 60 s|.
15. **APIL_CONTRA** apilado del lado contrario (de venta en un máximo) en la vela del extremo o la siguiente, dentro de los
    12 ticks del extremo.
16. **BIG_CONTRA_REAC** órdenes grandes del lado contrario en REAC.

Contexto: franja (APERTURA 9:30-10:30, MAÑANA 10:30-12:00, MEDIODÍA 12:00-14:00, TARDE 14:00-15:00, ÚLTIMA 15:00-16:00) y
tipo de día (tendencia / rango / mixto).

## 6. Factores en cada pierna

Recorrido, duración y velocidad (pts/min); delta acumulado y delta/volumen; puntos por cada 1.000 contratos netos (cuánto
movió cada contrato agresivo: lo opuesto a la absorción); apilados a favor y en contra (por cada 10 pts); órdenes grandes a
favor y en contra; referencias atravesadas (barridas); vacíos de liquidez (ticks del recorrido con volumen < 20 % de la
mediana del perfil de la rueda anterior, y segundos por tick adentro y afuera de los vacíos); absorciones que FALLARON
(ventanas de 60 s con ABS en contra de la pierna ≥ percentil 90 tras las cuales el precio siguió X/2 más a favor de la pierna).

## 7. Cuantificación (descriptiva y explicativa)

- Por factor binario: proporción en giros, en pausas y en momentos; **cociente de enriquecimiento** (RR = proporción en
  giros / proporción en el control) con intervalo del 95 % (método logarítmico de Katz) y el signo día por día.
- Factores continuos: mediana en giros contra controles y AUC de Mann-Whitney.
- Combinaciones más frecuentes en giros en lugar importante, contra su frecuencia en pausas en lugar importante.
- Peso de cada factor para separar giro de pausa: regresión logística con los factores fijados (L2, C = 1, estandarizados)
  y árbol chico (profundidad 3, hoja mínima 10), con AUC dejando un día afuera. Dos versiones: solo "visibles en el
  extremo" y con "reacción". Se informa la importancia de cada factor.
- Tendencia contra rango: las mismas proporciones por tipo de día.
- Calibración: umbrales y magnitudes típicas de cada factor en ES y MES (percentiles, frecuencia por hora).

## 8. Qué cuenta como "se repite" (va al baúl) y qué no

- **Patrón que se repite:** RR contra PAUSAS del mismo lado (> 1 o < 1) en al menos 5 de las 6 ruedas donde se puede medir
  (o en todas si hay menos), con el intervalo combinado sin incluir el 1. Va al BAUL como EXPLICATIVA (anatomía), con sus
  números y su contexto.
- **Aparece en un contexto:** RR con intervalo sin el 1 en un tipo de día (o franja) y no en otro: se dice en cuál.
- **Visto en una rueda:** se informa como hallazgo de esa rueda, sin generalizar.
- Nunca "descartado" ni "muere". "Sin información" solo si iguala a su control en varias muestras.
- Lo que NO afirma: que un factor explique el giro no quiere decir que lo anticipe. Lo que se use para operar tiene que
  pasar la prueba en vivo con calibración de ruedas previas.

## 9. Corrección de errores viejos que esta ronda aplica (de la auditoría, ver RESULTADOS.md)

- Hora de Nueva York por zona horaria (no UTC fija). Percentiles por franja de 30 min (la apertura no satura todo).
- Duplicados con todas las columnas. Error de las comparaciones por día (signo día por día) y no por evento.
- El control es un punto del MISMO tipo (pausa), no un instante cualquiera con el lado del evento (el placebo "misma media
  hora y mismo lado" de rondas viejas miraba adelante).
- Escala del scalper: giros de ~X (≈ 6-8 pts en ES/MES) y micro X/2; ventanas de 60 s alrededor del extremo.
- Desplazamiento contra flujo: el impacto es cóncavo (ley de la raíz cuadrada, Tóth et al. 2011), así que toda razón
  "precio por contrato" se informa también contra la raíz del volumen.

## Enmiendas

(Horas del reloj de la PC; las de las enmiendas 1 a 3 son aproximadas porque el archivo guarda solo la última
modificación. Lo que importa es el orden: cada enmienda se escribió antes de la corrida que la usa.)

**Enmienda 1 (26-11-2026 ~23:12 AR, después de armar los datos y ANTES de calcular un solo factor):** precisiones de
implementación que el texto no fijaba.
- Llegaron las cintas de MES del 16 al 20-11: dan los niveles de AYER del 23-11 y la SEMANA anterior de 23, 24 y 25-11. El MES
  anterior (octubre) sale de la cinta de ES (mismo contrato Z6). La escala X de cada rueda usa las 10 sesiones previas que hay,
  ES y MES mezcladas (mismo precio).
- Tipo de día de las ruedas de MES (las de ES ya están elegidas): e ≥ 0,55 TENDENCIA, e ≤ 0,25 RANGO, el resto MIXTA.
- No cuenta como giro un extremo en los primeros 30 s de la rueda (el primer print de la apertura no es un giro).
- V_min de los apilados en MES: el valor de MES con el mismo percentil que 30 contratos tiene entre las celdas (precio × lado,
  vela de 1 min, con volumen) de las 3 ruedas de ES de la tanda 0.
- DIV_VELA se mide con la vela completa (como dice el texto, incluye segundos posteriores al extremo); se agrega DIV_VELA_PRE
  (vela hasta el segundo del extremo) como versión visible.
- Referencias de BARRIDA: en un máximo, máximos (ayer, noche, IB, semana, mes, giros máximos previos del día) y el múltiplo de
  50 de abajo; en un mínimo, espejo.
- El lado de un MOMENTO al azar es el de la pierna a la que pertenece, vista completa (descriptivo).
- Órdenes grandes: percentil 99 y 99,9 del tamaño de orden de la MISMA rueda (medidos: ES p99 25-41 y p99,9 73-115 contratos;
  MES p99 18-25 y p99,9 55-72). En ES, 95 contratos queda cerca del p99,9.

**Enmienda 2 (26-11-2026 ~23:18 AR, DESPUÉS de ver la primera corrida de la tanda 0; todo lo de acá es post-hoc y se informa así):**
- Los MOMENTOS al azar quedaron casi vacíos (72 en la escala X, 3 en la micro): con giros cada 1-3 min, casi toda la grilla de
  5 min cae a menos de 2 min de un extremo. Se cambia la grilla a cada 30 s con exclusión de ±20 s. Solo cambia ese control.
- Sospecha de construcción: AGOTAMIENTO, SUBASTA, órdenes grandes en el extremo y DIV_VELA usan segundos POSTERIORES al extremo
  (ventana ±120 s, vela completa, ±60 s). En una pausa el precio vuelve a pasar por el extremo y le suma volumen; en un giro se
  va. Eso puede fabricar el enriquecimiento. Prueba del escéptico (antes de mirar su resultado): **versión visible a τ** =
  t + 15, 30 y 60 s; solo extremos que siguen en pie en τ (no superados); todo medido con datos hasta τ; y comparación giro
  contra pausa DENTRO del mismo tramo de retroceso ya hecho en τ (0-25 %, 25-50 %, 50-100 % de X). Si el enriquecimiento no
  sobrevive dentro de los tramos, era consecuencia del giro y no señal visible.
- Absorciones que "fallaron" en las piernas: dentro de una pierna el precio sigue por construcción; se agrega el control de
  segundos al azar de la misma pierna (misma regla "siguió X/2 a favor") para ver si fallan más o menos que cualquier segundo.
- Narración: además de la escala X se arma una escala GRANDE 4X (los movimientos principales del día) para contar la rueda.
- Las piernas se informan por instrumento (ES y MES tienen contratos distintos: "pts por 1.000 contratos" no se mezcla).

**Enmienda 3 (26-11-2026 ~23:21 AR, antes de correr la prueba visible):** para que la prueba a τ sea causal de verdad, los
candidatos no son solo giros y pausas (eso ya supone que el retroceso llegó a X/2): son TODOS los extremos con retroceso
≥ X/4 (pivotes de un zigzag X/4), con tres desenlaces según el retroceso máximo antes de que el precio supere el extremo:
GIRO (≥ X), PAUSA (X/2 a X), MENOR (X/4 a X/2). En τ = 15, 30 y 60 s se quedan los que siguen en pie y sin confirmar; se mide
P(GIRO) con y sin cada rasgo dentro de rueda × tramo de retroceso ya hecho (riesgo relativo de Mantel-Haenszel; intervalo del
90 % remuestreando ruedas). Rasgos: AGOT (≤ 25 % del volumen de adentro, hasta τ), SUBASTA terminada / sin terminar (vela del
extremo hasta τ), órdenes grandes en el extremo hasta τ, REAC_CONTRA (delta de (t, τ] en contra), y los visibles antes (VOL,
ABS, EMPUJE). Lectura combinada fijada antes: FIRMA = AGOT + SUBASTA TERMINADA + REAC_CONTRA.

**Enmienda 4 (26-11-2026 23:25 AR, post-hoc, después de leer la narración de los movimientos grandes de la tanda 0):** en
piernas largas el precio se movió CONTRA el delta neto (09-11 11:37-14:42 subió 60 pts con delta −6.150; 06-11 varias bajadas
con delta ≈ 0 o positivo). La teoría dice que el flujo de la PUNTA (Cont, Kukanov y Stoikov 2014: altas, bajas y
cancelaciones de límites, no solo agresiones) explica el precio mejor que el desbalance de agresiones. Se agrega, con la
columna `ofi` y `ofi_pas` (parte pasiva) que la tabla de 1 s ya trae: en cada pierna, signo del OFI total y del pasivo contra
la dirección; en la prueba a τ, REAC_OFI y REAC_OFI_PAS (OFI de (t, τ] en contra). Tanda 1 = las 6 ruedas de ES de noviembre
pre-registradas (02, 03, 04, 10, 12 y 13-11), con tipo de día por la misma regla de e (≥ 0,55 / ≤ 0,25), corrida con el
código CONGELADO a esta hora: es la confirmación de lo que se vio en la tanda 0.
