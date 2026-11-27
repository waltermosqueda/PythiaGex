# -*- coding: utf-8 -*-
"""RONDA 21, paso 4: la ESTRATEGIA por regimen (etapa 2 pre-registrada): BALANCE -> vuelta al valor (V1-V6), TENDENCIA -> continuacion
(T1-T4), DUDOSO -> esperar. Dentro de cada pliegue se elige el corte del detector y una variante por familia con las ruedas de
entrenamiento (mayor acierto con neto > 0, n >= 20); se juzga afuera. LOPO (3 cortes) y LODO con embargo +-2. Placebos PR (estado
sorteado entre ruedas) y PD (lado al azar, mismo R). Comparacion: cada familia con la llave contra la misma familia en todos los estados.
Uso: python -I r21_04_estrategia.py [D1|D3|D4]  (D1 = el detector elegido en la etapa 1; D3/D4 = robustez rotulada)"""
import json, math, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r21_comun as K
import numpy as np, pandas as pd
from scipy import stats
import r21_02_detector as DT
import finalistas as FIN

TK = K.TK
COSTOS = (0.50, 1.00)
SAL = os.path.join(K.AQUI, "salidas"); os.makedirs(SAL, exist_ok=True)
RNG = np.random.default_rng(2126)
SYM = {"S6": ("S6", "S6o"), "S1": ("S1", "S1o"), "S5": ("S5b", "S5bo"), "P3": ("P3b", "P3bo"), "S4": ("S4b", "S4bo"),
       "P1": ("P1", "P1o"), "P2": ("P2b", "P2bo"), "S6mc": ("S6mc", "S6")}
V_ENT = {"V1": FIN.e4, "V2": FIN.e5b,
         "V3": lambda df: ((df["a_favor_marea"] == 1) & (-df["vwap_rth_dist_pts"] >= df["R"])).to_numpy(),
         "V4": FIN.z_c1, "V5": FIN.m_a}
V_SAL = ("S6", "S1", "S5", "P3")
T1 = lambda df: ((df["a_favor_marea"] == 1) & (df["vwap_rth_dist_pts"] > 0)).to_numpy()
T_ENT = {"T1": T1, "T2": lambda df: T1(df) & (df["marea_con_agresores"] == 1).to_numpy(),
         "T3": lambda df: T1(df) & ((df["F6"] == 1) | (df["of_apilados_favor_3v"] >= 1)).to_numpy()}
T_SAL = ("S6", "S1", "S4", "P1", "P2")
N_VARIANTES_RONDA = 63


# ---------------------------------------------------------------- datos
def cargar():
    ev = pd.read_parquet(os.path.join(K.SCR, "eventos_r21.parquet"))
    cr = pd.read_parquet(os.path.join(K.SCR, "cruces_r21.parquet"))
    reg = pd.read_parquet(os.path.join(K.SCR, "regimen_minuto.parquet")).sort_values(["dia", "m"]).reset_index(drop=True)
    d_all = DT.preparar(reg); d_all["_pts_dalton"] = DT.puntos_dalton(d_all)
    dias = sorted(d_all["dia"].unique()); assert len(d_all) == 390 * len(dias)
    return ev, cr, d_all, dias


def filas(df, mask, var, fam, salida, fuente, invertir=False):
    sym, opp = SYM[salida]
    m = np.asarray(mask, bool) & (df["seg_rueda"] >= 1800).to_numpy() & (df["seg_rueda"] < 21600).to_numpy() & df[salida + "_pts"].notna().to_numpy()
    sub = df[m]
    lado = sub["lado"].to_numpy() * (-1 if invertir else 1)
    return pd.DataFrame(dict(var=var, fam=fam, salida=salida, dia=sub["dia"].to_numpy(), periodo=[K.periodo_de(x) for x in sub["dia"]],
                             seg=sub["seg_rueda"].to_numpy().astype(int), lado=lado,
                             R=sub["R"].to_numpy() + (TK if invertir else 0.0), entrada=sub["entrada"].to_numpy(),
                             pts=sub[salida + "_pts"].to_numpy(float), seg_out=sub[salida + "_seg_salida"].to_numpy().astype(int),
                             pts_sym=sub[sym + "_pts"].to_numpy(float), pts_opp=sub[opp + "_pts"].to_numpy(float),
                             fuente=fuente, idx=sub.index.to_numpy()))


