# -*- coding: utf-8 -*-
"""RONDA 22, paso 3: CORRIDA UNICA sobre el periodo independiente (velas-MES-m5.csv, ruedas 2025-08-21 a 2026-08-31; las 10 primeras
ruedas completas son el calentamiento de la mediana de 10) y H1b sobre velas-MES-m30.csv (2024-04-14 a 2025-08-20).
Verifica las huellas SHA-256 congeladas (huellas_r22.json) y se niega a correr si alguna cambio. Hipotesis y varas: PREREGISTRO.md.
Uso: python -I r22_02_corrida.py -> salidas/h1.txt, h2.txt, h1b.txt, robustez.txt (+ .json), ops-r22-*.csv; intermedios en scratchpad/r22."""
import json, math, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r22_comun as K
import r22_motor as MO
import numpy as np, pandas as pd

HUELLAS = os.path.join(K.AQUI, "huellas_r22.json")
RNG = np.random.default_rng(2222)
L = {"h1": [], "h2": [], "h1b": [], "rob": []}


def W(cual, s=""):
    print(s, flush=True); L[cual].append(s)


def verificar_huellas():
    hu = json.load(open(HUELLAS))["sha256"]
    for rel, sha in hu.items():
        ruta = os.path.join(K.LAB, rel)
        if K.sha256(ruta) != sha:
            raise SystemExit("HUELLA CAMBIADA: %s -> no se corre" % rel)
    return len(hu)


# ---------------------------------------------------------------- curvas f(m) para la robustez
def curva_de_velas5(excluir=lambda d: False):
    """Mismo procedimiento que r21_01_regimen.curvas_previas (MES 5 min, ruedas < 2026-09-08 con las 78 velas), dejando afuera 'excluir'."""
    v = pd.read_csv(K.M5, usecols=["t", "h", "l"])
    ny = pd.to_datetime(v["t"]).dt.tz_localize("UTC").dt.tz_convert(K.NY)
    v["fecha"] = ny.dt.strftime("%Y-%m-%d"); v["mi"] = ny.dt.hour * 60 + ny.dt.minute - 570
    rth = v[(v["mi"] >= 0) & (v["mi"] < 390) & (v["fecha"] < "2026-09-08")]
    F = []
    for fecha, g in rth.groupby("fecha"):
        if excluir(fecha): continue
        g = g.sort_values("mi")
        if len(g) != 78 or not (g["mi"].to_numpy() == 5 * np.arange(78)).all(): continue
        hh = g["h"].to_numpy(float); ll = g["l"].to_numpy(float); rng = hh.max() - ll.min()
        if rng <= 0: continue
        F.append((np.maximum.accumulate(hh) - np.minimum.accumulate(ll)) / rng)
    Fm = np.median(np.array(F), axis=0); xk = 5 * np.arange(78) + 4
    return np.interp(np.arange(390), np.r_[-1, xk], np.r_[0.0, Fm]), len(F)


def curva_de_velas30(desde, hasta):
    v = MO.leer_velas(K.M30, desde, hasta); F = []
    for r, g in v.groupby("rueda"):
        g = g[(g.hm >= 570) & (g.hm < 960)].sort_values("hm")
        if len(g) != 13 or not (g["hm"].to_numpy() == 570 + 30 * np.arange(13)).all(): continue
        hh = g["h"].to_numpy(float); ll = g["l"].to_numpy(float); rng = hh.max() - ll.min()
        if rng <= 0: continue
        F.append((np.maximum.accumulate(hh) - np.minimum.accumulate(ll)) / rng)
    Fm = np.median(np.array(F), axis=0); xk = 30 * np.arange(13) + 29
    return np.interp(np.arange(390), np.r_[-1, xk], np.r_[0.0, Fm]), len(F)


