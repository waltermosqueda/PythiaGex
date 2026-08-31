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
# Donde el indicador de ATAS anota lo que ve. Se busca primero la copia del
# repositorio y despues la carpeta viva de ATAS, para que funcione igual si se
# corre en esta maquina o si alguien lo corre despues sobre el archivo.
DIR_CTX = "datos/contexto"
DIR_CTX_ATAS = os.path.join(os.environ.get("APPDATA", ""), "ATAS", "PythiaGex", "contexto")
TOL_CTX_MIN = 8   # cuanto puede separarse la foto de ATAS de la de la cadena

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


def sincronizar_contexto():
    """Trae al repositorio lo que ATAS fue anotando en su carpeta.

    ATAS escribe en %APPDATA%. Si no se copia, ese conocimiento vive en una
    sola maquina y se pierde con ella, que es exactamente lo que no queremos.
    """
    if not DIR_CTX_ATAS or not os.path.isdir(DIR_CTX_ATAS):
        return 0
    os.makedirs(DIR_CTX, exist_ok=True)
    copiadas = 0
    for nom in os.listdir(DIR_CTX_ATAS):
        if not nom.startswith("contexto-") or not nom.endswith(".jsonl"):
            continue
        org = os.path.join(DIR_CTX_ATAS, nom)
        dst = os.path.join(DIR_CTX, nom)
        # se juntan las lineas de los dos lados sin repetir, por timestamp
        vistas = {}
        for ruta in (dst, org):
            if not os.path.exists(ruta):
                continue
            for l in open(ruta, encoding="utf-8"):
                l = l.strip()
                if not l:
                    continue
                try:
                    vistas[json.loads(l)["t"]] = l
                except Exception:
                    pass
        if not vistas:
            continue
        antes = 0
        if os.path.exists(dst):
            antes = sum(1 for _ in open(dst, encoding="utf-8"))
        with open(dst, "w", encoding="utf-8") as f:
            for k in sorted(vistas):
                f.write(vistas[k] + "\n")
        copiadas += max(0, len(vistas) - antes)
    return copiadas


def leer_contexto(fecha):
    """Lo que ATAS anoto ese dia, ordenado por hora.

    Devuelve lista vacia si ese dia el indicador no estuvo corriendo. Eso no
    es un error: significa que esa rueda no tiene contexto de order flow y las
    hipotesis que lo necesitan van a quedar afuera de la cuenta, no adivinadas.
    """
    salida = []
    for base in (DIR_CTX, DIR_CTX_ATAS):
        ruta = os.path.join(base, "contexto-%s.jsonl" % fecha) if base else ""
        if not ruta or not os.path.exists(ruta):
            continue
        for l in open(ruta, encoding="utf-8"):
            l = l.strip()
            if not l:
                continue
            try:
                salida.append(json.loads(l))
            except Exception:
                pass
        if salida:
            break
    salida.sort(key=lambda x: x.get("t", ""))
    return salida


def _mas_cerca(ctx, hora_utc):
    """La foto de ATAS mas cercana en el tiempo a la foto de la cadena."""
    if not ctx or not hora_utc:
        return None
    try:
        hh, mm = int(hora_utc[:2]), int(hora_utc[3:5])
    except Exception:
        return None
    obj = hh * 60 + mm
    mejor, dist = None, 10 ** 9
    for c in ctx:
        t = c.get("t") or ""
        if len(t) < 16:
            continue
        d = abs(int(t[11:13]) * 60 + int(t[14:16]) - obj)
        if d < dist:
            mejor, dist = c, d
    return mejor if dist <= TOL_CTX_MIN else None


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
    ctx_dia = leer_contexto(fecha)

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
        kk = f.get("k") or {}
        # Como venia cambiando la gamma respecto de la foto anterior. Es la
        # unica forma de distinguir un complejo que se esta armando de uno que
        # se esta deshaciendo, y eso cambia como se comporta cada pared.
        gex_prev = fotos[i - 1].get("gex") if i > 0 else None
        gex_delta = (gex - gex_prev) if gex_prev is not None else None
        hora_et = _hora_local(hora)

        # Lo que ATAS estaba viendo en ese mismo momento. Si el indicador no
        # estaba corriendo, esto queda en None y las hipotesis que dependen de
        # el se saltean en vez de inventarse.
        cx = _mas_cerca(ctx_dia, hora)
        cx_niv = {}
        if cx:
            for nv in (cx.get("niveles") or []):
                cx_niv[(nv.get("nombre"), bool(nv.get("es0dte")))] = nv

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
                    # distancia medida en unidades de expected move: un nivel
                    # a 30 puntos es lejisimos en una rueda tranquila y esta
                    # ahi nomas en una rueda de datos. En puntos crudos las
                    # dos ruedas se mezclan y se pierde la senal.
                    "dist_em": (round(abs(precio_nivel - spot) / f["em"], 2)
                                if f.get("em") else None),
                    "hora_et": hora_et,
                    "chex": f.get("chex"),
                    "dex": f.get("dex"),
                    "gex_delta": gex_delta,
                    **_desde_atas(cx, cx_niv.get((tipo, clave0))),
                    "minutos_restantes": len(restantes),
                    **res,
                })
    return obs


