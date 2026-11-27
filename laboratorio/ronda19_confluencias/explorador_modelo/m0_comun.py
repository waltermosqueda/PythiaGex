# -*- coding: utf-8 -*-
"""RONDA 19, explorador MODELO (meta-etiquetado, Lopez de Prado): comun.
- carga SOLO la muestra de diseno (eventos_diseno.parquet; nunca eventos_todos)
- arma los rasgos causales (firmados por el lado y normalizados por X o por la escala de volumen de la franja), elegidos A PRIORI
  desde la teoria y el pedido del operador (zonas, niveles, VWAP, order flow, estructura de ola/marea), ANTES de mirar etiquetas
- etiquetas: y1 = S1 (2R) termina > 0 ; y6 = S6 (1R) termina > 0
- simulacion: una posicion por vez por rueda (la siguiente entrada debe ser posterior al _seg_salida de la anterior), max 5 por rueda
- metricas con la vara doble (acierto + esperanza neta con costo 0,50 y 1,00, t POR RUEDA con ruedas sin operaciones = 0)
- registro de variantes con las MISMAS columnas que registro_variantes.csv
Las columnas de resultado (*_pts, *_motivo, *_seg_salida, mfe_R, mae_R, seg_hasta_stop) NUNCA entran como rasgo."""
import csv, ctypes, math, os, sys, time
import numpy as np, pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
SCR = r"C:\Users\wmx_7\AppData\Local\Temp\claude\6e29e09c80928880\233bf8d4-5113-4e95-a0d9-68bff726690d\scratchpad\r19"
DISENO = os.path.join(SCR, "eventos_diseno.parquet")
TMP = os.path.join(SCR, "modelo")
os.makedirs(TMP, exist_ok=True)
REGISTRO = os.path.join(AQUI, "registro_variantes_modelo.csv")
CAMPOS = ["fecha_hora_AR", "agente", "familia", "variante", "salida", "parametros", "muestra", "ruedas", "n", "ops_por_rueda", "acierto_pct",
          "gana_media", "pierde_media", "bruto_op", "neto_op_c050", "neto_op_c100", "t_rueda_c050", "ruedas_positivas_c050",
          "racha_perdedora", "caida_max_c050", "sharpe_diario_c050", "placeboA_p", "nota"]
NR = 23400


def prioridad_baja():
    try:
        k = ctypes.windll.kernel32
        k.GetCurrentProcess.restype = ctypes.c_void_p
        k.SetPriorityClass.argtypes = [ctypes.c_void_p, ctypes.c_uint32]
        k.SetPriorityClass(k.GetCurrentProcess(), 0x00000040)   # IDLE_PRIORITY_CLASS
    except Exception:
        pass


PROHIBIDAS = ("_pts", "_motivo", "_seg_salida")
FUTURO = {"mfe_R", "mae_R", "seg_hasta_stop"}

FAM_ZONA = {"vwap_central": ("vwr", "vwj"), "vwap_bandas": ("vwr+1", "vwr-1", "vwr+2", "vwr-2", "vwj+1", "vwj-1", "vwj+2", "vwj-2"),
            "vws_vwm": ("vws", "vwm"), "dia_ant": ("pdh", "pdl", "pdc", "pd_poc", "pd_vah", "pd_val"),
            "noche": ("asia_h", "asia_l", "lon_h", "lon_l"), "ib": ("ib_h", "ib_l", "ib_h05", "ib_h10", "ib_l05", "ib_l10"),
            "sem_ant": ("ps_poc", "ps_vah", "ps_val"), "r50": ("r50",)}
FIJOS = set(FAM_ZONA["dia_ant"] + FAM_ZONA["noche"] + FAM_ZONA["ib"] + FAM_ZONA["sem_ant"] + FAM_ZONA["r50"])
DINAM = set(FAM_ZONA["vwap_central"] + FAM_ZONA["vwap_bandas"] + FAM_ZONA["vws_vwm"])


def cargar():
    df = pd.read_parquet(DISENO)
    assert (df["muestra"] == "diseno").all(), "solo diseno"
    df = df.sort_values(["dia", "seg_rueda", "t_utc_ms"]).reset_index(drop=True)
    return df


def _fams(s, prefijo_barrida=False):
    out = set()
    if not isinstance(s, str) or not s:
        return out
    for tok in s.split("|"):
        nom = tok.split(":")[0]
        for f, noms in FAM_ZONA.items():
            if nom in noms:
                out.add(f)
    return out


def _niveles(s):
    if not isinstance(s, str) or not s:
        return set()
    return {tok.split(":")[0] for tok in s.split("|")}


