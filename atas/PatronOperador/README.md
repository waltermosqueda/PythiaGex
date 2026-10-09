# Patron Operador — EN PRUEBA, no validado

Indicador de ATAS que marca en vivo, sobre el grafico de MNQ de 2 minutos, las situaciones de las
reglas que salieron de la ronda 9. **No opera nada, no manda ordenes y no toca ninguna posicion.**

## Lo primero, que es lo que mas importa

Ninguna de estas reglas gana al costo. El juez de la ronda 9 las corrio sobre 24 sesiones que el
operador **no** opero (31-07 a 04-09, catorce de la reserva que nadie habia mirado):

| lo que marca | que es | neto en el juez | contra el azar |
|---|---|---|---|
| CONTRA (G5) | el precio corrio 3 pts **en contra** en los ultimos 2 min | −0,779 pts/op (n 22.447) | **+0,260** (t +4,67) |
| QUIETO+P (R3) | no viene persiguiendo **y** la punta no esta cargada en contra | −0,757 pts/op (n 22.397) | **+0,206** (t +3,16) |
| QUIETO (R2) | no viene persiguiendo | −0,765 pts/op (n 32.259) | **+0,217** (t +4,56) |
| PERSIGO (G1/G2) | el precio ya corrio 2 pts en 30 s | −1,41 / −1,30 pts/op | **−0,54 / −0,34** |
| PERSIGO!! (G4) | ademas pegado al extremo del minuto y con la cinta agrediendo | −1,437 pts/op (n 8.555) | **−0,502** |

La vuelta cuesta **0,948 puntos**. La mejor produce 0,26. Las dos ultimas son **anti-senal**: en el
mismo minuto, la misma franja y el mismo lado, tirar una moneda daba mejor.

Por eso el indicador lleva el rotulo `EN PRUEBA: no validado` arriba a la izquierda, con la tasa
medida de cada regla prendida al lado. Ese rotulo no es decoracion: sin el, una flecha verde en la
pantalla se parece demasiado a una recomendacion.

## Cuantas marcas vas a ver (medido, no estimado)

Sobre 3 sesiones grabadas (`r9_p_04_densidad.txt`), por sesion:

| regla | senales | con "una por vela" | con "solo cuando cambia" |
|---|---|---|---|
| CONTRA | 822–919 | 603–667 | 380–413 |
| QUIETO+P | 885–910 | 738–768 | 599–604 |
| QUIETO | 1.338–1.344 | 1.005–1.017 | 666–684 |
| PERSIGO | 670–731 | 584–619 | 484–517 |
| PERSIGO!! | 293–341 | 273–313 | 252–292 |

Con lo que viene prendido de fabrica (CONTRA + PERSIGO!!, una por vela y solo cuando cambia) son
unas **670 marcas por sesion** sobre 690 velas de 2 min: casi una por vela. Eso **es** el resultado
de la ronda 9: estas reglas describen el mercado, no dicen cuando entrar. El operador abre 36 ciclos
por sesion.

## Instalacion

1. Compilar (ya esta compilado en `bin/Release/PatronOperador.dll`, 31 KB):

   ```
   cd "C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\atas\PatronOperador"
   dotnet build -c Release
   ```

