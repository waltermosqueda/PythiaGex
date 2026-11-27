# -*- coding: utf-8 -*-
"""RONDA 22, paso 1: CALIBRACION de la traduccion a 5 min (NO es la prueba). Compara contra la R21 calculada con la cinta de ES.
(A) las fechas de velas-MES-m5.csv que tambien tienen cinta: 08-09, 17-09, 18-09 y 21-09-2026 (el archivo de 5 min no tiene 09..16-09).
    Para la mediana de 10 del 08-09 se leen las velas desde el 20-08 SOLO como historia (rango de la rueda); para 17/18/21-09 la historia
    de 5 min esta cortada, asi que se usa la mediana de la R19 (la misma que uso la R21). Ningun estado, evento ni resultado del periodo
    independiente se calcula aca.
(B) robustez de la resolucion: velas de 5 min ARMADAS desde la cinta de ES en las 55 ruedas de la R21 (+ 08 y 09-09): mismo instrumento,
    misma historia; mide solo cuanto se pierde por mirar velas de 5 min en vez de la cinta.
Elige (por fidelidad, nunca por resultado): precio por vela del VWAP, varianza dentro de la vela, orden dentro de la vela, VWAP de
referencia y empates del primer paso (L2). Informa el % de coincidencia del estado D1 cada 5 min, VWAP, L2, eventos, piezas y resultados.
Uso: python -I r22_01_calibracion.py -> salidas/calibracion.txt y .json"""
import glob, json, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r22_comun as K
import r22_motor as MO
import numpy as np, pandas as pd

RAS = os.path.join(K.SCR19, "rasgos"); CIN = os.path.join(K.SCR19, "cinta")
DIAS_A = ["2026-09-08", "2026-09-17", "2026-09-18", "2026-09-21"]
L = []


def W(s=""):
    print(s, flush=True); L.append(s)


def cargar_ras(dia):
    z = np.load(os.path.join(RAS, "ES-%s.npz" % dia)); meta = json.load(open(os.path.join(RAS, "ES-%s.json" % dia)))
    return {k: z[k] for k in ("c", "h", "l", "vw_r", "sd_r", "vw_j", "sd_j", "vw_sem", "vw_mes", "delta")}, meta


def ref_r21(R, meta, f):
    """Estado D1 por minuto con la formula de r21_01 (rango con la cinta / (mediana x f))."""
    sec = 60 * np.arange(390) + 59
    H = np.maximum.accumulate(R["h"]); Lo = np.minimum.accumulate(R["l"])
    rrn = (H[sec] - Lo[sec]) / (meta["mediana_rango_10"] * f)
    tipo, lvw = MO.estado(rrn, R["c"][sec], R["vw_r"][sec], meta["rth_o"])
    return dict(rrn=rrn, tipo=tipo, lvw=lvw, H=H[sec], L=Lo[sec], c=R["c"][sec], vw=R["vw_r"][sec])


def l2_r21(R, ref, X, m):
    """Copia de la etiqueta L2 de r21_01_regimen.py en el minuto m."""
    S = 60 * m + 59; s = ref["lvw"][m]
    if s == 0: return "ninguno"
    hh = R["h"][S + 1:]; ll = R["l"][S + 1:]; vv = R["vw_r"][S + 1:]
    if s > 0: tq = np.nonzero(ll <= vv)[0]; ex = np.nonzero(hh >= ref["H"][m] + X / 2)[0]
    else: tq = np.nonzero(hh >= vv)[0]; ex = np.nonzero(ll <= ref["L"][m] - X / 2)[0]
    a = tq[0] if len(tq) else 10 ** 9; b = ex[0] if len(ex) else 10 ** 9
    return "ninguno" if a == b == 10 ** 9 else ("vuelve" if a < b else ("extiende" if b < a else "empate"))


