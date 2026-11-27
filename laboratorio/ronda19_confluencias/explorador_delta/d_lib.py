# -*- coding: utf-8 -*-
"""Explorador DELTA (ronda 19) - biblioteca comun: carga de la base de DISENO, rasgos derivados (causales), simulador de una
posicion por vez por rueda, estadisticas (vara doble), placebos y registro de variantes (registro_variantes_delta.csv).
Solo lee eventos_diseno.parquet. Nunca abre eventos_todos.parquet (confirmacion). Prioridad baja."""
import csv, datetime, glob, math, os, sys
try:
    import ctypes, ctypes.wintypes as _W
    _k = ctypes.windll.kernel32; _k.GetCurrentProcess.restype = _W.HANDLE; _k.SetPriorityClass.argtypes = [_W.HANDLE, _W.DWORD]
    _k.SetPriorityClass(_k.GetCurrentProcess(), 0x00004000)       # BELOW_NORMAL
except Exception:
    pass
import numpy as np, pandas as pd
from scipy import stats

AQUI = os.path.dirname(os.path.abspath(__file__))
R19 = os.path.dirname(AQUI)
SCR = r"C:\Users\wmx_7\AppData\Local\Temp\claude\6e29e09c80928880\233bf8d4-5113-4e95-a0d9-68bff726690d\scratchpad\r19"
BASE = os.path.join(SCR, "eventos_diseno.parquet")
REGISTRO = os.path.join(R19, "registro_variantes_delta.csv")
CAMPOS = ["fecha_hora_AR", "agente", "familia", "variante", "salida", "parametros", "muestra", "ruedas", "n", "ops_por_rueda", "acierto_pct",
          "gana_media", "pierde_media", "bruto_op", "neto_op_c050", "neto_op_c100", "t_rueda_c050", "ruedas_positivas_c050",
          "racha_perdedora", "caida_max_c050", "sharpe_diario_c050", "placeboA_p", "nota"]
AGENTE = "r19-explorador-delta"
FAMILIA = "e (delta y flujo agresivo en el extremo)"
TK = 0.25


def _seg_de_hora(h):
    if not isinstance(h, str) or h.count(":") != 2: return np.nan
    a, b, c = h.split(":"); return (int(a) - 9) * 3600 + (int(b) - 30) * 60 + int(c)


def cargar():
    df = pd.read_parquet(BASE)
    assert (df["muestra"] == "diseno").all(), "la base debe ser SOLO diseno"
    df = df.sort_values(["dia", "seg_rueda", "t_utc_ms"]).reset_index(drop=True)
    # ---- derivados causales (todo lo sabido al cierre del segundo de la vuelta o el anterior)
    df["seg_ext"] = df["hora_ext"].map(_seg_de_hora)
    df["seg_desde_ext"] = df["seg_rueda"] - df["seg_ext"]                 # cuanto tardo el rebote de X desde el extremo
    df["leg_pts"] = df["ctx_vel_ret"] * df["ctx_dur_ret_s"]                # tamano del retroceso que termino (pts)
    df["del_ret_contra"] = df["ctx_del_ret"] * (-df["lado"])               # delta del retroceso con el signo del retroceso (>0 = con agresores)
    df["del_ret_por_pt"] = df["del_ret_contra"] / df["leg_pts"].clip(lower=TK)
    df["d60_ratio"] = df["of_delta_60"] / df["of_vol_60"].clip(lower=1)    # delta 60 s a favor / volumen 60 s
    df["d300"] = df["of_delta_300"]
    df["giro_delta"] = ((df["of_delta_60"] > 0) & (df["of_delta_300"] < 0)).astype(int)   # 5 min a contra, ultimo minuto a favor
    df["imb_neto_3v"] = df["of_imb_favor_3v"] - df["of_imb_contra_3v"]
    df["ice_neto_60"] = df["of_icebergs_60"] - df["of_icebergs_contra_60"]
    df["big_neto"] = df["of_netbig60_a_favor"]
    df["marea_del_por_X"] = df["marea_delta"] * df["marea_dir"] / (df["ctx_pierna_marea_X"].clip(lower=1) * df["X"])
    df["hora_h"] = 9.5 + df["seg_rueda"] / 3600.0
    df["vw_lado"] = np.sign(df["vwap_rth_dist_pts"])                      # >0: entrada del lado del VWAP a favor (largo arriba)
    df["contra_marea"] = 1 - df["a_favor_marea"]
    # ---- S6m = ESPEJO de S6 (pre-registrado 04:20 AR): operacion CONTRARIA a la vuelta, misma entrada; objetivo = extremo -/+ 1 tick
    # (se llena cuando se toca el stop de la vuelta, ext -/+ 2 ticks: pasa 1 tick); stop = entrada +/- (R + 1 tick) (donde se llena el
    # objetivo 1R de la vuelta). Gana R - 1 tick si S6 fue 'stop', pierde R + 1 tick si S6 fue 'objetivo', y -S6 si fue 'fin'. Sale en el
    # mismo segundo que S6 (el primer toque de cualquiera de los dos niveles). Conservador en 1 tick por lado.
    m = df["S6_motivo"]
    df["S6m_pts"] = np.where(m == "stop", df["R"] - TK, np.where(m == "objetivo", -(df["R"] + TK), -df["S6_pts"]))
    df["S6m_seg_salida"] = df["S6_seg_salida"]
    df["S6m_motivo"] = np.where(m == "stop", "objetivo", np.where(m == "objetivo", "stop", "fin"))
    return df


