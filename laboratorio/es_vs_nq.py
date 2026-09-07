# -*- coding: utf-8 -*-
"""ES CONTRA NQ: LOS NIVELES SE RESPETAN MAS EN UNO QUE EN EL OTRO?

Pregunta del operador (2026-09-07): "los numeros se respetan mucho mas como
soportes y resistencias en NQ que en ES; capaz toman ES como contexto y
atacan NQ". No se contesta con la vista: se mide, con el mismo metodo para
los dos y SIEMPRE contra placebo (ver medir_respeto.py y laboratorio/).

DOS FUENTES, LAS DOS ESCRITAS POR EL PROPIO INDICADOR EN ATAS:
  1. centinela-<inst>-M5.jsonl : una linea por vela de 5 min con O/H/L/C y
     los niveles prometidos en ese momento (muros, zero, 8 dominantes), ya en
     precio de futuro. Toques con maximos y minimos, que es lo honesto.
  2. AUDIT del log de Gamma Vivo: una lectura por minuto con el precio y los
     picos (dominantes), muros y zero. Mas dias, pero un solo precio por
     minuto.

QUE ES "RESPETO": el precio venia de lejos, entro en la tolerancia del nivel,
y en los 15 minutos siguientes se alejo mas de lo que lo atraveso.
PLACEBO: el mismo nivel corrido a OTRO strike (nunca entre strikes: ver la
memoria "la muestra son niveles"). Si el nivel real no le gana al placebo, no
informa nada.
ESCALA: las tolerancias van en funcion del rango tipico de cada instrumento,
porque el NQ se mueve unas 3 a 4 veces mas en puntos que el ES.

TERCERA PREGUNTA, la del operador: en los momentos en que el ES toca un nivel
suyo, el NQ se da vuelta mas que en cualquier otro momento?
"""
import io, json, os, re, statistics as st
from datetime import datetime, timedelta

ATAS = os.path.join(os.environ.get("APPDATA", ""), "ATAS")
LOG = os.path.join(ATAS, "pythiagex-gammavivo.log")
VENTANA_MIN = 15
MAX_ESPERA_MIN = 30      # el "venia de lejos" tiene que ser reciente


def t_de(s):
    return datetime.strptime(s[:19], "%Y-%m-%dT%H:%M:%S")


# ------------------------------------------------------------------ velas
def velas(inst, marco="M5"):
    p = os.path.join(ATAS, "pythiagex-centinela-%s-TimeFrame-%s.jsonl" % (inst, marco))
    out = []
    for l in io.open(p, encoding="utf-8", errors="replace"):
        try:
            d = json.loads(l)
        except Exception:
            continue
        if not d.get("niv"):
            continue
        out.append(dict(t=t_de(d["t"]), o=d["o"], h=d["h"], l=d["l"], c=d["c"], niv=d["niv"]))
    out.sort(key=lambda v: v["t"])
    return out


def rango_tipico(vs):
    return st.median(v["h"] - v["l"] for v in vs) if vs else 0.0


def probar_velas(vs, sacar, tol, lejos, corr=0.0, paso_min=5):
    """Toques y frenos sobre velas con maximo y minimo."""
    toques = frenos = 0
    strikes = set()
    lejos_antes = {}
    for i, v in enumerate(vs):
        for L in sacar(v):
            if L is None:
                continue
            L = L + corr
            k = round(L, 2)
            d = v["c"] - L
            if abs(d) > lejos:
                lejos_antes[k] = (v["t"], 1 if d > 0 else -1)
                continue
            if v["l"] > L + tol or v["h"] < L - tol:
                continue
            la = lejos_antes.get(k)
            if la is None or (v["t"] - la[0]).total_seconds() > 60 * MAX_ESPERA_MIN:
                continue
            lado = la[1]
            lejos_antes.pop(k, None)
            aleja = cruza = 0.0
            ok = True
            for j in range(i + 1, len(vs)):
                dt = (vs[j]["t"] - v["t"]).total_seconds() / 60.0
                if dt > VENTANA_MIN:
                    break
                if (vs[j]["t"] - vs[j - 1]["t"]).total_seconds() > 60 * paso_min * 4:
                    ok = False       # hueco (ATAS cerrado): no se juzga
                    break
                if lado > 0:
                    aleja = max(aleja, vs[j]["h"] - L); cruza = max(cruza, L - vs[j]["l"])
                else:
                    aleja = max(aleja, L - vs[j]["l"]); cruza = max(cruza, vs[j]["h"] - L)
            if not ok:
                continue
            toques += 1
            strikes.add(round(L))
            if aleja > cruza:
                frenos += 1
    return toques, frenos, strikes


