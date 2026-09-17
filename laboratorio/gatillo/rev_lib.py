# -*- coding: utf-8 -*-
"""
rev_lib.py — REVISION ESCEPTICA del gatillo de RECLAMO en las verdes (verdes_20_reclamo.py). Implementacion PROPIA, escrita desde la descripcion
(no copia el lazo del original): deteccion de la visita por cortes de numpy, parametros abiertos, entrada y costo configurables.
Solo LEE las tablas de 1 s (base.todas_las_sesiones) y las fotos (verdes_lib.fotos). No toca nada del banco.
"""
import os, sys, json
import numpy as np, pandas as pd
AQUI = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, AQUI)
import base as B, verdes_lib as V

DIAS7 = ["2026-09-08", "2026-09-09", "2026-09-11", "2026-09-14", "2026-09-15", "2026-09-16", "2026-09-17"]
P0 = dict(tol=1.5, lejos=10.0, visita=90, pen_max=10.0, rec=1.5, cruce=0.5, delta=True, vent_delta=10, stop_off=1.0, memoria=1800)
REVCACHE = os.path.join(AQUI, "rev_cache"); os.makedirs(REVCACHE, exist_ok=True)


def sesiones(dias=None):
    out = {}
    for s in (dias or DIAS7):
        p = os.path.join(B.CACHE, "seg-%s.parquet" % s)
        if os.path.exists(p): out[s] = pd.read_parquet(p)
    return out


class Cinta1s:
    """Los arreglos de una sesion, listos."""
    def __init__(self, seg, vent_delta=10):
        self.idx = seg.index; self.alto = seg["alto"].to_numpy(); self.bajo = seg["bajo"].to_numpy(); self.ult = seg["ultimo"].to_numpy()
        self.rueda = seg["rueda"].to_numpy(); self.n = len(self.ult); self.delta = seg["delta"].to_numpy("float64")
        self.ask = seg["ask"].to_numpy(); self.bid = seg["bid"].to_numpy(); self.rask = seg["rask"].to_numpy(); self.rbid = seg["rbid"].to_numpy()
        self.hueco = seg["hueco"].to_numpy(); self._d = {}

    def dsum(self, w):
        if w not in self._d:
            c = np.concatenate(([0.0], np.cumsum(self.delta))); i = np.arange(self.n) + 1; self._d[w] = c[i] - c[np.maximum(0, i - w)]
        return self._d[w]


def gatillos(A, Lt, P, donde=None):
    """Gatillos de RECLAMO ('cruce') para una raya Lt[t] (NaN = no vigente). donde = mascara de segundos en que se admite la ENTRADA A LA ZONA (defecto: rueda).
    Devuelve lista de dicts: i (segundo del gatillo), lado, L, ext (extremo de la mecha), pen, t0."""
    n = A.n; ar = np.arange(n); out = []; act = ~np.isnan(Lt); donde = A.rueda if donde is None else donde
    d10 = A.dsum(P["vent_delta"]) if P["delta"] else None
    for lado in (1, -1):
        with np.errstate(invalid="ignore"):
            if lado > 0: lejos = act & (A.bajo >= Lt + P["lejos"]); zona = act & (A.bajo <= Lt + P["tol"])
            else: lejos = act & (A.alto <= Lt - P["lejos"]); zona = act & (A.alto >= Lt - P["tol"])
        entra = zona.copy(); entra[1:] &= ~zona[:-1]
        ult_lejos = np.maximum.accumulate(np.where(lejos, ar, -1))
        ent_prev = np.maximum.accumulate(np.where(entra, ar, -1)); ent_prev = np.concatenate(([-1], ent_prev[:-1]))
        cand = np.flatnonzero(entra & donde & (ult_lejos >= 0) & (ar - ult_lejos <= P["memoria"]) & (ult_lejos > ent_prev))
        for t0 in cand:
            L = Lt[t0]; t1 = min(n - 1, t0 + P["visita"])
            if t1 - t0 < 2: continue
            if lado > 0: ext = np.minimum.accumulate(A.bajo[t0:t1]); pen = L - ext
            else: ext = np.maximum.accumulate(A.alto[t0:t1]); pen = ext - L
            rota = np.flatnonzero(pen > P["pen_max"]); fin = rota[0] if len(rota) else len(pen)
            ok = (pen >= P["cruce"]) & ((A.ult[t0:t1] - L) * lado >= P["rec"])
            if d10 is not None: ok &= (d10[t0:t1] * lado > 0)
            ok[0] = False; ok[fin:] = False
            k = np.flatnonzero(ok)
            if len(k):
                j = k[0]; out.append(dict(i=int(t0 + j), lado=lado, L=float(L), ext=float(ext[j]), pen=float(max(0.0, pen[j])), t0=int(t0)))
    return out


