---
name: regla-roja-roll-libro-vivo
description: "REGLA ROJA (15-09-2026): si el operador dice 'las dominantes estan lejisimas del precio de noche / antes habia tunel-canal-techo-piso y ahora no / la estela esta muy arriba', NO discutir que 'el dato es asi'. Primero revisar el libro vivo de Rithmic y el roll de contrato: en la semana del roll las weeklies viven en el trimestre que vence (U6) y el puente las pedia con Z6 -> 'no data' -> libro vivo sin 0DTE -> sin tunel. Arreglado en 1.10d; verificar en el log 'roll:' antes de responder."
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 961a521e-545b-452c-9eed-32904fb03eae
  modified: 2026-09-16T00:58:33.814Z
---

# REGLA ROJA: "dominantes lejos de noche" = revisar primero el libro vivo y el roll, no discutir

**Palabras del operador que disparan esta regla** (asi las dice el): "las dominantes estan lejisimas del precio",
"demasiado lejos del precio", "la estela esta muy arriba", "tendria que haber un tunel / canal / techo piso",
"antes en otros dias habia y ahora no", "en la jornada central de Chicago se acercaban", "a la noche siempre se
veian cerca del precio de NQ/MNQ y ahora ni aparecen", "las ambar / las de NDX o QQQ / los zero grises no estan",
"hace dias tuvimos el mismo problema y encontraste una formula mal".

**Lo que paso tres veces** (y la tercera le discuti durante horas que "la cuenta esta bien"):
1. 09-09: la base cruda de la nube rolo a diciembre 8 dias antes del vencimiento y TODOS los niveles de MNQ quedaron
   ~294 pts arriba ([[auditoria-en-vivo-2026-09-09]]).
2. 10/11-09: "500 arriba / 200 abajo desde las 17 hs": el empate tecnico (1.8i) acerco la dominante entre barras
   comparables ([[dominantes-de-noche-resto-de-ayer]]).
3. 15-09 (semana del roll de septiembre, graficos ya en MNQZ6/MESZ6): el puente de Rithmic listaba las series de la
   semana bajo NQZ6/ESZ6 pero al pedir sus contratos Rithmic decia "no data": esas weeklies (15, 16, 17, 18-09) son
   opciones SOBRE EL TRIMESTRE QUE VENCE (NQU6/ESU6). El libro vivo quedo toda la semana sin 0DTE ni 1DTE (41
   strikes, -0,010B) y de noche no habia tunel. Yo explique durante horas que "el libro de manana es flaco" (que es
   verdad para CBOE) sin mirar el libro vivo, que era lo que el recordaba cerca del precio.

**La resolucion (Gamma Hoy 1.10d, CadenaViva.cs + PuenteRithmic.cs)**: buscar en el servidor el trimestre anterior
del futuro del grafico (`CodigoTrimestreAnterior`: NQZ6 -> NQU6); si todavia cotiza, suscribirse a su precio, pedir
con su codigo las series con vencimiento <= su fecha (`OpcionesAsync(..., subAlternativo)`) y dibujar sus strikes
corridos por el spread vivo Z6 - U6 (`KDe`). Medido 15-09 21:40: 774/806/1018 contratos con NQU6, spread +291,13;
en pantalla NQ D1 +14 / D2 -9 del precio: el tunel volvio. Detalle en [[roll-semana-weeklies-en-el-trimestre-viejo]].

**Como aplicar, en este orden, antes de contestar UNA palabra:**
1. `grep "roll:\|no data\|FALTA EL VENC" %APPDATA%\ATAS\pythiagex-gammahoy.log`: si hay "no data" para la weekly del
   dia o "FALTA EL VENCIMIENTO MAS CERCANO", es el roll o el puente. Semanas de roll: la del tercer viernes de marzo,
   junio, septiembre y diciembre (los graficos suelen rolar 8 dias antes).
2. Mirar la capa NQ (opciones del futuro, en vivo) en la leyenda: si dice "sin dato" o strikes < 100, el libro vivo
   esta roto o vacio (reiniciar ATAS de noche lo vacia: el volumen se acumula solo con ATAS abierto).
