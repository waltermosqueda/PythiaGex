---
name: la-ola-1-0-instalada
description: "La Ola 1.0 (PythiaOla.dll, indicador aparte del Superindicador): instalada 27-11 07:12 en el grafico de pruebas ESZ6 1m (workspace NewX guardado), verificada contra la cinta (ola, marea, X, mediana y 8 lecturas del panel: 0 diferencias) y el CLIMA con la rueda abierta (11:54, 0 difs); el grafico se congelo 4 h sin dibujar y se destraba cambiando de pestaña por UIA; como verificarla y sus trampas"
metadata:
  node_type: memory
  type: project
  originSessionId: 233bf8d4-5113-4e95-a0d9-68bff726690d
  modified: 2026-11-27T15:04:51.128Z
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
para detectarlo: jsonl de auditoria quieto > 150 s con el csv al dia (`herramientas/vigia_dibujo_ola.py`, pasivo). Causa sin
identificar: el apagado de pantalla (15 min) no cierra con la hora.

**Trampas:**
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
