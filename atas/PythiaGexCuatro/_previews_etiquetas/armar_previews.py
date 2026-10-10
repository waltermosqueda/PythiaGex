# -*- coding: utf-8 -*-
"""armar_previews.py — 09-10-2026. Pedido del operador: "armame varias versiones mas ordenadas, minimalistas, de las etiquetas de ambos lados
... no repetir la data (por ejemplo el precio) ... a la izquierda QQQ (C, P, M+, 0G) 1,85B V/OI y a la derecha QQQ (C, P, M+) 31.500 V/OI y una
flecha de direccion y cuanto aumento/disminuyo ... lo mas importante para la toma de decisiones ... unas 10 minimo".
Lee datos.json (exportado del niv de la 4.1: ultimo minuto de la sesion del 09-10 y el cambio en 15 min) y escribe previews.html (autocontenido).
Uso: python -I armar_previews.py"""
import json, os

AQUI = os.path.dirname(os.path.abspath(__file__))
datos = json.load(open(os.path.join(AQUI, "datos.json"), encoding="utf-8"))

HTML = r"""<!doctype html>
<html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Etiquetas 4.1: maquetas</title>
<style>
:root{--fondo:#0f1218;--panel:#151a22;--texto:#d7dde8;--tenue:#7d8796;--borde:#262d3a;--vela-sube:#26a69a;--vela-baja:#ef5350;--precio:#e8eef7}
body{margin:0;background:var(--fondo);color:var(--texto);font:14px/1.45 system-ui,Segoe UI,Roboto,sans-serif}
header{padding:16px 16px 4px;max-width:1100px;margin:auto}
h1{font-size:20px;margin:0 0 6px}
.nota{color:var(--tenue);font-size:12.5px}
.grid{display:grid;grid-template-columns:1fr;gap:18px;padding:12px 16px 40px;max-width:1100px;margin:auto}
.card{background:var(--panel);border:1px solid var(--borde);border-radius:10px;padding:10px 12px}
.card h2{font-size:15px;margin:2px 0 2px}
.card p{margin:0 0 6px;color:var(--tenue);font-size:12.5px}
svg{width:100%;height:auto;display:block;background:#0b0e13;border-radius:6px}
text{font-family:Consolas,Menlo,monospace}
</style></head><body>
<header>
<h1>Etiquetas de la 4.1: 12 maquetas para elegir</h1>
<div class="nota" id="fuente"></div>
</header>
<div class="grid" id="grid"></div>
<script>
const D = __DATOS__;
const COL = {MUROS_NQ_vol:'#4db5e4',MUROS_NQ_oi:'#9a98d6',MAJORS_NQ_vol:'#6cc3ff',ZEST_NQ_vol:'#c9d4e3',MUROS_NDX_vol:'#2fb3a8',MUROS_NDX_oi:'#8de031',
  MAJORS_NDX_vol:'#4fd1c5',ZEST_NDX_vol:'#7dd3fc',MUROS_QQQ_vol:'#dabe6f',MUROS_QQQ_oi:'#ff9f43',MAJORS_QQQ_vol:'#f6c177',MAJORS_QQQ_oi:'#e2b872',
  DOMS_QQQ_vol:'#5b8cff',T_MUROS_vol:'#26c6da',T_DOMS_vol:'#ffd54f'};
const LIBCOL = {NQ:'#5ab8f0',NDX:'#3fd0b8',QQQ:'#f2b45c',TQQQ:'#d58cf0'};
const W=1060,H=470, X0=200, X1=760, YT=18, YB=H-22, PMIN=31035, PMAX=31215;
const y = p => YT + (PMAX-p)/(PMAX-PMIN)*(YB-YT);
const fut = D.fut;
document.getElementById('fuente').textContent = 'Datos reales: PythiaGex 4.1, sesion del 09-10, minuto ' + D.minuto + '; cambio contra ' + D.comparado +
  '. El mercado esta cerrado: dato de la sesion del viernes. Las velas son aproximadas, armadas con el precio de cada minuto; las rayas y los montos son los de la 4.1. Las 3 de TQQQ salen de tu pantalla (19:18-20:00).';

// ---------- niveles normalizados
function libroDe(s){ return s.includes('_NDX')?'NDX': s.startsWith('T_')?'TQQQ': s.includes('_QQQ')?'QQQ':'NQ'; }
function rolDe(s,e){ if(s.startsWith('MUROS')||s==='T_MUROS_vol') return e==='D1'?'C':'P'; if(s.startsWith('MAJORS')) return e==='D1'?'M+':'M−';
  if(s.startsWith('ZEST')) return '0Γ'; if(s.startsWith('DOMS')||s==='T_DOMS_vol') return e==='D1'?'D1':'D2'; return e; }
function fuenteDe(s){ return s.endsWith('_oi')?'OI':'V'; }
const N = D.niveles.map(n=>({serie:n.serie, libro:libroDe(n.serie), rol:rolDe(n.serie,n.etq), fuente:fuenteDe(n.serie), precio:n.precio, m:n.gexM, d:n.d15, color:COL[n.serie]||'#aaa'}));
// agrupar: mismo libro y mismo precio (0,5 pt) -> una entrada con varios roles
function agrupar(lista, tol){ const out=[]; for(const n of [...lista].sort((a,b)=>b.precio-a.precio)){ const g=out.find(o=>o.libro===n.libro && Math.abs(o.precio-n.precio)<=tol);
  if(g){ g.items.push(n); } else out.push({libro:n.libro, precio:n.precio, items:[n]}); } return out; }
const G = agrupar(N, 0.6);

// ---------- formato
const fmtP = p => Math.round(p).toLocaleString('es-AR');
function fmtM(m){ if(m==null||isNaN(m)) return ''; const a=Math.abs(m), s=m<0?'−':'+'; if(a>=1000) return s+(a/1000).toLocaleString('es-AR',{maximumFractionDigits:2,minimumFractionDigits:2})+'B';
  return s+Math.round(a).toLocaleString('es-AR')+'M'; }
function fmtD(d){ if(d==null||isNaN(d)) return ''; if(Math.abs(d)<0.5) return '='; const a=Math.abs(d); const v=a>=1000?(a/1000).toLocaleString('es-AR',{maximumFractionDigits:1})+'B':(a<10?a.toLocaleString('es-AR',{maximumFractionDigits:1}):Math.round(a).toLocaleString('es-AR'))+'M';
  return (d>0?'▲':'▼')+v; }
const rolesTxt = g => [...new Set(g.items.map(i=>i.rol))].join('/');
const fuentesTxt = g => [...new Set(g.items.map(i=>i.fuente))].join('/');
const mayor = g => g.items.filter(i=>i.m!=null).sort((a,b)=>Math.abs(b.m)-Math.abs(a.m))[0];
const montosTxt = g => { const i=mayor(g); return i?fmtM(i.m):''; };   // minimalista: solo el monto mas grande del grupo
const deltaTxt = g => { const i=mayor(g); return i?fmtD(i.d):''; };
const dColor = t => t.startsWith('▲')?'#4ade80': t.startsWith('▼')?'#f87171':'#9aa4b2';

// ---------- dibujo base
function svgBase(){ let s=`<svg viewBox="0 0 ${W} ${H}" xmlns="http://www.w3.org/2000/svg">`;
  for(let p=31040;p<=31210;p+=20){ s+=`<line x1="${X0}" x2="${X1}" y1="${y(p)}" y2="${y(p)}" stroke="#1a2029"/>`; }
  const P=D.precio, n=P.length, dx=(X1-X0-10)/n;
  for(let i=0;i<n;i++){ const o=i?P[i-1].f:P[i].f, c=P[i].f, hi=Math.max(o,c)+0.75+(i%3)*0.5, lo=Math.min(o,c)-0.75-((i+1)%3)*0.5, x=X0+5+i*dx;
    const col=c>=o?'var(--vela-sube)':'var(--vela-baja)';
    s+=`<line x1="${x+dx/2}" x2="${x+dx/2}" y1="${y(hi)}" y2="${y(lo)}" stroke="${col}" stroke-width="1"/>`;
    s+=`<rect x="${x+0.4}" y="${y(Math.max(o,c))}" width="${Math.max(1,dx-0.8)}" height="${Math.max(1,Math.abs(y(o)-y(c)))}" fill="${col}"/>`; }
  s+=`<line x1="${X0}" x2="${X1}" y1="${y(fut)}" y2="${y(fut)}" stroke="#e8eef7" stroke-dasharray="2,3" stroke-width="0.8"/>`;
  // 09-10 (pedido del operador): sin la cajita del precio; el espacio queda (el precio ya lo muestra ATAS en su eje)
  return s; }
function lineas(s, grupos, opts={}){ for(const g of grupos){ if(g.precio<PMIN||g.precio>PMAX) continue; const i=mayor(g)||g.items[0];
  const w = opts.peso? (1+Math.min(4,Math.log10(1+Math.abs(i.m||1))*1.2)) : 1.3;
  s+=`<line x1="${X0}" x2="${X1}" y1="${y(g.precio)}" y2="${y(g.precio)}" stroke="${LIBCOL[g.libro]}" stroke-width="${w}" stroke-opacity="${opts.op||0.85}"/>`; } return s; }
// separa etiquetas para que no se pisen (de arriba hacia abajo, minimo 'h' px)
function separar(items,h){ const a=[...items].sort((p,q)=>p.yy-q.yy); for(let i=1;i<a.length;i++) if(a[i].yy-a[i-1].yy<h) a[i].yy=a[i-1].yy+h;
  const exceso=a.length? a[a.length-1].yy-(YB-4):0; if(exceso>0) for(const it of a) it.yy-=exceso; for(let i=a.length-2;i>=0;i--) if(a[i+1].yy-a[i].yy<h) a[i].yy=a[i+1].yy-h; return a; }
function caja(s,x,yy,txt,col,anc='start',fs=11.5,fondo='#0b0e13'){ const w=txt.length*fs*0.6+8; const xx= anc==='end'? x-w : anc==='middle'? x-w/2 : x;
  return s+`<rect x="${xx}" y="${yy-fs*0.85}" width="${w}" height="${fs*1.25}" rx="3" fill="${fondo}" stroke="${col}" stroke-width="0.9"/>`+
    `<text x="${xx+4}" y="${yy+fs*0.25}" font-size="${fs}" fill="${col}">${txt}</text>`; }
function txt(s,x,yy,t,col,anc='start',fs=11.5,peso='normal'){ return s+`<text x="${x}" y="${yy+fs*0.32}" font-size="${fs}" fill="${col}" text-anchor="${anc}" font-weight="${peso}">${t}</text>`; }
function conector(s,x1,y1,x2,y2,col){ return s+`<path d="M${x1},${y1} L${x2},${y2}" stroke="${col}" stroke-width="0.7" stroke-opacity="0.7" fill="none"/>`; }
function fueraDeRango(s, grupos){ const arr=grupos.filter(g=>g.precio>PMAX), ab=grupos.filter(g=>g.precio<PMIN);
  if(arr.length) s=txt(s,X1-4,YT+4,'↑ '+arr.map(g=>g.libro+' '+rolesTxt(g)+' '+fmtP(g.precio)).join(' · '),'#9aa4b2','end',10.5);
  if(ab.length) s=txt(s,X1-4,YB-2,'↓ '+ab.map(g=>g.libro+' '+rolesTxt(g)+' '+fmtP(g.precio)).join(' · '),'#9aa4b2','end',10.5); return s; }
const enRango = gs => gs.filter(g=>g.precio>=PMIN&&g.precio<=PMAX);

// ---------- las variantes
const V = [];
V.push({t:'1. Tu idea: a la izquierda el monto, a la derecha el precio y el cambio', d:'Izquierda: LIBRO ROL MONTO FUENTE. Derecha: LIBRO ROL PRECIO y la flecha del cambio en 15 min. Cada dato aparece una sola vez; el libro y el rol van en los dos lados para que cada costado se lea solo.',
  f(){ let s=lineas(svgBase(),G); const R=enRango(G);
    let L=separar(R.map(g=>({g,yy:y(g.precio)})),14); for(const it of L){ const g=it.g; s=conector(s,X0-4,it.yy,X0+6,y(g.precio),LIBCOL[g.libro]); s=caja(s,X0-6,it.yy,g.libro+' '+rolesTxt(g)+' '+montosTxt(g)+' '+fuentesTxt(g),LIBCOL[g.libro],'end',10.5); }
    let Rr=separar(R.map(g=>({g,yy:y(g.precio)})),14); for(const it of Rr){ const g=it.g, dt=deltaTxt(g); s=conector(s,X1-6,y(g.precio),X1+66,it.yy,LIBCOL[g.libro]);
      s=caja(s,X1+68,it.yy,g.libro+' '+rolesTxt(g)+' '+fmtP(g.precio)+(dt?'  '+dt:''),LIBCOL[g.libro],'start',10.5); }
    return fueraDeRango(s,G)+'</svg>'; }});
V.push({t:'2. Una sola columna a la derecha, sin el precio', d:'LIBRO ROL MONTO y la flecha del cambio. El precio se lee en el eje: la etiqueta queda pegada a su raya. Es la mas corta.',
  f(){ let s=lineas(svgBase(),G); const R=separar(enRango(G).map(g=>({g,yy:y(g.precio)})),14);
    for(const it of R){ const g=it.g, dt=deltaTxt(g); s=conector(s,X1-4,y(g.precio),X1+66,it.yy,LIBCOL[g.libro]); s=txt(s,X1+68,it.yy,g.libro+' '+rolesTxt(g)+' '+montosTxt(g),LIBCOL[g.libro],'start',11);
      if(dt) s=txt(s,W-6,it.yy,dt,dColor(dt),'end',11); }
    return fueraDeRango(s,G)+'</svg>'; }});
V.push({t:'3. Libro y monto a la izquierda; a la derecha solo el precio y el cambio', d:'El nombre aparece una sola vez, a la izquierda. La raya une los dos lados.',
  f(){ let s=lineas(svgBase(),G,{op:0.6}); const R=enRango(G);
    for(const it of separar(R.map(g=>({g,yy:y(g.precio)})),14)){ const g=it.g; s=txt(s,X0-8,it.yy,g.libro+' '+rolesTxt(g)+' '+montosTxt(g),LIBCOL[g.libro],'end',10.5); s=conector(s,X0-6,it.yy,X0+4,y(g.precio),LIBCOL[g.libro]); }
    for(const it of separar(R.map(g=>({g,yy:y(g.precio)})),14)){ const g=it.g, dt=deltaTxt(g); s=txt(s,X1+68,it.yy,fmtP(g.precio)+' '+fuentesTxt(g),'#cfd6e2','start',10.5); if(dt) s=txt(s,X1+150,it.yy,dt,dColor(dt),'start',10.5); }
    return fueraDeRango(s,G)+'</svg>'; }});
V.push({t:'4. Columnas por libro (NQ · NDX · QQQ · TQQQ)', d:'A la derecha, cuatro columnas angostas: cada nivel cae en la columna de su libro, a la altura de su precio. Solo ROL y MONTO.',
  f(){ let s=lineas(svgBase(),G,{op:0.5}); const cols={NQ:X1+70,NDX:X1+128,QQQ:X1+186,TQQQ:X1+238};
    for(const L of Object.keys(cols)) s=txt(s,cols[L],YT-6,L,LIBCOL[L],'start',10.5,'bold');
    for(const L of Object.keys(cols)){ const R=separar(enRango(G).filter(g=>g.libro===L).map(g=>({g,yy:y(g.precio)})),13);
      for(const it of R){ const g=it.g, i=mayor(g); s=txt(s,cols[L],it.yy,rolesTxt(g)+' '+(i?fmtM(i.m).replace('+',''):''),LIBCOL[L],'start',10); } }
    return fueraDeRango(s,G)+'</svg>'; }});
V.push({t:'5. Solo lo cercano al precio (±40 pts), lo lejano como contador', d:'Se rotulan las rayas a menos de 40 pts. Las demas: un renglon arriba y otro abajo con la mas cercana de cada lado y cuantas hay.',
  f(){ const cerca=G.filter(g=>Math.abs(g.precio-fut)<=40), arr=G.filter(g=>g.precio-fut>40).sort((a,b)=>a.precio-b.precio), ab=G.filter(g=>fut-g.precio>40).sort((a,b)=>b.precio-a.precio);
    let s=lineas(svgBase(),cerca); for(const g of [...arr,...ab]) if(g.precio>=PMIN&&g.precio<=PMAX) s+=`<line x1="${X1-40}" x2="${X1}" y1="${y(g.precio)}" y2="${y(g.precio)}" stroke="${LIBCOL[g.libro]}" stroke-opacity="0.4"/>`;
    for(const it of separar(cerca.map(g=>({g,yy:y(g.precio)})),15)){ const g=it.g, dt=deltaTxt(g); s=conector(s,X1-4,y(g.precio),X1+66,it.yy,LIBCOL[g.libro]);
      s=caja(s,X1+68,it.yy,g.libro+' '+rolesTxt(g)+' '+montosTxt(g)+(dt?' '+dt:''),LIBCOL[g.libro],'start',11); }
    if(arr.length) s=txt(s,X1+68,YT+2,'↑ '+arr.length+' arriba · la mas cerca: '+arr[0].libro+' '+rolesTxt(arr[0])+' +'+Math.round(arr[0].precio-fut)+' pts','#9aa4b2','start',10);
    if(ab.length) s=txt(s,X1+68,YB-2,'↓ '+ab.length+' abajo · la mas cerca: '+ab[0].libro+' '+rolesTxt(ab[0])+' −'+Math.round(fut-ab[0].precio)+' pts','#9aa4b2','start',10);
    return s+'</svg>'; }});
V.push({t:'6. El peso se ve: grosor y letra segun el monto, solo las 6 mas grandes con texto', d:'La raya mas gruesa es la de mas GEX. Texto solo en las 6 de mayor monto; el resto queda como raya fina sin rotulo.',
  f(){ let s=lineas(svgBase(),G,{peso:true}); const top=enRango(G).filter(g=>mayor(g)).sort((a,b)=>Math.abs(mayor(b).m)-Math.abs(mayor(a).m)).slice(0,6);
    for(const it of separar(top.map(g=>({g,yy:y(g.precio)})),16)){ const g=it.g, i=mayor(g), fs=10+Math.min(4,Math.log10(1+Math.abs(i.m))*1.1), dt=deltaTxt(g);
      s=conector(s,X1-4,y(g.precio),X1+66,it.yy,LIBCOL[g.libro]); s=txt(s,X1+68,it.yy,g.libro+' '+rolesTxt(g)+' '+fmtM(i.m)+(dt?' '+dt:''),LIBCOL[g.libro],'start',fs,'bold'); }
    return fueraDeRango(s,G)+'</svg>'; }});
V.push({t:'7. Escalera a la derecha (tabla por precio, nada sobre el grafico)', d:'Una fila por precio, de arriba hacia abajo, con una celda por libro. El grafico queda limpio: solo las rayas.',
  f(){ let s=lineas(svgBase(),G,{op:0.55}); const precios=[...new Set(G.map(g=>Math.round(g.precio)))].sort((a,b)=>b-a); const xs={P:X1+66,NQ:X1+110,NDX:X1+158,QQQ:X1+206,TQQQ:X1+254};
    s=txt(s,xs.P,YT-4,'precio','#9aa4b2','start',10,'bold'); for(const L of ['NQ','NDX','QQQ','TQQQ']) s=txt(s,xs[L],YT-4,L,LIBCOL[L],'start',10,'bold');
    let yy=YT+12; for(const p of precios){ const fila=G.filter(g=>Math.round(g.precio)===p); s=txt(s,xs.P,yy,fmtP(p),Math.abs(p-fut)<1?'#fff':'#cfd6e2','start',10);
      for(const g of fila){ const i=mayor(g); s=txt(s,xs[g.libro],yy,rolesTxt(g)+(i?' '+fmtM(i.m).replace('+',''):''),LIBCOL[g.libro],'start',9.5); }
      if(p<fut && precios[precios.indexOf(p)-1]>=fut){ s+=`<line x1="${X1+62}" x2="${W-4}" y1="${yy-9}" y2="${yy-9}" stroke="#e8eef7" stroke-dasharray="2,3"/>`; }
      yy+=16; }
    return s+'</svg>'; }});
V.push({t:'8. Barras de peso a la izquierda, rol y cambio a la derecha', d:'A la izquierda, una barra por nivel con largo segun el monto (escala logaritmica) y el monto adentro. A la derecha, solo ROL y la flecha.',
  f(){ let s=lineas(svgBase(),G,{op:0.6}); const R=enRango(G).filter(g=>mayor(g)); const mx=Math.max(...R.map(g=>Math.log10(1+Math.abs(mayor(g).m))));
    for(const it of separar(R.map(g=>({g,yy:y(g.precio)})),14)){ const g=it.g, i=mayor(g), w=Math.max(18,(X0-14)*Math.log10(1+Math.abs(i.m))/mx);
      s+=`<rect x="${X0-4-w}" y="${it.yy-6}" width="${w}" height="12" rx="2" fill="${LIBCOL[g.libro]}" fill-opacity="${i.m<0?0.35:0.75}"/>`; s=txt(s,X0-8,it.yy,fmtM(i.m),'#0b0e13','end',10,'bold'); }
    for(const it of separar(enRango(G).map(g=>({g,yy:y(g.precio)})),14)){ const g=it.g, dt=deltaTxt(g); s=txt(s,X1+68,it.yy,g.libro+' '+rolesTxt(g)+' '+fuentesTxt(g),LIBCOL[g.libro],'start',10.5); if(dt) s=txt(s,W-8,it.yy,dt,dColor(dt),'end',10.5); }
    return fueraDeRango(s,G)+'</svg>'; }});
V.push({t:'9. Un rotulo por precio (se juntan los libros que caen a 3 pts o menos)', d:'Las rayas que coinciden se dicen en una sola linea: "31.100 · NQ C/P M+ · QQQ ...". Menos rotulos y se ve la confluencia.',
  f(){ let s=lineas(svgBase(),G,{op:0.7}); const F=[]; for(const g of [...enRango(G)].sort((a,b)=>b.precio-a.precio)){ const f=F.find(x=>Math.abs(x.precio-g.precio)<=3); if(f) f.gs.push(g); else F.push({precio:g.precio,gs:[g]}); }
    for(const it of separar(F.map(f=>({f,yy:y(f.precio)})),15)){ const f=it.f; let x=X1+68; s=conector(s,X1-4,y(f.precio),X1+66,it.yy,'#9aa4b2'); s=txt(s,x,it.yy,fmtP(f.precio),'#e8eef7','start',10.5,'bold'); x+=46;
      for(const g of f.gs){ const t=g.libro+' '+rolesTxt(g); s=txt(s,x,it.yy,t,LIBCOL[g.libro],'start',10.5); x+=t.length*6.3+8; } }
    return fueraDeRango(s,G)+'</svg>'; }});
V.push({t:'10. Marcas cortas en el borde, con leyenda de colores', d:'Al lado de cada raya solo el ROL (C, P, M+, 0Γ) en el color de su libro. Arriba, un cuadrito con los 5 montos mas grandes y su cambio.',
  f(){ let s=lineas(svgBase(),G); for(const it of separar(enRango(G).map(g=>({g,yy:y(g.precio)})),12)){ const g=it.g; s=txt(s,X1+68,it.yy,rolesTxt(g),LIBCOL[g.libro],'start',11,'bold'); }
    let x=X0+6; for(const L of ['NQ','NDX','QQQ','TQQQ']){ s+=`<rect x="${x}" y="${YT-2}" width="9" height="9" fill="${LIBCOL[L]}"/>`; s=txt(s,x+12,YT+2,L,LIBCOL[L],'start',10); x+=52; }
    const top=G.filter(g=>mayor(g)).sort((a,b)=>Math.abs(mayor(b).m)-Math.abs(mayor(a).m)).slice(0,5); let yy=YT+22; s+=`<rect x="${X0+4}" y="${YT+12}" width="210" height="${top.length*15+8}" rx="4" fill="#0b0e13" fill-opacity="0.85" stroke="#262d3a"/>`;
    for(const g of top){ const i=mayor(g), dt=deltaTxt(g); s=txt(s,X0+10,yy,g.libro+' '+rolesTxt(g)+' '+fmtM(i.m),LIBCOL[g.libro],'start',10.5); if(dt) s=txt(s,X0+206,yy,dt,dColor(dt),'end',10.5); yy+=15; }
    return fueraDeRango(s,G)+'</svg>'; }});
V.push({t:'11. Distancia al precio en vez del precio', d:'LIBRO ROL MONTO y cuantos puntos faltan para llegar (+ arriba, − abajo). Es lo que se usa para decidir si hay recorrido.',
  f(){ let s=lineas(svgBase(),G); for(const it of separar(enRango(G).map(g=>({g,yy:y(g.precio)})),14)){ const g=it.g, dist=Math.round(g.precio-fut), dt=deltaTxt(g);
      s=conector(s,X1-4,y(g.precio),X1+66,it.yy,LIBCOL[g.libro]); s=txt(s,X1+68,it.yy,g.libro+' '+rolesTxt(g)+' '+montosTxt(g),LIBCOL[g.libro],'start',10.5);
      s=txt(s,W-58,it.yy,(dist>0?'+':dist<0?'−':'')+Math.abs(dist)+' pts','#e8eef7','end',10.5); if(dt) s=txt(s,W-6,it.yy,dt,dColor(dt),'end',10.5); }
    return fueraDeRango(s,G)+'</svg>'; }});
V.push({t:'12. Tablero de decision arriba a la izquierda, rayas sin texto', d:'Las 3 mas cercanas arriba y abajo del precio: libro, rol, monto, distancia y cambio. Sobre el grafico solo las rayas.',
  f(){ let s=lineas(svgBase(),G,{op:0.8}); const arr=G.filter(g=>g.precio>fut).sort((a,b)=>a.precio-b.precio).slice(0,3), ab=G.filter(g=>g.precio<=fut).sort((a,b)=>b.precio-a.precio).slice(0,3);
    s+=`<rect x="${X0+6}" y="${YT+4}" width="300" height="${(arr.length+ab.length)*15+34}" rx="5" fill="#0b0e13" fill-opacity="0.9" stroke="#262d3a"/>`; let yy=YT+18;
    s=txt(s,X0+14,yy,'ARRIBA','#9aa4b2','start',10,'bold'); yy+=14; for(const g of [...arr].reverse()){ s=fila(s,g,yy); yy+=15; }
    s=txt(s,X0+14,yy,'precio '+fmtP(fut),'#e8eef7','start',10,'bold'); yy+=14; s=txt(s,X0+14,yy-2,'ABAJO','#9aa4b2','start',10,'bold'); yy+=12; for(const g of ab){ s=fila(s,g,yy); yy+=15; }
    function fila(s,g,yy){ const i=mayor(g), dt=deltaTxt(g), dist=Math.round(g.precio-fut); s=txt(s,X0+14,yy,g.libro+' '+rolesTxt(g),LIBCOL[g.libro],'start',10.5);
      s=txt(s,X0+120,yy,i?fmtM(i.m):'','#cfd6e2','start',10.5); s=txt(s,X0+186,yy,(dist>0?'+':dist<0?'−':'')+Math.abs(dist)+' pts','#cfd6e2','start',10.5); if(dt) s=txt(s,X0+298,yy,dt,dColor(dt),'end',10.5); return s; }
    return s+'</svg>'; }});
const grid=document.getElementById('grid');
for(const v of V){ const c=document.createElement('div'); c.className='card'; c.innerHTML=`<h2>${v.t}</h2><p>${v.d}</p>`+v.f(); grid.appendChild(c); }
</script></body></html>"""

open(os.path.join(AQUI, "previews.html"), "w", encoding="utf-8").write(HTML.replace("__DATOS__", json.dumps(datos, ensure_ascii=False)))
print("ok", os.path.join(AQUI, "previews.html"))
