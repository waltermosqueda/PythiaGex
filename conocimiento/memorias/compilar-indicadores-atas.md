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

**Trampa de tipos:** `AddAlert` usa el `Color` de `System.Windows.Media`; el dibujo usa el de `System.Drawing`. Mismo nombre, dos tipos. Hay que convertir.

Usar `InstrumentInfo.Instrument` y `InstrumentInfo.TickSize`, no `Instrument` ni `TickSize` sueltos: están obsoletos.

## Instalarlo (el paso que costó)

1. Copiar el DLL a `%APPDATA%\ATAS\Indicators\`.
2. **Reiniciar ATAS.** No lo toma en caliente. En el log aparece `Indicators: Created library '...dll'`.
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
