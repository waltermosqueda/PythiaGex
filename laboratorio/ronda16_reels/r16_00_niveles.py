# -*- coding: utf-8 -*-
"""RONDA 16 (23-11-2026) - el metodo del trader de los reels, medido. Paso 0: NIVELES y FOOTPRINT por sesion desde la cinta de ES.

Fuente: la cinta orden por orden de ES que saca la sonda de Flujo Claro (%APPDATA%/ATAS/PythiaGex/flujo/cinta-ES-*.csv): una fila
por orden agresora (t en UTC, primero/ultimo precio, vol, lado +1 compra a mercado / -1 venta a mercado, punta antes/despues).

Por cada sesion (dia de la RUEDA de Nueva York; la hora de NY sale de la zona horaria, asi el cambio de horario del 01-11-2026 no
corre nada) guarda en datos/ronda16/:
  niveles.parquet   una fila por dia: apertura/maximo/minimo/cierre de la rueda (9:30-16:00 NY), rango nocturno (18:00-9:30 NY),
                    balance inicial (9:30-10:30 NY), POC/VAH/VAL de la rueda (area de valor de ATAS: 70 %, paso 2, con huecos;
                    POC empatado = el mas alto) y VWAP de la rueda al cierre.
  fp-<dia>.parquet  footprint por TICK y por vela de 30 s de la rueda (compras y ventas a mercado por precio). Las ordenes que
                    barren varios precios (1,5 % del total) se reparten parejo entre los ticks que recorrieron: aproximacion dicha.
Nada mira adelante: los niveles de un dia se usan recien al dia siguiente (los arma r16_01).
Uso: python -I r16_00_niveles.py
"""
import glob, os, sys
import numpy as np, pandas as pd

try:
    import ctypes, ctypes.wintypes as _W
    _k = ctypes.windll.kernel32; _k.GetCurrentProcess.restype = _W.HANDLE; _k.SetPriorityClass.argtypes = [_W.HANDLE, _W.DWORD]
    _k.SetPriorityClass(_k.GetCurrentProcess(), 0x00004000)
except Exception:
    pass

FLUJO = os.path.join(os.environ.get("APPDATA", ""), "ATAS", "PythiaGex", "flujo")
RAIZ = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SALIDA = os.path.join(RAIZ, "datos", "ronda16")
TICK = 0.25
NY = "America/New_York"


def area_valor(vol, pct=70, paso=2):
    """vol: array por tick (contiguo). Regla de ATAS verificada 1.186/1.186 (Superindicador): con huecos, paso 2, POC empatado = alto."""
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


def repartir(d):
    """Filas (tick, compra, venta) por orden; las que barren se reparten parejo entre los ticks recorridos."""
    p0 = np.rint(d["primero"].to_numpy() / TICK).astype(np.int64); p1 = np.rint(d["ultimo"].to_numpy() / TICK).astype(np.int64)
    vol = d["vol"].to_numpy("float64"); lado = d["lado"].to_numpy(); t = d["t_ny"].to_numpy()
    n = np.abs(p1 - p0) + 1
    rep = np.repeat(np.arange(len(d)), n)
    paso = np.where(p1 >= p0, 1, -1)
    off = np.arange(len(rep)) - np.repeat(np.cumsum(n) - n, n)
    tick = p0[rep] + off * paso[rep]
    v = vol[rep] / n[rep]
    return pd.DataFrame({"t": t[rep], "tick": tick, "compra": np.where(lado[rep] == 1, v, 0.0), "venta": np.where(lado[rep] == -1, v, 0.0)})


def main():
    os.makedirs(SALIDA, exist_ok=True)
    filas = []
    for f in sorted(glob.glob(os.path.join(FLUJO, "cinta-ES-*.csv"))):
        if not os.path.exists(f + ".listo"):
            continue
        d = pd.read_csv(f, usecols=["t", "primero", "ultimo", "vol", "lado"]).drop_duplicates()
        if len(d) < 10000:
            continue
        d["t_ny"] = pd.to_datetime(d["t"], utc=True).dt.tz_convert(NY).dt.tz_localize(None)
        dia = d["t_ny"].max().normalize()
        hm = d["t_ny"].dt.strftime("%H:%M")
        rueda = (d["t_ny"].dt.normalize() == dia) & (hm >= "09:30") & (hm < "16:00")
        noche = d["t_ny"] < dia + pd.Timedelta(hours=9, minutes=30)
        ib = rueda & (hm < "10:30")
        r = d[rueda]
        if len(r) < 5000:
            print("sin rueda completa:", os.path.basename(f)); continue
        px = r["ultimo"].to_numpy()
        fp = repartir(r)
        tmin = int(fp["tick"].min()); vol = np.zeros(int(fp["tick"].max()) - tmin + 1)
        np.add.at(vol, (fp["tick"] - tmin).to_numpy(), (fp["compra"] + fp["venta"]).to_numpy())
        poc, vah, val = area_valor(vol)
        vwap = float((r["ultimo"] * r["vol"]).sum() / r["vol"].sum())
        fila = {"dia": dia.date().isoformat(), "o": float(px[0]), "h": float(r[["primero", "ultimo"]].max().max()),
                "l": float(r[["primero", "ultimo"]].min().min()), "c": float(px[-1]),
                "on_h": float(d.loc[noche, ["primero", "ultimo"]].max().max()) if noche.any() else np.nan,
                "on_l": float(d.loc[noche, ["primero", "ultimo"]].min().min()) if noche.any() else np.nan,
                "ib_h": float(d.loc[ib, ["primero", "ultimo"]].max().max()), "ib_l": float(d.loc[ib, ["primero", "ultimo"]].min().min()),
                "poc": (tmin + poc) * TICK, "vah": (tmin + vah) * TICK, "val": (tmin + val) * TICK, "vwap": vwap,
                "vol_rueda": float(r["vol"].sum()), "delta_rueda": float((r["vol"] * r["lado"]).sum()), "ordenes": len(r)}
        filas.append(fila)
        fp["vela30"] = fp["t"].dt.floor("30s")
        g = fp.groupby(["vela30", "tick"], as_index=False)[["compra", "venta"]].sum()
        g.to_parquet(os.path.join(SALIDA, "fp-%s.parquet" % fila["dia"]))
        print(fila["dia"], "O/H/L/C", fila["o"], fila["h"], fila["l"], fila["c"], "| POC", fila["poc"], "VAH", fila["vah"], "VAL", fila["val"],
              "| IB", fila["ib_l"], "-", fila["ib_h"], flush=True)
    pd.DataFrame(filas).to_parquet(os.path.join(SALIDA, "niveles.parquet"))
    print("dias:", len(filas))


if __name__ == "__main__":
    main()
