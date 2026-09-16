# Auditoria de la estela de dominantes (15-09-2026, 20:30-21:15 local)

Pedido del operador: "auditar bien todos los puntos dominantes como se estan dibujando (para mi estan dibujados muy
arriba la estela), todo en tiempo real, contrastando con fuentes de afuera y con todos los calculos de ATAS".

Herramientas: `laboratorio/auditar_estela.py` (nuevo: minuto a minuto, log del indicador contra el recalculo desde
la cadena archivada, contra Yahoo 1 min, y las distancias al precio), `laboratorio/capas_nq.py` (recalculo
independiente), y `atas/Rebobina --prueba` (el MISMO nucleo C# del indicador corrido afuera de ATAS sobre una cadena
dada). Todo lo que sigue se midio hoy; lo supuesto esta rotulado.

## 1. Veredicto corto

- **La estela esta donde la cuenta dice.** No hay corrimiento de dibujo. Lo que se ve "muy arriba" despues de las
  16:00 es el libro de la noche: cuando vence el 0DTE de hoy, el libro "Hoy" pasa a ser el vencimiento de manana, que
  casi no tiene operaciones, y la regla "la barra mas grande dentro del 2 %" elige barras lejanas y flacas.
- **La cuenta es la misma en tres lugares**: el indicador (log AUDIT), el nucleo C# corrido afuera (Rebobina) y el
  recalculo en Python (capas_nq.py). Con la misma cadena y el mismo precio dan lo mismo al millon.
- **Lo que estaba mal**: la edad que decia la leyenda (mentia por defecto), la trazabilidad de que cadena uso cada
  capa en cada minuto (no se guardaba cada version) y el auditor asumia horizonte "Hoy" cuando el grafico estuvo en
  "Todo". Arreglado en 1.10b (ver seccion 5).

## 2. Los numeros que explican "muy arriba"

| Hora (local) | Libro | D1 (arriba) | D2 (abajo) | Tamaño |
|---|---|---|---|---|
| 14:14 | NDX, 0DTE de hoy | 29.005 (+39 pts) con +1.595M | 28.906 (−60 pts) con −2.227M | `mucho=True` |
| 20:42 | NDX, vencimiento de manana | 29.553 (+270 pts) con +233M | 28.903 (−380 pts) con −62M | `mucho=False` |

Distancias de toda la rueda (auditar_estela.py, QQQ/SPX/SPY/NDX): en la sesion D1 entre +13 y +60 pts y D2 entre
−13 y −35; despues de las 16:00, D1 +120…+270 y D2 −200…−380. Los guiones "arriba" son los D1 de la noche, dibujados
a su precio exacto. Las cuatro capas son el mismo tipo de libro (0DTE por volumen), por eso sumar indices no acerca
nada de noche: no hay gamma cerca del precio hasta que entre el interes abierto nuevo (~8:00 ET) y arranque el
volumen del 0DTE (9:30).

## 3. Lo que coincide (medido)

- **Spot de la cadena vs Yahoo** (^NDX, QQQ, SPY, ^GSPC, alineado 902 s): mediana 0,0 pb en los cuatro libros.
- **Razon del indicador vs Yahoo** (NQ=F / ETF): −99 pb = el spread septiembre/diciembre (Yahoo ya estaba en
  diciembre; el grafico estuvo en septiembre hasta las 19:22). Coherente, no un error.
- **Nucleo C# = log**: `Rebobina --prueba ultima-NQ.json --precio 29280 --ahora <utc>` reproduce la linea AUDIT de
  la capa NDX de las 20:42 al centavo (zero 29.164,98 vs 29.164,94; dominantes 29.552,64 / 28.902,52 iguales;
  strikes 219; netOi −0,225B iguales).
- **Nucleo C# = Python**: sobre la cadena de las 18:14:13Z con S = 28.959,34: 28900 = −3.790M, 28950 = −3.414M,
  29000 = +2.913M en los dos; dominantes 29.000 / 28.950 en los dos.
- **La regla de dominantes** (una por lado, empate tecnico 20 %, centroide ±12): con esa regla el recalculo por
  minuto coincide en QQQ 25/29, SPX 49/76, SPY 36/50; probadas las hipotesis "sin una por lado" (7/29), "empate 0 %"
  y "empate 50 %" (6/29): la regla del indicador es la que mejor reproduce el log. Los minutos que no coinciden son
  los de la seccion 4.

