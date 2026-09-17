# -*- coding: utf-8 -*-
"""
base.py — la materia prima COMUN de la busqueda de un complemento / gatillo para el CVD (pedido del operador 17-09).

Fuentes (todas del propio ATAS del operador, sin pagar nada):
  CINTA   %APPDATA%/ATAS/PythiaGex/flujo/cinta-*.csv : TODAS las ordenes agresoras (una fila por orden, milisegundos) con la
          punta del libro ANTES (abid/aask) y DESPUES (dbid/dask) de cada una. La saca la sonda de Flujo Claro.
  VELAS   datos/flujo/velas-MNQ-M1-*.csv : 27 mil velas de 1 min (20 ruedas) con delta, maximo/minimo del delta, grandes.
  NIVELES %APPDATA%/ATAS/pythiagex-centinela-rebobinado-atas-MNQ-TimeFrame-M1.jsonl : zero gamma, dominantes, majors, Max Change
          por minuto, en precio del contrato del grafico de ese momento (se usan como DISTANCIA al cierre de esa misma vela).

Reglas de la casa (no negociables, las mismas para todos los que usen esto):
  - nada mira adelante: todo rasgo en el instante t usa solo filas con tiempo <= t;
  - el objetivo es de SCALPER: +X puntos antes que -X dentro de H segundos (barrera doble), medido con la cinta real;
  - se explora en explorar(T) (sesiones anteriores a CORTE_CONFIRMAR) y se confirma UNA vez en confirmar(T); lo que no repite, no existe;
  - costo de ida y vuelta: COSTO_PTS (spread + comision); el porcentaje para empatar con barrera simetrica X es (X + costo) / (2 X).
"""
import glob, json, os
import numpy as np, pandas as pd

try:   # todo lo que use esta base corre en PRIORIDAD BAJA: la PC es un i3 de 4 nucleos con ATAS abierto y el operador operando en vivo
    import ctypes; ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x00004000)
except Exception: pass

APP = os.path.join(os.environ.get("APPDATA", ""), "ATAS")
FLUJO = os.path.join(APP, "PythiaGex", "flujo")
RAIZ = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CACHE = os.path.join(RAIZ, "datos", "flujo", "cache")
TICK = 0.25
COSTO_PTS = 0.85          # MNQ: 1 tick de spread entre entrada y salida (0,25) + comision ~USD 1,2 ida y vuelta (0,60 pts a USD 2 el punto)
RUEDA = ("13:30", "20:00")  # UTC


def empate(x, costo=COSTO_PTS):
    """Porcentaje de aciertos que hace falta para no perder con barrera simetrica de x puntos."""
    return (x + costo) / (2.0 * x)


# ------------------------------------------------------------------ cinta
def archivos_cinta():
    fs = [f for f in glob.glob(os.path.join(FLUJO, "cinta-*.csv")) if os.path.exists(f + ".listo")]
    fs += glob.glob(os.path.join(RAIZ, "datos", "flujo", "cinta-*-sesion-*.csv"))
    return sorted(fs)


