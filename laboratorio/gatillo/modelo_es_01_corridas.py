# -*- coding: utf-8 -*-
"""modelo_es_01_corridas.py - FORMA del archivo rebobinado (ningun desenlace): el archivo se escribe por CORRIDAS (cada rebobinado del grafico agrega todas sus velas
en orden). En la semana del roll conviven corridas con el grafico en U6 y en Z6 (67 puntos de diferencia): mezclar velas de dos corridas rompe todo.
Aca se listan las corridas y, por dia de rueda, cual fue la ultima que lo cubrio entero y a que precio."""
import io, json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pandas as pd
import modelo_es_lib as M

src = os.path.join(M.APP, "pythiagex-centinela-rebobinado-atas-MES-TimeFrame-M2.jsonl")
corridas = []; act = None; prev = ""
with io.open(src, encoding="utf-8", errors="replace") as fh:
    for n, l in enumerate(fh):
        i = l.find('"t":"'); t = l[i + 5:i + 24]
        if act is None or t < prev:
            act = dict(id=len(corridas), linea0=n, t0=t, t1=t, n=0, dias={}); corridas.append(act)
        prev = t; act["t1"] = t; act["n"] += 1
        hm = t[11:16]
        if "13:30" <= hm < "20:00":
            d = act["dias"].setdefault(t[:10], [0, None])
            d[0] += 1
            if hm.startswith("15:0") and d[1] is None:
                try: d[1] = json.loads(l)["c"]
                except Exception: pass
print("corridas:", len(corridas))
for c in corridas[-25:]:
    print("  #%d linea %d: %s -> %s, %d velas, %d dias de rueda" % (c["id"], c["linea0"], c["t0"], c["t1"], c["n"], len(c["dias"])))
print("\npor dia: que corridas lo cubren (id: velas de rueda, cierre ~15:00 UTC)")
dias = sorted({d for c in corridas for d in c["dias"]})
for d in dias[-9:]:
    print(d, " | ".join("#%d: %d, %s" % (c["id"], c["dias"][d][0], c["dias"][d][1]) for c in corridas if d in c["dias"])[-900:])
