# -*- coding: utf-8 -*-
"""RONDA 25, paso 5: A3F = A3 + detector (DESCRIPTIVO: A3 ya se eligio mirando estas muestras; PREREGISTRO_R25.md seccion 6-7).
Operaciones de A3 (sin tocar la regla):
  diseno / chequeo: el motor de 5 min de la R23 (scratchpad\\r23\\post_<m>_F1.parquet = cada punto simulado con el bracket de A3 y 200
    caminos reflejados); la seleccion de A3 se rehace aca (13:00-15:30, >= 1,5 X, IB <= 1, P >= 0,55, una a la vez, max 5) y se comprueba
    que da EXACTO las operaciones de la R23;
  cinta: las 62 operaciones orden por orden del script congelado (ronda23\\salidas\\ops-cinta-A3.csv), con su azar de 5 min;
  A3-30 en 2024-25: las 157 operaciones de la prueba de la R24 (ops_prueba_2024_25.csv), con su azar de 30 min;
  robustez :00/:30: el mismo motor de 5 min mirando solo los cierres de en punto y de y media (diseno, chequeo y cinta).
A3F-dia: no se opera el dia que el detector marca a las 13:00. A3F-dir: ese dia no se toman las entradas del lado que marca el detector.
Oraculos (NO se pueden usar en vivo; son el techo): saltear los dias que terminan en tendencia, los de TA = 1 o los de ET = 1.
Uso: python -I r25_05_a3f.py -> salidas/a3f.txt, salidas/a3f_tabla.csv (+ registro_variantes.csv)"""
import os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import r25_comun as K

DETS = {"APE": "APE_0.15", "OTF": "OTF_5", "VWA": "VWA_5", "ACE": "ACE_0", "EXT": "EXT_1", "CAL": "CAL_fomc"}
DIRECCIONAL = {"APE", "OTF", "VWA", "ACE"}
ORACULOS = {"or_TIPO": "TIPO", "or_TA": "TA", "or_ET": "ET"}
L = []


def W(s=""):
    print(s, flush=True); L.append(s)


def a3_de_sim(sim, solo30=False):
    c = sim[(sim.s != 0) & (sim.min_dia >= 780) & (sim.min_dia <= 930) & (sim.dv >= 1.5) & (sim.ext_lado.fillna(0) <= 1.0)].copy()
    if solo30: c = c[c.min_dia % 30 == 0]
    dstop = (c.stop - c.c).abs(); dobj = (c.c - c.vw).abs(); c["Pf"] = dstop / (dstop + dobj)
    c = c[c.Pf >= 0.55].sort_values(["dia", "k"])
    tom = []
    for dia, g in c.groupby("dia", sort=True):
        libre = -1; n = 0
        for i, r in g.iterrows():
            if r.k <= libre or n >= 5: continue
            tom.append(i); n += 1; libre = int(r.jsal_real)
    t = c.loc[tom]
    return pd.DataFrame(dict(dia=t.dia, k=t.k, s=t.s.astype(int), pts=t.pts_real, azar_pts=t.azar_pts, azar_gana=t.azar_gana, mot=t.mot_real))


def cargar_operaciones():
    O = {}; dias = {}
    P = {m: pd.read_parquet(os.path.join(K.SCR23, "post_%s_F1.parquet" % m)) for m in ("diseno", "chequeo", "cinta")}
    for m in ("diseno", "chequeo"):
        t = a3_de_sim(P[m]); ref = pd.read_csv(os.path.join(K.SCR23, "ops_%s_A_1300-1530_dv1.5_ible1+Pfgt0.55_F1.csv" % m))
        igual = len(t) == len(ref) and (t[["dia", "k"]].to_numpy() == ref[["dia", "k"]].to_numpy()).all() and np.allclose(t.pts.to_numpy(), ref.pts_real.to_numpy())
        W("  control: A3 rehecha en %s = %d operaciones; las de la R23 = %d; %s" % (m, len(t), len(ref), "IDENTICAS" if igual else "DISTINTAS"))
        if not igual: raise SystemExit("A3 no se reproduce: no se sigue")
        O[m] = t; dias[m] = sorted(P[m].dia.unique())
        O[m + "_30"] = a3_de_sim(P[m], solo30=True); dias[m + "_30"] = dias[m]
    c = pd.read_csv(os.path.join(K.R23, "salidas", "ops-cinta-A3.csv"), dtype={"fecha": str})
    O["cinta"] = pd.DataFrame(dict(dia=c.fecha, k=c.k, s=np.where(c.lado == "VENTA", 1, -1), pts=c.pts, azar_pts=c.azar_pts, azar_gana=c.azar_gana,
                                   mot=c.motivo))
    dias["cinta"] = sorted(P["cinta"].dia.unique())
    O["cinta_30"] = a3_de_sim(P["cinta"], solo30=True); dias["cinta_30"] = dias["cinta"]
    r = pd.read_csv(os.path.join(K.R24, "ops_prueba_2024_25.csv"), dtype={"fecha": str})
    O["a3_30_2425"] = pd.DataFrame(dict(dia=r.fecha, k=r.k, s=-r.lado, pts=r.pts, azar_pts=r.az_pts, azar_gana=r.az_gana, mot=r.motivo))
    R = pd.read_csv(os.path.join(K.SAL, "rasgos_diseno.csv"), dtype={"dia": str})
    dias["a3_30_2425"] = sorted(R.dia.unique())
    W("  operaciones: " + ", ".join("%s %d (%d ruedas)" % (k, len(v), len(dias[k])) for k, v in O.items()))
    return O, dias


