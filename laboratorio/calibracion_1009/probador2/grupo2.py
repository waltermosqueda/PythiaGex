# -*- coding: utf-8 -*-
"""
grupo2.py — PROBADOR 2 de calibracion_1009: las series por defecto de PythiaGex 4.1 (REFERENCIAS C07-C12 del PREREGISTRO sec. 7,
Grupo 2), recalculadas minuto a minuto desde el dataset unificado (../datos/cargar.py), en el formato del arnes (../arnes/evaluar.py).

NO cambia nada del PREREGISTRO ni del arnes. Solo LEE ../datos (y, para la paridad, el niv de la 4.1 en AppData: solo lectura).

Cuenta comun (PREREGISTRO 7.0):
  * Grilla: cada minuto t de la sesion; F(t) = evaluar.minutos_y_precio(dia) (cierre de la vela de 1 min que abrio en t-1).
  * Foto y conversion: D.conversion(libro, dia, t, 'cuatro') (internamente D.foto_vigente: CBOE ultima con generado <= t y vigente
    C9; NQ ultima con ts <= t de menos de 300 s). Filas: D.filas(libro, dia, foto) (cacheadas por foto).
  * Horizonte Hoy con envejecimiento: env = min(2, dias desde generado (NQ: ts) hasta t); dias_env = dias - env; entran
    0 <= dias_env <= max(1, min(dias_env >= 0) + 0,01); tau = max(dias_env, 1/1440)/365 (receta_2_0/extraer_paridad_2_0.py:67-70,
    107-120; tres/auditoria_0810/backtest_familia.filas_hoy 336-356).
  * Gamma: CBOE Black-Scholes r = 0,0375, q = 0, sin descuento en la gamma (tres/nucleo.gamma_bs); NQ Black-76 (tres/nucleo.gamma76).
  * GC_w(K) = sum_venc Gc*w_c*M*S^2*0,01; GP_w(K) = -sum_venc Gp*w_p*M*S^2*0,01; M = 100 (CBOE), 20 (NQ).
  * Conversion 'cuatro': QQQ Fut = K*rho, S = F/rho; NDX Fut = K + B, S = F - B; NQ Fut = K + corr, S = F - corr.
  * Radio R = min(0,02*F, 100) sobre |Fut - F|. Empates de argmax/argmin: el de menor K (np.argmax/argmin sobre K ordenado, como
    tres/extremos_rebote._arg 197-201).
Candidatas (PREREGISTRO 7, Grupo 2):
  C07 C41_MAJORS_QQQ_oi  D1 = argmax GEX_oi > 0 en R; D2 = argmin GEX_oi < 0 en R (extremos_rebote.f_majors 233-241).
  C08 C41_MUROS_QQQ_oi   D1 = argmax GC_oi (> 0) en R; D2 = argmin GP_oi (< 0) en R (extremos_rebote.f_muros 222-231).
  C09 C41_MUROS_NDX_vol  como C08 sobre NDX con w = vol.
  C10 C41_ZEST_QQQ_vol   zero estandar del libro QQQ Hoy entero por volumen, grilla c0 +- 7,5 USD paso 0,05 (c0 = round(S)), cruce
                         mas cercano a S interpolado (backtest_familia.zero_estandar 394-420), x rho; se dibuja si |Z*rho - F| <= 300.
                         Calculado EXACTO cada minuto (sin la cache por cubos de S de backtest_familia.minutos_cboe 545-549: el
                         PREREGISTRO no la menciona; ver notas).
  C11 C41_MUROS_NQ_oi    libro NQ (Rithmic), Black-76, OI, D1/D2 como C08; OI con fecha (backtest_familia.oi_nq_viejo_hasta 438-466,
                         con el texto del PREREGISTRO: sin salto visible se suprime hasta las 02:00 UTC).
  C12 C41_FAM_MUROS_oi   solo minutos con NQ, NDX y QQQ vigentes y OI de NQ valido; cada libro |Fut - F| <= 3 %; GC_oi y GP_oi con
                         el multiplicador FINAL de cada libro = NQ 20, NDX/QQQ 100 USD/pt (backtest_familia: perfil x100 y fusion x
                         M/100, correccion C1); Fut redondeado (np.round) a grilla de 5 pts y sumado; D1/D2 como C08 en R.
Salida: DataFrame t, nivel, etiqueta, K, libro, dia (C10 y C12 SIN columna K: no son strikes; ver evaluar.a_rayas).
"""
import os
import sys

