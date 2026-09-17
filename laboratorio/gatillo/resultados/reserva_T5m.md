# Validacion en la reserva: T5m invertida (pre-registro escrito ANTES de correr, 2026-09-17 17:33)

**Regla (congelada, de escalas_lib + escalas_umbrales.json):** al cierre de cada vela de RELOJ de 5 minutos dentro de la rueda (13:32-20:00 UTC), r = delta / volumen de esos
300 s; dispara si |r| >= 0,06613 y volumen >= 10.242 contratos; lado = CONTRA el signo del delta; entrada en ultimo[t]; barrera +-8 puntos en 600 s; costo 0.96 pts (empate 56.0 %).
**H1 (principal):** toda la rueda. **H2 (secundaria):** solo disparos entre 15:00 y 18:00 UTC (la franja que se vio mejor post-hoc).
**Criterio para que sobreviva (cada hipotesis por separado):** n >= 100, acierto >= empate + 2 puntos (58.0 %), z contra placebo (200 sorteos, misma sesion y media hora, mismos lados) >= 2,5 y
>= 70 % de los dias arriba de 50 %. Si H1 falla y H2 pasa, queda como PISTA (dos miradas). Si las dos fallan: NADA, y no se retoca.
**Antecedente (no cuenta como prueba):** explorar 56,4 % (n 204), confirmar mirado post-hoc 62,2 % (n 111); las dos con tablas que tenian el error del precio congelado.

## Corrida 2026-09-17 17:34

- explorar (corregido) | H1 toda la rueda | +-8/600s: n 217, acierto 56.2 % (empate 56.0), placebo 43.4 +- 2.9, z contra placebo +4.41, dias arriba de 50 %: 10 de 12, peor dia 33 %, neto +0.04 pts/op
- explorar (corregido) | H1 toda la rueda | +-5/300s: n 215, acierto 55.3 % (empate 59.6), placebo 46.4 +- 3.2, z contra placebo +2.79, dias arriba de 50 %: 7 de 12, peor dia 41 %, neto -0.43 pts/op
- explorar (corregido) | H1 toda la rueda | +-12/900s: n 216, acierto 50.5 % (empate 54.0), placebo 40.5 +- 2.9, z contra placebo +3.43, dias arriba de 50 %: 8 de 12, peor dia 31 %, neto -0.85 pts/op
- explorar (corregido) | H2 15:00-18:00 UTC | +-8/600s: n 129, acierto 57.4 % (empate 56.0), placebo 44.0 +- 4.1, z contra placebo +3.23, dias arriba de 50 %: 8 de 12, peor dia 36 %, neto +0.22 pts/op
- explorar (corregido) | H2 15:00-18:00 UTC | +-5/300s: n 129, acierto 53.5 % (empate 59.6), placebo 46.3 +- 4.3, z contra placebo +1.65, dias arriba de 50 %: 6 de 12, peor dia 33 %, neto -0.61 pts/op
- explorar (corregido) | H2 15:00-18:00 UTC | +-12/900s: n 129, acierto 50.4 % (empate 54.0), placebo 41.2 +- 4.0, z contra placebo +2.32, dias arriba de 50 %: 6 de 12, peor dia 29 %, neto -0.87 pts/op
- confirmar (corregido, ya mirado) | H1 toda la rueda | +-8/600s: n 112, acierto 61.6 % (empate 56.0), placebo 47.1 +- 4.5, z contra placebo +3.21, dias arriba de 50 %: 5 de 8, peor dia 33 %, neto +0.90 pts/op
- confirmar (corregido, ya mirado) | H1 toda la rueda | +-5/300s: n 112, acierto 56.2 % (empate 59.6), placebo 49.1 +- 4.5, z contra placebo +1.60, dias arriba de 50 %: 4 de 8, peor dia 17 %, neto -0.33 pts/op
- confirmar (corregido, ya mirado) | H1 toda la rueda | +-12/900s: n 111, acierto 60.4 % (empate 54.0), placebo 43.6 +- 4.6, z contra placebo +3.65, dias arriba de 50 %: 5 de 8, peor dia 33 %, neto +1.53 pts/op
- confirmar (corregido, ya mirado) | H2 15:00-18:00 UTC | +-8/600s: n 65, acierto 67.7 % (empate 56.0), placebo 48.1 +- 5.9, z contra placebo +3.34, dias arriba de 50 %: 5 de 8, peor dia 50 %, neto +1.87 pts/op
- confirmar (corregido, ya mirado) | H2 15:00-18:00 UTC | +-5/300s: n 64, acierto 64.1 % (empate 59.6), placebo 49.3 +- 6.5, z contra placebo +2.29, dias arriba de 50 %: 5 de 8, peor dia 50 %, neto +0.45 pts/op
- confirmar (corregido, ya mirado) | H2 15:00-18:00 UTC | +-12/900s: n 64, acierto 65.6 % (empate 54.0), placebo 45.6 +- 5.7, z contra placebo +3.50, dias arriba de 50 %: 7 de 8, peor dia 50 %, neto +2.79 pts/op

## Corrida 2026-09-17 17:34

- RESERVA | H1 toda la rueda | +-8/600s: n 244, acierto 50.0 % (empate 56.0), placebo 44.6 +- 3.0, z contra placebo +1.83, dias arriba de 50 %: 5 de 14, peor dia 30 %, neto -0.96 pts/op
- RESERVA | H1 toda la rueda | +-5/300s: n 244, acierto 51.2 % (empate 59.6), placebo 46.7 +- 3.2, z contra placebo +1.42, dias arriba de 50 %: 7 de 14, peor dia 27 %, neto -0.84 pts/op
- RESERVA | H1 toda la rueda | +-12/900s: n 244, acierto 51.6 % (empate 54.0), placebo 41.7 +- 2.7, z contra placebo +3.64, dias arriba de 50 %: 6 de 14, peor dia 27 %, neto -0.57 pts/op
- RESERVA | H2 15:00-18:00 UTC | +-8/600s: n 135, acierto 54.1 % (empate 56.0), placebo 43.7 +- 4.1, z contra placebo +2.50, dias arriba de 50 %: 5 de 14, peor dia 29 %, neto -0.31 pts/op
- RESERVA | H2 15:00-18:00 UTC | +-5/300s: n 136, acierto 50.0 % (empate 59.6), placebo 45.7 +- 4.3, z contra placebo +1.00, dias arriba de 50 %: 6 de 14, peor dia 25 %, neto -0.96 pts/op
- RESERVA | H2 15:00-18:00 UTC | +-12/900s: n 135, acierto 56.3 % (empate 54.0), placebo 40.8 +- 4.1, z contra placebo +3.74, dias arriba de 50 %: 8 de 14, peor dia 22 %, neto +0.55 pts/op