3. Recien despues comparar CBOE (NDX/QQQ) contra Yahoo y la regla de dominantes. Y si el operador dice "antes se veia
   cerca y ahora no", eso ES evidencia de que algo se rompio en el codigo o en el dato (regla 6 y 7 del protocolo),
   no una impresion a corregir.

**Why:** el operador lo pidio como regla roja: "grabalo en memoria para no olvidarte en unos meses, porque si no me
vas a volver a discutir por dias que lo que yo digo esta mal, y ahi esta la prueba, mira como grafican un simil
tunel". Tenia razon las tres veces.

**How to apply:** ante cualquiera de las frases de arriba, este archivo primero, el log segundo, la explicacion
tercera. Nunca cerrar con "el dato es asi" sin haber mirado el libro vivo y el roll.

**Segunda parte (16-09, 1.10y):** Rithmic lista bajo Z6 solo ALGUNAS weeklies del trimestre que vence (el 16 y el 18, no el jueves 17). El puente ahora lista tambien con el codigo viejo (NQU6/ESU6) y suma las fechas que falten; log "roll: N vencimiento(s) que solo lista NQU6". Chequeo rapido: `viva-NQ-<hoy>.jsonl` tiene que traer un `dias` entre 1 y 2 de dia. Ver [[auditoria-2026-09-16-0dte-pelotitas-carga]].

**Cuarta vez (17-09 noche, vispera del vencimiento trimestral; Gamma Hoy 1.11c):** "las verdes de NQ estan lejos, siempre es un tunel". Le conteste "el dato no esta corrupto" SIN auditar que vencimientos cargaba el libro; me dijo "no mientas, audita todo" y tenia razon OTRA VEZ. El viernes del vencimiento trimestral conviven DOS series el mismo dia: la **Regular** (la trimestral, sobre U6, vence 9:30 NY) y la **Weekly de la tarde** (sobre Z6, vence 16:00 NY). Mi regla del roll era por FECHA ("vencimiento <= el de U6 -> pedir con U6"): la Weekly del viernes se pedia con NQU6 + 20260918 y Rithmic devolvia los 1028 contratos de la TRIMESTRAL (log: "1028 contratos de NQZ6 20260918 Weekly pedidos con NQU6"); el 0DTE real del viernes NO cargaba (tampoco en ES: 876 con ESU6) y toda la rueda del 18 habria quedado sin 0DTE. Arreglo: la serie es del trimestre viejo si vence ANTES que el, o el mismo dia Y es Regular; la llave de serie es fecha + tipo; la hora de vencimiento (9:30 / 16:00) va por CONTRATO (`_opsAm`), no por fecha; la trimestral sale del libro a las 9:30 en punto; las series ya vencidas de hoy no se piden (gastaban 806 contratos de cuota de noche). Log nuevo por serie: "serie 09-18 Weekly: 1034 contratos, sobre NQZ6, vence 16:00 NY".
**Y la parte honesta:** con el arreglo puesto, esa noche las rayas NO se acercaron: la trimestral (OI 5.877 en ventana, strikes cada 50-100 pts, 1.554 M en la raya de arriba) pesa 17 veces mas que cualquier strike de weekly (la noche anterior la dominante tenia 91 M), y la weekly del viernes recien nacia (OI 683, en strikes de a 100). Tunel de ~125 pts = vispera de trimestral, real. Herramienta: `laboratorio/gatillo/verdes_52_por_vencimiento.py` (de que vencimiento sale cada dominante) y `verdes_51_que_carga.py` (que vencimientos trae cada foto).
**Paso 0 nuevo de esta regla:** ante "estan lejos", ANTES de opinar correr `verdes_51` y `verdes_52` y leer en el log las lineas "serie MM-dd Tipo: N contratos, sobre X": cada serie con SU subyacente y SU hora. Nunca decir "no esta corrupto" sin esa lista delante.

