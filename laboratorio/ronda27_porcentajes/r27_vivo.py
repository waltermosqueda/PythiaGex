# -*- coding: utf-8 -*-
"""RONDA 27 - JUEZ EN VIVO de los porcentajes de LARGO / CORTO (muestra virgen desde el lunes 30-11-2026). No toca ATAS ni la red: solo lee las
cintas que ya bajo la sonda (%APPDATA%\\ATAS\\PythiaGex\\flujo\\cinta-ES-<desde>-<hasta>.csv o cinta-ES<contrato>-..., con su .listo).
Por cada jornada nueva posterior al 27-11 (en orden de fecha):
  1. verifica las huellas SHA-256 (huellas_r27.json) del pipeline (r27_comun.py, r27_03_modelos.py), de este juez y del modelo congelado
     (modelo_congelado.json); si cambio algo, NO corre;
  2. exclusiones por regla: ruedas cortas (26-11 feriado, 27-11 y 24-12 cierre 13:15), cinta incompleta (ultima orden RTH antes de las 15:59 NY o
     hueco > 300 s en la rueda), roll de diciembre sin contrato confirmado (desde el 07-12, vivo_contratos.json, como la R21);
  3. arma la tabla del dia con el pipeline congelado y la historia hasta la vispera, aplica M0-M3 para Y = X/2, X, 2X con un evaluador PROPIO de
     los JSON (logistica y arboles, como lo haria el C#), y anota: cada minuto (bitacora_r27_minutos.csv), las operaciones de R-frec y R-60
     (bitacora_r27_operaciones.csv) y la jornada (bitacora_r27_jornadas.csv); despues suma la jornada a la historia.
Uso:
  python -I r27_vivo.py                       -> procesa todas las jornadas nuevas pendientes y muestra el resumen
  python -I r27_vivo.py --resumen             -> solo el resumen (calibracion acumulada, reglas y zonas)
  python -I r27_vivo.py --verificar 2026-11-25 -> recorre una jornada YA CONOCIDA con la historia hasta la vispera y la compara con la tabla de la
                                                 ronda y con el modelo aplicado por r27_03 (no escribe la bitacora)
  python -I r27_vivo.py --control <cinta.csv> <AAAA-MM-DD> -> corre una cinta suelta (p. ej. la cortada de hoy) sin escribir nada"""
import argparse, csv, glob, json, math, os, shutil, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r27_comun as K
import r27_03_modelos as MD
import numpy as np, pandas as pd

AQUI = K.AQUI
ESTADO = os.path.join(os.environ.get("LOCALAPPDATA", K.DATOS), "PythiaGex_r27_vivo")
MODELO = os.path.join(AQUI, "modelo_congelado.json")
HUELLAS = os.path.join(AQUI, "huellas_r27.json")
B_MIN = os.path.join(AQUI, "bitacora_r27_minutos.csv")
B_OPS = os.path.join(AQUI, "bitacora_r27_operaciones.csv")
B_JOR = os.path.join(AQUI, "bitacora_r27_jornadas.csv")
CONTRATOS = os.path.join(AQUI, "vivo_contratos.json")
DESDE = "2026-11-28"
DIAS_CORTOS = {"2026-11-26": "feriado de Accion de Gracias", "2026-11-27": "rueda corta (cierre 13:15 NY)", "2026-12-24": "rueda corta (cierre 13:15 NY)"}
ROLL_DESDE = "2026-12-07"
MODELOS = ("M0", "M1", "M2", "M3")


# ---------------------------------------------------------------------------------------------------------------- huellas y estado
def verificar_huellas():
    if not os.path.exists(HUELLAS) or not os.path.exists(MODELO):
        raise SystemExit("falta modelo_congelado.json o huellas_r27.json (correr r27_06_congelar.py)")
    h = json.load(open(HUELLAS))["sha256"]
    mal = [f for f, v in h.items() if K.sha256(K.CURVA if f == "curva_rango_por_minuto.json" else os.path.join(AQUI, f)) != v]
    if mal:
        raise SystemExit("HUELLAS DISTINTAS (%s): el pipeline, el juez o el modelo cambiaron. No se corre." % mal)
    return h["modelo_congelado.json"][:12]


def preparar_estado():
    os.makedirs(os.path.join(ESTADO, "hist_tam"), exist_ok=True)
    f = os.path.join(ESTADO, "historia.json")
    if not os.path.exists(f):
        shutil.copy2(os.path.join(K.DATOS, "historia.json"), f)
        for x in glob.glob(os.path.join(K.DATOS, "hist_tam", "ES-*.npy")):
            shutil.copy2(x, os.path.join(ESTADO, "hist_tam", os.path.basename(x)))
    return json.load(open(f))


