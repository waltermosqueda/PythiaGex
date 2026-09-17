# -*- coding: utf-8 -*-
"""
verdes_05_reconstruir.py — las VERDES de los dias anteriores. El historial exacto de las rayas de la capa NQ (estela) existe solo desde el 16-09, pero la
cadena viva de opciones de NQ por Rithmic se graba desde el 07-09 (%APPDATA%/ATAS/PythiaGex/viva/viva-NQ-<dia>.jsonl, una foto cada ~70 s con strike, dias,
call/put, OI, IV y volumen del dia). Con la MISMA cuenta que el indicador (laboratorio/capas_nq.recalcular: gamma Black-76 x volumen, una dominante por
lado dentro del radio, empate tecnico) se rehacen las dominantes de cada foto y se guardan con el formato de la estela.
CONTROL: el 16-09 y el 17-09 se compara la reconstruccion contra la estela real (lo que se dibujo). Si no coincide, no se usa.
"""
import os, sys, json, glob
from datetime import datetime, timezone
import numpy as np, pandas as pd
AQUI = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, AQUI); sys.path.insert(0, os.path.dirname(AQUI))
import base as B
import capas_nq as C

VIVA = os.path.join(B.APP, "PythiaGex", "viva"); REC = os.path.join(B.CACHE, "estela_rec"); os.makedirs(REC, exist_ok=True)


def a_cadena(r):
    """Una linea de viva-NQ al formato que entiende capas_nq.recalcular (igual que capas_nq.viva_local)."""
    dias = sorted(set(round(float(x[1]), 4) for x in r["filas"])); por = {}
    for x in r["filas"]:
        K, di, call, oi, iv, vol = float(x[0]), round(float(x[1]), 4), float(x[2]) >= 0.5, float(x[3]), float(x[4]), float(x[7])
        if iv <= 0 or (oi <= 0 and vol <= 0): continue
        e = por.setdefault((K, dias.index(di)), [K, dias.index(di), 0, 0, 0, 0, 0, 0])
        if call: e[2], e[4], e[6] = oi, iv, vol
        else: e[3], e[5], e[7] = oi, iv, vol
    ts = r["ts"].replace(" ", "T") + "+00:00"
    return {"generado": ts, "es_futuro": True, "cadena": {"ts": r["ts"], "spot_idx": float(r["futuro"]), "vencimientos": [{"dias": d} for d in dias], "filas": list(por.values())}}


def reconstruir(raiz="NQ"):
    hechos = []
    for p in sorted(glob.glob(os.path.join(VIVA, "viva-%s-*.jsonl" % raiz))):
        dia = os.path.basename(p)[len("viva-%s-" % raiz):-6]; sal = os.path.join(REC, "estela-%s-%s.jsonl" % (raiz, dia)); n = 0; por_oi = 0
        with open(p, encoding="utf-8", errors="replace") as f, open(sal, "w", encoding="utf-8") as out:
            for l in f:
                l = l.strip()
                if len(l) < 40 or not l.endswith("}"): continue
                try: r = json.loads(l)
                except Exception: continue
                fut = float(r.get("futuro") or 0)
                if fut <= 0 or not r.get("filas"): continue
                d = a_cadena(r); ahora = datetime.fromisoformat(d["generado"])
                try: res = C.recalcular(d, fut, 1.0, 1, ahora)
                except Exception: continue
                doms = [x[1] for x in res["doms"]]
                if not doms: por_oi += 1; continue          # sin volumen todavia (de noche el indicador cae al interes abierto): no se reconstruye
                out.write(json.dumps({"t": r["ts"].replace(" ", "T") + "Z", "d": [round(x, 2) for x in doms]}) + "\n"); n += 1
        hechos.append((dia, n, por_oi))
    return hechos


def comparar(dia, raiz="NQ"):
    from verdes_01_hoy import estela
    T = B.todas_las_sesiones(); seg = T[dia]; r = seg["rueda"].to_numpy()
    real = estela(raiz, dia).reindex(seg.index, method="ffill"); rec = estela(raiz, dia, carpeta=REC).reindex(seg.index, method="ffill")
    # comparar como CONJUNTOS: cada raya real vigente contra la reconstruida mas cercana
    a = real[["d0", "d1"]].to_numpy()[r]; b = rec[["d0", "d1"]].to_numpy()[r]
    ok = ~np.isnan(a).any(axis=1) & ~np.isnan(b).any(axis=1)
    dif = np.minimum(np.abs(a[ok][:, :, None] - b[ok][:, None, :]).min(axis=2), 999)
    print("%s: segundos de rueda comparables %d | cada raya real tiene una reconstruida a <= 2 pts en el %.1f %% de los segundos (<= 6 pts: %.1f %%)" % (
        dia, ok.sum(), 100 * (dif <= 2).mean(), 100 * (dif <= 6).mean()))


if __name__ == "__main__":
    for dia, n, sin in reconstruir(): print("reconstruido %s: %d fotos con dominantes por volumen (%d fotos sin volumen)" % (dia, n, sin))
    for dia in ("2026-09-16", "2026-09-17"): comparar(dia)
