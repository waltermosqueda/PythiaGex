# -*- coding: utf-8 -*-
"""RONDA 21, paso 5: MECANICA. (1) Cada familia por estado del detector (BALANCE / DUDOSO / TENDENCIA) y por periodo, evento por evento
(sin la regla de una posicion por vez: es la tasa de base de cada pieza en cada clima). (2) Que separa ganadoras de perdedoras dentro de
cada familia y estado: cociente de acierto con / sin cada pieza, IC 95 % (Katz), y si el signo se repite en los periodos.
Descriptivo, en las 55 ruedas: NO es validacion. Uso: python -I r21_05_mecanica.py"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r21_comun as K
import numpy as np, pandas as pd
import r21_04_estrategia as ES

SAL = os.path.join(K.AQUI, "salidas")


def ic_cociente(a1, n1, a0, n0):
    if min(a1, n1 - a1, a0, n0 - a0) <= 0 or n1 == 0 or n0 == 0: return np.nan, np.nan, np.nan
    rr = (a1 / n1) / (a0 / n0); se = math.sqrt(1 / a1 - 1 / n1 + 1 / a0 - 1 / n0)
    return rr, rr * math.exp(-1.96 * se), rr * math.exp(1.96 * se)


def main():
    ev, cr, d_all, dias = ES.cargar()
    est = pd.read_parquet(os.path.join(K.SCR, "estados_minuto.parquet")).sort_values(["dia", "m"]).reset_index(drop=True)
    tipo = est["estado"].to_numpy(); s1 = d_all["s1"].to_numpy()
    c = ES.candidatos(ev, cr, dias)
    c["estado"] = tipo[c["row"].to_numpy()]
    c["lado_ok"] = c["lado"].to_numpy() == s1[c["row"].to_numpy()]
    # rasgos de regimen del minuto
    for col in ("rrn", "z", "sost", "vw_cruces60", "min_desde_toque", "vel30n", "ib_un_lado_favor", "vw_lado60", "_pts_dalton", "hora"):
        c["reg_" + col] = d_all[col].to_numpy()[c["row"].to_numpy()]
    L = []
    def W(s=""):
        print(s, flush=True); L.append(s)
    W("MECANICA (descriptiva, 55 ruedas, evento por evento): acierto y resultado en R por familia x estado x periodo")
    for vs in ("V3-S6", "V2-S6", "V1-S6", "V4-S6", "V5-S6", "V6-S6mc", "T1-S6", "T1-P2", "T2-S6", "T3-S6", "T4-S6", "T4-S4"):
        g = c[c.vs == vs]
        W("== %s" % vs)
        for pp in K.PERIODOS + ("todas",):
            gp = g if pp == "todas" else g[g.periodo == pp]
            partes = []
            for e in ("B", "D", "T"):
                ge = gp[gp.estado == e]
                if e == "T" and vs.startswith("T"): ge = ge[ge.lado_ok]
                if len(ge) == 0: partes.append("%s n 0" % e); continue
                partes.append("%s n %4d acierto %5.1f %% %+.3f R" % (e, len(ge), 100 * (ge.pts > 0).mean(), (ge.pts / ge.R).mean()))
            W("   %-9s %s" % (pp, " | ".join(partes)))
    # ------------------------------------------------------------ que separa ganadoras de perdedoras
    W(""); W("QUE SEPARA GANADORAS DE PERDEDORAS (salida 1R; cociente de acierto con/sin la pieza, IC 95 %; signo por periodo d/c/g)")
    piezas_v = {
        "absorcion F1": lambda e: e.F1 == 1, "iceberg F2": lambda e: e.F2 == 1, "divergencia F3": lambda e: e.F3 == 1,
        "agotamiento F4": lambda e: e.F4 == 1, "apilado a favor F5": lambda e: e.F5 == 1, "big trades a favor F6": lambda e: e.F6 == 1,
        "retroceso con agresores": lambda e: e.ctx_ret_con_agresores == 1, "nivel en la zona": lambda e: e.n_niveles_en_zona >= 1,
        "barrida y vuelta": lambda e: e.n_barridas_y_vuelta >= 1, "delta 60 s a favor": lambda e: e.of_delta_60 > 0,
        "delta 300 s a favor": lambda e: e.of_delta_300 > 0, "icebergs en contra 60 s": lambda e: e.of_icebergs_contra_60 >= 1,
        "apilados en contra": lambda e: e.of_apilados_contra_3v >= 1, "marea con agresores": lambda e: e.marea_con_agresores == 1,
        "vuelta rapida (<= 107 s)": lambda e: (e.seg_rueda - e.hora_ext.map(ES.FIN._seg)) <= 107,
        "retroceso profundo (>= 2 X)": lambda e: e.ctx_prof_X >= 2, "despues de las 12:00": lambda e: e.seg_rueda >= 9000,
        "VWAP a >= 2R": lambda e: (-e.vwap_rth_dist_pts) >= 2 * e.R, "lejos del VWAP (|sd| >= 1,5)": lambda e: e.vwap_rth_dist_sd.abs() >= 1.5,
        "fuera del ruido a favor": lambda e: e.ruido_a_favor_en_vuelta == 1, "fuera del ruido en contra": lambda e: e.ruido_en_contra_en_vuelta == 1,
        "IB roto de un lado": lambda e: e.ctx_ext_ib.isin(["arriba", "abajo"]), "IB roto de los dos": lambda e: e.ctx_ext_ib == "ambas",
        "dentro del valor de ayer": lambda e: e.ctx_en_va_ayer == "dentro",
    }
    piezas_r = {"rango >= 1,3 de lo normal": lambda g: g.reg_rrn >= 1.3, "ruido sostenido (sost >= 0,8)": lambda g: g.reg_sost >= 0.8,
                "2+ cruces del VWAP en 60 min": lambda g: g.reg_vw_cruces60 >= 2, "sin tocar el VWAP hace 60+ min": lambda g: g.reg_min_desde_toque >= 60,
                "velocidad 30 min >= 1,5": lambda g: g.reg_vel30n >= 1.5, "puntos de Dalton >= 2": lambda g: g._pts_d >= 2}
    for vs, estados in (("V3-S6", ("B",)), ("V1-S6", ("B",)), ("V4-S6", ("B",)), ("T1-S6", ("T",)), ("T3-S6", ("T",)), ("V3-S6", ("T",)), ("T1-S6", ("B",))):
        g = c[(c.vs == vs) & c.estado.isin(estados)]
        if vs.startswith("T") and "T" in estados: g = g[g.lado_ok]
        if len(g) < 30: continue
        e = ev.loc[g.idx.to_numpy()].reset_index(drop=True); g = g.reset_index(drop=True); g["_pts_d"] = g["reg__pts_dalton"]
        gana = (g.pts > 0).to_numpy(); per = g.periodo.to_numpy()
        W("== %s en estado %s: n %d, acierto %.1f %%" % (vs, "/".join(estados), len(g), 100 * gana.mean()))
        filas = []
        for nom, f in list(piezas_v.items()) + list(piezas_r.items()):
            try:
                m = np.asarray(f(e) if nom in piezas_v else f(g), bool)
            except Exception:
                continue
            n1 = int(m.sum()); n0 = int((~m).sum())
            if n1 < 8 or n0 < 8: continue
            a1 = int(gana[m].sum()); a0 = int(gana[~m].sum())
            rr, lo, hi = ic_cociente(a1, n1, a0, n0)
            signos = ""
            for pp in K.PERIODOS:
                mp = per == pp
                if (m & mp).sum() >= 4 and (~m & mp).sum() >= 4:
                    d_ = gana[m & mp].mean() - gana[~m & mp].mean(); signos += "+" if d_ > 0 else "-"
                else: signos += "."
            filas.append((rr if rr == rr else 1.0, nom, n1, 100 * a1 / n1, n0, 100 * a0 / n0, lo, hi, signos))
        for rr, nom, n1, p1, n0, p0, lo, hi, sg in sorted(filas, key=lambda z: -abs(math.log(z[0])) if z[0] > 0 else 0)[:12]:
            W("   %-32s con %4d: %5.1f %% | sin %4d: %5.1f %% | cociente %.2f (IC %.2f-%.2f) | periodos d/c/g %s" % (nom, n1, p1, n0, p0, rr, lo, hi, sg))
    open(os.path.join(SAL, "mecanica.txt"), "w", encoding="utf-8").write("\n".join(L))


if __name__ == "__main__":
    main()
