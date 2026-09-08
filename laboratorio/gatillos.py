# -*- coding: utf-8 -*-
"""GATILLOS DE ORDER FLOW EN LOS EXTREMOS DE LAS BANDAS DOMINANTES.

Pedido del operador (2026-09-08): un detector (delta inusual, prints grandes,
tren de deltas, absorcion, "ballena") que se dispare SOLO en los extremos de
las bandas dominantes y que despues el precio le de la razon, sin llenar el
grafico de humo. Antes de dibujar nada, esto lo mide sobre lo que el propio
indicador ya anoto en ATAS: una linea por vela de 1 minuto con O/H/L/C,
volumen, cantidad de operaciones (ops), delta y las dominantes que regian en
ese minuto (dom0 arriba/abajo, dom1), en precio del futuro.

EL EVENTO: el precio entra por primera vez en la banda de una dominante (la
franja hacia adentro, 0,08 % del precio, ~6 pts en ES) viniendo de mas lejos.
EL GATILLO: en la vela de entrada o en las 2 siguientes, una condicion de
order flow (cada hipotesis define la suya y el lado que implica).
EL DESENLACE (simetrico, no admite interpretacion): desde el cierre de la
vela del gatillo, ¿el precio llego G puntos a favor ANTES que G en contra,
dentro de H minutos? Si una vela toca los dos, no se sabe y no cuenta.
EL PLACEBO: la misma dominante corrida a otro strike (+-25/35/50 pts) con la
MISMA condicion de order flow. Si el gatillo real no le gana al placebo, la
banda no aporta: lo que se ve es el order flow solo (o nada).
EL CONTROL: la misma condicion de order flow en CUALQUIER vela (sin banda).

Uso:  python laboratorio/gatillos.py [MES] [M1] [--prefijo rebobinado-atas-]
        [--g 6] [--h 30] [--z 1.5] [--banda 0.08] [--todo]
"""
import io
import json
import math
import os
import statistics as st
import sys
from datetime import datetime

ATAS = os.path.join(os.environ.get("APPDATA", ""), "ATAS")


def t_de(s):
    return datetime.strptime(s[:19], "%Y-%m-%dT%H:%M:%S")


def wilson(exitos, total, z=1.96):
    if total == 0:
        return (0.0, 0.0, 1.0)
    p = exitos / float(total)
    d = 1 + z * z / total
    c = (p + z * z / (2 * total)) / d
    m = z * math.sqrt(p * (1 - p) / total + z * z / (4 * total * total)) / d
    return (p, max(0.0, c - m), min(1.0, c + m))


def cargar(prefijo, inst, marco):
    p = os.path.join(ATAS, "pythiagex-centinela-%s%s-TimeFrame-%s.jsonl" % (prefijo, inst, marco))
    por_t = {}
    if not os.path.exists(p):
        return [], p
    for l in io.open(p, encoding="utf-8", errors="replace"):
        try:
            d = json.loads(l)
        except Exception:
            continue
        if not d.get("niv") or d.get("vol", 0) <= 0:
            continue
        por_t[d["t"]] = d        # la ultima corrida pisa a las anteriores
    vs = []
    for t, d in sorted(por_t.items()):
        n = d["niv"]
        vs.append(dict(t=t_de(t), o=d["o"], h=d["h"], l=d["l"], c=d["c"], vol=float(d.get("vol", 0)),
                       ops=float(d.get("ops", 0) or 0), delta=float(d.get("delta", 0) or 0),
                       doms=[x for x in (n.get("dom0"), n.get("dom1")) if x], q=n.get("q_cuadrante")))
    return vs, p