os.environ.setdefault("OMP_NUM_THREADS", "1")
os.environ.setdefault("OPENBLAS_NUM_THREADS", "1")
os.environ.setdefault("MKL_NUM_THREADS", "1")

import json
import math
import time

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(AQUI)
for _p in (os.path.join(RAIZ, "datos"), os.path.join(RAIZ, "arnes")):
    if _p not in sys.path:
        sys.path.insert(0, _p)

import cargar as D          # noqa: E402
import evaluar as EV        # noqa: E402  (solo minutos_y_precio y las constantes de particion)

VERSION = "g2-1.0"
TASA = 0.0375
PISO_DIAS = 1.0 / 1440.0
MULT = {"NQ": 20.0, "NDX": 100.0, "QQQ": 100.0}
POSFERIADO = ("2026-09-08",)
FAM_GRILLA = 5.0
IDS = {"C07": "C07_C41_MAJORS_QQQ_oi", "C08": "C08_C41_MUROS_QQQ_oi", "C09": "C09_C41_MUROS_NDX_vol",
       "C10": "C10_C41_ZEST_QQQ_vol", "C11": "C11_C41_MUROS_NQ_oi", "C12": "C12_C41_FAM_MUROS_oi"}
LIBRO_DE = {"C07": "QQQ", "C08": "QQQ", "C09": "NDX", "C10": "QQQ", "C11": "NQ", "C12": "FAM"}
_INV_SQRT_2PI = 1.0 / math.sqrt(2.0 * math.pi)


# ============================================================================================ gamma (tres/nucleo.py, sin cambios)
def _fi(x):
    return np.exp(-0.5 * x * x) * _INV_SQRT_2PI


def gamma_bs(S, K, T, iv, r=TASA):
    S = np.asarray(S, float); K = np.asarray(K, float); T = np.asarray(T, float); iv = np.asarray(iv, float)
    ok = (S > 0) & (K > 0) & (T > 0) & (iv > 0)
    with np.errstate(divide="ignore", invalid="ignore"):
        v = iv * np.sqrt(T)
        d1 = (np.log(S / K) + (r + 0.5 * iv * iv) * T) / v
        g = _fi(d1) / (S * v)
    return np.where(ok, g, 0.0)


def gamma76(F, K, T, iv):
    F = np.asarray(F, float); K = np.asarray(K, float); T = np.asarray(T, float); iv = np.asarray(iv, float)
    ok = (F > 0) & (K > 0) & (T > 0) & (iv > 0)
    with np.errstate(divide="ignore", invalid="ignore"):
        v = iv * np.sqrt(T)
        d1 = (np.log(F / K) + 0.5 * iv * iv * T) / v
        g = _fi(d1) / (F * v)
    return np.where(ok, g, 0.0)


# ============================================================================================ horizonte Hoy
def _hoy(dias, env):
    """mascara del horizonte Hoy y tau en anos (GammaHoyNucleo.PasaHorizonte Hoy)."""
    de = np.asarray(dias, float) - env
    val = de[de >= 0]
    mas = float(val.min()) if len(val) else 0.0
    tope = max(1.0, mas + 0.01)
    ok = (de >= 0) & (de <= tope)
    return ok, np.maximum(de, PISO_DIAS) / 365.0


def _env(t, gen):
    s = (pd.Timestamp(t) - pd.Timestamp(gen)).total_seconds()
    return max(0.0, min(2.0, s / 86400.0))


