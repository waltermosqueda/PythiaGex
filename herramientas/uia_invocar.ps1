# uia_invocar.ps1 — invoca por UI Automation el elemento de ATAS con ese nombre (los clics del lado derecho no llegan; memoria
# atas-dialogo-indicadores-clics). -MinX/-MaxX filtran por posicion en pantalla (por ejemplo, el boton "Indicators" del grafico derecho).
# -Listar muestra los candidatos sin tocar nada. Uso: powershell -File herramientas/uia_invocar.ps1 -Nombre "Indicators" -MinX 900
param([string]$Nombre, [double]$MinX = -1e9, [double]$MaxX = 1e9, [switch]$Listar, [switch]$Contiene)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$A = [System.Windows.Automation.AutomationElement]
$root = $A::RootElement
$p = Get-Process OFT.Platform -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) { "ATAS no corre"; exit 1 }
$cond = New-Object System.Windows.Automation.PropertyCondition($A::ProcessIdProperty, $p.Id)
$wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
$cands = @()
foreach ($w in $wins) {
    $todos = $w.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($e in $todos) {
        try { $n = $e.Current.Name } catch { continue }
        if (-not $n) { continue }
        $ok = if ($Contiene) { $n -like "*$Nombre*" } else { $n -eq $Nombre }
        if (-not $ok) { continue }
        $r = $e.Current.BoundingRectangle
        if ($r.Width -le 0 -or $r.X -lt $MinX -or $r.X -gt $MaxX) { continue }
        $cands += ,@($e, $r, $e.Current.ControlType.ProgrammaticName, $n)
    }
}
if ($Listar) { foreach ($c in $cands) { "{0} | {1} | x={2:0} y={3:0} w={4:0} h={5:0}" -f $c[3], $c[2], $c[1].X, $c[1].Y, $c[1].Width, $c[1].Height }; "candidatos: $($cands.Count)"; exit 0 }
foreach ($c in $cands) {
    $e = $c[0]
    foreach ($t in @($e, [System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($e))) {
        try { $t.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); "INVOCADO '$($c[3])' ($($c[2])) x=$([int]$c[1].X) y=$([int]$c[1].Y)"; exit 0 } catch {}
        try { $t.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); "SELECCIONADO '$($c[3])' ($($c[2])) x=$([int]$c[1].X) y=$([int]$c[1].Y)"; exit 0 } catch {}
        try { $t.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle(); "TOGGLE '$($c[3])' ($($c[2]))"; exit 0 } catch {}
        try { $t.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand(); "EXPANDIDO '$($c[3])' ($($c[2]))"; exit 0 } catch {}
    }
}
"NO PUDE invocar '$Nombre' (candidatos $($cands.Count))"
