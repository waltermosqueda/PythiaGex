# -*- coding: utf-8 -*-
"""
rev_vivo_01_paridad.py — REVISION DEL DETECTOR EN VIVO (FlujoClaroVerdes.cs) contra el banco (verdes_20_reclamo.gatillos).

Que hace: un PORTE FIEL de la maquina de estados de VerdesOperacion / VerdesLatido (C#) a Python, alimentado con las mismas rayas del banco
(verdes_lib.rayas_por_segundo) y con la cinta de esos mismos 7 dias. Las "operaciones" de cada segundo se arman con: la apertura REAL del segundo
(primera orden de la cinta), los dos extremos y el ultimo. Asi se mide:
  1. cuantos gatillos del banco reproduce la logica del vivo (paridad) y cuanto rinde cada grupo;
  2. cuantas visitas del vivo arrancan SIN haber entrado desde el lado bueno (la banda del vivo es (L-10, L+1,5]: entrar desde abajo tambien la activa);
  3. que aporta la frescura de 10 min del vivo contra los 5 min del banco, y el vivo fuera de la rueda (el banco SOLO midio 13:30-20:00 UTC);
  4. cuantas entradas del banco nacen con la raya RECIEN aparecida al lado del precio (el vivo no las puede tener: borra la memoria al borrar la raya).
Solo LEE. No toca el banco ni los .cs.
"""
import os, sys, time
import numpy as np, pandas as pd
AQUI = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, AQUI)
import base as B, verdes_lib as V
import verdes_20_reclamo as R20

TOL, LEJOS, VISITA, PEN_MAX, REC = 1.5, 10.0, 90, 10.0, 1.5
PLC = (12.5, -12.5, 7.0, -7.0)
REVC = os.path.join(AQUI, "rev_cache"); os.makedirs(REVC, exist_ok=True)


def aperturas(dias):
    """Por segundo: precio de la PRIMERA orden (abre) y su volumen firmado (v0). Sale de la cinta orden por orden; se cachea chico."""
    falta = [d for d in dias if not os.path.exists(os.path.join(REVC, "vivo_abre_%s.parquet" % d))]
    if falta:
        import pyarrow.parquet as pq
        t = pq.read_table(os.path.join(B.CACHE, "cinta.parquet"), columns=["t", "primero", "vol", "lado", "sesion"], filters=[("sesion", "in", falta)]).to_pandas()
        for d, g in t.groupby("sesion"):
            s = g["t"].dt.floor("s"); o = pd.DataFrame({"abre": g.groupby(s)["primero"].first(), "v0": (g["vol"] * g["lado"]).groupby(s).first()})
            o.to_parquet(os.path.join(REVC, "vivo_abre_%s.parquet" % d))
        del t
    return {d: pd.read_parquet(os.path.join(REVC, "vivo_abre_%s.parquet" % d)) for d in dias}


class Linea:
    __slots__ = ("nivel", "la", "lb", "act")
    def __init__(self): self.nivel = 0.0; self.la = -10**9; self.lb = -10**9; self.act = None


