---
name: profundidad-3-0
description: "Profundidad 3.0 (07-10-2026): pagina local en tiempo real (http://localhost:8765) con el libro de opciones de NQ en profundidad: escalera por strike (OI, vol, gex vol/OI, deltas 5/15/60, zona, fuerza), matriz tiempo x strike, griegas Black-76 completas (gamma, vanna, charm, vega, volga), tres libros lado a lado (NQ vivo, NDX, QQQ con edad), lectura en criollo sin direccion y modo noche; carpeta PythiaGex/profundidad (motor.py, servir.py, pagina/index.html, estado/estado.json); fase 5 = indicador.json cada 5 s desde la 3.0 (3.2.0) compilada y SIN instalar hasta que el operador este sin posicion."
metadata:
  node_type: memory
  type: project
  originSessionId: f5879819-e2a4-405b-87b0-959ace090d7b
  modified: 2026-10-07T03:51:01.311Z
---

**Que es (pedido del operador 06-10 23:50, "hacelo todo"):** la evolucion del tablero: en vez de mas rayas en el grafico, una pagina local que
muestre TODO el libro de opciones y como cambia, de dia y de noche. Inspirada en "la referencia" (terminal de pago sobre QQQ como proxy de NQ,
NinjaTrader; NO se nombra en el repo): escalera por strike, matriz, griegas, "read". Diferencias nuestras: libro de NQ de primera mano por la API
(sin proxy, sin 15 min), cada numero con fuente y edad, y NINGUN score direccional (la referencia misma dice al pie que su read no esta calibrado
contra backtest y no es señal).

**Donde:** `PythiaGex/profundidad/` (motor.py: lee viva3 + estela + log + cboe-local + json de rueda y escribe estado/estado.json; griegas76.py
verificadas contra diferencias finitas; servir.py: http.server en localhost:8765 con /estado.json sin cache; pagina/index.html sin CDN).
Lanzador `herramientas/profundidad.ps1` (pythonw, BELOW_NORMAL; -parar). Esquema del estado en profundidad/README.md.

**Fases:** 1 escalera + deltas (fotos viva3 cada ~70 s, sin tocar ATAS); 2 matriz tiempo x strike; 3 griegas completas + tres libros; 4 lectura
+ noche (resto de ayer como F6_resto); 5 pulso de 5 s: la 3.0 (3.2.0, `GammaHoyTresEstado.cs`) escribe profundidad/estado/indicador.json y el
motor lo prefiere si tiene < 30 s. La 3.2.0 se compila pero NO se instala con posicion abierta (regla de la noche del 06-10).

**Why:** el operador quiere decidir en tiempo real con todas las variables a la vista; lo medido dice que como NIVELES casi nada le gana al
placebo (fase 2, noche, hipotesis), asi que el tablero DESCRIBE (que hay y como cambia) y la caja negra MIDE; nada se llama "zona de interes
validada" sin muestra. **How to apply:** antes de agregar un numero a la pagina, definir su fuente y su edad; antes de llamar "señal" a algo,
pasar por CAJA_NEGRA_PRE.md. Ver [[pythiagex-3-0-estado]], [[como-ensenarle-trading]], [[referencia-sin-nombre]].

**Estado al 07-10 01:25 ART (construido en una noche por 4 agentes + integracion):** corriendo motor (pythonw motor.py bucle --cada 15) y servidor
(servir.py --puerto 8765, 127.0.0.1 y ::1), los dos en BELOW_NORMAL; `herramientas/profundidad.ps1` (-estado, -parar, no duplica). Verificado con
datos reales: griegas vs diferencias finitas error max 3e-7 (el charm de Black-76 NO lleva el termino 2rT del modelo al contado: con el daba 545 %
de error); gexVol reproduce la g de la estela a 0,2-1,0 %; gamma neta del libro entero reproduce el netVol del AUDIT3 a 0,4 %; zonas = estela;
capas con edad desde el ultimo trade de CBOE (470 min de noche) y zero igual a las estelas NDX/QQQ. estado.json ~72 KB cada 15 s; la pagina lo pide
cada 5 s. Pendientes: instalar la 3.2.0 (indicador.json cada 5 s, volAyer) cuando el operador este sin posicion; la pagina solo se vio de noche
(falta verla en rueda); los doms de las capas en la pagina son strikes exactos (sin centroide 12) y pueden diferir unos puntos de la 3.0.

