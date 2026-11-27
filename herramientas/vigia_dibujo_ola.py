# -*- coding: utf-8 -*-
"""VIGIA DEL DIBUJO del grafico de La Ola (27-11-2026). PASIVO: no toca ATAS, no manda teclas ni clics.
Por que existe: el 27-11 el grafico de pruebas ESZ6 1m dejo de dibujarse a las 07:29:41 AR y quedo congelado 4 h 20 min mientras La Ola
seguia calculando (su csv se escribia cada minuto) y el grafico de la derecha seguia vivo. No lo trabo ningun indicador: con cambiar de
pestana por UI Automation (Orders -> ESZ6 1m Chart) volvio a dibujar. Este vigia sirve para saber CUANDO vuelve a pasar y en que
condiciones (inactividad del usuario, ventana al frente).
Como mide: el auditoria-*.jsonl de La Ola se escribe DESDE EL DIBUJO (una linea por minuto) y el ola-*.csv desde el latido (cada minuto,
dibuje o no). Si el csv esta al dia (< 120 s) y el jsonl lleva mas de 150 s sin crecer, el grafico dejo de dibujarse: lo anota y sale con
codigo 3 (asi el que lo lanzo se entera). Sale solo (codigo 0) a la hora --hasta (NY), porque sin operaciones ATAS no redibuja y daria
falsas alarmas.
Uso: python -I herramientas/vigia_dibujo_ola.py [--hasta 13:15] [--log ruta]"""
import sys, os, time, glob, ctypes, argparse
from ctypes import wintypes
from datetime import datetime
from zoneinfo import ZoneInfo
sys.stdout.reconfigure(encoding="utf-8")
ap = argparse.ArgumentParser(); ap.add_argument("--hasta", default="13:15"); ap.add_argument("--log", default=None)
a = ap.parse_args()
NY = ZoneInfo("America/New_York")
CARP = os.path.join(os.environ["APPDATA"], "ATAS", "PythiaGex", "pythiaola")
LOG = a.log or os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "laboratorio", "bitacora_vivo",
                            "vigia_dibujo_%s.log" % datetime.now().strftime("%Y-%m-%d"))
os.makedirs(os.path.dirname(LOG), exist_ok=True)
class LII(ctypes.Structure): _fields_ = [("cbSize", wintypes.UINT), ("dwTime", wintypes.DWORD)]
u32 = ctypes.windll.user32; k32 = ctypes.windll.kernel32
k32.GetTickCount.restype = wintypes.DWORD
def quieto():
    l = LII(); l.cbSize = ctypes.sizeof(LII); u32.GetLastInputInfo(ctypes.byref(l))
    return ((k32.GetTickCount() - l.dwTime) & 0xFFFFFFFF) / 1000.0
def al_frente():
    h = u32.GetForegroundWindow(); b = ctypes.create_unicode_buffer(300); u32.GetWindowTextW(h, b, 300); return b.value
def anotar(m):
    linea = "%s  %s" % (datetime.now().strftime("%Y-%m-%d %H:%M:%S"), m)
    print(linea, flush=True)
    with open(LOG, "a", encoding="utf-8") as f: f.write(linea + "\n")
def ultimo(patron):
    fs = glob.glob(os.path.join(CARP, patron)); return max(fs, key=os.path.getmtime) if fs else None
hh, mm = (int(x) for x in a.hasta.split(":"))
anotar("vigia arranca (hasta %02d:%02d NY); jsonl %s; csv %s" % (hh, mm, ultimo("auditoria-*.jsonl"), ultimo("ola-*.csv")))
tam = None; cambio = time.time()   # el jsonl se mide por su TAMANO (cada linea nueva lo agranda), no por la fecha del archivo
while True:
    ny = datetime.now(NY)
    if (ny.hour, ny.minute) >= (hh, mm): anotar("fin del horario: sale sin novedad"); sys.exit(0)
    j, c = ultimo("auditoria-*.jsonl"), ultimo("ola-*.csv")
    if j and c:
        ahora = time.time(); t = os.path.getsize(j)
        if t != tam: tam, cambio = t, ahora
        ej = ahora - cambio; ec = ahora - os.path.getmtime(c)
        if ec < 120 and ej > 150:
            anotar("GRAFICO SIN DIBUJAR: el jsonl no crece hace %.0f s (desde %s) y el csv se escribio hace %.0f s | usuario quieto %.0f s | al frente '%s'"
                   % (ej, time.strftime("%H:%M:%S", time.localtime(cambio)), ec, quieto(), al_frente()))
            sys.exit(3)
    time.sleep(20)
