# -*- coding: utf-8 -*-
"""sembrar_4_0.py — 08-10-2026. SIEMBRA UNICA de la PythiaGex 4.1 al instalarla (no es una dependencia: se corre UNA vez, antes del primer
arranque, y despues la 4.0 vive de lo suyo).

Por que: la 4.1 calcula todo adentro, pero algunas cuentas necesitan dias previos que la 4.0 todavia no grabo porque recien nace:
  * la base de NDX de noche (C7) usa la ultima rueda con >= 20 muestras sincronizadas (cadenas de CBOE + precio del MNQ 15 min antes);
  * la razon de QQQ (C8) usa las ultimas 24 muestras;
  * TQQQ usa los cierres de la sesion anterior y su c sincronizado;
  * la estela de la sesion en curso (lo ya dibujado hoy) y el libro de NQ de las ultimas horas.
Copia (sin pisar nada que la 4.0 ya tenga) a %APPDATA%\\ATAS\\PythiaGex4:
  cboe\\cadena-{NQ,QQQ,TQQQ}-<dia>.jsonl.gz (ultimos 8 dias) y ultima-*.json   <- de %APPDATA%\\ATAS\\PythiaGex\\cboe-local (mismo formato byte a byte)
  viva\\viva3-NQ-<dia>.jsonl (ultimos 2 dias)                                <- de %APPDATA%\\ATAS\\PythiaGex3\\viva
  cinta\\cinta-NQ-<sesion>.csv y -relleno.csv (ultimas 3 sesiones)          <- de profundidad\\estado\\cinta (la 4.0 lo importa a su formato)
  estela\\estela-{NQ,NDX,QQQ}-<dia>.jsonl (ultimos 2 dias)                  <- de %APPDATA%\\ATAS\\PythiaGex3\\estela (TRES_* de hoy)
Uso: python -I herramientas/sembrar_4_0.py [--ver]   (--ver = solo listar, sin copiar). Solo copia; nunca borra ni modifica el origen."""
import os, sys, shutil, glob, datetime

APP = os.path.join(os.environ["APPDATA"], "ATAS")
RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEST = os.path.join(APP, "PythiaGex4")
HOY = datetime.datetime.utcnow().date()


def dias(n):
    return {(HOY - datetime.timedelta(days=k)).isoformat() for k in range(n + 1)}


def plan():
    p = []
    d8, d2, d3 = dias(8), dias(2), dias(3)
    src = os.path.join(APP, "PythiaGex", "cboe-local")
    for f in glob.glob(os.path.join(src, "cadena-*-*.jsonl.gz")):
        b = os.path.basename(f)
        partes = b[:-len(".jsonl.gz")].split("-", 2)          # cadena, TICKER, yyyy-mm-dd
        if len(partes) == 3 and partes[1] in ("NQ", "QQQ", "TQQQ") and partes[2] in d8:
            p.append((f, os.path.join(DEST, "cboe", b)))
    for t in ("NQ", "QQQ", "TQQQ"):
        f = os.path.join(src, "ultima-%s.json" % t)
        if os.path.exists(f):
            p.append((f, os.path.join(DEST, "cboe", os.path.basename(f))))
    for f in glob.glob(os.path.join(APP, "PythiaGex3", "viva", "viva3-NQ-*.jsonl")):
        if os.path.basename(f)[len("viva3-NQ-"):-len(".jsonl")] in d2:
            p.append((f, os.path.join(DEST, "viva", os.path.basename(f))))
    for f in glob.glob(os.path.join(RAIZ, "profundidad", "estado", "cinta", "cinta-NQ-*.csv")):
        b = os.path.basename(f)
        ses = b[len("cinta-NQ-"):len("cinta-NQ-") + 10]
        if ses in d3:
            p.append((f, os.path.join(DEST, "cinta", b)))
    for f in glob.glob(os.path.join(APP, "PythiaGex3", "estela", "estela-*-*.jsonl")):
        b = os.path.basename(f)
        partes = b[:-len(".jsonl")].split("-", 2)
        if len(partes) == 3 and partes[1] in ("NQ", "NDX", "QQQ") and partes[2] in d2:
            p.append((f, os.path.join(DEST, "estela", b)))
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
