# -*- coding: utf-8 -*-
"""
paridad_41.py — PARIDAD DE FORMULA de las series C07-C12 (grupo2.py) contra lo que la 4.1 GRABO en vivo
(%APPDATA%/ATAS/PythiaGex4/familia/niv-2026-10-09-MNQZ6.jsonl, SOLO LECTURA). Es la unica sesion con niveles grabados por la 4.1.

Que mide y que NO: compara NIVELES (la formula), nunca velas ni resultados: no usa el juez ni el arnes, no abre la CASO para
evaluar nada. Dos comparaciones por minuto k del niv:
  (A) formula pura: mi cuenta con el precio f y la conversion c que la 4.1 escribio en esa linea (aisla la formula y el libro);
  (B) tuberia: mi niveles_sesion() (F = cierre previo, conversion 'cuatro' del dataset) en el minuto t = k (y t = k+1).
Salida: probador2/paridad_41.json y texto por pantalla.
"""
import os
import sys
import json

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import grupo2 as G          # noqa: E402
D = G.D

NIV = os.path.join(os.environ.get("APPDATA", ""), "ATAS", "PythiaGex4", "familia", "niv-2026-10-09-MNQZ6.jsonl")
DIA = "2026-10-09"
SERIE = {"C07": "MAJORS_QQQ_oi", "C08": "MUROS_QQQ_oi", "C09": "MUROS_NDX_vol", "C10": "ZEST_QQQ_vol", "C11": "MUROS_NQ_oi",
         "C12": "FAM_MUROS_oi"}


def leer_niv():
    out = []
    with open(NIV, encoding="utf-8") as f:
        f.readline()
        for l in f:
            r = json.loads(l)
            out.append(r)
    return out


def _formula(c, perf, fls, F, oi_ok):
    if c == "C07":
        p = perf.get("QQQ"); return G.majors(p, F, True) if p is not None else []
    if c == "C08":
        p = perf.get("QQQ"); return G.muros(p, F, True) if p is not None else []
    if c == "C09":
        p = perf.get("NDX"); return G.muros(p, F, False) if p is not None else []
    if c == "C10":
        p = perf.get("QQQ")
        if p is None:
            return []
        z = G.zero_estandar_vol(fls["QQQ"], p["S"])
        return [(z * p["conv"], "Z", float("nan"))] if (z == z and abs(z * p["conv"] - F) <= 300) else []
    if c == "C11":
        p = perf.get("NQ"); return G.muros(p, F, True) if (p is not None and oi_ok) else []
    if c == "C12":
        if not all(lb in perf for lb in ("NQ", "NDX", "QQQ")) or not oi_ok:
            return []
        partes = []
        for lb in ("NQ", "NDX", "QQQ"):
            p = perf[lb]
            m = np.abs(p["Fut"] - F) <= F * 0.03
            partes.append((np.round(p["Fut"][m] / 5.0) * 5.0, p["goC"][m], p["goP"][m]))
        Fut = np.concatenate([a for a, _, _ in partes])
        ks, inv = np.unique(Fut, return_inverse=True)
        fus = dict(K=ks, Fut=ks, goC=np.bincount(inv, np.concatenate([b for _, b, _ in partes]), len(ks)),
                   goP=np.bincount(inv, np.concatenate([x for _, _, x in partes]), len(ks)))
        return G.muros(fus, F, True)


