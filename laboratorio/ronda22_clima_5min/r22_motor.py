# -*- coding: utf-8 -*-
"""RONDA 22 - MOTOR de 5 min: la traduccion a velas de la R21 (D1, VWAP, X, ola/marea, delta, niveles y barrida de la T7, banda 2 sigma
de la V4, salidas S6 y P1) y la historia de varios dias (mediana de 10, ayer, semana anterior, VWAP semanal y mensual, cambio de contrato).
Todo causal: una senal usa solo velas ya cerradas (mas el camino dentro de la vela en curso hasta el punto de la entrada).
Convenciones (fijadas en la calibracion del paso 1, ver PREREGISTRO.md) en el dict CONV.

Orden dentro de la vela: cada vela es un camino de 4 puntos [apertura, extremo 1, extremo 2, cierre]; la regla elige que extremo va primero.
Entrada 'stop' (la fiel a la R21): orden de compra/venta a stop en extremo del retroceso +-X (si la vela abre ya pasada, se llena en la
apertura). Entrada 'cierre': al cierre de la vela donde se dio vuelta la ola. Regla conservadora: si el stop y el objetivo caen en la misma
vela (o en el tramo posterior a la entrada dentro de la vela de entrada), cuenta como stop; un stop salteado por un hueco se llena en la
apertura; el objetivo se cuenta lleno al pasarlo 1 tick y se cobra al precio del objetivo."""
import math, os
import numpy as np, pandas as pd
import r22_comun as K

TK = K.TK
NIVELES = ["vwr", "vwr+1", "vwr-1", "vwr+2", "vwr-2", "vwj", "vwj+1", "vwj-1", "vwj+2", "vwj-2", "vws", "vwm",
           "pdh", "pdl", "pdc", "pd_poc", "pd_vah", "pd_val", "asia_h", "asia_l", "lon_h", "lon_l",
           "ib_h", "ib_l", "ib_h05", "ib_h10", "ib_l05", "ib_l10", "ps_poc", "ps_vah", "ps_val"]       # + r50 (aparte)
I_IB = [NIVELES.index(x) for x in ("ib_h", "ib_l", "ib_h05", "ib_h10", "ib_l05", "ib_l10")]
BANDAS = {1: ["vwr-2", "vwj-2"], -1: ["vwr+2", "vwj+2"]}
CORTE_T, CORTE_E = 1.6, 1.0
MAX_DIA = 5
# Convenciones por defecto (se reemplazan por las elegidas en la calibracion; ver PREREGISTRO)
CONV = dict(precio="hlc3", var_intra=True, camino="color", l2_vw="inicio", l2_empate="camino")
# Cambio de contrato del continuo MES@CME (lunes de la semana del vencimiento): spread nuevo - viejo (ver PREREGISTRO, inventario)
ROLLS = {"2025-09-15": 54.00, "2025-12-15": 57.25, "2026-03-16": 52.25, "2026-06-15": 62.50, "2026-09-14": 67.00}


