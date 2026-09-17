# -*- coding: utf-8 -*-
"""
cvd_superador.py — banco de pruebas de un CVD "superador" con las velas que ya graba Gamma Hoy
(pythiagex-centinela-hoy-<INST>-TimeFrame-<M>.jsonl: o/h/l/c/vol/delta + of{dmax,dmin,big_buy,big_sell}).

Dos preguntas, medidas y contra placebo (regla del proyecto):
  1. LEGIBILIDAD: cuanto del alto del panel usa el movimiento reciente con el CVD acumulado del dia contra
     un CVD anclado/ventana (el mismo dato, otra escala).
  2. RETRASO: en los giros de precio (pivotes), cuantas velas antes o despues gira cada candidato:
        cvd        el CVD clasico (su propio pivote)
        rapido     presion = EMA3(delta) - EMA10(delta)  (tipo MACD del delta; cruza cero)
        z          z-score de la suma de delta de 5 velas contra 60
        mecha      rechazo intravela: delta cierra lejos de su extremo (dmax/dmin)
        absorcion  mucho delta sin avance de precio (esfuerzo sin resultado)
     y que pasa despues de cada señal (retorno a N velas) contra la misma señal puesta al azar.

Uso:  python laboratorio/cvd_superador.py [MNQ] [M2]
"""
import json, math, os, random, sys, collections, statistics as st

A = os.path.join(os.environ.get("APPDATA", ""), "ATAS")


def cargar(inst, marco):
    vs = []
    for nombre in ("pythiagex-centinela-hoy-%s-TimeFrame-%s.jsonl" % (inst, marco), "pythiagex-centinela-hoy-%sZ6-TimeFrame-%s.jsonl" % (inst, marco)):
        p = os.path.join(A, nombre)
        if not os.path.exists(p): continue
        for l in open(p, encoding="utf-8", errors="replace"):
            try: j = json.loads(l)
            except Exception: continue
            if j.get("delta") is None or not j.get("vol"): continue
            of = j.get("of") or {}
            vs.append(dict(t=j["t"], o=j["o"], h=j["h"], l=j["l"], c=j["c"], vol=j["vol"], d=j["delta"],
                           dmax=of.get("dmax", j["delta"]), dmin=of.get("dmin", j["delta"]),
                           bb=of.get("big_buy", 0) or 0, bs=of.get("big_sell", 0) or 0))
    vs.sort(key=lambda v: v["t"])
    # sin duplicados por hora
    out, visto = [], set()
    for v in vs:
        if v["t"] in visto: continue
        visto.add(v["t"]); out.append(v)
    return out


def en_rueda(v):
    h = int(v["t"][11:13]) * 60 + int(v["t"][14:16])
    return 13 * 60 + 30 <= h <= 20 * 60   # UTC, 9:30-16:00 NY


def ema(xs, n):
    k = 2.0 / (n + 1); e = xs[0]; out = []
    for x in xs:
        e = e + k * (x - e); out.append(e)
    return out


def candidatos(vs):
    d = [v["d"] for v in vs]
    cvd, s = [], 0
    dia = None
    for v in vs:
        if v["t"][:10] != dia: dia = v["t"][:10]; s = 0
        s += v["d"]; cvd.append(s)
    rap = [a - b for a, b in zip(ema(d, 3), ema(d, 10))]
    z = []
    for i in range(len(vs)):
        w = d[max(0, i - 59):i + 1]; s5 = sum(d[max(0, i - 4):i + 1])
        sd = st.pstdev(w) if len(w) > 5 else 0
        z.append(s5 / (sd * math.sqrt(5)) if sd > 0 else 0)
    return cvd, rap, z


def pivotes(vs, k=5, minimo=0.0008):
    """Pivotes de precio: minimo/maximo de +-k velas con un giro posterior de al menos 'minimo' (fraccion del precio)."""
    out = []
    for i in range(k, len(vs) - k):
        lo = min(v["l"] for v in vs[i - k:i + k + 1]); hi = max(v["h"] for v in vs[i - k:i + k + 1])
        if vs[i]["l"] == lo and max(v["h"] for v in vs[i:i + k + 1]) - lo >= minimo * lo: out.append((i, +1))
        if vs[i]["h"] == hi and hi - min(v["l"] for v in vs[i:i + k + 1]) >= minimo * hi: out.append((i, -1))
    return out


