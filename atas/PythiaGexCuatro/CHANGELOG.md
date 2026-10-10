# CHANGELOG — PythiaGex 4.x

## 4.1.5e (09-10-2026, compilada, SIN instalar: la instala el agente principal) — el grupo de arriba con la jerarquía de llaves
- Pedido del operador (textual): "pierdo mucho tiempo y tengo que estar buscando que es cada cosa ... adivinando para ver si aparecen/desaparecen
  las dominantes especificas que quiero". Medido por el verificador (09-10 ~19:30): 8 de sus 28 casillas prendidas no dibujaban nada porque
  dependían de una llave Tres41* apagada, y ni el nombre ni la descripción lo decían (salvo NQ M+/M−).
- **Solo [Display(Name/GroupName/Order/Description)]** del grupo "0. PRENDER / APAGAR (todo lo que se dibuja)": ninguna propiedad renombrada ni con
  otro tipo o default (ws_compat W2-W4: 227 de 227 iguales a la 4.1.5); el gráfico no cambia. La fuente de verdad pasa a
  `herramientas/grupo_arriba_415e.py` (idempotente; `--verificar` no escribe y sale con 1 si algo cambiaría; `--anchos` mide cada nombre);
  `grupo_arriba_415c.py` ahora solo avisa y corre el nuevo (su lista vieja borraría la jerarquía).
- **El orden (Order 0..100 sin huecos; los demás grupos siguen >= 1000):**
  1. lo que NO necesita llave, en el orden de siempre y con los mismos nombres: las 35 series de la Familia (NQ, NDX, QQQ, TQQQ, FAM/CONF, clásica,
     2.0, 3.0 rayas); después la pantalla de la 4.1: "Doble eje izq.", **"Estela▸"** con "↳ rot. tramos", **"Etiquetas▸"** con "↳ montos" y
     "↳ cambios ▲▼", y "Pestaña de detalle" (de las cuatro, solo "Rotulos de tramos" se movió: sube de 99 a 37, debajo de su llave);
  2. la 3.0: cada LLAVE (nombre que termina en ▸) inmediatamente antes de sus dependientes (nombre que empieza con "↳ "): "3.0 NQ dom▸",
     "3.0 NQ 0Γ▸", "3.0 tunel▸", "3.0 capas▸", "3.0 F1-F8▸", "3.0 libro▸" (el recuadro 'LIBRO NQ'), "3.0 cabecera▸". Dos sub-llaves:
     "↳ capas 0Γ▸" (Capa3ZeroRombos) y "↳ F1-F8 ver▸" (Formulas3Ver);
  3. al final, lo común a varias llaves de la 3.0: "↳ ver estela", "↳ ver rotulos", "↳ rot. cortos", "↳ rot. rayitas", "↳ rayas largas",
     "↳ sesion ant.", "↳ atravesadas" (su descripción dice cuáles).
- **La primera frase de cada descripción dice qué necesita:** "Necesita la llave '3.0 capas▸' prendida." (dependientes), "Necesita alguna de las
  llaves '3.0 NQ dom▸', '3.0 NQ 0Γ▸', '3.0 capas▸' o '3.0 F1-F8▸' prendida." (comunes), "Llave: sin ella no se dibuja ninguno de los renglones con ↳
  que la siguen." (llaves) o "No necesita ninguna llave..." (lo de arriba). Si además hace falta otra casilla, la frase siguiente empieza con "Tambien
  necesita" o "Solo cambia algo con". Las 35 series de la Familia que no tenían descripción ahora tienen la del catálogo (técnico: criollo. Estado: ...).
- **Nombres medidos** (calibrado con la captura 17:29: la columna muestra ~79 DIP en Segoe UI 12; entran "NDX majors OI" 78,9 y "NDX 0Γ est vol" 77,6,
  se cortan "NDX muros vol" 80,2 y "NDX majors vol" 82,4): las 11 llaves y sub-llaves entran enteras (la más ancha "3.0 cabecera▸" 76,2; por eso
  van sin espacio antes del ▸); de los ↳ solo "↳ NDX techo/piso" (94) se ve cortado ("↳ NDX techo/…"). Los nombres viejos de las series no se tocaron
  (varios ya se veían cortados, p. ej. "NDX muros v…").
