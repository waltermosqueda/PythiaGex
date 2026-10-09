# -*- coding: utf-8 -*-
"""explorar_fotos.py — cobertura de fotos QQQ (fotos_qqq.pkl de v41_01) por sesion y tramo. SOLO LECTURA. Uso: python -I explorar_fotos.py"""
import ctypes, os, collections
try:
    ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)
except Exception:
    pass
import pandas as pd, numpy as np
SCR = os.path.join(os.environ["LOCALAPPDATA"], "Temp", "claude", "C--Users-wmx-7-OneDrive-Escritorio-ATAS-nada",
                   "f5879819-e2a4-405b-87b0-959ace090d7b", "scratchpad", "v41")
F = pd.read_pickle(os.path.join(SCR, "fotos_qqq.pkl"))
print("fotos", len(F), F[0]["gen"], F[-1]["gen"], "claves", sorted(F[0].keys()))
c = collections.defaultdict(lambda: collections.Counter())
for f in F:
    g = f["gen"]; s = (g + pd.Timedelta(hours=2)).strftime("%Y-%m-%d")
    h = g.hour + g.minute / 60
    tramo = "rueda" if 13.5 <= h < 20 else ("noche" if (h >= 22 or h < 13.5) else "cierre")
    c[s][tramo] += 1
    c[s]["vivo_" + tramo] += bool(f.get("vivo"))
    c[s]["rz_" + tramo] += bool(f.get("razon") == f.get("razon"))
for s in sorted(c):
    print(s, dict(c[s]))
