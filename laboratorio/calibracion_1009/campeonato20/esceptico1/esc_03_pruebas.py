# -*- coding: utf-8 -*-
"""esc_03_pruebas.py — ESCEPTICO 1: intenta TUMBAR cada resultado que dijo 'le gana al placebo' (o lo mejor de PRUEBA). SOLO LECTURA.
Subcomandos (python -I esc_03_pruebas.py <cmd>):
  holm            Holm del CAMPEONATO ENTERO con las p por permutacion de las cuatro familias (MIRAR y PRUEBA por separado;
                  sin controles ni sensibilidades).
  desfase         la misma serie leida con desfase -1 (FUGA a proposito: control), 0 (lo juzgado), +1, +2, +5, +15 min (con juez20
                  oficial, sin placebo) y cruce con el juez propio en -1/0/+1/+2. Si el resultado vive de la informacion del minuto, se
                  cae con +1/+2; si -1 lo infla mucho, el juez detecta la fuga y 0 no la tiene.
  desfase_placebo juez20 con su placebo (500 juegos) a desfase +1 y +2 para las ganadoras de MIRAR: ¿el p sobrevive a 1-2 min de margen?
  corr            barrido de corrimiento -15..+15 pts (juez propio): ¿el % real es un pico aislado en 0 (forma de ruido) o una meseta?
  placebo_propio  placebo INDEPENDIENTE del oficial: cada noche la serie entera corrida un monto al azar +-U[8, 40] (500 juegos).
  r20doms         NDX: R20 y DOMS eligen casi los mismos strikes; difieren en la base (~5 pts). Se juzga R20 con la base C7 de la 4.1.
Escribe datos/<cmd>.json y datos/<cmd>.txt."""
import ctypes
try:
    ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)
except Exception:
    pass
import glob
import json
import math
import os
import sys
import time

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import numpy as np  # noqa: E402
import pandas as pd  # noqa: E402
import esc_comun as E  # noqa: E402

OUT = os.path.join(AQUI, "datos")
os.makedirs(OUT, exist_ok=True)
LOG = []


def out(s):
    print(s, flush=True); LOG.append(s)


# ------------------------------------------------------------------------------------------------ objetivos
OBJ = {
    "R20_NDX_vol|noche|mirar": ("NDX", "R20_NDX_vol", "noche", "mirar"),
    "DOMS_NDX_vol|noche|mirar": ("NDX", "DOMS_NDX_vol", "noche", "mirar"),
    "MUROS_NQ_vol|noche|mirar": ("NQ", "MUROS_NQ_vol", "noche", "mirar"),
    "MAJORS_NQ_vol|noche|mirar": ("NQ", "MAJORS_NQ_vol", "noche", "mirar"),
    "MUROS_NQ_vol.P|noche|mirar": ("NQ", "MUROS_NQ_vol.P", "noche", "mirar"),
    "CONF_vol|rueda|MIRAR": ("CONF", "CONF_vol", "rueda", "MIRAR"),
    "CONF_NDX_NQ_vol|rueda|MIRAR": ("CONF", "CONF_NDX_NQ_vol", "rueda", "MIRAR"),
    "ZTP_NQ_vol|noche|prueba": ("NQ", "ZTP_NQ_vol", "noche", "prueba"),
    "MUROS_NQ_vol.P|rueda|prueba": ("NQ", "MUROS_NQ_vol.P", "rueda", "prueba"),
    "ZEST_NQ_oi|noche|prueba": ("NQ", "ZEST_NQ_oi", "noche", "prueba"),
}
OBJ2 = {
    "R20_NDX_vol|noche|prueba": ("NDX", "R20_NDX_vol", "noche", "prueba"),
    "MUROS_NQ_vol|noche|prueba": ("NQ", "MUROS_NQ_vol", "noche", "prueba"),
    "MAJORS_NQ_vol|noche|prueba": ("NQ", "MAJORS_NQ_vol", "noche", "prueba"),
    "MUROS_NQ_vol.P|noche|prueba": ("NQ", "MUROS_NQ_vol.P", "noche", "prueba"),
    "CONF_vol|rueda|PRUEBA": ("CONF", "CONF_vol", "rueda", "PRUEBA"),
    "CONF_NDX_NQ_vol|rueda|PRUEBA": ("CONF", "CONF_NDX_NQ_vol", "rueda", "PRUEBA"),
    "MUROS_NQ_vol.P|rueda|mirar": ("NQ", "MUROS_NQ_vol.P", "rueda", "mirar"),
    "ZTP_NQ_vol|noche|mirar": ("NQ", "ZTP_NQ_vol", "noche", "mirar"),
    "ZEST_NQ_oi|noche|mirar": ("NQ", "ZEST_NQ_oi", "noche", "mirar"),
}
OBJ.update(OBJ2)
GANADORAS_MIRAR = ("R20_NDX_vol|noche|mirar", "MUROS_NQ_vol|noche|mirar", "MAJORS_NQ_vol|noche|mirar", "MUROS_NQ_vol.P|noche|mirar",
                   "CONF_vol|rueda|MIRAR", "CONF_NDX_NQ_vol|rueda|MIRAR")
