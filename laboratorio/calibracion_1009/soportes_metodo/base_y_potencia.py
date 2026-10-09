# -*- coding: utf-8 -*-
"""
base_y_potencia.py  (calibracion_1009 / soportes_metodo) -- 09-10-2026

Dos preguntas de metodo, sin tocar nada del proyecto (solo LEE velas):

1) TASA BASE: cuanto "rebote" da el juez del laboratorio (extremos_dibujado.py / criterio_operador.py: toque +-TOL,
   rebote = REB pts a favor por MECHA en MIRAR velas antes de un CIERRE CRUZ pts del otro lado) cuando el precio es un
   PASEO AL AZAR SIN MEMORIA y las rayas son fijas al azar. Si un paseo al azar ya da ~60-65 %, ese es el piso del juez,
   no una propiedad del mercado ni de la gamma.
   - Volatilidad calibrada con las velas m2 reales de MNQ (velas_cache_2026-10-07), noche y rueda por separado.
   - Variantes del juez: base (2/6/2/3), fino (1,5/5/2/3), ancho (3/8/3/3) y dos simetricas (todo por mecha / todo por cierre).
   - Referencia teorica (ruina del jugador, Browniano sin deriva): desde la raya, P(alejarse R antes de pasar C) = C/(R+C).

2) POTENCIA: cuantos toques (y sesiones) hacen falta para detectar +X puntos porcentuales sobre una base p0, con
   alfa corregido por cantidad de formulas probadas (Bonferroni) y efecto de diseno por toques de la misma sesion
   (DE = 1 + (m-1) ICC).

Uso:  python -I base_y_potencia.py         -> base_y_potencia.json y .md en esta carpeta
"""
import os, sys, json, math
import numpy as np
import pandas as pd
from statistics import NormalDist

AQUI = os.path.dirname(os.path.abspath(__file__))
VELAS = os.path.join(AQUI, "..", "..", "tres", "datos", "velas_cache_2026-10-07", "velas-MNQ-m2.csv")
RNG = np.random.default_rng(20261009)
Z = NormalDist().inv_cdf


# ---------------------------------------------------------------------------------------------------------------- velas reales
def ventanas(t):
    hm = t.dt.hour * 60 + t.dt.minute
    dia = (hm >= 13 * 60 + 30) & (hm < 20 * 60)
    noche = (hm >= 22 * 60) | (hm < 13 * 60 + 30)
    return {"noche": noche.values, "dia": dia.values}


def calibrar():
    d = pd.read_csv(VELAS, usecols=["t", "o", "h", "l", "c"], parse_dates=["t"])
    d = d.sort_values("t").reset_index(drop=True)
    contiguo = d["t"].diff().dt.total_seconds().eq(120).values
    w = ventanas(d["t"])
    out = {}
    for wn, m in w.items():
        dc = d["c"].diff().values
        ok = m & contiguo
        out[wn] = dict(velas=int(m.sum()), sd_cierre=float(np.nanstd(dc[ok])), rango_medio=float((d["h"] - d["l"]).values[m].mean()),
                       rango_mediano=float(np.median((d["h"] - d["l"]).values[m])),
                       desde=str(d["t"][m].min()), hasta=str(d["t"][m].max()))
    return out


# ---------------------------------------------------------------------------------------------------------------- simulacion
def simular_velas(n_velas, sd_vela, seg=120, tick=0.25, p0=30000.0):
    """Browniano sin deriva a 1 s, redondeado al tick, agregado en velas de `seg` segundos. Devuelve o,h,l,c."""
    s = sd_vela / math.sqrt(seg)
    x = p0 + np.cumsum(RNG.normal(0.0, s, n_velas * seg))
    x = np.round(x / tick) * tick
    x = x.reshape(n_velas, seg)
    return x[:, 0], x.max(1), x.min(1), x[:, -1]


