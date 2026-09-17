# -*- coding: utf-8 -*-
"""gamma_05_descriptivos.py — descriptivos DECLARADOS en el pre-registro (D1-D4). NO dan veredicto. Se corren DESPUES de confirmar las
finalistas, sobre las 20 sesiones (y por mitad), para LEER la microestructura:
  D1 velocidad de la barrera +-8 por regimen; D2 continuacion de las rafagas por regimen y por cuadrante; D3 absorcion cerca / lejos de una
  dominante; D4 regimen del dia contra continuacion del dia (la muestra efectiva son DIAS)."""
import sys, time; sys.path.insert(0, r"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\gatillo")
import numpy as np, pandas as pd, base as B, gamma_lib as L
from scipy import stats

t0 = time.time(); T = B.todas_las_sesiones(); EXP, CONF = L.mitades(T); mitad_de = {s: ("EXP" if s in EXP else "CONF") for s in T}
DISP, OK, MH, Y, SEG, NV = {}, {}, {}, {}, {}, {}
for s, d in T.items():
    disp, f, nv = L.disparos(d); DISP[s] = disp; OK[s] = f["elegible"].to_numpy(); MH[s] = L.media_hora(d); NV[s] = nv
    Y[s], SEG[s] = L.barrera_cache(s, d, 8, 600)
print("cargado en %.0f s" % (time.time() - t0))

# ---------------- D1 velocidad
print("\nD1. VELOCIDAD de la barrera +-8 (segundos hasta tocar), muestreo cada 30 s de la rueda, por regimen del zero gamma")
filas = []
for s in sorted(T):
    i = np.flatnonzero(OK[s])[::30]; z = NV[s]["d_zero"].to_numpy()[i]; sg = SEG[s][i]; q = NV[s]["cuadrante"].to_numpy()[i]
    for nb, m in (("POS", z >= L.ZONA), ("NEG", z <= -L.ZONA)):
        if m.sum() >= 100: filas.append(dict(ses=s, mitad=mitad_de[s], reg=nb, n=int(m.sum()), mediana=float(np.median(np.where(np.isinf(sg[m]), 601, sg[m]))), en60=100 * float((sg[m] <= 60).mean())))
D1 = pd.DataFrame(filas)
for nb in ("POS", "NEG"):
    x = D1[D1["reg"] == nb]; print("  %s: %2d dias | mediana de las medianas diarias %5.0f s (rango %3.0f-%3.0f) | resuelto en <= 60 s: %4.1f %% (rango %2.0f-%2.0f)" % (nb, len(x), x["mediana"].median(), x["mediana"].min(), x["mediana"].max(), x["en60"].median(), x["en60"].min(), x["en60"].max()))
mix = D1.pivot(index="ses", columns="reg", values="mediana").dropna()
print("  dias con los DOS regimenes (>= 100 muestras de cada uno): %d -> mediana POS / NEG por dia: %s" % (len(mix), " | ".join("%s %3.0f/%3.0f" % (k[5:], r["POS"], r["NEG"]) for k, r in mix.iterrows())))
if len(mix) >= 3: print("     POS mas lento que NEG en %d de %d dias" % (int((mix["POS"] > mix["NEG"]).sum()), len(mix)))
u = stats.mannwhitneyu(D1.loc[D1["reg"] == "POS", "mediana"], D1.loc[D1["reg"] == "NEG", "mediana"]); print("  Mann-Whitney entre dias (medianas diarias POS contra NEG): p = %.3f" % u.pvalue)


def grupo(v, cond_fn, lado_mult=1, mitad=None):
    """Junta los disparos del crudo v que cumplen cond_fn(nv, idx) y los juzga con lado = lado del flujo x lado_mult."""
    disp = {}
    for s in T:
        if mitad and mitad_de[s] != mitad: continue
        i, l = DISP[s][v]; m = cond_fn(NV[s], i); disp[s] = (i[m], l[m] * lado_mult)
    lado = np.concatenate([disp[s][1] for s in disp]); dias = np.concatenate([[s] * len(disp[s][0]) for s in disp]); y = np.concatenate([Y[s][disp[s][0]] for s in disp])
    if len(y) == 0 or (y != 0).sum() == 0: return None
    r = B.juzgar(y, lado, v, dias, 8); pm, ps = L.placebo(disp, OK, MH, Y, sorteos=200); r["pl"] = pm; r["zp"] = (r["acierto"] - pm) / ps if ps > 0 else np.nan
    mv = []
    for h in (10, 30, 60, 120, 300):
        tot = 0.0; n = 0
        for s in disp:
            p = T[s]["ultimo"].to_numpy(); i, l = disp[s]; ok = i + h < len(p); tot += float(((p[i[ok] + h] - p[i[ok]]) * l[ok]).sum()); n += int(ok.sum())
        mv.append(tot / max(n, 1))
    r["mov"] = mv; return r


