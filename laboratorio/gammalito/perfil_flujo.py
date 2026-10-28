# -*- coding: utf-8 -*-
r"""perfil_flujo.py — 28-10-2026. Arma, desde lo grabado por grabar_flujo_cboe.py, el perfil por strike con el volumen FIRMADO y lo compara
con las barras DERECHAS de Gammalito digitalizadas (signo cian + / violeta -, largo relativo a la mas larga de su instancia).

Firma de cada aumento de volumen de una opcion entre dos fotos seguidas: ultimo >= ask -> compra; ultimo <= bid -> venta; si no, del lado del
medio; en el medio exacto, por el tick (up compra, down venta, igual: sin firmar). Se acumula por opcion desde la primera foto (o desde --desde).
Candidatas por strike (S = spot de la foto, gamma y delta Black-Scholes con la IV de la opcion):
  flujo_c - flujo_p (contratos), gamma x (flujoC - flujoP), -(gamma x (flujoC + flujoP)) (gamma del dealer: corto lo que el cliente compra),
  delta x flujo (exposicion direccional del cliente), y las mismas con solo las ultimas --ventana minutos.
Uso: python -I perfil_flujo.py --hasta 15:45 --obs obs.json      (obs.json: {"SPX": {"7725": -1.0, ...}, "SPY": {"771": -1.0, ...}})"""
import os, sys, gzip, json, math, argparse, datetime
CARPETA = os.path.join(os.environ["APPDATA"], "ATAS", "PythiaGex5", "flujo")

def N(x): return math.exp(-0.5 * x * x) / math.sqrt(2 * math.pi)
def Phi(x): return 0.5 * (1 + math.erf(x / math.sqrt(2)))

def parse(sim):
    r0 = "SPXW" if sim.startswith("SPXW") else ("SPX" if sim.startswith("SPX") else "SPY")
    i = len(r0); f = datetime.date(2000 + int(sim[i:i + 2]), int(sim[i + 2:i + 4]), int(sim[i + 4:i + 6]))
    return f, sim[i + 6], int(sim[i + 7:]) / 1000.0

def firmar(dv, bid, ask, ult, tick):
    if dv <= 0 or ult is None: return 0.0
    if ask and ult >= ask: return dv
    if bid is not None and ult <= bid: return -dv
    if bid and ask:
        m = (bid + ask) / 2
        if ult > m: return dv
        if ult < m: return -dv
    return dv * tick

def cargar(sym, dia, hasta, ventana):
    p = os.path.join(CARPETA, "flujo-%s-%s.jsonl.gz" % (sym, dia))
    fs = [json.loads(x) for x in gzip.open(p, "rt", encoding="utf-8")]
    fs = [f for f in fs if f["ts"][11:16] <= hasta] if hasta else fs
    prev = {}; acum = {}; rec = {}; t_fin = fs[-1]["ts"]
    tf = datetime.datetime.fromisoformat(t_fin)
    for f in fs:
        t = datetime.datetime.fromisoformat(f["ts"])
        for s, v in f["o"].items():
            vol, bid, ask, ult, lt, tick = v[0], v[1], v[2], v[3], v[4], v[5]
            if s in prev:
                x = firmar(vol - prev[s], bid, ask, ult, tick)
                acum[s] = acum.get(s, 0.0) + x
                if (tf - t).total_seconds() <= ventana * 60: rec[s] = rec.get(s, 0.0) + x
            prev[s] = vol
    return fs[-1], acum, rec, len(fs)

def perfil(ult, flujo, hoy, solo_0dte):
    S = ult["spot"]; out = {}
    for s, x in flujo.items():
        if x == 0 or s not in ult["o"]: continue
        f, cp, K = parse(s)
        if solo_0dte and f != hoy: continue
        iv = ult["o"][s][6] or 0
        T = max((f - hoy).days + 0.25, 1 / 1440 * 60) / 365   # 0DTE: lo que falta del dia, aprox.
        if iv <= 0: continue
        sq = iv * math.sqrt(T); d1 = (math.log(S / K) + 0.5 * iv * iv * T) / sq; g = N(d1) / (S * sq)
        dl = Phi(d1) if cp == "C" else Phi(d1) - 1
        e = out.setdefault(K, {"neto_cp": 0, "g_cmp": 0, "g_dealer": 0, "delta": 0})
        e["neto_cp"] += x if cp == "C" else -x
        e["g_cmp"] += g * (x if cp == "C" else -x)
        e["g_dealer"] += -g * x
        e["delta"] += dl * x
    return out

def juzgar(obs, prof, nom):
    for k in ("neto_cp", "g_cmp", "g_dealer", "delta"):
        xs = [(o, prof.get(K, {}).get(k, 0.0)) for K, o in obs.items()]
        mx = max(abs(v) for _, v in xs) or 1
        sg = sum(1 for o, v in xs if (o > 0) == (v > 0))
        ob = [o for o, _ in xs]; rel = [v / mx for _, v in xs]
        mo = sum(ob) / len(ob); mr = sum(rel) / len(rel)
        va = sum((a - mo) ** 2 for a in ob); vb = sum((b - mr) ** 2 for b in rel)
        rr = sum((a - mo) * (b - mr) for a, b in zip(ob, rel)) / math.sqrt(va * vb) if va and vb else float("nan")
        print("  %-9s %-9s signos %2d/%d (invertido %2d) r %+.2f | %s" % (nom, k, sg, len(xs), len(xs) - sg, rr, " ".join("%+.2f" % v for v in rel)))

def main():
    ap = argparse.ArgumentParser(); ap.add_argument("--dia", default=datetime.date.today().isoformat()); ap.add_argument("--hasta", default="")
    ap.add_argument("--obs", required=True); ap.add_argument("--ventana", type=float, default=30)
    a = ap.parse_args(); obs = json.load(open(a.obs, encoding="utf-8")); hoy = datetime.date.fromisoformat(a.dia)
    for sym, solo in (("SPX", True), ("SPY", False)):
        if sym not in obs: continue
        o = {float(k): v for k, v in obs[sym].items()}
        ult, acum, rec, n = cargar(sym, a.dia, a.hasta, a.ventana)
        print("%s: %d fotos hasta %s (dato CBOE, UTC), spot %s" % (sym, n, ult["ts"], ult["spot"]))
        juzgar(o, perfil(ult, acum, hoy, solo), "acumulado")
        juzgar(o, perfil(ult, rec, hoy, solo), "ult%dmin" % a.ventana)
        if sym == "SPY": juzgar(o, perfil(ult, acum, hoy, True), "acum0DTE")

if __name__ == "__main__":
    main()
