# -*- coding: utf-8 -*-
"""RONDA 19, paso 8: BASE DE EVENTOS para los exploradores (familias a-f) y para la ronda 21 de meta-etiquetado.
Un evento = cada VUELTA DE LA OLA (zigzag de X) confirmada en la rueda (9:30-16:00 NY), en las 49 ruedas de ES (diseno y
confirmacion). La guardada (MES 16-25-11) NO se toca. Cada fila trae:
  - hora UTC al ms de la orden que confirmo la vuelta, precio de esa orden, entrada (ultimo precio de ese segundo), lado
    (+1 la ola se dio vuelta para arriba = compra), el extremo del retroceso que termino (stop = extremo -/+ 2 ticks) y R;
  - rasgos del retroceso que termino (en el segundo anterior a la vuelta): profundidad, duracion, delta, velocidad, niveles en
    la zona del extremo (los 32, con distancia firmada y penetracion), order flow F1-F6 y crudos (absorcion, icebergs, big trades,
    apilados, imbalances, delta 60/300 s, caudal), divergencia y agotamiento;
  - contexto en la vuelta: marea (direccion, a favor, con agresores, tamano de su pierna, extension fuera del ruido), VWAP RTH y
    de la jornada con desvios, semanal y mensual, area de ruido, balance inicial, area de valor de ayer, regimen de las 10 ruedas
    previas (eficiencia), hora;
  - el resultado de cada salida pre-registrada desde la entrada: S1 2R, S2 3R, S3 nivel siguiente 1R-6R, S4 raya de la marea (solo si
    va a favor de la marea), S4b stop que persigue a 4X, S5 VWAP RTH (solo si el VWAP queda del lado de la ganancia), S6 1R;
    y la excursion maxima a favor y en contra (en R) antes del stop.
Todo causal: los rasgos usan solo lo sabido hasta el cierre del segundo de la vuelta (o el anterior); las salidas miran despues.
Uso: python -I r19_08_eventos.py   -> scratchpad/r19/eventos_todos.parquet (+ eventos_diseno.parquet)"""
import json, math, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import r19_comun as C
import r19_02_motor as M
import r19_01_rasgos as RS

TK = C.TICK
FUENTE = "r19_08_eventos.py (ronda 19)"


def eficiencia_previa():
    res = json.load(open(os.path.join(C.SCR, "hist", "resumen.json")))
    rows = []
    for k, v in res.items():
        inst, d = k.split("|")
        if "rth_h" in v and "rth_o" in v and v["rth_h"] > v["rth_l"]:
            rows.append((d, abs(v["rth_c"] - v["rth_o"]) / (v["rth_h"] - v["rth_l"])))
    df = pd.DataFrame(rows, columns=["dia", "ef"]).sort_values("dia").drop_duplicates("dia", keep="last").reset_index(drop=True)
    df["ef10"] = df["ef"].rolling(10).median().shift(1)
    return dict(zip(df["dia"], df["ef10"]))


def pivotes_ms(inst, dia, X):
    """Re-corre la ola orden por orden para tener el instante exacto (ns UTC) y el precio de la orden que confirmo cada pivote."""
    d = C.leer(inst, dia)
    pri = d["primero"].to_numpy(); ult = d["ultimo"].to_numpy(); vol = d["vol"].to_numpy("float64"); lado = d["lado"].to_numpy().astype(np.int64)
    mn = np.minimum(pri, ult); mx = np.maximum(pri, ult); cum = np.cumsum(vol * lado)
    _, _, _, _, _, piv = RS.maquina(mn, mx, cum, X)
    ns = d["ns"].to_numpy(); s = d["ny_s"].to_numpy()
    out = []
    for (ie, px, tipo, ic, ce) in piv:
        out.append((int(ns[ic]), float(ult[ic]), float(s[ic]), float(s[ie]), float(px), int(tipo)))
    return out


def resultado(dd, t, lado, ent, stop, Rp, regla, obj=None):
    ts, px, mot = M.salida(dd, t, lado, ent, stop, regla, obj, X=dd.X)
    return (px - ent) * lado, mot, ts