- **Tabla llave → dependientes** (cada condición leída en el código; el arnés de la pantalla A23f la vuelve a leer del fuente en cada corrida):
  - `Estela▸` (Estela4) → ↳ rot. tramos (Tramos41Rotulos): ArmadoPantalla sigue los tramos adentro de `if (aj.Estela ...)`.
  - `Etiquetas▸` (Rotulos4) → ↳ montos (Monto41Rotulos), ↳ cambios ▲▼ (Cambio41Rotulos): solo dentro de `if (aj.Rotulos ...)`.
  - `3.0 NQ dom▸` (Tres41Dominantes) → ↳ NQ D1, ↳ NQ D2 (Raya3NqD1/D2: `if (i < 2 ? !Tres41Dominantes : !Tres41Zero)`), ↳ toques (Ver3Toques),
    ↳ NQ M+ OI / ↳ NQ M− OI (Raya3NqMasOi/MenosOi), ↳ perfil visual (Perfil3Visual), ↳ ver majors (Ver3Majors), ↳ NQ M+ vol / ↳ NQ M− vol
    (Raya3NqMas/Menos: `majors = Tres41Dominantes && VerMajorsEf`), ↳ barras perfil (Barras3Lado), ↳ NDX M+ OI / ↳ NDX M− OI / ↳ QQQ M+ OI /
    ↳ QQQ M− OI (Raya3NdxMasOi/MenosOi, Raya3QqqMasOi/MenosOi: `if (Tres41Dominantes) PintarMajorOi`), ↳ apoyo, ↳ 0Γ OI estela, ↳ todos cruces,
    ↳ sin cortes (Ver3Apoyo, Ver3EstelaZeroOi, Zero3TodosLosCruces, Cruces3SinCortes: `if (Tres41Dominantes) PintarApoyo`). 18 casillas.
  - `3.0 NQ 0Γ▸` (Tres41Zero) → ↳ NQ 0Γ (Raya3NqZero), ↳ ver 0Γ estela (Ver3EstelaZero), ↳ ver 0Γ raya (Ver3Zero).
  - `3.0 tunel▸` (Tres41Tunel) → ↳ ver tunel (Ver3Tunel).
  - `3.0 capas▸` (Tres41Capas: `var extra = Tres41Capas ? PintarCapas(...)`) → ↳ NDX D1/D2/D3, ↳ QQQ D1/D2/D3 (Raya3NdxD1-D3, Raya3QqqD1-D3),
    ↳ capas 0Γ▸ (Capa3ZeroRombos) y, colgando de ella, ↳ NDX 0Γ, ↳ NDX 0Γ enf., ↳ NDX techo/piso, ↳ QQQ 0Γ, ↳ QQQ 0Γ enf. (Raya3NdxZeroB,
    Raya3NdxZeroEnfasis, Raya3NdxZeroCerco, Raya3QqqZero, Raya3QqqZeroEnfasis), ↳ linea al eje (Capa3LineaActual). 13 casillas.
  - `3.0 F1-F8▸` (Tres41Formulas: `if (Tres41Formulas) PintarFormulas`) → ↳ F1-F8 ver▸ (Formulas3Ver: `if (!Formulas3Ver) return;`) y ↳ F1 cruce,
    ↳ F2 lado vol, ↳ F3 lado OI, ↳ F4 lado v+OI, ↳ F5 muros vol, ↳ F6 muros OI, ↳ F8 como 2.0 (Formula3F1..Formula3F8b).
  - `3.0 libro▸` (Tres41Recuadro: `bool recuadro = RecuadroLibro3Ver && Tres41Recuadro`) → ↳ ver libro (RecuadroLibro3Ver).
  - `3.0 cabecera▸` (Tres41Cabecera: `if (Tres41Cabecera) Cabecera(..., VerCabeceraEf)`) → ↳ ver cabecera (Ver3Cabecera).
  - Comunes: ↳ ver rotulos, ↳ rot. cortos, ↳ rot. rayitas (Ver3Rotulos, Rotulos3Cortos, Rotulos3Conectores: `if (VerRotulosEf && (Tres41Dominantes
    || Tres41Zero || Tres41Capas || Tres41Formulas)) Rotulos`); ↳ ver estela (Ver3Estela: estela de NQ y de las capas, no la de F1-F8, majors OI ni
    apoyo) y ↳ rayas largas (Rayas3Largas: zero y majors de NQ, apoyo, cruces OI y capas) con NQ dom / NQ 0Γ / capas; ↳ sesion ant.
    (Estela3SesionAnterior) y ↳ atravesadas (Atravesadas3AtenuarB) con NQ dom / NQ 0Γ / capas / F1-F8.
  - Sin llave (37): las 35 series S_* (las dibuja la pantalla de la 4.1 con lo que calcula el motor), "Doble eje izq." y "Pestaña de detalle".
- **Corregido contra lo que se suponía:** los majors por OI de NDX y QQQ NO dependen de "3.0 capas▸" sino de "3.0 NQ dom▸" (los dibuja
  PintarMajorOi, que corre con Tres41Dominantes); "↳ NQ 0Γ" depende de "3.0 NQ 0Γ▸", no de la de las dominantes.
