# -*- coding: utf-8 -*-
"""RONDA 23, paso 7: COMPROBACION DESDE EL CRUDO con codigo distinto del motor (modulo csv + datetime/zoneinfo, bucles de Python puro;
no importa r23_sim, ni r23_01, ni los scripts de las candidatas).
(a) Operaciones de la CINTA de ES: lee el CSV crudo de la sonda, saca duplicados exactos, arma por su cuenta el VWAP de la rueda, el maximo y
    el minimo, el balance inicial, la X con la mediana de 10 rangos previos, la condicion de la regla en el cierre de la vela y recorre las
    ordenes una por una hasta la salida. Compara hora, lado, entrada, stop, salida y resultado con lo que dio el script de la candidata.
(b) Operaciones de las VELAS de MES 5 min (diseno y chequeo): lee velas-MES-m5.csv con csv (solo lectura), arma la rueda, el VWAP con
    (max + min) / 2, la X con las 10 ruedas completas previas, y recorre las velas con la regla conservadora. Compara con el motor de 5 min.
Uso: python -I r23_07_verificar.py -> salidas/verificacion.txt"""
import csv, json, math, os, statistics, sys
from datetime import datetime, timezone, timedelta
from zoneinfo import ZoneInfo

AQUI = os.path.dirname(os.path.abspath(__file__))
SAL = os.path.join(AQUI, "salidas")
FLUJO = os.path.join(os.environ.get("APPDATA", ""), "ATAS", "PythiaGex", "flujo")
VELAS = os.path.join(os.path.dirname(AQUI), "dom", "ronda3", "velas", "velas-MES-m5.csv")
SCR = r"C:\Users\wmx_7\AppData\Local\Temp\claude\6e29e09c80928880\233bf8d4-5113-4e95-a0d9-68bff726690d\scratchpad\r23"
NY = ZoneInfo("America/New_York")
TK = 0.25
REGLAS = {"A1": dict(desde=13 * 60 + 30, hasta=15 * 60 + 30, dv=2.0, ib=1.0, pf=0.55, m=None),
          "A2": dict(desde=14 * 60, hasta=15 * 60 + 30, dv=2.0, ib=1.5, pf=None, m=1.5),
          "A3": dict(desde=13 * 60, hasta=15 * 60 + 30, dv=1.5, ib=1.0, pf=0.55, m=None)}
OUT = []


def P(s=""):
    print(s, flush=True); OUT.append(s)


def al_tick(x):          # distancia -> ticks, mitad para arriba
    return math.floor(x / TK + 0.5) * TK


# ------------------------------------------------------------------------------------------------ (a) cinta
def leer_cinta_csv(ruta):
    vistos = set(); filas = []
    with open(ruta, newline="") as fh:
        rd = csv.reader(fh); cab = next(rd)
        it, ip, iu, iv = cab.index("t"), cab.index("primero"), cab.index("ultimo"), cab.index("vol")
        for r in rd:
            tup = tuple(r)
            if tup in vistos: continue
            vistos.add(tup)
            t = datetime.strptime(r[it], "%Y-%m-%dT%H:%M:%S.%f").replace(tzinfo=timezone.utc).astimezone(NY)
            filas.append((t, float(r[ip]), float(r[iu]), float(r[iv])))
    filas.sort(key=lambda x: x[0])
    return filas


