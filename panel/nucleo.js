/* NUCLEO DE GAMMA HOY EN JAVASCRIPT.
 *
 * La misma cuenta que GammaHoyNucleo.cs (el indicador de ATAS) y que estado_nube.py (la nube):
 * perfil por strike (GEX por volumen y por OI, convexidad), zero gamma de cada libro (grilla
 * +-3 %, 60 pasos, cruce interpolado), majors, dominantes (una por lado dentro del radio,
 * centroide gamma-ponderado), cuadrante (pico + convexidad en el precio) y Max Change contra
 * fotos anteriores. Corre en el navegador con la cadena cruda para que el operador pueda cambiar
 * los ajustes (horizonte, libro, radio, centroide) sin esperar a la nube.
 *
 * Si se cambia algo aca hay que cambiarlo en los otros dos. La equivalencia se prueba en
 * panel/pruebas.html y en la pestaña Auditoria (JS contra nube contra indicador, en strike).
 */
(function (global) {
  "use strict";
  const MULT = 100.0, PISO_DIAS = 1.0 / 1440.0, VENTANAS = [1, 5, 10, 15, 30];
  const DIVIDENDO = { NQ: 0.008, ES: 0.012, RTY: 0.012 };

  function ajustesDefault(raiz) {
    return {
      horizonte: "Hoy", cuantas: 2, radioDomPct: 2.0, radioCentro: 12.0, picoPct: 0.35, muchoPct: 50,
      convexidad: "Auto", unaPorLado: true, centroide: true, empatePct: 20, tasa: 0.0375, dividendo: DIVIDENDO[raiz] || 0.012,
      expFuturo: null, expFuturoAlt: null,
    };
  }

  function tercerViernes(anio, mes) {
    const d = new Date(Date.UTC(anio, mes - 1, 1));
    const corr = (5 - d.getUTCDay() + 7) % 7;     // viernes = 5 en JS (domingo = 0)
    return new Date(Date.UTC(anio, mes - 1, 1 + corr + 14, 13, 30));
  }

  function trimestralDesde(ahora) {
    const out = [];
    let a = ahora.getUTCFullYear(), m = ahora.getUTCMonth() + 1;
    for (let i = 0; i < 8 && out.length < 2; i++) {
      const mq = (Math.floor((m - 1) / 3) + 1) * 3;
      const f = tercerViernes(a, mq);
      if (f > ahora && (!out.length || f > out[out.length - 1])) out.push(f);
      m = mq + 1; if (m > 12) { m = 1; a += 1; }
    }
    return out;
  }

  function fi(x) { return Math.exp(-0.5 * x * x) / Math.sqrt(2 * Math.PI); }
  function N(x) {
    const s = x < 0 ? -1 : 1; x = Math.abs(x) / Math.SQRT2;
    const t = 1 / (1 + 0.3275911 * x);
    const y = 1 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.exp(-x * x);
    return 0.5 * (1 + s * y);
  }
  function gammaBs(S, K, T, iv, r) {
    if (S <= 0 || K <= 0 || T <= 0 || iv <= 0) return 0;
    const v = iv * Math.sqrt(T);
    const d1 = (Math.log(S / K) + (r + 0.5 * iv * iv) * T) / v;
    return fi(d1) / (S * v);
  }
  // Black-76 (opciones sobre el futuro: el libro de ES por Rithmic)
  function gammaB76(F, K, T, s, r) {
    if (F <= 0 || K <= 0 || T <= 0 || s <= 0) return 0;
    const v = s * Math.sqrt(T);
    const d1 = (Math.log(F / K) + 0.5 * s * s * T) / v;
    return Math.exp(-r * T) * fi(d1) / (F * v);
  }

  /* la cadena del feed (flaca): {generado, cadena:{ts, spot_idx, vencimientos:[{f,dias}], campos, filas:[[K,V,oiC,oiP,ivC,ivP,volC,volP]]}, base...} */
  function parsear(d) {
    const c = d && d.cadena; if (!c || !c.filas || !c.filas.length) return null;
    return {
      ts: c.ts || "", spotIdx: +c.spot_idx || 0, dias: (c.vencimientos || []).map(v => +v.dias || 0), vencs: (c.vencimientos || []).map(v => v.f),
      filas: c.filas.filter(a => a.length >= 8).map(a => ({ K: +a[0], V: a[1] | 0, OiC: +a[2], OiP: +a[3], IvC: +a[4], IvP: +a[5], VolC: +a[6], VolP: +a[7] })),
      base: +d.base || 0, baseConfiable: !!d.base_confiable, baseCruda: +d.base_cruda || 0, baseErrorTicks: +d.base_error_ticks || 0,
      baseUltimaBuena: +d.base_ultima_buena || 0, baseUltimaBuenaEdad: +d.base_ultima_buena_edad_min || 0, edadMin: +d.edad_min || 0,
      generado: d.generado ? new Date(d.generado) : null, ultimoTrade: c.ultimo_trade, esFuturo: false, fuente: d.fuente || "CBOE",
    };
  }

  /* la cadena VIVA de Rithmic que graba el indicador (viva-ES-<dia>.jsonl): una linea por minuto,
   * campos strike,dias,es_call,oi,iv,bid,ask,vol_hoy,vol_cinta,vol_compra,vol_venta; opciones sobre el
   * futuro (Black-76, sin base). */
  function parsearViva(l) {
    if (!l || !l.filas || !l.filas.length) return null;
    const ix = {}; (l.campos || "strike,dias,es_call,oi,iv,bid,ask,vol_hoy,vol_cinta,vol_compra,vol_venta").split(",").forEach((k, i) => ix[k] = i);
    const diasSet = Array.from(new Set(l.filas.map(f => +f[ix.dias]))).sort((a, b) => a - b);
    const vidx = {}; diasSet.forEach((d, i) => vidx[d] = i);
    const por = {};
    for (const f of l.filas) {
      const K = +f[ix.strike], dd = +f[ix.dias], k = K + "|" + dd;
      const s = por[k] || (por[k] = { K, V: vidx[dd], OiC: 0, OiP: 0, IvC: 0, IvP: 0, VolC: 0, VolP: 0, bidC: 0, askC: 0, bidP: 0, askP: 0, compra: 0, venta: 0 });
      const esCall = +f[ix.es_call] === 1, oi = +f[ix.oi] || 0, iv = +f[ix.iv] || 0, vol = +f[ix.vol_hoy] || 0;
      if (esCall) { s.OiC += oi; s.IvC = iv; s.VolC += vol; s.bidC = +f[ix.bid]; s.askC = +f[ix.ask]; }
      else { s.OiP += oi; s.IvP = iv; s.VolP += vol; s.bidP = +f[ix.bid]; s.askP = +f[ix.ask]; }
      s.compra += +f[ix.vol_compra] || 0; s.venta += +f[ix.vol_venta] || 0;
    }
    const ts = l.ts ? new Date(l.ts.replace(" ", "T") + "Z") : null;
    return { ts: l.ts || "", spotIdx: +l.futuro || 0, dias: diasSet, vencs: diasSet.map(d => d.toFixed(3) + " d"), filas: Object.values(por),
             base: 0, baseConfiable: true, baseCruda: 0, baseErrorTicks: 0, baseUltimaBuena: 0, baseUltimaBuenaEdad: 0, edadMin: 0,
             generado: ts, ultimoTrade: l.ts, esFuturo: true, fuente: "Rithmic ES (grabado por el indicador)", grandes: l.grandes };
  }

  function gex(f, S, T, r, porVolumen, esFut) {
    const gC = esFut ? gammaB76(S, f.K, T, f.IvC, r) : gammaBs(S, f.K, T, f.IvC, r);
    const gP = esFut ? gammaB76(S, f.K, T, f.IvP, r) : gammaBs(S, f.K, T, f.IvP, r);
    const wC = porVolumen ? f.VolC : f.OiC, wP = porVolumen ? f.VolP : f.OiP;
    return (gC * wC - gP * wP) * MULT * S * S * 0.01;
  }
  function pasaHorizonte(A, dias, masCerca) {
    if (A.horizonte === "Hoy") return dias >= 0 && dias <= Math.max(1.0, masCerca + 0.01);
    if (A.horizonte === "Semana") return dias >= 0 && dias <= Math.max(7.0, masCerca + 0.01);
    return dias >= 0;
  }
  function envejecer(c, ahora) {
    if (!c.generado || ahora <= c.generado) return 0;
    return Math.min(2.0, (ahora - c.generado) / 86400000);
  }
  function cruce(A, c, S, r, masCerca, env, porVolumen) {
    const lo = S * 0.97, hi = S * 1.03, pasos = 60;
    const filas = [];
    for (const f of c.filas) {
      if (f.V < 0 || f.V >= c.dias.length) continue;
      const dd = c.dias[f.V] - env;
      if (!pasaHorizonte(A, dd, masCerca)) continue;
      filas.push([f, Math.max(dd, PISO_DIAS) / 365]);
    }
    let ant = null, xAnt = 0;
    for (let i = 0; i <= pasos; i++) {
      const x = lo + (hi - lo) * i / pasos; let t = 0;
      for (const [f, T] of filas) t += gex(f, x, T, r, porVolumen, c.esFuturo);
      if (ant !== null && ((ant < 0 && t >= 0) || (ant > 0 && t <= 0))) return t !== ant ? xAnt + (x - xAnt) * (-ant) / (t - ant) : x;
      ant = t; xAnt = x;
    }
    return null;
  }

  function elegirBase(A, c, futuro, ahora, baseRueda, edadRueda) {
    let carry = null, carryAlt = null;
    if (A.expFuturo && A.expFuturo > ahora) carry = futuro * (A.tasa - A.dividendo) * ((A.expFuturo - ahora) / 86400000) / 365;
    if (A.expFuturoAlt && A.expFuturoAlt > ahora) carryAlt = futuro * (A.tasa - A.dividendo) * ((A.expFuturoAlt - ahora) / 86400000) / 365;
    const cerca = (b, k) => Math.abs(b - k) <= Math.max(Math.abs(k) * 0.6, futuro * 0.0006);
    const razMed = b => carry === null || cerca(b, carry) || (carryAlt !== null && cerca(b, carryAlt));
    const raz = b => carry === null || cerca(b, carry);
    if (c.esFuturo) return { base: 0, origen: "libro ES (Rithmic), sin base", carry };
    if (c.baseConfiable && c.base !== 0 && razMed(c.base)) return { base: c.base, origen: "medida", carry };
    if (c.baseUltimaBuena !== 0 && c.baseUltimaBuenaEdad <= 360 && razMed(c.baseUltimaBuena)) return { base: c.baseUltimaBuena, origen: "medida hace " + c.baseUltimaBuenaEdad.toFixed(0) + " min", carry };
    if (baseRueda != null && edadRueda != null && edadRueda <= 24 * 60 && raz(baseRueda)) return { base: baseRueda, origen: "de la rueda hace " + edadRueda.toFixed(0) + " min", carry };
    if (c.baseCruda !== 0 && raz(c.baseCruda)) return { base: c.baseCruda, origen: "CRUDA " + c.baseErrorTicks.toFixed(0) + " ticks", carry };
    if (carry !== null) return { base: carry, origen: "TEORICA carry " + carry.toFixed(1) + (c.baseCruda !== 0 ? " (cruda " + c.baseCruda.toFixed(1) + " descartada)" : ""), carry };
    if (c.baseCruda !== 0) return { base: c.baseCruda, origen: "CRUDA " + c.baseErrorTicks.toFixed(0) + " ticks (sin cota)", carry };
    return { base: null, origen: "sin base", carry };
  }

  function perfilK(A, c, S, ahora) {
    const r = A.tasa, Sup = S * 1.01, env = envejecer(c, ahora);
    let mas = Infinity;
    for (const d of c.dias) { const dd = d - env; if (dd >= 0 && dd < mas) mas = dd; }
    if (mas === Infinity) mas = 0;
    const por = new Map();
    for (const f of c.filas) {
      if (f.V < 0 || f.V >= c.dias.length) continue;
      const dias = c.dias[f.V] - env;
      if (!pasaHorizonte(A, dias, mas)) continue;
      const T = Math.max(dias, PISO_DIAS) / 365;
      const gOi = gex(f, S, T, r, false, c.esFuturo), gVol = gex(f, S, T, r, true, c.esFuturo);
      const gOiUp = gex(f, Sup, T, r, false, c.esFuturo), gVolUp = gex(f, Sup, T, r, true, c.esFuturo);
      if (gOi === 0 && gVol === 0) continue;
      let s = por.get(f.K);
      if (!s) { s = { K: f.K, gexOi: 0, gexVol: 0, oi: 0, vol: 0, ivSum: 0, ivW: 0, dte: 1e9, convVol: 0, convOi: 0, oiC: 0, oiP: 0, volC: 0, volP: 0 }; por.set(f.K, s); }
      s.gexOi += gOi; s.gexVol += gVol; s.oi += f.OiC + f.OiP; s.vol += f.VolC + f.VolP;
      s.oiC += f.OiC; s.oiP += f.OiP; s.volC += f.VolC; s.volP += f.VolP;
      const wc = f.OiC + f.VolC, wp = f.OiP + f.VolP;
      if (f.IvC > 0) { s.ivSum += f.IvC * wc; s.ivW += wc; }
      if (f.IvP > 0) { s.ivSum += f.IvP * wp; s.ivW += wp; }
      if (dias < s.dte) s.dte = dias;
      s.convVol += gVolUp - gVol; s.convOi += gOiUp - gOi;
    }
    return { perfil: Array.from(por.values()).sort((a, b) => a.K - b.K), mas, env };
  }

  /* fotos: Map(minuto -> Map(K -> gexVol)) o un objeto {minuto: {K: gexVol}} */
  function calcular(A, c, futuro, ahora, fotos, baseRueda, edadRueda) {
    if (!c || !c.filas.length || !c.dias.length || !(futuro > 0)) return null;
    const eb = elegirBase(A, c, futuro, ahora, baseRueda, edadRueda);
    if (eb.base === null) return { sinBase: true, baseOrigen: eb.origen };
    const base = eb.base, S = futuro - base;
    if (S <= 0) return null;
    const { perfil, mas, env } = perfilK(A, c, S, ahora);
    for (const s of perfil) s.fut = s.K + base;
    let sumVol = 0, sumOi = 0; for (const s of perfil) { sumVol += Math.abs(s.gexVol); sumOi += Math.abs(s.gexOi); }
    const convPorVol = A.convexidad === "Volumen" || (A.convexidad === "Auto" && sumVol >= 0.2 * sumOi && sumVol > 0);
    for (const s of perfil) s.conv = convPorVol ? s.convVol : s.convOi;
    let netVol = 0, netOi = 0, maxAbsVol = 0, maxAbsOi = 0, maxAbsConv = 0;
    for (const s of perfil) { netVol += s.gexVol; netOi += s.gexOi; maxAbsVol = Math.max(maxAbsVol, Math.abs(s.gexVol)); maxAbsOi = Math.max(maxAbsOi, Math.abs(s.gexOi)); maxAbsConv = Math.max(maxAbsConv, Math.abs(s.conv)); }
    const zv = cruce(A, c, S, A.tasa, mas, env, true), zo = cruce(A, c, S, A.tasa, mas, env, false);
    const zeroVol = zv === null ? null : zv + base, zeroOi = zo === null ? null : zo + base;
    const mejor = (k, pos) => { let b = null; for (const s of perfil) { if (pos ? s[k] > 0 : s[k] < 0) { if (!b || (pos ? s[k] > b[k] : s[k] < b[k])) b = s; } } return b ? b.fut : null; };
    const mpVol = mejor("gexVol", true), mnVol = mejor("gexVol", false), mpOi = mejor("gexOi", true), mnOi = mejor("gexOi", false);
    // dominantes
    const radio = futuro * A.radioDomPct / 100, cuantas = Math.max(1, A.cuantas);
    let libroDom = "vol";
    let en = perfil.filter(s => Math.abs(s.fut - futuro) <= radio && Math.abs(s.gexVol) > 0);
    let cand = en.slice().sort((a, b) => Math.abs(b.gexVol) - Math.abs(a.gexVol)).slice(0, cuantas).map(s => [s.fut, s.gexVol]);
    if (!cand.length) {
      libroDom = "OI";
      en = perfil.filter(s => Math.abs(s.fut - futuro) <= radio && Math.abs(s.gexOi) > 0);
      cand = en.slice().sort((a, b) => Math.abs(b.gexOi) - Math.abs(a.gexOi)).slice(0, cuantas).map(s => [s.fut, s.gexOi]);
    }
    const gk = libroDom === "vol" ? "gexVol" : "gexOi", peso = s => Math.abs(s[gk]);
    if (A.unaPorLado && perfil.length) {
      const enRadio = perfil.filter(s => Math.abs(s.fut - futuro) <= radio && peso(s) > 0);
      // la mas fuerte de cada lado; con EMPATE TECNICO (Gamma Hoy 1.8i): si dos barras del
      // mismo lado estan dentro de empatePct de la mas grande, gana la MAS CERCANA al precio
      const elegir = lado => {
        if (!lado.length) return null;
        const pmax = Math.max(...lado.map(peso));
        const piso = pmax * (1 - Math.max(0, Math.min(90, A.empatePct == null ? 20 : A.empatePct)) / 100);
        return lado.filter(s => peso(s) >= piso).sort((a, b) => Math.abs(a.fut - futuro) - Math.abs(b.fut - futuro) || peso(b) - peso(a))[0];
      };
      const arriba = elegir(enRadio.filter(s => s.fut > futuro)), abajo = elegir(enRadio.filter(s => s.fut <= futuro));
      const lados = [];
      if (arriba) lados.push([arriba.fut, arriba[gk]]);
      if (abajo) lados.push([abajo.fut, abajo[gk]]);
      for (const s of enRadio.slice().sort((a, b) => peso(b) - peso(a))) {
        if (lados.length >= cuantas) break;
        if (lados.some(l => l[0] === s.fut)) continue;
        lados.push([s.fut, s[gk]]);
      }
      if (lados.length) cand = lados;
    }
    const domsStrike = cand.map(x => x.slice());
    if (A.centroide && cand.length) {
      cand = cand.map(([f0, g0]) => {
        let sw = 0, sx = 0;
        for (const s of perfil) { if (Math.abs(s.fut - f0) > A.radioCentro) continue; const w = peso(s); if (w <= 0) continue; sw += w; sx += w * s.fut; }
        return [sw > 0 ? sx / sw : f0, g0];
      });
    }
    // cuadrante
    const rPico = futuro * A.picoPct / 100;
    const cerca = perfil.filter(s => Math.abs(s.fut - futuro) <= rPico);
    const porVolCuad = sumVol > 0 && sumVol >= 0.2 * sumOi, gq = porVolCuad ? "gexVol" : "gexOi";
    let picoGex = 0, picoFut = null, convPrecio = 0;
    for (const s of cerca) { if (Math.abs(s[gq]) > Math.abs(picoGex)) { picoGex = s[gq]; picoFut = s.fut; } convPrecio += s.conv; }
    if (!cerca.length && perfil.length) { let v = perfil[0]; for (const s of perfil) if (Math.abs(s.fut - futuro) < Math.abs(v.fut - futuro)) v = s; convPrecio = v.conv; }
    const maxLibro = porVolCuad ? maxAbsVol : maxAbsOi;
    const mucho = maxLibro > 0 && Math.abs(picoGex) >= maxLibro * A.muchoPct / 100, convPos = convPrecio >= 0;
    let q, cuadrante, corto;
    if (mucho && convPos) { q = 1; cuadrante = "iman colchon: rango, reversion"; corto = "IMAN"; }
    else if (mucho && !convPos) { q = 2; cuadrante = "nivel explosivo: ruptura, momentum"; corto = "EXPLOSIVO"; }
    else if (!mucho && convPos) { q = 3; cuadrante = "mercado estable: rangos amplios"; corto = "ESTABLE"; }
    else { q = 4; cuadrante = "salvese quien pueda: tendencia, tamaño chico"; corto = "RIESGO"; }
    // max change
    const minuto = Math.floor(ahora.getTime() / 60000);
    const mc = [], antes = new Map();
    if (fotos) {
      const mins = (fotos instanceof Map ? Array.from(fotos.keys()) : Object.keys(fotos).map(Number)).sort((a, b) => a - b);
      const foto = m => fotos instanceof Map ? fotos.get(m) : fotos[m];
      const get = (f, K) => f instanceof Map ? (f.get(K) || 0) : (f[K] || 0);
      for (const w of VENTANAS) {
        let vieja = null; for (const m of mins) if (m <= minuto - w) vieja = foto(m);
        let best = 0, futM = null;
        if (vieja) for (const s of perfil) { const d = s.gexVol - get(vieja, s.K); if (Math.abs(d) > Math.abs(best)) { best = d; futM = s.fut; } }
        mc.push({ min: w, fut: futM, delta: best });
      }
      for (const w of [1, 5, 15]) {
        let vieja = null; for (const m of mins) if (m <= minuto - w) vieja = foto(m);
        if (vieja) for (const s of perfil) { if (!antes.has(s.K)) antes.set(s.K, {}); antes.get(s.K)[w] = get(vieja, s.K); }
      }
    }
    return { futuro, S, base, baseOrigen: eb.origen, carry: eb.carry, masCerca: mas, envejecido: env, perfil, netVol, netOi, zeroVol, zeroOi, mpVol, mnVol, mpOi, mnOi,
             maxAbsVol, maxAbsOi, maxAbsConv, doms: cand, domsStrike, libroDom, libroConv: convPorVol ? "vol" : "OI", q, cuadrante, corto, picoFut, picoGex, convPrecio, mucho, mc, antes, strikes: perfil.length };
  }

  function audit(L, c) {
    const f = (v, d = 2) => v == null ? "NaN" : v.toFixed(d);
    const mc30 = (L.mc || []).find(m => m.min === 30);
    return "AUDIT fut=" + f(L.futuro) + " S=" + f(L.S) + " base=" + f(L.base) + " origen=" + L.baseOrigen.replace(/ /g, "_") + " strikes=" + L.strikes +
      " netVol=" + (L.netVol / 1e9).toFixed(3) + "B netOi=" + (L.netOi / 1e9).toFixed(3) + "B zeroVol=" + f(L.zeroVol) + " zeroOi=" + f(L.zeroOi) + " mpVol=" + f(L.mpVol) + " mnVol=" + f(L.mnVol) +
      " doms=" + L.doms.map(d => d[0].toFixed(2) + "=" + (d[1] / 1e6).toFixed(0) + "M").join("/") + " libroDom=" + L.libroDom + " conv=" + L.libroConv + " q=" + L.q + " pico=" + f(L.picoFut) +
      " picoGex=" + (L.picoGex / 1e6).toFixed(0) + "M mucho=" + (L.mucho ? "True" : "False") + " convPrecio=" + (L.convPrecio / 1e6).toFixed(0) + "M mc30=" + (mc30 ? f(mc30.fut) : "NaN") + ":" + (mc30 ? (mc30.delta / 1e6).toFixed(0) : "0") + "M edadFeed=" + (c.edadMin || 0).toFixed(1) + "min";
  }

  /* por vencimiento, muros por OI/volumen del mas cercano, put/call, movimiento esperado 1 sigma por IV, max pain, sonrisa */
  function extras(A, c, S, base, ahora) {
    const r = A.tasa, env = envejecer(c, ahora), dias = c.dias.map(d => d - env);
    const porV = new Map();
    for (const f of c.filas) {
      if (f.V < 0 || f.V >= dias.length || dias[f.V] < 0) continue;
      const T = Math.max(dias[f.V], PISO_DIAS) / 365;
      let v = porV.get(f.V); if (!v) { v = { i: f.V, f: c.vencs[f.V], dias: dias[f.V], gexVol: 0, gexOi: 0, oiC: 0, oiP: 0, volC: 0, volP: 0, ivs: [] }; porV.set(f.V, v); }
      v.gexVol += gex(f, S, T, r, true, c.esFuturo); v.gexOi += gex(f, S, T, r, false, c.esFuturo);
      v.oiC += f.OiC; v.oiP += f.OiP; v.volC += f.VolC; v.volP += f.VolP;
      if (Math.abs(f.K - S) <= S * 0.01 && (f.IvC > 0 || f.IvP > 0)) v.ivs.push([Math.abs(f.K - S), (f.IvC + f.IvP) / ((f.IvC > 0 && f.IvP > 0) ? 2 : 1)]);
    }
    const vencs = Array.from(porV.values()).sort((a, b) => a.dias - b.dias).map(v => {
      const atm = v.ivs.sort((a, b) => a[0] - b[0]).slice(0, 4);
      const iv = atm.length ? atm.reduce((s, x) => s + x[1], 0) / atm.length : null;
      const T = Math.max(v.dias, PISO_DIAS) / 365;
      return { f: v.f, dias: v.dias, gexVol: v.gexVol, gexOi: v.gexOi, oiC: v.oiC, oiP: v.oiP, volC: v.volC, volP: v.volP, pc_oi: v.oiC > 0 ? v.oiP / v.oiC : null, pc_vol: v.volC > 0 ? v.volP / v.volC : null, iv_atm: iv, em_1sigma: iv ? S * iv * Math.sqrt(T) : null, i: v.i };
    });
    let muros = {}, maxpain = null, sonrisa = [];
    const cero = vencs.length ? vencs[0] : null;
    if (cero) {
      const filas0 = c.filas.filter(f => f.V === cero.i);
      const muro = attr => { let b = null; for (const f of filas0) if (f[attr] > 0 && (!b || f[attr] > b[attr])) b = f; return b ? { K: b.K, fut: b.K + base, n: b[attr] } : null; };
      muros = { call_oi: muro("OiC"), put_oi: muro("OiP"), call_vol: muro("VolC"), put_vol: muro("VolP") };
      const Ks = Array.from(new Set(filas0.filter(f => Math.abs(f.K - S) <= S * 0.03).map(f => f.K))).sort((a, b) => a - b);
      if (Ks.length) {
        const dolor = K => filas0.reduce((s, f) => s + f.OiC * Math.max(0, K - f.K) + f.OiP * Math.max(0, f.K - K), 0);
        let kmp = Ks[0], dm = dolor(kmp); for (const K of Ks) { const d = dolor(K); if (d < dm) { dm = d; kmp = K; } }
        maxpain = { K: kmp, fut: kmp + base };
        for (const f of filas0.slice().sort((a, b) => a.K - b.K)) if (Math.abs(f.K - S) <= S * 0.03 && (f.IvC > 0 || f.IvP > 0)) sonrisa.push([f.K, f.IvC || null, f.IvP || null]);
      }
    }
    let oiC = 0, oiP = 0, vC = 0, vP = 0; for (const f of c.filas) { oiC += f.OiC; oiP += f.OiP; vC += f.VolC; vP += f.VolP; }
    return { vencimientos: vencs, muros_0dte: muros, maxpain_0dte: maxpain, sonrisa_0dte: sonrisa, pc_oi: oiC ? oiP / oiC : null, pc_vol: vC ? vP / vC : null };
  }

  global.Nucleo = { ajustesDefault, trimestralDesde, tercerViernes, parsear, parsearViva, gammaBs, gammaB76, gex, cruce, elegirBase, perfilK, calcular, audit, extras, envejecer, VENTANAS, MULT, PISO_DIAS };
})(typeof window !== "undefined" ? window : globalThis);
