---
name: indicador-que-cuelga-atas
description: Un indicador que recorre muchas velas en OnCalculate cuelga ATAS; y con el mercado cerrado OnCalculate no corre.
metadata: 
  node_type: memory
  type: reference
  originSessionId: 15e03ce1-51d8-43ea-8c35-a2fa1a4b8145
  modified: 2026-09-05T00:56:37.985Z
---

Las dos trampas de escribir indicadores para ATAS que aparecieron al construir
`PythiaFlow - Nodos de Volumen` el 2026-09-04.

**1. `OnCalculate` corre una vez por CADA vela del grafico.**

No es una llamada por tick nueva: cuando ATAS calcula el historico recorre todas
las velas. Si el indicador hace un trabajo pesado adentro -- por ejemplo
recorrer 300 velas con su `GetAllPriceLevels()` -- eso se multiplica por la
cantidad de velas del grafico. En MES de 5 minutos, con pocas velas, ni se nota.
En **MNQ de 1 minuto ATAS quedo clavado en "Loading..." y hubo que matarlo con
Stop-Process**.

La guarda es una linea:

```csharp
if (bar < CurrentBar - 1) return;   // solo en el borde vivo
```

Va antes del calculo pesado. Si el calculo solo tiene sentido "ahora", esto lo
convierte de miles de veces a una.

**2. Con el mercado cerrado `OnCalculate` NO se ejecuta.**

Sin ticks entrando no hay recalculo, asi que **mover un ajuste no hace nada** y
el indicador sigue mostrando los valores viejos. Parece que el ajuste esta roto
y se pierde tiempo buscando el bug donde no esta.

`OnRender` en cambio corre siempre. La solucion es recalcular tambien ahi, pero
**solo cuando cambio la firma de los ajustes**, nunca en cada render:

```csharp
var firma = Firma();                 // los ajustes concatenados
if (firma != _firma) { _firma = firma; Recalcular(); }
```

**Y una de interfaz:** `ChartArea.Right` incluye la escala de precios, asi que
una etiqueta pegada al borde derecho queda cortada por los numeros del eje.
Medido: hacen falta unos **70 pixeles** de margen.

Ver [[compilar-indicadores-atas]] y [[datos-ocultos-de-atas]].
