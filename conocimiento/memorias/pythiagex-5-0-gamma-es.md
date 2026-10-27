---
name: pythiagex-5-0-gamma-es
description: "PythiaGex 5.0 \"Gamma ES\" (27-10) = clon de la 4.1 para ES/MES con SPX/SPY; INSTALADA en \"MES 1m Chart\" y auditada en pantalla (auditor codigo 0); trampas de instalacion y pendientes"
metadata:
  node_type: memory
  type: project
  originSessionId: 404dab2f-b5f6-48fa-ba08-3ab764cbbdd8
  modified: 2026-10-27T16:06:07.387Z
---

Pedido 26-10 23:5x: "creame un indicador pythiagex gamma v5 como el de nq pero para es mes spx spy". `atas/PythiaGexCinco`
(PythiaGexCinco.dll, tipo PythiaGexCinco.GammaCinco, datos `%APPDATA%\ATAS\PythiaGex5`, logs pythiagex5-*). Mapeo NQ→ES (Rithmic ×50),
NDX→SPX (CBOE _SPX, archivo "ES", SPXW + SPX AM separadas), QQQ→SPY (razon C8); TQQQ eliminado. Diseño y constantes:
`_diseno/DISENO_5_0.md` §4 (radio ES 25 / SPX-SPY 20, zero 0,5 ±75, CONF 3 y FAM 5 por paso de strike 5 IGUAL que NQ, r−q 3,4 %,
razon 9,95-10,25, SPY solo 04-20 NY, reinicio ex-div 3er viernes), P2/P3/P4/P5_*.md, MEDICIONES_ES.md, INVENTARIO_4_1.md.

**Instalada 27-10 12:53 ART (DLL b06c59c3…, git 6161b2230)** en "MES 1m Chart" en lugar de la 4.1 (que ahi no dibujaba). Defaults: ES muros OI,
SPX muros vol, 3.0 SPX, 2.0 SPX, Clas SPX 0Γ y D1-3; eje izq. SPX. Auditor independiente `herramientas/auditar_familia.py --perfil ES`
(validado en NQ contra la 4.1 al centavo): 15:56-15:59Z CODIGO 0, 34 series 100 %; captura `capturas/2026-10-27_1257_*` con cada etiqueta
cotejada contra el niv (ES C 2,04B=2037,8 en 7760; SPX C/P 85,7B/−17,6B en 7756,12 = SPX 7720 + base 36,12; 2.0/Clasica/3.0 iguales al niv).

**Trampas de esta sesion:** (1) ATAS NO carga un tipo nuevo desde el .ws ("Could not resolve type"): agregar UNA vez por el dialogo Indicators
(buscar "Gamma ES", UIA "Add to chart" + "Apply"); despues si carga del .ws. (2) Lo que la version PREVIA grabo en el .ws pisa los defaults
nuevos por nombre: 5 radios de NQ quedaron grabados → renombrados con sufijo Es. (3) Tras reiniciar ATAS, Rithmic puede dejar MUDAS casi todas
las suscripciones de opciones (184 de 188 sin datos 10+ min; la 4.1 de NQ no): vigia en GammaHoyTres (menos de 1/4 con puntas 3 min → RearmarDeCero).
(4) La extra cboe-local se leia solo al arrancar: faltaba la foto que cboe_local escribia en ese momento (base 0,1 pt corrida) → CerrarHuecoExtra.
(5) Reinicie ATAS 4 veces con una posicion abierta del operador: permitido por su regla, pero avisarlo.
Las replicas (2.0 SPX, Clasica SPX) usan SU base (cascada medida→rueda), 1,5-2,3 pts debajo de la de la familia: es a proposito (copian a las
originales), y por eso el mismo strike SPX 7730 sale en 3 alturas. Todo SIN MEDIR EN ES como nivel.

**Pendientes:** cierres anticipados 27-11 y 24-12 (vencido contado hasta 16:00); histeresis de 1 pt del roll en CadenaApi (ES: 0,25-0,5) antes del
14-12; recuadro 3.0 "LIBRO ES" muestra ×100 (ES es ×50; oculto por llave); SPX y SPY se bajan dos veces (5.0 + cboe_local.py).
La 4.1 de NQ tiene los mismos dos errores de NDX: 0DTE vencido de noche (55,8 ticks el 26-10) y tercer viernes AM+PM sumados: sin tocar.
Ver [[pythiagex-4-0-familia]], [[conversion-spx-a-es]], [[atas-dialogo-indicadores-clics]].
