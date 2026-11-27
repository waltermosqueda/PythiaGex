# -*- coding: utf-8 -*-
"""RONDA 21, paso 11: PRUEBA EN VIVO de la regla CONGELADA COMB-T7+V4 en ES (muestra virgen desde el 30-11-2026). No toca ATAS: solo lee
las cintas que baja la sonda (%APPDATA%\\ATAS\\PythiaGex\\flujo\\cinta-ES-<desde>-<hasta>.csv con su .listo).
Por cada jornada nueva posterior al 25-11 (en orden): verifica las huellas SHA-256 de la regla y del pipeline (se niega si cambio algo),
aplica las exclusiones por regla, corre el MISMO pipeline (compactacion r19_00, rasgos r19_01, vueltas de la ola r19_08, semaforo r21_01,
regla congelada) y agrega:
  - sus operaciones a ronda21_regimen/bitacora_en_vivo.csv (fecha, hora, regimen, regla, lado, entrada, stop, objetivo, resultado, motivo, ...);
  - la jornada (evaluada o excluida, con el motivo) a ronda21_regimen/bitacora_jornadas.csv.
Exclusiones por regla (PREREGISTRO.md, enmienda 5): rueda corta (lista DIAS_CORTOS: no se opera y no entra en la historia); cinta incompleta
(ultima orden de la rueda antes de las 15:59:00 NY o un hueco > 300 s en la rueda); posible roll sin spread medido (salto de apertura
> 4X sin contrato configurado en vivo/contratos.json).
Historia (mediana de 10, umbrales, ruido, ayer, semana y mes): en %LOCALAPPDATA%\\PythiaGex_r21_vivo; si falta, se arma desde la historia de
la R19 o, si tampoco esta, recalculando las jornadas previas desde sus cintas.
Uso:
  python -I r21_11_vivo.py                 -> procesa todas las jornadas nuevas pendientes
  python -I r21_11_vivo.py --verificar 2026-11-25   -> recorre esa jornada YA CONOCIDA con la historia hasta la vispera y compara con la
                                              etapa 3 (no escribe la bitacora real)"""
import argparse, csv, glob, json, os, shutil, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r21_comun as K
import numpy as np, pandas as pd
import r19_comun as C19
import r19_00_inventario as INV
import r19_01_rasgos as RS
import r19_02_motor as M
import r19_08_eventos as EV
import r21_01_regimen as RG1
import regla_congelada_comb_t7_v4 as RG

LAB = os.path.dirname(K.AQUI)
ESTADO = os.path.join(os.environ.get("LOCALAPPDATA", K.SCR), "PythiaGex_r21_vivo")
DIRS = {k: os.path.join(ESTADO, k) for k in ("cinta", "rasgos", "hist")}
BITACORA = os.path.join(K.AQUI, "bitacora_en_vivo.csv")
JORNADAS = os.path.join(K.AQUI, "bitacora_jornadas.csv")
CONTRATOS = os.path.join(K.AQUI, "vivo_contratos.json")
DESDE = "2026-11-26"                     # jornadas posteriores al 25-11 (la ultima de la guardada)
DIAS_CORTOS = {"2026-11-26": "feriado de Accion de Gracias (sin rueda de contado)",
               "2026-11-27": "rueda corta: viernes de Accion de Gracias (cierre 13:15 NY)",
               "2026-12-24": "rueda corta: Nochebuena (cierre 13:15 NY)"}
DST_HASTA = "2027-03-14"                 # r19_comun.ny_offset_h vale 5 h hasta el cambio de horario de 2027: despues hay que revisarlo
CAMPOS_B = ["fecha", "hora_entrada", "regimen", "regla", "lado", "entrada", "stop", "objetivo", "resultado_pts", "motivo", "hora_salida",
            "rrn_minuto", "R", "X", "neto_c050", "neto_c100", "nivel_en_zona", "barrida", "delta_300", "iceberg", "contrato", "version_regla"]
CAMPOS_J = ["fecha", "estado", "motivo", "operaciones", "acierto_dia", "neto_c050_dia", "contrato", "X", "mediana_10", "ultima_orden_rth",
            "hueco_rth_max_s", "procesada_AR"]


