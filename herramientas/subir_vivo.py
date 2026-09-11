# -*- coding: utf-8 -*-
"""SUBIR EL VIVO DE ATAS A LA NUBE, CADA 20 SEGUNDOS, EN UN SOLO ARCHIVO.

Mientras ATAS esta abierto con Gamma Hoy, el indicador escribe en %APPDATA%\\ATAS:
  pythiagex-centinela-hoy-<INST>-TimeFrame-<M>.jsonl   una linea por vela: o/h/l/c/vol/delta/spot + niveles (niv) + order flow (of)
  pythiagex-gatillos-<INST>-TimeFrame-<M>.jsonl        cada disparo (rebote, modelo, tren)
  pythiagex-gammahoy.log                               AUDIT, PELOTITAS, base de la rueda, version
  PythiaGex\\viva\\viva-ES-<dia>.jsonl                   la cadena viva de Rithmic (ES, 0DTE con puntas reales)
Este script empaqueta lo ultimo en UN JSON (vivo.json: latido + todos los graficos + la cadena viva)
y lo sube a la rama "cadenas" del repo por la API de GitHub (gh api, con la sesion de gh ya
autenticada). Un archivo = un commit por vuelta; la rama se aplana sola a las 22 UTC.
La web (panel/) lo lee y, si es fresco, lo prefiere a la nube. Si la PC se apaga, deja de llegar y la
web lo dice y sigue con la nube (CBOE + Yahoo, con retraso).

La web lee cada archivo por COMMIT (raw.githubusercontent.com/<repo>/<sha>/vivo.json, inmutable, nunca
cacheado viejo) preguntando el sha de la rama a la API cada 75 s. raw por rama cachea 5 min y jsDelivr se
sirve de ese cache (el purge traia la copia vieja): no sirven para esto.

Sin ventana: bajo pythonw cada llamada a gh abria una consola negra (10-09 19:30, el operador no
podia usar la PC). CREATE_NO_WINDOW la esconde. Cada llamada tiene tope de 60 s: una que se cuelga
no frena el bucle.

Uso: python herramientas/subir_vivo.py [--bucle] [--cada 20] [--velas 300]
"""
import base64
import glob
import json
import os
import re
import subprocess
import sys
import time
import urllib.request
from datetime import datetime, timezone

REPO = "waltermosqueda/PythiaGex"
RAMA = "cadenas"
ATAS = os.path.join(os.environ.get("APPDATA", ""), "ATAS")
GRAFICOS = [("MNQ", "M1"), ("MNQ", "M2"), ("MNQ", "M5"), ("MES", "M1"), ("MES", "M2"), ("MES", "M5")]
CLAVES_NIV = ["zero_vol", "zero_oi", "mp_vol", "mn_vol", "mp_oi", "mn_oi", "dom0", "dom1", "mc1", "mc5", "mc10", "mc15", "mc30", "pico", "q_cuadrante"]
CLAVES_OF = ["dmax", "dmin", "big_n", "big_max", "big_buy", "big_sell"]
_SIN_VENTANA = getattr(subprocess, "CREATE_NO_WINDOW", 0x08000000) if os.name == "nt" else 0
_sha = {}


LOG_ARCHIVO = os.path.join(os.environ.get("APPDATA", "."), "ATAS", "pythiagex-subir-vivo.log")


def log(m):
    # bajo pythonw la salida estandar no va a ningun lado: el 11-09 un subidor estuvo 6 horas sin subir
    # y no habia forma de saber por que. Queda tambien en un archivo (se poda a 2000 lineas).
    linea = datetime.now().strftime("%Y-%m-%d %H:%M:%S") + "  " + m
    print(linea, flush=True)
    try:
        with open(LOG_ARCHIVO, "a", encoding="utf-8") as f:
            f.write(linea + "\n")
        if os.path.getsize(LOG_ARCHIVO) > 400000:
            with open(LOG_ARCHIVO, encoding="utf-8", errors="replace") as f: lineas = f.readlines()[-2000:]
            with open(LOG_ARCHIVO, "w", encoding="utf-8") as f: f.writelines(lineas)
    except Exception:
        pass


def gh(args, entrada=None, tope=60):
    si = None
    if os.name == "nt":
        si = subprocess.STARTUPINFO(); si.dwFlags |= subprocess.STARTF_USESHOWWINDOW; si.wShowWindow = 0
    try:
        r = subprocess.run(["gh", "api"] + args, input=entrada, capture_output=True, text=True, encoding="utf-8", errors="replace",
                           creationflags=_SIN_VENTANA, startupinfo=si, timeout=tope)
    except subprocess.TimeoutExpired:
        return 124, "", "tope de %d s" % tope
    except Exception as e:
        return 1, "", str(e)
    return r.returncode, r.stdout.strip(), r.stderr.strip()


