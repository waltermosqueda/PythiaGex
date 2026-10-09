# -*- coding: utf-8 -*-
"""
juez_mechas.py  (calibracion_1009 / soportes_metodo) -- 09-10-2026

EL JUEZ PROPUESTO para "la raya cae en la mecha y despues rebota / gira", aplicado a lo que DE VERDAD dibujaron la clasica, la 2.0 y la
3.0 (las mismas estelas y AUDIT que lee laboratorio/tres/extremos_dibujado.py, con sus mismas funciones; solo lectura).

PRE-REGISTRO (escrito antes de correrlo, 09-10-2026 ~00:43 hora de la PC). No se cambia despues de ver resultados.
  EXTREMO ("mecha que importa"): giro direccional con umbral THETA sobre velas m2 de MNQ: un maximo es extremo si despues el precio cae
    THETA antes de superarlo (minimo: espejo). THETA = 20 pts ("rebote") y 40 pts ("cambio de tendencia"). Se reinicia por sesion.
    Confirmado ex post: es la VARA, nunca una senal.
  X = la punta de la vela cae a <= TOL = 2 pts de la raya VIGENTE a la apertura de la vela (ED.serie: nada mira adelante); filas D = D1 o D2.
  PLACEBO (jitter, Amarasingham et al. 2012): la MISMA serie de la raya corrida un desplazamiento constante por sesion, +-U(6, 30) pts
    (signo al azar), 1000 sorteos. p = (1 + #{placebo >= real}) / 1001 sobre lo agregado (las velas no se remuestrean).
  RECALL: % de extremos (con raya vigente) que tuvieron la raya a <= TOL; lift = recall / recall medio del placebo.
  MULTIPLICIDAD: Holm sobre todas las filas de cada (unidad, ventana, THETA). Prueba del signo por sesiones.
  CONTROLES: E_rango60 (max/min de los 60 min previos, sin opciones) y la grilla de 50 pts de NQ (multiplo de 50 arriba y abajo).

CORRECCION DE DISENO (agregada 09-10-2026 ~00:46 hora de la PC, DESPUES de la primera corrida, y por eso se informa como tal): con la unidad
"todas las puntas" el control E_rango60 gano por goleada (13,8 % contra 6,0 % del placebo). Sospecha: el giro direccional exige que el
extremo sea el maximo de su tramo, y una punta pegada al maximo de los ultimos 60 min ES casi siempre el maximo del tramo: la regla
hereda ventaja MECANICA por estar hecha con el precio pasado. Arreglo: unidad = PUNTA CANDIDATA (causal): en un tramo alcista ya
confirmado, un maximo que supera al maximo del tramo hasta la vela anterior (espejo para minimos). Y = esa candidata termina siendo el
extremo (el precio cae THETA antes de superarla). Por la propiedad de Markov, en un paseo al azar Y es independiente de cualquier raya
construida con el pasado, asi que el sesgo desaparece. Se prueba con simulacion (--simular) antes de mirar las rayas reales, y se
informan LAS DOS unidades; la principal pasa a ser 'candidatas'.
Uso: python -I juez_mechas.py            -> juez_mechas.json / .md (simulacion de control + rayas reales)
"""
import os, sys, json, math, time
AQUI = os.path.dirname(os.path.abspath(__file__))
LAB = os.path.normpath(os.path.join(AQUI, "..", ".."))
for p in (LAB, AQUI):
    if p not in sys.path:
        sys.path.insert(0, p)
import tres                                            # noqa: E402  (baja la prioridad del proceso)
from tres import extremos_dibujado as ED               # noqa: E402
import numpy as np                                     # noqa: E402

TOL = 2.0
THETAS = (20.0, 40.0)
M = 1000
J1, J2 = 6.0, 30.0
RNG = np.random.default_rng(20261009)
UNIDADES = ("candidatas", "todas")


