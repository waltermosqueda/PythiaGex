---
name: rondas-26-27-es-mes-y-porcentajes
description: "Rondas 26-27 (27-11-2026): el cruce ES/MES describe quien empuja pero no anticipa la pierna; los porcentajes de largo/corto desde La Ola + regimen + VWAP + flujo NO se cumplen fuera de muestra (panel honesto: 50/50 sin ventaja medida); unica zona sobre 50 % en los tres tramos: voto A3 56,6 %"
metadata:
  node_type: memory
  type: project
  originSessionId: 233bf8d4-5113-4e95-a0d9-68bff726690d
  modified: 2026-11-27T18:45:40.571Z
---

**R26 (`laboratorio/ronda26_es_mes`, 17 pares ES/MES, pre-registrada):** la divergencia es casi siempre "ES sin / MES con" (16 % de los
minutos de la ola, 19-21 % de la marea). El MES va "con agresores" 90-96 % de los minutos porque su agresion llega 1-2 s DESPUES del
precio del ES (eco, 14/14 ruedas); el color del ES lo dan vuelta ordenes de 10-49 contratos vendiendo en la suba; el precio lo corre el
libro pasivo (~3/4 del recorrido, visto 2 de 3). Para anticipar: NO (ES sin/MES con siguio igual que el resto: −0,1 pp ±6,6, t −0,02).
Pista debil al baul (#82): en la marea, cuando empujan solo los chicos del ES, siguio menos (−13,5 a −19 pp, t −1,4/−1,8). Recalculo mio:
16-11 color distinto 28,7 % de 380 minutos (ellos 29 % de 380).

**R27 (`laboratorio/ronda27_porcentajes`, 21.395 minutos de 55 ruedas):** meta simetrica (toca +Y o −Y primero), base 50 %; M0 (geometria)
peor que la moneda; M1/M2 eligen el nulo; M3 (votos T7/V4/A3) confirma (+0,0011) y no repite en la final. Como regla, M3 con 2X: 64,2 %
y +3,53 en 53 ops (t 1,67), final 57,9 %: NO aprueba. Pesa el DIA (desvio 0,07-0,10 contra 0,026 de una moneda) pero no se lee a tiempo;
los rasgos cambian de signo con el mes; el flujo acompaña. Zona A3: 56,6 % (IC90 50,0-63,8; 40 ruedas), la unica sobre 50 % en los
tres tramos. Juez en vivo: `python -I r27_vivo.py` desde el 30-11. Recalculo mio: etiquetas del 16-11 131/258 de 389 = iguales.

**How to apply:** no prometer "porcentajes certeros": lo honesto en el panel es "50/50 sin ventaja medida"; A3 sigue siendo la
candidata del juez en vivo. Lo que sirve de La Ola es leer QUIEN empuja y cuanto falta para el giro (geometria), no la direccion.
Ver [[busqueda-salto-de-calidad-rondas-19-21]], [[baul-de-pistas-no-descartar]], [[explicar-el-movimiento-no-azar]].
