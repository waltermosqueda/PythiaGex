# -*- coding: utf-8 -*-
"""RONDA 19, paso 2: el motor de la entrada de calidad (CONTEXTO -> LUGAR -> MOMENTO) y sus salidas, sobre los rasgos de r19_01.
Se usa desde r19_03_correr.py con una configuracion por etapa (ver PREREGISTRO.md). Todo causal: la decision del segundo t usa
solo lo que se sabia al cierre de t; la entrada es al ultimo precio de t; las salidas se buscan en las ordenes POSTERIORES."""
import json, math, os
import numpy as np, pandas as pd
import r19_comun as C

RAS = os.path.join(C.SCR, "rasgos")
TK = C.TICK
NR = 23400
NIVELES = ["vwr", "vwr+1", "vwr-1", "vwr+2", "vwr-2", "vwj", "vwj+1", "vwj-1", "vwj+2", "vwj-2", "vws", "vwm",
           "pdh", "pdl", "pdc", "pd_poc", "pd_vah", "pd_val", "asia_h", "asia_l", "lon_h", "lon_l",
           "ib_h", "ib_l", "ib_h05", "ib_h10", "ib_l05", "ib_l10", "ps_poc", "ps_vah", "ps_val", "r50"]
IB_DESDE = 3600   # 10:30 (segundos desde las 9:30)

E1 = dict(nombre="E1", marea_con=True, lado_vwap="vw_r", requiere_lugar=True, min_niveles=1, min_puntaje=2,
          factores=("F1", "F2", "F3", "F4", "F5", "F6"), veto_caudal=True, fresco_s=60, rebote_min_ticks=2, rebote_max_X=0.5,
          hora_ini=900, hora_fin=21600, max_dia=5, R_min_X=0.5, extra=None)


def cargar(inst, dia):
    z = np.load(os.path.join(RAS, "%s-%s.npz" % (inst, dia)))
    R = {k: z[k] for k in z.files}
    meta = json.load(open(os.path.join(RAS, "%s-%s.json" % (inst, dia))))
    return R, meta


def matriz_niveles(R, meta, delta=0.0):
    """Valor de cada nivel en cada segundo de la rueda (NaN si todavia no existe). delta corre TODO (placebo B)."""
    n = meta["niv"]; X = meta["X"]
    M = np.full((len(NIVELES) - 1, NR), np.nan)
    dyn = {"vwr": R["vw_r"], "vwr+1": R["vw_r"] + R["sd_r"], "vwr-1": R["vw_r"] - R["sd_r"], "vwr+2": R["vw_r"] + 2 * R["sd_r"],
           "vwr-2": R["vw_r"] - 2 * R["sd_r"], "vwj": R["vw_j"], "vwj+1": R["vw_j"] + R["sd_j"], "vwj-1": R["vw_j"] - R["sd_j"],
           "vwj+2": R["vw_j"] + 2 * R["sd_j"], "vwj-2": R["vw_j"] - 2 * R["sd_j"], "vws": R["vw_sem"], "vwm": R["vw_mes"]}
    for i, nom in enumerate(NIVELES[:-1]):
        if nom in dyn:
            M[i] = dyn[nom]
        elif nom in n:
            M[i] = n[nom]
            if nom.startswith("ib_"):
                M[i, :IB_DESDE] = np.nan
    return M + delta


