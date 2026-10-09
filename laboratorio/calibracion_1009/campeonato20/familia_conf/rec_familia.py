# -*- coding: utf-8 -*-
"""
rec_familia.py — campeonato20, familia sumada + confluencias + combinaciones (PREREGISTRO_familia.md). SOLO LECTURA del dataset
(../../datos/cargar.py, datos/conv_min) y del codigo de los probadores (se importa, no se cambia). Escribe solo en esta carpeta.

Por sesion d, cada minuto t de la grilla (arnes/evaluar.minutos_y_precio, F = cierre de la vela que abrio en t-1):
  libros NQ / NDX / QQQ con la foto vigente y la conversion 'cuatro' de datos/conv_min (la de la 4.1), horizonte Hoy envejecido,
  gamma BS (CBOE, r 0,0375) / Black-76 (NQ), GEX por lado x M x S^2 x 0,01 (NQ 20, CBOE 100), strikes a <= 3 % de F.
  Convenciones de probador2/grupo2.py (paridad 100 % con el niv de la 4.1 del 10-09 en FAM_MUROS_oi), con el volumen de NQ
  agregado (vol_hoy = el 'vol' del viva, FotoNq.cs).
Series (claves 'D1', 'D2', ... o 'LIBRO.D1'): F1 FAM_MUROS_vol, F2 FAM_MUROS_oi, F3 CONF_vol, K1 CONF_NDX_NQ_vol, K2 CONF_todo,
  K3a FAM_MUROS_vol_TOP, K3b FAM_MUROS_oi_TOP, K4a FAM_MUROS_oi_DOI, K4b MUROS_oi_DOI, K0 MUROS_oi_UNION (control).
Salida: datos/rec/<dia>.pkl = {'grilla': [t con F valido], 'series': {serie: {t: {clave: (precio, etiqueta)}}}, 'diag': {...}}.
Uso: python -I rec_familia.py [dia ...]   (sin dias: todas las sesiones con algun libro; 3 procesos, prioridad baja)
"""
import ctypes
import json
import math
import os
import sys
import time

os.environ.setdefault("OMP_NUM_THREADS", "1")
os.environ.setdefault("OPENBLAS_NUM_THREADS", "1")


def prioridad_baja():
    try:
        ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)
    except Exception:
        pass


prioridad_baja()

import numpy as np  # noqa: E402
import pandas as pd  # noqa: E402

AQUI = os.path.dirname(os.path.abspath(__file__))
CAL = os.path.normpath(os.path.join(AQUI, "..", ".."))
for _p in (os.path.join(CAL, "datos"), os.path.join(CAL, "arnes"), os.path.join(CAL, "probador2")):
    if _p not in sys.path:
        sys.path.insert(0, _p)
import cargar as D  # noqa: E402
import evaluar as EV  # noqa: E402
import grupo2 as G2  # noqa: E402  (oi_nq_viejo_hasta, gamma_bs, gamma76: sin cambios)

SALIDA = os.path.join(AQUI, "datos", "rec")
MULT = {"NQ": 20.0, "NDX": 100.0, "QQQ": 100.0}
LIBROS = ("NQ", "NDX", "QQQ")
PISO_DIAS = 1.0 / 1440.0
RADIO = 100.0
ISLA = 0.10
CONF_PTS = 3.0
FAM_GRILLA = 5.0
FILTRO = 0.03
MIN_CLAVES_SALTO = 20
FRACCION_SALTO = 0.20
SERIES = ("FAM_MUROS_vol", "FAM_MUROS_oi", "CONF_vol", "CONF_NDX_NQ_vol", "CONF_todo", "FAM_MUROS_vol_TOP", "FAM_MUROS_oi_TOP",
          "FAM_MUROS_oi_DOI", "MUROS_oi_DOI", "MUROS_oi_UNION")


