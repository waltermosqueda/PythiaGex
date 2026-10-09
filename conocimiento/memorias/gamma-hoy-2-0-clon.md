---
name: gamma-hoy-2-0-clon
description: "REGLA del operador (18-09-2026): la produccion (atas/PythiaGexNiveles, 'PythiaGex - Gamma Hoy') NO SE TOCA MAS; todo arreglo y feature va al clon 'PythiaGex 2.0' (atas/PythiaGexDos, ensamblado PythiaGexDos.dll, datos en %APPDATA%\ATAS\PythiaGex2, logs pythiagex2-*). Si 2.0 se rompe, el vuelve a agregar la original. Flujo Claro sigue solo en prod."
metadata:
  type: project
---

**Por que:** "demasiadas veces rompimos el indicador prod original; prefiero dejarla en paz y enfocarnos en la nueva". Creado con
`herramientas/clonar_2_0.py` (copia de Gamma Hoy 1.11d y dependencias, sin Flujo Claro; `--pisar` lo rehace desde prod y PIERDE los cambios
del clon: no usar salvo para empezar de nuevo). El instalador `reiniciar_e_instalar.ps1` copia los dos DLL. Los ajustes NO se comparten
(ATAS guarda por nombre de indicador): 2.0 arranca con sus defaults. Dos Gamma Hoy en el mismo grafico duplican suscripciones a Rithmic.
Flujo Claro (prod) lee la estela de PROD (`PythiaGex\estela`): si algun dia se saca la original de todos los graficos, hay que apuntar
Flujo Claro a `PythiaGex2\estela`.

**Primeros arreglos en 2.0 (18-09):** F1 archivador de cadenas repetidas (sello sin base viva), F2 razon NQ/QQQ con spot congelado de noche,
F3 rearme a 5 min cuando el armado de la viva falla (la viva de ES de prod murio a las 06:57 UTC), F4 gatillo modelo·es10 apagado por defecto y
con calentamiento, F5 opcion zero gamma interpolado (como la referencia), F6 opcion historia del dia (nube de dominantes/zero), F7 opcion
barras relativas al 30 % del ancho. Ver `atas/PythiaGexDos/CHANGELOG.md`.

**How to apply:** ante cualquier pedido de cambio en Gamma Hoy, editar SOLO `atas/PythiaGexDos`; compilar ahi; instalar con el instalador;
avisar con captura antes/despues (regla [[dominantes-de-noche-por-volumen]]). Ver [[regla-roja-roll-libro-vivo]], [[flujo-claro-cvd-superador]].