def seg_hora(t):
    t = int(t); return "%02d:%02d:%02d" % (9 + (30 + t // 60) // 60, (30 + t // 60) % 60, t % 60)


class Dia:
    """Todo lo de una rueda que no depende de la regla: rasgos + candidatos por lado + factores (vectorizados)."""
    def __init__(s, inst, dia):
        s.inst, s.dia = inst, dia
        s.R, s.meta = cargar(inst, dia)
        s.X = s.meta["X"]
        R = s.R
        s.c = R["c"]; s.h = R["h"]; s.l = R["l"]
        s.cvol = np.concatenate([[0.0], np.cumsum(R["vol"])])
        s.M = matriz_niveles(R, s.meta)
        # ordenes
        s.or_seg = R["or_seg"]; s.or_hi = R["or_hi"]; s.or_lo = R["or_lo"]; s.or_ult = R["or_ult"]
        s.or_mdir = R["or_mdir"]; s.or_mext = R["or_mext"]
        s.ult_antes_16 = int(np.searchsorted(s.or_seg, NR) - 1)
        # minimo/maximo corredizo del precio de icebergs en 60 s
        ib = np.where(np.isfinite(R["iceb_px"]), R["iceb_px"], np.inf)
        ia = np.where(np.isfinite(R["icea_px"]), R["icea_px"], -np.inf)
        s.iceb_min60 = pd.Series(ib).rolling(60, min_periods=1).min().to_numpy()
        s.icea_max60 = pd.Series(ia).rolling(60, min_periods=1).max().to_numpy()
        s.upiv = R["u_piv"]

    # ------------------------------------------------------------ factores en un segundo t para un lado
    def factores(s, t, lado):
        R = s.R; X = s.X; ext = R["o_ext"][t]; te = int(R["o_ext_seg"][t]); t0 = int(R["o_seg0"][t]); p0 = R["o_p0"][t]
        f = {}
        if lado == 1:
            f["F1"] = bool(R["absb60"][t] >= R["u_absb60"][t]) if np.isfinite(R["u_absb60"][t]) else False
            f["F2"] = bool(s.iceb_min60[t] <= ext + X / 2)
            f["F6"] = bool(R["netbig60"][t] > 0 and np.isfinite(R["u_netbig60_p80"][t]) and R["netbig60"][t] >= R["u_netbig60_p80"][t])
            b = t // 60 - 1
            f["F5"] = bool(b >= 0 and R["n_ap_c"][b] > 0 and R["ap_c_lo"][b] <= ext + X / 2)
            veto = bool(te >= 0 and np.isfinite(R["u_venta30"][te]) and R["venta30"][te] >= R["u_venta30"][te])
        else:
            f["F1"] = bool(R["absa60"][t] >= R["u_absa60"][t]) if np.isfinite(R["u_absa60"][t]) else False
            f["F2"] = bool(s.icea_max60[t] >= ext - X / 2)
            f["F6"] = bool(R["netbig60"][t] < 0 and np.isfinite(R["u_netbig60_p20"][t]) and R["netbig60"][t] <= R["u_netbig60_p20"][t])
            b = t // 60 - 1
            f["F5"] = bool(b >= 0 and R["n_ap_v"][b] > 0 and R["ap_v_hi"][b] >= ext - X / 2)
            veto = bool(te >= 0 and np.isfinite(R["u_compra30"][te]) and R["compra30"][te] >= R["u_compra30"][te])
        # F3 divergencia: micro pivote anterior del MISMO retroceso (confirmado antes de t), mas alto (largo) / mas bajo (corto)
        f["F3"] = False
        U = s.upiv
        if len(U):
            tipo = -1 if lado == 1 else 1
            m = (U[:, 2] == tipo) & (U[:, 0] > t0) & (U[:, 0] < te) & (U[:, 3] <= t)
            m &= (U[:, 1] > ext) if lado == 1 else (U[:, 1] < ext)
            if m.any():
                j = np.nonzero(m)[0][-1]
                cum_ext = R["o_ext_cum"][t]
                f["F3"] = bool(cum_ext > U[j, 4]) if lado == 1 else bool(cum_ext < U[j, 4])
        # F4 agotamiento: volumen por tick del ultimo X/4 del retroceso <= la mitad del promedio del retroceso
        f["F4"] = False
        a = max(t0, 0)
        if te > a and np.isfinite(p0):
            ticks_leg = abs(p0 - ext) / TK
            v_leg = s.cvol[te + 1] - s.cvol[a]
            q = te
            umbral = ext + X / 4 if lado == 1 else ext - X / 4
            while q > a and ((s.h[q] < umbral) if lado == 1 else (s.l[q] > umbral)):
                q -= 1
            v_fin = s.cvol[te + 1] - s.cvol[q]
            if ticks_leg > 0:
                f["F4"] = bool((v_fin / (X / 4 / TK)) <= 0.5 * (v_leg / ticks_leg))
        return f, veto

    def niveles_en_zona(s, t, lado, M=None, delta=0.0):
        M = s.M if M is None else M
        X = s.X; T = max(2 * TK, math.floor(X / 8 / TK + 0.5) * TK)
        ext = s.R["o_ext"][t]; te = int(s.R["o_ext_seg"][t])
        col = M[:, max(te, 0)]
        r50 = 50.0 * round((ext - delta) / 50.0) + delta
        vals = np.append(col, r50)
        if lado == 1:
            ok = (vals - X / 4 <= ext) & (ext <= vals + T)
        else:
            ok = (vals - T <= ext) & (ext <= vals + X / 4)
        ok &= np.isfinite(vals)
        return [NIVELES[i] for i in np.nonzero(ok)[0]]

    def base(s, cfg):
        """Mascara por lado de los segundos que cumplen contexto + retroceso + momento de tiempo (sin lugar ni puntaje)."""
        R = s.R; X = s.X; out = {}
        t = np.arange(NR)
        en_hora = (t >= cfg["hora_ini"]) & (t < cfg["hora_fin"])
        vline = R[cfg["lado_vwap"]]
        mdel = R["cum"] - R["m_c0"]
        for lado in (1, -1):
            m = en_hora & (R["m_dir"] == lado) & (R["o_dir"] == -lado)
            if cfg["marea_con"]:
                m &= (mdel * lado > 0)
            if vline is not None:
                m &= ((s.c - vline) * lado > 0)
            fresco = (t - R["o_ext_seg"]) <= cfg["fresco_s"]
            reb = (s.c - R["o_ext"]) * lado
            m &= fresco & (reb >= cfg["rebote_min_ticks"] * TK) & (reb <= cfg["rebote_max_X"] * X)
            out[lado] = np.nonzero(m)[0]
        return out


def salida(d, i_ent_seg, lado, entrada, stop, regla, objetivo=None, X=None):
    """Busca la salida en las ordenes posteriores al segundo de entrada. regla: 'obj' (objetivo fijo), 'marea' (raya de la marea),
    'perse' (stop que persigue a 4X del mejor precio, version simetrica para el placebo). Devuelve (seg_salida, precio, motivo)."""
    j0 = int(np.searchsorted(d.or_seg, i_ent_seg + 1))
    j1 = d.ult_antes_16
    if j0 > j1:
        return i_ent_seg, entrada, "fin"
    hi = d.or_hi[j0:j1 + 1]; lo = d.or_lo[j0:j1 + 1]
    if lado == 1:
        st = np.nonzero(lo <= stop)[0]
    else:
        st = np.nonzero(hi >= stop)[0]
    j_st = st[0] if len(st) else None
    cand = []
    if j_st is not None:
        cand.append((j_st, stop, "stop"))
    if regla == "obj":
        if lado == 1:
            ob = np.nonzero(hi >= objetivo + TK)[0]
        else:
            ob = np.nonzero(lo <= objetivo - TK)[0]
        if len(ob): cand.append((ob[0], objetivo, "objetivo"))
    elif regla == "marea":
        md = d.or_mdir[j0:j1 + 1]
        fl = np.nonzero(md == -lado)[0]
        if len(fl):
            jj = fl[0]
            prev_ext = d.or_mext[j0 + jj - 1] if j0 + jj - 1 >= 0 else d.or_mext[j0]
            raya = prev_ext - lado * 4 * X
            cand.append((jj, raya, "marea"))
    elif regla == "vwap":
        vw = d.R["vw_r"]; s0 = i_ent_seg + 1
        if s0 < NR:
            if lado == 1: tg = np.nonzero(d.h[s0:] >= vw[s0:] + TK)[0]
            else: tg = np.nonzero(d.l[s0:] <= vw[s0:] - TK)[0]
            if len(tg):
                s_tg = s0 + int(tg[0])
                s_st = int(d.or_seg[j0 + j_st]) if j_st is not None else NR + 1
                if s_tg < s_st:
                    return s_tg, float(vw[s_tg]), "vwap"
    elif regla == "perse":
        if lado == 1:
            mejor = np.maximum.accumulate(hi); mejor = np.concatenate([[entrada], mejor[:-1]]); mejor = np.maximum(mejor, entrada)
            tr = np.maximum(stop, mejor - 4 * X); ps = np.nonzero(lo <= tr)[0]
        else:
            mejor = np.minimum.accumulate(lo); mejor = np.concatenate([[entrada], mejor[:-1]]); mejor = np.minimum(mejor, entrada)
            tr = np.minimum(stop, mejor + 4 * X); ps = np.nonzero(hi >= tr)[0]
        if len(ps): cand = [(ps[0], tr[ps[0]], "persigue")]
    if not cand:
        return int(d.or_seg[j1]), float(d.or_ult[j1]), "fin"
    jj, px, mot = min(cand, key=lambda z: z[0])
    return int(d.or_seg[j0 + jj]), float(px), mot


def siguiente_nivel(d, t, lado, entrada, R_, delta=0.0):
    col = d.M[:, t] if delta == 0.0 else d.M[:, t] + delta
    vals = col[np.isfinite(col)]
    # redondos de 50
    base50 = 50.0 * math.floor((entrada - delta) / 50.0) + delta
    vals = np.concatenate([vals, base50 + 50.0 * np.arange(-3, 5)])
    if lado == 1:
        ok = vals[(vals >= entrada + R_) & (vals <= entrada + 6 * R_)]
        return float(ok.min()) if len(ok) else entrada + 3 * R_
    ok = vals[(vals <= entrada - R_) & (vals >= entrada - 6 * R_)]
    return float(ok.max()) if len(ok) else entrada - 3 * R_


SALIDAS = ("S1", "S2", "S3", "S4")
SALIDAS_R = ("S1", "S2", "S3", "S4", "S5")
SALIDAS_R6 = ("S1", "S2", "S3", "S4", "S5", "S6")


def simular(d, cfg, delta=0.0, M=None, guardar_factores=True):
    """Corre la regla en una rueda. Devuelve lista de operaciones (una lista por salida, porque la posicion abierta cambia
    que entradas siguientes se toman)."""
    if cfg.get("entrada") == "confirmada":
        return simular_confirmada(d, cfg, delta, M, guardar_factores)
    if cfg.get("entrada") == "reversion":
        return simular_reversion(d, cfg, delta, M, guardar_factores)
    R = d.R; X = d.X
    Mx = d.M + delta if M is None else M
    base = d.base(cfg)
    # candidatos ordenados en el tiempo (ambos lados)
    ev = sorted([(int(t), 1) for t in base[1]] + [(int(t), -1) for t in base[-1]])
    cache = {}
    def evaluar(t, lado):
        k = (t, lado)
        if k in cache: return cache[k]
        niv = d.niveles_en_zona(t, lado, Mx, delta)
        if cfg["requiere_lugar"] and len(niv) < cfg["min_niveles"]:
            cache[k] = None; return None
        f, veto = d.factores(t, lado)
        punt = sum(1 for x in cfg["factores"] if f[x])
        if (cfg["veto_caudal"] and veto) or punt < cfg["min_puntaje"]:
            cache[k] = None; return None
        if cfg.get("extra") and not cfg["extra"](d, t, lado, f, niv):
            cache[k] = None; return None
        cache[k] = (niv, f, veto, punt); return cache[k]
    res = {}
    for sal in SALIDAS:
        ops = []; libre_desde = -1; usadas = set(); n = 0
        for t, lado in ev:
            if t <= libre_desde or n >= cfg["max_dia"]: continue
            pierna = int(R["o_np"][t])
            if (pierna, lado) in usadas: continue
            e = evaluar(t, lado)
            if e is None: continue
            niv, f, veto, punt = e
            ext = R["o_ext"][t]; ent = float(d.c[t])
            stop = min(ext - 2 * TK, ent - cfg["R_min_X"] * X) if lado == 1 else max(ext + 2 * TK, ent + cfg["R_min_X"] * X)
            Rp = abs(ent - stop)
            if sal == "S1": ts, px, mot = salida(d, t, lado, ent, stop, "obj", ent + lado * 2 * Rp)
            elif sal == "S2": ts, px, mot = salida(d, t, lado, ent, stop, "obj", ent + lado * 3 * Rp)
            elif sal == "S3":
                obj = siguiente_nivel(d, t, lado, ent, Rp, delta); ts, px, mot = salida(d, t, lado, ent, stop, "obj", obj)
            else: ts, px, mot = salida(d, t, lado, ent, stop, "marea", X=X)
            op = dict(inst=d.inst, dia=d.dia, X=X, regla=cfg["nombre"], salida=sal, lado=lado, t=t, hora=seg_hora(t), entrada=ent,
                      stop=stop, R=Rp, ext=float(ext), t_sal=ts, hora_sal=seg_hora(min(ts, NR - 1)), px_sal=px, motivo=mot,
                      pts=(px - ent) * lado, pierna=pierna, puntaje=punt, niveles="|".join(niv), n_niveles=len(niv))
            if guardar_factores:
                op.update({k: int(v) for k, v in f.items()}); op["veto"] = int(veto)
                op.update(contexto(d, t, lado))
            ops.append(op); usadas.add((pierna, lado)); n += 1; libre_desde = ts
        res[sal] = ops
    return res


def contexto(d, t, lado):
    """Rasgos registrados para la anatomia (no filtran en la etapa 1)."""
    R = d.R; X = d.X; meta = d.meta; n = meta["niv"]
    ext = R["o_ext"][t]; te = int(R["o_ext_seg"][t]); t0 = int(R["o_seg0"][t]); p0 = R["o_p0"][t]
    c = d.c[t]
    m_ext = R["m_ext"][t]
    prof = abs(m_ext - ext) / X if np.isfinite(m_ext) else np.nan     # retroceso desde el extremo de la marea, en X
    a = max(t0, 0)
    ip = int(R["o_np"][t]) - 1
    cum0 = R["o_piv"][ip, 4] if ip >= 0 else R["cum"][0]
    del_ret = float(R["o_ext_cum"][t] - cum0)
    con_agr = int(del_ret * (-lado) > 0)      # el retroceso (contra el lado) lo empujaron agresores de ese lado
    dur = max(1, te - a)
    vel = abs(p0 - ext) / dur if np.isfinite(p0) else np.nan          # pts/s del retroceso
    hi_r = d.h[:t + 1].max(); lo_r = d.l[:t + 1].min()
    if t >= IB_DESDE:
        ext_ib = ("arriba" if hi_r > n["ib_h"] else "") + ("abajo" if lo_r < n["ib_l"] else "")
        ext_ib = {"arriba": "arriba", "abajo": "abajo", "arribaabajo": "ambas", "": "ninguna"}[ext_ib]
    else:
        ext_ib = "en_formacion"
    minuto = min(389, t // 60)
    sig = R["ruido_sigma"][minuto] if len(R["ruido_sigma"]) else np.nan
    o = meta["rth_o"]
    ruido = "fuera_a_favor" if (np.isfinite(sig) and (c - o) * lado > sig) else ("fuera_en_contra" if (np.isfinite(sig) and (c - o) * lado < -sig) else "dentro")
    en_va = "dentro" if n["pd_val"] <= c <= n["pd_vah"] else ("arriba" if c > n["pd_vah"] else "abajo")
    mdel = R["cum"][t] - R["m_c0"][t]
    dist_vw = (c - R["vw_r"][t]) / R["sd_r"][t] if R["sd_r"][t] > 0 else np.nan
    pierna_marea = abs(m_ext - R["m_p0"][t]) / X if np.isfinite(R["m_p0"][t]) else np.nan
    return dict(prof_X=round(float(prof), 2), del_ret=round(del_ret), ret_con_agresores=con_agr, vel_ret=round(float(vel), 3),
                dur_ret_s=int(dur), ext_ib=ext_ib, ruido=ruido, en_va_ayer=en_va, marea_delta=round(float(mdel)),
                dist_vwap_sd=round(float(dist_vw), 2), pierna_marea_X=round(float(pierna_marea), 2), minuto=int(minuto))


def contexto_c3(d, t, lado):
    """C3 (etapa 2): precio fuera del area de ruido de la apertura, del lado de la operacion (Zarattini-Aziz-Barbon 2024)."""
    sig = d.R["ruido_sigma"][min(389, t // 60)] if len(d.R["ruido_sigma"]) else np.nan
    return bool(np.isfinite(sig) and (d.c[t] - d.meta["rth_o"]) * lado > sig)


def simular_confirmada(d, cfg, delta=0.0, M=None, guardar_factores=True):
    """Etapa 2 en adelante: el setup de la etapa 1 ARMA el retroceso; se entra cuando la ola se da vuelta a favor de la marea
    (precio X desde el extremo) si el extremo final es el armado (a <= 2 ticks). Stop = extremo final -/+ 2 ticks."""
    R = d.R; X = d.X
    Mx = d.M + delta if M is None else M
    cfg_setup = dict(cfg, hora_ini=cfg.get("setup_hora_ini", 900), hora_fin=cfg.get("hora_fin", 21600))
    base = d.base(cfg_setup)
    armado = {}      # (pierna, lado) -> lista de (ext, t_setup, niveles, factores, veto, puntaje)
    for lado in (1, -1):
        for t in base[lado]:
            t = int(t)
            niv = d.niveles_en_zona(t, lado, Mx, delta)
            if cfg["requiere_lugar"] and len(niv) < cfg["min_niveles"]: continue
            f, veto = d.factores(t, lado)
            punt = sum(1 for x in cfg["factores"] if f[x])
            if (cfg["veto_caudal"] and veto) or punt < cfg["min_puntaje"]: continue
            if cfg.get("extra_setup") and not cfg["extra_setup"](d, t, lado, f, niv): continue
            armado.setdefault((int(R["o_np"][t]), lado), []).append((float(R["o_ext"][t]), t, niv, f, veto, punt))
    # vueltas de la ola
    onp = R["o_np"]; odir = R["o_dir"]
    giros = np.nonzero(onp[1:] > onp[:-1])[0] + 1
    ev = []
    for t in giros:
        t = int(t); lado = int(odir[t])
        if lado == 0 or not (cfg["hora_ini"] <= t < cfg["hora_fin"]): continue
        key = (int(onp[t - 1]), lado)
        if key not in armado: continue
        ext_f = float(R["o_p0"][t])
        ok = [a for a in armado[key] if (a[0] - ext_f) * lado <= 2 * TK + 1e-9]
        if not ok: continue
        # contexto en la entrada
        if R["m_dir"][t] != lado: continue
        if cfg["marea_con"] and (R["cum"][t] - R["m_c0"][t]) * lado <= 0: continue
        if cfg["lado_vwap"] and (d.c[t] - R[cfg["lado_vwap"]][t]) * lado <= 0: continue
        if cfg.get("c3_ruido", True) and not contexto_c3(d, t, lado): continue
        if cfg.get("extra") and not cfg["extra"](d, t, lado, ok[-1]): continue
        ev.append((t, lado, ext_f, ok[-1]))
    res = {}
    for sal in SALIDAS:
        ops = []; libre_desde = -1; n = 0
        for t, lado, ext_f, setup in ev:
            if t <= libre_desde or n >= cfg["max_dia"]: continue
            ent = float(d.c[t])
            stop = ext_f - 2 * TK if lado == 1 else ext_f + 2 * TK
            Rp = abs(ent - stop)
            if Rp <= 0: continue
            if sal == "S1": ts, px, mot = salida(d, t, lado, ent, stop, "obj", ent + lado * 2 * Rp)
            elif sal == "S2": ts, px, mot = salida(d, t, lado, ent, stop, "obj", ent + lado * 3 * Rp)
            elif sal == "S3":
                obj = siguiente_nivel(d, t, lado, ent, Rp, delta); ts, px, mot = salida(d, t, lado, ent, stop, "obj", obj)
            else: ts, px, mot = salida(d, t, lado, ent, stop, "marea", X=X)
            ext_s, t_s, niv, f, veto, punt = setup
            op = dict(inst=d.inst, dia=d.dia, X=X, regla=cfg["nombre"], salida=sal, lado=lado, t=t, hora=seg_hora(t), entrada=ent,
                      stop=stop, R=Rp, ext=ext_f, t_setup=t_s, hora_setup=seg_hora(t_s), t_sal=ts, hora_sal=seg_hora(min(ts, NR - 1)),
                      px_sal=px, motivo=mot, pts=(px - ent) * lado, pierna=int(onp[t - 1]), puntaje=punt, niveles="|".join(niv),
                      n_niveles=len(niv))
            if guardar_factores:
                op.update({k: int(v) for k, v in f.items()}); op["veto"] = int(veto)
                op.update(contexto(d, t_s, lado))
                op["ruido_entrada"] = int(contexto_c3(d, t, lado))
            ops.append(op); n += 1; libre_desde = ts
        res[sal] = ops
    return res


def extension_marea(d, t, lado):
    """E3: el pivote que inicio la pierna actual de la marea quedo fuera del area de ruido, del lado opuesto a la operacion."""
    R = d.R; P = R["m_piv"]; ip = int(R["m_np"][t]) - 1
    if ip < 0 or ip >= len(P): return False
    seg, px, tipo = P[ip, 0], P[ip, 1], int(P[ip, 2])
    if tipo != -lado or seg < 0: return False
    sig = R["ruido_sigma"][min(389, int(seg) // 60)] if len(R["ruido_sigma"]) else np.nan
    if not np.isfinite(sig): return False
    o = d.meta["rth_o"]
    return bool((o - px) > sig) if lado == 1 else bool((px - o) > sig)


def simular_reversion(d, cfg, delta=0.0, M=None, guardar_factores=True):
    """Etapa 3 "vuelta al valor": la marea vuelve hacia el VWAP despues de una extension fuera del ruido; se entra en cada vuelta
    de la ola a favor de la marea con el precio del lado extendido del VWAP RTH y a >= 1R de el. Stop = extremo del retroceso -/+ 2 ticks."""
    R = d.R; X = d.X
    Mx = d.M + delta if M is None else M
    onp = R["o_np"]; odir = R["o_dir"]; vw = R["vw_r"]
    giros = np.nonzero(onp[1:] > onp[:-1])[0] + 1
    ev = []
    for t in giros:
        t = int(t); lado = int(odir[t])
        if lado == 0 or not (cfg["hora_ini"] <= t < cfg["hora_fin"]): continue
        if R["m_dir"][t] != lado: continue
        if cfg["marea_con"] and (R["cum"][t] - R["m_c0"][t]) * lado <= 0: continue
        if cfg.get("extension", True) and not extension_marea(d, t, lado): continue
        ext_f = float(R["o_p0"][t]); ent = float(d.c[t])
        stop = ext_f - 2 * TK if lado == 1 else ext_f + 2 * TK
        Rp = abs(ent - stop)
        dist = (vw[t] - ent) * lado          # distancia al VWAP del lado del objetivo
        if Rp <= 0 or dist < cfg.get("dist_min_R", 1.0) * Rp: continue
        if cfg.get("flujo_min") is not None:
            f, veto = d.factores(t - 1, lado)
            if sum(1 for x in cfg.get("flujo_factores", ("F1", "F6")) if f[x]) < cfg["flujo_min"]: continue
        if cfg.get("extra") and not cfg["extra"](d, t, lado): continue
        ev.append((t, lado, ext_f, ent, stop, Rp, dist))
    res = {}
    for sal in cfg.get("salidas", SALIDAS_R):
        ops = []; libre_desde = -1; n = 0
        for t, lado, ext_f, ent, stop, Rp, dist in ev:
            if t <= libre_desde or n >= cfg["max_dia"]: continue
            if sal == "S1": ts, px, mot = salida(d, t, lado, ent, stop, "obj", ent + lado * 2 * Rp)
            elif sal == "S2": ts, px, mot = salida(d, t, lado, ent, stop, "obj", ent + lado * 3 * Rp)
            elif sal == "S3":
                obj = siguiente_nivel(d, t, lado, ent, Rp, delta); ts, px, mot = salida(d, t, lado, ent, stop, "obj", obj)
            elif sal == "S4": ts, px, mot = salida(d, t, lado, ent, stop, "marea", X=X)
            elif sal == "S6": ts, px, mot = salida(d, t, lado, ent, stop, "obj", ent + lado * 1 * Rp)
            else: ts, px, mot = salida(d, t, lado, ent, stop, "vwap")
            op = dict(inst=d.inst, dia=d.dia, X=X, regla=cfg["nombre"], salida=sal, lado=lado, t=t, hora=seg_hora(t), entrada=ent,
                      stop=stop, R=Rp, ext=ext_f, dist_vwap=round(float(dist), 3), t_sal=ts, hora_sal=seg_hora(min(ts, NR - 1)), px_sal=px,
                      motivo=mot, pts=(px - ent) * lado, pierna=int(onp[t - 1]))
            if guardar_factores:
                # rasgos del retroceso que termino (en t-1 la ola todavia iba en contra): niveles en su extremo y order flow
                niv = d.niveles_en_zona(t - 1, lado, Mx, delta)
                f, veto = d.factores(t - 1, lado)
                op.update(dict(puntaje=int(sum(f.values())), niveles="|".join(niv), n_niveles=len(niv)))
                op.update({k: int(v) for k, v in f.items()}); op["veto"] = int(veto)
                op.update(contexto(d, t - 1, lado))
            ops.append(op); n += 1; libre_desde = ts
        res[sal] = ops
    return res