def subir(nombre, obj, mensaje):
    """PUT del archivo en la rama (crea o pisa). Guarda el sha para no pedirlo cada vez; si cambio
    afuera (otro commit, o la rama se aplano a las 22 UTC), lo pide de nuevo y reintenta una vez."""
    cuerpo = json.dumps(obj, ensure_ascii=False, separators=(",", ":"))
    sha = _sha.get(nombre)
    if sha is None:
        rc, out, err = gh(["repos/%s/contents/%s?ref=%s" % (REPO, nombre, RAMA), "--jq", ".sha"], tope=40)
        sha = out if rc == 0 and out else None
    carga = {"message": mensaje, "branch": RAMA, "content": base64.b64encode(cuerpo.encode("utf-8")).decode("ascii")}
    if sha: carga["sha"] = sha
    rc, out, err = gh(["-X", "PUT", "repos/%s/contents/%s" % (REPO, nombre), "--input", "-", "--jq", ".content.sha"], json.dumps(carga))
    if rc != 0 and ("409" in err or "422" in err or "404" in err):
        _sha.pop(nombre, None)
        rc2, out2, _ = gh(["repos/%s/contents/%s?ref=%s" % (REPO, nombre, RAMA), "--jq", ".sha"], tope=40)
        if rc2 == 0 and out2: carga["sha"] = out2
        else: carga.pop("sha", None)
        rc, out, err = gh(["-X", "PUT", "repos/%s/contents/%s" % (REPO, nombre), "--input", "-", "--jq", ".content.sha"], json.dumps(carga))
    if rc == 0 and out:
        _sha[nombre] = out
        return len(cuerpo)
    log("fallo %s: %s" % (nombre, err[:200]))
    return 0


def purgar(nombre):
    """raw.githubusercontent.com cachea 5 min aunque se cambie la query (medido 10-09: devolvia el
    vivo de 5 min antes). jsDelivr sirve la rama y se puede purgar al instante: la web lee de ahi."""
    try:
        with urllib.request.urlopen("https://purge.jsdelivr.net/gh/%s@%s/%s" % (REPO, RAMA, nombre), timeout=10) as r:
            r.read(200)
    except Exception as e:
        log("purge jsDelivr fallo: %s" % e)


def leer_cola(ruta, n, ancho=900):
    """Las ultimas n lineas JSON de un archivo (lee solo la cola)."""
    try:
        with open(ruta, "rb") as f:
            f.seek(0, 2); tam = f.tell()
            f.seek(max(0, tam - n * ancho))
            datos = f.read().decode("utf-8", errors="replace")
    except Exception:
        return []
    lineas = datos.splitlines()
    if tam > n * ancho: lineas = lineas[1:]
    out = []
    for l in lineas:
        try: out.append(json.loads(l))
        except Exception: pass
    return out[-n:]


def leer_cola_texto(ruta, n):
    try:
        with open(ruta, "rb") as f:
            f.seek(0, 2); tam = f.tell(); f.seek(max(0, tam - n * 200))
            return f.read().decode("utf-8", errors="replace").splitlines()
    except Exception:
        return []


def paquete_velas(inst, marco, n):
    # El grafico con libro de Rithmic escribe "hoyrithmic-"; el de CBOE, "hoy-". Va el mas fresco y,
    # si los dos estan al dia (3 min), el de Rithmic: es el que mira el operador en ATAS (2026-09-11).
    cands = []
    for libro, pref in (("rithmic", "hoyrithmic"), ("cboe", "hoy")):
        q = os.path.join(ATAS, "pythiagex-centinela-%s-%s-TimeFrame-%s.jsonl" % (pref, inst, marco))
        if os.path.exists(q):
            cands.append((os.path.getmtime(q), libro, q))
    if not cands:
        return None
    cands.sort(reverse=True)
    mt, libro, p = cands[0]
    for mt2, libro2, q2 in cands[1:]:
        if libro2 == "rithmic" and mt - mt2 <= 180:
            mt, libro, p = mt2, libro2, q2
    if time.time() - mt > 3 * 3600:
        return None
    velas = leer_cola(p, n)
    if not velas:
        return None
    cols = dict(t=[], o=[], h=[], l=[], c=[], vol=[], delta=[], spot=[], niv=[], of=[])
    for v in velas:
        try:
            cols["t"].append(int(datetime.strptime(v["t"][:19], "%Y-%m-%dT%H:%M:%S").replace(tzinfo=timezone.utc).timestamp()))
        except Exception:
            continue
        for k in ("o", "h", "l", "c", "vol", "delta", "spot"):
            cols[k].append(v.get(k))
        n_ = v.get("niv") or {}
        cols["niv"].append([n_.get(k) for k in CLAVES_NIV])
        o_ = v.get("of") or {}
        cols["of"].append([o_.get(k) for k in CLAVES_OF])
    gat = []
    pg = os.path.join(ATAS, "pythiagex-gatillos-%s-TimeFrame-%s.jsonl" % (inst, marco))
    hoy = datetime.now(timezone.utc).strftime("%Y-%m-%d")
    for g in leer_cola(pg, 400, 300):
        if str(g.get("t", "")).startswith(hoy):
            gat.append(g)
    return dict(inst=inst, marco=marco, libro=libro, archivo_mtime=datetime.fromtimestamp(os.path.getmtime(p), timezone.utc).isoformat(timespec="seconds"), velas=cols, gatillos=gat)


