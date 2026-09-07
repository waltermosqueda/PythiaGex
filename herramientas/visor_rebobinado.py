# -*- coding: utf-8 -*-
"""EL VISOR DEL REBOBINADO: una pagina para recorrer cada rueda minuto a minuto.

Lee los centinelas que dejo Rebobina (rebobinado-MES M1 y, si esta, la
variante sin retraso rebobinado0) y el informe del laboratorio, y los mete
adentro de la plantilla HTML (herramientas/visor_rebobinado.html). El
resultado es un solo archivo sin dependencias: datos/simulador/visor.html.

Uso:  python herramientas/visor_rebobinado.py [--salida datos/simulador/visor.html]
"""
import argparse
import datetime as dt
import io
import json
import os
import subprocess
import sys

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ATAS = os.path.join(os.environ.get("APPDATA", ""), "ATAS")
NIVELES = ("zero_vol", "zero_oi", "mp_vol", "mn_vol", "mp_oi", "mn_oi", "dom0", "dom1", "pico", "mc30", "mc5")
NIVELES_VIVO = ("zero", "wall_pos", "wall_neg", "dom0", "dom1", "dom2", "dom3", "dom4", "dom5", "dom6", "dom7")


def leer(prefijo, inst="MES", marco="M1", niveles=NIVELES):
    p = os.path.join(ATAS, "pythiagex-centinela-%s-%s-TimeFrame-%s.jsonl" % (prefijo, inst, marco))
    dias = {}
    if not os.path.exists(p):
        return dias
    for l in io.open(p, encoding="utf-8", errors="replace"):
        try:
            d = json.loads(l)
        except Exception:
            continue
        dia = d["t"][:10]
        D = dias.setdefault(dia, {"t": [], "o": [], "h": [], "l": [], "c": [], "spot": [], "niv": {k: [] for k in niveles}, "q": []})
        D["t"].append(d["t"][11:16]); D["o"].append(d["o"]); D["h"].append(d["h"]); D["l"].append(d["l"]); D["c"].append(d["c"])
        D["spot"].append(d.get("spot"))
        n = d.get("niv") or {}
        for k in niveles:
            v = n.get(k)
            D["niv"][k].append(round(v, 2) if v else None)
        D["q"].append(int(n["q_cuadrante"]) if n.get("q_cuadrante") else 0)
    return dias


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--salida", default=os.path.join(RAIZ, "datos", "simulador", "visor.html"))
    ap.add_argument("--copia", default=None, help="segunda copia (p. ej. el scratchpad para publicar)")
    ap.add_argument("--instrumento", default="MES")
    ap.add_argument("--marco", default="M1")
    a = ap.parse_args()
    dias = leer("rebobinado", a.instrumento, a.marco)
    r0 = leer("rebobinado0", a.instrumento, a.marco)
    vivo = leer("rebobinado-vivo", a.instrumento, a.marco, NIVELES_VIVO)
    lab = subprocess.run([sys.executable, os.path.join(RAIZ, "laboratorio", "rebobinado.py"), a.instrumento, a.marco],
                         capture_output=True, text=True, cwd=RAIZ).stdout
    if vivo:
        lab += "\n" + subprocess.run([sys.executable, os.path.join(RAIZ, "laboratorio", "rebobinado.py"), a.instrumento, a.marco, "--nombre", "rebobinado-vivo"],
                                     capture_output=True, text=True, cwd=RAIZ).stdout
    gasto = ""
    try:
        gasto = subprocess.run([sys.executable, os.path.join(RAIZ, "herramientas", "databento_bajar.py"), "ledger"],
                               capture_output=True, text=True, cwd=RAIZ).stdout.strip().splitlines()[-1]
    except Exception:
        pass
    datos = {"dias": dias, "sinRetraso": r0, "vivo": vivo, "lab": lab, "gasto": gasto,
             "instrumento": a.instrumento, "marco": a.marco,
             "generado": dt.datetime.now(dt.timezone.utc).strftime("%Y-%m-%d %H:%M UTC")}
    tpl = io.open(os.path.join(RAIZ, "herramientas", "visor_rebobinado.html"), encoding="utf-8").read()
    html = tpl.replace("__DATOS__", json.dumps(datos, ensure_ascii=False, separators=(",", ":")))
    os.makedirs(os.path.dirname(a.salida), exist_ok=True)
    # la copia local se abre suelta en un navegador: necesita doctype y charset;
    # la copia para publicar (artifact) no, porque el envoltorio ya los trae
    io.open(a.salida, "w", encoding="utf-8", newline="\n").write("<!doctype html>\n<meta charset=\"utf-8\">\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n" + html)
    print("visor: %s (%.1f MB, %d dias, %s)" % (a.salida, os.path.getsize(a.salida) / 1e6, len(dias), ", ".join(sorted(dias))))
    if a.copia:
        io.open(a.copia, "w", encoding="utf-8", newline="\n").write(html)
        print("copia:", a.copia)


if __name__ == "__main__":
    main()
