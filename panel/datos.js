/* FUENTES DE DATOS DE LA WEB Y SU FUSION.
 *
 * Tres caminos, del mas fresco al mas lento, y cada uno dice su edad:
 *   VIVO   lo que el indicador Gamma Hoy escribe en la PC del operador y herramientas/subir_vivo.py
 *          sube cada 20 s en vivo.json (latido + todos los graficos + la cadena viva de Rithmic).
 *          Velas con order flow (delta, big trades), los niveles de cada vela tal como se dibujaron,
 *          los disparos.
 *   NUBE   la cadena de CBOE que cadenas.yml baja cada minuto (ultima-<RAIZ>.json), el estado que
 *          estado_nube.py calcula con ella (estado-<RAIZ>.json), la historia del dia
 *          (serie-<RAIZ>-<dia>.jsonl) y las velas de Yahoo (velas-<RAIZ>.json, con retraso, sin delta).
 *   JS     el nucleo en el navegador (nucleo.js) recalcula el perfil con la cadena y los AJUSTES del
 *          operador, asi los cambios de horizonte/libro/radio no esperan a nadie.
 * Si la PC esta apagada, VIVO no llega (se dice cuanto hace) y la web sigue con NUBE + JS.
 */
(function (global) {
  "use strict";
  // raw.githubusercontent.com cachea 5 minutos aunque cambie la query, y jsDelivr se sirve de ese mismo
  // cache (medido 10-09: el purge traia la copia vieja y quedaba pegada 12 h). Lo unico fresco de verdad
  // es la URL por COMMIT: raw.githubusercontent.com/<repo>/<sha>/<archivo>, que es inmutable y por eso
  // nunca esta vieja. El sha de la rama se pregunta a la API publica cada 75 s (48 pedidos por hora, el
  // limite sin token es 60). Si la API falla o se agota, se cae a raw por rama (5 min de cache).
  const REPO = "waltermosqueda/PythiaGex", RAMA = "cadenas";
  const BASE_DEF = "auto";
  const BASE_RESPALDO = "https://raw.githubusercontent.com/" + REPO + "/" + RAMA + "/";
  const CADA_SHA_MS = 75000;
  let shaCache = { sha: null, t: 0, fallos: 0 };
  async function shaRama() {
    const ahora = Date.now();
    if (ahora - shaCache.t < CADA_SHA_MS) return shaCache.sha;
    shaCache.t = ahora;
    try {
      const r = await fetch("https://api.github.com/repos/" + REPO + "/branches/" + RAMA, { headers: { Accept: "application/vnd.github+json" }, cache: "no-store" });
      if (r.ok) { const j = await r.json(); shaCache.sha = j.commit && j.commit.sha ? j.commit.sha : shaCache.sha; shaCache.fallos = 0; }
      else shaCache.fallos++;
    } catch (e) { shaCache.fallos++; }
    return shaCache.sha;
  }
  const RAIZ = { MNQ: "NQ", MES: "ES", NQ: "NQ", ES: "ES" };
  const cache = new Map();

  async function traer(base, nombre, maxEdadMs) {
    const k = base + nombre, ahora = Date.now();
    const c = cache.get(k);
    if (c && maxEdadMs && ahora - c.t < maxEdadMs) return c.v;
    try {
      let r = null;
      if (base === BASE_DEF) {
        const sha = await shaRama();
        if (sha) r = await fetch("https://raw.githubusercontent.com/" + REPO + "/" + sha + "/" + nombre, { cache: "no-store" }).catch(() => null);
        if (!r || !r.ok) r = await fetch(BASE_RESPALDO + nombre + "?v=" + ahora, { cache: "no-store" }).catch(() => null);
      } else r = await fetch(k + "?v=" + ahora, { cache: "no-store" }).catch(() => null);
      if (!r || !r.ok) throw new Error(r ? r.status : "red");
      const txt = await r.text();
      const v = nombre.endsWith(".jsonl") ? txt.split("\n").filter(l => l.length > 2).map(l => { try { return JSON.parse(l); } catch (e) { return null; } }).filter(Boolean) : JSON.parse(txt);
      cache.set(k, { t: ahora, v });
      return v;
    } catch (e) {
      if (c) return c.v;
      return null;
    }
  }

  function edadMin(iso) { if (!iso) return null; const t = new Date(iso).getTime(); return isFinite(t) ? (Date.now() - t) / 60000 : null; }
  function diaUtc(d) { return (d || new Date()).toISOString().slice(0, 10); }

  /* un grafico de vivo.json -> velas [{t,o,h,l,c,vol,delta,spot,niv:{...},of:{...}}] */
  function velasDeVivo(g, cn, co) {
    if (!g || !g.velas || !g.velas.t) return [];
    const V = g.velas, out = [];
    for (let i = 0; i < V.t.length; i++) {
      const niv = {}; (V.niv[i] || []).forEach((x, k) => { if (x != null) niv[cn[k]] = x; });
      const of = {}; (V.of[i] || []).forEach((x, k) => { if (x != null) of[co[k]] = x; });
      out.push({ t: V.t[i], o: V.o[i], h: V.h[i], l: V.l[i], c: V.c[i], vol: V.vol[i], delta: V.delta[i], spot: V.spot[i], niv: Object.keys(niv).length ? niv : null, of });
    }
    return out;
  }

  /* velas-<RAIZ>.json (Yahoo) -> velas sin delta; los niveles por vela salen de la serie del dia */
  function velasDeYahoo(y, serie) {
    if (!y || !y.futuro || !y.futuro.t) return [];
    const F = y.futuro, out = [];
    const porMin = new Map();
    (serie || []).forEach(s => { const t = new Date(s.t).getTime(); if (isFinite(t)) porMin.set(Math.floor(t / 60000), s); });
    let ult = null;
    for (let i = 0; i < F.t.length; i++) {
      const m = Math.floor(F.t[i] / 60);
      const s = porMin.get(m) || porMin.get(m - 1) || porMin.get(m - 2) || porMin.get(m - 3) || porMin.get(m - 4) || porMin.get(m - 5);
      if (s) ult = s;
      const niv = ult ? { zero_vol: ult.zeroVol, zero_oi: ult.zeroOi, mp_vol: ult.mpVol, mn_vol: ult.mnVol, dom0: ult.dom0, dom1: ult.dom1, pico: ult.pico, mc30: ult.mc30, q_cuadrante: ult.q } : null;
      out.push({ t: F.t[i], o: F.o[i], h: F.h[i], l: F.l[i], c: F.c[i], vol: F.v[i], delta: null, spot: ult ? ult.S : null, niv, of: null });
    }
    return out;
  }

  function agregar(velas, minutos) {
    if (!velas.length || minutos <= 1) return velas;
    const out = []; let cur = null, clave = null;
    for (const v of velas) {
      const k = Math.floor(v.t / (minutos * 60));
      if (k !== clave) { if (cur) out.push(cur); clave = k; cur = { t: k * minutos * 60, o: v.o, h: v.h, l: v.l, c: v.c, vol: v.vol || 0, delta: v.delta, spot: v.spot, niv: v.niv, of: v.of }; }
      else { cur.h = Math.max(cur.h, v.h); cur.l = Math.min(cur.l, v.l); cur.c = v.c; cur.vol += v.vol || 0; if (v.delta != null) cur.delta = (cur.delta || 0) + v.delta; cur.spot = v.spot ?? cur.spot; cur.niv = v.niv || cur.niv; cur.of = v.of || cur.of; }
    }
    if (cur) out.push(cur);
    return out;
  }

  /* VWAP de sesion: desde 13:30 UTC (rueda) o desde la reapertura de Globex (22:00 UTC) */
  function vwap(velas, modo) {
    if (modo === "off") return [];
    const out = new Array(velas.length); let pv = 0, vv = 0, sesion = null;
    for (let i = 0; i < velas.length; i++) {
      const v = velas[i]; const d = new Date(v.t * 1000); const hm = d.getUTCHours() * 60 + d.getUTCMinutes();
      const clave = modo === "rueda" ? (hm >= 810 ? d.toISOString().slice(0, 10) : null) : (hm >= 1320 ? d.toISOString().slice(0, 10) + "n" : new Date((v.t - 22 * 3600) * 1000).toISOString().slice(0, 10) + "n");
      if (clave !== sesion) { sesion = clave; pv = 0; vv = 0; }
      if (clave === null || !(v.vol > 0)) { out[i] = null; continue; }
      const tp = (v.h + v.l + v.c) / 3; pv += tp * v.vol; vv += v.vol; out[i] = pv / vv;
    }
    return out;
  }

  const MINUTOS = { M1: 1, M2: 2, M3: 3, M5: 5, M10: 10, M15: 15, M30: 30, M60: 60 };

  /* todo lo que la web necesita para un instrumento, en una pasada */
  async function cargar(inst, marco, ajustes, base) {
    base = base || BASE_DEF;
    const raiz = RAIZ[inst] || "NQ";
    const dia = diaUtc();
    const [vivoTodo, feed, estado, yahoo, serie] = await Promise.all([
      traer(base, "vivo.json", 8000),
      traer(base, "ultima-" + raiz + ".json", 45000), traer(base, "estado-" + raiz + ".json", 45000), traer(base, "velas-" + raiz + ".json", 45000),
      traer(base, "serie-" + raiz + "-" + dia + ".jsonl", 45000),
    ]);
    const pc = vivoTodo ? Object.assign({ generado: vivoTodo.generado, subidos: vivoTodo.resumen }, vivoTodo.latido || {}) : null;
    const edadPc = vivoTodo ? edadMin(vivoTodo.generado) : null;
    const vivoFrescoTodo = edadPc != null && edadPc <= 3;
    const cn = vivoTodo ? vivoTodo.claves_niv : [], co = vivoTodo ? vivoTodo.claves_of : [];
    const g = vivoFrescoTodo && vivoTodo.graficos ? vivoTodo.graficos[inst + "-" + marco] : null;
    // si el marco pedido no esta abierto en ATAS, se toma el mas fino que si este del mismo
    // instrumento y se agrupa (antes solo se miraba el de 1 min: con MNQ-M2/M5 abiertos y M1
    // cerrado la web decia "PC sin señal" con el latido a 0 min; 2026-09-11)
    let g1 = null, marcoFino = null;
    if (!g && vivoFrescoTodo && vivoTodo.graficos) {
      const mreq = MINUTOS[marco] || 1, mins = k => MINUTOS[k.slice(inst.length + 1)] || 0;
      // primero los mas finos o iguales (se agrupan al pedido); si no hay, el mas fino de los mas gruesos
      const cands = Object.keys(vivoTodo.graficos).filter(k => k.startsWith(inst + "-") && mins(k) > 0)
        .sort((a, b) => ((mins(a) <= mreq) === (mins(b) <= mreq)) ? mins(a) - mins(b) : (mins(a) <= mreq ? -1 : 1));
      if (cands.length) { marcoFino = cands[0].slice(inst.length + 1); g1 = vivoTodo.graficos[cands[0]]; }
    }
    const viva = vivoFrescoTodo && vivoTodo.viva ? (vivoTodo.viva[raiz] || null) : null;
    if (viva) viva.generado = vivoTodo.generado;
    const minutos = MINUTOS[marco] || 1;
    let velas, origenVelas, vivoFresco = false;
    if (g) { velas = velasDeVivo(g, cn, co); origenVelas = "VIVO desde tu ATAS (" + inst + " " + marco + ", hace " + Math.round(edadPc) + " min)"; vivoFresco = true; }
    else if (g1) { velas = agregar(velasDeVivo(g1, cn, co), minutos); origenVelas = "VIVO desde tu ATAS (" + inst + " " + marcoFino + ((MINUTOS[marcoFino] || 1) <= minutos ? " agrupado a " + marco : ", el mas fino abierto; pedido " + marco) + ")"; vivoFresco = true; }
    else { velas = agregar(velasDeYahoo(yahoo, serie), minutos); origenVelas = yahoo && yahoo.futuro && yahoo.futuro.t && yahoo.futuro.t.length ? "NUBE: Yahoo " + yahoo.futuro.simbolo + " (con retraso, sin order flow), niveles de la serie de la nube" : "sin velas"; }
    // historia larga: lo de Yahoo ANTES de la primera vela de ATAS (marcado v.yahoo = true, sin delta), para las
    // temporalidades largas y para ver la rueda entera; el vivo de ATAS manda desde donde empieza
    if (vivoFresco && velas.length && yahoo && yahoo.futuro && yahoo.futuro.t && yahoo.futuro.t.length) {
      const t0 = velas[0].t; const prev = agregar(velasDeYahoo(yahoo, serie), minutos).filter(v => v.t < t0 - (minutos * 60) / 2);
      if (prev.length) { prev.forEach(v => { v.yahoo = true; }); velas = prev.concat(velas); origenVelas += " · antes: Yahoo"; }
    }
    const cadena = feed ? Nucleo.parsear(feed) : null;
    const ahora = new Date();
    let futuro = null, futOrigen = "";
    if (vivoFresco && velas.length) { const v = velas[velas.length - 1]; futuro = v.c; futOrigen = "cierre de la última vela de ATAS (" + Math.round((Date.now() / 1000 - v.t) / 60) + " min)"; }
    else if (yahoo && yahoo.futuro && yahoo.futuro.t && yahoo.futuro.t.length) { const t = yahoo.futuro.t[yahoo.futuro.t.length - 1]; const ed = (Date.now() / 1000 - t) / 60; if (ed <= 30) { futuro = yahoo.futuro.c[yahoo.futuro.c.length - 1]; futOrigen = "Yahoo hace " + ed.toFixed(0) + " min"; } }
    if (futuro == null && estado) { futuro = estado.futuro; futOrigen = "nube: " + estado.fut_origen; }
    let baseRueda = null, edadRueda = null;
    if (vivoFresco && velas.length) { const v = velas[velas.length - 1]; if (v.spot) { baseRueda = v.c - v.spot; edadRueda = (Date.now() / 1000 - v.t) / 60; } }
    else if (estado && estado.base_por_precio != null) { baseRueda = estado.base_por_precio; edadRueda = 5; }
    let L = null, ex = null, A = null;
    const usarViva = ajustes.libro === "rithmic" && viva && viva.filas && viva.filas.length;
    const cad = usarViva ? Nucleo.parsearViva(viva) : cadena;
    if (cad && futuro) {
      A = Object.assign(Nucleo.ajustesDefault(raiz), ajustes.nucleo || {});
      const [e1, e2] = Nucleo.trimestralDesde(ahora); A.expFuturo = e1; A.expFuturoAlt = e2;
      L = Nucleo.calcular(A, cad, usarViva ? cad.spotIdx : futuro, ahora, null, baseRueda, edadRueda);
      if (L && !L.sinBase) {
        ex = Nucleo.extras(A, cad, L.S, L.base, ahora);
        if (estado && estado.perfil) { const m = new Map(estado.perfil.map(s => [s.K, s.antes])); for (const s of L.perfil) { const a = m.get(s.K); if (a) s.antes = a; } }
        L.pesadas = L.perfil.filter(s => Math.abs(s.fut - L.futuro) <= L.futuro * 0.006).sort((a, b) => Math.abs(b.gexVol) - Math.abs(a.gexVol)).slice(0, ajustes.pesadas || 2);
        if (estado && estado.mc) L.mc = estado.mc;
      }
    }
    // NIVELES DE TU ATAS: con el vivo fresco, lo que se dibuja es lo que calculo el indicador en la
    // ultima vela (zero, majors, dominantes, pico), no el recalculo de aca con CBOE. Antes la web
    // recalculaba con el libro de CBOE mientras ATAS mostraba el de Rithmic: dos mapas distintos.
    let nivAtas = null;
    if (vivoFresco) {
      const fuente = g || g1;
      for (let i = velas.length - 1; i >= 0 && i >= velas.length - 3; i--) {
        const n = velas[i].niv;
        if (n && (n.zero_vol != null || n.dom0 != null)) {
          nivAtas = { zeroVol: n.zero_vol, zeroOi: n.zero_oi, mpVol: n.mp_vol, mnVol: n.mn_vol,
                      doms: [n.dom0, n.dom1].filter(x => x != null).map(x => ({ fut: x, gex: null })),
                      picoFut: n.pico, libro: fuente && fuente.libro ? fuente.libro : null, marco: fuente ? fuente.marco : null, t: velas[i].t };
          break;
        }
      }
    }
    const marcas = [];
    for (const src of [g, g1]) if (src && src.gatillos) for (const x of src.gatillos) marcas.push({ t: Math.floor(new Date(x.t + "Z").getTime() / 1000), tipo: x.tipo, lado: x.lado, precio: x.precio, dom: x.dom, dz: x.dz, fuente: x.fuente, marco: src.marco });
    const vistos = new Set(); const marcasU = marcas.filter(m => { const k = m.t + "|" + m.tipo + "|" + m.lado; if (vistos.has(k)) return false; vistos.add(k); return true; });
    return { inst, raiz, marco, minutos, velas, origenVelas, vivoFresco, edadPc, pc, nivAtas, cadena: cad, feed, estado, yahoo, serie: serie || [], viva, usarViva, futuro, futOrigen, L, ex, A, marcas: marcasU, baseRueda, edadRueda, vwap: vwap(velas, ajustes.vwap || "rueda"), vivoTodo };
  }

  global.Datos = { cargar, traer, agregar, velasDeVivo, velasDeYahoo, vwap, edadMin, BASE_DEF, RAIZ, MINUTOS, shaRama, shaCache: () => shaCache };
})(typeof window !== "undefined" ? window : globalThis);
