# -*- coding: utf-8 -*-
"""BAJAR DE DATABENTO SIN PASARSE DEL CREDITO.

La cuenta tiene USD 125 de credito gratis (vence 2026-10-05) y ademas una
tarjeta cargada con "uso sin limite": si el credito se agota, lo que siga se
cobra. Por eso ACA hay un techo duro (TOPE_USD) y un libro contable: antes
de bajar se pide el costo exacto a la API, se suma a lo ya gastado, y si se
pasa del techo no se baja. Cada descarga queda anotada en el ledger con su
costo, para que el operador vea en que se fue cada centavo.

La clave vive fuera del repo: %APPDATA%\\PythiaGex\\databento.key

Uso:
  python herramientas/databento_bajar.py cotizar  GLBX.MDP3 ohlcv-1m ES.FUT parent 2026-06-01 2026-09-05
  python herramientas/databento_bajar.py bajar    GLBX.MDP3 ohlcv-1m ES.FUT parent 2026-06-01 2026-09-05
  python herramientas/databento_bajar.py ledger
"""
import datetime as dt
import io
import json
import os
import sys

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DESTINO = os.path.join(RAIZ, "datos", "databento")
LEDGER = os.path.join(DESTINO, "ledger.jsonl")
TOPE_USD = 60.0          # de 125 de credito: la mitad queda para lo que decida el operador
CLAVE = os.path.join(os.environ.get("APPDATA", ""), "PythiaGex", "databento.key")


def cliente():
    import databento as db
    key = io.open(CLAVE, encoding="utf-8").read().strip()
    return db.Historical(key)


def gastado():
    total = 0.0
    if os.path.exists(LEDGER):
        for l in io.open(LEDGER, encoding="utf-8"):
            try:
                total += float(json.loads(l).get("usd", 0))
            except Exception:
                pass
    return total


def anotar(reg):
    os.makedirs(DESTINO, exist_ok=True)
    with io.open(LEDGER, "a", encoding="utf-8") as f:
        f.write(json.dumps(reg, ensure_ascii=False) + "\n")


def nombre(dataset, schema, symbols, start, end):
    sym = symbols if isinstance(symbols, str) else "+".join(symbols)
    sym = sym.replace(".", "_").replace("/", "_")
    return os.path.join(DESTINO, dataset.replace(".", "_"), schema, "%s-%s-%s.dbn.zst" % (sym, start, end))


def cotizar(c, dataset, schema, symbols, stype, start, end):
    kw = dict(dataset=dataset, symbols=symbols, stype_in=stype, schema=schema, start=start, end=end)
    usd = float(c.metadata.get_cost(**kw))
    n = int(c.metadata.get_record_count(**kw))
    mb = float(c.metadata.get_billable_size(**kw)) / 1e6
    return usd, n, mb


def bajar(c, dataset, schema, symbols, stype, start, end, forzar=False):
    ruta = nombre(dataset, schema, symbols, start, end)
    if os.path.exists(ruta) and not forzar:
        return ruta, 0.0, "ya estaba"
    usd, n, mb = cotizar(c, dataset, schema, symbols, stype, start, end)
    ya = gastado()
    if ya + usd > TOPE_USD:
        raise SystemExit("NO SE BAJA: costaria USD %.4f y ya van USD %.4f; el techo es USD %.2f" % (usd, ya, TOPE_USD))
    os.makedirs(os.path.dirname(ruta), exist_ok=True)
    data = c.timeseries.get_range(dataset=dataset, symbols=symbols, stype_in=stype, schema=schema,
                                  start=start, end=end, path=ruta)
    anotar({"t": dt.datetime.now(dt.timezone.utc).isoformat(timespec="seconds"), "dataset": dataset,
            "schema": schema, "symbols": symbols, "stype": stype, "start": start, "end": end,
            "usd": round(usd, 4), "registros": n, "mb": round(mb, 1), "archivo": os.path.relpath(ruta, RAIZ)})
    return ruta, usd, "%s registros, %.1f MB" % ("{:,}".format(n), mb)


def main():
    a = sys.argv[1:]
    if not a or a[0] == "ledger":
        if os.path.exists(LEDGER):
            for l in io.open(LEDGER, encoding="utf-8"):
                r = json.loads(l)
                print("%s  USD %7.4f  %-11s %-10s %-22s %s..%s" % (r["t"][:16], r["usd"], r["dataset"], r["schema"],
                      r["symbols"] if isinstance(r["symbols"], str) else "+".join(r["symbols"]), r["start"], r["end"]))
        print("GASTADO: USD %.4f de un techo de %.2f (credito total 125)" % (gastado(), TOPE_USD))
        return
    orden, dataset, schema, syms, stype, start, end = a[0], a[1], a[2], a[3], a[4], a[5], a[6]
    symbols = syms.split(",") if "," in syms else syms
    c = cliente()
    if orden == "cotizar":
        usd, n, mb = cotizar(c, dataset, schema, symbols, stype, start, end)
        print("USD %.4f  %s registros  %.1f MB  (gastado hasta ahora USD %.4f)" % (usd, "{:,}".format(n), mb, gastado()))
    elif orden == "bajar":
        ruta, usd, det = bajar(c, dataset, schema, symbols, stype, start, end)
        print("BAJADO %s  USD %.4f  %s  (acumulado USD %.4f)" % (os.path.relpath(ruta, RAIZ), usd, det, gastado()))
    else:
        raise SystemExit("orden desconocida: " + orden)


if __name__ == "__main__":
    main()
