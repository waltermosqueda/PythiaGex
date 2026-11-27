# -*- coding: utf-8 -*-
"""ZONAS, paso 2: informacion de cada condicion a NIVEL EVENTO (todas las vueltas que cumplen, sin una-posicion-por-vez),
10:00-15:30. Mismas condiciones del cribado (paso 1, ya registradas) + cruces de a dos con el contexto de reversion.
Cada cruce NUEVO (combinacion que no estaba en el paso 1) se registra como variante con la simulacion completa (S6 y S1).
Salida: z02_nivel_evento.csv (n, acierto y R medio por salida, t por rueda con costo 0,50 operando todo)."""
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import itertools, math
import numpy as np, pandas as pd
import zonas_comun as Z

df = Z.cargar()
H = ((df.seg_rueda >= Z.H_INI) & (df.seg_rueda < Z.H_FIN)).to_numpy()
zona = Z.contacto(df, "todos", 0, 0.25, lo_T=True) > 0
barr = Z.contacto(df, "todos", 1e-6, 0.5, vuelve=True) > 0
U = zona | barr
est_z = Z.contacto(df, "estaticos_sin_r50", 0, 0.25, lo_T=True) > 0
est_b = Z.contacto(df, "estaticos_sin_r50", 1e-6, 0.5, vuelve=True) > 0


def ev(mask, sal):
    m = np.asarray(mask, bool) & H & df[sal + "_pts"].notna().to_numpy()
    pts = df[sal + "_pts"].to_numpy()[m]; R = df["R"].to_numpy()[m]
    if len(pts) < 5: return dict(n=len(pts))
    d = pd.Series(pts - 0.5, index=df["dia"].to_numpy()[m]).groupby(level=0).sum().reindex(Z.DIAS, fill_value=0)
    return dict(n=len(pts), por_rueda=round(len(pts) / 26, 2), acierto=round(100 * (pts > 0).mean(), 1), R_medio=round(float((pts / R).mean()), 3),
                neto050=round(float(pts.mean() - 0.5), 3), t_todo=round(Z.t_rueda(d), 2))


C = {
    "U": U, "zona": zona, "barrida": barr, "sin_nivel": ~U, "est_zona": est_z, "est_barrida": est_b, "est_U": est_z | est_b,
    "F1": df.F1 == 1, "F2": df.F2 == 1, "F6": df.F6 == 1, "F1oF2": (df.F1 == 1) | (df.F2 == 1), "F3": df.F3 == 1,
    "ice_favor>contra": df.of_icebergs_60 > df.of_icebergs_contra_60, "sin_veto": df.veto_caudal == 0,
    "ret_con_agresores": df.ctx_ret_con_agresores == 1, "delta300<0": df.of_delta_300 < 0,
    "hacia_vwap": df.hacia_vwap == 1, "vwap_a>=1R": df.dist_vwap_R >= 1, "ext_estirado_1.5sd": df.ext_vwap_sd_firmado <= -1.5,
    "estirado_1sd": df.vwap_sd_firmado <= -1, "contra_marea": df.a_favor_marea == 0, "a_favor_marea": df.a_favor_marea == 1,
    "prof=0": df.ctx_prof_X == 0, "en_va_ayer": df.ctx_en_va_ayer == "dentro", "va_y_vuelve": df.regimen_va_y_vuelve == 1,
    "h10-12": df.hora < 12, "h12-15:30": df.hora >= 12, "ruido_en_contra": df.ctx_ruido == "fuera_en_contra",
    "ruido_dentro": df.ctx_ruido == "dentro", "vel_ret<p75": df.ctx_vel_ret < 0.1,
}
C = {k: np.asarray(v, bool) for k, v in C.items()}
filas = []
for k, m in C.items():
    for sal in ("S6", "S1", "S5", "S3"):
        r = ev(m, sal); r.update(cond=k, salida=sal); filas.append(r)
out = pd.DataFrame(filas)
print("== condiciones solas (nivel evento, 10:00-15:30)")
p = out.pivot_table(index="cond", columns="salida", values=["n", "acierto", "R_medio", "t_todo"]).round(2)
print(p.to_string())

# cruces de a dos: lugar x (flujo o contexto)
LUG = ["U", "zona", "barrida", "est_U", "est_zona", "est_barrida"]
OTR = [k for k in C if k not in LUG + ["sin_nivel"]]
f2 = []
for a in LUG:
    for b in OTR:
        m = C[a] & C[b]
        sim = Z.evaluar("Z2_" + a + "&" + b, "a+d (cruce lugar x condicion)", a + " & " + b + " (lugar: zona -T..X/4 / barrida 0..X/2 y vuelve)",
                        m, ("S6", "S1"), nota="cruce de a dos; tambien mirado a nivel evento", imprimir=False)
        for sal in ("S6", "S1"):
            r = ev(m, sal); r.update(cond=a + " & " + b, salida=sal)
            r.update({"sim_n": sim[sal]["n"], "sim_acierto": sim[sal].get("acierto_pct"), "sim_neto050": sim[sal].get("neto_op_c050"),
                      "sim_t050": sim[sal].get("t_rueda_c050")})
            f2.append(r)
o2 = pd.DataFrame(f2)
o2.to_csv(os.path.join(Z.AQUI, "z02_nivel_evento.csv"), index=False)
for sal in ("S6", "S1"):
    q = o2[(o2.salida == sal) & (o2.n >= 40)].sort_values("R_medio", ascending=False)
    print("== cruces lugar x condicion, %s, top 25 por R medio (n >= 40 eventos)" % sal)
    print(q.head(25).to_string(index=False))
