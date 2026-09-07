# -*- coding: utf-8 -*-
"""STRIKE CONTRA STRIKE: LA UNICA COMPARACION SIN TRAMPA.

EL PROBLEMA QUE RESUELVE
En `tape.py` el nivel real siempre cae sobre un strike y el placebo (11, 19 o
31 puntos) siempre cae ENTRE strikes, porque los strikes de SPX estan todos en
multiplos de 5. Entonces el nivel real tiene DOS cosas que el placebo no
tiene: gamma acumulada y ser un numero redondo. El precio reacciona a los
numeros redondos por razones que no tienen nada que ver con las opciones, asi
que cualquier ventaja podria ser redondez disfrazada de gamma.

LA SOLUCION
No comparar nivel contra no-nivel. Comparar STRIKE CONTRA STRIKE.

Se miran solamente los minutos en que el precio esta parado sobre algun
strike, y se pregunta: corre mas rapido la cinta cuando ese strike tiene mucha
gamma que cuando tiene poca?

Todas las observaciones son igual de redondas. Todas son strikes. Lo unico que
cambia entre los dos grupos es la gamma. Si aun asi aparece diferencia, es
gamma. Si desaparece, era redondez -- y eso tambien es un hallazgo, porque
significa que las lineas funcionan por una razon distinta de la que dicen.

QUE NO PRUEBA
Nada sobre direccion. Solo si hay mas o menos actividad.
"""
import sys, os, argparse
import statistics as st

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cargar
import puntuar
import tape

PASO = 5.0           # separacion entre strikes de SPX, verificada en la cadena
TOL = 1.5            # cuanto cuenta como "parado sobre" el strike, en puntos
MULT = 100.0         # multiplicador de SPX


