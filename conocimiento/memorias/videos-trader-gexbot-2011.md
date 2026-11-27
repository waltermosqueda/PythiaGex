---
name: videos-trader-gexbot-2011
description: "Analisis del 20-11-2026 de 39 videos de un trader que enseña GEXBot + ATAS: usa el plan State (84 % del tiempo en pantalla), precios reales, demo gratis, donde quedo el material y como se midio"
metadata:
  node_type: memory
  type: reference
  originSessionId: f223fe47-37a3-4f31-9f95-4d26b0bbe4d6
  modified: 2026-11-20T19:36:30.905Z
---

El 20-11-2026 el operador bajo 39 videos (17 h) de un mismo canal y pidio saber que plan de GEXBot usa ese trader
para contratarlo sin pagar de mas, y despues una guia tecnica para aprender el metodo.

**Donde esta todo:** `Escritorio\ATAS nada\_referencia\2026-11-20-videos-gexbot\` (material de terceros: local, fuera
de los repos; OneDrive no estaba corriendo, asi que NO esta en la nube). Adentro: `guia-tecnica.html` (14 secciones,
13 capturas), `transcripciones/`, `cuadros/` (1.220), `medicion/`, `web/` (documentacion oficial completa), `scripts/`
y `LEEME.md`.

**Medido (se rehace con los scripts):**
- Ventanas de State en pantalla 84 % de 1.032 min; Classic 63 %; Orderflow 6,7 % (solo su serie de 3 videos); Quant 0.
- Tablero de 4 ventanas sobre QQQ (757 min; NDX 87): Classic + Options Profile + Convexity (gamma) + DEX (delta).
- En su ATAS (MNQ 1 min) el indicador dice "State ZeroGamma" en 83 cuadros, nunca "Classic". Clave Classic no sirve
  para eso segun la documentacion.
- Todo el canal es Nasdaq: ni un caso sobre SPX/SPY/ES.
- El trader es afiliado (enlace + codigo en 35 de 39 descripciones) y vende consultorias.

**Precios (20-11-2026):** lista 50 / 150 / 250 / 350 USD mes (Classic / State / Orderflow / Quant). Al operador le
muestra 42,50 / 127,50 / 212,50 / 297,50 por un descuento regional del 15 %. Con enlace de afiliado, primer pago
14,88 / 44,63 / 74,37 (65 % apilado; Quant sin descuento). Trimestral -10 %, anual -25 %. Fuentes crudas:
`app.gexbot.com/user/subscription/plans` y `app.gexbot.com/regional_discount` (devuelve 85 = se paga el 85 %).
Total del checkout NO verificado (hace falta cuenta).

**Datos utiles del proveedor:** `docs.gexbot.com/llms-full.txt` = toda la documentacion en texto. Demo gratis sin
cuenta: `gexbot.com/tour/classic`, `/tour/state`, `/tour/orderflow` (dia fijo 30-09-2025 sobre SPX, con reproductor).
Dato vivo solo 9:30-16:00 NY. `ES_SPX`/`NQ_NDX` son opciones del indice corridas al futuro, no la cadena del futuro.
Dentro de ATAS no se ve Options Profile ni los puntitos de volatilidad: solo en la web.

**El metodo en una linea:** cinco modelos (venta/compra en call wall, compra/venta en put wall, cruce de un major
short gamma en el medio); el nivel dice DONDE se decide, la confirmacion va por futuros (mapa de calor, big trades
>=125 en MNQ, CVD, footprint) y la DIRECCION sale de un "premercado" que no enseña en los videos.

**Como se midio (sirve para otros videos):** subtitulos con `yt-dlp --write-auto-subs` usando el id que viene en el
nombre del archivo; cuadros con OpenCV; texto en pantalla con el OCR de Windows desde PowerShell 5.1 (~1 s por
cuadro: lee los titulos de ventana "gexbot - QQQ - state - gamma"). El panel del navegador no abre `file://`: para
capturar un HTML local, Edge sin ventana con `--user-data-dir` propio.

**How to apply:** si vuelve a preguntar que plan: State, mensual, un mes, y decidir antes de renovar; nunca
Orderflow/Quant ni trimestral/anual; nada de cuentas duplicadas (el trader lo sugiere; la documentacion dice que el
codigo es solo para primer suscriptor). Recordarle la regla de las 15 sesiones (la carpeta `bitacora\` tiene 6, del
20 al 27-08-2026) y la demo gratis. Ver [[plan-gamma-gex]], [[costo-real-del-retraso]], [[como-ensenarle-trading]].
