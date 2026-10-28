# -*- coding: utf-8 -*-
r"""grabar_flujo_cboe.py — 28-10-2026. Pedido del operador: "quiero las barras de convexidad" (las DERECHAS de Gammalito, cian/violeta).
Lo ya medido: ninguna griega estatica x volumen/OI acierta su color (3.561 combinaciones en septiembre, 1.545 el 18-09, 8 familias el 27-10);
el autor dice usar QuantData "net-cmf 1m + exposure-by-strike" = flujo FIRMADO por agresor. CBOE no da el agresor, pero su JSON crudo trae
por opcion bid / ask / ultimo precio / hora / tick: con fotos seguidas el volumen nuevo de cada opcion se firma (ultimo >= ask compra, <= bid
venta, entre medio por el lado del medio y si empata por el tick). Este script SOLO GRABA (para medir despues contra capturas de Gammalito).

Graba cada ~90 s SPX (_SPX) y SPY: solo opciones con volumen y vencimiento dentro de DIAS_MAX dias, compacto:
  {"ts": sello de CBOE (UTC), "spot": ..., "o": {simbolo: [vol, bid, ask, ultimo, "HH:MM:SS" del ultimo, tick 1/0/-1, iv, oi]}}
en %APPDATA%\ATAS\PythiaGex5\flujo\flujo-<SPX|SPY>-<dia>.jsonl.gz. Descarga comprimida, leida de a trozos de 64 KB con pausa (regla del
proyecto: sin rafagas; el 16-09 las rafagas coincidian con cortes de Rithmic). Termina solo a la hora --hasta (NY). Solo lee CBOE."""
import os, sys, io, gzip, json, time, datetime, urllib.request, argparse
from zoneinfo import ZoneInfo
NY = ZoneInfo("America/New_York")
URL = {"SPX": "https://cdn.cboe.com/api/global/delayed_quotes/options/_SPX.json",
       "SPY": "https://cdn.cboe.com/api/global/delayed_quotes/options/SPY.json"}
DIAS_MAX = {"SPX": 0, "SPY": 7}
CARPETA = os.path.join(os.environ["APPDATA"], "ATAS", "PythiaGex5", "flujo")
TICK = {"up": 1, "down": -1}


def bajar(url, pausa_trozo=0.03):
    req = urllib.request.Request(url, headers={"Accept-Encoding": "gzip", "User-Agent": "Mozilla/5.0"})
    with urllib.request.urlopen(req, timeout=90) as r:
        buf = io.BytesIO()
        while True:
            ch = r.read(65536)
            if not ch: break
            buf.write(ch); time.sleep(pausa_trozo)   # ~2 MB/s como mucho, sin rafaga
        raw = buf.getvalue()
        if r.headers.get("Content-Encoding") == "gzip": raw = gzip.decompress(raw)
    return json.loads(raw)


def vence(sim, raiz):
    # SPXW261028C07735000 / SPX261120... / SPY261028P00770000 -> fecha
    i = len(raiz)
    while not sim[i].isdigit(): i += 1
    return datetime.date(2000 + int(sim[i:i + 2]), int(sim[i + 2:i + 4]), int(sim[i + 4:i + 6]))


def foto(sym, hoy):
    d = bajar(URL[sym])
    data = d["data"]; o = {}
    raiz = "SPXW" if sym == "SPX" else "SPY"
    for x in data["options"]:
        s = x["option"]; v = x.get("volume") or 0
        if v <= 0: continue
        r0 = "SPXW" if s.startswith("SPXW") else ("SPX" if s.startswith("SPX") else "SPY")
        if sym == "SPX" and r0 != "SPXW": continue
        try: dv = (vence(s, r0) - hoy).days
        except Exception: continue
        if dv < 0 or dv > DIAS_MAX[sym]: continue
        lt = (x.get("last_trade_time") or "")[11:19]
        o[s] = [v, x.get("bid"), x.get("ask"), x.get("last_trade_price"), lt, TICK.get(x.get("tick"), 0), x.get("iv"), x.get("open_interest")]
    return {"ts": d.get("timestamp"), "spot": data.get("current_price"), "o": o}


def main():
    ap = argparse.ArgumentParser(); ap.add_argument("--cada", type=float, default=90); ap.add_argument("--hasta", default="16:20")
    a = ap.parse_args()
    os.makedirs(CARPETA, exist_ok=True)
    hh, mm = map(int, a.hasta.split(":"))
    ult = {}
    while True:
        ahora = datetime.datetime.now(NY)
        if (ahora.hour, ahora.minute) >= (hh, mm): print("fin", ahora.strftime("%H:%M NY"), flush=True); return
        t0 = time.time(); hoy = ahora.date()
        for sym in ("SPX", "SPY"):
            try:
                f = foto(sym, hoy)
                if f["ts"] == ult.get(sym): continue          # mismo sello de CBOE: no se repite
                ult[sym] = f["ts"]
                p = os.path.join(CARPETA, "flujo-%s-%s.jsonl.gz" % (sym, hoy.isoformat()))
                with gzip.open(p, "at", encoding="utf-8") as g: g.write(json.dumps(f, separators=(",", ":")) + "\n")
                print(ahora.strftime("%H:%M:%S NY"), sym, "sello", f["ts"], "opciones", len(f["o"]), flush=True)
            except Exception as e:
                print(ahora.strftime("%H:%M:%S NY"), sym, "ERROR", repr(e)[:200], flush=True)
        time.sleep(max(5.0, a.cada - (time.time() - t0)))


if __name__ == "__main__":
    main()