def imprime(titulo, r):
    if r is None: print("  %-44s sin disparos" % titulo); return
    print("  %-44s n %4d | SEGUIR acierta %5.1f %% | placebo %5.1f -> z %+5.2f | dias > 50 %%: %s | mov 10/30/60/120/300 s: %s" % (titulo, r["n"], r["acierto"], r["pl"], r["zp"], r["dias_arriba"], " / ".join("%+.2f" % m for m in r["mov"])))


zpos = lambda nv, i: nv["d_zero"].to_numpy()[i] >= L.ZONA
zneg = lambda nv, i: nv["d_zero"].to_numpy()[i] <= -L.ZONA
c13 = lambda nv, i: np.isin(nv["cuadrante"].to_numpy()[i], (1, 3))
c24 = lambda nv, i: np.isin(nv["cuadrante"].to_numpy()[i], (2, 4))
todo = lambda nv, i: np.ones(len(i), bool)
print("\nD2. CONTINUACION de las rafagas (lado = SEGUIR el flujo), barrera +-8/600 s, las 20 sesiones y por mitad")
for v in ("RAF60", "RAF10", "D5M"):
    for nb, fn in (("todas", todo), ("zero POS (>= +25)", zpos), ("zero NEG (<= -25)", zneg), ("cuadrante 1/3 'iman/estable'", c13), ("cuadrante 2/4 'explosivo/riesgo'", c24)):
        imprime("%s | %s | 20 ses" % (v, nb), grupo(v, fn))
        if nb.startswith("zero"):
            for mt in ("EXP", "CONF"): imprime("%s | %s | %s" % (v, nb, mt), grupo(v, fn, mitad=mt))

print("\nD3. ABSORCION real de 30 s (percentil 95), lado = rebote (signo de A30): cerca de una dominante contra lejos")
imprime("ABS | todas", grupo("ABS_TODO", todo)); imprime("ABS | lejos (> 30 pts de toda dominante)", grupo("ABS_LEJOS", todo))
imprime("ABS | defendiendo una dominante (= V05)", grupo("V05_ABS_DOM", todo))

print("\nD4. REGIMEN DEL DIA contra CONTINUACION DEL DIA (una fila por dia; la muestra efectiva son 20 dias)")
for v in ("RAF60", "RAF10", "D5M"):
    fil = []
    for s in sorted(T):
        i, l = DISP[s][v]; y = Y[s][i]; ok = y != 0
        if ok.sum() < 8: continue
        e = OK[s]; z = NV[s]["d_zero"].to_numpy()[e]
        fil.append(dict(ses=s, cont=100 * float(((y[ok] * l[ok]) > 0).mean()), n=int(ok.sum()), pos=100 * float(np.nanmean(z >= L.ZONA)), zmed=float(np.nanmedian(z))))
    F = pd.DataFrame(fil); rho, p = stats.spearmanr(F["pos"], F["cont"])
    print("  %s: Spearman(%% del dia en gamma POS, %% de continuacion) = %+.2f (p %.2f, %d dias) | dias POS>=50 %%: cont media %.1f %% (%d dias) | dias POS<5 %%: cont media %.1f %% (%d dias)" % (
        v, rho, p, len(F), F.loc[F["pos"] >= 50, "cont"].mean(), int((F["pos"] >= 50).sum()), F.loc[F["pos"] < 5, "cont"].mean(), int((F["pos"] < 5).sum())))
    print("     " + " | ".join("%s %2.0f%%pos c%2.0f" % (r["ses"][5:], r["pos"], r["cont"]) for _, r in F.iterrows()))
print("\n%.0f s" % (time.time() - t0))
