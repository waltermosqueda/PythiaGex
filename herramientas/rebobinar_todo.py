# -*- coding: utf-8 -*-
"""CONVERTIR TODOS LOS DIAS BAJADOS Y REBOBINAR DE UNA.

1. Para cada dia con definition + statistics + ohlcv-1m de OPRA en
   datos/databento, arma la cadena por minuto (databento_a_cadenas.py) si
   todavia no esta, en paralelo (4 procesos: el convertidor es puro Python y
   tarda ~5 min por dia).
2. Corre Rebobina sobre todos los dias juntos -> un solo centinela
   "rebobinado-<instrumento>" con todas las ruedas.
3. Corre laboratorio/rebobinado.py.

Uso:  python herramientas/rebobinar_todo.py [--retraso 902] [--marco-min 1] [--instrumento MES] [--paralelo 4]
"""
import argparse
import glob
import os
import re
import subprocess
import sys
from concurrent.futures import ThreadPoolExecutor

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
os.chdir(RAIZ)
PY = sys.executable


def dias_bajados():
    dias = set()
    for p in glob.glob("datos/databento/OPRA_PILLAR/ohlcv-1m/SPX_OPT+SPXW_OPT-*.dbn.zst"):
        m = re.search(r"-(\d{4}-\d{2}-\d{2})-\d{4}-\d{2}-\d{2}\.dbn\.zst$", p)
        if not m:
            continue
        d = m.group(1)
        if (glob.glob("datos/databento/OPRA_PILLAR/definition/SPX_OPT+SPXW_OPT-%s-*.dbn.zst" % d)
                and glob.glob("datos/databento/OPRA_PILLAR/statistics/SPX_OPT+SPXW_OPT-%s-*.dbn.zst" % d)):
            dias.add(d)
    return sorted(dias)


def convertir(dia, retraso):
    salida = "datos/simulador/cadenas/sim-ES-%s-r%d.jsonl.gz" % (dia, retraso)
    if os.path.exists(salida) and os.path.getsize(salida) > 100000:
        return dia, "ya estaba"
    r = subprocess.run([PY, "herramientas/databento_a_cadenas.py", dia, "--retraso", str(retraso)],
                       capture_output=True, text=True)
    ult = (r.stdout.strip().splitlines() or ["?"])[-1]
    return dia, ("ok: " + ult) if r.returncode == 0 else ("FALLO: " + (r.stderr.strip().splitlines() or ["?"])[-1])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--retraso", type=int, default=902)
    ap.add_argument("--marco-min", type=int, default=1)
    ap.add_argument("--instrumento", default="MES")
    ap.add_argument("--paralelo", type=int, default=4)
    ap.add_argument("--nombre", default="rebobinado")
    ap.add_argument("--horizonte", default="Hoy")
    a = ap.parse_args()
    dias = dias_bajados()
    print("dias con OPRA completo:", len(dias), dias)
    with ThreadPoolExecutor(max_workers=a.paralelo) as ex:
        for dia, res in ex.map(lambda d: convertir(d, a.retraso), dias):
            print("  %s %s" % (dia, res))
    cadenas = [p for p in ("datos/simulador/cadenas/sim-ES-%s-r%d.jsonl.gz" % (d, a.retraso) for d in dias) if os.path.exists(p)]
    if not cadenas:
        print("sin cadenas"); return
    exe = os.path.join("atas", "Rebobina", "bin", "Release", "Rebobina.exe")
    cmd = [exe, "--cadenas", ",".join(cadenas), "--velas", "datos/simulador/velas/ESU6-1m.csv",
           "--instrumento", a.instrumento, "--marco-min", str(a.marco_min), "--nombre", a.nombre,
           "--horizonte", a.horizonte, "--audit", "0"]
    r = subprocess.run(cmd, capture_output=True, text=True)
    print(r.stdout.strip().splitlines()[-1] if r.stdout.strip() else r.stderr)
    subprocess.run([PY, "laboratorio/rebobinado.py", a.instrumento, "M%d" % a.marco_min, "--nombre", a.nombre])


if __name__ == "__main__":
    main()
