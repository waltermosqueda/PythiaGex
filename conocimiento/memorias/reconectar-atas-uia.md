---
name: reconectar-atas-uia
description: "Como reconectar ATAS a Rithmic sin reiniciar (06-10-2026): dialogo Connections por UIA; la conexion real es Rithmic 'lucid' (lucid2 tiene las MISMAS credenciales y ATAS la rechaza como duplicada); los clics por coordenadas del lado derecho de los dialogos NO llegan, el InvokePattern si; scripts herramientas/conectar_atas.ps1, fila_en_y.ps1, cerrar_ventana.ps1."
metadata:
  node_type: memory
  type: project
  originSessionId: f5879819-e2a4-405b-87b0-959ace090d7b
  modified: 2026-10-06T21:56:52.797Z
---

**Lo que paso (06-10-2026, 18:14 ART):** ATAS se reinicio solo (crash o relanzamiento: 5 s entre cierre y arranque) y quedo abierto SIN
proveedor de cotizaciones ("To display data, please connect a quote provider"); los tres indicadores dejaron de escribir a las 18:14. En
el log de ATAS (`%APPDATA%\ATAS\Logs\app_<dia>.log`): "Connecting all connectors... ERROR A connector with the same credentials is
already connected ... Connector: lucid2" y luego Rithmic[<login>] Connecting => Disconnected (error). La conexion que usa es la fila
**Rithmic / lucid** (login <login>, LucidTrading Sao Paolo); **lucid2 es un duplicado con las mismas credenciales** y <otra conexion de fondeo> esta
sin auto-connect. No hay nada de esto en los settings legibles: solo en ese log.

**Como se arregla sin reiniciar:**
1. `herramientas/conectar_atas.ps1`: abre Connections por UIA (boton "Connections" del ribbon). En la ventana de login (titulo
   "Authorization") hace Connect/Enter como el instalador.
2. `herramientas/fila_en_y.ps1 -Nombre Connect -Y 355`: los "Connect"/"Disconnect" de cada fila son HYPERLINKS (y un Text con el mismo
   nombre): filas a y=255 Crypto Sim, 280 ATAS Sim 15, 305 dxFeed, 330 Binance, **355 Rithmic lucid**, 380 ATAS Sim, 405 Bitget, 430
   lucid2, 455 <otra conexion de fondeo> (coordenadas UIA, con la ventana centrada). NO elegir por indice: al conectarse una fila su "Connect" pasa a
   "Disconnect" y la lista se corre (me paso: conecte dxFeed y Bitget por error y los desconecte con `-Nombre Disconnect -Y 305/405`).
3. `herramientas/cerrar_ventana.ps1 -Titulo Connections`: WindowPattern.Close. Escape y el clic en "Close" NO cerraron el dialogo.

**Why:** los clics por coordenadas (computer-use) en el lado derecho de los dialogos de ATAS no llegan (la ventana del chat de Claude
tapa esa zona aunque la captura no la muestre); el InvokePattern de UIA llega siempre. Y conectar la fila equivocada mete un proveedor
de cotizaciones extra (dxFeed 15 min) que hay que sacar.
**How to apply:** ante "connect a quote provider" o indicadores que dejan de loguear a la misma hora: mirar `app_<dia>.log` (Connectors),
correr los tres scripts en ese orden, verificar en el log "Rithmic[<login>] connected" y que `pythiagex3-gammahoy.log` vuelva a
armar la cadena. Ver [[pythiagex-3-0-estado]], [[atas-dialogo-indicadores-clics]], [[clics-que-no-llegan-y-loops]].
