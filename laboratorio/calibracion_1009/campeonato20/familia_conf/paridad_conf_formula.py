# -*- coding: utf-8 -*-
"""paridad_conf_formula.py — aisla la FORMULA de CONF_vol: confluencia() de rec_familia con los pozos que la 4.1 GRABO en el niv del
10-09 (MUROS/MAJORS/ZTP por volumen de NQ y NDX, MUROS/MAJORS de QQQ) + mi ZTP_QQQ_vol (la 4.1 lo calcula y lo mete al pozo pero no lo
graba: no esta en CatalogoFamilia.Calc), con el precio f de la linea. Compara contra el CONF_vol grabado. SOLO LECTURA."""
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
Q = RF.Libro("QQQ", dia)
n = ig = 0; ig_sinztpq = 0; ej = []
for i, (t, F) in enumerate(zip(g["t"], g["F"].to_numpy(float))):
    if not np.isfinite(F) or t not in niv:
        continue
    x = niv[t]; f = x["f"]
    pools = {}
    for lb in x["b"]:
        ps = []
        for s in ("MUROS", "MAJORS", "ZTP"):
            ps += [e[0] for e in (x["s"].get("%s_%s_vol" % (s, lb)) or [])]
        pools[lb] = ps
    sin = {k: list(v) for k, v in pools.items()}
    if "QQQ" in pools:
        b = Q.minuto(i, t, f)
        if b is not None:
            pools["QQQ"] += [p for p, _ in RF.ztp(b["Fut"], b["gv"], f)]
    c = sorted(p for p, _, _ in RF.confluencia(pools, f))
    c0 = sorted(p for p, _, _ in RF.confluencia(sin, f))
    bb = sorted(e[0] for e in (x["s"].get("CONF_vol") or []))
    n += 1
    ok = len(c) == len(bb) and (not c or np.abs(np.array(c) - np.array(bb)).max() <= 0.5)
    ok0 = len(c0) == len(bb) and (not c0 or np.abs(np.array(c0) - np.array(bb)).max() <= 0.5)
    ig += ok; ig_sinztpq += ok0
    if not ok and len(ej) < 10:
        ej.append((str(t), f, c, bb))
out = {"minutos": n, "iguales_con_ztp_qqq_mio": ig, "iguales_sin_ztp_qqq": ig_sinztpq, "ejemplos": ej}
json.dump(out, open(os.path.join(AQUI, "datos", "paridad_conf_formula.json"), "w", encoding="utf-8"), indent=1, default=str)
print(json.dumps(out, indent=1, default=str)[:3000])