def _desde_atas(cx, nv):
    """Los campos que aporta ATAS, o None parejo si no estaba corriendo.

    Se devuelven siempre las mismas claves, presentes o no. Una observacion a
    la que le falta una clave y otra que la tiene en None son cosas distintas
    para cualquier analisis, y mezclarlas ensucia todo.
    """
    vacio = {"vol_nivel": None, "delta_nivel": None, "pct_sesion": None,
             "absorcion": None, "nodo": None, "dist_vwap_tk": None,
             "confluencia": None, "apilados": None, "lado_apilados": None,
             "print_grande": None, "divergencia": None, "lado_divergencia": None,
             "poc_previo_virgen": None, "dist_poc_tk": None,
             "dentro_area_valor": None}
    if not cx or not nv:
        return vacio
    ses = cx.get("sesion") or {}
    tick = cx.get("tick") or 0.25
    fut = nv.get("fut")
    d_poc = None
    if fut and ses.get("poc") and tick:
        d_poc = round((fut - ses["poc"]) / tick)
    dentro = None
    if fut and ses.get("vah") and ses.get("val"):
        dentro = bool(ses["val"] <= fut <= ses["vah"])
    return {
        "vol_nivel": nv.get("vol"),
        "delta_nivel": nv.get("delta"),
        "pct_sesion": nv.get("pct_sesion"),
        "absorcion": nv.get("absorcion"),
        "nodo": nv.get("nodo"),
        "dist_vwap_tk": nv.get("dist_vwap_tk"),
        "confluencia": nv.get("confluencia"),
        "apilados": nv.get("apilados"),
        "lado_apilados": nv.get("lado_apilados"),
        "print_grande": nv.get("print_grande"),
        "divergencia": nv.get("divergencia"),
        "lado_divergencia": nv.get("lado_divergencia"),
        "poc_previo_virgen": cx.get("poc_previo_virgen"),
        "dist_poc_tk": d_poc,
        "dentro_area_valor": dentro,
    }


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
    ch_med = _mediana([o["chex"] for o in obs if o.get("chex") is not None])
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
        # --- de aca para abajo, hipotesis que necesitan el contexto nuevo.
        # Las fotos viejas no lo traen y devuelven None, asi que quedan
        # afuera de la cuenta en vez de contaminarla.
        ("el nivel esta adentro del expected move",
         lambda o: (None if o.get("dist_em") is None else o["dist_em"] <= 1.0)),
        ("es la primera hora de la rueda",
         lambda o: (None if not o.get("hora_et")
                    else o["hora_et"] < "10:30")),
        ("es la ultima hora de la rueda",
         lambda o: (None if not o.get("hora_et")
                    else o["hora_et"] >= "15:00")),
        ("el charm empuja fuerte (CHEX negativo grande)",
         lambda o: (None if o.get("chex") is None
                    else o["chex"] <= (ch_med if ch_med is not None else 0))),
        ("la gamma se esta agrandando respecto de la foto anterior",
         lambda o: (None if o.get("gex_delta") is None
                    else o["gex_delta"] > 0)),
        # --- de aca para abajo, lo que ve ATAS. Solo existe en las ruedas en
        # las que el indicador estuvo corriendo; en las demas devuelve None y
        # la hipotesis queda afuera de la cuenta, no adivinada.
        ("el muro cae sobre un nodo de alto volumen",
         lambda o: (None if o.get("nodo") is None else o["nodo"] == "alto")),
        ("el muro cae sobre un nodo de bajo volumen (hueco)",
         lambda o: (None if o.get("nodo") is None else o["nodo"] == "bajo")),
        ("hay absorcion en el nivel",
         lambda o: o.get("absorcion")),
        ("el nivel esta adentro del area de valor",
         lambda o: o.get("dentro_area_valor")),
        ("el nivel tiene tres o mas confluencias",
         lambda o: (None if o.get("confluencia") is None
                    else o["confluencia"] >= 3)),
        ("hay imbalances apilados en el nivel",
         lambda o: (None if o.get("apilados") is None else o["apilados"] > 0)),
        ("hubo un print grande en el nivel",
         lambda o: o.get("print_grande")),
        ("hay divergencia de delta en el nivel",
         lambda o: o.get("divergencia")),
        ("el POC de ayer sigue sin tocarse",
         lambda o: o.get("poc_previo_virgen")),
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
    print("  %-52s %7s %7s %9s" % ("factor", "con", "sin", "diferencia"))
    hallazgos, probados, sin_grupo, sin_dato = [], 0, [], []
    for nombre, cond in factores_a_probar(obs):
        r = comparar(resueltos, nombre, cond, "aguanto")
        if not r:
            # Dos motivos muy distintos para no poder comparar, y confundirlos
            # seria mentir por omision: o no hay NINGUN dato todavia (el
            # indicador de ATAS no estuvo corriendo esa rueda), o hay dato
            # pero todos los casos caen del mismo lado.
            if all(cond(o) is None for o in resueltos):
                sin_dato.append(nombre)
                continue
            # uno de los dos grupos quedo vacio: no hay con que comparar.
            # Pasa cuando toda la rueda cae del mismo lado, por ejemplo si
            # estuvo entera en gamma corta. No es un error, es que ese factor
            # no se puede evaluar con estos datos.
            sin_grupo.append(nombre)
            continue
        probados += 1
        marca = "  <-- SEPARA" if r["concluyente"] else "   " + r["motivo"]
        print("  %-52s %6.0f%% %6.0f%% %8.0f pp%s"
              % (nombre[:52], r["con"]["p"] * 100, r["sin"]["p"] * 100,
                 r["dif_pp"], marca))
        if r["concluyente"]:
            hallazgos.append(r)

    if sin_dato:
        print("\n  sin dato todavia (el indicador de ATAS no anoto estas ruedas):")
        for nom in sin_dato:
            print("     %s" % nom)
    if sin_grupo:
        print("\n  sin comparacion posible, todos los casos caen del mismo lado:")
        for nom in sin_grupo:
            print("     %s" % nom)
    print("\n  %d factores probados, %d concluyentes." % (probados, len(hallazgos)))
    if hallazgos:
        # La trampa de las comparaciones multiples. Probando N factores sobre
        # datos puramente al azar, uno espera ver alrededor de N/20 que
        # parezcan significativos. Con 9 factores eso es medio hallazgo falso
        # por corrida. Decirlo al lado del resultado es la unica forma de que
        # no se lea como un descubrimiento.
        print("  OJO: probando %d factores, ~%.1f pueden parecer buenos por"
              " puro azar." % (probados, probados / 20.0))
        print("  Un hallazgo recien vale cuando se repite en ruedas que no"
              " son las que lo encontraron.")
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
    """Archiva las observaciones REPISANDO las que ya estaban.

    Esto no es un detalle. El centinela corre cada quince minutos, asi que la
    misma foto se evalua muchas veces a lo largo del dia: a las 13:00 una foto
    de las 12:00 tiene una hora de rueda por delante, y al cierre tiene cuatro.
    Si se apendara sin repisar, quedaria grabado el veredicto de la evaluacion
    mas POBRE — la primera — y la base entera quedaria sesgada hacia "no se
    toco", porque a la mayoria de los niveles todavia no les habia dado tiempo.
    La ultima evaluacion es siempre la que mas rueda vio, y es la que manda.
    """
    os.makedirs(DIR_CONO, exist_ok=True)
    ruta = os.path.join(DIR_CONO, "observaciones.jsonl")
    def clave(o):
        return "%s|%s|%s|%s|%s" % (o["fecha"], o["hora"], o["tipo"],
                                   o["nivel"], o["es0dte"])
    previas = {}
    if os.path.exists(ruta):
        for l in open(ruta, encoding="utf-8"):
            try:
                o = json.loads(l)
                previas[clave(o)] = o
            except Exception:
                pass
    antes = len(previas)
    repisadas = 0
    for o in obs:
        k = clave(o)
        if k in previas:
            repisadas += 1
        previas[k] = o
    with open(ruta, "w", encoding="utf-8") as f:
        for k in sorted(previas):
            f.write(json.dumps(previas[k]) + "\n")
    print("\n" + "  archivo: %d observaciones (%d nuevas, %d actualizadas "
          "con mas rueda por delante)"
          % (len(previas), len(previas) - antes, repisadas))


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

    n = sincronizar_contexto()
    if n:
        print("  contexto de ATAS: %d anotaciones nuevas traidas al repositorio" % n)

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
