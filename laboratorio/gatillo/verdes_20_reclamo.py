# -*- coding: utf-8 -*-
"""
verdes_20_reclamo.py — EL GATILLO EN SEGUNDOS EN LAS VERDES (pedido del operador 17-09 19:00: "bajemos la temporalidad a segundos...
analiza si contra las rayas verdes funciona aun mejor o es lo mismo").

Lo que el describe: "las velas las traspasan por unos pocos puntos o sus mechas largas y terminan repeliendo". En segundos eso es un RECLAMO:
  1. el precio VENIA de lejos (>= LEJOS pts) de una verde que ya estaba dibujada;
  2. entra a la zona (<= TOL) y puede traspasarla hasta PEN_MAX puntos (la mecha);
  3. GATILLO: dentro de VISITA segundos vuelve al lado bueno: el ultimo precio queda >= REC puntos del lado del rebote respecto de la raya
     (variante 'cruce': exige que la haya traspasado >= 0,5) o rebota >= REB puntos desde el extremo de la mecha (variante 'mecha');
  4. entrada A MERCADO en ese segundo; stop = 1 punto detras del extremo de la mecha; objetivos medidos: +12, +20 y 2 veces el riesgo.
Se mide en puntos NETOS (costo 0,96 ida y vuelta) y contra: las mismas rayas corridas (placebo) y la grilla de strikes de 25 pts en 33 ruedas (muestra grande).
Variantes fijadas ANTES de correr (4): cruce/mecha x con/sin filtro de delta de 10 s a favor. Nada se elige despues de mirar.
"""
import os, sys
import numpy as np, pandas as pd
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import base as B, verdes_lib as V

TOL, LEJOS, VISITA, PEN_MAX, REC, REB = 1.5, 10.0, 90, 10.0, 1.5, 4.0
COSTO = B.COSTO_PTS
VARIANTES = [("cruce", False), ("cruce", True), ("mecha", False), ("mecha", True)]


def gatillos(seg, Lt, variante, con_delta):
    """Lista de (i_entrada, lado, entrada, stop, L, traspaso, seg_en_zona) para una raya Lt[t] (NaN = no vigente)."""
    alto = seg["alto"].to_numpy(); bajo = seg["bajo"].to_numpy(); ult = seg["ultimo"].to_numpy(); dlt = seg["delta"].to_numpy(); rueda = seg["rueda"].to_numpy()
    n = len(ult); ar = np.arange(n); out = []; d10 = pd.Series(dlt).rolling(10, min_periods=1).sum().to_numpy()
    act = ~np.isnan(Lt)
    for lado in (1, -1):
        with np.errstate(invalid="ignore"):
            if lado > 0: lej = act & (bajo >= Lt + LEJOS); zona = act & (bajo <= Lt + TOL)
            else: lej = act & (alto <= Lt - LEJOS); zona = act & (alto >= Lt - TOL)
        entra = zona & ~np.concatenate(([False], zona[:-1]))
        ul = np.maximum.accumulate(np.where(lej, ar, -1)); ue = np.concatenate(([-1], np.maximum.accumulate(np.where(entra, ar, -1))[:-1]))
        for t0 in np.flatnonzero(entra & rueda & (ul >= 0) & (ar - ul <= 1800) & (ul > ue)):
            L = Lt[t0]; ext = bajo[t0] if lado > 0 else alto[t0]
            for t in range(t0, min(n - 1, t0 + VISITA)):
                ext = min(ext, bajo[t]) if lado > 0 else max(ext, alto[t])
                pen = (L - ext) * lado
                if pen > PEN_MAX: break                                  # la rompio: no hay reclamo
                if t == t0: continue
                if variante == "cruce": ok = pen >= 0.5 and (ult[t] - L) * lado >= REC
                else: ok = (ult[t] - ext) * lado >= REB and (ult[t] - L) * lado >= -0.5
                if ok and (not con_delta or d10[t] * lado > 0):
                    out.append((t, lado, ult[t], ext - lado * 1.0, L, max(0.0, pen), t - t0)); break
    return out


