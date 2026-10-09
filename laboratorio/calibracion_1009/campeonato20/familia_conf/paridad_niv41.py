# -*- coding: utf-8 -*-
"""paridad_niv41.py — paridad de mi reconstruccion (datos/rec/2026-10-09.pkl) contra lo que la 4.1 GRABO en vivo
(%APPDATA%/ATAS/PythiaGex4/familia/niv-2026-10-09-MNQZ6.jsonl, SOLO LECTURA) para FAM_MUROS_vol, FAM_MUROS_oi y CONF_vol,
y ademas las series por libro que alimentan las combinaciones (MUROS_*_oi). Compara la clave k del niv con mi clave t = k.
Escribe datos/paridad_niv41.json."""
import ctypes, json, os, sys
ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)
import numpy as np, pandas as pd
AQUI = os.path.dirname(os.path.abspath(__file__))
NIV = os.path.join(os.environ["APPDATA"], "ATAS", "PythiaGex4", "familia", "niv-2026-10-09-MNQZ6.jsonl")
r = pd.read_pickle(os.path.join(AQUI, "datos", "rec", "2026-10-09.pkl"))
niv = {}
with open(NIV, encoding="utf-8") as fh:
    fh.readline()
    for l in fh:
        l = l.strip()
        if not l.endswith("}"):
            continue
        x = json.loads(l)
        niv[pd.Timestamp(x["k"] * 60, unit="s")] = x
grilla = r["grilla"]
out = {}
pares = {"FAM_MUROS_vol": "FAM_MUROS_vol", "FAM_MUROS_oi": "FAM_MUROS_oi", "CONF_vol": "CONF_vol"}
for mio, s41 in pares.items():
    st = dict(minutos=0, ambos=0, solo41=0, solomio=0, iguales=0, n_niv_distinto=0, difs=[])
    ej = []
    for t in grilla:
        if t not in niv:
            continue
        st["minutos"] += 1
        a = sorted(p for p, _ in r["series"][mio].get(t, {}).values())
        b = sorted(e[0] for e in (niv[t]["s"].get(s41) or []))
        if a and b:
            st["ambos"] += 1
            if len(a) != len(b):
                st["n_niv_distinto"] += 1
                if len(ej) < 6: ej.append((str(t), a, b))
            else:
                d = np.abs(np.array(a) - np.array(b)).max()
                st["difs"].append(float(d))
                if d <= 0.5: st["iguales"] += 1
                elif len(ej) < 6: ej.append((str(t), a, b))
        elif b:
            st["solo41"] += 1
            if len(ej) < 6: ej.append((str(t), a, b))
        elif a:
            st["solomio"] += 1
            if len(ej) < 6: ej.append((str(t), a, b))
    d = np.array(st.pop("difs"))
    st["dif_mediana"] = float(np.median(d)) if len(d) else None
    st["dif_p95"] = float(np.percentile(d, 95)) if len(d) else None
    st["pct_iguales_05_sobre_ambos"] = 100.0 * st["iguales"] / st["ambos"] if st["ambos"] else None
    st["ejemplos"] = ej
    out[mio] = st
# muros por OI por libro (alimentan K0/K4b)
for lb in ("NQ", "NDX", "QQQ"):
    st = dict(minutos=0, ambos=0, iguales=0, solo41=0, solomio=0)
    for t in grilla:
        if t not in niv:
            continue
        st["minutos"] += 1
        a = sorted(p for k, (p, _) in r["series"]["MUROS_oi_UNION"].get(t, {}).items() if k.startswith(lb + "."))
        b = sorted(e[0] for e in (niv[t]["s"].get("MUROS_%s_oi" % lb) or []))
        if a and b:
            st["ambos"] += 1
            if len(a) == len(b) and np.abs(np.array(a) - np.array(b)).max() <= 0.5:
                st["iguales"] += 1
        elif b:
            st["solo41"] += 1
        elif a:
            st["solomio"] += 1
    out["MUROS_%s_oi" % lb] = st
out["rango_comparado"] = [str(min(t for t in grilla if t in niv)), str(max(t for t in grilla if t in niv))]
json.dump(out, open(os.path.join(AQUI, "datos", "paridad_niv41.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1, default=str)
print(json.dumps(out, ensure_ascii=False, indent=1, default=str)[:6000])
