# -*- coding: utf-8 -*-
"""
tablero_01_placebo.py — EL JUEZ DEL TABLERO A (los velocimetros de Flujo Claro) ANTES de que exista en pantalla.

Que se juzga: el puntaje S del resumen del tablero (correcciones del critico, 18-09-2026), calculado SEGUNDO A SEGUNDO sobre las tablas
de 1 s de MNQ de base.py (cinta del propio ATAS del operador, 20 ruedas), con las mismas reglas que va a usar el C#:
  FLUJO 1m   r60  = delta/vol de los ultimos 60 s          voto +1/0/-1, zona muerta |r| < 0,10, PESA DOBLE
  FLUJO 5m   r300 = delta/vol de los ultimos 300 s         voto +1/0/-1, zona muerta |r| < 0,10
  GRANDES 5m G300 = delta de ordenes de 50+ en 300 s       vota si |G300| >= 0,3 x p95 de |G300| en los 30 min previos (t-1800..t-1)
  CVD 15m    D900 = delta de 900 s                         vota si |D900| >= 1 desvio de D900 en los 30 min previos
  PRECIO 15m ret900 = ultimo[t] - ultimo[t-900]            vota si |ret900| >= mediana de |ret900| del dia hasta ahi (desde las 22:00 UTC)
  S = 2 x FLUJO1m + FLUJO5m + GRANDES + CVD + PRECIO (de -6 a +6)
  estado: |S| <= 1 PAREJO · 2-3 compran / venden · >= 4 COMPRAN FUERTE / VENDEN FUERTE · "a favor n/5" = lecturas con el signo de S

CRITERIO ESCRITO ANTES DE CORRER (no se toca despues de ver numeros):
  - Solo rueda 13:32:00-20:00:00 UTC, sin los ultimos 900 s de la tabla y sin hueco (B.hueco) entre t-600 y t+900. Entrada = ultimo[t].
  - Dos muestras: GRILLA (una fila cada 15 s, segundos :14 :29 :44 :59, lo que el tablero MUESTRA) y CAMBIOS (el segundo en que el estado
    pasa a ser otro distinto de PAREJO, lo que el tablero va a GRABAR en tablero-<inst>-<dia>.jsonl).
  - (a) DESCRIBE: para cada estado con lado, % de veces en que el retorno de los 60 s ANTERIORES (ultimo[t] - ultimo[t-60]) tiene el signo del
    estado. Es lo que el tablero afirma ser: un estado de ahora.
  - (b) ANTICIPA: barrera +-8 en 600 s (B.barrera, mira de t+1 en adelante): % de veces que toca primero del lado del estado, sobre las resueltas;
    y retorno firmado (lado x (ultimo[t+k] - ultimo[t])) a 60 y 300 s. Contra TRES placebos con los MISMOS lados:
      P1 AZAR       el resultado de un segundo elegible al azar de la misma sesion (200 sorteos);
      P2 CORRIDO    el resultado 30 min DESPUES (el estado de hace media hora aplicado ahora; no comparte ventana con lo que armo el estado);
      P3 OTRO DIA   el resultado del mismo segundo del dia en OTRA sesion del mismo tramo (200 sorteos).
  - Explorar (sesiones anteriores al 08-09) y confirmar (08-09 en adelante) por separado; intervalo del 90 % remuestreando DIAS enteros (2000 veces).
  - Un estado ANTICIPA solo si en confirmar Y en explorar su acierto +-8/600 supera el empate (56,0 % con costo 0,96) y le gana a P1 y a P3
    con z >= 2 y a P2 por 2 puntos o mas. Si no, el tablero describe y no anticipa, y asi se le dice al operador.
  - De yapa (no decide nada): la tabla de VELOCIDAD por rango crudo de 5 min (< 16, 16-24, 24-32, 32-48, >= 48 pts): mediana de segundos hasta
    tocar +-8 y % sin resolver a los 180 s, para verificar los numeros de techo_ml.md 210-214 que el tablero va a mostrar.
Uso: python laboratorio/gatillo/tablero_01_placebo.py   (prioridad baja: la pone base.py al importarse)
Salida: resultados/tablero.md
"""
import os, sys, time
import numpy as np, pandas as pd
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import base as B

