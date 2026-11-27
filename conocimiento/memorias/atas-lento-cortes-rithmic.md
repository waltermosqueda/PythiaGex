---
name: atas-lento-cortes-rithmic
description: "22-09: ATAS tildado + 'Market Data Latency' = memoria (9-11 GB de 16) y cortes de Rithmic cada ~60 s desde la apertura RTH; internet medido sano; arreglado con el espacio 'MNQ liviano' (sin MBO DOM ni profundidad de opciones, 4,4 GB) y un solo grafico."
metadata: 
  node_type: memory
  type: project
  originSessionId: 6b882b35-31bc-4131-8cd4-4b099f670c25
  modified: 2026-11-16T17:02:36.240Z
---

Diagnostico del 22-09 (operador en RTH con MNQ, ATAS 8.0.14.399, i3 de 8 hilos, 16 GB, Wi-Fi):

- **Internet sano, medido:** Wi-Fi 99 % de señal, enlace 866 Mbps (5 GHz canal 36); ping 8.8.8.8 21 ms, al servidor de
  Rithmic (54.94.122.132, gateway en San Pablo) 44-61 ms sin perdidas y SIN subir bajo carga (no hay bufferbloat);
  ATAS recibe ~490 kB/s (~4 Mbps) parejo en la rueda. No es la conexion.
  Pings en paralelo 11:55-12:06: router 0,1 % perdidos (1 ms), Google y Cloudflare 0 % (20-26 ms), Rithmic 0,3-0,7 %
  (52 ms) y siempre con Google respondiendo en ese mismo instante: lo poco que se pierde es del camino al gateway de
  Rithmic (o su filtro de ICMP), no del proveedor. Hubo un bache de ~30 s a las 11:51 (rx casi 0, ping 686) sin
  desconexion de Wi-Fi ni corte de Rithmic.
- **El tildado era memoria:** ATAS en 9-11 GB con <1 GB libre -> "Application Hang" 11:22 y 11:25 (visor de eventos).
  Sospechoso principal: el MBO DOM (DomV10.MainIndicator) del grafico oculto MNQ 2m, suscripto a MarketByOrder (no se
  suelta sin reiniciar). Con el espacio nuevo `%APPDATA%\ATAS\Workspaces_v3\MNQ liviano.ws` (copia sin MBO DOM y con
  ProfundidadOpciones=false) ATAS arranca en ~9 GB (carga de los archivos local-*.jsonl de 160-280 MB) y baja solo a 4,4 GB.
  El `Default workspace.ws` todavia tiene el MBO DOM: no volver a abrirlo sin sacarlo.
- **Los cortes de Rithmic** ("Market Data Connection Broken", reconecta en 5 s): arrancan justo con la apertura de NY
  (10:30:57 AR) y caen cada ~60 s o multiplos, siempre entre el segundo :53 y el :04 de cada minuto. Por dia: 15-09 23,
  16-09 271 (profundidad de 444 opciones), 17-09 147, 18-09 72, 21-09 106. No coinciden con "Slow ticks processing"
  ni con busquedas de series; ningun hilo de ATAS esta clavado (max 46 % de un nucleo, total 3,4 de 8).
  Siguieron con 'MNQ liviano' (6 cortes 11:37-11:47) hasta que el operador cerro los graficos MES 5m (soltó 320 opciones
  de ES) y MNQ 2m (11:44-11:45): desde 11:46:57 ninguno (verificado hasta 12:06, ATAS en 3,7 GB).
- Los bucles `cboe_local.py` y `subir_vivo.py` murieron a las 11:25 junto con el ATAS colgado; subir_vivo ya fallaba
  por el token de gh ("NO subio ... GH_TOKEN"). Sin cboe_local las capas CBOE se refrescan desde la nube (8-25 min).

- 14:01 se relanzo cboe_local (BelowNormal) con OK del operador: 14:01-14:22 cero cortes, cero "Slow ticks", ping
  mediana 51 ms, 0,33 % perdidos: queda prendido. Los saltos de RAM de ATAS (2,5 -> 7,7 GB) de esa ventana NO fueron el
  bajador: fue el operador cambiando la sesion del grafico (080000_200000 / RTH / 070000_200000, 14:07-14:15); cada cambio
  reinicia Gamma Hoy y recarga el archivo entero (REBOBINADO de ~12.700 cadenas). Evitarlo en plena rueda.

- 25-09 02:20, "no levanta el Level 2": el heatmap dice "Waiting for Level 2 data (DOM events)" con el Level 1 andando.
  En el log, `get_order_book error : 13` en CADA suscripcion (Prints, Best, Quotes, Summary) desde las 23:57 del 24-09,
  justo cuando caduco la cuenta Lucid; 0 veces el 22 y el 23. La suscripcion que ATAS pide es la misma de siempre y el
  login Rithmic es el mismo (<login>): es el permiso de profundidad del login, no algo nuestro. Reclamo a Lucid.

- 16-11 (en vivo): 0 cortes 10:01-11:31 AR; desde la apertura (11:31) "Connection lost" casi cada minuto, "Slow ticks" de
  10,6 s, "No response to the options request within 120 s", ATAS en 5,9 GB con 2,6 GB libres; el operador reinicio ATAS
  11:49 (volvio en 1,2 GB, prioridad Normal) y no hubo mas cortes. Coincidio con la apertura Y con un workflow mio pesado
  (lo frene). El 13-11 hubo 22 cortes a la misma hora sin ese workflow: causa no aislada. Dos hallazgos firmes: (1) ATAS
  lanzado por la tarea programada de las 10:00 corre en BelowNormal (prioridad de Task Scheduler); (2) los agentes que bajan
  prioridad con ctypes SIN restype/argtypes quedan en Normal. Regla: nada pesado de 09:25 a 16:05 NY con ATAS abierto; si
  hace falta, procesos en IDLE (0x40) con argtypes y un vigia que baja python.exe a Idle.
  Correccion: el ATAS de las 11:49 se COLGO ("Application Hang" 11:52:29 en el visor) y arranco otro a las 11:52:33 (PID 1576).
  Ese crecio de ~1 GB a 10,8 GB privados en 2 h (14:00 AR, 0,5-1,7 GB libres; sin cortes desde 11:52). Espacio activo
  'MNQ liviano.ws' con heatmap OrderBookHistoryDepthHours 12, la referencia, PythiaGex 5, Gamma Hoy prod y Flujo Claro: culpable sin aislar.

**Why:** el operador perdio la rueda con ATAS tildado y pregunto si era su internet; la respuesta con numeros es no.
**How to apply:** ante "latencia"/"se tilda": medir primero RAM de ATAS y memoria libre, cortes por minuto en
`Logs\app_<fecha>.log` y el visor de eventos (Application Hang); menos graficos y menos suscripciones de opciones
antes que cualquier otra cosa. Ver [[auditoria-2026-09-16-0dte-pelotitas-carga]], [[libro-dom-mnq-armado]].
