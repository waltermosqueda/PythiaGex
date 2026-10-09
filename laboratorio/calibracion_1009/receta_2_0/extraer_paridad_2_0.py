# -*- coding: utf-8 -*-
"""REFERENCIA DE PARIDAD DE LA 2.0 (PythiaGex 2.0.6, GammaHoy + capa NDX) PARA EL PORT A LA 4.1. 09-10-2026. SOLO LECTURA.

Que hace:
  1. Lee las lineas AUDIT de %APPDATA%/ATAS/pythiagex2-gammahoy.log desde DESDE_LOCAL (hora de la PC, UTC-3):
       "AUDIT fut=..."           = libro primario (QQQ 0DTE por razon, LibroAuto en NQ)
       "AUDIT capa=NDX fut=..."  = capa NDX automatica (CapasAutoNQ)
  2. RECALCULA cada linea con la receta de la 2.0 (GammaHoyNucleo.CalcularAdentro, port 1:1 a Python, mas abajo) sobre la cadena
     archivada por cboe_local.py (%APPDATA%/ATAS/PythiaGex/cboe-local/cadena-<QQQ|NQ>-<dia>.jsonl.gz), con el fut, la razon / base
     y la hora que dice el propio AUDIT. Si el recalculo coincide con lo logueado, la receta esta bien escrita.
  3. Junta, por minuto, lo de la 4.1 (familia/niv-<sesion>-MNQZ6.jsonl y estela TRES de PythiaGex4) para ver la diferencia.
  4. Escribe paridad_2_0_minuto.csv y estela_ndx_2_0_sesion.csv en esta carpeta.

Uso:  python -I extraer_paridad_2_0.py [DESDE_LOCAL]      (default 2026-10-08T19:00:00 = 22:00 UTC)
"""
import csv
import datetime as dt
import glob
import gzip
import json
import math
import os
import re
import sys

APP = os.path.join(os.environ.get("APPDATA", ""), "ATAS")
LOG2 = os.path.join(APP, "pythiagex2-gammahoy.log")
CBOE = os.path.join(APP, "PythiaGex", "cboe-local")
EST2 = os.path.join(APP, "PythiaGex2", "estela")
FAM4 = os.path.join(APP, "PythiaGex4", "familia")
EST4 = os.path.join(APP, "PythiaGex4", "estela")
AQUI = os.path.dirname(os.path.abspath(__file__))
DESDE_LOCAL = sys.argv[1] if len(sys.argv) > 1 else "2026-10-08T19:00:00"
UTC_MENOS = dt.timedelta(hours=3)          # la PC esta en UTC-3 (Argentina, sin horario de verano): el log usa DateTime.Now
RAZON_ARCHIVO = os.path.join(APP, "PythiaGex2", "razon-NQ-QQQ.txt")   # la razon exacta que guardo la 2.0 (R|utc)

# ---------------------------------------------------------------------------------------------- la cuenta de la 2.0 (port 1:1)
R = 0.0375                 # Tasa (ajuste "Tasa", .ws = 0.0375)
MULT = 100.0               # GammaHoyNucleo.MULT_INDICE
PISO_DIAS = 1.0 / 1440.0   # GammaHoyNucleo.PISO_DIAS


def fi(x):
    return math.exp(-0.5 * x * x) / math.sqrt(2.0 * math.pi)


def gamma_bs(S, K, T, iv, r):
    """GammaHoyNucleo.GammaBs: Black-Scholes SIN dividendo y SIN descuento en la gamma (la r solo entra en d1)."""
    if S <= 0 or K <= 0 or T <= 0 or iv <= 0:
        return 0.0
    v = iv * math.sqrt(T)
    if v <= 0:
        return 0.0
    d1 = (math.log(S / K) + (r + 0.5 * iv * iv) * T) / v
    return fi(d1) / (S * v)


def gex(f, S, T, por_vol):
    """GammaHoyNucleo.Gex: (gC*wC - gP*wP) * 100 * S^2 * 0.01. f = [K, V, oiC, oiP, ivC, ivP, volC, volP]."""
    K = f[0]
    gC = gamma_bs(S, K, T, f[4], R)
    gP = gamma_bs(S, K, T, f[5], R)
    wC, wP = (f[6], f[7]) if por_vol else (f[2], f[3])
    return (gC * wC - gP * wP) * MULT * S * S * 0.01


