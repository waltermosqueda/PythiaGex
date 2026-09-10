# -*- coding: utf-8 -*-
"""GATILLO REBOTE: la formula de entrada que reproduce los ejemplos del operador del 10-09-2026 en
MNQ (rebotes en las rayas amarillas), medida en todos los dias contra placebo.

La raya amarilla que el ve es el conjunto ACUMULADO de dominantes del dia (cada dominante que hubo
hoy queda dibujada como fila de guiones), mas el zero gamma y los majors. La entrada larga (corta
es espejo):
  1. NIVEL L: cualquier dominante que hubo hoy hasta ahora (redondeada al strike), el zero gamma o
     un major.
  2. TOQUE: el minimo de la vela llega a L + TOL o lo traspasa hasta L - PEN (traspaso permitido).
  3. VENIA DE ARRIBA: en las 10 velas anteriores el maximo estuvo >= L + RET (es un retroceso al
     nivel, no una ruptura desde abajo).
  4. CIERRA BIEN: la vela cierra por encima de L (el nivel aguanto en el cierre).
  5. PRIMERA: la vela anterior no cumplia 2+4 (se entra en la primera vela que aguanta), y no hubo
     otro disparo en ese nivel en las ultimas COOL velas.
Entrada = cierre de esa vela. Se mide MFE/MAE a 20 min y varias reglas de salida (+G antes que -S),
porque el operador maneja la salida. Placebo = los mismos niveles corridos +-85 y +-145 pts.
Filtros probados (todos contra placebo): lado, precio sobre/bajo el zero, sobre/bajo la apertura
del dia, momentum 30 min, delta de la vela, franja, cuadrante.
Uso: python laboratorio/gatillo_rebote.py MNQ [--dia 2026-09-10] [--tol 10 --pen 25 --ret 15 --cool 5]
"""
import io
import json
import os
import sys
from collections import defaultdict
from datetime import datetime, timedelta

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rebote_dominantes_dia import cargar  # noqa: E402

CUAD = {1: "IMAN", 2: "EXPLOSIVO", 3: "ESTABLE", 4: "RIESGO"}
REGLAS = ((10, 10), (15, 15), (20, 20), (30, 30), (20, 10), (10, 20), (30, 15))
ESCALA = [1.0]   # ES: 0.25 (las reglas de salida se expresan en puntos de NQ y se escalan)