- **Dependencias entre casillas hermanas que NO se arreglaron (cambiarían el dibujo: necesitan aviso y captura antes/después), dichas en la
  descripción:** "↳ todos cruces" solo dibuja con "↳ apoyo" o "↳ 0Γ OI estela" prendida (PintarApoyo sale al principio si las dos están apagadas);
  "↳ rot. cortos" no cambia nada con 'Rotulos: estilo' en Minimal (el default, grupo 9.7); "↳ ver 0Γ raya" y "↳ linea al eje" solo cambian algo con
  "↳ rayas largas" (apagada por defecto); "↳ NQ M+ vol / M− vol" en NQ con el perfil Limpio (el default) no se ven sin "↳ ver majors" en Si; los 0Γ
  de las capas piden "↳ capas 0Γ▸" y el énfasis y el techo/piso de NDX además "↳ NDX 0Γ". Las series de la Familia se ven con "Estela▸" y/o
  "Etiquetas▸" (prendidas por defecto).
- **Reloj de casillas (GammaHoyTresCasillas.cs, solo el log):** lee la llave de la primera frase de cada descripción y en pythiagex4-pantalla.log
  anota al arrancar "prendidas SIN dibujo por su llave apagada: N (...)" (lo que el verificador contó a mano) y, en cada cambio, si la casilla queda
  prendida con su llave apagada, "· <Propiedad>: su llave '3.0 capas▸' esta APAGADA: no se dibuja". Sigue mirando las 101 del grupo (bool y enum).
- **Arneses:** pantalla A13/A17 con los Order y nombres nuevos, A21a/A22 leen grupo_arriba_415e.py, **A23 nueva** (a: la jerarquía en la clase
  real — 9 llaves, 48 dependientes con su llave de arriba, 7 comunes, 37 sin llave arriba de la 3.0; f: 54 casillas en 19 condiciones leídas del
  fuente; g: las 11 llaves y sub-llaves entran enteras en la columna, medido con WPF); ws_compat W6 con la LISTA nueva, **W7** (el script con
  --verificar: el código coincide y es idempotente) y **W8** (la primera frase de las 101); fam: **sección finde nueva** (ver la 4.1.5d de abajo).
- Versiones: FileVersion 4.1.5.5, InformationalVersion 4.1.5e (AssemblyVersion sigue 4.0.0.0); clase 4.1.5e. Los módulos (fam, pantalla, clásica,
  cambios) siguen 4.1.5d: no cambian.
- Pruebas: `correr_todo.ps1` COMPLETO 16 de 16 en verde (09-10 20:18-20:24 ART, DLL compilada con build_serial a las 20:18): pantalla 221 ok
  (A23 nueva), ws_compat 11 OK (W7/W8 nuevas), fam VERDE (finde 27 de 27), adaptadores, cboe, nq, ndx, qqq, fam integrado, tqqq, posiciones 33/33,
  replica20 28/28, clasica 44/44, clasica_dom 23/23 y e2e igual que con la 4.1.5d. DLL `bin/Release/PythiaGexCuatro.dll` 848.384 bytes, sha256
  68a6c47706155914c0566e860dd89cf29f9174bf7e11c22dd3aa37fea5ac6170 (copia en el scratchpad de la sesion, `dll415e`).
- SIN VERIFICAR en pantalla (no se tocó ATAS): que la fuente del diálogo muestre "↳" y "▸" (WPF los toma de Segoe UI Symbol) y que la columna tenga
  el ancho de la captura de las 17:29 (si el operador la ensancha, entran todos). El buscador del diálogo ("Search...") encuentra "NDX D1" igual.

## 4.1.5d — el fin de semana (09-10-2026 ~19:15 ART, INSTALADA por el agente principal: DLL sha256 a95b528a…, correr_todo 16/16 en verde)
- Pedido del operador (textual): "quiero que la 4.0 se vuelva a ver, o sea que tenga memoria".
- Causa: `SesionFamilia.De` en modo NY (Familia41Corregida, el default) toma el día = fecha NY de (t + 6 h): del viernes 18:00 NY al domingo 17:59 NY
  daba una sesión de SÁBADO o DOMINGO, que nunca tiene datos. Tras reiniciar ATAS después del cierre del viernes la 4.1 no dibujaba nada mientras la
  clásica y la 2.0 mostraban lo guardado.
