# -*- coding: utf-8 -*-
"""EL PERFIL DERECHO POR FLUJO FIRMADO, JUZGADO CONTRA PLACEBO (1.9, 2026-09-14).

Hipotesis (la lectura que la referencia hace de su 'convexity ladder'): donde los dealers quedan
LARGOS gamma por el flujo del dia (los clientes les vendieron opciones) el precio rebota
('colchon'); donde quedan CORTOS (los clientes les compraron) el precio se acelera ('tobogan').
Con la cadena viva de Rithmic tenemos compras y ventas por contrato (lado agresor), asi que el
perfil se puede calcular minuto a minuto para el libro de ES/NQ y ponerlo a prueba.

Como se juzga (misma vara para todos, sin mirar el resultado antes):
  - Cada minuto se toma el perfil 0DTE del libro de Rithmic: gamma Black-76 x (compras - ventas)
    por lado, con el signo del dealer (compra del cliente = dealer corto gamma = negativo).
  - Niveles del minuto: COLCHON = strike mas positivo a +-1 % del precio; TOBOGAN = el mas
    negativo; DOMINANTE = la barra mas larga de |gamma x volumen| (lo que ya dibujamos);
    PLACEBO = el colchon corrido 2 strikes (arriba y abajo) y un strike al azar en el radio.
  - Evento: el precio (cierre por minuto de la viva) llega al nivel del minuto anterior desde
    afuera de la banda (medio paso) y entra en ella. Se mira que toca primero en los 20 minutos
    siguientes: REBOTE (R puntos en contra de la llegada) o CRUCE (R puntos a favor).
  - Se reporta el % de rebote por tipo de nivel, con su n. Si colchon no le gana al placebo y
    tobogan no pierde contra el placebo, la lectura no sirve para operar.

Uso: python laboratorio/flujo_firmado.py [ES|NQ] [--r 4] [--radio 1.0] [--min 20]
"""
import glob, io, json, math, os, random, sys, collections

APP = os.path.join(os.environ.get("APPDATA", ""), "ATAS", "PythiaGex", "viva")
R_DEF = {"ES": 4.0, "NQ": 15.0}
PASO = {"ES": 5.0, "NQ": 10.0}
TOL = {"ES": 2.5, "NQ": 5.0}


def fi(x):
    return math.exp(-0.5 * x * x) / math.sqrt(2.0 * math.pi)


def gamma76(F, K, T, iv):
    if F <= 0 or K <= 0 or T <= 0 or iv <= 0:
        return 0.0
    v = iv * math.sqrt(T)
    d1 = (math.log(F / K) + 0.5 * v * v) / v
    return fi(d1) / (F * v)


def perfil(linea):
    """por strike del 0DTE: gexFlujo (dealer), gexVol (|gamma x vol neto|) y el precio"""
    F = float(linea["futuro"])
    filas = linea["filas"]
    if not filas or F <= 0:
        return F, {}
    # los campos cambiaron entre versiones (Gamma Vivo grababa 13, Gamma Hoy 11): se leen por nombre
    campos = (linea.get("campos") or "strike,dias,es_call,oi,iv,bid,ask,vol_hoy,vol_cinta,vol_compra,vol_venta").split(",")
    ix = {n: i for i, n in enumerate(campos)}
    iK, iD, iC, iIv, iVh, iComp, iVent = ix["strike"], ix["dias"], ix["es_call"], ix["iv"], ix["vol_hoy"], ix["vol_compra"], ix["vol_venta"]
    dias0 = min(f[iD] for f in filas)
    flujo = collections.defaultdict(float)
    vol = collections.defaultdict(float)
    for f in filas:
        K, dias, esc, iv, vh, comp, vent = f[iK], f[iD], f[iC], f[iIv], f[iVh], f[iComp], f[iVent]
        if dias > max(1.0, dias0 + 0.01) or iv <= 0:
            continue
        T = max(dias, 1 / 1440.0) / 365.0
        g = gamma76(F, K, T, iv) * 100.0 * F * F * 0.01
        flujo[K] += -(g * (comp - vent))
        vol[K] += g * vh * (1 if esc else -1)
    return F, {K: (flujo[K], vol[K]) for K in flujo}


