/* GRAFICO DE GAMMA HOY EN CANVAS, COPIA DE LA PANTALLA DE ATAS.
 *
 * Como en ATAS con el indicador puesto:
 *   fondo izquierdo   el perfil de GEX por strike (barras verdes/rojas desde el borde izquierdo, con
 *                     el OI mas fino detras), el rotulo "+1,8B oi-14M 0DTE" en la punta y las
 *                     pelotitas del Max Change (punta de la barra hace 15/5/1 min)
 *   velas             en el medio, con margen a la derecha de la ultima; VWAP; guiones amarillos de
 *                     las dominantes por vela; puntitos del zero; rayas (zero, majors, dominantes,
 *                     pesadas); bandas; disparos (R = rebote, M = modelo, tren)
 *   derecha           la escalera de convexidad (violeta) y el eje de precio con los rotulos D1/D2,
 *                     0Γ, +Γ/−Γ y el precio
 *   arriba izquierda  la cabecera (regimen, libro, base, fuente) y la linea del mouse sobre una vela
 *   arriba derecha    el cuadro de net vol / net OI, Δ 1'..30' (Max Change) y los niveles con su
 *                     distancia al precio
 *   abajo             CVD y delta por vela (solo con order flow de ATAS)
 * Manejo (ATAS): rueda = desplazar; ctrl+rueda o arrastrar el eje de tiempo = zoom de tiempo;
 * arrastrar el eje de precio o rueda sobre el eje = estirar el precio; arrastrar el grafico = mover
 * tiempo y precio; doble clic = autocentrar todo. Tactil: un dedo mueve, dos hacen zoom.
 */
