"""Prueba INDEPENDIENTE de la cuenta del Superindicador (23-11-2026).

Lee los casos sinteticos que escribe atas/_test_super (entrada: velas con su footprint por tick; salida: lo que calculo
SuperCalculos.cs) y rehace todo en Python sin mirar el C#:
  - filas de K ticks alineadas a la grilla absoluta: fila = piso(tick / K)
  - compras (ask), ventas (bid), volumen por fila y totales
  - POC: fila de mas volumen; empate -> la de precio MAS ALTO (regla de ATAS, medida en 28 de 28 velas con empate)
  - area de valor: el bucle de ATAS 8.0.15 (DynamicLevels.GetValueArea, descompilado) recorriendo TODAS las filas entre la
    mas baja y la mas alta con volumen (los huecos cuentan con 0, como el area de valor de cada vela de ATAS: 1.163 de 1.163).

Uso: python -I herramientas/test_superindicador.py <casos_super.json>
"""
import json
import sys
from decimal import Decimal


def filas(velas, k):
    acc = {}
    for v in velas:
        for t, bid, ask, vol in v["niveles"]:
            f = t // k  # piso tambien con negativos
            c = acc.setdefault(f, [Decimal(0), Decimal(0), Decimal(0)])
            c[0] += Decimal(str(bid)); c[1] += Decimal(str(ask)); c[2] += Decimal(str(vol))
    return acc


def area_valor_atas(precios, vol, poc_precio, pct, paso):
    """Copia fiel del bucle de ATAS sobre la lista ORDENADA de precios con operaciones (_allPrice)."""
    alto, bajo = max(precios), min(precios)
    total = sum(vol[p] for p in precios)
    objetivo = total * Decimal(pct) / Decimal(100)
    vah = val = poc_precio
    acum = vol[poc_precio]
    idx = {p: i for i, p in enumerate(precios)}
    while acum <= objetivo and not (vah >= alto and val <= bajo):
        i = idx[vah]; up_sum = Decimal(0); up_p = vah
        for _ in range(paso):
            if i + 1 >= len(precios):
                break
            i += 1; up_p = precios[i]; up_sum += vol[up_p]
        j = idx[val]; dn_sum = Decimal(0); dn_p = val
        for _ in range(paso):
            if j - 1 < 0:
                break
            j -= 1; dn_p = precios[j]; dn_sum += vol[dn_p]
        if up_sum == dn_sum == 0:
            vah = min(up_p, alto); val = max(dn_p, bajo)
        elif up_sum >= dn_sum:
            vah = up_p; acum += up_sum
        else:
            val = dn_p; acum += dn_sum
        if acum >= objetivo:
            break
    return vah, val, acum


def main(ruta):
    casos = json.load(open(ruta, encoding="utf-8"))
    malos = 0
    detalle = {"filas": 0, "totales": 0, "poc": 0, "va": 0}
    for c in casos:
        k, pct, paso, s = c["k"], c["pct"], c["paso"], c["salida"]
        acc = filas(c["velas"], k)
        if not acc:
            continue
        fmin, fmax = min(acc), max(acc)
        n = fmax - fmin + 1
        bid = [Decimal(0)] * n; ask = [Decimal(0)] * n; vol = [Decimal(0)] * n
        for f, (b, a, v) in acc.items():
            bid[f - fmin] = b; ask[f - fmin] = a; vol[f - fmin] = v
        ok = True
        cs_bid = [Decimal(str(x)) for x in s["bid"]]; cs_ask = [Decimal(str(x)) for x in s["ask"]]; cs_vol = [Decimal(str(x)) for x in s["vol"]]
        if s["fila_min"] != fmin or s["filas"] != n or cs_bid != bid or cs_ask != ask or cs_vol != vol:
            detalle["filas"] += 1; ok = False
        if Decimal(str(s["sum_bid"])) != sum(bid) or Decimal(str(s["sum_ask"])) != sum(ask) or Decimal(str(s["sum_vol"])) != sum(vol):
            detalle["totales"] += 1; ok = False
        # POC independiente
        con = [i for i in range(n) if vol[i] > 0]
        vmax = max(vol[i] for i in con)
        poc = max(i for i in con if vol[i] == vmax)
        if poc != s["poc"]:
            detalle["poc"] += 1; ok = False
        # area de valor con el bucle de ATAS sobre "precios" (aca: indices de fila con volumen)
        rango = list(range(con[0], con[-1] + 1))   # con los huecos
        vah, val, acum = area_valor_atas(rango, {i: vol[i] for i in rango}, poc, pct, paso)
        if (vah, val) != (s["vah"], s["val"]) or acum != Decimal(str(s["vol_av"])):
            detalle["va"] += 1; ok = False
            if detalle["va"] <= 3:
                print("  VA distinta semilla", c["semilla"], "py", vah, val, acum, "cs", s["vah"], s["val"], s["vol_av"])
        if not ok:
            malos += 1
    print(f"casos {len(casos)}: {len(casos) - malos} iguales, {malos} distintos {detalle}")
    return 0 if malos == 0 else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1]))
