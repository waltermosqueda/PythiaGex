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
   que nombran a un tablero externo (por la regla de "la referencia"): esos están en las otras dos copias.
2. **Repositorio privado** `github.com/waltermosqueda/PythiaGex-privado` (solo lo ves vos):
   - `atas/instalar/PythiaGexCuatro.dll` → la DLL lista para copiar, sin compilar nada.
   - `atas/Indicators/` → todas las DLL que tenías instaladas (la 4, la 3.0, la 2.0, la clásica, VWAP y las demás).
   - `atas/Workspaces_v3/` → tus espacios de trabajo: los gráficos, las pestañas y las casillas de cada indicador.
   - `atas/Chart/...`, `atas/IndicatorTemplates/`... → tus plantillas.
   - `appdata/PythiaGex4/` → la historia que fue guardando la 4 (cadenas de CBOE, libro de NQ, cinta, estela, niveles).
   - `proyecto/atas/PythiaGexCuatro/` → el código fuente COMPLETO de la 4, con las capturas de pantalla.
3. **Carpeta `Escritorio\Inversiones\PythiaGex-respaldo`** (Google Drive la sube sola a la nube): un espejo de todo lo
   anterior. Adentro, `PythiaGex-privado\` es el repositorio privado y `PythiaGex\` el proyecto entero.

Lo que **no está en ningún lado, a propósito**: tu usuario y clave de Rithmic/Lucid, el token de GitHub y los datos de
X-Ray (Windows los cifra para esa PC; se vuelven a activar con tu licencia).

## Lo que necesitás antes

- Windows 10 u 11.
- **ATAS 8.0.15 o más nuevo** (la 4.1.4 se probó en 8.0.15.303) con tu licencia Ultra y Rithmic con opciones de CME.
- Internet: el indicador baja solo las cadenas de NDX, QQQ y TQQQ de CBOE.
- Solo si vas a compilar: **.NET SDK 10** (se usó la 10.0.400). Se instala con `winget install Microsoft.DotNet.SDK.10`.

## Paso 1 — ATAS y Rithmic (esto lo hacés vos)

Instalá ATAS, abrilo, conectá Rithmic con tu usuario y clave como siempre, y **cerralo**. Abrirlo una vez crea la
carpeta `%APPDATA%\ATAS\Indicators`, que es donde van los indicadores.

## Paso 2 — Poner la DLL (elegí una de las dos)

**A) Sin compilar (lo más fácil).** Con ATAS **cerrado**, copiá `PythiaGexCuatro.dll` desde el repositorio privado
(`atas/instalar/`) o desde `Inversiones\PythiaGex-respaldo\PythiaGex-privado\atas\instalar\` a:

```
%APPDATA%\ATAS\Indicators\
```

(Pegá esa ruta en la barra del Explorador de archivos y te lleva.) Para confirmar que es la buena, en PowerShell:

```
Get-FileHash "$env:APPDATA\ATAS\Indicators\PythiaGexCuatro.dll"
```

Tiene que dar el SHA256 de arriba.

**B) Compilando (si ATAS es mucho más nuevo y la DLL no aparece en la lista).** Usá la copia completa del código:
`proyecto\atas\PythiaGexCuatro` del repositorio privado (o `Inversiones\PythiaGex-respaldo\PythiaGex\atas\PythiaGexCuatro`).
En esa carpeta, en PowerShell:

```
dotnet build -c Release -o bin/Release
```

El proyecto busca las piezas de ATAS en `C:\Program Files (x86)\ATAS Platform\`, así que ATAS tiene que estar
instalado en la ruta de siempre. La DLL nueva queda en `bin\Release\PythiaGexCuatro.dll`: copiala como en A.

## Paso 3 — La historia (opcional, pero recomendado)

Sin esto la 4 anda igual, pero arranca "de cero": la base de NDX de noche, la razón de QQQ y TQQQ necesitan días
previos. Con ATAS **cerrado**, copiá la carpeta `appdata\PythiaGex4` del repositorio privado a:

```
%APPDATA%\ATAS\PythiaGex4
```

## Paso 4 — Tus gráficos tal cual estaban (opcional)

Con ATAS **cerrado**, copiá `atas\Workspaces_v3` del repositorio privado a `%APPDATA%\ATAS\Workspaces_v3`, y si querés
las plantillas, `atas\Chart\Templates`, `atas\Chart\UnifiedTemplates`, `atas\IndicatorTemplates` y
`atas\ClusterTemplates` a las mismas rutas dentro de `%APPDATA%\ATAS\`. Eso trae el espacio "MNQ liviano" con la 4 ya
puesta y tus casillas. Si hacés este paso, el paso 5 ya está hecho: solo abrí ATAS y elegí el espacio.

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
