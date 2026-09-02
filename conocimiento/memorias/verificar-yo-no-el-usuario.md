---
name: verificar-yo-no-el-usuario
description: Reiniciar ATAS y verificar en pantalla yo mismo; el usuario no es el QA del proyecto.
metadata: 
  node_type: memory
  type: feedback
  originSessionId: a021c566-b4fe-42c5-acd1-0a01a165a079
  modified: 2026-09-01T16:22:10.223Z
---

**No dejar que el operador descubra los errores en vivo.** Me lo dijo el 2026-09-01 y tenía razón: le estaba entregando cambios a medio verificar y él terminaba haciendo de control de calidad.

## Lo que tengo que hacer yo, siempre

1. **Reiniciar ATAS yo mismo.** Se puede y no hace falta pedírselo:
   - `Get-Process OFT.Platform` → `CloseMainWindow()`, o clic en la X.
   - Aparece el diálogo **"Save current workspace?"** → **"Save and close"** para no perderle el layout.
   - `open_application "ATAS Platform"` → queda en la pantalla de login con las credenciales ya guardadas → clic en **Connect**.
   - Esperar con `until` sobre la memoria del proceso: arranca en ~460 MB y los gráficos están listos arriba de ~2.000 MB. Tarda 3 a 5 minutos en total.
   - **Antes de cerrar, mirar el panel de cuenta**: si `Open PnL` no es 0.00 hay posición abierta y hay que preguntar.

### El reinicio, paso por paso y sin perder tiempo

Tardaba 4-5 minutos por reinicio con esperas a ciegas. La secuencia correcta:

```bash
# 1. cerrar (PowerShell) — sale el diálogo "Save current workspace?"
Get-Process OFT.Platform | ForEach-Object { $_.CloseMainWindow() }
```

- Esperar 4 s y clickear **"Save and close"** en (841, 435).
- `open_application "ATAS Platform"`.
- **Esperar a que aparezca el login, ~40 s.** Antes de eso el clic no hace nada.
- **El clic en "Connect" NO funciona solo**: la ventana no tiene foco. Hay que
  **clickear primero el cuerpo del diálogo (959, 210) y después apretar Enter.**
  Esto me colgó cinco veces seguidas.
- Los gráficos están listos cuando el proceso pasa de ~2.000 MB.

**Y sobre todo: agrupar los cambios.** Cada reinicio cuesta 4-5 minutos de su
sesión. Compilar varias correcciones juntas y reiniciar UNA vez.

2. **Verificar en pantalla, midiendo.** No alcanza con "compiló". Sacar captura y **medir contra el eje de precios**: dos marcas del eje dan los píxeles por punto, y con eso se comprueba que cada etiqueta esté a la altura de su nivel. Así encontré que una etiqueta estaba a 14 puntos de su línea.

3. **Verificar los datos y la lógica igual de en serio**, no solo lo visual.

## La trampa que ya me mordió tres veces

**ATAS guarda el valor de cada propiedad en el workspace, y ese guardado le gana a cualquier default nuevo.** Cambiar el default en el código no cambia nada en la máquina del operador.

La única solución es **renombrar la propiedad** (`Nombre` → `NombreNivel`, `MinBarridoLotes` → `ModoBarrido`, `VerLeyenda` → `VerLeyendaSiempre`). Conviene dejar un alias privado con el nombre viejo para no tocar el resto del código:

```csharp
public bool VerLeyendaSiempre { get; set; } = false;
private bool VerLeyenda => VerLeyendaSiempre;
```

## Sobre dibujar etiquetas

Una etiqueta superpuesta molesta; **una etiqueta lejos de su nivel miente** sobre dónde está el nivel. Cualquier anticolisión necesita **correa**: si adentro del tope no entra, se dibuja en su lugar correcto aunque se superponga. Apretado y honesto antes que prolijo y falso.

**Why:** el proyecto entero existe para que ningún número llegue sin verificar. Entregar sin mirar la pantalla es exactamente el problema que vinimos a resolver, aplicado a mi propio trabajo.

**How to apply:** compilar → instalar → reiniciar ATAS → captura → medir → recién ahí contarle. Ver [[mirar-pantalla-antes-de-responder-atas]] y [[cambios-atas-de-a-uno]].