def velas_de_cinta(dia):
    d = pd.read_parquet(os.path.join(CIN, "ES-%s.parquet" % dia), columns=["ny_s", "primero", "ultimo", "vol", "lado"])
    b = np.floor(d["ny_s"].to_numpy() / 300.0).astype(np.int64)
    mn = np.minimum(d["primero"], d["ultimo"]).to_numpy(); mx = np.maximum(d["primero"], d["ultimo"]).to_numpy()
    g = pd.DataFrame({"b": b, "pri": d["primero"].to_numpy(), "ult": d["ultimo"].to_numpy(), "mn": mn, "mx": mx,
                      "vol": d["vol"].to_numpy(float), "dl": (d["vol"] * d["lado"]).to_numpy(float), "s": d["ny_s"].to_numpy()})
    a = g.groupby("b").agg(o=("pri", "first"), c=("ult", "last"), h=("mx", "max"), l=("mn", "min"), vol=("vol", "sum"), delta=("dl", "sum"))
    a = a[(a.index >= -72) & (a.index < 204)]                          # 18:00 de la vispera a 17:00
    ses = {"hm": (a.index.to_numpy() * 5).astype(int)}
    for k in ("o", "h", "l", "c", "vol", "delta"): ses[k] = a[k].to_numpy(float)
    # orden real dentro de cada vela: primer instante del maximo y del minimo
    g = g[(g.b >= -72) & (g.b < 204)]
    hi = g.groupby("b")["mx"].transform("max"); lo = g.groupby("b")["mn"].transform("min")
    t_hi = g[g.mx == hi].groupby("b")["s"].min(); t_lo = g[g.mn == lo].groupby("b")["s"].min()
    orden = pd.DataFrame({"t_hi": t_hi, "t_lo": t_lo}).reindex(a.index)
    return ses, orden


def ctx_r19(meta, X=None, med=None):
    n = meta["niv"]
    niv = {k: n.get(k, np.nan) for k in ("pdh", "pdl", "pdc", "pd_poc", "pd_vah", "pd_val", "ps_poc", "ps_vah", "ps_val")}
    med = meta["mediana_rango_10"] if med is None else med
    return dict(med10=med, X=meta["X"] if X is None else X, niv=niv, sem=None, mes=None)


def din_ext(R):
    sec = 300 * np.arange(78) + 299
    return {"vws": R["vw_sem"][sec], "vwm": R["vw_mes"][sec]}


def comparar_vwap(J, R):
    sec = 300 * np.arange(78) + 299
    e = {"vwr": np.abs(J["vwr"] - R["vw_r"][sec]), "vwj": np.abs(J["vwj"] - R["vw_j"][sec]),
         "sdr_rel": np.abs(J["sdr"][6:] / R["sd_r"][sec][6:] - 1), "sdj_rel": np.abs(J["sdj"] / R["sd_j"][sec] - 1)}
    return e


def estados_5(J, ref):
    k = np.arange(5, 72); m = 5 * k + 4
    return J["tipo"][k], ref["tipo"][m], ref["tipo"][m + 1]


