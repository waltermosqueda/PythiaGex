# -*- coding: utf-8 -*-
"""TRES MANERAS DE DIBUJAR GAMMA HOY MAS PARECIDAS A GAMMALITO, CON DATOS REALES.

Genera una pagina con tres lienzos, uno por opcion, sobre el mismo tramo real
(el 3 de septiembre rebobinado, velas de 1 minuto), para que el operador elija
antes de tocar el indicador. No usa Write/Edit sobre .html (abre el panel del
navegador): se escribe desde aca.

Uso: python herramientas/mockups_gammalito.py [dia] [salida.html]
"""
import io
import json
import os
import sys

ATAS = os.path.join(os.environ.get("APPDATA", ""), "ATAS")
dia = sys.argv[1] if len(sys.argv) > 1 else "2026-09-03"
salida = sys.argv[2] if len(sys.argv) > 2 else os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "datos", "simulador", "mockups-gammalito.html")

velas = []
for l in io.open(os.path.join(ATAS, "pythiagex-centinela-rebobinado-MES-TimeFrame-M1.jsonl"), encoding="utf-8", errors="replace"):
    try:
        d = json.loads(l)
    except Exception:
        continue
    if d["t"].startswith(dia) and d.get("niv") and "15:30" <= d["t"][11:16] <= "19:30":
        n = d["niv"]
        velas.append([d["t"][11:16], d["o"], d["h"], d["l"], d["c"], n.get("dom0"), n.get("dom1"), n.get("zero_vol"), n.get("mp_vol"), n.get("mn_vol"), n.get("pico"), n.get("mc30"), n.get("mc5"), n.get("mc1")])
datos = json.dumps(velas, separators=(",", ":"))

