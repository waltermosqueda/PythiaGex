# -*- coding: utf-8 -*-
"""EQUIVALENCIA DEL GATILLO REBOTE: los disparos que el indicador (GatilloRebote.cs) escribio en
pythiagex-gatillos-archivo-<inst>-*.jsonl (tipo rebote·*) contra los del laboratorio
(gatillo_rebote.py, mismos datos del centinela), minuto a minuto, para un dia. Si difieren mucho,
lo que se dibuja no es lo que se midio.
Uso: python laboratorio/rebote_equivalencia.py MNQ 2026-09-10 [--modo tendencia|todos]
"""
import glob
import io
import json
import os
import sys
from datetime import datetime, timedelta

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gatillo_rebote as gr  # noqa: E402
from rebote_dominantes_dia import cargar  # noqa: E402

ATAS = os.path.join(os.environ.get("APPDATA", ""), "ATAS")


def main():
    inst = sys.argv[1]; dia = sys.argv[2]
    modo = sys.argv[sys.argv.index("--modo") + 1] if "--modo" in sys.argv else "tendencia"
    es_nq = inst.startswith("MNQ") or inst.startswith("NQ")
    gr.ESCALA[0] = 1.0 if es_nq else 0.25
    f = 1.0 if es_nq else 0.25
    vs = cargar(inst)
    lab = [e for e in gr.fuegos(vs, 10 * f, 25 * f, 15 * f, 5, 0.0, 10.0 if es_nq else 5.0) if e["dia"] == dia]
    if modo == "tendencia":
        lab = [e for e in lab if gr.a_favor(e, "sobre_zero")]
    ind = []
    for p in glob.glob(os.path.join(ATAS, "pythiagex-gatillos-archivo-%s*M1*.jsonl" % inst)):
        for l in io.open(p, encoding="utf-8", errors="replace"):
            try:
                d = json.loads(l)
            except Exception:
                continue
            if d.get("tipo", "").startswith("rebote") and d["t"][:10] == dia:
                ind.append(d)
        print("indicador:", p, len(ind), "disparos rebote el", dia)
    kl = {(e["t"].strftime("%H:%M"), e["lado"]): e for e in lab}
    # el indicador anota la hora de CIERRE de la vela (la apertura de la siguiente); el laboratorio, la de apertura
    ki = {((datetime.strptime(d["t"][:19], "%Y-%m-%dT%H:%M:%S") - timedelta(minutes=1)).strftime("%H:%M"), "abajo" if d["lado"] > 0 else "arriba"): d for d in ind}
    comunes = sorted(set(kl) & set(ki)); solo_lab = sorted(set(kl) - set(ki)); solo_ind = sorted(set(ki) - set(kl))
    print("laboratorio %d | indicador %d | coinciden %d | solo laboratorio %d | solo indicador %d" % (len(kl), len(ki), len(comunes), len(solo_lab), len(solo_ind)))
    loc = lambda hm: (datetime.strptime(hm, "%H:%M") - timedelta(hours=3)).strftime("%H:%M")
    for k in comunes:
        e, d = kl[k], ki[k]
        print("  = %s local %-6s lab nivel %8.1f %-5s | ind nivel %8.2f %-12s toque %s" % (loc(k[0]), k[1], e["L"], e["tipo"], d["dom"], d["tipo"], d["dz"]))
    for k in solo_lab:
        e = kl[k]; print("  L %s local %-6s SOLO laboratorio: nivel %8.1f %s" % (loc(k[0]), k[1], e["L"], e["tipo"]))
    for k in solo_ind:
        d = ki[k]; print("  I %s local %-6s SOLO indicador: nivel %8.2f %s toque %s" % (loc(k[0]), k[1], d["dom"], d["tipo"], d["dz"]))


if __name__ == "__main__":
    main()
