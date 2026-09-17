# -*- coding: utf-8 -*-
"""
cvd_regla15.py — la regla de Flujo Claro 1.5, escrita UNA vez para el banco y para la paridad con el C#.

Sale de la revision de la 1.4 por tres revisores (17-09 15:57):
  - el 'lugar' del delta contra las ultimas 60 velas se SATURA en la apertura (a las 13:30 UTC la mitad de las celdas
    salen brillantes porque se comparan con el premercado): ahora el |delta| se divide antes por lo NORMAL DE ESA HORA
    (mediana de la misma franja +-15 min de las 5 sesiones anteriores) y recien despues se rankea contra las 60 velas;
  - la confluencia dejaba GRIS o al minimo a la vela mas grande de la hora (el CVD de 20 velas le restaba) y pintaba
    violeta sin causa propia: ahora LA VELA VOTA, el violeta exige que el delta de la vela o los grandes vayan contra
    ella, y el gris queda solo para la vela de indecision;
  - 'delta de la vela' y 'donde cerro el delta' eran la misma lectura (97 %): se funden en una;
  - la fila cinta pasa a percentil (prende el 30 % mas rapido de la hora) y grandes reparte su brillo por percentil.
"""
import csv, sys

VERDE, ROJO, CONTRA, GRIS = 1, -1, 2, 3


def cargar(ruta):
    vs = []
    for r in csv.DictReader(open(ruta, encoding="utf-8")):
        v = dict(t=r["t"], o=float(r["o"]), h=float(r["h"]), l=float(r["l"]), c=float(r["c"]), vol=float(r["vol"]), tk=float(r["ticks"]),
                 d=float(r["delta"]), dmax=float(r["dmax"]), dmin=float(r["dmin"]), big=float(r["big"]))
        for k in ("tono", "brillo", "tonoc", "brilloc", "lugar", "conf", "vel"):
            if k in r and r[k] != "": v["cs_" + k] = float(r[k])
        vs.append(v)
    return vs


def minutos(t):  # "AAAA-MM-DDTHH:MM:SS" -> minutos desde una epoca cualquiera (para medir 12 h) y minuto del dia
    import datetime
    dt = datetime.datetime(int(t[0:4]), int(t[5:7]), int(t[8:10]), int(t[11:13]), int(t[14:16]), int(t[17:19]))
    return (dt - datetime.datetime(2000, 1, 1)).total_seconds() / 60.0, dt.hour * 60 + dt.minute