def informe_velas(titulo, vs, sacar, tol, lejos, placebos):
    t0, f0, ks = probar_velas(vs, sacar, tol, lejos)
    tp = fp = 0
    for c in placebos:
        a, b, _ = probar_velas(vs, sacar, tol, lejos, c)
        tp += a; fp += b
    if t0 < 12:
        print("  %-14s toques %3d: muestra insuficiente (strikes distintos %d)" % (titulo, t0, len(ks)))
        return None
    r, rp = 100.0 * f0 / t0, (100.0 * fp / tp if tp else float("nan"))
    # z de la diferencia de proporciones (aprox), solo orientativo
    p = (f0 + fp) / float(t0 + tp) if (t0 + tp) else 0.5
    se = (p * (1 - p) * (1.0 / t0 + (1.0 / tp if tp else 0))) ** 0.5 if tp else float("nan")
    z = (r - rp) / 100.0 / se if se and se > 0 else float("nan")
    print("  %-14s toques %3d  freno %5.1f%%  | placebo %4d toques %5.1f%%  | ventaja %+5.1f pp  z %+4.1f | strikes %d"
          % (titulo, t0, r, tp, rp, r - rp, z, len(ks)))
    return (t0, r, rp)


def mitades(vs, sacar, tol, lejos, placebos):
    """Control barato: la ventaja en cada mitad de la muestra."""
    n = len(vs) // 2
    out = []
    for parte in (vs[:n], vs[n:]):
        t0, f0, _ = probar_velas(parte, sacar, tol, lejos)
        tp = fp = 0
        for c in placebos:
            a, b, _ = probar_velas(parte, sacar, tol, lejos, c)
            tp += a; fp += b
        if t0 >= 6 and tp >= 6:
            out.append("%+.1f pp (%d toques)" % (100.0 * f0 / t0 - 100.0 * fp / tp, t0))
        else:
            out.append("sin muestra (%d toques)" % t0)
    return out


def doms(v):
    return [v["niv"].get("dom%d" % i) for i in range(8)]


# ------------------------------------------------------------------ AUDIT
def audit():
    es, nq = [], []
    for l in io.open(LOG, encoding="utf-8", errors="replace"):
        if "AUDIT" not in l:
            continue
        m = re.match(r"(\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d)", l)
        if not m:
            continue
        def g(c):
            mm = re.search(r"\b" + c + r"=([\d.\-]+)", l)
            return float(mm.group(1)) if mm else None
        sp = g("spot_idx")
        if sp is None:
            continue
        base = g("base") or 0.0
        mp = re.search(r"picos=([\d./]+)", l)
        picos = []
        if mp:
            for x in mp.group(1).split("/"):
                try:
                    picos.append(float(x))
                except ValueError:
                    pass
        f = dict(t=t_de(m.group(1)), px=sp + base, picos=picos,
                 mp=(g("majorpos") + base) if g("majorpos") else None,
                 mn=(g("majorneg") + base) if g("majorneg") else None,
                 z=(g("zero") + base) if g("zero") and g("zero") > 0 else None)
        (nq if sp > 12000 else es).append(f)
    for s in (es, nq):
        s.sort(key=lambda f: f["t"])
    # una lectura por minuto (los dos graficos de MES escriben la misma)
    def unico(s):
        out, ult = [], None
        for f in s:
            k = f["t"].replace(second=0)
            if k != ult:
                out.append(f); ult = k
        return out
    return unico(es), unico(nq)


