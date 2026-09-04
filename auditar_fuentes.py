# -*- coding: utf-8 -*-
"""Audita el interes abierto de ATAS/Rithmic contra el oficial de CME.

    python auditar_fuentes.py [ES]

POR QUE ESTE ARCHIVO EXISTE

El proyecto ya tiene dos auditores que rehacen la CUENTA (auditar_indicador.py y
auditar_viva.py): comprueban que el C# y una implementacion independiente en
Python den el mismo numero a partir de la misma cadena. Eso valida la formula,
pero no valida EL DATO DE ENTRADA.

Este audita la entrada. Compara, strike por strike, el interes abierto que
entrega Rithmic -- el mismo feed que alimenta el Options Board de ATAS -- contra
el oficial de CME Group, que es la bolsa donde se listan esos contratos.

LA TRAMPA QUE YA COSTO UNA CONCLUSION FALSA

CME tiene DOS endpoints y dan numeros distintos:

  Settlements     -> interes abierto del dia ANTERIOR
  Volume/Details  -> el actual, en atClose (= Settlements + change)

Comparando contra Settlements salieron diferencias de hasta 75 % y se llego a
calcular que el major positive se corria 50 puntos segun la fuente. Era falso:
Rithmic coincide EXACTO con Volume/Details. Se usa ese y solo ese.
Ver la memoria [[cme-settlements-un-dia-atras]].

DE DONDE SALE CADA LADO

  ATAS/Rithmic : %APPDATA%\\ATAS\\pythiagex-cadena-viva-<RAIZ>.json
                 lo escribe el indicador junto con su renglon de auditoria
  CME          : datos/cme/<RAIZ>-<YYYYMMDD>.json
                 hay que bajarlo desde el navegador logueado -- Akamai
                 responde 403 a cualquier script (probado)

Si el archivo de CME no esta o esta viejo, se dice y no se compara: comparar
contra un dato de otro dia es justamente el error que este auditor existe para
no repetir.
"""
import glob, io, json, math, os, sys, datetime as dt

RAIZ_POR_DEFECTO = "ES"


def leer_rithmic(raiz):
    p = os.path.join(os.environ.get("APPDATA", ""), "ATAS",
                     "pythiagex-cadena-viva-%s.json" % raiz)
    if not os.path.exists(p):
        return None, "no hay foto de cadena viva para %s (el indicador la escribe cuando esta activa)" % raiz
    d = json.load(io.open(p, encoding="utf-8-sig"))
    hoy = dt.date.today()
    por_venc = {}
    for K, dias, call, oi, iv, bid, ask in d["filas"]:
        venc = (hoy + dt.timedelta(days=int(round(dias)))).isoformat()
        e = por_venc.setdefault(venc, {}).setdefault(K, {"C": None, "P": None})
        e["C" if call else "P"] = oi
    return {"futuro": d["futuro"], "ts": d.get("ts", ""), "venc": por_venc}, ""


def leer_cme(raiz):
    ds = sorted(glob.glob(os.path.join("datos", "cme", "%s-*.json" % raiz)))
    if not ds:
        return None, "no hay volcado de CME en datos/cme/ (hay que bajarlo desde el navegador)"
    p = ds[-1]
    d = json.load(io.open(p, encoding="utf-8"))
    return d, os.path.basename(p)


def main():
    raiz = (sys.argv[1].upper() if len(sys.argv) > 1 else RAIZ_POR_DEFECTO)
    if raiz in ("MES",): raiz = "ES"
    if raiz in ("MNQ",): raiz = "NQ"

    rit, err = leer_rithmic(raiz)
    if not rit:
        print(err); return 2
    cme, archivo = leer_cme(raiz)
    if not cme:
        print(archivo); return 2

    print("=" * 74)
    print("INTERES ABIERTO: ATAS/Rithmic contra CME  (%s)" % raiz)
    print("=" * 74)
    print("Rithmic : futuro %.2f   sello %s" % (rit["futuro"], rit["ts"]))
    print("CME     : %s   tradeDate %s   bajado %s" % (archivo, cme.get("tradeDate"), cme.get("bajado", "")[:19]))
    print()

    comunes = [v for v in cme.get("v", {}) if v in rit["venc"]]
    if not comunes:
        print("NO COMPARABLE: no hay ningun vencimiento en las dos fuentes.")
        print("  Rithmic tiene:", ", ".join(sorted(rit["venc"])[:8]))
        print("  CME tiene    :", ", ".join(sorted(cme.get("v", {}))))
        return 4

    fallas = 0
    for venc in sorted(comunes):
        fc = cme["v"][venc].get("f", {})
        fr = rit["venc"][venc]
        ks = sorted(set(int(float(k)) for k in fc) & set(int(k) for k in fr))
        if not ks:
            print("%s  sin strikes en comun" % venc); continue
        ig = di = 0
        peor = None
        for k in ks:
            c = fc.get(str(k)) or fc.get(str(float(k)))
            r = fr.get(k) or fr.get(float(k))
            if not c or not r: continue
            for t in ("C", "P"):
                a = (c.get(t) or [None])[0]
                b = r.get(t)
                if a is None or b is None: continue
                if abs(a - b) <= 1: ig += 1
                else:
                    di += 1
                    d = abs(a - b)
                    if peor is None or d > peor[0]: peor = (d, k, t, a, b)
        tot = ig + di
        pct = 100.0 * ig / tot if tot else 0
        print("%s   %3d strikes   %4d/%-4d coinciden (%.1f %%)"
              % (venc, len(ks), ig, tot, pct))
        if peor:
            print("     peor diferencia: strike %d %s  CME %d  Rithmic %d  (%d contratos)"
                  % (peor[1], peor[2], peor[3], peor[4], peor[0]))
        if pct < 95: fallas += 1

    print()
    if fallas == 0:
        print("PASA: el interes abierto de ATAS coincide con el oficial de CME.")
    else:
        print("OJO: %d vencimiento(s) por debajo del 95 %% de coincidencia." % fallas)
        print("Antes de culpar a la plataforma: verificar que el volcado de CME sea")
        print("del MISMO dia y que venga de Volume/Details, no de Settlements.")
    return 0 if fallas == 0 else 3


if __name__ == "__main__":
    sys.exit(main())