# ------------------------------------------------------------------ rasgos de order flow
def rasgos(vs, ventana=60):
    """Por vela: z del delta, z del volumen, z del tamano medio por operacion,
    rango relativo, tren de deltas, delta acumulado 3 velas."""
    for i, v in enumerate(vs):
        j0 = max(0, i - ventana)
        prev = [x for x in vs[j0:i] if (v["t"] - x["t"]).total_seconds() <= ventana * 60 * 3]
        v["rango"] = v["h"] - v["l"]
        v["tam"] = v["vol"] / v["ops"] if v["ops"] > 0 else 0.0
        if len(prev) < 20:
            v["dz"] = v["vz"] = v["sz"] = 0.0
            v["rango_rel"] = 1.0
        else:
            ds = [x["delta"] for x in prev]
            sd = st.pstdev(ds) or 1.0
            v["dz"] = v["delta"] / sd
            vol = [x["vol"] for x in prev]
            mv, sv = st.mean(vol), (st.pstdev(vol) or 1.0)
            v["vz"] = (v["vol"] - mv) / sv
            tams = [x["vol"] / x["ops"] for x in prev if x["ops"] > 0]
            if len(tams) >= 10:
                mt, stt = st.mean(tams), (st.pstdev(tams) or 1e-9)
                v["sz"] = (v["tam"] - mt) / stt
            else:
                v["sz"] = 0.0
            mr = st.median(x["h"] - x["l"] for x in prev) or 1e-9
            v["rango_rel"] = v["rango"] / mr
        d3 = [vs[k]["delta"] for k in range(max(0, i - 2), i + 1)]
        v["cum3"] = sum(d3)
        v["tren"] = len(d3) == 3 and (all(x > 0 for x in d3) or all(x < 0 for x in d3))
        h5 = max((vs[k]["h"] for k in range(max(0, i - 5), i)), default=v["h"])
        l5 = min((vs[k]["l"] for k in range(max(0, i - 5), i)), default=v["l"])
        v["nuevo_alto"] = v["h"] > h5
        v["nuevo_bajo"] = v["l"] < l5


# ------------------------------------------------------------------ desenlace
def desenlace(vs, i, precio, lado, g, h_min, gc=None):
    """True: llego g a favor antes que gc en contra (gc = g si no se da: simetrica);
    False: al reves; None: no se sabe."""
    if gc is None:
        gc = g
    meta, stop = precio + lado * g, precio - lado * gc
    for j in range(i + 1, len(vs)):
        dt = (vs[j]["t"] - vs[i]["t"]).total_seconds() / 60.0
        if dt > h_min:
            break
        if (vs[j]["t"] - vs[j - 1]["t"]).total_seconds() > 20 * 60:
            return None          # hueco: ATAS cerrado
        v = vs[j]
        gano = v["h"] >= meta if lado > 0 else v["l"] <= meta
        perdio = v["l"] <= stop if lado > 0 else v["h"] >= stop
        if gano and perdio:
            return None
        if gano:
            return True
        if perdio:
            return False
    return None


