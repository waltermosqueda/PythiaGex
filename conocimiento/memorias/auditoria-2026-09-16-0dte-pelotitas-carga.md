---
name: auditoria-2026-09-16-0dte-pelotitas-carga
description: "Auditoria del 16-09 (Gamma Hoy 1.10y/1.10z): el jueves de la semana del roll faltaba en el libro vivo, la latencia de 22 s era la profundidad persistida en el workspace (no la red), las pelotitas estaban apagadas por ajuste y no existian en las capas, y la clave de las fotos del libro Rithmic cambiaba con el spread."
metadata: 
  node_type: memory
  type: project
  originSessionId: 9345174c-7f69-4fe5-a793-976e41f2dc7c
  modified: 2026-09-16T15:40:11.453Z
---

Pedido del operador (16-09 11:40): "revisa audita el indicador, creo que falto afinar los strikes y los
vencimientos 0DTE; que las pelotitas de convexidad funcionen bien; los quiero ver ahora, de noche, mañana y
siempre, soy scalper intraday". Cuatro auditores + un esceptico por hallazgo, ninguno refutado. Detalle con
lineas en `atas/PythiaGexNiveles/GammaHoy.md` (seccion 1.10y/1.10z). Lo que hay que recordar:

- **Rithmic lista las weeklies del trimestre que vence de a pedazos:** bajo NQZ6/ESZ6 aparecen el 16 y el 18
  pero NO el jueves 17; bajo NQU6/ESU6 si. En semana de roll hay que listar series con los DOS codigos y
  sumar fechas (1.10y). Verificar en el log: "roll: N vencimiento(s) que solo lista NQU6" y `viva-NQ` con
  dias 0,x y 1,x de dia. Ver [[roll-semana-weeklies-en-el-trimestre-viejo]] y [[regla-roja-roll-libro-vivo]].
- **ATAS persiste los ajustes del indicador POR NOMBRE en el workspace** (`Workspaces_v3/Default workspace.ws`,
  JSON): cambiar el default en el codigo no cambia lo guardado. Un `true` viejo de `VivaProfundidad` (1.10s,
  02:03) pedia Quotes de 444 opciones (4-15k ev/s) y Rithmic cortaba la conexion de datos cada 62 s ("Market
  Data Latency 22841 ms" con posicion abierta). Para pisar un valor guardado: RENOMBRAR la propiedad
  (`ProfundidadOpciones`, `PelotitasMaxChange`). Y antes de culpar a la red: 134 pings a Rithmic sin perdidas.
- **Las pelotitas viven por capa** (`PelotitasCapas` en GammaHoyCapas.cs): cada `CapaLibro` tiene su nucleo con
  fotos por minuto; se siembran desde el archivo al arrancar. Sin eso, con la primaria oculta (capa NDX
  duplicada) no se veia ninguna. Ver [[pelotitas-max-change-medidas]].
- **La clave de las fotos del Max Change es el strike CRUDO** (`Strike.Clave`, `-K0` para el trimestre viejo,
  columna `strike0` en el viva): el strike corrido cambia con el spread Z6-U6 cada minuto y el Max Change del
  libro Rithmic era la barra entera.
- **Dias del libro vivo con la hora de Nueva York**, nunca la fecha local: de 00:00 a 01:00 (Argentina) el
  0DTE valia 0,01 dias. El trimestral vence a las 9:30 ET (regla de CME de memoria, sin confirmar en la
  especificacion).
- **Un grafico que no es ES/NQ/RTY arrancaba como ES** (oro GCZ6: 320 suscripciones, 17 lineas U6 en viva-ES,
  base-rueda-ES pisada). Ahora `RaizSoportada()`; si el operador abre otro instrumento con la plantilla, el
  indicador avisa y no hace nada.
- **Instalado y visto en pantalla 12:36:** sin cartel de latencia, 0 caidas, pelotitas de tres tamaños en las
  barras de SPX/QQQ/NDX (izquierda y escalera), viva-NQ con el 17. El build con `-K0` (12:36) quedo en
  bin/Release sin instalar: va en el proximo reinicio (solo importa con Horizonte=Semana/Todo en semana de roll).

- **Los cortes de Rithmic no terminaron con la profundidad:** siguieron ~1 cada 1-2 min. Segunda causa medida: `cboe_local.py`
  (nuevo hoy 01:48) bajaba las cadenas de CBOE SIN gzip (NDX ~8 MB, SPX ~20 MB cada 75 s): rafagas de 2-3 MB/s de
  bajada. `fuentes.bajar` ahora pide gzip (0,8 / 1,7 MB) y lee de a trozos. La prueba de 10 min con el bajador
  pausado y la de 10 min con el bajador comprimido estan en el log de ATAS (`Connection lost` por hora).

**Why:** tres veces el operador tuvo razon contra mi "el dato es asi"; esta vez pidio auditar antes de que
pasara y tenia razon en las dos cosas (0DTE del jueves y pelotitas).

**How to apply:** en semana de roll, mirar en el log que el 1DTE de U6 este; ante latencia, mirar primero
`Changing subscription status ... Quotes` en el log de ATAS y `Connection lost` por hora; ante "no se ve X",
abrir el workspace .ws y leer el valor guardado antes de tocar codigo.
