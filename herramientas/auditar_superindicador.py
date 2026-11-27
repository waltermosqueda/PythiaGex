"""AUDITORIA POR FUERA del Superindicador (23-11-2026).

Rehace, sin usar una linea del C#, todo lo que el indicador dice haber dibujado, desde el dato crudo que el mismo deja en
%APPDATA%\\ATAS\\PythiaGex\\superindicador\\:
  velas-<inst>-<tf>-<dia>.csv     footprint de cada vela cerrada (tick, bid, ask, vol) + lo que ATAS trae en la vela
                                  (delta, volumen, area de valor y POC de la vela)
  auditoria-<inst>-<tf>-<dia>.jsonl  cada 60 s: ventanas, filas sumadas, POC/VAH/VAL, textos y pixeles dibujados, y la vela en
                                  curso EXACTA que entro en cada suma
  cinta-<inst>-<dia>.csv          cada operacion en vivo (hora, precio, volumen, agresor C/V/E): FUENTE INDEPENDIENTE del footprint

Controles:
  A) cada vela: suma de niveles == delta y volumen que ATAS trae en la vela
  B) columna y perfil: filas (bid/ask/vol), totales, POC/VAH/VAL y TEXTO de cada fila dibujada, recalculados desde el footprint
  C) area de valor y POC de cada vela con el algoritmo de ATAS contra los que ATAS trae en la vela (valida la implementacion)
  D) la cinta en vivo contra el footprint, vela por vela y nivel por nivel (valida el dato crudo mismo)

Uso: python -I herramientas/auditar_superindicador.py [--linea N] [--inst MES] [--tf M1] [--dia AAAAMMDD]
"""
import argparse
import csv
import glob
import json
import os
import sys
from collections import defaultdict
from decimal import Decimal as D
from datetime import datetime, timedelta, timezone

CARP = os.path.join(os.environ.get("APPDATA", ""), "ATAS", "PythiaGex", "superindicador")


def numero(v, con_signo):
    """La regla de pantalla, escrita de nuevo: exacto hasta 99.999, desde 100.000 en miles con 'k'."""
    a = abs(v)
    if a >= 100000:
        q = (a / 1000).quantize(D(1), rounding="ROUND_HALF_UP")
        s = f"{q}k"
    elif a == a.to_integral_value():
        s = str(int(a))
    else:
        s = format(a.normalize(), "f")
    if v < 0:
        return "-" + s
    if con_signo and v > 0:
        return "+" + s
    return s


def leer_velas(ruta, carga=None):
    """velas2 (1.5) trae la columna 'carga': en el replay las mismas horas llegan de nuevo con otra carga; se toma la pedida."""
    velas = {}
    with open(ruta, encoding="utf-8") as f:
        for r in csv.DictReader(f):
            if carga is not None and "carga" in r and r["carga"] not in ("", None) and int(r["carga"]) != carga:
                continue
            h = r["hora_utc"]
            v = velas.get(h)
            if v is None or v["bar"] != int(r["bar"]):
                v = {"bar": int(r["bar"]), "hora": h, "fin": r["hora_fin_utc"], "o": D(r["o"]), "h": D(r["h"]), "l": D(r["l"]), "c": D(r["c"]),
                     "delta_atas": D(r["delta_atas"]), "vol_atas": D(r["vol_atas"]), "vah_atas": D(r["vah_atas"]), "val_atas": D(r["val_atas"]),
                     "poc_atas": D(r["poc_atas"]), "niv": {}}
                velas[h] = v
            if r["tick"] != "":
                t = int(r["tick"])
                v["niv"][t] = (D(r["bid"]), D(r["ask"]), D(r["vol"]))
    return velas


