# -*- coding: utf-8 -*-
"""RONDA 21 - comun: rutas, prioridad baja, RAM, periodos y registro de variantes. Reusa el motor y los rasgos de la R19
(laboratorio/ronda19_confluencias) sin tocarlos. Nada toca ATAS: solo lee lo que ya esta en el scratchpad de la R19."""
import csv, math, os, sys, time
AQUI = os.path.dirname(os.path.abspath(__file__))
R19 = os.path.join(os.path.dirname(AQUI), "ronda19_confluencias")
sys.path.insert(0, R19)
import numpy as np, pandas as pd
import r19_comun as C19          # fija prioridad BAJA del proceso al importarse

SCR19 = C19.SCR
SCR = os.path.join(os.path.dirname(SCR19), "r21")
os.makedirs(SCR, exist_ok=True)
TK = 0.25
NR = 23400
PERIODOS = ("diseno", "confirma", "guardada")
REGISTRO = os.path.join(AQUI, "registro_variantes.csv")
CAMPOS = ["fecha_hora_AR", "etapa", "familia", "variante", "salida", "parametros", "pliegue", "muestra", "ruedas", "n",
          "ops_por_rueda", "acierto_pct", "neto_op_c050", "neto_op_c100", "t_rueda_c050", "ruedas_positivas_c050", "placebo_p", "nota"]


def ram_libre_gb():
    return C19.ram_libre_gb()


def frenar_si_falta_ram(minimo=2.0):
    return C19.frenar_si_falta_ram(minimo)


def periodo_de(dia):
    if "2026-09-10" <= dia <= C19.DISENO_FIN: return "diseno"
    if C19.DISENO_FIN < dia <= C19.CONFIRMA_FIN: return "confirma"
    if C19.GUARDADA[0] <= dia <= C19.GUARDADA[1]: return "guardada"
    return "otra"


def dias_es():
    return [d for d in C19.dias("ES") if periodo_de(d) in PERIODOS]


def anotar(fila):
    nuevo = not os.path.exists(REGISTRO)
    with open(REGISTRO, "a", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=CAMPOS, extrasaction="ignore")
        if nuevo: w.writeheader()
        fila = dict(fila); fila.setdefault("fecha_hora_AR", time.strftime("%Y-%m-%d %H:%M"))
        w.writerow({k: fila.get(k, "") for k in CAMPOS})


def t_por_rueda(serie):
    serie = np.asarray(serie, float)
    if len(serie) < 2: return 0.0
    sd = serie.std(ddof=1)
    return float(serie.mean() / (sd / math.sqrt(len(serie)))) if sd > 0 else 0.0
