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
ARCHIVO = {"SPX": "ES", "NDX": "NQ"}   # SPX se archiva como ultima-ES.json y NDX como ultima-NQ.json
VIVA = os.path.join(os.environ.get("APPDATA", ""), "ATAS", "PythiaGex", "viva")
LOG = os.path.join(os.environ.get("APPDATA", ""), "ATAS", "pythiagex-gammahoy.log")
MULT = 100.0
PISO_DIAS = 1.0 / 1440.0
RADIO_DOM_PCT = 2.0     # "Dominantes: radio alrededor del precio (%)" por defecto
PICO_RADIO_PCT = 0.35   # "Pico de GEX cerca del precio: radio (%)" por defecto; la convexidad en el precio suma ese radio
CUANTAS = 2
UNA_POR_LADO = True     # "Canal: una dominante por lado" (por defecto en el indicador)
EMPATE_PCT = 20.0     # "Dominantes: empate tecnico, gana la mas cercana al precio (%)": leido de GammaHoy.cs
HORIZONTE = "Hoy"     # el del grafico: Hoy (<= 1 dia o el mas cercano), Semana (<= 7), Todo. Las capas COPIAN el del grafico
                      # (auditoria 15-09: el grafico con capas estuvo en Todo hasta las 19:22 y el auditor asumia Hoy)
TASA = 0.0375     # la "Tasa libre de riesgo" por defecto del indicador, leida de GammaHoy.cs


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


def gamma76(F, K, T, iv):
    """Black-76 (opciones sobre el futuro, la viva de ES por Rithmic): gamma = fi(d1) / (F sigma sqrt(T))."""
    if F <= 0 or K <= 0 or T <= 0 or iv <= 0:
        return 0.0
    v = iv * math.sqrt(T)
    d1 = (math.log(F / K) + 0.5 * iv * iv * T) / v
    return fi(d1) / (F * v)


def bajar(ticker):
    if ticker == "ES":
        return viva_local("ES")
    req = urllib.request.Request(RAW.format(ARCHIVO.get(ticker, ticker)), headers={"User-Agent": "PythiaGex-capas_nq/0.1"})
    with urllib.request.urlopen(req, timeout=30) as r:
        return json.loads(r.read().decode("utf-8"))


def viva_local(raiz):
    """La ultima linea del viva-<raiz>-<hoy>.jsonl que graba el grafico de MES (formato VivaJson: ts, futuro, filas
    [strike, dias, es_call, oi, iv, bid, ask, vol_hoy, ...]) convertida al mismo formato que la cadena de la nube."""
    from datetime import timedelta
    for atras in (0, 1):
        dia = (datetime.now(timezone.utc) - timedelta(days=atras)).strftime("%Y-%m-%d")
        p = os.path.join(VIVA, "viva-%s-%s.jsonl" % (raiz, dia))
        if not os.path.exists(p):
            continue
        with open(p, "rb") as f:
            f.seek(0, 2); largo = f.tell(); f.seek(max(0, largo - (1 << 20)))
            lineas = f.read().decode("utf-8", "replace").splitlines()
        for l in reversed(lineas):
            l = l.strip()
            if len(l) < 40 or not l.endswith("}"):
                continue
            r = json.loads(l)
            dias = sorted(set(round(float(x[1]), 4) for x in r["filas"]))
            por = {}
            for x in r["filas"]:
                K, di, call, oi, iv, vol = float(x[0]), round(float(x[1]), 4), float(x[2]) >= 0.5, float(x[3]), float(x[4]), float(x[7])
                if iv <= 0 or (oi <= 0 and vol <= 0):
                    continue
                e = por.setdefault((K, dias.index(di)), [K, dias.index(di), 0, 0, 0, 0, 0, 0])
                if call: e[2], e[4], e[6] = oi, iv, vol
                else: e[3], e[5], e[7] = oi, iv, vol
            ts = r["ts"].replace(" ", "T") + "+00:00"
            return {"generado": ts, "es_futuro": True,
                    "cadena": {"ts": r["ts"], "spot_idx": float(r["futuro"]), "vencimientos": [{"dias": d} for d in dias], "filas": list(por.values())}}
    raise FileNotFoundError("sin viva-%s en %s (el grafico de MES la graba con 'Guardar la cadena viva')" % (raiz, VIVA))


