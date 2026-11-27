---
name: tradingview-gex-spx-fixed
description: "26-11: su indicador de TradingView 'GEX / SPX-Fixed' (copia de 'GEX / Gamma - SPX' de OptionScalps) NO calcula GEX: 3 numeros a mano + eje propio autoescalado = 'acierta' siempre (tambien en NBIS). Arreglado v4.0 (mismo eje, SPX->ES con base medida, escala solo precio, diseno guardado)."
metadata:
  node_type: memory
  type: project
  originSessionId: 9ca694f8-c4a9-4e80-b09e-71f541dc27e2
  modified: 2026-11-26T17:51:33.088Z
---

Pedido 26-11 ~03:00 ART: revisar el indicador "gex gamma" de su TradingView (diseno "walterX" Jo0uMHKY, ES1! 30 s en
**Heikin Ashi**, ETH, B-ADJ). Creia que "acerto mas que el nuestro" y queria copiar su calculo a ATAS: **no hay calculo**.

- Original publicado: `PUB;19b835c46ddf4c09a4e04eab99d68db2` (OptionScalps, ~1K boosts), UNA sola version, 13-11-2025,
  nunca actualizada; su copia 1.0 (creada 25-11-2026 16:06Z) es identica linea por linea (176 lineas).
- La copia (`USER;044139bc...`, archivo "GEX / SPX-Fixed", titulo "GEX / Gamma - SPX"): tres `input.float` escritos a
  mano (6850/6800/6750, SPX) + `scale = scale.right`. Ese eje propio se autoescala con los mismos margenes (10 % arriba,
  8 % abajo) que el del ES: la caja de arriba cae SIEMPRE sobre el maximo visible y la de abajo sobre el minimo. En NBIS
  (accion de 85 USD) "acertaba" igual. Prueba del dia que el marco (25-11 11:45 ART): minimo ES 6982,50 (TradingView y
  Yahoo), SPX 6977-6984; el -GEX 6750 pasado a ES es 6763,5: 219 pts abajo. SPX 18 al 25-11: minimo 6882,66, nunca toco.
- Arreglado y GUARDADO como v4.0 del mismo script (la 1.0 queda en el historial de versiones). Fuente:
  `PythiaGex-privado/tradingview_spx_fixed/spx_fixed_v2.pine`. Mismo eje; nivel ES = SPX + base; base = mediana de
  (ES - SPX) por minuto 09:35-15:59 NY de la ultima rueda, congelada en el dia, re-medida si se aparta > 8 (roll); pares
  del MISMO minuto (su cuenta recibe `SP_DLY:SPX` demorado); `ticker.new` para medir sin Heikin Ashi; cartel abajo a la
  derecha (base + aviso si los niveles estan a > 120 pts). Diseno con "Scale price chart only" activado.
- Verificado en pantalla 26-11 03:35 ART: base +13,60 (mediana 25-11, 385 min) contra Yahoo 13,56 y carry teorico ~15,4;
  cajas 6760,5-6766,5 / 6810,5-6816,5 / 6860,5-6866,5 = SPX + 13,56 redondeado al tick, +/- 3.

**Trampas de TradingView (en la pagina, por JS):** `TradingViewApi.activeChart()`; `exportData` bloqueado -> velas con
`getSeries().data().each`. Guardar codigo sin el editor: `pine-facade.tradingview.com/pine-facade/translate_light/` (compila)
y `save/next/<id>?allow_create_new=false` (FormData `source`, credentials include). El dialogo de indicadores agrega la
version que tiene en memoria hasta recargar. Los cambios por API NO se guardan solos: `getSaveChartService()
.saveChartSilently()` (POST /api/v1/charts/save/). NUNCA clic derecho sobre el lienzo: el menu trae Buy/Sell; la escala se
cambia con la propiedad `scalesProperties.scaleSeriesOnly`.