class LibroSesion:
    """acceso cacheado a las fotos/filas de un libro en una sesion."""

    def __init__(self, libro, dia):
        self.libro, self.dia = libro, dia
        self.fot = D.fotos(libro, dia)
        self.cache = {}
        self.col_gen = "ts" if libro == "NQ" else "generado"
        self.gen = {int(f): g for f, g in zip(self.fot["foto"], self.fot[self.col_gen])} if len(self.fot) else {}

    def filas_hoy(self, foto, t):
        """filas del horizonte Hoy de la foto en t, como arrays (CBOE: una fila por strike-vencimiento con call y put;
        NQ: una fila por contrato)."""
        if foto not in self.cache:
            x = D.filas(self.libro, self.dia, foto)
            if self.libro == "NQ":
                arr = dict(K=x["strike"].to_numpy(float), dias=x["dias"].to_numpy(float), call=x["es_call"].to_numpy(int) == 1,
                           oi=x["oi"].to_numpy(float), iv=x["iv"].to_numpy(float))
            else:
                arr = dict(K=x["strike"].to_numpy(float), dias=x["dias"].to_numpy(float), oic=x["oi_call"].to_numpy(float),
                           oip=x["oi_put"].to_numpy(float), ivc=x["iv_call"].to_numpy(float), ivp=x["iv_put"].to_numpy(float),
                           volc=x["vol_call"].to_numpy(float), volp=x["vol_put"].to_numpy(float))
            self.cache[foto] = arr
            if len(self.cache) > 64:
                self.cache.pop(next(iter(self.cache)))
        arr = self.cache[foto]
        env = _env(t, self.gen[foto])
        ok, T = _hoy(arr["dias"], env)
        if not ok.any():
            return None
        out = {k: v[ok] for k, v in arr.items()}
        out["T"] = T[ok]
        out["env"] = env
        return out


def perfil(libro, fl, S, conv):
    """GC/GP por vol y OI por strike (ordenado por K), Fut en MNQ. fl = filas_hoy."""
    if fl is None or not (S > 0):
        return None
    esc = MULT[libro] * S * S * 0.01
    if libro == "NQ":
        g = gamma76(S, fl["K"], fl["T"], fl["iv"])
        a = g * fl["oi"] * esc
        c = fl["call"]
        K = fl["K"]
        aoc = np.where(c, a, 0.0); aop = np.where(c, 0.0, -a)
        avc = np.zeros_like(aoc); avp = np.zeros_like(aop)          # C11/C12 solo usan OI
    else:
        gc = gamma_bs(S, fl["K"], fl["T"], fl["ivc"]); gp = gamma_bs(S, fl["K"], fl["T"], fl["ivp"])
        avc, avp = gc * fl["volc"] * esc, -gp * fl["volp"] * esc
        aoc, aop = gc * fl["oic"] * esc, -gp * fl["oip"] * esc
        K = fl["K"]
    aporta = (avc != 0) | (avp != 0) | (aoc != 0) | (aop != 0)
    if not aporta.any():
        return None
    ks, inv = np.unique(K[aporta], return_inverse=True)
    n = len(ks)
    s = lambda x: np.bincount(inv, x[aporta], n)
    r = dict(K=ks, gvC=s(avc), gvP=s(avp), goC=s(aoc), goP=s(aop))
    r["gv"] = r["gvC"] + r["gvP"]; r["go"] = r["goC"] + r["goP"]
    r["Fut"] = ks * conv if libro == "QQQ" else ks + conv
    return r


def _arg(mask, val, mayor=True):
    idx = np.nonzero(mask)[0]
    if len(idx) == 0:
        return None
    return idx[np.argmax(val[idx])] if mayor else idx[np.argmin(val[idx])]


def muros(p, F, oi):
    R = min(F * 0.02, 100.0)
    m = np.abs(p["Fut"] - F) <= R
    C = p["goC"] if oi else p["gvC"]; P = p["goP"] if oi else p["gvP"]
    out = []
    i = _arg(m & (C > 0), C, True)
    if i is not None:
        out.append((float(p["Fut"][i]), "D1", float(p["K"][i])))
    j = _arg(m & (P < 0), P, False)
    if j is not None:
        out.append((float(p["Fut"][j]), "D2", float(p["K"][j])))
    return out


