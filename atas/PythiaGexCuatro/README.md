# PythiaGex 4.1 — Gamma Familia (un solo indicador, todo adentro) — 4.1.5e

Indicador de ATAS para MNQ/NQ. Calcula **adentro** las 29 series de la vista previa (NQ, NDX, QQQ, la familia sumada, zero estándar,
cruces, confluencia, lo que dibujaba la 3.0 por libro y TQQQ ×3 bien convertido) con la misma cuenta que `preview_niveles.py` /
`backtest_familia.py` (paridad exacta minuto a minuto, ver `atas/_test_cuatro_paridad/`). No depende de otro indicador ni de ningún
programa externo.

## De dónde salen los datos
- **Rithmic (dentro de ATAS):** la cinta del MNQ y el libro de opciones de NQ (CME), igual que la 3.0.
- **CBOE (el propio indicador lo baja):** las cadenas de NDX, QQQ y TQQQ, que Rithmic no transmite (Rithmic = solo CME; las opciones de
  acciones/índices son OPRA). Descarga pública con 15 min de atraso, comprimida, de a un ticker, con tope de velocidad (1250 KB/s) y
  cadencia por ticker (75 s en la rueda de NY, 300 s fuera). Un solo descargador por proceso; candado entre procesos.
- **Única condición externa:** internet para CBOE. Sin internet, NQ sigue y la pestaña avisa en naranja qué falta.

## Instalar
1. Compilar: `dotnet build -c Release -o bin/Release` en esta carpeta.
2. Una sola vez: `python -I herramientas/sembrar_4_0.py` (copia días previos ya guardados a `%APPDATA%\ATAS\PythiaGex4` para que la
   base de NDX de noche, la razón de QQQ y TQQQ tengan historia desde el primer minuto; después la 4.1 vive de lo suyo).
3. `herramientas/instalar_4_0.ps1` (copia `PythiaGexCuatro.dll`, reinicia ATAS con 30 s de pausa para que Lucid suelte la sesión).
El tipo es `PythiaGexCuatro.FamiliaCuatro` (el mismo que el visor 4.0.6): un gráfico que tenía el visor carga la 4.1 sola, con sus casillas.

