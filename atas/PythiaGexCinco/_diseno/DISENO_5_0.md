# PythiaGex 5.0 — Gamma ES (diseño, 27-10-2026)

Pedido del operador (textual): "copia y creame un indicador phitiagex gamma v5 como el de nq pero esta vez para es mes spx spy todo bien
analizado auditado dibujado utiliza los maximos recursos disponibles". Y: "tenes todo permitido total autorizacion ... se autonomo".

Estado de partida medido hoy: la 4.1 (PythiaGexCuatro 4.1.6g/4.1.7, DLL sha256 8cb1c5cb…, instalada el 13-10) está en el gráfico
"MNQZ6 1m" y TAMBIÉN en "MES 1m Chart", donde no dibuja nada: cartel "los niveles estan en precio de NQ/MNQ; este grafico es MES: no dibujo
nada" (captura `capturas/2026-10-27_0000_antes_MES_con_4_1.jpg`). Su núcleo 3.0 sí corre ahí (log: raiz=ES radioDom=25 radioDibujo=40).

## 1. Qué es la 5.0

Un indicador nuevo, autocontenido (regla del operador: uno solo por función, nada externo), clon de la 4.1 con los libros del S&P:

| 4.1 (NQ)                      | 5.0 (ES)                                                                 |
|-------------------------------|--------------------------------------------------------------------------|
| NQ: opciones sobre el futuro (Rithmic, ×20) | ES: opciones sobre el futuro ES (Rithmic, ×50), gráfico MESZ6 |
| NDX: CBOE `_NDX` (archivo "NQ"), ×100, base C7 | SPX: CBOE `_SPX` (archivo "ES", raíces SPXW diaria y SPX mensual AM), ×100, base C7 |
| QQQ: CBOE `QQQ`, ×100, razón C8 | SPY: CBOE `SPY`, ×100, razón C8 (ojo ex-dividendo trimestral) |
| TQQQ ×3                        | se elimina (no hay equivalente pedido)                                   |
| Réplica 2.0 (QQQ, NDX)         | Réplica 2.0 en ES (SPY, SPX): lo que la 2.0 dibuja en un gráfico de ES   |
| Réplica clásica (NDX 0Γ, D1-D3) | Réplica clásica en ES (SPX 0Γ, D1-D3) con su base de la rueda (base-rueda-ES) |
| Capas 3.0 TRES_NDX/TRES_QQQ    | Capas 3.0 TRES_SPX/TRES_SPY (el núcleo hoy las tiene "pendiente" en ES)  |

Identidad: `atas/PythiaGexCinco`, `PythiaGexCinco.dll`, tipo `PythiaGexCinco.GammaCinco`, "PythiaGex 5.0 - Gamma ES", categoría
"PythiaGex 5.0", AssemblyVersion 5.0.0.0, datos `%APPDATA%\ATAS\PythiaGex5`, logs `pythiagex5-*`. Convive con la 4.1 en el mismo ATAS
(otro ensamblado: estáticos propios; mutex y archivos propios; CadenaApi arranca a +50 s para no armar a la vez que la 4.1).

## 2. Cómo se construye

1. Clon mecánico: `herramientas/clonar_5_0.py` (hecho, commit f953afe83 + CRLF): renombres de infraestructura en todo y de tokens
   NQ/NDX/QQQ→ES/SPX/SPY solo en la familia; el núcleo 3.0 solo recibió los identificadores compuestos para compilar. Compila.
2. Sacar TQQQ (agente, `_diseno/QUITAR_TQQQ.md`).
3. Mediciones (agente, `_diseno/MEDICIONES_ES.md` + `constantes_medidas.json`) e inventario de lo NQ-específico (agente,
   `_diseno/INVENTARIO_4_1.md`).
4. Adaptación semántica por paquetes con archivos disjuntos (constructores en paralelo, compilan con `herramientas/build_serial.ps1`):
   - P1 libro ES + `familia/comun` + `fam/ReglasFam` (constantes en puntos, multiplicador 50, zero estándar, islas, OI de CME);
   - P2 SPX + `cboe` + `clasica` (SPXW/SPX, calendario, base C7 con carry de SPX, tamaño de bajada, réplica de la clásica en ES);
   - P3 SPY + `replica20` (razón C8, ex-dividendo, banda; réplica de la 2.0 en ES);
   - P4 núcleo 3.0: capas SPX/SPY en ES (GammaHoyTresCapas/MajorOi/Rayas, ajustes Raya3Spx*/Raya3Spy*);
   - P5 catálogo + pantalla + integración + posiciones: textos honestos (NADA medido en NQ se presenta como medido en ES), eje doble
     Ninguno/SPX/SPY, defaults, carteles.
5. Arneses ES (`atas/_test_cinco`), revisión adversarial, auditor independiente (`herramientas/auditar_familia.py --perfil ES`).
6. Instalar (`herramientas/sembrar_5_0.py` una vez + `herramientas/instalar_5_0.ps1`), agregar al gráfico "MES 1m Chart" en lugar de
   la 4.1 (que ahí no dibuja), captura antes/después, y comprobación fehaciente: cada raya visible recalculada por fuera.

