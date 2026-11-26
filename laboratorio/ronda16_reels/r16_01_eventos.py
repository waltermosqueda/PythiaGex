# -*- coding: utf-8 -*-
"""RONDA 16, paso 1: el PATRON del trader de los reels, detectado en la cinta de ES y medido contra placebos.

El patron (de sus videos y operaciones explicadas, ver _referencia; aca sin nombre propio):
  1. DONDE: un nivel marcado de antemano. Se usan los de su pantalla: maximo/minimo/cierre del dia anterior, POC/VAH/VAL del dia
     anterior, maximo/minimo de la noche y, desde las 10:30, el balance inicial. Nada mira adelante: los del dia anterior y la noche
     se conocen a las 9:30; el balance inicial recien a las 10:30.
  2. ABSORCION: el precio toca el nivel y el lado contrario EMPUJA (delta en contra) sin desplazar el precio mas alla de la zona.
  3. CONFIRMACION: aparece la agresion a favor que SI desplaza el precio (rompe el micro-rango de la absorcion).
  4. ENTRADA en esa ruptura; STOP detras de la zona absorbida (extremo del micro-rango + 1 tick, entre 2 y 8 puntos; si pide mas,
     no se opera); OBJETIVO = el siguiente nivel a favor que quede a >= 1,5 R (si no hay hasta 4 R, se usa 2 R). Salida a los
     60 minutos o a las 15:55 si no toco nada.
Placebos (mismos dias, mismos niveles, misma gestion): P1 = entrar al TOCAR el nivel sin mirar el order flow (rebote ciego);
P2 = mismo momento de la entrada pero direccion al azar (estimado con muchas tiradas).
Parametros FIJADOS ANTES de mirar resultados (PREREGISTRO.md). Costo ida y vuelta ES: 0,50 pts (1 tick + comision).
Uso: python -I r16_01_eventos.py [--variante base|grandes|apilados] -> resultados en datos/ronda16/r16_<variante>.csv + resumen.
"""
import argparse, glob, os, sys
import numpy as np, pandas as pd

try:
    import ctypes, ctypes.wintypes as _W
    _k = ctypes.windll.kernel32; _k.GetCurrentProcess.restype = _W.HANDLE; _k.SetPriorityClass.argtypes = [_W.HANDLE, _W.DWORD]
    _k.SetPriorityClass(_k.GetCurrentProcess(), 0x00004000)
except Exception:
    pass

RAIZ = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CACHE = os.path.join(RAIZ, "datos", "flujo", "cache", "ES")
R16 = os.path.join(RAIZ, "datos", "ronda16")
TICK = 0.25
COSTO = 0.50
NY = "America/New_York"
# ---- PREREGISTRO (no se tocan despues de ver resultados) ----
TOQUE = 2 * TICK          # el precio llega al nivel (+-2 ticks)
VENTANA_ABS = 180         # s: ventana de absorcion desde el toque
EXCESO_MAX = 6 * TICK     # durante la absorcion el precio no puede pasar el nivel por mas de 1,5 pts (si pasa, la zona no aguanto)
DELTA_ABS = 0.60          # el delta en contra en la ventana tiene que ser <= -p60 de los deltas de 3 min de las 5 sesiones previas
CONF_MAX = 600            # s: la confirmacion tiene que llegar dentro de los 10 min siguientes al toque
DELTA_CONF = 0.60         # delta a favor de 60 s >= p60 de los deltas de 60 s de las 5 sesiones previas
STOP_MIN, STOP_MAX = 2.0, 8.0
R_MIN, R_SIN_NIVEL = 1.5, 2.0
HORIZONTE = 3600
FIN_DIA = "15:55"
CORTE_CONFIRMAR = "2026-10-13"   # se explora hasta el 09-10 y se confirma UNA vez desde el 13-10


def cargar():
    niv = pd.read_parquet(os.path.join(R16, "niveles.parquet")).set_index("dia").sort_index()
    ses = {}
    for f in sorted(glob.glob(os.path.join(CACHE, "seg-*.parquet"))):
        dia = os.path.basename(f)[4:14]
        s = pd.read_parquet(f, columns=["ultimo", "alto", "bajo", "vol", "delta", "d50", "hueco"])
        s.index = pd.to_datetime(s.index).tz_localize("UTC").tz_convert(NY).tz_localize(None)
        ses[dia] = s
    return niv, ses


