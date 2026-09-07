# -*- coding: utf-8 -*-
"""EL PANEL LOCAL DEL SIMULADOR: un mini ATAS de simulacion, en tu PC.

Sirve una pagina en http://127.0.0.1:8770 con los parametros del motor, la
carga de archivos de datos, la descarga de Databento (cotizada, con techo) y
el boton "simular", que corre la MISMA cuenta que el indicador de ATAS
(atas/Rebobina, compilado de los mismos .cs) y regenera el visor.

No toca ATAS. Todo queda en datos/simulador y datos/databento.

Uso:  python herramientas/panel_local.py [--puerto 8770] [--sin-navegador]
"""
import argparse
import cgi
import datetime as dt
import glob
import io
import json
import os
import re
import subprocess
import sys
import threading
import traceback
import webbrowser
from concurrent.futures import ThreadPoolExecutor
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlparse

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
os.chdir(RAIZ)
sys.path.insert(0, RAIZ)
sys.path.insert(0, os.path.join(RAIZ, "herramientas"))
PY = sys.executable
HTML = os.path.join(RAIZ, "herramientas", "panel_local.html")
VISOR = os.path.join(RAIZ, "datos", "simulador", "visor.html")
EXE = os.path.join(RAIZ, "atas", "Rebobina", "bin", "Release", "Rebobina.exe")
ATAS = os.path.join(os.environ.get("APPDATA", ""), "ATAS")

_llave = threading.Lock()
_trabajo = {"activo": False, "nombre": "", "log": [], "resultado": None, "inicio": None}


def log(msg):
    with _llave:
        _trabajo["log"].append("%s  %s" % (dt.datetime.now().strftime("%H:%M:%S"), msg))
        if len(_trabajo["log"]) > 400:
            del _trabajo["log"][:100]


# ------------------------------------------------------------------ estado
def dias_opra(libro="SPX"):
    padres = {"SPX": "SPX_OPT+SPXW_OPT", "NDX": "NDX_OPT+NDXP_OPT"}[libro]
    dias = {}
    for esquema in ("definition", "statistics", "ohlcv-1m"):
        for p in glob.glob(os.path.join("datos", "databento", "OPRA_PILLAR", esquema, padres + "-*.dbn.zst")):
            m = re.search(r"-(\d{4}-\d{2}-\d{2})-\d{4}-\d{2}-\d{2}\.dbn\.zst$", p)
            if m:
                dias.setdefault(m.group(1), set()).add(esquema)
    return {d: sorted(e) for d, e in sorted(dias.items())}


def cadenas_listas():
    out = {}
    for p in glob.glob(os.path.join("datos", "simulador", "cadenas", "sim-*.jsonl.gz")):
        m = re.search(r"sim-(\w+)-(\d{4}-\d{2}-\d{2})-r(\d+)\.jsonl\.gz$", os.path.basename(p))
        if m:
            out.setdefault(m.group(2), []).append({"raiz": m.group(1), "retraso": int(m.group(3)), "mb": round(os.path.getsize(p) / 1e6, 1)})
    return out


def ledger():
    try:
        import databento_bajar as DB
        gastado = DB.gastado()
        return {"gastado": round(gastado, 2), "tope": DB.TOPE_USD, "credito": 125.0,
                "clave": os.path.exists(DB.CLAVE)}
    except Exception as e:
        return {"error": str(e)}


def estado():
    velas = [{"archivo": os.path.basename(p), "mb": round(os.path.getsize(p) / 1e6, 1)}
             for p in glob.glob(os.path.join("datos", "simulador", "velas", "*.csv"))]
    cent = []
    for p in glob.glob(os.path.join(ATAS, "pythiagex-centinela-rebobinado*-*.jsonl")):
        cent.append({"archivo": os.path.basename(p), "velas": sum(1 for _ in io.open(p, encoding="utf-8", errors="replace")),
                     "modificado": dt.datetime.fromtimestamp(os.path.getmtime(p)).strftime("%Y-%m-%d %H:%M")})
    return {
        "hora": dt.datetime.now(dt.timezone.utc).strftime("%Y-%m-%d %H:%M UTC"),
        "motor": os.path.exists(EXE),
        "visor": os.path.exists(VISOR),
        "dias_opra": dias_opra("SPX"),
        "cadenas": cadenas_listas(),
        "velas": velas,
        "centinelas": cent,
        "databento": ledger(),
        "indicadores": [
            {"id": "hoy", "nombre": "Gamma Hoy", "listo": True, "nota": "la misma cuenta que ATAS (GammaHoyNucleo)"},
            {"id": "vivo", "nombre": "Gamma Vivo", "listo": True,
             "nota": "zero y muros por interes abierto a 7 dias con el mismo nucleo; las dominantes del radar (radar.py) se calculan al convertir"},
        ],
        "trabajo": {"activo": _trabajo["activo"], "nombre": _trabajo["nombre"]},
    }


