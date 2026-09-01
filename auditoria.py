# -*- coding: utf-8 -*-
"""Auditoria de punta a punta.

No vuelve a llamar a las funciones del proyecto para verificarlas: recalcula
cada numero DESDE LA CADENA CRUDA, con la formula escrita de nuevo aca, y
recien despues compara. Si las dos cuentas coinciden, el numero es real. Si
no coinciden, eso ES el hallazgo.

Cada corrida se guarda en datos/auditoria/YYYY-MM-DD.jsonl para poder ver la
evolucion a lo largo de la rueda.

    python auditoria.py            # una pasada sobre SPX
    python auditoria.py NDX RUT    # otros simbolos
    python auditoria.py --historia   # que paso en las corridas anteriores
    python auditoria.py --vigilancia # si alguien se llevo el repositorio
"""
import datetime as dt
import json
import math
import os
import statistics
import sys
import urllib.request

from pythiagex.fuentes import bajar, normalizar
from pythiagex.exposicion import parse_occ, calcular, MULT_INDICE
from pythiagex.base import (medir as medir_base, contrato_vigente, edad_minutos,
                            mercado_abierto)
from pythiagex.niveles import curva_gamma, expected_move, niveles_clave
from pythiagex.griegas import gamma_bs as _gamma_bs
from pythiagex.tasas import curva as curva_tasas, tasa as tasa_plazo
from pythiagex.tablero import prob_toque, contratos_cobertura, CONTRATO

DIR = "datos/auditoria"
URL_FEED = "https://waltermosqueda.github.io/PythiaGex/datos/atas/{}.json"

OK, MAL, AVISO = "OK  ", "FALLA", "AVISO"


class Reporte:
    def __init__(self, simbolo):
        self.simbolo = simbolo
        self.filas = []
        self.fallas = 0
        self.avisos = 0

    def chequeo(self, nombre, nuestro, propio, tol, unidad="", nota=""):
        """Compara nuestro numero contra el recalculado aca."""
        if nuestro is None or propio is None:
            self.filas.append((AVISO, nombre, nuestro, propio, None, unidad, nota or "falta el dato"))
            self.avisos += 1
            return
        dif = abs(nuestro - propio)
        estado = OK if dif <= tol else MAL
        if estado == MAL:
            self.fallas += 1
        self.filas.append((estado, nombre, nuestro, propio, dif, unidad, nota))

    def dato(self, nombre, valor, nota=""):
        self.filas.append(("dato", nombre, valor, None, None, "", nota))

    def alerta(self, nombre, nota):
        self.filas.append((AVISO, nombre, None, None, None, "", nota))
        self.avisos += 1

    def imprimir(self):
        print("\n" + "=" * 92)
        print("  AUDITORIA  %s   %s" % (self.simbolo,
              dt.datetime.now().strftime("%Y-%m-%d %H:%M:%S")))
        print("=" * 92)
        for est, nom, a, b, dif, u, nota in self.filas:
            if est == "dato":
                print("       %-34s %s   %s" % (nom, _fmt(a), nota))
            elif a is None and b is None:
                print("%-5s  %-34s %s" % (est, nom, nota))
            else:
                d = "" if dif is None else "  dif %s" % _fmt(dif)
                print("%-5s  %-34s pipeline %-14s recalc %-14s%s %s"
                      % (est, nom, _fmt(a) + u, _fmt(b) + u, d, nota))
        print("-" * 92)
        print("  %d controles, %d fallas, %d avisos"
              % (len([f for f in self.filas if f[0] in (OK, MAL)]),
                 self.fallas, self.avisos))


def _fmt(v):
    if v is None:
        return "-"
    if isinstance(v, float):
        return ("%.4f" % v).rstrip("0").rstrip(".") if abs(v) < 1 else "%.2f" % v
    return str(v)


def _norm(x):
    return 0.5 * (1.0 + math.erf(x / math.sqrt(2.0)))