_CACHE = {}


def objetivo(clave):
    """-> dict(Vraw, rayas, vent, claves)."""
    fam, serie, ven, parte = OBJ[clave]
    if fam == "NDX":
        if "ndx" not in _CACHE:
            _CACHE["ndx"] = E.ndx_cargar()
        V, S = _CACHE["ndx"]
        mirar, prueba = E.ndx_sesiones(ven)
        ses = mirar if parte == "mirar" else prueba
        vent = E.ventanas_noche(ses) if ven == "noche" else E.ventanas_rueda(ses)
        return dict(Vraw=V, rayas=S[serie], vent=vent, claves=("D1", "D2"), n_noches=len(ses))
    if fam == "NQ":
        if "nq" not in _CACHE:
            _CACHE["nq"] = E.nq_cargar()
        V, reg, part = _CACHE["nq"]
        dias = part[ven][parte]
        rayas, vent = E.nq_rayas_y_ventanas(reg, dias, serie, ven)
        return dict(Vraw=V, rayas=rayas, vent=vent, claves=None, n_noches=len(dias))
    V, R = E.conf_cargar(serie)
    Vs, Rs, vent, sel = E.conf_parte(V, R, ven, parte)
    return dict(Vraw=Vs, rayas=Rs, vent=vent, claves=None, n_noches=len(sel))


# ------------------------------------------------------------------------------------------------ holm del campeonato
def holm_lista(ps):
    items = sorted(ps.items(), key=lambda kv: kv[1])
    m = len(items); run = 0.0; out_ = {}
    for j, (k, p) in enumerate(items):
        run = max(run, min(1.0, (m - j) * p))
        out_[k] = run
    return out_


