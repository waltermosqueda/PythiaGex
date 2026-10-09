# -*- coding: utf-8 -*-
"""test_juez20.py — casos sinteticos hechos a mano para juez20.py (09-10-2026). Correr: python -I test_juez20.py
Cada caso dice que esperaba y que dio. Termina con 'TODOS LOS CASOS OK' o con el primer assert que falla."""
import ctypes
import math
import os
import sys

import numpy as np
import pandas as pd

try:
    ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)
except Exception:
    pass

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import juez20 as J20  # noqa: E402

T0 = pd.Timestamp("2026-10-01 00:00")
L = 20000.0


def velas(ohlc, t0=T0):
    return pd.DataFrame([{"t": t0 + pd.Timedelta(minutes=k), "o": o, "h": h, "l": l, "c": c} for k, (o, h, l, c) in enumerate(ohlc)])


def rayas_fijas(t0, n, precios, desde=-1):
    return {t0 + pd.Timedelta(minutes=k): list(precios) for k in range(desde, n)}


def subida(desde, hasta, paso=4.0):
    out = []; p = desde
    while p + paso <= hasta + 1e-9:
        out.append((p, p + paso + 0.5, p - 0.5, p + paso)); p += paso
    return out


def bajada(desde, hasta, paso=5.0):
    out = []; p = desde
    while p - paso >= hasta - 1e-9:
        out.append((p, p + 0.5, p - paso - 0.5, p - paso)); p -= paso
    return out


def lateral(centro, n, amp=3.0):
    return [(centro, centro + amp, centro - amp, centro + (0.5 if k % 2 else -0.5)) for k in range(n)]


def muestra(nombre, evs):
    print("  %s" % nombre)
    for e in evs:
        print("     %s %-5s L=%.2f -> %-10s %-40s acierto20 %-5s falsa %-5s dist_rot %s prec %+.2f falso_romp %-5s rec %s" % (
            e["t_llegada"].strftime("%H:%M"), e["tipo"], e["raya"], e["resultado"], e["motivo"], e["acierto20"], e["falsa"],
            ("%.2f" % e["dist_rotura"]) if e["dist_rotura"] is not None else "-", e["precision"], e["falso_rompimiento"],
            ("%.2f" % e["recorrido"]) if "recorrido" in e else "-"))


def correr(ohlc, precios=(L,), opciones=None, t0=T0):
    V = velas(ohlc, t0)
    R = rayas_fijas(t0, len(V), precios)
    return J20.evaluar(V, R, opciones=opciones, n_azar=0, corrimientos=(), n_boot=0)


def de(res, raya):
    return [e for e in res["eventos"] if abs(e["raya"] - raya) < 1e-6]


# ---------------- (a) los cuatro casos pedidos -----------------------------------------------------------------------------------
def caso1():
    print("\nCASO 1: techo que aguanta y baja 25 -> ACIERTO20 (precision 0, recorrido 25)")
    ohlc = subida(19950, 19990) + [(19990, 20000, 19989, 19993)] + bajada(19993, 19978, 5) + [(19978, 19978.5, 19975, 19977)]
    ohlc += [(19977 + (k % 3), 19982 + (k % 3), 19976.0, 19979) for k in range(30)]
    res = correr(ohlc)
    e = de(res, L); muestra("tolerante", e)
    assert len(e) == 1 and e[0]["tipo"] == "techo" and e[0]["acierto20"] and not e[0]["falsa"], e
    assert abs(e[0]["precision"]) < 1e-9 and not e[0]["falso_rompimiento"]
    assert abs(e[0]["recorrido"] - 25.0) < 1e-9, e[0]["recorrido"]
    m = res["metricas"]
    assert m["aciertos"] == 1 and m["falsas"] == 0 and m["pct_acierto20"] == 100.0 and m["expectativa_pts"] == 20.0
    print("  OK")


