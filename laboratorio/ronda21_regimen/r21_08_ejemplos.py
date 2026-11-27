# -*- coding: utf-8 -*-
"""RONDA 21, paso 8: operaciones ejemplares por regimen y COMPROBACION desde la cinta cruda (independiente del motor):
precio de entrada, stop, R, salida (objetivo 1R pasado por 1 tick o stop), el estado del detector (rango hasta ahora / (mediana 10 x f(m)))
y el lado del VWAP, recalculados orden por orden. Uso: python -I r21_08_ejemplos.py"""
import json, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r21_comun as K
import numpy as np, pandas as pd
import r19_02_motor as M

TK = K.TK
SAL = os.path.join(K.AQUI, "salidas")


def hora(seg):
    return M.seg_hora(int(seg))


def verificar(dia, seg, lado, entrada, R, salida, f_curva):
    """Recalcula desde la cinta compacta: entrada (ultimo precio del segundo), estado rrn del minuto anterior, VWAP RTH, y el resultado
    de S6 (1R) o P1 (mitad 1R + resto 2R con stop en la entrada)."""
    tp = K.C19.leer("ES", dia)
    s = tp["ny_s"].to_numpy(); pri = tp["primero"].to_numpy(); ult = tp["ultimo"].to_numpy(); vol = tp["vol"].to_numpy(float)
    mn = np.minimum(pri, ult); mx = np.maximum(pri, ult)
    t0 = 9.5 * 3600
    en_seg = (s >= t0 + seg) & (s < t0 + seg + 1)
    ent_cinta = float(ult[np.nonzero(en_seg)[0][-1]]) if en_seg.any() else np.nan
    meta = json.load(open(os.path.join(K.SCR19, "rasgos", "ES-%s.json" % dia)))
    m = int(seg // 60 - 1); fin_m = t0 + 60 * (m + 1)
    rth = (s >= t0) & (s < fin_m)
    H = mx[rth].max(); L = mn[rth].min()
    rrn = (H - L) / (meta["mediana_rango_10"] * f_curva[m])
    pm = (pri + ult) / 2.0
    vw = float((pm[rth] * vol[rth]).sum() / vol[rth].sum())
    c_m = float(ult[np.nonzero(rth)[0][-1]])
    lado_vw = int(np.sign(c_m - vw)); lado_O = int(np.sign(c_m - meta["rth_o"]))
    # salida desde la cinta: ordenes posteriores al segundo de entrada, hasta las 16:00
    post = (s >= t0 + seg + 1) & (s < 16 * 3600)
    hi = mx[post]; lo = mn[post]; uu = ult[post]; ss = s[post]
    stop = entrada - lado * R
    def primero(cond):
        k = np.nonzero(cond)[0]; return k[0] if len(k) else 10 ** 9
    if lado == 1: k_st = primero(lo <= stop); k_ob = primero(hi >= entrada + R + TK)
    else: k_st = primero(hi >= stop); k_ob = primero(lo <= entrada - R - TK)
    if k_st == k_ob == 10 ** 9: res = (uu[-1] - entrada) * lado; mot = "fin"
    elif k_st <= k_ob: res = -R; mot = "stop %s" % (hora(ss[k_st] - t0))
    else:
        mot = "1R %s" % hora(ss[k_ob] - t0); res = R
        if salida == "P1":
            j = np.nonzero(ss >= np.floor(ss[k_ob]) + 1)[0]; j = j[0] if len(j) else len(ss)
            hi2, lo2, ss2 = hi[j:], lo[j:], ss[j:]
            if lado == 1: a = primero(lo2 <= entrada); b = primero(hi2 >= entrada + 2 * R + TK)
            else: a = primero(hi2 >= entrada); b = primero(lo2 <= entrada - 2 * R - TK)
            if a == b == 10 ** 9: r2 = (uu[-1] - entrada) * lado; mot += " + resto al cierre"
            elif a <= b: r2 = 0.0; mot += " + resto en la entrada %s" % hora(ss2[a] - t0)
            else: r2 = 2 * R; mot += " + resto 2R %s" % hora(ss2[b] - t0)
            res = 0.5 * R + 0.5 * r2
    return dict(entrada_cinta=ent_cinta, rrn=round(rrn, 2), H=H, L=L, vwap=round(vw, 2), precio_min=c_m, lado_vwap=lado_vw, lado_apertura=lado_O,
                resultado=round(res, 3), motivo=mot)


def main():
    f = np.array(json.load(open(os.path.join(K.SCR, "curvas_previas.json")))["f"])
    ev = pd.read_parquet(os.path.join(K.SCR, "eventos_r21.parquet"))
    reg = pd.read_parquet(os.path.join(K.SCR, "regimen_minuto.parquet"))
    ops = pd.read_csv(os.path.join(SAL, "ops-r21-etapa3.csv"))
    L = []
    def W(s=""):
        print(s, flush=True); L.append(s)
    cols = ["dia", "periodo", "seg", "lado", "entrada", "R", "pts", "salida", "var", "estado", "gex", "idx"]
    com = ops[ops.estrategia == "COMB-T7+V4"][cols].copy()
    e = ev.loc[com["idx"].to_numpy()]
    for k in ("ext", "stop", "hora_ext", "n_niveles_en_zona", "niveles_en_zona", "barrida_y_vuelta", "of_delta_300", "of_delta_60",
              "of_netbig60_a_favor", "F1", "F2", "F6", "marea_con_agresores", "ctx_prof_X", "vwap_rth_dist_pts", "vwap_rth_dist_sd", "S6_motivo",
              "S6_seg_salida", "X"):
        com[k] = e[k].to_numpy()
    rr = reg.set_index(["dia", "m"])
    com["rrn"] = [rr.loc[(d_, int(s_ // 60 - 1)), "rrn"] for d_, s_ in zip(com.dia, com.seg)]
    com["hora"] = com["seg"].map(hora)
    W("OPERACIONES DE COMB-T7+V4 (TENDENCIA: T7-S6, ROTACION: V4-S6), las 122:")
    for _, r in com.iterrows():
        W("  %s %s %s %-6s %s en %.2f (extremo %.2f, R %.2f) rrn %.2f | %s %+.2f (%s) | niveles %s barrida %s delta300 %+.0f grandes %+.0f F1 %d F2 %d | VWAP a %+.1f pts (%+.2f sd) | gamma %s" % (
            r.dia, r.hora, r.periodo[:4], r["var"], "COMPRA" if r.lado == 1 else "VENTA", r.entrada, r.ext, r.R, r.rrn,
            "GANA" if r.pts > 0 else "pierde", r.pts, r.S6_motivo, r.niveles_en_zona or "-", r.barrida_y_vuelta or "-", r.of_delta_300,
            r.of_netbig60_a_favor, r.F1, r.F2, -r.vwap_rth_dist_pts, r.vwap_rth_dist_sd, r.gex))
    # comprobacion desde la cinta en 6 operaciones (una por periodo y familia, ganadoras y perdedoras)
    W(""); W("COMPROBACION DESDE LA CINTA CRUDA (independiente del motor):")
    sel = []
    for pp in K.PERIODOS:
        for v in ("T7", "V4"):
            g = com[(com.periodo == pp) & (com["var"] == v)]
            if len(g): sel.append(g.iloc[0])
            gl = g[g.pts <= 0]
            if len(gl) and pp == "guardada": sel.append(gl.iloc[0])
    t6 = ops[(ops.estrategia == "T6-P1")].head(2)
    for _, r in t6.iterrows():
        x = r.copy(); x["var"] = "T6-P1"; sel.append(x)
    ok = 0
    for r in sel:
        sal = "P1" if r["var"] == "T6-P1" else "S6"
        v = verificar(r.dia, int(r.seg), int(r.lado), float(r.entrada), float(r.R), sal, f)
        rrn_motor = reg.set_index(["dia", "m"]).loc[(r.dia, int(r.seg // 60 - 1)), "rrn"]
        coincide = abs(v["entrada_cinta"] - r.entrada) < 1e-9 and abs(v["resultado"] - r.pts) < 1e-6 and abs(v["rrn"] - rrn_motor) < 0.01
        ok += coincide
        W("  %s %s %-6s lado %+d: entrada motor %.2f / cinta %.2f | resultado motor %+.3f / cinta %+.3f (%s) | rrn motor %.2f / cinta %.2f (H %.2f L %.2f) | VWAP cinta %.2f, precio %.2f, lado VWAP %+d, lado apertura %+d -> %s" % (
            r.dia, hora(r.seg), r["var"], r.lado, r.entrada, v["entrada_cinta"], r.pts, v["resultado"], v["motivo"], rrn_motor, v["rrn"], v["H"], v["L"],
            v["vwap"], v["precio_min"], v["lado_vwap"], v["lado_apertura"], "COINCIDE" if coincide else "DIFIERE"))
    W("  coinciden %d de %d" % (ok, len(sel)))
    com.to_csv(os.path.join(SAL, "ops-COMB-T7+V4-detalle.csv"), index=False)
    open(os.path.join(SAL, "ejemplos.txt"), "w", encoding="utf-8").write("\n".join(L))


if __name__ == "__main__":
    main()
