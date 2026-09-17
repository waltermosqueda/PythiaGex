# -*- coding: utf-8 -*-
"""modelo_es_04_arranques.py - AUDITORIA del registro en vivo: cada disparo 'modelo·es10' del log de Gamma Hoy contra el ultimo ARRANQUE del indicador (raiz ES).
Si el disparo cae en el mismo segundo que el arranque, no es el modelo leyendo el mercado: es el modelo recien nacido (sin historia: rt = rango de la vela, dz = 0)
y sin niveles cargados (rellenos -40/+40), que da p ~ 0,24 -> CORTO. Hora del log = hora local del operador (UTC-3)."""
import io, os, re
from datetime import datetime, timedelta

APP = os.path.join(os.environ.get("APPDATA", ""), "ATAS"); src = os.path.join(APP, "pythiagex-gammahoy.log")
ult_arranque = None; filas = []; n_arr_tarde = {}
with io.open(src, encoding="utf-8", errors="replace") as fh:
    for l in fh:
        if len(l) < 21 or l[4] != "-" or l[10] != "T": continue
        try: t = datetime.strptime(l[:19], "%Y-%m-%dT%H:%M:%S")
        except Exception: continue
        if " arranca en " in l and "raiz=ES" in l:
            ult_arranque = t; u = t + timedelta(hours=3)
            if 18 <= u.hour < 20: n_arr_tarde[u.strftime("%Y-%m-%d")] = n_arr_tarde.get(u.strftime("%Y-%m-%d"), 0) + 1
        elif "GATILLO modelo" in l:
            m = re.search(r"en ([\d.]+) p=([\d.]+)", l); seg = (t - ult_arranque).total_seconds() if ult_arranque else None
            filas.append((t + timedelta(hours=3), float(m.group(1)), float(m.group(2)), seg))
print("disparos modelo·es10 en el log de Gamma Hoy: %d" % len(filas))
rec = 0
for u, px, p, seg in filas:
    nacido = seg is not None and seg <= 5; rec += nacido
    print("  %s UTC  precio %.2f  p %.2f  | %s" % (u.strftime("%Y-%m-%d %H:%M:%S"), px, p, ("MISMO SEGUNDO QUE UN ARRANQUE del indicador (%.0f s)" % seg) if nacido else ("%.0f s despues del ultimo arranque" % seg if seg is not None else "sin arranque previo")))
print("\n%d de %d disparos caen dentro de los 5 s de un arranque del indicador (recien nacido, sin historia y sin niveles)" % (rec, len(filas)))
print("arranques del indicador (raiz ES) en la tarde 18-20 UTC, por dia:", n_arr_tarde)