# ================================================================================================ fotos por libro
def _dias_previos(libro, dia, n=1):
    ds = D.dias(libro)
    i = ds.index(dia) if dia in ds else len([x for x in ds if x < dia])
    return ds[max(0, i - n):i]


class Libro:
    """Fotos de un libro en la sesion (arrays por foto), su conversion por minuto y el regimen de OI de cada foto."""

    def __init__(self, libro, dia):
        self.libro, self.dia = libro, dia
        self.ok = dia in D.dias(libro)
        self.fotos = {}
        self.gen = {}
        if not self.ok:
            return
        cv = pd.read_parquet(os.path.join(CAL, "datos", "conv_min", "%s-%s.parquet" % (libro, dia)))
        self.t_cv = pd.DatetimeIndex(cv["t"])
        self.foto_min = cv["foto"].to_numpy()
        self.conv_min = cv["cuatro"].to_numpy(float)
        self._armar(dia, propio=True)
        self._regimenes()

    # ---- filas de todas las fotos de una sesion
    def _leer(self, dia):
        f = D.fotos(self.libro, dia)
        X = D._filas(self.libro, dia)
        out = []
        if f.empty or X.empty:
            return out
        if self.libro == "NQ":
            g = {int(k): v for k, v in X.groupby("foto")}
            for _, r in f.iterrows():
                x = g.get(int(r["foto"]))
                if x is None:
                    continue
                ts = pd.Timestamp(r["ts"])
                K = x["strike"].to_numpy(float); dias = x["dias"].to_numpy(float); call = x["es_call"].to_numpy(int) == 1
                oi = np.nan_to_num(x["oi"].to_numpy(float)); iv = np.nan_to_num(x["iv"].to_numpy(float))
                vol = np.nan_to_num(x["vol_hoy"].to_numpy(float))
                venc = (ts + pd.to_timedelta(dias, unit="D")).strftime("%m-%d").to_numpy()
                lado = np.where(call, 1, 0)
                out.append(dict(foto=int(r["foto"]), gen=ts, K=K, dias=dias, call=call, oi=oi, iv=iv, vol=vol,
                                clave=list(zip(K.tolist(), venc.tolist(), lado.tolist()))))
        else:
            g = {int(k): v for k, v in X.groupby("filas_id")}
            for _, r in f.iterrows():
                x = g.get(int(r["filas_id"]))
                if x is None:
                    continue
                vd = {int(k): float(v) for k, v in json.loads(r["vencs"]).items()}
                venc = x["venc"].to_numpy(np.int64)
                nn = lambda c: np.nan_to_num(x[c].to_numpy(float))
                out.append(dict(foto=int(r["foto"]), gen=pd.Timestamp(r["generado"]), K=x["strike"].to_numpy(float), venc=venc,
                                dias=np.array([vd.get(int(v), np.nan) for v in venc], float), oic=nn("oi_call"), oip=nn("oi_put"),
                                ivc=nn("iv_call"), ivp=nn("iv_put"), volc=nn("vol_call"), volp=nn("vol_put"), filas_id=int(r["filas_id"])))
        return out

    def _armar(self, dia, propio):
        for fo in self._leer(dia):
            self.fotos[fo["foto"]] = fo

    # ---- mapa de OI por (strike, vencimiento, lado): ClavesCambios.Mapa(f, oi=true) = solo lados con IV > 0
    def _mapa(self, fo):
        if self.libro == "NQ":
            return {k: o for k, o, iv in zip(fo["clave"], fo["oi"].tolist(), fo["iv"].tolist()) if iv > 0}
        m = {}
        for k, v, oc, op, ic, ip in zip(fo["K"].tolist(), fo["venc"].tolist(), fo["oic"].tolist(), fo["oip"].tolist(),
                                        fo["ivc"].tolist(), fo["ivp"].tolist()):
            if ic > 0:
                m[(k, v, 1)] = oc
            if ip > 0:
                m[(k, v, 0)] = op
        return m

    def _regimenes(self):
        """Port de RegimenesOi.Procesar (4.1, DESVIO 1 del pre-registro): fotos en orden (sesion previa disponible + esta); fotos con las
        mismas filas que la anterior (CBOE de noche) no se comparan; C = claves con OI > 0 en las DOS fotos; salto normal (|C| >= 20 y
        >= 20 % cambiadas), de libro flaco (8 <= |C| < 20 y >= 80 %) o DUDOSO (otro par con alguna cambiada y >= 50 %): R1 = R0, R0 nuevo.
        OI del regimen = ultimo valor visto. Para cada foto de la sesion se guarda (R0, R1) DESPUES de procesarla."""
        seq = []
        for d in _dias_previos(self.libro, self.dia, 1):
            for fo in self._leer(d):
                seq.append((fo["gen"], -1, fo.get("filas_id"), self._mapa(fo)))
        cache_mapa = {}
        for k in sorted(self.fotos, key=lambda k: (self.fotos[k]["gen"], k)):
            fo = self.fotos[k]
            ck = fo.get("filas_id", ("nq", k))
            if ck not in cache_mapa:
                cache_mapa[ck] = self._mapa(fo)
            seq.append((fo["gen"], k, fo.get("filas_id"), cache_mapa[ck]))
        seq.sort(key=lambda x: (x[0], x[1]))
        self.estado = {}
        self.saltos = []
        R0 = R1 = None
        prev = None; prev_fid = None
        for gen, k, fid, m in seq:
            if not m:
                if k >= 0:
                    self.estado[k] = (R0, R1)
                continue
            if not (prev is not None and R0 is not None and fid is not None and fid == prev_fid):
                if prev is not None:
                    com = dist = 0
                    for q, x in m.items():
                        if not (x > 0):
                            continue
                        y = prev.get(q)
                        if y is None or not (y > 0):
                            continue
                        com += 1
                        if x != y:
                            dist += 1
                    normal = com >= MIN_CLAVES_SALTO and dist >= FRACCION_SALTO * com
                    flaco = (not normal) and 8 <= com < MIN_CLAVES_SALTO and dist >= 0.80 * com
                    dudoso = (not normal) and (not flaco) and dist > 0 and dist >= 0.50 * com
                    if normal or flaco or dudoso:
                        self.saltos.append((str(gen), com, dist, "normal" if normal else ("flaco" if flaco else "dudoso")))
                        R1 = R0
                        R0 = {"inicio": gen, "salto": True, "dudoso": dudoso, "oi": {}}
                if R0 is None:
                    R0 = {"inicio": gen, "salto": False, "dudoso": False, "oi": {}}
                R0["oi"].update(m)
                prev = m; prev_fid = fid
            if k >= 0:
                self.estado[k] = (R0, R1)
        self._doi_cache = {}

    def doi(self, k):
        """CambiosFamilia.CompararOi: dOI por fila de la foto k = OI ahora - OI de R1 en la misma clave (lado con IV > 0; clave ausente
        en R1 = no se compara, 0). None si no hay R0/R1, si R0 es dudoso o si ninguna clave se pudo comparar. Devuelve tambien R0."""
        if k in self._doi_cache:
            return self._doi_cache[k]
        R0, R1 = self.estado.get(k, (None, None))
        fo = self.fotos[k]
        r = None
        if R0 is not None and R1 is not None and not R0["dudoso"]:
            ant = R1["oi"]; comp = 0
            if self.libro == "NQ":
                d = np.zeros(len(fo["K"]))
                for i, (q, o, iv) in enumerate(zip(fo["clave"], fo["oi"].tolist(), fo["iv"].tolist())):
                    if iv > 0 and q in ant:
                        d[i] = o - ant[q]; comp += 1
                r = d if comp else None
            else:
                dc = np.zeros(len(fo["K"])); dp = np.zeros(len(fo["K"]))
                for i, (a, b, oc, op, ic, ip) in enumerate(zip(fo["K"].tolist(), fo["venc"].tolist(), fo["oic"].tolist(), fo["oip"].tolist(),
                                                               fo["ivc"].tolist(), fo["ivp"].tolist())):
                    if ic > 0 and (a, b, 1) in ant:
                        dc[i] = oc - ant[(a, b, 1)]; comp += 1
                    if ip > 0 and (a, b, 0) in ant:
                        dp[i] = op - ant[(a, b, 0)]; comp += 1
                r = (dc, dp) if comp else None
        out = (r, R0)
        self._doi_cache[k] = out
        if len(self._doi_cache) > 32:
            self._doi_cache.pop(next(iter(self._doi_cache)))
        return out

    # ---- el libro en un minuto
    def minuto(self, i, t, F):
        """dict con Fut, K, GvC, GvP, GoC, GoP, gv, go, dGC, dGP (cambio de OI por lado valuado con la gamma de ahora; NaN si no se
        conoce la publicacion anterior), S, conv; strikes a <= 3 % de F. None si no hay libro."""
        if not self.ok:
            return None
        k = self.foto_min[i]; v = self.conv_min[i]
        if k is None or not (k == k) or k < 0 or not (v == v):
            return None
        k = int(k)
        fo = self.fotos.get(k)
        if fo is None:
            return None
        S = F / v if self.libro == "QQQ" else F - v
        if not (S > 0):
            return None
        env = max(0.0, min(2.0, (t - fo["gen"]).total_seconds() / 86400.0))
        de = fo["dias"] - env
        val = de[de >= 0]
        mas = float(val.min()) if len(val) else 0.0
        ok = (de >= 0) & (de <= max(1.0, mas + 0.01))
        if not ok.any():
            return None
        T = np.maximum(de[ok], PISO_DIAS) / 365.0
        K = fo["K"][ok]
        esc = MULT[self.libro] * S * S * 0.01
        dd, R0 = self.doi(k)
        if dd is not None and self.libro != "NQ" and R0 is not None:
            if _fecha_ny(R0["inicio"]) < _publicacion_esperada(t):
                dd = None          # CompararOi: sin la publicacion de OI del dia (desde las 09:30 NY)
        if self.libro == "NQ":
            g = G2.gamma76(S, K, T, fo["iv"][ok])
            c = fo["call"][ok]
            a_o = g * fo["oi"][ok] * esc; a_v = g * fo["vol"][ok] * esc
            avc = np.where(c, a_v, 0.0); avp = np.where(c, 0.0, -a_v)
            aoc = np.where(c, a_o, 0.0); aop = np.where(c, 0.0, -a_o)
            if dd is not None:
                a_d = g * dd[ok] * esc
                dgc = np.where(c, a_d, 0.0); dgp = np.where(c, 0.0, a_d)
            else:
                dgc = dgp = np.full(len(K), np.nan)
        else:
            gc = G2.gamma_bs(S, K, T, fo["ivc"][ok]); gp = G2.gamma_bs(S, K, T, fo["ivp"][ok])
            avc = gc * fo["volc"][ok] * esc; avp = -gp * fo["volp"][ok] * esc
            aoc = gc * fo["oic"][ok] * esc; aop = -gp * fo["oip"][ok] * esc
            if dd is not None:
                dgc = gc * dd[0][ok] * esc; dgp = gp * dd[1][ok] * esc     # dGP > 0 = crecio el OI de puts
            else:
                dgc = dgp = np.full(len(K), np.nan)
        aporta = (avc != 0) | (avp != 0) | (aoc != 0) | (aop != 0)
        if not aporta.any():
            return None
        ks, inv = np.unique(K[aporta], return_inverse=True)
        n = len(ks)
        s = lambda x: np.bincount(inv, x[aporta], n)
        Fut = ks * v if self.libro == "QQQ" else ks + v
        m = np.abs(Fut - F) <= F * FILTRO
        if not m.any():
            return None
        r = dict(K=ks[m], Fut=Fut[m], GvC=s(avc)[m], GvP=s(avp)[m], GoC=s(aoc)[m], GoP=s(aop)[m], S=S, conv=v, foto=k,
                 dGC=s(dgc)[m] if dd is not None else np.full(int(m.sum()), np.nan),
                 dGP=s(dgp)[m] if dd is not None else np.full(int(m.sum()), np.nan))
        r["gv"] = r["GvC"] + r["GvP"]; r["go"] = r["GoC"] + r["GoP"]
        return r


