# -*- coding: utf-8 -*-
"""SE ACELERA EL TAPE DE FUTUROS CUANDO EL PRECIO ESTA EN UN NIVEL?

Es la afirmacion textual de GAMMAlito ("va a aumentar la velocidad del tape").
Antes solo se pudo medir con volumen de OPCIONES (laboratorio/actividad.py).
Ahora el centinela graba por vela de 1 min el volumen, las operaciones y el
delta del FUTURO (MES) junto con los niveles vigentes: se mide lo que ellos
afirman, con placebo (niveles corridos a otro strike) y normalizando por la
hora (mediana de +-20 min) para que la apertura no haga trampa.
"""
import io, json, os, statistics as st
from datetime import datetime
ATAS = os.path.join(os.environ.get("APPDATA", ""), "ATAS")

def velas(inst="MES", marco="M1"):
    out = []
    for l in io.open(os.path.join(ATAS, "pythiagex-centinela-%s-TimeFrame-%s.jsonl" % (inst, marco)), encoding="utf-8", errors="replace"):
        try: d = json.loads(l)
        except Exception: continue
        if not d.get("niv") or not d.get("vol"): continue
        d["t"] = datetime.strptime(d["t"][:19], "%Y-%m-%dT%H:%M:%S"); out.append(d)
    out.sort(key=lambda v: v["t"]); return out

def normalizar(vs, campo, radio=20):
    """cada vela dividida por la mediana de sus vecinas de +-radio velas"""
    x = [abs(v[campo]) for v in vs]; y = []
    for i in range(len(vs)):
        vec = [x[j] for j in range(max(0, i - radio), min(len(vs), i + radio + 1)) if j != i and x[j] > 0]
        y.append(x[i] / st.median(vec) if vec else float("nan"))
    return y

def en_nivel(v, tol, corr=0.0, tipos=("dom", "wall", "zero")):
    for k, L in v["niv"].items():
        if not L or not any(k.startswith(t) for t in tipos): continue
        L += corr
        if v["l"] - tol <= L <= v["h"] + tol: return True
    return False

def prueba(vs, tol, placebos, tipos, nombre):
    nv = normalizar(vs, "vol"); no = normalizar(vs, "ops"); nd = normalizar(vs, "delta")
    def resumen(corr):
        a = [(nv[i], no[i], nd[i]) for i, v in enumerate(vs) if en_nivel(v, tol, corr, tipos) and nv[i] == nv[i]]
        b = [(nv[i], no[i], nd[i]) for i, v in enumerate(vs) if not en_nivel(v, tol, corr, tipos) and nv[i] == nv[i]]
        if len(a) < 20: return None
        return (len(a), st.median(x[0] for x in a), st.median(x[1] for x in a), st.median(x[2] for x in a),
                len(b), st.median(x[0] for x in b), st.median(x[1] for x in b), st.median(x[2] for x in b))
    r = resumen(0.0)
    if not r:
        print("  %-18s sin muestra" % nombre); return
    print("  %-18s en nivel %4d velas: vol x%.2f ops x%.2f |delta| x%.2f   | fuera %4d velas: vol x%.2f ops x%.2f |delta| x%.2f"
          % (nombre, r[0], r[1], r[2], r[3], r[4], r[5], r[6], r[7]))
    acc = []
    for c in placebos:
        p = resumen(c)
        if p: acc.append((p[1], p[2], p[3], p[0]))
    if acc:
        print("  %-18s placebo (mismo nivel en otro strike): vol x%.2f ops x%.2f |delta| x%.2f (%d velas)"
              % ("", st.mean(a[0] for a in acc), st.mean(a[1] for a in acc), st.mean(a[2] for a in acc), sum(a[3] for a in acc)))

def main():
    for inst, marco, tol, plac in (("MES", "M1", 1.0, (-35, -25, -15, 15, 25, 35)), ("MNQ", "M5", 6.0, (-140, -100, -60, 60, 100, 140))):
        vs = velas(inst, marco)
        if len(vs) < 100: print(inst, marco, "velas insuficientes:", len(vs)); continue
        dias = sorted(set(v["t"].date() for v in vs))
        print("\n%s %s: %d velas con nivel y volumen, dias %s..%s, toque = nivel dentro de la vela +-%.1f pts"
              % (inst, marco, len(vs), dias[0], dias[-1], tol))
        print("  (todo normalizado por la mediana de las 40 velas vecinas: 1,00 = lo normal a esa hora)")
        prueba(vs, tol, plac, ("dom",), "dominantes")
        prueba(vs, tol, plac, ("wall",), "muros")
        prueba(vs, tol, plac, ("zero",), "zero gamma")
        prueba(vs, tol, plac, ("dom", "wall", "zero"), "cualquier nivel")

if __name__ == "__main__":
    main()
