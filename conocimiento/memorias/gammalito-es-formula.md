---
name: la referencia-es-formula
description: "Ingenieria inversa de la referencia sobre ES (27-10): amarillos = 2 dominantes x 2 instancias (SPX 0DTE / SPY Full), 1 punto por minuto, conversion por minuto; izquierda gamma x (volC-volP); zero = cambio de signo; derecha \"convexidad\" ABIERTA"
metadata:
  node_type: memory
  type: project
  originSessionId: 404dab2f-b5f6-48fa-ba08-3ab764cbbdd8
  modified: 2026-10-28T20:22:50.638Z
---

Pedido 27-10 ~13:40 ART: "analiza profundamente, backtestea hasta encontrar la formula tal como lo tiene dibujado". 10 capturas del operador
(ES 15 s a 5 min, leyendas "la referencia SPX zero — SPX·0DTE" y "la referencia SPY full — SPY·Full") en el scratchpad de la sesion images\1-10.png.
Informe y scripts FUERA de los repos: `Escritorio\ATAS nada\_referencia\2026-10-27-la referencia-ES\` (regla: solo mirar su pantalla, nunca su API).

**Firme (medido por pixel y contra cadenas de CBOE de cboe-local):**
- Cada instancia dibuja SUS DOS perfiles: SPX en K + b cada 5 pts, SPY en J x r cada ~10,08 (en x1 se superponen las dos: "barras dobles";
  SPX encima). Colores verde #089981 / rojo #F23645 (izq.), cian / violeta (der.), 65 %; marcas azules (izq.) y grises (der.).
- IZQUIERDA ("strikes gamma", dicho por el operador) ≈ gamma x (volC - volP) del vencimiento de la instancia (como "la referencia" en sept.).
- AMARILLOS: 2 dominantes por instancia (D1 opaco, D2 al 50 %), UN punto por minuto en la vela hh:mm:00. A las 12:00 NY las de SPX = top por
  |gamma x (volC-volP)| 0DTE (7730/7725...). El tramo 7741 de 09:30-11:12 era SPY 768, no SPX.
- Conversion POR MINUTO con ruido blanco (b 34,5-38,0, sd 0,40): ES del cierre del minuto contra SPX/SPY EN VIVO ~10-15 s mas viejo (no
  reproducible con CBOE de 15 min; la version prolija = base suavizada, ~20-25 % de minutos fuera de 0,5 pt).
- ZERO gris ≈ cambio de signo interpolado del perfil gamma x (volC-volP) 0DTE de SPX, el cruce mas cercano al spot (+0,5-1 pt de sesgo sin cerrar).
**ABIERTO:** barras DERECHAS = "convexidad y sus cambios en vivo" (operador). Descartado su signo por: gamma x OI neto, OI c-p, speed, vol c-p,
vol+OI, cambios en ventanas 5 min-3 h, dIV, curvatura del perfil (mejor 9/13 = arriba/abajo del spot). Regla exacta de D1/D2 y largos (R2) a cerrar.
Ver [[tqqq-referencia]], [[auditoria-2026-09-14-referencia-es]], [[pythiagex-5-0-gamma-es]].

**Cierre del agente (27-10 ~15:00 ART, INFORME.md en _referencia):** IZQUIERDA ENCONTRADA: gamma BS x (volC - volP) por strike, R2 0,998 SPX 0DTE
(14/14 colores) y 0,996 SPY (7/7) contra la captura de 12:33 con la foto de CBOE de dato 12:32; cada lado escalado a su barra mas larga = 0,3 x ancho.
AMARILLOS: las 2 barras izquierdas mas largas de cada instancia (sin importar el lado), dato del final del minuto: 92 % de marcas predichas/observadas,
brillo D1/D2 80 %, minutos 100 % exactos 47 % (el ruido es la b/r de cada minuto: la referencia tiene precios EN VIVO; b 34,5-38,0 ruido 0,40, no
reproducible: 20-25 % de minutos fuera de 0,5 pt aunque el strike sea correcto). ZERO: 73 % <= 0,5 pt, 89 % <= 1 pt con la b del minuto.
DERECHA sigue abierta (digitalizada 12:33 y 12:50; faltan: segunda diferencia, flujo firmado con JSON crudo, cambio de OI de ayer a hoy).
Propuesta (sin implementar): serie opcional "estilo la referencia" en la 5.0 con base/razon suavizadas, avisando que con CBOE va 15 min atrasada.

**28-10 (pedido "quiero las barras de convexidad", 8 capturas ~12:20 NY):** digitalizadas con `scratchpad gl_der_pix.py` (cian (7,174,178), violeta
(93,12,99); la mas larga de cada instancia = 0,3 x ancho, igual que la izquierda). Captura con SOLO SPX 0DTE: 7725 violeta -1,00, 7745 cian +0,44,
7740 violeta -0,39, 7715 +0,29, 7735 +0,23, 7750 +0,22, 7720 +0,19, 7755 +0,15, 7730 +0,10, 7705 +0,07, 7710 violeta -0,07. SOLO SPY Full: 771 -1,00,
770 -0,71, 772 +0,59, 773 +0,17, 768 +0,17, 769 +0,09. Otras 11 estaticas (gex/dex/vanna/charm/speed/vomma x vol/OI, vol-OI, cambio de OI de
ayer a hoy) contra CBOE de las 12:12: SPX mejor 9/11 (gex_oi r 0,35); SPY vanna_oi 6/6 pero en SPX 4-5/11: casualidad. CONFIRMADO: no es estatica.
El JSON crudo de CBOE SI trae por opcion bid/ask/ultimo/hora/tick -> se puede firmar el flujo. Grabador `PythiaGex/laboratorio/la referencia/
grabar_flujo_cboe.py` (cada 90 s, gzip de a trozos, SPX 0DTE + SPY 7 dias -> %APPDATA%\ATAS\PythiaGex5\flujo\) corriendo desde 12:28 NY del 28-10;
`perfil_flujo.py --obs obs.json` compara el perfil firmado (acumulado y ultimos N min) con las barras. Falta: capturas nuevas de cada instancia
sola para medir; si es el acumulado del dia, grabar desde las 09:25 NY.
**Grabacion 28-10 terminada (12:27-16:18 NY):** SPX 155 fotos (296 opciones al cierre), SPY 154 (1218), hueco mediano 90 s, 1 timeout. Firma
(`scratchpad calidad_flujo.py`): SPX ask 34,8 % / bid 37,1 % / medio 16,2 % / tick 5,5 % / sin firmar 6,4 %; SPY 48,8 / 46,5 / 2,7 / 1,2 / 1,0.
LIMITE: todo el volumen nuevo entre dos fotos (SPX ~130 contratos por aumento) toma el lado del ULTIMO trade: firma gruesa. Rithmic NO empeoro:
37 cortes en el dia (18 en las horas de grabacion) contra 104 (48) el 27-10 y 51 el 26-10. NO SE PUDO COMPARAR: las capturas eran de 12:20 y la
grabacion arranco 12:28; no llegaron capturas de la tarde. Siguiente: grabar desde 09:25 NY y pedir capturas de cada instancia sola.
