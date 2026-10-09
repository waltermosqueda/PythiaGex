# -*- coding: utf-8 -*-
"""comparar_cboe.py — paridad del modulo cboe (B2) de PythiaGex 4.1 contra el Python de PRODUCCION (solo lectura, sin red).

Correr con:  python -I comparar_cboe.py <modo> ...   (se pone solo en BELOW_NORMAL)
  crudos <dir con *.cs.json>      construir + medir + linea_flaca de pythiagex/archivar_cadena sobre el MISMO crudo y el MISMO 'ahora'
                                  -> la linea del C# (sin los bloques nuevos "sub" y "bajada") tiene que ser IGUAL byte a byte
  lineas <cs.json> <archivos...>  foto_cboe_de_json (copia literal de preview_niveles.py, que no se importa porque arrastra la cuenta
                                  entera) sobre cada linea de los archivos -> mismos generado, ts, t_dato, spot, oi_total, err, n_filas, dias
  py <cs_nums.txt>                round()/repr() de Python contra Py.Round/Py.Repr del modulo
  bajada <dir>                    la bajada real del arnes: crudo comprimido + generado exacto -> misma linea que la archivada por el C#
No toca motor.py, fuentes.py, servir.py ni cboe_local.py: solo los importa (cadena_atas, base, archivar_cadena) y llama funciones puras.
"""
import ctypes, sys, os, gzip, json, re, math, time
import datetime as dt
from datetime import datetime, timedelta, timezone

ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)   # BELOW_NORMAL

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.abspath(os.path.join(AQUI, "..", "..", ".."))          # .../PythiaGex
sys.path.insert(0, RAIZ)
from pythiagex import cadena_atas as CAD, base as B                     # noqa: E402
import archivar_cadena as AC                                            # noqa: E402

B._TASAS["c"] = None      # sin red: la curva del Tesoro no se baja (confiable -> False, base -> None; igual que el C#, que escribe null/false)

RX_SUB = re.compile(r',"sub":\{[^{}]*\}')
RX_BAJ = re.compile(r',"bajada":\{[^{}]*\}')
fallas = 0


def mal(m):
    global fallas
    fallas += 1
    print("  MAL  " + m)


def bien(m):
    print("  OK   " + m)


def dif_texto(a, b, nombre):
    if a == b:
        bien("%s: igual byte a byte (%d caracteres)" % (nombre, len(a)))
        return True
    i = next((k for k in range(min(len(a), len(b))) if a[k] != b[k]), min(len(a), len(b)))
    mal("%s: difiere en el caracter %d (py %d car., cs %d car.)\n       py: ...%s...\n       cs: ...%s..." % (
        nombre, i, len(a), len(b), a[max(0, i - 80):i + 80], b[max(0, i - 80):i + 80]))
    return False


def linea_python(crudo, ahora):
    """bajar_y_archivar sin la red ni el disco: la misma cuenta y la misma linea."""
    t0 = time.perf_counter()
    cad = CAD.construir(crudo, ahora=ahora)
    t1 = time.perf_counter()
    try:
        b = B.medir(crudo)
    except Exception as e:
        b = {"base": None, "confiable": False, "aviso": str(e)}
    t2 = time.perf_counter()
    if b is None:
        b = {}            # en produccion esto revienta bajar_y_archivar (AttributeError) y la cadena NO se archiva
    base = b.get("base") if b.get("confiable") else None
    ts = crudo.get("timestamp")
    linea = {
        "generado": ahora.isoformat(timespec="seconds"), "cadena_ts": ts,
        "edad_min": round((ahora - datetime.strptime(ts, "%Y-%m-%d %H:%M:%S").replace(tzinfo=timezone.utc)).total_seconds() / 60.0, 1),
        "spot": crudo["data"].get("current_price"),
        "base": base, "base_confiable": base is not None, "base_cruda": b.get("base"), "base_error_ticks": b.get("residuo_ticks"),
        "fuente": "cboe directo (cadenas.yml)", "cadena": cad,
    }
    flaca = AC.linea_flaca(linea)
    return cad, b, flaca, (t1 - t0) * 1000, (t2 - t1) * 1000


def sin_bloques_nuevos(txt):
    return RX_BAJ.sub("", RX_SUB.sub("", txt, count=1), count=1)


