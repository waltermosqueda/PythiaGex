# -*- coding: utf-8 -*-
"""v41_01_fotos.py — fotos de la cadena QQQ de CBOE + conversion C8 de la 4.1 (razon SINCRONIZADA), foto por foto. SOLO LECTURA.

Fotos: cadena-QQQ-*.jsonl.gz de PythiaGex/cadenas, PythiaGex2/cadenas, PythiaGex/cboe-local, PythiaGex4/cboe y local-QQQ-*.jsonl de
  PythiaGex/cadenas (las carpetas de libros.fotos_cboe del laboratorio + la de la 4.1). Dedup por cadena.ts: gana la bajada MAS TEMPRANA
  (generado). Se descartan las de la pausa de CME (generado en (17:00, 18:00) NY, ConversionQqq.FueraDeSesion, modo Corregida).
C8 (port de ConversionQqq.Observar / backtest_familia.conversion rama QQQ):
  vivo   = alguna foto de los 600 s previos (por generado) con otro spot_idx
  precio = MNQZ6 en (ts - 900 s): 1) el 'precio' que anoto la propia 4.1 en muestras-QQQ-*.jsonl para ese ts (exacto, sesiones 10-02 y
           10-05..10-09); 2) la cinta por segundo de la 4.1 (seg-MNQZ6-*.bin: ultimo tick <= ts si es de <= 120 s); 3) la vela M1 que contiene
           el instante, interpolada apertura -> cierre (APROXIMACION: la 4.1 interpola la vela m2 de ticks; ver la validacion).
  muestra = precio / spot (solo vivo); razon = mediana robusta (3 MAD, piso 0,0002 x mediana) de las ultimas 24 muestras, >= 5; una foto
           sin muestra hereda la de las ultimas 24 sin tope de tiempo. Contrato del grafico fijo (MNQZ6): no hay reinicio por roll.
  congelada = ultimo_trade NY >= 15:59; vigencia C9 = 25 min desde generado, o (congelada) hasta las 09:30 NY siguientes.
Guarda en el scratchpad (cache, no son datos del proyecto) fotos_qqq.pkl y escribe datos/c8_validacion.txt."""
import glob
import gzip
import json
import os
import re
import struct
import sys
import time
from datetime import timezone
from zoneinfo import ZoneInfo

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
AP = os.path.join(os.environ["APPDATA"], "ATAS")
SCR = os.path.join(os.environ["LOCALAPPDATA"], "Temp", "claude", "C--Users-wmx-7-OneDrive-Escritorio-ATAS-nada",
                   "f5879819-e2a4-405b-87b0-959ace090d7b", "scratchpad", "v41")
NY = ZoneInfo("America/New_York")
CARPETAS = [os.path.join(AP, "PythiaGex", "cadenas"), os.path.join(AP, "PythiaGex2", "cadenas"), os.path.join(AP, "PythiaGex", "cboe-local"),
            os.path.join(AP, "PythiaGex4", "cboe")]
RX_TS = re.compile(r'"cadena_ts"\s*:\s*"([^"]+)"')
RX_GEN = re.compile(r'"generado"\s*:\s*"([^"]+)"')


def utc_naive(s):
    t = pd.Timestamp(s.replace("\\u002B", "+").replace("\\u002b", "+"))
    if t.tzinfo is not None:
        t = t.tz_convert("UTC").tz_localize(None)
    return t.floor("s")


def ny_a_utc(s):
    t = pd.Timestamp(s)
    return t.tz_localize(NY).tz_convert("UTC").tz_localize(None)


def a_ny(t):
    return pd.Timestamp(t).tz_localize("UTC").tz_convert(NY).tz_localize(None)


def cargar_fotos():
    archivos = []
    for c in CARPETAS:
        archivos += [(p, os.path.basename(c)) for p in sorted(glob.glob(os.path.join(c, "cadena-QQQ-*.jsonl.gz")))]
    archivos += [(p, "local") for p in sorted(glob.glob(os.path.join(AP, "PythiaGex", "cadenas", "local-QQQ-*.jsonl")))]
    vistos = {}
    nlin = 0
    for p, src in archivos:
        op = gzip.open if p.endswith(".gz") else open
        try:
            with op(p, "rt", encoding="utf-8", errors="replace") as fh:
                for l in fh:
                    nlin += 1
                    cab = l[:600]
                    m = RX_TS.search(cab); g = RX_GEN.search(cab)
                    if not m or not g:
                        continue
                    ts = m.group(1)
                    try:
                        gen = utc_naive(g.group(1))
                    except Exception:
                        continue
                    if ts in vistos and vistos[ts]["gen"] <= gen:
                        continue
                    try:
                        j = json.loads(l)
                    except Exception:
                        continue
                    c = j.get("cadena") or {}
                    filas = [x[:8] for x in c.get("filas", []) if len(x) >= 8]
                    if not filas:
                        continue
                    ut = c.get("ultimo_trade")
                    vistos[ts] = dict(gen=gen, ts=pd.Timestamp(ts).floor("s"), spot=float(c.get("spot_idx") or j.get("spot") or 0),
                                      ut=ut, dato=ny_a_utc(ut) if ut else pd.NaT, src=src,
                                      dias=np.array([float(v.get("dias") or 0) for v in c.get("vencimientos", [])], float),
                                      fvenc=[v.get("f") for v in c.get("vencimientos", [])],
                                      filas=np.array(filas, float))
        except (OSError, EOFError) as e:
            print("  aviso: %s ilegible (%s)" % (os.path.basename(p), e))
    print("lineas leidas %d, fotos unicas por cadena.ts %d" % (nlin, len(vistos)))
    fotos = sorted(vistos.values(), key=lambda f: (f["gen"], f["ts"]))
    fuera = []
    out = []
    for f in fotos:
        ny = a_ny(f["gen"])
        sec = ny.hour * 3600 + ny.minute * 60 + ny.second
        if 17 * 3600 < sec < 18 * 3600:
            fuera.append(f); continue
        out.append(f)
    print("descartadas por la pausa de CME (17-18 NY): %d; quedan %d" % (len(fuera), len(out)))
    return out


