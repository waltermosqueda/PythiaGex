# -*- coding: utf-8 -*-
"""RONDA 16c (23-11-2026): SUS gatillos de oct-nov 2026 (PREREGISTRO.md, seccion R16c, escrita ANTES que este codigo).
C1 absorcion en el nivel, C2 barrida y recuperacion, C3 estallido de continuacion, C4 burbujas (ordenes >= 95) y rechazo,
C5 ruptura y retesteo del apilado. Tabla de 1 s de ES + ordenes grandes de la cinta cruda + footprint de 30 s (r16_00) en velas de 1 min.
Interpretaciones fijadas al escribir el codigo (antes de ver un solo resultado):
  - "VWAP +-2 desv." como niveles = el VWAP y sus dos bandas de 2 desvios (tres niveles), con el valor del segundo que decide.
  - C2: el minimo de la barrida es un minimo de 60 s; el delta de la recuperacion se suma DESPUES del segundo del minimo.
  - C4: "maximo de los ultimos 120 s" = un segundo cuyo alto es >= todos los altos de los 120 s previos.
  - C5: si una vela tiene varios apilados del lado de la ruptura, vale el primero que dispare.
  - Grilla completa de segundos 9:00-16:00 (segundo sin operaciones: precio anterior, volumen y delta 0).
  - "Una operacion a la vez" = hasta la salida real (stop, objetivo o tiempo), no 60 min fijos.
Uso: python -I r16_03_gatillos.py   (la primera vez arma datos/ronda16/gr-<dia>.parquet desde la cinta cruda)
"""
import glob, os, sys
import numpy as np, pandas as pd
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from r16_01_eventos import resultado, R16, CACHE, TICK, COSTO, CORTE_CONFIRMAR, NY, HORIZONTE, FIN_DIA

FLUJO = os.path.join(os.environ.get("APPDATA", ""), "ATAS", "PythiaGex", "flujo")
GRANDE = 95
G0 = 9 * 3600                                  # la grilla arranca a las 9:00:00
T_INI, T_FIN = 9 * 3600 + 35 * 60, 12 * 3600   # entradas de 9:35:00 a 11:59:59
T_RTH, T_IB = 9 * 3600 + 30 * 60, 10 * 3600 + 30 * 60
STOP_CH, OBJ_CH = 8.25, 7.5
R_MIN, R_MAX, R_DEF = 1.5, 4.0, 2.0
LEJOS = 4.0                                    # placebo "lugar": a mas de 4 pts de todo nivel


# ---------------------------------------------------------------- datos
def dia_de_cinta(f):
    p = os.path.basename(f).split("-")[3][:8]
    return "%s-%s-%s" % (p[:4], p[4:6], p[6:8])


def tabla(dia):
    s = pd.read_parquet(os.path.join(CACHE, "seg-%s.parquet" % dia), columns=["ultimo", "alto", "bajo", "vol", "delta"])
    s.index = pd.to_datetime(s.index).tz_localize("UTC").tz_convert(NY).tz_localize(None)
    s = s.sort_index()
    return s[~s.index.duplicated(keep="last")]


def grilla(s, dia):
    d0 = pd.Timestamp(dia)
    idx = pd.date_range(d0 + pd.Timedelta(seconds=G0), d0 + pd.Timedelta(hours=16) - pd.Timedelta(seconds=1), freq="1s")
    pv = (s["ultimo"] * s["vol"]).cumsum(); v = s["vol"].cumsum(); p2v = (s["ultimo"] ** 2 * s["vol"]).cumsum()
    vw = pv / v.replace(0, np.nan); sd = np.sqrt((p2v / v.replace(0, np.nan) - vw ** 2).clip(lower=0))
    ult = s["ultimo"].reindex(idx, method="ffill")
    exacto = s.reindex(idx)
    G = {"t": idx, "x": (idx - d0).total_seconds().to_numpy().astype(int), "ult": ult.to_numpy(),
         "alto": exacto["alto"].fillna(ult).to_numpy(), "bajo": exacto["bajo"].fillna(ult).to_numpy(),
         "vol": exacto["vol"].fillna(0).to_numpy(), "delta": exacto["delta"].fillna(0).to_numpy(),
         "vw": vw.reindex(idx, method="ffill").to_numpy(), "sd": sd.reindex(idx, method="ffill").to_numpy()}
    adentro = s.loc[idx[0]: idx[-1], "vol"].sum()
    G["cobertura"] = float(G["vol"].sum() / adentro) if adentro else np.nan     # control: 1,0 = la grilla tomo todo
    return G


