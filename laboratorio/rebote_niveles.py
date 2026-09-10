# -*- coding: utf-8 -*-
"""REBOTES EN LOS NIVELES DE GAMMA: ¿cuantas veces por sesion el precio llega a una dominante, al
zero gamma o a un major, lo toca (o lo traspasa un poco) y REVIERTE? ¿En que temporalidad se ve
antes o acierta mas? ¿En que franja horaria se repite? ¿Con que filtro de order flow?

Pedido del operador (2026-09-10): "mira cuantas veces se acerca por sesion, las toca y rebota o lo
pasa un poco pero termina rebotando; que temporalidad ayuda; la franja horaria; un par de
señales por sesion, no 30". Esto lo cuenta todo, con placebo.

DEFINICIONES (fijadas antes de mirar):
  - Nivel: dominante de arriba / de abajo del precio, zero gamma (volumen), major + / major -.
  - Llegada: la vela de hace 5 (M1) / 3 (M2) / 2 (M5) cerraba a mas de LEJOS puntos del nivel
    del lado de la LLEGADA; la vela actual toca la banda del nivel (+- TOL) y la anterior no.
  - Traspaso: la penetracion maxima del otro lado en las 3 velas siguientes: 0 = no traspasa;
    chico <= P1; medio <= P2; mas = ruptura (no se juzga como toque).
  - Rebote (la medida): desde el CIERRE de la vela del toque, el precio se aleja REB puntos hacia
    el lado de la llegada antes de irse REB del otro lado, en HORIZ minutos. Vela que toca los dos
    = no se sabe (no cuenta). Regla simetrica: la moneda da 50 %.
  - Filtros: delta de la vela del toque EN CONTRA de la llegada (rechazo) / a favor; cuadrante.
  - Placebo: el mismo conteo con el nivel corrido +-X (varios X). Ventaja = rebote real - placebo.
Uso: python laboratorio/rebote_niveles.py MNQ M1 [--reb 20] [--tol 8] [--horiz 20]
"""
import io
import json
import os
import statistics as st
import sys
from collections import defaultdict
from datetime import datetime

ATAS = os.path.join(os.environ.get("APPDATA", ""), "ATAS")
RECLAMO = "--reclamo" in sys.argv
NIVELES = ("dom_arr", "dom_aba", "zero", "mp", "mn")


def cargar(inst, marco):
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
        d = ult[t]; n = d["niv"]; c = d["c"]
        doms = [x for x in (n.get("dom0"), n.get("dom1")) if x]
        arr = [x for x in doms if x > c]; aba = [x for x in doms if x <= c]
        vs.append(dict(t=datetime.strptime(t[:19], "%Y-%m-%dT%H:%M:%S"), dia=t[:10], o=d["o"], h=d["h"], l=d["l"], c=c,
                       delta=float(d.get("delta") or 0), q=n.get("q_cuadrante"),
                       dom_arr=min(arr) if arr else None, dom_aba=max(aba) if aba else None,
                       zero=n.get("zero_vol") or n.get("zero_oi"), mp=n.get("mp_vol") or n.get("mp_oi"), mn=n.get("mn_vol") or n.get("mn_oi")))
    return vs


def franja(v):
    m = (v["t"].hour - 4) * 60 + v["t"].minute
    return "09:30-10:30" if m < 630 else "10:30-12:00" if m < 720 else "12:00-14:00" if m < 840 else "14:00-15:00" if m < 900 else "15:00-16:00"


