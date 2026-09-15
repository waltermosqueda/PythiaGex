"""
capas_nq.py — recalculo INDEPENDIENTE de las capas de NQ (QQQ, TQQQ, NDX) contra el AUDIT del log de ATAS.

Protocolo del proyecto: ningun numero llega al operador sin recalcularlo desde la cadena cruda.
Este script no usa nada del indicador: baja ultima-<ticker>.json de la rama cadenas (CBOE, 902 s tarde),
rehace gamma Black-Scholes x volumen del dia por strike (misma convencion +call -put, x100 x S^2 x 1 %),
lleva cada strike al futuro con la MISMA razon que uso el indicador (la lee del log, asi se aisla la cuenta
del alineado de la vela) y con el apalancamiento (TQQQ = 3: un strike a +3 % de TQQQ es NQ a +1 %),
y compara dominantes, majors y zero gamma con la ultima linea AUDIT de ese libro.

Uso:
    python laboratorio/capas_nq.py                 # QQQ y TQQQ contra el log
    python laboratorio/capas_nq.py QQQ --apal 1
    python laboratorio/capas_nq.py TQQQ --apal 3 --razon 41.09 --fut 29008   (sin log)

Que se espera: los MISMOS strikes (K) en dominantes y majors; el zero a menos de 5 pts de NQ (la cadena
bajada puede ser un minuto mas nueva que la del log). Si TQQQ mapeado cae a cientos de puntos de QQQ,
el mapeo esta mal (casi seguro se olvido el apalancamiento).
"""
import argparse
import json
import math
import os
import re
import sys
import urllib.request
from datetime import datetime, timezone

RAW = "https://raw.githubusercontent.com/waltermosqueda/PythiaGex/cadenas/ultima-{}.json"
LOG = os.path.join(os.environ.get("APPDATA", ""), "ATAS", "pythiagex-gammahoy.log")
MULT = 100.0
PISO_DIAS = 1.0 / 1440.0
RADIO_DOM_PCT = 2.0     # "Dominantes: radio alrededor del precio (%)" por defecto
CUANTAS = 2
UNA_POR_LADO = True     # "Canal: una dominante por lado" (por defecto en el indicador)
TASA = 0.045


def fi(x):
    return math.exp(-0.5 * x * x) / math.sqrt(2.0 * math.pi)


def gamma_bs(S, K, T, iv, r):
    if S <= 0 or K <= 0 or T <= 0 or iv <= 0:
        return 0.0
    v = iv * math.sqrt(T)
    if v <= 0:
        return 0.0
    d1 = (math.log(S / K) + (r + 0.5 * iv * iv) * T) / v
    return fi(d1) / (S * v)


def bajar(ticker):
    req = urllib.request.Request(RAW.format(ticker), headers={"User-Agent": "PythiaGex-capas_nq/0.1"})
    with urllib.request.urlopen(req, timeout=30) as r:
        return json.loads(r.read().decode("utf-8"))


def ultimo_audit(ticker):
    """La ultima linea AUDIT del log de ese libro: la primaria (origen=libro_CBOE_<T>_x_razon_...) o la capa (capa=<T>)."""
    if not os.path.exists(LOG):
        return None
    pat_origen = re.compile(r"origen=libro_CBOE_" + re.escape(ticker) + r"_x_razon_([0-9.]+)")
    ultimo = None
    with open(LOG, encoding="utf-8", errors="replace") as f:
        for linea in f:
            if "AUDIT" not in linea:
                continue
            m = pat_origen.search(linea)
            if not m:
                continue
            cap = re.search(r"capa=(\w+)", linea)
            if cap and cap.group(1) != ticker:
                continue
            ultimo = linea.rstrip("\n")
    if not ultimo:
        return None
    def num(k):
        m = re.search(k + r"=(-?[0-9.]+|NaN)", ultimo)
        return float(m.group(1)) if m and m.group(1) != "NaN" else float("nan")
    doms = re.search(r"doms=(\S+)", ultimo)
    doms = [float(x.split("=")[0]) for x in doms.group(1).split("/")] if doms and doms.group(1) else []
    return {
        "linea": ultimo, "hora": ultimo[:19], "fut": num("fut"), "S": num("S"),
        "razon": float(pat_origen.search(ultimo).group(1)),
        "zeroVol": num("zeroVol"), "mpVol": num("mpVol"), "mnVol": num("mnVol"), "doms": doms,
        "capa": "capa=" in ultimo,
    }


def ultimo_fut():
    """El fut= del ultimo AUDIT del log, de cualquier libro (todos son del mismo grafico de NQ)."""
    if not os.path.exists(LOG):
        return None
    fut = None
    with open(LOG, encoding="utf-8", errors="replace") as f:
        for linea in f:
            if "AUDIT" in linea:
                m = re.search(r"fut=(-?[0-9.]+)", linea)
                if m:
                    fut = float(m.group(1))
    return fut


