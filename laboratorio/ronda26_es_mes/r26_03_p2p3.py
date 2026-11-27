# -*- coding: utf-8 -*-
"""RONDA 26, paso 3: P2 (LA INFORMACION) y P3 (COMBINACIONES) (PREREGISTRO secciones 6 a 8).

Minutos: cierres de minuto RTH con pierna en curso en ES y en MES y la MISMA direccion (misma X, la del ES). Desenlace en el zigzag del ES:
SIGUE = la pierna hace un extremo >= Xs/2 mas alla del extremo actual antes de que se confirme el giro (retroceso de Xs desde el extremo);
GIRA = se confirma antes. Pierna sin terminar a las 17:00 NY que no llego a Xs/2: censurada (afuera). Xs = X (ola) o 4X (marea).
Caja = retroceso ya hecho r/Xs al cierre: [0;0,25) [0,25;0,5) [0,5;0,75) [0,75;...).

P2: clase del minuto = (ES con/sin, MES con/sin). D(clase) = promedio, ponderado por los minutos de la clase en cada caja, de
    (tasa de SIGUE de la clase en la caja - tasa de TODOS los minutos elegibles de esa caja) = media de (sigue - base de su caja) en la clase.
    Robustez: la misma cuenta con la base por caja Y rueda.
P3: rasgo binario al cierre del minuto. S(rasgo) = diferencia de riesgo de Mantel-Haenszel (tasa de SIGUE con el rasgo - sin el rasgo, dentro
    de cada caja, pesos n1 n0 / (n1 + n0)). R0 = el color del ES (la referencia); V1..V6 fijados en el pre-registro. Se informa |S| - |S(R0)|.
Incertidumbre: bootstrap por PIERNAS del ES (bloque = la pierna a la que pertenece el minuto) y por RUEDAS, B = 2000 cada uno;
t = estimacion / max(EE piernas, EE ruedas) si hay >= 7 ruedas (si no, EE piernas). Placebo: 2000 barajadas de la clase (o del rasgo)
DENTRO de la misma caja y rueda; p de dos colas. Bonferroni: 22 pruebas (P2 4 clases x 2 escalas + P3 7 rasgos x 2 escalas), |t| >= z de
0,05/22 de dos colas (3,05) y p del placebo < 0,05/22. Una prueba con menos de 20 piernas (en la clase, o en cada lado del rasgo) no se juzga.
Uso: python -I r26_03_p2p3.py <tanda> [--etiquetar]   (--etiquetar: con la tanda 1, pone la etiqueta pre-registrada usando la tanda 0)"""
import json
import os
import sys
import warnings
from statistics import NormalDist

warnings.filterwarnings("ignore", category=RuntimeWarning)   # medias de cortes vacios (clases sin minutos en una replica): dan NaN y se informan

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import pandas as pd
import r26_comun as C

C.prioridad_baja()
B = 2000
NPERM = 2000
PRUEBAS = 22
ALFA = 0.05 / PRUEBAS
Z_BONF = NormalDist().inv_cdf(1 - ALFA / 2)
MIN_PIERNAS = 20
CLASES = ["CC", "SC", "CS", "SS"]
NOMBRE = {"CC": "ES con / MES con", "SC": "ES sin / MES con", "CS": "ES con / MES sin", "SS": "ES sin / MES sin"}
RASGOS = ["R0", "V1_comb", "V2_medianos", "V3_chicos", "V4_mes", "V5_brecha", "V6_patron"]
DESC = {"R0": "color del ES (referencia)", "V1_comb": "delta ES + MES/10 a favor", "V2_medianos": "ES 10-49 contratos a favor",
        "V3_chicos": "ES 1-9 contratos a favor", "V4_mes": "color del MES", "V5_brecha": "MES/10 mas a favor que el ES",
        "V6_patron": "ES chicos a favor y medianos en contra"}


def dias_de(tanda):
    p = json.load(open(os.path.join(C.DATOS, "pares.json")))
    return sorted(d for d, v in p.items() if v["tanda"] == tanda and v["alinea"]), p


def d_clase(y, w, estr, K, mascaras):
    ns = np.bincount(estr, weights=w, minlength=K)
    ys = np.bincount(estr, weights=w * y, minlength=K)
    base = ys / np.where(ns > 0, ns, 1.0)
    res = y - base[estr]
    out = []
    for m in mascaras:
        wc = w * m
        s = wc.sum()
        out.append((wc * res).sum() / s if s > 0 else np.nan)
    return np.array(out)


