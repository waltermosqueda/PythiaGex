// ---------- las variantes (todas de la familia de la 2: una columna a la derecha, sin precio) + el recuadro desplegable
const V = [];
const XL = X1+70;                                   // donde arranca la columna de etiquetas
const dSolo = d => (d && d!=='=') ? d : '';         // si no cambio, no se escribe nada
const enR = () => enRango(G);
function base2(op){ return lineas(svgBase(),G,{op:op||0.85}); }

// El recuadro: titulo con la leyenda de colores (clic = abre/cierra) y, abierto, los 5 montos mas grandes con su cambio.
function recuadro(s, abierta){
  const top=G.filter(g=>mayor(g)).sort((a,b)=>Math.abs(mayor(b).m)-Math.abs(mayor(a).m)).slice(0,5);
  const x=X0+6, y0=YT-2, w=208;
  let g=`<g class="caja" data-abierta="${abierta?1:0}">`;
  g+=`<g class="cab"><rect x="${x}" y="${y0}" width="${w}" height="16" rx="4" fill="#0b0e13" fill-opacity="0.92" stroke="#2a3240"/>`;
  g+=`<text class="flecha" x="${x+6}" y="${y0+11.5}" font-size="10" fill="#9aa4b2">${abierta?'▾':'▸'}</text>`;
  let lx=x+18; for(const L of ['NQ','NDX','QQQ','TQQQ']){ g+=`<rect x="${lx}" y="${y0+4}" width="8" height="8" rx="1.5" fill="${LIBCOL[L]}"/><text x="${lx+11}" y="${y0+11.5}" font-size="9.5" fill="${LIBCOL[L]}">${L}</text>`; lx+= L.length*6+20; }
  g+=`</g><g class="cuerpo" style="display:${abierta?'inline':'none'}"><rect x="${x}" y="${y0+17}" width="${w}" height="${top.length*14+6}" rx="4" fill="#0b0e13" fill-opacity="0.88" stroke="#2a3240"/>`;
  let yy=y0+29; for(const t of top){ const i=mayor(t), dt=dSolo(deltaTxt(t));
    g+=`<text x="${x+6}" y="${yy}" font-size="10.5" fill="${LIBCOL[t.libro]}">${t.libro} ${rolesTxt(t)} ${fmtM(i.m)}</text>`;
    if(dt) g+=`<text x="${x+w-6}" y="${yy}" font-size="10.5" fill="${dColor(dt)}" text-anchor="end">${dt}</text>`; yy+=14; }
  return s+g+`</g></g>`;
}

V.push({t:'2A. Limpia: punto de color, sin conectores', d:'Un punto del color del libro, el LIBRO en negrita, el rol y el monto en gris claro y el cambio chico al final, solo si hubo cambio. Recuadro ABIERTO (clic en su titulo para cerrarlo).',
  f(){ let s=base2(); for(const it of separar(enR().map(g=>({g,yy:y(g.precio)})),15)){ const g=it.g, dt=dSolo(deltaTxt(g)), c=LIBCOL[g.libro];
      s+=`<circle cx="${XL}" cy="${it.yy}" r="3.2" fill="${c}"/>`; s=txt(s,XL+8,it.yy,g.libro,c,'start',11,'bold'); s=txt(s,XL+8+g.libro.length*6.9+5,it.yy,rolesTxt(g)+'  '+montosTxt(g).replace('+',''),'#cfd6e2','start',11);
      if(dt) s=txt(s,W-8,it.yy,dt,dColor(dt),'end',10); }
    return recuadro(fueraDeRango(s,G),true)+'</svg>'; }});