def verificar_cinta(op, rangos):
    dia = op["fecha"]; R = REGLAS[op["candidata"]]
    ruta = [os.path.join(FLUJO, f) for f in os.listdir(FLUJO) if f.startswith("cinta-ES-") and f.endswith(".csv") and f[-16:-12] + "-" + f[-12:-10] + "-" + f[-10:-8] == dia][0]
    filas = leer_cinta_csv(ruta)
    rth = [x for x in filas if x[0].strftime("%Y-%m-%d") == dia and (9 * 60 + 30) <= x[0].hour * 60 + x[0].minute < 16 * 60]
    previas = sorted(d for d in rangos if d < dia)[-10:]
    med = statistics.median([rangos[d] for d in previas]); X = al_tick(0.08 * med)
    h_e, m_e = int(op["hora_entrada"][:2]), int(op["hora_entrada"][3:5])
    marca = datetime(int(dia[:4]), int(dia[5:7]), int(dia[8:10]), h_e, m_e, tzinfo=NY)
    antes = [x for x in rth if x[0] < marca]; despues = [x for x in rth if x[0] >= marca]
    pv = sum((a + b) / 2 * v for _, a, b, v in antes); vv = sum(v for *_, v in antes); vw = pv / vv
    H = max(max(a, b) for _, a, b, _ in antes); L = min(min(a, b) for _, a, b, _ in antes)
    ib = [x for x in rth if x[0] < datetime(marca.year, marca.month, marca.day, 10, 30, tzinfo=NY)]
    ibh = max(max(a, b) for _, a, b, _ in ib); ibl = min(min(a, b) for _, a, b, _ in ib)
    c = antes[-1][2]; s = 1 if c > vw else -1
    dvp = abs(c - vw); dv = dvp / X
    ext_ib = max(0.0, (H - ibh) / (ibh - ibl)) if s > 0 else max(0.0, (ibl - L) / (ibh - ibl))
    ext = H if s > 0 else L
    stop = ext + s * al_tick(0.5 * X)
    if R["m"]:
        a2 = c + s * al_tick(R["m"] * dvp); stop = max(stop, a2) if s > 0 else min(stop, a2)
    pf = abs(stop - c) / (abs(stop - c) + dvp)
    cumple = dv >= R["dv"] and ext_ib <= R["ib"] and (R["pf"] is None or pf >= R["pf"])
    lado = -s
    # objetivo: VWAP al cierre de cada vela de 5 min (limite para la vela siguiente), al tick mitad para arriba
    def vwap_hasta(t):
        a_ = [x for x in rth if x[0] < t]
        return sum((p1 + p2) / 2 * v for _, p1, p2, v in a_) / sum(v for *_, v in a_)
    lim_de = {}
    salida = None
    for (t, p1, p2, v) in despues:
        inicio_vela = t.replace(minute=t.minute - t.minute % 5, second=0, microsecond=0)
        if inicio_vela not in lim_de:
            lim_de[inicio_vela] = math.floor(vwap_hasta(inicio_vela) / TK + 0.5) * TK
        lim = lim_de[inicio_vela]
        hi, lo = max(p1, p2), min(p1, p2)
        if (lado < 0 and hi >= stop) or (lado > 0 and lo <= stop):
            pasada = (p1 >= stop) if lado < 0 else (p1 <= stop)
            salida = (t, p1 if pasada else stop, "stop"); break
        if (lado < 0 and lo <= lim - TK) or (lado > 0 and hi >= lim + TK):
            salida = (t, lim, "objetivo"); break
    if salida is None:
        salida = (despues[-1][0], despues[-1][2], "fin")
    pts = (salida[1] - c) * lado
    mio = dict(hora=op["hora_entrada"], lado="COMPRA" if lado > 0 else "VENTA", entrada=c, stop=stop, motivo=salida[2],
               hora_salida=salida[0].strftime("%H:%M:%S"), salida=salida[1], pts=round(pts, 4))
    suyo = dict(hora=op["hora_entrada"], lado=op["lado"], entrada=float(op["entrada"]), stop=float(op["stop"]), motivo=op["motivo"],
                hora_salida=op["hora_salida"], salida=float(op["salida"]), pts=round(float(op["pts"]), 4))
    igual = all((abs(mio[k] - suyo[k]) < 1e-6) if isinstance(mio[k], float) else (mio[k] == suyo[k]) for k in mio)
    return igual and cumple, mio, suyo, dict(X=X, med10=med, vwap=round(vw, 3), H=H, L=L, ib=(ibh, ibl), dv=round(dv, 3), ext_ib=round(ext_ib, 3), Pf=round(pf, 3), cumple=cumple)