def caso2():
    print("\nCASO 2: techo que aguanta pero baja 12 y despues rompe (cierre 20007, cuerpo 6) -> ENTRADA FALSA, dist_rotura 7")
    ohlc = subida(19950, 19990) + [(19990, 20000, 19989, 19994)] + bajada(19994, 19989, 5) + [(19989, 19990, 19988, 19989)]
    ohlc += [(19989, 19995, 19988.5, 19994), (19994, 19999, 19993, 19998), (19998, 19999.5, 19996, 19999), (20001, 20008, 20000.5, 20007)]
    ohlc += subida(20007, 20031)
    ohlc += [(20031, 20035, 20028, 20031) for _ in range(20)]
    res = correr(ohlc)
    e = de(res, L); muestra("tolerante", e)
    assert len(e) >= 1 and e[0]["tipo"] == "techo" and e[0]["falsa"] and not e[0]["acierto20"], e
    assert abs(e[0]["dist_rotura"] - 7.0) < 1e-9 and "cierre mas alla de +5" in e[0]["motivo"]
    mfe_antes = min(x[2] for x in ohlc[11:16])
    assert L - mfe_antes == 12.0, L - mfe_antes
    m = res["metricas"]
    assert m["falsas"] == 1 and m["aciertos"] == 0 and m["expectativa_pts"] == -7.0 and m["expectativa_pts_fija"] == -5.0
    print("  OK (llego a 12 a favor y despues se acepto arriba)")


def caso3():
    print("\nCASO 3: pinchazo de mecha +6 (cierre adentro) y baja 30 -> ACIERTO20 con FALSO ROMPIMIENTO (precision +6, recorrido 30)")
    ohlc = subida(19950, 19990) + [(19992, 20006, 19991, 19996)] + bajada(19996, 19971, 5) + [(19971, 19972, 19970, 19971)]
    ohlc += [(19972 + (k % 3), 19978 + (k % 3), 19971.0, 19975) for k in range(30)]
    for modo, esperado in (("tolerante", "sostenido"), ("estricto", "rota")):
        res = correr(ohlc, opciones={"modo": modo})
        e = de(res, L); muestra(modo, e)
        assert len(e) == 1 and e[0]["resultado"] == esperado, (modo, e)
        if modo == "tolerante":
            assert e[0]["acierto20"] and e[0]["falso_rompimiento"] and abs(e[0]["precision"] - 6.0) < 1e-9
            assert abs(e[0]["recorrido"] - 30.0) < 1e-9
            assert res["metricas"]["falsos_rompimientos"] == 1
    print("  OK (y en ESTRICTO, fuera de lo pre-registrado, la mecha +6 la rompe: muestra que la tolerancia esta activa)")


def caso4():
    print("\nCASO 4: aceptacion con 3 cierres afuera (20001; 20002; 20001,5, cuerpos chicos) -> ENTRADA FALSA aunque despues baje 30")
    ohlc = subida(19950, 19990) + [(19990, 20000, 19989, 19995), (19995, 20002, 19994, 20001), (20001, 20003, 19999, 20002),
                                   (20002, 20003, 20000, 20001.5)]
    ohlc += bajada(20001.5, 19971.5, 5) + [(19971.5, 19972, 19970, 19971) for _ in range(20)]
    res = correr(ohlc)
    e = de(res, L); muestra("tolerante", e)
    assert e[0]["falsa"] and e[0]["motivo"] == "3 cierres seguidos mas alla" and abs(e[0]["dist_rotura"] - 1.5) < 1e-9, e
    assert e[0]["t_decision"] == T0 + pd.Timedelta(minutes=len(subida(19950, 19990)) + 3)
    res_mt = correr(ohlc, opciones={"modo": "muy_tolerante"})
    e2 = de(res_mt, L); muestra("muy_tolerante (sensibilidad)", e2)
    assert e2[0]["acierto20"], e2
    print("  OK (la bajada de 30 posterior no la salva: se rompio antes de los 20; en MUY TOLERANTE seria acierto)")