def juzgar(o, h, l, c, L, tol, reb, cruz, mirar, modo="base"):
    """Replica de medir_serie (extremos_dibujado.py:297) para UNA raya fija L. modo: base (rebote por mecha, traspaso por
    cierre), mecha (los dos por mecha), cierre (los dos por cierre)."""
    n = len(c)
    toca = (l <= L + tol) & (h >= L - tol)
    prev = np.zeros(n, bool); prev[1:] = toca[:-1]
    cp = np.empty(n); cp[0] = np.nan; cp[1:] = c[:-1]
    fwd = np.zeros(n, bool); fwd[:n - mirar] = True
    with np.errstate(invalid="ignore"):
        cand = toca & ~prev & (np.abs(cp - L) > tol) & fwd
    reb_n = tras_n = ind_n = 0
    for i in np.nonzero(cand)[0]:
        lado = 1 if cp[i] > L else -1
        # traspaso en la vela del toque
        if modo == "mecha":
            pas0 = ((L - l[i]) if lado > 0 else (h[i] - L)) >= cruz
        else:
            pas0 = (L - c[i]) * lado >= cruz
        if pas0:
            tras_n += 1; continue
        r = "indef"
        for k in range(i + 1, i + 1 + mirar):
            if modo == "cierre":
                fav = ((c[k] - L) if lado > 0 else (L - c[k])) >= reb
            else:
                fav = ((h[k] - L) if lado > 0 else (L - l[k])) >= reb
            if modo == "mecha":
                contra = ((L - l[k]) if lado > 0 else (h[k] - L)) >= cruz
            else:
                contra = (L - c[k]) * lado >= cruz
            if fav and contra and modo != "base":   # misma vela: orden desconocido -> indefinido (en "base" se replica el
                r = "ambiguo"; break                 # original, que mira primero el rebote: extremos_dibujado.py:316-320)
            if fav:
                r = "rebote"; break
            if contra:
                r = "traspaso"; break
        if r == "rebote":
            reb_n += 1
        elif r == "traspaso":
            tras_n += 1
        else:
            ind_n += 1
    return reb_n, tras_n, ind_n


VARIANTES = {
    "base (2/6/2/3) mecha-vs-cierre": (2.0, 6.0, 2.0, 3, "base"),
    "fino (1.5/5/2/3)": (1.5, 5.0, 2.0, 3, "base"),
    "ancho (3/8/3/3)": (3.0, 8.0, 3.0, 3, "base"),
    "simetrico por mecha (2/6/2/3)": (2.0, 6.0, 2.0, 3, "mecha"),
    "simetrico por cierre (2/6/2/3)": (2.0, 6.0, 2.0, 3, "cierre"),
    "operador CLAUDE.md (+-2, 6 pts en 3 velas) = base": (2.0, 6.0, 2.0, 3, "base"),
}


def tasa_base_simulada(cal, sesiones=120, rayas=100):
    res = {}
    for wn, cv in cal.items():
        n_velas = 405 if wn == "noche" else 195          # 13,5 h y 6,5 h en velas de 2 min
        res[wn] = {}
        for nombre, (tol, reb, cruz, mirar, modo) in VARIANTES.items():
            R = T = I = 0
            for s in range(sesiones):
                o, h, l, c = simular_velas(n_velas, cv["sd_cierre"])
                lo, hi = l.min(), h.max()
                for L in RNG.uniform(lo, hi, rayas):
                    r, t, i = juzgar(o, h, l, c, float(L), tol, reb, cruz, mirar, modo)
                    R += r; T += t; I += i
            n = R + T + I
            res[wn][nombre] = dict(toques=n, rebote_pct=round(100 * R / n, 2), traspaso_pct=round(100 * T / n, 2),
                                   indef_pct=round(100 * I / n, 2), ruina_jugador_pct=round(100 * cruz / (reb + cruz), 1))
        # chequeo de la calibracion: rango medio simulado vs real
        o, h, l, c = simular_velas(5000, cv["sd_cierre"])
        res[wn]["_calibracion"] = dict(sd_cierre_real=round(cv["sd_cierre"], 3), rango_medio_real=round(cv["rango_medio"], 3),
                                       rango_medio_simulado=round(float((h - l).mean()), 3))
    return res


# ---------------------------------------------------------------------------------------------------------------- potencia
def n_una_muestra(p0, d, alfa, potencia=0.8):
    """Toques para detectar p1 = p0 + d contra una base p0 conocida (test unilateral, aproximacion normal)."""
    p1 = p0 + d
    za, zb = Z(1 - alfa), Z(potencia)
    return math.ceil(((za * math.sqrt(p0 * (1 - p0)) + zb * math.sqrt(p1 * (1 - p1))) / d) ** 2)


