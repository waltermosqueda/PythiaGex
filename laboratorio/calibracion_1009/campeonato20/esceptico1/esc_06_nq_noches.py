# -*- coding: utf-8 -*-
"""esc_06_nq_noches.py — ESCEPTICO 1: MUROS_NQ_vol de noche (la mas 'limpia' de MIRAR: robusta a 1-15 min de retraso y pico en 0 del
barrido de corrimiento). (1) por noche y jackknife (sacando una noche por vez) en MIRAR; (2) la noche del roll (09-15: corrimiento de
contrato con muestras que miran hasta 2 min adelante, MinuteroNq) y las noches U6 (09-08..09-14) aparte; (3) barrido de corrimiento
-15..+15 en PRUEBA. SOLO LECTURA. Uso: python -I esc_06_nq_noches.py"""
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


def main():
    V, reg, part = E.nq_cargar()
    VV = E.preparar_velas(V)
    res = {}
    for serie in ("MUROS_NQ_vol", "MAJORS_NQ_vol", "MUROS_NQ_vol.P"):
        dias = part["noche"]["mirar"]
        rayas, vent = E.nq_rayas_y_ventanas(reg, dias, serie, "noche")
        r, evs = E.juzgar(VV, rayas, vent)
        pn = {}
        for e in evs:
            x = pn.setdefault(e["noche"], [0, 0]); x[0] += e["res"] == "acierto"; x[1] += e["res"] == "falsa"
        jk = {}
        for d in dias:
            ac = sum(v[0] for k, v in pn.items() if k != d); fa = sum(v[1] for k, v in pn.items() if k != d)
            jk[d] = 100.0 * ac / (ac + fa) if ac + fa else float("nan")
        corr_dias = {d: sum(1 for x in reg[d] if x.get("corr")) for d in dias}
        sin_roll = [d for d in dias if d != "2026-09-15"]
        r_sr, _ = E.juzgar(VV, *E.nq_rayas_y_ventanas(reg, sin_roll, serie, "noche"))
        z6 = [d for d in dias if d >= "2026-09-15"]
        r_z6, _ = E.juzgar(VV, *E.nq_rayas_y_ventanas(reg, z6, serie, "noche"))
        res[serie] = dict(mirar=r, por_noche=pn, jackknife=jk, minutos_con_corr=corr_dias, sin_roll=r_sr, solo_z6=r_z6)
        out("%s MIRAR noche: %d llegadas %.1f %% | sin la noche del roll 09-15: %d %.1f %% | solo noches Z6 (09-15..): %d %.1f %%" % (
            serie, r["llegadas"], r["pct_acierto20"], r_sr["llegadas"], r_sr["pct_acierto20"], r_z6["llegadas"], r_z6["pct_acierto20"]))
        out("   por noche (aciertos/falsas): %s" % " ".join("%s:%d/%d" % (k[5:], v[0], v[1]) for k, v in sorted(pn.items())))
        out("   jackknife (sacando cada noche): min %.1f max %.1f | minutos con corrimiento de contrato por noche: %s" % (
            min(jk.values()), max(jk.values()), {k[5:]: v for k, v in corr_dias.items() if v}))
        # barrido en PRUEBA
        dias_p = part["noche"]["prueba"]
        rp, vp = E.nq_rayas_y_ventanas(reg, dias_p, serie, "noche")
        P = E.pistas(VV, rp, 0)
        bar = {}
        for c in range(-15, 16):
            rr, _ = E.juzgar(VV, None, vp, P=P, corrimiento=float(c))
            bar[c] = rr["pct_acierto20"]
        vec = [bar[c] for c in bar if c != 0]
        res[serie]["prueba_barrido"] = bar
        out("   PRUEBA barrido: 0 = %.1f %% | media +-1..15 %.1f | puesto de 0 (1 = el mejor) %d de 31 | %s" % (
            bar[0], float(np.mean(vec)), 1 + sum(v > bar[0] for v in vec), " ".join("%+d:%.0f" % (c, bar[c]) for c in range(-15, 16, 3))))
    json.dump(res, open(os.path.join(AQUI, "datos", "nq_noches.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1, default=str)
    open(os.path.join(AQUI, "datos", "nq_noches.txt"), "w", encoding="utf-8").write("\n".join(LOG) + "\n")


if __name__ == "__main__":
    main()