def niveles_del_dia(dia, niv, s):
    """Niveles conocidos a las 9:30 (dia anterior + noche) y, desde las 10:30, el balance inicial."""
    dias = list(niv.index)
    if dia not in dias or dias.index(dia) == 0:
        return None
    ant = niv.loc[dias[dias.index(dia) - 1]]; hoy = niv.loc[dia]
    base = {"PDH": ant["h"], "PDL": ant["l"], "PDC": ant["c"], "PPOC": ant["poc"], "PVAH": ant["vah"], "PVAL": ant["val"],
            "ONH": hoy["on_h"], "ONL": hoy["on_l"]}
    tarde = {"IBH": hoy["ib_h"], "IBL": hoy["ib_l"]}
    return base, tarde


def umbrales(dia, ses):
    """Percentiles de delta de 3 min y de 60 s en las 5 sesiones ANTERIORES (rueda). Nada mira adelante."""
    previas = [d for d in sorted(ses) if d < dia][-5:]
    if len(previas) < 3:
        return None
    d3, d1 = [], []
    for d in previas:
        s = ses[d].between_time("09:30", "15:59")
        d3.append(s["delta"].resample("180s").sum().abs()); d1.append(s["delta"].resample("60s").sum().abs())
    return float(pd.concat(d3).quantile(DELTA_ABS)), float(pd.concat(d1).quantile(DELTA_CONF))


def resultado(s, t_ent, lado, entrada, stop, objetivo):
    """Barrera doble con el precio de cada segundo (alto/bajo) desde t_ent (excluido); si nada toca, cierre por tiempo."""
    fin = min(t_ent + pd.Timedelta(seconds=HORIZONTE), pd.Timestamp(t_ent.date()) + pd.Timedelta(FIN_DIA + ":00"))
    w = s.loc[t_ent + pd.Timedelta(seconds=1): fin]
    if len(w) == 0:
        return None
    if lado > 0:
        st = np.nonzero(w["bajo"].to_numpy() <= stop)[0]; ob = np.nonzero(w["alto"].to_numpy() >= objetivo)[0]
    else:
        st = np.nonzero(w["alto"].to_numpy() >= stop)[0]; ob = np.nonzero(w["bajo"].to_numpy() <= objetivo)[0]
    i_st = st[0] if len(st) else 10 ** 9; i_ob = ob[0] if len(ob) else 10 ** 9
    if i_st == i_ob == 10 ** 9:
        salida = float(w["ultimo"].iloc[-1]); motivo = "tiempo"
    elif i_st <= i_ob:          # el mismo segundo: se asume el stop (conservador)
        salida = stop; motivo = "stop"
    else:
        salida = objetivo; motivo = "objetivo"
    return (salida - entrada) * lado - COSTO, motivo


