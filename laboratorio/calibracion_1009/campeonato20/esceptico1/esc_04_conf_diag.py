# -*- coding: utf-8 -*-
"""esc_04_conf_diag.py — ESCEPTICO 1: por que CONF_vol / CONF_NDX_NQ_vol de RUEDA dan 52-53 % con desfase 0 y 42-45 % con 1 minuto de
mas o de menos. SOLO LECTURA. (1) parpadeo: cuantas rayas cambian/aparecen minuto a minuto; edad de la raya al llegar (minutos desde que
nacio la pista); (2) llegadas con desfase 0 contra +1: comunes y exclusivas, y el % de cada grupo; (3) lo mismo en PRUEBA y en la NOCHE.
Escribe datos/conf_diag.json y .txt. Uso: python -I esc_04_conf_diag.py"""
import ctypes
try:
    ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)
except Exception:
    pass
import json
import os
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import numpy as np  # noqa: E402
import pandas as pd  # noqa: E402
import esc_comun as E  # noqa: E402

LOG = []


def out(s):
    print(s, flush=True); LOG.append(s)


def pct(evs):
    b = [e for e in evs if e["res"] != "censurada"]
    return (100.0 * sum(e["res"] == "acierto" for e in b) / len(b)) if b else float("nan"), len(b)


def main():
    res = {}
    for serie in ("CONF_vol", "CONF_NDX_NQ_vol", "FAM_MUROS_vol"):
        V, R = E.conf_cargar(serie)
        for ven in ("rueda", "noche"):
            for parte in ("MIRAR", "PRUEBA"):
                Vs, Rs, vent, sel = E.conf_parte(V, R, ven, parte)
                VV = E.preparar_velas(Vs)
                P0 = E.pistas(VV, Rs, 0)
                # parpadeo y edad
                naci = {pid: int(idx[0]) for pid, (idx, _) in enumerate(P0) if len(idx)}
                vida = np.array([len(idx) for idx, _ in P0])
                r0, e0 = E.juzgar(VV, None, vent, P=P0)
                r1, e1 = E.juzgar(VV, Rs, vent, desfase=1)
                rm, em = E.juzgar(VV, Rs, vent, desfase=-1)
                edad = np.array([e["i"] - naci[e["pista"]] for e in e0])
                k0 = {(e["t"], round(e["raya"], 2)): e for e in e0}
                k1 = {(e["t"], round(e["raya"], 2)): e for e in e1}
                com = set(k0) & set(k1)
                solo0 = [k0[k] for k in set(k0) - com]; solo1 = [k1[k] for k in set(k1) - com]
                comun0 = [k0[k] for k in com]
                nuevas = [e for e in e0 if e["i"] - naci[e["pista"]] == 0]
                viejas = [e for e in e0 if e["i"] - naci[e["pista"]] >= 5]
                d = dict(n_noches=len(sel), pistas=len(P0), vida_mediana_min=float(np.median(vida)) if len(vida) else None,
                         pistas_de_1_min=int((vida == 1).sum()),
                         d0=pct(e0), d1=pct(e1), dm1=pct(em), comunes=len(com), comunes_pct_d0=pct(comun0), solo_d0=pct(solo0),
                         solo_d1=pct(solo1), llegadas_raya_recien_nacida=pct(nuevas), llegadas_raya_con_5min_o_mas=pct(viejas),
                         edad_mediana=float(np.median(edad)) if len(edad) else None)
                res["%s|%s|%s" % (serie, ven, parte)] = d
                out("%-16s %-5s %-6s n=%2d | pistas %5d (vida mediana %.0f min, %d de 1 min) | d0 %.1f%% (%d) d+1 %.1f%% (%d) d-1 %.1f%% (%d) | "
                    "comunes d0/d+1 %d: %.1f%% | solo d0 %.1f%% (%d) | solo d+1 %.1f%% (%d) | raya nacida en la vela de llegada %.1f%% (%d) | "
                    "raya con >= 5 min %.1f%% (%d) | edad mediana %.0f" % (
                        serie, ven, parte, len(sel), len(P0), d["vida_mediana_min"] or -1, d["pistas_de_1_min"], d["d0"][0], d["d0"][1],
                        d["d1"][0], d["d1"][1], d["dm1"][0], d["dm1"][1], len(com), d["comunes_pct_d0"][0], d["solo_d0"][0], d["solo_d0"][1],
                        d["solo_d1"][0], d["solo_d1"][1], d["llegadas_raya_recien_nacida"][0], d["llegadas_raya_recien_nacida"][1],
                        d["llegadas_raya_con_5min_o_mas"][0], d["llegadas_raya_con_5min_o_mas"][1], d["edad_mediana"] or -1))
    json.dump(res, open(os.path.join(AQUI, "datos", "conf_diag.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1, default=str)
    open(os.path.join(AQUI, "datos", "conf_diag.txt"), "w", encoding="utf-8").write("\n".join(LOG) + "\n")


if __name__ == "__main__":
    main()