## 4. Lo que al principio NO se reproducia, y la causa real (corregido a las 21:00)

Durante una hora parecio que minutos enteros de la rueda no cerraban (NDX 14:14: el log decia 28900 = −2.227M y
29000 = +1.595M con 282 strikes, y ninguna cadena guardada daba eso). La causa era MIA, no del indicador:

1. **El auditor asumia que el log va en UTC−4. La maquina esta en UTC−3 (Argentina)**: cada minuto quedaba corrido
   una hora, apareado con la cadena equivocada y envejecido una hora de mas. Con la hora correcta, la cadena que la
   capa tenia (la `ultima` generada 17:06:18Z, sello de CBOE 17:05:38, o sea 8 min de nube + 15 de CBOE) y el
   horizonte del grafico, `Rebobina --prueba --ahora 2026-09-15T17:13:55Z --horizonte Todo` reproduce la linea de
   las 14:13:55 **exactamente**: strikes 282, netVol −7,393B, netOi +0,753B, D1 29.000 = +1.595M, D2 28.900 =
   −2.227M, majors 29.050 / 28.900, zero 28.991 (en el libro). Regla 7 del protocolo: se dice de frente.
2. **El grafico con las capas estuvo en horizonte "Todo"** hasta el reinicio de las 19:22 (arranque de las 14:09:43:
   `raiz=NQ horizonte=Todo`); las capas copian los ajustes de la primaria, horizonte incluido. Firma en el log:
   strikes 280-282 y netOi positivo (con Hoy: 260 y negativo). Desde las 19:22 todos los graficos estan en Hoy.
3. **Ni la nube ni el archivo local guardaban cada version de la cadena** (los dos deduplican por el sello de CBOE
   `ts|base`) y el log de la capa no decia que cadena uso. Arreglado en 1.10b (seccion 5).
4. **La nube "cada minuto" corre cada 8-25 minutos** (`gh run list --workflow=cadenas.yml`: 13:13, 13:29, 13:42,
   13:51, 13:59, 14:22 UTC…): el cron de GitHub no garantiza el minuto; el radar (actualizar.yml) va cada 3-15 min.
   El dato de las capas es CBOE 15 min tarde MAS hasta 25 min de nube.

**Resultado con la hora correcta** (`auditar_estela.py`, tolerancia 15 pts, misma cadena):

| Tramo | Horizonte usado | QQQ | SPX | SPY | NDX |
|---|---|---|---|---|---|
| Rueda (hasta 19:22) | Todo | 12/26 | 47/81 | 23/40 | **96/101** |
| Rueda (hasta 19:22) | Hoy (control) | **26/26** | **74/81** | **38/40** | — |
| Noche (desde 19:23) | Hoy | **45/45** | **41/41** | **43/43** | **45/45** |

Queda abierto (no se afirma): NDX cierra exacto con Todo y los ETF cierran mejor con Hoy, aunque el codigo copia el
mismo horizonte a todas las capas. Desde 1.10b cada linea AUDIT de capa anota `horizonte=`, `cadenaTs=` y `gen=`:
manana se aparea exacto y se cierra. Los pocos minutos que faltan en la rueda son saltos al strike vecino (SPX
7575↔7580, SPY 755↔750) con la cadena archivada mas cercana, que puede no ser la version exacta (causa 3).

## 5. Corregido hoy (Gamma Hoy 1.10b, DLL instalada y ATAS reiniciado)