def toques(vs, minutos_por_vela, tol, lejos, p1, p2, reb, horiz_min, atras, corr=0.0):
    out = []
    horiz = max(1, int(round(horiz_min / minutos_por_vela)))
    for i in range(atras + 1, len(vs)):
        v, p = vs[i], vs[i - 1]
        if v["dia"] != vs[i - atras]["dia"]:
            continue
        for tipo in NIVELES:
            L = v.get(tipo); Lp = p.get(tipo)
            if not L or not Lp:
                continue
            L, Lp = L + corr, Lp + corr
            toca = v["l"] <= L + tol and v["h"] >= L - tol
            tocaba = p["l"] <= Lp + tol and p["h"] >= Lp - tol
            if not toca or tocaba:
                continue
            c0 = vs[i - atras]["c"]
            if abs(c0 - L) < lejos:
                continue                              # no venia de lejos
            lado = 1 if c0 > L else -1                # +1: llega desde ARRIBA (el nivel es soporte)
            # traspaso en las 3 velas siguientes (incluida la del toque)
            pen = 0.0
            for j in range(i, min(len(vs), i + 3)):
                if vs[j]["dia"] != v["dia"]: break
                pen = max(pen, (L - vs[j]["l"]) if lado > 0 else (vs[j]["h"] - L))
            if pen > p2:
                cat = "ruptura"
            elif pen > p1:
                cat = "traspaso medio"
            elif pen > 0:
                cat = "traspaso chico"
            else:
                cat = "sin traspaso"
            # rebote desde el cierre del toque: REB hacia el lado de la llegada antes que REB en contra.
            # Con RECLAMO: solo toques con traspaso (chico o medio), y la entrada es el cierre de la
            # primera vela (hasta 5 despues) que vuelve a cerrar del lado de la llegada: la "falsa ruptura".
            ent = v["c"]; res = None; i_ent = i; delta_rec = v["delta"]
            if RECLAMO:
                if cat not in ("traspaso chico", "traspaso medio"):
                    continue
                i_ent = None
                for j in range(i + 1, min(len(vs), i + 6)):
                    if vs[j]["dia"] != v["dia"]: break
                    if (vs[j]["c"] > L) if lado > 0 else (vs[j]["c"] < L):
                        i_ent = j; break
                if i_ent is None:
                    continue
                ent = vs[i_ent]["c"]; delta_rec = vs[i_ent]["delta"]
            for j in range(i_ent + 1, min(len(vs), i_ent + 1 + horiz)):
                w = vs[j]
                if w["dia"] != v["dia"]: break
                gano = (w["h"] >= ent + reb) if lado > 0 else (w["l"] <= ent - reb)
                perdio = (w["l"] <= ent - reb) if lado > 0 else (w["h"] >= ent + reb)
                if gano and perdio: res = None; break
                if gano: res = True; break
                if perdio: res = False; break
            if res is None:
                continue
            delta_contra = (delta_rec > 0) if lado > 0 else (delta_rec < 0)   # el delta empuja hacia la llegada = rechazo
            out.append(dict(tipo=tipo, i=i, lado=lado, cat=cat, rebote=res, franja=franja(v), dia=v["dia"], delta_contra=delta_contra, q=v["q"]))
    return out


