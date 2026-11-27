# -*- coding: utf-8 -*-
"""DATABENTO -> CINTA EN FORMATO SONDA (27-11-2026), para el juez ciego de la R23 con ES de 2023 (si el operador compra los datos).
Sigue al pie de la letra PREREGISTRO_VIVO.md de la R23, seccion 5: UNA FILA POR OPERACION del archivo (schema trades o tbbo),
t = ts_event en UTC con milisegundos (AAAA-MM-DDTHH:MM:SS.mmm), primero = ultimo = price, vol = size, lado = +1 si el agresor compro
(side B), -1 si vendio (A), 0 si no se sabe (N). Con tbbo, la punta ANTES de la operacion va en abid/abidv/aask/aaskv (dbid..dask quedan
vacias: tbbo no trae la de despues). Las cintas se cortan por sesion CME (18:00 NY de la vispera a 17:00 NY) y se llaman
cinta-ES-<desde>-<hasta>.csv (UTC, AAAAMMDDHHMM) con su .listo, como las de la sonda.
NUNCA escribir en %APPDATA%\\ATAS\\PythiaGex\\flujo (ahi leen los jueces en vivo): usar una carpeta propia y pasarla al juez con --carpeta.
Lee de a trozos (no carga el archivo entero). Uso:
  python -I herramientas/databento_a_cinta.py <carpeta_salida> <archivo1.dbn.zst> [archivo2 ...]
  python -I herramientas/databento_a_cinta.py --validar <archivo_trades_de_un_dia.dbn.zst>   (contra las velas de 5 min de ES de ATAS)"""
import sys, os, glob
from datetime import datetime, timedelta
from zoneinfo import ZoneInfo
sys.stdout.reconfigure(encoding="utf-8")
import numpy as np, pandas as pd
NY = ZoneInfo("America/New_York"); UTC = ZoneInfo("UTC")
CAB = "t,primero,ultimo,vol,lado,prints,abid,abidv,aask,aaskv,dbid,dbidv,dask,daskv\n"
RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FLUJO = os.path.normcase(os.path.join(os.environ.get("APPDATA", ""), "ATAS", "PythiaGex", "flujo"))

def num(x):
    """Precio como lo escribe la sonda: 7710 / 7710.5 / 7710.25 (sin ceros de mas)."""
    return ("%.6f" % float(x)).rstrip("0").rstrip(".")

def dia_de_sesion(ts_utc):
    """Dia de la sesion CME: lo que pasa desde las 18:00 NY cuenta para el dia siguiente."""
    ny = ts_utc.dt.tz_convert(NY)  # .dt: los VALORES (Series.tz_convert sin .dt convierte el indice y deja los valores en UTC)
    return (ny + pd.Timedelta(hours=6)).dt.strftime("%Y-%m-%d")

def limites(dia):
    d = datetime.strptime(dia, "%Y-%m-%d")
    desde = (datetime(d.year, d.month, d.day, 18, 0, tzinfo=NY) - timedelta(days=1)).astimezone(UTC)
    hasta = datetime(d.year, d.month, d.day, 17, 0, tzinfo=NY).astimezone(UTC)
    return desde, hasta

def convertir(salida, archivos, trozo=2_000_000):
    import databento as db
    if os.path.normcase(os.path.abspath(salida)).startswith(FLUJO): sys.exit("no: esa es la carpeta de los jueces en vivo")
    os.makedirs(salida, exist_ok=True)
    abiertos = {}; filas = {}
    for a in archivos:
        st = db.DBNStore.from_file(a)
        print("leyendo %s (schema %s, %s .. %s)" % (os.path.basename(a), st.schema, st.start, st.end), flush=True)
        for df in st.to_df(count=trozo):
            if "action" in df.columns: df = df[df["action"] == "T"]
            if len(df) == 0: continue
            df = df.sort_values(["ts_event", "sequence"]) if "sequence" in df.columns else df.sort_values("ts_event")
            ts = pd.to_datetime(df["ts_event"], utc=True)
            dias = dia_de_sesion(ts)
            t = ts.dt.tz_localize(None).values.astype("datetime64[ms]").astype(str)
            lado = df["side"].map({"B": 1, "A": -1}).fillna(0).astype(int).to_numpy()
            pr = df["price"].to_numpy(float); vol = df["size"].to_numpy(int)
            tiene_punta = "bid_px_00" in df.columns
            if tiene_punta:
                bp, bs, ap, as_ = (df[c].to_numpy() for c in ("bid_px_00", "bid_sz_00", "ask_px_00", "ask_sz_00"))
            dv = dias.to_numpy()
            for dia in pd.unique(dv):
                m = dv == dia
                if dia not in abiertos:
                    desde, hasta = limites(dia)
                    ruta = os.path.join(salida, "cinta-ES-%s-%s.csv" % (desde.strftime("%Y%m%d%H%M"), hasta.strftime("%Y%m%d%H%M")))
                    abiertos[dia] = open(ruta, "w", encoding="utf-8", newline="\n"); abiertos[dia].write(CAB); filas[dia] = 0
                f = abiertos[dia]
                if tiene_punta:
                    lineas = ["%s,%s,%s,%d,%d,1,%s,%d,%s,%d,,,," % (a1, num(p1), num(p1), v1, l1, num(b1), int(b2), num(a2_), int(a3))
                              for a1, p1, v1, l1, b1, b2, a2_, a3 in zip(t[m], pr[m], vol[m], lado[m], bp[m], bs[m], ap[m], as_[m])]
                else:
                    lineas = ["%s,%s,%s,%d,%d,1,,,,,,,," % (a1, num(p1), num(p1), v1, l1) for a1, p1, v1, l1 in zip(t[m], pr[m], vol[m], lado[m])]
                f.write("\n".join(lineas) + "\n"); filas[dia] += int(m.sum())
    for dia, f in abiertos.items():
        f.close(); open(f.name + ".listo", "w").write(str(filas[dia]))
        print("  %s: %d operaciones -> %s" % (dia, filas[dia], os.path.basename(f.name)), flush=True)
    return {d: abiertos[d].name for d in abiertos}

