# -*- coding: utf-8 -*-
"""
evaluar_familia.py — juzga con juez20 (sin cambios) las series de PREREGISTRO_familia.md. SOLO LECTURA de datos; escribe en esta carpeta.

  python -I evaluar_familia.py armar            -> datos/rayas/<SERIE>.pkl ({t: {clave: (precio, etiqueta)}}, grilla completa: clave vacia =
                                                   la serie no dibujaba nada ese minuto) y datos/velas_familia.pkl
  python -I evaluar_familia.py juzgar [SERIE..] -> resultados/<SERIE>__<ventana>__<parte>.json (+ eventos .csv); 3 procesos
  python -I evaluar_familia.py tabla            -> resultados/tabla.json y tabla.txt (todas las series, MIRAR y PRUEBA, Holm interno)

Ventanas: noche [d-2h, d+13,5h), rueda [d+13,5h, d+20h). Noches de una serie = sesiones con alguna clave con nivel en la ventana y
>= 60 velas en la ventana. Particion por serie y ventana: juez20.particion_noches. Para que el placebo de una parte no vea la otra,
a cada corrida se le pasan SOLO las rayas de las sesiones de esa parte. desfase_min 0. n_azar 500 (MIRAR) / 2000 (PRUEBA).
"""
import ctypes
import json
import math
import os
import sys
import time

os.environ.setdefault("OMP_NUM_THREADS", "1")


def prioridad_baja():
    try:
        ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)
    except Exception:
        pass


prioridad_baja()

import numpy as np  # noqa: E402
import pandas as pd  # noqa: E402

AQUI = os.path.dirname(os.path.abspath(__file__))
CAL = os.path.normpath(os.path.join(AQUI, "..", ".."))
J20DIR = os.path.join(CAL, "campeonato20", "juez20")
for _p in (os.path.join(CAL, "datos"), J20DIR):
    if _p not in sys.path:
        sys.path.insert(0, _p)
import cargar as D  # noqa: E402
import juez20 as J20  # noqa: E402

DREC = os.path.join(AQUI, "datos", "rec")
DNUE = os.path.join(AQUI, "datos", "nuevas")
DRAY = os.path.join(AQUI, "datos", "rayas")
DRES = os.path.join(AQUI, "resultados")
FAMILIA = ["FAM_MUROS_vol", "FAM_MUROS_oi", "CONF_vol"]
COMBOS = ["CONF_NDX_NQ_vol", "CONF_todo", "FAM_MUROS_vol_TOP", "FAM_MUROS_oi_TOP", "FAM_MUROS_oi_DOI", "MUROS_oi_DOI"]
CONTROL = ["MUROS_oi_UNION"]
NUEVAS = ["C04_DOS_QQQ_DERIVA", "C06_REGIMEN_POS_QQQ", "C13_IMAN_QQQ_vol", "C14_REPEL_QQQ_vol", "C15_FLUJO_NQ", "C16_CONFLUENCIA_QQQ_NDX",
          "C17_BANDA_EM", "C18_CW_PW_OI_QQQ"]
TODAS = FAMILIA + COMBOS + CONTROL + NUEVAS
ROL = {**{s: "familia" for s in FAMILIA}, **{s: "combinacion" for s in COMBOS}, **{s: "control" for s in CONTROL},
       **{s: "nueva" for s in NUEVAS}}
VENTANAS = {"noche": (-2.0, 13.5), "rueda": (13.5, 20.0)}
SOLO_RUEDA = ("C17_BANDA_EM",)
N_AZAR = {"MIRAR": 500, "PRUEBA": 2000}
MIN_VELAS = 60


# ================================================================================================ armar
def armar_velas():
    partes = []
    for d in D.dias():
        v = D.velas(d)
        if v is None or v.empty:
            continue
        ini, fin = D.ventana(d)
        v = v[(v["t"] >= ini) & (v["t"] < fin)][["t", "o", "h", "l", "c"]].copy()
        v["noche"] = d
        partes.append(v)
    V = pd.concat(partes, ignore_index=True).sort_values("t").reset_index(drop=True)
    info = {"dataset_hasta": str(V["t"].max()), "n_dataset": len(V)}
    # extension de la sesion 2026-10-09 (solo para seguir llegadas): velas_hasta_ahora de juez20 (MNQZ6)
    try:
        from velas_hasta_ahora import construir
        X, inf2 = construir()
        ult = V["t"].max()
        ini, fin = D.ventana("2026-10-09")
        sol = V.merge(X[["t", "o", "h", "l", "c"]], on="t", suffixes=("", "_x"))
        sol = sol[sol["t"] >= pd.Timestamp("2026-10-01")]
        info["solape_con_juez20"] = len(sol)
        info["solape_iguales"] = int(((sol["o"] - sol["o_x"]).abs().lt(0.01) & (sol["h"] - sol["h_x"]).abs().lt(0.01) &
                                      (sol["l"] - sol["l_x"]).abs().lt(0.01) & (sol["c"] - sol["c_x"]).abs().lt(0.01)).sum())
        nue = X[(X["t"] > ult) & (X["t"] < fin)][["t", "o", "h", "l", "c"]].copy()
        nue["noche"] = "2026-10-09"
        V = pd.concat([V, nue], ignore_index=True).sort_values("t").reset_index(drop=True)
        info["extension_10_09"] = {"n": len(nue), "desde": str(nue["t"].min()) if len(nue) else None,
                                   "hasta": str(nue["t"].max()) if len(nue) else None}
    except Exception as e:
        info["extension_10_09_error"] = repr(e)
    return V, info


