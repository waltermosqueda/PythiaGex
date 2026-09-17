# -*- coding: utf-8 -*-
"""barridos_micro.py — DESCRIPTIVO, sin veredicto: que se aprende de la microestructura de barridos, ordenes grandes y rafagas.
Se muestra partido en explorar / confirmar solo para ver si una lectura se repite en las dos mitades (lo que no repite, no se le cuenta al operador)."""
import time
import numpy as np, pandas as pd
import barridos_lib as L
from barridos_explorar import correr
B = L.B

t0 = time.time()
Tt = B.todas_las_sesiones(); MIT = {"explorar": B.explorar(Tt), "confirmar": B.confirmar(Tt)}
pd.set_option("display.width", 250); pd.set_option("display.max_columns", 50)

# ---------------------------------------------------------------- A. por minuto: quien explica la vela y quien la siguiente
print("A. CORRELACION POR MINUTO (rueda): flujo del minuto contra el retorno del MISMO minuto y del SIGUIENTE")
for nom, T in MIT.items():
    partes = []
    for s, d in T.items():
        r = d[d["rueda"]]
        g = r.resample("1min")
        m = pd.DataFrame({"ret": g["ultimo"].last() - g["ultimo"].first(), "d1": g["d1"].sum(), "d2_4": g["d2_4"].sum(), "d5_9": g["d5_9"].sum(),
                          "d10_49": g["d10_49"].sum(), "d50": g["d50"].sum(), "barrido_neto": (g["barre_c"].sum() - g["barre_v"].sum()), "delta": g["delta"].sum(),
                          "n": g["n"].sum()}).dropna()
        m["ret_sig"] = m["ret"].shift(-1); partes.append(m)
    M = pd.concat(partes)
    cols = ["d1", "d2_4", "d5_9", "d10_49", "d50", "barrido_neto", "delta"]
    print(" %s (%d min): mismo minuto  " % (nom, len(M)) + "  ".join("%s %+.2f" % (c, M[c].corr(M["ret"])) for c in cols))
    print(" %s          : min siguiente " % nom + "  ".join("%s %+.2f" % (c, M[c].corr(M["ret_sig"])) for c in cols))
    print(" %s          : d50 con d1 (mismo minuto) %+.2f | d50 con d10_49 %+.2f | minutos con algun d50: %.0f %%" % (nom, M["d50"].corr(M["d1"]), M["d50"].corr(M["d10_49"]), 100 * (M["d50"] != 0).mean()))

# ---------------------------------------------------------------- B/C. movimiento medio firmado despues de cada evento, con error estandar
print("\nB. MOVIMIENTO MEDIO A FAVOR DEL LADO PRE-REGISTRADO (puntos, +- error estandar) — las 12 variantes, por mitad. Descriptivo.")
RES = {nom: correr(T) for nom, T in MIT.items()}
for v in sorted(RES["explorar"]):
    for nom in MIT:
        r = RES[nom].get(v)
        if r is None: continue
        ok = r["y8"] != 0; ac = 100 * ((r["y8"] * r["lado"] > 0)[ok]).mean()
        txt = "  ".join("%ds %+.2f+-%.2f" % (h, r["mov%d" % h].mean(), r["mov%d" % h].std() / np.sqrt(len(r))) for h in (10, 30, 60, 120, 300))
        print(" %-40s %-9s n %3d  ac+-8 %4.1f %%  | %s" % (v, nom, len(r), ac, txt))

# ---------------------------------------------------------------- D. la rafaga y el barrido como aviso de MOVIMIENTO (no de direccion)
print("\nD. AVISO DE MOVIMIENTO: segundos hasta resolver +-8 (cualquiera de los dos lados) y recorrido absoluto a 60 s, contra segundos al azar de la misma media hora")
rng = np.random.default_rng(7)
for v in ("V01_BARRIDO_SEGUIR", "V05_RAFAGA_SEGUIR", "V11_D50_AISLADO_A_FAVOR"):
    for nom, T in MIT.items():
        r = RES[nom][v]; tr = []; ta = []; ar = []; aa = []
        for s, d in T.items():
            rs = r[r["sesion"] == s]
            if len(rs) == 0: continue
            y, seg = B.barrera(d, 8, 600); p = d["ultimo"].to_numpy(); n = len(p)
            hms = d.index.strftime("%H:%M:%S"); e = d["rueda"].to_numpy() & (hms >= L.INICIO_RUEDA) & (hms < L.FIN_RUEDA); mh = d.index.floor("30min").strftime("%H:%M").to_numpy()
            i = rs["i"].to_numpy(); tr.append(seg[i]); ar.append(np.abs(p[np.minimum(i + 60, n - 1)] - p[i]))
            for m, g in rs.groupby("media_hora"):
                el = np.flatnonzero(e & (mh == m)); j = rng.choice(el, size=min(len(el), 20 * len(g)), replace=False)
                ta.append(seg[j]); aa.append(np.abs(p[np.minimum(j + 60, n - 1)] - p[j]))
        tr = np.concatenate(tr); ta = np.concatenate(ta); ar = np.concatenate(ar); aa = np.concatenate(aa)
        tr = np.where(np.isinf(tr), 600, tr); ta = np.where(np.isinf(ta), 600, ta)
        print(" %-26s %-9s mediana s hasta +-8: evento %4.0f | azar %4.0f  ||  |mov 60 s| medio: evento %.2f | azar %.2f" % (v, nom, np.median(tr), np.median(ta), ar.mean(), aa.mean()))
print("\n%.0f s" % (time.time() - t0))
