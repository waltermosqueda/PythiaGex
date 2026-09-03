# -*- coding: utf-8 -*-
"""Radar: dominantes + BigTrades, en un archivo que dibujan el panel y ATAS.

    python radar.py SPX                 una corrida
    python radar.py SPX --vigilar 60    corre cada 60 s hasta que lo cortes
    python radar.py SPX --rehacer       reconstruye el dia desde el cache

Dos mitades que se necesitan:

  DOMINANTES  donde la mesa tiene un incentivo real HOY. Sale del interes
              abierto, que es de ayer y no cambia hasta la noche, mas el
              precio y la volatilidad de ahora.

  BIGTRADES   quien esta entrando AHORA y de que lado. Sale de restar el
              volumen acumulado entre dos corridas.

La primera dice donde mirar. La segunda dice si esa pared se esta
reforzando o la estan comiendo. Ninguna de las dos sola alcanza.

EL RETRASO ESTA MEDIDO Y SE PUBLICA EN CADA ARCHIVO. CBOE sirve el dato 902
segundos tarde. Todo lo que sale de aca lleva la hora de mercado real, no la
del archivo.
"""
import argparse, glob, gzip, json, os, sys, time
import datetime as dt

from pythiagex.fuentes import bajar, normalizar, leer_cache
from pythiagex.base import (medir as medir_base, contrato_vigente,
                            nombre_futuro, edad_minutos)
from pythiagex import dominantes as DOM
from pythiagex import bigtrades as BT
from pythiagex import cadena_atas as CAD

CACHE = "datos/cache"
SALIDA = "panel/datos"


def _sello(ruta):
    """El sello UTC que lleva el nombre del archivo de cache."""
    b = os.path.basename(ruta)
    try:
        p = b.rsplit(".json", 1)[0].split("-")
        return dt.datetime.strptime(p[-2] + p[-1], "%Y%m%d%H%M%S")
    except Exception:
        return None


def corridas(sim, desde=None):
    """Las corridas guardadas de un simbolo, ordenadas por hora."""
    out = []
    for r in glob.glob(os.path.join(CACHE, "%s-*.json.gz" % sim)):
        s = _sello(r)
        if s and (desde is None or s >= desde):
            out.append((s, r))
    out.sort()
    return out


def anterior_util(sim, ahora_sello, minimo_s=30, maximo_s=900):
    """La corrida previa que sirve para restar volumenes.

    Muy pegada no da tiempo a que se acumule nada; muy lejos mete media
    sesion en una sola ventana y el 'ultimo precio' deja de representar
    nada. Se busca la mas reciente dentro de la banda util.
    """
    cs = corridas(sim)
    mejor = None
    for s, r in cs:
        d = (ahora_sello - s).total_seconds()
        if minimo_s <= d <= maximo_s:
            if mejor is None or d < (ahora_sello - mejor[0]).total_seconds():
                mejor = (s, r)
    return mejor


BASE_MEM = os.path.join(SALIDA, "base-ultima.json")


def recordar_base(sim, base, error_ticks):
    """Guarda la ultima base CONFIABLE, con su hora.

    POR QUE HACE FALTA PERSISTIRLA

    La base solo se puede medir cuando las opciones del indice cotizan. Fuera
    de ese horario medir_base() devuelve None -- no es que mida mal: no hay
    con que medir. Verificado el 2026-09-03 a las 08:23 UTC, con SPX cerrado.

    Sin memoria, el indicador arranca en frio sin base y no dibuja NADA. Y
    apagarse no es lo prudente: la base es carry, se mueve unos pocos puntos
    por dia, asi que la ultima medicion buena sigue siendo una estimacion
    razonable durante horas -- siempre que se diga de cuando es, que es
    justo lo que se guarda aca junto al numero.
    """
    if base is None:
        return
    try:
        os.makedirs(SALIDA, exist_ok=True)
        d = {}
        if os.path.exists(BASE_MEM):
            with open(BASE_MEM, encoding="utf-8") as f:
                d = json.load(f)
        d[sim] = {"base": round(base, 3),
                  "error_ticks": error_ticks,
                  "cuando": dt.datetime.now(dt.timezone.utc).isoformat(timespec="seconds")}
        with open(BASE_MEM, "w", encoding="utf-8") as f:
            json.dump(d, f, ensure_ascii=False, indent=1)
    except Exception:
        pass


