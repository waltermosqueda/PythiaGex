#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
PythiaGex - linea de comandos.

  python cli.py SPX                 # vista completa (18 dias)
  python cli.py SPX --venc latest   # solo el vencimiento mas cercano
  python cli.py SPX --venc next     # solo el siguiente
  python cli.py SPX --dias 90       # todo el complejo
  python cli.py NDX --base 12.28    # convierte los niveles a precio de futuro
  python cli.py SPX --panel         # ademas escribe panel/datos.json
"""
import argparse, json, os, sys, datetime as dt
from pythiagex.fuentes    import bajar, normalizar
from pythiagex.exposicion import calcular
from pythiagex.niveles    import (curva_gamma, expected_move, niveles_clave,
                                  skew_0dte, cambio_vs, alertas)

SALIDA = "datos/salida"

def M(v): return round(v / 1e6)
def B(v): return round(v / 1e9, 3)

def anterior(sym):
    """Ultima corrida guardada, para calcular max change strikes."""
    p = os.path.join(SALIDA, f"{sym}-anterior.json")
    if not os.path.exists(p): return None
    try:
        with open(p, encoding="utf-8") as f:
            return {float(k): v for k, v in json.load(f).items()}
    except Exception:
        return None

def main():
    ap = argparse.ArgumentParser(description="Exposicion de opciones desde CBOE")
    ap.add_argument("simbolo", nargs="?", default="SPX")
    ap.add_argument("--dias",  type=int, default=18, help="horizonte en dias")
    ap.add_argument("--venc",  choices=["latest", "next"], default=None)
    ap.add_argument("--base",  type=float, default=None,
                    help="base indice->futuro, para convertir los niveles")
    ap.add_argument("--panel", action="store_true", help="escribe panel/datos.json")
    ap.add_argument("--rango", type=float, default=0.03,
                    help="ancho de strikes alrededor del spot, en tanto por uno")
    a = ap.parse_args()

    sym = normalizar(a.simbolo)
    crudo = bajar(sym)
    r = calcular(crudo, dias_max=a.dias, solo_venc=a.venc)

    S  = r["spot"]
    st = r["strikes"]
    curva, flip = curva_gamma(r["curva_src"], S)
    em  = expected_move(r["curva_src"], S)
    niv = niveles_clave(st, S)
    cam = cambio_vs(anterior(sym), st)
    ale = alertas(S, niv)
    T   = r["totales"]

    conv = (lambda x: round(x + a.base, 2)) if a.base is not None else (lambda x: x)

    out = {
      "simbolo": r["simbolo"], "spot": round(S, 2),
      "timestamp": r["timestamp"], "generado": r["generado"],
      "vista": a.venc or f"{a.dias}d",
      "base": a.base,
      "regimen": "POSITIVO" if T["gex"] > 0 else "NEGATIVO",
      "gamma_flip": flip, "gamma_flip_futuro": conv(flip) if flip else None,
      "expected_move": em,
      "totales": {"net_gex_B": B(T["gex"]), "net_gex_vol_B": B(T["gex_vol"]),
                  "net_dex_B": B(T["dex"]), "net_vex_B": B(T["vex"]),
                  "net_chex_B": B(T["chex"]), "net_tex_M": M(T["tex"]),
                  "oi_total": int(T["oi"]), "oi_call": int(T["oi_call"]),
                  "oi_put": int(T["oi_put"]), "volumen": int(T["vol"]),
                  "put_call_oi": round(T["oi_put"]/T["oi_call"], 3) if T["oi_call"] else None},
      "niveles": {k: (conv(v) if v else None) for k, v in niv.items()},
      "niveles_indice": niv,
      "alertas": ale,
      "max_change": cam,
      "skew_0dte": skew_0dte(st, S),
      "curva": curva,
      "strikes": [dict(strike=k, dist=round(k - S, 1),
                       gex=M(s["gex"]), gex_vol=M(s["gex_vol"]),
                       gex_0dte=M(s["gex_0dte"]), dex=M(s["dex"]),
                       vex=M(s["vex"]), chex=M(s["chex"]), tex=M(s["tex"]),
                       oi_call=int(s["oi_call"]), oi_put=int(s["oi_put"]),
                       vol_call=int(s["vol_call"]), vol_put=int(s["vol_put"]),
                       iv_call=s["iv_call"], iv_put=s["iv_put"])
                  for k, s in sorted(st.items())
                  if abs(k - S) / S <= a.rango],
      "vencimientos": [dict(v, gex=M(v["gex"]), dex=M(v["dex"]),
                            oi=int(v["oi"]), vol=int(v["vol"]),
                            oi_call=int(v["oi_call"]), oi_put=int(v["oi_put"]))
                       for v in sorted(r["vencimientos"].values(),
                                       key=lambda z: z["dias"])],
    }

    os.makedirs(SALIDA, exist_ok=True)
    with open(f"{SALIDA}/{sym}.json", "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, separators=(",", ":"))
    with open(f"{SALIDA}/{sym}-anterior.json", "w", encoding="utf-8") as f:
        json.dump({str(k): {"gex": v["gex"]} for k, v in st.items()}, f)
    if a.panel:
        os.makedirs("panel", exist_ok=True)
        with open("panel/datos.json", "w", encoding="utf-8") as f:
            json.dump(out, f, ensure_ascii=False, separators=(",", ":"))

    t = out["totales"]
    print(f"{out['simbolo']}  spot {out['spot']}  ({out['vista']})  {out['timestamp']}")
    print(f"  GEX {t['net_gex_B']}B  DEX {t['net_dex_B']}B  VEX {t['net_vex_B']}B  "
          f"CHEX {t['net_chex_B']}B  TEX {t['net_tex_M']}M")
    print(f"  regimen {out['regimen']}   flip {out['gamma_flip']}"
          + (f" -> futuro {out['gamma_flip_futuro']}" if a.base else "")
          + f"   EM +/-{out['expected_move']}")
    print(f"  OI {t['oi_total']:,}  (C {t['oi_call']:,} / P {t['oi_put']:,})  P/C {t['put_call_oi']}")
    n = out["niveles"]
    print(f"  call wall {n['call_wall']}  put wall {n['put_wall']}  "
          f"pin {n['gamma_pin']}  maj+ {n['major_positive']}  maj- {n['major_negative']}")
    if ale:  print(f"  ALERTA: spot pegado a {', '.join(x['nivel'] for x in ale)}")
    if cam:  print(f"  max change: " + " · ".join(f"{c['strike']}({c['delta_gex']:+})" for c in cam[:4]))
    print(f"  -> {SALIDA}/{sym}.json")

if __name__ == "__main__":
    main()
