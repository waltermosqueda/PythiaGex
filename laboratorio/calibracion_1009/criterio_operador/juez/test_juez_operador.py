# -*- coding: utf-8 -*-
"""test_juez_operador.py — casos sinteticos hechos a mano para juez_operador.py. Correr: python -I test_juez_operador.py
Cada caso dice que esperaba y que dio. Termina con 'TODOS LOS CASOS OK' o con el primer assert que falla."""
import math
import os
import sys

import numpy as np
import pandas as pd

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import juez_operador as J  # noqa: E402

T0 = pd.Timestamp("2026-10-01 00:00")


def velas(ohlc, t0=T0):
    return pd.DataFrame([{"t": t0 + pd.Timedelta(minutes=k), "o": o, "h": h, "l": l, "c": c} for k, (o, h, l, c) in enumerate(ohlc)])


def rayas_fijas(n, precios, desde=-1):
    """las mismas rayas en todas las claves desde el minuto 'desde' (la clave -1 = vigente en la vela 0)."""
    return {T0 + pd.Timedelta(minutes=k): list(precios) for k in range(desde, n)}


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


def ev_de(res, raya, tipo=None):
    return [e for e in res["eventos"] if abs(e["raya"] - raya) < 1e-6 and (tipo is None or e["tipo"] == tipo)]


def muestra(nombre, evs):
    print("  %s" % nombre)
    for e in evs:
        print("     %s %-5s L=%.2f -> %-10s %-38s (min %d) prec %+.2f falso %-5s rec %s tramo %s opuesta %s llega %s" % (
            e["t_llegada"].strftime("%H:%M"), e["tipo"], e["raya"], e["resultado"], e["motivo"], e["min_a_decision"], e["precision"],
            e["falso_rompimiento"],
            ("%.2f" % e["recorrido"]) if "recorrido" in e else "-", ("%.2f" % e["recorrido_tramo"]) if "recorrido_tramo" in e else "-",
            e.get("opuesta"), e.get("llega_opuesta")))


def caso1_techo_perfecto():
    print("\nCASO 1: techo perfecto en la punta, aguanta y baja 60 (raya 20000; opuesta 19940)")
    sub = subida(19950, 19990)                                   # highs <= 19990.5: no tocan la banda
    punta = [(19990, 20000, 19989, 19993)]                        # la mecha llega EXACTO a la raya
    baj = bajada(19993, 19943, 5)                                 # baja de a 5
    baj.append((19943, 19943.5, 19940, 19941))                    # minimo 19940 = raya - 60
    lat = [(19941 + (k % 3), 19946 + (k % 3), 19941.0, 19943 + (k % 3)) for k in range(40)]  # nunca baja de 19941
    V = velas(sub + punta + baj + lat)
    R = rayas_fijas(len(V), [20000.0, 19940.0])
    for modo in ("tolerante", "estricto", "muy_tolerante"):
        res = J.juzgar(V, R, {"modo": modo})
        e = ev_de(res, 20000.0)
        muestra(modo, e)
        assert len(e) == 1, e
        e = e[0]
        assert e["tipo"] == "techo" and e["resultado"] == "sostenido", e
        assert abs(e["precision"]) < 1e-9 and not e["falso_rompimiento"], e
        assert abs(e["recorrido"] - 60.0) < 1e-9, e["recorrido"]
        assert e["llega_opuesta"] is True and abs(e["opuesta"] - 19940.0) < 1e-9
    print("  OK: sostenido, precision 0, recorrido 60, llega a la opuesta, en los tres modos")


