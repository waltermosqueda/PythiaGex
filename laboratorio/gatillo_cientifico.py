# -*- coding: utf-8 -*-
"""BANCO DE PRUEBAS DE GATILLOS DIRECCIONALES (scalping intradia, ES y NQ).

Pedido (2026-09-10): "un gatillo long/short que acierte, sin ruido, en el momento justo,
usando dominantes, niveles, convexidad, pelotitas, 0DTE, order flow; medir cuanto acerto y
cuanto subio". La respuesta seria es un banco de pruebas, no una corazonada:

  - Datos: una fila por minuto de la rueda americana anotada por Gamma Hoy en ATAS
    (rebobinado-atas-<inst>-M1): O/H/L/C, volumen, operaciones, DELTA real del futuro, y
    los niveles vigentes ese minuto (dominantes, zero gamma, majors, pico, cuadrante,
    Max Change). 13-16 dias.
  - Hipotesis DEFINIDAS ANTES de mirar resultados (abajo, S1..S8), cada una da LARGO o
    CORTO en un minuto concreto, con enfriamiento para no disparar en rafaga.
  - Objetivo a escala del instrumento (G puntos): regla A simetrica (G a favor antes que G
    en contra en H min) y regla B 2:1 (2G a favor antes que G en contra). Vela que toca los
    dos: no cuenta. Ademas MFE/MAE (hasta donde llego a favor / en contra).
  - Entrenamiento / prueba: los primeros N dias eligen; los ultimos juzgan. Lo que gana
    solo en entrenamiento es azar.
  - Controles: (1) PERMUTACION: mismos disparos con el lado al azar (20 veces): la tasa
    base real del objetivo; (2) PLACEBO: los mismos gatillos con TODOS los niveles corridos
    +-X puntos: si dan lo mismo, el nivel no aporta.

Uso: python laboratorio/gatillo_cientifico.py MNQ [--g 25] [--h 30] [--train 8] [--todo]
"""
import io
import json
import math
import os
import random
import statistics as st
import sys
from datetime import datetime

ATAS = os.path.join(os.environ.get("APPDATA", ""), "ATAS")
BANDA_PCT, QUIETA_PCT = 0.08, 0.026
ENFRIAMIENTO = 15          # minutos entre disparos del mismo gatillo


def cargar(inst, marco="M1"):
    p = os.path.join(ATAS, "pythiagex-centinela-rebobinado-atas-%s-TimeFrame-%s.jsonl" % (inst, marco))
    ult = {}
    for l in io.open(p, encoding="utf-8", errors="replace"):
        try:
            d = json.loads(l)
        except Exception:
            continue
        if d.get("niv") and "13:30" <= d["t"][11:16] < "20:00" and d.get("vol", 0) > 0:
            ult[d["t"]] = d
    vs = []
    for t in sorted(ult):
        d = ult[t]; n = d["niv"]
        vs.append(dict(t=datetime.strptime(t[:19], "%Y-%m-%dT%H:%M:%S"), dia=t[:10], o=d["o"], h=d["h"], l=d["l"], c=d["c"],
                       vol=float(d["vol"]), ops=float(d.get("ops") or 0), delta=float(d.get("delta") or 0),
                       doms=[x for x in (n.get("dom0"), n.get("dom1")) if x],
                       zero=n.get("zero_vol") or n.get("zero_oi"), mp=n.get("mp_vol") or n.get("mp_oi"), mn=n.get("mn_vol") or n.get("mn_oi"),
                       pico=n.get("pico"), q=n.get("q_cuadrante"), mc1=n.get("mc1"), mc5=n.get("mc5"), mc30=n.get("mc30")))
    return vs


def rasgos(vs):
    """Rasgos por vela, con ventana de 60 dentro del mismo dia."""
    for i, v in enumerate(vs):
        j0 = i
        while j0 > 0 and i - j0 < 60 and vs[j0 - 1]["dia"] == v["dia"]:
            j0 -= 1
        prev = vs[j0:i]
        v["rango"] = v["h"] - v["l"]
        if len(prev) >= 20:
            ds = [x["delta"] for x in prev]; sd = st.pstdev(ds) or 1.0
            v["dz"] = v["delta"] / sd
            vol = [x["vol"] for x in prev]; v["vz"] = (v["vol"] - st.mean(vol)) / (st.pstdev(vol) or 1.0)
            v["rt"] = st.median(x["rango"] for x in prev) or 0.25
        else:
            v["dz"] = v["vz"] = 0.0; v["rt"] = v["rango"] or 0.25
        k15 = [x for x in prev[-15:]]
        v["cum15"] = sum(x["delta"] for x in k15) + v["delta"]
        v["ret15"] = v["c"] - k15[0]["c"] if k15 else 0.0
        d3 = [x["delta"] for x in prev[-2:]] + [v["delta"]]
        v["tren"] = len(d3) == 3 and (all(x > 0 for x in d3) or all(x < 0 for x in d3))
        h15 = [x["h"] for x in k15]; l15 = [x["l"] for x in k15]
        v["nuevoAlto15"] = bool(h15) and v["h"] > max(h15); v["nuevoBajo15"] = bool(l15) and v["l"] < min(l15)
        v["conv_neg"] = v["q"] in (2, 4); v["mucho"] = v["q"] in (1, 2)
        arr = [d for d in v["doms"] if d > v["c"]]; aba = [d for d in v["doms"] if d <= v["c"]]
        v["dom_arr"] = min(arr) if arr else None; v["dom_aba"] = max(aba) if aba else None


