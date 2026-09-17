# -*- coding: utf-8 -*-
"""
barridos_lib.py — familia BARRIDOS, ORDENES GRANDES Y RAFAGAS (busqueda de gatillo, 17-09).

Todo rasgo en el segundo t usa SOLO filas <= t. Los umbrales son percentiles MOVILES de la ventana previa [t-w, t-1] (nunca del dia entero).
Las definiciones exactas estan pre-registradas en resultados/barridos.md; este archivo es su traduccion literal.
"""
import sys
import numpy as np, pandas as pd

sys.path.insert(0, r"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\gatillo")
import base as B

INICIO_RUEDA = "13:32:00"   # rueda sin los primeros 2 minutos
FIN_RUEDA = "20:00:00"
SEPARACION = 60              # segundos minimos entre disparos de la misma variante
BARRERAS = ((8, 600), (5, 300), (12, 900))   # principal primero


def q_previo(x, w, q):
    """Percentil q de x en la ventana PREVIA [t-w, t-1] (no incluye t)."""
    return x.rolling(w, min_periods=w).quantile(q).shift(1)


def rasgos(d):
    """Rasgos causales sobre la tabla de 1 s de UNA sesion (entera, con la noche, para que las ventanas moviles lleguen llenas a la rueda)."""
    f = pd.DataFrame(index=d.index)
    p = d["ultimo"]
    f["ultimo"] = p
    # --- barridos, ventana de 10 s
    f["bn10"] = (d["barre_c"] - d["barre_v"]).rolling(10).sum()
    f["bt10"] = (d["barre_c"] + d["barre_v"]).rolling(10).sum()
    f["bn10_p99"] = q_previo(f["bn10"].abs(), 1800, 0.99)
    f["av10"] = p - p.shift(10)
    # --- rafagas de velocidad, ventana de 10 s
    f["n10"] = d["n"].rolling(10).sum()
    f["n10_p99"] = q_previo(f["n10"], 1800, 0.99)
    f["n10_p50"] = q_previo(f["n10"], 1800, 0.50)
    f["delta10"] = d["delta"].rolling(10).sum()
    f["vol10"] = d["vol"].rolling(10).sum()
    # --- contexto: movimiento de 5 min y rango previo
    f["mov300"] = p - p.shift(300)
    am = f["mov300"].abs()
    f["mov300_p80"] = q_previo(am, 3600, 0.80)
    f["mov300_p60"] = q_previo(am, 3600, 0.60)
    f["hi_prev"] = d["alto"].rolling(300).max().shift(11)   # maximo de [t-310, t-11]
    f["lo_prev"] = d["bajo"].rolling(300).min().shift(11)
    f["rp"] = f["hi_prev"] - f["lo_prev"]
    f["rp_p40"] = q_previo(f["rp"], 3600, 0.40)
    # --- grandes contra chicos, ventana de 60 s
    f["G60"] = d["d50"].rolling(60).sum()
    f["C60"] = (d["d1"] + d["d2_4"]).rolling(60).sum()
    f["G60_p95"] = q_previo(f["G60"].abs(), 1800, 0.95)
    f["C60_p50"] = q_previo(f["C60"].abs(), 1800, 0.50)
    # --- d50 aislado
    f["d50"] = d["d50"]
    f["d50_pos_120"] = (d["d50"] > 0).astype("float32").rolling(60).sum().shift(1)   # segundos con d50 comprador en [t-60, t-1]
    f["d50_neg_120"] = (d["d50"] < 0).astype("float32").rolling(60).sum().shift(1)
    f["tend_prev"] = p.shift(1) - p.shift(301)                                         # tendencia de 5 min ANTES de la orden
    # --- grandes (d50) acumulados a 5 min contra el precio
    f["GB300"] = d["d50"].rolling(300).sum()
    f["GB300_p50"] = q_previo(f["GB300"].abs(), 3600, 0.50)
    f["mov300_p50"] = q_previo(am, 3600, 0.50)
    hms = d.index.strftime("%H:%M:%S")
    f["elegible"] = d["rueda"].to_numpy() & (hms >= INICIO_RUEDA) & (hms < FIN_RUEDA)
    return f


