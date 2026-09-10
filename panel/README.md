# Gamma Hoy en la nube (panel/)

La web de la mesa: https://waltermosqueda.github.io/PythiaGex/

Muestra lo mismo que el indicador **Gamma Hoy** de ATAS (velas, dominantes, zero gamma, majors,
perfil por strike, convexidad, pelotitas, cuadrante, disparos) y sigue funcionando **aunque la PC
del operador esté apagada**: la nube baja la cadena de CBOE cada minuto y hace la misma cuenta.

## Cómo llega cada dato (y cuánto tarda)

| camino | qué trae | de dónde | cadencia | cuando la PC está apagada |
|---|---|---|---|---|
| **VIVO** | velas con order flow (delta, big trades), los niveles de cada vela tal como el indicador los dibujó, los disparos (R / M / tren), la cadena viva de ES por Rithmic | `herramientas/subir_vivo.py` en la PC (arranca solo con Windows) sube `vivo-*.json` y `pc.json` a la rama `cadenas` | cada minuto, mientras ATAS está abierto | no llega; la web lo dice con la edad del último latido |
| **NUBE** | la cadena de CBOE (`ultima-<RAIZ>.json`), el estado calculado (`estado-<RAIZ>.json`), la historia del día (`serie-<RAIZ>-<día>.jsonl`), velas del futuro y del índice de Yahoo (`velas-<RAIZ>.json`) | `.github/workflows/cadenas.yml` → `archivar_cadena.py` + `estado_nube.py` | cada minuto en la rueda (13:30–21:00 UTC), cada 5 el resto | sigue igual: es la contingencia |
| **JS** | el perfil recalculado en el navegador con la cadena cruda y los ajustes del operador (horizonte, libro, radio, centroide) | `panel/nucleo.js` | en cada refresco (30 s) | sigue igual |

Reglas de la casa que la web respeta:
- **ningún número sin fuente ni edad**: las tres fichas de arriba dicen de dónde salió cada cosa y hace cuánto;
- **CBOE llega ~15 minutos tarde** (medido: 902 s) y lo dice; las velas de Yahoo también tienen retraso y no traen delta;
- **la base** (futuro − índice) se elige como en el indicador: medida > medida reciente > por precio > cruda > carry teórico, siempre acotada con el carry; si no hay base, no se inventan niveles;
- **nada se presenta como probado**: los gatillos se listan con su resultado crudo, y la ayuda recuerda que el rebote en las rayas rinde igual que una raya inventada.

## Equivalencia (tres cuentas de lo mismo)

`panel/pruebas.html` compara el núcleo JS contra la nube (Python) y contra la línea AUDIT del indicador
(C#). Verificado el 2026-09-10 con la cadena de las 21:34 UTC: JS y nube dan **exactamente** lo mismo
(186 strikes, diferencia 0,000 %), y contra el indicador la única diferencia es el zero gamma por 0,04
puntos (hora de cálculo distinta por segundos). La pestaña **Auditoría y fuentes** repite la comparación
en vivo, en strike (K = futuro − base), para que la base no confunda.

## Pestañas

- **Mesa**: el gráfico (canvas propio, sin librerías): velas 1/2/5/15 min, VWAP, guiones amarillos de las dominantes por vela, puntitos del zero, rayas (zero, majors, dominantes, pesadas), bandas, disparos; a la derecha el perfil por strike (GEX por volumen con el OI detrás, pelotitas de 15/5/1 min, escalera de convexidad); abajo CVD y delta por vela (solo con el vivo). Rueda = zoom, arrastrar = mover, doble clic = volver al final. Al costado: régimen, niveles, precio y base, 0DTE, Max Change, disparos del día.
- **Strikes**: tabla a ±3 % con GEX vol/OI, convexidad, OI y volumen call/put, IV, vencimiento y Δ1/Δ5/Δ15.
- **Vencimientos y 0DTE**: GEX por vencimiento, put/call, IV atm, movimiento esperado 1σ; muros por OI y volumen; max pain; sonrisa de IV del 0DTE.
- **Historia del día**: net GEX vol/OI, zero y dominantes contra el precio, EM del 0DTE, cuadrante a lo largo del día.
- **Gatillos**: los disparos de hoy con MFE/MAE y el resultado crudo (+20/−20 en NQ, +5/−5 en ES).
- **Auditoría y fuentes**: JS vs nube vs ATAS; sello y edad de cada fuente.
- **Qué es cada cosa**: cada concepto con su nombre técnico y su traducción.

## Ajustes

El engranaje abre los mismos ajustes del indicador (horizonte, libro de la convexidad, cuántas
dominantes, radio, centroide, libro de ES) y los de pantalla (banda, rayas pesadas, VWAP, pelotitas, OI
detrás, guiones, perfil por vol u OI, zona horaria, velas visibles). Quedan guardados en el navegador.
`?base=<carpeta>` apunta la web a otra fuente (para pruebas locales: `python -m http.server 8899 --directory panel` y `?base=_prueba/`).

## Lo que NO es

No es una fuente de precio al tick: sin Rithmic (la PC) el precio viene de Yahoo con retraso. No
reemplaza a ATAS para ejecutar. Y no promete rentabilidad: describe el régimen, no la dirección.

## Investigación previa (qué muestran los tableros que se usan)

Antes de decidir las pestañas se miró qué venden los tableros conocidos y qué se puede calcular con
honestidad con la cadena de CBOE:
- SpotGamma: Call Wall, Put Wall, Volatility Trigger, Zero Gamma, Absolute Gamma, implied move; HIRO (delta neto que llega a los dealers en tiempo real). Fuentes: https://spotgamma.com/options-key-levels-explained/ , https://support.spotgamma.com/hc/en-us/articles/15297391724179-Call-Wall-What-It-Is-and-How-SpotGamma-Uses-It
- GEXBot: GEX por strike recalculado cada minuto, gamma walls, zonas de charm, vanna. MenthorQ: NetGEX y su cambio intradía con el volumen fresco. Fuentes: https://groupbuytrading.com/best-gamma-analytics-platforms-2026/ , https://menthorq.com/guide/understanding-0dte-gamma-exposure/
- Tableros 0DTE: pin score, magnet strike, max pain, gamma flip del 0DTE, expected move por straddle ATM. Fuentes: https://flashalpha.com/articles/0dte-gamma-exposure-pin-risk-intraday-options-analytics , https://spotgamma.com/0dte-options-strategy-guide/
- Vanna y charm como exposiciones adicionales: https://quantwheel.com/products/gex-dashboard

Lo que se adoptó: muros por OI y por volumen, max pain, EM 1σ, gamma por vencimiento y 0DTE, net GEX
intradía, DEX y VEX por strike (en `estado-<RAIZ>.json`, con la fórmula escrita). Lo que no: HIRO (hace falta
el flujo de opciones al tick, que no tenemos gratis) y cualquier "pin score" (no está medido acá).

## Archivos

`index.html` (la página) · `app.js` (tableros) · `grafico.js` (el gráfico en canvas) · `datos.js` (fuentes y fusión) ·
`nucleo.js` (la cuenta) · `pruebas.html` (equivalencia) · `gamma-desk.html` (el tablero viejo de SPX/NDX, con `datos.json`) ·
`radar.html` (radar de dominantes y BigTrades) · `datos/` (lo que publica `actualizar.yml`).