def envejecer(gen_utc, ahora_utc):
    if gen_utc is None or ahora_utc <= gen_utc:
        return 0.0
    return min(2.0, (ahora_utc - gen_utc).total_seconds() / 86400.0)


def zero_por_signo(perfil, S, por_vol):
    """GammaHoyNucleo.ZeroPorSigno: cruce de signo entre strikes vecinos (saltando los de GEX 0), interpolado; el MAS CERCANO a S
    (estricto <: en empate gana el de menor K). Devuelve (zero en K, cuantos cruces a +-0,1 % de S, lista de cruces a +-0,2 %)."""
    mejor, dist, ant, n01, lista = float("nan"), float("inf"), None, 0, []
    for s in perfil:
        g = s["gv"] if por_vol else s["go"]
        if g == 0 or math.isnan(g):
            continue
        if ant is not None:
            ga = ant["gv"] if por_vol else ant["go"]
            if (ga < 0 < g) or (ga > 0 > g):
                k = ant["K"] + (s["K"] - ant["K"]) * (-ga) / (g - ga)
                d = abs(k - S)
                if d < dist:
                    dist, mejor = d, k
                if d <= S * 0.001:
                    n01 += 1
                if d <= S * 0.002:
                    lista.append(k)
        ant = s
    return mejor, n01, lista


