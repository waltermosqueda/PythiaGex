# -*- coding: utf-8 -*-
"""paridad_pozos.py — paridad por libro de los pozos de CONF (MUROS/MAJORS/ZTP por volumen) y de CONF_vol contra el niv de la 4.1
del 10-09, SOLO en los minutos en que la 4.1 tenia los tres libros. Recalcula los pozos con rec_familia (misma cuenta). SOLO LECTURA."""
import ctypes, json, os, sys
ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)
import numpy as np, pandas as pd
AQUI = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, AQUI)
import rec_familia as RF
NIV = os.path.join(os.environ["APPDATA"], "ATAS", "PythiaGex4", "familia", "niv-2026-10-09-MNQZ6.jsonl")
dia = "2026-10-09"
niv = {}
for l in open(NIV, encoding="utf-8").readlines()[1:]:
    l = l.strip()
    if l.endswith("}"):
        x = json.loads(l); niv[pd.Timestamp(x["k"] * 60, unit="s")] = x
g = RF.EV.minutos_y_precio(dia)
libs = {lb: RF.Libro(lb, dia) for lb in RF.LIBROS}
st = {}
ej = []
for i, (t, F) in enumerate(zip(g["t"], g["F"].to_numpy(float))):
    if not np.isfinite(F) or t not in niv or len(niv[t]["b"]) < 3:
        continue
    x = niv[t]
    bk = {lb: libs[lb].minuto(i, t, F) for lb in RF.LIBROS}
    if any(b is None for b in bk.values()):
        st["falta_libro_mio"] = st.get("falta_libro_mio", 0) + 1
        continue
    for lb, b in bk.items():
        mine = {"MUROS_%s_vol" % lb: [p for p, _, _ in RF.muros(b["Fut"], b["GvC"], b["GvP"], F)],
                "MAJORS_%s_vol" % lb: [p for p, _, _ in RF.muros(b["Fut"], b["gv"], b["gv"], F)],
                "ZTP_%s_vol" % lb: [p for p, _ in RF.ztp(b["Fut"], b["gv"], F)]}
        for s, a in mine.items():
            bb = [e[0] for e in (x["s"].get(s) or [])]
            d = st.setdefault(s, {"n": 0, "iguales": 0})
            d["n"] += 1
            if len(a) == len(bb) and (not a or np.abs(np.sort(a) - np.sort(bb)).max() <= 0.5):
                d["iguales"] += 1
            elif len(ej) < 12:
                ej.append((str(t), s, a, bb, float(F)))
    pv = {lb: RF.pozo(b, False, F) for lb, b in bk.items()}
    c = sorted(p for p, _, _ in RF.confluencia(pv, F))
    bb = sorted(e[0] for e in (x["s"].get("CONF_vol") or []))
    d = st.setdefault("CONF_vol", {"n": 0, "iguales": 0})
    d["n"] += 1
    if len(c) == len(bb) and (not c or np.abs(np.array(c) - np.array(bb)).max() <= 0.5):
        d["iguales"] += 1
    elif len(ej) < 24:
        ej.append((str(t), "CONF_vol", c, bb, float(F)))
for s, d in st.items():
    if isinstance(d, dict):
        d["pct"] = round(100.0 * d["iguales"] / d["n"], 1) if d["n"] else None
out = {"por_serie": st, "ejemplos": ej}
json.dump(out, open(os.path.join(AQUI, "datos", "paridad_pozos.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1, default=str)
print(json.dumps(out, ensure_ascii=False, indent=1, default=str)[:5000])
