# -*- coding: utf-8 -*-
"""RONDA 23, paso 4: las candidatas en la CINTA de ES (55 ruedas, 10-09 a 25-11-2026), orden por orden, con los scripts de la regla
(cand_A*.py, autocontenidos) sobre los CSV crudos de la sonda. Ademas:
  - velas de 5 min de ES armadas desde la misma cinta -> el motor de 5 min (r23_sim) en las mismas ruedas: el mismo resultado a la escala
    de la vela y la REFERENCIA AL AZAR CON LA MISMA UBICACION (200 caminos reflejados) en los puntos que opero la cinta;
  - la formula geometrica P = d_stop / (d_stop + d_VWAP) de cada operacion.
Historia de rangos: la de la R19 (scratchpad\\r19\\hist\\resumen.json, ES 31-07..25-11; para los dias con cinta coincide con la cinta).
Uso: python -I r23_04_cinta.py -> salidas/cinta.txt, salidas/ops-cinta-<ID>.csv, salidas/historia_rangos_cinta.json"""
import importlib.util, json, os, pickle, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import r23_comun as K
import r23_sim as S
import r23_01_base5 as B5

CANDS = {"A1": "cand_A1_tarde_giro.py", "A2": "cand_A2_tarde_estirado.py", "A3": "cand_A3_tarde_giro_amplia.py"}
SPEC5 = {"A1": dict(desde="13:30", hasta="15:30", dv=2.0, ib=1.0, pf=0.55, br={"lado": "fade", "stop": ("ext", 0.5), "obj": ("vw", 0.0)}),
         "A2": dict(desde="14:00", hasta="15:30", dv=2.0, ib=1.5, pf=None, br={"lado": "fade", "stop": ("extmult", 0.5, 1.5), "obj": ("vw", 0.0)}),
         "A3": dict(desde="13:00", hasta="15:30", dv=1.5, ib=1.0, pf=0.55, br={"lado": "fade", "stop": ("ext", 0.5), "obj": ("vw", 0.0)})}
L = []


def W(s=""):
    print(s, flush=True); L.append(s)


def cargar(nombre):
    ruta = os.path.join(K.AQUI, nombre)
    sp = importlib.util.spec_from_file_location(nombre[:-3], ruta); m = importlib.util.module_from_spec(sp); sp.loader.exec_module(m)
    return m


def cintas():
    out = {}
    for f in sorted(os.listdir(K.FLUJO)):
        if not (f.startswith("cinta-ES-") and f.endswith(".csv")): continue
        p = f[:-4].split("-")
        if len(p) != 4 or not os.path.exists(os.path.join(K.FLUJO, f + ".listo")): continue
        fin = p[3][:8]; out["%s-%s-%s" % (fin[:4], fin[4:6], fin[6:8])] = os.path.join(K.FLUJO, f)
    return out


def velas5(c_):
    """velas de 5 min de la rueda desde la cinta: o = primero de la primera orden, h/l = extremos, c = ultimo de la ultima, vol."""
    b = np.floor((c_["seg"] - 34200) / 300.0).astype(int)
    out = {x: np.full(78, np.nan) for x in ("o", "h", "l", "c", "vol", "delta")}
    for j in range(78):
        m = b == j
        if not m.any(): continue
        out["o"][j] = c_["pri"][m][0]; out["h"][j] = c_["hi"][m].max(); out["l"][j] = c_["lo"][m].min(); out["c"][j] = c_["ult"][m][-1]
        out["vol"][j] = c_["vol"][m].sum(); out["delta"][j] = 0.0
    out["hm"] = 570 + 5 * np.arange(78)
    return out


def resumen(tk, dias, col="pts"):
    n = len(tk); nd = len(dias)
    if n == 0: return dict(n=0, ruedas=nd, ops_por_rueda=0.0)
    net = tk[col] - K.COSTO
    por = net.groupby(tk["fecha"]).sum(); serie = np.array([por.get(d, 0.0) for d in dias])
    return dict(n=n, ruedas=nd, ops_por_rueda=round(n / nd, 2), acierto=round(100 * (tk[col] > 0).mean(), 1), bruto=round(tk[col].mean(), 3),
                neto=round(net.mean(), 3), t=round(K.t_por_rueda(serie), 2), ruedas_pos=int((serie > 0).sum()), ruedas_con_op=int(tk["fecha"].nunique()))