def base_recordada(sim):
    """(base, edad en minutos) de la ultima medicion confiable, o (None, None)."""
    try:
        with open(BASE_MEM, encoding="utf-8") as f:
            d = json.load(f).get(sim)
        if not d:
            return None, None
        t = dt.datetime.fromisoformat(d["cuando"])
        edad = (dt.datetime.now(dt.timezone.utc) - t).total_seconds() / 60.0
        return d["base"], round(edad, 1)
    except Exception:
        return None, None


def historial_path(sim, fecha):
    return os.path.join(SALIDA, "bigtrades-%s-%s.json" % (sim.lstrip("_"), fecha))


def cargar_historial(sim, fecha):
    p = historial_path(sim, fecha)
    if not os.path.exists(p):
        return {"fecha": fecha, "simbolo": sim, "eventos": []}
    try:
        with open(p, encoding="utf-8") as f:
            return json.load(f)
    except Exception:
        return {"fecha": fecha, "simbolo": sim, "eventos": []}


def guardar_historial(h, sim, fecha):
    os.makedirs(SALIDA, exist_ok=True)
    with open(historial_path(sim, fecha), "w", encoding="utf-8") as f:
        json.dump(h, f, ensure_ascii=False, separators=(",", ":"))


def una_corrida(simbolo, crudo=None, prima_minima=None, guardar=True,
                verboso=True, revivida=None):
    sim = normalizar(simbolo)
    if crudo is None:
        crudo = bajar(sim, cache_dir=CACHE, guardar=True)

    ts = crudo.get("timestamp")
    sello = dt.datetime.strptime(ts, "%Y-%m-%d %H:%M:%S")
    hora_mkt = BT._hora_mercado(ts)

    # LA BASE SE MIDE, NO SE ESCRIBE A MANO. Si no es confiable el archivo
    # igual sale, pero marcado: el consumidor dibuja los niveles del indice
    # y avisa. Un nivel de SPX dibujado crudo en ES esta ~20 puntos corrido.
    try:
        b = medir_base(crudo)
    except Exception as e:
        b = {"base": None, "confiable": False, "aviso": "no se pudo medir: %s" % e}
    base = b.get("base") if b.get("confiable") else None
    base_cruda = b.get("base")
    if base is not None:
        recordar_base(sim, base, b.get("residuo_ticks"))
    base_mem, base_mem_edad = base_recordada(sim)

    dom = DOM.calcular(crudo, base=base, ahora=sello.replace(tzinfo=dt.timezone.utc))

    # BigTrades contra la corrida anterior util
    prev = anterior_util(sim, sello)
    bt = None
    if prev:
        try:
            bt = BT.detectar(leer_cache(prev[1]), crudo, base=base,
                             prima_minima=prima_minima or BT.PRIMA_MINIMA)
        except Exception as e:
            bt = {"error": str(e)}

    fecha = (hora_mkt or sello).date().isoformat()
    hist = cargar_historial(sim, fecha)
    nuevos = 0
    if bt and bt.get("eventos"):
        vistos = {(e["contrato"], e["hora"]) for e in hist["eventos"]}
        for e in bt["eventos"]:
            if (e["contrato"], e["hora"]) not in vistos:
                hist["eventos"].append(e)
                nuevos += 1
        hist["eventos"].sort(key=lambda z: z["hora"])
        if guardar:
            guardar_historial(hist, sim, fecha)

    # nombre_futuro() devuelve (raiz, micro) y contrato_vigente() devuelve
    # (vencimiento, codigo). Las dos son tuplas: concatenarlas como si fueran
    # texto arma una tupla de cuatro y revienta recien al serializar.
    raiz, micro = nombre_futuro(sim.lstrip("_"))
    venc_fut, codigo = contrato_vigente()
    salida = {
        "simbolo": sim.lstrip("_"),
        "futuro": raiz,
        "micro": micro,
        "contrato": "%s%s" % (raiz, codigo or ""),
        "contrato_micro": "%s%s" % (micro, codigo or ""),
        "vencimiento_futuro": venc_fut.isoformat() if venc_fut else None,
        "generado": dt.datetime.now(dt.timezone.utc).isoformat(timespec="seconds"),
        "cadena_ts": ts,
        "cadena_edad_min": edad_minutos(ts),
        # Si esto es una sesion reconstruida, la antiguedad de la cadena es
        # de dias y el aviso de "dato viejo" no aplica: no esta viejo, es
        # de otro dia a proposito. Sin esta marca el panel grita por algo
        # que no es un problema, y un aviso que grita de mas se ignora
        # justo el dia que importa.
        "revivida": revivida,
        "hora_mercado": hora_mkt.isoformat(timespec="seconds") if hora_mkt else None,
        "retraso_s": BT.RETRASO_CBOE_S,
        "aviso_retraso": ("CBOE llega %d s tarde, medido. Los BigTrades describen "
                          "lo que paso hace %d minutos y no sirven de gatillo en vivo."
                          % (BT.RETRASO_CBOE_S, round(BT.RETRASO_CBOE_S / 60))),
        "spot": dom.get("spot"),
        "base": base,
        "base_cruda": base_cruda,
        "base_confiable": bool(b.get("confiable")),
        "base_aviso": b.get("aviso"),
        "base_error_ticks": b.get("residuo_ticks"),
        "base_ultima_buena": base_mem,
        "base_ultima_buena_edad_min": base_mem_edad,
        "dominantes": dom,
        "bigtrades_ventana": bt,
        # EL CRUCE CON LAS ZONAS VA CONTRA TODA LA SESION, NO CONTRA LA
        # ULTIMA VENTANA.
        #
        # La primera version lo calculaba solo con la ventana de un minuto.
        # En una ventana de un minuto casi nunca cae plata grande justo sobre
        # una zona dominante, asi que la seccion mas util del panel salia
        # vacia casi siempre -- y en una sesion reconstruida, vacia SIEMPRE,
        # aunque hubiera 229 operaciones guardadas.
        #
        # La pregunta "esta pared, la refuerzan o se la comen" es del dia,
        # no del ultimo minuto. La ventana se sigue publicando aparte para
        # quien quiera lo de recien.
        "bigtrades_por_strike": BT.por_strike({"eventos": hist["eventos"]}, top=14),
        "bigtrades_contra_zonas": BT.contra_dominantes(
            {"eventos": hist["eventos"]}, dom),
        "bigtrades_ventana_por_strike": BT.por_strike(bt) if bt else [],
        "bigtrades_ventana_contra_zonas": BT.contra_dominantes(bt, dom) if bt else [],
        "bigtrades_dia": hist["eventos"],
        "bigtrades_dia_n": len(hist["eventos"]),
    }

    if guardar:
        os.makedirs(SALIDA, exist_ok=True)
        with open(os.path.join(SALIDA, "radar-%s.json" % sim.lstrip("_")),
                  "w", encoding="utf-8") as f:
            json.dump(salida, f, ensure_ascii=False, separators=(",", ":"))
        _feed_atas(salida, crudo, sello)

    if verboso:
        _imprimir(salida, nuevos, prev)
    return salida


