# -*- coding: utf-8 -*-
"""r20_comun.py — carga comun para medir la 2.0 (conjunto r20) con el juez del operador (juez/juez_operador.py, SIN CAMBIOS).
SOLO LECTURA de: velas_m1.csv, niv20_m1.pkl (scratchpad/calibrar), y para extender ESTA NOCHE, el centinela vivo de la 2.0
(%APPDATA%/ATAS/pythiagex2-centinela-hoy-MNQZ6-TimeFrame-M1.jsonl) validado contra la cinta por segundo de la 4.1
(PythiaGex4/cinta/seg-MNQZ6-<dia>.bin, mismo lector que c01_velas.py).

Ventanas (las mismas de c03_reacciones.py / demo_escala.py para la CONGELADA):
  sesion N = fecha de (t + 2 h) (la sesion de CME que abre 22:00Z del dia anterior; la del lunes abre el domingo 22:00Z).
  CONGELADA: N 00:35Z -> N 08:30Z; los lunes, domingo 22:00Z -> lunes 08:30Z (QQQ no cotiza: el spot y la razon de la 2.0 quietos).
  COMPLETA:  (N - 1 dia) 22:00Z -> N 13:30Z (lunes: domingo 22:00Z -> lunes 13:30Z).
Las rayas de la clave t son lo que la 2.0 dibujo al CERRAR la vela t (centinela: rebobinado = cadena con generado <= cierre de la vela,
GammaHoy.cs 2.0 RecorrerArchivo ~1303-1345); el juez las usa en t+1 (desfase_min = 1): sin mirar adelante."""
import json
import os
import struct
import sys

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
JUEZ = os.path.join(os.path.dirname(AQUI), "juez")
sys.path.insert(0, JUEZ)
import juez_operador as J  # noqa: E402

CAL = os.path.join(os.environ["LOCALAPPDATA"], "Temp", "claude", "C--Users-wmx-7-OneDrive-Escritorio-ATAS-nada",
                   "f5879819-e2a4-405b-87b0-959ace090d7b", "scratchpad", "calibrar")
AP = os.path.join(os.environ["APPDATA"], "ATAS")

CONJUNTOS = {
    "r20_doms": ("dom0", "dom1"),            # las dos dominantes de la capa primaria (QQQ) de la 2.0, ya convertidas con su razon
    "r20_zero": ("zero_vol",),               # el zero por volumen de la primaria (QQQ) de la 2.0
}
CONJUNTOS_HOY = {
    "r20_doms+ndx": ("dom0", "dom1", "ndx_dom0", "ndx_dom1"),   # + las dominantes de la capa NDX (solo existen esta noche)
    "r20_zero_ndx": ("ndx_zero_vol",),                         # el zero interpolado de la capa NDX (los rombos de esta noche)
}


def cargar():
    V = pd.read_csv(os.path.join(CAL, "velas_m1.csv"), parse_dates=["t"])
    niv = pd.read_pickle(os.path.join(CAL, "niv20_m1.pkl"))
    return V[["t", "o", "h", "l", "c"]].copy(), niv


def sesion(t):
    return (pd.Timestamp(t) + pd.Timedelta(hours=2)).normalize()


def sesiones(V):
    return sorted(set(((V["t"] + pd.Timedelta(hours=2)).dt.normalize()).tolist()))


def ventana(N, tipo):
    N = pd.Timestamp(N)
    a_comp = N - pd.Timedelta(hours=2)
    if tipo == "congelada":
        a = a_comp if N.weekday() == 0 else N + pd.Timedelta(minutes=35)
        return a, N + pd.Timedelta(hours=8, minutes=30)
    if tipo == "completa":
        return a_comp, N + pd.Timedelta(hours=13, minutes=30)
    raise KeyError(tipo)


def ventanas(Ns, tipo):
    return [ventana(N, tipo) for N in Ns]


def leer_centinela(p):
    filas, niv = [], {}
    with open(p, encoding="utf-8", errors="replace") as fh:
        for ln in fh:
            ln = ln.strip()
            if not ln.endswith("}"):
                continue
            try:
                r = json.loads(ln)
            except Exception:
                continue
            t = pd.Timestamp(r["t"]).floor("1min")
            filas.append((t, r["o"], r["h"], r["l"], r["c"]))
            niv[t] = r.get("niv", {})
    return pd.DataFrame(filas, columns=["t", "o", "h", "l", "c"]), niv


