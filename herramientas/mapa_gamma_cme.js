// ============================================================
//  MAPA DE GAMMA DE ES  —  desde el settlement oficial de CME
// ------------------------------------------------------------
//  COMO SE USA
//  1. Abrir cmegroup.com y estar logueado.
//  2. Pegar todo esto en la consola del navegador (F12) y enter.
//  3. Esperar ~20 segundos. Imprime los niveles.
//
//  OJO: CME prohibe el scraping automatizado y bloquea por IP.
//  Esto es una lectura MANUAL, una vez por dia, dentro de tu
//  propia sesion de usuario registrado. No automatizar.
// ============================================================

(async () => {

// ---- 0. GUARDA ANTIBLOQUEO -------------------------------
// CME corre Akamai Bot Manager. El 2026-08-26 dos descargas
// completas en un mismo dia deslogearon al usuario de todo.
// Si ya se bajo hoy, se reusa el cache y NO se pide nada.
const HOY_KEY = "gexES_" + new Date().toISOString().slice(0,10).replace(/-/g,"");
if (localStorage.getItem(HOY_KEY)) {
  const c = JSON.parse(localStorage.getItem(HOY_KEY));
  console.log("%cYA SE BAJO HOY - usando cache", "font-weight:bold;color:#0a0");
  console.log("settlement de", c.tradeDate, "| filas", c.filas.length,
              "| futuro", c.F0);
  console.log("Para recalcular con otro precio, usar el cache. NO volver a pedir.");
  return "cache";
}

// ---- 1. QUE DIA PEDIR -------------------------------------
// El settlement sale a las 23:55 CT del mismo dia operado.
// Antes de la apertura, el ultimo dato disponible es el del
// dia habil anterior.
const HOY = new Date();
function ultimoDiaHabil(d) {
  const x = new Date(d);
  x.setDate(x.getDate() - 1);
  while (x.getDay() === 0 || x.getDay() === 6) x.setDate(x.getDate() - 1);
  return x;
}
const D = ultimoDiaHabil(HOY);
const pad = n => String(n).padStart(2, "0");
const TRADE_DATE = pad(D.getMonth() + 1) + "/" + pad(D.getDate()) + "/" + D.getFullYear();

// ---- 2. CALCULAR LAS FECHAS DE VENCIMIENTO -----------------
// No estan escritas a mano: se deducen del nombre.
// "Week 4-Aug 2026" del grupo de los lunes = el 4to lunes de agosto.
// Por eso esta herramienta no se vence nunca.
const MES = {Jan:0,Feb:1,Mar:2,Apr:3,May:4,Jun:5,Jul:6,Aug:7,Sep:8,Oct:9,Nov:10,Dec:11};
const DIA_SEMANA = {MW1:1, AB1:2, WD1:3, BB1:4, E21:5};

function enesimoDiaDelMes(anio, mes, diaSemana, n) {
  const x = new Date(Date.UTC(anio, mes, 1));
  let c = 0;
  for (let i = 0; i < 40; i++) {
    if (x.getUTCDay() === diaSemana) { c++; if (c === n) return new Date(x); }
    x.setUTCDate(x.getUTCDate() + 1);
  }
  return null;
}
function ultimoHabilDelMes(anio, mes) {
  const x = new Date(Date.UTC(anio, mes + 1, 0));
  while (x.getUTCDay() === 0 || x.getUTCDay() === 6) x.setUTCDate(x.getUTCDate() - 1);
  return x;
}
function fechaVencimiento(tipo, label, anio) {
  const m = label.match(/Week (\d)-(\w{3})/);
  if (tipo === "EOM") return ultimoHabilDelMes(anio, MES[label.slice(0,3)]);
  if (!m) return enesimoDiaDelMes(anio, MES[label.slice(0,3)], 5, 3);
  return enesimoDiaDelMes(anio, MES[m[2]], DIA_SEMANA[tipo], Number(m[1]));
}

// ---- 3. PEDIR EL INDICE DE VENCIMIENTOS --------------------
const idx = await (await fetch(
  "/CmeWS/mvc/Settlements/Options/TradeDateAndExpirations/133?isProtected",
  {credentials:"include"})).json();

const LIMITE_DIAS = 30;
const lista = [];
for (const g of idx) for (const e of (g.expirations || [])) {
  const venc = fechaVencimiento(g.optionType, e.label, e.expiration.year);
  if (!venc) continue;
  const dias = (venc - HOY) / 86400000;
  if (dias > -1 && dias <= LIMITE_DIAS) {
    const c = e.expiration.twoDigitsCode;
    lista.push({ pid:e.productId, cid:e.contractId, venc, dias,
                 oe: e.productId + "-" + c[0] + c.slice(-1) });
  }
}
lista.sort((a,b) => a.venc - b.venc);
console.log("Vencimientos a bajar:", lista.length, "| trade date:", TRADE_DATE);

// ---- 4. MATEMATICA: Black-76 -------------------------------
// Black-76 es Black-Scholes pero para opciones sobre FUTUROS,
// que es exactamente el caso de ES.
const Npdf = x => Math.exp(-x*x/2) / Math.sqrt(2*Math.PI);
function Ncdf(x) {
  const b = [.31938153,-.356563782,1.781477937,-1.821255978,1.330274429];
  const L = Math.abs(x), k = 1/(1+.2316419*L);
  const w = 1 - Npdf(L)*(b[0]*k + b[1]*k*k + b[2]*Math.pow(k,3)
                       + b[3]*Math.pow(k,4) + b[4]*Math.pow(k,5));
  return x < 0 ? 1-w : w;
}
const R = 0.04;
function precio(F,K,T,v,esCall) {
  if (T <= 0 || v <= 0) return Math.max(esCall ? F-K : K-F, 0);
  const s = v*Math.sqrt(T), d1 = (Math.log(F/K)+0.5*s*s)/s, d2 = d1-s, Dc = Math.exp(-R*T);
  return esCall ? Dc*(F*Ncdf(d1)-K*Ncdf(d2)) : Dc*(K*Ncdf(-d2)-F*Ncdf(-d1));
}
// Despeja la volatilidad implicita partiendo del precio de liquidacion:
// prueba por el medio hasta que el precio calculado coincide con el real.
function volImplicita(F,K,T,p,esCall) {
  const intrinseco = Math.max(esCall ? F-K : K-F, 0);
  if (!isFinite(p) || T <= 0 || !(p > intrinseco + 0.02)) return NaN;
  let lo = 0.01, hi = 3.0;
  for (let i = 0; i < 80; i++) {
    const m = (lo+hi)/2;
    if (precio(F,K,T,m,esCall) > p) hi = m; else lo = m;
  }
  const r = (lo+hi)/2;
  return (r > 0.012 && r < 2.5) ? r : NaN;
}
function gamma(F,K,T,v) {
  if (T <= 0 || !(v > 0)) return 0;
  const s = v*Math.sqrt(T), d1 = (Math.log(F/K)+0.5*s*s)/s;
  return Math.exp(-R*T)*Npdf(d1)/(F*s);
}

// ---- 5. BAJAR CADA VENCIMIENTO -----------------------------
const limpiar = s => {
  if (s == null) return NaN;
  s = String(s).replace(/,/g,"").replace(/[BA]$/,"");
  if (s === "CAB") return 0.05;
  const v = parseFloat(s);
  return isFinite(v) ? v : NaN;
};

window.__raw = {};
for (const L of lista) {
  const u = "/CmeWS/mvc/Settlements/Options/Settlements/" + L.pid + "/OOF"
          + "?strategy=DEFAULT&optionProductId=" + L.pid + "&monthYear=" + L.cid
          + "&optionExpiration=" + L.oe + "&tradeDate=" + TRADE_DATE
          + "&pageSize=700&isProtected";
  try {
    const r = await fetch(u, {credentials:"include"});
    if (!r.ok) { console.warn(L.cid, "HTTP", r.status); continue; }
    const d = await r.json();
    if (!(d.settlements || []).length) { console.warn(L.cid, "vacio"); continue; }
    window.__raw[L.cid] = { venc:L.venc, filas:d.settlements, upd:d.updateTime };
  } catch (e) { console.warn(L.cid, "error"); }
  await new Promise(r => setTimeout(r, 2000));  // 2 s: Akamai bloquea si se acelera
}

// ---- 6. EL PRECIO DEL FUTURO SALE SOLO ---------------------
// Un call muy dentro del dinero vale casi exactamente F - K.
// Entonces  F = strike + precio de liquidacion. Chequeo gratis.
let F0 = NaN;
for (const cid in window.__raw) {
  for (const o of window.__raw[cid].filas) {
    const K = limpiar(o.strike), s = limpiar(o.settle);
    if (o.type === "Call" && K > 0 && K <= 500 && isFinite(s) && s > 1000) { F0 = K + s; break; }
  }
  if (isFinite(F0)) break;
}
if (!isFinite(F0)) { console.error("No pude despejar el futuro."); return; }
console.log("Futuro despejado de la cadena: F =", F0);

// ---- 7. LAS DOS T — el error clasico ------------------------
// El precio de liquidacion es de AYER. Entonces hacen falta dos:
//   T_precio    = del settlement de ayer al vencimiento -> para la IV
//   T_valuacion = de AHORA al vencimiento               -> para la gamma
// Con una sola T la IV sale inflada y la gamma es la de ayer.
const T_SETT = new Date(Date.UTC(D.getFullYear(), D.getMonth(), D.getDate(), 20, 0, 0));
const AHORA = new Date();
const filas = [];

for (const cid in window.__raw) {
  const B = window.__raw[cid];
  const vencUTC = new Date(Date.UTC(B.venc.getUTCFullYear(), B.venc.getUTCMonth(),
                                    B.venc.getUTCDate(), 20, 0, 0));
  const Tp = (vencUTC - T_SETT) / (365*864e5);
  let  Tv = (vencUTC - AHORA)  / (365*864e5);
  if (Tv <= 0) Tv = 0.05/365;
  const esDelDia = (vencUTC - AHORA) < 1.2*864e5;

  const m = {};
  for (const o of B.filas) {
    const K = limpiar(o.strike), oi = limpiar(o.openInterest), s = limpiar(o.settle);
    if (!isFinite(K) || K < F0*0.75 || K > F0*1.25) continue;
    if (!m[K]) m[K] = {K, cOI:0, pOI:0, cS:NaN, pS:NaN};
    if (o.type === "Call") { m[K].cOI = oi || 0; m[K].cS = s; }
    else                   { m[K].pOI = oi || 0; m[K].pS = s; }
  }
  for (const k in m) {
    const o = m[k], fueraDelDinero = o.K >= F0;
    const v = volImplicita(F0, o.K, Tp, fueraDelDinero ? o.cS : o.pS, fueraDelDinero);
    if (!isFinite(v)) continue;
    filas.push({ K:o.K, T:Tv, v, neto:(o.cOI||0)-(o.pOI||0), cid, esDelDia });
  }
}

// ---- 8. CALCULAR EL MAPA -----------------------------------
// GEX = gamma x interes abierto neto x 50 x precio^2 x 0.01
// El 50 es el multiplicador de ES. El resto lo convierte a
// "dolares que hay que cubrir por cada 1% que se mueva el precio".
const gex = S => filas.reduce((a,f) => a + gamma(S,f.K,f.T,f.v)*f.neto*50*S*S*0.01, 0);
const S0 = F0;

let flip = null, ant = gex(S0*0.95);
for (let S = S0*0.95; S <= S0*1.08; S += 2) {
  const g = gex(S);
  if ((ant < 0 && g >= 0) || (ant > 0 && g <= 0)) { flip = S; break; }
  ant = g;
}

const porStrike = {}, delDia = {};
for (const f of filas) {
  const g = gamma(S0,f.K,f.T,f.v)*f.neto*50*S0*S0*0.01;
  porStrike[f.K] = (porStrike[f.K] || 0) + g;
  if (f.esDelDia) delDia[f.K] = (delDia[f.K] || 0) + g;
}
const M = x => Math.round(x/1e6);
const aLista = o => Object.keys(o).map(k => ({K:Number(k), g:o[k]}))
                          .filter(x => Math.abs(x.g) > 5e5);
const todos = aLista(porStrike), hoy = aLista(delDia);
const fmt = a => a.map(x => x.K + " (" + M(x.g) + "M)").join("   ");

console.log("%c=== MAPA DE GAMMA — ES ===", "font-weight:bold;font-size:14px");
console.log("settlement de", TRADE_DATE, "| futuro", F0, "| strikes usados", filas.length);
console.log("GAMMA NETA", M(gex(S0)), "M  ->",
  gex(S0) < 0 ? "NEGATIVA: los movimientos se agrandan"
              : "POSITIVA: los movimientos se frenan");
console.log("PUNTO DE GIRO", flip ? flip.toFixed(1) : "no encontrado en el rango");
console.log("MUROS DE CALL (arriba):", fmt(todos.filter(x=>x.g>0).sort((a,b)=>b.g-a.g).slice(0,6)));
console.log("MUROS DE PUT (abajo): ", fmt(todos.filter(x=>x.g<0).sort((a,b)=>a.g-b.g).slice(0,6)));
console.log("VENCIMIENTO DE HOY:   ", fmt(hoy.sort((a,b)=>Math.abs(b.g)-Math.abs(a.g)).slice(0,6)));
console.log("CURVA:", [-150,-100,-50,0,50,100,150]
  .map(d => Math.round(S0+d) + ":" + M(gex(S0+d))).join("   "));

// ---- 9. PERSISTIR EL CACHE --------------------------------
// Sin esto, si se cierra la pestana hay que volver a pedirle
// todo a CME - que es exactamente lo que disparo el bloqueo.
try {
  localStorage.setItem(HOY_KEY, JSON.stringify({
    fecha: new Date().toISOString().slice(0,10),
    tradeDate: TRADE_DATE, F0,
    filas: filas.map(f => [f.K, +f.v.toFixed(5), f.neto,
      Date.now() + f.T*365*864e5, f.esDelDia ? 1 : 0])
  }));
  console.log("Cache guardado en", HOY_KEY, "- no hace falta volver a pedir hoy.");
} catch (e) { console.warn("No pude guardar el cache:", e); }

return "listo";
})();
