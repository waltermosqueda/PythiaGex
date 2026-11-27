# -*- coding: utf-8 -*-
"""RONDA 25, paso 1: las RUEDAS de las tres fuentes (solo lectura). No calcula detectores ni resultados.
Por rueda: velas de 30 min de 9:30-16:00 (13), velas de 5 min (78) si la fuente las tiene, sesion de noche (18:00-9:30 NY), dia anterior
(maximo, minimo, cierre y apertura de su rueda; sin dato si hoy es dia de cambio de contrato), mediana de 10 y X.
Convenciones de historia (para que X sea la misma que uso cada version de A3):
  m30 (2024-25, A3-30 de la R24): solo ruedas completas (13 velas de 30 min);
  m5  (2025-26, A3 de la R23): completas (78 velas) + incompletas por hueco con >= 70 velas; las cortas (cierre <= 13:15) fuera;
  cinta (ES sep-nov 2026, A3 de la R23): los rangos de historia_rangos_semilla.json de la R23 (ES 31-07..25-11; misma fuente que su corrida).
Salidas (scratchpad\\r25): dias_m30.pkl, dias_m5.pkl, dias_cinta.pkl.
Uso: python -I r25_01_datos.py [m30|m5|cinta|todo]"""
import glob, json, os, pickle, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import r25_comun as K


def jornada_y_min(t_utc):
    ny = pd.to_datetime(t_utc).dt.tz_localize("UTC").dt.tz_convert(K.NY).dt.tz_localize(None)
    jor = (ny + pd.Timedelta(hours=6)).dt.strftime("%Y-%m-%d")
    mi = ny.dt.hour * 60 + ny.dt.minute
    return ny, jor, mi


def stats_noche(g):
    if len(g) == 0: return None
    v = g["vol"].to_numpy(float); p = (g["h"].to_numpy(float) + g["l"].to_numpy(float)) / 2
    return dict(o=float(g["o"].iloc[0]), h=float(g["h"].max()), l=float(g["l"].min()), c=float(g["c"].iloc[-1]),
                vw=float((p * v).sum() / v.sum()) if v.sum() > 0 else float("nan"), vol=float(v.sum()), n=len(g))


def agrega30(b5):
    """b5: array (78, 5) o,h,l,c,v -> (13, 5)."""
    out = np.zeros((13, 5))
    for j in range(13):
        s = b5[6 * j:6 * j + 6]
        out[j] = (s[0, 0], s[:, 1].max(), s[:, 2].min(), s[-1, 3], s[:, 4].sum())
    return out


def armar_velas(arch, nb, fuente):
    v = pd.read_csv(arch, usecols=["t", "o", "h", "l", "c", "vol"])
    ny, jor, mi = jornada_y_min(v["t"])
    v["jor"] = jor; v["min"] = mi
    paso = 30 if nb == 13 else 5
    dias = {}; info = {}
    for d, g in v.groupby("jor", sort=True):
        r = g[(g["min"] >= 570) & (g["min"] < 960)]
        n = len(r)
        mins = np.sort(r["min"].to_numpy())
        completa = n == nb and (mins == 570 + paso * np.arange(nb)).all()
        if n == 0: clase = "sin_rth"
        elif completa: clase = "completa"
        elif mins.max() <= 13 * 60 + 15: clase = "corta"
        else: clase = "incompleta"
        noche = stats_noche(g[(g["min"] >= 1080) | (g["min"] < 570)])
        rth = dict(H=float(r["h"].max()), L=float(r["l"].min()), C=float(r["c"].iloc[-1]), O=float(r["o"].iloc[0])) if n else None
        info[d] = dict(clase=clase, n=n, rth=rth, noche=noche)
        if completa:
            dias[d] = r.sort_values("min")[["o", "h", "l", "c", "vol"]].to_numpy(float)
    return dias, info