def main():
    niv = leer_niv()
    libs = {lb: G.LibroSesion(lb, DIA) for lb in ("NQ", "NDX", "QQQ")}
    viejo, como = G.oi_nq_viejo_hasta(DIA, libs["NQ"])
    print("OI NQ:", viejo, como)
    ini = pd.Timestamp(DIA) - pd.Timedelta(hours=2)
    A = {c: {"minutos": 0, "ambos": 0, "solo_41": 0, "solo_mio": 0, "iguales_05": 0, "dif": []} for c in SERIE}
    ejemplos = {c: [] for c in SERIE}
    for r in niv:
        t = pd.Timestamp(int(r["k"]) * 60, unit="s")
        F = float(r["f"])
        conv41 = {m["l"]: float(m["c"]) for m in r["m"]}
        perf, fls = {}, {}
        for lb, L in libs.items():
            if lb not in conv41:
                continue
            fv = D.foto_vigente(lb, DIA, t)
            if fv is None:
                continue
            v = conv41[lb]
            S = F / v if lb == "QQQ" else F - v
            fl = L.filas_hoy(int(fv["foto"]), t)
            p = G.perfil(lb, fl, S, v)
            if p is None:
                continue
            p["S"] = S; p["conv"] = v
            perf[lb] = p; fls[lb] = fl
        oi_ok = not (viejo is not None and ini <= t < viejo)
        for c, nom in SERIE.items():
            mio = _formula(c, perf, fls, F, oi_ok)
            suyo = [(float(x[0]), x[1]) for x in r["s"].get(nom, [])]
            a = A[c]; a["minutos"] += 1
            if mio and suyo:
                a["ambos"] += 1
                dm = {e: x for x, e, _ in mio}; ds = {e: x for x, e in suyo}
                ok = True
                for e in set(dm) | set(ds):
                    if e in dm and e in ds:
                        a["dif"].append(dm[e] - ds[e])
                        ok &= abs(dm[e] - ds[e]) <= 0.5
                    else:
                        ok = False
                a["iguales_05"] += ok
                if not ok and len(ejemplos[c]) < 5:
                    ejemplos[c].append({"t": str(t), "f": F, "mio": [(round(x, 2), e, k) for x, e, k in mio], "41": suyo})
            elif suyo:
                a["solo_41"] += 1
                if len(ejemplos[c]) < 5:
                    ejemplos[c].append({"t": str(t), "f": F, "mio": [], "41": suyo})
            elif mio:
                a["solo_mio"] += 1
                if len(ejemplos[c]) < 5:
                    ejemplos[c].append({"t": str(t), "f": F, "mio": [(round(x, 2), e, k) for x, e, k in mio], "41": []})
    res = {"A_formula": {}, "ejemplos_A": ejemplos, "oi_nq": [str(viejo), como], "lineas_niv": len(niv)}
    for c, a in A.items():
        d = np.abs(np.array(a["dif"])) if a["dif"] else np.array([np.nan])
        res["A_formula"][c] = {"serie_41": SERIE[c], "minutos": a["minutos"], "ambos": a["ambos"], "solo_41": a["solo_41"],
                               "solo_mio": a["solo_mio"], "pct_iguales_05_entre_ambos": 100.0 * a["iguales_05"] / a["ambos"] if a["ambos"] else None,
                               "dif_abs_mediana": float(np.nanmedian(d)), "dif_abs_p95": float(np.nanpercentile(d, 95)) if a["dif"] else None,
                               "dif_abs_max": float(np.nanmax(d))}
        print("(A)", c, json.dumps(res["A_formula"][c]))
    # (B) tuberia
    out, diag = G.niveles_sesion(DIA)
    res["B_diag"] = diag
    k41 = {int(r["k"]): r for r in niv}
    res["B_tuberia"] = {}
    for desf in (0, 1):
        for c, nom in SERIE.items():
            df = out[c]
            por_t = {}
            for t, x, e in zip(df["t"], df["nivel"], df["etiqueta"]):
                por_t.setdefault(int(pd.Timestamp(t).value // 60_000_000_000), {})[e] = float(x)
            n = amb = ig = s41 = smio = 0
            for k, r in k41.items():
                mio = por_t.get(k + desf, {})
                suyo = {e: float(x) for x, e, *_ in r["s"].get(nom, [])}
                n += 1
                if mio and suyo:
                    amb += 1
                    ig += all(e in mio and e in suyo and abs(mio[e] - suyo[e]) <= 0.5 for e in set(mio) | set(suyo))
                elif suyo:
                    s41 += 1
                elif mio:
                    smio += 1
            res["B_tuberia"]["%s_t=k%+d" % (c, desf)] = {"minutos": n, "ambos": amb, "pct_iguales_05": 100.0 * ig / amb if amb else None,
                                                         "solo_41": s41, "solo_mio": smio}
            print("(B) t=k%+d" % desf, c, json.dumps(res["B_tuberia"]["%s_t=k%+d" % (c, desf)]))
    with open(os.path.join(AQUI, "paridad_41.json"), "w", encoding="utf-8") as f:
        json.dump(res, f, ensure_ascii=False, indent=1, default=str)


if __name__ == "__main__":
    main()
