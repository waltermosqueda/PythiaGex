# -*- coding: utf-8 -*-
"""RONDA 22, paso 5: COMPROBACION DESDE LOS DATOS CRUDOS, con codigo DISTINTO del motor (no importa r22_motor ni r22_comun):
lee velas-MES-m5.csv con el modulo csv y zoneinfo, rehace para cada operacion elegida la mediana de 10, X, el VWAP de la rueda, el rango
relativo y el estado del semaforo en la vela de la entrada, controla que el extremo sea un pivote valido (minimo/maximo de la vela del
extremo, sin otro mas extremo hasta la entrada y con la vuelta de X), y recorre las velas siguientes con la regla conservadora para el resultado.
Elige 8 operaciones con una regla fija (primera ganadora y primera perdedora de cada variante en orden de fecha, saltando el 2025T3).
Uso: python -I r22_04_verificar.py -> salidas/verificacion.txt"""
import csv, json, math, os
from datetime import datetime, timedelta
from zoneinfo import ZoneInfo

AQUI = os.path.dirname(os.path.abspath(__file__))
CRUDO = os.path.join(os.path.dirname(AQUI), "dom", "ronda3", "velas", "velas-MES-m5.csv")
CURVA = os.path.join(os.path.dirname(AQUI), "ronda21_regimen", "curva_rango_por_minuto.json")
NYZ = ZoneInfo("America/New_York"); UTC = ZoneInfo("UTC")


def leer():
    ruedas = {}
    with open(CRUDO, newline="") as fh:
        for r in csv.DictReader(fh):
            t = datetime.fromisoformat(r["t"]).replace(tzinfo=UTC).astimezone(NYZ)
            rueda = (t + timedelta(hours=6)).date().isoformat()
            m = (t.hour * 60 + t.minute) - (0 if (t + timedelta(hours=6)).date() == t.date() else 1440)
            ruedas.setdefault(rueda, []).append((m, float(r["o"]), float(r["h"]), float(r["l"]), float(r["c"]), float(r["vol"])))
    for k in ruedas: ruedas[k].sort()
    return ruedas


def rth(barras):
    return [b for b in barras if 570 <= b[0] < 960]


def clase(barras):
    r = rth(barras)
    if len(r) == 78 and [b[0] for b in r] == list(range(570, 960, 5)): return "completa"
    if not r or max(b[0] for b in r) <= 795: return "corta"
    return "incompleta"


