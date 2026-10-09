# -*- coding: utf-8 -*-
"""Cuentas de control para las estadisticas publicadas por los proveedores de niveles de gamma.

Pregunta: las tasas de "acierto" que publican (SpotGamma, MenthorQ, ZeroGEX, Algoindex) son mas altas
que lo que daria UNA LINEA CUALQUIERA a la misma distancia? Ninguno publica un placebo, asi que se calcula
lo que daria el azar con un movimiento browniano sin deriva (supuesto, no medido sobre datos).

Tambien: cuanta muestra hace falta para ver un efecto del tamano del unico estudio con placebo que existe
(Osler 2000, FRBNY: 60,8 % contra 56,2 % de niveles arbitrarios), y cuanto mueve la razon NQ/QQQ el strike 750.

Correr:  python -I calculos_azar_y_potencia.py   (escribe calculos_azar_y_potencia.json al lado)
"""
import json
import math
import os


def Phi(x):
    return 0.5 * (1.0 + math.erf(x / math.sqrt(2.0)))


def Phi_inv(p, lo=-10.0, hi=10.0):
    for _ in range(200):
        m = 0.5 * (lo + hi)
        if Phi(m) < p:
            lo = m
        else:
            hi = m
    return 0.5 * (lo + hi)


def p_max_debajo(d):
    """P(max_{t<=1} W_t < d) para W browniano estandar, d en sigmas del dia (principio de reflexion)."""
    return 2.0 * Phi(d) - 1.0


def p_rango_adentro(a, terms=50):
    """P(sup_{t<=1} |W_t| < a): el rango entero del dia queda adentro de +-a sigmas (serie clasica)."""
    s = 0.0
    for k in range(terms):
        n = 2 * k + 1
        s += ((-1) ** k) / n * math.exp(-(n * n) * math.pi ** 2 / (8.0 * a * a))
    return 4.0 / math.pi * s


def resolver(f, objetivo, lo, hi):
    for _ in range(200):
        m = 0.5 * (lo + hi)
        if f(m) < objetivo:
            lo = m
        else:
            hi = m
    return 0.5 * (lo + hi)


out = {}

# 1) SpotGamma 2019-05-10 .. 2024-05-28: el maximo del dia no paso el Call Wall en 83 %, el minimo no perforo el Put Wall en 89 %.
d_cw = resolver(p_max_debajo, 0.83, 0.0, 5.0)
d_pw = resolver(p_max_debajo, 0.89, 0.0, 5.0)
out["spotgamma_walls"] = {
    "call_wall_83pct_equivale_a_linea_a_sigmas": round(d_cw, 3),
    "put_wall_89pct_equivale_a_linea_a_sigmas": round(d_pw, 3),
    "nota": "Una linea CUALQUIERA a esa distancia (en sigmas del dia, desde la apertura) 'aguanta' lo mismo por azar. "
            "SpotGamma dice que los muros suelen estar a 0,5-2 % del precio y el sigma diario del SPX 2019-2024 ronda 1,1 %: "
            "es el rango donde el azar ya da 83-89 %. Sin la distancia de cada dia no se puede saber si hay algo mas.",
    "tabla_linea_a_d_sigmas_no_tocada": {str(d): round(p_max_debajo(d), 4) for d in (0.5, 0.75, 1.0, 1.25, 1.5, 1.75, 2.0)},
}

# 2) SpotGamma 'implied move' (+-1 desvio): cierre adentro 76 %, rango intradia adentro 65 %.
a_cierre = Phi_inv(0.5 + 0.76 / 2.0)
a_rango = resolver(p_rango_adentro, 0.65, 0.3, 5.0)
out["spotgamma_implied_move"] = {
    "cierre_adentro_76pct_equivale_a_banda_de_sigmas_realizados": round(a_cierre, 3),
    "rango_adentro_65pct_equivale_a_banda_de_sigmas_realizados": round(a_rango, 3),
    "browniano_banda_1_sigma": {"cierre_adentro": round(2 * Phi(1) - 1, 4), "rango_adentro": round(p_rango_adentro(1.0), 4)},
    "nota": "Con una banda de exactamente 1 sigma REALIZADO el rango entero quedaria adentro solo ~37 % de los dias. Que den 65 % "
            "dice que su banda equivale a ~1,4 sigmas realizados: la volatilidad implicita suele exceder a la realizada (prima de "
            "varianza). Es aritmetica de volatilidad, no prueba de posicionamiento.",
}