def apuntar_rutas():
    for d_ in DIRS.values(): os.makedirs(d_, exist_ok=True)
    C19.CINTA = DIRS["cinta"]; RS.RAS = DIRS["rasgos"]; RS.HIST = DIRS["hist"]; M.RAS = DIRS["rasgos"]


def verificar_huellas():
    h = json.load(open(os.path.join(K.AQUI, "regla_congelada_huellas.json")))["sha256"]
    mal = [a for a, v in h.items() if RG.sha256(os.path.join(LAB, a)) != v]
    if mal: raise SystemExit("HUELLAS DISTINTAS: la regla o el pipeline cambiaron (%s). No se corre." % mal)
    return RG.sha256(os.path.join(K.AQUI, "regla_congelada_comb_t7_v4.py"))[:12]


ROLL_DESDE = "2026-12-07"               # semana del roll de diciembre (Z6 vence el 18-12): desde aca el contrato de cada cinta se confirma a mano


def contratos():
    if os.path.exists(CONTRATOS): return json.load(open(CONTRATOS))
    base = {"nota": ("Contrato de cada cinta de ES y spreads MEDIDOS entre contratos (no se inventan). Hasta el %s se asume Z6. Desde esa fecha "
                     "cada jornada necesita su contrato en 'confirmados' (mirando el precio de la cinta contra ESZ6 y ESH7 a la misma hora); si "
                     "cambia de contrato, 'cambios' necesita el spread medido: {'desde': primer dia del contrato nuevo, 'contrato': 'H7', "
                     "'spread_sobre_anterior': H7 - Z6 en pts}. Sin eso la jornada queda afuera." % ROLL_DESDE),
            "inicial": "Z6", "confirmados": {}, "cambios": []}
    json.dump(base, open(CONTRATOS, "w"), indent=1); return base


def contrato_de(dia, cfg):
    if dia < ROLL_DESDE: return cfg["inicial"]
    return cfg.get("confirmados", {}).get(dia)          # None = sin confirmar


def ajuste(d_hist, dia, cfg):
    """Pts a sumar a un precio del dia d_hist para llevarlo al contrato del dia (suma de los spreads medidos entre medio)."""
    a = 0.0
    for ch in cfg["cambios"]:
        if d_hist < ch["desde"] <= dia: a += float(ch["spread_sobre_anterior"])
    return a


# ---------------------------------------------------------------- historia
def cargar_hist():
    f = os.path.join(DIRS["hist"], "resumen_vivo.json")
    if os.path.exists(f): return json.load(open(f))
    res = {}
    r19 = os.path.join(K.SCR19, "hist", "resumen.json")
    if os.path.exists(r19):                                   # la historia de la R19 (ES hasta el 25-11)
        for k, v in json.load(open(r19)).items():
            inst, d = k.split("|")
            if inst != "ES": continue
            res[d] = dict(v, contrato=v.get("contrato", "Z6"))
            for pre, ext in (("mues", "npz"), ("perfil", "npz"), ("ruido", "npy")):
                src = os.path.join(K.SCR19, "hist", "%s-ES-%s.%s" % (pre, d, ext))
                if os.path.exists(src): shutil.copy2(src, os.path.join(DIRS["hist"], os.path.basename(src)))
        guardar_hist(res)
    return res


def guardar_hist(res):
    json.dump(res, open(os.path.join(DIRS["hist"], "resumen_vivo.json"), "w"))


def perfil(d):
    f = os.path.join(DIRS["hist"], "perfil-ES-%s.npz" % d)
    if not os.path.exists(f): return None
    z = np.load(f); return {"t0": int(z["t0"]), "arr": z["arr"]}


def ruido(d):
    f = os.path.join(DIRS["hist"], "ruido-ES-%s.npy" % d)
    return np.load(f) if os.path.exists(f) else None


