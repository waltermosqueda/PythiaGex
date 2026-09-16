# -*- coding: utf-8 -*-
"""
auditar_externo.py — LAS DOMINANTES DE CADA LIBRO CONTRA TRES FUENTES DE AFUERA, A MANO.

Para cada libro de las capas (QQQ, SPY, SPX, NDX) toma la ultima linea AUDIT del indicador (lo que ATAS dibuja) y la
compara con:
  1. CBOE crudo (cdn.cboe.com/delayed_quotes), con LAS GRIEGAS DE CBOE: GEX por volumen del vencimiento mas cercano
     usando la gamma que publica CBOE por contrato (no la nuestra), misma regla de dominantes (una por lado, radio 2 %,
     empate 20 %), llevada al futuro con la razon/base del log. Y la misma cuenta con NUESTRA gamma (Black-Scholes con
     la IV de CBOE) para medir cuanto difieren las griegas.
  2. InsiderFinance (__NEXT_DATA__ de la pagina): gamma e interes abierto por contrato, calculados por ellos. No traen
     volumen, asi que se compara la GAMMA por strike (la de ellos contra la nuestra) y los muros por INTERES ABIERTO.
  3. Yahoo (1 min): el spot del ETF/indice contra el current_price de CBOE.
Todo se imprime con la hora de cada dato (regla 1 y 4 del protocolo).

Uso:  python laboratorio/auditar_externo.py [QQQ SPY SPX NDX] [--dia 2026-09-16]
"""
import io
import json
import math
import os
import re
import statistics as st
import sys
import urllib.request
from datetime import datetime, timedelta, timezone

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import capas_nq as cq  # noqa: E402

LOG = os.path.join(os.environ.get("APPDATA", ""), "ATAS", "pythiagex-gammahoy.log")
CACHE = os.path.join(os.environ.get("TEMP", "."), "auditar_externo")
CBOE = {"QQQ": "QQQ", "SPY": "SPY", "SPX": "_SPX", "NDX": "_NDX"}
IFIN = {"QQQ": "QQQ", "SPY": "SPY", "SPX": "SPX", "NDX": "NDX"}
YAHOO = {"QQQ": "QQQ", "SPY": "SPY", "SPX": "^GSPC", "NDX": "^NDX"}
MULT = 100.0
UA = {"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/128 Safari/537.36"}


def bajar(url, nombre, texto=False):
    os.makedirs(CACHE, exist_ok=True)
    p = os.path.join(CACHE, nombre)
    if not (os.path.exists(p) and (datetime.now().timestamp() - os.path.getmtime(p)) < 600):
        data = urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=60).read()
        io.open(p, "wb").write(data)
    return io.open(p, encoding="utf-8", errors="replace").read()


def ultimo_audit(capa):
    ls = [l for l in io.open(LOG, encoding="utf-8", errors="replace") if f"AUDIT capa={capa} " in l]
    if not ls:
        return None
    l = ls[-1]
    def num(k):
        m = re.search(k + r"=(-?[0-9.]+)", l); return float(m.group(1)) if m else float("nan")
    mo = re.search(r"_x_razon_([0-9.]+)", l)
    doms = re.search(r"doms=(\S+)", l)
    return dict(hora=l[:19], fut=num("fut"), S=num("S"), razon=float(mo.group(1)) if mo else None, base=num("base"),
                zero=num("zeroVol"), mp=num("mpVol"), mn=num("mnVol"),
                doms=[(float(x.split("=")[0]), x.split("=")[1]) for x in doms.group(1).split("/")] if doms and doms.group(1) else [],
                cts=(re.search(r"cadenaTs=(\S+)", l) or [None, "?"])[1])


def hoy_ny():
    ny = datetime.now(timezone.utc) - timedelta(hours=4)
    return ny.date()


