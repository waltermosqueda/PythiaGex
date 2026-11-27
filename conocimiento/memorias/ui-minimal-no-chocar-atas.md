---
name: ui-minimal-no-chocar-atas
description: "Gusto del operador para lo que se dibuja sobre el grafico (27-11-2026): minimalista y profesional, minimizable, arrastrable con el mouse, que no tape el analisis y que NUNCA se pise con lo que escribe ATAS arriba a la izquierda"
metadata:
  node_type: memory
  type: feedback
  originSessionId: 233bf8d4-5113-4e95-a0d9-68bff726690d
  modified: 2026-11-27T18:45:30.043Z
---

Textual (27-11-2026, tarde): "son demasiados botones, hacelo mas profesional minimalista que no moleste al analisis, lo mismo con el
cuadro: tapa, molesta, deberia ser minimizable y que se pueda correr solo arrastrando el mouse... eso es simplicidad y amigable al
usuario". Y al ver la 1.0c instalada con el cuadro arriba a la izquierda: "choca, se mezcla, compite con la data de ATAS".

**Why:** analiza el grafico en vivo; cada cosa nuestra que tapa velas o se superpone a la informacion de ATAS le estorba.

**How to apply:**
- Arriba a la izquierda ATAS escribe: la marca de Rithmic, el aviso "Last Market Data Update" (con datos viejos), el contrato y la linea
  O/H/L/C del mouse, y el recuadro "7 v" de capas: hasta ~112 px de alto y ~330 px de ancho. Abajo a la izquierda va la caja de la
  cuenta. Nada nuestro de fabrica ahi: La Ola 1.0d arranca en (8, 118) (PoLugarX/PoLugarY = -1 automatico); el 27-11 el operador lo
  arrastro a (352, 11), la franja de arriba a la derecha de esos datos.
- **Orden por defecto que pidio (27-11 16:00, con capturas):** arriba a la izquierda, DEBAJO de la lista de indicadores de ATAS ("7 v"): primero el cuadro de La Ola (1.0e: 8, 118; PoCuadroX/Y = -1) y justo debajo la manija "☰" del Superindicador (1.9d: SpManijaBajoLaOla), que sigue al cuadro cuando se minimiza o se abre ("que no compitan entre si, desplegados o minimizados"). La Ola publica su rect en AppDomain.SetData("PythiaGex.LaOla.cuadro|<instrumento sin #>|<marco>", int[] x,y,w,h,tick); el Superindicador lo lee.
- Para verificar con ATAS tapado por Claude: cambiar de pestaña por UIA (Orders -> MES 1m Chart; ESZ6 Heatmap -> ESZ6 1m Chart) fuerza el dibujo sin foco; despues el jsonl dice la version y el rect dibujados.
- Cuadros compactos, con "–" para minimizar a pastilla y arrastre desde cualquier parte; botoneras plegadas en una manija ("☰") que se
  abre con un clic; pies y avisos solo cuando hay algo que avisar.
- Un default nuevo que cambia lo dibujado va con un nombre de propiedad NUEVO (ATAS guarda los ajustes por nombre) y con captura
  antes/despues. Ver [[la-ola-1-0-instalada]], [[superindicador-velas-delta-perfil]], [[como-dibujan-los-pros]].