## 3. Reglas que no se negocian

- Las constantes en puntos de NQ NO se copian: se reescalan con el factor MEDIDO (sección 4) o se justifican una por una.
- Lo que la 4.1 midió en NQ (porcentajes de rebote, "candidata principal", Holm) NO vale para ES: en la 5.0 cada serie dice
  "SIN MEDIR EN ES" hasta que haya bitácora de ES.
- Todo nivel de SPX/SPY se dibuja convertido a ES con su base/razón a la vista en la pestaña (fuente, edad, conversión).
- El GEX describe régimen, nunca dirección.

## 4. Constantes (medición rápida propia 27-10 01:00 ART; MEDICIONES_ES.md del agente las confirma o corrige)

Medido (`scratchpad/escala_rapida.py`, velas m1 de la cache de ATAS `laboratorio/dom/ronda3/velas`, 25 sesiones comunes MES/MNQ del
04-08 al 07-09; cadenas de cboe-local):
- precio NQ/ES = 3,824 (3,80-3,87); rango de sesión NQ/ES = 6,56 (p25 5,45, p75 7,72); rango de 30 min NQ/ES = 7,43.
- base ES−SPX (campo `base` confiable de cboe_local, ESZ6): 37,72 (22-10, n=299), 36,56 (23-10, n=281), 35,25 (26-10, n=22).
- SPX/SPY = 10,032 (22-10, misma hora de cadena); ES/SPY ≈ 10,07-10,08.

Regla: las DISTANCIAS DE PRECIO se reescalan por el cociente de precios (÷4, redondeado a ticks de 0,25), salvo las que salen de la
ESTRUCTURA DE STRIKES (se reescalan por el paso de strike) y las que ya tienen valor de ES en la 2.0/clásica (las réplicas copian lo que
esas hacen en ES). El cociente de volatilidad (6,6-7,4) se descarta para radios: achicaría el radio a ~15 pts (3 strikes de ES) y dejaría
afuera muros que la 3.0 en ES (radioDom = 25, lo que el operador ya ve en ES) sí mira.

| constante (4.1 NQ) | 5.0 ES | por qué |
|---|---|---|
| RADIO 100 pts (Seleccion, ReglasFam: muros/majors/ZTP/CONF) | 25 | ÷4; igual al radioDom ES de la 3.0 |
| ZEST_MAX 300 / grilla ±300, paso 2,5 (NQ, NDX) | 75 / ±75, paso 0,5 | ÷4 (paso a 2 ticks) |
| grilla QQQ 0,05 USD ±7,5 | SPY 0,05 USD ±7,5 | 0,05 USD de SPY = 0,5 pt de ES; ±7,5 USD = ±75 pts |
| CONF_PTS 3 | 3 (corregido 01:40) | paso de strike: ES y SPX listan cada 5, IGUAL que NQ (29270/29275/29280 medido): con 0,75 un nivel de ES y uno de SPX llevado a ES (desplazado ~1,4 mod 5 por la base) casi nunca coincidirían |
| FAM_GRILLA 5 | 5 (corregido 01:40) | un strike, como en NQ |
| RADIO por libro | ES 25, SPX 20, SPY 20 | cobertura equivalente medida (MEDICIONES_ES.md: ES 24-26, SPX 17 vol / 20 OI) |
| carry r−q | 3,4 % (medido) | la fórmula 3,75 % − 1,2 % queda 25-43 % corta; cota base: \|base − fut×0,034×días/365\| ≤ max(25 %, 3) |
| 0DTE vencido de noche | excluido | CBOE lo sigue mandando con bid/ask en 0: metía 6-13 pts de error en la base por forwards |
| SPY | muestras solo 04:00-20:00 NY; reinicio el 3er viernes trimestral 04:00 NY | a las 20:00 el spot vuelve al cierre oficial (36 pts de error); ex-div 18-09 = 15,6 pts |
| banda QQQ ±20 (medio strike de QQQ = 41/2) | banda SPY ±5 | medio strike de SPY (≈10 pts de ES) |
| túnel techo/piso ≤ 40 | ≤ 10 | ÷4 |
| multiplicador NQ 20 | ES 50 | producto |
| NDX ×100, QQQ ×100 | SPX ×100, SPY ×100 | producto |
| dividendo NDX 0,6 % | SPX 1,25 % | producto (aprox.; la base se MIDE, el carry solo acota) |
| razón QQQ 30-60 (cota) | ES/SPY 9,95-10,25 | medido C8 10,08-10,13 (15-09 a 26-10) |
| base NDX (cota contra carry) | ver "carry r−q" abajo | medido 35-38 contra carry teórico viejo ~27 |
| porcentajes relativos (2 %, FILTRO 3 %, ISLA 0,10), retrasos 900/960 s, vidas | iguales | no dependen del producto |
Réplicas: la 2.0 y la clásica en ES usan SUS parámetros de ES (leerlos en su código: GammaHoyNucleo de PythiaGexNiveles/PythiaGexDos).
TODO lo de esta tabla es SIN VALIDAR en ES como nivel operable: solo fija la escala.
