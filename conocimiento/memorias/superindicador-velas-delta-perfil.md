---
name: superindicador-velas-delta-perfil
description: "PythiaGex - Superindicador 1.9 (26-11-2026, 1.9 = sonda de cinta adentro): velas 2 colores + columna de delta + perfil (area de valor de ATAS) + JORNADA hasta la VELA DEL MOMENTO (la ultima a la vista): al correr el grafico se rebobina como estaba; auditado contra ATAS, la cinta y el replay"
metadata:
  node_type: memory
  type: project
  originSessionId: 233bf8d4-5113-4e95-a0d9-68bff726690d
  modified: 2026-11-27T02:51:41.772Z
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

**1.6 (26-11, mismo dia), pedido:** "si voy corriendo el grafico el perfil de volumen queda fijo y esta mal; tiene que ser dinamico,
sincronizado segun la vista o fecha". Causa: con < > la jornada quedaba FIJA (modo fecha) y la columna estaba en "visible".
Arreglo: por defecto columna Y perfil en "jornada" y "a la vista" (la que mas velas tiene en pantalla; con la ultima vela a la
vista, la de hoy o la del replay); una fecha fijada con < > se SUELTA sola al mover el grafico y mientras no sea la de pantalla
avisa en amarillo (renglon propio); con el grafico corrido hacia atras la columna va AL LADO del perfil (antes se encimaba).
Propiedades RENOMBRADAS para pisar lo guardado en el .ws: SpColPeriodo (antes SpColVentana) y SpJornadaQue (antes SpJornadaModo).
ATAS no expone desplazar el grafico en horizontal a un indicador (solo ScrollPrice/MoveChartUpAndDown): < > no puede llevar la vista.
Verificado corriendo el grafico por la barra de desplazamiento: 24-11 a la vista (546 de 546 velas) +11.658, POC 7218, VAH 7230,50,
VAL 7170,50; vuelta al borde 26-11 +1.529, POC 7250, VAH 7253, VAL 7227: todo igual al recalculo (auditor con control "a la vista").

**1.7 (26-11, noche), pedido:** "no era lo que buscaba: al ir moviendo el grafico, el delta y el perfil tienen que mostrar como
estaban EN ESE MOMENTO, como un footprint real; si todo queda estatico no sirve para analizar retrocediendo". Arreglo: TODO (columna
y perfil, cualquier periodo salvo 'rango visible') se suma hasta la VELA DEL MOMENTO = la ultima a la vista (boton "al momento";
"en vivo" = hasta la ultima vela). La jornada es la de esa vela desde su comienzo; si a esa hora la parte no habia empezado (RTH a
las 8:00) va la anterior completa (`Jornadas.AlMomento`, 7.200 consultas contra Python iguales). Rotulo "jue 26-11 completa hasta
07:33". "fijar" congela un dia completo (se suelta al mover el grafico). Propiedades nuevas: SpHasta, SpJornadaCual (renombrada).
Verificado: rebobinado a 26-11 05:33 NY: +2.671, POC 7236, VAH 7242, VAL 7217,50 = recalculo (auditor con control AL MOMENTO).
INCIDENTE en esa prueba: ver [[foco-atas-atajos-trading]] (2 Buy Limit reales por sus teclas; sin fill).

**1.8 (26-11, noche), pedido:** "que diga la fecha y hora exacta a la que pertenece el dato mostrado, y un puntito LIVE verde luminoso
o rojo apagado si no es en vivo". Encabezado de la columna: renglon 1 "● LIVE DELTA" (verde con halo si la suma incluye la vela en
curso; rojo apagado si es un momento pasado), renglon 2 dia y hora con segundos (hora de la PC, como el eje) de la ULTIMA operacion
sumada, renglon 3 el total. OJO: de noche la fecha es la del reloj ("jue 26-11 21:56") y la jornada es la de CME ("vie 27-11"): no es
un error. Auditor: control ENCABEZADO (LIVE = incluye la vela en curso; la hora = ultima operacion, al segundo). Verificado en vivo
21:56:54 IGUAL; el rojo (rebobinado) quedo sin mirar en pantalla porque el operador estaba en la PC (no se toca ATAS).
Instalador: ahora espera 20 s de teclado/mouse quietos y devuelve el foco a la ventana anterior (el aviso modal se lleva el foco).
**1.8b (26-11, 22:00), "se ve feo, se profesional":** encabezado sobre banda oscura y sin sombras: pastilla redondeada con punto y
LIVE (verde oscuro con borde verde / rojo apagado), el dato en gris a la derecha, fecha y hora en letra fina ("jue 26/11  22:00:05"),
"total" y el total grande alineado a la derecha como las filas, linea fina de separacion. Verificado: encabezado y valores IGUALES.
**1.8c (26-11, 22:10), ELEGIDA por el operador entre 5 bocetos (pidio: sin "DELTA", sin fondo verde, el dia mas grande; "mostrame
varias preview y cuando te de ok hacelo"):** variante E = renglon 1 "● LIVE" (verde / rojo apagado) a la izquierda y la hora con
segundos a la derecha; renglon 2 el dia en mayusculas grande ("JUE 26/11", 10 a 8 pt segun entre); linea; "total" + total a la
derecha. Sin fondos ni sombras. Verificado 22:10:30 IGUAL. Para cambios de aspecto: bocetos primero (show_widget), instalar con su OK.
**1.8d (26-11):** "algo mas chico el dia y la fecha": el dia baja a 8,5 pt (7,5 si no entra). Verificado 22:13:05 IGUAL.
**1.9 (26-11, noche): la SONDA adentro** (`SuperSonda.cs`, clase parcial): escribir `cinta;desde;hasta;volmin;minutos` (UTC) en
`%APPDATA%\ATAS\PythiaGex\flujo\sonda-<inst>.txt` y sale `cinta-<inst>-<desde>-<hasta>.csv` (+ `.listo`) con la cinta orden por orden
y la punta antes/despues; pedir de a UNA sesion (1380 min desde las 23:00 UTC en horario de invierno). Con eso se bajaron MES 16-25-11
(476 mil a 899 mil ordenes por rueda) para la [[ronda-17-anatomia-del-movimiento]]; no cambia nada de lo que se dibuja.
**Proximo (esperando OK):** "la ola" de la ronda 17, previews A (renglon bajo la fecha), B (ola dibujada) y C (ola + marea).