def hist_tam_previos(dia, dir_=None):
    dir_ = dir_ or os.path.join(ESTADO, "hist_tam")
    fs = sorted(x for x in glob.glob(os.path.join(dir_, "ES-*.npy")) if os.path.basename(x)[3:13] < dia)
    return [np.load(x) for x in fs[-10:]]


def contratos():
    if os.path.exists(CONTRATOS):
        return json.load(open(CONTRATOS))
    base = {"nota": "Contrato de cada cinta de ES y spreads MEDIDOS (no se inventan). Hasta el %s se asume Z6; desde esa fecha cada jornada "
                    "necesita su contrato en 'confirmados' y, si cambia, el spread medido en 'cambios' ({'desde', 'contrato', 'spread_sobre_anterior'}); "
                    "sin eso la jornada queda afuera." % ROLL_DESDE, "inicial": "Z6", "confirmados": {}, "cambios": []}
    json.dump(base, open(CONTRATOS, "w"), indent=1)
    return base


# ---------------------------------------------------------------------------------------------------------------- cintas
def cintas_vivo():
    """{dia: ruta} de las cintas de ES con .listo (cinta-ES-... o cinta-ES<contrato>-...); si hay varias del mismo dia, la de 'hasta' mas tardio."""
    out = {}
    for f in sorted(glob.glob(os.path.join(K.FLUJO, "cinta-ES*-*.csv"))):
        p = os.path.basename(f)[:-4].split("-")
        if len(p) != 4 or not os.path.exists(f + ".listo"):
            continue
        ok_inst = p[1] == "ES" or (len(p[1]) == 4 and p[1][:2] == "ES" and p[1][2] in "HMUZ" and p[1][3].isdigit())
        if not ok_inst:
            continue
        fin = p[3]; dia = "%s-%s-%s" % (fin[:4], fin[4:6], fin[6:8])
        if dia not in out or os.path.basename(out[dia]).split("-")[3] < fin:
            out[dia] = f
    return out


# ---------------------------------------------------------------------------------------------------------------- modelos congelados
def _arbol(nodo, x):
    while "leaf" not in nodo:
        j = int(nodo["split"][1:]); v = x[j]
        if not np.isfinite(v):
            sig = nodo["missing"]
        else:
            sig = nodo["yes"] if np.float32(v) < np.float32(nodo["split_condition"]) else nodo["no"]
        nodo = next(c for c in nodo["children"] if c["nodeid"] == sig)
    return float(nodo["leaf"])


def aplicar(exp, g, modelo):
    X, _ = MD.matriz(g, modelo)
    if exp.get("nulo"):
        return np.full(len(g), 0.5)
    if exp["tipo"] == "logistica":
        z = (X / np.asarray(exp["rms"])) @ np.asarray(exp["coef"])
        return 1 / (1 + np.exp(-z))
    Xs = MD.espejo_M2(X).astype(np.float32); Xf = X.astype(np.float32)
    def p_de(M):
        return np.array([1 / (1 + math.exp(-sum(_arbol(t, row) for t in exp["arboles"]))) for row in M])
    return (p_de(Xf) + 1 - p_de(Xs)) / 2


def predecir_dia(tab, mod):
    g = MD.preparar(tab).sort_values("m").reset_index(drop=True)
    P = {}
    for modelo in MODELOS:
        for Yk in K.YS:
            P["p_%s_%s" % (modelo, Yk)] = aplicar(mod["modelos"]["%s_%s" % (modelo, Yk)], g, modelo)
    return g, P


# ---------------------------------------------------------------------------------------------------------------- una jornada
def ajustar_hist(hist, dia, cfg):
    """Roll Z6 -> H7 (o el que siga): lleva los NIVELES de ayer de las ruedas anteriores a un cambio de contrato al contrato del dia, sumando
    los spreads MEDIDOS de vivo_contratos.json (los rangos no cambian, asi que la X tampoco)."""
    if not cfg or not cfg.get("cambios"):
        return hist
    h = {d: dict(v) for d, v in hist.items()}
    for d, v in h.items():
        a = sum(float(ch["spread_sobre_anterior"]) for ch in cfg["cambios"] if d < ch["desde"] <= dia)
        if a:
            for kk in ("rth_h", "rth_l", "rth_c", "rth_o", "poc", "vah", "val"):
                if kk in v: v[kk] = v[kk] + a
    return h


