# -*- coding: utf-8 -*-
"""RONDA 26, chequeo MECANICO del codigo (no mira desenlaces ni clases): sobre las cintas de hoy 27-11 (ES Z6 y MES), con X = 10,25.
  1. referencia independiente: el parseo de test_ola.referencia (csv, datetime.fromisoformat, float) con test_ola.Maquina, contra
     r26_comun.procesar (pandas + MaquinaI): mismos pivotes (hora, precio, tipo, confirmacion, cum) en ola y marea;
  2. cum de cada pivote: suma corrida del motor = cumsum de numpy;
  3. los numeros de la pierna de la marea de hoy que ya estan verificados contra las cintas (pedido del 27-11): arranca en el minimo
     7.222,75 (15:05:21 UTC); delta ES -2.970 = 1-9 +2.355, 10-19 -4.234, 20-49 -1.058, 50+ -33; MES +6.441 micro (+644 eq. ES); desde
     las 15:47 UTC ES -4.537 y MES -122 eq. ES; el delta de la pierna de ES cruza a negativo a las 16:11 UTC; correlacion por minuto 0,61.
Uso: python -I r26_00_chequeo.py"""
import csv
import os
import sys
from datetime import datetime, timezone

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import r26_comun as C

C.prioridad_baja()
X = 10.25
UTC = timezone.utc


def ms_de(dt):
    return int(round(dt.replace(tzinfo=UTC).timestamp() * 1000))


def referencia(ruta, w0, w1):
    m = {"X": C.T.Maquina(X), "4X": C.T.Maquina(4 * X)}
    cum = 0.0
    with open(ruta, newline="") as fh:
        r = csv.reader(fh)
        next(r)
        for x in r:
            t = datetime.fromisoformat(x[0])
            ms = ms_de(t)
            if ms < w0 or ms >= w1:
                continue
            a, b = float(x[1]), float(x[2])
            cum += float(x[3]) * int(x[4])
            for q in m.values():
                q.paso(ms, min(a, b), max(a, b), cum)
    return m


