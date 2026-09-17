# -*- coding: utf-8 -*-
"""punta_08_niveles.py — EXPLORATORIO, solo en EXPLORAR, fuera del pre-registro (no cuenta para el veredicto y NO se corrio en confirmar):
cuando el precio llega a un nivel de gamma (zero, dominantes, majors), los LIMITES lo defienden? Se ve en el flujo pasivo o en la punta?
Hipotesis escritas antes de correr:
  precio a <= 5 pts del nivel mas cercano (nivel de la ULTIMA vela de 1 min cerrada, llevado al segundo con el movimiento de la cinta).
  lado 'defensa' = rebote: compra si el precio esta ARRIBA del nivel, venta si esta ABAJO.
  grupos por flujo pasivo de 60 s alineado con la defensa: DEFENDIDO (zP60 >= +1,25), NEUTRO (|zP60| < 0,5), EN CONTRA (zP60 <= -1,25);
  y por punta de 10 s: CARGADA A FAVOR de la defensa (QI_10 >= +0,17), PAREJA (|QI_10| < 0,05), EN CONTRA (<= -0,17).
  Se juzga el rebote con la barrera +-8 / 600 s, desagrupado 60 s, con placebo corto."""
import sys; sys.path.insert(0, r"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\gatillo")
import numpy as np, pandas as pd, base as B, punta_lib as L

CERCA = 5.0; NIV = ["d_zero", "d_dom0", "d_dom1", "d_mp", "d_mn"]
T = B.todas_las_sesiones(); S = B.explorar(T); del T
N = B.niveles_m1()[NIV].copy(); N.index = N.index + pd.Timedelta(minutes=1)      # en el minuto m se conoce la vela m-1

grupos = {}; OK, T0, Y = {}, {}, {}
for s, d in S.items():
    f = L.rasgos(d); OK[s] = L.elegible(d); T0[s] = L.t0_de(d); Y[s] = L.barrera_cache(s, d, 8, 600)[0]
    m = d.index.floor("min"); cierre_prev = d["ultimo"].resample("1min").last().shift(1).reindex(m).to_numpy()
    corr = d["ultimo"].to_numpy() - cierre_prev
    dist = N.reindex(m).to_numpy() + corr[:, None]                                  # precio - nivel, en el segundo t
    k = np.nanargmin(np.where(np.isnan(dist), np.inf, np.abs(dist)), axis=1); dmin = dist[np.arange(len(d)), k]
    cerca = np.abs(dmin) <= CERCA; defensa = np.where(dmin >= 0, 1, -1)
    zp = np.nan_to_num(f["zP60"].to_numpy()) * defensa; qi = np.nan_to_num(f["qi10"].to_numpy()) * defensa
    G = {"cerca del nivel, TODOS": cerca,
         "pasivo DEFIENDE (zP60 >= 1,25)": cerca & (zp >= 1.25), "pasivo neutro": cerca & (np.abs(zp) < 0.5), "pasivo EN CONTRA (<= -1,25)": cerca & (zp <= -1.25),
         "punta cargada A FAVOR (QI10 >= 0,17)": cerca & (qi >= 0.17), "punta pareja": cerca & (np.abs(qi) < 0.05), "punta EN CONTRA (<= -0,17)": cerca & (qi <= -0.17)}
    for nb, cond in G.items():
        lado = np.where(cond, defensa, 0); i = L.desagrupar(lado, OK[s]); grupos.setdefault(nb, {})[s] = (i, lado[i].astype(int))

print("EXPLORAR, rebote en el nivel de gamma mas cercano (<= %.0f pts), barrera +-8/600 s (empate 55,3 %%)" % CERCA)
for nb, disp in grupos.items():
    y = np.concatenate([Y[s][disp[s][0]] for s in S]); lado = np.concatenate([disp[s][1] for s in S]); dias = np.concatenate([[s] * len(disp[s][0]) for s in S])
    r = B.juzgar(y, lado, nb, dias, 8); m, sd = L.placebo(disp, OK, T0, Y, sorteos=50)
    print("  %-40s n %4d | acierto %.1f %% | z %+.2f | placebo %.1f +- %.1f -> z %+.2f | dias arriba %s | peor dia %.0f %%" % (
        nb, r["n"], r["acierto"], r["z"], m, sd, (r["acierto"] - m) / sd, r["dias_arriba"], r["peor_dia"]))
