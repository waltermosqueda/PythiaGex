# -*- coding: utf-8 -*-
"""e02_placebos.py — esceptico2: intenta TUMBAR cada resultado "le gana al placebo" con 5 lentes, usando el juez propio (e01, que
reproduce exacto los conteos de las familias) para poder abrir el placebo por noche y por tipo.
  A. placebo oficial (pool: misma vida/parpadeo/deriva de cada raya, distancia al nacer sorteada del conjunto real, signo al azar),
     re-corrido con el motor propio y semilla propia -> p por permutacion (comprueba el p informado).
  B. AGRUPAMIENTO POR NOCHE: el placebo oficial sortea cada raya por separado, asi que trata las llegadas como independientes. Si las
     llegadas reales de una noche se mueven juntas (mismo tramo de mercado, re-toques de la misma raya, dos rayas pegadas), la varianza
     real es mayor y el p por permutacion sale chico de mas. Medida: exceso por noche d_n = aciertos_n - base_n x q_n (q_n = tasa del
     azar en ESA noche), error estandar robusto por noche (z_cluster) y test de signos al azar sobre las noches (p_noches).
  C. PLACEBO RIGIDO POR NOCHE: todas las rayas de una noche corridas el MISMO delta (|delta| ~ U[8, 40], signo al azar, distinto por
     noche): conserva la estructura entre rayas (pegadas, re-toques, densidad) -> p_rigido.
  D. TIPO DE LLEGADA (deriva del mercado): tasa del azar por techo y por piso; esperado = n_techos x q_techo + n_pisos x q_piso.
  E. DOBLES CONTEOS: llegadas del mismo tipo a <= 2 velas y <= 6 pts se cuentan UNA vez (la primera), en el real y en cada juego.
Uso: python -I e02_placebos.py <i> <n> [n_juegos]   (casos i, i+n, ...; maximo 3 procesos) -> datos/e02_<caso>.json"""
import json
import math
import os
import sys
import time

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import e00_comun as E  # noqa: E402
E.prioridad_baja()
import e01_juez_propio as P  # noqa: E402

import numpy as np  # noqa: E402

SEMILLA = 909102026


def dedup(evs):
    out = []
    ult = {}
    for e in evs:                       # evs ordenados por (i, raya)
        k = e["tipo"]
        dup = False
        for (i0, L0) in ult.get(k, []):
            if e["i"] - i0 <= 2 and abs(e["raya"] - L0) <= 6.0:
                dup = True; break
        if not dup:
            out.append(e)
        ult.setdefault(k, []).append((e["i"], e["raya"]))
        ult[k] = [x for x in ult[k] if e["i"] - x[0] <= 2]
    return out


def conteo(evs, noches):
    pos = {n: j for j, n in enumerate(noches)}
    a = np.zeros(len(noches)); b = np.zeros(len(noches))
    at = {"techo": [0, 0], "piso": [0, 0]}
    for e in evs:
        if e["res"] == "censurada" or e["noche"] not in pos:
            continue
        j = pos[e["noche"]]
        b[j] += 1; a[j] += e["res"] == "acierto"
        at[e["tipo"]][1] += 1; at[e["tipo"]][0] += e["res"] == "acierto"
    return a, b, at


def pval(nulo, x):
    nulo = np.asarray(nulo, float); nulo = nulo[np.isfinite(nulo)]
    return (1.0 + (nulo >= x).sum()) / (len(nulo) + 1.0), 100.0 * ((nulo < x).sum() + 0.5 * (nulo == x).sum()) / len(nulo)


