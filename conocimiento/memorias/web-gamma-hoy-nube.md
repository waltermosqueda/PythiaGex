---
name: web-gamma-hoy-nube
description: "La web de contingencia (waltermosqueda.github.io/PythiaGex): Gamma Hoy en la nube con la misma cuenta del indicador, vivo desde ATAS por subir_vivo.py, y que hacer si algo deja de llegar"
metadata: 
  node_type: memory
  type: project
  originSessionId: 9345174c-7f69-4fe5-a793-976e41f2dc7c
  modified: 2026-09-10T22:07:29.353Z
---

Pedido del operador (2026-09-10, noche): "una pagina que refleje en tiempo real los graficos y todo
lo del indicador, sincronizada, que sirva de respaldo en la nube si mi PC deja de funcionar, con
ajustes basicos, CVD y VWAP, y varios dashboards de gamma; investiga que dashboards sirven".

**Arquitectura (sin servidor propio, todo gratis):**
- `estado_nube.py` corre en `.github/workflows/cadenas.yml` cada minuto (rueda) sobre la cadena
  de CBOE recien bajada: es GammaHoyNucleo portado a Python. Escribe en la rama `cadenas`:
  `estado-<RAIZ>.json` (perfil, zero, majors, dominantes, cuadrante, pesadas, Max Change, extras:
  vencimientos, muros, max pain, sonrisa, DEX/VEX), `velas-<RAIZ>.json` (Yahoo 1 min del futuro y
  del indice, con retraso, sin delta) y `serie-<RAIZ>-<dia>.jsonl` (historia intradia).
- `herramientas/subir_vivo.py --bucle` en la PC (arranca con Windows: "PythiaGex subir vivo.bat"
  en la carpeta Inicio) sube cada 20 s por `gh api` (contents PUT) UN archivo `vivo.json` (latido,
  version, AUDIT por raiz, todos los graficos con 300 velas niv/of + disparos, la cadena viva de
  Rithmic). ~270 KB, un commit por vuelta; la rama se aplana a las 22 UTC. OJO: bajo pythonw cada
  gh abria una consola negra (el operador no podia usar la PC): CREATE_NO_WINDOW + STARTUPINFO.