**Independencia (07-10 02:05):** decision del operador: "esta bien, que funcione solo con mi ATAS abierto" (no se extraen credenciales; Rithmic
admite una sesion por credencial, log 06-10 18:16; la API directa la habilita el broker y Lucid es fondeadora). Listo: `herramientas/guardian_atas.ps1`
(cada 60 s: ATAS corre? si no, relanza + login; Rithmic conectado? si no > 5 min, Connect en la fila lucid; la 3.0 late?; escribe
profundidad/estado/guardian.json; -una/-estado/-parar; lo lanza profundidad.ps1), motor `bucle --cada 2` (pid nuevo, BELOW_NORMAL), pagina con
chip "ATAS vivo / CAIDO / Rithmic desconectado" y fuente, **3.2.1** (pulso por cambio: segundo temporizador de 1 s, escribe indicador.json si
CadenaApi.Cambios o el precio cambiaron, Hist.Aplicar con decidir=false, Estado3PulsoSeg) compilada y NO instalada (ATAS en uso: a las 02:09 el
operador tenia abierto el dialogo Indicators de la 2.0, Open PnL 0,00). Instalar con instalar_3_0.ps1 cuando este libre.
**Rediseño (07-10 02:00, pedido: "no te guiaste por los videos; pone el grafico real con las zonas en vez de la lectura"):** workflow
profundidad-como-la-referencia: spec de la UI de la referencia (profundidad/REFERENCIA_UI.md) desde los 5 videos (cuadros cada 2 s) y las 2 capturas;
layout por defecto = escalera heatmap izquierda + mapa visual con velas m2 reales y estelas de niveles a la derecha; pestañas como las de ellos
(ESCALERA, MAPA VISUAL, PROFUNDIDAD con griegas, GAMMA TIME, TRES LIBROS, LECTURA, DIAGNOSTICO); lectura reducida a resumen estructural con nota
"no calibrado"; sin señales ni score. Regla aprendida: cuando el operador da material de referencia, PRIMERO copiar su estructura, despues mejorar.

**07-10 tarde (Opus 5.5 siguio a Fable): grafico tipo TradingView + tiempo real.** Pedidos del operador con sus palabras en profundidad/PEDIDOS_GRAFICO.md (fuente de verdad). Hecho: pagina/grafico_tv.js (motor de gráfico: paneo/zoom/ejes/cruz/leyenda/temporalidades/tipos/dibujos; ?grafico=clasico vuelve al viejo), historia.py (estado/historia.json, 3 sesiones de m2 + estelas, bucle 60 s), la 3.0 **3.4.1** (GammaHoyTresCinta.cs: cada operacion al ms en profundidad/estado/cinta/cinta-NQ-<sesion>.csv 't,precio,dv,lado,id', relleno de la sesion que ESPERA a que no haya posicion), tiempo_real.py **1.2.0** en localhost:8766 (/stream SSE, /velas?seg=N, /salud, /footprint volumen por precio, GET/POST /perfiles -> estado/perfiles_vista.json), lanzador herramientas/profundidad.ps1 con -solo motor|servidor|historia|tiempo_real|guardian. Toques apagados por defecto en la pagina (pedido 13:50). Medido 14:40: servidor archivo->cliente ~2 ms; el reloj de la PC esta 612 ms ATRASADO respecto de NTP (por eso 'ahora - t' da negativo); corregido, operacion en CME -> pagina ~180 ms mediana (lo domina Rithmic/red). Integracion en curso (wf integracion-grafico-pro): menus desplegables, divisores con agarrador en todas las vistas, indicadores editables (perfil de volumen/TPO/VWAP sesion/VWAP anclado clon de PythiaVWAP, espec en profundidad/INDICADORES_ESPEC.md con SUS ajustes de ATAS: VA 70 % global en Platform.cnf, lineas violeta #FF8064A2, sesion 22:00 UTC), perfiles de vista. El operador autorizo reiniciar ATAS aun con posicion ('segui no importa lo que yo haga', 14:35): SL/TP quedaron en el servidor y ATAS los re-tomo ('stop/take is equal with previous opened').
