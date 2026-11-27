# -*- coding: utf-8 -*-
"""Explorador DELTA: comprobacion INDEPENDIENTE de los numeros de las candidatas (sin d_lib): lee el parquet crudo, arma la regla desde
cero, recorre una posicion por vez y recalcula n, acierto, neto y t por rueda. Si no coincide con d_lib, avisa."""
import math, os, sys
import numpy as np, pandas as pd
P = r"C:\Users\wmx_7\AppData\Local\Temp\claude\6e29e09c80928880\233bf8d4-5113-4e95-a0d9-68bff726690d\scratchpad\r19\eventos_diseno.parquet"
d = pd.read_parquet(P)
assert set(d.muestra) == {"diseno"}
def sx(h):
    if not isinstance(h, str) or h.count(":") != 2: return np.nan
    a, b, c = map(int, h.split(":")); return (a - 9) * 3600 + (b - 30) * 60 + c
d["rap"] = (d.seg_rueda - d.hora_ext.map(sx)) <= 107
d["fade_pts"] = [(r - 0.25) if m == "stop" else (-(r + 0.25) if m == "objetivo" else -p) for m, r, p in zip(d.S6_motivo, d.R, d.S6_pts)]
en_hora = (d.seg_rueda >= 1800) & (d.seg_rueda < 21600)
ap = d.of_apilados_favor_3v >= 1; sa = d.ctx_ret_con_agresores == 0; f3n = (d.F3 == 1) & (d.n_niveles_en_zona >= 1)
dias = sorted(d.dia.unique())
for nom, m in (("w1", en_hora & (ap | sa) & d.rap), ("w6", en_hora & (ap | f3n) & d.rap), ("a2", en_hora & sa & d.rap)):
    e = d[m].sort_values(["dia", "seg_rueda", "t_utc_ms"])
    pts = []; por_dia = {x: 0.0 for x in dias}
    for dia, g in e.groupby("dia"):
        libre = -1; n = 0
        for _, r in g.iterrows():
            if r.seg_rueda <= libre or n >= 5: continue
            pts.append(r.fade_pts); por_dia[dia] += r.fade_pts - 0.5; libre = r.S6_seg_salida; n += 1
    pts = np.array(pts); s = np.array([por_dia[x] for x in dias])
    t = s.mean() / s.std(ddof=1) * math.sqrt(len(s))
    print("%s (independiente): n %d | acierto %.1f %% | bruto %+.3f | neto c050 %+.3f c100 %+.3f | t por rueda c050 %+.2f | mejor %+.2f peor %+.2f" % (
        nom, len(pts), 100 * (pts > 0).mean(), pts.mean(), pts.mean() - 0.5, pts.mean() - 1.0, t, s.max(), s.min()))