def simular(seg, ab, L2, fotos_idx, corr=0.0, con_delta=True):
    """Porte de VerdesLatido + VerdesOperacion. L2 = rayas por segundo con la vida del VIVO (NaN = estela vieja). fotos_idx = segundos en que llega una foto nueva.
    Devuelve (gatillos, visitas): gatillos = dicts con i, lado, ent, stop, L, pen, dz, rth, origen; visitas = dicts con origen ('arriba'/'zona'/'contrario')."""
    alto = seg["alto"].to_numpy(); bajo = seg["bajo"].to_numpy(); ult = seg["ultimo"].to_numpy(); nord = seg["n"].to_numpy(); dl = seg["delta"].to_numpy("float64"); rueda = seg["rueda"].to_numpy()
    abre = ab["abre"].reindex(seg.index).to_numpy(); v0 = ab["v0"].reindex(seg.index).fillna(0).to_numpy()
    d0 = L2["d0"].to_numpy() + corr; d1 = L2["d1"].to_numpy() + corr; n = len(ult)
    cs = np.concatenate(([0.0], np.cumsum(dl)))
    lineas = {}; gat = []; vis = []; pf = 0; prev = 0.0; esf = set(fotos_idx.tolist())
    for i in np.flatnonzero(nord > 0):
        # VerdesLatido: cada foto nueva pisa niveles, agrega y BORRA las que dejaron de ser dominantes sin visita en curso (con su memoria de 'lejos')
        while pf < len(fotos_idx) and fotos_idx[pf] <= i:
            j = fotos_idx[pf]; pf += 1; vivos = set()
            for x in (d0[j], d1[j]):
                if not np.isfinite(x) or x <= 0: continue
                k = round(x / 5.0) * 5.0; vivos.add(k)
                if k not in lineas: lineas[k] = Linea()
                lineas[k].nivel = x
            for k in [k for k in lineas if k not in vivos and lineas[k].act is None]: del lineas[k]
        o = abre[i] if np.isfinite(abre[i]) else ult[i]
        ext = (alto[i], bajo[i]) if abs(alto[i] - o) <= abs(o - bajo[i]) else (bajo[i], alto[i])
        seq = [o]
        for x in ext + (ult[i],):
            if x != seq[-1]: seq.append(x)
        pcs = [prev if prev > 0 else o] + seq[:-1]; prev = seq[-1]
        if np.isnan(d0[i]): continue                       # estela de mas de 10 min: el vivo no mira las rayas (y no actualiza 'lejos')
        for k, ln in list(lineas.items()):
            L = ln.nivel
            if ln.act is None:                                            # atajo: todo el segundo lejos de la banda -> solo la memoria de 'lejos'
                if bajo[i] >= L + LEJOS: ln.la = i; continue
                if alto[i] <= L - LEJOS: ln.lb = i; continue
            for q, p in enumerate(seq):
                pc = pcs[q]
                if p >= L + LEJOS: ln.la = i
                elif p <= L - LEJOS: ln.lb = i
                v = ln.act
                if v is None:
                    lado = 0
                    if p <= L + TOL and p > L - PEN_MAX and i - ln.la <= 1800: lado = 1
                    elif p >= L - TOL and p < L + PEN_MAX and i - ln.lb <= 1800: lado = -1
                    if lado == 0: continue
                    if lado > 0: ln.la = -10**9
                    else: ln.lb = -10**9
                    d = (pc - L) * lado                                   # de donde venia la operacion ANTERIOR, medido del lado bueno
                    origen = "arriba" if d > TOL else ("contrario" if d <= -PEN_MAX else ("zona_detras" if d < -TOL else "zona"))
                    ln.act = v = dict(lado=lado, t0=i, ext=p, L=L, origen=origen); vis.append(dict(i=i, lado=lado, L=L, origen=origen, rth=bool(rueda[i])))
                    continue
                v["ext"] = min(v["ext"], p) if v["lado"] > 0 else max(v["ext"], p)
                pen = (v["L"] - v["ext"]) * v["lado"]
                if pen > PEN_MAX: ln.act = None; continue
                if i - v["t0"] > VISITA: ln.act = None; continue
                g = q == 0 and pen >= 0.5 and (pc - v["L"]) * v["lado"] >= REC
                if g and con_delta:
                    d10 = cs[i] - cs[max(0, i - 9)] + v0[i]               # Ventana(10) en la primera operacion del segundo: los 9 s anteriores + esa operacion
                    g = d10 * v["lado"] > 0
                if not g: continue
                gat.append(dict(i=int(i), lado=v["lado"], ent=float(p), stop=float(v["ext"] - v["lado"] * 1.0), L=float(v["L"]), pen=float(max(0.0, pen)), dz=int(i - v["t0"]),
                                rth=bool(rueda[v["t0"]]), origen=v["origen"], pc=float(pc)))
                ln.act = None
    return gat, vis


def puntos(seg, g, objetivo=20):
    """Mismo juez que el banco (verdes_20_reclamo.resultado) con la entrada y el stop del gatillo."""
    tup = [(x["i"], x["lado"], x["ent"], x["stop"], x["L"], x["pen"], x["dz"]) for x in g]
    return [r[0] for r in R20.resultado(seg, tup, objetivo)]