def majors(p, F, oi):
    R = min(F * 0.02, 100.0)
    m = np.abs(p["Fut"] - F) <= R
    g = p["go"] if oi else p["gv"]
    out = []
    i = _arg(m & (g > 0), g, True)
    if i is not None:
        out.append((float(p["Fut"][i]), "D1", float(p["K"][i])))
    j = _arg(m & (g < 0), g, False)
    if j is not None:
        out.append((float(p["Fut"][j]), "D2", float(p["K"][j])))
    return out


def zero_estandar_vol(fl, S, paso=0.05, semi=7.5, centro=1.0):
    """backtest_familia.zero_estandar (394-420), solo la parte por volumen, CBOE (Black-Scholes). En el eje del libro."""
    if fl is None or not (S > 0):
        return float("nan")
    c0 = round(S / centro) * centro
    n = int(round(semi / paso))
    xs = c0 + paso * np.arange(-n, n + 1)
    X = xs[:, None]
    gc = gamma_bs(X, fl["K"][None, :], fl["T"][None, :], fl["ivc"][None, :])
    gp = gamma_bs(X, fl["K"][None, :], fl["T"][None, :], fl["ivp"][None, :])
    cv = (gc * fl["volc"][None, :] - gp * fl["volp"][None, :]).sum(1) * xs * xs
    s = np.sign(cv)
    idx = np.nonzero((s[:-1] * s[1:]) < 0)[0]
    if len(idx) == 0:
        return float("nan")
    z = xs[idx] + (xs[idx + 1] - xs[idx]) * (-cv[idx]) / (cv[idx + 1] - cv[idx])
    return float(z[np.argmin(np.abs(z - S))])


# ============================================================================================ OI con fecha de NQ (C2)
def oi_nq_viejo_hasta(dia, lib):
    """backtest_familia.oi_nq_viejo_hasta (438-466) con el texto del PREREGISTRO (sec. 7, C11): sin rayas por OI desde las 22:00 UTC
    hasta el salto de OI de Rithmic (primer par de fotos con >= 50 % de los contratos comunes con OI distinto, entre 22:00 y 03:30
    UTC); lunes y posferiados no se suprime; sin salto visible, se suprime hasta las 02:00 UTC.
    DIFERENCIA declarada con el codigo citado: este, sin salto, NO suprimia si la primera foto de la noche era >= 01:55 UTC (SUPUESTO
    del 08-10); el PREREGISTRO no lo dice y aca se suprime hasta las 02:00 igual (afecta a lo sumo 5 min)."""
    ini = pd.Timestamp(dia) - pd.Timedelta(hours=2)
    lim = pd.Timestamp(dia) + pd.Timedelta(hours=3, minutes=30)
    if pd.Timestamp(dia).weekday() == 0 or dia in POSFERIADO:
        return None, "lunes o posferiado: OI ya actualizado"
    f = lib.fot
    if f.empty:
        return None, "sin fotos"
    noche = f[(f["ts"] >= ini) & (f["ts"] <= lim)]
    if noche.empty:
        return pd.Timestamp(dia) + pd.Timedelta(hours=2), "sin fotos de noche: suprimido hasta 02:00 UTC"
    X = D._filas("NQ", dia)

    def mapa(foto, ts):
        x = X[X["foto"] == int(foto)]
        venc = (pd.Timestamp(ts) + pd.to_timedelta(x["dias"].to_numpy(float), unit="D")).strftime("%m-%d")
        return {(float(k), v, int(c)): float(o) for k, v, c, o in zip(x["strike"], venc, x["es_call"], x["oi"])}
    filas = list(zip(noche["foto"], noche["ts"]))
    prev = mapa(*filas[0])
    for foto, ts in filas[1:]:
        cur = mapa(foto, ts)
        com = [k for k in cur if k in prev and (cur[k] > 0 or prev[k] > 0)]
        if len(com) >= 20 and sum(1 for k in com if cur[k] != prev[k]) >= 0.5 * len(com):
            return pd.Timestamp(ts), "salto de OI medido a las %s UTC" % pd.Timestamp(ts).strftime("%H:%M:%S")
        prev = cur
    return pd.Timestamp(dia) + pd.Timedelta(hours=2), "sin salto visible: suprimido hasta 02:00 UTC"


