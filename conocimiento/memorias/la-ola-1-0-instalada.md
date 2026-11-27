---
name: la-ola-1-0-instalada
description: "La Ola 1.0 (PythiaOla.dll, indicador aparte del Superindicador): instalada 27-11 07:12 en el grafico de pruebas ESZ6 1m (workspace NewX guardado), verificada contra la cinta (ola, marea, X, mediana y 8 lecturas del panel: 0 diferencias) y el CLIMA con la rueda abierta (11:54, 0 difs); el grafico se congelo 4 h sin dibujar y se destraba cambiando de pestaña por UIA; como verificarla y sus trampas"
metadata:
  node_type: memory
  type: project
  originSessionId: 233bf8d4-5113-4e95-a0d9-68bff726690d
  modified: 2026-11-27T17:25:34.658Z
---

**Que es:** `PythiaGex/atas/PythiaOla` ("PythiaGex - La Ola (marea, ola y clima)", categoria PythiaGex). Fondo de marea, linea de la ola,
rayas de giro ("ola gira", "marea gira"), rombo de giro de la marea, panel arriba a la izquierda (CLIMA D1 de la ronda 21, MAREA y OLA con
su delta y lo que falta para girar, pie con la X). SIN señales (ninguna valido, ajuste "Señales (en prueba)" apagado y vacio). El
Superindicador volvio a 1.9b (solo velas/delta/perfil/sonda), pedido del operador: "no los mezcles". Construida por un agente 27-11
(motor = copia byte a byte de SuperCalculos; arneses `_test_pythiaola`, `herramientas/auditar_pythiaola.py --r21`, `test_ola.py`).

**Instalada 27-11-2026 07:12 AR** con `herramientas/instalar_pythiaola.ps1` (recarga en caliente) y agregada al grafico de pruebas ESZ6 1m
(trading trabado) TODO por UIA salvo dos clics: la fila del buscador (la grilla AvailableIndicatorsGrid no expone filas) y el "Yes" de
"overwrite workspace" (sin InvokePattern). El buscador se llena con ValuePattern.SetValue (elegir el Edit con ValuePattern: hay dos
PART_Editor). Guardar workspace: el boton "Workspaces" del ribbon con InvokePattern (no Expand, que solo muestra "Edit...") abre la
ventana Workspaces -> "Save" -> "Yes". NewX.ws quedo con PythiaOla (07:19:17).

**Verificacion (27-11 07:17-07:25 AR, pre-mercado):** arneses re-corridos por mi: R21 0 diferencias (ola, marea, clima 390 min en 25-11
y 20-11), test_ola 17 ruedas 4.664.148 campos 0 difs. En vivo: X 10,25 = 0,08 x mediana 126,625 de 10 ruedas (recalculada por fuera
IGUAL); 13 pivotes y 682 fotos contra la cinta de la sonda: iguales; panel: 8 lecturas (ola y marea: dir, raya, faltan, delta)
contra la Maquina sobre las primeras n ordenes de la cinta: 0 distintos (`herramientas/auditar_panel_ola.py`); captura sincronizada
(`herramientas/foto_sincro_ola.py`) 10:24:39 UTC: "ola gira 7.318,75 / faltan 9,75 / Δ +278", "marea gira 7.302,00 / faltan 26,50 /
Δ +3.006" = cinta. El CLIMA no se pudo comprobar todavia (la rueda abre 9:30 NY).

**CLIMA comprobado con la rueda abierta (27-11 11:54 AR, media rueda):** captura sincronizada = la orden 143.573 de la cinta (dos
despues de la linea del jsonl): "ROTACION 1,1x (11:53)", marea/ola/rayas/delta total iguales; clima m=23 recalculado con codigo propio
(rrn 1,1437); 24 minutos de clima, 30 pivotes y 1.910 fotos: 0 difs; panel 16 lineas: 0 distintos; pivotes superpuestos a la captura
y 5 cambios del fondo a 1-2 px. Todo en `laboratorio/bitacora_vivo/la_ola_clima_2026-11-27/VERIFICACION.md`.