def ultimo_audit(ticker):
    """La ultima linea AUDIT del log de ese libro: la primaria (origen=libro_CBOE_<T>_x_razon_...) o la capa (capa=<T>)."""
    if not os.path.exists(LOG):
        return None
    # CBOE_QQQ, CBOE_TQQQ_x3, CBOE_SPX, CBOE_SPY, Rithmic_ES_(grabado): todos "libro ... x razon R"
    pat_origen = re.compile(r"origen=libro_(?:CBOE_|Rithmic_)" + re.escape(ticker) + r"(?:_x\d+|_\(grabado\))?_x_razon_([0-9.]+)")
    ultimo = None
    aditivo = ticker == "NDX"          # NDX va con base aditiva (Fut = K + base), no por razon
    with open(LOG, encoding="utf-8", errors="replace") as f:
        for linea in f:
            if "AUDIT" not in linea:
                continue
            if aditivo:
                if "capa=NDX " not in linea:
                    continue
            else:
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
    conv_precio = re.search(r"convPrecio=(-?[0-9.]+)M", ultimo)
    pico = re.search(r" pico=(-?[0-9.]+|NaN)", ultimo)
    doms = [float(x.split("=")[0]) for x in doms.group(1).split("/")] if doms and doms.group(1) else []
    return {
        "linea": ultimo, "hora": ultimo[:19], "fut": num("fut"), "S": num("S"),
        "razon": float(pat_origen.search(ultimo).group(1)) if not aditivo else None,
        "base": num("base") if aditivo else 0.0,
        "zeroVol": num("zeroVol"), "mpVol": num("mpVol"), "mnVol": num("mnVol"), "doms": doms,
        "capa": "capa=" in ultimo, "apal": (lambda m: float(m.group(1)) if m else None)(re.search(r"apal_([0-9.]+)x", ultimo)),
        "beta": num("beta"), "betaOrigen": (lambda m: m.group(1) if m else "")(re.search(r"betaOrigen=(\S+)", ultimo)),
        "convPrecio": float(conv_precio.group(1)) * 1e6 if conv_precio else float("nan"),
        "pico": float(pico.group(1)) if pico and pico.group(1) != "NaN" else float("nan"),
    }


def ultimo_fut():
    """El fut= del ultimo AUDIT del log, de cualquier libro (todos son del mismo grafico de NQ)."""
    if not os.path.exists(LOG):
        return None
    fut = None
    with open(LOG, encoding="utf-8", errors="replace") as f:
        for linea in f:
            # solo el grafico de NQ con capas: las lineas capa= o el libro de QQQ (el log lo comparten MES y MNQ)
            if "AUDIT" in linea and ("capa=" in linea or "_QQQ_" in linea):
                m = re.search(r"fut=(-?[0-9.]+)", linea)
                if m:
                    fut = float(m.group(1))
    return fut