# ------------------------------------------------------------------------------------------------ (b) velas de 5 min de MES
def leer_velas():
    ruedas = {}
    with open(VELAS, newline="") as fh:
        for r in csv.DictReader(fh):
            t = datetime.fromisoformat(r["t"]).replace(tzinfo=timezone.utc).astimezone(NY)
            mi = t.hour * 60 + t.minute
            if 570 <= mi < 960:
                ruedas.setdefault(t.strftime("%Y-%m-%d"), []).append((mi, float(r["o"]), float(r["h"]), float(r["l"]), float(r["c"]), float(r["vol"])))
    for d in ruedas: ruedas[d].sort()
    return ruedas


def verificar_vela(op, ruedas, R):
    dia = op["dia"]; k = int(op["k"])
    hist = sorted(d for d, v in ruedas.items() if d < dia and (len(v) == 78 or (len(v) >= 70 and v[-1][0] >= 955)))
    rangos = [max(x[2] for x in ruedas[d]) - min(x[3] for x in ruedas[d]) for d in hist[-10:]]
    X = al_tick(0.08 * statistics.median(rangos))
    v = ruedas[dia]
    pv = vv = 0.0; H = -1e9; L = 1e9; vws = []
    for (mi, o, h, l, c, vol) in v:
        pv += (h + l) / 2 * vol; vv += vol; H = max(H, h); L = min(L, l); vws.append((pv / vv, H, L))
    vw, Hk, Lk = vws[k]; c = v[k][4]; s = 1 if c > vw else -1
    ibh = max(x[2] for x in v[:12]); ibl = min(x[3] for x in v[:12])
    ext_ib = max(0.0, (Hk - ibh) / (ibh - ibl)) if s > 0 else max(0.0, (ibl - Lk) / (ibh - ibl))
    dvp = abs(c - vw); ext = Hk if s > 0 else Lk
    stop = ext + s * al_tick(0.5 * X)
    if R["m"]:
        a2 = c + s * al_tick(R["m"] * dvp); stop = max(stop, a2) if s > 0 else min(stop, a2)
    pf = abs(stop - c) / (abs(stop - c) + dvp)
    cumple = (dvp / X >= R["dv"]) and ext_ib <= R["ib"] and (R["pf"] is None or pf >= R["pf"]) and R["desde"] <= v[k][0] + 5 <= R["hasta"]
    lado = -s; res = None
    for j in range(k + 1, 78):
        mi, o, h, l, cc, vol = v[j]
        lim = math.floor(vws[j - 1][0] / TK + 0.5) * TK
        st = (l <= stop) if lado > 0 else (h >= stop)
        ob = (h >= lim + TK) if lado > 0 else (l <= lim - TK)
        if st:
            hueco = (o <= stop) if lado > 0 else (o >= stop)
            res = ((o if hueco else stop) - c) * lado, "stop", j; break
        if ob:
            res = (lim - c) * lado, "objetivo", j; break
    if res is None:
        res = (v[77][4] - c) * lado, "fin", 77
    return cumple, dict(entrada=c, stop=stop, pts=round(res[0], 4), motivo=res[1], j=res[2]), dict(X=X, vwap=round(vw, 3), dv=round(dvp / X, 3), ext_ib=round(ext_ib, 3), Pf=round(pf, 3))


