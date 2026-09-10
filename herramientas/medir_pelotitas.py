# -*- coding: utf-8 -*-
"""MEDIR LAS PELOTITAS DE GAMMAlito CUADRO A CUADRO (video "Te explico en vivo el Max Change",
NinjaTrader, MES 30 s, 1920x1080, 60 fps).

Que mide, sin creerle a nadie:
  1. DEFINICION: ¿la pelotita chica esta donde estaba la punta de la barra hace 1 minuto, y la
     mediana donde estaba hace 5? Se prueba con todos los retrasos de 0 a 300 s y se ve cual
     minimiza el error. Si el minimo cae en 60 s y 300 s, la definicion del creador es exacta.
  2. VALOR PREDICTIVO: cuando la pelotita chica queda ADENTRO (la barra crecio en el ultimo
     minuto), ¿la barra sigue creciendo en el minuto siguiente? Se compara contra los casos
     "afuera" y "en la punta".
  3. MOVIMIENTO VERTICAL de las barras: cuanto se corre la fila de cada barra a lo largo del video.

Salida: datos/simulador/pelotitas/<video>.json y un resumen por pantalla.
Uso: python herramientas/medir_pelotitas.py "<video.mp4>" [--fps 2]
"""
import json
import os
import statistics as st
import sys

import cv2
import numpy as np

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
X0, X1 = 67, 1000          # perfil izquierdo: las barras nacen en x=67
WEBCAM = (190, 655, 690, 950)   # x0, y0, x1, y1 de la camara del presentador: se ignora
Y0, Y1 = 85, 960


def barras_y_pelotitas(fr):
    """Filas de barras (y, punta x) y pelotitas grises (x, y, r) del perfil izquierdo."""
    hsv = cv2.cvtColor(fr, cv2.COLOR_BGR2HSV)
    h, s, v = hsv[:, :, 0], hsv[:, :, 1], hsv[:, :, 2]
    color = (s > 110) & (v > 110) & (((h < 12) | (h > 168)) | ((h > 45) & (h < 85)))   # rojo o verde
    color[:, :X0] = False; color[:, X1:] = False; color[:Y0, :] = False; color[Y1:, :] = False
    gris = (s < 45) & (v > 95) & (v < 225)
    gris[:, :X0] = False; gris[:, X1:] = False; gris[:Y0, :] = False; gris[Y1:, :] = False
    wx0, wy0, wx1, wy1 = WEBCAM
    color[wy0:wy1, wx0:wx1] = False; gris[wy0:wy1, wx0:wx1] = False
    # filas: y con >= 12 px de color que arrancan cerca de x=67
    cuenta = color[:, X0:X0 + 30].sum(axis=1)
    filas = []
    y = Y0
    while y < Y1:
        if cuenta[y] >= 8:
            y2 = y
            while y2 + 1 < Y1 and cuenta[y2 + 1] >= 8:
                y2 += 1
            yc = (y + y2) // 2
            # la punta: primer hueco de > 4 px en la corrida de color desde X0, en la fila central
            fila = color[yc, :]
            x = X0; hueco = 0; punta = X0
            while x < X1:
                if fila[x]:
                    punta = x; hueco = 0
                else:
                    hueco += 1
                    if hueco > 4:
                        break
                x += 1
            filas.append({"y": int(yc), "punta": int(punta), "alto": int(y2 - y + 1)})
            y = y2 + 1
        else:
            y += 1
    # pelotitas: componentes grises redondas
    n, lab, stats, cent = cv2.connectedComponentsWithStats(gris.astype(np.uint8), connectivity=8)
    pel = []
    for i in range(1, n):
        x, yy, w, hh, area = stats[i]
        if area < 12 or area > 500 or w > 26 or hh > 26 or w < 4 or hh < 4:
            continue
        if not (0.55 <= w / float(hh) <= 1.8):
            continue
        pel.append({"x": float(cent[i][0]), "y": float(cent[i][1]), "r": round((w + hh) / 4.0, 1)})
    # asignar a la fila mas cercana (|dy| <= 7)
    for f in filas:
        f["pel"] = sorted([(round(p["x"], 1), p["r"]) for p in pel if abs(p["y"] - f["y"]) <= 7], key=lambda t: t[0])
    return filas


