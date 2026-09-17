# -*- coding: utf-8 -*-
"""modelo_es_lib.py - lo comun de la familia MODELO_ES: juzgar con dias NUEVOS el gatillo "modelo·es10" (logistica MES, +3 antes que -3 en 10 min).

Fuentes (solo lectura, todas del ATAS del operador):
  VELAS   %APPDATA%/ATAS/pythiagex-centinela-rebobinado-atas-MES-TimeFrame-M2.jsonl  (200 MB: se lee por lineas; la ultima escritura de cada vela gana)
          %APPDATA%/ATAS/pythiagex-centinela-hoy-MES-TimeFrame-M2.jsonl              (las velas que el vivo fue anotando; sin niveles)
  DISPAROS %APPDATA%/ATAS/pythiagex-gatillos-MES-TimeFrame-M2.jsonl (vivo) y pythiagex-gatillos-archivo-MES-TimeFrame-M2.jsonl (ultimo rebobinado)
  MODELO  laboratorio/modelo_MES_M2_5velas.json (el que corre en el grafico de 2 min; ajustado con 16 dias, 19-08 al 10-09)

Convenciones:
  - una vela de 2 min se identifica por su APERTURA (piso de 2 min de la hora anotada: el rebobinado anota el ultimo trade, p. ej. 18:01:59 -> vela 18:00);
  - el disparo en vivo lleva la hora del ultimo trade de la vela cerrada (18:01:59 -> vela 18:00); el de archivo lleva la apertura de la vela SIGUIENTE (18:04:00 -> vela 18:02);
  - desenlace: entrada = cierre de la vela del disparo; se miran las 5 velas de 2 min siguientes (10 min), del mismo dia y consecutivas;
    +3 a favor antes que -3 en contra; si las dos caen en la misma vela NO se sabe (no resuelto); si ninguna, no resuelto.
"""
import io, json, math, os, pickle
import numpy as np, pandas as pd

try:
    import ctypes; ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x00004000)
except Exception: pass

APP = os.path.join(os.environ.get("APPDATA", ""), "ATAS")
AQUI = os.path.dirname(os.path.abspath(__file__))
LAB = os.path.dirname(AQUI)
CACHE = os.path.join(AQUI, "modelo_es_cache"); os.makedirs(CACHE, exist_ok=True)
G, H = 3.0, 5                     # +-3 puntos de ES, 5 velas de 2 min
COSTO_ES = 0.49                   # SUPUESTO (no medido en esta sesion): 1 tick de spread (0,25) + comision ~USD 1,2 ida y vuelta en MES (0,24 pts a USD 5 el punto)
EMPATE = (G + COSTO_ES) / (2 * G)  # 58,2 %
CORTE_OOS = "2026-09-11"          # el modelo M2 se ajusto con dias hasta el 10-09 inclusive: fuera de muestra = 11-09 en adelante
RUEDA = ("13:30", "20:00")


def velas_m2(refrescar=False):
    """Velas de 2 min de MES con niveles (la ultima escritura de cada vela gana). Indice = apertura de la vela (UTC)."""
    pq = os.path.join(CACHE, "velas_m2.parquet"); src = os.path.join(APP, "pythiagex-centinela-rebobinado-atas-MES-TimeFrame-M2.jsonl")
    if not refrescar and os.path.exists(pq) and os.path.getmtime(pq) >= os.path.getmtime(src): return pd.read_parquet(pq)
    filas = {}
    with io.open(src, encoding="utf-8", errors="replace") as fh:
        for l in fh:
            try: j = json.loads(l)
            except Exception: continue
            if j.get("c") is None: continue
            nv = j.get("niv") or {}; of = j.get("of") or {}
            r = dict(t_anotada=j["t"][:19], o=j["o"], h=j["h"], l=j["l"], c=j["c"], vol=float(j.get("vol") or 0), ops=float(j.get("ops") or 0), delta=float(j.get("delta") or 0),
                     con_niv=bool(nv), dmax=of.get("dmax"), dmin=of.get("dmin"))
            for k in ("zero_vol", "zero_oi", "mp_vol", "mn_vol", "mp_oi", "mn_oi", "dom0", "dom1", "mc1", "mc5", "mc30", "pico", "q_cuadrante"): r[k] = nv.get(k)
            filas[j["t"][:19]] = r
    d = pd.DataFrame.from_dict(filas, orient="index"); d.index = pd.to_datetime(d.index); d = d.sort_index()
    d["ap"] = d.index.floor("2min"); d = d[~d["ap"].duplicated(keep="last")].set_index("ap")
    for k in ("zero_vol", "zero_oi", "mp_vol", "mn_vol", "mp_oi", "mn_oi", "dom0", "dom1", "mc1", "mc5", "mc30", "pico", "q_cuadrante", "dmax", "dmin"): d[k] = pd.to_numeric(d[k], errors="coerce")
    d.to_parquet(pq); return d