def caso2_falso_rompimiento():
    print("\nCASO 2: falso rompimiento (mecha 6 arriba + dos velitas de cuerpo 1 cerrando 1 arriba, vuelve y baja 40)")
    sub = subida(19950, 19990)
    pin = [(19990, 20006, 19989.5, 20000.0),     # llegada: mechazo a 20006 (+6), cierra EN la raya
           (20000, 20002, 19999.5, 20001.0),     # velita 1: cuerpo 1, cierra +1
           (20000, 20002.5, 19999.75, 20001.0),  # velita 2: cuerpo 1, cierra +1
           (20001, 20001.5, 19994.0, 19995.0)]   # vuelve abajo
    baj = bajada(19995, 19965, 5)
    baj.append((19965, 19965.5, 19960, 19961))   # minimo 19960 = raya - 40
    lat = [(19962, 19968, 19961.0, 19964) for _ in range(40)]
    V = velas(sub + pin + baj + lat)
    R = rayas_fijas(len(V), [20000.0])
    r_t = ev_de(J.juzgar(V, R, {"modo": "tolerante"}), 20000.0)
    r_s = ev_de(J.juzgar(V, R, {"modo": "estricto"}), 20000.0)
    r_m = ev_de(J.juzgar(V, R, {"modo": "muy_tolerante"}), 20000.0)
    muestra("tolerante", r_t); muestra("estricto", r_s); muestra("muy_tolerante", r_m)
    assert len(r_t) == 1 and r_t[0]["resultado"] == "sostenido" and r_t[0]["falso_rompimiento"], r_t
    assert abs(r_t[0]["precision"] - 6.0) < 1e-9 and r_t[0]["cierres_mas_alla"] == 2
    assert abs(r_t[0]["recorrido"] - 40.0) < 1e-9 and abs(r_t[0]["recorrido_desde_punta"] - 46.0) < 1e-9
    assert r_s[0]["resultado"] == "rota" and "mecha" in r_s[0]["motivo"], r_s
    assert r_m[0]["resultado"] == "sostenido" and r_m[0]["falso_rompimiento"], r_m
    print("  OK: TOLERANTE sostenido con falso rompimiento (pinchazo +6, recorrido 40); ESTRICTO rota; MUY TOLERANTE sostenido")


def caso3_aceptacion():
    print("\nCASO 3: aceptacion arriba (5 cierres seguidos 10 pts arriba)")
    sub = subida(19950, 19990)
    acc = [(19990, 19999, 19989.5, 19998)]                          # llega
    acc += [(20009, 20011, 20008.5, 20010) for _ in range(5)]     # 5 cierres +10, cuerpo 1 (sin regla a)
    acc += bajada(20010, 19950, 6)                               # despues vuelve a bajar: igual fue rota
    acc += [(19950, 19953, 19948, 19951) for _ in range(30)]
    V = velas(sub + acc)
    R = rayas_fijas(len(V), [20000.0])
    # ESTRICTO: la mecha de +11 de la primera vela de arriba rompe dentro de la vela, antes del cierre (+10): 'mecha'
    esperado = {"tolerante": "3 cierres", "muy_tolerante": "5 cierres", "estricto": "mecha mas alla de +4"}
    for modo, mot in esperado.items():
        e = ev_de(J.juzgar(V, R, {"modo": modo}), 20000.0, "techo")
        muestra(modo, e)
        assert e[0]["resultado"] == "rota" and mot in e[0]["motivo"], (modo, e[0])
    # variante con vela grande: regla (a)
    acc2 = [(19990, 19999, 19989.5, 19998), (19998, 20011, 19997.5, 20010)] + [(20010, 20013, 20008, 20011) for _ in range(30)]
    e = ev_de(J.juzgar(velas(sub + acc2), rayas_fijas(len(sub) + len(acc2), [20000.0])), 20000.0, "techo")
    muestra("tolerante, vela grande", e)
    assert e[0]["resultado"] == "rota" and "cuerpo" in e[0]["motivo"], e
    print("  OK: rota en los tres modos (TOLERANTE al 3er cierre, MUY TOLERANTE al 5to, ESTRICTO al 1ro; vela grande por la regla a)")


def senoide(n, centro=20000.0, amp=50.0, periodo=40):
    p = [centro + amp * math.sin(2 * math.pi * k / periodo) for k in range(n + 1)]
    return [(p[k], max(p[k], p[k + 1]) + 1.0, min(p[k], p[k + 1]) - 1.0, p[k + 1]) for k in range(n)]


