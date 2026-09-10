/* GRAFICO DE GAMMA HOY EN CANVAS.
 *
 * Dibuja lo mismo que el indicador en ATAS, con la misma escala de precio compartida:
 *   izquierda  velas, VWAP, los guiones amarillos de las dominantes por vela, los puntitos del zero
 *              por vela, las rayas (zero, majors, dominantes, pesadas), las bandas y los disparos
 *              (R = rebote, M = modelo, tren)
 *   derecha    el perfil por strike (GEX por volumen con el OI detras), las pelotitas del Max Change
 *              (punta de cada barra hace 15/5/1 min) y la escalera de convexidad
 *   abajo      CVD (delta acumulado) y delta por vela, cuando hay order flow (solo desde ATAS)
 * Se maneja como ATAS: arrastrar el grafico mueve tiempo y precio; arrastrar el eje de precio lo
 * estira; rueda = zoom de tiempo sobre la vela del mouse; ctrl+rueda = zoom de precio;
 * shift+rueda = desplazar; doble clic = volver al vivo con escala automatica. Tactil: un dedo mueve,
 * dos dedos hacen zoom. Sin dependencias. Precio en puntos del FUTURO; el modo indice resta la base.
 */
(function (global) {
  "use strict";
  const COL = {
    fondo: "#0b0f14", panel: "#0e141b", grilla: "#1a222c", grilla2: "#141b24", texto: "#c9d3dd", tenue: "#5f6d7b", eje: "#8a97a6",
    alcista: "#3fbf7f", bajista: "#e5484d", dom: "#f2c14e", dom2: "#c9a03a", zero: "#a9b4c0", pos: "#3fbf7f", neg: "#e5484d",
    conv: "#9b7bff", conv2: "#5e4a9e", vwap: "#2fb6d6", banda: "rgba(242,193,78,0.10)", cvd: "#3fbf7f",
    crosshair: "rgba(201,211,221,0.35)", pesada: "rgba(201,211,221,0.55)",
  };

  function fmtB(v) { const a = Math.abs(v); return a >= 1e9 ? (v / 1e9).toFixed(1) + "B" : a >= 1e6 ? (v / 1e6).toFixed(0) + "M" : (v / 1e3).toFixed(0) + "K"; }
  function fmtP(v, dec) { return v == null ? "" : v.toLocaleString("es-AR", { minimumFractionDigits: dec, maximumFractionDigits: dec }); }
  const clamp = (v, a, b) => Math.max(a, Math.min(b, v));

  class Grafico {
    constructor(canvas, opciones) {
      this.cv = canvas; this.ctx = canvas.getContext("2d");
      this.op = Object.assign({ modoIndice: false, base: 0, decimales: 2, verPerfil: true, verConv: true, verCvd: true, verGuiones: true, verZeroPorVela: true, verBandas: true,
                                bandaPct: 0.08, verMarcas: true, verVwap: true, verPelotitas: true, verOi: true, alturaCvd: 0.17, anchoPerfil: 0.28, velasVisibles: 180, tipoPerfil: "vol",
                                zona: "America/Argentina/Buenos_Aires", alHover: null }, opciones || {});
      this.datos = { velas: [], perfil: [], niveles: {}, marcas: [], cabecera: [], vwap: [], futuro: null };
      this.vista = { fin: null, n: this.op.velasVisibles, fijo: false, auto: true, pmin: null, pmax: null };
      this.mouse = null; this.dpr = Math.max(1, window.devicePixelRatio || 1);
      this._eventos();
    }

    setDatos(d) {
      Object.assign(this.datos, d);
      if (!this.vista.fijo || this.vista.fin === null || this.vista.fin >= (this._nAnt || 0) - 1) this.vista.fin = this.datos.velas.length - 1;
      this._nAnt = this.datos.velas.length;
      this.render();
    }

    // ---------------------------------------------------------------- API de botones
    zoomTiempo(factor, xPivote) {
      const nAnt = this.vista.n, fin = this.vista.fin ?? this.datos.velas.length - 1;
      const n = clamp(Math.round(nAnt * factor), 20, 3000);
      if (xPivote != null && this.L) {
        // la vela bajo el mouse se queda quieta
        const frac = clamp((xPivote - this.L.velas.x0) / Math.max(1, this.L.velas.x1 - this.L.velas.x0), 0, 1);
        const barPiv = fin - nAnt + 1 + frac * nAnt;
        const nuevoFin = Math.round(barPiv + (1 - frac) * n - 1);
        this.vista.fin = clamp(nuevoFin, 10, this.datos.velas.length - 1);
        this.vista.fijo = this.vista.fin < this.datos.velas.length - 1;
      }
      this.vista.n = n; this.render();
    }
    zoomPrecio(factor, yPivote) {
      if (this.vista.auto) { this.vista.auto = false; this.vista.pmin = this.pmin; this.vista.pmax = this.pmax; }
      const L = this.L; const r = this.vista.pmax - this.vista.pmin;
      let fr = 0.5; if (yPivote != null && L) fr = clamp((L.velas.y1 - yPivote) / Math.max(1, L.velas.y1 - L.velas.y0), 0, 1);
      const piv = this.vista.pmin + r * fr;
      this.vista.pmin = piv - (piv - this.vista.pmin) * factor; this.vista.pmax = piv + (this.vista.pmax - piv) * factor;
      this.render();
    }
    moverPrecio(dPix) {
      if (!this.L) return;
      if (this.vista.auto) { this.vista.auto = false; this.vista.pmin = this.pmin; this.vista.pmax = this.pmax; }
      const porPix = (this.vista.pmax - this.vista.pmin) / Math.max(1, this.L.velas.y1 - this.L.velas.y0);
      this.vista.pmin += dPix * porPix; this.vista.pmax += dPix * porPix; this.render();
    }
    mover(nVelas) { this.vista.fin = clamp((this.vista.fin ?? this.datos.velas.length - 1) + nVelas, 10, this.datos.velas.length - 1); this.vista.fijo = this.vista.fin < this.datos.velas.length - 1; this.render(); }
    autoEscala() { this.vista.auto = true; this.render(); }
    reset() { this.vista.fijo = false; this.vista.fin = this.datos.velas.length - 1; this.vista.n = this.op.velasVisibles; this.vista.auto = true; this.render(); }

    _eventos() {
      const cv = this.cv;
      const pos = e => { const r = cv.getBoundingClientRect(); return { x: e.clientX - r.left, y: e.clientY - r.top }; };
      cv.addEventListener("mousemove", e => { this.mouse = pos(e); if (!this._arr) this.render(); });
      cv.addEventListener("mouseleave", () => { this.mouse = null; this.render(); });
      cv.addEventListener("wheel", e => {
        e.preventDefault(); const p = pos(e); const dir = Math.sign(e.deltaY);
        if (e.ctrlKey || e.metaKey) this.zoomPrecio(dir > 0 ? 1.15 : 0.87, p.y);
        else if (e.shiftKey) this.mover(dir * Math.max(1, Math.round(this.vista.n / 10)));
        else if (this.L && p.x >= this.L.ejeX) this.zoomPrecio(dir > 0 ? 1.15 : 0.87, p.y);
        else this.zoomTiempo(dir > 0 ? 1.15 : 0.87, p.x);
      }, { passive: false });
      cv.addEventListener("mousedown", e => { const p = pos(e); this._arr = { x: e.clientX, y: e.clientY, fin: this.vista.fin ?? this.datos.velas.length - 1, eje: this.L && p.x >= this.L.ejeX, ultY: e.clientY, ultX: e.clientX }; cv.style.cursor = "grabbing"; });
      window.addEventListener("mouseup", () => { if (this._arr) { this._arr = null; cv.style.cursor = "crosshair"; this.render(); } });
      window.addEventListener("mousemove", e => {
        const a = this._arr; if (!a) return;
        if (a.eje) { const dy = e.clientY - a.ultY; a.ultY = e.clientY; this.zoomPrecio(Math.exp(dy / 150), null); return; }
        const dx = e.clientX - a.x; const porVela = this._anchoVela || 6;
        this.vista.fin = clamp(Math.round(a.fin - dx / porVela), 10, this.datos.velas.length - 1); this.vista.fijo = this.vista.fin < this.datos.velas.length - 1;
        const dy = e.clientY - a.ultY; a.ultY = e.clientY; if (dy) this.moverPrecio(dy); else this.render();
      });
      cv.addEventListener("dblclick", () => this.reset());
      let toque = null;
      cv.addEventListener("touchstart", e => {
        if (e.touches.length === 1) toque = { x: e.touches[0].clientX, y: e.touches[0].clientY, fin: this.vista.fin ?? this.datos.velas.length - 1 };
        else if (e.touches.length === 2) toque = { d: Math.hypot(e.touches[0].clientX - e.touches[1].clientX, e.touches[0].clientY - e.touches[1].clientY), n: this.vista.n };
      }, { passive: true });
      cv.addEventListener("touchmove", e => {
        if (!toque) return;
        if (e.touches.length === 1 && toque.x != null) { const dx = e.touches[0].clientX - toque.x, dy = e.touches[0].clientY - toque.y; toque.y = e.touches[0].clientY; this.vista.fin = clamp(Math.round(toque.fin - dx / (this._anchoVela || 6)), 10, this.datos.velas.length - 1); this.vista.fijo = this.vista.fin < this.datos.velas.length - 1; if (dy) this.moverPrecio(dy); else this.render(); }
        else if (e.touches.length === 2 && toque.d) { const d = Math.hypot(e.touches[0].clientX - e.touches[1].clientX, e.touches[0].clientY - e.touches[1].clientY); this.vista.n = clamp(Math.round(toque.n * toque.d / Math.max(1, d)), 20, 3000); this.render(); }
      }, { passive: true });
      cv.addEventListener("touchend", () => { toque = null; });
    }

    precio(v) { return this.op.modoIndice ? v - this.op.base : v; }

    _layout() {
      const W = this.cv.clientWidth, H = this.cv.clientHeight;
      const ejeW = 72, ejeH = 24, cab = this.datos.cabecera.length ? 14 * this.datos.cabecera.length + 8 : 0;
      const perfilW = this.op.verPerfil ? Math.round(W * this.op.anchoPerfil) : 0;
      const cvdH = this.op.verCvd && this.datos.velas.some(v => v.delta != null) ? Math.round(H * this.op.alturaCvd) : 0;
      return { W, H, cab, ejeW, ejeH, velas: { x0: 0, x1: W - ejeW - perfilW, y0: cab, y1: H - ejeH - cvdH }, perfil: { x0: W - ejeW - perfilW, x1: W - ejeW, y0: cab, y1: H - ejeH - cvdH }, cvd: { x0: 0, x1: W - ejeW - perfilW, y0: H - ejeH - cvdH, y1: H - ejeH }, ejeX: W - ejeW, perfilW, cvdH };
    }

    render() {
      const cv = this.cv, ctx = this.ctx;
      const W = cv.clientWidth, H = cv.clientHeight;
      if (!W || !H) return;
      if (cv.width !== Math.round(W * this.dpr) || cv.height !== Math.round(H * this.dpr)) { cv.width = Math.round(W * this.dpr); cv.height = Math.round(H * this.dpr); }
      ctx.setTransform(this.dpr, 0, 0, this.dpr, 0, 0);
      ctx.fillStyle = COL.fondo; ctx.fillRect(0, 0, W, H);
      const L = this._layout(); this.L = L;
      const velas = this.datos.velas;
      const fin = Math.min(velas.length - 1, this.vista.fin ?? velas.length - 1), ini = Math.max(0, fin - this.vista.n + 1);
      const vis = velas.slice(ini, fin + 1);
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
      const anchoVela = (L.velas.x1 - L.velas.x0) / Math.max(1, vis.length); this._anchoVela = anchoVela;
      const xDe = i => L.velas.x0 + (i - ini + 0.5) * anchoVela;
      this.xDe = xDe; this.ini = ini; this.fin = fin;
      ctx.save(); ctx.beginPath(); ctx.rect(0, L.cab, L.ejeX, L.velas.y1 - L.cab); ctx.clip();
      this._grilla(L, vis, xDe, anchoVela);
      if (this.op.verBandas) this._bandas(L, yDe);
      this._rayas(L, yDe);
      if (this.op.verGuiones) this._guiones(vis, xDe, yDe, anchoVela);
      if (this.op.verVwap) this._vwap(vis, xDe, yDe, ini);
      this._velas(vis, xDe, yDe, anchoVela);
      if (this.op.verMarcas) this._marcas(vis, xDe, yDe, anchoVela, ini);
      ctx.restore();
      if (this.op.verPerfil) this._perfil(L, yDe);
      this._ejePrecio(L, yDe);
      this._ejeTiempo(L, vis, xDe, anchoVela);
      if (L.cvdH) this._cvd(L, vis, xDe, anchoVela, ini);
      this._cabecera(L);
      if (this.mouse) this._crosshair(L, vis, xDe, yDe, anchoVela, ini);
      if (!this.vista.auto || this.vista.fijo) { ctx.font = "10px Consolas, monospace"; ctx.fillStyle = COL.tenue; ctx.textAlign = "right"; ctx.textBaseline = "bottom"; ctx.fillText((this.vista.fijo ? "desplazado · " : "") + (this.vista.auto ? "" : "escala manual · ") + "doble clic = vivo", L.velas.x1 - 6, L.velas.y1 - 4); }
    }

    _pasoPrecio() {
      const r = this.pmax - this.pmin; const objetivo = r / 10; const base = Math.pow(10, Math.floor(Math.log10(Math.max(1e-6, objetivo))));
      for (const m of [1, 2, 2.5, 5, 10]) if (base * m >= objetivo) return base * m;
      return base * 10;
    }
    _grilla(L, vis, xDe, w) {
      const ctx = this.ctx; ctx.lineWidth = 1;
      const paso = this._pasoPrecio();
      for (let p = Math.ceil(this.pmin / paso) * paso; p <= this.pmax; p += paso) { const y = Math.round(L.velas.y1 - (p - this.pmin) / (this.pmax - this.pmin) * (L.velas.y1 - L.velas.y0)) + 0.5; ctx.strokeStyle = COL.grilla; ctx.beginPath(); ctx.moveTo(L.velas.x0, y); ctx.lineTo(L.perfil.x1, y); ctx.stroke(); }
      const cada = Math.max(1, Math.ceil(80 / Math.max(1, w)));
      for (let i = 0; i < vis.length; i += cada) { const x = Math.round(xDe(this.ini + i)) + 0.5; ctx.strokeStyle = COL.grilla2; ctx.beginPath(); ctx.moveTo(x, L.velas.y0); ctx.lineTo(x, L.velas.y1); ctx.stroke(); }
    }

    _bandas(L, yDe) {
      const n = this.datos.niveles; if (!n || !n.doms) return;
      const ctx = this.ctx; const f = this.datos.futuro || 1;
      for (const d of n.doms) { const b = f * this.op.bandaPct / 100; const y1 = yDe(d.fut + b), y2 = yDe(d.fut - b); ctx.fillStyle = COL.banda; ctx.fillRect(L.velas.x0, y1, L.velas.x1 - L.velas.x0, y2 - y1); }
    }
    _raya(y, color, ancho, guion, x0, x1) { const ctx = this.ctx; ctx.save(); ctx.strokeStyle = color; ctx.lineWidth = ancho; ctx.setLineDash(guion || []); ctx.beginPath(); ctx.moveTo(x0, Math.round(y) + 0.5); ctx.lineTo(x1, Math.round(y) + 0.5); ctx.stroke(); ctx.restore(); }
    _rayas(L, yDe) {
      const n = this.datos.niveles; if (!n) return;
      const x0 = L.velas.x0, x1 = L.perfil.x1;
      const en = v => v != null && this.precio(v) >= this.pmin && this.precio(v) <= this.pmax;
      if (en(n.zeroOi)) this._raya(yDe(n.zeroOi), "rgba(160,160,170,0.5)", 1, [2, 3], x0, x1);
      if (en(n.zeroVol)) this._raya(yDe(n.zeroVol), COL.zero, 1.4, [6, 4], x0, x1);
      if (en(n.mpVol)) this._raya(yDe(n.mpVol), COL.pos, 1.5, [], x0, x1);
      if (en(n.mnVol)) this._raya(yDe(n.mnVol), COL.neg, 1.5, [], x0, x1);
      (n.pesadas || []).forEach(p => { if (en(p.fut)) this._raya(yDe(p.fut), COL.pesada, 1, [1, 3], x0, x1); });
      (n.doms || []).forEach((d, i) => { if (en(d.fut)) this._raya(yDe(d.fut), i === 0 ? COL.dom : COL.dom2, i === 0 ? 1.6 : 1.1, [], x0, x1); });
    }
    _guiones(vis, xDe, yDe, w) {
      const ctx = this.ctx; const g = Math.max(2, Math.min(4, Math.round(w * 0.5))), largo = Math.max(3, w * 0.9);
      for (let i = 0; i < vis.length; i++) {
        const v = vis[i], n = v.niv; if (!n) continue;
        const x = xDe(this.ini + i);
        [n.dom0, n.dom1].forEach((d, k) => { if (d == null) return; const y = yDe(d); if (y < this.L.velas.y0 || y > this.L.velas.y1) return; ctx.fillStyle = k === 0 ? COL.dom : COL.dom2; ctx.fillRect(x - largo / 2, y - (k === 0 ? g : g - 1) / 2, largo, k === 0 ? g : Math.max(1, g - 1)); });
        if (this.op.verZeroPorVela && n.zero_vol != null) { const y = yDe(n.zero_vol); if (y >= this.L.velas.y0 && y <= this.L.velas.y1) { ctx.fillStyle = COL.zero; ctx.beginPath(); ctx.arc(x, y, 1.2, 0, Math.PI * 2); ctx.fill(); } }
      }
    }
    _vwap(vis, xDe, yDe, ini) {
      const vw = this.datos.vwap; if (!vw || !vw.length) return;
      const ctx = this.ctx; ctx.strokeStyle = COL.vwap; ctx.lineWidth = 1.3; ctx.beginPath(); let abierto = false;
      for (let i = 0; i < vis.length; i++) { const v = vw[ini + i]; if (v == null) { abierto = false; continue; } const x = xDe(ini + i), y = yDe(v); if (!abierto) { ctx.moveTo(x, y); abierto = true; } else ctx.lineTo(x, y); }
      ctx.stroke();
    }
    _velas(vis, xDe, yDe, w) {
      const ctx = this.ctx; const cuerpo = Math.max(1, Math.floor(w * 0.64));
      for (let i = 0; i < vis.length; i++) {
        const v = vis[i], x = xDe(this.ini + i);
        const alc = v.c >= v.o; ctx.strokeStyle = alc ? COL.alcista : COL.bajista; ctx.fillStyle = alc ? COL.alcista : COL.bajista; ctx.lineWidth = 1;
        ctx.beginPath(); ctx.moveTo(Math.round(x) + 0.5, yDe(v.h)); ctx.lineTo(Math.round(x) + 0.5, yDe(v.l)); ctx.stroke();
        const y1 = yDe(Math.max(v.o, v.c)), y2 = yDe(Math.min(v.o, v.c));
        if (cuerpo <= 1) ctx.fillRect(Math.round(x), y1, 1, Math.max(1, y2 - y1));
        else if (alc) { ctx.fillStyle = COL.fondo; ctx.fillRect(x - cuerpo / 2, y1, cuerpo, Math.max(1, y2 - y1)); ctx.strokeRect(Math.round(x - cuerpo / 2) + 0.5, Math.round(y1) + 0.5, cuerpo - 1, Math.max(1, y2 - y1)); }
        else ctx.fillRect(x - cuerpo / 2, y1, cuerpo, Math.max(1, y2 - y1));
      }
    }
    _marcas(vis, xDe, yDe, w, ini) {
      const ms = this.datos.marcas; if (!ms || !ms.length) return;
      const ctx = this.ctx; const porT = new Map(); vis.forEach((v, i) => porT.set(v.t, ini + i));
      const dur = vis.length > 1 ? (vis[1].t - vis[0].t) : 60;
      for (const m of ms) {
        let idx = porT.get(m.t - dur); if (idx == null) idx = porT.get(m.t); if (idx == null) continue;
        const x = xDe(idx), col = m.lado > 0 ? COL.pos : COL.neg, v = vis[idx - ini];
        ctx.save(); ctx.strokeStyle = col; ctx.fillStyle = col; ctx.lineWidth = 1.5; ctx.font = "10px Consolas, monospace"; ctx.textAlign = "center";
        if ((m.tipo || "").startsWith("rebote")) { const y = yDe(m.dom || m.precio), r = 4; ctx.beginPath(); ctx.arc(x, y, r, 0, Math.PI * 2); ctx.stroke(); ctx.fillText(m.dz <= 1 ? "R1" : "R", x, m.lado > 0 ? y + r + 10 : y - r - 3); }
        else if ((m.tipo || "").startsWith("modelo")) { const y = yDe(m.lado > 0 ? v.l : v.h) + (m.lado > 0 ? 12 : -12), r = 5; ctx.beginPath(); ctx.moveTo(x, y - r); ctx.lineTo(x + r, y); ctx.lineTo(x, y + r); ctx.lineTo(x - r, y); ctx.closePath(); ctx.fill(); ctx.fillText("M " + (m.dz != null ? m.dz.toFixed(2) : ""), x, m.lado > 0 ? y + r + 10 : y - r - 3); }
        else { const y = yDe(m.lado > 0 ? v.l : v.h) + (m.lado > 0 ? 10 : -10), r = 4; ctx.fillStyle = COL.dom; ctx.beginPath(); if (m.lado > 0) { ctx.moveTo(x - r, y + r); ctx.lineTo(x + r, y + r); ctx.lineTo(x, y - r); } else { ctx.moveTo(x - r, y - r); ctx.lineTo(x + r, y - r); ctx.lineTo(x, y + r); } ctx.closePath(); ctx.fill(); ctx.fillText("tren", x, m.lado > 0 ? y + r + 10 : y - r - 3); }
        ctx.restore();
      }
    }
    _perfil(L, yDe) {
      const P = this.datos.perfil; if (!P || !P.length) return;
      const ctx = this.ctx; const x0 = L.perfil.x0, x1 = L.perfil.x1, W = x1 - x0;
      ctx.fillStyle = COL.panel; ctx.fillRect(x0, L.perfil.y0, W, L.perfil.y1 - L.perfil.y0);
      ctx.save(); ctx.beginPath(); ctx.rect(x0, L.perfil.y0, W, L.perfil.y1 - L.perfil.y0); ctx.clip();
      const convW = this.op.verConv ? Math.round(W * 0.28) : 0, gexW = W - convW - 6;
      const clave = this.op.tipoPerfil === "oi" ? "gexOi" : "gexVol";
      const vis = P.filter(s => this.precio(s.fut) >= this.pmin - 1 && this.precio(s.fut) <= this.pmax + 1);
      let maxG = 0, maxO = 0, maxC = 0;
      for (const s of vis) { maxG = Math.max(maxG, Math.abs(s[clave])); maxO = Math.max(maxO, Math.abs(s.gexOi)); maxC = Math.max(maxC, Math.abs(s.conv || 0)); }
      if (!maxG) maxG = 1; if (!maxC) maxC = 1; if (!maxO) maxO = 1;
      let alto = 6; if (vis.length > 1) { const ys = vis.map(s => yDe(s.fut)).sort((a, b) => a - b); let d = Infinity; for (let i = 1; i < ys.length; i++) d = Math.min(d, ys[i] - ys[i - 1]); alto = clamp(d * 0.72, 2, 14); }
      const xc = x0 + gexW * 0.5;
      ctx.strokeStyle = COL.grilla; ctx.beginPath(); ctx.moveTo(Math.round(xc) + 0.5, L.perfil.y0); ctx.lineTo(Math.round(xc) + 0.5, L.perfil.y1); ctx.stroke();
      ctx.font = "10px Consolas, monospace"; ctx.textBaseline = "middle";
      for (const s of vis) {
        const y = yDe(s.fut), g = s[clave]; const largo = Math.abs(g) / maxG * (gexW * 0.48);
        if (this.op.verOi && clave === "gexVol") { const lo = Math.abs(s.gexOi) / maxO * (gexW * 0.48); ctx.fillStyle = s.gexOi >= 0 ? "rgba(63,191,127,0.18)" : "rgba(229,72,77,0.18)"; ctx.fillRect(s.gexOi >= 0 ? xc : xc - lo, y - alto / 2 - 1, lo, alto + 2); }
        ctx.fillStyle = g >= 0 ? COL.pos : COL.neg; ctx.fillRect(g >= 0 ? xc : xc - largo, y - alto / 2, largo, alto);
        if (this.op.verPelotitas && s.antes) s.antes.forEach((a, k) => { if (a == null) return; const la = Math.abs(a) / maxG * (gexW * 0.48); const xa = a >= 0 ? xc + la : xc - la; const r = [1.6, 2.4, 3.2][2 - k]; ctx.fillStyle = "rgba(223,230,238,0.85)"; ctx.beginPath(); ctx.arc(xa, y, r, 0, Math.PI * 2); ctx.fill(); ctx.strokeStyle = g >= 0 ? COL.pos : COL.neg; ctx.lineWidth = 0.8; ctx.stroke(); });
        if (alto >= 5 && largo > 18) { ctx.fillStyle = COL.texto; ctx.textAlign = g >= 0 ? "left" : "right"; ctx.fillText(fmtB(g) + (s.dte != null && s.dte < 1 ? " 0DTE" : ""), g >= 0 ? xc + largo + 3 : xc - largo - 3, y); }
        if (convW && s.conv != null) { const cx = x1 - convW + convW * 0.5, lc = Math.abs(s.conv) / maxC * (convW * 0.45); ctx.fillStyle = s.conv >= 0 ? COL.conv : COL.conv2; ctx.fillRect(s.conv >= 0 ? cx : cx - lc, y - alto / 2, lc, alto); }
      }
      ctx.fillStyle = COL.tenue; ctx.textAlign = "center"; ctx.textBaseline = "top";
      ctx.fillText(clave === "gexVol" ? "GEX vol (OI detrás)" : "GEX OI", x0 + gexW / 2, L.perfil.y0 + 2);
      if (convW) ctx.fillText("convexidad", x1 - convW / 2, L.perfil.y0 + 2);
      ctx.restore();
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
    _ejeTiempo(L, vis, xDe, w) {
      const ctx = this.ctx; ctx.fillStyle = COL.fondo; ctx.fillRect(0, L.velas.y1 + L.cvdH, L.ejeX, L.ejeH);
      ctx.strokeStyle = COL.grilla; ctx.beginPath(); ctx.moveTo(0, L.velas.y1 + L.cvdH + 0.5); ctx.lineTo(L.ejeX, L.velas.y1 + L.cvdH + 0.5); ctx.stroke();
      ctx.font = "10.5px Consolas, monospace"; ctx.fillStyle = COL.eje; ctx.textAlign = "center"; ctx.textBaseline = "top";
      const cada = Math.max(1, Math.ceil(80 / Math.max(1, w))); const tz = this.op.zona; let diaAnt = null;
      for (let i = 0; i < vis.length; i += cada) {
        const t = new Date(vis[i].t * 1000); const dia = t.toLocaleDateString("es-AR", { day: "2-digit", month: "2-digit", timeZone: tz });
        let txt = t.toLocaleTimeString("es-AR", { hour: "2-digit", minute: "2-digit", hour12: false, timeZone: tz });
        if (dia !== diaAnt) { txt = (diaAnt === null ? dia + " " : dia + " ") + txt; diaAnt = dia; }
        ctx.fillText(txt, xDe(this.ini + i), L.velas.y1 + L.cvdH + 6);
      }
    }
    _cvd(L, vis, xDe, w, ini) {
      const ctx = this.ctx; const y0 = L.cvd.y0, y1 = L.cvd.y1;
      ctx.fillStyle = COL.panel; ctx.fillRect(L.cvd.x0, y0, L.cvd.x1 - L.cvd.x0, y1 - y0);
      const velas = this.datos.velas; let acum = 0; const cvd = new Array(velas.length);
      for (let i = 0; i < velas.length; i++) { if (i > 0 && (velas[i].t - velas[i - 1].t) > 3600 * 3) acum = 0; acum += velas[i].delta || 0; cvd[i] = acum; }
      let mn = Infinity, mx = -Infinity, md = 0;
      for (let i = 0; i < vis.length; i++) { mn = Math.min(mn, cvd[ini + i]); mx = Math.max(mx, cvd[ini + i]); md = Math.max(md, Math.abs(vis[i].delta || 0)); }
      if (!isFinite(mn)) return; if (mx === mn) mx = mn + 1;
      const yC = v => y1 - 4 - (v - mn) / (mx - mn) * (y1 - y0 - 8); const ym = (y0 + y1) / 2;
      for (let i = 0; i < vis.length; i++) { const d = vis[i].delta || 0; const h = md ? Math.abs(d) / md * (y1 - y0) * 0.45 : 0; ctx.fillStyle = d >= 0 ? "rgba(63,191,127,0.35)" : "rgba(229,72,77,0.35)"; ctx.fillRect(xDe(ini + i) - w * 0.3, d >= 0 ? ym - h : ym, Math.max(1, w * 0.6), h); }
      ctx.strokeStyle = COL.cvd; ctx.lineWidth = 1.4; ctx.beginPath();
      for (let i = 0; i < vis.length; i++) { const x = xDe(ini + i), y = yC(cvd[ini + i]); if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y); }
      ctx.stroke();
      ctx.font = "10px Consolas, monospace"; ctx.fillStyle = COL.tenue; ctx.textAlign = "left"; ctx.textBaseline = "top";
      ctx.fillText("CVD " + Math.round(cvd[this.fin] || 0).toLocaleString("es-AR") + "  ·  delta por vela  ·  máx " + Math.round(mx).toLocaleString("es-AR") + "  mín " + Math.round(mn).toLocaleString("es-AR"), L.cvd.x0 + 6, y0 + 3);
      ctx.fillStyle = COL.fondo; ctx.fillRect(L.ejeX, y0, L.W - L.ejeX, y1 - y0); ctx.fillStyle = COL.eje; ctx.textAlign = "left"; ctx.textBaseline = "middle";
      ctx.fillText(Math.round(mx).toLocaleString("es-AR"), L.ejeX + 5, y0 + 8); ctx.fillText(Math.round(mn).toLocaleString("es-AR"), L.ejeX + 5, y1 - 8);
    }
    _cabecera(L) {
      const ctx = this.ctx; if (!this.datos.cabecera.length) return;
      ctx.fillStyle = COL.fondo; ctx.fillRect(0, 0, L.ejeX, L.cab);
      ctx.font = "11.5px Consolas, monospace"; ctx.textBaseline = "top"; ctx.textAlign = "left";
      this.datos.cabecera.forEach((l, i) => { ctx.fillStyle = i === 0 ? COL.dom : COL.texto; ctx.fillText(l, 8, 5 + i * 14); });
    }
    _crosshair(L, vis, xDe, yDe, w, ini) {
      const m = this.mouse, ctx = this.ctx;
      if (m.x > L.ejeX || m.y < L.cab || m.y > L.velas.y1) return;
      const i = clamp(Math.floor((m.x - L.velas.x0) / w), 0, vis.length - 1); const v = vis[i]; if (!v) return;
      const x = xDe(ini + i);
      ctx.save(); ctx.strokeStyle = COL.crosshair; ctx.setLineDash([3, 3]); ctx.beginPath(); ctx.moveTo(x, L.cab); ctx.lineTo(x, L.velas.y1); ctx.moveTo(0, m.y); ctx.lineTo(L.perfil.x1, m.y); ctx.stroke(); ctx.restore();
      const precio = this.pmin + (L.velas.y1 - m.y) / (L.velas.y1 - L.velas.y0) * (this.pmax - this.pmin);
      const hora = new Date(v.t * 1000).toLocaleTimeString("es-AR", { hour: "2-digit", minute: "2-digit", hour12: false, timeZone: this.op.zona });
      const dec = this.op.decimales;
      let txt = hora + "  O " + fmtP(this.precio(v.o), dec) + "  H " + fmtP(this.precio(v.h), dec) + "  L " + fmtP(this.precio(v.l), dec) + "  C " + fmtP(this.precio(v.c), dec) + (v.vol != null ? "  vol " + v.vol : "") + (v.delta != null ? "  Δ " + v.delta : "");
      if (v.niv) txt += "   | en esa vela: D " + [v.niv.dom0, v.niv.dom1].filter(x => x != null).map(x => fmtP(this.precio(x), 0)).join("/") + (v.niv.zero_vol != null ? "  0Γ " + fmtP(this.precio(v.niv.zero_vol), 0) : "") + (v.niv.q_cuadrante ? "  q" + v.niv.q_cuadrante : "");
      ctx.font = "11px Consolas, monospace"; ctx.textBaseline = "top"; ctx.textAlign = "left";
      const ancho = ctx.measureText(txt).width + 12; const bx = clamp(m.x + 12, 0, Math.max(0, L.ejeX - ancho)), by = Math.max(L.cab + 2, m.y - 22);
      ctx.fillStyle = "rgba(14,20,27,0.92)"; ctx.fillRect(bx, by, ancho, 17); ctx.fillStyle = COL.texto; ctx.fillText(txt, bx + 6, by + 3);
      ctx.fillStyle = COL.texto; ctx.fillRect(L.ejeX, m.y - 7, L.W - L.ejeX, 14); ctx.fillStyle = "#0b0f14"; ctx.textBaseline = "middle"; ctx.fillText(fmtP(precio, dec), L.ejeX + 3, m.y);
      const tx = new Date(v.t * 1000).toLocaleTimeString("es-AR", { hour: "2-digit", minute: "2-digit", hour12: false, timeZone: this.op.zona });
      ctx.fillStyle = COL.texto; ctx.fillRect(x - 20, L.velas.y1 + L.cvdH + 2, 40, 16); ctx.fillStyle = "#0b0f14"; ctx.textAlign = "center"; ctx.fillText(tx, x, L.velas.y1 + L.cvdH + 10);
      if (this.op.alHover) this.op.alHover({ vela: v, precio });
    }
  }

  global.Grafico = Grafico; global.GraficoCOL = COL; global.fmtB = fmtB; global.fmtP = fmtP;
})(typeof window !== "undefined" ? window : globalThis);