- Arreglo: del viernes 17:00 NY al domingo 17:59 NY la sesión es la del VIERNES (la última con datos, leída de `familia\niv-<día>-<contrato>.jsonl` y
  de las estelas); el domingo 18:00 NY empieza la del lunes como siempre. El modo UTC (paridad con la vista previa) NO cambia. En el motor
  (`Publicar`), con ahora >= FinUtc el aviso de la pestaña EMPIEZA con "mercado cerrado: se muestra la sesion del <día> (cerro hace ...)" (el dato es
  viejo: se dice antes que nada); vale también en la pausa diaria de 17:00 a 18:00 NY.
- Medido (agente principal, ATAS en vivo, 09-10 ~19:16 ART): antes abría la sesión 2026-10-10 con 0 minutos; ahora la 2026-10-09 con 1378 minutos y
  12269 líneas de estela. En pantalla: "PythiaGex 4.0 ▸ · DATO DE HACE 2,3 h · mercado cerrado: se muestra la sesion del 2026-10-09 (cerro hace 79 min)"
  (capturas/2026-10-09_1918_despues_4_1_5d_memoria_fin_de_semana.jpg).
- Arnés (agregado en la 4.1.5e, `atas/_test_cuatro_paridad/fam`, sección **finde**): modo NY — viernes 16:59 abierta (sesión del viernes), 17:00 y 18:05
  sesión del viernes cerrada, sábado 12:00 y domingo 17:59 viernes, domingo 18:00 lunes; el cambio de horario (sábado 31-10 y domingo 01-11 17:59 EST
  → viernes 30-10 [10-29 22:00Z, 10-30 21:00Z); domingo 01-11 18:00 EST → lunes 02-11 [11-01 23:00Z, 11-02 22:00Z), rueda 14:30Z); control entre semana
  (jueves 17:30 sigue la del jueves, 18:00 ya la del viernes): 11 de 11. Modo UTC sin cambios (día = fecha(t + 2 h), con el corrimiento de una hora del
  01-11 de la vista previa): 9 de 9. Motor (dobles, modo NY): viernes 16:59:30 sin aviso; 17:30 el aviso primero "(cerro hace 30 min)"; sábado con un
  motor NUEVO reabre la del viernes DESDE EL ARCHIVO (1380 minutos cargados, 0 recalculados, historia igual a la del viernes); a medio calcular el
  aviso va antes que "calculando la sesion"; domingo 17:59 sigue; domingo 18:00:10 abre la del lunes sin el aviso; ningún archivo de sábado ni de
  domingo: 7 de 7.
- No cubre los feriados de CME (un lunes feriado abre la sesión del lunes, sin datos, como antes).

## 4.1.5d (09-10-2026; instalada junto con el arreglo del fin de semana de arriba: DLL sha256 a95b528a…) — revisión de la 4.1.5b/c
Arreglador de la revisión adversarial de la 4.1.5b y la 4.1.5c (10 hallazgos). FileVersion 4.1.5.4, InformationalVersion 4.1.5d (AssemblyVersion
sigue 4.0.0.0); clase 4.1.5d, clasica 4.1.5d, fam 4.1.5d, pantalla 4.1.5d, cambios 4.1.5d. Ninguna propiedad renombrada ni con otro tipo o default
(ws_compat). LO QUE CAMBIA EN PANTALLA está marcado con ⚠.
- **(alta) La réplica de la clásica supone que la clásica NO mide la rueda.** MEDIDO en su log del 09-10: hasta las 19:32 UTC su primaria fue Rithmic
  (no midió; dibujó con 243,06 del 08-10); desde las 19:32 UTC (16:32 ART), tras los reinicios de ATAS, cayó a CBOE y midió con velas de 1 min: 229,88
  (sello 19:47:37), 232,26, 230,23 y 230,16 (20:09:38). La réplica siguió con 243,06 congelada: de 16:48 a 17:11 ART el 0Γ y las D1-D3 quedaron ~13 pts
  corridos de los de la clásica. No se puede replicar sin leer el estado de la clásica (independencia): se corrigieron los comentarios falsos de
  ClasicaNdx.cs y se dice la salvedad ⚠ en las descripciones de "Clas NDX 0Γ" y "Clas NDX D1-D3", en el catálogo y ⚠ en un renglón NUEVO de la pestaña
  debajo de la fuente "Clasica NDX (replica, CBOE)" (ArmadoPantalla.AVISO_CLASICA).
