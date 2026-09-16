"""
auditar_estela.py — ¿LAS DOMINANTES (Y SU ESTELA) ESTAN BIEN UBICADAS? Auditoria minuto a minuto, con fuente externa.

Para cada linea AUDIT de cada capa que dejo el indicador hoy (una por minuto: precio del futuro, razon o base usada,
dominantes, zero), hace tres cosas:
  1. INTERNO: toma la cadena archivada de ESE minuto (la misma que tuvo el indicador) y recalcula las dominantes
     con la razon/base que el indicador dice haber usado -> los strikes tienen que coincidir.
  2. EXTERNO: contrasta el spot que traia la cadena (CBOE) con el precio de Yahoo del mismo minuto, y el precio del
     futuro que uso el indicador con NQ=F de Yahoo del mismo minuto -> asi se ve si la razon (futuro/spot) esta bien
     alineada o si el grafico y Yahoo hablan de contratos distintos (septiembre vs diciembre = ~300 pts).
  3. DISTANCIA: cuanto por arriba y por abajo del precio quedaron D1 y D2 en cada minuto -> si "la estela esta muy
     arriba", aca se ve con numeros (mediana y cuartiles por capa), no a ojo.

Uso:  python laboratorio/auditar_estela.py [QQQ SPX SPY NDX] [--dia 2026-09-15] [--cada 1]
"""
import gzip
import io
import json
import math
import os
import re
import sys
import urllib.request
from datetime import datetime, timezone, timedelta

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import capas_nq as cq

LOG = os.path.join(os.environ.get("APPDATA", ""), "ATAS", "pythiagex-gammahoy.log")
CADENAS = os.path.join(os.environ.get("APPDATA", ""), "ATAS", "PythiaGex", "cadenas")
ARCHIVO = {"SPX": "ES", "NDX": "NQ"}
YAHOO = {"QQQ": "QQQ", "SPY": "SPY", "SPX": "^GSPC", "NDX": "^NDX"}
RETRASO = 902


