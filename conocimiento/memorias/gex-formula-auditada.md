---
name: gex-formula-auditada
description: "La formula del GEX contrastada contra tres fuentes y contra el Options Board de ATAS. Dos errores reales encontrados, y el descubrimiento de que el gamma no depende del tiempo si la IV se despeja con el mismo tiempo."
metadata:
  type: project
---

Auditado el 2026-09-04 contra tres textos de referencia que trajo el operador y
contra el **Options Board del propio ATAS**.

## Nuestra formula es la correcta

`Γ × OI × M × S² × 0,01`, que es la de la tercera fuente palabra por palabra.

Ojo con la primera fuente: decia que multiplicar por `S` da el valor por 1 % de
movimiento. **Es falso**: multiplicar por `S` una vez da los dolares por UN
PUNTO. Para el 1 % hace falta `S²/100`. Nosotros ya estabamos bien.

Las dos formulas que circulan (cruda `OI×Γ` y en dolares) **dan los mismos
niveles**: verificado recalculando en Python desde la cadena cruda, mismos 8
strikes en el mismo orden, mismo call wall y mismo put wall. `S` y el
multiplicador son la misma constante para todos los strikes, y una constante
positiva no cambia cual es el maximo.

## Error 1: el multiplicador (solo afectaba el titular)

Estaba en **100 para todo**, que es el de opciones de indice. Los futuros de CME
valen lo que su futuro: **ES 50, NQ 20, RTY 50**. El titular de ES venia inflado
al doble (6,187 -> 3,093 mil millones). **No movia ningun nivel**: constante
pareja sobre todos los strikes.

## Error 2: los muros caian del lado equivocado (SI importaba)

Se tomaba el maximo y el minimo **globales**, sin mirar de que lado del precio
caian. Medido sobre **1777 renglones** del registro propio:

- el "call wall" quedo **por debajo** del precio **217 veces (12,2 %)**
- el "put wall" quedo **por encima** del precio **167 veces (9,4 %)**

Caso real de NQ: precio 29.516 y put wall dibujado en **29.600**, 84 puntos
arriba. Un piso dibujado por encima del techo.

Arreglado: call wall = maximo con K > precio, put wall = minimo con K < precio,
con caida al global si de un lado no hay strikes. El renglon de auditoria ahora
publica **los dos** (`maxglobal` / `minglobal`) para poder medir cuando difieren.

## EL HALLAZGO: el gamma NO depende del tiempo

Mismo contrato, mismo precio de mercado, cuatro convenciones de tiempo:

```
  dias      IV despejada   IV x raiz(T)   gamma
   4,0000    20,2758 %      0,021226      0,00242214
   4,5404    19,0320 %      0,021227      0,00242187
  14,0000    10,8486 %      0,021247      0,00241725
  15,0000    10,4818 %      0,021249      0,00241676
```

**`IV × √T` es la misma siempre.** Es lo unico que el precio de mercado fija, y
el gamma depende solo de eso. Entre 4 dias y 15 dias el gamma cambia **0,2 %**.

Consecuencia practica: **si te equivocas en T pero despejas la IV con el MISMO T
equivocado, el error se cancela.** Por eso conviene siempre despejar la IV de la
punta y no usar una IV servida por un tercero calculada con otro T.

## Lo que revelo el Options Board de ATAS

Nuestra IV salia **+0,39 puntos porcentuales** sobre la de ATAS, constante en 36
comparaciones (mediana 0,386, maxima 0,402). Un sesgo constante con casi cero
dispersion no es un error del despejador: es un parametro distinto.

Es el tiempo: **ATAS usa 15 dias donde el calendario da 14** (reproduce su IV con
error medio de 0,014 pp). Nosotros contabamos dias enteros desde la medianoche
LOCAL. Los dos estabamos algo mal y **a ninguno de los dos le cambia el gamma**,
por lo de arriba.

Se corrigio igual, por higiene: el tiempo ahora lleva la hora (`dias +
TiempoQueQuedaHoy()`) y la fecha de HOY se toma de **Nueva York**, no de la local
-- entre medianoche y las 2 de la manana en Argentina alla todavia es el dia
anterior. Verificado que **no mueve ningun nivel**: mismos 8 strikes, mismos
muros, neto -0,0 %.

## Como se abre el Options Board (costo varios intentos)

El panel angosto no sirve: los controles vienen cortados. Hay que abrirlo desde
**Options Board β** en la barra principal, elegir el instrumento, y despues:

1. clic en el desplegable de **cuenta** (ese si abre con clic)
2. elegir la cuenta -- eso **puebla** la lista de series, que hasta entonces esta
   vacia y no abre con clic ni con alt+flecha
3. clic en el campo de serie, que ahora si despliega

El filtro **Series Type = Regular** muestra solo las trimestrales. Los diarios y
el 0DTE estan en otro valor de ese filtro.

**Why:** el proyecto existe porque los tableros ajenos mienten por omision; los
dos errores propios que aparecieron aca son exactamente del mismo tipo que los
que les encontramos a ellos.

**How to apply:** ver [[calcular-gex-propio]], [[atas-opciones-es]] y
[[pelotitas-son-eventos]].