# ---------------------------------------------------------------------------------------------------------------- giro direccional
def giros_causal(o, h, l, c, theta):
    """Mismo algoritmo que censo_mechas.giros, mas el estado causal: cand_hi[i] = en tramo alcista confirmado, h[i] supera el maximo
    del tramo hasta la vela anterior; cand_lo espejo."""
    n = len(h)
    pico = np.zeros(n, bool); valle = np.zeros(n, bool)
    cand_hi = np.zeros(n, bool); cand_lo = np.zeros(n, bool)
    modo = 0
    ihi = ilo = 0
    for i in range(1, n):
        if modo == 1 and h[i] > h[ihi]:
            cand_hi[i] = True
        if modo == -1 and l[i] < l[ilo]:
            cand_lo[i] = True
        if modo >= 0 and h[i] > h[ihi]:
            ihi = i
        if modo <= 0 and l[i] < l[ilo]:
            ilo = i
        conf_p = conf_v = False
        if modo >= 0:
            lo_desp = l[ihi + 1:i + 1].min() if i > ihi else np.inf
            if c[ihi] < o[ihi]:
                lo_desp = min(lo_desp, l[ihi])
            conf_p = h[ihi] - lo_desp >= theta
        if modo <= 0:
            hi_desp = h[ilo + 1:i + 1].max() if i > ilo else -np.inf
            if c[ilo] > o[ilo]:
                hi_desp = max(hi_desp, h[ilo])
            conf_v = hi_desp - l[ilo] >= theta
        if modo == 0 and conf_p and conf_v:
            if ihi <= ilo:
                conf_v = False
            else:
                conf_p = False
        if conf_p:
            pico[ihi] = True
            a = ihi if c[ihi] < o[ihi] else ihi + 1
            ilo = a + int(np.argmin(l[a:i + 1]))
            modo = -1
        elif conf_v:
            valle[ilo] = True
            a = ilo if c[ilo] > o[ilo] else ilo + 1
            ihi = a + int(np.argmax(h[a:i + 1]))
            modo = 1
    return pico, valle, cand_hi, cand_lo


# ---------------------------------------------------------------------------------------------------------------- medicion
def medir(h, l, Ls, G, w, deltas, unidad):
    """Ls: lista de series (n,) de una fila. G = (pico, valle, cand_hi, cand_lo). Devuelve conteos reales y por sorteo."""
    P, Vv, Ch, Cl = G
    vig = np.zeros(len(h), bool)
    for L in Ls:
        vig |= np.isfinite(L)
    m = vig & w
    if not m.any():
        return None
    if unidad == "candidatas":
        uh, ul = m & Ch, m & Cl
    else:
        uh, ul = m, m
    hh, ll, PP, VV = h[uh], l[ul], P[uh], Vv[ul]
    Lh = [L[uh] for L in Ls]; Ll = [L[ul] for L in Ls]

    def cerca(x, LL, sh):
        out = np.zeros((len(sh), len(x)), bool)
        for L in LL:
            with np.errstate(invalid="ignore"):
                out |= np.abs(x[None, :] - (L[None, :] + sh[:, None])) <= TOL + 1e-9
        return out
    z = np.zeros(1)
    Xh, Xl = cerca(hh, Lh, z)[0], cerca(ll, Ll, z)[0]
    # recall: todos los extremos con raya vigente (independiente de la unidad)
    ex_h, ex_l = m & P, m & Vv
    Rh, Rl = cerca(h[ex_h], [L[ex_h] for L in Ls], z)[0], cerca(l[ex_l], [L[ex_l] for L in Ls], z)[0]
    real = dict(unidades=int(uh.sum() + ul.sum()), y=int(PP.sum() + VV.sum()), x=int(Xh.sum() + Xl.sum()),
                xy=int((Xh & PP).sum() + (Xl & VV).sum()), ext=int(ex_h.sum() + ex_l.sum()), rec=int(Rh.sum() + Rl.sum()))
    Xh_p, Xl_p = cerca(hh, Lh, deltas), cerca(ll, Ll, deltas)
    Rh_p, Rl_p = cerca(h[ex_h], [L[ex_h] for L in Ls], deltas), cerca(l[ex_l], [L[ex_l] for L in Ls], deltas)
    plac = dict(x=(Xh_p.sum(1) + Xl_p.sum(1)).astype(float), xy=((Xh_p & PP[None, :]).sum(1) + (Xl_p & VV[None, :]).sum(1)).astype(float),
                rec=(Rh_p.sum(1) + Rl_p.sum(1)).astype(float))
    return real, plac


def acumular(acc, nom, real, plac, dia):
    a = acc.setdefault(nom, dict(unidades=0, y=0, x=0, xy=0, ext=0, rec=0, px=np.zeros(M), pxy=np.zeros(M), prec=np.zeros(M), ses=[]))
    for k in ("unidades", "y", "x", "xy", "ext", "rec"):
        a[k] += real[k]
    a["px"] += plac["x"]; a["pxy"] += plac["xy"]; a["prec"] += plac["rec"]
    if real["x"] >= 3:
        trp = np.where(plac["x"] > 0, plac["xy"] / np.maximum(plac["x"], 1), np.nan)
        a["ses"].append((dia, real["x"], real["xy"] / real["x"], float(np.nanmedian(trp))))


def holm(p):
    p = np.asarray(p, float); m = len(p); o = np.argsort(p); adj = np.empty(m); run = 0.0
    for k, i in enumerate(o):
        run = max(run, min(1.0, (m - k) * p[i])); adj[i] = run
    return adj