def eventos_del_dia(dia, niv, ses, variante):
    s = ses[dia]; r = s.between_time("09:30", FIN_DIA)
    nv = niveles_del_dia(dia, niv, s); um = umbrales(dia, ses)
    if nv is None or um is None or len(r) < 1000:
        return []
    base, tarde = nv; u3, u1 = um
    activos_todos = dict(base); activos_todos.update(tarde)
    px_h = r["alto"].to_numpy(); px_l = r["bajo"].to_numpy(); px = r["ultimo"].to_numpy(); dl = r["delta"].to_numpy()
    d50 = r["d50"].to_numpy(); t = r.index
    d1 = pd.Series(dl).rolling(60, min_periods=1).sum().to_numpy()      # delta de los ultimos 60 s en cada segundo
    hm = np.array(t.strftime("%H:%M"))
    out = []
    for nombre, L in activos_todos.items():
        if not np.isfinite(L):
            continue
        desde = "10:30" if nombre in tarde else "09:30"
        toques = np.nonzero((px_l <= L + TOQUE) & (px_h >= L - TOQUE) & (hm >= desde))[0]
        libre = -1
        for i in toques:
            if i < libre:
                continue
            prev = px[max(0, i - 60)]
            if prev > L + TOQUE: lado = +1          # viene de arriba: soporte -> largo
            elif prev < L - TOQUE: lado = -1        # viene de abajo: resistencia -> corto
            else: continue
            libre = i + 1800                        # un evento por nivel cada 30 min
            activos = dict(base)
            if hm[i] >= "10:30": activos.update(tarde)
            ev = {"dia": dia, "nivel": nombre, "L": L, "lado": lado, "t_toque": t[i]}
            ent1 = float(px[i]); st1 = L - lado * 4.0
            res1 = resultado(s, t[i], lado, ent1, st1, otro_nivel(activos, L, lado, ent1, st1))
            ev["p1_pts"], ev["p1_motivo"] = res1 if res1 else (np.nan, "")
            j1 = min(len(r) - 1, i + VENTANA_ABS)
            tramo_d = dl[i:j1 + 1].sum()
            if lado > 0: exceso = L - px_l[i:j1 + 1].min(); en_contra = -tramo_d
            else: exceso = px_h[i:j1 + 1].max() - L; en_contra = tramo_d
            ev["exceso"] = exceso; ev["delta_contra"] = en_contra; ev["u3"] = u3; ev["u1"] = u1
            ok_abs = exceso <= EXCESO_MAX and en_contra >= u3
            ev["absorcion"] = bool(ok_abs)
            if ok_abs:
                rng_h = px_h[i:j1 + 1].max(); rng_l = px_l[i:j1 + 1].min()
                j2 = min(len(r) - 1, i + CONF_MAX); k = None
                for jj in range(j1, j2 + 1):
                    if lado > 0 and d1[jj] >= u1 and px_h[jj] > rng_h: k = jj; break
                    if lado < 0 and -d1[jj] >= u1 and px_l[jj] < rng_l: k = jj; break
                if k is not None and variante == "grandes" and d50[i:k + 1].sum() * lado <= 0:
                    k = None        # variante: ademas, las ordenes grandes netas (>= 50) tienen que ir a favor
                if k is not None:
                    ent = float(px[k]); stop = (rng_l - TICK) if lado > 0 else (rng_h + TICK)
                    riesgo = (ent - stop) * lado; ev["riesgo"] = riesgo
                    if STOP_MIN <= riesgo <= STOP_MAX:
                        obj = otro_nivel(activos, L, lado, ent, stop)
                        res = resultado(s, t[k], lado, ent, stop, obj)
                        if res:
                            ev["t_ent"] = t[k]; ev["entrada"] = ent; ev["stop"] = stop; ev["objetivo"] = obj
                            ev["pts"], ev["motivo"] = res
                            # P2: la operacion CONTRARIA en el mismo instante, mismas distancias (stop y objetivo espejados)
                            dist_ob = abs(obj - ent)
                            res2 = resultado(s, t[k], -lado, ent, ent + lado * riesgo, ent - lado * dist_ob)
                            ev["contra_pts"] = res2[0] if res2 else np.nan
            out.append(ev)
    return out


def otro_nivel(activos, L, lado, ent, stop):
    riesgo = abs(ent - stop)
    cands = sorted(v for v in activos.values() if np.isfinite(v) and (v - ent) * lado >= R_MIN * riesgo and (v - ent) * lado <= 4 * riesgo)
    if cands:
        return cands[0] if lado > 0 else cands[-1]
    return ent + lado * R_SIN_NIVEL * riesgo


def main():
    ap = argparse.ArgumentParser(); ap.add_argument("--variante", default="base"); a = ap.parse_args()
    niv, ses = cargar()
    filas = []
    for dia in sorted(ses):
        filas += eventos_del_dia(dia, niv, ses, a.variante)
    df = pd.DataFrame(filas)
    df.to_csv(os.path.join(R16, "r16_%s.csv" % a.variante), index=False)
    print("eventos (toques):", len(df), "| con absorcion:", int(df["absorcion"].sum()), "| operaciones:", int(df["pts"].notna().sum()))
    for tramo, sub in (("EXPLORAR", df[df["dia"] < CORTE_CONFIRMAR]), ("CONFIRMAR", df[df["dia"] >= CORTE_CONFIRMAR])):
        op = sub[sub["pts"].notna()]
        p1 = sub[sub["p1_pts"].notna()]
        def fila(x, col):
            if len(x) == 0: return "n 0"
            v = x[col].to_numpy(); m = v.mean(); se = v.std(ddof=1) / np.sqrt(len(v)) if len(v) > 1 else np.nan
            return f"n {len(v)} | media {m:+.2f} pts | t {m / se:+.2f} | gana {np.mean(v > 0) * 100:.0f} %"
        print(f"{tramo}: PATRON {fila(op, 'pts')}  ||  P1 rebote ciego {fila(p1, 'p1_pts')}")
        if len(op) and "contra_pts" in op:
            p2 = (op["pts"] + op["contra_pts"]) / 2.0     # esperanza con direccion al azar en el mismo instante
            print(f"   P2 direccion al azar en el mismo instante: media {p2.mean():+.2f} pts")
            for nv_, g in op.groupby("nivel"):
                print(f"      {nv_:5s} n {len(g):3d} media {g['pts'].mean():+.2f} gana {np.mean(g['pts'] > 0) * 100:.0f} %")


if __name__ == "__main__":
    main()
