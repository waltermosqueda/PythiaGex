# -*- coding: utf-8 -*-
"""ZONAS, paso 1: cribado (TODO se registra). Universo de la familia = vueltas de la ola cuyo extremo toco un nivel:
zona (-T..X/4 de cualquier nivel) o barrida (perforo 0..X/2 y el precio volvio del otro lado). Entradas 10:00-15:30.
Salidas miradas en el cribado: S6 (1R, acierto) y S1 (2R, desplazamiento)."""
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import zonas_comun as Z

df = Z.cargar()
zona = Z.contacto(df, "todos", 0, 0.25, lo_T=True) > 0
barr = Z.contacto(df, "todos", 1e-6, 0.5, vuelve=True) > 0
U = zona | barr
SAL = ("S6", "S1")
FAM = "a+d (zona o barrida)"

print("== bases")
Z.evaluar("Z1_todas", "linea de base", "todas las vueltas 10:00-15:30", np.ones(len(df), bool), SAL)
Z.evaluar("Z1_sin_nivel", "contraste", "vueltas sin nivel (ni zona ni barrida)", ~U, SAL)
Z.evaluar("Z1_U", FAM, "zona (-T..X/4) o barrida (0..X/2 y vuelve), cualquier nivel", U, SAL)
Z.evaluar("Z1_zona", "a", "zona -T..X/4, cualquier nivel", zona, SAL)
Z.evaluar("Z1_barrida", "d", "barrida 0..X/2 y vuelve, cualquier nivel", barr, SAL)

C = {
    "F1": df.F1 == 1, "F2": df.F2 == 1, "F3": df.F3 == 1, "F4": df.F4 == 1, "F6": df.F6 == 1,
    "F1oF2": (df.F1 == 1) | (df.F2 == 1), "F1oF6": (df.F1 == 1) | (df.F6 == 1), "F1yF2": (df.F1 == 1) & (df.F2 == 1),
    "abs_rel>=0.7": df.of_abs_lado_pasivo_rel >= 0.7, "abs_rel>=1.5": df.of_abs_lado_pasivo_rel >= 1.5,
    "ice60>=3": df.of_icebergs_60 >= 3, "ice60>=7": df.of_icebergs_60 >= 7,
    "ice_favor>contra": df.of_icebergs_60 > df.of_icebergs_contra_60,
    "sin_veto": df.veto_caudal == 0, "veto_caudal": df.veto_caudal == 1,
    "ret_con_agresores": df.ctx_ret_con_agresores == 1, "ret_sin_agresores": df.ctx_ret_con_agresores == 0,
    "delta60>0": df.of_delta_60 > 0, "delta60<0": df.of_delta_60 < 0, "delta300<0": df.of_delta_300 < 0,
    "netbig_favor>0": df.of_netbig60_a_favor > 0,
    "imb_favor>contra": df.of_imb_favor_3v > df.of_imb_contra_3v,
    "hacia_vwap": df.hacia_vwap == 1, "vwap_a>=1R": df.dist_vwap_R >= 1, "vwap_a>=2R": df.dist_vwap_R >= 2,
    "estirado_1sd": df.vwap_sd_firmado <= -1, "ext_estirado_1.5sd": df.ext_vwap_sd_firmado <= -1.5,
    "ruido_en_contra": df.ctx_ruido == "fuera_en_contra", "ruido_dentro": df.ctx_ruido == "dentro",
    "ruido_a_favor": df.ctx_ruido == "fuera_a_favor",
    "a_favor_marea": df.a_favor_marea == 1, "contra_marea": df.a_favor_marea == 0,
    "marea_con_agresores": df.marea_con_agresores == 1,
    "va_y_vuelve": df.regimen_va_y_vuelve == 1,
    "h10-12": df.hora < 12, "h12-14": (df.hora >= 12) & (df.hora < 14), "h14-15:30": df.hora >= 14,
    "prof>=2X": df.ctx_prof_X >= 2, "prof=0(extremo_marea)": df.ctx_prof_X == 0,
    "extension_fuera_ruido": df.extension_fuera_ruido == 1,
    "en_va_ayer": df.ctx_en_va_ayer == "dentro", "fuera_va_ayer": df.ctx_en_va_ayer != "dentro",
    "ib_ambas": df.ctx_ext_ib == "ambas",
    "vel_ret_alta(>=p75)": df.ctx_vel_ret >= 0.1, "dur_ret>=360s": df.ctx_dur_ret_s >= 360,
}
print("== condiciones solas dentro del universo U")
for k, m in C.items():
    Z.evaluar("Z1_U+" + k, FAM, "U + " + k, U & m.to_numpy(), SAL)

print("== por grupo de nivel (zona o barrida del grupo)")
for g in ["vwap_rth_bandas", "vwap_rth_2sd", "vwap_centro", "vwap_jornada", "vwap_lento", "ayer", "ayer_extremos", "ayer_valor",
          "noche", "ib", "ib_ext", "semana", "r50", "extremos", "estaticos_sin_r50", "valor"]:
    mg = (Z.contacto(df, g, 0, 0.25, lo_T=True) > 0) | (Z.contacto(df, g, 1e-6, 0.5, vuelve=True) > 0)
    Z.evaluar("Z1_G_" + g, FAM, "zona o barrida de un nivel del grupo " + g + ": " + ",".join(Z.GRUPOS[g]), mg, SAL)

print("== geometria del contacto (cualquier nivel)")
GEO = {"toque_sin_perforar(-T..0)": Z.contacto(df, "todos", 0, 0.0, lo_T=True) > 0,
       "perfora_0..X/4": Z.contacto(df, "todos", 1e-6, 0.25, vuelve=True) > 0,
       "perfora_X/4..X/2": Z.contacto(df, "todos", 0.25 + 1e-6, 0.5, vuelve=True) > 0,
       "perfora_X/2..X": Z.contacto(df, "todos", 0.5 + 1e-6, 1.0, vuelve=True) > 0,
       "conf_zona>=2": Z.contacto(df, "todos", 0, 0.25, lo_T=True) >= 2,
       "conf_zona>=3": Z.contacto(df, "todos", 0, 0.25, lo_T=True) >= 3,
       "conf_U>=2(-T..X/2)": Z.contacto(df, "todos", 0, 0.5, lo_T=True) >= 2}
for k, m in GEO.items():
    Z.evaluar("Z1_geo_" + k, FAM, "contacto " + k, m, SAL)
