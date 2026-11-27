# -*- coding: utf-8 -*-
"""RONDA 21, paso 7: ETAPA 4 (descriptiva) - vecindario del corte de D1 para COMB-T7+V4 y T6-P1: a en {1,4; 1,6; 1,8} x b en {0,8; 1,0; 1,2}.
Uso: python -I r21_07_vecindario.py"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r21_comun as K
import numpy as np, pandas as pd
import r21_04_estrategia as ES
import r21_06_evolucion as EV3
import r21_02_detector as DT

SAL = os.path.join(K.AQUI, "salidas")


def main():
    ev, cr, d_all, dias = ES.cargar()
    s1 = d_all["s1"].to_numpy()
    c = EV3.candidatos3(ev, cr, dias)
    gex = np.array(["sin"] * len(d_all), dtype=object)
    per_idx = {pp: np.array([i for i, u in enumerate(dias) if K.periodo_de(u) == pp]) for pp in K.PERIODOS}
    L = []
    def W(s=""):
        print(s, flush=True); L.append(s)
    W("ETAPA 4 - vecindario del corte (descriptivo). Celda: acierto %% / neto c0,50 / n por periodo (diseno | confirmacion | guardada)")
    for nombre in ("COMB-T7+V4", "T6-P1"):
        W(""); W("== %s" % nombre); cumple = 0
        for a in (1.4, 1.6, 1.8):
            for b in (0.8, 1.0, 1.2):
                tipo = DT.D1(d_all, (a, b))
                partes = []; ok = []
                for pp in K.PERIODOS:
                    _, tk = EV3.correr(c, nombre, tipo, s1, gex, per_idx[pp]); r = ES.juzgar(tk, per_idx[pp])
                    if r.get("n", 0):
                        partes.append("%5.1f %% %+5.2f n %2d" % (r["acierto"], r["c050_neto_op"], r["n"])); ok.append(r["acierto"] >= 60 and r["c050_neto_op"] > 0)
                    else:
                        partes.append("   sin ops        "); ok.append(False)
                cumple += all(ok)
                _, tk = EV3.correr(c, nombre, tipo, s1, gex, np.arange(len(dias))); rj = ES.juzgar(tk, np.arange(len(dias)))
                W("   corte %.1f / %.1f: %s | juntas %.1f %% %+.2f t %+.2f %s" % (a, b, " | ".join(partes), rj.get("acierto", 0), rj.get("c050_neto_op", 0),
                                                                          rj.get("c050_t", 0), "META" if all(ok) else ""))
                K.anotar(dict(etapa="4-vecindario", familia="post hoc", variante=nombre, parametros="corte D1 %.1f/%.1f" % (a, b), pliegue="por periodo (descriptivo)",
                              muestra="55 ruedas", ruedas=len(dias), n=rj.get("n"), acierto_pct=rj.get("acierto"), neto_op_c050=rj.get("c050_neto_op"),
                              neto_op_c100=rj.get("c100_neto_op"), t_rueda_c050=rj.get("c050_t"), nota="META 3 periodos %s" % all(ok)))
        W("   cortes que cumplen la META en los 3 periodos: %d de 9 -> %s" % (cumple, "ESTABLE" if cumple >= 6 else "NO estable (filo de cuchillo)"))
    open(os.path.join(SAL, "vecindario.txt"), "w", encoding="utf-8").write("\n".join(L))


if __name__ == "__main__":
    main()
