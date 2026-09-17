# -*- coding: utf-8 -*-
"""modelo_es_03_porque_difiere.py - AUDITORIA (no cambia el veredicto): la p que anoto el VIVO (0,18-0,29, casi clavada en 0,23-0,24) no se parece a la p reconstruida
con los niveles del rebobinado (0,38-0,54). Dos controles:
  (1) mi port del modelo contra el C#: los disparos 'modelo' del registro de ARCHIVO (ultima corrida, #139) vela por vela contra mi p sobre esa misma corrida;
  (2) hipotesis 'el vivo corre SIN NIVELES': la p recalculada con todos los niveles en blanco (los rellenos del codigo: mn -40, mp +40, dominantes +-40, zero 0, mc30 0)
      contra la p anotada por el vivo en cada disparo. Y que niveles anoto el centinela del vivo en esas velas."""
import io, json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import modelo_es_lib as M

D, E = M.corridas()
w = D[D["corrida"] == 139].set_index("ap").sort_index(); p139 = M.p_congelada(w)
arch = M.disparos_log(True)
print("(1) registro de ARCHIVO (corrida #139, C#) contra mi port en Python sobre la misma corrida:")
for r in arch.itertuples(index=False):
    print("    %s lado %+d precio %.2f p C# %.2f | mi p %.4f cierre %.2f" % (r.ap, r.lado, r.precio, r.p, p139.loc[r.ap, "p"] if r.ap in p139.index else float("nan"), w.loc[r.ap, "c"] if r.ap in w.index else float("nan")))
mios = M.elegir(p139, 0.70, 5); mios = mios[(mios.index >= "2026-09-15 22:00") & M.en_rueda(mios.index, "18:00", "20:00")]
print("    mis disparos de tarde sobre #139: %s" % ", ".join("%s(%+d)" % (t.strftime("%d %H:%M"), l) for t, l in mios.items()))

print("\n(2) la p SIN NIVELES contra la p que anoto el vivo:")
L = M.disparos_log(False); L["dia"] = L["ap"].dt.strftime("%Y-%m-%d"); filas = []
for dia, g in L.groupby("dia"):
    k = E[dia]["ultima"]; wk = D[D["corrida"] == k].set_index("ap").sort_index(); ciego = wk.copy()
    for c in M.CAMPOS_NIV: ciego[c] = np.nan
    pc = M.p_congelada(ciego); pz = None
    solo_zero = wk.copy()
    for c in M.CAMPOS_NIV:
        if c != "zero_vol": solo_zero[c] = np.nan
    pz = M.p_congelada(solo_zero)
    for r in g.itertuples(index=False):
        filas.append(dict(ap=r.ap, p_vivo=r.p, zero_log=r.zero, p_sin_niveles=round(pc.loc[r.ap, "p"], 3), p_solo_zero=round(pz.loc[r.ap, "p"], 3), p_con_niveles=round(M.p_congelada(wk).loc[r.ap, "p"], 3) if False else np.nan))
T = pd.DataFrame(filas).set_index("ap"); T["p_explicada"] = np.where(T["zero_log"] > 0, T["p_solo_zero"], T["p_sin_niveles"]); T["dif"] = (T["p_vivo"] - T["p_explicada"]).round(3)
print(T[["p_vivo", "zero_log", "p_sin_niveles", "p_solo_zero", "p_explicada", "dif"]].to_string())
print("    |p vivo - p sin niveles (o solo con zero cuando el registro trae zero)|: mediana %.3f, maxima %.3f (el registro redondea a 0,01)" % (T["dif"].abs().median(), T["dif"].abs().max()))

print("\n(3) que niveles anoto el centinela del VIVO (hoy / hoyrithmic) en la tarde de esos dias:")
for nombre in ("hoy", "hoyrithmic"):
    src = os.path.join(M.APP, "pythiagex-centinela-%s-MES-TimeFrame-M2.jsonl" % nombre); tot = {}; con = {}; campos = {}
    with io.open(src, encoding="utf-8", errors="replace") as fh:
        for l in fh:
            try: j = json.loads(l)
            except Exception: continue
            t = j["t"]; d = t[:10]
            if not ("18:00" <= t[11:16] < "20:00"): continue
            tot[d] = tot.get(d, 0) + 1; nv = j.get("niv") or {}
            if nv: con[d] = con.get(d, 0) + 1
            for k in nv: campos.setdefault(d, set()).add(k)
    print("   ", nombre, {d: "%d de %d con niveles %s" % (con.get(d, 0), tot[d], sorted(campos.get(d, []))[:8]) for d in sorted(tot)})