def excursiones(dd, t, lado, ent, stop, Rp):
    """MFE y MAE en R desde la entrada hasta el stop (o el fin de la rueda), y segundos hasta el stop (NaN si no lo toca)."""
    j0 = int(np.searchsorted(dd.or_seg, t + 1)); j1 = dd.ult_antes_16
    if j0 > j1: return 0.0, 0.0, np.nan
    hi = dd.or_hi[j0:j1 + 1]; lo = dd.or_lo[j0:j1 + 1]
    st = np.nonzero(lo <= stop)[0] if lado == 1 else np.nonzero(hi >= stop)[0]
    k = st[0] if len(st) else len(hi)
    if lado == 1:
        mfe = (hi[:k + 1].max() - ent) / Rp if k > 0 or len(hi) else 0.0; mae = (ent - lo[:k + 1].min()) / Rp
    else:
        mfe = (ent - lo[:k + 1].min()) / Rp; mae = (hi[:k + 1].max() - ent) / Rp
    tst = float(dd.or_seg[j0 + st[0]] - t) if len(st) else np.nan
    return float(max(mfe, 0.0)), float(max(min(mae, 99), 0.0)), tst


def eventos_dia(inst, dia, muestra, ef10):
    dd = M.Dia(inst, dia); R = dd.R; X = dd.X; meta = dd.meta; niv = meta["niv"]
    pm = pivotes_ms(inst, dia, X)
    filas = []
    RTH0 = 9.5 * 3600
    T = max(2 * TK, math.floor(X / 8 / TK + 0.5) * TK)
    for (ns_c, px_c, s_c, s_e, px_e, tipo) in pm:
        if not (RTH0 <= s_c < 16 * 3600): continue
        t = int(math.floor(s_c - RTH0))            # segundo de la vuelta (relativo a 9:30)
        if t < 1 or t >= M.NR: continue
        lado = -tipo                              # pivote minimo -> la ola se dio vuelta para arriba -> compra
        ent = float(dd.c[t]); ext = px_e
        stop = ext - 2 * TK if lado == 1 else ext + 2 * TK
        Rp = abs(ent - stop)
        if Rp <= 0: continue
        tp = t - 1                                # el retroceso, un segundo antes de la vuelta
        coherente = int(R["o_dir"][tp] == -lado and abs(R["o_ext"][tp] - ext) < 1e-9)
        f, veto = dd.factores(tp, lado)
        en_zona = dd.niveles_en_zona(tp, lado)
        ctx = M.contexto(dd, tp, lado)
        te = int(R["o_ext_seg"][tp])
        col = dd.M[:, max(te, 0)]                 # niveles en el segundo del extremo
        r50 = 50.0 * round(ext / 50.0)
        fila = dict(inst=inst, dia=dia, muestra=muestra, X=X, t_utc_ms=ns_c // 1_000_000,
                    t_utc=pd.Timestamp(ns_c, unit="ns").strftime("%Y-%m-%d %H:%M:%S.%f")[:-3],
                    hora_ny=M.seg_hora(t), seg_rueda=t, minuto=t // 60, lado=lado, px_vuelta=px_c, entrada=ent, ext=ext,
                    hora_ext=M.seg_hora(max(te, 0)) if te >= 0 else "antes_9:30", stop=stop, R=Rp, R_en_X=round(Rp / X, 3),
                    coherente=coherente)
        # marea y contexto
        mdir = int(R["m_dir"][t]); mdel = float(R["cum"][t] - R["m_c0"][t])
        fila.update(marea_dir=mdir, a_favor_marea=int(mdir == lado), marea_con_agresores=int(mdel * mdir > 0), marea_delta=round(mdel),
                    marea_ext=float(R["m_ext"][t]), marea_p0=float(R["m_p0"][t]),
                    extension_fuera_ruido=int(M.extension_marea(dd, t, lado)), extension_fuera_ruido_contra=int(M.extension_marea(dd, t, -lado)))
        fila.update({"ctx_" + k: v for k, v in ctx.items()})
        fila["ruido_a_favor_en_vuelta"] = int(M.contexto_c3(dd, t, lado))
        fila["ruido_en_contra_en_vuelta"] = int(M.contexto_c3(dd, t, -lado))
        fila["ruido_sigma"] = float(R["ruido_sigma"][min(389, t // 60)]) if len(R["ruido_sigma"]) else np.nan
        fila["mov_desde_apertura"] = round(ent - meta["rth_o"], 2)
        fila["eficiencia_10_previas"] = ef10
        fila["regimen_va_y_vuelve"] = int(ef10 < 0.465) if ef10 == ef10 else np.nan
        # VWAPs (en la vuelta)
        for k_, a, sd in (("vwap_rth", "vw_r", "sd_r"), ("vwap_jornada", "vw_j", "sd_j")):
            fila[k_] = float(R[a][t]); fila[k_ + "_sd"] = float(R[sd][t])
            fila[k_ + "_dist_pts"] = round((ent - R[a][t]) * lado, 3)
            fila[k_ + "_dist_sd"] = round((ent - R[a][t]) / R[sd][t], 3) if R[sd][t] > 0 else np.nan
        for k_, a in (("vwap_semanal", "vw_sem"), ("vwap_mensual", "vw_mes")):
            fila[k_] = float(R[a][t]); fila[k_ + "_dist_pts"] = round((ent - R[a][t]) * lado, 3)
        fila["ext_dist_vwap_rth_sd"] = round((ext - R["vw_r"][max(te, 0)]) / R["sd_r"][max(te, 0)], 3) if te >= 0 and R["sd_r"][max(te, 0)] > 0 else np.nan
        # niveles: distancia firmada del extremo a cada nivel (ext - L; positiva = el extremo quedo arriba) y si esta en la zona
        for i, nom in enumerate(M.NIVELES[:-1]):
            L = col[i]
            fila["d_" + nom] = round(float(ext - L), 3) if np.isfinite(L) else np.nan
        fila["d_r50"] = round(float(ext - r50), 3)
        fila["niveles_en_zona"] = "|".join(en_zona); fila["n_niveles_en_zona"] = len(en_zona)
        fila["zona_T"] = T
        # penetracion y vuelta adentro (familia d): niveles a <= X/2 que el extremo PERFORO (del lado del retroceso) y el precio ya volvio
        perf = []
        for i, nom in enumerate(M.NIVELES):
            L = col[i] if i < len(col) else r50
            if not np.isfinite(L): continue
            pen = (L - ext) if lado == 1 else (ext - L)          # cuanto paso el extremo mas alla del nivel (positivo = perforo)
            if 0 < pen <= X / 2 and (ent - L) * lado > 0:
                perf.append("%s:%.2f" % (nom, pen))
        fila["barrida_y_vuelta"] = "|".join(perf); fila["n_barridas_y_vuelta"] = len(perf)
        # order flow del retroceso (t-1) y crudos
        fila.update({"F%d" % k: int(f["F%d" % k]) for k in range(1, 7)})
        fila["veto_caudal"] = int(veto); fila["puntaje_F1_F6"] = int(sum(f.values()))
        for k_ in ("absb60", "absa60", "netbig60", "venta30", "compra30", "u_absb60", "u_absa60", "u_netbig60_p80", "u_netbig60_p20",
                   "u_venta30", "u_compra30", "u_big"):
            fila["of_" + k_] = float(R[k_][tp])
        fila["of_abs_lado_pasivo_60"] = float(R["absb60"][tp] if lado == 1 else R["absa60"][tp])   # absorcion del lado que defiende
        fila["of_abs_lado_pasivo_rel"] = round(fila["of_abs_lado_pasivo_60"] / (R["u_absb60"][tp] if lado == 1 else R["u_absa60"][tp]), 3) \
            if (R["u_absb60"][tp] if lado == 1 else R["u_absa60"][tp]) > 0 else np.nan
        fila["of_netbig60_a_favor"] = float(R["netbig60"][tp] * lado)
        a0 = max(0, tp - 59)
        ib_px = R["iceb_px"][a0:tp + 1] if lado == 1 else R["icea_px"][a0:tp + 1]
        fila["of_icebergs_60"] = int(np.isfinite(ib_px).sum())
        fila["of_icebergs_contra_60"] = int(np.isfinite((R["icea_px"] if lado == 1 else R["iceb_px"])[a0:tp + 1]).sum())
        fila["of_delta_60"] = float(R["delta"][a0:tp + 1].sum() * lado)
        fila["of_delta_300"] = float(R["delta"][max(0, tp - 299):tp + 1].sum() * lado)
        fila["of_vol_60"] = float(R["vol"][a0:tp + 1].sum())
        fila["of_bigc_60"] = float(R["bigc"][a0:tp + 1].sum()); fila["of_bigv_60"] = float(R["bigv"][a0:tp + 1].sum())
        b = t // 60 - 1; bb = [x for x in (b, b - 1, b - 2) if x >= 0]
        fila["of_apilados_favor_3v"] = int(sum(R["n_ap_c"][x] if lado == 1 else R["n_ap_v"][x] for x in bb))
        fila["of_apilados_contra_3v"] = int(sum(R["n_ap_v"][x] if lado == 1 else R["n_ap_c"][x] for x in bb))
        fila["of_imb_favor_3v"] = int(sum(R["imb_c"][x] if lado == 1 else R["imb_v"][x] for x in bb))
        fila["of_imb_contra_3v"] = int(sum(R["imb_v"][x] if lado == 1 else R["imb_c"][x] for x in bb))
        fila["cvd"] = float(R["cum"][t])
        # salidas pre-registradas
        o1 = resultado(dd, t, lado, ent, stop, Rp, "obj", ent + lado * 2 * Rp)
        o2 = resultado(dd, t, lado, ent, stop, Rp, "obj", ent + lado * 3 * Rp)
        obj3 = M.siguiente_nivel(dd, t, lado, ent, Rp)
        o3 = resultado(dd, t, lado, ent, stop, Rp, "obj", obj3)
        o4 = resultado(dd, t, lado, ent, stop, Rp, "marea") if mdir == lado else (np.nan, "no_aplica", np.nan)
        o4b = resultado(dd, t, lado, ent, stop, Rp, "perse")
        o5 = resultado(dd, t, lado, ent, stop, Rp, "vwap") if (R["vw_r"][t] - ent) * lado > 0 else (np.nan, "no_aplica", np.nan)
        o6 = resultado(dd, t, lado, ent, stop, Rp, "obj", ent + lado * Rp)
        for nom, o in (("S1", o1), ("S2", o2), ("S3", o3), ("S4", o4), ("S4b", o4b), ("S5", o5), ("S6", o6)):
            fila[nom + "_pts"] = round(float(o[0]), 3) if o[0] == o[0] else np.nan
            fila[nom + "_motivo"] = o[1]
            fila[nom + "_seg_salida"] = int(o[2]) if o[2] == o[2] else -1
        fila["S3_objetivo"] = obj3
        mfe, mae, tst = excursiones(dd, t, lado, ent, stop, Rp)
        fila.update(mfe_R=round(mfe, 3), mae_R=round(mae, 3), seg_hasta_stop=tst)
        filas.append(fila)
    return filas


def main_guardada(inst="ES"):
    """SOLO para el juez final (despues de la confirmacion y con el aviso del coordinador): eventos de la guardada, mismo codigo.
    inst = 'ES' (juicio final, 16-25-11) o 'MES' (chequeo secundario, 16-25-11)."""
    t0 = time.time(); ef = eficiencia_previa(); todas = []
    mu_obj = "guardada" if inst == "ES" else "guardada_mes"
    for dia in C.dias(inst):
        if C.muestra_de(inst, dia) != mu_obj: continue
        f = eventos_dia(inst, dia, mu_obj, ef.get(dia, np.nan)); todas += f
        print(inst, dia, "eventos", len(f), "%.1f s" % (time.time() - t0), flush=True)
    pd.DataFrame(todas).to_parquet(os.path.join(C.SCR, "eventos_%s.parquet" % mu_obj), index=False)


def main():
    t0 = time.time()
    ef = eficiencia_previa()
    todas = []
    for dia in C.dias("ES"):
        if dia < "2026-09-10": continue          # 08 y 09-09: calentamiento de umbrales (sin eventos)
        mu = C.muestra_de("ES", dia)
        if mu not in ("diseno", "confirma"): continue
        C.frenar_si_falta_ram(2.0)
        f = eventos_dia("ES", dia, mu, ef.get(dia, np.nan))
        todas += f
        print("ES", dia, mu, "eventos", len(f), "%.1f s" % (time.time() - t0), flush=True)
    df = pd.DataFrame(todas)
    df.to_parquet(os.path.join(C.SCR, "eventos_todos.parquet"), index=False)
    df[df["muestra"] == "diseno"].to_parquet(os.path.join(C.SCR, "eventos_diseno.parquet"), index=False)
    print("eventos", len(df), "columnas", len(df.columns), "| por rueda: media %.1f" % df.groupby("dia").size().mean(),
          "| diseno", int((df.muestra == "diseno").sum()), "confirma", int((df.muestra == "confirma").sum()), "| %.1f s" % (time.time() - t0))


if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "guardada": main_guardada("ES")
    elif len(sys.argv) > 1 and sys.argv[1] == "guardada_mes": main_guardada("MES")
    else: main()
