# -*- coding: utf-8 -*-
"""punta_lib.py — familia PUNTA: desbalance de la punta del libro y flujo de ordenes PASIVO (lo que el CVD no ve).
Rasgos causales (en el segundo t solo se usan filas <= t), variantes pre-registradas, desagrupado de 60 s, barreras cacheadas y placebo.
No toca base.py; solo lo usa."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd, base as B

AQUI = os.path.dirname(os.path.abspath(__file__))
CACHE = os.path.join(AQUI, "punta_cache"); os.makedirs(CACHE, exist_ok=True)
BARRERAS = [(8, 600), (5, 300), (12, 900)]     # principal primero
MIN_ENTRE = 60                                   # segundos minimos entre disparos de una misma variante
INICIO = "13:32:00"                              # rueda sin los dos primeros minutos
# umbrales CONGELADOS en el pre-registro (17-09), fijados mirando solo la forma de los rasgos y la cantidad de disparos en explorar, nunca un resultado
U = dict(qi_inst=0.8, qi_30=0.17, qi_10=0.17, mov_60=15.75, div60_p=1.25, div300=1.0, ofi60=1.25)
COLA = 900                                       # no se dispara en los ultimos 900 s de la tabla (la barrera mas larga no entraria)


# ------------------------------------------------------------------ rasgos (todos causales)
def rasgos(d):
    """d = tabla de 1 s de UNA sesion (entera, con la noche, para que las ventanas ya tengan historia a las 13:32)."""
    f = pd.DataFrame(index=d.index)
    bv = d["bidv"].astype("float64"); av = d["askv"].astype("float64"); den = (bv + av)
    f["qi"] = ((bv - av) / den.where(den > 0)).fillna(0.0)
    f["qi10"] = f["qi"].rolling(10).mean(); f["qi30"] = f["qi"].rolling(30).mean()
    mid = (d["bid"] + d["ask"]) / 2.0; f["mid"] = mid; f["spread"] = d["ask"] - d["bid"]; f["prof"] = den
    micro = (d["ask"] * bv + d["bid"] * av) / den.where(den > 0)
    f["mp_mid"] = ((micro - mid) / B.TICK).fillna(0.0); f["mp_ult"] = ((micro - d["ultimo"]) / B.TICK).fillna(0.0)
    for c, k in (("ofi_pas", "P"), ("ofi", "O"), ("delta", "D")):
        x = d[c].astype("float64"); sd = x.rolling(1800, min_periods=600).std()
        for W in (10, 30, 60, 300):
            f["z%s%d" % (k, W)] = x.rolling(W).sum() / (sd * np.sqrt(W))
    for W in (30, 60, 300): f["ret%d" % W] = d["ultimo"] - d["ultimo"].shift(W)
    return f


def elegible(d):
    """Segundos donde se permite disparar: rueda, sin los 2 primeros minutos, y con cola para que entre la barrera mas larga."""
    hms = d.index.strftime("%H:%M:%S"); ok = d["rueda"].to_numpy() & (hms >= INICIO)
    ok[max(0, len(d) - COLA):] = False
    return ok


# ------------------------------------------------------------------ variantes pre-registradas: devuelven lado (+1/-1/0) por segundo
def variantes(f, U):
    """U = umbrales congelados (dict). Cada variante: vector de lado, 0 = no dispara."""
    s = np.sign; z = lambda a: np.nan_to_num(a.to_numpy(), nan=0.0)
    qi, qi10, qi30 = z(f["qi"]), z(f["qi10"]), z(f["qi30"])
    P60, P300, D60, D300, O60 = z(f["zP60"]), z(f["zP300"]), z(f["zD60"]), z(f["zD300"]), z(f["zO60"]); r60 = z(f["ret60"])
    V = {}
    V["P01_qi_inst"] = np.where(np.abs(qi) >= U["qi_inst"], s(qi), 0)
    V["P02_qi_30"] = np.where(np.abs(qi30) >= U["qi_30"], s(qi30), 0)
    V["P03_pas_60"] = np.where(np.abs(P60) >= 2.0, s(P60), 0)
    V["P04_pas_300"] = np.where(np.abs(P300) >= 2.0, s(P300), 0)
    V["P05_div_60"] = np.where((np.abs(D60) >= 1.5) & (np.abs(P60) >= U["div60_p"]) & (s(D60) != s(P60)), s(P60), 0)        # gana el pasivo
    V["P06_div_300"] = np.where((np.abs(D300) >= U["div300"]) & (np.abs(P300) >= U["div300"]) & (s(D300) != s(P300)), s(P300), 0)
    V["P07_conf_60"] = np.where((np.abs(D60) >= 1.5) & (np.abs(P60) >= 1.5) & (s(D60) == s(P60)), s(D60), 0)        # pasivo y agresivo empujan juntos
    V["P08_delta_con_pas300"] = np.where((np.abs(D60) >= 2.0) & (P300 * s(D60) >= 1.0), s(D60), 0)                  # delta extremo A FAVOR del pasivo lento -> sigue
    V["P09_delta_contra_pas300"] = np.where((np.abs(D60) >= 2.0) & (P300 * s(D60) <= -1.0), -s(D60), 0)             # delta extremo CONTRA el pasivo lento -> gana el pasivo
    V["P10_engrosa_contra_mov"] = np.where((np.abs(r60) >= U["mov_60"]) & (qi10 * s(r60) <= -U["qi_10"]), -s(r60), 0)  # tras el movimiento la punta se carga en contra -> rebote
    V["P11_engrosa_a_favor_mov"] = np.where((np.abs(r60) >= U["mov_60"]) & (qi10 * s(r60) >= U["qi_10"]), s(r60), 0)   # la punta se carga a favor -> sigue
    V["P12_ofi_sin_respuesta"] = np.where((np.abs(O60) >= U["ofi60"]) & (r60 * s(O60) <= 0), s(O60), 0)                    # el OFI empuja y el precio no respondio -> se pone al dia
    return V


def desagrupar(lado, ok, minimo=MIN_ENTRE):
    """Indices de los disparos aceptados: en orden de tiempo, el primero gana y tapa los 'minimo' segundos siguientes."""
    idx = np.flatnonzero((lado != 0) & ok); out = []; ult = -10 ** 9
    for i in idx:
        if i - ult >= minimo: out.append(i); ult = i
    return np.asarray(out, dtype=int)


# ------------------------------------------------------------------ barreras cacheadas
def barrera_cache(sesion, d, x, h):
    fn = os.path.join(CACHE, "y_%s_%d_%d_%d.npz" % (sesion, x, h, len(d)))
    if os.path.exists(fn):
        z = np.load(fn); return z["y"].astype(int), z["pnl_to"]
    y, _ = B.barrera(d, x, h); p = d["ultimo"].to_numpy(); fut = np.full(len(p), np.nan); fut[:len(p) - h] = p[h:]
    pnl_to = (fut - p).astype("float32")      # movimiento a t+h, para valuar los disparos que no tocan ninguna barrera
    np.savez_compressed(fn, y=y.astype("int8"), pnl_to=pnl_to)
    return y, pnl_to


def puntos_netos(y, lado, pnl_to, x):
    """Puntos por operacion, netos del costo de ida y vuelta: gana +x, pierde -x, sin resolver sale al precio de t+h."""
    bruto = np.where(y != 0, x * y * lado, np.nan_to_num(pnl_to, nan=0.0) * lado)
    return float(np.mean(bruto) - B.COSTO_PTS) if len(bruto) else float("nan")


# ------------------------------------------------------------------ placebo: mismas sesiones, misma media hora, mismos lados, mismo espaciado minimo
def placebo(disparos, elegibles, t0s, ys, sorteos=200, semilla=7):
    """disparos = {sesion: (idx, lado)}; elegibles = {sesion: bool[]}; t0s = {sesion: segundo del dia del primer indice}; ys = {sesion: y}.
    Por cada media hora de reloj de cada sesion sortea la misma cantidad de segundos elegibles (con el mismo espaciado minimo) y reparte
    los mismos lados. Devuelve media y desvio del % de acierto del azar."""
    rng = np.random.default_rng(semilla); plan = []
    for s, (idx, lado) in disparos.items():
        if len(idx) == 0: continue
        ok = elegibles[s]; balde = (np.arange(len(ok)) + t0s[s]) // 1800
        for b in np.unique(balde[idx]):
            plan.append((s, np.flatnonzero(ok & (balde == b)), lado[balde[idx] == b]))
    acs = []
    for _ in range(sorteos):
        aci = 0; n = 0
        for s, cand, lados in plan:
            k = len(lados); L = len(cand); paso = min(MIN_ENTRE, max(1, L // k)); libre = L - (k - 1) * paso
            pos = np.sort(rng.integers(0, max(1, libre), size=k)) + paso * np.arange(k); pos = np.minimum(pos, L - 1)
            yy = ys[s][cand[pos]]; ll = rng.permutation(lados); r = yy != 0
            aci += int(((yy[r] * ll[r]) > 0).sum()); n += int(r.sum())
        acs.append(100.0 * aci / max(1, n))
    return float(np.mean(acs)), float(np.std(acs, ddof=1))


def t0_de(d):
    """Segundos desde medianoche UTC del primer indice (para alinear los baldes de media hora al reloj)."""
    i = d.index[0]; return int(i.hour * 3600 + i.minute * 60 + i.second)
