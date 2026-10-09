# -*- coding: utf-8 -*-
"""
g3_verificar.py — paridad de la implementacion del grupo 3 contra cuentas INDEPENDIENTES del proyecto (solo lectura):
  * C13 / C14 / C16(a = C03): perfil por strike recalculado con receta_2_0/extraer_paridad_2_0.calcular (port 1:1 de la 2.0) y
    con tres/nucleo.calcular; seleccion rehecha a mano con sorted() de Python.
  * C16(b): NDX con base 'cuatro' por la misma receta (escala=None, base=B).
  * C18: bucle escalar por fila (math) sobre todos los vencimientos con 0 <= dias_env <= 31.
  * C15: bucle escalar por fila con Black-76.
Compara contra probadores/grupo3/<ID>/niveles_ENTRENAMIENTO.parquet en minutos al azar. Escribe verificacion_g3.json.
"""
import os
import sys
import json
import math
import datetime as dt

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(AQUI)
LAB = os.path.dirname(RAIZ)
for _p in (os.path.join(RAIZ, "datos"), os.path.join(RAIZ, "arnes"), os.path.join(RAIZ, "receta_2_0"), os.path.join(LAB, "tres")):
    if _p not in sys.path:
        sys.path.insert(0, _p)
import cargar as D                       # noqa: E402
import evaluar as EV                     # noqa: E402
import extraer_paridad_2_0 as RC         # noqa: E402  (solo funciones; main() no corre)
import nucleo as NU                      # noqa: E402

NIV = os.path.join(RAIZ, "probadores", "grupo3")


def niv(cid):
    x = pd.read_parquet(os.path.join(NIV, cid, "niveles_ENTRENAMIENTO.parquet"))
    return {t: g for t, g in x.groupby("t")}


def cad_receta(libro, dia, foto):
    f = D.fotos(libro, dia).set_index("foto").loc[foto]
    y = D.filas(libro, dia, foto)
    vencs = json.loads(f["vencs"])
    orden = sorted(vencs, key=lambda k: vencs[k])
    ix = {int(k): i for i, k in enumerate(orden)}
    filas = [[r.strike, ix[int(r.venc)], r.oi_call, r.oi_put, r.iv_call, r.iv_put, r.vol_call, r.vol_put] for r in y.itertuples()]
    gen = pd.Timestamp(f["generado"]).tz_localize("UTC").isoformat()
    return {"generado": gen, "cadena": {"vencimientos": [{"dias": vencs[k]} for k in orden], "filas": filas}}, [vencs[k] for k in orden], filas