- **(media) La base de una rueda ya cerrada cambiaba según cómo arrancara el indicador** (09-10 17:35 ART: 229,32 → 229,98 con las mismas 296
  muestras). MEDIDO en el arnés (cinta solo de ticks, como la del arranque de las 17:35 sin las velas del gráfico): 8 muestras del 09-10 tienen la vela
  de 2 min con un hueco de ticks (velas 16:48, 19:31-19:33, 19:43, 19:50, 19:51 y 19:53 UTC: los reinicios de ATAS), 7 de ellas entre las últimas 24
  del día (sellos 19:47:37, 19:48:37, 19:49:37, 19:59:37, 20:06:38, 20:07:38 y 20:09:38). MEDIDO (arnés clasica G3d): con el cierre de 2 min del
  GRÁFICO en esas provisionales (= el de 1 min de su minuto impar, que la clásica logueó para esos mismos sellos) 3 cambian de cierre (19:47:37
  31.117,25 → 31.117,00; 20:06:38 y 20:07:38 31.095,75 → 31.092,75) y la regla del día da 229,32 exacto; con los de la cinta de ticks, 229,98: el
  escalón sale entero de esas velas con hueco.
  Arreglo: las muestras de la regla se GUARDAN (PythiaGex4\familia\muestras-clasica-NDX-<día del sello>.jsonl, como las de C7/C8) la primera vez que se
  miden con una vela COMPLETA (balde escuchado entero o con la vela del gráfico: CintaFamilia.VelaM2Completa, que el integrador pasa en
  OpcionesClasicaNdx.VelaCompleta) y al reiniciar se usan las guardadas; una con la vela incompleta entra provisional, no se guarda y se vuelve a
  mirar cada 20 s. Arnés clasica G1-G3: con la cinta SIN el 08 ni el 09-10, las 877 muestras y las bases (09-10 229,98; 08-10 243,06) salen iguales
  desde el archivo (sin el archivo: la base cae a la del 07-10, 247,54). La vela: VelaMin sigue en 2 (la clásica midió el 08-10 en 2 min y el 09-10 en
  1 min; con 1 la rueda del 08-10 da 242,45 en vez de los 243,06 que dibujó la clásica): el texto de la base dice ⚠ ", vela de 2 min". NO se tocan los
  minutos ya guardados: en el niv del 09-10 quedan 20:36-20:59 UTC con 229,98 (el escalón de 0,66 pt de la estela de hoy sigue).
- **(media) El grupo de arriba no tenía todo lo que se dibuja; '3.0 tunel D1-D2' no dibujaba nada.** ⚠ El túnel ahora se prende SOLO con esa casilla
  (antes pedía además "Tunel sombreado" de 9.7, que el .ws del operador tiene en False; hoy la casilla está apagada, así que no cambia nada hasta que
  la prenda). ⚠ herramientas/grupo_arriba_415c.py pasa de 70 a 101 casillas en "0. PRENDER / APAGAR": + 20 bool de dibujo de la 3.0 que habían
  quedado en 9.x (apoyo, 0Γ OI, todos los cruces, capas rombos/línea, fórmulas y F1..F8, recuadro, sesión anterior, rayas largas, rótulos rayitas y
  cortos, cruces sin cortes, atenuar atravesadas) y + 11 listas (perfil visual y "ver ..." de la 3.0, barras, doble eje); sólo [Display]. Las
  descripciones de "3.0· NQ M+/M−" dicen que en NQ con el perfil Limpio piden "3.0 ver majors" en Si. El reloj de 250 ms mira también las listas.
- **(media) Casillas del operador apagadas (17:33).** No es del código y no se tocó ATAS: a las 18:08:37 una recarga volvió a la lista de las 17:28:45
  (la del .ws de las 17:19) y entre las 18:10 y las 18:48 hubo 893 cambios de casillas a mano; a las 18:48 quedaban prendidas 17 (sin las dos R10, sin
  Tres41Dominantes, sin las R20). Una reinstalación vuelve a lo guardado en el .ws.
- **(baja) Comentario del reloj.** Corregido: sin ticks el Tick de 5 s ya redibujaba (peor caso ≤ 5 s, ahora ≤ 250 ms); lo de "nunca" no era cierto.
- **(baja) ⚠ Las D1-D3 de la clásica ya no llevan cambio ▲▼** (antes D1/D2 tomaban el del libro NDX de la 4.1 y D3 ninguno): igual que las R20, NaN
  con nota "replica de la clasica: sin cambio por nivel (su base y su S no son las del libro de la 4.1)".
- **(baja) ⚠ Detalle de R10_NDX_dom:** "(strike NDX 30.900, centroide ±12)" (antes "(en el indice NDX 30.900)", la leyenda del zero).
- **(baja) ⚠ Color de "Clasica NDX" D1-D3:** #b8f8d8 (menta pálido; el #9dffd2 quedaba a CIEDE2000 11,4 del "2.0 NDX"): ahora 14,3 del 2.0 NDX, 14,7
  del NDX major V y 21,2 del blanco del 0Γ.
