# PythiaGex 4.1.2 — montos y cambios en las etiquetas (diseño del agente principal, 08-10-2026 ~23:30 ART)

## Pedido del operador (textual)
"quiero que se vean en las etiquetas (podés sacar esos +1 +2 etc) los millones o billones en tiempo real así puedo saber si están
aumentando o sacando posiciones en vivo en algún strike o muro". Formato que pidió: "ndx M+ P 558m 31500 oi, V, 0G ... según corresponda".

## Lo que se puede medir y lo que no (NO NEGOCIABLE: ningún número dice más de lo que mide)
- El monto (NivelActual.GexM) = M USD de cobertura por 1 % del subyacente, calls +, puts − (convención; no está medido quién está largo).
- OI = posiciones abiertas, consolidadas por la OCC UNA vez por noche. Intradía NO cambia en ninguna fuente (medido: NQ salta una vez por noche
  01:24/01:30/01:48 UTC; NDX 06:26-06:36 UTC; QQQ 07:31-09:12 UTC; fuera de esos saltos 0 cambios). El monto por OI que cambia intradía cambia
  SOLO por precio/tiempo/IV (medido: MUROS_QQQ_oi strike 750 pasó de −439 a −499 M entre 22:02 y 02:14 UTC con la cadena congelada).
  => para series por OI el cambio que se muestra es OI(publicación vigente) − OI(publicación anterior) en el mismo (strike, vencimiento, lado),
  valuado con la gamma de AHORA. Eso SÍ es cambio de posiciones (de una noche a otra).
- Volumen = contratos operados hoy. Solo sube; cerrar también suma volumen; no dice si abren o cierran.
  => para series por VOLUMEN el cambio que se muestra es el volumen operado en la ventana (5/15/30 min) en el mismo (strike, vencimiento, lado),
  valuado con la gamma de AHORA (el efecto precio queda afuera). Con la cadena congelada (CBOE de noche) el cambio es 0 exacto.
- El perfil es LINEAL en VolC/VolP/OiC/OiP (comun/Perfil.cs:100-101): ΔGEX = PerfilLado(fotoΔ) con fotoΔ = foto de AHORA con las columnas
  reemplazadas por los deltas (misma IV, mismo S, misma t, mismo GeneradoUtc/Dias). Nada de restar montos entre minutos.

## Contrato (ya escrito por el agente principal en _contratos/Contratos.cs — NO cambiarlo; si hace falta algo, proponerlo en el informe)
NivelActual: CambioVolM[3], CambioVolSegReal[3], CambioVolDesdeUtc[3], CambioVolHastaUtc[3] (índice = CambiosVentanas.Min = {5,15,30}),
CambioOiDiaM, CambioOiDesdeUtc, CambioOiHastaUtc, CambioCobertura[4] (3 ventanas + IndiceOiDia=3), CambioNota, Copia().
FotoFamilia: CambiosEstado (IReadOnlyList<string>), Copia().
Unidades del cambio = las de GexM del mismo nivel (NQ ×0,2 = Mult 20/100; NDX/QQQ ×1; familia = suma escalada; TQQQ = las de TqqqPerfil),
SIN redondear (redondea la pantalla). Signo como GexM.

## Módulos y dueños (cada constructor toca SOLO lo suyo)
### B-pos: _modulos/familia/posiciones/ (carpeta NUEVA) + IntegracionFamilia.cs + PythiaGexCuatro.csproj + atas/_test_cuatro_paridad/posiciones/ (NUEVO) + correr_todo.ps1
- Clase CambiosFamilia (namespace PythiaGexCuatro.Familia): la llama HostFamilia en SU hilo después de _motor.Avanzar; devuelve una FotoFamilia
  anotada (foto.Copia() con Actuales = copias de NivelActual con los campos de cambio y CambiosEstado). Nunca modifica lo del motor.
  PublicarConAviso pasa a usar Copia() (hoy copia campo por campo y perdería los campos nuevos).
