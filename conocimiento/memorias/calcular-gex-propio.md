---
name: calcular-gex-propio
description: "El método propio para calcular los niveles de gamma, que salió mejor que los cinco tableros pagos y gratuitos."
metadata: 
  node_type: memory
  type: reference
  originSessionId: 46e731fd-f0ba-466c-bd15-979f9343516d
  modified: 2026-08-19T22:28:16.063Z
---

Ningún tablero da la lente correcta para intradía, así que el 2026-08-19 armé el cálculo propio. Es reproducible y es la fuente de niveles que conviene usar.

**Método:**
1. Bajar la cadena cruda de InsiderFinance (`__NEXT_DATA__` → `props.pageProps.initialData.options`, ~25.700 contratos con `gamma`, `impliedVol`, `openInterest`, `bid`, `ask`).
2. Filtrar: vencimientos de las próximas ~2 semanas, **excluyendo el 0DTE ya vencido** (T ≤ 0), y descartar IV inválida (≤0,005 o >3).
3. Repreciar la gamma con Black-Scholes a cada nivel de precio en vez de sumar acumulados: `gamma = φ(d1) / (S·σ·√T)` con `r ≈ 0,034` (sale de la base implícita).
4. `GEX = Σ ±gamma × OI × 100 × S² × 0,01`, positivo para calls, negativo para puts.
5. El **gamma flip** es donde la curva cruza cero, no donde un strike cambia de signo.

**Resultado del 19-ago-2026** (SPX cierre 7708,03): net GEX −$6,11 B · flip SPX 7715,8 = **ES 7737,4** · muros de call 7800/7775/7750 · muros de put 7690/7640. Con MESU6 en 7739,50 esa noche, el precio estaba a 2,1 puntos del flip calculado.

La forma de la curva importa más que los números: fuertemente positiva por arriba (+100 B cerca de 7950) y negativa sostenida por abajo (−58 B en 7460) → **los rallies se apagan, las caídas se aceleran**.

**Why:** los cinco tableros o suman vencimientos irrelevantes, o cuentan 0DTE vencido, o tienen IV rota. El cálculo propio evita las tres cosas y quedó a 11 puntos del mejor de ellos.

**How to apply:** rehacerlo después de las 8:00 ET, cuando la OCC publica el interés abierto nuevo — antes de esa hora todo tablero muestra la estructura de ayer. Ver [[paginas-gex-auditadas]] y [[conversion-spx-a-es]].
