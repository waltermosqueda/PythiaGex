# -*- coding: utf-8 -*-
"""SUBIR EL VIVO DE ATAS A LA NUBE, CADA MINUTO.

Mientras ATAS esta abierto con Gamma Hoy, el indicador escribe en %APPDATA%\\ATAS:
  pythiagex-centinela-hoy-<INST>-TimeFrame-<M>.jsonl   una linea por vela: o/h/l/c/vol/delta/spot + niveles (niv) + order flow (of)
  pythiagex-gatillos-<INST>-TimeFrame-<M>.jsonl        cada disparo (rebote, modelo, tren)
  pythiagex-gammahoy.log                               AUDIT, PELOTITAS, base de la rueda, version
  PythiaGex\\viva\\viva-ES-<dia>.jsonl                   la cadena viva de Rithmic (ES, 0DTE con puntas reales)
Este script empaqueta lo ultimo en JSON chico y lo sube a la rama "cadenas" del repo por la API
de GitHub (gh api, con la sesion ya autenticada de gh):
  vivo-<INST>-<M>.json   las ultimas N velas con sus niveles y disparos del dia
  vivo-viva-ES.json      la ultima cadena viva de Rithmic (compacta)
  pc.json                latido: version del indicador, ultimo AUDIT por raiz, que archivos se subieron
La web (panel/) los lee y, si son frescos, los prefiere a la nube. Si la PC se apaga, dejan de
llegar y la web lo dice y sigue con la nube (CBOE + Yahoo, con retraso).

Uso: python herramientas/subir_vivo.py [--bucle] [--cada 60] [--velas 300]
"""
import base64
import glob
import io
import json
import os
import re
import subprocess
import sys
import time
from datetime import datetime, timedelta, timezone

REPO = "waltermosqueda/PythiaGex"
RAMA = "cadenas"
ATAS = os.path.join(os.environ.get("APPDATA", ""), "ATAS")
GRAFICOS = [("MNQ", "M1"), ("MNQ", "M2"), ("MNQ", "M5"), ("MES", "M1"), ("MES", "M2"), ("MES", "M5")]
_shas = {}


def log(m):
    print(datetime.now().strftime("%H:%M:%S") + "  " + m, flush=True)


def gh(args, entrada=None):
    r = subprocess.run(["gh", "api"] + args, input=entrada, capture_output=True, text=True, encoding="utf-8", errors="replace")
    return r.returncode, r.stdout.strip(), r.stderr.strip()


def subir(nombre, obj, mensaje):
    """PUT del archivo en la rama (crea o pisa). Guarda el sha para no pedirlo cada vez."""
    cuerpo = json.dumps(obj, ensure_ascii=False, separators=(",", ":"))
    sha = _shas.get(nombre)
    if sha is None:
        rc, out, err = gh(["repos/%s/contents/%s?ref=%s" % (REPO, nombre, RAMA), "--jq", ".sha"])
        sha = out if rc == 0 and out else None
    carga = {"message": mensaje, "branch": RAMA, "content": base64.b64encode(cuerpo.encode("utf-8")).decode("ascii")}
    if sha: carga["sha"] = sha
    rc, out, err = gh(["-X", "PUT", "repos/%s/contents/%s" % (REPO, nombre), "--input", "-", "--jq", ".content.sha"], json.dumps(carga))
    if rc != 0 and ("409" in err or "422" in err or "404" in err):
        # el sha cambio (otro commit, o la rama se aplano a las 22 UTC): pedirlo de nuevo y reintentar
        _shas.pop(nombre, None)
        rc2, out2, _ = gh(["repos/%s/contents/%s?ref=%s" % (REPO, nombre, RAMA), "--jq", ".sha"])
        if rc2 == 0 and out2: carga["sha"] = out2
        else: carga.pop("sha", None)
        rc, out, err = gh(["-X", "PUT", "repos/%s/contents/%s" % (REPO, nombre), "--input", "-", "--jq", ".content.sha"], json.dumps(carga))
    if rc == 0 and out:
        _shas[nombre] = out
        return len(cuerpo)
    log("fallo %s: %s" % (nombre, err[:200]))
    return 0


def leer_cola(ruta, n):
    """Las ultimas n lineas JSON de un archivo (lee solo la cola)."""
    try:
        with open(ruta, "rb") as f:
            f.seek(0, 2); tam = f.tell()
            f.seek(max(0, tam - n * 900))
            datos = f.read().decode("utf-8", errors="replace")
    except Exception:
        return []
    out = []
    for l in datos.splitlines()[1:] if tam > n * 900 else datos.splitlines():
        try: out.append(json.loads(l))
        except Exception: pass
    return out[-n:]


