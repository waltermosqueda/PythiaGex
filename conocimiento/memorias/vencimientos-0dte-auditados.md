---
name: vencimientos-0dte-auditados
description: "Los dias al vencimiento y el 0DTE de Gamma Hoy, auditados contra cuenta manual y CBOE directo: bien calculados; con Horizonte=Hoy TODO el perfil es 0DTE; el nucleo no envejecia los dias (arreglado) y Rithmic perdia el 0DTE al arrancar (reintento)."
metadata: 
  node_type: memory
  type: project
  originSessionId: 9345174c-7f69-4fe5-a793-976e41f2dc7c
  modified: 2026-09-08T15:37:07.576Z
---

Auditado el 2026-09-08 (laboratorio/auditar_vencimientos.py):

- **Los dias estan bien**: vencimiento a las 16:00 de Nueva York (20:00
  UTC); la cadena y la cuenta manual difieren 6-21 segundos. La lista de
  vencimientos coincide con CBOE bajado aparte.
- **Con Horizonte = Hoy el perfil entero es 0DTE** (solo entra el
  vencimiento mas cercano): no hay barras "de otra fecha" en pantalla. El
  "-1,3B sin 0DTE" que vio el operador era el rotulo de la convexidad, que
  ahora lleva "Δ" adelante.
- **La formula de gamma da lo mismo que la de CBOE** (mediana 0,97 SPX y
  1,00 NDX, misma hora/IV/spot); el OI coincide 100 %. Las diferencias de GEX
  contra CBOE "directo" son volumen que crece y el spot que se mueve.
- **Dos fallas honestas encontradas y arregladas (1.4b)**: el nucleo usaba
  los dias congelados de la cadena (ahora los envejece con la hora de la
  cuenta: la gamma usa el tiempo real y el 0DTE vencido sale solo a las
  16:00 NY); y la cadena viva de Rithmic caia al micro (solo trimestral, sin
  0DTE) porque SearchSecuritiesAsync tira NullReference al arrancar ATAS
  (ahora reintenta 6 veces cada 15 s).

**Why:** el operador pregunto si los 0DTE eran "honestos en tiempo real" y si
las barras pesadas sin rotulo eran de otra fecha. Sin medir, la respuesta
habria sido "si" a lo primero y equivocada: los dias si estaban congelados.

**How to apply:** ante cualquier duda sobre un rotulo, correr
`python laboratorio/auditar_vencimientos.py ES|NQ --cboe <json de CBOE>
--precio <futuro>`. Con Horizonte = Semana/Todo los rotulos de barra pesada
dicen "Nd". Ver [[gex-formula-auditada]], [[conversion-spx-a-es]],
[[cadena-es-en-vivo-rithmic]].
