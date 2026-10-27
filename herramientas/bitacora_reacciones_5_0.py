# -*- coding: utf-8 -*-
r"""bitacora_reacciones_5_0.py — 27-10-2026. Pedido del operador: "controla cada 15 minutos que este graficando bien ... que los niveles strikes se
respeten y reboten ... contrasta con los dias anteriores". Mide, SIN prometer nada, como reacciono el precio del MES en cada nivel que dibuja la 5.0.

Regla (fijada ANTES de mirar resultados):
  * nivel = el precio de cada serie dibujada en el minuto t (niv de la 5.0, %APPDATA%\ATAS\PythiaGex5\familia\niv-*.jsonl);
  * TOQUE = en el minuto t el rango [min, max] del MES (cinta de la 5.0, ticks por segundo) pasa a <= 0,5 pt (2 ticks) del nivel, viniendo de un lado
    (el cierre del minuto anterior a > 1 pt del nivel); un toque por nivel cada 10 min;
  * REBOTE = en los 10 min siguientes el precio se aleja >= 3 pts del nivel HACIA EL LADO DE DONDE VINO antes de cruzarlo por >= 2 pts;
    RUPTURA = cruza >= 2 pts antes; si no pasa ninguna de las dos: NADA;
  * PLACEBO = lo mismo con el nivel corrido +-2,5 y +-5 pts (cuatro copias): si el nivel real no rebota mas que sus copias corridas, no hay respeto
    medible (la gamma describe regimen; en 22 sesiones de NQ ninguna regla de dominantes le gano al azar).
Uso: python -I herramientas/bitacora_reacciones_5_0.py [--desde 2026-10-27T13:30Z] [--salida bitacora_5_0]
Escribe <salida>/reacciones-<dia>.csv (un renglon por toque) y <salida>/resumen-<dia>.txt; imprime el resumen. Solo LEE %APPDATA%."""
import os, sys, json, glob, datetime, collections, argparse
import numpy as np
AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import auditar_familia as AF   # el lector de la cinta del auditor (formato seg-MES-<dia>.bin)

APP = os.path.join(os.environ["APPDATA"], "ATAS", "PythiaGex5")
SERIES = ["MUROS_ES_oi", "MUROS_SPX_vol", "R20_SPX_vol", "R10_SPX_dom", "R10_SPX_zero", "GL_SPX_dom", "GL_SPY_dom", "GL_SPX_zero", "GL_ES_dom", "GL_ES_zero"]
TOQUE, LEJOS, REBOTE, RUPTURA, VENTANA, ESPERA = 0.5, 1.0, 3.0, 2.0, 10, 10
CORRIDOS = [-5.0, -2.5, 2.5, 5.0]


def minutos_precio(desde_utc):
    """min, max y cierre por minuto UTC (clave = minutos desde 1970) desde la cinta de la 5.0."""
    dias = sorted({os.path.basename(p)[len("seg-MES-"):-4] for p in glob.glob(os.path.join(APP, "cinta", "seg-MES-*.bin"))})
    c = AF.Cinta(APP, "MES", dias)
    pm = {}
    for num, T in c.seg_t.items():
        P = c.seg_p[num]
        for i in np.nonzero(T)[0]:
            t = int(T[i]) // 1000 if T[i] > 10 ** 11 else int(T[i])
            k = t // 60
            if k * 60 < desde_utc: continue
            p = float(P[i])
            a = pm.get(k)
            pm[k] = [p, p, p] if a is None else [min(a[0], p), max(a[1], p), p]
    return pm


def niveles(desde_k):
    out = {}
    for f in sorted(glob.glob(os.path.join(APP, "familia", "niv-*-MESZ6.jsonl"))):
        for ln in open(f, encoding="utf-8").read().strip().splitlines()[1:]:
            try: u = json.loads(ln)
            except Exception: continue
            if u["k"] < desde_k: continue
            lv = []
            for s in SERIES:
                for x in u["s"].get(s) or []:
                    if x and x[0] is not None: lv.append((s + ":" + str(x[1]), float(x[0])))
            out[u["k"]] = lv
    return out