# ------------------------------------------------------------------ hipotesis
def hipotesis(z):
    """Cada una: (nombre, funcion(vela, arriba) -> lado o 0). arriba = la
    dominante esta por encima del precio (el precio sube hacia ella)."""
    rech = lambda arriba: -1 if arriba else 1      # el lado del rechazo
    cont = lambda arriba: 1 if arriba else -1      # el lado de la continuacion
    sg = lambda v: 1 if v["delta"] > 0 else (-1 if v["delta"] < 0 else 0)
    return [
        ("solo la banda (toque, sin order flow) -> rechazo", lambda v, a: rech(a)),
        ("delta fuerte EN CONTRA de la llegada (z<=-%.1f) -> rechazo" % z,
         lambda v, a: rech(a) if (v["dz"] <= -z if a else v["dz"] >= z) else 0),
        ("delta fuerte A FAVOR de la llegada (z>=%.1f) -> continuacion" % z,
         lambda v, a: cont(a) if (v["dz"] >= z if a else v["dz"] <= -z) else 0),
        ("absorcion: volumen z>=%.1f y rango chico (<=0.7x) -> rechazo" % z,
         lambda v, a: rech(a) if (v["vz"] >= z and v["rango_rel"] <= 0.7) else 0),
        ("tren: 3 deltas seguidos hacia la banda -> continuacion",
         lambda v, a: cont(a) if (v["tren"] and (v["delta"] > 0) == a) else 0),
        ("tren: 3 deltas seguidos contra la banda -> rechazo",
         lambda v, a: rech(a) if (v["tren"] and (v["delta"] < 0) == a) else 0),
        ("prints grandes: tamano medio z>=%.1f, lado del delta" % z,
         lambda v, a: sg(v) if v["sz"] >= z else 0),
        ("delta inusual |z|>=%.1f, lado del delta (sin mirar la banda)" % z,
         lambda v, a: sg(v) if abs(v["dz"]) >= z else 0),
        ("divergencia: nuevo extremo en la banda con delta contrario -> rechazo",
         lambda v, a: rech(a) if ((v["nuevo_alto"] and v["delta"] < 0) if a else (v["nuevo_bajo"] and v["delta"] > 0)) else 0),
        ("absorcion + delta en contra (z<=-%.1f) -> rechazo" % (z / 2),
         lambda v, a: rech(a) if (v["vz"] >= z and v["rango_rel"] <= 0.8 and (v["dz"] <= -z / 2 if a else v["dz"] >= z / 2)) else 0),
        # compuestas (definidas ANTES de mirar el resultado, 2026-09-08 08:30 UTC):
        ("RECHAZO CONFIRMADO: delta en contra (z<=-1) y (tren contra o divergencia) -> rechazo",
         lambda v, a: rech(a) if ((v["dz"] <= -1.0 if a else v["dz"] >= 1.0) and ((v["tren"] and (v["delta"] < 0) == a) or ((v["nuevo_alto"] and v["delta"] < 0) if a else (v["nuevo_bajo"] and v["delta"] > 0)))) else 0),
        ("rechazo con cierre adentro: delta en contra (z<=-1) y la vela no cierra del otro lado -> rechazo",
         lambda v, a: rech(a) if ((v["dz"] <= -1.0 and v["c"] < v["dom_ev"]) if a else (v["dz"] >= 1.0 and v["c"] > v["dom_ev"])) else 0),
        ("ruptura confirmada: cierra del otro lado con delta a favor (z>=1) -> continuacion",
         lambda v, a: cont(a) if ((v["dz"] >= 1.0 and v["c"] > v["dom_ev"]) if a else (v["dz"] <= -1.0 and v["c"] < v["dom_ev"])) else 0),
    ]


