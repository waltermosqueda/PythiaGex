// ---------- la 2E elegida por el operador (09-10): barrita de peso + LIBRO ROL MONTO y el cambio PEGADO al monto. Dos escalas para elegir.
const V = [];
const XL = X1+70;
const dSolo = d => (d && d!=='=') ? d : '';
const enR = () => enRango(G);
function base2(op){ return lineas(svgBase(),G,{op:op||0.85}); }
function recuadro(s, abierta){
  const top=G.filter(g=>mayor(g)).sort((a,b)=>Math.abs(mayor(b).m)-Math.abs(mayor(a).m)).slice(0,5);
  const x=X0+6, y0=YT-2, w=208;
  let g=`<g class="caja" data-abierta="${abierta?1:0}"><g class="cab"><rect x="${x}" y="${y0}" width="${w}" height="16" rx="4" fill="#0b0e13" fill-opacity="0.92" stroke="#2a3240"/>`;
  g+=`<text class="flecha" x="${x+6}" y="${y0+11.5}" font-size="10" fill="#9aa4b2">${abierta?'▾':'▸'}</text>`;
  let lx=x+18; for(const L of ['NQ','NDX','QQQ','TQQQ']){ g+=`<rect x="${lx}" y="${y0+4}" width="8" height="8" rx="1.5" fill="${LIBCOL[L]}"/><text x="${lx+11}" y="${y0+11.5}" font-size="9.5" fill="${LIBCOL[L]}">${L}</text>`; lx+= L.length*6+20; }
  g+=`</g><g class="cuerpo" style="display:${abierta?'inline':'none'}"><rect x="${x}" y="${y0+17}" width="${w}" height="${top.length*14+6}" rx="4" fill="#0b0e13" fill-opacity="0.88" stroke="#2a3240"/>`;
  let yy=y0+29; for(const t of top){ const i=mayor(t), dt=dSolo(deltaTxt(t));
    g+=`<text x="${x+6}" y="${yy}" font-size="10.5" fill="${LIBCOL[t.libro]}">${t.libro} ${rolesTxt(t)} ${fmtM(i.m)}${dt?' ':''}</text>`;
    if(dt) g+=`<text x="${x+w-6}" y="${yy}" font-size="10.5" fill="${dColor(dt)}" text-anchor="end">${dt}</text>`; yy+=14; }
  return s+g+`</g></g>`;
}
// la etiqueta 2E: barrita (ancho segun 'escala') + texto + cambio pegado
function etiqueta2E(s, g, yy, ancho){ const i=mayor(g), c=LIBCOL[g.libro], dt=dSolo(deltaTxt(g)), BX=XL+48;
  s+=`<rect x="${BX-48}" y="${yy-4}" width="48" height="8" rx="2" fill="#1a2029"/>`;                 // el carril (100 %)
  if(i){ s+=`<rect x="${BX-ancho}" y="${yy-4}" width="${ancho}" height="8" rx="2" fill="${i.m<0?'#ef5350':'#26a69a'}" fill-opacity="0.9"/>`; }
  const t=g.libro+' '+rolesTxt(g)+' '+montosTxt(g).replace('+',''); s=txt(s,BX+6,yy,t,c,'start',10.8);
  if(dt) s=txt(s,BX+6+t.length*6.55+6,yy,dt,dColor(dt),'start',10.5,'bold');
  return s; }
V.push({t:'2E LINEAL: la barrita es proporcional de verdad (la mas grande = 100 %)', d:'Ancho = |monto| / el monto mas grande en pantalla, sin trucos. Es la proporcion exacta: QQQ C 1,87B llena el carril y NQ C 15M casi no se ve (pesa 125 veces menos). Minimo 2 px para que se note que hay algo. El cambio va pegado al monto. Recuadro abierto.',
  f(){ let s=base2(); const R=enR().filter(g=>mayor(g)); const mx=Math.max(...R.map(g=>Math.abs(mayor(g).m)));
    for(const it of separar(enR().map(g=>({g,yy:y(g.precio)})),15)){ const g=it.g, i=mayor(g); const a=i? Math.max(2, 48*Math.abs(i.m)/mx) : 0; s=etiqueta2E(s,g,it.yy,a); }
    return recuadro(fueraDeRango(s,G),true)+'</svg>'; }});
V.push({t:'2E RAIZ: proporcional a la raiz cuadrada (las chicas se ven, el orden se respeta)', d:'Ancho = raiz(|monto| / el mas grande). Sigue el orden exacto (mas grande = mas larga) pero las chicas se distinguen: NQ C 15M queda en 9 % del carril en vez de 1 %. Es lo que usan los perfiles de volumen para que no desaparezcan las barras chicas. Recuadro cerrado.',
  f(){ let s=base2(); const R=enR().filter(g=>mayor(g)); const mx=Math.max(...R.map(g=>Math.abs(mayor(g).m)));
    for(const it of separar(enR().map(g=>({g,yy:y(g.precio)})),15)){ const g=it.g, i=mayor(g); const a=i? Math.max(2, 48*Math.sqrt(Math.abs(i.m)/mx)) : 0; s=etiqueta2E(s,g,it.yy,a); }
    return recuadro(fueraDeRango(s,G),false)+'</svg>'; }});
V.push({t:'2E LOG (la que viste antes): escala logaritmica', d:'Ancho = log(1+|monto|) / log(1+el mas grande). Las diferencias se aplastan: la de 108M se ve al 62 % de la de 1,87B cuando en realidad pesa el 6 %. Lo dejo para comparar; no es la proporcion real.',
  f(){ let s=base2(); const R=enR().filter(g=>mayor(g)); const mx=Math.max(...R.map(g=>Math.log10(1+Math.abs(mayor(g).m))));
    for(const it of separar(enR().map(g=>({g,yy:y(g.precio)})),15)){ const g=it.g, i=mayor(g); const a=i? Math.max(2, 48*Math.log10(1+Math.abs(i.m))/mx) : 0; s=etiqueta2E(s,g,it.yy,a); }
    return recuadro(fueraDeRango(s,G),true)+'</svg>'; }});