def con_niveles_corridos(vs, corr):
    """Copia con todos los niveles corridos 'corr' puntos (placebo)."""
    out = []
    for v in vs:
        w = dict(v)
        for k in ("zero", "mp", "mn", "pico", "mc1", "mc5", "mc30"):
            if w.get(k): w[k] = w[k] + corr
        w["doms"] = [d + corr for d in v["doms"]]
        arr = [d for d in w["doms"] if d > w["c"]]; aba = [d for d in w["doms"] if d <= w["c"]]
        w["dom_arr"] = min(arr) if arr else None; w["dom_aba"] = max(aba) if aba else None
        out.append(w)
    return out


# ------------------------------------------------------------------ las hipotesis (definidas antes de mirar)
def semi(v):
    return v["c"] * BANDA_PCT / 100.0


def S1_zero_momentum(vs, i):
    """Cruce del zero gamma con convexidad negativa (tobogan): sigue el cruce."""
    v, p = vs[i], vs[i - 1]
    if not v["zero"] or not p["zero"] or not v["conv_neg"]:
        return 0
    if p["c"] < p["zero"] and v["c"] > v["zero"]: return 1
    if p["c"] > p["zero"] and v["c"] < v["zero"]: return -1
    return 0


def S1b_zero_fade(vs, i):
    """Cruce del zero gamma con convexidad positiva (colchon): se desvanece el cruce."""
    v, p = vs[i], vs[i - 1]
    if not v["zero"] or not p["zero"] or v["conv_neg"]:
        return 0
    if p["c"] < p["zero"] and v["c"] > v["zero"]: return -1
    if p["c"] > p["zero"] and v["c"] < v["zero"]: return 1
    return 0


def S2_major_break(vs, i):
    """Cierre mas alla del major con delta a favor: continuacion."""
    v, p = vs[i], vs[i - 1]
    if v["mp"] and p["mp"] and p["c"] <= p["mp"] and v["c"] > v["mp"] and v["dz"] >= 1.0: return 1
    if v["mn"] and p["mn"] and p["c"] >= p["mn"] and v["c"] < v["mn"] and v["dz"] <= -1.0: return -1
    return 0


def S2f_major_break_conv(vs, i):
    return S2_major_break(vs, i) if vs[i]["conv_neg"] else 0


def S3_major_fade(vs, i):
    """Primera entrada a la banda del major con delta en contra: rechazo."""
    v, p = vs[i], vs[i - 1]
    s = semi(v)
    if v["mp"] and p["mp"] and v["h"] >= v["mp"] - s and p["h"] < p["mp"] - s and v["c"] < v["mp"] and v["dz"] <= -1.0: return -1
    if v["mn"] and p["mn"] and v["l"] <= v["mn"] + s and p["l"] > p["mn"] + s and v["c"] > v["mn"] and v["dz"] >= 1.0: return 1
    return 0


def S3f_major_fade_conv(vs, i):
    return S3_major_fade(vs, i) if not vs[i]["conv_neg"] else 0


def S4_mc_align(vs, i):
    """Max Change alineado (30, 5 y 1 min en el mismo strike) a 0,1-0,6 % del precio: iman."""
    v = vs[i]
    if not (v["mc1"] and v["mc5"] and v["mc30"]):
        return 0
    if abs(v["mc1"] - v["mc5"]) > 1.0 or abs(v["mc5"] - v["mc30"]) > 1.0:
        return 0
    d = v["mc30"] - v["c"]
    if v["c"] * 0.001 <= d <= v["c"] * 0.006: return 1
    if -v["c"] * 0.006 <= d <= -v["c"] * 0.001: return -1
    return 0


def S5_tren_momentum(vs, i):
    """Tres deltas seguidos + delta fuerte + convexidad negativa, fuera de las bandas: sigue el delta."""
    v = vs[i]
    if not (v["tren"] and abs(v["dz"]) >= 1.5 and v["conv_neg"]):
        return 0
    s = semi(v)
    if v["dom_arr"] and v["dom_arr"] - v["c"] <= s: return 0
    if v["dom_aba"] and v["c"] - v["dom_aba"] <= s: return 0
    return 1 if v["delta"] > 0 else -1