def recalcular(d, fut, razon, apal, ahora, base=0.0):
    c = d["cadena"]
    es_fut = bool(d.get("es_futuro"))          # Black-76 (viva de ES) o Black-Scholes (CBOE)
    gam = (lambda x, K, T, iv: gamma76(x, K, T, iv)) if es_fut else (lambda x, K, T, iv: gamma_bs(x, K, T, iv, TASA))
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
    tope = {"Hoy": max(1.0, mas_cerca + 0.01), "Semana": max(7.0, mas_cerca + 0.01)}.get(HORIZONTE, float("inf"))   # GammaHoyNucleo.PasaHorizonte

    # el mapeo, identico a Feed.Cadena.AlFuturo / AlLibro; con base aditiva (NDX): Fut = K + base
    def al_futuro(k):
        if razon is None:
            return k + base
        if apal == 1 or spot <= 0:
            return k * razon
        return razon * (spot + (k - spot) / apal)

    def al_libro(f):
        if razon is None:
            return f - base
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
            t += (gam(x, K, T, ivc) * wc - gam(x, K, T, ivp) * wp) * MULT * x * x * 0.01
        return t

    por_k = {}
    Sup = S * 1.01
    for f in filas:
        K, v, oic, oip, ivc, ivp, volc, volp = (float(f[0]), int(f[1]), *map(float, f[2:8]))
        T = max(dias_env[v], PISO_DIAS) / 365.0
        gv = (gam(S, K, T, ivc) * volc - gam(S, K, T, ivp) * volp) * MULT * S * S * 0.01
        go = (gam(S, K, T, ivc) * oic - gam(S, K, T, ivp) * oip) * MULT * S * S * 0.01
        gv_up = (gam(Sup, K, T, ivc) * volc - gam(Sup, K, T, ivp) * volp) * MULT * Sup * Sup * 0.01
        if gv == 0 and go == 0:
            continue
        e = por_k.setdefault(K, [0.0, 0.0, 0.0])
        e[0] += gv
        e[1] += go
        e[2] += gv_up - gv          # convexidad por volumen: cuanto cambia el GEX del strike si S sube 1 %
    perfil = sorted((K, al_futuro(K), gv, go, cv) for K, (gv, go, cv) in por_k.items())

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

    r_pico = fut * PICO_RADIO_PCT / 100.0
    en_pico = [p for p in perfil if abs(p[1] - fut) <= r_pico]
    conv_precio = sum(p[4] for p in en_pico)
    pico = max(en_pico, key=lambda p: abs(p[2])) if en_pico else None
    conv_max = max(perfil, key=lambda p: abs(p[4])) if perfil else None
    radio = fut * RADIO_DOM_PCT / 100.0
    cerca = [p for p in perfil if abs(p[1] - fut) <= radio and p[2] != 0]
    # "Canal: una dominante por lado" (ajuste por defecto del indicador): la mas fuerte ARRIBA del precio y la mas
    # fuerte ABAJO; sin ese ajuste, las CUANTAS mas fuertes sin mirar el lado
    if UNA_POR_LADO:
        # la mas fuerte de cada lado; con empate tecnico (a menos de EMPATE_PCT de la mas fuerte), la mas cercana al precio
        def elegir(lado):
            if not lado:
                return None
            pmax = max(abs(p[2]) for p in lado)
            piso = pmax * (1.0 - max(0.0, min(90.0, EMPATE_PCT)) / 100.0)
            return sorted([p for p in lado if abs(p[2]) >= piso], key=lambda p: (abs(p[1] - fut), -abs(p[2])))[0]
        arriba = [p for p in cerca if p[1] > fut]
        abajo = [p for p in cerca if p[1] <= fut]
        doms = [x for x in (elegir(arriba), elegir(abajo)) if x]
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
        "net_vol": sum(p[2] for p in perfil), "conv_precio": conv_precio, "pico": pico, "conv_max": conv_max,
    }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("tickers", nargs="*", default=["QQQ", "TQQQ", "SPX", "SPY", "ES"])
    ap.add_argument("--apal", type=float, default=None, help="apalancamiento (TQQQ = 3); por defecto 3 si el ticker es TQQQ")
    ap.add_argument("--razon", type=float, default=None, help="razon futuro/ETF a usar (por defecto la del ultimo AUDIT del log)")
    ap.add_argument("--fut", type=float, default=None, help="precio del futuro (por defecto el del ultimo AUDIT del log)")
    a = ap.parse_args()
    ahora = datetime.now(timezone.utc)
    for t in a.tickers:
        apal = a.apal if a.apal is not None else (3.0 if t.upper() == "TQQQ" else None)   # None: del AUDIT (1/beta) o 1
        print("=" * 100)
        print(f"{t}  apalancamiento {'del log' if apal is None else format(apal, 'g')}")
        au = ultimo_audit(t.upper())
        razon, fut = a.razon, a.fut
        if au:
            print(f"  log: AUDIT {'capa' if au['capa'] else 'primaria'} {au['hora']}  fut={au['fut']:.2f} "
                  + (f"razon={au['razon']:.4f} " if au['razon'] is not None else f"base={au['base']:.2f} (aditiva) ")
                  + f"zeroVol={au['zeroVol']:.2f} mp={au['mpVol']:.2f} mn={au['mnVol']:.2f} doms={au['doms']}")
            razon = razon or au["razon"]
            fut = fut or au["fut"]
            base = au.get("base", 0.0) or 0.0
            if apal is None:
                apal = au["apal"] or 1.0
                if au["beta"] == au["beta"]:
                    print(f"  beta del log {au['beta']:.3f} ({au['betaOrigen']}) -> apalancamiento {apal:.3f}")
        if apal is None:
            apal = 1.0
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
        if razon is None and t.upper() != "NDX" and fut is not None and float(d["cadena"].get("spot_idx") or 0) > 0:
            razon = fut / float(d["cadena"]["spot_idx"])
            print(f"  razon CRUDA sin alinear (fut/spot) = {razon:.4f}: no hay AUDIT de este libro; sirve para ver la zona, no para comparar al punto")
        if fut is None or (razon is None and t.upper() != "NDX"):
            print("  falta --razon y --fut (no hay AUDIT en el log para sacarlos)")
            continue
        r = recalcular(d, fut, razon, apal, ahora, base=locals().get("base", 0.0))
        edad = (ahora - datetime.fromisoformat(r["generado"].replace("Z", "+00:00"))).total_seconds() / 60 if r["generado"] else float("nan")
        print(f"  cadena: generada {r['generado']} (hace {edad:.0f} min, +15 de CBOE)  ts CBOE {r['ts']}  spot {r['spot']:.2f}  "
              f"strikes 0DTE {r['strikes']}  mas cercano {r['mas_cerca']:.3f} d  S implicita {r['S']:.2f}")
        print(f"  propio: zero {r['zero']:.2f}  netVol {r['net_vol']/1e9:.3f}B")
        for i, p in enumerate(r["doms"]):
            print(f"          D{i+1} K={p[0]:.0f} -> fut {p[1]:.2f}  gexVol {p[2]/1e6:.0f}M")
        if r["mp"]:
            print(f"          major+ K={r['mp'][0]:.0f} -> {r['mp'][1]:.2f}   major- K={r['mn'][0]:.0f} -> {r['mn'][1]:.2f}" if r["mn"] else "")
        if r["conv_max"] and r["pico"]:
            print(f"          perfil derecho (convexidad por volumen): mayor |conv| K={r['conv_max'][0]:.0f} -> {r['conv_max'][1]:.2f} ({r['conv_max'][4]/1e6:+.0f}M); "
                  f"conv en el precio (+-{PICO_RADIO_PCT} %) {r['conv_precio']/1e6:+.0f}M; pico |GEX| K={r['pico'][0]:.0f} -> {r['pico'][1]:.2f}")
        if au:
            ks_log = sorted(round((x - base) if razon is None else (x / razon if apal == 1 else r["spot"] + apal * (x / razon - r["spot"]))) for x in au["doms"])
            ks_prop = sorted(round(p[0]) for p in r["doms"])
            dz = abs(r["zero"] - au["zeroVol"]) if not math.isnan(r["zero"]) and not math.isnan(au["zeroVol"]) else float("nan")
            print(f"  veredicto: dominantes por K log {ks_log} vs propio {ks_prop} -> {'COINCIDEN' if ks_log == ks_prop else 'DISTINTAS'}; "
                  f"zero difiere {dz:.2f} pts -> {'OK' if dz < 5 else 'REVISAR'}")
            if au["convPrecio"] == au["convPrecio"] and r["conv_precio"] == r["conv_precio"]:
                mismo_signo = (au["convPrecio"] >= 0) == (r["conv_precio"] >= 0)
                rel = abs(r["conv_precio"] - au["convPrecio"]) / max(1e-9, abs(au["convPrecio"]))
                print(f"  perfil derecho: conv en el precio log {au['convPrecio']/1e6:+.0f}M vs propio {r['conv_precio']/1e6:+.0f}M -> "
                      f"{'MISMO SIGNO' if mismo_signo else 'SIGNO DISTINTO'}, diferencia {rel*100:.0f} % "
                      f"({'OK' if mismo_signo and rel < 0.25 else 'REVISAR: cadena de otro minuto, precio distinto o tasa distinta'})")
            if au["pico"] == au["pico"] and r["pico"]:
                print(f"  pico |GEX| cerca del precio: log {au['pico']:.2f} vs propio {r['pico'][1]:.2f} -> {'COINCIDE' if abs(au['pico'] - r['pico'][1]) < 5 else 'DISTINTO'}")
    print("=" * 100)
    print("Magnitudes NO comparables entre capas (TQQQ lleva un factor x3/x9 que no esta medido): comparar strikes, no barras.")


if __name__ == "__main__":
    main()
