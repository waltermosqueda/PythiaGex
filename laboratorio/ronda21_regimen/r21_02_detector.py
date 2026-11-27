# -*- coding: utf-8 -*-
"""RONDA 21, paso 2: los 5 detectores de regimen pre-registrados (D0-D4), elegidos DENTRO de cada pliegue y juzgados afuera.
Validacion: LOPO (cada periodo afuera) y LODO con embargo +-2 ruedas. Vara del regimen: q = +y1 si TENDENCIA, -y1 si BALANCE,
0 si DUDOSO (y1 = lo que siguio el desplazamiento desde el VWAP hasta el cierre, en X), t POR RUEDA; aciertos por estado; L2; L3.
Placebo: tipos de estado sorteados entre ruedas del periodo de prueba (el lado sigue siendo el del propio dia).
Uso: python -I r21_02_detector.py  -> salidas/detector.txt, scratchpad/r21/estados_*.parquet"""
import itertools, json, math, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r21_comun as K
import numpy as np, pandas as pd
from sklearn.linear_model import LogisticRegression

SAL = os.path.join(K.AQUI, "salidas"); os.makedirs(SAL, exist_ok=True)
PC = list(range(30, 361, 5))          # puntos de control 10:00..15:30
RNG = np.random.default_rng(2121)


def preparar(df):
    d = df.copy()
    s1 = np.sign(d["c"] - d["vw"]); d["s1"] = s1
    d["lado_O"] = np.sign(d["c"] - d["O"])
    ea = d["ib_ext_arriba"].fillna(0); eb = d["ib_ext_abajo"].fillna(0)
    ext_lado = np.where(s1 > 0, ea, eb); ext_otro = np.where(s1 > 0, eb, ea)
    d["ib_un_lado_favor"] = np.where((ext_otro == 0), ext_lado, 0.0)
    d["ib_dos"] = ((ea > 0) & (eb > 0)).astype(int); d["ib_ninguno"] = ((ea == 0) & (eb == 0)).astype(int)
    d["ruido_sost"] = d["z"].abs() * d["sost"]
    d["pend_favor"] = d["vw_pend60_X"] * s1
    d["marea_favor_X"] = d["marea_pierna_X"] * np.where(d["marea_dir"] == s1, 1, -1)
    d["cvd_favor"] = (np.sign(d["cvd"]) == s1).astype(int)
    return d


def tipo_con_lado(d, T, B):
    """T, B: mascaras booleanas. Devuelve tipo 'T'/'B'/'D' con la regla del lado (T exige signo(P-VWAP) == signo(P-apertura))."""
    tipo = np.where(T, "T", np.where(B, "B", "D")).astype(object)
    mal = (tipo == "T") & ((d["s1"].to_numpy() != d["lado_O"].to_numpy()) | (d["s1"].to_numpy() == 0))
    tipo[mal] = "D"
    return tipo


# ---------------------------------------------------------------- detectores
def D0(d, p, fit=None):
    return tipo_con_lado(d, (d["ef10"] >= 0.465).to_numpy(), (d["ef10"] < 0.465).to_numpy())


def D1(d, p, fit=None):
    a, b = p
    return tipo_con_lado(d, (d["rrn"] >= a).to_numpy(), (d["rrn"] <= b).to_numpy())


def D2(d, p, fit=None):
    k = p
    az = d["z"].abs()
    return tipo_con_lado(d, ((az > 1) & (d["sost"] >= k)).to_numpy(), (az <= 1).to_numpy())


