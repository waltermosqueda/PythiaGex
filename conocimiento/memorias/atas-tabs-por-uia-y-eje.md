---
name: atas-tabs-por-uia-y-eje
description: "Cambiar de pestaña de gráfico en ATAS por UI Automation (SelectionItemPattern) cuando los clics no entran; y medido: el lienzo del indicador NO llega al eje de precio (clip 849 de 913 px), los chips en el eje solo pueden ser rótulos nativos de ATAS."
metadata: 
  node_type: memory
  type: project
  originSessionId: 9345174c-7f69-4fe5-a793-976e41f2dc7c
  modified: 2026-09-07T07:30:49.654Z
---

Medido el 2026-09-07 a las 04:29, ATAS 8.0.14.398.

## Pestañas que no cambian con el clic

Después de reabrir ATAS, ni el clic del computer-use ni un `mouse_event` crudo
de user32 en la pestaña "MES 5m Chart" (rect físico 184,794 88x22) cambiaban
de gráfico, aunque la ventana estaba al frente (`SetForegroundWindow` ok).
Lo que sí funciona, siempre:

```powershell
Add-Type -AssemblyName UIAutomationClient; Add-Type -AssemblyName UIAutomationTypes
$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "MES 5m Chart")
$els = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)   # hay 2: panel izquierdo (X<800) y derecho
$izq = $els | ? { $_.Current.BoundingRectangle.X -lt 800 } | select -First 1
$pat = $null; $izq.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pat); $pat.Select()
```

Los nombres exactos son "MNQ 5m Chart", "MES 1m Chart", "MES 5m Chart"
(TabItem). Traer la ventana al frente: `scratchpad/frente.ps1` (keybd_event
ALT + SetForegroundWindow); el `AppActivate` de WScript dice True pero no
alcanza.

## El lienzo no llega al eje

En el primer render, `g.ClipBounds` = {0,0,849x558} con `ChartArea` =
913x580: el eje de precio (64 px a la derecha) y el eje de tiempo (22 px
abajo) quedan FUERA del recorte. Nada que dibuje el indicador aparece ahí.
Verificado en el SDK con `_api --miembros`: `DrawingLayouts` solo tiene
None/Historical/LatestBar/Final (no hay capa de eje); lo que sí existe es
`ValueDataSeries.ShowCurrentValue` / `StringFormat` y `LineSeries` (Value,
Text, Color, Width, LineDashStyle, UseScale), que ATAS dibuja él mismo con
rótulo en el eje. Y `ProcessMouseClick` / `MouseLocationInfo` existen: los
desplegables son posibles.

**Why:** el operador pidió "chips en el eje de precio". La única vía es la
nativa y falta probar si el rótulo nativo admite texto además del precio.

**How to apply:** para pestañas usar UIA, no clics. Para el eje, probar
primero un `ValueDataSeries` con `ShowCurrentValue=true` y `StringFormat`
con texto antes de prometer nada. Ver [[clics-que-no-llegan-y-loops]],
[[atas-chartarea-mas-alto]] y [[produccion-en-vivo-2026-09-06]].