def candidatos(ev, cr, dias):
    out = []
    for v, f in V_ENT.items():
        mk = f(ev)
        for s in V_SAL: out.append(filas(ev, mk, v, "B", s, "vueltas"))
    out.append(filas(ev, FIN.d_w6(ev), "V6", "B", "S6mc", "vueltas", invertir=True))
    for v, f in T_ENT.items():
        mk = f(ev)
        for s in T_SAL: out.append(filas(ev, mk, v, "T", s, "vueltas"))
    for s in T_SAL: out.append(filas(cr, np.ones(len(cr), bool), "T4", "T", s, "cruces"))
    c = pd.concat(out, ignore_index=True)
    di = {u: i for i, u in enumerate(dias)}
    c["dia_idx"] = c["dia"].map(di).astype(int)
    c["row"] = c["dia_idx"] * 390 + np.clip(c["seg"] // 60 - 1, 0, 389)
    c["vs"] = c["var"] + "-" + c["salida"]
    return c.sort_values(["dia_idx", "seg"], kind="mergesort").reset_index(drop=True)


# ---------------------------------------------------------------- simulacion y vara
def elegibles(c, tipo, s1, sin_llave=False):
    if sin_llave: return np.ones(len(c), bool)
    st = tipo[c["row"].to_numpy()]
    fam = c["fam"].to_numpy()
    okB = (fam == "B") & (st == "B")
    okT = (fam == "T") & (st == "T") & (c["lado"].to_numpy() == s1[c["row"].to_numpy()])
    return okB | okT


def simular(c, mask, dias_ok, max_dia=5):
    m = mask & np.isin(c["dia_idx"].to_numpy(), dias_ok)
    sub = c[m]
    di = sub["dia_idx"].to_numpy(); sg = sub["seg"].to_numpy(); so = sub["seg_out"].to_numpy(); ix = sub.index.to_numpy()
    tomadas = []; dia_act = -1; libre = -1; n = 0
    for k in range(len(sub)):
        if di[k] != dia_act: dia_act = di[k]; libre = -1; n = 0
        if sg[k] <= libre or n >= max_dia: continue
        tomadas.append(ix[k]); n += 1; libre = so[k]
    return c.loc[tomadas]


def juzgar(tk, dias_idx, col="pts"):
    n = len(tk); nd = len(dias_idx)
    out = dict(n=int(n), ruedas=int(nd), ops_por_rueda=round(n / nd, 2) if nd else 0)
    if n == 0: return out
    pts = tk[col].to_numpy(float); R = tk["R"].to_numpy(float)
    g = pts[pts > 0]; p = pts[pts <= 0]
    out.update(acierto=round(100 * len(g) / n, 1), gana_media=round(float(g.mean()), 2) if len(g) else 0.0,
               pierde_media=round(float(p.mean()), 2) if len(p) else 0.0, bruto_op=round(float(pts.mean()), 3),
               en_R=round(float((pts / R).mean()), 3), R_medio=round(float(R.mean()), 2))
    for c in COSTOS:
        net = pts - c
        por = pd.Series(net).groupby(tk["dia_idx"].to_numpy()).sum()
        serie = np.array([por.get(d, 0.0) for d in dias_idx])
        r = mx = 0
        for x in net:
            r = r + 1 if x < 0 else 0; mx = max(mx, r)
        eq = np.concatenate([[0.0], np.cumsum(net)])
        k = "c%03d" % int(round(c * 100))
        out.update({k + "_neto_op": round(float(net.mean()), 3), k + "_t": round(K.t_por_rueda(serie), 2),
                    k + "_ruedas_pos": int((serie > 0).sum()), k + "_racha": int(mx),
                    k + "_caida": round(float((np.maximum.accumulate(eq) - eq).max()), 2), k + "_por_rueda": round(float(serie.mean()), 2)})
    return out


def txt(r):
    if r.get("n", 0) == 0: return "sin operaciones"
    return "n %3d (%.2f/rueda) acierto %5.1f %% | +%.2f/%.2f | bruto %+.3f (%+.3f R) | neto c0,50 %+.3f t %+.2f (%d/%d +) racha %d caida %.1f | c1,00 %+.3f t %+.2f" % (
        r["n"], r["ops_por_rueda"], r["acierto"], r["gana_media"], r["pierde_media"], r["bruto_op"], r["en_R"], r["c050_neto_op"],
        r["c050_t"], r["c050_ruedas_pos"], r["ruedas"], r["c050_racha"], r["c050_caida"], r["c100_neto_op"], r["c100_t"])


# ---------------------------------------------------------------- procedimiento
class Pliegues:
    def __init__(s, det, c, d_all, dias):
        s.det = det; s.c = c; s.d_all = d_all; s.dias = dias
        s.dck = d_all[d_all["m"].isin(DT.PC)].reset_index(drop=True)
        s.s1 = d_all["s1"].to_numpy(); s.ladoO = d_all["lado_O"].to_numpy()
        s.f = DT.DETECTORES[det][0]

    def estados(s, dias_tr):
        p, fit = DT.elegir(s.det, s.dck[s.dck["dia"].isin(dias_tr)])
        return p, s.f(s.d_all, p, fit)

    def elegir_familia(s, fam, tipo, dias_tr_idx):
        res = []
        cf = s.c[s.c["fam"] == fam]
        for vs, cv in cf.groupby("vs", sort=True):
            mk = elegibles(cv, tipo, s.s1)
            r = juzgar(simular(cv, mk, dias_tr_idx), dias_tr_idx); r["vs"] = vs; res.append(r)
        el = [r for r in res if r["n"] >= 20 and r.get("c050_neto_op", -1) > 0]
        if not el: return None, res
        el.sort(key=lambda r: -r["acierto"])
        top = el[0]; empate = [r for r in el if top["acierto"] - r["acierto"] < 100.0 / top["n"]]
        return max(empate, key=lambda r: r["c050_t"])["vs"], res

    def correr(s, dias_tr, dias_te):
        di = {u: i for i, u in enumerate(s.dias)}
        tr = np.array([di[u] for u in dias_tr]); te = np.array([di[u] for u in dias_te])
        p, tipo = s.estados(dias_tr)
        vb, resB = s.elegir_familia("B", tipo, tr)
        vt, resT = s.elegir_familia("T", tipo, tr)
        elegidas = [v for v in (vb, vt) if v]
        cc = s.c[s.c["vs"].isin(elegidas)]
        tk = simular(cc, elegibles(cc, tipo, s.s1), te)
        return dict(param=p, vb=vb, vt=vt, tipo=tipo, tk=tk, te=te, tr=tr, resB=resB, resT=resT, cc=cc)


def placebo_PR(P, out, sorteos=200):
    """tipos de estado sorteados entre las ruedas de prueba (mismo periodo); el lado sigue siendo el del propio dia."""
    te = out["te"]; tipo = out["tipo"]; cc = out["cc"]; tk = out["tk"]
    if len(tk) == 0 or len(te) < 3: return dict(p_neto=np.nan, p_acierto=np.nan)
    real_n = float((tk["pts"] - 0.5).mean()); real_a = float((tk["pts"] > 0).mean())
    cn = ca = 0; meds = []
    for _ in range(sorteos):
        perm = RNG.permutation(len(te)); t2 = tipo.copy()
        for a, b in zip(te, te[perm]):
            t2[a * 390:(a + 1) * 390] = tipo[b * 390:(b + 1) * 390]
        mal = (t2 == "T") & ((P.s1 != P.ladoO) | (P.s1 == 0)); t2[mal] = "D"
        tk2 = simular(cc, elegibles(cc, t2, P.s1), te)
        if len(tk2) == 0: continue
        x = float((tk2["pts"] - 0.5).mean()); meds.append(x)
        cn += x >= real_n; ca += float((tk2["pts"] > 0).mean()) >= real_a
    return dict(p_neto=round(cn / sorteos, 3), p_acierto=round(ca / sorteos, 3), placebo_neto_medio=round(float(np.mean(meds)), 3) if meds else np.nan)


def placebo_PD(tk, sorteos=200):
    """mismas entradas, lado al azar, mismo R; salidas simetricas en R."""
    if len(tk) == 0: return dict(p_neto=np.nan)
    sym = tk["pts_sym"].to_numpy(float); opp = tk["pts_opp"].to_numpy(float)
    ok = np.isfinite(sym) & np.isfinite(opp)
    sym, opp = sym[ok], opp[ok]
    real = float(np.mean(sym)); real_a = float(np.mean(sym > 0)); cn = ca = 0; meds = []
    for _ in range(sorteos):
        m = RNG.random(len(sym)) < 0.5
        x = np.where(m, sym, opp); meds.append(float(x.mean()))
        cn += x.mean() >= real; ca += (x > 0).mean() >= real_a
    return dict(real_simetrica=round(real, 3), placebo_media=round(float(np.mean(meds)), 3), p_neto=round(cn / sorteos, 3),
                p_acierto=round(ca / sorteos, 3), n=int(len(sym)))


def main(det="D1"):
    t0 = time.time()
    ev, cr, d_all, dias = cargar()
    c = candidatos(ev, cr, dias)
    P = Pliegues(det, c, d_all, dias)
    lineas = []
    def W(s=""):
        print(s, flush=True); lineas.append(s)
    W("ETAPA 2 - estrategia por regimen | detector %s%s | %d candidatas (variante x evento) | %d ruedas" % (
        det, "" if det == "D1" else " (ROBUSTEZ, no decide)", len(c), len(dias)))
    W("Variantes: BALANCE %s | TENDENCIA %s" % (sorted(c[c.fam == "B"].vs.unique()), sorted(c[c.fam == "T"].vs.unique())))
    res = {}; ops_te = []
    tcrit63 = {}
    for afuera in K.PERIODOS:
        dtr = [u for u in dias if K.periodo_de(u) != afuera]; dte = [u for u in dias if K.periodo_de(u) == afuera]
        out = P.correr(dtr, dte)
        tk = out["tk"]; rt = juzgar(tk, out["te"])
        W(""); W("== LOPO: %s AFUERA (%d ruedas de prueba; entrenamiento %d ruedas) | corte del detector %s" % (afuera, len(dte), len(dtr), out["param"]))
        for fam, resf, el in (("BALANCE", out["resB"], out["vb"]), ("TENDENCIA", out["resT"], out["vt"])):
            W("  %s - entrenamiento (cada variante sola, en su estado):" % fam)
            for r in sorted(resf, key=lambda r: -r.get("acierto", 0)):
                W("     %-9s %s%s" % (r["vs"], txt(r), "   <- ELEGIDA" if r["vs"] == el else ""))
                K.anotar(dict(etapa="2-variante", familia=fam, variante=r["vs"], salida=r["vs"].split("-")[1], parametros="detector %s %s" % (det, out["param"]),
                              pliegue="LOPO %s afuera" % afuera, muestra="entrenamiento", ruedas=r["ruedas"], n=r["n"], ops_por_rueda=r["ops_por_rueda"],
                              acierto_pct=r.get("acierto"), neto_op_c050=r.get("c050_neto_op"), neto_op_c100=r.get("c100_neto_op"),
                              t_rueda_c050=r.get("c050_t"), ruedas_positivas_c050=r.get("c050_ruedas_pos")))
        W("  ELEGIDAS: BALANCE %s | TENDENCIA %s" % (out["vb"], out["vt"]))
        pr = placebo_PR(P, out); pdd = placebo_PD(tk)
        W("  PRUEBA (%s, una sola vez): %s" % (afuera, txt(rt)))
        W("     placebo PR (estado sorteado entre ruedas): p neto %s, p acierto %s (neto medio del placebo %s) | placebo PD (lado al azar, salidas simetricas): real %s contra %s, p %s, p acierto %s" % (
            pr["p_neto"], pr["p_acierto"], pr.get("placebo_neto_medio"), pdd.get("real_simetrica"), pdd.get("placebo_media"), pdd.get("p_neto"), pdd.get("p_acierto")))
        meta = rt.get("n", 0) > 0 and rt["acierto"] >= 60 and rt["c050_neto_op"] > 0
        W("     META (acierto >= 60 %% y neto > 0): %s" % ("CUMPLE" if meta else "no cumple"))
        # por familia dentro de la combinada, y cada familia sola con y sin llave
        for fam, v in (("B", out["vb"]), ("T", out["vt"])):
            if not v: W("  familia %s: no opera (ninguna variante elegible en entrenamiento)" % fam); continue
            sub = tk[tk.fam == fam]
            W("  %s dentro de la combinada: %s" % (v, txt(juzgar(sub, out["te"]))))
            cv = c[c.vs == v]
            con = juzgar(simular(cv, elegibles(cv, out["tipo"], P.s1), out["te"]), out["te"])
            sin = juzgar(simular(cv, elegibles(cv, out["tipo"], P.s1, sin_llave=True), out["te"]), out["te"])
            W("     %s sola CON la llave: %s" % (v, txt(con)))
            W("     %s sola SIN la llave: %s" % (v, txt(sin)))
            res[(afuera, "familia_" + fam)] = dict(variante=v, con_llave=con, sin_llave=sin)
        res[(afuera, "combinada")] = dict(param=str(out["param"]), vb=out["vb"], vt=out["vt"], prueba=rt, PR=pr, PD=pdd, META=bool(meta))
        K.anotar(dict(etapa="2-procedimiento", familia="combinada", variante="B:%s T:%s" % (out["vb"], out["vt"]), salida="", parametros="detector %s %s" % (det, out["param"]),
                      pliegue="LOPO %s afuera" % afuera, muestra="prueba", ruedas=rt["ruedas"], n=rt["n"], ops_por_rueda=rt["ops_por_rueda"],
                      acierto_pct=rt.get("acierto"), neto_op_c050=rt.get("c050_neto_op"), neto_op_c100=rt.get("c100_neto_op"),
                      t_rueda_c050=rt.get("c050_t"), ruedas_positivas_c050=rt.get("c050_ruedas_pos"), placebo_p=pr["p_neto"],
                      nota="PD p %s; META %s" % (pdd.get("p_neto"), meta)))
        tk = tk.assign(pliegue="LOPO_" + afuera, estado=out["tipo"][tk["row"].to_numpy()])
        ops_te.append(tk)
    lopo = pd.concat(ops_te, ignore_index=True)
    di_all = np.arange(len(dias))
    rj = juzgar(lopo, di_all)
    tcrit_k3 = stats.t.ppf(1 - 0.05 / 3, len(dias) - 1); tcrit_63 = stats.t.ppf(1 - 0.05 / N_VARIANTES_RONDA, len(dias) - 1)
    W(""); W("LOPO JUNTAS (55 ruedas, cada una juzgada por el procedimiento entrenado sin su periodo): %s" % txt(rj))
    W("   t de Bonferroni: k = 3 pliegues %.2f | 63 variantes de la ronda %.2f (una cola 5 %%, gl %d)" % (tcrit_k3, tcrit_63, len(dias) - 1))
    res[("LOPO", "juntas")] = dict(prueba=rj, t_bonf_k3=float(tcrit_k3), t_bonf_63=float(tcrit_63))
    meta3 = all(res[(a, "combinada")]["META"] for a in K.PERIODOS)
    W("   META en los 3 cortes: %s" % ("CUMPLE" if meta3 else "NO (cortes que cumplen: %s)" % [a for a in K.PERIODOS if res[(a, "combinada")]["META"]]))
    # ------------------------------------------------------------ LODO con embargo +-2 (solo con el detector elegido)
    if det == "D1":
        W(""); W("== LODO con embargo +-2 ruedas (55 pliegues; detector y variantes elegidos sin la rueda y sus vecinas)")
        tks = []; elec = {}
        for i, u in enumerate(dias):
            veda = {dias[j] for j in range(max(0, i - 2), min(len(dias), i + 3))}
            out = P.correr([x for x in dias if x not in veda], [u])
            elec[u] = (str(out["param"]), out["vb"], out["vt"])
            tks.append(out["tk"].assign(pliegue="LODO", estado=out["tipo"][out["tk"]["row"].to_numpy()]))
        lodo = pd.concat(tks, ignore_index=True)
        for pp in K.PERIODOS + ("todas",):
            di_p = np.array([i for i, u in enumerate(dias) if pp == "todas" or K.periodo_de(u) == pp])
            sub = lodo[np.isin(lodo["dia_idx"], di_p)]
            r = juzgar(sub, di_p); res[("LODO", pp)] = dict(prueba=r)
            W("  LODO %-9s %s" % (pp, txt(r)))
            for fam in ("B", "T"):
                rf = juzgar(sub[sub.fam == fam], di_p)
                W("       familia %s: %s" % (fam, txt(rf)))
        cnt = pd.Series([("B:%s T:%s" % (b, t)) for _, b, t in elec.values()]).value_counts()
        W("  elecciones LODO (variante B / T): %s" % cnt.to_dict())
        lodo.to_csv(os.path.join(SAL, "ops-r21-LODO-%s.csv" % det), index=False)
    lopo.to_csv(os.path.join(SAL, "ops-r21-LOPO-%s.csv" % det), index=False)
    json.dump({"%s|%s" % k: v for k, v in res.items()}, open(os.path.join(SAL, "estrategia-%s.json" % det), "w"), indent=1, default=str)
    open(os.path.join(SAL, "estrategia-%s.txt" % det), "w", encoding="utf-8").write("\n".join(lineas))
    W("%.1f s" % (time.time() - t0))


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "D1")