def puntos_dalton(d):
    s1 = d["s1"].to_numpy(); m = d["m"].to_numpy()
    p = np.zeros(len(d))
    rrn = d["rrn"].to_numpy(); p += (rrn >= 1.3) * 1 - (rrn <= 0.8) * 1
    v = d["vel30n"].to_numpy(); p += (v >= 1.5) * 1 - (v <= 0.7) * 1
    az = d["z"].abs().to_numpy(); p += ((az > 1) & (d["sost"].to_numpy() >= 0.8)) * 1 - (az <= 1) * 1
    ib_mas = (m >= 60) & (d["ib_un_lado_favor"].to_numpy() >= 0.5)
    ib_menos = (m >= 60) & ((d["ib_dos"].to_numpy() == 1) | ((m >= 150) & (d["ib_ninguno"].to_numpy() == 1)))
    p += ib_mas * 1 - ib_menos * 1
    vw_mas = (d["vw_lado60"].to_numpy() >= 0.9) & (d["pend_favor"].to_numpy() >= 1.0)
    p += vw_mas * 1 - (d["vw_cruces60"].to_numpy() >= 2) * 1
    ma_mas = (d["marea_pierna_X"].to_numpy() >= 6) & (d["marea_dir"].to_numpy() == s1)
    ma_menos = (d["marea_ult2_max_X"].to_numpy() < 5)
    p += ma_mas * 1 - ma_menos * 1
    ap = d["apertura"].to_numpy(); p += (ap == "fuera_rango") * 1 - (ap == "dentro_va") * 1
    cr = d["cvd_corr60"].to_numpy()
    p += ((d["cvd_favor"].to_numpy() == 1) & (np.nan_to_num(cr, nan=0) >= 0.5)) * 1 - (np.nan_to_num(cr, nan=1) <= 0) * 1
    return p


def D3(d, p, fit=None):
    khi, klo = p
    pts = d["_pts_dalton"].to_numpy()
    return tipo_con_lado(d, pts >= khi, pts <= klo)


FEAT4 = ["rrn", "vel30n", "ruido_sost", "ib_un_lado_favor", "vw_lado60", "pend_favor", "min_desde_toque", "marea_favor_X",
         "cvd_favor", "noche_rel", "ef10"]


def ajustar_logistica(dtr):
    x = dtr[FEAT4].astype(float); mu = x.mean(); sd = x.std().replace(0, 1)
    z = ((x - mu) / sd).fillna(0).to_numpy(); y = (dtr["y1"] > 0).astype(int).to_numpy()
    ok = dtr["y1"].notna().to_numpy()
    lr = LogisticRegression(C=1.0, max_iter=2000).fit(z[ok], y[ok])
    return dict(mu=mu, sd=sd, lr=lr)


def D4(d, p, fit=None):
    q = p
    z = ((d[FEAT4].astype(float) - fit["mu"]) / fit["sd"]).fillna(0).to_numpy()
    pr = fit["lr"].predict_proba(z)[:, 1]
    return tipo_con_lado(d, pr >= q, pr <= 1 - q)


DETECTORES = {"D0": (D0, [None]), "D1": (D1, [(a, b) for a in (1.2, 1.4, 1.6) for b in (0.8, 1.0)]),
              "D2": (D2, [0.6, 0.8, 1.0]), "D3": (D3, [(kh, kl) for kh in (2, 3, 4) for kl in (-1, -2, -3)]),
              "D4": (D4, [0.55, 0.60, 0.65])}
DEFAULT = {"D0": None, "D1": (1.4, 1.0), "D2": 0.8, "D3": (3, -2), "D4": 0.60}


# ---------------------------------------------------------------- vara
def q_de(d, tipo):
    y = d["y1"].to_numpy()
    q = np.where(tipo == "T", y, np.where(tipo == "B", -y, 0.0))
    return np.nan_to_num(q, nan=0.0)


def t_q(d, tipo):
    q = q_de(d, tipo)
    por = pd.Series(q).groupby(d["dia"].to_numpy()).mean()
    return K.t_por_rueda(por.to_numpy()), float(por.mean()), por


