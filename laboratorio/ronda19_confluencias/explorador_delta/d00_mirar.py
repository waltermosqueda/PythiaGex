# -*- coding: utf-8 -*-
"""Explorador DELTA (ronda 19): primera mirada a la base de DISENO (solo eventos_diseno.parquet). Prioridad baja."""
import os, sys
try:
    import ctypes, ctypes.wintypes as _W
    _k = ctypes.windll.kernel32; _k.GetCurrentProcess.restype = _W.HANDLE; _k.SetPriorityClass.argtypes = [_W.HANDLE, _W.DWORD]
    _k.SetPriorityClass(_k.GetCurrentProcess(), 0x00004000)
except Exception:
    pass
import numpy as np, pandas as pd
pd.set_option("display.width", 250); pd.set_option("display.max_columns", 200); pd.set_option("display.max_rows", 400)
SCR = r"C:\Users\wmx_7\AppData\Local\Temp\claude\6e29e09c80928880\233bf8d4-5113-4e95-a0d9-68bff726690d\scratchpad\r19"
df = pd.read_parquet(os.path.join(SCR, "eventos_diseno.parquet"))
print(df.shape, df["muestra"].unique(), df["dia"].nunique())
for c in df.columns:
    s = df[c]
    if s.dtype.kind in "if":
        print("%-28s %-8s nan %4d  min %10.3f  p50 %10.3f  max %10.3f  mean %10.3f" % (c, s.dtype, s.isna().sum(), s.min(), s.median(), s.max(), s.mean()))
    else:
        vc = s.astype(str).value_counts()
        print("%-28s %-8s uniq %4d  top: %s" % (c, s.dtype, len(vc), "; ".join("%s=%d" % (k[:25], v) for k, v in vc.head(5).items())))
