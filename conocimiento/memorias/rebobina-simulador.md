---
name: rebobina-simulador
description: El simulador Rebobina corre Gamma Hoy afuera de ATAS con los mismos archivos fuente; equivalencia probada contra el indicador vivo; que datos consume y que falta
metadata:
  type: project
---

Decidido el 2026-09-07: el operador NO quiere archivos de afuera dentro de
ATAS ("apenas anda, lo rompemos"). El backtest corre en `atas/Rebobina`
(consola .NET) que compila los MISMOS .cs del indicador: `GammaHoyNucleo.cs`
(toda la cuenta, sin ATAS), `Feed.cs`, `Centinela.cs`. El indicador quedo
como cascara: elige cadena, precio y hora, y dibuja.

**Equivalencia probada:** `Rebobina --prueba <cadena.json> --precio 7709`
dio la misma linea AUDIT que el indicador vivo con la misma cadena (netVol
-9.327B, netOi -4.629B, zeroVol 7718.89, zeroOi 7717.06, majors 7726/7706,
doms, q=1). Si esto se rompe, el backtest no vale.

Datos: Databento con credito gratis (ver [[databento-cuenta-y-costos]]).
`herramientas/databento_a_cadenas.py` arma cadenas por minuto desde OPRA
(OI 100 % igual a CBOE; volumen 82 % exacto al mismo corte). Retraso de CBOE
como parametro (r902 y r0). Base: medida del dia si hay, si no carry (tasa
menos 1,2 % de dividendos). `herramientas/rebobinar_todo.py` convierte todos
los dias bajados y rebobina de una; `laboratorio/rebobinado.py` juzga contra
placebo.

Hallazgo ya corregido en el nucleo: la alerta de TRANSICION se disparaba a
cada minuto (dos strikes vecinos alternando el maximo); ahora mira una zona
(80 % del maximo, banda 2,5) y grita una vez cada 10 min: 09-03 paso de ~40 a 6.

**Resultado con 13 ruedas RTH (2026-08-19 a 09-04, 5863 velas de 1 min, cadena
de <= 20 min de edad, cada nivel contra su placebo):** dominantes por volumen
(2) -10,4 pp (141 toques, 56 strikes); dominante 1 sola -1,7; majors por
volumen +6,7 (79 toques, 41 strikes); majors por OI +4,3; max change 30' y 5'
~0; pico cerca del precio -6,0. Cuadrantes: 1=2189, 2=3556, 3=81, 4=37 velas.
El "volumen puro +42 pp" del laboratorio vivo era muestra chica. Visor:
https://claude.ai/code/artifact/70e2d6db-2ba1-4faf-a57e-a139dad2d507
(herramientas/visor_rebobinado.py lo regenera desde los centinelas).

**Panel local (2026-09-07, opcion 1 elegida por el operador, "quiero todo"):**
`herramientas/panel_local.py` sirve http://127.0.0.1:8770 (lanzador
`ATAS nada/Rebobina Panel.bat`): parametros del motor, ruedas, carga de
archivos (.dbn.zst de Databento por su metadata, .csv de velas), cotizar y
bajar de Databento con el techo, simular (convierte, corre Rebobina, lab,
visor), visor embebido. Python 3.14 no trae `cgi`: multipart con email.parser.

**Gamma Vivo en el simulador:** `GammaVivoNucleo.cs` reproduce SOLO lo que
Gamma Vivo anota (zero y muros del OI a 7 dias, picos modo 1), copiado de
GammaVivo.cs con lineas citadas; no es el nucleo entero (5.600 lineas). Con
13 ruedas: dominantes (8, OI) -3,4 pp, (2, OI) -7,1, muros +0,4, zero casi
sin toques. Rebobina `--indicadores hoy+vivo` -> centinela rebobinado-vivo.

**Cruce CBOE vs Databento, 2026-09-03, misma cuenta, 451 minutos
(laboratorio/cruzar_fuentes.py):** spot igual (0,06 pts mediana); +Γ vol,
dom0, pico y max change 30' coinciden (mediana 0,1 pts, 62-71 % a <= 2,5
pts); cuadrante igual 94 %. Lo que NO coincide: zero vol (mediana 3,4),
zero OI (5,4), -Γ vol (10) y sobre todo -Γ OI (45 pts): la IV de los puts
lejanos sin operar esta interpolada y mueve el argmin. Arreglo posible:
cbbo-1m de Databento en ventanas de 1 minuto cada 15 (barato) para IV real
de todos los strikes; o usar la IV de CBOE de nuestro archivo hacia adelante.

