# -*- coding: utf-8 -*-
"""RONDA 19 - EXPLORADOR ZONAS (familias a: rechazo en zona, d: barrida que vuelve). Modulo comun.
- Lee SOLO la muestra de diseno (eventos_diseno.parquet). Nunca abre eventos_todos.parquet.
- Simulador: una posicion por vez por rueda (la siguiente entrada debe ser posterior a <salida>_seg_salida de la anterior),
  maximo 5 por rueda, entradas 10:00-15:30 NY por defecto (como las etapas 2-4 del pre-registro).
- Metricas: acierto (bruto > 0), esperanza neta con costo 0,50 y 1,00, t POR RUEDA (26 ruedas, las sin operaciones = 0),
  mejor y peor rueda, racha, caida.
- Cada variante evaluada (regla x salida) se anota en registro_variantes_zonas.csv (mismas columnas que registro_variantes.csv)
  y con columnas extra en resultados_zonas.csv.
- Placebo B (niveles corridos un delta por rueda, U(-2X,+2X) con |delta| >= X/2) recalculando el contacto con el nivel."""
import csv, glob, math, os, sys, time
import numpy as np, pandas as pd
from scipy import stats

try:   # prioridad baja (BELOW_NORMAL), igual que r19_comun
    import ctypes, ctypes.wintypes as _W
    _k = ctypes.windll.kernel32; _k.GetCurrentProcess.restype = _W.HANDLE; _k.SetPriorityClass.argtypes = [_W.HANDLE, _W.DWORD]
    _k.SetPriorityClass(_k.GetCurrentProcess(), 0x00004000)
except Exception:
    pass

AQUI = os.path.dirname(os.path.abspath(__file__))
R19 = os.path.dirname(AQUI)
SCR = r"C:\Users\wmx_7\AppData\Local\Temp\claude\6e29e09c80928880\233bf8d4-5113-4e95-a0d9-68bff726690d\scratchpad\r19"
BASE = os.path.join(SCR, "eventos_diseno.parquet")
REG = os.path.join(AQUI, "registro_variantes_zonas.csv")
RES = os.path.join(AQUI, "resultados_zonas.csv")
AGENTE = "r19-explorador-zonas"
CAMPOS = ["fecha_hora_AR", "agente", "familia", "variante", "salida", "parametros", "muestra", "ruedas", "n", "ops_por_rueda", "acierto_pct",
          "gana_media", "pierde_media", "bruto_op", "neto_op_c050", "neto_op_c100", "t_rueda_c050", "ruedas_positivas_c050",
          "racha_perdedora", "caida_max_c050", "sharpe_diario_c050", "placeboA_p", "nota"]
EXTRA = ["bruto_R", "t_rueda_c100", "mejor_rueda_c050", "peor_rueda_c050", "ruedas_con_ops", "placeboB_p", "placeboB_media_c050",
         "placeboB_acierto"]
TK = 0.25
NIVELES = ["vwr", "vwr+1", "vwr-1", "vwr+2", "vwr-2", "vwj", "vwj+1", "vwj-1", "vwj+2", "vwj-2", "vws", "vwm",
           "pdh", "pdl", "pdc", "pd_poc", "pd_vah", "pd_val", "asia_h", "asia_l", "lon_h", "lon_l",
           "ib_h", "ib_l", "ib_h05", "ib_h10", "ib_l05", "ib_l10", "ps_poc", "ps_vah", "ps_val", "r50"]
