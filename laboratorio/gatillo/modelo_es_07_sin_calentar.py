# -*- coding: utf-8 -*-
"""modelo_es_07_sin_calentar.py - AUDITORIA: los 6 disparos del vivo que NO cayeron en el segundo de un arranque. Hipotesis: 5 de ellos salieron con el modelo SIN CALENTAR
(menos de 20 velas de historia desde el ultimo arranque: GatilloModelo usa rt = rango de la propia vela y dz = 0). Se recalcula la p con los NIVELES QUE EL PROPIO VIVO ANOTO
en esa vela (centinela hoy / hoyrithmic) de dos maneras y se compara con la p del registro:
   fria     = historia = solo las velas cerradas desde el ultimo arranque (raiz ES) del log
   caliente = historia = las 60 velas previas (como se midio el modelo)"""
import io, json, math, os, sys
from datetime import datetime, timedelta
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import modelo_es_lib as M

m = M.modelo(); mu, sc, co, b0 = np.array(m["media"]), np.array(m["escala"]), np.array(m["coef"]), m["intercepto"]

# arranques (raiz ES) del log, en UTC
arr = []
with io.open(os.path.join(M.APP, "pythiagex-gammahoy.log"), encoding="utf-8", errors="replace") as fh:
    for l in fh:
        if " arranca en " in l and "raiz=ES" in l:
            try: arr.append(datetime.strptime(l[:19], "%Y-%m-%dT%H:%M:%S") + timedelta(hours=3))
            except Exception: pass
arr = pd.to_datetime(pd.Series(arr))

# niveles que anoto el vivo, por vela y por fuente
niv = {}
for fuente in ("hoy", "hoyrithmic"):
    with io.open(os.path.join(M.APP, "pythiagex-centinela-%s-MES-TimeFrame-M2.jsonl" % fuente), encoding="utf-8", errors="replace") as fh:
        for l in fh:
            try: j = json.loads(l)
            except Exception: continue
            if j.get("niv"): niv[(fuente, pd.Timestamp(j["t"][:19]).floor("2min"))] = j["niv"]

velas = M.velas_hoy()          # las velas que vio el vivo (U6 el 14-09)
D, E = M.corridas(); reb = D[D["corrida"] == E["2026-09-17"]["ultima"]].set_index("ap").sort_index()[["o", "h", "l", "c", "vol", "delta"]]
velas = pd.concat([velas, reb[~reb.index.isin(velas.index) & (reb.index >= "2026-09-17")]]).sort_index()   # el 17-09 el vivo anoto pocas velas en 'hoy': se completan con el rebobinado (mismo contrato Z6)
L = M.disparos_log(False)


def p_de(hist, v, nv):
    c, rango, delta = v["c"], v["h"] - v["l"], v["delta"]; rs = list(hist["h"] - hist["l"]); ds = list(hist["delta"]); cs = list(hist["c"])
    if len(rs) >= 20: o = sorted(rs); rt = o[len(o) // 2] or 0.25
    else: rt = rango if rango > 0 else 0.25
    dz = 0.0
    if len(ds) >= 20: mm = sum(ds) / len(ds); sd = math.sqrt(sum((x - mm) ** 2 for x in ds) / len(ds)); dz = delta / (sd if sd > 0 else 1.0)
    cum = sum(ds[-15:]) + delta; ret15 = c - cs[-15] if len(cs) >= 15 else (c - cs[0] if cs else 0.0)
    def Dn(x, sin): return sin if (x is None or x <= 0) else (x - c) / rt
    doms = [x for x in (nv.get("dom0"), nv.get("dom1")) if x]; a = [x for x in doms if x > c]; b = [x for x in doms if x <= c]
    f = [ret15 / rt, Dn(nv.get("zero_vol"), 0.0), rango / rt, Dn(nv.get("mn_vol"), -40.0), Dn(min(a) if a else None, 40.0), cum / (abs(cum) + 500.0), Dn(nv.get("mc30"), 0.0), dz, Dn(nv.get("mp_vol"), 40.0), Dn(max(b) if b else None, -40.0)]
    return 1.0 / (1.0 + math.exp(-(b0 + float(np.dot(co, (np.array(f) - mu) / sc))))), rt

print("disparo (vela UTC)      p vivo | fuente de niveles | velas desde el arranque | p FRIA (rt)      | p CALIENTE (rt)")
for r in L.itertuples(index=False):
    if r.zero <= 0: continue                     # los de "dom":0 son los del segundo del arranque (modelo_es_04)
    prev = arr[arr <= r.t + pd.Timedelta(seconds=5)]; t0 = prev.iloc[-1]; primera = t0.floor("2min") - pd.Timedelta(minutes=2)
    if r.ap not in velas.index: print("  %s sin vela del vivo" % r.ap); continue
    v = velas.loc[r.ap]; fria = velas[(velas.index >= primera) & (velas.index < r.ap)]; cal = velas[velas.index < r.ap].iloc[-60:]
    for fuente in ("hoy", "hoyrithmic"):
        nv = niv.get((fuente, r.ap))
        if not nv: continue
        pf, rtf = p_de(fria, v, nv); pc, rtc = p_de(cal, v, nv)
        print("  %s   %.2f  | %-10s zero %.2f | %2d velas (arranco %s) | %.3f (rt %.2f) | %.3f (rt %.2f, %d velas)" % (r.ap, r.p, fuente, nv.get("zero_vol") or 0, len(fria), t0.strftime("%H:%M:%S"), pf, rtf, pc, rtc, len(cal)))
