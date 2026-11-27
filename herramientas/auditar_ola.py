# -*- coding: utf-8 -*-
"""Auditoria POR FUERA de la ola del Superindicador (2.0a, 27-11-2026).

Toma un ola-<inst>-<tf>-<clave>.csv (lo escribe el indicador cada minuto en %APPDATA%/ATAS/PythiaGex/superindicador, o el arnes
atas/_test_super en modo 'flujo') y una cinta de la sonda de la MISMA jornada (%APPDATA%/ATAS/PythiaGex/flujo/cinta-<inst>-<desde>-<hasta>.csv).
Rehace la ola (X) y la marea (4X) con la referencia de test_ola.py (la clase Maquina copiada tal cual de ola_demo_datos.py, verificada en
17 ruedas) manejada igual que alli, con las MISMAS horas de corte que trae el csv (una por vela), y compara sin tolerancias:
  - cada pivote confirmado y el extremo en curso (hora del extremo, precio, tipo, confirmado, hora y numero de orden de la confirmacion,
    cum en el extremo);
  - la foto de cada vela de la ola y de la marea (ordenes, hora y ultimo precio, cum, direccion, pivotes, origen, extremo, delta de la
    pierna, con/sin agresores, retroceso, faltan, raya y recorrido).
Que ordenes entran en la referencia: las de la cinta en el orden del archivo, de [inicio del motor, hasta) de la jornada, hasta la
ultima orden que tenia el indicador cuando escribio el csv (t_ultima; en ese milisegundo, hasta completar sus n ordenes). Si la carga
quedo "sin cinta" (solo en vivo), el motor arranca en la primera orden en vivo: en ese milisegundo entran solo las ultimas n_inicio.
Ademas controla lo que dice el csv: cortes tarde 0, actualizaciones viejas 0, costura no dudosa, una fila por vela.

Uso: python -I herramientas/auditar_ola.py <ola.csv> <cinta.csv> [--max-difs N]
     python -I herramientas/auditar_ola.py --lista <flujo-csvs.txt>
         (la lista del arnes: csv <tab> cinta <tab> positivo|negativo; los positivos tienen que dar 0 diferencias y el control
          negativo MAS de 0: asi se prueba que el auditor ve un error cuando lo hay)
Todo en prioridad baja; se frena si la RAM libre baja de 2 GB.
"""
import csv
import glob
import os
import re
import sys
import tempfile
import time
from datetime import datetime

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import test_ola as T   # noqa: E402  (la referencia: Maquina, foto, iso, comparar; al importarse baja la prioridad del proceso)


def _txt(s):
    return None if s == "" else s


def _num(s):
    return None if s == "" else float(s)


def _ent(s):
    return None if s == "" else int(s)


def _hora(s):
    return None if not s else datetime.fromisoformat(s)


def leer_ola(ruta):
    """El csv del indicador: M (datos de la carga), P (pivotes) y F (foto de cada vela), separados por coma."""
    meta, P, F = {}, {"X": [], "4X": []}, {"X": [], "4X": []}
    with open(ruta, encoding="utf-8") as fh:
        for linea in fh:
            if linea.startswith("#"):
                continue
            c = linea.rstrip("\n").split(",")
            if c[0] == "M":
                meta[c[1]] = c[2] if len(c) > 2 else ""
            elif c[0] == "P":
                P[c[1]].append((int(c[2]), (c[3], float(c[4]), int(c[5]), int(c[6]), _txt(c[7]), int(c[8]), float(c[9]))))
            elif c[0] == "F":
                fila = ([int(c[6]), _txt(c[7]), _num(c[8]), float(c[9]), int(c[10]), int(c[11])]
                        + [_txt(c[12]), _num(c[13]), _ent(c[14]), _num(c[15]), _txt(c[16]), _num(c[17]), _num(c[18]),
                           _num(c[19]), _ent(c[20]), _num(c[21]), _num(c[22]), _num(c[23]), _num(c[24])])
                F[c[1]].append((int(c[2]), int(c[3]), _txt(c[4]), c[5] == "1", fila))
    for esc in P:
        P[esc].sort(key=lambda x: x[0])
        F[esc].sort(key=lambda x: x[0])
    return meta, P, F