def binom_sign_p(k, n):
    return sum(math.comb(n, j) for j in range(k, n + 1)) / 2 ** n if n else float("nan")


def resumir(acc, minimo=20):
    tabla = []
    for nom, a in acc.items():
        if a["x"] < minimo:
            continue
        tr = a["xy"] / a["x"]
        trp = a["pxy"] / np.maximum(a["px"], 1)
        p = (1 + (trp >= tr).sum()) / (M + 1)
        rec = a["rec"] / max(a["ext"], 1); recp = a["prec"] / max(a["ext"], 1)
        gan = sum(1 for s in a["ses"] if s[2] > s[3])
        tabla.append(dict(fila=nom, unidades=a["unidades"], en_raya=a["x"], extremos_en_raya=a["xy"], tasa_giro_pct=round(100 * tr, 2),
                          tasa_placebo_pct=round(100 * float(trp.mean()), 2),
                          tasa_placebo_p5_p95=[round(100 * float(np.percentile(trp, 5)), 2), round(100 * float(np.percentile(trp, 95)), 2)],
                          tasa_base_pct=round(100 * a["y"] / max(a["unidades"], 1), 2), p_jitter=float(p),
                          extremos=a["ext"], recall_pct=round(100 * rec, 2), recall_placebo_pct=round(100 * float(recp.mean()), 2),
                          lift_recall=round(rec / max(float(recp.mean()), 1e-9), 3),
                          sesiones=len(a["ses"]), sesiones_ganadas=gan, p_signo=binom_sign_p(gan, len(a["ses"]))))
    if tabla:
        for t, x in zip(tabla, holm([t["p_jitter"] for t in tabla])):
            t["p_holm"] = float(x)
        tabla.sort(key=lambda t: t["p_jitter"])
    return tabla


def renglones(tabla, titulo):
    L = ["### " + titulo, "",
         "| fila | unidades | en raya | extremos en raya | tasa de giro % | placebo % [p5-p95] | tasa base % | p jitter | p Holm | recall % | recall placebo % | lift recall | sesiones (ganadas) | p signo |",
         "|---|---|---|---|---|---|---|---|---|---|---|---|---|---|"]
    for t in tabla:
        L.append("| %s | %d | %d | %d | %.1f | %.1f [%.1f-%.1f] | %.1f | %.3f | %.3f | %.1f | %.1f | %.2f | %d (%d) | %.3f |" % (
            t["fila"], t["unidades"], t["en_raya"], t["extremos_en_raya"], t["tasa_giro_pct"], t["tasa_placebo_pct"], t["tasa_placebo_p5_p95"][0],
            t["tasa_placebo_p5_p95"][1], t["tasa_base_pct"], t["p_jitter"], t["p_holm"], t["recall_pct"], t["recall_placebo_pct"], t["lift_recall"],
            t["sesiones"], t["sesiones_ganadas"], t["p_signo"]))
    L.append("")
    return L


# ---------------------------------------------------------------------------------------------------------------- controles
def rango60_arr(t, h, l, seg):
    S = dict(t=t, h=h, l=l, n=len(h), seg=seg)
    return ED.rango60(S)


def grilla50(cp):
    a = np.ceil(cp / 50.0) * 50.0; b = np.floor(cp / 50.0) * 50.0
    b = np.where(a == b, b - 50.0, b)
    return a, b


def simulacion(sesiones=40, n_velas=405, sd_vela=8.744):
    """Paseo al azar sin memoria (sd del cierre m2 de la noche real, base_y_potencia.json) con las mismas rayas de control.
    Si un juez da ventaja a E_rango60 o a la grilla aca, esa ventaja es MECANICA."""
    acc = {u: {} for u in UNIDADES}
    for s in range(sesiones):
        seg = 120
        x = 30000.0 + np.cumsum(RNG.normal(0.0, sd_vela / math.sqrt(seg), n_velas * seg))
        x = (np.round(x / 0.25) * 0.25).reshape(n_velas, seg)
        o, h, l, c = x[:, 0], x.max(1), x.min(1), x[:, -1]
        t = np.datetime64("2026-01-01T22:00") + np.arange(n_velas) * np.timedelta64(seg, "s")
        cp = np.r_[np.nan, c[:-1]]
        tch, pso = rango60_arr(t, h, l, seg)
        ga, gb = grilla50(cp)
        fijas = [np.full(n_velas, v) for v in RNG.uniform(l.min(), h.max(), 6)]
        fams = {"E_rango60 (techo+piso)": [tch, pso], "grilla 50": [ga, gb], "6 rayas fijas al azar": fijas}
        G = giros_causal(o, h, l, c, 20.0)
        w = np.ones(n_velas, bool)
        for nom, Ls in fams.items():
            deltas = RNG.uniform(J1, J2, M) * RNG.choice([-1.0, 1.0], M)
            for u in UNIDADES:
                r = medir(h, l, Ls, G, w, deltas, u)
                if r:
                    acumular(acc[u], nom, r[0], r[1], "sim%d" % s)
    return {u: resumir(acc[u], minimo=1) for u in UNIDADES}


