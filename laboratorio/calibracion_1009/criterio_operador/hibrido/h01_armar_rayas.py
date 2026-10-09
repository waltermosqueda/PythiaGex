# -*- coding: utf-8 -*-
"""h01_armar_rayas.py — CONJUNTO HIBRIDO: la SELECCION de la 2.0 con la CONVERSION de la 4.1 (09-10-2026). SOLO LECTURA de datos.

Seleccion (receta_2_0/extraer_paridad_2_0.py: calcular(), port 1:1 de GammaHoyNucleo.CalcularAdentro, paridad 137/138):
  QQQ, horizonte Hoy (vencimientos con dias-env en [0, max(1, mas_cerca+0,01)]; env = edad de la bajada en dias), GEX por VOLUMEN
  por strike (Black-Scholes de la 2.0: r 3,75 % dentro de d1, sin descuento), S = fut / escala, Fut = K x escala; candidatas a
  |Fut - fut| <= min(2 % de fut, 100) con |GEX vol| > 0; las 2 de mayor |GEX vol| (si no hay ninguna con volumen: por OI).
Conversion (escala):
  'h41'  = razon C8 de la 4.1: spot VIVO -> mediana robusta de las ultimas 24 muestras sincronizadas MNQ(ts-900 s)/spot (la
           reconstruccion de c08_noches_qqq_v2.py); spot CONGELADO -> la de noches.pkl (r41: la real de muestras-QQQ cuando existe,
           si no la reconstruida; la noche 11-09 no tiene r41: se usa la C8 reconstruida al congelarse).
  'h41c' = lo mismo con el escalon de sesion: -0,0077 en razon mientras la cadena esta congelada (calib_conversion.md, punto 4).
  'r20'  = REFERENCIA (no es mi conjunto): la misma seleccion con la razon de la 2.0 (noches.pkl r20), solo en la parte congelada.
Tiempo: rayas[t] = lo calculado al CERRAR la vela t (fut = cierre de t, cadena = ultima bajada con generado <= t+60 s, edad a t+60 s).
  El juez usa rayas[t-1] para la vela t: nunca mira adelante.
Congelada: [t_congela, t_descongela) por noche = la racha de bajadas con el spot_idx de la foto de noches.pkl (spot_c); empieza
  recien cuando el spot lleva >= 6 min igual (regla de la 4.1) y termina con la primera bajada de otro spot (pre-market).
  OJO (supuesto): la racha se identifica con el spot final (spot_c), que en vivo se sabe recien al repetirse: afecta solo el
  minuto exacto del escalon de h41c alrededor de 00:16-00:56Z. Ademas, desde 00:35Z (por reloj: QQQ deja el after-hours a las
  20:00 NY y CBOE llega 15 min tarde) se trata como congelada aunque la bajada que lo confirme llegue mas tarde (noches del
  archivo viejo de la 2.0 con bajadas cada ~hora: 15-09, 18-09, 23-09).
Escribe en esta carpeta: rayas_hibrido.pkl ({'h41': {t: {...}}, 'h41c': ..., 'r20': ..., 'meta': ...}) y h01_salida.txt (via stdout).
Correr: python -I h01_armar_rayas.py"""
import glob
import gzip
import json
import math
import os
import sys
from datetime import timedelta

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
CAL = os.path.join(os.environ["LOCALAPPDATA"], "Temp", "claude", "C--Users-wmx-7-OneDrive-Escritorio-ATAS-nada",
                   "f5879819-e2a4-405b-87b0-959ace090d7b", "scratchpad", "calibrar")
AP = os.path.join(os.environ["APPDATA"], "ATAS")
CARPETAS = [os.path.join(AP, "PythiaGex2", "cadenas"), os.path.join(AP, "PythiaGex4", "cboe"), os.path.join(AP, "PythiaGex", "cboe-local")]
R = 0.0375
MULT = 100.0
PISO_DIAS = 1.0 / 1440.0
ESCALON = 0.0077