def validar(archivo):
    tmp = os.path.join(os.environ["TEMP"], "databento_a_cinta_validar"); os.makedirs(tmp, exist_ok=True)
    for x in glob.glob(os.path.join(tmp, "cinta-ES-*")): os.remove(x)
    hechos = convertir(tmp, [archivo])
    v = pd.read_csv(os.path.join(RAIZ, "laboratorio", "dom", "ronda3", "velas", "velas-ES-m5.csv"), usecols=["t", "o", "h", "l", "c", "vol"])
    tv = pd.to_datetime(v["t"]).dt.tz_localize("UTC").dt.tz_convert(NY); v["dia"] = tv.dt.strftime("%Y-%m-%d"); v["min"] = tv.dt.hour * 60 + tv.dt.minute
    for dia, ruta in sorted(hechos.items()):
        c = pd.read_csv(ruta, usecols=["t", "primero", "ultimo", "vol"])
        t = pd.to_datetime(c["t"]).dt.tz_localize("UTC").dt.tz_convert(NY)
        mm = t.dt.hour * 60 + t.dt.minute + t.dt.second / 60
        r = c[(mm >= 570) & (mm < 960)].copy(); r["b"] = ((mm[(mm >= 570) & (mm < 960)] - 570) // 5).astype(int)
        if len(r) == 0: print("  %s: sin rueda en el archivo" % dia); continue
        g = r.groupby("b").agg(o=("primero", "first"), h=("primero", "max"), l=("primero", "min"), c=("ultimo", "last"), vol=("vol", "sum"))
        a = v[(v.dia == dia) & (v["min"] >= 570) & (v["min"] < 960)].copy(); a["b"] = (a["min"] - 570) // 5; a = a.set_index("b")
        com = g.index.intersection(a.index)
        if len(com) == 0: print("  %s: ATAS no tiene velas de 5 min de ES para ese dia" % dia); continue
        d = {k: (g.loc[com, k] - a.loc[com, k]).abs() for k in ("o", "h", "l", "c")}
        dvol = (g.loc[com, "vol"] - a.loc[com, "vol"]).abs()
        vw = (r.primero * r.vol).cumsum() / r.vol.cumsum()
        print("  %s: velas de 5 min en comun %d de 78 | dif max o/h/l/c %.2f/%.2f/%.2f/%.2f pts | velas con o/h/l/c distinto %d | volumen: dif max %d, total %d vs %d (%.3f %%) | rango rueda %.2f vs %.2f | VWAP al cierre %.4f"
              % (dia, len(com), d["o"].max(), d["h"].max(), d["l"].max(), d["c"].max(), int(sum((d[k] > 1e-9).sum() for k in d)),
                 dvol.max(), g.loc[com, "vol"].sum(), a.loc[com, "vol"].sum(), 100 * (g.loc[com, "vol"].sum() / a.loc[com, "vol"].sum() - 1),
                 r.primero.max() - r.primero.min(), a.h.max() - a.l.min(), vw.iloc[-1]))

if __name__ == "__main__":
    if len(sys.argv) >= 3 and sys.argv[1] == "--validar": validar(sys.argv[2])
    elif len(sys.argv) >= 3: convertir(sys.argv[1], sys.argv[2:])
    else: print(__doc__)