def elegir_doms(perfil, fut, idx):
    """la misma regla del indicador sobre un perfil [(K, fut, valor...)]: una por lado, radio 2 %, empate 20 % -> la mas cercana"""
    radio = fut * cq.RADIO_DOM_PCT / 100.0
    cerca = [p for p in perfil if abs(p[1] - fut) <= radio and p[idx] != 0]
    def elegir(lado):
        if not lado:
            return None
        pmax = max(abs(p[idx]) for p in lado); piso = pmax * (1 - cq.EMPATE_PCT / 100.0)
        return sorted([p for p in lado if abs(p[idx]) >= piso], key=lambda p: (abs(p[1] - fut), -abs(p[idx])))[0]
    out = [x for x in (elegir([p for p in cerca if p[1] > fut]), elegir([p for p in cerca if p[1] <= fut])) if x]
    return sorted(out, key=lambda p: -abs(p[idx]))


def cboe_perfil(ticker, au, ahora):
    raw = json.loads(bajar(f"https://cdn.cboe.com/api/global/delayed_quotes/options/{CBOE[ticker]}.json", f"cboe_{ticker}.json"))
    data = raw["data"]; spot = float(data.get("current_price") or 0); ts = raw.get("timestamp")
    pat = re.compile(r"^([A-Z]+)(\d{6})([CP])(\d{8})$")
    hoy = hoy_ny()
    por = {}
    vencs = set()
    for o in data["options"]:
        m = pat.match(o.get("option", ""))
        if not m:
            continue
        venc = datetime.strptime(m.group(2), "%y%m%d").date()
        if venc < hoy:
            continue
        vencs.add(venc)
    if not vencs:
        return None
    venc0 = min(vencs)
    exp_utc = datetime(venc0.year, venc0.month, venc0.day, 20, 0, tzinfo=timezone.utc)   # 16:00 Nueva York
    T = max((exp_utc - ahora).total_seconds() / 86400.0, cq.PISO_DIAS) / 365.0
    S = au["S"] if au and au["S"] == au["S"] and au["S"] > 0 else spot
    for o in data["options"]:
        m = pat.match(o.get("option", ""))
        if not m or datetime.strptime(m.group(2), "%y%m%d").date() != venc0:
            continue
        K = int(m.group(4)) / 1000.0; call = m.group(3) == "C"
        vol = float(o.get("volume") or 0); oi = float(o.get("open_interest") or 0)
        g_cboe = float(o.get("gamma") or 0); iv = float(o.get("iv") or 0)
        g_bs = cq.gamma_bs(S, K, T, iv, cq.TASA) if iv > 0 else 0.0
        e = por.setdefault(K, [0.0, 0.0, 0.0, 0.0, []])   # gexVol_cboe, gexVol_bs, gexOi_cboe, gexOi_bs, ratios
        sgn = 1 if call else -1
        e[0] += sgn * g_cboe * vol * MULT * S * S * 0.01
        e[1] += sgn * g_bs * vol * MULT * S * S * 0.01
        e[2] += sgn * g_cboe * oi * MULT * S * S * 0.01
        e[3] += sgn * g_bs * oi * MULT * S * S * 0.01
        if g_bs > 0 and g_cboe > 0 and vol > 0 and abs(K / S - 1) < 0.02:
            e[4].append(g_cboe / g_bs)
    return dict(ts=ts, spot=spot, S=S, venc=venc0, T=T, por=por)


def al_futuro(K, au):
    if au["razon"]:
        return K * au["razon"]
    return K + au["base"]