def grandes(dia):
    """Ordenes agresivas >= GRANDE contratos del dia (hora NY al segundo), desde la cinta cruda. Cacheado en gr-<dia>.parquet."""
    dst = os.path.join(R16, "gr-%s.parquet" % dia)
    if os.path.exists(dst):
        return pd.read_parquet(dst)
    cand = [f for f in glob.glob(os.path.join(FLUJO, "cinta-ES-*.csv")) if dia_de_cinta(f) == dia and os.path.exists(f + ".listo")]
    if not cand:
        return None
    d = pd.read_csv(cand[0], usecols=["t", "primero", "ultimo", "vol", "lado"]).drop_duplicates()   # igual que r16_00
    d = d[d["vol"] >= GRANDE]
    t = pd.to_datetime(d["t"], utc=True).dt.tz_convert(NY).dt.tz_localize(None).dt.floor("1s")
    g = pd.DataFrame({"t": t.to_numpy(), "lado": d["lado"].to_numpy(), "vol": d["vol"].to_numpy("float64"),
                      "pmax": d[["primero", "ultimo"]].max(axis=1).to_numpy(), "pmin": d[["primero", "ultimo"]].min(axis=1).to_numpy()})
    g.to_parquet(dst)
    return g


def footprint_1m(dia):
    fp = pd.read_parquet(os.path.join(R16, "fp-%s.parquet" % dia))
    fp["m"] = fp["vela30"].dt.floor("60s")                                     # las dos medias velas de 30 s suman la de 1 min
    g = fp.groupby(["m", "tick"], as_index=False)[["compra", "venta"]].sum().sort_values(["m", "tick"])
    return {m: (x["tick"].to_numpy(), x["compra"].to_numpy(), x["venta"].to_numpy()) for m, x in g.groupby("m")}


# ---------------------------------------------------------------- niveles
def niveles_fijos(dia, niv):
    dias = list(niv.index)
    if dia not in dias or dias.index(dia) == 0:
        return None
    ant = niv.loc[dias[dias.index(dia) - 1]]; hoy = niv.loc[dia]
    base = {"PVAH": ant["vah"], "PVAL": ant["val"], "PPOC": ant["poc"], "PDH": ant["h"], "PDL": ant["l"],
            "ONH": hoy["on_h"], "ONL": hoy["on_l"], "OPEN": hoy["o"]}
    ib = {"IBH": hoy["ib_h"], "IBL": hoy["ib_l"]}
    return {k: float(v) for k, v in base.items()}, {k: float(v) for k, v in ib.items()}


def lista_niveles(G, i, base, ib):
    v = list(base.values())
    if G["x"][i] >= T_IB:
        v += list(ib.values())
    if np.isfinite(G["vw"][i]) and np.isfinite(G["sd"][i]):
        v += [G["vw"][i], G["vw"][i] + 2 * G["sd"][i], G["vw"][i] - 2 * G["sd"][i]]
    return [float(a) for a in v if np.isfinite(a)]


def objetivo_nivel(ent, stop, lado, niveles):
    r = abs(ent - stop)
    cand = [(n - ent) * lado for n in niveles if R_MIN * r <= (n - ent) * lado <= R_MAX * r]
    return ent + lado * (min(cand) if cand else R_DEF * r)


def dist_nivel(p, niveles):
    return min(abs(n - p) for n in niveles) if niveles else np.inf


def bloques(G, B):
    nb = len(G["x"]) // B; n = nb * B
    return (nb, G["bajo"][:n].reshape(nb, B).min(1), G["alto"][:n].reshape(nb, B).max(1),
            G["vol"][:n].reshape(nb, B).sum(1), G["delta"][:n].reshape(nb, B).sum(1))