# ------------------------------------------------------------------ trabajos largos
def correr(nombre, fn):
    with _llave:
        if _trabajo["activo"]:
            return False
        _trabajo.update(activo=True, nombre=nombre, log=[], resultado=None, inicio=dt.datetime.now())

    def cuerpo():
        try:
            r = fn()
            with _llave:
                _trabajo["resultado"] = r if r is not None else {"ok": True}
        except SystemExit as e:
            log("DETENIDO: %s" % e)
            with _llave:
                _trabajo["resultado"] = {"ok": False, "error": str(e)}
        except Exception as e:
            log("ERROR: %s" % e)
            log(traceback.format_exc()[-1500:])
            with _llave:
                _trabajo["resultado"] = {"ok": False, "error": str(e)}
        finally:
            with _llave:
                _trabajo["activo"] = False
    threading.Thread(target=cuerpo, daemon=True).start()
    return True


def sub(cmd, cwd=RAIZ):
    log("$ " + " ".join(os.path.basename(c) if i == 0 else c for i, c in enumerate(cmd))[:200])
    p = subprocess.run(cmd, cwd=cwd, capture_output=True, text=True, encoding="utf-8", errors="replace")
    for l in (p.stdout or "").strip().splitlines()[-12:]:
        log("  " + l[:220])
    if p.returncode != 0:
        for l in (p.stderr or "").strip().splitlines()[-6:]:
            log("  ! " + l[:220])
    return p


def simular(par):
    """Convierte lo que falte, corre el motor con los parametros y regenera el visor."""
    retraso = int(par.get("retraso", 902))
    dias = par.get("dias") or sorted(dias_opra("SPX"))
    marco = int(par.get("marco_min", 1))
    inst = par.get("instrumento", "MES")
    indicadores = par.get("indicadores") or ["hoy"]
    log("simulacion: %d dias, marco %d min, retraso %d s, indicadores %s" % (len(dias), marco, retraso, "+".join(indicadores)))

    faltan = [d for d in dias if not os.path.exists(os.path.join("datos", "simulador", "cadenas", "sim-ES-%s-r%d.jsonl.gz" % (d, retraso)))]
    if faltan:
        log("convertir %d dias de Databento a cadenas por minuto (4 en paralelo, ~5 min por dia)" % len(faltan))
        with ThreadPoolExecutor(max_workers=4) as ex:
            def conv(d):
                cmd = [PY, "herramientas/databento_a_cadenas.py", d, "--retraso", str(retraso)]
                p = subprocess.run(cmd, cwd=RAIZ, capture_output=True, text=True, encoding="utf-8", errors="replace")
                return d, p.returncode, (p.stdout.strip().splitlines() or ["?"])[-1]
            for d, rc, ult in ex.map(conv, faltan):
                log("  %s %s %s" % (d, "ok" if rc == 0 else "FALLO", ult[:120]))
    cadenas = [os.path.join("datos", "simulador", "cadenas", "sim-ES-%s-r%d.jsonl.gz" % (d, retraso)) for d in dias]
    cadenas = [c for c in cadenas if os.path.exists(c)]
    if not cadenas:
        raise SystemExit("no hay cadenas para esos dias")
    velas = par.get("velas") or "ESU6-1m.csv"
    cmd = [EXE, "--cadenas", ",".join(cadenas), "--velas", os.path.join("datos", "simulador", "velas", velas),
           "--instrumento", inst, "--marco-min", str(marco), "--audit", "0",
           "--horizonte", par.get("horizonte", "Hoy"), "--dominantes", str(par.get("dominantes", 2)),
           "--radio", str(par.get("radio", 2.0)), "--pico", str(par.get("pico", 0.35)), "--mucho", str(par.get("mucho", 50)),
           "--convexidad", par.get("convexidad", "Auto"), "--tasa", str(par.get("tasa", 0.0375)),
           "--edad-max", str(par.get("edad_max", 20)), "--nombre", "rebobinado",
           "--indicadores", "+".join(indicadores)]
    p = sub(cmd)
    if p.returncode != 0:
        raise SystemExit("el motor fallo")
    lab = sub([PY, "laboratorio/rebobinado.py", inst, "M%d" % marco])
    texto = lab.stdout
    if "vivo" in indicadores:
        lab2 = sub([PY, "laboratorio/rebobinado.py", inst, "M%d" % marco, "--nombre", "rebobinado-vivo"])
        texto += "\n" + lab2.stdout
    sub([PY, "herramientas/visor_rebobinado.py", "--marco", "M%d" % marco, "--instrumento", inst])
    log("listo: visor regenerado")
    return {"ok": True, "lab": texto}


