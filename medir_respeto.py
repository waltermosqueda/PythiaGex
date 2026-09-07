# -*- coding: utf-8 -*-
"""LAS DOMINANTES: FRENARON AL PRECIO, O ES CASUALIDAD?

EL PROBLEMA DE MEDIR ESTO SIN CONTROL
El precio va y vuelve todo el tiempo. Si uno dibuja CUALQUIER linea y despues
cuenta cuantas veces el precio la toco y se dio vuelta, va a encontrar un
monton -- y no prueba nada, porque lo mismo pasaria con una linea puesta al
azar.

POR ESO ESTE SCRIPT MIDE DOS COSAS A LA VEZ:
  1. el nivel de verdad
  2. el MISMO nivel corrido unos puntos (el placebo), que esta en la misma zona
     de precios y en el mismo momento, pero no significa nada

Si el nivel real no le gana al placebo, el nivel no informa nada. Punto.

QUE CUENTA COMO "RESPETO"
Se busca un TOQUE: el precio venia lejos y se metio adentro de la tolerancia.
Desde ahi se mira una ventana hacia adelante y se compara cuanto se ALEJO del
nivel contra cuanto lo ATRAVESO. Si se alejo mas de lo que lo cruzo, el nivel
lo freno.
"""
import re, io, os, sys, random, statistics as st
from datetime import datetime

LOG = os.path.join(os.environ["APPDATA"], "ATAS", "pythiagex-gammavivo.log")
TOL = 1.5          # que tan cerca hay que estar para llamarlo toque, en puntos
LEJOS = 5.0        # antes del toque tenia que estar al menos a esto
VENTANA = 15       # minutos que se miran hacia adelante
PLACEBOS = (-37.0, -23.0, -13.0, 13.0, 23.0, 37.0)


def leer():
    fs = []
    for l in io.open(LOG, encoding="utf-8", errors="replace"):
        if "AUDIT" not in l:
            continue
        t = re.match(r"(\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d)", l)
        if not t:
            continue
        def g(c):
            m = re.search(r"\b" + c + r"=([\d.\-]+)", l)
            return float(m.group(1)) if m else None
        sp = g("spot_idx")
        if sp is None or sp > 12000:
            continue
        base = g("base") or 0.0
        m = re.search(r"picos=([\d./]+)", l)
        picos = []
        if m:
            for x in m.group(1).split("/"):
                try:
                    picos.append(float(x))
                except ValueError:
                    pass
        mp, mn, z = g("majorpos"), g("majorneg"), g("zero")
        fs.append(dict(
            t=datetime.strptime(t.group(1), "%Y-%m-%dT%H:%M:%S"),
            px=sp + base,
            picos=picos,
            mp=(mp + base) if mp else None,
            mn=(mn + base) if mn else None,
            z=(z + base) if z and z > 0 else None))
    fs.sort(key=lambda f: f["t"])
    return fs


def probar(fs, sacar_niveles, corrimiento=0.0):
    """Cuenta toques y cuantos frenaron, para un nivel corrido lo que se pida."""
    toques = frenos = 0
    margenes = []
    lejos_antes = {}
    for i, f in enumerate(fs):
        niveles = [n + corrimiento for n in sacar_niveles(f) if n]
        for L in niveles:
            k = round(L, 2)
            d = f["px"] - L
            if abs(d) > LEJOS:
                lejos_antes[k] = f["t"]
                continue
            if abs(d) > TOL:
                continue
            # es toque solo si venia de lejos hace poco
            ta = lejos_antes.get(k)
            if ta is None or (f["t"] - ta).total_seconds() > 60 * 30:
                continue
            lado = 1 if d > 0 else -1     # de que lado venia
            lejos_antes.pop(k, None)
            # ventana hacia adelante
            aleja = cruza = 0.0
            for j in range(i + 1, len(fs)):
                dt = (fs[j]["t"] - f["t"]).total_seconds() / 60.0
                if dt > VENTANA:
                    break
                e = (fs[j]["px"] - L) * lado      # positivo = se alejo
                aleja = max(aleja, e)
                cruza = max(cruza, -e)
            toques += 1
            if aleja > cruza:
                frenos += 1
            margenes.append(aleja - cruza)
    return toques, frenos, margenes


def informe(nombre, fs, sacar):
    t0, f0, m0 = probar(fs, sacar)
    if t0 < 12:
        print("  %-22s solo %d toques: muestra insuficiente" % (nombre, t0))
        return
    tasa = 100.0 * f0 / t0
    # placebo: el mismo nivel corrido, misma zona, mismo momento
    tp = fp = 0
    mp_ = []
    for c in PLACEBOS:
        a, b, m = probar(fs, sacar, c)
        tp += a; fp += b; mp_ += m
    tasap = 100.0 * fp / tp if tp else float("nan")
    print("  %-22s toques %4d   freno el %4.1f%%   |   placebo %4d toques, %4.1f%%   |  ventaja %+5.1f pp"
          % (nombre, t0, tasa, tp, tasap, tasa - tasap))
    if m0 and mp_:
        print("  %-22s margen medio %+6.2f pts   |   placebo %+6.2f pts"
              % ("", st.fmean(m0), st.fmean(mp_)))


def main():
    fs = leer()
    if len(fs) < 200:
        print("registro insuficiente: %d lecturas" % len(fs)); return
    print("=" * 74)
    print("RESPETO DE LOS NIVELES  --  cada uno contra su propio placebo")
    print("=" * 74)
    print("lecturas: %d   de %s a %s" % (len(fs), fs[0]["t"], fs[-1]["t"]))
    print("toque = entrar a %.1f pts viniendo de mas de %.1f;  se miran %d min hacia adelante"
          % (TOL, LEJOS, VENTANA))
    print("placebo = el mismo nivel corrido %s puntos" % str(PLACEBOS))
    print()
    informe("dominantes (ambar)", fs, lambda f: f["picos"])
    informe("call wall", fs, lambda f: [f["mp"]])
    informe("put wall", fs, lambda f: [f["mn"]])
    informe("zero gamma", fs, lambda f: [f["z"]])
    print()
    print("COMO LEERLO: si la ventaja sobre el placebo no es claramente positiva,")
    print("el nivel no esta informando nada que no informe una linea cualquiera.")


if __name__ == "__main__":
    main()
