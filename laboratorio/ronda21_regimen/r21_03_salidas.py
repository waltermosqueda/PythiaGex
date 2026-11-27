# -*- coding: utf-8 -*-
"""RONDA 21, paso 3: salidas NUEVAS desde la cinta (motor de la R19) para las vueltas de la ola y para las rupturas con iniciativa (B2),
y las versiones del lado contrario con el mismo R (placebo PD). Ver PREREGISTRO.md, etapa 2.
Por evento (lado real): P1 (mitad 1R + resto 2R con stop en la entrada), P2 (mitad 1R + resto con la raya de la marea, stop en la entrada;
solo a favor de la marea), P3 (mitad 1R + resto al VWAP RTH, stop en la entrada; solo con el VWAP del lado de la ganancia), y las
simetricas S5b (objetivo fijo a la distancia del VWAP), P2b (resto persigue 4X), P3b (resto a la distancia fija del VWAP).
Lado contrario (mismo R): S6o, S1o, S4bo, P1o, P2bo, S5bo, P3bo. Fade de la R19 (S6mc) para todas las vueltas.
Salidas: scratchpad/r21/eventos_r21.parquet y cruces_r21.parquet. Uso: python -I r21_03_salidas.py"""
import math, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r21_comun as K
import numpy as np, pandas as pd
import r19_02_motor as M
import r19_08_eventos as EV
import r19_10_cruces as CR

TK = K.TK


def parcial(d, t, s, E, stop, R, resto, obj2=None):
    ts1, px1, mot1 = M.salida(d, t, s, E, stop, "obj", E + s * R)
    if mot1 != "objetivo":
        return (px1 - E) * s, mot1, ts1
    if resto == "obj": ts2, px2, mot2 = M.salida(d, ts1, s, E, E, "obj", obj2)
    elif resto == "marea": ts2, px2, mot2 = M.salida(d, ts1, s, E, E, "marea", X=d.X)
    elif resto == "vwap": ts2, px2, mot2 = M.salida(d, ts1, s, E, E, "vwap")
    else: ts2, px2, mot2 = M.salida(d, ts1, s, E, E, "perse", X=d.X)
    return 0.5 * R + 0.5 * (px2 - E) * s, "1R+" + mot2, ts2


def simple(d, t, s, E, stop, regla, obj=None):
    ts, px, mot = M.salida(d, t, s, E, stop, regla, obj, X=d.X)
    return (px - E) * s, mot, ts


def calcular(d, r, tipo_base):
    """r: fila del evento. Devuelve dict de columnas nuevas."""
    t = int(r["seg_rueda"]); s = int(r["lado"]); E = float(r["entrada"]); stop = float(r["stop"]); R = float(r["R"])
    out = {}
    def put(nom, res):
        out[nom + "_pts"] = round(float(res[0]), 3); out[nom + "_motivo"] = res[1]; out[nom + "_seg_salida"] = int(res[2])
    vw = float(d.R["vw_r"][t]); dist = (vw - E) * s
    put("P1", parcial(d, t, s, E, stop, R, "obj", E + s * 2 * R))
    if int(r["marea_dir"]) == s: put("P2", parcial(d, t, s, E, stop, R, "marea"))
    put("P2b", parcial(d, t, s, E, stop, R, "perse"))
    if dist > 0:
        put("P3", parcial(d, t, s, E, stop, R, "vwap"))
        put("S5b", simple(d, t, s, E, stop, "obj", E + s * dist))
        put("P3b", parcial(d, t, s, E, stop, R, "obj", E + s * dist))
    # lado contrario, mismo R
    o = -s; st_o = E - o * R
    put("S6o", simple(d, t, o, E, st_o, "obj", E + o * R))
    put("S1o", simple(d, t, o, E, st_o, "obj", E + o * 2 * R))
    put("S4bo", simple(d, t, o, E, st_o, "perse"))
    put("P1o", parcial(d, t, o, E, st_o, R, "obj", E + o * 2 * R))
    put("P2bo", parcial(d, t, o, E, st_o, R, "perse"))
    if dist > 0:
        put("S5bo", simple(d, t, o, E, st_o, "obj", E + o * dist))
        put("P3bo", parcial(d, t, o, E, st_o, R, "obj", E + o * dist))
    if tipo_base == "vueltas":
        # fade de la R19 (r19_16_fade.py): lado -s, objetivo = extremo -/+ 1 tick, stop = entrada +/- (R + 1 tick)
        ext = float(r["ext"]); fs = -s
        put("S6mc", simple(d, t, fs, E, E - fs * (R + TK), "obj", ext + fs * TK))
    return out