def probar_px(fs, sacar, tol, lejos, corr=0.0):
    toques = frenos = 0
    strikes = set()
    lejos_antes = {}
    for i, f in enumerate(fs):
        for L in sacar(f):
            if L is None:
                continue
            L = L + corr
            k = round(L, 2)
            d = f["px"] - L
            if abs(d) > lejos:
                lejos_antes[k] = (f["t"], 1 if d > 0 else -1)
                continue
            if abs(d) > tol:
                continue
            la = lejos_antes.get(k)
            if la is None or (f["t"] - la[0]).total_seconds() > 60 * MAX_ESPERA_MIN:
                continue
            lado = la[1]
            lejos_antes.pop(k, None)
            aleja = cruza = 0.0
            for j in range(i + 1, len(fs)):
                dt = (fs[j]["t"] - f["t"]).total_seconds() / 60.0
                if dt > VENTANA_MIN:
                    break
                e = (fs[j]["px"] - L) * lado
                aleja = max(aleja, e); cruza = max(cruza, -e)
            toques += 1
            strikes.add(round(L))
            if aleja > cruza:
                frenos += 1
    return toques, frenos, strikes


def informe_px(titulo, fs, sacar, tol, lejos, placebos):
    t0, f0, ks = probar_px(fs, sacar, tol, lejos)
    tp = fp = 0
    for c in placebos:
        a, b, _ = probar_px(fs, sacar, tol, lejos, c)
        tp += a; fp += b
    if t0 < 12:
        print("  %-14s toques %3d: muestra insuficiente (strikes distintos %d)" % (titulo, t0, len(ks)))
        return
    r, rp = 100.0 * f0 / t0, (100.0 * fp / tp if tp else float("nan"))
    print("  %-14s toques %3d  freno %5.1f%%  | placebo %4d toques %5.1f%%  | ventaja %+5.1f pp | strikes %d"
          % (titulo, t0, r, tp, rp, r - rp, len(ks)))


# --------------------------------------------- ES toca, NQ se da vuelta?
def cruce(es_vs, nq_vs, tol_es, lejos_es):
    """Momentos en que el ES toca un nivel suyo (dominante, muro o zero).
    En esos momentos, el NQ se da vuelta mas que en el resto?
    'Darse vuelta' para el NQ, sin nivel: el movimiento de los 15 min
    siguientes tiene signo contrario al de los 15 min anteriores."""
    por_t = {v["t"]: i for i, v in enumerate(nq_vs)}

    def revierte(i):
        if i < 3 or i + 3 >= len(nq_vs):
            return None
        antes = nq_vs[i]["c"] - nq_vs[i - 3]["c"]
        despues = nq_vs[i + 3]["c"] - nq_vs[i]["c"]
        if antes == 0 or despues == 0:
            return None
        return (antes > 0) != (despues > 0)

    def toques_es(sacar):
        ts = []
        lejos_antes = {}
        for v in es_vs:
            for L in sacar(v):
                if L is None:
                    continue
                k = round(L, 2)
                d = v["c"] - L
                if abs(d) > lejos_es:
                    lejos_antes[k] = v["t"]; continue
                if v["l"] > L + tol_es or v["h"] < L - tol_es:
                    continue
                la = lejos_antes.get(k)
                if la is None or (v["t"] - la).total_seconds() > 60 * MAX_ESPERA_MIN:
                    continue
                lejos_antes.pop(k, None)
                ts.append(v["t"])
        return ts

    momentos = set(toques_es(doms) + toques_es(lambda v: [v["niv"].get("wall_pos"), v["niv"].get("wall_neg")])
                   + toques_es(lambda v: [v["niv"].get("zero")]))
    en, fuera = [], []
    for t, i in por_t.items():
        r = revierte(i)
        if r is None:
            continue
        (en if t in momentos else fuera).append(r)
    if len(en) < 8:
        print("  ES toca -> NQ: solo %d momentos coincidentes, no alcanza" % len(en))
        return
    print("  ES toca un nivel -> el NQ se da vuelta el %.1f%% (%d momentos) | el resto del tiempo %.1f%% (%d)"
          % (100.0 * sum(en) / len(en), len(en), 100.0 * sum(fuera) / len(fuera), len(fuera)))