def historia_y_contexto(dias, info, fuente, rangos_externos=None):
    """mediana de 10 y dia anterior para cada rueda completa."""
    if rangos_externos is not None:
        hist = sorted(rangos_externos); rango = dict(rangos_externos)
    elif fuente == "m30":
        hist = sorted(dias); rango = {d: info[d]["rth"]["H"] - info[d]["rth"]["L"] for d in hist}
    else:
        hist = sorted(d for d in info if info[d]["clase"] == "completa" or (info[d]["clase"] == "incompleta" and info[d]["n"] >= 70))
        rango = {d: info[d]["rth"]["H"] - info[d]["rth"]["L"] for d in hist}
    con_rth = sorted(d for d in info if info[d]["rth"] is not None)
    out = {}
    for d in sorted(dias):
        prev = [x for x in hist if x < d]
        if len(prev) < 10: continue
        med10 = float(np.median([rango[x] for x in prev[-10:]]))
        # dia anterior = la jornada previa con rueda (aunque sea corta), si es de los 5 dias corridos previos y hoy no es cambio de contrato
        pa = [x for x in con_rth if x < d]
        ant = None
        if pa and d not in K.ROLLS:
            x = pa[-1]
            if (pd.Timestamp(d) - pd.Timestamp(x)).days <= 5:
                ant = dict(info[x]["rth"], dia=x, clase=info[x]["clase"])
        out[d] = dict(med10=med10, X=K.X_de(med10), prev=ant)
    return out


def fuente_velas(nombre):
    if nombre == "m30":
        dias, info = armar_velas(K.M30, 13, "m30"); b5 = None
    else:
        dias5, info = armar_velas(K.M5, 78, "m5"); b5 = dias5
        dias = {d: agrega30(a) for d, a in dias5.items()}
    ctx = historia_y_contexto(dias, info, nombre)
    R = {}
    for d, c in ctx.items():
        R[d] = dict(dia=d, fuente=nombre, b30=dias[d], b5=(b5[d] if b5 is not None else None), med10=c["med10"], X=c["X"],
                    rango=float(dias[d][:, 1].max() - dias[d][:, 2].min()), prev=c["prev"], noche=info[d]["noche"], rollo=d in K.ROLLS)
    return R


# ------------------------------------------------------------------------------------------------ cinta de ES
def cintas_es():
    out = {}
    for f in sorted(glob.glob(os.path.join(K.FLUJO, "cinta-ES-*.csv"))):
        p = os.path.basename(f)[:-4].split("-")
        if len(p) != 4 or not os.path.exists(f + ".listo"): continue
        fin = p[3][:8]; out["%s-%s-%s" % (fin[:4], fin[4:6], fin[6:8])] = f
    return out


def leer_cinta(ruta, dia):
    d = pd.read_csv(ruta).drop_duplicates()            # duplicados de FILA ENTERA (como la sonda y A3), no de 4 columnas
    tu = pd.to_datetime(d["t"], format="%Y-%m-%dT%H:%M:%S.%f")
    orden = np.argsort(tu.to_numpy(dtype="datetime64[ns]").astype(np.int64), kind="stable")
    d = d.iloc[orden].reset_index(drop=True); tu = tu.iloc[orden].reset_index(drop=True)
    ny = tu.dt.tz_localize("UTC").dt.tz_convert(K.NY).dt.tz_localize(None)
    jor = (ny + pd.Timedelta(hours=6)).dt.strftime("%Y-%m-%d").to_numpy()
    seg = (ny.dt.hour * 3600 + ny.dt.minute * 60 + ny.dt.second + ny.dt.microsecond / 1e6).to_numpy(float)
    pri = d["primero"].to_numpy(float); ult = d["ultimo"].to_numpy(float); vol = d["vol"].to_numpy(float)
    hi = np.maximum(pri, ult); lo = np.minimum(pri, ult)
    m = jor == dia
    rth = m & (seg >= 34200) & (seg < 57600)
    noc = m & ((seg >= 64800) | (seg < 34200))

    def velas(mask, paso, n):
        b = np.floor((seg[mask] - 34200) / paso).astype(int)
        P, U, H, L, Vv = pri[mask], ult[mask], hi[mask], lo[mask], vol[mask]
        out = np.full((n, 5), np.nan)
        for j in range(n):
            q = b == j
            if not q.any(): continue
            out[j] = (P[q][0], H[q].max(), L[q].min(), U[q][-1], Vv[q].sum())
        return out
    b30 = velas(rth, 1800, 13); b5 = velas(rth, 300, 78)
    noche = None
    if noc.any():
        pm = (pri[noc] + ult[noc]) / 2
        noche = dict(o=float(pri[noc][0]), h=float(hi[noc].max()), l=float(lo[noc].min()), c=float(ult[noc][-1]),
                     vw=float((pm * vol[noc]).sum() / vol[noc].sum()), vol=float(vol[noc].sum()), n=int(noc.sum()))
    ok = rth.any()
    return dict(b30=b30, b5=b5, noche=noche, ultima=float(seg[rth][-1]) if ok else None, primera=float(seg[rth][0]) if ok else None,
                hueco=float(np.diff(seg[rth]).max()) if rth.sum() > 1 else None, n_ordenes=int(rth.sum()))


