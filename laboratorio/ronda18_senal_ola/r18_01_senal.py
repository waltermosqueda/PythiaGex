# -*- coding: utf-8 -*-
"""Ronda 18: la señal de entrada y salida con la ola (X) y la marea (4X), medida orden por orden. Ver PREREGISTRO.md.
Las maquinas son la clase Maquina de la referencia (la misma semantica que el motor del Superindicador 2.0a).
Uso: python -I r18_01_senal.py   (escribe operaciones.csv y resumen en pantalla)"""
import csv, os, math, statistics as st

FLUJO = os.path.join(os.environ['APPDATA'], 'ATAS', 'PythiaGex', 'flujo')
AQUI = os.path.dirname(os.path.abspath(__file__))
# (muestra, instrumento, dia, archivo de la cinta (jornada 18:00 NY de la vispera a 17:00 NY), X)
RUEDAS = [
    ('diseno', 'MES', '2026-11-16', 'cinta-MES-202611152300-202611162200.csv', 6.75),
    ('diseno', 'MES', '2026-11-17', 'cinta-MES-202611162300-202611172200.csv', 6.75),
    ('diseno', 'MES', '2026-11-18', 'cinta-MES-202611172300-202611182200.csv', 8.50),
    ('diseno', 'MES', '2026-11-19', 'cinta-MES-202611182300-202611192200.csv', 8.50),
    ('diseno', 'MES', '2026-11-20', 'cinta-MES-202611192300-202611202200.csv', 8.50),
    ('diseno', 'MES', '2026-11-23', 'cinta-MES-202611222300-202611232200.csv', 9.75),
    ('diseno', 'MES', '2026-11-24', 'cinta-MES-202611232300-202611242200.csv', 9.75),
    ('diseno', 'MES', '2026-11-25', 'cinta-MES-202611242300-202611252200.csv', 9.75),
    ('confirma', 'ES', '2026-11-02', 'cinta-ES-202611012300-202611022200.csv', 4.75),
    ('confirma', 'ES', '2026-11-03', 'cinta-ES-202611022300-202611032200.csv', 5.00),
    ('confirma', 'ES', '2026-11-04', 'cinta-ES-202611032300-202611042200.csv', 4.75),
    ('confirma', 'ES', '2026-11-06', 'cinta-ES-202611052300-202611062200.csv', 5.00),
    ('confirma', 'ES', '2026-11-09', 'cinta-ES-202611082300-202611092200.csv', 5.25),
    ('confirma', 'ES', '2026-11-10', 'cinta-ES-202611092300-202611102200.csv', 5.75),
    ('confirma', 'ES', '2026-11-11', 'cinta-ES-202611102300-202611112200.csv', 6.00),
    ('confirma', 'ES', '2026-11-12', 'cinta-ES-202611112300-202611122200.csv', 6.25),
    ('confirma', 'ES', '2026-11-13', 'cinta-ES-202611122300-202611132200.csv', 6.75),
]
REGLAS = ['R1', 'R2', 'R3', 'R4', 'V1', 'V2', 'V3', 'V4']   # V1-V4: enmienda 1 del pre-registro
COSTOS = (0.50, 1.00)


class Maquina:
    """Copia de la referencia (ola_demo_datos.py): zigzag en vivo, cum incluido en cada extremo."""
    def __init__(s, X):
        s.X = X; s.dir = 0; s.ext = None; s.ext_t = None; s.ext_cum = 0
        s.lo = s.hi = None; s.lo_t = s.hi_t = None; s.lo_cum = s.hi_cum = 0
        s.pivotes = []
    def paso(s, t, mn, mx, cum):
        if s.dir == 0:
            if s.lo is None or mn < s.lo: s.lo, s.lo_t, s.lo_cum = mn, t, cum
            if s.hi is None or mx > s.hi: s.hi, s.hi_t, s.hi_cum = mx, t, cum
            if s.hi - s.lo >= s.X:
                if s.hi_t > s.lo_t: s.pivotes.append((s.lo_t, s.lo, 'min', t, s.lo_cum)); s.dir = 1; s.ext, s.ext_t, s.ext_cum = s.hi, s.hi_t, s.hi_cum
                else: s.pivotes.append((s.hi_t, s.hi, 'max', t, s.hi_cum)); s.dir = -1; s.ext, s.ext_t, s.ext_cum = s.lo, s.lo_t, s.lo_cum
            return
        if s.dir == 1:
            if mx > s.ext: s.ext, s.ext_t, s.ext_cum = mx, t, cum
            elif s.ext - mn >= s.X:
                s.pivotes.append((s.ext_t, s.ext, 'max', t, s.ext_cum)); s.dir = -1; s.ext, s.ext_t, s.ext_cum = mn, t, cum
        else:
            if mn < s.ext: s.ext, s.ext_t, s.ext_cum = mn, t, cum
            elif mx - s.ext >= s.X:
                s.pivotes.append((s.ext_t, s.ext, 'min', t, s.ext_cum)); s.dir = 1; s.ext, s.ext_t, s.ext_cum = mx, t, cum


