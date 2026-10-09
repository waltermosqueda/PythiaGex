# PythiaGex 4.1 — plan de construcción en paralelo (contratos del agente principal, 08-10-2026)

Objetivo: UN indicador (PythiaGexCuatro.dll, "PythiaGex 4.0 - Gamma Familia") que calcula todo adentro: el libro de NQ por Rithmic (del
clon de la 3.0), la cinta del MNQ, las cadenas de NDX/QQQ/TQQQ bajadas por él mismo de CBOE, las 29 series de la vista previa con la misma
cuenta, y la UI aprobada (4.0.6). Cero programas externos en tiempo de ejecución.

El CLON ya está hecho por el agente principal (`herramientas/clonar_4_0.py`, compila). Contratos: `_contratos/Contratos.cs`
(namespace `PythiaGexCuatro.Familia`). Especificación de la cuenta: la del informe "cuenta vista previa" (en el prompt). Referencia de
paridad YA generada: `atas/_test_cuatro_paridad/referencia/ref-2026-10-07.json` y `ref-2026-10-08.json`.

## Propiedad de archivos (nadie toca archivos de otro)

| Constructor | Escribe SOLO en | Implementa |
|---|---|---|
| B1 clon | `atas/PythiaGexCuatro/*.cs` (los clonados) | terminar el clon: ganchos `// GANCHO-4.1`, defaults de la 3.0 apagados, relleno de cinta apagado, desfase de armado, `ILibroNq` y `ICinta` como adaptadores sobre CadenaApi/la cinta del clon (archivo nuevo `AdaptadoresFamilia.cs` en la raíz del proyecto) |
| B2 descargador | `_modulos/cboe/*`, `atas/_test_cuatro_paridad/cboe/*` | `IFuenteCboe` (bajada, construir, base por forwards, archivo propio en `%APPDATA%/ATAS/PythiaGex4/cboe`) |
| B3a NQ + común | `_modulos/familia/comun/*`, `_modulos/familia/nq/*`, `atas/_test_cuatro_paridad/nq/*` | gamma BS/Black-76 sin descuento, perfil por lado, `LibroMinuto` builder, zero estándar C5 con su caché, selección MUROS/MAJORS/ZTP (islas) + `ILibroMinutero` NQ (corr, C2) |
| B3b NDX | `_modulos/familia/ndx/*`, `atas/_test_cuatro_paridad/ndx/*` | C7 (base sincronizada), C9, oi_fresco, `ILibroMinutero` NDX |
| B3c QQQ | `_modulos/familia/qqq/*`, `atas/_test_cuatro_paridad/qqq/*` | C8 (razón sincronizada), C9, oi_fresco, `ILibroMinutero` QQQ |
| B3d motor | `_modulos/familia/fam/*`, `atas/_test_cuatro_paridad/fam/*` | `IMotorFamilia`: grilla por minuto, CONF, FAM (C1), montos, catálogo de 29 series, historia m2, actuales, persistencia propia en PythiaGex4, TRES_* desde las capas del clon |
| B4 TQQQ | `_modulos/tqqq/*`, `atas/_test_cuatro_paridad/tqqq/*` | `ICalculoTqqq` |
| B5 pantalla | `_modulos/pantalla/*` | la UI 4.0.6 sobre `FotoFamilia` (sin json): rayitas, etiquetas, pestaña, doble eje, control de vencimiento, propiedades |

El INTEGRADOR (agente principal) copia los módulos al csproj, conecta los ganchos y compila.

## Reglas comunes
- Mientras B3a no publique `comun/`, los demás programan contra las firmas de la especificación y después alinean (B3a publica primero
  `comun/Gamma.cs`, `comun/Perfil.cs`, `comun/Seleccion.cs`, `comun/ZeroEstandar.cs`).
- Cada módulo compila solo (csproj de prueba propio) y tiene un arnés re-ejecutable que imprime la paridad por serie contra la referencia.
- Criterio de paridad: strike elegido idéntico; precio en NQ a ≤ 0,01; diferencias explicadas una por una (p. ej. gamma Black-76 con
  descuento en la 3.0 vs sin descuento en la vista previa: usar SIN descuento).
- Prioridad baja para todo lo que corra en Python; nada de rodar la cuenta pesada de la vista previa: la referencia ya está generada.
