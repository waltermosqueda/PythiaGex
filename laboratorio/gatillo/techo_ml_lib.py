# -*- coding: utf-8 -*-
"""techo_ml_lib.py — familia TECHO: tablero de rasgos causales en grilla de 15 s + modelos chicos (xgboost / logistica) para medir si en
TODOS los datos juntos hay direccion a 10 minutos (barrera del scalper) y, aparte, si se puede anticipar expansion / compresion.
No toca base.py ni archivos de otras familias; solo usa base.py. Pre-registro en resultados/techo_ml.md."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd, base as B

AQUI = os.path.dirname(os.path.abspath(__file__))
CACHE = os.path.join(AQUI, "techo_ml_cache"); os.makedirs(CACHE, exist_ok=True)
BARRERAS = [(8, 600), (5, 300), (12, 900)]      # principal primero
MIN_ENTRE = 60                                    # segundos minimos entre disparos de una misma variante
INICIO = "13:32:00"                               # rueda sin los dos primeros minutos
COLA = 900                                        # no se dispara en los ultimos 900 s de la tabla
PASO = 15                                         # grilla: filas :14, :29, :44, :59
CADUCA_NIVEL = 300                                # un registro de niveles vale como mucho 300 s

NUCLEO = ["dr_60", "dr_300", "dr_900", "zD_300", "zO_300", "zP_300", "ret_60", "ret_300", "ret_900", "retz_1800", "rng_300", "rng_900", "act_60",
          "pos_1800", "pos_3600", "vwap_r_z", "mins", "qi_60", "g_zero", "g_dom0", "g_dom1", "er_900"]
CONTROL_RANGO = ["l_rng_60", "l_rng_300", "l_rng_900", "l_n_60", "mins", "mins2"]


# ------------------------------------------------------------------ utilidades causales
def _suma(x, W):
    """Suma movil de W segundos, inclusive del segundo actual (por diferencia de acumuladas: rapido)."""
    c = x.cumsum(); return c - c.shift(W).fillna(0.0)


def _razon(a, b):
    return (a / b.where(b > 0)).fillna(0.0)


# ------------------------------------------------------------------ niveles de gamma llevados a la cinta, con el minuto YA cerrado
def niveles_en_cinta(d, N):
    """El registro del minuto m (c = cierre de esa vela = ultimo[m+59 s], verificado) recien se usa desde m+60 s. Nivel en precio de cinta = ultimo[m+59] - d."""
    idx = d.index; Nm = N.loc[(N.index >= idx[0].floor("min")) & (N.index <= idx[-1])]
    c59 = d["ultimo"].reindex(Nm.index + pd.Timedelta(seconds=59)).to_numpy()
    L = pd.DataFrame(index=Nm.index + pd.Timedelta(seconds=60))
    for k in ("d_zero", "d_dom0", "d_dom1", "d_mp", "d_mn", "d_mc15"): L[k] = c59 - Nm[k].to_numpy()
    L["cuadrante"] = Nm["cuadrante"].to_numpy()
    L = L[~L.index.duplicated(keep="last")].sort_index()
    return L.reindex(idx, method="ffill", limit=CADUCA_NIVEL)


# ------------------------------------------------------------------ rasgos (todos causales)
def rasgos(d, N):
    """d = tabla de 1 s de UNA sesion entera (con la noche, asi las ventanas ya tienen historia a las 13:32). Devuelve rasgos por segundo."""
    f = {}
    p = d["ultimo"].astype("float64"); hi = d["alto"].astype("float64"); lo = d["bajo"].astype("float64")
    vol = d["vol"].astype("float64"); delta = d["delta"].astype("float64"); n = d["n"].astype("float64")
    ofi = d["ofi"].astype("float64"); pas = d["ofi_pas"].astype("float64")
    dp = p.diff().fillna(0.0); adp = dp.abs(); sd_dp = dp.rolling(1800, min_periods=600).std()
    vW = {W: _suma(vol, W) for W in (15, 60, 300, 900, 1800)}; nW = {W: _suma(n, W) for W in (15, 60, 300, 1800)}
    # A y B: flujo
    for W in (15, 60, 300, 900): f["dr_%d" % W] = _razon(_suma(delta, W), vW[W])
    for x, k in ((delta, "D"), (ofi, "O"), (pas, "P")):
        sd = x.rolling(1800, min_periods=600).std()
        for W in (15, 60, 300, 900): f["z%s_%d" % (k, W)] = _suma(x, W) / (sd.where(sd > 0) * np.sqrt(W))
    # C: absorcion, rotura, barridos
    absn = (d["abs_bid"] - d["abs_ask"]).astype("float64"); abst = (d["abs_bid"] + d["abs_ask"]).astype("float64")
    romn = (d["rompe_ask"] - d["rompe_bid"]).astype("float64"); barn = (d["barre_c"] - d["barre_v"]).astype("float64"); bart = (d["barre_c"] + d["barre_v"]).astype("float64")
    for W in (60, 300, 900):
        for x, k in ((absn, "absn"), (abst, "abst"), (romn, "romn"), (barn, "barn"), (bart, "bart")): f["%s_%d" % (k, W)] = _razon(_suma(x, W), vW[W])
    # D: delta por tamaño
    dch = (d["d1"] + d["d2_4"]).astype("float64"); dme = d["d5_9"].astype("float64"); dgr = (d["d10_49"] + d["d50"]).astype("float64")
    for W in (60, 300, 900):
        for x, k in ((dch, "dch"), (dme, "dme"), (dgr, "dgr")): f["%s_%d" % (k, W)] = _razon(_suma(x, W), vW[W])
    # E: retornos
    for W in (15, 60, 300, 900, 1800, 3600):
        r = p - p.shift(W); f["ret_%d" % W] = r; f["retz_%d" % W] = r / (sd_dp.where(sd_dp > 0) * np.sqrt(W))
    # F: volatilidad
    rng = {W: hi.rolling(W, min_periods=1).max() - lo.rolling(W, min_periods=1).min() for W in (60, 300, 900, 1800)}
    for W in rng: f["rng_%d" % W] = rng[W]
    for W in (60, 300): f["rv_%d" % W] = dp.rolling(W, min_periods=W // 2).std()
    f["rv_1800"] = sd_dp
    f["comp_60_900"] = _razon(rng[60], rng[900]); f["comp_300_1800"] = _razon(rng[300], rng[1800])
    # G: velocidad
    for W in (15, 60, 300): f["act_%d" % W] = _razon(nW[W] / W, nW[1800] / 1800.0)
    for W in (60, 300): f["actv_%d" % W] = _razon(vW[W] / W, vW[1800] / 1800.0)
    f["l_n_60"] = np.log1p(nW[60]); f["l_vol_300"] = np.log1p(vW[300]); f["tam_300"] = _razon(vW[300], nW[300])
    # H: eficiencia
    for W in (300, 900):
        camino = _suma(adp, W); r = p - p.shift(W); f["ser_%d" % W] = _razon(r, camino); f["er_%d" % W] = f["ser_%d" % W].abs()
    # I: posicion en rangos
    for W in (1800, 3600):
        a = hi.rolling(W, min_periods=1).max(); b = lo.rolling(W, min_periods=1).min(); f["pos_%d" % W] = _razon(p - b, a - b).where((a - b) > 0, 0.5)
    a = hi.cummax(); b = lo.cummin(); f["pos_sesion"] = _razon(p - b, a - b).where((a - b) > 0, 0.5)
    ru = d["rueda"].to_numpy()
    hr = hi.where(ru).cummax(); lr = lo.where(ru).cummin(); f["pos_rueda"] = ((p - lr) / (hr - lr).where((hr - lr) > 0)).where(ru)
    p_ap = p[ru].iloc[0] if ru.any() else np.nan; f["d_apertura"] = (p - p_ap).where(ru)
    # J: VWAP anclado (rueda y sesion), en desvios y en puntos
    p0 = p - p.iloc[0]
    for nombre, mascara in (("r", ru), ("s", np.ones(len(d), bool))):
        v = vol.where(mascara, 0.0); sv = v.cumsum(); m1 = (v * p0).cumsum() / sv.where(sv > 0); m2 = (v * p0 * p0).cumsum() / sv.where(sv > 0)
        sdv = np.sqrt((m2 - m1 * m1).clip(lower=0)); dist = (p0 - m1)
        f["vwap_%s_p" % nombre] = dist.where(mascara); f["vwap_%s_z" % nombre] = (dist / sdv.where(sdv > 0.5)).where(mascara)
    # K: hora
    seg_dia = (d.index.hour * 3600 + d.index.minute * 60 + d.index.second).to_numpy()
    f["mins"] = pd.Series((seg_dia - 13.5 * 3600) / 60.0, index=d.index); f["mins2"] = f["mins"] ** 2 / 100.0
    # L: punta en reposo
    bv = d["rbidv"].astype("float64"); av = d["raskv"].astype("float64"); den = bv + av
    qi = ((bv - av) / den.where(den > 0)).fillna(0.0); f["qi"] = qi; f["qi_15"] = qi.rolling(15, min_periods=1).mean(); f["qi_60"] = qi.rolling(60, min_periods=1).mean()
    f["spread_60"] = ((d["rask"] - d["rbid"]) / B.TICK).rolling(60, min_periods=1).mean()
    f["prof_60"] = _razon(den.rolling(60, min_periods=1).mean(), den.rolling(1800, min_periods=60).mean())
    # M: gamma
    L = niveles_en_cinta(d, N)
    for k, nom in (("d_zero", "g_zero"), ("d_dom0", "g_dom0"), ("d_dom1", "g_dom1"), ("d_mp", "g_mp"), ("d_mn", "g_mn"), ("d_mc15", "g_mc15")): f[nom] = p - L[k]
    f["g_sz"] = np.sign(f["g_zero"]); f["g_azero"] = f["g_zero"].abs(); f["g_cuad"] = L["cuadrante"]
    d0 = f["g_dom0"]; d1 = f["g_dom1"]; f["g_cerca"] = d0.where(d0.abs().fillna(np.inf) <= d1.abs().fillna(np.inf), d1)
    # N: CVD de la rueda y divergencias
    dl = delta.where(ru, 0.0).cumsum(); vl = vol.where(ru, 0.0).cumsum(); f["cvdr"] = _razon(dl, vl).where(ru)
    f["div_300"] = f["zD_300"] - f["retz_300"]; f["div_900"] = f["zD_900"] - f["retz_900"]
    # control del rango
    for W in (60, 300, 900): f["l_rng_%d" % W] = np.log(rng[W].clip(lower=B.TICK))
    out = pd.DataFrame(f, index=d.index).replace([np.inf, -np.inf], np.nan)
    return out.astype("float32")


SOLO_CONTROL = ["l_rng_60", "l_rng_300", "l_rng_900", "mins2"]      # rasgos que existen solo para el control lineal V10 (no entran a "todos")


def columnas(df):
    """(todos los rasgos, los sin gamma). 'todos' excluye las columnas de objetivo / indice y las que son solo del control lineal."""
    no = set(["sesion", "pos", "balde", "R300", "t8"] + ["y%d" % x for x, _ in BARRERAS] + ["pnl%d" % x for x, _ in BARRERAS] + SOLO_CONTROL)
    todos = [c for c in df.columns if c not in no]; sin_gamma = [c for c in todos if not c.startswith("g_")]
    return todos, sin_gamma


# ------------------------------------------------------------------ elegibilidad, objetivos, tablero
def elegible(d):
    """Segundos donde se permite disparar: rueda desde 13:32, con cola para la barrera mas larga y sin hueco entre t-600 y t+900."""
    hms = d.index.strftime("%H:%M:%S"); ok = d["rueda"].to_numpy() & (hms >= INICIO)
    ok[max(0, len(d) - COLA):] = False
    h = pd.Series(d["hueco"].to_numpy().astype("float64"))
    atras = h.rolling(601, min_periods=1).max().to_numpy(); adel = h[::-1].rolling(901, min_periods=1).max().to_numpy()[::-1]
    return ok & (atras == 0) & (adel == 0)


def t0_de(d):
    i = d.index[0]; return int(i.hour * 3600 + i.minute * 60 + i.second)


def objetivos(sesion, d):
    """Barreras por SEGUNDO (para el placebo) cacheadas en techo_ml_cache. Devuelve dict con y{x}, pnl{x}, t8, R300, ok, t0."""
    fn = os.path.join(CACHE, "obj_%s_%d.npz" % (sesion, len(d)))
    if os.path.exists(fn):
        z = np.load(fn); return {k: z[k] for k in z.files}
    p = d["ultimo"].to_numpy(); o = {}
    for x, h in BARRERAS:
        y, seg = B.barrera(d, x, h); fut = np.full(len(p), np.nan); fut[:len(p) - h] = p[h:]
        o["y%d" % x] = y.astype("int8"); o["pnl%d" % x] = (fut - p).astype("float32")
        if x == 8: o["t8"] = np.where(np.isinf(seg), np.nan, seg).astype("float32")
    a = d["alto"].shift(-1)[::-1].rolling(300, min_periods=300).max()[::-1]; b = d["bajo"].shift(-1)[::-1].rolling(300, min_periods=300).min()[::-1]
    o["R300"] = (a - b).to_numpy().astype("float32"); o["ok"] = elegible(d); o["t0"] = np.asarray(t0_de(d))
    np.savez_compressed(fn, **o)
    return o


def tablero(sesiones, N, nombre):
    """Tablero en grilla de 15 s de un conjunto de sesiones ({sesion: tabla}). Cacheado en parquet con 'nombre' (explorar / confirmar)."""
    pq = os.path.join(CACHE, "tablero_%s.parquet" % nombre)
    if os.path.exists(pq): return pd.read_parquet(pq)
    partes = []
    for s, d in sorted(sesiones.items()):
        o = objetivos(s, d); f = rasgos(d, N)
        g = o["ok"] & ((d.index.second.to_numpy() % PASO) == PASO - 1); pos = np.flatnonzero(g)
        t = f.iloc[pos].copy(); t["sesion"] = s; t["pos"] = pos; t["balde"] = (pos + int(o["t0"])) // 1800
        for x, _ in BARRERAS: t["y%d" % x] = o["y%d" % x][pos]; t["pnl%d" % x] = o["pnl%d" % x][pos]
        t["t8"] = o["t8"][pos]; t["R300"] = o["R300"][pos]
        partes.append(t)
    df = pd.concat(partes); df.to_parquet(pq)
    return df


# ------------------------------------------------------------------ modelos (hiperparametros FIJOS del pre-registro)
def xgb_clf():
    import xgboost as xgb
    return xgb.XGBClassifier(n_estimators=200, max_depth=3, learning_rate=0.03, subsample=0.5, colsample_bytree=0.5, min_child_weight=100, reg_lambda=10,
                             tree_method="hist", n_jobs=2, random_state=7, eval_metric="logloss", verbosity=0)


def xgb_reg():
    import xgboost as xgb
    return xgb.XGBRegressor(n_estimators=200, max_depth=3, learning_rate=0.03, subsample=0.5, colsample_bytree=0.5, min_child_weight=100, reg_lambda=10,
                            tree_method="hist", n_jobs=2, random_state=7, verbosity=0)


class Lineal:
    """Control lineal: recorte a percentiles 1-99 del entrenamiento, media para los faltantes, estandarizado, L2 (C = 0,01). clase = True -> logistica; False -> ridge."""
    def __init__(self, clase=True, C=0.01): self.clase = clase; self.C = C

    def _prep(self, X, ajustar=False):
        X = np.asarray(X, dtype="float64")
        if ajustar:
            self.lo = np.nanpercentile(X, 1, axis=0); self.hi = np.nanpercentile(X, 99, axis=0)
        X = np.clip(X, self.lo, self.hi)
        if ajustar:
            self.mu = np.nanmean(X, axis=0); self.sd = np.nanstd(X, axis=0); self.sd[~(self.sd > 0)] = 1.0
        X = (X - self.mu) / self.sd
        return np.nan_to_num(X, nan=0.0)

    def fit(self, X, y):
        from sklearn.linear_model import LogisticRegression, Ridge
        Z = self._prep(X, True)
        self.m = LogisticRegression(C=self.C, max_iter=1000) if self.clase else Ridge(alpha=1.0 / self.C)
        self.m.fit(Z, y); return self

    def predict_proba(self, X): return self.m.predict_proba(self._prep(X))

    def predict(self, X): return self.m.predict(self._prep(X))


def fuera_de_muestra(df, cols, ycol, fabrica):
    """Deja UNA sesion afuera por vuelta. Ajusta con las filas resueltas (y != 0) de las otras y predice p(sube) para TODAS las filas de la sesion afuera."""
    p = np.full(len(df), np.nan); ses = df["sesion"].to_numpy(); y = df[ycol].to_numpy(); X = df[cols].to_numpy(dtype="float32")
    for s in np.unique(ses):
        tr = (ses != s) & (y != 0); te = ses == s
        m = fabrica(); m.fit(X[tr], (y[tr] > 0).astype(int)); p[te] = m.predict_proba(X[te])[:, 1]
    return p


def auc(y01, p):
    from sklearn.metrics import roc_auc_score
    y01 = np.asarray(y01); p = np.asarray(p)
    if len(np.unique(y01)) < 2: return np.nan
    return float(roc_auc_score(y01, p))


def auc_por_dia(df, ycol, p):
    y = df[ycol].to_numpy(); ses = df["sesion"].to_numpy(); out = {}
    for s in np.unique(ses):
        m = (ses == s) & (y != 0); out[s] = auc((y[m] > 0).astype(int), p[m])
    return out


# ------------------------------------------------------------------ disparos, juicio y placebo
def desagrupar(pos, minimo=MIN_ENTRE):
    """pos = posiciones (segundo dentro de la tabla de 1 s) candidatas, ordenadas. El primero gana y tapa los 'minimo' segundos siguientes."""
    out = []; ult = -10 ** 9
    for k, i in enumerate(pos):
        if i - ult >= minimo: out.append(k); ult = i
    return np.asarray(out, dtype=int)


def disparos_de(df, p, U):
    """Disparos del modelo: confianza |p-0,5| >= U, lado = signo(p-0,5), desagrupado 60 s por sesion. Devuelve {sesion: (filas del df, pos, lado)}."""
    out = {}; ses = df["sesion"].to_numpy(); pos = df["pos"].to_numpy(); conf = np.abs(p - 0.5)
    for s in np.unique(ses):
        m = np.flatnonzero((ses == s) & (conf >= U) & ~np.isnan(p))
        if len(m) == 0: continue
        k = desagrupar(pos[m]); m = m[k]; out[s] = (m, pos[m], np.sign(p[m] - 0.5).astype(int))
    return out


def puntos_netos(y, lado, pnl_to, x):
    """Puntos por operacion, netos del costo: gana +x, pierde -x, sin resolver sale al precio de t+h."""
    bruto = np.where(y != 0, x * y * lado, np.nan_to_num(pnl_to, nan=0.0) * lado)
    return float(np.mean(bruto) - B.COSTO_PTS) if len(bruto) else float("nan")


def juicio(disp, objs, x, nombre=""):
    """disp = {sesion: (filas, pos, lado)}; objs = {sesion: dict de objetivos por segundo}. Usa B.juzgar + puntos netos."""
    ys, ls, ds, pn = [], [], [], []
    for s, (_, pos, lado) in disp.items():
        ys.append(objs[s]["y%d" % x][pos]); ls.append(lado); ds.append(np.full(len(pos), s)); pn.append(objs[s]["pnl%d" % x][pos])
    if not ys: return dict(nombre=nombre, n=0)
    y = np.concatenate(ys).astype(int); l = np.concatenate(ls); dd = np.concatenate(ds); pnl = np.concatenate(pn)
    r = B.juzgar(y, l, nombre, dd, x); r["netos"] = round(puntos_netos(y, l, pnl, x), 2); r["largos_%"] = round(100 * float((l > 0).mean()), 1)
    return r


def placebo(disp, objs, x, sorteos=200, semilla=7):
    """Mismas sesiones, misma media hora de reloj, mismos lados, mismo espaciado minimo; segundos elegibles al azar. Devuelve (media, desvio) del % de acierto."""
    rng = np.random.default_rng(semilla); plan = []
    for s, (_, pos, lado) in disp.items():
        ok = objs[s]["ok"]; balde = (np.arange(len(ok)) + int(objs[s]["t0"])) // 1800; y = objs[s]["y%d" % x]
        for b in np.unique(balde[pos]):
            plan.append((y, np.flatnonzero(ok & (balde == b)), lado[balde[pos] == b]))
    acs = []
    for _ in range(sorteos):
        aci = 0; n = 0
        for y, cand, lados in plan:
            k = len(lados); Lc = len(cand)
            if Lc == 0: continue
            paso = min(MIN_ENTRE, max(1, Lc // k)); libre = Lc - (k - 1) * paso
            pp = np.sort(rng.integers(0, max(1, libre), size=k)) + paso * np.arange(k); pp = np.minimum(pp, Lc - 1)
            yy = y[cand[pp]]; ll = rng.permutation(lados); r = yy != 0
            aci += int(((yy[r] * ll[r]) > 0).sum()); n += int(r.sum())
        acs.append(100.0 * aci / max(1, n))
    return float(np.mean(acs)), float(np.std(acs, ddof=1))
