# -*- coding: utf-8 -*-
"""RONDA 21, paso 10: la regla CONGELADA COMB-T7+V4 en MNQ (muestra nueva). Plomeria nueva (rutas, roll, lista de ruedas) alrededor del
pipeline congelado SIN tocarlo: r19_01_rasgos.jornada, r19_08_eventos.eventos_dia, r21_01_regimen.rasgos_dia y regla_congelada_comb_t7_v4.
Verifica las huellas SHA-256 antes de correr. Ver PREREGISTRO.md, enmienda 5.
Uso: python -I r21_10_mnq_prueba.py"""
import json, math, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r21_comun as K
import numpy as np, pandas as pd
import r19_comun as C19
import r19_01_rasgos as RS
import r19_02_motor as M
import r19_08_eventos as EV
import r21_01_regimen as RG1
import regla_congelada_comb_t7_v4 as RG

TK = 0.25
LAB = os.path.dirname(K.AQUI)
MNQ = os.path.join(K.SCR, "mnq")
DIRS = {k: os.path.join(MNQ, k) for k in ("cinta", "rasgos", "hist")}
for d_ in DIRS.values(): os.makedirs(d_, exist_ok=True)
SPREAD_MNQ = 298.00            # Z6 - U6 medido el 14-09 (r21_09_mnq_inventario.py, 76 velas)
ULTIMO_U6 = "2026-09-14"
PRIMERA = "2026-07-31"
EXCLUIDAS = {"2026-09-17": "cinta incompleta (termina 15:41 NY)"}
N_CALENTAMIENTO = 10
COSTOS = (0.50, 1.00)
RNG = np.random.default_rng(2140)
SAL = os.path.join(K.AQUI, "salidas")

# --- rutas del pipeline apuntadas a MNQ (sin tocar los archivos congelados)
C19.CINTA = DIRS["cinta"]; RS.RAS = DIRS["rasgos"]; RS.HIST = DIRS["hist"]; M.RAS = DIRS["rasgos"]
RS.VMIN_APILADO["MNQ"] = 30.0   # agregado declarado: los apilados no entran en T7 ni en V4


def verificar_huellas():
    h = json.load(open(os.path.join(K.AQUI, "regla_congelada_huellas.json")))["sha256"]
    mal = [a for a, v in h.items() if RG.sha256(os.path.join(LAB, a)) != v]
    if mal: raise SystemExit("HUELLAS DISTINTAS (no se corre): %s" % mal)
    return len(h)


def contrato(dd):
    return "U6" if dd <= ULTIMO_U6 else "Z6"


def velas_nq():
    v = pd.read_csv(os.path.join(LAB, "dom", "ronda3", "velas", "velas-NQ-m5.csv"))
    v["t"] = pd.to_datetime(v["t"])
    v["ny"] = v["t"] - pd.Timedelta(hours=4)                         # julio: horario de verano
    v["rueda"] = (v["ny"] + pd.Timedelta(hours=6)).dt.strftime("%Y-%m-%d")
    v["hm"] = v["ny"].dt.hour + v["ny"].dt.minute / 60.0
    return v


def dias_cinta():
    """Ruedas con cinta compactada Y con ordenes en la rueda (el 22-09 es solo noche: no es rueda)."""
    inv = pd.read_csv(os.path.join(SAL, "mnq_inventario.csv"))
    con_rth = set(inv.loc[inv["rth_o"].notna(), "dia"])
    return sorted(d for d in (os.path.basename(f)[4:-8] for f in __import__("glob").glob(os.path.join(DIRS["cinta"], "MNQ-*.parquet"))) if d in con_rth)