def main():
    t0 = time.time(); K.frenar_si_falta_ram(2.0); f = K.curva_f()
    W("RONDA 23 - CANDIDATAS EN LA CINTA DE ES %s" % time.strftime("%Y-%m-%d %H:%M"))
    res19 = json.load(open(os.path.join(K.SCR19, "hist", "resumen.json")))
    rangos = {k.split("|")[1]: v["rth_h"] - v["rth_l"] for k, v in res19.items() if k.startswith("ES|") and "rth_h" in v}
    json.dump({"fuente": "R19 scratchpad hist/resumen.json (ES); rango = rth_h - rth_l", "rangos": rangos},
              open(os.path.join(K.SAL, "historia_rangos_cinta.json"), "w"), indent=0)
    mods = {k: cargar(v) for k, v in CANDS.items()}
    crudas = {d: f_ for d, f_ in cintas().items() if K.CINTA[0] <= d <= K.CINTA[1]}
    W("cintas en el periodo: %d (%s a %s); historia de rangos: %d jornadas (%s a %s)" % (len(crudas), min(crudas), max(crudas), len(rangos), min(rangos), max(rangos)))
    ops = {k: [] for k in CANDS}; infos = []; ruedas5 = {}; filas5 = []
    for dia, ruta in sorted(crudas.items()):
        K.frenar_si_falta_ram(2.0)
        c_ = mods["A1"].leer_cinta(ruta)
        if c_ is None or c_["dia"] != dia:
            W("  %s: la cinta no corresponde (%s)" % (dia, None if c_ is None else c_["dia"])); continue
        r_cinta = float(c_["hi"].max() - c_["lo"].min())
        for k, m in mods.items():
            o, info = m.operar(ruta, rangos, cinta=c_)
            ops[k] += o
            if k == "A1": infos.append(dict(info, rango_cinta=r_cinta, rango_r19=rangos.get(dia, np.nan)))
        # velas de 5 min de ES desde la cinta y sus rasgos (mismo codigo que la base de MES)
        a = velas5(c_)
        if np.isnan(a["c"]).any():
            W("  %s: vela de 5 min sin ordenes: se omite en el motor de 5 min" % dia); continue
        previas = sorted(d for d in rangos if d < dia)
        med10 = float(np.median([rangos[d] for d in previas[-10:]])); med60 = float(np.median([rangos[d] for d in previas[-60:]])) if len(previas) >= 20 else np.nan
        ctx = dict(med10=med10, med60=med60, X=B5.X_de(med10), vr10=np.nan, vr20=np.nan)
        ruedas5[dia] = a; filas5.append(B5.rasgos_rueda(dia, a, ctx, f))
        print("  %s listo (%.0f s)" % (dia, time.time() - t0), flush=True)
    I = pd.DataFrame(infos); I.to_csv(os.path.join(K.SAL, "cinta_jornadas.csv"), index=False)
    dif = (I["rango_cinta"] - I["rango_r19"]).abs()
    W("control: rango de la rueda desde la cinta contra la historia de la R19: diferencia maxima %.2f pts en %d jornadas" % (dif.max(), len(I)))
    dias = sorted(I["dia"].dropna().unique())
    P5 = pd.concat(filas5, ignore_index=True)
    P5["muestra"] = "cinta"
    pickle.dump(dict(ruedas=ruedas5), open(os.path.join(K.SCR, "ruedas5_cinta.pkl"), "wb")); P5.to_parquet(os.path.join(K.SCR, "puntos5_cinta.parquet"), index=False)
    per = lambda d: "R21-diseno" if d <= "2026-10-15" else ("R21-confirma" if d <= "2026-11-13" else "R21-guardada")
    out = {}
    for k in CANDS:
        tk = pd.DataFrame(ops[k])
        W(""); W("=== %s (%s)" % (k, CANDS[k]))
        if len(tk) == 0:
            W("  sin operaciones"); continue
        tk["k"] = ((tk["hora_entrada"].str[:2].astype(int) * 60 + tk["hora_entrada"].str[3:5].astype(int)) - 570) // 5 - 1
        # referencia al azar con la misma ubicacion: motor de 5 min con las velas de ES, en los mismos puntos (dia, k)
        sp = SPEC5[k]
        pts = P5.merge(tk[["fecha", "k"]].rename(columns={"fecha": "dia"}), on=["dia", "k"])
        z = S.correr(ruedas5, pts, sp["br"], M=200)
        tk = tk.merge(z[["dia", "k", "azar_gana", "azar_pts", "pts_real", "mot_real"]].rename(columns={"dia": "fecha", "pts_real": "pts_5min", "mot_real": "mot_5min"}), on=["fecha", "k"], how="left")
        r = resumen(tk, dias)
        W("  CINTA (orden por orden): n %d (%.2f por rueda, %d ruedas con operacion) | acierto %.1f %% | bruto %+.3f | neto c1,00 %+.3f | t por rueda %.2f | ruedas + %d de %d" % (
            r["n"], r["ops_por_rueda"], r["ruedas_con_op"], r["acierto"], r["bruto"], r["neto"], r["t"], r["ruedas_pos"], r["ruedas"]))
        W("  referencia al azar con la misma ubicacion (5 min, mismos puntos): acierto %.1f %% | neto c1,00 %+.3f | formula P %.1f %%" % (
            100 * tk["azar_gana"].mean(), (tk["azar_pts"] - K.COSTO).mean(), 100 * tk["P_geometrica"].mean()))
        W("  las mismas entradas resueltas con velas de 5 min (regla conservadora): acierto %.1f %% | neto c1,00 %+.3f" % (
            100 * (tk["pts_5min"] > 0).mean(), (tk["pts_5min"] - K.COSTO).mean()))
        W("  motivos: %s | stop medio %.2f pts | objetivo medio %.2f pts | X medio %.2f" % (
            tk["motivo"].value_counts().to_dict(), (tk["stop"] - tk["entrada"]).abs().mean(), (tk["objetivo_inicial"] - tk["entrada"]).abs().mean(), tk["X"].mean()))
        for pp in ("R21-diseno", "R21-confirma", "R21-guardada"):
            dd = [d for d in dias if per(d) == pp]; s_ = tk[tk["fecha"].isin(dd)]
            rr = resumen(s_, dd)
            if rr["n"]:
                W("     %-13s n %3d (%.2f/r) acierto %5.1f %% neto %+.3f t %.2f | azar %.1f %%" % (pp, rr["n"], rr["ops_por_rueda"], rr["acierto"], rr["neto"], rr["t"], 100 * s_["azar_gana"].mean()))
            else:
                W("     %-13s sin operaciones (%d ruedas)" % (pp, len(dd)))
        tk.to_csv(os.path.join(K.SAL, "ops-cinta-%s.csv" % k), index=False)
        out[k] = dict(r, azar_acierto=round(100 * tk["azar_gana"].mean(), 1), azar_neto=round((tk["azar_pts"] - K.COSTO).mean(), 3),
                      P_formula=round(100 * tk["P_geometrica"].mean(), 1), acierto_5min=round(100 * (tk["pts_5min"] > 0).mean(), 1),
                      neto_5min=round((tk["pts_5min"] - K.COSTO).mean(), 3))
        nv = K.siguiente_n_var()
        K.anotar(dict(n_var=nv, etapa="cinta" + os.environ.get("R23_SUF", ""), familia="A_elastico_tarde", variante="%s %s" % (k, CANDS[k]), muestra="cinta ES 10-09..25-11-2026",
                      ruedas=r["ruedas"], n=r["n"], ops_por_rueda=r["ops_por_rueda"], acierto_pct=r["acierto"], acierto_azar_pct=out[k]["azar_acierto"],
                      P_formula_pct=out[k]["P_formula"], bruto_op=r["bruto"], neto_op_c100=r["neto"], neto_azar_c100=out[k]["azar_neto"],
                      t_rueda_c100=r["t"], ruedas_pos_c100=r["ruedas_pos"], pasa_vara=int(r["acierto"] >= 60 and r["neto"] > 0 and r["ops_por_rueda"] <= 5),
                      nota="orden por orden con el script de la regla; 5 min con velas de la cinta: %.1f %% %+.3f" % (out[k]["acierto_5min"], out[k]["neto_5min"])))
    json.dump(out, open(os.path.join(K.SAL, "cinta.json"), "w"), indent=1)
    open(os.path.join(K.SAL, "cinta.txt"), "w", encoding="utf-8").write("\n".join(L))
    W("%.1f s" % (time.time() - t0))


if __name__ == "__main__":
    main()
