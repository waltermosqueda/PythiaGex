# -*- coding: utf-8 -*-
"""
rayas_base.py — LAS ZONAS DE REACCION DEL OPERADOR, con la cinta orden por orden (pedido del 17-09 18:00):
"cuando toca las rayas dominantes yo las veo rebotar varias veces por jornada; las velas las traspasan por unos pocos puntos o con mechas
largas y terminan repeliendo, yendose por bastantes puntos para el otro lado; el donde buscar es tan importante como el que buscar".

QUE ES UNA RAYA (igual que GatilloRebote.cs / laboratorio/gatillo_rebote.py, que reprodujo sus 7 ejemplos del 10-09):
  - 'dom'   : cada dominante que hubo en la sesion hasta ese momento (identidad = strike redondeado a 10 pts de NQ; valor = el ultimo exacto).
              La dominante deja su fila de guiones sobre las velas en que rigio; el ojo la prolonga hacia la derecha: por eso se acumulan.
  - 'zero'  : el zero gamma por volumen vigente.          - 'major': los dos majors vigentes (mp, mn).
  Un nivel del minuto m se conoce al CIERRE de ese minuto: vale desde el primer segundo del minuto m+1 (no mira adelante).
QUE ES UN TOQUE: el precio venia de LEJOS (todo un segundo a >= LEJOS pts de la raya, hace menos de VENTANA s) y entra a la zona (a <= TOL pts).
  lado = +1 si llega desde ARRIBA (la raya haria de soporte: el rebote es hacia arriba); -1 si llega desde abajo.
PLACEBO: las mismas rayas corridas DELTA puntos (fuera de la grilla de strikes): si el rebote es igual, la raya no aporta; si es mayor, si.
"""
import os, sys
import numpy as np, pandas as pd
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import base as B

PASO = 10.0
TOL, LEJOS, VENTANA = 1.0, 15.0, 1800
PLACEBOS = (23.0, -23.0, 57.0, -57.0)
CACHE = os.path.join(B.CACHE, "rayas"); os.makedirs(CACHE, exist_ok=True)


def filas_por_segundo(seg, N):
    """{(tipo, K): vector por segundo con el valor de la raya (NaN antes de nacer)} + vector 'vigente' por raya dom (1 si es dominante AHORA)."""
    idx = seg.index; n = len(idx); t0 = idx[0]
    nv = N[(N.index >= idx[0] - pd.Timedelta(minutes=1)) & (N.index <= idx[-1])]
    filas = {}; vig = {}
    def poner(clave, seg_desde, valor):
        a = filas.get(clave)
        if a is None: a = np.full(n, np.nan); filas[clave] = a
        a[seg_desde:] = valor
    actuales_prev = set()
    for m, r in nv.iterrows():
        desde = int((m + pd.Timedelta(minutes=1) - t0).total_seconds())     # se conoce al cierre del minuto m
        if desde >= n: break
        desde = max(0, desde); c = r["c"]; act = set()
        for k in ("d_dom0", "d_dom1"):
            if pd.notna(r[k]):
                L = c - r[k]; K = round(L / PASO) * PASO; poner(("dom", K), desde, L); act.add(K)
        for K in act | actuales_prev:
            v = vig.get(K)
            if v is None: v = np.zeros(n, "int8"); vig[K] = v
            v[desde:] = 1 if K in act else 0
        actuales_prev = act
        if pd.notna(r["d_zero"]): poner(("zero", 0.0), desde, c - r["d_zero"])
        if pd.notna(r["d_mp"]): poner(("major", 1.0), desde, c - r["d_mp"])
        if pd.notna(r["d_mn"]): poner(("major", -1.0), desde, c - r["d_mn"])
    return filas, vig


def _entradas(alto, bajo, Lt, lado, tol, lejos, ventana):
    """Segundos en que el precio ENTRA a la zona de la raya viniendo de lejos (primer ingreso despues de cada 'lejos')."""
    n = len(Lt); ar = np.arange(n); act = ~np.isnan(Lt)
    with np.errstate(invalid="ignore"):
        if lado > 0: lejos_m = act & (bajo >= Lt + lejos); zona = act & (bajo <= Lt + tol)
        else: lejos_m = act & (alto <= Lt - lejos); zona = act & (alto >= Lt - tol)
    entra = zona & ~np.concatenate(([False], zona[:-1]))
    ult_lejos = np.maximum.accumulate(np.where(lejos_m, ar, -1))
    ult_entra_antes = np.concatenate(([-1], np.maximum.accumulate(np.where(entra, ar, -1))[:-1]))
    ok = entra & (ult_lejos >= 0) & (ar - ult_lejos <= ventana) & (ult_lejos > ult_entra_antes)
    return np.flatnonzero(ok), ult_lejos


