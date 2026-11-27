# -*- coding: utf-8 -*-
"""RONDA 17 - comun: rutas, lectura de la cinta, tabla de 1 s, footprint por segundo y tick, niveles y area de valor.
Todo en prioridad baja (la PC es un i3 con ATAS abierto). Nada toca ATAS: solo lee archivos que ya bajo la sonda."""
import glob, os, sys
import numpy as np, pandas as pd

try:
    import ctypes, ctypes.wintypes as _W
    _k = ctypes.windll.kernel32; _k.GetCurrentProcess.restype = _W.HANDLE; _k.SetPriorityClass.argtypes = [_W.HANDLE, _W.DWORD]
    _k.SetPriorityClass(_k.GetCurrentProcess(), 0x00004000)   # BELOW_NORMAL
except Exception:
    pass

AQUI = os.path.dirname(os.path.abspath(__file__))
LAB = os.path.dirname(AQUI)
RAIZ = os.path.dirname(LAB)
FLUJO = os.path.join(os.environ.get("APPDATA", ""), "ATAS", "PythiaGex", "flujo")
CACHE_ES = os.path.join(RAIZ, "datos", "flujo", "cache", "ES")
R16 = os.path.join(RAIZ, "datos", "ronda16")
R17 = os.path.join(RAIZ, "datos", "ronda17")
TICK = 0.25
NY = "America/New_York"
sys.path.insert(0, os.path.join(LAB, "gatillo"))


def archivo_cinta(inst, dia):
    """La cinta de la sesion cuya rueda cae en 'dia' (AAAA-MM-DD). El nombre lleva desde-hasta en UTC; el 'hasta' es el dia."""
    for f in sorted(glob.glob(os.path.join(FLUJO, "cinta-%s-*.csv" % inst))):
        p = os.path.basename(f).split("-")
        if len(p) < 4 or not os.path.exists(f + ".listo"):
            continue
        fin = p[3][:8]
        if "%s-%s-%s" % (fin[:4], fin[4:6], fin[6:8]) == dia:
            return f
    return None


def leer_cinta(f):
    """Ordenes agresoras de una sesion, sin duplicados (TODAS las columnas, como base.py), con hora NY sin zona."""
    d = pd.read_csv(f)
    d = d.drop_duplicates()
    d["t"] = pd.to_datetime(d["t"])
    d = d.sort_values("t", kind="stable").reset_index(drop=True)
    d["t_ny"] = d["t"].dt.tz_localize("UTC").dt.tz_convert(NY).dt.tz_localize(None)
    return d


def tabla_1s_de_cinta(d):
    """La MISMA tabla de 1 s que laboratorio/gatillo/base.py (_tabla_1s), con indice en hora NY."""
    import base
    x = d.drop(columns=["t_ny"]).copy()
    t = base._tabla_1s(x)
    t.index = pd.to_datetime(t.index).tz_localize("UTC").tz_convert(NY).tz_localize(None)
    return t


def tabla_1s(inst, dia):
    """Tabla de 1 s (sesion completa 18:00 -> 17:00 NY), indice en hora NY. ES: cache de base.py; MES: datos/ronda17."""
    if inst == "ES":
        s = pd.read_parquet(os.path.join(CACHE_ES, "seg-%s.parquet" % dia))
        s.index = pd.to_datetime(s.index).tz_localize("UTC").tz_convert(NY).tz_localize(None)
    else:
        s = pd.read_parquet(os.path.join(R17, "seg-%s-%s.parquet" % (inst, dia)))
    s = s.sort_index()
    return s[~s.index.duplicated(keep="last")]


def explotar(d):
    """Footprint por orden y tick: las ordenes que barren se reparten parejo entre los ticks recorridos (aproximacion declarada).
    Devuelve t (segundo NY), tick (entero), compra, venta, barr (True si el tick vino de una orden que barrio varios precios)."""
    p0 = np.rint(d["primero"].to_numpy() / TICK).astype(np.int64); p1 = np.rint(d["ultimo"].to_numpy() / TICK).astype(np.int64)
    vol = d["vol"].to_numpy("float64"); lado = d["lado"].to_numpy(); t = d["t_ny"].dt.floor("s").to_numpy()
    n = np.abs(p1 - p0) + 1
    rep = np.repeat(np.arange(len(d)), n)
    paso = np.where(p1 >= p0, 1, -1)
    off = np.arange(len(rep)) - np.repeat(np.cumsum(n) - n, n)
    tick = p0[rep] + off * paso[rep]
    v = vol[rep] / n[rep]
    return pd.DataFrame({"t": t[rep], "tick": tick, "compra": np.where(lado[rep] == 1, v, 0.0),
                         "venta": np.where(lado[rep] == -1, v, 0.0), "barr": n[rep] > 1})


