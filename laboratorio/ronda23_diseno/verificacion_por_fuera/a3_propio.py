# -*- coding: utf-8 -*-
"""A3 POR FUERA (27-11-2026): mi propia implementacion de la regla A3 leida del PREREGISTRO_VIVO.md de la R23, SIN mirar el codigo de
la candidata ni el del agente. Corre sobre las cintas crudas de la sonda (ES, 10-09 a 25-11-2026) y compara operacion por operacion con
salidas/ops-cinta-A3.csv. Uso: python -I a3_propio.py [espera]   (espera = 1 o 2: velas de espera tras la salida)"""
import sys, os, glob, json, math
sys.stdout.reconfigure(encoding="utf-8")
import numpy as np, pandas as pd

TICK = 0.25
R23 = r"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\ronda23_diseno"
FLUJO = os.path.join(os.environ["APPDATA"], "ATAS", "PythiaGex", "flujo")
ESPERA = int(sys.argv[1]) if len(sys.argv) > 1 else 1

def al_tick(x):  # al tick, la mitad para arriba
    return math.floor(x / TICK + 0.5 + 1e-9) * TICK

hist = json.load(open(os.path.join(R23, "historia_rangos_semilla.json"), encoding="utf-8"))["rangos"]

# una cinta por jornada: la de 'hasta' mas tardio entre las que tienen .listo
porfecha = {}
for f in glob.glob(os.path.join(FLUJO, "cinta-ES*.csv")):
    b = os.path.basename(f)[:-4].split("-")
    hasta = b[-1]; dia = pd.Timestamp(hasta[:8] + "T" + hasta[8:], tz="UTC").tz_convert("America/New_York").strftime("%Y-%m-%d")
    listo = os.path.exists(f[:-4] + ".listo")
    clave = (listo, hasta)
    if dia not in porfecha or clave > porfecha[dia][0]: porfecha[dia] = (clave, f)
dias = sorted(d for d in porfecha if "2026-09-10" <= d <= "2026-11-25")
print("jornadas con cinta en el periodo:", len(dias))

def jornada(dia, f):
    df = pd.read_csv(f, usecols=["t", "primero", "ultimo", "vol"])
    t = pd.to_datetime(df["t"], format="%Y-%m-%dT%H:%M:%S.%f").dt.tz_localize("UTC").dt.tz_convert("America/New_York")
    ini = pd.Timestamp(dia + " 09:30:00", tz="America/New_York"); fin = pd.Timestamp(dia + " 16:00:00", tz="America/New_York")
    m = (t >= ini) & (t < fin)
    seg = ((t[m] - ini).dt.total_seconds()).to_numpy()
    p = df["primero"].to_numpy(float)[m.to_numpy()]; u = df["ultimo"].to_numpy(float)[m.to_numpy()]; v = df["vol"].to_numpy(float)[m.to_numpy()]
    return seg, p, u, v

