---
name: directriz-replica-tradingview
description: "23-09: los 7 reels de scalping que trajo el operador usan un script ABIERTO de TradingView (79 lineas Pine); replicado exacto en ATAS como 'Directriz' (pestaña MNQZ6 5s Chart); medido en 36 sesiones de MNQ a 5 s: 48,7 % vs 51,4 % necesario, -0,87 pts/op, igual que una moneda; 63 % de sus ops salen de lineas 'fantasma'."
metadata:
  node_type: memory
  type: project
  originSessionId: 66d6ed1e-b70d-43cb-92e4-4ddc3f9d1e50
  modified: 2026-09-24T01:40:06.634Z
---

Pedido 23-09 (noche): analizar 7 reels (Downloads\Comenta CLASE/SALA/SCALPING*.mp4) y armar "esa estrategia/indicador" en
ATAS, enfocado en MNQ/NQ. Material local y copia del codigo FUERA de los repos: `Escritorio\ATAS nada\<carpeta local de la directriz>\` (cuadros,
subtitulos, fuente\ con PROCEDENCIA.md y sha256). En repo y memorias se la llama "la directriz" (misma convencion que
[[referencia-sin-nombre]], aplicada por analogia: el operador puede pedir otra cosa).

- El "indicador gratuito" es un script de TradingView de codigo abierto (URL y autor en <carpeta local de la directriz>\fuente\PROCEDENCIA.md):
  EMA 100 coloreada por EMA100>EMA25, pivotes (Pivot izq, 10 der) con etiquetas L/H, linea entre dos pivotes del
  retroceso, entrada cuando una vela abre de un lado y cierra del otro, sesion 0830-0930 hora bolsa, SL/TP FIJOS 64 ticks
  (1:1, +spread al TP). Lo que dicen los reels (1:2, stop tras minimos/media) son cajas manuales; la descripcion
  publicada (ATR, cruce de EMAs) no coincide con el codigo. `line.delete` no apaga `line.get_price`: lineas fantasma.
- Los agentes de video: las operaciones de los reels son casi todas ORO (OANDA XAUUSD) en 5 s, varias en modo
  Repeticion (no en vivo); v6 BTC 15 m, v7 BTC 1 m (leyenda "...10 64 0 0730-0930"). Grafico publicado: NQ 5 s 15/64/4.
- Laboratorio `PythiaGex\laboratorio\directriz` (INFORME.md, D_00_PREREGISTRO.md): 36 sesiones MNQ en 5 s armadas desde
  la cinta de la sonda (auditadas contra ATAS m1 99,9-100 %). P1 publicado: 156 ops, 48,7 % (necesita 51,4), -0,87 pts/op
  con costo 0,948, percentil 52 vs lado al azar. P2 defaults: -0,26/op. Toda la rueda 5 s: -1.019 pts (t -2,33). Noche:
  bruto +0,20/op y percentil 99,5 contra el lado al azar (pista post-hoc, no paga costo). Salidas 1:2 de los videos: nada.
- Indicador `PythiaGex\atas\Directriz` (Directriz.dll, grupo PythiaGex 2.0, "Directriz (replica TradingView) - EN
  PRUEBA"): paridad C#/Python 0 diferencias (7 configs); integracion en ATAS (volcado auditoria-*.csv +
  integracion_atas.py) sesion 21-09: velas = cinta 99,99 %, 3/3 senales, 532/532 etiquetas, 3/3 ops. Instalado en
  "MNQ liviano", pestaña "MNQZ6 5s Chart" que armo el operador. Senales en vivo a %APPDATA%\ATAS\PythiaGex2\directriz.
  El 5 s carga solo la sesion en curso: calendario abajo -> fecha para mirar dias viejos; "Today" para volver.

**Why:** el operador queria la estrategia de los videos en su ATAS; lo honesto es replica exacta + la medicion.
**How to apply:** no presentarla como estrategia; medir hacia adelante con el CSV (15 sesiones) antes de cualquier
conclusion; si pide "la de los videos" en otro activo (oro), es fuera de su alcance declarado (MNQ). Ver
[[busqueda-estrategia-sin-parar]], [[estrategia-ronda-9-patron]].
