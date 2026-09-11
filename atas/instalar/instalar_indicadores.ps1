# Instala los indicadores de PythiaGex en un ATAS recien instalado (o los actualiza).
#
# QUE HACE: copia PythiaGexNiveles.dll (Gamma Hoy, Gamma Vivo, Niveles, Radar) y
# PythiaVwap.dll (VWAP anclado, nodos de volumen, flujo) a la carpeta de indicadores
# de ATAS y crea la carpeta de datos del indicador. Nada mas: no toca credenciales,
# no toca la conexion, no toca el workspace.
#
# COMO SE USA (cualquiera de las dos):
#   a) desde el zip bajado de GitHub (Releases): descomprimir y doble clic en
#      instalar_indicadores.bat (o correr este .ps1). Los DLL vienen al lado.
#   b) desde el repositorio clonado: correr este .ps1 desde atas\instalar; los DLL
#      se buscan en atas\PythiaGexNiveles\bin\Release y atas\PythiaVwap\bin\Release.
#
# DESPUES, EN ATAS (una sola vez por grafico): Indicators -> buscar "Gamma Hoy" ->
# un clic en la fila -> "Add to chart" -> "Apply" -> Workspaces -> Save.
# Con la version de ATAS que trajo la DLL (8.0.14.399) carga directo; si ATAS es
# mas nuevo y el DLL no aparece en la lista, hay que recompilar (ver README.md).

$ErrorActionPreference = "Stop"
$aqui = Split-Path -Parent $MyInvocation.MyCommand.Path
$destino = Join-Path $env:APPDATA "ATAS\Indicators"
$datos = Join-Path $env:APPDATA "ATAS\PythiaGex"

function Buscar($nombre, $relativo) {
    $cands = @((Join-Path $aqui $nombre), (Join-Path $aqui $relativo))
    foreach ($c in $cands) { if (Test-Path $c) { return (Resolve-Path $c).Path } }
    return $null
}

$dlls = @(
    @{ n = "PythiaGexNiveles.dll"; r = "..\PythiaGexNiveles\bin\Release\PythiaGexNiveles.dll" },
    @{ n = "PythiaVwap.dll";       r = "..\PythiaVwap\bin\Release\PythiaVwap.dll" }
)

if (-not (Test-Path $destino)) { New-Item -ItemType Directory -Force $destino | Out-Null; Write-Output "creada $destino (ATAS todavia no la tenia: abrilo una vez antes si algo falla)" }
if (-not (Test-Path $datos)) { New-Item -ItemType Directory -Force $datos | Out-Null }

$ok = 0
foreach ($d in $dlls) {
    $origen = Buscar $d.n $d.r
    if (-not $origen) { Write-Output ("FALTA {0}: no esta ni al lado del script ni en {1}" -f $d.n, $d.r); continue }
    $atas = Get-Process OFT.Platform -ErrorAction SilentlyContinue
    Copy-Item $origen (Join-Path $destino $d.n) -Force
    $v = (Get-Item (Join-Path $destino $d.n))
    Write-Output ("instalado {0}  ({1:N0} bytes, {2})" -f $d.n, $v.Length, $v.LastWriteTime)
    $ok++
}

if ($ok -eq 0) { Write-Output "no se instalo nada"; exit 1 }
if (Get-Process OFT.Platform -ErrorAction SilentlyContinue) {
    Write-Output "ATAS esta abierto: avisa 'Some indicator libraries have been changed'. Cerralo y abrilo de nuevo para que los tome."
} else {
    Write-Output "Listo. Abri ATAS: Indicators -> 'Gamma Hoy' -> un clic -> Add to chart -> Apply -> Workspaces -> Save."
}