def paquete_velas(inst, marco, n):
    p = os.path.join(ATAS, "pythiagex-centinela-hoy-%s-TimeFrame-%s.jsonl" % (inst, marco))
    if not os.path.exists(p) or time.time() - os.path.getmtime(p) > 3 * 3600:
        return None
    velas = leer_cola(p, n)
    if not velas:
        return None
    claves_niv = ["zero_vol", "zero_oi", "mp_vol", "mn_vol", "mp_oi", "mn_oi", "dom0", "dom1", "mc1", "mc5", "mc10", "mc15", "mc30", "pico", "q_cuadrante"]
    cols = dict(t=[], o=[], h=[], l=[], c=[], vol=[], delta=[], spot=[], niv=[], of=[])
    for v in velas:
        try:
            cols["t"].append(int(datetime.strptime(v["t"][:19], "%Y-%m-%dT%H:%M:%S").replace(tzinfo=timezone.utc).timestamp()))
        except Exception:
            continue
        for k in ("o", "h", "l", "c", "vol", "delta", "spot"):
            cols[k].append(v.get(k))
        n_ = v.get("niv") or {}
        cols["niv"].append([n_.get(k) for k in claves_niv])
        o_ = v.get("of") or {}
        cols["of"].append([o_.get("dmax"), o_.get("dmin"), o_.get("big_n"), o_.get("big_max"), o_.get("big_buy"), o_.get("big_sell")])
    # los disparos del dia (rebote, modelo, tren) de ese grafico
    gat = []
    pg = os.path.join(ATAS, "pythiagex-gatillos-%s-TimeFrame-%s.jsonl" % (inst, marco))
    hoy = datetime.now(timezone.utc).strftime("%Y-%m-%d")
    for g in leer_cola(pg, 400):
        if str(g.get("t", "")).startswith(hoy):
            gat.append(g)
    return dict(inst=inst, marco=marco, generado=datetime.now(timezone.utc).isoformat(timespec="seconds"), archivo_mtime=datetime.fromtimestamp(os.path.getmtime(p), timezone.utc).isoformat(timespec="seconds"),
                claves_niv=claves_niv, claves_of=["dmax", "dmin", "big_n", "big_max", "big_buy", "big_sell"], velas=cols, gatillos=gat)


def paquete_viva(raiz="ES"):
    fs = sorted(glob.glob(os.path.join(ATAS, "PythiaGex", "viva", "viva-%s-*.jsonl" % raiz)))
    if not fs or time.time() - os.path.getmtime(fs[-1]) > 20 * 60:
        return None
    ls = leer_cola(fs[-1], 1)
    if not ls:
        return None
    l = ls[0]
    return dict(raiz=raiz, generado=datetime.now(timezone.utc).isoformat(timespec="seconds"), ts=l.get("ts"), futuro=l.get("futuro"), grandes=l.get("grandes"),
                campos=l.get("campos"), filas=l.get("filas"))


def latido(subidos):
    lg = os.path.join(ATAS, "pythiagex-gammahoy.log")
    audits = {}; version = None; pelotitas = None; base_rueda = None
    for l in leer_cola_texto(lg, 3000):
        if "AUDIT fut=" in l:
            # el log lo comparten todos los graficos: la raiz se deduce del tamaño del futuro (NQ ~29000, ES ~7600)
            m = re.search(r"fut=([0-9.]+)", l)
            fut = float(m.group(1)) if m else 0.0
            audits["NQ" if fut > 15000 else "ES"] = l.strip()
            audits["ultimo"] = l.strip()
        if "arranca" in l and "Gamma Hoy" in l:
            m = re.search(r"Gamma Hoy ([0-9.a-z]+) arranca", l)
            if m: version = m.group(1)
        if "PELOTITAS" in l: pelotitas = l.strip()
        if "base de la rueda" in l.lower(): base_rueda = l.strip()
    return dict(generado=datetime.now(timezone.utc).isoformat(timespec="seconds"), pc=os.environ.get("COMPUTERNAME", "?"), version=version, audit=audits.get("ultimo"),
                audit_NQ=audits.get("NQ"), audit_ES=audits.get("ES"), pelotitas=pelotitas, base_rueda=base_rueda, subidos=subidos)


def leer_cola_texto(ruta, n):
    try:
        with open(ruta, "rb") as f:
            f.seek(0, 2); tam = f.tell(); f.seek(max(0, tam - n * 200))
            return f.read().decode("utf-8", errors="replace").splitlines()
    except Exception:
        return []


def una_vuelta(n_velas):
    subidos = {}
    ahora = datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M UTC")
    for inst, marco in GRAFICOS:
        paq = paquete_velas(inst, marco, n_velas)
        if paq is None:
            continue
        nombre = "vivo-%s-%s.json" % (inst, marco)
        tam = subir(nombre, paq, "vivo %s %s %s" % (inst, marco, ahora))
        if tam: subidos[nombre] = dict(bytes=tam, velas=len(paq["velas"]["t"]), ultima=paq["velas"]["t"][-1], gatillos=len(paq["gatillos"]))
    for raiz in ("ES", "NQ"):
        pv = paquete_viva(raiz)
        if pv is not None:
            nombre = "vivo-viva-%s.json" % raiz
            tam = subir(nombre, pv, "viva %s %s" % (raiz, ahora))
            if tam: subidos[nombre] = dict(bytes=tam, filas=len(pv.get("filas") or []), ts=pv.get("ts"))
    subir("pc.json", latido(subidos), "latido %s" % ahora)
    log("subidos: " + ", ".join("%s (%d KB)" % (k, v["bytes"] // 1024) for k, v in subidos.items()) if subidos else "nada fresco para subir (ATAS cerrado?); latido enviado")


def main():
    a = sys.argv[1:]
    cada = int(a[a.index("--cada") + 1]) if "--cada" in a else 60
    n = int(a[a.index("--velas") + 1]) if "--velas" in a else 300
    if "--bucle" not in a:
        una_vuelta(n); return
    log("subir_vivo en bucle cada %d s (%d velas por grafico)" % (cada, n))
    while True:
        t0 = time.time()
        try:
            una_vuelta(n)
        except Exception as e:
            log("error: %s" % e)
        time.sleep(max(5, cada - (time.time() - t0)))


if __name__ == "__main__":
    main()
