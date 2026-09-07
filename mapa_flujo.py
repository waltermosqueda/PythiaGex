# -*- coding: utf-8 -*-
"""EL MAPA DEL FLUJO DE HOY, CONTRA EL MAPA DE AYER.

POR QUE IMPORTA
Todos los tableros de GEX -- este incluido, y GEXbot tambien -- construyen
sobre el INTERES ABIERTO, que la OCC consolida de noche. O sea que el mapa es
siempre el de ayer, y eso vale igual pagando lo que se pague.

Lo que si es de hoy es el VOLUMEN: contratos operados en la rueda de hoy,
strike por strike. La cadena de CBOE lo trae en vol_call y vol_put. No hace
falta el enganche de Rithmic, que ademas esta roto.

QUE NO ES
Esto NO reemplaza al mapa de gamma. El interes abierto dice donde estan las
posiciones que hay que cubrir; el volumen dice donde se estan armando o
cerrando AHORA. Son dos preguntas distintas y conviene mirarlas separadas.
"""
import json, io, os, math, sys

APP = os.environ.get("APPDATA", "")
TASA = 0.0375
PISO_DIAS = 0.02


def fi(x):
    return math.exp(-0.5 * x * x) / math.sqrt(2.0 * math.pi)


def gamma_bs(S, K, T, iv, r=TASA):
    if S <= 0 or K <= 0 or T <= 0 or iv <= 0:
        return 0.0
    v = iv * math.sqrt(T)
    d1 = (math.log(S / K) + (r + 0.5 * iv * iv) * T) / v
    return fi(d1) / (S * v)


def cargar(raiz="ES"):
    p = os.path.join(APP, "ATAS", "pythiagex-cadena-usada-%s.json" % raiz)
    if not os.path.exists(p):
        return None
    d = json.load(io.open(p, encoding="utf-8"))
    c = d["cadena"]
    ix = {n: i for i, n in enumerate(c["campos"].split(","))}
    dias = [v["dias"] for v in c["vencimientos"]]
    filas = []
    for f in c["filas"]:
        v = int(f[ix["venc"]])
        if v < 0 or v >= len(dias):
            continue
        filas.append(dict(
            K=float(f[ix["strike"]]), dias=dias[v],
            oiC=float(f[ix["oi_call"]] or 0), oiP=float(f[ix["oi_put"]] or 0),
            ivC=float(f[ix["iv_call"]] or 0), ivP=float(f[ix["iv_put"]] or 0),
            volC=float(f[ix["vol_call"]] or 0), volP=float(f[ix["vol_put"]] or 0)))
    return dict(base=float(d["base"]), spot=float(c["spot_idx"]),
                ts=c["ts"], edad=float(d.get("edad_min", 0)), filas=filas,
                contrato=d.get("contrato"))


def perfil(filas, S, campo_c, campo_p, dias_max=7.0, mult=100.0):
    """GEX por strike, en dolares por 1 % de movimiento."""
    d = {}
    for f in filas:
        if f["dias"] > dias_max:
            continue
        T = max(f["dias"], PISO_DIAS) / 365.0
        g = (gamma_bs(S, f["K"], T, f["ivC"]) * f[campo_c]
             - gamma_bs(S, f["K"], T, f["ivP"]) * f[campo_p]) * mult * S * S * 0.01
        if g:
            d[f["K"]] = d.get(f["K"], 0.0) + g
    return d


def muros(p, S):
    ar = {k: v for k, v in p.items() if k > S}
    ab = {k: v for k, v in p.items() if k < S}
    return (max(ar, key=ar.get) if ar else None,
            min(ab, key=ab.get) if ab else None)


def cruce(p, S):
    ks = sorted(p)
    ac, prev, pk = 0.0, None, None
    for k in ks:
        ac += p[k]
        if prev is not None and ((prev < 0 <= ac) or (prev > 0 >= ac)):
            return pk + (k - pk) * (-prev) / (ac - prev) if ac != prev else k
        prev, pk = ac, k
    return None


def main(raiz="ES"):
    d = cargar(raiz)
    if not d:
        print("no hay cadena de %s en disco" % raiz); return
    S, b = d["spot"], d["base"]
    print("=" * 72)
    print("MAPA DE HOY vs MAPA DE AYER   --   %s (%s)" % (raiz, d["contrato"]))
    print("=" * 72)
    print("cadena %s   (%.1f min de antiguedad)" % (d["ts"], d["edad"]))
    print("indice %.2f   base %+.2f   futuro %.2f" % (S, b, S + b))

    volT = sum(f["volC"] + f["volP"] for f in d["filas"])
    oiT = sum(f["oiC"] + f["oiP"] for f in d["filas"])
    print("interes abierto (de AYER): %,d contratos".replace(",", "") % int(oiT))
    print("volumen operado (de HOY) : %d contratos   = %.1f%% del interes abierto"
          % (int(volT), 100.0 * volT / max(1, oiT)))

    poi = perfil(d["filas"], S, "oiC", "oiP")
    pvo = perfil(d["filas"], S, "volC", "volP")
    if not poi or not pvo:
        print("perfil vacio"); return

    moi = muros(poi, S)
    mvo = muros(pvo, S)
    zoi, zvo = cruce(poi, S), cruce(pvo, S)

    print()
    print("                        por INTERES ABIERTO      por VOLUMEN DE HOY")
    def li(nom, a, bb):
        fa = "%.2f" % (a + b) if a else "---"
        fb = "%.2f" % (bb + b) if bb else "---"
        dif = ("%+.2f" % (bb - a)) if (a and bb) else ""
        print("  %-20s  %14s  %20s   %s" % (nom, fa, fb, dif))
    li("call wall", moi[0], mvo[0])
    li("put wall", moi[1], mvo[1])
    li("zero gamma", zoi, zvo)
    print("  %-20s  %13.2f mM %17.2f mM"
          % ("neto", sum(poi.values()) / 1e9, sum(pvo.values()) / 1e9))

    print()
    print("LOS 8 STRIKES MAS OPERADOS HOY  (donde se esta reescribiendo el mapa)")
    porK = {}
    for f in d["filas"]:
        porK[f["K"]] = porK.get(f["K"], 0.0) + f["volC"] + f["volP"]
    tot = sum(porK.values()) or 1
    for k in sorted(porK, key=lambda x: -porK[x])[:8]:
        oi_k = sum(f["oiC"] + f["oiP"] for f in d["filas"] if f["K"] == k)
        reno = 100.0 * porK[k] / max(1, oi_k)
        print("   %8.2f (futuro %8.2f)   %7d contratos  %4.1f%% del flujo   renueva %5.1f%% de su OI"
              % (k, k + b, int(porK[k]), 100 * porK[k] / tot, reno))

    print()
    print("COINCIDEN LOS DOS MAPAS?")
    toi = set(sorted(poi, key=lambda x: -abs(poi[x]))[:8])
    tvo = set(sorted(pvo, key=lambda x: -abs(pvo[x]))[:8])
    print("   de los 8 strikes mas fuertes, coinciden %d" % len(toi & tvo))
    print("   solo en el de ayer : %s" % sorted(toi - tvo))
    print("   solo en el de hoy  : %s" % sorted(tvo - toi))


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "ES")