def armar():
    os.makedirs(DRAY, exist_ok=True)
    t0 = time.time()
    V, info = armar_velas()
    pd.to_pickle(V, os.path.join(AQUI, "datos", "velas_familia.pkl"))
    print("velas", info, flush=True)
    # mis series
    acc = {s: {} for s in FAMILIA + COMBOS + CONTROL}
    for f in sorted(os.listdir(DREC)):
        r = pd.read_pickle(os.path.join(DREC, f))
        for s in acc:
            acc[s].update(r["series"][s])
    for s, R in acc.items():
        pd.to_pickle(R, os.path.join(DRAY, "%s.pkl" % s))
        print(s, len(R), "claves", sum(1 for v in R.values() if v), "con nivel", flush=True)
    # las nuevas
    grillas = pd.read_pickle(os.path.join(DNUE, "grillas.pkl"))
    dq = set(D.dias("QQQ")); dn = set(D.dias("NQ"))
    for s in NUEVAS:
        df = pd.read_parquet(os.path.join(DNUE, "%s.parquet" % s))
        ses = dn if s == "C15_FLUJO_NQ" else dq
        R = {}
        for d in sorted(ses):
            for t in grillas.get(d, []):
                R[t] = {}
        for t, nv, et, K, lb in zip(pd.to_datetime(df["t"]), df["nivel"].astype(float), df["etiqueta"].astype(str),
                                    df["K"].astype(float) if "K" in df else [np.nan] * len(df),
                                    df["libro"].astype(str) if "libro" in df else [""] * len(df)):
            if not (nv == nv) or nv <= 0:
                continue
            R.setdefault(t, {})[et] = (float(nv), ("%s%g" % (lb, K)) if K == K else None)
        pd.to_pickle(R, os.path.join(DRAY, "%s.pkl" % s))
        print(s, len(R), "claves", sum(1 for v in R.values() if v), "con nivel", flush=True)
    with open(os.path.join(AQUI, "datos", "armar_info.json"), "w", encoding="utf-8") as fh:
        json.dump(info, fh, ensure_ascii=False, indent=1, default=str)
    print("listo %.0f s" % (time.time() - t0))


# ================================================================================================ juzgar
_V = None


def velas():
    global _V
    if _V is None:
        _V = pd.read_pickle(os.path.join(AQUI, "datos", "velas_familia.pkl"))
    return _V


def sesion_de(t):
    return (pd.Timestamp(t) + pd.Timedelta(hours=2)).strftime("%Y-%m-%d")


def noches_de(R, ventana):
    a, b = VENTANAS[ventana]
    V = velas()
    con = set()
    for t, v in R.items():
        if not v:
            continue
        d = sesion_de(t)
        N = pd.Timestamp(d)
        if N + pd.Timedelta(hours=a) <= t < N + pd.Timedelta(hours=b):
            con.add(d)
    out = []
    tv = V["t"]
    for d in sorted(con):
        N = pd.Timestamp(d)
        n = int(((tv >= N + pd.Timedelta(hours=a)) & (tv < N + pd.Timedelta(hours=b))).sum())
        if n >= MIN_VELAS:
            out.append(d)
    return out


def _resumen_placebo(pl):
    az = pl.get("azar", {})
    out = {"tasa_base_azar": pl.get("tasa_base_azar"), "tasa_base_corridas": pl.get("tasa_base_corridas"), "n_azar": pl.get("n_azar"),
           "corridos": {str(k): {m: v.get(m) for m in ("llegadas", "pct_acierto20", "falsas_por_noche", "aciertos_por_noche",
                                                     "expectativa_pts")} for k, v in pl.get("corridos", {}).items()},
           "corridos_juntos": {m: pl.get("corridos_juntos", {}).get(m) for m in ("llegadas", "pct_acierto20", "pct_acierto20_decididas",
                                                                               "falsas_por_noche", "aciertos_por_noche", "expectativa_pts",
                                                                               "recorrido_mediano_acierto")},
           "azar": az}
    return out