def _feed_atas(s, crudo=None, sello=None):
    """El extracto liviano que baja el indicador de ATAS.

    El archivo completo pesa cientos de KB por los BigTrades del dia. ATAS lo
    parsea en el hilo de dibujo, asi que aca va solo lo que se dibuja.
    """
    fut = s.get("futuro") or "ES"
    d = s["dominantes"]

    def z(x):
        if not x:
            return None
        return {"fut": x.get("fut"), "idx": x["strike"],
                "desde": x.get("fut_desde"), "hasta": x.get("fut_hasta"),
                "idx_desde": x["desde"], "idx_hasta": x["hasta"],
                "caracter": x["caracter"], "lado": x["lado"],
                "incentivo": x["incentivo_100"], "relevante": x["relevante"],
                "gex_M": x["gex_M"], "tam": x["tamano"], "inm": x["inmediatez"],
                "alc": x["alcance"], "criollo": DOM.criollo(x)}

    out = {
        "generado": s["generado"], "cadena_ts": s["cadena_ts"],
        "revivida": s.get("revivida"),
        "edad_min": s["cadena_edad_min"], "hora_mercado": s["hora_mercado"],
        "retraso_s": s["retraso_s"],
        "spot": s["spot"], "base": s["base"],
        "base_confiable": s["base_confiable"],
        # La base CRUDA viaja igual aunque no sea confiable. Sin esto el
        # indicador se queda sin nada y apaga todo el dibujo, que es peor:
        # la base es carry y se mueve lento, asi que una medicion floja
        # avisada sirve mucho mas que una pantalla en blanco.
        "base_cruda": s.get("base_cruda"),
        "base_error_ticks": s.get("base_error_ticks"),
        # La ultima base CONFIABLE medida, con su edad. Es el respaldo
        # que evita que el indicador se apague fuera del horario de
        # opciones, cuando la base no se puede medir en absoluto.
        "base_ultima_buena": s.get("base_ultima_buena"),
        "base_ultima_buena_edad_min": s.get("base_ultima_buena_edad_min"),
        "contrato": s["contrato"],
        "dominantes": [z(x) for x in (d.get("dominante_arriba"),
                                      d.get("dominante_abajo"),
                                      d.get("acelerador_arriba"),
                                      d.get("acelerador_abajo")) if x],
        "zonas": [z(x) for x in d.get("zonas", []) if x.get("relevante")][:12],
        "faltan": d.get("faltan", []),
        # LOS INSUMOS PARA REPRECIAR EN VIVO. Ver cadena_atas.py: el
        # indicador rehace la gamma con el precio de cada tick, que es
        # lo unico que se mueve. El interes abierto es de ayer para
        # todos, asi que el retraso de CBOE no lo toca.
        "cadena": (CAD.construir(
            crudo, ahora=sello.replace(tzinfo=dt.timezone.utc))
            if crudo is not None and sello is not None else None),

        # el perfil por strike, para la barra lateral
        "perfil": [{"idx": p["strike"],
                    "fut": round(p["strike"] + s["base"], 2) if s["base"] else None,
                    "gex_M": round(p["gex"] / 1e6),
                    "incentivo": round(p["incentivo"] * 100, 1)}
                   for p in d.get("perfil", []) if abs(p["gex"]) > 5e6],
        # Los BigTrades del dia, recortados a lo dibujable.
        #
        # Los codigos de una letra van escritos a mano, no sacados con [0]:
        # "amplifica" y "amortigua" empiezan las dos con "a", asi que la
        # primera version mandaba la misma letra para los dos efectos
        # opuestos y el indicador los habria pintado del mismo color.
        # EL PRECIO DE FUTURO SE DERIVA ACA, NO SE LEE DEL EVENTO GUARDADO.
        #
        # El historial se escribe corrida a corrida, y las corridas que
        # reconstruyen un dia entero desde el cache no miden la base: guardan
        # el evento con fut en null. El indicador descarta los eventos sin
        # precio de futuro, asi que se quedaba sin dibujar NINGUN BigTrade y
        # el archivo se veia perfecto igual. Lo unico invariante es el strike;
        # el precio del futuro sale de la base de ahora, que es con la que se
        # esta mirando el grafico.
        "bigtrades": [{"h": e["hora"], "idx": e["strike"],
                       "fut": (round(e["strike"] + s["base"], 2)
                               if s["base"] is not None else None),
                       "t": "C" if e["tipo"] == "call" else "P",
                       "c": e["contratos"], "p": e["prima"],
                       "l": {"compra agresiva": "C", "venta agresiva": "V"}
                             .get(e["lado"], "N"),
                       "e": {"amplifica": "A", "amortigua": "M"}
                             .get(e["efecto_gamma"], "I"),
                       "z": 1 if e["es_0dte"] else 0}
                      for e in s["bigtrades_dia"][-400:]],
    }
    os.makedirs(os.path.join(SALIDA, "atas"), exist_ok=True)
    with open(os.path.join(SALIDA, "atas", "%s_radar.json" % fut), "w",
              encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, separators=(",", ":"))


