# -*- coding: utf-8 -*-
"""RONDA 27 - ARCHIVO DE CIERRE antes de cada corrida: SHA-256 de todos los scripts r27_*.py, de la tabla por minuto y de la curva, con la hora del
sistema, en cierre_<tramo>.json, y una linea en PREREGISTRO.md. Se corre ANTES de abrir el tramo; despues no se toca nada de lo que figura.
Uso: python -I r27_cierre.py <tramo> "<nota>" """
import glob, json, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r27_comun as K


def main():
    tramo = sys.argv[1]; nota = sys.argv[2] if len(sys.argv) > 2 else ""
    h = {}
    for f in sorted(glob.glob(os.path.join(K.AQUI, "r27_*.py"))):
        h[os.path.basename(f)] = K.sha256(f)
    h["tabla_minutos.parquet"] = K.sha256(os.path.join(K.DATOS, "tabla_minutos.parquet"))
    h["curva_rango_por_minuto.json"] = K.sha256(K.CURVA)
    hora = time.strftime("%Y-%m-%d %H:%M:%S")
    json.dump({"tramo": tramo, "hora_AR": hora, "nota": nota, "sha256": h}, open(os.path.join(K.AQUI, "cierre_%s.json" % tramo), "w"), indent=1)
    with open(os.path.join(K.AQUI, "PREREGISTRO.md"), "a", encoding="utf-8") as f:
        f.write("\n- **Cierre %s** (%s AR, `cierre_%s.json`): %s\n" % (tramo, hora, tramo, nota))
        for k, v in h.items():
            f.write("  - `%s` %s\n" % (k, v))
    print("cierre %s escrito a las %s" % (tramo, hora))


if __name__ == "__main__":
    main()
