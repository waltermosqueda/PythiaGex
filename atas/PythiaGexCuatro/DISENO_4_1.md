# PythiaGex 4.1.0: un solo indicador, todo calculado adentro

Diseño del 08-10-2026. **Estado: diseño. No hay nada compilado ni instalado.**

- Versión objetivo: 4.1.0.
- Ensamblado: `PythiaGexCuatro.dll`, el mismo nombre que el visor 4.0.6, así que lo copia `herramientas/instalar_4_0.ps1` sin cambios.
- Nombre en ATAS: "PythiaGex 4.0 - Gamma Familia", categoría "PythiaGex 4.0".

**Base de este diseño (solo lectura).**
- Código: `atas/PythiaGexTres` (3.6.9), `atas/PythiaGexCuatro` (visor 4.0.6) y `atas/_propuesta_3_7/PythiaGexTres/Conversion37.cs`.
- Laboratorio: `laboratorio/tres/auditoria_0810/{preview_niveles.py, backtest_familia.py}` y `laboratorio/tres/tqqq/{tqqq_vivo.py, tq_comun.py}`.
- Python de producción: `pythiagex/{fuentes.py, cadena_atas.py, base.py, exposicion.py}`, `archivar_cadena.py` y `herramientas/cboe_local.py`.
- Workspace: `%APPDATA%\ATAS\Workspaces_v3\MNQ liviano.ws`.
- Una bajada de prueba por ticker de CBOE, con gzip (08-10, 22:25 UTC).

Todo lo que no se midió lleva la marca **SIN VERIFICAR**.

---

## 0. Respuesta corta al operador (para el mensaje de entrega, en criollo)

- **Rithmic es un caño que trae lo que se negocia en CME:** el futuro NQ/MNQ y las opciones *sobre ese futuro*. Esas ya las calculaba la 3.0 adentro de ATAS, y la 4.1 también.
- **NDX, QQQ y TQQQ son opciones de otras bolsas** (CBOE y la red de opciones de acciones de EE.UU.). Rithmic no las transmite. Es como un paquete de cable que trae los canales de deportes pero no los de películas.
  - Lo medimos: el catálogo de Rithmic de tu ATAS tiene 67.059 instrumentos de CME y ninguno de QQQ, TQQQ o NDX.
  - El único conector de ATAS que podría traer opciones de acciones es Interactive Brokers. No lo tenés y cuesta plata.
- **Entonces la 4.1 baja esas tres cadenas ella misma**, de la página pública de CBOE, adentro de ATAS. Las baja despacito (comprimidas y de a pedacitos) para no ahogarle la conexión a Rithmic. No hay Python, ni cboe_local, ni archivos de otros programas.
- **Lo único que se pierde:** lo que la 4.1 no vio, no lo puede reconstruir. CBOE solo da la cadena *de ahora*. Si ATAS está cerrado durante una rueda, esa rueda no queda guardada. Hoy pasaba lo mismo con la 3.0 y el libro de NQ.

---

## 1. Decisiones

1. **Qué es la 4.1:** la 3.0 3.6.9 copiada casi textual, más un módulo Familia y la pantalla de la 4.0.6.
   - De la 3.0 vienen el libro de NQ por la OptionsApi de Rithmic, la cinta, las capas NDX/QQQ y la regla Tres.
   - El módulo Familia calcula las 29 series de la vista previa con la misma cuenta.
   - Todo va en una DLL y un solo indicador.
2. **NDX, QQQ y TQQQ:** ATAS no las puede dar (§3.1). La 4.1 tiene su propio descargador de CBOE, en un hilo de fondo dentro del indicador (§3.2).
3. **Cero dependencias externas mientras corre.** No lee:
   - `PythiaGex\cboe-local`;
   - la nube (GitHub raw);
   - `PythiaGex\base-rueda-NQ.json` de la clásica;
   - `profundidad\pagina\preview_datos`;
   - las carpetas viva3, estela y cinta de la 3.0.
4. **La clase principal sigue siendo `PythiaGexCuatro.FamiliaCuatro`, con `AssemblyVersion` fija en 4.0.0.0.**
   - El `.ws` guarda el tipo así: `"Type": "PythiaGexCuatro.FamiliaCuatro, PythiaGexCuatro, Version=4.0.0.0, ..."` (leído en `MNQ liviano.ws`, línea 2666).
   - Con eso, el gráfico donde hoy está el visor carga la 4.1 sin volver a agregarla y conserva las 29 casillas `S_*` que eligió el operador.
   - La versión 4.1.0 va en `FileVersion`, en `InformationalVersion` y en la constante `VERSION`.
   - Que ATAS resuelva el tipo así está **SIN VERIFICAR**. Si no lo resuelve, el operador agrega el indicador a mano una vez.
5. **La cuenta del módulo Familia es un port exacto** de `backtest_familia.py` + `preview_niveles.py` + `tqqq_vivo.py`. Tiene dos modos:
   - `ParidadPython`: reproduce también los horarios fijos en UTC, la tabla fija de contratos y feriados, y el `s` de TQQQ sacado del after-hours. Lo usa solo el arnés.
   - Corregido (default del indicador): horarios en hora de Nueva York, contrato sacado del gráfico, calendario de feriados y `s` de TQQQ tomado del cierre oficial.
   - En horario de verano los dos modos dan lo mismo en NQ, NDX y QQQ. La única diferencia visible hoy es el `s` de TQQQ (§5.4).
6. **Lo propio de la 3.0 se calcula siempre** (dominantes, túnel, zero, capas, recuadro y cabecera), porque las series TRES_* salen de ahí.
   - Se dibuja solo con seis casillas nuevas, apagadas por defecto (§5.2).
7. **La 3.0 y la 4.1 pueden correr a la vez.**
   - Todo lo de la 4.1 va a `%APPDATA%\ATAS\PythiaGex4\` y a logs `pythiagex4-*`.
   - La exportación de la cinta a CSV, su relleno a CSV, el pulso de Profundidad, la caja negra y el centinela quedan apagados por defecto (§6).
8. **Nada se da por bueno sin el arnés de paridad** (§7). El criterio:
   - 0 diferencias en los strikes elegidos y en la presencia de cada serie por minuto;
   - ≤ 0,01 pts en los precios convertidos;
   - solo excepciones de una lista cerrada y explicada.

---

## 2. El clonado: `herramientas/clonar_4_0.py`

- Se corre con `python -I herramientas/clonar_4_0.py [--pisar] [--verificar]`.
- El script baja su propia prioridad a BELOW_NORMAL (`SetPriorityClass 0x4000`, el mismo bloque que `_prio.py`).
- **Solo lee `atas/PythiaGexTres`.** No toca la 3.0, ni `%APPDATA%`, ni ATAS.
- **Nunca hace `rmtree`.** `clonar_2_0.py --pisar` borra el destino; este no.

### 2.1 Respaldo del visor 4.0.6 (primer paso, siempre)

Se respalda en `atas/PythiaGexCuatro/_visor_4_0_6/`, y solo si la carpeta todavía no existe: un respaldo viejo no se pisa nunca.

- Se copian `FamiliaCuatro.cs`, `DatosCuatro.cs`, `PythiaGexCuatro.csproj`, `README.md` y `bin/Release/PythiaGexCuatro.dll` (el DLL del visor, para volver atrás).
- `MANIFIESTO.txt` guarda el sha256, el tamaño y la fecha de cada archivo.
- Recién con los hashes verificados, el script saca `FamiliaCuatro.cs` y `DatosCuatro.cs` de la raíz. Si quedaran, la clase `FamiliaCuatro` estaría definida dos veces.
- `capturas/` queda donde está.
- `atas/_test_cuatro/_test_cuatro.csproj` compila `../PythiaGexCuatro/DatosCuatro.cs`. Hay que apuntarlo a `_visor_4_0_6/DatosCuatro.cs` o retirarlo.

### 2.2 Estructura resultante

```
atas/PythiaGexCuatro/
  _visor_4_0_6/        respaldo del visor (2.1)
  Tres/                copia de la 3.0 renombrada; la escribe SOLO el script
    Black76.cs CadenaApi.cs Centinela.cs Feed.cs GammaHoyNucleo.cs Registro.cs RelojNy.cs Viva3.cs
    Tres.cs (era GammaHoyTres.cs)  TresApoyo.cs  TresAtravesadas.cs  TresCaja.cs  TresCapas.cs  TresCinta.cs
    TresEstado.cs  TresFormula.cs  TresHisteresis.cs  TresMajorOi.cs  TresMemoria.cs  TresRayas.cs
    TresRecuadro.cs  TresTunel.cs
    .clon              manifiesto: version 3.6.9, sha256 de cada original y de cada copia
  Cboe/                sin referencias a ATAS (el arnes lo compila): CboeCrudo.cs CboeCadena.cs CboeArchivo.cs BajadorCboe.cs
  Familia/             sin referencias a ATAS: Foto.cs Ticks.cs Libros.cs Gamma.cs Conversiones.cs ZeroEstandar.cs
                       Series.cs Tqqq.cs Calendario.cs Motor.cs Host.cs
  Pantalla/            con ATAS: Pantalla.cs (port del render de FamiliaCuatro.cs 4.0.6)
  Cuatro.cs            con ATAS: la clase (atributos, ajustes S_* y nuevos, overrides que reparten a la 3.0 y a la Familia)
  PythiaGexCuatro.csproj  README.md  CHANGELOG.md  DISENO_4_1.md  capturas/
