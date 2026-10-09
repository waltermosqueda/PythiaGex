# Pauta 1.0 (réplica de "la pauta") — EN PRUEBA, no validado

Indicador de ATAS que pone en el gráfico del operador **lo que usa el canal de reels que trajo el 24-09** ("la pauta"
en el repo; nunca el nombre real), con la misma pinta: las tres medias, los niveles "marcados" que la formalización P1
calcula sola a las 09:15 de Nueva York con su etiqueta de origen, la ventana de la apertura sombreada, y la marca del
disparo (P1 y, cuando es distinta, P2) con el stop y el objetivo de la regla como líneas finas. **No opera, no toca
órdenes**: dibuja y anota en un CSV cada disparo, para poder medir lo que pase de acá en adelante con las mismas reglas.

## Lo primero: lo medido (24-09, pre-registro `laboratorio/pauta/P_00_PREREGISTRO.md`, criterio fijado antes de correr)

| regla | instrumento | operaciones | % ganadoras (necesario) | neto por operación | t por sesión | controles de azar |
|---|---|---|---|---|---|---|
| P1 marcaciones | MNQ, 36 sesiones de 1 s | 42 | 9,5 % (15,7 %) | −3,09 pts | −1,38 | percentiles 64 / 27 / 28 |
| P1 marcaciones | MES, 31 sesiones | 41 | 7,3 % (17,2 %) | −1,23 pts | −2,40 | percentiles 65 / 84 / 36 |
| P2 extremos 08:15–09:15 | MNQ | 34 | 8,8 % (17,2 %) | −4,18 pts | −1,71 | percentiles 9 / 24 / 26 |
| P2 extremos 08:15–09:15 | MES | 27 | 7,4 % (21,0 %) | −1,70 pts | −2,65 | percentiles 44 / 31 / 33 |

Costo 0,948 pts la vuelta en MNQ (medido, ronda 8) y 0,50 en MES (supuesto; medido 0,453). **0 de 7 condiciones en los
dos instrumentos**: pierde más que el costo y no se distingue de entrar al azar en la apertura con el mismo lado. En
criollo: con objetivo 4 veces el stop y el stop llevado a cero al 40 %, hay que acertar 1 de cada 6; el latigazo de las
09:30 llena el nivel y sigue de largo casi siempre (25 stops, 13 ceros, 3–4 objetivos). Detalle: `laboratorio/pauta/
resultados/P1_veredicto.txt`, `P2_principal.txt`, y las auditorías independientes `p_esc_P1.md` … `p_esc_P4.md`.

Además, ninguna formalización reproduce las operaciones fechadas del canal (v1 04-09, v4 14-09, v6 18-09, v7 21-09):
la unión de niveles estructurales elige otro nivel y otro lado. Nada de esto afirma nada sobre los resultados reales
del canal: son 9 reels de operaciones elegidas y editadas, sin precio, sin fecha y sin cuenta en pantalla. Por eso el
rótulo dice **EN PRUEBA: no validado** con estos números, y no hay flechas de dirección.

## Qué dibuja

- **Las tres medias**, con los colores del canal: la lenta **roja gruesa** (EMA 200, default) y las rápidas **verde**
  (SMA 20, la más rápida) y **naranja** (SMA 26; v7 contra la cinta de MNQ: verde ≈ SMA 20–21, naranja ≈ SMA 26). Períodos editables (estimados de los videos; el canal nunca los nombra). Sobre las velas
  del gráfico, cualquiera sea el marco. En el canal no gatillan nada; se ven como objetivo y como nivel.
- **Los niveles de P1** a las 09:15 NY, con etiqueta corta a la derecha y el precio: `max noche` / `min noche` (N1:
  la sesión electrónica desde las 18:00), `max ayer` / `min ayer` / `cierre ayer` (N2: la rueda 09:30–16:00 anterior),
  `max 8:15-9:15` / `min 8:15-9:15` (N4), `recta pivotes altos` / `recta pivotes bajos` (N3: por los dos últimos pivotes
  de 5 min, dibujadas desde sus pivotes) y `EMA 200 de 1 min` (N5, se reposiciona por minuto: escalones). Los que
  quedan **dentro de D** del precio de las 09:15 (P0) y reciben limitada van **sólidos y con el color del lado**
  (celeste compra / violeta venta, como sus flechas); los de afuera, punteados y tenues. La banda P0 ± D queda apenas
  sombreada y P0 punteado.
- **La ventana**: 09:15–09:45 NY sombreada (órdenes activas) y más tenue hasta las 10:30 (cierre por tiempo). Hora de
  Nueva York, independiente de la zona de la PC.