def correr_dia(dia, ruta, hist, ht_prev, mod, cfg=None):
    tab, hoy, ht, info = K.procesar_jornada(dia, ruta, ajustar_hist(hist, dia, cfg), ht_prev)
    g, P = predecir_dia(tab, mod)
    ops = []
    for modelo in MODELOS:
        for Yk in K.YS:
            exp = mod["modelos"]["%s_%s" % (modelo, Yk)]; p = P["p_%s_%s" % (modelo, Yk)]
            for nom, tau in (("R-frec", exp["tau_R_frec"]), ("R-60", 0.60)):
                for (m, s, pr, bruto, sal) in MD.operar_dia(g, p, tau, Yk):
                    r = g[g.m == m].iloc[0]
                    ops.append({"fecha": dia, "hora": "%02d:%02d" % divmod(570 + m + 1, 60), "modelo": modelo, "Y": Yk, "regla": nom, "lado": s,
                                "p": round(pr, 4), "tau": tau, "entrada": r["c"], "Y_pts": r["Y_" + Yk], "bruto": bruto,
                                "neto_c050": bruto - 0.5, "neto_c100": bruto - 1.0, "clima": r["tipo"]})
    return tab, hoy, ht, info, g, P, ops


def completa(info):
    return info["ultima_rth_s"] >= 15 * 3600 + 59 * 60 and info["hueco_rth_max_s"] <= 300


def anotar(ruta, filas, campos):
    nuevo = not os.path.exists(ruta)
    with open(ruta, "a", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=campos, extrasaction="ignore")
        if nuevo:
            w.writeheader()
        for x in filas:
            w.writerow(x)


CAMPOS_J = ["fecha", "estado", "motivo", "X", "mediana_10", "ordenes", "ultima_orden_rth", "hueco_rth_max_s", "minutos", "operaciones", "version_modelo",
            "procesada_AR"]


def simular(desde, hasta="2026-11-25"):
    """Prueba de punta a punta del camino REAL del vivo en una carpeta aparte: estado sembrado solo con la historia anterior a 'desde', bitacoras
    de prueba, y las jornadas conocidas de 'desde' a 'hasta' como si fueran nuevas. Compara despues cada minuto con la tabla de la ronda."""
    global ESTADO, B_MIN, B_OPS, B_JOR, DESDE, CONTRATOS
    import tempfile
    base = os.path.join(tempfile.gettempdir(), "r27_vivo_simulacion")
    shutil.rmtree(base, ignore_errors=True); os.makedirs(os.path.join(base, "hist_tam"))
    h = json.load(open(os.path.join(K.DATOS, "historia.json")))
    json.dump({d: v for d, v in h.items() if d < desde}, open(os.path.join(base, "historia.json"), "w"))
    for x in glob.glob(os.path.join(K.DATOS, "hist_tam", "ES-*.npy")):
        if os.path.basename(x)[3:13] < desde:
            shutil.copy2(x, os.path.join(base, "hist_tam", os.path.basename(x)))
    ESTADO = base; DESDE = desde
    B_MIN, B_OPS, B_JOR = (os.path.join(base, n) for n in ("min.csv", "ops.csv", "jor.csv"))
    CONTRATOS = os.path.join(base, "contratos.json")
    procesar_pendientes(hasta=hasta)
    B = pd.read_csv(B_MIN)
    malos = 0; n = 0
    for dia, g in B.groupby("fecha"):
        ref = pd.read_parquet(os.path.join(K.DATOS, "minutos", "ES-%s.parquet" % dia)).set_index("m")
        for Yk in K.YS:
            a = g.set_index("m")["y_" + Yk]; b = ref["y_" + Yk].reindex(a.index)
            malos += int((~((a == b) | (a.isna() & b.isna()))).sum()); n += len(a)
        malos += int((~np.isclose(g.set_index("m")["c"], ref["c"].reindex(g.m))).sum())
    print("simulacion %s .. %s: %d jornadas en la bitacora de prueba; minutos x Y comparados con la tabla de la ronda: %d, distintos %d"
          % (desde, hasta, B.fecha.nunique(), n, malos))
    print(open(B_JOR, encoding="utf-8").read())
    resumen()


