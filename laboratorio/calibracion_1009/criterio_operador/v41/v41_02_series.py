# -*- coding: utf-8 -*-
"""v41_02_series.py — las 4 series de QQQ de la 4.1 reconstruidas MINUTO A MINUTO desde las cadenas crudas, sin mirar adelante.
SOLO LECTURA de los datos (lee fotos_qqq.pkl de v41_01_fotos.py y datos/velas_v41.csv). Escribe datos/rayas_v41.pkl.

Port de MinuteroQqq.Minuto + SeriesQqq + SeleccionFam (atas/PythiaGexCuatro/_modulos/familia), modo Corregida, contrato MNQZ6:
  clave t (inicio del minuto, UTC) -> fut = cierre de la ultima vela m2 CERRADA antes de t (CierreConocido: m2 alineadas a minutos pares;
    cierre = el de la ultima M1 de esa m2), solo velas de la sesion de t (desde las 18:00 NY), tope 4 h.
  foto = la ultima con generado <= t; vale si t < vigencia (C9); razon = la C8 de esa foto (la que anoto la 4.1 en muestras-QQQ si la
    foto esta ahi; si no, la mia de v41_01); NaN -> la 4.1 no dibuja QQQ ese minuto.
  S = fut / razon; filas del horizonte Hoy con envejecimiento (FilasHoy: env = (t - generado)/86400 en [0, 2]; vencido excluido; tope =
    max(1, mas cercano + 0,01)); gamma Black-Scholes r 0,0375 sin dividendo; GEX por lado por strike (calls +, puts -) x 100 x S^2 x 1 %.
  Fut = K x razon; R = min(2 % de fut, 100) alrededor de fut:
    MUROS_QQQ_vol  D1 = Fut[argmax GEX calls por volumen, > 0]  D2 = Fut[argmin GEX puts por volumen, < 0]
    MAJORS_QQQ_vol D1 = Fut[argmax neto por volumen, > 0]       D2 = Fut[argmin neto por volumen, < 0]
    MUROS_QQQ_oi / MAJORS_QQQ_oi: igual con el interes abierto.
  argmax/argmin = primera ocurrencia en orden de strike (estricto). La clave t es lo VIGENTE en la vela t (usa cierres anteriores a t):
  el juez se corre con desfase_min = 0 (sensibilidad: 1).
Salida: {'series': {nombre: {Timestamp minuto: {'D1': (precio, 'QQQ<K>'), 'D2': ...}}}, 'meta': DataFrame por minuto}."""
import math
import os
import sys
import time
from zoneinfo import ZoneInfo

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
SCR = os.path.join(os.environ["LOCALAPPDATA"], "Temp", "claude", "C--Users-wmx-7-OneDrive-Escritorio-ATAS-nada",
                   "f5879819-e2a4-405b-87b0-959ace090d7b", "scratchpad", "v41")
AP = os.path.join(os.environ["APPDATA"], "ATAS")
NY = ZoneInfo("America/New_York")
TASA = 0.0375
PISO_DIAS = 1.0 / 1440.0
SERIES = ("MUROS_QQQ_vol", "MAJORS_QQQ_vol", "MUROS_QQQ_oi", "MAJORS_QQQ_oi")


from v41_01_fotos import vigencia  # noqa: E402


def gamma_bs(S, K, T, iv):
    ok = (S > 0) & (K > 0) & (T > 0) & (iv > 0)
    ivs = np.where(ok, iv, 1.0); Ts = np.where(ok, T, 1.0); Ks = np.where(ok, K, 1.0)
    v = ivs * np.sqrt(Ts)
    d1 = (np.log(S / Ks) + (TASA + 0.5 * ivs * ivs) * Ts) / v
    g = np.exp(-0.5 * d1 * d1) / math.sqrt(2 * math.pi) / (S * v)
    return np.where(ok, g, 0.0)