def comparar_medir(b, m, nombre):
    if not b:
        if m is None:
            bien("%s: medir no da en los dos" % nombre)
        else:
            mal("%s: medir da en C# y no en Python" % nombre)
        return
    if m is None:
        mal("%s: medir da en Python y no en C#" % nombre)
        return
    pares = [("base", b["base"], m["base"]), ("contado", b["contado_implicito"], m["contado"]), ("forward", b["forward"], m["forward"]),
             ("residuo_ticks", b["residuo_ticks"], m["residuo_ticks"]), ("vencimiento", b["vencimiento"], m["venc"]),
             ("cercano", b["vencimiento_cercano"], m["cercano"]), ("puntos", b["vencimientos_usados"], m["puntos"]),
             ("muestras", b["muestras"], m["muestras"]), ("dispersion", b["dispersion"], m["dispersion"])]
    malos = [(k, x, y) for k, x, y in pares if x != y]
    curva_py = [[c["vencimiento"], c["dias"], c["forward"], c["muestras"], c["dispersion"]] for c in b["curva_forward"]]
    if curva_py != m["curva"]:
        malos.append(("curva", curva_py, m["curva"]))
    if malos:
        mal("%s: medir difiere: %s" % (nombre, malos))
    else:
        bien("%s: medir igual (base_cruda %s, contado %s, forward %s, err %s tk, %d vencimientos, cercano %s)" % (
            nombre, b["base"], b["contado_implicito"], b["forward"], b["residuo_ticks"], b["vencimientos_usados"], b["vencimiento_cercano"]))


def modo_crudos(d):
    for f in sorted(os.listdir(d)):
        if not f.endswith(".cs.json"):
            continue
        cs = json.load(open(os.path.join(d, f), encoding="utf-8"))
        nombre = f[:-len(".cs.json")]
        crudo = json.loads(gzip.open(cs["crudo"]).read())
        ahora = datetime.fromisoformat(cs["ahora"])
        if cs["hoy"] != dt.date.today().isoformat():
            print("  aviso: el C# corrio con hoy=%s y Python con %s (medir usa la fecha local)" % (cs["hoy"], dt.date.today()))
        print("== %s (%d contratos; C# ms %s)" % (nombre, cs["n_contratos"], cs["ms"]))
        cad, b, flaca, tc, tm = linea_python(crudo, ahora)
        print("     Python: construir %.0f ms, medir %.0f ms" % (tc, tm))
        py = json.dumps(flaca, ensure_ascii=False, separators=(",", ":"))
        dif_texto(py, sin_bloques_nuevos(cs["linea"]), "linea flaca")
        completa = {k: cad.get(k) for k in ("ts", "spot_idx", "ultimo_trade", "horizonte_dias", "vencimientos", "campos")}
        completa["filas"] = cad["filas"]
        completa["dias_max_archivo"] = 1e9
        dif_texto(json.dumps(completa, ensure_ascii=False, separators=(",", ":")),
                  _texto_crudo(cs, "cadena_completa", d, f),
                  "construir completo (%d filas a <= 14 dias, %d vencimientos)" % (len(cad["filas"]), len(cad["vencimientos"])))
        comparar_medir(b, cs["medir"], nombre)
        # la foto releida por el lector del modulo vs foto_cboe_de_json sobre la linea de Python
        fp = foto_cboe_de_json(json.loads(py))
        r = cs["releida"]
        if fp is None or r is None:
            (bien if fp is None and r is None else mal)("releida: py %s / cs %s" % (fp is not None, r is not None))
        else:
            comparar_foto(fp, r, "foto releida (generado/t_dato/spot/oi_total/n_filas)", con_sumas=False)
        sub = crudo["data"]
        print("     sub: current %s close %s prev_day_close %s price_change %s last_trade_time %s | C# releida prev_day_close %s" % (
            sub.get("current_price"), sub.get("close"), sub.get("prev_day_close"), sub.get("price_change"), sub.get("last_trade_time"), r and r.get("prev_day_close")))


def _texto_crudo(cs, clave, d, f):
    """El texto EXACTO que escribio el C# para una clave (sin pasar por json.loads, que normaliza 1233.0 vs 1233)."""
    raw = open(os.path.join(d, f), encoding="utf-8").read()
    i = raw.index('"%s":' % clave) + len(clave) + 3
    prof = 0
    for k in range(i, len(raw)):
        ch = raw[k]
        if ch == "{":
            prof += 1
        elif ch == "}":
            prof -= 1
            if prof == 0:
                return raw[i:k + 1]
    return raw[i:]


