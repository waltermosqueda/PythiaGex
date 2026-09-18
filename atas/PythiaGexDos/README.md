# PythiaGex 2.0 (clon de Gamma Hoy)

Ensamblado APARTE (`PythiaGexDos.dll`) creado el 18-09-2026 con `herramientas/clonar_2_0.py` a partir de `PythiaGexNiveles`
(Gamma Hoy 1.11d). Regla del operador: la produccion (`PythiaGexNiveles.dll`, "PythiaGex - Gamma Hoy") NO se toca mas; todo
arreglo y toda feature nueva va aca, y si algo se rompe se vuelve a agregar la original.

- Nombres en ATAS: "PythiaGex 2.0 - Gamma Hoy" (categoria "PythiaGex 2.0").
- Datos propios: `%APPDATA%\ATAS\PythiaGex2\` (estela, cadenas, viva, flujo...) y logs `%APPDATA%\ATAS\pythiagex2-*.log`.
- Los ajustes NO se comparten con la original (ATAS los guarda por nombre de indicador).
- Flujo Claro no esta en este ensamblado: sigue en prod. Sus "verdes" leen la estela de PROD (`PythiaGex\estela`).
- OJO: dos Gamma Hoy en el mismo grafico (original + 2.0) duplican las suscripciones a Rithmic. Para probar, agregar 2.0 en UN grafico.
  Desde 2.0.2, si prod armo su cadena viva en el mismo ATAS, la 2.0 NO desuscribe contratos (rearme ni al quitarse): el conector es
  uno por proceso y no esta medido que cuente referencias por contrato (log "prod presente: no suelto N contratos").
- `cboe-local` se comparte con prod (`%APPDATA%\ATAS\PythiaGex\cboe-local`): la escribe `herramientas/cboe_local.py`, el DLL solo la lee.
- La razon del ETF se guarda por raiz del futuro + ticker (`PythiaGex2\razon-NQ-QQQ.txt`, `razon-ES-SPY.txt`).
- El instalador `herramientas/reiniciar_e_instalar.ps1` copia los dos DLL si existen.
- Cambios por version, con evidencia y donde tocan: `CHANGELOG.md` (2.0.1 = F1-F7, 2.0.2 = revision, 2.0.3 = F8 estilo referencia + libro automatico, 18-09).
