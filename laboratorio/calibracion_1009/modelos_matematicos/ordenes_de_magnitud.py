# -*- coding: utf-8 -*-
"""
Ordenes de magnitud del flujo de cobertura de dealers en NQ (calibracion 10-09, angulo: modelos matematicos).

SOLO LEE:
  %APPDATA%/ATAS/PythiaGex4/cboe/cadena-{QQQ,NQ}-<dia>.jsonl.gz   (cadena CBOE, 15 min de retraso; 'NQ' = NDX)
  %APPDATA%/ATAS/PythiaGex4/cinta/cinta-NQ-<dia>.csv               (cinta de MNQZ6, t ms UTC, precio, dv, lado)
ESCRIBE: resultados_ordenes.json y resultados_ordenes.txt en esta carpeta.

Que calcula (definiciones exactas en formulas_candidatas.json):
  1. Gamma de dealers en CONTRATOS DE NQ POR PUNTO DE NQ, repreciada en una grilla de precios (no solo en el spot):
       L(p) = sum_k n_k * Gamma_BS(S_k(p); K, iv, tau) * 100 / c
       QQQ: S(p) = p / R, c = 20 * R^2      (R = NQ/QQQ)
       NDX: S(p) = p - B, c = 20            (B = NQ - NDX)
     con tres convenciones de n_k: clasica (+OI call, -OI put), referencia (vol call - vol put, volumen de hoy) y bruta (OI c + OI p).
  2. Campo de fuerza de charm (Avellaneda-Lipkin generalizado), solo vencimientos <= 1 dia:
       A(p) = sum_k n_k * dDelta_call/dtau (S_k(p)) * 100 / c'   [contratos de NQ por hora que los dealers compran(+)/venden(-)]
     atractores: A cruza de + a - al subir p.
  3. Lambda de Kyle de la cinta de MNQ (rueda): dP_1min = lambda * flujo_agresor_1min (MNQ).
  4. Multiplicador de Frey: sigma_eff/sigma = 1 / (1 + lambda_tot * L(spot)).
  5. Beta de Avellaneda-Lipkin para el strike de 0DTE mas cargado y probabilidad de pin al cierre.
Uso: python -I ordenes_de_magnitud.py
"""
import csv, gzip, json, math, os, sys, statistics as st
from datetime import datetime, timedelta, timezone

import numpy as np
from scipy.stats import norm

APP = os.path.join(os.environ.get('APPDATA', r'C:\Users\wmx_7\AppData\Roaming'), 'ATAS', 'PythiaGex4')
AQUI = os.path.dirname(os.path.abspath(__file__))
R_TASA, Q_DIV = 0.04, 0.006          # tasa y dividendo aproximados (el gamma casi no depende: memoria laboratorio-formulas)
NY_UTC = timedelta(hours=4)          # EDT en octubre
HORAS_NY = ['10:30', '12:00', '13:30', '14:30', '15:15', '15:45', '15:58']
DIAS = ['2026-10-07', '2026-10-08']


def cargar_cadena(sim, dia):
    p = os.path.join(APP, 'cboe', f'cadena-{sim}-{dia}.jsonl.gz')
    out = []
    with gzip.open(p, 'rt', encoding='utf-8') as f:
        for l in f:
            d = json.loads(l)
            c = d['cadena']
            ut = datetime.fromisoformat(c['ultimo_trade'])  # hora NY sin zona
            out.append((ut, d))
    return out


def elegir(snaps, dia, hhmm):
    obj = datetime.fromisoformat(f'{dia}T{hhmm}:00')
    mejor = min(snaps, key=lambda s: abs((s[0] - obj).total_seconds()))
    if abs((mejor[0] - obj).total_seconds()) > 600:
        return None
    return mejor


def cargar_cinta(dia):
    p = os.path.join(APP, 'cinta', f'cinta-NQ-{dia}.csv')
    t, px, dv, lado = [], [], [], []
    with open(p, encoding='utf-8') as f:
        r = csv.reader(f)
        next(r)
        for row in r:
            t.append(int(row[0])); px.append(float(row[1])); dv.append(int(row[2])); lado.append(int(row[3]))
    return np.array(t), np.array(px), np.array(dv), np.array(lado)


def precio_en(cinta, ts_utc):
    t, px = cinta[0], cinta[1]
    ms = int(ts_utc.timestamp() * 1000)
    i = np.searchsorted(t, ms)
    if i == 0 or i >= len(t):
        return None
    if ms - t[i - 1] > 120000:
        return None
    return float(px[i - 1])


