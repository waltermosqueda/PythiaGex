---
name: como-dibujan-los-pros
description: "Como presentan los niveles de gamma en el grafico los productos reales (GEXBot, SpotGamma, MenthorQ, TanukiTrade, BackQuant, TLADe, Volland): pocas lineas, nombres estandar, etiqueta corta a la derecha, vencimiento cercano para intradia. Investigado el 2026-09-07 con fuentes."
metadata: 
  node_type: memory
  type: reference
  originSessionId: 9345174c-7f69-4fe5-a793-976e41f2dc7c
  modified: 2026-09-07T05:54:57.863Z
---

Investigado el 2026-09-07 a las 03:00 despues de que el operador dijera
"demasiadas rayas, caotico, fijate como lo hacen otros".

## Lo que tienen en comun

- **Tres a siete lineas, nunca mas.** GEXBot: Zero Gamma, Major Positive,
  Major Negative, y el perfil en barras al costado. SpotGamma: Zero Gamma,
  Vol Trigger, Call Wall, Put Wall, Risk Pivot. MenthorQ: Call Resistance,
  Put Support, HVL, mas 1D Max/Min y las variantes 0DTE.
- **Nombres estandar**: Zero Gamma / HVL / Gamma Flip; Call Wall / Call
  Resistance; Put Wall / Put Support; Key Gamma Strike (pin); Hedge Wall.
  Verde arriba, rojo abajo, blanco el zero.
- **Trazo**: MenthorQ punteado para Call Res / Put Support; TanukiTrade
  solido para C1-C3/P1-P3 y RAYADO para los secundarios; GEXBot puntos.
- **Etiqueta corta a la derecha** con nombre y precio; los datos finos en
  panel o tooltip. BackQuant fusiona etiquetas coincidentes ("Max Pain /
  Call Res") con tolerancia de 0,50.
- **Intradia = vencimiento cercano.** "Top 5 del 0DTE" para no saturar
  (BackQuant); MenthorQ separa Call Res 0DTE, Put Sup 0DTE, HVL 0DTE y
  Gamma Wall 0DTE (10 strikes rankeados).
- **Zonas, no filos**: los niveles respetados son grupos de strikes.
  TanukiTrade pinta caja de transicion entre P1 y C1 y avisa que se apaga
  si distrae. TLADe: "walls flip role when price crosses them".
- **Ninguno tiene volumen de opciones en vivo por strike.** Eso es nuestro.

## Fuentes

- GexBot Gamma Point (Quantower): https://github.com/The-R2D2-code/Quantower_GexBot_Gamma_Point
- SpotGamma GEX: https://spotgamma.com/gamma-exposure-gex/
- MenthorQ terminos: https://menthorq.com/guide/key-levels-and-key-terms/
- MenthorQ en ES: https://menthorq.com/guide/gamma-levels-on-es/
- GEX Profile PRO (TanukiTrade): https://www.tradingview.com/script/v04Kzl4Q-GEX-Profile-PRO-Real-Auto-Updated-Gamma-Exposure-Levels/
- BackQuant GEX Levels: https://www.tradingview.com/script/nyyInUl8-Gamma-Exposure-Levels-BackQuant/
- TLADe NT8: https://tradelikeadealer.com/nt8
- InsiderFinance (zonas): https://www.insiderfinance.io/resources/how-to-read-gamma-exposure-gex-like-a-pro
- Volland user guide (PDF, 403 al bajar): https://vol.land/VollandUserGuide_Jun24.pdf

**Why:** el indicador propio llego a seis lenguajes visuales a la vez
(nodos, niveles G, zero, barras, zonas del radar, estela, pelotitas). Los
productos que se usan para esto no pasan de tres a siete lineas.

**How to apply:** las ocho maquetas calcadas de estas fuentes estan en la
pagina "Como lo Dibujan los Pros" (artifact, 2026-09-07). El operador elige y
se pule. Ver [[produccion-en-vivo-2026-09-06]] y [[que-afirma-gammalito]].