GRUPOS = {
    "todos": NIVELES,
    "vwap_rth": ["vwr", "vwr+1", "vwr-1", "vwr+2", "vwr-2"],
    "vwap_rth_bandas": ["vwr+1", "vwr-1", "vwr+2", "vwr-2"],
    "vwap_rth_2sd": ["vwr+2", "vwr-2"],
    "vwap_centro": ["vwr", "vwj"],
    "vwap_jornada": ["vwj", "vwj+1", "vwj-1", "vwj+2", "vwj-2"],
    "vwap_lento": ["vws", "vwm"],
    "dinamicos": ["vwr", "vwr+1", "vwr-1", "vwr+2", "vwr-2", "vwj", "vwj+1", "vwj-1", "vwj+2", "vwj-2", "vws", "vwm"],
    "ayer": ["pdh", "pdl", "pdc", "pd_poc", "pd_vah", "pd_val"],
    "ayer_extremos": ["pdh", "pdl"],
    "ayer_valor": ["pd_poc", "pd_vah", "pd_val", "pdc"],
    "noche": ["asia_h", "asia_l", "lon_h", "lon_l"],
    "ib": ["ib_h", "ib_l"],
    "ib_ext": ["ib_h05", "ib_h10", "ib_l05", "ib_l10"],
    "semana": ["ps_poc", "ps_vah", "ps_val"],
    "r50": ["r50"],
    # extremos donde se juntan stops (maximos y minimos de referencia)
    "extremos": ["pdh", "pdl", "asia_h", "asia_l", "lon_h", "lon_l", "ib_h", "ib_l"],
    "estaticos": ["pdh", "pdl", "pdc", "pd_poc", "pd_vah", "pd_val", "asia_h", "asia_l", "lon_h", "lon_l",
                  "ib_h", "ib_l", "ib_h05", "ib_h10", "ib_l05", "ib_l10", "ps_poc", "ps_vah", "ps_val", "r50"],
    "estaticos_sin_r50": ["pdh", "pdl", "pdc", "pd_poc", "pd_vah", "pd_val", "asia_h", "asia_l", "lon_h", "lon_l",
                          "ib_h", "ib_l", "ib_h05", "ib_h10", "ib_l05", "ib_l10", "ps_poc", "ps_vah", "ps_val"],
    "valor": ["pd_poc", "pd_vah", "pd_val", "ps_poc", "ps_vah", "ps_val"],
}
# grupos que dependen del lado de la operacion (1 = compra en un minimo, -1 = venta en un maximo)
GRUPOS_LADO = {
    "bandas_rev": {1: ["vwr-1", "vwr-2", "vwj-1", "vwj-2"], -1: ["vwr+1", "vwr+2", "vwj+1", "vwj+2"]},      # banda del lado opuesto: opera HACIA el VWAP
    "bandas_rev_rth": {1: ["vwr-1", "vwr-2"], -1: ["vwr+1", "vwr+2"]},
    "bandas_rev_2sd": {1: ["vwr-2", "vwj-2"], -1: ["vwr+2", "vwj+2"]},
    "bandas_rev_1sd": {1: ["vwr-1", "vwj-1"], -1: ["vwr+1", "vwj+1"]},
    "bandas_cont": {1: ["vwr+1", "vwr+2", "vwj+1", "vwj+2"], -1: ["vwr-1", "vwr-2", "vwj-1", "vwj-2"]},     # retroceso a la banda del lado de la extension
    "est_naturales": {1: ["pdl", "pd_val", "asia_l", "lon_l", "ib_l", "ib_l05", "ib_l10", "ps_val"],
                      -1: ["pdh", "pd_vah", "asia_h", "lon_h", "ib_h", "ib_h05", "ib_h10", "ps_vah"]},          # minimos como soporte / maximos como resistencia
    "est_inversion": {1: ["pdh", "pd_vah", "asia_h", "lon_h", "ib_h", "ib_h05", "ib_h10", "ps_vah"],
                      -1: ["pdl", "pd_val", "asia_l", "lon_l", "ib_l", "ib_l05", "ib_l10", "ps_val"]},          # maximo roto que se vuelve soporte (y espejo)
    "extremos_naturales": {1: ["pdl", "asia_l", "lon_l", "ib_l"], -1: ["pdh", "asia_h", "lon_h", "ib_h"]},
    "extremos_inversion": {1: ["pdh", "asia_h", "lon_h", "ib_h"], -1: ["pdl", "asia_l", "lon_l", "ib_l"]},
}
SALIDAS = ["S1", "S2", "S3", "S4", "S4b", "S5", "S6"]
H_INI, H_FIN = 1800, 21600      # 10:00 - 15:30 NY (segundos desde las 9:30)
_DF = None
DIAS = None