def gamma_bs(S, K, iv, tau):
    S = np.asarray(S, float)
    sq = iv * math.sqrt(tau)
    d1 = (np.log(S / K) + (R_TASA - Q_DIV + 0.5 * iv * iv) * tau) / sq
    return math.exp(-Q_DIV * tau) * norm.pdf(d1) / (S * sq)


def charm_tau(S, K, iv, tau):
    """dDelta_call/dtau (sin el termino q, despreciable)."""
    S = np.asarray(S, float)
    sq = iv * math.sqrt(tau)
    d1 = (np.log(S / K) + (R_TASA - Q_DIV + 0.5 * iv * iv) * tau) / sq
    dd1 = ((R_TASA - Q_DIV + 0.5 * iv * iv) * tau - np.log(S / K)) / (2 * iv * tau ** 1.5)
    return norm.pdf(d1) * dd1


def dias_reales(fecha_venc, d):
    """Tiempo al vencimiento (16:00 NY) medido desde la HORA DEL DATO (ultimo_trade), no desde la bajada (+15 min)."""
    ut = datetime.fromisoformat(d['cadena']['ultimo_trade'])
    ven = datetime.fromisoformat(fecha_venc + 'T16:00:00')
    return (ven - ut).total_seconds() / 86400.0


def vanna(S, K, iv, tau):
    S = np.asarray(S, float)
    sq = iv * math.sqrt(tau)
    d1 = (np.log(S / K) + (R_TASA - Q_DIV + 0.5 * iv * iv) * tau) / sq
    d2 = d1 - sq
    return -math.exp(-Q_DIV * tau) * norm.pdf(d1) * d2 / iv


def perfil(d, conv, F, grilla, sim, solo_0dte=False):
    """Devuelve L(p) [NQ ctos/pt] y A(p) [NQ ctos/hora] en la grilla de precios de NQ."""
    c = d['cadena']
    venc = c['vencimientos']
    spot = float(d['spot'])
    if sim == 'QQQ':
        R = F / spot
        S_de_p = grilla / R
        conv_g = 100.0 / (20.0 * R * R)        # (acciones por $ QQQ) -> NQ ctos por punto NQ
        conv_c = 100.0 / (20.0 * R)            # acciones QQQ -> NQ ctos
    else:
        B = F - spot
        S_de_p = grilla - B
        conv_g = 100.0 / 20.0
        conv_c = 100.0 / 20.0
    L = np.zeros_like(grilla)
    A = np.zeros_like(grilla)
    porK = {}
    V = 0.0
    for fila in c['filas']:
        K, iv_idx, oic, oip, ivc, ivp, vc, vp = fila
        dias = dias_reales(venc[int(iv_idx)]['f'], d)
        if dias <= 0.0005:
            continue
        if solo_0dte and dias > 1.0:
            continue
        tau = dias / 365.0
        iv = ivc if (ivc and ivc > 0) else ivp
        if not iv or iv <= 0:
            continue
        if conv == 'clasica':
            n = oic - oip
        elif conv == 'referencia':
            n = vc - vp
        elif conv == 'bruta':
            n = oic + oip
        else:
            raise ValueError(conv)
        if n == 0:
            continue
        g = gamma_bs(S_de_p, K, iv, tau)
        L += n * g * conv_g
        if dias <= 1.0:
            A += n * charm_tau(S_de_p, K, iv, tau) * conv_c / (365.0 * 24.0)   # por hora
        V += float(vanna(spot, K, iv, tau)) * n * 0.01 * conv_c   # NQ ctos por +1 punto de vol
        g0 = float(gamma_bs(spot, K, iv, tau)) * n * conv_g
        porK[K] = porK.get(K, 0.0) + g0
    perfil.ultimo_vanna = V
    return L, A, porK, spot


def cruces(grilla, y, signo):
    """signo=-1: cruces de + a - (atractores para A; para L es el zero 'de positivo a negativo' al subir)."""
    out = []
    for i in range(len(y) - 1):
        if signo < 0 and y[i] > 0 >= y[i + 1]:
            out.append(float(grilla[i] + (grilla[i + 1] - grilla[i]) * y[i] / (y[i] - y[i + 1])))
        if signo > 0 and y[i] < 0 <= y[i + 1]:
            out.append(float(grilla[i] + (grilla[i + 1] - grilla[i]) * (-y[i]) / (y[i + 1] - y[i])))
    return out


