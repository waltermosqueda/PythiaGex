# Familia BARRIDOS, ORDENES GRANDES Y RAFAGAS — busqueda de gatillo para acompañar al CVD (MNQ, 17-09-2026)

Fuente de todo: tabla de 1 segundo de `base.py` (cinta del propio ATAS del operador, 20 sesiones MNQ del 20-08 al 17-09). Codigo: `barridos_lib.py`.

## 1. PRE-REGISTRO (escrito ANTES de mirar un solo resultado)

Lo unico que se miro antes de escribir esto fueron **conteos de disparos** (cuantas veces se da cada condicion en las 12 sesiones de explorar,
`barridos_conteo.py`), para no registrar variantes sin muestra. Con esos conteos se aflojaron cuatro umbrales (rango chico p30->p40, rafaga
unilateral 0,30->0,25, d50 aislado 100->75 contratos y 120->60 s, V12 pasado a d50 solo con p50/p50) y V04 se redefinio (de "sin avance", 26 casos,
a "barrido devuelto"). Ningun resultado (barrera, retorno futuro) fue calculado hasta cerrar este texto.

### Convenciones comunes
- Todo se calcula en el segundo t con filas <= t. Ventana movil de k s = suma de [t-k+1, t]. Umbral "pXX previo de w s" = percentil XX de la serie en [t-w, t-1].
- Entrada = `ultimo[t]`; resultado con `B.barrera` (mira de t+1 en adelante).
- Solo rueda 13:32:00-20:00:00 UTC. Desagrupado: minimo 60 s entre disparos de la misma variante (gana el primero).
- `bn10` = suma 10 s de (barre_c - barre_v); `bt10` = suma 10 s de (barre_c + barre_v).
- BARRIDO GRANDE en t: |bn10| >= p99 previo de 1800 s de |bn10|, y |bn10| >= 0,6 x bt10 (unilateral). side = signo(bn10).
- RAFAGA en t: n10 (ordenes en 10 s) >= p99 previo de 1800 s de n10, y |delta10| >= 0,25 x vol10. side = signo(delta10).
- mov300 = ultimo[t] - ultimo[t-300]. CLIMAX(side): side x mov300 >= p80 previo de 3600 s de |mov300| (el flujo llega DESPUES de un movimiento largo a su favor).
- Rango previo rp = max(alto) - min(bajo) en [t-310, t-11]. RUPTURA(side): rp <= p40 previo de 3600 s de rp, y ultimo[t] fuera de ese rango del lado de side.

### Las 12 variantes (lado = lo que dice el gatillo)
| # | Nombre | Condicion | Lado |
|---|---|---|---|
| V01 | BARRIDO_SEGUIR | barrido grande | side |
| V02 | BARRIDO_CLIMAX_CONTRA | barrido grande + CLIMAX(side) | -side (agotamiento) |
| V03 | BARRIDO_RUPTURA_SEGUIR | barrido grande + RUPTURA(side) | side |
| V04 | BARRIDO_DEVUELTO_CONTRA | para cada segundo i con barrido grande: primer t en [i+1, i+60] con side x (ultimo[t] - ultimo[i-10]) <= 0 (el precio devolvio todo el barrido) | -side |
| V05 | RAFAGA_SEGUIR | rafaga | side |
| V06 | RAFAGA_CLIMAX_CONTRA | rafaga + CLIMAX(side) | -side |
| V07 | RAFAGA_APAGADA_CONTRA | tras un segundo de rafaga i: primer t en [i+5, i+60] (sin otra rafaga en el medio) con n10 <= p50 previo de 1800 s | -side |
| V08 | GRANDES_VS_CHICOS | G60 = suma 60 s de d50; C60 = suma 60 s de (d1 + d2_4). |G60| >= p95 previo 1800 s, |C60| >= p50 previo 1800 s, signos OPUESTOS | signo(G60) |
| V09 | GRANDES_CON_CHICOS | igual que V08 pero signos IGUALES | signo(G60) |
| V10 | D50_AISLADO_CONTRA_TENDENCIA | |d50[t]| >= 75, ningun d50 del mismo signo en [t-60, t-1]; tendencia previa = ultimo[t-1] - ultimo[t-301]; -signo(d50) x tendencia >= p60 previo 3600 s de |mov300| | signo(d50) (giro) |
| V11 | D50_AISLADO_A_FAVOR | igual que V10 pero signo(d50) x tendencia >= p60 | signo(d50) |
| V12 | GRANDES_DIVERGEN_300 | G300 = suma 300 s de d50; |G300| >= p50 previo 3600 s; signo(G300) = -signo(mov300); |mov300| >= p50 previo 3600 s | signo(G300) |