- `panel/` (GitHub Pages, deploy en actualizar.yml cada 5 min y pages.yml al tocar panel/**):
  `nucleo.js` = la cuenta en el navegador (ajustes del operador), `datos.js` = fusion VIVO > NUBE,
  `grafico.js` = canvas propio (no hay lightweight-charts en cdnjs y hacia falta el perfil lateral),
  `app.js` = tableros. `pruebas.html` = equivalencia JS / nube / ATAS.
- La web lee raw.githubusercontent.com/.../cadenas/ con `?v=` (CORS *, cache 5 min se salta con la
  query). Opensera ya no tiene API (402): no se usa.

**Verificado:** JS y Python dan la MISMA linea AUDIT que el indicador (186 strikes iguales; zero a
0,04 pts por segundos de hora). El Max Change de la nube usa el precio de cada minuto de Yahoo (o
el spot de la cadena): aproxima al del indicador, no lo iguala.

**Why:** si la PC o ATAS se caen, el operador queda sin niveles; con esto tiene el mismo cuadro
desde el telefono, con la edad de cada dato a la vista y sin inventar precio (Yahoo con retraso).

**How to apply:** si la web dice "PC apagada o sin subidor" con ATAS abierto, revisar que corra
`pythonw subir_vivo.py --bucle` (Get-Process pythonw) y que `gh auth status` siga logueado. Si
falta `estado-*.json`, mirar `gh run list --workflow=cadenas.yml`. Probar local:
`python -m http.server 8899 --directory panel` y `index.html?base=_prueba/`. Ver
[[archivo-cadenas-y-respaldo]], [[calcular-gex-propio]], [[gatillo-cientifico-2026-09-10]].

**v2 (10-09 noche, pedido del operador):** todo en una sola pagina (mesa rapida de strikes a
+-1,5 % con etiquetas D1/D2/+Γ/−Γ/muros/0Γ/pesada/0DTE, referentes, tendencia ahora = lecturas
con regla y sin señal, vencimientos, historia, gatillos, auditoria), grafico movible y escalable
como ATAS (arrastre = tiempo y precio, eje = estirar, rueda, ctrl+rueda, teclas, doble clic = vivo),
1/2/3/5/15/30 min, refresco 15 s. Pendiente al escribir esto: reiniciar ATAS para cargar 1.8c (poda
de memoria + busqueda del conector de Rithmic) y ver si la cadena viva vuelve.

**2026-09-11 03:30, "la web quedo rota":** no estaba rota por los renombres (Actions verdes); lo que
fallaba era (1) el subidor `subir_vivo.py` no corria (ultimo latido 3,4 h; hay que relanzarlo con
`herramientas/subir_vivo.bat` o `Start-Process pythonw herramientas\subir_vivo.py --bucle`), y (2) la
pagina solo aceptaba el vivo si estaba abierto el marco pedido o el de 1 min: con MNQ-M2/M5 abiertos y
1m por defecto decia "PC sin señal hace 0 min". Ahora usa el mas fino abierto (agrupa si es mas fino,
lo dice si es mas grueso). Ademas nucleo.js y estado_nube.py llevan el empate tecnico de la 1.8i, y el
subidor busca la version del indicador mas atras en el log (antes salia null). Verificado en el
navegador de fondo: "TU ATAS · vivo hace 36 s · Gamma Hoy 1.8i", sin errores de consola.

**2026-09-11 04:00, "los niveles no sincronizan con ATAS" y "las barras laterales son invasivas":**
la desincronizacion era real: el subidor solo mandaba el centinela `hoy-` (grafico con libro CBOE) y la
web ademas RECALCULABA los niveles con CBOE, mientras el operador miraba el grafico de MNQ 2m con el
libro vivo de Rithmic (`hoyrithmic-`): dos mapas distintos (29.465/29.152 contra 29.400/29.000).
Arreglo: (1) el subidor manda por grafico el centinela mas fresco y prefiere Rithmic si los dos estan
al dia, con etiqueta `libro`; (2) con el vivo fresco, la web toma los NIVELES de la ultima vela de
ATAS (zero, majors, dominantes, pico) y lo dice en la cabecera ("NIVELES DE TU ATAS (libro Rithmic
vivo, M2)"); el recalculo propio queda para el perfil, la auditoria y la contingencia; (3) rotulos del
perfil solo en las 3 barras mas grandes por lado + dominantes + majors; (4) columna lateral plegable
con el boton "info" (oculta por defecto) y tarjetas de abajo en <details> con memoria; (5) Pages
cachea 10 min: los scripts van con `?v=<sha>` (pages.yml los versiona en Python al publicar) porque
la pagina nueva cargaba app.js viejo. Verificado en el navegador de fondo (1600x900): tarjeta
"Niveles de tu ATAS, libro Rithmic" = 29.400 / 28.999,57 / zero 29.195,93 / majors 29.400 y 29.000,
identico al cuadro de ATAS; sin errores de consola. OJO: pages.yml solo se dispara con cambios en
panel/** (o en el propio yml); un cambio solo en el yml no siempre corre: tocar panel/.

**2026-09-11 16:45-17:00, auditoria de la nube pedida por el operador:** la web tenia los scripts del
repo (app.js, nucleo.js, datos.js identicos por sha) y las Actions verdes, pero el vivo estaba muerto:
`vivo.json` generado 10:49 local, y el `pythonw subir_vivo.py --bucle` de las 10:51 nunca subio (6 h)
sin dejar rastro porque `log()` solo imprimia a stdout. Corrido a mano subio al instante. Arreglo:
el log tambien va a `%APPDATA%\ATAS\pythiagex-subir-vivo.log`; se mato el proceso viejo y se relanzo
con `Start-Process pythonw` (desde Bash, `cmd /c start` no lo lanza). Ademas ATAS estaba cerrado desde
las 16:17 y el vigilante no corria: el `.bat` de Inicio tenia un BEL en vez de `\a` (la ruta decia
`PythiaGex<BEL>tas_vigilante.ps1`, invisible al leerlo) y nunca habia arrancado al iniciar sesion;
reescrito byte a byte y verificado sin caracteres de control. OJO al escribir rutas con `\a`, `\v`,
`
` desde printf o heredocs: el harness des-escapa una vez y el segundo escape se vuelve un
caracter de control. Verificado: subidor subiendo cada 20 s con viva ES y NQ, version 1.8j.