def maximos_locales(grilla, y, k=3, minimo=0.0):
    out = []
    for i in range(1, len(y) - 1):
        if y[i] > minimo and y[i] == max(y[max(0, i - 15):i + 16]):
            out.append((float(grilla[i]), float(y[i])))
    out.sort(key=lambda t: -t[1])
    return out[:k]


def lambda_kyle(cinta, dia):
    t, px, dv, lado = cinta
    ini = int(datetime.fromisoformat(f'{dia}T13:30:00+00:00').timestamp() * 1000)
    fin = int(datetime.fromisoformat(f'{dia}T20:00:00+00:00').timestamp() * 1000)
    m = (t >= ini) & (t < fin)
    if m.sum() < 1000:
        return None
    tt, pp, vv, ll = t[m], px[m], dv[m], lado[m]
    minuto = (tt - ini) // 60000
    nmin = int(minuto.max()) + 1
    flujo = np.bincount(minuto, weights=vv * ll, minlength=nmin)
    vol = np.bincount(minuto, weights=vv, minlength=nmin)
    ult = np.full(nmin, np.nan)
    pri = np.full(nmin, np.nan)
    for i in range(len(tt)):
        k = minuto[i]
        if np.isnan(pri[k]):
            pri[k] = pp[i]
        ult[k] = pp[i]
    ok = ~np.isnan(ult) & ~np.isnan(pri) & (vol > 0)
    dP = (ult - pri)[ok]
    of = flujo[ok]
    lam = float(np.dot(dP, of) / np.dot(of, of))
    r2 = float(np.corrcoef(dP, of)[0, 1] ** 2)
    # rango de 1 y 5 minutos
    hi = np.full(nmin, -np.inf); lo = np.full(nmin, np.inf)
    np.maximum.at(hi, minuto, pp); np.minimum.at(lo, minuto, pp)
    rango1 = (hi - lo)[ok]
    hi5 = [hi[i:i + 5].max() - lo[i:i + 5].min() for i in range(0, nmin - 5, 5)]
    return {
        'minutos': int(ok.sum()),
        'lambda_pts_por_ctoMNQ_1min': lam,
        'r2_1min': r2,
        'vol_MNQ_por_min_mediana': float(np.median(vol[ok])),
        'vol_MNQ_dia_rueda': float(vol.sum()),
        '|flujo_neto_1min|_mediana_MNQ': float(np.median(np.abs(of))),
        'rango_1min_mediana_pts': float(np.median(rango1)),
        'rango_5min_mediana_pts': float(np.median(hi5)),
        'precio_max_rueda': float(pp.max()), 'precio_min_rueda': float(pp.min()),
    }


