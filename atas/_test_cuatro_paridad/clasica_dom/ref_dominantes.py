# -*- coding: utf-8 -*-
"""ref_dominantes.py — la referencia Python del arnes clasica_dom (PythiaGex 4.1.5b, 09-10-2026). SOLO LECTURA (lee las copias que deja el arnes).

Uso:  python -I -B ref_dominantes.py <carpeta de copias> <salida.json>
  <copias>/niv/niv-*.jsonl               copias de familia/niv-<sesion>-MNQZ6.jsonl (se usan los minutos con R10_NDX_dom)
  <copias>/cboe4/cadena-NQ-*.jsonl.gz    copias de las cadenas _NDX que bajo la 4.1 (PythiaGex4/cboe)
  <copias>/audit_ndx.txt                 las lineas "AUDIT capa=NDX" del dia en el log de la clasica (hora ART), extraidas por el arnes

La cuenta es la del port VERIFICADO de la clasica (laboratorio/calibracion_1009/receta_clasica_dominantes/dominantes/extraer_dominantes_clasica.py:
calcular(c, futuro, ahora, base_forzada=...), 927 de 927 AUDIT capa=NDX iguales con la cadena de la clasica). Se importa, no se copia.
  A. NIV: cada minuto con R10_NDX_dom. t = inicio del minuto (k minutos desde 1970, como SesionFamilia.DeClave); base = la de la meta 'Clasica NDX'
     del minuto; fut = round(S + base, 2) (el fut de la clasica es un tick del MNQ); la cadena con la regla de ClasicaNdx: deduplicadas por sello
     (gana el generado mas temprano, como BajadorCboe), en orden de generado, la ULTIMA con generado (al segundo) <= t.
  B. AUDIT: cada AUDIT capa=NDX con base=243.06: la cadena de la 4.1 con el MISMO sello de CBOE que el AUDIT (cadenaTs), si la hay; su fut, su hora
     (ART + 3 h) y la base 243,06.
  C. CASOS BORDE con cadenas sinteticas (cada uno con su expectativa escrita a mano: que strikes, que lado y que libro), verificados aca contra el port.
La salida (json) lleva las entradas, lo que dio el port y, en C, las expectativas: el arnes C# compara SU cuenta (DominantesClasica) con todo eso.
"""
import bisect
import datetime as dt
import gzip
import io
import json
import math
import os
import re
import sys

sys.dont_write_bytecode = True        # no dejar __pycache__ en la carpeta del laboratorio
AQUI = os.path.dirname(os.path.abspath(__file__))
PORT = os.path.normpath(os.path.join(AQUI, "..", "..", "..", "laboratorio", "calibracion_1009", "receta_clasica_dominantes", "dominantes"))
sys.path.insert(0, PORT)
import extraer_dominantes_clasica as X   # noqa: E402  (el port; solo se usan sus funciones)

UTC = dt.timezone.utc
EPOCA = dt.datetime(1970, 1, 1, tzinfo=UTC)
BASE_RUEDA = 243.06


def leer_gz(ruta):
    """Las lineas de un .jsonl.gz que se esta escribiendo: lo que se pueda leer (la ultima puede estar cortada)."""
    out = []
    try:
        with gzip.open(ruta, "rt", encoding="utf-8") as fh:
            for l in fh:
                out.append(l)
    except (EOFError, OSError, gzip.BadGzipFile):
        pass
    return out


def cadenas_41(carpeta):
    """Las cadenas de la 4.1 como las ve ClasicaNdx: por sello la de generado mas temprano, en orden de generado."""
    por_ts = {}
    n = 0
    for f in sorted(os.listdir(carpeta)):
        if not (f.startswith("cadena-NQ-") and f.endswith(".jsonl.gz")):
            continue
        for l in leer_gz(os.path.join(carpeta, f)):
            mg = re.search(r'"generado":"([^"]+)"', l[:200]); mt = re.search(r'"cadena_ts":"([^"]+)"', l[:300])
            if not mg or not mt:
                continue
            try:
                g = dt.datetime.fromisoformat(mg.group(1)).astimezone(UTC)
            except ValueError:
                continue
            n += 1
            ts = mt.group(1)
            if ts not in por_ts or g < por_ts[ts][0]:
                por_ts[ts] = (g, l)
    lista = sorted(((g, ts, l) for ts, (g, l) in por_ts.items()), key=lambda z: z[0])
    return lista, n


def seg(g):
    return g.replace(microsecond=0)


