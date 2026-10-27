# -*- coding: utf-8 -*-
r"""sembrar_5_0.py — 27-10-2026. SIEMBRA UNICA de la PythiaGex 5.0 (Gamma ES) al instalarla (no es una dependencia: se corre UNA vez, antes
del primer arranque, y despues la 5.0 vive de lo suyo). Copia de sembrar_4_0.py con los libros de ES.

Por que: la 5.0 calcula todo adentro, pero algunas cuentas necesitan dias previos que todavia no grabo porque recien nace:
  * la base de SPX de noche (C7) usa la ultima rueda con >= 20 muestras sincronizadas (cadenas de CBOE + precio del MES 15 min antes);
  * la razon de SPY (C8) usa las ultimas 24 muestras.
Copia (sin pisar nada que la 5.0 ya tenga) a %APPDATA%\ATAS\PythiaGex5:
  cboe\cadena-{ES,SPY}-<dia>.jsonl.gz (ultimos 8 dias) y ultima-{ES,SPY}.json  <- de %APPDATA%\ATAS\PythiaGex\cboe-local (mismo formato
                                                                                 byte a byte; el archivo "ES" es la cadena de _SPX, como en cboe_local)
No hay libro de ES (viva3-ES), cinta de MES ni estela previos que copiar: la 5.0 los empieza a grabar al arrancar (la base y la razon
sincronizadas necesitan el precio del MES: hasta juntar muestras propias usa los forwards de la cadena, como la 4.1).
Uso: python -I herramientas/sembrar_5_0.py [--ver]   (--ver = solo listar, sin copiar). Solo copia; nunca borra ni modifica el origen."""
import os, sys, shutil, glob, datetime

APP = os.path.join(os.environ["APPDATA"], "ATAS")
DEST = os.path.join(APP, "PythiaGex5")
HOY = datetime.datetime.now(datetime.timezone.utc).date()
TICKERS = ("ES", "SPY")


def dias(n):
    return {(HOY - datetime.timedelta(days=k)).isoformat() for k in range(n + 1)}


def plan():
    p = []
    d8 = dias(8)
    src = os.path.join(APP, "PythiaGex", "cboe-local")
    for f in glob.glob(os.path.join(src, "cadena-*-*.jsonl.gz")):
        b = os.path.basename(f)
        partes = b[:-len(".jsonl.gz")].split("-", 2)          # cadena, TICKER, yyyy-mm-dd
        if len(partes) == 3 and partes[1] in TICKERS and partes[2] in d8:
            p.append((f, os.path.join(DEST, "cboe", b)))
    for t in TICKERS:
        f = os.path.join(src, "ultima-%s.json" % t)
        if os.path.exists(f):
            p.append((f, os.path.join(DEST, "cboe", os.path.basename(f))))
    return sorted(p, key=lambda x: x[1])


def main():
    ver = "--ver" in sys.argv[1:]
    total = 0
    for a, b in plan():
        tam = os.path.getsize(a)
        if os.path.exists(b):
            print("ya existe, no lo piso:", b); continue
        print(("copiaria" if ver else "copio") + " %8.1f MB  %s -> %s" % (tam / 1e6, a, b))
        if not ver:
            os.makedirs(os.path.dirname(b), exist_ok=True)
            shutil.copy2(a, b)
        total += tam
    print("%s %.1f MB a %s" % ("se copiarian" if ver else "copiados", total / 1e6, DEST))


if __name__ == "__main__":
    main()
