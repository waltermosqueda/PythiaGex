# -*- coding: utf-8 -*-
"""RONDA 23 - SIMULADOR de brackets en velas de 5 min (real) y su REFERENCIA AL AZAR CON LA MISMA UBICACION.
Entrada al CIERRE de la vela k (lo exige la calibracion de la R22). Desde la vela k+1:
  - stop fijo o pegado al VWAP de la rueda (el de la ultima vela cerrada); tocarlo = stop; si la vela abre pasada, se llena en la apertura;
  - objetivo fijo o pegado al VWAP (orden limitada que se corre en cada vela al VWAP de la ultima vela cerrada); se cuenta lleno al pasarlo
    1 tick y se cobra al precio del objetivo;
  - REGLA CONSERVADORA: si en la misma vela caen el stop y el objetivo, cuenta como stop;
  - lo abierto se cierra al cierre de la vela jmax (por defecto la de 15:55-16:00).
Referencia al azar ('azar'): M caminos en los que cada vela posterior a la entrada se refleja al azar (+/-) alrededor del cierre previo
(se conserva su tamano y su forma; el VWAP se recalcula con los precios simulados y el volumen real). Es un paseo sin memoria con la
misma ubicacion, la misma volatilidad posterior, el mismo VWAP que se mueve, el mismo cierre de la rueda y la MISMA regla conservadora:
la esperanza bruta de cualquier bracket ahi es ~0 (sale levemente negativa por la regla conservadora). Lo que el mercado real da por
encima de esa referencia es memoria del mercado (el elastico, o su contrario)."""
import numpy as np, pandas as pd

TK = 0.25


def simular_dia(a, ks, lado, stop_f, stop_vw, stop_off, obj_f, obj_vw, obj_off, jmax, M=0, semilla=0, real=True):
    """a: arrays RTH de la rueda (o,h,l,c,vol). ks: velas de entrada (P). lado: +1 compra / -1 venta (P).
    stop_f / obj_f: niveles fijos (P; ignorados donde *_vw es True). *_off: corrimiento desde el VWAP (P, pts; nivel = vw - lado*off).
    jmax: ultima vela (P). Con real=True devuelve el camino real (M=1); si no, M caminos reflejados al azar.
    Devuelve dict de arrays (P x M): pts, motivo (0 fin, 1 stop, 2 objetivo, 3 stop_hueco), jsal; y (solo real) mfe, mae."""
    o, h, l, c, vol = (a[x] for x in ("o", "h", "l", "c", "vol"))
    n = len(c); P = len(ks)
    if P == 0:
        return None
    Mm = 1 if real else M
    p = (h + l) / 2.0
    cpv = np.cumsum(p * vol); cv = np.cumsum(vol)
    ks = np.asarray(ks, int); lado = np.asarray(lado, float)[:, None]
    stop_f = np.asarray(stop_f, float)[:, None]; obj_f = np.asarray(obj_f, float)[:, None]
    stop_vw = np.asarray(stop_vw, bool)[:, None]; obj_vw = np.asarray(obj_vw, bool)[:, None]
    stop_off = np.asarray(stop_off, float)[:, None]; obj_off = np.asarray(obj_off, float)[:, None]
    jmax = np.minimum(np.asarray(jmax, int), n - 1)[:, None]
    ent = c[ks][:, None] * np.ones((1, Mm))
    cprev = ent.copy()
    pv = cpv[ks][:, None] * np.ones((1, Mm)); vv = cv[ks][:, None] * np.ones((1, Mm))
    vlast = pv / vv
    vivo = np.ones((P, Mm), bool)
    pts = np.zeros((P, Mm)); mot = np.zeros((P, Mm), np.int8); jsal = np.full((P, Mm), -1, np.int16)
    mfe = np.zeros((P, Mm)); mae = np.zeros((P, Mm))
    rng = np.random.default_rng(semilla)
    kk = ks[:, None]
    for j in range(int(ks.min()) + 1, n):
        act = vivo & (j > kk) & (j <= jmax)
        if not act.any():
            if (j > jmax).all(): break
            continue
        do = o[j] - c[j - 1]; dh = h[j] - c[j - 1]; dl_ = l[j] - c[j - 1]; dc = c[j] - c[j - 1]
        if real:
            eps = np.ones((P, Mm))
        else:
            eps = rng.integers(0, 2, size=(P, Mm)) * 2.0 - 1.0
        o2 = cprev + eps * do
        h2 = cprev + np.where(eps > 0, dh, -dl_)
        l2 = cprev + np.where(eps > 0, dl_, -dh)
        c2 = cprev + eps * dc
        vt = np.floor(vlast / TK + 0.5) * TK                # el VWAP como precio de orden: al tick mas cercano (mitad para arriba)
        st = np.where(stop_vw, vt - lado * stop_off, stop_f)
        ob = np.where(obj_vw, vt - lado * obj_off, obj_f)
        larga = lado > 0
        st_hit = np.where(larga, l2 <= st, h2 >= st)
        gap = np.where(larga, o2 <= st, o2 >= st)
        ob_hit = np.where(larga, h2 >= ob + TK, l2 <= ob - TK)
        # excursiones (antes de resolver: dentro de la vela no se sabe el orden; se acotan al stop si lo toco)
        fav = np.where(larga, h2 - ent, ent - l2); con = np.where(larga, ent - l2, h2 - ent)
        m1 = act & st_hit
        if m1.any():
            px = np.where(gap, o2, st)
            pts[m1] = ((px - ent) * lado)[m1]
            mot[m1] = np.where(gap, 3, 1)[m1]; jsal[m1] = j; vivo[m1] = False
        m2 = act & ~st_hit & ob_hit
        if m2.any():
            pts[m2] = ((ob - ent) * lado)[m2]; mot[m2] = 2; jsal[m2] = j; vivo[m2] = False
        if real:
            mfe = np.where(act, np.maximum(mfe, fav), mfe); mae = np.where(act, np.maximum(mae, con), mae)
        m3 = act & vivo & (j == jmax)
        if m3.any():
            pts[m3] = ((c2 - ent) * lado)[m3]; mot[m3] = 0; jsal[m3] = j; vivo[m3] = False
        upd = act
        pv = np.where(upd, pv + (h2 + l2) / 2.0 * vol[j], pv); vv = np.where(upd, vv + vol[j], vv)
        vlast = np.where(upd, pv / vv, vlast); cprev = np.where(upd, c2, cprev)
    # entradas en la ultima vela (sin velas posteriores): cierre inmediato
    sin = vivo & (kk >= jmax)
    if sin.any():
        pts[sin] = 0.0; mot[sin] = 0; jsal[sin] = kk.repeat(Mm, 1)[sin]
    out = dict(pts=pts, motivo=mot, jsal=jsal)
    if real:
        out.update(mfe=mfe, mae=mae)
    return out