def doms_py(res, base):
    """[pos, gex (USD), rol, K] de cada dominante (K = el strike: Fut del ganador - base)."""
    if not res or res.get("sin_base"):
        return None
    return [[d[0], d[1], d[2], g[0] - base] for d, g in zip(res["doms"], res["ganadores"])]


def parte_a(niv_dir, lista):
    gsec = [seg(g) for g, _, _ in lista]
    cache = {}
    out = []
    for f in sorted(os.listdir(niv_dir)):
        if not f.endswith(".jsonl"):
            continue
        with io.open(os.path.join(niv_dir, f), encoding="utf-8", errors="replace") as fh:
            for l in fh:
                if '"R10_NDX_dom"' not in l:
                    continue
                try:
                    r = json.loads(l)
                except ValueError:
                    continue          # la ultima linea de una copia puede estar cortada
                mx = [x for x in r.get("mx", []) if x.get("l") == "Clasica NDX"]
                if not mx:
                    out.append({"k": r["k"], "error": "sin la meta 'Clasica NDX'"}); continue
                base, S = mx[0]["c"], mx[0]["s"]
                fut = round(S + base, 2)
                t = EPOCA + dt.timedelta(minutes=r["k"])
                i = bisect.bisect_right(gsec, t) - 1
                if i < 0:
                    out.append({"k": r["k"], "error": "sin cadena con generado <= t"}); continue
                g, ts, linea = lista[i]
                if ts not in cache:
                    if len(cache) > 32:
                        cache.clear()
                    cache[ts] = X.parsear_cadena_json(linea)
                c = cache[ts]
                res = X.calcular(c, fut, t, base_forzada=base)
                out.append({"k": r["k"], "t": t.strftime("%Y-%m-%dT%H:%M:%SZ"), "fut": fut, "base": base, "S": S, "gen": g.strftime("%Y-%m-%dT%H:%M:%SZ"),
                            "ts": ts, "libro": res.get("libro_dom") if res else None, "doms": doms_py(res, base), "guardado": r["s"]["R10_NDX_dom"],
                            "archivo": f})
    return out


def parte_b(audit_txt, lista):
    por_ts = {ts: (g, l) for g, ts, l in lista}
    cache = {}
    out = []
    if not os.path.exists(audit_txt):
        return out
    with io.open(audit_txt, encoding="utf-8", errors="replace") as fh:
        for l in fh:
            i = l.find("  AUDIT capa=NDX ")
            if i < 0:
                continue
            kv = {}
            for tok in l[i + 8:].strip().split(" "):
                k, _, v = tok.partition("=")
                if k and k not in kv:
                    kv[k] = v
            if kv.get("base") != "243.06":
                continue
            t_loc = dt.datetime.strptime(l[:19], "%Y-%m-%dT%H:%M:%S")
            t = t_loc.replace(tzinfo=UTC) + dt.timedelta(hours=3)          # la PC esta en ART (UTC-3)
            ts = (kv.get("cadenaTs") or "").replace("_", " ")
            fut = float(kv["fut"])
            reg = {"t": t.strftime("%Y-%m-%dT%H:%M:%SZ"), "fut": fut, "ts": ts, "gen_clasica": kv.get("gen", ""), "libro_audit": kv.get("libroDom", ""),
                   "doms_audit": [[a, b] for a, b in X.parsear_doms(kv.get("doms"))]}
            if ts not in por_ts:
                reg["sin_cadena_41"] = True
                out.append(reg); continue
            g, linea = por_ts[ts]
            if ts not in cache:
                if len(cache) > 32:
                    cache.clear()
                cache[ts] = X.parsear_cadena_json(linea)
            res = X.calcular(cache[ts], fut, t, base_forzada=BASE_RUEDA)
            reg.update({"gen": g.strftime("%Y-%m-%dT%H:%M:%SZ"), "libro": res.get("libro_dom") if res else None, "doms": doms_py(res, BASE_RUEDA)})
            out.append(reg)
    return out


# ------------------------------------------------------------------------------------------------ C. casos borde
GEN = dt.datetime(2026, 10, 9, 15, 0, 0, tzinfo=UTC)
B = 243.0                   # base de los casos (K + B = el precio del strike en el futuro)
FUT = 31093.0               # S = 30850: K = 30850 cae justo en el precio
IV = 0.20


def g1(K, S, T, call=True):
    """GEX del port con 1 contrato (para fijar pesos relativos con el volumen o el OI)."""
    f = X.Fila(K, 0, 0, 0, IV, IV, 1.0 if call else 0.0, 0.0 if call else 1.0)
    return abs(X.gex(f, S, T, X.TASA, True, False))


