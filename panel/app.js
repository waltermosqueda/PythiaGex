/* LA WEB DE GAMMA HOY: une las fuentes (datos.js), la cuenta (nucleo.js) y el grafico (grafico.js)
 * y arma los tableros. Todo lo que muestra dice de donde salio y cuanto hace. */
(function () {
  "use strict";
  const $ = s => document.querySelector(s), $$ = s => Array.from(document.querySelectorAll(s));
  const q = new URLSearchParams(location.search);
  const CLAVE = "pythiagex.web.v1";
  const def = { inst: "MNQ", marco: "M1", vista: "futuro", tab: "mesa", nucleo: { horizonte: "Hoy", convexidad: "Auto", cuantas: 2, radioDomPct: 2.0, centroide: true, radioCentro: 12 },
                libro: "cboe", bandaPct: 0.08, pesadas: 2, vwap: "rueda", verPelotitas: true, verOi: true, verGuiones: true, tipoPerfil: "vol", zona: "America/Argentina/Buenos_Aires", velasVisibles: 180 };
  let aj = cargarAjustes();
  const base = q.get("base") || Datos.BASE_DEF;
  let datos = null, ocupado = false, temporizador = null;
  const grafico = new Grafico($("#lienzo"), { zona: aj.zona, velasVisibles: aj.velasVisibles });

  function cargarAjustes() {
    try { const g = JSON.parse(localStorage.getItem(CLAVE) || "{}"); return Object.assign({}, def, g, { nucleo: Object.assign({}, def.nucleo, g.nucleo || {}) }); } catch (e) { return JSON.parse(JSON.stringify(def)); }
  }
  function guardar() { try { localStorage.setItem(CLAVE, JSON.stringify(aj)); } catch (e) { } }
  function leer(obj, ruta) { return ruta.split(".").reduce((o, k) => o == null ? o : o[k], obj); }
  function poner(obj, ruta, v) { const ks = ruta.split("."); let o = obj; for (let i = 0; i < ks.length - 1; i++) o = o[ks[i]] = o[ks[i]] || {}; o[ks[ks.length - 1]] = v; }
  function esNum(x) { return typeof x === "number" && isFinite(x); }
  const dec = () => aj.inst === "MES" ? 2 : 2;
  const fP = (v, d) => v == null ? "—" : Number(v).toLocaleString("es-AR", { minimumFractionDigits: d == null ? 2 : d, maximumFractionDigits: d == null ? 2 : d });
  const fB = v => v == null ? "—" : (Math.abs(v) >= 1e9 ? (v / 1e9).toFixed(2) + " B" : (v / 1e6).toFixed(0) + " M");
  const hora = (t, seg) => new Date(typeof t === "number" ? t * 1000 : t).toLocaleTimeString("es-AR", { hour: "2-digit", minute: "2-digit", second: seg ? "2-digit" : undefined, hour12: false, timeZone: aj.zona });
  const precio = v => v == null ? null : (aj.vista === "indice" && datos && datos.L ? v - datos.L.base : v);
  const ptsObjetivo = () => aj.inst === "MES" ? 5 : 20;

  // ---------------------------------------------------------------- controles
  $$("#instrumentos button").forEach(b => b.onclick = () => { aj.inst = b.dataset.inst; marcar("#instrumentos", "inst", aj.inst); guardar(); refrescar(true); });
  $$("#marcos button").forEach(b => b.onclick = () => { aj.marco = b.dataset.marco; marcar("#marcos", "marco", aj.marco); guardar(); refrescar(true); });
  $$("#vistas button").forEach(b => b.onclick = () => { aj.vista = b.dataset.vista; marcar("#vistas", "vista", aj.vista); guardar(); pintar(); });
  $$("#pestanas button").forEach(b => b.onclick = () => { aj.tab = b.dataset.tab; marcar("#pestanas", "tab", aj.tab); $$(".vista").forEach(v => v.classList.toggle("on", v.dataset.vistaTab === aj.tab)); guardar(); pintar(); });
  function marcar(sel, clave, valor) { $$(sel + " button").forEach(b => b.classList.toggle("on", b.dataset[clave] === valor)); }
  marcar("#instrumentos", "inst", aj.inst); marcar("#marcos", "marco", aj.marco); marcar("#vistas", "vista", aj.vista); marcar("#pestanas", "tab", aj.tab);
  $$(".vista").forEach(v => v.classList.toggle("on", v.dataset.vistaTab === aj.tab));
  $("#btnAjustes").onclick = () => $("#ajustes").classList.add("on");
  $("#cerrarAjustes").onclick = () => $("#ajustes").classList.remove("on");
  $$("[data-aj]").forEach(el => {
    const v = leer(aj, el.dataset.aj); if (v != null) el.value = String(v);
    el.onchange = () => {
      let val = el.value; if (val === "true") val = true; else if (val === "false") val = false; else if (el.type === "number" || /^-?\d+(\.\d+)?$/.test(val) && !el.dataset.aj.endsWith("horizonte") && !el.dataset.aj.endsWith("zona")) val = parseFloat(val);
      poner(aj, el.dataset.aj, val); guardar(); grafico.op.zona = aj.zona; grafico.op.velasVisibles = aj.velasVisibles; refrescar(true);
    };
  });

  // ---------------------------------------------------------------- carga
  async function refrescar(rapido) {
    if (ocupado) return; ocupado = true;
    try { datos = await Datos.cargar(aj.inst, aj.marco, aj, base); pintar(); }
    catch (e) { console.error(e); $("#avisos").innerHTML = '<div class="aviso rojo">No pude cargar los datos: ' + (e.message || e) + '</div>'; }
    finally { ocupado = false; }
  }
  function programar() { clearInterval(temporizador); temporizador = setInterval(() => { if (document.visibilityState === "visible") refrescar(); }, 30000); }
  document.addEventListener("visibilitychange", () => { if (document.visibilityState === "visible") refrescar(); });
  window.addEventListener("resize", () => grafico.render());

  // ---------------------------------------------------------------- pintar
  function pintar() {
    if (!datos) return;
    const d = datos, L = d.L;
    chips(d);
    avisos(d);
    const niveles = L && !L.sinBase ? { zeroVol: L.zeroVol, zeroOi: L.zeroOi, mpVol: L.mpVol, mnVol: L.mnVol, doms: L.doms.map(x => ({ fut: x[0], gex: x[1] })), pesadas: L.pesadas || [], picoFut: L.picoFut } : {};
    Object.assign(grafico.op, { modoIndice: aj.vista === "indice", base: L && !L.sinBase ? L.base : 0, decimales: dec(), bandaPct: aj.bandaPct, verPelotitas: aj.verPelotitas, verOi: aj.verOi, verGuiones: aj.verGuiones, tipoPerfil: aj.tipoPerfil, zona: aj.zona });
    grafico.setDatos({ velas: d.velas, perfil: L && !L.sinBase ? L.perfil : [], niveles, marcas: d.marcas, cabecera: cabecera(d), futuro: d.futuro, vwap: d.vwap });
    lado(d);
    if (aj.tab === "strikes") strikes(d);
    if (aj.tab === "vencimientos") vencimientos(d);
    if (aj.tab === "historia") historia(d);
    if (aj.tab === "gatillos") gatillos(d);
    if (aj.tab === "auditoria") auditoria(d);
    if (aj.tab === "ayuda") ayuda();
  }

  function chips(d) {
    const cp = $("#chipPc"), cc = $("#chipCadena"), cv = $("#chipVelas");
    const edadPc = d.edadPc;
    if (d.vivoFresco) { cp.className = "chip vivo"; cp.textContent = "TU ATAS · vivo hace " + Math.round(edadPc || 0) + " min" + (d.pc && d.pc.version ? " · Gamma Hoy " + d.pc.version : ""); }
    else if (edadPc != null && edadPc < 24 * 60) { cp.className = "chip mal"; cp.textContent = "PC sin señal hace " + (edadPc < 90 ? Math.round(edadPc) + " min" : (edadPc / 60).toFixed(1) + " h") + " · sigo con la nube"; }
    else { cp.className = "chip mal"; cp.textContent = "PC apagada o sin subidor · modo nube"; }
    const c = d.cadena;
    if (c) { const ed = c.generado ? (Date.now() - c.generado.getTime()) / 60000 : null; cc.className = "chip " + (ed != null && ed < 20 ? "nube" : "mal"); cc.textContent = (d.usarViva ? "Rithmic ES " : "CBOE ") + (c.ts || "") + " UTC" + (ed != null ? " · bajada hace " + Math.round(ed) + " min" : "") + (d.usarViva ? "" : " · CBOE llega ~15 min tarde"); }
    else { cc.className = "chip mal"; cc.textContent = "sin cadena"; }
    if (d.velas.length) { const ult = d.velas[d.velas.length - 1]; const ed = (Date.now() / 1000 - ult.t) / 60; cv.className = "chip " + (d.vivoFresco ? "vivo" : "nube"); cv.textContent = (d.vivoFresco ? "velas de ATAS" : "velas de Yahoo (retraso)") + " · última " + hora(ult.t) + " (" + Math.round(ed) + " min)"; }
    else { cv.className = "chip mal"; cv.textContent = "sin velas"; }
  }

  function avisos(d) {
    const a = [];
    if (!d.vivoFresco) a.push('<div class="aviso">Tu PC no está mandando el vivo' + (d.edadPc != null ? ' (último latido hace ' + Math.round(d.edadPc) + ' min)' : '') + '. Estás viendo la <b>contingencia</b>: cadena de CBOE con ~15 min de retraso, velas de Yahoo con retraso y sin order flow, niveles calculados en la nube y en este navegador. Sirve para ubicarte; para operar al tick necesitás Rithmic.</div>');
    if (d.L && d.L.sinBase) a.push('<div class="aviso rojo">Sin base para convertir el índice a futuro: no se inventa una. Niveles en pausa.</div>');
    if (d.L && !d.L.sinBase && /CRUDA|TEORICA/.test(d.L.baseOrigen)) a.push('<div class="aviso">Base ' + d.L.baseOrigen + ': la medida de la cadena no pasó la cota del carry; los niveles pueden estar corridos unos puntos respecto del futuro.</div>');
    $("#avisos").innerHTML = a.join("");
  }

  function cabecera(d) {
    const L = d.L; if (!L || L.sinBase) return ["GAMMA HOY  sin cuenta (" + (L ? L.baseOrigen : "sin cadena") + ")"];
    const c = d.cadena; const ed = c && c.generado ? Math.round((Date.now() - c.generado.getTime()) / 60000) : null;
    const conv = L.convPrecio >= 0 ? "+" : "-";
    return [
      "GAMMA HOY  " + L.corto + "  " + L.cuadrante + "   conv " + conv + " (" + L.libroConv + ")  pico " + fP(precio(L.picoFut), 0) + (L.mucho ? " mucho" : ""),
      (d.usarViva ? "libro Rithmic ES vivo" : "CBOE " + (ed != null ? ed + " min tarde" : "")) + " · OI de ayer · base " + L.baseOrigen + " · dominantes por " + L.libroDom + " · " + d.origenVelas,
    ];
  }

  function lado(d) {
    const L = d.L, ex = d.ex, e = d.estado;
    const cl = ["", "q1", "q2", "q3", "q4"];
    let h = "";
    if (L && !L.sinBase) {
      const v0 = ex && ex.vencimientos.length ? ex.vencimientos[0] : null;
      h += '<div class="tarjeta"><h3>Régimen (cuadrante)</h3><div class="regimen ' + cl[L.q] + '">' + L.corto + '<small>' + L.cuadrante + '</small></div>' +
        '<div class="kv" style="margin-top:8px"><b>pico cerca del precio</b><span class="v">' + fP(precio(L.picoFut), 0) + ' · ' + fB(L.picoGex) + (L.mucho ? ' · mucho' : '') + '</span><b>convexidad en el precio</b><span class="v ' + (L.convPrecio >= 0 ? "pos" : "neg") + '">' + fB(L.convPrecio) + ' (' + L.libroConv + ')</span></div></div>';
      h += '<div class="tarjeta"><h3>Niveles (en ' + (aj.vista === "indice" ? "índice" : "futuro") + ')</h3><div class="kv">' +
        L.doms.map((x, i) => '<b class="dom">dominante ' + (i + 1) + (x[0] > L.futuro ? " (arriba)" : " (abajo)") + '</b><span class="v dom">' + fP(precio(x[0]), 2) + ' · ' + fB(x[1]) + '</span>').join("") +
        '<b>zero gamma (vol)</b><span class="v">' + fP(precio(L.zeroVol), 2) + '</span><b>zero gamma (OI)</b><span class="v t2">' + fP(precio(L.zeroOi), 2) + '</span>' +
        '<b class="pos">+Γ major (vol)</b><span class="v pos">' + fP(precio(L.mpVol), 2) + '</span><b class="neg">−Γ major (vol)</b><span class="v neg">' + fP(precio(L.mnVol), 2) + '</span>' +
        '<b>net GEX vol / OI</b><span class="v">' + fB(L.netVol) + ' / ' + fB(L.netOi) + '</span>' +
        (L.pesadas || []).map(p => '<b class="t2">barra pesada</b><span class="v t2">' + fP(precio(p.fut), 0) + ' · ' + fB(p.gexVol) + (p.dte < 1 ? " 0DTE" : "") + '</span>').join("") +
        '</div></div>';
      h += '<div class="tarjeta"><h3>Precio y base</h3><div class="kv"><b>futuro</b><span class="v">' + fP(d.futuro, 2) + '</span><b class="t2">origen</b><span class="v t2">' + d.futOrigen + '</span><b>índice (S)</b><span class="v">' + fP(L.S, 2) + '</span><b>base usada</b><span class="v">' + fP(L.base, 2) + '</span><b class="t2">origen</b><span class="v t2">' + L.baseOrigen + '</span>' +
        (L.carry != null ? '<b class="t2">carry teórico</b><span class="v t2">' + fP(L.carry, 2) + '</span>' : '') + (d.baseRueda != null ? '<b class="t2">base medida por precio</b><span class="v t2">' + fP(d.baseRueda, 2) + ' (hace ' + Math.round(d.edadRueda) + ' min)</span>' : '') + '</div></div>';
      if (v0) h += '<div class="tarjeta"><h3>0DTE (vence ' + v0.f + ')</h3><div class="kv"><b>GEX vol / OI</b><span class="v">' + fB(v0.gexVol) + ' / ' + fB(v0.gexOi) + '</span><b>movimiento esperado 1σ</b><span class="v">±' + fP(v0.em_1sigma, 1) + ' pts (IV atm ' + (v0.iv_atm != null ? (v0.iv_atm * 100).toFixed(1) + " %" : "—") + ')</span><b>put/call OI · vol</b><span class="v">' + (v0.pc_oi != null ? v0.pc_oi.toFixed(2) : "—") + ' · ' + (v0.pc_vol != null ? v0.pc_vol.toFixed(2) : "—") + '</span>' +
        (ex.muros_0dte.call_oi ? '<b class="pos">muro call (OI)</b><span class="v pos">' + fP(precio(ex.muros_0dte.call_oi.fut), 0) + '</span>' : '') + (ex.muros_0dte.put_oi ? '<b class="neg">muro put (OI)</b><span class="v neg">' + fP(precio(ex.muros_0dte.put_oi.fut), 0) + '</span>' : '') + (ex.maxpain_0dte ? '<b class="t2">max pain</b><span class="v t2">' + fP(precio(ex.maxpain_0dte.fut), 0) + '</span>' : '') + '</div></div>';
      if (L.mc && L.mc.length) h += '<div class="tarjeta"><h3>Max Change (nube)</h3><div class="kv">' + L.mc.map(m => '<b>' + m.min + ' min</b><span class="v">' + (m.fut != null ? fP(precio(m.fut), 0) + ' · ' + fB(m.delta) : "—") + '</span>').join("") + '</div></div>';
    } else h += '<div class="tarjeta"><h3>Cuenta</h3><div class="t2">' + (L ? L.baseOrigen : "sin cadena todavía") + '</div></div>';
    const gs = d.marcas.slice().sort((a, b) => b.t - a.t).slice(0, 12);
    h += '<div class="tarjeta"><h3>Disparos de hoy (' + d.marcas.length + ')</h3>' + (gs.length ? '<div class="gat">' + gs.map(m => '<span class="t2">' + hora(m.t) + '</span><span class="' + (m.lado > 0 ? "r" : "rn") + '">' + (m.lado > 0 ? "LARGO" : "CORTO") + '</span><span>' + m.tipo + '</span><span>' + fP(precio(m.precio), 2) + '</span>').join("") + '</div>' : '<div class="t3">ninguno todavía (los manda tu ATAS)</div>') + '</div>';
    $("#lado").innerHTML = h;
  }

  function strikes(d) {
    const L = d.L; if (!L || L.sinBase) { $("#strikes").innerHTML = '<div class="nota">Sin cuenta.</div>'; return; }
    const P = L.perfil.filter(s => Math.abs(s.fut - L.futuro) <= L.futuro * 0.03).sort((a, b) => b.K - a.K);
    const maxV = Math.max(1, ...P.map(s => Math.abs(s.gexVol))), maxO = Math.max(1, ...P.map(s => Math.abs(s.gexOi)));
    const barra = (v, m, col) => '<span class="mini" style="width:' + Math.round(Math.abs(v) / m * 60) + 'px;background:' + col + '"></span> ';
    let aquiPuesto = false;
    let h = '<div class="nota">Strikes a ±3 % del precio, de arriba hacia abajo. GEX = gamma × contratos × 100 × S² × 1 % (Black-Scholes, +call −put). Δ1/Δ5/Δ15: cambio del GEX por volumen contra 1, 5 y 15 min antes (las pelotitas), de la nube.</div>' +
      '<div class="tabla-caja"><table><thead><tr><th>strike</th><th>futuro</th><th>GEX vol</th><th>GEX OI</th><th>convexidad</th><th>OI c / p</th><th>vol hoy c / p</th><th>IV</th><th>vence</th><th>Δ1</th><th>Δ5</th><th>Δ15</th></tr></thead><tbody>';
    for (const s of P) {
      const aqui = !aquiPuesto && s.fut <= L.futuro; if (aqui) aquiPuesto = true;
      const a = s.antes || [];
      h += '<tr' + (aqui ? ' class="aqui"' : '') + '><td>' + fP(s.K, 0) + '</td><td>' + fP(precio(s.fut), 2) + '</td><td class="' + (s.gexVol >= 0 ? "pos" : "neg") + '">' + barra(s.gexVol, maxV, s.gexVol >= 0 ? "#3fbf7f" : "#e5484d") + fB(s.gexVol) + '</td><td class="t2">' + barra(s.gexOi, maxO, s.gexOi >= 0 ? "rgba(63,191,127,.45)" : "rgba(229,72,77,.45)") + fB(s.gexOi) + '</td><td class="' + (s.conv >= 0 ? "" : "neg") + '" style="color:' + (s.conv >= 0 ? "#9b7bff" : "") + '">' + fB(s.conv) + '</td><td>' + fP(s.oiC, 0) + ' / ' + fP(s.oiP, 0) + '</td><td>' + fP(s.volC, 0) + ' / ' + fP(s.volP, 0) + '</td><td>' + (s.ivW > 0 ? (s.ivSum / s.ivW * 100).toFixed(1) + " %" : "—") + '</td><td>' + (s.dte < 1 ? '<span class="dom">0DTE</span>' : s.dte.toFixed(1) + " d") + '</td>' +
        [0, 1, 2].map(k => { const v = a[k]; if (v == null) return '<td class="t3">—</td>'; const dd = s.gexVol - v; return '<td class="' + (dd >= 0 ? "pos" : "neg") + '">' + (dd >= 0 ? "+" : "") + fB(dd) + '</td>'; }).join("") + '</tr>';
    }
    $("#strikes").innerHTML = h + '</tbody></table></div>';
  }

  function vencimientos(d) {
    const ex = d.ex, L = d.L; if (!ex || !L || L.sinBase) { $("#vencimientos").innerHTML = '<div class="nota">Sin cuenta.</div>'; return; }
    const tot = ex.vencimientos.reduce((s, v) => s + Math.abs(v.gexVol), 0) || 1, totO = ex.vencimientos.reduce((s, v) => s + Math.abs(v.gexOi), 0) || 1;
    let h = '<div class="nota">Todos los vencimientos que trae la cadena (el indicador usa solo el horizonte elegido: ' + d.A.horizonte + '). EM 1σ = S × IV atm × √T: el movimiento de un desvío hasta ese vencimiento, según la volatilidad implícita. Muros y max pain: del vencimiento más cercano.</div>' +
      '<div class="tabla-caja" style="max-height:44vh"><table><thead><tr><th>vence</th><th>días</th><th>GEX vol</th><th>% del total</th><th>GEX OI</th><th>% OI</th><th>OI call / put</th><th>vol hoy call / put</th><th>P/C OI</th><th>P/C vol</th><th>IV atm</th><th>EM 1σ (pts)</th></tr></thead><tbody>';
    for (const v of ex.vencimientos) h += '<tr><td>' + v.f + (v.dias < 1 ? ' <span class="dom">0DTE</span>' : '') + '</td><td>' + v.dias.toFixed(2) + '</td><td class="' + (v.gexVol >= 0 ? "pos" : "neg") + '">' + fB(v.gexVol) + '</td><td>' + (Math.abs(v.gexVol) / tot * 100).toFixed(0) + ' %</td><td class="' + (v.gexOi >= 0 ? "pos" : "neg") + '">' + fB(v.gexOi) + '</td><td>' + (Math.abs(v.gexOi) / totO * 100).toFixed(0) + ' %</td><td>' + fP(v.oiC, 0) + ' / ' + fP(v.oiP, 0) + '</td><td>' + fP(v.volC, 0) + ' / ' + fP(v.volP, 0) + '</td><td>' + (v.pc_oi != null ? v.pc_oi.toFixed(2) : "—") + '</td><td>' + (v.pc_vol != null ? v.pc_vol.toFixed(2) : "—") + '</td><td>' + (v.iv_atm != null ? (v.iv_atm * 100).toFixed(1) + " %" : "—") + '</td><td>' + (v.em_1sigma != null ? "±" + fP(v.em_1sigma, 1) : "—") + '</td></tr>';
    h += '</tbody></table></div>';
    const m = ex.muros_0dte || {};
    h += '<div class="tres" style="margin-top:10px"><div class="tarjeta"><h3>Muros del 0DTE</h3><div class="kv">' +
      (m.call_oi ? '<b class="pos">muro call por OI</b><span class="v pos">' + fP(precio(m.call_oi.fut), 0) + ' (' + fP(m.call_oi.n, 0) + ')</span>' : '') + (m.put_oi ? '<b class="neg">muro put por OI</b><span class="v neg">' + fP(precio(m.put_oi.fut), 0) + ' (' + fP(m.put_oi.n, 0) + ')</span>' : '') +
      (m.call_vol ? '<b class="pos">muro call por volumen</b><span class="v pos">' + fP(precio(m.call_vol.fut), 0) + ' (' + fP(m.call_vol.n, 0) + ')</span>' : '') + (m.put_vol ? '<b class="neg">muro put por volumen</b><span class="v neg">' + fP(precio(m.put_vol.fut), 0) + ' (' + fP(m.put_vol.n, 0) + ')</span>' : '') +
      (ex.maxpain_0dte ? '<b class="t2">max pain</b><span class="v t2">' + fP(precio(ex.maxpain_0dte.fut), 0) + '</span>' : '') + '<b class="t2">put/call OI · vol (toda la cadena)</b><span class="v t2">' + (ex.pc_oi != null ? ex.pc_oi.toFixed(2) : "—") + ' · ' + (ex.pc_vol != null ? ex.pc_vol.toFixed(2) : "—") + '</span></div></div>' +
      '<div class="tarjeta" style="grid-column:span 2"><h3>Sonrisa de IV del 0DTE (call y put por strike)</h3><canvas class="chico" id="cvSonrisa"></canvas></div></div>';
    $("#vencimientos").innerHTML = h;
    lineas($("#cvSonrisa"), (ex.sonrisa_0dte || []).map(s => s[0]), [{ y: (ex.sonrisa_0dte || []).map(s => s[1] != null ? s[1] * 100 : null), col: "#3fbf7f", nombre: "IV call %" }, { y: (ex.sonrisa_0dte || []).map(s => s[2] != null ? s[2] * 100 : null), col: "#e5484d", nombre: "IV put %" }], { xEs: "strike", marcaX: L.S });
  }

  function historia(d) {
    const s = d.serie || [];
    const conNiv = d.velas.filter(v => v.niv);
    let h = '<div class="nota">La serie de la nube (una línea por corrida, cada minuto en la rueda): net GEX por volumen y por OI, y los niveles contra el precio. ' + (d.vivoFresco ? 'Los niveles por vela vienen de tu ATAS.' : 'Los niveles por vela vienen de la nube.') + '</div>' +
      '<div class="dos"><div class="tarjeta"><h3>Net GEX del día (vol y OI, en B)</h3><canvas class="chico" id="cvNet"></canvas></div><div class="tarjeta"><h3>Zero gamma y dominantes contra el precio</h3><canvas class="chico" id="cvNiv"></canvas></div>' +
      '<div class="tarjeta"><h3>Movimiento esperado del 0DTE (1σ, pts)</h3><canvas class="chico" id="cvEm"></canvas></div><div class="tarjeta"><h3>Cuadrante a lo largo del día</h3><canvas class="chico" id="cvQ"></canvas></div></div>';
    $("#historia").innerHTML = h;
    const ts = s.map(x => new Date(x.t).getTime() / 1000);
    lineas($("#cvNet"), ts, [{ y: s.map(x => x.netVol / 1e9), col: "#3fbf7f", nombre: "net vol" }, { y: s.map(x => x.netOi / 1e9), col: "#8ab4dc", nombre: "net OI" }], { cero: true });
    const vs = d.velas;
    lineas($("#cvNiv"), vs.map(v => v.t), [{ y: vs.map(v => precio(v.c)), col: "#dfe6ee", nombre: "cierre" }, { y: vs.map(v => v.niv ? precio(v.niv.zero_vol) : null), col: "#a9b4c0", nombre: "zero" }, { y: vs.map(v => v.niv ? precio(v.niv.dom0) : null), col: "#f2c14e", nombre: "dom 1" }, { y: vs.map(v => v.niv ? precio(v.niv.dom1) : null), col: "#c9a03a", nombre: "dom 2" }], {});
    lineas($("#cvEm"), ts, [{ y: s.map(x => x.em0), col: "#9b7bff", nombre: "EM 1σ" }], {});
    lineas($("#cvQ"), ts, [{ y: s.map(x => x.q), col: "#f2c14e", nombre: "cuadrante (1 imán, 2 explosivo, 3 estable, 4 riesgo)", escalon: true }], { yMin: 0.5, yMax: 4.5 });
  }

  function gatillos(d) {
    const G = ptsObjetivo();
    const ms = d.marcas.slice().sort((a, b) => a.t - b.t);
    let h = '<div class="nota">Los disparos que tu ATAS registró hoy en ' + d.inst + ' (R = rebote en una raya, M = modelo, tren = order flow en la banda). Resultado: desde el cierre de la vela del disparo, ¿llegó a +' + G + ' antes que a −' + G + ' en 20 velas? MFE / MAE = lo más que fue a favor / en contra. Sin salidas gestionadas: es la medida cruda del laboratorio.</div>';
    if (!ms.length) { $("#gatillos").innerHTML = h + '<div class="tarjeta t3">Ningún disparo todavía. Aparecen cuando tu ATAS está abierto y el subidor manda el vivo.</div>'; return; }
    const velas = d.velas; const idxDe = new Map(velas.map((v, i) => [v.t, i]));
    const dur = velas.length > 1 ? velas[1].t - velas[0].t : 60;
    let g = 0, p = 0, ab = 0;
    h += '<div class="tabla-caja"><table><thead><tr><th>hora</th><th>tipo</th><th>lado</th><th>entrada</th><th>nivel</th><th>toque nº / p</th><th>MFE</th><th>MAE</th><th>+' + G + '/−' + G + '</th></tr></thead><tbody>';
    for (const m of ms) {
      let i = idxDe.get(m.t - dur); if (i == null) i = idxDe.get(m.t);
      let mfe = 0, mae = 0, res = null;
      if (i != null) for (let j = i + 1; j < Math.min(velas.length, i + 21); j++) { const w = velas[j]; const fav = m.lado > 0 ? w.h - m.precio : m.precio - w.l, con = m.lado > 0 ? m.precio - w.l : w.h - m.precio; mfe = Math.max(mfe, fav); mae = Math.max(mae, con); if (res === null) { if (fav >= G && con >= G) res = false; else if (fav >= G) res = true; else if (con >= G) res = false; } }
      if (res === true) g++; else if (res === false) p++; else ab++;
      h += '<tr><td>' + hora(m.t) + '</td><td>' + m.tipo + '</td><td class="' + (m.lado > 0 ? "pos" : "neg") + '">' + (m.lado > 0 ? "LARGO" : "CORTO") + '</td><td>' + fP(precio(m.precio), 2) + '</td><td>' + fP(precio(m.dom), 2) + '</td><td>' + (m.dz != null ? (String(m.tipo).startsWith("modelo") ? m.dz.toFixed(2) : m.dz) : "") + '</td><td class="pos">' + mfe.toFixed(1) + '</td><td class="neg">' + mae.toFixed(1) + '</td><td>' + (res === true ? '<span class="pos">GANA</span>' : res === false ? '<span class="neg">pierde</span>' : '<span class="t3">abierto</span>') + '</td></tr>';
    }
    h += '</tbody></table></div><div class="nota">Hoy: ' + g + ' ganaron, ' + p + ' perdieron, ' + ab + ' abiertos (' + (g + p ? Math.round(100 * g / (g + p)) : 0) + ' % a 1:1). Recordá lo medido en 16 días: el rebote en las rayas rinde igual que una raya inventada.</div>';
    $("#gatillos").innerHTML = h;
  }

  function auditoria(d) {
    const L = d.L, e = d.estado, pc = d.pc, c = d.cadena;
    let h = '<div class="nota">Tres cuentas de lo mismo: este navegador (JS), la nube (Python) y tu ATAS (C#). Cada una con su hora, su precio del futuro y su base: se comparan en STRIKE (K = futuro − base) para que la base no confunda. Diferencias de 1 o 2 puntos en el zero son normales (hora y precio distintos); una dominante en otro strike no lo es.</div>';
    const filaK = (nombre, txt, base) => {
      if (!txt) return '<tr><td>' + nombre + '</td><td colspan="6" class="t3">sin dato</td></tr>';
      const g = k => { const m = txt.match(new RegExp(k + "=(-?[0-9.]+|NaN)")); return m && m[1] !== "NaN" ? +m[1] : null; };
      const b = g("base"), k = v => v == null || b == null ? "—" : fP(v - b, 1);
      const doms = (txt.match(/doms=([^ ]+)/) || [])[1] || "";
      const dk = doms.split("/").filter(Boolean).map(x => { const f = +x.split("=")[0]; return b == null ? "—" : fP(f - b, 1) + " (" + x.split("=")[1] + ")"; }).join(" / ");
      const t = (txt.match(/^(\S+)\s+AUDIT/) || [])[1] || "";
      return '<tr><td>' + nombre + '<br><span class="t3">' + t + '</span></td><td>' + fP(g("fut"), 2) + '</td><td>' + fP(b, 2) + '</td><td>' + k(g("zeroVol")) + '</td><td>' + k(g("mpVol")) + ' / ' + k(g("mnVol")) + '</td><td>' + dk + '</td><td>q' + (g("q") || "?") + ' · ' + (txt.match(/netVol=([^ ]+)/) || [])[1] + '</td></tr>';
    };
    h += '<div class="tabla-caja" style="max-height:none"><table><thead><tr><th>cuenta</th><th>futuro</th><th>base</th><th>zero vol (K)</th><th>majors +Γ / −Γ (K)</th><th>dominantes (K)</th><th>cuadrante · net vol</th></tr></thead><tbody>' +
      filaK("JS (este navegador, ahora)", L && !L.sinBase ? Nucleo.audit(L, c) : null) + filaK("NUBE (estado_nube.py)", e ? (e.generado + " " + e.audit) : null) + filaK("ATAS (tu indicador, último AUDIT de " + d.raiz + ")", pc && (pc["audit_" + d.raiz] || pc.audit) ? (pc["audit_" + d.raiz] || pc.audit) : null) + '</tbody></table></div>';
    h += '<h3 style="margin:14px 0 6px;font:600 11px var(--sans);letter-spacing:.1em;text-transform:uppercase;color:var(--t3)">Líneas completas</h3>' +
      '<pre>JS    ' + (L && !L.sinBase ? Nucleo.audit(L, c) : "—") + '</pre><pre>NUBE  ' + (e ? e.generado + "  " + e.audit : "—") + '</pre><pre>ATAS  ' + (pc && (pc["audit_" + d.raiz] || pc.audit) ? (pc["audit_" + d.raiz] || pc.audit) : "—") + '</pre>';
    // fuentes
    const ed = iso => { const m = Datos.edadMin(iso); return m == null ? "—" : m < 90 ? Math.round(m) + " min" : (m / 60).toFixed(1) + " h"; };
    const y = d.yahoo && d.yahoo.futuro && d.yahoo.futuro.t && d.yahoo.futuro.t.length ? d.yahoo.futuro : null;
    h += '<div class="tabla-caja" style="max-height:none;margin-top:10px"><table><thead><tr><th>fuente</th><th>qué trae</th><th>sello</th><th>edad</th></tr></thead><tbody>' +
      '<tr><td>CBOE (cadena SPX/NDX)</td><td>strikes, OI de ayer, IV, volumen de hoy · llega ~15 min tarde (medido 902 s)</td><td>' + (c ? c.ts + " UTC" : "—") + '</td><td>' + (c && c.generado ? ed(c.generado.toISOString()) : "—") + '</td></tr>' +
      '<tr><td>Nube (estado_nube.py)</td><td>la cuenta del indicador en Python, cada minuto en GitHub Actions</td><td>' + (e ? e.generado : "—") + '</td><td>' + (e ? ed(e.generado) : "—") + '</td></tr>' +
      '<tr><td>Yahoo (velas del futuro)</td><td>' + (y ? y.simbolo + " · " + (y.nombre || "") + " · con retraso, sin delta" : "—") + '</td><td>' + (y ? hora(y.t[y.t.length - 1], true) : "—") + '</td><td>' + (y ? Math.round((Date.now() / 1000 - y.t[y.t.length - 1]) / 60) + " min" : "—") + '</td></tr>' +
      '<tr><td>Tu PC (latido)</td><td>' + (pc ? "Gamma Hoy " + (pc.version || "?") + " en " + (pc.pc || "?") + " · subió " + Object.keys(pc.subidos || {}).join(", ") : "—") + '</td><td>' + (pc ? pc.generado : "—") + '</td><td>' + (pc ? ed(pc.generado) : "—") + '</td></tr>' +
      '<tr><td>Rithmic ES (cadena viva)</td><td>' + (d.viva ? "0DTE con puntas reales, " + (d.viva.filas || []).length + " filas, " + (d.viva.grandes || 0) + " grandes" : "no llega (solo con tu ATAS abierto)") + '</td><td>' + (d.viva ? d.viva.ts + " UTC" : "—") + '</td><td>' + (d.viva ? ed(d.viva.generado) : "—") + '</td></tr>' +
      '</tbody></table></div>';
    if (e && L && !L.sinBase) {
      const m = new Map(e.perfil.map(s => [s.K, s])); let n = 0, peor = 0, pk = null;
      for (const s of L.perfil) { const p = m.get(s.K); if (!p) continue; n++; const dd = Math.abs(s.gexVol - p.gexVol) / Math.max(1e6, Math.abs(p.gexVol)); if (dd > peor) { peor = dd; pk = s.K; } }
      h += '<div class="nota">Perfil por strike, JS contra nube: ' + n + ' strikes comparados; peor diferencia relativa ' + (peor * 100).toFixed(2) + ' % en K ' + pk + ' (el precio del futuro y la hora difieren un poco entre las dos; con el mismo precio y hora dan lo mismo: ver pruebas.html).</div>';
    }
    $("#auditoria").innerHTML = h;
  }

  function ayuda() {
    $("#ayuda").innerHTML = [
      ["Dominante", "El strike con más GEX (por volumen de hoy) de cada lado del precio, dentro del 2 %; el centroide lo corre unos puntos hacia donde hay más gamma alrededor.", "La raya amarilla: donde más plata de opciones hay parada cerca. Suele frenar, pero medido en 16 días rinde igual que una raya inventada."],
      ["Zero gamma", "El precio donde la suma de GEX de la cadena cruza cero (grilla ±3 %, interpolado). Por volumen (línea) y por OI (puntitos).", "Arriba del zero los dealers frenan el precio; abajo lo empujan. Es un termómetro de régimen, no una dirección."],
      ["Majors +Γ / −Γ", "El strike con el GEX positivo más grande y el negativo más grande, del libro de volumen.", "Los dos muros más grandes del día, uno de cada signo."],
      ["Cuadrante", "Pico de GEX cerca del precio (≥ 50 % del máximo = 'mucho') × convexidad en el precio: IMÁN, EXPLOSIVO, ESTABLE, RIESGO.", "Imán: el precio va y vuelve a la raya. Explosivo: si la rompe, se va. Estable: rangos amplios. Riesgo: tendencia, tamaño chico."],
      ["Convexidad", "Cuánto cambia el GEX de cada strike si el precio sube 1 % (la escalera violeta).", "Si es positiva, la raya se refuerza cuando el precio sube; si es negativa, se debilita."],
      ["Pelotitas (Max Change)", "La punta de cada barra hace 15, 5 y 1 minuto (grande, mediana, chica). Adentro de la barra = la barra creció; afuera = se achicó.", "Ver de dónde viene la barra: si las tres están adentro, entró plata; si están afuera, se está yendo."],
      ["Barras pesadas", "Las barras con más |GEX| a ±0,6 % del precio, con raya punteada.", "Lo pesado que tenés pegado al precio, aunque no sea la dominante."],
      ["0DTE y EM 1σ", "Las opciones que vencen hoy. EM 1σ = S × IV atm × √T, el movimiento de un desvío hasta el vencimiento.", "Cuánto se espera que se mueva el índice hoy, según lo que pagan las opciones."],
      ["Muros por OI / max pain", "Muro call = strike con más OI de calls del 0DTE; muro put, ídem puts. Max pain = strike donde vence menos plata.", "Referencias que usan otros tableros (SpotGamma, MenthorQ); acá están para comparar, no están medidas."],
      ["Base", "Futuro − índice. Medida (paridad de opciones) > medida por precio > cruda > carry teórico (tasa − dividendo × tiempo). Siempre acotada con el carry.", "Los strikes son del índice; para dibujarlos en el futuro hay que sumarles la base. Si la base está mal, todo se corre."],
      ["Gatillos R / M / tren", "R: rebote en una raya (1.8). M: modelo logístico de ES a 10 min (1.7). tren: tres deltas en contra en la banda (1.3). Todos se registran para juzgarlos con días nuevos.", "Marcas de lo que el indicador vio. Ninguna está probada como ventaja; el R rinde igual que el placebo."],
      ["VIVO / NUBE", "VIVO: tu ATAS manda cada minuto velas con order flow, niveles y disparos. NUBE: GitHub baja la cadena de CBOE y calcula lo mismo; velas de Yahoo con retraso.", "Si tu PC se apaga, la web sigue sola con la nube. Lo dice arriba, con la edad de cada dato."],
    ].map(x => '<h4>' + x[0] + '</h4><p>' + x[1] + '</p><p class="criollo">' + x[2] + '</p>').join("");
  }

  /* graficos chicos de lineas: x = tiempos (s) o strikes; series [{y:[], col, nombre, escalon}] */
  function lineas(cv, xs, series, op) {
    if (!cv) return; const dpr = window.devicePixelRatio || 1; const W = cv.clientWidth, H = cv.clientHeight; cv.width = W * dpr; cv.height = H * dpr;
    const ctx = cv.getContext("2d"); ctx.setTransform(dpr, 0, 0, dpr, 0, 0); ctx.fillStyle = "#0b0f14"; ctx.fillRect(0, 0, W, H);
    const pts = []; series.forEach(s => s.y.forEach((v, i) => { if (v != null && isFinite(v) && xs[i] != null) pts.push([xs[i], v]); }));
    if (!pts.length) { ctx.fillStyle = "#5f6d7b"; ctx.font = "11px JetBrains Mono, monospace"; ctx.fillText("sin datos todavía", 10, 20); return; }
    let xmin = Math.min(...pts.map(p => p[0])), xmax = Math.max(...pts.map(p => p[0])), ymin = op.yMin ?? Math.min(...pts.map(p => p[1])), ymax = op.yMax ?? Math.max(...pts.map(p => p[1]));
    if (op.cero) { ymin = Math.min(ymin, 0); ymax = Math.max(ymax, 0); }
    if (xmax === xmin) xmax = xmin + 1; if (ymax === ymin) { ymax += 1; ymin -= 1; } const pad = (ymax - ymin) * 0.08; ymin -= pad; ymax += pad;
    const m = { l: 52, r: 10, t: 10, b: 22 };
    const X = x => m.l + (x - xmin) / (xmax - xmin) * (W - m.l - m.r), Y = y => H - m.b - (y - ymin) / (ymax - ymin) * (H - m.t - m.b);
    ctx.strokeStyle = "#1f2a36"; ctx.fillStyle = "#8a97a6"; ctx.font = "10px JetBrains Mono, monospace"; ctx.textAlign = "right";
    for (let i = 0; i <= 4; i++) { const y = ymin + (ymax - ymin) * i / 4; ctx.beginPath(); ctx.moveTo(m.l, Y(y)); ctx.lineTo(W - m.r, Y(y)); ctx.stroke(); ctx.fillText(y.toLocaleString("es-AR", { maximumFractionDigits: Math.abs(ymax - ymin) < 10 ? 2 : 0 }), m.l - 4, Y(y) + 3); }
    if (op.cero && ymin < 0 && ymax > 0) { ctx.strokeStyle = "#5f6d7b"; ctx.beginPath(); ctx.moveTo(m.l, Y(0)); ctx.lineTo(W - m.r, Y(0)); ctx.stroke(); }
    ctx.textAlign = "center";
    for (let i = 0; i <= 4; i++) { const x = xmin + (xmax - xmin) * i / 4; ctx.fillText(op.xEs === "strike" ? x.toFixed(0) : hora(x), X(x), H - 6); }
    if (op.marcaX != null) { ctx.strokeStyle = "#f2c14e"; ctx.setLineDash([3, 3]); ctx.beginPath(); ctx.moveTo(X(op.marcaX), m.t); ctx.lineTo(X(op.marcaX), H - m.b); ctx.stroke(); ctx.setLineDash([]); }
    series.forEach((s, k) => {
      ctx.strokeStyle = s.col; ctx.lineWidth = 1.4; ctx.beginPath(); let abierto = false, yAnt = null;
      s.y.forEach((v, i) => { if (v == null || !isFinite(v) || xs[i] == null) { abierto = false; return; } const x = X(xs[i]), y = Y(v); if (!abierto) { ctx.moveTo(x, y); abierto = true; } else { if (s.escalon && yAnt != null) ctx.lineTo(x, yAnt); ctx.lineTo(x, y); } yAnt = y; });
      ctx.stroke();
      ctx.fillStyle = s.col; ctx.textAlign = "left"; ctx.fillText(s.nombre, m.l + 6 + k * 90, m.t + 10);
    });
  }

  // ---------------------------------------------------------------- arranque
  refrescar().then(programar);
})();
