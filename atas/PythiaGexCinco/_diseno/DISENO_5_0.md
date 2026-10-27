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

## 4. Constantes (se completa con MEDICIONES_ES.md)

PENDIENTE: factor de escala NQ→ES, radios, grillas, bandas, tolerancias, cotas de base/razón, pasos de strike, tamaño de bajada.