def gex_por_strike(f, solo_hoy=False):
    """Gamma en dolares por strike, con el signo de la convencion del dealer.

    Con solo_hoy se queda unicamente con lo que vence en el dia. Sirve para
    preguntar si lo que separa a un strike movido de uno quieto es toda su
    gamma o solamente la que se apaga hoy.
    """
    S = f["spot"]
    d = {}
    for x in f["filas"]:
        if x["oi"] <= 0 or x["gamma"] <= 0:
            continue
        if solo_hoy and x["dias"] >= 1.0:
            continue
        g = x["gamma"] * x["oi"] * MULT * S * S * 0.01
        d[x["K"]] = d.get(x["K"], 0.0) + (g if x["call"] else -g)
    return d


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dia", default="20260903")
    ap.add_argument("--dia-iso", default="2026-09-03")
    ap.add_argument("--cent", default=os.path.join(tape.DIR_CENT,
                                                   "prueba2-ticks-MES-M1.jsonl"))
    ap.add_argument("--cada", type=int, default=4)
    ap.add_argument("--causal", action="store_true")
    ap.add_argument("--solo-hoy", action="store_true",
                    help="usar solo la gamma que vence hoy")
    a = ap.parse_args()

    cent = tape.leer_centinela(a.cent)
    if not cent:
        print("no hay filas del centinela"); return
    velas = cargar.velas(a.dia_iso)
    ph = {v["hora"]: v for v in velas}
    solape = sorted(set(ph) & set(r["et"] for r in cent))
    print("minutos que se solapan: %d" % len(solape))
    if len(solape) < 60:
        print("muestra insuficiente"); return
    dentro = set(solape)
    _, norm = tape.normalizar([r for r in cent if r["et"] in dentro])
    hmin, hmax = min(solape), max(solape)

    fotos = []
    for r in cargar.fotos(a.dia)[::a.cada]:
        f = cargar.leer_foto(r)
        if not f:
            continue
        h = f["t"].hour * 60 + f["t"].minute + puntuar.OFF_MIN
        if hmin - 30 <= h <= hmax:
            f["h"] = h
            f["gex"] = gex_por_strike(f, a.solo_hoy)
            fotos.append(f)
    print("fotos usables: %d" % len(fotos))
    if len(fotos) < 10:
        print("muestra insuficiente de fotos"); return

    retraso = tape.RETRASO_CBOE if a.causal else 0

    def foto_vigente(m):
        """La foto mas nueva que YA estaba disponible en el minuto m."""
        cand = [f for f in fotos if f["h"] + retraso <= m]
        return cand[-1] if cand else None

    # una observacion por (minuto, strike que el precio toca en ese minuto)
    obs = []
    for m in solape:
        v = ph.get(m)
        nm = norm.get(m)
        if not v or nm is None:
            continue
        f = foto_vigente(m)
        if not f or not f["gex"]:
            continue
        k = PASO * round(v["l"] / PASO)
        while k <= v["h"] + TOL:
            if v["l"] - TOL <= k <= v["h"] + TOL and k in f["gex"]:
                obs.append((abs(f["gex"][k]), nm, m, k))
            k += PASO
    print("observaciones (minuto sobre un strike): %d" % len(obs))
    if len(obs) < 40:
        print("muestra insuficiente"); return

    gs = sorted(o[0] for o in obs)
    q1 = gs[len(gs) // 4]
    q3 = gs[3 * len(gs) // 4]
    bajo = [o[1] for o in obs if o[0] <= q1]
    alto = [o[1] for o in obs if o[0] >= q3]

    print()
    print("STRIKES CON MUCHA GAMMA CONTRA STRIKES CON POCA  (%s)"
          % ("solo lo que vence HOY" if a.solo_hoy else "toda la gamma"))
    print("  todas las observaciones son strikes: la redondez no puede explicar nada")
    print()
    print("  strikes de POCA gamma  (hasta %5.2f B) : %3d minutos, cinta %5.3f"
          % (q1 / 1e9, len(bajo), st.median(bajo)))
    print("  strikes de MUCHA gamma (desde %5.2f B) : %3d minutos, cinta %5.3f"
          % (q3 / 1e9, len(alto), st.median(alto)))
    dif = st.median(alto) - st.median(bajo)
    print("  diferencia: %+0.3f" % dif)

    # cuantos minutos distintos hay de verdad detras de esas observaciones:
    # el mismo minuto puede tocar dos strikes y contarse dos veces
    mb = len(set(o[2] for o in obs if o[0] <= q1))
    ma = len(set(o[2] for o in obs if o[0] >= q3))
    print("  (minutos DISTINTOS: %d con poca gamma, %d con mucha)" % (mb, ma))

    print()
    print("  Lectura: positivo = la cinta corre mas rapido sobre los strikes")
    print("  cargados de gamma que sobre los strikes flacos. Como los dos grupos")
    print("  son strikes, lo que quede no puede ser el efecto del numero redondo.")

    # ---- SEGUNDA CAPA DE LA MISMA TRAMPA ----------------------------------
    # Los strikes con mas interes abierto tienden a ser los mas redondos
    # (7700, 7750) y los flacos los intermedios (7735). Si no separo por
    # redondez, podria haber cambiado "redondo contra no redondo" por "muy
    # redondo contra poco redondo", que es el mismo error con otra ropa.
    def clase(k):
        if k % 50 == 0:
            return "multiplo de 50"
        if k % 25 == 0:
            return "multiplo de 25"
        if k % 10 == 0:
            return "multiplo de 10"
        return "multiplo de 5"

    print()
    print("CONTROL DE REDONDEZ: el efecto DENTRO de cada tipo de strike")
    print("  si solo aparece en los mas redondos, era redondez y no gamma")
    print()
    for c in ("multiplo de 50", "multiplo de 25", "multiplo de 10", "multiplo de 5"):
        sub = [o for o in obs if clase(o[3]) == c]
        if len(sub) < 12:
            print("  %-16s  %3d observaciones -- muy pocas" % (c, len(sub)))
            continue
        g2 = sorted(o[0] for o in sub)
        c1, c3 = g2[len(g2) // 3], g2[2 * len(g2) // 3]
        b = [o[1] for o in sub if o[0] <= c1]
        al = [o[1] for o in sub if o[0] >= c3]
        if len(b) < 4 or len(al) < 4:
            print("  %-16s  %3d observaciones -- no se puede partir" % (c, len(sub)))
            continue
        print("  %-16s  %3d obs   poca gamma %5.3f   mucha gamma %5.3f   dif %+0.3f"
              % (c, len(sub), st.median(b), st.median(al), st.median(al) - st.median(b)))

    print()
    print("REDONDEZ SOLA: cinta por tipo de strike, sin mirar la gamma")
    for c in ("multiplo de 50", "multiplo de 25", "multiplo de 10", "multiplo de 5"):
        sub = [o[1] for o in obs if clase(o[3]) == c]
        if len(sub) < 6:
            continue
        print("  %-16s  %3d obs   cinta %5.3f" % (c, len(sub), st.median(sub)))

    # ---- LA MITAD CONTRA LA OTRA MITAD ------------------------------------
    # Con un solo dia, lo minimo es ver si el efecto aparece en las dos partes
    # del tramo o si lo trajo un unico momento raro.
    corte = (min(solape) + max(solape)) // 2
    print()
    print("MITAD CONTRA MITAD (el mismo efecto en los dos tramos?)")
    for etiqueta, sel in (("primera mitad", lambda m: m <= corte),
                          ("segunda mitad", lambda m: m > corte)):
        sub = [o for o in obs if sel(o[2])]
        if len(sub) < 12:
            print("  %-14s  %3d observaciones -- muy pocas" % (etiqueta, len(sub)))
            continue
        g2 = sorted(o[0] for o in sub)
        c1, c3 = g2[len(g2) // 3], g2[2 * len(g2) // 3]
        b = [o[1] for o in sub if o[0] <= c1]
        al = [o[1] for o in sub if o[0] >= c3]
        if len(b) < 4 or len(al) < 4:
            print("  %-14s  no se puede partir" % etiqueta)
            continue
        print("  %-14s  %3d obs   poca %5.3f   mucha %5.3f   dif %+0.3f"
              % (etiqueta, len(sub), st.median(b), st.median(al),
                 st.median(al) - st.median(b)))


if __name__ == "__main__":
    main()