def armar_rasgos():
    """Orquestador (copia de la logica de r19_01_rasgos.main, con las claves de MNQ y su roll)."""
    RS.velas_m5 = velas_nq
    m5 = {r: dd for r, dd in RS.preparar_historia_m5().items() if r < PRIMERA}
    resumen, perfiles, ruidos = {}, {}, {}
    for r, dd in m5.items():
        if "rth_h" in dd:
            resumen[r] = {kk: dd[kk] for kk in ("rth_h", "rth_l", "rth_c", "rth_o")}
            resumen[r].update(RS.va_de(dd["perfil_rth"])); ruidos[r] = dd["ruido"]
        perfiles[r] = dd["perfil_eth"]
        resumen.setdefault(r, {}).update({"eth_pv": dd["eth_pv"], "eth_p2v": dd["eth_p2v"], "eth_v": dd["eth_v"], "contrato": "U6", "fuente": "velas NQ 5 min"})
    print("historia de velas NQ:", sorted(m5.keys()), flush=True)
    t0 = time.time()
    for dia in dias_cinta():
        K.frenar_si_falta_ram(2.0)
        previas = sorted(d for d, v in resumen.items() if "rth_h" in v and d < dia)
        X, med = RS.X_de([resumen[p]["rth_h"] - resumen[p]["rth_l"] for p in previas])
        pd_dia = previas[-1]; prev = dict(resumen[pd_dia])
        aj = SPREAD_MNQ if (contrato(pd_dia) == "U6" and contrato(dia) == "Z6") else 0.0
        for kk in ("rth_h", "rth_l", "rth_c", "poc", "vah", "val"): prev[kk] = prev[kk] + aj
        sem = pd.Timestamp(dia).isocalendar()[:2]
        todas = sorted(set(resumen) | set(perfiles))
        sem_prev = [dd for dd in todas if dd < dia and tuple(pd.Timestamp(dd).isocalendar()[:2]) == (sem[0], sem[1] - 1)]
        ps = [perfiles[dd] for dd in sem_prev if dd in perfiles]
        sh = [int(round((SPREAD_MNQ if (contrato(dd) == "U6" and contrato(dia) == "Z6") else 0.0) / TK)) for dd in sem_prev if dd in perfiles]
        sem_prev_perfil = RS.sumar_perfiles(ps, sh) if ps else None
        def carry(lst):
            pv = p2v = v = 0.0
            for dd in lst:
                rr = resumen.get(dd)
                if not rr or "eth_v" not in rr: continue
                a = SPREAD_MNQ if (contrato(dd) == "U6" and contrato(dia) == "Z6") else 0.0
                pv += rr["eth_pv"] + a * rr["eth_v"]; p2v += rr["eth_p2v"] + 2 * a * rr["eth_pv"] + a * a * rr["eth_v"]; v += rr["eth_v"]
            return {"pv": pv, "p2v": p2v, "v": v}
        c_sem = carry([dd for dd in todas if dd < dia and tuple(pd.Timestamp(dd).isocalendar()[:2]) == tuple(sem)])
        c_mes = carry([dd for dd in todas if dd < dia and dd[:7] == dia[:7]])
        prev_mismo = sorted(os.path.basename(f)[len("mues-MNQ-"):-4] for f in __import__("glob").glob(os.path.join(DIRS["hist"], "mues-MNQ-*.npz")))
        prev_mismo = [p for p in prev_mismo if p < dia][-10:]
        mp = RS.cargar_muestras("MNQ", prev_mismo)
        rp = [ruidos[d] for d in sorted(ruidos) if d < dia][-10:]
        R, meta, hoy, mues = RS.jornada("MNQ", dia, X, prev, mp, c_sem, c_mes, rp, sem_prev_perfil, 1.0)
        meta.update({"mediana_rango_10": med, "dia_previo": pd_dia, "ajuste_roll_previo": aj, "semana_previa": sem_prev, "umbral_dias": prev_mismo,
                     "contrato": contrato(dia)})
        RS.guardar("MNQ", dia, R, meta, mues)
        resumen[dia] = {x: hoy[x] for x in ("rth_h", "rth_l", "rth_c", "rth_o", "poc", "vah", "val", "eth_pv", "eth_p2v", "eth_v")}
        resumen[dia]["contrato"] = contrato(dia); perfiles[dia] = hoy["perfil_eth"]; ruidos[dia] = hoy["ruido"]
        print("MNQ %s X %.2f (mediana %.2f) previa %s aj %.2f | umbral con %d ruedas | %.1f s | RAM %.1f GB" % (
            dia, X, med, pd_dia, aj, len(mp), time.time() - t0, K.ram_libre_gb()), flush=True)
    json.dump({d: {k: v for k, v in r.items() if not isinstance(v, (dict, np.ndarray))} for d, r in resumen.items()},
              open(os.path.join(DIRS["hist"], "resumen_mnq.json"), "w"))
    return resumen