def yahoo_1m(sym):
    u = f"https://query1.finance.yahoo.com/v8/finance/chart/{urllib.request.quote(sym)}?interval=1m&range=1d"
    d = json.loads(urllib.request.urlopen(urllib.request.Request(u, headers={"User-Agent": "Mozilla/5.0"}), timeout=30).read())
    r = d["chart"]["result"][0]
    return {int(t // 60): c for t, c in zip(r["timestamp"], r["indicators"]["quote"][0]["close"]) if c is not None}


def cerca(serie, minuto, tol=3):
    for k in range(tol + 1):
        for m in (minuto - k, minuto + k):
            if m in serie:
                return serie[m]
    return None


def cargar_cadenas(ticker, dia):
    """las cadenas archivadas del dia (gz de la nube + jsonl local), ordenadas por 'generado'"""
    raiz = ARCHIVO.get(ticker, ticker)
    salida = []
    for nombre, abrir in ((f"cadena-{raiz}-{dia}.jsonl.gz", lambda p: gzip.open(p, "rt", encoding="utf-8")),
                          (f"local-{raiz}-{dia}.jsonl", lambda p: io.open(p, encoding="utf-8", errors="replace"))):
        p = os.path.join(CADENAS, nombre)
        if not os.path.exists(p):
            continue
        try:
            with abrir(p) as f:
                for l in f:
                    l = l.strip()
                    if len(l) < 40:
                        continue
                    try:
                        d = json.loads(l)
                    except Exception:
                        continue
                    g = d.get("generado")
                    if not g or "cadena" not in d:
                        continue
                    salida.append((datetime.fromisoformat(g.replace("Z", "+00:00")), d))
        except Exception as e:
            print(f"  no pude leer {nombre}: {e}")
    salida.sort(key=lambda x: x[0])
    # una por 'generado' (el gz y el local pueden repetir)
    unicas, vistos = [], set()
    for g, d in salida:
        k = g.replace(microsecond=0)
        if k in vistos:
            continue
        vistos.add(k); unicas.append((g, d))
    return unicas


ZONA_LOG = datetime.now().astimezone().tzinfo    # la zona del log = la de la maquina que corre ATAS (UTC-3 en esta)


def lineas_audit(ticker, dia):
    pat = re.compile(r"^(\S+)\s+AUDIT capa=" + re.escape(ticker) + r" (.*)$")
    out = []
    with open(LOG, encoding="utf-8", errors="replace") as f:
        for l in f:
            m = pat.match(l.rstrip())
            if not m or not m.group(1).startswith(dia):
                continue
            ts = datetime.fromisoformat(m.group(1)).replace(tzinfo=ZONA_LOG)   # el log va en hora LOCAL de la maquina (UTC-3 aca, medido)
            r = m.group(2)
            def num(k):
                mm = re.search(k + r"=(-?[0-9.]+|NaN)", r)
                return float(mm.group(1)) if mm and mm.group(1) != "NaN" else float("nan")
            mo = re.search(r"origen=libro_(?:CBOE_|Rithmic_)[A-Z]+(?:_x\d+|_\(grabado\))?_x_razon_([0-9.]+)", r)
            ap = re.search(r"apal_([0-9.]+)x", r)
            doms = re.search(r"doms=(\S+)", r)
            cts = re.search(r"cadenaTs=(\S+)", r); gen = re.search(r" gen=(\S+)", r); hor = re.search(r"horizonte=(\w+)", r)
            out.append(dict(cadenaTs=cts.group(1).replace("_", " ") if cts else None, gen=gen.group(1) if gen else None,
                            horizonte=hor.group(1) if hor else None,
                            ts=ts.astimezone(timezone.utc), fut=num("fut"), razon=float(mo.group(1)) if mo else None,
                            base=num("base") if not mo else 0.0, apal=float(ap.group(1)) if ap else 1.0,
                            edad=num("edadFeed"), zero=num("zeroVol"),
                            doms=[float(x.split("=")[0]) for x in doms.group(1).split("/")] if doms and doms.group(1) else []))
    return out


def q(xs, p):
    if not xs:
        return float("nan")
    xs = sorted(xs); i = (len(xs) - 1) * p
    lo, hi = int(math.floor(i)), int(math.ceil(i))
    return xs[lo] + (xs[hi] - xs[lo]) * (i - lo)


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    tickers = args or ["QQQ", "SPX", "SPY", "NDX"]
    dia = next((sys.argv[i + 1] for i, a in enumerate(sys.argv) if a == "--dia"), datetime.now().strftime("%Y-%m-%d"))
    cada = int(next((sys.argv[i + 1] for i, a in enumerate(sys.argv) if a == "--cada"), "1"))
    if "--sin-lado" in sys.argv: cq.UNA_POR_LADO = False        # hipotesis: el grafico no tiene "una dominante por lado"
    emp = next((sys.argv[i + 1] for i, a in enumerate(sys.argv) if a == "--empate"), None)
    if emp is not None: cq.EMPATE_PCT = float(emp)             # hipotesis: otro % de empate tecnico
    global ZONA_LOG
    off = next((sys.argv[i + 1] for i, a in enumerate(sys.argv) if a == "--utc-offset"), None)
    if off is not None: ZONA_LOG = timezone(timedelta(hours=float(off)))   # log de otra maquina
    hor = next((sys.argv[i + 1] for i, a in enumerate(sys.argv) if a == "--horizonte"), None)
    if hor is not None: cq.HORIZONTE = hor                     # para lineas AUDIT viejas, sin horizonte= (antes de 1.10b)
    desde = next((sys.argv[i + 1] for i, a in enumerate(sys.argv) if a == "--desde"), "00:00")
    hasta = next((sys.argv[i + 1] for i, a in enumerate(sys.argv) if a == "--hasta"), "23:59")
    print(f"log en hora local {ZONA_LOG}; horizonte por defecto {cq.HORIZONTE}; ventana {desde}-{hasta} (hora del log)")
    print(f"regla de dominantes usada: una por lado={cq.UNA_POR_LADO}, empate {cq.EMPATE_PCT} %, radio {cq.RADIO_DOM_PCT} %, tasa {cq.TASA}")
    try:
        nq = yahoo_1m("NQ=F"); print(f"Yahoo NQ=F: {len(nq)} minutos, ultimo {max(nq.items())[1]:.2f}")
    except Exception as e:
        nq = {}; print("Yahoo NQ=F no disponible:", str(e)[:80])
    for t in tickers:
        print("=" * 110)
        au = lineas_audit(t, dia)
        cad = cargar_cadenas(t, dia)
        print(f"{t}: {len(au)} lineas AUDIT del {dia}, {len(cad)} cadenas archivadas")
        if not au or not cad:
            continue
        try:
            ext = yahoo_1m(YAHOO.get(t, t))
        except Exception as e:
            ext = {}; print(f"  Yahoo {YAHOO.get(t, t)} no disponible: {str(e)[:60]}")
        n = coinciden = 0
        dif_spot, dif_fut, dif_razon, arriba, abajo, sin_cadena = [], [], [], [], [], 0
        dif_fut_dic = []
        ejemplos = []
        au = [a for a in au if desde <= a["ts"].astimezone(ZONA_LOG).strftime("%H:%M") <= hasta]
        for i, a in enumerate(au):
            if i % cada:
                continue
            # la cadena que tenia el indicador. Desde 1.10b la capa anota cadenaTs= (sello de CBOE) y gen= (generado):
            # se busca ESA en el archivo local (que desde 1.10b guarda cada version bajada). Antes: la ultima
            # generada antes del AUDIT, y solo si su edad coincide con edadFeed (que era la edad al generarse, no hasta ahora)
            if a.get("horizonte"):
                cq.HORIZONTE = a["horizonte"]
            if a.get("cadenaTs"):
                exactas = [c for c in cad if c[1].get("cadena_ts") == a["cadenaTs"] and (not a.get("gen") or c[0].strftime("%H:%M:%S") == a["gen"])]
                if not exactas:
                    sin_cadena += 1; continue
                g, d = exactas[-1]
                edad_arch = (a["ts"] - g).total_seconds() / 60.0
            else:
                prev = [c for c in cad if c[0] <= a["ts"]]
                if not prev:
                    sin_cadena += 1; continue
                g, d = prev[-1]
                edad_arch = (a["ts"] - g).total_seconds() / 60.0
                if (a["ts"] - g).total_seconds() > 20 * 60 or (a["edad"] == a["edad"] and abs(edad_arch - a["edad"]) > 1.5):
                    sin_cadena += 1; continue   # el archivo no tiene la cadena que el indicador tenia ese minuto
            if a["razon"] is None and t != "NDX":
                continue
            try:
                r = cq.recalcular(d, a["fut"], a["razon"], a["apal"], a["ts"], base=a["base"] if a["razon"] is None else 0.0)
            except Exception as e:
                print("  error recalculando", a["ts"], str(e)[:60]); continue
            n += 1
            # 1) interno: strikes de las dominantes
            spot = float(d["cadena"].get("spot_idx") or 0)
            def a_k(x):
                if a["razon"] is None: return round(x - a["base"])
                return round(x / a["razon"] if a["apal"] == 1 else spot + a["apal"] * (x / a["razon"] - spot))
            ks_log = sorted(a_k(x) for x in a["doms"]); ks_prop = sorted(round(p[0]) for p in r["doms"])
            f_log = sorted(a["doms"]); f_prop = sorted(p[1] for p in r["doms"])
            ok = len(f_log) == len(f_prop) and all(abs(x - y) <= 15 for x, y in zip(f_log, f_prop))
            coinciden += ok
            if not ok and len(ejemplos) < 4:
                ejemplos.append(f"    {a['ts'].astimezone(ZONA_LOG).strftime('%H:%M')} log {ks_log} ({[round(x) for x in f_log]}) vs propio {ks_prop} ({[round(x) for x in f_prop]}) (cadena {g.strftime('%H:%M:%S')}Z, edad {edad_arch:.1f} min)")
            # 2) externo: el spot de la cadena vs Yahoo al minuto del ts de CBOE; el futuro vs NQ=F al minuto del AUDIT
            ts_cboe = d["cadena"].get("ts")
            if ts_cboe and ext and spot > 0:
                mcb = datetime.strptime(ts_cboe, "%Y-%m-%d %H:%M:%S").replace(tzinfo=timezone.utc)
                y = cerca(ext, int((mcb - timedelta(seconds=RETRASO)).timestamp() // 60), 2)
                if y: dif_spot.append((spot - y) / y * 1e4)          # puntos basicos
                ynq = cerca(nq, int((mcb - timedelta(seconds=RETRASO)).timestamp() // 60), 2) if nq else None
                if y and ynq and a["razon"] is not None and t in ("QQQ", "SPY"):
                    dif_razon.append((a["razon"] - ynq / y) / (ynq / y) * 1e4)   # pb: la razon del indicador vs Yahoo NQ/ETF alineados
            if nq:
                ynq2 = cerca(nq, int(a["ts"].timestamp() // 60), 2)
                if ynq2: (dif_fut if a["ts"] < a["ts"].replace(hour=23, minute=22, second=0) else dif_fut_dic).append(a["fut"] - ynq2)
            # 3) distancia de las dominantes al precio
            for x in a["doms"]:
                (arriba if x > a["fut"] else abajo).append(x - a["fut"])
        print(f"  1) INTERNO: {coinciden} de {n} minutos con las dominantes a menos de 15 pts del recalculo (misma cadena); {sin_cadena} minutos sin esa cadena en el archivo"
              + (" (la capa no anotaba cadenaTs/gen: apareado por minuto; desde 1.10b es exacto)" if not any(a.get("cadenaTs") for a in au) else " (falta en el archivo local)"))
        for e in ejemplos: print(e)
        if dif_spot:
            print(f"  2) EXTERNO spot de la cadena vs Yahoo {YAHOO.get(t, t)} (alineado 902 s): mediana {q(dif_spot, .5):+.1f} pb, cuartiles {q(dif_spot, .25):+.1f} / {q(dif_spot, .75):+.1f} pb (n {len(dif_spot)})")
        if dif_razon:
            print(f"     razon del indicador vs NQ=F/{YAHOO.get(t, t)} de Yahoo al mismo minuto: mediana {q(dif_razon, .5):+.1f} pb, cuartiles {q(dif_razon, .25):+.1f} / {q(dif_razon, .75):+.1f} pb (n {len(dif_razon)})")
        if dif_fut:
            print(f"     precio del futuro del indicador vs NQ=F de Yahoo, ANTES del roll del grafico (chart en septiembre): mediana {q(dif_fut, .5):+.1f} pts (n {len(dif_fut)}) -> ~-300 = Yahoo ya estaba en diciembre")
        if dif_fut_dic:
            print(f"     precio del futuro del indicador vs NQ=F de Yahoo, DESPUES del roll (chart en diciembre): mediana {q(dif_fut_dic, .5):+.1f} pts, cuartiles {q(dif_fut_dic, .25):+.1f} / {q(dif_fut_dic, .75):+.1f} (n {len(dif_fut_dic)})")
        if arriba or abajo:
            print(f"  3) DISTANCIA al precio: D arriba mediana {q(arriba, .5):+.0f} pts (cuartiles {q(arriba, .25):+.0f} / {q(arriba, .75):+.0f}, n {len(arriba)}); "
                  f"D abajo mediana {q(abajo, .5):+.0f} pts (cuartiles {q(abajo, .25):+.0f} / {q(abajo, .75):+.0f}, n {len(abajo)})")
    print("=" * 110)
    print("Como leerlo: 1) si los strikes coinciden, la estela dibuja lo que la cuenta da; 2) si el spot y la razon estan a pocos pb de Yahoo,")
    print("la traduccion a NQ esta alineada; 3) la distancia dice si D1/D2 abrazan el precio o quedan lejos (y de que lado).")


if __name__ == "__main__":
    main()
