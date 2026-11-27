# -*- coding: utf-8 -*-
"""RONDA 26, paso 2: P1, LA MECANICA (descriptiva; PREREGISTRO seccion 5). Con la MISMA X (la del ES) en los dos instrumentos:
  a. minutos RTH con pierna en curso en los dos y misma direccion: cuantas veces discrepa el color (ES con / MES sin y al reves), por hora NY;
     y cuantos minutos los dos zigzags van en direccion distinta (eso es mercado, no parametro: la X es la misma);
  b. piernas confirmadas del ES que terminan en la rueda, emparejadas con la del MES: color final de cada una, por hora del final;
  c. en las piernas que discrepan contra las que coinciden: delta por tamano de la orden agresora (ES: 1, 2-4, 5-9, 10-19, 20-49, 50+;
     MES en su tamano y en equivalente ES = /10), con signo de la pierna (+ = a favor), por pierna y cada 10 pts de recorrido;
  d. contexto: correlacion minuto a minuto del delta ES contra MES/10 en la rueda; y la 'prima' del MES (punta media MES - ES, en ticks, con
     el signo de la pierna) por clase, como lectura de quien empuja a quien (descriptivo: la hipotesis del arbitraje NO se prueba aca).
Uso: python -I r26_02_p1.py <tanda>      (lee datos\\ronda26\\pares.json: los dias de la tanda que alinean)"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import pandas as pd
import r26_comun as C

C.prioridad_baja()
HORAS = [(570, 600, "09:30-10"), (600, 660, "10-11"), (660, 720, "11-12"), (720, 780, "12-13"), (780, 840, "13-14"), (840, 900, "14-15"),
         (900, 960, "15-16")]
CLASES = ["CC", "SC", "CS", "SS"]
NOMBRE = {"CC": "ES con / MES con", "SC": "ES sin / MES con", "CS": "ES con / MES sin", "SS": "ES sin / MES sin"}


def dias_de(tanda):
    p = json.load(open(os.path.join(C.DATOS, "pares.json")))
    ok = sorted(d for d, v in p.items() if v["tanda"] == tanda and v["alinea"])
    fuera = sorted(d for d, v in p.items() if v["tanda"] == tanda and not v["alinea"])
    return ok, fuera, p


def pct(a, b):
    return 100.0 * a / b if b else float("nan")


def hora_de(mins):
    for a, b, et in HORAS:
        if a <= mins < b:
            return et
    return "otra"


def main():
    tanda = sys.argv[1]
    dias, fuera, pares = dias_de(tanda)
    print("RONDA 26 - P1 (mecanica), %s: %d ruedas %s" % (tanda, len(dias), " ".join(dias)))
    if fuera:
        print("AFUERA (no alinean o falta cinta): " + ", ".join("%s (%s)" % (d, pares[d].get("motivo", "mediana |ES-MES| %.2f, p95 %.2f" %
                                                                             (pares[d].get("mediana_abs", np.nan), pares[d].get("p95_abs", np.nan))))
                                                         for d in fuera))
    for esc in ("X", "4X"):
        nombre = "OLA (X)" if esc == "X" else "MAREA (4X)"
        M = pd.concat([C.armar_minutos(d, esc, pares[d]["X"]) for d in dias], ignore_index=True)
        P = pd.concat([C.armar_piernas(d, esc) for d in dias], ignore_index=True)
        print("\n" + "=" * 110 + "\n%s\n" % nombre + "=" * 110)
        # a. minutos
        hay = (M["e_dir"] != 0) & (M["m_dir"] != 0)
        el = M[M["elegible"]]
        print("a. MINUTOS RTH: %d cierres con pierna en los dos; direccion distinta en %d (%.1f %%) con la misma X; elegibles (misma direccion) %d;"
              " de esos, con el mismo origen (+-120 s, +-2 ticks) %.1f %%" % (hay.sum(), (hay & ~M["elegible"]).sum(),
                                                                            pct((hay & ~M["elegible"]).sum(), hay.sum()), len(el),
                                                                            pct(el["mismo_origen"].sum(), len(el))))
        dis = el["e_con"] != el["m_con"]
        print("   color distinto en %d de %d minutos (%.1f %%): ES sin / MES con %.1f %%, ES con / MES sin %.1f %% | ES con en %.1f %%, MES con en %.1f %%"
              % (dis.sum(), len(el), pct(dis.sum(), len(el)), pct((el["clase"] == "SC").sum(), len(el)), pct((el["clase"] == "CS").sum(), len(el)),
                 pct(el["e_con"].sum(), len(el)), pct(el["m_con"].sum(), len(el))))
        print("   por rueda: " + " | ".join("%s %.0f %% (n %d)" % (d[5:], pct((g["e_con"] != g["m_con"]).sum(), len(g)), len(g))
                                            for d, g in el.groupby("dia")))
        el = el.assign(hora=el["min_ny"].map(hora_de))
        print("   por hora NY:  hora       minutos  distinto   SC     CS    (solo con el mismo origen: distinto)")
        for _, _, et in HORAS:
            g = el[el["hora"] == et]
            g2 = g[g["mismo_origen"]]
            print("                 %-9s  %7d  %6.1f %%  %5.1f  %5.1f   %6.1f %% (n %d)" % (et, len(g), pct((g["e_con"] != g["m_con"]).sum(), len(g)),
                                                                                 pct((g["clase"] == "SC").sum(), len(g)), pct((g["clase"] == "CS").sum(), len(g)),
                                                                                 pct((g2["e_con"] != g2["m_con"]).sum(), len(g2)), len(g2)))
        # d. correlacion minuto a minuto del delta (dentro de la rueda)
        cs = []
        for d, g in M.groupby("dia"):
            E, Mm = C.cargar("ES", d), C.cargar("MES", d)
            n = min(len(E["fotos"]), len(Mm["fotos"]))
            de = np.diff(E["fotos"]["cum"].to_numpy()[:n])
            dm = np.diff(Mm["fotos"]["cum"].to_numpy()[:n]) / 10.0
            cs.append((d, float(np.corrcoef(de, dm)[0, 1]), float(de.sum()), float(dm.sum())))
        if esc == "X":
            print("d. delta de cada minuto RTH, ES contra MES/10 (correlacion; suma de la rueda ES y MES en eq. ES):")
            print("   " + " | ".join("%s r %.2f (ES %+.0f, MES %+.0f)" % (d[5:], r, a, b) for d, r, a, b in cs))
        # b. piernas
        emp = P[P["emparejada"]]
        print("\nb. PIERNAS del ES que terminan en la rueda: %d; emparejadas con una pierna del MES: %d (%.1f %%)" % (len(P), len(emp), pct(len(emp), len(P))))
        if not len(emp):
            continue
        print("   color final: " + ", ".join("%s %d (%.1f %%)" % (NOMBRE[c], (emp["clase"] == c).sum(), pct((emp["clase"] == c).sum(), len(emp))) for c in CLASES))
        print("   ES con su delta a favor %.1f %%; MES %.1f %%; ES + MES/10 %.1f %%" % (pct(emp["e_con"].sum(), len(emp)), pct(emp["m_con"].sum(), len(emp)),
                                                                           pct((np.sign(emp["e_delta"] + emp["m_delta"] / 10) == emp["dir"]).sum(), len(emp))))
        emp = emp.assign(hora=emp["min_fin"].map(hora_de))
        print("   por hora del final: " + " | ".join("%s %.0f %% distinto (n %d)" % (et, pct((g["clase"].isin(["SC", "CS"])).sum(), len(g)), len(g))
                                                     for et in [h[2] for h in HORAS] for g in [emp[emp["hora"] == et]] if len(g)))
        print("   desfase de los extremos MES - ES (s): origen mediana %+.1f (p90 |.| %.1f), final mediana %+.1f (p90 |.| %.1f)" %
              (emp["m_dt0_s"].median(), emp["m_dt0_s"].abs().quantile(0.9), emp["m_dt1_s"].median(), emp["m_dt1_s"].abs().quantile(0.9)))
        # c. anatomia por tamano
        print("\nc. QUIEN HACE LA DIFERENCIA: delta por tamano de la orden agresora con el signo de la pierna (+ = a favor), media por pierna")
        cab = "   clase               n    recorr " + "".join("%9s" % ("ES " + k) for k in C.ETIQ["ES"]) + "  ES tot |" + \
              "".join("%9s" % ("M " + k) for k in C.ETIQ["MES"]) + "  MES/10 | ES+M/10"
        print(cab)
        for c in CLASES:
            g = emp[emp["clase"] == c]
            if not len(g):
                continue
            s = g["dir"].to_numpy()
            fila = "   %-17s %4d  %6.1f " % (NOMBRE[c], len(g), g["recorrido"].mean())
            fila += "".join("%+9.0f" % np.mean(s * g["e_d_" + k]) for k in C.ETIQ["ES"]) + " %+7.0f |" % np.mean(s * g["e_delta"])
            fila += "".join("%+9.1f" % np.mean(s * g["m_d_" + k] / 10.0) for k in C.ETIQ["MES"]) + " %+7.1f | %+7.1f" % (
                np.mean(s * g["m_delta"] / 10.0), np.mean(s * (g["e_delta"] + g["m_delta"] / 10.0)))
            print(fila)
        print("   mismo cuadro cada 10 pts de recorrido (suma de la clase / recorrido total x 10):")
        for c in CLASES:
            g = emp[emp["clase"] == c]
            if not len(g):
                continue
            s = g["dir"].to_numpy()
            rec = g["recorrido"].sum() / 10.0
            fila = "   %-17s %4d  %6.1f " % (NOMBRE[c], len(g), g["recorrido"].mean())
            fila += "".join("%+9.0f" % (np.sum(s * g["e_d_" + k]) / rec) for k in C.ETIQ["ES"]) + " %+7.0f |" % (np.sum(s * g["e_delta"]) / rec)
            fila += "".join("%+9.1f" % (np.sum(s * g["m_d_" + k] / 10.0) / rec) for k in C.ETIQ["MES"]) + " %+7.1f | %+7.1f" % (
                np.sum(s * g["m_delta"] / 10.0) / rec, np.sum(s * (g["e_delta"] + g["m_delta"] / 10.0)) / rec)
            print(fila)
        print("   piernas con cada tramo A FAVOR (%):")
        for c in CLASES:
            g = emp[emp["clase"] == c]
            if not len(g):
                continue
            s = g["dir"].to_numpy()
            fila = "   %-17s %4d         " % (NOMBRE[c], len(g))
            fila += "".join("%9.0f" % pct((s * g["e_d_" + k] > 0).sum(), len(g)) for k in C.ETIQ["ES"]) + " %7.0f |" % pct((s * g["e_delta"] > 0).sum(), len(g))
            fila += "".join("%9.0f" % pct((s * g["m_d_" + k] > 0).sum(), len(g)) for k in C.ETIQ["MES"]) + " %7.0f | %7.0f" % (
                pct((s * g["m_delta"] > 0).sum(), len(g)), pct((s * (g["e_delta"] + g["m_delta"] / 10.0) > 0).sum(), len(g)))
            print(fila)
        cc, sc = emp[emp["clase"] == "CC"], emp[emp["clase"] == "SC"]
        if len(cc) and len(sc):
            print("   diferencia SC - CC por pierna (que tramo se da vuelta cuando el ES queda sin agresores y el MES no):")
            sc_s, cc_s = sc["dir"].to_numpy(), cc["dir"].to_numpy()
            print("   " + " | ".join("ES %s %+.0f" % (k, np.mean(sc_s * sc["e_d_" + k]) - np.mean(cc_s * cc["e_d_" + k])) for k in C.ETIQ["ES"]))
            print("   " + " | ".join("MES %s %+.1f" % (k, np.mean(sc_s * sc["m_d_" + k] / 10) - np.mean(cc_s * cc["m_d_" + k] / 10)) for k in C.ETIQ["MES"]))
        # d. prima del MES
        el2 = M[M["elegible"] & M["prima_ticks"].notna()]
        print("\nd. PRIMA del MES al cierre del minuto (punta media MES - ES, ticks, con el signo de la pierna): " +
              " | ".join("%s media %+.3f mediana %+.2f (n %d)" % (c, el2.loc[el2["clase"] == c, "prima_ticks"].mean(),
                                                                 el2.loc[el2["clase"] == c, "prima_ticks"].median(), (el2["clase"] == c).sum())
                         for c in CLASES))


if __name__ == "__main__":
    main()