def cmd_holm():
    P = {"mirar": {}, "prueba": {}}
    # NDX
    for p in glob.glob(os.path.join(E.NDX, "datos", "juez", "*.json")):
        d = json.load(open(p, encoding="utf-8"))
        if d["particion"] not in ("mirar", "prueba"):
            continue
        pv = d["placebo"]["azar"]["pct_acierto20"]["p_valor"]
        if pv is not None:
            P[d["particion"]]["NDX|%s|%s" % (d["conjunto"], d["ventana"])] = (pv, d["n_azar"], d["placebo"]["azar"]["pct_acierto20"].get("p_normal"))
    # NQ (las 500 de todas las series; la confirmacion con 5000 se informa aparte)
    T = pd.read_csv(os.path.join(E.NQD, "datos", "juez_nq_tabla.csv"))
    for r in T[T["n_azar"] == 500].itertuples():
        if r.p_perm == r.p_perm:
            P[r.parte]["NQ|%s|%s" % (r.serie, r.ventana)] = (float(r.p_perm), 500, float(r.p_normal))
    conf5000 = {"%s|%s" % (r.serie, r.ventana): float(r.p_perm) for r in T[T["n_azar"] == 5000].itertuples()}
    # QQQ (sin el control GRILLA)
    Q = pd.read_csv(os.path.join(E.QQQ, "datos", "resumen_qqq20.csv"))
    for r in Q.itertuples():
        if "GRILLA" in str(r.serie) or r.p != r.p:
            continue
        P[r.particion]["QQQ|%s|%s" % (r.serie, r.ventana)] = (float(r.p), int(r.n_azar), float(r.p_normal))
    # CONF (sin el control UNION)
    C = json.load(open(os.path.join(E.CONF, "resultados", "tabla.json"), encoding="utf-8"))
    for r in C:
        if r.get("rol") == "control" or r.get("p_valor") is None:
            continue
        P["mirar" if r["parte"] == "MIRAR" else "prueba"]["CONF|%s|%s" % (r["serie"], r["ventana"])] = (float(r["p_valor"]), int(r["n_azar"]), r.get("p_normal"))
    res = {}
    for parte in ("mirar", "prueba"):
        ps = {k: v[0] for k, v in P[parte].items()}
        hp = holm_lista(ps)
        pn = {k: v[2] for k, v in P[parte].items() if v[2] is not None and v[2] == v[2]}
        hn = holm_lista(pn)
        orden = sorted(ps, key=lambda k: ps[k])
        res[parte] = dict(m=len(ps), mejores=[dict(prueba=k, p=ps[k], n_azar=P[parte][k][1], p_holm=hp[k], p_normal=P[parte][k][2],
                                                   p_holm_normal=hn.get(k)) for k in orden[:10]],
                          rechazan_05=[k for k in orden if hp[k] <= 0.05], rechazan_05_normal=[k for k in pn if hn[k] <= 0.05],
                          n_p_menor_05=sum(1 for k in ps if ps[k] <= 0.05), esperadas_por_azar_05=0.05 * len(ps))
        out("HOLM CAMPEONATO %s: m = %d pruebas | p <= 0,05 sin corregir: %d (por azar se esperan %.1f) | rechazan con Holm: %s (normal: %s)" % (
            parte.upper(), len(ps), res[parte]["n_p_menor_05"], 0.05 * len(ps), res[parte]["rechazan_05"] or "NINGUNA",
            res[parte]["rechazan_05_normal"] or "NINGUNA"))
        for x in res[parte]["mejores"][:8]:
            out("   %-40s p %.4f (n_azar %d) -> p_holm %.3f | p_normal %s -> holm %s" % (
                x["prueba"], x["p"], x["n_azar"], x["p_holm"], "%.4f" % x["p_normal"] if x["p_normal"] is not None else "-",
                "%.3f" % x["p_holm_normal"] if x["p_holm_normal"] is not None else "-"))
    res["nq_confirmacion_5000"] = conf5000
    out("NQ confirmacion (5000 juegos, PRUEBA, candidatas de MIRAR): %s" % conf5000)
    return res


# ------------------------------------------------------------------------------------------------ desfase
def cmd_desfase():
    J = E.j20()
    res = {}
    for clave in OBJ:
        o = objetivo(clave)
        fila = {}
        for d in (-1, 0, 1, 2, 5, 15):
            r = J.evaluar(o["Vraw"], o["rayas"], ventanas=o["vent"], opciones={"desfase_min": d, "claves": o["claves"]}, n_azar=0,
                          corrimientos=(), n_boot=0)
            m = r["metricas"]
            fila[d] = dict(llegadas=m["llegadas"], pct=m["pct_acierto20"], aciertos=m["aciertos"], falsas=m["falsas"])
        VV = E.preparar_velas(o["Vraw"])
        propio = {}
        for d in (-1, 0, 1, 2):
            rp, _ = E.juzgar(VV, o["rayas"], o["vent"], desfase=d, claves=o["claves"])
            propio[d] = dict(llegadas=rp["llegadas"], pct=rp["pct_acierto20"])
        res[clave] = dict(juez20=fila, propio=propio)
        out("DESFASE %-30s %s | propio %s" % (clave, "  ".join("%+d: %d %.1f%%" % (d, v["llegadas"], v["pct"]) for d, v in fila.items()),
                                              "  ".join("%+d: %.1f%%" % (d, v["pct"]) for d, v in propio.items())))
    return res


def cmd_desfase_placebo():
    J = E.j20()
    res = {}
    for clave in GANADORAS_MIRAR:
        o = objetivo(clave)
        res[clave] = {}
        for d in (1, 2):
            t0 = time.time()
            r = J.evaluar(o["Vraw"], o["rayas"], ventanas=o["vent"], opciones={"desfase_min": d, "claves": o["claves"]}, n_azar=500, n_boot=0)
            m = r["metricas"]; a = r["placebo"]["azar"]["pct_acierto20"]
            res[clave][d] = dict(llegadas=m["llegadas"], pct=m["pct_acierto20"], azar_p50=a["p50"], percentil=a["percentil"], p=a["p_valor"],
                                 p_normal=a["p_normal"], corridas=r["placebo"]["tasa_base_corridas"])
            out("DESFASE+PLACEBO %-30s d=%+d: %d llegadas %.1f %% | azar p50 %.1f | percentil %.1f | p %.3f | p_normal %.3f | corridas %.1f (%.0f s)" % (
                clave, d, m["llegadas"], m["pct_acierto20"], a["p50"], a["percentil"], a["p_valor"], a["p_normal"],
                r["placebo"]["tasa_base_corridas"], time.time() - t0))
    return res


