# Restaurar PythiaGex 4 en una PC nueva (o en un ATAS recién instalado)

Pensalo como mudarte de casa: los muebles (ATAS y Rithmic) los ponés vos, y nosotros traemos lo que va adentro
(el indicador, tus gráficos y la historia que fue juntando). Son pocos pasos y van de a uno.

Indicador: **PythiaGex 4.0 - Gamma Familia**, versión **4.1.4** (el archivo se llama `PythiaGexCuatro.dll`).
Huella de la DLL 4.1.4 que estaba instalada el 09-10-2026 (sirve para confirmar que copiaste la correcta):
`SHA256 7DB5F0F8782C4D040114DD8656395DEC217CF948D5B0D0CA3639BAD07898E262`, 772.096 bytes.

## Dónde está guardado cada cosa

Hay tres copias. Con cualquiera de las dos últimas alcanza para todo.

1. **Repositorio público** `github.com/waltermosqueda/PythiaGex`: el código fuente de la 4 (`atas/PythiaGexCuatro`),
   los arneses de prueba, las herramientas y el conocimiento. **No** tiene la DLL de la 4 ni tres archivos del código
   que nombran a un tablero externo (por la regla de "la referencia"): esos están en las otras dos copias. Por eso
   **desde el público solo no se puede compilar** (da el error CS2001: faltan 3 archivos).
2. **Repositorio privado** `github.com/waltermosqueda/PythiaGex-privado` (solo lo ves vos):
   - `atas/instalar/PythiaGexCuatro.dll` → la DLL lista para copiar, sin compilar nada.
   - `atas/Indicators/` → todas las DLL que tenías instaladas (la 4, la 3.0, la 2.0, la clásica, VWAP y las demás).
   - `atas/Workspaces_v3/` → tus espacios de trabajo: los gráficos, las pestañas y las casillas de cada indicador.
   - `atas/Chart/...`, `atas/IndicatorTemplates/`... → tus plantillas.
   - `appdata/PythiaGex4/` → la historia que fue guardando la 4 (cadenas de CBOE, libro de NQ, cinta, estela, niveles).
   - `proyecto/atas/PythiaGexCuatro/` → el código fuente COMPLETO de la 4, con las capturas de pantalla.
