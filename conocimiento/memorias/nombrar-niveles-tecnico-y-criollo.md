---
name: nombrar-niveles-tecnico-y-criollo
description: "Todo nivel y toda métrica se entrega con su nombre técnico estándar primero y la traducción en criollo debajo, siempre."
metadata:
  node_type: memory
  type: feedback
  modified: 2026-08-24T17:10:00.000Z
---

Pedido explícito del 2026-08-24: **cada valor, nivel o métrica se entrega con el nombre técnico que usan los expertos, y justo debajo, entre paréntesis, qué significa en criollo.**

No es "o uno o el otro". Son los dos, siempre, en ese orden.

```
Gamma Flip / Zero Gamma — ES 7720
  (la línea que separa el día tranquilo del día bravo)

Call Wall — ES 7800
  (el techo: arriba de ahí la mesa frena las subas)

Put Wall — ES 7650
  (el piso: abajo de ahí la mesa acelera las bajas)
```

Vale para todo: `Net GEX`, `0DTE`, `IV ATM`, `Expected Move`, `Max Gamma`, `Open Interest`,
`Basis`, `Absorción`, `Delta`, `POC`, `VWAP`, `Imbalance`, `Footprint`, `DOM`.

**Why:** quiere poder leer cualquier tablero, video o foro por su cuenta y reconocer el término, pero
necesita el significado al lado para que el concepto se le fije. Darle solo el criollo lo deja aislado
del vocabulario real; darle solo la jerga lo bloquea. Ya dijo "me mareo" cuando fue solo jerga.

**How to apply:**
- El nombre técnico va en **inglés tal como aparece en los tableros** (Call Wall, no "muro de call"),
  porque así lo va a encontrar afuera.
- La traducción va en una **línea aparte debajo**, no al lado — ver [[widgets-sin-choque-de-ui]].
- La traducción describe **qué hace**, no qué es. "El piso donde se acelera", no "exposición gamma negativa".
- Sigue valiendo todo lo de [[como-ensenarle-trading]]: una idea por vez, analogía concreta, cero tablas
  comparativas cuando se está explicando un concepto nuevo.
- En la bitácora y en los archivos de niveles, misma regla.

## SE ME ESCAPO DOS VECES — reforzado el 2026-08-28

Lo pidió el 24-ago, lo guardé, y el 28-ago volví a entregar un análisis con apodos criollos
("el imán del día", "el piso", "el techo pesado") **sin el nombre técnico arriba**. Me lo marcó:
*"siempre te pedí así y lo olvidas"*.

**Chequeo obligatorio antes de mandar cualquier análisis:** recorrer la respuesta y verificar que
**cada** valor tenga las dos líneas. Si un número aparece con apodo pero sin su nombre
institucional en inglés, está mal y hay que corregirlo antes de enviar.

**No alcanza con poner el técnico en algunos y el criollo en otros.** Van los dos, en todos,
siempre, en este orden:

```
Nombre Técnico En Inglés — valor
  (qué hace, en criollo)
```

### Glosario mínimo que debe salir siempre en inglés

`Underlying Price` · `Settlement Price` · `Net GEX` · `Gamma Flip` / `Zero Gamma` ·
`Gamma Regime` (`Long Gamma` / `Short Gamma`) · `Call Wall` · `Put Wall` · `Gamma Pin` ·
`Max Gamma Strike` · `0DTE` · `Front Expiry` · `EOM Expiry` · `Quarterly Expiry` ·
`Open Interest` · `IV ATM` · `Expected Move` (`1-Sigma`) · `Touch Probability` · `Basis` ·
`Gamma Profile Curve` · `Absorción` · `Delta` · `POC` · `VWAP` · `Imbalance` · `Footprint` · `DOM`