previos = sorted(hist)
ops = []; rango_ctl = []
for dia in dias:
    seg, p, u, v = jornada(dia, porfecha[dia][1])
    lo = np.minimum(p, u); hi = np.maximum(p, u); mid = (p + u) / 2
    prev = [d for d in previos if d < dia][-10:]
    med = float(np.median([hist[d] for d in prev])); X = al_tick(0.08 * med)
    rango_ctl.append((dia, hi.max() - lo.min(), hist.get(dia)))
    barra = (seg // 300).astype(int)
    cpv = np.cumsum(mid * v); cv = np.cumsum(v); chi = np.maximum.accumulate(hi); clo = np.minimum.accumulate(lo)
    ib = seg < 3600; ibh, ibl = hi[ib].max(), lo[ib].min(); ibw = ibh - ibl
    # estado al cierre de cada vela k (cierre a las 9:30 + 5(k+1)): ultima orden de la vela
    ultimo_de = {}
    for k in range(78):
        idx = np.searchsorted(barra, k, side="right") - 1
        if idx >= 0 and barra[idx] == k: ultimo_de[k] = idx
    def estado(k):
        i = ultimo_de[k]; return i, u[i], cpv[i] / cv[i], chi[i], clo[i]
    siguiente_ok = 0; n_dia = 0
    for k in range(78):
        cierre_min = 9 * 60 + 30 + 5 * (k + 1)
        if not (13 * 60 <= cierre_min <= 15 * 60 + 30): continue
        if k < siguiente_ok or n_dia >= 5 or k not in ultimo_de: continue
        i, px, vw, dh, dl = estado(k)
        dv = px - vw
        if abs(dv) < 1.5 * X: continue
        lado = -1 if dv > 0 else 1  # -1 VENTA (precio arriba), +1 COMPRA
        if lado == -1:
            ext = max(0.0, dh - ibh) / ibw; stop = dh + al_tick(X / 2)
        else:
            ext = max(0.0, ibl - dl) / ibw; stop = dl - al_tick(X / 2)
        if ext > 1: continue
        dstop = abs(stop - px); P = dstop / (dstop + abs(dv))
        if P < 0.55: continue
        # simular hacia adelante, orden por orden; el objetivo se corre en cada cierre de vela
        L = al_tick(vw); j = i + 1; salida = None; kk = k
        while j < len(seg) and salida is None:
            b = barra[j]
            L = al_tick(cpv[j - 1] / cv[j - 1])  # al empezar cada vela: el VWAP del ultimo cierre (la orden j-1 es la ultima antes de esta vela)
            fin_b = np.searchsorted(barra, b, side="right")
            hl = slice(j, fin_b)
            if lado == -1:
                tg = lo[hl] <= L - TICK; st = hi[hl] >= stop
            else:
                tg = hi[hl] >= L + TICK; st = lo[hl] <= stop
            it = np.argmax(tg) if tg.any() else None; is_ = np.argmax(st) if st.any() else None
            if it is not None and (is_ is None or it < is_):
                salida = (j + it, L, "objetivo")
            elif is_ is not None:
                q = j + is_; fp = p[q] if (p[q] - stop) * (-lado) >= 0 else stop
                salida = (q, fp, "stop")
            j = fin_b
        if salida is None: salida = (len(seg) - 1, u[-1], "fin")
        q, sp, mot = salida
        pts = (sp - px) * lado
        hhmm = "%02d:%02d" % divmod(cierre_min, 60)
        ops.append(dict(fecha=dia, hora=hhmm, lado="VENTA" if lado == -1 else "COMPRA", entrada=px, stop=stop, obj0=al_tick(vw), X=X, P=round(P, 4),
                        motivo=mot, salida=sp, seg_salida=seg[q], pts=pts))
        n_dia += 1
        siguiente_ok = barra[q] + ESPERA
dif_r = [(d, a, b) for d, a, b in rango_ctl if b is None or abs(a - b) > 1e-9]
print("control de rangos: %d jornadas, distintas a la historia %d" % (len(rango_ctl), len(dif_r)), dif_r[:5])
mio = pd.DataFrame(ops)
ref = pd.read_csv(os.path.join(R23, "salidas", "ops-cinta-A3.csv"))
ref["hora"] = ref["hora_entrada"].str[:5]
print("MIAS: n %d | acierto pts>0 %.1f %% | neto c1 %+.3f | motivos %s" % (len(mio), 100 * (mio.pts > 0).mean(), (mio.pts - 1).mean(), mio.motivo.value_counts().to_dict()))
print("AGENTE: n %d | acierto pts>0 %.1f %% | neto>0 %.1f %% | neto c1 %+.3f" % (len(ref), 100 * (ref.pts > 0).mean(), 100 * (ref.neto_c100 > 0).mean(), ref.neto_c100.mean()))
k1 = set(zip(mio.fecha, mio.hora, mio.lado)); k2 = set(zip(ref.fecha, ref.hora, ref.lado))
print("entradas iguales %d | solo mias %d | solo del agente %d" % (len(k1 & k2), len(k1 - k2), len(k2 - k1)))
for k in sorted(k1 - k2): print("   solo mia:", k, mio[(mio.fecha == k[0]) & (mio.hora == k[1])].iloc[0][["entrada", "motivo", "pts"]].to_dict())
for k in sorted(k2 - k1): print("   solo del agente:", k, ref[(ref.fecha == k[0]) & (ref.hora == k[1])].iloc[0][["entrada", "motivo", "pts"]].to_dict())
m = mio.merge(ref, on=["fecha", "hora", "lado"], suffixes=("_m", "_a"))
dis = m[(abs(m.entrada_m - m.entrada_a) > 1e-9) | (abs(m.pts_m - m.pts_a) > 1e-9) | (m.motivo_m != m.motivo_a) | (abs(m.stop_m - m.stop_a) > 1e-9)]
print("en las iguales: distintas en entrada/stop/salida/motivo: %d" % len(dis))
for _, r in dis.iterrows(): print("   ", r.fecha, r.hora, r.lado, "mia", r.entrada_m, r.stop_m, r.motivo_m, r.pts_m, "| agente", r.entrada_a, r.stop_a, r.motivo_a, r.pts_a)
mio.to_csv(os.path.join(os.path.dirname(os.path.abspath(__file__)), "a3_propio_ops_espera%d.csv" % ESPERA), index=False)
