# -*- coding: utf-8 -*-
"""
absorcion_base.py — herramientas comunes de la familia ABSORCION REAL EN LA PUNTA. No toca base.py ni su cache: todo lo propio va a
laboratorio/gatillo/absorcion_cache/.

  tablas()            las tablas de 1 s de base.py, pero REHECHAS desde la cinta para las sesiones donde la de base.py no cubre la cinta
                      (base.segundos_ricos descarta los px_grupo lejos del modal: en dias de rango > 450 pts corta las colas del dia y deja
                      el precio congelado; pasa en 09-03, 09-14 y 09-16). Misma cuenta, sin ese filtro (la cinta de cada dia es de UN contrato).
  cinta_sesion(s)     la cinta cruda de UNA sesion (filtro de parquet, no carga las demas).
  valido(seg)         segundos de rueda sin los 2 primeros minutos y sin huecos de datos (> 30 s sin ordenes) entre t-600 y t+900.
  desagrupar(...)     minimo 60 s entre disparos de la misma variante, gana el primero.
  evaluar(...)        barreras +-8/600 (principal), +-5/300, +-12/900, por lado, dias arriba, neto por operacion, movimiento medio firmado.
  placebo(...)        misma cantidad de disparos al azar en la misma sesion y la misma media hora, con los mismos lados, separados 60 s.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd, base as B

AQUI = os.path.dirname(os.path.abspath(__file__))
MI_CACHE = os.path.join(AQUI, "absorcion_cache")
PQ_CINTA = os.path.join(B.CACHE, "cinta.parquet")
BARRERAS = ((8, 600), (5, 300), (12, 900))   # la primera es la principal
SEP = 60                                      # segundos minimos entre disparos de la misma variante


def cinta_sesion(s, columnas=None):
    d = pd.read_parquet(PQ_CINTA, filters=[("sesion", "==", s)], columns=columnas)
    return d.sort_values("t", kind="stable").reset_index(drop=True)


def _segundos_ricos_sin_filtro(d):
    """Copia literal de la cuenta de base.segundos_ricos, sin el filtro de px_grupo (d = cinta de una sesion, ordenada)."""
    vol = d["vol"].to_numpy("float64"); lado = d["lado"].to_numpy(); sv = vol * lado
    ab, abv, aa, aav = (d[c].to_numpy("float64") for c in ("abid", "abidv", "aask", "aaskv")); db, dbv, da, dav = (d[c].to_numpy("float64") for c in ("dbid", "dbidv", "dask", "daskv"))
    ult = d["ultimo"].to_numpy("float64"); pri = d["primero"].to_numpy("float64")
    venta = lado == -1; compra = lado == 1
    agota_b = venta & (vol >= abv); agota_a = compra & (vol >= aav)
    x = pd.DataFrame({"vol": vol, "delta": sv, "n": 1.0,
                      "d1": np.where(vol <= 1, sv, 0), "d2_4": np.where((vol >= 2) & (vol <= 4), sv, 0), "d5_9": np.where((vol >= 5) & (vol <= 9), sv, 0),
                      "d10_49": np.where((vol >= 10) & (vol <= 49), sv, 0), "d50": np.where(vol >= 50, sv, 0),
                      "barre_c": np.where(compra & (ult != pri), vol, 0), "barre_v": np.where(venta & (ult != pri), vol, 0),
                      "abs_bid": np.where(agota_b & (db >= ab), vol, 0), "abs_ask": np.where(agota_a & (da <= aa), vol, 0),
                      "rompe_bid": np.where(agota_b & (db < ab), vol, 0), "rompe_ask": np.where(agota_a & (da > aa), vol, 0)})
    pdb, pdbv, pda, pdav = np.roll(db, 1), np.roll(dbv, 1), np.roll(da, 1), np.roll(dav, 1)
    pas = B._ofi(pdb, pdbv, pda, pdav, ab, abv, aa, aav); pas[0] = 0
    dt = d["t"].to_numpy(); hueco = np.empty(len(d), bool); hueco[0] = True; hueco[1:] = (dt[1:] - dt[:-1]) > np.timedelta64(60, "s"); pas[hueco] = 0
    x["ofi_pas"] = pas; x["ofi"] = pas + B._ofi(ab, abv, aa, aav, db, dbv, da, dav)
    s = d["t"].dt.floor("s").to_numpy(); x.index = s
    out = x.groupby(level=0).sum()
    px = pd.DataFrame({"ultimo": ult, "alto": np.maximum(ult, pri), "bajo": np.minimum(ult, pri), "bid": db, "bidv": dbv, "ask": da, "askv": dav}, index=s)
    px = px.groupby(level=0).agg({"ultimo": "last", "alto": "max", "bajo": "min", "bid": "last", "bidv": "last", "ask": "last", "askv": "last"})
    out = out.join(px); idx = pd.date_range(out.index[0], out.index[-1], freq="s"); out = out.reindex(idx)
    out["ultimo"] = out["ultimo"].ffill(); out["alto"] = out["alto"].fillna(out["ultimo"]); out["bajo"] = out["bajo"].fillna(out["ultimo"])
    for c in ("bid", "bidv", "ask", "askv"): out[c] = out[c].ffill()
    out = out.fillna(0.0)[B.COLS_RICAS]
    for c in B.COLS_RICAS:
        if c not in ("ultimo", "alto", "bajo", "bid", "ask"): out[c] = out[c].astype("float32")
    hm = out.index.strftime("%H:%M"); out["rueda"] = (hm >= B.RUEDA[0]) & (hm < B.RUEDA[1])
    return out


# sesiones donde la tabla de base.py NO cubre la cinta de la rueda (medido en absorcion_02_integridad.py)
REHACER = ("2026-08-24", "2026-09-03", "2026-09-14", "2026-09-16")


def tablas():
    os.makedirs(MI_CACHE, exist_ok=True); T = B.todas_las_sesiones()
    for s in REHACER:
        if s not in T: continue
        pq = os.path.join(MI_CACHE, "seg-%s.parquet" % s)
        if not os.path.exists(pq):
            d = cinta_sesion(s); dp = d["ultimo"].diff().abs().max()
            assert dp < 80, "la cinta de %s parece traer dos contratos (salto de %.1f pts)" % (s, dp)
            _segundos_ricos_sin_filtro(d).to_parquet(pq)
        T[s] = pd.read_parquet(pq)
    return T


def valido(seg, atras=600, adelante=900, hueco=30):
    """True en los segundos de rueda donde se puede disparar: sin los 2 primeros minutos y sin huecos de datos alrededor."""
    n = (seg["n"].to_numpy() > 0); N = len(n)
    # segundos desde la ultima orden / hasta la proxima
    idx = np.arange(N); ult = np.where(n, idx, -1); ult = np.maximum.accumulate(ult); desde = idx - ult
    prox = np.where(n, idx, N * 2); prox = np.minimum.accumulate(prox[::-1])[::-1]; hasta = prox - idx
    en_hueco = ((desde + hasta) > hueco) & ~n
    c = np.concatenate([[0], np.cumsum(en_hueco)])
    a = np.clip(idx - atras, 0, N); b = np.clip(idx + adelante + 1, 0, N)
    limpio = (c[b] - c[a]) == 0
    hm = seg.index.strftime("%H:%M")
    return seg["rueda"].to_numpy() & (hm >= "13:32") & limpio & (idx + adelante < N)


def desagrupar(pos, lado, sep=SEP):
    """pos = indices (segundos) ordenados de los disparos de UNA sesion; deja el primero y descarta los que caen a menos de sep s del ultimo aceptado."""
    keep = []; last = -10 ** 9
    for k, p in enumerate(pos):
        if p - last >= sep: keep.append(k); last = p
    keep = np.asarray(keep, int)
    return np.asarray(pos)[keep], np.asarray(lado)[keep]


_Y = {}


def barreras_de(s, seg):
    """Cache en memoria de las tres barreras por sesion."""
    if s not in _Y: _Y[s] = {(x, h): B.barrera(seg, x, h)[0] for x, h in BARRERAS}
    return _Y[s]


def disparos(T, regla, ses=None):
    """regla(s, seg) -> (pos, lado) sin desagrupar ni filtrar. Devuelve DataFrame de disparos validos y desagrupados con los resultados."""
    filas = []
    for s, seg in T.items():
        if ses is not None and s not in ses: continue
        pos, lado = regla(s, seg); pos = np.asarray(pos, int); lado = np.asarray(lado, int)
        o = np.argsort(pos, kind="stable"); pos, lado = pos[o], lado[o]
        ok = valido_de(s, seg)[pos]; pos, lado = pos[ok], lado[ok]
        pos, lado = desagrupar(pos, lado)
        if len(pos) == 0: continue
        Y = barreras_de(s, seg); p = seg["ultimo"].to_numpy(); N = len(p)
        f = pd.DataFrame({"sesion": s, "pos": pos, "t": seg.index[pos], "lado": lado, "px": p[pos]})
        for (x, h), y in Y.items():
            f["y%d" % x] = y[pos]
            fin = np.clip(pos + h, 0, N - 1); f["fin%d" % x] = (p[fin] - p[pos]) * lado
        for k in (10, 30, 60, 300): f["m%d" % k] = (p[np.clip(pos + k, 0, N - 1)] - p[pos]) * lado
        filas.append(f)
    return pd.concat(filas, ignore_index=True) if filas else pd.DataFrame(columns=["sesion", "pos", "t", "lado", "px"])


_V = {}


def valido_de(s, seg):
    if s not in _V: _V[s] = valido(seg)
    return _V[s]


def neto(f, x):
    """Puntos netos por operacion con barrera +-x: +x / -x si resolvio, y lo que marque el reloj si no; menos el costo."""
    y = f["y%d" % x].to_numpy() * f["lado"].to_numpy(); fin = f["fin%d" % x].to_numpy()
    return np.where(y > 0, x, np.where(y < 0, -x, fin)) - B.COSTO_PTS


def resumen(f, nombre):
    """Una fila por variante: las tres barreras, dias, lados, neto y movimiento medio firmado."""
    r = dict(nombre=nombre, disparos=len(f))
    if len(f) == 0: return r
    for x, h in BARRERAS:
        j = B.juzgar(f["y%d" % x], f["lado"], nombre, f["sesion"], x)
        if j.get("n", 0) == 0: continue
        r["n%d" % x] = j["n"]; r["ac%d" % x] = j["acierto"]; r["z%d" % x] = j["z"]; r["dias%d" % x] = j["dias_arriba"]; r["neto%d" % x] = round(float(neto(f, x).mean()), 2)
    for l, nm in ((1, "L"), (-1, "C")):
        g = f[f["lado"] == l]; j = B.juzgar(g["y8"], g["lado"]) if len(g) else {}
        r["n8" + nm] = j.get("n", 0); r["ac8" + nm] = j.get("acierto", np.nan)
    for k in (10, 30, 60, 300): r["m%d" % k] = round(float(f["m%d" % k].mean()), 2)
    return r


def z_por_dia(f, x=8):
    """z robusto: la unidad es el DIA (acierto del dia - 50 %), t de Student sobre los dias. No infla por disparos solapados."""
    ok = f["y%d" % x] != 0; g = ((f.loc[ok, "y%d" % x] * f.loc[ok, "lado"]) > 0).groupby(f.loc[ok, "sesion"]).mean() - 0.5
    if len(g) < 3 or g.std(ddof=1) == 0: return np.nan
    return float(g.mean() / (g.std(ddof=1) / np.sqrt(len(g))))


def placebo(T, f, x=8, sorteos=200, semilla=7):
    """Mismos disparos por sesion y media hora, en segundos validos al azar (separados 60 s), con los mismos lados. Devuelve media, desvio
    del acierto placebo y z del acierto real contra el placebo; idem para el neto por operacion."""
    rng = np.random.default_rng(semilla); acs = []; nets = []
    grupos = []
    for s, g in f.groupby("sesion"):
        seg = T[s]; v = valido_de(s, seg); media = (np.arange(len(seg)) + int(seg.index[0].timestamp())) // 1800
        y = barreras_de(s, seg)[(x, dict(BARRERAS)[x])]; p = seg["ultimo"].to_numpy(); h = dict(BARRERAS)[x]; N = len(p)
        for mh, gg in g.groupby(media[g["pos"].to_numpy()]):
            cand = np.flatnonzero(v & (media == mh)); grupos.append((cand, gg["lado"].to_numpy(), y, p, h, N))
    for _ in range(sorteos):
        ys = []; ls = []; ns = []
        for cand, lados, y, p, h, N in grupos:
            k = len(lados); perm = rng.permutation(cand); acc = []
            for c in perm:
                if all(abs(c - a) >= SEP for a in acc):
                    acc.append(c)
                    if len(acc) == k: break
            acc = np.asarray(acc, int); l = rng.permutation(lados)[:len(acc)]
            yy = y[acc] * l; ys.append(yy); fin = (p[np.clip(acc + h, 0, N - 1)] - p[acc]) * l
            ns.append(np.where(yy > 0, x, np.where(yy < 0, -x, fin)) - B.COSTO_PTS)
        yy = np.concatenate(ys); ok = yy != 0; acs.append((yy[ok] > 0).mean()); nets.append(np.concatenate(ns).mean())
    ok = f["y%d" % x] != 0; real = float(((f.loc[ok, "y%d" % x] * f.loc[ok, "lado"]) > 0).mean()); real_n = float(neto(f, x).mean())
    acs = np.asarray(acs); nets = np.asarray(nets)
    return dict(acierto=round(100 * real, 1), placebo_media=round(100 * acs.mean(), 1), placebo_desvio=round(100 * acs.std(ddof=1), 2), z_placebo=round((real - acs.mean()) / acs.std(ddof=1), 2),
                neto=round(real_n, 2), neto_placebo=round(float(nets.mean()), 2), z_neto=round((real_n - nets.mean()) / nets.std(ddof=1), 2))


# ------------------------------------------------------------------ rasgos de la familia (todo causal: ventanas que terminan en t)
_R = {}


def eventos(s):
    """Absorciones LIMPIAS de la cinta: la orden consumio toda la punta visible, la punta quedo en el mismo precio Y con tamano >= 1
    (el 16 % de las abs de base.py tiene 'despues == 0': la foto salio antes de que el libro se actualizara; no se sabe si repusieron)."""
    ev = pd.read_parquet(os.path.join(MI_CACHE, "eventos-%s.parquet" % s))
    return ev[ev["despues"] >= 1]


def rasgos(s, seg):
    if s in _R: return _R[s]
    ev = eventos(s); sec = ev["t"].dt.floor("s"); b = ev["punta"] == 1; a = ~b
    f = pd.DataFrame(index=seg.index)
    f["ab"] = ev.loc[b, "vol"].groupby(sec[b]).sum().reindex(seg.index).fillna(0.0); f["aa"] = ev.loc[a, "vol"].groupby(sec[a]).sum().reindex(seg.index).fillna(0.0)
    f["ab_px"] = ev.loc[b, "px"].groupby(sec[b]).min().reindex(seg.index); f["aa_px"] = ev.loc[a, "px"].groupby(sec[a]).max().reindex(seg.index)
    for w in (10, 30, 60):
        f["AB%d" % w] = f["ab"].rolling(w, min_periods=1).sum(); f["AA%d" % w] = f["aa"].rolling(w, min_periods=1).sum()
    f["RB60"] = seg["rompe_bid"].rolling(60, min_periods=1).sum(); f["RA60"] = seg["rompe_ask"].rolling(60, min_periods=1).sum()
    f["OP30"] = seg["ofi_pas"].rolling(30, min_periods=1).sum(); f["D60"] = seg["delta"].rolling(60, min_periods=1).sum()
    f["dP60"] = seg["ultimo"] - seg["ultimo"].shift(60)
    f["min600"] = seg["bajo"].rolling(600, min_periods=300).min(); f["max600"] = seg["alto"].rolling(600, min_periods=300).max()
    f["pos600"] = (seg["ultimo"] - f["min600"]) / (f["max600"] - f["min600"]).replace(0, np.nan)
    f["L60"] = f["ab_px"].rolling(60, min_periods=1).min(); f["H60"] = f["aa_px"].rolling(60, min_periods=1).max()
    # eventos sueltos grandes / con reposicion fuerte, por segundo (1 si hubo alguno en ese segundo)
    g = ev["vol"] >= 10; f["gb"] = (g & b).groupby(sec).any().reindex(seg.index, fill_value=False).astype(bool); f["ga"] = (g & a).groupby(sec).any().reindex(seg.index, fill_value=False).astype(bool)
    r = (ev["antes"] >= 3) & (ev["despues"] >= ev["antes"]); f["rb"] = (r & b).groupby(sec).any().reindex(seg.index, fill_value=False).astype(bool); f["ra"] = (r & a).groupby(sec).any().reindex(seg.index, fill_value=False).astype(bool)
    _R[s] = f
    return f