def juzgar_uno(args):
    serie, ventana, parte = args
    prioridad_baja()
    t0 = time.time()
    R = pd.read_pickle(os.path.join(DRAY, "%s.pkl" % serie))
    noches = noches_de(R, ventana)
    if not noches:
        return serie, ventana, parte, {"error": "sin noches"}
    mirar, prueba = J20.particion_noches(noches)
    sel = mirar if parte == "MIRAR" else prueba
    if not sel:
        return serie, ventana, parte, {"error": "parte vacia", "noches": noches}
    ss = set(sel)
    Rs = {t: v for t, v in R.items() if sesion_de(t) in ss}
    V = velas()
    V = V[V["noche"].isin(ss)].reset_index(drop=True)
    a, b = VENTANAS[ventana]
    res = J20.evaluar(V, Rs, ventanas=J20.ventanas_de_noches(sel, a, b), opciones={"desfase_min": 0}, n_azar=N_AZAR[parte])
    ev = pd.DataFrame(res["eventos"])
    os.makedirs(DRES, exist_ok=True)
    base = os.path.join(DRES, "%s__%s__%s" % (serie, ventana, parte))
    if len(ev):
        cols = [c for c in ev.columns if not isinstance(ev[c].iloc[0], (list, dict))]
        ev[cols].to_csv(base + "_eventos.csv", index=False)
    out = {"serie": serie, "rol": ROL[serie], "ventana": ventana, "parte": parte, "noches": sel, "noches_todas": noches,
           "mirar": mirar, "prueba": prueba, "n_pistas": res["n_pistas"], "velas": res["velas"], "metricas": res["metricas"],
           "bootstrap_ic90": res["bootstrap_ic90"], "placebo": _resumen_placebo(res.get("placebo", {})),
           "segundos": round(time.time() - t0, 1), "juez20_sha": _sha(os.path.join(J20DIR, "juez20.py"))}
    with open(base + ".json", "w", encoding="utf-8") as fh:
        json.dump(out, fh, ensure_ascii=False, indent=1, default=lambda o: float(o) if isinstance(o, (np.floating,)) else str(o))
    return serie, ventana, parte, {"segundos": out["segundos"], "llegadas": out["metricas"]["llegadas"],
                                   "noches": len(sel)}


def _sha(p):
    import hashlib
    return hashlib.sha256(open(p, "rb").read()).hexdigest()[:16]


def juzgar(series):
    tareas = []
    for s in series:
        for w in VENTANAS:
            if s in SOLO_RUEDA and w == "noche":
                continue
            for parte in ("MIRAR", "PRUEBA"):
                if os.path.exists(os.path.join(DRES, "%s__%s__%s.json" % (s, w, parte))) and os.environ.get("REHACER") != "1":
                    continue
                tareas.append((s, w, parte))
    # las PRUEBA (2000 juegos) primero para repartir mejor
    tareas.sort(key=lambda x: (x[2] != "PRUEBA",))
    print("tareas", len(tareas), flush=True)
    t0 = time.time()
    from multiprocessing import Pool
    with Pool(int(os.environ.get("N_PROC", "3")), initializer=prioridad_baja) as pool:
        for s, w, p, r in pool.imap_unordered(juzgar_uno, tareas):
            print("%-26s %-5s %-6s %s  (%.0f s)" % (s, w, p, r, time.time() - t0), flush=True)


# ================================================================================================ tabla
def _f(x, d=1):
    return "-" if x is None or (isinstance(x, float) and not math.isfinite(x)) else ("%.*f" % (d, x))