Cada variante prueba a la vez su espejo (ir en contra = 100 - acierto), asi que son 24 pruebas efectivas; con 24 pruebas, un |z| de 2 aparece por azar
mas de la mitad de las veces. Se juzga con |z| y el lado queda FIJADO en explorar.

### Regla para elegir finalistas (fijada ahora)
1. Barrera principal +-8 / 600 s, solo en `B.explorar(T)`.
2. Candidata = n resueltos >= 150, acierto del lado elegido >= 55,3 % (empate), y >= 70 % de los dias del lado correcto (> 50 %).
3. Entre las candidatas, las 2 de mayor |z|. Desempate: mejor acierto en +-12 / 900 s.
4. Si NINGUNA cumple, se llevan igual a confirmacion las 2 de mayor |z| con n >= 150, rotuladas "no cumplio la regla"; su veredicto maximo es PISTA.
5. Los finalistas se corren UNA vez en `B.confirmar(T)` con el lado fijado. Placebo: mismos disparos por sesion y media hora, mismos lados, segundos al azar
   (con los mismos 60 s de separacion), 200 sorteos.
6. Veredicto: SOBREVIVE = acierto >= 57,3 %, z contra placebo >= 2,5, >= 6 de 8 dias arriba de 50 %, n >= 100. PISTA = positivo pero falla un criterio. NADA = el resto.

Secundarias que se reportan para todas: +-5 / 300 s (empate 58,5 %), +-12 / 900 s (53,5 %) y movimiento medio firmado a 10/30/60/120/300 s.

### Sesgo conocido de la referencia de entrada (anotado antes de medir)
Despues de un barrido comprador `ultimo[t]` queda en el ASK. Tocar +8 exige que el mercado suba 8 enteros; tocar -8 se logra con el bid 7,75 abajo.
Eso le regala ~0,8 puntos porcentuales al que va EN CONTRA del barrido y se los quita al que lo sigue. Un "contra" de 51 % no es nada.

---

## 2. EXPLORAR (12 sesiones, 20-08 al 04-09) — `barridos_explorar.py`

Lado segun el pre-registro. Un acierto < 50 quiere decir que el ESPEJO (hacer lo contrario) acierta 100 - ac. Empates: +-8 -> 55,3 % | +-5 -> 58,5 % | +-12 -> 53,5 %.
Deriva de la muestra (todos los segundos de rueda, +-8/600): sube primero 51,5 %.

| Variante | disparos | n (+-8) | acierto +-8 | z | dias > 50 % | +-5/300 | +-12/900 | mov medio a favor 10 / 30 / 60 / 120 / 300 s (pts) |
|---|---|---|---|---|---|---|---|---|
| V01 BARRIDO_SEGUIR | 375 | 373 | 53,1 | 1,19 | 7 de 12 | 51,3 | 53,8 | 0,17 / 0,06 / -0,07 / -0,42 / -0,12 |
| V02 BARRIDO_CLIMAX_CONTRA | 191 | 189 | 51,9 | 0,51 | 6 de 12 | 55,6 | 50,5 | 0,36 / 1,02 / 1,33 / 2,28 / 2,22 |
| V03 BARRIDO_RUPTURA_SEGUIR | 158 | 157 | **58,0** | 2,00 | 10 de 12 | 56,1 | 53,2 | 0,06 / 0,09 / -0,29 / 0,02 / 0,54 |
| V04 BARRIDO_DEVUELTO_CONTRA | 173 | 172 | 49,4 | -0,15 | 3 de 11 | 48,6 | 51,4 | 0,00 / 0,74 / 0,53 / 0,18 / 0,84 |
| V05 RAFAGA_SEGUIR | 275 | 273 | 54,9 | 1,63 | 8 de 12 | 52,6 | 54,4 | -0,05 / -0,28 / -0,30 / 0,30 / -0,31 |
| V06 RAFAGA_CLIMAX_CONTRA | 155 | 154 | 46,1 | -0,97 | 2 de 12 | 49,7 | 44,4 | 0,37 / 0,19 / 0,60 / 0,19 / -0,05 |
| V07 RAFAGA_APAGADA_CONTRA | 171 | 171 | 50,3 | 0,08 | 6 de 12 | 49,7 | 47,6 | 0,46 / 0,11 / -0,21 / -0,10 / 0,50 |
| V08 GRANDES_VS_CHICOS (seguir grandes) | 169 | 167 | 53,3 | 0,85 | 6 de 12 | 46,4 | 51,5 | -0,57 / -1,23 / -2,24 / -1,10 / -2,02 |
| V09 GRANDES_CON_CHICOS (seguir) | 330 | 329 | 47,4 | -0,94 | 6 de 12 | 45,6 | 45,7 | -0,47 / -0,50 / -1,08 / -0,63 / -1,33 |
| V10 D50_AISLADO_CONTRA_TENDENCIA | 190 | 188 | 51,6 | 0,44 | 7 de 12 | 50,3 | 51,3 | 0,25 / -0,19 / -0,31 / -0,12 / -1,67 |
| V11 D50_AISLADO_A_FAVOR | 209 | 208 | 55,8 | 1,66 | 8 de 12 | 52,9 | 55,6 | -0,02 / 0,87 / 0,58 / 0,32 / 0,11 |
| V12 GRANDES_DIVERGEN_300 (seguir grandes) | 209 | 208 | 52,4 | 0,69 | 7 de 12 | 50,5 | 55,4 | 0,28 / 0,63 / 0,98 / 2,96 / 4,48 |

