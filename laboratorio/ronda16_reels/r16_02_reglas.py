# -*- coding: utf-8 -*-
"""RONDA 16b: SUS reglas de 2026 (PREREGISTRO.md, seccion R16b), en velas de 1 minuto de ES armadas desde la cinta.
S1 reingreso al area de valor, S2 absorcion en VAH/VAL + agresion con recompensa, S3 2.a desviacion del VWAP -> VWAP.
Gestion: stop 2-6 pts, objetivo >= 1,5 R (si no, 2 R), 60 min o 15:55, max 2 operaciones/dia, fin del dia tras 2 stops, costo 0,50.
Placebo: la operacion contraria en el mismo instante (distancias espejadas); direccion al azar = promedio de las dos.
Uso: python -I r16_02_reglas.py
"""
import os, sys
import numpy as np, pandas as pd
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from r16_01_eventos import cargar, resultado, R16, TICK, COSTO, CORTE_CONFIRMAR

VENTANA = ("09:45", "12:00")
STOP_MIN, STOP_MAX, R_MIN, R_DEF = 2.0, 6.0, 1.5, 2.0
P_AGRESION = 0.70


def velas_1m(s):
    m = pd.DataFrame({"o": s["ultimo"].resample("60s").first(), "h": s["alto"].resample("60s").max(), "l": s["bajo"].resample("60s").min(),
                      "c": s["ultimo"].resample("60s").last(), "vol": s["vol"].resample("60s").sum(), "delta": s["delta"].resample("60s").sum()})
    # VWAP y desvio de la SESION COMPLETA (desde el comienzo de la tabla = 18:00 NY del dia anterior), al cierre de cada minuto
    pv = (s["ultimo"] * s["vol"]).cumsum(); v = s["vol"].cumsum(); p2v = (s["ultimo"] ** 2 * s["vol"]).cumsum()
    vw = pv / v.replace(0, np.nan); var = (p2v / v.replace(0, np.nan) - vw ** 2).clip(lower=0)
    m["vwap"] = vw.resample("60s").last(); m["sd"] = np.sqrt(var).resample("60s").last()
    return m.dropna(subset=["c"])


def u_agresion(dia, ses):
    previas = [d for d in sorted(ses) if d < dia][-5:]
    if len(previas) < 3:
        return None
    x = pd.concat([ses[d].between_time("09:30", "15:59")["delta"].resample("60s").sum().abs() for d in previas])
    return float(x.quantile(P_AGRESION))


def objetivo_de(ent, stop, lado, preferido):
    riesgo = abs(ent - stop)
    if preferido is not None and np.isfinite(preferido) and (preferido - ent) * lado >= R_MIN * riesgo:
        return preferido
    return ent + lado * R_DEF * riesgo


