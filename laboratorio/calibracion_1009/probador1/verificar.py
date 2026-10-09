# -*- coding: utf-8 -*-
"""
probador1/verificar.py — verificacion de la IMPLEMENTACION (no del juez): solo sesiones de ENTRENAMIENTO.
 (1) Paridad de las dominantes de C01 / C02 / C03 contra receta_2_0/extraer_paridad_2_0.calcular() (el port 1:1 de la 2.0)
     armando la cadena con las mismas filas del dataset, en minutos al azar.
 (2) C05: misma cantidad de rayas y mismos minutos que C03; cambios de sorteo = cambios del conjunto de C03.
 (3) C06: subconjunto de C03.
 (4) C04: rho deriva contra 'cuatro' y 'dos' de noche, en pts de MNQ (diferencia de razon x spot de QQQ).
Escribe verificacion_implementacion.json en esta carpeta.
"""
import json
import os
import sys
import time

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(AQUI)
sys.path.insert(0, AQUI)
sys.path.insert(0, os.path.join(RAIZ, "receta_2_0"))
import candidatas as C                      # noqa: E402
import extraer_paridad_2_0 as REC           # noqa: E402  (solo se usan gamma_bs/gex/calcular; main() no corre)

D = C.D


def cadena_receta(libro, dia, foto):
    cab = D.fotos(libro, dia).set_index("foto").loc[foto]
    x = D.filas(libro, dia, foto)
    vencs = json.loads(cab["vencs"])
    orden = list(vencs.keys())
    pos = {int(k): j for j, k in enumerate(orden)}
    filas = []
    for r in x.itertuples(index=False):
        z = lambda v: 0.0 if not (v == v) else float(v)
        filas.append([float(r.strike), pos[int(r.venc)], z(r.oi_call), z(r.oi_put), z(r.iv_call), z(r.iv_put), z(r.vol_call), z(r.vol_put)])
    gen = pd.Timestamp(cab["generado"]).tz_localize("UTC").isoformat()
    return {"generado": gen, "cadena": {"vencimientos": [{"dias": float(vencs[k])} for k in orden], "filas": filas}}