DIAS = None


def dias(df):
    global DIAS
    if DIAS is None: DIAS = sorted(df["dia"].unique())
    return DIAS


def simular(ev, sal, max_dia=5):
    """Una posicion por vez por rueda: la siguiente entrada debe ser POSTERIOR al segundo de salida de la anterior; max 5 por rueda.
    Devuelve DataFrame de operaciones (con pts brutos)."""
    cp, cs = sal + "_pts", sal + "_seg_salida"
    e = ev[ev[cp].notna()].sort_values(["dia", "seg_rueda", "t_utc_ms"])
    keep = []
    for dia, g in e.groupby("dia", sort=True):
        libre = -1; n = 0
        for idx, sr, ss in zip(g.index.values, g["seg_rueda"].values, g[cs].values):
            if sr <= libre or n >= max_dia: continue
            keep.append(idx); libre = ss; n += 1
    ops = e.loc[keep].copy()
    ops["pts"] = ops[cp]; ops["pts_R"] = ops[cp] / ops["R"]
    return ops


def serie_diaria(ops, todos_dias, costo):
    s = (ops["pts"] - costo).groupby(ops["dia"]).sum()
    return np.array([s.get(d, 0.0) for d in todos_dias], float)


def t_rueda(x):
    sd = x.std(ddof=1)
    return float(x.mean() / sd * math.sqrt(len(x))) if sd > 0 else 0.0


def resumen(ops, todos_dias):
    n = len(ops); out = dict(n=n, ruedas=len(todos_dias), ops_por_rueda=round(n / len(todos_dias), 2))
    if n == 0:
        out.update(acierto_pct=np.nan, bruto_op=np.nan, neto_op_c050=np.nan, neto_op_c100=np.nan, t_rueda_c050=0.0, t_rueda_c100=0.0)
        return out
    p = ops["pts"].values
    s050 = serie_diaria(ops, todos_dias, 0.5); s100 = serie_diaria(ops, todos_dias, 1.0)
    neto = p - 0.5
    eq = np.cumsum(neto); dd = float(np.max(np.maximum.accumulate(np.concatenate([[0], eq])) - np.concatenate([[0], eq])))
    racha = 0; mr = 0
    for v in neto:
        racha = racha + 1 if v < 0 else 0; mr = max(mr, racha)
    con_ops = [d for d in todos_dias if d in set(ops["dia"])]
    sd = s050.std(ddof=1)
    out.update(acierto_pct=round(100 * float((p > 0).mean()), 1), gana_media=round(float(p[p > 0].mean()), 3) if (p > 0).any() else np.nan,
               pierde_media=round(float(p[p <= 0].mean()), 3) if (p <= 0).any() else np.nan, bruto_op=round(float(p.mean()), 3),
               bruto_R=round(float(ops["pts_R"].mean()), 3), neto_op_c050=round(float(p.mean() - 0.5), 3), neto_op_c100=round(float(p.mean() - 1.0), 3),
               t_rueda_c050=round(t_rueda(s050), 2), t_rueda_c100=round(t_rueda(s100), 2),
               ruedas_positivas_c050=int((s050 > 0).sum()), ruedas_con_ops=len(con_ops), racha_perdedora=mr, caida_max_c050=round(dd, 2),
               sharpe_diario_c050=round(float(s050.mean() / sd), 4) if sd > 0 else np.nan,
               mejor_rueda=round(float(s050.max()), 2), peor_rueda=round(float(s050.min()), 2),
               dia_mejor=todos_dias[int(np.argmax(s050))], dia_peor=todos_dias[int(np.argmin(s050))],
               neto_total_c050=round(float(s050.sum()), 2), R_medio=round(float(ops["R"].mean()), 2))
    return out