**Grafico que deja de dibujarse (27-11):** el ESZ6 1m quedo congelado de 07:29:41 a 11:53 AR (La Ola seguia calculando: su csv al dia,
pero el jsonl, que sale del dibujo, cortado). No es un indicador trabado (CPU normal): ATAS dejo de pintar la pestaña. Lo destraba
cambiar de pestaña por UIA (SelectionItemPattern "Orders" y de vuelta "ESZ6 1m Chart"), sin clics; RedrawWindow NO alcanza. Señal
para detectarlo: jsonl de auditoria quieto > 150 s con el csv al dia (`herramientas/vigia_dibujo_ola.py`, pasivo).
**Causa probable (27-11 14:05):** ATAS NO PINTA los graficos que estan TAPADOS por otra ventana. La PC tiene UN solo monitor
(1600x900) y la ventana de Claude, maximizada (-8,-8,1608,860), queda encima de ATAS (mismo rect) cuando el operador me escribe:
los dos jsonl (ES y MES) cortaron a la vez a las 14:02:48-49 con "al frente: Claude" y el usuario activo, el csv al dia, y la
camara pasiva (PrintWindow) devolvio la misma foto vieja; los huecos del jsonl de hoy (13:41-13:44, 13:57-14:00, 14:00-14:02)
caen cuando el me estaba escribiendo. La mañana (07:29-11:53) seria lo mismo con mi ventana adelante (no verificado: no se
guardo la geometria). Es INOFENSIVO: La Ola calcula igual y al volver a ATAS repinta. Consecuencias para mis herramientas: el
jsonl y la foto pasiva solo valen con ATAS a la vista; el vigia da falsa alarma si ATAS esta tapado (anotar la ventana de
adelante, no "sin dibujar"). Falta confirmar que el jsonl reanuda apenas el vuelve a ATAS.

**ES y MES a la vez (27-11 desde 13:36 AR, el operador con los dos lado a lado):** su captura de 13:46:31 = la orden 238.231 del ES
y la 322.352 del MES al milisegundo (marea, ola, rayas y el "total" del Superindicador −3.491 / +8.068); los dos indicadores auditados
contra sus cintas: 0 difs en lo cubierto (mediana MES 126,50 IGUAL). La divergencia de color (ES ambar, MES verde) es del MERCADO:
misma pierna (7.222,75 a las 12:05), direccion de la marea igual 100 % de los minutos, color 70 %; delta de la pierna ES −2.970
(ordenes de 1-9 contratos +2.355, de 10-49 −5.292) contra MES +6.441 micro (+644 eq ES); ES cruzo a negativo 13:11. La R17 ya midio
que la pierna "sin agresores" NO gira mas que cualquier momento. Si el cruce ES/MES informa: `laboratorio/ronda26_es_mes`.
Evidencia: `laboratorio/bitacora_vivo/la_ola_es_mes_2026-11-27/VERIFICACION.md`.

**Cortes de Rithmic = ordenes perdidas en vivo (27-11):** las dos conexiones se cortaron a la vez 12:47, 12:50 y 13:06 AR; La Ola
ES perdio 533 ordenes en el corte de 12:47 (el pico 7.293,50 nunca le llego: techo 7.292,00, rayas corridas 1,50) y 965 hasta
13:36; se corrigio sola al recargar el grafico (13:37:58, pide la jornada al historial). El csv auditado despues de una recarga NO
muestra el error (se reescribe): el que lo delata es `auditar_panel_ola.py` (el jsonl guarda lo que se mostro) y la cuenta de
ordenes del jsonl contra la cinta. Arreglo propuesto, sin hacer: re-pedir el tramo del hueco tras cada reconexion.

**ATAS mismo pierde las ordenes del corte (27-11):** la vela de ATAS de 12:46 quedo en vivo con 2.483 de 2.873 contratos y cierre
7.292,00 (real 7.292,25) hasta la recarga: NO es solo La Ola, es el grafico entero (velas, footprint, delta, perfil). La ayuda de ATAS
lo confirma (despues de un corte hay que recargar; pedido de recarga automatica "In Progress" en su foro, sin respuesta del staff). El
boton se llama **"Reload all historical data"** (el operador lo uso 14:18 en los dos graficos: en blanco 6 s el MES y 15 s el ES; La
Ola recargo la jornada en 4-6 s). Pedido del operador: "estaria bueno agregarlo automaticamente cuando pase lo de las desconexiones".

