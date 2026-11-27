# -*- coding: utf-8 -*-
"""RONDA 23 - CANDIDATA A3: elastico de la tarde, despues del giro (13:00-15:30, VWAP a >= 1,5 X, IB extendido <= 1 vez, P geometrica >= 0,55) [CONGELADA].
Script AUTOCONTENIDO (solo numpy y pandas; no importa nada del laboratorio). Lee UNA cinta de ES de la sonda (CSV con columnas
t [UTC], primero, ultimo, vol, lado, ...; una fila por orden agresora) y la historia de rangos de la rueda de las jornadas previas,
y devuelve las operaciones que la regla habria hecho ese dia. No toca ATAS ni la red.

LA REGLA (todo con lo sabido al cierre de cada vela de 5 min de la rueda, 9:30-16:00 NY):
  - Escala: X = 0,08 x mediana del rango de la rueda (9:30-16:00) de las 10 jornadas previas, al tick (mitad para arriba).
  - VWAP de la rueda desde las 9:30:00 con el precio medio de cada orden ((primero + ultimo) / 2) ponderado por su volumen.
  - Balance inicial (IB) = maximo y minimo de 9:30 a 10:30. Extension del lado del precio = cuanto paso el maximo (o el minimo) del dia
    al IB, en veces el ancho del IB (0 si no lo paso).
  - Puntos de decision: los cierres de vela de 13:00 a 15:30 NY. En cada uno, s = lado del precio respecto del VWAP.
  - CONDICIONES: distancia al VWAP >= 1.5 X; extension del IB de ese lado <= 1.0 veces el IB; y la P geometrica = d_stop / (d_stop + d_VWAP) >= 0,55 (el precio ya volvio desde el extremo: esta mas cerca del VWAP que del stop).
  - OPERACION: hacia el VWAP (VENTA si el precio esta arriba, COMPRA si esta abajo), al precio del cierre de la vela.
    Stop: X/2 (al tick) mas alla del extremo del dia de ese lado (el maximo si se vende, el minimo si se compra).
    Objetivo: orden limitada en el VWAP (al tick), que se corre en cada cierre de vela al VWAP de ese momento; cuenta llena cuando
    una orden opera 1 tick mas alla del limite y se cobra al precio del limite. Stop lleno cuando una orden toca o pasa el stop (si la
    orden ya abrio pasada, al precio de esa orden). Lo abierto se cierra con la ultima orden antes de las 16:00:00.
  - Una posicion por vez: la entrada siguiente solo en un cierre de vela posterior a la vela que sigue a la de la salida; maximo 5 por rueda.
  - Costo de referencia del juez: 1,00 pt de ES por operacion completa.
Uso:  python -I cand_A3_tarde_giro_amplia.py <cinta.csv> <historia.json> [salida.csv]
      historia.json = {"rangos": {"AAAA-MM-DD": rango_de_la_rueda, ...}} (solo jornadas completas; se usan las 10 previas al dia).
Importable: operar(ruta_cinta, rangos) -> (lista de operaciones, info del dia)."""
import json, math, sys
import numpy as np, pandas as pd

ID = "A3"
VERSION = "r23-A3-1"
TK = 0.25
DESDE, HASTA = "13:00", "15:30"
DV_MIN = 1.5
IB_MAX = 1.0
PF_MIN = 0.55            # None = sin condicion geometrica
STOP_TIPO = "ext"    # 'ext': extremo + B*X ; 'extmult': el mas lejano entre extremo + B*X y M veces la distancia al VWAP
B_X = 0.5
M_DV = None
MAX_DIA = 5
RTH0, RTH1 = 9 * 3600 + 30 * 60, 16 * 3600


def dtick(x):
    return math.floor(x / TK + 0.5) * TK