def _imprimir(s, nuevos, prev):
    d = s["dominantes"]
    print("=" * 74)
    print("%s  spot %.2f   cadena %s (%s min)   mercado %s"
          % (s["simbolo"], s["spot"] or 0, s["cadena_ts"], s["cadena_edad_min"],
             (s["hora_mercado"] or "?")[11:19]))
    if s["base_confiable"]:
        print("base %+.2f -> %s   (error %.1f ticks)"
              % (s["base"], s["contrato"], s.get("base_error_ticks") or 0))
    else:
        print("base NO confiable (%.2f cruda): los niveles salen en indice"
              % (s.get("base_cruda") or 0))
        if s.get("base_aviso"):
            print("   %s" % s["base_aviso"])
    print()
    print("DOMINANTES")
    for k, et in (("dominante_arriba", "techo"), ("dominante_abajo", "piso"),
                  ("acelerador_arriba", "trampolin"),
                  ("acelerador_abajo", "resbaladilla")):
        z = d.get(k)
        if not z:
            continue
        # EL PRECIO Y LA BANDA TIENEN QUE ESTAR EN LA MISMA UNIDAD.
        # La primera version imprimia el nucleo convertido a futuro y la banda
        # en strikes del indice, en el mismo renglon: "7705.70 [7695-7695]".
        # Son diez puntos de diferencia, que es justo el error que este
        # proyecto existe para no cometer.
        if z.get("fut"):
            pr = "%8.2f" % z["fut"]
            d1, d2, un = z["fut_desde"], z["fut_hasta"], ""
        else:
            pr = "%8.0f" % z["strike"]
            d1, d2, un = z["desde"], z["hasta"], " idx"
        print("  %-12s %s%s   [%.0f-%.0f]  incentivo %5.1f%s"
              % (et, pr, un, d1, d2, z["incentivo_100"],
                 "" if z["relevante"] else "   DEBIL: hoy es decorativo"))
        print("               tamano %.2f x inmediatez %.2f x alcance %.2f  |  %s"
              % (z["tamano"], z["inmediatez"], z["alcance"], DOM.criollo(z)))
    for f in d.get("faltan", []):
        print("  aviso: %s" % f)

    bt = s.get("bigtrades_ventana")
    print()
    if not bt:
        print("BIGTRADES: no hay corrida anterior en la banda util (30-900 s)."
              if not prev else "BIGTRADES: sin datos")
    elif bt.get("error"):
        print("BIGTRADES: %s" % bt["error"])
    else:
        print("BIGTRADES  ventana de %d s, %d nuevos, %d en el dia   [%s]"
              % (bt["ventana_s"], nuevos, s["bigtrades_dia_n"],
                 s["aviso_retraso"]))
        for e in bt["eventos"][:8]:
            print("  %s  %6.0f %-4s %5s  %6d contr  USD %11s  %-16s %s"
                  % (e["hora"][11:19], e["strike"], e["tipo"],
                     "0DTE" if e["es_0dte"] else "%.0fd" % e["dte"],
                     e["contratos"], format(e["prima"], ","), e["lado"],
                     e["efecto_gamma"]))
        cruce = s.get("bigtrades_contra_zonas") or []
        if cruce:
            print()
            print("  QUE LE ESTAN HACIENDO A CADA ZONA")
            for c in cruce[:5]:
                print("    %6.0f %-11s USD %11s  ->  %s"
                      % (c["zona"], c["caracter"], format(c["prima"], ","),
                         c["lectura"]))


