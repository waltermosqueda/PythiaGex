"""Prueba INDEPENDIENTE de la division por JORNADA del Superindicador 1.5 (26-11-2026).

Lee los casos que escribe atas/_test_super (velas en UTC como las da ATAS + lo que calculo SuperCalculos.cs con la zona de
Windows) y lo rehace en Python con OTRA base de husos horarios (zoneinfo America/New_York), sin mirar el C#:
  - jornada de CME de cada vela por la hora NY de su apertura: desde las 18:00 cuenta para el dia siguiente; sabado y domingo
    van al lunes
  - partes: RTH 9:30-16:00, despues del cierre 16:00-18:00, noche el resto
  - lista ordenada de jornadas con primera/ultima vela de la jornada, de la noche y de la RTH, rotulo y hora de comienzo
  - "a la vista": la jornada con mas velas visibles de la parte pedida (empate: la mas nueva); si ninguna, la de mas velas visibles
  - "N atras": la ultima menos N, sin bajar de la primera
Uso: python -I herramientas/test_jornadas.py <jornadas.json>
"""
import json
import sys
from datetime import datetime, timedelta, time, timezone
from zoneinfo import ZoneInfo

NY = ZoneInfo("America/New_York")
DIAS = ["lun", "mar", "mie", "jue", "vie", "sab", "dom"]   # weekday() de Python: lunes = 0


def clave_y_tipo(utc_txt):
    h = datetime.strptime(utc_txt, "%Y-%m-%dT%H:%M").replace(tzinfo=timezone.utc).astimezone(NY)
    t = h.time()
    d = h.date() + timedelta(days=1) if t >= time(18, 0) else h.date()
    if d.weekday() == 5:
        d += timedelta(days=2)
    elif d.weekday() == 6:
        d += timedelta(days=1)
    tipo = 1 if time(9, 30) <= t < time(16, 0) else (2 if time(16, 0) <= t < time(18, 0) else 0)
    return d, tipo


def lista_de(utcs):
    js = {}
    for b, u in enumerate(utcs):
        d, tipo = clave_y_tipo(u)
        j = js.setdefault(d, {"p": b, "u": b, "np": -1, "nu": -1, "rp": -1, "ru": -1})
        j["p"] = min(j["p"], b); j["u"] = max(j["u"], b)
        if tipo == 0:
            j["np"] = b if j["np"] < 0 else min(j["np"], b); j["nu"] = max(j["nu"], b)
        elif tipo == 1:
            j["rp"] = b if j["rp"] < 0 else min(j["rp"], b); j["ru"] = max(j["ru"], b)
    out = []
    for d in sorted(js):
        j = js[d]
        ini_c = datetime.combine(d - timedelta(days=1), time(18, 0)); ini_r = datetime.combine(d, time(9, 30))
        out.append([d.year * 10000 + d.month * 100 + d.day, j["p"], j["u"], j["np"], j["nu"], j["rp"], j["ru"],
                    DIAS[d.weekday()] + " " + d.strftime("%d-%m"), ini_c.strftime("%Y-%m-%dT%H:%M"), ini_r.strftime("%Y-%m-%dT%H:%M")])
    return out


def rango(j, parte):
    if parte == 1:
        return j[5], j[6]
    if parte == 2:
        return j[3], j[4]
    return j[1], j[2]


def solape(a0, a1, b0, b1):
    if a0 < 0 or a1 < a0:
        return 0
    x0, x1 = max(a0, b0), min(a1, b1)
    return x1 - x0 + 1 if x1 >= x0 else 0


def a_la_vista(js, b0, b1, parte):
    if b1 < b0:
        return -1
    mejor, mejor_n, tot_i, tot_n = -1, 0, -1, 0
    for i, j in enumerate(js):
        t = solape(j[1], j[2], b0, b1)
        if t > 0 and t >= tot_n:
            tot_n, tot_i = t, i
        d, h = rango(j, parte)
        n = solape(d, h, b0, b1)
        if n > 0 and n >= mejor_n:
            mejor_n, mejor = n, i
    return mejor if mejor >= 0 else tot_i


def con_borde(js, b0, b1, ult, parte):
    """1.6: con la ultima vela a la vista, la ultima jornada que tiene esa parte; si no, la que mas se ve."""
    if not js:
        return -1
    if ult >= 0 and b1 >= ult:
        for i in range(len(js) - 1, -1, -1):
            d, h = rango(js[i], parte)
            if d >= 0 and h >= d:
                return i
        return len(js) - 1
    return a_la_vista(js, b0, b1, parte)


def al_momento(utcs, m, parte):
    """1.7, escrito de nuevo desde las horas: la jornada de la vela m; si su parte ya habia empezado a la altura de m, desde su
    comienzo hasta m (o su final); si no, la misma parte de la jornada anterior mas reciente que la tenga, completa."""
    marcas = [clave_y_tipo(u) for u in utcs]
    def entra(t):
        return True if parte == 0 else (t == 1 if parte == 1 else t == 0)
    claves = sorted({d for d, _ in marcas})
    cm = marcas[m][0]
    for c in reversed([c for c in claves if c <= cm]):
        barras = [b for b, (d, t) in enumerate(marcas) if d == c and entra(t)]
        if not barras or barras[0] > m:
            continue
        return claves.index(c), barras[0], min(barras[-1], m)
    return -1, -1, -1


def main(ruta):
    casos = json.load(open(ruta, encoding="utf-8"))
    malos = {"lista": 0, "vista": 0, "borde": 0, "momento": 0, "atras": 0}
    bordes = momentos = 0
    velas = vistas = 0
    raras = 0
    for c in casos:
        utcs = c["utc"]; velas += len(utcs)
        mia = lista_de(utcs)
        if mia != c["lista"]:
            malos["lista"] += 1
            if malos["lista"] <= 3:
                print("  lista distinta semilla", c["semilla"], "\n   py", mia[:3], "\n   cs", c["lista"][:3])
        for b0, b1, parte, cs in c["vista"]:
            vistas += 1
            if a_la_vista(mia, b0, b1, parte) != cs:
                malos["vista"] += 1
        for b0, b1, ult, parte, cs in c.get("borde", []):
            bordes += 1
            if con_borde(mia, b0, b1, ult, parte) != cs:
                malos["borde"] += 1
        for m, parte, im, dm, hm in c.get("momento", []):
            momentos += 1
            if al_momento(utcs, m, parte) != (im, dm, hm):
                malos["momento"] += 1
                if malos["momento"] <= 3:
                    print("  momento distinto semilla", c["semilla"], "m", m, "parte", parte, "py", al_momento(utcs, m, parte), "cs", (im, dm, hm))
        n = len(mia)
        esperado = [max(0, n - 1 - q) if n else -1 for q in range(n + 2)]
        if esperado != c["atras"]:
            malos["atras"] += 1
        for u in utcs:
            h = datetime.strptime(u, "%Y-%m-%dT%H:%M").replace(tzinfo=timezone.utc).astimezone(NY)
            if h.weekday() == 5 or (h.weekday() == 6 and h.time() < time(18, 0)) or time(17, 0) <= h.time() < time(18, 0):
                raras += 1
    total = sum(malos.values())
    print(f"casos {len(casos)} ({velas} velas, {raras} en horario cerrado a proposito, {vistas} consultas 'a la vista', {bordes} con la regla del borde, {momentos} 'al momento'): "
          f"{'TODO IGUAL' if total == 0 else 'DIFERENCIAS'} {malos}")
    return 0 if total == 0 else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1]))