def cargar():
    global _DF, DIAS
    if _DF is not None:
        return _DF
    df = pd.read_parquet(BASE)
    assert (df["muestra"] == "diseno").all(), "la base de diseno trae otra muestra"
    df = df.sort_values(["dia", "seg_rueda"]).reset_index(drop=True)
    lado = df["lado"].to_numpy(); ext = df["ext"].to_numpy(); ent = df["entrada"].to_numpy()
    for nom in NIVELES:
        d = df["d_" + nom].to_numpy()
        df["pen_" + nom] = -d * lado                         # > 0: el extremo paso el nivel (del lado del retroceso)
        df["vuelve_" + nom] = (ent - (ext - d)) * lado > 0   # L = ext - d; el precio de entrada quedo del otro lado
    df["hacia_vwap"] = (df["vwap_rth_dist_pts"] < 0).astype(int)                   # el VWAP RTH queda del lado de la ganancia
    df["dist_vwap_R"] = -df["vwap_rth_dist_pts"] / df["R"]                         # cuantas R hay hasta el VWAP (positivo = a favor)
    df["vwap_sd_firmado"] = df["vwap_rth_dist_sd"] * df["lado"]                    # < 0: precio del lado contrario del VWAP, en desvios
    df["ext_vwap_sd_firmado"] = df["ext_dist_vwap_rth_sd"] * df["lado"]            # idem en el extremo del retroceso
    df["vwapj_sd_firmado"] = df["vwap_jornada_dist_sd"] * df["lado"]
    df["hacia_vws"] = (df["vwap_semanal_dist_pts"] < 0).astype(int)
    df["hacia_vwm"] = (df["vwap_mensual_dist_pts"] < 0).astype(int)
    df["hora"] = df["seg_rueda"] / 3600.0 + 9.5
    _DF = df
    DIAS = sorted(df["dia"].unique())
    return df


def contacto(df, grupo, lo, hi, vuelve=False, delta=None, lo_T=False):
    """Cuenta por evento cuantos niveles del grupo estan 'en contacto': lo*X <= pen <= hi*X (pen > 0 = perforo).
    lo_T=True usa -T (T = X/8 al tick, minimo 2 ticks) como borde inferior (la zona de la base). vuelve=True exige que la
    entrada quede del otro lado del nivel. delta (pts por evento) corre todos los niveles (placebo B)."""
    X = df["X"].to_numpy(); lado = df["lado"].to_numpy(); ext = df["ext"].to_numpy(); ent = df["entrada"].to_numpy()
    lo_v = -df["zona_T"].to_numpy() if lo_T else lo * X
    hi_v = hi * X
    cnt = np.zeros(len(df), int)
    if isinstance(grupo, str) and grupo in GRUPOS_LADO:
        grupo = GRUPOS_LADO[grupo]
    if isinstance(grupo, dict):     # grupo que depende del lado: {1: [...], -1: [...]}
        for s_, noms_ in grupo.items():
            c_ = contacto(df, noms_, lo, hi, vuelve, delta, lo_T)
            cnt += np.where(lado == s_, c_, 0)
        return cnt
    noms = GRUPOS[grupo] if isinstance(grupo, str) else grupo
    for nom in noms:
        if nom == "r50":
            if delta is None:
                L = 50.0 * np.round(ext / 50.0)
            else:
                L = 50.0 * np.round((ext - delta) / 50.0) + delta
        else:
            L = ext - df["d_" + nom].to_numpy()
            if delta is not None:
                L = L + delta
        pen = (L - ext) * lado
        ok = np.isfinite(pen) & (pen >= lo_v - 1e-9) & (pen <= hi_v + 1e-9)
        if vuelve:
            ok &= (ent - L) * lado > 0
        cnt += ok
    return cnt


def tomar(df, mask, sal, max_dia=5, h_ini=H_INI, h_fin=H_FIN):
    """Una posicion por vez por rueda. Devuelve los indices tomados (orden cronologico)."""
    m = np.asarray(mask, bool) & (df["seg_rueda"].to_numpy() >= h_ini) & (df["seg_rueda"].to_numpy() < h_fin) & df[sal + "_pts"].notna().to_numpy()
    idx = np.nonzero(m)[0]
    if len(idx) == 0:
        return idx
    dias = df["dia"].to_numpy()[idx]; t = df["seg_rueda"].to_numpy()[idx]; ts = df[sal + "_seg_salida"].to_numpy()[idx]
    out = []
    dia_act = None; libre = -1; n = 0
    for k in range(len(idx)):
        if dias[k] != dia_act:
            dia_act = dias[k]; libre = -1; n = 0
        if t[k] <= libre or n >= max_dia:
            continue
        out.append(idx[k]); libre = ts[k]; n += 1
    return np.array(out, int)


def serie_diaria(df, idx, pts_neto):
    s = pd.Series(pts_neto, index=df["dia"].to_numpy()[idx]).groupby(level=0).sum()
    return s.reindex(DIAS, fill_value=0.0)