```

`SondaApi.cs` **no se copia**: es un segundo indicador ("PythiaGex 3.0 - Sonda API") y nada de la 3.0 lo usa.

### 2.3 Cambios mecánicos

Se aplican en orden. Cada reemplazo afirma cuántas veces tiene que aparecer: si la 3.0 cambió y el conteo no da, el script aborta y dice dónde.

1. `namespace PythiaGexTres` pasa a `namespace PythiaGexCuatro`, en los 22 archivos.
2. `\bGammaHoyTres\b` pasa a `FamiliaCuatro`: es el nombre de la clase parcial y aparece en sus usos.
3. Se borran `[DisplayName("PythiaGex 3.0 - Gamma Hoy")]` y `[Category("PythiaGex 3.0")]` (`Tres.cs`, ex-`GammaHoyTres.cs:79-80`). Los atributos nuevos van en `Cuatro.cs`.
4. **Los overrides de la 3.0 pasan a métodos privados** (ATAS admite un solo override por clase). Los llama `Cuatro.cs`:
   - `public GammaHoyTres() : base(true)` → `private void Construir3()` (ex-`GammaHoyTres.cs:289`);
   - `protected override void OnInitialize()` → `private void OnInitialize3()` (:302);
   - `OnDispose` → `OnDispose3` (:321);
   - `OnCalculate` → `OnCalculate3` (:561);
   - `OnRender` → `OnRender3` (:700);
   - `OnCumulativeTrade` → `OnCumulativeTrade3` y `OnUpdateCumulativeTrade` → `OnUpdateCumulativeTrade3` (Caja:66, :78);
   - `OnCumulativeTradesResponse` → `OnCumulativeTradesResponse3` (Cinta:318);
   - `public override bool ProcessMouseClick(` → `private bool ProcessMouseClick3(` (Recuadro:50). Adentro, `base.ProcessMouseClick(e)` pasa a `false`.
5. **`Registro.cs`:** `"PythiaGex3"` → `"PythiaGex4"` (:26) y `"pythiagex3-"` → `"pythiagex4-"` (:28).
6. **`Centinela.cs:53`:** `"pythiagex3-centinela-"` → `"pythiagex4-centinela-"`.
7. **Cinta:**
   - el default de la carpeta (Cinta:63) y el respaldo fijo de `CintaCarpeta()` (:177) pasan a `""` → `Path.Combine(Registro.CarpetaDatos, "cinta")`;
   - el nombre del hilo `"PythiaGex3 cinta"` (:357) pasa a `"PythiaGex4 cinta"`.
8. **Estado:** el default de la ruta (Estado:49) pasa a `""` → `Path.Combine(Registro.CarpetaDatos, "estado", "indicador.json")`.
9. **Textos visibles:** `"PythiaGex 3.0` pasa a `"PythiaGex 4.0/3.0` en los literales (ex-`GammaHoyTres.cs:328, 723, 1002`). Así se ve que es el motor 3.0 adentro de la 4.0.
10. **Nombres de ajustes.**
    - Cada propiedad pública de ajuste de la 3.0 lleva un "3" en el nombre; el script reemplaza ese "3" por "4": `Ver3Zero` → `Ver4Zero`, `Cinta3Exportar` → `Cinta4Exportar`, `Capa3BaseNdx` → `Capa4BaseNdx`, `CrucesOiRadio3Pts` → `CrucesOiRadio4Pts`, etc.
    - La lista sale por regex de las declaraciones `public <tipo> <Nombre> { get; set; }` (son unas 100) y se reemplaza con `\b<Nombre>\b` en todos los usos.
    - El script afirma que ningún nombre nuevo choca con los del visor (`S_*`, `Eje4Libro`, `Estela4`, `Rotulos4`, `Cabecera4`, `Recuadro4Abierto`, `Letra4`, `MargenSup4`) ni con otro identificador de la 3.0.
    - Los enums (`Tri3`, `CapaModo3`, etc.) conservan su nombre: ATAS guarda los valores como texto y el tipo vive en otro namespace.
11. **Defaults que cambian en la copia.** Cada uno es un reemplazo textual del inicializador, afirmado una vez. Ninguno toca lo que se dibuja:
    - `Cinta4Exportar` true → **false**;
    - `Cinta4Rellenar` true → **false**: el relleno a CSV de la 3.0 no hace falta; la Familia pide el suyo (§4.3);
    - `Estado4Pulso` true → **false**;
    - `Caja4Activa` true → **false**;
    - `Guardar4Centinela` true → **false**.
    - Los ajustes de dibujo de la 3.0 **no cambian de default**. Los apagan las casillas `Tres41*` de §5.2. Así, prender una casilla muestra esa parte de la 3.0 exactamente como era.

### 2.4 Parches no mecánicos

Son pocos. Cada uno se escribe en el script como bloque literal "texto original exacto → texto nuevo" y se afirma una vez.

1. **`TresCapas.cs`: la fuente de las capas pasa a ser el descargador propio.**
   - Se borran `NUBE` (:128), `_http` (:127), `CarpetaCboeLocal` (:130), `UltimaNubeUtc`, `NubeJson` y la rama de la nube (:281-296).
   - En `ActualizarCapas` (:279-280) el JSON sale de `BajadorCboe.Ultima(k.Archivo)`: la línea flaca de la última foto aceptada y su `generado`, con el mismo control de 20 min, ahora medido sobre `generado`. `origen = "cboe-4.1"`.
   - En `CargarBaseRuedaSiHaceFalta` (:247) se saca la tupla de la clásica (`PythiaGex\base-rueda-*.json`). Queda solo `PythiaGex4\base-rueda-NQ.json` / `razon-rueda-QQQ.json`.
   - **`RETRASO_CBOE_S = 960` NO se toca.** TRES_* es "lo que dibuja la 3.0", y la 3.0 usa 960. La Familia usa 900 por su cuenta (§4).
2. **Ganchos hacia la Familia:** una línea cada uno, siempre después de la escritura que ya hace la 3.0.
   - `Viva3.Guardar`: después de `Registro.Anexar(ruta, json);` se agrega `FamiliaHost.FotoNq(raiz, json, ahora);`. La Familia recibe **el mismo texto** que va al archivo viva.
   - `GuardarEstela` (ex-`GammaHoyTres.cs:669`): la línea se arma en una variable local, se anexa como hoy y se agrega `FamiliaHost.Estela(raiz, linea);`. Va después del control de escritor único, así que solo postea el dueño.
   - `CintaEvento` (Cinta:186):
     - se borra el `if (!_cintaEncolar) return;` temprano;
     - antes de `int pend = Interlocked.Increment(ref _cintaPendientes);` se inserta `FamiliaHost.Tick(this, tMs, (double)px); if (!_cintaEncolar) return;`.
     - Así la Familia ve exactamente cada evento que iría al CSV (con volumen nuevo o cambio de precio), en O(1) y sin I/O.
3. **`CadenaApi`: `_proximoTurnoGlobal` (:179) arranca en `DateTime.UtcNow.AddSeconds(20)`.**
   - El espaciador es estático por ensamblado, así que no separa a la 3.0 de la 4.1.
   - Con ese desfase, al reiniciar ATAS no arman a la vez.
4. **Visibilidad de la 3.0.** Los getters `VerEstelaEf`, `VerZeroEf`, `VerEstelaZeroEf`, `VerTunelEf`, `VerToquesEf`, `VerMajorsEf`, `VerRotulosEf` y `VerCabeceraEf` (ex-`GammaHoyTres.cs:356-363`) se combinan con `&&` con su casilla `Tres41*` (§5.2).
   - `PintarCapas`, `PintarApoyo`, `PintarFormulas`, `PintarMajorOi` y `PintarRecuadro` reciben como primera línea `if (!Tres41<X>) return <vacío>;`.
   - Si no compila, el script falla; nunca se corrige a mano dentro de `Tres/`.

### 2.5 `--verificar` y `--pisar`

- **`--verificar`:**
  1. Regenera `Tres/` en memoria y la compara con la que está en disco.
  2. Deshace los renombres sobre `Tres/` y hace un diff contra `atas/PythiaGexTres`. El diff tiene que mostrar **solo** los parches de 2.4.
  - Esa es la prueba de que el motor 3.0 adentro de la 4.1 es la 3.6.9.
  - Si la 3.0 cambió desde el clon (hash distinto en `.clon`), imprime qué cambió para portarlo.
- **`--pisar`:** rehace `Tres/` solo si cada archivo de `Tres/` sigue con el hash de `.clon`. Si alguien lo editó a mano, aborta y lista los archivos. No toca nada fuera de `Tres/`.
- **El csproj** lo escribe el script solo si todavía es el del visor (si contiene `<Compile Include="DatosCuatro.cs" />`). Después se mantiene a mano.

### 2.6 `PythiaGexCuatro.csproj` (lo que escribe el script)

- `TargetFramework` net10.0-windows, `UseWPF`, `Nullable` disable, `EnableDefaultCompileItems` false, `AppendTargetFrameworkToOutputPath` false, `CopyLocalLockFileAssemblies` false, `DebugType` none.
- `AssemblyName` y `RootNamespace` PythiaGexCuatro. `Product` "PythiaGex 4.0 - Gamma Familia".
- **`AssemblyVersion` 4.0.0.0, fija** (decisión 4). `FileVersion` 4.1.0.0 e `InformationalVersion` 4.1.0.
- `Compile` con la lista explícita de `Tres/`, `Cboe/`, `Familia/`, `Pantalla/` y `Cuatro.cs`.
- Referencias: las mismas 9 de la 3.0, todas con `Private=false`: ATAS.Indicators, ATAS.DataFeedsCore, ATAS.Types, OFT.Rendering, OFT.Attributes, OFT.Localization, Utils.Common, OFT.Core y OFT.Platform.Core.
- Compilar con `dotnet build -c Release -o bin/Release` en esa carpeta.

### 2.7 `Cuatro.cs`: la clase

- `[DisplayName("PythiaGex 4.0 - Gamma Familia")] [Category("PythiaGex 4.0")] public partial class FamiliaCuatro : Indicator`, con `VERSION = "4.1.0"`.
  - El `.ws` guardó `"Name": "PythiaGex 4.0 - Familia"`, así que la leyenda de ese gráfico va a seguir diciendo eso. Es solo un rótulo.
- **Constructor:** `base(true)`, después `Construir3()` (que hace `DenyToChangePanel`, `EnableCustomDrawing`, `SubscribeToDrawingEvents(Final)` y oculta la serie).
- **`OnInitialize`:** `OnInitialize3()` y después `FamiliaHost.Registrar(this)`.
  - El registro es perezoso: se concreta en el primer `OnCalculate` con `ChartInfo` e `InstrumentInfo` presentes.
  - Así no arranca la instancia extra que ATAS crea al abrir los ajustes.
- **`OnDispose`:** `FamiliaHost.Soltar(this)` y después `OnDispose3()`.
- **`OnCalculate`:** `OnCalculate3(bar, value)` y después un push O(1) de la vela del gráfico (ticks crudos de `Time` y `LastTime`, o, c) a un anillo de 4096. Ese anillo sirve de respaldo para el `NQ_1600` de TQQQ (§B.6). Nada más.
- **`OnRender`:**
  1. si alguna casilla `Tres41*` está prendida, `OnRender3(g, layout)`;
  2. después, siempre, `Pantalla.Pintar(g)`, que lee la foto publicada por la Familia (§4.5). Así las etiquetas de la 4.0 quedan arriba.
- **`ProcessMouseClick`:** primero la pestaña de la 4.0. Si el clic no cayó ahí y `Tres41Recuadro` está prendido, `ProcessMouseClick3`. Si no, `base`.
- **`OnCumulativeTrade` / `OnUpdateCumulativeTrade`:** reenvían a los `*3`, que contienen el gancho de §2.4.2.
- **`OnCumulativeTradesResponse`:** si `request.RequestId` es de un pedido de relleno de la Familia, va a `FamiliaHost.Relleno(this, lista)`, en un `Task` aparte con la lista copiada. Si no, a `OnCumulativeTradesResponse3`.

---

## 3. Fuente de NDX, QQQ y TQQQ

### 3.1 ¿Las puede dar ATAS? No (medido hoy sin abrir ATAS)

- **Solo dos conectores implementan `IOptionsDataFeed`:** `OFT.Rithmic.dll` y `OFT.InteractiveBrokers2.dll`. Lo consume `OFT.Platform.Core.dll`.
  - Se buscó en todos los DLL de `C:\Program Files (x86)\ATAS Platform`; la versión instalada es 8.0.15.303.
  - `OptionsSubscriptionService.CanServeOptions` devuelve false si el conector no la implementa. Además exige `IsSupportedOptionsApi` en la cuenta.
- **`Connectors.cnf` tiene:** 3 Rithmic, 1 dxFeed Delayed, 3 ATAS Sim, Binance y Bitget. Ninguno es de Interactive Brokers.
  - dxFeed no implementa la interfaz. Su "OPRA" es solo una tabla de feriados y zonas horarias.
- **Rithmic resuelve opciones con `getInstrumentByUnderlying` sobre instrumentos de su propio catálogo.** `Database\Security.cdb` tiene:
  - 67.059 instrumentos @CME, 771 NYMEX, 238 COMEX, 195 CBOT, 178 IFUS, 72 EUREX y 14 CME_Ind;
  - **cero** acciones o índices QQQ, TQQQ o NDX. Lo único con esos nombres son tokens de cripto.
- **Logs `app_20261001` a `app_20261008` (~93 MB):** cero menciones de OPRA y cero búsquedas de QQQ o TQQQ por Rithmic.
- **X-Ray** usa su propio servidor y una caché cifrada con DPAPI, que es licencia de un tercero. No se toca.
- **Interactive Brokers** pide cuenta de IB más OPRA pago. Eso choca con "no pagar todavía".

**Diseño para el futuro:** el módulo Familia lee las cadenas a través de `ICadenaFuente` (`Ultima(tk)` y `Desde(tk, seq)`).
- Hoy la implementa solo `BajadorCboe`.
- Si algún día ATAS da OPRA, se agrega `FuenteAtas` sin tocar la cuenta.

### 3.2 El descargador propio: `Cboe/BajadorCboe.cs`

**Un solo descargador por proceso y entre procesos**
- Es un singleton estático con contador de referencias: arranca con el primer `FamiliaHost` y se detiene cuando se suelta el último.
- Corre en un hilo propio `"PythiaGex4 cboe"` (`IsBackground`, `ThreadPriority.BelowNormal`). **Nunca** usa el pool de hilos de ATAS ni sus temporizadores.
- Toma el `Mutex` con nombre `Local\PythiaGex4.BajadorCboe`.
  - La instancia que no lo consigue no baja nada: lee `PythiaGex4\cboe\ultima-*.json`, que escribe el dueño, cada 30 s y solo si cambió la fecha del archivo.
- Bajar de CBOE es HTTP aparte: no usa ningún cupo de opciones de ATAS.

**URL y símbolos**
- La URL es `https://cdn-api.cboe.com/api/global/delayed_quotes/options/{SYM}.json`.
  - Medido 08-10 22:25 UTC: `cdn.cboe.com` responde 307 hacia `cdn-api`.
  - Si falla el DNS o da 404, se vuelve a `cdn.cboe.com` con redirección y se anota en el log.
- Símbolos: NDX → `_NDX` (archivo `NQ`, como cboe_local), QQQ → `QQQ`, TQQQ → `TQQQ`.

**Cliente HTTP**
- Un `HttpClient` estático con `SocketsHttpHandler`:
  - `AutomaticDecompression = None`;
  - `AllowAutoRedirect = true`, con 3 redirecciones como máximo;
  - `UseCookies = false`;
  - `ConnectTimeout` 15 s y `PooledConnectionLifetime` 10 min.
- Cabeceras:
  - `User-Agent`: el de `fuentes.py` (Chrome 120);
  - `Accept-Encoding: gzip`;
  - `Accept: application/json`.
- Cada pedido tiene su propio tope de 60 s (`CancellationTokenSource`) y se hace con `HttpCompletionOption.ResponseHeadersRead`.

**Lectura en trozos, sin ráfagas** (port de `fuentes.bajar`)
- Se leen los bytes **comprimidos** del stream de a 64 KB, con `Thread.Sleep(50)` entre trozos, unos 1,25 MB/s como máximo.
- `fuentes.py` usa 256 KB por trozo. Acá van 64 KB porque el descargador vive en el mismo proceso que el socket de Rithmic.
- El ajuste `Cboe41TopeKBps` (1250) recalcula la pausa.
- Después se descomprime en memoria con `GZipStream`, si los datos empiezan con `1F 8B` o si `Content-Encoding` es gzip.
  - Si la respuesta llega sin comprimir, se acepta y el log dice "sin gzip".
- Cada ticker tiene su buffer propio, que crece y se reutiliza: comprimido ~1 MB, crudo ~7 MB en NDX. Así no se reservan 7 MB de LOH cada 75 s.
- Tiempos estimados: NDX unos 0,7 s, QQQ unos 0,6 s y TQQQ unos 0,1 s.
- Medido 08-10 22:25 UTC (comprimido / crudo / contratos):
  - `_NDX`: 903.794 / 6.994.192 / 16.010;
  - `QQQ`: 765.887 / 5.099.689 / 11.442;
  - `TQQQ`: 142.689 / 850.559 / 1.896.

**Cadencia**
- Igual que cboe_local, pero con la ventana en hora de NY:
  - una vuelta cada **75 s** de lunes a viernes entre 09:20 y 16:10 NY;
  - **300 s** el resto del tiempo. De noche hace falta: el NDX congelado y el cambio de `prev_day_close` (§B.2).
- La próxima vuelta se calcula como `max(5 s, espera − duración)`.
- Dentro de la vuelta va un ticker por vez, nunca en paralelo: NDX → QQQ → TQQQ, con 3 s entre tickers.
- La primera vuelta arranca 15 s después de que se registra el primer host, cuando ya pasó la carga de ATAS.
- Con `ParidadPython` la ventana es la de cboe_local (13:20-20:10 UTC). En verano es la misma.

**Reintentos**
- No hay reintento dentro de la vuelta.
- Con 3 fallos seguidos de un ticker, ese ticker pasa a 300 s hasta que vuelva a responder.
- Con HTTP 403 o 429 (Cloudflare), todo espera 15 min.
- Siempre queda en uso la última cadena buena, con su edad real.
- Medido: cboe_local registró 111 fallos históricos (DNS y timeouts) sin ningún reintento.

**Validación antes de aceptar una cadena**
- `timestamp` parseable;
- `data.current_price > 0`;
- más de 100 contratos;
- al menos 1 fila después de `construir`;
- `timestamp` **no anterior** al último aceptado de ese ticker (descarta un servidor de Cloudflare atrasado).
- Si el `timestamp` es igual al último, la foto no se acepta como nueva: gana la más temprana, como `archivar_cadena`.

**Sellos y retraso**
- `ts` = el `timestamp` de la raíz, en UTC.
- `generado` = hora UTC al terminar la bajada, con formato `yyyy-MM-ddTHH:mm:ss+00:00`.
- Los `last_trade_time` (de cada contrato y de `data`) vienen en hora de NY sin zona.
  - Se convierten con `TimeZoneInfo` "Eastern Standard Time". **Nunca con `ToUniversalTime`.**
- `ultimo_trade` = el máximo, como texto, de `last_trade_time` en toda la cadena, antes de filtrar.
- Retraso medido el 08-10 en la rueda: `ts − ultimo_trade` da 900/901/956 s en NDX, 900/901/953 en QQQ y 900/901/960 en TQQQ (mínimo/mediana/máximo).
- Desde la bajada hasta `ts` pasan 34-41 s de mediana, con un máximo de 183.
- **El momento del dato:** `t_dato = ultimo_trade` pasado a UTC; si no hay, `ts − 902 s`.
- **La edad que se muestra:** ahora − `t_dato`. Si pasa de 30 min, va **antes** del número.

**`construir`, `medir` y la línea flaca:** port exacto de `cadena_atas.construir`, `base.medir` y `archivar_cadena.linea_flaca`. Ver anexo C.

**Archivo propio en `%APPDATA%\ATAS\PythiaGex4\cboe\`**
- `ultima-<NQ|QQQ|TQQQ>.json`: la línea flaca, escrita de forma atómica (temporal + reemplazo).
- `cadena-<X>-<yyyy-MM-dd UTC de generado>.jsonl.gz`: un miembro gzip por línea, agregado al final.
  - Solo se agrega si `ts` cambió contra `cadena-<X>.ultimo` (persistido).
  - Las fotos repetidas de la noche **se guardan**: la lógica de "vivo" y de vigencia las cuenta, como en la vista previa.
- El formato es idéntico al de cboe-local. Así `libros.fotos_cboe` del laboratorio lee esta carpeta cambiando solo la ruta.
- `cierres.json`: por fecha NY, los cierres de NDX, QQQ y TQQQ con su fuente y su hora (§B.2).
- Retención: 14 días, podados una vez por día al arrancar.
- Peso: unos 7 MB por día entre los tres tickers.

**Log:** `%APPDATA%\ATAS\pythiagex4-cboe.log`, una línea por bajada (ticker, http, bytes_gz, bytes, ms, ts, aceptada o por qué no) y los cambios de modo.

**Interfaz hacia la Familia y las capas**
- `Ultima(tk)` devuelve un registro inmutable: texto flaco, foto parseada y `generado`.
- `Desde(tk, seq)` devuelve las fotos nuevas en orden.
- Las capas de la 3.0 leen `Ultima` en su latido de 60 s, desde el hilo del temporizador de ATAS. La lectura es solo de una referencia volátil: no bloquea.

**Bajada doble mientras siga corriendo cboe_local.py**
- La 3.0 lo necesita mientras viva. Son unos 1,8 MB comprimidos más por vuelta, de a trozos.
- Se decide cuando el operador retire la 3.0. No es una dependencia de la 4.1.

---

## 4. El módulo Familia

### 4.1 Archivos y clases

- **`Cboe/CboeCrudo.cs`**: lee el JSON crudo de CBOE con `Utf8JsonReader`, en una sola pasada sobre el buffer y sin armar árbol.
  - Saca `data.{current_price, close, prev_day_close, price_change, last_trade_time, bid, ask}`.
  - Por contrato saca `option, iv, open_interest, volume, bid, ask, last_trade_price, last_trade_time`. Nulo cuenta como 0.
  - `gamma`, `delta` y `prev_day_close` de cada contrato se ignoran.
- **`Cboe/CboeCadena.cs`**: `Construir`, `Medir` (forwards), `LineaFlaca` (texto con `Utf8JsonWriter`, cultura invariante) y `FotoCboe.DeLinea(texto)`, que es el port de `preview_niveles.foto_cboe_de_json`.
- **`Cboe/CboeArchivo.cs`**: escritura y lectura de `ultima-*`, `cadena-*.jsonl.gz`, `cierres.json` y la poda.
- **`Familia/Ticks.cs`**: la cinta en memoria (§4.3).
- **`Familia/Libros.cs`**: `FotoNq.DeViva3(linea)` (port de `foto_viva3`), `Cadena` (filas `double[n·8]`, días), `FilasHoy` (C10) y `Perfil` (`perfil_cp`, con calls y puts separados).
- **`Familia/Gamma.cs`**: Black-76 **sin** descuento (como `gamma76` de Python; el `Black76` de la 3.0 multiplica por `e^(−0,0375·T)`) y Black-Scholes con r = 0,0375 sin dividendo.
- **`Familia/ZeroEstandar.cs`**: C5, con la misma caché que la vista previa.
- **`Familia/Conversiones.cs`**: C7 (base de NDX), C8 (razón de QQQ), C9 (vigencia), `oi_fresco`, C2 (OI de NQ con fecha) y el corrimiento `corr` de NQ.
  - Se arranca de `ConversionCboe37` y `SaltoOi37` de la propuesta 3.7, que ya portaban C7/C8 y el salto en hora NY.
  - Antes de aceptarlos se verifican contra `backtest_familia` con el arnés.
- **`Familia/Series.cs`**: tabla de las 29 series (id, corto, libro, fuente, tipo, grupo, color, banda y los textos `tecnico`/`criollo`/`estado` copiados literal de `preview_niveles.SERIES` y `tqqq_vivo`).
  - También las reglas MUROS, MAJORS, ZTP (con islas), ZEST, CONF, FAM y los montos.
- **`Familia/Tqqq.cs`**: `s`, `c`, perfil del vencimiento más cercano, las 5 series, la historia por vela y los actuales. Con `Tqqq41AnclaNoche`, los modos A/B/C (anexo B).
- **`Familia/Calendario.cs`**:
  - sesión de CME, en UTC (paridad) o en NY (corregido);
  - contrato desde el código del gráfico (MNQZ6 → Z6) y su vencimiento: tercer viernes, 09:30 NY pasado a UTC;
  - feriados de NYSE/CME 2026-2027 y medios días;
  - posferiados.
- **`Familia/Motor.cs`**: una sesión. Guarda el estado, ingiere las fotos, recorre los minutos, persiste y publica la foto. El reloj y las opciones vienen inyectados (`Func<DateTime> AhoraUtc`, `Opciones{ParidadPython, Corregida, AnclaNoche}`), así el arnés lo usa igual que el indicador.
- **`Familia/Host.cs`**: `FamiliaHost`, uno por raíz (NQ).
  - Un diccionario estático le asigna una instancia dueña: el primer gráfico registrado.
  - Las otras instancias solo leen la foto publicada.
  - Si el dueño se suelta, toma la siguiente instancia registrada, que reconstruye desde lo persistido. Con 0 instancias, se detiene y vacía la persistencia.
- **`Familia/Foto.cs`**: el modelo inmutable que dibuja la pantalla.
  - Es la clase `Foto` de `DatosCuatro.cs` 4.0.6 con los mismos campos: `Series` (con `Hist` por vela m2 en ms UTC), `Actuales`, `Fuentes`, `NdxBase`, `QqqRazon`, `TqS`, `TqC`, las fechas de los datos, `Sesion`, `Instrumento`, `Precio` y `Error`.
  - Sin `Leer()`: ya no hay JSON.

Nada en `Cboe/` ni en `Familia/` referencia ensamblados de ATAS. El arnés los compila sin ATAS, y eso mismo lo prueba.

### 4.2 Hilos y temporizadores (nada pesado en el hilo de ATAS ni en el render)

- **Temporizador de ATAS de 5 s** (el `Tick` de la 3.0, sin cambios):
  - libro NQ (`CadenaApi.Latido`) y núcleo;
  - capas cada 60 s;
  - viva una vez por minuto, que dispara el gancho `FotoNq`;
  - estela, que dispara el gancho `Estela`.
- **Temporizador de ATAS de 2 s** (reloj de la cinta de la 3.0): queda. Con la exportación apagada no escribe nada.
- **Temporizador de ATAS de 1 s** (pulso de Profundidad): con `Estado4Pulso = false` no escribe.
- **Hilo `"PythiaGex4 cboe"`** (BelowNormal, uno por proceso): las bajadas (§3.2).
- **Hilo `"PythiaGex4 familia"`** (BelowNormal, uno por host):
  - se despierta cada 5 s, o antes si llega una foto nueva (`AutoResetEvent`);
  - corre `Motor.Paso()`: drena las colas, calcula los minutos nuevos, calcula TQQQ, persiste y publica.
  - Todo con try/catch y log `pythiagex4-familia.log`, como máximo una línea de error por minuto.
- **Hilo de ATAS en `OnCumulativeTrade`:** `Ticks.Agregar` es O(1) bajo una llave corta. Sin I/O y sin LINQ.
- **`OnCalculate`:** solo el push O(1) de §2.7, además de lo que ya hacía la 3.0.
- **`OnRender`:** `var f = Volatile.Read(ref host.Foto)`. Ninguna llave del motor, ningún archivo.
  - El mapeo vela del gráfico → vela m2 es el de la 4.0.6: `k = floor(ms(LastTime ?? Time)/120000)·120000`, sobre los ticks crudos de `Time` (Kind Unspecified = UTC; nunca `ToUniversalTime`).
- **No se usa `Task.Run`** para la Familia. El único `Task` es la copia de la respuesta del relleno.

### 4.3 Insumos dentro del proceso

**Cinta del MNQ (`Ticks.cs`)**
- **Almacén por segundo de la sesión:** `double[90000]` de precio y `long[90000]` de t en ms (unos 1,4 MB).
  - La casilla de un evento es `ceil(t_ms/1000)`.
  - En cada casilla gana el evento de mayor `t_ms`; si empatan, el último en llegar.
  - Es paridad exacta con "último tick ≤ T" para T en segundos enteros, que es lo que piden C7, C8 y TQQQ (sellos de CBOE − 900 s y fin de vela − 1 s).
- **Acumulador m2 por cubeta** `b = floor(t_ms/120000)·120000`:
  - `o` = precio del primer evento, en orden de llegada, entre los de `t` mínimo;
  - `c` = precio del último evento, en orden de llegada, entre los de `t` máximo;
  - `h` y `l` = máximo y mínimo.
  - Equivale a `velas_de_ticks` con su orden estable por t.
  - Una vela está cerrada si `b + 120000 ≤ t` del último tick.
- **`CierreConocido(t)`**, **`Precio(ts)`** (tick con respaldo de vela) y **`PrecioSoloTick(ts)`** (TQQQ): port exacto del anexo A.2.
- **Relleno.** Al arrancar (o al volver de un hueco de más de 2 min durante la rueda), el host le pide al dueño `RequestForCumulativeTrades` desde el último segundo que tiene hasta ahora.
  - El pedido se manda desde el temporizador y solo sin posición ni órdenes activas (reusa `CintaPosicionAbierta()` de la 3.0), porque bajar la sesión carga a ATAS unos 45 s.
  - Con el almacén persistido, el pedido es solo del hueco, no de la sesión entera.
  - Los eventos del relleno entran solo con `t <` el primer tick vivo, como en la vista previa.
- **Persistencia.** Cada 5 min se escribe `familia/ticks-<sesion>.bin` de forma atómica (el almacén por segundo + las cubetas m2).
- **Roll.** Si cambia el contrato del gráfico, el almacén y las muestras se reinician (como C7/C8).

**Libro NQ.** Llega por el gancho `FotoNq(raiz, linea, ahora)`, con el texto exacto de viva.
- `FotoNq.DeViva3` aplica el mismo filtro y el mismo armado que `foto_viva3`.
- El motor recibe exactamente lo que el arnés lee de un archivo viva.

**CBOE.** Llega de `BajadorCboe.Desde(tk, seq)`, como texto flaco. `FotoCboe.DeLinea` es el mismo lector que usa el arnés con los archivos de cboe-local.

**TRES_*.** Llega por el gancho `Estela(capa, linea)`, con el mismo texto de la estela, y se lee igual que lo hace la vista previa:
- el campo `d[0..1]` con valores > 15000;
- vida de 300 s para NQ y de 1500 s para NDX y QQQ.

**Al reiniciar,** todo se lee de los archivos propios: `viva/`, `cboe/`, `estela/` y `familia/`.

### 4.4 La cuenta

Es el port exacto de las especificaciones de los anexos A, B y C, sin fórmulas nuevas. Por minuto UTC:

1. **`fut = CierreConocido(t)`.**
2. **Fotos vigentes:**
   - NQ: `ts ≤ t` y `t − ts < 300 s`;
   - NDX y QQQ: la última con `generado ≤ t` y `t < vigencia` (C9).
3. **Conversiones:**
   - NQ: `corr`;
   - NDX: base C7;
   - QQQ: razón C8.
4. **Perfil por lado** con el horizonte "Hoy" envejecido (C10) y el filtro de ±3 % al precio de NQ.
5. **Series:** MUROS, MAJORS, ZEST (C5, con caché), ZTP (islas C3), FAM (C1), CONF y montos.
6. **TQQQ por vela m2** (anexo B).
7. **TRES_*** desde las líneas de estela.

**Modo corregido contra `ParidadPython`.** Solo cambian estas funciones del calendario:
- la ventana de la sesión;
- las horas fijas de C7, C9, `oi_fresco`, C2 y TQQQ;
- el contrato y su vencimiento;
- los posferiados;
- el `s` de TQQQ.

En verano dan igual, salvo el `s` de TQQQ (§5.4). El arnés corre los dos modos (§7).

### 4.5 Publicación, persistencia y arranque

**Publicación**
- Después de cada minuto calculado, o cuando cambian los actuales, el motor arma una `Foto` nueva y la publica con `Volatile.Write`.
- Las `Hist` se copian por escritura: solo cambia la vela en curso.
- Se arma como máximo una por minuto, más una por cada foto nueva de CBOE o de NQ.

**Persistencia** (hilo familia, escritura atómica o `append`):
- `familia/niv-<sesion>.jsonl`: una línea por minuto calculado, `{key, serie: [[precio, etq, monto, strike]]}`.
  - **Los minutos ya calculados no se recalculan.** El pasado no se mueve, y la semántica de "en vivo" de C2 se conserva.
- `familia/muestras-<NDX|QQQ|TQQQ>-<sesion>.jsonl`: una línea por foto `{ts, generado, spot, precio, muestra, vivo, en_hora, contrato}`.
  - Al arrancar se reconstruyen el `roll24`, las ruedas (C7), la razón (C8) y el `c` de TQQQ de los últimos 6 días, sin necesitar la cinta vieja.
- `familia/estado-<sesion>.json`: salto de OI (C2), estado de `oi_fresco` y el historial de `corr`.
- `familia/tqqq-cierres.json` (anexo B.6).

**Arranque**
1. Leer lo persistido y los archivos de la sesión.
2. Pedir el relleno del hueco.
3. Calcular los minutos faltantes en tandas de 240, con `Thread.Sleep(300)` entre tandas, publicando después de cada una.
4. Entrar en régimen.

**Lo que no se puede reconstruir**
- CBOE solo da la cadena de ahora y Rithmic solo el libro de ahora. La historia de la 4.1 empieza la primera vez que corre y desde ahí se persiste.
- Si falta la rueda anterior (ATAS cerrado), C7 no tiene referencia de noche. **NDX por base no se dibuja de noche** y la pestaña dice por qué. No se rellena con otro método (protocolo, regla 5).
- El `s` de TQQQ no necesita historia: sale del `prev_day_close` de la cadena (§B.2).

---

## 5. Qué se dibuja

### 5.1 Por defecto: las 8 series del operador y la pantalla de la 4.0.6

- **Casillas `S_<ID>`:** las 29, con **los mismos nombres de propiedad que el visor**, para que se conserve lo que guardó el `.ws`.
- **Prendidas por defecto** (las eligió el operador, captura del 08-10 17:5x):
  - `S_MAJORS_QQQ_oi`, `S_MUROS_NQ_oi`, `S_MUROS_NDX_vol`, `S_MUROS_QQQ_oi`;
  - `S_FAM_MUROS_oi`, `S_ZEST_QQQ_vol`, `S_TRES_NDX`, `S_T_MUROS_oi`.
  - El resto, apagadas.
- **Grupos:** "1. Recomendadas", "2. Familia", "3. Zero", "4. Lo que dibuja la 3.0", "5. TQQQ x3" y "6. Pantalla".
- **De la 4.0.6 se conserva** (código portado a `Pantalla/Pantalla.cs`, con propiedades del mismo nombre):
  - rayitas por vela m2 (`Estela4`);
  - etiquetas chicas pegadas al eje, con "+N" si hay series a ≤ 1 pt (`Rotulos4`);
  - pestaña desplegable que arranca cerrada, con fuentes, edades, conversiones y detalle (`Cabecera4`, `Recuadro4Abierto`);
  - la edad **antes** de los números si pasa de 30 min;
  - sin sombreado (la banda ±N va solo en el texto);
  - margen superior de 56 (`MargenSup4`) y letra 9 (`Letra4`);
  - doble eje elegible `Eje4Libro`: Ninguno, NDX (NQ − base), QQQ (NQ ÷ razón) o TQQQ ((NQ − c) ÷ s), con la misma conversión que sus rayas;
  - control de vencimiento: con el gráfico en otro contrato que el estado persistido, no dibuja y avisa.
- **Se elimina `Carpeta4`.** ATAS va a encontrar el valor viejo en el `.ws`; que ignore una propiedad desconocida está **SIN VERIFICAR**. El log de arranque lista los ajustes leídos.
- **La pestaña dice de dónde sale cada fuente:**
  - "NQ: Rithmic OptionsApi (libro propio), foto de hace N s";
  - "NDX/QQQ/TQQQ: CBOE (bajada propia, retraso de CBOE 15 min), dato de hace N min";
  - "cinta: último tick hace N s".
  - Si no hay ticks hace más de 2 min en la rueda, aparece el cartel "sin cinta: ¿pestaña oculta?".

### 5.2 Lo propio de la 3.0: calculado siempre, dibujado solo si se pide

Grupo "8. Lo propio de la 3.0 (apagado)". Son casillas nuevas: nunca existieron en ningún `.ws`. Todas arrancan en false:

- `Tres41Dominantes`: D1/D2, su estela, toques, majors, fórmulas F1..F8, apoyo y rótulos;
- `Tres41Tunel`: el túnel;
- `Tres41Zero`: el zero, su estela y los cruces;
- `Tres41Capas`: las capas NDX/QQQ de la 3.0;
- `Tres41Recuadro`: el recuadro del libro, con su clic;
- `Tres41Cabecera`: la cabecera de la 3.0.

Cómo funcionan:
- Con las seis apagadas, `OnRender3` no se llama.
- Con alguna prendida, el resto de los ajustes de la 3.0 decide como en la 3.0. Están renombrados con "4" en el grupo "9. Ajustes finos de la 3.0" y tienen los defaults de la 3.6.9.
- Las capas siempre se **calculan** (`Capa4QQQ` y `Capa4NDX` en Propia), porque TRES_NDX y TRES_QQQ salen de ahí.

### 5.3 Ajustes nuevos de la 4.1

Grupo "7. Fuentes (todo adentro)":
- `Cboe41Bajar` (true): bajar las cadenas de NDX, QQQ y TQQQ. Apagado, esas series dicen "descarga apagada".
- `Cboe41TopeKBps` (1250): tope de velocidad de la bajada.
- `Familia41Corregida` (true): horarios en NY, contrato del gráfico, calendario y `s` de TQQQ del cierre oficial. Apagado, se comporta igual que el Python de la vista previa.
- `Tqqq41AnclaNoche` (**false**): reanclaje de TQQQ de noche con los modos B/C del anexo B.5. Es una función nueva, **SIN VALIDAR** (1 noche medida). Prendida, dibuja TQQQ de noche con el rótulo "ancla de noche, 1 noche medida".

### 5.4 Cambios contra lo que dibuja hoy la 4.0.6

Regla del operador: avisar en el mismo mensaje, con captura antes y después.

1. **TQQQ: `s` sale del cierre oficial** (`prev_day_close` validado) y no del último precio del after-hours.
   - El Python toma la última foto con `t_dato ≤ 20:00 UTC`, que después del cierre siempre es 15:59:59 NY: se queda con el after-hours. Hoy dio P0 80,45 contra un cierre oficial estimado entre 80,17 y 80,22.
   - Efecto: unos −0,3 % de pendiente, menos de 2 pts dentro del ±5 %.
   - El 08-10 la vista previa usó el `s` de `conversion.json` (cierres), así que ese día no cambia nada.
2. **Las fuentes son propias.** El libro NQ es de la 4.1 y CBOE lo baja la 4.1, con su propio `generado`.
   - Los bordes de minuto pueden correrse unos segundos contra la vista previa.
   - La cuenta no cambia.
3. **La historia empieza cuando arranca la 4.1** (§4.5).
4. **TRES_*** sale del motor 3.0 que corre adentro de la 4.1.
   - Sin la base de rueda de la clásica, la primera noche la capa NDX hace lo que la 3.0 hacía sin archivo. **SIN VERIFICAR en vivo**: se mira la línea "capa NDX" del log.
5. **Horarios en NY:** no hay cambio visible hasta el 01-11.

Captura "antes": la 4.0.6 con los generadores y la 3.0 corriendo. Si ya no corren, `capturas/2026-10-08_1855_despues_4_0_6.jpg`. Captura "después": la 4.1 en el mismo gráfico y con el mismo zoom.

---

## 6. Archivos que escribe (nada pisa a la 3.0)

Todo va bajo `%APPDATA%\ATAS\PythiaGex4\`:
- `cboe/`: `ultima-{NQ,QQQ,TQQQ}.json`, `cadena-{X}-<día>.jsonl.gz`, `cadena-{X}.ultimo` y `cierres.json` (14 días).
- `viva/viva3-NQ-<día>.jsonl`: lo escribe el código copiado de la 3.0.
  - Se deja el prefijo `viva3-` a propósito, porque el formato es idéntico: el laboratorio lo lee cambiando solo la carpeta.
  - Pesa unos 17 MB por día. Retención de 7 días.
- `estela/estela-{NQ,NDX,QQQ}-<día>.jsonl`: del código de la 3.0, con `Guardar4Estela = true`.
- `base-rueda-NQ.json` y `razon-rueda-QQQ.json`: de las capas.
- `familia/`: `niv-<sesion>.jsonl`, `muestras-*-<sesion>.jsonl`, `estado-<sesion>.json`, `ticks-<sesion>.bin` y `tqqq-cierres.json` (14 días; los ticks, 7).
- Solo si se prenden:
  - `caja/` (`Caja4Activa`);
  - `rebobinado/`;
  - `cinta/` (`Cinta4Exportar`);
  - `estado/indicador.json` (`Estado4Pulso`).

Logs en `%APPDATA%\ATAS\`:
- `pythiagex4-gammahoy.log` (el motor 3.0), `pythiagex4-cboe.log`, `pythiagex4-familia.log` y `pythiagex4-centinela-*` (solo con `Guardar4Centinela`).
- El `PythiaGex4\pythiagex4.log` del visor deja de crecer.

**No se escribe nada** en:
- `PythiaGex3\`;
- `PythiaGex\` (cboe-local ni base-rueda);
- `profundidad\estado\cinta` ni `profundidad\estado\indicador.json`;
- `profundidad\pagina\preview_datos`.

Los escritores únicos (`_estelaEscritor`, `Viva3._escritor`, `_cintaDuenos`) son estáticos por ensamblado: no se coordinan con la 3.0. Por eso las carpetas tienen que ser distintas.

---

## 7. Arnés de paridad: `atas/_test_cuatro_paridad`

### 7.1 Proyecto

- Es un programa de consola: `_test_cuatro_paridad.csproj` (Exe, net10.0-windows, sin referencias a ATAS).
- Compila `../PythiaGexCuatro/Cboe/*.cs`, `../PythiaGexCuatro/Familia/*.cs`, `Program.cs` y `Comparar.cs`.
- Usa los mismos archivos `.cs` que el indicador: no hay copias.
- Se corre con `dotnet run -c Release -- --entrada entrada --referencia referencia --salida resultados [--dia 2026-10-08] [--modo paridad|corregido|replay]`.
- Pone su propia prioridad en BelowNormal y lee con pausas. Se corre fuera de la rueda.
- `entrada/`, `referencia/` y `resultados/` van en `.gitignore`.

### 7.2 Entrada congelada (las sesiones del 07-10 y el 08-10)

`laboratorio/tres/auditoria_0810/referencia_4_1.py` (con `python -I` y `_prio`) copia una sola vez a `atas/_test_cuatro_paridad/entrada/`, con un `manifiesto.json` (sha256, tamaño y mtime):

- la cinta: `profundidad/estado/cinta/cinta-NQ-2026-10-0{7,8}.csv` y `-relleno.csv` (unos 141 MB);
- viva3: `%APPDATA%/ATAS/PythiaGex3/viva/viva3-NQ-2026-10-0{7,8}.jsonl` (unos 35 MB);
- cboe-local: `%APPDATA%/ATAS/PythiaGex/cboe-local/cadena-{NQ,QQQ,TQQQ}-2026-10-01..08.jsonl.gz`, con los 6 días previos (unos 50 MB);
- las estelas de la 3.0: `%APPDATA%/ATAS/PythiaGex3/estela/estela-{NQ,NDX,QQQ}-2026-10-0{7,8}.jsonl`;
- velas m2 previas a la cinta: `laboratorio/tres/datos/velas_cache_2026-10-06/velas-MNQ-m2.csv` y `laboratorio/dom/ronda3/velas/velas-MNQ-m2.csv`.
  - Solo las usa el respaldo de `Precio()` para fotos anteriores al 07-10.
  - En el indicador ese lugar lo ocupan las muestras persistidas. Es una interfaz `IPrecioHistorico` que solo implementa el arnés.
- crudos de CBOE para `construir` y `medir`:
  - `laboratorio/tres/auditoria_0810/crudo/_NDX-20261008-064032.json.gz`;
  - `laboratorio/tres/tqqq/crudo/TQQQ-20261008-172310.json.gz`;
  - las tres bajadas de prueba del 08-10 22:25 UTC (`scratchpad/fuentes4/bajada_prueba/{_NDX,QQQ,TQQQ}.json.gz`; copiarlas ya, porque el scratchpad es temporal).
- **Congelar ya** `profundidad/pagina/preview_datos/tqqq_vivo.json` (hoy todavía es el de las 21:59:12Z) y `preview_niveles.json`. Los generadores los pisan apenas vuelven a correr.

### 7.3 Referencia Python (la produce el mismo script)

- **Rutas redirigidas.** Por monkeypatch, apunta `P.CINTA`, `P.VIVA3`, `P.CBOE_LOCAL`, `_prio.CBOE`, las rutas de `libros` y las de `tq_comun` a `entrada/`.
  - Después de correr, compara el hash de cada archivo que se abrió contra el manifiesto. Si alguno no está en el manifiesto, aborta (garantiza que no se leyó un archivo vivo).
- **Vista previa:** para cada día, `P.Motor(dia, vivo=False, hasta=<dia> 21:00 UTC).historico()`.
  - Vuelca por minuto `{key: {serie: [[precio, etq, monto]]}}`.
  - Por minuto y libro vuelca también `fut`, `corr`, `base`, `razón`, `oi_ok`, `oi_fresco`, `vigencia` y el índice de foto. Sirven para diagnosticar.
  - Salida: `referencia/preview-<dia>.json.gz`.
- **TQQQ:** `tqqq_vivo.ciclo` con `pd.Timestamp.utcnow` parcheado a 2026-10-07 21:59 y a 2026-10-08 21:59:12 UTC. Vuelca historia y actuales a `referencia/tqqq-<dia>.json`.
- **CBOE:** `cadena_atas.construir`, `base.medir` y `archivar_cadena.linea_flaca` sobre los 5 crudos. Salida: `referencia/cboe-<crudo>.json`.
- **Hashes de los scripts.** La cabecera de cada salida lleva los sha256 de `backtest_familia.py`, `preview_niveles.py`, `tqqq_vivo.py`, `tq_comun.py`, `cadena_atas.py` y `base.py`. Si el laboratorio cambia después, se sabe contra qué versión se comparó.

### 7.4 Comparaciones

**A. CBOE (exacto)**
- Sobre los 5 crudos:
  - filas iguales en las 8 columnas;
  - `vencimientos[].dias` iguales a 4 decimales;
  - `ultimo_trade`, `spot_idx` y `campos` iguales;
  - la línea flaca igual byte a byte en las claves de §3.2, salvo `generado`, que se inyecta.
- Esperado de `_NDX-20261008-064032`: `base_cruda` = **238,36** y `base_error_ticks` = **30,8**. Lo midió el agente de fuentes; son los mismos 238,36 que cita la 3.0.

**B. Lectura de fotos.** Cada línea de cboe-local leída con `FotoCboe.DeLinea` tiene que dar lo mismo que `foto_cboe_de_json` en `generado`, `ts`, `t_dato`, `spot`, `oi_total`, `err` y `n_filas`. Cada línea viva3 leída con `FotoNq.DeViva3` tiene que dar lo mismo que `foto_viva3`.

**C. Las 24 series, minuto a minuto** (07-10 completo y 08-10 hasta las 21:00 UTC), en modo `paridad`:
- las claves con cada serie son las mismas (0 minutos de un solo lado);
- en cada clave, misma cantidad de niveles y mismas etiquetas;
- **strikes elegidos iguales:** el C# expone K; en Python se recupera con `p − corr`, `p − base` o `p / razón` del mismo minuto. Tolerancia 1e-9, es decir, 0;
- |Δprecio| ≤ 0,01 en NQ;
- |Δmonto| ≤ 0,1.
- Las interpoladas (ZEST, ZTP, CONF, T_ZERO) se comparan solo por precio.

**D. TQQQ (08-10)**
- Esperado, contra la referencia regenerada y contra el `tqqq_vivo.json` congelado de las 21:59:12Z:
  - 223 velas en T_DOMS_vol, T_MUROS_* y T_ZERO_oi, y 201 en T_DOMS_raz;
  - última vela: 1791494280000 (21:18 UTC), con la foto generada a las 20:56:05Z (dato 19:59:59Z);
  - c = 21.012,058;
  - dominantes: 82,0 → 31.197,53 (+13,49 M) y 83,0 → 31.321,74 (+6,43 M);
  - muros por volumen: C 82 y P 80 (→ 30.949,10, −13,36 M);
  - muros por OI: C 81 → 31.073,31 y P 80;
  - zero por OI: 80,8798 → 31.058,38.
- El 08-10 se fuerza `s = 124,21302718647851`, como hizo el Python con `conversion.json`.
- El 07-10 se compara contra el `s` que el Python saca de las fotos.

**E. Replay.** El mismo motor, alimentado minuto a minuto con un reloj simulado:
- las fotos se entregan a su `generado` y los ticks a su `t`;
- persiste y reinicia el motor dos veces en mitad de la sesión: 03:00 y 15:00 UTC.
- Tiene que dar lo mismo que el histórico, salvo los minutos de C2 entre las 22:00 UTC y el salto de OI. Ahí la versión en vivo depende de "ahora" ("esperando el salto"); se listan.

**F. Modo corregido.** En 07-10 y 08-10 tiene que dar 0 diferencias en NQ, NDX y QQQ. En TQQQ informa las diferencias del `s` (el 07-10) para el aviso de §5.4. Es informativo, no decide si pasa.

**G. Horario de invierno, con fechas sintéticas:**
- la ventana de la rueda pasa de 13:20 a 14:20 UTC el 02-11-2026;
- el vencimiento de las 16:00 NY cae a las 20 UTC el 30-10 y a las 21 UTC el 06-11;
- el vencimiento de H7 es 2027-03-19 13:30 UTC (la tabla del Python dice 14:30: es su bug);
- la sesión NY es de 18:00 a 17:00;
- C9 cae a las 09:30 NY.

**H. Recursos.** Mide e imprime (son metas a medir, no supuestos):
- ms por minuto (mediana y p99);
- segundos por sesión completa;
- memoria pico.
- Metas: menos de 50 ms de mediana por minuto y menos de 10 s de CPU por sesión completa.

### 7.5 Criterio de aceptación

Pasa solo si se cumple todo esto:
- 0 diferencias en strikes elegidos y en presencia;
- ≤ 0,01 pts en precios convertidos;
- ≤ 0,1 en montos;
- A, B y D exactos.

Cada excepción se lista con minuto, serie, los dos valores y su clase. Solo se aceptan estas:
- **E1 empate numérico:** dos candidatos a menos de 1e-9 relativo. El orden de las sumas (numpy suma por pares) o el último ulp de `exp`/`log` puede dar vuelta un `argmax` o un signo estricto. Hay que mostrar los dos valores.
- **E2 empate exacto de |gv| en T_DOMS_vol:** `nlargest` de pandas no es estable.
- **E3 borde de redondeo** de `round(x, 2)` o `round(x, 1)`: diferencia exactamente de 0,01 o 0,1.
- **E4 `round(dias, 4)`** del Python con T cerca del piso de 1/1440: solo con `dias < 0,0007`.

Cualquier otra diferencia es un error del C#. Se arregla el C#, nunca la tolerancia. El resultado queda en `resultados/paridad-<dia>-<modo>.txt`, con una tabla por serie y código de salida 0 o 1.

### 7.6 Prueba en vivo (blanda, después del arnés)

1. **A/B de una sesión con la 3.0 y la 4.1 en gráficos distintos:**
   - estela NDX y NQ de `PythiaGex3` contra `PythiaGex4`;
   - la línea "capa NDX" de los dos logs.
2. **Si el operador vuelve a correr los generadores:** la 4.1 contra la vista previa en vivo, por fracción de minutos con el mismo strike. Las fuentes difieren por segundos, así que esto no decide nada.

---

## 8. Riesgos y mitigación

1. **CPU en el hilo de ATAS.**
   - `OnCalculate`, `OnCumulativeTrade` y `OnRender` no agregan trabajo que no sea O(1). El render lee una referencia inmutable.
   - Toda la cuenta va en el hilo familia (BelowNormal). El arranque va de a tandas de 240 minutos con pausas, y la caché de ZEST evita recalcular.
   - El log anota `ciclo familia: N ms` cada 10 min y avisa si un ciclo pasa de 200 ms.
   - Lo que ya pesaba en la 3.0 (núcleo cada 5 s, capas cada 60 s) sigue igual. La caja negra, el centinela, el pulso y la exportación de la cinta quedan apagados.
2. **La descarga dentro de ATAS y los cortes de Rithmic.**
   - Antecedente: las ráfagas de 2-3 MB/s cada 75 s coincidieron con cortes (166 contra 18, el 16-09).
   - Mitigaciones:
     - trozos de 64 KB con 50 ms de pausa (unos 1,25 MB/s);
     - un ticker por vez, con 3 s entre tickers;
     - siempre gzip (unos 1,8 MB comprimidos por vuelta, cerca de 1,4 s de red cada 75 s);
     - hilo propio BelowNormal;
     - buffers reutilizados;
     - `Cboe41Bajar` para cortar y `Cboe41TopeKBps` para bajar el ritmo.
   - Verificación de la primera rueda: cruzar los cortes del `app_*.log` de ATAS contra las horas de `pythiagex4-cboe.log`.
     - Criterio: los cortes a ±5 s de una bajada no pasan de lo esperable por azar (la fracción del tiempo que se está bajando, unos 2 %).
     - Si pasa, se baja el tope a 512 KB/s y se vuelve a medir.
3. **Cupo de suscripciones con la 3.0 y la 4.1 a la vez.**
   - Las dos piden el mismo libro de NQ (320 contratos cada una) por `OptionsSubscriptionService`.
   - Según el código descompilado (**SIN VERIFICAR en vivo**):
     - el tope es de 3000 suscripciones vivas en total (ráfaga 2000 con recarga de 5/s, consultas 20 con recarga de 1/s);
     - el cupo de 512 es solo del camino del proveedor;
     - el mismo `Security` se comparte entre dueños.
   - Peor caso: 640 suscripciones, menos de 3000. `ReleaseOwned(this)` es por instancia: la 4.1 no suelta lo de la 3.0.
   - Mitigaciones: el desfase de 20 s del primer armado (§2.4.3) y, si el log muestra `refused`, bajar `Tope4Contratos` o sacar la 3.0.
   - Con las dos en el mismo gráfico, las dos pestañas usan la esquina de arriba a la izquierda: conviene usar gráficos distintos.
4. **Memoria.**
   - Estimado, a medir con 7.4.H:
     - fotos NQ de la sesión: unas 1.380 de ~10 KB, ~14 MB;
     - NDX: unas 530 de ~40 KB, ~21 MB;
     - QQQ: ~10 MB;
     - cinta: 1,4 MB;
     - buffers de bajada: ~8 MB.
     - Total: menos de 70 MB.
   - De los 6 días previos solo se guardan las cabeceras de las fotos y las muestras, no las filas.
   - No se hacen reservas de LOH por bajada.
   - Antecedente: ATAS ya se colgó por memoria (MBO DOM, 9-11 GB, el 22-09). 70 MB no cambian eso, pero se mide.
5. **Instancias duplicadas** (la extra que ATAS crea al abrir los ajustes, o dos gráficos de MNQ).
   - Hay un solo descargador por proceso (con mutex), un host por raíz con dueño y un registro perezoso en el primer `OnCalculate` con gráfico.
   - El libro NQ por instancia es como en la 3.0: dos gráficos de MNQ con la 4.1 duplican el armado. Igual que hoy.
6. **Pestaña oculta.**
   - `OnCalculate` no llega con la pestaña oculta (medido antes). Si llegan `OnCumulativeTrade` y los temporizadores está **SIN VERIFICAR**.
   - La descarga y la cuenta no dependen de ATAS.
   - Si faltan ticks o fotos de NQ, la pestaña lo dice ("sin cinta", "libro NQ sin foto hace N min") y el relleno cubre el hueco al volver.
7. **Excepciones tragadas por ATAS.** Todo método de entrada lleva try/catch con log propio y como máximo una línea por minuto por lugar. El descargador y el hilo familia nunca dejan escapar una excepción.
8. **Horas e instantes.**
   - `IndicatorCandle.Time` y `CumulativeTrade.Time` son UTC con Kind Unspecified: se usan los ticks crudos, nunca `ToUniversalTime`.
   - NY siempre con `TimeZoneInfo` "Eastern Standard Time".
   - El 01-11 lo cubre la prueba 7.4.G.
   - El ChartArea es más alto que lo visible: las etiquetas no se anclan al fondo (se conserva el margen de la 4.0.6).
9. **Cambios de CBOE** (formato, bloqueo de Cloudflare, cadena atrasada).
   - Se valida antes de aceptar (§3.2) y se registra cada rechazo.
   - Con 403 o 429 se espera 15 min.
   - No hay fuente alternativa (cero dependencias): la pestaña muestra la edad real y, a partir de 30 min, la pone antes del número.
10. **El workspace.** Si ATAS no resuelve el tipo con la misma `AssemblyVersion`, o rechaza la propiedad `Carpeta4`, el indicador desaparece del gráfico y se agrega a mano una vez. Se mira en el log de arranque y en la captura "después".
11. **El laboratorio cambia después del port.** Por eso se guardan los hashes en la referencia (§7.3). Un cambio en `backtest_familia` obliga a regenerar la referencia y volver a correr el arnés antes de portarlo.
12. **Calendario escrito a mano.**
    - Feriados de NYSE de 2026: 01-01, 19-01, 16-02, 03-04, 25-05, 19-06, 03-07, 07-09, 26-11 y 25-12.
    - Feriados de 2027: 01-01, 18-01, 15-02, 26-03, 31-05, 18-06, 05-07, 06-09, 25-11 y 24-12.
    - Medios días: 27-11-2026 y 24-12-2026.
    - Es **SIN VERIFICAR**: hay que contrastarlo con el calendario oficial de CME y NYSE antes de instalar. El modo paridad usa la lista fija del Python (solo 2026-09-08).
13. **Volver atrás.** Con ATAS cerrado, copiar `_visor_4_0_6/PythiaGexCuatro.dll`. Pero el visor depende de los generadores y de la 3.0. La vuelta real es usar la 3.0, que no se toca.

---

## 9. Pendientes por verificar (no medidos)

1. Si ATAS resuelve el tipo `PythiaGexCuatro.FamiliaCuatro` del `.ws` con `AssemblyVersion` 4.0.0.0 y si ignora `Carpeta4`.
2. Si `OnCumulativeTrade` y los temporizadores de ATAS llegan con la pestaña oculta.
3. Si las suscripciones al mismo contrato se comparten entre la 3.0 y la 4.1 (cero `refused` en el log con las dos corriendo).
4. A qué hora cambia el `prev_day_close` de TQQQ de noche. En SPX fue entre las 00:19 y las 01:36 UTC, una sola noche. Y cuál fue el cierre oficial de TQQQ del 08-10: 80,2186 implícito contra 80,17 por la fórmula NAV.
5. Que `CadenaApi.Filas()` siga llamándose solo desde el temporizador. El diseño no la llama desde otros hilos: la foto de NQ llega por el gancho de viva.
6. Los números de CPU y memoria de §8, que se miden con 7.4.H.
7. Qué hace la capa NDX de la 3.0 sin la base de la clásica la primera noche (§5.4.4).

---

## 10. Orden de trabajo y entrega

1. **F0.**
   - Correr `clonar_4_0.py`: respaldo + `Tres/`.
   - Escribir `Cuatro.cs` mínimo (solo la 3.0 adentro) y compilar.
   - Correr `--verificar`: el diff tiene que mostrar solo los parches de §2.4.
2. **F1.** `Cboe/` y el arnés en sus comparaciones A y B. Congelar la entrada y generar la referencia (§7.2-7.3).
3. **F2.** `Familia/` para NQ, NDX y QQQ, con las comparaciones C, E, F, G y H en verde.
4. **F3.** `Tqqq.cs` y la comparación D.
5. **F4.** `Pantalla.cs` (port de la 4.0.6), las casillas y la compilación final. README y CHANGELOG 4.1.0.
6. **F5. Entrega** (la hace el agente principal; este diseño no instala nada):
   - `herramientas/instalar_4_0.ps1`;
   - las capturas antes/después;
   - revisar `pythiagex4-cboe.log`, `pythiagex4-familia.log` y `pythiagex4-gammahoy.log`;
   - el aviso al operador con §0 y §5.4;
   - medir los cortes de Rithmic durante la primera rueda (§8.2).

---

## Anexo A. La cuenta de NQ, NDX y QQQ (port de `backtest_familia` + `preview_niveles`)

### A.1 Sesión y grilla

- **Paridad.** `dia = fecha(t_utc + 2 h)`.
  - La sesión va de `ini = dia − 2 h` a `fin = dia + 21 h`, con `fin` excluido.
  - Se calcula una vez por minuto, con clave `key = t_ns // 60e9`, desde `ini` hasta `fin − 1 min`.
  - Las fotos de CBOE con `generado` entre las 21:00 y las 22:00 UTC se descartan.
- **Corregido.** La sesión de CME va de las 18:00 NY de la víspera a las 17:00 NY. En verano es lo mismo.

### A.2 Velas, `cierre_conocido` y `Precio`

- **Velas m2:** como en §4.3.
- **`cierre_conocido(t)`** = cierre de la última vela cerrada con `b + 120 s ≤ t`.
  - Da NaN si `t − (b + 120 s) > 4 h`.
  - Ese es el `fut` del minuto.
- **`Precio(ts)`:**
  1. si hay un tick con `t ≤ ts`, `ts − t ≤ 120 s` y `ts ≤ último tick`, ese precio;
  2. si no, la vela m2 que contiene ts, interpolada: `o + (c − o)·(ts − b)/120 s`;
  3. si no hay vela, NaN.
- **TQQQ** usa solo el tick (edad ≤ 120 s), sin el respaldo de la vela.

### A.3 Libro NQ (de viva3)

- **Filtro de la línea:** se descarta la fila si `iv ≤ 0` o si `oi ≤ 0 && vol ≤ 0`.
- **Armado:** `dias = sorted(set(round(dias, 4)))`.
  - Una fila por `(K, índice de dias)`: `[K, v, oi_c, oi_p, iv_c, iv_p, vol_c, vol_p]`. El call pisa las posiciones 2, 4 y 6; el put, las 3, 5 y 7.
- Se deduplica por ts: gana la foto con más filas.
- **Uso en el minuto:** la última foto con `ts ≤ t`, si `t − ts < 300 s`.
- **`corr`:** con cada foto, `cv` = `cierre_conocido(ts + 120 s)` si `ts − fin de vela ≤ 240 s`. Se acumula `cv − futuro`.
  - `med` = mediana si hay ≥ 5 valores; si no, 0.
  - `corr = med` si `|med| > 30`; si no, 0.
  - `S = fut − corr`, `Fut = K + corr` y el zero se corre `+ corr`.

### A.4 Fotos de CBOE

- **Campos:** `generado`, `ts`, `t_dato`, `spot = spot_idx`, `oi_total` = Σ oi_c + Σ oi_p, `err` = `base_error_ticks` y `base_cruda`.
- Se deduplica por ts: gana el `generado` más temprano.
- **C9:** `vigencia = generado + 1500 s`.
  - Si la cadena está congelada (`HH:MM` de `t_dato` en NY ≥ "15:59"), vale hasta el máximo entre eso y las 13:30 UTC siguientes al `generado`. En el modo corregido, las 09:30 NY siguientes.

### A.5 Horizonte "Hoy" (C10) y gamma

```
env = clamp((t − generado)/86400, 0, 2);  dias_env = dias − env
mas = min(dias_env ≥ 0) (0 si no hay);   tope = max(1.0, mas + 0.01)
entra si 0 ≤ dias_env[v] ≤ tope;          T = max(dias_env[v], 1/1440)/365
NQ:  Black-76 SIN descuento   d1 = (ln(S/K) + σ²T/2)/(σ√T),          γ = φ(d1)/(S σ √T)
CBOE: Black-Scholes r=0.0375  d1 = (ln(S/K) + (r + σ²/2)T)/(σ√T),    γ = φ(d1)/(S σ √T)
γ = 0 si S, K, T o σ ≤ 0
```

### A.6 Perfil por lado y filtro de ±3 %

```
esc = 100·S²·0.01
a_vc =  γc·vol_c·esc   a_vp = −γp·vol_p·esc   a_oc = γc·oi_c·esc   a_op = −γp·oi_p·esc
la fila aporta si (a_vc + a_vp ≠ 0) o (a_oc + a_op ≠ 0); por K ascendente: gvC gvP goC goP, gv = gvC + gvP, go = goC + goP
```

- **Conversión:** NQ `Fut = K + corr`, NDX `Fut = K + base`, QQQ `Fut = K·razón`.
- Quedan solo los strikes con `|Fut − fut| ≤ 0,03·fut`, ordenados por Fut.
- **S en el eje de cada libro:** NQ `fut − corr`, NDX `fut − base`, QQQ `fut/razón`.

### A.7 Zero estándar (C5)

```
c0 = round(S/cp)·cp (bancario); n = round(semi/paso); xs = c0 + paso·j, j = −n..n
Cv(x) = x²·Σ[γc(x)·vol_c − γp(x)·vol_p]   Co(x) = x²·Σ[γc(x)·oi_c − γp(x)·oi_p]   (todas las filas del horizonte, sin el ±3 %)
cruce si sign(C_i)·sign(C_{i+1}) < 0; z = x_i + (x_{i+1} − x_i)·(−C_i)/(C_{i+1} − C_i); el z mas cercano a S
```

- **Parámetros:** NQ y NDX, paso 2,5 / semi 300 / cp 50. QQQ, paso 0,05 / semi 7,5 / cp 1,0.
- **Al precio de NQ:** `+corr`, `+base` o `·razón`, según el libro.
- **Caché:**
  - NQ: `(índice de foto, round(S/50))`;
  - CBOE: `(índice de foto, floor((t − generado)/900 s), round(S/50)` en NDX o `round(S/1)` en QQQ`)`.

### A.8 Conversiones

**`robusta(x, piso)`**
- Saca los NaN, toma `med` (la mediana de numpy: con cantidad par, el promedio de los dos del medio) y `mad`.
- Conserva los valores con `|x − med| ≤ max(3·mad, piso)`.
- Devuelve `(mediana, n)`. Si no conserva ninguno, `(med, len)`.

**C7: base de NDX.** Por cada foto, en orden de `generado`:
1. `t_spot = ts − 900 s` y `precio = Precio(t_spot)`.
2. `vivo` = existe una foto previa a ≤ 600 s con un spot distinto (> 1e-9).
3. `muestra = precio − spot`.
4. `en_hora` = la hora NY de `t_spot` está entre 09:35 y 15:59, en día hábil.
5. Si vivo, en hora y con muestra:
   - se agrega a `ruedas[fecha NY]`, con el contrato;
   - se agrega a `roll24`, que se reinicia al cambiar el día NY y guarda las últimas 24.
6. `b24, nb = robusta(roll24, 0,5)`. El modo es "dia" si vivo, en hora y `nb ≥ 5`; si no, "noche".
7. `ref` = la rueda más reciente (contando la de hoy) con ≥ 20 muestras. `ba = robusta(todas, 0,5)`.
8. `base(t)`:
   - modo dia: `b24`;
   - modo noche: `ba·fac`, con `fac = (EXP − t)/(EXP − cierre_ref)` (1 si el denominador es ≤ 0). Exige que `contrato(ref) == contrato(t)`.
   - `cierre_ref` es las 20:00 UTC de la fecha de referencia en paridad, o las 16:00 NY en el modo corregido.
   - `EXP` (paridad): Z6 = 2026-12-18 14:30 UTC.

**C8: razón de QQQ**
- `muestra = Precio(ts − 900 s)/spot`, con vivo a cualquier hora.
- `roll24` se reinicia con cada contrato.
- `r24, nr = robusta(roll24, 0,0002·mediana)`. La razón vale `r24` si `nr ≥ 5`; si no, NaN.
- La foto sin muestra hereda la razón, sin tope de tiempo.
- La razón solo se usa si su contrato es el contrato de t.

**`oi_fresco`** (solo marca "(OI 2s)" en NDX y QQQ). Se recorren las fotos con `hmu = HH:MM(generado)` y `hprev` de la anterior (o "12:00" para la primera):
- si `"13:30" ≤ hmu < "22:00"`: `viejo = false`;
- si no, si `(hmu ≥ "22:00" o hmu < "03:00")`, `!viejo` y (`hprev` en la rueda o pasaron más de 6 h desde la foto anterior): `viejo = true`;
- si no, si `"03:00" ≤ hmu < "13:30"`, `viejo` y el salto relativo de `oi_total` es > 5 %: `viejo = false`.
- En el modo corregido las horas pasan a NY: 09:30, 18:00 y 23:00.

**C2: OI de NQ con fecha**
- Si la sesión es lunes o posferiado, no hay supresión.
- Si no, se recorren las fotos NQ con ts entre `dia − 2 h` y `dia + 3 h 30`:
  - mapa `(K, MM-dd de ts + dias[v], lado)` → OI;
  - hay salto si `|comunes| ≥ 20` y al menos la mitad cambiaron.
- `viejo_hasta`:
  - si hubo salto: `salto`;
  - si no, si la primera foto está entre las 01:55 y las 13:30: sin supresión;
  - si no, si ahora < `dia + 3:30`: `dia + 3:30` ("esperando el salto");
  - si no: `dia + 2:00`.
- `oi_ok(t) = !(dia − 2 h ≤ t < viejo_hasta)`.
- En el modo corregido rige `SaltoOi37`: de 18:00 a 23:30 NY, primera foto de la tarde a las 21:55 NY.

### A.9 Las 24 series

Notación:
- `R = min(0,02·fut, 100)`.
- `argmax` y `argmin` toman la primera ocurrencia, en orden de Fut.
- Los montos son `valor·mult/1e6`, redondeados a 1 decimal, con `mult` = 0,2 en NQ y 1 en NDX, QQQ y la familia.
- MUROS toma gvC/goC para D1 y gvP/goP para D2; MAJORS toma gv/go.

**Reglas**
- **MUROS:** D1 "muro C" = Fut del argmax de gvC entre `|Fut − fut| ≤ R` y gvC > 0. D2 "muro P" = argmin de gvP con gvP < 0. En la versión por OI se usan goC y goP.
- **MAJORS:** D1 "M+" = argmax de go entre `m && go > 0`. D2 "M−" = argmin con go < 0. En la versión por volumen se usa gv.
- **ZEST:** "0G est." = zv o zo de A.7, si no es NaN y `|z − fut| ≤ 300`.
- **ZTP:** sobre gv con g ≠ 0.
  - Una posición interior es isla si su signo difiere de los dos vecinos y `|g_m| < 0,10·min(vecinos)`. Se evalúa sobre la secuencia original y se quitan todas las islas juntas.
  - Cruces entre los consecutivos que quedan, interpolados. Se conservan los de `|z − fut| ≤ 100`.
  - D1 = el menor z > fut; D2 = el mayor z < fut.
- **FAM (C1):** solo en minutos con los tres libros.
  - `Fut_r = round(Fut/5)·5`, con redondeo bancario.
  - Los vectores se escalan por `MULT/100` (NQ 0,2, NDX 1, QQQ 1) y se suman por `Fut_r`.
  - Sobre esa suma se aplica MUROS con R.
- **CONF:** el pool sale, de cada libro presente, de MUROS_vol, MAJORS_vol y ZTP_vol (D1 y D2), incluido el ZTP de QQQ.
  - Se ordena por precio y después por libro.
  - Se agrupa mientras `p_j − p_i ≤ 3`. Un grupo con ≥ 2 libros distintos da su promedio como nivel si está a ≤ 100 de fut, con etiqueta `"D" + (n+1)`.

**Las series, con su color**
- **Recomendadas:**
  - MUROS_NQ_vol `#4db5e4`
  - MAJORS_QQQ_oi `#e2b872` (±20)
  - CONF_vol `#f3d35a`
  - MAJORS_NQ_vol `#6cc3ff`
- **Familia:**
  - MUROS_NQ_oi `#9a98d6` (solo con `oi_ok`)
  - MUROS_NDX_vol `#2fb3a8`
  - MUROS_NDX_oi `#8de031`
  - MUROS_QQQ_vol `#dabe6f` (±20)
  - MUROS_QQQ_oi `#ff9f43` (±20)
  - MAJORS_NQ_oi `#b9a4ff` (solo con `oi_ok`)
  - MAJORS_NDX_vol `#4fd1c5`
  - MAJORS_NDX_oi `#a3e635`
  - MAJORS_QQQ_vol `#f6c177` (±20)
  - FAM_MUROS_vol `#d04ad0`
  - FAM_MUROS_oi `#e879f9` (solo con `oi_ok` de NQ)
- **Zero:**
  - ZEST_NQ_vol `#c9d4e3`
  - ZEST_NQ_oi `#94a3b8` (solo con `oi_ok`)
  - ZEST_NDX_vol `#7dd3fc`
  - ZEST_QQQ_vol `#fcd34d`
  - ZTP_NQ_vol `#f472b6`
  - ZTP_NDX_vol `#fb7185`
- **3.0:**
  - TRES_NQ `#8a96a8`
  - TRES_NDX `#5f6b7d`
  - TRES_QQQ `#7c6f57`

### A.10 Salida para la pantalla

- **Historia.** La vela m2 que abre en T toma `niv[T/60000]`; si no está, `niv[T/60000 − 1]`.
- **Actuales** = el último minuto con algún libro.
- **Edad de cada fuente:**
  - NQ: desde el ts de la foto;
  - NDX y QQQ: desde `t_dato`;
  - la familia: desde el `t_dato` más viejo de los libros que entran.
  - Se marca `viejo` si pasa de 1800 s.
- **`oi_viejo`:**
  - NQ: `!oi_ok`;
  - NDX y QQQ: `!oi_fresco`;
  - la familia: si algún libro no está bien.
- **Strike que se muestra:** NQ y NDX `p − conv`, QQQ `p/conv`, TQQQ K.
- **Doble eje, con la conversión del último minuto:**
  - NDX `NQ = NDX + base`, aceptada si `|base| < 2000`;
  - QQQ `NQ = QQQ·razón`, aceptada entre 30 y 60;
  - TQQQ `NQ = s·K + c`, aceptada si `20 ≤ s ≤ 1000`.

## Anexo B. TQQQ x3 (port de `tqqq_vivo.py`, con los arreglos del modo corregido)

### B.1 Cadena y perfil

- La cadena flaca es la de C.1, con días ≤ 8.
- Los días se cuentan desde la hora del dato: `d_v = (venc − t_dato)/86400`.
- Entra **solo el vencimiento más cercano:** `0 ≤ d ≤ min(d ≥ 0) + 0,01`, sin `max(1, …)`.
- `T = max(d, 1/1440)/365`. Black-Scholes con r = 0,0375 y `esc = S²`, con `S = spot_idx` de la foto: no se reprecia con NQ.
- `gvc`, `gvp`, `goc` y `gop` por K, como en A.6.

### B.2 Conversión `NQ = s·K + c` (banda ±4)

**El `s`**
- **Corregido:** `s = N0/(3·P0)`, con `P0 = prev_day_close` de TQQQ y `N0 = prev_day_close` de `_NDX`.
  - Esos valores solo se aceptan si la cadena es de la rueda o del premarket de D: fecha NY de `data.last_trade_time` = D y `|current − prev_day_close − price_change| < 0,01`.
  - Después del cierre se usa `close`, si `data.last_trade_time` es 15:59 NY o más.
  - Validado el 08-10: P0 83,62 y N0 31.160,08 dan s = 124,213.
- **Paridad:** el `s_del_dia` del Python (la última foto de la sesión anterior con `t_dato ≤` 20:00 UTC). Si falta, una recta libre con ≥ 10 puntos.

**El `c`, por foto**
1. `px = PrecioSoloTick(ts − 900 s)`.
2. `vivo` = alguna de las 12 fotos previas a ≤ 600 s tiene un spot distinto.
3. La muestra cuenta en la rueda: 09:33-16:00 NY en el modo corregido, o 13,55-20,0 UTC en paridad.
4. Con vivo, `px` válido y `spot > 0`: muestra `px − s·spot`, conservando las últimas 24.
5. `c_i = robusta(muestras, 1,0)` si quedan ≥ 5; si no, NaN.
6. Se calcula en todas las fotos, así que después del cierre queda congelado.

### B.3 Las 5 series

Sobre `q` = los strikes con `|K − S| ≤ 0,05·S`. El precio en NQ es `round(s·K + c_i, 2)`.

- **T_DOMS_vol** `#ffd54f` (±4): los 2 strikes de mayor |gv| > 0.
- **T_MUROS_vol** `#26c6da` (±4): "muro C" = K del max(gvc) si es > 0; "muro P" = K del min(gvp) si es < 0.
- **T_MUROS_oi** `#ba68c8` (±4): igual, con goc y gop.
- **T_ZERO_oi** `#b0bec5` (±4):
  - filas con `|K − S| ≤ 0,06·S` y go ≠ 0;
  - cruces entre vecinos, interpolados; el más cercano a S;
  - sin islas y sin C5.
- **T_DOMS_raz** `#ff7043` (banda 0, solo para comparar): `precio = K·nq/T_eq`, con `T_eq = (nq − c)/s` y `nq = PrecioSoloTick(fin de la vela − 1 s)`.

### B.4 Historia y actuales

- Velas m2 desde las 13:30 UTC del día (09:30 NY en el modo corregido) hasta ahora.
- La foto de cada vela es la última con `generado ≤ k + 120 s` (mira **el cierre** de la vela, a diferencia de NQ/NDX/QQQ). Vale si `k + 120 s − generado ≤ 1500 s` y `c_i` no es NaN.
- Cada vela usa los `(s, c_i)` de **su** foto: el pasado no se mueve.
- La foto es `congelada` si la hora de `generado` es > 20,3 UTC (16:18 NY en el modo corregido).
- Los actuales son los de la última vela con niveles. La edad se cuenta desde `t_dato`.
- El rótulo es "TQQQ 82,0 x3: NQ = 124,21 x K + 21012,1 (±4)". Si el dato tiene más de 30 min, la edad va antes.
- Doble eje: `K = (NQ − c)/s`.

### B.5 Noche: solo con `Tqqq41AnclaNoche` (SIN VALIDAR, default false)

- **Fórmulas:** `s' = N0'/(3·P0')` y `c' = NQ_1600 − N0'/3`.
  - `NQ_1600` = último tick ≤ 16:00:00 NY. Si no hay, el cierre de la vela del gráfico que termina a las 16:00 NY (del anillo de §2.7).
  - `N0'` = el spot de `_NDX` congelado (la primera foto con ts ≥ 16:15 NY que se repite).
- **Modos:**
  - **A:** la rueda.
  - **Entre las 16:00 NY y la llegada de N0':** sigue A, con el rótulo "esperando NDX congelado".
  - **B:** con `P0'` implícito = `(NQ_1600 − c_cierre)/s_D`, banda ±8.
  - **C:** con `P0'` oficial, cuando cambió `prev_day_close`, banda ±5. Dura hasta que la rueda de D+1 junta 5 muestras.
  - En D+1, `s = s'`.
- **Validación:** una sola noche (07→08-10). C quedó a +0,4 pts y B entre +3,5 y +3,8.
- Cada noche se guarda en `tqqq-cierres.json` el error de B y de C contra el primer `c` medido de D+1, para juntar muestra.
- En este modo, la ventana de fotos de la sesión es continua (22:00 → 22:00 UTC). Sin él, la de paridad.
- Los medios días (27-11 y 24-12) se detectan por `ultimo_trade` de TQQQ congelado antes de las 15:59 NY.

### B.6 `tqqq-cierres.json`

Por fecha NY guarda:
- P0 y N0, con su fuente y su hora;
- `NQ_1600`, `c_cierre` y `s`;
- las anclas B y C con sus errores.

## Anexo C. CBOE: `construir`, `medir` y la línea flaca

### C.1 `construir` (port de `pythiagex/cadena_atas.py`)

**Datos de partida**
- `S = data.current_price` y `ahora = generado`.
- Símbolo: `^([A-Z]+)(\d{2})(\d{2})(\d{2})([CP])(\d{8})$`.
  - `K = entero/1000`.
  - Vencimiento = 16:00 NY de esa fecha, en UTC: 20 UTC en verano y 21 UTC en invierno.
  - NDX y NDXP se tratan igual. Eso incluye la mezcla del tercer viernes: se mide y se replica.

**Filtros, en este orden**
1. `0 ≤ dias ≤ 14` y `|K − S|/S ≤ 0,05`;
2. se descarta si OI y volumen son ambos 0;
3. se descarta si iv ≤ 0.

**Agrupado**
- Por `(K, fecha)`: OI y volumen **se suman** por lado. La IV es la del último contrato visto en orden de archivo, redondeada con `round(iv, 4)`.
- Vencimientos ordenados por días, cada uno `{f, dias: round(·, 4)}`.
- Filas `[K, iv_idx, oi_c, oi_p, iv_c, iv_p, vol_c, vol_p]`, ordenadas por `(K, fecha)`.
- `spot_idx = round(S, 2)`, `campos = "strike,venc,oi_call,oi_put,iv_call,iv_put,vol_call,vol_put"` y `horizonte_dias = 14`.

### C.2 `medir` (port de `pythiagex/base.py`)

**Fechas**
- Trimestral = tercer viernes de marzo, junio, septiembre o diciembre. Pasa al siguiente 8 días antes de vencer. Hoy da 2026-12-18.
- `vencs` = todas las fechas de la cadena cruda, incluida la vencida. `cercano = vencs[0]`.
- `hasta` = los primeros 10 vencimientos hasta el trimestral, más el trimestral.

**Precios**
- `mid = (bid + ask)/2`, solo si bid y ask ≠ 0.
- `last = last_trade_price` si es ≠ 0.
- Si `(K, fecha, lado)` se repite, gana el último en orden de archivo.

**Forward por vencimiento**
- Promedio de `K + C − P` sobre los 12 strikes más cercanos a S. El empate se resuelve por orden de aparición.
- Primero con mid; si quedan menos de 3 strikes, con last.

**Recta y base**
- Mínimos cuadrados sobre `((v − cercano).days, forward)`, con ≥ 3 puntos.
- `contado = round(a, 2)`.
- `base_cruda = round(fwd_trimestral − contado, 2)`.
- `base_error_ticks = round(max|residuo|/0,25, 1)`.
- `base` y `base_confiable` necesitan la curva del Tesoro (una descarga externa): se escriben `null` y `false`.

### C.3 Línea flaca (`archivar_cadena.linea_flaca`)

**Claves, en este orden:** `generado`, `cadena_ts`, `edad_min`, `spot`, `base`, `base_confiable`, `base_cruda`, `base_error_ticks`, `sub`, `bajada`, `cadena`.
- `sub`: `current_price`, `close`, `prev_day_close`, `price_change`, `last_trade_time`, `bid` y `ask`.
- `bajada`: `url`, `http`, `bytes_gz`, `bytes` y `ms`.
- `cadena`:
  - `ts`, `spot_idx`, `ultimo_trade`, `horizonte_dias` y `campos`;
  - los vencimientos completos hasta 14 días;
  - las filas solo con `0 ≤ dias ≤ 8`, sin reindexar;
  - `dias_max_archivo: 8.0`.

`generado` y `cadena_ts` tienen que caer dentro de los primeros 500 caracteres: el laboratorio los busca con una regex ahí.
