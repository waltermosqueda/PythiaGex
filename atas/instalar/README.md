# Recuperar los indicadores en una PC nueva (o en un ATAS recien instalado)

Todo el trabajo vive en dos lugares de la nube, y con eso alcanza para volver a
tener los indicadores andando en unos minutos:

1. **Este repositorio publico** (`github.com/waltermosqueda/PythiaGex`): el codigo
   fuente de los dos indicadores, los DLL ya compilados, el nucleo de calculo, el
   laboratorio, el panel web, el archivo de cadenas (rama `cadenas`) y el
   conocimiento (memorias, bitacora, guias). No tiene claves ni cuentas.
2. **El repositorio privado** (`github.com/waltermosqueda/PythiaGex-privado`): el
   workspace de ATAS con los graficos y los ajustes de cada indicador, las plantillas,
   las grabaciones propias de la cadena viva de Rithmic y las memorias. Se actualiza
   con `respaldar_privado.ps1` (esta en la carpeta `PythiaGex-privado` del escritorio).

## Camino rapido (5 minutos): solo los indicadores

1. Instalar ATAS y conectarlo a Rithmic como siempre (eso no se puede automatizar:
   son tus credenciales).
2. Bajar el repositorio entero como zip: en `github.com/waltermosqueda/PythiaGex`,
   boton verde `Code` -> `Download ZIP`, y descomprimirlo. (Si hay una **Release**
   `indicadores-AAAA-MM-DD.zip`, sirve igual y es mas chica.)
3. Doble clic en `atas\instalar\instalar_indicadores.bat`. Busca los DLL al lado del
   script o en `atas\PythiaGexNiveles\bin\Release` y `atas\PythiaVwap\bin\Release`
   y los copia a `%APPDATA%\ATAS\Indicators\`.
4. Abrir ATAS, en cada grafico: `Indicators` -> buscar `Gamma Hoy` -> un clic en la
   fila -> `Add to chart` -> `Apply`. Lo mismo con `PythiaVwap`. Despues
   `Workspaces` -> `Save`.

Gamma Hoy baja solo el feed y el archivo de cadenas desde GitHub: no hace falta
copiar datos. La cadena viva de Rithmic arranca sola si la conexion esta.

## Camino completo (15 minutos): los graficos como los tenias

Ademas del camino rapido:

1. Clonar el repo privado (o bajarlo como zip) y copiar `atas\Workspaces_v3` a
   `%APPDATA%\ATAS\Workspaces_v3` **con ATAS cerrado**. Eso trae los graficos, las
   pestañas y los ajustes de cada indicador tal cual estaban.
2. Copiar `atas\Chart\Templates`, `atas\Chart\UnifiedTemplates`,
   `atas\IndicatorTemplates` y `atas\ClusterTemplates` a las mismas rutas dentro de
   `%APPDATA%\ATAS\`.
3. Copiar `appdata\PythiaGex` a `%APPDATA%\ATAS\PythiaGex` (las grabaciones de
   Rithmic y el contexto; sin esto el indicador funciona igual, solo pierde el
   pasado propio).
4. Opcional: el token de GitHub (`%APPDATA%\PythiaGex\github.token`) hace que el
   indicador lea el feed por la API sin el cache de 5 minutos. Se genera de nuevo en
   GitHub (Settings -> Developer settings -> tokens); no esta en ningun repo.

## Si ATAS es mas nuevo que la DLL

La DLL se compilo contra ATAS 8.0.14.399 y .NET 10. Suele cargar en versiones
posteriores; si no aparece en la lista de indicadores, recompilar:

```
winget install Microsoft.DotNet.SDK.10
cd PythiaGex\atas\PythiaGexNiveles
dotnet build -c Release
```

El csproj apunta a `C:\Program Files (x86)\ATAS Platform\*.dll`. El DLL nuevo queda
en `bin\Release\` y se instala con el mismo script. El puente a las opciones de
Rithmic (`PuenteRithmic.cs`) busca las piezas del conector por forma; si una
version futura las cambia, el log dice `FALTAN PIEZAS` y el indicador sigue con
CBOE.

## Lo que se pierde si se pierde la PC y no se corrio el respaldo privado

- El workspace y los ajustes de cada grafico posteriores al ultimo respaldo.
- Las grabaciones de la cadena viva de Rithmic posteriores al ultimo respaldo.
- El token de GitHub (se regenera).
- Las credenciales de Rithmic (nunca se guardan; se vuelven a escribir en ATAS).

Por eso `respaldar_privado.ps1` conviene correrlo al terminar cada sesion, como
`respaldar.py`.
