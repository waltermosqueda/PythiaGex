# -*- coding: utf-8 -*-
"""nq01_rayas.py — FAMILIA NQ: arma las rayas por minuto (sin mirar adelante) de todas las sesiones con libro NQ de Rithmic y mide la
paridad contra lo que la 4.1 grabo esta noche (familia/niv-2026-10-09-MNQZ6.jsonl). SOLO LECTURA de datos; escribe solo en datos/.
Uso: python -I nq01_rayas.py            (todas las sesiones, 3 procesos, prioridad baja)
Salidas: datos/registros-<sesion>.pkl (minuto a minuto: fut, corr, foto, oi_ok, series), datos/velas_m1_nq.csv (velas del juez),
         datos/nq01_info.json, datos/paridad_41_0910.json/.txt."""
import ctypes
import json
import os
import sys
import time
from multiprocessing import Pool

try:
    ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)
except Exception:
    pass

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import nq_comun as C  # noqa: E402

NIV41 = os.path.join(C.P4, "familia", "niv-2026-10-09-MNQZ6.jsonl")
HOY = "2026-10-09"


def _prio():
    try:
        ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)
    except Exception:
        pass


def sesiones():
    sys.path.insert(0, C.DS)
    import cargar as D
    s = list(D.dias("NQ"))
    if HOY not in s:
        s.append(HOY)
    return sorted(s)


def tarea(dia):
    _prio()
    t0 = time.time()
    v1, iv = C.velas_sesion(dia)
    fotos, inf = C.leer_fotos(dia)
    reg, info = C.minutos_sesion(dia, fotos, v1)
    info.update(inf); info["velas"] = iv; info["seg"] = round(time.time() - t0, 1)
    info["fuentes"] = pd.Series([f["fuente"] for f in fotos]).value_counts().to_dict() if fotos else {}
    pd.to_pickle(reg, os.path.join(C.DATOS, "registros-%s.pkl" % dia))
    return dia, info, v1


