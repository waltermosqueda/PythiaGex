# -*- coding: utf-8 -*-
"""
cvd_coherencia.py — banco de pruebas de las cintas de Flujo Claro con EL CRITERIO DEL OPERADOR (17-09 15:10):
"el heatmap avisa tarde o el color no corresponde con la vela, o aparecen muchos negros huecos; trabajalo hasta que
coincidan bien, tengan logica y muy buen porcentaje".

Tres medidas de lo que el ojo ve, sobre velas de rueda (13:30-20:00 UTC):
  coincide   en las velas CON CUERPO: la celda tiene el color de la vela y se ve (alfa >= 0,35)
  opuesto    la celda muestra el color CONTRARIO al de la vela (lo que el operador llama "no corresponde")
  apagado    la celda esta negra o casi (lo que llama "negros huecos")
  contra     (solo reglas nuevas) celda violeta: la vela fue CONTRA el delta; es un estado con nombre, no un error
  retraso    en los giros de precio, cuantas velas tarda la celda en mostrar el color del giro
Y las medidas honestas, que NO dependen de como se pinte (protocolo: distinguir lo descriptivo de lo predictivo):
  siguiente  con que frecuencia la vela SIGUIENTE tiene el color de la celda (si da ~50 %, la cinta describe, no anticipa)

Fuentes: el volcado del propio indicador (velas-<inst>-<marco>.csv, todas las velas del grafico) o el centinela (jsonl).
Uso:  python laboratorio/cvd_coherencia.py [ruta.csv | MNQ M1] [--ejemplo AAAA-MM-DDTHH:MM]
"""
import csv, json, os, sys, random, statistics as st

A = os.path.join(os.environ.get("APPDATA", ""), "ATAS")
VERDE, ROJO, CONTRA, GRIS, NEGRO = 1, -1, 2, 3, 0
VISIBLE = 0.35


# ------------------------------------------------------------------ datos
def cargar_csv(ruta):
    vs = []
    for r in csv.DictReader(open(ruta, encoding="utf-8")):
        vs.append(dict(t=r["t"], o=float(r["o"]), h=float(r["h"]), l=float(r["l"]), c=float(r["c"]), vol=float(r["vol"]), tk=float(r["ticks"]),
                       d=float(r["delta"]), dmax=float(r["dmax"]), dmin=float(r["dmin"]), big=float(r["big"]), conf=int(r["conf"]),
                       tono=int(r.get("tono") or 0), brillo=float(r.get("brillo") or 0), tonoc=int(r.get("tonoc") or 0), brilloc=float(r.get("brilloc") or 0)))
    return vs


def cargar_centinela(inst, marco):
    mejor = {}
    for nombre in ("pythiagex-centinela-hoy-%s-TimeFrame-%s.jsonl" % (inst, marco), "pythiagex-centinela-hoy-%sZ6-TimeFrame-%s.jsonl" % (inst, marco)):
        p = os.path.join(A, nombre)
        if not os.path.exists(p): continue
        for l in open(p, encoding="utf-8", errors="replace"):
            try: j = json.loads(l)
            except Exception: continue
            if j.get("delta") is None or not j.get("vol"): continue
            of = j.get("of") or {}
            v = dict(t=j["t"], o=j["o"], h=j["h"], l=j["l"], c=j["c"], vol=j["vol"], tk=j.get("ops") or 0, d=j["delta"],
                     dmax=of.get("dmax", j["delta"]), dmin=of.get("dmin", j["delta"]), big=(of.get("big_buy") or 0) - (of.get("big_sell") or 0))
            if v["t"] not in mejor or v["vol"] > mejor[v["t"]]["vol"]: mejor[v["t"]] = v
    return [mejor[t] for t in sorted(mejor)]


def de_rueda(v): return "13:30" <= v["t"][11:16] < "20:00"