def rasgos(df):
    """Rasgos causales, firmados por el lado (positivo = a favor de la operacion salvo que se diga) y normalizados."""
    L = df["lado"].to_numpy().astype(float)
    X = df["X"].to_numpy()
    Rp = df["R"].to_numpy()
    escv = (df["of_u_venta30"] + df["of_u_compra30"]).to_numpy() / 2.0          # escala de volumen de la franja (p99,5 de 30 s)
    escv = np.where(escv > 0, escv, np.nan)
    escb = ((df["of_u_netbig60_p80"] - df["of_u_netbig60_p20"]) / 2.0).to_numpy()
    escb = np.where(escb > 0, escb, np.nan)
    F = pd.DataFrame(index=df.index)
    # A. estructura ola / marea / retroceso
    F["a_favor_marea"] = df["a_favor_marea"]
    F["marea_agr_favor"] = ((df["a_favor_marea"] == 1) & (df["marea_con_agresores"] == 1)).astype(int)
    F["marea_delta_favor"] = df["marea_delta"].to_numpy() * L / escv
    F["extension_fuera_ruido"] = df["extension_fuera_ruido"]
    F["extension_fuera_ruido_contra"] = df["extension_fuera_ruido_contra"]
    F["prof_ret_X"] = df["ctx_prof_X"]
    F["pierna_marea_X"] = df["ctx_pierna_marea_X"]
    F["dur_ret_log"] = np.log1p(df["ctx_dur_ret_s"].clip(lower=0))
    F["vel_ret_X"] = df["ctx_vel_ret"] / X
    F["del_ret_favor"] = df["ctx_del_ret"].to_numpy() * L / escv          # >0: el retroceso se hizo con delta A FAVOR (sin agresores)
    F["ret_con_agresores"] = df["ctx_ret_con_agresores"]
    F["R_en_X"] = df["R_en_X"]
    F["hora"] = df["seg_rueda"] / 3600.0 + 9.5
    # B. contexto del dia / ruido / balance inicial / area de valor de ayer
    sig = df["ruido_sigma"].to_numpy()
    F["ext_sigma_favor"] = df["mov_desde_apertura"].to_numpy() * L / np.where(sig > 0, sig, np.nan)   # <0: extendido EN CONTRA (compra abajo)
    F["ruido_a_favor"] = df["ruido_a_favor_en_vuelta"]
    F["ruido_en_contra"] = df["ruido_en_contra_en_vuelta"]
    ib = df["ctx_ext_ib"].to_numpy()
    F["ib_favor"] = (((ib == "arriba") & (L > 0)) | ((ib == "abajo") & (L < 0))).astype(int)
    F["ib_contra"] = (((ib == "abajo") & (L > 0)) | ((ib == "arriba") & (L < 0))).astype(int)
    F["ib_ambas"] = (ib == "ambas").astype(int)
    F["ib_en_formacion"] = (ib == "en_formacion").astype(int)
    va = df["ctx_en_va_ayer"].to_numpy()
    F["va_hacia"] = (((va == "abajo") & (L > 0)) | ((va == "arriba") & (L < 0))).astype(int)   # el valor de ayer queda del lado de la ganancia
    F["va_contra"] = (((va == "arriba") & (L > 0)) | ((va == "abajo") & (L < 0))).astype(int)
    # C. VWAP (dist_pts ya firmada: negativa = el VWAP queda del lado de la ganancia)
    F["vwr_dist_X"] = df["vwap_rth_dist_pts"] / X
    F["vwr_dist_sd"] = df["vwap_rth_dist_sd"].to_numpy() * L
    F["ext_vwr_sd"] = df["ext_dist_vwap_rth_sd"].to_numpy() * L
    F["vwj_dist_X"] = df["vwap_jornada_dist_pts"] / X
    F["vwj_dist_sd"] = df["vwap_jornada_dist_sd"].to_numpy() * L
    F["vws_dist_X"] = df["vwap_semanal_dist_pts"] / X
    F["vwm_dist_X"] = df["vwap_mensual_dist_pts"] / X
    F["vwr_sd_X"] = df["vwap_rth_sd"] / X
    # D. niveles y zonas (en el segundo del extremo)
    F["n_niveles_zona"] = df["n_niveles_en_zona"]
    F["n_barridas"] = df["n_barridas_y_vuelta"]
    zf = df["niveles_en_zona"].map(_fams)
    for f in FAM_ZONA:
        F["zona_" + f] = zf.map(lambda s, f=f: int(f in s))
    bn = df["barrida_y_vuelta"].map(_niveles)
    F["barrida_fijo"] = bn.map(lambda s: int(len(s & FIJOS) > 0))
    F["barrida_vwap"] = bn.map(lambda s: int(len(s & DINAM) > 0))
    F["sig_nivel_R"] = (df["S3_objetivo"] - df["entrada"]).to_numpy() * L / Rp      # lugar libre hasta el nivel siguiente (en R, 1..6; 3 si no hay)
    for nom in ("pd_poc", "ps_poc", "pdc"):
        F["d_%s_X" % nom] = df["d_" + nom].to_numpy() * L / X                       # >0: el nivel queda MAS ALLA del extremo (no tocado)
    # E. order flow (ventana de 60 s que termina un segundo antes de la vuelta)
    for k in range(1, 7):
        F["F%d" % k] = df["F%d" % k]
    F["veto_caudal"] = df["veto_caudal"]
    F["puntaje_F"] = df["puntaje_F1_F6"]
    F["abs_pasivo_rel"] = df["of_abs_lado_pasivo_rel"]
    ua = np.where(L > 0, df["of_u_absa60"], df["of_u_absb60"])
    F["abs_contra_rel"] = np.where(L > 0, df["of_absa60"], df["of_absb60"]) / np.where(ua > 0, ua, np.nan)
    F["netbig_favor_rel"] = df["of_netbig60_a_favor"].to_numpy() / escb
    F["big_favor_rel"] = np.where(L > 0, df["of_bigc_60"], df["of_bigv_60"]) / escv
    F["big_contra_rel"] = np.where(L > 0, df["of_bigv_60"], df["of_bigc_60"]) / escv
    F["icebergs_60"] = df["of_icebergs_60"]
    F["icebergs_contra_60"] = df["of_icebergs_contra_60"]
    F["delta60_rel"] = df["of_delta_60"] / escv
    F["delta300_rel"] = df["of_delta_300"] / escv
    F["delta60_ratio"] = df["of_delta_60"] / df["of_vol_60"].where(df["of_vol_60"] > 0)
    F["vol60_rel"] = df["of_vol_60"] / escv
    F["apilados_favor_3v"] = df["of_apilados_favor_3v"]
    F["apilados_contra_3v"] = df["of_apilados_contra_3v"]
    F["imb_favor_3v"] = df["of_imb_favor_3v"]
    F["imb_contra_3v"] = df["of_imb_contra_3v"]
    uv = df["of_u_venta30"].where(df["of_u_venta30"] > 0); uc = df["of_u_compra30"].where(df["of_u_compra30"] > 0)
    F["caudal_contra_rel"] = np.where(L > 0, df["of_venta30"] / uv, df["of_compra30"] / uc)
    F["caudal_favor_rel"] = np.where(L > 0, df["of_compra30"] / uc, df["of_venta30"] / uv)
    F = F.astype(float)
    for c in F.columns:
        assert not any(c.endswith(p) for p in PROHIBIDAS) and c not in FUTURO
    return F