# ============================================================================================ una sesion
def niveles_sesion(dia, que=("C07", "C08", "C09", "C10", "C11", "C12"), con_diag=True):
    """Niveles por minuto de las candidatas pedidas en la sesion `dia`. Devuelve ({id: DataFrame}, diag)."""
    t0 = time.time()
    g = EV.minutos_y_precio(dia)
    libros_nec = set()
    for c in que:
        libros_nec |= ({"NQ", "NDX", "QQQ"} if c == "C12" else {LIBRO_DE[c]})
    libs = {lb: LibroSesion(lb, dia) for lb in libros_nec if dia in D.dias(lb)}
    viejo_hasta, como = (None, "sin NQ")
    if "NQ" in libs:
        viejo_hasta, como = oi_nq_viejo_hasta(dia, libs["NQ"])
    ini_noche = pd.Timestamp(dia) - pd.Timedelta(hours=2)
    filas = {c: [] for c in que}
    diag = {"dia": dia, "minutos": len(g), "minutos_con_F": int(np.isfinite(g["F"]).sum()), "oi_nq_viejo_hasta": str(viejo_hasta),
            "oi_nq_como": como, "minutos_libro": {lb: 0 for lb in libs}, "sin_conv": {lb: 0 for lb in libs},
            "conv_origenes": {lb: {} for lb in libs}, "env_max": {lb: 0.0 for lb in libs}, "zest_sin_cruce": 0, "zest_lejos": 0,
            "fam_minutos": 0, "fam_sin_oi": 0}
    for t, F in zip(g["t"], g["F"].to_numpy(float)):
        if not np.isfinite(F):
            continue
        perf = {}
        fls = {}
        for lb, L in libs.items():
            c = D.conversion(lb, dia, t, "cuatro")
            if c["foto"] is None:
                continue
            v = c["valor"]
            if not (v == v):
                diag["sin_conv"][lb] += 1
                o = c["origen"][:60]
                diag["conv_origenes"][lb][o] = diag["conv_origenes"][lb].get(o, 0) + 1
                continue
            S = F / v if lb == "QQQ" else F - v
            fl = L.filas_hoy(int(c["foto"]), t)
            p = perfil(lb, fl, S, v)
            if p is None:
                continue
            p["S"] = S; p["conv"] = v; p["foto"] = int(c["foto"])
            perf[lb] = p; fls[lb] = fl
            diag["minutos_libro"][lb] += 1
            diag["env_max"][lb] = max(diag["env_max"][lb], fl["env"])
        oi_nq_ok = not (viejo_hasta is not None and ini_noche <= t < viejo_hasta)
        q = perf.get("QQQ")
        if q is not None:
            if "C07" in filas:
                filas["C07"] += [(t, x, e, k, "QQQ") for x, e, k in majors(q, F, True)]
            if "C08" in filas:
                filas["C08"] += [(t, x, e, k, "QQQ") for x, e, k in muros(q, F, True)]
            if "C10" in filas:
                z = zero_estandar_vol(fls["QQQ"], q["S"])
                if z == z:
                    zf = z * q["conv"]
                    if abs(zf - F) <= 300.0:
                        filas["C10"].append((t, zf, "Z", float("nan"), "QQQ"))
                    else:
                        diag["zest_lejos"] += 1
                else:
                    diag["zest_sin_cruce"] += 1
        n = perf.get("NDX")
        if n is not None and "C09" in filas:
            filas["C09"] += [(t, x, e, k, "NDX") for x, e, k in muros(n, F, False)]
        nq = perf.get("NQ")
        if nq is not None and "C11" in filas and oi_nq_ok:
            filas["C11"] += [(t, x, e, k, "NQ") for x, e, k in muros(nq, F, True)]
        if "C12" in filas and all(lb in perf for lb in ("NQ", "NDX", "QQQ")):
            if not oi_nq_ok:
                diag["fam_sin_oi"] += 1
            else:
                diag["fam_minutos"] += 1
                partes = []
                for lb in ("NQ", "NDX", "QQQ"):
                    p = perf[lb]
                    m = np.abs(p["Fut"] - F) <= F * 0.03
                    # multiplicador FINAL por libro: NQ 20, NDX/QQQ 100 (perfil() ya lo trae con M del libro)
                    partes.append((np.round(p["Fut"][m] / FAM_GRILLA) * FAM_GRILLA, p["goC"][m], p["goP"][m]))
                Fut = np.concatenate([a for a, _, _ in partes])
                if len(Fut):
                    ks, inv = np.unique(Fut, return_inverse=True)
                    fus = dict(K=ks, Fut=ks, goC=np.bincount(inv, np.concatenate([b for _, b, _ in partes]), len(ks)),
                               goP=np.bincount(inv, np.concatenate([c for _, _, c in partes]), len(ks)))
                    fus["gvC"] = np.zeros(len(ks)); fus["gvP"] = np.zeros(len(ks))
                    filas["C12"] += [(t, x, e, float("nan"), "FAM") for x, e, _ in muros(fus, F, True)]
    out = {}
    for c, fs in filas.items():
        df = pd.DataFrame(fs, columns=["t", "nivel", "etiqueta", "K", "libro"])
        df["dia"] = dia
        if c in ("C10", "C12"):
            df = df.drop(columns=["K"])
        out[c] = df
    diag["segundos"] = round(time.time() - t0, 1)
    diag["filas"] = {c: len(v) for c, v in out.items()}
    return out, diag