# ------------------------------------------------------------------ copia literal de preview_niveles.foto_cboe_de_json (con libros._utc / _ny_a_utc)
from zoneinfo import ZoneInfo   # noqa: E402
NY = ZoneInfo("America/New_York")
RETRASO_S = 902


def _utc(s):
    d = datetime.fromisoformat(s.replace("Z", "+00:00"))
    if d.tzinfo is not None:
        d = d.astimezone(timezone.utc).replace(tzinfo=None)
    return d


def _ny_a_utc(s):
    d = datetime.fromisoformat(s)
    if d.tzinfo is None:
        d = d.replace(tzinfo=NY)
    return d.astimezone(timezone.utc).replace(tzinfo=None)


def foto_cboe_de_json(j, retraso=RETRASO_S):
    try:
        gen = _utc(str(j["generado"]).replace("\\u002B", "+")); ts = str(j["cadena_ts"])
    except Exception:
        return None
    c = j.get("cadena") or {}
    filas = [x[:8] for x in c.get("filas", []) if len(x) >= 8]
    if not filas:
        return None
    ut = c.get("ultimo_trade")
    try:
        t_dato = _ny_a_utc(ut) if ut else datetime.strptime(ts, "%Y-%m-%d %H:%M:%S") - timedelta(seconds=int(j.get("retraso_s") or retraso))
    except Exception:
        t_dato = datetime.strptime(ts, "%Y-%m-%d %H:%M:%S") - timedelta(seconds=retraso)
    import numpy as np
    Fl = np.asarray(filas, float).reshape(-1, 8)
    f = dict(generado=gen, ts=ts, t_dato=t_dato, spot=float(c.get("spot_idx") or j.get("spot") or 0), n_filas=len(filas),
             dias=[float(v.get("dias") or 0) for v in c.get("vencimientos", [])], base_cruda=j.get("base_cruda"))
    be = j.get("base_error_ticks")
    f["err"] = float(be) if be is not None else float("nan")
    f["oi_total"] = float(Fl[:, 2].sum() + Fl[:, 3].sum()) if len(Fl) else 0.0
    sk = sv = si = 0.0
    for x in filas:
        sk += float(x[0]) * (int(float(x[1])) + 1); sv += float(x[6]) - float(x[7]); si += float(x[4]) + 2 * float(x[5])
    f["suma_k"], f["suma_vol"], f["suma_iv"] = sk, sv, si
    return f


def _iso(t):
    return t.replace(tzinfo=timezone.utc).isoformat(timespec="microseconds")


def comparar_foto(fp, r, nombre, con_sumas=True):
    malos = []
    if _iso(fp["generado"]) != r["generado"]:
        malos.append(("generado", _iso(fp["generado"]), r["generado"]))
    if _iso(fp["t_dato"]) != r["t_dato"]:
        malos.append(("t_dato", _iso(fp["t_dato"]), r["t_dato"]))
    if fp["spot"] != r["spot"]:
        malos.append(("spot", fp["spot"], r["spot"]))
    if fp["oi_total"] != r["oi_total"]:
        malos.append(("oi_total", fp["oi_total"], r["oi_total"]))
    if fp["n_filas"] != r["n_filas"]:
        malos.append(("n_filas", fp["n_filas"], r["n_filas"]))
    if con_sumas:
        if fp["ts"] != r["ts"]:
            malos.append(("ts", fp["ts"], r["ts"]))
        e1, e2 = fp["err"], r["err"]
        if not ((e1 != e1 and e2 is None) or e1 == e2):
            malos.append(("err", e1, e2))
        if fp["base_cruda"] != r["base_cruda"]:
            malos.append(("base_cruda", fp["base_cruda"], r["base_cruda"]))
        if fp["dias"] != r["dias"]:
            malos.append(("dias", fp["dias"], r["dias"]))
        for k in ("suma_k", "suma_vol", "suma_iv"):
            if fp[k] != r[k]:
                malos.append((k, fp[k], r[k]))
    if malos:
        mal("%s: %s" % (nombre, malos[:6]))
        return False
    if not con_sumas:
        bien(nombre)
    return True