- **Edad real**: la leyenda de cada capa y la cabecera de la primaria suman la edad hasta ahora (antes: "dato de
  hace 25 min" con una cadena de CBOE de las 19:29, o sea 74 min a las 20:43). Protocolo, reglas 1 y 4.
- **Trazabilidad**: la linea `AUDIT capa=…` anota `cadenaTs=` (sello de CBOE), `gen=` (cuando la genero la nube),
  `horizonte=` y `fuente=` (radar o ultima por API/raw).
- **Archivo local completo**: `local-<raiz>-<dia>.jsonl` guarda cada version bajada (el sello incluye `generado`).
- **Auditor**: `capas_nq.py` tiene `HORIZONTE` (Hoy/Semana/Todo, igual que `PasaHorizonte`); `auditar_estela.py`
  aparea por `cadenaTs`/`gen` cuando existen y toma el horizonte que anota la capa. Falta correrlo en una rueda
  completa con 1.10b para el cierre exacto minuto a minuto.

### Visto en pantalla (20:58 local, 1.10b)

Leyenda: NDX "dato de hace 32 min", QQQ 17, SPX 36, SPY 36 → 21 al minuto siguiente (bajo una cadena nueva), ES 2
(viva local); cabecera "vol CBOE 32 min tarde".
Contra el log: NDX: cadena de CBOE 23:41:22Z generada 23:47:58Z -> edad real ahora 32 min (log 2026-09-15T20:58:14); QQQ: cadena de CBOE 23:55:57Z generada 23:56:21Z -> edad real ahora 18 min (log 2026-09-15T20:58:14); SPX: cadena de CBOE 23:37:32Z generada 23:39:28Z -> edad real ahora 36 min (log 2026-09-15T20:58:14); SPY: cadena de CBOE 23:55:48Z generada 23:56:24Z -> edad real ahora 18 min (log 2026-09-15T20:58:14). Antes de 1.10b la misma leyenda habria dicho ~25 min.

## 6. Propuesta (no implementada: espera la palabra del operador)

De noche (16:00 → 9:30 ET) las dominantes de las capas son reales pero flacas (cientos de M contra miles de M de
dia). Propuesta: dibujarlas atenuadas con la marca "noche · libro flaco" y dejar punteado, rotulado "cierre", el
ultimo tunel de la rueda (D1/D2 de las 15:59) hasta la apertura. Es lo que hacen los tableros profesionales
(memoria como-dibujan-los-pros): no inventan un tunel donde todavia no hay gamma. Alternativa: dominantes por
interes abierto del vencimiento mas cercano, pero a las 20:42 ese libro tambien era flaco (netOi −0,225B).

## 7. Como repetir

```bash
cd "C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex" && python laboratorio/auditar_estela.py QQQ SPX SPY NDX
```

Un minuto puntual con el nucleo C# (la cadena sale del `local-NQ-<dia>.jsonl` o de `ultima-NQ.json`):

```bash
atas/Rebobina/bin/Release/Rebobina.exe --prueba cadena.json --precio 28966 --ahora 2026-09-15T17:14:56Z --horizonte Hoy --tabla 20
```

Ojo con la hora: el log del indicador va en hora local de la maquina (UTC−3); `--ahora` va en UTC.

## 8. "Las ambar y los zero grises no aparecen" (pregunta de las 21:05)

- **Las ambar son la primaria del grafico.** En el grafico que mira (cabecera "base CRUDA 148 ticks") la primaria es
  el libro NDX, y desde el bloque 18 de hoy se oculta a proposito cuando la capa NDX esta prendida: es la misma cuenta
  y se dibujaba dos veces (la leyenda lo dice: "primaria (ambar) = NDX: misma cuenta que la capa NDX, oculta"). Las
  mismas dominantes estan dibujadas en blanco por la capa NDX; esta noche caen en 29.553 / 28.903 (libro flaco,
  seccion 2), sean ambar o blancas. Para verlas ambar de nuevo: "Capas: atenuar primaria %" = 100 o apagar la capa NDX.
- **El zero gris esta**: es la raya guion-punto con la sigla "0Γ NDX" en 29.165 (se ve en su propia captura).
- **Lo que recuerda "cerca del precio de noche" era el libro QQQ**: anoche (14-09, 21:17-22:58) la primaria del
  grafico de NQ era `libro_CBOE_QQQ` y sus dominantes estaban a +5…+63 / −3…−36 pts del precio con 0,8-1,2B.
  Esta noche el libro QQQ de manana tiene sus barras grandes en 708 / 700 (29.395 / 29.063, +115 / −217 pts,
  625M / −545M): es el dato del dia, no el dibujo. Esos niveles se ven en celeste (QQQ D1 / D2).
- **El libro vivo de Rithmic (capa RITHMIC) quedo vacio por mis reinicios**: el volumen del dia de las opciones de
  NQ por Rithmic se acumula solo mientras ATAS esta abierto, y hoy reinicie dos veces (19:22 por el contrato
  MNQZ6, 20:56 por 1.10b). A las 21:02 la capa RITHMIC tiene netVol 0,002B (nada) y Rithmic rechazo las series
  del 15 y 16-09 ("no data"), como en la memoria nq-rithmic-pierde-0dte. Se recupera con la rueda de manana. Es un
  costo real de reiniciar de noche: queda anotado.

### 8b. Pedido de las 21:20: "que se vean las ambar por defecto, con su estela" (Gamma Hoy 1.10c)

- Cambio: la primaria (ambar) y su estela se dibujan SIEMPRE, al 75 % con capas prendidas (antes 40 %, y 0 si una
  capa dibujaba el mismo libro). Ocultarla por duplicada paso a ser el ajuste "Capas: ocultar la primaria si una
  capa dibuja el mismo libro", apagado por defecto. La leyenda dice "las ambar son ese mismo libro, con su estela".
- Calculado antes de tocar nada (capas_nq.py sobre las cadenas de las 23:41Z y 23:55Z, fut del log):

| Libro | Por volumen (lo que dibuja) | Por interes abierto (alternativa de noche) |
|---|---|---|
| NDX (ambar en este grafico) | 29.553 (+289) con 221M / 28.903 (−361) con −64M | 29.403 (+139) con 116M / 29.003 (−261) con −48M |
| QQQ (el otro MNQ) | 29.387 (+111) con 645M / 29.055 (−221) con −552M | 29.470 (+194) con −55M / 29.055 (−221) con −117M |

  Anoche (14-09, 21:17-22:58) la primaria QQQ estaba a +5…+63 / −3…−36 pts con 0,8-1,2B: eso es lo que el operador
  recuerda "cerca del precio". Esta noche ni por volumen ni por interes abierto hay una barra grande cerca del precio
  en ninguno de los dos libros: las ambar vuelven a verse, pero caen lejos porque el dato de hoy es ese.
- Visto en pantalla 21:28 (1.10c): rotulos ambar del Max Change arriba a la derecha, estela amarilla sobre las velas
  de la rueda, NDX D1 29.553 / 0Γ 29.166 / D2 28.903 en la escalera (son la primaria). En ese momento la capa NDX
  estaba APAGADA en el grafico (el log deja de tener AUDIT capa=NDX desde el reinicio 21:25; no fue un reset: los
  defaults de todas las capas son false y QQQ/RITHMIC/SPX/SPY/ES siguen prendidas). Con 1.10c, al prender NDX la
  ambar sigue visible; el % de atenuacion guardado en su grafico sigue en 40 (el default 75 es para graficos nuevos).

## 9. El episodio anterior, revalidado, y la falla real de esta semana (21:40-21:50, Gamma Hoy 1.10d)

El operador insistio: "hace dias tuvimos el mismo problema y encontraste una formula mal". Revisado en las
transcripciones y las memorias:

- 10-09 23:34 y 11-09 03:22: "las dominantes de MNQ desde las 17 hs con 500 arriba y 200 abajo". El arreglo de
  entonces (1.8i) fue el **empate tecnico 20 %** (entre barras comparables gana la mas cercana al precio), y la
  memoria dominantes-de-noche-resto-de-ayer concluyo que la cuenta estaba bien y el libro de la noche es el resto
  de manana. Esta noche esa regla esta activa y se verifico barra por barra (seccion 2 y capas_nq.py): NDX arriba
  solo 29.250 (221M) es comparable (la siguiente, 95M, no llega al piso de 177M); abajo solo 28.600 (−64M); QQQ 708
  (648M) le gana a 710 (632M) por cercania. La regla hace lo que debe.
- Base y razon contra Yahoo esta noche: NQ=F − ^NDX 309 vs 302,5 del indicador (2 pb); NQ=F / QQQ 41,5112 vs
  41,5129 (0,4 pb). Ninguna base rota como la del 09-09.
- **Lo que SI estaba roto** (y explica que "antes de noche se veian cerca"): el libro VIVO de Rithmic. Desde que los
  graficos estan en el trimestre nuevo (MESZ6/MNQZ6), el puente pedia las series de esta semana (15, 16, 17 y 18-09)
  con NQZ6/ESZ6 y Rithmic contestaba "no data" (log 14:09, 19:23, 20:59): esas weeklies son opciones sobre
  NQU6/ESU6 hasta el 18-09. Sin ellas el libro vivo quedo sin 0DTE ni 1DTE toda la semana del roll (capa RITHMIC a las
  14:30: 41 strikes, −0,010B). El 11-09, con los graficos en U6, ese mismo libro tenia bandas a −18 pts de noche.
- **Arreglo 1.10d** (CadenaViva.cs + PuenteRithmic.cs): busca el trimestre que vence en el servidor; si todavia
  cotiza, las series hasta su fecha se piden con su codigo y sus strikes se dibujan corridos por el spread vivo
  Z6 − U6 (ventana al dinero, filas, bloques grandes, volumen por strike); sin el precio del viejo no se dibujan.
- **Medido tras reiniciar (21:40)**: "roll: NQU6 en 28.984,38 y NQZ6 en 29.275,5: strikes corridos +291,13"; "774
  contratos de 20260915 Weekly pedidos con NQU6", 806 del 16-09, 1.018 del 18-09 (ES: 720 / 714 / 872 con ESU6);
  "5 vencimientos, 3.696 contratos" contra 1.672 antes. Capa RITHMIC 21:40:50: D1 29.290,7 (−98M) y D2 29.269,0
  (+8M) con el futuro en 29.274. **Visto en pantalla 21:45**: "RITHMIC · en vivo · D1 29.291 D2 29.269", escalera
  "RITHMIC D1 ▲ 29.291 +15" y "RITHMIC D2 ▼ 29.269 −7" con el precio en 29.276,50. Los tamaños son chicos porque el
  volumen acumulado se perdio con los reinicios; crece con la rueda de manana.
- Queda: la semana del roll se repite en marzo, junio, septiembre y diciembre; mirar "roll:" en el log esos dias.

## 10. Auditoria externa de las dominantes, libro por libro (16-09, 01:10 local) — `laboratorio/auditar_externo.py`

Pedido: "audita todo indice, derivado o futuro a nivel dominantes, compara manualmente contra ATAS y contra dos o tres
fuentes de afuera". Tres fuentes: CBOE crudo CON SUS PROPIAS GRIEGAS (no la gamma nuestra), InsiderFinance (gamma e
interes abierto calculados por ellos) y Yahoo (spot). Misma regla de dominantes en todos (una por lado, radio 2 %,
empate 20 %), llevada a NQ con la razon/base del log.