def calcular(cad, fut, ahora_utc, escala=None, base=None):
    """GammaHoyNucleo.CalcularAdentro con los ajustes EFECTIVOS del .ws del operador:
    Horizonte=Hoy, CuantasDominantes=2, RadioDominantesPct=2, RadioDominantesMaxPts=100, DominantesReferencia=true (=> UnaPorLado=false
    y Centroide=false en el nucleo), DominantesDeNoche=Volumen, ZeroInterpolado=true, Convexidad=Auto, PicoRadioPct=0.35, MuchoPct=50.
    escala = razon (libro por razon: S = fut/escala, Fut = K*escala); base = base aditiva (S = fut - base, Fut = K + base)."""
    c = cad["cadena"]
    dias = [float(v.get("dias") or 0) for v in c["vencimientos"]]
    filas = [f for f in c["filas"] if len(f) >= 8]
    por_razon = escala is not None
    S = fut / escala if por_razon else fut - base
    al_fut = (lambda k: k * escala) if por_razon else (lambda k: k + base)
    gen = dt.datetime.fromisoformat(cad["generado"]).astimezone(dt.timezone.utc).replace(tzinfo=None)
    env = envejecer(gen, ahora_utc)
    mas_cerca = min([d - env for d in dias if d - env >= 0] or [0.0])
    Sup = S * 1.01
    por = {}
    conv_oi = {}
    for f in filas:
        V = int(f[1])
        if V < 0 or V >= len(dias):
            continue
        d = dias[V] - env
        if not (d >= 0 and d <= max(1.0, mas_cerca + 0.01)):      # PasaHorizonte(Hoy)
            continue
        T = max(d, PISO_DIAS) / 365.0
        go, gv = gex(f, S, T, False), gex(f, S, T, True)
        goU, gvU = gex(f, Sup, T, False), gex(f, Sup, T, True)
        if go == 0 and gv == 0:
            continue
        K = f[0]
        s = por.get(K)
        if s is None:
            s = por[K] = {"K": K, "Fut": al_fut(K), "go": 0.0, "gv": 0.0, "conv": 0.0}
        s["go"] += go
        s["gv"] += gv
        s["conv"] += gvU - gv
        conv_oi[K] = conv_oi.get(K, 0.0) + (goU - go)
    perfil = [por[k] for k in sorted(por)]
    sum_vol = sum(abs(x["gv"]) for x in perfil)
    sum_oi = sum(abs(x["go"]) for x in perfil)
    conv_por_vol = sum_vol >= 0.2 * sum_oi and sum_vol > 0              # Convexidad = Auto
    if not conv_por_vol:
        for x in perfil:
            x["conv"] = conv_oi.get(x["K"], 0.0)
    net_vol = sum(x["gv"] for x in perfil)
    net_oi = sum(x["go"] for x in perfil)
    zv, nzv, lzv = zero_por_signo(perfil, S, True)
    zo, nzo, lzo = zero_por_signo(perfil, S, False)
    zv = al_fut(zv) if not math.isnan(zv) else zv       # (sin cruce por strike la 2.0 cae a Cruce(): no paso esta noche)
    zo = al_fut(zo) if not math.isnan(zo) else zo
    pos = [x for x in perfil if x["gv"] > 0]
    neg = [x for x in perfil if x["gv"] < 0]
    mp = max(pos, key=lambda x: x["gv"])["Fut"] if pos else float("nan")      # max() devuelve el primero en empate = OrderByDescending.First
    mn = min(neg, key=lambda x: x["gv"])["Fut"] if neg else float("nan")
    radio = min(fut * 2.0 / 100.0, 100.0)
    cand = [x for x in perfil if abs(x["Fut"] - fut) <= radio and abs(x["gv"]) > 0]
    cand = sorted(cand, key=lambda x: -abs(x["gv"]))[:2]                      # sorted es estable, como OrderByDescending
    libro_dom = "vol"
    doms = [(x["Fut"], x["gv"], x["K"]) for x in cand]
    if not doms:
        libro_dom = "OI"
        cand = sorted([x for x in perfil if abs(x["Fut"] - fut) <= radio and abs(x["go"]) > 0], key=lambda x: -abs(x["go"]))[:2]
        doms = [(x["Fut"], x["go"], x["K"]) for x in cand]
    # cuadrante (q) y pico
    r_pico = fut * 0.35 / 100.0
    cerca = [x for x in perfil if abs(x["Fut"] - fut) <= r_pico]
    por_vol_cuad = sum_vol > 0 and sum_vol >= 0.2 * sum_oi
    pico_gex, pico_fut, conv_precio = 0.0, float("nan"), 0.0
    for x in cerca:
        gg = x["gv"] if por_vol_cuad else x["go"]
        if abs(gg) > abs(pico_gex):
            pico_gex, pico_fut = gg, x["Fut"]
        conv_precio += x["conv"]
    if not cerca and perfil:
        conv_precio = min(perfil, key=lambda x: abs(x["Fut"] - fut))["conv"]
    max_libro = max((abs(x["gv"]) for x in perfil), default=0) if por_vol_cuad else max((abs(x["go"]) for x in perfil), default=0)
    mucho = max_libro > 0 and abs(pico_gex) >= max_libro * 50 / 100.0
    conv_pos = conv_precio >= 0
    q = 1 if (mucho and conv_pos) else 2 if mucho else 3 if conv_pos else 4
    return {"S": S, "strikes": len(perfil), "netVol": net_vol, "netOi": net_oi, "zeroVol": zv, "zeroOi": zo,
            "zeroCrucesVol": nzv, "zeroCrucesOi": nzo, "crucesVol": [al_fut(k) for k in lzv],
            "mpVol": mp, "mnVol": mn, "doms": doms, "libroDom": libro_dom, "q": q, "pico": pico_fut, "picoGex": pico_gex,
            "convPrecio": conv_precio, "env": env, "masCerca": mas_cerca}


# ---------------------------------------------------------------------------------------------- datos
def cargar_cadenas(raiz):
    out = []
    for p in sorted(glob.glob(os.path.join(CBOE, "cadena-%s-2026-10-0[89].jsonl.gz" % raiz))):
        with gzip.open(p, "rt", encoding="utf-8") as fh:
            for l in fh:
                l = l.strip()
                if not l:
                    continue
                x = json.loads(l)
                x["_gen"] = dt.datetime.fromisoformat(x["generado"]).astimezone(dt.timezone.utc).replace(tzinfo=None)
                out.append(x)
    out.sort(key=lambda x: x["_gen"])
    return out


def cadena_en(cads, t_utc, ts=None, gen_hms=None):
    if ts:
        for x in cads:
            if x["cadena"].get("ts") == ts and (gen_hms is None or x["_gen"].strftime("%H:%M:%S") == gen_hms):
                return x
    best = None
    for x in cads:
        if x["_gen"] <= t_utc:
            best = x
        else:
            break
    return best


RX = re.compile(r"^(\S+)\s+AUDIT (capa=(\S+) )?(.*)$")


