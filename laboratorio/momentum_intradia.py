# -*- coding: utf-8 -*-
"""MOMENTUM INTRADIA DE LA ULTIMA MEDIA HORA (Baltussen, Da, Lammers y Martens, Journal of
Financial Economics 2021): el retorno de los ultimos 30 minutos se predice con el retorno del
resto del dia, y lo atribuyen a la cobertura de gamma de los market makers (cuando estan
cortos de gamma, cubren en la direccion del movimiento). Gao, Han, Li y Zhou (2018) lo
encontraron primero con la primera media hora del dia.

Aca se replica sobre NUESTROS futuros por minuto (Databento, junio-septiembre), y en los dias
con cadena se condiciona al regimen de gamma que el indicador anoto (cuadrante 2 y 4 =
convexidad negativa = "tobogan"). Es un sesgo de UNA vez por dia, no un gatillo de scalping,
pero es lo unico con respaldo cientifico publicado que tenemos a mano.

Uso: python laboratorio/momentum_intradia.py [NQ|ES] [--umbral 0.2]
"""
import io
import json
import math
import os
import sys

import pandas as pd

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ATAS = os.path.join(os.environ.get("APPDATA", ""), "ATAS")


def regimen_por_dia(inst):
    """Cuadrante que regia a las 19:30 UTC en cada dia con cadena (centinela del rebobinado)."""
    p = os.path.join(ATAS, "pythiagex-centinela-rebobinado-atas-%s-TimeFrame-M1.jsonl" % inst)
    q = {}
    if not os.path.exists(p):
        return q
    for l in io.open(p, encoding="utf-8", errors="replace"):
        try:
            d = json.loads(l)
        except Exception:
            continue
        if d["t"][11:16] == "19:30" and d.get("niv", {}).get("q_cuadrante"):
            q[d["t"][:10]] = d["niv"]["q_cuadrante"]
    return q


def main():
    raiz = sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith("--") else "NQ"
    umbral = float(sys.argv[sys.argv.index("--umbral") + 1]) if "--umbral" in sys.argv else 0.0
    micro = {"NQ": "MNQ", "ES": "MES"}[raiz]
    df = pd.read_csv(os.path.join(RAIZ, "datos", "simulador", "velas", "%sU6-1m.csv" % raiz))
    df["ts"] = pd.to_datetime(df["ts_event"], utc=True)
    df["dia"] = df["ts"].dt.strftime("%Y-%m-%d"); df["hm"] = df["ts"].dt.strftime("%H:%M")
    q = regimen_por_dia(micro)
    filas = []
    for dia, g in df.groupby("dia"):
        g = g.set_index("hm")
        need = ("13:30", "14:00", "19:30", "19:59")
        if not all(k in g.index for k in need):
            continue
        if g.loc["13:30":"19:59", "volume"].sum() < 20000:
            continue                     # contrato sin volumen (antes del roll de junio)
        o = g.loc["13:30", "open"]; c1 = g.loc["14:00", "close"]; c2 = g.loc["19:30", "close"]; cf = g.loc["19:59", "close"]
        filas.append(dict(dia=dia, r_resto=(c2 - o) / o * 100, r_primera=(c1 - o) / o * 100, r_ultima=(cf - c2) / c2 * 100, pts_ultima=cf - c2, q=q.get(dia)))
    d = pd.DataFrame(filas)
    print("%s: %d dias de rueda con volumen (%s a %s), umbral |r| >= %.2f %%" % (raiz, len(d), d["dia"].min(), d["dia"].max(), umbral))

    def prueba(nombre, pred, sub):
        s = sub[abs(sub[pred]) >= umbral]
        if len(s) < 5:
            print("  %-46s n=%d: pocos" % (nombre, len(s))); return
        acierto = (s[pred].apply(lambda x: 1 if x > 0 else -1) == s["r_ultima"].apply(lambda x: 1 if x > 0 else -1)).mean() * 100
        # retorno de la ultima media hora en la direccion del predictor (puntos)
        a_favor = (s["pts_ultima"] * s[pred].apply(lambda x: 1 if x > 0 else -1))
        t = a_favor.mean() / (a_favor.std(ddof=1) / math.sqrt(len(a_favor))) if len(a_favor) > 2 and a_favor.std(ddof=1) > 0 else float("nan")
        print("  %-46s n=%3d  acierto de signo %5.1f %%  | a favor: media %+6.1f pts, mediana %+6.1f, t = %+.2f" % (nombre, len(s), acierto, a_favor.mean(), a_favor.median(), t))

    print("  (moneda = 50 %; Baltussen y otros informan ~55-60 % en indices con datos de decadas)")
    prueba("resto del dia (9:30-15:30) -> ultimos 30 min", "r_resto", d)
    prueba("primera media hora -> ultimos 30 min", "r_primera", d)
    dq = d[d["q"].notna()]
    if len(dq):
        print("  con regimen de gamma (dias con cadena, %d):" % len(dq))
        prueba("   convexidad NEGATIVA (cuadrante 2/4): resto -> ultimos", "r_resto", dq[dq["q"].isin([2, 4])])
        prueba("   convexidad POSITIVA (cuadrante 1/3): resto -> ultimos", "r_resto", dq[dq["q"].isin([1, 3])])
    print()
    print("COMO LEERLO: si el acierto pasa del 55 %% y t > 2 con muchos dias, hay sesgo de fin de rueda.")
    print("Con %d dias el intervalo es ancho: esto es una replica, no una demostracion." % len(d))


if __name__ == "__main__":
    main()
