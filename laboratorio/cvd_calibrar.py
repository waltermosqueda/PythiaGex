# -*- coding: utf-8 -*-
"""
cvd_calibrar.py — calibrar la VELOCIDAD de las cintas de Flujo Claro para scalping (pedido del operador 17-09 14:00:
"los colores avisaron tarde: a las 13:57 habia un martillo y la presion seguia roja; a las 13:58 vela verde y la
presion roja y la confluencia negra").

La cinta de presion original suma 5 velas: por construccion arrastra el color viejo. Aca se comparan lecturas mas
rapidas con TRES medidas que tiran para lados opuestos (no hay almuerzo gratis):
  retraso   en los giros de precio (pivote de +-3 velas con recorrido posterior), cuantas velas despues del pivote
            la cinta pasa al color del giro (0 = en la misma vela del pivote). Menos es mejor.
  parpadeo  cambios de color por hora, y que fraccion de los colores nuevos dura una sola vela. Menos es mejor.
  acuerdo   con la vela actual: cuando la vela tiene cuerpo, que fraccion de las veces el color coincide con ella.
            (El delta y el precio van juntos: una cinta que describe AHORA tiene que acordar con la vela de ahora.)
Los umbrales de cada lectura se fijan para que todas dejen el mismo porcentaje de velas en neutro (comparacion pareja).

Uso:  python laboratorio/cvd_calibrar.py [MNQ] [M1] [--ejemplo AAAA-MM-DDTHH:MM]   (la hora del ejemplo en UTC)
"""
import json, math, os, sys, statistics as st

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
                           dmax=of.get("dmax", j["delta"]), dmin=of.get("dmin", j["delta"])))
    mejor = {}
    for v in vs:                                   # duplicados (dos graficos grabando la misma vela): queda la de mayor volumen
        if v["t"] not in mejor or v["vol"] > mejor[v["t"]]["vol"]: mejor[v["t"]] = v
    return [mejor[t] for t in sorted(mejor)]


def sd_movil(d, k, n=60):
    w = d[max(0, k - n + 1):k + 1]
    return st.pstdev(w) if len(w) > 5 else 0.0


def lecturas(vs):
    """Cada lectura es una lista de valores por vela (positivo = comprador), sin mirar adelante."""
    n = len(vs); d = [v["d"] for v in vs]
    sd = [sd_movil(d, k) for k in range(n)]
    L = {}
    for w in (5, 3, 2, 1):
        L["suma%d" % w] = [sum(d[max(0, k - w + 1):k + 1]) / (sd[k] * math.sqrt(w)) if sd[k] > 0 else 0 for k in range(n)]
    for vm in (1.0, 1.5, 2.5):          # olvido exponencial con vida media en velas
        a = 1 - 0.5 ** (1.0 / vm); e = 0; out = []
        for k in range(n):
            e = e + a * (d[k] - e); out.append(e / (sd[k] * math.sqrt(a / (2 - a))) if sd[k] > 0 else 0)
        L["exp%.1f" % vm] = out
    L["vela%"] = [v["d"] / v["vol"] * 10 if v["vol"] > 0 else 0 for v in vs]                       # delta / volumen de ESA vela
    cierre = [((v["d"] - v["dmin"]) / (v["dmax"] - v["dmin"]) * 2 - 1) if v["dmax"] > v["dmin"] else 0 for v in vs]   # donde cerro el delta en su recorrido
    L["cierreDelta"] = cierre
    # mezcla: el delta de la vela (z de 1) y donde cerro dentro de la vela (capta el martillo: venta temprana, compra al final)
    z1 = L["suma1"]
    L["vela+cierre"] = [0.6 * max(-3, min(3, z1[k])) + 0.9 * cierre[k] for k in range(n)]
    L["exp1.5+cierre"] = [0.6 * max(-3, min(3, L["exp1.5"][k])) + 0.9 * cierre[k] for k in range(n)]
    return L


def estados(vals, neutro=0.35):
    ab = sorted(abs(x) for x in vals); thr = ab[int(len(ab) * neutro)]
    return [1 if x > thr else -1 if x < -thr else 0 for x in vals], thr