def parse_audit(linea):
    m = RX.match(linea.strip())
    if not m:
        return None
    t_local = dt.datetime.fromisoformat(m.group(1))
    capa = m.group(3) or "PRIMARIA"
    kv = {}
    for tok in m.group(4).split(" "):
        if "=" in tok:
            k, v = tok.split("=", 1)
            kv[k] = v
    doms = []
    for d in (kv.get("doms") or "").split("/"):
        if "=" in d:
            p, g = d.split("=")
            doms.append((float(p), float(g.rstrip("M"))))
    kv["_doms"] = doms
    return t_local, capa, kv


def f2(x):
    return "" if x is None or (isinstance(x, float) and math.isnan(x)) else ("%.2f" % x)


def num(s):
    try:
        return float(s)
    except Exception:
        return float("nan")


def main():
    razon_exacta, razon_utc = float("nan"), ""
    try:
        a, b = open(RAZON_ARCHIVO, encoding="utf-8").read().strip().split("|")
        razon_exacta, razon_utc = float(a), b
    except Exception:
        pass
    cq, cn = cargar_cadenas("QQQ"), cargar_cadenas("NQ")

    # instancias: cada "arranca" abre una
    filas_log, inst, n_inst = [], "previa", 0
    with open(LOG2, encoding="utf-8", errors="replace") as fh:
        for l in fh:
            if l[:19] < DESDE_LOCAL:
                continue
            if "Gamma Hoy 2.0" in l and " arranca" in l:
                n_inst += 1
                inst = "I%d@%s" % (n_inst, l[11:19])
                continue
            if " AUDIT " in l:
                p = parse_audit(l)
                if p:
                    filas_log.append((inst,) + p)

    # 4.1: familia por minuto (k = minutos UTC desde 1970) y estela TRES
    fam = {}
    for p in sorted(glob.glob(os.path.join(FAM4, "niv-2026-10-0[89]-MNQZ6.jsonl"))):
        for l in open(p, encoding="utf-8"):
            x = json.loads(l)
            if "k" in x:
                fam[x["k"]] = x
    tres = {"NDX": [], "QQQ": []}
    for cap in tres:
        for p in sorted(glob.glob(os.path.join(EST4, "estela-%s-2026-10-0[89].jsonl" % cap))):
            for l in open(p, encoding="utf-8"):
                try:
                    x = json.loads(l)
                    tres[cap].append((dt.datetime.strptime(x["t"], "%Y-%m-%dT%H:%M:%SZ"), x))
                except Exception:
                    pass
        tres[cap].sort(key=lambda z: z[0])

    def tres_en(cap, t):
        best = None
        for tt, x in tres[cap]:
            if tt <= t:
                best = x
            else:
                break
        return best

    # por minuto local
    minutos = {}
    for inst_, t_local, capa, kv in filas_log:
        clave = t_local.strftime("%Y-%m-%d %H:%M")
        minutos.setdefault(clave, {})[capa] = (inst_, t_local, kv)

    TOL_P, TOL_M = 0.02, 1.0     # tolerancias: 0,02 pts de precio (redondeo F2) y 1 M (redondeo F0)
    cols = None
    salida = []
    stats = {"q_ok": 0, "q_n": 0, "n_ok": 0, "n_n": 0}
    for clave in sorted(minutos):
        reg = minutos[clave]
        t_loc = dt.datetime.strptime(clave, "%Y-%m-%d %H:%M")
        t_utc = t_loc + UTC_MENOS
        row = {"min_local": clave, "min_utc": t_utc.strftime("%Y-%m-%d %H:%M")}
        # ---------------- primaria (QQQ por razon)
        if "PRIMARIA" in reg:
            inst_, tl, kv = reg["PRIMARIA"]
            tu = tl + UTC_MENOS
            m = re.search(r"x_razon_([0-9.]+)", kv.get("origen", ""))
            rz4 = float(m.group(1)) if m else float("nan")
            rz = razon_exacta if (not math.isnan(razon_exacta) and abs(rz4 - razon_exacta) < 5e-5) else rz4
            fut = num(kv["fut"])
            row.update({"instancia": inst_, "q_hora_local": tl.strftime("%H:%M:%S"), "q_fut": kv["fut"], "q_razon_log": "%.4f" % rz4,
                        "q_razon_usada": repr(rz), "q_origen": kv.get("origen", ""), "q_S": kv["S"], "q_strikes": kv["strikes"],
                        "q_netVol_B": kv["netVol"].rstrip("B"), "q_netOi_B": kv["netOi"].rstrip("B"), "q_zeroVol": kv["zeroVol"], "q_zeroOi": kv["zeroOi"],
                        "q_mpVol": kv["mpVol"], "q_mnVol": kv["mnVol"], "q_libroDom": kv.get("libroDom", ""), "q_q": kv.get("q", ""),
                        "q_pico": kv.get("pico", ""), "q_picoGex_M": kv.get("picoGex", "").rstrip("M")})
            for i in range(2):
                if i < len(kv["_doms"]):
                    p, g = kv["_doms"][i]
                    row["q_d%d" % (i + 1)] = "%.2f" % p
                    row["q_d%d_M" % (i + 1)] = "%.0f" % g
                    row["q_d%d_K" % (i + 1)] = "%.2f" % (p / rz) if rz == rz else ""
            cad = cadena_en(cq, tu)
            if cad is not None and rz == rz:
                r = calcular(cad, fut, tu, escala=rz)
                row.update({"q_cadena_gen": cad["_gen"].strftime("%H:%M:%S"), "q_cadena_ts": cad["cadena"]["ts"], "q_spot_idx": cad["cadena"]["spot_idx"],
                            "r_q_strikes": r["strikes"], "r_q_netVol_B": "%.3f" % (r["netVol"] / 1e9), "r_q_zeroVol": f2(r["zeroVol"]), "r_q_zeroOi": f2(r["zeroOi"]),
                            "r_q_mpVol": f2(r["mpVol"]), "r_q_mnVol": f2(r["mnVol"]), "r_q_q": r["q"], "r_q_zeroCruces01": r["zeroCrucesVol"]})
                for i in range(2):
                    if i < len(r["doms"]):
                        row["r_q_d%d" % (i + 1)] = "%.2f" % r["doms"][i][0]
                        row["r_q_d%d_M" % (i + 1)] = "%.0f" % (r["doms"][i][1] / 1e6)
                ok = (str(r["strikes"]) == kv["strikes"] and abs(r["netVol"] / 1e9 - num(kv["netVol"].rstrip("B"))) <= 0.0015
                      and all(abs(num(row.get("q_d%d" % (i + 1), "nan")) - num(row.get("r_q_d%d" % (i + 1), "nan"))) <= TOL_P for i in range(len(kv["_doms"])))
                      and all(abs(num(row.get("q_d%d_M" % (i + 1), "nan")) - num(row.get("r_q_d%d_M" % (i + 1), "nan"))) <= TOL_M for i in range(len(kv["_doms"])))
                      and abs(num(kv["zeroVol"]) - r["zeroVol"]) <= TOL_P and str(r["q"]) == kv.get("q", ""))
                row["q_paridad_ok"] = "SI" if ok else "NO"
                stats["q_n"] += 1
                stats["q_ok"] += ok
        # ---------------- capa NDX (base aditiva)
        if "NDX" in reg:
            inst_, tl, kv = reg["NDX"]
            tu = tl + UTC_MENOS
            base, fut = num(kv["base"]), num(kv["fut"])
            row.update({"instancia": row.get("instancia") or inst_, "n_hora_local": tl.strftime("%H:%M:%S"), "n_fut": kv["fut"], "n_base": kv["base"],
                        "n_origen": kv.get("origen", ""), "n_cadenaTs": kv.get("cadenaTs", ""), "n_gen": kv.get("gen", ""), "n_fuente": kv.get("fuente", ""),
                        "n_S": kv["S"], "n_strikes": kv["strikes"], "n_netVol_B": kv["netVol"].rstrip("B"), "n_zeroVol": kv["zeroVol"], "n_zeroOi": kv["zeroOi"],
                        "n_mpVol": kv["mpVol"], "n_mnVol": kv["mnVol"], "n_q": kv.get("q", "")})
            for i in range(2):
                if i < len(kv["_doms"]):
                    p, g = kv["_doms"][i]
                    row["n_d%d" % (i + 1)] = "%.2f" % p
                    row["n_d%d_M" % (i + 1)] = "%.0f" % g
                    row["n_d%d_K" % (i + 1)] = "%.2f" % (p - base)
            ts = (kv.get("cadenaTs") or "").replace("_", " ")
            cad = cadena_en(cn, tu, ts=ts or None, gen_hms=kv.get("gen"))
            if cad is not None:
                r = calcular(cad, fut, tu, base=base)
                row.update({"r_n_strikes": r["strikes"], "r_n_netVol_B": "%.3f" % (r["netVol"] / 1e9), "r_n_zeroVol": f2(r["zeroVol"]), "r_n_zeroOi": f2(r["zeroOi"]),
                            "r_n_mpVol": f2(r["mpVol"]), "r_n_mnVol": f2(r["mnVol"]), "r_n_q": r["q"], "r_n_zeroCruces01": r["zeroCrucesVol"],
                            "r_n_crucesVol_02pct": " ".join("%.2f" % z for z in r["crucesVol"])})
                for i in range(2):
                    if i < len(r["doms"]):
                        row["r_n_d%d" % (i + 1)] = "%.2f" % r["doms"][i][0]
                        row["r_n_d%d_M" % (i + 1)] = "%.0f" % (r["doms"][i][1] / 1e6)
                ok = (str(r["strikes"]) == kv["strikes"] and abs(r["netVol"] / 1e9 - num(kv["netVol"].rstrip("B"))) <= 0.0015
                      and all(abs(num(row.get("n_d%d" % (i + 1), "nan")) - num(row.get("r_n_d%d" % (i + 1), "nan"))) <= TOL_P for i in range(len(kv["_doms"])))
                      and all(abs(num(row.get("n_d%d_M" % (i + 1), "nan")) - num(row.get("r_n_d%d_M" % (i + 1), "nan"))) <= TOL_M for i in range(len(kv["_doms"])))
                      and abs(num(kv["zeroVol"]) - r["zeroVol"]) <= TOL_P and str(r["q"]) == kv.get("q", ""))
                row["n_paridad_ok"] = "SI" if ok else "NO"
                stats["n_n"] += 1
                stats["n_ok"] += ok
        # ---------------- 4.1 en el mismo minuto UTC
        k = int((t_utc - dt.datetime(1970, 1, 1)).total_seconds() // 60)
        x = fam.get(k)
        if x:
            for mt in x.get("m", []):
                if mt["l"] == "QQQ":
                    row["v41_qqq_razon"] = "%.6f" % mt["c"]
                    if row.get("q_d1_K"):
                        row["dif_conv_q_d1_pts"] = "%.2f" % (num(row["q_d1"]) - num(row["q_d1_K"]) * mt["c"])
                if mt["l"] == "NDX":
                    row["v41_ndx_base"] = "%.2f" % mt["c"]
                    if row.get("n_base"):
                        row["dif_conv_ndx_pts"] = "%.2f" % (num(row["n_base"]) - mt["c"])
            for sid in ("MUROS_QQQ_oi", "MAJORS_QQQ_oi", "MUROS_QQQ_vol", "MAJORS_QQQ_vol", "ZEST_QQQ_vol",
                        "MUROS_NDX_vol", "MAJORS_NDX_vol", "ZTP_NDX_vol", "ZEST_NDX_vol"):
                v = x["s"].get(sid) or []
                row["v41_" + sid] = " ".join("%s:%.2f(%s)" % (e[1], e[0], "" if e[2] is None else e[2]) for e in v)
        for cap in ("NDX", "QQQ"):
            e = tres_en(cap, t_utc + dt.timedelta(seconds=59))
            if e:
                row["v41_TRES_%s_d1d2zero" % cap] = " ".join("%.2f" % z for z in e["d"])
        salida.append(row)

    cols = ["min_local", "min_utc", "instancia",
            "q_hora_local", "q_fut", "q_razon_log", "q_razon_usada", "q_origen", "q_S", "q_strikes", "q_netVol_B", "q_netOi_B", "q_zeroVol", "q_zeroOi",
            "q_mpVol", "q_mnVol", "q_d1", "q_d1_M", "q_d1_K", "q_d2", "q_d2_M", "q_d2_K", "q_libroDom", "q_q", "q_pico", "q_picoGex_M",
            "q_cadena_gen", "q_cadena_ts", "q_spot_idx",
            "r_q_strikes", "r_q_netVol_B", "r_q_zeroVol", "r_q_zeroOi", "r_q_mpVol", "r_q_mnVol", "r_q_d1", "r_q_d1_M", "r_q_d2", "r_q_d2_M", "r_q_q",
            "r_q_zeroCruces01", "q_paridad_ok",
            "n_hora_local", "n_fut", "n_base", "n_origen", "n_cadenaTs", "n_gen", "n_fuente", "n_S", "n_strikes", "n_netVol_B", "n_zeroVol", "n_zeroOi",
            "n_mpVol", "n_mnVol", "n_d1", "n_d1_M", "n_d1_K", "n_d2", "n_d2_M", "n_d2_K", "n_q",
            "r_n_strikes", "r_n_netVol_B", "r_n_zeroVol", "r_n_zeroOi", "r_n_mpVol", "r_n_mnVol", "r_n_d1", "r_n_d1_M", "r_n_d2", "r_n_d2_M", "r_n_q",
            "r_n_zeroCruces01", "r_n_crucesVol_02pct", "n_paridad_ok",
            "v41_qqq_razon", "dif_conv_q_d1_pts", "v41_ndx_base", "dif_conv_ndx_pts",
            "v41_MUROS_QQQ_oi", "v41_MAJORS_QQQ_oi", "v41_MUROS_QQQ_vol", "v41_MAJORS_QQQ_vol", "v41_ZEST_QQQ_vol",
            "v41_MUROS_NDX_vol", "v41_MAJORS_NDX_vol", "v41_ZTP_NDX_vol", "v41_ZEST_NDX_vol", "v41_TRES_NDX_d1d2zero", "v41_TRES_QQQ_d1d2zero"]
    ruta = os.path.join(AQUI, "paridad_2_0_minuto.csv")
    with open(ruta, "w", newline="", encoding="utf-8") as fh:
        w = csv.DictWriter(fh, fieldnames=cols, extrasaction="ignore")
        w.writeheader()
        for r in salida:
            w.writerow(r)

    # la estela NDX de la 2.0 en la sesion: cada cambio de D1/D2/zero (las "filas de rombos" son la columna zero)
    ruta_e = os.path.join(AQUI, "estela_ndx_2_0_sesion.csv")
    desde_utc = dt.datetime.fromisoformat(DESDE_LOCAL) + UTC_MENOS
    n_e = 0
    with open(ruta_e, "w", newline="", encoding="utf-8") as fh:
        w = csv.writer(fh)
        w.writerow(["t_utc", "fut", "ndx_d1", "ndx_d2", "ndx_zero_rombo", "d1_absM", "d2_absM", "netVol_M", "libro"])
        for p in sorted(glob.glob(os.path.join(EST2, "estela-NDX-2026-10-0[89].jsonl"))):
            for l in open(p, encoding="utf-8"):
                try:
                    x = json.loads(l)
                except Exception:
                    continue
                t = dt.datetime.strptime(x["t"], "%Y-%m-%dT%H:%M:%SZ")
                if t < desde_utc:
                    continue
                d = x.get("d", [0, 0, 0]) + [0, 0, 0]
                g = x.get("g", []) + ["", ""]
                w.writerow([x["t"], x.get("f", ""), d[0], d[1], d[2], g[0], g[1], x.get("n", ""), x.get("b", "")])
                n_e += 1

    print("razon exacta de la 2.0 (archivo): %r @ %s" % (razon_exacta, razon_utc))
    print("lineas AUDIT leidas: %d (primaria %d, NDX %d); minutos con algo: %d" % (
        len(filas_log), sum(1 for z in filas_log if z[2] == "PRIMARIA"), sum(1 for z in filas_log if z[2] == "NDX"), len(salida)))
    print("paridad QQQ: %d de %d minutos recalculados iguales; NDX: %d de %d" % (stats["q_ok"], stats["q_n"], stats["n_ok"], stats["n_n"]))
    print("escrito:", ruta, "y", ruta_e, "(%d cambios de estela NDX)" % n_e)


if __name__ == "__main__":
    main()