# ---------------------------------------------------------------- una pasada sobre el periodo
def pasada(ses, H, dias, f_de_dia, conv, minutos, eventos, variantes=None):
    ctrl = []; evs = []; ops = {n: [] for n in (variantes or {})}; info = []
    for i, d in enumerate(dias):
        if i % 40 == 0: K.frenar_si_falta_ram(2.0)
        ctx = H.ctx(d)
        J = MO.jornada(ses[d], ctx, f_de_dia(d), conv, minutos, eventos=eventos)
        c = J["control"].assign(dia=d)
        ctrl.append(c)
        rng = float(J["h"].max() - J["l"].min()); e_dia = abs(J["c"][-1] - J["O"]) / rng if rng > 0 else np.nan
        tT = c[c.tipo == "T"]
        info.append(dict(dia=d, trimestre=K.trimestre(d), med10=ctx["med10"], med60=ctx["med60"], X=ctx["X"], rango=rng,
                         rango_rel=rng / ctx["med10"], e_dia=e_dia, L3="tendencia" if e_dia >= 0.6 else ("balance" if e_dia < 0.3 else "mixto"),
                         frac_T=float((c.tipo == "T").mean()), frac_R=float((c.tipo == "R").mean()), frac_E=float((c.tipo == "E").mean()),
                         frac_N=float((c.tipo == "N").mean()), primer_T=tT.hora.iloc[0] if len(tT) else ""))
        if eventos:
            ev = J["eventos"].assign(dia=d); evs.append(ev)
            for n in ops:
                tk = MO.operar(J["eventos"], n, variantes)
                ops[n].append(tk.assign(dia=d))
    ctrl = pd.concat(ctrl, ignore_index=True); info = pd.DataFrame(info)
    evs = pd.concat(evs, ignore_index=True) if evs else None
    ops = {n: (pd.concat(v, ignore_index=True) if v else pd.DataFrame()) for n, v in ops.items()}
    return ctrl, evs, ops, info


# ---------------------------------------------------------------- H1
def h1_medir(ctrl, etiqueta, cual="h1", detalle=True):
    c = ctrl
    T = (c.tipo == "T").to_numpy(); ext = (c.L2 == "extiende").to_numpy(); sig = (c.y1 > 0).to_numpy()
    dias = c.dia.to_numpy()
    nT = int(T.sum())
    if nT == 0:
        W(cual, "  %s: sin puntos en TENDENCIA" % etiqueta); return dict(n=len(c), nT=0)
    pT = ext[T].mean(); base = ext.mean(); dif = pT - base
    lo, hi = K.boot_ruedas(dias, ext & T, T, ext, np.ones(len(c)))
    loP, hiP = K.boot_ruedas(dias, ext & T, T)
    sT = sig[T].mean(); sb = sig.mean(); slo, shi = K.boot_ruedas(dias, sig & T, T, sig, np.ones(len(c)))
    pasa = bool(pT >= 0.55 and dif >= 0.15 and lo > 0)
    W(cual, "  %-28s puntos %6d (TENDENCIA %5d = %4.1f %%, ruedas con TENDENCIA %3d de %3d) | extremo antes que el VWAP: TENDENCIA %5.1f %% [IC %4.1f-%4.1f] base %5.1f %% "
            "dif %+5.1f [IC %+4.1f a %+4.1f] -> %s | sigue al cierre: TENDENCIA %5.1f %% base %5.1f %% dif %+5.1f [IC %+4.1f a %+4.1f]" % (
              etiqueta, len(c), nT, 100 * T.mean(), len(set(dias[T])), len(set(dias)), 100 * pT, 100 * loP, 100 * hiP, 100 * base, 100 * dif, 100 * lo, 100 * hi,
              "PASA" if pasa else "no pasa", 100 * sT, 100 * sb, 100 * (sT - sb), 100 * slo, 100 * shi))
    out = dict(n=int(len(c)), nT=nT, ruedas=int(len(set(dias))), ruedas_T=int(len(set(dias[T]))), pT=pT, base=base, dif=dif, ic_dif=(lo, hi),
               ic_pT=(loP, hiP), sigue_T=sT, sigue_base=sb, ic_sigue=(slo, shi), pasa=pasa)
    if detalle:
        for st, nom in (("R", "ROTACION"), ("E", "ESTRECHO"), ("N", "lados en desacuerdo")):
            m = (c.tipo == st).to_numpy()
            if m.any():
                W(cual, "      %-20s %5.1f %% de los puntos | extiende primero %5.1f %% | vuelve primero %5.1f %% | sigue al cierre %5.1f %%" % (
                    nom, 100 * m.mean(), 100 * ext[m].mean(), 100 * (c.L2 == "vuelve").to_numpy()[m].mean(), 100 * sig[m].mean()))
    return out


