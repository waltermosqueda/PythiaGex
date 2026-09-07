# -*- coding: utf-8 -*-
"""DEL CACHE DE CADENAS CRUDAS AL ARCHIVO QUE REBOBINA GAMMA HOY.

datos/cache guarda las cadenas completas de CBOE que radar.py bajo en esta
maquina (2026-09-03: 706 fotos cada ~70 s). El archivo del rebobinado quiere
lineas flacas con la base medida y la cadena comprimida, igual que
archivar_cadena.py. Este script las fabrica pasando cada cadena cruda por el
mismo radar (una_corrida con guardar=False) y las deja en la carpeta que lee
el indicador: %APPDATA%\\ATAS\\PythiaGex\\cadenas\\local-<raiz>-<dia>.jsonl.

"generado" = el sello de descarga que lleva el nombre del archivo de cache:
es la hora a la que ESTA maquina tuvo la cadena, que es lo honesto.

Uso:  python herramientas/cache_a_cadenas.py [SPX] [--dia 2026-09-03] [--cada 60]
"""
import argparse
import datetime as dt
import io
import json
import os
import sys

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, RAIZ)
os.chdir(RAIZ)

import radar  # noqa: E402
import archivar_cadena as AC  # noqa: E402

FUTURO = {"_SPX": "ES", "_NDX": "NQ", "_RUT": "RTY"}


def carpeta_destino():
    return os.path.join(os.environ.get("APPDATA", ""), "ATAS", "PythiaGex", "cadenas")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("simbolo", nargs="?", default="SPX")
    ap.add_argument("--dia", default=None, help="AAAA-MM-DD (UTC del sello); vacio = todos")
    ap.add_argument("--cada", type=int, default=60, help="segundos minimos entre fotos (60 = como mucho una por minuto)")
    ap.add_argument("--destino", default=None)
    a = ap.parse_args()
    sim = radar.normalizar(a.simbolo)
    fut = FUTURO.get(sim, sim.lstrip("_"))
    dest = a.destino or carpeta_destino()
    os.makedirs(dest, exist_ok=True)

    corridas = radar.corridas(sim)
    if a.dia:
        corridas = [(s, r) for s, r in corridas if s.strftime("%Y-%m-%d") == a.dia]
    print("%d cadenas en cache de %s%s" % (len(corridas), sim, " del " + a.dia if a.dia else ""))
    ultimo_sello, ultima_hora = None, None
    hechas, saltadas, fallas = 0, 0, 0
    abiertos = {}
    for sello, ruta in corridas:
        if ultima_hora is not None and (sello - ultima_hora).total_seconds() < a.cada:
            saltadas += 1
            continue
        try:
            crudo = radar.leer_cache(ruta)
            s = radar.una_corrida(sim, crudo=crudo, guardar=False, verboso=False)
        except Exception as e:
            fallas += 1
            print("  fallo %s: %s" % (os.path.basename(ruta), e))
            continue
        # el radar arma la salida pensando en 'ahora': se le pone la hora de la descarga
        gen = sello.replace(tzinfo=dt.timezone.utc).isoformat(timespec="seconds")
        c = s.get("cadena") or {}
        marca = "%s|%s" % (c.get("ts"), s.get("base"))
        if marca == ultimo_sello or not c.get("filas"):
            saltadas += 1
            continue
        ultimo_sello, ultima_hora = marca, sello
        s = dict(s); s["generado"] = gen
        linea = json.dumps(AC.linea_flaca(s), ensure_ascii=False, separators=(",", ":")) + "\n"
        dia = gen[:10]
        f = abiertos.get(dia)
        if f is None:
            f = abiertos[dia] = io.open(os.path.join(dest, "local-%s-%s.jsonl" % (fut, dia)), "a", encoding="utf-8", newline="\n")
        f.write(linea)
        hechas += 1
        if hechas % 50 == 0:
            print("  %d cadenas escritas (ultima %s)" % (hechas, gen))
    for f in abiertos.values():
        f.close()
    print("listo: %d escritas, %d saltadas (mismo sello o muy seguidas), %d fallas, en %s" % (hechas, saltadas, fallas, dest))
    for dia in sorted(abiertos):
        p = os.path.join(dest, "local-%s-%s.jsonl" % (fut, dia))
        print("  %s  %.1f MB" % (p, os.path.getsize(p) / 1e6))


if __name__ == "__main__":
    main()
