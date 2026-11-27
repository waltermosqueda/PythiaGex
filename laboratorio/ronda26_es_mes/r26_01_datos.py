# -*- coding: utf-8 -*-
"""RONDA 26, paso 1: los DATOS de cada par ES/MES (PREREGISTRO secciones 2 y 4). Por dia, de a UNA cinta por vez y en prioridad baja:
  A. rangos RTH de las ruedas previas que hagan falta para la X del ES (cinta de ES leida al paso > tabla de la ronda 16 > velas de MES de 5 min);
  B. X del ES del dia (la MISMA para ES y MES);
  C. ES: ola (X) y marea (4X) orden por orden en la jornada 18:00-17:00 NY, verificadas contra test_ola.Maquina; fotos al cierre de cada
     minuto RTH; pivotes con su numero de orden; delta acumulado total y por tamano de orden; ultimo precio de cada segundo RTH;
  D. MES: lo mismo con la X del ES;
  E. alineacion del par (mismo vencimiento): ES - MES en los segundos RTH en que operaron los dos; alinea si la mediana de |dif| <= 0,50
     y el percentil 95 <= 1,00. Si no alinea, el par queda AFUERA (se dice en la salida y en pares.json).
No mira desenlaces: solo escribe en datos\\ronda26.
Uso: python -I r26_01_datos.py <tanda> <dia> [<dia> ...]     (tanda: tanda0 | tanda1 | hoy)"""
import gc
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import r26_comun as C

C.prioridad_baja()
RANGOS_CINTA = os.path.join(C.DATOS_OFICIAL, "rangos-cinta-ES.json")
PARES = os.path.join(C.DATOS, "pares.json")
CORTE_HOY_UTC_MS = C.ms_ny("2026-11-27", 11, 48)     # control de hoy: las dos cintas cortadas a las 16:48:00 UTC (11:48 NY)


def rangos_combinados(necesarios):
    """{dia: rango} con prioridad cinta de ES > tabla r16 > velas de MES. Lee (streaming) las cintas de ES de los dias necesarios que falten."""
    cache = json.load(open(RANGOS_CINTA)) if os.path.exists(RANGOS_CINTA) else {}
    for d in sorted(necesarios):
        if d in cache or d <= "2026-11-13":
            continue
        ruta = C.archivo("ES", d)
        if ruta is None:
            continue
        C.frenar_si_falta_ram()
        t0 = time.time()
        r = C.rango_rth_streaming(ruta, d)
        if r is not None:
            cache[d] = r
            print("  rango RTH de la cinta de ES %s: %.2f-%.2f = %.2f (cubre %.2f h, %.1f s)" % (d, r["l"], r["h"], r["h"] - r["l"], r["cubre_h"],
                                                                                              time.time() - t0), flush=True)
        json.dump(cache, open(RANGOS_CINTA, "w"), indent=1)
    out = dict(C.rangos_velas_mes())
    out.update(C.rangos_r16())
    out.update(cache)
    return out


def previos_necesarios(dias):
    """Las ruedas habiles anteriores que pueden entrar en las 10 previas de cada dia (lunes a viernes, 16 dias corridos para atras)."""
    import pandas as pd
    nec = set()
    for d in dias:
        for k in range(1, 22):
            x = (pd.Timestamp(d) - pd.Timedelta(days=k))
            if x.weekday() < 5:
                nec.add(x.strftime("%Y-%m-%d"))
    return nec