_CINTAS = {}


def leer_cinta(ruta):
    """La cinta de la sonda en el orden del archivo, sin sacar duplicados (como la referencia). Se guarda leida entre csv."""
    if ruta in _CINTAS:
        return _CINTAS[ruta]
    out = []
    with open(ruta, newline="") as fh:
        r = csv.reader(fh)
        cab = next(r)
        if cab[:5] != ["t", "primero", "ultimo", "vol", "lado"]:
            raise SystemExit("encabezado inesperado en %s: %s" % (ruta, cab))
        for x in r:
            out.append((datetime.fromisoformat(x[0]), float(x[1]), float(x[2]), float(x[3]), int(x[4])))
    _CINTAS.clear()   # una sola cinta en memoria a la vez (~100 MB por rueda)
    _CINTAS[ruta] = out
    return out


class Rehacer:
    """La ola y la marea de la referencia con los cortes del csv: la foto del corte c es el estado con las ordenes de t < c (se toma antes
    de pasar la primera orden de t >= c); al final, las que quedan (y las filas sin corte: la vela en curso) son el estado final.
    Misma cuenta que test_ola.Ventana (mn, mx, cum += v * lado antes del paso, numero de orden de cada confirmacion)."""

    def __init__(s, X, cortes, filas):
        s.X = X
        s.m = {"X": T.Maquina(X), "4X": T.Maquina(4 * X)}
        s.cum = 0
        s.n = 0
        s.ult = None
        s.t_ult = None
        s.cortes = cortes
        s.filas = filas
        s.fotos = {"X": [], "4X": []}
        s.nconf = {"X": [], "4X": []}

    def _fotear(s):
        for esc, m in s.m.items():
            s.fotos[esc].append(T.foto(m, s))

    def orden(s, t, a, b, v, lado):
        while len(s.fotos["X"]) < len(s.cortes) and t >= s.cortes[len(s.fotos["X"])]:
            s._fotear()
        mn, mx = min(a, b), max(a, b)
        s.cum += v * lado
        s.n += 1
        s.ult, s.t_ult = b, t
        for esc, m in s.m.items():
            antes = len(m.pivotes)
            m.paso(t, mn, mx, s.cum)
            if len(m.pivotes) != antes:
                s.nconf[esc].append(s.n)

    def cerrar(s):
        while len(s.fotos["X"]) < s.filas:
            s._fotear()


def pivotes_ref(r, esc):
    m = r.m[esc]
    out = [(T.iso(te), p, 1 if tipo == "max" else -1, 1, T.iso(tc), n, c) for (te, p, tipo, tc, c), n in zip(m.pivotes, r.nconf[esc])]
    if m.dir != 0:
        out.append((T.iso(m.ext_t), m.ext, m.dir, 0, None, 0, m.ext_cum))
    return out


def rango_del_nombre(ruta):
    """cinta-<inst>-<desde>-<hasta>.csv (UTC yyyyMMddHHmm) -> (inst, desde, hasta); None si el nombre no es de la sonda."""
    m = re.match(r"cinta-(.+)-(\d{12})-(\d{12})\.csv$", os.path.basename(ruta))
    if not m:
        return None
    return m.group(1), datetime.strptime(m.group(2), "%Y%m%d%H%M"), datetime.strptime(m.group(3), "%Y%m%d%H%M")


