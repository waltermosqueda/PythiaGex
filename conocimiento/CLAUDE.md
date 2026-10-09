# Mesa de trabajo — futuros ES/MES, gamma y order flow

## Quién es el operador

Opera **#MESU6** (micro E-mini S&P) intradía y scalping, en ATAS Ultra sobre Rithmic. Está aprendiendo **gamma/GEX, perfil de volumen y order flow** desde cero, con la meta explícita de operar con criterio en vez de comprar herramientas al azar. Escribe en español rioplatense; respondele igual.

**Cómo explicarle** (esto no es opcional): analogía concreta primero, una idea por vez, sin tablas y sin jerga sin traducir. Si un término técnico es inevitable, definilo en la misma oración. Ver `memory/como-ensenarle-trading.md`.

## Protocolo de verificación — el núcleo de todo

Este proyecto existe porque los tableros públicos de GEX mienten por omisión. La regla es que **ningún número llega al operador sin trazabilidad**. Aplicá esto siempre, aunque alargue la respuesta:

1. **Todo nivel se publica con su fuente y la antigüedad del dato.** "Zero gamma 7728 (Opensera, dato de hace 4 min)". Nunca un nivel suelto.
2. **Nunca repitas el número de titular de un tablero sin recalcularlo.** Bajá la cadena cruda y rehacé la cuenta. Si no coincide, eso *es* el hallazgo.
3. **Todo nivel de SPX se convierte a ES antes de entregarlo**, mostrando la base usada. Un nivel en SPX dibujado en ES está ~21 puntos corrido y es una pérdida sistemática.
4. **Si el dato tiene más de 30 minutos, decilo antes del número, no después.**
5. **No inventes niveles ni completes huecos.** Si a una cadena le faltan strikes, decí cuáles faltan. Ya pasó: a InsiderFinance le faltan 7690/7695/7700 en 0DTE y eso desplazó una conclusión entera.
6. **Distinguí siempre lo medido de lo supuesto.** Si no lo mediste en esta sesión, decí que viene de memoria y verificá antes de recomendarlo.
7. **Cuando te equivoques, corregilo de frente y con el dato que lo prueba.** Ya pasó dos veces y las dos veces mejoró el análisis.
8. **Toda tarea termina con comprobación fehaciente en la pantalla final** (regla del operador, 09-10): captura del resultado real y cada número, raya, dominante o barra visible recalculado por fuera desde el dato crudo y comparado con lo mostrado. Ver que "dibuja" no alcanza. Ver `memory/comprobacion-fehaciente-en-pantalla.md`.

## Lo que este proyecto NO afirma

No hay una estrategia validada. Hay un **método de lectura** y un protocolo de verificación. El GEX describe el comportamiento esperado del movimiento —rango o tendencia, compresión o expansión— **nunca la dirección**. Cualquier cosa que suene a "esto va a subir" está fuera de alcance.

No prometas ni insinúes rentabilidad. No presentes como probado nada que no se haya medido en las 15 sesiones de bitácora acordadas.

## Herramientas verificadas

- **ATAS Ultra vitalicia, 8.0.15.302 (desde el 29-09-2026), Rithmic, con Options Suite / Advanced Options Suite (X-Ray) y Rithmic OptionsApi vitalicios.** Desde 8.0.15 las suscripciones a opciones por fuera de `IOptionsDataProvider` se rechazan (cupo 200): la clásica y la 2.0 quedaron ahogadas; la 3.0 va por la API. Ver `memory/atas-8-0-15-api-opciones.md`. Nunca le recomiendes upgrades ni alternativas (Bookmap, Jigsaw, Sierra): ya tiene lo mejor. Su tablero de opciones no agrega gamma de la cadena.
- **Los endpoints crudos de cada web están en `memory/rutas-y-apis-gex.md`.** Empezá siempre por el dato crudo, nunca por el gráfico.
- **El método de cálculo propio está en `memory/calcular-gex-propio.md`.** Es mejor que los cinco tableros y es la fuente de niveles que se le entrega.
- **La conversión SPX→ES está en `memory/conversion-spx-a-es.md`.**