- No llamar Minuto/NuevaTanda/Sincronizar/Observar de los minuteros (tienen estado). Elegir la foto "de ahora" de cada libro con la MISMA regla del
  minutero (NQ: última con TsUtc<=t y t−ts<300 s; NDX/QQQ: la de LibroMinuteroNdx/MinuteroQqq — leer el código, incluye vigencia/congelada),
  con t = hora de la clave del último minuto (_motor.Almacen.Ultimo) y S/Conv de su MetaLibro (MetaDe). OJO nombres de CBOE: verificar en
  BajadorCboe qué nombre de libro devuelve NDX (el lector dijo que Fotos("NQ") devuelve NDX porque "NQ" es el nombre de archivo).
- Clave de fila: (K del libro, vencimiento UTC = GeneradoUtc + Dias[V] redondeado a 30 min, lado C/P). Lado presente solo con IV>0 en la foto de ahora.
  Ausente ≠ 0: una clave que falta en la foto de antes queda fuera (y baja la cobertura), nunca se toma como 0. Excluir vencimientos <= t.
- Volumen (3 ventanas): foto de antes = la última con Ts <= Ts_ahora − W (tolerancia: si la más cercana está a más de W + max(120 s, W/2), NaN
  "sin foto de hace W min"). Δvol<0 en una clave = reinicio de volumen (nuevo día de negociación): esa clave queda fuera; si más de la mitad de las
  claves comparadas bajan, NaN "volumen reiniciado". Misma foto o mismas filas (cadena congelada) => 0 exacto con nota "cadena congelada".
- OI del día: detector de regímenes por libro sobre la historia de fotos (incremental, como OiNq._idx): entre fotos consecutivas con filas,
  C = claves presentes con OI>0 en las dos; salto si |C|>=20 y >=20 % de C cambió (NO el 50 % de OiNq: NDX salta 52-59 %). R0 = desde el último
  salto, R1 = el régimen anterior; OI de cada clave = el último valor presente en su régimen. ΔOI = R0 − R1 (solo claves en los dos).
  CambioOiDesdeUtc/HastaUtc = inicio de R1/R0. Sin R1 en memoria (p. ej. lunes en NQ: 30 h) => NaN con nota.
- Valuación: FilasHoy.Armar(fotoΔ, t) y PerfilLado.Armar(fl, S, esFuturo) (funciones puras de comun/Perfil.cs), escala ReglasFam.Escala(libro)/1e6.
  Lado por serie: MUROS rol "muro C" -> GvC/GoC; "muro P" -> GvP/GoP; MAJORS (M+/M-) -> Gv/Go. Strike: NivelActual.Strike (unidad del libro, round 2)
  contra K con tolerancia 0,006.
- Familia (FAM_MUROS_vol/oi): el nivel es un balde de 5 pts en precio de NQ (ReglasFam.Fusion: round(Fut/5) al par ×5, Fut = K+conv o K×razón).
  Cambio = suma de los cambios por libro de los strikes que HOY caen en ese balde, cada uno escalado; NaN si falta algún libro.
- TQQQ (T_DOMS_vol neto, T_MUROS_vol C/P, T_MUROS_oi C/P día, T_DOMS_raz = copia de T_DOMS_vol, T_ZERO_oi nada): con las fotos "TQQQ" de CBOE y
  la MISMA cuenta de _modulos/tqqq/TqqqPerfil.cs (vencimiento más cercano, spot de la foto, T desde el dato). Leerlo y reusar su función si es pura.
- Cache: recalcular solo con minuto nuevo o foto nueva; <50 ms por llamada con datos reales. Log una línea por minuto en
  %APPDATA%\ATAS\pythiagex4-cambios.log (por libro: foto ahora/antes por ventana, claves comparadas, reinicios, congelada, R0/R1, claves OI).
