# -*- coding: utf-8 -*-
"""
cboe_local.py — LA CADENA DE CBOE BAJADA DESDE ESTA PC, CADA MINUTO, SIN PASAR POR LA NUBE.

Por que: la nube (GitHub Actions, cadenas.yml) "corre cada minuto" pero en la practica corre cada 8 a 25 minutos
(medido 15-09: 13:13, 13:29, 13:42, 13:51, 13:59, 14:22 UTC...). CBOE actualiza su cadena retrasada cada 1 o 2
minutos en la rueda. Con la nube, las capas QQQ/SPX/SPY/NDX se movian a saltos y la estela parecia una raya; con
esto se mueven al minuto, como el libro vivo de Rithmic. Sin costo: es el mismo JSON publico de cdn.cboe.com.

Que hace: cada BUCLE segundos llama a archivar_cadena.bajar_y_archivar(raiz, destino) para ES (=SPX), NQ (=NDX),
QQQ, SPY y TQQQ, con destino %APPDATA%\\ATAS\\PythiaGex\\cboe-local\\. Ahi quedan ultima-<raiz>.json (la que lee
Gamma Hoy antes que la nube, si es mas fresca) y cadena-<raiz>-<dia>.jsonl.gz (que el rebobinado de la estela lee
como tercera fuente). Solo anota cuando el sello de CBOE cambio.

Cadencia: en la rueda de Nueva York (13:20-20:10 UTC, lunes a viernes) cada 75 s; fuera, cada 5 min.
Peso: SPX 13 MB + NDX 6 + QQQ 5 + SPY 6 + TQQQ 3 = ~33 MB por vuelta; en la rueda ~1,6 GB por hora.

Uso:  python herramientas/cboe_local.py --bucle        (o el .bat, que lo lanza sin ventana)
      python herramientas/cboe_local.py                (una sola vuelta)
"""
import datetime as dt
import io
import os
import sys
import time

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(AQUI)
sys.path.insert(0, RAIZ)
os.chdir(RAIZ)

DESTINO = os.path.join(os.environ.get("APPDATA", ""), "ATAS", "PythiaGex", "cboe-local")
LOG = os.path.join(os.environ.get("APPDATA", ""), "ATAS", "pythiagex-cboe-local.log")
RAICES = ["ES", "NQ", "QQQ", "SPY", "TQQQ"]


def log(m):
    linea = dt.datetime.now().strftime("%Y-%m-%dT%H:%M:%S") + "  " + m
    try:
        with io.open(LOG, "a", encoding="utf-8") as f:
            f.write(linea + "\n")
    except Exception:
        pass
    try:
        print(linea)
    except Exception:
        pass


def en_rueda(ahora_utc):
    if ahora_utc.weekday() >= 5:
        return False
    hm = ahora_utc.hour * 60 + ahora_utc.minute
    return 13 * 60 + 20 <= hm <= 20 * 60 + 10


def una_vuelta():
    import archivar_cadena as AC
    os.makedirs(DESTINO, exist_ok=True)
    for r in RAICES:
        try:
            log(AC.bajar_y_archivar(r, DESTINO))
        except Exception as e:
            log("%s: fallo (%s)" % (r, str(e)[:120]))


def main():
    if "--bucle" not in sys.argv:
        una_vuelta()
        return
    log("cboe_local arranca: destino " + DESTINO)
    while True:
        t0 = time.time()
        try:
            una_vuelta()
        except Exception as e:
            log("vuelta fallo: " + str(e)[:160])
        espera = 75 if en_rueda(dt.datetime.now(dt.timezone.utc)) else 300
        time.sleep(max(5, espera - (time.time() - t0)))


if __name__ == "__main__":
    main()