def rehacer(simbolo, prima_minima=None):
    """Reconstruye el dia entero desde las corridas ya guardadas.

    Sirve para dos cosas: llenar el historial de una sesion que se corrio a
    mano, y auditar el detector contra un dia completo sin volver a bajar
    nada de la red.
    """
    sim = normalizar(simbolo)
    cs = corridas(sim)
    if len(cs) < 2:
        print("hacen falta al menos dos corridas guardadas de %s" % sim)
        return
    print("reconstruyendo %d corridas de %s" % (len(cs), sim))
    total, hist_por_fecha = 0, {}
    for (s0, r0), (s1, r1) in zip(cs, cs[1:]):
        d = (s1 - s0).total_seconds()
        if not (30 <= d <= 900):
            continue
        try:
            a, b = leer_cache(r0), leer_cache(r1)
            res = BT.detectar(a, b, prima_minima=prima_minima or BT.PRIMA_MINIMA)
        except Exception:
            continue
        if res.get("error") or not res.get("eventos"):
            continue
        fecha = res["hasta"][:10]
        h = hist_por_fecha.setdefault(fecha, cargar_historial(sim, fecha))
        vistos = {(e["contrato"], e["hora"]) for e in h["eventos"]}
        for e in res["eventos"]:
            if (e["contrato"], e["hora"]) not in vistos:
                h["eventos"].append(e)
                total += 1
    for fecha, h in hist_por_fecha.items():
        h["eventos"].sort(key=lambda z: z["hora"])
        guardar_historial(h, sim, fecha)
        print("  %s -> %d eventos" % (fecha, len(h["eventos"])))
    print("total agregados: %d" % total)


