---
name: plan-gamma-gex
description: "Su camino para aprender gamma/GEX: gratis primero, GEX Metrix después, GEXbot dentro de ATAS al final."
metadata:
  node_type: memory
  type: project
  originSessionId: f223fe47-37a3-4f31-9f95-4d26b0bbe4d6
  modified: 2026-11-20T19:18:08.292Z
---

Desde el 2026-08-17 está aprendiendo **order flow + gamma/GEX** para operar futuros ES/NQ intradía y scalping. Parte de cero en ambos temas. El plan acordado, por etapas y por costo:

1. Mes 1, gratis: capa gratuita de GEX Metrix + cuenta free de MenthorQ. Único trabajo: anotar cada día de qué lado del nivel de giro de gamma abrió el mercado y si el día fue de rango o de tendencia.
2. Mes 2, USD 19: GEX Metrix Plus (agrega ES y NQ). Dibuja los niveles a mano en ATAS a propósito, y practica entradas en el **reproductor de ATAS**, sin plata real.
3. Mes 3: **GEXbot Classic**, que es el único proveedor con DLL oficial para ATAS y ATAS X. Ahí el perfil de gamma queda adentro de su gráfico. **Precio corregido el 2026-11-20 (medido en su servidor de precios):** lista 50 USD/mes; los 42,50 que muestra la web NO son una promo sino un descuento regional del 15 % (`regional_discount=85`). El dato viejo "42,50 = TRIAL50 a mitad de precio" estaba mal. Planes, qué usa el trader de los videos y qué alcanza: ver [[videos-trader-gexbot-2011]].
4. Recién después, si rinde: MenthorQ Premium + complemento futuros (USD 198/mes reales, no 129) o SpotGamma Alpha (USD 299) por HIRO.

Informe completo publicado como artifact: https://claude.ai/code/artifact/4f5226fa-e995-4954-9db4-bd194b850c9a

**Why:** él quiere "ganar dinero de verdad" y tiende a creer que la clave está en comprar la plataforma correcta. El orden importa más que la herramienta: sin entender el régimen de gamma, ningún dato caro le sirve.

**How to apply:** frenar cualquier impulso de comprar la plataforma cara antes de terminar las etapas. Recordarle siempre que gamma se relaciona con la **volatilidad, no con la dirección**: elige el libro de jugadas del día y el tamaño, no el lado. Ver [[setup-atas-verificado]] y [[como-ensenarle-trading]].

**Curso base publicado el 2026-08-19:** https://claude.ai/code/artifact/26c32238-349d-4d0b-91ae-c4c070e92c3f — gamma/GEX, perfil de volumen y order flow explicados desde cero con sus números reales, más el mapa de equivalencias entre las cuatro herramientas (qué se llama igual en cada una y dónde está). Es el material al que hay que remitirlo cuando pregunte "qué es esto que estoy viendo".
