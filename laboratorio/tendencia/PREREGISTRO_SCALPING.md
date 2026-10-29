# Pre-registro 4: la Corriente a la escala del scalper

Escrito el 2026-10-29 ~13:15 AR, **antes de calcular**. Pedido: "de 7760 bajó a 7748 y subió a 7753 y 7755 y seguía diciendo
bajista: subió más del 50 % del tramo y no se enteró; para scalping está malísimo; pensá fuera de la caja". Diagnóstico
declarado: las rondas 1-3 midieron el retraso en movimientos de **20 pts** (escala de swing); el operador opera tramos de
**5-7 pts** con TP de 2,5. Lo ya visto: rondas 1-3 y la sesión de hoy (su ejemplo: 12:45-12:58 AR).

## 1. La vara: acertar el tramo actual a SU escala

- **Tramo verdadero (mirando hacia atrás)**: zigzag con umbral Z pts sobre máximos y mínimos, por bloque: un giro queda
  confirmado cuando el precio se aleja Z del extremo; cada vela después de un pivote y hasta el siguiente pivote (incluido)
  pertenece al tramo que empieza en ese pivote. Las velas después del último pivote del bloque no se puntúan.
- **Z = 6 pts primaria** (su ejemplo: tramo de 12, rebote de 7); secundarias Z = 4, 8 y 10.
- **Acierto** = % de velas de la rueda (9:30-16:00 NY, válidas, con tramo conocido) en las que la Corriente marca la misma
  dirección que el tramo verdadero; promedio por sesión. Integra las dos cosas: retraso en cada giro y vueltas en falso.
- También: puntos tarde en cada pivote de la rueda (desde el extremo hasta el primer cierre en que la Corriente coincide,
  dentro de ese tramo; si no coincide nunca, "no lo vio"), cambios por sesión en la rueda, vueltas falsas (después del
  cambio el precio no avanza ni 2,5 pts a favor: su TP).

## 2. Candidatos (fijos)

- **R_X** (cierre): se da vuelta cuando el cierre retrocede X pts desde el extremo del tramo. X en {2, 3, 4, 5, 6, 7, 8, 10}.
- **P(f, m, X)** (su regla): umbral = f x (tamaño del tramo anterior: desde el extremo donde empezó hasta el extremo actual),
  acotado entre m y X pts. f en {0,382; 0,5; 0,618}, m en {2, 3, 4, 5}, X en {8, 10, 12} (36).
- **Variantes "en vivo" (toque)** de R_X X en {3, 4, 5, 6} y de P(0,5, m, 10) m en {2, 3, 4, 5}: se da vuelta en la vela cuyo
  mínimo (máximo) toca la línea calculada con el extremo de la vela anterior (es lo que vería la flecha en vivo).
- Referencias: ST(10,3) (1.0c), 1.1, R10 (1.2).

## 3. Elección y examen

- Elegido = el de mayor acierto medio por sesión con Z = 6 en DISEÑO (45 ses; MES 1 min, 08-04..10-05).
- **Examen** en EXAMEN (17 ses, 10-06..10-28): el elegido contra R10 y contra ST(10,3), sesión por sesión; pasa si gana en
  acierto (Z = 6) con prueba de signos sobre sesiones p <= 0,05 contra los dos, y además gana en Z = 4 y 8 en la media.
- Se informa el costo en cambios y vueltas falsas sin ocultarlo: a esta escala va a cambiar mucho más que antes, y eso es
  lo pedido.

## 4. Lo que NO mide

No anticipa: describe el tramo actual a una escala. No es una estrategia ni promete ganancia.

## Enmiendas

(ninguna al momento de escribir)

**Veredicto (14:05 AR, `scalp_01_salida.txt`): PASA.** Elegido en DISEÑO: R4 en vivo (acierto Z = 6: 76,11 %). EXAMEN: 75,99 %
contra 54,87 % de R10 (1.2) y 57,36 % de ST(10,3) (1.0c); gana en 16 de 16 sesiones contra los dos (p < 0,0001); medias Z4 /
Z8 / Z10: 78,2 / 72,0 / 68,7 contra R10 51,7 / 57,8 / 64,4. Costo: 82 cambios por sesión en la rueda (falsos de 2,5 pts:
39,6) contra 10,5 de R10. Su regla del 50 % (P 0,5) quedó por debajo de los retrocesos fijos chicos (60-68 %). Se instala
Corriente 1.3 (tramo 4 pts en vivo + corriente grande de 10 pts en la cinta). Paridad del C# con la regla: 0 de 84.364 velas.