def insider_perfil(ticker, au):
    html = bajar(f"https://www.insiderfinance.io/gamma-exposure/{IFIN[ticker]}", f"if_{ticker}.html", texto=True)
    m = re.search(r'<script id="__NEXT_DATA__" type="application/json">(.*?)</script>', html, re.S)
    if not m:
        return None
    ini = json.loads(m.group(1))["props"]["pageProps"].get("initialData") or {}
    ops = ini.get("options") or []
    hoy = hoy_ny()
    vencs = sorted({datetime(o["expireYear"], o["expireMonth"], o["expireDay"]).date() for o in ops if datetime(o["expireYear"], o["expireMonth"], o["expireDay"]).date() >= hoy})
    if not vencs:
        return None
    v0 = vencs[0]
    por = {}
    for o in ops:
        if datetime(o["expireYear"], o["expireMonth"], o["expireDay"]).date() != v0:
            continue
        K = float(o["strike"]); call = o.get("cp") == "C"
        e = por.setdefault(K, [0.0, 0.0, 0.0])   # gamma call, gamma put, gexOi (con su gamma)
        g = float(o.get("gamma") or 0); oi = float(o.get("openInterest") or 0)
        if call: e[0] = g
        else: e[1] = g
        e[2] += (1 if call else -1) * g * oi * MULT * float(ini.get("spot") or 0) ** 2 * 0.01
    return dict(spot=ini.get("spot"), ts=ini.get("timestamp"), venc=v0, por=por)


def yahoo_ultimo(sym):
    try:
        u = f"https://query1.finance.yahoo.com/v8/finance/chart/{urllib.request.quote(sym)}?interval=1m&range=1d"
        d = json.loads(urllib.request.urlopen(urllib.request.Request(u, headers=UA), timeout=30).read())
        r = d["chart"]["result"][0]; q = r["indicators"]["quote"][0]["close"]; t = r["timestamp"]
        pares = [(tt, c) for tt, c in zip(t, q) if c is not None]
        return pares[-1] if pares else None
    except Exception:
        return None