def verificar(op, ruedas, f):
    dia = op["dia"]; lado = int(op["lado"]); X_m = float(op["X"])
    hist = [d for d in sorted(ruedas) if d < dia and (clase(ruedas[d]) == "completa" or (clase(ruedas[d]) == "incompleta" and len(rth(ruedas[d])) >= 70))]
    prev = hist[-10:]
    rangos = sorted(max(b[2] for b in rth(ruedas[d])) - min(b[3] for b in rth(ruedas[d])) for d in prev)
    med = (rangos[4] + rangos[5]) / 2.0
    X = math.floor(0.08 * med / 0.25 + 0.5) * 0.25
    R_ = rth(ruedas[dia])
    hh, mm = int(op["hora"][:2]), int(op["hora"][3:])
    k = (hh * 60 + mm - 5 - 570) // 5                      # entrada al cierre de la vela k (que cierra a la 'hora')
    o, h, l, c, v = zip(*[(b[1], b[2], b[3], b[4], b[5]) for b in R_])
    ent = c[k]
    pv = sum((h[i] + l[i]) / 2 * v[i] for i in range(k + 1)); vv = sum(v[:k + 1]); vw = pv / vv
    rng = max(h[:k + 1]) - min(l[:k + 1]); rrn = rng / (med * f[5 * k + 4])
    lado_vw = 1 if ent > vw else (-1 if ent < vw else 0); lado_o = 1 if ent > o[0] else (-1 if ent < o[0] else 0)
    est = "T" if (rrn >= 1.6 and lado_vw == lado_o and lado_vw != 0) else ("R" if 1.0 < rrn < 1.6 else ("E" if rrn <= 1.0 else "N"))
    ext = float(op["ext"])
    # el extremo: alguna vela j <= k (de la rueda o de la noche) tiene ese minimo (compra) / maximo (venta); de j a k no hay otro mas extremo
    S = [(b[2], b[3]) for b in ruedas[dia]]
    idx0 = [i for i, b in enumerate(ruedas[dia]) if b[0] >= 570][0] + k              # indice de la vela k en la jornada completa
    js = [j for j in range(idx0 + 1) if (S[j][1] == ext if lado == 1 else S[j][0] == ext)]
    j = js[-1] if js else None
    piv_ok = j is not None and (all(S[i][1] >= ext for i in range(j, idx0 + 1)) if lado == 1 else all(S[i][0] <= ext for i in range(j, idx0 + 1)))
    vuelta_ok = j is not None and ((max(S[i][0] for i in range(j, idx0 + 1)) - ext >= X) if lado == 1 else (ext - min(S[i][1] for i in range(j, idx0 + 1)) >= X))
    # banda de 2 sigma (solo V4e): VWAP de la rueda y de la jornada con su desvio (varianza dentro de la vela incluida) al cierre de la
    # vela del extremo; el extremo tiene que quedar entre la banda y X/2 antes, sin perforarla
    banda_ok = None
    if op["familia"] == "V4e":
        jr = max(min(j - (idx0 - k), k), 0) if j is not None else 0          # indice de la vela del extremo dentro de la rueda
        def vw_sd(barras):
            sv = sum(b[5] for b in barras); m1 = sum((b[2] + b[3]) / 2 * b[5] for b in barras) / sv
            m2 = sum((((b[2] + b[3]) / 2) ** 2 + (b[2] - b[3]) ** 2 / 12) * b[5] for b in barras) / sv
            return m1, math.sqrt(max(m2 - m1 * m1, 0))
        i_r0 = idx0 - k
        bandas = [vw_sd(ruedas[dia][i_r0:i_r0 + jr + 1]), vw_sd(ruedas[dia][:i_r0 + jr + 1])]
        banda_ok = False
        for m1, sd in bandas:
            Lb = m1 - 2 * sd if lado == 1 else m1 + 2 * sd
            pen = (Lb - ext) * lado
            banda_ok |= (pen > -0.5 * X + 1e-9) and (pen <= 1e-9)
    stop = ext - lado * 0.5; R = (ent - stop) * lado
    # resultado con la regla conservadora
    def sim(tipo):
        T1 = ent + lado * R; T2 = ent + lado * 2 * R; fase = 1; parcial = 0.0
        for i in range(k + 1, 78):
            oo, hi, lo, cc = o[i], h[i], l[i], c[i]
            if fase == 1:
                toca_stop = lo <= stop if lado == 1 else hi >= stop
                hueco = oo <= stop if lado == 1 else oo >= stop
                toca_obj = hi >= T1 + 0.25 if lado == 1 else lo <= T1 - 0.25
                if toca_stop: return ((oo if hueco else stop) - ent) * lado, "stop_hueco" if hueco else "stop", i
                if toca_obj:
                    if tipo == "S6": return R, "objetivo", i
                    parcial = 0.5 * R; fase = 2
                    be = lo <= ent if lado == 1 else hi >= ent
                    t2 = hi >= T2 + 0.25 if lado == 1 else lo <= T2 - 0.25
                    if be: return parcial, "1R+entrada", i
                    if t2: return parcial + R, "1R+2R", i
                    continue
            else:
                hueco = oo <= ent if lado == 1 else oo >= ent
                be = lo <= ent if lado == 1 else hi >= ent
                t2 = hi >= T2 + 0.25 if lado == 1 else lo <= T2 - 0.25
                if be: return parcial + 0.5 * ((oo if hueco else ent) - ent) * lado, "1R+entrada", i
                if t2: return parcial + R, "1R+2R", i
        return ((c[77] - ent) * lado, "fin", 77) if fase == 1 else (parcial + 0.5 * (c[77] - ent) * lado, "1R+fin", 77)
    pts, mot, isal = sim(op["salida"])
    t_sal = "%02d:%02d-%02d:%02d" % (divmod(570 + 5 * isal, 60) + divmod(575 + 5 * isal, 60))
    v4 = op["familia"] == "V4e"
    chequeos = dict(entrada=abs(ent - float(op["entrada"])) < 1e-9, X=abs(X - X_m) < 1e-9, rrn=abs(rrn - float(op["rrn"])) < 1e-6,
                    estado=est == op["estado"], lado_vwap=(lado_vw == -lado) if v4 else (lado_vw == lado), pivote=bool(piv_ok and vuelta_ok),
                    resultado=abs(pts - float(op["pts"])) < 1e-9 and mot == op["motivo"])
    if v4: chequeos["banda_2sigma"] = bool(banda_ok)
    return dict(med=med, X=X, ent=ent, vw=vw, rrn=rrn, est=est, stop=stop, R=R, pts=pts, mot=mot, t_sal=t_sal, ok=chequeos)