def armar_eventos_y_regimen(resumen, evaluadas):
    ef = {}
    rows = sorted((d, abs(v["rth_c"] - v["rth_o"]) / (v["rth_h"] - v["rth_l"])) for d, v in resumen.items() if "rth_h" in v and v["rth_h"] > v["rth_l"])
    eff = pd.Series([e for _, e in rows], index=[d for d, _ in rows])
    ef10 = eff.rolling(10).median().shift(1).to_dict()
    cur = json.load(open(RG.CURVA)); f = np.array(cur["f"]); g = np.array(cur["g"])
    evs, regs = [], []
    orig = M.cargar
    for dia in evaluadas:
        K.frenar_si_falta_ram(2.0)
        evs.append(pd.DataFrame(EV.eventos_dia("MNQ", dia, "mnq", ef10.get(dia, np.nan))))
        M.cargar = lambda inst, d_: orig("MNQ", d_)          # rasgos_dia de la R21 pide "ES": se le sirve la de MNQ
        try:
            regs.append(RG1.rasgos_dia(dia, f, g, ef10, resumen).assign(periodo="mnq"))
        finally:
            M.cargar = orig
        print("eventos y regimen", dia, len(evs[-1]), flush=True)
    ev = pd.concat(evs, ignore_index=True); reg = pd.concat(regs, ignore_index=True)
    ev.to_parquet(os.path.join(MNQ, "eventos_mnq.parquet"), index=False); reg.to_parquet(os.path.join(MNQ, "regimen_mnq.parquet"), index=False)
    return ev, reg


# ---------------------------------------------------------------- vara
def juzgar(op, dias, col="resultado_pts"):
    n = len(op); nd = len(dias); out = dict(n=n, ruedas=nd, ops_por_rueda=round(n / nd, 2) if nd else 0)
    if n == 0: return out
    pts = op[col].to_numpy(float); R = op["R"].to_numpy(float)
    g = pts[pts > 0]; p = pts[pts <= 0]
    out.update(acierto=round(100 * len(g) / n, 1), gana_media=round(float(g.mean()), 2) if len(g) else 0.0,
               pierde_media=round(float(p.mean()), 2) if len(p) else 0.0, bruto=round(float(pts.mean()), 3), en_R=round(float((pts / R).mean()), 3),
               R_medio=round(float(R.mean()), 2))
    for c in COSTOS:
        net = pts - c; por = pd.Series(net).groupby(op["dia"].to_numpy()).sum()
        serie = np.array([por.get(d, 0.0) for d in dias])
        k = "c%03d" % int(round(c * 100))
        out.update({k + "_neto": round(float(net.mean()), 3), k + "_t": round(K.t_por_rueda(serie), 2), k + "_ruedas_pos": int((serie > 0).sum())})
    return out


def txt(r):
    if r.get("n", 0) == 0: return "sin operaciones"
    return "n %3d (%.2f/rueda) acierto %5.1f %% | +%.2f/%.2f | bruto %+.3f (%+.3f R, R medio %.1f) | neto c0,50 %+.3f t %+.2f (%d/%d +) | c1,00 %+.3f t %+.2f (%d/%d +)" % (
        r["n"], r["ops_por_rueda"], r["acierto"], r["gana_media"], r["pierde_media"], r["bruto"], r["en_R"], r["R_medio"], r["c050_neto"], r["c050_t"],
        r["c050_ruedas_pos"], r["ruedas"], r["c100_neto"], r["c100_t"], r["c100_ruedas_pos"], r["ruedas"])


def lado_contrario(op):
    """1R del lado contrario con el mismo R, desde la cinta (placebo PD)."""
    out = np.full(len(op), np.nan)
    for dia, g in op.groupby("dia"):
        d = M.Dia("MNQ", dia)
        for k, (i, r) in enumerate(g.iterrows()):
            o = -int(r["lado"]); E = float(r["entrada"]); R_ = float(r["R"])
            ts, px, mot = M.salida(d, int(r["seg_rueda"]), o, E, E - o * R_, "obj", E + o * R_)
            out[op.index.get_loc(i)] = (px - E) * o
    return out


def placebo_PD(op, opp, sorteos=2000):
    sym = op["resultado_pts"].to_numpy(float); ok = np.isfinite(opp)
    real = float(sym[ok].mean()); real_a = float((sym[ok] > 0).mean()); cn = ca = 0; meds = []
    for _ in range(sorteos):
        m = RNG.random(ok.sum()) < 0.5; x = np.where(m, sym[ok], opp[ok]); meds.append(x.mean())
        cn += x.mean() >= real; ca += (x > 0).mean() >= real_a
    return dict(real=round(real, 3), placebo=round(float(np.mean(meds)), 3), p_neto=round(cn / sorteos, 4), p_acierto=round(ca / sorteos, 4))