def caso4_raya_en_el_medio():
    print("\nCASO 4: senoide 19950-20050; raya en el MEDIO (20000) contra rayas en las PUNTAS (20050 techo, 19950 piso)")
    V = velas(senoide(230))                       # mechas extremas: 20051 / 19949
    medio = J.juzgar(V, rayas_fijas(len(V), [20000.0]))
    puntas = J.juzgar(V, rayas_fijas(len(V), [20051.0, 19949.0]))
    mm, mp = medio["metricas"], puntas["metricas"]
    print("  medio : llegadas %d sostenidos %d (%.0f %%) rotas %d" % (mm["llegadas"], mm["sostenidos"], mm["pct_sostenidos"], mm["rotas"]))
    print("  puntas: llegadas %d sostenidos %d (%.0f %%) rotas %d precision mediana %+.2f recorrido medio %.1f llega opuesta %.0f %%" % (
        mp["llegadas"], mp["sostenidos"], mp["pct_sostenidos"], mp["rotas"], mp["precision_mediana"], mp["recorrido_medio"],
        mp["pct_llega_opuesta"]))
    assert mm["llegadas"] >= 10, mm
    assert mm["pct_sostenidos"] <= 20, mm
    assert mp["pct_sostenidos"] >= 90 and mp["precision_mediana_abs"] <= 2.5 and mp["recorrido_medio"] >= 80, mp
    assert mp["pct_llega_opuesta"] >= 90, mp
    # el juez viejo (toques/rebotes) premiaba al medio por tocarse muchas veces; aca el medio pierde
    print("  OK: la del medio se toca >= 10 veces y casi nunca es extremo sostenido; las de las puntas si")
    # SENSIBILIDAD documentada: raya 2 pts adentro de la punta (20049). En la cima la senoide cierra 3 minutos seguidos apenas
    # arriba (+0,4/+1/+0,4): la regla (b) pre-registrada ('3 cierres seguidos mas alla') la da ROTA en TOLERANTE; con 5 cierres
    # (MUY TOLERANTE) queda SOSTENIDA. Es indecision chica, no aceptacion: ojo al leer los resultados del TOLERANTE.
    adentro_t = J.juzgar(V, rayas_fijas(len(V), [20049.0, 19951.0]))["metricas"]
    adentro_m = J.juzgar(V, rayas_fijas(len(V), [20049.0, 19951.0]), {"modo": "muy_tolerante"})["metricas"]
    print("  raya 2 pts adentro de la punta: TOLERANTE sostenidos %.0f %% ; MUY TOLERANTE %.0f %%" % (
        adentro_t["pct_sostenidos"], adentro_m["pct_sostenidos"]))
    assert adentro_t["pct_sostenidos"] <= 10 and adentro_m["pct_sostenidos"] >= 90


def caso5_raya_atravesada():
    print("\nCASO 5: tendencia que atraviesa un techo (20000) y despues un piso (19900)")
    sube = [(19900 + 6 * k, 19906 + 6 * k + 0.5, 19900 + 6 * k - 0.5, 19906 + 6 * k) for k in range(30)]   # 19900 -> 20080
    top = sube[-1][3]
    baja = [(top - 6 * k, top - 6 * k + 0.5, top - 6 * k - 6.5, top - 6 * k - 6) for k in range(40)]  # 20080 -> 19840
    V = velas(sube + baja)
    R = rayas_fijas(len(V), [20000.0, 19880.0])
    res = J.juzgar(V, R)
    muestra("tolerante", res["eventos"])
    e1 = ev_de(res, 20000.0, "techo"); e2 = ev_de(res, 20000.0, "piso"); e3 = ev_de(res, 19880.0, "piso")
    assert e1 and e1[0]["resultado"] == "rota"
    assert e2 and e2[0]["resultado"] == "rota"
    assert e3 and e3[0]["resultado"] == "rota"
    print("  OK: atravesada = rota (de subida como techo, de bajada como piso)")