def correr_jornada(dia, res, cfg, hasta=None):
    """El pipeline congelado para UNA jornada con la historia de res (solo dias < dia; con 'hasta', solo dias < hasta).
    Devuelve (R, meta, hoy, mues)."""
    lim = hasta or dia
    previas = sorted(d for d, v in res.items() if "rth_h" in v and d < lim and d < dia)
    X, med = RS.X_de([res[p]["rth_h"] - res[p]["rth_l"] for p in previas])
    pd_dia = previas[-1]; prev = dict(res[pd_dia]); aj = ajuste(pd_dia, dia, cfg)
    for kk in ("rth_h", "rth_l", "rth_c", "poc", "vah", "val"): prev[kk] = prev[kk] + aj
    sem = pd.Timestamp(dia).isocalendar()[:2]
    todas = [d for d in sorted(res) if d < lim and d < dia]
    sem_prev = [d for d in todas if tuple(pd.Timestamp(d).isocalendar()[:2]) == (sem[0], sem[1] - 1)] if sem[1] > 1 else []
    ps = [(perfil(d), int(round(ajuste(d, dia, cfg) / RG.TICK))) for d in sem_prev]
    ps = [(p, s) for p, s in ps if p is not None]
    sem_prev_perfil = RS.sumar_perfiles([p for p, _ in ps], [s for _, s in ps]) if ps else None
    def carry(lst):
        pv = p2v = v = 0.0
        for d in lst:
            rr = res.get(d)
            if not rr or "eth_v" not in rr: continue
            a = ajuste(d, dia, cfg); e = rr.get("escala_vol", 1.0)
            pv += e * (rr["eth_pv"] + a * rr["eth_v"]); p2v += e * (rr["eth_p2v"] + 2 * a * rr["eth_pv"] + a * a * rr["eth_v"]); v += e * rr["eth_v"]
        return {"pv": pv, "p2v": p2v, "v": v}
    c_sem = carry([d for d in todas if tuple(pd.Timestamp(d).isocalendar()[:2]) == tuple(sem)])
    c_mes = carry([d for d in todas if d[:7] == dia[:7]])
    prev_mismo = [d for d in previas if os.path.exists(os.path.join(DIRS["hist"], "mues-ES-%s.npz" % d))][-10:]
    mp = RS.cargar_muestras("ES", prev_mismo)
    rp = [x for x in (ruido(d) for d in previas) if x is not None][-10:]
    R, meta, hoy, mues = RS.jornada("ES", dia, X, prev, mp, c_sem, c_mes, rp, sem_prev_perfil, 1.0)
    meta.update({"mediana_rango_10": med, "dia_previo": pd_dia, "ajuste_roll_previo": aj, "semana_previa": sem_prev, "umbral_dias": prev_mismo,
                 "contrato": contrato_de(dia, cfg)})
    return R, meta, hoy, mues


def sumar_a_hist(res, dia, hoy, mues, cfg):
    np.savez_compressed(os.path.join(DIRS["hist"], "mues-ES-%s.npz" % dia), **mues)
    np.savez_compressed(os.path.join(DIRS["hist"], "perfil-ES-%s.npz" % dia), t0=hoy["perfil_eth"]["t0"], arr=hoy["perfil_eth"]["arr"])
    np.save(os.path.join(DIRS["hist"], "ruido-ES-%s.npy" % dia), hoy["ruido"])
    res[dia] = {x: hoy[x] for x in ("rth_h", "rth_l", "rth_c", "rth_o", "poc", "vah", "val", "eth_pv", "eth_p2v", "eth_v")}
    res[dia]["contrato"] = contrato_de(dia, cfg); res[dia]["escala_vol"] = 1.0
    guardar_hist(res)


# ---------------------------------------------------------------- una jornada
def chequeo_cinta(d):
    s = d["ny_s"].to_numpy(); rth = (s >= 9.5 * 3600) & (s < 16 * 3600)
    if not rth.any(): return False, "sin ordenes en la rueda", np.nan, np.nan
    sr = s[rth]; hueco = float(np.diff(sr).max()) if len(sr) > 1 else np.inf
    ult = float(sr[-1])
    if ult < 15 * 3600 + 59 * 60: return False, "cinta incompleta: la ultima orden de la rueda es a las %s NY" % M.seg_hora(ult - 9.5 * 3600), ult, hueco
    if hueco > 300: return False, "cinta incompleta: hueco de %.0f s en la rueda" % hueco, ult, hueco
    return True, "", ult, hueco