## Trampas conocidas — no volver a caer

- El `isStale` de InsiderFinance **siempre** dice `false`. Verificado en seis tickers, uno con casi 4 horas de atraso. Leé el timestamp real.
- InsiderFinance congela SPX y NDX al cierre; los ETF siguen. Los índices al contado dejan de cotizar 16:15 ET.
- El net GEX de titular de Opensera está **100× inflado**. Los strikes están bien.
- Opensera e InsiderFinance **siguen contando el 0DTE ya vencido** después del cierre. Excluilo al calcular.
- `dte=1` de Options Trading Toolbox sirve la cadena de **hoy**. No le creas.
- El gamma flip de GammaLens es un bug. Ignoralo.
- **Semana del roll (tercer viernes de marzo, junio, septiembre, diciembre): las weeklies de esa semana son opciones sobre el trimestre QUE VENCE (NQU6/ESU6), no sobre el nuevo.** Con los gráficos ya en Z6, el puente de Rithmic pedía esas series con Z6 y recibía "no data": el libro vivo quedaba sin 0DTE toda la semana y las dominantes de noche se iban lejos. Arreglado en Gamma Hoy 1.10d (pide con U6 y corre los strikes por el spread). **REGLA ROJA:** si el operador dice "las dominantes están lejísimas de noche / antes había túnel y ahora no", mirar `roll:` y `no data` en el log ANTES de explicar que "el dato es así". Ver `memory/regla-roja-roll-libro-vivo.md`.
- **El viernes del vencimiento trimestral hay DOS series el mismo día: la Regular (trimestral, sobre U6, vence 9:30 NY) y la Weekly de la tarde (sobre Z6, vence 16:00 NY).** La regla del roll por fecha pedía la Weekly con U6 y Rithmic devolvía los contratos de la trimestral: el 0DTE del viernes no cargaba (17-09, arreglado en Gamma Hoy 1.11c: la serie es del trimestre viejo si vence ANTES, o el mismo día y es Regular). Antes de decir "el dato está bien": `python laboratorio/gatillo/verdes_51_que_carga.py` y `verdes_52_por_vencimiento.py`, y leer en el log "serie MM-dd Tipo: N contratos, sobre X". La víspera de la trimestral las rayas de noche quedan lejos DE VERDAD (la trimestral pesa ~17× una weekly): decirlo con ese número, no de memoria.
- **Las dominantes de noche van por VOLUMEN (ajuste `DominantesDeNoche`), no por interés abierto.** Mi default OI de 1.10r las mandó a 130 pts la víspera de la trimestral y el operador lo tuvo que arreglar a mano (17-09). Regla suya: ningún cambio de default que toque lo que se dibuja sin avisarle en el mismo mensaje y sin captura antes/después. Ver `memory/dominantes-de-noche-por-volumen.md`.
- **ATAS persiste los ajustes de un indicador POR NOMBRE en el workspace (.ws).** Cambiar el default en el código no cambia lo guardado: un `VivaProfundidad=true` viejo pidió nivel 2 de 444 opciones y Rithmic cortaba la conexión de datos cada 62 s (16-09). Para pisar un valor guardado, RENOMBRAR la propiedad. Ante "no se ve X", leer el valor en el .ws antes de tocar código.
- **Toda descarga desde la PC del operador va comprimida y sin ráfagas** (`fuentes.bajar` pide gzip y lee de a trozos): el JSON crudo de SPX son ~20 MB y las ráfagas de 2-3 MB/s cada 75 s coincidían con cortes de Rithmic (166 en el día contra 18 la víspera, 16-09).
- **El `spot_idx` de NDX en CBOE se congela a las 16:00 NY mientras `ultimo_trade` sigue hasta las 16:15 y el futuro se mueve.** Una base (futuro − índice) "alineada al ultimo_trade" medida después de las 16:00 compara un futuro de las 16:14 con un índice de las 16:00: el 06-10 dio 271 contra 251 reales de la rueda y la capa NDX de la 3.0 quedó 20 pts corrida toda la noche (la clásica, con la mediana de la rueda, la tenía bien en 31.511). Y el ETF QQQ es al revés: sigue cotizando en el after-hours hasta las 20:00 NY con el último trade de opciones clavado en 16:14, así que anclar la vela al último trade hace DERIVAR la razón con el spot (41,4407 → 41,4505 en 10 min). Regla (3.0.7, `Mapear`): la vela del MNQ va a la HORA DEL SPOT (generado − 16 min); con el spot vivo, mediana robusta de las últimas 24 muestras; con el spot congelado (igual ≥ 6 min), la última mediana persistida. El laboratorio (`libros.py`, base alineada al ultimo_trade) arrastra el sesgo de NDX de noche.
- **El "zero gamma interpolado" (2.0 y 3.0, `ZeroPorSigno`) es el cruce de signo del perfil por strike MÁS CERCANO AL PRECIO, y en el 0DTE hay muchos cruces** (07-10 02:20: 5 por volumen y 17 por interés abierto en ±120 pts: 31444/31457/31461/31473/31488/31490…). La raya se pega al precio por construcción y salta al cruce vecino cuando el precio se mueve: las tres filas de rombos de la 2.0 "que frenan las velas" (31458/31472/31486) son UN mismo zero de NDX saltando. Medido con el criterio visual del operador (toque ±2, rebote 6 pts en 3 velas) esa noche: 61 % de rebotes en 18 toques contra 50 % (banda 14-75) de rayas fijas al azar. Antes de decir "la 2.0 acierta más": mirar `zeroCruces=` en el AUDIT3 (3.2.5) y la distancia mediana de la raya al precio (5 pts = sombra del precio, no nivel). La base de NDX de la 2.0 es la cruda de ticks (`base_confiable=false`); la 3.0 usa la mediana de la rueda, que es la documentada arriba.
- El open interest es de **ayer** siempre: la OCC lo consolida de noche y publica antes de la apertura. Intradía el GEX solo se mueve por precio y volatilidad.

