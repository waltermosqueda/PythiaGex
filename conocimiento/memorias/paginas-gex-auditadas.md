---
name: paginas-gex-auditadas
description: "Veredicto verificado de las 5 webs de GEX, con las dos correcciones que salieron al ver Options Trading Toolbox logueado."
metadata: 
  node_type: memory
  type: project
  originSessionId: 46e731fd-f0ba-466c-bd15-979f9343516d
  modified: 2026-08-19T23:10:40.666Z
---

Auditadas el 2026-08-19 bajando la cadena cruda de cada una y recalculando el GEX. Dos hallazgos estructurales por encima del ranking:

**1. Un timestamp que avanza no prueba dato fresco.** Medí a las 17:07 y a las 18:23 ET. Opensera: cron real cada hora exacta, números que cambian. GammaLens: reloj +75 min, **cero cambios** en spot, flip y muros. InsiderFinance: congelado y con `isStale:false` a las 2 h 10 min.

**2. El interés abierto no es el mismo entre proveedores.** Options Trading Toolbox, QuantWheel e InsiderFinance coinciden **al contrato** en cada strike del 0DTE (7705 put 1.097 · 7720 put 5.428 · 7725 put 4.271 · 7700 call 2.637). **Opensera reporta otra cosa** (418 · 5.677 · 2.617 · —), y no por escala: en un strike da más y en otro menos. Probablemente ajusta por volumen como dice su marketing, pero entonces su afirmación de usar "datos oficiales de OCC" es engañosa. Sus niveles siguen sirviendo — su Zero Gamma quedó a 11 pts de mi cálculo — pero **no se reconcilian con nadie**.

**Ranking:** 1) Opensera (mejor metodología escrita, único que refresca, da la base ES–SPX; net GEX de titular 100× inflado; OI propio no reconciliable) · 2) InsiderFinance (mejor materia prima, 25.702 contratos, pero **le faltan strikes del 0DTE — 7690/7695/7700** — e interfaz congelada que miente) · 3) Options Trading Toolbox (**mejor OI de los cinco**, refresca cada ~5 min, expone `asOf`; pero `dte=1` devuelve la cadena de hoy y `dte=30` da "No data available"; requiere login) · 4) QuantWheel (OI correcto en 0DTE, pero 13–27 % de IV rota en vencimientos vivos donde Opensera está en 0 %) · 5) GammaLens (flip roto + reloj que corre solo).

**Correcciones que hice sobre mi propio análisis:** dije que el gráfico de Options Trading Toolbox estaba invertido (falso: el agujero era de InsiderFinance) y que mostraba solo 10 strikes (falso: tiene 193, hace zoom automático correcto porque fuera de 7685–7730 la gamma 0DTE es 10⁻⁹). Sus unidades son millones de USD por 1 %, solo que el eje no lo dice.

**Acceso:** la extensión de Chrome no conecta y Edge está en modo lectura. La vía que funciona es **abrirle la página en el panel del navegador de Claude y que él se loguee ahí**. Truco suyo, útil: editar el parámetro en la URL fuerza recarga real del servidor.

Auditoría completa: https://claude.ai/code/artifact/9dd746e5-0a0b-4459-9688-c2adebe4f854

**Why:** pidió que dudara y contrastara. La lección que más vale es que **cruzar proveedores strike por strike detecta lo que ningún gráfico muestra** — agujeros de datos y OI que no coincide.

**How to apply:** antes de confiar en un tablero nuevo, leerle el timestamp dos veces y cruzarle el OI contra otro proveedor. Ver [[calcular-gex-propio]] y [[conversion-spx-a-es]].