def mh_rd(y, w, estr, K, rasgos):
    out = []
    for f in rasgos:
        n1 = np.bincount(estr, weights=w * f, minlength=K)
        n0 = np.bincount(estr, weights=w * (1 - f), minlength=K)
        y1 = np.bincount(estr, weights=w * f * y, minlength=K)
        y0 = np.bincount(estr, weights=w * (1 - f) * y, minlength=K)
        ok = (n1 > 0) & (n0 > 0)
        if not ok.any():
            out.append(np.nan)
            continue
        ws = n1[ok] * n0[ok] / (n1[ok] + n0[ok])
        out.append(float(np.sum(ws * (y1[ok] / n1[ok] - y0[ok] / n0[ok])) / ws.sum()))
    return np.array(out)


def estimar(D, rng):
    """D = minutos elegibles y resueltos de una escala. Devuelve los resultados de P2 y P3 con bootstrap y placebo."""
    y = D["e_sigue"].to_numpy().astype(float)
    caja = D["caja"].to_numpy().astype(np.int64)
    dias = D["dia"].to_numpy()
    ud, di = np.unique(dias, return_inverse=True)
    up, pi = np.unique(D["pierna"].to_numpy(), return_inverse=True)
    est_caja, K1 = caja, 4
    est_dc = di * 4 + caja
    K2 = len(ud) * 4
    mc = [(D["clase"] == c).to_numpy().astype(float) for c in CLASES]
    fr = [D[r].to_numpy().astype(float) for r in RASGOS]
    w1 = np.ones(len(D))
    obs = {"D": d_clase(y, w1, est_caja, K1, mc), "Ddc": d_clase(y, w1, est_dc, K2, mc),
           "S": mh_rd(y, w1, est_caja, K1, fr), "Sdc": mh_rd(y, w1, est_dc, K2, fr)}
    bo = {"leg": {"D": [], "S": []}, "dia": {"D": [], "S": []}}
    for tipo, idx, n in (("leg", pi, len(up)), ("dia", di, len(ud))):
        for _ in range(B):
            cnt = np.bincount(rng.integers(0, n, n), minlength=n).astype(float)
            w = cnt[idx]
            bo[tipo]["D"].append(d_clase(y, w, est_caja, K1, mc))
            bo[tipo]["S"].append(mh_rd(y, w, est_caja, K1, fr))
        bo[tipo]["D"] = np.array(bo[tipo]["D"])
        bo[tipo]["S"] = np.array(bo[tipo]["S"])
    # placebo: barajar las etiquetas (clase y rasgos juntos, la misma permutacion) dentro de caja x rueda
    orden_g = np.argsort(est_dc, kind="stable")
    pl = {"D": [], "S": []}
    cl_arr = np.vstack(mc)
    fr_arr = np.vstack(fr)
    for _ in range(NPERM):
        orden = np.lexsort((rng.random(len(D)), est_dc))
        perm = np.empty(len(D), np.int64)
        perm[orden] = orden_g
        pl["D"].append(d_clase(y, w1, est_caja, K1, list(cl_arr[:, perm])))
        pl["S"].append(mh_rd(y, w1, est_caja, K1, list(fr_arr[:, perm])))
    pl = {k: np.array(v) for k, v in pl.items()}
    return obs, bo, pl, len(ud)


def resumen_prueba(est, b_leg, b_dia, plac, ndias):
    se_l = float(np.nanstd(b_leg, ddof=1))
    se_d = float(np.nanstd(b_dia, ddof=1))
    se = max(se_l, se_d) if ndias >= 7 else se_l
    t = est / se if se > 0 else np.nan
    lo, hi = np.nanpercentile(b_leg, [2.5, 97.5])
    p = (1 + np.sum(np.abs(plac) >= abs(est) - 1e-15)) / (1 + len(plac))
    return {"est": float(est), "se_pierna": se_l, "se_rueda": se_d, "t": float(t), "ic_lo": float(lo), "ic_hi": float(hi),
            "p_placebo": float(p), "placebo_media": float(np.nanmean(plac)), "placebo_sd": float(np.nanstd(plac))}