# ------------------------------------------------------------------------------------------------ cadenas
def cargar_cadenas(intervalos):
    """Todas las bajadas de QQQ con generado dentro de algun intervalo [desde, hasta], dedup por cadena_ts (la mas temprana)."""
    desde = min(a for a, _ in intervalos); hasta = max(b for _, b in intervalos)
    ia = np.array([a.value for a, _ in intervalos]); ib = np.array([b.value for _, b in intervalos])
    por_ts = {}
    archivos = []
    for c in CARPETAS:
        archivos += glob.glob(os.path.join(c, "cadena-QQQ-*.jsonl.gz")) + glob.glob(os.path.join(c, "local-QQQ-*.jsonl"))
    for p in sorted(archivos):
        dia = os.path.basename(p).split("QQQ-")[1][:10]
        if not (desde - pd.Timedelta(days=2) <= pd.Timestamp(dia) <= hasta + pd.Timedelta(days=1)):
            continue
        op = gzip.open if p.endswith(".gz") else open
        try:
            fh = op(p, "rt", encoding="utf-8", errors="replace")
            for l in fh:
                l = l.strip()
                if not l:
                    continue
                try:
                    r = json.loads(l)
                except Exception:
                    continue
                c = r.get("cadena") or {}
                if not c.get("filas") or not c.get("vencimientos"):
                    continue
                gen = pd.Timestamp(r["generado"]).tz_convert("UTC").tz_localize(None)
                if not ((ia <= gen.value) & (gen.value <= ib)).any():
                    continue
                ts = c.get("ts") or r.get("cadena_ts")
                if ts in por_ts and por_ts[ts]["gen"] <= gen:
                    continue
                F = np.array([f[:8] for f in c["filas"] if len(f) >= 8], np.float64)
                por_ts[ts] = {"gen": gen, "ts": ts, "spot": float(c.get("spot_idx") or r.get("spot")), "ut": c.get("ultimo_trade"),
                              "dias": np.array([float(v.get("dias") or 0) for v in c["vencimientos"]]),
                              "fechas": [v["f"] for v in c["vencimientos"]], "F": F, "archivo": os.path.basename(p)}
            fh.close()
        except (OSError, EOFError) as e:
            print("ilegible", p, e)
    out = sorted(por_ts.values(), key=lambda x: x["gen"])
    return out


def gamma_bs(S, K, T, iv):
    ok = (K > 0) & (T > 0) & (iv > 0) & (S > 0)
    ivs = np.where(ok, iv, 1.0); Ks = np.where(ok, K, 1.0); Ts = np.where(ok, T, 1.0)
    v = ivs * np.sqrt(Ts)
    d1 = (np.log(S / Ks) + (R + 0.5 * ivs * ivs) * Ts) / v
    g = np.exp(-0.5 * d1 * d1) / math.sqrt(2.0 * math.pi) / (S * v)
    return np.where(ok, g, 0.0)


def dominantes(cad, fut, ahora, escala, cuantas=2):
    """La seleccion de la 2.0 (calcular() de la receta, solo la parte de dominantes). -> lista de (Fut, gex, K, libro)."""
    env = 0.0
    if ahora > cad["gen"]:
        env = min(2.0, (ahora - cad["gen"]).total_seconds() / 86400.0)
    dias = cad["dias"] - env
    pos = dias[dias >= 0]
    mas_cerca = float(pos.min()) if len(pos) else 0.0
    F = cad["F"]
    V = F[:, 1].astype(int)
    okv = (V >= 0) & (V < len(dias))
    d = np.where(okv, dias[np.clip(V, 0, len(dias) - 1)], -1.0)
    m = okv & (d >= 0) & (d <= max(1.0, mas_cerca + 0.01))
    if not m.any():
        return [], None
    F = F[m]; d = d[m]
    S = fut / escala
    T = np.maximum(d, PISO_DIAS) / 365.0
    K = F[:, 0]
    gC = gamma_bs(S, K, T, F[:, 4]); gP = gamma_bs(S, K, T, F[:, 5])
    esc = MULT * S * S * 0.01
    gv = (gC * F[:, 6] - gP * F[:, 7]) * esc
    go = (gC * F[:, 2] - gP * F[:, 3]) * esc
    nz = ~((go == 0) & (gv == 0))
    K, gv, go = K[nz], gv[nz], go[nz]
    ks, inv = np.unique(K, return_inverse=True)
    sv = np.bincount(inv, gv, len(ks)); so = np.bincount(inv, go, len(ks))
    Fut = ks * escala
    radio = min(fut * 2.0 / 100.0, 100.0)
    cerca = np.abs(Fut - fut) <= radio
    libro = "vol"
    c = np.flatnonzero(cerca & (np.abs(sv) > 0))
    g = sv
    if not len(c):
        libro = "OI"
        c = np.flatnonzero(cerca & (np.abs(so) > 0)); g = so
    if not len(c):
        return [], libro
    orden = c[np.argsort(-np.abs(g[c]), kind="stable")][:cuantas]
    return [(float(Fut[j]), float(g[j]), float(ks[j]), libro) for j in orden], libro