def main():
    res = {'generado': datetime.now(timezone.utc).isoformat(), 'fuentes': {}, 'dias': {}}
    for dia in DIAS:
        cin = cargar_cinta(dia)
        kyle = lambda_kyle(cin, dia)
        res['dias'][dia] = {'kyle_MNQ': kyle, 'fotos': []}
        qqq = cargar_cadena('QQQ', dia)
        ndx = cargar_cadena('NQ', dia)
        for hh in HORAS_NY:
            sq = elegir(qqq, dia, hh)
            sn = elegir(ndx, dia, hh)
            if not sq or not sn:
                continue
            ts_utc = (sq[0] + NY_UTC).replace(tzinfo=timezone.utc)
            F = precio_en(cin, ts_utc)
            if F is None:
                continue
            grilla = np.arange(round(F) - 300, round(F) + 301, 1.0)
            foto = {'hora_NY_dato': sq[0].isoformat(), 'edad_cboe': 'retraso 15 min (dato de la hora indicada, bajado 15 min despues)',
                    'F_MNQ': F, 'QQQ': float(sq[1]['spot']), 'NDX': float(sn[1]['spot']),
                    'R': F / float(sq[1]['spot']), 'B': F - float(sn[1]['spot'])}
            for conv in ('clasica', 'referencia', 'bruta'):
                Lq, Aq, pkq, _ = perfil(sq[1], conv, F, grilla, 'QQQ')
                Vq = perfil.ultimo_vanna
                Ln, An, pkn, _ = perfil(sn[1], conv, F, grilla, 'NDX')
                Vn = perfil.ultimo_vanna
                Lq0, Aq0, _, _ = perfil(sq[1], conv, F, grilla, 'QQQ', solo_0dte=True)
                Ln0, An0, _, _ = perfil(sn[1], conv, F, grilla, 'NDX', solo_0dte=True)
                L = Lq + Ln
                A = Aq + An
                i0 = int(np.argmin(np.abs(grilla - F)))
                bloque = {
                    'L_spot_QQQ_ctosNQ_por_pt': float(Lq[i0]), 'L_spot_NDX_ctosNQ_por_pt': float(Ln[i0]),
                    'L_spot_familia': float(L[i0]), 'L_spot_solo0DTE': float(Lq0[i0] + Ln0[i0]),
                    'GEX_familia_MUSD_por_1pct': float(L[i0] * 20 * F * F * 0.01 / 1e6),
                    'L_max_grilla': float(L.max()), 'L_min_grilla': float(L.min()),
                    'zero_repreciado_(cruces +->- y -->+)': [round(x, 1) for x in cruces(grilla, L, -1) + cruces(grilla, L, +1)],
                    'paredes_L_max_locales_(precio, ctos/pt)': [(round(a, 1), round(b, 1)) for a, b in maximos_locales(grilla, L)],
                    'atractores_charm_0DTE': [round(x, 1) for x in cruces(grilla, A, -1)],
                    'repulsores_charm_0DTE': [round(x, 1) for x in cruces(grilla, A, +1)],
                    'A_max_abs_ctosNQ_por_hora': float(np.abs(A).max()),
                    'A_spot_ctosNQ_por_hora': float(A[i0]),
                    'vanna_spot_ctosNQ_por_punto_de_vol': float(Vq + Vn),
                    'hay_0DTE_en_la_foto': any(dias_reales(v['f'], sq[1]) < 1.0 for v in sq[1]['cadena']['vencimientos']),
                }
                # strike de max |GEX neta| y max bruta al spot (en precio NQ)
                Rr, Bb = foto['R'], foto['B']
                if pkq:
                    kq = max(pkq, key=lambda k: abs(pkq[k]))
                    bloque['QQQ_strike_max_abs'] = (kq, round(kq * Rr, 1), round(pkq[kq], 2))
                if pkn:
                    kn = max(pkn, key=lambda k: abs(pkn[k]))
                    bloque['NDX_strike_max_abs'] = (kn, round(kn + Bb, 1), round(pkn[kn], 2))
                if conv == 'bruta':
                    pes = [(k * Rr, v) for k, v in pkq.items()] + [(k + Bb, v) for k, v in pkn.items()]
                    cerca = [(x, v) for x, v in pes if abs(x - F) <= 300]
                    if cerca:
                        bloque['centroide_bruto_+-300'] = round(sum(x * v for x, v in cerca) / sum(v for _, v in cerca), 1)
                foto[conv] = bloque
            # Avellaneda-Lipkin: strike de 0DTE de QQQ con mas OI total cerca del precio
            c = sq[1]['cadena']
            mejores = []
            for fila in c['filas']:
                K, vi, oic, oip, ivc, ivp, vc, vp = fila
                dr = dias_reales(c['vencimientos'][int(vi)]['f'], sq[1])
                if dr <= 1.0 and abs(K * foto['R'] - F) < 150:
                    mejores.append((oic + oip, K, ivc or ivp, dr, oic, oip, vc, vp))
            if mejores:
                mejores.sort(reverse=True)
                oi, K, iv, dias, oic, oip, vc, vp = mejores[0]
                foto['AL_strike_QQQ_0DTE'] = {'K': K, 'K_en_NQ': round(K * foto['R'], 1), 'oi_call': oic, 'oi_put': oip,
                                             'vol_call': vc, 'vol_put': vp, 'iv': iv, 'dias': dias}
            res['dias'][dia]['fotos'].append(foto)
    # parametros derivados (Frey y Avellaneda-Lipkin), con supuestos explicitos
    lam = [res['dias'][d]['kyle_MNQ']['lambda_pts_por_ctoMNQ_1min'] for d in DIAS if res['dias'][d]['kyle_MNQ']]
    lam_mnq = float(np.mean(lam))
    deriv = {'lambda_MNQ_medido_pts_por_ctoMNQ': lam_mnq}
    # lambda por contrato NQ-equivalente del flujo TOTAL: lambda_tot = 10 * s * lambda_MNQ, s = participacion de MNQ en el flujo NQ-eq
    for s in (0.15, 0.25, 0.40):
        deriv[f'lambda_tot_pts_por_ctoNQ_(s={s})'] = 10 * s * lam_mnq
    res['derivados'] = deriv
    with open(os.path.join(AQUI, 'resultados_ordenes.json'), 'w', encoding='utf-8') as f:
        json.dump(res, f, ensure_ascii=False, indent=1, default=float)
    print(json.dumps(res, ensure_ascii=False, indent=1, default=float)[:20000])


if __name__ == '__main__':
    main()