def procesar_pendientes(hasta=None):
    ver = verificar_huellas()
    mod = json.load(open(MODELO))
    hist = preparar_estado()
    cfg = contratos()
    hechas = set()
    if os.path.exists(B_JOR):
        hechas = set(pd.read_csv(B_JOR)["fecha"].astype(str))
    tope = hasta or time.strftime("%Y-%m-%d")
    for dia in sorted(DIAS_CORTOS):
        if dia >= max("2026-11-26", DESDE) and dia not in hechas and dia <= tope:
            anotar(B_JOR, [{"fecha": dia, "estado": "excluida", "motivo": DIAS_CORTOS[dia] + " (por regla)", "procesada_AR": time.strftime("%Y-%m-%d %H:%M")}], CAMPOS_J)
            hechas.add(dia)
    for dia, ruta in sorted(cintas_vivo().items()):
        if dia < DESDE or dia in hechas or dia > tope:
            continue
        K.esperar_ram(2.5)
        base = {"fecha": dia, "version_modelo": ver, "procesada_AR": time.strftime("%Y-%m-%d %H:%M")}
        if dia in DIAS_CORTOS:
            anotar(B_JOR, [dict(base, estado="excluida", motivo=DIAS_CORTOS[dia])], CAMPOS_J); continue
        if dia >= ROLL_DESDE and dia not in cfg.get("confirmados", {}):
            print("%s: falta confirmar el contrato en vivo_contratos.json (roll de diciembre); queda pendiente" % dia); continue
        if dia >= ROLL_DESDE and cfg["confirmados"][dia] != cfg["inicial"] and not cfg.get("cambios"):
            print("%s: contrato %s sin spread medido en 'cambios'; queda pendiente" % (dia, cfg["confirmados"][dia])); continue
        ht_prev = hist_tam_previos(dia)
        tab, hoy, ht, info, g, P, ops = correr_dia(dia, ruta, hist, ht_prev, mod, cfg)
        if not completa(info):
            anotar(B_JOR, [dict(base, estado="excluida", motivo="cinta incompleta (ultima orden RTH %.0f s, hueco %.0f s)" % (info["ultima_rth_s"], info["hueco_rth_max_s"]),
                                X=info["X"], mediana_10=info["med10"], ordenes=info["ordenes"])], CAMPOS_J)
            continue
        filas = []
        for i, r in g.iterrows():
            f = {"fecha": dia, "m": int(r["m"]), "hora": "%02d:%02d" % divmod(570 + int(r["m"]) + 1, 60), "clima": r["tipo"], "c": r["c"], "X": r["X"],
                 "v_T7": r["v_T7"], "v_V4": r["v_V4"], "v_A3": r["v_A3"], "o_dir": r["o_dir"], "m_dir": r["m_dir"], "lado_T": r["lado_T"], "vw_X": r["vw_X"]}
            for Yk in K.YS:
                f["y_" + Yk] = r["y_" + Yk]
            for k_, v in P.items():
                f[k_] = round(float(v[i]), 5)
            filas.append(f)
        anotar(B_MIN, filas, list(filas[0].keys()))
        if ops:
            anotar(B_OPS, ops, list(ops[0].keys()))
        anotar(B_JOR, [dict(base, estado="evaluada", motivo="", X=info["X"], mediana_10=info["med10"], ordenes=info["ordenes"],
                            ultima_orden_rth="%.0f" % info["ultima_rth_s"], hueco_rth_max_s="%.0f" % info["hueco_rth_max_s"], minutos=len(filas),
                            operaciones=len(ops))], CAMPOS_J)
        hist[dia] = hoy
        json.dump(hist, open(os.path.join(ESTADO, "historia.json"), "w"), indent=0)
        np.save(os.path.join(ESTADO, "hist_tam", "ES-%s.npy" % dia), ht)
        print("%s evaluada: X %.2f, %d minutos, %d operaciones (todas las reglas y modelos)" % (dia, info["X"], len(filas), len(ops)), flush=True)


# ---------------------------------------------------------------------------------------------------------------- resumen
def resumen():
    if not os.path.exists(B_MIN):
        print("bitacora vacia: todavia no hay jornadas evaluadas"); return
    B = pd.read_csv(B_MIN)
    nd = B.fecha.nunique()
    print("JUEZ R27 en vivo: %d jornadas evaluadas (%s .. %s). Lectura descriptiva a las 15; ninguna decision antes." % (nd, B.fecha.min(), B.fecha.max()))
    for Yk in K.YS:
        for modelo in MODELOS:
            c = "p_%s_%s" % (modelo, Yk); D = B[B["y_" + Yk].notna()]
            p = D[c].to_numpy(); y = D["y_" + Yk].to_numpy()
            if len(y) == 0: continue
            bs = 1 - np.mean((p - y) ** 2) / 0.25
            print("  %s %-2s: n %5d | arriba %.3f | p media %.3f [%.3f, %.3f] | Brier skill %+.5f" % (Yk, modelo, len(y), y.mean(), p.mean(), p.min(), p.max(), bs))
    if os.path.exists(B_OPS):
        O = pd.read_csv(B_OPS)
        for (mo, Yk, rg), g in O.groupby(["modelo", "Y", "regla"]):
            print("  ops %s %s %s: %d (%.2f por jornada) acierto %.1f %% neto c0,50 %+.2f por op" % (mo, Yk, rg, len(g), len(g) / nd, 100 * (g.bruto > 0).mean(), g.neto_c050.mean()))
    for nom, col in (("voto T7", "v_T7"), ("voto V4", "v_V4"), ("voto A3", "v_A3"), ("lado TENDENCIA", "lado_T"), ("marea", "m_dir"), ("ola", "o_dir")):
        for Yk in K.YS:
            D = B[(B[col] != 0) & B["y_" + Yk].notna()]
            if len(D) == 0: continue
            a = np.where(D[col] > 0, D["y_" + Yk], 1 - D["y_" + Yk])
            print("  zona %-15s Y %-2s: %5d minutos en %d jornadas, %.1f %%" % (nom, Yk, len(D), D.fecha.nunique(), 100 * a.mean()))