def auditar(ruta_ola, ruta_cinta, max_difs=25, callado=False):
    t0 = time.time()
    meta, P, F = leer_ola(ruta_ola)
    difs, avisos = [], []
    nombre = os.path.basename(ruta_ola)
    if meta.get("hay_motor") != "1":
        difs.append("la carga no tiene motor (%s: %s)" % (meta.get("estado"), meta.get("motivo")))
        return difs, avisos, {}
    X = float(meta["x"])
    desde, hasta = _hora(meta["desde"]), _hora(meta["hasta"])
    inicio = _hora(meta["inicio_motor"]) or desde
    t_fin = _hora(meta["t_ultima"])
    n_ind = int(meta["n"])
    solo_vivo = meta.get("costura_tipo") in ("sin cinta", "sin respuesta")
    n_inicio = int(meta.get("n_inicio") or 0)
    # la cinta tiene que ser de la MISMA jornada y del mismo instrumento
    rn = rango_del_nombre(ruta_cinta)
    if rn:
        inst_c, d_c, h_c = rn
        if inst_c.replace("#", "").strip() != meta.get("inst", inst_c).replace("#", "").strip():
            difs.append("la cinta es de %s y la ola de %s" % (inst_c, meta.get("inst")))
        if d_c > inicio or (t_fin is not None and h_c < min(hasta, t_fin)):
            difs.append("la cinta (%s a %s UTC) no cubre la ola (%s a %s)" % (d_c, h_c, inicio, t_fin))
    else:
        avisos.append("el nombre de la cinta no es de la sonda: no se controla que sea de la misma jornada")
    # controles del csv
    for k, nom in (("cortes_tarde_x", "cortes tarde de la ola"), ("cortes_tarde_4x", "cortes tarde de la marea"), ("viejas", "actualizaciones viejas"),
                   ("cortes_forzados", "cortes forzados"), ("cortes_corridos", "cortes corridos")):
        if int(meta.get(k) or 0) != 0:
            difs.append("%s: %s" % (nom, meta[k]))
    if meta.get("costura_dudosa") == "1":
        difs.append("costura dudosa (el historial y el bufer no traen las mismas ordenes en el milisegundo de la costura)")
    if meta.get("sin_solape") == "1":
        avisos.append("sin solape entre el historial y el bufer: mercado quieto o un hueco (las fotos lo dicen)")
    primer, ultimo = int(meta["primer_bar"]), int(meta["ultimo_bar"])
    for esc in ("X", "4X"):
        filas = F[esc]
        if len(filas) != ultimo - primer + 1:
            difs.append("%s: %d filas de fotos para %d velas" % (esc, len(filas), ultimo - primer + 1))
        for i, (k, bar, corte, tomada, _) in enumerate(filas):
            if k != i or bar != primer + k:
                difs.append("%s: la fila %d es la vela %d con k %d (esperada %d)" % (esc, i, bar, k, primer + i))
                break
    # cortes: los mismos para las dos escalas
    cortes = [_hora(c) for (_, _, c, _, _) in F["X"] if c]
    cortes4 = [_hora(c) for (_, _, c, _, _) in F["4X"] if c]
    if cortes != cortes4:
        difs.append("los cortes de la ola y de la marea no son los mismos")
    vacias = [i for i, (_, _, c, _, _) in enumerate(F["X"]) if not c]
    if vacias and vacias[0] != len(F["X"]) - len(vacias):
        difs.append("hay filas sin corte antes de la ultima (la %d)" % vacias[0])
    for a, b in zip(cortes, cortes[1:]):
        if not b > a:
            difs.append("cortes no crecientes: %s y %s" % (a, b))
            break
    # la referencia
    cinta = leer_cinta(ruta_cinta)
    r = Rehacer(X, cortes, len(F["X"]))
    saltear = 0
    if solo_vivo and inicio is not None:
        en_inicio = sum(1 for o in cinta if o[0] == inicio)
        saltear = max(0, en_inicio - n_inicio)
    usadas = 0
    for (t, a, b, v, lado) in cinta:
        if t < inicio or t >= hasta:
            continue
        if t_fin is not None and t > t_fin:
            continue
        if saltear and t == inicio:
            saltear -= 1
            continue
        if t_fin is not None and t == t_fin and r.n >= n_ind:
            continue   # en el ultimo milisegundo, las que el indicador todavia no tenia
        r.orden(t, a, b, v, lado)
        usadas += 1
    r.cerrar()
    if r.n != n_ind:
        difs.append("ordenes: el indicador tiene %d y la cinta %d en el mismo tramo" % (n_ind, r.n))
    # comparar
    ncam = npiv = nfot = 0
    for esc in ("X", "4X"):
        et = "%s %s" % (nombre, esc)
        a, b = T.comparar(et + " pivotes", [p for (_, p) in P[esc]], pivotes_ref(r, esc), T.CAMPOS_P, difs)
        npiv += a
        ncam += b
        cs = [(c, fila) for (_, _, c, _, fila) in F[esc]]
        py = [(T.iso(c) if c is not None else None, f) for c, f in zip(cortes + [None] * (len(F[esc]) - len(cortes)), r.fotos[esc])]
        a, b = T.comparar(et + " fotos", cs, py, T.CAMPOS_F, difs, con_clave=True)
        nfot += a
        ncam += b
    res = {"x": X, "estado": meta.get("estado"), "costura": meta.get("costura_tipo"), "ordenes": n_ind, "pivotes": npiv, "fotos": nfot,
           "campos": ncam, "difs": len(difs), "seg": time.time() - t0, "tomadas": meta.get("tomadas_x"), "velas": len(F["X"])}
    if not callado:
        print("%s | X %s | %s | costura %s | ordenes %d | pivotes %d, fotos %d (%s tomadas), campos %d | difs %d | %.1f s"
              % (nombre, meta["x"], meta.get("estado"), meta.get("costura_tipo"), n_ind, npiv, nfot, meta.get("tomadas_x"), ncam, len(difs), res["seg"]), flush=True)
        for x in avisos:
            print("    AVISO", x)
        for x in difs[:max_difs]:
            print("    DIF", x)
    return difs, avisos, res