def main():
    a = [x for x in sys.argv[1:] if not x.startswith("--")]
    inst = a[0] if a else "MNQ"; marco = a[1] if len(a) > 1 else "M1"
    arg = lambda k, d: type(d)(sys.argv[sys.argv.index(k) + 1]) if k in sys.argv else d
    es_nq = inst.startswith("MNQ") or inst.startswith("NQ")
    mpv = {"M1": 1, "M2": 2, "M5": 5}[marco]
    reb = arg("--reb", 20.0 if es_nq else 5.0); tol = arg("--tol", 8.0 if es_nq else 2.0); horiz_min = arg("--horiz", 20)
    lejos = arg("--lejos", 30.0 if es_nq else 7.0); p1 = arg("--p1", 8.0 if es_nq else 2.0); p2 = arg("--p2", 20.0 if es_nq else 5.0)
    atras = {"M1": 5, "M2": 3, "M5": 2}[marco]
    placebos = (-140, -80, 80, 140) if es_nq else (-35, -20, 20, 35)
    vs = cargar(inst, marco)
    dias = sorted(set(v["dia"] for v in vs))
    if RECLAMO: print("  MODO RECLAMO: solo toques que traspasan (chico/medio); entrada al cierre de la primera vela que vuelve al lado de la llegada")
    print("%s %s: %d velas, %d dias | toque +-%g, llegada desde >= %g pts hace %d velas, traspaso chico <= %g, medio <= %g, rebote %g pts en %d min" % (
        inst, marco, len(vs), len(dias), tol, lejos, atras, p1, p2, reb, horiz_min))
    real = [t for t in toques(vs, mpv, tol, lejos, p1, p2, reb, horiz_min, atras) if t["cat"] != "ruptura"]
    plc = []
    for c in placebos:
        plc += [t for t in toques(vs, mpv, tol, lejos, p1, p2, reb, horiz_min, atras, c) if t["cat"] != "ruptura"]
    nd = float(len(dias))
    print("  toques reales (sin rupturas): %d = %.1f por dia | rebote %.1f %% | placebo %d, %.1f %%" % (
        len(real), len(real) / nd, 100.0 * sum(t["rebote"] for t in real) / max(1, len(real)), len(plc), 100.0 * sum(t["rebote"] for t in plc) / max(1, len(plc))))

    def tabla(titulo, clave, minimo=15):
        print("  " + titulo)
        g = defaultdict(list); gp = defaultdict(list)
        for t in real: g[clave(t)].append(t["rebote"])
        for t in plc: gp[clave(t)].append(t["rebote"])
        filas = []
        for k in g:
            r = g[k]; p = gp.get(k, [])
            if len(r) < minimo: continue
            pr = 100.0 * sum(r) / len(r); pp = 100.0 * sum(p) / len(p) if p else float("nan")
            filas.append((k, len(r), pr, len(p), pp))
        for (k, n, pr, npl, pp) in sorted(filas, key=lambda f: -(f[2] - f[4] if f[4] == f[4] else -99)):
            print("    %-42s %4d toques (%.1f/dia) rebota %5.1f %% | placebo %4d %5.1f %% | ventaja %+5.1f pp%s" % (
                str(k), n, n / nd, pr, npl, pp, pr - pp, "  <-- candidata" if (pr - pp >= 10 and pr >= 60 and n >= 25) else ""))
        if not filas: print("    (sin grupos con %d+ toques)" % minimo)
    tabla("POR NIVEL:", lambda t: t["tipo"])
    tabla("POR TRASPASO:", lambda t: t["cat"])
    tabla("POR FRANJA (NY):", lambda t: t["franja"])
    tabla("POR DELTA DE LA VELA DEL TOQUE:", lambda t: "delta en contra de la llegada (rechazo)" if t["delta_contra"] else "delta a favor de la llegada")
    tabla("POR CUADRANTE:", lambda t: {1: "IMAN", 2: "EXPLOSIVO", 3: "ESTABLE", 4: "RIESGO"}.get(t["q"], "?"))
    tabla("NIVEL x TRASPASO:", lambda t: (t["tipo"], t["cat"]))
    tabla("NIVEL x FRANJA:", lambda t: (t["tipo"], t["franja"]))
    tabla("NIVEL x DELTA:", lambda t: (t["tipo"], "rechazo" if t["delta_contra"] else "a favor"))
    tabla("TRASPASO x DELTA:", lambda t: (t["cat"], "rechazo" if t["delta_contra"] else "a favor"))
    tabla("FRANJA x DELTA:", lambda t: (t["franja"], "rechazo" if t["delta_contra"] else "a favor"))
    tabla("NIVEL x FRANJA x DELTA (rechazo):", lambda t: (t["tipo"], t["franja"]) if t["delta_contra"] else "a favor", 12)
    print()
    print("COMO LEERLO: 'candidata' = rebota 60 %+ con 10 pp+ sobre el placebo y 25+ toques. Lo que no le gana al nivel corrido")
    print("no es el nivel: es como se mueve el precio en general (velas grandes tocan mas rayas).")


if __name__ == "__main__":
    main()