Lectura honesta de explorar: el mayor |z| es 2,00 con 24 pruebas efectivas; eso es lo que da el azar. Nada salta.

### Finalistas (por la regla fijada arriba, ANTES de abrir confirmar)
- **Finalista 1: V03 BARRIDO_RUPTURA_SEGUIR** — unica que cumple la regla (n 157 >= 150, 58,0 % >= 55,3 %, 10 de 12 dias = 83 %).
  Aviso previo: por dia es despareja (18 % el 20-08, 88 % el 25-08 con 8 casos, cinco dias en 53-55 %).
- **Finalista 2 (FUERA de regla, veredicto maximo PISTA): V11 D50_AISLADO_A_FAVOR** — la regla daba un solo candidato; se agrega el siguiente por |z| con n >= 150
  (z 1,66; 55,8 %; falla dias: 8 de 12 = 67 % < 70 %). Se declara aca, antes de correr confirmar, como desvio informativo del pre-registro.
- Lados fijados: V03 = seguir el barrido; V11 = seguir al d50.

---

## 3. CONFIRMAR (8 sesiones, 08-09 al 17-09) — `barridos_confirmar.py`, corrido UNA vez, sin retoques

Placebo = mismos disparos por sesion y media hora, mismos lados, segundos al azar separados 60 s, 200 sorteos (semilla 20260917).

| Finalista | barrera | n | acierto | empate | z vs 50 | placebo (media +- desvio) | z vs placebo | dias > 50 % | peor dia | neto por operacion |
|---|---|---|---|---|---|---|---|---|---|---|
| V03 BARRIDO_RUPTURA_SEGUIR | +-8/600 | 97 | **50,5 %** | 55,3 | 0,10 | 57,3 +- 5,1 | **-1,31** | 4 de 8 | 28,6 % | **-0,77 pts** |
| V03 | +-5/300 | 98 | 50,0 % | 58,5 | 0,00 | 53,9 +- 4,7 | -0,82 | 3 de 8 | 35,7 % | -0,85 pts |
| V03 | +-12/900 | 92 | 50,0 % | 53,5 | 0,00 | 60,6 +- 4,7 | -2,23 | 4 de 8 | 14,3 % | -0,85 pts |
| V11 D50_AISLADO_A_FAVOR (fuera de regla) | +-8/600 | 142 | **42,3 %** | 55,3 | -1,85 | 55,2 +- 3,8 | **-3,34** | 2 de 8 | 15,8 % | **-2,08 pts** |
| V11 | +-5/300 | 143 | 49,7 % | 58,5 | -0,08 | 52,6 +- 4,0 | -0,73 | 4 de 8 | 26,3 % | -0,88 pts |
| V11 | +-12/900 | 138 | 40,6 % | 53,5 | -2,21 | 55,9 +- 4,1 | -3,77 | 3 de 8 | 26,3 % | -3,11 pts |

Por dia (+-8): V03 -> 08-09 60 % (10), 09-09 29 % (14), 10-09 36 % (14), 11-09 53 % (15), 14-09 60 % (5), 15-09 50 % (16), 16-09 77 % (13), 17-09 50 % (10).
V11 -> 08-09 35 % (34), 09-09 69 % (13), 10-09 46 % (13), 11-09 33 % (15), 14-09 42 % (12), 15-09 50 % (24), 16-09 16 % (19), 17-09 67 % (12).

El mismo placebo corrido sobre EXPLORAR (referencia): V03 58,0 % contra placebo 56,8 +- 3,8 (z +0,31); V11 55,8 % contra 56,7 +- 3,3 (z -0,26).
Es decir: ni siquiera en explorar el MOMENTO del barrido agregaba algo. El 58 % salia de que el lado del barrido coincide con hacia donde iba esa media
hora (cosa que se sabe despues), no del instante del disparo.