def cargar(raiz):
    series = []
    for p in sorted(glob.glob(os.path.join(APP, "viva-%s-*.jsonl" % raiz))):
        dia = []
        with io.open(p, encoding="utf-8") as f:
            for l in f:
                try:
                    d = json.loads(l)
                except Exception:
                    continue
                if d.get("filas"):
                    dia.append(d)
        if len(dia) >= 60:
            series.append((os.path.basename(p), dia))
    return series


def niveles(F, per, radio_pct, paso, rnd):
    cerca = {K: v for K, v in per.items() if abs(K - F) <= F * radio_pct / 100.0}
    if len(cerca) < 3:
        return {}
    out = {}
    pos = [(v[0], K) for K, v in cerca.items() if v[0] > 0]
    neg = [(v[0], K) for K, v in cerca.items() if v[0] < 0]
    if pos:
        out["colchon"] = max(pos)[1]
    if neg:
        out["tobogan"] = min(neg)[1]
    out["dominante"] = max(cerca.items(), key=lambda kv: abs(kv[1][1]))[0]
    if "colchon" in out:
        out["placebo+2"] = out["colchon"] + 2 * paso
        out["placebo-2"] = out["colchon"] - 2 * paso
    out["placebo_azar"] = rnd.choice(sorted(cerca))
    return out


def juzgar(raiz, R, radio_pct, minutos, semilla=7):
    rnd = random.Random(semilla)
    paso, tol = PASO[raiz], TOL[raiz]
    res = collections.defaultdict(lambda: [0, 0])   # tipo -> [rebotes, eventos]
    dias_usados = 0
    for nombre, dia in cargar(raiz):
        precios = [float(d["futuro"]) for d in dia]
        nivs = []
        for d in dia:
            F, per = perfil(d)
            nivs.append(niveles(F, per, radio_pct, paso, rnd))
        n_ev = 0
        for t in range(2, len(dia) - minutos):
            F0, F1 = precios[t - 1], precios[t]
            for tipo, nivel in nivs[t - 1].items():
                # entra en la banda del nivel (medio paso) desde afuera; con cierres por minuto no hay mas fino
                if abs(F0 - nivel) <= tol or abs(F1 - nivel) > tol:
                    continue
                d = 1 if F0 < nivel else -1          # llega subiendo (+1) o bajando (-1)
                rebote = cruce = False
                for k in range(t + 1, min(len(precios), t + 1 + minutos)):
                    p = precios[k]
                    if (p - nivel) * d <= -R:
                        rebote = True; break
                    if (p - nivel) * d >= R:
                        cruce = True; break
                if rebote or cruce:
                    res[tipo][1] += 1; n_ev += 1
                    if rebote:
                        res[tipo][0] += 1
        dias_usados += 1
        print("  %s: %d minutos, %d eventos" % (nombre, len(dia), n_ev))
    print("\n%s: %d dias, R=%.0f pts, radio %.1f %%, ventana %d min, paso %.0f" % (raiz, dias_usados, R, radio_pct, minutos, paso))
    print("  %-14s %8s %8s   %s" % ("nivel", "rebotes", "eventos", "% rebote (intervalo 95 %)"))
    for tipo in ["colchon", "tobogan", "dominante", "placebo+2", "placebo-2", "placebo_azar"]:
        r, n = res[tipo]
        if n == 0:
            print("  %-14s %8s %8s   sin eventos" % (tipo, "-", "-")); continue
        p = r / n; se = math.sqrt(p * (1 - p) / n)
        print("  %-14s %8d %8d   %5.1f %%  (%4.1f - %4.1f)" % (tipo, r, n, 100 * p, 100 * max(0, p - 1.96 * se), 100 * min(1, p + 1.96 * se)))
    return res


if __name__ == "__main__":
    a = sys.argv[1:]
    raiz = next((x for x in a if x in ("ES", "NQ")), "ES")
    def arg(k, d):
        return float(a[a.index(k) + 1]) if k in a else d
    juzgar(raiz, arg("--r", R_DEF[raiz]), arg("--radio", 1.0), int(arg("--min", 20)))
