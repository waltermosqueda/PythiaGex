# -*- coding: utf-8 -*-
"""esc_02_causalidad.py — ESCEPTICO 1: busca FUGA DE FUTURO en las rayas de las cuatro familias. SOLO LECTURA.
(1) PRECIO: el 'fut' de cada clave t tiene que ser el cierre de la ultima m2 CERRADA (t par -> vela t-1; t impar -> vela t-2), nunca el
    de la vela t (en formacion). Se mide en NDX (meta), NQ (registros) y QQQ (meta) contra las velas que usaron.
(2) FOTO: la foto usada en t tiene que tener disponibilidad <= t (NDX gen, NQ foto_ts, QQQ foto).
(3) FIRMA DE FUGA EN LAS RAYAS: en los minutos en que una raya CAMBIA (|delta| >= 0,5 pts) se mira si el signo del cambio coincide con
    el signo de la vela donde esa raya pasa a estar vigente (vela t + desfase: NO la conoce) y con el de la vela anterior (si la conoce).
    Sin fuga, la coincidencia con la vela vigente tiene que ser ~50 % (la autocorrelacion de 1 min del MNQ es ~0); una raya que mira
    adelante la empuja arriba. Se compara con un control CON fuga a proposito (la misma serie leida 1 minuto antes: desfase - 1).
Escribe datos/causalidad.json y datos/causalidad.txt. Uso: python -I esc_02_causalidad.py"""
import ctypes
try:
    ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)
except Exception:
    pass
import glob
import json
import math
import os
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import numpy as np  # noqa: E402
import pandas as pd  # noqa: E402
import esc_comun as E  # noqa: E402

OUT = os.path.join(AQUI, "datos")
LOG = []


def out(s):
    print(s, flush=True); LOG.append(s)


def mapa_velas(V):
    V = V.copy(); V["t"] = pd.to_datetime(V["t"]).dt.floor("min")
    V = V.drop_duplicates("t", keep="last")
    tm = V["t"].to_numpy().astype("datetime64[m]").astype(np.int64)
    return dict(zip(tm.tolist(), zip(V["o"].astype(float), V["c"].astype(float))))


def chequeo_fut(nombre, ts, fut, MV):
    """ts: minutos enteros; fut: precio usado. Esperado = cierre de la ultima m2 cerrada (con su M1 final presente o la par)."""
    ok = mal_actual = sin_ref = otra = 0
    ej = []
    for k, f in zip(ts, fut):
        if not (f == f):
            continue
        fin = k - (k % 2)                      # la m2 que cerro en 'fin' (<= k) cubre [fin-2, fin)
        esper = None
        for kk in (fin - 1, fin - 2):           # su M1 final; si falta, la par
            if kk in MV:
                esper = MV[kk][1]; break
        cur = MV.get(k)
        if esper is None:
            sin_ref += 1; continue
        if abs(f - esper) < 0.01:
            ok += 1
        elif cur is not None and abs(f - cur[1]) < 0.01:
            mal_actual += 1
            if len(ej) < 5:
                ej.append(str(pd.Timestamp(k * 60, unit="s")))
        else:
            otra += 1
    tot = ok + mal_actual + otra
    out("FUT %-6s minutos %6d | = cierre de la ultima m2 cerrada %6d (%.2f %%) | = cierre de la vela EN CURSO (fuga) %d | otro %d | sin vela ref %d %s" % (
        nombre, tot, ok, 100.0 * ok / tot if tot else float("nan"), mal_actual, otra, sin_ref, ej))
    return dict(minutos=tot, ok=ok, fuga_vela_en_curso=mal_actual, otro=otra, sin_ref=sin_ref, ejemplos=ej)


def firma(nombre, rayas, desfase, MV, claves=None):
    """Signo del cambio de cada raya (por clave/rol) contra la vela vigente (t + desfase) y la anterior (t + desfase - 1)."""
    items = sorted(((E._min(k), v) for k, v in rayas.items()), key=lambda z: z[0])
    prev = {}
    a_vig = a_ant = a_fuga = n = 0
    for k, v in items:
        cur = {}
        for nom, x in (v or {}).items():
            if claves and nom not in claves:
                continue
            p = x[0] if isinstance(x, (tuple, list)) else x
            try:
                p = float(p)
            except (TypeError, ValueError):
                continue
            if p == p and p != 0:
                cur[nom] = p
        for nom, p in cur.items():
            q = prev.get(nom)
            if q is None or q[0] != k - 1:
                continue
            d = p - q[1]
            if abs(d) < 0.5:
                continue
            vig = MV.get(k + desfase); ant = MV.get(k + desfase - 1); fug = MV.get(k + desfase + 1)
            if vig is None or ant is None or fug is None:
                continue
            mv, ma, mf = vig[1] - vig[0], ant[1] - ant[0], fug[1] - fug[0]
            if mv == 0 or ma == 0 or mf == 0:
                continue
            n += 1
            a_vig += (np.sign(d) == np.sign(mv)); a_ant += (np.sign(d) == np.sign(ma)); a_fuga += (np.sign(d) == np.sign(mf))
        prev = {nom: (k, p) for nom, p in cur.items()}
        # las claves ausentes cortan la continuidad
    if n == 0:
        out("FIRMA %-28s sin cambios" % nombre)
        return dict(n=0)
    se = 50.0 / math.sqrt(n)
    r = dict(n=n, coincide_vela_vigente=100.0 * a_vig / n, coincide_vela_anterior=100.0 * a_ant / n,
             coincide_vela_siguiente=100.0 * a_fuga / n, error_std_pp=se)
    out("FIRMA %-28s cambios %6d | signo = vela VIGENTE (no la conoce) %5.1f %% | = vela anterior (la conoce) %5.1f %% | = vela siguiente %5.1f %% | 1 e.e. = %.1f pp" % (
        nombre, n, r["coincide_vela_vigente"], r["coincide_vela_anterior"], r["coincide_vela_siguiente"], se))
    return r