def revivir(simbolo, fecha, prima_minima=None):
    """Rearma el radar como quedo al final de una sesion pasada.

    El dato de CBOE llega quince minutos tarde, asi que no sirve de gatillo
    en vivo -- pero para estudiar una sesion terminada el retraso no importa
    nada. Esto es justamente para lo que ese dato SI sirve: mirar el dia de
    ayer con las dominantes y el flujo grande superpuestos.
    """
    sim = normalizar(simbolo)
    cs = [(s, r) for s, r in corridas(sim) if s.date().isoformat() == fecha]
    if not cs:
        print("no hay corridas guardadas de %s el %s" % (sim, fecha))
        disp = sorted({s.date().isoformat() for s, _ in corridas(sim)})
        if disp:
            print("dias disponibles: %s" % ", ".join(disp))
        return
    print("reviviendo %s del %s con la ultima de %d corridas" % (sim, fecha, len(cs)))
    return una_corrida(sim, crudo=leer_cache(cs[-1][1]), prima_minima=prima_minima,
                       revivida=fecha)


def main():
    ap = argparse.ArgumentParser(description="Dominantes y BigTrades")
    ap.add_argument("simbolo", nargs="?", default="SPX")
    ap.add_argument("--vigilar", type=int, metavar="SEG",
                    help="corre cada N segundos hasta que lo cortes")
    ap.add_argument("--rehacer", action="store_true",
                    help="reconstruye el historial desde el cache")
    ap.add_argument("--dia", metavar="AAAA-MM-DD",
                    help="rearma el radar como quedo al final de esa sesion")
    ap.add_argument("--prima", type=int, default=None,
                    help="prima minima en USD para que una operacion sea grande")
    a = ap.parse_args()

    if a.rehacer:
        rehacer(a.simbolo, a.prima)
        return
    if a.dia:
        revivir(a.simbolo, a.dia, a.prima)
        return

    if not a.vigilar:
        una_corrida(a.simbolo, prima_minima=a.prima)
        return

    # EL PISO DE 30 SEGUNDOS NO ES CAPRICHO. CBOE regenera el archivo cada
    # pocos segundos pero el contenido avanza de a un minuto; pedirlo mas
    # seguido baja bytes que no cambiaron y no agrega ninguna operacion.
    per = max(30, a.vigilar)
    print("vigilando %s cada %d s. Ctrl+C para cortar." % (a.simbolo, per))
    while True:
        try:
            una_corrida(a.simbolo, prima_minima=a.prima)
        except KeyboardInterrupt:
            raise
        except Exception as e:
            print("  fallo la corrida: %s" % e)
        try:
            time.sleep(per)
        except KeyboardInterrupt:
            print("\ncortado.")
            return


if __name__ == "__main__":
    main()
