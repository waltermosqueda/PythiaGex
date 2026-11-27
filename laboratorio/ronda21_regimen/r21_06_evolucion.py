# -*- coding: utf-8 -*-
"""RONDA 21, paso 6: ETAPA 3 "evolucion mecanica" (pre-registrada; DISENADA DESPUES de ver la etapa 2: sus numeros por periodo NO son
validacion). Mapa: TENDENCIA (rrn >= 1,6) -> continuacion; ROTACION (1,0 < rrn < 1,6) -> banda de 2 sigma (V4); ESTRECHO (<= 1,0) -> esperar.
Reglas fijas por periodo + placebos PR y PD + eleccion anidada LOPO + estados LODO + GEX de la ronda 20 + mecanica por orden y hora.
Uso: python -I r21_06_evolucion.py"""
import json, math, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r21_comun as K
import numpy as np, pandas as pd
import r21_04_estrategia as ES

SAL = os.path.join(K.AQUI, "salidas")
RNG = np.random.default_rng(2131)
GEX = os.path.join(os.path.dirname(K.SCR), "r20", "gex_minuto.parquet")


def candidatos3(ev, cr, dias):
    T1 = ES.T1(ev)
    nivel = (ev["n_niveles_en_zona"] >= 1).to_numpy(); barr = (ev["n_barridas_y_vuelta"] >= 1).to_numpy()
    d300 = (ev["of_delta_300"] > 0).to_numpy()
    ent = {"T1": T1, "T5": T1 & nivel, "T6": T1 & d300, "T7": T1 & (nivel | barr | d300)}
    out = []
    for v, mk in ent.items():
        for s in ("S6", "P1"): out.append(ES.filas(ev, mk, v, "T", s, "vueltas"))
    out.append(ES.filas(ev, ES.FIN.z_c1(ev), "V4", "R", "S6", "vueltas"))
    c = pd.concat(out, ignore_index=True)
    di = {u: i for i, u in enumerate(dias)}
    c["dia_idx"] = c["dia"].map(di).astype(int)
    c["row"] = c["dia_idx"] * 390 + np.clip(c["seg"] // 60 - 1, 0, 389)
    c["vs"] = c["var"] + "-" + c["salida"]
    return c.sort_values(["dia_idx", "seg"], kind="mergesort").reset_index(drop=True)


def elegibles3(c, tipo, s1, gex=None, filtro_gex=None):
    st = tipo[c["row"].to_numpy()]; fam = c["fam"].to_numpy()
    ok = ((fam == "T") & (st == "T") & (c["lado"].to_numpy() == s1[c["row"].to_numpy()])) | ((fam == "R") & (st == "D"))
    if filtro_gex is not None:
        g = gex[c["row"].to_numpy()]
        ok &= (g == filtro_gex)
    return ok


ESTRATEGIAS = {  # nombre: (lista de vs, filtro gex por familia)
    "T5-S6": (["T5-S6"], None), "T5-P1": (["T5-P1"], None), "T6-S6": (["T6-S6"], None), "T6-P1": (["T6-P1"], None),
    "T7-S6": (["T7-S6"], None), "T7-P1": (["T7-P1"], None), "V4-S6-ROT": (["V4-S6"], None), "COMB-T7+V4": (["T7-S6", "V4-S6"], None),
    "T7-S6-gneg": (["T7-S6"], "neg"), "V4-S6-ROT-gpos": (["V4-S6"], "pos"),
    "T1-S6 (control)": (["T1-S6"], None), "T1-P1 (control)": (["T1-P1"], None)}


def correr(c, nombre, tipo, s1, gex, dias_idx):
    vss, fg = ESTRATEGIAS[nombre]
    cc = c[c["vs"].isin(vss)]
    return cc, ES.simular(cc, elegibles3(cc, tipo, s1, gex, fg), dias_idx)


def placebo_PR3(c, nombre, tipo, s1, ladoO, gex, te, tk, sorteos=200):
    if len(tk) == 0 or len(te) < 3: return dict(p_neto=np.nan, p_acierto=np.nan)
    vss, fg = ESTRATEGIAS[nombre]; cc = c[c["vs"].isin(vss)]
    real_n = float((tk["pts"] - 0.5).mean()); real_a = float((tk["pts"] > 0).mean()); cn = ca = 0; meds = []
    for _ in range(sorteos):
        perm = RNG.permutation(len(te)); t2 = tipo.copy()
        for a, b in zip(te, te[perm]): t2[a * 390:(a + 1) * 390] = tipo[b * 390:(b + 1) * 390]
        mal = (t2 == "T") & ((s1 != ladoO) | (s1 == 0)); t2[mal] = "D"
        tk2 = ES.simular(cc, elegibles3(cc, t2, s1, gex, fg), te)
        if len(tk2) == 0: continue
        x = float((tk2["pts"] - 0.5).mean()); meds.append(x); cn += x >= real_n; ca += float((tk2["pts"] > 0).mean()) >= real_a
    return dict(p_neto=round(cn / sorteos, 3), p_acierto=round(ca / sorteos, 3), placebo_neto=round(float(np.mean(meds)), 3) if meds else np.nan)


def main():
    t0 = time.time()
    ev, cr, d_all, dias = ES.cargar()
    est = pd.read_parquet(os.path.join(K.SCR, "estados_minuto.parquet")).sort_values(["dia", "m"]).reset_index(drop=True)
    assert (est["dia"].to_numpy() == d_all["dia"].to_numpy()).all()
    tipo = est["estado"].to_numpy().astype(object); tipo_lodo = est["estado_lodo"].to_numpy().astype(object)
    s1 = d_all["s1"].to_numpy(); ladoO = d_all["lado_O"].to_numpy()
    # GEX de la ronda 20 (regimen en el minuto; valido con dato <= 60 min)
    g = pd.read_parquet(GEX)[["dia", "minuto", "regimen", "edad_s", "hay_cadena"]]
    g = g[(g["hay_cadena"] == True) & (g["edad_s"] <= 3600)]
    gm = d_all[["dia", "m"]].merge(g.rename(columns={"minuto": "m"}), on=["dia", "m"], how="left")
    gex = gm["regimen"].fillna("sin").to_numpy().astype(object)
    c = candidatos3(ev, cr, dias)
    L = []
    def W(s=""):
        print(s, flush=True); L.append(s)
    W("ETAPA 3 (post hoc, NO es validacion) - TENDENCIA rrn>=1,6 / ROTACION 1,0-1,6 / ESTRECHO <=1,0 | %d candidatas" % len(c))
    per_idx = {pp: np.array([i for i, u in enumerate(dias) if K.periodo_de(u) == pp]) for pp in K.PERIODOS}
    todos = np.arange(len(dias)); res = {}; ops = []
    for nombre in ESTRATEGIAS:
        W(""); W("== %s" % nombre)
        meta = []
        for pp in K.PERIODOS + ("todas",):
            te = per_idx[pp] if pp != "todas" else todos
            cc, tk = correr(c, nombre, tipo, s1, gex, te)
            r = ES.juzgar(tk, te)
            extra = ""
            if pp != "todas" and r.get("n", 0) > 0:
                pr = placebo_PR3(c, nombre, tipo, s1, ladoO, gex, te, tk); pdd = ES.placebo_PD(tk)
                extra = " | PR p %s (acierto p %s) | PD p %s" % (pr["p_neto"], pr["p_acierto"], pdd.get("p_neto"))
                meta.append(r["acierto"] >= 60 and r["c050_neto_op"] > 0)
                res[(nombre, pp)] = dict(r, PR=pr, PD=pdd)
            elif pp != "todas":
                meta.append(False); res[(nombre, pp)] = r
            else:
                res[(nombre, pp)] = r
            W("   %-9s %s%s" % (pp, ES.txt(r), extra))
            if pp != "todas": ops.append(tk.assign(estrategia=nombre, estado=tipo[tk["row"].to_numpy()], gex=gex[tk["row"].to_numpy()]))
        W("   META en los 3 periodos: %s" % ("CUMPLE" if all(meta) else "no (%s)" % "".join("+" if m else "-" for m in meta)))
        res[(nombre, "META")] = bool(all(meta))
        K.anotar(dict(etapa="3-evolucion", familia="post hoc", variante=nombre, salida="", parametros="TENDENCIA rrn>=1,6 / ROTACION 1,0-1,6",
                      pliegue="por periodo (no validacion)", muestra="55 ruedas", ruedas=len(dias), n=res[(nombre, "todas")].get("n"),
                      acierto_pct=res[(nombre, "todas")].get("acierto"), neto_op_c050=res[(nombre, "todas")].get("c050_neto_op"),
                      neto_op_c100=res[(nombre, "todas")].get("c100_neto_op"), t_rueda_c050=res[(nombre, "todas")].get("c050_t"),
                      ruedas_positivas_c050=res[(nombre, "todas")].get("c050_ruedas_pos"), nota="META 3 periodos %s" % res[(nombre, "META")]))
    # ---------------- division por signo de gamma (descriptiva)
    W(""); W("GEX (ronda 20, dato <= 60 min): division por signo de gamma en el minuto de la entrada (evento por evento, sin secuencia)")
    for vs, fam_estado in (("T1-S6", "T"), ("T7-S6", "T"), ("V4-S6", "D")):
        cc = c[c.vs == vs]; st = tipo[cc["row"].to_numpy()]
        ok = (st == fam_estado) & ((cc["fam"].to_numpy() != "T") | (cc["lado"].to_numpy() == s1[cc["row"].to_numpy()]))
        sub = cc[ok]; gg = gex[sub["row"].to_numpy()]
        for pp in K.PERIODOS + ("todas",):
            mk = np.ones(len(sub), bool) if pp == "todas" else (sub["periodo"].to_numpy() == pp)
            partes = []
            for s_ in ("neg", "pos", "sin"):
                m2 = mk & (gg == s_)
                partes.append("%s n %3d %s" % (s_, int(m2.sum()), ("%.1f %%" % (100 * (sub["pts"].to_numpy()[m2] > 0).mean())) if m2.sum() else "-"))
            W("   %-6s en %s %-9s %s" % (vs, fam_estado, pp, " | ".join(partes)))
    # ---------------- eleccion anidada LOPO entre las 8 sin GEX
    W(""); W("ELECCION ANIDADA LOPO (mayor acierto con neto > 0 y n >= 20 en los dos periodos de entrenamiento)")
    ocho = ["T5-S6", "T5-P1", "T6-S6", "T6-P1", "T7-S6", "T7-P1", "V4-S6-ROT", "COMB-T7+V4"]
    tks = []
    for afuera in K.PERIODOS:
        tr = np.concatenate([per_idx[p] for p in K.PERIODOS if p != afuera]); te = per_idx[afuera]
        cands = []
        for nombre in ocho:
            _, tk = correr(c, nombre, tipo, s1, gex, tr); r = ES.juzgar(tk, tr)
            if r.get("n", 0) >= 20 and r.get("c050_neto_op", -1) > 0: cands.append((r["acierto"], r["c050_t"], nombre, r))
        if not cands: W("  %s afuera: ninguna elegible" % afuera); continue
        cands.sort(key=lambda z: (-z[0], -z[1])); top = cands[0]
        emp = [z for z in cands if top[0] - z[0] < 100.0 / top[3]["n"]]; el = max(emp, key=lambda z: z[1])
        _, tk = correr(c, el[2], tipo, s1, gex, te); r = ES.juzgar(tk, te); tks.append(tk)
        W("  %-9s afuera: elegida %s (entrenamiento %.1f %%, n %d) -> PRUEBA %s" % (afuera, el[2], el[0], el[3]["n"], ES.txt(r)))
    if tks:
        W("  juntas: %s" % ES.txt(ES.juzgar(pd.concat(tks), todos)))
    # ---------------- estados LODO de D1 para las reglas fijas
    W(""); W("REGLAS FIJAS con los estados LODO de D1 (corte elegido sin la rueda y sus vecinas)")
    for nombre in ("T7-S6", "T5-S6", "COMB-T7+V4", "T1-S6 (control)"):
        partes = []
        for pp in K.PERIODOS + ("todas",):
            te = per_idx[pp] if pp != "todas" else todos
            _, tk = correr(c, nombre, tipo_lodo, s1, gex, te); r = ES.juzgar(tk, te)
            partes.append("%s %s" % (pp, ("n %d %.1f %% %+.2f" % (r["n"], r["acierto"], r["c050_neto_op"])) if r.get("n") else "n 0"))
        W("   %-16s %s" % (nombre, " | ".join(partes)))
    # ---------------- mecanica: T1-S6 en TENDENCIA por orden y por hora
    W(""); W("MECANICA: T1-S6 y T7-S6 en TENDENCIA (secuenciadas, 55 ruedas) por orden de la operacion en la rueda y por hora de entrada")
    for nombre in ("T1-S6 (control)", "T7-S6"):
        _, tk = correr(c, nombre, tipo, s1, gex, todos)
        tk = tk.copy(); tk["orden"] = tk.groupby("dia_idx").cumcount() + 1
        tk["franja"] = pd.cut(tk["seg"], [1800, 3600, 7200, 12600, 18000, 21600], labels=["10:00-10:30", "10:30-11:30", "11:30-13:00", "13:00-14:30", "14:30-15:30"], right=False)
        W("  %s:" % nombre)
        for k_, g_ in tk.groupby("orden"):
            W("     operacion %d de la rueda: n %3d acierto %5.1f %% neto c0,50 %+.2f" % (k_, len(g_), 100 * (g_.pts > 0).mean(), (g_.pts - 0.5).mean()))
        for k_, g_ in tk.groupby("franja", observed=True):
            W("     %s: n %3d acierto %5.1f %% neto c0,50 %+.2f" % (k_, len(g_), 100 * (g_.pts > 0).mean(), (g_.pts - 0.5).mean()))
    pd.concat(ops, ignore_index=True).to_csv(os.path.join(SAL, "ops-r21-etapa3.csv"), index=False)
    json.dump({"%s|%s" % k: v for k, v in res.items()}, open(os.path.join(SAL, "etapa3.json"), "w"), indent=1, default=str)
    open(os.path.join(SAL, "etapa3.txt"), "w", encoding="utf-8").write("\n".join(L))
    W("%.1f s" % (time.time() - t0))


if __name__ == "__main__":
    main()