def metricas(d, tipo):
    y = d["y1"].to_numpy(); L2 = d["L2"].to_numpy()
    t, qm, por = t_q(d, tipo)
    T = tipo == "T"; B = tipo == "B"; D = tipo == "D"
    out = dict(t_q=round(t, 2), q_medio_X=round(qm, 3), ruedas=int(d["dia"].nunique()), ruedas_q_pos=int((por > 0).sum()),
               frac_T=round(float(T.mean()), 3), frac_B=round(float(B.mean()), 3), frac_D=round(float(D.mean()), 3),
               T_sigue=round(float(np.nanmean(y[T] > 0)) * 100, 1) if T.any() else None,
               T_y_medio_X=round(float(np.nanmean(y[T])), 2) if T.any() else None,
               B_vuelve=round(float(np.nanmean(y[B] < 0)) * 100, 1) if B.any() else None,
               B_y_medio_X=round(float(np.nanmean(y[B])), 2) if B.any() else None,
               base_sigue=round(float(np.nanmean(y > 0)) * 100, 1),
               T_L2_extiende=round(100 * float(np.mean(L2[T] == "extiende")), 1) if T.any() else None,
               B_L2_vuelve=round(100 * float(np.mean(L2[B] == "vuelve")), 1) if B.any() else None,
               base_L2_vuelve=round(100 * float(np.mean(L2 == "vuelve")), 1))
    # tipo de dia a las 11:00 y 12:30
    for mm, nom in ((90, "11:00"), (180, "12:30")):
        k = d["m"].to_numpy() == mm
        if k.any():
            tt = tipo[k]; l3 = d["L3"].to_numpy()[k]
            out["dia_" + nom] = dict(
                T_en_dias_tendencia="%d/%d" % (int(((tt == "T") & (l3 == "tendencia")).sum()), int((l3 == "tendencia").sum())),
                B_en_dias_balance="%d/%d" % (int(((tt == "B") & (l3 == "balance")).sum()), int((l3 == "balance").sum())),
                T_en_dias_balance="%d/%d" % (int(((tt == "T") & (l3 == "balance")).sum()), int((l3 == "balance").sum())),
                B_en_dias_tendencia="%d/%d" % (int(((tt == "B") & (l3 == "tendencia")).sum()), int((l3 == "tendencia").sum())))
    return out


def placebo(d, tipo, sorteos=500):
    dias = d["dia"].to_numpy(); ud = sorted(set(dias))
    if len(ud) < 3: return np.nan
    real = t_q(d, tipo)[1]
    idx = {u: np.nonzero(dias == u)[0] for u in ud}
    n_ok = all(len(idx[u]) == len(idx[ud[0]]) for u in ud)
    cnt = 0
    for _ in range(sorteos):
        perm = RNG.permutation(len(ud)); t2 = tipo.copy()
        for a, b in zip(ud, [ud[j] for j in perm]):
            ia, ib = idx[a], idx[b]
            if len(ia) == len(ib): t2[ia] = tipo[ib]
        # la regla del lado se re-aplica con el precio del propio dia
        mal = (t2 == "T") & ((d["s1"].to_numpy() != d["lado_O"].to_numpy()) | (d["s1"].to_numpy() == 0))
        t2[mal] = "D"
        if t_q(d, t2)[1] >= real: cnt += 1
    return round(cnt / sorteos, 3)


def elegir(nombre, dtr):
    f, grilla = DETECTORES[nombre]
    fit = ajustar_logistica(dtr) if nombre == "D4" else None
    mejor = None
    for p in grilla:
        tp = f(dtr, p, fit)
        if (tp == "D").mean() > 0.60 and nombre != "D0": continue
        t, qm, _ = t_q(dtr, tp)
        if mejor is None or t > mejor[0]: mejor = (t, p)
    if mejor is None:
        mejor = (np.nan, DEFAULT[nombre])
    return mejor[1], fit