# ------------------------------------------------------------------------------------------------ corrimiento fino
def cmd_corr():
    res = {}
    for clave in ("R20_NDX_vol|noche|mirar", "DOMS_NDX_vol|noche|mirar", "MUROS_NQ_vol|noche|mirar", "CONF_vol|rueda|MIRAR",
                  "CONF_NDX_NQ_vol|rueda|MIRAR", "ZTP_NQ_vol|noche|prueba"):
        o = objetivo(clave)
        VV = E.preparar_velas(o["Vraw"])
        P = E.pistas(VV, o["rayas"], 0, o["claves"])
        fila = {}
        for c in range(-15, 16):
            r, _ = E.juzgar(VV, None, o["vent"], P=P, corrimiento=float(c))
            fila[c] = dict(llegadas=r["llegadas"], pct=r["pct_acierto20"])
        pcts = np.array([fila[c]["pct"] for c in range(-15, 16)])
        vec = [fila[c]["pct"] for c in range(-15, 16) if c != 0]
        r0 = fila[0]["pct"]
        rango = sorted(vec)
        res[clave] = dict(barrido=fila, real=r0, puesto_de_0=1 + int(sum(v > r0 for v in vec)), de=len(vec) + 1,
                          media_sin_0=float(np.mean(vec)), sd_sin_0=float(np.std(vec, ddof=1)),
                          vecinos_pm3=float(np.mean([fila[c]["pct"] for c in (-3, -2, -1, 1, 2, 3)])))
        out("CORR %-30s 0: %.1f %% | media +-1..15 %.1f (sd %.1f) | vecinos +-1..3 %.1f | puesto de 0 (1 = el mejor) = %d de %d | %s" % (
            clave, r0, res[clave]["media_sin_0"], res[clave]["sd_sin_0"], res[clave]["vecinos_pm3"], res[clave]["puesto_de_0"],
            len(vec) + 1, " ".join("%+d:%.0f" % (c, fila[c]["pct"]) for c in range(-15, 16, 3))))
    return res


# ------------------------------------------------------------------------------------------------ placebo propio
def cmd_placebo_propio(n=500, semilla=909, claves=None):
    res = {}
    rng = np.random.default_rng(semilla)
    for clave in (claves or [k for k in OBJ if k not in OBJ2]):
        o = objetivo(clave)
        VV = E.preparar_velas(o["Vraw"])
        P = E.pistas(VV, o["rayas"], 0, o["claves"])
        r0, _ = E.juzgar(VV, None, o["vent"], P=P)
        noches = np.unique(VV["noche"]); pos = {x: i for i, x in enumerate(noches)}
        ni = np.array([pos[x] for x in VV["noche"]])
        acc = []
        t0 = time.time()
        for _ in range(n):
            off_n = rng.uniform(8, 40, len(noches)) * rng.choice([-1.0, 1.0], len(noches))
            r, _ = E.juzgar(VV, None, o["vent"], P=P, off_vela=off_n[ni])
            if r["pct_acierto20"] == r["pct_acierto20"]:
                acc.append(r["pct_acierto20"])
        a = np.array(acc)
        x = r0["pct_acierto20"]
        res[clave] = dict(real=x, llegadas=r0["llegadas"], azar_p50=float(np.median(a)), azar_p5=float(np.percentile(a, 5)),
                          azar_p95=float(np.percentile(a, 95)), percentil=float(100 * ((a < x).sum() + 0.5 * (a == x).sum()) / len(a)),
                          p=float((1 + (a >= x).sum()) / (len(a) + 1)), n=len(a))
        out("PLACEBO PROPIO %-30s real %.1f %% (%d llegadas) | azar p50 %.1f [p5 %.1f, p95 %.1f] | percentil %.1f | p %.3f (%d juegos, %.0f s)" % (
            clave, x, r0["llegadas"], res[clave]["azar_p50"], res[clave]["azar_p5"], res[clave]["azar_p95"], res[clave]["percentil"],
            res[clave]["p"], len(a), time.time() - t0))
    return res


