# Ronda 16 — resultados

## R16 base (pre-registrada, corrida el 23-11-2026)
49 ruedas de ES (08-09 al 13-11-2026), 662 toques de niveles, 37 con absorcion, 21 operaciones.
| Tramo | Patron (n, media, t, gana) | P1 rebote ciego en el nivel | P2 direccion al azar |
|---|---|---|---|
| EXPLORAR (hasta 09-10) | 10, +0,80 pts, t +0,44, 50 % | 319, -0,58 pts, t -1,57, 33 % | -0,45 |
| CONFIRMAR (desde 13-10) | 11, -3,00 pts, t -2,11, 18 % | 342, -1,00 pts, t -2,80, 29 % | -1,84 |
Lectura: la traduccion mecanica de su patron NO muestra ventaja (confirmacion negativa); n chico (21). El rebote ciego en niveles
pierde en los dos tramos (coincide con rondas previas: "niveles = placebo"). Caveat: sus zonas son discrecionales (las marca en un
grafico mayor: nodos de bajo volumen, gaps, maximos/minimos relevantes), no todos los niveles genericos; opera 1-2 veces por semana
en directo, entre 9:49 y 10:08 NY, y evita la apertura y el medio del rango (analisis L4). La segunda version tiene que salir de
SUS reglas, no de estos resultados, y el tramo de confirmacion ya se uso una vez: la prueba final es hacia adelante.

## R16b — SUS reglas de 2026 (corrida el 23-11-2026; exploratoria: el tramo de confirmacion ya se habia usado en R16 base)
28 operaciones en 49 ruedas (9:45-12:00 NY, max 2/dia, corte tras 2 stops, stop 2-6 pts, costo 0,50).
| Setup | n | media pts | t | gana | azar (contraria) |
|---|---|---|---|---|---|
| TODOS | 28 | +0,46 | +0,30 | 32 % | -0,38 |
| S1 reingreso al area | 14 | -1,20 | -0,60 | 21 % | -1,46 |
| S2 absorcion en VAH/VAL | 5 | +0,60 | +0,17 | 40 % | -0,82 |
| S3 2.a desviacion -> VWAP | 9 | +2,97 | +0,88 | 44 % | +1,55 |
Por tramo: explorar 14 ops -0,88 (t -0,53); confirmar 14 ops +1,80 (t +0,67).
Lectura: ninguna version mecanica de su metodo muestra ventaja estadistica en 49 ruedas de ES (n chico). S3 positivo pero n 9 y
el azar en esos mismos instantes tambien da positivo (+1,55): el dia tenia recorrido. Coincide con sus propias cifras declaradas
(acierto 36-51 %, ~1,5 R medio, expectativa que el mismo estimo en +0,15 R). Prueba que vale: hacia adelante, registrando cada
senal del indicador.

---
## R16c — SUS gatillos de oct-nov 2026 (L5), 49 ruedas de ES, exploratorio (23-11-2026)
Preregistro: PREREGISTRO.md seccion R16c, escrita antes del codigo (`r16_03_gatillos.py`). Prueba de implementacion: en sus dias
verificados los gatillos disparan donde entro el (28-10 C1 9:49:44 contra su 9:50:05; 04-11 C2 9:35:08 contra 9:35:16; C3 9:41:39
contra 9:42:37). Grilla de segundos: cobertura 1,0000 los 49 dias. Costo 0,50. "Azar" = promedio con la operacion contraria.
Crudo (cada gatillo solo, una operacion a la vez), TODO:
- C1 absorcion en el nivel: n 14, media -3,89 pts, t -2,17, gana 14 %. Sin exigir nivel: n 52, +0,61; los disparos A <= 4 pts de un
  nivel -0,71 y LEJOS de todo nivel +2,90: el nivel no suma (si algo, resta).
- C2 barrida y recuperacion (8 pts en 15 s con delta +1.000): n 3. Demasiado raro para decir nada.
- C3 estallido de continuacion (plantilla 8,25/7,5): n 80, +1,36, t +1,61, gana 64 % (hace falta 55,6 % para empatar).
  Sin el filtro de apertura: n 128, +1,04, t +1,55. Por tramo NO repite: EXPLORAR -0,55 (n 53) y CONFIRMAR +2,17 (n 75, t 2,57);
  el tramo bueno junta dias de tendencia fuerte (04-11: 12 disparos, 05-11: 19).
- C4 burbujas (>= 95) y rechazo: n 127, -0,21, t -0,38. Nada.
- C5 ruptura y retesteo del apilado: n 33, +1,85, t +1,21; sin nivel n 73, +1,42.
- Todos juntos, una posicion a la vez, fin del dia tras 2 stops: n 147, +0,58 pts, t +0,95, gana 51 % (EXPLORAR -0,40; CONFIRMAR +1,40).
Lectura: ninguno llega a t 3; su gatillo emblema (absorcion en el nivel, C1) da NEGATIVO y el nivel no le agrega nada. Lo unico
con algo de vida es la continuacion (C3), que se parece mas a "subirse a la tendencia de un dia tendencial" que a leer absorcion,
y no repite entre tramos. Con R16 y R16b: ninguna version mecanica de su metodo muestra ventaja en ES. Queda la prueba hacia
adelante (ruedas desde el 16-11 y el registro del indicador), sin plata.