def main():
    t0 = time.time()
    df = pd.read_parquet(os.path.join(K.SCR, "regimen_minuto.parquet"))
    d_all = preparar(df)
    d_all["_pts_dalton"] = puntos_dalton(d_all)
    d = d_all[d_all["m"].isin(PC)].reset_index(drop=True)
    dias = sorted(d["dia"].unique()); pos = {u: i for i, u in enumerate(dias)}
    lineas = []; res = {}
    def P(s=""):
        print(s, flush=True); lineas.append(s)
    P("ETAPA 1 - detector de regimen. Puntos de control cada 5 min 10:00-15:30 (%d por rueda), %d ruedas." % (len(PC), len(dias)))
    P("q = +y1 si TENDENCIA, -y1 si BALANCE, 0 si DUDOSO (y1 = lo que siguio el desplazamiento desde el VWAP hasta el cierre, en X).")
    P("Base: el desplazamiento desde el VWAP siguio hasta el cierre en el %.1f %% de los puntos (diseno %.1f, confirmacion %.1f, guardada %.1f)." % (
        100 * np.nanmean(d.y1 > 0), *[100 * np.nanmean(d[d.periodo == pp].y1 > 0) for pp in K.PERIODOS]))
    P("")
    estados_lopo = {}
    for nom in DETECTORES:
        f, grilla = DETECTORES[nom]
        P("== %s" % nom)
        tipo_pool = np.array([""] * len(d), dtype=object)
        for afuera in K.PERIODOS:
            tr = d[d.periodo != afuera]; te_m = (d.periodo == afuera).to_numpy()
            p, fit = elegir(nom, tr)
            tipo_tr = f(tr, p, fit); te = d[te_m]
            tipo_te = f(te, p, fit) if nom != "D4" else D4(te, p, fit)
            tipo_pool[te_m] = tipo_te
            mt = metricas(tr, tipo_tr); me = metricas(te, tipo_te); pl = placebo(te, tipo_te)
            # D0 en el mismo periodo de prueba, para comparar
            m0 = metricas(te, D0(te, None))
            res[(nom, afuera)] = dict(param=str(p), entrenamiento=mt, prueba=me, placebo_p=pl, D0_t=m0["t_q"])
            P("  %s afuera | parametro elegido en entrenamiento %s | entrenamiento t %+.2f q %+.3f X | PRUEBA t %+.2f q %+.3f X (%d/%d ruedas +) placebo p %.3f | D0 en prueba t %+.2f" % (
                afuera, p, mt["t_q"], mt["q_medio_X"], me["t_q"], me["q_medio_X"], me["ruedas_q_pos"], me["ruedas"], pl, m0["t_q"]))
            P("     prueba: T %.0f %% de los puntos (sigue %s %%, y %s X; L2 extiende primero %s %%) | B %.0f %% (vuelve %s %%, y %s X; L2 vuelve primero %s %%) | D %.0f %% | base sigue %.1f %%, L2 vuelve %.1f %%" % (
                100 * me["frac_T"], me["T_sigue"], me["T_y_medio_X"], me["T_L2_extiende"], 100 * me["frac_B"], me["B_vuelve"], me["B_y_medio_X"],
                me["B_L2_vuelve"], 100 * me["frac_D"], me["base_sigue"], me["base_L2_vuelve"]))
            P("     tipo de dia (prueba): a las 11:00 %s | a las 12:30 %s" % (me.get("dia_11:00"), me.get("dia_12:30")))
            K.anotar(dict(etapa="1-detector", familia="regimen", variante=nom, salida="q", parametros="grilla %s; elegido %s" % (grilla, p),
                          pliegue="LOPO %s afuera" % afuera, muestra="prueba", ruedas=me["ruedas"], n=int(len(te)),
                          t_rueda_c050=me["t_q"], placebo_p=pl, nota="q medio %.3f X; T sigue %s %%; B vuelve %s %%" % (me["q_medio_X"], me["T_sigue"], me["B_vuelve"])))
        mp = metricas(d, tipo_pool)
        res[(nom, "LOPO_juntos")] = dict(prueba=mp)
        P("  LOPO juntas (55 ruedas, cada una juzgada con el corte elegido sin ella): t %+.2f q %+.3f X (%d/%d +) | T sigue %s %% (y %s X) | B vuelve %s %% (y %s X) | D %.0f %%" % (
            mp["t_q"], mp["q_medio_X"], mp["ruedas_q_pos"], mp["ruedas"], mp["T_sigue"], mp["T_y_medio_X"], mp["B_vuelve"], mp["B_y_medio_X"], 100 * mp["frac_D"]))
        estados_lopo[nom] = tipo_pool
        # LODO con embargo +-2
        tipo_lodo = np.array([""] * len(d), dtype=object)
        for u in dias:
            i = pos[u]; veda = {dias[j] for j in range(max(0, i - 2), min(len(dias), i + 3))}
            tr = d[~d.dia.isin(veda)]; te_m = (d.dia == u).to_numpy()
            p, fit = elegir(nom, tr)
            tipo_lodo[te_m] = f(d[te_m], p, fit)
        for pp in K.PERIODOS + ("todas",):
            mk = (d.periodo == pp).to_numpy() if pp != "todas" else np.ones(len(d), bool)
            ml = metricas(d[mk], tipo_lodo[mk])
            res[(nom, "LODO_" + pp)] = dict(prueba=ml)
            P("  LODO (embargo +-2) %-9s t %+.2f q %+.3f X (%d/%d +) | T %.0f %% sigue %s %% | B %.0f %% vuelve %s %% | D %.0f %%" % (
                pp, ml["t_q"], ml["q_medio_X"], ml["ruedas_q_pos"], ml["ruedas"], 100 * ml["frac_T"], ml["T_sigue"], 100 * ml["frac_B"], ml["B_vuelve"], 100 * ml["frac_D"]))
        P("")
    # eleccion pre-registrada
    P("ELECCION (pre-registrada): mayor t de q LOPO juntas, ganandole a D0 y a su placebo (p < 0,05) en >= 2 de 3 pliegues.")
    cand = []
    for nom in DETECTORES:
        if nom == "D0": continue
        gana = sum(1 for af in K.PERIODOS if res[(nom, af)]["prueba"]["t_q"] > res[(nom, af)]["D0_t"] and res[(nom, af)]["placebo_p"] < 0.05)
        cand.append((res[(nom, "LOPO_juntos")]["prueba"]["t_q"], nom, gana))
        P("  %s: t LOPO juntas %+.2f | pliegues que le ganan a D0 y al placebo: %d de 3" % (nom, cand[-1][0], gana))
    ok = [c for c in cand if c[2] >= 2]
    elegido = max(ok)[1] if ok else max(cand)[1]
    P("  ELEGIDO: %s%s" % (elegido, "" if ok else " (rotulado 'no separa': ninguno cumplio la condicion)"))
    # estados por minuto (todas las 390) con el corte elegido en cada pliegue LOPO, para la etapa 2
    full = d_all.copy(); full["estado"] = ""; full["estado_lodo"] = ""
    f, grilla = DETECTORES[elegido]
    for afuera in K.PERIODOS:
        tr = d[d.periodo != afuera]; p, fit = elegir(elegido, tr)
        mk = (full.periodo == afuera).to_numpy()
        full.loc[mk, "estado"] = f(full[mk], p, fit)
        P("  estado LOPO %s afuera: parametro %s" % (afuera, p))
    for u in dias:
        i = pos[u]; veda = {dias[j] for j in range(max(0, i - 2), min(len(dias), i + 3))}
        tr = d[~d.dia.isin(veda)]; p, fit = elegir(elegido, tr)
        mk = (full.dia == u).to_numpy(); full.loc[mk, "estado_lodo"] = f(full[mk], p, fit)
    # parametro con TODAS las ruedas (para la especificacion en vivo)
    p_all, fit_all = elegir(elegido, d)
    full["estado_todas"] = f(full, p_all, fit_all)
    P("  parametro con las 55 ruedas (para la especificacion en vivo): %s" % (p_all,))
    full[["dia", "periodo", "m", "hora", "s1", "estado", "estado_lodo", "estado_todas", "_pts_dalton", "rrn", "z", "sost", "y1", "L2", "L3"]].to_parquet(
        os.path.join(K.SCR, "estados_minuto.parquet"), index=False)
    # todos los detectores en LOPO (para comparar en la etapa 2 si hace falta)
    out = d[["dia", "periodo", "m"]].copy()
    for nom, tp in estados_lopo.items(): out["estado_" + nom] = tp
    out.to_parquet(os.path.join(K.SCR, "estados_control_todos.parquet"), index=False)
    json.dump({"%s|%s" % k: v for k, v in res.items()}, open(os.path.join(SAL, "detector.json"), "w"), indent=1, default=str)
    open(os.path.join(SAL, "detector.txt"), "w", encoding="utf-8").write("\n".join(lineas))
    print("%.1f s" % (time.time() - t0))


if __name__ == "__main__":
    main()