def S6_cvd_div(vs, i):
    """Nuevo minimo de 15 min dentro de la banda de la dominante de abajo con delta acumulado positivo: divergencia, largo. Espejo arriba."""
    v = vs[i]
    s = semi(v)
    if v["dom_aba"] and v["l"] <= v["dom_aba"] + s and v["nuevoBajo15"] and v["cum15"] > 0 and v["ret15"] < 0: return 1
    if v["dom_arr"] and v["h"] >= v["dom_arr"] - s and v["nuevoAlto15"] and v["cum15"] < 0 and v["ret15"] > 0: return -1
    return 0


def S7_rechazo_tren(vs, i):
    """La pista del 08-09: entrada a la banda de una dominante quieta + tres deltas en contra."""
    v = vs[i]
    if i < 6:
        return 0
    s = semi(v)
    for arriba, d in ((True, v["dom_arr"]), (False, v["dom_aba"])):
        if d is None:
            continue
        # quieta: la dominante de ese lado se movio <= QUIETA_PCT en 5 velas
        hist = [(vs[k]["dom_arr"] if arriba else vs[k]["dom_aba"]) for k in range(i - 5, i)]
        if any(x is None for x in hist) or abs(d - hist[0]) > v["c"] * QUIETA_PCT / 100.0:
            continue
        dentro = v["h"] >= d - s if arriba else v["l"] <= d + s
        p = vs[i - 1]
        dentro_p = (p["h"] >= (p["dom_arr"] or 1e12) - s) if arriba else (p["l"] <= (p["dom_aba"] or -1e12) + s)
        if not dentro or dentro_p:
            continue
        if v["tren"] and ((v["delta"] < 0) == arriba):
            return -1 if arriba else 1
    return 0


def S8_dom_reject_delta(vs, i):
    """Entrada a la banda de una dominante (sin exigir quieta) con delta fuerte en contra y cierre adentro: rechazo."""
    v, p = vs[i], vs[i - 1]
    s = semi(v)
    if v["dom_arr"] and p["dom_arr"] and v["h"] >= v["dom_arr"] - s and p["h"] < p["dom_arr"] - s and v["c"] < v["dom_arr"] and v["dz"] <= -1.5: return -1
    if v["dom_aba"] and p["dom_aba"] and v["l"] <= v["dom_aba"] + s and p["l"] > p["dom_aba"] + s and v["c"] > v["dom_aba"] and v["dz"] >= 1.5: return 1
    return 0


HIPOTESIS = [("S1 zero cross + conv negativa (momentum)", S1_zero_momentum),
             ("S1b zero cross + conv positiva (fade)", S1b_zero_fade),
             ("S2 ruptura del major con delta", S2_major_break),
             ("S2f ruptura del major con delta y conv negativa", S2f_major_break_conv),
             ("S3 rechazo en el major con delta", S3_major_fade),
             ("S3f rechazo en el major con delta y conv positiva", S3f_major_fade_conv),
             ("S4 Max Change alineado (iman)", S4_mc_align),
             ("S5 tren + delta fuerte + conv negativa (momentum)", S5_tren_momentum),
             ("S6 divergencia CVD en la banda", S6_cvd_div),
             ("S7 rechazo·tren en dominante quieta", S7_rechazo_tren),
             ("S8 rechazo en dominante con delta fuerte", S8_dom_reject_delta)]


# ------------------------------------------------------------------ desenlaces
def desenlace(vs, i, lado, g, gc, h):
    ent = vs[i]["c"]; meta, stop = ent + lado * g, ent - lado * gc
    mfe = mae = 0.0
    for j in range(i + 1, min(len(vs), i + 1 + h)):
        if vs[j]["dia"] != vs[i]["dia"]:
            break
        v = vs[j]
        mfe = max(mfe, (v["h"] - ent) * lado); mae = max(mae, (ent - v["l"]) * lado)
        gano = v["h"] >= meta if lado > 0 else v["l"] <= meta
        perdio = v["l"] <= stop if lado > 0 else v["h"] >= stop
        if gano and perdio: return None, mfe, mae
        if gano: return True, mfe, mae
        if perdio: return False, mfe, mae
    return None, mfe, mae


def disparos(vs, f):
    out = []; ult = -10 ** 9
    for i in range(1, len(vs)):
        if vs[i]["dia"] != vs[i - 1]["dia"]:
            continue
        lado = f(vs, i)
        if lado and i - ult >= ENFRIAMIENTO:
            out.append((i, lado)); ult = i
    return out