def anotar(variante, sal, parametros, r, placebo_p="", nota=""):
    nuevo = not os.path.exists(REGISTRO)
    fila = dict(fecha_hora_AR=datetime.datetime.now().strftime("%Y-%m-%d %H:%M"), agente=AGENTE, familia=FAMILIA, variante=variante, salida=sal,
                parametros=parametros, muestra="diseno", placeboA_p=placebo_p, nota=nota)
    for k in CAMPOS:
        if k in r and k not in fila: fila[k] = r[k]
    with open(REGISTRO, "a", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=CAMPOS, extrasaction="ignore")
        if nuevo: w.writeheader()
        w.writerow({k: fila.get(k, "") for k in CAMPOS})


def correr(df, mascara, variante, parametros, salidas=("S1", "S6"), registrar=True, nota="", max_dia=5):
    """Simula la regla (mascara booleana sobre df) con cada salida, la anota en el registro y devuelve {salida: (resumen, ops)}."""
    todos = dias(df); out = {}
    for sal in salidas:
        ops = simular(df[mascara], sal, max_dia=max_dia)
        r = resumen(ops, todos)
        if registrar: anotar(variante, sal, parametros, r, nota=nota)
        out[sal] = (r, ops)
    return out


def linea(nombre, sal, r):
    if r["n"] == 0: return "%-34s %-3s n    0" % (nombre, sal)
    return ("%-34s %-3s n %4d (%.2f/r) acierto %5.1f %% bruto %+6.2f (%+.2f R) neto050 %+6.2f neto100 %+6.2f t050 %+5.2f t100 %+5.2f "
            "ruedas+ %2d/%2d racha %2d caida %6.1f" % (nombre, sal, r["n"], r["ops_por_rueda"], r["acierto_pct"], r["bruto_op"], r["bruto_R"],
            r["neto_op_c050"], r["neto_op_c100"], r["t_rueda_c050"], r["t_rueda_c100"], r["ruedas_positivas_c050"], r["ruedas_con_ops"],
            r["racha_perdedora"], r["caida_max_c050"]))


# ------------------------------------------------------------------ placebos
def placebo_barajar(df, mascara_ctx, flujo_fn, sal, real_neto, draws=500, semilla=7, max_dia=5):
    """Placebo F: el rasgo de flujo se baraja ENTRE EVENTOS DE LA MISMA RUEDA (el contexto queda fijo). flujo_fn(df_barajado)->mascara.
    Devuelve (p una cola de que el placebo iguale o supere el neto medio por operacion real, media placebo, p del t por rueda)."""
    rng = np.random.default_rng(semilla); todos = dias(df)
    cols = flujo_fn.cols
    grupos = [g.index.values for _, g in df.groupby("dia")]
    vals = []; ts = []
    real_t = None
    for k in range(draws):
        sh = df.copy()
        for idx in grupos:
            perm = rng.permutation(idx)
            sh.loc[idx, cols] = df.loc[perm, cols].values
        m = mascara_ctx & flujo_fn(sh)
        ops = simular(df[m], sal, max_dia=max_dia)        # resultados REALES de los eventos elegidos con el rasgo barajado
        if len(ops) == 0: vals.append(-9); ts.append(-9); continue
        vals.append(float(ops["pts"].mean() - 0.5)); ts.append(t_rueda(serie_diaria(ops, todos, 0.5)))
    vals = np.array(vals); ts = np.array(ts)
    return float((vals >= real_neto).mean()), float(np.mean(vals[vals > -9])), vals, ts