AQUI = os.path.dirname(os.path.abspath(__file__))
CACHE = os.path.join(AQUI, "tablero_cache"); os.makedirs(CACHE, exist_ok=True)
SALIDA = os.path.join(AQUI, "resultados", "tablero.md")
X, H = 8, 600
ZONA_R = 0.10; FRAC_P95 = 0.3; K_SD_CVD = 1.0; PREVIO = 1800; MINIMO = 600
INICIO_SOD = 13 * 3600 + 32 * 60; COLA = 900; PASO = 15; CORRIDO = 1800
SORTEOS = 200; BOOT = 2000; SEMILLA = 7
FIRMA = "v1_z%.2f_g%.1f_c%.1f" % (ZONA_R, FRAC_P95, K_SD_CVD)
NOMBRE = {-2: "VENDEN FUERTE", -1: "venden", 0: "PAREJO", 1: "compran", 2: "COMPRAN FUERTE"}
ESCALONES = [(0, 16, "DORMIDO (< 16)"), (16, 24, "tranquilo (16-24)"), (24, 32, "normal (24-32)"), (32, 48, "movido (32-48)"), (48, 1e9, "DESATADO (>= 48)")]
TECHO = {"DORMIDO (< 16)": "97-125 s / 24-36 %", "tranquilo (16-24)": "75-86 s / 17-26 %", "normal (24-32)": "52 s / 9-11 %", "movido (32-48)": "29-31 s / 3 %", "DESATADO (>= 48)": "13-14 s / 1 %"}


# ------------------------------------------------------------------ utilidades causales
def _suma(x, W):
    """Suma de los W segundos que terminan en t (incluye t); parcial al principio."""
    c = np.cumsum(x); out = c.copy(); out[W:] = c[W:] - c[:-W]; return out


def _lag(x, k):
    out = np.full(len(x), np.nan); out[k:] = x[:-k]; return out


def _lead(x, k):
    out = np.full(len(x), np.nan); out[:-k] = x[k:]; return out


def _voto(x, umbral):
    """+1/-1 si |x| >= umbral (y el umbral existe y es > 0), 0 si no."""
    x = np.asarray(x, "float64"); u = np.asarray(umbral, "float64")
    ok = np.isfinite(x) & np.isfinite(u) & (u > 0) & (np.abs(x) >= u)
    return np.where(ok, np.sign(x), 0).astype("int8")