def leer_cinta(ruta):
    d = pd.read_csv(ruta)
    d = d.drop_duplicates()
    tu = pd.to_datetime(d["t"], format="%Y-%m-%dT%H:%M:%S.%f")                 # hora UTC sin zona
    orden = np.argsort(tu.to_numpy(dtype="datetime64[ns]").astype(np.int64), kind="stable")
    d = d.iloc[orden].reset_index(drop=True)
    t = tu.iloc[orden].reset_index(drop=True).dt.tz_localize("UTC").dt.tz_convert("America/New_York")
    seg = (t.dt.hour * 3600 + t.dt.minute * 60 + t.dt.second + t.dt.microsecond / 1e6).to_numpy(float)
    fecha = t.dt.strftime("%Y-%m-%d").to_numpy()
    rth = (seg >= RTH0) & (seg < RTH1)
    if not rth.any():
        return None
    dia = fecha[rth][0]
    rth &= (fecha == dia)
    pri = d["primero"].to_numpy(float)[rth]; ult = d["ultimo"].to_numpy(float)[rth]; vol = d["vol"].to_numpy(float)[rth]
    return dict(dia=dia, seg=seg[rth], pri=pri, ult=ult, vol=vol, hi=np.maximum(pri, ult), lo=np.minimum(pri, ult))