def t_rueda(daily):
    sd = daily.std(ddof=1)
    return float(daily.mean() / sd * math.sqrt(len(daily))) if sd > 0 else 0.0


def metricas(df, idx, sal):
    pts = df[sal + "_pts"].to_numpy()[idx] if len(idx) else np.array([])
    R = df["R"].to_numpy()[idx] if len(idx) else np.array([])
    n = len(pts)
    out = dict(n=n, ruedas=len(DIAS), ops_por_rueda=round(n / len(DIAS), 3))
    if n == 0:
        out.update(acierto_pct=np.nan, bruto_op=np.nan, neto_op_c050=np.nan, neto_op_c100=np.nan, t_rueda_c050=0.0, t_rueda_c100=0.0)
        return out
    out["acierto_pct"] = round(100.0 * float((pts > 0).mean()), 1)
    out["gana_media"] = round(float(pts[pts > 0].mean()), 3) if (pts > 0).any() else np.nan
    out["pierde_media"] = round(float(pts[pts <= 0].mean()), 3) if (pts <= 0).any() else np.nan
    out["bruto_op"] = round(float(pts.mean()), 3)
    out["bruto_R"] = round(float((pts / R).mean()), 3)
    for c, k in ((0.5, "c050"), (1.0, "c100")):
        net = pts - c
        d = serie_diaria(df, idx, net)
        out["neto_op_" + k] = round(float(net.mean()), 3)
        out["t_rueda_" + k] = round(t_rueda(d), 3)
        if k == "c050":
            out["ruedas_positivas_c050"] = int((d > 0).sum())
            out["mejor_rueda_c050"] = round(float(d.max()), 2)
            out["peor_rueda_c050"] = round(float(d.min()), 2)
            out["sharpe_diario_c050"] = round(float(d.mean() / d.std(ddof=1)), 4) if d.std(ddof=1) > 0 else ""
            cum = np.cumsum(net); pico = np.maximum.accumulate(np.concatenate([[0.0], cum]))[1:]
            out["caida_max_c050"] = round(float(np.max(pico - cum)), 2)
            racha = 0; mx = 0
            for v in net:
                racha = racha + 1 if v < 0 else 0; mx = max(mx, racha)
            out["racha_perdedora"] = mx
            out["_serie050"] = d.to_numpy()
    out["ruedas_con_ops"] = int(len(set(df["dia"].to_numpy()[idx])))
    return out


def anotar(fila):
    nuevo = not os.path.exists(REG)
    with open(REG, "a", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=CAMPOS, extrasaction="ignore")
        if nuevo: w.writeheader()
        w.writerow({k: fila.get(k, "") for k in CAMPOS})
    nuevo = not os.path.exists(RES)
    with open(RES, "a", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=CAMPOS + EXTRA, extrasaction="ignore")
        if nuevo: w.writeheader()
        w.writerow({k: fila.get(k, "") for k in CAMPOS + EXTRA})


def evaluar(variante, familia, parametros, mask, salidas=("S6", "S1"), nota="", registrar=True, max_dia=5, h_ini=H_INI, h_fin=H_FIN,
            imprimir=True):
    df = cargar(); res = {}
    for sal in salidas:
        idx = tomar(df, mask, sal, max_dia, h_ini, h_fin)
        met = metricas(df, idx, sal)
        met.update(fecha_hora_AR=time.strftime("%Y-%m-%d %H:%M"), agente=AGENTE, familia=familia, variante=variante, salida=sal,
                   parametros=parametros, muestra="diseno", nota=nota)
        if registrar:
            anotar(met)
        met["_idx"] = idx
        res[sal] = met
        if imprimir:
            print("  %-34s %-3s n %4d (%.2f/r) acierto %5s %% bruto %+6.2f (%+.2fR) neto050 %+6.2f neto100 %+6.2f t %+5.2f mejor %+6.1f peor %+6.1f" % (
                variante[:34], sal, met["n"], met["ops_por_rueda"], met.get("acierto_pct"), met.get("bruto_op", np.nan) if met["n"] else 0,
                met.get("bruto_R", np.nan) if met["n"] else 0, met.get("neto_op_c050", np.nan) if met["n"] else 0,
                met.get("neto_op_c100", np.nan) if met["n"] else 0, met.get("t_rueda_c050", 0), met.get("mejor_rueda_c050", 0) or 0,
                met.get("peor_rueda_c050", 0) or 0), flush=True)
    return res


