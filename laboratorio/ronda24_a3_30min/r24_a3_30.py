# -*- coding: utf-8 -*-
"""RONDA 24 - A3 A 30 MINUTOS (27-11-2026). La regla A3 congelada en la R23 (cand_A3_tarde_giro_amplia.py / PREREGISTRO_VIVO.md) llevada
TAL CUAL a velas de 30 min, sin ningun parametro nuevo, para probarla en MES de abril-2024 a agosto-2025, donde la R23 nunca eligio nada
(de ese periodo solo hay velas de 30 y 60 min en la cache de ATAS; la R22 las uso para el semaforo D1, no para esta regla).

Traduccion a 30 min (lo unico que cambia es la resolucion):
- Rueda 9:30-16:00 NY completa = 13 velas (9:30 ... 15:30). X = 0,08 x mediana del rango de la rueda de las 10 ruedas completas previas, al tick.
- VWAP de la rueda con (max+min)/2 x volumen de cada vela. IB = las dos primeras velas (9:30-10:30).
- Se mira en los cierres de 13:00, 13:30, 14:00, 14:30, 15:00 y 15:30 (6 de los 31 cierres de 5 min de A3).
- Mismas tres condiciones (>= 1,5 X del VWAP; IB extendido <= 1 vez del lado del precio; P = d_stop/(d_stop+d_VWAP) >= 0,55).
- Entrada al cierre; stop X/2 (al tick) tras el extremo del dia; objetivo = VWAP del cierre previo al tick, 1 tick mas alla; misma vela =
  stop (conservador); hueco = apertura; lo abierto sale al cierre de la vela de 15:30-16:00; una a la vez; max 5; costo 1,00.
- Azar con la misma ubicacion: 200 caminos con cada vela posterior reflejada al azar alrededor del cierre previo (mismo volumen, VWAP
  recalculado, mismas salidas). Semilla fija.
Uso: python -I r24_a3_30.py calibrar   (ano ya visto: 5 min agregadas a 30 y 30 nativas, contra A3 en 5 min)
     python -I r24_a3_30.py prueba     (UNA vez, solo con PREREGISTRO.md y huellas_r24.json escritos antes; deja candado)"""
import sys, os, math, json, hashlib
sys.stdout.reconfigure(encoding="utf-8")
import numpy as np, pandas as pd
TICK = 0.25; N = 200; COSTO = 1.0
AQUI = os.path.dirname(os.path.abspath(__file__))
VELAS = os.path.join(os.path.dirname(AQUI), "dom", "ronda3", "velas")
DECIDE = range(6, 12)  # cierres 13:00 .. 15:30 en velas de 30 min (vela k cierra a las 9:30 + 30 (k+1))

def al_tick(x): return math.floor(x / TICK + 0.5 + 1e-9) * TICK
def al_tick_v(x): return np.floor(np.asarray(x) / TICK + 0.5 + 1e-9) * TICK

def rth(archivo):
    d = pd.read_csv(os.path.join(VELAS, archivo), usecols=["t", "o", "h", "l", "c", "vol"])
    t = pd.to_datetime(d["t"]).dt.tz_localize("UTC").dt.tz_convert("America/New_York")
    d["dia"] = t.dt.strftime("%Y-%m-%d"); d["min"] = t.dt.hour * 60 + t.dt.minute
    return d[(d["min"] >= 570) & (d["min"] < 960)].copy()

def ruedas30_nativas():
    r = rth("velas-MES-m30.csv"); out = {}
    for dia, g in r.groupby("dia"):
        if len(g) == 13 and g["min"].iloc[0] == 570 and g["min"].iloc[-1] == 930: out[dia] = g.reset_index(drop=True)
    return out

def ruedas30_desde5():
    r = rth("velas-MES-m5.csv"); out = {}
    for dia, g in r.groupby("dia"):
        if len(g) != 78: continue
        g = g.copy(); g["b"] = (g["min"] - 570) // 30
        a = g.groupby("b").agg(o=("o", "first"), h=("h", "max"), l=("l", "min"), c=("c", "last"), vol=("vol", "sum")).reset_index(drop=True)
        out[dia] = a
    return out