(function (global) {
  "use strict";
  const COL = {
    fondo: "#0b0f14", panel: "#0e141b", grilla: "#182029", grilla2: "#121920", texto: "#c9d3dd", tenue: "#5f6d7b", eje: "#8a97a6",
    alcista: "#3fbf7f", bajista: "#e5484d", dom: "#f2c14e", dom2: "#c9a03a", zero: "#a9b4c0", pos: "#3fbf7f", neg: "#e5484d",
    conv: "#9b7bff", conv2: "#5e4a9e", vwap: "#2fb6d6", banda: "rgba(242,193,78,0.10)", cvd: "#3fbf7f",
    crosshair: "rgba(201,211,221,0.35)", pesada: "rgba(201,211,221,0.55)", caja: "rgba(11,15,20,0.86)",
  };
  function fmtB(v) { const a = Math.abs(v); return a >= 1e9 ? (v / 1e9).toFixed(1).replace(".", ",") + "B" : a >= 1e6 ? (v / 1e6).toFixed(0) + "M" : (v / 1e3).toFixed(0) + "K"; }
  function fmtS(v) { return (v >= 0 ? "+" : "") + fmtB(v); }
  function fmtP(v, dec) { return v == null ? "" : v.toLocaleString("es-AR", { minimumFractionDigits: dec, maximumFractionDigits: dec }); }
  const clamp = (v, a, b) => Math.max(a, Math.min(b, v));

  class Grafico {
    constructor(canvas, opciones) {
      this.cv = canvas; this.ctx = canvas.getContext("2d");
      this.op = Object.assign({ modoIndice: false, base: 0, decimales: 2, verPerfil: true, verConv: true, verCvd: true, verGuiones: true, verZeroPorVela: true, verBandas: true,
                                bandaPct: 0.08, verMarcas: true, verVwap: true, verPelotitas: true, verOi: true, verCuadro: true, alturaCvd: 0.16, anchoPerfil: 0.26, anchoConv: 0.09,
                                velasVisibles: 160, margenPct: 0.14, tipoPerfil: "vol", zona: "America/Argentina/Buenos_Aires", alHover: null }, opciones || {});
      this.datos = { velas: [], perfil: [], niveles: {}, marcas: [], cabecera: [], vwap: [], futuro: null, info: null };
      this.vista = { fin: null, n: this.op.velasVisibles, fijo: false, auto: true, pmin: null, pmax: null };
      this.mouse = null; this.dpr = Math.max(1, window.devicePixelRatio || 1);
      this._eventos();
    }
    setDatos(d) {
      const nAnt = this._nAnt || 0;
      Object.assign(this.datos, d);
      const N = this.datos.velas.length;
      if (!this.vista.fijo || this.vista.fin === null) this.vista.fin = N - 1 + this._margen();
      else if (N !== nAnt) this.vista.fin += (N - nAnt);      // llegaron velas: la vista se queda donde estaba
      this._nAnt = N; this.render();
    }
    _margen() { return Math.round(this.vista.n * this.op.margenPct); }

    // ---------------------------------------------------------------- manejo
    zoomTiempo(factor, xPivote) {
      const nAnt = this.vista.n, fin = this.vista.fin ?? this.datos.velas.length - 1;
      const n = clamp(Math.round(nAnt * factor), 15, 4000);
      let frac = 0.5;
      if (xPivote != null && this.L) frac = clamp((xPivote - this.L.velas.x0) / Math.max(1, this.L.velas.x1 - this.L.velas.x0), 0, 1);
      const barPiv = fin - nAnt + 1 + frac * nAnt;
      this.vista.fin = Math.round(barPiv + (1 - frac) * n - 1); this.vista.n = n; this._acotar(); this.render();
    }
    zoomPrecio(factor, yPivote) {
      if (this.vista.auto) { this.vista.auto = false; this.vista.pmin = this.pmin; this.vista.pmax = this.pmax; }
      const L = this.L; const r = this.vista.pmax - this.vista.pmin;
      let fr = 0.5; if (yPivote != null && L) fr = clamp((L.velas.y1 - yPivote) / Math.max(1, L.velas.y1 - L.velas.y0), 0, 1);
      const piv = this.vista.pmin + r * fr;
      this.vista.pmin = piv - (piv - this.vista.pmin) * factor; this.vista.pmax = piv + (this.vista.pmax - piv) * factor; this.render();
    }
    moverPrecio(dPix) {
      if (!this.L) return;
      if (this.vista.auto) { this.vista.auto = false; this.vista.pmin = this.pmin; this.vista.pmax = this.pmax; }
      const porPix = (this.vista.pmax - this.vista.pmin) / Math.max(1, this.L.velas.y1 - this.L.velas.y0);
      this.vista.pmin += dPix * porPix; this.vista.pmax += dPix * porPix; this.render();
    }
    mover(nVelas) { this.vista.fin = (this.vista.fin ?? this.datos.velas.length - 1) + nVelas; this._acotar(); this.render(); }
    _acotar() { const N = this.datos.velas.length; this.vista.fin = clamp(this.vista.fin, Math.min(10, N - 1), N - 1 + Math.max(this._margen(), Math.round(this.vista.n * 0.8))); this.vista.fijo = this.vista.fin < N - 1 + this._margen() - 1; }
    autoEscala() { this.vista.auto = true; this.render(); }
    centrar() {
      // la ultima vela al 60 % del ancho y el ultimo precio en el medio de la escala
      const N = this.datos.velas.length; if (!N) return;
      this.vista.fin = N - 1 + Math.round(this.vista.n * 0.4); this.vista.fijo = true;
      const ult = this.datos.velas[N - 1]; const r = (this.vista.auto || this.vista.pmin == null) ? (this.pmax - this.pmin) : (this.vista.pmax - this.vista.pmin);
      const c = this.precio(ult.c); this.vista.auto = false; this.vista.pmin = c - r / 2; this.vista.pmax = c + r / 2; this.render();
    }
    reset() { this.vista.fijo = false; this.vista.n = this.op.velasVisibles; this.vista.fin = this.datos.velas.length - 1 + this._margen(); this.vista.auto = true; this.render(); }

    _eventos() {
      const cv = this.cv;
      const pos = e => { const r = cv.getBoundingClientRect(); return { x: e.clientX - r.left, y: e.clientY - r.top }; };
      cv.addEventListener("mousemove", e => { this.mouse = pos(e); if (!this._arr) { this._cursor(); this.render(); } });
      cv.addEventListener("mouseleave", () => { this.mouse = null; this.render(); });
      cv.addEventListener("wheel", e => {
        e.preventDefault(); const p = pos(e); const dir = Math.sign(e.deltaY); if (!this.L) return;
        if (p.x >= this.L.ejeX) this.zoomPrecio(dir > 0 ? 1.12 : 0.89, p.y);                       // sobre el eje de precio: estirar
        else if (p.y >= this.L.velas.y1 + this.L.cvdH) this.zoomTiempo(dir > 0 ? 1.15 : 0.87, p.x);  // sobre el eje de tiempo: zoom
        else if (e.ctrlKey || e.metaKey || e.shiftKey) this.zoomTiempo(dir > 0 ? 1.15 : 0.87, p.x);
        else this.mover(dir * Math.max(1, Math.round(this.vista.n / 12)));                            // rueda = desplazar, como ATAS
      }, { passive: false });
      cv.addEventListener("mousedown", e => { const p = pos(e); if (!this.L) return; this._arr = { x: e.clientX, y: e.clientY, fin: this.vista.fin, n: this.vista.n, ejeP: p.x >= this.L.ejeX, ejeT: p.y >= this.L.velas.y1 + this.L.cvdH, ultY: e.clientY }; cv.style.cursor = "grabbing"; });
      window.addEventListener("mouseup", () => { if (this._arr) { this._arr = null; this._cursor(); this.render(); } });
      window.addEventListener("mousemove", e => {
        const a = this._arr; if (!a) return;
        if (a.ejeP) { const dy = e.clientY - a.ultY; a.ultY = e.clientY; this.zoomPrecio(Math.exp(dy / 160), null); return; }
        if (a.ejeT) { const dx = e.clientX - a.x; this.vista.n = clamp(Math.round(a.n * Math.exp(-dx / 200)), 15, 4000); this.vista.fin = a.fin; this._acotar(); this.render(); return; }
        const dx = e.clientX - a.x; const porVela = this._anchoVela || 6;
        this.vista.fin = Math.round(a.fin - dx / porVela); this._acotar();
        const dy = e.clientY - a.ultY; a.ultY = e.clientY; if (dy) this.moverPrecio(dy); else this.render();
      });
      cv.addEventListener("dblclick", () => this.reset());
      let toque = null;
      cv.addEventListener("touchstart", e => {
        if (e.touches.length === 1) toque = { x: e.touches[0].clientX, y: e.touches[0].clientY, fin: this.vista.fin };
        else if (e.touches.length === 2) toque = { dx: Math.abs(e.touches[0].clientX - e.touches[1].clientX), dy: Math.abs(e.touches[0].clientY - e.touches[1].clientY), n: this.vista.n, pmin: this.pmin, pmax: this.pmax };
      }, { passive: true });
      cv.addEventListener("touchmove", e => {
        if (!toque) return;
        if (e.touches.length === 1 && toque.x != null) { const dx = e.touches[0].clientX - toque.x, dy = e.touches[0].clientY - toque.y; toque.y = e.touches[0].clientY; this.vista.fin = Math.round(toque.fin - dx / (this._anchoVela || 6)); this._acotar(); if (dy) this.moverPrecio(dy); else this.render(); }
        else if (e.touches.length === 2 && toque.n) {
          const dx = Math.abs(e.touches[0].clientX - e.touches[1].clientX), dy = Math.abs(e.touches[0].clientY - e.touches[1].clientY);
          if (Math.abs(dx - toque.dx) >= Math.abs(dy - toque.dy)) { this.vista.n = clamp(Math.round(toque.n * toque.dx / Math.max(1, dx)), 15, 4000); this._acotar(); }
          else { const f = toque.dy / Math.max(1, dy); const c = (toque.pmin + toque.pmax) / 2, r = (toque.pmax - toque.pmin) * f; this.vista.auto = false; this.vista.pmin = c - r / 2; this.vista.pmax = c + r / 2; }
          this.render();
        }
      }, { passive: true });
      cv.addEventListener("touchend", () => { toque = null; });
    }
    _cursor() { const m = this.mouse, L = this.L; if (!m || !L) return; this.cv.style.cursor = m.x >= L.ejeX ? "ns-resize" : m.y >= L.velas.y1 + L.cvdH ? "ew-resize" : "crosshair"; }

    precio(v) { return this.op.modoIndice ? v - this.op.base : v; }

    _layout() {
      const W = this.cv.clientWidth, H = this.cv.clientHeight;
      const ejeW = 76, ejeH = 24, cab = this.datos.cabecera.length ? 14 * (this.datos.cabecera.length + 1) + 6 : 0;
      const cvdH = this.op.verCvd && this.datos.velas.some(v => v.delta != null) ? Math.round(H * this.op.alturaCvd) : 0;
      const convW = this.op.verConv ? Math.round(W * this.op.anchoConv) : 0;
      return { W, H, cab, ejeW, ejeH, cvdH, convW, perfilW: this.op.verPerfil ? Math.round(W * this.op.anchoPerfil) : 0,
               velas: { x0: 0, x1: W - ejeW - convW, y0: cab, y1: H - ejeH - cvdH }, conv: { x0: W - ejeW - convW, x1: W - ejeW }, cvd: { x0: 0, x1: W - ejeW, y0: H - ejeH - cvdH, y1: H - ejeH }, ejeX: W - ejeW };
    }

    render() {
      const cv = this.cv, ctx = this.ctx;
      const W = cv.clientWidth, H = cv.clientHeight; if (!W || !H) return;
      if (cv.width !== Math.round(W * this.dpr) || cv.height !== Math.round(H * this.dpr)) { cv.width = Math.round(W * this.dpr); cv.height = Math.round(H * this.dpr); }
      ctx.setTransform(this.dpr, 0, 0, this.dpr, 0, 0);
      ctx.fillStyle = COL.fondo; ctx.fillRect(0, 0, W, H);
      const L = this._layout(); this.L = L;
      const velas = this.datos.velas, N = velas.length;
      if (this.vista.fin === null) this.vista.fin = N - 1 + this._margen();
      const fin = this.vista.fin, ini = fin - this.vista.n + 1;
      const iniV = Math.max(0, ini), finV = Math.min(N - 1, fin);
      const vis = finV >= iniV ? velas.slice(iniV, finV + 1) : [];
      if (this.vista.auto || this.vista.pmin == null) {
        let pmin = Infinity, pmax = -Infinity;
        for (const v of vis) { pmin = Math.min(pmin, v.l); pmax = Math.max(pmax, v.h); }
        if (!isFinite(pmin)) { const f = this.datos.futuro || 100; pmin = f * 0.995; pmax = f * 1.005; }
        const rango = Math.max(pmax - pmin, (this.datos.futuro || pmax) * 0.002);
        pmin -= rango * 0.08; pmax += rango * 0.08;
        this.pmin = this.precio(pmin); this.pmax = this.precio(pmax);
      } else { this.pmin = this.vista.pmin; this.pmax = this.vista.pmax; }
      const yDe = p => L.velas.y1 - (this.precio(p) - this.pmin) / (this.pmax - this.pmin) * (L.velas.y1 - L.velas.y0);
      this.yDe = yDe;
      const anchoVela = (L.velas.x1 - L.velas.x0) / Math.max(1, this.vista.n); this._anchoVela = anchoVela;
      const xDe = i => L.velas.x0 + (i - ini + 0.5) * anchoVela;
      this.xDe = xDe; this.ini = iniV; this.fin = finV; this.iniVista = ini;
      // fondo del area de velas: grilla, perfil (detras), bandas, rayas
      ctx.save(); ctx.beginPath(); ctx.rect(0, L.cab, L.ejeX, L.velas.y1 - L.cab); ctx.clip();
      this._grilla(L, xDe, anchoVela, ini, fin);
      if (this.op.verPerfil) this._perfilIzquierdo(L, yDe);
      if (this.op.verBandas) this._bandas(L, yDe);
      this._rayas(L, yDe);
      if (this.op.verGuiones) this._guiones(vis, xDe, yDe, anchoVela, iniV);
      if (this.op.verVwap) this._vwap(vis, xDe, yDe, iniV);
      this._velas(vis, xDe, yDe, anchoVela, iniV);
      if (this.op.verMarcas) this._marcas(vis, xDe, yDe, anchoVela, iniV);
      if (this.op.verConv) this._escaleraConv(L, yDe);
      ctx.restore();
      this._ejePrecio(L, yDe);
      this._ejeTiempo(L, velas, xDe, anchoVela, ini, fin);
      if (L.cvdH) this._cvd(L, velas, xDe, anchoVela, iniV, finV);
      this._cabecera(L, vis, anchoVela, iniV);
      if (this.op.verCuadro) this._cuadro(L);
      if (this.mouse) this._crosshair(L, velas, xDe, yDe, anchoVela, ini);
      if (!this.vista.auto || this.vista.fijo) { ctx.font = "10px Consolas, monospace"; ctx.fillStyle = COL.tenue; ctx.textAlign = "right"; ctx.textBaseline = "bottom"; ctx.fillText((this.vista.fijo ? "desplazado · " : "") + (this.vista.auto ? "" : "escala manual · ") + "doble clic = autocentrar", L.velas.x1 - 6, L.velas.y1 - 4); }
    }

    _pasoPrecio() {
      const r = this.pmax - this.pmin; const objetivo = r / 10; const base = Math.pow(10, Math.floor(Math.log10(Math.max(1e-6, objetivo))));
      for (const m of [1, 2, 2.5, 5, 10]) if (base * m >= objetivo) return base * m;
      return base * 10;
    }
    _grilla(L, xDe, w, ini, fin) {
      const ctx = this.ctx; ctx.lineWidth = 1; const paso = this._pasoPrecio();
      for (let p = Math.ceil(this.pmin / paso) * paso; p <= this.pmax; p += paso) { const y = Math.round(L.velas.y1 - (p - this.pmin) / (this.pmax - this.pmin) * (L.velas.y1 - L.velas.y0)) + 0.5; ctx.strokeStyle = COL.grilla; ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(L.ejeX, y); ctx.stroke(); }
      const cada = Math.max(1, Math.ceil(90 / Math.max(1, w)));
      for (let i = ini; i <= fin; i++) if (((i % cada) + cada) % cada === 0) { const x = Math.round(xDe(i)) + 0.5; ctx.strokeStyle = COL.grilla2; ctx.beginPath(); ctx.moveTo(x, L.velas.y0); ctx.lineTo(x, L.velas.y1); ctx.stroke(); }
    }
    _bandas(L, yDe) {
      const n = this.datos.niveles; if (!n || !n.doms) return; const ctx = this.ctx; const f = this.datos.futuro || 1;
      for (const d of n.doms) { const b = f * this.op.bandaPct / 100; const y1 = yDe(d.fut + b), y2 = yDe(d.fut - b); ctx.fillStyle = COL.banda; ctx.fillRect(0, y1, L.velas.x1, y2 - y1); }
    }
    _raya(y, color, ancho, guion, x0, x1) { const ctx = this.ctx; ctx.save(); ctx.strokeStyle = color; ctx.lineWidth = ancho; ctx.setLineDash(guion || []); ctx.beginPath(); ctx.moveTo(x0, Math.round(y) + 0.5); ctx.lineTo(x1, Math.round(y) + 0.5); ctx.stroke(); ctx.restore(); }
    _rayas(L, yDe) {
      const n = this.datos.niveles; if (!n) return; const x0 = 0, x1 = L.ejeX;
      const en = v => v != null && this.precio(v) >= this.pmin && this.precio(v) <= this.pmax;
      if (en(n.zeroOi)) this._raya(yDe(n.zeroOi), "rgba(160,160,170,0.5)", 1, [2, 3], x0, x1);
      if (en(n.zeroVol)) this._raya(yDe(n.zeroVol), COL.zero, 1.4, [6, 4], x0, x1);
      if (en(n.mpVol)) this._raya(yDe(n.mpVol), COL.pos, 1.5, [], x0, x1);
      if (en(n.mnVol)) this._raya(yDe(n.mnVol), COL.neg, 1.5, [], x0, x1);
      (n.pesadas || []).forEach(p => { if (en(p.fut)) this._raya(yDe(p.fut), COL.pesada, 1, [1, 3], x0, x1); });
      (n.doms || []).forEach((d, i) => { if (en(d.fut)) this._raya(yDe(d.fut), i === 0 ? COL.dom : COL.dom2, i === 0 ? 1.6 : 1.1, [], x0, x1); });
    }
    _guiones(vis, xDe, yDe, w, iniV) {
      const ctx = this.ctx; const g = clamp(Math.round(w * 0.5), 2, 4), largo = Math.max(3, w * 0.9);
      for (let i = 0; i < vis.length; i++) {
        const v = vis[i], n = v.niv; if (!n) continue; const x = xDe(iniV + i);
        [n.dom0, n.dom1].forEach((d, k) => { if (d == null) return; const y = yDe(d); if (y < this.L.velas.y0 || y > this.L.velas.y1) return; ctx.fillStyle = k === 0 ? COL.dom : COL.dom2; ctx.fillRect(x - largo / 2, y - (k === 0 ? g : g - 1) / 2, largo, k === 0 ? g : Math.max(1, g - 1)); });
        if (this.op.verZeroPorVela && n.zero_vol != null) { const y = yDe(n.zero_vol); if (y >= this.L.velas.y0 && y <= this.L.velas.y1) { ctx.fillStyle = COL.zero; ctx.beginPath(); ctx.arc(x, y, 1.2, 0, Math.PI * 2); ctx.fill(); } }
      }
    }
    _vwap(vis, xDe, yDe, iniV) {
      const vw = this.datos.vwap; if (!vw || !vw.length) return; const ctx = this.ctx; ctx.strokeStyle = COL.vwap; ctx.lineWidth = 1.3; ctx.beginPath(); let abierto = false;
      for (let i = 0; i < vis.length; i++) { const v = vw[iniV + i]; if (v == null) { abierto = false; continue; } const x = xDe(iniV + i), y = yDe(v); if (!abierto) { ctx.moveTo(x, y); abierto = true; } else ctx.lineTo(x, y); }
      ctx.stroke();
    }
    _velas(vis, xDe, yDe, w, iniV) {
      const ctx = this.ctx; const cuerpo = Math.max(1, Math.floor(w * 0.64));
      for (let i = 0; i < vis.length; i++) {
        const v = vis[i], x = xDe(iniV + i); const alc = v.c >= v.o; ctx.strokeStyle = alc ? COL.alcista : COL.bajista; ctx.fillStyle = alc ? COL.alcista : COL.bajista; ctx.lineWidth = 1;
        ctx.beginPath(); ctx.moveTo(Math.round(x) + 0.5, yDe(v.h)); ctx.lineTo(Math.round(x) + 0.5, yDe(v.l)); ctx.stroke();
        const y1 = yDe(Math.max(v.o, v.c)), y2 = yDe(Math.min(v.o, v.c));
        if (cuerpo <= 1) ctx.fillRect(Math.round(x), y1, 1, Math.max(1, y2 - y1));
        else if (alc) { ctx.fillStyle = COL.fondo; ctx.fillRect(x - cuerpo / 2, y1, cuerpo, Math.max(1, y2 - y1)); ctx.strokeRect(Math.round(x - cuerpo / 2) + 0.5, Math.round(y1) + 0.5, cuerpo - 1, Math.max(1, y2 - y1)); }
        else ctx.fillRect(x - cuerpo / 2, y1, cuerpo, Math.max(1, y2 - y1));
        if (v.yahoo && i % 7 === 0) { ctx.fillStyle = "rgba(138,151,166,0.5)"; ctx.fillRect(Math.round(x), this.L.velas.y1 - 3, 1, 3); }
      }
    }
    _marcas(vis, xDe, yDe, w, iniV) {
      const ms = this.datos.marcas; if (!ms || !ms.length) return; const ctx = this.ctx; const porT = new Map(); vis.forEach((v, i) => porT.set(v.t, iniV + i));
      const dur = vis.length > 1 ? (vis[1].t - vis[0].t) : 60;
      for (const m of ms) {
        let idx = porT.get(m.t - dur); if (idx == null) idx = porT.get(m.t); if (idx == null) continue;
        const x = xDe(idx), col = m.lado > 0 ? COL.pos : COL.neg, v = vis[idx - iniV];
        ctx.save(); ctx.strokeStyle = col; ctx.fillStyle = col; ctx.lineWidth = 1.5; ctx.font = "10px Consolas, monospace"; ctx.textAlign = "center";
        if ((m.tipo || "").startsWith("rebote")) { const y = yDe(m.dom || m.precio), r = 4; ctx.beginPath(); ctx.arc(x, y, r, 0, Math.PI * 2); ctx.stroke(); ctx.fillText(m.dz <= 1 ? "R1" : "R", x, m.lado > 0 ? y + r + 10 : y - r - 3); }
        else if ((m.tipo || "").startsWith("modelo")) { const y = yDe(m.lado > 0 ? v.l : v.h) + (m.lado > 0 ? 12 : -12), r = 5; ctx.beginPath(); ctx.moveTo(x, y - r); ctx.lineTo(x + r, y); ctx.lineTo(x, y + r); ctx.lineTo(x - r, y); ctx.closePath(); ctx.fill(); ctx.fillText("M " + (m.dz != null ? m.dz.toFixed(2) : ""), x, m.lado > 0 ? y + r + 10 : y - r - 3); }
        else { const y = yDe(m.lado > 0 ? v.l : v.h) + (m.lado > 0 ? 10 : -10), r = 4; ctx.fillStyle = COL.dom; ctx.beginPath(); if (m.lado > 0) { ctx.moveTo(x - r, y + r); ctx.lineTo(x + r, y + r); ctx.lineTo(x, y - r); } else { ctx.moveTo(x - r, y - r); ctx.lineTo(x + r, y - r); ctx.lineTo(x, y + r); } ctx.closePath(); ctx.fill(); ctx.fillText("tren", x, m.lado > 0 ? y + r + 10 : y - r - 3); }
        ctx.restore();
      }
    }
    _altoBarra(vis, yDe) { let alto = 6; if (vis.length > 1) { const ys = vis.map(s => yDe(s.fut)).sort((a, b) => a - b); let d = Infinity; for (let i = 1; i < ys.length; i++) d = Math.min(d, ys[i] - ys[i - 1]); alto = clamp(d * 0.72, 2, 14); } return alto; }
    _perfilIzquierdo(L, yDe) {
      const P = this.datos.perfil; if (!P || !P.length) return; const ctx = this.ctx; const W = L.perfilW;
      const clave = this.op.tipoPerfil === "oi" ? "gexOi" : "gexVol";
      const vis = P.filter(s => this.precio(s.fut) >= this.pmin - 1 && this.precio(s.fut) <= this.pmax + 1);
      let maxG = 0, maxO = 0; for (const s of vis) { maxG = Math.max(maxG, Math.abs(s[clave])); maxO = Math.max(maxO, Math.abs(s.gexOi)); } if (!maxG) maxG = 1; if (!maxO) maxO = 1;
      const alto = this._altoBarra(vis, yDe);
      // ROTULOS SOLO EN LAS BARRAS QUE IMPORTAN (como en ATAS): las 3 mas grandes de cada lado del
      // precio, mas las dominantes y los majors. Antes cualquier barra > 8 % del maximo llevaba texto
      // y el perfil se volvia una nube de numeros (pedido del operador, 2026-09-11).
      const fRef = this.datos.futuro || 0, nivR = this.datos.niveles || {};
      const fijos = [].concat((nivR.doms || []).map(d => d.fut), [nivR.mpVol, nivR.mnVol]).filter(x => x != null);
      const porTam = (a, b) => Math.abs(b[clave]) - Math.abs(a[clave]);
      const marcados = new Set(vis.filter(s => s.fut > fRef).sort(porTam).slice(0, 3).concat(vis.filter(s => s.fut <= fRef).sort(porTam).slice(0, 3)).map(s => s.fut));
      const conRotulo = s => marcados.has(s.fut) || fijos.some(v => Math.abs(v - s.fut) < 0.6);
      ctx.font = "10.5px Consolas, monospace"; ctx.textBaseline = "middle"; ctx.textAlign = "left";
      for (const s of vis) {
        const y = yDe(s.fut), g = s[clave]; const largo = Math.abs(g) / maxG * W;
        if (this.op.verOi && clave === "gexVol") { const lo = Math.abs(s.gexOi) / maxO * W; ctx.fillStyle = s.gexOi >= 0 ? "rgba(63,191,127,0.16)" : "rgba(229,72,77,0.16)"; ctx.fillRect(0, y - alto / 2 - 1, lo, alto + 2); }
        ctx.fillStyle = g >= 0 ? "rgba(63,191,127,0.62)" : "rgba(229,72,77,0.62)"; ctx.fillRect(0, y - alto / 2, largo, alto);
        if (this.op.verPelotitas && s.antes) s.antes.forEach((a, k) => { if (a == null) return; const xa = Math.abs(a) / maxG * W; const r = [1.7, 2.5, 3.3][2 - k]; ctx.fillStyle = "rgba(223,230,238,0.9)"; ctx.beginPath(); ctx.arc(xa, y, r, 0, Math.PI * 2); ctx.fill(); ctx.strokeStyle = g >= 0 ? COL.pos : COL.neg; ctx.lineWidth = 0.8; ctx.stroke(); });
        if (alto >= 4 && conRotulo(s)) { ctx.fillStyle = g >= 0 ? COL.pos : COL.neg; ctx.fillText(fmtS(g) + (clave === "gexVol" ? " oi" + fmtS(s.gexOi) : "") + (s.dte != null && s.dte < 1 ? " 0DTE" : ""), largo + 4, y); }
      }
      ctx.fillStyle = COL.tenue; ctx.textBaseline = "top"; ctx.fillText(clave === "gexVol" ? "GEX por volumen (OI detrás)" : "GEX por OI", 4, L.cab + 2);
    }
    _escaleraConv(L, yDe) {
      const P = this.datos.perfil; if (!P || !P.length) return; const ctx = this.ctx; const x0 = L.conv.x0, x1 = L.conv.x1, W = x1 - x0;
      ctx.fillStyle = COL.panel; ctx.fillRect(x0, L.velas.y0, W, L.velas.y1 - L.velas.y0);
      const vis = P.filter(s => this.precio(s.fut) >= this.pmin - 1 && this.precio(s.fut) <= this.pmax + 1);
      let maxC = 0; for (const s of vis) maxC = Math.max(maxC, Math.abs(s.conv || 0)); if (!maxC) maxC = 1;
      const alto = this._altoBarra(vis, yDe);
      ctx.font = "9.5px Consolas, monospace"; ctx.textBaseline = "middle"; ctx.textAlign = "right";
      for (const s of vis) {
        if (s.conv == null) continue; const y = yDe(s.fut), lc = Math.abs(s.conv) / maxC * (W - 4);
        ctx.fillStyle = s.conv >= 0 ? COL.conv : COL.conv2; ctx.fillRect(x1 - lc, y - alto / 2, lc, alto);
        if (alto >= 5 && Math.abs(s.conv) >= maxC * 0.3) { ctx.fillStyle = "#dfe6ee"; ctx.fillText("Δ" + fmtS(s.conv), x1 - 2, y); }
      }
      ctx.fillStyle = COL.tenue; ctx.textAlign = "center"; ctx.textBaseline = "top"; ctx.fillText("convexidad", x0 + W / 2, L.velas.y0 + 2);
    }
    _ejePrecio(L, yDe) {
      const ctx = this.ctx; ctx.fillStyle = COL.fondo; ctx.fillRect(L.ejeX, 0, L.W - L.ejeX, L.H);
      ctx.strokeStyle = COL.grilla; ctx.beginPath(); ctx.moveTo(L.ejeX + 0.5, L.cab); ctx.lineTo(L.ejeX + 0.5, L.velas.y1); ctx.stroke();
      ctx.font = "10.5px Consolas, monospace"; ctx.textBaseline = "middle"; ctx.textAlign = "left"; ctx.fillStyle = COL.eje;
      const paso = this._pasoPrecio(); const dec = paso < 1 ? 2 : 0;
      for (let p = Math.ceil(this.pmin / paso) * paso; p <= this.pmax; p += paso) { const y = L.velas.y1 - (p - this.pmin) / (this.pmax - this.pmin) * (L.velas.y1 - L.velas.y0); if (y < L.cab + 6 || y > L.velas.y1 - 6) continue; ctx.fillText(fmtP(p, dec), L.ejeX + 5, y); }
      const n = this.datos.niveles || {};
      const etiqueta = (v, txt, col) => { if (v == null) return; const y = yDe(v); if (y < L.velas.y0 || y > L.velas.y1) return; ctx.fillStyle = col; ctx.fillRect(L.ejeX, y - 7, L.W - L.ejeX, 14); ctx.fillStyle = "#0b0f14"; ctx.fillText(txt, L.ejeX + 3, y); };
      (n.doms || []).forEach((d, i) => etiqueta(d.fut, "D" + (i + 1) + " " + fmtP(this.precio(d.fut), 0), i === 0 ? COL.dom : COL.dom2));
      etiqueta(n.zeroVol, "0Γ " + fmtP(this.precio(n.zeroVol), 0), COL.zero);
      etiqueta(n.mpVol, "+Γ " + fmtP(this.precio(n.mpVol), 0), COL.pos); etiqueta(n.mnVol, "−Γ " + fmtP(this.precio(n.mnVol), 0), COL.neg);
      if (this.datos.futuro != null) etiqueta(this.datos.futuro, fmtP(this.precio(this.datos.futuro), this.op.decimales), COL.texto);
    }
    _ejeTiempo(L, velas, xDe, w, ini, fin) {
      const ctx = this.ctx; ctx.fillStyle = COL.fondo; ctx.fillRect(0, L.velas.y1 + L.cvdH, L.ejeX, L.ejeH);
      ctx.strokeStyle = COL.grilla; ctx.beginPath(); ctx.moveTo(0, L.velas.y1 + L.cvdH + 0.5); ctx.lineTo(L.ejeX, L.velas.y1 + L.cvdH + 0.5); ctx.stroke();
      ctx.font = "10.5px Consolas, monospace"; ctx.fillStyle = COL.eje; ctx.textAlign = "center"; ctx.textBaseline = "top";
      const cada = Math.max(1, Math.ceil(90 / Math.max(1, w))); const tz = this.op.zona; let diaAnt = null; const N = velas.length;
      const dur = N > 1 ? velas[N - 1].t - velas[N - 2].t : 60;
      for (let i = ini; i <= fin; i++) {
        if (((i % cada) + cada) % cada !== 0) continue;
        const ts = i < N ? velas[i].t : (N ? velas[N - 1].t + (i - N + 1) * dur : null); if (ts == null) continue;
        const t = new Date(ts * 1000); const dia = t.toLocaleDateString("es-AR", { day: "2-digit", month: "2-digit", timeZone: tz });
        let txt = t.toLocaleTimeString("es-AR", { hour: "2-digit", minute: "2-digit", hour12: false, timeZone: tz });
        if (dia !== diaAnt) { txt = dia + " " + txt; diaAnt = dia; }
        ctx.fillStyle = i < N ? COL.eje : COL.tenue; ctx.fillText(txt, xDe(i), L.velas.y1 + L.cvdH + 6);
      }
    }
    _cvd(L, velas, xDe, w, iniV, finV) {
      const ctx = this.ctx; const y0 = L.cvd.y0, y1 = L.cvd.y1;
      ctx.fillStyle = COL.panel; ctx.fillRect(0, y0, L.ejeX, y1 - y0);
      let acum = 0; const cvd = new Array(velas.length);
      for (let i = 0; i < velas.length; i++) { if (i > 0 && (velas[i].t - velas[i - 1].t) > 3600 * 3) acum = 0; acum += velas[i].delta || 0; cvd[i] = acum; }
      let mn = Infinity, mx = -Infinity, md = 0;
      for (let i = iniV; i <= finV; i++) { mn = Math.min(mn, cvd[i]); mx = Math.max(mx, cvd[i]); md = Math.max(md, Math.abs(velas[i].delta || 0)); }
      if (!isFinite(mn)) return; if (mx === mn) mx = mn + 1;
      const yC = v => y1 - 4 - (v - mn) / (mx - mn) * (y1 - y0 - 8); const ym = (y0 + y1) / 2;
      for (let i = iniV; i <= finV; i++) { const d = velas[i].delta || 0; const h = md ? Math.abs(d) / md * (y1 - y0) * 0.45 : 0; ctx.fillStyle = d >= 0 ? "rgba(63,191,127,0.35)" : "rgba(229,72,77,0.35)"; ctx.fillRect(xDe(i) - w * 0.3, d >= 0 ? ym - h : ym, Math.max(1, w * 0.6), h); }
      ctx.strokeStyle = COL.cvd; ctx.lineWidth = 1.4; ctx.beginPath();
      for (let i = iniV; i <= finV; i++) { const x = xDe(i), y = yC(cvd[i]); if (i === iniV) ctx.moveTo(x, y); else ctx.lineTo(x, y); }
      ctx.stroke();
      ctx.font = "10px Consolas, monospace"; ctx.fillStyle = COL.tenue; ctx.textAlign = "left"; ctx.textBaseline = "top";
      ctx.fillText("CVD " + Math.round(cvd[finV] || 0).toLocaleString("es-AR") + "  ·  delta por vela", 6, y0 + 3);
      ctx.fillStyle = COL.fondo; ctx.fillRect(L.ejeX, y0, L.W - L.ejeX, y1 - y0); ctx.fillStyle = COL.eje; ctx.textBaseline = "middle";
      ctx.fillText(Math.round(mx).toLocaleString("es-AR"), L.ejeX + 5, y0 + 8); ctx.fillText(Math.round(mn).toLocaleString("es-AR"), L.ejeX + 5, y1 - 8);
    }
    _cabecera(L, vis, w, iniV) {
      const ctx = this.ctx; if (!this.datos.cabecera.length) return;
      ctx.fillStyle = COL.fondo; ctx.fillRect(0, 0, L.ejeX, L.cab);
      ctx.font = "11.5px Consolas, monospace"; ctx.textBaseline = "top"; ctx.textAlign = "left";
      this.datos.cabecera.forEach((l, i) => { ctx.fillStyle = i === 0 ? COL.dom : COL.texto; ctx.fillText(l, 8, 4 + i * 14); });
      // tercera linea: la vela bajo el mouse (como el modo Cabecera de ATAS)
      const m = this.mouse; let txt = "";
      if (m && m.x < L.ejeX && m.y >= L.cab && m.y <= L.velas.y1) {
        const i = Math.floor((m.x - L.velas.x0) / w) + this.iniVista; const v = this.datos.velas[i];
        if (v) {
          const hora = new Date(v.t * 1000).toLocaleTimeString("es-AR", { hour: "2-digit", minute: "2-digit", hour12: false, timeZone: this.op.zona }); const dec = this.op.decimales;
          txt = "vela " + hora + "  O " + fmtP(this.precio(v.o), dec) + "  H " + fmtP(this.precio(v.h), dec) + "  L " + fmtP(this.precio(v.l), dec) + "  C " + fmtP(this.precio(v.c), dec) + (v.vol != null ? "  vol " + v.vol : "") + (v.delta != null ? "  Δ " + v.delta : "") + (v.yahoo ? "  (Yahoo)" : "");
          if (v.niv) txt += "   · en esa vela: D " + [v.niv.dom0, v.niv.dom1].filter(x => x != null).map(x => fmtP(this.precio(x), 0)).join("/") + (v.niv.zero_vol != null ? "  0Γ " + fmtP(this.precio(v.niv.zero_vol), 0) : "") + (v.niv.q_cuadrante ? "  q" + v.niv.q_cuadrante : "");
        }
      }
      ctx.fillStyle = COL.tenue; ctx.fillText(txt || "mouse sobre una vela: acá aparece lo que regía en esa vela", 8, 4 + this.datos.cabecera.length * 14);
    }
    _cuadro(L) {
      const inf = this.datos.info, n = this.datos.niveles || {}; if (!inf) return; const ctx = this.ctx;
      const lineas = [];
      if (inf.netVol != null) lineas.push([COL.texto, "net vol " + fmtS(inf.netVol)]);
      if (inf.netOi != null) lineas.push([COL.texto, "net OI  " + fmtS(inf.netOi)]);
      (inf.mc || []).forEach(m => { if (m.fut != null) lineas.push([COL.dom, "Δ " + String(m.min).padStart(2) + "' " + fmtP(this.precio(m.fut), 0) + " " + fmtS(m.delta)]); });
      const f = this.datos.futuro; const niv = [];
      if (n.zeroOi != null) niv.push([COL.zero, "0Γ OI", n.zeroOi]); if (n.mpVol != null) niv.push([COL.pos, "+Γ", n.mpVol]);
      (n.doms || []).forEach((d, i) => niv.push([i === 0 ? COL.dom : COL.dom2, "D" + (i + 1), d.fut])); if (n.zeroVol != null) niv.push([COL.zero, "0Γ vol", n.zeroVol]); if (n.mnVol != null) niv.push([COL.neg, "−Γ", n.mnVol]);
      niv.sort((a, b) => b[2] - a[2]).forEach(x => lineas.push([x[0], x[1].padEnd(6) + " " + fmtP(this.precio(x[2]), 0) + (f != null ? " " + (x[2] - f >= 0 ? "+" : "") + (x[2] - f).toFixed(0) : "")]));
      if (!lineas.length) return;
      ctx.font = "10.5px Consolas, monospace"; ctx.textBaseline = "top"; ctx.textAlign = "left";
      let ancho = 0; for (const l of lineas) ancho = Math.max(ancho, ctx.measureText(l[1]).width);
      const x = L.conv.x0 - ancho - 22, y = L.cab + 4, h = lineas.length * 13 + 8;
      ctx.fillStyle = COL.caja; ctx.fillRect(x, y, ancho + 14, h); ctx.strokeStyle = COL.grilla; ctx.strokeRect(x + 0.5, y + 0.5, ancho + 13, h - 1);
      lineas.forEach((l, i) => { ctx.fillStyle = l[0]; ctx.fillText(l[1], x + 7, y + 4 + i * 13); });
    }
    _crosshair(L, velas, xDe, yDe, w, ini) {
      const m = this.mouse, ctx = this.ctx; if (m.x > L.ejeX || m.y < L.cab || m.y > L.velas.y1) return;
      const i = Math.floor((m.x - L.velas.x0) / w) + ini; const x = xDe(i);
      ctx.save(); ctx.strokeStyle = COL.crosshair; ctx.setLineDash([3, 3]); ctx.beginPath(); ctx.moveTo(x, L.cab); ctx.lineTo(x, L.velas.y1); ctx.moveTo(0, m.y); ctx.lineTo(L.ejeX, m.y); ctx.stroke(); ctx.restore();
      const precio = this.pmin + (L.velas.y1 - m.y) / (L.velas.y1 - L.velas.y0) * (this.pmax - this.pmin);
      ctx.font = "11px Consolas, monospace"; ctx.textBaseline = "middle"; ctx.textAlign = "left";
      ctx.fillStyle = COL.texto; ctx.fillRect(L.ejeX, m.y - 7, L.W - L.ejeX, 14); ctx.fillStyle = "#0b0f14"; ctx.fillText(fmtP(precio, this.op.decimales), L.ejeX + 3, m.y);
      const v = velas[i]; const N = velas.length; const dur = N > 1 ? velas[N - 1].t - velas[N - 2].t : 60; const ts = v ? v.t : (N ? velas[N - 1].t + (i - N + 1) * dur : null);
      if (ts != null) { const tx = new Date(ts * 1000).toLocaleTimeString("es-AR", { hour: "2-digit", minute: "2-digit", hour12: false, timeZone: this.op.zona }); ctx.fillStyle = COL.texto; ctx.fillRect(x - 20, L.velas.y1 + L.cvdH + 2, 40, 16); ctx.fillStyle = "#0b0f14"; ctx.textAlign = "center"; ctx.fillText(tx, x, L.velas.y1 + L.cvdH + 10); }
      if (this.op.alHover && v) this.op.alHover({ vela: v, precio });
    }
  }

  global.Grafico = Grafico; global.GraficoCOL = COL; global.fmtB = fmtB; global.fmtP = fmtP;
})(typeof window !== "undefined" ? window : globalThis);
