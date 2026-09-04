// VOLCADO DE INTERES ABIERTO OFICIAL DE CME, para auditar_fuentes.py
//
// Se pega en la consola del navegador LOGUEADO, con una pestana abierta en
// cmegroup.com. No se puede automatizar desde un script: Akamai responde 403
// a cualquier peticion que no venga del navegador (probado).
//
// OJO CON EL ENDPOINT. Hay dos y dan numeros distintos:
//   Settlements     -> interes abierto del dia ANTERIOR
//   Volume/Details  -> el actual, en atClose  <-- ES EL QUE VA
// Comparar contra Settlements ya costo una conclusion falsa: parecia que el
// major positive se corria 50 puntos segun la fuente, y era el dato viejo.
//
// El resultado se guarda en datos/cme/ES-<YYYYMMDD>.json

(async () => {
  const TRADE = '20260902';          // ultimo dia con settlement publicado
  const REF   = 7752, RADIO = 120;   // ventana de strikes alrededor del dinero

  // dia de semana de cada grupo, para resolver "Week N-Mon YYYY" a fecha real
  const DIA = {'Weekly Monday Option':1,'Weekly Tuesday Option':2,
               'Weekly Wednesday Option':3,'Weekly Thursday Option':4,
               'Weekly Friday Option':5};
  const MES = {Jan:0,Feb:1,Mar:2,Apr:3,May:4,Jun:5,Jul:6,Aug:7,Sep:8,Oct:9,Nov:10,Dec:11};
  const fechaDe = (grupo, label) => {
    const d = DIA[grupo]; if (!d) return null;
    const m = /Week\s*(\d)-(\w{3})\s*(\d{4})/.exec(label||''); if (!m) return null;
    const mes = MES[m[2]]; if (mes === undefined) return null;
    let c = 0;
    for (let dia = 1; dia <= 31; dia++) {
      const f = new Date(+m[3], mes, dia);
      if (f.getMonth() !== mes) break;
      if (f.getDay() === d && ++c === +m[1]) return f;
    }
    return null;
  };

  const idx = await (await fetch('/CmeWS/mvc/Settlements/Options/TradeDateAndExpirations/133',
                                 {credentials:'include'})).json();
  const hoy = new Date(); hoy.setHours(0,0,0,0);
  const cerca = [];
  for (const g of idx) for (const e of (g.expirations||[])) {
    const f = fechaDe(g.label, e.label); if (!f) continue;
    const dte = Math.round((f - hoy)/86400000);
    if (dte >= 0 && dte <= 8) cerca.push({f: f.toISOString().slice(0,10), pid: e.productId});
  }
  cerca.sort((a,b)=> a.f < b.f ? -1 : 1);

  const num = v => parseFloat(String(v).replace(/,/g,''))||0;
  const out = {tradeDate:TRADE, bajado:new Date().toISOString(), ref:REF, radio:RADIO, v:{}};
  for (const o of cerca) {
    const r = await fetch(`/CmeWS/mvc/Volume/Details/O/${o.pid}/${TRADE}/P?_=`+Date.now(),
                          {credentials:'include'});
    if (!r.ok) { out.v[o.f] = {error:r.status}; continue; }
    const j = await r.json();
    const nm = ['','JAN','FEB','MAR','APR','MAY','JUN','JUL','AUG','SEP','OCT','NOV','DEC'][+o.f.slice(5,7)];
    const fl = {};
    for (const m of (j.monthData||[])) {
      // OJO: monthData no son meses sino MES + TIPO ("SEP 26 Calls", "SEP 26 Puts").
      // El tipo sale del label, no de strikeData. Juntarlos por strike hace que
      // octubre pise a septiembre y que todo quede clasificado como put.
      const et = String(m.month||''); if (!new RegExp(nm,'i').test(et)) continue;
      const t = /Calls/i.test(et) ? 'C' : 'P';
      for (const sd of (m.strikeData||[])) {
        const k = num(sd.strike);
        if (!k || Math.abs(k-REF) > RADIO) continue;
        fl[k] = fl[k]||{}; fl[k][t] = [num(sd.atClose), num(sd.change)];
      }
    }
    out.v[o.f] = {pid:o.pid, n:Object.keys(fl).length, f:fl};
    await new Promise(s=>setTimeout(s,800));   // no apurar a Akamai
  }
  console.log(JSON.stringify(out));
  return JSON.stringify(out);
})();