def n_dos_muestras(p0, d, alfa, potencia=0.8, k=1.0):
    """Toques de la formula (y k veces mas del placebo) para detectar p1 - p0 = d (unilateral)."""
    p1 = p0 + d
    pb = (p1 + k * p0) / (1 + k)
    za, zb = Z(1 - alfa), Z(potencia)
    num = za * math.sqrt(pb * (1 - pb) * (1 + 1 / k)) + zb * math.sqrt(p1 * (1 - p1) + p0 * (1 - p0) / k)
    return math.ceil((num / d) ** 2)


def tabla_potencia():
    filas = []
    for p0 in (0.50, 0.56, 0.65):
        for d in (0.03, 0.05, 0.08, 0.10, 0.15):
            for nf in (1, 20, 72):
                a = 0.05 / nf
                n1 = n_una_muestra(p0, d, a)
                n2 = n_dos_muestras(p0, d, a, k=1.0)
                for icc in (0.0, 0.02, 0.05):
                    m = 30                                   # toques por sesion de una familia de 2 rayas (orden medido en extremos_dibujado)
                    de = 1 + (m - 1) * icc
                    filas.append(dict(p0=p0, d_pp=round(100 * d), formulas=nf, alfa=round(a, 5), icc=icc, toques_por_sesion=m,
                                      n_vs_base_fija=math.ceil(n1 * de), n_vs_placebo_igual_tamano=math.ceil(n2 * de),
                                      sesiones_vs_base_fija=math.ceil(n1 * de / m)))
    return filas


def md(cal, sim, pot):
    L = ["# Tasa base del juez y potencia (soportes_metodo)", "",
         "Generado por `base_y_potencia.py` (09-10-2026). Velas reales SOLO para calibrar la volatilidad: `%s`." % os.path.relpath(VELAS, AQUI), ""]
    L.append("## 1. Cuanto 'rebota' un paseo al azar con el juez del laboratorio")
    L.append("")
    for wn in ("noche", "dia"):
        cv = sim[wn]["_calibracion"]
        L.append("**%s** -- sd del cierre m2 real %.2f pts; rango medio real %.2f vs simulado %.2f (el real tiene colas y agrupamiento de volatilidad que el Browniano no tiene)." %
                 (wn, cv["sd_cierre_real"], cv["rango_medio_real"], cv["rango_medio_simulado"]))
        L.append("")
        L.append("| variante del juez | toques | rebote % | traspaso % | indef/ambiguo % | ruina del jugador C/(R+C) % |")
        L.append("|---|---|---|---|---|---|")
        for k, v in sim[wn].items():
            if k.startswith("_"):
                continue
            L.append("| %s | %d | %.1f | %.1f | %.1f | %.1f |" % (k, v["toques"], v["rebote_pct"], v["traspaso_pct"], v["indef_pct"], v["ruina_jugador_pct"]))
        L.append("")
    L.append("## 2. Potencia (unilateral, 80 %, aproximacion normal; m = 30 toques por sesion)")
    L.append("")
    L.append("| p0 | +pp | formulas probadas | alfa | ICC | toques vs base fija | toques vs placebo del mismo tamano | sesiones vs base fija |")
    L.append("|---|---|---|---|---|---|---|---|")
    for f in pot:
        L.append("| %.2f | %d | %d | %.5f | %.2f | %d | %d | %d |" % (f["p0"], f["d_pp"], f["formulas"], f["alfa"], f["icc"],
                                                                 f["n_vs_base_fija"], f["n_vs_placebo_igual_tamano"], f["sesiones_vs_base_fija"]))
    return "\n".join(L) + "\n"


def main():
    cal = calibrar()
    sim = tasa_base_simulada(cal)
    pot = tabla_potencia()
    out = dict(calibracion=cal, simulacion=sim, potencia=pot, semilla=20261009)
    with open(os.path.join(AQUI, "base_y_potencia.json"), "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=1)
    with open(os.path.join(AQUI, "base_y_potencia.md"), "w", encoding="utf-8") as f:
        f.write(md(cal, sim, pot))
    for wn in ("noche", "dia"):
        print(wn, json.dumps(sim[wn], ensure_ascii=False))


if __name__ == "__main__":
    main()
