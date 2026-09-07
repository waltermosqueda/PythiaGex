# -*- coding: utf-8 -*-
"""CARGADOR DEL LABORATORIO DE FORMULAS.

Lee las fotos crudas de la cadena de SPX que quedaron archivadas en
datos/cache y las velas de un minuto de datos/historico, y las deja en una
forma comoda para probar formulas alternativas de dominantes.

NO calcula ninguna formula. Solo carga y valida. Si el cargador miente,
todo lo que venga despues miente.
"""
import json, io, gzip, glob, os, re
from datetime import datetime, timedelta

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CACHE = os.path.join(RAIZ, "datos", "cache")
HIST = os.path.join(RAIZ, "datos", "historico")

# SPX260918C00200000 -> vencimiento 2026-09-18, call, strike 200
RE_OPT = re.compile(r"^([A-Z]+)(\d{2})(\d{2})(\d{2})([CP])(\d{8})$")


def parsear_simbolo(s):
    m = RE_OPT.match(s or "")
    if not m:
        return None
    raiz, aa, mm, dd, cp, k = m.groups()
    try:
        # LAS OPCIONES NO VENCEN A MEDIANOCHE.
        #
        # Los SPXW diarios liquidan a las 16:00 de Nueva York, que en UTC -- que
        # es la hora de los sellos de las fotos -- son las 20:00.
        #
        # Con medianoche, el 0DTE VERDADERO daba tiempo NEGATIVO en cualquier
        # foto tomada despues de las 00:00 UTC y quedaba excluido: lo que
        # entraba como "0DTE" era en realidad el vencimiento del dia siguiente.
        # Una tabla entera salio mal por esto.
        venc = datetime(2000 + int(aa), int(mm), int(dd), 20, 0)
    except ValueError:
        return None
    return dict(raiz=raiz, venc=venc, call=(cp == "C"), K=int(k) / 1000.0)


def fotos(dia):
    """Las rutas de las fotos de un dia, ordenadas por hora."""
    pat = os.path.join(CACHE, "_SPX-%s-*.json.gz" % dia)
    return sorted(glob.glob(pat))


def leer_foto(ruta, dias_max=7.0, radio=250.0):
    """Una foto, reducida a lo que hace falta.

    Se recorta por vencimiento y por distancia al precio porque el archivo
    completo son 28.000 contratos y el 95 % no participa de ningun nivel.
    """
    try:
        with gzip.open(ruta, "rt", encoding="utf-8") as f:
            d = json.load(f)
    except Exception:
        return None
    dd = d.get("data") or {}
    spot = dd.get("current_price") or dd.get("close")
    if not spot:
        return None
    ts = d.get("timestamp")
    try:
        t = datetime.strptime(ts, "%Y-%m-%d %H:%M:%S")
    except Exception:
        return None

    filas = []
    for o in dd.get("options", []):
        p = parsear_simbolo(o.get("option"))
        if not p:
            continue
        if abs(p["K"] - spot) > radio:
            continue
        dias = (p["venc"] - t).total_seconds() / 86400.0
        if dias < 0 or dias > dias_max:
            continue
        oi = float(o.get("open_interest") or 0)
        vol = float(o.get("volume") or 0)
        if oi <= 0 and vol <= 0:
            continue
        filas.append(dict(
            K=p["K"], call=p["call"], dias=dias,
            oi=oi, vol=vol,
            iv=float(o.get("iv") or 0),
            gamma=float(o.get("gamma") or 0),
            delta=float(o.get("delta") or 0),
            vega=float(o.get("vega") or 0),
            bid=float(o.get("bid") or 0), ask=float(o.get("ask") or 0)))
    return dict(t=t, spot=float(spot), filas=filas, ruta=ruta)


def velas(dia_iso):
    """Velas de un minuto del dia, con maximo y minimo."""
    p = os.path.join(HIST, "precio-SPX-%s.json" % dia_iso)
    if not os.path.exists(p):
        return []
    d = json.load(io.open(p, encoding="utf-8"))
    out = []
    for v in d.get("velas", []):
        try:
            hh, mm = v["t"].split(":")
            out.append(dict(hora=int(hh) * 60 + int(mm),
                            o=float(v["o"]), h=float(v["h"]),
                            l=float(v["l"]), c=float(v["c"])))
        except Exception:
            continue
    out.sort(key=lambda x: x["hora"])
    return out


def _validar():
    print("=" * 66)
    print("VALIDACION DEL CARGADOR")
    print("=" * 66)
    for dia in ("20260903", "20260831"):
        fs = fotos(dia)
        print("%s : %d fotos" % (dia, len(fs)))
        if not fs:
            continue
        f = leer_foto(fs[len(fs) // 2])
        if not f:
            print("   no se pudo leer la del medio"); continue
        print("   foto del medio: %s   spot %.2f   contratos utiles %d"
              % (f["t"], f["spot"], len(f["filas"])))
        ks = sorted(set(x["K"] for x in f["filas"]))
        vs = sorted(set(round(x["dias"], 2) for x in f["filas"]))
        print("   strikes %d  (de %.0f a %.0f)   vencimientos %s"
              % (len(ks), ks[0], ks[-1], vs[:6]))
        oi = sum(x["oi"] for x in f["filas"])
        vo = sum(x["vol"] for x in f["filas"])
        cg = sum(1 for x in f["filas"] if x["gamma"] > 0)
        print("   interes abierto %.0f   volumen del dia %.0f   con gamma servida %d"
              % (oi, vo, cg))
    print()
    for d in ("2026-09-03", "2026-08-31"):
        v = velas(d)
        if v:
            print("velas %s : %d   rango del dia %.2f a %.2f"
                  % (d, len(v), min(x["l"] for x in v), max(x["h"] for x in v)))


if __name__ == "__main__":
    _validar()