FERIADOS = {"2026-09-07", "2026-11-26", "2026-12-25"}


def _fecha_ny(t):
    return D.a_ny(t).strftime("%Y-%m-%d")


def _publicacion_esperada(t):
    """CambiosFamilia.PublicacionCboeEsperada(t, 09:30): fecha habil de NY cuya publicacion de OI ya tendria que estar vigente."""
    ny = D.a_ny(t)
    d = ny.normalize() if (ny.hour * 60 + ny.minute) >= 570 else ny.normalize() - pd.Timedelta(days=1)
    for _ in range(15):
        if d.weekday() >= 5 or d.strftime("%Y-%m-%d") in FERIADOS:
            d = d - pd.Timedelta(days=1)
        else:
            break
    return d.strftime("%Y-%m-%d")


# ================================================================================================ formulas (ReglasFam.cs)
def _R(F):
    return min(F * 0.02, 100.0)


def muros(Fut, C, P, F):
    """[(precio, 'D1', i), (precio, 'D2', j)]: D1 = argmax C > 0, D2 = argmin P < 0 en R (primera ocurrencia en orden de Fut)."""
    r = _R(F); out = []
    en = np.abs(Fut - F) <= r
    ii = np.nonzero(en & (C > 0))[0]
    if len(ii):
        i = ii[np.argmax(C[ii])]; out.append((float(Fut[i]), "D1", int(i)))
    jj = np.nonzero(en & (P < 0))[0]
    if len(jj):
        j = jj[np.argmin(P[jj])]; out.append((float(Fut[j]), "D2", int(j)))
    return out