GRUPO = {}
for c in ("a_favor_marea", "marea_agr_favor", "marea_delta_favor", "extension_fuera_ruido", "extension_fuera_ruido_contra", "prof_ret_X",
          "pierna_marea_X", "dur_ret_log", "vel_ret_X", "del_ret_favor", "ret_con_agresores", "R_en_X", "hora"):
    GRUPO[c] = "estructura"
for c in ("ext_sigma_favor", "ruido_a_favor", "ruido_en_contra", "ib_favor", "ib_contra", "ib_ambas", "ib_en_formacion", "va_hacia", "va_contra"):
    GRUPO[c] = "contexto"
for c in ("vwr_dist_X", "vwr_dist_sd", "ext_vwr_sd", "vwj_dist_X", "vwj_dist_sd", "vws_dist_X", "vwm_dist_X", "vwr_sd_X"):
    GRUPO[c] = "vwap"


def grupo(c):
    if c in GRUPO: return GRUPO[c]
    if c.startswith(("zona_", "barrida_", "n_niveles", "n_barridas", "sig_nivel", "d_")): return "niveles"
    return "order_flow"


def etiquetas(df):
    return {"S1": (df["S1_pts"].to_numpy() > 0).astype(int), "S6": (df["S6_pts"].to_numpy() > 0).astype(int)}


# ---------------------------------------------------------------- simulacion: una posicion por vez por rueda
class Base:
    def __init__(s, df):
        s.df = df
        s.dia = df["dia"].to_numpy()
        s.dias = np.array(sorted(df["dia"].unique()))
        s.di = np.searchsorted(s.dias, s.dia)                  # indice de rueda 0..25
        s.t = df["seg_rueda"].to_numpy()
        s.R = df["R"].to_numpy()
        s.pts = {k: df[k + "_pts"].to_numpy() for k in ("S1", "S6")}
        s.sal = {k: df[k + "_seg_salida"].to_numpy() for k in ("S1", "S6")}
        s.nd = len(s.dias)
        s.idx_dia = [np.nonzero(s.di == d)[0] for d in range(s.nd)]   # ya ordenados por seg_rueda