def cotizar(par):
    import databento_bajar as DB
    c = DB.cliente()
    total, det = 0.0, []
    d0, d1 = par["desde"], par["hasta"]
    dias = dias_habiles(d0, d1)
    for d in dias:
        n = (dt.date.fromisoformat(d) + dt.timedelta(days=1)).isoformat()
        for esquema in ("definition", "statistics", "ohlcv-1m"):
            if os.path.exists(DB.nombre("OPRA.PILLAR", esquema, ["SPX.OPT", "SPXW.OPT"], d, n)):
                continue
            usd, nreg, mb = DB.cotizar(c, "OPRA.PILLAR", esquema, ["SPX.OPT", "SPXW.OPT"], "parent", d, n)
            total += usd
            det.append({"dia": d, "esquema": esquema, "usd": round(usd, 3), "mb": round(mb, 1)})
    return {"dias": dias, "usd": round(total, 2), "gastado": round(DB.gastado(), 2), "tope": DB.TOPE_USD,
            "entra": DB.gastado() + total <= DB.TOPE_USD, "detalle": det}


def bajar(par):
    import databento_bajar as DB
    c = DB.cliente()
    dias = dias_habiles(par["desde"], par["hasta"])
    for d in dias:
        n = (dt.date.fromisoformat(d) + dt.timedelta(days=1)).isoformat()
        for esquema in ("definition", "statistics", "ohlcv-1m"):
            ruta, usd, det = DB.bajar(c, "OPRA.PILLAR", esquema, ["SPX.OPT", "SPXW.OPT"], "parent", d, n)
            log("%s %-10s USD %.3f  %s" % (d, esquema, usd, det))
    log("descarga lista; gastado USD %.2f de %.2f" % (DB.gastado(), DB.TOPE_USD))
    return {"ok": True}


def dias_habiles(d0, d1):
    a, b = dt.date.fromisoformat(d0), dt.date.fromisoformat(d1)
    out = []
    while a <= b:
        if a.weekday() < 5:
            out.append(a.isoformat())
        a += dt.timedelta(days=1)
    return out[:60]


def guardar_subida(nombre, datos):
    """Ubica el archivo segun lo que es: DBN de Databento (lee su metadata) o CSV de velas."""
    nombre = os.path.basename(nombre)
    if nombre.endswith(".dbn.zst") or nombre.endswith(".dbn"):
        tmp = os.path.join("datos", "databento", "_subidas", nombre)
        os.makedirs(os.path.dirname(tmp), exist_ok=True)
        with open(tmp, "wb") as f:
            f.write(datos)
        import databento as db
        s = db.DBNStore.from_file(tmp)
        md = s.metadata
        ds, esquema = md.dataset, str(md.schema)
        ini = dt.datetime.fromtimestamp(md.start / 1e9, dt.timezone.utc).date().isoformat()
        fin = dt.datetime.fromtimestamp(md.end / 1e9, dt.timezone.utc).date().isoformat() if md.end else ini
        syms = "+".join(md.symbols).replace(".", "_") if md.symbols else "subida"
        dst = os.path.join("datos", "databento", ds.replace(".", "_"), esquema, "%s-%s-%s.dbn.zst" % (syms, ini, fin))
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        os.replace(tmp, dst)
        return "Databento %s %s %s..%s -> %s" % (ds, esquema, ini, fin, os.path.relpath(dst))
    if nombre.lower().endswith(".csv"):
        cab = datos[:400].decode("utf-8", "replace").splitlines()[0].lower() if datos else ""
        if not all(k in cab for k in ("open", "high", "low", "close")):
            return "CSV sin columnas open/high/low/close en la primera linea: hace falta un adaptador para este formato"
        dst = os.path.join("datos", "simulador", "velas", nombre)
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        with open(dst, "wb") as f:
            f.write(datos)
        return "velas -> %s (la primera columna debe ser la hora UTC ISO; ts_event,open,high,low,close,volume)" % os.path.relpath(dst)
    return "formato no reconocido (%s): se aceptan .dbn.zst de Databento y .csv de velas" % nombre


