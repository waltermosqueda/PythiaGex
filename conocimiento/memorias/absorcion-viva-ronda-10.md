---
name: absorcion-viva-ronda-10
description: "24-09 ronda 10: Absorcion Viva 1.0 (atas/AbsorcionViva), pelotitas en el precio exacto estilo Big Trades por OnCumulativeTrade; medido en 23 ruedas: ninguna de las 4 familias paga el costo y el esceptico las tumbo a las 4; la absorcion solo se ve encadenando filas de la cinta; instalado en ATAS el 24-09 con el OK del operador."
metadata:
  node_type: memory
  type: project
  originSessionId: 6b882b35-31bc-4131-8cd4-4b099f670c25
  modified: 2026-09-24T09:40:17.243Z
---

`PythiaGex\laboratorio\dom\ronda10_absorcion` (INFORME_ABSORCION.md) y `PythiaGex\atas\AbsorcionViva`
(AbsorcionNucleo.cs puro + AbsorcionViva.cs + arnes; DLL Release 49.152 bytes). Produccion y clon 2.0 intactos.

- **La via de datos**: `OnCumulativeTrade(CumulativeTrade)` trae Time (UTC), FirstPrice/Lastprice, Volume, Direction,
  Ticks y las cuatro puntas PreviousBid/PreviousAsk/NewBid/NewAsk. Es la MISMA via que grabo FlujoClaroSonda, asi que
  cinta grabada y vivo son el mismo dato, y no hace falta MarketByOrder ni profundidad. El Big Trades oficial usa esa
  via y pinta a mano en OnRender (no son objetos de dibujo). El "Absorption" oficial de ATAS es POR VELA y dibuja
  lineas: no cubre esto.
- **Hallazgo que define el detector**: dentro de UNA fila el tamano de la punta despues es el de antes menos el volumen
  (100,0 % de 129.896 filas); una sola agresion mayor que la punta deja el precio quieto en el 0,2 % de 84.105 casos.
  La absorcion SOLO se ve encadenando filas contra el mismo precio (el precio de la punta engancha en el 62,6 %).
- **Medido en 23 ruedas de MNQ (20-08 al 22-09; 14 de reserva sin mirar)**: las 4 familias (muro, iceberg, volumen por
  tick, agresor atrapado) dan neto NEGATIVO con el juez de la ronda 9 y el esceptico tumbo las 4. Lo mejor (t +2,59)
  esta por debajo del techo del azar (2,65 por 143 ensayos). El "muro" parecia +1,535 (t 3,33) pero los eventos ya se
  diferenciaban -1,120 (t -10,72) 60 s ANTES: control mal apareado. Un detector bobo (precio clavado en el extremo del
  minuto, sin mirar el libro) da mas. En la familia 3 el precio sigue del lado del AGRESOR el 67,5 % de los 5 min
  siguientes (control 56,3 %): si algo hay, es continuacion, no rebote.
- **El nivel absorbido NO es soporte**: 95 % roto en 5 min, 98 % en 30, igual o peor que un nivel al azar.
- **El indicador**: pelotita llena en (hora del evento, precio exacto), diametro por contratos comidos, verde = se
  comieron COMPRAS agresoras; aro fino = iceberg (~4 por sesion); circulo punteado = agresor atrapado (APAGADO, sale
  tarde). Umbral partido 150 contratos en la rueda / 90 fuera (MNQ; en MES saca 10-20x mas: subirlo). 38 ajustes con
  prefijo `Abv`. Densidad medida con el mismo nucleo: 24 marcas por sesion, 2,15 por hora en RTH. Nunca corre una marca
  de lugar: fusiona a 2 ticks y topea 6 por vela. Registro de TODOS los eventos en
  `%APPDATA%\ATAS\PythiaGex2\absorcion\absorcion-<fecha>.csv`; se mide con `r10_p_medir.py`.
- **Paridad C#/Python 100,000 %** (34.326 episodios x 16 columnas + 47 atrapados, 3 sesiones, una de la reserva),
  recorrida por mi despues del build limpio. Trampa nueva: pandas lee floats con un parser rapido que se equivoca en el
  ultimo bit; se arregla con `float_precision="round_trip"`.
- **Instalado 24-09 06:32** (el operador lo pidio: "instalalo obvio, si no como vas a probar en tiempo real"): DLL en
  `%APPDATA%\ATAS\Indicators\AbsorcionViva.dll` y agregado al grafico MNQZ6 1m; `reiniciar_e_instalar.ps1` ahora lo
  copia solo en cada reinicio.

**Why:** es el primer indicador del proyecto pedido por el operador por su forma visual (pelotitas como Big Trades) y
el primero que se midio ANTES de dibujarlo; el resultado obliga a presentarlo como espejo, no como senal.
**How to apply:** no venderlo como gatillo; sirve para ver donde el libro se come agresion mientras pasa. Proxima:
misma arquitectura para el DELTA (pedido del operador 24-09). Ver [[estrategia-ronda-9-patron]], [[flujo-claro-cvd-superador]].
