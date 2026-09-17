---
name: roll-semana-weeklies-en-el-trimestre-viejo
description: "En la semana del roll (graficos ya en Z6, U6 todavia vivo) las weeklies de esa semana son opciones sobre U6; el puente de Rithmic las pedia con Z6 y recibia \"no data\", y el libro vivo quedaba sin 0DTE toda la semana. 1.10d las pide con U6 y corre los strikes por el spread Z6-U6."
metadata: 
  node_type: memory
  type: project
  originSessionId: 961a521e-545b-452c-9eed-32904fb03eae
  modified: 2026-09-16T00:40:45.069Z
---

Medido el 15-09-2026 (semana del roll de septiembre: NQU6/ESU6 vencen el 18-09; los graficos ya estaban en
MNQZ6/MESZ6): el puente listaba las series bajo NQZ6 ("19 series para NQZ6@CME (09-15 Weekly, 09-16 Weekly, ...)")
pero al pedir los contratos del 15-09 o del 16-09 Rithmic contestaba "no data" (log 14:09, 19:23, 20:59). Las
weeklies que vencen antes que el trimestre viejo son opciones SOBRE EL VIEJO (NQU6). Resultado: el libro vivo se quedo
sin 0DTE ni 1DTE toda la semana (capa RITHMIC: 41 strikes, -0,010B a las 14:30) y "4 vencimientos" en vez de 6.
El 11-09, con los graficos en U6, ese mismo libro tenia bandas a -18 pts del precio de noche (ver
[[dominantes-de-noche-resto-de-ayer]], 1.8i). Probablemente el "0DTE perdido a las 10:16" del 11-09 tuvo este mismo
origen cuando algun grafico ya pedia Z6 ([[nq-rithmic-pierde-0dte]]).

Arreglo (Gamma Hoy 1.10d, CadenaViva.cs + PuenteRithmic.cs): busca en el servidor el trimestre anterior del futuro
del grafico (`CodigoTrimestreAnterior`: NQZ6 -> NQU6); si todavia no vencio, se suscribe a su precio, las series con
vencimiento <= su fecha se piden con su codigo (`OpcionesAsync(..., subAlternativo)`) y sus strikes se dibujan
corridos por el spread vivo Z6 - U6 (`KDe`: ventana al dinero, filas, bloques grandes, volumen por strike). Sin el
precio del viejo, esas filas no se dibujan (antes que dibujar 300 pts corrido). Log: "[cadena viva] roll: ...". Medido 21:40: 774/806/1018 contratos (15, 16, 18-09) con NQU6, spread +291,13; ES 720/714/872 con ESU6, +66,63; en pantalla RITHMIC D1/D2 a +15/-7 del precio.

**Why:** el operador recordaba dominantes vivas cerca del precio de noche y "una formula mal" de dias atras; la
formula (empate tecnico) estaba bien, lo roto era el roll: el mismo tipo de falla que la base de 294 pts del 09-09
([[auditoria-en-vivo-2026-09-09]]), ahora del lado de Rithmic.

**How to apply:** cada semana de roll (marzo, junio, septiembre, diciembre; la semana del tercer viernes) mirar en el
log "roll:" y "[puente] N contratos ... pedidos con XU6"; si aparece "no data" para la weekly del dia, es esto. Y
reiniciar ATAS de noche vacia el volumen acumulado del libro vivo: evitarlo si se quiere ver ese libro a la noche.

**Regresion vista el mismo dia (22:43) y arreglada en 1.10h:** suscribir NQU6 lo mete en `_conn.Securities` y `Buscar()`
("el NQ de vencimiento mas cercano") elegia NQU6 como futuro de la cadena: referencia 300 pts abajo del grafico y sin
niveles. Ahora Buscar() prefiere el codigo del grafico (MNQZ6/NQZ6) y descarta vencidos. Cualquier cambio en el
catalogo local cambia el candidato: mirar "futuro XXX en" en el log.

**Contrato continuo, 23:05 (1.10j):** en un grafico "MNQ Continuous" la historia mezcla los dos contratos (empalme
de ~300 pts al recargar). La razon "vela alineada" (vela de hace 902 s / spot del ETF) tomaba una vela de
septiembre con el precio en diciembre: razon 41,10 contra 41,51 en el mismo minuto en otro grafico, niveles ~290 pts
arriba durante ~15 min despues de cada recarga ("se desfasan al abrir un grafico"). 1.10j descarta la vela alineada
si difiere mas de 0,6 % del precio actual (origen "CRUDA: la vela alineada es de otro contrato"). La estela repetida
de NDX sobre velas viejas con la base nueva sigue corrida en graficos continuos: usar contrato explicito en la
semana del roll.

**Tercera parte (17-09, 1.11c): la weekly del VIERNES trimestral es del trimestre NUEVO.** U6 vence el viernes 9:30 NY; la weekly de ese viernes vence a las 16:00 y es sobre Z6 (Rithmic: Z6 lista "09-18 Weekly", U6 lista "09-18 Regular"). La regla por fecha ("<= vencimiento de U6") la pedia con U6 y recibia los contratos de la trimestral (ya el 15-09 los "1018 contratos del 18-09 con NQU6" eran eso, sin que se notara). Regla correcta: viejo si vence ANTES, o el mismo dia y es Regular. Mismo dia = dos series: llave fecha+tipo, ventana de strikes por (fecha, trimestre), hora de vencimiento por contrato. En diciembre pasa igual con Z6/H7. Ver [[regla-roja-roll-libro-vivo]].

