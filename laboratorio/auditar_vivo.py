# -*- coding: utf-8 -*-
"""AUDITORIA EN VIVO DE GAMMA HOY: rehacer a mano lo que dibuja y compararlo con su AUDIT.

Toma la ultima cadena archivada de la raiz y rehace, con la misma regla del
nucleo pero en codigo aparte: perfil por strike (GEX vol/OI, convexidad),
neto, zero gamma de cada libro (grilla +-3 %, 60 pasos, cruce interpolado),
majors, dominantes (la mas fuerte de cada lado dentro del radio, centroide
gamma-ponderado a +-12 pts), pico/mucho/convexidad en el precio (cuadrante),
y Max Change contra las cadenas de 1/5/30 minutos antes. Imprime todo en
STRIKE (K) y en futuro con la base que se le diga, y al lado lo que el
indicador anoto en su ultima linea AUDIT (convertido a K con SU base), asi la
comparacion no depende de la base.

Uso: python laboratorio/auditar_vivo.py NQ --audit "<linea AUDIT>" [--base 28.7] [--fut 29385]
"""
import gzip
import glob
import json
import math
import os
import re
import sys
from datetime import datetime, timezone, timedelta

CARPETA = os.path.join(os.environ.get("APPDATA", ""), "ATAS", "PythiaGex", "cadenas")
MULT, TASA, PISO = 100.0, 0.0375, 1.0 / 1440.0
RADIO_DOM_PCT, RADIO_CENTRO, PICO_PCT, MUCHO_PCT = 2.0, 12.0, 0.35, 50


def fi(x):
    return math.exp(-0.5 * x * x) / math.sqrt(2 * math.pi)


def gbs(S, K, T, iv, r=TASA):
    if iv <= 0 or T <= 0 or S <= 0 or K <= 0:
        return 0.0
    v = iv * math.sqrt(T)
    d1 = (math.log(S / K) + (r + 0.5 * iv * iv) * T) / v
    return fi(d1) / (S * v)


def cadenas(raiz, n=40):
    """Las ultimas n cadenas distintas (ts) de los archivos del dia."""
    out = {}
    for p in sorted(glob.glob(os.path.join(CARPETA, "cadena-%s-*.jsonl.gz" % raiz)))[-2:]:
        with gzip.open(p, "rt", encoding="utf-8") as f:
            for l in f:
                if len(l) < 100:
                    continue
                try:
                    d = json.loads(l)
                except Exception:
                    continue
                out[d["cadena"]["ts"]] = d
    ts = sorted(out)[-n:]
    return [out[t] for t in ts]


def perfil(d, S, ahora_utc=None):
    c = d["cadena"]
    ix = {k: i for i, k in enumerate(c["campos"].split(","))}
    ts = datetime.strptime(c["ts"], "%Y-%m-%d %H:%M:%S").replace(tzinfo=timezone.utc)
    env = max(0.0, ((ahora_utc or ts) - ts).total_seconds() / 86400.0)
    dias = [v["dias"] - env for v in c["vencimientos"]]
    mas = min([x for x in dias if x >= 0] or [0])
    por = {}
    for f in c["filas"]:
        K, vi = float(f[ix["strike"]]), int(f[ix["venc"]])
        dd = dias[vi]
        if not (0 <= dd <= max(1.0, mas + 0.01)):
            continue
        T = max(dd, PISO) / 365.0
        ivc, ivp = float(f[ix["iv_call"]]), float(f[ix["iv_put"]])
        oic, oip = float(f[ix["oi_call"]]), float(f[ix["oi_put"]])
        vc, vp = float(f[ix["vol_call"]]), float(f[ix["vol_put"]])
        def gex(s, w_c, w_p):
            return (gbs(s, K, T, ivc) * w_c - gbs(s, K, T, ivp) * w_p) * MULT * s * s * 0.01
        gv, go = gex(S, vc, vp), gex(S, oic, oip)
        if gv == 0 and go == 0:
            continue
        s = por.setdefault(K, dict(K=K, vol=0.0, oi=0.0, convV=0.0, convO=0.0, OI=0.0, V=0.0, filas=[]))
        s["vol"] += gv; s["oi"] += go
        s["convV"] += gex(S * 1.01, vc, vp) - gv; s["convO"] += gex(S * 1.01, oic, oip) - go
        s["OI"] += oic + oip; s["V"] += vc + vp
        s["filas"].append((K, T, ivc, ivp, vc, vp, oic, oip))
    return sorted(por.values(), key=lambda s: s["K"]), mas