def main():
    P("RONDA 23 - COMPROBACION DESDE EL CRUDO (codigo propio, sin el motor)")
    rangos = json.load(open(os.path.join(SAL, "historia_rangos_cinta.json")))["rangos"]
    # (a) cinta: A3 (la congelada) la 1a, 11a, 21a, 31a, 41a, 51a y la ultima; A1 y A2 la primera y la ultima
    P(""); P("(a) CINTA DE ES, orden por orden")
    ok = tot = 0
    for cid in ("A1", "A2", "A3"):
        f = os.path.join(SAL, "ops-cinta-%s.csv" % cid)
        if not os.path.exists(f): continue
        with open(f, newline="", encoding="utf-8") as fh:
            ops = list(csv.DictReader(fh))
        idx = sorted(set([0, 10, 20, 30, 40, 50, len(ops) - 1])) if cid == "A3" else [0, len(ops) - 1]   # regla fija: A3 cada 10, A1/A2 primera y ultima
        for op in [ops[i] for i in idx if i < len(ops)]:
            igual, mio, suyo, info = verificar_cinta(op, rangos)
            tot += 1; ok += igual
            P("  %s %s %s %s: mio %s %.2f -> %s %.2f a las %s (%s, %+.2f) | script %s %.2f -> %s %.2f a las %s (%+.2f) | %s" % (
                cid, op["fecha"], mio["hora"], mio["lado"], mio["lado"], mio["entrada"], mio["motivo"], mio["salida"], mio["hora_salida"], mio["motivo"], mio["pts"],
                suyo["lado"], suyo["entrada"], suyo["motivo"], suyo["salida"], suyo["hora_salida"], suyo["pts"], "COINCIDE" if igual else "DIFIERE"))
            P("      X %.2f (mediana 10 %.2f) | VWAP %.3f | maximo %.2f minimo %.2f | IB %.2f-%.2f | %.2f X del VWAP | IB extendido %.2f veces | P geometrica %.3f | cumple la regla: %s" % (
                info["X"], info["med10"], info["vwap"], info["H"], info["L"], info["ib"][0], info["ib"][1], info["dv"], info["ext_ib"], info["Pf"], info["cumple"]))
    P("  cinta: %d de %d coinciden" % (ok, tot))
    # (b) velas de 5 min de MES: 2 operaciones de diseno y 2 de chequeo de A1 y A3 (la primera y la ultima de cada archivo)
    P(""); P("(b) VELAS DE 5 MIN DE MES (regla conservadora)")
    ruedas = leer_velas()
    nombres = {"A1": "A_1330-1530_dv2.0_ible1+Pfgt0.55_F1", "A2": "A_1400-1530_dv2.0_ible1.5_F3", "A3": "A_1300-1530_dv1.5_ible1+Pfgt0.55_F1"}
    ok2 = tot2 = 0
    for muestra in ("diseno", "chequeo"):
        for cid in ("A1", "A2", "A3"):
            f = os.path.join(SCR, "ops_%s_%s.csv" % (muestra, nombres[cid]))
            if not os.path.exists(f): continue
            with open(f, newline="", encoding="utf-8") as fh:
                ops = list(csv.DictReader(fh))
            for op in [ops[0], ops[-1]]:
                cumple, mio, info = verificar_vela(op, ruedas, REGLAS[cid])
                suyo = dict(entrada=float(op["c"]), stop=float(op["stop"]), pts=round(float(op["pts_real"]), 4), j=int(op["jsal_real"]))
                igual = cumple and abs(mio["entrada"] - suyo["entrada"]) < 1e-9 and abs(mio["stop"] - suyo["stop"]) < 1e-9 and abs(mio["pts"] - suyo["pts"]) < 1e-6 and mio["j"] == suyo["j"]
                tot2 += 1; ok2 += igual
                P("  %s %-7s %s %s: entrada %.2f stop %.2f -> %s %+.2f en la vela de las %02d:%02d | motor %+.2f vela %02d:%02d | X %.2f VWAP %.3f %.2f X IB %.2f P %.3f | %s" % (
                    cid, muestra, op["dia"], op["hora"], mio["entrada"], mio["stop"], mio["motivo"], mio["pts"], *divmod(570 + 5 * mio["j"], 60), suyo["pts"],
                    *divmod(570 + 5 * suyo["j"], 60), info["X"], info["vwap"], info["dv"], info["ext_ib"], info["Pf"], "COINCIDE" if igual else "DIFIERE"))
    P("  velas: %d de %d coinciden" % (ok2, tot2))
    open(os.path.join(SAL, "verificacion.txt"), "w", encoding="utf-8").write("\n".join(OUT))


if __name__ == "__main__":
    main()