| Libro | ATAS (log 01:10) | CBOE con griegas de CBOE | Nuestra gamma sobre CBOE crudo | gamma CBOE / nuestra | Yahoo vs CBOE |
|---|---|---|---|---|---|
| QQQ | 700 / 708 → 29.093 / 29.425 | 700 / 708 | 700 / 708 | 0,957 (n 56) | +10 pb (Yahoo 20:00Z) |
| SPY | 755 / 760 → 29.210 / 29.403 | 755 / 760 | 755 / 760 | 0,984 (n 58) | 0 pb |
| SPX | 7580 / 7620 → 29.289 / 29.443 | 7580 / 7620 | 7580 / 7620 | 0,972 (n 122) | 0 pb |
| NDX | 28600 / 29250 → 28.903 / 29.553 | 28600 / 29250 | 28600 / 29250 | 0,901 (n 183) | 0 pb |

- **Los tres coinciden strike por strike en los cuatro libros**: la dominante no depende de quien calcule la gamma
  (la de CBOE es 2-10 % mas chica que la nuestra por su convencion de tasa/tiempo; el orden de las barras no cambia).
- **Muros por interes abierto** (otro objeto: posiciones de ayer) contra InsiderFinance con su gamma: QQQ 700/710 =
  700/710; SPY 760/757 = 760/757; SPX 7625/7525 = 7625/7525; NDX 29100/29000 vs 29100/28500 (la segunda barra difiere:
  −51M vs −64M, dos barras chicas). Gamma de InsiderFinance / CBOE: 0,75 a 0,99 (griegas propias, y sus datos de SPX/NDX
  son de las 20:04Z).
- **NQ y ES (opciones del futuro por Rithmic)**: no hay fuente externa gratuita con volumen por strike de opciones de
  futuros; se contrastan contra la cuenta interna (auditar_todo, seccion 2) y contra CBOE por la relacion NQ/NDX. Queda
  como limite conocido.
- **Niveles ocultos o coincidentes** (16-09 01:10): la fusion de rayas iguales quedo apagada (1.10l); los majors +Γ/−Γ
  de cada capa venian apagados ("Capas: majors") y en SPX el +Γ (29.559) NO es la D1 (29.443): se prenden para que se
  vea todo. Los muros por interes abierto de las capas no se dibujan (solo la sombra OI de la primaria).