def modo_lineas(cs_json, archivos):
    cs = json.load(open(cs_json, encoding="utf-8"))
    for p in archivos:
        n = os.path.basename(p)
        t0 = time.perf_counter()
        fotos = []
        with gzip.open(p, "rt", encoding="utf-8", errors="replace") as fh:
            for l in fh:
                l = l.strip()
                if not l:
                    continue
                try:
                    j = json.loads(l)
                except Exception:
                    continue
                f = foto_cboe_de_json(j)
                if f is not None:
                    fotos.append(f)
        ms = (time.perf_counter() - t0) * 1000
        lc = cs.get(n, [])
        if len(lc) != len(fotos):
            mal("%s: %d fotos en Python, %d en C#" % (n, len(fotos), len(lc)))
            continue
        malas = 0
        for fp, r in zip(fotos, lc):
            if not comparar_foto(fp, r, "%s linea gen %s" % (n, _iso(fp["generado"]))):
                malas += 1
                if malas > 3:
                    break
        if malas == 0:
            bien("%s: %d lineas iguales en generado, ts, t_dato, spot, oi_total, err, base_cruda, n_filas, dias y sumas de filas (Python %.0f ms)" % (n, len(fotos), ms))


def modo_py(ruta):
    n = 0
    for l in open(ruta, encoding="utf-8"):
        p = l.rstrip("\n").split("\t")
        if len(p) < 5:
            continue
        x = float(p[0])
        esp = [repr(x), repr(round(x, 1)), repr(round(x, 2)), repr(round(x, 4))]
        if esp != p[1:5]:
            mal("x=%r py %s cs %s" % (x, esp, p[1:5]))
            if fallas > 10:
                break
        n += 1
    if fallas == 0:
        bien("%d numeros: repr y round(x, 1/2/4) iguales" % n)


def modo_bajada(d):
    crudos = os.path.join(d, "crudos")
    arch = {"_NDX": "NQ", "QQQ": "QQQ", "TQQQ": "TQQQ"}
    for sim, ar in arch.items():
        pc = os.path.join(crudos, sim + ".json.gz")
        if not os.path.exists(pc):
            mal("%s: no hay crudo bajado" % sim)
            continue
        gz = open(pc, "rb").read()
        crudo = json.loads(gzip.decompress(gz))
        ahora = datetime.fromisoformat(open(os.path.join(crudos, sim + ".generado.txt"), encoding="utf-8").read().strip())
        cs_txt = open(os.path.join(d, "cboe", "ultima-%s.json" % ar), encoding="utf-8").read()
        print("== %s: %d B comprimido, %d contratos, timestamp %s, generado %s" % (sim, len(gz), len(crudo["data"]["options"]), crudo.get("timestamp"), ahora.isoformat()))
        cad, b, flaca, tc, tm = linea_python(crudo, ahora)
        py = json.dumps(flaca, ensure_ascii=False, separators=(",", ":"))
        dif_texto(py, sin_bloques_nuevos(cs_txt), "linea archivada por el C# vs Python sobre el mismo crudo")
        j = json.loads(cs_txt)
        print("     sub: %s" % j.get("sub"))
        print("     bajada: %s" % j.get("bajada"))
        # el archivo del dia tiene que tener esa misma linea
        dia = j["generado"][:10]
        with gzip.open(os.path.join(d, "cboe", "cadena-%s-%s.jsonl.gz" % (ar, dia)), "rt", encoding="utf-8") as fh:
            ls = [x.rstrip("\n") for x in fh if x.strip()]
        (bien if ls and ls[-1] == cs_txt else mal)("cadena-%s-%s.jsonl.gz: %d linea(s), la ultima = ultima-%s.json" % (ar, dia, len(ls), ar))


if __name__ == "__main__":
    modo = sys.argv[1]
    if modo == "crudos":
        modo_crudos(sys.argv[2])
    elif modo == "lineas":
        modo_lineas(sys.argv[2], sys.argv[3:])
    elif modo == "py":
        modo_py(sys.argv[2])
    elif modo == "bajada":
        modo_bajada(sys.argv[2])
    print("RESULTADO: %s" % ("TODO IGUAL" if fallas == 0 else "%d DIFERENCIAS" % fallas))
    sys.exit(0 if fallas == 0 else 1)