- **La marca del disparo**: un **círculo lleno** en el precio y la vela de la entrada (sin punta de flecha), el stop en
  rojo y el objetivo en verde como líneas finas hasta la salida, `BE` punteado desde que el stop pasa a la entrada, y en
  la salida un cuadradito con el resultado bruto y el motivo (`stop`, `objetivo`, `be`, `tiempo`…). P2 se marca con un
  **círculo hueco** y líneas punteadas **solo cuando su entrada es distinta de la de P1**.
- **El rótulo** (no se puede confundir con una señal): `PAUTA 1.0 - EN PRUEBA: no validado`, los números medidos en dos
  renglones, el marco y la escala en uso, la sesión en curso (P0, las limitadas armadas con su familia y precio, N4, las
  notas) y la anterior, y cada disparo con su resultado.

En un gráfico de **1 minuto** la marca aparece al cierre del minuto en que se llenó la limitada (el indicador trabaja
solo con velas cerradas, como la medición). En un gráfico de segundos aparece al segundo siguiente. **En 1 minuto la
ejecución dentro del minuto se resuelve como en las 24 sesiones de MES en 1 min** (lo medido en MNQ fue con velas de
1 s): los niveles son los mismos, la salida puede diferir; el rótulo lo dice. En un marco **mayor
a 1 minuto** los niveles de 1 min (N4, EMA 200, P0) salen de esas velas y **no son los medidos**: el rótulo lo avisa.

## La regla que replica (defaults = lo pre-registrado)

A las 09:15:00 NY se congelan los niveles con datos anteriores. Limitadas de compra en cada nivel por debajo de P0 y de
venta por encima, dentro de D; los fijos con el mismo precio se funden en una sola. N3 y N5 se reposicionan al cierre de
cada minuto. Activas de 09:15 a 09:45; la primera que se llena gana (OCO); llenado conservador (la vela tiene que
atravesarla un tick). Stop fijo, objetivo limitado (también atravesado un tick), stop a la entrada exacta cuando el
flotante llega al 40 % del objetivo; si en la misma barra se tocan stop y objetivo gana el stop. Un reingreso solo si la
primera salió exactamente en cero. Cierre por tiempo a las 10:30. P2 es lo mismo con dos limitadas fijas (máximo y
mínimo de 08:15–09:15) sin filtro D y con sus reglas de barra propias (en la vela de entrada solo el stop).

| grupo | ajuste (nombre interno) | viene | nota |
|---|---|---|---|
| 1. Medias | EMA lenta / SMA rápida 1 / SMA rápida 2 (`PtaEmaLenta`, `PtaSmaRapida1`, `PtaSmaRapida2`) | 200 / 20 / 26 | solo se dibujan |
| 2. Regla | Escala (`PtaEscala`) | Automática | NQ/MNQ: D 60, stop 10, objetivo 40, tope P2 50; ES/MES: 15 / 2,5 / 10 / 20 |
| 2. Regla | D, stop, objetivo (`PtaD`, `PtaStop`, `PtaObjetivo`) | 0 = según escala | escala ×4 entre MES y MNQ, como el pre-registro |
| 2. Regla | Stop a la entrada al % (`PtaBePorcentaje`) | 40 | 0 = nunca (V2); 80 (V3) |
| 2. Regla | Reingreso / llenado por toque / calcular P2 | sí / no / sí | toque = variante V1, no es lo medido |
| 2. Regla | Ventana NY (`PtaHoraCongela` … ) | 09:15 / 09:45 / 10:30 | cambiarlo ya no es lo medido |
| 3–4 | qué se dibuja, colores, letra, rótulo | | |
| 5. Aviso | alerta sonora / registrar CSV / volcar auditoría | sí / sí / sí | ver abajo |

Todos los ajustes llevan prefijo `Pta` en el nombre interno (ATAS persiste por nombre en el workspace: si algún día hay
que pisar un valor guardado, se renombra la propiedad).

## Registro y auditoría

- **En vivo** (`Registrar en vivo`): `%APPDATA%\ATAS\PythiaGex2\pauta\pauta-<sesión>.csv`, una fila por nivel
  congelado, por entrada, por BE armado y por salida (`utc, sesion, instrumento, marco, paso_s, version, regla, evento,
  n_op, lado, familia, tipo, precio, nivel, stop, objetivo, motivo, bruto, be_armado, entrada_por, P0, D, stop_pts,
  objetivo_pts, be_porc, notas`). Solo lo que cierra después de cargada la historia: es la materia prima de
  `laboratorio/pauta/p_medir_adelante.py`, que cuenta con las mismas reglas del pre-registro (una sesión un voto, costo,
  t, mitades, sin las 3 mejores) y, con `--recalcular`, vuelve a correr P1 sobre la cinta y compara operación por
  operación. **La prueba hacia adelante NO está habilitada** (P1 no pasó): el script mide, no habilita.
