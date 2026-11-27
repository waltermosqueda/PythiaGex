# -*- coding: utf-8 -*-
"""CINTA DEL DIA + JUECES EN VIVO (27-11-2026). Lo que hay que correr cada dia habil despues de las 17:00 NY (19:00 AR) desde el 30-11:
1. Pide a la sonda del Superindicador la sesion CME entera del dia (18:00 NY de la vispera a 17:00 NY), escribiendo
   %APPDATA%\\ATAS\\PythiaGex\\flujo\\sonda-<instrumento>.txt  (ATAS tiene que estar abierto con el Superindicador en ese grafico;
   por defecto el de pruebas ESZ6). Espera el .listo.
2. Deja la cinta tambien con el nombre que exige el juez de la R21 (cinta-ES-<desde>-<hasta>.csv): r19_comun.cintas_crudas('ES') solo
   acepta 'ES' exacto y la sonda del grafico de pruebas la llama 'ESZ6'. Es un ENLACE DURO (mismo archivo, cero disco extra), no una copia.
3. Corre los dos jueces congelados: ronda21_regimen/r21_11_vivo.py (COMB-T7+V4) y ronda23_diseno/r23_vivo.py (A3). No toca sus archivos:
   cada juez verifica sus huellas y anota en su propia bitacora. La salida queda en laboratorio/bitacora_vivo/<dia>.txt.
No toca la ventana de ATAS ni manda teclas: solo archivos.
Uso: python -I herramientas/cinta_y_jueces.py [AAAA-MM-DD] [--instrumento ESZ6] [--sin-pedir] [--sin-jueces] [--verificar] [--esperar 2400]
     --verificar: corre los jueces en modo --verificar <dia> (no escriben nada); sirve para probar el circuito con un dia conocido.
     --hasta-ahora: solo baja la cinta PARCIAL de hoy hasta este minuto (controles dentro de la rueda, p. ej. La Ola); no enlaza ni juzga.
Probado 27-11 09:39 con 2026-11-24 (--sin-pedir --verificar): los dos jueces COINCIDEN con su diseno.
OJO roll de diciembre: desde el 07-12 la R21 pide el contrato de cada cinta en ronda21_regimen/vivo_contratos.json; despues del vencimiento
de Z6 (18-12) el grafico de pruebas pasa a ESH7: pasar --instrumento ESH7. Esta herramienta anota el contrato de cada cinta en el log."""
import sys, os, time, glob, argparse, subprocess
from datetime import datetime, timedelta
from zoneinfo import ZoneInfo
sys.stdout.reconfigure(encoding="utf-8")
NY = ZoneInfo("America/New_York"); UTC = ZoneInfo("UTC")
RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LAB = os.path.join(RAIZ, "laboratorio")
FLUJO = os.path.join(os.environ["APPDATA"], "ATAS", "PythiaGex", "flujo")
JUECES = (("R21 COMB-T7+V4", os.path.join(LAB, "ronda21_regimen"), "r21_11_vivo.py"),
          ("R23 A3", os.path.join(LAB, "ronda23_diseno"), "r23_vivo.py"))

def sesion_utc(dia):
    d = datetime.strptime(dia, "%Y-%m-%d")
    desde = datetime(d.year, d.month, d.day, 18, 0, tzinfo=NY) - timedelta(days=1)
    hasta = datetime(d.year, d.month, d.day, 17, 0, tzinfo=NY)
    return desde.astimezone(UTC), hasta.astimezone(UTC)

def nombre(inst, desde, hasta):
    return os.path.join(FLUJO, "cinta-%s-%s-%s.csv" % (inst, desde.strftime("%Y%m%d%H%M"), hasta.strftime("%Y%m%d%H%M")))