# 3) MenthorQ 1D Min/Max, SPX, 4 años: cierre arriba del 1D Min 87 %, debajo del 1D Max 85 %.
out["menthorq_1d_min_max"] = {
    "cierre_debajo_max_85pct_equivale_a_sigmas": round(Phi_inv(0.85), 3),
    "cierre_arriba_min_87pct_equivale_a_sigmas": round(Phi_inv(0.87), 3),
    "un_lado_1_sigma_normal": round(Phi(1.0), 4),
    "nota": "Un borde a 1 sigma da 84,1 % de cierres del lado de adentro por pura campana. 85-87 % es eso.",
}

# 4) Potencia: Osler (2000) 60,8 % publicados contra 56,2 % arbitrarios (+4,6 pp). Toques independientes necesarios.
def n_necesario(p0, efecto, z_a=1.96, z_b=0.8416, pareado=True):
    p1 = p0 + efecto
    pbar = 0.5 * (p0 + p1)
    var = pbar * (1 - pbar)
    if pareado:   # placebo con miles de lineas: casi sin ruido del lado del placebo
        return math.ceil(((z_a + z_b) ** 2) * var / efecto ** 2)
    return math.ceil(2 * ((z_a + z_b) ** 2) * var / efecto ** 2)


def efecto_detectable(n, p=0.6, z_a=1.96, z_b=0.8416):
    return (z_a + z_b) * math.sqrt(p * (1 - p) / n)


out["potencia"] = {
    "osler_efecto_pp": 4.6,
    "toques_necesarios_placebo_de_miles_de_lineas": n_necesario(0.562, 0.046, pareado=True),
    "toques_necesarios_placebo_de_una_linea_por_toque": n_necesario(0.562, 0.046, pareado=False),
    "efecto_minimo_detectable_pp_(80%_potencia, 5%_dos_colas)": {str(n): round(100 * efecto_detectable(n), 1) for n in (29, 50, 100, 300, 1000)},
    "nota": "Con 29 toques (la muestra de NQ del 07-09) solo se puede ver una diferencia de ~25 pp. El unico efecto de soporte/"
            "resistencia medido con placebo en la literatura (FX, Osler) es de 4,6 pp: hacen falta ~900 toques independientes. "
            "Toques sobre el mismo strike el mismo dia NO son independientes (la muestra son niveles).",
}

# 5) Razon NQ/QQQ: lo que vio el operador el 09-10.
r20, r41, K = 41.4447, 41.4352, 750.0
out["razon_qqq_750"] = {
    "nivel_2_0": round(K * r20, 2),
    "nivel_4_1": round(K * r41, 2),
    "diferencia_pts": round(K * (r20 - r41), 3),
    "movimiento_equivalente_del_mnq_pct": round(100 * (r20 / r41 - 1), 4),
    "nota": "La 2.0 divide el MNQ de las 16:14 NY por el QQQ del cierre (16:00): la diferencia es el movimiento del MNQ en esos "
            "14 minutos (~0,023 %), no un cambio de la relacion entre los dos mercados.",
}

# 6) Deriva por carry del futuro (supuesto: r - q entre 3 % y 4 % anual; tasas reales no medidas en esta sesion).
F = 31080.0
out["carry_por_dia_pts"] = {f"r_menos_q_{x:.3f}": round(F * x / 365.0, 2) for x in (0.030, 0.035, 0.040)}
out["carry_nota"] = ("Una razon fija tomada al cierre del viernes y usada el lunes ignora ~3 dias de carry: el NQ que corresponde al "
                     "mismo QQQ baja F*(r-q)*dias/365. SUPUESTO: no se midio r ni q hoy.")

dst = os.path.join(os.path.dirname(os.path.abspath(__file__)), "calculos_azar_y_potencia.json")
with open(dst, "w", encoding="utf-8") as fh:
    json.dump(out, fh, ensure_ascii=False, indent=2)
print(json.dumps(out, ensure_ascii=False, indent=2))
