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
