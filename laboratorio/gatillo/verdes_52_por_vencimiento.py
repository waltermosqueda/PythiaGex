# -*- coding: utf-8 -*-
"""verdes_52 — DE QUE VENCIMIENTO SALEN LAS DOMINANTES DE NOCHE. De la ultima foto del libro vivo de NQ (o la de --dia/--hora):
por vencimiento, los strikes con mas gamma x interes abierto (en precio del grafico), y que dominantes por lado saldrian
(a) con todo el libro del horizonte Hoy, (b) solo con cada vencimiento. Misma cuenta que capas_nq.recalcular, por OI.
Uso: python verdes_52_por_vencimiento.py [--dia 2026-09-17 --hora 23:30]"""
import os, sys, json, math
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import capas_nq as C

VIVA = os.path.join(os.environ["APPDATA"], "ATAS", "PythiaGex", "viva")


def foto(dia, hora):
    ult = None
    for l in open(os.path.join(VIVA, "viva-NQ-%s.jsonl" % dia), encoding="utf-8", errors="replace"):
        l = l.strip()
        if len(l) > 40 and l.endswith("}"):
            try: r = json.loads(l)
            except Exception: continue
            if hora is None or r["ts"][11:16] <= hora: ult = r
    return ult


def perfil(r, solo=None, por_vol=False):
    fut = float(r["futuro"]); por = {}
    for x in r["filas"]:
        K, d, call, oi, iv, vol = float(x[0]), round(float(x[1]), 2), float(x[2]) >= 0.5, float(x[3]), float(x[4]), float(x[7])
        if iv <= 0 or d < 0 or d > 1.0 or (solo is not None and d != solo): continue
        w = vol if por_vol else oi
        if w <= 0: continue
        T = max(d, C.PISO_DIAS) / 365.0
        g = C.gamma76(fut, K, T, iv) * w * C.MULT * fut * fut * 0.01
        por[K] = por.get(K, 0.0) + (g if call else -g)
    return fut, sorted(por.items())


def dominantes(fut, p):
    radio = min(fut * C.RADIO_DOM_PCT / 100.0, C.RADIO_MAX_PTS) if C.RADIO_MAX_PTS > 0 else fut * C.RADIO_DOM_PCT / 100.0
    cerca = [(k, g) for k, g in p if abs(k - fut) <= radio and g != 0]
    def elegir(lado):
        if not lado: return None
        pmax = max(abs(g) for _, g in lado); piso = pmax * (1.0 - C.EMPATE_PCT / 100.0)
        return sorted([(k, g) for k, g in lado if abs(g) >= piso], key=lambda q: (abs(q[0] - fut), -abs(q[1])))[0]
    return elegir([q for q in cerca if q[0] > fut]), elegir([q for q in cerca if q[0] <= fut]), radio


if __name__ == "__main__":
    dia = sys.argv[sys.argv.index("--dia") + 1] if "--dia" in sys.argv else None
    hora = sys.argv[sys.argv.index("--hora") + 1] if "--hora" in sys.argv else None
    if dia is None:
        from datetime import datetime, timezone
        dia = datetime.now(timezone.utc).strftime("%Y-%m-%d")
    por_vol = "--vol" in sys.argv
    r = foto(dia, hora)
    if r is None: sys.exit("sin foto")
    vencs = sorted(set(round(float(x[1]), 2) for x in r["filas"] if 0 <= float(x[1]) <= 1.0))
    print("foto %s | futuro %.2f | peso = %s | vencimientos del horizonte Hoy: %s" % (r["ts"], float(r["futuro"]), "VOLUMEN" if por_vol else "INTERES ABIERTO", vencs))
    for solo in [None] + vencs:
        fut, p = perfil(r, solo, por_vol); a, b, radio = dominantes(fut, p)
        print("\n== %s (radio %.0f pts)" % ("TODO EL LIBRO" if solo is None else "solo el que vence en %.2f d" % solo, radio))
        print("   dominante arriba: %s | abajo: %s | tunel: %s" % (
            "%.2f (%+.0f pts, %.0f M)" % (a[0], a[0] - fut, abs(a[1]) / 1e6) if a else "--", "%.2f (%+.0f pts, %.0f M)" % (b[0], b[0] - fut, abs(b[1]) / 1e6) if b else "--",
            "%.0f pts" % (a[0] - b[0]) if a and b else "--"))
        top = sorted(p, key=lambda q: -abs(q[1]))[:8]
        print("   los 8 strikes mas fuertes: " + " | ".join("%.0f (%+.0f) %.0fM" % (k, k - fut, abs(g) / 1e6) for k, g in top))
