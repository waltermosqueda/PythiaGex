# -*- coding: utf-8 -*-
"""MEDIR EN LOS VIDEOS DE LA REFERENCIA COMO SE DIBUJAN LAS DOMINANTES Y LAS BARRAS.

Pregunta del operador (2026-09-07): en la referencia aparecen VARIOS guiones de
dominante por instante, arriba y abajo, como una nube que delimita una zona; y
las barras laterales se mueven tambien en vertical. Nosotros dibujamos un
guion por vela en fila. Esto no discute: mide.

Por cada cuadro muestreado del video:
  - guiones amarillos: componentes conexas de color amarillo (HSV), anchas y
    bajas; se agrupan por columna (vela) y se cuenta cuantos hay por columna y
    cuanto se abren en vertical;
  - barras laterales: componentes verdes/rojas anchas a la izquierda del
    grafico; se sigue la altura (y) y el largo (w) de cada fila cuadro a
    cuadro, y se separa el corrimiento comun (autoescala del grafico) del
    movimiento propio de cada fila.
Guarda cuadros anotados para mirar con los ojos lo que midio la maquina.

Uso: python herramientas/analizar_guiones.py <video.mp4> [--fps 2] [--desde s] [--hasta s] [--salida dir]
"""
import argparse
import json
import os
import sys

import cv2
import numpy as np


def guiones(hsv, bgr, w_min=4, w_max=80, h_max=9):
    """Componentes amarillas anchas y bajas: (x, y, w, h, cx, cy)."""
    m = cv2.inRange(hsv, (18, 110, 140), (40, 255, 255))
    n, lab, st, cen = cv2.connectedComponentsWithStats(m, 8)
    out = []
    for i in range(1, n):
        x, y, w, h, a = st[i]
        if w_min <= w <= w_max and h <= h_max and a >= 3:
            out.append((int(x), int(y), int(w), int(h), float(cen[i][0]), float(cen[i][1])))
    return out, m


def barras(hsv, ancho, izq_frac=0.30):
    """Barras verdes/rojas del perfil izquierdo: (color, x, y, w, h, cy)."""
    verde = cv2.inRange(hsv, (40, 80, 90), (90, 255, 255))
    rojo = cv2.inRange(hsv, (0, 90, 90), (10, 255, 255)) | cv2.inRange(hsv, (165, 90, 90), (180, 255, 255))
    out = []
    for nombre, m in (("verde", verde), ("rojo", rojo)):
        n, lab, st, cen = cv2.connectedComponentsWithStats(m, 8)
        for i in range(1, n):
            x, y, w, h, a = st[i]
            if x < ancho * izq_frac and w >= 12 and 3 <= h <= 24 and w >= 2 * h:
                out.append((nombre, int(x), int(y), int(w), int(h), float(cen[i][1])))
    return out


