# PythiaGex 4.1 — Gamma Familia (un solo indicador, todo adentro)

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
Defaults (4.1.4): NQ muros OI, NDX muros vol, QQQ zero est. vol, 3.0 NDX, TQQQ muros OI, 2.0 QQQ y 2.0 NDX. QQQ majors OI, QQQ muros OI/vol,
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

## Archivos que escribe (todo en `%APPDATA%\ATAS\PythiaGex4`, logs `%APPDATA%\ATAS\pythiagex4-*.log`)
`cboe\` cadenas propias (14 días), `viva\` libro NQ por minuto, `cinta\` precio por segundo, `estela\` TRES, `familia\` niveles por
minuto (por sesión, contrato y modo), muestras de NDX/QQQ, cierres de TQQQ.

## Sin validar
Describe dónde están los niveles; no anticipa dirección. En 22 sesiones ninguna regla de dominantes le ganó al azar.