# ================================================================ lectura (solo lectura)
def leer_velas(ruta, desde, hasta):
    v = pd.read_csv(ruta, usecols=["t", "o", "h", "l", "c", "vol", "delta"])
    ny = pd.to_datetime(v["t"]).dt.tz_localize("UTC").dt.tz_convert(K.NY).dt.tz_localize(None)
    v["rueda"] = (ny + pd.Timedelta(hours=6)).dt.strftime("%Y-%m-%d")
    v = v[(v["rueda"] >= desde) & (v["rueda"] <= hasta)].copy()
    ny = ny[v.index]
    v["hm"] = ((ny - pd.to_datetime(v["rueda"])).dt.total_seconds() // 60).astype(int)
    return v.sort_values(["rueda", "hm"]).reset_index(drop=True)


def sesiones(v):
    out = {}
    for r, g in v.groupby("rueda", sort=True):
        out[r] = {k: g[k].to_numpy(float if k != "hm" else int) for k in ("hm", "o", "h", "l", "c", "vol", "delta")}
    return out


def clase(ses, minutos):
    """'completa' (todas las velas 9:30-16:00), 'corta' (feriado o media rueda: sin velas despues de las 13:15) o 'incompleta' (hueco de datos)."""
    hm = ses["hm"]; r = hm[(hm >= 570) & (hm < 960)]
    n = 390 // minutos
    if len(r) == n and (np.sort(r) == 570 + minutos * np.arange(n)).all(): return "completa"
    if len(r) == 0 or r.max() <= 13 * 60 + 15: return "corta"
    return "incompleta"


# ================================================================ piezas
def camino(o, h, l, c, regla):
    """(n, 4): apertura, primer extremo, segundo extremo, cierre."""
    if regla == "color":
        bajo_primero = c >= o
    elif regla == "cercano":
        dl = o - l; dh = h - o
        bajo_primero = np.where(dl != dh, dl < dh, c >= o)
    else:
        raise ValueError(regla)
    P = np.empty((len(o), 4))
    P[:, 0] = o; P[:, 1] = np.where(bajo_primero, l, h); P[:, 2] = np.where(bajo_primero, h, l); P[:, 3] = c
    return P


def precio_vela(o, h, l, c, cual):
    return {"hlc3": (h + l + c) / 3.0, "ohlc4": (o + h + l + c) / 4.0, "hl2": (h + l) / 2.0, "c": c}[cual]


def vw_sd(cpv, cp2v, cv):
    with np.errstate(invalid="ignore", divide="ignore"):
        w = cpv / cv; sd = np.sqrt(np.maximum(cp2v / cv - w * w, 0.0))
    return w, sd


def maquina_pts(p, X, con_ext=False):
    """Zigzag de X punto por punto (misma semantica que r19_01_rasgos.maquina con mn = mx = punto).
    Devuelve D (direccion tras cada punto), NP (pivotes confirmados hasta el punto) y piv = [(idx_ext, precio_ext, tipo, idx_conf)];
    con con_ext=True ademas el extremo en curso de la pierna (E) y su indice (EI) tras cada punto."""
    n = len(p); pl = p.tolist()
    D = [0] * n; NP = [0] * n; piv = []; E = [np.nan] * n; EI = [-1] * n
    dr = 0; ext = None; ei = -1; lo = hi = None; lo_i = hi_i = -1; npv = 0
    for i in range(n):
        a = pl[i]
        if dr == 0:
            if lo is None or a < lo: lo, lo_i = a, i
            if hi is None or a > hi: hi, hi_i = a, i
            if hi - lo >= X:
                if hi_i > lo_i:
                    piv.append((lo_i, lo, -1, i)); dr = 1; ext, ei = hi, hi_i
                else:
                    piv.append((hi_i, hi, 1, i)); dr = -1; ext, ei = lo, lo_i
                npv += 1
        elif dr == 1:
            if a > ext: ext, ei = a, i
            elif ext - a >= X:
                piv.append((ei, ext, 1, i)); dr = -1; ext, ei = a, i; npv += 1
        else:
            if a < ext: ext, ei = a, i
            elif a - ext >= X:
                piv.append((ei, ext, -1, i)); dr = 1; ext, ei = a, i; npv += 1
        D[i] = dr; NP[i] = npv
        if ext is not None: E[i] = ext; EI[i] = ei
    if con_ext:
        return np.array(D, np.int8), np.array(NP, np.int32), piv, np.array(E, float), np.array(EI, np.int64)
    return np.array(D, np.int8), np.array(NP, np.int32), piv


def perfil_velas(h, l, vol, corr_ticks=0):
    """Volumen de cada vela repartido parejo en su rango (como r19_01.preparar_historia_m5)."""
    if len(h) == 0: return None
    lo = np.rint(l / TK).astype(np.int64) + corr_ticks; hi = np.rint(h / TK).astype(np.int64) + corr_ticks
    n = hi - lo + 1; rep = np.repeat(np.arange(len(h)), n); off = np.arange(len(rep)) - np.repeat(np.cumsum(n) - n, n)
    tick = lo[rep] + off; v = (vol / n)[rep]
    t0 = int(tick.min()); arr = np.zeros(int(tick.max()) - t0 + 1); np.add.at(arr, tick - t0, v)
    return {"t0": t0, "arr": arr}


def sumar_perfiles(ps):
    ps = [p for p in ps if p is not None]
    if not ps: return None
    t0 = min(p["t0"] for p in ps); t1 = max(p["t0"] + len(p["arr"]) for p in ps)
    arr = np.zeros(t1 - t0)
    for p in ps: arr[p["t0"] - t0:p["t0"] - t0 + len(p["arr"])] += p["arr"]
    return {"t0": t0, "arr": arr}


def area_valor(vol, pct=70, paso=2):
    """Copia exacta de r19_01_rasgos.area_valor (regla de ATAS verificada 1.186/1.186)."""
    con = np.nonzero(vol > 0)[0]
    if len(con) == 0:
        return -1, -1, -1
    bajo, alto = con[0], con[-1]
    vmax = vol.max(); poc = int(np.nonzero(vol == vmax)[0][-1])
    obj = vol[bajo:alto + 1].sum() * pct / 100.0
    acum = vol[poc]; hi = lo = poc
    while acum <= obj and not (hi >= alto and lo <= bajo):
        up = dn = 0.0; hiN, loN = hi, lo
        for _ in range(paso):
            if hiN + 1 <= alto: hiN += 1; up += vol[hiN]
        for _ in range(paso):
            if loN - 1 >= bajo: loN -= 1; dn += vol[loN]
        if up == 0 and dn == 0: hi, lo = hiN, loN
        elif up >= dn: hi = hiN; acum += up
        else: lo = loN; acum += dn
        if acum >= obj: break
    return poc, hi, lo


def va_de(p):
    poc, vah, val = area_valor(p["arr"])
    return {"poc": (p["t0"] + poc) * TK, "vah": (p["t0"] + vah) * TK, "val": (p["t0"] + val) * TK}


def estado(rrn, c, vw, O):
    """Copia de regla_congelada_comb_t7_v4.estado_minuto: 'T' TENDENCIA (rrn >= 1,6 con precio del mismo lado del VWAP RTH y de la
    apertura), 'R' ROTACION (1,0 < rrn < 1,6), 'E' ESTRECHO (rrn <= 1,0), 'N' no se opera (rrn >= 1,6 con lados en desacuerdo)."""
    rrn = np.asarray(rrn, float); lvw = np.sign(np.asarray(c, float) - np.asarray(vw, float)); lo = np.sign(np.asarray(c, float) - O)
    tipo = np.full(len(rrn), "N", dtype=object); ok = np.isfinite(rrn)
    tipo[ok & (rrn >= CORTE_T) & (lvw == lo) & (lvw != 0)] = "T"
    tipo[ok & (rrn > CORTE_E) & (rrn < CORTE_T)] = "R"
    tipo[ok & (rrn <= CORTE_E)] = "E"
    return tipo, lvw


def X_de(med):
    return float(math.floor(0.08 * med / TK + 0.5) * TK)


# ================================================================ historia (varios dias)
def roll_hasta(d0, d1):
    """Suma de spreads de los cambios de contrato con d0 < fecha <= d1 (lleva precios de la sesion d0 al contrato de d1)."""
    return sum(s for f, s in ROLLS.items() if d0 < f <= d1)


def resumen_dia(r, ses, conv, minutos):
    hm = ses["hm"]; o, h, l, c, vol = (ses[k] for k in ("o", "h", "l", "c", "vol"))
    rt = (hm >= 570) & (hm < 960)
    p = precio_vela(o, h, l, c, conv["precio"]); vi = (h - l) ** 2 / 12.0 if conv["var_intra"] else 0.0
    d = dict(rueda=r, clase=clase(ses, minutos), n_rth=int(rt.sum()),
             eth_pv=float((p * vol).sum()), eth_p2v=float(((p * p + vi) * vol).sum()), eth_v=float(vol.sum()),
             perfil_eth=perfil_velas(h, l, vol))
    if rt.any():
        d.update(rth_h=float(h[rt].max()), rth_l=float(l[rt].min()), rth_c=float(c[rt][-1]), rth_o=float(o[rt][0]),
                 perfil_rth=perfil_velas(h[rt], l[rt], vol[rt]))
        d["rango"] = d["rth_h"] - d["rth_l"]
    return d


class Historia:
    """Resumenes por rueda (en orden). 'corta' no entra en la historia (regla de la R21 en vivo); 'incompleta' (hueco de datos) entra
    como historia (R19: rueda con >= 70 velas de 5 min) pero no se evalua."""
    def __init__(s, ses_dict, conv, minutos=5):
        s.minutos = minutos; s.res = {}
        for r in sorted(ses_dict):
            s.res[r] = resumen_dia(r, ses_dict[r], conv, minutos)
        s.dias = sorted(s.res)
        s.hist = [r for r in s.dias if s.res[r]["clase"] == "completa" or
                  (s.res[r]["clase"] == "incompleta" and s.res[r]["n_rth"] >= 70 * 5 // minutos)]
        s.completas = [r for r in s.dias if s.res[r]["clase"] == "completa"]

    def previas(s, dia, n):
        p = [r for r in s.hist if r < dia]
        return p[-n:]

    def ctx(s, dia):
        prev10 = s.previas(dia, 10)
        if len(prev10) < 10: return None
        med = float(np.median([s.res[r]["rango"] for r in prev10]))
        prev60 = s.previas(dia, 60)
        med60 = float(np.median([s.res[r]["rango"] for r in prev60])) if len(prev60) >= 20 else np.nan
        ay = prev10[-1]; a = roll_hasta(ay, dia); ra = s.res[ay]
        va = va_de(ra["perfil_rth"])
        niv = {"pdh": ra["rth_h"] + a, "pdl": ra["rth_l"] + a, "pdc": ra["rth_c"] + a,
               "pd_poc": va["poc"] + a, "pd_vah": va["vah"] + a, "pd_val": va["val"] + a}
        # semana anterior (ISO), perfil de todas las sesiones de esa semana que estan en la historia
        lun = pd.Timestamp(dia) - pd.Timedelta(days=pd.Timestamp(dia).weekday())
        sem_prev = [r for r in s.hist if (lun - pd.Timedelta(days=7)) <= pd.Timestamp(r) < lun]
        ps = []
        for r in sem_prev:
            pf = s.res[r]["perfil_eth"]
            if pf is None: continue
            ct = int(round(roll_hasta(r, dia) / TK)); ps.append({"t0": pf["t0"] + ct, "arr": pf["arr"]})
        sp = sumar_perfiles(ps)
        if sp is not None:
            vv = va_de(sp); niv.update({"ps_poc": vv["poc"], "ps_vah": vv["vah"], "ps_val": vv["val"]})
        else:
            niv.update({"ps_poc": np.nan, "ps_vah": np.nan, "ps_val": np.nan})
        def carry(lista):
            pv = p2v = v = 0.0
            for r in lista:
                rr = s.res[r]; a_ = roll_hasta(r, dia)
                pv += rr["eth_pv"] + a_ * rr["eth_v"]; p2v += rr["eth_p2v"] + 2 * a_ * rr["eth_pv"] + a_ * a_ * rr["eth_v"]; v += rr["eth_v"]
            return {"pv": pv, "p2v": p2v, "v": v}
        sem_act = [r for r in s.hist if lun <= pd.Timestamp(r) < pd.Timestamp(dia)]
        mes_act = [r for r in s.hist if r < dia and r[:7] == dia[:7]]
        return dict(dia=dia, med10=med, med60=med60, X=X_de(med), niv=niv, sem=carry(sem_act), mes=carry(mes_act),
                    ayer=ay, prev10=prev10, sem_prev=sem_prev, sem_act=sem_act, mes_act=mes_act)


# ================================================================ una rueda
def _zona_T(X):
    return max(2 * TK, math.floor(X / 8 / TK + 0.5) * TK)


def _niveles_col(Mk, ext, lado, X, ib_ok):
    """Mk: valores de los 31 niveles en la vela de evaluacion. Devuelve (n en zona, n barridos hasta X/2 (sin chequear la vuelta), valores)."""
    vals = Mk.copy()
    if not ib_ok: vals[I_IB] = np.nan
    vals = np.append(vals, 50.0 * round(ext / 50.0))
    T = _zona_T(X)
    fin = np.isfinite(vals)
    if lado == 1: zona = (vals - X / 4 <= ext) & (ext <= vals + T)
    else: zona = (vals - T <= ext) & (ext <= vals + X / 4)
    return zona & fin, vals


def simular(lado, E, stop0, R, seg0, O_, H_, L_, C_, tipo):
    """seg0: tramo de la vela de entrada POSTERIOR a la entrada: array de puntos del camino (entrada 'stop' con camino), o una tupla
    ('vela', o, h, l, c) = la vela de entrada entera sin orden conocido (entrada 'stopc': si la vela toca el stop, cuenta como stop),
    o None (entrada al cierre). O_, H_, L_, C_: velas siguientes hasta las 16:00 (la ultima cierra todo).
    tipo 'S6' (1R) o 'P1' (mitad 1R + resto 2R con stop en la entrada).
    Devuelve (pts, motivo, velas_hasta_salida (0 = la de entrada), mfe_R, mae_R)."""
    s = lado; T1 = E + s * R; T2 = E + s * 2 * R
    mejor = 0.0; peor = 0.0
    def segs():
        if isinstance(seg0, tuple):
            yield ("vela0", seg0[1:], 0)
        elif seg0 is not None:
            q = np.asarray(seg0, float); yield ("cam", q, None)
        for j in range(len(H_)):
            yield ("vela", (O_[j], H_[j], L_[j], C_[j]), j + 1)
    estadoP = "A"; parcial = 0.0
    ult_c = E; nvel = 0
    for cl, dat, jj in segs():
        if cl == "cam":
            q = dat; hi = q.max(); lo = q.min(); op = None; nvel = 0
        elif cl == "vela0":
            _, hi, lo, cc = dat; op = None; nvel = 0; ult_c = cc      # sin hueco: la entrada fue despues de la apertura
        else:
            op, hi, lo, cc = dat; nvel = jj; ult_c = cc
        fav = (hi - E) if s == 1 else (E - lo); con = (E - lo) if s == 1 else (hi - E)
        if estadoP == "A":
            stop_hit = (lo <= stop0) if s == 1 else (hi >= stop0)
            gap = (op is not None) and ((op <= stop0) if s == 1 else (op >= stop0))
            t1_hit = (hi >= T1 + TK) if s == 1 else (lo <= T1 - TK)
            if stop_hit:
                peor = max(peor, R); mejor = max(mejor, min(fav, R) if t1_hit else fav)
                px = op if gap else stop0
                return (px - E) * s, ("stop_hueco" if gap else "stop"), nvel, mejor / R, peor / R
            mejor = max(mejor, fav); peor = max(peor, con)
            if t1_hit:
                if tipo == "S6":
                    return R, "objetivo", nvel, mejor / R, peor / R
                parcial = 0.5 * R; estadoP = "B"
                # el resto dentro del mismo tramo: conservador
                if cl == "cam":
                    idx = np.nonzero(((q >= T1 + TK) if s == 1 else (q <= T1 - TK)))[0][0]
                    sub = q[idx:]; shi, slo = sub.max(), sub.min()
                else:
                    shi, slo = hi, lo
                be = (slo <= E) if s == 1 else (shi >= E)
                t2 = (shi >= T2 + TK) if s == 1 else (slo <= T2 - TK)
                if be:
                    return parcial, "1R+entrada", nvel, mejor / R, peor / R
                if t2:
                    return parcial + R, "1R+2R", nvel, mejor / R, peor / R
                continue
        else:   # resto abierto (stop en la entrada, objetivo 2R)
            gap = (op is not None) and ((op <= E) if s == 1 else (op >= E))
            be = (lo <= E) if s == 1 else (hi >= E)
            t2 = (hi >= T2 + TK) if s == 1 else (lo <= T2 - TK)
            mejor = max(mejor, fav)
            if be:
                px = op if gap else E
                return parcial + 0.5 * (px - E) * s, "1R+entrada", nvel, mejor / R, peor / R
            if t2:
                return parcial + R, "1R+2R", nvel, mejor / R, peor / R
    # fin de la rueda: cierra al ultimo precio
    if estadoP == "A":
        return (ult_c - E) * s, "fin", nvel, mejor / R, peor / R
    return parcial + 0.5 * (ult_c - E) * s, "1R+fin", nvel, mejor / R, peor / R


def jornada(ses, ctx, f, conv, minutos=5, eventos=True, din_externos=None):
    """Una rueda completa. ctx: med10, X, niv (estaticos de la historia), sem y mes (acumulados para el VWAP semanal/mensual).
    din_externos (solo calibracion): dict con arrays por vela de la rueda para 'vws' y 'vwm' (si no, se calculan con los acumulados).
    Devuelve dict con: velas de la rueda, estado por vela, etiquetas de los puntos de control y eventos (con sus resultados)."""
    hm = ses["hm"]; o, h, l, c, vol, dl = (ses[k] for k in ("o", "h", "l", "c", "vol", "delta"))
    rth = np.nonzero((hm >= 570) & (hm < 960))[0]; nr = len(rth); r0 = int(rth[0])
    assert nr == 390 // minutos
    X = ctx["X"]; med = ctx["med10"]
    P = camino(o, h, l, c, conv["camino"]); pts = P.ravel()
    p = precio_vela(o, h, l, c, conv["precio"]); vi = (h - l) ** 2 / 12.0 if conv["var_intra"] else 0.0
    cpv = np.cumsum(p * vol); cp2v = np.cumsum((p * p + vi) * vol); cv = np.cumsum(vol)
    vwj, sdj = vw_sd(cpv[rth], cp2v[rth], cv[rth])
    rpv = np.cumsum((p * vol)[rth]); rp2v = np.cumsum(((p * p + vi) * vol)[rth]); rv = np.cumsum(vol[rth])
    vwr, sdr = vw_sd(rpv, rp2v, rv)
    if din_externos is not None:
        vws = din_externos["vws"]; vwm = din_externos["vwm"]
    else:
        vws, _ = vw_sd(ctx["sem"]["pv"] + cpv[rth], ctx["sem"]["p2v"] + cp2v[rth], ctx["sem"]["v"] + cv[rth])
        vwm, _ = vw_sd(ctx["mes"]["pv"] + cpv[rth], ctx["mes"]["p2v"] + cp2v[rth], ctx["mes"]["v"] + cv[rth])
    hr, lr, cr, orr = h[rth], l[rth], c[rth], o[rth]
    Hk = np.maximum.accumulate(hr); Lk = np.minimum.accumulate(lr)
    mins = (np.arange(nr) + 1) * minutos - 1
    rrn = (Hk - Lk) / (med * f[mins])
    O = float(orr[0])
    tipo, lvw = estado(rrn, cr, vwr, O)
    out = dict(nr=nr, r0=r0, X=X, med10=med, O=O, rrn=rrn, tipo=tipo, lvw=lvw, vwr=vwr, sdr=sdr, vwj=vwj, sdj=sdj, vws=vws, vwm=vwm,
               o=orr, h=hr, l=lr, c=cr, vol=vol[rth], delta=dl[rth], Hk=Hk, Lk=Lk)
    # ---------------- puntos de control (cierres 10:00 .. 15:30): L2 (primer paso) e y1 (hasta el cierre)
    k_ini = 30 // minutos - 1; k_fin = 360 // minutos - 1
    PR = P[rth]
    filas = []
    for k in range(k_ini, k_fin + 1):
        s_ = int(np.sign(cr[k] - vwr[k]))
        lab = "ninguno"
        if s_ != 0:
            meta_ext = Hk[k] + X / 2 if s_ > 0 else Lk[k] - X / 2
            for j in range(k + 1, nr):
                vref = vwr[j - 1] if conv["l2_vw"] == "inicio" else vwr[j]
                q = PR[j]
                toca = (q <= vref) if s_ > 0 else (q >= vref)
                ext_ = (q >= meta_ext) if s_ > 0 else (q <= meta_ext)
                if toca.any() or ext_.any():
                    if toca.any() and ext_.any():
                        if conv["l2_empate"] == "camino":
                            lab = "vuelve" if np.argmax(toca) < np.argmax(ext_) else "extiende"
                        else:
                            lab = "empate"
                    else:
                        lab = "vuelve" if toca.any() else "extiende"
                    break
        y1 = (cr[-1] - cr[k]) * s_ / X if s_ != 0 else np.nan
        dist_ext = ((Hk[k] - cr[k]) if s_ > 0 else (cr[k] - Lk[k])) / X if s_ != 0 else np.nan
        filas.append(dict(k=k, hora="%02d:%02d" % divmod(570 + (k + 1) * minutos, 60), tipo=tipo[k], lado=s_, rrn=rrn[k], L2=lab, y1=y1,
                          dist_ext_X=dist_ext, dist_vw_X=abs(cr[k] - vwr[k]) / X, c=cr[k], vw=vwr[k]))
    out["control"] = pd.DataFrame(filas)
    if not eventos:
        return out
    # ---------------- ola (X) y marea (4X) sobre el camino de toda la jornada (desde las 18:00)
    oD, oNP, opiv, oE, oEI = maquina_pts(pts, X, con_ext=True)
    mD, mNP, mpiv = maquina_pts(pts, 4 * X)
    out["n_pivotes_ola"] = len(opiv)
    # niveles: matriz 31 x velas de la rueda
    n_ = ctx["niv"]
    asia = (hm < 180); lon = (hm >= 180) & (hm < 570)
    est = dict(n_)
    est.update(asia_h=float(h[asia].max()) if asia.any() else np.nan, asia_l=float(l[asia].min()) if asia.any() else np.nan,
               lon_h=float(h[lon].max()) if lon.any() else np.nan, lon_l=float(l[lon].min()) if lon.any() else np.nan)
    nib = 60 // minutos
    ibh = float(hr[:nib].max()); ibl = float(lr[:nib].min()); ibr = ibh - ibl
    est.update(ib_h=ibh, ib_l=ibl, ib_h05=ibh + 0.5 * ibr, ib_h10=ibh + ibr, ib_l05=ibl - 0.5 * ibr, ib_l10=ibl - ibr)
    din = {"vwr": vwr, "vwr+1": vwr + sdr, "vwr-1": vwr - sdr, "vwr+2": vwr + 2 * sdr, "vwr-2": vwr - 2 * sdr,
           "vwj": vwj, "vwj+1": vwj + sdj, "vwj-1": vwj - sdj, "vwj+2": vwj + 2 * sdj, "vwj-2": vwj - 2 * sdj, "vws": vws, "vwm": vwm}
    M = np.full((len(NIVELES), nr), np.nan)
    for i, nom in enumerate(NIVELES):
        M[i] = din[nom] if nom in din else est.get(nom, np.nan)
    out["niveles_estaticos"] = est
    ib_desde = nib            # el extremo tiene que estar en una vela que empieza a las 10:30 o despues
    evs = []
    k_ev = (30 // minutos, 360 // minutos - 1)        # vela de la entrada a stop: empieza entre 10:00 y 15:25
    # ---- entradas a STOP (fieles a la R21)
    for (ie, pxe, tp, ic) in opiv:
        sb = ic // 4; k = sb - r0; pos = ic % 4
        if not (k_ev[0] <= k <= k_ev[1]): continue
        lado = -tp; ext = float(pxe); th = ext + lado * X
        ent = float(pts[ic]) if pos == 0 else th
        stop = ext - lado * 2 * TK; R = (ent - stop) * lado
        if R <= 0: continue
        kp = k - 1
        j_ext = ie // 4 - r0
        js = max(min(j_ext, kp), 0)
        zona, vals = _niveles_col(M[:, js], ext, lado, X, j_ext >= ib_desde)
        pen = (vals - ext) if lado == 1 else (ext - vals)
        barr = np.isfinite(vals) & (pen > 0) & (pen <= X / 2) & ((ent - vals) * lado > 0)
        banda = False
        for nom in BANDAS[lado]:
            L_ = M[NIVELES.index(nom), js]
            if np.isfinite(L_):
                pb = (L_ - ext) * lado
                banda |= bool((pb >= (-0.5 + 1e-6) * X - 1e-9) and (pb <= 1e-9))
        seg0 = np.concatenate([[ent], pts[ic:4 * sb + 4]])
        res = {}
        for tsal in ("S6", "P1"):
            res[tsal] = simular(lado, ent, stop, R, seg0, orr[k + 1:], hr[k + 1:], lr[k + 1:], cr[k + 1:], tsal)
            res[tsal + "o"] = simular(-lado, ent, ent + lado * R, R, seg0, orr[k + 1:], hr[k + 1:], lr[k + 1:], cr[k + 1:], tsal)
        evs.append(dict(entrada_tipo="stop", k=k, pos=pos, ic=ic, hora="%02d:%02d" % divmod(570 + k * minutos, 60), lado=lado, ext=ext,
                        j_ext=j_ext, entrada=ent, stop=stop, R=R, X=X, estado=tipo[kp], lado_vw=int(lvw[kp]), rrn=float(rrn[kp]),
                        marea=int(mD[ic]), a_favor_marea=int(mD[ic] == lado), vw_dist=float((ent - vwr[kp]) * lado),
                        delta_fav=float(dl[r0 + kp] * lado), n_zona=int(zona.sum()), n_barr=int(barr.sum()), banda=int(banda),
                        niveles="|".join(([NIVELES[i] for i in np.nonzero(zona[:-1])[0]] + (["r50"] if zona[-1] else []))),
                        barridos="|".join(([NIVELES[i] for i in np.nonzero(barr[:-1])[0]] + (["r50"] if barr[-1] else []))),
                        **{"%s_%s" % (a, b): v for a, r in res.items() for b, v in zip(("pts", "motivo", "velas", "mfe", "mae"), r)}))
    # ---- entradas a STOP CONSERVADORAS ('stopc'): al cierre de la vela k-1 la ola va en contra (retroceso) con su extremo e;
    #      la orden pendiente para la vela k es e +- X. Se llena si la vela k la toca (en la apertura si abre pasada). Stop = e -+ 2 ticks.
    #      Si la vela k tambien toca el stop, cuenta como stop (no se sabe el orden). Todo lo demas, con lo sabido al cierre de k-1.
    for k in range(k_ev[0], k_ev[1] + 1):
        kp = k - 1; sbp = r0 + kp; sb = r0 + k; lp = 4 * sbp + 3
        d_ = int(oD[lp])
        if d_ == 0: continue
        lado = -d_; ext = float(oE[lp]); ie = int(oEI[lp])
        th = ext + lado * X
        if lado == 1:
            if h[sb] < th: continue
            ent = float(o[sb]) if o[sb] >= th else th
        else:
            if l[sb] > th: continue
            ent = float(o[sb]) if o[sb] <= th else th
        stop = ext - lado * 2 * TK; R = (ent - stop) * lado
        if R <= 0: continue
        j_ext = ie // 4 - r0; js = max(j_ext, 0)
        zona, vals = _niveles_col(M[:, js], ext, lado, X, j_ext >= ib_desde)
        pen = (vals - ext) if lado == 1 else (ext - vals)
        barr = np.isfinite(vals) & (pen > 0) & (pen <= X / 2) & ((ent - vals) * lado > 0)
        banda = False
        for nom in BANDAS[lado]:
            L_ = M[NIVELES.index(nom), js]
            if np.isfinite(L_):
                pb = (L_ - ext) * lado
                banda |= bool((pb >= (-0.5 + 1e-6) * X - 1e-9) and (pb <= 1e-9))
        seg0 = ("vela", float(o[sb]), float(h[sb]), float(l[sb]), float(c[sb]))
        res = {}
        for tsal in ("S6", "P1"):
            res[tsal] = simular(lado, ent, stop, R, seg0, orr[k + 1:], hr[k + 1:], lr[k + 1:], cr[k + 1:], tsal)
            res[tsal + "o"] = simular(-lado, ent, ent + lado * R, R, seg0, orr[k + 1:], hr[k + 1:], lr[k + 1:], cr[k + 1:], tsal)
        evs.append(dict(entrada_tipo="stopc", k=k, pos=0, ic=4 * sb, hora="%02d:%02d" % divmod(570 + k * minutos, 60), lado=lado, ext=ext,
                        j_ext=j_ext, entrada=ent, stop=stop, R=R, X=X, estado=tipo[kp], lado_vw=int(lvw[kp]), rrn=float(rrn[kp]),
                        marea=int(mD[lp]), a_favor_marea=int(mD[lp] == lado), vw_dist=float((ent - vwr[kp]) * lado),
                        delta_fav=float(dl[sbp] * lado), n_zona=int(zona.sum()), n_barr=int(barr.sum()), banda=int(banda),
                        niveles="|".join(([NIVELES[i] for i in np.nonzero(zona[:-1])[0]] + (["r50"] if zona[-1] else []))),
                        barridos="|".join(([NIVELES[i] for i in np.nonzero(barr[:-1])[0]] + (["r50"] if barr[-1] else []))),
                        **{"%s_%s" % (a, b): v for a, r in res.items() for b, v in zip(("pts", "motivo", "velas", "mfe", "mae"), r)}))
    # ---- entradas al CIERRE de la vela de la vuelta
    for k in range(30 // minutos - 1, 360 // minutos - 1):
        sb = r0 + k; li = 4 * sb + 3; pi_ = 4 * sb - 1
        if oNP[li] <= oNP[pi_]: continue
        lado = int(oD[li])
        if lado == 0: continue
        ie, pxe, tp, ic = opiv[oNP[li] - 1]
        if -tp != lado: continue
        ext = float(pxe); ent = float(cr[k]); stop = ext - lado * 2 * TK; R = (ent - stop) * lado
        if R <= 0: continue
        j_ext = ie // 4 - r0; js = max(min(j_ext, k), 0)
        zona, vals = _niveles_col(M[:, js], ext, lado, X, j_ext >= ib_desde)
        pen = (vals - ext) if lado == 1 else (ext - vals)
        barr = np.isfinite(vals) & (pen > 0) & (pen <= X / 2) & ((ent - vals) * lado > 0)
        banda = False
        for nom in BANDAS[lado]:
            L_ = M[NIVELES.index(nom), js]
            if np.isfinite(L_):
                pb = (L_ - ext) * lado
                banda |= bool((pb >= (-0.5 + 1e-6) * X - 1e-9) and (pb <= 1e-9))
        res = {}
        for tsal in ("S6", "P1"):
            res[tsal] = simular(lado, ent, stop, R, None, orr[k + 1:], hr[k + 1:], lr[k + 1:], cr[k + 1:], tsal)
            res[tsal + "o"] = simular(-lado, ent, ent + lado * R, R, None, orr[k + 1:], hr[k + 1:], lr[k + 1:], cr[k + 1:], tsal)
        evs.append(dict(entrada_tipo="cierre", k=k, pos=4, ic=li, hora="%02d:%02d" % divmod(570 + (k + 1) * minutos, 60), lado=lado, ext=ext,
                        j_ext=j_ext, entrada=ent, stop=stop, R=R, X=X, estado=tipo[k], lado_vw=int(lvw[k]), rrn=float(rrn[k]),
                        marea=int(mD[li]), a_favor_marea=int(mD[li] == lado), vw_dist=float((ent - vwr[k]) * lado),
                        delta_fav=float(dl[sb] * lado), n_zona=int(zona.sum()), n_barr=int(barr.sum()), banda=int(banda),
                        niveles="|".join(([NIVELES[i] for i in np.nonzero(zona[:-1])[0]] + (["r50"] if zona[-1] else []))),
                        barridos="|".join(([NIVELES[i] for i in np.nonzero(barr[:-1])[0]] + (["r50"] if barr[-1] else []))),
                        **{"%s_%s" % (a, b): v for a, r in res.items() for b, v in zip(("pts", "motivo", "velas", "mfe", "mae"), r)}))
    out["eventos"] = pd.DataFrame(evs)
    return out


# ================================================================ variantes y operacion
def mascaras(ev):
    t1 = (ev["a_favor_marea"] == 1) & (ev["vw_dist"] > 0)
    t7 = t1 & ((ev["n_zona"] >= 1) | (ev["n_barr"] >= 1) | (ev["delta_fav"] > 0))
    t6 = t1 & (ev["delta_fav"] > 0)
    en_T = (ev["estado"] == "T") & (ev["lado"] == ev["lado_vw"])
    en_R = ev["estado"] == "R"
    return dict(T1=t1 & en_T, T7=t7 & en_T, T6=t6 & en_T, V4e=(ev["banda"] == 1) & en_R, T1_todos=t1, T7_todos=t7)


VARIANTES = {   # nombre: (entrada, lista de (mascara, salida)). Las 4 pre-registradas: entrada al CIERRE de la vela de la vuelta.
    "T7-S6": ("cierre", [("T7", "S6")]),
    "T6-P1": ("cierre", [("T6", "P1")]),
    "COMB-T7+V4e-S6": ("cierre", [("T7", "S6"), ("V4e", "S6")]),
    "V4e-S6": ("cierre", [("V4e", "S6")]),
}
# COTAS descriptivas (no son prueba; la calibracion mostro que estan sesgadas): entrada a stop dentro de la vela con el orden supuesto
# (cota alta: esconde las vueltas que fallan dentro de la vela) y entrada a stop conservadora (cota baja: entrada y stop en la misma vela = stop)
VARIANTES_COTA = {"T7-S6-stop-camino": ("stop", [("T7", "S6")]), "T7-S6-stop-conservador": ("stopc", [("T7", "S6")]),
                  "T6-P1-stop-camino": ("stop", [("T6", "P1")]), "T6-P1-stop-conservador": ("stopc", [("T6", "P1")])}
VARIANTES_CAMINO = {"T7-S6-camino": ("stop", [("T7", "S6")]), "T6-P1-camino": ("stop", [("T6", "P1")]),
                    "COMB-camino": ("stop", [("T7", "S6"), ("V4e", "S6")]),
                    "T7-S6-stopc": ("stopc", [("T7", "S6")]), "T6-P1-stopc": ("stopc", [("T6", "P1")])}


def operar(ev_dia, nombre, variantes=None):
    """Una posicion por vez, maximo 5 por rueda. ev_dia: eventos de UNA rueda. Devuelve las operaciones tomadas."""
    variantes = variantes or VARIANTES
    ent, partes = variantes[nombre]
    e = ev_dia[ev_dia["entrada_tipo"] == ent]
    if len(e) == 0: return e.assign(familia=[], salida=[], pts=[], pts_o=[], motivo=[])
    mk = mascaras(e)
    filas = []
    for fam, sal in partes:
        sub = e[mk[fam].to_numpy()].copy()
        sub["familia"] = fam; sub["salida"] = sal
        sub["pts"] = sub[sal + "_pts"]; sub["pts_o"] = sub[sal + "o_pts"]; sub["motivo"] = sub[sal + "_motivo"]
        sub["velas_sal"] = sub[sal + "_velas"]; sub["mfe"] = sub[sal + "_mfe"]; sub["mae"] = sub[sal + "_mae"]
        filas.append(sub)
    c = pd.concat(filas).sort_values(["ic"], kind="mergesort")
    c = c[~c.index.duplicated(keep="first")]
    tom = []; libre = -1; n = 0
    for i, r in c.iterrows():
        if r["k"] <= libre or n >= MAX_DIA: continue
        tom.append(i); n += 1; libre = r["k"] + int(r["velas_sal"])
    return c.loc[tom]


def juzgar(tk, dias, costos=(0.50, 1.00)):
    """tk: operaciones (col 'pts', 'dia', 'R'); dias: lista de ruedas evaluadas (las sin operaciones cuentan 0)."""
    n = len(tk); nd = len(dias)
    out = dict(n=int(n), ruedas=int(nd), ops_por_rueda=round(n / nd, 2) if nd else 0.0)
    if n == 0: return out
    pts = tk["pts"].to_numpy(float); R = tk["R"].to_numpy(float)
    g = pts[pts > 0]; p = pts[pts <= 0]
    out.update(acierto=round(100 * len(g) / n, 1), gana_media=round(float(g.mean()), 2) if len(g) else 0.0,
               pierde_media=round(float(p.mean()), 2) if len(p) else 0.0, bruto_op=round(float(pts.mean()), 3),
               en_R=round(float((pts / R).mean()), 3), R_medio=round(float(R.mean()), 2))
    for c_ in costos:
        net = pts - c_
        por = pd.Series(net).groupby(tk["dia"].to_numpy()).sum()
        serie = np.array([por.get(d, 0.0) for d in dias])
        r = mx = 0
        for x in net:
            r = r + 1 if x < 0 else 0; mx = max(mx, r)
        eq = np.concatenate([[0.0], np.cumsum(net)])
        k_ = "c%03d" % int(round(c_ * 100))
        out.update({k_ + "_neto_op": round(float(net.mean()), 3), k_ + "_t": round(K.t_por_rueda(serie), 2),
                    k_ + "_ruedas_pos": int((serie > 0).sum()), k_ + "_racha": int(mx),
                    k_ + "_caida": round(float((np.maximum.accumulate(eq) - eq).max()), 2)})
    return out
