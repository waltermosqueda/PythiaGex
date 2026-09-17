# -*- coding: utf-8 -*-
"""
verdes_01_hoy.py — LAS VERDES FUERTES (dominantes de la capa NQ, libro de opciones de NQ por Rithmic) tal como el operador las vio EN VIVO,
contra la cinta orden por orden. Fuente de las rayas: %APPDATA%/ATAS/PythiaGex/estela/estela-<capa>-<dia>.jsonl (cada cambio de las dominantes
de la capa, con su hora UTC: es exactamente lo que se dibujo). Primer paso: VER lo mismo que el operador ve, en numeros.
Uso: python verdes_01_hoy.py [NQ] [2026-09-17]
"""
import os, sys, json
import numpy as np, pandas as pd
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import base as B

EST = os.path.join(B.APP, "PythiaGex", "estela")


def estela(capa, dia, carpeta=None):
    """Serie por segundo (UTC) de las dominantes de la capa: columnas d0, d1, (d2). Vale desde el segundo SIGUIENTE a su marca."""
    filas = []
    for d in (pd.Timestamp(dia) - pd.Timedelta(days=1), pd.Timestamp(dia)):
        p = os.path.join(carpeta or EST, "estela-%s-%s.jsonl" % (capa, d.strftime("%Y-%m-%d")))
        if not os.path.exists(p): continue
        for l in open(p, encoding="utf-8"):
            try: j = json.loads(l)
            except Exception: continue
            v = [x if x > 0 else np.nan for x in j["d"]] + [np.nan] * 3
            filas.append((pd.Timestamp(j["t"]).tz_localize(None) + pd.Timedelta(seconds=1), v[0], v[1], v[2]))
    e = pd.DataFrame(filas, columns=["t", "d0", "d1", "d2"]).drop_duplicates("t", keep="last").set_index("t").sort_index()
    return e


def tramos(serie, tol=6.0, minimo_s=120):
    """Tramos en que una raya se queda quieta (a +- tol pts de su mediana movil): [(desde, hasta, nivel)]."""
    out = []; ini = None; ref = None; vals = []
    for t, v in serie.items():
        if np.isnan(v):
            if ini is not None and (ult - ini).total_seconds() >= minimo_s: out.append((ini, ult, float(np.median(vals))))
            ini = None; continue
        if ini is None or abs(v - ref) > tol:
            if ini is not None and (ult - ini).total_seconds() >= minimo_s: out.append((ini, ult, float(np.median(vals))))
            ini = t; ref = v; vals = []
        vals.append(v); ref = float(np.median(vals)); ult = t
    if ini is not None and (ult - ini).total_seconds() >= minimo_s: out.append((ini, ult, float(np.median(vals))))
    return out


if __name__ == "__main__":
    capa = sys.argv[1] if len(sys.argv) > 1 else "NQ"; dia = sys.argv[2] if len(sys.argv) > 2 else "2026-09-17"
    T = B.todas_las_sesiones(); seg = T[dia]; e = estela(capa, dia)
    e = e[(e.index >= seg.index[0]) & (e.index <= seg.index[-1])]
    print("capa %s, sesion %s: %d cambios de dominantes entre %s y %s" % (capa, dia, len(e), e.index.min(), e.index.max()))
    s = e.reindex(seg.index, method="ffill")
    r = seg["rueda"].to_numpy()
    for col in ("d0", "d1"):
        print("\n%s de la capa %s durante la RUEDA (tramos quietos de 2+ minutos; hora local = UTC-3):" % (col.upper(), capa))
        for a, b, nivel in tramos(s[col][r]):
            px = seg["ultimo"][a:b]
            print("   %s -> %s  raya %.2f  | precio en el tramo %.2f - %.2f  (la raya quedo %s del precio)" % (
                (a - pd.Timedelta(hours=3)).strftime("%H:%M"), (b - pd.Timedelta(hours=3)).strftime("%H:%M"), nivel, px.min(), px.max(),
                "ARRIBA" if nivel >= px.max() else "ABAJO" if nivel <= px.min() else "ADENTRO"))