**La Ola 1.0b (27-11 14:25, COMPILADA, falta instalar despues del cierre):** `OlaCortes.cs` (DetectorCortes, sin ATAS) +
`PythiaOlaCortes.cs`: lee lo nuevo del log de ATAS cada 2 s ("Market Data Connection Broken" / "Connection lost") y, en la rueda, un
silencio de la cinta >= 20 s; cuando la cinta vuelve (5 s) y pasaron 10 s del corte: con el ajuste "Tras un corte de datos: recargar
el grafico solo" (5. Avanzado, de fabrica SI) ejecuta por dentro el ICommand ReloadDataCommand del modelo de vista del grafico (buscado
por reflexion desde ChartInfo o en el DataContext de las ventanas que apunta al mismo ChartInfo; hilo de la interfaz; sin mouse ni
teclado), como maximo cada 180 s; si no lo encuentra, La Ola re-pide su jornada (como al cambiar la X fija) y el pie avisa "corte de
datos HH:MM: en ATAS Reload all historical data". Prueba sin pantalla: crear `%APPDATA%\ATAS\PythiaGex\pythiaola\recargar-<inst>.txt`.
Arnes: `_test_pythiaola cortes <log> 3 18:15:00 <huecos>` con el log real del 27-11: re-pide en los 5 cortes (04:00, 12:47, 12:50,
13:06 y 14:07 AR) y en ningun otro momento. El motor (OlaMotor/OlaNucleo/OlaClima) no se toco. NO probado todavia adentro de ATAS: la
ruta del comando sale en pythiaola.log ("comando de recarga encontrado: ...").

**Instalada la 1.0b en vivo (27-11 14:44-14:48):** recarga del grafico por dentro PROBADA (prueba por archivo: comando del panel 'ESZ6 1m Chart', 'blank for 4920 ms', nota en el pie). Jornada entera 27-11 contra la cinta completa: csv de ES y MES TODO IGUAL (224 min de clima); el panel solo difirio en los cortes (12:48-13:36 ES, 14:08-14:17 ES y MES) y en 6 lineas con la ultima orden llenandose (1-5 contratos de delta). Foto de la camara 14:51:47 = cinta al milisegundo en los dos cuadros. Evidencia: `laboratorio/bitacora_vivo/la_ola_es_mes_2026-11-27/VERIFICACION.md` secciones 7-9.

**Trampas:**
- La X del MES puede salir distinta de la del ES por el redondeo al tick: medianas 126,50 / 126,625 -> X 10,00 / 10,25 (limite
  126,5625); el 27-11 le dio al MES 2 giros de ola de mas. Con "X fija" 10,25 en el MES caen en los mismos minutos.
- Para medir pixeles en la captura: ATAS escribe la etiqueta de hora del eje ~5 px a la IZQUIERDA del centro de su vela; anclar el eje x
  en un pivote (quiebre de la linea), no en las etiquetas.
- El control de panel de `auditar_pythiaola.py --jsonl` compara el panel en vivo (mitad de minuto) con la foto del CIERRE del minuto:
  da "diferencias" falsas. Usar `auditar_panel_ola.py` (mismo instante por cantidad de ordenes n).
- El auditor deduce el fin de la cinta del NOMBRE (redondeado al minuto): la ultima foto del csv puede quedar "sin cubrir" (1 dif falsa).
- Con la pantalla apagada por ahorro de energia (15 min en AC), computer-use y PrintWindow devuelven el ULTIMO CUADRO VIEJO (vi 04:29
  a las 07:10). SetThreadExecutionState no la prende; un SendInput de mouse de 0 px si (tamaño de INPUT en x64 = 40 bytes).
- 27-11 es media rueda (cierra 13:15 NY): la curva f(m) del clima supone rueda entera; ese dia el clima no es comparable.
Ver [[busqueda-salto-de-calidad-rondas-19-21]], [[ronda-18-senal-ola-y-indicador-aparte]], [[foco-atas-atajos-trading]],
[[atas-dialogo-indicadores-clics]], [[comprobacion-fehaciente-en-pantalla]].
