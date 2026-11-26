---
name: recarga-en-caliente-atas
description: "ATAS 8.0.15.306 recarga indicadores SIN reiniciar (medido 23-11-2026): copiar el DLL + boton azul de la barra de estado + aviso con OK en cirilico; script herramientas/instalar_superindicador.ps1"
metadata:
  node_type: memory
  type: reference
  originSessionId: 233bf8d4-5113-4e95-a0d9-68bff726690d
  modified: 2026-11-26T22:11:34.155Z
---

Medido el 23-11-2026 instalando el Superindicador (3 veces, ~7 s cada una, sin reiniciar ni cortar Rithmic):

1. Copiar el DLL a `%APPDATA%\ATAS\Indicators\`. El log dice `Created library` (nuevo) o `Changed library` (pisado) y
   `Debounce timer elapsed`, y una notificacion "Some indicator libraries have been changed. You can reload indicators in the
   status bar of the main window". **Hasta recargar, un DLL NUEVO no aparece en el buscador del dialogo Indicators.**
2. En la barra de estado, abajo a la derecha, aparece un boton AZUL (icono de grafico, 32x32) a la IZQUIERDA del icono amarillo
   de la esquina. Para UIA es un Button SIN nombre (x=1536 y=820 con la ventana maximizada 1600x900). Invocarlo ->
   `yModulesManager: Trying to reload indicator types`.
3. Sale un aviso MODAL "Indicators were updated successfully." (ventana 'ATAS' de 1600x852) cuyo boton dice **"ОК" con letras
   CIRILICAS**: buscarlo por nombre "OK" da 0 candidatos; tomar el unico Button de esa ventana (o el PART_Message para
   reconocerla) e invocarlo.
4. Las instancias que ya estan en los graficos pasan a la version nueva CONSERVANDO sus ajustes (verificado: paleta y ventana
   elegidas por el operador sobrevivieron a 1.1 -> 1.2).

**26-11:** el boton azul de recarga fue el UNICO boton sin nombre de la esquina (x=1568 y=820; el icono amarillo no estaba) y
el script se cortaba pidiendo dos: parcheado para aceptar uno. Generico para cualquier DLL: `herramientas/instalar_en_caliente.ps1 -Dll <ruta>`.
ATAS crea una INSTANCIA NUEVA del indicador en cada recarga (en caliente, al conectar el replay, al reiniciar el replay): los
campos de instancia arrancan de cero.

Todo eso lo hace `herramientas/instalar_superindicador.ps1` (parametro -Dll para otro DLL). Cada recarga produce un pico de
latencia de Rithmic de unos segundos (log "market data latency ... back to normal"): no hacerlo con una orden por llenarse.

Otras trampas del mismo dia: despues de la recarga, el boton "Indicators" del grafico IZQUIERDO dejo de verse por UIA (FromPoint
devolvia un "PART_Close" fantasma de bordes infinitos): se abrio el dialogo con un clic real de computer-use en el boton y
"Add to chart"/"Apply" por UIA. `GetYByPrice(p, false)` es el CENTRO del renglon del precio y `true` su borde de arriba (con
filas de 2,26 px dieron 278 y 277). Corrige lo dicho en [[compilar-indicadores-atas]] ("reiniciar sigue siendo el camino").
