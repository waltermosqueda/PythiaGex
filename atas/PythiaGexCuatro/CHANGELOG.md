# CHANGELOG — PythiaGex 4.x

## 4.1.4 (09-10-2026, instalada 05:16 ART y auditada en pantalla) — rótulos de los tramos de historia, defaults apagados y arreglos de la revisión de la 4.1.3
- (agente principal, después de la verificación) `S_ZEST_QQQ_vol` ("QQQ 0G") también apagada por defecto: pedido del operador 09-10 ~03:05 ("sacala de la visualización predeterminada, no del código"); se sigue calculando. Pruebas A5/A6/A8/A9/A18 actualizadas (197/197); correr_todo 13/13 en verde (DLL 05:10:08, 772.096 bytes). En pantalla 05:18: 'NDX muro V/OI · NDX major V · 2.0 NDX 31.040', 'NDX cruce 31.063,86', '2.0 QQQ 31.083,50' (#CC8800).
- Pedido del operador (textual): "la 31041 31040 etc dominantes, la doble o triple raya no tiene rotulo/etiqueta y es importante" y "la linea roja
  no se que es, falta marcarla porque se comporto como lo que buscamos". La columna de la derecha solo nombra el nivel VIGENTE de cada serie: cuando
  una raya deja de serlo, su estela quedaba sin nombre.
- **Rótulos de los tramos de historia** (`_modulos/pantalla/ArmadoPantalla.cs`, `RotularTramos`; ajuste NUEVO `Tramos41Rotulos` "Rotulos de los
  tramos de historia", prendido, "6. Pantalla", orden 28; ningún .ws lo tiene). En la misma pasada de la estela se siguen los TRAMOS de cada serie
  visible (mismo precio a <= 0,5 pt del último, huecos de hasta 3 velas sin dato). Cada tramo de >= 15 velas del gráfico **y >= 8 velas m2 de la
  historia (unos 15 min; revisión de la 4.1.4)** que NO es el vigente (el vigente llega a la última vela, con el gráfico en la última vela, y la serie
  tiene un nivel actual en ese precio: lo nombra la columna) lleva en SU FINAL un rótulo chico: letra tamC − 1, cada nombre en el color de su serie,
  sombra y caja tenue, nombre corto + precio ("NDX muro V 31.040,74"). Solo un tramo que termina mientras la MISMA serie sigue en el MISMO precio
  (a <= 0,5 pt en ese momento: el muro C y el muro P en el mismo strike) no lleva rótulo propio: es la misma raya (la nombra la columna si es la
  vigente, o su rótulo al final). Rayas a <= 1,5 pt con el final a <= 40 px: un solo rótulo ("NDX muro V/OI · 2.0 NDX 31.041", precio redondeado si
  junta series distintas; como mucho 4 nombres, los de las series que más velas dibujaron, sin "+N"). Lugar: arriba de la raya; si choca, abajo;
  después un renglón más arriba o más abajo; si no entra, se descarta (los más largos primero). Nunca sobre la columna de etiquetas de la derecha
  (ni en su franja), ni sobre la pestaña, ni en la franja de arriba que reserva `MargenSup4` (56 px: leyenda y cartel rojo de ATAS); tope 14.
  - Con la historia REAL de esta noche (copia de solo lectura de `niv-2026-10-09-MNQZ6.jsonl` hasta las 05:44Z en `prueba/datos`, las casillas de
    "MNQ liviano.ws", el gráfico del operador de 852 px con 781 visibles, 00:20Z a 05:44Z): la doble/triple raya sale **"NDX muro V/OI · NDX major V
    31.040"** (00:20Z → 05:23Z) y la línea roja era el **cruce abajo del zero de NDX por volumen** (ZTP_NDX_vol): **"NDX cruce 31.063,86"** (01:52Z →
    03:09Z) y **"NDX cruce 31.063,77"** (03:16Z → 03:53Z). Con las R20 calculadas por la réplica (arnés replica20 §8; el archivo no las tiene antes de
    las 05:37Z: la 4.1.3 las completaba en memoria): "NDX muro V/OI · NDX major V · 2.0 NDX 31.040" al final de los muros (05:23Z) y, donde terminan
    el 2.0 QQQ y el 2.0 NDX de 31.042 (01:05Z), su propio rótulo "2.0 QQQ · 2.0 NDX 31.042" (antes de la revisión iban todos en el de las 05:23Z).
  - Costo (arnés de pantalla, E). **Corrección de la revisión**: los 12,8/14,2 ms que decía acá NO eran el peor caso de los rótulos (niveles quietos:
    el atajo deja 66 tramos). El peor caso medido (33 series × 2 niveles que saltan cada 16 velas, 2.000 velas, 8.192 tramos) costaba 222 ms por
    render con la 4.1.4 del constructor (572 ms con 4 niveles); con la revisión, 11 ms (7 ms sin rótulos). Caso normal (200 velas, 7 series) 0,8 ms.
- **Defaults apagados** (pedido: "estas dos que marque (31076) podes desactivar por defecto ... suman ruido al ser ya superadas por la formula de la
  2.0 qqq"): `S_MAJORS_QQQ_oi`, `S_MUROS_QQQ_oi`, `S_FAM_MUROS_oi` y `S_DOMS_QQQ_vol` (`S_MUROS_QQQ_vol` ya lo estaba). SIN renombrar: su
  "MNQ liviano.ws" ya las tiene en false; el default nuevo vale para un gráfico nuevo. `SeriesPantalla`/`CatalogoFamilia.PrendidasPorDefecto` = 7.
- **Arreglos de la revisión de la 4.1.3** (`hallazgos_413.json`, uno por uno):
  1. Etiquetas de las series "como la 2.0" tapadas en un grupo (QQQ dom D1 en el MISMO strike que el muro de QQQ: tapada 313 de 345 minutos; 2.0 NDX
     D1 a 0,23-0,36 pt: 345 de 345) — REAL, arreglado: en un grupo, las series R20_/DOMS_ que no son la cabeza van nombradas en la etiqueta, hasta
     dos, AL FINAL, después del precio y la fuente de la cabeza ("QQQ P −2,19B 31.076 OI · QQQ dom D1", "2.0 QQQ D2 +266M 31.042,06 V · 2.0 NDX D1";
     orden corregido en la revisión de la 4.1.4, ver abajo). NO se cambió el orden de dibujo de la estela (la rayita
     azul de QQQ dom sobre la naranja del muro): ahora la etiqueta nombra las dos; mover el orden cambia lo que se dibuja (queda para su OK con captura).
  2. Razón de la 2.0 calculada una sola vez por cadena — REAL (reproducido: sin el 10-08 en la cinta la razón de esta noche sale 41,625237 "ultima
     valida"; con la vela, 41,444667: 31.010,80 contra 31.083,50 en la raya D1), arreglado en `Replica20`: las cadenas cuya razón salió sin la vela de
     1 min de la cinta (o sin el precio de cuando se bajaron) se vuelven a mirar cada `SondeoCadaS` = 20 s; si la vela apareció se rehace el estado
     entero en orden (igual que un arranque con la cinta completa) y, si cambió alguna razón, el motor rehace los minutos ya calculados desde esa
     cadena (`IExtrasRehacer`: solo R20_QQQ_vol y su "2.0 QQQ"; las 21 series no se tocan) y los vuelve a guardar (línea nueva: gana la última).
     Arnés replica20 §7: 161 minutos rehechos, iguales a un arranque con la cinta completa en 162 de 162, y releídos del archivo. Queda (no es de la
     réplica, viene de la 4.1.0): si una fuente todavía carga cuando el motor arranca, los minutos de antes quedan sin libros hasta el próximo
     reinicio (cota: `EsperaArranqueS` 90 s). Se vio en la corrida de base de esta noche con la PC cargada: e2e 'utc' y 'ny-vivo' con la historia de
     CBOE en 631 s dieron 1338 minutos con libros en vez de 1378 (la corrida de la 4.1.4, sin carga, 1378 e idéntica a la del revisor). Propuesta,
     sin hacer: exigir las fuentes cargadas sin cota de tiempo (o reintentar los minutos sin libros cuando la fuente termina).
  3. El texto de la conversión en el detalle mentía con un respaldo — REAL, arreglado: `ConvDe` lee el origen real del texto de la fuente ("= CRUDA:
     MNQ de ahora / QQQ spot (la vela del ultimo trade era de otro contrato: guardia del 0,6 %)", "la ultima razon valida (hace N min)", "la mediana
     de la rueda", "TEORICA: el carry"...) y, si es un respaldo, agrega debajo un renglón NARANJA "conversion de RESPALDO de la 2.0 (...): la raya
     puede quedar corrida". La conversión de siempre conserva su texto.
  4. Color de "2.0 QQQ" (#E8C83C) igual al "QQQ 0G" (#FCD34D): CIEDE2000 3,8 — REAL, arreglado: **#CC8800** (ámbar oscuro, entre la D1 #E8C83C y la
     D2 #E8A838 de la 2.0): 20,5 contra el QQQ 0G y >= 12 contra cualquier serie del catálogo.
  5. La razón de la 2.0 depende del marco del gráfico donde corre la 2.0 — REAL: la réplica es la de la 2.0 en un gráfico de **1 min**; en 2 min la
     vela de las 16:14 NY cierra 30.993,25 (41,4487 contra 41,4447, ~3 pts de noche). **Corrección del informe de la 4.1.3**: los 3 minutos QQQ
     distintos del 09-10 (22:00, 22:02 y 22:03 UTC) eran la 2.0 en un gráfico de 2 min (y a las 22:00 otra bajada), NO "la instancia previa / I1
     recién arrancada". Documentado en la pestaña (fuente "2.0 QQQ"), en la descripción de la casilla y en el README; sin ajuste nuevo de marco.
  6. El precio con que se pone la réplica — medido y CAMBIADO: `OpcionesReplica20.FutDelInstante` (prendido) = el último precio del MNQ al empezar
     el minuto (el último tick <= t, a lo sumo 120 s antes; lo mismo en vivo que al recalcular) en vez del cierre de la última vela m2 cerrada.
     Arnés replica20, variante E contra la D (AUDIT de la 2.0 del 09-10): minutos iguales a la 2.0 QQQ 113 de 138 contra 97, NDX 113 de 132 contra
     101; |fut − fut de la 2.0| mediana 2,25 contra 3,25 pts, máximo 12,75 contra 22,25. Las rayas NO se mueven (strike × razón / strike + base):
     cambia el monto y, en el borde de los 100 pts, qué strike entra. Los minutos que ya guardó la 4.1.3 esta noche quedan como están.
  7. El e2e no corría con la Replica20 — REAL (hueco de cobertura), cubierto: escenario e2e NUEVO **ny-extras** (el host como en producción, con
     `Extras` = Replica20): las 21 series, la historia, los actuales y las fuentes sin las claves R20_/DOMS_ dan IDÉNTICOS a 'ny' (sección 6).
- Además (no era de la revisión; lo encontró `correr_todo` con los datos de esta noche, ya fallaba con la 4.1.3): posiciones **P3** (linealidad del
  cambio) fallaba en NQ OI 10-09 05:59:55Z, K 31.160: +9 calls y +9 puts con la MISMA IV (0,206258) se anulan en el neto y `PerfilLado.Armar`
  descartaba la fila de la fotoΔ: el cambio del muro C y del muro P de ese strike salía 0. Arreglado: la fotoΔ se valúa con "aporta si CUALQUIER
  lado es distinto de 0" (`PerfilLado.Armar(..., porLado: true)`, solo desde `CambiosFamilia`; las series siguen con el neto). Prueba nueva P3c.
- **Revisión de la 4.1.4** (revisor adversarial, `rev414`; cada hallazgo reproducido con su arnés antes de tocar nada), todo en
  `_modulos/pantalla/ArmadoPantalla.cs`:
  1. (media) La regla de "absorber" contradecía el pedido — REAL, ARREGLADO. Un tramo terminado se absorbía en cualquier raya a <= 1,5 pt que
     siguiera: si esa raya era la VIGENTE quedaba sin nombre en toda la pantalla (caso S1: el 2.0 NDX de 31.041,83 adentro del muro NDX V vigente,
     "rotulos: (ninguno)"; noche real con las R20: 352 casos minuto-tramo sin nombre, p. ej. el major NDX V 31.041,37 de 22:16Z a 22:47Z sin nombre
     173 min), y si no, su nombre iba al final de la otra raya (S2: el cruce de las velas 0-20 nombrado 550 px después; 1.036 casos a más de 40 px).
     Ahora solo se absorbe la MISMA serie en el MISMO precio (el muro C y el P en el mismo strike), mirado cuando el tramo termina (`Sigue`) y no
     con el precio final de la otra raya: el muro de NDX deriva con la base (31.041,47 a las 22:02Z, 31.040,42 a las 05:23Z) y con el precio final
     el C/P de 30.800 por OI salía rotulado a mitad de raya. Lo demás lleva su rótulo en su propio final (y se junta con otros solo a <= 40 px).
     Barrido del revisor repetido con el arreglo (noche real con R20, 527 minutos): 0 sin nombre, 0 nombrados lejos; de 9.419 tramos no vigentes,
     7.051 con rótulo en su final, 738 la misma serie que sigue en el mismo precio y 1.630 sin lugar o fuera del tope de 14 (la pantalla del
     operador a 2 px por vela no tiene lugar para más). Pruebas T14 (S1 y S1b), T15 (S2), T16, T20/T20b (C/P que deriva), T12b (03:00Z, la raya de
     31.040 vigente: "NDX major V 31.041,37" en su final de las 22:47Z) y T21 (cobertura cada 2 min de 00:20Z a 05:44Z: 0 lejos, 0 sin nombre).
  2. (baja) Costo cuadrático sin tope — REAL, ARREGLADO: juntar y agrupar entran con los vigentes y los `TRAMO_CAND` = 200 no vigentes más largos
     (orden total; `TramosRecortados` lo cuenta; 0 con datos reales); el juntar ya no compara de a pares (va por `Sigue`). Arnés del revisor:
     P1 (8.200 tramos) 222 → 21,8 ms con rótulos (10,8 sin); P3 (16.400 tramos) 572 → 22,9 ms (12,5 sin). La prueba E2 pasó a medir ESE peor caso
     (antes medía niveles quietos): 11,0 ms con rótulos, 7,0 sin. T19 lo prueba funcional (14 rótulos sin choques con 8.192 tramos).
  3. (baja) `TRAMO_MIN` en velas del gráfico: en gráficos de segundos cada escalón de 2 min de la historia ya era un "tramo" (5 s: 24 velas) —
     REAL, ARREGLADO: además de 15 velas, el tramo tiene que abarcar 8 velas m2 (`TRAMO_MIN_M2`, unos 15 min). En 1 min no cambia nada (15 velas
     seguidas son siempre 8 m2). T17: 5 s con un cruce que cambia cada 2 min, 0 rótulos (antes 8); 8 velas m2 sí, 7 no.
  4. (baja) Rótulos en la franja de `MargenSup4` (10 de 190 en el barrido del revisor con la caja en y < 56) — REAL, ARREGLADO: un lugar con la caja
     arriba de `area.Top + MargenSup4` no se usa (prueba el de abajo y los otros; si ninguno sirve, se descarta). T18; barridos: 0 en la franja.
  5. (baja) En la etiqueta con acompañantes el precio y la fuente de la cabeza quedaban pegados al acompañante ("QQQ P −2,19B · QQQ dom D1 31.076
     OI": el OI es del muro; "2.0 QQQ D2 +263M · 2.0 NDX D1 31.042,06 V": el 2.0 NDX D1 está en 31.041,83) — REAL, ARREGLADO con la opción A del
     revisor: los acompañantes van AL FINAL ("QQQ P −2,19B 31.076 OI · QQQ dom D1", "NDX P −250M 30.941,47 V · 2.0 NDX D1 · NDX dom D2"); el precio
     de cada acompañante sigue en el detalle de la pestaña. No se adoptó la opción B (precio y fuente de cada acompañante: la etiqueta pasaba de 50
     letras). Elección de texto SIN VERIFICAR con el operador: mostrarle captura al instalar. Pruebas C91, R1, R1b, R2, R2b.
  6. (info) Lo verificado sin errores (series, paridad, defaults, color, ConvDe, Replica20, porLado) — nada que arreglar.
  Además: diagnóstico `TramosNoPuestos` (por qué no se rotuló cada raya: "tope", "sin lugar", "columna") para el arnés, y el log del primer render
  dice cuántos tramos, vigentes, descartados y recortados hubo. La descripción de `Tramos41Rotulos` dice el largo en tiempo y la franja de arriba
  (mismo nombre, tipo y default).
- Arneses: pantalla (A17-A19 ajuste nuevo y defaults; T1-T13 tramos; T12 la historia real; R1-R4 revisión; E mide con y sin rótulos; desde la
  revisión de la 4.1.4: T12b, T14-T21, R2b, E2 en el peor caso), replica20 (§1b variante E, §7 razón rehecha, §8 rótulos con las R20: T1 ahora
  pide el rótulo propio del 2.0 NDX de las 01:05Z), posiciones (P3c), e2e (ny-extras; `correr.ps1` lo corre). Las series y la paridad
  (nq/ndx/qqq/fam/integrado/e2e/posiciones/replica20) quedan iguales; en las salidas solo cambian versiones, las marcas "*" de los defaults en el
  e2e y las líneas nuevas. `correr_todo.ps1` del constructor (09-10 03:55-04:01): los 13 pasos con exit=0; pantalla 183 ok. `correr_todo.ps1`
  después de la revisión (09-10 04:48-04:55 hora local): los 13 pasos con exit=0 (la DLL recompilada con build_serial: "Build succeeded");
  nq/ndx/qqq/fam/integrado/tqqq con paridad exacta; posiciones 33/33; replica20 28 OK; pantalla 197 ok; los 7 volcados de foto del e2e (ny,
  ny-reinicio, ny-recalculo, utc, ny-union, ny-vivo, ny-extras) IDÉNTICOS byte a byte a los de la corrida del revisor de la 4.1.4, ny-extras
  IDÉNTICA a ny, y los 10 dif-*.txt de ndx iguales (la revisión solo toca la pantalla).
- Versiones: DLL 4.1.4.0 (AssemblyVersion fija 4.0.0.0), indicador 4.1.4, pantalla 4.1.4, fam 4.1.4, replica20 4.1.4, cambios 4.1.4.
- SIN VERIFICAR en ATAS (no se instaló ni se tocó ATAS): cómo se ven los rótulos sobre las velas reales (y que no pisen la leyenda real de ATAS
  con MargenSup4 = 56), el color #CC8800 y las etiquetas con " · QQQ dom D1" al final. Regla del operador para cambios que tocan lo que se
  dibuja: captura antes/después al instalar. Con la regla nueva hay más rayas con rótulo propio compitiendo por los 14 lugares: en la pantalla
  del operador (2 px por vela) 1 de cada 6 tramos no vigentes queda sin lugar o fuera del tope (barrido con R20: 1.630 de 9.419).
- Visto y NO cambiado (no era hallazgo): cuando la base de NDX salta más de 0,5 pt en el último minuto, la vela m2 de la historia todavía tiene el
  precio de antes y ese tramo llega al borde sin ser "vigente": sale un rótulo al borde al lado de la etiqueta de la misma serie (caso S3 del
  revisor; 4 veces en 1.358 minutos del 08-10 en 5 s). Se puede tratar como vigente si la serie tiene un nivel a <= 1,5 pt; queda para su OK.

## 4.1.3 (09-10-2026) — las dominantes como la 2.0, calculadas adentro
- Pedido del operador (textual): "copia la formula de la 2.0 y agregala a lo que ya tenemos, que tambien funciona, asi vamos refinando y
  calibrando". Quiere ver en la 4.1, PRENDIDAS, las dominantes de la 2.0 (la raya de 31.083,50 del 08-10 = QQQ 750 x 41,4447) junto a las de la 4.1.
- Cuatro series NUEVAS (`_modulos/familia/replica20/Replica20.cs`), todo adentro (no se lee nada de la 2.0):
  - `R20_QQQ_vol` "2.0 QQQ" (ambar #E8C83C, PRENDIDA): replica exacta de la 2.0 — libro QQQ de CBOE, horizonte Hoy, gamma BS r 0,0375,
    gv = (g(ivC) volC - g(ivP) volP) x 100 x S^2 x 0,01; las 2 de mayor |gv| a <= min(2 % del futuro, 100 pts); y SU conversion (razon = cierre
    de la vela de 1 min del MNQ del ultimo trade de la cadena / spot, con sus respaldos). De noche esa razon junta el MNQ de las 16:14 NY con el
    QQQ del after-hours (error medido: |err| mediano 24 pts en 16 noches): por eso es una replica para comparar, no la conversion buena.
  - `R20_NDX_vol` "2.0 NDX" (verde 61,220,151, PRENDIDA): la capa NDX de la 2.0, base = cruda de forwards de la cadena si pasa la cota del carry.
  - `DOMS_QQQ_vol` "QQQ dom" (azul #5B8CFF, PRENDIDA): la SELECCION de la 2.0 sobre el libro QQQ de la 4.1 (razon sincronizada C8).
  - `DOMS_NDX_vol` "NDX dom" (#A9C4FF, APAGADA: sin medir): la seleccion de la 2.0 sobre el libro NDX de la 4.1 (base C7).
  Rol D1/D2, monto con signo (escala 1). Etiqueta "2.0 QQQ D1 +417M 31.083,50 V". La pestaña dice de donde sale cada una y su conversion
  ("razon 2.0 = MNQ del ultimo trade / QQQ spot" contra "razon sincronizada").
- Fuera de la cuenta de la vista previa: el motor las agrega DESPUES de `ReglasFam.Minuto` (`OpcionesMotorFamilia.Extras`), en campos propios
  (`RegistroMinuto.Extra/ExtraStrikes/MetaExtra`); CONF y FAM no las ven; sin `Extras` el motor es el de la 4.1.2. Se guardan en
  `niv-*.jsonl` con claves nuevas en "s" y la conversion en "mx" (un minuto sin extras queda byte a byte igual). Los minutos guardados por la
  4.1.2 se completan en memoria (solo las R20: las DOMS necesitan el libro del minuto).
- Cambios por nivel: DOMS_* con el cambio NETO (como M+/M-); R20_* NaN con nota (su S y su conversion no son las del libro de la 4.1).
- Casillas NUEVAS (`S_R20_QQQ_vol`, `S_R20_NDX_vol`, `S_DOMS_QQQ_vol`, `S_DOMS_NDX_vol`) en "4b. Dominantes como la 2.0".
- Arnes nuevo `atas/_test_cuatro_paridad/replica20/` (en `correr_todo.ps1`): paridad minuto a minuto contra `paridad_2_0_minuto.csv` (los AUDIT
  de la 2.0 del 09-10) y el motor con/sin extras (las 21 series identicas), persistencia, completado, cambios y etiqueta con datos reales.

## 4.1.2 (09-10-2026 01:18, instalada) — montos y cambios en las etiquetas
- Pedido del operador: "que se vean en las etiquetas los millones o billones en tiempo real (podés sacar esos +1 +2) así puedo saber si
  están aumentando o sacando posiciones"; formato pedido "ndx M+ P 558m 31500 oi". Etiqueta: `LIBRO ROL MONTO [CAMBIO] PRECIO FUENTE`
  (p. ej. `NQ P −60M ▲44M 31.100 OI`); sin "+N" (el grupo muestra el de mayor |monto|); el zero/cruces sin monto.
- Cambio por nivel (`_modulos/familia/posiciones`, en el hilo del host sobre copias): series por VOLUMEN = volumen operado en 5/15/30 min
  en el mismo strike valuado con la gamma de ahora (el precio no lo mueve; no dice si abren o cierran); series por OI = OI de la
  publicación vigente menos la anterior (saltos detectados por noche: NQ ~01:30Z, NDX ~06:30Z, QQQ ~07:30-09:15Z). ▲ crece / ▼ se achica
  en magnitud / ♦ el neto dio vuelta. CBOE con la misma hora de dato = 0 exacto (alterna versiones después del cierre).
- Montos de las rayas 3.0 (campo nuevo "gm" en la estela, NQ ×0,2). Etiquetas ↑/↓ fuera de pantalla con renglón propio (antes tapaban a
  la primera visible). Ajustes nuevos: Monto41Rotulos, Cambio41Rotulos, Cambio41Ventana (5/15/30).
- Auditado en pantalla (09-10 01:22): montos y cambios recalculados desde la cadena cruda por fuera del indicador, coinciden
  (p. ej. put 31.100 NQ OI 56→210: −43,68 M indep. contra ▲44M). Arneses: pantalla 140/140, posiciones 32/32, paridad intacta.

## 4.1.1 (08-10-2026 22:12) — la capa 3.0 de NDX/QQQ ya no se corre de noche
- Medido en vivo a las 22:05 NY, con ATAS recién abierto y sin mediana propia guardada: la capa NDX heredada de la 3.0 (TRES_NDX, una de
  las 8 rayas por defecto) convertía con la "muestra alineada" (MNQ de las 20:48 menos el NDX congelado a las 16:00) y dio base 299.44,
  después 287.44 (la "base" solo copiaba lo que se movía el futuro), contra 241.03 de la familia y 241.83 de forwards. La raya "3.0 NDX
  D1 31.000,70" era el strike 30.700 dibujado 58 pts arriba. QQQ igual: razón 41.4915 contra 41.4352 de la familia (~42 pts).
- Arreglo (`GammaHoyTresCapas.Mapear`): sin mediana propia y con el spot QUIETO se usa la conversión de la familia (C7/C8, de la foto del
  motor de este mismo indicador); con el spot quieto la alineada ya no sirve para descartar la base de forwards; si no queda nada
  confiable la capa no se calcula ni se dibuja (antes dibujaba corrida con un "OJO" en el log).
- Después (22:16): NDX por forwards 241.83 (la familia da 241.01), QQQ por la razón de la familia 41.4352; "3.0 NDX D2 30.943,10" cae
  sobre "NDX P v 30.941,01" (el mismo strike 30.700). Capturas `capturas/2026-10-08_2211_antes_4_1_1_tres_ndx_corrida.jpg` y
  `capturas/2026-10-08_2216_despues_4_1_1.jpg`. Las estelas de NDX/QQQ escritas por la 4.1.0 esa noche (7 + 7 líneas) se movieron al
  respaldo del scratchpad con ATAS cerrado.

## 4.1.0 (08-10-2026) — un solo indicador, todo adentro
- Pedido del operador: "debe ser independiente siempre… quiero uno solo… que esté todo dentro del indicador". La 4.0.6 era un visor de
  los json de la vista previa (dependía de la 3.0 y de generadores Python) y quedó en blanco cuando se cerró la pestaña de la 3.0.
- Clon de la 3.0 (`herramientas/clonar_4_0.py`, NO re-correr: hay parches encima) renombrado a PythiaGexCuatro, datos en PythiaGex4.
- Descarga propia de CBOE (`_modulos/cboe`): construir/base por forwards/línea flaca idénticos byte a byte al Python; gzip manual,
  trozos de 64 KB con pausa, cadencia por ticker, DNS cancelable, una conexión por pedido, candado entre procesos, poda a 14 días.
- Familia (`_modulos/familia`): minuteros NQ (C2, corrimiento), NDX (C7, C9, oi_fresco), QQQ (C8), motor (CONF, FAM C1, TRES, historia,
  actuales, persistencia por sesión/contrato/modo) y TQQQ (`_modulos/tqqq`, s por cierres, c sincronizado, ancla de noche apagada).
- Pantalla (`_modulos/pantalla`): la UI 4.0.6 sobre la foto del motor (sin json).
- Integración (`IntegracionFamilia.cs`): un host por proceso y contrato en su hilo; bajada = OR de las instancias; al cerrar no suelta
  CBOE con el motor a mitad de un minuto; avisos de CBOE/Rithmic en la pestaña; muestras de QQQ sin duplicar y podadas.
- Verificado: 9 arneses en verde (paridad exacta contra `ref-2026-10-0{7,8}.json`), prueba de punta a punta con las piezas reales,
  revisión adversarial de código ATAS y de autonomía/red. Diferencia esperada en vivo: la página mezcla las bajadas de 4 programas;
  la 4.1 tiene solo la suya (QQQ hasta ~1 pt en algunos minutos).

## 4.0.0–4.0.6 (08-10-2026) — visor (respaldado en `_visor_4_0_6/`)
