# -*- coding: utf-8 -*-
"""
cvd_fusion.py — maquetas de la FUSION D+B para scalping en 1 minuto, con velas reales grabadas por Gamma Hoy
(pythiagex-centinela-hoy-MNQ-TimeFrame-M1.jsonl). Tambien mide la "confluencia" contra placebo antes de dibujarla.

Uso:  python laboratorio/cvd_fusion.py <carpeta_salida> [dia AAAA-MM-DD] [--medir]
"""
import json, math, os, random, sys, statistics as st
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle, FancyBboxPatch
from matplotlib.colors import LinearSegmentedColormap

A = os.path.join(os.environ.get("APPDATA", ""), "ATAS")
FONDO, REJA, TXT = "#151924", "#232838", "#b8c0d0"
VERDE, ROJO, AMBAR, CEL, GRIS = "#26a69a", "#ef5350", "#ffc857", "#5ab4ff", "#3a4052"
CM = LinearSegmentedColormap.from_list("pr", [ROJO, "#3a1f24", FONDO, "#16332f", VERDE])
CMV = LinearSegmentedColormap.from_list("vel", [FONDO, "#4a3b12", AMBAR])


def cargar_m1():
    vs = []
    for nombre in ("pythiagex-centinela-hoy-MNQ-TimeFrame-M1.jsonl", "pythiagex-centinela-hoy-MNQZ6-TimeFrame-M1.jsonl"):
        p = os.path.join(A, nombre)
        if not os.path.exists(p): continue
        for l in open(p, encoding="utf-8", errors="replace"):
            try: j = json.loads(l)
            except Exception: continue
            if j.get("delta") is None or not j.get("vol"): continue
            of = j.get("of") or {}
            vs.append(dict(t=j["t"], o=j["o"], h=j["h"], l=j["l"], c=j["c"], vol=j["vol"], ops=j.get("ops") or j["vol"], d=j["delta"],
                           dmax=of.get("dmax", j["delta"]), dmin=of.get("dmin", j["delta"]), bb=of.get("big_buy", 0) or 0, bs=of.get("big_sell", 0) or 0))
    vs.sort(key=lambda v: v["t"]); out, visto = [], set()
    for v in vs:
        if v["t"] in visto: continue
        visto.add(v["t"]); out.append(v)
    return out


def series(todo):
    """Todo lo que se dibuja, calculado sobre la serie completa (sin mirar adelante)."""
    n = len(todo); d = [v["d"] for v in todo]
    cvd, s, dia = [], 0, None
    for v in todo:
        if v["t"][:10] != dia: dia = v["t"][:10]; s = 0
        s += v["d"]; cvd.append(s)
    z, vel = [], []
    for k in range(n):
        w = d[max(0, k - 59):k + 1]; s5 = sum(d[max(0, k - 4):k + 1]); sd = st.pstdev(w) if len(w) > 5 else 0
        z.append(max(-3, min(3, s5 / (sd * math.sqrt(5)) if sd > 0 else 0)))
        wo = [v["ops"] for v in todo[max(0, k - 59):k + 1]]; so = st.pstdev(wo) if len(wo) > 5 else 0
        vel.append(max(0, min(3, (todo[k]["ops"] - st.mean(wo)) / so if so > 0 else 0)))
    big = [v["bb"] - v["bs"] for v in todo]
    bmax = max(1, sorted(abs(x) for x in big)[int(0.95 * n)])
    dabs = sorted(abs(x) for x in d); p80 = dabs[int(0.8 * n)]
    rng = [v["h"] - v["l"] for v in todo]; med = st.median(rng)
    absor = []
    for i, v in enumerate(todo):
        w = [abs(x["c"] - x["o"]) / abs(x["d"]) for x in todo[max(0, i - 120):i] if abs(x["d"]) >= 50]
        beta = st.median(w) if len(w) >= 20 else None          # puntos de precio por contrato de delta, lo habitual de las ultimas 2 h
        w80 = sorted(abs(x["d"]) for x in todo[max(0, i - 59):i + 1]); p80m = w80[min(len(w80) - 1, int(len(w80) * 0.8))]   # movil, sin mirar adelante
        if beta is None or abs(v["d"]) < p80m or p80m <= 0: absor.append(False); continue
        avance = (v["c"] - v["o"]) * (1 if v["d"] > 0 else -1)   # cuanto avanzo el precio EN EL SENTIDO del delta
        absor.append(avance <= 0.25 * beta * abs(v["d"]))
    # confluencia: cuantas de las cuatro lecturas de flujo apuntan al mismo lado (-4..+4)
    conf = []
    for k in range(n):
        a = (1 if z[k] > 0.5 else -1 if z[k] < -0.5 else 0)
        b = (1 if big[k] > 0.3 * bmax else -1 if big[k] < -0.3 * bmax else 0)
        c = (1 if k >= 20 and cvd[k] > cvd[k - 20] else -1 if k >= 20 and cvd[k] < cvd[k - 20] else 0)   # 20 velas: con 5 era la misma cuenta que la presion
        dp = todo[k]["d"] / max(1, todo[k]["vol"]); e = (1 if dp > 0.05 else -1 if dp < -0.05 else 0)
        conf.append(a + b + c + e)
    return dict(cvd=cvd, z=z, vel=vel, big=big, bmax=bmax, absor=absor, conf=conf, p80=p80)


