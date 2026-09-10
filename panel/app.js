/* LA WEB DE GAMMA HOY: une las fuentes (datos.js), la cuenta (nucleo.js) y el grafico (grafico.js)
 * y arma los tableros, todos en la misma pagina. Todo lo que muestra dice de donde salio y cuanto hace. */
(function () {
  "use strict";
  const $ = s => document.querySelector(s), $$ = s => Array.from(document.querySelectorAll(s));
  const q = new URLSearchParams(location.search);
  const CLAVE = "pythiagex.web.v2";
  const def = { inst: "MNQ", marco: "M1", vista: "futuro", nucleo: { horizonte: "Hoy", convexidad: "Auto", cuantas: 2, radioDomPct: 2.0, centroide: true, radioCentro: 12 },
                libro: "cboe", bandaPct: 0.08, pesadas: 2, vwap: "rueda", verPelotitas: true, verOi: true, verGuiones: true, tipoPerfil: "vol", zona: "America/Argentina/Buenos_Aires", velasVisibles: 180, refresco: 15 };
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
  const fP = (v, d) => v == null ? "—" : Number(v).toLocaleString("es-AR", { minimumFractionDigits: d == null ? 2 : d, maximumFractionDigits: d == null ? 2 : d });
  const fB = v => v == null ? "—" : (Math.abs(v) >= 1e9 ? (v / 1e9).toFixed(2) + " B" : (v / 1e6).toFixed(0) + " M");
  const hora = (t, seg) => new Date(typeof t === "number" ? t * 1000 : t).toLocaleTimeString("es-AR", { hour: "2-digit", minute: "2-digit", second: seg ? "2-digit" : undefined, hour12: false, timeZone: aj.zona });
  const precio = v => v == null ? null : (aj.vista === "indice" && datos && datos.L ? v - datos.L.base : v);
  const ptsObjetivo = () => aj.inst === "MES" ? 5 : 20;
  const cl = ["", "q1", "q2", "q3", "q4"];

  // ---------------------------------------------------------------- controles
  $$("#instrumentos button").forEach(b => b.onclick = () => { aj.inst = b.dataset.inst; marcar("#instrumentos", "inst", aj.inst); guardar(); refrescar(); });
  $$("#marcos button").forEach(b => b.onclick = () => { aj.marco = b.dataset.marco; marcar("#marcos", "marco", aj.marco); guardar(); refrescar(); });
  $$("#vistas button").forEach(b => b.onclick = () => { aj.vista = b.dataset.vista; marcar("#vistas", "vista", aj.vista); guardar(); pintar(); });
  function marcar(sel, clave, valor) { $$(sel + " button").forEach(b => b.classList.toggle("on", b.dataset[clave] === valor)); }
  marcar("#instrumentos", "inst", aj.inst); marcar("#marcos", "marco", aj.marco); marcar("#vistas", "vista", aj.vista);
  $("#zIn").onclick = () => grafico.zoomTiempo(0.8); $("#zOut").onclick = () => grafico.zoomTiempo(1.25);
  $("#zIzq").onclick = () => grafico.mover(-Math.round(grafico.vista.n / 3)); $("#zDer").onclick = () => grafico.mover(Math.round(grafico.vista.n / 3));
  $("#zAuto").onclick = () => grafico.autoEscala(); $("#zReset").onclick = () => grafico.reset();
  $("#zPIn").onclick = () => grafico.zoomPrecio(0.8); $("#zPOut").onclick = () => grafico.zoomPrecio(1.25); $("#zCentrar").onclick = () => grafico.centrar();
  $("#btnAjustes").onclick = () => $("#ajustes").classList.add("on");
  $("#cerrarAjustes").onclick = () => $("#ajustes").classList.remove("on");
  $$("[data-aj]").forEach(el => {
    const v = leer(aj, el.dataset.aj); if (v != null) el.value = String(v);
    el.onchange = () => {
      let val = el.value; if (val === "true") val = true; else if (val === "false") val = false; else if (el.type === "number" || (/^-?\d+(\.\d+)?$/.test(val) && !el.dataset.aj.endsWith("horizonte") && !el.dataset.aj.endsWith("zona"))) val = parseFloat(val);
      poner(aj, el.dataset.aj, val); guardar(); grafico.op.zona = aj.zona; grafico.op.velasVisibles = aj.velasVisibles; programar(); refrescar();
    };
  });
  document.addEventListener("keydown", e => {
    if (e.target && /input|select|textarea/i.test(e.target.tagName)) return;
    if (e.key === "+" || e.key === "=") grafico.zoomTiempo(0.8); else if (e.key === "-") grafico.zoomTiempo(1.25);
    else if (e.key === "ArrowLeft") grafico.mover(-10); else if (e.key === "ArrowRight") grafico.mover(10); else if (e.key === "ArrowUp") grafico.moverPrecio(-20); else if (e.key === "ArrowDown") grafico.moverPrecio(20); else if (e.key === "Home" || e.key === "0") grafico.reset(); else if (e.key === "c") grafico.centrar();
  });

  // ---------------------------------------------------------------- carga
  async function refrescar() {
    if (ocupado) return; ocupado = true;
    try { datos = await Datos.cargar(aj.inst, aj.marco, aj, base); pintar(); }
    catch (e) { console.error(e); $("#avisos").innerHTML = '<div class="aviso rojo">No pude cargar los datos: ' + (e.message || e) + '</div>'; }
    finally { ocupado = false; }
  }
  function programar() { clearInterval(temporizador); temporizador = setInterval(() => { if (document.visibilityState === "visible") refrescar(); }, Math.max(10, aj.refresco || 15) * 1000); }
  document.addEventListener("visibilitychange", () => { if (document.visibilityState === "visible") refrescar(); });
  window.addEventListener("resize", () => { grafico.render(); if (datos) historia(datos); });
  setInterval(() => { if (datos) chips(datos); }, 5000);   // las edades avanzan aunque no llegue nada nuevo

  // ---------------------------------------------------------------- pintar
  function pintar() {
    if (!datos) return;
    const d = datos, L = d.L;
    chips(d); avisos(d);
    const niveles = L && !L.sinBase ? { zeroVol: L.zeroVol, zeroOi: L.zeroOi, mpVol: L.mpVol, mnVol: L.mnVol, doms: L.doms.map(x => ({ fut: x[0], gex: x[1] })), pesadas: L.pesadas || [], picoFut: L.picoFut } : {};
    Object.assign(grafico.op, { modoIndice: aj.vista === "indice", base: L && !L.sinBase ? L.base : 0, decimales: 2, bandaPct: aj.bandaPct, verPelotitas: aj.verPelotitas, verOi: aj.verOi, verGuiones: aj.verGuiones, tipoPerfil: aj.tipoPerfil, zona: aj.zona });
    grafico.setDatos({ velas: d.velas, perfil: L && !L.sinBase ? L.perfil : [], niveles, marcas: d.marcas, cabecera: cabecera(d), futuro: d.futuro, vwap: d.vwap,
                       info: L && !L.sinBase ? { netVol: L.netVol, netOi: L.netOi, mc: L.mc || [] } : null });
    lado(d); rapida(d); vencimientos(d); historia(d); gatillos(d); auditoria(d); strikes(d);
    if (!$("#ayuda").innerHTML) ayuda();
  }

  function chips(d) {
    const cp = $("#chipPc"), cc = $("#chipCadena"), cv = $("#chipVelas");
    const edadPc = d.pc ? Datos.edadMin(d.pc.generado) : null;
    const seg = m => m == null ? "—" : m < 2 ? Math.round(m * 60) + " s" : Math.round(m) + " min";
    if (d.vivoFresco) { cp.className = "chip vivo"; cp.textContent = "TU ATAS · vivo hace " + seg(edadPc) + (d.pc && d.pc.version ? " · Gamma Hoy " + d.pc.version : ""); }
    else if (edadPc != null && edadPc < 24 * 60) { cp.className = "chip mal"; cp.textContent = "PC sin señal hace " + (edadPc < 90 ? Math.round(edadPc) + " min" : (edadPc / 60).toFixed(1) + " h") + " · sigo con la nube"; }
    else { cp.className = "chip mal"; cp.textContent = "PC apagada o sin subidor · modo nube"; }
    const c = d.cadena;
    if (c) { const ed = c.generado ? (Date.now() - c.generado.getTime()) / 60000 : null; cc.className = "chip " + (ed != null && ed < 20 ? "nube" : "mal"); cc.textContent = (d.usarViva ? "Rithmic ES " : "CBOE ") + (c.ts || "").slice(11) + " UTC" + (ed != null ? " · bajada hace " + Math.round(ed) + " min" : "") + (d.usarViva ? "" : " · llega ~15 min tarde"); }
    else { cc.className = "chip mal"; cc.textContent = "sin cadena"; }
    if (d.velas.length) { const ult = d.velas[d.velas.length - 1]; const ed = (Date.now() / 1000 - ult.t) / 60; cv.className = "chip " + (d.vivoFresco ? "vivo" : "nube"); cv.textContent = (d.vivoFresco ? "velas de ATAS" : "velas de Yahoo (retraso)") + " · última " + hora(ult.t) + " (" + seg(ed) + ")"; }
    else { cv.className = "chip mal"; cv.textContent = "sin velas"; }
  }

  function avisos(d) {
    const a = [];
    if (!d.vivoFresco) a.push('<div class="aviso">Tu PC no está mandando el vivo' + (d.pc ? ' (último latido hace ' + Math.round(Datos.edadMin(d.pc.generado)) + ' min)' : '') + '. Estás viendo la <b>contingencia</b>: cadena de CBOE con ~15 min de retraso, velas de Yahoo con retraso y sin order flow, niveles calculados en la nube y en este navegador. Sirve para ubicarte; para operar al tick necesitás Rithmic.</div>');
    if (d.L && d.L.sinBase) a.push('<div class="aviso rojo">Sin base para convertir el índice a futuro: no se inventa una. Niveles en pausa.</div>');
    if (d.L && !d.L.sinBase && /CRUDA|TEORICA/.test(d.L.baseOrigen)) a.push('<div class="aviso">Base ' + d.L.baseOrigen + ': la medida de la cadena no pasó la cota del carry; los niveles pueden estar corridos unos puntos respecto del futuro.</div>');
    $("#avisos").innerHTML = a.join("");
  }

  function cabecera(d) {
    const L = d.L; if (!L || L.sinBase) return ["GAMMA HOY  sin cuenta (" + (L ? L.baseOrigen : "sin cadena") + ")"];
    const c = d.cadena; const ed = c && c.generado ? Math.round((Date.now() - c.generado.getTime()) / 60000) : null;
    return [
      "GAMMA HOY  " + L.corto + "  " + L.cuadrante + "   conv " + (L.convPrecio >= 0 ? "+" : "-") + " (" + L.libroConv + ")  pico " + fP(precio(L.picoFut), 0) + (L.mucho ? " mucho" : ""),
      (d.usarViva ? "libro Rithmic ES vivo" : "CBOE " + (ed != null ? ed + " min tarde" : "")) + " · OI de ayer · base " + L.baseOrigen + " · dominantes por " + L.libroDom + " · " + d.origenVelas,
    ];
  }

  /* la tendencia de ahora: lecturas descriptivas, cada una con su regla y su numero (no es una señal) */
  function tendencia(d) {
    const v = d.velas, L = d.L; if (!v.length) return null;
    const ult = v[v.length - 1], n = v.length, m = d.minutos || 1;
    const atras = k => v[Math.max(0, n - 1 - Math.round(k / m))];
    const filas = [];
    const et = (x, umb, alTxt, baTxt, neTxt) => x > umb ? ["al", alTxt] : x < -umb ? ["ba", baTxt] : ["ne", neTxt];
    const vw = d.vwap && d.vwap[n - 1];
    if (vw) { const dv = ult.c - vw; filas.push(["precio vs VWAP", fP(dv, 1) + " pts", et(dv, (d.futuro || 1) * 0.0003, "arriba", "abajo", "encima")]); }
    if (L && !L.sinBase && L.zeroVol) { const dz = ult.c - L.zeroVol; filas.push(["precio vs zero gamma", fP(dz, 1) + " pts", et(dz, 0, "gamma positiva (frena)", "gamma negativa (empuja)", "en el zero")]); }
    const c30 = atras(30), c60 = atras(60); if (c30) { const dm = ult.c - c30.c; filas.push(["momentum 30 min", fP(dm, 1) + " pts", et(dm, (d.futuro || 1) * 0.0005, "sube", "baja", "lateral")]); }
    if (c60) { const dm = ult.c - c60.c; filas.push(["momentum 60 min", fP(dm, 1) + " pts", et(dm, (d.futuro || 1) * 0.0008, "sube", "baja", "lateral")]); }
    if (ult.delta != null) { let s = 0, k = 0; for (let i = n - 1; i >= 0 && k < Math.round(30 / m); i--, k++) s += v[i].delta || 0; filas.push(["delta acumulado 30 min", Math.round(s).toLocaleString("es-AR"), et(s, 300, "compran", "venden", "parejo")]); }
    let hi = -Infinity, lo = Infinity; for (let i = Math.max(0, n - Math.round(60 / m)); i < n; i++) { hi = Math.max(hi, v[i].h); lo = Math.min(lo, v[i].l); }
    if (isFinite(hi)) { const pos = (ult.c - lo) / Math.max(1e-9, hi - lo); filas.push(["lugar en el rango de 60 min", Math.round(pos * 100) + " % (" + fP(lo, 0) + "–" + fP(hi, 0) + ")", pos > 0.7 ? ["al", "arriba del rango"] : pos < 0.3 ? ["ba", "abajo del rango"] : ["ne", "en el medio"]]); }
    if (L && !L.sinBase && L.doms.length) { const cerca = L.doms.map(x => x[0] - ult.c).sort((a, b) => Math.abs(a) - Math.abs(b))[0]; filas.push(["dominante más cercana", fP(cerca, 1) + " pts " + (cerca > 0 ? "arriba" : "abajo"), Math.abs(cerca) <= (d.futuro || 1) * 0.0008 ? ["ne", "encima de la raya"] : ["ne", "lejos"]]); }
    let al = 0, ba = 0; for (const f of filas) { if (f[2][0] === "al") al++; else if (f[2][0] === "ba") ba++; }
    const res = al >= ba + 2 ? ["al", "sesgo alcista"] : ba >= al + 2 ? ["ba", "sesgo bajista"] : ["ne", "sin sesgo claro"];
    return { filas, res, al, ba };
  }

  function lado(d) {
    const L = d.L, ex = d.ex; let h = "";
    const t = tendencia(d);
    if (t) h += '<div class="tarjeta"><h3>Tendencia ahora <small>lecturas, no señal</small></h3><div class="tend">' + t.filas.map(f => '<span class="t2">' + f[0] + '</span><span class="' + f[2][0] + '">' + f[1] + ' · ' + f[2][1] + '</span>').join("") + '</div><div class="resumen ' + t.res[0] + '">' + t.res[1] + ' <span class="t3" style="font-size:11px;font-weight:400">(' + t.al + ' a favor, ' + t.ba + ' en contra)</span></div></div>';
    if (L && !L.sinBase) {
      const v0 = ex && ex.vencimientos.length ? ex.vencimientos[0] : null;
      h += '<div class="tarjeta"><h3>Régimen (cuadrante)</h3><div class="regimen ' + cl[L.q] + '">' + L.corto + '<small>' + L.cuadrante + '</small></div>' +
        '<div class="kv" style="margin-top:8px"><b>pico cerca del precio</b><span class="v">' + fP(precio(L.picoFut), 0) + ' · ' + fB(L.picoGex) + (L.mucho ? ' · mucho' : '') + '</span><b>convexidad en el precio</b><span class="v ' + (L.convPrecio >= 0 ? "pos" : "neg") + '">' + fB(L.convPrecio) + ' (' + L.libroConv + ')</span></div></div>';
      h += '<div class="tarjeta"><h3>Niveles (en ' + (aj.vista === "indice" ? "índice" : "futuro") + ')</h3><div class="kv">' +
        L.doms.map((x, i) => '<b class="dom">dominante ' + (i + 1) + (x[0] > L.futuro ? " (arriba)" : " (abajo)") + '</b><span class="v dom">' + fP(precio(x[0]), 2) + ' · ' + fB(x[1]) + '</span>').join("") +
        '<b>zero gamma (vol)</b><span class="v">' + fP(precio(L.zeroVol), 2) + '</span><b>zero gamma (OI)</b><span class="v t2">' + fP(precio(L.zeroOi), 2) + '</span>' +
        '<b class="pos">+Γ major (vol)</b><span class="v pos">' + fP(precio(L.mpVol), 2) + '</span><b class="neg">−Γ major (vol)</b><span class="v neg">' + fP(precio(L.mnVol), 2) + '</span>' +
        '<b>net GEX vol / OI</b><span class="v">' + fB(L.netVol) + ' / ' + fB(L.netOi) + '</span>' +
        (L.pesadas || []).map(p => '<b class="t2">barra pesada</b><span class="v t2">' + fP(precio(p.fut), 0) + ' · ' + fB(p.gexVol) + (p.dte < 1 ? " 0DTE" : "") + '</span>').join("") + '</div></div>';
      h += '<div class="tarjeta"><h3>Precio y base</h3><div class="kv"><b>futuro</b><span class="v">' + fP(d.futuro, 2) + '</span><b class="t2">origen</b><span class="v t2">' + d.futOrigen + '</span><b>índice (S)</b><span class="v">' + fP(L.S, 2) + '</span><b>base usada</b><span class="v">' + fP(L.base, 2) + '</span><b class="t2">origen</b><span class="v t2">' + L.baseOrigen + '</span>' +
        (L.carry != null ? '<b class="t2">carry teórico</b><span class="v t2">' + fP(L.carry, 2) + '</span>' : '') + (d.baseRueda != null ? '<b class="t2">base medida por precio</b><span class="v t2">' + fP(d.baseRueda, 2) + ' (hace ' + Math.round(d.edadRueda) + ' min)</span>' : '') + '</div></div>';
      if (v0) h += '<div class="tarjeta"><h3>0DTE (vence ' + v0.f + ')</h3><div class="kv"><b>GEX vol / OI</b><span class="v">' + fB(v0.gexVol) + ' / ' + fB(v0.gexOi) + '</span><b>movimiento esperado 1σ</b><span class="v">±' + fP(v0.em_1sigma, 1) + ' pts (IV ' + (v0.iv_atm != null ? (v0.iv_atm * 100).toFixed(1) + " %" : "—") + ')</span><b>put/call OI · vol</b><span class="v">' + (v0.pc_oi != null ? v0.pc_oi.toFixed(2) : "—") + ' · ' + (v0.pc_vol != null ? v0.pc_vol.toFixed(2) : "—") + '</span>' +
        (ex.muros_0dte.call_oi ? '<b class="pos">muro call (OI)</b><span class="v pos">' + fP(precio(ex.muros_0dte.call_oi.fut), 0) + '</span>' : '') + (ex.muros_0dte.put_oi ? '<b class="neg">muro put (OI)</b><span class="v neg">' + fP(precio(ex.muros_0dte.put_oi.fut), 0) + '</span>' : '') + (ex.maxpain_0dte ? '<b class="t2">max pain</b><span class="v t2">' + fP(precio(ex.maxpain_0dte.fut), 0) + '</span>' : '') + '</div></div>';
      if (L.mc && L.mc.length) h += '<div class="tarjeta"><h3>Max Change (nube)</h3><div class="kv">' + L.mc.map(m => '<b>' + m.min + ' min</b><span class="v">' + (m.fut != null ? fP(precio(m.fut), 0) + ' · ' + fB(m.delta) : "—") + '</span>').join("") + '</div></div>';
    } else h += '<div class="tarjeta"><h3>Cuenta</h3><div class="t2">' + (L ? L.baseOrigen : "sin cadena todavía") + '</div></div>';
    const gs = d.marcas.slice().sort((a, b) => b.t - a.t).slice(0, 10);
    h += '<div class="tarjeta"><h3>Disparos de hoy (' + d.marcas.length + ')</h3>' + (gs.length ? '<div class="gat">' + gs.map(m => '<span class="t2">' + hora(m.t) + '</span><span class="' + (m.lado > 0 ? "r" : "rn") + '">' + (m.lado > 0 ? "LARGO" : "CORTO") + '</span><span>' + m.tipo + '</span><span>' + fP(precio(m.precio), 2) + '</span>').join("") + '</div>' : '<div class="t3">ninguno todavía (los manda tu ATAS)</div>') + '</div>';
    $("#lado").innerHTML = h;
  }

  /* etiquetas de un strike: dominante, major, muro, pesada, 0DTE, zero cerca */
  function etiquetas(s, L, ex) {
    const out = [];
    L.doms.forEach((x, i) => { if (Math.abs(x[0] - s.fut) <= 6) out.push('<span class="etq d">D' + (i + 1) + '</span>'); });
    if (L.mpVol != null && Math.abs(L.mpVol - s.fut) < 0.01) out.push('<span class="etq p">+Γ</span>');
    if (L.mnVol != null && Math.abs(L.mnVol - s.fut) < 0.01) out.push('<span class="etq n">−Γ</span>');
    if (ex && ex.muros_0dte) { const m = ex.muros_0dte; if (m.call_oi && m.call_oi.K === s.K) out.push('<span class="etq p">muro call</span>'); if (m.put_oi && m.put_oi.K === s.K) out.push('<span class="etq n">muro put</span>'); }
    if (L.zeroVol != null && Math.abs(L.zeroVol - s.fut) <= 5) out.push('<span class="etq z">0Γ</span>');
    if ((L.pesadas || []).some(p => p.K === s.K)) out.push('<span class="etq o">pesada</span>');
    if (s.dte != null && s.dte < 1) out.push('<span class="etq o">0DTE</span>');
    return out.join("");
  }

  function rapida(d) {
    const L = d.L, ex = d.ex; if (!L || L.sinBase) { $("#rapida").innerHTML = '<div class="nota">Sin cuenta.</div>'; return; }
    const f = L.futuro; const P = L.perfil.filter(s => Math.abs(s.fut - f) <= f * 0.015);
    const maxV = Math.max(1, ...P.map(s => Math.abs(s.gexVol)));
    const arriba = P.filter(s => s.fut > f).sort((a, b) => a.fut - b.fut).slice(0, 9).reverse(), abajo = P.filter(s => s.fut <= f).sort((a, b) => b.fut - a.fut).slice(0, 9);
    const barra = (v, m, col) => '<span class="mini" style="width:' + Math.round(Math.abs(v) / m * 48) + 'px;background:' + col + '"></span> ';
    const fila = s => { const a = s.antes || []; const d5 = a[1] != null ? s.gexVol - a[1] : null; return '<tr><td>' + fP(precio(s.fut), 0) + etiquetas(s, L, ex) + '</td><td class="' + (s.fut > f ? "pos" : "neg") + '">' + (s.fut > f ? "+" : "") + fP(s.fut - f, 1) + '</td><td class="' + (s.gexVol >= 0 ? "pos" : "neg") + '">' + barra(s.gexVol, maxV, s.gexVol >= 0 ? "#3fbf7f" : "#e5484d") + fB(s.gexVol) + '</td><td class="t2">' + fB(s.gexOi) + '</td><td class="' + (d5 == null ? "t3" : d5 >= 0 ? "pos" : "neg") + '">' + (d5 == null ? "—" : (d5 >= 0 ? "+" : "") + fB(d5)) + '</td><td>' + fP(s.volC, 0) + '/' + fP(s.volP, 0) + '</td></tr>'; };
    let h = '<div class="tabla-caja" style="max-height:none"><table><thead><tr><th>nivel</th><th>dist.</th><th>GEX vol</th><th>GEX OI</th><th>Δ5</th><th>vol c/p</th></tr></thead><tbody>' + arriba.map(fila).join("") +
      '<tr class="aqui"><td>' + fP(precio(f), 2) + ' <span class="etq z">precio</span></td><td colspan="5" class="t2">' + d.futOrigen + '</td></tr>' + abajo.map(fila).join("") + '</tbody></table></div>';
    $("#rapida").innerHTML = h; $("#rapidaNota").textContent = "±1,5 % · en " + (aj.vista === "indice" ? "índice" : "futuro") + " · Δ5 = cambio del GEX vol en 5 min";
    const ref = L.perfil.filter(s => Math.abs(s.fut - f) <= f * 0.03).sort((a, b) => Math.abs(b.gexVol) - Math.abs(a.gexVol)).slice(0, 7);
    $("#referentes").innerHTML = '<table><thead><tr><th>nivel</th><th>dist.</th><th>GEX vol</th><th>GEX OI</th></tr></thead><tbody>' + ref.map(s => '<tr><td>' + fP(precio(s.fut), 0) + etiquetas(s, L, ex) + '</td><td class="' + (s.fut > f ? "pos" : "neg") + '">' + (s.fut > f ? "+" : "") + fP(s.fut - f, 0) + '</td><td class="' + (s.gexVol >= 0 ? "pos" : "neg") + '">' + fB(s.gexVol) + '</td><td class="t2">' + fB(s.gexOi) + '</td></tr>').join("") + '</tbody></table>';
  }

  function vencimientos(d) {
    const ex = d.ex, L = d.L; if (!ex || !L || L.sinBase) { $("#vencimientos").innerHTML = '<div class="nota">Sin cuenta.</div>'; return; }
    const tot = ex.vencimientos.reduce((s, v) => s + Math.abs(v.gexVol), 0) || 1;
    let h = '<table><thead><tr><th>vence</th><th>días</th><th>GEX vol</th><th>%</th><th>GEX OI</th><th>P/C vol</th><th>EM 1σ</th></tr></thead><tbody>';
    for (const v of ex.vencimientos.slice(0, 8)) h += '<tr><td>' + v.f.slice(5) + (v.dias < 1 ? ' <span class="etq o">0DTE</span>' : '') + '</td><td>' + v.dias.toFixed(1) + '</td><td class="' + (v.gexVol >= 0 ? "pos" : "neg") + '">' + fB(v.gexVol) + '</td><td>' + (Math.abs(v.gexVol) / tot * 100).toFixed(0) + ' %</td><td class="' + (v.gexOi >= 0 ? "pos" : "neg") + '">' + fB(v.gexOi) + '</td><td>' + (v.pc_vol != null ? v.pc_vol.toFixed(2) : "—") + '</td><td>' + (v.em_1sigma != null ? "±" + fP(v.em_1sigma, 0) : "—") + '</td></tr>';
    h += '</tbody></table>';
    const m = ex.muros_0dte || {};
    h += '<div class="kv" style="margin-top:8px">' + (m.call_oi ? '<b class="pos">muro call 0DTE (OI)</b><span class="v pos">' + fP(precio(m.call_oi.fut), 0) + ' (' + fP(m.call_oi.n, 0) + ')</span>' : '') + (m.put_oi ? '<b class="neg">muro put 0DTE (OI)</b><span class="v neg">' + fP(precio(m.put_oi.fut), 0) + ' (' + fP(m.put_oi.n, 0) + ')</span>' : '') +
      (m.call_vol ? '<b class="pos">muro call por volumen</b><span class="v pos">' + fP(precio(m.call_vol.fut), 0) + '</span>' : '') + (m.put_vol ? '<b class="neg">muro put por volumen</b><span class="v neg">' + fP(precio(m.put_vol.fut), 0) + '</span>' : '') +
      (ex.maxpain_0dte ? '<b class="t2">max pain 0DTE</b><span class="v t2">' + fP(precio(ex.maxpain_0dte.fut), 0) + '</span>' : '') + '<b class="t2">put/call OI · vol (cadena)</b><span class="v t2">' + (ex.pc_oi != null ? ex.pc_oi.toFixed(2) : "—") + ' · ' + (ex.pc_vol != null ? ex.pc_vol.toFixed(2) : "—") + '</span></div>';
    h += '<div class="nota" style="margin-top:8px">EM 1σ = S × IV atm × √T. Muros = strike con más OI / volumen del vencimiento más cercano. Horizonte del indicador: ' + d.A.horizonte + '.</div>';
    $("#vencimientos").innerHTML = h;
  }

  function historia(d) {
    const s = d.serie || [];
    if (!$("#cvNet")) $("#historia").innerHTML = '<div class="cuatro"><div><div class="t3" style="font-size:10.5px">Net GEX del día (B)</div><canvas class="chico" id="cvNet"></canvas></div><div><div class="t3" style="font-size:10.5px">Zero y dominantes contra el precio</div><canvas class="chico" id="cvNiv"></canvas></div><div><div class="t3" style="font-size:10.5px">EM 0DTE 1σ (pts)</div><canvas class="chico" id="cvEm"></canvas></div><div><div class="t3" style="font-size:10.5px">Cuadrante (1 imán · 2 explosivo · 3 estable · 4 riesgo)</div><canvas class="chico" id="cvQ"></canvas></div></div><div class="nota">Serie de la nube (una línea por minuto en la rueda). ' + (d.vivoFresco ? 'Niveles por vela: de tu ATAS.' : 'Niveles por vela: de la nube.') + '</div>';
    const ts = s.map(x => new Date(x.t).getTime() / 1000);
    lineas($("#cvNet"), ts, [{ y: s.map(x => x.netVol / 1e9), col: "#3fbf7f", nombre: "net vol" }, { y: s.map(x => x.netOi / 1e9), col: "#8ab4dc", nombre: "net OI" }], { cero: true });
    const vs = d.velas;
    lineas($("#cvNiv"), vs.map(v => v.t), [{ y: vs.map(v => precio(v.c)), col: "#dfe6ee", nombre: "cierre" }, { y: vs.map(v => v.niv ? precio(v.niv.zero_vol) : null), col: "#a9b4c0", nombre: "zero" }, { y: vs.map(v => v.niv ? precio(v.niv.dom0) : null), col: "#f2c14e", nombre: "dom 1" }, { y: vs.map(v => v.niv ? precio(v.niv.dom1) : null), col: "#c9a03a", nombre: "dom 2" }], {});
    lineas($("#cvEm"), ts, [{ y: s.map(x => x.em0), col: "#9b7bff", nombre: "EM 1σ" }], {});
    lineas($("#cvQ"), ts, [{ y: s.map(x => x.q), col: "#f2c14e", nombre: "cuadrante", escalon: true }], { yMin: 0.5, yMax: 4.5 });
  }

  function gatillos(d) {
    const G = ptsObjetivo(); const ms = d.marcas.slice().sort((a, b) => a.t - b.t);
    if (!ms.length) { $("#gatillos").innerHTML = '<div class="t3">Ningún disparo todavía. Aparecen cuando tu ATAS está abierto y el subidor manda el vivo.</div>'; return; }
    const velas = d.velas; const idxDe = new Map(velas.map((v, i) => [v.t, i])); const dur = velas.length > 1 ? velas[1].t - velas[0].t : 60;
    let g = 0, p = 0, ab = 0;
    let h = '<div class="tabla-caja" style="max-height:40vh"><table><thead><tr><th>hora</th><th>tipo</th><th>lado</th><th>entrada</th><th>nivel</th><th>toque / p</th><th>MFE</th><th>MAE</th><th>+' + G + '/−' + G + '</th></tr></thead><tbody>';
    for (const m of ms.slice().reverse()) {
      let i = idxDe.get(m.t - dur); if (i == null) i = idxDe.get(m.t);
      let mfe = 0, mae = 0, res = null;
      if (i != null) for (let j = i + 1; j < Math.min(velas.length, i + 21); j++) { const w = velas[j]; const fav = m.lado > 0 ? w.h - m.precio : m.precio - w.l, con = m.lado > 0 ? m.precio - w.l : w.h - m.precio; mfe = Math.max(mfe, fav); mae = Math.max(mae, con); if (res === null) { if (fav >= G && con >= G) res = false; else if (fav >= G) res = true; else if (con >= G) res = false; } }
      if (res === true) g++; else if (res === false) p++; else ab++;
      h += '<tr><td>' + hora(m.t) + '</td><td>' + m.tipo + '</td><td class="' + (m.lado > 0 ? "pos" : "neg") + '">' + (m.lado > 0 ? "LARGO" : "CORTO") + '</td><td>' + fP(precio(m.precio), 2) + '</td><td>' + fP(precio(m.dom), 2) + '</td><td>' + (m.dz != null ? (String(m.tipo).startsWith("modelo") ? m.dz.toFixed(2) : m.dz) : "") + '</td><td class="pos">' + mfe.toFixed(1) + '</td><td class="neg">' + mae.toFixed(1) + '</td><td>' + (res === true ? '<span class="pos">GANA</span>' : res === false ? '<span class="neg">pierde</span>' : '<span class="t3">abierto</span>') + '</td></tr>';
    }
    h += '</tbody></table></div><div class="nota">Hoy: ' + g + ' ganaron, ' + p + ' perdieron, ' + ab + ' abiertos (' + (g + p ? Math.round(100 * g / (g + p)) : 0) + ' % a 1:1, +' + G + ' antes que −' + G + ' en 20 velas desde el cierre del disparo). Medido en 16 días: el rebote en las rayas rinde igual que una raya inventada.</div>';
    $("#gatillos").innerHTML = h;
  }

  function auditoria(d) {
    const L = d.L, e = d.estado, pc = d.pc, c = d.cadena;
    const audAtas = pc && (pc["audit_" + d.raiz] || pc.audit) ? (pc["audit_" + d.raiz] || pc.audit) : null;
    const filaK = (nombre, txt) => {
      if (!txt) return '<tr><td>' + nombre + '</td><td colspan="6" class="t3">sin dato</td></tr>';
      const g = k => { const m = txt.match(new RegExp(k + "=(-?[0-9.]+|NaN)")); return m && m[1] !== "NaN" ? +m[1] : null; };
      const b = g("base"), k = v => v == null || b == null ? "—" : fP(v - b, 1);
      const doms = (txt.match(/doms=([^ ]+)/) || [])[1] || "";
      const dk = doms.split("/").filter(Boolean).map(x => { const f = +x.split("=")[0]; return b == null ? "—" : fP(f - b, 1) + " (" + x.split("=")[1] + ")"; }).join(" / ");
      const t = (txt.match(/^(\S+)\s+AUDIT/) || [])[1] || "";
      return '<tr><td>' + nombre + '<br><span class="t3">' + t + '</span></td><td>' + fP(g("fut"), 2) + '</td><td>' + fP(b, 2) + '</td><td>' + k(g("zeroVol")) + '</td><td>' + k(g("mpVol")) + ' / ' + k(g("mnVol")) + '</td><td>' + dk + '</td><td>q' + (g("q") || "?") + ' · ' + (txt.match(/netVol=([^ ]+)/) || [])[1] + '</td></tr>';
    };
    let h = '<div class="tabla-caja" style="max-height:none"><table><thead><tr><th>cuenta</th><th>futuro</th><th>base</th><th>zero vol (K)</th><th>majors +Γ / −Γ (K)</th><th>dominantes (K)</th><th>cuadrante · net vol</th></tr></thead><tbody>' +
      filaK("JS (este navegador)", L && !L.sinBase ? Nucleo.audit(L, c) : null) + filaK("NUBE (estado_nube.py)", e ? (e.generado + " " + e.audit) : null) + filaK("ATAS (tu indicador, " + d.raiz + ")", audAtas) + '</tbody></table></div>';
    const ed = iso => { const m = Datos.edadMin(iso); return m == null ? "—" : m < 2 ? Math.round(m * 60) + " s" : m < 90 ? Math.round(m) + " min" : (m / 60).toFixed(1) + " h"; };
    const y = d.yahoo && d.yahoo.futuro && d.yahoo.futuro.t && d.yahoo.futuro.t.length ? d.yahoo.futuro : null;
    h += '<div class="tabla-caja" style="max-height:none;margin-top:8px"><table><thead><tr><th>fuente</th><th>qué trae</th><th>sello</th><th>edad</th></tr></thead><tbody>' +
      '<tr><td>CBOE (cadena SPX/NDX)</td><td>strikes, OI de ayer, IV, volumen de hoy · ~15 min tarde (medido 902 s) · strikes a ±5 % del spot</td><td>' + (c ? c.ts + " UTC" : "—") + '</td><td>' + (c && c.generado ? ed(c.generado.toISOString()) : "—") + '</td></tr>' +
      '<tr><td>Nube (estado_nube.py)</td><td>la cuenta del indicador en Python, cada minuto en GitHub Actions</td><td>' + (e ? e.generado : "—") + '</td><td>' + (e ? ed(e.generado) : "—") + '</td></tr>' +
      '<tr><td>Yahoo (velas del futuro)</td><td>' + (y ? y.simbolo + " · " + (y.nombre || "") + " · con retraso, sin delta" : "—") + '</td><td>' + (y ? hora(y.t[y.t.length - 1], true) : "—") + '</td><td>' + (y ? Math.round((Date.now() / 1000 - y.t[y.t.length - 1]) / 60) + " min" : "—") + '</td></tr>' +
      '<tr><td>Tu PC (vivo.json)</td><td>' + (pc ? "Gamma Hoy " + (pc.version || "?") + " en " + (pc.pc || "?") + " · " + Object.keys(pc.subidos || {}).join(", ") + (pc.viva_estado ? "<br><span class='t3'>" + pc.viva_estado.slice(20) + "</span>" : "") : "—") + '</td><td>' + (pc ? pc.generado : "—") + '</td><td>' + (pc ? ed(pc.generado) : "—") + '</td></tr>' +
      '<tr><td>Rithmic ES (cadena viva)</td><td>' + (d.viva ? "0DTE con puntas reales, " + (d.viva.filas || []).length + " filas, " + (d.viva.grandes || 0) + " grandes" : "no llega (solo con tu ATAS abierto y el conector encontrado)") + '</td><td>' + (d.viva ? d.viva.ts + " UTC" : "—") + '</td><td>' + (d.viva ? ed(d.viva.generado) : "—") + '</td></tr>' +
      '</tbody></table></div>';
    if (e && L && !L.sinBase) {
      const m = new Map(e.perfil.map(s => [s.K, s])); let n = 0, peor = 0, pk = null;
      for (const s of L.perfil) { const p = m.get(s.K); if (!p) continue; n++; const dd = Math.abs(s.gexVol - p.gexVol) / Math.max(1e6, Math.abs(p.gexVol)); if (dd > peor) { peor = dd; pk = s.K; } }
      h += '<div class="nota">Perfil por strike, JS contra nube: ' + n + ' strikes comparados; peor diferencia relativa ' + (peor * 100).toFixed(2) + ' % en K ' + pk + ' (precio y hora difieren un poco; con los mismos dan lo mismo: pruebas.html).</div>';
    }
    h += '<pre>JS    ' + (L && !L.sinBase ? Nucleo.audit(L, c) : "—") + '\nNUBE  ' + (e ? e.generado + "  " + e.audit : "—") + '\nATAS  ' + (audAtas || "—") + '</pre>';
    $("#auditoria").innerHTML = h;
  }

  function strikes(d) {
    const L = d.L; if (!L || L.sinBase) { $("#strikes").innerHTML = '<div class="nota">Sin cuenta.</div>'; return; }
    const P = L.perfil.filter(s => Math.abs(s.fut - L.futuro) <= L.futuro * 0.03).sort((a, b) => b.K - a.K);
    const maxV = Math.max(1, ...P.map(s => Math.abs(s.gexVol))), maxO = Math.max(1, ...P.map(s => Math.abs(s.gexOi)));
    const barra = (v, m, col) => '<span class="mini" style="width:' + Math.round(Math.abs(v) / m * 60) + 'px;background:' + col + '"></span> ';
    let aquiPuesto = false;
    let h = '<div class="nota">GEX = gamma × contratos × 100 × S² × 1 % (Black-Scholes, +call −put). Δ1/Δ5/Δ15: cambio del GEX por volumen contra 1, 5 y 15 min antes (las pelotitas), de la nube.</div><div class="tabla-caja" style="max-height:55vh"><table><thead><tr><th>strike</th><th>futuro</th><th>GEX vol</th><th>GEX OI</th><th>convexidad</th><th>OI c / p</th><th>vol hoy c / p</th><th>IV</th><th>vence</th><th>Δ1</th><th>Δ5</th><th>Δ15</th></tr></thead><tbody>';
    for (const s of P) {
      const aqui = !aquiPuesto && s.fut <= L.futuro; if (aqui) aquiPuesto = true;
      const a = s.antes || [];
      h += '<tr' + (aqui ? ' class="aqui"' : '') + '><td>' + fP(s.K, 0) + etiquetas(s, L, d.ex) + '</td><td>' + fP(precio(s.fut), 2) + '</td><td class="' + (s.gexVol >= 0 ? "pos" : "neg") + '">' + barra(s.gexVol, maxV, s.gexVol >= 0 ? "#3fbf7f" : "#e5484d") + fB(s.gexVol) + '</td><td class="t2">' + barra(s.gexOi, maxO, s.gexOi >= 0 ? "rgba(63,191,127,.45)" : "rgba(229,72,77,.45)") + fB(s.gexOi) + '</td><td style="color:' + (s.conv >= 0 ? "#9b7bff" : "#e5484d") + '">' + fB(s.conv) + '</td><td>' + fP(s.oiC, 0) + ' / ' + fP(s.oiP, 0) + '</td><td>' + fP(s.volC, 0) + ' / ' + fP(s.volP, 0) + '</td><td>' + (s.ivW > 0 ? (s.ivSum / s.ivW * 100).toFixed(1) + " %" : "—") + '</td><td>' + (s.dte < 1 ? '<span class="dom">0DTE</span>' : s.dte.toFixed(1) + " d") + '</td>' +
        [0, 1, 2].map(k => { const v = a[k]; if (v == null) return '<td class="t3">—</td>'; const dd = s.gexVol - v; return '<td class="' + (dd >= 0 ? "pos" : "neg") + '">' + (dd >= 0 ? "+" : "") + fB(dd) + '</td>'; }).join("") + '</tr>';
    }
    $("#strikes").innerHTML = h + '</tbody></table></div>';
  }

  function ayuda() {
    $("#ayuda").innerHTML = [
      ["Dominante", "El strike con más GEX (por volumen de hoy) de cada lado del precio, dentro del 2 %; el centroide lo corre unos puntos hacia donde hay más gamma alrededor.", "La raya amarilla: donde más plata de opciones hay parada cerca. Suele frenar, pero medido en 16 días rinde igual que una raya inventada."],
      ["Zero gamma", "El precio donde la suma de GEX de la cadena cruza cero (grilla ±3 %, interpolado). Por volumen (línea) y por OI (puntitos).", "Arriba del zero los dealers frenan el precio; abajo lo empujan. Es un termómetro de régimen, no una dirección."],
      ["Majors +Γ / −Γ", "El strike con el GEX positivo más grande y el negativo más grande, del libro de volumen.", "Los dos muros más grandes del día, uno de cada signo."],
      ["Cuadrante", "Pico de GEX cerca del precio (≥ 50 % del máximo = 'mucho') × convexidad en el precio: IMÁN, EXPLOSIVO, ESTABLE, RIESGO.", "Imán: el precio va y vuelve a la raya. Explosivo: si la rompe, se va. Estable: rangos amplios. Riesgo: tendencia, tamaño chico."],
      ["Tendencia ahora", "Lecturas simples con su regla: precio contra VWAP y contra el zero, momentum de 30 y 60 min, delta acumulado, lugar en el rango de 60 min. El sesgo cuenta cuántas apuntan para cada lado.", "Es una foto de cómo viene, no una señal de entrada. Sirve para no pelearse con lo que está pasando."],
      ["Mesa rápida", "Los strikes a ±1,5 % del precio, de arriba hacia abajo, con su distancia en puntos, GEX por volumen y OI, el cambio en 5 min y el volumen call/put; con etiquetas D1/D2, +Γ/−Γ, muros, zero, pesada, 0DTE.", "Lo que tenés pegado al precio, para ubicarte en dos segundos."],
      ["Convexidad", "Cuánto cambia el GEX de cada strike si el precio sube 1 % (la escalera violeta).", "Si es positiva, la raya se refuerza cuando el precio sube; si es negativa, se debilita."],
      ["Pelotitas (Max Change)", "La punta de cada barra hace 15, 5 y 1 minuto (grande, mediana, chica). Adentro de la barra = creció; afuera = se achicó.", "Ver de dónde viene la barra: si las tres están adentro, entró plata; si están afuera, se está yendo."],
      ["Barras pesadas", "Las barras con más |GEX| a ±0,6 % del precio, con raya punteada.", "Lo pesado que tenés pegado al precio, aunque no sea la dominante."],
      ["0DTE y EM 1σ", "Las opciones que vencen hoy. EM 1σ = S × IV atm × √T, el movimiento de un desvío hasta el vencimiento.", "Cuánto se espera que se mueva el índice hoy, según lo que pagan las opciones."],
      ["Muros por OI / max pain", "Muro call = strike con más OI de calls del 0DTE; muro put, ídem puts. Max pain = strike donde vence menos plata.", "Referencias que usan otros tableros (SpotGamma, MenthorQ); acá están para comparar, no están medidas."],
      ["Base", "Futuro − índice. Medida (paridad de opciones) > medida por precio > cruda > carry teórico (tasa − dividendo × tiempo). Siempre acotada con el carry.", "Los strikes son del índice; para dibujarlos en el futuro hay que sumarles la base. Si la base está mal, todo se corre."],
      ["Gatillos R / M / tren", "R: rebote en una raya (1.8). M: modelo logístico de ES a 10 min (1.7). tren: tres deltas en contra en la banda (1.3). Todos se registran para juzgarlos con días nuevos.", "Marcas de lo que el indicador vio. Ninguna está probada como ventaja; el R rinde igual que el placebo."],
      ["VIVO / NUBE", "VIVO: tu ATAS manda cada 20 s velas con order flow, niveles y disparos. NUBE: GitHub baja la cadena de CBOE cada minuto y calcula lo mismo; velas de Yahoo con retraso.", "Si tu PC se apaga, la web sigue sola con la nube. Lo dice arriba, con la edad de cada dato."],
      ["Mover el gráfico", "Como ATAS: rueda = desplazar en el tiempo; ctrl+rueda o arrastrar el eje de tiempo = zoom de tiempo; rueda sobre el eje de precio o arrastrarlo = estirar el precio; arrastrar el gráfico = mover tiempo y precio; ⊙ centrar = última vela y precio al medio; ⟲ autocentrar o doble clic = vivo con escala automática. Teclas: + − ← → ↑ ↓ c Home.", "Lo movés a gusto y con doble clic vuelve solo al presente. Hay margen a la derecha para que la última vela no quede pegada al borde."],
    ].map(x => '<h4>' + x[0] + '</h4><p>' + x[1] + '</p><p class="criollo">' + x[2] + '</p>').join("");
  }

  /* graficos chicos de lineas con ejes y ultimo valor: x = tiempos (s) o strikes; series [{y:[], col, nombre, escalon}] */
  function lineas(cv, xs, series, op) {
    if (!cv) return; const dpr = window.devicePixelRatio || 1; const W = cv.clientWidth, H = cv.clientHeight; if (!W || !H) return; cv.width = W * dpr; cv.height = H * dpr;
    const ctx = cv.getContext("2d"); ctx.setTransform(dpr, 0, 0, dpr, 0, 0); ctx.fillStyle = "#0b0f14"; ctx.fillRect(0, 0, W, H);
    const pts = []; series.forEach(s => s.y.forEach((v, i) => { if (v != null && isFinite(v) && xs[i] != null) pts.push([xs[i], v]); }));
    if (!pts.length) { ctx.fillStyle = "#5f6d7b"; ctx.font = "11px JetBrains Mono, monospace"; ctx.fillText("sin datos todavía", 10, 20); return; }
    let xmin = Infinity, xmax = -Infinity, ymin = Infinity, ymax = -Infinity; for (const p of pts) { xmin = Math.min(xmin, p[0]); xmax = Math.max(xmax, p[0]); ymin = Math.min(ymin, p[1]); ymax = Math.max(ymax, p[1]); }
    if (op.yMin != null) ymin = op.yMin; if (op.yMax != null) ymax = op.yMax;
    if (op.cero) { ymin = Math.min(ymin, 0); ymax = Math.max(ymax, 0); }
    if (xmax === xmin) xmax = xmin + 1; if (ymax === ymin) { ymax += 1; ymin -= 1; } const pad = (ymax - ymin) * 0.08; ymin -= pad; ymax += pad;
    const m = { l: 56, r: 12, t: 12, b: 22 };
    const X = x => m.l + (x - xmin) / (xmax - xmin) * (W - m.l - m.r), Y = y => H - m.b - (y - ymin) / (ymax - ymin) * (H - m.t - m.b);
    ctx.strokeStyle = "#1f2a36"; ctx.fillStyle = "#8a97a6"; ctx.font = "10px JetBrains Mono, monospace"; ctx.textAlign = "right"; ctx.textBaseline = "middle";
    const dec = Math.abs(ymax - ymin) < 10 ? 2 : 0;
    for (let i = 0; i <= 4; i++) { const y = ymin + (ymax - ymin) * i / 4; ctx.beginPath(); ctx.moveTo(m.l, Y(y)); ctx.lineTo(W - m.r, Y(y)); ctx.stroke(); ctx.fillText(y.toLocaleString("es-AR", { maximumFractionDigits: dec }), m.l - 4, Y(y)); }
    if (op.cero && ymin < 0 && ymax > 0) { ctx.strokeStyle = "#5f6d7b"; ctx.beginPath(); ctx.moveTo(m.l, Y(0)); ctx.lineTo(W - m.r, Y(0)); ctx.stroke(); }
    ctx.textAlign = "center"; ctx.textBaseline = "top";
    for (let i = 0; i <= 4; i++) { const x = xmin + (xmax - xmin) * i / 4; ctx.fillText(op.xEs === "strike" ? x.toFixed(0) : hora(x), X(x), H - m.b + 6); }
    if (op.marcaX != null) { ctx.strokeStyle = "#f2c14e"; ctx.setLineDash([3, 3]); ctx.beginPath(); ctx.moveTo(X(op.marcaX), m.t); ctx.lineTo(X(op.marcaX), H - m.b); ctx.stroke(); ctx.setLineDash([]); }
    series.forEach((s, k) => {
      ctx.strokeStyle = s.col; ctx.lineWidth = 1.4; ctx.beginPath(); let abierto = false, yAnt = null, ult = null;
      s.y.forEach((v, i) => { if (v == null || !isFinite(v) || xs[i] == null) { abierto = false; return; } const x = X(xs[i]), y = Y(v); if (!abierto) { ctx.moveTo(x, y); abierto = true; } else { if (s.escalon && yAnt != null) ctx.lineTo(x, yAnt); ctx.lineTo(x, y); } yAnt = y; ult = v; });
      ctx.stroke();
      ctx.fillStyle = s.col; ctx.textAlign = "left"; ctx.textBaseline = "top"; ctx.fillText(s.nombre + (ult != null ? " " + ult.toLocaleString("es-AR", { maximumFractionDigits: dec }) : ""), m.l + 6 + k * 118, m.t - 2);
    });
  }

  // ---------------------------------------------------------------- arranque
  refrescar().then(programar);
})();