def correr(muestra, inst, dia, archivo, X):
    ola, marea = Maquina(X), Maquina(4 * X)
    ent0, ent1, cierre = dia + 'T14:30:00', dia + 'T20:55:00', dia + 'T21:00:00'   # 9:30, 15:55 y 16:00 NY (EST)
    pos = {r: None for r in REGLAS}            # (lado, t_entrada, precio_entrada, contexto)
    ops = []
    cum = 0.0; ult = None; t_ult = None; npiv = 0; nmar = 0
    v2_pierna = -1                              # V2: pierna de la marea (cantidad de pivotes) en la que ya se entro
    v4_fin = dia + 'T16:00:00'                  # V4: entradas hasta las 11:00 NY
    def cerrar(r, t, p, motivo):
        lado, t0, p0, ctx = pos[r]
        ops.append(dict(muestra=muestra, inst=inst, dia=dia, X=X, regla=r, lado=lado, t_in=t0, p_in=p0, t_out=t, p_out=p,
                        pts=(p - p0) * lado, motivo=motivo, **ctx))
        pos[r] = None
    with open(os.path.join(FLUJO, archivo), newline='') as f:
        rd = csv.reader(f); next(rd)
        for x in rd:
            t = x[0]
            if t >= cierre and t_ult is not None and t_ult < cierre:
                for r in REGLAS:
                    if pos[r]: cerrar(r, t_ult, ult, 'hora')
            a, b = float(x[1]), float(x[2]); v = float(x[3]); s = int(x[4])
            mn, mx = (a, b) if a <= b else (b, a)
            cum += v * s
            ola.paso(t, mn, mx, cum); marea.paso(t, mn, mx, cum)
            ult, t_ult = b, t
            if len(marea.pivotes) != nmar:              # V1: la MAREA se dio vuelta
                nmar = len(marea.pivotes)
                mte, mpp, mtipo, mtc, mc = marea.pivotes[-1]
                mg = 1 if mtipo == 'min' else -1
                mniv = mpp + 4 * X if mg == 1 else mpp - 4 * X
                if pos['V1'] and pos['V1'][0] == -mg: cerrar('V1', t, mniv, 'marea')
                if not pos['V1'] and ent0 <= t < ent1:
                    pos['V1'] = (mg, t, mniv, dict(marea_dir=mg, marea_delta=0, marea_con=True, origen=mpp, t_origen=mte))
            if len(ola.pivotes) == npiv: continue
            npiv = len(ola.pivotes)
            te, pp, tipo, tc, c = ola.pivotes[-1]
            giro = 1 if tipo == 'min' else -1              # +1: la ola se dio vuelta para arriba
            nivel = pp + X if giro == 1 else pp - X        # la raya que se cruzo: precio de la orden stop
            mdir = marea.dir
            mdel = (cum - marea.pivotes[-1][4]) if marea.pivotes else 0.0
            mcon = (mdir == 1 and mdel > 0) or (mdir == -1 and mdel < 0)
            ventana = ent0 <= t < ent1
            ctx = dict(marea_dir=mdir, marea_delta=round(mdel), marea_con=mcon, origen=pp, t_origen=te)
            r1 = mdir == giro and mcon
            # V3: la ola en contra que acaba de terminar (del pivote anterior a este) no tuvo delta en contra de la marea
            sin_agr = len(ola.pivotes) >= 2 and (c - ola.pivotes[-2][4]) * giro >= 0
            for r in REGLAS:
                if r == 'V1': continue
                if pos[r] and pos[r][0] == -giro: cerrar(r, t, nivel, 'ola')      # la ola se dio vuelta en contra
                if pos[r] or not ventana: continue
                quiere = (r == 'R1' and r1) or (r == 'R2' and mdir == giro) or r == 'R3' or (r == 'R4' and mdir == -giro) or \
                         (r == 'V2' and r1 and v2_pierna != nmar) or (r == 'V3' and r1 and sin_agr) or (r == 'V4' and r1 and t < v4_fin)
                if quiere:
                    pos[r] = (giro, t, nivel, ctx)
                    if r == 'V2': v2_pierna = nmar
    for r in REGLAS:
        if pos[r]: cerrar(r, t_ult, ult, 'fin')
    return ops