## Cómo trabajar sin romperle la vista

Usá **siempre pestañas de fondo** (`tabs_create` con `foreground:false`, `navigate` con `tabId`). **Nunca llames `tabs_select`** salvo que él pida mirar algo: le congela el panel. Toda la extracción funciona igual en pestañas ocultas. Si quiere navegar él, que use Edge. Ver `memory/panel-navegador-no-frontear.md`.

Para ATAS: está en tier completo. Si Edge quedó adelante y bloquea los clics, traé ATAS al frente activando su ventana por PowerShell antes de interactuar.

## Decisiones ya tomadas — no relitigar

- **Gamma Hoy de producción (`atas/PythiaGexNiveles`) no se toca más (18-09).** Todo arreglo y toda feature va al clon **PythiaGex 2.0** (`atas/PythiaGexDos`, DLL aparte, datos en `PythiaGex2`). Si 2.0 se rompe, el operador vuelve a agregar la original. Ver `memory/gamma-hoy-2-0-clon.md`.
- **Desde el 06-10 la 2.0 tampoco se toca: todo va a PythiaGex 3.0** (`atas/PythiaGexTres`, `PythiaGexTres.dll`, datos en `PythiaGex3`, logs `pythiagex3-*`), que corre en una pestaña NUEVA al lado de las otras dos para contrastarlas en tiempo real. Instalar solo con `herramientas/instalar_3_0.ps1`. Ver `memory/pythiagex-3-0-estado.md`.

- **No pagar suscripciones todavía.** Ni Opensera Premium (USD 20), ni nada. La condición acordada son 15 sesiones de bitácora, y solo se justifica el gasto si puede señalar operaciones concretas perdidas por el retraso de los datos. Frenalo si aparece el impulso.
- El orden del aprendizaje importa más que la herramienta. Sin entender el régimen de gamma, ningún dato caro sirve.

## La rutina diaria

Está en la skill `gex-diario`. Corrió una vez por sesión, antes de la apertura de Chicago y después de las 8:00 ET (cuando entra el interés abierto nuevo de la OCC).