def cruces(Fut, g):
    nz = np.nonzero(g != 0)[0]
    if len(nz) < 2:
        return []
    if len(nz) >= 3:
        gg = g[nz]; sg = np.sign(gg); a = np.abs(gg)
        mid = (sg[1:-1] != sg[:-2]) & (sg[1:-1] != sg[2:]) & (a[1:-1] < ISLA * np.minimum(a[:-2], a[2:]))
        keep = np.ones(len(nz), bool); keep[1:-1] = ~mid
        nz = nz[keep]
    out = []
    for i0, i1 in zip(nz[:-1], nz[1:]):
        if g[i0] * g[i1] < 0:
            out.append(float(Fut[i0] + (Fut[i1] - Fut[i0]) * (-g[i0]) / (g[i1] - g[i0])))
    return out


def ztp(Fut, g, F):
    zs = [z for z in cruces(Fut, g) if abs(z - F) <= RADIO]
    ar = [z for z in zs if z > F]; ab = [z for z in zs if z < F]
    return ([(min(ar), "D1")] if ar else []) + ([(max(ab), "D2")] if ab else [])


def pozo(b, oi, F):
    C, P, g = (b["GoC"], b["GoP"], b["go"]) if oi else (b["GvC"], b["GvP"], b["gv"])
    ps = [p for p, _, _ in muros(b["Fut"], C, P, F)]
    ps += [p for p, _, _ in muros(b["Fut"], g, g, F)]
    ps += [p for p, _ in ztp(b["Fut"], g, F)]
    return ps


