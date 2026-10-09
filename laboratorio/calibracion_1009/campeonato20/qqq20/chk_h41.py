# chequeo informativo: DOMS_QQQ_vol (q01) contra el hibrido h41 (criterio_operador/hibrido). SOLO LECTURA.
import ctypes, os, collections
ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)
import pandas as pd, numpy as np
AQUI = os.path.dirname(os.path.abspath(__file__))
R = pd.read_pickle(os.path.join(AQUI, "datos", "rayas_qqq20.pkl"))
H = pd.read_pickle(os.path.join(AQUI, "..", "..", "criterio_operador", "hibrido", "rayas_hibrido.pkl"))
mine = R["series"]["DOMS_QQQ_vol"]; M = R["meta"].set_index("t")
c = collections.Counter(); ej = []
dr = []
for t, d in H["h41"].items():
    e = mine.get(t + pd.Timedelta(minutes=1))
    if not e:
        continue
    kh = sorted(v[1] for k, v in d.items() if k.startswith("dom")); km = sorted(v[1] for v in e.values())
    ph = sorted(v[0] for k, v in d.items() if k.startswith("dom")); pm = sorted(v[0] for v in e.values())
    if kh == km:
        c["mismos strikes"] += 1
        dr.append(np.median([abs(a - b) for a, b in zip(ph, pm)]))
    elif set(kh) & set(km):
        c["uno en comun"] += 1
    else:
        c["ninguno en comun"] += 1
        if len(ej) < 5: ej.append((str(t), kh, km, d.get("_esc"), M.loc[t + pd.Timedelta(minutes=1), "rz"] if (t + pd.Timedelta(minutes=1)) in M.index else None))
print(c); print("dif de precio con los mismos strikes: mediana %.2f p90 %.2f" % (np.median(dr), np.percentile(dr, 90)))
for x in ej: print(x)
