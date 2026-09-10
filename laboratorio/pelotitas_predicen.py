# -*- coding: utf-8 -*-
"""¿LAS PELOTITAS SON ADELANTADAS? La afirmacion del creador: "lo mas importante no es la
dominante sino la pelotita: es adelantado. Cuando la barra logra ser la mas larga empieza a ser
la dominante" y "el Max Change nos fue cantando... 45 minutos despues termino siendo la dominante".

La prueba, con nuestras cadenas por minuto (Databento convertidas, 13 dias, rueda americana):
  - En cada minuto t y para cada strike K a menos de 2 % del precio se mira el GEX por volumen de
    su barra ahora, hace 5 y hace 15 minutos (las tres pelotitas).
  - CRECE = |GEX(t)| > |GEX(t-5)| > |GEX(t-15)| y subio por lo menos 20 % en 15 min (las tres
    pelotitas adentro y alineadas). DECRECE = al reves. QUIETO = lo demas.
  - Exito = K pasa a ser la dominante de su lado (la barra mas larga del lado del precio, radio
    2 %) en algun minuto de los 45 siguientes, sin serlo en t.
  - Se compara P(exito | CRECE) contra P(exito | QUIETO) y P(exito | DECRECE), y contra el
    strike vecino (K +- 1 paso) con la misma condicion (placebo de vecindad).
Si CRECE no le gana a QUIETO, la pelotita no adelanta nada en nuestros datos.

Uso: python laboratorio/pelotitas_predicen.py ES [--dias 2026-08-19,2026-08-20] [--horizonte 45]
"""
import glob
import gzip
import json
import math
import os
import sys
from datetime import datetime, timedelta

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(RAIZ, "laboratorio"))
from auditar_vivo import perfil  # noqa: E402

RADIO_PCT = 2.0


def cadenas_de(archivo):
    out = {}
    with gzip.open(archivo, "rt", encoding="utf-8") as f:
        for l in f:
            if len(l) < 100:
                continue
            try:
                d = json.loads(l)
            except Exception:
                continue
            ts = d["cadena"]["ts"]
            if "13:30:00" <= ts[11:] <= "20:00:00":
                out[ts] = d
    return [out[t] for t in sorted(out)]


def main():
    a = [x for x in sys.argv[1:] if not x.startswith("--")]
    raiz = a[0] if a else "ES"
    arg = lambda k, d: type(d)(sys.argv[sys.argv.index(k) + 1]) if k in sys.argv else d
    horizonte = arg("--horizonte", 45)
    dias = arg("--dias", "")
    archivos = sorted(glob.glob(os.path.join(RAIZ, "datos", "simulador", "cadenas", "sim-%s-*-r902.jsonl.gz" % raiz)))
    if dias:
        archivos = [p for p in archivos if any(d in p for d in dias.split(","))]
    print("%s: %d dias de cadenas por minuto, horizonte %d min, radio %.1f %%" % (raiz, len(archivos), horizonte, RADIO_PCT))
    grupos = {"CRECE": [0, 0], "DECRECE": [0, 0], "QUIETO": [0, 0]}
    vecino = {"CRECE": [0, 0], "DECRECE": [0, 0], "QUIETO": [0, 0]}
    total_min = 0
    for p in archivos:
        cs = cadenas_de(p)
        if len(cs) < 60:
            continue
        # GEX por strike por minuto (cada cadena a su propio spot) y la dominante de cada lado
        serie = []
        for d in cs:
            c = d["cadena"]; S = float(c["spot_idx"])
            P, _ = perfil(d, S)
            g = {s["K"]: s["vol"] for s in P}
            radio = S * RADIO_PCT / 100.0
            arr = [s for s in P if S < s["K"] <= S + radio and s["vol"] != 0]
            aba = [s for s in P if S - radio <= s["K"] <= S and s["vol"] != 0]
            dom_arr = max(arr, key=lambda s: abs(s["vol"]))["K"] if arr else None
            dom_aba = max(aba, key=lambda s: abs(s["vol"]))["K"] if aba else None
            serie.append((datetime.strptime(c["ts"], "%Y-%m-%d %H:%M:%S"), S, g, dom_arr, dom_aba))
        total_min += len(serie)
        pasos = sorted({round(k2 - k1, 2) for (_, _, g, _, _) in serie[:1] for k1, k2 in zip(sorted(g)[:-1], sorted(g)[1:])})
        paso = pasos[0] if pasos else 5.0
        def indice_antes(i, minutos):
            t0 = serie[i][0] - timedelta(minutes=minutos)
            j = i
            while j > 0 and serie[j][0] > t0:
                j -= 1
            return j if serie[j][0] <= t0 + timedelta(seconds=90) else None
        for i in range(len(serie)):
            j5, j15 = indice_antes(i, 5), indice_antes(i, 15)
            if j5 is None or j15 is None or j5 == i or j15 == j5:
                continue
            t, S, g, dom_arr, dom_aba = serie[i]
            radio = S * RADIO_PCT / 100.0
            # los minutos futuros dentro del horizonte
            fut = [serie[k] for k in range(i + 1, len(serie)) if (serie[k][0] - t).total_seconds() <= horizonte * 60]
            if not fut or (fut[-1][0] - t).total_seconds() < horizonte * 60 * 0.8:
                continue
            for K, gv in g.items():
                if abs(K - S) > radio or gv == 0 or K in (dom_arr, dom_aba):
                    continue
                g5, g15 = serie[j5][2].get(K, 0.0), serie[j15][2].get(K, 0.0)
                a0, a5, a15 = abs(gv), abs(g5), abs(g15)
                if a0 > a5 > a15 and a15 > 0 and a0 >= 1.2 * a15:
                    grupo = "CRECE"
                elif a0 < a5 < a15 and a0 > 0 and a0 <= a15 / 1.2:
                    grupo = "DECRECE"
                else:
                    grupo = "QUIETO"
                exito = any(K in (f[3], f[4]) for f in fut)
                grupos[grupo][1] += 1; grupos[grupo][0] += 1 if exito else 0
                # placebo de vecindad: el strike de al lado, juzgado con la MISMA etiqueta
                Kv = K + paso
                if Kv in g and Kv not in (dom_arr, dom_aba):
                    ex_v = any(Kv in (f[3], f[4]) for f in fut)
                    vecino[grupo][1] += 1; vecino[grupo][0] += 1 if ex_v else 0
        print("  %s: %d minutos" % (os.path.basename(p)[7:17], len(serie)))
    print()
    print("minutos en total:", total_min)
    print("  %-8s %10s %8s | %s" % ("grupo", "casos", "exito", "el strike de al lado con la misma etiqueta"))
    for gname in ("CRECE", "QUIETO", "DECRECE"):
        e, n = grupos[gname]; ev, nv = vecino[gname]
        print("  %-8s %10d %7.1f%% | vecino %d casos %5.1f%%" % (gname, n, 100.0 * e / max(1, n), nv, 100.0 * ev / max(1, nv)))
    print()
    print("COMO LEERLO: si CRECE (las tres pelotitas adentro) se vuelve dominante en los %d min siguientes" % horizonte)
    print("mucho mas seguido que QUIETO, la pelotita adelanta. Si el vecino con la misma etiqueta da lo mismo,")
    print("no es el strike: es que el precio se acerca y todas las barras de esa zona crecen juntas.")


if __name__ == "__main__":
    main()
