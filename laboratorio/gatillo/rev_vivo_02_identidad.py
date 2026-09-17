# -*- coding: utf-8 -*-
"""
rev_vivo_02_identidad.py — REVISION DEL DETECTOR EN VIVO: que le hace a la maquina de estados la ESTELA REAL (la que de verdad lee FlujoClaroVerdes.cs).

La estela-NQ la escriben DOS instancias de Gamma Hoy (los dos graficos de MNQ con la capa NQ): sus niveles difieren ~1 punto y se alternan en el archivo.
El detector identifica cada raya por round(nivel / 5) * 5 y BORRA (con su memoria de 'venia de lejos') la que no esta en la ultima linea. Aca se corre el
porte del vivo sobre el 16 y el 17-09 con las lineas reales de la estela, dos veces:
  EXACTA     = como esta hoy (clave redondeada a 5, borra en el acto);
  TOLERANTE  = misma raya si el nivel esta a <= 2,6 pts de una que ya se seguia; se borra recien a los 180 s de no verla.
Se cuenta cuantas veces se pierde la memoria, cuantas visitas y gatillos salen de cada forma. Solo LEE.
"""
import os, sys, time
import numpy as np, pandas as pd
AQUI = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, AQUI)
import base as B
from verdes_01_hoy import estela
import rev_vivo_01_paridad as P1

TOL, LEJOS, VISITA, PEN_MAX, REC = P1.TOL, P1.LEJOS, P1.VISITA, P1.PEN_MAX, P1.REC


class Linea:
    __slots__ = ("nivel", "la", "lb", "act", "visto")
    def __init__(self): self.nivel = 0.0; self.la = -10**9; self.lb = -10**9; self.act = None; self.visto = 0


def simular(seg, ab, fidx, fd0, fd1, vida=600, tolerante=False, gracia=180):
    alto = seg["alto"].to_numpy(); bajo = seg["bajo"].to_numpy(); ult = seg["ultimo"].to_numpy(); nord = seg["n"].to_numpy(); dl = seg["delta"].to_numpy("float64"); rueda = seg["rueda"].to_numpy()
    abre = ab["abre"].reindex(seg.index).to_numpy(); v0 = ab["v0"].reindex(seg.index).fillna(0).to_numpy(); n = len(ult)
    cs = np.concatenate(([0.0], np.cumsum(dl))); ar = np.arange(n); esf = np.zeros(n, bool); esf[fidx] = True
    ultf = np.maximum.accumulate(np.where(esf, ar, -1)); fresco = (ultf >= 0) & (ar - ultf <= vida)
    lineas = {}; gat = []; nvis = [0, 0]; pf = 0; prev = 0.0; borradas_con_memoria = 0; creadas = 0
    for i in np.flatnonzero(nord > 0):
        while pf < len(fidx) and fidx[pf] <= i:
            j = fidx[pf]; vivos = set()
            for x in (fd0[pf], fd1[pf]):
                if not np.isfinite(x) or x <= 0: continue
                if not tolerante: k = round(x / 5.0) * 5.0
                else:
                    k = None; mejor = 2.6
                    for kk, l2 in lineas.items():
                        if abs(l2.nivel - x) <= mejor: mejor = abs(l2.nivel - x); k = kk
                    if k is None: k = x
                vivos.add(k)
                if k not in lineas: lineas[k] = Linea(); creadas += 1
                lineas[k].nivel = x; lineas[k].visto = j
            for k in [k for k in lineas if k not in vivos and lineas[k].act is None and (not tolerante or j - lineas[k].visto > gracia)]:
                if j - max(lineas[k].la, lineas[k].lb) <= 1800: borradas_con_memoria += 1
                del lineas[k]
            pf += 1
        o = abre[i] if np.isfinite(abre[i]) else ult[i]
        ext = (alto[i], bajo[i]) if abs(alto[i] - o) <= abs(o - bajo[i]) else (bajo[i], alto[i])
        seq = [o]
        for x in ext + (ult[i],):
            if x != seq[-1]: seq.append(x)
        pcs = [prev if prev > 0 else o] + seq[:-1]; prev = seq[-1]
        if not fresco[i]: continue
        for k, ln in list(lineas.items()):
            L = ln.nivel
            if ln.act is None:
                if bajo[i] >= L + LEJOS: ln.la = i; continue
                if alto[i] <= L - LEJOS: ln.lb = i; continue
            for q, p in enumerate(seq):
                pc = pcs[q]
                if p >= L + LEJOS: ln.la = i
                elif p <= L - LEJOS: ln.lb = i
                v = ln.act
                if v is None:
                    lado = 0
                    if p <= L + TOL and p > L - PEN_MAX and i - ln.la <= 1800: lado = 1
                    elif p >= L - TOL and p < L + PEN_MAX and i - ln.lb <= 1800: lado = -1
                    if lado == 0: continue
                    if lado > 0: ln.la = -10**9
                    else: ln.lb = -10**9
                    ln.act = v = dict(lado=lado, t0=i, ext=p, L=L); nvis[1 if rueda[i] else 0] += 1
                    continue
                v["ext"] = min(v["ext"], p) if v["lado"] > 0 else max(v["ext"], p)
                pen = (v["L"] - v["ext"]) * v["lado"]
                if pen > PEN_MAX: ln.act = None; continue
                if i - v["t0"] > VISITA: ln.act = None; continue
                g = q == 0 and pen >= 0.5 and (pc - v["L"]) * v["lado"] >= REC
                if g: g = (cs[i] - cs[max(0, i - 9)] + v0[i]) * v["lado"] > 0
                if not g: continue
                gat.append(dict(i=int(i), lado=v["lado"], ent=float(p), stop=float(v["ext"] - v["lado"] * 1.0), L=float(v["L"]), pen=float(max(0.0, pen)), dz=int(i - v["t0"]), rth=bool(rueda[v["t0"]])))
                ln.act = None
    return gat, nvis, creadas, borradas_con_memoria


