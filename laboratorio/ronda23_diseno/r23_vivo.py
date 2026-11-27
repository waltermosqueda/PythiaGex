# -*- coding: utf-8 -*-
"""RONDA 23 - JUEZ EN VIVO de las candidatas CONGELADAS (muestra virgen desde el lunes 30-11-2026). No toca ATAS ni la red: solo lee
las cintas que baja la sonda (%APPDATA%\\ATAS\\PythiaGex\\flujo\\cinta-ES-<desde>-<hasta>.csv, o cinta-ES<contrato>-..., p. ej. ESZ6/ESH7).
La vara esta pre-registrada en PREREGISTRO_VIVO.md (con hora); este script la aplica tal cual.

Por cada jornada nueva (en orden, desde el 30-11-2026; no repite las ya anotadas):
  1. verifica las huellas SHA-256 de cada candidata congelada y de la semilla de historia (huellas_r23.json): si algo cambio, NO corre;
  2. exclusiones por regla: rueda corta o feriado (lista DIAS_CORTOS), cinta incompleta (ultima orden de la rueda antes de las 15:59:00 NY
     o un hueco > 300 s entre 9:30 y 16:00), o faltan jornadas previas para la mediana de 10. Una jornada excluida no se juzga; si es una rueda
     completa (cinta completa), su rango SI entra en la historia;
  3. corre cada candidata congelada sobre la cinta (el script de la regla, sin tocarlo) y anota cada operacion en bitacora_r23_vivo.csv con la
     referencia al azar con la misma ubicacion (200 caminos reflejados con velas de 5 min de la misma cinta) y la formula geometrica;
  4. anota la jornada en bitacora_r23_jornadas.csv y suma su rango a la historia (estado en %LOCALAPPDATA%\\PythiaGex_r23_vivo).
Al final imprime el estado del juez de cada candidata contra la vara pre-registrada.
El roll de diciembre (Z6 -> H7) NO afecta a estas reglas: solo usan precios del dia y RANGOS de jornadas previas (no cambian con el contrato).
El horario de verano se resuelve con la zona America/New_York (pandas), sin tablas a mano.
Uso:
  python -I r23_vivo.py                         -> procesa las jornadas nuevas pendientes y muestra el juez
  python -I r23_vivo.py --resumen               -> solo el estado del juez
  python -I r23_vivo.py --verificar 2026-11-25  -> recorre una jornada YA CONOCIDA (de la cinta de diseno) con la historia hasta la vispera y la
                                                   compara con salidas/ops-cinta-<ID>.csv (no escribe nada)
  python -I r23_vivo.py --carpeta D --estado E --desde AAAA-MM-DD --sembrar-desde-cintas
                                                -> el MISMO juez sobre otra carpeta de cintas (p. ej. ES de 2023 convertido al formato de la sonda);
                                                   la historia se arma con las propias cintas (las 10 primeras jornadas completas son calentamiento)"""
import argparse, csv, glob, hashlib, importlib.util, json, math, os, sys, time
import numpy as np, pandas as pd

try:   # prioridad baja
    import ctypes, ctypes.wintypes as _W
    _k = ctypes.windll.kernel32; _k.GetCurrentProcess.restype = _W.HANDLE; _k.SetPriorityClass.argtypes = [_W.HANDLE, _W.DWORD]
    _k.SetPriorityClass(_k.GetCurrentProcess(), 0x00004000)
except Exception:
    pass

AQUI = os.path.dirname(os.path.abspath(__file__))
HUELLAS = os.path.join(AQUI, "huellas_r23.json")
FLUJO = os.path.join(os.environ.get("APPDATA", ""), "ATAS", "PythiaGex", "flujo")
DESDE = "2026-11-30"
DIAS_CORTOS = {"2026-11-26": "feriado de Accion de Gracias", "2026-11-27": "rueda corta (cierre 13:15 NY)",
               "2026-12-24": "rueda corta: Nochebuena (cierre 13:15 NY)", "2026-12-25": "feriado de Navidad",
               "2027-01-01": "feriado de Anio Nuevo", "2027-01-18": "feriado (Martin Luther King): rueda corta",
               "2027-02-15": "feriado (Presidents Day): rueda corta", "2027-03-26": "Viernes Santo"}
