# -*- coding: utf-8 -*-
"""ei_chequeos.py — controles de los conjuntos de referencia antes de medir. SOLO LECTURA. Correr: python -I ei_chequeos.py
 1) SIN MIRAR ADELANTE: cada conjunto causal se rearma con las velas cortadas en T y todas sus claves <= T tienen que dar igual
    que con la serie entera (8 cortes al azar por conjunto + el corte de la ultima vela de cada sesion de CME de las 3 ultimas).
 2) El zigzag del ORACULO es el mismo del juez: cada giro de juez_operador._pivotes esta en el oraculo con la misma vela y precio.
 3) El ORACULO_TRAMO cubre el 100 % de los giros del juez (cobertura +-2) en la ventana congelada.
 4) Muestra lo que dibuja cada conjunto esta noche a las 02:00Z y 03:00Z (para mirarlo a ojo contra el grafico)."""
import os
import sys

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
sys.path.insert(0, os.path.join(os.path.dirname(AQUI), "juez"))
import ei_conjuntos as C  # noqa: E402
import juez_operador as J  # noqa: E402

CAL = os.path.join(os.environ["LOCALAPPDATA"], "Temp", "claude", "C--Users-wmx-7-OneDrive-Escritorio-ATAS-nada",
                   "f5879819-e2a4-405b-87b0-959ace090d7b", "scratchpad", "calibrar")


def iguales(a, b):
    if len(a) != len(b):
        return False
    for (x, ex), (y, ey) in zip(a, b):
        if abs(x - y) > 1e-9 or ex != ey:
            return False
    return True


def main():
    V = C.cargar_velas(os.path.join(CAL, "velas_m1.csv"))
    print("velas %d: %s -> %s" % (len(V), V["t"].iloc[0], V["t"].iloc[-1]))
    rng = np.random.default_rng(7)
    cortes = sorted(set(rng.integers(200, len(V) - 1, 8).tolist()))
    noche = C.etiqueta_noche(V["t"])
    for n in sorted(set(noche))[-4:-1]:
        cortes.append(int(np.flatnonzero(noche == n)[-1]))
    for nom in C.CAUSALES:
        full = C.armar(V, nom)
        ts = V["t"].tolist()
        malos = 0
        for T in cortes:
            parcial = C.armar(V.iloc[:T + 1].reset_index(drop=True), nom)
            for i in range(T + 1):
                if not iguales(full[ts[i]], parcial[ts[i]]):
                    malos += 1
                    if malos <= 3:
                        print("   MIRA ADELANTE? %s corte %s clave %s: %s vs %s" % (nom, ts[T], ts[i], full[ts[i]], parcial[ts[i]]))
        print("1) %-8s sin mirar adelante en %d cortes: %s" % (nom, len(cortes), "OK" if malos == 0 else "FALLA (%d)" % malos))
        assert malos == 0, nom
    # 2) zigzag igual al del juez
    op = J._op(None)
    Vp = J.preparar_velas(V[["t", "o", "h", "l", "c"]], op)
    assert len(Vp["t"]) == len(V) and (pd.to_datetime(Vp["t"]) == V["t"]).all()
    piv_j = J._pivotes(Vp, op)
    mios = {(k, tp) for a, b, P in C.pivotes_todos(V) for tp, k, kc in P}
    falta = [p for p in piv_j if (p["k"], p["tipo"]) not in mios]
    print("2) giros del juez (zigzag 20, toda la serie): %d; los del oraculo: %d; del juez que faltan en el oraculo: %d" % (
        len(piv_j), len(mios), len(falta)))
    assert not falta
    # 3) cobertura del oraculo
    ven = []
    for n in pd.read_pickle(os.path.join(CAL, "noches.pkl")):
        D = pd.Timestamp(n["D"]); N = pd.Timestamp(n["N"])
        a = N - pd.Timedelta(hours=2) if D.weekday() == 4 else N + pd.Timedelta(minutes=35)
        ven.append((a, N + pd.Timedelta(hours=8, minutes=30)))
    R = C.armar(V, "ORACULO_TRAMO")
    cg = J.cobertura_giros(V[["t", "o", "h", "l", "c"]], R, {"ventanas": ven})
    print("3) cobertura del ORACULO_TRAMO (ventana congelada, 19 noches): %d de %d (%.1f %%)" % (cg["cubiertos"], cg["n_giros"], cg["pct"]))
    sin = [g for g in cg["giros"] if not g["cubierto"]]
    for g in sin[:5]:
        print("   sin cubrir: %s %s %.2f aprox %s raya mas cercana %s" % (g["t"], g["tipo"], g["precio"], g["t_aprox"], g["raya_mas_cercana"]))
    # 4) a ojo
    for hh in ("2026-10-09 01:59", "2026-10-09 02:59"):
        t = pd.Timestamp(hh)
        print("4) dibujado al cerrar la vela %s (vigente en la siguiente); cierre %.2f" % (hh, float(V.loc[V["t"] == t, "c"].iloc[0])))
        for nom in ("ORACULO_TRAMO", "ORACULO_NOCHE") + C.CAUSALES:
            r = C.armar(V, nom)[t]
            print("     %-14s %s" % (nom, ", ".join("%.2f %s" % (x, e) for x, e in sorted(r))[:230]))
    print("\nCHEQUEOS OK")


if __name__ == "__main__":
    main()