# ---------------------------------------------------------------------------------------------------------------- principal
def main():
    t0 = time.time()
    out = dict(pre_registro=__doc__, parametros=dict(TOL=TOL, THETAS=THETAS, M=M, J=[J1, J2]))
    L = ["# Juez de mechas (giro direccional): control con paseo al azar y rayas reales de clasica / 2.0 / 3.0", "",
         "Generado por `juez_mechas.py` (09-10-2026). TOL %.1f pts; placebo = la misma raya corrida +-U(%g, %g) pts constante por sesion, %d sorteos. "
         "'tasa de giro' = de las unidades cuya punta llego a <= TOL de la raya, %% que fueron el extremo THETA. Unidad 'todas' = toda punta de vela; "
         "'candidatas' = punta que extiende un tramo ya confirmado (causal; la principal)." % (TOL, J1, J2, M), ""]
    sim = simulacion()
    out["simulacion_paseo_azar"] = sim
    L.append("## 0. Control: paseo al azar sin memoria (40 sesiones de 405 velas m2, THETA 20). Aca NINGUNA raya puede tener ventaja real.")
    L.append("")
    for u in UNIDADES:
        L += renglones(sim[u], "unidad = " + u)
    print("simulacion", round(time.time() - t0, 1), "s", flush=True)

    filas, diag = ED.construir_filas()
    V2 = ED.cargar_velas(120)
    ses = {d: ED.preparar(v, 120) for d, (v, _) in V2.items()}
    grupos = {}
    for f in filas:
        if f["src"] == "estela":
            grupos[f["nombre"]] = [f["nombre"]]
            if f["raya"] in ("D1", "D2"):
                grupos.setdefault("%s %s D (D1+D2)" % (f["ver"], f["capa"]), []).append(f["nombre"])
    porfila = {f["nombre"]: f for f in filas}
    acc = {(u, wn, th): {} for u in UNIDADES for wn in ("noche", "dia") for th in THETAS}
    for dia, S in sorted(ses.items()):
        Gs = {th: giros_causal(S["o"], S["h"], S["l"], S["c"], th) for th in THETAS}
        series = {n: ED.serie(f, S["t"]) for n, f in porfila.items() if f["src"] == "estela"}
        tch, pso = ED.rango60(S)
        ga, gb = grilla50(S["cp"])
        fams = {g: [series[n] for n in miembros] for g, miembros in grupos.items()}
        fams["CONTROL E_rango60 (techo+piso)"] = [tch, pso]
        fams["CONTROL grilla 50 (arriba+abajo)"] = [ga, gb]
        for nom, Ls in fams.items():
            if not any(np.isfinite(Lx).any() for Lx in Ls):
                continue
            deltas = RNG.uniform(J1, J2, M) * RNG.choice([-1.0, 1.0], M)
            for th in THETAS:
                for wn in ("noche", "dia"):
                    for u in UNIDADES:
                        r = medir(S["h"], S["l"], Ls, Gs[th], S["win"][wn], deltas, u)
                        if r:
                            acumular(acc[(u, wn, th)], nom, r[0], r[1], dia)
    out["sesiones"] = sorted(ses)
    out["reales"] = {}
    L.append("## Rayas reales: velas m2 de MNQ, sesiones %s .. %s (%d)" % (min(ses), max(ses), len(ses)))
    L.append("")
    for u in UNIDADES:
        for th in THETAS:
            for wn in ("noche", "dia"):
                tabla = resumir(acc[(u, wn, th)])
                out["reales"]["%s|%s|%g" % (u, wn, th)] = tabla
                L += renglones(tabla, "unidad = %s, %s, THETA %g pts (%d filas con >= 20 en raya)" % (u, wn.upper(), th, len(tabla)))
    with open(os.path.join(AQUI, "juez_mechas.json"), "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=1)
    with open(os.path.join(AQUI, "juez_mechas.md"), "w", encoding="utf-8") as f:
        f.write("\n".join(L) + "\n")
    print("listo", round(time.time() - t0, 1), "s", flush=True)


if __name__ == "__main__":
    main()
