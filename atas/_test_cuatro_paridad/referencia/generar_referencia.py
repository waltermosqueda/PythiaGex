# -*- coding: utf-8 -*-
"""generar_referencia.py — 08-10-2026. LA RESPUESTA CORRECTA contra la que se compara la 4.1 en C#: corre UNA vez la misma cuenta de la vista previa
(laboratorio/tres/auditoria_0810/preview_niveles.py, que importa backtest_familia.py) sobre sesiones ARCHIVADAS completas y guarda, minuto a
minuto, el nivel de cada una de las 24 series (precio en NQ, etiqueta, GEX en M USD) mas lo que dibujo la 3.0 (capas TRES) y la salida de la pagina
(historia por vela m2 y actuales al final). Asi los constructores no corren la cuenta pesada en paralelo con el mercado abierto.
Salida: atas/_test_cuatro_paridad/referencia/ref-<dia>.json   Uso: python -I generar_referencia.py [dia ...]  (default 2026-10-07 2026-10-08)
Prioridad BELOW_NORMAL (la pone _prio al importar el generador)."""
import os, sys, json, time
AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.abspath(os.path.join(AQUI, "..", "..", ".."))
GEN = os.path.join(RAIZ, "laboratorio", "tres", "auditoria_0810")
sys.path.insert(0, GEN); sys.path.insert(0, os.path.join(RAIZ, "laboratorio"))
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
import pandas as pd                                                   # noqa: E402
import preview_niveles as PN                                          # noqa: E402  (la misma cuenta, importada, no copiada)


def una(dia):
    t0 = time.time()
    fin = pd.Timestamp(dia) + pd.Timedelta(hours=21)                  # fin de la sesion (21:00 UTC)
    m = PN.Motor(dia, vivo=False, hasta=fin)
    m.historico()
    sal = m.salida()
    niv = {str(k): {sid: [[p, e, g] for p, e, g in lv] for sid, lv in reg.items()} for k, reg in sorted(m.niv.items())}
    tres = {}
    for capa, d in (m.tres or {}).items():
        tres[capa] = {str(k): [v[0] if not hasattr(v[0], "isoformat") else str(v[0]), v[1], [[float(p), e] for p, e in v[2]]] for k, v in d.items()}
    out = dict(dia=dia, ns_por_clave=PN.NS, generado=time.strftime("%Y-%m-%d %H:%M:%S"), origen="preview_niveles.Motor(vivo=False).historico()",
               oi_nq=getattr(m, "oi_nq", None), niv=niv, tres=tres, salida=sal)
    ruta = os.path.join(AQUI, "ref-%s.json" % dia)
    with open(ruta, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, default=str)
    print("%s: %d minutos con niveles, series %s, %.0f s -> %s" % (dia, len(niv), sorted({s for r in m.niv.values() for s in r}), time.time() - t0, ruta), flush=True)


if __name__ == "__main__":
    for d in (sys.argv[1:] or ["2026-10-07", "2026-10-08"]):
        una(d)
