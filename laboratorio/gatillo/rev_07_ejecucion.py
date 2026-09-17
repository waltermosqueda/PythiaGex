# -*- coding: utf-8 -*-
"""rev_07_ejecucion.py — (6) REALISMO DE EJECUCION. El banco entra al ULTIMO precio del segundo del gatillo (un precio que ya paso). Aca se entra DESPUES:
   sig    = ultimo precio del segundo siguiente;          peor  = el peor precio del segundo siguiente (techo de pesimismo);
   abre_px= precio de la PRIMERA orden posterior al segundo del gatillo (la 'apertura del segundo siguiente'), costo 0,96 (medio spread ya adentro);
   abre_q = la punta contraria EN REPOSO que encuentra esa primera orden (ask para comprar / bid para vender): es donde llena una orden a mercado; como el spread de
            entrada ya esta pagado en el precio, el costo baja a comision 0,60 (gana por limite) / 0,60 + 0,25 de deslizamiento del stop (pierde);
   punta  = la punta contraria justo DESPUES de la ultima orden del segundo del gatillo (libro recien comido: pesimista), mismo costo que abre_q.
La cinta orden por orden se lee de a UNA sesion (filtro por grupo de filas) y se guarda chica en rev_cache/."""
import os, sys, time
import numpy as np, pandas as pd
import pyarrow.parquet as pq
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rev_lib as R, verdes_lib as V, base as B


def abre_de(s, seg):
    p = os.path.join(R.REVCACHE, "abre-%s.parquet" % s)
    if os.path.exists(p): return pd.read_parquet(p)
    t = pq.read_table(os.path.join(B.CACHE, "cinta.parquet"), columns=["t", "primero", "abid", "aask"], filters=[("sesion", "=", s)]).to_pandas().sort_values("t", kind="stable")
    tt = t["t"].to_numpy(); j = np.searchsorted(tt, (seg.index + pd.Timedelta(seconds=1)).to_numpy(), "left"); ok = j < len(tt); j = np.minimum(j, len(tt) - 1)
    out = pd.DataFrame({"px": np.where(ok, t["primero"].to_numpy()[j], np.nan), "ask": np.where(ok, t["aask"].to_numpy()[j], np.nan), "bid": np.where(ok, t["abid"].to_numpy()[j], np.nan),
                        "espera": np.where(ok, (tt[j] - seg.index.to_numpy()).astype("timedelta64[ms]").astype(float) / 1000.0 - 1.0, np.nan)}, index=seg.index)
    out.to_parquet(p); return out


if __name__ == "__main__":
    t0 = time.time(); T = R.sesiones(); F, _ = V.fotos("NQ"); P = dict(R.P0); PLC = (12.5, -12.5, 37.5, -37.5, 62.5, -62.5); filas = []
    for s, seg in T.items():
        ab = abre_de(s, seg); A = R.Cinta1s(seg); lin = R.lineas_de(V.rayas_por_segundo(seg, F))
        apx = (ab["px"].to_numpy(), ab["px"].to_numpy()); aq = (ab["ask"].to_numpy(), ab["bid"].to_numpy())
        for Lt in lin:
            for c in (0.0,) + PLC:
                g = R.gatillos(A, Lt + c, P)
                for obj in (20, 12):
                    for modo, kw in (("ult (banco)", dict(entrada="ult")), ("sig", dict(entrada="sig")), ("peor", dict(entrada="peor")), ("abre_px", dict(entrada="abre", abre=apx)),
                                     ("abre_q", dict(entrada="abre", abre=aq, costo_gana=0.60, costo_pierde=0.85)), ("punta", dict(entrada="punta", costo_gana=0.60, costo_pierde=0.85))):
                        for x in R.resultado(A, g, obj, **kw): filas.append((s, c, obj, modo, x["neto"], x["riesgo"], x["gana"], ab["espera"].iloc[x["i"]], (A.ask[x["i"]] - A.bid[x["i"]]), (aq[0][x["i"]] - aq[1][x["i"]])))
        print("   %s listo (%.0f s)" % (s, time.time() - t0), flush=True)
    D = pd.DataFrame(filas, columns=["dia", "corr", "obj", "modo", "neto", "riesgo", "gana", "espera", "spread_post", "spread_reposo"]); D.to_parquet(os.path.join(R.REVCACHE, "ejecucion.parquet"))
    r = D[(D["corr"] == 0.0) & (D["obj"] == 20) & (D["modo"] == "ult (banco)")]
    print("\nen el segundo del gatillo: spread justo despues de la ultima orden: mediana %.2f, media %.2f | spread en reposo antes de la orden siguiente: mediana %.2f, media %.2f | espera hasta la orden siguiente: mediana %.2f s, p90 %.1f s" % (
        r["spread_post"].median(), r["spread_post"].mean(), r["spread_reposo"].median(), r["spread_reposo"].mean(), r["espera"].median(), r["espera"].quantile(.9)))
    for obj in (20, 12):
        print("\n=== objetivo %s ===" % obj)
        for modo in ("ult (banco)", "sig", "peor", "abre_px", "abre_q", "punta"):
            d = D[(D["obj"] == obj) & (D["modo"] == modo)]; r = d[d["corr"] == 0.0]; q = d[d["corr"] != 0.0]; sin = ~d["dia"].isin(["2026-09-16", "2026-09-17"])
            print("   %-12s VERDES n %3d | gana %4.1f %% | neto %+.2f | riesgo med %.1f | sin 16/17: %+.2f || PLACEBO (+-12,5 37,5 62,5) n %3d neto %+.2f | sin 16/17 %+.2f" % (
                modo, len(r), 100 * r["gana"].mean(), r["neto"].mean(), r["riesgo"].median(), d[(d["corr"] == 0.0) & sin]["neto"].mean(), len(q), q["neto"].mean(), d[(d["corr"] != 0.0) & sin]["neto"].mean()))
    print("%.0f s" % (time.time() - t0))