def main():
    t0 = time.time()
    ev = pd.read_parquet(os.path.join(K.SCR19, "eventos_todos_con_guardada.parquet"))
    ev = ev.sort_values(["dia", "seg_rueda", "t_utc_ms"], kind="mergesort").reset_index(drop=True)
    # cruces: diseno+confirma de la R19 + guardada armada con el MISMO codigo
    cr = pd.read_parquet(os.path.join(K.SCR19, "eventos_cruces.parquet"))
    f_g = os.path.join(K.SCR, "eventos_cruces_guardada.parquet")
    if not os.path.exists(f_g):
        ef = EV.eficiencia_previa(); tod = []
        for dia in K.dias_es():
            if K.periodo_de(dia) != "guardada": continue
            K.frenar_si_falta_ram(2.0)
            f = CR.cruces_dia("ES", dia, "guardada", ef.get(dia, np.nan)); tod += f
            print("cruces guardada", dia, len(f), "%.1f s" % (time.time() - t0), flush=True)
        pd.DataFrame(tod).to_parquet(f_g, index=False)
    crg = pd.read_parquet(f_g)
    cr = pd.concat([cr, crg], ignore_index=True)
    b2 = ((cr["of_apilados_favor_3v"] >= 1) & (cr["of_big_favor_fuerte"] == 1) & (cr["a_favor_marea"] == 1) & (cr["marea_con_agresores"] == 1))
    cr = cr[b2].sort_values(["dia", "seg_rueda"], kind="mergesort").reset_index(drop=True)
    print("vueltas", len(ev), "| rupturas B2", len(cr), "(por muestra: %s)" % cr.groupby("muestra").size().to_dict(), flush=True)
    nuevas_v = []; nuevas_c = []
    for dia in sorted(ev["dia"].unique()):
        K.frenar_si_falta_ram(2.0)
        d = M.Dia("ES", dia)
        for _, r in ev[ev.dia == dia].iterrows():
            nuevas_v.append(calcular(d, r, "vueltas"))
        for _, r in cr[cr.dia == dia].iterrows():
            nuevas_c.append(calcular(d, r, "cruces"))
        print(dia, K.periodo_de(dia), "%.1f s | RAM libre %.1f GB" % (time.time() - t0, K.ram_libre_gb()), flush=True)
        del d
    ev = pd.concat([ev, pd.DataFrame(nuevas_v)], axis=1)
    cr = pd.concat([cr, pd.DataFrame(nuevas_c)], axis=1)
    ev.to_parquet(os.path.join(K.SCR, "eventos_r21.parquet"), index=False)
    cr.to_parquet(os.path.join(K.SCR, "cruces_r21.parquet"), index=False)
    # control: la parcial P1 debe coincidir con S6 cuando S6 termina en stop (mismo stop, mismo primer objetivo)
    st = ev["S6_motivo"] == "stop"
    print("control: S6 stop -> P1 igual en %d de %d" % (int((np.abs(ev.loc[st, "P1_pts"] - ev.loc[st, "S6_pts"]) < 1e-6).sum()), int(st.sum())))
    ob = ev["S6_motivo"] == "objetivo"
    print("control: S6 objetivo -> P1 con 1R+ en %d de %d" % (int(ev.loc[ob, "P1_motivo"].str.startswith("1R+").sum()), int(ob.sum())))
    print("control: fade S6mc contra -S6 (R19 verifico 220/220): coincide en %d de %d" % (
        int((np.abs(ev["S6mc_pts"] + ev["S6_pts"]) <= 2 * TK + 1e-9).sum()), len(ev)))
    print("total %.1f s" % (time.time() - t0))


if __name__ == "__main__":
    main()
