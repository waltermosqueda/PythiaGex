---
name: atas-chartarea-mas-alto
description: "ATAS entrega un ChartArea más alto que el visible: lo anclado a area.Bottom cae detrás del eje de tiempo y no se ve nunca."
metadata: 
  node_type: memory
  type: reference
  originSessionId: 15e03ce1-51d8-43ea-8c35-a2fa1a4b8145
  modified: 2026-09-03T18:31:55.622Z
---

Medido el 2026-09-03 sobre `GammaVivo`. El indicador registró
`area=0,0 977x580`, pero el gráfico visible en pantalla medía **unos 505
píxeles de alto**. O sea que **los últimos ~75 px del `ChartArea` que entrega
ATAS caen detrás del eje de tiempo** y nada de lo que se dibuje ahí se ve.

## Qué rompía

- La cinta de estado, anclada a `area.Bottom - alto - 4`, **no aparecía nunca**.
  Se perdió tiempo buscando el bug en la lógica de dibujo cuando el dibujo
  estaba bien: caía fuera de la vista.
- Al tablero compacto se le **cortaba el último renglón**.
- Los marcadores de nivel fuera de pantalla quedaban a medias.

## La solución

Un margen inferior configurable (`MargenInferior`, por defecto **48 px**) que se
descuenta de `area.Bottom` en **todo** lo que se ancle abajo. Con 48 además se
esquiva el reloj de cuenta regresiva de la vela, que ATAS dibuja en esa esquina.

```csharp
int yc = TableroAbajo
       ? area.Bottom - hc - Math.Max(6, MargenInferior)
       : area.Top + 14;
```

## Síntoma para reconocerlo rápido

Si un elemento **no aparece y no hay excepción en el log**, antes de revisar la
lógica hay que preguntarse dónde está anclado. Si es al fondo, casi seguro es
esto. Ver también [[compilar-indicadores-atas]] para el otro caso parecido: el
indicador iba a `NewPanel` y dibujaba fuera del recorte.

**Why:** dos síntomas distintos (la cinta invisible y el renglón cortado) tenían
la misma causa, y ninguno daba error.

**How to apply:** cualquier cosa nueva que se ancle a `area.Bottom` tiene que
descontar `MargenInferior`. Arriba no hace falta: `area.Top` sí coincide.