COSTO = 1.00
TK = 0.25
CANDIDATAS = {"A3": "cand_A3_tarde_giro_amplia.py"}          # las candidatas CONGELADAS (ver huellas_r23.json y PREREGISTRO_VIVO.md)
SEMILLA = "historia_rangos_semilla.json"                     # rangos de la rueda de ES 31-07..25-11-2026 (historia de la R19)
VARA = dict(acierto_min=60.0,            # % de operaciones con resultado bruto > 0, sobre todas las de las jornadas de la lectura
            ops_min=40,                  # operaciones minimas en una lectura decisiva
            alfa_una_cola=0.05,          # se reparte (Bonferroni) entre candidatas x lecturas decisivas
            lecturas_decisivas=[60, 120],
            lecturas_futilidad=[20, 40], futilidad_min_ops=15, futilidad_acierto_menor_a=55.0,
            lectura_descriptiva=15)      # la bitacora de 15 sesiones del proyecto: se informa, no decide
CAMPOS_B = ["candidata", "version", "fecha", "hora_entrada", "lado", "entrada", "stop", "objetivo_inicial", "vwap", "X", "mediana_10",
            "dist_vwap_X", "dist_extremo_X", "ext_IB", "P_geometrica", "motivo", "hora_salida", "salida", "pts", "neto_c100", "mfe", "mae",
            "azar_acierto", "azar_pts", "contrato", "archivo", "procesada_AR"]
CAMPOS_J = ["fecha", "estado", "motivo", "contrato", "archivo", "ultima_orden_rth", "hueco_rth_max_s", "rango_rueda", "X", "mediana_10",
            "ops_A3", "procesada_AR"]


def sha256(ruta):
    h = hashlib.sha256()
    with open(ruta, "rb") as f:
        for b in iter(lambda: f.read(1 << 20), b""): h.update(b)
    return h.hexdigest()


def ram_libre_gb():
    try:
        class Mem(ctypes.Structure):
            _fields_ = [("l", ctypes.c_ulong), ("p", ctypes.c_ulong), ("tot", ctypes.c_ulonglong), ("av", ctypes.c_ulonglong),
                        ("a", ctypes.c_ulonglong), ("b", ctypes.c_ulonglong), ("c", ctypes.c_ulonglong), ("d", ctypes.c_ulonglong),
                        ("e", ctypes.c_ulonglong)]
        m = Mem(); m.l = ctypes.sizeof(Mem); ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(m))
        return m.av / 2 ** 30
    except Exception:
        return 99.0


def candidatas_congeladas():
    """Verifica el SHA-256 de cada archivo de huellas_r23.json (candidatas, semilla de historia y este juez) y carga las candidatas."""
    H = json.load(open(HUELLAS, encoding="utf-8"))
    faltan = [v for v in list(CANDIDATAS.values()) + [SEMILLA, os.path.basename(__file__)] if v not in H["sha256"]]
    if faltan:
        raise SystemExit("huellas_r23.json no tiene la huella de %s: no se corre." % faltan)
    mal = [r for r, v in H["sha256"].items() if sha256(os.path.join(AQUI, r)) != v]
    if mal:
        raise SystemExit("HUELLAS DISTINTAS (%s): una candidata, la semilla o el juez cambiaron. No se corre." % mal)
    H = dict(H, candidatas=dict(CANDIDATAS), vara=dict(VARA), semilla_historia=SEMILLA)
    mods = {}
    for cid, ruta_rel in CANDIDATAS.items():
        sp = importlib.util.spec_from_file_location("cand_" + cid, os.path.join(AQUI, ruta_rel))
        m = importlib.util.module_from_spec(sp); sp.loader.exec_module(m); mods[cid] = m
    return mods, H


# ------------------------------------------------------------------------------------------------ cintas
def cintas(carpeta):
    """{dia de la rueda: (archivo, contrato, listo)}. El dia sale del 'hasta' del nombre (UTC) menos 6 horas (asi una descarga que termina
    despues de la medianoche UTC sigue cayendo en su rueda). Si hay varias para el mismo dia, se prefiere la que tiene .listo y, entre
    ellas, la de 'hasta' mas tardio. El contenido se vuelve a chequear al procesar (la rueda de la cinta tiene que ser ese dia)."""
    from datetime import datetime, timedelta
    cand = {}
    for f in sorted(glob.glob(os.path.join(carpeta, "cinta-ES*.csv"))):
        p = os.path.basename(f)[:-4].split("-")
        if len(p) != 4 or not p[1].startswith("ES") or os.path.getsize(f) < 100000: continue
        try:
            hasta = datetime.strptime(p[3], "%Y%m%d%H%M")
        except ValueError:
            continue
        contrato = p[1][2:] or ""
        dia = (hasta - timedelta(hours=6)).strftime("%Y-%m-%d")
        listo = os.path.exists(f + ".listo")
        clave = (listo, p[3])
        if dia not in cand or clave > cand[dia][2]:
            cand[dia] = (f, contrato, clave)
    return {d: (v[0], v[1], v[2][0]) for d, v in cand.items()}


