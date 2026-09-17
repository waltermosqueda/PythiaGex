# -*- coding: utf-8 -*-
"""cvd_calibrar2.py — segunda vuelta: lecturas por ESTADO (color directo), con precio que acompaña, mezclas y anti-parpadeo.
Mismas tres medidas que cvd_calibrar.py. Uso: python laboratorio/cvd_calibrar2.py [MNQ|MES] [M1|M2] [--ejemplo UTC]"""
import sys, math, statistics as st
sys.path.insert(0, __import__("os").path.dirname(__file__))
import cvd_calibrar as C

def candidatos(vs):
    n = len(vs); d = [v["d"] for v in vs]; L = C.lecturas(vs)
    z1, s2, s3, s5 = L["suma1"], L["suma2"], L["suma3"], L["suma5"]
    cuerpo = [v["c"] - v["o"] for v in vs]
    # beta movil: cuanto precio suele mover un contrato de delta (mediana de |cuerpo|/|delta| de las ultimas 120 velas)
    beta = []
    for k in range(n):
        w = [abs(cuerpo[j]) / abs(d[j]) for j in range(max(0, k - 120), k) if abs(d[j]) >= 50]
        beta.append(st.median(w) if len(w) >= 20 else 0)
    def est(vals, thr): return [1 if x > thr else -1 if x < -thr else 0 for x in vals]
    E = {}
    E["suma5 (hoy)"] = C.estados(s5)[0]
    E["suma2"] = C.estados(s2)[0]
    E["suma1"] = C.estados(z1)[0]
    # A) eficaz: el color de la vela solo si el precio ACOMPAÑA al delta (cuerpo del mismo signo y al menos 25 % de lo habitual)
    def eficaz(zs, ventana):
        out = []
        for k in range(n):
            dd = sum(d[max(0, k - ventana + 1):k + 1]); pp = vs[k]["c"] - vs[max(0, k - ventana + 1)]["o"]
            s = 1 if zs[k] > 0.4 else -1 if zs[k] < -0.4 else 0
            ok = s != 0 and pp * s > 0 and (beta[k] == 0 or abs(pp) >= 0.25 * beta[k] * abs(dd))
            out.append(s if ok else 0)
        return out
    E["eficaz1"] = eficaz(z1, 1)
    E["eficaz2"] = eficaz(s2, 2)
    # B) fuerte manda: si la vela actual es fuerte (|z1| >= 1) manda ella; si no, la suma de 3
    E["fuerte1|suma3"] = [(1 if z1[k] > 0 else -1) if abs(z1[k]) >= 1.0 else (1 if s3[k] > 0.5 else -1 if s3[k] < -0.5 else 0) for k in range(n)]
    E["fuerte1|suma2"] = [(1 if z1[k] > 0 else -1) if abs(z1[k]) >= 1.0 else (1 if s2[k] > 0.5 else -1 if s2[k] < -0.5 else 0) for k in range(n)]
    # C) anti-parpadeo sobre suma1: un color nuevo entra si es fuerte (|z1| >= 1) o si se repite dos velas
    def antiparpadeo(zs, thr=0.45, fuerte=1.0):
        out, act = [], 0
        for k in range(n):
            s = 1 if zs[k] > thr else -1 if zs[k] < -thr else 0
            if s == act: pass
            elif s == 0: act = 0 if (k > 0 and abs(zs[k - 1]) < thr) else act      # a neutro recien con dos velas flojas
            elif abs(zs[k]) >= fuerte or (k > 0 and ((zs[k - 1] > thr) if s > 0 else (zs[k - 1] < -thr))): act = s
            out.append(act)
        return out
    E["suma1 anti-parp."] = antiparpadeo(z1)
    # D) eficaz + anti-parpadeo: color de la vela si el precio acompaña; se sostiene una vela mas si la siguiente es floja
    ef = E["eficaz1"]; out, act = [], 0
    for k in range(n):
        if ef[k] != 0: act = ef[k]
        elif abs(z1[k]) >= 0.4 and (1 if z1[k] > 0 else -1) == -act: act = 0       # flujo en contra sin precio: neutro (posible absorcion)
        elif k > 0 and ef[k - 1] == 0 and ef[k] == 0: act = 0
        out.append(act)
    E["eficaz1 sostenido"] = out
    return E

def medir_estados(vs, nombre, e, piv, marco, tope=8):
    lags, nunca = [], 0
    for i, s in piv:
        j = next((j for j in range(i, min(len(e), i + tope + 1)) if e[j] == s), None)
        if j is None: nunca += 1; lags.append(tope + 1)
        else: lags.append(j - i)
    lags.sort()
    nuevos = [k for k in range(1, len(e)) if e[k] != 0 and e[k] != e[k - 1]]
    fugaces = sum(1 for k in nuevos if k + 1 < len(e) and e[k + 1] != e[k])
    horas = len(vs) / 60.0 * (2 if marco == "M2" else 1)
    med = st.median(v["h"] - v["l"] for v in vs)
    con = [(e[k], 1 if vs[k]["c"] > vs[k]["o"] else -1) for k in range(len(vs)) if abs(vs[k]["c"] - vs[k]["o"]) >= med / 3]
    neutro = sum(1 for x in e if x == 0) / len(e)
    print("%-18s |   %d velas   %4.1f   %3.0f %%   %3.0f %% |  %5.1f     %3.0f %%    |     %3.0f %%      %3.0f %%    |  %3.0f %%" % (
        nombre, lags[len(lags) // 2], sum(lags) / len(lags), 100 * sum(1 for x in lags if x <= 1) / len(lags), 100 * nunca / len(lags),
        len(nuevos) / horas, 100 * fugaces / max(1, len(nuevos)), 100 * sum(1 for a, b in con if a == b) / len(con), 100 * sum(1 for a, b in con if a == -b) / len(con), 100 * neutro))

def main():
    inst = next((a for a in sys.argv[1:] if a in ("MNQ", "MES")), "MNQ"); marco = next((a for a in sys.argv[1:] if a in ("M1", "M2")), "M1")
    vs = [v for v in C.cargar(inst, marco) if "13:30" <= v["t"][11:16] <= "20:00"]; piv = C.pivotes(vs); E = candidatos(vs)
    print("%s %s: %d velas, %d giros" % (inst, marco, len(vs), len(piv)))
    print("%-18s | retraso: med  prom  <=1 vela nunca | cambios/h duran 1 | con la vela: acuerda  en contra | neutro" % "lectura")
    for nombre, e in E.items(): medir_estados(vs, nombre, e, piv, marco)
    if "--ejemplo" in sys.argv:
        t0 = sys.argv[sys.argv.index("--ejemplo") + 1]; idx = [k for k, v in enumerate(vs) if v["t"][:16] >= t0][:12]
        print("\nEJEMPLO (hora local, cuerpo, delta):   " + "  ".join(n[:9] for n in E))
        for k in idx:
            v = vs[k]; hl = "%02d:%s" % ((int(v["t"][11:13]) - 3) % 24, v["t"][14:16])
            print("%s %+6.1f d%+5d                 " % (hl, v["c"] - v["o"], v["d"]) + "  ".join("%-9s" % ("+" if E[n][k] > 0 else "-" if E[n][k] < 0 else ".") for n in E))

if __name__ == "__main__":
    main()
