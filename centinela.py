# -*- coding: utf-8 -*-
"""El centinela: mide si lo que el indicador predice pasa de verdad.

QUE ES Y QUE NO ES

No adivina la direccion ni busca la tendencia. Hace algo mas aburrido y mucho
mas util: **anota cada prediccion que el indicador hizo, mira que paso
despues, y saca la cuenta**.

El indicador dice, por ejemplo, "el Put Wall en 7660 tiene 31 % de
probabilidad de que lo toquen hoy". Eso es una prediccion falsable. Al cierre
o se toco o no se toco. Repitiendolo sobre cientos de niveles y decenas de
ruedas aparecen dos cosas que ninguna opinion puede dar:

  1. CALIBRACION. De todos los niveles a los que les dimos 70 %, cuantos se
     tocaron de verdad. Si se tocan el 45 %, el numero esta inflado y hay que
     corregirlo. Eso es medible y no es opinable.

  2. QUE FACTOR PESA. De los niveles que el precio toco, cuales aguantaron y
     cuales se rompieron, y que los distinguia: la absorcion, la confluencia,
     el tamano de la gamma, el interes abierto, el regimen, la hora del dia,
     si estaba disputado. Ahi aparece a que esta reaccionando el precio de
     verdad, que puede no ser a lo que creemos.

LA HONESTIDAD ES PARTE DEL DISENO

Con pocas ruedas no se puede concluir NADA, y el programa lo dice en la cara
en vez de esconderlo en una nota al pie. Cada tasa sale con su intervalo de
confianza de Wilson y con la cantidad de casos. Si el intervalo cruza el de
la comparacion, la conclusion es "todavia no se sabe" y punto.

Y hay una trampa que se avisa sola: probando quince factores, uno o dos van a
parecer significativos por puro azar. El programa cuenta cuantos probo y lo
recuerda al final.

    python centinela.py                    # la rueda de hoy
    python centinela.py --fecha 2026-08-29
    python centinela.py --todo             # todas las ruedas juntas
    python centinela.py --informe          # escribe la conclusion en la bitacora
"""
import datetime as dt
import glob
import json
import math
import os
import sys

DIR_HIST = "datos/historico"
DIR_CONO = os.path.join("conocimiento", "centinela")

# --------------------------------------------------------------------------
# Umbrales. Ninguno es una ley: son cortes elegidos y por eso estan aca arriba
# con su razon al lado, no escondidos adentro de una funcion.
# --------------------------------------------------------------------------
TICK = 0.25          # ES y MES
TOCAR_TK = 2         # a dos ticks del nivel se considera tocado
ROMPER_TK = 12       # pasarlo por doce ticks es romperlo, no tantearlo
AGUANTAR_TK = 16     # alejarse dieciseis ticks sin romperlo es que aguanto
MIN_CASOS = 12       # abajo de esto no se saca ninguna conclusion
MIN_SEPARACION = 0.0 # los intervalos no se pueden tocar para llamarlo hallazgo


def wilson(exitos, total, z=1.96):
    """Intervalo de confianza para una proporcion, metodo de Wilson.

    Se usa este y no el normal porque con pocos casos el normal da intervalos
    que se salen de cero y uno, y aca justamente vamos a tener pocos casos
    durante un buen tiempo.
    """
    if total == 0:
        return (0.0, 0.0, 1.0)
    p = exitos / total
    d = 1 + z * z / total
    centro = (p + z * z / (2 * total)) / d
    margen = z * math.sqrt(p * (1 - p) / total + z * z / (4 * total * total)) / d
    return (p, max(0.0, centro - margen), min(1.0, centro + margen))


def leer_fotos(sym, fecha):
    ruta = os.path.join(DIR_HIST, "_%s-%s.jsonl" % (sym, fecha))
    if not os.path.exists(ruta):
        return []
    fotos = []
    for linea in open(ruta, encoding="utf-8"):
        try:
            fotos.append(json.loads(linea))
        except Exception:
            pass
    return fotos