if __name__ == "__main__":
    t_ini = time.time(); dias = ["2026-09-16", "2026-09-17"]; AB = P1.aperturas(dias)
    for s in dias:
        seg = pd.read_parquet(os.path.join(B.CACHE, "seg-%s.parquet" % s)); e = estela("NQ", s); e = e[(e.index >= seg.index[0]) & (e.index <= seg.index[-1])]
        idx = seg.index.get_indexer(e.index); ok = idx >= 0; fidx = idx[ok]; fd0 = e["d0"].to_numpy()[ok]; fd1 = e["d1"].to_numpy()[ok]
        # cuanto difiere una linea de la anterior (los dos escritores): saltos de 0,5 a 2,5 pts en la misma raya
        dd = np.abs(np.diff(fd0)); dd = dd[np.isfinite(dd)]
        print("\n%s: %d lineas de estela en la sesion | linea siguiente a 0,5-2,5 pts de la anterior en d0: %.0f %% | cambia la CLAVE redondeada de d0: %d veces, de d1: %d veces" % (
            s, len(fidx), 100 * ((dd >= 0.5) & (dd <= 2.5)).mean(), int((np.diff(np.round(fd0 / 5) * 5) != 0).sum()), int((np.diff(np.round(fd1 / 5) * 5) != 0).sum())))
        for nombre, tol in (("EXACTA (como esta hoy)", False), ("TOLERANTE (<= 2,6 pts, gracia 180 s)", True)):
            g, nv, cre, bor = simular(seg, AB[s], fidx, fd0, fd1, tolerante=tol)
            for x, p in zip(g, P1.puntos_vivo(seg, g)): x["pts"] = p
            print("  %-38s rayas creadas %5d | borradas CON memoria de 'lejos' viva %5d | visitas rueda %3d / fuera %3d" % (nombre, cre, bor, nv[1], nv[0]))
            print(P1.linea("    gatillos en rueda", [x["pts"] for x in g if x["rth"]])); print(P1.linea("    gatillos fuera de rueda", [x["pts"] for x in g if not x["rth"]]))
    print("\n(%.0f s)" % (time.time() - t_ini))
