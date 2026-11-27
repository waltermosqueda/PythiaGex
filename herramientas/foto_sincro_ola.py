# -*- coding: utf-8 -*-
"""FOTO SINCRONIZADA de La Ola (27-11-2026): espera la proxima linea del jsonl de auditoria y en ese instante captura la ventana de ATAS
con PrintWindow (pasivo: sin foco, sin teclado). OJO: con la pantalla APAGADA (ahorro de energia) PrintWindow y las capturas devuelven
el ultimo cuadro viejo: despertarla antes. Uso: python -I herramientas/foto_sincro_ola.py <salida_sin_extension> [jsonl]"""
import sys, os, time, json
sys.stdout.reconfigure(encoding="utf-8")
sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "laboratorio", "opciones_cero", "C_xray"))
import xr
from PIL import Image
j = sys.argv[2] if len(sys.argv) > 2 else sorted(__import__("glob").glob(os.path.join(os.environ["APPDATA"], "ATAS", "PythiaGex", "pythiaola", "auditoria-*.jsonl")), key=os.path.getmtime)[-1]
out = sys.argv[1]
n0 = os.path.getsize(j); t0 = time.time()
while os.path.getsize(j) == n0 and time.time() - t0 < 90: time.sleep(0.05)
h = xr.ventanas(r"^ATAS - ")[0]
a = xr.foto(h); tcap = time.strftime("%H:%M:%S")
Image.fromarray(a).save(out + ".png")
time.sleep(0.5)
ult = open(j, encoding="utf-8").read().strip().split("\n")[-1]
open(out + ".json", "w", encoding="utf-8").write(ult)
p = json.loads(ult)["pantalla"]
print("captura", tcap, "| jsonl", json.loads(ult)["utc"], "| textos:", " / ".join(p["panel"]["textos"]), "| pie:", p["panel"]["pie"])
for k in ("ola", "marea"):
    r = p["rayas"].get(k); print("  raya", k, r)
print("  panel rect", p["panel"]["rect"], "momento", p["momento"], "visibles", p["visibles"])