def deltas_placebo(rng, df):
    """Un delta por rueda ~ U(-2X, +2X) con |delta| >= X/2 (pre-registro, placebo B). Devuelve vector por evento."""
    out = np.zeros(len(df))
    for dia in DIAS:
        m = (df["dia"] == dia).to_numpy(); X = float(df.loc[m, "X"].iloc[0])
        while True:
            d = rng.uniform(-2 * X, 2 * X)
            if abs(d) >= X / 2: break
        out[m] = d
    return out


def placebo_B(regla, sal, draws=200, seed=19, max_dia=5, h_ini=H_INI, h_fin=H_FIN):
    """regla(df, delta) -> mascara. Corre la regla con los niveles corridos y compara el neto (c050) y el acierto."""
    df = cargar(); rng = np.random.default_rng(seed)
    real = metricas(df, tomar(df, regla(df, None), sal, max_dia, h_ini, h_fin), sal)
    netos = []; aciertos = []; ns = []; ts = []
    for _ in range(draws):
        dl = deltas_placebo(rng, df)
        idx = tomar(df, regla(df, dl), sal, max_dia, h_ini, h_fin)
        m = metricas(df, idx, sal)
        if m["n"] == 0:
            netos.append(-0.5); aciertos.append(0.0); ns.append(0); ts.append(0.0); continue
        netos.append(m["neto_op_c050"]); aciertos.append(m["acierto_pct"]); ns.append(m["n"]); ts.append(m["t_rueda_c050"])
    netos = np.array(netos); aciertos = np.array(aciertos); ts = np.array(ts)
    return dict(real_neto=real.get("neto_op_c050"), real_acierto=real.get("acierto_pct"), real_t=real.get("t_rueda_c050"), real_n=real["n"],
                plac_neto_media=round(float(netos.mean()), 3), plac_neto_p95=round(float(np.percentile(netos, 95)), 3),
                plac_acierto_media=round(float(aciertos.mean()), 1), plac_n_media=round(float(np.mean(ns)), 1),
                plac_t_media=round(float(ts.mean()), 2),
                p_neto=round(float((netos >= real.get("neto_op_c050", -9)).mean()), 3),
                p_acierto=round(float((aciertos >= real.get("acierto_pct", 101)).mean()), 3),
                p_t=round(float((ts >= real.get("t_rueda_c050", 99)).mean()), 3))


def contar_variantes_globales():
    """Suma las filas de diseno de TODOS los registro_variantes*.csv de la ronda (todos los exploradores)."""
    tot = 0; por = {}; srs = []; ruedas = None
    for f in sorted(set(glob.glob(os.path.join(R19, "**", "registro_variantes*.csv"), recursive=True))):
        k = 0
        with open(f, encoding="utf-8") as fh:
            for r in csv.DictReader(fh):
                if r.get("muestra") != "diseno": continue
                k += 1
                try:
                    if r.get("sharpe_diario_c050") not in ("", None): srs.append(float(r["sharpe_diario_c050"]))
                    if r.get("ruedas"): ruedas = int(float(r["ruedas"]))
                except ValueError:
                    pass
        por[os.path.relpath(f, R19)] = k; tot += k
    return tot, por, np.array(srs), ruedas


EULER = 0.5772156649


def bonferroni_y_dsr(serie_diaria_c050):
    N, por, srs, ruedas = contar_variantes_globales()
    T = len(serie_diaria_c050)
    tcrit = float(stats.t.ppf(1 - 0.05 / max(N, 1), T - 1))
    x = np.asarray(serie_diaria_c050, float); sd = x.std(ddof=1)
    sr = x.mean() / sd if sd > 0 else 0.0
    v = float(np.var(srs, ddof=1)) if len(srs) > 1 else 0.0
    sr0 = math.sqrt(v) * ((1 - EULER) * stats.norm.ppf(1 - 1 / N) + EULER * stats.norm.ppf(1 - 1 / (N * math.e))) if N > 1 else 0.0
    g3 = stats.skew(x); g4 = stats.kurtosis(x, fisher=False)
    z = (sr - sr0) * math.sqrt(T - 1) / math.sqrt(max(1e-12, 1 - g3 * sr + (g4 - 1) / 4 * sr * sr))
    return dict(N_total=N, por_registro=por, t_bonferroni=round(tcrit, 2), sharpe_diario=round(float(sr), 3), sr0_azar=round(float(sr0), 3),
                dsr=round(float(stats.norm.cdf(z)), 3))