def candidatos(f):
    """{variante: (mascara de candidatos, lado del DISPARO)} antes de desagrupar. 'side' es el lado del flujo; 'lado' es lo que dice el gatillo."""
    e = f["elegible"].to_numpy()
    sg = np.sign
    out = {}
    # ---------- barridos
    bn = f["bn10"].to_numpy(); sb = sg(bn)
    barrido = e & (np.abs(bn) >= f["bn10_p99"].to_numpy()) & (np.abs(bn) >= 0.6 * f["bt10"].to_numpy()) & (sb != 0)
    mov = f["mov300"].to_numpy()
    out["V01_BARRIDO_SEGUIR"] = (barrido, sb)
    climax_b = sb * mov >= f["mov300_p80"].to_numpy()
    out["V02_BARRIDO_CLIMAX_CONTRA"] = (barrido & climax_b, -sb)
    u = f["ultimo"].to_numpy(); hi = f["hi_prev"].to_numpy(); lo = f["lo_prev"].to_numpy()
    rango_chico = f["rp"].to_numpy() <= f["rp_p40"].to_numpy()
    rompe = np.where(sb > 0, u > hi, u < lo)
    out["V03_BARRIDO_RUPTURA_SEGUIR"] = (barrido & rango_chico & rompe, sb)
    # V04: barrido DEVUELTO: primer segundo t, hasta 60 s despues de un barrido grande en i, en que el precio vuelve al de ANTES del barrido (ultimo[i-10])
    m4 = np.zeros(len(f), bool); l4 = np.zeros(len(f)); idb = np.flatnonzero(barrido)
    for i in idb:
        if i < 10: continue
        ref = u[i - 10]
        for t in range(i + 1, min(i + 61, len(f))):
            if sb[i] * (u[t] - ref) <= 0:
                if e[t] and not m4[t]: m4[t] = True; l4[t] = -sb[i]
                break
    out["V04_BARRIDO_DEVUELTO_CONTRA"] = (m4, l4)
    # ---------- rafagas
    d10 = f["delta10"].to_numpy(); sd = sg(d10)
    rafaga = e & (f["n10"].to_numpy() >= f["n10_p99"].to_numpy()) & (np.abs(d10) >= 0.25 * f["vol10"].to_numpy()) & (sd != 0)
    out["V05_RAFAGA_SEGUIR"] = (rafaga, sd)
    out["V06_RAFAGA_CLIMAX_CONTRA"] = (rafaga & (sd * mov >= f["mov300_p80"].to_numpy()), -sd)
    # V07: la rafaga se APAGA: primer segundo t, entre 5 y 60 s despues del ultimo segundo de rafaga, en que n10 <= mediana previa
    n10 = f["n10"].to_numpy(); p50 = f["n10_p50"].to_numpy()
    m7 = np.zeros(len(f), bool); l7 = np.zeros(len(f))
    idx = np.flatnonzero(rafaga); usado = -10**9
    for k, i in enumerate(idx):
        fin = idx[k + 1] if k + 1 < len(idx) else len(f)
        for t in range(i + 5, min(i + 61, fin, len(f))):   # si llega otra rafaga antes, manda la nueva
            if n10[t] <= p50[t]:
                if e[t] and t != usado: m7[t] = True; l7[t] = -sd[i]; usado = t
                break
    out["V07_RAFAGA_APAGADA_CONTRA"] = (m7, l7)
    # ---------- grandes contra chicos (60 s)
    G = f["G60"].to_numpy(); C = f["C60"].to_numpy(); sG = sg(G); sC = sg(C)
    grande = e & (np.abs(G) >= f["G60_p95"].to_numpy()) & (sG != 0) & (np.abs(C) >= f["C60_p50"].to_numpy()) & (sC != 0)
    out["V08_GRANDES_VS_CHICOS_SEGUIR_GRANDES"] = (grande & (sG == -sC), sG)
    out["V09_GRANDES_CON_CHICOS_SEGUIR"] = (grande & (sG == sC), sG)
    # ---------- d50 aislado
    d50 = f["d50"].to_numpy(); s5 = sg(d50)
    previos = np.where(s5 > 0, f["d50_pos_120"].to_numpy(), f["d50_neg_120"].to_numpy())
    aislado = e & (np.abs(d50) >= 75) & (previos == 0)
    tend = f["tend_prev"].to_numpy(); p60 = f["mov300_p60"].to_numpy()
    out["V10_D50_AISLADO_CONTRA_TENDENCIA"] = (aislado & (-s5 * tend >= p60), s5)
    out["V11_D50_AISLADO_A_FAVOR"] = (aislado & (s5 * tend >= p60), s5)
    # ---------- los grandes (d50 acumulado 300 s) divergen del precio a 5 min
    GB = f["GB300"].to_numpy(); sGB = sg(GB)
    out["V12_GRANDES_DIVERGEN_300_SEGUIR_GRANDES"] = (e & (np.abs(GB) >= f["GB300_p50"].to_numpy()) & (sGB != 0) & (sGB == -sg(mov)) & (np.abs(mov) >= f["mov300_p50"].to_numpy()), sGB)
    return out


def desagrupar(mask, sep=SEPARACION):
    """Minimo sep segundos entre disparos (gana el primero)."""
    idx = np.flatnonzero(mask); keep = []; ult = -10**9
    for i in idx:
        if i - ult >= sep: keep.append(i); ult = i
    return np.asarray(keep, dtype=int)


def disparos(d):
    """{variante: (indices desagrupados, lados)} de una sesion."""
    f = rasgos(d); c = candidatos(f); out = {}
    for v, (m, lado) in c.items():
        m = m & ~np.isnan(np.where(m, lado, 0.0))
        i = desagrupar(m); out[v] = (i, np.asarray(lado)[i].astype(int))
    return out, f


def movimiento_firmado(d, idx, lado, hs=(10, 30, 60, 120, 300)):
    """Movimiento medio a favor del lado (puntos) a h segundos, para entender la microestructura."""
    p = d["ultimo"].to_numpy(); n = len(p); r = {}
    for h in hs:
        ok = idx + h < n
        r[h] = float(np.mean((p[idx[ok] + h] - p[idx[ok]]) * lado[ok])) if ok.any() else np.nan
    return r