- **(baja) Bordes de la clásica:** sin cruce del zero las D1-D3 se calculan igual (el 0Γ queda sin nivel; Falta no lo vuelve a pedir); si las
  dominantes salen del OI el texto de la fuente lo dice ("dominantes por OI") y ⚠ la etiqueta lleva OI en vez de V; D3 sigue dejando estela (la clásica
  no): dicho en el catálogo. Impacto medido hasta hoy: nulo (0 AUDIT con zero NaN, 0 con libroDom=OI desde el 17-09).
- **(baja) Versiones y documentación:** este CHANGELOG (con la 4.1.5b y la 4.1.5c abajo), FileVersion 4.1.5.4, comentarios de 35 series / 8 prendidas.
- Pruebas: ver el informe del arreglador (pantalla, clasica §9 G1-G7 y E8-E10, ws_compat, correr_todo completo).

## 4.1.5c (09-10-2026, hecha a mano por el agente principal e INSTALADA, verificada en pantalla) — el grupo de arriba
- Pedido del operador (textual): "quiero que aparezca primero, bien arriba siempre, la lista de todas las opciones para activar/desactivar las
  dominantes y dibujos individuales".
- herramientas/grupo_arriba_415c.py movió 70 casillas de dibujo (S_*, Tres41*, Raya3*, Estela4, Rotulos4, Monto41Rotulos, Cambio41Rotulos,
  Tramos41Rotulos, Cabecera4) al grupo "0. PRENDER / APAGAR (todo lo que se dibuja)" con Order 0..69 y nombres cortos, y corrió +1000 el Order de
  todos los demás [Display] de la clase (MEDIDO en el diálogo: ATAS ordena los grupos por el menor Order de sus casillas y los negativos van al final).
  Sólo [Display]: ninguna propiedad renombrada ni con otro tipo o default (arnés ws_compat 9/9 contra la 4.1.5).
- GammaHoyTresCasillas.cs NUEVO: un reloj de 250 ms (SubscribeToTimer) compara las casillas del grupo por reflexión y, si cambió alguna, pide
  RedrawChart y anota en pythiagex4-pantalla.log "casilla X: a->b detectada" y "primer render con el cambio (N ms)" (medido en vivo: 12-16 ms; mide el
  OnRender, no que algo se vea).
- La DLL instalada salió con FileVersion 4.1.5.0 (igual que la 4.1.5): corregido en la 4.1.5d.

## 4.1.5b (09-10-2026, hecha a mano por el agente principal e INSTALADA, verificada en pantalla) — las dominantes D1-D3 de la clásica
- Pedido del operador: "replicar la clasica su formula ... todo se pueda activar desactivar".
- Serie NUEVA R10_NDX_dom "Clasica NDX" D1-D3 (ClasicaNdx.cs: DominantesClasica, DomClasica, NivelesDom y Guardar con las dos series): GEX por
  volumen del 0DTE de NDX, radio min(2 %, 100 pts), una por lado con empate 20 % (la más cercana), D3 = la siguiente más fuerte, en el centroide de
  ±12 pts, con la misma cadena, base y fut que la Clasica NDX 0Γ. Casilla NUEVA S_R10_NDX_dom, prendida (CatalogoFamilia, SeriesPantalla,
  FamiliaCuatroPantalla); en ArmadoPantalla el rol D1-D3 en la etiqueta y la base en la pestaña.
- Cotejo contra el port Python verificado (laboratorio/calibracion_1009/receta_clasica_dominantes): 4 de 4 minutos; después el arnés clasica_dom
  (23/23: 68/68 minutos del niv al bit, 950/950 AUDIT dentro de su segundo, 12 casos borde).

## 4.1.5 (09-10-2026, compilada, SIN instalar) — la estela 'NDX 0Γ' de la clásica, calculada adentro
- Pedido del operador (textual): "podes analizar como la version clasica hizo esta estela con que formula porque veo que 14:10 y 14:18, 31067
  31068 aprox acertaron mucho mejor que nosotros que teniamos en 31062 aprox la estela dominante mas cercana y copiarla ahora para nuestro 4.0".
- Qué es la estela (medido en `laboratorio/calibracion_1009/receta_clasica`, paridad 910/910 contra los AUDIT capa=NDX de la clásica): el zero gamma
  POR VOLUMEN del 0DTE de NDX con la grilla de la clásica (61 precios en ±3 % de S, el PRIMER cruce de abajo hacia arriba, interpolado), llevado al
  futuro con SU base. Esa base era la "de la rueda" del 08-10 (243,06, medida a las 20:12 UTC del 08-10, la última vez que se le cayó Rithmic: con la
  primaria en Rithmic la clásica NO mide la rueda). La base real del 09-10 rondaba 228-230: los 13-15 pts de "acierto" salen enteros de esa base
  vieja; con la misma base la fórmula da lo mismo que `ZEST_NDX_vol` (mediana −0,14 pt).