# ------------------------------------------------------------------ lecturas, puntaje y objetivos de UNA sesion
def tabla(ses, seg):
    pq = os.path.join(CACHE, "tab-%s-%s.parquet" % (ses, FIRMA))
    if os.path.exists(pq): return pd.read_parquet(pq)
    p = seg["ultimo"].to_numpy("float64"); delta = seg["delta"].to_numpy("float64"); vol = seg["vol"].to_numpy("float64"); d50 = seg["d50"].to_numpy("float64")
    hi = seg["alto"].to_numpy("float64"); lo = seg["bajo"].to_numpy("float64"); n = len(p)
    with np.errstate(divide="ignore", invalid="ignore"):
        r60 = np.where(_suma(vol, 60) > 0, _suma(delta, 60) / _suma(vol, 60), 0.0)
        r300 = np.where(_suma(vol, 300) > 0, _suma(delta, 300) / _suma(vol, 300), 0.0)
    v1 = _voto(r60, np.full(n, ZONA_R)); v5 = _voto(r300, np.full(n, ZONA_R))
    G300 = _suma(d50, 300); p95 = pd.Series(np.abs(G300)).rolling(PREVIO, min_periods=MINIMO).quantile(0.95).shift(1).to_numpy()
    vG = _voto(G300, FRAC_P95 * p95)
    D900 = _suma(delta, 900); sdC = pd.Series(D900).rolling(PREVIO, min_periods=MINIMO).std().shift(1).to_numpy()
    vC = _voto(D900, K_SD_CVD * sdC)
    ret900 = p - _lag(p, 900); medP = pd.Series(np.abs(ret900)).expanding(min_periods=MINIMO).median().to_numpy()
    vP = _voto(ret900, medP)
    S = (2 * v1.astype(int) + v5 + vG + vC + vP).astype("int8")
    aS = np.abs(S); e = (np.where(aS <= 1, 0, np.where(aS <= 3, 1, 2)) * np.sign(S)).astype("int8")
    sg = np.sign(S); afavor = sum(((v == sg) & (sg != 0)).astype(int) for v in (v1, v5, vG, vC, vP)).astype("int8")
    y, tt = B.barrera(seg, X, H)
    sod = ((seg.index.view("int64") // 10**9) % 86400).astype("int64")
    ok = seg["rueda"].to_numpy() & (sod >= INICIO_SOD); ok[max(0, n - COLA):] = False
    h = pd.Series(seg["hueco"].to_numpy().astype("float64"))
    atras = h.rolling(601, min_periods=1).max().to_numpy(); adel = h[::-1].rolling(901, min_periods=1).max().to_numpy()[::-1]
    eleg = ok & (atras == 0) & (adel == 0)
    rng300 = pd.Series(hi).rolling(300, min_periods=1).max().to_numpy() - pd.Series(lo).rolling(300, min_periods=1).min().to_numpy()
    out = pd.DataFrame({"sod": sod, "S": S, "e": e, "afavor": afavor, "v1": v1, "v5": v5, "vG": vG, "vC": vC, "vP": vP,
                        "r60": r60.astype("float32"), "r300": r300.astype("float32"), "y": y.astype("int8"), "tt": tt.astype("float32"),
                        "f60": (_lead(p, 60) - p).astype("float32"), "f300": (_lead(p, 300) - p).astype("float32"), "rprev60": (p - _lag(p, 60)).astype("float32"),
                        "rng300": rng300.astype("float32"), "eleg": eleg}, index=seg.index)
    out.to_parquet(pq); return out


# ------------------------------------------------------------------ juicio
def _ic_dias(valores, dias, rng, b=BOOT):
    """Media agrupada remuestreando DIAS enteros: (p5, p95, dias con media > 0,5, dias)."""
    v = pd.Series(valores).groupby(np.asarray(dias)).agg(["sum", "size"]); s = v["sum"].to_numpy("float64"); c = v["size"].to_numpy("float64"); k = len(s)
    if k == 0: return (np.nan, np.nan, 0, 0)
    arriba = int(((v["sum"] / v["size"]) > 0.5).sum())
    if k < 3: return (np.nan, np.nan, arriba, k)
    idx = rng.integers(0, k, size=(b, k)); m = s[idx].sum(1) / c[idx].sum(1)
    return (float(np.percentile(m, 5)), float(np.percentile(m, 95)), arriba, k)


def _ic_dias_media(valores, dias, rng, b=BOOT):
    v = pd.Series(valores).groupby(np.asarray(dias)).agg(["sum", "size"]); s = v["sum"].to_numpy("float64"); c = v["size"].to_numpy("float64"); k = len(s)
    if k < 3: return (np.nan, np.nan)
    idx = rng.integers(0, k, size=(b, k)); m = s[idx].sum(1) / c[idx].sum(1)
    return (float(np.percentile(m, 5)), float(np.percentile(m, 95)))


def _acierto(y, lado):
    ok = y != 0
    return (float(((y[ok] * lado[ok]) > 0).mean()) if ok.sum() else np.nan), int(ok.sum())


def juzgar(R, M, ses_lista, rng):
    """R = filas (ses, sod, dia, e, lado, y, f60, f300, rprev60); M = matrices [sesion, segundo del dia] de y, f60, f300, eleg.
    Devuelve, por estado (y para todos los que tienen lado), la senal y los tres placebos."""
    out = {}; ses_i = {s: i for i, s in enumerate(ses_lista)}; k = len(ses_lista)
    for est in (-2, -1, 1, 2, "lado"):
        d = R[R["e"] == est] if est != "lado" else R[R["e"] != 0]
        if not len(d): out[est] = dict(n=0); continue
        lado = d["lado"].to_numpy("int8"); y = d["y"].to_numpy("int8"); f60 = d["f60"].to_numpy("float64"); f300 = d["f300"].to_numpy("float64")
        dias = d["dia"].to_numpy(); rp = d["rprev60"].to_numpy("float64"); sesarr = d["ses"].to_numpy(); sodarr = d["sod"].to_numpy()
        ii = np.array([ses_i[s] for s in sesarr])
        r = dict(n=len(d), n_dias=int(d["dia"].nunique()))
        okp = rp != 0; r["describe"] = float((np.sign(rp[okp]) == lado[okp]).mean()) if okp.sum() else np.nan
        r["describe_pts"] = float((lado * rp).mean())
        r["acierto"], r["n_res"] = _acierto(y, lado); r["sin_resolver"] = float((y == 0).mean())
        okr = y != 0; r["ic"] = _ic_dias(((y * lado) > 0)[okr].astype(float), dias[okr], rng)
        r["m60"] = float(np.nanmean(lado * f60)); r["m300"] = float(np.nanmean(lado * f300))
        r["ic60"] = _ic_dias_media(np.nan_to_num(lado * f60), dias, rng); r["ic300"] = _ic_dias_media(np.nan_to_num(lado * f300), dias, rng)
        # P1 AZAR: un segundo elegible al azar de la misma sesion, con el mismo lado
        acs, m60s, m300s = [], [], []
        pools = {s: np.flatnonzero(M["eleg"][ses_i[s]]) for s in np.unique(sesarr)}
        for _ in range(SORTEOS):
            yy = np.zeros(len(d), "int8"); a = np.zeros(len(d)); b = np.zeros(len(d))
            for s, pool in pools.items():
                m = np.flatnonzero(sesarr == s); pick = pool[rng.integers(0, len(pool), size=len(m))]; i = ses_i[s]
                yy[m] = M["y"][i][pick]; a[m] = M["f60"][i][pick]; b[m] = M["f300"][i][pick]
            acs.append(_acierto(yy, lado)[0]); m60s.append(np.nanmean(lado * a)); m300s.append(np.nanmean(lado * b))
        r["p1"] = (float(np.nanmean(acs)), float(np.nanstd(acs, ddof=1)), int(np.sum(np.array(acs) >= r["acierto"])), float(np.nanmean(m60s)), float(np.nanmean(m300s)))
        # P2 CORRIDO: el resultado 30 min despues, si ese segundo tambien es elegible
        sod2 = sodarr + CORRIDO; val = sod2 < 86400
        y2 = np.zeros(len(d), "int8"); e2 = np.zeros(len(d), bool); a2 = np.full(len(d), np.nan); b2 = np.full(len(d), np.nan)
        y2[val] = M["y"][ii[val], sod2[val]]; e2[val] = M["eleg"][ii[val], sod2[val]]; a2[val] = M["f60"][ii[val], sod2[val]]; b2[val] = M["f300"][ii[val], sod2[val]]
        if e2.sum():
            ac2, n2 = _acierto(y2[e2], lado[e2]); ok2 = (y2 != 0) & e2
            r["p2"] = (ac2, n2, _ic_dias(((y2 * lado) > 0)[ok2].astype(float), dias[ok2], rng), float(np.nanmean((lado * a2)[e2])), float(np.nanmean((lado * b2)[e2])))
        else: r["p2"] = (np.nan, 0, (np.nan, np.nan, 0, 0), np.nan, np.nan)
        # P3 OTRO DIA: el mismo segundo del dia en otra sesion del tramo, con el mismo lado
        acs, m60s, m300s = [], [], []
        if k >= 2:
            for _ in range(SORTEOS):
                otro = (ii + rng.integers(1, k, size=len(d))) % k; e3 = M["eleg"][otro, sodarr]
                if not e3.any(): continue
                yy = M["y"][otro, sodarr]; a = M["f60"][otro, sodarr]; b = M["f300"][otro, sodarr]
                acs.append(_acierto(yy[e3], lado[e3])[0]); m60s.append(np.nanmean((lado * a)[e3])); m300s.append(np.nanmean((lado * b)[e3]))
        r["p3"] = (float(np.nanmean(acs)), float(np.nanstd(acs, ddof=1)), int(np.sum(np.array(acs) >= r["acierto"])), float(np.nanmean(m60s)), float(np.nanmean(m300s))) if acs else (np.nan, np.nan, 0, np.nan, np.nan)
        out[est] = r
    return out


def z(real, placebo):
    m, sd = placebo[0], placebo[1]
    return (real - m) / sd if np.isfinite(sd) and sd > 0 else np.nan


def linea(nombre, r):
    if not r.get("n"): return "   %-16s sin casos" % nombre
    ic = r["ic"]; p1 = r["p1"]; p2 = r["p2"]; p3 = r["p3"]
    s = "   %-16s n %5d (%2d dias) | describe: ultimo minuto del mismo lado %4.1f %% (%+.2f pts) | +-8/600: %4.1f %% [IC %4.1f;%4.1f] dias>50 %% %d de %d, sin resolver %2.0f %%" % (
        nombre, r["n"], r["n_dias"], 100 * r["describe"], r["describe_pts"], 100 * r["acierto"], 100 * ic[0], 100 * ic[1], ic[2], ic[3], 100 * r["sin_resolver"])
    s += "\n   %-16s   placebos: AZAR %4.1f +- %3.1f (z %+.1f, lo iguala %d/%d) | CORRIDO 30 min %4.1f [%4.1f;%4.1f] n %d | OTRO DIA %4.1f +- %3.1f (z %+.1f, lo iguala %d/%d)" % (
        "", 100 * p1[0], 100 * p1[1], z(r["acierto"], p1), p1[2], SORTEOS, 100 * p2[0], 100 * p2[2][0], 100 * p2[2][1], p2[1], 100 * p3[0], 100 * p3[1], z(r["acierto"], p3), p3[2], SORTEOS)
    s += "\n   %-16s   retorno firmado 60 s %+.2f [%+.2f;%+.2f] (azar %+.2f, corrido %+.2f, otro dia %+.2f) | 300 s %+.2f [%+.2f;%+.2f] (azar %+.2f, corrido %+.2f, otro dia %+.2f)" % (
        "", r["m60"], r["ic60"][0], r["ic60"][1], p1[3], p2[3], p3[3], r["m300"], r["ic300"][0], r["ic300"][1], p1[4], p2[4], p3[4])
    return s


def anticipa(rx, rc):
    """La regla escrita: supera el empate y le gana a los tres placebos en los DOS tramos."""
    emp = B.empate(X)
    def ok(r):
        if not r.get("n") or r["n_res"] < 30: return False
        return r["acierto"] > emp and z(r["acierto"], r["p1"]) >= 2 and z(r["acierto"], r["p3"]) >= 2 and (r["acierto"] - r["p2"][0]) >= 0.02
    return ok(rx) and ok(rc)


# ------------------------------------------------------------------ principal
if __name__ == "__main__":
    t0 = time.time(); T = B.todas_las_sesiones(); rng = np.random.default_rng(SEMILLA)
    TAB = {s: tabla(s, seg) for s, seg in T.items()}
    print("tablas listas: %d sesiones en %.0f s" % (len(TAB), time.time() - t0))
    M_all = {}
    for s, t in TAB.items():
        m = {k: np.zeros(86400, dt) for k, dt in (("y", "int8"), ("f60", "float64"), ("f300", "float64"), ("eleg", bool))}
        sod = t["sod"].to_numpy()
        for k in m: m[k][sod] = t[k].to_numpy() if k == "eleg" else np.nan_to_num(t[k].to_numpy())
        M_all[s] = m
    L = ["# Familia \"tablero\" — el resumen del tablero A (velocimetros) juzgado ANTES de dibujarlo (18-09-2026)\n"]
    L.append("Pregunta: el estado COMPRAN / PAREJO / VENDEN que va a mostrar Flujo Claro, describe la vela actual (lo que dice ser) y anticipa algo (lo que NO dice ser)?")
    L.append("Datos: tablas de 1 s de MNQ de base.py (cinta del propio ATAS del operador), %d sesiones del %s al %s. Codigo: tablero_01_placebo.py (el criterio esta en su docstring y se escribio antes de correr)." % (len(T), min(T), max(T)))
    L.append("Reglas del puntaje: FLUJO 1m (x2) y FLUJO 5m = delta/vol con zona muerta |r| < %.2f; GRANDES 5m = delta de 50+ en 300 s contra %.1f x p95 de los 30 min previos; CVD 15m = delta de 900 s contra %.0f desvio de los 30 min previos; PRECIO 15m = retorno de 900 s contra la mediana de |retorno| del dia. S de -6 a +6: |S| <= 1 PAREJO, 2-3 debil, >= 4 fuerte." % (ZONA_R, FRAC_P95, K_SD_CVD))
    L.append("Muestras: GRILLA = una fila cada 15 s (lo que se muestra); CAMBIOS = el segundo en que el estado pasa a otro distinto de PAREJO (lo que se graba en tablero-<inst>-<dia>.jsonl). Solo rueda 13:32-20:00 UTC, sin huecos, sin los ultimos 900 s.")
    L.append("Placebos con los MISMOS lados: AZAR = segundo elegible al azar de la misma sesion (200 sorteos); CORRIDO = el resultado 30 min despues; OTRO DIA = mismo segundo del dia en otra sesion del mismo tramo (200 sorteos). IC 90 %% remuestreando dias (%d veces). Empate de +-8 con costo %.2f: %.1f %%.\n" % (BOOT, B.COSTO_PTS, 100 * B.empate(X)))
    resumen = {}
    for tramo, sel in (("EXPLORAR", B.explorar(TAB)), ("CONFIRMAR", B.confirmar(TAB))):
        ses = sorted(sel)
        M = {k: np.stack([M_all[s][k] for s in ses]) for k in ("y", "f60", "f300", "eleg")}
        filas = {"GRILLA": [], "CAMBIOS": []}
        for s in ses:
            t = sel[s]; e = t["e"].to_numpy(); el = t["eleg"].to_numpy(); sod = t["sod"].to_numpy()
            g = el & (sod % PASO == PASO - 1)
            prev = np.roll(e, 1); prev[0] = 0; c = el & (e != prev) & (e != 0)
            for nombre, msk in (("GRILLA", g), ("CAMBIOS", c)):
                d = t[msk].copy(); d["ses"] = s; d["dia"] = s; d["lado"] = np.sign(d["e"]).astype("int8"); filas[nombre].append(d)
        cab = "\n## %s: %d sesiones (%s a %s)" % (tramo, len(ses), ses[0], ses[-1]); print(cab); L.append(cab)
        G = pd.concat(filas["GRILLA"]); C = pd.concat(filas["CAMBIOS"])
        rep = G.groupby("e").size() / len(G) * 100
        s1 = "   reparto del tiempo (grilla, n %d): " % len(G) + " · ".join("%s %.1f %%" % (NOMBRE[k], rep.get(k, 0.0)) for k in (-2, -1, 0, 1, 2))
        af = G[G["e"] != 0]["afavor"].value_counts(normalize=True).sort_index()
        vot = "   lecturas que votan (grilla, %% del tiempo con voto distinto de 0): FLUJO 1m %.0f · FLUJO 5m %.0f · GRANDES %.0f · CVD 15m %.0f · PRECIO 15m %.0f | a favor cuando hay lado: %s" % tuple(
            [100 * (G[k] != 0).mean() for k in ("v1", "v5", "vG", "vC", "vP")] + [" · ".join("%d/5 %.0f %%" % (k, 100 * v) for k, v in af.items())])
        pd_ = G.groupby(["dia", "e"]).size().unstack(fill_value=0).reindex(columns=[-2, -1, 0, 1, 2], fill_value=0); pd_ = (pd_.T / pd_.sum(1)).T * 100
        pdc = C.groupby(["dia", "e"]).size().unstack(fill_value=0).reindex(columns=[-2, -1, 1, 2], fill_value=0)
        s2 = "   por dia (grilla, % del tiempo VF/v/P/c/CF | cambios de estado VF/v/c/CF): " + " | ".join("%s %s (%s)" % (
            d[5:], "/".join("%.0f" % pd_.loc[d, k] for k in (-2, -1, 0, 1, 2)), "/".join(("%d" % pdc.loc[d, k]) if d in pdc.index else "0" for k in (-2, -1, 1, 2))) for d in pd_.index)
        cambios_dia = C.groupby("dia").size()
        s3 = "   cambios de estado por rueda: mediana %.0f, min %d, max %d (un cambio cada %.0f s de rueda elegible en promedio)" % (cambios_dia.median(), cambios_dia.min(), cambios_dia.max(), len(G) * PASO / max(1, len(C)))
        for s_ in (s1, vot, s2, s3): print(s_); L.append(s_)
        for nombre in ("GRILLA", "CAMBIOS"):
            R = pd.concat(filas[nombre]); J = juzgar(R, M, ses, rng); resumen[(tramo, nombre)] = J
            cab2 = "\n### %s — %s (n %d)" % (tramo, nombre, len(R)); print(cab2); L.append(cab2)
            for est in (2, 1, -1, -2, "lado"):
                s_ = linea(NOMBRE[est] if est != "lado" else "TODOS con lado", J[est]); print(s_); L.append(s_)
        Gv = G.copy(); Gv["esc"] = pd.cut(Gv["rng300"], [e[0] for e in ESCALONES] + [1e9], labels=[e[2] for e in ESCALONES], right=False)
        s4 = "   VELOCIDAD (rango crudo de los ultimos 5 min, grilla): " + " | ".join("%s: %.0f %% del tiempo, mediana hasta +-8 %.0f s, sin resolver a 180 s %.0f %% (techo_ml: %s)" % (
            k, 100 * len(g) / len(Gv), np.median(g["tt"]), 100 * (g["tt"] > 180).mean(), TECHO[k]) for k, g in Gv.groupby("esc", observed=True))
        print(s4); L.append("\n" + s4)
    # ------------------------------------------------------------ la frase honesta (regla escrita antes de correr, ver docstring)
    L.append("\n## VEREDICTO (regla escrita antes de correr)")
    frases = []
    for nombre in ("GRILLA", "CAMBIOS"):
        for est in (2, 1, -1, -2, "lado"):
            rx = resumen[("EXPLORAR", nombre)][est]; rc = resumen[("CONFIRMAR", nombre)][est]
            if anticipa(rx, rc): frases.append("%s en %s ANTICIPA segun la regla (explorar %.1f %%, confirmar %.1f %%)." % (NOMBRE[est] if est != "lado" else "TODOS con lado", nombre, 100 * rx["acierto"], 100 * rc["acierto"]))
    gx = resumen[("EXPLORAR", "GRILLA")]["lado"]; gc = resumen[("CONFIRMAR", "GRILLA")]["lado"]; cx = resumen[("EXPLORAR", "CAMBIOS")]["lado"]; cc = resumen[("CONFIRMAR", "CAMBIOS")]["lado"]
    L.append("- Describe: cuando el tablero tiene lado, el ultimo minuto fue del mismo lado el %.0f %% (explorar) y el %.0f %% (confirmar) de las veces en la grilla; en el segundo del cambio de estado, %.0f %% y %.0f %%." % (100 * gx["describe"], 100 * gc["describe"], 100 * cx["describe"], 100 * cc["describe"]))
    L.append("- Anticipa: +-8 en 10 min del lado del tablero (todos los estados con lado): grilla %.1f %% -> %.1f %% (azar %.1f / %.1f, corrido %.1f / %.1f, otro dia %.1f / %.1f); cambios %.1f %% -> %.1f %% (azar %.1f / %.1f, corrido %.1f / %.1f, otro dia %.1f / %.1f). Empate %.1f %%." % (
        100 * gx["acierto"], 100 * gc["acierto"], 100 * gx["p1"][0], 100 * gc["p1"][0], 100 * gx["p2"][0], 100 * gc["p2"][0], 100 * gx["p3"][0], 100 * gc["p3"][0],
        100 * cx["acierto"], 100 * cc["acierto"], 100 * cx["p1"][0], 100 * cc["p1"][0], 100 * cx["p2"][0], 100 * cc["p2"][0], 100 * cx["p3"][0], 100 * cc["p3"][0], 100 * B.empate(X)))
    if frases: L.append("- Estados que pasan la regla en los dos tramos: " + " ".join(frases))
    else: L.append("- NINGUN estado pasa la regla en los dos tramos: el tablero describe la vela actual y NO anticipa el +-8 de los proximos 10 minutos.")
    L.append("\n**Para el operador, en criollo:** el tablero A te dice quien esta mandando en este momento y de donde viene el precio, y eso lo dice bien (cuando marca COMPRAN, el ultimo minuto subio la mayoria de las veces). "
             + ("Pero hacia adelante no sabe nada: apostar +-8 puntos para el lado que marca acierta lo mismo que apostar al azar, que apostar con el estado de hace media hora, o que apostar en otro dia a la misma hora. Es un espejo retrovisor prolijo, no un parabrisas. Sirve para no operar EN CONTRA de lo que esta pasando, nunca como razon para entrar." if not frases
                else "Y en esta muestra algun estado le gano a los tres placebos en los dos tramos (ver arriba); con 20 ruedas es una pista, no una prueba: hay que verlo repetirse en el registro en sombra del propio tablero antes de creerle."))
    L.append("\nTiempo de corrida: %.0f s." % (time.time() - t0))
    with open(SALIDA, "w", encoding="utf-8") as fh: fh.write("\n".join(L) + "\n")
    print("\nescrito %s en %.0f s" % (SALIDA, time.time() - t0))
