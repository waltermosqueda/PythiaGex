"""Potencia para la fase ADELANTE (informe final calibracion_1009). python -I potencia_adelante.py

Efecto de diseno (DE) estimado de la dispersion del placebo de azar en PRUEBA (p5-p95 de los resultados de los probadores)
contra la dispersion binomial. Muestra necesaria para detectar una ventaja d sobre una base p0 ~ 0,47 (vara del azar del juez
del operador, PREREGISTRO sec. 10) con 80 % de potencia, prueba unilateral contra el azar. Llegadas por sesion medidas en PRUEBA.
"""
from math import sqrt
from statistics import NormalDist
import datetime as dt
import json, os

N = NormalDist()
# (llegadas en PRUEBA, azar p5, azar p95, azar media)  -- de probador2/resultados/resumen_PRUEBA.json y probador4/resumen_PRUEBA.md
casos = {"C07 noche": (110, 38.64, 55.41, 47.07), "C08 noche": (125, 38.9, 57.1, 47.8), "C19 noche": (694, 46.8, 53.8, 50.3),
         "C20 noche": (359, 42.1, 52.9, 47.6), "C22 noche": (431, 44.0, 53.4, 48.7), "C19 dia": (488, 47.6, 55.4, 51.5),
         "C22 dia": (323, 44.4, 55.5, 49.9)}
de = {}
for k, (n, a, b, p) in casos.items():
    sd = (b - a) / (2 * 1.645)
    sb = 100 * sqrt(p / 100 * (1 - p / 100) / n)
    de[k] = round((sd / sb) ** 2, 2)

# llegadas por sesion en PRUEBA (6 sesiones)
por_sesion = {"C08 noche": 125 / 6, "C07 noche": 110 / 6, "C08 dia": 98 / 6, "C08 noche+dia": (125 + 98) / 6}

FERIADOS = {dt.date(2026, 11, 26), dt.date(2026, 12, 25), dt.date(2027, 1, 1), dt.date(2027, 1, 18)}
def fecha_sesion(n, desde=dt.date(2026, 10, 12)):
    d, k = desde, 0
    while True:
        if d.weekday() < 5 and d not in FERIADOS:
            k += 1
            if k == n:
                return d.isoformat()
        d += dt.timedelta(days=1)

p0 = 0.47
tabla = []
for DE in (1.3, 1.5):
    for d in (0.046, 0.05, 0.10):
        for m, lab in ((1, "1 hipotesis"), (3, "3 hipotesis (Holm)")):
            za, zb = N.inv_cdf(1 - 0.05 / m), N.inv_cdf(0.80)
            n = (za + zb) ** 2 * p0 * (1 - p0) / d ** 2 * DE
            fila = {"DE": DE, "ventaja_pp": round(100 * d, 1), "hipotesis": lab, "llegadas": round(n)}
            for s, r in por_sesion.items():
                ses = n / r
                fila["sesiones_" + s] = round(ses)
                fila["sesiones_" + s + "_con_80pct_utiles"] = round(ses / 0.8)
            fila["fecha_aprox_noches_C08_80pct_utiles"] = fecha_sesion(round(n / por_sesion["C08 noche"] / 0.8))
            tabla.append(fila)

out = {"efecto_diseno": de, "llegadas_por_sesion_PRUEBA": {k: round(v, 1) for k, v in por_sesion.items()}, "tabla": tabla,
       "nota": "unilateral alfa 0,05 (o 0,05/3), potencia 80 %, base 0,47; 'utiles' = solo ~80 % de las noches tienen QQQ completo (datos/indice.json: 17 de 21)."}
dst = os.path.join(os.path.dirname(os.path.abspath(__file__)), "potencia_adelante.json")
json.dump(out, open(dst, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print(json.dumps(out["efecto_diseno"]))
for f in tabla:
    print(f)