def fuente_cinta():
    semilla = json.load(open(os.path.join(K.R23, "historia_rangos_semilla.json"), encoding="utf-8"))["rangos"]
    res19 = json.load(open(os.path.join(K.SCR, "..", "r19", "hist", "resumen.json")))
    prev19 = {k.split("|")[1]: v for k, v in res19.items() if k.startswith("ES|") and v.get("rth_h") is not None}
    crudas = cintas_es()
    R = {}; t0 = time.time()
    for dia, ruta in sorted(crudas.items()):
        if not ("2026-09-08" <= dia <= "2026-11-25"): continue
        K.frenar_si_falta_ram(2.0)
        c = leer_cinta(ruta, dia)
        if c["n_ordenes"] == 0 or np.isnan(c["b30"]).any():
            print("  %s: sin rueda completa en la cinta" % dia); continue
        previas = sorted(x for x in semilla if x < dia)
        if len(previas) < 10: continue
        med10 = float(np.median([float(semilla[x]) for x in previas[-10:]]))
        pa = sorted(x for x in prev19 if x < dia)
        ant = None
        if pa and dia not in K.ROLLS and (pd.Timestamp(dia) - pd.Timestamp(pa[-1])).days <= 5:
            v = prev19[pa[-1]]; ant = dict(H=v["rth_h"], L=v["rth_l"], C=v["rth_c"], O=v["rth_o"], dia=pa[-1], clase="r19",
                                           poc=v.get("poc"), vah=v.get("vah"), val=v.get("val"))
        R[dia] = dict(dia=dia, fuente="cinta", b30=c["b30"], b5=c["b5"], med10=med10, X=K.X_de(med10),
                      rango=float(c["b30"][:, 1].max() - c["b30"][:, 2].min()), prev=ant, noche=c["noche"], rollo=dia in K.ROLLS,
                      ultima=c["ultima"], primera=c["primera"], hueco=c["hueco"], archivo=os.path.basename(ruta))
        print("  %s listo (%.0f s) rango %.2f semilla %s" % (dia, time.time() - t0, R[dia]["rango"], semilla.get(dia)), flush=True)
    return R


def main():
    t0 = time.time(); K.frenar_si_falta_ram(2.0)
    que = sys.argv[1] if len(sys.argv) > 1 else "todo"
    for nom in ("m30", "m5", "cinta"):
        if que not in (nom, "todo"): continue
        R = fuente_cinta() if nom == "cinta" else fuente_velas(nom)
        pickle.dump(R, open(os.path.join(K.SCR, "dias_%s.pkl" % nom), "wb"))
        ds = sorted(R)
        print("%s: %d ruedas con contexto (%s a %s); sin dia anterior: %d; dias de cambio de contrato: %s | %.1f s" % (
            nom, len(ds), ds[0], ds[-1], sum(R[d]["prev"] is None for d in ds), [d for d in ds if R[d]["rollo"]], time.time() - t0), flush=True)


if __name__ == "__main__":
    main()
