---
name: fuentes-tiempo-real-y-xray
description: SPX/SPY en vivo (27-10): no hay fuente gratis publica < 15 min (OPRA); vias con cuenta (tastytrade, Tradier) o pagas; X-Ray de ATAS = analitica de OptionsDepth sobre SPX (no CME), sin API; "Options Chain" en cambio usa ES de CME por Rithmic
metadata:
  type: reference
---

Informe completo: `PythiaGex/atas/PythiaGexCinco/_diseno/FUENTES_TIEMPO_REAL.md` (27-10-2026, fuentes citadas).
- Atraso real medido de CBOE en la 5.0: 919 s SPX, 959 s SPY. OPRA cobra todo lo de < 15 min: solo broker (gratis con cuenta) o vendedor pago.
- Rithmic: sin SPX/SPY en ningun plan. IB como 2do conector de ATAS: USD 1,50/mes OPRA, USD 500 minimo, tope 100 lineas (0DTE ±1 % = 94), TWS abierto.
- Gratis con cuenta: tastytrade (streaming, 25.000 suscripciones) y Tradier (HTTP, 120/min); Argentina elegible segun fuente secundaria. Pagos: MarketData.app
  75, ThetaData 40/80, Databento OPRA 199/mes. Riesgo: capital de Lucid y la declaracion "no profesional" de OPRA (preguntar por escrito).
- **X-Ray (Options X-Ray: ...) = analitica de OptionsDepth sobre datos de CBOE de SPX marcados por participante; dibuja solo en ES/MES/SPY/US500; se recalcula
  cada minuto; sin API ni exportacion: NO puede alimentar a la 5.0, solo usarse en pantalla como testigo.** En cambio los "Options Chain: ..." (GEX Profile,
  Key Levels, OI Profile, Expected Move) usan las opciones de ES de CME por Rithmic (medido 27-10: "20 Nov · 24 DTE · 122 opts").
- Trampa: el boton "Options X-Ray Suite" de la cinta abre la version WEB con la misma cuenta y ATAS desconecta la sesion de escritorio
  ("Logout user error MultipleConnection" → "Server is unavailable"): no usarlo; cargar los indicadores desde Indicators > Options Analytics.
Ver [[pythiagex-5-0-gamma-es]], [[apis-datos-opciones-2026-09-15]], [[databento-cuenta-y-costos]].
