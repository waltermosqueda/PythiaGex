# -*- coding: utf-8 -*-
"""
verdes_lib.py — LAS VERDES (dominantes del libro de opciones del propio futuro, por Rithmic) contra la cinta orden por orden, para NQ/MNQ y para ES/MES.

De donde salen las rayas: la cadena viva grabada (%APPDATA%/ATAS/PythiaGex/viva/viva-<NQ|ES>-<dia>.jsonl, una foto cada ~70 s) pasada por LA MISMA cuenta
del indicador (laboratorio/capas_nq.recalcular: gamma Black-76 x volumen del dia por strike; una dominante por lado dentro del radio; empate tecnico).
Cada foto da: las dos dominantes (precio + FUERZA = |gamma x volumen| del strike), la gamma neta del libro y la fuerza de todos los strikes.
La foto trae el precio del futuro del grafico en ese instante: la diferencia con la cinta en ese mismo segundo corrige cualquier cambio de contrato (roll).

Toque: el precio venia de >= LEJOS puntos y entra a <= TOL de una raya VIGENTE (con foto de menos de 5 min). Orden LIMITADA en la raya: se ejecuta solo si
el precio la toca de verdad en los 2 minutos siguientes. Resultado desde la raya: +G a favor antes que -S en contra, 15 minutos.
"""
import os, sys, json, glob, bisect
from datetime import datetime
import numpy as np, pandas as pd
AQUI = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, AQUI); sys.path.insert(0, os.path.dirname(AQUI))
import base as B, capas_nq as C
from rayas_base import barrera_asim

VIVA = os.path.join(B.APP, "PythiaGex", "viva")
# parametros por mercado (ES se mueve ~1/4 de NQ en puntos): tolerancia, 'venia de lejos', reglas (G, S), corrimientos del placebo, redondeo de identidad
MERCADO = {
    "NQ": dict(tol=1.5, lejos=10.0, reglas=((12, 6), (20, 8), (8, 8)), placebo=(12.5, -12.5, 7.0, -7.0), paso=5.0, radio=100.0, costo_gana=0.60, costo_pierde=0.78),
    "ES": dict(tol=0.5, lejos=2.5, reglas=((3, 1.5), (5, 2), (2, 2)), placebo=(2.5, -2.5, 1.25, -1.25), paso=1.0, radio=25.0, costo_gana=0.24, costo_pierde=0.37),
}


def a_cadena(r):
    dias = sorted(set(round(float(x[1]), 4) for x in r["filas"])); por = {}
    for x in r["filas"]:
        K, di, call, oi, iv, vol = float(x[0]), round(float(x[1]), 4), float(x[2]) >= 0.5, float(x[3]), float(x[4]), float(x[7])
        if iv <= 0 or (oi <= 0 and vol <= 0): continue
        e = por.setdefault((K, dias.index(di)), [K, dias.index(di), 0, 0, 0, 0, 0, 0])
        if call: e[2], e[4], e[6] = oi, iv, vol
        else: e[3], e[5], e[7] = oi, iv, vol
    return {"generado": r["ts"].replace(" ", "T") + "+00:00", "es_futuro": True,
            "cadena": {"ts": r["ts"], "spot_idx": float(r["futuro"]), "vencimientos": [{"dias": d} for d in dias], "filas": list(por.values())}}


def fotos(raiz, refrescar=False):
    """Todas las fotos del libro con dominantes por volumen: DataFrame (t, fut, d0, f0, d1, f1, neto, total_cerca) + dict t -> {strike redondeado: fuerza}."""
    pq = os.path.join(B.CACHE, "verdes_fotos_%s.parquet" % raiz); pj = os.path.join(B.CACHE, "verdes_perfil_%s.json" % raiz)
    if not refrescar and os.path.exists(pq) and os.path.exists(pj): return pd.read_parquet(pq), json.load(open(pj))
    M = MERCADO[raiz]; filas = []; perf = {}
    for p in sorted(glob.glob(os.path.join(VIVA, "viva-%s-*.jsonl" % raiz))):
        for l in open(p, encoding="utf-8", errors="replace"):
            l = l.strip()
            if len(l) < 40 or not l.endswith("}"): continue
            try: r = json.loads(l)
            except Exception: continue
            fut = float(r.get("futuro") or 0)
            if fut <= 0 or not r.get("filas"): continue
            try: res = C.recalcular(a_cadena(r), fut, 1.0, 1, datetime.fromisoformat(r["ts"].replace(" ", "T") + "+00:00"))
            except Exception: continue
            ds = res["doms"]
            if not ds: continue
            cerca = [x for x in res["perfil"] if abs(x[1] - fut) <= M["radio"]]
            filas.append(dict(t=pd.Timestamp(r["ts"]), fut=fut, d0=ds[0][1], f0=abs(ds[0][2]) / 1e6, d1=ds[1][1] if len(ds) > 1 else np.nan, f1=abs(ds[1][2]) / 1e6 if len(ds) > 1 else np.nan,
                              neto=res["net_vol"] / 1e6, total_cerca=sum(abs(x[2]) for x in cerca) / 1e6))
    d = pd.DataFrame(filas).drop_duplicates("t", keep="last").sort_values("t").reset_index(drop=True); d.to_parquet(pq); json.dump(perf, open(pj, "w"))
    return d, perf