def en_ventana(G, i):
    return T_INI <= G["x"][i] < T_FIN


# ---------------------------------------------------------------- gatillos (cada uno devuelve senales: dict)
def c1(G, base, ib, exigir_nivel=True):
    B = 15; nb, lo, hi, vo, de = bloques(G, B); ult = G["ult"]; out = []
    for b in range(20, nb):
        mvol = vo[b - 20:b].mean()
        if mvol <= 0 or vo[b] < 2 * mvol or abs(de[b]) < 400:
            continue
        lado = +1 if de[b] <= -400 else -1                  # ventas que pegan contra el nivel -> largo (y al reves)
        i_fin = b * B + B - 1; i_ant = b * B - 60
        if i_ant < 0:
            continue
        niv = lista_niveles(G, i_fin, base, ib)
        extremo = lo[b] if lado > 0 else hi[b]
        if exigir_nivel:
            if lado > 0:
                ok = [n for n in niv if n - 1.5 <= lo[b] <= n + 0.5 and ult[i_ant] > n + 0.5]
            else:
                ok = [n for n in niv if n - 0.5 <= hi[b] <= n + 1.5 and ult[i_ant] < n - 0.5]
            if not ok:
                continue
        racha = 0
        for k in range(b + 1, min(nb, b + 7)):
            if (lado > 0 and lo[k] < lo[b]) or (lado < 0 and hi[k] > hi[b]):
                break
            racha = racha + 1 if de[k] * lado > 0 else 0
            if racha == 3:
                i = k * B + B - 1
                if en_ventana(G, i):
                    ent = float(ult[i]); stop = ent - lado * 6.0
                    out.append({"gatillo": "C1" if exigir_nivel else "C1_lugar", "i": i, "lado": lado, "entrada": ent, "stop": stop,
                                "objetivo": objetivo_nivel(ent, stop, lado, lista_niveles(G, i, base, ib)),
                                "dist_nivel": dist_nivel(extremo, niv)})
                break
    return out


def c2(G, base, ib):
    ult, alto, bajo, de, x = G["ult"], G["alto"], G["bajo"], G["delta"], G["x"]; n = len(x); out = []
    mn60 = pd.Series(bajo).rolling(60).min().shift(1).to_numpy(); mx60 = pd.Series(alto).rolling(60).max().shift(1).to_numpy()
    for m in range(60, n - 1):
        for lado in (+1, -1):
            if lado > 0:
                if not bajo[m] <= mn60[m]:
                    continue
                refs = [base["OPEN"], base["ONL"], base["PDL"], base["PVAL"]] + ([ib["IBL"]] if x[m] >= T_IB else [])
                if not any(R - 4 <= bajo[m] <= R - 0.25 for R in refs if x[m] >= T_RTH or R != base["OPEN"]):
                    continue
            else:
                if not alto[m] >= mx60[m]:
                    continue
                refs = [base["OPEN"], base["ONH"], base["PDH"], base["PVAH"]] + ([ib["IBH"]] if x[m] >= T_IB else [])
                if not any(R + 0.25 <= alto[m] <= R + 4 for R in refs if x[m] >= T_RTH or R != base["OPEN"]):
                    continue
            acum = 0.0
            for u in range(m + 1, min(n, m + 16)):
                if (lado > 0 and bajo[u] < bajo[m]) or (lado < 0 and alto[u] > alto[m]):
                    break                                       # el minimo se movio: ese segundo es su propio candidato
                acum += de[u]
                sube = (ult[u] - bajo[m]) if lado > 0 else (alto[m] - ult[u])
                if sube >= 8.0 and acum * lado >= 1000:
                    if en_ventana(G, u):
                        ent = float(ult[u])
                        out.append({"gatillo": "C2", "i": u, "lado": lado, "entrada": ent, "stop": ent - lado * STOP_CH,
                                    "objetivo": ent + lado * OBJ_CH, "dist_nivel": 0.0})
                    break
    return out