def marcas():
    """por dia: marca (0/1) de cada detector con su parametro de teoria, s13 y los resultados para los oraculos."""
    fr = [pd.read_csv(os.path.join(K.SAL, "rasgos_%s.csv" % n), dtype={"dia": str}) for n in ("diseno", "val1", "explora", "val2")]
    F = pd.concat(fr, ignore_index=True).drop_duplicates("dia").set_index("dia")
    return F


def resumen(t, dias):
    nd = len(dias); n = len(t)
    if n == 0: return dict(n=0, por_rueda=0.0, acierto=np.nan, azar=np.nan, neto=np.nan, neto_rueda=0.0, t_rueda=0.0, ventaja=np.nan, t_ventaja=np.nan)
    net = t.pts - K.COSTO
    por = net.groupby(t.dia).sum(); serie = np.array([por.get(d, 0.0) for d in dias])
    ed = (t.pts - t.azar_pts).to_numpy()
    return dict(n=n, por_rueda=n / nd, acierto=100 * (t.pts > 0).mean(), azar=100 * t.azar_gana.mean(), neto=net.mean(), neto_rueda=serie.mean(),
                t_rueda=K.t_rueda(serie), ventaja=ed.mean(), t_ventaja=K.t_agrupado(ed, t.dia.to_numpy()))


def filtrar(t, F, det, modo):
    """devuelve las operaciones que quedan con el filtro."""
    if det.startswith("or_"):
        col = ORACULOS[det]
        flag = t.dia.map(lambda d: int(F.loc[d, col]) if d in F.index else 0)
        return t[flag.to_numpy() == 0]
    col = DETS[det]
    flag = t.dia.map(lambda d: int(F.loc[d, col]) if d in F.index else 0).to_numpy()
    if modo == "dia" or det not in DIRECCIONAL:
        return t[flag == 0]
    s13 = t.dia.map(lambda d: int(F.loc[d, "s13"]) if d in F.index else 0).to_numpy()
    return t[~((flag == 1) & (t.s.to_numpy() == s13))]


def placebo(t, dias, n_skip, B=2000, semilla=2505):
    """neto por operacion al saltear n_skip ruedas al azar (B sorteos)."""
    rng = np.random.default_rng(semilla); out = np.empty(B)
    dd = np.array(dias); net = (t.pts - K.COSTO).to_numpy(); tdia = t.dia.to_numpy()
    for b in range(B):
        sk = set(rng.choice(dd, n_skip, replace=False)) if n_skip else set()
        keep = np.array([d not in sk for d in tdia])
        out[b] = net[keep].mean() if keep.any() else np.nan
    return out


def mitad(m, d):
    corte = {"diseno": "2025-11-30", "chequeo": "2026-05-31", "cinta": "2026-10-15", "a3_30_2425": "2024-12-31"}[m.replace("_30", "") if m != "a3_30_2425" else m]
    return "S1" if d <= corte else "S2"