**Why:** la ilusion del eje propio es exactamente el tipo de "acierto" que este proyecto existe para desarmar
([[que-afirma-referencia]], [[comprobacion-fehaciente-en-pantalla]]).
**Los otros 5 "GEX" de su grafico (auditados 26-11, fuentes abiertas en `PythiaGex-privado/tradingview_spx_fixed/terceros`):**
GEX Dealer Heatmap = grilla cada 5 alrededor del precio + campana de "fuerza", flip = VWAP (falso). Gamma & Volatility
Levels [Pro] = maximo/minimo de 50 velas, SMA20+1 sigma, cierre +-1,5 ATR (falso). GEXR Matrix (cerrado, sin datos de
entrada) = redondos multiplos de 25 fijados a la apertura RTH + senales ATR (max 3/dia). Gamma Levels Pro (cerrado) = lista
tipeada: la suya es la de fabrica de FEBRERO 2026 (ES = SPX + 31,95) + PDH/PDL/ONH/ONL. TLADe (abierto, v35) = GEX real SI
se pega el texto diario de su terminal: la suya es la muestra de fabrica del 27-10 (cartel STALE) + VWAPs de sesion.
Medido 23-25/11 RTH (`medicion/medir_3_dias.py`, Bar Replay + Yahoo 1 min, placebo corrido +-7,5/12,5): ninguno rebota
mas que su placebo; el unico arriba (max/min 50: 56 vs 47 %, n 87, IC 46-66) no es GEX y es circular. El "acierto" del
25-11 = raya de febrero de Gamma Levels Pro a 0,55 del minimo (p 0,05, 1 de 66 pruebas). Senales GEXR: 3 ganan, 6 pierden.
Diseno de prueba creado en su cuenta: "Unnamed" (dvEYU6lp) con los 7 indicadores; borrarlo solo si el lo pide.
**Ranking 26-11 (`ranking/RANKING.md`, `CONCLUSIONES.md`, preregistro antes de medir):** 1.214 indicadores, 931 codigos,
86 medidos (337 pruebas, ES 5 min 71 ruedas, finalistas en NQ, GEX de dibujo 12 ruedas por replay): NINGUNO pasa. Pistas:
Accumulated P/C Ratio (puts/calls SPX CBOE intradia; signo igual en 4 mitades, fragil) y VWAP +-3 desvios (asimetria:
+3 se frena, -3 sigue). Herramienta: `model.createStudyInserter({type:'pine',pineId,pineVersion:'last'},[]).insert(...)`
carga cualquier script publico sin clics; el evaluador en JS de la pagina mide contra corrimientos por dias.
**Ronda 2 (26-11 tarde, `ronda2/RESULTADOS_RONDA2.md`, preregistrada):** las dos pistas + AlgoAlpha en muestra NUEVA
(ES ago-2025..ago-2026 de la cache de ATAS, NQ, oro de Yahoo) con filtros del operador: NADA aprueba en ningun mercado.
VWAP +3 frena mas que el placebo (47,5 vs 38,7 %) pero no paga (empate 55,8); put/call -0,25/op; AlgoAlpha -0,35/-0,26.
Filtros elegidos en ago-feb (58-66 %) se cayeron en mar-ago (43-48 %): sobreajuste visto en vivo. El tramo ago-nov 2026
favorecio a TODO (de ahi salieron las pistas). Vara limpia: entrar al CIERRE de la vela del toque (la vieja, limite en el
nivel, da 44 % en un paseo al azar con velas grandes). Herramientas: CVSPX/PVSPX son acumulados del dia (USI:), 5 min
desde 17-11-2025; AlgoAlpha reproducido 107/107; datos ATAS ES = TradingView ES1! al centavo en Z6.
**Ronda 3 (26-11 noche, `ronda3_fusion/`):** el 34 % de los toques de banda eran la banda NACIENDO (18:00-18:30 NY,
desvio ~0): excluir siempre banda a < 6 pts del VWAP. La banda +3 FORMADA frena mas que el placebo en los 3 tramos
(53/33, 46/37, 66/30 %) pero no paga 6/6; la receta A1 (regimen + sin maximo de 60 min) era ese artefacto. Interruptor
"va y vuelve" (eficiencia de 10 ruedas) confirmado. Baul completo con 4 rastreos: `PythiaGex-privado/BAUL_DE_PISTAS.md`.
**How to apply:** si vuelve con "este indicador de TradingView acierta", primero mirar si tiene `scale=` o eje propio, de
donde salen los numeros y de que FECHA son; medir contra placebo con la misma densidad de rayas. Pine no lee cadenas de
opciones (de memoria; ningun script auditado lo hace).