def leer_seg(p):
    b = open(p, "rb").read(); i = 0

    def rstr():
        nonlocal i
        n = 0; s = 0
        while True:
            x = b[i]; i += 1; n |= (x & 0x7F) << s; s += 7
            if x < 0x80:
                break
        v = b[i:i + n].decode("utf-8"); i += n
        return v
    rstr(); i += 4; rstr(); i += 8
    n = struct.unpack_from("<i", b, i)[0]; i += 4
    arr = np.frombuffer(b, dtype=np.dtype([("k", "<i4"), ("t", "<i8"), ("p", "<f8")]), count=n, offset=i)
    return arr["t"].astype(np.int64), arr["p"].astype(float)


class Precio:
    def __init__(self, velas):
        tt, pp = [], []
        for p in sorted(glob.glob(os.path.join(AP, "PythiaGex4", "cinta", "seg-MNQZ6-????-??-??.bin"))):
            t, x = leer_seg(p); tt.append(t); pp.append(x)
        if tt:
            t = np.concatenate(tt); x = np.concatenate(pp)
            o = np.argsort(t, kind="stable"); self.st = t[o]; self.sp = x[o]
        else:
            self.st = np.array([], np.int64); self.sp = np.array([])
        self.vt = velas["t"].to_numpy().astype("datetime64[s]").astype(np.int64)
        self.vo = velas["o"].to_numpy(float); self.vc = velas["c"].to_numpy(float)
        self.sembrado = {}
        for p in glob.glob(os.path.join(AP, "PythiaGex4", "familia", "muestras-QQQ-*.jsonl")):
            for l in open(p, encoding="utf-8"):
                if not l.strip():
                    continue
                r = json.loads(l)
                if r.get("precio") is not None:
                    self.sembrado[pd.Timestamp(r["ts"])] = (float(r["precio"]), r.get("razon"), r.get("muestra"), r.get("vivo"), r.get("n"))
        self.cuenta = {"muestras41": 0, "cinta": 0, "vela": 0, "nada": 0}

    def __call__(self, ts):
        if ts + pd.Timedelta(seconds=900) in self.sembrado:
            self.cuenta["muestras41"] += 1
            return self.sembrado[ts + pd.Timedelta(seconds=900)][0], "muestras41"
        ms = int(ts.value // 1_000_000)
        if len(self.st) and self.st[0] <= ms <= self.st[-1]:
            j = int(np.searchsorted(self.st, ms, side="right")) - 1
            if j >= 0 and ms - self.st[j] <= 120_000:
                self.cuenta["cinta"] += 1
                return float(self.sp[j]), "cinta"
        s = int(ts.value // 1_000_000_000)
        i = int(np.searchsorted(self.vt, s, side="right")) - 1
        if i >= 0 and 0 <= s - self.vt[i] < 60:
            self.cuenta["vela"] += 1
            return float(self.vo[i] + (self.vc[i] - self.vo[i]) * (s - self.vt[i]) / 60.0), "vela"
        self.cuenta["nada"] += 1
        return float("nan"), "nada"


def robusta(x, piso):
    x = np.asarray(x, float)
    if len(x) == 0:
        return float("nan"), 0
    med = float(np.median(x)); mad = float(np.median(np.abs(x - med)))
    b = x[np.abs(x - med) <= max(3.0 * mad, piso)]
    return (float(np.median(b)), len(b)) if len(b) else (med, len(x))


def vigencia(gen, congelada):
    h1 = gen + pd.Timedelta(seconds=1500)
    if not congelada:
        return h1
    ny = a_ny(gen)
    prox = ny_a_utc(ny.normalize() + pd.Timedelta(hours=9, minutes=30))
    if prox <= gen:
        prox = ny_a_utc(ny.normalize() + pd.Timedelta(days=1, hours=9, minutes=30))
    return max(prox, h1)


def c8(fotos, PR):
    recientes = []; roll = []
    for f in fotos:
        gen = f["gen"]; ts = f["ts"]
        precio, origen = PR(ts - pd.Timedelta(seconds=900))
        vivo = False
        for g, s in reversed(recientes):
            if (gen - g).total_seconds() > 600:
                break
            if abs(s - f["spot"]) > 1e-9:
                vivo = True; break
        recientes.append((gen, f["spot"]))
        while len(recientes) > 1 and (gen - recientes[0][0]).total_seconds() > 600:
            recientes.pop(0)
        val = precio / f["spot"] if (vivo and precio == precio and f["spot"] > 0) else float("nan")
        if val == val:
            roll.append(val); roll = roll[-24:]
        r, n = robusta(roll, 0.0002 * (float(np.median(roll)) if roll else 0.0))
        f.update(precio=precio, origen=origen, vivo=vivo, muestra=val, razon=r if n >= 5 else float("nan"), razon_n=n)
        dny = a_ny(f["dato"]) if f["dato"] is not pd.NaT and f["dato"] == f["dato"] else None
        f["congelada"] = bool(dny is not None and dny.hour * 60 + dny.minute >= 15 * 60 + 59)
        f["vig"] = vigencia(gen, f["congelada"])


def main():
    t0 = time.time()
    os.makedirs(SCR, exist_ok=True)
    velas = pd.read_csv(os.path.join(AQUI, "datos", "velas_v41.csv"), parse_dates=["t"])
    fotos = cargar_fotos()
    print("carga %.0f s" % (time.time() - t0))
    PR = Precio(velas)
    c8(fotos, PR)
    print("origen del precio de las muestras:", PR.cuenta)
    # validacion contra lo que anoto la 4.1 (muestras-QQQ-*.jsonl): misma foto (ts) -> misma razon?
    lin = []
    m41 = {}
    for p in sorted(glob.glob(os.path.join(AP, "PythiaGex4", "familia", "muestras-QQQ-*.jsonl"))):
        for l in open(p, encoding="utf-8"):
            if l.strip():
                r = json.loads(l); m41[pd.Timestamp(r["ts"])] = r
    por_ts = {f["ts"]: f for f in fotos}
    comun = [k for k in m41 if k in por_ts]
    d = np.array([por_ts[k]["razon"] - (m41[k]["razon"] if m41[k]["razon"] is not None else np.nan) for k in comun], float)
    ok = np.isfinite(d)
    lin.append("VALIDACION C8 contra la 4.1 (muestras-QQQ-*.jsonl, %d fotos de la 4.1; %d con el mismo ts en mi set; %d con razon en los dos)" % (
        len(m41), len(comun), int(ok.sum())))
    if ok.any():
        ad = np.abs(d[ok])
        lin.append("  |dif razon|: mediana %.6f p95 %.6f max %.6f  (x 750 = pts de NQ: mediana %.2f p95 %.2f max %.2f); iguales a 1e-9: %.1f %%" % (
            np.median(ad), np.percentile(ad, 95), ad.max(), 750 * np.median(ad), 750 * np.percentile(ad, 95), 750 * ad.max(),
            100 * (ad < 1e-9).mean()))
    solo41 = sorted(k for k in m41 if k not in por_ts)
    lin.append("  fotos de la 4.1 que NO estan en mi set: %d %s" % (len(solo41), [str(x) for x in solo41[:5]]))
    # por sesion: la razon de la noche congelada (primera foto con generado >= 00:35Z) mia contra la de la 4.1
    ses = sorted({(f["gen"] + pd.Timedelta(hours=2)).strftime("%Y-%m-%d") for f in fotos})
    lin.append("RAZON DE LA NOCHE CONGELADA por sesion (mi C8 en la primera foto con generado >= 00:35Z; 4.1 si hay):")
    for s in ses:
        N = pd.Timestamp(s)
        fn = [f for f in fotos if N + pd.Timedelta(minutes=35) <= f["gen"] < N + pd.Timedelta(hours=8, minutes=30)]
        if not fn:
            continue
        f = fn[0]
        r41 = m41.get(f["ts"], {}).get("razon")
        orig = {}
        for g in fotos:
            if N - pd.Timedelta(hours=12) <= g["gen"] <= f["gen"] and g["muestra"] == g["muestra"]:
                orig[g["origen"]] = orig.get(g["origen"], 0) + 1
        lin.append("  sesion %s foto %s spot %.2f congelada %s vig %s razon %.5f (n %d) | 4.1 %s | origen de las muestras 12 h previas %s" % (
            s, f["gen"], f["spot"], f["congelada"], f["vig"], f["razon"], f["razon_n"], ("%.5f" % r41) if r41 else "-", orig))
    txt = "\n".join(lin)
    print(txt)
    open(os.path.join(AQUI, "datos", "c8_validacion.txt"), "w", encoding="utf-8").write(txt + "\n")
    pd.to_pickle(fotos, os.path.join(SCR, "fotos_qqq.pkl"))
    print("listo %.0f s, %d fotos -> %s" % (time.time() - t0, len(fotos), os.path.join(SCR, "fotos_qqq.pkl")))


if __name__ == "__main__":
    main()