def operar_con_tipo(ev, reg, tipo_por_dia, familias=("T7", "V4"), t7_mask=None, v4_mask=None):
    """La misma regla con los estados dados por dia (para el placebo PR y la lectura descriptiva). No cambia parametros."""
    ev = ev.sort_values(["dia", "seg_rueda", "t_utc_ms"], kind="mergesort").reset_index(drop=True)
    r = reg.set_index(["dia", "m"])
    m_prev = np.clip(ev["seg_rueda"].to_numpy() // 60 - 1, 0, 389)
    st = r.loc[list(zip(ev["dia"], m_prev)), ["c", "vw", "O"]]
    lvw = np.sign(st["c"].to_numpy() - st["vw"].to_numpy()); lo = np.sign(st["c"].to_numpy() - st["O"].to_numpy())
    tipo = np.array([tipo_por_dia[d][m] for d, m in zip(ev["dia"], m_prev)], dtype=object)
    tipo = np.where((tipo == "T") & ~((lvw == lo) & (lvw != 0)), "N", tipo)
    t7, v4 = RG.mascaras(ev)
    if t7_mask is not None: t7 = t7_mask(ev)
    if v4_mask is not None: v4 = v4_mask(ev)
    e7 = t7 & (tipo == "T") & (ev["lado"].to_numpy() == lvw) if "T7" in familias else np.zeros(len(ev), bool)
    e4 = v4 & (tipo == "R") if "V4" in familias else np.zeros(len(ev), bool)
    hora = (ev["seg_rueda"] >= RG.HORA_INI).to_numpy() & (ev["seg_rueda"] < RG.HORA_FIN).to_numpy() & ev["S6_pts"].notna().to_numpy()
    ok = (e7 | e4) & hora
    sen = ev[ok].copy(); sen["familia"] = np.where(e7[ok], "T7", "V4")
    return RG.operar(sen)


def tipos_por_dia(reg):
    out = {}
    for dia, g in reg.groupby("dia"):
        g = g.sort_values("m")
        t, _ = RG.estado_minuto(g["rrn"].to_numpy(), g["c"].to_numpy(), g["vw"].to_numpy(), g["O"].to_numpy())
        # la regla de lados se re-aplica con el precio del propio dia en operar_con_tipo; aca va el tipo "crudo" por rrn
        crudo = np.where(np.isfinite(g["rrn"].to_numpy()) & (g["rrn"].to_numpy() >= RG.CORTE_TENDENCIA), "T", t)
        out[dia] = crudo
    return out


def main():
    t0 = time.time()
    print("huellas verificadas:", verificar_huellas(), "archivos", flush=True)
    resumen = armar_rasgos()
    cinta = dias_cinta()
    evaluadas = [d for d in cinta[N_CALENTAMIENTO:] if d not in EXCLUIDAS]
    print("calentamiento:", cinta[:N_CALENTAMIENTO]); print("evaluadas (%d):" % len(evaluadas), evaluadas); print("excluidas:", EXCLUIDAS, flush=True)
    ev, reg = armar_eventos_y_regimen(resumen, evaluadas)
    op = RG.correr(ev, reg)
    op["opuesto_pts"] = lado_contrario(op)
    op.to_csv(os.path.join(SAL, "ops-MNQ-COMB-T7+V4.csv"), index=False)
    L = []
    def W(s=""):
        print(s, flush=True); L.append(s)
    W("REGLA CONGELADA COMB-T7+V4 EN MNQ (muestra nueva) | %d ruedas evaluadas (14-08 a 21-09, sin el 17-09) | costo en puntos de NQ" % len(evaluadas))
    W("X mediano %.2f pts de NQ; R medio de las operaciones %.2f" % (np.median([json.load(open(os.path.join(DIRS["rasgos"], "MNQ-%s.json" % d)))["X"] for d in evaluadas]),
                                                                     op["R"].mean() if len(op) else np.nan))
    antes = [d for d in evaluadas if d < "2026-09-10"]; despues = [d for d in evaluadas if d >= "2026-09-10"]
    res = {}
    for nom, dias in (("las %d evaluadas" % len(evaluadas), evaluadas), ("antes del 10-09 (%d)" % len(antes), antes), ("10-09 a 21-09 (%d)" % len(despues), despues)):
        W(""); W("== %s" % nom)
        sub = op[op["dia"].isin(dias)]
        for fam in ("juntas", "T7", "V4"):
            s2 = sub if fam == "juntas" else sub[sub["familia"] == fam]
            r = juzgar(s2, dias); res[(nom, fam)] = r
            W("   %-6s %s" % (fam, txt(r)))
    # placebos sobre las evaluadas
    W(""); W("PLACEBOS (las %d evaluadas)" % len(evaluadas))
    for fam in ("juntas", "T7", "V4"):
        s2 = op if fam == "juntas" else op[op["familia"] == fam]
        if len(s2) == 0: continue
        pdd = placebo_PD(s2, s2["opuesto_pts"].to_numpy(float))
        W("   PD (lado al azar, mismo R, 1R) %-6s real %+.3f contra %+.3f | p neto %.4f, p acierto %.4f" % (fam, pdd["real"], pdd["placebo"], pdd["p_neto"], pdd["p_acierto"]))
        res[("PD", fam)] = pdd
    tpd = tipos_por_dia(reg)
    real_n = float((op["resultado_pts"] - 0.5).mean()) if len(op) else np.nan; real_a = float((op["resultado_pts"] > 0).mean()) if len(op) else np.nan
    chk = operar_con_tipo(ev, reg, tpd)
    W("   control: la regla con los estados propios por la via del placebo da %d operaciones (la congelada %d), mismo resultado %s" % (
        len(chk), len(op), bool(len(chk) == len(op) and np.allclose(np.sort(chk["resultado_pts"].to_numpy()), np.sort(op["resultado_pts"].to_numpy())))))
    meds = []; cn = ca = 0; S = 500
    for _ in range(S):
        perm = RNG.permutation(len(evaluadas)); t2 = {d: tpd[evaluadas[j]] for d, j in zip(evaluadas, perm)}
        o2 = operar_con_tipo(ev, reg, t2)
        if len(o2) == 0: continue
        x = float((o2["resultado_pts"] - 0.5).mean()); meds.append(x); cn += x >= real_n; ca += float((o2["resultado_pts"] > 0).mean()) >= real_a
    W("   PR (estado sorteado entre ruedas evaluadas, %d sorteos): neto real %+.3f contra %+.3f | p neto %.3f, p acierto %.3f" % (S, real_n, np.mean(meds), cn / S, ca / S))
    res[("PR", "juntas")] = dict(real=real_n, placebo=float(np.mean(meds)), p_neto=cn / S, p_acierto=ca / S)
    # lectura descriptiva del flujo (no son candidatas)
    W(""); W("LECTURA DESCRIPTIVA (no son candidatas; misma gestion): regimen + estructura SIN las piezas de flujo")
    t1 = lambda e: ((e["a_favor_marea"] == 1) & (e["vwap_rth_dist_pts"] > 0)).to_numpy()
    t7_sin_delta = lambda e: t1(e) & ((e["n_niveles_en_zona"] >= 1) | (e["n_barridas_y_vuelta"] >= 1)).to_numpy()
    for nom, kw in (("T1 en TENDENCIA (sin confluencia)", dict(familias=("T7",), t7_mask=t1)),
                    ("T7 sin el delta (solo nivel o barrida)", dict(familias=("T7",), t7_mask=t7_sin_delta)),
                    ("banda 2 sd respetada SIN iceberg en ROTACION", dict(familias=("V4",), v4_mask=RG.banda_respetada)),
                    ("COMB sin flujo (T7 sin delta + banda sin iceberg)", dict(t7_mask=t7_sin_delta, v4_mask=RG.banda_respetada))):
        o3 = operar_con_tipo(ev, reg, tpd, **kw)
        for nd, dias in (("evaluadas", evaluadas), ("antes 10-09", antes)):
            W("   %-48s %-11s %s" % (nom, nd, txt(juzgar(o3[o3["dia"].isin(dias)], dias))))
    json.dump({"%s|%s" % k: v for k, v in res.items()}, open(os.path.join(SAL, "mnq_prueba.json"), "w"), indent=1, default=float)
    open(os.path.join(SAL, "mnq_prueba.txt"), "w", encoding="utf-8").write("\n".join(L))
    W("%.1f s" % (time.time() - t0))


if __name__ == "__main__":
    main()
