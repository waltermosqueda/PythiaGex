# -*- coding: utf-8 -*-
"""AUDITORIA DE LOS VENCIMIENTOS (0DTE) Y DE LAS BARRAS PESADAS, CON LA CADENA CRUDA.

Pregunta del operador (2026-09-08): los "0DTE" que se ven en pantalla, ¿estan
bien calculados y son honestos en tiempo real? ¿Las barras grandes cercanas al
precio son tambien 0DTE, o solo las marcadas?

Lo que hace, sin pasar por el indicador:
  1. Toma la ULTIMA cadena archivada de la raiz (ES = libro SPX, NQ = libro NDX)
     y rehace a mano los dias al vencimiento: vencimiento a las 16:00 de Nueva
     York (20:00 UTC en horario de verano) menos la hora de la cadena. Los compara
     con los "dias" que trae la cadena.
  2. Rehace el GEX por strike y POR VENCIMIENTO con la misma formula del nucleo
     (Black-Scholes, +call -put, x100 x S^2 x 1 %), en precio del futuro (base
     de la cadena), y lista las barras mas pesadas arriba y abajo del precio con
     cuanto viene del vencimiento de hoy y cuanto de los siguientes.
  3. Si hay una bajada directa de CBOE (cboe_<raiz>.json, delayed quotes), rehace
     el GEX con la GAMMA QUE PUBLICA CBOE en vez de la nuestra, y compara el OI de
     esos strikes: dos calculos independientes sobre el mismo dato crudo.

Uso:  python laboratorio/auditar_vencimientos.py ES [--cboe scratchpad/cboe_SPX.json] [--precio 7699.9]
      python laboratorio/auditar_vencimientos.py NQ [--cboe scratchpad/cboe_NDX.json] [--precio 29576.5]
"""
import gzip
import io
import json
import math
import os
import re
import sys
from datetime import datetime, timedelta, timezone

CARPETA = os.path.join(os.environ.get("APPDATA", ""), "ATAS", "PythiaGex", "cadenas")
MULT = 100.0
TASA = 0.045
PISO_DIAS = 1.0 / 1440.0


def fi(x):
    return math.exp(-0.5 * x * x) / math.sqrt(2 * math.pi)


def gamma_bs(S, K, T, iv, r):
    if iv <= 0 or T <= 0 or S <= 0 or K <= 0:
        return 0.0
    v = iv * math.sqrt(T)
    d1 = (math.log(S / K) + (r + 0.5 * iv * iv) * T) / v
    return fi(d1) / (S * v)


def ultima_cadena(raiz):
    fs = sorted(f for f in os.listdir(CARPETA) if f.startswith("cadena-%s-" % raiz) and f.endswith(".jsonl.gz"))
    if not fs:
        return None, None
    p = os.path.join(CARPETA, fs[-1])
    ult = None
    with gzip.open(p, "rt", encoding="utf-8") as f:
        for l in f:
            if len(l) > 100:
                ult = l
    return json.loads(ult), p


def fmt(v):
    a = abs(v)
    return ("%+.2fB" % (v / 1e9)) if a >= 1e9 else ("%+.0fM" % (v / 1e6)) if a >= 1e6 else ("%+.0fk" % (v / 1e3))