- Arnés NUEVO atas/_test_cuatro_paridad/posiciones/ (csproj propio que compile _contratos, comun, posiciones y lo mínimo; datos reales de
  %APPDATA%\ATAS\PythiaGex4 SOLO LECTURA: viva\viva3-NQ-*.jsonl, cboe\cadena-*.jsonl.gz, familia\niv-*.jsonl). Pruebas mínimas:
  identidad de linealidad (Δ por fotoΔ == Perfil(ahora) − Perfil(ahora con vol de antes) a 1e-9 rel.); congelada => 0 exacto; reinicio => NaN;
  saltos de OI reales detectados (NQ 10-07 ~01:24Z, 10-08 ~01:30Z, 10-09 ~01:48Z; NDX ~06:26-06:36Z; QQQ ~07:31-09:12Z) y CERO saltos falsos;
  un caso a mano (strike 31100 calls NQ: gamma de ahora × Δvol 15 min); balde FAM = suma de libros; tiempo <50 ms. Agregar el paso a correr_todo.ps1.
### B-pant: _modulos/pantalla/*.cs + _modulos/pantalla/prueba/* (+ FamiliaCuatroDoble.cs si existe ahí)
- Etiqueta (pedido del operador): `LIBRO ROL MONTO [CAMBIO] PRECIO FUENTE`, p. ej. `NDX P −558M 31.500 OI`, `NQ C +45M ▲3,1M 31.100 V`,
  `QQQ 0G 31.054 V` (zero sin monto), `3.0 NDX D1 +143M 31.036` (3.0 sin fuente), `TQQQ 81 C +3,4M 31.073 OI`, `CONF 31.000 V`.
  Fuente al final: "V" / "OI" (+"*" si OiViejo). Rol corto como hoy; "cruce arriba/abajo" -> "cruce↑"/"cruce↓".
- Monto(double m) público y probado: NaN/Inf -> ""; |m|<0,05 -> "0M"; <10 -> 1 decimal ("+0,4M", "−2,1M"); <999,5 -> entero ("+102M");
  si no, B: <9,995 B -> 2 decimales ("+2,30B"), <99,95 B -> 1 decimal, si no entero. Signo "+" o "−" (U+2212). Coma decimal es-AR.
- Sin "+N". En un grupo (≤1 pt) la etiqueta es la del ítem con mayor |GexM| (NaN al final; empate: el de precio más alto, como hoy).
  Misma serie con muro C y P en el mismo precio ("C/P"): `QQQ C/P +336M/−499M 31.076 OI` (sin cambio).
- Cambio en la etiqueta (si Cambio41Rotulos): series "vol" -> CambioVolM[índice de la ventana elegida]; series "oi" -> CambioOiDiaM, y NUNCA si
  OiViejo. Se muestra si |Δ| redondeado != 0 con el mismo formato sin signo: "▲3,1M" si el muro CRECE en magnitud (signo de Δ == signo de GexM;
  si GexM es NaN, signo de Δ positivo), "▼…" si se achica. Color del tramo del cambio: ▲ #089981, ▼ #f23645 (tramo aparte, medido con medir()).
  El resto del texto como hoy. Evitar ↑/↓ dentro de la etiqueta (ya significan fuera de pantalla).
- Ajustes nuevos (grupo "6. Pantalla", nombres NUEVOS para que el .ws no los pise): Monto41Rotulos (bool, true, Order 22),
  Cambio41Rotulos (bool, true, Order 24), Cambio41Ventana (enum VentanaCambio41 { M5, M15, M30 }, default M15, Order 26). Copiarlos a
  AjustesPantalla y al Resumen del log. Con Monto41Rotulos=false y Cambio41Rotulos=false la etiqueta vuelve a ser la de hoy SIN "+N".