3. **Carpeta `Escritorio\Inversiones\PythiaGex-respaldo`** (Google Drive la sube sola a la nube): un espejo de todo lo
   anterior. Adentro, `PythiaGex-privado\` es el repositorio privado y `PythiaGex\` el proyecto entero (este sí
   trae los 3 archivos que faltan en el público).

Lo que **no está en ningún lado, a propósito**: tu usuario y clave de Rithmic/Lucid, el token de GitHub y los datos de
X-Ray (Windows los cifra para esa PC; se vuelven a activar con tu licencia).

## Lo que necesitás antes

- Windows 10 u 11.
- **ATAS 8.0.15 o más nuevo** (la 4.1.4 se probó en 8.0.15.303) con tu licencia Ultra y Rithmic con opciones de CME.
- Internet: el indicador baja solo las cadenas de NDX, QQQ y TQQQ de CBOE.
- Solo si vas a compilar: **.NET SDK 10** (se usó la 10.0.400). Se instala con `winget install Microsoft.DotNet.SDK.10`.

## Paso 0 — Traer la copia a la PC nueva

Es como ir a buscar las cajas de la mudanza al depósito: primero tienen que estar en la casa nueva.

- **Desde GitHub:** entrá a github.com con tu usuario → repositorio **PythiaGex-privado** → botón verde **Code** →
  **Download ZIP**. Descomprimilo en una carpeta **corta**, por ejemplo `C:\PythiaGex-privado` (si queda adentro de
  muchas carpetas, Windows corta los nombres largos y faltan archivos).
- **O desde Google Drive:** entrá a drive.google.com y bajá la carpeta `Inversiones\PythiaGex-respaldo`. Usá la
  subcarpeta `PythiaGex-privado` de adentro como si fuera `C:\PythiaGex-privado`. (Drive agrega archivos
  `desktop.ini` en cada carpeta: para restaurar no molestan. Solo si después usás git ahí adentro y se queja de
  `desktop.ini`, borrá los que quedaron dentro de la carpeta `.git`.)
- Si usás `git clone` en vez del ZIP, antes corré `git config --global core.longpaths true`.

**Cómo abrir PowerShell** (lo vas a usar en los pasos que siguen): tecla Windows, escribís `PowerShell` y Enter.
Los comandos de abajo se pegan ahí tal cual, de a uno, y se confirman con Enter. Todos suponen que la copia quedó en
`C:\PythiaGex-privado`; si la pusiste en otro lado, cambiá esa parte.

## Paso 1 — ATAS y Rithmic (esto lo hacés vos)

Instalá ATAS, abrilo, conectá Rithmic con tu usuario y clave como siempre, y **cerralo**. Abrirlo una vez crea la
carpeta `%APPDATA%\ATAS\Indicators`, que es donde van los indicadores.

## Paso 2 — Poner la DLL (elegí una de las dos)

**A) Sin compilar (lo más fácil).** Con ATAS **cerrado**, en PowerShell:

```
Copy-Item "C:\PythiaGex-privado\atas\instalar\PythiaGexCuatro.dll" "$env:APPDATA\ATAS\Indicators\"
Get-FileHash "$env:APPDATA\ATAS\Indicators\PythiaGexCuatro.dll"
```

La segunda línea tiene que dar el SHA256 de arriba (7DB5F0F8…E262).

**B) Compilando (si ATAS es mucho más nuevo y la DLL no aparece en la lista).** Usá la copia completa del código:
`proyecto\atas\PythiaGexCuatro` del repositorio privado (o `Inversiones\PythiaGex-respaldo\PythiaGex\atas\PythiaGexCuatro`).
**No compiles desde el repositorio público**: le faltan 3 archivos y falla con CS2001.

Después de instalar el SDK, **cerrá PowerShell y abrilo de nuevo** (si no, no encuentra `dotnet`). Entrá a la carpeta
y compilá:

```
cd "C:\PythiaGex-privado\proyecto\atas\PythiaGexCuatro"
dotnet build -c Release -o bin/Release
```

El proyecto busca las piezas de ATAS en `C:\Program Files (x86)\ATAS Platform\`, así que ATAS tiene que estar
instalado en la ruta de siempre. La DLL nueva queda en `bin\Release\PythiaGexCuatro.dll`; con ATAS cerrado, copiala:

```
Copy-Item ".\bin\Release\PythiaGexCuatro.dll" "$env:APPDATA\ATAS\Indicators\"
```

**Ojo: la huella de una DLL compilada por vos NO coincide con la de arriba**, porque lleva adentro el número de
commit. No es que compilaste mal. Para controlarla: clic derecho en la DLL → **Propiedades** → **Detalles** →
**Versión del archivo** tiene que decir **4.1.4.0**. Si la querés idéntica byte a byte, compilá así:

```
dotnet build -c Release -o bin/Release -p:SourceRevisionId=9e86b1956b428e1043c306f10cb3d459cd049087
```

Eso da exactamente 7DB5F0F8…E262 (verificado el 09-10-2026 con el SDK 10.0.400, en otra carpeta).

## Paso 3 — La historia (opcional, pero recomendado)

Sin esto la 4 anda igual, pero arranca "de cero": la base de NDX de noche, la razón de QQQ y TQQQ necesitan días
previos. Con ATAS **cerrado**, en PowerShell:

```
robocopy "C:\PythiaGex-privado\appdata\PythiaGex4" "$env:APPDATA\ATAS\PythiaGex4" /E
```

(robocopy copia el **contenido** de la primera carpeta adentro de la segunda; no crea una carpeta dentro de otra.)
Si te falta algún archivo `cinta-NQ-*.csv` grande, también está en el repositorio privado de GitHub.

## Paso 4 — Tus gráficos tal cual estaban (opcional)

Con ATAS **cerrado**, en PowerShell, de a una línea:

```
robocopy "C:\PythiaGex-privado\atas\Workspaces_v3" "$env:APPDATA\ATAS\Workspaces_v3" /E
robocopy "C:\PythiaGex-privado\atas\Chart\Templates" "$env:APPDATA\ATAS\Chart\Templates" /E
robocopy "C:\PythiaGex-privado\atas\Chart\UnifiedTemplates" "$env:APPDATA\ATAS\Chart\UnifiedTemplates" /E
robocopy "C:\PythiaGex-privado\atas\IndicatorTemplates" "$env:APPDATA\ATAS\IndicatorTemplates" /E
robocopy "C:\PythiaGex-privado\atas\ClusterTemplates" "$env:APPDATA\ATAS\ClusterTemplates" /E
```

**El espacio usa más indicadores que la 4** (la clásica, la 2.0 y el VWAP). Copiá TODAS las DLL, no solo la 4; si no,
ATAS abre el espacio con indicadores sin su DLL y al guardar se pueden perder:

```
Copy-Item "C:\PythiaGex-privado\atas\Indicators\*.dll" "$env:APPDATA\ATAS\Indicators\"
```

Opcional, la historia de la clásica:
`robocopy "C:\PythiaGex-privado\appdata\PythiaGex" "$env:APPDATA\ATAS\PythiaGex" /E`

**Si tu usuario de Windows no es `wmx_7`** (en una PC nueva casi seguro cambia), corré esto ANTES de abrir ATAS. El
espacio guarda dos rutas con el usuario viejo (la carpeta de la cinta y la del estado de la 4) y en otro usuario la 4
no podría escribir ahí:

```
$yo = Split-Path $env:USERPROFILE -Leaf
Get-ChildItem "$env:APPDATA\ATAS\Workspaces_v3\*.ws" | ForEach-Object { $t = [IO.File]::ReadAllText($_.FullName); if ($t.Contains("wmx_7")) { [IO.File]::WriteAllText($_.FullName, $t.Replace("wmx_7", $yo)) } }
```

(También se puede a mano, ya con ATAS abierto: Indicators → PythiaGex 4.0 → "9.6 3.0 · Cinta" → "Cinta: carpeta", y
"9.5 3.0 · Profundidad" → "Ruta del estado (indicador.json)"; después Apply y Workspaces → Save.)

Abrí ATAS y elegí el espacio **"MNQ liviano"**, **no** "Default workspace": ese trae el DOM pesado (MBO) que
tildaba ATAS. "MNQ liviano" ya tiene la 4 puesta con tus casillas: si hacés este paso, el paso 5 ya está hecho.

## Paso 5 — Agregar el indicador al gráfico (si no hiciste el paso 4)

1. Abrí ATAS y un gráfico de **MNQ** (el contrato vigente, por ejemplo MNQZ6) de **1 minuto**.
2. Botón **Indicators** del gráfico.
3. Buscá **Gamma Familia** (está en el grupo de los indicadores propios, "PythiaGex 4.0").
4. Un clic en la fila **PythiaGex 4.0 - Gamma Familia** → **Add to chart** → **Apply**.
5. Guardá: **Workspaces** → **Save**.

## Cómo saber que anda

- En el gráfico aparecen las rayitas y, arriba, la pestaña desplegable (arranca cerrada) con las fuentes y la edad de
  cada dato. Si dice "DATO DE HACE" es que el dato tiene más de 30 minutos.
- Los registros quedan en `%APPDATA%\ATAS\pythiagex4-*.log`. En `pythiagex4-gammahoy.log` buscá la línea
  `capa NDX: ... base` para ver de dónde sale la conversión de noche.
- Sin internet, NQ sigue andando y la pestaña avisa en naranja qué falta.

## Recordatorio honesto

El indicador describe dónde están los niveles; **no** anticipa la dirección. No hay una estrategia validada.
