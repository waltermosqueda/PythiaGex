# -*- coding: utf-8 -*-
"""rev_09_varios.py — controles sueltos: (a) UNA posicion por vez (un bot no puede tener dos operaciones abiertas a la vez); (b) cuanta seleccion hay en el titular:
las 12 combinaciones variante x objetivo del banco, con las funciones ORIGINALES; (c) operaciones que pisan huecos de datos; (d) por lado y por raya de arriba/abajo;
(e) el dia como regimen: el resultado de las verdes de cada dia contra el resultado del MISMO gatillo en todos los corrimientos de ese dia (el dia manda o la raya manda?)."""
import os, sys
import numpy as np, pandas as pd
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rev_lib as R, verdes_lib as V
import verdes_20_reclamo as O

T = R.sesiones(); F, _ = V.fotos("NQ")
print("(a) una posicion por vez (se saltea el gatillo si hay una operacion abierta; sin repetidos):")
for obj in (20, 12):
    real = pd.read_parquet(os.path.join(R.REVCACHE, "real_obj%s.parquet" % obj)).drop_duplicates(["dia", "i", "lado"]).sort_values(["dia", "i"]); keep = []
    for d, g in real.groupby("dia"):
        libre = -1
        for x in g.itertuples():
            if x.i >= libre: keep.append(x.Index); libre = x.i + x.dur
    u = real.loc[keep]; print("   objetivo %s: todas %s" % (obj, R.resumen(real))); print("   objetivo %s: una x vez %s | sin 16/17: %+.2f (n %d)" % (obj, R.resumen(u), u[~u["dia"].isin(["2026-09-16", "2026-09-17"])]["neto"].mean(), (~u["dia"].isin(["2026-09-16", "2026-09-17"])).sum()))

print("\n(b) las 12 combinaciones del banco (funciones originales), VERDES 7 dias | sin 16 y 17-09:")
for variante, con_delta in O.VARIANTES:
    for obj in (12, 20, "R2"):
        r = []; r2 = []
        for s, seg in T.items():
            for Lt in R.lineas_de(V.rayas_por_segundo(seg, F)):
                x = [y[0] for y in O.resultado(seg, O.gatillos(seg, Lt, variante, con_delta), obj)]; r += x
                if s not in ("2026-09-16", "2026-09-17"): r2 += x
        print("   %-6s %-9s objetivo %-3s n %3d neto %+.2f | sin 16/17: n %3d neto %+.2f" % (variante, "+ delta" if con_delta else "", obj, len(r), np.mean(r), len(r2), np.mean(r2)))

print("\n(c) huecos de datos y (d) cortes simples (objetivo 20):")
real = pd.read_parquet(os.path.join(R.REVCACHE, "real_obj20.parquet")); hh = []
for x in real.itertuples():
    h = T[x.dia]["hueco"].to_numpy(); hh.append(bool(h[max(0, x.i - 90): x.i + x.dur + 1].any()))
real["hueco"] = hh; print("   operaciones que pisan un hueco de datos: %d de %d" % (real["hueco"].sum(), len(real)))
for k, g in real.groupby("lado"): print("   lado %+d (%s): n %d neto %+.2f | sin 16/17 %+.2f" % (k, "compra en soporte" if k > 0 else "venta en resistencia", len(g), g["neto"].mean(), g[~g["dia"].isin(["2026-09-16", "2026-09-17"])]["neto"].mean()))
hora = real["t"].dt.hour + real["t"].dt.minute / 60.0
for nombre, m in (("13:30-15:00 UTC", hora < 15), ("15:00-18:00", (hora >= 15) & (hora < 18)), ("18:00-20:00", hora >= 18)): print("   %s: n %d neto %+.2f" % (nombre, m.sum(), real[m]["neto"].mean()))

print("\n(e) el dia manda o la raya manda? neto medio por dia: verdes | todos los corrimientos de -75 a +75 (el mismo gatillo en rayas cualesquiera)")
D = pd.read_parquet(os.path.join(R.REVCACHE, "perfil_corrimientos.parquet")); D = D[D["obj"] == 20]
a = D[D["corr"] == 0.0].groupby("dia")["neto"].mean(); b = D[D["corr"] != 0.0].groupby("dia")["neto"].mean(); sd = D[D["corr"] != 0.0].groupby(["dia", "corr"])["neto"].mean().groupby("dia").std()
rk = {d: int((D[(D["dia"] == d) & (D["corr"] != 0.0)].groupby("corr")["neto"].mean() >= a[d]).sum()) for d in a.index}
for d in a.index: print("   %s verdes %+.2f | cualquier raya %+.2f (desvio entre corrimientos %.2f) | corrimientos que igualan o superan a las verdes ese dia: %d de 60" % (d, a[d], b[d], sd[d], rk[d]))
print("   correlacion entre dias (verdes, cualquier raya): %.2f" % np.corrcoef(a.to_numpy(), b.reindex(a.index).to_numpy())[0, 1])