- Pestaña (abierta): una línea de leyenda corta arriba del detalle: "monto = M USD de cobertura por 1 % (M millones, B mil millones) · ▲▼ en V:
  volumen nuevo en N min (no dice si abren o cierran) · en OI: posiciones vs la publicación anterior · gamma de ahora: el precio no lo mueve".
  Cada nivel del detalle: monto y cambio con su ventana real ("vol 15 min 01:59→02:14 UTC: ▲3,1M") o el OI con sus fechas
  ("OI 10-09 01:48Z vs 10-08 01:30Z: ▲12M"), cobertura si <95 %, y CambioNota si hay. TQQQ: "por 1 % de TQQQ". Las líneas de
  FotoFamilia.CambiosEstado al final de las fuentes. La edad sigue ANTES de los números si pasa de 30 min (protocolo).
- Arnés: actualizar C6/C7/C9/C10/D2/C25 y A7 (lista blanca de propiedades nuevas), doble realista (ZEST/ZTP/CONF/TRES con GexM NaN),
  pruebas nuevas desde C53 (formato, sin +N, cabeza por |monto|, cambio solo vol/oi correcto, OiViejo oculta, colores de tramo, ancho, flechas
  fuera de pantalla con monto, valores enormes/Infinito no tiran, ajustes apagados). Todas en verde.
### B-tres: GammaHoyTres.cs (GuardarEstela y VERSION -> "4.1.2"), _modulos/familia/fam/AlmacenTres.cs, _modulos/familia/fam/SalidaFamilia.cs, arnés fam (atas/_test_cuatro_paridad/fam/)
- Monto de las rayas 3.0 (TRES_NQ/NDX/QQQ): GuardarEstela escribe un campo NUEVO "gm" alineado con "d" (solo D1/D2): GEX NETO con signo del strike
  elegido / 1e6 EN UNIDADES DE LA FAMILIA (NQ ×0,2 porque la 3.0 usa multiplicador 100 y la familia 20; NDX/QQQ ×1), y null cuando la dominante
  no es un strike con monto (túnel/cruces de noche: relleno ±1e6, ver GammaHoyTresTunel.cs). NO tocar "g" ni el resto de la línea.
- AlmacenTres.Parsear lee "gm" alineado con "d" (si d[i] se descarta, gm[i] también); sin "gm" o null -> NaN. Niveles() pone el monto en Nivel.GexM.
  SalidaFamilia.Actuales: TRES con GexM = n.GexM (hoy NaN). Las estelas viejas (sin gm) dan NaN: la paridad e2e con datos históricos no cambia.
- Verificar con datos reales (solo lectura) que gm de NDX/QQQ coincide con MAJORS_*_vol del mismo minuto/strike cuando corresponde, y que los
  lectores Python de la estela (laboratorio/tres/extremos_rebote.py leer_estela) ignoran campos nuevos. Arnés fam en verde + pruebas nuevas de gm.

## Reglas para todos
- Compilar la DLL SOLO con: powershell -ExecutionPolicy Bypass -File "C:/Users/wmx_7/OneDrive/Escritorio/ATAS nada/PythiaGex/herramientas/build_serial.ps1" -Dir "<PythiaGexCuatro>" -Salida "bin/Release"
  (candado global: hay otros constructores). Los arneses tienen su propio csproj/obj: se compilan con dotnet build normal.
- NO instalar, NO copiar DLLs a %APPDATA%\ATAS\Indicators, NO tocar ATAS ni sus procesos, NO escribir en %APPDATA%\ATAS\PythiaGex4 (solo leer;
  copiar a una carpeta de prueba si hace falta). No tocar PythiaGexNiveles, PythiaGexDos, PythiaGexTres, profundidad/motor.py, fuentes.py, servir.py.
- No cambiar Contratos.cs (lo escribió el principal). No cambiar la paridad: minuteros, comun/, ReglasFam, formato de niv-*.jsonl, cboe/.
- Comentarios y textos en español, como el código que rodea. LF o CRLF: respetar el del archivo.
- Informe final: qué cambió (archivo:línea), qué pruebas corriste con su salida textual, qué NO pudiste verificar.