def main():
    w0, _ = C.ventana(C.HOY)
    corte = C.ms_ny(C.HOY, 11, 48)
    for inst in ("ES", "MES"):
        ruta = C.archivo(inst, C.HOY)
        C.frenar_si_falta_ram()
        res = C.procesar(inst, C.HOY, X, ruta, hasta_ms=corte)
        ref = referencia(ruta, w0, corte)
        for e in ("X", "4X"):
            p = res["piv"][e]
            mio = list(zip(p["t_ext"], p["precio"], np.where(p["tipo"] == 1, "max", "min"), p["t_conf"], p["cum"]))
            otro = ref[e].pivotes
            igual = len(mio) == len(otro) and all((int(a[0]), float(a[1]), a[2], int(a[3]), float(a[4])) == (b[0], b[1], b[2], b[3], b[4])
                                                  for a, b in zip(mio, otro))
            print("%s %s: pivotes %d (referencia independiente %d) -> %s; cum del motor = cumsum numpy: %s; verificacion interna: %s" %
                  (inst, e, len(mio), len(otro), "IDENTICOS" if igual else "DISTINTOS", bool(np.all(p["cum"].to_numpy() == p["cum_np"].to_numpy())),
                   res["meta"]["verificacion"][e]))
        res_full = C.procesar(inst, C.HOY, X, ruta)       # sin corte: hasta el final del archivo
        p4 = res_full["piv"]["4X"]
        k = np.where((p4["tipo"] == -1) & (np.abs(p4["t_ext"] - C.ms_ny(C.HOY, 10, 5)) <= 180000))[0]
        print("%s marea: minimos confirmados cerca de las 15:05 UTC: %s" % (inst, [(C.txt_ny(int(p4["t_ext"].iloc[j])), float(p4["precio"].iloc[j]))
                                                                             for j in k]))
        fin = res_full["piv"]["4X_final"]
        print("%s marea al final del archivo: dir %+d, extremo %.2f, pivotes %d" % (inst, fin["dir"], fin["ext"], len(p4)))
        # delta desde el minimo de 7.222,75 (o el minimo del MES en ese momento) hasta la ultima orden del archivo, por tamano
        d, _, _ = C.leer(ruta)
        w0_, w1_ = C.ventana(C.HOY)
        d = d[(d["ms"] >= w0_) & (d["ms"] < w1_)].reset_index(drop=True)
        if not len(k):
            print("%s: no hay minimo de la marea cerca de las 15:05 UTC" % inst)
            continue
        i0 = int(p4["i_ext"].iloc[k[0]])           # la orden que marco el minimo (indice en la misma ventana)
        sv = (d["vol"] * d["lado"]).to_numpy()
        vol = d["vol"].to_numpy()
        tramo = slice(i0 + 1, len(d))
        tot = sv[tramo].sum()
        bk = np.digitize(vol[tramo], C.BORDES[inst])
        partes = {C.ETIQ[inst][j]: float(sv[tramo][bk == j].sum()) for j in range(len(C.ETIQ[inst]))}
        print("%s: orden del minimo 15:05:21 UTC = fila %d, precio min %.2f; delta desde ahi (sin contarla) hasta el final (%s UTC): %+.0f%s" %
              (inst, i0, min(d["primero"][i0], d["ultimo"][i0]), datetime.fromtimestamp(d["ms"].iloc[-1] / 1000, UTC).strftime("%H:%M:%S"), tot,
               " (= %+.0f eq. ES)" % (tot / 10) if inst == "MES" else ""))
        print("   por tamano: " + ", ".join("%s %+.0f" % (k_, v) for k_, v in partes.items()))
        if inst == "ES":
            print("   ES 1-9 = %+.0f" % (partes["1"] + partes["2-4"] + partes["5-9"]))
            hasta = (d["ms"] < corte).to_numpy()
            sel = np.zeros(len(d), bool)
            sel[i0 + 1:] = True
            sel &= hasta
            bk2 = np.digitize(vol[sel], C.BORDES[inst])
            p2 = {C.ETIQ[inst][j]: float(sv[sel][bk2 == j].sum()) for j in range(len(C.ETIQ[inst]))}
            print("   hasta las 16:48:00 UTC (el corte del MES): %+.0f; 1-9 %+.0f, 10-19 %+.0f, 20-49 %+.0f, 50+ %+.0f; desde las 15:47 %+.0f" %
                  (sv[sel].sum(), p2["1"] + p2["2-4"] + p2["5-9"], p2["10-19"], p2["20-49"], p2["50+"],
                   sv[sel & (d["ms"] >= C.ms_ny(C.HOY, 10, 47)).to_numpy()].sum()))
            cl = np.cumsum(sv[sel])
            msl = d["ms"].to_numpy()[sel]
            pr = np.where((cl[1:] < 0) & (cl[:-1] >= 0))[0]
            print("   primeras veces que el delta de la pierna cruzo a negativo: %s" % [datetime.fromtimestamp(msl[j + 1] / 1000, UTC).strftime("%H:%M:%S")
                                                                                   for j in pr[:3]])
        t47 = C.ms_ny(C.HOY, 10, 47)
        desde47 = sv[(d["ms"] >= t47).to_numpy()].sum()
        print("   desde las 15:47 UTC: %+.0f%s" % (desde47, " (= %+.0f eq. ES)" % (desde47 / 10) if inst == "MES" else ""))
        if inst == "ES":
            cum_leg = np.cumsum(sv[tramo])
            ms_leg = d["ms"].to_numpy()[tramo]
            neg = cum_leg < 0
            cambios = np.where(neg[1:] & ~neg[:-1])[0]
            print("   delta de la pierna de ES: ultimas veces que cruzo a negativo: %s" % [datetime.fromtimestamp(ms_leg[j + 1] / 1000, UTC).strftime("%H:%M:%S")
                                                                                       for j in cambios[-3:]])
        if inst == "ES":
            es_min = (d["ms"].to_numpy(), sv)
        else:
            me_min = (d["ms"].to_numpy(), sv)
    # correlacion por minuto en la pierna (15:05 a 16:48 UTC)
    a, b = C.ms_ny(C.HOY, 10, 5), C.ms_ny(C.HOY, 11, 48)
    edges = np.arange(a, b + 1, 60000)
    he = np.histogram(es_min[0], bins=edges, weights=es_min[1])[0]
    hm = np.histogram(me_min[0], bins=edges, weights=me_min[1])[0] / 10.0
    print("correlacion por minuto ES contra MES/10, 15:05-16:48 UTC: %.2f (%d minutos)" % (np.corrcoef(he, hm)[0, 1], len(he)))


if __name__ == "__main__":
    main()