def resultado(A, g, objetivo, costo=0.96, stop_off=1.0, entrada="ult", horizonte=900, costo_gana=None, costo_pierde=None, abre=None):
    """Puntos netos por gatillo. entrada: 'ult' (ultimo del segundo del gatillo, como el banco) | 'sig' (ultimo del segundo siguiente) |
    'peor' (el peor precio del segundo siguiente) | 'punta' (la punta contraria DESPUES de la ultima orden del segundo: ask para comprar, bid para vender) |
    'abre' (la punta contraria en reposo que encuentra la PRIMERA orden posterior al segundo del gatillo; necesita abre=(ask_abre, bid_abre, seg_abre))."""
    res = []
    for x in g:
        i, lado = x["i"], x["lado"]; stop = x["ext"] - lado * stop_off; j0 = i + 1
        if i + 2 >= A.n: continue
        if entrada == "ult": ent = A.ult[i]
        elif entrada == "sig": ent = A.ult[i + 1]; j0 = i + 2
        elif entrada == "peor": ent = A.alto[i + 1] if lado > 0 else A.bajo[i + 1]; j0 = i + 2
        elif entrada == "punta": ent = A.ask[i] if lado > 0 else A.bid[i]
        elif entrada == "abre":
            ent = abre[0][i] if lado > 0 else abre[1][i]
            if not np.isfinite(ent) or ent <= 0: ent = A.ask[i] if lado > 0 else A.bid[i]
        riesgo = (ent - stop) * lado
        if riesgo <= 0:   # con entrada demorada el precio ya esta detras del stop: no hay operacion sensata (se cuenta como no tomada)
            continue
        G = 2 * riesgo if objetivo == "R2" else float(objetivo); j1 = min(A.n, i + 1 + horizonte)
        h = A.alto[j0:j1]; b = A.bajo[j0:j1]
        if lado > 0: tg = np.flatnonzero(h >= ent + G); tp = np.flatnonzero(b <= stop)
        else: tg = np.flatnonzero(b <= ent - G); tp = np.flatnonzero(h >= stop)
        a = tg[0] if len(tg) else 10**9; c = tp[0] if len(tp) else 10**9
        if entrada in ("sig", "peor") and ((lado > 0 and A.bajo[i + 1] <= stop) or (lado < 0 and A.alto[i + 1] >= stop)): pts = -riesgo; dur = 1
        elif a < c: pts = G; dur = a + 1
        elif c < a: pts = -riesgo; dur = c + 1
        elif a == c == 10**9: pts = (A.ult[j1 - 1] - ent) * lado; dur = j1 - i
        else: pts = -riesgo; dur = c + 1
        ct = costo if costo_gana is None else (costo_gana if pts > 0 else costo_pierde)
        res.append(dict(i=i, lado=lado, L=x["L"], neto=pts - ct, bruto=pts, riesgo=riesgo, gana=int(pts > 0), dur=int(dur), t=A.idx[i]))
    return res


def lineas_de(L2, paso=5.0):
    """Cada raya con identidad (redondeo a 'paso'), como el banco: devuelve lista de arreglos Lt."""
    out = []
    for col in ("d0", "d1"):
        Lv = L2[col].to_numpy(); K = np.round(Lv / paso) * paso
        for k in np.unique(K[~np.isnan(K)]): out.append(np.where(K == k, Lv, np.nan))
    return out


def correr(T, F, P, objetivo=20, corrimientos=(0.0,), rayas=None, donde_fn=None, **kw):
    """{corrimiento: DataFrame de operaciones con 'dia'} para todas las sesiones de T. rayas = funcion (seg, F) -> L2 (defecto: la del banco)."""
    rayas = rayas or V.rayas_por_segundo; out = {c: [] for c in corrimientos}
    for s, seg in sorted(T.items()):
        L2 = rayas(seg, F)
        if L2 is None or L2["d0"].notna().sum() < 600: continue
        A = Cinta1s(seg); donde = None if donde_fn is None else donde_fn(A)
        for Lt in lineas_de(L2):
            for c in corrimientos:
                r = resultado(A, gatillos(A, Lt + c, P, donde), objetivo, stop_off=P["stop_off"], **kw)
                for x in r: x["dia"] = s
                out[c] += r
    return {c: pd.DataFrame(v) for c, v in out.items()}


def resumen(d, nombre=""):
    if d is None or not len(d): return "%-34s sin gatillos" % nombre
    return "%-34s n %4d | gana %4.1f %% | neto %+5.2f pts/op (total %+7.1f) | riesgo med %.1f | dur med %3.0f s" % (nombre, len(d), 100 * d["gana"].mean(), d["neto"].mean(), d["neto"].sum(), d["riesgo"].median(), d["dur"].median())
