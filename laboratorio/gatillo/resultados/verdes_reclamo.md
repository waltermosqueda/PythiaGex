# El reclamo en las verdes, en segundos (17-09-2026)

Pedido del operador: "bajemos la temporalidad a segundos y construir un indicador robusto que no falle, y analiza si contra las rayas verdes funciona
aun mejor o es lo mismo... vemos si funciona en tiempo real con la sesion Asia y si lo grafica con exactitud, construir un bot".

## La regla (CONGELADA el 17-09 19:30; cambiarla reinicia la medicion)

Nucleo unico `atas/PythiaGexNiveles/VerdesNucleo.cs` (lo usan el indicador y el banco `atas/VerdesBanco`):
el precio estuvo a >= 10 pts del lado bueno de una verde YA dibujada (hace <= 30 min) y entra a <= 1,5 pts en una transicion desde ese lado; la sigue operacion
por operacion; si la traspasa mas de 10 pts es ROTA; a los 90 s sin reclamo, nada. RECLAMO: al cerrar un segundo (no el de entrada) la cruzo >= 0,5, el cierre
quedo >= 1,5 pts del lado bueno y el delta de los 10 s que terminan ahi va a favor. Entrada en la operacion siguiente; stop 1 pt detras de la mecha (anotado al peor
precio); objetivo 20; vence a los 15 min. Rayas validas: linea de estela de <= 150 s, escrita despues de arrancar, con el futuro del escritor en la misma escala.
Misma raya = a <= 2,5 pts; gracia de 180 s cuando deja de figurar. En paralelo, rayas de CONTROL corridas +-37,5 y +-62,5 (fuera del tunel).

## Lo medido (7 dias con el libro de NQ grabado, cinta orden por orden, costo 0,96)

| que | verdes | control |
|---|---|---|
| banco en Python, por segundo (placebo +-12,5/+-7, ADENTRO del tunel) | +1,40 pts/op (n 158) | -0,51 (n 688) |
| idem, sin 16 y 17-09 | -0,09 (n 103) | -1,04 |
| idem, placebo FUERA del tunel (+-37,5/+-62,5) | +1,40 | +0,85 / +0,88 |
| detector 1.6 tal como corria (porte del revisor) | +0,55 (n 216); sin 17-09 -0,27 | -1,10 |
| **NUCLEO UNICO 1.7, rueda** | **+0,95 (n 198), IC 90 % por dias [-0,43 ; +2,08], 4 de 7 dias** | **-0,23 (n 519), IC [-1,38 ; +0,71]** |
| NUCLEO UNICO 1.7, fuera de rueda | -1,80 (n 107) | -0,47 (n 325) |
| grilla de strikes de 25 pts, 33 ruedas (banco Python) | -0,65 | -0,77 |

Por dia (nucleo unico, rueda): 09-08 +34,5 | 09-09 +2,4 | 09-11 -32,3 | 09-14 -39,9 | 09-15 -2,3 | 09-16 +153,6 | 09-17 +72,4.

**Lectura honesta:** las verdes quedan arriba de cualquier control, pero casi toda la ganancia sale del 16 y el 17-09 (el 17 es ademas el dia que genero la
hipotesis) y el intervalo por dias incluye el cero. Es una PISTA. De noche no hay nada (y el libro nocturno es por interes abierto, nunca medido).
El signo positivo aguanta 243 de 243 combinaciones de parametros (no es sobreajuste fino); el problema es la muestra: 7 dias.

## El juez (criterio escrito ANTES de juntar datos)

`python laboratorio/gatillo/verdes_40_sombra.py` lee el registro en sombra del indicador (`%APPDATA%\\ATAS\\PythiaGex\\flujo\\verdes-NQ-<dia>.jsonl`).
- Cuentan solo las ruedas posteriores al 17-09, 13:30-20:00 UTC, con la regla congelada.
- A las 30 ruedas: SIRVE si el neto por operacion de las verdes tiene IC 90 % por dias arriba de cero Y supera al de las rayas de control del mismo registro.
- A las 20 ruedas: si el neto por operacion es menor que +0,5, SE ABANDONA.
- Potencia: con efecto +1,4 hacen falta ~30 ruedas; con +0,7, ~100. Un bot con ordenes reales se discute recien despues, y seria un ejecutor aparte
  (bracket en el servidor, 1 MNQ, una posicion, tope de perdida diaria, solo rueda), que activa el operador.

## Lo que encontro la revision del detector 1.6 y se arreglo en la 1.7

Dos Gamma Hoy escribian la misma estela con 0,75 pts de diferencia (la raya cambiaba de identidad cada 26 s): ahora un solo escritor por capa (static) y cada linea
lleva el futuro del escritor ("f"), la fuerza ("g"), la gamma neta ("n") y el libro ("b"). El vivo y el banco eran dos logicas (paridad 82 %): ahora un nucleo unico.
Rayas viejas (10 min, de antes del reinicio): ahora <= 150 s y posteriores al arranque. Un solo detector por raiz aunque haya dos graficos (el otro solo dibuja).
Marcas por HORA, no por indice de vela. Rueda / noche y verdes / control separados en la fila AHORA y en el registro (con hora de la PC, bid y ask).
En MES el detector queda apagado con aviso: la capa ES de Gamma Hoy esta en precio de NQ y la replica en ES no replico.

## Incidentes del libro (para leer el registro en sombra con contexto)

- **17-09 noche (vispera de la trimestral):** hasta las 23:07 UTC el libro vivo NO tenia la weekly del viernes 18 (el roll la pedia con U6 y
  Rithmic devolvia la trimestral; arreglado en Gamma Hoy 1.11c, commit c7322f343). Con el arreglo las rayas de esa noche siguieron saliendo de la
  trimestral (1.554 M arriba contra 91 M la noche anterior; tunel ~125 pts): es real, no un error. La noche no decide nada en el criterio.
- **18-09 (rueda 1 de la sombra, vencimiento trimestral):** la trimestral sale del libro a las 9:30 NY en punto; desde ahi el 0DTE es la weekly
  sobre Z6. Sin el arreglo, toda esa rueda habria corrido sin 0DTE y sin aviso.