def cruce(filas_todas, S, por_vol):
    lo, hi, pasos = S * 0.97, S * 1.03, 60
    ant, xant = None, 0.0
    for i in range(pasos + 1):
        x = lo + (hi - lo) * i / pasos
        t = 0.0
        for (K, T, ivc, ivp, vc, vp, oic, oip) in filas_todas:
            wc, wp = (vc, vp) if por_vol else (oic, oip)
            t += (gbs(x, K, T, ivc) * wc - gbs(x, K, T, ivp) * wp) * MULT * x * x * 0.01
        if ant is not None and ((ant < 0 <= t) or (ant > 0 >= t)):
            return xant + (x - xant) * (-ant) / (t - ant) if t != ant else x
        ant, xant = t, x
    return float("nan")


def fmt(v):
    a = abs(v)
    return ("%+.2fB" % (v / 1e9)) if a >= 1e9 else ("%+.0fM" % (v / 1e6))


def main():
    a = [x for x in sys.argv[1:] if not x.startswith("--")]
    raiz = a[0] if a else "NQ"
    arg = lambda k, d: type(d)(sys.argv[sys.argv.index(k) + 1]) if k in sys.argv else d
    audit = arg("--audit", "")
    base_mano = arg("--base", 0.0)
    fut = arg("--fut", 0.0)
    cs = cadenas(raiz)
    d = cs[-1]; c = d["cadena"]
    S_idx = float(c["spot_idx"])
    base_cad = d["base"] if d.get("base_confiable") and d.get("base") else (d.get("base_cruda") or 0.0)
    base = base_mano or base_cad
    # el AUDIT del indicador: su base y su futuro, para comparar en K
    au = {}
    if audit:
        for k in ("fut", "S", "base", "netVol", "netOi", "zeroVol", "zeroOi", "mpVol", "mnVol", "pico", "picoGex", "convPrecio"):
            m = re.search(k + r"=(-?[0-9.]+|NaN)", audit)
            if m and m.group(1) != "NaN":
                au[k] = float(m.group(1).rstrip("B").rstrip("M"))
        m = re.search(r"doms=([^ ]+)", audit)
        if m:
            au["doms"] = [(float(x.split("=")[0]), x.split("=")[1]) for x in m.group(1).split("/") if "=" in x]
        m = re.search(r"origen=([A-Za-z_0-9]+)", audit); au["origen"] = m.group(1) if m else ""
    S = (au["S"] if "S" in au else (fut - base if fut else S_idx))
    futuro = fut or (S + base)
    print("== %s | cadena %s UTC (generada %s) | spot_idx %.2f | base cadena %s%.2f | base usada aca %.2f | S usado %.2f | futuro %.2f" % (
        raiz, c["ts"], d.get("generado"), S_idx, "medida " if d.get("base_confiable") else "CRUDA ", base_cad, base, S, futuro))
    if au:
        print("   AUDIT del indicador: fut %.2f S %.2f base %.2f (%s)" % (au["fut"], au["S"], au["base"], au.get("origen")))
    P, mas = perfil(d, S)
    filas_todas = [f for s in P for f in s["filas"]]
    netV, netO = sum(s["vol"] for s in P), sum(s["oi"] for s in P)
    zv, zo = cruce(filas_todas, S, True), cruce(filas_todas, S, False)
    pv = max((s for s in P if s["vol"] > 0), key=lambda s: s["vol"], default=None)
    nv = min((s for s in P if s["vol"] < 0), key=lambda s: s["vol"], default=None)
    print("   vencimiento mas cercano %.3f d | strikes %d | net vol %s (AUDIT %s) | net OI %s (AUDIT %s)" % (
        mas, len(P), fmt(netV), fmt(au["netVol"] * 1e9) if "netVol" in au else "--", fmt(netO), fmt(au["netOi"] * 1e9) if "netOi" in au else "--"))
    def K_de(v):
        return v - au["base"] if au and v is not None else None
    print("   zero vol  K %.2f -> fut %.2f | AUDIT fut %.2f (K %.2f)" % (zv, zv + base, au.get("zeroVol", float("nan")), K_de(au.get("zeroVol")) if "zeroVol" in au else float("nan")))
    print("   zero OI   K %.2f -> fut %.2f | AUDIT fut %.2f (K %.2f)" % (zo, zo + base, au.get("zeroOi", float("nan")), K_de(au.get("zeroOi")) if "zeroOi" in au else float("nan")))
    if pv: print("   +Γ (major vol) K %.0f %s -> fut %.2f | AUDIT %.2f (K %.2f)" % (pv["K"], fmt(pv["vol"]), pv["K"] + base, au.get("mpVol", float("nan")), K_de(au.get("mpVol")) if "mpVol" in au else float("nan")))
    if nv: print("   -Γ (major vol) K %.0f %s -> fut %.2f | AUDIT %.2f (K %.2f)" % (nv["K"], fmt(nv["vol"]), nv["K"] + base, au.get("mnVol", float("nan")), K_de(au.get("mnVol")) if "mnVol" in au else float("nan")))
    # dominantes: una por lado dentro del radio, por |GEX vol|; centroide +-12 pts
    radio = futuro * RADIO_DOM_PCT / 100.0
    en = [s for s in P if abs((s["K"] + base) - futuro) <= radio and abs(s["vol"]) > 0]
    arriba = max((s for s in en if s["K"] + base > futuro), key=lambda s: abs(s["vol"]), default=None)
    abajo = max((s for s in en if s["K"] + base <= futuro), key=lambda s: abs(s["vol"]), default=None)
    for nombre, s in (("D arriba", arriba), ("D abajo", abajo)):
        if s is None:
            print("   %s: ninguna" % nombre); continue
        sw = sx = 0.0
        for x in P:
            if abs(x["K"] - s["K"]) <= RADIO_CENTRO and abs(x["vol"]) > 0:
                sw += abs(x["vol"]); sx += abs(x["vol"]) * x["K"]
        cen = sx / sw if sw else s["K"]
        print("   %s: strike %.0f %s, centroide K %.2f -> fut %.2f" % (nombre, s["K"], fmt(s["vol"]), cen, cen + base))
    if au.get("doms"):
        print("   AUDIT doms: " + " / ".join("fut %.2f (K %.2f) %s" % (f, f - au["base"], g) for f, g in au["doms"]))
    # cuadrante: pico y convexidad en el precio
    rp = futuro * PICO_PCT / 100.0
    cerca = [s for s in P if abs((s["K"] + base) - futuro) <= rp]
    sumV, sumO = sum(s["V"] for s in P), sum(s["OI"] for s in P)
    porVol = sumV > 0 and sumV >= 0.2 * sumO
    pico = max(cerca, key=lambda s: abs(s["vol"] if porVol else s["oi"]), default=None)
    convP = sum((s["convV"] if porVol else s["convO"]) for s in cerca)
    maxL = max(abs(s["vol"] if porVol else s["oi"]) for s in P) if P else 0
    if pico:
        g = pico["vol"] if porVol else pico["oi"]
        print("   pico (radio %.0f pts, libro %s): K %.0f %s mucho=%s | conv en precio %s | AUDIT pico %.2f (K %.2f) %sM conv %sM" % (
            rp, "vol" if porVol else "OI", pico["K"], fmt(g), abs(g) >= maxL * MUCHO_PCT / 100.0, fmt(convP),
            au.get("pico", float("nan")), K_de(au.get("pico")) if "pico" in au else float("nan"), au.get("picoGex", "--"), au.get("convPrecio", "--")))
    else:
        print("   pico: ningun strike a %.0f pts del precio (radio %.2f %%)" % (rp, PICO_PCT))
    # barras mas pesadas cerca (lo que llevan raya punteada) y su convexidad
    print("   las 4 barras mas pesadas a +-0,6 %:")
    for s in sorted((s for s in P if abs((s["K"] + base) - futuro) <= futuro * 0.006), key=lambda s: -abs(s["vol"]))[:4]:
        print("      K %.0f -> fut %.2f  GEX vol %s  OI-GEX %s  conv %s  OI %.0f vol %.0f" % (s["K"], s["K"] + base, fmt(s["vol"]), fmt(s["oi"]), fmt(s["convV"]), s["OI"], s["V"]))
    # Max Change: contra las cadenas de 1, 5 y 30 minutos antes (cada una a su propio spot)
    print("   Max Change (strike con mayor |cambio| de GEX vol contra la cadena de hace N min):")
    t_ult = datetime.strptime(c["ts"], "%Y-%m-%d %H:%M:%S")
    for n in (1, 5, 30):
        prev = None
        for x in reversed(cs[:-1]):
            tx = datetime.strptime(x["cadena"]["ts"], "%Y-%m-%d %H:%M:%S")
            if (t_ult - tx).total_seconds() >= n * 60 - 20:
                prev = x; break
        if prev is None:
            print("      %2d min: sin cadena anterior" % n); continue
        Pp, _ = perfil(prev, float(prev["cadena"]["spot_idx"]))
        vp = {s["K"]: s["vol"] for s in Pp}
        mejor = max(P, key=lambda s: abs(s["vol"] - vp.get(s["K"], 0.0)), default=None)
        dlt = mejor["vol"] - vp.get(mejor["K"], 0.0) if mejor else 0
        print("      %2d min (cadena %s): K %.0f -> fut %.2f  Δ %s%s" % (n, prev["cadena"]["ts"][11:16], mejor["K"], mejor["K"] + base, fmt(dlt), "  (cadena identica: sin cambios)" if dlt == 0 else ""))


if __name__ == "__main__":
    main()
