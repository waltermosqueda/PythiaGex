---
name: xray-historia-medida
description: "13-11: X-Ray guarda historia por dia (Flow Delta, Flow por participante, Hedge Pressure, tira GAMMA/FLOW del Market State) y se lee por PIXELES (una captura por dia); 19 dias medidos: NO da direccion mas alla del precio (Hedge Pressure/Flow Delta acumulado = espejo de precio vs VWAP); el regimen gamma NO cambia la reversion. El 'SI da tamaño' del 13-11 quedo DESMENTIDO el 16-11 (R3: era noche contra dia y el reloj)"
metadata:
  node_type: memory
  type: project
  originSessionId: f3419dc9-e1cb-4b1b-99aa-8707c1a3c2f4
  modified: 2026-11-17T03:11:12.303Z
---

Pedido 13-11 ("analiza profundo el options analytics de ATAS, grafico limpio, construyamos un bot superador").
Codigo y datos: `PythiaGex/laboratorio/xray/` (x05_paginas.py captura un dia por pagina del grafico flotante
"ES 5m Chart" maximizado; x06_pixeles.py lee paneles + OCR del eje (error < 1 px); x07/x08/x09/x10 = analisis;
export_es5m.csv = velas ES 5m 18-10..13-11 exportadas con clic derecho > Export (solo OHLC, NO indicadores, hora ART
formato yyyy-dd-MM)).

- Inventario Options Analytics (16): Options Chain (Expected Move, GEX Profile, Key Levels, OI Profile) y X-Ray (Big
  Trades, Charm/GEX/Strike Heatmap, Expected Move, Flow, Flow Delta, Hedge Pressure, Key Levels, Market State, Strike
  Profile GEX/DEX/VEX/CEX, Surface Profile). Strike/Surface Profile tienen FollowCursor (historia al pasar el mouse,
  ~0,6 s por posicion: lento; x01_cosechar.py + OCR). Options Chain NO tiene historia (siempre el dato actual).
- Medido (19 sesiones 19-10..12-11, diseño 9 / examen 10, un dia un voto): Hedge Pressure (nivel) y Flow Delta acumulado
  "predicen" r5/15/30 (rho -0,10..-0,19 y +0,07..+0,14 en examen) PERO correlacionan +0,57/-0,39 con precio-vs-apertura;
  parcial controlando precio = 0. El precio solo (vs VWAP) revierte (examen t -3,5) pero la regla k=15/stop=10 elegida en
  diseño PIERDE en examen (-2,18 pts/op, 36 %, 2/10 dias). GAMMA "amplifica" vs "amortigua": misma reversion (-0,44/-0,42).
  Unico aporte propio: volumen de opciones 0DTE -> rango 30 min mas alla del rango previo (parcial +0,25 t 4,1 / +0,23 t 4,8).
- Trampas: la ventana del chat de Claude no sale en las capturas pero TAPA clics (moverla a x>=1240 fisico); el panel de
  Flow Delta mide 58 px (resolucion gruesa); sin escalas (ejes bajo el chat) -> pixeles normalizados por dia; latencia de
  X-Ray en vivo SIN medir (posible mirada adelante en lo historico).

**CORRECCION 16-11 (R3 del informe diario, 67 ruedas, dos verificadores):** el "SI da tamaño" de arriba es FALSO. El volumen
0DTE de X-Ray mezclaba velas de noche (cancha vacia) y de dia: solo en la rueda da +0,03 y con la hora del dia 0,00. Ninguna
serie de X-Ray le gana a su espejo de precio en tamaño (0 de 12); lo que parecia andar (volumen del ES, |brokers|) era el reloj.
Pros +0,529 con sus mismos 15 min (copian), +0,31 con los 15 previos, -0,03 con los siguientes; retail -0,679; market maker
(Hedge Pressure) -0,57 (37 ruedas); firms +0,05 (cambia de signo), brokers -0,12. Direccion: 49-51 % todos; 0 de 42 pruebas.
Detalle: `PythiaGex/laboratorio/informe_diario/r3_xray/xray_hallazgos.json` y sus `verificacion_*/veredictos.json`.

**How to apply:** X-Ray relata lo que el precio ya hizo: no da lado NI tamaño; no venderle un gatillo de X-Ray. Para el tamaño
dentro del dia manda el reloj (ver [[straddle-no-es-el-rango]]). Pendiente: medir el atraso de X-Ray en vivo. Ver
[[busqueda-gatillo-2026-09-17]], [[pythiabot-1-0]], [[informe-diario-es]].