**Modo Archivo dentro de ATAS (instalado 2026-09-07 ~21:45 UTC con el OK del
operador):** Gamma Hoy 0.3, Fuente = Archivo: recorre la historia cargada con
la cadena de cada minuto (carpeta %APPDATA%\ATAS\PythiaGex\cadenas, los
13 dias de Databento copiados como cadena-ES-<dia>.jsonl.gz + la nube);
escalera y rayas siguen la vela bajo el mouse (IMouseLocationInfo.BarBelowMouse).

**Fuente = Hibrido (Gamma Hoy 0.4, compilado e instalado el 2026-09-07 ~22:10
UTC, carga en el proximo reinicio de ATAS):** la historia con el archivo, la
vela que se forma con el vivo (feed + Rithmic); cada vela que cierra guarda su
foto (mouse) y el centinela del archivo es aparte ("rebobinado-atas-"), el vivo
sigue en "hoy-". Lo grabado hoy (local-ES-<dia>.jsonl + la nube cada 5 min)
es el archivo de manana sin Databento; lo que NO se archiva todavia es la
cadena viva de Rithmic (bloques, volumen del futuro).

**0.5 (instalado 2026-09-07 ~22:50 UTC, carga en el proximo reinicio):** el
archivo se recorre en un hilo propio con un nucleo propio, sin
RecalculateValues (la 0.3b/0.4 metia 5.472 velas en el hilo de ticks: "Slow
ticks processing 6 s"; y ATAS llama OnCalculate para las DOS ultimas velas
en cada tick, asi que el rebobinado se reiniciaba y logueaba "termino" a
cada tick: 1.650 lineas en 6 min). Auditoria tras el reinicio del operador
(22:32 UTC): heatmap recuperado, sin Unobserved exception, cadenas.yml
corriendo (cada 5 min fuera de rueda). Mockups a la GAMMAlito (guiones por
vela, rayas tenues o sin rayas, semillas del Max Change):
https://claude.ai/code/artifact/46f9cf4c-021f-4bf7-abbd-1d2a0bf91487 (elige el).

**0.7 (2026-09-07 ~23:00 hora de la maquina, cargado y verificado):** dominante
como centroide (radio 12 pts), un guion por actualizacion (hasta 24 por vela,
> 0,25 pt), amarillo/naranja; en el rebobinado un guion por cada cadena de la
vela. Esta en la pestaña "MES 5m Chart" de la DERECHA en Hibrido (5.862
cadenas, 3.078 velas en 13 s en hilo propio). A las 21:47 (hora maquina)
alguien reinicio ATAS y puso Gamma Vivo en la 4ta pestaña de la izquierda;
Gamma Hoy quedo sin grafico hasta que lo agregue a la derecha. Las pestañas
ocultas SI siguen calculando una vez inicializadas (la viva de NQ se grabo
desde la pestaña MNQ oculta). Rebobinado 13 dias con centroide: dominantes
(2, vol) 24 toques, +8,2 pp vs placebo, 22 strikes (muestra chica: el
centroide no cae en strikes y toca menos).

**Pendiente:** el DLL recompilado NO se instalo en ATAS (el operador teme
romperlo; instalar con mercado cerrado y reinicio avisado). El convertidor
tarda 5 min por dia (Python puro): paralelizado de a 4. Un dia son ~6
strikes distintos: hacen falta 15+ ruedas antes de concluir nada. La
correlacion "rebobinado vs vivo" del mismo dia todavia no se midio (el
centinela vivo de Gamma Hoy arranco el 09-07 con mercado cerrado).
Ver [[fuentes-datos-historicos]], [[laboratorio-formulas]].

**0.6 (2026-09-07 ~23:00 UTC, cargado y verificado):** guiones por vela
(GrosorGuion 3), Rayas = Tenues por defecto (Ninguna/Normales), semillas del
Max Change 30/5/1 por vela (VerSemillas), zero por vela (puntitos), feed por
minuto desde la rama cadenas (FeedMinuto, ultima-<raiz>.json), viva grabada
en todos los modos. El recorrido del archivo: 5.856 cadenas, 3.042 velas en
6 s en hilo propio, un solo "termino" en el log. Mockups elegidos por mi
(operador ausente y autorizando): la 1 con rayas tenues y las semillas de la 3.
