# -*- coding: utf-8 -*-
"""RONDA 19, paso 9: registro COMUN de variantes probadas (`registro_variantes.csv`), para todos los agentes de la ronda.
- `volcar_mis_variantes()`: pasa lo corrido por r19_03_correr.py (variantes_probadas.json) al registro comun.
- `anotar(...)`: lo que use cualquier explorador para agregar UNA fila por variante probada (regla x salida x muestra).
- `resumen_busqueda()`: correccion por busqueda multiple sobre el TOTAL de variantes de DISENO del registro.
Uso: python -I r19_09_registro.py volcar | resumen"""
import csv, json, math, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from scipy import stats

AQUI = os.path.dirname(os.path.abspath(__file__))
REGISTRO = os.path.join(AQUI, "registro_variantes.csv")
CAMPOS = ["fecha_hora_AR", "agente", "familia", "variante", "salida", "parametros", "muestra", "ruedas", "n", "ops_por_rueda", "acierto_pct",
          "gana_media", "pierde_media", "bruto_op", "neto_op_c050", "neto_op_c100", "t_rueda_c050", "ruedas_positivas_c050",
          "racha_perdedora", "caida_max_c050", "sharpe_diario_c050", "placeboA_p", "nota"]

FAMILIA_DE = {"E1": "a+c (retroceso a favor de la marea en un nivel, flujo, entrada anticipada)",
              "E1_sin_lugar": "a+c ablacion sin nivel", "E1_sin_momento": "a+c ablacion sin flujo",
              "E2": "c (retroceso a favor de la marea, regimen direccional, entrada confirmada)",
              "E3": "f (vuelta al valor: extension fuera del ruido, marea hacia el VWAP)",
              "E4": "f (vuelta al valor + absorcion o big trades)"}
PARAM_DE = {"E1": "C1 marea con agresores; C2 lado VWAP RTH; nivel en zona (L-X/4..L+T); puntaje F1-F6>=2; veto caudal; minimo fresco<=60 s; rebote 2 ticks..X/2; stop min(ext-2t, ent-X/2); 9:45-15:30; max 5",
            "E1_sin_lugar": "E1 sin exigir nivel", "E1_sin_momento": "E1 sin puntaje ni veto",
            "E2": "setup E1 arma; entra en la vuelta de la ola a favor de la marea si el extremo es el armado; C3 fuera del ruido a favor; stop ext-2t; 10:00-15:30",
            "E3": "pivote de la marea fuera del ruido del otro lado; marea hacia el valor con agresores; vuelta de la ola a favor; precio del lado extendido del VWAP y a >=1R; stop ext-2t; 10:00-15:30",
            "E4": "E3 + (F1 absorcion o F6 big trades netos a favor) en los 60 s previos a la vuelta"}


def anotar(fila):
    nuevo = not os.path.exists(REGISTRO)
    with open(REGISTRO, "a", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=CAMPOS, extrasaction="ignore")
        if nuevo: w.writeheader()
        w.writerow({k: fila.get(k, "") for k in CAMPOS})


def volcar_mis_variantes():
    reg = json.load(open(os.path.join(AQUI, "variantes_probadas.json")))
    ya = set()
    if os.path.exists(REGISTRO):
        with open(REGISTRO, encoding="utf-8") as f:
            for r in csv.DictReader(f): ya.add((r["variante"], r["salida"], r["muestra"]))
    for r in reg:
        if (r["etapa"], r["salida"], r["muestra"]) in ya: continue
        res = json.load(open(os.path.join(AQUI, "salidas", "res-%s-%s.json" % (r["etapa"], r["muestra"]))))["res"][r["salida"]]
        sd = np.std(res.get("_serie050", [0, 0]), ddof=1) if res.get("_serie050") else 0
        anotar(dict(fecha_hora_AR=r["hora"], agente="r19-principal", familia=FAMILIA_DE.get(r["etapa"], ""), variante=r["etapa"],
                    salida=r["salida"], parametros=PARAM_DE.get(r["etapa"], ""), muestra=r["muestra"], ruedas=r.get("ruedas"), n=r.get("n"),
                    ops_por_rueda=r.get("ops_por_rueda"), acierto_pct=r.get("acierto"), gana_media=r.get("gana_media"),
                    pierde_media=r.get("pierde_media"), bruto_op=r.get("bruto_op"), neto_op_c050=r.get("c050_neto_op"),
                    neto_op_c100=r.get("c100_neto_op"), t_rueda_c050=r.get("c050_t"), ruedas_positivas_c050=r.get("c050_ruedas_pos"),
                    racha_perdedora=r.get("c050_racha"), caida_max_c050=r.get("c050_caida"),
                    sharpe_diario_c050=round(float(np.mean(res["_serie050"]) / sd), 4) if sd > 0 else "",
                    placeboA_p=(r.get("placeboA") or {}).get("p", ""),
                    nota="subconjunto del diseno (no cuenta aparte)" if r["muestra"] in ("tanda0", "diseno_resto") else ""))
    print("registro:", REGISTRO)