def leer_precio(sym, fecha, base):
    """El camino que hizo el precio ese dia, en precio de futuro.

    Se guarda una copia por dia porque CBOE solo sirve la rueda en curso: si
    no se archiva, al dia siguiente no hay con que contrastar.
    """
    ruta = os.path.join(DIR_HIST, "precio-%s-%s.json" % (sym, fecha))
    es_hoy = (fecha == dt.date.today().isoformat())
    # La rueda en curso se vuelve a bajar siempre. Si se cachea, el centinela
    # analiza con la serie de hace un rato y cree que el precio nunca llego a
    # niveles que si toco despues. Paso: se congelo en 78 velas cuando ya
    # habia mas del doble.
    # Pero si la copia se acaba de escribir (cli.py la archiva en cada ronda),
    # volver a bajarla seria pedirle a CBOE lo mismo dos veces en un minuto.
    fresca = (os.path.exists(ruta)
              and (dt.datetime.now().timestamp() - os.path.getmtime(ruta)) < 300)
    if os.path.exists(ruta) and (not es_hoy or fresca):
        velas = json.load(open(ruta, encoding="utf-8")).get("velas") or []
    else:
        try:
            from pythiagex.precio import intradia
            from pythiagex.fuentes import normalizar
            velas = (intradia(normalizar(sym)) or {}).get("velas") or []
            if velas:
                os.makedirs(DIR_HIST, exist_ok=True)
                json.dump({"velas": velas}, open(ruta, "w", encoding="utf-8"))
        except Exception:
            velas = []
    salida = []
    for v in velas:
        if v.get("h") is None or v.get("l") is None:
            continue
        salida.append({"t": v.get("t"),
                       "alto": v["h"] + base, "bajo": v["l"] + base,
                       "cierre": (v.get("c") or 0) + base})
    return salida


def observar(sym, fecha):
    """Cada nivel de cada foto, con lo que se sabia entonces y lo que paso despues."""
    fotos = leer_fotos(sym, fecha)
    if len(fotos) < 2:
        return []

    # la base de ese dia, para pasar todo a precio de futuro
    base = 0.0
    try:
        from pythiagex.base import CONTRATO  # noqa
    except Exception:
        pass
    ult = fotos[-1]
    if ult.get("niv") and ult.get("spot"):
        base = 0.0   # las fotos guardan el nivel en indice; se compara en indice

    velas = leer_precio(sym, fecha, base)
    if len(velas) < 10:
        return []

    obs = []
    for i, f in enumerate(fotos):
        spot = f.get("spot")
        if not spot:
            continue
        hora = f.get("t", "")[11:16]
        # las velas que faltan desde esta foto hasta el cierre
        restantes = [v for v in velas if v["t"] and v["t"] >= _hora_local(hora)]
        if len(restantes) < 5:
            continue

        gex = f.get("gex") or 0.0
        gex0 = None
        kk = f.get("k") or {}

        for grupo, clave0 in (("niv", False), ("niv0", True)):
            niveles = f.get(grupo) or {}
            probs = f.get("prob0" if clave0 else "prob") or {}
            for tipo, precio_nivel in niveles.items():
                if precio_nivel is None:
                    continue
                if tipo in ("major_positive", "major_negative"):
                    continue          # casi siempre repiten al call/put wall
                dist_tk = (precio_nivel - spot) / TICK
                if abs(dist_tk) < TOCAR_TK:
                    continue          # ya estaba encima: no hay prediccion
                if abs(dist_tk) > 800:
                    continue          # tan lejos que no se juega nada hoy

                datos_k = kk.get(str(precio_nivel)) or kk.get("%.1f" % precio_nivel) or []
                gam = datos_k[0] if len(datos_k) > 0 else None
                oic = datos_k[1] if len(datos_k) > 1 else None
                oip = datos_k[2] if len(datos_k) > 2 else None

                res = _desenlace(precio_nivel, dist_tk > 0, restantes)
                obs.append({
                    "fecha": fecha, "hora": hora, "tipo": tipo, "es0dte": clave0,
                    "nivel": precio_nivel, "spot": spot,
                    "dist_tk": round(dist_tk),
                    "arriba": dist_tk > 0,
                    "gex_neto": gex,
                    "regimen": "corto" if gex < 0 else "largo",
                    "gamma_strike": gam, "oi_call": oic, "oi_put": oip,
                    "flip_dist_tk": (round((f["flip"] - spot) / TICK)
                                     if f.get("flip") else None),
                    "em": f.get("em"),
                    "prob": probs.get(tipo),
                    "minutos_restantes": len(restantes),
                    **res,
                })
    return obs


def _hora_local(hhmm):
    """Las velas de CBOE vienen con hora de Nueva York; las fotos en UTC.
    Se resta la diferencia para poder comparar. En verano son cuatro horas."""
    try:
        h, m = hhmm.split(":")
        return "%02d:%s" % ((int(h) - 4) % 24, m)
    except Exception:
        return "00:00"


