---
name: pythiagex-4-0-familia
description: PythiaGex 4.1.1 (08-10) = UN indicador autocontenido (familia NQ/NDX/QQQ/TQQQ, CBOE propio) en vivo en MNQZ6 1m; 4.1.1 arreglo la capa 3.0 NDX corrida 58 pts de noche; instalar_4_0.ps1 con pausa de 30 s
metadata:
  node_type: memory
  type: project
  originSessionId: f5879819-e2a4-405b-87b0-959ace090d7b
  modified: 2026-10-08T21:54:42.960Z
---

Construida el 08-10-2026 a pedido del operador ("construi o actualiza la 3.0 o 4.0 como te quede mejor"). `atas/PythiaGexCuatro`
(PythiaGexCuatro.dll, README ahi). NO calcula: dibuja lo mismo que la pagina preview.html leyendo los json de preview_niveles.py y
tqqq_vivo.py (que tienen que estar corriendo; tqqq_vivo arrancado con pythonw, sin autoarranque). La 3.0 sigue siendo la fuente del libro NQ.
Va en un grafico NUEVO "MNQZ6 5m Chart" del panel derecho (plantilla por defecto: le saque el Depth Of Market).

Lo que el operador pidio y quedo: defaults = sus 8 casillas; sin sombreado; rotulos largos apilados = "cosa horrible" -> etiquetas chicas
pegadas al eje + pestaña desplegable (arranca cerrada); doble eje elegible NDX/QQQ/TQQQ con la misma conversion que las rayas
("no quiero errores de extrapolacion"). Revision adversarial aplicada en 4.0.2 (edad por fuente, OI 2s, roll, hora por ticks: Kind=Unspecified
confirmado en el log).

**Trampas de esta sesion:** (1) relanzar ATAS al toque tras cerrarlo dejo el login de ordenes de Lucid colgado (18:16) y dos procesos
OFT.Platform a la vez dieron "SessionTakenOver": instalar_4_0.ps1 espera 30 s antes de relanzar. (2) Invocar dos veces el boton
"Indicators" por UIA rompe el dialogo ("Cannot set Owner…", "nested BeginInit"): UNA sola invocacion y esperar. (3) El titulo anclado a
area.Bottom cae detras del eje de tiempo (ver [[atas-chartarea-mas-alto]]). (4) Los clics del lado derecho de ATAS no llegan: UIA.

Ver [[pythiagex-3-0-estado]], [[tqqq-referencia]], [[atas-dialogo-indicadores-clics]], [[reconectar-atas-uia]].

**4.1 (08-10 noche, reemplaza al visor):** el visor 4.0.6 dependia de la 3.0 y se cayo cuando el operador cerro su pestaña. La 4.1 es UN
indicador autocontenido en atas/PythiaGexCuatro: clon de la 3.0 (herramientas/clonar_4_0.py; NO re-correr: B1 parcheo encima) + descarga
propia de CBOE (_modulos/cboe, gzip, de a uno, Mutex Local\PythiaGex4.BajadorCboe) + minuteros NQ/NDX/QQQ + motor (fam) + TQQQ + pantalla,
unidos en IntegracionFamilia.cs (HostFamilia por proceso y contrato, hilo "PythiaGex4 familia"). Paridad exacta modulo por modulo contra
atas/_test_cuatro_paridad/referencia/ref-2026-10-0{7,8}.json (generar_referencia.py). Siembra unica al instalar: herramientas/sembrar_4_0.py
(copia historia de cboe-local, viva3, cinta y estela de la 3.0 a PythiaGex4). Rithmic NO trae opciones de NDX/QQQ/TQQQ (catalogo local:
solo CME; IB pide OPRA pago). La clase principal es PythiaGexCuatro.FamiliaCuatro (mismo nombre que el visor: conserva sus ajustes S_*).
- Trampa de orquestacion (08-10 21:3x): contestarle con SendMessage a un agente que corre DENTRO de un workflow lo "resume" como una
  SEGUNDA copia que edita los mismos archivos en paralelo. A los agentes de un workflow no se les escribe: se espera su resultado.
