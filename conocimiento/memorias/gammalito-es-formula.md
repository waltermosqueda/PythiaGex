---
name: la referencia-es-formula
description: "Ingenieria inversa de la referencia sobre ES (27-10): amarillos = 2 dominantes x 2 instancias (SPX 0DTE / SPY Full), 1 punto por minuto, conversion por minuto; izquierda gamma x (volC-volP); zero = cambio de signo; derecha \"convexidad\" ABIERTA"
metadata:
  node_type: memory
  type: project
  originSessionId: 404dab2f-b5f6-48fa-ba08-3ab764cbbdd8
  modified: 2026-10-27T17:07:28.190Z
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