def main():
    f = json.load(open(CURVA))["f"]
    ruedas = leer()
    L = ["RONDA 22 - COMPROBACION DESDE LOS DATOS CRUDOS (velas-MES-m5.csv; codigo propio, sin el motor)"]
    elegidas = []
    for var in ("T7-S6", "T6-P1", "V4e-S6", "COMB-T7+V4e-S6"):
        rows = list(csv.DictReader(open(os.path.join(AQUI, "salidas", "ops-r22-%s.csv" % var), encoding="utf-8")))
        rows = [r for r in rows if r["trimestre"] != "2025Q3"]
        g = next((r for r in rows if float(r["pts"]) > 0), None); p = next((r for r in rows if float(r["pts"]) <= 0), None)
        if var == "COMB-T7+V4e-S6":       # en la COMB, la primera de la parte V4e de 2026 (otra fecha)
            g = next((r for r in rows if r["familia"] == "V4e" and r["dia"] >= "2026-04-01" and float(r["pts"]) > 0), None)
            p = next((r for r in rows if r["familia"] == "T7" and r["dia"] >= "2026-06-01" and float(r["pts"]) <= 0), None)
        extra = []
        if var == "T7-S6":                # una ganadora de 2026 (otra fecha)
            extra.append(next((r for r in rows if r["dia"] >= "2026-01-01" and float(r["pts"]) > 0), None))
        if var == "T6-P1":                # una que llego a 1R + 2R
            extra.append(next((r for r in rows if r["motivo"] == "1R+2R"), None))
        for r in [g, p] + extra:
            if r is not None and not any(e[1]["dia"] == r["dia"] and e[1]["hora"] == r["hora"] and e[0] == var for e in elegidas):
                elegidas.append((var, r))
    todo_ok = 0
    for var, op in elegidas:
        z = verificar(op, ruedas, f)
        ok = all(z["ok"].values()); todo_ok += ok
        L.append("%s %s %s vela que cierra %s NY | %s a %.2f (extremo %.2f, stop %.2f, R %.2f, objetivo %.2f) | mediana de 10 %.2f, X %.2f | VWAP %.2f | rango relativo %.3f -> %s | "
                 "resultado %+.2f (%s, sale en la vela de las %s) | motor: entrada %s, %s %s | coincide: %s" % (
                     var, op["dia"], op["familia"], op["hora"], "COMPRA" if int(op["lado"]) == 1 else "VENTA", z["ent"], float(op["ext"]), z["stop"], z["R"],
                     z["ent"] + int(op["lado"]) * z["R"], z["med"], z["X"], z["vw"], z["rrn"], z["est"], z["pts"], z["mot"], z["t_sal"], op["entrada"], op["pts"], op["motivo"],
                     "TODO" if ok else ", ".join("%s=%s" % kv for kv in z["ok"].items())))
    L.append("Coinciden en todo %d de %d operaciones." % (todo_ok, len(elegidas)))
    open(os.path.join(AQUI, "salidas", "verificacion.txt"), "w", encoding="utf-8").write("\n".join(L))
    print("\n".join(L))


if __name__ == "__main__":
    main()