# ------------------------------------------------------------------ eventos
def eventos(vs, banda_pct, corr=0.0, lejos_mult=2.5, espera_min=30, quieta=None):
    """Primeras entradas del precio en la banda de una dominante (corrida
    'corr' puntos para el placebo). Devuelve (i, dom, arriba).
    quieta: si se da, la dominante tiene que haberse movido <= quieta puntos
    en las 5 velas previas (el precio fue a la banda, no la banda al precio)."""
    # La dominante se mueve minuto a minuto (centroide): se sigue POR LADO (la de
    # arriba y la de abajo del precio), no por valor. Estado por lado: "estuve
    # lejos" -> primera vela que entra en la banda de la dominante de ese lado.
    out = []
    lejos_desde = {True: None, False: None}     # arriba / abajo: hora en que estuvo lejos
    dentro_ya = {True: False, False: False}
    historia = {True: [], False: []}            # (t, d) de las ultimas velas por lado
    rt = st.median(v["h"] - v["l"] for v in vs) or 0.25
    # el paso del marco (60 s en M1, 300 en M5): "5 velas atras" sin hueco = 5 pasos (con tolerancia)
    pasos = sorted((vs[i]["t"] - vs[i - 1]["t"]).total_seconds() for i in range(1, min(len(vs), 400)))
    paso = (pasos[len(pasos) // 2] if pasos else 60.0) or 60.0
    for i, v in enumerate(vs):
        semi = v["c"] * banda_pct / 100.0
        lejos = lejos_mult * rt + semi
        ds = [d + corr for d in v["doms"]]
        arr = [d for d in ds if d > v["c"]]
        aba = [d for d in ds if d <= v["c"]]
        for arriba, d in ((True, min(arr) if arr else None), (False, max(aba) if aba else None)):
            if d is None:
                dentro_ya[arriba] = False
                continue
            hh = historia[arriba]
            hh.append((v["t"], d))
            if len(hh) > 6:
                hh.pop(0)
            if quieta is not None:
                # la dominante de este lado, 5 velas atras (y sin hueco en el medio)
                if len(hh) < 6 or (hh[-1][0] - hh[0][0]).total_seconds() > 5 * paso * 1.6 or abs(hh[-1][1] - hh[0][1]) > quieta:
                    dentro_ya[arriba] = (v["h"] >= d - semi) if arriba else (v["l"] <= d + semi)
                    if abs(v["c"] - d) > lejos:
                        lejos_desde[arriba] = v["t"]
                    continue
            dist = abs(v["c"] - d)
            if dist > lejos:
                lejos_desde[arriba] = v["t"]
                dentro_ya[arriba] = False
                continue
            dentro = (v["h"] >= d - semi) if arriba else (v["l"] <= d + semi)
            if not dentro:
                dentro_ya[arriba] = False
                continue
            if dentro_ya[arriba]:
                continue                          # sigue adentro: no es una entrada nueva
            dentro_ya[arriba] = True
            la = lejos_desde[arriba]
            if la is None or (v["t"] - la).total_seconds() > espera_min * 60:
                continue                          # no venia de lejos (o hace demasiado)
            out.append((i, d, arriba))
    return out


OPC = dict(gc=None, quieta=None)     # regla asimetrica y dominante quieta (desde main)


def correr(vs, hip, banda_pct, g, h_min, corr=0.0, velas_gatillo=3):
    """Para cada hipotesis: lista de desenlaces (True/False) de los gatillos
    en las entradas a la banda (real o placebo)."""
    res = {n: [] for n, _ in hip}
    velas_gatillo = OPC.get("velas") or velas_gatillo
    for (i, d, arriba) in eventos(vs, banda_pct, corr, quieta=OPC["quieta"]):
        for n, f in hip:
            for j in range(i, min(len(vs), i + velas_gatillo)):
                v = vs[j]
                semi = v["c"] * banda_pct / 100.0
                # el precio tiene que seguir cerca de la banda cuando dispara
                if abs(v["c"] - d) > 2 * semi:
                    break
                v["dom_ev"] = d
                lado = f(v, arriba)
                if lado:
                    r = desenlace(vs, j, v["c"], lado, g, h_min, OPC["gc"])
                    if r is not None:
                        res[n].append(r)
                    break
    return res


def control_sin_banda(vs, hip, g, h_min, paso=1):
    """La misma condicion de order flow en cualquier vela (arriba/abajo se
    decide por el signo del delta para que 'rechazo' tenga un lado)."""
    res = {n: [] for n, _ in hip}
    for j in range(0, len(vs), paso):
        v = vs[j]
        v["dom_ev"] = v["c"]            # sin banda no hay dominante: las compuestas con cierre no aplican
        for n, f in hip:
            if n.startswith("solo la banda"):
                continue
            a = v["delta"] > 0          # "la dominante arriba" = el precio viene subiendo
            lado = f(v, a)
            if lado:
                r = desenlace(vs, j, v["c"], lado, g, h_min, OPC["gc"])
                if r is not None:
                    res[n].append(r)
    return res


def fila(nombre, casos, placebo, ctrl=None):
    n = len(casos)
    if n == 0:
        return "  %-62s sin casos" % nombre[:62]
    p, lo, hi = wilson(sum(1 for c in casos if c), n)
    s = "  %-62s %4d casos %5.1f%% (%3.0f-%3.0f)" % (nombre[:62], n, p * 100, lo * 100, hi * 100)
    if placebo:
        pp = 100.0 * sum(1 for c in placebo if c) / len(placebo)
        s += " | placebo %4d %5.1f%% | ventaja %+5.1f pp" % (len(placebo), pp, p * 100 - pp)
    if ctrl:
        pc = 100.0 * sum(1 for c in ctrl if c) / len(ctrl)
        s += " | sin banda %5d %5.1f%%" % (len(ctrl), pc)
    if n >= 20:
        if lo > 0.5 and placebo and p * 100 - 100.0 * sum(1 for c in placebo if c) / len(placebo) >= 5:
            s += "  <-- SIRVE (por ahora)"
        elif hi < 0.5:
            s += "  <-- AL REVES"
    else:
        s += "  (pocos)"
    return s


def main():
    a = [x for x in sys.argv[1:] if not x.startswith("--")]
    inst = a[0] if a else "MES"
    marco = a[1] if len(a) > 1 else "M1"
    arg = lambda k, d: type(d)(sys.argv[sys.argv.index(k) + 1]) if k in sys.argv else d
    prefijo = arg("--prefijo", "rebobinado-atas-")
    g, h_min, z, banda = arg("--g", 6.0), arg("--h", 30.0), arg("--z", 1.5), arg("--banda", 0.08)
    OPC["gc"] = arg("--gc", g)                     # puntos en contra (asimetrica: --g 10 --gc 5)
    OPC["quieta"] = arg("--quieta", 0.0) or None    # dominante quieta: max pts de movimiento en 5 velas
    OPC["velas"] = arg("--velas", 3)                # velas desde la entrada en las que puede disparar
    rth = "--rth" in sys.argv                      # solo rueda americana (13:30-20:00 UTC)
    vs, p = cargar(prefijo, inst, marco)
    if len(vs) < 200:
        print("%s: %d velas, poco (%s)" % (inst, len(vs), p)); return
    rasgos(vs)
    if rth:
        vs = [v for v in vs if 13 * 60 + 30 <= v["t"].hour * 60 + v["t"].minute < 20 * 60]
    dias = sorted(set(v["t"].date() for v in vs))
    es_nq = inst.startswith("MNQ") or inst.startswith("NQ")
    placebos = (-140, -100, -60, 60, 100, 140) if es_nq else (-50, -35, -25, 25, 35, 50)
    ev = eventos(vs, banda, quieta=OPC["quieta"])
    print("%s %s (%s): %d velas%s, %d dias (%s a %s), banda %.2f%% (~%.1f pts), regla %g a favor antes que %g en contra en %g min, z=%.1f%s"
          % (inst, marco, prefijo, len(vs), " SOLO RUEDA AMERICANA" if rth else "", len(dias), dias[0], dias[-1], banda, vs[-1]["c"] * banda / 100.0, g, OPC["gc"], h_min, z,
             (", dominante quieta (<= %g pts en 5 velas)" % OPC["quieta"]) if OPC["quieta"] else ""))
    print("entradas a la banda (reales): %d, de las cuales arriba %d y abajo %d" % (len(ev), sum(1 for e in ev if e[2]), sum(1 for e in ev if not e[2])))
    hip = hipotesis(z)
    real = correr(vs, hip, banda, g, h_min)
    plc = {n: [] for n, _ in hip}
    for c in placebos:
        r = correr(vs, hip, banda, g, h_min, c)
        for n in plc:
            plc[n].extend(r[n])
    ctrl = control_sin_banda(vs, hip, g, h_min)
    print()
    print("  hipotesis                                                       casos  acierto (IC 95)  | placebo (banda corrida)  | ventaja | la misma condicion sin banda")
    for n, _ in hip:
        print(fila(n, real[n], plc[n], ctrl.get(n)))
    # mitades: un ganador que solo gana en una mitad es azar
    if "--todo" in sys.argv:
        print()
        print("  MITADES (primera / segunda mitad de los dias): ventaja sobre placebo en cada una")
        m = len(dias) // 2
        for parte, nombre in ((set(dias[:m]), "1ra"), (set(dias[m:]), "2da")):
            sub = [v for v in vs if v["t"].date() in parte]
            rasgos(sub)
            rr = correr(sub, hip, banda, g, h_min)
            pp = {n: [] for n, _ in hip}
            for c in placebos:
                r = correr(sub, hip, banda, g, h_min, c)
                for n in pp:
                    pp[n].extend(r[n])
            for n, _ in hip:
                if len(rr[n]) >= 8 and pp[n]:
                    print("   %s %-60s %3d casos %5.1f%% | placebo %5.1f%% | %+5.1f pp" % (nombre, n[:60], len(rr[n]), 100.0 * sum(rr[n]) / len(rr[n]), 100.0 * sum(pp[n]) / len(pp[n]), 100.0 * sum(rr[n]) / len(rr[n]) - 100.0 * sum(pp[n]) / len(pp[n])))
    print()
    print("COMO LEERLO: una moneda da 50 %. Un gatillo sirve si su intervalo entero queda arriba de 50")
    print("Y le gana al placebo por 5 pp o mas Y lo repite en las dos mitades. Si 'sin banda' da lo")
    print("mismo, la banda no aporta nada: es el order flow solo.")


if __name__ == "__main__":
    main()