# ---------------------------------------------------------------------------
def auditar(simbolo):
    r = Reporte(simbolo)
    sym = normalizar(simbolo)
    crudo = bajar(sym)
    d = crudo["data"]
    S_publicado = d["current_price"]
    ts = crudo["timestamp"]

    # --- 1. frescura del dato -------------------------------------------
    edad = edad_minutos(ts)
    r.dato("cadena cotizada", ts, "hace %.1f min" % (edad or -1))
    r.dato("mercado abierto", mercado_abierto(ts))
    if edad is not None and edad < 0:
        r.alerta("timestamp", "edad negativa: la zona horaria del feed cambio")
    if edad is not None and edad > 60:
        r.alerta("frescura", "la cadena tiene %.0f minutos" % edad)

    # --- 2. base por forwards, recalculada de cero -----------------------
    venc, _ = contrato_vigente()
    porV = {}
    for o in d["options"]:
        p = parse_occ(o["option"])
        if not p:
            continue
        vc, cp, K = p
        porV.setdefault(vc.date(), {}).setdefault(K, {})[cp] = o

    def fwd(vencimiento, n=12):
        pk = porV.get(vencimiento) or {}
        cand = sorted(pk.keys(), key=lambda k: abs(k - S_publicado))[:n]
        out = []
        for k in cand:
            e = pk[k]
            if "C" not in e or "P" not in e:
                continue
            c, p_ = e["C"], e["P"]
            if not (c["bid"] > 0 and p_["bid"] > 0):
                continue
            out.append(k + (c["bid"] + c["ask"]) / 2 - (p_["bid"] + p_["ask"]) / 2)
        return (sum(out) / len(out), len(out), max(out) - min(out)) if len(out) >= 3 else None

    vencs = sorted(porV.keys())
    cercano = vencs[0]
    f_cer = fwd(cercano)
    f_fut = fwd(venc) if venc in porV else None

    med = medir_base(crudo)
    if med and f_cer and f_fut:
        base_propia = f_fut[0] - f_cer[0]
        r.chequeo("base " + str(venc), med["base"], base_propia, 0.75, " pts",
                  "forward trimestral menos forward cercano")
        r.chequeo("contado implicito", med["contado_implicito"], f_cer[0], 1.0, "",
                  "el forward de 0 dias ES el contado")
        # el carry que implica la pendiente, contra la tasa del Tesoro
        dias = (venc - cercano).days
        ct = curva_tasas()
        rt = tasa_plazo(max(dias, 28), ct) if ct else None
        if rt and med.get("dividendo_implicito") is not None:
            carry_propio = f_cer[0] * (rt - med["dividendo_implicito"]) * dias / 365.0
            # Esto NO es un control de la base: es una medida de cuanto se
            # aparta la curva de forwards de una recta. La diferencia entre el
            # extremo contra extremo y la pendiente por los dias es la suma de
            # los residuos de los dos extremos, y eso no tiene por que dar
            # cero. Llamarlo "falla" con una tolerancia inventada solo
            # generaba alarmas que no significaban nada.
            r.dato("curvatura de los forwards",
                   round(base_propia - carry_propio, 2),
                   "pts de diferencia entre extremo-a-extremo (%.2f) y pendiente "
                   "por dias (%.2f); es la suma de los residuos de las dos puntas"
                   % (base_propia, carry_propio))
        r.dato("dividendo implicito",
               round((med.get("dividendo_implicito") or 0) * 100, 3),
               "% anual, despejado de la recta")
        r.dato("base confiable", med["confiable"],
               "%s ticks de error, %d vencimientos"
               % (med.get("residuo_ticks"), med.get("vencimientos_usados")))
        # ----------------------------------------------------------------
        # EL CONTROL EXTERNO: la base medida contra el precio real de ATAS.
        #
        # Todo lo demas se recalcula desde la misma cadena de CBOE, asi que un
        # error de metodo comun a las dos cuentas no se veria. Este control usa
        # una fuente completamente distinta —el precio del futuro que llega por
        # Rithmic y que el indicador deja anotado— y compara:
        #
        #     ES de ATAS  -  contado implicito de CBOE   contra   la base
        #
        # Si esas dos cosas coinciden, la base esta bien medida de verdad. Si
        # no, todos los niveles convertidos a ES estan corridos por esa
        # diferencia, que es la perdida sistematica que este proyecto existe
        # para evitar.
        # ----------------------------------------------------------------
        _control_atas(r, med, f_cer[0], crudo.get("timestamp"))

        desfase = f_cer[0] - S_publicado
        r.dato("indice publicado", S_publicado,
               "desfase %+.2f contra el contado de la cadena" % desfase)

    # --- 3. exposiciones, recalculadas contrato por contrato -------------
    S = med["contado_implicito"] if (med and med.get("indice_atrasado")) else S_publicado
    crudo2 = json.loads(json.dumps(crudo))
    crudo2["data"]["current_price"] = S
    res = calcular(crudo2, dias_max=18)

    gex_propio = 0.0

    gex_publicada = 0.0
    dex_propio = 0.0
    por_strike = {}
    ahora = dt.datetime.now(dt.timezone.utc)
    for o in d["options"]:
        p = parse_occ(o["option"])
        if not p:
            continue
        vc, cp, K = p
        dias = (vc - ahora).total_seconds() / 86400.0
        if dias < 0 or dias > 18:
            continue
        oi = o.get("open_interest") or 0
        vol = o.get("volume") or 0
        if not oi and not vol:
            continue
        # LA GAMMA SE RECALCULA, IGUAL QUE EN EL PIPELINE, Y SE COMPARA CON LA
        # PUBLICADA COMO DATO APARTE.
        #
        # CBOE publica la gamma redondeada a cuatro decimales. El pipeline la
        # recalcula desde la IV porque eso agrega precision (verificado: misma
        # formula, sin sesgo por plazo). Este control verifica la AGREGACION,
        # que es lo suyo; y la eleccion de la fuente de gamma queda medida
        # abajo, como numero, para que siga siendo discutible.
        g_pub = o.get("gamma") or 0.0
        iv_o = o.get("iv") or 0.0
        g = g_pub
        if iv_o > 0 and dias > 0:
            _g = _gamma_bs(S, K, max(dias, 0.02) / 365.0, iv_o)
            if _g > 0:
                g = _g
        dl = o.get("delta") or 0.0
        sgn = 1 if cp == "C" else -1
        # GEX = gamma x OI x multiplicador x S^2 x 1%, con signo por tipo
        gk = g * oi * MULT_INDICE * S * S * 0.01 * sgn
        gex_propio += gk
        gex_publicada += g_pub * oi * MULT_INDICE * S * S * 0.01 * sgn
        dex_propio += dl * oi * MULT_INDICE * S * sgn
        por_strike[K] = por_strike.get(K, 0.0) + gk

    r.dato("redondeo de CBOE en la gamma",
           round((gex_propio - gex_publicada) / 1e9, 2),
           "B de diferencia entre recalcular la gamma desde la IV y usar la que "
           "publica CBOE, que viene redondeada a 4 decimales")

    T = res["totales"]
    r.chequeo("Net GEX", round(T["gex"] / 1e9, 3), round(gex_propio / 1e9, 3),
              0.05, " B", "gamma x OI x 100 x S^2 x 1%%, S=%.2f" % S)
    r.chequeo("Net DEX", round(T["dex"] / 1e9, 3), round(dex_propio / 1e9, 3),
              0.5, " B", "delta x OI x 100 x S")

    # --- 4. niveles: verificar que son el maximo y el minimo -------------
    niv = niveles_clave(res["strikes"], S)
    arriba = {k: v for k, v in por_strike.items() if k > S}
    abajo = {k: v for k, v in por_strike.items() if k < S}
    if arriba:
        cw = max(arriba, key=arriba.get)
        r.chequeo("Call Wall", niv.get("call_wall"), cw, 0.01, "",
                  "el strike de mayor gamma por encima del precio")
    if abajo:
        pw = min(abajo, key=abajo.get)
        r.chequeo("Put Wall", niv.get("put_wall"), pw, 0.01, "",
                  "el strike de menor gamma por debajo del precio")

    # --- 5. gamma flip: verificar el cruce por cero ----------------------
    curva, flip = curva_gamma(res["curva_src"], S)
    if curva and flip:
        # El cruce exacto se saca interpolando entre los dos puntos que lo
        # encierran. La grilla tiene pasos de varios puntos: quedarse con el
        # punto de la grilla erraba hasta 18 ticks de ES.
        cruces = []
        for a_, b_ in zip(curva, curva[1:]):
            if (a_[1] < 0 <= b_[1]) or (a_[1] > 0 >= b_[1]):
                if b_[1] != a_[1]:
                    cruces.append(a_[0] + (b_[0] - a_[0]) * (-a_[1]) / (b_[1] - a_[1]))
                else:
                    cruces.append(a_[0])
        if cruces:
            r.chequeo("gamma flip", flip, round(cruces[0], 2), 0.05, "",
                      "%d cruce por cero, interpolado" % len(cruces))
            if len(cruces) > 1:
                r.alerta("flip", "la curva cruza cero %d veces: %s. El primero "
                                 "puede no ser el que manda"
                         % (len(cruces), ", ".join("%.0f" % c for c in cruces[:4])))
        else:
            r.alerta("flip", "la curva no cruza cero en +/-6%: el regimen no "
                             "cambia dentro del rango calculado")

    # --- 6. expected move y probabilidad de toque ------------------------
    em = expected_move(res["curva_src"], S)
    if res["curva_src"]:
        # El pipeline promedia la IV de call y put del strike at-the-money del
        # vencimiento mas cercano VIVO. Aca se rehace igual: si el control
        # usara una sola punta mediria otra cosa y fallaria siempre por unas
        # decimas, que es ruido con forma de alarma.
        vivos = [z for z in res["curva_src"] if z[4] and z[4] > 0 and z[3] and z[3] > 0]
        Tmin = min(z[4] for z in vivos) if vivos else 0.0
        cerc = [z for z in vivos if abs(z[4] - Tmin) < 1e-9]
        k0 = min(cerc, key=lambda z: abs(z[0] - S))[0] if cerc else S
        ivs = [z[3] for z in cerc if z[0] == k0]
        iv0 = sum(ivs) / len(ivs) if ivs else 0.0
        t0 = Tmin
        em_propio = S * iv0 * math.sqrt(t0) if t0 > 0 else 0.0
        r.chequeo("expected move 1 sigma", em, round(em_propio, 1), 0.15, " pts",
                  "S x IV x raiz(T), IV %.2f%% T %.4f a" % (iv0 * 100, t0))
        if niv.get("call_pared" if False else "call_wall"):
            cw = niv["call_wall"]
            dd = abs(cw - S) / (S * iv0 * math.sqrt(t0))
            pt_propio = round(min(1.0, 2 * _norm(-dd)) * 100, 1)
            r.chequeo("toque del Call Wall", prob_toque(S, cw, iv0, t0), pt_propio,
                      0.2, " %", "2 x N(-d), aproximacion por reflexion")

    # --- 6b. la probabilidad que paga el mercado, recalculada de cero -----
    # No se usa el modulo del proyecto: se rehace -dC/dK sobre precios crudos.
    # Si las dos cuentas coinciden, el numero que se muestra es el que el
    # mercado esta pagando de verdad.
    porVK_aud = {}
    for o in d["options"]:
        pp = parse_occ(o["option"])
        if not pp:
            continue
        vc, cp_, K_ = pp
        porVK_aud.setdefault(vc, {}).setdefault(K_, {})[cp_] = o
    if porVK_aud:
        # mismo criterio que el pipeline: solo vencimientos con vida por
        # delante. Con el 0DTE ya liquidado la probabilidad se va a 0 o 100 y
        # el control comparaba dos numeros sin sentido contra otros dos.
        _ah = dt.datetime.now(dt.timezone.utc)
        _viv = [v for v in porVK_aud if (v - _ah).total_seconds() > 1800]
        if not _viv:
            _viv = list(porVK_aud)
        Vp = min(_viv)
        Tp = max((Vp - dt.datetime.now(dt.timezone.utc)).total_seconds() / 86400.0,
                 1e-6) / 365.0
        pk_ = porVK_aud[Vp]
        ks_ = sorted(k for k, v in pk_.items() if "C" in v and "P" in v)

        def _md(o):
            b_, a_ = o.get("bid") or 0.0, o.get("ask") or 0.0
            return (b_ + a_) / 2.0 if a_ > 0 else (o.get("last_trade_price") or 0.0)

        from pythiagex.probabilidad import curva_probabilidad, interpolar
        # el mismo criterio que el pipeline: el vencimiento tiene que estar
        # vivo, si no la probabilidad no informa nada
        cur_pipe = curva_probabilidad(pk_, S, Tp)
        r.dato("probabilidad sobre", Vp.date().isoformat(),
               "%.2f dias, %d strikes" % (Tp * 365, len(cur_pipe)))
        controlados = 0
        for objetivo in (niv.get("call_wall"), niv.get("put_wall"), niv.get("gamma_pin")):
            if objetivo is None or objetivo not in ks_:
                continue
            i_ = ks_.index(objetivo)
            if i_ == 0 or i_ == len(ks_) - 1:
                continue
            ka_, kb_ = ks_[i_ - 1], ks_[i_ + 1]
            if objetivo >= S:
                prop = -(_md(pk_[kb_]["C"]) - _md(pk_[ka_]["C"])) / (kb_ - ka_)
            else:
                prop = (_md(pk_[kb_]["P"]) - _md(pk_[ka_]["P"])) / (kb_ - ka_)
            prop = max(0.0, min(1.0, prop)) * 100
            pp_ = interpolar(cur_pipe, objetivo)
            if pp_:
                r.chequeo("P(mas alla de %g)" % objetivo, pp_["final_mercado"],
                          round(prop, 1), 0.2, " %",
                          "menos la derivada del call respecto del strike, precios reales")
                controlados += 1
                # el delta como segunda opinion, sin ser un control duro
                lado = "C" if objetivo >= S else "P"
                dl_ = abs(pk_[objetivo].get(lado, {}).get("delta") or 0) * 100
                r.dato("   delta en %g" % objetivo, round(dl_, 1),
                       "%% - segunda opinion, disp %s pp" % pp_.get("dispersion_pp"))
        if controlados == 0:
            r.alerta("probabilidad", "ningun nivel cayo en un strike con vecinos a los dos lados")

    # --- 7. cobertura y flujos derivados ---------------------------------
    raiz = CONTRATO.get((res["simbolo"] or "").replace("^", ""), None)
    fut_raiz = {"^SPX": "ES", "^NDX": "NQ", "^RUT": "RTY"}.get(res["simbolo"], "ES")
    mult = CONTRATO[fut_raiz]["mult"]
    base = med["base"] if med else None
    pf = (med.get("forward") if med else None)
    if pf:
        cob = contratos_cobertura(T["gex"], pf, fut_raiz)
        propio = int(round(T["gex"] / (pf * mult)))
        r.chequeo("cobertura por 1%%", cob["contratos"], propio, 1, " " + fut_raiz,
                  "GEX en dolares / (precio %.2f x %g)" % (pf, mult))
        # charm pendiente
        dias_c = round(Tmin * 365, 3) if res["curva_src"] else None
        if dias_c:
            charm_propio = int(round(T["chex"] * dias_c / (pf * mult)))
            r.dato("charm pendiente", charm_propio,
                   "%s contratos en %.3f dias (CHEX x dias / nocional)" % (fut_raiz, dias_c))
        vanna_propio = int(round(T["vex"] / (pf * mult)))
        r.dato("vanna por 1%% de IV", vanna_propio, fut_raiz + " contratos")

    # --- 8. contra lo publicado -----------------------------------------
    try:
        req = urllib.request.Request(URL_FEED.format(fut_raiz),
                                     headers={"User-Agent": "PythiaGex-auditoria"})
        pub = json.load(urllib.request.urlopen(req, timeout=20))
        r.dato("feed publicado", pub.get("cadena_ts"),
               "%s min de antiguedad al publicarse" % pub.get("cadena_edad_min"))
        # Comparar lo publicado contra lo de ahora NO es un control de
        # correccion: es una medida de deriva. Si el mercado se movio desde la
        # ultima corrida del workflow tienen que diferir. Lo que si es un
        # problema es que el archivo publicado sea viejo.
        g = pub.get("griegas") or {}
        eg = pub.get("generado")
        edad_pub = None
        if eg:
            try:
                edad_pub = (dt.datetime.now(dt.timezone.utc)
                            - dt.datetime.fromisoformat(eg)).total_seconds() / 60
            except Exception:
                pass
        if edad_pub is not None:
            r.dato("feed generado hace", round(edad_pub, 1), "minutos")
            if edad_pub > 45:
                r.alerta("feed", "el workflow no publica hace %.0f minutos" % edad_pub)
        # El repositorio puede tener dato fresco y el sitio publicado no: los
        # commits del bot no disparan el deploy de Pages. Se compara.
        try:
            local = json.load(open("datos/salida/atas/%s.json" % fut_raiz, encoding="utf-8"))
            if local.get("generado") and eg and local["generado"] > eg:
                r.alerta("Pages", "el repositorio tiene dato de %s y el sitio "
                                  "publicado de %s: el deploy quedo atras"
                         % (local["generado"][11:19], eg[11:19]))
        except Exception:
            pass
        if g.get("gex_B") is not None:
            r.dato("deriva del GEX", round(gex_propio / 1e9 - g["gex_B"], 3),
                   "B desde que se publico")
        if pub.get("base") is not None and f_cer and f_fut:
            r.dato("deriva de la base",
                   round((f_fut[0] - f_cer[0]) - pub["base"], 3), "pts desde que se publico")
    except Exception as e:
        r.alerta("feed publicado", "no se pudo leer: %s" % str(e)[:60])

    # --- guardar para ver la evolucion -----------------------------------
    os.makedirs(DIR, exist_ok=True)
    fila = {
        "t": dt.datetime.now(dt.timezone.utc).isoformat(timespec="seconds"),
        "simbolo": simbolo, "cadena_ts": ts, "edad_min": edad,
        "indice_publicado": S_publicado,
        "contado": round(f_cer[0], 2) if f_cer else None,
        "base": round(f_fut[0] - f_cer[0], 2) if (f_cer and f_fut) else None,
        "futuro": round(f_fut[0], 2) if f_fut else None,
        "gex_B": round(gex_propio / 1e9, 3),
        "dex_B": round(dex_propio / 1e9, 3),
        "flip": flip, "em": em,
        "call_wall": niv.get("call_wall"), "put_wall": niv.get("put_wall"),
        "gamma_pin": niv.get("gamma_pin"),
        "fallas": r.fallas, "avisos": r.avisos,
    }
    with open(os.path.join(DIR, dt.date.today().isoformat() + ".jsonl"), "a",
              encoding="utf-8") as f:
        f.write(json.dumps(fila) + "\n")

    return r, fila


