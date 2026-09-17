# -*- coding: utf-8 -*-
"""verdes_50 — QUE HAY EN EL LIBRO VIVO DE NQ AHORA: por vencimiento y por strike cerca del precio, interes abierto, volumen y gamma x OI / gamma x volumen,
y que raya elige la regla de noche (la mas fuerte por OI de cada lado dentro de 100 pts, empate 20 % -> la mas cercana). Recalculo independiente del indicador."""
import os, sys, json, math
from datetime import datetime
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import capas_nq as C
VIVA = os.path.join(os.environ["APPDATA"], "ATAS", "PythiaGex", "viva")
dia = sys.argv[1] if len(sys.argv) > 1 else datetime.utcnow().strftime("%Y-%m-%d"); hora = sys.argv[2] if len(sys.argv) > 2 else "99"
p = os.path.join(VIVA, "viva-NQ-%s.jsonl" % dia); ult = None
for l in open(p, encoding="utf-8", errors="replace"):
    l = l.strip()
    if len(l) > 40 and l.endswith("}"):
        try: r = json.loads(l)
        except Exception: continue
        if r["ts"][11:16] <= hora: ult = r
r = ult; fut = float(r["futuro"]); print("foto %s | futuro %.2f | %d contratos en la foto" % (r["ts"], fut, len(r["filas"])))
venc = {}
for x in r["filas"]:
    d = round(float(x[1]), 3); v = venc.setdefault(d, dict(n=0, oi=0.0, vol=0.0, con_oi=0)); v["n"] += 1; v["oi"] += float(x[3]); v["vol"] += float(x[7]); v["con_oi"] += 1 if float(x[3]) > 0 else 0
print("vencimientos (dias -> contratos, con OI > 0, OI total, volumen de hoy):")
for d in sorted(venc): print("   %7.3f dias: %4d contratos | con OI %4d | OI %7.0f | vol %6.0f" % (d, venc[d]["n"], venc[d]["con_oi"], venc[d]["oi"], venc[d]["vol"]))
mas_cerca = min(d for d in venc if d >= 0); tope = max(1.0, mas_cerca + 0.01)
print("horizonte Hoy: entran vencimientos hasta %.2f dias" % tope)
por = {}
for x in r["filas"]:
    K, di, call, oi, iv, vol, k0 = float(x[0]), float(x[1]), float(x[2]) >= 0.5, float(x[3]), float(x[4]), float(x[7]), float(x[11]) if len(x) > 11 else 0
    if di > tope or iv <= 0: continue
    T = max(di, 1 / 1440.0) / 365.0; g = C.gamma76(fut, K, T, iv) * 100 * fut * fut * 0.01; s = 1 if call else -1
    e = por.setdefault(K, dict(k0=k0, oi_c=0, oi_p=0, vol=0, gex_oi=0.0, gex_vol=0.0)); e["oi_c" if call else "oi_p"] += oi; e["vol"] += vol; e["gex_oi"] += s * g * oi; e["gex_vol"] += s * g * vol
print("\nstrikes a +-130 pts del precio (K dibujado | strike crudo | OI call/put | vol | gamma x OI en M | gamma x vol en M):")
for K in sorted(por):
    if abs(K - fut) <= 130:
        e = por[K]; print("   %9.2f | %7.0f | OI %5.0f / %5.0f | vol %5.0f | GEX-OI %8.0f M | GEX-vol %7.0f M %s" % (K, e["k0"], e["oi_c"], e["oi_p"], e["vol"], e["gex_oi"] / 1e6, e["gex_vol"] / 1e6, "<-- precio" if abs(K - fut) < 13 else ""))
for lado, nombre in ((1, "ARRIBA"), (-1, "ABAJO")):
    c = [(K, abs(e["gex_oi"])) for K, e in por.items() if abs(K - fut) <= 100 and (K - fut) * lado > 0 and e["gex_oi"] != 0]
    if not c: print(nombre, ": sin candidatos por OI"); continue
    mx = max(v for _, v in c); el = sorted([(abs(K - fut), K, v) for K, v in c if v >= 0.8 * mx])[0]
    print("regla de noche, %s: la mas fuerte por OI = %.2f (%.0f M); elegida (empate 20 %% -> la mas cercana) = %.2f (%.0f M), a %.0f pts del precio" % (nombre, max(c, key=lambda z: z[1])[0], mx / 1e6, el[1], el[2] / 1e6, el[0]))
