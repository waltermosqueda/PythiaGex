# PythiaGex 2.0 (clon de Gamma Hoy)

Ensamblado APARTE (`PythiaGexDos.dll`) creado el 18-09-2026 con `herramientas/clonar_2_0.py` a partir de `PythiaGexNiveles`
(Gamma Hoy 1.11d). Regla del operador: la produccion (`PythiaGexNiveles.dll`, "PythiaGex - Gamma Hoy") NO se toca mas; todo
arreglo y toda feature nueva va aca, y si algo se rompe se vuelve a agregar la original.

- Nombres en ATAS: "PythiaGex 2.0 - Gamma Hoy" (categoria "PythiaGex 2.0").
- Datos propios: `%APPDATA%\ATAS\PythiaGex2\` (estela, cadenas, viva, flujo...) y logs `%APPDATA%\ATAS\pythiagex2-*.log`.
- Los ajustes NO se comparten con la original (ATAS los guarda por nombre de indicador).
- Flujo Claro no esta en este ensamblado: sigue en prod. Sus "verdes" leen la estela de PROD (`PythiaGex\estela`).
- OJO: dos Gamma Hoy en el mismo grafico (original + 2.0) duplican las suscripciones a Rithmic. Para probar, agregar 2.0 en UN grafico.
- El instalador `herramientas/reiniciar_e_instalar.ps1` copia los dos DLL si existen.
