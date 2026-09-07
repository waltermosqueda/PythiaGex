# -*- coding: utf-8 -*-
"""DE DATABENTO (OPRA) A LAS CADENAS QUE LEE GAMMA HOY, MINUTO A MINUTO.

Databento vende la historia completa de las opciones de SPX/SPXW: interes
abierto del dia (statistics, stat_type 9: coincide 100 % con CBOE, medido el
2026-09-03 en 28.130 contratos), volumen por contrato y minuto (ohlcv-1m: al
mismo corte, 82 % de los contratos igual exacto que CBOE y la suma 0,91) y
la definicion de cada contrato (strike, vencimiento, call/put).

Este script arma, para cada minuto de la rueda, la misma linea que guarda
archivar_cadena.py: {"generado", "base", ..., "cadena": {"ts", "spot_idx",
"vencimientos", "filas": [[strike, venc, oi_call, oi_put, iv_call, iv_put,
vol_call, vol_put], ...]}}. El simulador (atas/Rebobina) las consume con el
MISMO lector que usa el indicador dentro de ATAS.

Lo que se asume, dicho de frente:
- La IV de cada contrato sale de su ULTIMO precio operado del dia (Black-
  Scholes invertido con S = spot). Los strikes sin operar toman la IV
  interpolada de sus vecinos del mismo vencimiento (en strike), y si el
  vencimiento entero no opero, la del vencimiento mas cercano. Para el GEX
  por volumen eso es exacto donde importa; para el de OI es aproximado.
- El spot del indice = futuro ESU6 (cierre del minuto) - base. La base se
  toma del archivo propio del dia si existe (medida por paridad), si no de
  --base.
- El retraso: "generado" = minuto t, y la cadena lleva el volumen hasta
  t - retraso (por defecto 902 s, lo que tarda CBOE). Con --retraso 0 se
  simula el dato al instante: la diferencia entre los dos es lo que cuesta
  el retraso, medido.

Uso:
  python herramientas/databento_a_cadenas.py 2026-09-03 [--retraso 902] [--base 6.1] [--cada 1]
"""
import argparse
import datetime as dt
import glob
import gzip
import io
import json
import math
import os
import sys

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, RAIZ)
D = os.path.join(RAIZ, "datos", "databento")
SALIDA = os.path.join(RAIZ, "datos", "simulador", "cadenas")
VELAS = os.path.join(RAIZ, "datos", "simulador", "velas")
DIAS_MAX = 8.0
TASA = 0.0375


def carga(pat):
    import databento as db
    ps = glob.glob(pat)
    if not ps:
        raise SystemExit("falta " + pat + " (bajar con herramientas/databento_bajar.py)")
    return db.DBNStore.from_file(ps[0]).to_df()


# ---------------------------------------------------------------- Black-Scholes
def _N(x):
    return 0.5 * (1.0 + math.erf(x / math.sqrt(2.0)))


def precio_bs(S, K, T, r, iv, call):
    if T <= 0 or iv <= 0:
        return max(0.0, (S - K) if call else (K - S))
    v = iv * math.sqrt(T)
    d1 = (math.log(S / K) + (r + 0.5 * iv * iv) * T) / v
    d2 = d1 - v
    if call:
        return S * _N(d1) - K * math.exp(-r * T) * _N(d2)
    return K * math.exp(-r * T) * _N(-d2) - S * _N(-d1)


def iv_de(precio, S, K, T, r, call):
    """Biseccion: robusta y suficiente (60 pasos)."""
    if T <= 0 or precio <= 0:
        return None
    intr = max(0.0, (S - K) if call else (K - S))
    if precio <= intr + 1e-9:
        return None
    lo, hi = 1e-4, 5.0
    if precio_bs(S, K, T, r, hi, call) < precio:
        return None
    for _ in range(60):
        m = 0.5 * (lo + hi)
        if precio_bs(S, K, T, r, m, call) > precio:
            hi = m
        else:
            lo = m
    return 0.5 * (lo + hi)