def main():
    R = {"fut": {}, "foto": {}, "firma": {}}
    # ------------------------------------------------ NDX
    V, S = E.ndx_cargar()
    MV = mapa_velas(V)
    metas = pd.concat([pd.read_pickle(p)["meta"] for p in sorted(glob.glob(os.path.join(E.NDX, "datos", "series", "ndx_*.pkl")))], ignore_index=True)
    ts = (metas["t"].astype("int64") // 60_000_000_000).to_numpy()
    R["fut"]["NDX"] = chequeo_fut("NDX", ts, metas["fut"].to_numpy(float), MV)
    g = metas.dropna(subset=["gen"])
    R["foto"]["NDX"] = dict(minutos=len(g), gen_mayor_que_t=int((g["gen"] > g["t"]).sum()))
    out("FOTO NDX: %d minutos con foto, generado > t en %d" % (len(g), R["foto"]["NDX"]["gen_mayor_que_t"]))
    for s in ("MUROS_NDX_vol", "MUROS_NDX_oi", "MAJORS_NDX_vol", "MAJORS_NDX_oi", "ZTP_NDX_vol", "ZEST_NDX_vol", "DOMS_NDX_vol",
              "R20_NDX_vol"):
        R["firma"][s] = firma(s, S[s], 0, MV)
    # ------------------------------------------------ NQ
    V, reg, part = E.nq_cargar()
    MV = mapa_velas(V)
    ks, fu, nf, nmal = [], [], 0, 0
    rayas_nq = {}
    for d, regs in reg.items():
        for r in regs:
            ks.append(r["k"]); fu.append(r["fut"])
            if r.get("foto_ts"):
                nf += 1
                if pd.Timestamp(r["foto_ts"]) > pd.Timestamp(r["k"] * 60, unit="s"):
                    nmal += 1
            for base, lst in r["series"].items():
                rayas_nq.setdefault(base, {})[pd.Timestamp(r["k"] * 60, unit="s")] = {"%s.%s" % (base, e): (p, None) for p, e, K in lst}
    R["fut"]["NQ"] = chequeo_fut("NQ", ks, fu, MV)
    R["foto"]["NQ"] = dict(minutos=nf, foto_ts_mayor_que_t=nmal)
    out("FOTO NQ: %d minutos con foto, foto_ts > t en %d" % (nf, nmal))
    for s in sorted(rayas_nq):
        R["firma"][s] = firma(s, rayas_nq[s], 0, MV)
    # ------------------------------------------------ QQQ
    q = pd.read_pickle(os.path.join(E.QQQ, "datos", "rayas_qqq20.pkl"))
    Vq = pd.read_csv(os.path.join(E.QQQ, "datos", "velas_snapshot.csv"), parse_dates=["t"])
    MV = mapa_velas(Vq)
    M = q["meta"]
    tsq = (M["t"].astype("int64") // 60_000_000_000).to_numpy()
    R["fut"]["QQQ"] = chequeo_fut("QQQ", tsq, M["fut"].to_numpy(float), MV)
    gq = M[M["foto"].notna()]
    try:
        mal = int((pd.to_datetime(gq["foto"]) > gq["t"]).sum())
    except Exception:
        mal = None
    R["foto"]["QQQ"] = dict(minutos=len(gq), foto_mayor_que_t=mal)
    out("FOTO QQQ: %d minutos con foto, foto > t en %s" % (len(gq), mal))
    for s, ray in q["series"].items():
        R["firma"][s] = firma(s, ray, q["desfase"][s], MV)
    # ------------------------------------------------ CONF (precio = cierre de la vela que abrio en t-1, arnes/evaluar: causal por codigo)
    Vc = pd.read_pickle(os.path.join(E.CONF, "datos", "velas_familia.pkl"))
    MV = mapa_velas(Vc)
    for s in ("FAM_MUROS_vol", "FAM_MUROS_oi", "CONF_vol", "CONF_NDX_NQ_vol", "CONF_todo", "FAM_MUROS_oi_TOP", "MUROS_oi_DOI"):
        _, Rr = E.conf_cargar(s)
        R["firma"][s] = firma(s, Rr, 0, MV)
    json.dump(R, open(os.path.join(OUT, "causalidad.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1, default=str)
    open(os.path.join(OUT, "causalidad.txt"), "w", encoding="utf-8").write("\n".join(LOG) + "\n")


if __name__ == "__main__":
    main()
