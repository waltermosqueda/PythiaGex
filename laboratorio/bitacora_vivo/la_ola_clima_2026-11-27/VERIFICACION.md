# La Ola 1.0 — el CLIMA con la rueda abierta, contra la cinta (27-11-2026)

Media rueda (Black Friday: la rueda cierra 13:00 NY, el futuro 13:15 NY). La curva f(m) del clima supone rueda entera, así que **el número
del clima de hoy no es comparable con el de otros días**; lo que se controla acá es que lo mostrado sea la cuenta exacta.

## Incidente antes de medir: el gráfico de pruebas dejó de dibujarse
- A las 11:46 AR el gráfico ESZ6 1m (pestaña de abajo "ESZ6 1m Chart") estaba congelado en **07:29:41 AR**: velas, panel, rayas y eje
  quietos 4 h 20 min. El gráfico de la derecha seguía vivo.
- La Ola **seguía calculando**: su csv (lo escribe el latido) estaba al día; lo que se cortó fue el registro que se escribe DESDE EL
  DIBUJO (auditoria-*.jsonl de La Ola y del Superindicador, últimas líneas 07:29:39 y 07:29:29). Ningún hilo de ATAS gastaba CPU de más
  (máximo 10 % de un núcleo): no era un bucle.
- `RedrawWindow` no alcanzó. **Cambiar de pestaña por UI Automation** (Orders y de vuelta a ESZ6 1m Chart, sin clics ni teclas, con el
  usuario quieto 349 s) lo destrabó al instante: a las 11:53:05 volvió la línea del jsonl. O sea, **no lo trabó ningún indicador**:
  ATAS dejó de pintar esa pestaña. La causa no está identificada (el apagado de pantalla por inactividad no cierra con la hora).
- Queda `herramientas/vigia_dibujo_ola.py` (pasivo) para anotar cuándo vuelve a pasar.
- **Corrección (27-11, 14:05 AR):** la causa probable es que ATAS NO PINTA los gráficos tapados por otra ventana. La PC tiene un solo
  monitor (1600×900) y la ventana de Claude, maximizada, queda exactamente encima de ATAS; a las 14:02:48-49 los dos gráficos (ES y
  MES) dejaron de escribir su registro de dibujo a la vez, con Claude al frente y el operador escribiéndome, mientras La Ola seguía
  calculando. Es inofensivo (al volver a ATAS repinta). Para la mañana (07:29-11:53) no quedó guardada la geometría de las ventanas:
  es lo más probable, no está medido. Detalle en `../la_ola_es_mes_2026-11-27/VERIFICACION.md`, sección 6.

## Lo comprobado (todo desde la cinta de la sonda `cinta-ESZ6-202611262300-202611271455.csv`, 144.484 órdenes)
1. **Captura sincronizada** 11:54:05 AR (`captura_1154_sincronizada.png`, la cuenta tapada). La foto corresponde a UNA sola orden de la
   cinta: la **143.573** (14:54:05.299 UTC), dos órdenes después de la línea del jsonl (143.571). En esa orden la cinta da exactamente lo
   que se ve (`salida_instante_de_la_foto.txt`):
   - MAREA ▼ con vendedores, Δ −3.509, faltan 32,50, raya "marea gira 7.272,75";
   - OLA ▼ con vendedores, Δ −761, faltan 7,25, raya "ola gira 7.247,50";
   - último 7.240,25 (el del eje); delta acumulado de la jornada −555 (el "total" del Superindicador).
2. **CLIMA "ROTACIÓN · 1,1× lo normal a esta hora (11:53)"**, recalculado con código propio (sin la grilla por segundo): minuto 23
   (9:53 NY, cierra 14:54:00 UTC), máximo 7.288,50, mínimo 7.231,75, rango 56,75, apertura 7.287,00, cierre 7.238,50, VWAP 7.253,546;
   rrn = 56,75 / (126,625 × f(23) 0,39186) = **1,1437** → entre 1,0 y 1,6 → ROTACIÓN. f(23) leído del json de la ronda 21 (huella
   72589182…bc33, la misma que trae La Ola).
3. **Los 24 minutos cerrados del clima** (9:30 a 9:53) del csv contra la cinta con la definición de la R21: **0 diferencias**; mediana de
   10 ruedas recalculada de las cintas (12-11 a 25-11): **126,625 = la del indicador**; X 10,25 (`salida_auditar_pythiaola.txt`).
   Ola y marea: 30 pivotes y 1.910 fotos, 36.500 campos: **0 diferencias**. (Las 28 "diferencias" del control de panel de ese script
   son las falsas conocidas: compara el panel a mitad de minuto con la foto del cierre.)
4. **Panel**, 16 líneas del jsonl cubiertas por la cinta, comparadas en el mismo instante (cantidad de órdenes n): **0 distintos**
   (`salida_auditar_panel_ola.txt`), incluidas las dos de la rueda abierta (11:53 y 11:54).
5. **La línea de la ola**: los pivotes de la cinta superpuestos a la captura caen en los quiebres (`captura_con_pivotes_de_la_cinta.png`);
   el quiebre del techo de las 10:10 (7.320,00) está en x 602,5 a la altura exacta (y 310 contra 310,4 calculado).
6. **El fondo de la marea**: sus 5 cambios (rojo 08:46, ámbar 09:57, rojo 11:08, ámbar 11:10, rojo 11:35 AR) aparecen en la captura en el
   mismo orden y con las mismas distancias (99, 99-100, 2-3 y 34 px), a 1-2 px de la vela que corresponde (una vela = 1,39 px)
   (`salida_fondo.txt`). Ojo al medir: ATAS escribe la etiqueta de hora del eje ~5 px a la izquierda del centro de su vela.
7. **Lo que no es de La Ola pero se ve**: la vela de ATAS arriba a la izquierda (11:53: O 7.243,75 H 7.248,25 L 7.237,50 C 7.238,50
   V 3.238) = cinta; el perfil del Superindicador en la misma línea de dibujo (14:54:05.365): POC 7.315,00, VAH 7.342,50, VAL 7.255,50
   = pantalla, "LO DIBUJADO: TODO IGUAL" (`salida_auditar_superindicador_linea22.txt`). Un minuto después el POC saltó a 7.241 por el
   volumen de la caída de la apertura.

Nada de esto valida al clima como señal: es una lectura del día con su número (ronda 21), no una entrada.