def c3(G, base, ib, filtro_apertura=True):
    B = 10; nb, lo, hi, vo, de = bloques(G, B); ult, alto, bajo, vw = G["ult"], G["alto"], G["bajo"], G["vw"]; out = []
    arriba = base["OPEN"] > base["PVAH"]; abajo = base["OPEN"] < base["PVAL"]
    for b in range(12, nb):
        ini = b * B; i = ini + B - 1
        if not en_ventana(G, i) or not np.isfinite(vw[i]):
            continue
        if de[b] >= 600 and hi[b] > alto[ini - 120:ini].max() and ult[i] > vw[i] and (arriba or not filtro_apertura):
            lado = +1
        elif de[b] <= -600 and lo[b] < bajo[ini - 120:ini].min() and ult[i] < vw[i] and (abajo or not filtro_apertura):
            lado = -1
        else:
            continue
        ent = float(ult[i])
        out.append({"gatillo": "C3" if filtro_apertura else "C3_sinfiltro", "i": i, "lado": lado, "entrada": ent,
                    "stop": ent - lado * STOP_CH, "objetivo": ent + lado * OBJ_CH, "dist_nivel": 0.0})
    return out


def c4(G, gr, dia):
    ult, alto, bajo, de, x = G["ult"], G["alto"], G["bajo"], G["delta"], G["x"]; n = len(x); out = []
    bmax = np.full(n, -np.inf); smin = np.full(n, np.inf)        # precio extremo de las compras / ventas grandes de cada segundo
    if gr is not None and len(gr):
        j = ((pd.to_datetime(gr["t"]) - G["t"][0]).dt.total_seconds()).to_numpy().astype(int)
        ok = (j >= 0) & (j < n)
        for jj, la, pmx, pmn in zip(j[ok], gr["lado"].to_numpy()[ok], gr["pmax"].to_numpy()[ok], gr["pmin"].to_numpy()[ok]):
            if la == 1:
                bmax[jj] = max(bmax[jj], pmx)
            elif la == -1:
                smin[jj] = min(smin[jj], pmn)
    mx120 = pd.Series(alto).rolling(120).max().shift(1).to_numpy(); mn120 = pd.Series(bajo).rolling(120).min().shift(1).to_numpy()
    for h in range(900, n - 1):
        for lado in (-1, +1):
            if lado < 0:   # corto: maximo de un rebote en tendencia corta bajista, con compras grandes arriba
                if not (alto[h] >= mx120[h] and ult[h] < ult[h - 900] and bmax[max(0, h - 30):h + 1].max() >= alto[h] - 2 * TICK):
                    continue
            else:
                if not (bajo[h] <= mn120[h] and ult[h] > ult[h - 900] and smin[max(0, h - 30):h + 1].min() <= bajo[h] + 2 * TICK):
                    continue
            ext = alto[h] if lado < 0 else bajo[h]; acum = 0.0
            for u in range(h + 1, min(n, h + 31)):
                if (lado < 0 and alto[u] > ext) or (lado > 0 and bajo[u] < ext):
                    break
                acum += de[u]
                if (ext - ult[u]) * (-lado) >= 2.0 and acum * lado > 0:
                    if en_ventana(G, u):
                        ent = float(ult[u])
                        out.append({"gatillo": "C4", "i": u, "lado": lado, "entrada": ent, "stop": ext - lado * 3.0,
                                    "objetivo": ent + lado * OBJ_CH, "dist_nivel": 0.0})
                    break
    return out


def apilados(ticks, compra, venta, lado):
    n = len(ticks); im = np.zeros(n, bool)
    for i in range(n):
        if lado > 0:
            ab = venta[i - 1] if i > 0 and ticks[i - 1] == ticks[i] - 1 else 0.0
            im[i] = compra[i] >= 3 * ab and compra[i] >= 30
        else:
            ar = compra[i + 1] if i + 1 < n and ticks[i + 1] == ticks[i] + 1 else 0.0
            im[i] = venta[i] >= 3 * ar and venta[i] >= 30
    out = []; i = 0
    while i < n:
        if im[i]:
            j = i
            while j + 1 < n and im[j + 1] and ticks[j + 1] == ticks[j] + 1:
                j += 1
            if j - i + 1 >= 3:
                out.append((int(ticks[i]), int(ticks[j])))
            i = j + 1
        else:
            i += 1
    return out