def _desenlace(nivel, es_arriba, velas):
    """Que hizo el precio con ese nivel: lo toco, aguanto, o lo rompio."""
    tol = TICK * TOCAR_TK
    romper = TICK * ROMPER_TK
    aguantar = TICK * AGUANTAR_TK

    idx_toque = None
    for i, v in enumerate(velas):
        if es_arriba and v["alto"] >= nivel - tol:
            idx_toque = i
            break
        if not es_arriba and v["bajo"] <= nivel + tol:
            idx_toque = i
            break

    if idx_toque is None:
        return {"tocado": False, "aguanto": None, "rompio": None,
                "excursion_tk": round(max(
                    (v["alto"] - nivel) / TICK if es_arriba else (nivel - v["bajo"]) / TICK
                    for v in velas))}

    # despues del toque: se rompio o se dio vuelta
    post = velas[idx_toque:]
    rompio = any((v["alto"] >= nivel + romper) if es_arriba
                 else (v["bajo"] <= nivel - romper) for v in post)
    reboto = any((v["bajo"] <= nivel - aguantar) if es_arriba
                 else (v["alto"] >= nivel + aguantar) for v in post)
    return {"tocado": True,
            "rompio": bool(rompio),
            # aguanto = se dio vuelta lo suficiente sin haberlo roto antes
            "aguanto": bool(reboto and not rompio),
            "minutos_hasta_toque": idx_toque,
            "excursion_tk": round(max(
                (v["alto"] - nivel) / TICK if es_arriba else (nivel - v["bajo"]) / TICK
                for v in post))}


# --------------------------------------------------------------------------
def tasa(obs, filtro, campo):
    casos = [o for o in obs if filtro(o) and o.get(campo) is not None]
    if not casos:
        return None
    exitos = sum(1 for o in casos if o[campo])
    p, lo, hi = wilson(exitos, len(casos))
    return {"n": len(casos), "exitos": exitos, "p": p, "lo": lo, "hi": hi}


def comparar(obs, nombre, condicion, campo="aguanto"):
    """Compara la tasa con y sin una condicion, y dice si se puede concluir."""
    con = tasa(obs, lambda o: condicion(o) is True, campo)
    sin = tasa(obs, lambda o: condicion(o) is False, campo)
    if not con or not sin:
        return None
    separados = con["lo"] > sin["hi"] or sin["lo"] > con["hi"]
    suficiente = con["n"] >= MIN_CASOS and sin["n"] >= MIN_CASOS
    return {"factor": nombre, "con": con, "sin": sin,
            "dif_pp": (con["p"] - sin["p"]) * 100,
            "concluyente": bool(separados and suficiente),
            "motivo": ("" if (separados and suficiente)
                       else ("pocos casos" if not suficiente
                             else "los intervalos se pisan"))}


# --------------------------------------------------------------------------
# Los factores que se ponen a prueba. Cada uno es una hipotesis falsable
# sobre a que esta reaccionando el precio de verdad.
# --------------------------------------------------------------------------
def factores_a_probar(obs):
    d_med = _mediana([abs(o["dist_tk"]) for o in obs])
    g_med = _mediana([abs(o["gamma_strike"]) for o in obs
                      if o.get("gamma_strike") is not None])
    oi_med = _mediana([(o.get("oi_call") or 0) + (o.get("oi_put") or 0) for o in obs])
    return [
        ("el complejo esta en gamma corta",
         lambda o: o["regimen"] == "corto"),
        ("es una pared del vencimiento de hoy",
         lambda o: o["es0dte"]),
        ("el nivel esta arriba del precio",
         lambda o: o["arriba"]),
        ("el nivel es el iman (gamma pin)",
         lambda o: o["tipo"] == "gamma_pin"),
        ("el nivel es un piso (put wall)",
         lambda o: o["tipo"] == "put_wall"),
        ("la gamma del strike es de las grandes",
         lambda o: (None if o.get("gamma_strike") is None
                    else abs(o["gamma_strike"]) >= g_med)),
        ("el interes abierto del strike es de los altos",
         lambda o: ((o.get("oi_call") or 0) + (o.get("oi_put") or 0)) >= oi_med),
        ("el nivel esta cerca (menos que la distancia tipica)",
         lambda o: abs(o["dist_tk"]) < d_med),
        ("el precio esta del lado que amplifica del flip",
         lambda o: (None if o.get("flip_dist_tk") is None
                    else o["flip_dist_tk"] > 0)),
        ("queda mas de media rueda por delante",
         lambda o: o["minutos_restantes"] > 195),
    ]


def _mediana(xs):
    xs = sorted(x for x in xs if x is not None)
    if not xs:
        return 0
    m = len(xs) // 2
    return xs[m] if len(xs) % 2 else (xs[m - 1] + xs[m]) / 2