# ------------------------------------------------------------------------------------------------ C8 de la 4.1 (c08_noches_qqq_v2.py)
def serie_c8():
    sys.path.insert(0, CAL)
    import c04_comun as C  # noqa: E402  (Precio, cargar_cab, robusta, mediana, a_ny, tramo, ms)
    P = C.Precio()
    cab = C.cargar_cab("QQQ")
    for i, d in enumerate(cab):
        prev = cab[i - 1] if i else None
        d["vivo2"] = d["vivo"] or (prev is not None and (d["gen"] - prev["gen"]).total_seconds() <= 1800 and abs(prev["spot"] - d["spot"]) > 1e-9)
        d["tsp"] = d["ts"] - timedelta(seconds=900)
        d["tr"] = C.tramo(C.a_ny(d["tsp"]))
        d["fuera"] = (C.a_ny(d["gen"]).hour == 17)
        p, _ = P.en(C.ms(d["tsp"]), tol_s=180)
        d["m"] = p / d["spot"] if (d["vivo2"] and not math.isnan(p) and not d["fuera"] and d["tr"] in ("RTH", "AH", "PRE")) else float("nan")
    roll = []
    out = []
    for d in cab:
        if d["fuera"]:
            continue
        if not math.isnan(d["m"]):
            roll.append(d["m"]); roll = roll[-24:]
        v, n = C.robusta(roll, 0.0002 * (C.mediana(roll) if roll else 0))
        if n >= 5:
            out.append((pd.Timestamp(d["gen"]), v, n, d["vivo2"], d["spot"]))
    return out