def caso6_sin_mirar_adelante():
    print("\nCASO 6: no mirar adelante (la punta es la vela 10; la raya aparece en distintas claves)")
    sub = subida(19960, 19990, 3)                       # 10 velas: 0..9, highs <= 19990.5
    assert len(sub) == 10
    punta = [(19990, 20000, 19989, 19992)]              # vela 10 toca 20000
    baj = bajada(19992, 19952, 5) + [(19952, 19955, 19950, 19953) for _ in range(30)]
    V = velas(sub + punta + baj)
    n = len(V)
    def con_raya_desde(k):
        return {T0 + pd.Timedelta(minutes=j): ([20000.0] if j >= k else []) for j in range(-1, n)}
    r9 = J.juzgar(V, con_raya_desde(9))
    r10 = J.juzgar(V, con_raya_desde(10))
    r11 = J.juzgar(V, con_raya_desde(11))
    print("  raya dibujada desde la clave 9 (vigente en la vela 10): llegadas %d" % len(r9["eventos"]))
    print("  raya dibujada desde la clave 10 (con la vela de la punta ya vista): llegadas %d" % len(r10["eventos"]))
    print("  raya dibujada desde la clave 11: llegadas %d" % len(r11["eventos"]))
    assert len(r9["eventos"]) == 1 and r9["eventos"][0]["resultado"] == "sostenido"
    assert len(r10["eventos"]) == 0 and len(r11["eventos"]) == 0
    # una raya que cambia: la clave 10 dice 20000 pero la 9 decia 19900 -> la vela 10 usa 19900 (no toca)
    R = {T0 + pd.Timedelta(minutes=j): ([19900.0] if j < 10 else [20000.0]) for j in range(-1, n)}
    assert all(abs(e["raya"] - 20000.0) > 1e-6 for e in J.juzgar(V, R)["eventos"])
    # cobertura: la punta 20000 es un giro; con la raya desde la clave 9 esta cubierta, desde la 10 no
    c9 = J.cobertura_giros(V, con_raya_desde(9)); c10 = J.cobertura_giros(V, con_raya_desde(10))
    g9 = [g for g in c9["giros"] if g["tipo"] == "max"]; g10 = [g for g in c10["giros"] if g["tipo"] == "max"]
    print("  cobertura del giro 20000: raya desde clave 9 -> %s ; desde clave 10 -> %s" % (g9[0]["cubierto"], g10[0]["cubierto"]))
    assert g9 and g9[0]["cubierto"] and abs(g9[0]["precio"] - 20000) < 1e-9
    assert g10 and not g10[0]["cubierto"]
    print("  OK: solo cuenta la raya que ya estaba dibujada con datos hasta la vela anterior")


def caso7_orden_intravela():
    print("\nCASO 7: orden dentro de la vela de llegada")
    sub = subida(19950, 19990)
    # bajista: toca 20000 y en la MISMA vela cae a 19980 (o->h->l->c): giro valido en la vela de llegada
    a = velas(sub + [(19990, 20000.5, 19980, 19982)] + bajada(19982, 19950, 4) + lateral(19950, 20))
    ea = ev_de(J.juzgar(a, rayas_fijas(len(a), [20000.0])), 20000.0)
    # alcista: el minimo 19980 fue ANTES del toque (o->l->h->c): no es giro todavia
    b = velas(sub + [(19990, 20000.5, 19980, 19998)] + [(19998, 19999, 19990, 19991)] + bajada(19991, 19950, 4) + lateral(19950, 20))
    eb = ev_de(J.juzgar(b, rayas_fijas(len(b), [20000.0])), 20000.0)
    muestra("bajista", ea); muestra("alcista", eb)
    assert ea[0]["resultado"] == "sostenido" and ea[0]["min_a_decision"] == 0
    assert eb[0]["resultado"] == "sostenido" and eb[0]["min_a_decision"] >= 1
    print("  OK")


def caso8_indefinida_y_censura():
    print("\nCASO 8: indefinida (60 min pegada a la raya sin definir) y censura por fin de datos")
    sub = subida(19950, 19990)
    pegada = [(19995, 19999, 19993, 19996 + (k % 2)) for k in range(70)]   # 70 min en la banda, sin cierre arriba ni giro
    V = velas(sub + [(19990, 19999, 19989.5, 19996)] + pegada + bajada(19996, 19950, 5) + lateral(19950, 10))
    e = ev_de(J.juzgar(V, rayas_fijas(len(V), [20000.0])), 20000.0)
    muestra("tolerante", e)
    assert e[0]["resultado"] == "indefinida" and not e[0]["censurada"]
    V2 = velas(sub + [(19990, 19999, 19989.5, 19996)] + pegada[:10])
    e2 = ev_de(J.juzgar(V2, rayas_fijas(len(V2), [20000.0])), 20000.0)
    assert e2[0]["resultado"] == "indefinida" and e2[0]["censurada"]
    print("  OK")