def divergencias(vis, anc):
    out = []; ult_max = ult_min = None
    for i in range(20, len(vis)):
        if vis[i]["h"] >= max(v["h"] for v in vis[i - 20:i + 1]):
            if ult_max is not None and i - ult_max[0] >= 5 and anc[i] < ult_max[1]: out.append((i, -1))
            ult_max = (i, anc[i])
        if vis[i]["l"] <= min(v["l"] for v in vis[i - 20:i + 1]):
            if ult_min is not None and i - ult_min[0] >= 5 and anc[i] > ult_min[1]: out.append((i, +1))
            ult_min = (i, anc[i])
    return out


def medir(todo, S):
    print("CONFLUENCIA contra placebo (retorno a 5 velas de 1 min en el sentido de la lectura, en pb):")
    def ret(i, s, n=5):
        if i + n >= len(todo) or todo[i + n]["t"][:10] != todo[i]["t"][:10]: return None
        return s * (todo[i + n]["c"] - todo[i]["c"]) / todo[i]["c"] * 1e4
    random.seed(11)
    for umbral in (3, 4):
        ss = [(i, 1 if S["conf"][i] > 0 else -1) for i in range(1, len(todo)) if abs(S["conf"][i]) >= umbral and abs(S["conf"][i - 1]) < umbral]
        rs = [r for r in (ret(i, s) for i, s in ss) if r is not None]
        if len(rs) < 20: print("  |confluencia| >= %d: n %d, muestra corta" % (umbral, len(rs))); continue
        plac = []
        for _ in range(300):
            m = [ret(random.randrange(len(todo) - 6), random.choice((1, -1))) for _i in range(len(rs))]; m = [x for x in m if x is not None]
            plac.append(sum(m) / len(m))
        media = sum(rs) / len(rs); pm = sum(plac) / len(plac); ps = st.pstdev(plac)
        print("  |confluencia| >= %d: n %3d | acierta %4.1f %% | media %+5.2f pb | azar %+5.2f +- %4.2f | z %+4.1f" % (
            umbral, len(rs), 100.0 * sum(1 for r in rs if r > 0) / len(rs), media, pm, ps, (media - pm) / ps if ps else 0))


# ------------------------------------------------------------------ dibujo
def lienzo(titulo, filas, con_ahora=True):
    """filas = lista de (nombre, alto_relativo). Devuelve fig y dict de ejes; columna 'ahora' a la derecha."""
    fig = plt.figure(figsize=(14, 6.6), dpi=100, facecolor=FONDO)
    gs = fig.add_gridspec(len(filas), 2 if con_ahora else 1, height_ratios=[f[1] for f in filas], width_ratios=[12, 1.55] if con_ahora else [1],
                          hspace=0.035, wspace=0.085, left=0.045, right=0.988 if con_ahora else 0.93, top=0.93, bottom=0.055)
    ejes = {}
    for r, (nombre, _) in enumerate(filas):
        a = fig.add_subplot(gs[r, 0]); ejes[nombre] = a
        a.set_facecolor(FONDO); a.tick_params(colors=TXT, labelsize=8); a.yaxis.tick_right()
        for s in a.spines.values(): s.set_color(REJA)
        a.grid(color=REJA, lw=0.6)
        if r < len(filas) - 1: a.set_xticklabels([])
    if con_ahora:
        a = fig.add_subplot(gs[:, 1]); a.set_facecolor("#0e1118"); a.set_xticks([]); a.set_yticks([]); a.set_xlim(0, 1); a.set_ylim(0, 1)
        for s in a.spines.values(): s.set_color(REJA)
        ejes["ahora"] = a
    fig.suptitle(titulo, color="white", fontsize=12, x=0.025, ha="left", y=0.985)
    return fig, ejes