def main():
    t0 = time.time(); K.frenar_si_falta_ram(2.0)
    W("RONDA 25 - A3F (DESCRIPTIVO) %s" % time.strftime("%Y-%m-%d %H:%M"))
    O, dias = cargar_operaciones(); F = marcas()
    nomb = {"diseno": "diseno A3 (MES 5m)", "chequeo": "chequeo A3 (MES 5m)", "cinta": "cinta ES (orden x orden)", "a3_30_2425": "A3-30 MES 2024-25",
            "diseno_30": "diseno solo :00/:30", "chequeo_30": "chequeo solo :00/:30", "cinta_30": "cinta solo :00/:30 (5m)"}
    filas = []
    base = {m: resumen(O[m], dias[m]) for m in O}
    W(""); W("A3 SIN FILTRO (referencia):")
    for m in O:
        b = base[m]
        W("  %-26s n %3d (%.2f/r) | acierto %5.1f %% (azar %5.1f) | neto %+5.2f/op, %+5.2f/rueda (t %5.2f) | ventaja %+5.2f (t %5.2f)" % (
            nomb[m], b["n"], b["por_rueda"], b["acierto"], b["azar"], b["neto"], b["neto_rueda"], b["t_rueda"], b["ventaja"], b["t_ventaja"]))
    for det in list(DETS) + list(ORACULOS):
        for modo in (("dia", "dir") if det in DIRECCIONAL else ("dia",)):
            W(""); W("== %s-%s %s" % ("A3F" if not det.startswith("or_") else "ORACULO", modo, det if not det.startswith("or_") else "(saltear los dias con %s = 1, ex post)" % ORACULOS[det]))
            for m in O:
                t = filtrar(O[m], F, det, modo); r = resumen(t, dias[m]); b = base[m]
                # placebo: saltear al azar la misma cantidad de ruedas
                col = DETS.get(det) or ORACULOS[det]
                n_marca = int(sum(int(F.loc[d, col]) for d in dias[m] if d in F.index))
                pp = placebo(O[m], dias[m], n_marca) if modo == "dia" else None
                p_pl = float(np.mean(pp >= r["neto"] - 1e-12)) if pp is not None and np.isfinite(r["neto"]) else np.nan
                sub = {}
                for s_ in ("S1", "S2"):
                    dd = [d for d in dias[m] if mitad(m, d) == s_]
                    sub[s_] = (resumen(t[t.dia.isin(dd)], dd)["neto"], resumen(O[m][O[m].dia.isin(dd)], dd)["neto"])
                W("  %-26s marca %3d r | n %3d (%.2f/r) | acierto %5.1f %% (azar %5.1f) | neto %+5.2f/op (A3 %+5.2f), %+5.2f/rueda (A3 %+5.2f) | ventaja %+5.2f (t %5.2f) | placebo p %s | mitades %+5.2f/%+5.2f (A3 %+5.2f/%+5.2f)" % (
                    nomb[m], n_marca, r["n"], r["por_rueda"], r["acierto"], r["azar"], r["neto"], b["neto"], r["neto_rueda"], b["neto_rueda"], r["ventaja"], r["t_ventaja"],
                    ("%.3f" % p_pl) if np.isfinite(p_pl) else "  -  ", sub["S1"][0], sub["S2"][0], sub["S1"][1], sub["S2"][1]))
                filas.append(dict(detector=det, modo=modo, muestra=m, n_marca=n_marca, **{k: r[k] for k in r}, **{"A3_" + k: b[k] for k in ("n", "acierto", "azar", "neto", "neto_rueda", "ventaja")},
                                  p_placebo=p_pl, S1=sub["S1"][0], S2=sub["S2"][0], S1_A3=sub["S1"][1], S2_A3=sub["S2"][1]))
    T = pd.DataFrame(filas); T.to_csv(os.path.join(K.SAL, "a3f_tabla.csv"), index=False)
    # criterios F2-F5 (informativos: F1 ya fallo en el diseno)
    W(""); W("CRITERIOS DE CONGELAMIENTO (seccion 7; F1 fallo: ningun detector paso el diseno) - se informan igual:")
    for det in DETS:
        for modo in (("dia", "dir") if det in DIRECCIONAL else ("dia",)):
            q = T[(T.detector == det) & (T.modo == modo)].set_index("muestra")
            tres = ["diseno", "chequeo", "cinta"]
            f2 = all(q.loc[m, "acierto"] >= 60 and q.loc[m, "neto"] > 0 and q.loc[m, "por_rueda"] <= 5 and q.loc[m, "ventaja"] > 0 for m in tres)
            mejora = sum(q.loc[m, "neto"] > q.loc[m, "A3_neto"] for m in tres)
            nr = sum(q.loc[m, "neto_rueda"] * len(dias[m]) for m in tres) / sum(len(dias[m]) for m in tres)
            nr_a3 = sum(q.loc[m, "A3_neto_rueda"] * len(dias[m]) for m in tres) / sum(len(dias[m]) for m in tres)
            f3 = mejora >= 2 and nr >= nr_a3
            f5a = q.loc["diseno_30", "neto"] > q.loc["diseno_30", "A3_neto"] and q.loc["chequeo_30", "neto"] > q.loc["chequeo_30", "A3_neto"]
            mit = sum((q.loc[m, "S1"] >= q.loc[m, "S1_A3"]) + (q.loc[m, "S2"] >= q.loc[m, "S2_A3"]) for m in tres)
            f5 = f5a and mit >= 4
            W("  %s-%s: F2 %s | F3 %s (mejora el neto/op en %d de 3; neto por rueda juntas %+.2f contra %+.2f) | F5 %s (:00/:30 %s; mitades %d de 6)" % (
                det, modo, "si" if f2 else "NO", "si" if f3 else "NO", mejora, nr, nr_a3, "si" if f5 else "NO", "mejora" if f5a else "no mejora", mit))
            K.anotar(dict(etapa="a3f_descriptivo", detector=det, variante="A3F-%s" % modo, parametros=DETS[det], muestra="diseno+chequeo+cinta",
                          n_dias=sum(len(dias[m]) for m in tres), pasa=int(f2 and f3 and f5),
                          nota="F2 %s F3 %s F5 %s; neto/op diseno %+.2f chequeo %+.2f cinta %+.2f (A3 %+.2f %+.2f %+.2f)" % (
                              f2, f3, f5, q.loc["diseno", "neto"], q.loc["chequeo", "neto"], q.loc["cinta", "neto"], q.loc["diseno", "A3_neto"],
                              q.loc["chequeo", "A3_neto"], q.loc["cinta", "A3_neto"])))
    open(os.path.join(K.SAL, "a3f.txt"), "w", encoding="utf-8").write("\n".join(L))
    W("%.1f s" % (time.time() - t0))


if __name__ == "__main__":
    main()
