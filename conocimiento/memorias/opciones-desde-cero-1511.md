---
name: opciones-desde-cero-1511
description: "15/16-11: X-Ray releido DESDE CERO con lectura exacta y sin mouse (PythiaRegla + xr.py): 74 ruedas (no 19); strikes fuertes, flujo por participante, Hedge Pressure (dos partes), pin y muro-que-crece NO pasan el placebo; flujo = espejo del precio; gamma roja cerca del precio -> mas rango solo en la 2da mitad; dos sesgos mios detectados (rankear dentro del dia, placebo corrido en el pin)"
metadata:
  node_type: memory
  type: project
  originSessionId: b1346044-cbec-4f19-80c3-ffb1de06776c
  modified: 2026-11-16T03:39:56.507Z
---

Pedido 15-11 (textual, resumido): analizar TODO el Options Analytics de ATAS en ES, encontrar relacion con los 2-3 grandes
movimientos de la rueda NY, "sin excusas"; y despues: "no tomes por verdad estudios anteriores de otros agentes, empeza de cero".
Carpeta: `PythiaGex/laboratorio/opciones_cero/` (PREREGISTRO.md, INFORME.md, C_xray/, A_movimientos/, B_cadena/, fichas/).

**Herramientas nuevas (reusar, no rehacer):**
- `atas/PythiaRegla` (instalado, 1.1): escribe `%APPDATA%\ATAS\PythiaGex\regla\mapa-a.json` (vela y precio de cada pixel) y obedece
  `orden-a.json` (barsp, barw, velas=1 oculta velas, scrollp, auto). Vuelca velas a `velas-a-ES-<tf>.csv` (t en UTC).
- `C_xray/xr.py`: PrintWindow de la ventana FLOTANTE "ES 5m Chart" aunque este tapada; teclas LEFT/RIGHT al top-level y arrastre/rueda
  virtual al ultimo hijo WindowsForms mueven el grafico SIN mouse real; arrastre virtual en el eje de precio cambia la escala.
  Los carteles (tooltips) y el Scrub de X-Ray SI necesitan el cursor real (poner "Hover tooltip: While Ctrl is held").
- `c01_cosechar.py <pasada> [--paneles]` (~6 s por dia), `c02_strike.py`, `c05_paneles.py`, `c06_escala.py`, `c11..c14`, `c20_fichas.py`.
- Paneles de indicadores: cada borde agranda el panel de ABAJO a costa del de precio (no el vecino).

**Medido de X-Ray:** 74 sesiones de historia (Hedge Pressure desde ~11-09); base SPX->ES propia por dia (22-09: 68,56) y el dia del
roll (14-09) carriles ~50-60 pts corridos; Strike Heatmap: color ~ valor al cuadrado, satura en ~9,5 % del mayor del dia con brillo
maximo, 0DTE de fabrica solo se ve al final de la rueda (usar "All expirations"); Hedge Pressure = parte precio + parte clientes.

**Resultados (73-74 ruedas, un dia un voto, mitades por fecha):** extremos de tramos grandes a <=2,5 pts de un strike fuerte 26,9 %
vs placebo 21,4 % (t 1,5) y un precio cualquiera 37,7 %; 48 variantes de flujo/HP -> retorno 30/60 min: ninguna |t|>2,05; flujo
espejo del precio (retail -0,73, pros +0,62); pin con placebo espejo ~50 %; muro que crece 22,2 vs 21,7 %; balance rojo a +-25 pts
-> rango 30 min mayor: DISEÑO t -0,6, CONFIRM t -4,4 (no pasa la vara); regimen de la mañana no predice el rango del dia (rho 0,03).


**16-11, niveles OFICIALES (pasada `G1_gex`, GEX Heatmap, 68 ruedas; `c07_gex.py`, `c15_gex_niveles.py`):** extremos de tramos grandes a
<=2,5 pts del muro verde 6,5 % vs placebo 6,2 % (precio cualquiera 13,3 %); flip 9,4 vs 9,8 %; toques al muro verde: queda del lado
del rebote 51,6 % (placebo 57,4 %); zona verde en el precio -> rango 30 min 12,2 vs 15,1 sin zona vs 16,6 roja, PERO solo en la 2da
mitad (DISEÑO t -0,5; CONFIRM t -4,5); corrimiento del muro verde 10 pts -> retorno 30 min -1,1 pts (DISEÑO t -3,2; CONFIRM t -0,9).
En 8.0.16 las flechas van al lienzo (hijo WinForms), no a la ventana; el GEX Heatmap tapa las marcas de la Regla (registro fijo 27,29).
La raya del precio actual (punteada de 1 px) se confunde con la linea roja punteada: se filtra por grosor.
Pedido nuevo del operador (16-11): sumar la referencia (solo lectura, no tocar su script, las claves las carga el) y trabajar con
POCOS dias a fondo antes de ampliar la muestra; "prohibido darse por vencido".

**Why:** el operador desconfia de los nulos previos; este es un pipeline independiente con 4x la muestra y da lo mismo.
**How to apply:** no presentar strikes/flujo de X-Ray como gatillo; faltan las pasadas GEX Heatmap (muros/flip oficiales), Charm,
Expected Move y Market State (H3, H7, H8, H9, H12) y la grabacion hacia adelante. NUNCA medir con correlacion por rango dentro del
dia variables que son espejo del precio (da t de 10-15 falsos): regresion agrupada con error por dia. Placebo de un nivel elegido
cerca del precio = su ESPEJO, no un corrimiento. Los datos derivados de X-Ray quedan locales (.gitignore): no al repo publico.
Ver [[xray-historia-medida]] (su "19 dias" y "sin hover escondido" quedaron superados), [[rondas-12-14-resultados]],
[[modo-cortes-mouse]], [[atas-8-0-16-ai-connect]].