# ------------------------------------------------------------------------------------------------ R20 contra DOMS (NDX)
def cmd_r20doms():
    V, S = E.ndx_cargar()
    metas = pd.concat([pd.read_pickle(p)["meta"] for p in sorted(glob.glob(os.path.join(E.NDX, "datos", "series", "ndx_*.pkl")))], ignore_index=True)
    metas = metas.set_index("t")
    res = {}
    VV = E.preparar_velas(V)
    for parte in ("mirar", "prueba"):
        mirar, prueba = E.ndx_sesiones("noche")
        ses = mirar if parte == "mirar" else prueba
        vent = E.ventanas_noche(ses)
        sset = set(ses)
        R20, DOM = S["R20_NDX_vol"], S["DOMS_NDX_vol"]
        n = mismo = 0; difs = []; difbase = []
        r20c7 = {}
        for t, v in R20.items():
            if (t + pd.Timedelta(hours=2)).strftime("%Y-%m-%d") not in sset:
                continue
            hm = t.strftime("%H:%M")
            if not (hm >= "22:00" or hm < "13:30"):
                continue
            m = metas.loc[t] if t in metas.index else None
            if m is None:
                continue
            if isinstance(m, pd.DataFrame):
                m = m.iloc[0]
            b7, b20 = m["base"], m["r20_base"]
            nuevo = {}
            for rol, (p, et) in (v or {}).items():
                K = float(et[3:])
                if b7 == b7:
                    nuevo[rol] = (K + b7, et)
            r20c7[t] = nuevo
            d = DOM.get(t) or {}
            if v and d:
                n += 1
                if {x[1] for x in v.values()} == {x[1] for x in d.values()}:
                    mismo += 1
                if b7 == b7 and b20 == b20:
                    difbase.append(b20 - b7)
        rr, _ = E.juzgar(VV, R20, vent, claves=("D1", "D2"))
        rd, _ = E.juzgar(VV, DOM, vent, claves=("D1", "D2"))
        rc, _ = E.juzgar(VV, r20c7, vent, claves=("D1", "D2"))
        db = np.array(difbase)
        res[parte] = dict(minutos_ambas=n, mismos_strikes=mismo, pct_mismos=100.0 * mismo / n if n else None,
                          base_r20_menos_c7_mediana=float(np.median(db)) if len(db) else None,
                          base_p10=float(np.percentile(db, 10)) if len(db) else None, base_p90=float(np.percentile(db, 90)) if len(db) else None,
                          R20=rr, DOMS=rd, R20_con_base_C7=rc)
        out("R20 vs DOMS NDX noche %s: minutos con las dos %d, MISMOS strikes %d (%.1f %%) | base R20 - C7 mediana %.2f (p10 %.2f, p90 %.2f) | "
            "R20 %d %.1f %% | DOMS %d %.1f %% | R20 con base C7 %d %.1f %%" % (
                parte, n, mismo, res[parte]["pct_mismos"], res[parte]["base_r20_menos_c7_mediana"], res[parte]["base_p10"], res[parte]["base_p90"],
                rr["llegadas"], rr["pct_acierto20"], rd["llegadas"], rd["pct_acierto20"], rc["llegadas"], rc["pct_acierto20"]))
    return res


def main():
    cmd = sys.argv[1]
    t0 = time.time()
    f = {"holm": cmd_holm, "desfase": cmd_desfase, "desfase_placebo": cmd_desfase_placebo, "corr": cmd_corr,
         "placebo_propio": cmd_placebo_propio, "r20doms": cmd_r20doms,
         "placebo_propio2": lambda: cmd_placebo_propio(claves=list(OBJ2))}[cmd]
    res = f()
    json.dump(res, open(os.path.join(OUT, "%s.json" % cmd), "w", encoding="utf-8"), ensure_ascii=False, indent=1, default=str)
    out("listo %.0f s" % (time.time() - t0))
    open(os.path.join(OUT, "%s.txt" % cmd), "w", encoding="utf-8").write("\n".join(LOG) + "\n")


if __name__ == "__main__":
    main()