def dtick(x):
    """distancia en pts -> multiplo de tick, redondeando la mitad para arriba."""
    return np.floor(np.asarray(x, float) / TK + 0.5) * TK


def niveles(pt, spec):
    """Del punto (fila con c, vw, H, L, X, s) y la especificacion, arma el bracket.
    spec: lado 'fade' (hacia el VWAP: lado = -s) o 'puerta' (a favor de la extension: lado = +s);
          stop: ('ext', b) extremo del dia del lado s + b*X | ('mult', m) entrada -lado* m*|c-vw| | ('extmult', b, m) el mas lejano de los dos |
                ('vw', off) pegado al VWAP (nivel = vw - lado*off*X, se mueve) | ('vwfijo', f) en c - s*f*|c-vw| fijo | ('X', m) entrada -lado*m*X;
          obj:  ('vw', off) pegado al VWAP (fade) | ('frac', f) fijo a f*|c-vw| hacia el VWAP | ('ext', b) extremo + b*X (puerta) |
                ('R', m) m veces la distancia al stop | ('X', m) entrada + lado*m*X.
    Devuelve (lado, stop_f, stop_vw, stop_off, obj_f, obj_vw, obj_off) en arrays."""
    s = pt["s"].to_numpy(float); c = pt["c"].to_numpy(float); vw = pt["vw"].to_numpy(float); X = pt["X"].to_numpy(float)
    H = pt["H"].to_numpy(float); L = pt["L"].to_numpy(float)
    dvp = np.abs(c - vw)
    lado = -s if spec["lado"] == "fade" else s
    ext = np.where(s > 0, H, L)
    st = spec["stop"]; P = len(c)
    stop_vw = np.zeros(P, bool); stop_off = np.zeros(P)
    # todas las DISTANCIAS se pasan a ticks enteros redondeando la mitad para arriba (floor(x + 0,5)); los niveles quedan en la grilla
    if st[0] == "ext":
        stop_f = ext + s * dtick(st[1] * X)
    elif st[0] == "mult":
        stop_f = c - lado * dtick(st[1] * dvp)
    elif st[0] == "extmult":
        a1 = ext + s * dtick(st[1] * X); a2 = c - lado * dtick(st[2] * dvp)
        stop_f = np.where(lado < 0, np.maximum(a1, a2), np.minimum(a1, a2))
    elif st[0] == "X":
        stop_f = c - lado * dtick(st[1] * X)
    elif st[0] == "vw":
        stop_f = np.full(P, np.nan); stop_vw[:] = True; stop_off = dtick(st[1] * X)
    elif st[0] == "vwfijo":
        stop_f = c - s * dtick(st[1] * dvp)
    else:
        raise ValueError(st)
    ob = spec["obj"]
    obj_vw = np.zeros(P, bool); obj_off = np.zeros(P)
    if ob[0] == "vw":
        obj_f = np.full(P, np.nan); obj_vw[:] = True; obj_off = dtick(ob[1] * X)
    elif ob[0] == "frac":
        obj_f = c + lado * dtick(ob[1] * dvp)
    elif ob[0] == "ext":
        obj_f = ext + s * dtick(ob[1] * X)
    elif ob[0] == "R":
        dist = np.abs(c - stop_f); obj_f = c + lado * dtick(ob[1] * dist)
    elif ob[0] == "X":
        obj_f = c + lado * dtick(ob[1] * X)
    else:
        raise ValueError(ob)
    return lado, stop_f, stop_vw, np.broadcast_to(stop_off, (P,)).astype(float), obj_f, obj_vw, np.broadcast_to(obj_off, (P,)).astype(float)