- **Al cargar** (`Volcar la historia`): `auditoria-<instrumento>-<marco>-{sesiones,ordenes,operaciones}.csv` y
  `-ajustes.txt` con todo lo calculado sobre la historia del gráfico, para compararlo con el laboratorio.
- Log propio: `%APPDATA%\ATAS\pythiagex2-pauta.log`.

## Los archivos

- `PautaNucleo.cs` — el núcleo, código puro (sin ATAS, sin dibujo, sin archivos): sesiones de CME, agregados de 1 y 5
  min causales, N1–N5, motor P1 (port literal de `p_P1_lib.sim_pos` + `correr_p1`) y motor P2 (port de
  `p_P2_lib.salir` + `buscar_llenado`). Reproduce el relleno de segundos vacíos de la grilla del Python (barras de
  relleno que no entran en niveles ni agregados) y la convención bid/ask = apertura ∓ medio tick cuando la vela no
  trae punta (velas de 1 min).
- `Pauta.cs` — la capa de ATAS: velas cerradas de a una (`OnCalculate` incremental, nada pesado por vela: el único
  trabajo no constante es la pasada por ~180 velas de 5 min una vez por sesión a las 09:15), medias de la vista, dibujo,
  rótulo, alerta, CSV. Sin suscripciones nuevas (ni profundidad, ni cinta).
- `arnes/` — consola sin ATAS que compila **el mismo** `PautaNucleo.cs` sobre las velas exportadas por el laboratorio.

## Paridad (obligatoria; `laboratorio/pauta/p_paridad_pauta.py`, salida en `resultados/paridad_pauta.txt`)

Sobre **las mismas filas** que usó la medición (`p_P1_lib.cargar_sesiones`), el C# contra el Python:

| instrumento | sesiones | órdenes a las 09:15 | operaciones P1 | operaciones P2 | diferencias |
|---|---|---|---|---|---|
| MNQ (1 s) | 36 | 139 | 42 | 34 | 0 / 0 / 0 |
| MES (24 de 1 min + 7 de 1 s, con el feriado y el roll adentro) | 32 | 171 | 41 | 27 | 0 / 0 / 0 |

Comparado: P0, notas, precio + lado + familia de cada orden; y de cada operación sesión, n_op, hora de entrada y
salida, lado, familia, tipo, nivel, entrada, salida, motivo, `entrada_por`, BE armado, bruto y duración (P1);
sesión, lado, segundo de entrada y salida, entrada, salida, motivo, BE, bruto, reingreso, nivel, N4 (P2).
**100,000 %.** El motor es el mismo objeto de C# que corre en el gráfico. Re-verificado el 24-09 23:32 por la revisión
adversarial: `bin` y `obj` borrados, recompilado (0 / 0) y paridad corrida de nuevo: 0 / 0 / 0 en los dos instrumentos
(tercer bloque de `resultados/paridad_pauta.txt`).

## Compilación e instalación

1. `dotnet build -c Release` en esta carpeta → `bin\Release\Pauta.dll` (0 errores, 0 advertencias el 24-09).
2. La instalación (copiar la DLL a `%APPDATA%\ATAS\Indicators\` y reiniciar ATAS) la hace el agente principal, no este
   proyecto. En el gráfico: Indicators → "All" → **Pauta (replica de la pauta) - EN PRUEBA** → Add to chart → Apply.
   Grupo *PythiaGex 2.0*. Va en el gráfico de 1 minuto de MNQ; en la primera carga tarda lo que tarde ATAS en darle la
   historia (el cálculo en sí es de segundos: el arnés procesa 2,7 M de barras de 1 s en 2,6 s).
3. No se instala nada más: producción (`PythiaGexNiveles`), el clon (`PythiaGexDos`), `AbsorcionViva` y `DeltaVivo` no
   se tocaron (verificado con `git status` al terminar).

## Lo que esto NO afirma

No hay estrategia validada acá. Hay una réplica fiel de lo mecánico del método (ventana, niveles, bracket, breakeven,
"una y a casa") y su medición, que dio negativa en los dos instrumentos. Lo que el canal no muestra (de dónde salen sus
niveles y si en cada uno compra o vende) no se inventó: se eligió una definición (P1), se declaró SUPUESTO y se midió.
Nada de esto promete ni insinúa rentabilidad.
