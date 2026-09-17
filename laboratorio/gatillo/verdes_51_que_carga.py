# -*- coding: utf-8 -*-
"""verdes_51 — QUE VENCIMIENTOS TRAE EL LIBRO VIVO DE NQ: por foto, cuantos contratos por vencimiento (dias), con OI, con volumen. Anoche contra esta noche."""
import os, sys, json
VIVA = os.path.join(os.environ["APPDATA"], "ATAS", "PythiaGex", "viva")
def foto(dia, hora):
    ult = None
    for l in open(os.path.join(VIVA, "viva-NQ-%s.jsonl" % dia), encoding="utf-8", errors="replace"):
        l = l.strip()
        if len(l) > 40 and l.endswith("}"):
            try: r = json.loads(l)
            except Exception: continue
            if r["ts"][11:16] <= hora: ult = r
    return ult
for dia, hora in (("2026-09-15", "22:46"), ("2026-09-16", "22:46"), ("2026-09-17", "01:30"), ("2026-09-17", "14:30"), ("2026-09-17", "19:30"), ("2026-09-17", "22:46"), ("2026-09-17", "23:59")):
    r = foto(dia, hora)
    if r is None: print(dia, hora, "sin foto"); continue
    fut = float(r["futuro"]); venc = {}
    for x in r["filas"]:
        d = round(float(x[1]), 2); v = venc.setdefault(d, [0, 0, 0.0, 0.0, 1e9, -1e9]); v[0] += 1; v[1] += 1 if float(x[3]) > 0 else 0; v[2] += float(x[3]); v[3] += float(x[7]); v[4] = min(v[4], float(x[0])); v[5] = max(v[5], float(x[0]))
    print("\nfoto %s | futuro %.2f | %d contratos" % (r["ts"], fut, len(r["filas"])))
    for d in sorted(venc): print("   vence en %5.2f dias: %4d contratos (%3d con OI) | OI %7.0f | vol hoy %7.0f | strikes dibujados %.0f a %.0f" % (d, venc[d][0], venc[d][1], venc[d][2], venc[d][3], venc[d][4], venc[d][5]))
