# -*- coding: utf-8 -*-
"""CAMARA PASIVA DE ATAS (27-11-2026). Una foto de la ventana de ATAS cada --cada segundos hasta --hasta (hora NY), con lo que cada
grafico de La Ola anoto que dibujaba en ese momento (ultima linea de su auditoria-*.jsonl). PASIVA: PrintWindow sobre la ventana
(no la trae al frente, no mueve el mouse, no manda teclas): se puede usar con el operador operando.
Para que: ver la EVOLUCION en pantalla (no solo los numeros de los registros) y despues del cierre comparar cada foto con la cinta
(foto_sincro / instante_dos_paneles). Ademas avisa si la zona de los graficos queda identica 3 fotos seguidas (= ATAS dejo de pintar:
el 27-11 el grafico de pruebas quedo congelado 4 h mientras La Ola seguia calculando).
Las fotos muestran la cuenta: guardarlas en el scratchpad, NUNCA en el repo sin tapar.
Uso: python -I herramientas/camara_pasiva_atas.py --dir <carpeta> [--cada 30] [--hasta 13:15]"""
import sys, os, time, json, glob, argparse
sys.stdout.reconfigure(encoding="utf-8")
from datetime import datetime
from zoneinfo import ZoneInfo
import numpy as np
from PIL import Image
RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(RAIZ, "laboratorio", "opciones_cero", "C_xray"))
import xr
ap = argparse.ArgumentParser(); ap.add_argument("--dir", required=True); ap.add_argument("--cada", type=int, default=30)
ap.add_argument("--hasta", default="13:15")
a = ap.parse_args()
NY = ZoneInfo("America/New_York")
os.makedirs(a.dir, exist_ok=True)
CARP = os.path.join(os.environ["APPDATA"], "ATAS", "PythiaGex", "pythiaola")
def ultima(f):
    with open(f, "rb") as fh:
        fh.seek(0, 2); n = fh.tell(); fh.seek(max(0, n - 30000))
        return json.loads(fh.read().decode("utf-8", "replace").strip().splitlines()[-1])
hh, mm = (int(x) for x in a.hasta.split(":"))
idx = open(os.path.join(a.dir, "indice.jsonl"), "a", encoding="utf-8")
prev = None; quietas = 0
print("camara arranca: cada %d s hasta %02d:%02d NY -> %s" % (a.cada, hh, mm, a.dir), flush=True)
while True:
    ny = datetime.now(NY)
    if (ny.hour, ny.minute) >= (hh, mm): break
    t = datetime.now().strftime("%H%M%S")
    try:
        arr = xr.foto(xr.ventanas(r"^ATAS - ")[0])
        im = Image.fromarray(np.ascontiguousarray(arr[:, :, :3][:, :, ::-1]))
        im.save(os.path.join(a.dir, t + ".jpg"), quality=80)
        zona = np.asarray(im.convert("L"))[190:720, 30:1530]
        igual = prev is not None and prev.shape == zona.shape and np.array_equal(prev, zona)
        quietas = quietas + 1 if igual else 0; prev = zona
        reg = {"hora": t, "quietas": quietas}
        for f in glob.glob(os.path.join(CARP, "auditoria-*-M1-%s.jsonl" % datetime.now().strftime("%Y%m%d"))):
            e = ultima(f); p = e["pantalla"]
            reg[e["inst"]] = {"utc": e["utc"], "textos": p["panel"]["textos"], "n": (p.get("ola") or {}).get("n")}
        idx.write(json.dumps(reg, ensure_ascii=False) + "\n"); idx.flush()
        if quietas == 3: print(t, "PANTALLA QUIETA: 3 fotos seguidas identicas (ATAS dejo de pintar?)", flush=True)
    except Exception as ex:
        print(t, "error:", ex, flush=True)
    time.sleep(a.cada)
print("fin del horario", flush=True)