def senales_del_dia(dia, niv, ses):
    dias = list(niv.index)
    if dia not in dias or dias.index(dia) == 0:
        return []
    ant = niv.loc[dias[dias.index(dia) - 1]]
    vah, val, poc = float(ant["vah"]), float(ant["val"]), float(ant["poc"])
    u = u_agresion(dia, ses)
    if u is None:
        return []
    s = ses[dia]; m = velas_1m(s)
    rueda = m.between_time("09:30", "15:59")
    hm = np.array(rueda.index.strftime("%H:%M"))
    o, h, l, c, d = (rueda[k].to_numpy() for k in ("o", "h", "l", "c", "delta"))
    vw, sd = rueda["vwap"].to_numpy(), rueda["sd"].to_numpy()
    out = []
    afuera = 0; ext = np.nan          # S1: +1 = cerro arriba de VAH, -1 = abajo de VAL; ext = extremo de la excursion
    for i in range(len(rueda)):
        # ---- S1: seguimiento de la excursion fuera del area
        if c[i] > vah:
            ext = h[i] if afuera != 1 else max(ext, h[i]); afuera = 1
        elif c[i] < val:
            ext = l[i] if afuera != -1 else min(ext, l[i]); afuera = -1
        en_ventana = VENTANA[0] <= hm[i] < VENTANA[1]
        if not en_ventana:
            if afuera == 1 and c[i] <= vah: afuera = 0
            if afuera == -1 and c[i] >= val: afuera = 0
            continue
        if afuera == 1 and c[i] <= vah and c[i] > val:
            if d[i] < 0:
                out.append(("S1", i, -1, c[i], ext + TICK, poc))
            afuera = 0
        elif afuera == -1 and c[i] >= val and c[i] < vah:
            if d[i] > 0:
                out.append(("S1", i, +1, c[i], ext - TICK, poc))
            afuera = 0
        # ---- S2 (bordes del area) y S3 (2.a desviacion): vela de absorcion i, agresion con recompensa en i+1..i+3
        cands = [("S2", val, +1, poc), ("S2", vah, -1, poc)]
        if np.isfinite(sd[i]) and sd[i] > 0:
            cands += [("S3", vw[i] - 2 * sd[i], +1, None), ("S3", vw[i] + 2 * sd[i], -1, None)]
        for nombre, nivel, lado, obj_pref in cands:
            toca = (l[i] <= nivel + 1.0 and l[i] >= nivel - 1.0) if lado > 0 else (h[i] >= nivel - 1.0 and h[i] <= nivel + 1.0)
            if nombre == "S3":
                toca = (l[i] <= nivel) if lado > 0 else (h[i] >= nivel)
            absorcion = (d[i] < 0 and c[i] > o[i]) if lado > 0 else (d[i] > 0 and c[i] < o[i])
            if not (toca and absorcion):
                continue
            for k in range(i + 1, min(len(rueda), i + 4)):
                if lado > 0 and d[k] >= u and c[k] > h[i]:
                    out.append((nombre, k, +1, c[k], l[i] - TICK, vw[k] if nombre == "S3" else obj_pref)); break
                if lado < 0 and -d[k] >= u and c[k] < l[i]:
                    out.append((nombre, k, -1, c[k], h[i] + TICK, vw[k] if nombre == "S3" else obj_pref)); break
    # ---- ejecucion secuencial con su gestion
    out.sort(key=lambda x: x[1])
    trades = []; libre_hasta = None; n_dia = 0; stops = 0
    for nombre, k, lado, ent, stop, obj_pref in out:
        t_ent = rueda.index[k] + pd.Timedelta(seconds=59)      # entra al CIERRE de la vela k
        if libre_hasta is not None and t_ent < libre_hasta:
            continue
        if n_dia >= 2 or stops >= 2:
            break
        riesgo = (ent - stop) * lado
        if not (STOP_MIN <= riesgo <= STOP_MAX):
            continue
        obj = objetivo_de(ent, stop, lado, obj_pref)
        if nombre == "S3" and abs(obj - ent) < 2.0:
            continue
        res = resultado(s, t_ent, lado, ent, stop, obj)
        if not res:
            continue
        pts, motivo = res
        contra = resultado(s, t_ent, -lado, ent, ent + lado * riesgo, ent - lado * abs(obj - ent))
        trades.append({"dia": dia, "setup": nombre, "t": t_ent, "lado": lado, "entrada": ent, "stop": stop, "objetivo": obj,
                       "riesgo": riesgo, "pts": pts, "motivo": motivo, "contra_pts": contra[0] if contra else np.nan})
        n_dia += 1; stops += motivo == "stop"
        libre_hasta = t_ent + pd.Timedelta(minutes=60)        # conservador: una operacion a la vez
    return trades


def main():
    niv, ses = cargar()
    filas = []
    for dia in sorted(ses):
        filas += senales_del_dia(dia, niv, ses)
    df = pd.DataFrame(filas)
    df.to_csv(os.path.join(R16, "r16b_operaciones.csv"), index=False)
    print("operaciones:", len(df))
    for tramo, sub in (("EXPLORAR", df[df["dia"] < CORTE_CONFIRMAR]), ("CONFIRMAR", df[df["dia"] >= CORTE_CONFIRMAR]), ("TODO", df)):
        for setup, g in [("TODOS", sub)] + list(sub.groupby("setup")):
            if len(g) == 0:
                continue
            v = g["pts"].to_numpy(); m = v.mean(); se = v.std(ddof=1) / np.sqrt(len(v)) if len(v) > 1 else np.nan
            p2 = ((g["pts"] + g["contra_pts"]) / 2).mean()
            print(f"{tramo:9s} {setup:5s} n {len(v):3d} | media {m:+.2f} pts | t {m / se if se else np.nan:+.2f} | gana {np.mean(v > 0) * 100:3.0f} % | "
                  f"R medio {g['riesgo'].mean():.2f} | azar {p2:+.2f}")


if __name__ == "__main__":
    main()