def estacional(vs, campo, ancho=15, sesiones=5, minimo=30):
    """Lo normal de ESA hora: mediana (alta) del |campo| en la misma franja +-ancho min de las 'sesiones' anteriores (>= 12 h atras).
    Sin historia suficiente: mediana de las 60 velas previas. Nunca menos de 1. Causal: solo mira velas anteriores."""
    n = len(vs); tm = [minutos(v["t"]) for v in vs]; por = {}; out = [1.0] * n
    for k in range(n):
        ahora, m = tm[k]; vals = []
        for off in range(-ancho, ancho + 1):
            lst = por.get((m + off) % 1440)
            if not lst: continue
            tomados = 0
            for j in reversed(lst):
                if ahora - tm[j][0] < 720: continue
                vals.append(abs(vs[j][campo])); tomados += 1
                if tomados >= sesiones: break
        if len(vals) < minimo:
            vals = [abs(vs[j][campo]) for j in range(max(0, k - 60), k)]
        if vals:
            vals.sort(); out[k] = max(1.0, vals[len(vals) // 2])
        por.setdefault(m, []).append(k)
    return out


def lugares(norm, n=60):
    """Lugar de norm[k] entre los de las ultimas n velas (0 = el mas chico, 1 = el mas grande); con menos de 10 velas, 0."""
    out = []
    for k in range(len(norm)):
        a0 = max(0, k - n + 1); m = k - a0 + 1
        if m < 10: out.append(0.0); continue
        a = norm[k]; cnt = 1
        for j in range(a0, k):
            if norm[j] <= a: cnt += 1
        out.append(min(1.0, (cnt - 0.5) / m))
    return out


def lugar_no_nulos(vals, k, n=300):
    """Lugar de |vals[k]| entre los |valores| NO nulos de las ultimas n velas (para los grandes)."""
    a = abs(vals[k]); w = [abs(x) for x in vals[max(0, k - n + 1):k + 1] if x != 0]
    if a == 0 or len(w) < 5: return 0.5 if a != 0 else 0.0
    return min(1.0, (sum(1 for x in w if x <= a) - 0.5) / len(w))


def regla(vs, doji=0.15, piso=0.20, contra_desde=0.35, delta_min=20, fundir=False, vota=True, estacion=True):
    """Devuelve por vela: dict(tono, bri, tonoc, bric, lugar, conf, vel, brig, tonog)."""
    n = len(vs); d = [v["d"] for v in vs]; big = [v["big"] for v in vs]
    estD = estacional(vs, "d") if estacion else [1.0] * n; estT = estacional(vs, "tk") if estacion else [1.0] * n
    lug = lugares([abs(d[k]) / estD[k] for k in range(n)]); lugT = lugares([vs[k]["tk"] / estT[k] for k in range(n)])
    cvd = []; s = 0
    for x in d: s += x; cvd.append(s)
    out = []
    for k in range(n):
        v = vs[k]; cuerpo = v["c"] - v["o"]; rango = v["h"] - v["l"]
        sv = (1 if cuerpo > 0 else -1) if rango > 0 and abs(cuerpo) >= doji * rango else 0
        sd = 1 if d[k] > 0 else -1 if d[k] < 0 else 0
        lu = lug[k] if abs(d[k]) >= delta_min else 0.0
        # presion
        if sv == 0: tono, bri = GRIS, 0.60
        elif sd == sv and lu >= piso: tono, bri = sv, 0.40 + 0.60 * lu
        elif sd == -sv and lu >= contra_desde: tono, bri = CONTRA, 0.45 + 0.55 * lu
        else: tono, bri = sv, 0.40
        # lecturas firmadas
        a = sd if lu >= piso else 0
        rec = v["dmax"] - v["dmin"]; cd = (d[k] - v["dmin"]) / rec * 2 - 1 if rec > 0 else 0; e = 1 if cd > 0.3 else -1 if cd < -0.3 else 0
        w = sorted(abs(x) for x in big[max(0, k - 299):k + 1]); bmax = w[min(len(w) - 1, int(len(w) * 0.95))]
        b = (1 if big[k] > 0.3 * bmax else -1 if big[k] < -0.3 * bmax else 0) if bmax > 0 else 0
        c = (1 if cvd[k] > cvd[k - 20] else -1 if cvd[k] < cvd[k - 20] else 0) if k >= 20 else 0
        if fundir:
            ae = a if a != 0 else e; conf = ae + b + c
            aP = sd if ((sd == sv and lu >= piso) or (sd == -sv and lu >= contra_desde)) else (e if (a == 0 and e == sv) else 0)
            lect = aP + b + c
        else:
            conf = a + e + b + c
            aP = sd if ((sd == sv and lu >= piso) or (sd == -sv and lu >= contra_desde)) else 0
            lect = aP + e + b + c
        # confluencia respecto de la vela
        if sv == 0: tonoc, bric = GRIS, 0.60
        else:
            neto = (1 if vota else 0) + sv * lect
            causa = (sd == -sv and lu >= contra_desde) or b == -sv
            if neto >= 1: tonoc, bric = sv, (0.40, 0.60, 0.80, 1.0)[min(4, neto) - 1]
            elif causa: tonoc, bric = CONTRA, (0.50, 0.75, 1.0, 1.0)[min(3, -neto)] if fundir else (0.45, 0.62, 0.82, 1.0)[min(3, -neto)]
            else: tonoc, bric = sv, 0.40
        # grandes y cinta
        tonog = 0; brig = 0.0
        if big[k] != 0:
            tonog = 1 if big[k] > 0 else -1; brig = 0.40 + 0.60 * lugar_no_nulos(big, k)
            if sv != 0 and tonog == -sv: tonog = CONTRA
        out.append(dict(tono=tono, bri=bri, tonoc=tonoc, bric=bric, lugar=lug[k], conf=conf, vel=lugT[k], tonog=tonog, brig=brig, sv=sv))
    return out


def de_rueda(v): return "13:30" <= v["t"][11:16] < "20:00"


if __name__ == "__main__":
    ruta = sys.argv[1]; vs = cargar(ruta); idx = [k for k in range(len(vs)) if de_rueda(vs[k])]
    print("%s: %d velas, %d de rueda" % (ruta, len(vs), len(idx)))
    hora = {}
    for nombre, kw in (("1.4 (60 velas, 4 lecturas, sin voto)", dict(fundir=False, vota=False, estacion=False, delta_min=0)),
                       ("1.5: 4 lecturas + la vela vota (el delta pesa doble si ademas cerro de ese lado)", dict(fundir=False, vota=True, estacion=True)),
                       ("descartada: delta fundido en una lectura + la vela vota", dict(fundir=True, vota=True, estacion=True))):
        R = regla(vs, **kw)
        con = [k for k in idx if vs[k]["h"] > vs[k]["l"] and abs(vs[k]["c"] - vs[k]["o"]) >= 0.20 * (vs[k]["h"] - vs[k]["l"])]
        N = float(len(con))
        def pct(f): return 100.0 * sum(1 for k in con if f(R[k])) / N
        # vela grande: cuerpo en el 20 % mayor de las ultimas 60 y >= 50 % del rango
        grandes = []
        for k in idx:
            v = vs[k]; cu = abs(v["c"] - v["o"]); rg = v["h"] - v["l"]
            if rg <= 0 or cu < 0.5 * rg: continue
            w = sorted(abs(vs[j]["c"] - vs[j]["o"]) for j in range(max(0, k - 59), k + 1))
            if cu >= w[int(len(w) * 0.8)]: grandes.append(k)
        floja = sum(1 for k in grandes if R[k]["tonoc"] in (GRIS, CONTRA) or R[k]["bric"] <= 0.41)
        print("\n%s" % nombre)
        print("  PRESION      coincide %5.1f %%  violeta %4.1f %%" % (pct(lambda r: r["tono"] in (1, -1)), pct(lambda r: r["tono"] == CONTRA)))
        print("  CONFLUENCIA  coincide %5.1f %%  violeta %4.1f %%  gris %4.1f %%   | brillo: minimo %4.1f %%  medio %4.1f %%  fuerte %4.1f %%  pleno %4.1f %%" % (
            pct(lambda r: r["tonoc"] in (1, -1)), pct(lambda r: r["tonoc"] == CONTRA), pct(lambda r: r["tonoc"] == GRIS),
            pct(lambda r: r["tonoc"] in (1, -1) and r["bric"] <= 0.41), pct(lambda r: r["tonoc"] in (1, -1) and 0.41 < r["bric"] <= 0.61),
            pct(lambda r: r["tonoc"] in (1, -1) and 0.61 < r["bric"] <= 0.81), pct(lambda r: r["tonoc"] in (1, -1) and r["bric"] > 0.81)))
        print("  vela GRANDE con confluencia floja (gris, violeta o al minimo): %d de %d = %.1f %% de las grandes (%.2f %% de la rueda)" % (floja, len(grandes), 100.0 * floja / max(1, len(grandes)), 100.0 * floja / len(idx)))
        # brillo de presion por franja de 30 min: % de celdas con lugar >= 0,8 (parejo = 20 %)
        fr = {}
        for k in range(len(vs)):
            h = vs[k]["t"][11:13] + (":00" if vs[k]["t"][14:16] < "30" else ":30"); a, b2 = fr.get(h, (0, 0)); fr[h] = (a + (1 if R[k]["lugar"] >= 0.8 else 0), b2 + 1)
        sel = ["13:00", "13:30", "14:00", "14:30", "16:00", "18:00", "19:30", "20:00", "20:30", "00:00", "07:00"]
        print("  celdas con lugar >= 0,8 por franja (parejo = 20 %): " + "  ".join("%s %.0f" % (h, 100.0 * fr[h][0] / fr[h][1]) for h in sel if h in fr))
        vals = [100.0 * a / b2 for a, b2 in fr.values()]; med = sum(vals) / len(vals)
        print("  desvio entre las %d franjas: %.1f puntos" % (len(vals), (sum((x - med) ** 2 for x in vals) / len(vals)) ** 0.5))
        lt = [R[k]["vel"] for k in idx]
        print("  fila cinta: prende (lugar de operaciones >= 0,70) el %.1f %% de las velas de rueda" % (100.0 * sum(1 for x in lt if x >= 0.70) / len(lt)))
