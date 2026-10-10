# -*- coding: utf-8 -*-
"""armar_previews_2.py — 09-10-2026. El operador eligio la maqueta 2 ("una sola columna a la derecha, sin el precio") y pidio "varias versiones
mas lindas o minimalistas y elijo", "e incluime un recuadro asi minimalista y desplegable facil que no moleste" (el cuadrito de la 10: leyenda de
colores + los 5 montos mas grandes con su cambio). Reusa la base de armar_previews.py (mismos datos reales, mismo grafico) y cambia SOLO las
variantes. El recuadro se abre y se cierra con un clic en su titulo (en la 4.1 seria un clic como el de la pestaña)."""
import json, os, re

AQUI = os.path.dirname(os.path.abspath(__file__))
fuente = open(os.path.join(AQUI, "armar_previews.py"), encoding="utf-8").read()
html = re.search(r'HTML = r"""(.*?)"""', fuente, re.S).group(1)
ini = html.index("// ---------- las variantes")
fin = html.index("const grid=")

import sys
VAR = sys.argv[1] if len(sys.argv) > 1 else "variantes_2.js"
SAL = sys.argv[2] if len(sys.argv) > 2 else "previews_2.html"
TIT = sys.argv[3] if len(sys.argv) > 3 else "La 2, en 9 versiones + el recuadro desplegable"
NUEVAS = open(os.path.join(AQUI, VAR), encoding="utf-8").read()
FINAL = r"""const grid=document.getElementById('grid');
for(const v of V){ const c=document.createElement('div'); c.className='card'; c.innerHTML=`<h2>${v.t}</h2><p>${v.d}</p>`+v.f(); grid.appendChild(c); }
document.querySelectorAll('g.caja').forEach(g=>{ const cab=g.querySelector('.cab'); cab.style.cursor='pointer';
  cab.addEventListener('click',()=>{ const ab=g.getAttribute('data-abierta')==='1'; g.setAttribute('data-abierta',ab?'0':'1');
    g.querySelector('.cuerpo').style.display=ab?'none':'inline'; g.querySelector('.flecha').textContent=ab?'▸':'▾'; }); });
</script></body></html>"""
html2 = html[:ini] + NUEVAS + FINAL
html2 = html2.replace("Etiquetas de la 4.1: 12 maquetas para elegir", TIT)
html2 = html2.replace("<title>Etiquetas 4.1: maquetas</title>", "<title>Etiquetas 4.1: la 2</title>")
datos = json.load(open(os.path.join(AQUI, "datos.json"), encoding="utf-8"))
open(os.path.join(AQUI, SAL), "w", encoding="utf-8").write(html2.replace("__DATOS__", json.dumps(datos, ensure_ascii=False)))
print("ok", os.path.join(AQUI, SAL))
