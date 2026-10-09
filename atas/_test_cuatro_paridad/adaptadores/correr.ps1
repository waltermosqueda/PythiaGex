# correr.ps1 — arnes de B1 (adaptadores de la 4.1). Compila y corre a prioridad BelowNormal (ATAS corre con el mercado abierto).
# Antes, si falta ref_adaptadores.json: python -I ref_adaptadores.py (tambien BelowNormal, solo lectura).
$ErrorActionPreference = "Stop"
$aqui = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not (Test-Path (Join-Path $aqui "ref_adaptadores.json"))) { & python -I (Join-Path $aqui "ref_adaptadores.py") }
$log = Join-Path $aqui "build.log"
$b = Start-Process -FilePath "dotnet" -ArgumentList @("build", "`"$(Join-Path $aqui 'adaptadores.csproj')`"", "-c", "Release", "-o", "`"$(Join-Path $aqui 'bin')`"", "-nologo", "-v:q", "-m:1", "/nodeReuse:false", "-p:UseSharedCompilation=false") -NoNewWindow -PassThru -RedirectStandardOutput $log
try { $b.PriorityClass = "BelowNormal" } catch {}
$b.WaitForExit()
Get-Content $log | Select-String "error|Error" | ForEach-Object { $_.Line }
$exe = Join-Path $aqui "bin\adaptadores.exe"
$out = Join-Path $aqui "resultado.txt"
$p = Start-Process -FilePath $exe -ArgumentList "`"$aqui`"" -NoNewWindow -PassThru -RedirectStandardOutput $out
try { $p.PriorityClass = "BelowNormal" } catch {}
$p.WaitForExit()
Get-Content $out
