---
name: clics-que-no-llegan-y-loops
description: "Mis clics en botones de ATAS a veces no llegan; cuando pasa, cambiar de método en vez de repetir el mismo clic."
metadata: 
  node_type: memory
  type: feedback
  originSessionId: a8062245-bc99-4fb7-b817-ef244a0b1012
  modified: 2026-09-02T01:04:11.921Z
---

El 2026-09-01, instalando el indicador de VWAP anclado, apreté **cuatro veces** "Add to chart" en el diálogo de indicadores de ATAS. El contador `Added (N)` nunca subió. Yo lo leí como "ATAS rechaza el indicador" y me puse a buscar el error en mi código, en el log y en la API. **El indicador estaba perfecto: el clic nunca llegaba al botón.** Lo destrabó él apretándolo a mano.

Me lo marcó así: *"nunca hiciste el click correspondiente... perdés el foco de la tarea o entrás en loop"*. Y tiene razón: también me pasa al cerrar y abrir ATAS, donde quedo minutos dando vueltas.

## La regla

**Dos intentos del mismo clic y basta.** Si a la segunda no pasó nada, el problema es el clic, no el software. Ahí hay que cambiar de método, no repetir:

1. Buscar un camino alternativo en la interfaz (el diálogo se abre también desde el ícono al lado del nombre del indicador en la lista del gráfico, no solo desde el menú).
2. Hacer una **prueba de control**: intentar la misma acción con otro elemento. Si tampoco anda, es la UI. Eso separa "mi código falla" de "mi clic falla" en un solo paso y sin adivinar.
3. Si sigue sin salir, **pedírselo a él en una línea** en vez de quemar diez minutos. Un clic suyo cuesta tres segundos.

## La causa de la mitad de los bloqueos: el permiso de File Explorer

El 2026-09-01, durante todo el trabajo del VWAP, los clics fallaban con
*"The desktop shell is frontmost"* — sobre todo al manejar el diálogo
**"Save current workspace?"** despues de cerrar ATAS. No era ATAS ni el clic:
Windows reportaba el **escritorio** como ventana activa, y el control de
pantalla bloquea los clics en esa situacion.

**Se destraba pidiendo `request_access` con exactamente `"File Explorer"`**
(queda en tier "click": solo clic izquierdo, sin teclado). Apenas se concedió,
el mismo clic que había fallado cinco veces funcionó a la primera.

**Pedirlo al principio de cualquier sesión que vaya a reiniciar ATAS**, junto
con el permiso de ATAS. Ahorra el ciclo entero de pelear con el foco.

## La causa REAL, encontrada el 2026-09-02

El diagnostico de arriba (File Explorer) ayuda, pero la causa de fondo es otra y
se encontro enumerando las ventanas del proceso:

**El dialogo "Indicators" de ATAS es MODAL.** Mientras esta abierto —incluso
minimizado, incluso invisible— deja la ventana principal con `IsWindowEnabled =
False`. Con la principal deshabilitada y el dialogo sin foco, Windows no reporta
ninguna ventana activa y el control de pantalla lee **el escritorio** como
frontmost. De ahi el `"The desktop shell is frontmost"` y de ahi que
`CloseMainWindow()` y hasta la X de la ventana no hagan nada.

**Como diagnosticarlo:** enumerar las ventanas del proceso con `EnumWindows` +
`GetWindowThreadProcessId` y mirar `IsWindowVisible` / `IsWindowEnabled`. Si la
principal figura `Enabled=False`, hay un modal colgado.

**Como destrabarlo, sin matar el proceso:** mandarle `WM_CLOSE` (0x0010) por
`SendMessage` al handle del dialogo. La principal vuelve a `Enabled=True` al
instante y despues cierra normal, con su dialogo de guardar workspace.

**Nunca cerrar ATAS con `Stop-Process` para salir de esto**: se pierde el
workspace sin guardar. El modal siempre se puede cerrar por handle.

### Variante peor: la UI congelada con el render roto

El 2026-09-02 ATAS quedo con **el render colgado** (un grafico en negro, el
reloj de la pantalla sin avanzar, capturas identicas una tras otra) pero con
`Responding = True`. Ese flag engaña: mide la cola de mensajes, no el dibujo.

La logica seguia viva. `EnumWindows` mostro el dialogo **"Save current
workspace?"** con `Vis=True, En=True` — existia y estaba activo, solo que no se
dibujaba (en pantalla se veia apenas un rectangulo tenue).

**La salida:** mandarle el Enter **por mensaje**, sin depender del render ni del
foco:

```powershell
SetForegroundWindow($dlg)
PostMessage($dlg, 0x0100, 0x0D, 0x00000001)   # WM_KEYDOWN VK_RETURN
PostMessage($dlg, 0x0101, 0x0D, 0xC0000001)   # WM_KEYUP
```

Cerro guardando el workspace, sin perder nada. ATAS es WPF: los botones no son
ventanas hijas, asi que no sirve buscarlos con `EnumChildWindows` — hay que
mandar teclado a la HWND del dialogo.

**Ojo con TextInputHost**: se mete adelante y bloquea el teclado del control de
pantalla (`"Textinputhost" is not in the allowed applications`). Por eso la via
de `PostMessage` es mejor que la tecla fisica. Ver [[freeze-portapapeles-textinputhost]].

## Lo que NO hay que hacer

Repetir el clic con esperas cada vez más largas, buscar la causa en el código cuando no hay ninguna evidencia de que el código haya corrido, o ponerse a leer logs antes de confirmar que la acción se ejecutó.

**Why:** el costo no fue el error, fue el tiempo. Diez minutos de sesión en un botón, mientras él miraba, y encima concluyendo mal ("ATAS lo rechaza") sobre algo que funcionaba bien. Es el mismo pecado que el proyecto entero viene a evitar: afirmar sin verificar.

**How to apply:** antes de investigar por qué "no funcionó", verificar que la acción **se haya ejecutado**. Ver [[verificar-yo-no-el-usuario]] y [[compilar-indicadores-atas]].