def procesar(dia, archivo, res, cfg, version, escribir=True, hasta=None):
    t0 = time.time()
    fila_j = dict(fecha=dia, estado="", motivo="", operaciones=0, contrato=contrato_de(dia, cfg), procesada_AR=time.strftime("%Y-%m-%d %H:%M"))
    if dia in DIAS_CORTOS:
        fila_j.update(estado="excluida", motivo=DIAS_CORTOS[dia]); return fila_j, None
    if dia >= DST_HASTA:
        raise SystemExit("La jornada %s cae despues del cambio de horario de 2027: revisar r19_comun.ny_offset_h antes de seguir." % dia)
    ruta = C19.ruta_compacta("ES", dia)
    d = pd.read_parquet(ruta) if os.path.exists(ruta) else INV.compactar("ES", dia, archivo)[0]
    ok, motivo, ult, hueco = chequeo_cinta(d)
    fila_j.update(ultima_orden_rth=M.seg_hora(ult - 9.5 * 3600) if ult == ult else "", hueco_rth_max_s=round(hueco, 1) if hueco == hueco else "")
    if not ok:
        fila_j.update(estado="excluida", motivo=motivo); return fila_j, None
    # roll de diciembre: el contrato de la cinta tiene que estar confirmado y, si cambio, el spread medido
    previas = sorted(p for p, v in res.items() if "rth_h" in v and p < (hasta or dia) and p < dia)
    pdia = previas[-1]
    c_hoy = contrato_de(dia, cfg); c_ayer = res[pdia].get("contrato") or contrato_de(pdia, cfg)
    if c_hoy is None:
        fila_j.update(estado="excluida", motivo="semana del roll: falta confirmar el contrato de la cinta en vivo_contratos.json"); return fila_j, None
    if c_hoy != c_ayer and not any(ch["desde"] <= dia and ch["contrato"] == c_hoy for ch in cfg.get("cambios", [])):
        fila_j.update(estado="excluida", motivo="roll %s -> %s sin spread medido en vivo_contratos.json" % (c_ayer, c_hoy)); return fila_j, None
    del d
    R, meta, hoy, mues = correr_jornada(dia, res, cfg, hasta)
    RS.guardar("ES", dia, R, meta, mues)
    ef = {}
    rows = sorted((p, abs(v["rth_c"] - v["rth_o"]) / (v["rth_h"] - v["rth_l"])) for p, v in res.items() if "rth_h" in v and v["rth_h"] > v["rth_l"] and p < dia)
    if len(rows) >= 10: ef[dia] = float(np.median([e for _, e in rows[-10:]]))
    ev = pd.DataFrame(EV.eventos_dia("ES", dia, "vivo", ef.get(dia, np.nan)))
    cur = json.load(open(RG.CURVA)); f = np.array(cur["f"]); g = np.array(cur["g"])
    reg = RG1.rasgos_dia(dia, f, g, ef, {p: res[p] for p in previas})
    op = RG.correr(ev, reg) if len(ev) else pd.DataFrame()
    filas_b = []
    for _, r in op.iterrows():
        filas_b.append(dict(fecha=dia, hora_entrada=M.seg_hora(r["seg_rueda"]), regimen=r["estado"], regla=r["familia"],
                            lado="COMPRA" if r["lado"] == 1 else "VENTA", entrada=r["entrada"], stop=r["stop"], objetivo=r["objetivo"],
                            resultado_pts=r["resultado_pts"], motivo=r["motivo"], hora_salida=M.seg_hora(min(int(r["seg_salida"]), 23399)),
                            rrn_minuto=round(float(r["rrn_minuto"]), 3), R=r["R"], X=r["X"], neto_c050=round(r["resultado_pts"] - 0.5, 3),
                            neto_c100=round(r["resultado_pts"] - 1.0, 3), nivel_en_zona=r["niveles_en_zona"], barrida=r["barrida_y_vuelta"],
                            delta_300=r["of_delta_300"], iceberg=int(r["F2"]), contrato=contrato_de(dia, cfg), version_regla=version))
    pts = op["resultado_pts"].to_numpy(float) if len(op) else np.array([])
    fila_j.update(estado="evaluada", operaciones=len(op), acierto_dia=round(100 * float((pts > 0).mean()), 1) if len(pts) else "",
                  neto_c050_dia=round(float((pts - 0.5).sum()), 3) if len(pts) else 0.0, X=meta["X"], mediana_10=meta["mediana_rango_10"])
    if escribir:
        sumar_a_hist(res, dia, hoy, mues, cfg)
    print("  %s: %d operaciones (%.1f s)" % (dia, len(op), time.time() - t0), flush=True)
    return fila_j, filas_b


