"""
auditar_todo.py — LA AUDITORIA DE PUNTA A PUNTA DE LO QUE HAY EN PANTALLA, en un solo comando.

Corre, en orden, cada control independiente que existe en el laboratorio y junta los veredictos:
  1. Cada libro (QQQ, NDX, SPX, SPY) recalculado desde la cadena cruda de la nube contra el AUDIT del log:
     dominantes por strike, zero gamma, perfil derecho (convexidad en el precio) y pico |GEX|  -> capas_nq.py
  2. La primaria contra su capa gemela: si el grafico usa el libro QQQ y la capa QQQ esta prendida, los dos
     AUDIT (primaria y capa=QQQ) tienen que dar las mismas dominantes                          -> del log
  3. El respeto de cada fuente contra placebo, con la regla de siempre                          -> capas_respeto.py
  4. Lo que NO esta medido y se dibuja como supuesto (beta, magnitud de TQQQ)                   -> del log

Uso:  python laboratorio/auditar_todo.py [MNQ] [M2]
"""
import os
import re
import subprocess
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
LOG = os.path.join(os.environ.get("APPDATA", ""), "ATAS", "pythiagex-gammahoy.log")
PY = sys.executable


def correr(args):
    r = subprocess.run([PY] + args, capture_output=True, text=True, encoding="utf-8", errors="replace", cwd=os.path.dirname(AQUI))
    return (r.stdout or "") + (r.stderr or "")


def ultimas_lineas_audit():
    """La ultima linea AUDIT de cada capa (del grafico con capas) y LA PRIMARIA DE ESE MISMO GRAFICO: el log lo
    comparten varios graficos (MES, MNQ, NQ), asi que la primaria se elige por la misma vela (fut a menos de 3 pts)
    y el mismo minuto que las capas. El libro de esa primaria se lee de su origen (QQQ/SPY por razon; si no, NDX)."""
    prim, capas, primarias = None, {}, []
    if not os.path.exists(LOG):
        return prim, capas, "?"
    with open(LOG, encoding="utf-8", errors="replace") as f:
        for l in f:
            if "AUDIT" not in l:
                continue
            m = re.search(r"capa=(\w+)", l)
            if m:
                capas[m.group(1)] = l.rstrip()
            else:
                primarias.append(l.rstrip())
    ref = next(iter(capas.values()), None)
    if not ref:
        return None, capas, "?"
    fut_ref = float(re.search(r"fut=(-?[0-9.]+)", ref).group(1)); minuto = ref[:16]
    cand = [x for x in primarias if x[:16] == minuto and abs(float(re.search(r"fut=(-?[0-9.]+)", x).group(1)) - fut_ref) <= 3]
    if not cand:
        cand = [x for x in primarias if abs(float(re.search(r"fut=(-?[0-9.]+)", x).group(1)) - fut_ref) <= 3]
    prim = cand[-1] if cand else None
    libro = "?"
    if prim:
        mo = re.search(r"origen=libro_CBOE_([A-Z]+)_x_razon", prim)
        libro = mo.group(1) if mo else ("RITHMIC" if "Rithmic" in prim else "NDX")
    return prim, capas, libro


def doms_de(linea):
    m = re.search(r"doms=(\S+)", linea)
    return [round(float(x.split("=")[0]), 2) for x in m.group(1).split("/")] if m and m.group(1) else []


def main():
    a = [x for x in sys.argv[1:] if not x.startswith("--")]
    inst = a[0] if a else "MNQ"; marco = a[1] if len(a) > 1 else "M2"
    print("=" * 100)
    print("1) CADA LIBRO CONTRA SU RECALCULO INDEPENDIENTE (cadena cruda de la nube; misma razon/base que el log)")
    print("=" * 100)
    salida = correr([os.path.join(AQUI, "capas_nq.py"), "QQQ", "NDX", "SPX", "SPY"])
    veredictos = []
    for l in salida.splitlines():
        if re.match(r"^[A-Z]{2,5}\s", l) or "veredicto" in l or "perfil derecho:" in l or "pico |GEX|" in l or "log:" in l or "no se pudo" in l:
            print("  " + l.strip()[:170])
        if "veredicto" in l:
            veredictos.append(l)
    print()
    print("=" * 100)
    print("2) LA PRIMARIA CONTRA SU CAPA GEMELA (mismo libro, misma cuenta: tienen que dar lo mismo)")
    print("=" * 100)
    prim, capas, libro = ultimas_lineas_audit()
    if prim and libro in capas:
        dp, dc = doms_de(prim), doms_de(capas[libro])
        print(f"  primaria (libro {libro}) {prim[:19]} fut={re.search(r'fut=(-?[0-9.]+)', prim).group(1)}: doms {dp}")
        print(f"  capa {libro:<6}          {capas[libro][:19]} fut={re.search(r'fut=(-?[0-9.]+)', capas[libro]).group(1)}: doms {dc}")
        iguales = len(dp) == len(dc) and all(abs(x - y) < 1.0 for x, y in zip(sorted(dp), sorted(dc)))
        print("  -> " + ("IGUALES (a menos de 1 pt: el centroide y el minuto pueden mover centavos)" if iguales else "DISTINTAS: revisar (cadena de otro minuto?)"))
    elif prim:
        print(f"  la primaria de este grafico es el libro {libro} y esa capa no esta prendida: no hay gemela para comparar (sus niveles van a la escalera como {libro})")
    else:
        print("  sin AUDIT de la primaria del grafico con capas en el log")
    print()
    print("=" * 100)
    print("3) RESPETO DE CADA FUENTE CONTRA PLACEBO (la regla de rebote_niveles.py; 25+ toques para afirmar algo)")
    print("=" * 100)
    for l in correr([os.path.join(AQUI, "capas_respeto.py"), inst, marco]).splitlines():
        if l.strip():
            print("  " + l.rstrip()[:170])
    print()
    print("=" * 100)
    print("4) LO SUPUESTO (se dibuja, pero no esta medido)")
    print("=" * 100)
    for k in ("SPX", "SPY", "ES"):
        if k in capas:
            m = re.search(r"beta=(\S+) betaN=(\d+) betaR2=(\S+) betaOrigen=(\S+)", capas[k])
            if m:
                print(f"  {k}: beta {m.group(1)} (n {m.group(2)}, r2 {m.group(3)}, origen {m.group(4)}) -> "
                      + ("MEDIDA con las velas de NQ y MES" if m.group(4).startswith("velas") else "SUPUESTA = 1: el muro se dibuja a la distancia porcentual pura"))
    if "TQQQ" in capas:
        print("  TQQQ: strikes llevados a NQ con apalancamiento 3 (verificado); la MAGNITUD de sus barras no es comparable (factor x3/x9 supuesto)")
    print()
    n_ok = sum(1 for v in veredictos if "COINCIDEN" in v); n_tot = len(veredictos)
    print(f"RESUMEN: {n_ok} de {n_tot} libros coinciden strike por strike con el recalculo independiente. "
          + ("Todo lo dibujado tiene su cuenta verificada; lo supuesto esta rotulado." if n_ok == n_tot and n_tot > 0 else "Hay diferencias: leer arriba."))


if __name__ == "__main__":
    main()