def _num(r, claves, defecto=None):
    for k in claves:
        v = r.get(k)
        if v not in (None, "", "nan", "NaN"):
            try: return float(v)
            except ValueError: pass
    return defecto


def leer_todos_los_registros():
    """Junta TODOS los registro_variantes*.csv de la carpeta de la ronda (raiz y subcarpetas de exploradores)."""
    import glob
    filas = []
    for f in sorted(set(glob.glob(os.path.join(AQUI, "registro_variantes*.csv")) + glob.glob(os.path.join(AQUI, "**", "registro_variantes*.csv"), recursive=True))):
        with open(f, encoding="utf-8") as fh:
            for r in csv.DictReader(fh):
                r["_archivo"] = os.path.relpath(f, AQUI); filas.append(r)
    return filas


def resumen_busqueda():
    filas = []
    for r in leer_todos_los_registros():
        mu = (r.get("muestra") or "").strip().lower().replace("ñ", "n")
        if mu != "diseno": continue
        T = _num(r, ["ruedas", "n_ruedas"], 26.0)
        t = _num(r, ["t_rueda_c050", "t_rueda", "t_por_rueda", "t"])
        sr = _num(r, ["sharpe_diario_c050", "sharpe_diario", "sharpe"])
        if sr is None and t is not None and T: sr = t / math.sqrt(T)
        if t is None and sr is not None and T: t = sr * math.sqrt(T)
        if t is None: continue
        filas.append(dict(arch=r["_archivo"], variante=r.get("variante", ""), salida=r.get("salida", ""), familia=r.get("familia", ""),
                          n=_num(r, ["n", "ops", "n_ops"]), acierto=_num(r, ["acierto_pct", "acierto"]), neto=_num(r, ["neto_op_c050", "neto_c050", "neto_op"]),
                          opr=_num(r, ["ops_por_rueda"]), t=t, sr=sr, T=T))
    N = len(filas)
    if N == 0:
        print("sin variantes de diseno"); return
    T = int(np.median([f["T"] for f in filas]))
    sr = np.array([f["sr"] for f in filas])
    tcrit = stats.t.ppf(1 - 0.05 / N, T - 1)
    v = float(np.var(sr, ddof=1)) if N > 1 else 0.0
    e = 0.5772156649
    sr0 = math.sqrt(v) * ((1 - e) * stats.norm.ppf(1 - 1 / N) + e * stats.norm.ppf(1 - 1 / (N * math.e))) if N > 1 else 0
    porarch = {}
    for f in filas: porarch[f["arch"]] = porarch.get(f["arch"], 0) + 1
    print("REGISTROS: " + ", ".join("%s (%d)" % kv for kv in sorted(porarch.items())))
    print("variantes de DISENO en TOTAL: N = %d | ruedas %d | t de Bonferroni (una cola, 5 %%/N) = %.2f | Sharpe diario del mejor esperable por azar (DSR) = %.3f" % (N, T, tcrit, sr0))
    for f in sorted(filas, key=lambda z: -z["t"])[:20]:
        z = (f["sr"] - sr0) * math.sqrt(max(f["T"] - 1, 1))
        print("  %-28s %-18s %-5s n %5s acierto %6s neto %7s t %+5.2f Sharpe %+6.3f DSR~%.3f %s" % (
            f["arch"][:28], str(f["variante"])[:18], f["salida"], "%.0f" % f["n"] if f["n"] is not None else "-",
            "%.1f" % f["acierto"] if f["acierto"] is not None else "-", "%+.3f" % f["neto"] if f["neto"] is not None else "-",
            f["t"], f["sr"], stats.norm.cdf(z), "PASA BONFERRONI" if f["t"] >= tcrit else ""))


if __name__ == "__main__":
    {"volcar": volcar_mis_variantes, "resumen": resumen_busqueda}[sys.argv[1]]()