V.push({t:'2B. Pastillas de color', d:'Cada etiqueta es una pastilla con el color del libro de fondo, suave, y texto blanco. El cambio va en una pastilla chica verde o roja. Recuadro CERRADO.',
  f(){ let s=base2(0.7); for(const it of separar(enR().map(g=>({g,yy:y(g.precio)})),16)){ const g=it.g, dt=dSolo(deltaTxt(g)), c=LIBCOL[g.libro], t=g.libro+' '+rolesTxt(g)+' '+montosTxt(g).replace('+',''), w=t.length*6.6+12;
      s+=`<rect x="${XL}" y="${it.yy-7.5}" width="${w}" height="15" rx="7.5" fill="${c}" fill-opacity="0.28" stroke="${c}" stroke-width="0.8"/>`; s=txt(s,XL+6,it.yy,t,'#f2f5fa','start',10.8);
      if(dt){ const w2=dt.length*6.4+10; s+=`<rect x="${XL+w+5}" y="${it.yy-7}" width="${w2}" height="14" rx="7" fill="${dColor(dt)}" fill-opacity="0.2"/>`; s=txt(s,XL+w+10,it.yy,dt,dColor(dt),'start',10); } }
    return recuadro(fueraDeRango(s,G),false)+'</svg>'; }});
V.push({t:'2C. El monto primero, numeros alineados', d:'Los montos en columna, alineados (se comparan de un vistazo: verde positivo, rojo negativo) y despues LIBRO ROL. Recuadro ABIERTO.',
  f(){ let s=base2(); for(const it of separar(enR().map(g=>({g,yy:y(g.precio)})),15)){ const g=it.g, dt=dSolo(deltaTxt(g)), c=LIBCOL[g.libro], i=mayor(g);
      s=txt(s,XL+58,it.yy,i?fmtM(i.m):'·',i&&i.m<0?'#f39b9b':'#9fe0b0','end',11,'bold'); s=txt(s,XL+66,it.yy,g.libro+' '+rolesTxt(g),c,'start',11); if(dt) s=txt(s,W-8,it.yy,dt,dColor(dt),'end',10); }
    return recuadro(fueraDeRango(s,G),true)+'</svg>'; }});
V.push({t:'2D. Ultra minima: rol y monto en el color del libro', d:'Sin el nombre del libro: lo dice el color, y la leyenda esta en el titulo del recuadro. Solo C/P/M+/0Γ y el monto; el cambio como flecha sola. Recuadro CERRADO.',
  f(){ let s=base2(); for(const it of separar(enR().map(g=>({g,yy:y(g.precio)})),14)){ const g=it.g, dt=dSolo(deltaTxt(g)), c=LIBCOL[g.libro]; s=txt(s,XL,it.yy,rolesTxt(g)+' '+montosTxt(g).replace('+',''),c,'start',11.5,'bold');
      if(dt) s=txt(s,XL+150,it.yy,dt[0],dColor(dt),'start',12); }
    return recuadro(fueraDeRango(s,G),false)+'</svg>'; }});
V.push({t:'2E. Con barrita de peso', d:'Una barrita fina antes del texto: el largo dice cuanto pesa (escala logaritmica), verde si es positivo y roja si es negativo. Recuadro ABIERTO.',
  f(){ let s=base2(); const R=enR().filter(g=>mayor(g)); const mx=Math.max(...R.map(g=>Math.log10(1+Math.abs(mayor(g).m))));
    for(const it of separar(enR().map(g=>({g,yy:y(g.precio)})),15)){ const g=it.g, dt=dSolo(deltaTxt(g)), c=LIBCOL[g.libro], i=mayor(g);
      if(i){ const w=6+40*Math.log10(1+Math.abs(i.m))/mx; s+=`<rect x="${XL+46-w}" y="${it.yy-3}" width="${w}" height="6" rx="2" fill="${i.m<0?'#ef5350':'#26a69a'}" fill-opacity="0.85"/>`; }
      s=txt(s,XL+52,it.yy,g.libro+' '+rolesTxt(g)+' '+montosTxt(g).replace('+',''),c,'start',10.8); if(dt) s=txt(s,W-8,it.yy,dt,dColor(dt),'end',10); }
    return recuadro(fueraDeRango(s,G),true)+'</svg>'; }});
