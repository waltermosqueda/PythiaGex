---
name: archivo-cadenas-y-respaldo
description: Donde se archiva la cadena (rama cadenas cada minuto, local, viva de Rithmic), que NO se puede grabar sin su ATAS, y el respaldo espejo a Inversiones
metadata:
  type: project
---

Pedido del operador (2026-09-07 noche): "todo minuto a minuto en git", la
cadena viva de Rithmic "en lo posible sin que yo este conectado", y el
proyecto tambien adentro de Escritorio\Inversiones (sincronizada).

**Cadena de CBOE, cada minuto:** `.github/workflows/cadenas.yml` corre cada
minuto en 13-21 UTC (cada 5 el resto) y anota `archivar_cadena.py --bajar ES
NQ` en la RAMA `cadenas` (orfana; se aplana a un commit a las 22 UTC). Un
archivo por dia y raiz: cadena-ES-AAAA-MM-DD.jsonl.gz, ~16 KB por minuto.
El indicador lo baja de https://raw.githubusercontent.com/waltermosqueda/PythiaGex/cadenas/
(ajuste UrlArchivo). Main sigue con actualizar.yml cada 5 min (feed y panel).

**Cadena viva de Rithmic:** NO se puede sin su ATAS abierto (son sus
credenciales, un solo login, y ATAS Ultra en una PC). Lo que hay: Gamma Hoy
0.4 guarda un renglon por minuto en %APPDATA%\ATAS\PythiaGex\viva\viva-ES-<dia>.jsonl
mientras ATAS esta abierto (ajuste GuardarViva). Alternativas pagas (Databento
live, VPS con ATAS) descartadas por la regla de no pagar.

**Grabacion de la viva, hecha automatica (2026-09-07 23:00 UTC, con
autorizacion "sos autonomo"):** Gamma Hoy 0.6 arranca la cadena viva y la graba
en TODOS los modos (tambien Archivo), y Gamma Vivo la archiva tambien (una
sola linea por minuto y raiz: Feed.Archivo.GuardarViva deduplica). Archivo:
%APPDATA%\ATAS\PythiaGexivaiva-ES-<dia>.jsonl (ts, futuro, grandes,
filas strike/dias/es_call/oi/iv/bid/ask/vol_hoy/vol_cinta/compra/venta).
Para que ATAS este arriba sin el operador: `%APPDATA%\PythiaGextas_vigilante.ps1`
(bucle cada 15 min llama atas_autoarranque.ps1: lanza ATAS, aprieta Connect
con la clave recordada por UIA, restaura la ventana), arrancado desde la
carpeta Inicio de Windows ("PythiaGex ATAS vigilante.bat"). schtasks dio
"Access is denied" desde el sandbox: por eso la carpeta Inicio. Limite
fisico que queda: si la PC esta apagada o dormida, no hay Rithmic.

**Respaldo:** `herramientas/respaldo_inversiones.ps1` (robocopy /MIR) copia
repo + %APPDATA%\ATAS\PythiaGex + pythiagex-* + memorias a
Escritorio\Inversiones\PythiaGex-respaldo (1,8 GB la primera vez). OJO: el
2026-09-07 el proceso OneDrive NO estaba corriendo: la carpeta no sincroniza
hasta que arranque. Tarea programada diaria: propuesta, sin su OK todavia.

**QQQ desde el 2026-09-11:** `archivar_cadena.py --bajar ES NQ QQQ` (INDICE admite QQQ y SPY) y
`cadenas.yml` lo baja por minuto; ese dia ademas corrio un bucle local hasta las 21:06 UTC que
escribio `%APPDATA%\ATAS\PythiaGex\cadenas\cadena-QQQ-2026-09-11.jsonl.gz` (mismo formato,
campos strike,venc,oi_call,oi_put,iv_call,iv_put,vol_call,vol_put; la "base" ahi no significa
nada). Sirve para probar en el laboratorio el libro que usa la referencia para NQ (ver
[[referencia-formulas-nq-medidas]]) y su perfil derecho, que parece un cambio en el tiempo.

**How to apply:** los datos nuevos son propios desde hoy; Databento solo para
antes del 2026-08-19. Ver [[rebobina-simulador]], [[databento-cuenta-y-costos]].