def main():
    if len(sys.argv) < 3:
        raise SystemExit(__doc__)
    tanda, dias = sys.argv[1], sys.argv[2:]
    os.makedirs(C.DATOS, exist_ok=True)
    t_ini = time.time()
    print("RONDA 26 - datos, %s: %s  (RAM libre %.0f MB)" % (tanda, " ".join(dias), C.ram_libre_mb()), flush=True)
    print("A. rangos RTH previos", flush=True)
    rangos = rangos_combinados(previos_necesarios(dias))
    # control cruzado: rango de la tabla r16 (ES) contra el de las velas de MES en los dias que estan en las dos
    vm = C.rangos_velas_mes()
    r16 = C.rangos_r16()
    comunes = sorted(set(vm) & set(r16))
    if comunes:
        dif = np.array([(r16[d]["h"] - r16[d]["l"]) - (vm[d]["h"] - vm[d]["l"]) for d in comunes])
        print("  control: rango ES (r16) - rango MES (velas 5 min) en %d ruedas comunes (%s..%s): mediana %+.2f, max |dif| %.2f"
              % (len(comunes), comunes[0], comunes[-1], float(np.median(dif)), float(np.abs(dif).max())), flush=True)
    pares = json.load(open(PARES)) if os.path.exists(PARES) else {}
    for dia in dias:
        X, med, prev = C.x_de(dia, rangos)
        print("\nB. %s: X del ES = %.2f (mediana %.3f de %d ruedas: %s)" % (dia, X, med, len(prev),
              ", ".join("%s %.2f [%s]" % (d[5:], r, f) for d, r, f in prev)), flush=True)
        if len(prev) < 10:
            print("   OJO: menos de 10 ruedas previas completas", flush=True)
        res = {}
        for inst in ("ES", "MES"):
            C.frenar_si_falta_ram()
            ruta = C.archivo(inst, dia)
            if ruta is None:
                print("   FALTA la cinta de %s %s" % (inst, dia), flush=True)
                res = None
                break
            t0 = time.time()
            hasta = CORTE_HOY_UTC_MS if dia == C.HOY else None
            r = C.procesar(inst, dia, X, ruta, hasta_ms=hasta)
            C.guardar(r)
            m = r["meta"]
            print("   %s %s: %s | ordenes %d (RTH %d) | duplicados exactos %d | fuera de orden %d | RTH %s-%s, hueco max %.0f s | rango RTH %.2f-%.2f"
                  " | pivotes X %d / 4X %d | identicos a test_ola.Maquina: X %s, 4X %s | %.1f s" %
                  (inst, dia, m["archivo"], m["ordenes_ventana"], m["ordenes_rth"], m["duplicados_exactos"], m["fuera_de_orden"],
                   m["rth_primera"], m["rth_ultima"], m["rth_hueco_max_s"], m["rth_l"], m["rth_h"], m["verificacion"]["X"]["pivotes"],
                   m["verificacion"]["4X"]["pivotes"], m["verificacion"]["X"]["identicos"], m["verificacion"]["4X"]["identicos"],
                   time.time() - t0), flush=True)
            if inst == "ES" and dia != C.HOY and dia > "2026-11-13" and dia in rangos and rangos[dia]["fuente"] == "cinta-ES":
                if abs((rangos[dia]["h"] - rangos[dia]["l"]) - (m["rth_h"] - m["rth_l"])) > 1e-9:
                    print("   OJO: el rango RTH de la lectura completa no coincide con el de la lectura streaming", flush=True)
            res[inst] = r["ult_seg"]
            del r
            gc.collect()
        if res is None:
            pares[dia] = {"tanda": tanda, "X": X, "alinea": False, "motivo": "falta una cinta"}
            continue
        al = C.alineacion(res["ES"], res["MES"])
        al.update({"tanda": tanda, "X": X, "mediana10": med, "previas": [d for d, _, _ in prev], "fuentes": sorted(set(f for _, _, f in prev))})
        pares[dia] = al
        print("E. %s alineacion ES-MES: %d segundos, mediana %+.2f, mediana |dif| %.2f, p95 |dif| %.2f, max %.2f -> %s" %
              (dia, al["segundos"], al.get("mediana_dif", np.nan), al.get("mediana_abs", np.nan), al.get("p95_abs", np.nan),
               al.get("max_abs", np.nan), "ALINEA" if al["alinea"] else "NO ALINEA: el par queda afuera"), flush=True)
        json.dump(pares, open(PARES, "w"), indent=1)
    print("\nListo en %.0f s (RAM libre %.0f MB)" % (time.time() - t_ini, C.ram_libre_mb()), flush=True)


if __name__ == "__main__":
    main()