def main():
    V = pd.read_csv(os.path.join(CAL, "velas_m1.csv"), parse_dates=["t"])
    V = V.drop_duplicates("t").sort_values("t").reset_index(drop=True)
    noches = pd.read_pickle(os.path.join(CAL, "noches.pkl"))
    v0, v1 = V["t"].iloc[0], V["t"].iloc[-1]
    print("velas M1 MNQZ6: %s -> %s (%d)" % (v0, v1, len(V)))

    c8 = serie_c8()
    c8_t = np.array([x[0].value for x in c8], np.int64)
    print("C8 reconstruida: %d puntos %s -> %s" % (len(c8), c8[0][0], c8[-1][0]))

    def c8_en(t):
        j = int(np.searchsorted(c8_t, t.value, side="right")) - 1
        return (c8[j][1], c8[j][0]) if j >= 0 else (float("nan"), None)

    # paridad de la C8 reconstruida contra la 4.1 real (muestras-QQQ: campo razon)
    difs = []
    for p in sorted(glob.glob(os.path.join(AP, "PythiaGex4", "familia", "muestras-QQQ-*.jsonl"))):
        for l in open(p, encoding="utf-8"):
            if not l.strip():
                continue
            x = json.loads(l)
            if not x.get("razon"):
                continue
            g = pd.Timestamp(x["generado"]).tz_convert("UTC").tz_localize(None) if "Z" in x["generado"] or "+" in x["generado"] else pd.Timestamp(x["generado"])
            r, _ = c8_en(g + pd.Timedelta(seconds=5))
            if r == r:
                difs.append((g, x["razon"], r, x.get("vivo")))
    if difs:
        dd = np.array([abs(a - b) for _, a, b, _ in difs])
        dv = np.array([abs(a - b) for _, a, b, v in difs if v])
        print("paridad C8 reconstruida vs 4.1 real (muestras-QQQ, %d muestras): |dif| mediana %.5f p90 %.5f max %.5f (en pts del strike 750: "
              "mediana %.2f p90 %.2f); solo con spot vivo (%d): mediana %.5f p90 %.5f" % (
                  len(dd), np.median(dd), np.percentile(dd, 90), dd.max(), 750 * np.median(dd), 750 * np.percentile(dd, 90), len(dv),
                  np.median(dv) if len(dv) else float("nan"), np.percentile(dv, 90) if len(dv) else float("nan")))

    rayas = {"h41": {}, "h41c": {}, "r20": {}}
    meta = []
    intervalos = []
    for n in noches:
        D = pd.Timestamp(n["D"]); N = pd.Timestamp(n["N"])
        a0 = D + pd.Timedelta(hours=20) if D.weekday() == 4 else N - pd.Timedelta(hours=10)
        intervalos.append((a0, N + pd.Timedelta(hours=13, minutes=32)))
    TODAS = cargar_cadenas(intervalos)
    print("bajadas QQQ cargadas (dedup por cadena_ts): %d" % len(TODAS))
    gen_todas = np.array([c["gen"].value for c in TODAS], np.int64)
    ivel = V.set_index("t")
    for n in noches:
        D = pd.Timestamp(n["D"]); N = pd.Timestamp(n["N"])
        a_full = N - pd.Timedelta(hours=2)                 # 22:00Z de la vispera (viernes: domingo 22:00Z)
        b_full = N + pd.Timedelta(hours=13, minutes=30)
        a_cong = a_full if D.weekday() == 4 else N + pd.Timedelta(minutes=35)
        b_cong = N + pd.Timedelta(hours=8, minutes=30)
        a0 = D + pd.Timedelta(hours=20) if D.weekday() == 4 else N - pd.Timedelta(hours=10)   # fin de semana: desde el viernes
        cads = [c for c in TODAS if a0 <= c["gen"] <= b_full + pd.Timedelta(minutes=2)]
        if not cads:
            print("sin cadenas", n["D"]); continue
        gens = np.array([c["gen"].value for c in cads], np.int64)
        # racha congelada: las bajadas con el spot de la foto, alrededor de la foto
        foto = pd.Timestamp(n["foto"])
        k0 = int(np.argmin([abs((pd.Timestamp(c["ts"]) - foto).total_seconds()) for c in cads]))
        sc = n["spot_c"]
        if abs(cads[k0]["spot"] - sc) > 1e-6:
            print("  OJO %s: la foto no tiene spot_c (%.2f vs %.2f)" % (n["D"], cads[k0]["spot"], sc))
        ka = k0
        while ka - 1 >= 0 and abs(cads[ka - 1]["spot"] - sc) < 1e-6:
            ka -= 1
        kb = k0
        while kb + 1 < len(cads) and abs(cads[kb + 1]["spot"] - sc) < 1e-6:
            kb += 1
        # regla de la 4.1: congelada recien cuando el spot lleva >= 6 min igual (primera bajada de la racha a >= 6 min de la primera)
        t_cong = next((cads[k]["gen"] for k in range(ka, kb + 1) if (cads[k]["gen"] - cads[ka]["gen"]) >= pd.Timedelta(minutes=6)),
                      cads[ka]["gen"] + pd.Timedelta(minutes=6))
        t_desc = cads[kb + 1]["gen"] if kb + 1 < len(cads) else pd.Timestamp.max
        r41 = n["r41"]
        r41_origen = "real" if n["r41_real"] == n["r41_real"] else "reconstruida c02"
        if not r41 == r41:
            r41, _ = c8_en(t_cong + pd.Timedelta(minutes=10)); r41_origen = "C8 reconstruida (c08) al congelarse"
        c8c, _ = c8_en(t_cong + pd.Timedelta(minutes=10))
        W = V[(V["t"] >= a_full - pd.Timedelta(minutes=1)) & (V["t"] < b_full)]
        nmin = 0; libros = {"vol": 0, "OI": 0, "": 0}; esc_vivas = []
        for t, c in zip(W["t"], W["c"]):
            fin = t + pd.Timedelta(seconds=60)
            j = int(np.searchsorted(gens, fin.value, side="right")) - 1
            if j < 0:
                continue
            cad = cads[j]
            cong = (t_cong <= fin or t >= a_cong) and fin < t_desc     # por reloj: desde 00:35Z QQQ ya no cotiza (causal)
            if cong:
                e41 = r41
            else:
                e41, _ = c8_en(fin)
                esc_vivas.append(e41)
            for nom, esc in (("h41", e41), ("h41c", e41 - ESCALON if cong else e41)):
                if not esc == esc:
                    continue
                ds, lib = dominantes(cad, float(c), fin, esc)
                libros[lib or ""] += nom == "h41"
                rayas[nom][t] = {"dom%d" % k: (x[0], "QQQ%g" % x[2]) for k, x in enumerate(ds)}
                rayas[nom][t]["_esc"] = esc; rayas[nom][t]["_cong"] = cong; rayas[nom][t]["_libro"] = lib
                rayas[nom][t]["_gex"] = [x[1] for x in ds]; rayas[nom][t]["_cad"] = cad["ts"]
            if cong and a_cong <= t < b_cong and n["r20"] == n["r20"]:
                ds, lib = dominantes(cad, float(c), fin, n["r20"])
                rayas["r20"][t] = {"dom%d" % k: (x[0], "QQQ%g" % x[2]) for k, x in enumerate(ds)}
            nmin += 1
        meta.append({"D": n["D"], "N": n["N"], "a_full": a_full, "b_full": b_full, "a_cong": a_cong, "b_cong": b_cong, "t_congela": t_cong,
                     "t_descongela": t_desc, "spot_c": sc, "r41": r41, "r41_origen": r41_origen, "c8_al_congelar": c8c, "r20": n["r20"],
                     "minutos": nmin, "libros": libros, "cadenas": len(cads),
                     "esc_viva_min": min(esc_vivas) if esc_vivas else None, "esc_viva_max": max(esc_vivas) if esc_vivas else None})
        print("%s->%s cadenas %4d | congelada %s -> %s (spot %.2f) | r41 %.4f (%s) C8 rec al congelar %.4f (dif %+.1f pts K750) | r20 %s | "
              "escala viva %s..%s | minutos %d libros %s" % (
                  n["D"], n["N"], len(cads), t_cong, t_desc if t_desc != pd.Timestamp.max else "-", sc, r41, r41_origen, c8c,
                  (c8c - r41) * 750, ("%.4f" % n["r20"]) if n["r20"] == n["r20"] else "-",
                  ("%.4f" % min(esc_vivas)) if esc_vivas else "-", ("%.4f" % max(esc_vivas)) if esc_vivas else "-", nmin, libros))

    # validacion de la seleccion: con la razon de la 2.0, contra lo que DIBUJO la 2.0 (niv20 dom0/dom1), en la parte congelada
    niv = pd.read_pickle(os.path.join(CAL, "niv20_m1.pkl"))
    for desfase in (0, 1):
        tot = igual = igual1 = 0
        difs = []
        for t, d in rayas["r20"].items():
            # rayas['r20'][t] usa el cierre de t; con desfase 1 comparamos contra lo dibujado en la vela t+1
            g = niv.get(t + pd.Timedelta(minutes=desfase)) or {}
            a = sorted(x for x in (g.get("dom0"), g.get("dom1")) if x)
            b = sorted(v[0] for k, v in d.items() if k.startswith("dom"))
            if not a or not b:
                continue
            tot += 1
            ok = len(a) == len(b) and all(abs(x - y) <= 0.05 for x, y in zip(a, b))
            igual += ok
            igual1 += any(abs(x - y) <= 0.05 for x in a for y in b)
            if not ok:
                difs.append((t, a, b))
        print("VALIDACION seleccion (razon 2.0, parte congelada) contra niv20 dom0/dom1, niv20 en t+%d: %d minutos, mismo par %d (%.1f %%), "
              "al menos una igual %d (%.1f %%)" % (desfase, tot, igual, 100.0 * igual / tot if tot else float("nan"), igual1,
                                                   100.0 * igual1 / tot if tot else float("nan")))
        for x in difs[:6]:
            print("    distinto:", x)
    pd.to_pickle({"h41": rayas["h41"], "h41c": rayas["h41c"], "r20": rayas["r20"], "meta": meta}, os.path.join(AQUI, "rayas_hibrido.pkl"))
    print("escrito rayas_hibrido.pkl: h41 %d minutos, h41c %d, r20 %d" % (len(rayas["h41"]), len(rayas["h41c"]), len(rayas["r20"])))


if __name__ == "__main__":
    main()