def correr(ruedas, pts, spec, M=200, semilla=23, jmax_k=None):
    """Simula el bracket 'spec' en TODOS los puntos de pts (un DataFrame con dia, k, c, vw, H, L, X, s). Devuelve pts con columnas:
    pts_real, mot_real, jsal_real, mfe, mae y, si M > 0, azar_pts (media de M caminos), azar_gana (fraccion de caminos con pts > 0)."""
    out = []
    for dia, g in pts.groupby("dia", sort=True):
        a = ruedas[dia]
        lado, sf, svw, soff, of, ovw, ooff = niveles(g, spec)
        ks = g["k"].to_numpy(int)
        jm = np.full(len(ks), len(a["c"]) - 1) if jmax_k is None else np.minimum(ks + jmax_k, len(a["c"]) - 1)
        if "hasta_k" in spec:
            jm = np.minimum(jm, spec["hasta_k"])
        r = simular_dia(a, ks, lado, sf, svw, soff, of, ovw, ooff, jm, real=True)
        gg = g.copy()
        gg["lado_op"] = lado; gg["stop"] = sf; gg["obj"] = of
        gg["pts_real"] = r["pts"][:, 0]; gg["mot_real"] = r["motivo"][:, 0]; gg["jsal_real"] = r["jsal"][:, 0]
        gg["mfe"] = r["mfe"][:, 0]; gg["mae"] = r["mae"][:, 0]
        if M > 0:
            z = simular_dia(a, ks, lado, sf, svw, soff, of, ovw, ooff, jm, M=M, semilla=(semilla * 1000003 + int(dia.replace("-", ""))) % (2 ** 32), real=False)
            gg["azar_pts"] = z["pts"].mean(axis=1); gg["azar_gana"] = (z["pts"] > 0).mean(axis=1)
            gg["azar_stop"] = np.isin(z["motivo"], (1, 3)).mean(axis=1); gg["azar_obj"] = (z["motivo"] == 2).mean(axis=1)
        out.append(gg)
    return pd.concat(out) if out else pts.iloc[0:0]


def operar(cand, max_dia=5):
    """cand: puntos candidatos con pts_real y jsal_real (una fila por punto, ordenados por dia y k). Una posicion por vez (la siguiente
    entrada solo en una vela posterior a la de la salida), maximo max_dia por rueda. Devuelve las filas tomadas."""
    tom = []
    for dia, g in cand.groupby("dia", sort=True):
        libre = -1; n = 0
        for i, r in g.sort_values("k").iterrows():
            if r["k"] <= libre or n >= max_dia: continue
            tom.append(i); n += 1; libre = int(r["jsal_real"])
    return cand.loc[tom]
