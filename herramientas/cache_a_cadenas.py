# -*- coding: utf-8 -*-
"""DEL CACHE DE CADENAS CRUDAS DE CBOE AL ARCHIVO QUE REBOBINA GAMMA HOY.

datos/cache guarda las cadenas completas de CBOE que radar.py bajo en esta
maquina (2026-09-03: 706 fotos cada ~70 s). Este script las pasa por las
MISMAS dos funciones que usa la nube para armar el feed del indicador
(pythiagex.cadena_atas.construir y pythiagex.base.medir) y deja lineas
flacas en el formato del archivo, para rebobinar con lo que el indicador
VIO ese dia y cruzarlo contra la cadena reconstruida desde Databento.

No pasa por radar.una_corrida: esa funcion ademas detecta BigTrades contra
la foto anterior y consulta CME, y sobre 450 fotos se queda horas.

"generado" = el sello de descarga que lleva el nombre del archivo de cache:
la hora a la que ESTA maquina tuvo la cadena, que es lo honesto.

Uso:  python herramientas/cache_a_cadenas.py [SPX] [--dia 2026-09-03] [--cada 60] [--salida ruta.jsonl.gz]
"""
import argparse
import datetime as dt
import gzip
import json
import os
import sys

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, RAIZ)
os.chdir(RAIZ)

from pythiagex.fuentes import normalizar, leer_cache  # noqa: E402
from pythiagex.base import medir as medir_base  # noqa: E402
from pythiagex import cadena_atas as CAD  # noqa: E402
import radar  # noqa: E402  (corridas y _sello)
import archivar_cadena as AC  # noqa: E402

FUTURO = {"_SPX": "ES", "_NDX": "NQ", "_RUT": "RTY"}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("simbolo", nargs="?", default="SPX")
    ap.add_argument("--dia", default=None, help="AAAA-MM-DD (UTC del sello); vacio = todos")
    ap.add_argument("--cada", type=int, default=60, help="segundos minimos entre fotos")
    ap.add_argument("--salida", default=None)
    a = ap.parse_args()
    sim = normalizar(a.simbolo)
    fut = FUTURO.get(sim, sim.lstrip("_"))
    corridas = radar.corridas(sim)
    if a.dia:
        corridas = [(s, r) for s, r in corridas if s.strftime("%Y-%m-%d") == a.dia]
    salida = a.salida or os.path.join("datos", "simulador", "cadenas", "cboe-%s-%s.jsonl.gz" % (fut, a.dia or "todo"))
    os.makedirs(os.path.dirname(salida), exist_ok=True)
    if os.path.exists(salida):
        os.remove(salida)
    print("%d fotos en cache de %s%s -> %s" % (len(corridas), sim, " del " + a.dia if a.dia else "", salida), flush=True)
    ultimo, ultima_hora, hechas, saltadas, fallas = None, None, 0, 0, 0
    ultima_base_buena, ultima_base_hora = None, None
    with open(salida, "ab") as fout:
        for sello, ruta in corridas:
            if ultima_hora is not None and (sello - ultima_hora).total_seconds() < a.cada:
                saltadas += 1
                continue
            try:
                crudo = leer_cache(ruta)
                ts = crudo.get("timestamp")
                if ts == ultimo:
                    saltadas += 1
                    continue
                ahora = sello.replace(tzinfo=dt.timezone.utc)
                cad = CAD.construir(crudo, ahora=ahora)
                try:
                    b = medir_base(crudo)
                except Exception as e:
                    b = {"base": None, "confiable": False, "aviso": str(e)}
                base = b.get("base") if b.get("confiable") else None
                if base is not None:
                    ultima_base_buena, ultima_base_hora = base, ahora
                edad_buena = (ahora - ultima_base_hora).total_seconds() / 60.0 if ultima_base_hora else None
                linea = {
                    "generado": ahora.isoformat(timespec="seconds"), "cadena_ts": ts,
                    "edad_min": round((ahora - dt.datetime.strptime(ts, "%Y-%m-%d %H:%M:%S").replace(tzinfo=dt.timezone.utc)).total_seconds() / 60.0, 1),
                    "spot": crudo["data"].get("current_price"),
                    "base": base, "base_confiable": base is not None, "base_cruda": b.get("base"),
                    "base_error_ticks": b.get("residuo_ticks"),
                    "base_ultima_buena": ultima_base_buena, "base_ultima_buena_edad_min": round(edad_buena, 1) if edad_buena is not None else None,
                    "fuente": "cboe cache %s" % os.path.basename(ruta),
                    "cadena": cad,
                }
                flaca = AC.linea_flaca(linea)
                fout.write(gzip.compress((json.dumps(flaca, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")))
                ultimo, ultima_hora = ts, sello
                hechas += 1
                if hechas % 25 == 0:
                    print("  %d fotos escritas (ultima %s, base %s)" % (hechas, ahora.strftime("%H:%M"), base), flush=True)
            except Exception as e:
                fallas += 1
                print("  fallo %s: %s" % (os.path.basename(ruta), e), flush=True)
    print("listo: %d escritas, %d saltadas, %d fallas -> %s (%.1f MB)" % (hechas, saltadas, fallas, salida, os.path.getsize(salida) / 1e6), flush=True)


if __name__ == "__main__":
    main()
