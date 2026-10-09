# RESPALDO A LA CARPETA SINCRONIZADA (Escritorio\Inversiones\PythiaGex-respaldo; Google Drive para escritorio la sube).
#
# Copia espejo (robocopy /MIR) SOLO dentro de PythiaGex-respaldo. Nunca toca nada fuera de esa carpeta.
#   - PythiaGex\                el repo publico entero con lo no versionado (codigo, laboratorio, datos), sin
#                               bin/obj/__pycache__ ni las carpetas de cache (cache, *_cache: se regeneran o se
#                               vuelven a bajar, ver .gitignore)
#   - PythiaGex\.git\           la historia del repo publico, COMPLETA (sin tope: un .git al que le falta un pack
#                               no sirve para nada; 09-10: el tope de 20 MB dejo afuera un pack de 206 MB y el .git
#                               del espejo quedo roto)
#   - PythiaGex-privado\        el repo privado entero, COMPLETO y sin tope (workspace, plantillas, DLL instaladas,
#                               datos de la 4 con la cinta grande, fuente completo de la 4 con capturas, memorias;
#                               con su .git). Sus archivos ya vienen con tope de 90 MB desde respaldar_privado.ps1.
#   - RESTAURAR_PythiaGex4.md   el instructivo para una PC nueva, a la vista
#   - ATAS-nada-raiz\           los archivos sueltos de Escritorio\ATAS nada (CLAUDE.md, etc.)
#   - appdata-ATAS-archivos\    los logs y archivos sueltos pythiagex* de %APPDATA%\ATAS (clasica, 2.0, 3.0 y 4)
#   - memoria-claude\           las memorias de Claude de este proyecto
#   - appdata-ATAS-PythiaGex3\  datos de la 3.0
#   - con -Completo, ademas:    appdata-ATAS-PythiaGex\ (la clasica: cinta, cadenas locales, viva; ~10 GB) y
#                               appdata-ATAS-PythiaGex2\ (la 2.0)
# (los datos de la 4, %APPDATA%\ATAS\PythiaGex4, van adentro de PythiaGex-privado\appdata\PythiaGex4)
#
# TOPE POR ARCHIVO (salvo los dos .git y el repo privado, que van completos): sin -Completo, 20 MB (liviano: lo que
# hace falta para restaurar y el proyecto, sin los crudos grandes); con -Completo, 90 MB. Lo que pasa el tope queda en
# el disco y no se sube.
# OJO: Google Drive sube todo lo nuevo apenas aparece y puede ocupar la subida de internet un buen rato. Con el mercado
# abierto, eso puede cortar Rithmic (16-09: las rafagas de red coincidian con cortes). La primera corrida con los .git
# completos suma ~300 MB a la subida: mejor con el mercado cerrado. Las siguientes solo suben lo que cambio.
# Al final controla que los dos repos del espejo esten sanos (git fsck) y que el privado coincida con su ultimo commit.
# Idempotente: correrlo de nuevo solo copia lo que cambio.
#   powershell -ExecutionPolicy Bypass -File herramientas\respaldo_inversiones.ps1 [-Completo] [-Probar]
#   -Probar: robocopy /L (solo lista, no copia ni borra nada) y salta el control final.
param([switch]$Completo, [switch]$Probar)
$ErrorActionPreference = "Continue"
$destino = Join-Path $env:USERPROFILE "OneDrive\Escritorio\Inversiones\PythiaGex-respaldo"
$raiz    = Join-Path $env:USERPROFILE "OneDrive\Escritorio\ATAS nada"
$appdata = Join-Path $env:APPDATA "ATAS"
$mem     = Join-Path $env:USERPROFILE ".claude\projects\C--Users-wmx-7-OneDrive-Escritorio-ATAS-nada\memory"
$tope    = if ($Completo) { "/MAX:94371840" } else { "/MAX:20971520" }
# desktop.ini: lo crea Google Drive en cada carpeta del espejo; excluirlo evita que /MIR lo borre en cada corrida.
# ADENTRO de los .git, en cambio, se borra (switch -Git): git lee .git\refs\desktop.ini como una rama rota y fsck falla.
$nunca   = @("/XF", "github.token", "*.token", "Connectors*.cnf", "desktop.ini")
$nuncaGit = @("/XF", "github.token", "*.token", "Connectors*.cnf")
$listar  = if ($Probar) { @("/L") } else { @() }
New-Item -ItemType Directory -Force -Path $destino | Out-Null