def evaluar(pm, k, nivel, desde_arriba):
    for j in range(1, VENTANA + 1):
        m = pm.get(k + j)
        if not m: continue
        lo, hi = m[0], m[1]
        if desde_arriba:
            if lo <= nivel - RUPTURA: return "ruptura"
            if hi >= nivel + REBOTE: return "rebote"
        else:
            if hi >= nivel + RUPTURA: return "ruptura"
            if lo <= nivel - REBOTE: return "rebote"
    return "nada"


def main():
    ap = argparse.ArgumentParser(); ap.add_argument("--desde", default=""); ap.add_argument("--salida", default=os.path.join(os.path.dirname(AQUI), "bitacora_5_0"))
    a = ap.parse_args()
    ahora = datetime.datetime.now(datetime.timezone.utc)
    desde = datetime.datetime.fromisoformat(a.desde.replace("Z", "+00:00")) if a.desde else ahora - datetime.timedelta(hours=26)
    desde_s = int(desde.timestamp())
    pm = minutos_precio(desde_s); nv = niveles(desde_s // 60)
    ult = {}; filas = []
    cuenta = collections.defaultdict(lambda: collections.Counter())
    for k in sorted(nv):
        m, prev = pm.get(k), pm.get(k - 1)
        if not m or not prev: continue
        for nombre, lvl in nv[k]:
            for corr in [0.0] + CORRIDOS:
                L = lvl + corr; clave = (nombre, corr, round(L * 4))
                if k - ult.get(clave, -10 ** 9) < ESPERA: continue
                if not (m[0] - TOQUE <= L <= m[1] + TOQUE): continue
                if abs(prev[2] - L) <= LEJOS: continue
                ult[clave] = k
                if max(pm.keys()) < k + VENTANA: continue     # todavia no se puede juzgar
                res = evaluar(pm, k, L, prev[2] > L)
                serie = nombre.split(":")[0]
                cuenta[(serie, "real" if corr == 0 else "corrido")][res] += 1
                if corr == 0:
                    filas.append((datetime.datetime.utcfromtimestamp(k * 60).strftime("%Y-%m-%d %H:%M"), nombre, round(L, 2), "arriba" if prev[2] > L else "abajo", res))
    os.makedirs(a.salida, exist_ok=True)
    dia = ahora.strftime("%Y-%m-%d")
    with open(os.path.join(a.salida, "reacciones-%s.csv" % dia), "w", encoding="utf-8") as f:
        f.write("minuto_utc,serie,nivel,viene_de,resultado\n")
        for r in filas: f.write(",".join(map(str, r)) + "\n")
    lineas = ["bitacora de reacciones 5.0 — desde %s UTC, %d minutos con precio, %d con niveles (regla en el encabezado del script)" % (desde.strftime("%Y-%m-%d %H:%M"), len(pm), len(nv)),
              "serie            | real: toques reb rup nada  %reb | corrido (placebo): toques %reb | real - placebo"]
    tot = collections.Counter(); totc = collections.Counter()
    for s in SERIES:
        r, c = cuenta[(s, "real")], cuenta[(s, "corrido")]
        n, nc = sum(r.values()), sum(c.values())
        if n == 0 and nc == 0: continue
        pr = 100.0 * r["rebote"] / n if n else float("nan"); pc = 100.0 * c["rebote"] / nc if nc else float("nan")
        lineas.append("%-16s | %4d %4d %4d %4d %5.1f | %4d %5.1f | %+5.1f" % (s, n, r["rebote"], r["ruptura"], r["nada"], pr, nc, pc, pr - pc))
        tot.update(r); totc.update(c)
    n, nc = sum(tot.values()), sum(totc.values())
    if n:
        lineas.append("TODAS            | %4d %4d %4d %4d %5.1f | %4d %5.1f | %+5.1f" % (n, tot["rebote"], tot["ruptura"], tot["nada"], 100.0 * tot["rebote"] / n, nc,
                      100.0 * totc["rebote"] / nc if nc else float("nan"), 100.0 * tot["rebote"] / n - (100.0 * totc["rebote"] / nc if nc else 0)))
    lineas.append("Ojo: con pocas decenas de toques la diferencia contra el placebo es ruido; recien con cientos (varias sesiones) dice algo. Describe, no anticipa.")
    txt = "\n".join(lineas)
    open(os.path.join(a.salida, "resumen-%s.txt" % dia), "w", encoding="utf-8").write(txt + "\n")
    print(txt)


if __name__ == "__main__":
    main()