def confluencia(pools, F):
    items = sorted((p, lb) for lb, ps in pools.items() for p in ps)
    out = []; i = 0
    while i < len(items):
        j = i
        while j + 1 < len(items) and items[j + 1][0] - items[i][0] <= CONF_PTS:
            j += 1
        grupo = items[i:j + 1]
        libs = sorted({lb for _, lb in grupo})
        if len(libs) >= 2:
            p = float(np.mean([x for x, _ in grupo]))
            if abs(p - F) <= RADIO:
                out.append((p, "D%d" % (len(out) + 1), "+".join(libs)))
            i = j + 1
        else:
            i += 1
    return out


def fusion(bk):
    """perfil sumado (NQ, NDX, QQQ en ese orden) en baldes de 5 pts; los perfiles ya traen el multiplicador del libro."""
    Fut = np.concatenate([np.round(bk[lb]["Fut"] / FAM_GRILLA) * FAM_GRILLA for lb in LIBROS])
    ks, inv = np.unique(Fut, return_inverse=True)
    f = {"Fut": ks}
    for c in ("GvC", "GvP", "GoC", "GoP", "dGC", "dGP"):
        f[c] = np.bincount(inv, np.concatenate([bk[lb][c] for lb in LIBROS]), len(ks))
    return f


# ================================================================================================ una sesion
def sesion(dia):
    t0 = time.time()
    g = EV.minutos_y_precio(dia)
    libs = {lb: Libro(lb, dia) for lb in LIBROS}
    libs = {lb: L for lb, L in libs.items() if L.ok}
    viejo_hasta, como = None, "sin NQ"
    if "NQ" in libs:
        class _L:
            pass
        o = _L(); o.fot = D.fotos("NQ", dia)
        viejo_hasta, como = G2.oi_nq_viejo_hasta(dia, o)
    ini_noche = pd.Timestamp(dia) - pd.Timedelta(hours=2)
    series = {s: {} for s in SERIES}
    grilla = []
    diag = {"dia": dia, "libros": sorted(libs), "oi_nq_viejo_hasta": str(viejo_hasta), "oi_nq_como": como,
            "saltos_oi": {lb: L.saltos for lb, L in libs.items()}, "min_libro": {lb: 0 for lb in libs}, "min_fam": 0,
            "min_fam_oi": 0, "min_fam_doi": 0, "doi_nan_min": {lb: 0 for lb in libs}}
    for i, (t, F) in enumerate(zip(g["t"], g["F"].to_numpy(float))):
        if not np.isfinite(F):
            continue
        grilla.append(t)
        for s in SERIES:
            series[s][t] = {}
        bk = {}
        for lb, L in libs.items():
            if (L.t_cv[i] != t):
                raise AssertionError("conv_min desalineado %s %s" % (lb, t))
            b = L.minuto(i, t, F)
            if b is not None:
                bk[lb] = b
                diag["min_libro"][lb] += 1
                if not np.isfinite(b["dGC"]).all():
                    diag["doi_nan_min"][lb] += 1
        if not bk:
            continue
        oi_ok = {lb: (lb != "NQ" or not (viejo_hasta is not None and ini_noche <= t < viejo_hasta)) for lb in bk}
        # ---- confluencias
        pv = {lb: pozo(b, False, F) for lb, b in bk.items()}
        c = confluencia(pv, F)
        series["CONF_vol"][t] = {e: (p, None) for p, e, _ in c}
        if "NDX" in bk and "NQ" in bk:
            c = confluencia({lb: pv[lb] for lb in ("NDX", "NQ")}, F)
            series["CONF_NDX_NQ_vol"][t] = {e: (p, None) for p, e, _ in c}
        pt = {lb: pv[lb] + (pozo(b, True, F) if oi_ok[lb] else []) for lb, b in bk.items()}
        c = confluencia(pt, F)
        series["CONF_todo"][t] = {e: (p, None) for p, e, _ in c}
        # ---- muros por OI de cada libro (K0, K4b)
        for lb, b in bk.items():
            if not oi_ok[lb]:
                continue
            for p, e, j in muros(b["Fut"], b["GoC"], b["GoP"], F):
                et = "%s%g" % (lb, b["K"][j])
                series["MUROS_oi_UNION"][t]["%s.%s" % (lb, e)] = (p, et)
                d = b["dGC"][j] if e == "D1" else b["dGP"][j]
                if d == d and d > 0:
                    series["MUROS_oi_DOI"][t]["%s.%s" % (lb, e)] = (p, et)
        # ---- familia sumada
        if all(lb in bk for lb in LIBROS):
            diag["min_fam"] += 1
            f = fusion(bk)
            for p, e, j in muros(f["Fut"], f["GvC"], f["GvP"], F):
                series["FAM_MUROS_vol"][t][e] = (p, None)
                top = (f["GvC"][j] >= f["GvC"].max()) if e == "D1" else (f["GvP"][j] <= f["GvP"].min())
                if top:
                    series["FAM_MUROS_vol_TOP"][t][e] = (p, None)
            if all(oi_ok[lb] for lb in LIBROS):
                diag["min_fam_oi"] += 1
                doi_ok = all(np.isfinite(bk[lb]["dGC"]).all() for lb in LIBROS)
                diag["min_fam_doi"] += int(doi_ok)
                for p, e, j in muros(f["Fut"], f["GoC"], f["GoP"], F):
                    series["FAM_MUROS_oi"][t][e] = (p, None)
                    top = (f["GoC"][j] >= f["GoC"].max()) if e == "D1" else (f["GoP"][j] <= f["GoP"].min())
                    if top:
                        series["FAM_MUROS_oi_TOP"][t][e] = (p, None)
                    if doi_ok:
                        d = f["dGC"][j] if e == "D1" else f["dGP"][j]
                        if d > 0:
                            series["FAM_MUROS_oi_DOI"][t][e] = (p, None)
    diag["minutos_grilla"] = len(grilla)
    diag["minutos_con_nivel"] = {s: sum(1 for v in series[s].values() if v) for s in SERIES}
    diag["segundos"] = round(time.time() - t0, 1)
    return {"grilla": grilla, "series": series, "diag": diag}