def etiqueta(r1, r0):
    """Regla pre-registrada (seccion 8): r1 = tanda 1, r0 = tanda 0 (mismo test)."""
    if r1 is None or r1.get("sin_muestra"):
        return "SIN MUESTRA"
    mismo = r0 is not None and not r0.get("sin_muestra") and np.sign(r0["est"]) == np.sign(r1["est"])
    if abs(r1["t"]) >= Z_BONF and r1["p_placebo"] < ALFA and mismo:
        return "PASA"
    if abs(r1["t"]) >= 2.0 and mismo:
        return "PISTA (no aprueba)"
    cero1 = r1["ic_lo"] <= 0 <= r1["ic_hi"]
    cero0 = r0 is not None and not r0.get("sin_muestra") and r0["ic_lo"] <= 0 <= r0["ic_hi"]
    if cero1 and cero0 and ((abs(r1["est"]) < 0.02 and abs(r0["est"]) < 0.02) or not mismo):
        return "SIN INFORMACION en esta forma (2 muestras)"
    return "INCONCLUSO (no aprueba)"


def main():
    tanda = sys.argv[1]
    etiquetar = "--etiquetar" in sys.argv
    dias, pares = dias_de(tanda)
    rng = np.random.default_rng(C.SEMILLA)
    print("RONDA 26 - P2 y P3, %s: %d ruedas %s" % (tanda, len(dias), " ".join(dias)))
    print("Bonferroni: %d pruebas, alfa %.5f por prueba, |t| >= %.2f; minimo %d piernas" % (PRUEBAS, ALFA, Z_BONF, MIN_PIERNAS))
    salida = {"tanda": tanda, "dias": dias, "pruebas": {}}
    previa = None
    if etiquetar:
        f0 = os.path.join(C.DATOS, "r26_03-tanda0.json")
        previa = json.load(open(f0))["pruebas"] if os.path.exists(f0) else None
    for esc in ("X", "4X"):
        nombre = "OLA (X)" if esc == "X" else "MAREA (4X)"
        M = pd.concat([C.armar_minutos(d, esc, pares[d]["X"]) for d in dias], ignore_index=True)
        el = M[M["elegible"]]
        D = el[el["e_resuelto"]].reset_index(drop=True)
        print("\n" + "=" * 110 + "\n%s: minutos elegibles %d, resueltos %d (censurados %d), piernas %d, ruedas %d" %
              (nombre, len(el), len(D), len(el) - len(D), D["pierna"].nunique(), D["dia"].nunique()) + "\n" + "=" * 110)
        obs, bo, pl, nd = estimar(D, rng)
        # tabla por caja
        print("P2. Tasa de SIGUE por caja de retroceso (r/Xs) y clase  [n minutos / piernas]")
        print("   caja        base (todos)        formula paseo   " + "".join("%-24s" % NOMBRE[c] for c in CLASES))
        for k in range(4):
            g = D[D["caja"] == k]
            fila = "   %-9s  %5.1f %% [%5d/%4d]   %5.1f %%        " % (C.ETIQ_CAJA[k], 100 * g["e_sigue"].mean() if len(g) else np.nan, len(g),
                                                                       g["pierna"].nunique(), 100 * g["geo"].mean() if len(g) else np.nan)
            for c in CLASES:
                h = g[g["clase"] == c]
                fila += "%5.1f %% [%5d/%4d]     " % (100 * h["e_sigue"].mean() if len(h) else np.nan, len(h), h["pierna"].nunique())
            print(fila)
        print("   D = clase - base dentro de la caja, promediada (pp); IC 95 %% bootstrap por piernas; t con max(EE piernas, EE ruedas)")
        for i, c in enumerate(CLASES):
            npi = D.loc[D["clase"] == c, "pierna"].nunique()
            r = resumen_prueba(obs["D"][i], bo["leg"]["D"][:, i], bo["dia"]["D"][:, i], pl["D"][:, i], nd)
            r.update({"n_min": int((D["clase"] == c).sum()), "n_piernas": int(npi), "tasa": float(D.loc[D["clase"] == c, "e_sigue"].mean()),
                      "est_caja_rueda": float(obs["Ddc"][i]), "sin_muestra": bool(npi < MIN_PIERNAS)})
            clave = "P2|%s|%s" % (esc, c)
            salida["pruebas"][clave] = r
            et = ""
            if etiquetar:
                et = " -> " + etiqueta(r, previa.get(clave) if previa else None)
            print("   %-18s n %5d min / %4d piernas  tasa %5.1f %%  D %+6.2f pp [%+6.2f; %+6.2f]  t %+5.2f (EE pierna %.2f, rueda %.2f)  "
                  "caja x rueda %+6.2f  placebo p %.4f (media %+.2f)%s%s" %
                  (NOMBRE[c], r["n_min"], npi, 100 * r["tasa"], 100 * r["est"], 100 * r["ic_lo"], 100 * r["ic_hi"], r["t"], 100 * r["se_pierna"],
                   100 * r["se_rueda"], 100 * r["est_caja_rueda"], r["p_placebo"], 100 * r["placebo_media"],
                   "  [menos de %d piernas: no se juzga]" % MIN_PIERNAS if r["sin_muestra"] else "", et))
        # P3
        print("\nP3. Rasgos: S = tasa de SIGUE con el rasgo - sin el rasgo, dentro de la caja (Mantel-Haenszel), pp")
        sR0 = None
        for i, rg in enumerate(RASGOS):
            f = D[rg].to_numpy()
            n1, n0 = int(f.sum()), int((~f).sum())
            l1 = D.loc[f, "pierna"].nunique()
            l0 = D.loc[~f, "pierna"].nunique()
            r = resumen_prueba(obs["S"][i], bo["leg"]["S"][:, i], bo["dia"]["S"][:, i], pl["S"][:, i], nd)
            r.update({"n_si": n1, "n_no": n0, "piernas_si": int(l1), "piernas_no": int(l0), "est_caja_rueda": float(obs["Sdc"][i]),
                      "sin_muestra": bool(min(l1, l0) < MIN_PIERNAS)})
            dif = np.abs(bo["leg"]["S"][:, i]) - np.abs(bo["leg"]["S"][:, 0])
            r["vs_R0"] = float(abs(obs["S"][i]) - abs(obs["S"][0]))
            r["vs_R0_ic"] = [float(x) for x in np.nanpercentile(dif, [2.5, 97.5])]
            clave = "P3|%s|%s" % (esc, rg)
            salida["pruebas"][clave] = r
            et = ""
            if etiquetar:
                et = " -> " + etiqueta(r, previa.get(clave) if previa else None)
            print("   %-12s %-38s si %5d min/%4d piernas, no %5d/%4d  S %+6.2f pp [%+6.2f; %+6.2f]  t %+5.2f  caja x rueda %+6.2f  placebo p %.4f"
                  "  |S|-|S(R0)| %+6.2f [%+6.2f; %+6.2f]%s%s" %
                  (rg, DESC[rg], n1, l1, n0, l0, 100 * r["est"], 100 * r["ic_lo"], 100 * r["ic_hi"], r["t"], 100 * r["est_caja_rueda"],
                   r["p_placebo"], 100 * r["vs_R0"], 100 * r["vs_R0_ic"][0], 100 * r["vs_R0_ic"][1],
                   "  [menos de %d piernas de un lado: no se juzga]" % MIN_PIERNAS if r["sin_muestra"] else "", et))
        # secundario: desenlace en el zigzag del MES (descriptivo)
        Dm = el[el["m_resuelto"]].copy()
        Dm["caja"] = np.clip(np.searchsorted(C.CAJAS, Dm["m_r"].fillna(0).to_numpy(), side="right") - 1, 0, 3)
        ns = Dm.groupby("caja")["m_sigue"].mean()
        Dm["res"] = Dm["m_sigue"].astype(float) - Dm["caja"].map(ns)
        print("\n   (descriptivo) desenlace en el zigzag del MES: D por clase " +
              " | ".join("%s %+.2f pp (n %d)" % (c, 100 * Dm.loc[Dm["clase"] == c, "res"].mean(), (Dm["clase"] == c).sum()) for c in CLASES) +
              "; acuerdo del desenlace ES/MES en los minutos resueltos en los dos: %.1f %%" %
              (100 * (el.loc[el["e_resuelto"] & el["m_resuelto"], "e_sigue"] == el.loc[el["e_resuelto"] & el["m_resuelto"], "m_sigue"]).mean()))
        print("   (descriptivo) cuanto MAS siguio la pierna (en Xs, desde el extremo del minuto), mediana por clase: " +
              " | ".join("%s %.2f" % (c, D.loc[D["clase"] == c, "e_mas"].median()) for c in CLASES))
    json.dump(salida, open(os.path.join(C.DATOS, "r26_03-%s.json" % tanda), "w"), indent=1)


if __name__ == "__main__":
    main()
