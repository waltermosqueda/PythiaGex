# -*- coding: utf-8 -*-
"""
VALIDACION EN LA RESERVA de la unica pista de la tanda 1: ir EN CONTRA del delta extremo de la vela de reloj de 5 minutos (T5m invertida).
La reserva son 14 sesiones (31-07 al 19-08) que ningun investigador miro. Se corre UNA vez. El pre-registro se escribe ANTES de calcular nada.
"""
import os, sys, time, datetime
import numpy as np, pandas as pd
import escalas_lib as L
B = L.B
AQUI = os.path.dirname(os.path.abspath(__file__)); MD = os.path.join(AQUI, "resultados", "reserva_T5m.md")
PRE = """# Validacion en la reserva: T5m invertida (pre-registro escrito ANTES de correr, %s)

**Regla (congelada, de escalas_lib + escalas_umbrales.json):** al cierre de cada vela de RELOJ de 5 minutos dentro de la rueda (13:32-20:00 UTC), r = delta / volumen de esos
300 s; dispara si |r| >= 0,06613 y volumen >= 10.242 contratos; lado = CONTRA el signo del delta; entrada en ultimo[t]; barrera +-8 puntos en 600 s; costo %.2f pts (empate %.1f %%).
**H1 (principal):** toda la rueda. **H2 (secundaria):** solo disparos entre 15:00 y 18:00 UTC (la franja que se vio mejor post-hoc).
**Criterio para que sobreviva (cada hipotesis por separado):** n >= 100, acierto >= empate + 2 puntos (%.1f %%), z contra placebo (200 sorteos, misma sesion y media hora, mismos lados) >= 2,5 y
>= 70 %% de los dias arriba de 50 %%. Si H1 falla y H2 pasa, queda como PISTA (dos miradas). Si las dos fallan: NADA, y no se retoca.
**Antecedente (no cuenta como prueba):** explorar 56,4 %% (n 204), confirmar mirado post-hoc 62,2 %% (n 111); las dos con tablas que tenian el error del precio congelado.
"""


def correr(R, U, nombre):
    res, det = L.juzgar_todo(R, U, variantes=["T5m"], invertir=("T5m",)); d = det["T5m"]
    lado = np.array(d["lado"]); dias = np.array(d["ses"]); sod = np.array(d["sod"]); out = []
    for etiqueta, m in (("H1 toda la rueda", np.ones(len(lado), bool)), ("H2 15:00-18:00 UTC", (sod >= 15 * 3600) & (sod < 18 * 3600))):
        for (x, h) in L.BARRERAS:
            y = np.array(d["y"][(x, h)])[m]; l = lado[m]; dd = dias[m]; ok = y != 0
            if ok.sum() == 0: continue
            ac = float(((y[ok] * l[ok]) > 0).mean())
            sub = dict(ses=list(dd), lado=list(l), sod=list(sod[m]))
            pm, psd, _ = L.placebo(R, sub, (x, h)); zp = (ac - pm) / psd if psd > 0 else np.nan
            pd_ = pd.Series((y[ok] * l[ok]) > 0).groupby(dd[ok]).mean()
            out.append("%s | %s | +-%d/%ds: n %d, acierto %.1f %% (empate %.1f), placebo %.1f +- %.1f, z contra placebo %+.2f, dias arriba de 50 %%: %d de %d, peor dia %.0f %%, neto %+.2f pts/op" % (
                nombre, etiqueta, x, h, ok.sum(), 100 * ac, 100 * B.empate(x), 100 * pm, 100 * psd, zp, int((pd_ > 0.5).sum()), len(pd_), 100 * pd_.min(), (2 * ac - 1) * x - B.COSTO_PTS))
    return out


if __name__ == "__main__":
    if not os.path.exists(MD):
        open(MD, "w", encoding="utf-8").write(PRE % (datetime.datetime.now().strftime("%Y-%m-%d %H:%M"), B.COSTO_PTS, 100 * B.empate(8), 100 * B.empate(8) + 2))
    U = L.cargar_umbrales(); t0 = time.time()
    lineas = []
    if "--referencia" in sys.argv:   # las mismas cuentas en explorar y confirmar con las tablas ya corregidas (solo referencia)
        T = B.todas_las_sesiones(); lineas += correr(B.explorar(T), U, "explorar (corregido)") + correr(B.confirmar(T), U, "confirmar (corregido, ya mirado)")
    else:
        lineas += correr(B.reserva(), U, "RESERVA")
    print("\n".join(lineas)); print("%.0f s" % (time.time() - t0))
    open(MD, "a", encoding="utf-8").write("\n## Corrida %s\n\n" % datetime.datetime.now().strftime("%Y-%m-%d %H:%M") + "\n".join("- " + x for x in lineas) + "\n")
