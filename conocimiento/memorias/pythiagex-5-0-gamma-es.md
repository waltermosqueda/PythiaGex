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

**27-10 tarde (estado por si se corta):** control multifuente OK (Yahoo/CME/InsiderFinance; `_diseno/CONTROL_MULTIFUENTE_2710.md`); hallo que las
replicas 2.0/clasica dibujan 1,2 pts abajo y la 2.0 salta 1,1-1,7 pts → agente "replicas con la conversion de la 5.0" (ajuste nuevo
Replicas50Conversion, default true) y agente 5.0.1 (cierres anticipados 27-11/24-12, histeresis del roll por raiz, recuadro x0,5) en curso,
SIN instalar. Pedido del operador: primero terminar y validar la 5.0; la reparacion NDX de la 4.1 (vencido de noche + 3er viernes) DESPUES:
avance parcial guardado en `PythiaGex/atas/_pendiente_4_1_10_parcial.patch` (sin compilar ni probar; la 4.1 quedo restaurada a 31d68ec46).
Instalador: `instalar_5_0.ps1 -Dll <5.0> [-Dll41 <4.1>]` instala las dos en un reinicio. la referencia: ver [[la referencia-es-formula]].

**5.0.1 INSTALADA 27-10 17:04 ART (DLL 00d52c55…, git 585695e1f):** cierres anticipados (27-11 y 24-12 a las 13:00 NY: Cboe/CME/NYSE; SPY SIN VERIFICAR),
histeresis del roll ES 0,25, recuadro LIBRO ES x0,5, y replicas 2.0/clasica con la conversion de la 5.0 (ajuste Replicas50Conversion, default true:
la raya en K + base C7; seleccion y gamma con la base de cada original: mismos strikes, saltos de R20 66 → 0). Probado en vivo: el vencimiento de las
16:00 NY saco las 94 opciones de ES de hoy y paso al 10-28; auditor 20:08-20:10Z CODIGO 0. **Ojo al leer la pantalla:** el renglon naranja
"◀ MM-dd HH:mmZ · fut …" = REBOBINADO (4.1.8): muestra el minuto bajo el mouse, no esta trabada; a minutos del vencimiento los montos explotan
(SPX C 1.221B = 1,2 billones a las 19:52Z): es la gamma con T → 0, no un error. Pendiente: reparar NDX en la 4.1 (parche parcial guardado).

**5.0.2 INSTALADA 27-10 17:21 ART (git 97ad5d4c8):** el operador pidio ELIMINAR (no esconder) la pestaña de detalle "PythiaGex 5.0 ▸/▾" ("poco
profesional, no hace falta"): se saco del codigo (titulo, panel, clic, casilla Cabecera4). La edad del dato arriba a la derecha lleva ahora "MOTOR
PARADO" (rojo) y "mercado cerrado". Cotejado en pantalla 20:23Z: SPX C 3,48B / P −2,36B en 7760,74/7755,74, ES C 156M 7765 / P −118M 7750 = niv.
Preferencia: nada de cuadros de texto largos en el grafico; el detalle va en logs/archivos, no en pantalla.

**5.0.3 INSTALADA 27-10 17:54 ART (git 65fb18fd0, DLL 84625f96…):** dibujo "estilo la referencia" (pedido del operador): barras por strike a la izquierda
(gamma BS x (volC - volP); SPX en K + base C7, SPY en J x razon C8; verde #089981 / rojo #F23645 al 65 %; la mas larga = 30 % del ancho; SPY abajo, SPX
encima), amarillos GL_SPX_dom / GL_SPY_dom (top-2 |valor| por libro; D1 opaca, D2 50 %) y zero gris GL_SPX_zero (cruce de signo mas cercano). Casillas
"GL barras SPX/SPY", "GL amar. SPX/SPY", "GL zero SPX" prendidas (Order 99-103). Probado: 34.598 barras = recalculo Python (error 0); barras contra la
captura de la referencia de las 12:33 NY R2 0,98-0,99 y colores 14/14 SPX, 7/7 SPY; amarillos 80 % a <= 0,9 pt (la referencia tiene dato en vivo: con CBOE
el zero llega 15 min tarde, mediana 1,55 pt). En pantalla 20:58Z: etiquetas GL = niv (GL SPY D1 1,10B 7771,2; GL SPX 0Γ 7753,5). Despues del cierre
el "Hoy" pasa al vencimiento siguiente y los GL quedan lejos (SPX 7770 = 7805,7): la referencia arranca 9:30 NY; recortar a la rueda lo decide el operador.
Pendientes: auditor sin las GL; reparar NDX en la 4.1; "X-Ray Key Levels" quedo cargado en "MES 1m Chart" y "GEX Profile" en el grafico nuevo "MESZ6 5m Chart".
