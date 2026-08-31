---
name: centinela-que-mide
description: El centinela anota cada nivel que el indicador promete y mide si pasó; corre solo en el workflow y no concluye nada hasta que hay muestra.
metadata: 
  node_type: memory
  type: project
  originSessionId: a021c566-b4fe-42c5-acd1-0a01a165a079
  modified: 2026-08-31T16:44:23.515Z
---

`centinela.py` es el medidor del proyecto. No adivina dirección: **anota cada nivel que el indicador publicó, mira qué hizo el precio después, y saca la cuenta**. Corre solo en cada ciclo del workflow y escribe el informe una vez por día, a las 20:00/21:00 UTC (después del cierre de 16:00 ET, cuando la rueda ya se puede juzgar entera).

```bash
cd "PythiaGex" && python centinela.py --informe
```

`--todo` junta todas las ruedas; `--fecha 2026-08-29` una sola.

## Los dos controles

- **Calibración** — de los niveles a los que les dimos 70 %, cuántos se tocaron. Necesita que el histórico guarde la probabilidad prometida: eso se agregó el **2026-08-31**, así que las fotos anteriores a esa fecha no sirven para calibrar y el programa lo dice en pantalla en vez de callarse.
- **Qué factor pesa** — 15 hipótesis falsables (0DTE, gamma pin, tamaño de la gamma, OI, régimen, hora de la rueda, distancia en unidades de *expected move*, charm, si la gamma se agranda). Cada una con intervalo de Wilson. **Si los intervalos se pisan, la conclusión es "todavía no se sabe"** y lo imprime.

## Tres trampas que ya mordieron y están arregladas

1. **La rueda en curso se cacheaba.** Quedó congelada en 78 velas cuando había más del doble, y el centinela creía que el precio nunca llegó a niveles que sí tocó. Ahora se vuelve a bajar salvo que la copia tenga menos de 5 minutos (`cli.py` la archiva en cada ronda, así no se le pide a CBOE lo mismo dos veces).
2. **Apendaba sin repisar.** La misma foto se evalúa cada 15 minutos: a las 13:00 una foto de las 12:00 tiene una hora de rueda por delante, al cierre tiene cuatro. Quedaba grabado el veredicto de la evaluación **más pobre** y el archivo entero se sesgaba hacia "no se tocó". Ahora la última evaluación manda.
3. **Los factores incomparables desaparecían sin decir por qué.** Cuando toda la rueda cae del mismo lado (por ejemplo entera en gamma corta) no hay con qué comparar. Se listan aparte en vez de esfumarse.

## La honestidad es parte del diseño

Con una rueda **no se puede concluir nada** y el programa lo dice en la cara. Además avisa al lado del resultado que probando 9 factores, medio hallazgo por corrida puede ser azar puro, y que un hallazgo recién vale cuando se repite en ruedas que no son las que lo encontraron.

**Why:** el proyecto entero existe porque los tableros públicos afirman sin respaldo. Un medidor que se apura a concluir sería exactamente el problema que vinimos a resolver.

**How to apply:** dejarlo juntar ruedas. No leer los "hallazgos" de las primeras sesiones como descubrimientos. Ver [[calcular-gex-propio]], [[auditoria-punta-a-punta]] y [[respaldo-del-conocimiento]].
