# -*- coding: utf-8 -*-
"""RONDA 22, paso 0: INVENTARIO de las velas de MES (5 y 30 min). Solo plomeria: cuantas velas tiene cada jornada (18:00 NY de la vispera
a 17:00 NY), si la rueda 9:30-16:00 esta completa, huecos, medias ruedas y el salto de precio entre el cierre de una jornada y la apertura
de la siguiente (para detectar el cambio de contrato del continuo). NO calcula rangos, estados, eventos ni resultados.
Uso: python -I r22_00_inventario.py  -> salidas/inventario_m5.csv, salidas/inventario_m30.csv"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r22_comun as K
import numpy as np, pandas as pd


def leer(ruta, minutos):
    v = pd.read_csv(ruta, usecols=["t", "o", "h", "l", "c", "vol", "dia_carpeta"])
    ny = pd.to_datetime(v["t"]).dt.tz_localize("UTC").dt.tz_convert(K.NY).dt.tz_localize(None)
    v["ny"] = ny
    v["rueda"] = (ny + pd.Timedelta(hours=6)).dt.strftime("%Y-%m-%d")
    base = pd.to_datetime(v["rueda"])
    v["hm"] = ((ny - base).dt.total_seconds() // 60).astype(int)      # minutos desde la medianoche NY del dia de la rueda (noche < 0)
    v["min"] = minutos
    return v.sort_values("ny").reset_index(drop=True)


def inventario(v, minutos):
    n_rth = 390 // minutos
    filas = []
    prev_c = None; prev_r = None
    for r, g in v.groupby("rueda", sort=True):
        hm = g["hm"].to_numpy()
        rth = g[(hm >= 570) & (hm < 960)]
        esperado = 570 + minutos * np.arange(n_rth)
        completa = len(rth) == n_rth and (np.sort(rth["hm"].to_numpy()) == esperado).all()
        ultima_rth = int(rth["hm"].max()) if len(rth) else -1
        dif = np.diff(np.sort(hm)) if len(hm) > 1 else np.array([0])
        hueco_max = int(dif.max()) if len(dif) else 0
        noche = g[(hm < 570)]
        asia = g[(hm < 180)]; lon = g[(hm >= 180) & (hm < 570)]
        salto = float(g["o"].iloc[0] - prev_c) if prev_c is not None else np.nan
        filas.append(dict(rueda=r, dia_semana=pd.Timestamp(r).day_name()[:3], velas=len(g), velas_rth=len(rth), rth_completa=int(completa),
                          primera_ny=g["ny"].iloc[0].strftime("%m-%d %H:%M"), ultima_ny=g["ny"].iloc[-1].strftime("%m-%d %H:%M"),
                          ultima_rth_hm="%02d:%02d" % divmod(ultima_rth, 60) if ultima_rth >= 0 else "",
                          velas_noche=len(noche), velas_asia=len(asia), velas_londres=len(lon), hueco_max_min=hueco_max - minutos,
                          rueda_previa=prev_r, salto_apertura=salto, vol=int(g["vol"].sum())))
        prev_c = float(g["c"].iloc[-1]); prev_r = r
    return pd.DataFrame(filas)


def main():
    for nombre, ruta, mins in (("m5", K.M5, 5), ("m30", K.M30, 30)):
        v = leer(ruta, mins)
        inv = inventario(v, mins)
        inv.to_csv(os.path.join(K.SAL, "inventario_%s.csv" % nombre), index=False)
        print("== %s: %d velas, %d jornadas (%s a %s)" % (nombre, len(v), len(inv), inv.rueda.min(), inv.rueda.max()))
        print("   rueda completa: %d | incompletas: %d" % (inv.rth_completa.sum(), (inv.rth_completa == 0).sum()))
        inc = inv[inv.rth_completa == 0]
        print("   incompletas (rueda, dia, velas rth, ultima vela rth):")
        for _, x in inc.iterrows():
            print("     %s %s rth %2d ultima %s velas %3d noche %3d" % (x.rueda, x.dia_semana, x.velas_rth, x.ultima_rth_hm, x.velas, x.velas_noche))
        esperado_noche = (570 + 360) // mins
        print("   noche (18:00-9:30) esperado %d velas; jornadas completas con noche < 90 %%: %d" % (
            esperado_noche, int(((inv.rth_completa == 1) & (inv.velas_noche < 0.9 * esperado_noche)).sum())))
        for _, x in inv[(inv.rth_completa == 1) & (inv.velas_noche < 0.9 * esperado_noche)].iterrows():
            print("     %s %s noche %d asia %d londres %d hueco max %d min" % (x.rueda, x.dia_semana, x.velas_noche, x.velas_asia, x.velas_londres, x.hueco_max_min))
        print("   huecos > 30 min dentro de jornadas con rueda completa:")
        for _, x in inv[(inv.rth_completa == 1) & (inv.hueco_max_min > 30)].iterrows():
            print("     %s hueco %d min" % (x.rueda, x.hueco_max_min))
        s = inv["salto_apertura"].abs()
        print("   salto de apertura |o - c previo|: mediana %.2f, p95 %.2f, max %.2f" % (s.median(), s.quantile(0.95), s.max()))
        for _, x in inv[s > 8].iterrows():
            print("     SALTO %s (previa %s): %+.2f" % (x.rueda, x.rueda_previa, x.salto_apertura))


if __name__ == "__main__":
    main()
