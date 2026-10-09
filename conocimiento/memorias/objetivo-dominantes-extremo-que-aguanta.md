---
name: objetivo-dominantes-extremo-que-aguanta
description: "LO QUE EL OPERADOR BUSCA con las dominantes (aclarado 09-10-2026): raya en la PUNTA del extremo de las velas, que la prueben y no la rompan, y después el giro con el máximo recorrido hasta la otra dominante; NO % de rebotes tras toques"
metadata:
  node_type: memory
  type: user
  originSessionId: f5879819-e2a4-405b-87b0-959ace090d7b
  modified: 2026-10-09T04:07:01.523Z
---

Textual (09-10-2026 ~01:00 ART): "lo que busco quiero anhelo sueño es que la dominante dibujada esté en el mayor extremo de las velas y
aunque aparezcan otras velas no la puedan romper ... y que se produzca el cambio de tendencia y tenga el máximo recorrido posible hasta
la siguiente dominante ... no busco que las dominantes toquen todas las velas o se posicionen en el medio ... (en otras palabras
soportes y resistencias)". Dice que lo viene explicando hace semanas y que yo lo desviaba.

**Falso rompimiento permitido (aclarado 09-10, textual):** "puede atravesarla un poquito solo con mechazos o velas de pequeño cuerpo o
indecisión pero finalmente terminan retrocediendo y cumpliendo el cambio de tendencia (falso rompimiento, para dejar atrapados)". Rota =
el precio se ACEPTA del otro lado: cierre de 1 min más allá de raya+5 con cuerpo ≥4, o 3 cierres seguidos afuera, o >10 min afuera.
Las mechas que pinchan y vuelven NO rompen (se cuenta el evento como sostenido con falso rompimiento).

**VARA OFICIAL PARA SCALPING (09-10 ~02:10 ART, textual):** "que se dibujen las dominantes y que respeten, como mínimo cumplan 20 puntos
de recorrido ya sea para arriba o para abajo ... (para scalping) y obvio no llenar el gráfico de rayas o ruido o falsas entradas".
=> ACIERTO = llegada que aguanta (falso rompimiento permitido) y recorre >= 20 pts a favor antes de romper; ENTRADA FALSA = llegada que
rompe o no llega a 20; además medir RAYAS POR HORA (densidad) y entradas falsas por noche. Le encantó NDX 30800 = 31040,74 del 09-10
(piso 03:11Z, precisión 0,74, recorrido 93 pts). Busca el candidato en FÓRMULA y en INSTRUMENTO/DERIVADO (NQ, NDX, QQQ, TQQQ, SPX...).

**Error mío a no repetir:** medí durante semanas "% de rebotes ≥6 pts tras cada toque", que PREMIA rayas en el medio del rango tocadas
muchas veces. Su vara es otra. Juez operativo (criterio_operador, laboratorio/calibracion_1009/criterio_operador/juez/juez_operador.py):
llegada (mecha a ±2 pts desde el otro lado) → aguanta con falso rompimiento permitido (modo TOLERANTE, el principal; el ESTRICTO
"cierre más allá de raya+2 o mecha más allá de raya+4 rompe" queda solo como sensibilidad) hasta que el precio se aleja 15 pts en contra → precisión del extremo (raya vs punta real) → recorrido tras sostener (MFE hasta volver a romper, y si
llega a la dominante opuesta) → cobertura de giros grandes (pivotes ≥20 pts) ya dibujados antes. Siempre contra placebo.

**How to apply:** toda comparación de fórmulas/indicadores de dominantes se juzga con ESTA vara primero. Él usa la 2.0 como ejemplo
de lo que quiere (31083,50 del 08-10); copiarla y sumarla a la 4.1 lo pidió explícitamente. Ver [[laboratorio-formulas]],
[[pythiagex-4-0-familia]], [[comprobacion-fehaciente-en-pantalla]].
