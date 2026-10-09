# PRE-REGISTRO — campeonato20, familia sumada + confluencias + combinaciones + 8 nuevas (09-10-2026, ~06:05 UTC)

Escrito ANTES de juzgar ninguna serie con juez20 (no se miro ningun resultado de esta vara para estas series). Se congela con
sha256 en `PREREGISTRO_familia.sha256`. Lo que se cambie despues se anota al final como DESVIO, con hora y motivo.

## Vara
`../juez20/juez20.py` sin cambios (giro 20, ventana 120 min, TOLERANTE; ACIERTO20 = no se rompe antes de 20 pts a favor medidos
desde la raya). Opciones: `{'desfase_min': 0}` (la clave t de mis series ya es lo vigente en la vela t: precio = cierre de la vela
que abrio en t-1, foto con generado <= t; igual que la 4.1). Placebo default (corridas +-11/+-19/+-31 y azar 'pool'), bootstrap
2000, semilla 20261009. n_azar = 500 en MIRAR y 2000 en PRUEBA (para que el p por permutacion pueda bajar a 0,0005 y Holm tenga
lugar). p para Holm = `placebo['azar']['pct_acierto20']['p_valor']`; se informa tambien `p_normal` (aproximacion).

## Cuenta comun de los libros (port de la 4.1 / backtest_familia, igual que probador2/grupo2.py, que dio paridad 100 % con el niv
de la 4.1 del 10-09 en FAM_MUROS_oi)
- Grilla: cada minuto t de la sesion d (`arnes/evaluar.minutos_y_precio`); F(t) = cierre de la vela de 1 min que abrio en t-1.
- Libros NQ (Rithmic), NDX y QQQ (CBOE) con la foto vigente y la conversion 'cuatro' minuto a minuto de `datos/conv_min`
  (= `cargar.serie_conversion`, la de la 4.1). TQQQ NO entra (la conversion de la 4.1 para TQQQ no esta armada en el dataset).
- Horizonte Hoy con envejecimiento; gamma Black-Scholes r 0,0375 (CBOE) y Black-76 (NQ); GEX por lado por strike = Gamma x w x M x
  S^2 x 0,01 (M: NQ 20, NDX/QQQ 100; calls +, puts -); w = volumen (CBOE vol_call/vol_put; NQ `vol_hoy` = el 'vol' del viva, el de
  FotoNq.cs) u OI. Strikes del libro: |Fut - F| <= 3 % F. R = min(2 % F, 100).
- OI de NQ valido segun C2 (`grupo2.oi_nq_viejo_hasta`): sin rayas por OI de NQ antes del salto de OI de la noche.
- Cada minuto de la grilla con F valido es una clave (vacia si la serie no tiene nivel): el juez no arrastra rayas sobre minutos en
  los que la serie no dibujaba nada.

## Series (mi familia, 3)
- F1 `FAM_MUROS_vol`: minutos con los tres libros; perfil sumado en grilla de 5 pts (np.round, empate a par), NQ x20, NDX/QQQ x100;
  D1 = argmax C > 0, D2 = argmin P < 0 en R (primera ocurrencia). = ReglasFam.cs.
- F2 `FAM_MUROS_oi`: igual con OI; ademas OI valido en los tres (NQ por C2).
- F3 `CONF_vol`: pozo por libro presente = MUROS D1/D2 + MAJORS D1/D2 + ZTP D1/D2 (cruces sin islas C3, a <= 100) por volumen;
  items ordenados por precio; grupo = los que estan a <= 3 pts del primero; si tiene >= 2 libros distintos, raya = promedio y se
  dibuja si |p - F| <= 100. = ReglasFam.Confluencia.

## Combinaciones pre-registradas (6 + 1 control)
- K1 `CONF_NDX_NQ_vol`: F3 con los pozos SOLO de NDX y NQ (hacen falta los dos libros): niveles de NDX y NQ a <= 3 pts.
- K2 `CONF_todo`: confluencia de cualquier 2 instrumentos (NQ, NDX, QQQ) con los pozos de volumen Y de OI juntos (OI solo si vale).
- K3a `FAM_MUROS_vol_TOP`: nivel de F1 solo si su valor de lado (C para D1, |P| para D2) es el MAXIMO de ese lado en todo el libro
  sumado (todos los baldes de 5 pts a <= 3 % de F): "el muro cercano es tambien el muro mas grande del libro" (top 1 por |GexM|).