def main():
    args = sys.argv[1:]
    max_difs = 25
    if "--max-difs" in args:
        i = args.index("--max-difs")
        max_difs = int(args[i + 1])
        del args[i:i + 2]
    # como test_ola.py: si el original de la demo esta a mano, se controla que la Maquina sea la misma
    cand = sorted(glob.glob(os.path.join(tempfile.gettempdir(), "claude", "*", "*", "scratchpad", "ola_demo_datos.py")), key=os.path.getmtime)
    print("referencia: la Maquina de test_ola.py; " + T.verificar_copia(cand[-1] if cand else None), flush=True)
    if len(args) == 2 and args[0] == "--lista":
        malos = 0
        tot = {"pivotes": 0, "fotos": 0, "campos": 0}
        with open(args[1], encoding="utf-8") as fh:
            pedidos = [l.rstrip("\n").split("\t") for l in fh if l.strip()]
        for csv_ola, cinta, tipo in pedidos:
            if T.ram_libre_mb() < 2048:
                print("FRENO: RAM libre %.0f MB (< 2 GB). Lo que falta no se audito." % T.ram_libre_mb(), flush=True)
                return 1
            difs, _, res = auditar(csv_ola, cinta, max_difs=max_difs if tipo == "positivo" else 3)
            for k in tot:
                tot[k] += res.get(k, 0)
            if tipo == "positivo" and difs:
                malos += 1
            if tipo == "negativo":
                print("    -> control negativo: %s" % ("el auditor ENCONTRO las diferencias (bien)" if difs else "NO encontro diferencias (MAL)"), flush=True)
                if not difs:
                    malos += 1
        print("\nTOTAL: %d csv, %d pivotes y %d fotos comparados (%d campos); %s"
              % (len(pedidos), tot["pivotes"], tot["fotos"], tot["campos"], "todo como se esperaba" if malos == 0 else "%d csv MAL" % malos))
        return 0 if malos == 0 else 1
    if len(args) != 2:
        raise SystemExit(__doc__)
    difs, _, _ = auditar(args[0], args[1], max_difs=max_difs)
    return 0 if not difs else 1


if __name__ == "__main__":
    sys.exit(main())