def evaluar(vs, tiros, g, h, lados=None):
    A, B, mfes = [], [], []
    for k, (i, lado) in enumerate(tiros):
        ld = lados[k] if lados else lado
        rA, mfe, mae = desenlace(vs, i, ld, g, g, h)
        rB, _, _ = desenlace(vs, i, ld, 2 * g, g, int(h * 1.5))
        if rA is not None: A.append(rA)
        if rB is not None: B.append(rB)
        mfes.append(mfe)
    return A, B, mfes


def pct(xs):
    return 100.0 * sum(1 for x in xs if x) / len(xs) if xs else float("nan")


def main():
    a = [x for x in sys.argv[1:] if not x.startswith("--")]
    inst = a[0] if a else "MNQ"
    arg = lambda k, d: type(d)(sys.argv[sys.argv.index(k) + 1]) if k in sys.argv else d
    es_nq = inst.startswith("MNQ") or inst.startswith("NQ")
    g = arg("--g", 25.0 if es_nq else 6.0); h = arg("--h", 30); ntrain = arg("--train", 8)
    vs = cargar(inst); rasgos(vs)
    dias = sorted(set(v["dia"] for v in vs))
    train_d, test_d = set(dias[:ntrain]), set(dias[ntrain:])
    placebos = (-140, -60, 60, 140) if es_nq else (-35, -15, 15, 35)
    print("%s: %d velas de rueda, %d dias (%s a %s) | G = %g pts, H = %d min | entrena %d dias, prueba %d" % (
        inst, len(vs), len(dias), dias[0], dias[-1], g, h, len(train_d), len(test_d)))
    print("regla A: %g a favor antes que %g en contra (moneda = 50 %%) | regla B: %g a favor antes que %g en contra (moneda = 33 %%)" % (g, g, 2 * g, g))
    print()
    random.seed(7)
    filas = []
    for nombre, f in HIPOTESIS:
        tiros = disparos(vs, f)
        if len(tiros) < 8:
            print("  %-52s %3d disparos: pocos" % (nombre, len(tiros))); continue
        tr = [(i, l) for (i, l) in tiros if vs[i]["dia"] in train_d]; te = [(i, l) for (i, l) in tiros if vs[i]["dia"] in test_d]
        A_tr, B_tr, _ = evaluar(vs, tr, g, h); A_te, B_te, mfe_te = evaluar(vs, te, g, h)
        A_all, B_all, mfe_all = evaluar(vs, tiros, g, h)
        # permutacion del lado
        pA, pB = [], []
        for _ in range(20):
            lados = [random.choice((1, -1)) for _ in tiros]
            Ap, Bp, _ = evaluar(vs, tiros, g, h, lados); pA.append(pct(Ap)); pB.append(pct(Bp))
        # placebo: niveles corridos
        plA, plB, npl = [], [], 0
        for corr in placebos:
            vc = con_niveles_corridos(vs, corr)
            tc = disparos(vc, f)
            Ac, Bc, _ = evaluar(vc, tc, g, h); plA += Ac; plB += Bc; npl += len(tc)
        por_dia = len(tiros) / float(len(dias))
        filas.append((nombre, len(tiros), por_dia, pct(A_tr), len(A_tr), pct(A_te), len(A_te), pct(B_tr), pct(B_te), len(B_te),
                      st.mean(pA), st.mean(pB), pct(plA), pct(plB), npl, st.median(mfe_all) if mfe_all else float("nan"), pct(A_all), pct(B_all)))
    print("  %-52s %5s %5s | %-17s | %-17s | %-13s | %-15s | %s" % ("gatillo", "disp", "x dia", "A entrena/prueba", "B entrena/prueba", "azar A / B", "placebo A / B", "MFE med"))
    for (n, nd, pd, atr, natr, ate, nate, btr, bte, nbte, pa, pb, pla, plb, npl, mfe, aall, ball) in sorted(filas, key=lambda r: -(r[5] if not math.isnan(r[5]) else -1)):
        print("  %-52s %5d %5.1f | %5.1f%%(%3d) %5.1f%%(%3d) | %5.1f%% %5.1f%%(%3d) | %5.1f%% / %4.1f%% | %5.1f%% / %4.1f%% (%d) | %5.1f pts" % (
            n[:52], nd, pd, atr, natr, ate, nate, btr, bte, nbte, pa, pb, pla, plb, npl, mfe))
    print()
    print("COMO LEERLO: un gatillo sirve si en PRUEBA (dias que no eligieron nada) supera claramente al azar (A > 50 %, B > 33 %)")
    print("Y al placebo con niveles corridos, con 15+ casos; y si ademas gano en entrenamiento. Todo lo demas es ruido.")


if __name__ == "__main__":
    main()
