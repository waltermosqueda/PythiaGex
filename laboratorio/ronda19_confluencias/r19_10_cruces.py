# -*- coding: utf-8 -*-
"""RONDA 19, paso 10: BASE DE CRUCES DE NIVEL (familia b, ruptura con iniciativa). Ver PREREGISTRO.md (03:36 AR).
Evento = el precio cruza un nivel con histeresis T (de <= L-T a >= L+T, o al reves) en la rueda. Entrada = ultimo precio del segundo;
stop = L -/+ X/2. Salidas pre-registradas S1 2R, S2 3R, S3 nivel siguiente, S4 marea (a favor), S4b persigue 4X, S5 VWAP, S6 1R.
Solo ES de diseno y confirmacion (la guardada no se toca). Uso: python -I r19_10_cruces.py -> scratchpad/r19/eventos_cruces.parquet"""
import json, math, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import r19_comun as C
import r19_02_motor as M
import r19_08_eventos as EV

TK = C.TICK


def cruces_dia(inst, dia, muestra, ef10):
    dd = M.Dia(inst, dia); R = dd.R; X = dd.X; meta = dd.meta
    T = max(2 * TK, math.floor(X / 8 / TK + 0.5) * TK)
    c = dd.c
    nombres = list(M.NIVELES[:-1]); mats = [dd.M[i] for i in range(len(nombres))]
    lo, hi = np.nanmin(c), np.nanmax(c)
    for k in range(int(math.floor((lo - 60) / 50.0)), int(math.ceil((hi + 60) / 50.0)) + 1):
        nombres.append("r50_%d" % (50 * k)); mats.append(np.full(M.NR, 50.0 * k))
    filas = []
    for nom, L in zip(nombres, mats):
        ok = np.isfinite(L)
        arriba = ok & (c >= L + T); abajo = ok & (c <= L - T)
        estado = 0
        for t in np.nonzero(arriba | abajo)[0]:
            s = 1 if arriba[t] else -1
            if estado != 0 and s != estado and t >= 1:
                lado = s
                ent = float(c[t]); Lt = float(L[t])
                stop = Lt - lado * X / 2.0
                Rp = abs(ent - stop)
                if Rp > 0:
                    filas.append((t, lado, nom, Lt, ent, stop, Rp))
            estado = s
    out = []
    for (t, lado, nom, Lt, ent, stop, Rp) in sorted(filas):
        tp = max(t - 1, 0)
        a0 = max(0, tp - 59)
        mdir = int(R["m_dir"][t]); mdel = float(R["cum"][t] - R["m_c0"][t])
        b = t // 60 - 1; bb = [x for x in (b, b - 1, b - 2) if x >= 0]
        fila = dict(inst=inst, dia=dia, muestra=muestra, X=X, seg_rueda=int(t), hora_ny=M.seg_hora(t), minuto=int(t // 60), lado=lado,
                    nivel=nom, nivel_familia=("redondo50" if nom.startswith("r50") else nom), L=Lt, entrada=ent, stop=stop, R=Rp,
                    R_en_X=round(Rp / X, 3), zona_T=T,
                    marea_dir=mdir, a_favor_marea=int(mdir == lado), marea_con_agresores=int(mdel * mdir > 0), marea_delta=round(mdel),
                    ola_dir=int(R["o_dir"][t]), ola_a_favor=int(R["o_dir"][t] == lado),
                    ola_tramo_X=round(float(abs(c[t] - R["o_p0"][t]) / X), 3) if np.isfinite(R["o_p0"][t]) else np.nan,
                    vwap_rth_dist_pts=round(float((ent - R["vw_r"][t]) * lado), 3),
                    vwap_rth_dist_sd=round(float((ent - R["vw_r"][t]) / R["sd_r"][t]), 3) if R["sd_r"][t] > 0 else np.nan,
                    ruido_a_favor=int(M.contexto_c3(dd, t, lado)), ruido_en_contra=int(M.contexto_c3(dd, t, -lado)),
                    mov_desde_apertura=round(ent - meta["rth_o"], 2), eficiencia_10_previas=ef10,
                    regimen_va_y_vuelve=int(ef10 < 0.465) if ef10 == ef10 else np.nan,
                    of_apilados_favor_1v=int((R["n_ap_c"][b] if lado == 1 else R["n_ap_v"][b]) if b >= 0 else 0),
                    of_apilados_contra_1v=int((R["n_ap_v"][b] if lado == 1 else R["n_ap_c"][b]) if b >= 0 else 0),
                    of_apilados_favor_3v=int(sum(R["n_ap_c"][x] if lado == 1 else R["n_ap_v"][x] for x in bb)),
                    of_apilados_contra_3v=int(sum(R["n_ap_v"][x] if lado == 1 else R["n_ap_c"][x] for x in bb)),
                    of_imb_favor_3v=int(sum(R["imb_c"][x] if lado == 1 else R["imb_v"][x] for x in bb)),
                    of_imb_contra_3v=int(sum(R["imb_v"][x] if lado == 1 else R["imb_c"][x] for x in bb)),
                    of_netbig60_a_favor=float(R["netbig60"][tp] * lado),
                    of_big_favor_fuerte=int((R["netbig60"][tp] >= R["u_netbig60_p80"][tp] and R["netbig60"][tp] > 0) if lado == 1 else
                                            (R["netbig60"][tp] <= R["u_netbig60_p20"][tp] and R["netbig60"][tp] < 0)),
                    of_delta_60=float(R["delta"][a0:tp + 1].sum() * lado), of_delta_300=float(R["delta"][max(0, tp - 299):tp + 1].sum() * lado),
                    of_vol_60=float(R["vol"][a0:tp + 1].sum()),
                    of_abs_resiste_60=float(R["absa60"][tp] if lado == 1 else R["absb60"][tp]),
                    of_abs_resiste_rel=round(float((R["absa60"][tp] / R["u_absa60"][tp]) if lado == 1 else (R["absb60"][tp] / R["u_absb60"][tp])), 3)
                    if ((R["u_absa60"][tp] if lado == 1 else R["u_absb60"][tp]) > 0) else np.nan,
                    of_icebergs_resiste_60=int(np.isfinite((R["icea_px"] if lado == 1 else R["iceb_px"])[a0:tp + 1]).sum()),
                    of_caudal_30_rel=round(float((R["compra30"][tp] / R["u_compra30"][tp]) if lado == 1 else (R["venta30"][tp] / R["u_venta30"][tp])), 3)
                    if ((R["u_compra30"][tp] if lado == 1 else R["u_venta30"][tp]) > 0) else np.nan,
                    recorrido_60_pts=round(float((c[tp] - c[a0]) * lado), 2))
        o = {}
        o["S1"] = EV.resultado(dd, t, lado, ent, stop, Rp, "obj", ent + lado * 2 * Rp)
        o["S2"] = EV.resultado(dd, t, lado, ent, stop, Rp, "obj", ent + lado * 3 * Rp)
        obj3 = M.siguiente_nivel(dd, t, lado, ent, Rp)
        o["S3"] = EV.resultado(dd, t, lado, ent, stop, Rp, "obj", obj3)
        o["S4"] = EV.resultado(dd, t, lado, ent, stop, Rp, "marea") if mdir == lado else (np.nan, "no_aplica", np.nan)
        o["S4b"] = EV.resultado(dd, t, lado, ent, stop, Rp, "perse")
        o["S5"] = EV.resultado(dd, t, lado, ent, stop, Rp, "vwap") if (R["vw_r"][t] - ent) * lado > 0 else (np.nan, "no_aplica", np.nan)
        o["S6"] = EV.resultado(dd, t, lado, ent, stop, Rp, "obj", ent + lado * Rp)
        for k, v in o.items():
            fila[k + "_pts"] = round(float(v[0]), 3) if v[0] == v[0] else np.nan
            fila[k + "_motivo"] = v[1]; fila[k + "_seg_salida"] = int(v[2]) if v[2] == v[2] else -1
        fila["S3_objetivo"] = obj3
        mfe, mae, tst = EV.excursiones(dd, t, lado, ent, stop, Rp)
        fila.update(mfe_R=round(mfe, 3), mae_R=round(mae, 3), seg_hasta_stop=tst)
        out.append(fila)
    return out


def main_guardada(inst="ES"):
    t0 = time.time(); ef = EV.eficiencia_previa(); todas = []
    mu_obj = "guardada" if inst == "ES" else "guardada_mes"
    for dia in C.dias(inst):
        if C.muestra_de(inst, dia) != mu_obj: continue
        f = cruces_dia(inst, dia, mu_obj, ef.get(dia, np.nan)); todas += f
        print(inst, dia, "cruces", len(f), "%.1f s" % (time.time() - t0), flush=True)
    pd.DataFrame(todas).to_parquet(os.path.join(C.SCR, "eventos_cruces_%s.parquet" % mu_obj), index=False)


def main():
    t0 = time.time(); ef = EV.eficiencia_previa(); todas = []
    for dia in C.dias("ES"):
        if dia < "2026-09-10": continue
        mu = C.muestra_de("ES", dia)
        if mu not in ("diseno", "confirma"): continue
        C.frenar_si_falta_ram(2.0)
        f = cruces_dia("ES", dia, mu, ef.get(dia, np.nan)); todas += f
        print("ES", dia, mu, "cruces", len(f), "%.1f s" % (time.time() - t0), flush=True)
    df = pd.DataFrame(todas)
    df.to_parquet(os.path.join(C.SCR, "eventos_cruces.parquet"), index=False)
    df[df.muestra == "diseno"].to_parquet(os.path.join(C.SCR, "eventos_cruces_diseno.parquet"), index=False)
    print("cruces", len(df), "columnas", len(df.columns), "por rueda %.1f" % df.groupby("dia").size().mean(), "| %.1f s" % (time.time() - t0))


if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "guardada": main_guardada("ES")
    elif len(sys.argv) > 1 and sys.argv[1] == "guardada_mes": main_guardada("MES")
    else: main()
