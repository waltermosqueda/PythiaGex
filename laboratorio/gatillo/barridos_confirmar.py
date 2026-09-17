# -*- coding: utf-8 -*-
"""barridos_confirmar.py — corre los FINALISTAS (fijados en resultados/barridos.md) UNA vez en B.confirmar(T), con placebo.
Placebo: por cada (sesion, media hora) la misma cantidad de disparos, con los mismos lados, en segundos elegibles al azar separados >= 60 s; 200 sorteos."""
import sys, time, os
import numpy as np, pandas as pd
import barridos_lib as L
from barridos_explorar import correr
B = L.B

FINALISTAS = ["V03_BARRIDO_RUPTURA_SEGUIR", "V11_D50_AISLADO_A_FAVOR"]
SORTEOS = 200
_CACHE = {}


def sortear(elegibles, k, rng, sep=L.SEPARACION):
    """k segundos al azar de 'elegibles' separados al menos sep s (rechazo simple)."""
    for _ in range(50):
        c = np.sort(rng.choice(elegibles, size=k, replace=False))
        if k == 1 or np.diff(c).min() >= sep: return c
    perm = rng.permutation(elegibles); out = []
    for i in perm:
        if all(abs(i - j) >= sep for j in out): out.append(i)
        if len(out) == k: break
    return np.asarray(sorted(out))


def placebo(T, r, x, rng):
    """Acierto de SORTEOS placebos para la barrera x. r = disparos reales (sesion, i, lado, media_hora)."""
    h = dict(L.BARRERAS)[x]; prep = {}
    for s, d in T.items():
        rs = r[r["sesion"] == s]
        if len(rs) == 0: continue
        if (s, "e") not in _CACHE:
            hms = d.index.strftime("%H:%M:%S"); _CACHE[(s, "e")] = d["rueda"].to_numpy() & (hms >= L.INICIO_RUEDA) & (hms < L.FIN_RUEDA)
            _CACHE[(s, "mh")] = d.index.floor("30min").strftime("%H:%M").to_numpy()
        if (s, x) not in _CACHE: _CACHE[(s, x)] = B.barrera(d, x, h)[0]
        f_e = _CACHE[(s, "e")]; mh = _CACHE[(s, "mh")]; y = _CACHE[(s, x)]
        grupos = []
        for m, g in rs.groupby("media_hora"):
            el = np.flatnonzero(f_e & (mh == m)); grupos.append((el, g["lado"].to_numpy()))
        prep[s] = (y, grupos)
    acs = np.empty(SORTEOS)
    for k in range(SORTEOS):
        gan = 0; res = 0
        for s, (y, grupos) in prep.items():
            for el, lados in grupos:
                idx = sortear(el, len(lados), rng); yy = y[idx] * rng.permutation(lados)[:len(idx)]
                gan += int((yy > 0).sum()); res += int((yy != 0).sum())
        acs[k] = gan / max(res, 1)
    return acs


def informe(T, nombre_muestra, rng):
    res = correr(T, solo=FINALISTAS); filas = []
    for v in FINALISTAS:
        r = res[v]
        for x, h in L.BARRERAS:
            j = B.juzgar(r["y%d" % x].to_numpy(), r["lado"].to_numpy(), v, r["sesion"].to_numpy(), x)
            pl = placebo(T, r, x, rng); zpl = (j["acierto"] / 100 - pl.mean()) / pl.std(ddof=1)
            neto = (2 * j["acierto"] / 100 - 1) * x - B.COSTO_PTS   # puntos netos por operacion RESUELTA, barrera simetrica
            filas.append({"muestra": nombre_muestra, "variante": v, "barrera": "+-%d/%ds" % (x, h), "n": j["n"], "acierto_%": j["acierto"], "empate_%": j["empate"],
                          "z_vs_50": j["z"], "placebo_media_%": round(100 * pl.mean(), 1), "placebo_desvio": round(100 * pl.std(ddof=1), 1), "z_vs_placebo": round(zpl, 2),
                          "dias_arriba": j["dias_arriba"], "peor_dia_%": j["peor_dia"], "neto_pts_por_op": round(neto, 2)})
        ok = r["y8"] != 0
        g = r[ok].assign(ac=(r["y8"] * r["lado"] > 0)).groupby("sesion")["ac"].agg(["mean", "size"])
        print("\n%s | %s | por dia (+-8): " % (nombre_muestra, v) + "  ".join("%s %.0f%% (%d)" % (s[5:], 100 * a, n) for s, (a, n) in zip(g.index, g.to_numpy())))
        print("   compras %.0f %% | entrada pagando la punta del lado: %.0f %% | mov medio a favor 10/30/60/120/300 s: %s"
              % (100 * (r["lado"] > 0).mean(), 100 * r["en_punta_del_lado"].mean(), " / ".join("%.2f" % r["mov%d" % h].mean() for h in (10, 30, 60, 120, 300))))
    return pd.DataFrame(filas)


if __name__ == "__main__":
    t0 = time.time(); rng = np.random.default_rng(20260917)
    Tt = B.todas_las_sesiones(); muestra = sys.argv[1] if len(sys.argv) > 1 else "confirmar"
    T = B.confirmar(Tt) if muestra == "confirmar" else B.explorar(Tt)
    pd.set_option("display.width", 250); pd.set_option("display.max_columns", 50)
    tb = informe(T, muestra, rng)
    print("\n" + tb.to_string(index=False))
    print("\n%.0f s" % (time.time() - t0))