def dias_de(c, fase):
    """sesiones de la fase donde existe el libro de la candidata (PREREGISTRO sec. 6)."""
    if fase == "ENTRENAMIENTO":
        base = [d for d in D.dias() if d <= EV.ENTRENAMIENTO_HASTA]
    elif fase == "PRUEBA":
        base = [d for d in D.dias() if d in EV.PRUEBA]
    else:
        raise ValueError(fase)
    lb = LIBRO_DE[c]
    if lb == "FAM":
        con = set(D.dias("NQ")) & set(D.dias("NDX")) & set(D.dias("QQQ"))
    else:
        con = set(D.dias(lb))
    return [d for d in base if d in con]


def _uno(args):
    dia, que = args
    return niveles_sesion(dia, que)


def generar(fase, que=("C07", "C08", "C09", "C10", "C11", "C12"), workers=4):
    """genera y guarda niveles_<FASE>.parquet de cada candidata (en probador2/niveles/<ID>/ y en probadores/grupo2/<ID>/)."""
    from concurrent.futures import ProcessPoolExecutor
    dias = sorted(set(d for c in que for d in dias_de(c, fase)))
    print("fase", fase, "sesiones", dias, flush=True)
    res = {}
    with ProcessPoolExecutor(max_workers=workers) as ex:
        for (out, diag), d in zip(ex.map(_uno, [(d, tuple(c for c in que if d in dias_de(c, fase))) for d in dias]), dias):
            res[d] = (out, diag)
            print(d, json.dumps(diag["filas"]), "%.1fs" % diag["segundos"], flush=True)
    diags = {d: v[1] for d, v in res.items()}
    rutas = {}
    for c in que:
        partes = [res[d][0][c] for d in dias if c in res[d][0]]
        df = pd.concat(partes, ignore_index=True) if partes else pd.DataFrame(columns=["t", "nivel", "etiqueta", "K", "libro", "dia"])
        for base in (os.path.join(AQUI, "niveles", IDS[c]), os.path.join(RAIZ, "probadores", "grupo2", IDS[c])):
            os.makedirs(base, exist_ok=True)
            p = os.path.join(base, "niveles_%s.parquet" % fase)
            df.to_parquet(p, index=False)
        rutas[c] = p
    with open(os.path.join(AQUI, "niveles", "diag_%s.json" % fase), "w", encoding="utf-8") as f:
        json.dump(diags, f, ensure_ascii=False, indent=1, default=str)
    return rutas, diags


if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] in ("ENTRENAMIENTO", "PRUEBA"):
        generar(sys.argv[1])
    else:
        print(__doc__)