def velas(ax, vs, ancho=0.62, pad=8):
    for i, v in enumerate(vs):
        col = VERDE if v["c"] >= v["o"] else ROJO
        ax.plot([i, i], [v["l"], v["h"]], color=col, lw=1.0, zorder=2)
        ax.add_patch(Rectangle((i - ancho / 2, min(v["o"], v["c"])), ancho, max(abs(v["c"] - v["o"]), 0.25), color=col, zorder=3))
    ax.set_xlim(-1, len(vs) + 1); ax.set_ylim(min(v["l"] for v in vs) - pad, max(v["h"] for v in vs) + pad)


def horas(ax, vs, cada=15):
    ticks = [i for i, v in enumerate(vs) if (int(v["t"][14:16]) + 1) % cada == 0]
    def rot(v):
        m = int(v["t"][11:13]) * 60 + int(v["t"][14:16]) + 1 - 180
        return "%02d:%02d" % ((m // 60) % 24, m % 60)
    ax.set_xticks(ticks); ax.set_xticklabels([rot(vs[i]) for i in ticks], color=TXT, fontsize=8)


def cintas(ax, n, filas, rotulos_derecha=True):
    """filas = [(nombre, valores -1..1 o 0..1, cmap, color_rotulo)]"""
    ax.set_ylim(0, len(filas)); ax.set_xlim(-1, n + 1); ax.set_yticks([]); ax.grid(False)
    for r, (nombre, xs, cm, colr) in enumerate(filas):
        y = len(filas) - 1 - r
        for i, x in enumerate(xs):
            if x is None: continue
            ax.add_patch(Rectangle((i - 0.5, y + 0.07), 1.0, 0.86, color=cm(x)))
        ax.text(0.2, y + 0.5, nombre, color=colr, fontsize=8.5, va="center", ha="left",
                bbox=dict(boxstyle="round,pad=0.15", fc="#0e1118", ec="none", alpha=0.75))


def marcas(ax, vis, absor, divs, paso=5):
    for i, v in enumerate(vis):
        if absor[i]: ax.plot(i, v["l"] - paso if v["d"] < 0 else v["h"] + paso, marker="D", color=AMBAR, ms=6, zorder=6)
    for i, s in divs:
        ax.plot(i, vis[i]["h"] + 2.2 * paso if s < 0 else vis[i]["l"] - 2.2 * paso, marker="v" if s < 0 else "^", color=ROJO if s < 0 else VERDE, ms=9, zorder=7)


def panel_presion(ax, z, anc, n, rotulo=True):
    ax2 = ax.twinx(); ax2.set_facecolor("none"); ax2.tick_params(colors=TXT, labelsize=8); ax2.yaxis.tick_left(); ax.yaxis.tick_right()
    for s_ in ax2.spines.values(): s_.set_color(REJA)
    cols = [VERDE if x >= 0 else ROJO for x in z]
    ax.bar(range(n), z, width=0.8, color=cols, alpha=0.5, zorder=2)
    ax.bar([n - 1], [z[-1]], width=0.8, color=cols[-1], alpha=0.95, edgecolor="white", linewidth=1.2, zorder=3)
    for lvl, a_ in ((1, 0.5), (2, 0.8)):
        ax.axhline(lvl, color=TXT, lw=0.5, ls=":", alpha=a_); ax.axhline(-lvl, color=TXT, lw=0.5, ls=":", alpha=a_)
    ax.axhline(0, color=TXT, lw=0.6); ax.set_ylim(-3.2, 3.2); ax.set_yticks([-2, -1, 0, 1, 2]); ax.set_yticklabels(["-2σ", "-1σ", "0", "+1σ", "+2σ"])
    for i in range(1, n):
        ax2.plot([i - 1, i], [anc[i - 1], anc[i]], color=(VERDE if anc[i] >= anc[i - 1] else ROJO), lw=2.4, zorder=5, solid_capstyle="round")
    m = max(abs(min(anc)), abs(max(anc))) * 1.08 or 1; ax2.set_ylim(-m, m); ax2.set_xlim(-1, n + 1); ax.set_xlim(-1, n + 1)
    if rotulo: ax.text(0.004, 0.84, "CVD anclado (linea)  ·  presion en sigmas (barras; la ultima se mueve tick a tick)", transform=ax.transAxes, color=TXT, fontsize=8.5)


def columna_ahora(ax, vis, S, i0, titulo="AHORA", checklist=True):
    k = i0 + len(vis) - 1; v = vis[-1]
    dp1 = v["d"] / max(1, v["vol"]); d5 = sum(x["d"] for x in vis[-5:]); v5 = sum(x["vol"] for x in vis[-5:]); dp5 = d5 / max(1, v5)
    fin = (v["d"] - v["dmin"]) / max(1, v["dmax"] - v["dmin"]) * 2 - 1   # donde cerro el delta dentro de su recorrido: proxy del flujo del final de la vela
    ax.text(0.5, 0.965, titulo, color="white", fontsize=11, ha="center", va="top", weight="bold")
    ax.text(0.5, 0.925, "tick a tick", color=TXT, fontsize=8, ha="center", va="top")
    celdas = [("5 s", None), ("15 s", fin), ("60 s", max(-1, min(1, dp1 * 4))), ("5 min", max(-1, min(1, dp5 * 6)))]
    y = 0.86
    for nombre, x in celdas:
        col = GRIS if x is None else CM((x + 1) / 2)
        ax.add_patch(FancyBboxPatch((0.08, y - 0.075), 0.84, 0.07, boxstyle="round,pad=0.004,rounding_size=0.012", fc=col, ec=REJA, lw=0.8))
        ax.text(0.14, y - 0.04, nombre, color="white", fontsize=9, va="center")
        ax.text(0.86, y - 0.04, "vivo" if x is None else ("%+.0f %%" % (100 * x / (4 if nombre == "60 s" else 6 if nombre == "5 min" else 1))) if nombre != "15 s" else ("▲" if x > 0.2 else "▼" if x < -0.2 else "•"),
                color="white", fontsize=9, va="center", ha="right")
        y -= 0.085
    if not checklist: return
    z, big, conf = S["z"][k], S["big"][k], S["conf"][k]
    sube = S["cvd"][k] > S["cvd"][k - 5]
    lineas = [("PRESION", "%+.1fσ" % z, VERDE if z > 0.5 else ROJO if z < -0.5 else TXT),
              ("GRANDES", "%+d" % big, VERDE if big > 0 else ROJO if big < 0 else TXT),
              ("CVD 5 min", "sube" if sube else "baja", VERDE if sube else ROJO),
              ("CINTA", "rapida" if S["vel"][k] > 1 else "normal", AMBAR if S["vel"][k] > 1 else TXT),
              ("ABSORCION", "SI" if S["absor"][k] else "no", AMBAR if S["absor"][k] else TXT)]
    y = 0.50
    ax.plot([0.08, 0.92], [y + 0.035, y + 0.035], color=REJA, lw=1)
    for nombre, valor, col in lineas:
        ax.text(0.08, y, nombre, color=TXT, fontsize=8.5, va="center"); ax.text(0.92, y, valor, color=col, fontsize=10, va="center", ha="right", weight="bold")
        y -= 0.052
    ax.plot([0.08, 0.92], [y + 0.02, y + 0.02], color=REJA, lw=1)
    lado = "COMPRADOR" if conf > 0 else "VENDEDOR" if conf < 0 else "PAREJO"
    ax.text(0.5, y - 0.025, "flujo", color=TXT, fontsize=8.5, ha="center", va="center")
    ax.text(0.5, y - 0.075, lado, color=VERDE if conf > 0 else ROJO if conf < 0 else TXT, fontsize=13, ha="center", va="center", weight="bold")
    ax.text(0.5, y - 0.12, "%d de 4 lecturas" % abs(conf), color="white", fontsize=9.5, ha="center", va="center")
    if abs(conf) >= 3:
        ax.add_patch(FancyBboxPatch((0.06, y - 0.205), 0.88, 0.055, boxstyle="round,pad=0.004,rounding_size=0.012", fc="#3d3212", ec=AMBAR, lw=0.8))
        ax.text(0.5, y - 0.178, "EXTREMO: no perseguir", color=AMBAR, fontsize=8, ha="center", va="center", weight="bold")


def main():
    salida = sys.argv[1]; dia = next((a for a in sys.argv[2:] if a[:2] == "20"), "2026-09-10")
    os.makedirs(salida, exist_ok=True)
    todo = cargar_m1(); S = series(todo)
    if "--medir" in sys.argv: medir([v for v in todo], S)
    idx = [i for i, v in enumerate(todo) if v["t"][:10] == dia and "13:30" <= v["t"][11:16] <= "20:00"]
    # la ventana de 90 min con mas recorrido del dia (para que se vea un giro y una tendencia)
    rangos = {a: max(v["h"] for v in todo[a:a + 90]) - min(v["l"] for v in todo[a:a + 90]) for a in range(idx[0], idx[-1] - 90)}
    rmax = max(rangos.values()); mejor, i0 = -1, idx[0]
    for a, r in rangos.items():          # buen recorrido Y marcas a la vista, para que la maqueta muestre todo
        if r < 0.55 * rmax: continue
        puntos = sum(1 for x in S["absor"][a:a + 90] if x) + r / rmax
        if puntos > mejor: mejor, i0 = puntos, a
    vis = todo[i0:i0 + 90]; n = len(vis)
    anc = [c - S["cvd"][i0] for c in S["cvd"][i0:i0 + n]]
    z = S["z"][i0:i0 + n]; big = [max(-1, min(1, x / S["bmax"])) for x in S["big"][i0:i0 + n]]; vel = S["vel"][i0:i0 + n]
    absor = S["absor"][i0:i0 + n]; conf = S["conf"][i0:i0 + n]; divs = divergencias(vis, anc)
    f_pres = ("presion", [(max(-1, min(1, x / 1.8)) + 1) / 2 for x in z], CM, TXT)
    f_big = ("grandes", [(x + 1) / 2 for x in big], CM, TXT)
    f_vel = ("cinta", [x / 3 for x in vel], CMV, AMBAR)
    f_conf = ("confluencia", [(x / 4 + 1) / 2 for x in conf], CM, "white")
    f_abs = ("absorcion", [1.0 if a else None for a in absor], LinearSegmentedColormap.from_list("a", [AMBAR, AMBAR]), AMBAR)
    sub = "MNQ 1 min, %s  ·  90 minutos" % dia

    # F1 — TABLERO: cintas pegadas al precio + panel de presion/CVD + columna AHORA
    fig, E = lienzo("FUSION 1 · TABLERO  ·  cintas pegadas al precio + panel CVD/presion + columna AHORA  ·  " + sub, [("precio", 3.0), ("cintas", 0.55), ("panel", 1.0)])
    velas(E["precio"], vis); marcas(E["precio"], vis, absor, divs)
    cintas(E["cintas"], n, [f_pres, f_big, f_vel]); panel_presion(E["panel"], z, anc, n); horas(E["panel"], vis)
    E["precio"].text(0.004, 0.955, "◆ absorcion   ▼▲ divergencia precio / CVD", transform=E["precio"].transAxes, color=TXT, fontsize=9)
    columna_ahora(E["ahora"], vis, S, i0); fig.savefig(os.path.join(salida, "fusion_1_tablero.png"), facecolor=FONDO); plt.close(fig)

    # F2 — SEMAFORO: una sola cinta de confluencia arriba de todo + absorcion, panel igual
    fig, E = lienzo("FUSION 2 · SEMAFORO  ·  una cinta que resume (cuantas lecturas coinciden) + absorcion, y el panel debajo  ·  " + sub, [("precio", 3.0), ("cintas", 0.42), ("panel", 1.1)])
    velas(E["precio"], vis); marcas(E["precio"], vis, absor, divs)
    cintas(E["cintas"], n, [f_conf, f_abs]); panel_presion(E["panel"], z, anc, n); horas(E["panel"], vis)
    E["precio"].text(0.004, 0.955, "cinta intensa = las 4 lecturas de flujo del mismo lado  ·  ambar = absorcion (cuidado: el esfuerzo no mueve el precio)", transform=E["precio"].transAxes, color=TXT, fontsize=9)
    columna_ahora(E["ahora"], vis, S, i0); fig.savefig(os.path.join(salida, "fusion_2_semaforo.png"), facecolor=FONDO); plt.close(fig)

    # F3 — ZOOM SCALPER: 40 velas, velas de delta con mecha + numeros por vela + cintas
    m = 40; v3 = vis[-m:]; j0 = n - m
    fig, E = lienzo("FUSION 3 · ZOOM SCALPER  ·  40 velas: delta con mecha y numero por vela, cintas y columna AHORA  ·  MNQ 1 min", [("precio", 2.8), ("cintas", 0.55), ("panel", 1.15)])
    velas(E["precio"], v3, pad=5); marcas(E["precio"], v3, absor[j0:], [(i - j0, s) for i, s in divs if i >= j0], paso=3)
    cintas(E["cintas"], m, [("presion", f_pres[1][j0:], CM, TXT), ("grandes", f_big[1][j0:], CM, TXT), ("cinta", f_vel[1][j0:], CMV, AMBAR)])
    ax = E["panel"]; acc = 0
    for i, v in enumerate(v3):
        a = acc; acc += v["d"]; col = VERDE if acc >= a else ROJO
        ax.plot([i, i], [a + v["dmin"], a + v["dmax"]], color=col, lw=1.2)
        ax.add_patch(Rectangle((i - 0.33, min(a, acc)), 0.66, max(abs(acc - a), 3), color=col))
    lo, hi = ax.get_ylim(); ax.set_ylim(lo - (hi - lo) * 0.28, hi); lo2 = ax.get_ylim()[0]
    for i, v in enumerate(v3): ax.text(i, lo2 + (hi - lo2) * 0.03, "%+d" % v["d"], color=VERDE if v["d"] >= 0 else ROJO, fontsize=6.5, ha="center", va="bottom", rotation=90)
    ax.axhline(0, color=TXT, lw=0.6); ax.set_xlim(-1, m + 1); horas(ax, v3, cada=5)
    ax.text(0.004, 0.88, "CVD de lo visible en velas con mecha (max/min del delta dentro del minuto)  ·  abajo, el delta de cada vela", transform=ax.transAxes, color=TXT, fontsize=8.5)
    columna_ahora(E["ahora"], vis, S, i0); fig.savefig(os.path.join(salida, "fusion_3_zoom_scalper.png"), facecolor=FONDO); plt.close(fig)

    # F4 — SIN PANEL: todo en cintas + columna AHORA con mini CVD; el precio ocupa casi todo
    fig, E = lienzo("FUSION 4 · SIN PANEL  ·  el precio ocupa casi todo: cuatro cintas + columna AHORA  ·  " + sub, [("precio", 4.2), ("cintas", 0.78)])
    velas(E["precio"], vis); marcas(E["precio"], vis, absor, divs)
    cintas(E["cintas"], n, [f_conf, f_pres, f_big, f_vel]); horas(E["cintas"], vis)
    E["precio"].text(0.004, 0.955, "◆ absorcion   ▼▲ divergencia   ·   sin panel: las cuatro cintas ocupan lo que hoy ocupa medio CVD", transform=E["precio"].transAxes, color=TXT, fontsize=9)
    columna_ahora(E["ahora"], vis, S, i0)
    fig.savefig(os.path.join(salida, "fusion_4_sin_panel.png"), facecolor=FONDO); plt.close(fig)
    print("listo:", salida, "| ventana", vis[0]["t"], "->", vis[-1]["t"], "| absorciones", sum(absor), "| divergencias", len(divs))


if __name__ == "__main__":
    main()
