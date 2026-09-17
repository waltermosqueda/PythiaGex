# -*- coding: utf-8 -*-
"""
verdes_30_nucleo.py — EL BANCO CON EL MISMO CODIGO QUE EL VIVO. Corre atas/VerdesBanco (que compila VerdesNucleo.cs, el nucleo del indicador) sobre la cinta
orden por orden de cada dia con libro grabado, y resume: verdes contra rayas de control (corridas +-37,5 y +-62,5, fuera del tunel), rueda y noche por
separado, por dia, con intervalo por dias. Rayas = las reconstruidas desde la cadena viva (verdes_lib.fotos) y, donde existe, la estela REAL.
Uso: python verdes_30_nucleo.py [--estela-real] [--sin-delta] [--objetivo 20]
"""
import os, sys, json, glob, subprocess
import numpy as np, pandas as pd
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import base as B, verdes_lib as V

RAIZ = B.RAIZ; EXE = os.path.join(RAIZ, "atas", "VerdesBanco", "bin", "Release", "net8.0", "VerdesBanco.dll")
TMP = os.path.join(B.CACHE, "nucleo"); os.makedirs(TMP, exist_ok=True)
COSTO = B.COSTO_PTS
DIAS = ["2026-09-08", "2026-09-09", "2026-09-11", "2026-09-14", "2026-09-15", "2026-09-16", "2026-09-17"]


def cinta_de(dia):
    fin = dia.replace("-", "") + "2100"
    fs = [f for f in glob.glob(os.path.join(B.FLUJO, "cinta-MNQ*-*-%s.csv" % fin)) if os.path.exists(f + ".listo")]
    return fs[0] if fs else None


def rayas_de(dia, real):
    p = os.path.join(TMP, "rayas-%s-%s.jsonl" % ("real" if real else "rec", dia))
    if real:
        import verdes_01_hoy as H
        e = H.estela("NQ", dia); filas = ['{"t":"%s","d":[%.2f,%.2f]}' % ((t - pd.Timedelta(seconds=1)).strftime("%Y-%m-%dT%H:%M:%SZ"), r["d0"] if r["d0"] == r["d0"] else 0, r["d1"] if r["d1"] == r["d1"] else 0) for t, r in e.iterrows()]
    else:
        F, _ = V.fotos("NQ"); ini = pd.Timestamp(dia) - pd.Timedelta(hours=2); fin = pd.Timestamp(dia) + pd.Timedelta(hours=21)
        f = F[(F["t"] >= ini) & (F["t"] <= fin)]
        filas = ['{"t":"%s","d":[%.2f,%.2f],"g":[%.0f,%.0f],"n":%.0f,"b":"vol","f":%.2f}' % ((r["t"] + pd.Timedelta(seconds=1)).strftime("%Y-%m-%dT%H:%M:%SZ"), r["d0"], r["d1"] if r["d1"] == r["d1"] else 0,
                                                                                         r["f0"], r["f1"] if r["f1"] == r["f1"] else 0, r["neto"], r["fut"]) for _, r in f.iterrows()]
    open(p, "w", encoding="utf-8").write("\n".join(filas) + "\n"); return p, len(filas)


def correr(dia, real, extra):
    c = cinta_de(dia)
    if not c: return None
    r, n = rayas_de(dia, real)
    if n < 20: return None
    sal = os.path.join(TMP, "eventos-%s-%s.jsonl" % ("real" if real else "rec", dia))
    out = subprocess.run(["dotnet", EXE, "--cinta", c, "--rayas", r, "--salida", sal] + extra, capture_output=True, text=True)
    if out.returncode != 0: print(dia, "ERROR", out.stderr[:300]); return None
    ev = [json.loads(l) for l in open(sal, encoding="utf-8") if l.strip()]
    g = {(e["id"], e["corr"]): e for e in ev if e["ev"] == "gatillo"}; filas = []
    for e in ev:
        if e["ev"] != "resultado": continue
        k = (e["id"], e["corr"]); q = g.get(k)
        if q is None: continue
        filas.append(dict(dia=dia, id=e["id"], corr=e["corr"], rueda=q["rueda"], lado=q["lado"], puntos=e["puntos"], neto=e["puntos"] - COSTO, resultado=e["resultado"], traspaso=q["traspaso"], fuerza=q["fuerza"]))
    return pd.DataFrame(filas)


def linea(nombre, d):
    if d is None or not len(d): return "%-34s sin operaciones" % nombre
    por_dia = d.groupby("dia")["neto"].mean(); rng = np.random.default_rng(7); dias = list(d["dia"].unique()); bs = []
    for _ in range(4000):
        m = pd.concat([d[d["dia"] == x] for x in rng.choice(dias, len(dias))]); bs.append(m["neto"].mean())
    lo, hi = np.percentile(bs, [5, 95])
    return "%-34s n %4d | gana %4.1f %% | neto %+5.2f pts/op (total %+7.1f) | IC 90 %% por dias [%+.2f ; %+.2f] | dias positivos %d de %d" % (
        nombre, len(d), 100 * (d["puntos"] > 0).mean(), d["neto"].mean(), d["neto"].sum(), lo, hi, int((por_dia > 0).sum()), len(por_dia))


if __name__ == "__main__":
    real = "--estela-real" in sys.argv; extra = []
    if "--sin-delta" in sys.argv: extra.append("--sin-delta")
    if "--objetivo" in sys.argv: extra += ["--objetivo", sys.argv[sys.argv.index("--objetivo") + 1]]
    dias = ["2026-09-16", "2026-09-17"] if real else DIAS
    D = [x for x in (correr(d, real, extra) for d in dias) if x is not None and len(x)]
    if not D: sys.exit("sin resultados")
    D = pd.concat(D, ignore_index=True); D.to_parquet(os.path.join(TMP, "resumen-%s.parquet" % ("real" if real else "rec")))
    print("NUCLEO UNICO (el mismo codigo del indicador) sobre la cinta orden por orden. Rayas: %s. Costo %.2f. %s" % ("ESTELA REAL (lo dibujado)" if real else "reconstruidas desde la cadena viva", COSTO, " ".join(extra)))
    for nombre, m in (("RUEDA (13:30-20:00 UTC)", D["rueda"]), ("FUERA DE RUEDA", ~D["rueda"])):
        d = D[m]; print("\n== " + nombre)
        print("   " + linea("VERDES", d[d["corr"] == 0])); print("   " + linea("CONTROL (corridas +-37,5 / +-62,5)", d[d["corr"] != 0]))
        v = d[d["corr"] == 0]
        if len(v): print("   por dia (verdes): " + " | ".join("%s %+.1f (%d)" % (k[5:], g["neto"].sum(), len(g)) for k, g in v.groupby("dia")))
        c = d[d["corr"] != 0]
        if len(c): print("   por dia (control): " + " | ".join("%s %+.1f (%d)" % (k[5:], g["neto"].sum(), len(g)) for k, g in c.groupby("dia")))
