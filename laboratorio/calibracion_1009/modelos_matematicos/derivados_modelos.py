# -*- coding: utf-8 -*-
"""
Cuentas derivadas de los modelos (Avellaneda-Lipkin 2003, Frey 1998/2000, ley de raiz cuadrada, Osler 2000)
con los ordenes de magnitud medidos en resultados_ordenes.json. Escribe derivados_modelos.json.
Uso: python -I derivados_modelos.py
Todo lo que no sale de resultados_ordenes.json esta marcado 'supuesto' en la salida.
"""
import json, math, os
from scipy.stats import norm

AQUI = os.path.dirname(os.path.abspath(__file__))
med = json.load(open(os.path.join(AQUI, 'resultados_ordenes.json'), encoding='utf-8'))
lam_mnq = med['derivados']['lambda_MNQ_medido_pts_por_ctoMNQ']          # medido (cinta MNQ, rueda 10-07 y 10-08)
out = {'medido': {'lambda_MNQ_pts_por_ctoMNQ_1min': lam_mnq}}

# 1) liquidez efectiva D = 1/lambda_tot  (NQ ctos por punto). s = parte de MNQ en el flujo NQ-equivalente (supuesto)
D = {}
for s in (0.15, 0.25, 0.40):
    lam_tot = 10 * s * lam_mnq
    D[s] = {'lambda_tot_1min': lam_tot, 'D_1min_ctosNQ_por_pt': 1 / lam_tot,
            'D_permanente_(x2,_supuesto_mitad_transitoria)': 2 / lam_tot}
out['liquidez_efectiva_D'] = {'supuesto': 's = participacion de MNQ en el flujo NQ-eq; impacto permanente = mitad del de 1 min', 'por_s': D}

# 2) Frey: sigma_eff/sigma = 1/(1 + L/D) (dealers largos, L>0) ; 1/(1 - |L|/D) (cortos)
frey = []
for L in (2.2, 5, 10, 20, 40):
    for s in (0.25,):
        Dp = D[s]['D_permanente_(x2,_supuesto_mitad_transitoria)']
        frey.append({'L_ctosNQ_por_pt': L, 'D': round(Dp, 1), 'largos_sigma_eff/sigma': round(1 / (1 + L / Dp), 3),
                     'cortos_sigma_eff/sigma': (round(1 / (1 - L / Dp), 3) if L < Dp else 'inestable (L>=D)')})
out['frey_multiplicador'] = {'nota': 'L=2,2 es la escala Cboe (0,1 % del volumen diario por 1 %), L=40 es la convencion clasica medida el 10-08', 'tabla': frey}

# 3) Gamma de UN strike de 0DTE de QQQ en NQ ctos/pt, al dinero, segun minutos al cierre
R, QQQ, F = 41.4352, 748.0, 31000.0
sh_por_NQ = 20 * R            # acciones QQQ por contrato NQ
gam = []
for minutos in (240, 120, 60, 30, 15, 10, 5):
    for iv in (0.20,):
        T = minutos / 525600.0
        sT = iv * math.sqrt(T)
        g_sh = norm.pdf(0) / (QQQ * sT)                  # acciones por $ por accion
        por_10k = g_sh * 100 * 10000 / R / sh_por_NQ    # NQ ctos por punto NQ, 10.000 contratos netos
        gam.append({'minutos_al_cierre': minutos, 'iv': iv, 'sigma_raiz_tau_pts_NQ': round(sT * F, 1),
                    'gamma_ATM_10k_QQQ_netos_ctosNQ_por_pt': round(por_10k, 1)})
out['gamma_0DTE_un_strike'] = gam

# 4) Avellaneda-Lipkin: beta = n*E / (sigma*sqrt(2*pi*T)); P_pin = 1 - exp(-2 beta exp(-z0^2/2))
al = []
casos = [  # medidos en resultados_ordenes.json (10-08, QQQ 0DTE)
    {'hora': '14:30', 'K': 750, 'K_NQ': 31072.5, 'F': 30991.75, 'oi': 894 + 13710, 'iv': 0.2313, 'minutos': 0.06232638888888889 * 1440},
    {'hora': '15:15', 'K': 749, 'K_NQ': 31034.3, 'F': 30907.5, 'oi': 430 + 10867, 'iv': 0.2325, 'minutos': 0.031064814814814816 * 1440},
]
for c in casos:
    n_eq = c['oi'] / 2 * 100 / sh_por_NQ                 # straddles-equivalentes en NQ ctos (si los dealers estan LARGOS todo)
    T = c['minutos'] / 525600.0
    sT = c['iv'] * math.sqrt(T)
    z0 = math.log(c['F'] / c['K_NQ']) / sT
    for s in (0.15, 0.40):
        lam_perm = 10 * s * lam_mnq / 2
        E = lam_perm / c['F']
        beta = n_eq * E / (c['iv'] * math.sqrt(2 * math.pi * T))
        p = 1 - math.exp(-2 * beta * math.exp(-z0 * z0 / 2))
        al.append({**c, 's': s, 'n_eq_NQ': round(n_eq), 'E_por_ctoNQ': E, 'sigma_raiz_T_pts': round(sT * c['F'], 1), 'z0': round(z0, 2),
                   'beta': round(beta, 3), 'P_pin_al_cierre_si_dealers_largos': round(p, 3),
                   'distancia_fuerza_maxima_pts': round(sT * c['F'], 1)})
out['avellaneda_lipkin'] = {'formula': 'beta = n E / (sigma sqrt(2 pi T)); P = 1 - exp(-2 beta e^(-z0^2/2)); fuerza maxima a |S-K| = sigma sqrt(tau)',
                            'supuesto': 'dealers LARGOS todo el OI del strike (si estan cortos el modelo predice anti-pin)', 'casos': al}

# 5) Tamano de muestra para detectar una ventaja de rebote (dos proporciones, alfa 5 % dos colas, potencia 80 %)
def n_por_brazo(p0, p1, za=1.959964, zb=0.841621):
    pb = (p0 + p1) / 2
    return math.ceil((za * math.sqrt(2 * pb * (1 - pb)) + zb * math.sqrt(p0 * (1 - p0) + p1 * (1 - p1))) ** 2 / (p1 - p0) ** 2)
out['muestra_necesaria'] = [{'placebo': p0, 'real': p1, 'toques_independientes_por_brazo': n_por_brazo(p0, p1)}
                            for p0, p1 in ((0.562, 0.608), (0.60, 0.62), (0.60, 0.65), (0.60, 0.70), (0.65, 0.62))]
out['muestra_necesaria_nota'] = '0,562 -> 0,608 es el efecto de Osler (2000) en FX; los toques del mismo nivel no son independientes: multiplicar por el efecto de diseno'

# 6) Hundimiento de la mecha ('impulso agotado'): d = Q / (D + L)
imp = []
for Q in (100, 300, 1000):
    for L in (0, 2.2, 10, 40):
        Dp = D[0.25]['D_permanente_(x2,_supuesto_mitad_transitoria)']
        imp.append({'Q_ctosNQ_agresivos': Q, 'L': L, 'D': round(Dp, 1), 'recorrido_pts': round(Q / (Dp + L), 1)})
out['impulso_agotado'] = imp
json.dump(out, open(os.path.join(AQUI, 'derivados_modelos.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print(json.dumps(out, ensure_ascii=False, indent=1))