def main():
    tickers = [a for a in sys.argv[1:] if not a.startswith("--")] or ["QQQ", "SPY", "SPX", "NDX"]
    ahora = datetime.now(timezone.utc)
    print(f"ahora {ahora.strftime('%Y-%m-%d %H:%M')}Z · regla: una por lado, radio {cq.RADIO_DOM_PCT} %, empate {cq.EMPATE_PCT} %, tasa {cq.TASA}")
    for t in tickers:
        au = ultimo_audit(t)
        print("=" * 110)
        if not au:
            print(f"{t}: sin AUDIT en el log"); continue
        print(f"{t}  ATAS (log {au['hora']}, cadena CBOE {au['cts']}): fut {au['fut']:.2f} S {au['S']:.2f} "
              + (f"razon {au['razon']}" if au['razon'] else f"base {au['base']:.2f}")
              + f" · D {' / '.join(f'{p:.1f} ({g})' for p, g in au['doms'])} · zero {au['zero']:.1f} · majors +{au['mp']:.1f} / -{au['mn']:.1f}")
        # 1) CBOE crudo con sus griegas
        try:
            c = cboe_perfil(t, au, ahora)
        except Exception as e:
            c = None; print(f"  CBOE crudo: no se pudo ({e})")
        if c:
            perfil_c = sorted((K, al_futuro(K, au), v[0], v[2]) for K, v in c["por"].items() if v[0] != 0 or v[2] != 0)
            perfil_b = sorted((K, al_futuro(K, au), v[1], v[3]) for K, v in c["por"].items() if v[1] != 0 or v[3] != 0)
            dc = elegir_doms(perfil_c, au["fut"], 2); db = elegir_doms(perfil_b, au["fut"], 2)
            ratios = [r for v in c["por"].values() for r in v[4]]
            print(f"  1) CBOE crudo {c['ts']} (spot {c['spot']:.2f}, vencimiento {c['venc']}, T {c['T']*365:.2f} dias):")
            print(f"     con la GAMMA DE CBOE:  D {' / '.join(f'{p[1]:.1f} (K {p[0]:g}, {p[2]/1e6:+.0f}M)' for p in dc)}")
            print(f"     con NUESTRA gamma:     D {' / '.join(f'{p[1]:.1f} (K {p[0]:g}, {p[2]/1e6:+.0f}M)' for p in db)}")
            if ratios:
                print(f"     gamma CBOE / nuestra, strikes a +-2 % con volumen: mediana {st.median(ratios):.3f} (n {len(ratios)})")
            ks_log = sorted(round(p / au['razon']) if au['razon'] else round(p - au['base']) for p, _ in au["doms"])
            ks_c = sorted(round(p[0]) for p in dc); ks_b = sorted(round(p[0]) for p in db)
            print(f"     strikes: ATAS {ks_log} · CBOE-griegas {ks_c} · nuestra {ks_b} -> "
                  + ("COINCIDEN los tres" if ks_log == ks_c == ks_b else ("ATAS = nuestra, CBOE-griegas distinto (griegas)" if ks_log == ks_b else "DIFIEREN: revisar")))
            # los muros por interes abierto (para cotejar con InsiderFinance)
            doi = elegir_doms(perfil_b, au["fut"], 3)
            print(f"     muros por INTERES ABIERTO (nuestra gamma, OI de CBOE): {' / '.join(f'{p[1]:.1f} (K {p[0]:g}, {p[3]/1e6:+.0f}M)' for p in doi)}")
        # 2) InsiderFinance
        try:
            i = insider_perfil(t, au)
        except Exception as e:
            i = None; print(f"  InsiderFinance: no se pudo ({e})")
        if i and c:
            perfil_i = sorted((K, al_futuro(K, au), v[2]) for K, v in i["por"].items() if v[2] != 0)
            di = elegir_doms(perfil_i, au["fut"], 2)
            rat = []
            for K, v in i["por"].items():
                if K in c["por"] and abs(K / c["S"] - 1) < 0.02:
                    # gamma nuestra al mismo strike (call): la reconstruyo con la IV de CBOE
                    pass
            print(f"  2) InsiderFinance {i['ts']} (spot {i['spot']}, vencimiento {i['venc']}): muros por INTERES ABIERTO con su gamma: "
                  + " / ".join(f"{p[1]:.1f} (K {p[0]:g}, {p[2]/1e6:+.0f}M)" for p in di))
            # gamma de ellos vs la nuestra, strike por strike (calls, +-2 %)
            comp = []
            S = c["S"]
            for K, v in i["por"].items():
                if abs(K / S - 1) >= 0.02 or v[0] <= 0:
                    continue
                # nuestra gamma con la IV de CBOE de ese strike (call)
                raw = c["por"].get(K)
                if not raw:
                    continue
                comp.append((K, v[0]))
            if comp:
                # comparo contra la gamma BS con la IV de InsiderFinance? no la tenemos aca: se compara con CBOE
                cb = json.loads(bajar(f"https://cdn.cboe.com/api/global/delayed_quotes/options/{CBOE[t]}.json", f"cboe_{t}.json"))
                pat = re.compile(r"^([A-Z]+)(\d{6})([CP])(\d{8})$")
                gc = {}
                for o in cb["data"]["options"]:
                    m = pat.match(o.get("option", ""))
                    if m and m.group(3) == "C" and datetime.strptime(m.group(2), "%y%m%d").date() == c["venc"]:
                        gc[int(m.group(4)) / 1000.0] = float(o.get("gamma") or 0)
                rr = [g / gc[K] for K, g in comp if gc.get(K, 0) > 0]
                if rr:
                    print(f"     gamma InsiderFinance / gamma CBOE (calls a +-2 %): mediana {st.median(rr):.3f} (n {len(rr)})")
        # 3) Yahoo
        y = yahoo_ultimo(YAHOO[t])
        if y and c:
            print(f"  3) Yahoo {YAHOO[t]} ultimo {datetime.fromtimestamp(y[0], tz=timezone.utc).strftime('%H:%M')}Z = {y[1]:.2f} vs CBOE current_price {c['spot']:.2f}: {(c['spot']/y[1]-1)*1e4:+.0f} pb")
    print("=" * 110)
    print("Como leerlo: si ATAS = nuestra gamma = griegas de CBOE en los mismos strikes, la dominante no depende de quien calcule la gamma;")
    print("los muros por interes abierto son OTRO objeto (posiciones abiertas de ayer) y sirven para cotejar con InsiderFinance, que no trae volumen.")


if __name__ == "__main__":
    main()