def pivotes(vs, k=3, minimo=0.0005, adelante=5):
    out = []
    for i in range(k, len(vs) - adelante):
        if vs[i]["t"][:10] != vs[i - k]["t"][:10] or vs[i]["t"][:10] != vs[i + adelante]["t"][:10]: continue
        lo = min(v["l"] for v in vs[i - k:i + k + 1]); hi = max(v["h"] for v in vs[i - k:i + k + 1])
        if vs[i]["l"] == lo and max(v["h"] for v in vs[i + 1:i + adelante + 1]) - lo >= minimo * lo: out.append((i, +1))
        if vs[i]["h"] == hi and hi - min(v["l"] for v in vs[i + 1:i + adelante + 1]) >= minimo * hi: out.append((i, -1))
    return out


def medir(vs, nombre, vals, piv, tope=8):
    e, thr = estados(vals)
    # retraso: primera vela j >= i con el color del giro
    lags, nunca = [], 0
    for i, s in piv:
        j = next((j for j in range(i, min(len(e), i + tope + 1)) if e[j] == s), None)
        if j is None: nunca += 1; lags.append(tope + 1)
        else: lags.append(j - i)
    lags.sort()
    # parpadeo: colores nuevos (paso a +1 o -1 desde otra cosa) y cuantos duran una sola vela
    nuevos = [k for k in range(1, len(e)) if e[k] != 0 and e[k] != e[k - 1]]
    fugaces = sum(1 for k in nuevos if k + 1 < len(e) and e[k + 1] != e[k])
    horas = len(vs) / 60.0 * (2 if "M2" in sys.argv else 1)
    # acuerdo con la vela actual (solo velas con cuerpo >= 1/3 de su rango mediano)
    med = st.median(v["h"] - v["l"] for v in vs)
    con = [(e[k], 1 if vs[k]["c"] > vs[k]["o"] else -1) for k in range(len(vs)) if abs(vs[k]["c"] - vs[k]["o"]) >= med / 3]
    acuerdo = sum(1 for a, b in con if a == b) / len(con); contra = sum(1 for a, b in con if a == -b) / len(con)
    return dict(nombre=nombre, lag_med=lags[len(lags) // 2], lag_prom=sum(lags) / len(lags), en1=sum(1 for x in lags if x <= 1) / len(lags),
                nunca=nunca / len(lags), cambios_h=len(nuevos) / horas, fugaz=fugaces / max(1, len(nuevos)), acuerdo=acuerdo, contra=contra, thr=thr)


def main():
    inst = next((a for a in sys.argv[1:] if a in ("MNQ", "MES")), "MNQ"); marco = next((a for a in sys.argv[1:] if a in ("M1", "M2")), "M1")
    vs = [v for v in cargar(inst, marco) if "13:30" <= v["t"][11:16] <= "20:00"]
    dias = sorted(set(v["t"][:10] for v in vs)); piv = pivotes(vs)
    print("%s %s: %d velas de rueda, %d dias, %d giros de precio (pivote +-3 velas, recorrido >= 0,05 %%)" % (inst, marco, len(vs), len(dias), len(piv)))
    L = lecturas(vs)
    print("%-15s | retraso: mediana  prom  <=1 vela  nunca | cambios/h  duran 1 vela | con la vela: acuerda  en contra" % "lectura")
    res = []
    for nombre, vals in L.items():
        r = medir(vs, nombre, vals, piv); res.append(r)
        print("%-15s |   %d velas       %4.1f    %3.0f %%    %3.0f %% |   %5.1f       %3.0f %%      |          %3.0f %%     %3.0f %%" % (
            nombre, r["lag_med"], r["lag_prom"], 100 * r["en1"], 100 * r["nunca"], r["cambios_h"], 100 * r["fugaz"], 100 * r["acuerdo"], 100 * r["contra"]))
    if "--ejemplo" in sys.argv:
        t0 = sys.argv[sys.argv.index("--ejemplo") + 1]
        idx = [k for k, v in enumerate(vs) if v["t"][:16] >= t0][:12]
        print("\nEJEMPLO desde %s UTC (vela: hora local, cuerpo, delta | color de cada lectura: + verde, - rojo, . neutro)" % t0)
        E = {nombre: estados(vals)[0] for nombre, vals in L.items()}
        print("%-22s " % "vela" + " ".join("%-7s" % n[:7] for n in L))
        for k in idx:
            v = vs[k]; hl = "%02d:%s" % ((int(v["t"][11:13]) - 3) % 24, v["t"][14:16])
            print("%s %+6.1f d%+5d        " % (hl, v["c"] - v["o"], v["d"]) + " ".join("%-7s" % ("+" if E[n][k] > 0 else "-" if E[n][k] < 0 else ".") for n in L))


if __name__ == "__main__":
    main()