def recalcular(d, fut, razon, apal, ahora):
    c = d["cadena"]
    spot = float(c.get("spot_idx") or 0)
    dias_v = [float(v.get("dias") or 0) for v in c.get("vencimientos", [])]
    gen = d.get("generado")
    envejecer = 0.0
    if gen:
        g = datetime.fromisoformat(gen.replace("Z", "+00:00"))
        if g.tzinfo is None:
            g = g.replace(tzinfo=timezone.utc)
        envejecer = max(0.0, min(2.0, (ahora - g).total_seconds() / 86400.0))
    dias_env = [x - envejecer for x in dias_v]
    mas_cerca = min([x for x in dias_env if x >= 0], default=0.0)
    tope = max(1.0, mas_cerca + 0.01)          # Horizonte = Hoy

    # el mapeo, identico a Feed.Cadena.AlFuturo / AlLibro
    def al_futuro(k):
        if apal == 1 or spot <= 0:
            return k * razon
        return razon * (spot + (k - spot) / apal)

    def al_libro(f):
        if apal == 1 or spot <= 0:
            return f / razon
        return spot + apal * (f / razon - spot)

    S = al_libro(fut)
    filas = [f for f in c["filas"] if len(f) >= 8 and 0 <= int(f[1]) < len(dias_env) and 0 <= dias_env[int(f[1])] <= tope]

    def gex_en(x, por_vol=True):
        t = 0.0
        for f in filas:
            K, v, oic, oip, ivc, ivp, volc, volp = (float(f[0]), int(f[1]), *map(float, f[2:8]))
            T = max(dias_env[v], PISO_DIAS) / 365.0
            wc, wp = (volc, volp) if por_vol else (oic, oip)
            t += (gamma_bs(x, K, T, ivc, TASA) * wc - gamma_bs(x, K, T, ivp, TASA) * wp) * MULT * x * x * 0.01
        return t

    por_k = {}
    for f in filas:
        K, v, oic, oip, ivc, ivp, volc, volp = (float(f[0]), int(f[1]), *map(float, f[2:8]))
        T = max(dias_env[v], PISO_DIAS) / 365.0
        gv = (gamma_bs(S, K, T, ivc, TASA) * volc - gamma_bs(S, K, T, ivp, TASA) * volp) * MULT * S * S * 0.01
        go = (gamma_bs(S, K, T, ivc, TASA) * oic - gamma_bs(S, K, T, ivp, TASA) * oip) * MULT * S * S * 0.01
        if gv == 0 and go == 0:
            continue
        e = por_k.setdefault(K, [0.0, 0.0])
        e[0] += gv
        e[1] += go
    perfil = sorted((K, al_futuro(K), gv, go) for K, (gv, go) in por_k.items())

    # zero: cruce de signo en el eje del LIBRO, grilla +-3 % x apalancamiento, 61 pasos, interpolado; se mapea al final
    amp = 0.03 * max(1.0, apal)
    lo, hi = S * (1 - amp), S * (1 + amp)
    zero = float("nan")
    ant, x_ant = None, 0.0
    for i in range(61):
        x = lo + (hi - lo) * i / 60
        t = gex_en(x)
        if ant is not None and ((ant < 0 <= t) or (ant > 0 >= t)):
            zero = x_ant + (x - x_ant) * (0 - ant) / (t - ant) if t != ant else x
            break
        ant, x_ant = t, x
    zero_fut = al_futuro(zero) if not math.isnan(zero) else float("nan")

    radio = fut * RADIO_DOM_PCT / 100.0
    cerca = [p for p in perfil if abs(p[1] - fut) <= radio and p[2] != 0]
    # "Canal: una dominante por lado" (ajuste por defecto del indicador): la mas fuerte ARRIBA del precio y la mas
    # fuerte ABAJO; sin ese ajuste, las CUANTAS mas fuertes sin mirar el lado
    if UNA_POR_LADO:
        arriba = [p for p in cerca if p[1] >= fut]
        abajo = [p for p in cerca if p[1] < fut]
        doms = [x for x in (max(arriba, key=lambda p: abs(p[2]), default=None), max(abajo, key=lambda p: abs(p[2]), default=None)) if x]
        doms = sorted(doms, key=lambda p: -abs(p[2]))
    else:
        doms = sorted(cerca, key=lambda p: -abs(p[2]))[:CUANTAS]
    pos = [p for p in perfil if p[2] > 0]
    neg = [p for p in perfil if p[2] < 0]
    mp = max(pos, key=lambda p: p[2]) if pos else None
    mn = min(neg, key=lambda p: p[2]) if neg else None
    return {
        "spot": spot, "S": S, "generado": gen, "ts": c.get("ts"), "envejecer_dias": envejecer, "mas_cerca": mas_cerca,
        "strikes": len(perfil), "zero": zero_fut, "doms": doms, "mp": mp, "mn": mn, "perfil": perfil,
        "net_vol": sum(p[2] for p in perfil),
    }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("tickers", nargs="*", default=["QQQ", "TQQQ"])
    ap.add_argument("--apal", type=float, default=None, help="apalancamiento (TQQQ = 3); por defecto 3 si el ticker es TQQQ")
    ap.add_argument("--razon", type=float, default=None, help="razon futuro/ETF a usar (por defecto la del ultimo AUDIT del log)")
    ap.add_argument("--fut", type=float, default=None, help="precio del futuro (por defecto el del ultimo AUDIT del log)")
    a = ap.parse_args()
    ahora = datetime.now(timezone.utc)
    for t in a.tickers:
        apal = a.apal if a.apal is not None else (3.0 if t.upper() == "TQQQ" else 1.0)
        print("=" * 100)
        print(f"{t}  apalancamiento {apal:g}")
        au = ultimo_audit(t.upper())
        razon, fut = a.razon, a.fut
        if au:
            print(f"  log: AUDIT {'capa' if au['capa'] else 'primaria'} {au['hora']}  fut={au['fut']:.2f} razon={au['razon']:.4f} "
                  f"zeroVol={au['zeroVol']:.2f} mp={au['mpVol']:.2f} mn={au['mnVol']:.2f} doms={au['doms']}")
            razon = razon or au["razon"]
            fut = fut or au["fut"]
        else:
            print("  log: sin AUDIT de este libro (capa apagada o log sin acceso)")
        try:
            d = bajar(t.upper())
        except Exception as e:
            print(f"  no se pudo bajar ultima-{t}.json: {e}  (si es TQQQ: el archivador recien lo suma tras el push)")
            continue
        if fut is None:
            fut = ultimo_fut()
            if fut is not None:
                print(f"  fut: del ultimo AUDIT del log (cualquier libro): {fut:.2f}")
        if razon is None and fut is not None and float(d["cadena"].get("spot_idx") or 0) > 0:
            razon = fut / float(d["cadena"]["spot_idx"])
            print(f"  razon CRUDA sin alinear (fut/spot) = {razon:.4f}: no hay AUDIT de este libro; sirve para ver la zona, no para comparar al punto")
        if razon is None or fut is None:
            print("  falta --razon y --fut (no hay AUDIT en el log para sacarlos)")
            continue
        r = recalcular(d, fut, razon, apal, ahora)
        edad = (ahora - datetime.fromisoformat(r["generado"].replace("Z", "+00:00"))).total_seconds() / 60 if r["generado"] else float("nan")
        print(f"  cadena: generada {r['generado']} (hace {edad:.0f} min, +15 de CBOE)  ts CBOE {r['ts']}  spot {r['spot']:.2f}  "
              f"strikes 0DTE {r['strikes']}  mas cercano {r['mas_cerca']:.3f} d  S implicita {r['S']:.2f}")
        print(f"  propio: zero {r['zero']:.2f}  netVol {r['net_vol']/1e9:.3f}B")
        for i, p in enumerate(r["doms"]):
            print(f"          D{i+1} K={p[0]:.0f} -> fut {p[1]:.2f}  gexVol {p[2]/1e6:.0f}M")
        if r["mp"]:
            print(f"          major+ K={r['mp'][0]:.0f} -> {r['mp'][1]:.2f}   major- K={r['mn'][0]:.0f} -> {r['mn'][1]:.2f}" if r["mn"] else "")
        if au:
            ks_log = sorted(round(x / razon if apal == 1 else r["spot"] + apal * (x / razon - r["spot"])) for x in au["doms"])
            ks_prop = sorted(round(p[0]) for p in r["doms"])
            dz = abs(r["zero"] - au["zeroVol"]) if not math.isnan(r["zero"]) and not math.isnan(au["zeroVol"]) else float("nan")
            print(f"  veredicto: dominantes por K log {ks_log} vs propio {ks_prop} -> {'COINCIDEN' if ks_log == ks_prop else 'DISTINTAS'}; "
                  f"zero difiere {dz:.2f} pts -> {'OK' if dz < 5 else 'REVISAR'}")
    print("=" * 100)
    print("Magnitudes NO comparables entre capas (TQQQ lleva un factor x3/x9 que no esta medido): comparar strikes, no barras.")


if __name__ == "__main__":
    main()