def ini_sesion_ms(t):
    """inicio de la sesion de CME de t (18:00 NY) en ms UTC."""
    ny = pd.Timestamp(t).tz_localize("UTC").tz_convert(NY)
    d = ny.normalize()
    if ny.hour >= 18:
        ini = d + pd.Timedelta(hours=18)
    else:
        ini = d - pd.Timedelta(days=1) + pd.Timedelta(hours=18)
    return int(ini.tz_convert("UTC").tz_localize(None).value // 1_000_000)


class Cierres:
    """CierreConocido sobre velas m2 armadas con las M1 (o = primera M1, c = ultima M1 de la m2)."""

    def __init__(self, V):
        tm = (V["t"].to_numpy().astype("datetime64[s]").astype(np.int64) * 1000)
        b = (tm // 120_000) * 120_000
        df = pd.DataFrame({"b": b, "c": V["c"].to_numpy(float), "tm": tm}).sort_values("tm")
        g = df.groupby("b")["c"].last()
        self.b = g.index.to_numpy(np.int64); self.c = g.to_numpy(float)
        self.ultimo = int(tm.max()) + 60_000          # fin de la ultima M1 = 'ultimo tick' aproximado

    def __call__(self, t):
        ts = int(pd.Timestamp(t).value // 1_000_000)
        ini = ini_sesion_ms(t)
        b0 = ((ts - 120_000) // 120_000) * 120_000
        j = int(np.searchsorted(self.b, b0, side="right")) - 1
        while j >= 0:
            b = int(self.b[j])
            if b < ini:
                return float("nan")
            if ts - (b + 120_000) > 4 * 3600_000:
                return float("nan")
            if b + 120_000 <= self.ultimo:
                return float(self.c[j])
            j -= 1
        return float("nan")


def seleccion(Fut, x, fut, signo):
    r = min(0.02 * fut, 100.0)
    best = -1; bv = 0.0
    for i in range(len(Fut)):
        if not (abs(Fut[i] - fut) <= r):
            continue
        v = x[i]
        if signo > 0:
            if not (v > 0):
                continue
            if best < 0 or v > bv:
                best, bv = i, v
        else:
            if not (v < 0):
                continue
            if best < 0 or v < bv:
                best, bv = i, v
    return best


def minuto(f, t, fut):
    """-> dict serie -> {'D1': (precio, etiqueta), 'D2': ...} y meta."""
    rz = f["rz"]
    S = fut / rz
    env = max(0.0, min(2.0, (t - f["gen"]).total_seconds() / 86400.0))
    de = f["dias"] - env
    val = de[de >= 0]
    mas = float(val.min()) if len(val) else 0.0
    tope = max(1.0, mas + 0.01)
    F = f["filas"]
    v = F[:, 1].astype(int)
    ok = (v >= 0) & (v < len(de))
    d = np.where(ok, de[np.clip(v, 0, max(0, len(de) - 1))], -1.0)
    ok &= (d >= 0) & (d <= tope)
    # solo hacen falta los strikes a <= R de fut (el filtro de 3 % de la 4.1 es un superconjunto)
    K = F[:, 0]
    ok &= np.abs(K * rz - fut) <= min(0.02 * fut, 100.0) + 1e-9
    if not ok.any():
        return {}, dict(S=S, rz=rz, env=env, tope=tope, n=0)
    F = F[ok]; d = d[ok]
    K = F[:, 0]; T = np.maximum(d, PISO_DIAS) / 365.0
    gc = gamma_bs(S, K, T, F[:, 4]); gp = gamma_bs(S, K, T, F[:, 5])
    esc = 100.0 * S * S * 0.01
    avc = gc * F[:, 6] * esc; avp = -gp * F[:, 7] * esc; aoc = gc * F[:, 2] * esc; aop = -gp * F[:, 3] * esc
    aporta = (avc + avp != 0) | (aoc + aop != 0)
    if not aporta.any():
        return {}, dict(S=S, rz=rz, env=env, tope=tope, n=0)
    ks, inv = np.unique(K[aporta], return_inverse=True)
    sm = lambda x: np.bincount(inv, x[aporta], len(ks))
    GvC, GvP, GoC, GoP = sm(avc), sm(avp), sm(aoc), sm(aop)
    Gv = GvC + GvP; Go = GoC + GoP
    Fut = ks * rz
    out = {}
    for nom, (A, B) in (("MUROS_QQQ_vol", (GvC, GvP)), ("MAJORS_QQQ_vol", (Gv, Gv)), ("MUROS_QQQ_oi", (GoC, GoP)), ("MAJORS_QQQ_oi", (Go, Go))):
        s = {}
        i = seleccion(Fut, A, fut, +1)
        if i >= 0:
            s["D1"] = (float(Fut[i]), "QQQ%g" % ks[i])
        j = seleccion(Fut, B, fut, -1)
        if j >= 0:
            s["D2"] = (float(Fut[j]), "QQQ%g" % ks[j])
        out[nom] = s
    return out, dict(S=S, rz=rz, env=env, tope=tope, n=len(ks))


def main():
    t0 = time.time()
    V = pd.read_csv(os.path.join(AQUI, "datos", "velas_v41.csv"), parse_dates=["t"])
    fotos = pd.read_pickle(os.path.join(SCR, "fotos_qqq.pkl"))
    # razon: la que anoto la 4.1 para esa foto (si la tiene); si no, la C8 reconstruida
    import glob, json
    m41 = {}
    for p in glob.glob(os.path.join(AP, "PythiaGex4", "familia", "muestras-QQQ-*.jsonl")):
        for l in open(p, encoding="utf-8"):
            if l.strip():
                r = json.loads(l); m41[pd.Timestamp(r["ts"])] = r
    n41 = 0
    for f in fotos:
        r = m41.get(f["ts"])
        if r is not None and r.get("razon") is not None:
            f["rz"] = float(r["razon"]); f["rz_origen"] = "4.1"; n41 += 1
        else:
            f["rz"] = f["razon"]; f["rz_origen"] = "C8 reconstruida"
    print("fotos %d; razon de la 4.1 en %d, reconstruida en el resto" % (len(fotos), n41))
    # desde la primera foto que proceso la 4.1 (su historia arranca el 02-10), solo SUS fotos (las de las otras carpetas no las vio)
    corte = min(pd.Timestamp(r["generado"]).tz_localize(None) if pd.Timestamp(r["generado"]).tzinfo else pd.Timestamp(r["generado"])
                for r in m41.values())
    antes = len(fotos)
    fotos = [f for f in fotos if f["gen"] < corte or f["ts"] in m41]
    # y con SU 'generado' (la dedup de v41_01 se queda con la bajada mas temprana de cualquier carpeta; la 4.1 usa la suya)
    ngen = 0
    for f in fotos:
        r = m41.get(f["ts"])
        if r is not None:
            g = pd.Timestamp(r["generado"])
            g = g.tz_convert("UTC").tz_localize(None) if g.tzinfo else g
            if g != f["gen"]:
                ngen += 1
                f = f.update(gen=g, vig=vigencia(g, f["congelada"]))
    fotos.sort(key=lambda f: (f["gen"], f["ts"]))
    print("desde %s solo las fotos de la 4.1: quedan %d de %d (generado de la 4.1 en %d)" % (corte, len(fotos), antes, ngen))
    gen = np.array([f["gen"].value for f in fotos], np.int64)
    CC = Cierres(V)
    # grilla: toda sesion con velas, de 22:00Z (18:00 NY) a 13:30Z del dia de la sesion
    ses = sorted({(t + pd.Timedelta(hours=2)).strftime("%Y-%m-%d") for t in V["t"]})
    series = {s: {} for s in SERIES}
    meta = []
    for s in ses:
        N = pd.Timestamp(s)
        a = N - pd.Timedelta(hours=2); b = N + pd.Timedelta(hours=13, minutes=30)
        tlast = min(b, V["t"].max() + pd.Timedelta(minutes=1))
        for t in pd.date_range(a, tlast, freq="1min", inclusive="left"):
            fut = CC(t)
            k = int(np.searchsorted(gen, t.value, side="right")) - 1
            m = dict(t=t, sesion=s, fut=fut, foto=None, motivo="")
            res = {}
            if fut != fut:
                m["motivo"] = "sin fut"
            elif k < 0:
                m["motivo"] = "sin foto"
            else:
                f = fotos[k]
                m["foto"] = f["gen"]; m["rz_origen"] = f["rz_origen"]
                if t >= f["vig"]:
                    m["motivo"] = "foto vencida (C9)"
                elif not (f["rz"] == f["rz"]):
                    m["motivo"] = "sin razon"
                else:
                    res, mm = minuto(f, t, fut)
                    m.update(mm)
                    m["motivo"] = "ok" if res else "sin strikes"
            for nom in SERIES:
                series[nom][t] = res.get(nom, {})
            meta.append(m)
    M = pd.DataFrame(meta)
    print("minutos %d en %d sesiones; motivos:\n%s" % (len(M), len(ses), M["motivo"].value_counts().to_string()))
    print(M.groupby("sesion")["motivo"].apply(lambda x: (x == "ok").sum()).to_string())
    pd.to_pickle({"series": series, "meta": M}, os.path.join(AQUI, "datos", "rayas_v41.pkl"))
    print("listo %.0f s" % (time.time() - t0))


if __name__ == "__main__":
    main()
