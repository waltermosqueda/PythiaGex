"""Potencia estadistica para pruebas de rebote en niveles (calibracion 09-10-2026, agente academico).

Pregunta: con N toques INDEPENDIENTES, cual es la ventaja minima sobre el placebo que se puede
detectar (alfa 5 % a dos colas, potencia 80 %)? Y cuantos toques hacen falta para detectar los
tamanos de efecto que publica la literatura?

Efectos de referencia (medidos por otros, no por nosotros):
  Osler (2000, FRBNY EPR): rebote 60,8 % en niveles publicados vs 56,2 % en niveles artificiales (+4,6 pp),
      miles de toques, 3 monedas, 28 meses.
  Golez & Jackwerth (2012, JFE): cierre del futuro a <= 0,375 del strike ATM 21,3 % en 136 vencimientos
      seriales vs 15 % esperado (+6,3 pp).
  Laboratorio propio (extremos_rebote.md, 08-10): MUROS_vol extremo 60,5 % vs corrido 52,8 (+7,7 pp, 311 toques);
      rebote 67,5 vs 64,8 (+2,7 pp).
Uso: python -I potencia.py
"""
import math

Z_A, Z_B = 1.959964, 0.841621  # alfa 5 % dos colas, potencia 80 %


def ventaja_minima(n, p0):
    """Ventaja minima detectable (en pp) contra un placebo de tasa p0 conocida con precision (muchas rayas)."""
    return 100 * (Z_A + Z_B) * math.sqrt(p0 * (1 - p0) / n)


def n_necesario(p0, d):
    """Toques independientes necesarios para detectar una ventaja d (fraccion) sobre p0."""
    p1 = p0 + d
    pbar = (p0 + p1) / 2
    return math.ceil(((Z_A * math.sqrt(pbar * (1 - pbar)) + Z_B * math.sqrt(p1 * (1 - p1))) / d) ** 2)


def n_efectivo(n, toques_por_racimo, icc):
    """Toques efectivos si los toques vienen en racimos (mismo strike y dia) con correlacion intra-racimo icc."""
    return n / (1 + (toques_por_racimo - 1) * icc)


if __name__ == "__main__":
    print("Ventaja minima detectable (pp) segun toques independientes, placebo 63,5 % (rayas al azar, extremos_rebote.md):")
    for n in (29, 60, 100, 200, 311, 500, 900, 1500, 3000):
        print(f"  n={n:5d}  ->  {ventaja_minima(n, 0.635):5.1f} pp")
    print()
    print("Toques independientes necesarios para efectos de la literatura:")
    for nombre, p0, d in (("Osler +4,6 pp sobre 56,2 %", 0.562, 0.046),
                          ("Golez +6,3 pp sobre 15 % (pin al cierre)", 0.15, 0.063),
                          ("lab MUROS_vol extremo +7,7 pp sobre 52,8 %", 0.528, 0.077),
                          ("lab MUROS_vol rebote +2,7 pp sobre 64,8 %", 0.648, 0.027)):
        print(f"  {nombre:45s} -> {n_necesario(p0, d):6d}")
    print()
    print("Efecto racimo: 311 toques en racimos de 5 por strike-dia")
    for icc in (0.0, 0.1, 0.2, 0.3):
        ne = n_efectivo(311, 5, icc)
        print(f"  icc={icc:.1f}  n_efectivo={ne:6.1f}  ventaja minima={ventaja_minima(ne, 0.635):5.1f} pp")