def azar(rng, lado, entrada, stop, vw0, cv0, cpv0, O, H, L, C, V):
    n = len(H)
    cprev = np.concatenate([[entrada], C[:-1]])
    dO, dH, dL, dC = O - cprev, H - cprev, L - cprev, C - cprev
    s = rng.choice([-1.0, 1.0], size=(N, n))
    rO = s * dO; rH = np.where(s > 0, dH, -dL); rL = np.where(s > 0, dL, -dH); rC = s * dC
    base = entrada + np.concatenate([np.zeros((N, 1)), np.cumsum(rC, axis=1)[:, :-1]], axis=1)
    o, h, l, c = base + rO, base + rH, base + rL, base + rC
    cpv = cpv0 + np.cumsum((h + l) / 2 * V, axis=1); cv = cv0 + np.cumsum(np.broadcast_to(V, (N, n)), axis=1)
    vprev = np.concatenate([np.full((N, 1), vw0), (cpv / cv)[:, :-1]], axis=1); Lt = al_tick_v(vprev)
    if lado < 0: st = h >= stop; tg = l <= Lt - TICK
    else: st = l <= stop; tg = h >= Lt + TICK
    cual = st | tg; hay = cual.any(axis=1); j = np.argmax(cual, axis=1); f = np.arange(N)
    gap = (o[f, j] - stop) * (-lado) >= 0
    salida = np.where(~hay, c[:, -1], np.where(st[f, j], np.where(gap, o[f, j], stop), Lt[f, j]))
    pts = (salida - entrada) * lado
    return float((pts > 0).mean()), float(pts.mean())

def correr(ruedas, desde, hasta, semilla, nb=13, decide=DECIDE, ib_velas=2):
    rng = np.random.default_rng(semilla)
    dias = sorted(ruedas); rango = {x: ruedas[x]["h"].max() - ruedas[x]["l"].min() for x in dias}
    ops = []; evaluadas = []
    for n, dia in enumerate(dias):
        if n < 10 or not (desde <= dia <= hasta): continue
        evaluadas.append(dia)
        X = al_tick(0.08 * float(np.median([rango[x] for x in dias[n - 10:n]])))
        g = ruedas[dia]; o, h, l, c, v = (g[k].to_numpy(float) for k in ("o", "h", "l", "c", "vol"))
        mid = (h + l) / 2; cpv = np.cumsum(mid * v); cv = np.cumsum(v); vw = cpv / cv
        dh = np.maximum.accumulate(h); dl = np.minimum.accumulate(l)
        ibh, ibl = h[:ib_velas].max(), l[:ib_velas].min(); ibw = ibh - ibl
        sig = 0; nd = 0
        for k in decide:
            if k < sig or nd >= 5: continue
            px = c[k]; dv = px - vw[k]
            if abs(dv) < 1.5 * X: continue
            lado = -1 if dv > 0 else 1
            if lado == -1: ext = max(0.0, dh[k] - ibh) / ibw; stop = dh[k] + al_tick(X / 2)
            else: ext = max(0.0, ibl - dl[k]) / ibw; stop = dl[k] - al_tick(X / 2)
            if ext > 1: continue
            ds = abs(stop - px); P = ds / (ds + abs(dv))
            if P < 0.55: continue
            sal = None
            for j in range(k + 1, nb):
                L = al_tick(vw[j - 1])
                if lado == -1: st = h[j] >= stop; tg = l[j] <= L - TICK; gap = o[j] >= stop
                else: st = l[j] <= stop; tg = h[j] >= L + TICK; gap = o[j] <= stop
                if st: sal = (j, o[j] if gap else stop, "stop"); break
                if tg: sal = (j, L, "objetivo"); break
            if sal is None: sal = (nb - 1, c[nb - 1], "fin")
            j, sp, mot = sal
            ag, ap = azar(rng, lado, px, stop, vw[k], cv[k], cpv[k], o[k + 1:], h[k + 1:], l[k + 1:], c[k + 1:], v[k + 1:])
            ops.append(dict(fecha=dia, k=k, lado=lado, entrada=px, vwap=vw[k], stop=stop, X=X, P=P, motivo=mot, salida=sp,
                            pts=(sp - px) * lado, az_gana=ag, az_pts=ap))
            nd += 1; sig = j + 1
    return pd.DataFrame(ops), evaluadas

def t_cluster(d, grupos):
    d = np.asarray(d, float); n = len(d)
    if n < 2: return float("nan")
    m = d.mean(); s = pd.Series(d - m).groupby(np.asarray(grupos)).sum()
    se = math.sqrt((s ** 2).sum()) / n
    return m / se if se > 0 else float("nan")