def main():
    t0 = time.time()
    rng = np.random.default_rng(7)
    sal = {"paridad": {}, "C05": {}, "C06": {}, "C04": {}}
    dias = ["2026-09-16", "2026-09-22", "2026-09-25", "2026-09-29"]
    for dia in dias:
        res, dg = C.sesion(dia)
        g = C.E.minutos_y_precio(dia)
        for cid, libro, met in (("C01", "QQQ", "dos"), ("C03", "QQQ", "cuatro"), ("C02", "NDX", "dos")):
            df = res[cid]
            if df.empty:
                continue
            ts_ = df["t"].unique()
            muestra = rng.choice(ts_, size=min(40, len(ts_)), replace=False)
            ok = n = 0
            malos = []
            for t in muestra:
                t = pd.Timestamp(t)
                fila = df[df["t"] == t]
                foto = int(fila["foto"].iat[0]); F = float(fila["F"].iat[0]); cv = float(fila["conv"].iat[0])
                cad = cadena_receta(libro, dia, foto)
                if libro == "QQQ":
                    r = REC.calcular(cad, F, t.to_pydatetime(), escala=cv)
                else:
                    r = REC.calcular(cad, F, t.to_pydatetime(), base=cv)
                mio = [(round(a, 4), round(k, 2)) for a, k in zip(fila["nivel"], fila["K"])]
                rec = [(round(a, 4), round(k, 2)) for a, _, k in r["doms"]]
                n += 1
                if mio == rec:
                    ok += 1
                else:
                    malos.append({"t": str(t), "mio": mio, "receta": rec})
            sal["paridad"].setdefault(cid, {"n": 0, "iguales": 0, "ejemplos_distintos": []})
            p = sal["paridad"][cid]
            p["n"] += n; p["iguales"] += ok; p["ejemplos_distintos"] += malos[:3]
        # C05 y C06 contra C03
        c3 = res["C03"].groupby("t").agg(n=("K", "size"), ks=("K", lambda s: tuple(sorted(s))))
        c5 = res["C05"].groupby("t").agg(n=("K", "size"), ks=("K", lambda s: tuple(sorted(s))))
        j = c3.join(c5, rsuffix="_5", how="left")
        cambios3 = int((j["ks"] != j["ks"].shift()).sum())
        cambios5 = int((j["ks_5"] != j["ks_5"].shift()).sum())
        sal["C05"][dia] = {"minutos_C03": len(c3), "minutos_C05": len(c5), "misma_cantidad": float((j["n"] == j["n_5"]).mean()),
                           "cambios_C03": cambios3, "cambios_C05": cambios5, "diag": dg.get("C05"),
                           "coinciden_con_C03": float((j["ks"] == j["ks_5"]).mean())}
        c6 = res["C06"]
        m = c6.merge(res["C03"][["t", "etiqueta", "K", "nivel"]], on=["t", "etiqueta"], how="left", suffixes=("", "_3"))
        sal["C06"][dia] = {"minutos_C03": int(res["C03"]["t"].nunique()), "minutos_C06": int(c6["t"].nunique()),
                           "subconjunto": bool((m["K"] == m["K_3"]).all() and (abs(m["nivel"] - m["nivel_3"]) < 1e-9).all())}
        # C04: rho deriva vs cuatro/dos de noche
        cv = pd.read_parquet(os.path.join(RAIZ, "datos", "conv_min", "QQQ-%s.parquet" % dia))
        tt = [pd.Timestamp(x) for x in cv["t"]]
        rd, org = C.serie_deriva(dia, tt, cv["cuatro"].to_numpy(float))
        w = E_vent = C.E._cod_ventana_min(cv["t"].to_numpy().astype("datetime64[m]").astype(np.int64))
        noche = (w == 0) & np.isfinite(rd) & np.isfinite(cv["cuatro"].to_numpy(float))
        Sq = C.E.minutos_y_precio(dia)["F"].to_numpy(float) / cv["cuatro"].to_numpy(float)   # spot de QQQ: la diferencia de razon x S = pts de MNQ
        d4 = ((rd - cv["cuatro"].to_numpy(float)) * Sq)[noche]
        d2 = ((rd - cv["dos"].to_numpy(float)) * Sq)[noche & np.isfinite(cv["dos"].to_numpy(float))]
        sal["C04"][dia] = {"minutos_noche_con_ambas": int(noche.sum()),
                           "deriva_menos_cuatro_pts_MNQ": {"mediana": float(np.median(d4)) if len(d4) else None,
                                                            "p5": float(np.percentile(d4, 5)) if len(d4) else None,
                                                            "p95": float(np.percentile(d4, 95)) if len(d4) else None},
                           "deriva_menos_dos_pts_MNQ": {"mediana": float(np.median(d2)) if len(d2) else None,
                                                         "p5": float(np.percentile(d2, 5)) if len(d2) else None,
                                                         "p95": float(np.percentile(d2, 95)) if len(d2) else None},
                           "origenes": pd.Series([o.split(" (")[0].split(" rueda")[0] if o else "?" for o in org]).value_counts().to_dict(),
                           "ejemplo_noche": [str(o) for o in np.asarray(org)[w == 0][:1]] + [str(o) for o in np.asarray(org)[w == 0][-1:]]}
        print(dia, json.dumps({k: sal[k].get(dia) for k in ("C05", "C06")}, default=str), flush=True)
    sal["segundos"] = round(time.time() - t0, 1)
    with open(os.path.join(AQUI, "verificacion_implementacion.json"), "w", encoding="utf-8") as f:
        json.dump(sal, f, ensure_ascii=False, indent=1, default=str)
    print(json.dumps(sal["paridad"], indent=1, default=str)[:3000])
    print(json.dumps(sal["C04"], indent=1, default=str)[:3000])


if __name__ == "__main__":
    main()