def resumen(ops, etiqueta):
    print(f'\n== {etiqueta} ==')
    for r in REGLAS:
        o = [q for q in ops if q['regla'] == r]
        if not o: print(f'  {r}: sin operaciones'); continue
        gan = [q['pts'] for q in o if q['pts'] > 0]; per = [q['pts'] for q in o if q['pts'] <= 0]
        enX = [q['pts'] / q['X'] for q in o]
        dias = sorted(set((q['inst'], q['dia']) for q in o))
        linea = f'  {r}: {len(o)} ops en {len(dias)} ruedas | ganadoras {100*len(gan)/len(o):.1f} % | gana media {st.mean(gan) if gan else 0:+.2f} pts, pierde media {st.mean(per) if per else 0:+.2f} pts | bruto/op {st.mean(q["pts"] for q in o):+.3f} pts ({st.mean(enX):+.3f} X)'
        for c in COSTOS:
            netos = [sum(q['pts'] - c for q in o if (q['inst'], q['dia']) == d) for d in dias]
            m = st.mean(netos); sd = st.stdev(netos) if len(netos) > 1 else 0.0
            tt = m / (sd / math.sqrt(len(netos))) if sd > 0 else 0.0
            pos_d = sum(1 for n in netos if n > 0)
            linea += f' | costo {c:.2f}: neto/op {st.mean(q["pts"] - c for q in o):+.3f}, por rueda {m:+.1f} (t {tt:+.2f}, {pos_d}/{len(netos)} ruedas +)'
        print(linea)


if __name__ == '__main__':
    todas = []
    for (mu, inst, dia, arch, X) in RUEDAS:
        o = correr(mu, inst, dia, arch, X); todas += o
        r1 = [q for q in o if q['regla'] == 'R1']
        print(f'{inst} {dia} X {X}: R1 {len(r1)} ops, bruto {sum(q["pts"] for q in r1):+.2f} pts, neto(0,5) {sum(q["pts"] - 0.5 for q in r1):+.2f}')
    with open(os.path.join(AQUI, 'operaciones.csv'), 'w', newline='') as f:
        w = csv.DictWriter(f, fieldnames=list(todas[0].keys())); w.writeheader(); w.writerows(todas)
    resumen([q for q in todas if q['muestra'] == 'diseno'], 'DISENO: MES 16 a 25-11 (8 ruedas)')
    resumen([q for q in todas if q['muestra'] == 'confirma'], 'CONFIRMACION: ES 02 a 13-11 (9 ruedas)')
    resumen(todas, 'TODO (17 ruedas)')
    # por que: largo de las piernas que se operaron con R1 (en X): gana si la pierna mide mas de 2X + costo
    o = [q for q in todas if q['regla'] == 'R1']
    largos = sorted(round((q['pts'] / q['X']) + 2, 2) for q in o)   # pierna origen->extremo en X = (pts + 2X) / X
    if largos:
        print('\nR1: largo de la pierna operada en X (origen al extremo): mediana %.2f, p25 %.2f, p75 %.2f; > 2X: %.1f %%; > 3X: %.1f %%'
              % (st.median(largos), largos[len(largos)//4], largos[3*len(largos)//4],
                 100 * sum(1 for l in largos if l > 2) / len(largos), 100 * sum(1 for l in largos if l > 3) / len(largos)))
