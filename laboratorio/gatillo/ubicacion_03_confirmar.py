# -*- coding: utf-8 -*-
"""
ubicacion_03_confirmar.py — los 2 finalistas, UNA vez, en B.confirmar(T). Con placebo del protocolo (200 sorteos) y placebo de lugar.
Finalistas (congelados en resultados/ubicacion.md): F1 = PAS_sigue ; F2 = SINABS_B en ESPEJO (rebote).
"""
import sys, os, json, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import base as B, ubicacion_lib as UL
import numpy as np, pandas as pd

t0 = time.time(); T = B.todas_las_sesiones(); ENSAYO = os.environ.get("UBIC_ENSAYO") == "1"   # ensayo del codigo sobre EXPLORAR (para no gastar la unica corrida de confirmar en un error de tipeo)
TC = B.explorar(T) if ENSAYO else B.confirmar(T); C = sorted(TC); U = UL.cargar_umbrales()
ev = UL.pegar_resultados(UL.eventos(T, C), TC)
print("ENSAYO SOBRE EXPLORAR" if ENSAYO else "CONFIRMACION", "| sesiones:", C); print("toques: %d | por dia: %s" % (len(ev), ev.groupby("ses").size().to_dict()))


def finalista(tabla, cual):
    if cual == "F1_PAS_sigue": return UL.variante(tabla, "PAS_sigue", U)
    e = UL.variante(tabla, "SINABS_B", U); e["lado"] = -e["lado"]; return e


def neto(e, T_, x=8, h=600):
    out = []
    for s, g in e.groupby("ses"):
        p = T_[s]["ultimo"].to_numpy("float64"); t = g["t"].to_numpy(); l = g["lado"].to_numpy("float64"); y = g["y%d" % x].to_numpy()
        fin = (p[np.minimum(t + h, len(p) - 1)] - p[t]) * l
        out.append(np.where(y * l > 0, x, np.where(y * l < 0, -x, fin)) - B.COSTO_PTS)
    return float(np.concatenate(out).mean())


pl = pd.concat([UL.eventos(T, C, placebo_corr=c, etiqueta="pla%d" % int(c)).assign(rep=int(c)) for c in UL.PLACEBO_CORRIMIENTOS], ignore_index=True)
pl = UL.pegar_resultados(pl, TC)

for cual in ("F1_PAS_sigue", "F2_SINABS_espejo_R"):
    e = finalista(ev, cual); print("\n================ %s — disparos %d" % (cual, len(e)))
    for x, h in UL.BARRERAS:
        j = UL.juzgar_variante(e, x, cual); m, sd, acs = UL.placebo_protocolo(TC, e, (x, h))
        zp = (j["acierto"] / 100.0 - m) / sd
        print("  +-%d/%d: n %d | acierto %.1f %% (empate %.1f) | z vs 50 %.2f | dias arriba %s | peor dia %.1f | t dias %.2f | placebo %.1f +- %.1f %% -> z vs placebo %.2f | neto %.2f pts/op"
              % (x, h, j["n"], j["acierto"], 100 * B.empate(x), j["z"], j["dias_arriba"], j["peor_dia"], UL.t_dias(e, x), 100 * m, 100 * sd, zp, neto(e, TC, x, h)))
    y = e["y8"].to_numpy(); l = e["lado"].to_numpy(); ok = y != 0
    pd_ = pd.DataFrame({"ses": e["ses"].to_numpy()[ok], "ac": (y[ok] * l[ok]) > 0}).groupby("ses")["ac"].agg(["mean", "size"])
    print("  por dia (+-8):", {k: "%.0f%% n%d" % (100 * a, b) for k, (a, b) in zip(pd_.index, pd_.to_numpy())})
    print("  lados: compra %d / venta %d | rebote %d / ruptura %d" % ((l > 0).sum(), (l < 0).sum(), (l == -e["dir"].to_numpy()).sum(), (l == e["dir"].to_numpy()).sum()))
    if cual == "F1_PAS_sigue":
        for nom, mm in (("rama rebote (pas30 bajo)", l == -e["dir"].to_numpy()), ("rama ruptura (pas30 alto)", l == e["dir"].to_numpy())):
            g = e[mm]; j = UL.juzgar_variante(g, 8, nom); print("   %s: n %d acierto %.1f %% dias %s" % (nom, j["n"], j["acierto"], j["dias_arriba"]))
    for k in (10, 30, 60): print("  movimiento medio a favor a %d s: %.2f pts" % (k, float((e["mv%d" % k] * e["lado"]).mean())))
    pe = pd.concat([finalista(pl[pl["rep"] == int(c)], cual) for c in UL.PLACEBO_CORRIMIENTOS], ignore_index=True); j = UL.juzgar_variante(pe, 8, "pla")
    print("  PLACEBO DE LUGAR (misma regla en precios sin significado): n %d acierto %.1f %% z %.2f dias %s" % (j["n"], j["acierto"], j["z"], j["dias_arriba"]))

b = ev.copy(); b["lado"] = -b["dir"]; b = UL.desagrupar(b); j = UL.juzgar_variante(b, 8, "base")
print("\ncontexto: TODOS los toques -> rebote en confirmar: n %d acierto %.1f %% dias %s" % (j["n"], j["acierto"], j["dias_arriba"]))
print("%.0f s" % (time.time() - t0))