# ---------------- extras ------------------------------------------------------------------------------------------------------------
def caso5_piso():
    print("\nCASO 5: PISO que aguanta y sube 22 -> ACIERTO20 (simetria)")
    ohlc = bajada(20050, 20005, 5) + [(20005, 20006, 19999, 20001)] + subida(20001, 20021, 4) + [(20021, 20022.5, 20020, 20021)]
    ohlc += [(20018, 20021, 20015, 20019) for _ in range(20)]
    res = correr(ohlc)
    e = de(res, L); muestra("tolerante", e)
    assert len(e) == 1 and e[0]["tipo"] == "piso" and e[0]["acierto20"] and abs(e[0]["precision"] - 1.0) < 1e-9
    assert abs(e[0]["recorrido"] - 22.5) < 1e-9, e[0]["recorrido"]
    print("  OK")


def caso6_indefinida_y_censurada():
    print("\nCASO 6: toca y queda lateral 130 min (ni 20 ni rotura) -> INDEFINIDA; toca y se terminan los datos -> CENSURADA")
    ohlc = subida(19950, 19990) + [(19990, 20000, 19989, 19993)] + [(19992, 19998, 19985, 19993 + (k % 2)) for k in range(130)]
    res = correr(ohlc)
    e = de(res, L); muestra("indefinida", e[:1])
    assert e[0]["resultado"] == "indefinida" and not e[0]["censurada"] and "120 min" in e[0]["motivo"]
    m = res["metricas"]
    assert m["indefinidas"] >= 1 and m["pct_acierto20"] == 0.0 and math.isnan(m["pct_acierto20_decididas"])
    ohlc2 = subida(19950, 19990) + [(19990, 20000, 19989, 19993)] + [(19992, 19998, 19985, 19993) for _ in range(30)]
    res2 = correr(ohlc2)
    e2 = de(res2, L); muestra("censurada", e2)
    assert e2[0]["censurada"] and res2["metricas"]["base"] == 0
    print("  OK")


def caso7_metricas_combinadas():
    print("\nCASO 7: casos 1 + 2 + 3 en una misma noche (separados por huecos > 30 min) -> 2 aciertos, 1 falsa (7 pts)")
    oh1 = subida(19950, 19990) + [(19990, 20000, 19989, 19993)] + bajada(19993, 19978, 5) + [(19978, 19978.5, 19975, 19977)]
    oh1 += [(19977 + (k % 3), 19982 + (k % 3), 19976.0, 19979) for k in range(30)]
    oh2 = subida(19950, 19990) + [(19990, 20000, 19989, 19994)] + bajada(19994, 19989, 5) + [(19989, 19990, 19988, 19989)]
    oh2 += [(19989, 19995, 19988.5, 19994), (19994, 19999, 19993, 19998), (19998, 19999.5, 19996, 19999), (20001, 20008, 20000.5, 20007)]
    oh2 += subida(20007, 20031) + [(20031, 20035, 20028, 20031) for _ in range(20)]
    oh3 = subida(19950, 19990) + [(19992, 20006, 19991, 19996)] + bajada(19996, 19971, 5) + [(19971, 19972, 19970, 19971)]
    oh3 += [(19972 + (k % 3), 19978 + (k % 3), 19971.0, 19975) for k in range(30)]
    V = pd.concat([velas(oh1, T0), velas(oh2, T0 + pd.Timedelta(hours=6)), velas(oh3, T0 + pd.Timedelta(hours=12))], ignore_index=True)
    R = {T0 + pd.Timedelta(minutes=k): [L] for k in range(-1, 15 * 60)}
    res = J20.evaluar(V, R, n_azar=0, corrimientos=(), n_boot=200)
    m = res["metricas"]
    print("  aciertos %d falsas %d base %d pct %.2f expectativa %.4f fija %.4f base %.4f por noche: %.1f aciertos %.1f falsas (noches %d)" % (
        m["aciertos"], m["falsas"], m["base"], m["pct_acierto20"], m["expectativa_pts"], m["expectativa_pts_fija"],
        m["expectativa_pts_base"], m["aciertos_por_noche"], m["falsas_por_noche"], m["n_noches_ventana"]))
    assert m["aciertos"] == 2 and m["falsas"] == 1 and m["base"] == 3
    assert abs(m["pct_acierto20"] - 200 / 3) < 1e-9 and abs(m["expectativa_pts"] - 11.0) < 1e-9
    assert abs(m["expectativa_pts_fija"] - 35 / 3) < 1e-9 and m["n_noches_ventana"] == 1
    assert m["aciertos_por_noche"] == 2 and m["falsas_por_noche"] == 1
    assert m["recorrido_mediano_acierto"] == 27.5 and m["precision_mediana_abs"] == 3.0
    ic = res["bootstrap_ic90"]["pct_acierto20"]
    assert abs(ic[0] - 200 / 3) < 1e-9 and abs(ic[1] - 200 / 3) < 1e-9, ic   # una sola noche: el bootstrap no se mueve
    print("  OK (expectativa = (20+20-7)/3 = 11,00; con -5 fijo = 11,67; IC90 con una noche = el punto)")