def hhmm(seg):
    seg = int(math.floor(seg)); return "%02d:%02d:%02d" % (seg // 3600, (seg % 3600) // 60, seg % 60)     # hora truncada al segundo


def operar(ruta_cinta, rangos, cinta=None):
    c_ = cinta if cinta is not None else leer_cinta(ruta_cinta)
    if c_ is None:
        return [], {"motivo": "sin ordenes en la rueda"}
    dia = c_["dia"]
    previas = sorted(d for d in rangos if d < dia)[-10:]
    if len(previas) < 10:
        return [], {"dia": dia, "motivo": "faltan jornadas previas para la mediana de 10 (%d)" % len(previas)}
    med10 = float(np.median([float(rangos[d]) for d in previas])); X = dtick(0.08 * med10)
    seg, ult, vol, hi, lo = c_["seg"], c_["ult"], c_["vol"], c_["hi"], c_["lo"]
    pm = (c_["pri"] + ult) / 2.0
    barra = np.floor((seg - RTH0) / 300.0).astype(int)              # vela de 5 min de cada orden (0 = 9:30-9:35)
    cpv = np.cumsum(pm * vol); cv = np.cumsum(vol)
    Hc = np.maximum.accumulate(hi); Lc = np.minimum.accumulate(lo)
    nb = 78
    fin = np.searchsorted(barra, np.arange(nb), side="right") - 1      # ultima orden de cada vela (indice; -1 si no hubo)
    ok = fin >= 0
    vw_b = np.where(ok, cpv[np.maximum(fin, 0)] / cv[np.maximum(fin, 0)], np.nan)
    c_b = np.where(ok, ult[np.maximum(fin, 0)], np.nan); H_b = np.where(ok, Hc[np.maximum(fin, 0)], np.nan); L_b = np.where(ok, Lc[np.maximum(fin, 0)], np.nan)
    ib = barra < 12
    if not ib.any():
        return [], {"dia": dia, "motivo": "sin ordenes en el balance inicial"}
    ibh, ibl = float(hi[ib].max()), float(lo[ib].min()); ibr = ibh - ibl
    k0 = (int(DESDE[:2]) * 60 + int(DESDE[3:]) - 570) // 5 - 1; k1 = (int(HASTA[:2]) * 60 + int(HASTA[3:]) - 570) // 5 - 1
    # limite del objetivo para las ordenes de la vela j: VWAP al cierre de la vela j-1 (al tick, mitad para arriba; sin ordenes: el ultimo)
    vwprev = np.full(nb + 1, np.nan)
    for j in range(1, nb + 1):
        vwprev[j] = math.floor(vw_b[j - 1] / TK + 0.5) * TK if np.isfinite(vw_b[j - 1]) else vwprev[j - 1]
    ops = []; libre = -1; n = 0
    ult_rth = len(seg) - 1
    for k in range(k0, k1 + 1):
        if n >= MAX_DIA or k <= libre or not ok[k] or fin[k] >= ult_rth: continue
        c = float(c_b[k]); vw = float(vw_b[k]); s = 1 if c > vw else (-1 if c < vw else 0)
        if s == 0: continue
        dvp = abs(c - vw); dv = dvp / X
        ext_ib = max(0.0, (H_b[k] - ibh) / ibr) if s > 0 else max(0.0, (ibl - L_b[k]) / ibr)
        if dv < DV_MIN or ext_ib > IB_MAX: continue
        lado = -s
        ext = float(H_b[k]) if s > 0 else float(L_b[k])
        stop = ext + s * dtick(B_X * X)
        if STOP_TIPO == "extmult":
            a2 = c + s * dtick(M_DV * dvp)
            stop = max(stop, a2) if s > 0 else min(stop, a2)
        dstop = abs(stop - c); Pf = dstop / (dstop + dvp)
        if PF_MIN is not None and Pf < PF_MIN: continue
        # ---- simulacion orden por orden desde el cierre de la vela k
        i0 = fin[k] + 1
        b = barra[i0:]; p_hi = hi[i0:]; p_lo = lo[i0:]; p_pri = c_["pri"][i0:]
        lim = vwprev[np.minimum(b, nb)]
        if lado < 0:
            st_hit = p_hi >= stop; ob_hit = p_lo <= lim - TK
        else:
            st_hit = p_lo <= stop; ob_hit = p_hi >= lim + TK
        i_st = int(np.argmax(st_hit)) if st_hit.any() else None
        i_ob = int(np.argmax(ob_hit)) if ob_hit.any() else None
        if i_st is not None and (i_ob is None or i_st <= i_ob):
            j = i_st; motivo = "stop"
            abrio_pasada = (p_pri[j] >= stop) if lado < 0 else (p_pri[j] <= stop)
            px = float(p_pri[j]) if abrio_pasada else stop          # si la orden ya llego pasada, se llena a su precio (peor)
        elif i_ob is not None:
            j = i_ob; px = float(lim[j]); motivo = "objetivo"
        else:
            j = len(b) - 1; px = float(ult[-1]); motivo = "fin"
        pts = (px - c) * lado
        tramo_hi = p_hi[:j + 1].max(); tramo_lo = p_lo[:j + 1].min()
        mfe = (c - tramo_lo) if lado < 0 else (tramo_hi - c); mae = (tramo_hi - c) if lado < 0 else (c - tramo_lo)
        if motivo == "objetivo": mae = min(mae, dstop)
        jb = int(b[j])
        ops.append(dict(candidata=ID, version=VERSION, fecha=dia, hora_entrada=hhmm(RTH0 + 300 * (k + 1)), lado="COMPRA" if lado > 0 else "VENTA",
                        entrada=c, stop=stop, objetivo_inicial=float(np.floor(vw / TK + 0.5) * TK), vwap=round(vw, 4), X=X, mediana_10=med10,
                        dist_vwap_X=round(dv, 3), dist_extremo_X=round(abs(ext - c) / X, 3), ext_IB=round(ext_ib, 3), P_geometrica=round(Pf, 4),
                        motivo=motivo, hora_salida=hhmm(seg[i0 + j]), salida=px, pts=round(pts, 4), neto_c100=round(pts - 1.0, 4),
                        mfe=round(float(mfe), 4), mae=round(float(mae), 4)))
        n += 1; libre = jb
    return ops, {"dia": dia, "X": X, "mediana_10": med10, "ib_h": ibh, "ib_l": ibl, "ultima_orden_rth": hhmm(seg[-1]),
                 "hueco_rth_max_s": round(float(np.diff(seg).max()), 1) if len(seg) > 1 else None, "operaciones": len(ops)}


def main():
    if len(sys.argv) < 3:
        print(__doc__); sys.exit(1)
    rangos = json.load(open(sys.argv[2], encoding="utf-8"))["rangos"]
    ops, info = operar(sys.argv[1], rangos)
    print(json.dumps(info, ensure_ascii=False))
    for o in ops: print(json.dumps(o, ensure_ascii=False))
    if len(sys.argv) > 3 and ops:
        pd.DataFrame(ops).to_csv(sys.argv[3], index=False)


if __name__ == "__main__":
    main()