def resumen(nombre, o, evaluadas):
    nr = len(evaluadas)
    if len(o) == 0:
        print("%-34s ruedas %d | sin operaciones" % (nombre, nr)); return {}
    pr = o.groupby("fecha")["pts"].apply(lambda s: (s - COSTO).sum()).reindex(evaluadas, fill_value=0.0)
    t_r = pr.mean() / (pr.std(ddof=1) / math.sqrt(len(pr)))
    ed = o.pts - o.az_pts; t_e = t_cluster(ed, o.fecha)
    r = dict(ruedas=nr, ops=len(o), por_rueda=len(o) / nr, acierto=100 * (o.pts > 0).mean(), azar=100 * o.az_gana.mean(),
             formula=100 * o.P.mean(), neto=(o.pts - COSTO).mean(), neto_azar=(o.az_pts - COSTO).mean(), t_rueda=t_r,
             ventaja=ed.mean(), t_ventaja=t_e, motivos=o.motivo.value_counts().to_dict())
    print("%-34s ruedas %3d | ops %3d (%.2f/r) | acierto %.1f %% (azar %.1f, formula %.1f) | neto %+.2f (azar %+.2f) t rueda %.2f | ventaja %+.2f pts/op t %.2f | %s"
          % (nombre, nr, r["ops"], r["por_rueda"], r["acierto"], r["azar"], r["formula"], r["neto"], r["neto_azar"], t_r, r["ventaja"], t_e, r["motivos"]))
    return r

if __name__ == "__main__":
    modo = sys.argv[1] if len(sys.argv) > 1 else "calibrar"
    if modo == "calibrar":
        n30 = ruedas30_nativas(); a30 = ruedas30_desde5()
        comunes = sorted(set(n30) & set(a30))
        dif = [abs(n30[x][k] - a30[x][k]).max() for x in comunes for k in ("o", "h", "l", "c")]
        dv = [abs(n30[x]["vol"] - a30[x]["vol"]).max() for x in comunes]
        print("velas de 30 nativas contra 5 agregadas: %d ruedas en comun, diferencia maxima de precio %.2f pts, de volumen %d" % (len(comunes), max(dif), max(dv)))
        for nom, ru, a, b in (("A3-30 desde 5 min DISENO", a30, "2025-09-09", "2026-02-27"), ("A3-30 desde 5 min CHEQUEO", a30, "2026-03-02", "2026-08-31"),
                              ("A3-30 nativa DISENO", n30, "2025-09-09", "2026-02-27"), ("A3-30 nativa CHEQUEO (hasta 17-08)", n30, "2026-03-02", "2026-08-17")):
            o, ev = correr(ru, a, b, 24)
            resumen(nom, o, ev)
        print("referencia A3 en 5 min (R23, verificada por fuera): diseno 109 ops 74,3 % (azar 63,2) neto +2,49 ventaja +3,74; chequeo 122-123 ops 67,2-67,5 % (azar 62,6) neto +0,59/+0,66 ventaja +1,46")
    elif modo == "prueba":
        candado = os.path.join(AQUI, "PRUEBA_CORRIDA.lock")
        if os.path.exists(candado): sys.exit("la prueba ya se corrio una vez (%s): no se repite" % candado)
        h = json.load(open(os.path.join(AQUI, "huellas_r24.json"), encoding="utf-8"))
        for f, v in h["sha256"].items():
            if hashlib.sha256(open(os.path.join(AQUI, f), "rb").read()).hexdigest() != v: sys.exit("huella distinta: " + f)
        open(candado, "w").write(pd.Timestamp.now().isoformat())
        n30 = ruedas30_nativas()
        o, ev = correr(n30, h["prueba_desde"], h["prueba_hasta"], 2024)
        r = resumen("PRUEBA A3-30 MES 2024-25", o, ev)
        o.to_csv(os.path.join(AQUI, "ops_prueba_2024_25.csv"), index=False)
        for nom, a, b in (("  primera mitad", h["prueba_desde"], "2024-12-31"), ("  segunda mitad", "2025-01-01", h["prueba_hasta"])):
            q = o[(o.fecha >= a) & (o.fecha <= b)]; resumen(nom, q, [x for x in ev if a <= x <= b])
        pasa = r and r["acierto"] >= 60 and r["neto"] > 0 and r["t_ventaja"] >= 1.96
        print("VEREDICTO (vara pre-registrada: acierto >= 60 %%, neto > 0 con costo 1,00, ventaja sobre el azar t >= 1,96): %s" % ("PASA" if pasa else "NO PASA"))
        json.dump(dict(resultado=r, veredicto="PASA" if pasa else "NO PASA", corrida=pd.Timestamp.now().isoformat()),
                  open(os.path.join(AQUI, "resultado_prueba.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1, default=float)
