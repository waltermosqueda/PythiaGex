# -*- coding: utf-8 -*-
"""La tasa corta, medida, no escrita a mano.

Estaba fija en 4,0 % adentro del codigo. Medida contra el Tesoro de Estados
Unidos el 2026-08-28, la letra a 4 semanas rendia 3,72 % y la de 52 semanas
4,04 %. O sea: el numero fijo estaba 28 puntos basicos arriba en el tramo
corto y 4 abajo en el largo, y encima no tenia forma de enterarse cuando la
Reserva Federal moviera la tasa.

Eso importa porque la tasa entra en dos lugares:

  - el carry teorico contra el que se controla la base indice-futuro
  - la valuacion de vanna y de charm

Fuente: el feed oficial de Daily Treasury Bill Rates. Es XML, sin clave, y
publica el rendimiento equivalente a cupon por cada plazo. Se baja una vez
por dia y queda en cache.
"""
import datetime as dt
import json
import os
import re
import urllib.request

URL = ("https://home.treasury.gov/resource-center/data-chart-center/"
       "interest-rates/pages/xml?data=daily_treasury_bill_rates"
       "&field_tdr_date_value_month={:04d}{:02d}")

# Cada campo del feed y a cuantos dias corresponde.
PLAZOS = {"CS_4WK_YIELD_AVG": 28, "CS_6WK_YIELD_AVG": 42,
          "CS_8WK_YIELD_AVG": 56, "CS_13WK_YIELD_AVG": 91,
          "CS_17WK_YIELD_AVG": 119, "CS_26WK_YIELD_AVG": 182,
          "CS_52WK_YIELD_AVG": 364}

CACHE = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                     "datos", "cache")

def _bajar(anio, mes):
    req = urllib.request.Request(URL.format(anio, mes),
                                 headers={"User-Agent": "Mozilla/5.0"})
    return urllib.request.urlopen(req, timeout=25).read().decode("utf-8", "ignore")

def curva(hoy=None, forzar=False):
    """Ultima curva de letras publicada. Devuelve dias -> tasa decimal."""
    hoy = hoy or dt.date.today()
    os.makedirs(CACHE, exist_ok=True)
    ruta = os.path.join(CACHE, "tasas-%s.json" % hoy.isoformat())
    if os.path.exists(ruta) and not forzar:
        with open(ruta, encoding="utf-8") as f:
            return json.load(f)

    txt = None
    for delta in (0, 1):  # si el mes recien arranca, el ultimo dato esta en el anterior
        m = hoy.month - delta
        a = hoy.year
        if m < 1:
            m += 12
            a -= 1
        try:
            t = _bajar(a, m)
        except Exception:
            continue
        if "<entry>" in t:
            txt = t
            break
    if not txt:
        return None

    ent = txt.split("<entry>")[-1]
    fm = re.search(r"<d:INDEX_DATE[^>]*>([^<]+)", ent)
    tenores = {}
    for campo, dias in PLAZOS.items():
        m = re.search(r"<d:%s[^>]*>([^<]+)</d:" % campo, ent)
        if m:
            try:
                tenores[str(dias)] = round(float(m.group(1)) / 100.0, 6)
            except ValueError:
                pass
    if not tenores:
        return None

    out = {"fecha": (fm.group(1)[:10] if fm else None),
           "tenores": tenores,
           "fuente": "US Treasury Daily Bill Rates (coupon equivalent)",
           "bajado": dt.datetime.now(dt.timezone.utc).isoformat(timespec="seconds")}
    try:
        with open(ruta, "w", encoding="utf-8") as f:
            json.dump(out, f)
    except OSError:
        pass
    return out

def tasa(dias, c=None):
    """Tasa para un plazo, interpolando linealmente entre los que publica el
    Tesoro. Fuera de los extremos se toma el extremo, sin extrapolar."""
    c = c if c is not None else curva()
    if not c or not c.get("tenores"):
        return None
    pts = sorted((int(k), v) for k, v in c["tenores"].items())
    if dias <= pts[0][0]:
        return pts[0][1]
    if dias >= pts[-1][0]:
        return pts[-1][1]
    for (d0, r0), (d1, r1) in zip(pts, pts[1:]):
        if d0 <= dias <= d1:
            return round(r0 + (r1 - r0) * (dias - d0) / (d1 - d0), 6)
    return pts[-1][1]
