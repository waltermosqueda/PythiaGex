# -*- coding: utf-8 -*-
"""barridos_explorar.py — corre las 12 variantes pre-registradas SOLO en B.explorar(T). Barrera principal +-8/600 s; secundarias +-5/300 y +-12/900.
Uso: python barridos_explorar.py            (explorar)
     python barridos_explorar.py confirmar V02_... V07_...   (lo llama barridos_confirmar.py; no usar a mano)"""
import sys, time, json, os
import numpy as np, pandas as pd
import barridos_lib as L
B = L.B


def correr(T, solo=None):
    """Devuelve {variante: DataFrame de disparos con sesion, t, lado, y8, y5, y12, mov10..mov300, media_hora, en_ask}."""
    acc = {}
    for s, d in T.items():
        dis, f = L.disparos(d)
        ys = {x: B.barrera(d, x, h)[0] for x, h in L.BARRERAS}
        p = d["ultimo"].to_numpy(); n = len(p); ask = d["ask"].to_numpy(); bid = d["bid"].to_numpy()
        for v, (i, lado) in dis.items():
            if solo and v not in solo: continue
            if len(i) == 0: continue
            r = pd.DataFrame({"sesion": s, "t": d.index[i], "i": i, "lado": lado})
            for x, h in L.BARRERAS: r["y%d" % x] = ys[x][i]
            for h in (10, 30, 60, 120, 300):
                j = np.minimum(i + h, n - 1); r["mov%d" % h] = (p[j] - p[i]) * lado
            r["media_hora"] = d.index[i].floor("30min").strftime("%H:%M")
            r["en_punta_del_lado"] = np.where(lado > 0, p[i] >= ask[i], p[i] <= bid[i])   # entrar 'a mercado' pagaria esta punta
            acc.setdefault(v, []).append(r)
    return {v: pd.concat(rs, ignore_index=True) for v, rs in acc.items()}


def tabla(res):
    filas = []
    for v in sorted(res):
        r = res[v]; fila = {"variante": v, "disparos": len(r), "compras_%": round(100 * (r["lado"] > 0).mean(), 0)}
        for x, h in L.BARRERAS:
            j = B.juzgar(r["y%d" % x].to_numpy(), r["lado"].to_numpy(), v, r["sesion"].to_numpy(), x)
            if x == 8:
                fila.update({"n8": j["n"], "ac8_%": j.get("acierto"), "z8": j.get("z"), "dias8": j.get("dias_arriba"), "peor_dia8": j.get("peor_dia")})
            else:
                fila.update({"n%d" % x: j["n"], "ac%d_%%" % x: j.get("acierto"), "z%d" % x: j.get("z")})
        for h in (10, 30, 60, 120, 300): fila["mov%d" % h] = round(float(r["mov%d" % h].mean()), 2)
        filas.append(fila)
    return pd.DataFrame(filas)


if __name__ == "__main__":
    t0 = time.time()
    T = B.explorar(B.todas_las_sesiones())
    res = correr(T)
    tb = tabla(res)
    pd.set_option("display.width", 250); pd.set_option("display.max_columns", 50)
    print("EXPLORAR (%d sesiones) — lado segun pre-registro; ac < 50 significa que el ESPEJO acierta 100 - ac" % len(T))
    print(tb.to_string(index=False))
    print("\nempates: +-8 -> %.1f %% | +-5 -> %.1f %% | +-12 -> %.1f %%" % (100 * B.empate(8), 100 * B.empate(5), 100 * B.empate(12)))
    # base de comparacion: cualquier segundo elegible, lado al azar no hace falta (50 %), pero si la tasa 'sube' de la muestra
    sube = []
    for s, d in T.items():
        f_e = L.rasgos(d)["elegible"].to_numpy(); y = B.barrera(d, 8, 600)[0][f_e]; sube.append((s, (y > 0).sum(), (y < 0).sum()))
    up = sum(a for _, a, _ in sube); dn = sum(b for _, _, b in sube)
    print("deriva de la muestra (todos los segundos de rueda, +-8/600): sube primero %.1f %%" % (100 * up / (up + dn)))
    tmp = os.path.join(os.environ.get("TEMP", "."), "barridos_tmp"); os.makedirs(tmp, exist_ok=True)   # volcados de trabajo FUERA del repo
    tb.to_csv(os.path.join(tmp, "explorar.csv"), index=False)
    pd.concat([r.assign(variante=v) for v, r in res.items()]).to_parquet(os.path.join(tmp, "disparos_explorar.parquet"))
    print("%.0f s" % (time.time() - t0))