def resultado(seg, g, objetivo):
    """Puntos netos de cada gatillo: objetivo = puntos fijos (12, 20) o 'R2' (2 veces el riesgo). Stop detras de la mecha. Tope 15 min (sale al ultimo)."""
    alto = seg["alto"].to_numpy(); bajo = seg["bajo"].to_numpy(); ult = seg["ultimo"].to_numpy(); n = len(ult); res = []
    for (i, lado, ent, stop, L, pen, dz) in g:
        riesgo = (ent - stop) * lado; G = 2 * riesgo if objetivo == "R2" else float(objetivo); j1 = min(n, i + 901)
        h = alto[i + 1:j1]; b = bajo[i + 1:j1]
        if lado > 0: tg = np.flatnonzero(h >= ent + G); tp = np.flatnonzero(b <= stop)
        else: tg = np.flatnonzero(b <= ent - G); tp = np.flatnonzero(h >= stop)
        a = tg[0] if len(tg) else 10**9; c = tp[0] if len(tp) else 10**9
        if a < c: pts = G
        elif c < a: pts = -riesgo
        elif a == c == 10**9: pts = (ult[j1 - 1] - ent) * lado
        else: pts = -riesgo                                              # las dos en el mismo segundo: se cuenta como perdida
        res.append((pts - COSTO, riesgo, 1 if pts > 0 else 0))
    return res


def linea(nombre, R):
    if not R: return "%-38s sin gatillos" % nombre
    p = np.array([x[0] for x in R]); r = np.array([x[1] for x in R]); g = np.array([x[2] for x in R])
    return "%-38s n %4d | gana %4.1f %% | neto %+5.2f pts/op (total %+7.1f) | riesgo mediano %.1f pts" % (nombre, len(p), 100 * g.mean(), p.mean(), p.sum(), np.median(r))


if __name__ == "__main__":
    T = B.todas_las_sesiones(); F, _ = V.fotos("NQ")
    dias = [s for s in sorted(T) if V.rayas_por_segundo(T[s], F) is not None and V.rayas_por_segundo(T[s], F)["d0"].notna().sum() >= 600]
    print("dias con verdes reconstruidas:", " ".join(d[5:] for d in dias)); print("costo por operacion: %.2f pts. Objetivo con stop detras de la mecha.\n" % COSTO)
    for variante, con_delta in VARIANTES:
        nombre = "%s%s" % (variante, " + delta 10 s a favor" if con_delta else "")
        for objetivo in (12, 20, "R2"):
            real = []; plc = []; real_sin = []; por_dia = {}
            for s in dias:
                seg = T[s]; L2 = V.rayas_por_segundo(seg, F)
                for col in ("d0", "d1"):
                    Lv = L2[col].to_numpy(); K = np.round(Lv / 5.0) * 5.0
                    for k in np.unique(K[~np.isnan(K)]):
                        Lt = np.where(K == k, Lv, np.nan)
                        r = resultado(seg, gatillos(seg, Lt, variante, con_delta), objetivo); real += r; por_dia[s] = por_dia.get(s, []) + r
                        if s != "2026-09-17": real_sin += r
                        for dl in (12.5, -12.5, 7.0, -7.0): plc += resultado(seg, gatillos(seg, Lt + dl, variante, con_delta), objetivo)
            print("[%s | objetivo %s]" % (nombre, objetivo)); print("   " + linea("VERDES (7 dias)", real)); print("   " + linea("VERDES sin el 17-09", real_sin)); print("   " + linea("PLACEBO (rayas corridas)", plc))
            if objetivo == 12: print("   por dia: " + " | ".join("%s %+.1f (%d)" % (d[5:], sum(x[0] for x in v), len(v)) for d, v in sorted(por_dia.items())))
    # muestra grande: la grilla de strikes de 25 pts en todas las ruedas con cinta (33), mismo gatillo
    print("\nGRILLA DE STRIKES DE 25 PUNTOS, 33 ruedas (misma regla; placebo = grilla corrida 12,5):")
    T2 = dict(T); T2.update(B.reserva()); CORR = {"2026-09-16": -4.2, "2026-09-17": -4.2}
    for variante, con_delta in VARIANTES:
        G1 = []; G0 = []
        for s, seg in sorted(T2.items()):
            if s == "2026-09-15": continue
            lo = np.floor(seg["bajo"].min() / 25) * 25; hi = np.ceil(seg["alto"].max() / 25) * 25
            for K in np.arange(lo, hi + 25, 25):
                base = np.full(len(seg), K + CORR.get(s, 0.0))
                G1 += resultado(seg, gatillos(seg, base, variante, con_delta), 12); G0 += resultado(seg, gatillos(seg, base + 12.5, variante, con_delta), 12)
        print("[%s%s | objetivo 12]" % (variante, " + delta" if con_delta else "")); print("   " + linea("STRIKES", G1)); print("   " + linea("PLACEBO", G0))
