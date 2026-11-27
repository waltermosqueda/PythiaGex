# -*- coding: utf-8 -*-
"""RONDA 25, paso 3: CLASIFICACION de los seis detectores (PREREGISTRO_R25.md secciones 2-5 y enmienda 1).
  python -I r25_03_clasificar.py diseno   -> MES 30 min 29-04-2024..08-09-2025 (337 ruedas): la prueba principal y la ELECCION (una vez)
  python -I r25_03_clasificar.py val1     -> MES 5 min agregadas a 30, 09-09-2025..31-08-2026 (242 ruedas)
  python -I r25_03_clasificar.py val2     -> cinta de ES agregada a 30, 01-10..25-11-2026 (40 ruedas)
  python -I r25_03_clasificar.py explora  -> las 15 ruedas de exploracion (solo descriptivo)
Por detector: AUC del puntaje contra TA (principal), TIPO, NV y ET; p de una cola por permutacion (2.000), error de Hanley-McNeil, AUC del
azar (las 200 tardes reflejadas por rueda) y memoria = AUC real - AUC del azar; en todas las ruedas con s13 != 0 y en la "tarde con tension"
(|C13 - VWAP13| >= 1 X). Por marca (grilla): ruedas marcadas, base, precision, recall, lift y D medio con y sin marca.
Todo se anota en registro_variantes.csv. En el diseno ademas: la regla de eleccion (alfa 0,05/6 de una cola) -> elegido_r25.json."""
import json, os, pickle, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import r25_comun as K
import r25_rasgos as RS

DETS = ["APE", "OTF", "VWA", "ACE", "EXT", "CAL"]
OBJS = ["TA", "TIPO", "NV", "ET"]
MUESTRAS = {"diseno": ("m30", K.DISENO), "val1": ("m5", K.VAL1), "val2": ("cinta", K.VAL2), "explora": ("cinta", K.EXPLORA)}
L = []


def W(s=""):
    print(s, flush=True); L.append(s)


def cargar(nombre):
    fuente, per = MUESTRAS[nombre]
    R = pickle.load(open(os.path.join(K.SCR, "dias_%s.pkl" % fuente), "rb"))
    F, AZ = RS.tabla(R, per[0], per[1])
    F = F[F["s13"] != 0].reset_index(drop=True)
    return F, AZ


def auc_azar(score, dias, AZ, obj, real=None):
    """AUC del puntaje contra el resultado de cada una de las 200 tardes reflejadas: media, desvio y p de una cola (fraccion de tardes al
    azar con AUC >= el real): es la prueba de MEMORIA (lo que el detector ve mas alla de la ubicacion del precio a las 13:00)."""
    S = np.asarray(score, float)
    sims = np.stack([AZ[d][obj] for d in dias])            # (n_dias, M)
    a = np.array([K.auc(S, sims[:, m]) for m in range(sims.shape[1])])
    p = float(((a >= real - 1e-12).sum() + 1) / (len(a) + 1)) if real is not None and np.isfinite(real) else float("nan")
    return float(np.nanmean(a)), float(np.nanstd(a)), p