def main():
    a = [x for x in sys.argv[1:] if not x.startswith("--")]
    raiz = a[0] if a else "ES"
    arg = lambda k, d: type(d)(sys.argv[sys.argv.index(k) + 1]) if k in sys.argv else d
    cboe = arg("--cboe", "")
    precio = arg("--precio", 0.0)
    d, p = ultima_cadena(raiz)
    if not d:
        print("sin cadena de", raiz); return
    c = d["cadena"]
    ts = datetime.strptime(c["ts"], "%Y-%m-%d %H:%M:%S").replace(tzinfo=timezone.utc)
    S = float(c["spot_idx"])
    base = d["base"] if d.get("base_confiable") and d.get("base") else (d.get("base_cruda") or 0.0)
    print("== %s: %s | cadena %s UTC (generada %s, edad %s min) | spot indice %.2f | base %s%.2f" % (
        raiz, os.path.basename(p), c["ts"], d.get("generado"), d.get("edad_min"), S,
        "medida " if d.get("base_confiable") else "CRUDA ", base))
    vencs = c["vencimientos"]
    print()
    print("1) DIAS AL VENCIMIENTO: cadena contra cuenta manual (vence 16:00 Nueva York = 20:00 UTC)")
    for i, v in enumerate(vencs):
        f = datetime.strptime(v["f"], "%Y-%m-%d").replace(tzinfo=timezone.utc)
        venc_utc = f + timedelta(hours=20)
        manual = (venc_utc - ts).total_seconds() / 86400.0
        dif_s = (v["dias"] - manual) * 86400.0
        marca = "  <-- HOY (0DTE)" if v["dias"] < 1.0 else ""
        print("   venc %d %s  cadena %.4f d | manual %.4f d | diferencia %+.0f s%s" % (i, v["f"], v["dias"], manual, dif_s, marca))
    mas_cerca = min(v["dias"] for v in vencs if v["dias"] >= 0)
    print("   ahora es %s UTC: al vencimiento de hoy le quedan %.2f h desde la cadena y %.2f h desde ahora" % (
        datetime.now(timezone.utc).strftime("%H:%M:%S"), mas_cerca * 24, (ts + timedelta(days=mas_cerca) - datetime.now(timezone.utc)).total_seconds() / 3600))

    # 2) GEX por strike y por vencimiento, con la formula del nucleo
    campos = c["campos"].split(",")
    ix = {k: i for i, k in enumerate(campos)}
    por = {}
    for f in c["filas"]:
        K, vi = float(f[ix["strike"]]), int(f[ix["venc"]])
        dias = vencs[vi]["dias"]
        if dias < 0:
            continue
        T = max(dias, PISO_DIAS) / 365.0
        ivc, ivp = float(f[ix["iv_call"]]), float(f[ix["iv_put"]])
        oic, oip = float(f[ix["oi_call"]]), float(f[ix["oi_put"]])
        vc, vp = float(f[ix["vol_call"]]), float(f[ix["vol_put"]])
        gc, gp = gamma_bs(S, K, T, ivc, TASA), gamma_bs(S, K, T, ivp, TASA)
        gvol = (gc * vc - gp * vp) * MULT * S * S * 0.01
        goi = (gc * oic - gp * oip) * MULT * S * S * 0.01
        s = por.setdefault(K, {"K": K, "fut": K + base, "vol": {}, "oi": {}, "OI": 0.0, "V": 0.0})
        s["vol"][vi] = s["vol"].get(vi, 0.0) + gvol
        s["oi"][vi] = s["oi"].get(vi, 0.0) + goi
        s["OI"] += oic + oip; s["V"] += vc + vp
    hoy = [i for i, v in enumerate(vencs) if 0 <= v["dias"] <= max(1.0, mas_cerca + 0.01)]
    semana = [i for i, v in enumerate(vencs) if 0 <= v["dias"] <= max(7.0, mas_cerca + 0.01)]
    ref = precio if precio > 0 else S + base
    print()
    print("2) BARRAS PESADAS CERCA DEL PRECIO %.2f (futuro): GEX por volumen del dia, en precio del futuro" % ref)
    print("   Horizonte=Hoy usa SOLO los vencimientos %s; 'semana' sumaria los %s" % (hoy, semana))
    print("   %9s %9s | %10s %10s %6s | %10s | %8s %8s | %s" % ("fut", "strike", "GEX hoy", "GEX 7d", "hoy%", "GEX OI hoy", "OI", "vol", "vence"))
    filas = list(por.values())
    def tabla(lado):
        cand = [s for s in filas if (s["fut"] > ref) == lado and abs(s["fut"] - ref) <= ref * 0.006]
        cand.sort(key=lambda s: -abs(sum(s["vol"].get(i, 0.0) for i in hoy)))
        for s in cand[:5]:
            g_hoy = sum(s["vol"].get(i, 0.0) for i in hoy)
            g_7 = sum(s["vol"].get(i, 0.0) for i in semana)
            share = 100.0 * g_hoy / g_7 if g_7 else float("nan")
            oi_hoy = sum(s["oi"].get(i, 0.0) for i in hoy)
            vive = sorted(i for i in s["vol"] if s["vol"][i] != 0)
            print("   %9.2f %9.0f | %10s %10s %5.0f%% | %10s | %8.0f %8.0f | venc %s" % (
                s["fut"], s["K"], fmt(g_hoy), fmt(g_7), share, fmt(oi_hoy), s["OI"], s["V"], ",".join(str(i) for i in vive)))
    print("   ARRIBA del precio (las 5 mas pesadas por GEX hoy):"); tabla(True)
    print("   ABAJO del precio:"); tabla(False)

    # 3) contra la gamma que publica CBOE (bajada directa)
    if cboe and os.path.exists(cboe):
        j = json.load(io.open(cboe, encoding="utf-8"))
        dd = j.get("data", j)
        print()
        print("3) CONTRA CBOE DIRECTO (%s, timestamp %s, indice %.2f): GEX del 0DTE con la GAMMA DE CBOE y OI cotejado" % (
            os.path.basename(cboe), j.get("timestamp"), float(dd.get("current_price") or 0)))
        hoy_f = vencs[hoy[0]]["f"].replace("-", "")[2:]
        agg = {}
        for o in dd.get("options", []):
            m = re.search(r"(\d{6})([CP])(\d{8})", o["option"])
            if not m or m.group(1) != hoy_f:
                continue
            K = int(m.group(3)) / 1000.0
            gam = float(o.get("gamma") or 0)
            vol = float(o.get("volume") or 0); oi = float(o.get("open_interest") or 0)
            sgn = 1.0 if m.group(2) == "C" else -1.0
            s = agg.setdefault(K, {"gvol": 0.0, "OI": 0.0, "V": 0.0})
            s["gvol"] += sgn * gam * vol * MULT * S * S * 0.01
            s["OI"] += oi; s["V"] += vol
        print("   %9s %9s | %12s %12s | %8s %8s | %s" % ("fut", "strike", "GEX nuestro", "GEX CBOE-g", "OI arch", "OI CBOE", "nota"))
        for s in sorted(filas, key=lambda s: -abs(sum(s["vol"].get(i, 0.0) for i in hoy))):
            if abs(s["fut"] - ref) > ref * 0.006:
                continue
            g_n = sum(s["vol"].get(i, 0.0) for i in hoy)
            oi_hoy = sum(1 for _ in [0])
            cb = agg.get(s["K"])
            if cb is None:
                print("   %9.2f %9.0f | %12s %12s | %8.0f %8s | no esta en CBOE hoy" % (s["fut"], s["K"], fmt(g_n), "--", s["OI"], "--")); continue
            # el OI del archivo suma todos los vencimientos del strike; el de CBOE es solo hoy
            oi_arch_hoy = 0.0
            for f in c["filas"]:
                if float(f[ix["strike"]]) == s["K"] and int(f[ix["venc"]]) in hoy:
                    oi_arch_hoy += float(f[ix["oi_call"]]) + float(f[ix["oi_put"]])
            nota = "" if abs(g_n) == 0 or abs(cb["gvol"] - g_n) / max(1.0, abs(g_n)) < 0.25 else "DIFIEREN > 25 % (volumen creció o gamma distinta)"
            print("   %9.2f %9.0f | %12s %12s | %8.0f %8.0f | %s" % (s["fut"], s["K"], fmt(g_n), fmt(cb["gvol"]), oi_arch_hoy, cb["OI"], nota))
            if s["fut"] == ref:
                pass
        print("   (el volumen de CBOE es de %s y el del archivo de %s: la diferencia de GEX por volumen es en parte tiempo)" % (j.get("timestamp"), c["ts"]))
    print()
    print("LECTURA: con Horizonte=Hoy el perfil entero es 0DTE por construccion (solo entra el vencimiento de hoy);")
    print("una barra sin rotulo 0DTE no es 'de otra fecha', es otro rotulo (la convexidad, a la izquierda de la escalera).")


if __name__ == "__main__":
    main()