def puntos_vivo(seg, g, objetivo=20.0):
    """El juez del banco, pero el vivo entra en la PRIMERA operacion del segundo i: el resto de ese segundo tambien cuenta (si toca el stop ahi, pierde)."""
    alto = seg["alto"].to_numpy(); bajo = seg["bajo"].to_numpy(); ult = seg["ultimo"].to_numpy(); n = len(ult); res = []
    for x in g:
        i, lado, ent, stop = x["i"], x["lado"], x["ent"], x["stop"]; riesgo = (ent - stop) * lado; j1 = min(n, i + 901); h = alto[i:j1]; b = bajo[i:j1]
        if lado > 0: tg = np.flatnonzero(h >= ent + objetivo); tp = np.flatnonzero(b <= stop)
        else: tg = np.flatnonzero(b <= ent - objetivo); tp = np.flatnonzero(h >= stop)
        a = tg[0] if len(tg) else 10**9; c = tp[0] if len(tp) else 10**9
        pts = objetivo if a < c else (-riesgo if c < 10**9 else (ult[j1 - 1] - ent) * lado)
        res.append(pts - B.COSTO_PTS)
    return res


def linea(nombre, p):
    p = np.asarray(p, float)
    if not len(p): return "   %-46s sin gatillos" % nombre
    es = p.std(ddof=1) / np.sqrt(len(p)) if len(p) > 1 else float("nan")
    return "   %-46s n %4d | gana %4.1f %% | neto %+5.2f pts/op (total %+7.1f) | t %+4.1f" % (nombre, len(p), 100 * (p + B.COSTO_PTS > 1e-9).mean(), p.mean(), p.sum(), p.mean() / es if es and es > 0 else float("nan"))


