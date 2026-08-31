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
from pythiagex.matriz     import construir as matriz_construir, concentracion
from pythiagex.historico  import guardar as hist_guardar, lookbacks, intradia, cambio as hist_cambio
from pythiagex.volatilidad import skew, term, superficie
from pythiagex.flujo       import hottest, actividad_por_strike, resumen_actividad
from pythiagex.precio      import intradia as precio_intradia, cotizacion
from pythiagex.base        import (medir as medir_base, contrato_vigente, nombre_futuro,
                                   convertir as a_futuro, edad_minutos as edad_cadena)
from pythiagex.tasas       import curva as curva_tasas, tasa as tasa_plazo
from pythiagex.feed_atas   import construir as feed_atas
from pythiagex.griegas     import fijar_tasa
from pythiagex.tablero     import (enriquecer, escalera, huecos, sesion,
                                   contratos_cobertura, CONTRATO)

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
                    help="forzar la base; por defecto se mide sola")
    ap.add_argument("--sin-precio", action="store_true",
                    help="no bajar la serie de precio intradia")
    ap.add_argument("--panel", action="store_true", help="escribe panel/datos.json")
    ap.add_argument("--rango", type=float, default=0.03,
                    help="ancho de strikes alrededor del spot, en tanto por uno")
    ap.add_argument("--matriz", action="store_true",
                    help="agrega la matriz strike x vencimiento")
    ap.add_argument("--sin-historico", action="store_true",
                    help="no guardar esta corrida en el historico")
    a = ap.parse_args()

    sym = normalizar(a.simbolo)
    crudo = bajar(sym)

    # La base se mide primero, porque de paso devuelve el contado implicito.
    # Si el indice publicado quedo congelado (deja de cotizar 16:15 ET), toda
    # la exposicion se calcularia sobre un precio viejo: la gamma de cada
    # strike depende de donde esta el precio. Se corrige antes de calcular.
    # La tasa entra en vanna y charm. Se mide del Tesoro antes de calcular;
    # si el feed no contesta, queda el respaldo y el panel lo avisa.
    ctas = None
    try:
        ctas = curva_tasas()
    except Exception:
        ctas = None
    tasa_ok = fijar_tasa(tasa_plazo(30, ctas)) if ctas else False

    med = medir_base(crudo)
    indice_publicado = crudo["data"]["current_price"]
    if med and med.get("indice_atrasado") and med.get("contado_implicito"):
        crudo["data"]["current_price"] = med["contado_implicito"]

    r = calcular(crudo, dias_max=a.dias, solo_venc=a.venc)

    S  = r["spot"]
    st = r["strikes"]
    curva, flip = curva_gamma(r["curva_src"], S)
    em  = expected_move(r["curva_src"], S)
    niv = niveles_clave(st, S)
    # niveles del vencimiento de hoy: son los imanes que mandan en el intradia
    st0 = {k: v for k, v in st.items() if abs(v.get("gex_0dte", 0.0)) > 0}
    niv0 = niveles_clave(st0, S, campo="gex_0dte") if st0 else {}
    gex0 = sum(v.get("gex_0dte", 0.0) for v in st.values())
    # IV at-the-money y tiempo al vencimiento mas cercano: con eso se calcula
    # la probabilidad de que el precio toque cada nivel antes del cierre
    iv_atm, T_atm = None, None
    if r["curva_src"]:
        Tmin = min(z[4] for z in r["curva_src"])
        cerc = [z for z in r["curva_src"] if abs(z[4] - Tmin) < 1e-9]
        if cerc:
            k0, _, _, iv0, t0 = min(cerc, key=lambda z: abs(z[0] - S))
            iv_atm, T_atm = iv0, t0
    cam = cambio_vs(anterior(sym), st)
    ale = alertas(S, niv)
    T   = r["totales"]

    # historico: habilita lookbacks e intradia
    lb = lookbacks(sym)
    if not a.sin_historico:
        hist_guardar(sym, {"spot": S, "net_gex_B": B(T["gex"]),
                           "net_dex_B": B(T["dex"]), "net_vex_B": B(T["vex"]),
                           "net_chex_B": B(T["chex"]), "gamma_flip": flip,
                           "expected_move": em, "niveles": niv,
                           "niveles_0dte": niv0, "timestamp": r["timestamp"]}, st)
    out_lb = {}
    for etq, snap in lb.items():
        out_lb[etq] = {"edad_min": snap["edad_min"], "spot": snap["spot"],
                       "gex": snap["gex"], "flip": snap["flip"],
                       "k": {k: v[0] for k, v in snap["k"].items()}}
    prev = lb.get("10m") or lb.get("20m") or lb.get("30m")

    # base indice -> futuro, ya medida arriba
    base = a.base if a.base is not None else (med["base"] if med else None)
    vfut, cod = contrato_vigente()
    fut, micro = nombre_futuro(r["simbolo"], cod)
    conv = (lambda x: a_futuro(x, base)) if base is not None else (lambda x: x)

    # serie de precio intradia, en indice y en futuro
    px = None
    if not a.sin_precio:
        try:
            px = precio_intradia(sym, base=base)
        except Exception as e:
            px = {"error": str(e)[:80]}


    out = {
      "simbolo": r["simbolo"], "spot": round(S, 2),
      "timestamp": r["timestamp"], "generado": r["generado"],
      "vista": a.venc or f"{a.dias}d",
      "base": base,
      "base_detalle": med,
      # el precio del futuro es el forward del vencimiento trimestral, medido
      # sobre la cadena. No se calcula como indice + base porque el indice
      # publicado se congela 16:15 ET y arrastraria ese atraso.
      "futuro": {"contrato": fut, "micro": micro, "vencimiento": vfut.isoformat() if vfut else None,
                 "spot": (med.get("forward") if med else None)
                         or (a_futuro(S, base) if base is not None else None)},
      "tasas": ({"fecha": ctas.get("fecha"), "fuente": ctas.get("fuente"),
                 "tenores": ctas.get("tenores"), "medida": bool(tasa_ok)}
                if ctas else {"medida": False,
                              "fuente": "sin respuesta del Tesoro: se usa el respaldo"}),
      "contado_implicito": med.get("contado_implicito") if med else None,
      "indice_publicado": indice_publicado,
      "spot_corregido": bool(med and med.get("indice_atrasado")),
      "desfase_indice": med.get("desfase_indice") if med else None,
      "indice_atrasado": bool(med.get("indice_atrasado")) if med else False,
      "precio_intradia": px,
      "regimen": "POSITIVO" if T["gex"] > 0 else "NEGATIVO",
      "gamma_flip": flip,
      "expected_move": em,
      "totales": {"net_gex_B": B(T["gex"]), "net_gex_vol_B": B(T["gex_vol"]),
                  "net_dex_B": B(T["dex"]), "net_vex_B": B(T["vex"]),
                  "net_chex_B": B(T["chex"]), "net_tex_M": M(T["tex"]),
                  "oi_total": int(T["oi"]), "oi_call": int(T["oi_call"]),
                  "oi_put": int(T["oi_put"]), "volumen": int(T["vol"]),
                  "put_call_oi": round(T["oi_put"]/T["oi_call"], 3) if T["oi_call"] else None},
      "niveles": {k: (conv(v) if v else None) for k, v in niv.items()},
      "niveles_indice": niv,
      "niveles_0dte": {k: (conv(v) if v else None) for k, v in niv0.items()},
      "niveles_0dte_indice": niv0,
      "gex_0dte_B": B(gex0),
      "gamma_flip_futuro": conv(flip) if flip else None,
      "iv_atm": round(iv_atm, 4) if iv_atm else None,
      "dias_al_vencimiento_cercano": round(T_atm * 365, 3) if T_atm else None,
      "alertas": ale,
      "max_change": cam,
      "lookbacks": out_lb,
      "cambio": hist_cambio(st, prev, top=12) if prev else [],
      "intradia": intradia(sym),
      "skew": skew(crudo),
      "term": term(crudo),
      "actividad": actividad_por_strike(crudo, ancho=a.rango),
      "posicion_nueva": resumen_actividad(actividad_por_strike(crudo, ancho=a.rango)),
      "hottest": hottest(crudo, top=15),
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
      "atm_strike": min(st.keys(), key=lambda k: abs(k - S)) if st else None,
      "horizonte": {"dias": a.dias, "vencimientos": len(r["vencimientos"])},
      "regimen_label": "Dampening" if T["gex"] > 0 else "Amplifying",
      "vencimientos": [dict(v, gex=M(v["gex"]), dex=M(v["dex"]),
                            oi=int(v["oi"]), vol=int(v["vol"]),
                            oi_call=int(v["oi_call"]), oi_put=int(v["oi_put"]))
                       for v in sorted(r["vencimientos"].values(),
                                       key=lambda z: z["dias"])],
    }

    # tablero.py razona en millones, igual que el panel
    st_M = {k: {"gex": M(v["gex"]), "gex_0dte": M(v["gex_0dte"]),
                "oi_call": int(v["oi_call"]), "oi_put": int(v["oi_put"]),
                "vol_call": int(v["vol_call"]), "vol_put": int(v["vol_put"])}
            for k, v in st.items()}

    raiz = (fut or "ES")[:-2] or "ES"
    if raiz not in CONTRATO:
        raiz = "ES"
    cfg = CONTRATO[raiz]
    out["contrato_detalle"] = dict(cfg, raiz=raiz)

    # cada nivel con todos sus numeros encima, no solo el strike
    out["niveles_ricos"] = enriquecer(niv, st_M, S, base=base, iv_atm=iv_atm,
                                      T=T_atm, raiz=raiz, flip=flip)
    out["niveles_ricos_0dte"] = enriquecer(niv0, st_M, S, base=base, iv_atm=iv_atm,
                                           T=T_atm, raiz=raiz, sufijo=" 0DTE")
    # convexity ladder: la cobertura obligada en cada escalon de precio
    out["escalera"] = escalera(curva, S, base=base, raiz=raiz)
    # tramos sin gamma, donde el precio viaja sin freno
    out["huecos"] = huecos(st_M, S, base=base)
    # apertura, maximo, minimo e initial balance de la sesion
    out["sesion"] = sesion((px or {}).get("velas"), base=base) if px else None
    # cuantos contratos tiene que operar la mesa por 1% de movimiento
    pf = (med.get("forward") if med else None) or (a_futuro(S, base) if base else None)
    out["cobertura"] = contratos_cobertura(T["gex"], pf, raiz) if pf else None

    # Cuanto hace que CBOE cotizo esta cadena. Es distinto de "cuando corrio
    # el calculo": el calculo puede ser de recien y el dato de anteayer.
    edad = edad_cadena(r["timestamp"])
    out["edad_cadena_min"] = round(edad) if edad is not None else None
    out["cadena_vencida"] = bool(edad is not None and edad > 60)
    out["cadena_muy_vencida"] = bool(edad is not None and edad > 20 * 60)
    # si diera negativo, la zona horaria del feed cambio y hay que revisarla
    out["timestamp_incoherente"] = bool(edad is not None and edad < -5)

    if a.matriz:
        mz = matriz_construir(crudo, "gex", dias_max=max(a.dias, 30), ancho=a.rango)
        out["matriz"] = mz
        out["concentracion"] = concentracion(mz, top=8)
        out["superficie"] = superficie(crudo, ancho=a.rango)

    os.makedirs(SALIDA, exist_ok=True)
    with open(f"{SALIDA}/{sym}.json", "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, separators=(",", ":"))
    with open(f"{SALIDA}/{sym}-anterior.json", "w", encoding="utf-8") as f:
        json.dump({str(k): {"gex": v["gex"]} for k, v in st.items()}, f)
    if a.panel:
        os.makedirs("panel", exist_ok=True)
        with open("panel/datos.json", "w", encoding="utf-8") as f:
            json.dump(out, f, ensure_ascii=False, separators=(",", ":"))

    # extracto liviano para el indicador de ATAS: lo baja cada 30 segundos
    fa = feed_atas(out)
    os.makedirs(os.path.join(SALIDA, "atas"), exist_ok=True)
    raiz = (fa.get("contrato") or "ES")[:-2] or "ES"
    ruta_atas = os.path.join(SALIDA, "atas", "%s.json" % raiz)
    with open(ruta_atas, "w", encoding="utf-8") as f:
        json.dump(fa, f, ensure_ascii=False, separators=(",", ":"))
    print("  -> %s  (%d niveles, %.1f KB)"
          % (ruta_atas, len(fa["niveles"]), os.path.getsize(ruta_atas) / 1024))

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
    if a.matriz:
        print("  concentracion (donde pesa cada nivel):")
        for x in out["concentracion"][:5]:
            print(f"    {x['strike']:>8.0f}  {x['total']:>7}M   {x['concentracion_pct']:>3}% en {x['vencimiento_dominante']} ({x['dte']}d)")
    sk, tm = out["skew"], out["term"]
    if sk.get("pendiente_pp") is not None:
        print(f"  skew {sk['pendiente_pp']:+} pp ({sk['vencimiento']})   term {tm.get('forma')}")
    pn = out["posicion_nueva"]
    if pn:
        print("  posicion nueva: " + " · ".join(f"{x['strike']:.0f}({x['vol_oi']}x)" for x in pn[:5]))
    if base is not None:
        cf = out["futuro"]
        print(f"  {fut}/{micro} {cf['spot']}   base {base:+.2f}"
              + (f" ({med['muestras']} strikes, disp {med['dispersion']})" if med else "")
              + (f"   flip futuro {out['gamma_flip_futuro']}" if out.get("gamma_flip_futuro") else ""))
    if px and px.get("velas"):
        print(f"  precio intradia: {px['n']} velas de 1 min  "
              f"{px['minimo']}-{px['maximo']}  vol C/P {px['pc_volumen']}")
    print(f"  -> {SALIDA}/{sym}.json")

if __name__ == "__main__":
    main()