- Serie NUEVA `R10_NDX_zero` "Clasica NDX 0Γ" (`_modulos/familia/clasica/ClasicaNdx.cs`), tipo ZEST, rol Z, sin monto, blanca (#ffffff; los puntitos
  de la clásica son #EBEBEB; a CIEDE2000 12,0 del "NQ 0G", la más cercana), casilla NUEVA `S_R10_NDX_zero` PRENDIDA en el grupo NUEVO "4c. Como la
  clasica" (pedido explícito: "copiarla ahora"). Todo adentro: NO lee el log, ni `base-rueda-NQ.json`, ni las cadenas ni las estelas de la clásica.
  - `CruceClasica`: GammaHoyNucleo.Cruce port 1:1 sobre la cadena _NDX que bajó la 4.1 (la última con generado <= t), horizonte Hoy envejecido.
  - La base: la cascada de la clásica (de la rueda si tiene <= 24 h → CRUDA de forwards → TEÓRICA (carry) → cruda sin cota, cada una con la cota del
    carry). "Medida" y "medida reciente" no se usan: las cadenas de CBOE traen base null (910 de 910 AUDIT; todas las cadenas de la 4.1).
  - La base "de la rueda" = `MedirBaseRueda` de la clásica (`ReglaBaseClasica.Paso`, port 1:1: sello 13:50-20:10 UTC, cadena <= 30 min, cierre de la
    vela del gráfico en sello − 960 s menos el spot, últimas 24, mediana robusta, adopta con >= 5 buenas) sobre las cadenas y la cinta de la 4.1. La
    vela es la m2 de la cinta (`OpcionesClasicaNdx.VelaMin` = 2: el 08-10 la clásica corría en un gráfico de 2 min; sus 92 velas son de minuto par) y
    tiene que estar ENTERA en la cinta (sin buscar la anterior: el 08-10 la cinta tiene un hueco de 17:49 a ~18:15 UTC por Rithmic caído).
  - Durante la ventana de la rueda (13:50-20:11 UTC) la base queda CONGELADA en la del día anterior, como la clásica de hoy; fuera de la ventana, la
    regla a esa hora (desde las 20:11 UTC, la de la rueda que terminó).
  - Una cuenta por minuto (al empezar el minuto, con el último tick del MNQ), como las R20; la clásica recalcula cada 5 s y pone hasta 24 puntitos por vela.
- Pantalla: etiqueta "Clasica NDX 0Γ 31.068,42 V" (el nombre ya dice que es el zero: sin repetir "0G"); en un grupo, nombrada al final como las de la
  2.0; rótulo de tramo "Clasica NDX 0Γ 31.068,21"; detalle "(base clasica 243,06 de la rueda del 08-10, congelada; base de ahora 229,33 (sincronizada):
  +13,73 pts) (en el indice NDX 30.825,36)"; fuente "Clasica NDX (replica, CBOE)" con su texto (base usada, de qué rueda, buenas/N, cuándo se adoptó,
  la base sincronizada de ahora y la regla de la clásica con la rueda de hoy); en naranja si la base es de RESPALDO (CRUDA o carry).
- Motor: `ExtrasCompuestos` (`_modulos/familia/fam`): Replica20 y después ClasicaNdx (una que falla no tira a la otra; Rehacer solo a la que cambió).
  `IExtrasParciales` (opcional): un minuto del archivo de la 4.1.4 (con R20, sin R10) se completa en memoria con la R10 sin tocar las R20 ni el archivo.
  Con la Replica20 sola el motor es el de la 4.1.4. `IExtrasRehacer`: si la cinta trae tarde el día anterior (ATAS cargando), la regla se rehace y el
  motor rehace los minutos.
