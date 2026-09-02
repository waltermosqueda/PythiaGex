---
name: compilar-indicadores-atas
description: "Cómo compilar e instalar un indicador propio en ATAS: SDK, referencias, la API real por reflexión y el paso de 'Add to chart' que no es obvio."
metadata: 
  node_type: memory
  type: project
  modified: 2026-08-31T06:25:20.729Z
  originSessionId: a021c566-b4fe-42c5-acd1-0a01a165a079
---

Hecho y verificado el 2026-08-31. El indicador **PythiaGex - Niveles Gamma** quedó compilado, instalado y corriendo sobre el gráfico de MESU6.

## Lo que hay en la máquina

ATAS 8.0.14.397 en `C:\Program Files (x86)\ATAS Platform`, apunta a **net10.0-windows**. Trae el *runtime* de .NET 10 pero **no el SDK**: se instaló con `winget install Microsoft.DotNet.SDK.10`. Los DLL de usuario van a `%APPDATA%\ATAS\Indicators\`.

## El proyecto

`TargetFramework net10.0-windows`, `UseWPF true`. Referencias a `ATAS.Indicators`, `ATAS.DataFeedsCore`, `ATAS.Types`, `OFT.Rendering`, `OFT.Attributes`, `OFT.Localization`, `OFT.Core` — **todas con `<Private>false</Private>`**. Si se copian, ATAS carga dos veces los mismos tipos y no lo reconoce.

## No adivinar la API: leerla

Está en `PythiaGex/atas/_api` — un programa chico que carga los ensamblados con `AssemblyLoadContext` y vuelca tipos y miembros por reflexión. Ahorró todas las conjeturas. Lo esencial:

- clase base `ATAS.Indicators.Indicator`, se hereda y se sobreescribe `OnCalculate`, `OnRender(RenderContext, DrawingLayouts)`, `OnInitialize`, `OnDispose`
- en el constructor: `EnableCustomDrawing = true` y `SubscribeToDrawingEvents(DrawingLayouts.Final)`
- precio a pixel: `ChartInfo.PriceChartContainer.GetYByPrice(decimal, bool)`; el rango visible sale de `.High` y `.Low`
- dibujo: `RenderContext.DrawLine / DrawString / FillRectangle / MeasureString`, con `RenderPen(Color, float, DashStyle)` y `RenderFont(string, float)`
- refresco periódico: `SubscribeToTimer(TimeSpan, Action)` y `RedrawChart(new RedrawArg(ChartArea))`
- los ajustes se declaran con `[Display(Name=..., GroupName=..., Order=...)]` de `System.ComponentModel.DataAnnotations`

**Trampa de tipos:** `AddAlert` usa el `Color` de `System.Windows.Media`; el dibujo usa el de `System.Drawing`. Mismo nombre, dos tipos. Hay que convertir. Lo mismo en las series: `ValueDataSeries.Color` y `RangeDataSeries.RangeColor` son **Media**, mientras que `RenderColor` es **Drawing**.

**El eje de precios se dibuja ENCIMA de `ChartArea`**, así que `area.Right` no es el borde visible: mide unos **62 px** de más. Una etiqueta alineada a la derecha queda cortada por el eje y parece que le falta texto. Hay que restar ese margen y dejarlo configurable.

**Las propiedades de cada serie tambien las guarda el workspace, y ese guardado le gana al default del codigo** — igual que con las propiedades del indicador. Pasó con `ShowCurrentValue`: ATAS dibujaba el valor de la serie en el eje de precios, duplicando la etiqueta propia. Cambiar el default a `false` en el código no alcanzó para la instancia ya agregada; hay que destildar **"Show value"** a mano en `Ctrl+I` → indicador → sección *Drawing* → expandir la serie. Cambiar el `Id` de la serie sí hace que las nuevas tomen el default.

**ATAS se traga las excepciones de los indicadores sin dejar nada en su log.** Un fallo se ve solo como un indicador que no dibuja. Conviene envolver `OnCalculate` y `OnRender` en try/catch que escriba a un archivo propio.

Usar `InstrumentInfo.Instrument` y `InstrumentInfo.TickSize`, no `Instrument` ni `TickSize` sueltos: están obsoletos.

## Instalarlo (el paso que costó)

1. Copiar el DLL a `%APPDATA%\ATAS\Indicators\`. **El archivo NO queda bloqueado aunque ATAS esté abierto**: se puede pisar en caliente.
2. **Reiniciar ATAS.** En el log aparece `Indicators: Created library '...dll'`.

   **CORREGIDO el 2026-09-01:** decía que no lo toma en caliente. En realidad ATAS **detecta el cambio** y avisa: `Changed library '...dll'` seguido de la notificación *"Some indicator libraries have been changed. You can reload indicators in the status bar of the main window."* O sea que existe una recarga en caliente. **Todavía no encontré el botón** — no está en el ícono de la derecha de la barra de estado, que es "Open changelog". Vale la pena buscarlo bien: ahorraría los 4-5 minutos de cada reinicio.
3. Gráfico → `Indicators` → buscarlo por nombre.
4. Seleccionarlo con **un solo clic**. Ahí el botón de abajo a la derecha cambia de `Apply` a **`Add to chart`**.
5. Apretar `Add to chart` — recién ahí sube el contador de `Added (N)`. **Doble clic no alcanza, arrastrarlo tampoco.**
6. Apretar `Apply`.
7. `Workspaces` → `Save` → `Yes`, si no se pierde al reiniciar.

Si el diálogo está tan abajo que no se ven los botones, arrastrar la barra de título hacia arriba primero.

## Diagnóstico cuando no aparece

`%APPDATA%\ATAS\Logs\app_YYYYMMDD.log`. Si dice `Created library` el DLL está bien y el problema es la interacción, no el código. Si el panel de ajustes de la derecha muestra las propiedades, el constructor corrió sin excepción.

**Why:** es la única forma de tener el precio en vivo de Rithmic y los niveles auditados en una sola pantalla, sin pagar un feed de opciones aparte.

**How to apply:** el fuente está en `PythiaGex/atas/PythiaGexNiveles`. Recompilar con `dotnet build -c Release`, cerrar ATAS, copiar el DLL, reabrir. Ver [[navegar-atas-sin-pedir-permiso]] y [[atas-opciones-es]].