def placebo_hora(df, ops_real, sal, draws=500, semilla=11, ventana_s=600, max_dia=5):
    """Placebo H: misma hora, direccion y contexto al azar: cada operacion real se cambia por un evento al azar de la MISMA rueda
    a <= 10 min (cualquier lado), y se re-simula una posicion por vez. Devuelve (p, media placebo, vals)."""
    rng = np.random.default_rng(semilla); todos = dias(df)
    cp = sal + "_pts"
    pool = df[df[cp].notna()]
    por_dia = {d: g for d, g in pool.groupby("dia")}
    real_neto = float(ops_real["pts"].mean() - 0.5)
    vals = []
    filas = list(zip(ops_real["dia"].values, ops_real["seg_rueda"].values))
    for k in range(draws):
        idx = []
        for d, s in filas:
            g = por_dia[d]; c = g.index.values[np.abs(g["seg_rueda"].values - s) <= ventana_s]
            if len(c): idx.append(rng.choice(c))
        ev = df.loc[sorted(set(idx))]
        ops = simular(ev, sal, max_dia=max_dia)
        vals.append(float(ops["pts"].mean() - 0.5) if len(ops) else -9)
    vals = np.array(vals)
    return float((vals >= real_neto).mean()), float(np.mean(vals[vals > -9])), vals


def placebo_lado(df, mascara, sal_real, draws=500, semilla=13, max_dia=5):
    """Placebo A (direccion al azar) para la familia S6/S6m: mismas vueltas elegidas; en cada una se sortea el lado (la vuelta = S6,
    la contraria = S6m; las dos salen en el mismo segundo, asi que la secuencia de una posicion por vez no cambia). p una cola."""
    rng = np.random.default_rng(semilla); todos = dias(df)
    ops = simular(df[mascara], sal_real, max_dia=max_dia)
    real = float(ops["pts"].mean() - 0.5)
    a = ops["S6_pts"].values; b = ops["S6m_pts"].values
    vals = np.empty(draws); ts = np.empty(draws); hits = np.empty(draws)
    for k in range(draws):
        lado = rng.random(len(a)) < 0.5
        p = np.where(lado, a, b); vals[k] = p.mean() - 0.5; hits[k] = (p > 0).mean()
        o2 = ops.copy(); o2["pts"] = p; ts[k] = t_rueda(serie_diaria(o2, todos, 0.5))
    return float((vals >= real).mean()), float(vals.mean()), float(hits.mean() * 100), vals, ts


def total_variantes():
    """Suma TODAS las variantes de diseno de todos los registro_variantes*.csv de la ronda (para la correccion)."""
    filas = []
    for f in sorted(glob.glob(os.path.join(R19, "registro_variantes*.csv"))):
        with open(f, encoding="utf-8") as fh:
            for r in csv.DictReader(fh):
                if r.get("muestra") == "diseno" and r.get("sharpe_diario_c050") not in ("", None, "nan"):
                    try: filas.append((os.path.basename(f), float(r["sharpe_diario_c050"]), float(r["t_rueda_c050"])))
                    except Exception: pass
    return filas


def bonferroni(N, ruedas=26, alfa=0.05):
    return float(stats.t.ppf(1 - alfa / N, ruedas - 1))


def dsr(serie, var_sr, N):
    x = np.asarray(serie, float); T = len(x); sd = x.std(ddof=1)
    if sd == 0 or T < 3 or N < 2: return np.nan, np.nan
    sr = x.mean() / sd; g3 = stats.skew(x); g4 = stats.kurtosis(x, fisher=False); e = 0.5772156649
    sr0 = math.sqrt(var_sr) * ((1 - e) * stats.norm.ppf(1 - 1 / N) + e * stats.norm.ppf(1 - 1 / (N * math.e)))
    z = (sr - sr0) * math.sqrt(T - 1) / math.sqrt(max(1e-12, 1 - g3 * sr + (g4 - 1) / 4 * sr * sr))
    return sr, float(stats.norm.cdf(z))