2. Copiar **un solo archivo**: `bin\Release\PatronOperador.dll` → `%APPDATA%\ATAS\Indicators\`.
   No copiar el `.deps.json` ni ningun DLL de ATAS. El archivo se puede pisar con ATAS abierto.

3. **Reiniciar ATAS.** En `%APPDATA%\ATAS\Logs\app_<fecha>.log` tiene que aparecer
   `Created library '...PatronOperador.dll'`. Si no aparece, el problema es el DLL, no la pantalla.

4. En el grafico de **MNQZ6 de 2 minutos**: `Indicators` → buscar **"Patron Operador (EN PRUEBA)"**
   (grupo *PythiaGex 2.0*) → **un solo clic** para seleccionarlo → el boton de abajo a la derecha
   cambia a **`Add to chart`** → apretarlo (el contador `Added (N)` tiene que subir) → `Apply`.
   Doble clic no alcanza y arrastrarlo tampoco.

5. `Workspaces` → `Save` → `Yes`.

6. Tarda **unos 130 segundos** en arrancar: necesita 2 minutos de cinta para poder calcular el
   retorno de 120 s. Mientras tanto el rotulo dice `calentando: N s de cinta`. Si despues de un
   minuto dice `sin cinta: el conector todavia no mando ninguna orden`, el problema es el feed.

### Ajustes, y con que valores viene

| ajuste | viene | por que |
|---|---|---|
| CONTRA (G5) | **prendido** | la senal con mas ventaja medida sobre el azar |
| QUIETO+P (R3) | apagado | 740 flechas por sesion: sola tapa el grafico (igual se registra) |
| QUIETO (R2) | apagado | 1.010 flechas por sesion |
| PERSIGO (G1/G2) | apagado | 590 flechas por sesion |
| PERSIGO!! (G4) | **prendido** | es la foto mas parecida a su gatillo y la peor de las 17 |
| Una marca por vela y por regla | **prendido** | solo limita el dibujo |
| Marcar solo cuando la regla CAMBIA de lado | **prendido** | solo limita el dibujo |
| Alerta sonora | **apagada** | sonaria casi una vez por minuto |
| Registrar las senales en CSV | **prendido** | es para que esto se pueda medir despues |
| Registrar tambien las reglas apagadas | **prendido** | asi el registro no depende de lo que se ve |

Nada de esto manda ordenes ni toca la posicion. El indicador no tiene ningun camino de ejecucion.

## Lo que graba

`%APPDATA%\ATAS\PythiaGex2\patron\senales-<fecha UTC>.csv`, una fila por senal de **las cinco**
reglas (se dibujen o no), con hora UTC, instrumento, regla, lado, si se dibujo, y los rasgos del
momento: `mid, bid, ask, spread_ticks, desbal_punta, ret_20s, ret_30s, ret_120s, pos_rango_60s,
desbal_10, m2p_cuerpo, eventos_cinta, seg_cinta, tope_s, barra`.

Se mide despues con
`laboratorio/dom/ronda9_patron/r9_p_03_medir_senales.py` (descriptivo: cuenta y mide con el mismo
motor del juez, entrada el segundo siguiente y salida a 60 s con costo 0,948). **No es un
veredicto**: para eso hace falta un pre-registro escrito antes de mirar los numeros, como el de
`ronda8/adelante_diario.py`, que no se toca.

## Como calcula, y como se comprobo

`PatronNucleo.cs` es C# puro, sin una sola referencia a ATAS: come la cinta orden por orden
(`CumulativeTrade`, con la punta despues de cada orden, que es exactamente lo que grabo la sonda de
Flujo Claro en las 37 sesiones) y saca los mismos rasgos causales que el Python de la ronda 9. La
grilla es un momento cada 60 s anclado al minuto UTC redondo, con el mismo motor sin solapes del
juez (`r9_j_lib.elegir_entradas`).

**Paridad:** `arnes/` compila ese mismo archivo en una consola y lo corre sobre 3 sesiones grabadas
(12-08 del juez, 15-09 y 18-09 operadas). Resultado en `r9_p_02_salida.txt`:

- **20.610 de 20.610 senales identicas = 100,000 %** (3 sesiones × 5 reglas × 1.374 momentos);
- 18.479 entradas del motor sin solapes, todas iguales;
- los rasgos coinciden al bit salvo el redondeo del CSV (peor diferencia 5,7·10⁻⁸).

Para rehacerlo:

```
cd "...\PythiaGex\laboratorio\dom\ronda9_patron"
python r9_p_01_exportar.py
cd "...\PythiaGex\atas\PatronOperador\arnes"
dotnet run -c Release -- "C:\Users\wmx_7\AppData\Local\Temp\patron_paridad" 2026-08-12 2026-09-15 2026-09-18
dotnet run -c Release -- --vivo "C:\Users\wmx_7\AppData\Local\Temp\patron_paridad" 2026-08-12 2026-09-15 2026-09-18
cd "...\PythiaGex\laboratorio\dom\ronda9_patron"
python r9_p_02_comparar.py
```

## Lo que NO se toco

Produccion (`atas/PythiaGexNiveles`) y el clon (`atas/PythiaGexDos`) quedaron intactos: esto es un
proyecto nuevo con DLL propio. No se abrio ATAS, no se toco su configuracion ni el workspace, y no
se copio ningun DLL a `%APPDATA%\ATAS\Indicators` (ese paso lo hace el operador). Lo unico que este
indicador escribe es su propio CSV en `PythiaGex2\patron` y su log en
`%APPDATA%\ATAS\pythiagex2-patron.log`.