def rayas_por_segundo(seg, F, vida_s=300):
    """Las dos verdes llevadas a la grilla de 1 s de la cinta, en precio de la CINTA (corrige el contrato con fut de la foto contra la cinta en ese segundo).
    Devuelve DataFrame por segundo: d0, f0, d1, f1, neto (NaN si la ultima foto tiene mas de vida_s)."""
    f = F[(F["t"] >= seg.index[0]) & (F["t"] <= seg.index[-1])].copy()
    if not len(f): return None
    px = seg["ultimo"].reindex(f["t"].dt.floor("s"), method="ffill").to_numpy(); dif = pd.Series(px - f["fut"].to_numpy())
    # corrimiento de contrato (roll): mediana movil de 31 fotos de (cinta - futuro de la foto); si es de pocos ticks es el mismo contrato y no se corrige
    corr = dif.rolling(31, center=True, min_periods=5).median().bfill().ffill().to_numpy()
    if np.nanmedian(np.abs(corr)) < 3.0: corr = np.zeros(len(f))
    rayas_por_segundo.ultimo_corrimiento = float(np.nanmedian(corr))
    for c in ("d0", "d1"): f[c] = f[c].to_numpy() + corr
    f["t"] = f["t"].dt.floor("s") + pd.Timedelta(seconds=1); f = f.drop_duplicates("t", keep="last").set_index("t")
    return f[["d0", "f0", "d1", "f1", "neto", "total_cerca"]].reindex(seg.index, method="ffill", tolerance=pd.Timedelta(seconds=vida_s))


def toques(seg, L2, raiz, delta=0.0):
    """Toques de las verdes (o de su placebo corrido 'delta') con llenado realista. Una fila por orden EJECUTADA."""
    M = MERCADO[raiz]; alto = seg["alto"].to_numpy(); bajo = seg["bajo"].to_numpy(); ult = seg["ultimo"].to_numpy(); nord = seg["n"].to_numpy(); rueda = seg["rueda"].to_numpy()
    n = len(ult); ar = np.arange(n); out = []
    for col, fcol in (("d0", "f0"), ("d1", "f1")):
        Lv = L2[col].to_numpy(); Fv = L2[fcol].to_numpy()
        K = np.round(Lv / M["paso"]) * M["paso"]
        for k in np.unique(K[~np.isnan(K)]):
            Lt = np.where(K == k, Lv, np.nan) + delta
            for lado in (1, -1):
                act = ~np.isnan(Lt)
                with np.errstate(invalid="ignore"):
                    if lado > 0: lej = act & (bajo >= Lt + M["lejos"]); zona = act & (bajo <= Lt + M["tol"])
                    else: lej = act & (alto <= Lt - M["lejos"]); zona = act & (alto >= Lt - M["tol"])
                # LA RAYA YA ESTABA DIBUJADA CUANDO EL PRECIO ESTABA LEJOS: 'lejos' solo cuenta en segundos en que esa raya estaba vigente. Es lo que el operador
                # ve (el precio VIAJA hacia una raya que ya existe). Una dominante que aparece al lado del precio cuando este ya llego no es un toque.
                entra = zona & ~np.concatenate(([False], zona[:-1]))
                ul = np.maximum.accumulate(np.where(lej, ar, -1)); ue = np.concatenate(([-1], np.maximum.accumulate(np.where(entra, ar, -1))[:-1]))
                ok = entra & rueda & (ul >= 0) & (ar - ul <= 1800) & (ul > ue)
                for i in np.flatnonzero(ok):
                    L = Lt[i]; j = min(n, i + 120)
                    lleno = (bajo[i:j] <= L).any() if lado > 0 else (alto[i:j] >= L).any()
                    if not lleno: continue
                    fila = dict(i=int(i), t=seg.index[i], L=float(L), K=float(k), lado=lado, fuerza=float(Fv[i]), neto=float(L2["neto"].iloc[i]), parte=float(Fv[i] / L2["total_cerca"].iloc[i]) if L2["total_cerca"].iloc[i] else np.nan,
                                vel60=float(abs(ult[i] - ult[max(0, i - 60)])), vel300=float(abs(ult[i] - ult[max(0, i - 300)])), cinta60=float(nord[max(0, i - 60):i].sum()))
                    for G, S in M["reglas"]:
                        y, _ = barrera_asim(seg, [i], [lado], G, S, 900, entrada=[L]); fila["r%g_%g" % (G, S)] = int(y[0])
                    out.append(fila)
    return pd.DataFrame(out)


def correr(raiz, T, refrescar=False):
    """(reales, placebo) de todos los dias con cinta y grabacion del libro."""
    F, _ = fotos(raiz, refrescar); M = MERCADO[raiz]; R = []; P = []
    for s, seg in sorted(T.items()):
        L2 = rayas_por_segundo(seg, F)
        if L2 is None or L2["d0"].notna().sum() < 600: continue
        r = toques(seg, L2, raiz); r["dia"] = s; R.append(r)
        for dl in M["placebo"]:
            p = toques(seg, L2, raiz, delta=dl); p["dia"] = s; p["delta"] = dl; P.append(p)
        print("%s %s: cobertura de rueda %.0f %% | ordenes ejecutadas en las verdes %d | en el placebo %d" % (raiz, s, 100 * L2["d0"].notna()[seg["rueda"]].mean(), len(r), sum(len(x) for x in P[-len(M["placebo"]):])), flush=True)
    return pd.concat(R, ignore_index=True), pd.concat(P, ignore_index=True)


def tabla(d, col, cortes, etiquetas, regla):
    y = d["r%g_%g" % regla]; dd = d[y != 0].assign(gana=(y[y != 0] > 0).astype(int)); g = pd.cut(dd[col], cortes, labels=etiquetas)
    r = dd.groupby(g, observed=True)["gana"].agg(["mean", "size"])
    return " | ".join("%s %.0f %% (n %d)" % (k, 100 * v["mean"], v["size"]) for k, v in r.iterrows())