def fila(dK, peso, signo=1, libro="vol", v=0, dias=None):
    """Una fila en K = S + dK con |GEX| = peso unidades (signo + call, - put) por volumen u OI; el otro libro en 0."""
    S = FUT - B
    K = S + dK
    T = max(dias[v], X.PISO_DIAS) / 365.0
    u = 1e6 / g1(S, S, T)               # contratos por unidad en el precio (1 unidad = 1 M USD por 1 % en el ATM)
    q = peso * u * g1(S, S, T) / g1(K, S, T, signo > 0)
    call = signo > 0
    vc, vp = (q, 0.0) if call else (0.0, q)
    if libro == "vol":
        return [K, v, 0.0, 0.0, IV, IV, vc, vp]
    return [K, v, vc, vp, IV, IV, 0.0, 0.0]       # por OI: el volumen en 0


CASOS = [
    # id, descripcion, dias, filas (dK, peso, signo, libro, venc), horas desde GEN, expectativa: libro y [(dK del ganador, lado)]
    ("C1", "sin volumen en el radio -> OI (el unico con volumen esta a +150, fuera del radio de 100)", [0.25],
     [(20, 5, 1, "oi"), (60, 10, 1, "oi"), (-30, 8, -1, "oi"), (-80, 3, 1, "oi"), (150, 40, 1, "vol")], 0,
     ("OI", [(60, "arriba"), (-30, "abajo"), (20, "relleno")])),
    ("C2", "un solo strike con volumen en el radio: libro vol y UNA sola dominante (el resto del radio solo tiene OI)", [0.25],
     [(40, 6, 1, "vol"), (20, 5, 1, "oi"), (-30, 8, -1, "oi")], 0,
     ("vol", [(40, "arriba")])),
    ("C3", "un solo lado (todo arriba): D1 el elegido de arriba, D2 y D3 relleno por peso", [0.25],
     [(10, 6, 1, "vol"), (40, 10, 1, "vol"), (70, 9, 1, "vol"), (90, 2, 1, "vol")], 0,
     ("vol", [(40, "arriba"), (70, "relleno"), (10, "relleno")])),
    ("C4", "un solo lado (todo abajo, puts): D1 el elegido de abajo, D2 y D3 relleno", [0.25],
     [(-10, 6, -1, "vol"), (-40, 10, -1, "vol"), (-70, 9, -1, "vol"), (-90, 2, -1, "vol")], 0,
     ("vol", [(-40, "abajo"), (-70, "relleno"), (-10, "relleno")])),
    ("C5", "empate del 20 %: arriba gana la MAS CERCANA (85 % del mayor); abajo la de 75 % queda afuera y gana la mayor", [0.25],
     [(20, 8.5, 1, "vol"), (50, 10, 1, "vol"), (-20, 7.5, -1, "vol"), (-50, 10, -1, "vol")], 0,
     ("vol", [(20, "arriba"), (-50, "abajo"), (50, "relleno")])),
    ("C6", "el strike justo en el precio va ABAJO (Fut <= fut) y gana por distancia 0", [0.25],
     [(0, 10, 1, "vol"), (30, 4, 1, "vol"), (-30, 9, -1, "vol")], 0,
     ("vol", [(30, "arriba"), (0, "abajo"), (-30, "relleno")])),
    ("C7", "el tope del radio (min(2 %, 100 pts)): +-100 entran, +-105 no; el centroide usa el perfil ENTERO (+105 a 5 pts de +100 lo corre)", [0.25],
     [(100, 10, 1, "vol"), (105, 50, 1, "vol"), (-100, 10, -1, "vol"), (-105, 50, -1, "vol")], 0,
     ("vol", [(100, "arriba"), (-100, "abajo")])),
    ("C8", "dos strikes: dos dominantes, sin D3", [0.25],
     [(30, 5, 1, "vol"), (-30, 5, -1, "vol")], 0,
     ("vol", [(30, "arriba"), (-30, "abajo")])),
    ("C9", "centroide de +-12 pts: vecinos a 10 pts entran (pesan), el de 15 no; el ganador en +40", [0.25],
     [(30, 6, 1, "vol"), (40, 10, 1, "vol"), (50, 2, 1, "vol"), (55, 9, 1, "vol"), (-60, 3, -1, "vol")], 0,
     ("vol", [(40, "arriba"), (-60, "abajo"), (55, "relleno")])),
    ("C10", "volumen que se cancela (calls = puts, misma IV): GEX por volumen 0 en todo el radio -> OI", [0.25],
     [("cancela", 20), (25, 7, 1, "oi"), (-25, 9, -1, "oi")], 0,
     ("OI", [(25, "arriba"), (-25, "abajo")])),
    ("C11", "horizonte Hoy: el vencimiento de 3,25 dias (volumen enorme) no entra; el de 0,9 si", [0.25, 0.9, 3.25],
     [(20, 6, 1, "vol", 0), (-20, 5, -1, "vol", 1), (60, 500, 1, "vol", 2), (-60, 500, -1, "vol", 2)], 0,
     ("vol", [(20, "arriba"), (-20, "abajo")])),
    ("C12", "envejecida 2 h: el vencimiento de 0,05 dias ya vencio (sale) y el de 1,05 pasa a ser el mas cercano", [0.05, 1.05],
     [(20, 50, 1, "vol", 0), (-20, 50, -1, "vol", 0), (40, 4, 1, "vol", 1), (-40, 3, -1, "vol", 1)], 2,
     ("vol", [(40, "arriba"), (-40, "abajo")])),
]