def columnas(gs, tol):
    """Agrupa guiones por columna x (una vela). Devuelve lista de listas."""
    gs = sorted(gs, key=lambda g: g[4])
    cols = []
    for g in gs:
        if cols and abs(g[4] - cols[-1][-1][4]) <= tol:
            cols[-1].append(g)
        else:
            cols.append([g])
    return cols


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("video")
    ap.add_argument("--fps", type=float, default=2.0)
    ap.add_argument("--desde", type=float, default=0.0)
    ap.add_argument("--hasta", type=float, default=1e9)
    ap.add_argument("--salida", default=None)
    ap.add_argument("--anotar", type=int, default=6, help="cuantos cuadros anotados guardar")
    a = ap.parse_args()
    cap = cv2.VideoCapture(a.video)
    if not cap.isOpened():
        sys.exit("no pude abrir " + a.video)
    fps_v = cap.get(cv2.CAP_PROP_FPS) or 30.0
    nfr = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
    W, H = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH)), int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
    nombre = os.path.splitext(os.path.basename(a.video))[0][:40]
    salida = a.salida or os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "datos", "simulador", "guiones", nombre)
    os.makedirs(salida, exist_ok=True)
    paso = max(1, int(round(fps_v / a.fps)))
    print("%s: %dx%d, %.1f fps, %d cuadros; muestreo cada %d cuadros" % (nombre, W, H, fps_v, nfr, paso), flush=True)

    filas = []          # por cuadro: resumen
    hist_col = {}       # columna x (redondeada) -> conjunto de y de guiones vistos a lo largo del tiempo
    barras_t = []       # por cuadro: lista de (color, cy, w)
    anotados = 0
    i = 0
    while True:
        ok = cap.grab()
        if not ok:
            break
        t = i / fps_v
        if i % paso != 0 or t < a.desde:
            i += 1
            continue
        if t > a.hasta:
            break
        ok, bgr = cap.retrieve()
        if not ok:
            break
        hsv = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV)
        gs, mask = guiones(hsv, bgr)
        if gs:
            wmed = float(np.median([g[2] for g in gs]))
        else:
            wmed = 8.0
        cols = columnas(gs, max(3.0, wmed * 0.6))
        # las columnas mas a la derecha son las velas recientes
        cols.sort(key=lambda c: c[0][4])
        ult = cols[-6:] if cols else []
        por_col = [len(c) for c in cols]
        spread = [(max(g[5] for g in c) - min(g[5] for g in c)) if len(c) > 1 else 0.0 for c in cols]
        for c in cols:
            k = int(round(c[0][4] / max(3.0, wmed * 0.6)))
            hist_col.setdefault(k, set()).update(int(round(g[5])) for g in c)
        bs = barras(hsv, W)
        barras_t.append((t, [(b[0], b[5], b[3]) for b in bs]))
        filas.append({
            "t": round(t, 2), "guiones": len(gs), "columnas": len(cols), "ancho_med": round(wmed, 1),
            "por_col_max": max(por_col) if por_col else 0, "por_col_med": float(np.median(por_col)) if por_col else 0,
            "cols_con_2o_mas": sum(1 for p in por_col if p >= 2),
            "spread_med": round(float(np.median(spread)), 1) if spread else 0,
            "spread_max": round(max(spread), 1) if spread else 0,
            "ultimas6_por_col": [len(c) for c in ult], "ultimas6_spread": [round((max(g[5] for g in c) - min(g[5] for g in c)) if len(c) > 1 else 0.0, 1) for c in ult],
            "barras": len(bs),
        })
        if anotados < a.anotar and gs and (len(filas) % max(1, int((a.hasta if a.hasta < 1e8 else min(nfr / fps_v, 400)) - a.desde) * a.fps // a.anotar) == 1):
            an = bgr.copy()
            for (x, y, w, h, cx, cy) in gs:
                cv2.rectangle(an, (x - 1, y - 1), (x + w + 1, y + h + 1), (255, 0, 255), 1)
            for b in bs:
                cv2.rectangle(an, (b[1], b[2]), (b[1] + b[3], b[2] + b[4]), (0, 255, 255), 1)
            cv2.imwrite(os.path.join(salida, "anotado-%06.1fs.jpg" % t), an, [cv2.IMWRITE_JPEG_QUALITY, 80])
            anotados += 1
        i += 1
    cap.release()

    # ------------------------------------------------------------ resumen
    print("cuadros medidos: %d" % len(filas))
    if not filas:
        return
    g = [f for f in filas if f["guiones"] > 0]
    print("cuadros con guiones amarillos: %d (%.0f%%)" % (len(g), 100.0 * len(g) / len(filas)))
    if g:
        print("guiones por cuadro: mediana %.0f, max %d" % (np.median([f["guiones"] for f in g]), max(f["guiones"] for f in g)))
        print("ancho mediano del guion: %.1f px" % np.median([f["ancho_med"] for f in g]))
        print("columnas (velas con guion) por cuadro: mediana %.0f" % np.median([f["columnas"] for f in g]))
        print("GUIONES POR COLUMNA: mediana %.1f, max %d; columnas con 2 o mas: mediana %.0f por cuadro" % (
            np.median([f["por_col_med"] for f in g]), max(f["por_col_max"] for f in g), np.median([f["cols_con_2o_mas"] for f in g])))
        print("apertura vertical de una columna (px): mediana %.1f, max %.1f" % (np.median([f["spread_med"] for f in g]), max(f["spread_max"] for f in g)))
        u = [x for f in g for x in f["ultimas6_por_col"]]
        us = [x for f in g for x in f["ultimas6_spread"]]
        if u:
            print("en las 6 velas mas recientes: guiones por vela mediana %.1f, max %d; apertura mediana %.1f px, max %.1f px" % (np.median(u), max(u), np.median(us), max(us)))
        # cuantas alturas distintas acumula una misma columna a lo largo del video
        alt = sorted(len(v) for v in hist_col.values())
        if alt:
            print("alturas distintas que acumula una misma columna a lo largo del video: mediana %.0f, p90 %.0f, max %d (columnas %d)" % (
                np.median(alt), np.percentile(alt, 90), max(alt), len(alt)))
    # barras: corrimiento comun vs propio
    filas_b = [b for b in barras_t if b[1]]
    print("cuadros con barras laterales detectadas: %d" % len(filas_b))
    if len(filas_b) >= 3:
        # emparejar filas por altura entre cuadros consecutivos (misma fila si |dy| <= 6 px tras quitar el corrimiento comun)
        dy_comun, dy_propio, dw = [], [], []
        for (t0, b0), (t1, b1) in zip(filas_b, filas_b[1:]):
            y0 = np.array([b[1] for b in b0]); y1 = np.array([b[1] for b in b1])
            if len(y0) < 3 or len(y1) < 3:
                continue
            # corrimiento comun = mediana de los desplazamientos minimos
            d = [float(min(y1 - y, key=abs)) for y in y0]
            com = float(np.median(d))
            dy_comun.append(com)
            for (c, y, w) in b0:
                j = int(np.argmin(np.abs(y1 - (y + com))))
                if abs(y1[j] - (y + com)) <= 6:
                    dy_propio.append(float(y1[j] - (y + com)))
                    dw.append(float(b1[j][2] - w))
        if dy_comun:
            print("corrimiento COMUN de todas las barras entre cuadros: mediana %.1f px, p90 |%.1f| px (autoescala/scroll del grafico)" % (np.median(dy_comun), np.percentile(np.abs(dy_comun), 90)))
        if dy_propio:
            print("movimiento PROPIO de una barra (quitado el comun): |dy| mediana %.2f px, p90 %.2f px, max %.1f px  (n=%d)" % (
                np.median(np.abs(dy_propio)), np.percentile(np.abs(dy_propio), 90), max(np.abs(dy_propio)), len(dy_propio)))
            print("cambio de LARGO de una barra entre cuadros: |dw| mediana %.1f px, p90 %.1f px, max %.0f px" % (
                np.median(np.abs(dw)), np.percentile(np.abs(dw), 90), max(np.abs(dw))))
    with open(os.path.join(salida, "medidas.json"), "w", encoding="utf-8") as f:
        json.dump({"video": a.video, "cuadros": filas, "alturas_por_columna": {str(k): sorted(v) for k, v in hist_col.items()}}, f)
    print("anotados en", salida)


if __name__ == "__main__":
    main()