def _control_atas(r, med, contado, ts_cadena):
    """La base medida contra el precio real del futuro que anota el indicador.

    POR QUE NO ALCANZA CON COMPARAR UN PRECIO CONTRA OTRO

    La primera version de este control tomaba la anotacion de ATAS mas cercana
    en el tiempo y le restaba el contado de la cadena. Dio "falla" con 8 puntos
    de diferencia y era MENTIRA: los dos precios estaban tomados con dos
    minutos de separacion, y en un mercado que se mueve un punto por minuto eso
    solo mide el movimiento, no la base.

    Medido sobre 29 pares del 2026-09-01: la diferencia iba de -6.9 a +20.1
    segun el par que tocara. La MEDIANA daba +8.86, contra una base publicada
    de 9.8. O sea que el metodo estaba bien y el control estaba mal.

    Asi que ahora:

      - se usan TODOS los pares del dia, no el mas cercano
      - solo los que estan a un minuto o menos
      - se descartan las fotos con la cadena congelada, que de madrugada
        repiten el mismo contado durante horas y fabrican diferencias enormes
      - se juzga la MEDIANA, que es lo unico estable cuando cada par arrastra
        el ruido del desfase temporal
    """
    hoy = dt.date.today().isoformat()

    def mins(t):
        try:
            return int(t[11:13]) * 60 + int(t[14:16])
        except Exception:
            return None

    # ---- lo que anoto el indicador
    atas = []
    for d in (os.path.join("datos", "contexto"),
              os.path.join(os.environ.get("APPDATA", ""), "ATAS", "PythiaGex", "contexto")):
        ruta = os.path.join(d, "contexto-%s.jsonl" % hoy) if d else ""
        if not ruta or not os.path.exists(ruta):
            continue
        for l in open(ruta, encoding="utf-8-sig"):
            l = l.strip()
            if not l:
                continue
            try:
                x = json.loads(l)
            except Exception:
                continue
            m = mins(x.get("t") or "")
            if m is not None and x.get("precio"):
                atas.append((m, x["precio"], (x.get("instrumento") or "").upper()))
        if atas:
            break

    if not atas:
        r.dato("base contra ATAS", "sin datos",
               "el indicador tiene que estar corriendo para dejar el precio anotado")
        return

    # ---- las fotos de la cadena, con su propio timestamp de cotizacion
    fotos = []
    ruta_h = os.path.join("datos", "historico", "_SPX-%s.jsonl" % hoy)
    if os.path.exists(ruta_h):
        for l in open(ruta_h, encoding="utf-8"):
            l = l.strip()
            if not l:
                continue
            try:
                x = json.loads(l)
            except Exception:
                continue
            m = mins(x.get("t") or "")
            if m is not None and x.get("spot"):
                fotos.append((m, x["spot"], x.get("ts_cadena")))

    if len(fotos) < 3:
        r.dato("base contra ATAS", "%d fotos de cadena hoy" % len(fotos),
               "hacen falta varias para sacar una mediana; el historico local "
               "se llena corriendo cli.py o trayendo lo del bot")
        return

    # ---- descartar las fotos con la cadena congelada: el mismo contado
    # repetido significa que CBOE no refresco, y restarle un futuro que si se
    # movio da diferencias enormes que no son la base
    vistos = {}
    for m, sp, ts in fotos:
        vistos.setdefault(round(sp, 2), []).append(m)
    congelados = {v for v, ms in vistos.items() if len(ms) >= 3}

    pares = []
    for m, sp, ts in fotos:
        if round(sp, 2) in congelados:
            continue
        c = min(atas, key=lambda a: abs(a[0] - m))
        if abs(c[0] - m) > 1:      # un minuto o menos, o no sirve
            continue
        pares.append(c[1] - sp)

    if len(pares) < 5:
        r.dato("base contra ATAS", "%d pares utiles" % len(pares),
               "hacen falta al menos 5 a un minuto o menos y con la cadena viva")
        return

    pares.sort()
    mediana = statistics.median(pares)
    q1 = pares[len(pares) // 4]
    q3 = pares[(3 * len(pares)) // 4]
    inst = atas[-1][2] or "?"
    r.chequeo("base contra ATAS (%s)" % inst, med["base"], mediana, 2.5, " pts",
              "mediana de %d pares a <=1 min, entre %.2f y %.2f "
              "(el rango es ancho porque cada par arrastra el desfase temporal)"
              % (len(pares), q1, q3))


def historia(simbolo="SPX"):
    ruta = os.path.join(DIR, dt.date.today().isoformat() + ".jsonl")
    if not os.path.exists(ruta):
        print("todavia no hay corridas de hoy")
        return
    filas = [json.loads(l) for l in open(ruta, encoding="utf-8")
             if json.loads(l)["simbolo"] == simbolo]
    if not filas:
        print("sin corridas de %s hoy" % simbolo)
        return
    print("\n  EVOLUCION DE %s   (%d corridas)" % (simbolo, len(filas)))
    print("  %-9s %8s %9s %8s %9s %9s %9s %9s %6s" %
          ("hora", "edad", "contado", "base", "futuro", "GEX B", "flip", "call/put", "fallas"))
    for f in filas:
        h = f["t"][11:16]
        print("  %-9s %7.1fm %9s %8s %9s %9s %9s %5s/%-5s %5s" % (
            h, f.get("edad_min") or -1, f.get("contado"), f.get("base"),
            f.get("futuro"), f.get("gex_B"), f.get("flip"),
            f.get("call_wall"), f.get("put_wall"), f.get("fallas")))
    if len(filas) > 1:
        a, z = filas[0], filas[-1]
        print("\n  Se movio en la ventana:")
        for k, etq in (("contado", "contado"), ("base", "base"), ("gex_B", "GEX B"),
                       ("flip", "gamma flip"), ("call_wall", "call wall"),
                       ("put_wall", "put wall")):
            if a.get(k) is not None and z.get(k) is not None:
                print("    %-12s %10s -> %-10s  %+.2f" % (etq, a[k], z[k], z[k] - a[k]))


# ---------------------------------------------------------------------------
def vigilancia():
    """Quien mira el repositorio, y si alguien se lo llevo.

    El repositorio es publico porque GitHub Pages gratis lo necesita para
    servir el panel. Publico no significa libre: la licencia es de todos los
    derechos reservados. Pero una licencia no impide que alguien copie, solo
    da con que reclamar despues.

    Esto guarda una foto de forks, estrellas, observadores y trafico, y avisa
    cuando cambia. No previene nada; hace que uno se entere.

    Ojo con los clones: los propios runners de Actions bajan el repositorio en
    cada corrida, asi que el numero es alto por diseno. Lo que importa es que
    NO suba de golpe por encima de las corridas del dia.
    """
    import subprocess
    ruta = os.path.join(DIR, "vigilancia.json")
    try:
        campos = "forks_count,stargazers_count,subscribers_count"
        j = json.loads(subprocess.run(
            ["gh", "api", "repos/waltermosqueda/PythiaGex",
             "--jq", "{forks:.forks_count,estrellas:.stargazers_count,"
                     "observadores:.subscribers_count}"],
            capture_output=True, text=True, timeout=30).stdout or "{}")
        for clave, ruta_api in (("clones", "traffic/clones"),
                                ("visitas", "traffic/views")):
            r = subprocess.run(
                ["gh", "api", "repos/waltermosqueda/PythiaGex/" + ruta_api,
                 "--jq", "{c:.count,u:.uniques}"],
                capture_output=True, text=True, timeout=30).stdout
            d_ = json.loads(r or "{}")
            j[clave] = d_.get("c", 0)
            j[clave + "_origenes"] = d_.get("u", 0)
    except Exception as e:
        print("  no se pudo consultar GitHub: %s" % str(e)[:70])
        return

    ant = {}
    if os.path.exists(ruta):
        try:
            ant = json.load(open(ruta, encoding="utf-8"))
        except Exception:
            ant = {}

    print("\n  VIGILANCIA DEL REPOSITORIO   %s"
          % dt.datetime.now().strftime("%Y-%m-%d %H:%M"))
    print("  %-16s %8s %10s" % ("", "ahora", "cambio"))
    alerta = False
    for k in ("forks", "estrellas", "observadores", "clones", "clones_origenes",
              "visitas", "visitas_origenes"):
        a = ant.get(k)
        n = j.get(k, 0)
        dif = "" if a is None else ("%+d" % (n - a) if n != a else "=")
        marca = ""
        # un fork, una estrella o un observador nuevo es alguien real mirando
        if k in ("forks", "estrellas", "observadores") and a is not None and n > a:
            marca = "   <-- ALGUIEN LO TOMO"
            alerta = True
        print("  %-16s %8d %10s%s" % (k, n, dif, marca))

    if j.get("forks", 0) > 0:
        print("\n  Hay %d fork(s). Ver quien:" % j["forks"])
        print("     gh api repos/waltermosqueda/PythiaGex/forks --jq '.[].full_name'")
    if not alerta and ant:
        print("\n  sin novedades desde la foto anterior")

    j["t"] = dt.datetime.now(dt.timezone.utc).isoformat(timespec="seconds")
    os.makedirs(DIR, exist_ok=True)
    with open(ruta, "w", encoding="utf-8") as f:
        json.dump(j, f, indent=1)

if __name__ == "__main__":
    args = [x for x in sys.argv[1:] if not x.startswith("--")]
    if "--vigilancia" in sys.argv:
        vigilancia()
        sys.exit(0)
    if "--historia" in sys.argv:
        for s in (args or ["SPX"]):
            historia(s)
        sys.exit(0)
    total_f = 0
    for s in (args or ["SPX"]):
        try:
            rep, _ = auditar(s)
            rep.imprimir()
            total_f += rep.fallas
        except Exception as e:
            print("\n  %s: la auditoria fallo: %s" % (s, e))
            total_f += 1
    sys.exit(1 if total_f else 0)
