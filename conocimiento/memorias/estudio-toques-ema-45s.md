---
name: estudio-toques-ema-45s
description: "15-10-2026: estudio de toques a EMA 9/20 en su grafico MNQZ6 de 45 s (calibrado al centavo con sus capturas); fuentes de velas del dia: relleno Rithmic exacto, cinta en vivo pierde extremos, cache de ATAS solo tras el cierre"
metadata:
  node_type: memory
  type: project
  originSessionId: 1c10d3db-7e2b-4280-869b-0e37fa7ec916
  modified: 2026-10-15T21:43:05.332Z
---

Pedido 15-10-2026 ~16:50 ART: dos estudios separados (EMA 9 y EMA 20) de cuantas veces el precio volvio a la linea (toco, perforo,
falso rompimiento) y siguio; puntos de maxima ganancia hasta revertir (promedio, min, max); grafico de velas marcando cada zona.

**Medido, no supuesto:**
- Su grafico de esas capturas es **MNQZ6 de 45 segundos**, violeta = EMA 9, azul = EMA 20 (vela viva 15:52:30 NY con 30132,00 ->
  30147,48 / 30154,77 contra 30147,49 / 30154,83 en pantalla). Antes de medir algo "en su grafico", calibrar la temporalidad con los
  valores que muestra el eje: 1 min daba 3-4 pts de error, 45 s 0,01-0,1.
- Velas del dia en curso: la cache de ATAS (Cache_v2) NO guarda la sesion en curso; la escribe cuando se carga un grafico DESPUES de
  que la sesion termino. Mientras tanto: `PythiaGex/profundidad/estado/cinta/cinta-NQ-<dia>-relleno.csv` (Rithmic, pedido por la 3.0)
  reproduce las velas de 45 s de ATAS con cierres identicos (1264/1264 el 14-10) y H/L a <= 0,75; la cinta EN VIVO (`cinta-NQ-<dia>.csv`)
  pierde operaciones en minutos rapidos: 27 de 574 velas con H/L > 1 pt (hasta 29,5) y 19 cierres distintos. NQ=F de Yahoo 1 min sirve
  de tercer arbitro (diferencia media 0,4 con el relleno).
- Trampa propia corregida: clasificar el "tipo de toque" mirando todo el episodio contamina con el resultado (daba "apoyo justo 42 de 44
  siguieron"). Clasificar con la vela del toque sola: EMA 9 66 % / 54 % / 12 % (toque limpio / mecha / cerro del otro lado).

- Reglas: v1 (2 cierres = rotura, vela previa despegada, tol 1) captaba 3 de 9 circulos suyos; v2 (4 cierres, arranque con vela previa
  cerrada del lado bueno, tol 2) los captaba pero tenia doble conteo entre lados, rupturas en V de 40 pts como "falso rompimiento" y
  fragilidad de 0,05 pts. **v3 vigente** (ESPEC3.md): 5 cierres o 1 a >12 pts, banda muerta 0,5, cruce >6 pts aparte, siguio = extremo
  nuevo y >= 5 pts, exclusividad entre lados. Paridad 0 diferencias contra implementacion independiente; estable a linea ±0,10 y al dato vivo.
- Resultado v3 dia 9:30-16:00: EMA 9 65 de 99 siguieron (26 recorridos), prom +88,16, min +14,67, max +220,45; EMA 20 37 de 69 (17),
  prom +109,59, min +16,46, max +248,95. Pagina: https://claude.ai/artifact/GauoEm6ZR4ZCyxPfxfHYkR
- **Incidente**: reiniciar_atas.ps1 mataba ATAS a los 60 s de "Save and close"; el 15-10 lo mato a mitad del guardado y su espacio
  "MNQ liviano" volvio al de las 15:15 (se perdio su grafico de 45 s). Corregido: espera 240 s e informa la hora del .ws guardado.

Laboratorio: `PythiaGex/laboratorio/ema_toques/` (README.md, motor3.py, ESPEC3.md, armar_pagina3.py, plantilla.html, datos/, verificacion/).
**Why:** el operador quiere numeros de su propio grafico; sin calibrar la temporalidad y sin elegir la fuente buena los toques cambian.
**How to apply:** para cualquier estudio intradia del dia: relleno hasta su 'hasta', vivo despues marcado como dudoso, y rehacer con la
cache de ATAS al cierre. Ver [[comprobacion-fehaciente-en-pantalla]], [[fuentes-datos-historicos]], [[cache-velas-atas-podada]].
