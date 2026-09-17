# -*- coding: utf-8 -*-
"""
cvd_previews.py — maquetas del "CVD superador" con velas REALES grabadas por Gamma Hoy (MNQZ6 M2 del 16-09).
Cada imagen tiene arriba el precio y abajo el panel con el MISMO alto que usa hoy el operador (~140 px de 620),
para comparar legibilidad a igual espacio. No es el indicador: es como se veria.

Uso:  python laboratorio/cvd_previews.py <carpeta_salida> [dia AAAA-MM-DD]
"""
import json, math, os, sys, statistics as st
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle
from matplotlib.colors import LinearSegmentedColormap

A = os.path.join(os.environ.get("APPDATA", ""), "ATAS")
FONDO, REJA, TXT = "#151924", "#232838", "#b8c0d0"
VERDE, ROJO, AMBAR, CEL = "#26a69a", "#ef5350", "#ffc857", "#5ab4ff"


def cargar(dia):
    vs = []
    for nombre in ("pythiagex-centinela-hoy-MNQZ6-TimeFrame-M2.jsonl", "pythiagex-centinela-hoy-MNQ-TimeFrame-M2.jsonl"):
        p = os.path.join(A, nombre)
        if not os.path.exists(p): continue
        for l in open(p, encoding="utf-8", errors="replace"):
            try: j = json.loads(l)
            except Exception: continue
            if not j.get("t", "").startswith(dia) or j.get("delta") is None: continue
            of = j.get("of") or {}
            vs.append(dict(t=j["t"], o=j["o"], h=j["h"], l=j["l"], c=j["c"], vol=j["vol"], d=j["delta"],
                           dmax=of.get("dmax", j["delta"]), dmin=of.get("dmin", j["delta"]), bb=of.get("big_buy", 0) or 0, bs=of.get("big_sell", 0) or 0))
    vs.sort(key=lambda v: v["t"]); out, visto = [], set()
    for v in vs:
        if v["t"] in visto: continue
        visto.add(v["t"]); out.append(v)
    return out


def ema(xs, n):
    k = 2.0 / (n + 1); e = xs[0]; out = []
    for x in xs: e = e + k * (x - e); out.append(e)
    return out


def ejes(titulo, alto_panel=1.0):
    fig = plt.figure(figsize=(14, 6.2), dpi=100, facecolor=FONDO)
    gs = fig.add_gridspec(2, 1, height_ratios=[3.3, alto_panel], hspace=0.03, left=0.03, right=0.93, top=0.93, bottom=0.06)
    ap, ab = fig.add_subplot(gs[0]), fig.add_subplot(gs[1])
    for a in (ap, ab):
        a.set_facecolor(FONDO); a.tick_params(colors=TXT, labelsize=8); a.yaxis.tick_right()
        for s in a.spines.values(): s.set_color(REJA)
        a.grid(color=REJA, lw=0.6)
    ap.set_xticklabels([]); fig.suptitle(titulo, color="white", fontsize=12, x=0.03, ha="left", y=0.985)
    return fig, ap, ab


def velas(ax, vs, ancho=0.62):
    for i, v in enumerate(vs):
        col = VERDE if v["c"] >= v["o"] else ROJO
        ax.plot([i, i], [v["l"], v["h"]], color=col, lw=0.9, zorder=2)
        ax.add_patch(Rectangle((i - ancho / 2, min(v["o"], v["c"])), ancho, max(abs(v["c"] - v["o"]), 0.25), color=col, zorder=3))
    ax.set_xlim(-1, len(vs) + 6)
    ax.set_ylim(min(v["l"] for v in vs) - 6, max(v["h"] for v in vs) + 6)


