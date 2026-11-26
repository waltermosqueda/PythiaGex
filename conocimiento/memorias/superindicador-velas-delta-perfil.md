---
name: superindicador-velas-delta-perfil
description: "PythiaGex - Superindicador 1.5 (26-11-2026): velas en 2 colores + columna de delta por precio + perfil con el area de valor de ATAS + JORNADA ELEGIDA (solo un dia: completa/RTH/noche, botones < >); auditado contra ATAS, la cinta y el replay"
metadata:
  node_type: memory
  type: project
  originSessionId: 233bf8d4-5113-4e95-a0d9-68bff726690d
  modified: 2026-11-26T22:11:59.912Z
---

Pedido 23-11-2026 con 4 reels (todos del mismo trader, 3 en NinjaTrader y 1 en ATAS): "grafico en dos colores con paletas, el
order flow bien calculado en una columna y el perfil de volumen, en un unico superindicador que se active a un clic".

**Donde:** `PythiaGex/atas/Superindicador/` (SuperCalculos.cs = la cuenta sin ATAS; Superindicador.cs = ATAS), DLL
`PythiaSuperindicador.dll`, ajustes con prefijo Sp. Instalado en el grafico LIMPIO "MES 1m Chart" (ver [[probar-en-grafico-limpio]]).
Instalar con `herramientas/instalar_superindicador.ps1` (en caliente, ver [[recarga-en-caliente-atas]]). Hay otra instancia vieja en
un grafico MES de 30 s (seguia en 1.4e el 26-11: no se redibujaba).

**1.5 (26-11), pedido:** "ver el indicador con los valores del dia anterior o la sesion que yo elija, data limpia SOLO de esa
jornada; con rango visible me suma la ultima sesion y la del dia donde estoy; para backtestear con el replay". Periodo nuevo
"Jornada elegida" en columna y perfil: jornada de CME por la hora NY de apertura de cada vela (18:00 = dia siguiente), partes
completa / RTH 9:30-16:00 / noche 18:00-9:30. Cual: la ultima (en el replay, la del dia reproducido), una fecha (botones < > de la
botonera; '>' hasta la ultima vuelve a "la ultima") o "a la vista" (la que mas se ve). Avisa en amarillo si el grafico no cargo la
jornada desde su comienzo o si hay velas sin footprint. Valores de enum nuevos AL FINAL (ATAS guarda el elegido).
**Verificado 26-11:** sintetico 400/400 + jornadas 120 casos (232.735 velas, cambios de horario, Thanksgiving, fin de ano, 4.800
consultas "a la vista") contra Python con otra base de husos: todo igual. En vivo 25-11 RTH: 390 velas 9:30-15:59 NY, frontera
exacta, filas/totales/POC/VAH/VAL/textos iguales al recalculo; pantalla final (+24.467, POC 7021,75, VAH 7169,25, VAL 6982,75)
fila por fila. En el replay del 25-11: jornada en curso correcta y pantalla pausada (+16.371) igual al recalculo; replay = vivo.
**Decisiones medidas (1.4):** area de valor = la de ATAS con HUECOS (1.163/1.163 velas) y paso de la plataforma; POC empatado = el
MAS ALTO; columna en filas FIJAS de K ticks con letra medida; nunca encima de velas; encabezado fijo; lupa que se borra sola.
**Auditoria:** `herramientas/auditar_superindicador.py` (A velas, JORNADA frontera exacta, B filas/textos/POC/VA, C area de valor por
vela vs ATAS, D cinta: saltea huecos de grabacion) + `test_superindicador.py` + `test_jornadas.py`. Archivo `velas2-*` con columna
carga. **Pendiente chico:** la "carga" se numera por instancia (cada recarga/replay crea una instancia nueva y vuelve a 1): hacerla
unica si hace falta separar pasadas; hoy se separan por numero de vela y orden en el archivo.
