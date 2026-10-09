"""Recalculo independiente: cuanto se separa la conversion QQQ de la 2.0 ('dos') de la sincronizada de la 4.1 ('cuatro'),
en puntos de MNQ al strike 750, por sesion y ventana NY (noche 18:00-09:30, dia 09:30-16:00). Lee datos/conv_min/QQQ-*.parquet.
python -I deriva_conversion.py"""
import glob, json, os
import pandas as pd

base = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
filas = []
for f in sorted(glob.glob(os.path.join(base, "datos", "conv_min", "QQQ-*.parquet"))):
    d = pd.read_parquet(f)
    ny = d["t"].dt.tz_localize("UTC").dt.tz_convert("America/New_York")
    hm = ny.dt.hour * 60 + ny.dt.minute
    ventanas = {"dia": (hm >= 570) & (hm < 960), "noche": (hm >= 1080) | (hm < 570)}
    x = ((d["dos"] - d["cuatro"]) * 750).abs()
    for v, m in ventanas.items():
        s = x[m].dropna()
        if len(s) > 30:
            filas.append({"sesion": os.path.basename(f)[4:14], "ventana": v, "minutos": int(len(s)),
                          "mediana_pts": round(float(s.median()), 1), "p90_pts": round(float(s.quantile(0.9)), 1),
                          "max_pts": round(float(s.max()), 1)})
r = pd.DataFrame(filas)
res = {"por_sesion": filas}
for v, g in r.groupby("ventana"):
    res[v] = {"sesiones": int(len(g)), "mediana_de_medianas": float(g.mediana_pts.median()),
              "p90_mediano": float(g.p90_pts.median()), "p90_maximo": float(g.p90_pts.max())}
json.dump(res, open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "deriva_conversion.json"), "w", encoding="utf-8"),
          ensure_ascii=False, indent=1)
print({k: v for k, v in res.items() if k != "por_sesion"})