def parte_c():
    out = []
    S = FUT - B
    for cid, desc, dias, specs, horas, (libro_esp, esp) in CASOS:
        filas = []
        for s in specs:
            if s[0] == "cancela":         # calls = puts con la misma IV: el GEX por volumen da 0 exacto
                K = S + s[1]
                filas.append([K, 0, 0.0, 0.0, IV, IV, 37.0, 37.0])
                continue
            dK, peso, signo, libro = s[0], s[1], s[2], s[3]
            v = s[4] if len(s) > 4 else 0
            filas.append(fila(dK, peso, signo, libro, v, dias))
        filas.sort(key=lambda a: (a[0], a[1]))
        t = GEN + dt.timedelta(hours=horas)
        linea = json.dumps({"generado": GEN.isoformat(), "cadena_ts": GEN.strftime("%Y-%m-%d %H:%M:%S"),
                            "cadena": {"ts": GEN.strftime("%Y-%m-%d %H:%M:%S"), "spot_idx": S, "vencimientos": [{"dias": d} for d in dias], "filas": filas}})
        c = X.parsear_cadena_json(linea)
        res = X.calcular(c, FUT, t, base_forzada=B)
        doms = doms_py(res, B) or []
        got = [(round(d[3] - S, 6), d[2]) for d in doms]          # (dK del ganador, lado)
        ok = (res is not None and res.get("libro_dom") == libro_esp and got == [(float(a), b) for a, b in esp])
        out.append({"id": cid, "desc": desc, "generado": GEN.strftime("%Y-%m-%dT%H:%M:%SZ"), "t": t.strftime("%Y-%m-%dT%H:%M:%SZ"), "dias": dias, "filas": filas,
                    "fut": FUT, "base": B, "S": S, "libro": res.get("libro_dom") if res else None, "doms": doms,
                    "esperado": {"libro": libro_esp, "doms": [[a, b] for a, b in esp]}, "port_ok": ok})
        print("  %-4s %-5s %s | port: libro %s %s" % (cid, "OK" if ok else "MAL", desc[:70], res.get("libro_dom") if res else None,
                                                     " ".join("%+g:%s" % (a, b) for a, b in got)))
    return out


def main():
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass
    cop, salida = sys.argv[1], sys.argv[2]
    lista, n = cadenas_41(os.path.join(cop, "cboe4"))
    print("ref_dominantes.py: port %s; cadenas de la 4.1: %d lineas, %d sellos" % (os.path.join(PORT, "extraer_dominantes_clasica.py"), n, len(lista)))
    a = parte_a(os.path.join(cop, "niv"), lista)
    print("A. minutos de niv con R10_NDX_dom: %d" % len(a))
    b = parte_b(os.path.join(cop, "audit_ndx.txt"), lista)
    print("B. AUDIT capa=NDX con base 243.06: %d (con la cadena de la 4.1 del mismo sello: %d)" % (len(b), sum(1 for x in b if "sin_cadena_41" not in x)))
    print("C. casos borde:")
    c = parte_c()
    with io.open(salida, "w", encoding="utf-8") as fh:
        json.dump({"port": os.path.join(PORT, "extraer_dominantes_clasica.py"), "niv": a, "audit": b, "casos": c}, fh)
    return 0 if all(x["port_ok"] for x in c) else 3


if __name__ == "__main__":
    sys.exit(main())