def paquete_viva(raiz="ES"):
    fs = sorted(glob.glob(os.path.join(ATAS, "PythiaGex", "viva", "viva-%s-*.jsonl" % raiz)))
    if not fs or time.time() - os.path.getmtime(fs[-1]) > 20 * 60:
        return None
    ls = leer_cola(fs[-1], 1, 120000)
    if not ls:
        return None
    l = ls[0]
    return dict(raiz=raiz, ts=l.get("ts"), futuro=l.get("futuro"), grandes=l.get("grandes"), campos=l.get("campos"), filas=l.get("filas"))


def latido():
    lg = os.path.join(ATAS, "pythiagex-gammahoy.log")
    audits = {}; version = None; pelotitas = None; base_rueda = None; viva_estado = None
    for l in leer_cola_texto(lg, 3000):
        if "AUDIT fut=" in l:
            m = re.search(r"fut=([0-9.]+)", l)
            fut = float(m.group(1)) if m else 0.0
            audits["NQ" if fut > 15000 else "ES"] = l.strip()   # el log lo comparten los graficos: la raiz sale del tamaño del futuro
        if "arranca" in l and "Gamma Hoy" in l:
            m = re.search(r"Gamma Hoy ([0-9.a-z]+) arranca", l)
            if m: version = m.group(1)
        if "PELOTITAS" in l: pelotitas = l.strip()
        if "base de la rueda" in l.lower(): base_rueda = l.strip()
        if "[cadena viva]" in l: viva_estado = l.strip()
    if version is None:   # la linea "arranca" queda atras en el log despues de un rato: buscar mas lejos
        for l in leer_cola_texto(lg, 120000):
            if "arranca" in l and "Gamma Hoy" in l:
                m = re.search(r"Gamma Hoy ([0-9.a-z]+) arranca", l)
                if m: version = m.group(1)
    return dict(pc=os.environ.get("COMPUTERNAME", "?"), version=version, audit_NQ=audits.get("NQ"), audit_ES=audits.get("ES"),
                pelotitas=pelotitas, base_rueda=base_rueda, viva_estado=viva_estado)


def una_vuelta(n_velas):
    ahora = datetime.now(timezone.utc)
    graficos = {}
    for inst, marco in GRAFICOS:
        paq = paquete_velas(inst, marco, n_velas)
        if paq is not None:
            graficos[inst + "-" + marco] = paq
    viva = {}
    for raiz in ("ES", "NQ"):
        pv = paquete_viva(raiz)
        if pv is not None:
            viva[raiz] = pv
    paquete = dict(generado=ahora.isoformat(timespec="seconds"), claves_niv=CLAVES_NIV, claves_of=CLAVES_OF, latido=latido(),
                   graficos=graficos, viva=viva, resumen={k: dict(velas=len(v["velas"]["t"]), ultima=v["velas"]["t"][-1], gatillos=len(v["gatillos"])) for k, v in graficos.items()})
    tam = subir("vivo.json", paquete, "vivo %s" % ahora.strftime("%Y-%m-%d %H:%M:%S UTC"))
    log(("subido vivo.json %d KB: " % (tam // 1024) if tam else "NO subio: ") + (", ".join(graficos) if graficos else "sin graficos frescos (ATAS cerrado?)") + (" + viva " + ",".join(viva) if viva else ""))
    # hay algo fresco si ATAS escribio en la ultima hora: entonces se insiste cada 'cada' segundos aunque una subida falle
    return bool(graficos)


def main():
    a = sys.argv[1:]
    cada = int(a[a.index("--cada") + 1]) if "--cada" in a else 20
    n = int(a[a.index("--velas") + 1]) if "--velas" in a else 300
    if "--bucle" not in a:
        una_vuelta(n); return
    log("subir_vivo en bucle cada %d s (%d velas por grafico)" % (cada, n))
    while True:
        t0 = time.time()
        try:
            fresco = una_vuelta(n)
        except Exception as e:
            log("error: %s" % e); fresco = False
        # sin nada fresco (ATAS cerrado) el latido va cada 5 minutos; si hay velas frescas se insiste siempre
        espera = cada if fresco else 300
        time.sleep(max(3, espera - (time.time() - t0)))


if __name__ == "__main__":
    main()