def evaluar(nombre, F, AZ, etapa, anotar=True):
    filas = []; marcas = []
    subs = {"todas": np.ones(len(F), bool), "tension": (F["dv13"] >= 1.0).to_numpy()}
    for sub, m in subs.items():
        G = F[m].reset_index(drop=True); dias = list(G["dia"])
        for det in DETS:
            sc = G["%s_score" % det].to_numpy(float)
            for obj in OBJS:
                y = G[obj].to_numpy(int)
                a = K.auc(sc, y); n1 = int(y.sum()); n0 = len(y) - n1
                p = K.p_perm_auc(sc, y, B=2000, semilla=2525 + DETS.index(det)) if np.isfinite(a) else np.nan
                az, azsd, paz = auc_azar(sc, dias, AZ, obj, real=a)
                filas.append(dict(muestra=nombre, subconjunto=sub, detector=det, objetivo=obj, n_dias=len(y), base_pct=100 * y.mean(),
                                  auc=a, auc_ee=K.auc_ee(a, n1, n0), p_perm=p, auc_azar=az, auc_azar_sd=azsd, memoria=a - az, p_azar=paz))
            # marcas de la grilla
            for par in RS.GRILLA[det]:
                col = "%s_%g" % (det, par) if det != "CAL" else "CAL_%s" % par
                fl = G[col].to_numpy(int) == 1
                fila = dict(muestra=nombre, subconjunto=sub, detector=det, variante=col, teoria=int(par == RS.TEORIA[det]), n_dias=len(G),
                            n_marca=int(fl.sum()), pct_marca=100 * fl.mean())
                for obj in OBJS:
                    y = G[obj].to_numpy(int) == 1
                    fila["base_%s" % obj] = 100 * y.mean()
                    fila["prec_%s" % obj] = 100 * y[fl].mean() if fl.any() else np.nan
                    fila["rec_%s" % obj] = 100 * (y & fl).sum() / y.sum() if y.any() else np.nan
                D = G["D"].to_numpy(float)
                fila["D_marca"] = D[fl].mean() if fl.any() else np.nan; fila["D_resto"] = D[~fl].mean() if (~fl).any() else np.nan
                fila["t_D"] = K.t_welch(D[fl], D[~fl]) if fl.sum() >= 2 and (~fl).sum() >= 2 else np.nan
                marcas.append(fila)
    T = pd.DataFrame(filas); Mk = pd.DataFrame(marcas)
    if anotar:
        reg = []
        for _, r in T.iterrows():
            reg.append(dict(etapa=etapa, detector=r.detector, variante="%s_score" % r.detector, parametros="puntaje continuo", muestra=nombre,
                            subconjunto=r.subconjunto, objetivo=r.objetivo, n_dias=r.n_dias, base_pct=round(r.base_pct, 1), auc=round(r.auc, 4),
                            auc_ee=round(r.auc_ee, 4), p_perm=round(r.p_perm, 4), auc_azar=round(r.auc_azar, 4), memoria=round(r.memoria, 4),
                            nota="AUC del azar = media de 200 tardes reflejadas; p de memoria (tardes al azar con AUC >= real) = %.4f" % r.p_azar))
        for _, r in Mk.iterrows():
            for obj in OBJS:
                lift = r["prec_%s" % obj] / r["base_%s" % obj] if r["base_%s" % obj] > 0 and np.isfinite(r["prec_%s" % obj]) else np.nan
                reg.append(dict(etapa=etapa, detector=r.detector, variante=r.variante, parametros="teoria" if r.teoria else "grilla", muestra=nombre,
                                subconjunto=r.subconjunto, objetivo=obj, n_dias=r.n_dias, n_marca=r.n_marca, base_pct=round(r["base_%s" % obj], 1),
                                precision_pct=round(r["prec_%s" % obj], 1) if np.isfinite(r["prec_%s" % obj]) else "",
                                recall_pct=round(r["rec_%s" % obj], 1) if np.isfinite(r["rec_%s" % obj]) else "", lift=round(lift, 3) if np.isfinite(lift) else "",
                                D_marca=round(r.D_marca, 3) if np.isfinite(r.D_marca) else "", D_resto=round(r.D_resto, 3) if np.isfinite(r.D_resto) else "",
                                t_D=round(r.t_D, 2) if np.isfinite(r.t_D) else ""))
        K.anotar(reg)
    return T, Mk


def imprimir(nombre, F, T, Mk):
    W("")
    W("== CLASIFICACION en %s: %d ruedas con s13 != 0 (%s a %s); 'tarde con tension' (|C13-VWAP13| >= 1 X): %d" % (
        nombre, len(F), F.dia.min(), F.dia.max(), int((F.dv13 >= 1).sum())))
    W("   bases: TA %.1f %% | TIPO (tendencia ex post) %.1f %% | NV (la tarde no toca el VWAP) %.1f %% | ET (tarde de tendencia) %.1f %% | D medio %+.2f X (t %.2f)" % (
        100 * F.TA.mean(), 100 * F.TIPO.mean(), 100 * F.NV.mean(), 100 * F.ET.mean(), F.D.mean(), K.t_rueda(F.D)))
    for sub in ("todas", "tension"):
        W("   -- AUC del puntaje (%s): real / azar / memoria (p contra 0,5 por permutacion; p de memoria contra las tardes al azar)" % sub)
        for det in DETS:
            fila = []
            for obj in OBJS:
                r = T[(T.subconjunto == sub) & (T.detector == det) & (T.objetivo == obj)].iloc[0]
                fila.append("%s %.3f/%.3f/%+.3f (p %.3f; mem %.3f)" % (obj, r.auc, r.auc_azar, r.memoria, r.p_perm, r.p_azar))
            W("      %-4s %s" % (det, " | ".join(fila)))
    W("   -- marcas (todas las ruedas): %% marcadas | precision TA (base) | precision TIPO (base) recall TIPO | precision ET | D marcadas / resto (t)")
    for _, r in Mk[Mk.subconjunto == "todas"].iterrows():
        W("      %-14s %s %5.1f %% (n %3d) | TA %5.1f (%4.1f) | TIPO %5.1f (%4.1f) rec %5.1f | ET %5.1f (%4.1f) | D %+5.2f / %+5.2f (t %5.2f)" % (
            r.variante, "*" if r.teoria else " ", r.pct_marca, r.n_marca, r.prec_TA, r.base_TA, r.prec_TIPO, r.base_TIPO, r.rec_TIPO,
            r.prec_ET, r.base_ET, r.D_marca, r.D_resto, r.t_D))


