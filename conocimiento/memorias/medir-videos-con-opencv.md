---
name: medir-videos-con-opencv
description: Como medir en los videos de la referencia lo que dibuja (guiones, barras) en vez de mirarlos; la herramienta, sus trampas y los resultados del 2026-09-07
metadata:
  type: reference
---

`herramientas/analizar_guiones.py <video.mp4> --fps 2` (OpenCV): guiones =
componentes amarillas anchas y bajas (HSV 18-40); barras = verdes/rojas en el
30 % izquierdo; agrupa por columna (vela), cuenta guiones por columna y
apertura vertical; sigue las barras cuadro a cuadro separando el corrimiento
comun (autoescala) del propio. Guarda cuadros anotados en
datos/simulador/guiones/<video>/ y medidas.json.

Trampas medidas: (1) en NinjaTrader las velas bajistas son NARANJAS (hue 20)
y el detector las cuenta como guiones: mirar los cuadros anotados antes de
creer un numero; (2) los rotulos amarillos de los shorts (subtitulos, bandas)
inflan el maximo por columna; (3) el x de una columna no es una vela fija si
el grafico hace scroll: "alturas distintas por columna a lo largo del video"
esta contaminado por el scroll; (4) muestrear colores sobre el cuadro ANOTADO
da magenta (hue 153): muestrear siempre sobre el cuadro crudo.

Resultados (ver [[anatomia-referencia]]): guion por actualizacion (hasta 4-6
por vela), dominante = banda ~5 pts NQ (centroide), amarillo 29 / naranja 19,
barras fijas en vertical (|dy| propio p90 <= 1,9 px), pelotitas 15/5/1.
Videos en %USERPROFILE%\Downloads (nombres largos con espacios y tildes).