# ------------------------------------------------------------------ http
class Manejador(BaseHTTPRequestHandler):
    def log_message(self, *a):
        pass

    def _json(self, obj, code=200):
        b = json.dumps(obj, ensure_ascii=False).encode("utf-8")
        self.send_response(code); self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(b))); self.end_headers(); self.wfile.write(b)

    def _html(self, ruta):
        if not os.path.exists(ruta):
            self.send_response(404); self.end_headers(); self.wfile.write(b"no existe"); return
        b = io.open(ruta, "rb").read()
        self.send_response(200); self.send_header("Content-Type", "text/html; charset=utf-8")
        self.send_header("Content-Length", str(len(b))); self.end_headers(); self.wfile.write(b)

    def do_GET(self):
        u = urlparse(self.path)
        if u.path == "/":
            return self._html(HTML)
        if u.path == "/visor":
            return self._html(VISOR)
        if u.path == "/api/estado":
            return self._json(estado())
        if u.path == "/api/progreso":
            with _llave:
                return self._json({"activo": _trabajo["activo"], "nombre": _trabajo["nombre"], "log": _trabajo["log"][-120:], "resultado": _trabajo["resultado"]})
        if u.path == "/api/lab":
            p = subprocess.run([PY, "laboratorio/rebobinado.py", "MES", "M1"], capture_output=True, text=True, encoding="utf-8", errors="replace")
            return self._json({"lab": p.stdout})
        self.send_response(404); self.end_headers()

    def do_POST(self):
        u = urlparse(self.path)
        if u.path == "/api/subir":
            ct = self.headers.get("Content-Type", "")
            form = cgi.FieldStorage(fp=self.rfile, headers=self.headers, environ={"REQUEST_METHOD": "POST", "CONTENT_TYPE": ct})
            res = []
            for k in form.keys():
                item = form[k]
                items = item if isinstance(item, list) else [item]
                for it in items:
                    if it.filename:
                        try:
                            res.append(guardar_subida(it.filename, it.file.read()))
                        except Exception as e:
                            res.append("%s: no se pudo guardar (%s)" % (it.filename, e))
            return self._json({"ok": True, "mensajes": res})
        n = int(self.headers.get("Content-Length", "0") or 0)
        par = json.loads(self.rfile.read(n).decode("utf-8") or "{}") if n else {}
        if u.path == "/api/simular":
            ok = correr("simular", lambda: simular(par))
            return self._json({"ok": ok, "error": None if ok else "ya hay un trabajo corriendo"})
        if u.path == "/api/databento/cotizar":
            try:
                return self._json(cotizar(par))
            except Exception as e:
                return self._json({"error": str(e)}, 500)
        if u.path == "/api/databento/bajar":
            if not par.get("confirmar"):
                return self._json({"ok": False, "error": "falta confirmar"})
            ok = correr("bajar", lambda: bajar(par))
            return self._json({"ok": ok, "error": None if ok else "ya hay un trabajo corriendo"})
        self.send_response(404); self.end_headers()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--puerto", type=int, default=8770)
    ap.add_argument("--sin-navegador", action="store_true")
    a = ap.parse_args()
    srv = ThreadingHTTPServer(("127.0.0.1", a.puerto), Manejador)
    url = "http://127.0.0.1:%d/" % a.puerto
    print("Panel local del simulador en", url, "(Ctrl+C para cerrar)")
    if not a.sin_navegador:
        threading.Timer(0.8, lambda: webbrowser.open(url)).start()
    try:
        srv.serve_forever()
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