## Lo que dibuja (UI aprobada por el operador)
Rayitas por vela de cada serie prendida; etiquetas chicas pegadas al eje (+N si comparten precio) con la edad del dato arriba
("DATO DE HACE" si pasa de 30 min); pestaña desplegable cerrada (fuentes, conversiones, edades, detalle de cada nivel y avisos);
doble eje elegible Ninguno/NDX/QQQ/TQQQ con la conversión de sus rayas; sin sombreado; control de vencimiento (roll).
Defaults (4.1.5): NQ muros OI, NDX muros vol, 3.0 NDX, TQQQ muros OI, 2.0 QQQ, 2.0 NDX y Clasica NDX 0Γ (QQQ zero est. vol apagada en la 4.1.4). QQQ majors OI, QQQ muros OI/vol,
FAM muros OI y QQQ dom quedaron APAGADAS por defecto a pedido del operador (sin renombrar: un grafico que ya las tenia guardadas conserva lo suyo).
Lo propio de la 3.0 (dominantes, túnel, zero, capas, recuadro, cabecera, fórmulas) queda calculando pero sin dibujar (casillas `Tres41*`).
4.1.3: además las dominantes como la 2.0 (grupo "4b. Dominantes como la 2.0"): "2.0 QQQ" (ámbar oscuro #CC8800 desde la 4.1.4) y "2.0 NDX"
(réplica exacta de la 2.0, con SU conversión; prendidas) y "QQQ dom"/"NDX dom" (la selección de la 2.0 sobre los libros de la 4.1; apagadas). "2.0 QQQ"
replica a la 2.0 corriendo en un gráfico de **1 min**: con la 2.0 en otro marco su "vela del último trade", y su razón, cambian (de noche ~3 pts).
Si su razón sale de un respaldo (no de la vela de la cinta) la pestaña lo dice en naranja. Ver `_modulos/familia/replica20`.
4.1.4: **rótulos de los tramos de historia** (`Tramos41Rotulos`, prendido): la columna de la derecha nombra solo los niveles de AHORA; cada raya
de la estela que ya no es la vigente (15 velas o más, y unos 15 min o más, al mismo precio) lleva en SU final un rótulo chico con su nombre y su
precio ("NDX muro V 31.040,74"), aunque otra raya siga a su lado; varias rayas de la misma altura que terminan juntas (a <= 40 px), uno solo ("NDX muro
V/OI · 2.0 NDX 31.041"); el muro C y el P en el mismo strike son una sola raya. Sin choques, nunca sobre la columna, la pestaña ni la franja de arriba
(MargenSup4), como mucho 14. En un grupo de etiquetas, las series "como la 2.0" que no son la cabeza van nombradas al final ("QQQ P −2,19B 31.076 OI
· QQQ dom D1").

4.1.5: **la estela 'NDX 0Γ' de la clásica** (grupo "4c. Como la clasica", casilla `S_R10_NDX_zero`, PRENDIDA; serie `R10_NDX_zero`, blanca): el zero
gamma por volumen del 0DTE de NDX con la grilla de la clásica (61 precios en ±3 %, el primer cruce de abajo hacia arriba) más SU base: la de la rueda
ANTERIOR (su regla `MedirBaseRueda` sobre las cadenas y la cinta de la 4.1, vela de 2 min), congelada durante la rueda; con más de 24 h, la cruda de
forwards y después el carry. Etiqueta "Clasica NDX 0Γ 31.068,42 V"; la pestaña dice la base usada, de qué rueda es y la de ahora ("base clasica 243,06
de la rueda del 08-10, congelada; base de ahora 229,33: +13,73 pts"). Medido el 09-10: la base de la rueda del 08-10 sale 243,06, igual que la clásica;
la fórmula es la del zero estándar de NDX: lo que la corre 13-15 pts es la base vieja. De noche NO coincide con la clásica (ella pasa a la cruda; la
réplica usa la rueda que terminó). Ver `_modulos/familia/clasica/ClasicaNdx.cs` y el arnés `atas/_test_cuatro_paridad/clasica`. Sin validar.

4.1.5b: **las D1-D3 de la capa NDX de la clásica** (`S_R10_NDX_dom` "Clas NDX D1-D3", prendida; serie `R10_NDX_dom`, menta pálido #b8f8d8 desde la
4.1.5d): su fórmula (radio min(2 %, 100 pts), una por lado con empate 20 %, D3 la siguiente, centroide ±12) con la misma base que el 0Γ; sin cambio ▲▼
(4.1.5d, como las 2.0). 4.1.5c: todas las casillas de dibujo en el grupo de arriba "0. PRENDER / APAGAR (todo lo que se dibuja)" (101 desde la
4.1.5d) con redibujo en 250 ms. **Salvedad medida el 09-10 (4.1.5d):** la réplica copia a la clásica cuando dibuja con la base de la rueda ANTERIOR
(su primaria en Rithmic); si la clásica la pierde (p. ej. al reiniciar ATAS) re-mide su base en la rueda y la réplica deja de coincidir: de 16:48
a 17:11 ART quedó ~13 pts corrida. Las muestras de esa base se guardan (`familia\muestras-clasica-NDX-<día>.jsonl`): la rueda cerrada no cambia al
reiniciar.

4.1.5d (fin de semana, pedido del operador: "quiero que la 4.0 se vuelva a ver, o sea que tenga memoria"): del viernes 17:00 NY al domingo 17:59 NY
la sesion es la del VIERNES (la ultima con datos, leida del archivo; antes abria una de sabado o domingo sin nada) y la pestaña dice primero
"mercado cerrado: se muestra la sesion del <dia> (cerro hace ...)"; el domingo 18:00 NY empieza la del lunes. Medido el 09-10: antes la 2026-10-10
con 0 minutos; ahora la 2026-10-09 con 1378 minutos y 12269 lineas de estela. El modo UTC (paridad) no cambia. Arnes: fam, seccion finde.

4.1.5e: **el grupo de arriba con la jerarquia de llaves** (pedido del operador: "pierdo mucho tiempo ... adivinando para ver si aparecen/desaparecen
las dominantes especificas que quiero"). Arriba lo que no necesita llave (las series de la Familia en el orden de siempre, el doble eje, "Estela▸",
"Etiquetas▸", la pestaña); despues la 3.0: cada LLAVE (nombre que termina en ▸: "3.0 NQ dom▸", "3.0 NQ 0Γ▸", "3.0 tunel▸", "3.0 capas▸",
"3.0 F1-F8▸", "3.0 libro▸", "3.0 cabecera▸") va justo antes de sus dependientes ("↳ NQ D1", "↳ NDX D1", "↳ F1 cruce", ...), y al final lo
comun a varias llaves. La primera frase de cada descripcion dice que necesita ("Necesita la llave '3.0 capas▸' prendida."). Los majors por OI de
NDX y QQQ cuelgan de "3.0 NQ dom▸" (asi esta en el codigo), no de "3.0 capas▸". Solo cambia el dialogo (nombre, orden, descripcion): ninguna
propiedad renombrada ni con otro default. Fuente de verdad: `herramientas/grupo_arriba_415e.py`. El log de casillas (pythiagex4-pantalla.log) dice
al arrancar cuantas casillas prendidas no dibujan por su llave apagada y, en cada cambio, si la llave de esa casilla esta apagada.

## Archivos que escribe (todo en `%APPDATA%\ATAS\PythiaGex4`, logs `%APPDATA%\ATAS\pythiagex4-*.log`)
`cboe\` cadenas propias (14 días), `viva\` libro NQ por minuto, `cinta\` precio por segundo, `estela\` TRES, `familia\` niveles por
minuto (por sesión, contrato y modo), muestras de NDX/QQQ (y, 4.1.5d, las de la base de la rueda de la clásica), cierres de TQQQ.

## Sin validar
Describe dónde están los niveles; no anticipa dirección. En 22 sesiones ninguna regla de dominantes le ganó al azar.