def sumar(niveles_list, k):
    acc = defaultdict(lambda: [D(0), D(0), D(0)])
    for niv in niveles_list:
        for t, (b, a, v) in niv.items():
            c = acc[t // k]
            c[0] += b; c[1] += a; c[2] += v
    return acc


def poc_regla(vol_por_fila):
    """Fila de mas volumen; empate: la de precio mas alto (regla de ATAS)."""
    con = [f for f, v in vol_por_fila.items() if v > 0]
    if not con:
        return None
    vmax = max(vol_por_fila[f] for f in con)
    return max(f for f in con if vol_por_fila[f] == vmax)


def area_valor(vol_por_fila, poc, pct, paso):
    """Bucle de ATAS sobre TODAS las filas entre la mas baja y la mas alta con volumen (huecos = 0)."""
    con = sorted(f for f, v in vol_por_fila.items() if v > 0)
    precios = list(range(con[0], con[-1] + 1))
    vol_por_fila = {p: vol_por_fila.get(p, D(0)) for p in precios}
    idx = {p: i for i, p in enumerate(precios)}
    alto, bajo = precios[-1], precios[0]
    objetivo = sum(vol_por_fila[p] for p in precios) * D(pct) / D(100)
    vah = val = poc
    acum = vol_por_fila[poc]
    while acum <= objetivo and not (vah >= alto and val <= bajo):
        i = idx[vah]; up = D(0); up_p = vah
        for _ in range(paso):
            if i + 1 >= len(precios):
                break
            i += 1; up_p = precios[i]; up += vol_por_fila[up_p]
        j = idx[val]; dn = D(0); dn_p = val
        for _ in range(paso):
            if j - 1 < 0:
                break
            j -= 1; dn_p = precios[j]; dn += vol_por_fila[dn_p]
        if up == dn == 0:
            vah, val = min(up_p, alto), max(dn_p, bajo)
        elif up >= dn:
            vah = up_p; acum += up
        else:
            val = dn_p; acum += dn
        if acum >= objetivo:
            break
    return vah, val, acum


def auditar_ventana(nombre, w, velas, tick, informe):
    if not w.get("dibujado"):
        informe.append(f"{nombre}: no dibujada (on={w.get('on')})")
        return True
    k = w["k"]
    viva = w.get("viva")
    t0, t1 = w["t_desde"], w["t_hasta"]
    cerradas = [v for h, v in velas.items() if t0 <= h <= t1 and (viva is None or h != viva["hora"])]
    niveles = [v["niv"] for v in cerradas]
    if viva is not None:
        niveles.append({t: (D(str(b)), D(str(a)), D(str(vv))) for t, b, a, vv in viva["niveles"]})
    n_velas = len(cerradas) + (1 if viva else 0)
    acc = sumar(niveles, k)
    ok = True
    if n_velas != w["velas"]:
        informe.append(f"{nombre}: velas {n_velas} recalculadas vs {w['velas']} del indicador (falta footprint de alguna vela?)")
        ok = False
    fmin = w["fila_min"]
    ind = {fmin + i: (D(str(b)), D(str(a)), D(str(v))) for i, b, a, v in w["filas_datos"]}
    mio = {f: tuple(c) for f, c in acc.items() if any(c)}
    dif = [f for f in set(ind) | set(mio) if ind.get(f) != mio.get(f)]
    sb = sum(c[0] for c in mio.values()); sa = sum(c[1] for c in mio.values()); sv = sum(c[2] for c in mio.values())
    tot_ok = (sb, sa, sv) == (D(str(w["sum_bid"])), D(str(w["sum_ask"])), D(str(w["sum_vol"])))
    atas_ok = (sa - sb) == D(str(w["sum_delta_atas"])) and sv == D(str(w["sum_vol_atas"]))
    informe.append(f"{nombre} [{w['nombre']}] {n_velas} velas {t0}..{t1} UTC, K={k}: filas {len(mio)} | distintas {len(dif)} | "
                   f"totales {'OK' if tot_ok else 'DISTINTOS'} (compras {sa} ventas {sb} delta {sa - sb:+} vol {sv}) | "
                   f"suma de deltas/volumenes de ATAS por vela {'IGUAL' if atas_ok else 'DISTINTA'}")
    if dif:
        ok = False
        for f in sorted(dif)[:5]:
            informe.append(f"   fila {f} ({f * k * tick}): indicador {ind.get(f)} vs recalculado {mio.get(f)}")
    # textos dibujados
    malos_txt = 0
    ocultos = 0
    for i, y0, y1, txt in w["dibujadas"]:
        f = fmin + i
        if txt == "(oculto)":   # numero que no entraba entero (debajo del encabezado fijo o contra el borde): no se dibujo
            ocultos += 1
            continue
        b, a, v = mio.get(f, (D(0), D(0), D(0)))
        if nombre == "PERFIL":
            esperado = numero(v, False)
        elif w["dato"] == "Delta":
            esperado = numero(a - b, True) if (a or b or v) else ""
        elif w["dato"] == "Volumen":
            esperado = numero(v, False) if (a or b or v) else ""
        else:
            esperado = (numero(b, False) + "x" + numero(a, False)) if (a or b or v) else ""
        if txt != esperado:
            malos_txt += 1
            if malos_txt <= 3:
                informe.append(f"   texto fila {f}: dibujado '{txt}' vs esperado '{esperado}'")
    informe.append(f"   textos de {len(w['dibujadas']) - ocultos} filas dibujadas: {'todos iguales' if malos_txt == 0 else str(malos_txt) + ' distintos'}"
                   f" ({ocultos} sin numero por no entrar entero; numeros en pantalla: {w['numeros']}, letra {w['letra']})")
    ok = ok and malos_txt == 0 and tot_ok
    # 1.8: el encabezado dice LIVE (verde) solo si la suma incluye la vela en curso, y la fecha y hora (hora de la PC, con segundos)
    # de la ULTIMA operacion que entro en la suma: la ultima de la vela en curso o la de la ultima vela de la ventana
    if nombre == "COLUMNA" and "live" in w:
        live_ok = w["live"] == (viva is not None)
        fin = (viva or {}).get("hora_fin") if viva is not None else velas.get(t1, {}).get("fin")
        esperado_t = ""
        if fin:
            loc = datetime.fromisoformat(fin[:23]).replace(tzinfo=timezone.utc).astimezone()
            esperado_t = loc.strftime("%d-%m %H:%M:%S")
        # 1.8b: el formato en pantalla es "jue 26/11  21:56:54" (o sin el dia si no entra): se comparan los digitos
        solo_digitos = lambda s: "".join(ch for ch in s if ch.isdigit())
        t_ok = bool(esperado_t) and solo_digitos(w.get("cuando", "")).endswith(solo_digitos(esperado_t))
        informe.append(f"   ENCABEZADO: LIVE {'verde' if w['live'] else 'rojo apagado'} ({'IGUAL' if live_ok else 'DISTINTO'} a "
                       f"{'incluye' if viva is not None else 'no incluye'} la vela en curso) | dice '{w.get('cuando', '')}', ultima operacion "
                       f"sumada {esperado_t} hora local -> {'IGUAL' if t_ok else 'DISTINTA'}")
        ok = ok and live_ok and t_ok
    if nombre == "PERFIL":
        volf = {f: c[2] for f, c in mio.items()}
        poc = poc_regla(volf)
        vah, val, acum = area_valor(volf, poc, w["pct"], w["paso"])
        pi = (w["poc"] + fmin, w["vah"] + fmin, w["val"] + fmin)
        igual = (poc, vah, val) == pi and acum == D(str(w["vol_av"]))
        def pr(f, alto=False):
            return (f * k + (k - 1 if alto else 0)) * tick
        informe.append(f"   POC {pr(poc)} VAH {pr(vah, True)} VAL {pr(val)} (AV {w['pct']} %, paso {w['paso']}, {acum / sv * 100:.2f} % del volumen) -> "
                       f"{'IGUAL al indicador' if igual else 'DISTINTO del indicador: ' + str([pr(x) for x in pi])}")
        empates = sum(1 for f in volf if volf[f] == volf[poc])
        if empates > 1:
            informe.append(f"   ojo: {empates} filas empatadas en el maximo de volumen")
        ok = ok and igual
    return ok


def jornada_de(hora_utc):
    """1.5, escrito de nuevo: jornada de CME por la hora NY de apertura (desde las 18:00 cuenta para el dia siguiente; sabado y
    domingo al lunes) y tipo de vela (0 noche, 1 RTH 9:30-16:00, 2 despues del cierre)."""
    from zoneinfo import ZoneInfo
    from datetime import time as _t
    h = datetime.strptime(hora_utc[:19], "%Y-%m-%dT%H:%M:%S").replace(tzinfo=timezone.utc).astimezone(ZoneInfo("America/New_York"))
    d = h.date() + timedelta(days=1) if h.time() >= _t(18, 0) else h.date()
    if d.weekday() == 5:
        d += timedelta(days=2)
    elif d.weekday() == 6:
        d += timedelta(days=1)
    tipo = 1 if _t(9, 30) <= h.time() < _t(16, 0) else (2 if _t(16, 0) <= h.time() < _t(18, 0) else 0)
    return d.year * 10000 + d.month * 100 + d.day, tipo, h


def auditar_jornada(au, velas, informe):
    """1.5: la ventana 'Jornada elegida' tiene que ser exactamente las velas de esa jornada y esa parte (ni una mas ni una menos)."""
    j = au.get("jornada")
    ventanas = [(n, w) for n, w in (("COLUMNA", au["col"]), ("PERFIL", au["per"])) if w.get("ventana") == "Jornada"]
    if not j or not ventanas:
        informe.append("JORNADA: ninguna ventana usa 'Jornada elegida'")
        return None
    parte = {"Completa": 0, "Rth": 1, "Noche": 2}[j["parte"]]
    entra = (lambda tipo: True) if parte == 0 else (lambda tipo: tipo == (1 if parte == 1 else 0))
    horas = sorted(velas)
    marca = {h: jornada_de(h) for h in horas}
    # 1.7 "al momento": la ventana termina en la vela del momento (la ultima a la vista); se recalcula por fuera que jornada le
    # toca: la de esa vela si la parte ya habia empezado, si no la anterior mas reciente con esa parte (completa)
    m = j.get("momento") if j.get("modo") == "Momento" else None
    if m is not None and m >= 0:
        por_bar = {v["bar"]: h for h, v in velas.items()}
        h_m = por_bar.get(m)
        if h_m is None:
            for _, w in ventanas:
                if w.get("viva") and w["viva"]["bar"] == m:
                    h_m = w["viva"]["hora"]
        if h_m is not None:
            if h_m not in marca:
                marca[h_m] = jornada_de(h_m)
            barras = {b: marca[h] for b, h in por_bar.items() if b <= m}
            barras[m] = marca[h_m]
            c_m = marca[h_m][0]
            esperada = None
            for c in sorted({cc for cc, _, _ in barras.values()}, reverse=True):
                if c > c_m:
                    continue
                bs = sorted(b for b, (cc, t, _) in barras.items() if cc == c and entra(t))
                if bs:
                    esperada = (c, bs[0], min(bs[-1], m)); break
            okm = esperada == (j["clave"], j["desde"], j["hasta"])
            informe.append(f"   AL MOMENTO (vela {m}, {marca[h_m][2]:%d-%m %H:%M} NY): esperada {esperada} -> "
                           f"{'IGUAL' if okm else 'DISTINTA de ' + str((j['clave'], j['desde'], j['hasta']))}")
            if not okm:
                return False
    sel = [h for h in horas if marca[h][0] == j["clave"] and entra(marca[h][1]) and (m is None or velas[h]["bar"] <= m)]
    informe.append(f"JORNADA elegida: {j['clave']} {j['parte']} (modo {j['modo']}, fecha {j['fecha']}) -> velas {j['desde']}..{j['hasta']}"
                   f"{' EN CURSO' if j['en_curso'] else ''}{' CORTADA (el grafico no la cargo desde el comienzo)' if j['cortada'] else ''}"
                   f"{' aviso: ' + j['aviso'] if j.get('aviso') else ''} | jornadas cargadas: {len(j['lista'])}")
    ok = True
    # 1.6: en "a la vista" la jornada tiene que ser la que se ve: la que mas velas de esa parte tiene entre la primera y la ultima
    # vela visibles (empate: la mas nueva); con la ultima vela del grafico a la vista, la ultima jornada que tiene esa parte
    if j["modo"] == "AlaVista" and au.get("visibles"):
        v0, v1 = au["visibles"]; ult = au["ultima"]
        por_bar = {v["bar"]: h for h, v in velas.items()}
        if v1 >= ult:
            claves = [marca[h][0] for h in horas if entra(marca[h][1])]
            esperada = claves[-1] if claves else None
            como = "borde derecho: la ultima con esa parte"
        else:
            cuenta = defaultdict(int); total = defaultdict(int)
            for b in range(v0, v1 + 1):
                h = por_bar.get(b)
                if h is None:
                    continue
                c, t, _ = marca[h]
                total[c] += 1
                if entra(t):
                    cuenta[c] += 1
            base = cuenta if cuenta else total
            esperada = max(base, key=lambda c: (base[c], c)) if base else None
            como = "la que mas velas tiene a la vista: " + ", ".join(f"{c} {base[c]}" for c in sorted(base))
        okv = esperada == j["clave"]
        informe.append(f"   A LA VISTA (velas {v0}..{v1}, ultima {ult}): esperada {esperada} ({como}) -> {'IGUAL' if okv else 'DISTINTA'}")
        ok = ok and okv
    for nombre, w in ventanas:
        if not w.get("dibujado"):
            informe.append(f"   {nombre}: no dibujada"); continue
        t0, t1 = w["t_desde"], w["t_hasta"]
        viva = w.get("viva")
        cerradas_sel = [h for h in sel if viva is None or h != viva["hora"]]
        # el comienzo: la primera vela de la seleccion es la del indicador, y la vela anterior volcada NO es de esa jornada y parte
        ok_ini = bool(cerradas_sel) and cerradas_sel[0] == t0
        prev = [h for h in horas if h < t0]
        frontera = "sin vela anterior volcada"
        if prev:
            cp, tp, hp = marca[prev[-1]]
            ok_ini = ok_ini and not (cp == j["clave"] and entra(tp))
            frontera = f"la anterior ({hp:%d-%m %H:%M} NY) es de {cp} tipo {tp}"
        # el final: la ultima cerrada de la seleccion, o la vela en curso si la ventana la incluye
        if viva is not None:
            cv, tv, hv = jornada_de(viva["hora"])
            ok_fin = viva["hora"] == t1 and cv == j["clave"] and entra(tv)
        else:
            ok_fin = bool(cerradas_sel) and cerradas_sel[-1] == t1
        # nada de otra jornada o parte entre medio
        intrusas = [h for h in horas if t0 <= h <= t1 and not (marca[h][0] == j["clave"] and entra(marca[h][1]))]
        _, _, h0 = jornada_de(t0); _, _, h1 = jornada_de(t1)
        informe.append(f"   {nombre}: {h0:%d-%m %H:%M} a {h1:%d-%m %H:%M} NY | comienzo {'OK' if ok_ini else 'MAL'} ({frontera}) | "
                       f"final {'OK' if ok_fin else 'MAL'} | velas de otra jornada o parte adentro: {len(intrusas)}")
        ok = ok and ok_ini and ok_fin and not intrusas
    return ok


def auditar_area_valor_velas(velas, pct, paso, tick, informe):
    """Valida la implementacion contra ATAS: area de valor y POC de CADA vela, 1 tick por fila."""
    n = iguales_va = iguales_poc = empates_poc = 0
    ejemplos = []
    for h, v in sorted(velas.items()):
        if not v["niv"] or v["vah_atas"] == 0:
            continue
        n += 1
        volf = {t: c[2] for t, c in v["niv"].items()}
        vmax = max(volf.values())
        maximos = [t for t, x in volf.items() if x == vmax]
        poc_atas_t = int((v["poc_atas"] / tick).to_integral_value())
        poc_usado = poc_regla(volf)          # la regla propia (empate -> el mas alto), NO la de ATAS: se compara
        if len(maximos) > 1:
            empates_poc += 1
        if poc_usado == poc_atas_t:
            iguales_poc += 1
        vah, val, _ = area_valor(volf, poc_usado, pct, paso)
        if (D(vah) * tick, D(val) * tick) == (v["vah_atas"], v["val_atas"]):
            iguales_va += 1
        elif len(ejemplos) < 4:
            ejemplos.append(f"{h}: calc VAH {D(vah) * tick} VAL {D(val) * tick} vs ATAS {v['vah_atas']} {v['val_atas']}")
    informe.append(f"AREA DE VALOR POR VELA (algoritmo de ATAS, {pct} %, paso {paso}) contra la de ATAS en {n} velas: "
                   f"{iguales_va} iguales ({(iguales_va / n * 100) if n else 0:.2f} %); POC igual en {iguales_poc} de {n}"
                   f" (de ellas {empates_poc} con empate en el maximo)")
    for e in ejemplos:
        informe.append("   " + e)
    return n > 0 and iguales_va == n and iguales_poc == n


def auditar_cinta(velas, ruta_cinta, tick, informe, minutos=1):
    if not os.path.exists(ruta_cinta):
        informe.append("CINTA: no hay archivo todavia")
        return None
    ops = []
    with open(ruta_cinta, encoding="utf-8") as f:
        for r in csv.DictReader(f):
            try:
                ops.append((r["hora_utc"], D(r["precio"]), D(r["volumen"]), r["agresor"]))
            except Exception:
                pass
    if not ops:
        informe.append("CINTA: vacia")
        return None
    ops.sort(key=lambda x: x[0])
    t_ini, t_fin = ops[0][0], ops[-1][0]
    horas = sorted(velas)
    cubiertas = iguales = 0
    malas = []
    # 1.5: la cinta se graba solo mientras ATAS corre con el indicador: si ATAS estuvo cerrado hay HUECOS (medido el 26-11: 330
    # minutos "sin cinta" entre las 7:30 y las 13:00 UTC, todos en un mismo corte). Solo se comparan velas con cinta en el
    # minuto anterior, en el propio y en el siguiente (grabacion continua); los minutos del borde de un corte quedan afuera.
    con_cinta = {t[:16] for t, _, _, _ in ops}
    salteadas = 0
    for i, h in enumerate(horas):
        h_sig = (datetime.fromisoformat(h) + timedelta(minutes=minutos)).strftime("%Y-%m-%dT%H:%M:%S")
        # solo velas enteramente dentro de la cinta (con un minuto de margen a cada lado)
        if not (h > t_ini and h_sig < t_fin):
            continue
        h_ant = (datetime.fromisoformat(h) - timedelta(minutes=1)).strftime("%Y-%m-%dT%H:%M")
        if not (h[:16] in con_cinta and h_ant in con_cinta and h_sig[:16] in con_cinta):
            salteadas += 1
            continue
        hi = h + ".000"; hs = h_sig + ".000"
        niv = defaultdict(lambda: [D(0), D(0), D(0)])
        for t, p, v, ag in ops:
            if hi <= t < hs:
                c = niv[int((p / tick).to_integral_value())]
                if ag == "C":
                    c[1] += v
                elif ag == "V":
                    c[0] += v
                c[2] += v
        cubiertas += 1
        fp = {t: tuple(x) for t, x in velas[h]["niv"].items()}
        ci = {t: tuple(x) for t, x in niv.items()}
        if fp == ci:
            iguales += 1
        elif len(malas) < 4:
            d = [t for t in set(fp) | set(ci) if fp.get(t) != ci.get(t)]
            malas.append(f"{h}: {len(d)} niveles distintos, ej. {d[0] * tick}: footprint {fp.get(d[0])} vs cinta {ci.get(d[0])}")
    informe.append(f"CINTA en vivo ({len(ops)} operaciones, {t_ini}..{t_fin} UTC) contra el footprint de ATAS: {cubiertas} velas "
                   f"enteramente cubiertas, {iguales} identicas nivel por nivel ({salteadas} salteadas por hueco de grabacion o borde)")
    for m in malas:
        informe.append("   " + m)
    return cubiertas > 0 and iguales == cubiertas


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--inst", default="MES"); ap.add_argument("--tf", default="M1")
    ap.add_argument("--dia", default=datetime.now(timezone.utc).strftime("%Y%m%d")); ap.add_argument("--linea", type=int, default=-1)
    a = ap.parse_args()
    ra = os.path.join(CARP, f"auditoria-{a.inst}-{a.tf}-{a.dia}.jsonl")
    rv = os.path.join(CARP, f"velas2-{a.inst}-{a.tf}-{a.dia}.csv")
    if not os.path.exists(rv):
        rv = os.path.join(CARP, f"velas-{a.inst}-{a.tf}-{a.dia}.csv")   # anterior a la 1.5
    rc = os.path.join(CARP, f"cinta-{a.inst}-{a.dia}.csv")
    lineas = open(ra, encoding="utf-8").read().strip().splitlines()
    au = json.loads(lineas[a.linea])
    tick = D(str(au["tick"]))
    velas = leer_velas(rv, au.get("carga"))
    informe = [f"AUDITORIA {au['utc']} UTC (linea {a.linea if a.linea >= 0 else len(lineas) + a.linea} de {len(lineas)}) v{au['v']} {au['inst']} {au['tf']} "
               f"tick {tick} | carga {au.get('carga')} | velas en el volcado de esa carga: {len(velas)}"]
    malas_a = [(h, v) for h, v in velas.items() if v["niv"] and (sum(c[1] - c[0] for c in v["niv"].values()) != v["delta_atas"]
                                                                   or sum(c[2] for c in v["niv"].values()) != v["vol_atas"])]
    sin_agresor = sum(sum(c[2] - c[0] - c[1] for c in v["niv"].values()) for v in velas.values())
    informe.append(f"A) VELAS: {len(velas) - len(malas_a)} de {len(velas)} con delta y volumen de niveles == los de ATAS; volumen sin agresor {sin_agresor}")
    ok_j = auditar_jornada(au, velas, informe)
    ok_b1 = auditar_ventana("COLUMNA", au["col"], velas, tick, informe)
    ok_b2 = auditar_ventana("PERFIL", au["per"], velas, tick, informe)
    pct, paso = au["va_plataforma"]
    ok_c = auditar_area_valor_velas(velas, pct, paso, tick, informe)
    minutos = int(au["tf"][1:]) if au["tf"].startswith("M") and au["tf"][1:].isdigit() else 1
    ok_d = auditar_cinta(velas, rc, tick, informe, minutos)
    # colores de las ultimas velas
    pal = au["velas"]
    informe.append(f"VELAS PINTADAS ({pal['paleta']}, doji {pal['doji']}): " + ", ".join(f"{h} {o}->{c} {col}" for _, h, o, c, col in pal["ultimas"][-5:]))
    print("\n".join(informe))
    print("RESULTADO JORNADA (velas de esa jornada y parte, ni una mas ni una menos):",
          "no se usa" if ok_j is None else ("IGUAL" if ok_j else "DIFERENCIAS"))
    print("RESULTADO LO DIBUJADO (A+B):", "TODO IGUAL" if (not malas_a and ok_b1 and ok_b2) else "HAY DIFERENCIAS (ver arriba)")
    print("RESULTADO CONTRA ATAS (C):", "IGUAL" if ok_c else "DIFERENCIAS")
    print("RESULTADO CINTA (D):", "sin velas cubiertas todavia" if ok_d is None else ("IGUAL" if ok_d else "DIFERENCIAS"))


if __name__ == "__main__":
    main()