def control_geometrico(ctrl, cual="h1"):
    """Descriptivo pre-registrado: TENDENCIA contra el resto con la MISMA geometria (distancia al extremo y al VWAP en X, franja horaria)."""
    c = ctrl.copy()
    c = c[c.lado != 0]
    c["b_ext"] = pd.cut(c.dist_ext_X, [-0.01, 0.5, 1, 2, 4, 1e9], labels=False)
    c["b_vw"] = pd.cut(c.dist_vw_X, [-0.01, 1, 2, 4, 8, 1e9], labels=False)
    hh = c.hora.str[:2].astype(int) * 60 + c.hora.str[3:].astype(int)
    c["b_h"] = pd.cut(hh, [0, 690, 810, 1000], labels=False)
    c["ext"] = (c.L2 == "extiende").astype(float); c["T"] = (c.tipo == "T")
    g = c.groupby(["b_ext", "b_vw", "b_h", "T"])["ext"].agg(["mean", "size"]).unstack("T")
    g = g.dropna()
    if len(g) == 0: return None
    w = g[("size", True)]
    efT = float((g[("mean", True)] * w).sum() / w.sum()); efN = float((g[("mean", False)] * w).sum() / w.sum())
    W(cual, "  CONTROL GEOMETRICO (mismo casillero de distancia al extremo x distancia al VWAP x franja; %d casilleros con los dos): "
            "TENDENCIA %.1f %% contra el resto con la misma geometria %.1f %% -> el clima agrega %+.1f puntos (sin controlar: %+.1f)" % (
              len(g), 100 * efT, 100 * efN, 100 * (efT - efN), 100 * (c.ext[c["T"]].mean() - c.ext[~c["T"]].mean())))
    return dict(T=efT, resto=efN, casilleros=int(len(g)))


# ---------------------------------------------------------------- H2
def h2_medir(tk, dias, k_var=4, etiqueta="", cual="h2", placebo=True):
    r = MO.juzgar(tk, dias)
    tb = K.t_bonferroni(k_var, len(dias))
    if r["n"] == 0:
        W(cual, "  %-34s sin operaciones" % etiqueta); return dict(r, t_bonf=tb, pasa=False)
    pasa = bool(r["acierto"] >= 60 and r["c100_neto_op"] > 0 and r["c100_t"] >= max(2.0, tb))
    pp = np.nan
    if placebo and r["n"] >= 5:
        own = tk["pts"].to_numpy(float); opp = tk["pts_o"].to_numpy(float); real = own.mean()
        cnt = 0; B = 2000
        for _ in range(B):
            m = RNG.random(len(own)) < 0.5
            cnt += np.where(m, own, opp).mean() >= real
        pp = cnt / B
    W(cual, "  %-34s n %4d (%.2f/rueda) acierto %5.1f %% | +%.2f/%.2f | R %.2f | neto c0,50 %+.3f (t %+.2f) | c1,00 %+.3f (t %+.2f; Bonferroni %.2f) | ruedas + %d/%d | racha %d caida %.1f | PD p %s -> %s" % (
        etiqueta, r["n"], r["ops_por_rueda"], r["acierto"], r["gana_media"], r["pierde_media"], r["R_medio"], r["c050_neto_op"], r["c050_t"],
        r["c100_neto_op"], r["c100_t"], tb, r["c100_ruedas_pos"], r["ruedas"], r["c100_racha"], r["c100_caida"],
        ("%.3f" % pp) if pp == pp else "-", "PASA" if pasa else "no pasa"))
    return dict(r, t_bonf=tb, pasa=pasa, placebo_PD=pp)


def tercil(x, cortes):
    return np.where(np.isnan(x), -1, np.where(x <= cortes[0], 0, np.where(x <= cortes[1], 1, 2)))