def anotar(ruta, campos, filas):
    nuevo = not os.path.exists(ruta)
    with open(ruta, "a", newline="", encoding="utf-8") as fh:
        w = csv.DictWriter(fh, fieldnames=campos, extrasaction="ignore")
        if nuevo: w.writeheader()
        for fl in filas: w.writerow({k: fl.get(k, "") for k in campos})


def ya_procesadas():
    if not os.path.exists(JORNADAS): return set()
    return set(pd.read_csv(JORNADAS)["fecha"].astype(str))


def main():
    ap = argparse.ArgumentParser(); ap.add_argument("--verificar", default=None); a = ap.parse_args()
    apuntar_rutas(); version = verificar_huellas(); cfg = contratos(); res = cargar_hist()
    print("regla congelada %s... | estado en %s | historia con %d jornadas de ES (ultima %s)" % (version, ESTADO, len(res), max(res) if res else "-"), flush=True)
    crudas = C19.cintas_crudas("ES")
    if a.verificar:
        dia = a.verificar
        if dia not in crudas: raise SystemExit("no esta la cinta de %s" % dia)
        print("VERIFICACION de %s con la historia hasta la vispera (no se escribe la bitacora)" % dia, flush=True)
        fila_j, filas_b = procesar(dia, crudas[dia], res, cfg, version, escribir=False, hasta=dia)
        ref = pd.read_csv(os.path.join(K.AQUI, "salidas", "ops-COMB-T7+V4-detalle.csv")); ref = ref[ref["dia"] == dia]
        print("  jornada:", {k: fila_j.get(k) for k in ("estado", "motivo", "operaciones", "X", "mediana_10")})
        mias = [(b["hora_entrada"], b["lado"], b["regla"], round(float(b["resultado_pts"]), 3)) for b in (filas_b or [])]
        suyas = [(M.seg_hora(r["seg"]), "COMPRA" if r["lado"] == 1 else "VENTA", r["var"], round(float(r["pts"]), 3)) for _, r in ref.iterrows()]
        for x in mias: print("   en vivo   ", x)
        for x in suyas: print("   etapa 3   ", x)
        print("  COINCIDE" if mias == suyas else "  DIFIERE", flush=True)
        return
    hechas = ya_procesadas()
    pendientes = [(d, f) for d, f in sorted(crudas.items()) if d >= DESDE and d not in hechas]
    if not pendientes: print("no hay jornadas nuevas de ES posteriores al 25-11 sin procesar"); return
    for dia, archivo in pendientes:
        K.frenar_si_falta_ram(2.0)
        print("jornada %s (%s)" % (dia, os.path.basename(archivo)), flush=True)
        fila_j, filas_b = procesar(dia, archivo, res, cfg, version)
        if filas_b: anotar(BITACORA, CAMPOS_B, filas_b)
        anotar(JORNADAS, CAMPOS_J, [fila_j])
        print("  ->", fila_j["estado"], fila_j.get("motivo", ""), flush=True)
    if os.path.exists(BITACORA):
        b = pd.read_csv(BITACORA)
        print("BITACORA: %d operaciones en %d jornadas | acierto %.1f %% | neto c0,50 %+.3f por operacion" % (
            len(b), b["fecha"].nunique(), 100 * (b["resultado_pts"] > 0).mean(), (b["resultado_pts"] - 0.5).mean()))


if __name__ == "__main__":
    main()