def main(n_min=150, semilla=7):
    rng = np.random.default_rng(semilla)
    dias = [d for d in D.dias("QQQ") if d <= EV.ENTRENAMIENTO_HASTA]
    N13, N14, N16, N18 = niv("C13_IMAN_QQQ_vol"), niv("C14_REPEL_QQQ_vol"), niv("C16_CONFLUENCIA_QQQ_NDX"), niv("C18_CW_PW_OI_QQQ")
    res = {"C13": [0, 0], "C14": [0, 0], "C03_vs_receta": [0, 0], "C16": [0, 0], "C18": [0, 0], "perfil_vs_nucleo_maxrel": 0.0}
    malos = []
    for _ in range(n_min):
        d = dias[rng.integers(len(dias))]
        g = EV.minutos_y_precio(d)
        sq = D.serie_conversion("QQQ", d, "cuatro"); sn = D.serie_conversion("NDX", d, "cuatro")
        ok = np.isfinite(g["F"].to_numpy()) & np.isfinite(sq["valor"].to_numpy()) & (sq["foto"].fillna(-1).to_numpy() >= 0)
        cand = np.nonzero(ok)[0]
        if not len(cand):
            continue
        i = int(cand[rng.integers(len(cand))])
        t = g["t"].iat[i]; F = float(g["F"].iat[i]); rho = float(sq["valor"].iat[i]); fo = int(sq["foto"].iat[i])
        cad, dv, filas = cad_receta("QQQ", d, fo)
        tpy = t.to_pydatetime()
        r = RC.calcular(cad, F, tpy, escala=rho)
        # perfil independiente (nucleo, vectorizado distinto) para C13/C14
        C = NU.Cadena(filas, dv, 0.0, False, pd.Timestamp(cad["generado"]).to_pydatetime())
        pn = NU.calcular(C, F, pd.Timestamp(t).tz_localize("UTC").to_pydatetime(), razon=rho)
        perf = [(k, fu, gv, go) for k, fu, gv, go, _ in pn["perfil"]]
        R = min(0.02 * F, 100.0)
        enR = [p for p in perf if abs(p[1] - F) <= R]
        # C13
        Nt = sum(p[2] for p in perf if abs(p[1] - F) <= 0.01 * F)
        esp13 = sorted([p for p in enR if p[2] > 0], key=lambda p: (-p[2], p[0]))[:2] if Nt > 0 else []
        got = N13.get(t)
        got13 = [] if got is None else list(zip(got["K"], got["etiqueta"]))
        e13 = [(p[0], "I%d" % (k + 1)) for k, p in enumerate(esp13)]
        res["C13"][0] += 1; res["C13"][1] += e13 == [(float(a), b) for a, b in got13]
        if e13 != [(float(a), b) for a, b in got13]:
            malos.append(("C13", str(t), e13, got13))
        esp14 = sorted([p for p in enR if p[2] < 0], key=lambda p: (p[2], p[0]))[:2]
        got = N14.get(t)
        got14 = [] if got is None else list(zip(got["K"], got["etiqueta"]))
        e14 = [(p[0], "N%d" % (k + 1)) for k, p in enumerate(esp14)]
        res["C14"][0] += 1; res["C14"][1] += e14 == [(float(a), b) for a, b in got14]
        if e14 != [(float(a), b) for a, b in got14]:
            malos.append(("C14", str(t), e14, got14))
        # C03 (a de C16) contra la receta de la 2.0
        a = [(x[0], x[2]) for x in r["doms"]]
        # C16 completo
        b = []
        fn = sn["foto"].iat[i]; B = sn["valor"].iat[i]
        if fn == fn and fn >= 0 and B == B:
            cn, _, _ = cad_receta("NDX", d, int(fn))
            rn = RC.calcular(cn, F, tpy, base=float(B))
            b = [(x[0], x[2]) for x in rn["doms"]]
        pares = sorted(((abs(x[0] - y[0]), ia, ib) for ia, x in enumerate(a) for ib, y in enumerate(b) if abs(x[0] - y[0]) <= 5.0), key=lambda z: z[0])
        ua, ub, e16 = set(), set(), []
        for _, ia, ib in pares:
            if ia in ua or ib in ub or len(e16) >= 2:
                continue
            ua.add(ia); ub.add(ib); e16.append(round(0.5 * (a[ia][0] + b[ib][0]), 6))
        got = N16.get(t)
        g16 = [] if got is None else [round(float(v), 6) for v in got["nivel"]]
        ok16 = len(e16) == len(g16) and all(abs(x - y) < 1e-6 for x, y in zip(e16, g16))
        res["C16"][0] += 1; res["C16"][1] += ok16
        if not ok16:
            malos.append(("C16", str(t), e16, g16))
        # C18 escalar
        env = max(0.0, min(2.0, (t - pd.Timestamp(cad["generado"]).tz_localize(None)).total_seconds() / 86400.0))
        S = F / rho
        cw, pw = {}, {}
        for f_ in filas:
            de = dv[f_[1]] - env
            if not (0 <= de <= 31):
                continue
            T = max(de, 1 / 1440) / 365.0
            gc = RC.gamma_bs(S, f_[0], T, f_[4], 0.0375); gp = RC.gamma_bs(S, f_[0], T, f_[5], 0.0375)
            cw[f_[0]] = cw.get(f_[0], 0.0) + gc * f_[2] * 100 * S * S * 0.01
            pw[f_[0]] = pw.get(f_[0], 0.0) + gp * f_[3] * 100 * S * S * 0.01
        e18 = []
        if cw and max(cw.values()) > 0:
            e18.append(("CW", sorted(cw, key=lambda k: (-cw[k], k))[0]))
        if pw and max(pw.values()) > 0:
            e18.append(("PW", sorted(pw, key=lambda k: (-pw[k], k))[0]))
        got = N18.get(t)
        g18 = [] if got is None else list(zip(got["etiqueta"], got["K"].astype(float)))
        res["C18"][0] += 1; res["C18"][1] += e18 == g18
        if e18 != g18:
            malos.append(("C18", str(t), e18, g18))
        # C03 de mi seleccion contra la receta (doms con K)
        import g3_niveles as G3
        Q = G3.LibroCboe("QQQ", d)
        pp = G3.perfil_cboe(Q.filas(fo), t, S, lambda k: k * rho, "Hoy")
        mia, _ = G3.seleccion_dos(pp, F)
        okc = [round(x[1], 6) for x in mia] == [round(x[1], 6) for x in a]
        res["C03_vs_receta"][0] += 1; res["C03_vs_receta"][1] += okc
        if not okc:
            malos.append(("C03", str(t), mia, a))
        # perfil gv mio contra nucleo
        mp = dict(zip(pp["K"], pp["gv"]))
        for k, fu, gv, go in perf:
            if k in mp and abs(gv) > 1:
                res["perfil_vs_nucleo_maxrel"] = max(res["perfil_vs_nucleo_maxrel"], abs(mp[k] - gv) / abs(gv))
    # C15: bucle escalar Black-76
    N15 = niv("C15_FLUJO_NQ")
    dn = [d for d in D.dias("NQ") if d <= EV.ENTRENAMIENTO_HASTA]
    res["C15"] = [0, 0]
    for _ in range(n_min):
        d = dn[rng.integers(len(dn))]
        g = EV.minutos_y_precio(d)
        sc = D.serie_conversion("NQ", d, "cuatro")
        ok = np.isfinite(g["F"].to_numpy()) & (sc["foto"].fillna(-1).to_numpy() >= 0)
        cand = np.nonzero(ok)[0]
        if not len(cand):
            continue
        i = int(cand[rng.integers(len(cand))])
        t = g["t"].iat[i]; F = float(g["F"].iat[i]); fo = int(sc["foto"].iat[i]); corr = float(sc["valor"].iat[i])
        fr = D.fotos("NQ", d).set_index("foto").loc[fo]
        y = D.filas("NQ", d, fo)
        env = max(0.0, min(2.0, (t - pd.Timestamp(fr["ts"])).total_seconds() / 86400.0))
        de_all = y["dias"].to_numpy() - env
        val = de_all[de_all >= 0]
        tope = max(1.0, (val.min() if len(val) else 0.0) + 0.01)
        S = F - corr
        G = {}
        for r_ in y.itertuples():
            de = r_.dias - env
            if not (0 <= de <= tope):
                continue
            T = max(de, 1 / 1440) / 365.0
            iv = r_.iv
            if S <= 0 or r_.strike <= 0 or T <= 0 or not (iv > 0):
                gm = 0.0
            else:
                v = iv * math.sqrt(T); d1 = (math.log(S / r_.strike) + 0.5 * iv * iv * T) / v
                gm = math.exp(-0.5 * d1 * d1) / math.sqrt(2 * math.pi) / (S * v)
            G[r_.strike] = G.get(r_.strike, 0.0) - gm * ((r_.vol_compra or 0) - (r_.vol_venta or 0)) * 20 * S * S * 0.01
        R = min(0.02 * F, 100.0)
        e15 = []
        if any(v != 0 for v in G.values()):
            for lado, et in ((lambda fu: fu > F, "CW+"), (lambda fu: fu <= F, "PW+")):
                c = [k for k in G if lado(k + corr) and abs(k + corr - F) <= R and G[k] > 0]
                if c:
                    e15.append((et, sorted(c, key=lambda k: (-G[k], k))[0]))
        got = N15.get(t)
        g15 = [] if got is None else list(zip(got["etiqueta"], got["K"].astype(float)))
        res["C15"][0] += 1; res["C15"][1] += e15 == g15
        if e15 != g15:
            malos.append(("C15", str(t), e15, g15))
    res["malos"] = malos[:30]
    with open(os.path.join(AQUI, "verificacion_g3.json"), "w", encoding="utf-8") as fh:
        json.dump(res, fh, ensure_ascii=False, indent=1, default=str)
    print(json.dumps({k: v for k, v in res.items() if k != "malos"}), "malos:", len(malos))
    for m in malos[:10]:
        print(m)


if __name__ == "__main__":
    sys.path.insert(0, AQUI)
    main()