- Arnés NUEVO `atas/_test_cuatro_paridad/clasica` (en `correr_todo.ps1`), 27/27 con los datos reales del 09-10:
  - fórmula: con la MISMA cadena, fut, base y hora que cada AUDIT, el zero igual a 0,05 pt en 910 de 910 (máx 0,0068 pt);
  - base: la regla con las 92 muestras del log de la clásica da sus 92 medianas/buenas/MAD y termina en 243,06 a las 20:12:06 UTC; el spot de la
    4.1 con el mismo sello es el suyo en 92 de 92; el cierre de la vela de 2 min de la cinta de la 4.1, en 77 de 77 con vela (15 sin vela: el hueco);
    la réplica SOLA calcula 243,06 para la rueda del 08-10 (las mismas últimas 24 muestras, 19:39:54-20:09:56 UTC);
  - todo 4.1 por AUDIT (su cadena, su base, el fut y la hora del AUDIT): base igual en 910 de 910; zero igual en 746; los 164 restantes son la
    cadena (la 4.1 tenía otro sello de CBOE en ese instante; con la cadena de la clásica, iguales los 164). Zero 4.1 − clásica: mediana 0,00,
    p10/p90 ±0,01, extremos −17,7/+25,2 a la apertura (13:50-14:05 UTC, el 0DTE cambia mucho de una cadena a la siguiente);
  - por minuto (lo que guarda): contra los AUDIT del mismo minuto mediana 0,00 (p10 −0,13, p90 0,07); adentro del rango de los puntitos de su
    vela en 834 de 909 velas;
  - las velas del operador: 17:10Z "Clasica NDX 0Γ" 31.067,33 (la clásica: puntitos 31.067,33 / 31.068,03 / 31.068,32); 17:18Z 31.068,99 (31.068,32);
  - motor: las 21 series, la meta y las R20/DOMS idénticas con y sin la clásica (1169 minutos); R10 en 1169 de 1169; persistencia idéntica;
    completado de un archivo de la 4.1.4 igual al cálculo en vivo (1169) sin tocar R20 ni el archivo; rehecho con la cinta del 08-10 que llega tarde
    (sin ella: CRUDA 235,51 → 31.059,76; con ella: 243,06 → 31.067,33, 985 minutos rehechos, iguales al arranque con la cinta completa).
- Lo que NO coincide con la clásica (dicho, no escondido):
  - DE NOCHE: la clásica de hoy no midió la rueda: a las 20:12 UTC del 09-10 su base cumple 24 h y pasa a la CRUDA de cada cadena (en la rueda del
    09-10 fue de 223,7 a 243,5). La réplica, desde las 20:11 UTC, usa la base de la rueda del 09-10 (~229,4). Mañana en la rueda la réplica usa la
    del 09-10 congelada; la clásica, la CRUDA (su archivo no se renueva mientras Rithmic no se caiga).
  - El lunes la base del viernes tiene más de 24 h: la réplica (como la clásica) usa la CRUDA durante la rueda del lunes.
  - De día el sello de CBOE puede ser otro (la 4.1 baja cada 75 s; la clásica lee cboe-local): 164 de 910 AUDIT del 09-10, casi siempre <= 0,05 pt,
    hasta 25 pts en la apertura.
- SIN VALIDAR: la "ventaja" del 09-10 son 2 toques mirados después de los hechos y sale de una base vieja; el corrimiento de esa base cambia de un día
  a otro (+0,2 a +14,9 pts del 06-10 al 09-10). Describe; no anticipa.
- Versiones: DLL 4.1.5 (FileVersion 4.1.5.0; AssemblyVersion sigue 4.0.0.0), fam 4.1.5, pantalla 4.1.5, clasica 4.1.5. Pruebas de la pantalla
  actualizadas (A7, A8, A9, B1, B2, B5, T10; A20 nueva): 198/198; e2e `ny-extras` con el compuesto como producción.
- Dos arneses de OTROS módulos que fallaban por la hora o por los datos (no compilan nada de la 4.1.5; verificado):
  - `cboe` (B2), prueba "fotos viejas livianas": corría el reloj +3 días; un VIERNES después de las 09:00 NY eso cae lunes, cuya "rueda hábil
    anterior" es hoy, y las fotos recién bajadas quedaban con filas (a las 05:16 ART pasaba, a las 16:4x no). Ahora +4 días (la rueda hábil
    anterior siempre queda después de hoy y el archivo de hoy dentro de DiasHistoria).
  - `posiciones` (B-pos): copia el `niv-2026-10-09-MNQZ6.jsonl` VIVO, que la 4.1.4 en ATAS siguió escribiendo con la rueda; sus pruebas "de esta
    noche" dieron 5 fallas, 4 de ellas en minutos de la rueda (P2/P10 14:00Z, P4 09:09Z, P7b). Ahora se acota a la noche validada (hasta las 08:16Z;
    `--hasta 2026-10-09T23:59Z` para mirar todo): P2, P4, P7b y P10 en verde. Lo de la rueda (p. ej. P2: 304 de 28.897 niveles con monto NaN desde
    las 14:00Z) queda SIN REVISAR: es de CambiosFamilia (4.1.2), no de esta versión.
  - `posiciones` P9 (tope de 50 ms por llamada) falla con el mercado abierto y ATAS corriendo: mediana 2,3-3,1 ms, picos de 95-120 ms, con la
    escritura del log sola hasta 92 ms. Es carga de la máquina; no se aflojó el criterio.

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