def c5(G, fp1, base, ib, exigir_nivel=True):
    B = 60; nb = len(G["x"]) // B; ult = G["ult"]; out = []
    o = ult[0:nb * B:B]; c = ult[B - 1:nb * B:B]
    _, l, h, _, de = bloques(G, B)
    t0 = G["t"][0]
    for k in range((T_RTH - G0) // B, nb - 1):
        m = t0 + pd.Timedelta(seconds=k * B)
        if m not in fp1:
            continue
        ticks, compra, venta = fp1[m]
        i_k = k * B + B - 1
        niv = lista_niveles(G, i_k, base, ib)
        hecho = False
        for lado in (+1, -1):
            if hecho:
                break
            if exigir_nivel:
                if lado > 0 and not any(l[k] <= N < c[k] for N in niv):
                    continue
                if lado < 0 and not any(h[k] >= N > c[k] for N in niv):
                    continue
            for piso, techo in apilados(ticks, compra, venta, lado):
                if hecho:
                    break
                P, T = piso * TICK, techo * TICK
                for j in range(k + 1, min(nb, k + 11)):
                    if (lado > 0 and c[j] < P) or (lado < 0 and c[j] > T):
                        break
                    toca = (l[j] <= T and c[j] > T and de[j] > 0) if lado > 0 else (h[j] >= P and c[j] < P and de[j] < 0)
                    if toca:
                        i = j * B + B - 1
                        if en_ventana(G, i):
                            ent = float(ult[i]); stop = (P - TICK) if lado > 0 else (T + TICK)
                            dist = (ent - stop) * lado
                            if dist < 6.0:
                                stop = ent - lado * 6.0
                            if dist <= 9.0:
                                out.append({"gatillo": "C5" if exigir_nivel else "C5_lugar", "i": i, "lado": lado, "entrada": ent,
                                            "stop": stop, "objetivo": objetivo_nivel(ent, stop, lado, lista_niveles(G, i, base, ib)),
                                            "dist_nivel": dist_nivel(T if lado > 0 else P, niv)})
                                hecho = True
                        break
    return out


# ---------------------------------------------------------------- ejecucion
def salida(s, t_ent, lado, ent, stop, obj):
    """Igual que resultado() de r16_01 pero devuelve tambien la hora de salida (para 'una operacion a la vez')."""
    fin = min(t_ent + pd.Timedelta(seconds=HORIZONTE), pd.Timestamp(t_ent.date()) + pd.Timedelta(FIN_DIA + ":00"))
    w = s.loc[t_ent + pd.Timedelta(seconds=1): fin]
    if len(w) == 0:
        return None
    lo, hi = w["bajo"].to_numpy(), w["alto"].to_numpy()
    st = np.nonzero(lo <= stop)[0] if lado > 0 else np.nonzero(hi >= stop)[0]
    ob = np.nonzero(hi >= obj)[0] if lado > 0 else np.nonzero(lo <= obj)[0]
    i_st = st[0] if len(st) else 10 ** 9; i_ob = ob[0] if len(ob) else 10 ** 9
    if i_st == i_ob == 10 ** 9:
        return (float(w["ultimo"].iloc[-1]) - ent) * lado - COSTO, "tiempo", w.index[-1]
    if i_st <= i_ob:
        return (stop - ent) * lado - COSTO, "stop", w.index[i_st]
    return (obj - ent) * lado - COSTO, "objetivo", w.index[i_ob]


def ejecutar(senales, G, s, dia, tope_stops=None):
    trades = []; libre = None; stops = 0
    for z in sorted(senales, key=lambda z: z["i"]):
        t_ent = G["t"][z["i"]]
        if libre is not None and t_ent <= libre:
            continue
        if tope_stops is not None and stops >= tope_stops:
            break
        r = salida(s, t_ent, z["lado"], z["entrada"], z["stop"], z["objetivo"])
        if not r:
            continue
        pts, motivo, t_sal = r
        chk = resultado(s, t_ent, z["lado"], z["entrada"], z["stop"], z["objetivo"])
        assert chk and abs(chk[0] - pts) < 1e-9 and chk[1] == motivo, (dia, z, chk, r)
        riesgo = abs(z["entrada"] - z["stop"])
        contra = resultado(s, t_ent, -z["lado"], z["entrada"], z["entrada"] + z["lado"] * riesgo,
                           z["entrada"] - z["lado"] * abs(z["objetivo"] - z["entrada"]))
        trades.append({**{k: v for k, v in z.items() if k != "i"}, "dia": dia, "t": t_ent, "riesgo": riesgo, "pts": pts, "motivo": motivo,
                       "t_salida": t_sal, "contra_pts": contra[0] if contra else np.nan})
        libre = t_sal; stops += motivo == "stop"
    return trades


def resumen(df, titulo):
    print("\n==== " + titulo)
    for tramo, sub in (("EXPLORAR", df[df["dia"] < CORTE_CONFIRMAR]), ("CONFIRMAR", df[df["dia"] >= CORTE_CONFIRMAR]), ("TODO", df)):
        for g, x in sub.groupby("gatillo"):
            v = x["pts"].to_numpy(); m = v.mean(); se = v.std(ddof=1) / np.sqrt(len(v)) if len(v) > 1 else np.nan
            az = ((x["pts"] + x["contra_pts"]) / 2).mean()
            print(f"{tramo:9s} {g:13s} n {len(v):4d} | media {m:+.2f} pts | t {m / se if se else np.nan:+.2f} | gana {np.mean(v > 0) * 100:3.0f} % "
                  f"| R {x['riesgo'].mean():.2f} | obj {np.mean(x['motivo'] == 'objetivo') * 100:3.0f} % | azar {az:+.2f}")


def main():
    niv = pd.read_parquet(os.path.join(R16, "niveles.parquet")).set_index("dia").sort_index()
    crudo, gestion = [], []
    for f in sorted(glob.glob(os.path.join(CACHE, "seg-*.parquet"))):
        dia = os.path.basename(f)[4:14]
        nf = niveles_fijos(dia, niv)
        if nf is None or not os.path.exists(os.path.join(R16, "fp-%s.parquet" % dia)):
            continue
        base, ib = nf
        s = tabla(dia); G = grilla(s, dia); gr = grandes(dia); fp1 = footprint_1m(dia)
        sen = {"C1": c1(G, base, ib), "C1_lugar": c1(G, base, ib, False), "C2": c2(G, base, ib), "C3": c3(G, base, ib),
               "C3_sinfiltro": c3(G, base, ib, False), "C4": c4(G, gr, dia), "C5": c5(G, fp1, base, ib), "C5_lugar": c5(G, fp1, base, ib, False)}
        for g, z in sen.items():
            crudo += ejecutar(z, G, s, dia)
        gestion += ejecutar(sen["C1"] + sen["C2"] + sen["C3"] + sen["C4"] + sen["C5"], G, s, dia, tope_stops=2)
        print(dia, "cobertura %.4f" % G["cobertura"], "grandes", 0 if gr is None else len(gr),
              " ".join("%s %d" % (g, len(z)) for g, z in sen.items()), flush=True)
    dc = pd.DataFrame(crudo); dg = pd.DataFrame(gestion)
    dc.to_csv(os.path.join(R16, "r16c_crudo.csv"), index=False); dg.to_csv(os.path.join(R16, "r16c_gestionado.csv"), index=False)
    resumen(dc, "CRUDO (cada gatillo solo, una operacion a la vez)")
    for g in ("C1_lugar", "C5_lugar"):
        x = dc[dc["gatillo"] == g]
        if len(x):
            for nom, sub in (("cerca <= 4 pts", x[x["dist_nivel"] <= LEJOS]), ("lejos > 4 pts", x[x["dist_nivel"] > LEJOS])):
                if len(sub) > 1:
                    v = sub["pts"].to_numpy()
                    print(f"  {g} {nom}: n {len(v)} media {v.mean():+.2f} t {v.mean() / (v.std(ddof=1) / np.sqrt(len(v))):+.2f}")
    if len(dg):
        dg["gatillo"] = "GESTIONADO"
        resumen(dg, "GESTIONADO (C1-C5 juntos, una posicion a la vez, fin del dia tras 2 stops)")


if __name__ == "__main__":
    main()
