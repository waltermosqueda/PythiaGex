---
name: foco-atas-atajos-trading
description: "PELIGRO (16-11 y 26-11): si le doy el foco a un grafico de ATAS mientras el operador escribe o dicta, SUS teclas disparan atajos de trading (A = Buy Bid, S = Sell market, D = Sell Ask, Q/U = cancelar/cerrar, E/C = Cancel All, T = panel). El 26-11 SALIERON 2 Buy Limit reales (canceladas sin fill): nunca clic ni arrastre en ATAS con el operador en la PC"
metadata:
  node_type: memory
  type: feedback
  originSessionId: b1346044-cbec-4f19-80c3-ffb1de06776c
  modified: 2026-11-27T00:49:10.450Z
---

**DECISION DEL OPERADOR 27-11-2026 ~14:45 AR (manda sobre la espera de 20 s):** "no esperes 20 segundos, saca esa regla; si yo te lo pido hacelo ahora, si yo te freno explicitamente es otro tema" y "hacelo vos todo, te di los permisos, yo quiero analizar el grafico". Desde ahi: instalar/recargar en caliente SIN esperar quietud (instalar_pythiaola.ps1 QuietoSeg 0 de fabrica), apretando yo el boton de recarga y el OK por UIA. Lo que SIGUE valiendo: nunca apretar un boton ambiguo (el 27-11 14:30, con ATAS minimizado, la busqueda vieja apreto el scroll del MES); devolver el foco a la ventana que lo tenia; y avisarle en una linea que va a aparecer un cartel de ATAS.

16-11-2026 01:12-01:13 ART (log `app_20261116.log`): traje al frente mi ventana flotante de trabajo (`SetForegroundWindow`) para
usar el dialogo Indicators mientras el operador escribia un mensaje en el chat. Sus teclas cayeron en el grafico: `[Hot-Key] Down
'A'` -> `Trading manager: Buy Bid click`, 'D' -> Sell Ask, 'E' -> Cancel All, 'S' -> Sell market; 10 intentos, todos con
`Trading is locked` (no salio ninguna orden; verificado linea por linea). El dialogo ademas se cerro solo y mis propios clics/teclas
siguientes cayeron sobre el lienzo.

**26-11-2026 21:45:43 ART, SE REPITIO Y ESTA VEZ SALIERON ORDENES (log `app_20261126.log`):** yo arrastraba la barra de
desplazamiento del grafico limpio "MES 1m Chart" con computer-use para probar el Superindicador mientras el operador dictaba/escribia
(teclas `IsManual=False`, " a la que pertenece..."). Ese grafico NO tenia el trading trabado: 'A' -> dos Buy Limit de 1 MES a 7246,75
en su cuenta de fondeo, ACTIVAS en el mercado ~1 s; 'Q'/'U' -> cancelacion + "Close position"; 'E'/'C' -> Cancel All; 'T' -> abrio el
Trading Panel. Sin fills (verificado: "vol 1/1 ... Done", ninguna operacion, saldo igual). Se lo dije en el acto.
Mi error: confie en [[usar-pc-sin-esperar]] y no mire GetLastInputInfo ni el candado del grafico. ESA REGLA NO PISA ESTA.

**Why:** con el grafico destrabado salen ordenes reales (paso el 26-11). Los atajos son de UNA letra.
**How to apply:**
- NUNCA darle el foco a ATAS (ni `SetForegroundWindow`, ni clic NI ARRASTRE de computer-use, tampoco en la barra de
  desplazamiento) mientras el operador esta en la PC, salvo que el diga que saco las manos del teclado; antes de cualquier clic
  mirar `GetLastInputInfo` (>= 60 s) y que el grafico diga "Trading is locked". Si el grafico de pruebas no esta trabado, pedirle
  que lo trabe antes de tocarlo. Al terminar, devolver el foco al chat de Claude, NO a ATAS (`xr.al_fondo` ya lo hace asi).
- Para mover un grafico sin foco: preferir UIA o mensajes de ventana; si no hay forma pasiva, pedirle al operador que lo mueva.
- Preferir siempre lo pasivo: `PrintWindow` + `PostMessage` (teclas/arrastre virtual) no cambian el foco.
- No tipear texto en dialogos de ATAS si no verifique en una captura que el dialogo sigue abierto.
- Mi grafico de trabajo se mantiene SIEMPRE con trading trabado; jamas tocar "Unlock trading".
- En "Connect AI agent > Access" el permiso de ordenes del agente estaba ENCENDIDO el 16-11: recomendarle apagarlo; yo no envio ordenes.
Ver [[no-mover-mouse-si-opera]], [[modo-cortes-mouse]], [[atas-8-0-16-ai-connect]], [[clics-que-no-llegan-y-loops]].