def area_valor(vol, pct=70, paso=2):
    """vol: array por tick (contiguo). Regla de ATAS verificada 1.186/1.186 (Superindicador): con huecos, paso 2, POC empatado = alto.
    (Copia exacta de r16_00_niveles.area_valor.)"""
    con = np.nonzero(vol > 0)[0]
    if len(con) == 0:
        return -1, -1, -1
    bajo, alto = con[0], con[-1]
    vmax = vol.max(); poc = int(np.nonzero(vol == vmax)[0][-1])
    obj = vol[bajo:alto + 1].sum() * pct / 100.0
    acum = vol[poc]; hi = lo = poc
    while acum <= obj and not (hi >= alto and lo <= bajo):
        up = dn = 0.0; hiN, loN = hi, lo
        for _ in range(paso):
            if hiN + 1 <= alto: hiN += 1; up += vol[hiN]
        for _ in range(paso):
            if loN - 1 >= bajo: loN -= 1; dn += vol[loN]
        if up == 0 and dn == 0: hi, lo = hiN, loN
        elif up >= dn: hi = hiN; acum += up
        else: lo = loN; acum += dn
        if acum >= obj: break
    return poc, hi, lo


def perfil_va(ticks, vols):
    """POC/VAH/VAL en precio desde (tick, volumen) sueltos."""
    ticks = np.asarray(ticks, dtype=np.int64); vols = np.asarray(vols, dtype="float64")
    tmin = int(ticks.min()); arr = np.zeros(int(ticks.max()) - tmin + 1)
    np.add.at(arr, ticks - tmin, vols)
    poc, vah, val = area_valor(arr)
    return (tmin + poc) * TICK, (tmin + vah) * TICK, (tmin + val) * TICK


def niveles_de_cinta(d, dia):
    """Igual que r16_00_niveles pero con duplicados por todas las columnas: rueda O/H/L/C, noche, balance inicial, POC/VAH/VAL."""
    dia_ts = pd.Timestamp(dia)
    hm = d["t_ny"].dt.strftime("%H:%M")
    rueda = (d["t_ny"].dt.normalize() == dia_ts) & (hm >= "09:30") & (hm < "16:00")
    noche = d["t_ny"] < dia_ts + pd.Timedelta(hours=9, minutes=30)
    ib = rueda & (hm < "10:30")
    r = d[rueda]
    fp = explotar(r)
    poc, vah, val = perfil_va(fp["tick"].to_numpy(), (fp["compra"] + fp["venta"]).to_numpy())
    px = r["ultimo"].to_numpy()
    return {"dia": dia, "o": float(px[0]), "h": float(r[["primero", "ultimo"]].max().max()), "l": float(r[["primero", "ultimo"]].min().min()),
            "c": float(px[-1]), "on_h": float(d.loc[noche, ["primero", "ultimo"]].max().max()),
            "on_l": float(d.loc[noche, ["primero", "ultimo"]].min().min()),
            "ib_h": float(d.loc[ib, ["primero", "ultimo"]].max().max()), "ib_l": float(d.loc[ib, ["primero", "ultimo"]].min().min()),
            "poc": poc, "vah": vah, "val": val, "vol_rueda": float(r["vol"].sum()), "ordenes": int(len(r))}


def niveles_periodo(tablas, cierre_dia):
    """Semana o mes: maximo/minimo de las sesiones completas, cierre de la ultima rueda (16:00 NY) y POC/VAH/VAL aproximados
    (volumen de cada segundo en su ultimo precio). tablas: lista de tablas de 1 s (indice NY)."""
    h = max(float(s["alto"].max()) for s in tablas); l = min(float(s["bajo"].min()) for s in tablas)
    ult = tablas[-1]; c = float(ult.loc[:pd.Timestamp(cierre_dia) + pd.Timedelta(hours=16) - pd.Timedelta(seconds=1), "ultimo"].iloc[-1])
    tk = np.concatenate([np.rint(s["ultimo"].to_numpy() / TICK).astype(np.int64) for s in tablas])
    vv = np.concatenate([s["vol"].to_numpy("float64") for s in tablas])
    poc, vah, val = perfil_va(tk[vv > 0], vv[vv > 0])
    return {"h": h, "l": l, "c": c, "poc": poc, "vah": vah, "val": val}