def chequeo_cinta(c_):
    s = c_["seg"]
    ult = float(s[-1]); hueco = float(np.diff(s).max()) if len(s) > 1 else math.inf
    if ult < 15 * 3600 + 59 * 60: return False, "cinta incompleta: la ultima orden de la rueda es a las %02d:%02d:%02d NY" % (ult // 3600, ult % 3600 // 60, ult % 60), ult, hueco
    if s[0] > 9 * 3600 + 31 * 60: return False, "cinta incompleta: la primera orden de la rueda es despues de las 9:31 NY", ult, hueco
    if hueco > 300: return False, "cinta incompleta: hueco de %.0f s en la rueda" % hueco, ult, hueco
    return True, "", ult, hueco


# ------------------------------------------------------------------------------------------------ referencia al azar (informativa)
def velas5(c_):
    b = np.floor((c_["seg"] - 34200) / 300.0).astype(int)
    o = np.full(78, np.nan); h = o.copy(); l = o.copy(); c = o.copy(); v = np.zeros(78)
    for j in range(78):
        m = b == j
        if not m.any(): continue
        o[j] = c_["pri"][m][0]; h[j] = c_["hi"][m].max(); l[j] = c_["lo"][m].min(); c[j] = c_["ult"][m][-1]; v[j] = c_["vol"][m].sum()
    return o, h, l, c, v


def azar(c_, op, M=200, semilla=23):
    """Misma entrada, mismo stop y objetivo en el VWAP (que se corre en cada vela), pero cada vela posterior reflejada al azar alrededor del
    cierre previo (regla conservadora de 5 min: si en la misma vela caen stop y objetivo, cuenta stop). Devuelve (fraccion ganadora, pts medio)."""
    o, h, l, c, v = velas5(c_)
    if np.isnan(c).any(): return np.nan, np.nan
    k = (int(op["hora_entrada"][:2]) * 60 + int(op["hora_entrada"][3:5]) - 570) // 5 - 1
    lado = 1.0 if op["lado"] == "COMPRA" else -1.0; ent = float(c[k]); stop = float(op["stop"])
    p = (h + l) / 2.0; pv0 = float((p[:k + 1] * v[:k + 1]).sum()); vv0 = float(v[:k + 1].sum())
    rng = np.random.default_rng((semilla * 1000003 + int(op["fecha"].replace("-", ""))) % (2 ** 32))
    cprev = np.full(M, ent); pv = np.full(M, pv0); vv = np.full(M, vv0); vivo = np.ones(M, bool); pts = np.zeros(M)
    for j in range(k + 1, 78):
        eps = rng.integers(0, 2, M) * 2.0 - 1.0
        do, dh, dl, dc = o[j] - c[j - 1], h[j] - c[j - 1], l[j] - c[j - 1], c[j] - c[j - 1]
        o2 = cprev + eps * do; h2 = cprev + np.where(eps > 0, dh, -dl); l2 = cprev + np.where(eps > 0, dl, -dh); c2 = cprev + eps * dc
        lim = np.floor(pv / vv / TK + 0.5) * TK
        st = (l2 <= stop) if lado > 0 else (h2 >= stop)
        ob = (h2 >= lim + TK) if lado > 0 else (l2 <= lim - TK)
        gap = (o2 <= stop) if lado > 0 else (o2 >= stop)
        m1 = vivo & st; pts[m1] = (np.where(gap, o2, stop)[m1] - ent) * lado; vivo &= ~m1
        m2 = vivo & ob; pts[m2] = (lim[m2] - ent) * lado; vivo &= ~m2
        if j == 77:
            pts[vivo] = (c2[vivo] - ent) * lado; vivo[:] = False
        pv = pv + (h2 + l2) / 2.0 * v[j]; vv = vv + v[j]; cprev = c2
    return float((pts > 0).mean()), float(pts.mean())


# ------------------------------------------------------------------------------------------------ historia
def cargar_historia(estado, H, sembrar_desde_cintas):
    f = os.path.join(estado, "historia_rangos.json")
    if os.path.exists(f): return json.load(open(f, encoding="utf-8"))["rangos"]
    if sembrar_desde_cintas:
        return {}
    semilla = json.load(open(os.path.join(AQUI, SEMILLA), encoding="utf-8"))["rangos"]
    guardar_historia(estado, semilla)
    return dict(semilla)


def guardar_historia(estado, rangos):
    json.dump({"nota": "rango de la rueda (9:30-16:00 NY) de cada jornada completa; semilla: %s" % "historia_rangos_semilla.json",
               "rangos": rangos}, open(os.path.join(estado, "historia_rangos.json"), "w", encoding="utf-8"), indent=0)


# ------------------------------------------------------------------------------------------------ bitacoras y juez
def anotar(ruta, campos, filas):
    nuevo = not os.path.exists(ruta)
    with open(ruta, "a", newline="", encoding="utf-8") as fh:
        w = csv.DictWriter(fh, fieldnames=campos, extrasaction="ignore")
        if nuevo: w.writeheader()
        for fl in filas: w.writerow({k: fl.get(k, "") for k in campos})


def t_rueda(serie):
    serie = np.asarray(serie, float)
    if len(serie) < 2: return 0.0
    sd = serie.std(ddof=1)
    return float(serie.mean() / (sd / math.sqrt(len(serie)))) if sd > 0 else 0.0


def t_critico(alfa, gl):
    try:
        from scipy import stats
        return float(stats.t.ppf(1 - alfa, max(gl, 1)))
    except Exception:          # aproximacion de Cornish-Fisher si no esta scipy
        from statistics import NormalDist
        z = NormalDist().inv_cdf(1 - alfa); g = max(gl, 1)
        return z + (z ** 3 + z) / (4 * g) + (5 * z ** 5 + 16 * z ** 3 + 3 * z) / (96 * g * g)


def stats(b, dias):
    n = len(b)
    if n == 0: return dict(n=0, acierto=float("nan"), neto=float("nan"), t=0.0, pos=0, azar=float("nan"), formula=float("nan"))
    net = b["pts"].astype(float) - COSTO
    por = net.groupby(b["fecha"]).sum(); serie = np.array([por.get(d, 0.0) for d in dias])
    return dict(n=n, acierto=100 * float((b["pts"].astype(float) > 0).mean()), neto=float(net.mean()), t=t_rueda(serie), pos=int((serie > 0).sum()),
                azar=100 * float(b["azar_acierto"].astype(float).mean()), formula=100 * float(b["P_geometrica"].astype(float).mean()))


def juez(bitacora, jornadas, H):
    V = H["vara"]
    if not os.path.exists(jornadas):
        print("JUEZ: todavia no hay jornadas procesadas."); return {}
    J = pd.read_csv(jornadas, dtype=str)
    ev = sorted(J[J["estado"] == "evaluada"]["fecha"].unique())
    B = pd.read_csv(bitacora, dtype={"fecha": str}) if os.path.exists(bitacora) else pd.DataFrame(columns=CAMPOS_B)
    k = len(H["candidatas"]); alfa = V["alfa_una_cola"] / (k * len(V["lecturas_decisivas"])); out = {}
    print("JUEZ R23 (vara de PREREGISTRO_VIVO.md; alfa por prueba %.4f = %.2f / (%d candidata(s) x %d lecturas)): %d jornadas evaluadas (%s a %s), %d excluidas" % (
        alfa, V["alfa_una_cola"], k, len(V["lecturas_decisivas"]), len(ev), ev[0] if ev else "-", ev[-1] if ev else "-", int((J["estado"] == "excluida").sum())))
    for cid in H["candidatas"]:
        bc = B[B["candidata"] == cid]
        def de(nj):
            d = ev[:nj]; return stats(bc[bc["fecha"].isin(d)], d)
        estado = "en curso: %d de %d jornadas para la primera lectura decisiva" % (len(ev), V["lecturas_decisivas"][0]); final = False
        for x in sorted(set(V["lecturas_futilidad"] + V["lecturas_decisivas"])):
            if len(ev) < x or final: continue
            s_ = de(x)
            if x in V["lecturas_futilidad"] and s_["n"] >= V["futilidad_min_ops"] and s_["acierto"] < V["futilidad_acierto_menor_a"] and s_["neto"] <= 0:
                estado = "ABANDONADA por futilidad en la lectura de %d jornadas (%d ops, acierto %.1f %%, neto %+.3f)" % (x, s_["n"], s_["acierto"], s_["neto"]); final = True
            elif x in V["lecturas_decisivas"]:
                tc = t_critico(alfa, x - 1)
                ok12 = s_["acierto"] >= V["acierto_min"] and s_["neto"] > 0
                if ok12 and s_["t"] >= tc and s_["n"] >= V["ops_min"]:
                    estado = "PASA en la lectura de %d jornadas (%d ops, acierto %.1f %%, neto %+.3f, t %.2f >= %.2f)" % (x, s_["n"], s_["acierto"], s_["neto"], s_["t"], tc); final = True
                elif not ok12:
                    estado = "NO PASA en la lectura de %d jornadas (acierto %.1f %%, neto %+.3f)" % (x, s_["acierto"], s_["neto"]); final = True
                elif x == V["lecturas_decisivas"][-1]:
                    estado = "NO PASA en la lectura final de %d jornadas (t %.2f < %.2f o %d < %d operaciones)" % (x, s_["t"], tc, s_["n"], V["ops_min"]); final = True
                else:
                    estado = "SIN DECIDIR en la lectura de %d jornadas (acierto y neto bien; t %.2f < %.2f o %d < %d ops): sigue hasta %d" % (
                        x, s_["t"], tc, s_["n"], V["ops_min"], V["lecturas_decisivas"][-1])
        s_ = de(len(ev))
        print("  %s: %d operaciones (%.2f por jornada) | acierto %.1f %% (azar con la misma ubicacion %.1f %%, formula %.1f %%) | neto c1,00 %+.3f por operacion | "
              "t por jornada %.2f | jornadas + %d de %d" % (cid, s_["n"], s_["n"] / len(ev) if ev else 0, s_["acierto"], s_["azar"], s_["formula"], s_["neto"], s_["t"], s_["pos"], len(ev)))
        print("      ESTADO: %s" % estado)
        if len(ev) >= V["lectura_descriptiva"]:
            d15 = de(V["lectura_descriptiva"])
            print("      (lectura descriptiva de las primeras %d jornadas, no decide: %d ops, acierto %.1f %%, neto %+.3f)" % (V["lectura_descriptiva"], d15["n"], d15["acierto"], d15["neto"]))
        out[cid] = dict(s_, estado=estado)
    return out


# ------------------------------------------------------------------------------------------------ una jornada
def procesar(dia, ruta, contrato, mods, rangos, M_azar=200):
    fila_j = dict(fecha=dia, contrato=contrato, archivo=os.path.basename(ruta), procesada_AR=time.strftime("%Y-%m-%d %H:%M"))
    if dia in DIAS_CORTOS:
        fila_j.update(estado="excluida", motivo=DIAS_CORTOS[dia]); return fila_j, [], None
    m0 = next(iter(mods.values()))
    c_ = m0.leer_cinta(ruta)
    if c_ is None:
        fila_j.update(estado="excluida", motivo="sin ordenes en la rueda"); return fila_j, [], None
    if c_["dia"] != dia:
        fila_j.update(estado="excluida", motivo="la cinta es de la rueda %s, no de %s" % (c_["dia"], dia)); return fila_j, [], None
    ok, motivo, ult, hueco = chequeo_cinta(c_)
    rango = float(c_["hi"].max() - c_["lo"].min())
    fila_j.update(ultima_orden_rth="%02d:%02d:%02d" % (ult // 3600, ult % 3600 // 60, ult % 60), hueco_rth_max_s=round(hueco, 1), rango_rueda=rango)
    if not ok:
        fila_j.update(estado="excluida", motivo=motivo); return fila_j, [], None
    filas = []
    for cid, m in mods.items():
        ops, info = m.operar(ruta, rangos, cinta=c_)
        if "motivo" in info and not ops and "faltan" in str(info.get("motivo")):
            fila_j.update(estado="excluida", motivo=info["motivo"]); return fila_j, [], rango
        fila_j.update(X=info.get("X"), mediana_10=info.get("mediana_10"))
        fila_j["ops_" + cid] = len(ops)
        for o in ops:
            a_g, a_p = azar(c_, o, M=M_azar)
            filas.append(dict(o, azar_acierto=round(a_g, 4), azar_pts=round(a_p, 4), contrato=contrato, archivo=os.path.basename(ruta),
                              procesada_AR=time.strftime("%Y-%m-%d %H:%M")))
    fila_j.update(estado="evaluada", motivo="")
    return fila_j, filas, rango


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--resumen", action="store_true"); ap.add_argument("--verificar", default=None)
    ap.add_argument("--carpeta", default=FLUJO); ap.add_argument("--estado", default=os.path.join(os.environ.get("LOCALAPPDATA", AQUI), "PythiaGex_r23_vivo"))
    ap.add_argument("--desde", default=DESDE); ap.add_argument("--sembrar-desde-cintas", action="store_true")
    ap.add_argument("--bitacora", default=None)
    a = ap.parse_args()
    os.makedirs(a.estado, exist_ok=True)
    mods, H = candidatas_congeladas()
    pref = a.bitacora or (AQUI if a.carpeta == FLUJO else a.estado)
    BIT = os.path.join(pref, "bitacora_r23_vivo.csv"); JOR = os.path.join(pref, "bitacora_r23_jornadas.csv")
    print("R23 juez | candidatas congeladas: %s | estado: %s | cintas: %s" % (", ".join("%s (%s)" % (k, H["sha256"][v][:12]) for k, v in H["candidatas"].items()), a.estado, a.carpeta), flush=True)
    if a.resumen:
        juez(BIT, JOR, H); return
    crudas = cintas(a.carpeta)
    if a.verificar:
        rangos = json.load(open(os.path.join(AQUI, SEMILLA), encoding="utf-8"))["rangos"]
        dia = a.verificar
        if dia not in crudas: raise SystemExit("no esta la cinta de %s" % dia)
        hist = {d: r for d, r in rangos.items() if d < dia}
        fila_j, filas, _ = procesar(dia, crudas[dia][0], crudas[dia][1], mods, hist, M_azar=50)
        print("VERIFICACION %s (historia hasta la vispera, no se escribe nada): %s" % (dia, {k: fila_j.get(k) for k in ("estado", "motivo", "X", "mediana_10")}))
        for cid in mods:
            f = os.path.join(AQUI, "salidas", "ops-cinta-%s.csv" % cid)
            ref = pd.read_csv(f, dtype={"fecha": str}) if os.path.exists(f) else pd.DataFrame()
            ref = ref[ref["fecha"] == dia] if len(ref) else ref
            mias = [(x["hora_entrada"], x["lado"], x["entrada"], x["stop"], x["motivo"], x["hora_salida"], round(float(x["pts"]), 4)) for x in filas if x["candidata"] == cid]
            suyas = [(r["hora_entrada"], r["lado"], float(r["entrada"]), float(r["stop"]), r["motivo"], r["hora_salida"], round(float(r["pts"]), 4)) for _, r in ref.iterrows()]
            for x in mias: print("   en vivo  %s %s" % (cid, x))
            for x in suyas: print("   diseno   %s %s" % (cid, x))
            print("  %s: %s" % (cid, "COINCIDE" if mias == suyas else "DIFIERE"), flush=True)
        return
    rangos = cargar_historia(a.estado, H, a.sembrar_desde_cintas)
    hechas = set(pd.read_csv(JOR, dtype=str)["fecha"]) if os.path.exists(JOR) else set()
    pend = [(d, v) for d, v in sorted(crudas.items()) if d >= a.desde and d not in hechas]
    if not pend:
        print("no hay jornadas nuevas de ES desde el %s sin procesar" % a.desde)
    for dia, (ruta, contrato, listo) in pend:
        if ram_libre_gb() < 2.0: raise SystemExit("RAM libre < 2 GB: freno (regla del operador)")
        hist = {d: r for d, r in rangos.items() if d < dia}
        fila_j, filas, rango = procesar(dia, ruta, contrato, mods, hist)
        if fila_j["estado"] == "excluida" and str(fila_j.get("motivo", "")).startswith("cinta incompleta") and not listo:
            print("  %s (%s): %s -> la cinta no tiene .listo: queda PENDIENTE (no se anota; se reintenta en la proxima corrida)" % (
                dia, os.path.basename(ruta), fila_j["motivo"]), flush=True)
            continue
        if filas: anotar(BIT, CAMPOS_B, filas)
        anotar(JOR, CAMPOS_J, [fila_j])
        rueda_completa = (rango is not None) and (dia not in DIAS_CORTOS) and (
            fila_j["estado"] == "evaluada" or str(fila_j.get("motivo", "")).startswith("faltan"))
        if rueda_completa:
            rangos[dia] = rango; guardar_historia(a.estado, rangos)
        print("  %s (%s): %s %s | operaciones %s" % (dia, os.path.basename(ruta), fila_j["estado"], fila_j.get("motivo", ""),
                                                   {k: v for k, v in fila_j.items() if k.startswith("ops_")}), flush=True)
    juez(BIT, JOR, H)


if __name__ == "__main__":
    main()
