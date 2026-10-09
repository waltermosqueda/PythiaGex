---
name: apagar-por-defecto-solo-casilla
description: Cuando el operador pide "desactivar/sacar" una raya, es SOLO destildar su casilla en la vista por defecto (y en su .ws); la serie se sigue calculando en el código porque estamos en investigación
metadata:
  type: feedback
---

Textual (09-10-2026 ~03:05 ART): "la idea no es que la saques/desactives del código sino que la saques de la visualización
predeterminada en pantalla por defecto, porque como estamos en investigación por ahí la quiero ver más adelante".

**Why:** quiere poder volver a prenderla desde los ajustes del indicador para comparar; borrar código le quita opciones.

**How to apply:** (1) en su gráfico: poner S_<id> = false en "MNQ liviano.ws" con ATAS cerrado (herramientas/ws_apagar_series_0910.py,
lo llama instalar_4_0.ps1 -SinDll -AntesDeLanzar; respalda el .ws en herramientas/respaldos_ws/); (2) en el código: solo el default de
la casilla (SeriesPantalla.PrendidasPorDefecto / FamiliaCuatroPantalla), sin renombrar la propiedad ni tocar el cálculo. Apagadas así el
09-10: MUROS_QQQ_vol, MUROS_QQQ_oi, MAJORS_QQQ_oi, DOMS_QQQ_vol, FAM_MUROS_oi (31.076, "superadas por la 2.0 QQQ 31.083") y
ZEST_QQQ_vol (31.057). Ver [[pythiagex-4-0-familia]], [[dominantes-de-noche-por-volumen]] (avisar y captura antes/después).