- **Instalada y en vivo 08-10 22:04** en "MNQZ6 1m Chart" (panel derecho del espacio "MNQ liviano"), agregada por el dialogo Indicators
  (Custom, buscar "Gamma Familia", UIA "Add to chart" y "Apply" una vez cada uno). Al arrancar: libro NQ 320 suscriptos 0 rechazados,
  CBOE NDX/QQQ/TQQQ bajados, motor calculo la sesion desde las 18:00 NY en segundos. TQQQ de noche = SIN DATO a proposito (c necesita
  spot vivo; ancla nocturna apagada por default).
- **4.1.1 (22:12): la capa 3.0 de NDX/QQQ (TRES_*) se corria de noche tras reiniciar ATAS.** Sin mediana propia (base-rueda-NQ.json /
  razon-rueda-QQQ.json en PythiaGex4: la siembra NO los copia) usaba la muestra alineada = MNQ(hora) - NDX congelado a las 16:00:
  base 299.44 y despues 287.44 (copiaba el movimiento del futuro) contra 241.03 familia / 241.83 forwards; "3.0 NDX" 58 pts arriba.
  Arreglo en Mapear: spot quieto + sin mediana -> conversion de la familia (FamiliaFoto.Fuentes); la alineada dudosa no veta forwards;
  sin nada confiable no dibuja. Verificado en vivo: NDX 241.83 forwards, QQQ 41.4352 familia; "3.0 NDX D2" sobre "NDX P v" (strike 30700).
  Antes de decir "la raya 3.0 esta bien" de noche: leer "capa NDX: ... base X (origen)" en pythiagex4-gammahoy.log.
- **4.1.2 (instalada 09-10 01:18):** montos M/B en etiquetas, sin "+N", flechas de cambio (vol 5/15/30 min con gamma de ahora; OI
  publicación vigente vs anterior), ↑/↓ con renglón propio. Auditado en pantalla contra la cadena cruda (put 31.100 NQ OI 56→210).
- **4.1.3 (instalada 09-10 02:35):** copia de la 2.0 adentro (`_modulos/familia/replica20`): R20_QQQ_vol "2.0 QQQ", R20_NDX_vol
  "2.0 NDX", DOMS_QQQ_vol "QQQ dom" (selección 2.0 = 2 de mayor |GEX vol| a ±min(2 %,100) pts, con razón C8) PRENDIDAS; DOMS_NDX_vol
  apagada. Paridad contra el AUDIT de la 2.0: 135/138 QQQ, 132/132 NDX; en vivo 05:37Z idéntica (31.249,28 843 M / 31.083,50 437 M).
  Razón 2.0 = MNQ de la vela 1 min del último trade (16:14 NY) / spot QQQ actual: en 16 noches erra mediana 24 pts (máx 193) contra
  4,4 de la C8; el operador la quiere igual (le acertó el 08-10). Las extras van en RegistroMinuto.Extra (CONF/FAM no las ven).
- **4.1.4 (instalada 09-10 05:16):** rótulos al final de cada tramo de historia (Tramos41Rotulos; tope 14, juntan series a ≤1,5 pt; la
  triple raya 'NDX muro V/OI · NDX major V · 2.0 NDX 31.040'), defaults apagados (QQQ muros/majors OI, FAM OI, QQQ dom, QQQ 0G; solo
  la casilla), color 2.0 QQQ #CC8800, réplica con precio del instante y razón recalculada si faltaba la cinta. Su .ws: 9 prendidas.
- **4.1.5 (instalada 09-10 16:33):** R10_NDX_zero "Clasica NDX 0Γ" (_modulos/familia/clasica/ClasicaNdx.cs): zero de NDX por volumen
  como la clasica (grilla de 61 precios ±3 %, primer cruce) + SU base "de la rueda" replicada adentro (regla MedirBaseRueda sobre
  cadenas y cinta de la 4.1, congelada durante la rueda: hoy 243,06 del 08-10). En vivo 16:34: 31.086,72 contra 31.086,69 del AUDIT
  de la clasica (misma cadena). Desde las 20:11 UTC usa la rueda del dia (229,4); la clasica a las 24 h pasa a la CRUDA: de noche difieren.