def caso8_densidad():
    print("\nCASO 8: RAYAS POR HORA: 2 rayas fijas 3 h -> 2/h; + una que salta 10 pts cada 10 min -> 2 + 6 = 8/h")
    ohlc = lateral(19000, 180)
    V = velas(ohlc)
    op0 = {"desfase_min": 0}                      # clave t = vigente en la vela t (asi los saltos caen alineados a la hora)
    R1 = rayas_fijas(T0, 180, [18000.0, 21000.0])
    m1 = J20.evaluar(V, R1, opciones=op0, n_azar=0, corrimientos=(), n_boot=0)["metricas"]
    minuto = lambda t: int((t - T0) / pd.Timedelta(minutes=1))
    R2 = {t: v + [19500.0 + 10 * (minuto(t) // 10)] for t, v in R1.items()}
    m2 = J20.evaluar(V, R2, opciones=op0, n_azar=0, corrimientos=(), n_boot=0)["metricas"]
    print("  fijas: %.2f/h (horas %d, simultaneas %.0f); con la saltarina: %.2f/h (mediana %.1f, p90 %.1f, simultaneas %.0f, pistas %d)" % (
        m1["rayas_por_hora"], m1["horas_contadas"], m1["rayas_simultaneas_mediana"], m2["rayas_por_hora"], m2["rayas_por_hora_mediana"],
        m2["rayas_por_hora_p90"], m2["rayas_simultaneas_mediana"], m2["pistas_ventana"]))
    assert m1["rayas_por_hora"] == 2.0 and m1["horas_contadas"] == 3 and m1["rayas_simultaneas_mediana"] == 2
    assert m2["rayas_por_hora"] == 8.0 and m2["rayas_simultaneas_mediana"] == 3 and m2["pistas_ventana"] == 20
    # ventana de solo 1 hora: cuenta solo esa hora
    m3 = J20.evaluar(V, R2, ventanas=[(T0 + pd.Timedelta(hours=1), T0 + pd.Timedelta(hours=2))], opciones=op0, n_azar=0,
                     corrimientos=(), n_boot=0)["metricas"]
    assert m3["horas_contadas"] == 1 and m3["rayas_por_hora"] == 8.0, (m3["horas_contadas"], m3["rayas_por_hora"])
    print("  OK")


def caso9_sin_mirar_adelante():
    print("\nCASO 9: la raya aparece en la clave 10 y la vela 10 la toca: con desfase 1 NO cuenta (recien vale en la 11); con 0 si")
    ohlc = subida(19950, 19990) + [(19990, 20000, 19989, 19993)] + bajada(19993, 19968, 5) + lateral(19970, 20)
    k_toque = len(subida(19950, 19990))
    R = {T0 + pd.Timedelta(minutes=k): ([L] if k >= k_toque else []) for k in range(len(ohlc))}
    V = velas(ohlc)
    r1 = J20.evaluar(V, R, n_azar=0, corrimientos=(), n_boot=0)
    r0 = J20.evaluar(V, R, opciones={"desfase_min": 0}, n_azar=0, corrimientos=(), n_boot=0)
    print("  desfase 1: %d llegadas; desfase 0: %d llegadas (%s)" % (len(r1["eventos"]), len(r0["eventos"]),
                                                                   [e["t_llegada"].strftime("%H:%M") for e in r0["eventos"]]))
    assert len(r1["eventos"]) == 0 and len(r0["eventos"]) == 1 and r0["eventos"][0]["acierto20"]
    print("  OK")


def caso10_forzada():
    print("\nCASO 10: evaluar_llegada (forzada) reproduce lo del juez en el caso 3")
    ohlc = subida(19950, 19990) + [(19992, 20006, 19991, 19996)] + bajada(19996, 19971, 5) + [(19971, 19972, 19970, 19971)]
    ohlc += [(19972 + (k % 3), 19978 + (k % 3), 19971.0, 19975) for k in range(30)]
    V = velas(ohlc)
    t = T0 + pd.Timedelta(minutes=len(subida(19950, 19990)))
    e = J20.evaluar_llegada(V, t, L, "techo")
    muestra("forzada", [e])
    assert e["acierto20"] and e["falso_rompimiento"] and abs(e["recorrido"] - 30.0) < 1e-9
    print("  OK")


def _zigzag(h, l, X):
    piv = []; tr = 0; hi, hik, lo, lok = h[0], 0, l[0], 0
    for k in range(1, len(h)):
        if tr == 0:
            if h[k] > hi: hi, hik = h[k], k
            if l[k] < lo: lo, lok = l[k], k
            if hi - lo >= X:
                if hik > lok: piv.append((lok, lo)); tr = 1
                else: piv.append((hik, hi)); tr = -1
        elif tr == 1:
            if h[k] > hi: hi, hik = h[k], k
            elif hi - l[k] >= X: piv.append((hik, hi)); tr = -1; lo, lok = l[k], k
        else:
            if l[k] < lo: lo, lok = l[k], k
            elif h[k] - lo >= X: piv.append((lok, lo)); tr = 1; hi, hik = h[k], k
    return piv


def caso11_placebo():
    print("\nCASO 11: PLACEBO. Paseo al azar, 12 noches x 300 min. (i) ORACULO (raya = la proxima punta de un zigzag de 30, MIRANDO"
          " ADELANTE) debe quedar arriba de todo el azar; (ii) rayas AL AZAR deben quedar en el medio.")
    rng = np.random.default_rng(7)
    filas = []; rayas_or = {}; rayas_az = {}
    for n in range(12):
        t0 = pd.Timestamp("2026-09-01") + pd.Timedelta(days=n)
        p = 20000.0 + rng.normal(0, 50)
        ohlc = []
        for k in range(300):
            sub = p + np.cumsum(rng.normal(0, 1.6, 4))
            o, c = p, float(np.round(sub[-1] * 4) / 4)
            h = float(np.ceil(max(o, sub.max()) * 4) / 4); l_ = float(np.floor(min(o, sub.min()) * 4) / 4)
            ohlc.append((o, h, l_, c)); p = c
        h = np.array([x[1] for x in ohlc]); l_ = np.array([x[2] for x in ohlc])
        piv = _zigzag(h, l_, 30.0)
        for k in range(300):
            t = t0 + pd.Timedelta(minutes=k)
            prox = [pp for kk, pp in piv if kk >= k]                   # MIRA ADELANTE a proposito
            rayas_or[t] = prox[:1]
            if k % 30 == 0:
                ref = ohlc[k - 1][3] if k else ohlc[0][0]              # sin mirar la vela k
                cur = [ref + rng.uniform(-40, 40) for _ in range(2)]
            rayas_az[t] = list(cur)
        filas.extend((t0 + pd.Timedelta(minutes=k), *x) for k, x in enumerate(ohlc))
    V = pd.DataFrame(filas, columns=["t", "o", "h", "l", "c"])
    out = {}
    for nombre, R in (("oraculo", rayas_or), ("azar", rayas_az)):
        res = J20.evaluar(V, R, opciones={"desfase_min": 0}, n_azar=200, n_boot=500)
        m = res["metricas"]; pl = res["placebo"]; az = pl["azar"]["pct_acierto20"]
        print("  %-8s llegadas %3d acierto20 %5.1f %% (IC90 %.1f-%.1f) | tasa base azar %.1f %% (p5 %.1f p95 %.1f) corridas %.1f %% | "
              "percentil %.1f p %.4f | expectativa %.2f (azar p50 %.2f) | rayas/h %.1f" % (
                  nombre, m["llegadas"], m["pct_acierto20"], *res["bootstrap_ic90"]["pct_acierto20"], pl["tasa_base_azar"], az["p5"],
                  az["p95"], pl["tasa_base_corridas"], az["percentil"], az["p_valor"], m["expectativa_pts"],
                  pl["azar"]["expectativa_pts"]["p50"], m["rayas_por_hora"]))
        print("           z %.2f p_normal %.2g" % (az["z"], az["p_normal"]))
        out[nombre] = az
    assert out["oraculo"]["percentil"] >= 99.0 and out["oraculo"]["p_valor"] < 0.01, out["oraculo"]
    assert out["oraculo"]["z"] > 3 and abs(out["azar"]["z"]) < 1.65, (out["oraculo"]["z"], out["azar"]["z"])
    assert 5.0 <= out["azar"]["percentil"] <= 95.0, out["azar"]
    print("  OK (el placebo distingue un oraculo y no premia rayas al azar)")


def caso12_holm_y_particion():
    print("\nCASO 12: Holm y particion por noches")
    h = J20.holm({"a": 0.01, "b": 0.04, "c": 0.03, "d": 0.005, "e": float("nan")})
    print("  " + ", ".join("%s p %.3f holm %.3f %s" % (k, v["p"], v["p_holm"], v["rechaza_05"]) for k, v in sorted(h.items())))
    assert abs(h["d"]["p_holm"] - 0.02) < 1e-12 and abs(h["a"]["p_holm"] - 0.03) < 1e-12
    assert abs(h["c"]["p_holm"] - 0.06) < 1e-12 and abs(h["b"]["p_holm"] - 0.06) < 1e-12
    assert h["d"]["rechaza_05"] and h["a"]["rechaza_05"] and not h["c"]["rechaza_05"] and not h["e"]["rechaza_05"]
    noches = ["2026-09-%02d" % d for d in range(1, 20)]
    mi, pr = J20.particion_noches(noches)
    print("  19 noches -> mirar %d (%s..%s), prueba %d (%s..%s)" % (len(mi), mi[0], mi[-1], len(pr), pr[0], pr[-1]))
    assert len(mi) == 12 and len(pr) == 7 and pr[0] == "2026-09-13"
    w = J20.ventanas_de_noches(["2026-10-09"])
    assert w == [(pd.Timestamp("2026-10-08 22:00"), pd.Timestamp("2026-10-09 13:30"))], w
    print("  OK")


if __name__ == "__main__":
    for f in (caso1, caso2, caso3, caso4, caso5_piso, caso6_indefinida_y_censurada, caso7_metricas_combinadas, caso8_densidad,
              caso9_sin_mirar_adelante, caso10_forzada, caso11_placebo, caso12_holm_y_particion):
        f()
    print("\nTODOS LOS CASOS OK")