CAMPOS_NIV = ("zero_vol", "zero_oi", "mp_vol", "mn_vol", "mp_oi", "mn_oi", "dom0", "dom1", "mc1", "mc5", "mc30", "pico", "q_cuadrante")


def corridas(refrescar=False):
    """El archivo rebobinado se escribe por CORRIDAS (cada rebobinado agrega todas sus velas en orden). En la semana del roll hay corridas en U6 y en Z6:
    NO se pueden mezclar. Devuelve (tabla de velas de las corridas elegidas con columna 'corrida', tabla dia -> corrida 'ultima' y 'primera' completa).
    ultima = la ultima corrida que cubre la rueda de ese dia con la mayor cantidad de velas; primera = la primera que llega a esa cantidad."""
    pq = os.path.join(CACHE, "corridas.parquet"); pk = os.path.join(CACHE, "corridas_dias.pkl"); src = os.path.join(APP, "pythiagex-centinela-rebobinado-atas-MES-TimeFrame-M2.jsonl")
    if not refrescar and os.path.exists(pq) and os.path.exists(pk) and os.path.getmtime(pq) >= os.path.getmtime(src): return pd.read_parquet(pq), pickle.load(open(pk, "rb"))
    lim = []; cuenta = []; prev = ""                      # pasada 1: limites de cada corrida y velas de rueda por dia
    with io.open(src, encoding="utf-8", errors="replace") as fh:
        for n, l in enumerate(fh):
            i = l.find('"t":"'); t = l[i + 5:i + 24]
            if not lim or t < prev: lim.append(n); cuenta.append({})
            prev = t; hm = t[11:16]
            if RUEDA[0] <= hm < RUEDA[1]: cuenta[-1][t[:10]] = cuenta[-1].get(t[:10], 0) + 1
    dias = sorted({d for c in cuenta for d in c}); eleccion = {}
    for d in dias:
        mx = max(c.get(d, 0) for c in cuenta); ids = [k for k, c in enumerate(cuenta) if c.get(d, 0) == mx]
        eleccion[d] = dict(ultima=ids[-1], primera=ids[0], velas=mx)
    quiero = sorted({e["ultima"] for e in eleccion.values()} | {e["primera"] for e in eleccion.values()}); qs = set(quiero)
    filas = []; k = -1; lim_set = set(lim)
    with io.open(src, encoding="utf-8", errors="replace") as fh:   # pasada 2: solo las corridas elegidas
        for n, l in enumerate(fh):
            if n in lim_set: k += 1
            if k not in qs: continue
            try: j = json.loads(l)
            except Exception: continue
            if j.get("c") is None: continue
            nv = j.get("niv") or {}
            r = dict(corrida=k, t_anotada=j["t"][:19], o=j["o"], h=j["h"], l=j["l"], c=j["c"], vol=float(j.get("vol") or 0), delta=float(j.get("delta") or 0), con_niv=bool(nv))
            for c in CAMPOS_NIV: r[c] = nv.get(c)
            filas.append(r)
    d = pd.DataFrame(filas); d["ap"] = pd.to_datetime(d["t_anotada"]).dt.floor("2min")
    for c in CAMPOS_NIV: d[c] = pd.to_numeric(d[c], errors="coerce")
    d = d.drop_duplicates(subset=["corrida", "ap"], keep="last").reset_index(drop=True)
    d.to_parquet(pq); pickle.dump(eleccion, open(pk, "wb")); return d, eleccion


def velas_hoy():
    """Las velas que anoto el VIVO (sin niveles). Sirven para controlar que el rebobinado tiene la misma vela."""
    src = os.path.join(APP, "pythiagex-centinela-hoy-MES-TimeFrame-M2.jsonl"); filas = {}
    with io.open(src, encoding="utf-8", errors="replace") as fh:
        for l in fh:
            try: j = json.loads(l)
            except Exception: continue
            if j.get("c") is None: continue
            filas[j["t"][:19]] = dict(o=j["o"], h=j["h"], l=j["l"], c=j["c"], vol=float(j.get("vol") or 0), delta=float(j.get("delta") or 0))
    d = pd.DataFrame.from_dict(filas, orient="index"); d.index = pd.to_datetime(d.index); d = d.sort_index()
    d["ap"] = d.index.floor("2min"); return d[~d["ap"].duplicated(keep="last")].set_index("ap")


