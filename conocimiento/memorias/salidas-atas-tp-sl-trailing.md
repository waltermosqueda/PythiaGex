---
name: salidas-atas-tp-sl-trailing
description: "Gestion automatica de salidas en ATAS (18-09-2026): SL y TP viven en el servidor de Rithmic; breakeven, trailing y OCO corren en la PC (ATAS.Strategies.ATM.StopProfit, Strategies.cnf) y se congelan si ATAS se cierra. Banco de 4.000 entradas al azar en MNQ (salidas_01_trailing.py): ninguna salida cambia el signo; lo que mejor conserva es TP fijo 8/6 o un trailing corto activado TARDE (desde +8); trailing pegado desde la entrada y el breakeven 10T/1T del panel del operador son lo peor (92 % de las operaciones salen a +0,25). Tres plantillas propuestas en ticks."
metadata:
  type: project
---

**Medido en su PC y en la doc oficial (help.atas.net <numero>, <numero>, <numero>, <numero>):** Rithmic = "Server Stops: Yes, Server OCO: No":
el stop y el take enviados quedan en el servidor; breakeven, trailing y la cancelacion cruzada las hace ATAS en la PC (modulo
`ATAS.Strategies.ATM.StopProfit`, estado en `%APPDATA%\ATAS\Strategies.cnf`, reintentos 2 s / 30 s / 0,2 s). Si ATAS se cierra con posicion
abierta: el freno se congela, y si una pata se ejecuta la otra queda huerfana (abrir Orders al reconectar). Tocar SL/TP a mano con posicion
abierta manda la estrategia a Watch Mode (BE y trailing apagados). Multi-level (parciales) no tiene BE ni trailing y con 1 lote no aplica.
Semantica del trailing: la doc da dos lecturas ("Trailing Stop" = ticks a favor que disparan, "Step" = ticks que se mueve el stop; con
Stop = Step el freno viaja a distancia fija = SL inicial) y NO esta medida en su ATAS: se decide con la prueba de Market Replay.

**Banco (laboratorio/gatillo/salidas_01_trailing.py -> resultados/salidas.md; 4.000 entradas AL AZAR, 20 ruedas, grilla 1 s, costo 0,96):**
59 reglas, neto -0,9 a -1,8 pts todas: la salida no pone el signo. Entre las 1.682 que llegaron a +8 sin tocar -6: TP 8/SL 6 conserva 8 el
98 %; trailing D3-D4 activado desde +8 da mediana 7 [5;9], nunca cero; BE +8 + D8 desde +8 mediana 5, p90 18; trailing D3-D4 desde la entrada
conserva >= 8 solo 9-16 % y sale "sacudido" el 78-93 %. Panel actual del operador (SL 150T, TP 200T, BE 10T/1T, trailing 10/1T): el BE saca
el 92 % a +0,25; el TP se toca el 2 %. En estado desatado (rango 5 min >= 48) ningun trailing aguanta (caida mediana de 5 s = 19,5 pts).
**Plantillas propuestas (ticks MNQ):** Scalp fijo SL 24 / TP 32, sin BE ni trailing. Escalera SL 40 / TP 80 / BE 32 con offset 20 /
trailing 4/4 (si Replay muestra que "Trailing Stop" es distancia detras del maximo: 40/4). Desatado SL 32 / TP 48 sin BE ni trailing (o no
entrar). MES: /6, SIN medir. Hotkeys: Flatten y Cancel Stop Orders.

**How to apply:** el operador setea el panel; nadie mas toca ordenes. Antes de confiar: prueba de Replay (¿el stop se mueve a +4 ticks o
recien a +40? ¿salta a entrada+20 en +32? ¿nunca baja?). Ver [[busqueda-gatillo-2026-09-17]] (la apuesta +-8 se resuelve en 48 s; el rango
de 5 min anticipa tamaño, no lado), [[flujo-claro-cvd-superador]] (VELOCIDAD del tablero).