def pedir(inst, desde, hasta, esperar, log):
    destino = nombre(inst, desde, hasta)
    if os.path.exists(destino + ".listo"):
        log("ya estaba la cinta %s (%s ordenes)" % (os.path.basename(destino), open(destino + ".listo").read().strip())); return destino
    pedido = os.path.join(FLUJO, "sonda-%s.txt" % inst)
    if os.path.exists(pedido): sys.exit("ya hay un pedido sin tomar (%s): la sonda no esta corriendo; no se pisa" % pedido)
    linea = "cinta;%s;%s;1;1380" % (desde.strftime("%Y-%m-%dT%H:%M:%S"), hasta.strftime("%Y-%m-%dT%H:%M:%S"))
    open(pedido, "w").write(linea); log("pedido a la sonda: %s -> %s" % (linea, os.path.basename(pedido)))
    t0 = time.time()
    while os.path.exists(pedido):
        if time.time() - t0 > 20:
            os.remove(pedido)
            sys.exit("la sonda no tomo el pedido en 20 s: ATAS tiene que estar abierto con el Superindicador 1.9 en el grafico %s (pedido borrado)" % inst)
        time.sleep(1)
    log("la sonda tomo el pedido en %.0f s; esperando el .listo (hasta %d s)" % (time.time() - t0, esperar))
    while not os.path.exists(destino + ".listo"):
        if time.time() - t0 > esperar: sys.exit("no llego el .listo de %s en %d s" % (os.path.basename(destino), esperar))
        time.sleep(5)
    log("cinta lista: %s, %s ordenes, %.1f MB, en %.0f s" % (os.path.basename(destino), open(destino + ".listo").read().strip(),
                                                         os.path.getsize(destino) / 1e6, time.time() - t0))
    return destino

def enlazar(origen, desde, hasta, log):
    es = nombre("ES", desde, hasta)
    if os.path.abspath(es) == os.path.abspath(origen): return es
    for a, b in ((origen, es), (origen + ".listo", es + ".listo")):
        if os.path.exists(b):
            if os.path.getsize(b) != os.path.getsize(a): sys.exit("ya existe %s y no es igual a %s: revisar a mano" % (b, a))
            continue
        os.link(a, b)
    log("nombre para el juez de la R21: %s (enlace duro de %s)" % (os.path.basename(es), os.path.basename(origen)))
    return es

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("dia", nargs="?", default=datetime.now(NY).strftime("%Y-%m-%d"))
    ap.add_argument("--instrumento", default="ESZ6"); ap.add_argument("--esperar", type=int, default=2400)
    ap.add_argument("--sin-pedir", action="store_true"); ap.add_argument("--sin-jueces", action="store_true"); ap.add_argument("--verificar", action="store_true")
    ap.add_argument("--hasta-ahora", action="store_true", help="cinta PARCIAL hasta este minuto (para controles en la rueda): no se enlaza ni se juzga")
    a = ap.parse_args()
    if a.hasta_ahora:
        desde, _ = sesion_utc(a.dia); hasta = datetime.now(UTC).replace(second=0, microsecond=0)
        print("cinta parcial %s -> %s UTC" % (desde, hasta), flush=True)
        print("lista:", pedir(a.instrumento, desde, hasta, a.esperar, lambda m: print(datetime.now().strftime("%H:%M:%S"), m, flush=True)))
        return
    os.makedirs(os.path.join(LAB, "bitacora_vivo"), exist_ok=True)
    salida = os.path.join(LAB, "bitacora_vivo", "%s%s.txt" % (a.dia, "-verificar" if a.verificar else ""))
    fh = open(salida, "a", encoding="utf-8")
    def log(m):
        s = "%s  %s" % (datetime.now().strftime("%Y-%m-%d %H:%M:%S"), m); print(s, flush=True); fh.write(s + "\n"); fh.flush()
    desde, hasta = sesion_utc(a.dia)
    log("jornada %s: sesion CME %s a %s UTC | instrumento %s (contrato de la cinta: %s)" % (a.dia, desde, hasta, a.instrumento, a.instrumento[2:] or "?"))
    if datetime.now(UTC) < hasta and not a.verificar and not a.sin_pedir:
        log("OJO: todavia no son las 17:00 NY de esa jornada; la cinta va a quedar cortada en este momento")
    if a.sin_pedir:
        cand = sorted(glob.glob(nombre(a.instrumento, desde, hasta))) or sorted(glob.glob(nombre("ES", desde, hasta)))
        if not cand or not os.path.exists(cand[0] + ".listo"): sys.exit("no hay cinta con .listo para %s" % a.dia)
        origen = cand[0]; log("sin pedir: uso %s" % os.path.basename(origen))
    else:
        origen = pedir(a.instrumento, desde, hasta, a.esperar, log)
    enlazar(origen, desde, hasta, log)
    if a.sin_jueces: return
    for nom, carpeta, script in JUECES:
        args = [sys.executable, "-I", script] + (["--verificar", a.dia] if a.verificar else [])
        log("== juez %s: %s" % (nom, " ".join(args[1:])))
        r = subprocess.run(args, cwd=carpeta, capture_output=True, text=True, encoding="utf-8", errors="replace")
        for l in (r.stdout + r.stderr).strip().splitlines(): log("   " + l)
        log("   (salida %d)" % r.returncode)
    log("listo; log en %s" % salida)

if __name__ == "__main__":
    main()
