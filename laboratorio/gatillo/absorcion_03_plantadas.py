# -*- coding: utf-8 -*-
"""
absorcion_03_plantadas.py — pasada por la cinta cruda, de a UNA sesion, para sacar lo que la tabla de 1 s no tiene:

  PLANTADA (del lado del bid; el ask es el espejo): un precio P contra el que se vende en la punta y la punta no cede.
    Se sigue cada precio P contra el que pega una venta de un solo nivel (primero == ultimo == mejor bid de antes). Por cada P vivo:
      S = contratos vendidos contra P, R = cantidad de REPOSICIONES (ventas que consumieron todo el bid visible y el bid quedo en P),
      SR = contratos de esas ventas, D = mayor tamano visible visto en P.
    P MUERE (rota) cuando el mejor bid queda por debajo de P (lo vemos en la punta de antes o de despues de cualquier orden).
    Si P pasa mas de 60 s sin recibir ventas, la cuenta arranca de cero (otra plantada).
  Se guarda, por plantada: cuando S cruzo cada escalon (10, 15, 20, 30, 40, 60, 80, 120) con R y D de ese momento, y como termino.
  Tambien, por segundo: el precio minimo de las abs_bid y el maximo de las abs_ask (para la variante FALLA).

Uso: python absorcion_03_plantadas.py 2026-08-20 2026-08-21 ...   (sin argumentos: todas las que falten, hasta agotar ~4 min)
"""
import sys, os, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd, base as B, absorcion_base as A

ESCALONES = (10, 15, 20, 30, 40, 60, 80, 120)
MEMORIA_S = 60.0


def lado_bid(t, px_pri, px_ult, vol, es_venta, ab, abv, db):
    """Devuelve (cruces, finales). Todo en 'mundo bid'; para el ask se llama con los precios negados."""
    cruces = []; finales = []; pila = []   # pila de niveles vivos, precio ascendente; cada nivel: [P, S, R, SR, D, t0, tlast, idx_escalon, id]
    nid = 0
    for i in range(len(t)):
        a = ab[i]; d = db[i]
        if a != a or d != d: continue
        ti = t[i]
        while pila and pila[-1][0] > a:
            L = pila.pop(); finales.append((L[8], L[0], L[5], L[6], ti, L[1], L[2], L[3], L[4], 0))   # 0 = rota entre ordenes (pasiva)
        if es_venta[i] and px_pri[i] == a and px_ult[i] == a:
            v = vol[i]
            if pila and pila[-1][0] == a:
                L = pila[-1]
                if ti - L[6] > MEMORIA_S:
                    finales.append((L[8], L[0], L[5], L[6], ti, L[1], L[2], L[3], L[4], 2))   # 2 = vencio por tiempo
                    nid += 1; L[1] = 0; L[2] = 0; L[3] = 0; L[4] = 0; L[5] = ti; L[7] = 0; L[8] = nid
            else:
                nid += 1; L = [a, 0, 0, 0, 0, ti, ti, 0, nid]; pila.append(L)
            L[1] += v; L[6] = ti
            if abv[i] > L[4]: L[4] = abv[i]
            if v >= abv[i] and d >= a: L[2] += 1; L[3] += v
            while L[7] < len(ESCALONES) and L[1] >= ESCALONES[L[7]]:
                cruces.append((L[8], a, ti, ESCALONES[L[7]], L[1], L[2], L[3], L[4], L[5])); L[7] += 1
        while pila and pila[-1][0] > d:
            L = pila.pop(); finales.append((L[8], L[0], L[5], L[6], ti, L[1], L[2], L[3], L[4], 1))   # 1 = rota por esta orden
    return cruces, finales


def sesion(s):
    d = A.cinta_sesion(s); hm = d["t"].dt.strftime("%H:%M"); d = d[(hm >= "13:00") & (hm < "20:30")].reset_index(drop=True)
    t = (d["t"].astype("int64") / 1e9).to_numpy().tolist()
    pri = d["primero"].to_numpy(); ult = d["ultimo"].to_numpy(); vol = d["vol"].to_numpy(); lado = d["lado"].to_numpy()
    ab, abv, aa, aav, db, da = (d[c].to_numpy("float64") for c in ("abid", "abidv", "aask", "aaskv", "dbid", "dask"))
    cb, fb = lado_bid(t, pri.tolist(), ult.tolist(), vol.tolist(), (lado == -1).tolist(), ab.tolist(), abv.tolist(), db.tolist())
    ca, fa = lado_bid(t, (-pri).tolist(), (-ult).tolist(), vol.tolist(), (lado == 1).tolist(), (-aa).tolist(), aav.tolist(), (-da).tolist())
    cc = ["id", "P", "t", "escalon", "S", "R", "SR", "D", "t0"]; cf = ["id", "P", "t0", "tlast", "tfin", "S", "R", "SR", "D", "como"]
    C = pd.concat([pd.DataFrame(cb, columns=cc).assign(punta="bid"), pd.DataFrame(ca, columns=cc).assign(punta="ask")], ignore_index=True)
    F = pd.concat([pd.DataFrame(fb, columns=cf).assign(punta="bid"), pd.DataFrame(fa, columns=cf).assign(punta="ask")], ignore_index=True)
    for X in (C, F): X.loc[X["punta"] == "ask", "P"] *= -1
    C["sesion"] = s; F["sesion"] = s
    # precio de las absorciones por segundo
    venta = lado == -1; compra = lado == 1
    absb = venta & (vol >= abv) & (db >= ab); absa = compra & (vol >= aav) & (da <= aa)
    sec = d["t"].dt.floor("s")
    pb = pd.Series(ult[absb], index=sec[absb]).groupby(level=0).min(); pa = pd.Series(ult[absa], index=sec[absa]).groupby(level=0).max()
    PX = pd.DataFrame({"abs_bid_px": pb, "abs_ask_px": pa})
    return C, F, PX


if __name__ == "__main__":
    os.makedirs(A.MI_CACHE, exist_ok=True); t00 = time.time()
    todas = [os.path.basename(f)[4:-8] for f in sorted(__import__("glob").glob(os.path.join(B.CACHE, "seg-*.parquet")))]
    pedir = sys.argv[1:] or todas
    for s in pedir:
        f1 = os.path.join(A.MI_CACHE, "plantadas-cruces-%s.parquet" % s)
        if os.path.exists(f1): continue
        if time.time() - t00 > 200: print("corte por tiempo; volver a correr para seguir"); break
        t0 = time.time(); C, F, PX = sesion(s)
        F = F[F["S"] >= 10]   # solo las de 10+ contratos: el resto son millones de filas de 1-5 contratos que no se usan
        C.to_parquet(f1); F.to_parquet(os.path.join(A.MI_CACHE, "plantadas-finales-%s.parquet" % s)); PX.to_parquet(os.path.join(A.MI_CACHE, "abspx-%s.parquet" % s))
        print(s, "cruces", len(C), "plantadas", len(F), "seg con abs", len(PX), "en %.1f s" % (time.time() - t0), flush=True)