def donde_se_hace(F):
    """Enmienda 1: de los dias de tendencia ex post, cuanto del movimiento del dia ya estaba hecho a las 13:00."""
    T = F[F.TIPO == 1]
    if len(T) == 0: return
    neto = T.C16 - T.O; man = T.c13 - T.O
    hecho = np.where(np.sign(man) == np.sign(neto), np.abs(man) / np.abs(neto), 0.0)
    W("   -- donde se hace el movimiento de los %d dias de tendencia: a las 13:00 ya estaba hecho >= 2/3 en %d (%.0f %%), entre 1/3 y 2/3 en %d, < 1/3 en %d (se hizo de tarde)" % (
        len(T), (hecho >= 2 / 3).sum(), 100 * (hecho >= 2 / 3).mean(), ((hecho >= 1 / 3) & (hecho < 2 / 3)).sum(), (hecho < 1 / 3).sum()))
    E = F[F.ET == 1]
    W("   -- de los %d dias con TARDE de tendencia (ET): %d terminaron tendencia, %d mixtos, %d balance; la tarde fue para el lado del VWAP de las 13 (TA=0) en %d" % (
        len(E), (E.tipo == "tendencia").sum(), (E.tipo == "mixto").sum(), (E.tipo == "balance").sum(), (E.TA == 0).sum()))


def main():
    nombre = sys.argv[1]; t0 = time.time(); K.frenar_si_falta_ram(2.0)
    repaso = nombre == "diseno_repaso"          # el diseno otra vez SOLO para agregar el p de memoria (no elige nada: la eleccion ya esta escrita)
    if repaso: nombre = "diseno"
    etapa = {"diseno": "diseno", "val1": "validacion1", "val2": "validacion2", "explora": "exploracion"}[nombre] + ("_repaso" if repaso else "")
    if nombre == "diseno" and not repaso and os.path.exists(os.path.join(K.AQUI, "elegido_r25.json")):
        raise SystemExit("la eleccion ya se hizo (elegido_r25.json): el diseno no se vuelve a correr")
    F, AZ = cargar(nombre)
    W("RONDA 25 - clasificacion %s (%s) %s" % (nombre, etapa, time.strftime("%Y-%m-%d %H:%M")))
    # control de la herramienta: con TA el azar tiene que dar ~0,50 para cualquier puntaje
    T, Mk = evaluar(nombre, F, AZ, etapa)
    imprimir(nombre, F, T, Mk)
    donde_se_hace(F)
    W("   control: AUC del azar contra TA (todas) por detector: %s (tiene que dar ~0,50)" % ", ".join(
        "%s %.3f" % (d, T[(T.subconjunto == "todas") & (T.detector == d) & (T.objetivo == "TA")].auc_azar.iloc[0]) for d in DETS))
    F.to_csv(os.path.join(K.SAL, "rasgos_%s.csv" % nombre), index=False)
    T.to_csv(os.path.join(K.SAL, "clasif_auc_%s.csv" % nombre), index=False); Mk.to_csv(os.path.join(K.SAL, "clasif_marcas_%s.csv" % nombre), index=False)
    if repaso:
        W("   (repaso: mismos numeros que la corrida de eleccion; agrega el p de memoria. La eleccion de elegido_r25.json no se toca.)")
    if nombre == "diseno" and not repaso:
        alfa = 0.05 / len(DETS)
        P = T[(T.subconjunto == "todas") & (T.objetivo == "TA")].set_index("detector")
        pasan = [d for d in DETS if P.loc[d, "p_perm"] < alfa and P.loc[d, "auc"] > 0.5]
        W(""); W("REGLA DE ELECCION (alfa %.4f de una cola por detector): pasan %s" % (alfa, pasan or "NINGUNO"))
        if pasan:
            eleg = max(pasan, key=lambda d: P.loc[d, "auc"]); estado = "elegido"
        else:
            eleg = max(DETS, key=lambda d: P.loc[d, "auc"]); estado = "mejor_sin_aprobar"
        par = RS.TEORIA[eleg]
        out = dict(escrito_AR=time.strftime("%Y-%m-%d %H:%M:%S"), estado=estado, detector=eleg, parametro_teoria=par,
                   auc_TA=float(P.loc[eleg, "auc"]), p_perm=float(P.loc[eleg, "p_perm"]), pasan=pasan,
                   todos={d: dict(auc=float(P.loc[d, "auc"]), p=float(P.loc[d, "p_perm"]), azar=float(P.loc[d, "auc_azar"])) for d in DETS})
        json.dump(out, open(os.path.join(K.AQUI, "elegido_r25.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
        W("-> %s: %s (parametro de teoria %s), AUC TA %.3f, p %.4f" % (estado.upper(), eleg, par, out["auc_TA"], out["p_perm"]))
    open(os.path.join(K.SAL, "clasificacion_%s.txt" % nombre), "w", encoding="utf-8").write("\n".join(L))
    W("%.1f s" % (time.time() - t0))


if __name__ == "__main__":
    main()