def horas(ax, vs):
    # la vela trae su hora de CIERRE (14:29:59): se rotula la media hora que cierra, en hora local (UTC-3)
    ticks = [i for i, v in enumerate(vs) if (int(v["t"][14:16]) + 1) % 30 == 0]
    def rot(v):
        m = int(v["t"][11:13]) * 60 + int(v["t"][14:16]) + 1 - 180
        return "%02d:%02d" % ((m // 60) % 24, m % 60)
    ax.set_xticks(ticks); ax.set_xticklabels([rot(vs[i]) for i in ticks], color=TXT, fontsize=8)


def main():
    salida = sys.argv[1]; dia = sys.argv[2] if len(sys.argv) > 2 else "2026-09-16"
    os.makedirs(salida, exist_ok=True)
    todo = cargar(dia)
    rueda = [v for v in todo if "13:30" <= v["t"][11:16] <= "20:00"]
    # CVD del dia entero (como hoy) y ventana visible: las ultimas 150 velas (5 h en M2)
    cvd_dia, s = [], 0
    for v in todo: s += v["d"]; cvd_dia.append(s)
    i0 = todo.index(rueda[-150]) if len(rueda) >= 150 else todo.index(rueda[0])
    vis = todo[i0:i0 + 150]; cvd_vis = cvd_dia[i0:i0 + 150]; n = len(vis)
    d = [v["d"] for v in vis]
    pres = [a - b for a, b in zip(ema(d, 3), ema(d, 10))]
    z = []
    dd = [v["d"] for v in todo]
    for k in range(i0, i0 + n):
        w = dd[max(0, k - 59):k + 1]; s5 = sum(dd[max(0, k - 4):k + 1]); sd = st.pstdev(w) if len(w) > 5 else 0
        z.append(max(-3, min(3, s5 / (sd * math.sqrt(5)) if sd > 0 else 0)))
    dabs = sorted(abs(x) for x in dd); p80 = dabs[int(0.8 * len(dabs))]

    # ---------- 0) COMO SE VE HOY
    fig, ap, ab = ejes("HOY  ·  CVD de fabrica (velas, acumulado de todo el dia) en un panel de ~140 px  ·  MNQZ6 2 min, %s" % dia)
    velas(ap, vis)
    for i in range(n):
        a = cvd_vis[i] - vis[i]["d"]; c = cvd_vis[i]; col = VERDE if c >= a else ROJO
        ab.plot([i, i], [a + vis[i]["dmin"], a + vis[i]["dmax"]], color=col, lw=0.8)
        ab.add_patch(Rectangle((i - 0.31, min(a, c)), 0.62, max(abs(c - a), 1), color=col))
    ab.set_xlim(-1, n + 6); ab.set_ylim(min(cvd_dia) * 1.02 - 50, max(cvd_dia) * 1.02 + 50); horas(ab, vis)
    ab.text(0.005, 0.86, "CVD - Cumulative Volume Delta (Bars)", transform=ab.transAxes, color=TXT, fontsize=9)
    usado = (max(cvd_vis[-30:]) - min(cvd_vis[-30:])) / (max(cvd_dia) - min(cvd_dia))
    ab.text(0.62, 0.08, "la ultima hora usa el %.0f %% del alto del panel" % (100 * usado), transform=ab.transAxes, color=AMBAR, fontsize=10)
    fig.savefig(os.path.join(salida, "cvd_0_hoy.png"), facecolor=FONDO); plt.close(fig)

    # ---------- A) CVD ANCLADO + PRESION con bandas z
    fig, ap, ab = ejes("PROPUESTA A  ·  CVD anclado a lo visible + presion (delta rapido - delta lento) con bandas fijas  ·  mismo alto de panel")
    velas(ap, vis)
    base = cvd_vis[0]; anc = [c - base for c in cvd_vis]
    ab2 = ab.twinx(); ab2.set_facecolor("none"); ab2.tick_params(colors=TXT, labelsize=8); ab2.yaxis.tick_left(); ab.yaxis.tick_right()
    for s_ in ab2.spines.values(): s_.set_color(REJA)
    ab.bar(range(n), z, width=0.8, color=[VERDE if x >= 0 else ROJO for x in z], alpha=0.55, zorder=2)
    for lvl, a_ in ((1, 0.5), (2, 0.8)):
        ab.axhline(lvl, color=TXT, lw=0.5, ls=":", alpha=a_); ab.axhline(-lvl, color=TXT, lw=0.5, ls=":", alpha=a_)
    ab.axhline(0, color=TXT, lw=0.6); ab.set_ylim(-3.2, 3.2); ab.set_yticks([-2, -1, 0, 1, 2]); ab.set_yticklabels(["-2σ", "-1σ", "0", "+1σ", "+2σ"])
    for i in range(1, n):
        ab2.plot([i - 1, i], [anc[i - 1], anc[i]], color=(VERDE if anc[i] >= anc[i - 1] else ROJO), lw=2.2, zorder=5, solid_capstyle="round")
    m = max(abs(min(anc)), abs(max(anc))) * 1.08; ab2.set_ylim(-m, m); ab2.set_xlim(-1, n + 6); ab.set_xlim(-1, n + 6); horas(ab, vis)
    ab.text(0.005, 0.86, "CVD anclado (linea gruesa, escala izquierda)  ·  presion en sigmas (barras, escala derecha)", transform=ab.transAxes, color=TXT, fontsize=9)
    ap.text(0.995, 0.965, "Δ vela  %+d\nΔ 10 min  %+d\npresion  %+.1fσ" % (d[-1], sum(d[-5:]), z[-1]), transform=ap.transAxes, color="white", fontsize=11, ha="right", va="top",
            bbox=dict(boxstyle="round,pad=0.4", fc="#0e1118", ec=REJA))
    fig.savefig(os.path.join(salida, "cvd_A_anclado_presion.png"), facecolor=FONDO); plt.close(fig)

    # ---------- B) CINTAS bajo el precio (sin agrandar nada): presion, grandes, absorcion
    fig, ap, ab = ejes("PROPUESTA B  ·  cintas de calor pegadas al precio (presion / operaciones grandes / absorcion): se lee sin panel", alto_panel=0.62)
    velas(ap, vis)
    cm = LinearSegmentedColormap.from_list("pr", [ROJO, "#3a1f24", FONDO, "#16332f", VERDE])
    big = []; s = 0
    for v in vis: big.append(v["bb"] - v["bs"])
    bmax = max(1, sorted(abs(x) for x in big)[int(0.95 * len(big))])
    filas = [("presion (σ)", [max(-1, min(1, x / 1.8)) for x in z]), ("grandes", [max(-1, min(1, x / bmax)) for x in big])]
    ab.set_ylim(0, 3); ab.set_xlim(-1, n + 6); ab.set_yticks([]); ab.grid(False)
    for r, (nombre, xs) in enumerate(filas):
        y = 2 - r
        for i, x in enumerate(xs): ab.add_patch(Rectangle((i - 0.5, y + 0.08), 1.0, 0.84, color=cm((x + 1) / 2)))
        ab.text(n + 0.8, y + 0.5, nombre, color=TXT, fontsize=9, va="center")
    # fila 3: absorcion (mucho delta, vela chica o en contra) como marcas
    rng = [v["h"] - v["l"] for v in vis]; med = st.median(rng)
    for i, v in enumerate(vis):
        if abs(v["d"]) >= p80 and ((v["c"] - v["o"]) * v["d"] <= 0 or rng[i] <= 0.6 * med):
            ab.add_patch(Rectangle((i - 0.5, 0.08), 1.0, 0.84, color=AMBAR, alpha=0.9))
            ap.plot(i, v["l"] - 3 if v["d"] < 0 else v["h"] + 3, marker="D", color=AMBAR, ms=5, zorder=6)
    ab.text(n + 0.8, 0.5, "absorcion", color=AMBAR, fontsize=9, va="center"); horas(ab, vis)
    fig.savefig(os.path.join(salida, "cvd_B_cintas.png"), facecolor=FONDO); plt.close(fig)

    # ---------- C) VELAS DE DELTA CON MECHA, ancladas por hora + divergencias marcadas sobre el precio
    fig, ap, ab = ejes("PROPUESTA C  ·  velas de delta con mecha (max/min dentro de la vela), reinicio cada hora + divergencias marcadas EN EL PRECIO")
    velas(ap, vis)
    acc = 0; hora = None; cv = []
    for i, v in enumerate(vis):
        if v["t"][11:13] != hora:
            hora = v["t"][11:13]; acc = 0; ab.axvline(i - 0.5, color=REJA, lw=1.0)
        a = acc; acc += v["d"]; cv.append(acc); col = VERDE if acc >= a else ROJO
        ab.plot([i, i], [a + v["dmin"], a + v["dmax"]], color=col, lw=1.0)
        ab.add_patch(Rectangle((i - 0.33, min(a, acc)), 0.66, max(abs(acc - a), 2), color=col))
    ab.axhline(0, color=TXT, lw=0.6); ab.set_xlim(-1, n + 6); horas(ab, vis)
    ab.text(0.005, 0.86, "delta acumulado de la hora en curso (velas con mecha): la escala siempre es la de ahora", transform=ab.transAxes, color=TXT, fontsize=9)
    # divergencias simples: maximo de 20 velas en precio con CVD anclado por debajo del maximo anterior (y al reves)
    anc = [c - cvd_vis[0] for c in cvd_vis]; ult_max = ult_min = None
    for i in range(20, n):
        if vis[i]["h"] >= max(v["h"] for v in vis[i - 20:i + 1]):
            if ult_max is not None and i - ult_max[0] >= 5 and anc[i] < ult_max[1]: ap.plot(i, vis[i]["h"] + 5, marker="v", color=ROJO, ms=8, zorder=7)
            ult_max = (i, anc[i])
        if vis[i]["l"] <= min(v["l"] for v in vis[i - 20:i + 1]):
            if ult_min is not None and i - ult_min[0] >= 5 and anc[i] > ult_min[1]: ap.plot(i, vis[i]["l"] - 5, marker="^", color=VERDE, ms=8, zorder=7)
            ult_min = (i, anc[i])
    ap.text(0.005, 0.95, "▼ precio hace maximo nuevo y el CVD no  ·  ▲ precio hace minimo nuevo y el CVD no   (marca de LECTURA, no validada como señal)", transform=ap.transAxes, color=TXT, fontsize=9)
    fig.savefig(os.path.join(salida, "cvd_C_velas_mecha_divergencias.png"), facecolor=FONDO); plt.close(fig)
    # ---------- D) LA QUE YO HARIA: panel A + marcas en el precio (absorcion y divergencia) + lectura grande
    fig, ap, ab = ejes("PROPUESTA D (la que yo haria)  ·  CVD anclado + presion en el panel, y absorcion / divergencias marcadas en el precio  ·  mismo alto de panel")
    velas(ap, vis)
    anc = [c - cvd_vis[0] for c in cvd_vis]
    ab2 = ab.twinx(); ab2.set_facecolor("none"); ab2.tick_params(colors=TXT, labelsize=8); ab2.yaxis.tick_left(); ab.yaxis.tick_right()
    for s_ in ab2.spines.values(): s_.set_color(REJA)
    cols = [VERDE if x >= 0 else ROJO for x in z]
    ab.bar(range(n), z, width=0.8, color=cols, alpha=0.5, zorder=2)
    ab.bar([n - 1], [z[-1]], width=0.8, color=cols[-1], alpha=0.95, edgecolor="white", linewidth=1.2, zorder=3)
    for lvl, a_ in ((1, 0.5), (2, 0.8)):
        ab.axhline(lvl, color=TXT, lw=0.5, ls=":", alpha=a_); ab.axhline(-lvl, color=TXT, lw=0.5, ls=":", alpha=a_)
    ab.axhline(0, color=TXT, lw=0.6); ab.set_ylim(-3.2, 3.2); ab.set_yticks([-2, -1, 0, 1, 2]); ab.set_yticklabels(["-2σ", "-1σ", "0", "+1σ", "+2σ"])
    for i in range(1, n):
        ab2.plot([i - 1, i], [anc[i - 1], anc[i]], color=(VERDE if anc[i] >= anc[i - 1] else ROJO), lw=2.4, zorder=5, solid_capstyle="round")
    m = max(abs(min(anc)), abs(max(anc))) * 1.08; ab2.set_ylim(-m, m); ab2.set_xlim(-1, n + 6); ab.set_xlim(-1, n + 6); horas(ab, vis)
    ab.text(0.005, 0.86, "CVD anclado (linea)  ·  presion en sigmas (barras; la ultima se actualiza tick a tick)", transform=ab.transAxes, color=TXT, fontsize=9)
    for i, v in enumerate(vis):
        if abs(v["d"]) >= p80 and ((v["c"] - v["o"]) * v["d"] <= 0 or rng[i] <= 0.6 * med):
            ap.plot(i, v["l"] - 4 if v["d"] < 0 else v["h"] + 4, marker="D", color=AMBAR, ms=6, zorder=6)
    ult_max = ult_min = None
    for i in range(20, n):
        if vis[i]["h"] >= max(v["h"] for v in vis[i - 20:i + 1]):
            if ult_max is not None and i - ult_max[0] >= 5 and anc[i] < ult_max[1]: ap.plot(i, vis[i]["h"] + 9, marker="v", color=ROJO, ms=8, zorder=7)
            ult_max = (i, anc[i])
        if vis[i]["l"] <= min(v["l"] for v in vis[i - 20:i + 1]):
            if ult_min is not None and i - ult_min[0] >= 5 and anc[i] > ult_min[1]: ap.plot(i, vis[i]["l"] - 9, marker="^", color=VERDE, ms=8, zorder=7)
            ult_min = (i, anc[i])
    ap.text(0.005, 0.95, "◆ absorcion: mucho delta y la vela no avanza   ▼▲ divergencia precio / CVD   (marcas de lectura; se miden contra placebo antes de llamarlas señal)", transform=ap.transAxes, color=TXT, fontsize=9)
    ap.text(0.995, 0.965, ("Δ vela  %+d" + chr(10) + "Δ 10 min  %+d" + chr(10) + "presion  %+.1fσ") % (d[-1], sum(d[-5:]), z[-1]), transform=ap.transAxes, color="white", fontsize=11, ha="right", va="top",
            bbox=dict(boxstyle="round,pad=0.4", fc="#0e1118", ec=REJA))
    fig.savefig(os.path.join(salida, "cvd_D_recomendada.png"), facecolor=FONDO); plt.close(fig)
    print("listo:", salida, "| velas visibles", n, "| ultima hora usa", round(100 * usado), "% del panel hoy")


if __name__ == "__main__":
    main()