if __name__ == "__main__":
    t_ini = time.time()
    F, _ = V.fotos("NQ")
    dias = []
    for f in sorted(os.listdir(B.CACHE)):
        if f.startswith("seg-") and f.endswith(".parquet"): dias.append(f[4:-8])
    T = {}
    for s in dias:
        seg = pd.read_parquet(os.path.join(B.CACHE, "seg-%s.parquet" % s)); L2 = V.rayas_por_segundo(seg, F)
        if L2 is not None and L2["d0"].notna().sum() >= 600: T[s] = seg
    dias = sorted(T); print("dias con verdes:", " ".join(d[5:] for d in dias)); AB = aperturas(dias)

    banco = {}; banco_nace = []; vivo600 = {}; vivo300 = {}; plc_vivo = []; plc_banco = []; vis600 = []
    for s in dias:
        seg = T[s]; L2b = V.rayas_por_segundo(seg, F); L6 = V.rayas_por_segundo(seg, F, vida_s=600)
        f = F[(F["t"] >= seg.index[0]) & (F["t"] <= seg.index[-1])]; tf = (f["t"].dt.floor("s") + pd.Timedelta(seconds=1)).drop_duplicates()
        fidx = seg.index.get_indexer(tf); fidx = np.unique(fidx[fidx >= 0])
        # --- el banco, tal cual (cruce + delta) y cuantas entradas nacen con la raya recien aparecida
        gb = []
        for col in ("d0", "d1"):
            Lv = L2b[col].to_numpy(); K = np.round(Lv / 5.0) * 5.0
            for k in np.unique(K[~np.isnan(K)]):
                Lt = np.where(K == k, Lv, np.nan)
                for (i, lado, ent, stop, L, pen, dz) in R20.gatillos(seg, Lt, "cruce", True):
                    t0 = i - dz; gb.append(dict(i=i, lado=lado, ent=ent, stop=stop, L=L, pen=pen, dz=dz, nace=bool(t0 > 0 and np.isnan(Lt[t0 - 1]))))
                for dlt in PLC: plc_banco += [r[0] for r in R20.resultado(seg, R20.gatillos(seg, Lt + dlt, "cruce", True), 20)]
        for x, p in zip(gb, puntos(seg, gb)): x["pts"] = p
        banco[s] = gb
        # --- el vivo (porte), con su frescura de 10 min y con la del banco (5 min)
        g6, v6 = simular(seg, AB[s], L6, fidx); g3, _ = simular(seg, AB[s], L2b, fidx)
        for x, p in zip(g6, puntos_vivo(seg, g6)): x["pts"] = p
        for x, p in zip(g3, puntos_vivo(seg, g3)): x["pts"] = p
        vivo600[s] = g6; vivo300[s] = g3; vis600 += [dict(v, dia=s) for v in v6]
        for dlt in PLC:
            gp, _ = simular(seg, AB[s], L6, fidx, corr=dlt); pp = puntos_vivo(seg, gp)
            plc_vivo += [dict(rth=x["rth"], pts=p) for x, p in zip(gp, pp)]
        print("  %s listo (%.0f s)" % (s, time.time() - t_ini), flush=True)

    todo_b = [x for s in dias for x in banco[s]]; todo_v = [x for s in dias for x in vivo600[s]]; todo_v3 = [x for s in dias for x in vivo300[s]]
    print("\n=== 1. EL BANCO (cruce + delta, objetivo 20), rehecho con este arnes ===")
    print(linea("banco VERDES (debe dar n 158, +1,40)", [x["pts"] for x in todo_b])); print(linea("banco PLACEBO", plc_banco))
    print(linea("  de esas, la raya NACIO al lado del precio", [x["pts"] for x in todo_b if x["nace"]])); print(linea("  de esas, la raya ya estaba", [x["pts"] for x in todo_b if not x["nace"]]))
    print("\n=== 2. LA LOGICA DEL VIVO (porte de FlujoClaroVerdes.cs) sobre los mismos dias ===")
    print(linea("vivo, entrada a la zona EN RUEDA (comparable)", [x["pts"] for x in todo_v if x["rth"]])); print(linea("vivo PLACEBO en rueda", [x["pts"] for x in plc_vivo if x["rth"]]))
    print(linea("vivo FUERA de rueda (el banco no lo midio)", [x["pts"] for x in todo_v if not x["rth"]])); print(linea("vivo PLACEBO fuera de rueda", [x["pts"] for x in plc_vivo if not x["rth"]]))
    print(linea("vivo en rueda con frescura de 5 min (la del banco)", [x["pts"] for x in todo_v3 if x["rth"]]))
    print("   por dia (vivo en rueda): " + " | ".join("%s %+.1f (%d)" % (s[5:], sum(x["pts"] for x in vivo600[s] if x["rth"]), sum(1 for x in vivo600[s] if x["rth"])) for s in dias))
    print("   por dia (banco):         " + " | ".join("%s %+.1f (%d)" % (s[5:], sum(x["pts"] for x in banco[s]), len(banco[s])) for s in dias))
    # --- paridad gatillo a gatillo
    com = 0; solo_b = []; usados = set()
    for s in dias:
        for x in banco[s]:
            m = [j for j, y in enumerate(vivo600[s]) if y["lado"] == x["lado"] and abs(y["L"] - x["L"]) < 2.6 and abs(y["i"] - x["i"]) <= 5 and (s, j) not in usados]
            if m: com += 1; usados.add((s, m[0]))
            else: solo_b.append(x)
    solo_v = [y for s in dias for j, y in enumerate(vivo600[s]) if y["rth"] and (s, j) not in usados]
    print("\n=== 3. PARIDAD gatillo a gatillo (mismo lado, misma raya, <= 5 s) ===")
    print("   banco %d | vivo en rueda %d | COMUNES %d (%.0f %% del banco) | solo banco %d | solo vivo %d" % (len(todo_b), sum(1 for x in todo_v if x["rth"]), com, 100.0 * com / max(1, len(todo_b)), len(solo_b), len(solo_v)))
    print(linea("solo en el banco", [x["pts"] for x in solo_b])); print(linea("   (de esos, raya nacida al lado)", [x["pts"] for x in solo_b if x["nace"]])); print(linea("solo en el vivo", [x["pts"] for x in solo_v]))
    dif = [y["ent"] - y["pc"] for y in todo_v]; adv = [(y["ent"] - y["pc"]) * y["lado"] for y in todo_v]
    print("   entrada del vivo (1a operacion del segundo siguiente) contra el cierre que decidio: media en contra %+.2f pts | p90 %.2f | max %.2f" % (np.mean(adv), np.percentile(adv, 90), np.max(adv)))
    # --- de donde arrancan las visitas del vivo
    vv = pd.DataFrame(vis600); print("\n=== 4. DE DONDE VENIA EL PRECIO al abrir cada visita del vivo (operacion anterior) ===")
    print(vv.groupby(["rth", "origen"]).size().to_string())
    gg = pd.DataFrame(todo_v); print("\n   gatillos del vivo por origen de la visita:")
    for (rth, o), g in gg.groupby(["rth", "origen"]): print(linea("rueda=%s origen=%s" % (rth, o), g["pts"].tolist()))
    print("\n(%.0f s)" % (time.time() - t_ini))