def tabla():
    filas = []
    for s in TODAS:
        for w in VENTANAS:
            for p in ("MIRAR", "PRUEBA"):
                q = os.path.join(DRES, "%s__%s__%s.json" % (s, w, p))
                if not os.path.exists(q):
                    continue
                r = json.load(open(q, encoding="utf-8"))
                m = r["metricas"]; pl = r["placebo"]; az = pl["azar"]; ic = r["bootstrap_ic90"]
                a20 = az.get("pct_acierto20", {})
                filas.append({
                    "serie": s, "rol": r["rol"], "ventana": w, "parte": p, "noches": len(r["noches"]),
                    "desde": r["noches"][0], "hasta": r["noches"][-1], "llegadas": m["llegadas"], "base": m["base"],
                    "aciertos": m["aciertos"], "falsas": m["falsas"], "indefinidas": m["indefinidas"], "censuradas": m["censuradas"],
                    "pct_acierto20": m["pct_acierto20"], "ic90": ic.get("pct_acierto20"), "pct_decididas": m["pct_acierto20_decididas"],
                    "aciertos_noche": m["aciertos_por_noche"], "falsas_noche": m["falsas_por_noche"],
                    "rayas_hora": m["rayas_por_hora"], "rayas_hora_p90": m["rayas_por_hora_p90"],
                    "recorrido_med": m["recorrido_mediano_acierto"], "precision_abs": m["precision_mediana_abs"],
                    "precision": m["precision_mediana"], "dist_rotura": m["dist_rotura_mediana"],
                    "expectativa": m["expectativa_pts"], "expectativa_fija": m["expectativa_pts_fija"],
                    "tasa_azar": pl["tasa_base_azar"], "tasa_corridas": pl["tasa_base_corridas"],
                    "azar_p5": a20.get("p5"), "azar_p95": a20.get("p95"), "percentil": a20.get("percentil"),
                    "p_valor": a20.get("p_valor"), "p_normal": a20.get("p_normal"), "z": a20.get("z"), "n_azar": pl["n_azar"],
                    "pct_falsas_noche_percentil": az.get("falsas_por_noche", {}).get("percentil"),
                    "expectativa_percentil": az.get("expectativa_pts", {}).get("percentil"),
                    "falsos_rompimientos": m["falsos_rompimientos"], "n_pistas": r["n_pistas"]})
    # Holm interno sobre las PRUEBA (todas las series de este agente, noche y rueda)
    pr = {"%s|%s" % (f["serie"], f["ventana"]): f["p_valor"] for f in filas if f["parte"] == "PRUEBA" and f["p_valor"] is not None}
    h = J20.holm(pr)
    for f in filas:
        if f["parte"] == "PRUEBA":
            k = "%s|%s" % (f["serie"], f["ventana"])
            f["p_holm_interno"] = h.get(k, {}).get("p_holm")
            ic = f["ic90"] or (None, None)
            f["sobrevive_prereg"] = bool(f["p_valor"] is not None and f["p_valor"] < 0.05 and f["tasa_corridas"] is not None and
                                         f["pct_acierto20"] > f["tasa_corridas"] and ic[0] is not None and f["tasa_azar"] is not None and
                                         ic[0] > f["tasa_azar"] and (f["pct_falsas_noche_percentil"] or 100) <= 50)
    with open(os.path.join(DRES, "tabla.json"), "w", encoding="utf-8") as fh:
        json.dump(filas, fh, ensure_ascii=False, indent=1, default=str)
    lin = ["%-24s %-5s %-6s %3s %5s %6s %-13s %5s %5s %5s %5s %5s %5s %5s %5s %5s %6s %6s %5s %5s" % (
        "serie", "vent", "parte", "noc", "lleg", "%a20", "IC90", "azar", "corr", "pctl", "p", "pnorm", "ac/n", "fa/n", "ray/h",
        "recor", "prec", "expec", "pHolm", "sobr")]
    for f in filas:
        ic = f["ic90"] or (None, None)
        lin.append("%-24s %-5s %-6s %3d %5d %6s %-13s %5s %5s %5s %5s %5s %5s %5s %5s %5s %6s %6s %5s %5s" % (
            f["serie"][:24], f["ventana"], f["parte"], f["noches"], f["llegadas"], _f(f["pct_acierto20"]),
            "%s-%s" % (_f(ic[0]), _f(ic[1])), _f(f["tasa_azar"]), _f(f["tasa_corridas"]), _f(f["percentil"]), _f(f["p_valor"], 3),
            _f(f["p_normal"], 3), _f(f["aciertos_noche"], 2), _f(f["falsas_noche"], 2), _f(f["rayas_hora"], 1), _f(f["recorrido_med"]),
            _f(f["precision_abs"], 2), _f(f["expectativa"], 2), _f(f.get("p_holm_interno"), 2), "SI" if f.get("sobrevive_prereg") else ""))
    txt = "\n".join(lin)
    open(os.path.join(DRES, "tabla.txt"), "w", encoding="utf-8").write(txt + "\n")
    print(txt)


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else ""
    if cmd == "armar":
        armar()
    elif cmd == "juzgar":
        juzgar(sys.argv[2:] or TODAS)
    elif cmd == "tabla":
        tabla()
    else:
        print(__doc__)
