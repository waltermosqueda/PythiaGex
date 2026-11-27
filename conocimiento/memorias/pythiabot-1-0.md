---
name: pythiabot-1-0
description: "13-11: PythiaBot 1.0 = primera ESTRATEGIA de ATAS (ChartStrategy) en atas/PythiaBot; guardian (tope, stop por ciclo, no promediar, reloj, perdida diaria de la cuenta persistida, cierre 16:40 NY) + autonomo solo en cuentas con prefijo (DEMO/CRYPTO); DLL en %APPDATA%\\ATAS\\Strategies; Lucid permite bots (prohibe HFT/microscalping)"
metadata:
  node_type: memory
  type: project
  originSessionId: f3419dc9-e1cb-4b1b-99aa-8707c1a3c2f4
  modified: 2026-11-13T23:03:16.670Z
---

`PythiaGex/atas/PythiaBot/` (BotNucleo.cs puro + PythiaBot.cs + _prueba con 16 pruebas OK + _esqueleto del agente).
Log: `%APPDATA%\ATAS\PythiaBot\bot-AAAAMMDD.log`; base del dia por cuenta en `base-<hash>-<dia>.txt`.
- API 8.0.15.306 (leida): ChartStrategy hereda de Indicator; OpenOrderAsync/ModifyOrderAsync(Clone)/CancelOrderAsync;
  no hay ClosePosition en ChartStrategy (si en TradingManager.ClosePositionAsync); CurrentPosition = SOLO lo propio
  (el guardian usa TradingManager.Position, que incluye lo manual); stop/take = ordenes separadas con OCOGroup +
  ReduceOnly; la estrategia se detiene sola si se cae la conexion y al detener NO cancela en el servidor.
- Carga: DLL en %APPDATA%\ATAS\Strategies; ATAS escribe "Created library" pero la lista de "Chart strategies" no
  la muestra hasta REINICIAR (no hay boton de recarga accesible por UIA).
- Cuentas de prueba: ATAS Sim (DEMO..., datos CME en vivo: solo con mercado abierto) y Crypto Sim (CRYPTO59F78, 100k,
  BTCUSDT 24/7: sirve para probar la cañeria de noche/fin de semana).
- Lucid (support.lucidtrading.com 11404728, 26-08-2026): "Automated trading systems and trade copiers are permitted";
  prohibe HFT y microscalping (>50 % de ganancia en operaciones <= 5 s); cierre 16:45 NY; MLL de fin de dia.
- NINGUNA señal de entrada validada: MODELO 1.7b fallo hacia adelante (41 % vs 53 % de empate, = azar; laboratorio/bot/
  adelante); X-Ray no da direccion (ver [[xray-historia-medida]]). El autonomo trae solo una "prueba de cañeria".

**How to apply:** el guardian es lo unico con evidencia (ronda 8); el autonomo NO va a una cuenta Lucid hasta que una
señal pase un pre-registro hacia adelante. Ver [[estrategia-ronda-8-edge-personal]], [[salidas-atas-tp-sl-trailing]].