- **4.1.5b (instalada 09-10 16:51, la hice yo directo por apuro del operador):** R10_NDX_dom "Clasica NDX" D1-D3 (DominantesClasica en
  ClasicaNdx.cs: radio min(2 %,100), una por lado con empate 20 %, D3 relleno, centroide ±12) con la misma base; casilla S_R10_NDX_dom
  prendida, grupo "4c. Como la clasica". Cotejo contra el port Python verificado (laboratorio/.../extraer_dominantes_clasica.py,
  scratchpad cotejo_dom.py): 4 de 4 minutos iguales al centavo y al M. En el .ws: Tres41Dominantes=true (NQ D1/D2 de la 3.0 interna),
  Regla3Dominantes=clasica, Rayas3Independientes=false (centroide como la clasica: D1 31.122,5 en vez de 31.120). Scripts .ws en
  herramientas/ws_prender_tres41_dominantes_0910.py, ws_regla3_clasica_0910.py, ws_rayas3_centroide_0910.py (respaldos en respaldos_ws).
- **Trampa 09-10:** cada reinicio de ATAS le tira a la CLASICA su cadena de Rithmic unos minutos ("solo 15 de 320 con las dos puntas"):
  cae al libro de CBOE NDX y, con primaria de indice, MIDE la base de la rueda: tras los reinicios de las 16:4x paso de 243,06 a 229,88 y su
  "NDX D1" bajo de 31.141 a 31.128. Avisarle al operador antes de comparar contra la clasica despues de un reinicio.
- **Casillas (prueba 09-10 17:04, mercado abierto):** destildar/tildar en el dialogo de ajustes aplica EN VIVO sin Apply y se ve en < 1 s
  (Clasica NDX D1-D3 y 3.0 dominantes). El buscador del panel de ajustes filtra por nombre. La columna de nombres es angosta (~12
  caracteres visibles): nombres cortos con lo que distingue al principio. Falta medir sin ticks (pausa 18-19 ART). 4.1.5c (workflow
  whfr801er): grupo de arriba con todas las casillas de dibujo + redibujo inmediato + latencia en el log.
- **4.1.5c/4.1.5d (instalada 09-10 19:16, verificada: correr_todo 16/16, ws_compat sin propiedades cambiadas):** grupo de arriba
  "0. PRENDER / APAGAR" (101 entradas), reloj de 250 ms que redibuja y loguea la latencia (pythiagex4-pantalla.log 'casilla X ...
  detectada' / 'primer render (N ms)'; medido 12-22 ms con ticks, hasta 1,5 s en la pausa); muestras de la base de la clasica
  guardadas por dia (muestras-clasica-NDX-<dia>.jsonl). **Fin de semana (4.1.5d):** SesionFamilia.De daba una sesion de SABADO vacia
  y tras reiniciar ATAS la 4.1 no dibujaba nada ("no tiene memoria"); ahora sabado/domingo usan la sesion del VIERNES (archivo) con
  aviso "mercado cerrado: se muestra la sesion del ...". El domingo 18:00 NY empieza la del lunes.
- **Pedido del operador (09-10 19:2x): NO mas copias de la clasica** ("eso ya lo tenemos y carga cuando yo quiera"): el port 4.1.6 de
  las dominantes NQ/R1/R/pesadas (laboratorio/calibracion_1009/port_dominantes_clasica, paridad 927/927) quedo SIN integrar a proposito.
  Medido con la vara de 20 pts (15-18 sesiones): ninguna dominante de la clasica le gana al azar; NDX D1 de dia peor que el azar.
- **Trampa:** las casillas de la 3.0 (Raya3*, capas, formulas, recuadro, apoyo) no dibujan sin su llave Tres41*; 4.1.5e (workflow
  wkb0f7pma) las pone debajo de su llave con "↳".