# ------------------------------------------------------------------ piezas sin mirar adelante
def rango_movil(vals, k, n=60):
    """Lugar de |vals[k]| entre los |valores| de las ultimas n velas (0 = el mas chico, 1 = el mas grande). Robusto a rafagas."""
    a = abs(vals[k]); w = vals[max(0, k - n + 1):k + 1]
    if len(w) < 10: return 0.5
    return sum(1 for x in w if abs(x) <= a) / float(len(w)) - 0.5 / len(w)


def sd_movil(d, k, n=60):
    w = d[max(0, k - n + 1):k + 1]
    return st.pstdev(w) if len(w) > 5 else 0.0


def cierre_delta(v):
    rec = v["dmax"] - v["dmin"]
    return (v["d"] - v["dmin"]) / rec * 2 - 1 if rec > 0 else 0


def dir_vela(v, doji):
    cuerpo = v["c"] - v["o"]; rango = v["h"] - v["l"]
    if rango <= 0 or abs(cuerpo) < doji * rango: return 0
    return 1 if cuerpo > 0 else -1


# ------------------------------------------------------------------ reglas de la cinta PRESION: devuelven [(tono, alfa)]
def regla_13(vs):
    """La 1.3 instalada: presion eficaz sostenida, con sus alfas."""
    n = len(vs); d = [v["d"] for v in vs]; out = []; ef = [0] * n; cruda = [0] * n
    for k in range(n):
        v = vs[k]; sd = sd_movil(d, k); z1 = max(-3, min(3, d[k] / sd)) if sd > 0 else 0
        a = sorted(abs(x) for x in d[max(0, k - 59):k + 1]); medabs = a[len(a) // 2]
        w = sorted(abs(vs[j]["c"] - vs[j]["o"]) / abs(d[j]) for j in range(max(0, k - 120), k) if abs(d[j]) >= max(1, medabs))
        beta = w[len(w) // 2] if len(w) >= 20 else 0
        cuerpo = v["c"] - v["o"]; rango = max(1e-9, v["h"] - v["l"]); sg = 1 if z1 > 0.4 else -1 if z1 < -0.4 else 0
        ind = abs(cuerpo) < 0.20 * rango
        e = sg if (sg != 0 and cuerpo * sg > 0 and (beta <= 0 or abs(cuerpo) >= 0.25 * beta * abs(d[k]))) else 0
        act = ef[k - 1] if k > 0 else 0
        if e != 0: act = e
        elif sg != 0 and sg == -act: act = 0
        elif k > 0 and cruda[k - 1] == 0: act = 0
        cruda[k] = e; ef[k] = act
        alfa = 0 if act == 0 else (0.06 if ind else 0.22 if e == 0 else max(0.4, min(1, abs(z1) / 1.8)))
        out.append((act, alfa))
    return out


def regla_delta(vs, piso=0.0):
    """Delta de la vela a secas: tono = signo del delta, brillo = lugar del |delta| en la ultima hora."""
    d = [v["d"] for v in vs]; out = []
    for k in range(len(vs)):
        r = rango_movil(d, k); s = 1 if d[k] > 0 else -1 if d[k] < 0 else 0
        out.append((s if r >= piso else 0, 0.35 + 0.65 * r))
    return out


def regla_vela_flujo(vs, doji=0.15, piso=0.20, contra_desde=0.35):
    """PROPUESTA 1.4: la celda SIEMPRE habla de la vela que tiene encima.
       vela con cuerpo y delta a favor  -> color de la vela, brillo = cuanto delta la respalda (lugar en la ultima hora)
       vela con cuerpo y delta chico    -> color de la vela, a media luz (se movio sin flujo)
       vela con cuerpo y delta EN CONTRA-> violeta 'contra' (el precio le gano al flujo: absorcion / trampa), brillo por tamaño
       vela de indecision (doji)        -> gris (estado con nombre, no un hueco); si el delta fue grande, la fila absorcion lo marca"""
    d = [v["d"] for v in vs]; out = []
    for k in range(len(vs)):
        v = vs[k]; s = dir_vela(v, doji); r = rango_movil(d, k); sd_ = 1 if d[k] > 0 else -1 if d[k] < 0 else 0
        if s == 0: out.append((GRIS, 0.45)); continue
        if sd_ == s or r < piso: out.append((s, 0.40 + 0.60 * r if sd_ == s and r >= piso else 0.40)); continue
        if r >= contra_desde: out.append((CONTRA, 0.45 + 0.55 * r)); continue
        out.append((s, 0.40))
    return out


# ------------------------------------------------------------------ confluencia
def lecturas_conf(vs):
    """Las cuatro lecturas firmadas de cada vela (sin mirar adelante): delta, donde cerro el delta, grandes, CVD de 20 velas."""
    n = len(vs); d = [v["d"] for v in vs]; big = [v.get("big", 0) for v in vs]
    cvd = []; s = 0
    for x in d: s += x; cvd.append(s)
    out = []
    for k in range(n):
        r = rango_movil(d, k); a = (1 if d[k] > 0 else -1) if r >= 0.20 and d[k] != 0 else 0
        cd = cierre_delta(vs[k]); e = 1 if cd > 0.3 else -1 if cd < -0.3 else 0
        w = sorted(abs(x) for x in big[max(0, k - 299):k + 1]); bmax = w[min(len(w) - 1, int(len(w) * 0.95))]
        b = (1 if big[k] > 0.3 * bmax else -1 if big[k] < -0.3 * bmax else 0) if bmax > 0 else 0
        c = (1 if cvd[k] > cvd[k - 20] else -1 if cvd[k] < cvd[k - 20] else 0) if k >= 20 else 0
        out.append((a, e, b, c))
    return out


def conf_13(vs):
    p = regla_13(vs); L = lecturas_conf(vs); out = []
    n = len(vs); d = [v["d"] for v in vs]
    for k in range(n):
        # en la 1.3 la primera lectura es la presion eficaz CRUDA; aca se aproxima con 'tono firme de la regla 1.3 y no sostenido'
        a = p[k][0] if p[k][1] >= 0.4 else 0
        t = a + L[k][1] + L[k][2] + L[k][3]
        out.append(((1 if t > 0 else -1 if t < 0 else 0), abs(t) / 4.0 * (215 / 250.0) + 35 / 250.0 if t != 0 else 0))
    return out


def conf_vela(vs, doji=0.15):
    """PROPUESTA 1.4: cuantas de las 4 lecturas ACOMPAÑAN a la vela (menos las que van en contra).
       neto > 0 -> color de la vela, brillo por cantidad; neto < 0 -> violeta (el flujo le lleva la contra); 0 -> gris tenue; doji -> gris."""
    L = lecturas_conf(vs); out = []
    for k in range(len(vs)):
        s = dir_vela(vs[k], doji)
        if s == 0: out.append((GRIS, 0.45)); continue
        neto = sum(1 for x in L[k] if x == s) - sum(1 for x in L[k] if x == -s)
        if neto > 0: out.append((s, (0.40, 0.58, 0.80, 1.0)[neto - 1]))
        elif neto < 0: out.append((CONTRA, (0.45, 0.62, 0.82, 1.0)[-neto - 1]))
        else: out.append((GRIS, 0.30))
    return out


# ------------------------------------------------------------------ medidas
def pivotes(vs, k=3, minimo=0.0005, adelante=5):
    out = []
    for i in range(k, len(vs) - adelante):
        if vs[i]["t"][:10] != vs[i - k]["t"][:10] or vs[i]["t"][:10] != vs[i + adelante]["t"][:10]: continue
        lo = min(v["l"] for v in vs[i - k:i + k + 1]); hi = max(v["h"] for v in vs[i - k:i + k + 1])
        if vs[i]["l"] == lo and max(v["h"] for v in vs[i + 1:i + adelante + 1]) - lo >= minimo * lo: out.append((i, +1))
        if vs[i]["h"] == hi and hi - min(v["l"] for v in vs[i + 1:i + adelante + 1]) >= minimo * hi: out.append((i, -1))
    return out


def medir(vs, celdas, nombre, idx, tope=8):
    """idx = velas de rueda. 'Con cuerpo' usa una definicion FIJA (>= 20 % del rango) para que todas las reglas se midan igual."""
    con = [k for k in idx if dir_vela(vs[k], 0.20) != 0]; doj = [k for k in idx if dir_vela(vs[k], 0.20) == 0]
    N = float(len(con)); c = dict(coincide=0, opuesto=0, apagado=0, contra=0, gris=0)
    for k in con:
        s = dir_vela(vs[k], 0.20); tono, alfa = celdas[k]
        if tono == CONTRA: c["contra"] += 1
        elif tono == GRIS: c["gris"] += 1
        elif tono == 0 or alfa < VISIBLE: c["apagado"] += 1
        elif tono == s: c["coincide"] += 1
        else: c["opuesto"] += 1
    dj = sum(1 for k in doj if celdas[k][0] in (GRIS, 0) or celdas[k][1] < VISIBLE) / max(1.0, float(len(doj)))
    # retraso en los giros: primera vela j >= i con el tono del giro y visible
    piv = [(i, s) for i, s in pivotes(vs) if de_rueda(vs[i])]; lags = []
    for i, s in piv:
        j = next((j for j in range(i, min(len(vs), i + tope + 1)) if celdas[j][0] == s and celdas[j][1] >= VISIBLE), None)
        lags.append(tope + 1 if j is None else j - i)
    lags.sort()
    # honesta: la vela SIGUIENTE tiene el color de la celda? (solo celdas verde/rojo visibles, siguiente con cuerpo, mismo dia)
    sig = [(celdas[k][0], dir_vela(vs[k + 1], 0.20)) for k in idx if k + 1 < len(vs) and vs[k + 1]["t"][:10] == vs[k]["t"][:10]
           and celdas[k][0] in (1, -1) and celdas[k][1] >= VISIBLE and dir_vela(vs[k + 1], 0.20) != 0]
    sg = sum(1 for a, b in sig if a == b) / max(1.0, float(len(sig)))
    return dict(nombre=nombre, n=int(N), coincide=c["coincide"] / N, opuesto=c["opuesto"] / N, apagado=c["apagado"] / N, contra=c["contra"] / N, gris=c["gris"] / N,
                doji_neutra=dj, lag_med=lags[len(lags) // 2] if lags else -1, lag_prom=sum(lags) / max(1.0, float(len(lags))), giros=len(lags), siguiente=sg, n_sig=len(sig))


def linea(r):
    return ("%-34s | coincide %5.1f %%  opuesto %5.1f %%  apagado %5.1f %%  contra %5.1f %%  gris %4.1f %% | doji neutra %3.0f %% | giro: mediana %d, prom %.2f | "
            "sig. vela %4.1f %% (n %d)") % (r["nombre"], 100 * r["coincide"], 100 * r["opuesto"], 100 * r["apagado"], 100 * r["contra"], 100 * r["gris"],
                                            100 * r["doji_neutra"], r["lag_med"], r["lag_prom"], 100 * r["siguiente"], r["n_sig"])


def valor_del_contra(vs, celdas, idx, m=3, vueltas=400):
    """La celda violeta (vela contra el delta) dice algo de lo que sigue? Retorno a m velas EN EL SENTIDO DEL DELTA (o sea contra la vela), contra azar."""
    casos = []
    for k in idx:
        if celdas[k][0] != CONTRA or k + m >= len(vs) or vs[k + m]["t"][:10] != vs[k]["t"][:10]: continue
        s = 1 if vs[k]["d"] > 0 else -1
        casos.append(s * (vs[k + m]["c"] - vs[k]["c"]) / vs[k]["c"] * 1e4)
    if len(casos) < 10: return None
    random.seed(7); base = [k for k in idx if k + m < len(vs) and vs[k + m]["t"][:10] == vs[k]["t"][:10]]; plac = []
    for _ in range(vueltas):
        mm = [random.choice((1, -1)) * (vs[k + m]["c"] - vs[k]["c"]) / vs[k]["c"] * 1e4 for k in random.sample(base, len(casos))]
        plac.append(sum(mm) / len(mm))
    media = sum(casos) / len(casos); ps = st.pstdev(plac)
    return dict(n=len(casos), a_favor_del_delta=sum(1 for x in casos if x > 0) / float(len(casos)), media_pb=media, z=(media - sum(plac) / len(plac)) / ps if ps > 0 else 0)


def main():
    arg = [a for a in sys.argv[1:] if not a.startswith("--")]
    if arg and arg[0].lower().endswith(".csv"): vs = cargar_csv(arg[0]); fuente = os.path.basename(arg[0])
    else:
        inst = arg[0] if arg else "MNQ"; marco = arg[1] if len(arg) > 1 else "M1"; vs = cargar_centinela(inst, marco); fuente = "centinela %s %s" % (inst, marco)
    idx = [k for k in range(len(vs)) if de_rueda(vs[k])]; dias = sorted(set(vs[k]["t"][:10] for k in idx))
    print("%s: %d velas (%d de rueda, %d dias: %s a %s)" % (fuente, len(vs), len(idx), len(dias), dias[0] if dias else "-", dias[-1] if dias else "-"))
    print("\nCINTA PRESION")
    reglas = [("1.3 instalada (eficaz sostenida)", regla_13(vs)), ("delta de la vela a secas", regla_delta(vs)),
              ("PROPUESTA vela+flujo (doji 15, piso 20)", regla_vela_flujo(vs))]
    for dj in (0.10, 0.20, 0.25):
        reglas.append(("  variante doji %d %%" % (dj * 100), regla_vela_flujo(vs, doji=dj)))
    for cd in (0.20, 0.50):
        reglas.append(("  variante contra desde %.2f" % cd, regla_vela_flujo(vs, contra_desde=cd)))
    for nombre, c in reglas: print(linea(medir(vs, c, nombre, idx)))
    print("\nCINTA CONFLUENCIA")
    for nombre, c in (("1.3 instalada (suma de 4 lecturas)", conf_13(vs)), ("PROPUESTA lecturas que acompañan a la vela", conf_vela(vs))):
        print(linea(medir(vs, c, nombre, idx)))
    print("\nLA CELDA VIOLETA (vela contra el delta): dice algo de lo que sigue?  (retorno en el sentido del DELTA, contra azar)")
    for m in (1, 3, 5):
        r = valor_del_contra(vs, regla_vela_flujo(vs), idx, m)
        if r: print("  a %d velas: n %d | a favor del delta %.1f %% | media %+.2f pb | z %+.1f" % (m, r["n"], 100 * r["a_favor_del_delta"], r["media_pb"], r["z"]))
    if "--ejemplo" in sys.argv:
        t0 = sys.argv[sys.argv.index("--ejemplo") + 1]; ks = [k for k in range(len(vs)) if vs[k]["t"][:16] >= t0][:30]
        a, b, cA, cB = regla_13(vs), regla_vela_flujo(vs), conf_13(vs), conf_vela(vs)
        def cel(x):
            tono, alfa = x
            if tono == CONTRA: return "VIOL"
            if tono == GRIS: return "gris"
            if tono == 0 or alfa < VISIBLE: return " .  "
            return ("VERD" if tono > 0 else "ROJO") if alfa >= 0.7 else ("verd" if tono > 0 else "rojo")
        print("\nEJEMPLO desde %s UTC  (MAYUSCULA = intenso, minuscula = media luz, '.' = apagado)" % t0)
        print("hora   vela  cuerpo  delta | presion 1.3  presion 1.4 | confl 1.3  confl 1.4")
        for k in ks:
            v = vs[k]; s = dir_vela(v, 0.20); hl = "%02d:%s" % ((int(v["t"][11:13]) - 3) % 24, v["t"][14:16])
            print("%s  %s %+7.2f %+6d |    %s         %s    |   %s       %s" % (hl, "VERDE" if s > 0 else "ROJA " if s < 0 else "doji ", v["c"] - v["o"], v["d"], cel(a[k]), cel(b[k]), cel(cA[k]), cel(cB[k])))


if __name__ == "__main__":
    main()