def simular(base, sel, salida, pts=None, sal=None, max_dia=5):
    """sel: mascara booleana de eventos elegidos. Devuelve indices tomados (en orden)."""
    pts = base.pts[salida] if pts is None else pts
    sal = base.sal[salida] if sal is None else sal
    tomadas = []
    for d in range(base.nd):
        ii = base.idx_dia[d]
        ii = ii[sel[ii]]
        libre = -1; n = 0
        for i in ii:
            if n >= max_dia: break
            if base.t[i] <= libre: continue
            tomadas.append(i); n += 1
            libre = sal[i] if sal[i] >= 0 else NR
    return np.array(tomadas, dtype=int)


def metricas(base, tomadas, salida, pts=None):
    pts = base.pts[salida] if pts is None else pts
    nd = base.nd
    out = dict(ruedas=nd, n=int(len(tomadas)))
    if len(tomadas) == 0:
        out.update(ops_por_rueda=0.0, acierto=np.nan, gana_media=np.nan, pierde_media=np.nan, bruto_op=np.nan, bruto_R=np.nan,
                   neto050=np.nan, neto100=np.nan, t050=0.0, t100=0.0, ruedas_pos=0, racha=0, caida=0.0, sharpe050=np.nan, ruedas_activas=0)
        return out
    p = pts[tomadas]; R = base.R[tomadas]; d = base.di[tomadas]
    g = p[p > 0]; l = p[p <= 0]
    out["ops_por_rueda"] = len(tomadas) / nd
    out["acierto"] = 100.0 * (p > 0).mean()
    out["gana_media"] = float(g.mean()) if len(g) else np.nan
    out["pierde_media"] = float(l.mean()) if len(l) else np.nan
    out["bruto_op"] = float(p.mean()); out["bruto_R"] = float((p / R).mean())
    for c, k in ((0.5, "050"), (1.0, "100")):
        net = p - c
        out["neto" + k] = float(net.mean())
        daily = np.bincount(d, weights=net, minlength=nd)
        sd = daily.std(ddof=1)
        out["t" + k] = float(daily.mean() / sd * math.sqrt(nd)) if sd > 0 else 0.0
        if k == "050":
            out["ruedas_pos"] = int((daily > 0).sum())
            out["sharpe050"] = float(daily.mean() / sd) if sd > 0 else np.nan
            eq = np.cumsum(net); out["caida"] = float((np.maximum.accumulate(np.concatenate([[0], eq])) - np.concatenate([[0], eq])).max())
            r = 0; mx = 0
            for x in p:
                r = r + 1 if x <= 0 else 0; mx = max(mx, r)
            out["racha"] = mx
    out["ruedas_activas"] = int(len(np.unique(d)))
    return out


def anotar(variante, salida, parametros, m, familia="meta-etiquetado (modelo chico + regla destilada)", muestra="diseno", placebo_p="", nota=""):
    nuevo = not os.path.exists(REGISTRO)
    fila = dict(fecha_hora_AR=time.strftime("%Y-%m-%d %H:%M"), agente="r19-explorador-modelo", familia=familia, variante=variante,
                salida=salida, parametros=parametros, muestra=muestra, ruedas=m.get("ruedas"), n=m.get("n"),
                ops_por_rueda=_r(m.get("ops_por_rueda"), 2), acierto_pct=_r(m.get("acierto"), 1), gana_media=_r(m.get("gana_media"), 2),
                pierde_media=_r(m.get("pierde_media"), 2), bruto_op=_r(m.get("bruto_op"), 3), neto_op_c050=_r(m.get("neto050"), 3),
                neto_op_c100=_r(m.get("neto100"), 3), t_rueda_c050=_r(m.get("t050"), 2), ruedas_positivas_c050=m.get("ruedas_pos"),
                racha_perdedora=m.get("racha"), caida_max_c050=_r(m.get("caida"), 2), sharpe_diario_c050=_r(m.get("sharpe050"), 4),
                placeboA_p=placebo_p, nota=nota)
    with open(REGISTRO, "a", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=CAMPOS, extrasaction="ignore")
        if nuevo: w.writeheader()
        w.writerow(fila)


def _r(x, k):
    if x is None: return ""
    try:
        if isinstance(x, float) and not np.isfinite(x): return ""
        return round(float(x), k)
    except Exception:
        return x
