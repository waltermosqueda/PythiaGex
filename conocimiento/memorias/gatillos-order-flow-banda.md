---
name: gatillos-order-flow-banda
description: "Gatillos de order flow en los extremos de las bandas dominantes: medido contra placebo antes de dibujar; el order flow solo es una moneda, la unica pista es 'tres deltas en contra' con dominante quieta (pocos casos)."
metadata: 
  node_type: memory
  type: project
  originSessionId: 9345174c-7f69-4fe5-a793-976e41f2dc7c
  modified: 2026-09-08T08:03:25.951Z
---

Medido el 2026-09-08 con laboratorio/gatillos.py sobre 15 dias de ES minuto a
minuto anotados por Gamma Hoy (rebobinado-atas-MES-M1: O/H/L/C, vol, ops,
delta, dom0/dom1 por vela). Regla simetrica G a favor antes que G en contra,
placebo = misma condicion con la dominante corrida a otro strike.

- **El order flow solo (delta z, prints grandes por tamano medio, tren de 3
  deltas, divergencia, absorcion) es una moneda**: 46-53 % en cientos o miles
  de casos, sin banda. Ningun gatillo "sirve" por si mismo.
- **La dominante centroide persigue al volumen**: sin filtrar, la banda
  "entra" sola en el precio (588 entradas en 15 dias, nada distinto del
  placebo). Con la dominante QUIETA (<= 2 pts en 5 velas) quedan 99 entradas
  en 14 ruedas americanas.
- **La unica pista**: "tres deltas seguidos EN CONTRA de la llegada ->
  rechazo": 70 % de 27 casos contra 37 % del placebo, en las dos mitades.
  El espejo ("tres deltas HACIA la banda -> continuacion") pierde siempre.
  De noche se diluye (60 % de 35 contra 50 %); en M5 no hay muestra (12
  entradas). Son 13 hipotesis x 10 configuraciones: puede ser azar.
- La absorcion (volumen alto, rango chico) casi no ocurre en la banda: cero
  casos con z>=1,5.

**Why:** el operador pidio "que no sea humo": un detector que marque todo no
sirve. Y "verificar yo, no el usuario": lo que se dibuja tiene que ser lo que
se midio.

**How to apply:** Gamma Hoy 1.3 dibuja SOLO esa pista (triangulo "tren",
ajuste VerGatillos = RechazoTren, dominante quieta 0,026 %, solo rueda
americana) y registra todos los tipos en pythiagex-gatillos-*.jsonl; el
centinela lleva "of" (dmax/dmin, big_n/big_max/big_buy/big_sell por
OnCumulativeTrade) para medir ballenas cuando haya muestra. Volver a correr
`python laboratorio/gatillos.py MES M1 --rth --quieta 2 --velas 5 --todo`
cada semana; hacen falta 60+ casos por tipo antes de afirmar nada. Si pierde,
se saca. Ver [[laboratorio-formulas]], [[centinela-que-mide]],
[[la-muestra-son-niveles-no-minutos]].

**REPLICACION EN NQ (2026-09-08, 13:35, con 13 dias de NDX de Databento):**
MNQ M1, rueda americana, dominante quieta 8 pts, regla 25/25 en 30 min, 296
entradas: NADA le gana al placebo, y "tres deltas en contra -> rechazo" da
42,7 % de 75 casos contra 51 % del placebo (al reves que en ES). Conclusion
honesta: la pista de ES (70 % de 27) NO replica; tratarla como azar. Lo mas
prudente es poner "Gatillos de order flow en la banda" = Ninguno (o cambiar
el default en GammaHoy.cs a Ninguno cuando haya cuota) y seguir registrando.