def toques(sesion, seg, N, delta=0.0, tol=TOL, lejos=LEJOS, ventana=VENTANA):
    """Todos los toques de una sesion. delta != 0 = rayas placebo (corridas delta puntos). Devuelve DataFrame, una fila por toque."""
    filas, vig = filas_por_segundo(seg, N)
    alto = seg["alto"].to_numpy(); bajo = seg["bajo"].to_numpy(); out = []
    hueco = seg["hueco"].to_numpy() if "hueco" in seg else np.zeros(len(seg), bool)
    # confluencia: cuantas OTRAS rayas hay a <= 5 pts en ese segundo
    claves = list(filas); M = np.vstack([filas[k] for k in claves]) + delta if claves else np.zeros((0, len(seg)))
    for j, (tipo, K) in enumerate(claves):
        Lt = M[j]; nac = int(np.flatnonzero(~np.isnan(Lt))[0]) if np.any(~np.isnan(Lt)) else -1
        if nac < 0: continue
        for lado in (1, -1):
            idx, ult_lejos = _entradas(alto, bajo, Lt, lado, tol, lejos, ventana)
            for k_num, i in enumerate(idx):
                if hueco[max(0, i - 600):i + 900].any(): continue
                with np.errstate(invalid="ignore"): conf = int(np.nansum(np.abs(M[:, i] - Lt[i]) <= 5.0)) - 1
                out.append(dict(sesion=sesion, i=int(i), t=seg.index[i], tipo=tipo, K=K, L=float(Lt[i]), lado=lado, n_toque=k_num + 1,
                                edad_min=(i - nac) / 60.0, vigente=int(vig[K][i]) if tipo == "dom" and K in vig else 1,
                                seg_desde_lejos=int(i - ult_lejos[i]), confluencia=conf, rueda=bool(seg["rueda"].iloc[i])))
    d = pd.DataFrame(out)
    if len(d): d = d.sort_values("i").reset_index(drop=True)
    return d


def barrera_asim(seg, idx, lado, G, S, H, entrada=None):
    """Desde el segundo i (referencia = entrada o ultimo[i]): +1 si el precio recorre G a FAVOR del lado antes que S en contra, dentro de H s; -1 al reves; 0 ninguna
    (o las dos en el mismo segundo). Devuelve (y, segundos). Mira de i+1 en adelante."""
    p = seg["ultimo"].to_numpy(); hi = seg["alto"].to_numpy(); lo = seg["bajo"].to_numpy(); n = len(p)
    idx = np.asarray(idx, int); lado = np.asarray(lado, float); y = np.zeros(len(idx), int); tt = np.full(len(idx), np.nan)
    ref = p[idx] if entrada is None else np.asarray(entrada, float)
    for a, (i, l, r) in enumerate(zip(idx, lado, ref)):
        j1 = min(n, i + 1 + H)
        if i + 1 >= j1: continue
        h = hi[i + 1:j1]; b = lo[i + 1:j1]
        if l > 0: gan = np.flatnonzero(h >= r + G); per = np.flatnonzero(b <= r - S)
        else: gan = np.flatnonzero(b <= r - G); per = np.flatnonzero(h >= r + S)
        tg = gan[0] if len(gan) else 10**9; tp = per[0] if len(per) else 10**9
        if tg < tp: y[a] = 1; tt[a] = tg + 1
        elif tp < tg: y[a] = -1; tt[a] = tp + 1
    return y, tt


def todos_los_toques(refrescar=False):
    """(reales, placebo) de las 20 sesiones, cacheados."""
    pr = os.path.join(CACHE, "toques_reales.parquet"); pp = os.path.join(CACHE, "toques_placebo.parquet")
    if not refrescar and os.path.exists(pr) and os.path.exists(pp): return pd.read_parquet(pr), pd.read_parquet(pp)
    T = B.todas_las_sesiones(); N = B.niveles_m1(); R = []; P = []
    for s, seg in sorted(T.items()):
        r = toques(s, seg, N); R.append(r)
        for dl in PLACEBOS:
            q = toques(s, seg, N, delta=dl); q["delta"] = dl; P.append(q)
        print(s, "toques reales", len(r), "| placebo", sum(len(x) for x in P[-len(PLACEBOS):]), flush=True)
    R = pd.concat(R, ignore_index=True); P = pd.concat(P, ignore_index=True)
    R.to_parquet(pr); P.to_parquet(pp)
    return R, P


if __name__ == "__main__":
    R, P = todos_los_toques(refrescar="--refrescar" in sys.argv)
    print("toques reales: %d (rueda %d) | placebo: %d (rueda %d, %d corrimientos)" % (len(R), R["rueda"].sum(), len(P), P["rueda"].sum(), len(PLACEBOS)))
    print(R[R["rueda"]].groupby("tipo").size().to_string()); print("por sesion (rueda):", R[R["rueda"]].groupby("sesion").size().to_dict())