def _uno(dia):
    prioridad_baja()
    os.makedirs(SALIDA, exist_ok=True)
    try:
        r = sesion(dia)
    except Exception as e:
        import traceback
        return dia, {"error": "%s\n%s" % (e, traceback.format_exc())}
    pd.to_pickle(r, os.path.join(SALIDA, "%s.pkl" % dia))
    return dia, r["diag"]


def todas_las_sesiones():
    s = set()
    for lb in LIBROS:
        s |= set(D.dias(lb))
    return sorted(s)


def main():
    dias = sys.argv[1:] or todas_las_sesiones()
    os.makedirs(SALIDA, exist_ok=True)
    t0 = time.time()
    diags = {}
    if len(dias) == 1:
        d, dg = _uno(dias[0]); diags[d] = dg
        print(json.dumps(dg, default=str)[:3000])
    else:
        from multiprocessing import Pool
        with Pool(int(os.environ.get("N_PROC", "3")), initializer=prioridad_baja) as pool:
            for d, dg in pool.imap_unordered(_uno, dias):
                diags[d] = dg
                if "error" in dg:
                    print(d, "ERROR", dg["error"][:2000], flush=True)
                else:
                    print(d, "%.0fs" % dg["segundos"], dg["libros"], dg["minutos_con_nivel"], "saltos", {k: len(v) for k, v in dg["saltos_oi"].items()},
                          "%.0f s" % (time.time() - t0), flush=True)
    viejo = {}
    p = os.path.join(AQUI, "datos", "rec_diag.json")
    if os.path.exists(p):
        viejo = json.load(open(p, encoding="utf-8"))
    viejo.update(diags)
    with open(p, "w", encoding="utf-8") as fh:
        json.dump(viejo, fh, ensure_ascii=False, indent=1, default=str)
    print("listo %.0f s" % (time.time() - t0))


if __name__ == "__main__":
    main()