def main():
    print("=" * 96)
    print("ES CONTRA NQ: RESPETO DE LOS NIVELES, MISMO METODO, CADA UNO CONTRA SU PLACEBO")
    print("=" * 96)
    es5, nq5 = velas("MES"), velas("MNQ")
    # la misma ventana para los dos
    if es5 and nq5:
        t0 = max(es5[0]["t"], nq5[0]["t"]); t1 = min(es5[-1]["t"], nq5[-1]["t"])
        es5 = [v for v in es5 if t0 <= v["t"] <= t1]; nq5 = [v for v in nq5 if t0 <= v["t"] <= t1]
        print("velas de 5 min con niveles: ES %d, NQ %d, de %s a %s" % (len(es5), len(nq5), t0, t1))
    for nombre, vs, placebos in (("ES", es5, (-35, -25, -15, 15, 25, 35)), ("NQ", nq5, (-140, -100, -60, 60, 100, 140))):
        if not vs:
            print(nombre, "sin velas"); continue
        rt = rango_tipico(vs)
        tol, lejos = 0.6 * rt, 2.5 * rt
        print("\n%s  rango tipico de una vela de 5 min %.2f pts -> toque = a %.2f pts, venir de mas de %.2f" % (nombre, rt, tol, lejos))
        informe_velas("dominantes", vs, doms, tol, lejos, placebos)
        informe_velas("call wall", vs, lambda v: [v["niv"].get("wall_pos")], tol, lejos, placebos)
        informe_velas("put wall", vs, lambda v: [v["niv"].get("wall_neg")], tol, lejos, placebos)
        informe_velas("zero gamma", vs, lambda v: [v["niv"].get("zero")], tol, lejos, placebos)
        print("  mitades (dominantes): %s" % " / ".join(mitades(vs, doms, tol, lejos, placebos)))

    print("\n" + "-" * 96)
    print("LA PREGUNTA DEL OPERADOR: cuando el ES toca un nivel suyo, el NQ se da vuelta?")
    if es5 and nq5:
        rt = rango_tipico(es5)
        cruce(es5, nq5, 0.6 * rt, 2.5 * rt)

    print("\n" + "-" * 96)
    print("SEGUNDA FUENTE: una lectura por minuto del log (mas dias, un solo precio por minuto)")
    es, nq = audit()
    for nombre, fs, placebos in (("ES", es, (-35, -25, -15, 15, 25, 35)), ("NQ", nq, (-140, -100, -60, 60, 100, 140))):
        if len(fs) < 200:
            print(nombre, "lecturas insuficientes:", len(fs)); continue
        difs = [abs(fs[i]["px"] - fs[i - 1]["px"]) for i in range(1, len(fs))
                if (fs[i]["t"] - fs[i - 1]["t"]).total_seconds() <= 120]
        mv = st.median(difs) if difs else 0.5
        tol, lejos = max(3.0 * mv, 0.5), max(10.0 * mv, 2.0)
        dias = sorted(set(f["t"].date() for f in fs))
        print("\n%s  %d lecturas en %d dias (%s..%s), movimiento tipico por minuto %.2f -> toque a %.2f, lejos %.2f"
              % (nombre, len(fs), len(dias), dias[0], dias[-1], mv, tol, lejos))
        informe_px("dominantes", fs, lambda f: f["picos"], tol, lejos, placebos)
        informe_px("call wall", fs, lambda f: [f["mp"]], tol, lejos, placebos)
        informe_px("put wall", fs, lambda f: [f["mn"]], tol, lejos, placebos)
        informe_px("zero gamma", fs, lambda f: [f["z"]], tol, lejos, placebos)

    print("\nCOMO LEERLO: la ventaja es lo unico que cuenta. Un freno del 70 % con placebo del 70 % es cero.")
    print("Menos de ~15 strikes distintos es anecdota, no prueba. Las mitades con signo distinto son ruido.")


if __name__ == "__main__":
    main()