V.push({t:'2F. Jerarquia: las 4 que mas pesan grandes, el resto chico y gris', d:'De un golpe de vista se ven las 4 que mas pesan; las demas quedan en gris como referencia. Recuadro CERRADO.',
  f(){ let s=base2(); const top=new Set(enR().filter(g=>mayor(g)).sort((a,b)=>Math.abs(mayor(b).m)-Math.abs(mayor(a).m)).slice(0,4));
    for(const it of separar(enR().map(g=>({g,yy:y(g.precio)})),15)){ const g=it.g, dt=dSolo(deltaTxt(g)), c=LIBCOL[g.libro], big=top.has(g);
      s=txt(s,XL,it.yy,g.libro+' '+rolesTxt(g)+' '+montosTxt(g).replace('+',''),big?c:'#6f7a89','start',big?12.5:10,big?'bold':'normal'); if(dt&&big) s=txt(s,W-8,it.yy,dt,dColor(dt),'end',11,'bold'); }
    return recuadro(fueraDeRango(s,G),false)+'</svg>'; }});
V.push({t:'2G. Con la distancia chiquita en gris', d:'Igual que la 2, mas los puntos que faltan para llegar, en gris al final: sirve para ver si hay 20 pts de recorrido. Recuadro ABIERTO.',
  f(){ let s=base2(); for(const it of separar(enR().map(g=>({g,yy:y(g.precio)})),15)){ const g=it.g, dt=dSolo(deltaTxt(g)), c=LIBCOL[g.libro], dist=Math.round(g.precio-fut);
      s=txt(s,XL,it.yy,g.libro+' '+rolesTxt(g)+' '+montosTxt(g).replace('+',''),c,'start',11); if(dt) s=txt(s,XL+178,it.yy,dt,dColor(dt),'start',10);
      s=txt(s,W-8,it.yy,(dist>0?'+':dist<0?'−':'')+Math.abs(dist),'#6f7a89','end',10); }
    return recuadro(fueraDeRango(s,G),true)+'</svg>'; }});
V.push({t:'2H. Pegada al eje, sin cajas, letra fina', d:'La etiqueta arranca pegada al borde del grafico, sin cajas: lo mas parecido a como dibujan los profesionales. Recuadro CERRADO.',
  f(){ let s=base2(); for(const it of separar(enR().map(g=>({g,yy:y(g.precio)})),13)){ const g=it.g, dt=dSolo(deltaTxt(g)), c=LIBCOL[g.libro];
      if(Math.abs(it.yy-y(g.precio))>1) s=conector(s,X1,y(g.precio),X1+64,it.yy,c); s=txt(s,X1+66,it.yy,g.libro+' '+rolesTxt(g)+' '+montosTxt(g).replace('+','')+(dt?' '+dt:''),c,'start',10); }
    return recuadro(fueraDeRango(s,G),false)+'</svg>'; }});
V.push({t:'2I. Separadas por lado: arriba del precio en una columna, abajo en otra', d:'Las que estan ARRIBA del precio a un costado y las de ABAJO al otro: se ve el lado de un vistazo. Recuadro ABIERTO.',
  f(){ let s=base2(); const arr=enR().filter(g=>g.precio>fut), ab=enR().filter(g=>g.precio<=fut);
    s=txt(s,XL,YT-4,'arriba','#9aa4b2','start',9.5,'bold'); s=txt(s,XL+128,YT-4,'abajo','#9aa4b2','start',9.5,'bold');
    for(const it of separar(arr.map(g=>({g,yy:y(g.precio)})),14)){ const g=it.g, dt=dSolo(deltaTxt(g)); s=txt(s,XL,it.yy,g.libro+' '+rolesTxt(g)+' '+montosTxt(g).replace('+','')+(dt?' '+dt:''),LIBCOL[g.libro],'start',10); }
    for(const it of separar(ab.map(g=>({g,yy:y(g.precio)})),14)){ const g=it.g, dt=dSolo(deltaTxt(g)); s=txt(s,XL+128,it.yy,g.libro+' '+rolesTxt(g)+' '+montosTxt(g).replace('+','')+(dt?' '+dt:''),LIBCOL[g.libro],'start',10); }
    return recuadro(fueraDeRango(s,G),true)+'</svg>'; }});