def analizar(obs, titulo):
    print("\n" + "=" * 88)
    print("  CENTINELA  %s" % titulo)
    print("=" * 88)
    if not obs:
        print("  sin observaciones")
        return []

    ruedas = sorted({o["fecha"] for o in obs})
    tocados = [o for o in obs if o["tocado"]]
    resueltos = [o for o in tocados if o.get("aguanto") is not None
                 and (o["aguanto"] or o["rompio"])]
    print("  %d predicciones sobre %d rueda(s): %s"
          % (len(obs), len(ruedas), ", ".join(ruedas)))
    print("  se tocaron %d (%.0f%%). De esos, %d llegaron a resolverse"
          % (len(tocados), len(tocados) / len(obs) * 100, len(resueltos)))

    # ---- que tan seguido se toca un nivel, por distancia
    print("\n  CUANTO SE TOCA, SEGUN LA DISTANCIA")
    print("  %-22s %6s %8s %18s" % ("distancia", "casos", "tocado", "intervalo"))
    for etq, lo_, hi_ in (("hasta 20 ticks", 0, 20), ("20 a 50", 20, 50),
                          ("50 a 100", 50, 100), ("100 a 200", 100, 200),
                          ("mas de 200", 200, 10**9)):
        t = tasa(obs, lambda o, a=lo_, b=hi_: a <= abs(o["dist_tk"]) < b, "tocado")
        if not t:
            continue
        inter = "%.0f%% a %.0f%%" % (t["lo"] * 100, t["hi"] * 100)
        print("  %-22s %6d %7.0f%% %16s%s"
              % (etq, t["n"], t["p"] * 100, inter,
                 "" if t["n"] >= MIN_CASOS else "   pocos casos"))

    # ---- que aguanta y que se rompe
    if resueltos:
        agu = sum(1 for o in resueltos if o["aguanto"])
        p, lo_, hi_ = wilson(agu, len(resueltos))
        print("\n  CUANDO EL PRECIO LLEGA AL NIVEL")
        print("  aguanta el %.0f%% de las veces  (intervalo %.0f%% a %.0f%%, %d casos)"
              % (p * 100, lo_ * 100, hi_ * 100, len(resueltos)))

    # ---- a que factor esta reaccionando de verdad
    print("\n  QUE FACTOR SEPARA LO QUE AGUANTA DE LO QUE SE ROMPE")
    print("  %-46s %7s %7s %9s" % ("factor", "con", "sin", "diferencia"))
    hallazgos, probados, sin_grupo = [], 0, []
    for nombre, cond in factores_a_probar(obs):
        r = comparar(resueltos, nombre, cond, "aguanto")
        if not r:
            # uno de los dos grupos quedo vacio: no hay con que comparar.
            # Pasa cuando toda la rueda cae del mismo lado, por ejemplo si
            # estuvo entera en gamma corta. No es un error, es que ese factor
            # no se puede evaluar con estos datos.
            sin_grupo.append(nombre)
            continue
        probados += 1
        marca = "  <-- SEPARA" if r["concluyente"] else "   " + r["motivo"]
        print("  %-46s %6.0f%% %6.0f%% %8.0f pp%s"
              % (nombre[:46], r["con"]["p"] * 100, r["sin"]["p"] * 100,
                 r["dif_pp"], marca))
        if r["concluyente"]:
            hallazgos.append(r)

    if sin_grupo:
        print("\n  sin comparacion posible, todos los casos caen del mismo lado:")
        for nom in sin_grupo:
            print("     %s" % nom)
    print("\n  %d factores probados, %d concluyentes." % (probados, len(hallazgos)))
    if probados >= 5 and hallazgos:
        print("  Ojo: probando %d factores, uno o dos pueden parecer significativos" % probados)
        print("  por puro azar. Un hallazgo vale cuando se repite en ruedas nuevas.")
    if len(resueltos) < MIN_CASOS * 2:
        print("\n  ADVERTENCIA: %d casos resueltos es MUY poco. Nada de esto es"
              % len(resueltos))
        print("  concluyente todavia. La maquina funciona; falta que corra ruedas.")
    return hallazgos