# ---------------------------------------------------------------------------------------------------------------- verificacion
def verificar(dia, ruta_modelo=MODELO):
    mod = json.load(open(ruta_modelo))
    hist = json.load(open(os.path.join(K.DATOS, "historia.json")))
    hist = {d: v for d, v in hist.items() if d < dia}
    ht_prev = hist_tam_previos(dia, os.path.join(K.DATOS, "hist_tam"))
    ruta = K.cintas("ES")[dia]
    tab, hoy, ht, info, g, P, ops = correr_dia(dia, ruta, hist, ht_prev, mod)
    ref = pd.read_parquet(os.path.join(K.DATOS, "minutos", "ES-%s.parquet" % dia))
    cols = [c for c in ref.columns if c in tab.columns and c not in ("tramo",)]
    dif = [c for c in cols if not ((tab[c] == ref[c]) | (tab[c].isna() & ref[c].isna())).all()]
    print("%s: tabla del juez contra la de la ronda: %d columnas, distintas %s" % (dia, len(cols), dif))
    # el evaluador propio de los JSON contra el ajuste de r27_03 (mismo modelo congelado)
    for modelo in MODELOS:
        for Yk in K.YS:
            exp = mod["modelos"]["%s_%s" % (modelo, Yk)]
            p = P["p_%s_%s" % (modelo, Yk)]
            print("  %s %-2s: %s, p [%.4f, %.4f], %d operaciones R-frec" % (modelo, Yk, "nulo" if exp.get("nulo") else exp["tipo"], p.min(), p.max(),
                                                                       sum(1 for o in ops if o["modelo"] == modelo and o["Y"] == Yk and o["regla"] == "R-frec")))
    return dif


def control(ruta, dia, ruta_modelo=MODELO):
    mod = json.load(open(ruta_modelo))
    hist = json.load(open(os.path.join(K.DATOS, "historia.json")))
    ht_prev = hist_tam_previos(dia, os.path.join(K.DATOS, "hist_tam"))
    tab, hoy, ht, info, g, P, ops = correr_dia(dia, ruta, hist, ht_prev, mod)
    print("control %s (%s): X %.2f, ordenes %d, ultima orden RTH %.0f s NY, hueco max %.0f s" % (dia, os.path.basename(ruta), info["X"], info["ordenes"],
                                                                                            info["ultima_rth_s"], info["hueco_rth_max_s"]))
    hasta = info["ultima_rth_s"] - 9.5 * 3600
    g2 = g[(g.m + 1) * 60 <= hasta]
    print("  minutos con datos: %d; clima %s" % (len(g2), g2.tipo.value_counts().to_dict()))
    for modelo in MODELOS:
        p = P["p_%s_X" % modelo][: len(g2)]
        print("  %s Y X: p [%.4f, %.4f]" % (modelo, p.min(), p.max()))
    return g2, P


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--resumen", action="store_true")
    ap.add_argument("--verificar")
    ap.add_argument("--control", nargs=2)
    ap.add_argument("--modelo", default=MODELO, help="solo para --verificar / --control (pruebas de plomeria)")
    ap.add_argument("--simular", help="AAAA-MM-DD: recorre las jornadas conocidas desde esa fecha por el camino del vivo, en una carpeta aparte")
    a = ap.parse_args()
    if a.simular:
        simular(a.simular); return
    if a.verificar:
        verificar(a.verificar, a.modelo); return
    if a.control:
        control(a.control[0], a.control[1], a.modelo); return
    if not a.resumen:
        procesar_pendientes()
    resumen()


if __name__ == "__main__":
    main()