def disparos_log(archivo=False):
    """Los disparos modelo·es10 del registro. Columna 'ap' = apertura de la vela que disparo."""
    src = os.path.join(APP, "pythiagex-gatillos-%sMES-TimeFrame-M2.jsonl" % ("archivo-" if archivo else "")); out = []
    with io.open(src, encoding="utf-8", errors="replace") as fh:
        for l in fh:
            try: j = json.loads(l)
            except Exception: continue
            if j.get("tipo") != "modelo·es10": continue
            t = pd.Timestamp(j["t"][:19])
            ap = (t - pd.Timedelta(minutes=2)).floor("2min") if archivo else t.floor("2min")
            out.append(dict(t=t, ap=ap, lado=int(j["lado"]), precio=float(j["precio"]), p=float(j["dz"]), zero=float(j.get("dom") or 0), fuente=j.get("fuente")))
    d = pd.DataFrame(out)
    return d.drop_duplicates(subset=["ap"], keep="first").reset_index(drop=True) if len(d) else d


def modelo(nombre="modelo_MES_M2_5velas.json"):
    return json.load(open(os.path.join(LAB, nombre)))


def p_congelada(v, m=None, a_la_cs=True):
    """La p del modelo CONGELADO sobre las velas v (todas, en orden), calcada de GatilloModelo.cs:
    ventana movil de 60 velas PROCESADAS (el indicador solo le pasa al modelo las velas con cadena = con niveles), sin corte por dia;
    rt = mediana del rango de las previas (elemento n/2 del ordenado; hacen falta 20), dz = delta / desvio poblacional de las previas,
    cum15 = 15 deltas previos + el propio, ret15 = c - cierre de hace 15 velas; niveles: zero_vol, mp_vol, mn_vol, dominante mas cercana arriba/abajo, mc30.
    Devuelve una tabla con p y los rasgos, indexada como v (solo velas con niveles)."""
    m = m or modelo(); mu, sc, co, b0 = np.array(m["media"]), np.array(m["escala"]), np.array(m["coef"]), m["intercepto"]
    w = v[v["con_niv"]] if a_la_cs else v
    cs, ds, rs = [], [], []; filas = []
    for ap, r in zip(w.index, w.itertuples(index=False)):
        c, delta, rango = r.c, r.delta, r.h - r.l
        if len(rs) >= 20:
            o = sorted(rs); rt = o[len(o) // 2]
            if rt <= 0: rt = 0.25
        else: rt = rango if rango > 0 else 0.25
        dz = 0.0
        if len(ds) >= 20:
            mm = sum(ds) / len(ds); sd = math.sqrt(sum((x - mm) ** 2 for x in ds) / len(ds)); dz = delta / (sd if sd > 0 else 1.0)
        cum = sum(ds[-15:]) + delta
        ret15 = c - cs[-15] if len(cs) >= 15 else (c - cs[0] if cs else 0.0)
        def D(x, sin): return sin if (x is None or not np.isfinite(x) or x <= 0) else (x - c) / rt
        doms = [x for x in (r.dom0, r.dom1) if x is not None and np.isfinite(x) and x > 0]
        arr = [x for x in doms if x > c]; aba = [x for x in doms if x <= c]
        f = [ret15 / rt, D(r.zero_vol, 0.0), rango / rt, D(r.mn_vol, -40.0), D(min(arr) if arr else None, 40.0), cum / (abs(cum) + 500.0),
             D(r.mc30, 0.0), dz, D(r.mp_vol, 40.0), D(max(aba) if aba else None, -40.0)]
        z = b0 + float(np.dot(co, (np.array(f) - mu) / sc)); p = 1.0 / (1.0 + math.exp(-z))
        filas.append([ap, p, len(rs)] + f)
        cs.append(c); ds.append(delta); rs.append(rango)
        if len(cs) > 60: cs.pop(0); ds.pop(0); rs.pop(0)
    out = pd.DataFrame(filas, columns=["ap", "p", "previas"] + m["rasgos"]).set_index("ap")
    return out


def desenlaces(v, g=G, h=H):
    """Por cada vela: y = +1 si desde su cierre el precio toca +g antes que -g en las h velas siguientes (consecutivas, de 2 min, mismo dia UTC), -1 al reves,
    0 si no se resolvio (ninguna, o las dos en la misma vela). 'completo' = habia las h velas siguientes sin agujeros (o se resolvio antes del agujero)."""
    idx = v.index; hi = v["h"].to_numpy(); lo = v["l"].to_numpy(); c = v["c"].to_numpy(); n = len(v)
    y = np.zeros(n, int); amb = np.zeros(n, bool); completo = np.zeros(n, bool); k_res = np.full(n, np.nan)
    ts = idx.values.astype("datetime64[s]").astype("int64")
    for i in range(n):
        ok = True
        for k in range(1, h + 1):
            j = i + k
            if j >= n or ts[j] - ts[i] != 120 * k: ok = False; break
            up = hi[j] >= c[i] + g; dn = lo[j] <= c[i] - g
            if up and dn: amb[i] = True; k_res[i] = k; break
            if up: y[i] = 1; k_res[i] = k; break
            if dn: y[i] = -1; k_res[i] = k; break
        completo[i] = ok
    return pd.DataFrame({"y": y, "ambigua": amb, "completo": completo, "k": k_res}, index=idx)


def en_rueda(idx, desde=RUEDA[0], hasta=RUEDA[1]):
    hm = idx.strftime("%H:%M"); return (hm >= desde) & (hm < hasta)


def elegir(p, umbral=0.70, enfriamiento=5, mascara=None):
    """Los disparos como en GatilloModelo.cs: p >= umbral (largo) o p <= 1 - umbral (corto), con enfriamiento en VELAS PROCESADAS desde el ultimo disparo.
    OJO: en el indicador el enfriamiento corre ANTES del filtro de la tarde (un disparo de las 13:58 NY tapa al de las 14:02). mascara = filtro posterior (p. ej. tarde)."""
    pv = p["p"].to_numpy(); n = len(pv); lado = np.zeros(n, int); ult = -10 ** 6
    for i in range(n):
        if i - ult >= enfriamiento:
            if pv[i] >= umbral: lado[i] = 1; ult = i
            elif pv[i] <= 1 - umbral: lado[i] = -1; ult = i
    s = pd.Series(lado, index=p.index)
    if mascara is not None: s = s.where(mascara, 0)
    return s[s != 0]


def juzgar(y, lado, dias=None):
    y = np.asarray(y); lado = np.asarray(lado); ok = y != 0; n = int(ok.sum())
    if n == 0: return dict(n=0, sin_resolver=int((~ok).sum()))
    ac = float(((y[ok] * lado[ok]) > 0).mean()); r = dict(n=n, acierto=round(100 * ac, 1), z50=round((ac - 0.5) / math.sqrt(0.25 / n), 2), sin_resolver=int((~ok).sum()))
    if dias is not None:
        dd = pd.Series((y[ok] * lado[ok]) > 0).groupby(np.asarray(dias)[ok]).agg(["mean", "size", "sum"])
        r["dias_arriba"] = "%d de %d" % (int((dd["mean"] > 0.5).sum()), len(dd)); r["por_dia"] = " ".join("%s %d/%d" % (d[5:], s, t) for d, s, t in zip(dd.index, dd["sum"], dd["size"]))
    return r


def placebo(tiros, univ, n_sorteos=200, semilla=7):
    """tiros: tabla con indice = apertura de la vela y columnas lado; univ: tabla de TODAS las velas elegibles con columna y (desenlace de subir).
    Por cada disparo se sortea otra vela del MISMO dia y la MISMA media hora (con desenlace completo), con el mismo lado. Devuelve media, desvio de la tasa de acierto."""
    rng = np.random.RandomState(semilla)
    clave_u = univ.index.strftime("%Y-%m-%d") + "|" + (univ.index.floor("30min")).strftime("%H:%M"); yu = univ["y"].to_numpy()
    grupos = {}
    for k, yy in zip(clave_u, yu): grupos.setdefault(k, []).append(yy)
    grupos = {k: np.array(a) for k, a in grupos.items()}
    clave_t = tiros.index.strftime("%Y-%m-%d") + "|" + (tiros.index.floor("30min")).strftime("%H:%M"); lados = tiros["lado"].to_numpy()
    tasas = []
    for _ in range(n_sorteos):
        ac = res = 0
        for k, ld in zip(clave_t, lados):
            a = grupos.get(k)
            if a is None or len(a) == 0: continue
            yy = a[rng.randint(len(a))]
            if yy != 0: res += 1; ac += int(yy * ld > 0)
        if res: tasas.append(ac / res)
    return float(np.mean(tasas)), float(np.std(tasas))