def main():
    t0 = time.time()
    nh = verificar_huellas()
    CONV = json.load(open(os.path.join(K.SAL, "calibracion.json")))["conv"]
    f = K.curva_f()
    for cual in L: W(cual, "RONDA 22 - %s - corrida unica (%s). Huellas verificadas: %d de %d. Convenciones: %s" % (cual.upper(), time.strftime("%Y-%m-%d %H:%M"), nh, nh, CONV))
    # ============================================================ 5 min: periodo independiente
    v = MO.leer_velas(K.M5, K.INDEP[0], K.INDEP[1])
    ses = MO.sesiones(v); H = MO.Historia(ses, CONV, 5)
    comp = H.completas; cal = comp[:10]; dias = comp[10:]
    clases = pd.Series({r: H.res[r]["clase"] for r in H.dias})
    W("h1", "Jornadas %d; ruedas completas %d (calentamiento %s a %s); evaluadas %d (%s a %s). Cortas (fuera de todo): %s. Incompletas (historia si, evaluacion no): %s" % (
        len(H.dias), len(comp), cal[0], cal[-1], len(dias), dias[0], dias[-1], ",".join(clases[clases == "corta"].index),
        ",".join(clases[clases == "incompleta"].index)))
    TODAS = dict(MO.VARIANTES); TODAS.update(MO.VARIANTES_COTA)
    ctrl, evs, ops, info = pasada(ses, H, dias, lambda d: f, CONV, 5, True, TODAS)
    info["r10_60"] = info.med10 / info.med60
    cm = np.quantile(info.med10, [1 / 3, 2 / 3]); cr_ = np.nanquantile(info.r10_60, [1 / 3, 2 / 3])
    info["vol_tercil"] = tercil(info.med10.to_numpy(), cm); info["clima_tercil"] = tercil(info.r10_60.to_numpy(), cr_)
    ctrl = ctrl.merge(info[["dia", "trimestre", "med10", "r10_60", "vol_tercil", "clima_tercil", "L3"]], on="dia")
    ctrl.to_parquet(os.path.join(K.SCR, "control_5min.parquet"), index=False); evs.to_parquet(os.path.join(K.SCR, "eventos_5min.parquet"), index=False)
    info.to_csv(os.path.join(K.SAL, "ruedas_5min.csv"), index=False)
    nom_vol = {0: "volatilidad baja (mediana de 10 <= %.1f)" % cm[0], 1: "volatilidad media (%.1f-%.1f)" % (cm[0], cm[1]), 2: "volatilidad alta (> %.1f)" % cm[1]}
    nom_cli = {0: "10 previas mas calmas que las 60 (r <= %.2f)" % cr_[0], 1: "10 previas normales (%.2f-%.2f)" % (cr_[0], cr_[1]),
               2: "10 previas mas agitadas (r > %.2f)" % cr_[1], -1: "sin 60 previas"}
    # ------------------------------------------------ H1
    W("h1", ""); W("h1", "H1 (clima) - cada 5 min de 10:00 a 15:30 (cierres de vela). PASA si TENDENCIA >= 55 %, >= base + 15 puntos y el IC 95 % por rueda de la diferencia > 0.")
    res = {"H1": h1_medir(ctrl, "TODAS")}
    W("h1", "  por trimestre:")
    for q in sorted(ctrl.trimestre.unique()):
        res["H1|" + q] = h1_medir(ctrl[ctrl.trimestre == q], q, detalle=False)
    W("h1", "  por nivel de volatilidad (tercil de la mediana de 10):")
    for t_ in (0, 1, 2):
        res["H1|vol%d" % t_] = h1_medir(ctrl[ctrl.vol_tercil == t_], nom_vol[t_][:28], detalle=False)
    W("h1", "  por clima heredado (mediana de 10 / mediana de 60):")
    for t_ in (0, 1, 2, -1):
        sub = ctrl[ctrl.clima_tercil == t_]
        if len(sub): res["H1|cli%d" % t_] = h1_medir(sub, nom_cli[t_][:28], detalle=False)
    W("h1", ""); res["geom"] = control_geometrico(ctrl)
    W("h1", ""); W("h1", "Distribucion del semaforo por trimestre (% de los puntos de 10:00-15:30) y mediana de 10:")
    for q, g in ctrl.groupby("trimestre"):
        ii = info[info.trimestre == q]
        W("h1", "  %s: TENDENCIA %4.1f %% | ROTACION %4.1f %% | ESTRECHO %4.1f %% | desacuerdo %4.1f %% | mediana de 10 %.1f pts (X %.2f) | ruedas con TENDENCIA %d de %d" % (
            q, 100 * (g.tipo == "T").mean(), 100 * (g.tipo == "R").mean(), 100 * (g.tipo == "E").mean(), 100 * (g.tipo == "N").mean(),
            ii.med10.median(), ii.X.median(), int((ii.frac_T > 0).sum()), len(ii)))
    W("h1", "Por tipo de dia (al cierre; e = |cierre - apertura| / rango): % de los puntos en TENDENCIA")
    for l3, g in ctrl.groupby("L3"):
        W("h1", "  %-10s ruedas %3d | TENDENCIA %4.1f %% de los puntos | ruedas que pasaron por TENDENCIA %d" % (
            l3, g.dia.nunique(), 100 * (g.tipo == "T").mean(), int(g[g.tipo == "T"].dia.nunique())))
    # ------------------------------------------------ H2
    W("h2", ""); W("h2", "H2 (esqueleto operable) - entrada al CIERRE de la vela de 5 min donde se dio vuelta la ola; 10:00-15:30; una posicion; maximo 5 por rueda.")
    W("h2", "PASA si acierto >= 60 %% y neto > 0 con costo 1,00 y t por rueda (costo 1,00) >= t de Bonferroni (k = 4; %d ruedas)." % len(dias))
    for n in MO.VARIANTES:
        tk = ops[n].merge(info[["dia", "trimestre", "vol_tercil", "clima_tercil"]], on="dia")
        ops[n] = tk
        res["H2|" + n] = h2_medir(tk, dias, 4, n + " (TODAS)")
        for q in sorted(info.trimestre.unique()):
            dq = list(info[info.trimestre == q].dia)
            res["H2|%s|%s" % (n, q)] = h2_medir(tk[tk.trimestre == q], dq, 4, "   " + q, placebo=False)
        for t_ in (0, 1, 2):
            dq = list(info[info.vol_tercil == t_].dia)
            res["H2|%s|vol%d" % (n, t_)] = h2_medir(tk[tk.vol_tercil == t_], dq, 4, "   " + nom_vol[t_][:30], placebo=False)
        for t_ in (0, 1, 2):
            dq = list(info[info.clima_tercil == t_].dia)
            res["H2|%s|cli%d" % (n, t_)] = h2_medir(tk[tk.clima_tercil == t_], dq, 4, "   " + nom_cli[t_][:30], placebo=False)
        if n.startswith("COMB"):
            for fam in ("T7", "V4e"):
                s = tk[tk.familia == fam]
                res["H2|%s|%s" % (n, fam)] = h2_medir(s, dias, 4, "   parte " + fam, placebo=False)
        tk.to_csv(os.path.join(K.SAL, "ops-r22-%s.csv" % n), index=False)
        r = res["H2|" + n]
        K.anotar(dict(etapa="H2", familia="esqueleto 5 min", variante=n, salida=MO.VARIANTES[n][1][0][1], parametros="entrada al cierre; D1 1,6/1,0; X 0,08 x med10",
                      muestra="MES 5 min %s a %s" % (dias[0], dias[-1]), corte="todas", ruedas=len(dias), n=r.get("n"), ops_por_rueda=r.get("ops_por_rueda"),
                      acierto_pct=r.get("acierto"), neto_op_c050=r.get("c050_neto_op"), neto_op_c100=r.get("c100_neto_op"), t_rueda_c050=r.get("c050_t"),
                      t_rueda_c100=r.get("c100_t"), t_bonferroni=round(r["t_bonf"], 2), ruedas_positivas_c100=r.get("c100_ruedas_pos"),
                      placebo_p=r.get("placebo_PD"), pasa=r["pasa"], nota="pre-registrada (k = 4)"))
    W("h2", ""); W("h2", "COTAS descriptivas (NO son prueba; la calibracion mostro que la entrada a stop dentro de la vela esta sesgada para arriba con el orden supuesto y para abajo con la regla conservadora):")
    for n in MO.VARIANTES_COTA:
        tk = ops[n]; r = h2_medir(tk, dias, 4, n, placebo=False); res["COTA|" + n] = r
        tk.to_csv(os.path.join(K.SAL, "ops-r22-cota-%s.csv" % n), index=False)
        K.anotar(dict(etapa="cota", familia="descriptiva", variante=n, parametros="entrada a stop dentro de la vela", muestra="MES 5 min", corte="todas",
                      ruedas=len(dias), n=r.get("n"), acierto_pct=r.get("acierto"), neto_op_c050=r.get("c050_neto_op"), neto_op_c100=r.get("c100_neto_op"),
                      t_rueda_c050=r.get("c050_t"), t_rueda_c100=r.get("c100_t"), nota="no decide"))
    # ------------------------------------------------ robustez: f sin el trimestre, f externa (m30 2024-25), empates
    W("rob", ""); W("rob", "ROBUSTEZ (rotulada, no decide)")
    f_full, nf = curva_de_velas5()
    W("rob", "  control: la curva f(m) rehecha con el procedimiento de la R21 (%d ruedas) difiere de la congelada en max %.2e" % (nf, float(np.abs(f_full - f).max())))
    fq = {}
    for q in sorted(info.trimestre.unique()):
        fq[q], nq = curva_de_velas5(excluir=lambda d, q=q: K.trimestre(d) == q)
        W("rob", "  f sin %s: %d ruedas | f(10:00) %.3f (congelada %.3f) | f(11:00) %.3f (%.3f) | f(12:30) %.3f (%.3f)" % (
            q, nq, fq[q][29], f[29], fq[q][89], f[89], fq[q][179], f[179]))
    ctrlQ, evsQ, opsQ, infoQ = pasada(ses, H, dias, lambda d: fq[K.trimestre(d)], CONV, 5, True, MO.VARIANTES)
    ctrlQ = ctrlQ.merge(info[["dia", "trimestre"]], on="dia")
    W("rob", "  H1 con f sin el trimestre que se prueba:")
    res["ROB_fq|H1"] = h1_medir(ctrlQ, "TODAS (f sin su trimestre)", "rob", detalle=False)
    for q in sorted(ctrlQ.trimestre.unique()):
        res["ROB_fq|H1|" + q] = h1_medir(ctrlQ[ctrlQ.trimestre == q], q, "rob", detalle=False)
    W("rob", "  H2 con f sin el trimestre que se prueba:")
    for n in MO.VARIANTES:
        tk = opsQ[n].merge(info[["dia", "trimestre"]], on="dia")
        res["ROB_fq|H2|" + n] = h2_medir(tk, dias, 4, n, "rob", placebo=False)
        for q in sorted(info.trimestre.unique()):
            h2_medir(tk[tk.trimestre == q], list(info[info.trimestre == q].dia), 4, "   " + q, "rob", placebo=False)
    f30, n30 = curva_de_velas30(*K.H1B)
    W("rob", "  f EXTERNA (velas de 30 min 2024-04 a 2025-08, %d ruedas, nunca vistas por la R21): f(10:00) %.3f (congelada %.3f) | f(11:00) %.3f (%.3f) | f(12:30) %.3f (%.3f)" % (
        n30, f30[29], f[29], f30[89], f[89], f30[179], f[179]))
    ctrlX, evsX, opsX, infoX = pasada(ses, H, dias, lambda d: f30, CONV, 5, True, MO.VARIANTES)
    res["ROB_fext|H1"] = h1_medir(ctrlX, "TODAS (f externa)", "rob", detalle=False)
    for n in MO.VARIANTES:
        res["ROB_fext|H2|" + n] = h2_medir(opsX[n], dias, 4, n + " (f externa)", "rob", placebo=False)
    cv2 = dict(CONV, l2_empate="empate")
    ctrlE, _, _, _ = pasada(ses, H, dias, lambda d: f, cv2, 5, False)
    T = ctrlE.tipo == "T"
    W("rob", "  empates del primer paso (misma vela) contados como 'no extiende': %d empates de %d puntos (%d en TENDENCIA); TENDENCIA %.1f %% base %.1f %%" % (
        int((ctrlE.L2 == "empate").sum()), len(ctrlE), int(((ctrlE.L2 == "empate") & T).sum()), 100 * (ctrlE.L2 == "extiende")[T].mean(), 100 * (ctrlE.L2 == "extiende").mean()))
    for clave, etapa, var in (("H1", "H1", "D1 5 min TENDENCIA: extremo +X/2 antes que el VWAP"), ("ROB_fq|H1", "robustez", "H1 con f sin su trimestre"),
                              ("ROB_fext|H1", "robustez", "H1 con f externa (30 min 2024-25)")):
        r = res[clave]
        K.anotar(dict(etapa=etapa, familia="clima", variante=var, muestra="MES 5 min %s a %s" % (dias[0], dias[-1]),
                      corte="todas", ruedas=r["ruedas"], n=r["nT"], acierto_pct=round(100 * r["pT"], 1), pasa=r["pasa"] if etapa == "H1" else "",
                      nota="base %.1f; dif %+.1f IC %+.1f a %+.1f; sigue al cierre %.1f vs %.1f" % (100 * r["base"], 100 * r["dif"], 100 * r["ic_dif"][0],
                                                                                                   100 * r["ic_dif"][1], 100 * r["sigue_T"], 100 * r["sigue_base"])))
    for n in MO.VARIANTES:
        for clave, nota in (("ROB_fq|H2|" + n, "f sin su trimestre"), ("ROB_fext|H2|" + n, "f externa (30 min 2024-25)")):
            r = res[clave]
            K.anotar(dict(etapa="robustez", familia="esqueleto 5 min", variante=n, muestra="MES 5 min", corte="todas", ruedas=len(dias), n=r.get("n"),
                          acierto_pct=r.get("acierto"), neto_op_c050=r.get("c050_neto_op"), neto_op_c100=r.get("c100_neto_op"), t_rueda_c050=r.get("c050_t"),
                          t_rueda_c100=r.get("c100_t"), nota=nota + "; no decide"))
    # ============================================================ H1b: 30 min 2024-04 .. 2025-08
    v30 = MO.leer_velas(K.M30, K.H1B[0], K.H1B[1])
    s30 = MO.sesiones(v30); H30 = MO.Historia(s30, CONV, 30)
    c30 = H30.completas; d30 = c30[10:]
    W("h1b", "H1b - velas de 30 min %s a %s: jornadas %d, completas %d, calentamiento %s a %s, evaluadas %d. Puntos de control cada 30 min (10:00-15:30)." % (
        K.H1B[0], K.H1B[1], len(H30.dias), len(c30), c30[0], c30[9], len(d30)))
    ctrl30, _, _, info30 = pasada(s30, H30, d30, lambda d: f, CONV, 30, False)
    ctrl30 = ctrl30.merge(info30[["dia", "trimestre", "med10"]], on="dia")
    cm30 = np.quantile(info30.med10, [1 / 3, 2 / 3]); info30["vol_tercil"] = tercil(info30.med10.to_numpy(), cm30)
    ctrl30 = ctrl30.merge(info30[["dia", "vol_tercil"]], on="dia")
    ctrl30.to_parquet(os.path.join(K.SCR, "control_30min.parquet"), index=False); info30.to_csv(os.path.join(K.SAL, "ruedas_30min.csv"), index=False)
    res["H1b"] = h1_medir(ctrl30, "TODAS (30 min)", "h1b")
    W("h1b", "  por trimestre:")
    for q in sorted(ctrl30.trimestre.unique()):
        res["H1b|" + q] = h1_medir(ctrl30[ctrl30.trimestre == q], q, "h1b", detalle=False)
    W("h1b", "  por nivel de volatilidad (tercil de la mediana de 10: <= %.1f, %.1f-%.1f, > %.1f):" % (cm30[0], cm30[0], cm30[1], cm30[1]))
    for t_ in (0, 1, 2):
        res["H1b|vol%d" % t_] = h1_medir(ctrl30[ctrl30.vol_tercil == t_], "tercil %d" % t_, "h1b", detalle=False)
    r = res["H1b"]
    K.anotar(dict(etapa="H1b", familia="clima", variante="D1 30 min TENDENCIA", muestra="MES 30 min %s a %s" % (d30[0], d30[-1]), corte="todas", ruedas=r["ruedas"],
                  n=r["nT"], acierto_pct=round(100 * r["pT"], 1), pasa=r["pasa"], nota="base %.1f; dif %+.1f IC %+.1f a %+.1f" % (
                      100 * r["base"], 100 * r["dif"], 100 * r["ic_dif"][0], 100 * r["ic_dif"][1])))
    # ============================================================ salida
    for cual in L:
        open(os.path.join(K.SAL, "%s.txt" % {"h1": "h1", "h2": "h2", "h1b": "h1b", "rob": "robustez"}[cual]), "w", encoding="utf-8").write("\n".join(L[cual]))
    json.dump({k: v for k, v in res.items()}, open(os.path.join(K.SAL, "corrida.json"), "w"), indent=1, default=lambda o: o.tolist() if hasattr(o, "tolist") else str(o))
    print("total %.1f s" % (time.time() - t0))


if __name__ == "__main__":
    main()