# ---------------------------------------------------------------- vencimientos
def hora_vencimiento(exp_date, asset):
    """SPXW liquida al cierre (16:15 ET); SPX mensual, a la apertura (09:30 ET)."""
    # horario de verano en septiembre: ET = UTC-4
    h = dt.time(13, 30) if asset == "SPX" else dt.time(20, 15)
    return dt.datetime.combine(exp_date, h, tzinfo=dt.timezone.utc)


def base_del_dia(dia, base_manual):
    """La base medida ese dia por radar.py, si el archivo propio la tiene."""
    for pat in (os.path.join(RAIZ, "datos", "historico", "cadena-ES-%s.jsonl.gz" % dia),
                os.path.join(os.environ.get("APPDATA", ""), "ATAS", "PythiaGex", "cadenas", "local-ES-%s.jsonl" % dia)):
        if not os.path.exists(pat):
            continue
        op = gzip.open if pat.endswith(".gz") else io.open
        bases = []
        with op(pat, "rt", encoding="utf-8") as f:
            for l in f:
                try:
                    d = json.loads(l)
                except Exception:
                    continue
                if d.get("base_confiable") and d.get("base"):
                    bases.append((d["generado"], float(d["base"])))
        if bases:
            return bases, "medida ese dia (%d corridas)" % len(bases)
    # las fotos crudas del cache: radar mide la base al vuelo
    fotos = sorted(glob.glob(os.path.join(RAIZ, "datos", "cache", "_SPX-%s-*.json.gz" % dia.replace("-", ""))))
    if fotos:
        try:
            import radar
            bases = []
            for p in fotos[::max(1, len(fotos) // 40)]:
                try:
                    crudo = radar.leer_cache(p)
                    b = radar.medir_base(crudo)
                    if b.get("confiable") and b.get("base"):
                        s = radar._sello(p).replace(tzinfo=dt.timezone.utc).isoformat(timespec="seconds")
                        bases.append((s, float(b["base"])))
                except Exception:
                    pass
            if bases:
                return bases, "medida ese dia en las fotos crudas (%d)" % len(bases)
        except Exception as e:
            print("no pude medir la base con radar:", e)
    return [(None, base_manual)], "manual %.2f" % base_manual


def base_en(bases, t_iso):
    """La base medida mas reciente a esa hora; si no hay ninguna anterior, la primera."""
    ult = None
    for s, b in bases:
        if s is None or s <= t_iso:
            ult = b
        else:
            break
    return ult if ult is not None else bases[0][1]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("dia")
    ap.add_argument("--retraso", type=int, default=902, help="segundos de retraso del volumen (CBOE: 902)")
    ap.add_argument("--base", type=float, default=6.1, help="base ES-SPX si no hay medida ese dia")
    ap.add_argument("--cada", type=int, default=1, help="minutos entre cadenas")
    ap.add_argument("--desde", default="13:30", help="UTC")
    ap.add_argument("--hasta", default="20:59", help="UTC")
    ap.add_argument("--tasa", type=float, default=TASA)
    a = ap.parse_args()
    dia = a.dia
    d0 = dt.date.fromisoformat(dia)

    import pandas as pd
    dfn = carga(os.path.join(D, "OPRA_PILLAR", "definition", "SPX_OPT+SPXW_OPT-%s-*.dbn.zst" % dia))
    st = carga(os.path.join(D, "OPRA_PILLAR", "statistics", "SPX_OPT+SPXW_OPT-%s-*.dbn.zst" % dia))
    oh = carga(os.path.join(D, "OPRA_PILLAR", "ohlcv-1m", "SPX_OPT+SPXW_OPT-%s-*.dbn.zst" % dia))
    fut = carga(os.path.join(D, "GLBX_MDP3", "ohlcv-1m", "ES_FUT-*.dbn.zst"))

    # el futuro vigente ese dia: el ES con mas volumen en la sesion
    fdia = fut[(fut.index >= pd.Timestamp(dia, tz="UTC")) & (fut.index < pd.Timestamp(d0 + dt.timedelta(days=1), tz="UTC"))]
    fdia = fdia[~fdia["symbol"].str.contains("-")]
    contrato = fdia.groupby("symbol")["volume"].sum().idxmax()
    fdia = fdia[fdia.symbol == contrato]
    cierre_min = fdia["close"].to_dict()   # ts -> cierre
    print("futuro %s: %d velas de 1 min ese dia" % (contrato, len(fdia)))

    # contratos: id -> (strike, venc_idx, es_call)
    dfn = dfn.drop_duplicates("instrument_id")
    vencs = {}
    for r in dfn.itertuples():
        e = r.expiration.date()
        vencs.setdefault(e, hora_vencimiento(e, r.asset))
    vlist = sorted(vencs)
    vidx = {e: i for i, e in enumerate(vlist)}
    info = {}
    for r in dfn.itertuples():
        info[r.instrument_id] = (float(r.strike_price), vidx[r.expiration.date()], r.instrument_class == "C")

    # OI del dia (publicado ~10:30 UTC, es el de AYER): stat_type 9, ultimo por contrato
    oi = st[st.stat_type == 9].drop_duplicates("instrument_id", keep="last").set_index("instrument_id")["quantity"].to_dict()
    print("OI: %d contratos con interes abierto; %d vencimientos en la definicion" % (len(oi), len(vlist)))

    # volumen y ultimo precio por contrato, acumulados minuto a minuto
    oh = oh.sort_index()
    bases, origen_base = base_del_dia(dia, a.base)
    print("base:", origen_base)

    os.makedirs(SALIDA, exist_ok=True)
    os.makedirs(VELAS, exist_ok=True)
    salida = os.path.join(SALIDA, "sim-ES-%s-r%d.jsonl.gz" % (dia, a.retraso))
    if os.path.exists(salida):
        os.remove(salida)
    t = dt.datetime.combine(d0, dt.time.fromisoformat(a.desde), tzinfo=dt.timezone.utc)
    fin = dt.datetime.combine(d0, dt.time.fromisoformat(a.hasta), tzinfo=dt.timezone.utc)
    paso = dt.timedelta(minutes=a.cada)
    vol = {}      # id -> [vol_acum, ultimo_precio, ts_ultimo]
    cursor = 0
    filas_ts = oh.index.to_pydatetime()
    recs = oh[["instrument_id", "close", "volume"]].to_numpy()
    escritas = 0
    with open(salida, "ab") as fout:
        while t <= fin:
            corte = t - dt.timedelta(seconds=a.retraso)      # hasta donde llega el dato (CBOE llega tarde)
            while cursor < len(recs) and filas_ts[cursor] < corte:
                iid, close, v = recs[cursor]
                e = vol.get(iid)
                if e is None:
                    vol[iid] = [float(v), float(close), filas_ts[cursor]]
                else:
                    e[0] += float(v); e[1] = float(close); e[2] = filas_ts[cursor]
                cursor += 1
            ts_fut = pd.Timestamp(t.replace(second=0)) - pd.Timedelta(minutes=1)
            S_fut = cierre_min.get(ts_fut)
            if S_fut is None:
                t += paso
                continue
            base = base_en(bases, t.isoformat(timespec="seconds"))
            S = float(S_fut) - base
            # por (strike, venc): oi/vol/iv de call y put
            por = {}
            for iid, (K, vi, call) in info.items():
                dias = (vencs[vlist[vi]] - t).total_seconds() / 86400.0
                if dias < 0 or dias > DIAS_MAX:
                    continue
                o = float(oi.get(iid, 0.0))
                e = vol.get(iid)
                v = e[0] if e else 0.0
                if o == 0 and v == 0:
                    continue
                f = por.setdefault((K, vi), [K, vi, 0.0, 0.0, None, None, 0.0, 0.0])
                if call:
                    f[2] = o; f[6] = v
                else:
                    f[3] = o; f[7] = v
                if e:
                    T = max(dias, 1.0 / 1440.0) / 365.0
                    iv = iv_de(e[1], S, K, T, a.tasa, call)
                    if iv:
                        f[4 if call else 5] = round(iv, 4)
            # IV faltante: interpolar por strike dentro del vencimiento; si no hay, vecino
            por_v = {}
            for (K, vi), f in por.items():
                por_v.setdefault(vi, []).append(f)
            iv_media = {}
            for vi, fs in por_v.items():
                fs.sort(key=lambda z: z[0])
                for col in (4, 5):
                    con = [(z[0], z[col]) for z in fs if z[col]]
                    if not con:
                        continue
                    iv_media.setdefault(vi, []).extend(x[1] for x in con)
                    for z in fs:
                        if z[col]:
                            continue
                        # vecino mas cercano por strike (lineal entre los dos que lo rodean)
                        izq = [c for c in con if c[0] <= z[0]]
                        der = [c for c in con if c[0] >= z[0]]
                        if izq and der:
                            (k1, v1), (k2, v2) = izq[-1], der[0]
                            z[col] = round(v1 if k1 == k2 else v1 + (v2 - v1) * (z[0] - k1) / (k2 - k1), 4)
                        elif izq:
                            z[col] = izq[-1][1]
                        else:
                            z[col] = der[0][1]
            for vi, fs in por_v.items():
                for z in fs:
                    for col in (4, 5):
                        if not z[col]:
                            # ningun contrato de este vencimiento opero todavia: la IV media del vecino
                            vec = sorted(iv_media, key=lambda w: abs(w - vi))
                            z[col] = round(sum(iv_media[vec[0]]) / len(iv_media[vec[0]]), 4) if vec else 0.15
            filas = [[f[0], f[1], f[2], f[3], f[4], f[5], f[6], f[7]] for f in sorted(por.values(), key=lambda z: (z[0], z[1]))]
            linea = {
                "generado": t.isoformat(timespec="seconds"),
                "cadena_ts": corte.strftime("%Y-%m-%d %H:%M:%S"),
                "edad_min": round(a.retraso / 60.0, 1), "retraso_s": a.retraso,
                "spot": round(S, 2), "base": round(base, 4), "base_confiable": True, "base_cruda": round(base, 4),
                "base_error_ticks": 0, "contrato": contrato, "fuente": "databento OPRA %s" % dia,
                "cadena": {"ts": corte.strftime("%Y-%m-%d %H:%M:%S"), "spot_idx": round(S, 2),
                           "campos": "strike,venc,oi_call,oi_put,iv_call,iv_put,vol_call,vol_put",
                           "vencimientos": [{"f": e.isoformat(), "dias": round((vencs[e] - t).total_seconds() / 86400.0, 4)} for e in vlist],
                           "filas": filas, "n_filas": len(filas), "horizonte_dias": DIAS_MAX,
                           "dias_max_archivo": DIAS_MAX},
            }
            fout.write(gzip.compress((json.dumps(linea, separators=(",", ":")) + "\n").encode("utf-8")))
            escritas += 1
            if escritas % 60 == 0:
                print("  %s  %d filas  S=%.2f  fut=%.2f  vol acumulado %d" % (t.strftime("%H:%M"), len(filas), S, S_fut, int(sum(e[0] for e in vol.values()))))
            t += paso
    print("listo: %d cadenas en %s (%.1f MB)" % (escritas, salida, os.path.getsize(salida) / 1e6))

    # las velas del futuro, en CSV, para el simulador (toda la descarga, no solo el dia)
    todo = fut[fut.symbol == contrato][["open", "high", "low", "close", "volume"]]
    pv = os.path.join(VELAS, "%s-1m.csv" % contrato)
    todo.to_csv(pv, date_format="%Y-%m-%dT%H:%M:%SZ")
    print("velas: %d de 1 min de %s en %s" % (len(todo), contrato, pv))


if __name__ == "__main__":
    main()