def main():
    t0 = time.time()
    f = K.curva_f()
    W("RONDA 22 - CALIBRACION de la traduccion a 5 min (%s)" % time.strftime("%Y-%m-%d %H:%M"))
    W("Curva f(m) congelada de la R21 verificada (SHA-256 %s...)." % K.CURVA_SHA[:12])
    ev21 = pd.read_parquet(os.path.join(K.SCR21, "eventos_r21.parquet"))
    reg21 = pd.read_parquet(os.path.join(K.SCR21, "regimen_minuto.parquet"))
    dias_B = sorted(set(os.path.basename(x)[3:13] for x in glob.glob(os.path.join(RAS, "ES-*.npz"))))
    dias_B = [d for d in dias_B if os.path.exists(os.path.join(CIN, "ES-%s.parquet" % d)) and d <= "2026-11-25"]
    # ---------- control: mi estado R21 por minuto = el de regimen_minuto.parquet
    dif = 0; tot = 0
    for d in sorted(reg21.dia.unique()):
        R, meta = cargar_ras(d); ref = ref_r21(R, meta, f)
        g = reg21[reg21.dia == d].sort_values("m")
        dif += int((np.abs(g["rrn"].to_numpy() - ref["rrn"]) > 1e-9).sum()); tot += 390
    W("Control: el rango relativo por minuto recalculado desde la cinta difiere del de la R21 en %d de %d minutos." % (dif, tot))
    # ======================================================== (B) velas armadas desde la cinta (55 ruedas + 08/09-09)
    W(""); W("(B) VELAS DE 5 MIN ARMADAS DESDE LA CINTA DE ES (%d ruedas, %s a %s): efecto de la resolucion" % (len(dias_B), dias_B[0], dias_B[-1]))
    conv_base = dict(MO.CONV)
    datos = {}
    acc = {"color": [0, 0], "cercano": [0, 0]}
    for d in dias_B:
        K.frenar_si_falta_ram(2.0)
        ses, orden = velas_de_cinta(d); R, meta = cargar_ras(d)
        datos[d] = (ses, R, meta, ref_r21(R, meta, f))
        ok = orden["t_hi"].notna() & orden["t_lo"].notna() & (orden["t_hi"] != orden["t_lo"])
        alto_primero = (orden["t_hi"] < orden["t_lo"]).to_numpy()[ok.to_numpy()]
        o, h, l, c = (ses[k][ok.to_numpy()] for k in ("o", "h", "l", "c"))
        for regla in acc:
            P = MO.camino(o, h, l, c, regla); pred_alto = P[:, 1] == h
            pred_alto = np.where(h == l, False, pred_alto)
            acc[regla][0] += int((pred_alto == alto_primero).sum()); acc[regla][1] += len(alto_primero)
    W("Orden dentro de la vela (que extremo llega primero), %d velas de 18:00 a 17:00:" % acc["color"][1])
    for regla, (a, n) in acc.items():
        W("   regla '%s': acierta %.1f %%" % (regla, 100 * a / n))
    regla_ok = max(acc, key=lambda r: acc[r][0])
    W("   -> ELEGIDA: '%s'" % regla_ok)
    # precio por vela y varianza dentro de la vela
    W("Precio por vela para el VWAP (error contra el VWAP de la cinta al cierre de cada vela de la rueda):")
    mejor = None
    for pr in ("hlc3", "ohlc4", "hl2", "c"):
        for vi in (False, True):
            cv = dict(conv_base, precio=pr, var_intra=vi, camino=regla_ok)
            E = {"vwr": [], "vwj": [], "sdr_rel": [], "sdj_rel": []}
            for d, (ses, R, meta, ref) in datos.items():
                J = MO.jornada(ses, ctx_r19(meta), f, cv, 5, eventos=False, din_externos=din_ext(R))
                e = comparar_vwap(J, R)
                for k_ in E: E[k_].append(e[k_])
            E = {k_: np.concatenate(v) for k_, v in E.items()}
            W("   %-5s var_intra=%-5s | VWAP rueda: mediana |err| %.3f pts (p90 %.3f) | jornada %.3f | desvio rueda: mediana |err rel| %.1f %% | jornada %.1f %%" % (
                pr, vi, np.median(E["vwr"]), np.quantile(E["vwr"], 0.9), np.median(E["vwj"]), 100 * np.median(E["sdr_rel"]), 100 * np.median(E["sdj_rel"])))
            if not vi:
                if mejor is None or np.median(E["vwr"]) < mejor[0]: mejor = (np.median(E["vwr"]), pr)
            else:
                if pr == (mejor[1] if mejor else pr): pass
    precio_ok = mejor[1]
    # var_intra: la que mejor da el desvio con el precio elegido
    sd_err = {}
    for vi in (False, True):
        cv = dict(conv_base, precio=precio_ok, var_intra=vi, camino=regla_ok); E = []
        for d, (ses, R, meta, ref) in datos.items():
            J = MO.jornada(ses, ctx_r19(meta), f, cv, 5, eventos=False, din_externos=din_ext(R))
            e = comparar_vwap(J, R); E.append(np.concatenate([e["sdr_rel"], e["sdj_rel"]]))
        sd_err[vi] = float(np.median(np.concatenate(E)))
    vi_ok = min(sd_err, key=sd_err.get)
    W("   -> ELEGIDO: precio '%s' (menor error del VWAP); varianza dentro de la vela = %s (desvio: %.1f %% con, %.1f %% sin)" % (
        precio_ok, vi_ok, 100 * sd_err[True], 100 * sd_err[False]))
    # L2: VWAP de referencia y empates
    W("Primer paso (L2: extremo nuevo + X/2 antes que el VWAP), contra la etiqueta de la R21 recalculada en el MISMO instante (cierre de cada vela):")
    l2_res = {}
    for vwref in ("inicio", "fin"):
        for emp in ("camino", "empate"):
            cv = dict(conv_base, precio=precio_ok, var_intra=vi_ok, camino=regla_ok, l2_vw=vwref, l2_empate=emp)
            a = []; b = []
            for d, (ses, R, meta, ref) in datos.items():
                J = MO.jornada(ses, ctx_r19(meta), f, cv, 5, eventos=False, din_externos=din_ext(R))
                for _, r in J["control"].iterrows():
                    a.append(r["L2"]); b.append(l2_r21(R, ref, meta["X"], 5 * int(r["k"]) + 4))
            a = np.array(a); b = np.array(b)
            co = float(np.mean(a == b)); n_emp = int((a == "empate").sum())
            ext_a = float(np.mean(a == "extiende")); ext_b = float(np.mean(b == "extiende"))
            l2_res[(vwref, emp)] = co
            W("   VWAP de referencia '%s', empates '%s': coincide %.1f %% de %d puntos | empates %d | 'extiende' 5 min %.1f %% vs cinta %.1f %%" % (
                vwref, emp, 100 * co, len(a), n_emp, 100 * ext_a, 100 * ext_b))
    vwref_ok, emp_ok = max(l2_res, key=l2_res.get)
    CONV = dict(precio=precio_ok, var_intra=bool(vi_ok), camino=regla_ok, l2_vw=vwref_ok, l2_empate=emp_ok)
    W("   -> ELEGIDO: referencia '%s', empates '%s'" % (vwref_ok, emp_ok))
    W("CONVENCIONES ELEGIDAS: %s" % CONV)
    # ---------- estado D1 con las convenciones elegidas (B)
    W("")
    W("Estado D1 cada 5 min (cierres 10:00-15:30; 67 puntos por rueda), 5 min contra la cinta en el MISMO instante (minuto 5k+4):")
    A5 = []; B1 = []; B2 = []; per = []
    for d, (ses, R, meta, ref) in datos.items():
        J = MO.jornada(ses, ctx_r19(meta), f, CONV, 5, eventos=False, din_externos=din_ext(R))
        a, b1, b2 = estados_5(J, ref); A5.append(a); B1.append(b1); B2.append(b2); per += [d] * len(a)
    A5 = np.concatenate(A5); B1 = np.concatenate(B1); B2 = np.concatenate(B2); per = np.array(per)
    W("   coincidencia del estado (T/R/E/N): %.1f %% de %d puntos (contra el minuto siguiente, los puntos de control de la R21: %.1f %%)" % (
        100 * np.mean(A5 == B1), len(A5), 100 * np.mean(A5 == B2)))
    W("   TENDENCIA si/no: coincide %.1f %%; de los puntos TENDENCIA de la cinta, el 5 min marca TENDENCIA en %.1f %%; de los TENDENCIA de 5 min, la cinta lo es en %.1f %%" % (
        100 * np.mean((A5 == "T") == (B1 == "T")), 100 * np.mean(A5[B1 == "T"] == "T"), 100 * np.mean(B1[A5 == "T"] == "T")))
    tab = pd.crosstab(pd.Series(B1, name="cinta"), pd.Series(A5, name="5min"))
    W("   matriz (filas cinta, columnas 5 min):"); [W("      " + x) for x in tab.to_string().split("\n")]
    # ======================================================== (A) velas de MES de 5 min (el archivo real de la prueba)
    W(""); W("(A) VELAS DE MES 5 MIN (velas-MES-m5.csv) contra la cinta de ES: %s" % ", ".join(DIAS_A))
    v = MO.leer_velas(K.M5, "2026-08-20", "2026-09-21")
    sesA = MO.sesiones(v)
    H = MO.Historia({r: s for r, s in sesA.items() if r <= "2026-09-08"}, CONV, 5)
    cA = H.ctx("2026-09-08")
    _, meta08 = cargar_ras("2026-09-08")
    W("   08-09: mediana de 10 con las velas de MES %.2f (X %.2f) | la de la R19/R21 %.2f (X %.2f) | ruedas %s" % (
        cA["med10"], cA["X"], meta08["mediana_rango_10"], meta08["X"], ",".join(x[5:] for x in cA["prev10"])))
    A5 = []; B1 = []; dA = []; eA = {"vwr": [], "vwj": [], "sdr_rel": []}; sgn = []; cor = []
    evA = []
    for d in DIAS_A:
        R, meta = cargar_ras(d); ref = ref_r21(R, meta, f)
        ses = sesA[d]
        med = cA["med10"] if d == "2026-09-08" else meta["mediana_rango_10"]
        ctx = ctx_r19(meta, X=MO.X_de(med), med=med)
        J = MO.jornada(ses, ctx, f, CONV, 5, eventos=True, din_externos=din_ext(R))
        a, b1, _ = estados_5(J, ref); A5.append(a); B1.append(b1); dA += [d] * len(a)
        e = comparar_vwap(J, R)
        for k_ in eA: eA[k_].append(e[k_])
        # delta de 5 min: MES contra ES
        dl_es = np.array([R["delta"][300 * k:300 * (k + 1)].sum() for k in range(78)])
        sgn.append(np.sign(J["delta"]) == np.sign(dl_es)); cor.append(np.corrcoef(J["delta"], dl_es)[0, 1])
        W("   %s: estado coincide %.1f %% | VWAP rueda mediana |err| %.2f pts | X %.2f (R21 %.2f)" % (
            d, 100 * np.mean(a == b1), np.median(e["vwr"]), ctx["X"], meta["X"]))
        evA.append((d, J, R, meta, ref))
    A5 = np.concatenate(A5); B1 = np.concatenate(B1)
    W("   (A) JUNTAS: estado D1 coincide %.1f %% de %d puntos; TENDENCIA si/no %.1f %%" % (100 * np.mean(A5 == B1), len(A5), 100 * np.mean((A5 == "T") == (B1 == "T"))))
    W("   VWAP rueda (MES contra cinta ES): mediana |err| %.3f pts, p90 %.3f; jornada %.3f; desvio rel %.1f %%" % (
        np.median(np.concatenate(eA["vwr"])), np.quantile(np.concatenate(eA["vwr"]), 0.9), np.median(np.concatenate(eA["vwj"])), 100 * np.median(np.concatenate(eA["sdr_rel"]))))
    sg = np.concatenate(sgn)
    W("   delta de 5 min del MES contra el del ES (78 velas x 4 ruedas): mismo signo %.1f %%, correlacion media %.2f" % (100 * sg.mean(), np.mean(cor)))
    # ======================================================== eventos: vueltas de la ola, piezas y resultados (B y A)
    W(""); W("EVENTOS (vueltas de la ola 10:00-15:30): 5 min (entrada a stop) contra la cinta (eventos_r21), mismo lado, extremo a <= 2 ticks, +-1 vela")
    for etiqueta, fuente in (("B (velas de la cinta)", "B"), ("A (velas de MES)", "A")):
        filas = []
        if fuente == "B":
            it = [(d, MO.jornada(ses, ctx_r19(meta), f, CONV, 5, eventos=True, din_externos=din_ext(R)), R, meta, ref)
                  for d, (ses, R, meta, ref) in datos.items() if d in set(ev21.dia)]
        else:
            it = [x for x in evA if x[0] in set(ev21.dia)]
        for d, J, R, meta, ref in it:
            e5 = J["eventos"]; e5 = e5[e5.entrada_tipo == "stop"].copy()
            e21 = ev21[(ev21.dia == d) & (ev21.seg_rueda >= 1800) & (ev21.seg_rueda < 21600)].copy()
            mp = np.clip(e21["seg_rueda"].to_numpy() // 60 - 1, 0, 389)
            e21["estado21"] = ref["tipo"][mp]; e21["lvw21"] = ref["lvw"][mp]
            usados = set()
            for i, r in e21.iterrows():
                kb = int(r.seg_rueda) // 300
                cand = e5[(e5.lado == r.lado) & ((e5.ext - r.ext).abs() <= 0.5 + 1e-9) & ((e5.k - kb).abs() <= 1)]
                cand = cand[~cand.index.isin(usados)]
                fila = dict(dia=d, seg=int(r.seg_rueda), lado=int(r.lado), match=len(cand) > 0)
                t1_21 = (r.a_favor_marea == 1) and (r.vwap_rth_dist_pts > 0)
                fila.update(T1_21=t1_21, T7_21=t1_21 and ((r.n_niveles_en_zona >= 1) or (r.n_barridas_y_vuelta >= 1) or (r.of_delta_300 > 0)),
                            enT_21=(r.estado21 == "T") and (r.lado == r.lvw21), enR_21=(r.estado21 == "R"),
                            niv_21=r.n_niveles_en_zona >= 1, barr_21=r.n_barridas_y_vuelta >= 1, d300_21=r.of_delta_300 > 0,
                            marea_21=r.a_favor_marea == 1, vw_21=r.vwap_rth_dist_pts > 0, S6_21=r.S6_pts > 0, P1_21=r.P1_pts > 0,
                            banda_21=bool(MO_banda21(r)))
                if len(cand):
                    cc = cand.assign(dd=(cand.ext - r.ext).abs() * 100 + (cand.k - kb).abs()).sort_values("dd").iloc[0]
                    usados.add(cc.name)
                    fila.update(T1_5=(cc.a_favor_marea == 1) and (cc.vw_dist > 0), niv_5=cc.n_zona >= 1, barr_5=cc.n_barr >= 1, d300_5=cc.delta_fav > 0,
                                marea_5=cc.a_favor_marea == 1, vw_5=cc.vw_dist > 0, enT_5=(cc.estado == "T") and (cc.lado == cc.lado_vw), enR_5=cc.estado == "R",
                                S6_5=cc.S6_pts > 0, P1_5=cc.P1_pts > 0, banda_5=cc.banda == 1, R_5=cc.R, R_21=r.R)
                    fila["T7_5"] = fila["T1_5"] and (fila["niv_5"] or fila["barr_5"] or fila["d300_5"])
                filas.append(fila)
            n5 = len(e5[(e5.k >= 6) & (e5.k <= 71)])
            fila_extra = dict(n5=n5, emparejados=len(usados))
            filas.append(dict(dia=d, seg=-1, _n5=n5, _usados=len(usados)))
        df = pd.DataFrame([x for x in filas if x.get("seg", 0) >= 0]); ex_ = pd.DataFrame([x for x in filas if x.get("seg", 0) < 0])
        m = df[df.match == True]
        W("  %s: vueltas de la cinta %d; con pareja en 5 min %.1f %%; vueltas de 5 min %d, con pareja %.1f %%" % (
            etiqueta, len(df), 100 * df.match.mean(), int(ex_._n5.sum()), 100 * ex_._usados.sum() / max(ex_._n5.sum(), 1)))
        for a_, b_, nom in (("enT_5", "enT_21", "estado TENDENCIA del lado"), ("enR_5", "enR_21", "estado ROTACION"), ("marea_5", "marea_21", "a favor de la marea"),
                            ("vw_5", "vw_21", "VWAP detras"), ("T1_5", "T1_21", "T1"), ("niv_5", "niv_21", "nivel en la zona"), ("barr_5", "barr_21", "barrida"),
                            ("d300_5", "d300_21", "delta 5 min a favor (vela previa vs 300 s)"), ("T7_5", "T7_21", "T7 (sin estado)"),
                            ("banda_5", "banda_21", "banda 2 sigma respetada"), ("S6_5", "S6_21", "resultado 1R (gana/pierde)"), ("P1_5", "P1_21", "resultado P1 (gana/pierde)")):
            a = m[a_].astype(bool).to_numpy(); b = m[b_].astype(bool).to_numpy()
            W("     %-45s coincide %5.1f %% (si en cinta %4.1f %%, si en 5 min %4.1f %%; n %d)" % (nom, 100 * np.mean(a == b), 100 * b.mean(), 100 * a.mean(), len(a)))
        # la regla completa: T7 en TENDENCIA del lado
        selc = df.T7_21 & df.enT_21
        sel5 = m.T7_5.astype(bool) & m.enT_5.astype(bool)
        W("     T7 en TENDENCIA: en la cinta %d vueltas; de esas, con pareja que el 5 min tambien toma %d; el 5 min toma %d (de las emparejadas)" % (
            int(selc.sum()), int((m.T7_21 & m.enT_21 & sel5).sum()), int(sel5.sum())))
        if fuente == "B":
            W("     R medio de las parejas: 5 min %.2f, cinta %.2f" % (m.R_5.mean(), m.R_21.mean()))
    json.dump({"conv": CONV, "orden_vela": acc, "sd_err": {str(k): v for k, v in sd_err.items()},
               "l2": {"%s|%s" % k: v for k, v in l2_res.items()}}, open(os.path.join(K.SAL, "calibracion.json"), "w"), indent=1)
    open(os.path.join(K.SAL, "calibracion.txt"), "w", encoding="utf-8").write("\n".join(L))
    W("%.1f s" % (time.time() - t0))


def MO_banda21(r):
    X = r.X; lado = r.lado; ok = False
    for nom in MO.BANDAS[lado]:
        dn = r.get("d_" + nom)
        if dn is None or not np.isfinite(dn): continue
        Lb = r.ext - dn; pen = (Lb - r.ext) * lado
        ok |= (pen >= (-0.5 + 1e-6) * X - 1e-9) and (pen <= 1e-9)
    return ok


if __name__ == "__main__":
    main()