def leer_seg(p):
    """cinta compacta de la 4.1 (copiado de c01_velas.py, formato AdaptadoresFamilia.cs:747-762)."""
    b = open(p, "rb").read(); i = 0

    def rstr():
        nonlocal i
        n = 0; s = 0
        while True:
            x = b[i]; i += 1; n |= (x & 0x7F) << s; s += 7
            if x < 0x80:
                break
        v = b[i:i + n].decode("utf-8"); i += n; return v
    rstr(); i += 4; rstr(); i += 8
    n = struct.unpack_from("<i", b, i)[0]; i += 4
    arr = np.frombuffer(b, dtype=np.dtype([("k", "<i4"), ("t", "<i8"), ("p", "<f8")]), count=n, offset=i)
    return pd.DataFrame({"t": pd.to_datetime(arr["t"], unit="ms"), "p": arr["p"]})


def extender_esta_noche(V, niv):
    """Agrega las velas cerradas del centinela VIVO de la 2.0 posteriores a la ultima de velas_m1.csv (y sus niveles).
    Devuelve (V2, niv2, informe de validacion contra la cinta)."""
    vivo, nv = leer_centinela(os.path.join(AP, "pythiagex2-centinela-hoy-MNQZ6-TimeFrame-M1.jsonl"))
    ult = V["t"].max()
    extra = vivo[vivo["t"] > ult].drop_duplicates("t", keep="last").sort_values("t")
    # solape: las velas comunes tienen que coincidir con velas_m1.csv
    com = vivo[vivo["t"] > ult - pd.Timedelta(hours=3)].drop_duplicates("t", keep="last").merge(V, on="t", suffixes=("", "_v"))
    solape = {"n": int(len(com)), "iguales_ohlc": int(((com[["o", "h", "l", "c"]].to_numpy() - com[["o_v", "h_v", "l_v", "c_v"]].to_numpy()) == 0).all(1).sum())}
    # cinta de hoy
    seg = []
    for d in sorted({x.strftime("%Y-%m-%d") for x in extra["t"]}):
        p = os.path.join(AP, "PythiaGex4", "cinta", "seg-MNQZ6-%s.bin" % d)
        if os.path.exists(p):
            seg.append(leer_seg(p))
    val = {"velas_extra": int(len(extra)), "desde": str(extra["t"].min()) if len(extra) else None,
           "hasta": str(extra["t"].max()) if len(extra) else None, "solape_con_velas_m1": solape}
    if seg:
        S = pd.concat(seg).sort_values("t"); S["m"] = S["t"].dt.floor("1min")
        g = S.groupby("m")["p"].agg(["first", "max", "min", "last"])
        j = extra.set_index("t").join(g, how="inner")
        val.update({"cruce_cinta_min": int(len(j)), "cierre_igual_pct": float(100 * ((j["c"] - j["last"]).abs() < 0.01).mean()) if len(j) else None,
                    "dif_cierre_p95": float((j["c"] - j["last"]).abs().quantile(.95)) if len(j) else None,
                    "dif_alto_p95": float((j["h"] - j["max"]).abs().quantile(.95)) if len(j) else None,
                    "dif_bajo_p95": float((j["l"] - j["min"]).abs().quantile(.95)) if len(j) else None,
                    "cinta_hasta": str(S["t"].max())})
    V2 = pd.concat([V, extra], ignore_index=True).sort_values("t").reset_index(drop=True)
    niv2 = dict(niv)
    for t in extra["t"]:
        niv2[t] = nv.get(t, {})
    return V2, niv2, val


def rayas(niv, claves):
    return J.rayas_desde_niv20(niv, claves)


def por_noche(eventos):
    out = {}
    for e in eventos:
        out.setdefault(e["noche"], []).append(e)
    return out


def r(x, nd=1):
    try:
        x = float(x)
    except (TypeError, ValueError):
        return x
    return round(x, nd) if x == x else None


def limpio(o):
    """para json: Timestamps a str, numpy a python, NaN a None."""
    if isinstance(o, dict):
        return {str(k): limpio(v) for k, v in o.items()}
    if isinstance(o, (list, tuple)):
        return [limpio(v) for v in o]
    if isinstance(o, (pd.Timestamp,)):
        return str(o)
    if isinstance(o, (np.integer,)):
        return int(o)
    if isinstance(o, (np.floating, float)):
        return None if o != o else float(o)
    if isinstance(o, np.bool_):
        return bool(o)
    return o