HTML = r'''<title>Gamma Hoy a la GAMMAlito</title>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=IBM+Plex+Sans+Condensed:wght@500;600&family=IBM+Plex+Sans:wght@400;500&family=IBM+Plex+Mono:wght@400;500&display=swap">
<style>
  :root { --fondo:#0d1218; --panel:#141b24; --linea:#263141; --texto:#dde4ec; --apagado:#8794a4; --sube:#2ddc82; --baja:#eb3c3c; --dom:#e8c83c; --dom2:#b89a2e; --cero:#ebebeb; --aviso:#f0a058; --agua:#5dd9d0; --purpura:#a86bff; --foco:#ffd166; color-scheme: dark; }
  body { background:var(--fondo); color:var(--texto); font:14px/1.45 "IBM Plex Sans","Segoe UI",system-ui,sans-serif; margin:0; }
  .marco { max-width:1180px; margin:0 auto; padding:22px 22px 48px; }
  h1,h2 { font-family:"IBM Plex Sans Condensed","Arial Narrow",sans-serif; margin:0; text-wrap:balance; }
  .ojal { font-family:"IBM Plex Mono",Consolas,monospace; font-size:11px; letter-spacing:.12em; text-transform:uppercase; color:var(--apagado); }
  h1 { font-size:28px; font-weight:600; margin:4px 0 6px; }
  p.sub { color:var(--apagado); max-width:72ch; margin:0 0 18px; }
  .op { background:var(--panel); border:1px solid var(--linea); border-radius:4px; padding:14px 16px 12px; margin-bottom:18px; }
  .op h2 { font-size:19px; font-weight:600; }
  .op h2 span { font-family:"IBM Plex Mono",Consolas,monospace; color:var(--dom); margin-right:10px; }
  .op p { color:var(--apagado); max-width:80ch; margin:4px 0 10px; }
  .op ul { margin:0 0 10px 18px; padding:0; color:var(--texto); }
  .op li { margin:2px 0; }
  canvas { display:block; width:100%; height:420px; background:#0b1017; border:1px solid var(--linea); border-radius:3px; }
  .leyenda { display:flex; flex-wrap:wrap; gap:6px 16px; font-size:12px; color:var(--apagado); margin-top:8px; }
  .leyenda span { display:inline-flex; align-items:center; gap:6px; }
  .leyenda i { width:14px; height:3px; display:inline-block; border-radius:2px; }
  .como { margin-top:26px; padding-top:14px; border-top:1px solid var(--linea); color:var(--apagado); font-size:13px; max-width:78ch; }
</style>
<div class="marco">
  <span class="ojal">PythiaGex · Gamma Hoy · mockups</span>
  <h1>Gamma Hoy a la GAMMAlito</h1>
  <p class="sub">El mismo tramo real, jueves 3 de septiembre de 11:30 a 15:30 de Nueva York, velas de 1 minuto rebobinadas con la misma cuenta que corre en ATAS. Lo que cambia es solo el dibujo. Elegí una, o mezclá: "la dos con las pelotitas de la tres".</p>

  <div class="op"><h2><span>1</span>GAMMAlito puro: guiones, sin rayas</h2>
    <p>Como en los videos: la dominante primaria es un guion amarillo grueso por vela, la secundaria un guion más fino y apagado; forman la línea sola, minuto a minuto, y se ve dónde nació y cuándo saltó. Ninguna raya cruza el gráfico. El zero gamma queda como puntitos blancos chicos. Los valores del último minuto van en el eje, como etiquetas, no como líneas.</p>
    <ul><li>Se ve la historia de las dominantes de un vistazo, sin tapar las velas.</li><li>Se pierde la "raya a la derecha" que hoy te muestra el nivel hacia adelante: lo reemplaza la etiqueta en el eje.</li></ul>
    <canvas id="c1"></canvas>
    <div class="leyenda"><span><i style="background:var(--dom)"></i>dominante 1</span><span><i style="background:var(--dom2)"></i>dominante 2</span><span><i style="background:var(--cero)"></i>zero gamma (puntos)</span></div>
  </div>

  <div class="op"><h2><span>2</span>Guiones fuertes, rayas apenas insinuadas</h2>
    <p>Igual que la 1, pero los niveles del último minuto siguen como rayas hacia la derecha, casi transparentes (un 25 % de opacidad), para conservar el "hacia dónde" sin que compitan con los guiones. Los majors en verde y rojo bien tenues; el zero en blanco punteado.</p>
    <ul><li>Lo mejor de las dos: historia con guiones, presente con rayas suaves.</li><li>Un poco más cargado que la 1.</li></ul>
    <canvas id="c2"></canvas>
    <div class="leyenda"><span><i style="background:var(--dom)"></i>dominantes</span><span><i style="background:var(--sube);opacity:.4"></i>+Γ mayor (tenue)</span><span><i style="background:var(--baja);opacity:.4"></i>−Γ mayor (tenue)</span><span><i style="background:var(--cero);opacity:.5"></i>zero (tenue)</span></div>
  </div>

  <div class="op"><h2><span>3</span>Guiones con fuerza y semillas del Max Change</h2>
    <p>Los guiones son más gruesos cuanto más pesa la dominante, y se agrega lo que GAMMAlito llama "semillita": tres puntos naranjas por vela con el strike de mayor cambio a 30, 5 y 1 minuto. Cuando los tres se alinean varios minutos, ahí suele nacer la próxima dominante. Sin rayas; el pico cercano al precio va como cruz naranja.</p>
    <ul><li>Es la lectura completa de los videos: dominante, semilla, pico.</li><li>Más elementos en pantalla: conviene tamaño de letra chico y un gráfico ancho.</li></ul>
    <canvas id="c3"></canvas>
    <div class="leyenda"><span><i style="background:var(--dom)"></i>dominantes (grosor = fuerza)</span><span><i style="background:var(--aviso)"></i>Max Change 30 · 5 · 1 min</span><span><i style="background:var(--agua)"></i>pico cerca del precio</span></div>
  </div>

  <div class="como">Cómo lo leo yo: la 1 es la más fiel a los videos y la más limpia; la 2 es la que menos cambia respecto de lo que tenés hoy; la 3 es la más completa y la más cargada. En las tres desaparecen las rayas gruesas de hoy. Después de elegir, va al indicador y a la versión Híbrido en un solo paso.</div>
</div>
<script>
const V = __DATOS__;
const C = getComputedStyle(document.documentElement), col = n => C.getPropertyValue(n).trim();
function dibujar(id, modo) {
  const cv = document.getElementById(id), ctx = cv.getContext('2d');
  const dpr = window.devicePixelRatio || 1, W = cv.clientWidth, H = cv.clientHeight;
  cv.width = W * dpr; cv.height = H * dpr; ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  const izq = 46, der = 84, arr = 12, aba = 22, ancho = W - izq - der, alto = H - arr - aba;
  let lo = Math.min(...V.map(v => v[3])), hi = Math.max(...V.map(v => v[2]));
  for (const v of V) for (const k of [5, 6, 7, 8, 9]) if (v[k] && v[k] > lo - 25 && v[k] < hi + 25) { lo = Math.min(lo, v[k]); hi = Math.max(hi, v[k]); }
  const pad = (hi - lo) * .05; lo -= pad; hi += pad;
  const y = p => arr + alto * (1 - (p - lo) / (hi - lo)), x = j => izq + ancho * (j + .5) / V.length, w = ancho / V.length;
  ctx.font = '11px "IBM Plex Mono",Consolas,monospace'; ctx.textBaseline = 'middle';
  ctx.strokeStyle = col('--linea'); ctx.fillStyle = col('--apagado');
  for (let p = Math.ceil(lo / 5) * 5; p <= hi; p += 5) { ctx.beginPath(); ctx.moveTo(izq, Math.round(y(p)) + .5); ctx.lineTo(izq + ancho, Math.round(y(p)) + .5); ctx.stroke(); ctx.textAlign = 'right'; ctx.fillText(p.toFixed(0), izq - 5, y(p)); }
  ctx.textAlign = 'center'; ctx.textBaseline = 'top';
  for (let j = 0; j < V.length; j += 30) { ctx.fillText(V[j][0], x(j), arr + alto + 6); }
  // velas
  for (let j = 0; j < V.length; j++) {
    const v = V[j], up = v[4] >= v[1]; ctx.strokeStyle = ctx.fillStyle = col(up ? '--sube' : '--baja'); ctx.globalAlpha = .75;
    ctx.beginPath(); ctx.moveTo(x(j), y(v[2])); ctx.lineTo(x(j), y(v[3])); ctx.stroke();
    const y1 = y(Math.max(v[1], v[4])), y2 = y(Math.min(v[1], v[4])); ctx.fillRect(x(j) - w * .35, y1, w * .7, Math.max(1, y2 - y1));
  }
  ctx.globalAlpha = 1;
  const ult = V[V.length - 1];
  // zero gamma: puntitos blancos
  ctx.fillStyle = col('--cero'); ctx.globalAlpha = modo === 2 ? .35 : .6;
  for (let j = 0; j < V.length; j++) if (V[j][7]) { ctx.beginPath(); ctx.arc(x(j), y(V[j][7]), 1.1, 0, 7); ctx.fill(); }
  ctx.globalAlpha = 1;
  // guiones de dominantes por vela
  for (let j = 0; j < V.length; j++) {
    const v = V[j];
    for (const [k, c, g] of [[5, '--dom', modo === 3 ? 3.4 : 2.6], [6, '--dom2', modo === 3 ? 2.2 : 1.6]]) {
      if (!v[k]) continue;
      ctx.strokeStyle = col(c); ctx.lineWidth = g; ctx.globalAlpha = k === 5 ? 1 : .8;
      ctx.beginPath(); ctx.moveTo(x(j) - w * .5, y(v[k])); ctx.lineTo(x(j) + w * .5, y(v[k])); ctx.stroke();
    }
  }
  ctx.globalAlpha = 1; ctx.lineWidth = 1;
  if (modo === 2) {
    // rayas tenues del ultimo minuto
    const rayas = [[7, '--cero', [2, 3]], [8, '--sube', []], [9, '--baja', []], [5, '--dom', []], [6, '--dom2', []]];
    for (const [k, c, dash] of rayas) { if (!ult[k]) continue; ctx.strokeStyle = col(c); ctx.globalAlpha = .28; ctx.setLineDash(dash); ctx.lineWidth = 1.2;
      ctx.beginPath(); ctx.moveTo(izq, y(ult[k])); ctx.lineTo(izq + ancho, y(ult[k])); ctx.stroke(); }
    ctx.setLineDash([]); ctx.globalAlpha = 1;
  }
  if (modo === 3) {
    // semillas del Max Change: 30 (grande), 5 (mediana), 1 (chica) y el pico como cruz
    for (let j = 0; j < V.length; j++) {
      const v = V[j];
      for (const [k, r] of [[11, 2.6], [12, 1.9], [13, 1.2]]) if (v[k]) { ctx.fillStyle = col('--aviso'); ctx.globalAlpha = k === 11 ? .9 : .7; ctx.beginPath(); ctx.arc(x(j), y(v[k]), r, 0, 7); ctx.fill(); }
      if (v[10] && j % 3 === 0) { ctx.strokeStyle = col('--agua'); ctx.globalAlpha = .7; ctx.beginPath(); ctx.moveTo(x(j) - 2.5, y(v[10])); ctx.lineTo(x(j) + 2.5, y(v[10])); ctx.moveTo(x(j), y(v[10]) - 2.5); ctx.lineTo(x(j), y(v[10]) + 2.5); ctx.stroke(); }
    }
    ctx.globalAlpha = 1;
  }
  // etiquetas del ultimo minuto en el eje derecho
  const et = [[5, 'D1', '--dom'], [6, 'D2', '--dom2'], [7, '0Γ', '--cero'], [8, '+Γ', '--sube'], [9, '−Γ', '--baja']];
  const usados = [];
  ctx.font = '10.5px "IBM Plex Mono",Consolas,monospace'; ctx.textAlign = 'left'; ctx.textBaseline = 'middle';
  for (const [k, n, c] of et) { if (!ult[k]) continue; let yy = y(ult[k]); while (usados.some(u => Math.abs(u - yy) < 12)) yy += 12; usados.push(yy);
    ctx.fillStyle = col('--panel'); ctx.fillRect(izq + ancho + 2, yy - 7, der - 4, 14); ctx.fillStyle = col(c); ctx.fillText(n + ' ' + ult[k].toFixed(0), izq + ancho + 5, yy); }
}
function todo() { dibujar('c1', 1); dibujar('c2', 2); dibujar('c3', 3); }
todo(); window.addEventListener('resize', todo);
</script>
'''
os.makedirs(os.path.dirname(salida), exist_ok=True)
io.open(salida, "w", encoding="utf-8", newline="\n").write(HTML.replace("__DATOS__", datos))
print("mockups: %s (%d velas del %s)" % (salida, len(velas), dia))