### VEREDICTOS
- **V03 BARRIDO_RUPTURA_SEGUIR: NADA.** 58,0 % en explorar -> 50,5 % en confirmar, n 97 (< 100), 4 de 8 dias, debajo del placebo.
- **V11 D50_AISLADO_A_FAVOR: NADA.** 55,8 % -> 42,3 %; se dio vuelta.
- **FAMILIA: NADA.** Ni barridos, ni rafagas, ni ordenes grandes dan direccion a +-8 / +-5 / +-12 en MNQ.

## 4. Controles de la maquinaria — `barridos_auditoria.py`
- Nada mira adelante: 48 de 48 disparos (4 por variante) se reproducen identicos cortando la tabla en el segundo del disparo.
- Control positivo: con un lado tramposo (signo de los 30 s siguientes) el acierto da 81,5 %: la barrera y los indices estan alineados.

## 5. Lo que se aprendio de la microestructura (descriptivo, `barridos_micro.py`; solo se cuenta lo que se repite en las DOS mitades)

1. **La apuesta de +-8 en MNQ dura menos que una vela.** Desde un segundo cualquiera de la rueda: mediana 48 s hasta tocar +-8 (p25 20 s, p75 108 s), 56 % resuelta
   en 60 s o menos. +-5: mediana 19 s (82 % en <= 60 s). +-12: mediana 104 s. Un gatillo que espera el cierre de la vela de 1 min llega cuando media apuesta ya se jugo.
2. **Barrido grande y rafaga avisan MOVIMIENTO, no direccion.** Tras un barrido p99 la apuesta +-8 se resuelve en 41 s (explorar) y 48 s (confirmar) contra 49 y 61 s
   al azar en la misma media hora; tras una rafaga, 39 y 42 s contra 47 y 65 s. El recorrido absoluto a 60 s sube de ~6,3-7,0 a ~7,1-8,3 pts. La direccion: seguir
   el barrido 53,1 % / 49,8 %; seguir la rafaga 54,9 % / 52,9 % (junto 54,1 %, z 1,8): debajo del empate de 55,3 %.
3. **Quien mueve la vela.** Correlacion por minuto con el retorno del MISMO minuto (explorar / confirmar): ordenes de 10-49 contratos +0,79 / +0,77; barrido neto
   +0,78 / +0,74; delta entero +0,82 / +0,79; ordenes >= 50: +0,57 / +0,52; ordenes de 1 contrato +0,11 / +0,06. Con el minuto SIGUIENTE: todas entre -0,06 y +0,01.
   Los de 1 lote son ruido; la vela la mueven las ordenes medianas y los barridos; y ninguno adelanta nada.
4. **Los grandes no van ni a favor ni en contra de los chicos:** correlacion d50 con d1 en el mismo minuto +0,01 / 0,00. Son independientes. Seguir a los grandes cuando
   contradicen a los chicos (V08): 53,3 % / 51,9 %. Un d50 aislado contra la tendencia (V10) no marca giro: 51,6 % / 53,8 %.
5. **Mucho d50 empujando junto con los chicos = el minuto siguiente devuelve un poco** (V09): -1,08 +- 0,57 pts y -2,34 +- 0,88 pts a 60 s, en las dos mitades. Pero ir
   en contra con barrera +-8 acierta 52,6 % / 52,9 %: es la reversion de 1 minuto ya conocida y no paga el costo.
6. **El climax engaña.** Barrido grande despues de un movimiento largo (V02): el PROMEDIO devuelve (+1,3 / +1,6 pts a 60 s; +2,3 / +2,1 a 120 s, en las dos mitades) pero
   la apuesta +-8 en contra acerto 51,9 % y 44,3 %. Primero te pasa por arriba y despues devuelve: con stop corto, ir contra el climax pierde.
7. **Leccion de muestra.** Variantes NO elegidas dieron en confirmar 59,1 % (V04), 62,2 % (V07) y 60,0 % (V12) habiendo dado 49,4 %, 50,3 % y 52,4 % en explorar; las
   finalistas hicieron el camino inverso. Con 100-200 casos un 60 % aparece y desaparece solo. Lo que no repite, no existe: no se promueven.

## 6. Archivos
`barridos_lib.py` (rasgos y las 12 variantes), `barridos_conteo.py` (conteos previos al pre-registro), `barridos_explorar.py`, `barridos_confirmar.py` (finalistas + placebo),
`barridos_micro.py` (descriptivo), `barridos_auditoria.py` (controles). Volcados de trabajo en %TEMP%arridos_tmp (fuera del repo).