def giro_de(serie, i, sentido, atras=6, adelante=8):
    """En que vela (relativa al pivote de precio i) la serie da su giro en ese sentido: primer j en [-atras, +adelante]
    donde la serie deja de caer y sube (sentido +1) o al reves. None si no gira en la ventana."""
    for j in range(-atras, adelante + 1):
        a, b, c = i + j - 1, i + j, i + j + 1
        if a < 0 or c >= len(serie): continue
        if sentido > 0 and serie[b] <= serie[a] and serie[c] > serie[b]: return j + 1   # se ve al cerrar la vela c
        if sentido < 0 and serie[b] >= serie[a] and serie[c] < serie[b]: return j + 1
    return None


def cruce_cero(serie, i, sentido, atras=6, adelante=8):
    for j in range(-atras, adelante + 1):
        b, a = i + j, i + j - 1
        if a < 0 or b >= len(serie): continue
        if sentido > 0 and serie[a] <= 0 < serie[b]: return j
        if sentido < 0 and serie[a] >= 0 > serie[b]: return j
    return None


def resumen(nombre, xs):
    xs = [x for x in xs if x is not None]
    if not xs: print("  %-10s sin casos" % nombre); return
    xs.sort()
    print("  %-10s n %4d | mediana %+d velas | p25 %+d | p75 %+d | antes o en el pivote: %2.0f %%" % (
        nombre, len(xs), xs[len(xs) // 2], xs[len(xs) // 4], xs[3 * len(xs) // 4], 100.0 * sum(1 for x in xs if x <= 0) / len(xs)))


def main():
    inst = sys.argv[1] if len(sys.argv) > 1 else "MNQ"
    marco = sys.argv[2] if len(sys.argv) > 2 else "M2"
    vs = [v for v in cargar(inst, marco) if en_rueda(v)]
    dias = sorted(set(v["t"][:10] for v in vs))
    print("%s %s: %d velas de rueda en %d dias (%s .. %s)" % (inst, marco, len(vs), len(dias), dias[0], dias[-1]))
    cvd, rap, z = candidatos(vs)

    # 1) legibilidad: que fraccion del rango del panel ocupa la ultima hora
    paso = 30 if marco == "M2" else 60
    fr_dia, fr_ancla = [], []
    for dia in dias:
        idx = [i for i, v in enumerate(vs) if v["t"][:10] == dia]
        if len(idx) < paso * 2: continue
        for fin in range(idx[0] + paso * 2, idx[-1], paso):
            todo = cvd[idx[0]:fin + 1]; ult = cvd[fin - paso:fin + 1]
            r_todo = max(todo) - min(todo); r_ult = max(ult) - min(ult)
            if r_todo > 0: fr_dia.append(r_ult / r_todo)
            fr_ancla.append(1.0)
    print("\n1) LEGIBILIDAD: la ultima hora ocupa el %.0f %% del alto del panel con el CVD del dia entero (mediana; p25 %.0f %%)."
          % (100 * st.median(fr_dia), 100 * sorted(fr_dia)[len(fr_dia) // 4]))
    print("   Con el CVD anclado a la ultima hora (mismo dato, otra escala) ocupa el 100 %%: %.1fx mas alto de lectura." % (1 / st.median(fr_dia)))

    # 2) QUIEN LE GANA DE MANO A QUIEN: correlacion entre el delta de una vela y el retorno de las velas vecinas.
    #    (La version anterior media "giros cerca de pivotes" y era un artefacto: con los deltas barajados daba lo mismo.)
    print()
    print("2) DELTA DE UNA VELA contra el retorno de las velas vecinas (correlacion; +k = k velas DESPUES del delta):")
    def corr(xs, ys):
        n = len(xs); mx, my = sum(xs) / n, sum(ys) / n
        sx = math.sqrt(sum((x - mx) ** 2 for x in xs)); sy = math.sqrt(sum((y - my) ** 2 for y in ys))
        return sum((x - mx) * (y - my) for x, y in zip(xs, ys)) / (sx * sy) if sx > 0 and sy > 0 else 0
    for k in (-2, -1, 0, 1, 2, 3):
        xs, ys = [], []
        for i in range(3, len(vs) - 4):
            j = i + k
            if vs[j]["t"][:10] != vs[i]["t"][:10]: continue
            xs.append(vs[i]["d"]); ys.append(vs[j]["c"] - vs[j]["o"])
        print("   vela %+d: %+.2f  (n %d)" % (k, corr(xs, ys), len(xs)))

    # 3) que pasa despues de cada señal contra la misma cantidad de señales al azar
    print("\n3) DESPUES DE LA SEÑAL (retorno a 5 velas en el sentido de la señal, en % del precio x 100 = pb), contra azar:")
    def retorno(i, s, n=5):
        if i + n >= len(vs) or vs[i + n]["t"][:10] != vs[i]["t"][:10]: return None
        return s * (vs[i + n]["c"] - vs[i]["c"]) / vs[i]["c"] * 1e4
    senales = collections.OrderedDict()
    senales["rapido cruza 0"] = [(i, 1 if rap[i] > 0 else -1) for i in range(1, len(vs)) if (rap[i - 1] <= 0 < rap[i]) or (rap[i - 1] >= 0 > rap[i])]
    senales["z pasa +-1"] = [(i, 1 if z[i] > 0 else -1) for i in range(1, len(vs)) if (abs(z[i - 1]) < 1 <= abs(z[i]))]
    rng = [v["h"] - v["l"] for v in vs]
    med_rng = st.median(rng); dabs = sorted(abs(v["d"]) for v in vs); p80 = dabs[int(0.8 * len(dabs))]
    senales["absorcion (contra el delta)"] = [(i, -1 if vs[i]["d"] > 0 else 1) for i in range(len(vs)) if abs(vs[i]["d"]) >= p80 and (vs[i]["c"] - vs[i]["o"]) * vs[i]["d"] <= 0 and rng[i] <= med_rng * 1.2]
    senales["mecha de delta (rechazo)"] = [(i, -1 if vs[i]["dmax"] - vs[i]["d"] > vs[i]["d"] - vs[i]["dmin"] else 1) for i in range(len(vs))
                                           if (vs[i]["dmax"] - vs[i]["dmin"]) >= p80 and min(vs[i]["dmax"] - vs[i]["d"], vs[i]["d"] - vs[i]["dmin"]) <= 0.15 * (vs[i]["dmax"] - vs[i]["dmin"]) * 0 + 0.0
                                           or False]
    # mecha bien definida: el delta toco un extremo y cerro devolviendo mas del 60 % del recorrido
    mech = []
    for i, v in enumerate(vs):
        rec = v["dmax"] - v["dmin"]
        if rec < p80: continue
        if v["dmax"] > 0 and (v["dmax"] - v["d"]) >= 0.6 * rec: mech.append((i, -1))
        elif v["dmin"] < 0 and (v["d"] - v["dmin"]) >= 0.6 * rec: mech.append((i, +1))
    senales["mecha de delta (rechazo)"] = mech
    random.seed(7)
    for nombre, ss in senales.items():
        rs = [retorno(i, s) for i, s in ss]; rs = [r for r in rs if r is not None]
        if len(rs) < 20: print("  %-28s n %d: muestra corta" % (nombre, len(rs))); continue
        gana = 100.0 * sum(1 for r in rs if r > 0) / len(rs)
        # placebo: mismas horas del dia no, mismo numero de señales, sentido al azar, 200 repeticiones
        plac = []
        for _ in range(200):
            m = []
            for _i in range(len(rs)):
                i = random.randrange(len(vs) - 6); s = random.choice((1, -1)); r = retorno(i, s)
                if r is not None: m.append(r)
            plac.append(sum(m) / len(m))
        media = sum(rs) / len(rs); pm = sum(plac) / len(plac); ps = st.pstdev(plac)
        print("  %-28s n %4d | acierta %4.1f %% | media %+5.2f pb | azar %+5.2f +- %4.2f pb | z %+4.1f" % (nombre, len(rs), gana, media, pm, ps, (media - pm) / ps if ps > 0 else 0))


if __name__ == "__main__":
    main()