function Espejo($origen, $sub, $extras, [switch]$SinTope, [switch]$Git) {
    if (-not (Test-Path $origen)) { Write-Output "  no existe $origen (se salta)"; return }
    $d = Join-Path $destino $sub          # siempre una subcarpeta de PythiaGex-respaldo
    $t = if ($SinTope) { @() } else { @($tope) }
    $x = if ($Git) { $nuncaGit } else { $nunca }
    $a = @($origen, $d, "/MIR", "/R:1", "/W:1", "/NFL", "/NDL", "/NJH", "/NJS", "/NP") + $listar + $t + $x + $extras
    & robocopy @a | Out-Null
    $rc = $LASTEXITCODE
    Write-Output ("  {0} -> {1}{2}  (robocopy {3}{4})" -f $origen, $sub, $(if ($SinTope) { " [completo]" } else { "" }), $rc, $(if ($rc -ge 8) { " ERROR" } else { "" }))
}

# los dos repos: primero el arbol de trabajo SIN su .git (excluido por ruta: tampoco se purga en el destino), despues
# el .git entero, sin tope y sin los desktop.ini de Drive. El publico con tope por archivo; el privado completo.
Espejo "$raiz\PythiaGex"                 "PythiaGex"              @("/XD", "bin", "obj", "__pycache__", "cache", "*_cache", "$raiz\PythiaGex\.git")
Espejo "$raiz\PythiaGex\.git"            "PythiaGex\.git"         @() -SinTope -Git
Espejo "$raiz\PythiaGex-privado"         "PythiaGex-privado"      @("/XD", "$raiz\PythiaGex-privado\.git") -SinTope
Espejo "$raiz\PythiaGex-privado\.git"    "PythiaGex-privado\.git" @() -SinTope -Git
Espejo $mem                      "memoria-claude"    @()
Espejo "$appdata\PythiaGex3"     "appdata-ATAS-PythiaGex3" @()
if ($Completo) {
    Espejo "$appdata\PythiaGex"  "appdata-ATAS-PythiaGex"  @()
    Espejo "$appdata\PythiaGex2" "appdata-ATAS-PythiaGex2" @()
}
# archivos sueltos (sin /MIR: solo agrega o actualiza)
& robocopy "$raiz" "$destino\ATAS-nada-raiz" /R:1 /W:1 /NFL /NDL /NJH /NJS /NP /LEV:1 @listar /XF "*.token" | Out-Null
& robocopy "$appdata" "$destino\appdata-ATAS-archivos" pythiagex*.* /R:1 /W:1 /NFL /NDL /NJH /NJS /NP /LEV:1 @listar $tope | Out-Null
$rest = Join-Path $raiz "PythiaGex\atas\instalar\RESTAURAR_PythiaGex4.md"
if ((Test-Path $rest) -and -not $Probar) { Copy-Item $rest (Join-Path $destino "RESTAURAR_PythiaGex4.md") -Force }

if ($Probar) { Write-Output "  modo -Probar: no se copio ni se borro nada"; exit 0 }

# control final: los dos repos del espejo tienen que poder leerse enteros
# (si Drive volvio a meter un desktop.ini en un .git entre la copia y el control, ese renglon de fsck no cuenta)
$avisos = @()
function Fsck($repo, $nombre) {
    $o = & git -C $repo fsck --connectivity-only 2>&1 | ForEach-Object { "$_" }
    if ($LASTEXITCODE -eq 0) { return }
    $malas = @($o | Where-Object { $_ -match '^(error|fatal|missing|broken|bad)' -and $_ -notmatch 'desktop\.ini' })
    if ($malas.Count -gt 0) { $script:avisos += ("{0}\.git NO esta sano (git fsck): {1}" -f $nombre, (($malas | Select-Object -First 3) -join " | ")) }
}
$priv = Join-Path $destino "PythiaGex-privado"
Fsck (Join-Path $destino "PythiaGex") "PythiaGex"
Fsck $priv "PythiaGex-privado"
$st = & git -C $priv status --porcelain --untracked-files=no 2>&1   # sin los desktop.ini de Drive
if ($LASTEXITCODE -ne 0 -or $st) { $avisos += ("PythiaGex-privado del espejo no coincide con su ultimo commit ({0} renglones de git status; el primero: {1})" -f @($st).Count, @($st)[0]) }
if ($avisos) { $avisos | ForEach-Object { Write-Output "  AVISO: $_" } }
else { Write-Output "  control: los dos .git del espejo sanos (fsck) y el privado igual a su ultimo commit" }

$tam = (Get-ChildItem $destino -Recurse -File -Force -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum / 1MB
"{0}  respaldo{1} en {2}: {3:N0} MB{4}" -f (Get-Date -Format "yyyy-MM-dd HH:mm"), $(if ($Completo) { " completo" } else { " liviano" }), $destino, $tam, $(if ($avisos) { "  CON AVISOS" } else { "" }) | Tee-Object -FilePath (Join-Path $destino "ultimo-respaldo.txt") -Append
