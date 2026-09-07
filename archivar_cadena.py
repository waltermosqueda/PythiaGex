# -*- coding: utf-8 -*-
"""ARCHIVAR LA CADENA FLACA, CORRIDA A CORRIDA.

Hasta el 2026-09-07 cada corrida de Actions pisaba <raiz>_radar.json y la
cadena de quince minutos antes se perdia. Para hacer backtest hace falta
justo eso: la cadena que el indicador TENIA en cada momento (con el retraso
real de CBOE adentro). Este script guarda, por dia UTC y por raiz, una linea
por corrida con lo minimo que Feed.Parsear necesita: la base medida y la
cadena comprimida (strike, vencimiento, OI, IV, volumen).

Se anota solo si el sello de CBOE cambio: de noche la cadena se congela y
guardar cien copias iguales seria mentir sobre cuanto dato hay.

Formato: datos/historico/cadena-<raiz>-<AAAA-MM-DD>.jsonl.gz, un miembro gzip
por linea (se puede seguir agregando sin descomprimir; .NET y Python leen
miembros concatenados). Cada linea trae "generado" = cuando la nube lo
publico, que es lo que el indicador hubiera tenido a esa hora.

Uso:  python archivar_cadena.py [ES NQ RTY]
"""
import datetime as dt
import gzip
import io
import json
import os
import sys

ENTRADA = os.path.join("panel", "datos", "atas")
SALIDA = os.path.join("datos", "historico")
PUBLICAR = os.path.join("panel", "datos", "atas", "cadenas")
CAMPOS_RAIZ = ("generado", "cadena_ts", "edad_min", "retraso_s", "spot", "base", "base_confiable",
               "base_cruda", "base_error_ticks", "base_ultima_buena", "base_ultima_buena_edad_min",
               "contrato")


def ruta_dia(raiz, dia):
    return os.path.join(SALIDA, "cadena-%s-%s.jsonl.gz" % (raiz, dia))


def ultimo_sello(raiz):
    p = os.path.join(SALIDA, "cadena-%s.ultimo" % raiz)
    try:
        with io.open(p, encoding="utf-8") as f:
            return f.read().strip()
    except Exception:
        return ""


def anotar_sello(raiz, sello):
    with io.open(os.path.join(SALIDA, "cadena-%s.ultimo" % raiz), "w", encoding="utf-8") as f:
        f.write(sello)


DIAS_MAX = 8.0   # Gamma Vivo corta a 7 dias y Gamma Hoy a hoy/semana: mas lejos no se backtestea


def linea_flaca(radar, dias_max=DIAS_MAX):
    out = {k: radar.get(k) for k in CAMPOS_RAIZ if k in radar}
    c = radar.get("cadena") or {}
    out["cadena"] = {k: c.get(k) for k in ("ts", "spot_idx", "ultimo_trade", "horizonte_dias",
                                             "vencimientos", "campos") if k in c}
    vencs = c.get("vencimientos") or []
    # las filas viajan con el indice del vencimiento en la posicion 1; se
    # filtran por dias sin tocar la lista de vencimientos, asi el indice sigue valiendo
    def cerca(f):
        try:
            return 0 <= vencs[int(f[1])]["dias"] <= dias_max
        except Exception:
            return False
    out["cadena"]["filas"] = [f for f in (c.get("filas") or []) if cerca(f)]
    out["cadena"]["dias_max_archivo"] = dias_max
    return out


def archivar(raiz, entrada=ENTRADA):
    p = os.path.join(entrada, "%s_radar.json" % raiz)
    if not os.path.exists(p):
        return "%s: no hay %s" % (raiz, p)
    with io.open(p, encoding="utf-8") as f:
        radar = json.load(f)
    c = radar.get("cadena") or {}
    if not c.get("filas"):
        return "%s: la cadena vino vacia, no se archiva" % raiz
    sello = "%s|%s" % (c.get("ts", ""), radar.get("base"))
    if sello == ultimo_sello(raiz):
        return "%s: mismo sello de CBOE (%s), no se repite" % (raiz, c.get("ts"))
    gen = radar.get("generado") or dt.datetime.now(dt.timezone.utc).isoformat(timespec="seconds")
    dia = gen[:10]
    os.makedirs(SALIDA, exist_ok=True)
    linea = json.dumps(linea_flaca(radar), ensure_ascii=False, separators=(",", ":")) + "\n"
    with open(ruta_dia(raiz, dia), "ab") as f:
        f.write(gzip.compress(linea.encode("utf-8")))
    anotar_sello(raiz, sello)
    return "%s: archivada la cadena de %s en %s (%d filas, %d bytes)" % (
        raiz, c.get("ts"), ruta_dia(raiz, dia), len(c["filas"]), len(linea))


def publicar():
    """Copia los archivos por dia al panel para que el indicador los baje por URL."""
    os.makedirs(PUBLICAR, exist_ok=True)
    n = 0
    for f in os.listdir(SALIDA):
        if f.startswith("cadena-") and f.endswith(".jsonl.gz"):
            src, dst = os.path.join(SALIDA, f), os.path.join(PUBLICAR, f)
            with open(src, "rb") as a, open(dst, "wb") as b:
                b.write(a.read())
            n += 1
    return "%d archivos publicados en %s" % (n, PUBLICAR)


def leer(ruta):
    """Devuelve las lineas (dict) de un archivo por dia. Sirve para el laboratorio."""
    out = []
    with gzip.open(ruta, "rt", encoding="utf-8") as f:
        for l in f:
            l = l.strip()
            if l:
                out.append(json.loads(l))
    return out


if __name__ == "__main__":
    raices = sys.argv[1:] or ["ES", "NQ", "RTY"]
    for r in raices:
        print(archivar(r))
    print(publicar())
