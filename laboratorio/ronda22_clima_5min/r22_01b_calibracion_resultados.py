# -*- coding: utf-8 -*-
"""RONDA 22, paso 1b (calibracion, NO es la prueba): con las convenciones elegidas en r22_01,
(a) que ventana de delta de 5 min copia mejor el 'delta de los 300 s previos' de la cinta (por fidelidad, solo ventanas causales para la
    entrada a stop: la vela previa o las dos previas);
(b) cuanto rinde la TRADUCCION a 5 min de las 4 variantes en las ruedas de la R21 YA VISTAS (velas armadas desde la cinta, y las 3-4 de MES),
    contra lo que dio la cinta en la R21. Sirve para saber cuanto se pierde por traducir; no cambia ninguna definicion de variante.
Uso: python -I r22_01b_calibracion_resultados.py -> salidas/calibracion_b.txt"""
import json, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r22_comun as K
import r22_motor as MO
import r22_01_calibracion as CA
import numpy as np, pandas as pd

L = []


def W(s=""):
    print(s, flush=True); L.append(s)


def main():
    t0 = time.time(); f = K.curva_f()
    CONV = json.load(open(os.path.join(K.SAL, "calibracion.json")))["conv"]
    W("RONDA 22 - CALIBRACION (b) con las convenciones %s (%s)" % (CONV, time.strftime("%Y-%m-%d %H:%M")))
    ev21 = pd.read_parquet(os.path.join(K.SCR21, "eventos_r21.parquet"))
    reg21 = pd.read_parquet(os.path.join(K.SCR21, "regimen_minuto.parquet"))
    per = dict(zip(reg21.dia, reg21.periodo))
    dias_B = sorted(ev21.dia.unique())
    TODAS = dict(MO.VARIANTES); TODAS.update(MO.VARIANTES_CAMINO)
    ops = {nom: [] for nom in TODAS}; dl_ok = {"previa": [], "dos_previas": [], "la_de_la_vuelta (no causal)": []}
    par = []
    for d in dias_B:
        K.frenar_si_falta_ram(2.0)
        ses, _ = CA.velas_de_cinta(d); R, meta = CA.cargar_ras(d); ref = CA.ref_r21(R, meta, f)
        J = MO.jornada(ses, CA.ctx_r19(meta), f, CONV, 5, eventos=True, din_externos=CA.din_ext(R))
        ev = J["eventos"]
        for nom in TODAS:
            tk = MO.operar(ev, nom, TODAS); ops[nom].append(tk.assign(dia=d, periodo=per[d]))
        # parejas de las entradas a stop conservadoras con las vueltas de la cinta (mismo lado, extremo a <= 2 ticks, misma vela +-1)
        ec = ev[ev.entrada_tipo == "stopc"]
        e21 = ev21[(ev21.dia == d) & (ev21.seg_rueda >= 1800) & (ev21.seg_rueda < 21600)]
        usados = set()
        for _, r in e21.iterrows():
            kb = int(r.seg_rueda) // 300
            cand = ec[(ec.lado == r.lado) & ((ec.ext - r.ext).abs() <= 0.5 + 1e-9) & ((ec.k - kb).abs() <= 1) & (~ec.index.isin(usados))]
            if len(cand):
                cc = cand.iloc[0]; usados.add(cand.index[0])
                par.append(dict(S6_5=cc.S6_pts > 0, S6_21=r.S6_pts > 0, R5=cc.R, R21=r.R, k_menos_kb=int(cc.k) - kb))
            else:
                par.append(dict(S6_5=np.nan, S6_21=r.S6_pts > 0, R5=np.nan, R21=r.R, k_menos_kb=np.nan))
        par.append(dict(_n=len(ec), _u=len(usados)))
        # delta: parejas con la cinta
        e5 = ev[ev.entrada_tipo == "stop"]
        e21 = ev21[(ev21.dia == d) & (ev21.seg_rueda >= 1800) & (ev21.seg_rueda < 21600)]
        dlt = J["delta"]
        for _, r in e21.iterrows():
            kb = int(r.seg_rueda) // 300
            cand = e5[(e5.lado == r.lado) & ((e5.ext - r.ext).abs() <= 0.5 + 1e-9) & ((e5.k - kb).abs() <= 1)]
            if not len(cand): continue
            k = int(cand.iloc[0].k); s = int(r.lado); y = r.of_delta_300 > 0
            dl_ok["previa"].append((dlt[k - 1] * s > 0) == y)
            dl_ok["dos_previas"].append(((dlt[k - 1] + dlt[k - 2]) * s > 0) == y)
            dl_ok["la_de_la_vuelta (no causal)"].append((dlt[k] * s > 0) == y)
    W("(a) delta a favor: coincidencia con el signo del delta de los 300 s previos de la cinta (vueltas emparejadas, velas de la cinta):")
    for k_, v in dl_ok.items():
        W("    %-30s %.1f %% (n %d)" % (k_, 100 * np.mean(v), len(v)))
    pp_ = pd.DataFrame([x for x in par if "_n" not in x]); nn = pd.DataFrame([x for x in par if "_n" in x])
    mm = pp_[pp_.S6_5.notna()]
    W("Entradas a STOP CONSERVADORAS contra las vueltas de la cinta: %d vueltas de la cinta, con pareja %.1f %%; %d entradas de 5 min, con pareja %.1f %%" % (
        len(pp_), 100 * len(mm) / len(pp_), int(nn._n.sum()), 100 * nn._u.sum() / max(nn._n.sum(), 1)))
    W("   en las parejas: resultado 1R coincide %.1f %% (gana en la cinta %.1f %%, en 5 min %.1f %%); R medio 5 min %.2f, cinta %.2f; vela 5 min - vela de la vuelta: %s" % (
        100 * np.mean(mm.S6_5.astype(bool) == mm.S6_21.astype(bool)), 100 * mm.S6_21.astype(bool).mean(), 100 * mm.S6_5.astype(bool).mean(),
        mm.R5.mean(), mm.R21.mean(), mm.k_menos_kb.value_counts().sort_index().to_dict()))
    W("   vueltas de la cinta SIN pareja: ganan a 1R el %.1f %% (con pareja %.1f %%)" % (100 * pp_[pp_.S6_5.isna()].S6_21.astype(bool).mean(), 100 * mm.S6_21.astype(bool).mean()))
    W("")
    W("(b) LAS VARIANTES TRADUCIDAS, en las ruedas de la R21 (velas de 5 min armadas desde la cinta de ES; ya vistas):")
    ref21 = {"T7-S6": "[entrada al cierre] cinta R21 (entrada en el segundo de la vuelta): 50,0 / 69,4 / 66,7 % ; juntas 86 ops 62,8 % +1,22 (c0,50)",
             "T6-P1": "[entrada al cierre] cinta R21: 60,0 / 62,5 / 72,7 % ; juntas 74 ops 64,9 % +1,88",
             "COMB-T7+V4e-S6": "[entrada al cierre] cinta R21 (con iceberg): 60,4 / 63,8 / 66,7 % ; juntas 122 ops 63,1 % +1,20",
             "V4e-S6": "[entrada al cierre] cinta R21 V4-S6-ROT (con iceberg): 75,0 / 56,2 / 75,0 % ; juntas 52 ops 69,2 % +1,72",
             "T7-S6-camino": "(cota alta: stop dentro de la vela con el orden supuesto)", "T6-P1-camino": "(cota alta)",
             "COMB-camino": "(cota alta)", "T7-S6-stopc": "(cota baja: stop conservador)", "T6-P1-stopc": "(cota baja)"}
    for nom in TODAS:
        tk = pd.concat(ops[nom], ignore_index=True)
        partes = []
        for pp in ("diseno", "confirma", "guardada"):
            dd = [d for d in dias_B if per[d] == pp]
            r = MO.juzgar(tk[tk.periodo == pp], dd)
            partes.append("%s n %d %.1f %% %+.2f" % (pp, r["n"], r.get("acierto", 0), r.get("c050_neto_op", 0)) if r["n"] else "%s n 0" % pp)
        r = MO.juzgar(tk, dias_B)
        W("  %-15s %s | juntas n %d (%.2f/rueda) %.1f %% neto c0,50 %+.3f c1,00 %+.3f t %.2f" % (
            nom, " | ".join(partes), r["n"], r["ops_por_rueda"], r.get("acierto", 0), r.get("c050_neto_op", 0), r.get("c100_neto_op", 0), r.get("c050_t", 0)))
        W("  %-15s %s" % ("", ref21[nom]))
        if nom in ("COMB-T7+V4e-S6", "COMB-camino"):
            for fam in ("T7", "V4e"):
                s = tk[tk.familia == fam]
                W("  %-15s   parte %s: n %d %.1f %% %+.2f" % ("", fam, len(s), 100 * (s.pts > 0).mean() if len(s) else 0, (s.pts - 0.5).mean() if len(s) else 0))
    open(os.path.join(K.SAL, "calibracion_b.txt"), "w", encoding="utf-8").write("\n".join(L))
    W("%.1f s" % (time.time() - t0))


if __name__ == "__main__":
    main()