def main():
    video = sys.argv[1]
    fps_m = float(sys.argv[sys.argv.index("--fps") + 1]) if "--fps" in sys.argv else 2.0
    cap = cv2.VideoCapture(video)
    fps = cap.get(cv2.CAP_PROP_FPS); n = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
    paso = max(1, int(round(fps / fps_m)))
    salida = os.path.join(RAIZ, "datos", "simulador", "pelotitas"); os.makedirs(salida, exist_ok=True)
    cuadros = []
    for i in range(0, n, paso):
        cap.set(cv2.CAP_PROP_POS_FRAMES, i); ok, fr = cap.read()
        if not ok:
            break
        cuadros.append({"t": round(i / fps, 2), "filas": barras_y_pelotitas(fr)})
    base = os.path.splitext(os.path.basename(video))[0][:60]
    json.dump(cuadros, open(os.path.join(salida, base + ".json"), "w"))
    print("cuadros medidos:", len(cuadros), "| filas por cuadro (mediana):", st.median(len(c["filas"]) for c in cuadros))

    # ---- seguimiento de filas por y (+-5 px) a lo largo del video
    pistas = {}     # y redondeado -> lista de (t, punta, pelotitas, y)
    for c in cuadros:
        for f in c["filas"]:
            k = None
            for ky in pistas:
                if abs(ky - f["y"]) <= 5:
                    k = ky; break
            if k is None:
                k = f["y"]; pistas[k] = []
            pistas[k].append((c["t"], f["punta"], f["pel"], f["y"]))
    pistas = {k: v for k, v in pistas.items() if len(v) >= 0.5 * len(cuadros)}
    print("filas seguidas todo el video:", len(pistas))

    # ---- 1) definicion: ¿la pelotita chica = punta hace 60 s? ¿la mediana = punta hace 300 s?
    def punta_en(v, t):
        mejor = None
        for (tt, p, _, _) in v:
            if abs(tt - t) <= 0.6 and (mejor is None or abs(tt - t) < abs(mejor[0] - t)):
                mejor = (tt, p)
        return None if mejor is None else mejor[1]
    print()
    print("1) DEFINICION: error mediano (px) entre la pelotita y la punta de hace N segundos")
    print("   %6s | %10s %10s %10s" % ("N s", "chica", "mediana", "grande"))
    for N in (0, 15, 30, 45, 60, 90, 120, 180, 240, 300):
        err = {"chica": [], "mediana": [], "grande": []}
        for k, v in pistas.items():
            for (t, p, pel, _) in v:
                if len(pel) < 2 or t < N + 1:
                    continue
                p_ant = punta_en(v, t - N)
                if p_ant is None:
                    continue
                orden = sorted(pel, key=lambda q: q[1])         # por radio: chica, mediana, grande
                err["chica"].append(abs(orden[0][0] - p_ant))
                if len(orden) >= 2: err["mediana"].append(abs(orden[-1][0] - p_ant) if len(orden) == 2 else abs(orden[1][0] - p_ant))
                if len(orden) >= 3: err["grande"].append(abs(orden[-1][0] - p_ant))
        print("   %6d | %10s %10s %10s" % (N, "%.1f (n=%d)" % (st.median(err["chica"]), len(err["chica"])) if err["chica"] else "--",
                                            "%.1f (n=%d)" % (st.median(err["mediana"]), len(err["mediana"])) if err["mediana"] else "--",
                                            "%.1f (n=%d)" % (st.median(err["grande"]), len(err["grande"])) if err["grande"] else "--"))

    # ---- 2) valor predictivo: chica adentro / afuera / en la punta -> cambio de la punta en los 60 s siguientes
    print()
    print("2) PREDICCION: cambio de la punta (px) en los 60 s siguientes, segun donde estaba la pelotita chica")
    grupos = {"adentro (crecio)": [], "afuera (decrecio)": [], "en la punta (quieto)": []}
    for k, v in pistas.items():
        for (t, p, pel, _) in v:
            if not pel:
                continue
            chica = min(pel, key=lambda q: q[1])[0]
            p_fut = punta_en(v, t + 60)
            if p_fut is None:
                continue
            d = chica - p
            g = "adentro (crecio)" if d < -3 else "afuera (decrecio)" if d > 3 else "en la punta (quieto)"
            grupos[g].append(p_fut - p)
    for g, xs in grupos.items():
        if xs:
            up = sum(1 for x in xs if x > 2); dn = sum(1 for x in xs if x < -2)
            print("   %-22s n=%4d  cambio mediano %+5.1f px | sigue creciendo %3.0f %% | se achica %3.0f %%" % (g, len(xs), st.median(xs), 100.0 * up / len(xs), 100.0 * dn / len(xs)))

    # ---- 3) movimiento vertical de las filas
    print()
    print("3) MOVIMIENTO VERTICAL de las filas (px; el eje del video: ~11 px por punto de MES)")
    mov = [max(q[3] for q in v) - min(q[3] for q in v) for v in pistas.values()]
    print("   rango de y por fila: mediana %.1f px, maximo %.1f px, en %d filas" % (st.median(mov), max(mov), len(mov)))


if __name__ == "__main__":
    main()
