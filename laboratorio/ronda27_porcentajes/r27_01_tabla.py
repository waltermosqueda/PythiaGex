# -*- coding: utf-8 -*-
"""RONDA 27, paso 1: la TABLA POR MINUTO de las 57 jornadas de ES (08-09 a 25-11-2026), de a UNA cinta por vez, en orden de fecha,
prioridad BELOW_NORMAL, freno si la RAM libre baja de 2,5 GB. Las dos primeras (08 y 09-09) son calentamiento: alimentan la historia
(rangos, ayer, tamanos de orden) y no se evalúan.
Salidas en PythiaGex/datos/ronda27: minutos/ES-<dia>.parquet, historia.json (ruedas previas + las de las cintas), hist_tam/ES-<dia>.npy,
info_jornadas.json y tabla_minutos.parquet (las 55 evaluadas, con la columna 'tramo').
Uso: python -I r27_01_tabla.py [--solo AAAA-MM-DD ...]"""
import json, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r27_comun as K
import numpy as np, pandas as pd

DIS_FIN, CONF_FIN = "2026-10-26", "2026-11-13"


def tramo(dia):
    if dia in K.CALENTAMIENTO: return "calentamiento"
    if dia <= DIS_FIN: return "diseno"
    if dia <= CONF_FIN: return "confirmacion"
    return "final"


def main():
    args = sys.argv[1:]
    solo = args[args.index("--solo") + 1:] if "--solo" in args else None
    t0 = time.time()
    dmin = os.path.join(K.DATOS, "minutos"); dht = os.path.join(K.DATOS, "hist_tam")
    for d_ in (dmin, dht): os.makedirs(d_, exist_ok=True)
    cin = K.cintas("ES")
    dias = sorted(d for d in cin if "2026-09-08" <= d <= "2026-11-25")
    print("cintas de ES: %d (%s .. %s)" % (len(dias), dias[0], dias[-1]), flush=True)
    hist = K.historia_semilla()
    hist_tam = []
    infos = {}
    f = K.curva_f()
    for dia in dias:
        g = K.esperar_ram(2.5)
        t1 = time.time()
        tab, hoy, ht, info = K.procesar_jornada(dia, cin[dia], hist, hist_tam, f)
        tab["tramo"] = tramo(dia)
        if solo is None or dia in solo:
            tab.to_parquet(os.path.join(dmin, "ES-%s.parquet" % dia), index=False)
        np.save(os.path.join(dht, "ES-%s.npy" % dia), ht)
        hist[dia] = hoy; hist_tam.append(ht)
        info["tramo"] = tramo(dia); info["seg"] = round(time.time() - t1, 1); info["ram_gb"] = round(g, 2)
        infos[dia] = info
        r = tab["y_X"]
        print("%s %-13s X %5.2f ordenes %8d retro_t %d | resueltas X %.3f arriba %.3f | T %3d R %3d E %3d N %3d | %.1f s RAM %.1f" % (
            dia, info["tramo"], info["X"], info["ordenes"], info["retrocesos_t"], r.notna().mean(), r.mean(),
            (tab.tipo == "T").sum(), (tab.tipo == "R").sum(), (tab.tipo == "E").sum(), (tab.tipo == "N").sum(), info["seg"], g), flush=True)
        del tab
        if solo is not None and dia >= max(solo):
            break
    json.dump(hist, open(os.path.join(K.DATOS, "historia.json"), "w"), indent=0)
    json.dump(infos, open(os.path.join(K.DATOS, "info_jornadas.json"), "w"), indent=0, default=float)
    if solo is None:
        partes = [pd.read_parquet(os.path.join(dmin, "ES-%s.parquet" % d)) for d in dias if tramo(d) != "calentamiento"]
        T = pd.concat(partes, ignore_index=True)
        T.to_parquet(os.path.join(K.DATOS, "tabla_minutos.parquet"), index=False)
        print("tabla: %d filas, %d ruedas | %.0f s" % (len(T), T.dia.nunique(), time.time() - t0), flush=True)


if __name__ == "__main__":
    main()