def fuegos(vs, tol, pen, ret, cool, corr=0.0, paso=10.0, hor=20, tipos=("dom", "zero", "major"), atras=10):
    out = []; acum = {}; ult = {}; dia = None; apertura = None; nacio = {}; toques = {}; rangos = []
    for i, v in enumerate(vs):
        if v["dia"] != dia:
            dia = v["dia"]; acum = {}; ult = {}; apertura = v["o"]; nacio = {}; toques = {}; rangos = []
        n = v["niv"]; c = v["c"]
        rangos.append(v["h"] - v["l"])
        rt = sorted(rangos[-60:])[len(rangos[-60:]) // 2] if len(rangos) >= 20 else (v["h"] - v["l"]) or 1.0
        # identidad de la fila = el strike (redondeo a PASO); el VALOR es el exacto (el ultimo de la
        # dominante, como el guion dibujado): igual que GatilloRebote.cs
        actuales = set()
        for k in ("dom0", "dom1"):
            if n.get(k):
                L0 = round((n[k] + corr) / paso) * paso
                acum[L0] = n[k] + corr; actuales.add(L0); nacio.setdefault(L0, i)
        niveles = {K: (x, "dom") for K, x in acum.items()} if "dom" in tipos else {}
        if "zero" in tipos and n.get("zero_vol"):
            niveles.setdefault(round((n["zero_vol"] + corr) / paso) * paso, (n["zero_vol"] + corr, "zero"))
        if "major" in tipos:
            for k in ("mp_vol", "mn_vol"):
                if n.get(k):
                    niveles.setdefault(round((n[k] + corr) / paso) * paso, (n[k] + corr, "major"))
        if i < atras or vs[i - atras]["dia"] != v["dia"]:
            continue
        prev = vs[i - 1]
        maxh = max(w["h"] for w in vs[i - atras:i]); minl = min(w["l"] for w in vs[i - atras:i])
        cand = {}
        for K, (L, tipo) in niveles.items():
            for lado in ("abajo", "arriba"):
                if lado == "abajo":
                    toca = v["l"] <= L + tol and v["l"] >= L - pen; bien = c >= L; venia = maxh >= L + ret
                    antes = prev["l"] <= L + tol and prev["l"] >= L - pen and prev["c"] >= L
                    dist = abs(v["l"] - L)
                else:
                    toca = v["h"] >= L - tol and v["h"] <= L + pen; bien = c <= L; venia = minl <= L - ret
                    antes = prev["h"] >= L - tol and prev["h"] <= L + pen and prev["c"] <= L
                    dist = abs(v["h"] - L)
                if not (toca and bien and venia) or antes or i - ult.get((K, lado), -999) <= cool:
                    continue
                if lado not in cand or dist < cand[lado][0]:
                    cand[lado] = (dist, L, tipo, K)
        for lado, (dist, L, tipo, K) in cand.items():
            ult[(K, lado)] = i
            toques[(K, lado)] = toques.get((K, lado), 0) + 1
            confl = sum(1 for K2 in niveles if abs(K2 - K) <= 10)
            rapido = sum(rangos[-3:]) / (3.0 * rt)
            ent = c; mfe = 0.0; mae = 0.0; res = {}
            fav_seq = []
            for j in range(i + 1, min(len(vs), i + 1 + hor)):
                w = vs[j]
                if w["dia"] != v["dia"]:
                    break
                fav = (w["h"] - ent) if lado == "abajo" else (ent - w["l"])
                con = (ent - w["l"]) if lado == "abajo" else (w["h"] - ent)
                fav_seq.append((fav, con)); mfe = max(mfe, fav); mae = max(mae, con)
            for g, s in REGLAS:
                gg, ss = g * ESCALA[0], s * ESCALA[0]
                r = False
                for fav, con in fav_seq:
                    if fav >= gg and con >= ss:
                        r = False; break         # la misma vela toca los dos: se cuenta perdida (conservador)
                    if fav >= gg:
                        r = True; break
                    if con >= ss:
                        r = False; break
                res[(g, s)] = r
            t = datetime.strptime(v["t"][:19], "%Y-%m-%dT%H:%M:%S")
            hr = (t.hour - 4) * 60 + t.minute
            zero = n.get("zero_vol")
            c30 = vs[i - 30]["c"] if i >= 30 and vs[i - 30]["dia"] == v["dia"] else None
            out.append(dict(t=t, dia=v["dia"], lado=lado, L=L, tipo=tipo, ext=v["l"] if lado == "abajo" else v["h"], c=c, delta=v["delta"],
                            mfe=mfe, mae=mae, res=res, q=n.get("q_cuadrante"), traspaso=max(0.0, (L - v["l"]) if lado == "abajo" else (v["h"] - L)),
                            franja="09:30-10:30" if hr < 630 else "10:30-12:00" if hr < 720 else "12:00-14:00" if hr < 840 else "14:00-16:00",
                            sobre_zero=(c > zero) if zero else None, sobre_apertura=c > apertura, mom30=(c > c30) if c30 else None,
                            n_toque=toques[(K, lado)], actual=K in actuales, confl=confl, rapido=rapido, edad=(i - nacio[K]) if K in nacio else -1,
                            dz=abs(v["delta"]) / (sorted(abs(w["delta"]) for w in vs[max(0, i - 60):i])[min(59, i - 1) // 2] + 1.0) if i > 0 else 0.0))
    return out


def a_favor(e, clave):
    x = e[clave]
    if x is None:
        return None
    return (e["lado"] == "abajo") == x


def main():
    a = [x for x in sys.argv[1:] if not x.startswith("--")]
    inst = a[0] if a else "MNQ"
    arg = lambda k, d: type(d)(sys.argv[sys.argv.index(k) + 1]) if k in sys.argv else d
    es_nq = inst.startswith("MNQ") or inst.startswith("NQ")
    tol = arg("--tol", 10.0 if es_nq else 2.5); pen = arg("--pen", 25.0 if es_nq else 6.0); ret = arg("--ret", 15.0 if es_nq else 4.0)
    marco = arg("--marco", "M1"); mpv = {"M1": 1, "M2": 2, "M5": 5}[marco]
    cool = max(1, arg("--cool", 5) // mpv); hor = max(2, arg("--hor", 20) // mpv); paso = 10.0 if es_nq else 5.0; atras = max(2, 10 // mpv)
    tipos = tuple(arg("--tipos", "dom,zero,major").split(","))
    ESCALA[0] = 1.0 if es_nq else 0.25
    vs = cargar(inst, marco)
    dia = arg("--dia", "")
    if dia:
        ev = [e for e in fuegos(vs, tol, pen, ret, cool, 0.0, paso, hor, tipos, atras) if e["dia"] == dia]
        print("%s %s: %d disparos (toque +-%g, traspaso <= %g, venia de >= %g, enfriamiento %d)" % (inst, dia, len(ev), tol, pen, ret, cool))
        print("hora   lado    nivel  tipo    extremo   cierre  delta  tras  zero apert mom30 q  MFE   MAE  +20/-20 +15/-15 +20/-10")
        for e in ev:
            print("%s %-7s %6.0f %-6s %8.2f %8.2f %6d %5.1f  %-4s %-4s %-4s %s %5.1f %5.1f  %-7s %-7s %-7s" % (
                (e["t"] - timedelta(hours=3)).strftime("%H:%M"), e["lado"], e["L"], e["tipo"], e["ext"], e["c"], round(e["delta"]), e["traspaso"],
                {True: "SI", False: "no", None: "-"}[a_favor(e, "sobre_zero")], {True: "SI", False: "no", None: "-"}[a_favor(e, "sobre_apertura")],
                {True: "SI", False: "no", None: "-"}[a_favor(e, "mom30")], e["q"], e["mfe"], e["mae"],
                "GANA" if e["res"][(20, 20)] else "pierde", "GANA" if e["res"][(15, 15)] else "pierde", "GANA" if e["res"][(20, 10)] else "pierde"))
        return
    real = fuegos(vs, tol, pen, ret, cool, 0.0, paso, hor, tipos, atras)
    placebos = (-145, -85, 85, 145) if es_nq else (-37, -22, 22, 37)
    plc = []
    for cc in placebos:
        plc += fuegos(vs, tol, pen, ret, cool, cc, paso, hor, tipos, atras)
    dias = sorted(set(v["dia"] for v in vs)); nd = float(len(dias))
    print("%s %s: %d dias | disparos reales %d (%.1f/dia) | placebo %d (%.1f/dia por juego) | toque +-%g traspaso <= %g venia >= %g enfriamiento %d velas horizonte %d velas" % (
        inst, marco, len(dias), len(real), len(real) / nd, len(plc), len(plc) / nd / len(placebos), tol, pen, ret, cool, hor))

    def pct(ev, regla):
        return 100.0 * sum(1 for e in ev if e["res"][regla]) / len(ev) if ev else float("nan")

    def fila(nombre, ev, evp, nd_):
        if len(ev) < 12:
            return
        mfe = sorted(e["mfe"] for e in ev); mfep = sorted(e["mfe"] for e in evp) if evp else [0]
        print("    %-44s %4d (%4.1f/dia) | +10/-10 %5.1f (%5.1f) | +15/-15 %5.1f (%5.1f) | +20/-20 %5.1f (%5.1f) | +20/-10 %5.1f (%5.1f) | +10/-20 %5.1f (%5.1f) | MFE med %5.1f (%5.1f)" % (
            nombre, len(ev), len(ev) / nd_, pct(ev, (10, 10)), pct(evp, (10, 10)), pct(ev, (15, 15)), pct(evp, (15, 15)), pct(ev, (20, 20)), pct(evp, (20, 20)),
            pct(ev, (20, 10)), pct(evp, (20, 10)), pct(ev, (10, 20)), pct(evp, (10, 20)), mfe[len(mfe) // 2], mfep[len(mfep) // 2]))

    def tabla(tit, clave):
        print("  " + tit + "   [real % (placebo %)]")
        g = defaultdict(list); gp = defaultdict(list)
        for e in real: g[clave(e)].append(e)
        for e in plc: gp[clave(e)].append(e)
        for k in sorted(g, key=lambda k: str(k)):
            fila(str(k), g[k], gp.get(k, []), nd)
    tabla("TODO:", lambda e: "todos")
    tabla("por lado:", lambda e: e["lado"])
    tabla("por tipo de nivel:", lambda e: e["tipo"])
    tabla("a favor del zero (largo si el precio esta sobre el zero, corto si debajo):", lambda e: {True: "a favor del zero", False: "contra el zero", None: "sin zero"}[a_favor(e, "sobre_zero")])
    tabla("a favor de la apertura del dia (largo si el precio esta sobre la apertura):", lambda e: {True: "a favor de la apertura", False: "contra la apertura", None: "?"}[a_favor(e, "sobre_apertura")])
    tabla("a favor del momentum de 30 min:", lambda e: {True: "a favor del momentum", False: "contra el momentum", None: "sin 30 min"}[a_favor(e, "mom30")])
    tabla("delta de la vela del toque (rechazo = delta contra el nivel; absorcion = delta hacia el nivel y aguanta):",
          lambda e: "delta hacia el nivel (absorcion)" if ((e["delta"] < 0) == (e["lado"] == "abajo")) else "delta contra el nivel (rechazo)")
    tabla("traspaso:", lambda e: "sin traspaso" if e["traspaso"] == 0 else "traspasa <= 10" if e["traspaso"] <= 10 else "traspasa > 10")
    tabla("franja:", lambda e: e["franja"])
    tabla("cuadrante:", lambda e: CUAD.get(e["q"], "?"))
    tabla("numero de toque del nivel en el dia (1 = primera vez):", lambda e: "1er toque" if e["n_toque"] == 1 else "2do toque" if e["n_toque"] == 2 else "3er toque o mas")
    tabla("nivel actual (dominante de ahora) o fila vieja:", lambda e: ("dominante actual" if e["actual"] else "fila vieja") if e["tipo"] == "dom" else "zero/major")
    tabla("edad de la dominante (min desde que aparecio):", lambda e: "nueva (< 15 min)" if 0 <= e["edad"] < 15 else "15-60 min" if e["edad"] < 60 else "vieja (> 60 min)" if e["edad"] >= 60 else "no dom")
    tabla("confluencia (niveles distintos a +-10 pts):", lambda e: "1 nivel" if e["confl"] <= 1 else "2 niveles" if e["confl"] == 2 else "3 o mas")
    tabla("llegada rapida (rango de las ultimas 3 velas / rango tipico):", lambda e: "rapida (>= 2x)" if e["rapido"] >= 2.0 else "normal (1-2x)" if e["rapido"] >= 1.0 else "lenta (< 1x)")
    tabla("delta de la vela del toque, en tamaño (|delta| / mediana 60 velas):", lambda e: "delta grande (>= 2x)" if e["dz"] >= 2.0 else "delta normal")
    tabla("primer toque + dominante actual:", lambda e: "1er toque de la dominante actual" if (e["n_toque"] == 1 and e["actual"]) else "resto")
    tabla("primer toque + dominante actual + a favor del zero:", lambda e: "1er toque dom actual a favor del zero" if (e["n_toque"] == 1 and e["actual"] and a_favor(e, "sobre_zero")) else "resto")
    tabla("llegada rapida + delta grande (capitulacion) por lado:", lambda e: ("capitulacion", e["lado"]) if (e["rapido"] >= 2.0 and e["dz"] >= 2.0) else "resto")
    tabla("llegada rapida + delta grande + a favor del zero:", lambda e: "capitulacion a favor del zero" if (e["rapido"] >= 2.0 and e["dz"] >= 2.0 and a_favor(e, "sobre_zero")) else "resto")
    tabla("REGLA A = a favor del zero Y de la apertura:", lambda e: "REGLA A" if (a_favor(e, "sobre_zero") and a_favor(e, "sobre_apertura")) else "resto")
    tabla("REGLA A por franja:", lambda e: ("REGLA A", e["franja"]) if (a_favor(e, "sobre_zero") and a_favor(e, "sobre_apertura")) else "resto")
    tabla("REGLA A por tipo:", lambda e: ("REGLA A", e["tipo"]) if (a_favor(e, "sobre_zero") and a_favor(e, "sobre_apertura")) else "resto")
    tabla("REGLA B = a favor del zero, apertura y momentum 30:", lambda e: "REGLA B" if (a_favor(e, "sobre_zero") and a_favor(e, "sobre_apertura") and a_favor(e, "mom30")) else "resto")
    pd = defaultdict(lambda: [0, 0, 0, 0])
    for e in real:
        if a_favor(e, "sobre_zero") and a_favor(e, "sobre_apertura"):
            pd[e["dia"]][1] += 1; pd[e["dia"]][0] += 1 if e["res"][(20, 20)] else 0
            pd[e["dia"]][3] += 1; pd[e["dia"]][2] += 1 if e["res"][(15, 15)] else 0
    print("  REGLA A por dia (+20/-20 aciertos/disparos): " + " ".join("%s %d/%d" % (d[5:], v[0], v[1]) for d, v in sorted(pd.items())))
    print("  REGLA A por dia (+15/-15 aciertos/disparos): " + " ".join("%s %d/%d" % (d[5:], v[2], v[3]) for d, v in sorted(pd.items())))


if __name__ == "__main__":
    main()