def correr(nombre, n_juegos):
    t0 = time.time()
    cs = E.caso(nombre)
    Vx = P.velas(cs["V"], cs["ventanas"])
    PS = P.pistas(Vx, cs["rayas"], cs["opciones"].get("desfase_min", 1), cs["opciones"].get("claves"))
    real = P.juzgar(Vx, PS)
    noches = sorted(set(Vx["noche"][Vx["en"]].tolist()))
    a_r, b_r, at_r = conteo(real, noches)
    pct_r = 100 * a_r.sum() / b_r.sum()
    rd = dedup(real); ad_r, bd_r, _ = conteo(rd, noches)
    pctd_r = 100 * ad_r.sum() / bd_r.sum()
    # distancias al nacer (como juez_operador._offsets_azar)
    d0 = np.array([vs[0] - Vx["o"][ix[0]] for ix, vs in PS], float)
    ad = np.abs(d0)
    noche_p = np.array([Vx["noche"][ix[0]] for ix, _ in PS])
    un = sorted(set(noche_p.tolist()))
    rng = np.random.default_rng(SEMILLA)
    A_pool = []; B_pool = []; AT_pool = []; PCT_pool = []; PCTD_pool = []
    PCT_rig = []
    for g in range(n_juegos):
        off = rng.choice([-1.0, 1.0], len(ad)) * rng.choice(ad, len(ad), replace=True) - d0
        ev = P.juzgar(Vx, PS, off)
        a, b, at = conteo(ev, noches)
        A_pool.append(a); B_pool.append(b); AT_pool.append(at)
        PCT_pool.append(100 * a.sum() / b.sum() if b.sum() else np.nan)
        evd = dedup(ev); a2, b2, _ = conteo(evd, noches)
        PCTD_pool.append(100 * a2.sum() / b2.sum() if b2.sum() else np.nan)
        # rigido por noche
        dl = {n: rng.choice([-1.0, 1.0]) * rng.uniform(8.0, 40.0) for n in un}
        off2 = np.array([dl[n] for n in noche_p], float)
        ev2 = P.juzgar(Vx, PS, off2)
        a3, b3, _ = conteo(ev2, noches)
        PCT_rig.append(100 * a3.sum() / b3.sum() if b3.sum() else np.nan)
    A = np.array(A_pool); B = np.array(B_pool)
    # A. placebo oficial (motor propio)
    pA, pctlA = pval(PCT_pool, pct_r)
    # B. por noche
    qn = np.where(B.sum(0) > 0, A.sum(0) / np.maximum(B.sum(0), 1e-9), np.nan)
    ok = b_r > 0
    q_glob = A.sum() / B.sum()
    qn = np.where(np.isfinite(qn), qn, q_glob)
    d = a_r - b_r * qn
    exceso = d.sum() / b_r.sum()
    Nn = int(ok.sum())
    r = d - b_r * exceso
    se = math.sqrt(Nn / max(Nn - 1, 1) * (r[ok] ** 2).sum()) / b_r.sum()
    z = exceso / se if se > 0 else float("nan")
    p_z = 0.5 * math.erfc(z / math.sqrt(2)) if z == z else float("nan")
    rs = np.random.default_rng(SEMILLA + 1)
    dd = d[ok]
    flips = np.array([(rs.choice([-1.0, 1.0], len(dd)) * dd).sum() for _ in range(20000)])
    p_noches = (1.0 + (flips >= dd.sum()).sum()) / (len(flips) + 1.0)
    sd_juego = float(np.nanstd(PCT_pool, ddof=1))
    se_cluster = 100 * se
    noches_pos = int((dd > 0).sum()); noches_neg = int((dd < 0).sum())
    # C. rigido
    pC, pctlC = pval(PCT_rig, pct_r)
    # D. tipo
    qt = {k: sum(x[k][0] for x in AT_pool) / max(sum(x[k][1] for x in AT_pool), 1) for k in ("techo", "piso")}
    esperado_tipo = 100 * (at_r["techo"][1] * qt["techo"] + at_r["piso"][1] * qt["piso"]) / max(at_r["techo"][1] + at_r["piso"][1], 1)
    pct_tipo_r = {k: 100 * at_r[k][0] / at_r[k][1] if at_r[k][1] else float("nan") for k in at_r}
    pct_tipo_az = {k: 100 * qt[k] for k in qt}
    frac_techo_real = at_r["techo"][1] / max(at_r["techo"][1] + at_r["piso"][1], 1)
    frac_techo_azar = sum(x["techo"][1] for x in AT_pool) / max(sum(x["techo"][1] + x["piso"][1] for x in AT_pool), 1)
    # E. dedup
    pE, pctlE = pval(PCTD_pool, pctd_r)
    out = dict(caso=nombre, n_juegos=n_juegos, noches=noches, n_noches_con_llegadas=Nn, n_pistas=len(PS),
               real=dict(llegadas=len(real), base=int(b_r.sum()), aciertos=int(a_r.sum()), pct=pct_r),
               informado=E.informado(nombre),
               A_pool=dict(p50=float(np.nanmedian(PCT_pool)), media=float(np.nanmean(PCT_pool)), sd=sd_juego, p=pA, percentil=pctlA),
               B_noche=dict(q_azar_global=100 * q_glob, exceso_pp=100 * exceso, se_cluster_pp=se_cluster, z=z, p_normal_cluster=p_z,
                            p_signos_noches=p_noches, noches_exceso_pos=noches_pos, noches_exceso_neg=noches_neg,
                            inflacion_varianza=(se_cluster / sd_juego) ** 2 if sd_juego > 0 else float("nan"),
                            exceso_por_noche={n: float(x) for n, x in zip(noches, d)}, base_por_noche={n: int(x) for n, x in zip(noches, b_r)}),
               C_rigido=dict(p50=float(np.nanmedian(PCT_rig)), sd=float(np.nanstd(PCT_rig, ddof=1)), p=pC, percentil=pctlC),
               D_tipo=dict(pct_real=pct_tipo_r, pct_azar=pct_tipo_az, n_real={k: at_r[k][1] for k in at_r},
                           frac_techo_real=frac_techo_real, frac_techo_azar=frac_techo_azar, esperado_por_tipo=esperado_tipo,
                           exceso_ajustado_pp=pct_r - esperado_tipo),
               E_dedup=dict(llegadas_real=len(rd), duplicadas_real=len(real) - len(rd), pct_real=pctd_r,
                            p50=float(np.nanmedian(PCTD_pool)), p=pE, percentil=pctlE),
               seg=round(time.time() - t0, 1))
    with open(os.path.join(AQUI, "datos", "e02_%s.json" % nombre.replace("|", "__")), "w", encoding="utf-8") as fh:
        json.dump(out, fh, ensure_ascii=False, indent=1, default=float)
    print("%-34s real %5.1f%% (n %d) | A pool p50 %5.1f sd %.2f p %.4f | B exceso %+5.1f pp se_noche %.2f (infl %.1fx) z %.2f p %.4f "
          "p_signos %.4f (%d+/%d-) | C rigido p50 %5.1f p %.4f | D techo %4.1f/%4.1f piso %4.1f/%4.1f esperado %5.1f -> %+4.1f | "
          "E dedup %d dup, %5.1f vs %5.1f p %.4f | %.0f s" % (
              nombre, pct_r, int(b_r.sum()), out["A_pool"]["p50"], sd_juego, pA, 100 * exceso, se_cluster,
              out["B_noche"]["inflacion_varianza"], z, p_z, p_noches, noches_pos, noches_neg, out["C_rigido"]["p50"], pC,
              pct_tipo_r["techo"], pct_tipo_az["techo"], pct_tipo_r["piso"], pct_tipo_az["piso"], esperado_tipo, pct_r - esperado_tipo,
              len(real) - len(rd), pctd_r, out["E_dedup"]["p50"], pE, out["seg"]), flush=True)


def main():
    i, n = int(sys.argv[1]), int(sys.argv[2])
    nj = int(sys.argv[3]) if len(sys.argv) > 3 else 1000
    casos = list(E.CASOS)[i::n]
    for c in casos:
        correr(c, nj)


if __name__ == "__main__":
    main()