def zigzag_irregular(semilla=7, tramos=60):
    """precio en zigzag con tramos de 25-90 pts y 8-40 min, con ruido. Devuelve (velas, puntas[(k, precio, tipo)], inicios)."""
    rng = np.random.default_rng(semilla)
    p = 20000.0; path = [p]; piv = []; sg = 1
    for _ in range(tramos):
        amp = rng.uniform(25, 90); dur = int(rng.integers(8, 40))
        paso = sg * amp / dur
        for _ in range(dur):
            p += paso + rng.normal(0, 0.8); path.append(p)
        piv.append(len(path) - 1); sg = -sg
    o = np.array(path[:-1]); c = np.array(path[1:])
    h = np.maximum(o, c) + np.abs(rng.normal(0, 1.0, len(o))); l = np.minimum(o, c) - np.abs(rng.normal(0, 1.0, len(o)))
    ohlc = [tuple(np.round(np.array(x) * 4) / 4) for x in zip(o, h, l, c)]
    V = velas(ohlc)
    hh = V["h"].to_numpy(); ll = V["l"].to_numpy()
    puntas = []; ini = 0
    for q, kp in enumerate(piv):          # la punta real (mecha) del tramo que termina en kp
        a, b = ini, min(kp + 1, len(V))
        if q % 2 == 0:
            k = a + int(np.argmax(hh[a:b])); puntas.append((k, float(hh[k]), "max", ini))
        else:
            k = a + int(np.argmin(ll[a:b])); puntas.append((k, float(ll[k]), "min", ini))
        ini = k
    return V, puntas


def caso9_placebo_mecanica():
    print("\nCASO 9: placebo (mecanica) en un zigzag irregular: ORACULO (raya en la punta real de cada tramo, dibujada desde que")
    print("        arranca el tramo) contra MITAD (raya a mitad de cada tramo), contra corridas y 300 juegos al azar")
    V, puntas = zigzag_irregular()
    n = len(V)
    def conjunto(fn):
        R = {T0 + pd.Timedelta(minutes=j): [] for j in range(-1, n)}
        for k, precio, tp, ini in puntas:
            x = fn(k, precio, tp, ini)
            for j in range(ini, min(k + 20, n)):      # clave j -> vigente en j+1: dibujada desde el arranque del tramo
                R[T0 + pd.Timedelta(minutes=j)].append(x)
        return R
    oraculo = conjunto(lambda k, precio, tp, ini: precio)
    mitad = conjunto(lambda k, precio, tp, ini: (precio + (V["l"].iloc[ini] if tp == "max" else V["h"].iloc[ini])) / 2)
    for nom, R in (("ORACULO", oraculo), ("MITAD", mitad)):
        pl = J.placebo(V, R, n_azar=300, n_boot=300)
        print("  %s: %d pistas" % (nom, pl["n_pistas"]))
        for m in ("llegadas", "pct_sostenidos", "pct_exactos", "precision_mediana_abs", "recorrido_medio", "cobertura_pct"):
            a = pl["azar"][m]
            print("    %-22s real %7.1f | azar p5 %7.1f p50 %7.1f p95 %7.1f | percentil %5.1f" % (
                m, a["real"], a["p5"], a["p50"], a["p95"], a["percentil"]))
        cj = pl["corridos_juntos"]
        print("    corridas juntas: llegadas %d sostenidos %.0f %% exactos %.0f %% cobertura %.0f %%" % (
            cj["llegadas"], cj["pct_sostenidos"], cj["pct_exactos"], cj["cobertura_pct"]))
        ic = pl["bootstrap_ic90"]
        print("    IC90 bootstrap por noche (%d noches sinteticas: solo prueba la mecanica): pct_sostenidos %s" % (
            pl["real"]["n_noches_ventana"], tuple(round(x, 1) for x in ic["pct_sostenidos"])))
        if nom == "ORACULO":
            assert pl["azar"]["pct_exactos"]["percentil"] >= 95 and pl["azar"]["cobertura_pct"]["percentil"] >= 95
            assert pl["real"]["cobertura_pct"] >= 90 and cj["pct_exactos"] < pl["real"]["pct_exactos"]
        else:
            assert pl["azar"]["pct_exactos"]["percentil"] <= 60 and pl["real"]["cobertura_pct"] <= 10
    print("  OK: el oraculo queda arriba del placebo y la raya a mitad de tramo no")


if __name__ == "__main__":
    caso1_techo_perfecto()
    caso2_falso_rompimiento()
    caso3_aceptacion()
    caso4_raya_en_el_medio()
    caso5_raya_atravesada()
    caso6_sin_mirar_adelante()
    caso7_orden_intravela()
    caso8_indefinida_y_censura()
    caso9_placebo_mecanica()
    print("\nTODOS LOS CASOS OK")