def paridad():
    """La 4.1 de esta noche contra la reconstruccion con SOLO su archivo (viva4) y las velas del dataset/centinela."""
    v1, _ = C.velas_sesion(HOY)
    fotos, inf = C.leer_fotos(HOY, fuentes=("viva4",))
    reg, info = C.minutos_sesion(HOY, fotos, v1)
    mio = {r["k"]: r for r in reg}
    series = ["MUROS_NQ_vol", "MAJORS_NQ_vol", "MUROS_NQ_oi", "MAJORS_NQ_oi", "ZEST_NQ_vol", "ZEST_NQ_oi", "ZTP_NQ_vol"]
    cnt = {s: dict(n=0, iguales=0, solo41=0, solomio=0, dif=[]) for s in series}
    fut_dif = []; oi_dif = 0; nmin = 0; ejemplos = []
    with open(NIV41, encoding="utf-8", errors="replace") as fh:
        for ln in fh:
            ln = ln.strip()
            if not ln.endswith("}"):
                continue
            try:
                r = json.loads(ln)
            except Exception:
                continue
            if "k" not in r or r["k"] not in mio:
                continue
            m = mio[r["k"]]
            nmin += 1
            if r.get("f") is not None and m["fut"] == m["fut"]:
                fut_dif.append(float(r["f"]) - m["fut"])
            mq = [x for x in r.get("m", []) if x.get("l") == "NQ"]
            if mq and m["libro"] and bool(mq[0].get("o")) != bool(m["oi_ok"]):
                oi_dif += 1
            s41 = r.get("s") or {}
            for s in series:
                a = sorted((round(float(e[0]), 2), e[1]) for e in s41.get(s, []))
                b = sorted((round(float(p), 2), e) for p, e, _ in m["series"].get(s, []))
                if not a and not b:
                    continue
                c = cnt[s]; c["n"] += 1
                if a and not b:
                    c["solo41"] += 1
                elif b and not a:
                    c["solomio"] += 1
                elif len(a) == len(b) and all(x[1] == y[1] and abs(x[0] - y[0]) <= 0.011 for x, y in zip(a, b)):
                    c["iguales"] += 1
                else:
                    c["dif"].append(max(abs(x[0] - y[0]) for x, y in zip(a, b)) if len(a) == len(b) else 999.0)
                    if len(ejemplos) < 25:
                        ejemplos.append(dict(k=r["k"], t=str(pd.Timestamp(r["k"] * 60, unit="s")), serie=s, cuatro=a, mio=b, fut41=r.get("f"), fut_mio=m["fut"]))
    res = dict(minutos_comunes=nmin, fotos_viva4=inf, info=info, fut_dif_abs_med=float(np.median(np.abs(fut_dif))) if fut_dif else None,
               fut_iguales=int(sum(abs(x) < 1e-6 for x in fut_dif)), fut_n=len(fut_dif), oi_ok_distinto=oi_dif, series={}, ejemplos=ejemplos)
    lin = ["PARIDAD 4.1 (niv-2026-10-09-MNQZ6.jsonl) contra la reconstruccion con su propio archivo viva4: %d minutos comunes" % nmin,
           "  fut: %d de %d minutos iguales (|dif| mediana %s); oi_ok distinto en %d minutos con libro" % (
               res["fut_iguales"], res["fut_n"], res["fut_dif_abs_med"], oi_dif),
           "  C2 de la reconstruccion: %s (salto %s, primera foto %s)" % (info["oi_como"], info["oi_salto"], info["oi_primera"])]
    for s in series:
        c = cnt[s]
        d = np.array([x for x in c["dif"] if x < 999], float)
        res["series"][s] = dict(minutos=c["n"], iguales=c["iguales"], solo_41=c["solo41"], solo_mio=c["solomio"], distintos=len(c["dif"]),
                                dif_max=float(d.max()) if len(d) else None, dif_med=float(np.median(d)) if len(d) else None)
        lin.append("  %-14s minutos con algo %4d | iguales %4d (%.1f %%) | solo 4.1 %3d | solo mio %3d | distintos %3d (med %s, max %s)" % (
            s, c["n"], c["iguales"], 100.0 * c["iguales"] / c["n"] if c["n"] else float("nan"), c["solo41"], c["solomio"], len(c["dif"]),
            res["series"][s]["dif_med"], res["series"][s]["dif_max"]))
    txt = "\n".join(lin)
    print(txt)
    with open(os.path.join(C.DATOS, "paridad_41_0910.txt"), "w", encoding="utf-8") as fh:
        fh.write(txt + "\n\nEJEMPLOS DE DIFERENCIA\n" + "\n".join(json.dumps(e, ensure_ascii=False) for e in ejemplos) + "\n")
    with open(os.path.join(C.DATOS, "paridad_41_0910.json"), "w", encoding="utf-8") as fh:
        json.dump(res, fh, ensure_ascii=False, indent=1, default=str)


def main():
    os.makedirs(C.DATOS, exist_ok=True)
    t0 = time.time()
    ss = sesiones()
    if "--solo-paridad" not in sys.argv:
        infos = {}; velas = []
        with Pool(3, initializer=_prio) as pool:
            for dia, info, v1 in pool.imap_unordered(tarea, ss):
                infos[dia] = info; velas.append(v1)
                print("%s: %s" % (dia, json.dumps({k: info[k] for k in ("fotos", "con_libro", "minutos_corr", "doms_por_oi", "oi_como", "seg")},
                                                    ensure_ascii=False)), flush=True)
        V = pd.concat(velas, ignore_index=True).sort_values("t").drop_duplicates("t", keep="last").reset_index(drop=True)
        V.to_csv(os.path.join(C.DATOS, "velas_m1_nq.csv"), index=False)
        # conteo de fotos contra el dataset unificado
        sys.path.insert(0, C.DS)
        import cargar as D
        for d in ss:
            try:
                infos[d]["fotos_dataset"] = int(len(D.fotos("NQ", d)))
            except Exception:
                infos[d]["fotos_dataset"] = None
        with open(os.path.join(C.DATOS, "nq01_info.json"), "w", encoding="utf-8") as fh:
            json.dump(dict(sesiones=ss, info=infos, velas=dict(n=len(V), desde=str(V["t"].min()), hasta=str(V["t"].max())),
                           seg=round(time.time() - t0, 1)), fh, ensure_ascii=False, indent=1, default=str)
        print("velas %d (%s -> %s); %.0f s" % (len(V), V["t"].min(), V["t"].max(), time.time() - t0))
    paridad()


if __name__ == "__main__":
    main()