def calibracion(obs):
    """Lo que prometimos contra lo que paso. Es el control mas duro que hay."""
    con_prob = [o for o in obs if o.get("prob") is not None]
    if not con_prob:
        # Callarse aca seria lo peor: parece que el control paso cuando en
        # realidad no corrio. La probabilidad se empezo a archivar el
        # 2026-08-31; las fotos viejas no la tienen, y la foto mas nueva
        # todavia no tiene rueda por delante contra la cual medirse.
        print("\n  CALIBRACION: sin datos todavia")
        print("  ninguna foto de este tramo trae la probabilidad que se prometio.")
        print("  se empieza a poder medir con las fotos de manana en adelante.")
        return
    print("\n  CALIBRACION: LO QUE PROMETIMOS CONTRA LO QUE PASO")
    print("  %-16s %6s %10s %10s" % ("prometido", "casos", "real", "diferencia"))
    for lo_, hi_ in ((0, 20), (20, 40), (40, 60), (60, 80), (80, 101)):
        gr = [o for o in con_prob if lo_ <= o["prob"] < hi_]
        if not gr:
            continue
        real = sum(1 for o in gr if o["tocado"]) / len(gr) * 100
        prom = sum(o["prob"] for o in gr) / len(gr)
        print("  %-16s %6d %9.0f%% %9.0f pp%s"
              % ("%d a %d%%" % (lo_, hi_), len(gr), real, real - prom,
                 "" if len(gr) >= MIN_CASOS else "   pocos casos"))


def guardar(obs, hallazgos, titulo):
    os.makedirs(DIR_CONO, exist_ok=True)
    ruta = os.path.join(DIR_CONO, "observaciones.jsonl")
    ya = set()
    if os.path.exists(ruta):
        for l in open(ruta, encoding="utf-8"):
            try:
                o = json.loads(l)
                ya.add((o["fecha"], o["hora"], o["tipo"], o["nivel"], o["es0dte"]))
            except Exception:
                pass
    nuevas = 0
    with open(ruta, "a", encoding="utf-8") as f:
        for o in obs:
            k = (o["fecha"], o["hora"], o["tipo"], o["nivel"], o["es0dte"])
            if k in ya:
                continue
            f.write(json.dumps(o) + "\n")
            nuevas += 1
    print("\n  guardadas %d observaciones nuevas en %s" % (nuevas, ruta))


def informe(obs, hallazgos, titulo, fechas):
    """Deja la conclusion escrita en la bitacora, con fecha y cantidad de casos.

    Lo que se escribe no es un resumen bonito: es lo que se puede defender.
    Si nada fue concluyente, eso mismo es la entrada. Una bitacora que solo
    anota los aciertos no sirve para aprender nada.
    """
    os.makedirs(DIR_CONO, exist_ok=True)
    ruta = os.path.join(DIR_CONO, "bitacora.md")
    resueltos = [o for o in obs if o["tocado"]]
    L = []
    L.append("\n## %s  (%d rueda/s: %s)\n"
             % (dt.date.today().isoformat(), len(fechas), ", ".join(fechas)))
    L.append("- %d predicciones, %d llegaron al nivel (%d%%).\n"
             % (len(obs), len(resueltos),
                round(len(resueltos) / len(obs) * 100) if obs else 0))
    if resueltos:
        t = wilson(sum(1 for o in resueltos if o["aguanto"]), len(resueltos))
        L.append("- cuando el precio llega, el nivel aguanta el %d%% "
                 "(intervalo %d%% a %d%%, %d casos).\n"
                 % (t[0] * 100, t[1] * 100, t[2] * 100, len(resueltos)))
    if hallazgos:
        L.append("- factores que separan con intervalos que no se pisan:\n")
        for h in hallazgos:
            L.append("  - %s: %d%% contra %d%% (%+d pp)\n"
                     % (h["factor"], h["con"]["p"] * 100,
                        h["sin"]["p"] * 100, h["dif_pp"]))
    else:
        L.append("- **ningun factor resulto concluyente.** Con esta cantidad "
                 "de ruedas no alcanza; no es un resultado negativo, es falta "
                 "de muestra.\n")
    with open(ruta, "a", encoding="utf-8") as f:
        f.write("".join(["\n"] + L))
    print("  informe agregado a %s" % ruta)


def main():
    args = sys.argv[1:]
    hoy = dt.date.today().isoformat()
    sym = "SPX"

    if "--todo" in args:
        fechas = sorted({os.path.basename(f).split("-", 1)[1].replace(".jsonl", "")
                         for f in glob.glob(os.path.join(DIR_HIST, "_%s-*.jsonl" % sym))})
    elif "--fecha" in args:
        fechas = [args[args.index("--fecha") + 1]]
    else:
        fechas = [hoy]

    todas = []
    for fe in fechas:
        todas.extend(observar(sym, fe))

    h = analizar(todas, "%s   %s" % (sym, " + ".join(fechas)))
    calibracion(todas)
    if todas:
        guardar(todas, h, sym)
        if "--informe" in args:
            informe(todas, h, sym, fechas)


if __name__ == "__main__":
    main()
