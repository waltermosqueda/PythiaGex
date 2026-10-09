---
name: delta-vivo-ronda-11
description: "24-09 ronda 11: Delta Vivo 1.0 (atas/DeltaVivo), rombos en el precio exacto para el delta por evento; de 4 familias medidas quedan 2 dibujadas (ola y ola pura) y 3 detectores que NO existen o no se pueden ubicar; instalado junto a Absorcion Viva en el grafico MNQ 1m."
metadata:
  node_type: memory
  type: project
  originSessionId: 6b882b35-31bc-4131-8cd4-4b099f670c25
  modified: 2026-09-24T12:28:57.153Z
---

`PythiaGex\laboratorio\dom\ronda11_delta` (INFORME_DELTA.md) y `PythiaGex\atas\DeltaVivo` (DeltaNucleo.cs puro +
DeltaVivo.cs + arnes; DLL Release 51.200 bytes). Hermano de [[absorcion-viva-ronda-10]]: el nucleo se COPIO, no se
enlazo, para no poder tocar AbsorcionViva que ya esta instalado (Franja y Reloj quedan duplicados a proposito).

- **El hueco que llena**: de los 311 indicadores, 16 nombran el delta y ninguno lo marca en un precio: son panel por
  vela (Delta, CVD, MarketPower...), texto sobre la vela, o ClusterSearch, que es por clúster DENTRO de la vela y se
  resetea con ella. Los 10 que comen la cinta y dibujan en el precio (BigTrades, OrderFlow, ActiveVolume...) disparan
  por VOLUMEN y usan el lado solo para el color. El hueco es de DIBUJO, no de informacion: el dato ya esta en el
  footprint.
- **Se dibuja**: rombo lleno = "la ola" (delta neto de 10 s sobre la vara, crece mientras la ola empuja; azul compras,
  naranja ventas, sin punta a proposito) y rombo hueco = "ola pura" (razon |D|/V >= 0,70; 19,5 % de las olas, 1 por
  sesion). Umbral partido: 1.500 contratos netos en la rueda (percentil 98,9) y 800 fuera (99,5). Densidad medida con
  el mismo nucleo: 241 olas en 23 ruedas, mediana 10 por sesion, 1,06 por hora en RTH.
- **NO se dibuja, y por que**: el contrapie (ola contra el precio) son 8 marcas en 23 ruedas; la traba (mucho delta y
  precio quieto) da 0 con la vara fija, y la version "movimiento neto" que parecia dar 7 por sesion tiene un rango de
  52 ticks: el precio se fue y volvio, no estuvo quieto (la absorcion real ya la marca Absorcion Viva); y el nido
  (todo el delta en un precio) no se puede ubicar: la cinta no dice a que precio se opero cada contrato de una
  agresion que camina el libro, y el precio dominante cambia en el 95,9 % de los casos segun la convencion. Los tres
  se calculan y se anotan igual en el registro.
- **Paridad C#/Python 100,000 %** (5.296 eventos, 100.624 celdas, 3 sesiones, una de la reserva), recorrida por mi
  despues del build limpio. Registro en `%APPDATA%\ATAS\PythiaGex2\delta\delta-<fecha>.csv` con dos filas por evento.
- **Instalado 24-09 09:24** en el grafico MNQ 1m, al lado de Absorcion Viva; `reiniciar_e_instalar.ps1` copia los dos
  DLL en cada reinicio. Primer evento en vivo a las 09:25:18 (ola vendedora, 1.017 contratos netos de maximo).
- Igual que la ronda 10: ninguna familia le gana al costo; el entregable es un instrumento de lectura.

**Why:** cierra el pedido del operador de "algo similar para el delta" con la misma vara de honestidad, y deja
documentado que tres de los cuatro fenomenos que uno esperaria ver NO existen o no se pueden ubicar.
**How to apply:** leerlo junto con la pelotita de absorcion (el rombo es agresion que SI movio; la pelotita es
agresion que NO movio). Ver [[absorcion-viva-ronda-10]], [[flujo-claro-cvd-superador]].