def cinta(refrescar=False):
    """Todas las ordenes agresoras, una fila por orden. Columnas: t (datetime64 UTC), primero, ultimo, vol, lado (+1 compra / -1 venta),
    prints, abid/abidv/aask/aaskv (punta ANTES), dbid/dbidv/dask/daskv (punta DESPUES), sesion (fecha de la rueda), rueda (bool)."""
    os.makedirs(CACHE, exist_ok=True); pq = os.path.join(CACHE, "cinta.parquet"); fs = archivos_cinta()
    firma = "|".join("%s:%d" % (os.path.basename(f), os.path.getsize(f)) for f in fs)
    if not refrescar and os.path.exists(pq) and os.path.exists(pq + ".firma") and open(pq + ".firma").read() == firma:
        return pd.read_parquet(pq)
    partes = []
    for f in fs:
        d = pd.read_csv(f, parse_dates=["t"])
        if len(d) < 1000: continue
        partes.append(d)
    df = pd.concat(partes, ignore_index=True).drop_duplicates().sort_values("t", kind="stable").reset_index(drop=True)
    # la sesion de futuros va de 22:00 UTC a 21:00 UTC del dia siguiente: se nombra por el dia en que cae la rueda
    df["sesion"] = (df["t"] + pd.Timedelta(hours=2)).dt.strftime("%Y-%m-%d")
    hm = df["t"].dt.strftime("%H:%M"); df["rueda"] = (hm >= RUEDA[0]) & (hm < RUEDA[1])
    # un mismo dia puede venir de dos contratos (U6 por el grafico continuo, Z6 por el propio): se queda el de MAS ordenes por sesion
    df["px_grupo"] = (df["ultimo"] // 150).astype(int)
    for c in ("vol", "lado", "prints"): df[c] = df[c].astype("int32")
    df.to_parquet(pq); open(pq + ".firma", "w").write(firma)
    return df


def sesiones(df):
    return sorted(df["sesion"].unique())


def segundos(df_sesion):
    """Una sesion de cinta llevada a una grilla de 1 segundo (solo segundos con o sin operaciones, relleno hacia adelante):
    ultimo, alto, bajo, vol, delta, n ordenes, punta (bid/ask y tamanos al cierre del segundo)."""
    d = df_sesion; s = d["t"].dt.floor("s")
    g = d.groupby(s)
    out = pd.DataFrame({"ultimo": g["ultimo"].last(), "alto": g[["primero", "ultimo"]].max().max(axis=1), "bajo": g[["primero", "ultimo"]].min().min(axis=1),
                        "vol": g["vol"].sum(), "delta": (d["vol"] * d["lado"]).groupby(s).sum(), "n": g["vol"].size(),
                        "bid": g["dbid"].last(), "bidv": g["dbidv"].last(), "ask": g["dask"].last(), "askv": g["daskv"].last()})
    idx = pd.date_range(out.index[0], out.index[-1], freq="s"); out = out.reindex(idx)
    for c in ("vol", "delta", "n"): out[c] = out[c].fillna(0)
    out["ultimo"] = out["ultimo"].ffill(); out["alto"] = out["alto"].fillna(out["ultimo"]); out["bajo"] = out["bajo"].fillna(out["ultimo"])
    for c in ("bid", "bidv", "ask", "askv"): out[c] = out[c].ffill()
    return out


COLS_RICAS = ["ultimo", "alto", "bajo", "vol", "delta", "n", "d1", "d2_4", "d5_9", "d10_49", "d50", "barre_c", "barre_v", "abs_bid", "abs_ask",
              "rompe_bid", "rompe_ask", "ofi", "ofi_pas", "bid", "bidv", "ask", "askv"]


def _ofi(pb0, qb0, pa0, qa0, pb1, qb1, pa1, qa1):
    """Desbalance del flujo de ORDENES de un cambio de la punta (Cont, Kukanov y Stoikov 2014): lo que se suma al bid y se saca del ask,
    menos lo que se saca del bid y se suma al ask."""
    return (np.where(pb1 >= pb0, qb1, 0) - np.where(pb1 <= pb0, qb0, 0) - np.where(pa1 <= pa0, qa1, 0) + np.where(pa1 >= pa0, qa0, 0)).astype("float64")


def segundos_ricos(sesion, df=None, refrescar=False):
    """LA TABLA DE TRABAJO: una sesion en grilla de 1 segundo con todo lo que sale de la cinta orden por orden (cacheada en parquet).
      ultimo/alto/bajo/vol/delta/n      precio y flujo agresor del segundo
      d1, d2_4, d5_9, d10_49, d50       delta por TAMANO de la orden (1 contrato, 2-4, 5-9, 10-49, 50 o mas)
      barre_c / barre_v                 contratos de ordenes de compra / venta que BARRIERON mas de un nivel de precio
      abs_bid / abs_ask                 ABSORCION REAL en la punta: contratos de ventas que consumieron TODO el bid visible y el bid NO bajo
                                        (lo repusieron: iceberg / comprador pasivo aguantando); idem compras contra el ask
      rompe_bid / rompe_ask             contratos de ordenes que consumieron la punta visible y la punta SI cedio
      ofi                               desbalance del flujo de ordenes en la punta (pasivo + agresivo), muestreado en cada orden
      ofi_pas                           la parte PASIVA del ofi: lo que pasa en la punta ENTRE ordenes (altas y bajas de limites).
                                        Es lo unico que el delta/CVD no ve.
      bid/bidv/ask/askv                 la punta al final del segundo (relleno hacia adelante)
      rueda                             True entre 13:30 y 20:00 UTC"""
    os.makedirs(CACHE, exist_ok=True); pq = os.path.join(CACHE, "seg-%s.parquet" % sesion)
    if not refrescar and os.path.exists(pq): return pd.read_parquet(pq)
    if df is None: df = cinta()
    d = df[df["sesion"] == sesion]
    if d["px_grupo"].nunique() > 1:   # si la sesion vino de dos contratos, queda el de mas ordenes
        g = d.groupby("px_grupo").size()
        if g.max() < 0.9 * len(d): d = d[(d["px_grupo"] - g.idxmax()).abs() <= 1]
    d = d.sort_values("t", kind="stable")
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
    # ofi: transicion pasiva (despues de la orden anterior -> antes de esta) + transicion de la propia orden (antes -> despues)
    pdb, pdbv, pda, pdav = np.roll(db, 1), np.roll(dbv, 1), np.roll(da, 1), np.roll(dav, 1)
    pas = _ofi(pdb, pdbv, pda, pdav, ab, abv, aa, aav); pas[0] = 0
    dt = d["t"].to_numpy(); hueco = np.empty(len(d), bool); hueco[0] = True; hueco[1:] = (dt[1:] - dt[:-1]) > np.timedelta64(60, "s"); pas[hueco] = 0
    x["ofi_pas"] = pas; x["ofi"] = pas + _ofi(ab, abv, aa, aav, db, dbv, da, dav)
    s = d["t"].dt.floor("s").to_numpy(); x.index = s
    out = x.groupby(level=0).sum()
    px = pd.DataFrame({"ultimo": ult, "alto": np.maximum(ult, pri), "bajo": np.minimum(ult, pri), "bid": db, "bidv": dbv, "ask": da, "askv": dav}, index=s)
    px = px.groupby(level=0).agg({"ultimo": "last", "alto": "max", "bajo": "min", "bid": "last", "bidv": "last", "ask": "last", "askv": "last"})
    out = out.join(px); idx = pd.date_range(out.index[0], out.index[-1], freq="s"); out = out.reindex(idx)
    out["ultimo"] = out["ultimo"].ffill(); out["alto"] = out["alto"].fillna(out["ultimo"]); out["bajo"] = out["bajo"].fillna(out["ultimo"])
    for c in ("bid", "bidv", "ask", "askv"): out[c] = out[c].ffill()
    out = out.fillna(0.0)[COLS_RICAS]
    for c in COLS_RICAS:
        if c not in ("ultimo", "alto", "bajo", "bid", "ask"): out[c] = out[c].astype("float32")
    hm = out.index.strftime("%H:%M"); out["rueda"] = (hm >= RUEDA[0]) & (hm < RUEDA[1])
    out.to_parquet(pq)
    return out


CORTE_CONFIRMAR = "2026-09-08"   # se EXPLORA en las sesiones anteriores a esta fecha y se CONFIRMA, una sola vez, de esta en adelante


def explorar(T): return {s: d for s, d in T.items() if s < CORTE_CONFIRMAR}


def confirmar(T): return {s: d for s, d in T.items() if s >= CORTE_CONFIRMAR}


def todas_las_sesiones(refrescar=False):
    """{sesion: tabla de 1 s} de todas las sesiones con cinta. Liviano: ~80 mil filas por sesion. Usar ESTO, no la cinta cruda (8+ millones de filas)."""
    fs = sorted(glob.glob(os.path.join(CACHE, "seg-*.parquet")))
    if fs and not refrescar: return {os.path.basename(f)[4:-8]: pd.read_parquet(f) for f in fs}
    df = cinta(); return {s: segundos_ricos(s, df, refrescar) for s in sesiones(df)}


def barrera(seg, x, h):
    """Objetivo del scalper sobre la grilla de 1 s: por cada segundo t, +1 si el precio toca ultimo[t] + x ANTES que ultimo[t] - x dentro de
    h segundos, -1 al reves, 0 si ninguna (o las dos en el mismo segundo). Devuelve (y, segundos hasta tocar). Solo mira t+1 en adelante."""
    p = seg["ultimo"].to_numpy(); hi = seg["alto"].to_numpy(); lo = seg["bajo"].to_numpy(); n = len(p)
    t_up = np.full(n, np.inf); t_dn = np.full(n, np.inf)
    for k in range(1, h + 1):
        a = np.empty(n); a[:] = -np.inf; a[:n - k] = hi[k:]; b = np.empty(n); b[:] = np.inf; b[:n - k] = lo[k:]
        m = (a >= p + x) & np.isinf(t_up); t_up[m] = k
        m = (b <= p - x) & np.isinf(t_dn); t_dn[m] = k
    y = np.where(t_up < t_dn, 1, np.where(t_dn < t_up, -1, 0)).astype(int)
    return y, np.minimum(t_up, t_dn)


def barras(seg, regla="1min"):
    """Velas de cualquier marco a partir de la grilla de 1 s (para probar temporalidades: 5s, 15s, 30s, 1min, 2min, 5min)."""
    g = seg.resample(regla, label="left", closed="left")
    out = pd.DataFrame({"o": g["ultimo"].first(), "h": g["alto"].max(), "l": g["bajo"].min(), "c": g["ultimo"].last(), "vol": g["vol"].sum(), "delta": g["delta"].sum(), "n": g["n"].sum()})
    return out.dropna(subset=["c"])


# ------------------------------------------------------------------ velas de 1 min (20 ruedas) y niveles de gamma
def velas_m1():
    f = sorted(glob.glob(os.path.join(RAIZ, "datos", "flujo", "velas-MNQ-M1-*.csv")))[-1]
    d = pd.read_csv(f, parse_dates=["t"]).drop_duplicates("t").set_index("t").sort_index()
    hm = d.index.strftime("%H:%M"); d["rueda"] = (hm >= RUEDA[0]) & (hm < RUEDA[1]); d["dia"] = d.index.strftime("%Y-%m-%d")
    return d


def niveles_m1(refrescar=False):
    """Niveles de gamma por minuto (la ultima version grabada de cada vela), como DISTANCIA en puntos al cierre de esa vela:
    d_zero (precio - zero gamma), d_dom0, d_dom1, d_mp (major positivo), d_mn, y el cuadrante."""
    os.makedirs(CACHE, exist_ok=True); pq = os.path.join(CACHE, "niveles_m1.parquet")
    src = os.path.join(APP, "pythiagex-centinela-rebobinado-atas-MNQ-TimeFrame-M1.jsonl")
    if not refrescar and os.path.exists(pq) and os.path.getmtime(pq) > os.path.getmtime(src) - 86400: return pd.read_parquet(pq)
    filas = {}
    with open(src, encoding="utf-8", errors="replace") as fh:
        for l in fh:
            try: j = json.loads(l)
            except Exception: continue
            nv = j.get("niv") or {}
            if not nv or j.get("c") is None: continue
            c = j["c"]; r = dict(c=c)
            for k_src, k_dst in (("zero_vol", "d_zero"), ("dom0", "d_dom0"), ("dom1", "d_dom1"), ("mp_vol", "d_mp"), ("mn_vol", "d_mn"), ("mc1", "d_mc1"), ("mc5", "d_mc5"), ("mc15", "d_mc15")):
                v = nv.get(k_src); r[k_dst] = (c - v) if v is not None else np.nan
            r["cuadrante"] = nv.get("q_cuadrante", np.nan)
            filas[j["t"][:16]] = r   # la ultima escritura de cada minuto gana
    d = pd.DataFrame.from_dict(filas, orient="index"); d.index = pd.to_datetime(d.index); d = d.sort_index()
    d.index = d.index.floor("min"); d.to_parquet(pq)
    return d


# ------------------------------------------------------------------ juicio
def juzgar(y, lado, nombre="", dias=None, x=None):
    """y = resultado de la barrera (+1/-1/0) en cada disparo; lado = +1 si el disparo dice 'sube', -1 si dice 'baja'.
    Devuelve n, aciertos sobre los resueltos, z contra 50 %, y (si hay dias) cuantos dias quedaron arriba de 50 %."""
    y = np.asarray(y); lado = np.asarray(lado); ok = y != 0; n = int(ok.sum())
    if n == 0: return dict(nombre=nombre, n=0)
    ac = float(((y[ok] * lado[ok]) > 0).mean()); z = (ac - 0.5) / np.sqrt(0.25 / n)
    r = dict(nombre=nombre, n=n, acierto=round(100 * ac, 1), z=round(z, 2), sin_resolver=int((~ok).sum()))
    if x: r["empate"] = round(100 * empate(x), 1)
    if dias is not None:
        dd = pd.Series((y[ok] * lado[ok]) > 0).groupby(np.asarray(dias)[ok]).agg(["mean", "size"])
        r["dias_arriba"] = "%d de %d" % (int((dd["mean"] > 0.5).sum()), len(dd)); r["peor_dia"] = round(100 * float(dd["mean"].min()), 1)
    return r


if __name__ == "__main__":
    df = cinta(); print("cinta:", len(df), "ordenes |", df["t"].min(), "->", df["t"].max())
    r = df.groupby("sesion").agg(ordenes=("vol", "size"), contratos=("vol", "sum"), rueda=("rueda", "sum"), pmin=("ultimo", "min"), pmax=("ultimo", "max"))
    print(r.to_string())
    print("sin punta antes (abid nulo): %.2f %% | sin punta despues: %.2f %%" % (100 * df["abid"].isna().mean(), 100 * df["dbid"].isna().mean()))
    print("barrido (ultimo != primero): %.2f %% de las ordenes | orden >= 10: %.2f %% | >= 50: %.3f %%" % (100 * (df["ultimo"] != df["primero"]).mean(), 100 * (df["vol"] >= 10).mean(), 100 * (df["vol"] >= 50).mean()))
    for x in (5, 8, 12): print("barrera +-%d pts: hace falta %.1f %% para empatar" % (x, 100 * empate(x)))