- K3b `FAM_MUROS_oi_TOP`: igual sobre F2.
- K4a `FAM_MUROS_oi_DOI`: nivel de F2 solo si el cambio de OI del lado en ese balde es > 0:
  dG = suma sobre los tres libros y los strikes cuyo Fut redondeado cae en el balde (a <= 3 % de F) de suma_vencs_Hoy Gamma_lado x
  dOI_lado x M x S^2 x 0,01, con la gamma de AHORA (como CambiosFamilia.cs). dOI = OI(publicacion vigente) - OI(publicacion
  anterior) en el mismo (strike, vencimiento, lado); clave ausente = 0. Publicacion = regimen de OI: cambia cuando entre dos fotos
  consecutivas hay >= 20 claves comunes y >= 20 % cambiaron (MinClavesSalto/FraccionSalto de la 4.1). La anterior se busca en la
  sesion previa disponible del dataset. Sin publicacion anterior conocida en alguno de los tres: sin nivel. A diferencia de la 4.1,
  NO se exige que las tres publicaciones sean de la misma noche (con esa regla FAM no tendria cambio de OI entre el salto de NQ
  ~01:30Z y el de CBOE ~06:30-09:00Z).
- K4b `MUROS_oi_DOI`: union de MUROS_NQ_oi, MUROS_NDX_oi y MUROS_QQQ_oi (NQ solo con OI valido), cada nivel solo si el dG de su
  libro, su strike y su lado es > 0 (la idea del muro de puts 31100 de NQ del 09-10).
- K0 `MUROS_oi_UNION` (CONTROL de K4b, no candidata): la misma union sin el filtro de dOI.

## Las 8 nuevas de la investigacion (codigo sin cambios, solo generado para TODAS las sesiones con su libro)
C04_DOS_QQQ_DERIVA y C06_REGIMEN_POS_QQQ (`probador1/candidatas.sesion`, escribir=False), C13_IMAN_QQQ_vol, C14_REPEL_QQQ_vol,
C16_CONFLUENCIA_QQQ_NDX, C17_BANDA_EM, C18_CW_PW_OI_QQQ (`probador3/g3_niveles.sesion_qqq_ndx`) y C15_FLUJO_NQ
(`probador3/g3_niveles.sesion_nq`). Sus filas se pasan a claves por minuto igual que mis series (grilla completa, vacia sin nivel).
C17 es de dia por construccion: solo se juzga en la rueda.

## Ventanas, noches y particion
- Noche: [d - 2 h, d + 13,5 h) = 22:00Z -> 13:30Z. Rueda: [d + 13,5 h, d + 20 h) = 13:30Z -> 20:00Z. Se juzgan por separado.
- Noches (o ruedas) de una serie = sesiones con al menos una clave con nivel dentro de la ventana y >= 60 velas en la ventana.
- Particion POR SERIE Y VENTANA con `juez20.particion_noches`: MIRAR = los 2/3 primeros en orden de fecha, PRUEBA = el ultimo
  ceil(n/3). En PRUEBA no se elige nada: se informa todo lo pre-registrado.
- Velas: `cargar.velas(d)` (serie principal = contrato frente: U6 hasta la sesion 09-14, Z6 desde la 09-15), [d-2h, d+21h). La
  sesion 2026-10-09 se extiende con `juez20/velas_hasta_ahora.construir()` (MNQZ6) despues de la ultima vela del dataset, solo para
  seguir las llegadas; los niveles de esa sesion terminan donde termina el dataset (~03:53Z).

## Que se considera "sobrevive" (para MI conclusion; la seleccion final y Holm del campeonato son del orquestador)
En PRUEBA: pct_acierto20 con p_valor (2000 juegos) < 0,05 Y por encima de la tasa base de las corridas, Y el limite inferior del IC90
por noches por encima de la tasa base del azar, Y sin empeorar las falsas por noche contra el azar (percentil de falsas_por_noche
<= 50). Holm dentro de mis 35 pruebas (18 series x noche/rueda, C17 solo rueda) se informa como referencia.
Descriptivo, fuera de toda decision: la sesion 2026-10-09 con los niveles REALES de la 4.1 (familia/niv-2026-10-09-MNQZ6.jsonl) para
F1/F2/F3 y la paridad de mi reconstruccion contra ese niv.

## DESVIOS (se anotan aca si los hay)
- DESVIO 1 (2026-10-09 06:16 UTC, ANTES de juzgar ninguna serie; no se miro ningun resultado de la vara): en K4a/K4b el
  regimen de OI y el dOI pasan a ser un port EXACTO de RegimenesOi.cs + CambiosFamilia.CompararOi de la 4.1 (que el pre-registro ya
  citaba): claves = lados con IV > 0; comunes = OI > 0 en las DOS fotos; salto normal (>= 20 comunes y >= 20 % cambiadas), de libro
  flaco (8-19 comunes y >= 80 %) o DUDOSO (otro par con >= 50 % cambiadas: mientras sea el vigente, sin dOI); OI de cada regimen = el
  ultimo valor visto en el; clave ausente en la publicacion anterior = no se compara (dOI 0, no "OI entero"); CBOE: desde las 09:30 NY
  la publicacion vigente tiene que ser de ese dia habil (si no, sin dOI). Motivo: la version simplificada que habia escrito (comunes =
  OI > 0 en alguna, sin dudosos) partio la sesion 2026-09-18 en 12 "publicaciones" de NQ (fotos alternadas de dos fuentes con OI
  distinto, la vispera de la trimestral) y daba dOI basura esos minutos. Las demas series no cambian.
