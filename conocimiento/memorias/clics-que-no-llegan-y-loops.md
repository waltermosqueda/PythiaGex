---
name: clics-que-no-llegan-y-loops
description: "REGLA PRIMERA (4 veces pedida, ultima 20-11, molesto): correr la ventana del chat ANTES de la primera interaccion con ATAS (no despues de fallar); desplegables de ajustes = clic en la FLECHA; lotes de clics; lo pesado a agentes en paralelo; nunca mas de 2 intentos iguales."
metadata: 
  node_type: memory
  type: feedback
  originSessionId: a8062245-bc99-4fb7-b817-ef244a0b1012
  modified: 2026-11-20T21:21:11.968Z
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

## Cuando el clic "no llega": correr la ventana y reintentar (2026-09-06)

Lo dijo el operador: cuando el control de escritorio contesta "el escritorio
esta al frente" o "TextInputHost esta al frente", casi siempre es que **su
ventana de chat (la app de Claude) se superpone en pantalla con el punto donde
quiero clickear**. La solucion que pide, en cualquier contexto: **correr la
ventana un poco (por PowerShell, ver [[mover-ventana-atas-oft-platform]]) y
volver a intentar el clic**, antes de cambiar de metodo o de pedirselo a el.
Esa noche paso cuatro veces con el dialogo "Save current workspace?" de ATAS.

**Reafirmado 2026-09-07 (dos veces, molesto):** "cuando los clicks no te
funcionan es porque chocan con el chat: corre TU ventana y volve a intentar".
No cerrar nada, no cambiar de metodo primero: mover la ventana del chat
(Claude) fuera del area y repetir el clic. UIA (InvokePattern) sigue siendo
el camino que no depende de eso.


## 13-11-2026 — tercera vez que me lo pide: PRIMER paso obligatorio, sin excepcion

Textual: "cuando no te funcione ningun click/interaccion, sea la situacion que sea, corre la ventana; chocan contra la
ventana del chat; error conocido tuyo de interaccion y perdes muchos minutos". Ese dia perdi ~15 min con clics de
dialogos de ATAS (Chart settings, Scrub, desplegables de la referencia) que caian sobre el chat.

**Lo que hay que saber:** la ventana del chat (proceso `claude`, titulo "Claude") NO aparece en las capturas de
computer-use (la filtran), asi que la pantalla se ve "limpia" y el clic igual cae en el chat. Su rectangulo real se lee
con EnumWindows sobre los procesos `claude` (el que mide ~536x728, no el de 1600x900 que es la capa de captura).
Ese dia estaba en x 1052..1588 fisico, justo encima de los dialogos y del eje derecho de ATAS.

**Protocolo (en este orden):**
1. ANTES de clickear en la mitad derecha de la pantalla o en cualquier dialogo: leer el rectangulo del chat y, si se
   superpone, MoveWindow a un costado (ej. 1240,110,360,640 fisico) — el operador lo autorizo explicitamente.
2. Si el clic igual no llega: UIA (InvokePattern / SelectionItemPattern; `herramientas/uia_invocar.ps1`).
3. Teclas: si computer-use dice "Claude's own window still has keyboard focus", NO insistir: SetForegroundWindow a la
   ventana de ATAS por PowerShell o usar el boton/menu equivalente.
4. Desplegables del PropertyGrid de DevExpress (ajustes de indicadores): la seleccion por clic/teclado/UIA no confirmo
   el 13-11 -> pedirle al operador ese ajuste en una linea (le cuesta 3 segundos).
5. Nunca mas de 2 intentos iguales.

## 20-11-2026 — cuarta vez, y dos mensajes molestos seguidos

Textual: "ya te dije muchas veces si no te responde los clicks move la ventana y volve a intentar rapidamente, no podes
perder mucho tiempo en esas acciones" y despues "no podes tardar tanto en los clicks, apura, se mas inteligente, busca mas
ayuda agente, es tu punto debil". Ese dia probe F4, flecha abajo y clic en el texto del desplegable "Positioning" del
Strike Profile de X-Ray antes de correr la ventana: minutos perdidos.

**Lo que funciono a la primera, apenas corri la ventana:**
- Mover el chat (proceso `claude`, MainWindowHandle) con MoveWindow a 1700,700 y `AppActivate` a OFT.Platform.
- En el PropertyGrid del dialogo Indicators, el desplegable se abre con clic en la **FLECHA** del borde derecho de la celda
  del valor (no en el texto, no con F4, no con flecha abajo: esas no cambian nada). Despues, clic en el item de la lista.
  Esto corrige el punto 4 de arriba: los desplegables SI se pueden operar.
- Encadenar en un solo `computer_batch`: abrir dialogo -> elegir indicador -> flecha -> item -> Apply -> zoom.
- Al terminar: Alt + ShowWindow(3) + SetForegroundWindow al chat.

**How to apply:** correr la ventana del chat es el PASO CERO de toda tanda de clics en ATAS, no el remedio. Lo que no es
clic (leer logs, recalcular, buscar en el SDK, investigar) va a agentes en paralelo mientras yo hago los clics, para que el
operador no me vea quieto.
