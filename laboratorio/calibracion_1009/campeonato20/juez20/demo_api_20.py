# -*- coding: utf-8 -*-
"""demo_api_20.py — prueba de punta a punta de la API de juez20 con muchas noches (NO es un resultado del campeonato). SOLO LECTURA.
Conjunto: las dominantes que DIBUJO la 2.0 (dom0, dom1 de QQQ + ndx_dom0, ndx_dom1 de NDX; niv20_m1.pkl, desfase 1) sobre las velas
M1 de MNQZ6. Ventana por noche 22:00Z -> 13:30Z. Solo las noches de MIRAR (los 2/3 primeros): la PRUEBA no se toca aca.
Mide el tiempo de evaluar() con placebo de 500 juegos. Uso: python -I demo_api_20.py [n_azar]"""
import ctypes
import json
import os
import sys
import time

import pandas as pd

try:
    ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)
except Exception:
    pass

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import juez20 as J20  # noqa: E402
from velas_hasta_ahora import construir  # noqa: E402

CAL = os.path.join(os.environ["LOCALAPPDATA"], "Temp", "claude", "C--Users-wmx-7-OneDrive-Escritorio-ATAS-nada",
                   "f5879819-e2a4-405b-87b0-959ace090d7b", "scratchpad", "calibrar")
CLAVES = ("dom0", "dom1", "ndx_dom0", "ndx_dom1")


def main():
    n_azar = int(sys.argv[1]) if len(sys.argv) > 1 else 500
    V, _ = construir()
    niv = pd.read_pickle(os.path.join(CAL, "niv20_m1.pkl"))
    R = J20.J.rayas_desde_niv20(niv, CLAVES)
    ks = sorted(R)
    noches = sorted({(pd.Timestamp(k) + pd.Timedelta(hours=2)).strftime("%Y-%m-%d") for k in ks})
    noches = [n for n in noches if ((V["t"] >= pd.Timestamp(n) - pd.Timedelta(hours=2)) & (V["t"] < pd.Timestamp(n) + pd.Timedelta(hours=13.5))).sum() > 60]
    mirar, prueba = J20.particion_noches(noches)
    print("noches con niveles de la 2.0 y velas: %d -> mirar %d (%s..%s), prueba %d (%s..%s; NO se evalua aca)" % (
        len(noches), len(mirar), mirar[0], mirar[-1], len(prueba), prueba[0], prueba[-1]))
    t0 = time.time()
    res = J20.evaluar(V, R, ventanas=J20.ventanas_de_noches(mirar), opciones={"claves": CLAVES}, n_azar=n_azar)
    dt = time.time() - t0
    m = res["metricas"]; pl = res["placebo"]; az = pl["azar"]; ic = res["bootstrap_ic90"]
    print("tiempo evaluar (con placebo %d + 6 corridas + bootstrap 2000): %.1f s; pistas %d; noches en ventana %d" % (
        n_azar, dt, res["n_pistas"], len(res["noches_ventana"])))
    print("llegadas %d (base %d, censuradas %d): aciertos %d, falsas %d, indefinidas %d" % (
        m["llegadas"], m["base"], m["censuradas"], m["aciertos"], m["falsas"], m["indefinidas"]))
    print("ACIERTO20 %.1f %% (IC90 %.1f-%.1f) | decididas %.1f %% | tasa base azar %.1f %% (p5 %.1f p95 %.1f) | corridas %.1f %% | "
          "percentil %.1f p %.4f" % (m["pct_acierto20"], *ic["pct_acierto20"], m["pct_acierto20_decididas"], pl["tasa_base_azar"],
                                     az["pct_acierto20"]["p5"], az["pct_acierto20"]["p95"], pl["tasa_base_corridas"],
                                     az["pct_acierto20"]["percentil"], az["pct_acierto20"]["p_valor"]))
    print("por noche: %.2f aciertos (IC90 %.2f-%.2f), %.2f falsas (IC90 %.2f-%.2f) | rayas/h %.2f (p90 %.1f, simultaneas %.0f)" % (
        m["aciertos_por_noche"], *ic["aciertos_por_noche"], m["falsas_por_noche"], *ic["falsas_por_noche"], m["rayas_por_hora"],
        m["rayas_por_hora_p90"], m["rayas_simultaneas_mediana"]))
    print("recorrido mediano tras acierto %.1f | precision mediana |%.2f| (%+.2f) | dist rotura mediana %.2f | expectativa DESCRIPTIVA "
          "%.2f pts/llegada decidida (azar p50 %.2f, percentil %.1f); con -5 fijo %.2f" % (
              m["recorrido_mediano_acierto"], m["precision_mediana_abs"], m["precision_mediana"], m["dist_rotura_mediana"],
              m["expectativa_pts"], az["expectativa_pts"]["p50"], az["expectativa_pts"]["percentil"], m["expectativa_pts_fija"]))
    with open(os.path.join(AQUI, "datos", "demo_api_20.json"), "w", encoding="utf-8") as fh:
        json.dump({"tiempo_s": dt, "metricas": m, "bootstrap_ic90": ic, "placebo": {k: v for k, v in pl.items()},
                   "mirar": mirar, "prueba_no_evaluada": prueba}, fh, ensure_ascii=False, indent=1, default=str)


if __name__ == "__main__":
    main()
