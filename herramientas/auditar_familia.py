# -*- coding: utf-8 -*-
r"""
auditar_familia.py — AUDITOR INDEPENDIENTE de la Familia de PythiaGex: la 4.1 "Gamma Familia" (NQ: libros NQ por Rithmic, NDX y QQQ de
CBOE) y la 5.0 "Gamma ES" (ES: libros ES por Rithmic, SPX y SPY de CBOE).

QUE HACE
  Para un minuto (o un rango muestreado) RECALCULA POR FUERA del indicador, desde los datos CRUDOS que guarda el propio indicador
  (%APPDATA%\ATAS\PythiaGex<4|5>: viva\, cboe\, cinta\) mas la carpeta extra de CBOE que la Familia tambien lee (PythiaGex\cboe-local),
  cada nivel que la Familia anoto en familia\niv-<sesion>-<contrato>.jsonl y lo compara serie por serie:
    * el strike elegido (muro C/P por volumen y por OI, major +/-, top-N, cruces ZTP, zero estandar, confluencia, familia sumada),
    * su precio convertido al futuro (libro del futuro: K + corrimiento; indice: K + base C7; ETF: K x razon C8),
    * el monto (millones de USD por 1 % con el multiplicador de su libro, C1),
    * la conversion de cada libro (base del indice y razon del ETF) y los insumos del minuto (precio del futuro, foto usada, OI).
  Salida: tabla "indicador / recalculado / diferencia / MOTIVO" y codigo de salida (ver CODIGOS). Tolerancias: strike exacto (el niv lo
  guarda con 2 decimales); precio convertido <= 0,01 pt (base <= 0,01; razon con efecto <= 0,01 pt en el futuro); monto <= 0,5 % o media
  unidad del ultimo decimal guardado (0,05 M).

INDEPENDENCIA (de donde sale cada formula)
  La cuenta se escribio desde la ESPECIFICACION, no desde el C#:
    laboratorio/tres/auditoria_0810/backtest_familia.py  C1-C10: multiplicadores, OI del futuro con fecha (C2), islas (C3), zero estandar en
                                                          grilla (C5), base del indice sincronizada (C7), razon del ETF sincronizada (C8),
                                                          cadena congelada (C9), vencido excluido y horizonte Hoy (C10); reglas MUROS/
                                                          MAJORS/ZTP/ZEST/FAM/CONF y el precio en un instante (Precio).
    laboratorio/tres/auditoria_0810/preview_niveles.py   foto_viva3, foto_cboe_de_json, montos (_monto), strike del libro (_actual) y el OI
                                                          con fecha EN VIVO (_oi_hasta_vivo: 'esperando el salto').
    laboratorio/tres/extremos_rebote.py (f_muros, f_majors, R = min(2 %, radio)), laboratorio/tres/nucleo.py (gamma76, gamma_bs con
    tasa 3,75 %, envejecer), laboratorio/tres/libros.py (fotos_cboe, _ventana, corrimiento 30 pts NQ / 8 ES) y velas.py (cierre_conocido).
    ES: las constantes DECIDIDAS de atas/PythiaGexCinco/_diseno/DISENO_5_0.md §4 (radio por libro, zero estandar, ZEST_MAX, CONF, FAM,
    multiplicador 50, cota de la base C7, rango de la razon C8, horas de SPY, reinicio trimestral, vencido excluido).
  Del C# (atas/PythiaGexCuatro y atas/PythiaGexCinco) se leyeron FORMATOS de archivo (Viva3.cs, _modulos/cboe/CboeArchivo.cs,
  _modulos/familia/fam/AlmacenNiveles.cs, la cinta seg-*.bin de AdaptadoresFamilia.cs), que series existen (CatalogoFamilia.cs) y las
  decisiones que la especificacion no dice: estan en DECISIONES_DEL_CSHARP, en el reporte y en auditar_familia_LEEME.md. Ninguna formula
  de gamma, seleccion o conversion se copio del C#.

QUE SABIA EL INDICADOR EN CADA MINUTO (lo que hace falta para comparar al centavo; DECISIONES DEL C#, no de la especificacion)
  * La Familia lee las cadenas de CBOE de DOS carpetas: la propia (PythiaGex<N>\cboe, se agrega en vivo) y la extra de cboe_local.py
    (PythiaGex\cboe-local), pero la extra SOLO al arrancar (BajadorCboe). Un minuto calculado en vivo conoce las fotos propias hasta ese
    minuto y las de la extra hasta su ultimo arranque; un minuto rellenado al arrancar las conoce todas. Los arranques se leen del log
    %APPDATA%\ATAS\pythiagex<4|5>-cboe.log ("historia <libro>:"); sin el log, de los tramos de la cinta.
  * Las muestras de C7/C8 y el precio del futuro salen de la cinta compacta seg-*.bin (ultimo tick por segundo + velas m2 de ticks +
    tramos escuchados). Cuando el indicador reinicia usa las velas del GRAFICO de ATAS para lo que la cinta no escucho, y esas velas no se
    guardan: ese precio no se puede verificar. Para las muestras se toma entonces el precio que el indicador guardo en
    familia\muestras-*.jsonl (rotulado "precio del indicador, no verificable").
  * El minuto T en vivo se calcula a T + 6 s y espera el tick que cierra la vela m2 hasta T + 60 s (MotorFamilia.EsperaCierreS).

USO (desde la raiz PythiaGex; solo LEE %APPDATA%\ATAS, nunca escribe ahi; prioridad BELOW_NORMAL)
  python -I herramientas/auditar_familia.py --perfil ES --ultimo                 el ultimo minuto que anoto la 5.0, nivel por nivel
  python -I herramientas/auditar_familia.py --perfil ES --minuto 2026-10-27T15:30Z
  python -I herramientas/auditar_familia.py --perfil ES --desde 2026-10-27T15:00Z --hasta 2026-10-27T15:14Z --cada 1
  python -I herramientas/auditar_familia.py --perfil NQ --desde 2026-10-26T00:00Z --hasta 2026-10-26T20:00Z --cada 30   (4.1, validacion)
  Opciones: --series MUROS_ES_oi,MUROS_SPX_vol | --detalle | --solo-diferencias | --salida archivo.txt (fuera de %APPDATA%\ATAS)
            --sin-diagnostico | --datos <carpeta> | --extra <carpeta o ""> | --log-cboe <archivo> | --paridad (sesion en UTC fijo)
            --minutos <lista ISO> | --instrumento MESZ6 | --constantes <json que pisa constantes, para probar otra regla>

CODIGOS DE SALIDA
  0  todo lo comparado coincide dentro de tolerancia (strike exacto; precio <= 0,01 pt; monto <= 0,5 % o media unidad del ultimo decimal)
  1  hay diferencias, TODAS explicadas por un INSUMO del minuto (precio del futuro con velas del grafico no guardadas, foto o muestra que el
     indicador tenia o no tenia en ese momento): con los insumos del indicador la cuenta coincide
  2  hay diferencias con los MISMOS insumos (formula) o sin explicar: mirar el reporte
  3  no se pudo auditar (faltan datos o constantes)
"""
import os, sys, re, gzip, json, glob, math, time, argparse
from datetime import datetime, timedelta, timezone
from zoneinfo import ZoneInfo
import numpy as np


# ====================================================================================================== prioridad baja (ATAS corre en vivo)
def _bajar_prioridad():
    """BELOW_NORMAL_PRIORITY_CLASS con los tipos declarados (sin restype/argtypes el handle -1 se trunca y SetPriorityClass falla:
    medido el 06-10 en laboratorio/tres/__init__.py)."""
    try:
        import ctypes, ctypes.wintypes as W
        k = ctypes.windll.kernel32
        k.GetCurrentProcess.restype = W.HANDLE
        k.SetPriorityClass.argtypes = [W.HANDLE, W.DWORD]
        k.SetPriorityClass(k.GetCurrentProcess(), 0x00004000)
        k.GetPriorityClass.argtypes = [W.HANDLE]
        return hex(k.GetPriorityClass(k.GetCurrentProcess()))
    except Exception as e:  # noqa: BLE001
        return "sin cambiar (%s)" % e


PRIORIDAD = _bajar_prioridad()
os.environ.setdefault("OMP_NUM_THREADS", "1"); os.environ.setdefault("OPENBLAS_NUM_THREADS", "1")
try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

VERSION = "auditar_familia 1.0 (27-10-2026)"
NY = ZoneInfo("America/New_York")
UTC = timezone.utc
APP = os.path.join(os.environ.get("APPDATA", ""), "ATAS")
AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(AQUI)
EPOCA = datetime(1970, 1, 1)

# ====================================================================================================== perfiles
# Constantes de NQ: TODAS de la especificacion (backtest_familia.py / extremos_rebote.py / libros.py / nucleo.py), con la cita.
C_NQ = dict(
    radio_libro={"fut": 100.0, "ind": 100.0, "etf": 100.0},   # backtest_familia.RADIO (ZTP) y tope de R (MUROS/MAJORS/TOPN): 100 los tres
    radio_familia=100.0,        # backtest_familia.RADIO en confluencia() y tope de R en FAM (f_muros sobre la fusion)
    radio_pct=0.02,             # extremos_rebote._R: R = min(fut x 0,02, radio) (MUROS, MAJORS; TOPN por el texto del catalogo)
    filtro_pct=0.03,            # backtest_familia.BM: solo strikes con |Fut - fut| <= 3 % del futuro
    isla=0.10,                  # C3 (backtest_familia.ISLA)
    conf_pts=3.0,               # backtest_familia.CONF_PTS
    fam_grilla=5.0,             # backtest_familia.FAM_GRILLA
    zest={"fut": (2.5, 300.0, 50.0), "ind": (2.5, 300.0, 50.0), "etf": (0.05, 7.5, 1.0)},   # C5: (paso, semi, centro) en el eje del libro
    zest_max=300.0,             # backtest_familia.zest: |z - fut| <= 300
    vida_viva=300,              # backtest_familia.VIDA_VIVA (s)
    vida_cboe=1500,             # backtest_familia.VIDA_CBOE (s)
    retraso=900,                # backtest_familia.RETRASO (C7/C8: precio del futuro en cadena.ts - 900 s)
    vivo_ventana=600,           # backtest_familia.conversion: spot vivo = cambio contra una foto de los 600 s previos
    corr_umbral=30.0,           # backtest_familia.minutos_nq / libros.construir: corrimiento de contrato si |mediana| > 30 pts (ES: 8)
    corr_min=5,                 # ... con >= 5 fotos
    base_piso=0.5,              # C7: robusta(x, 0,5)
    base_hora=("09:35", "15:59"),   # C7: hora NY del spot
    base_n_dia=5, base_n_rueda=20, base_ultimas=24,
    razon_piso_rel=0.0002, razon_n=5, razon_ultimas=24,  # C8
    topn=6,                     # DECISION DEL C#: ReglasFam.TopNCantidad = 6 (la especificacion no tiene TOPN)
    tasa=0.0375,                # nucleo.TASA (Black-Scholes del indice y del ETF)
    piso_dias=1.0 / 1440.0,     # nucleo.PISO_DIAS
    mult={"fut": 20.0, "ind": 100.0, "etf": 100.0},       # C1
    c2=dict(comunes=20, frac=0.5),                        # C2: >= 20 contratos comunes y >= 50 % con OI distinto
    # Reglas solo de ES (DISENO_5_0 §4); en NQ no existen (ni la especificacion ni la 4.1 las tienen):
    cota_base=None,             # C7 ES: 0 < base <= max y |base - fut x carry x dias/365| <= max(tol_rel x carry, tol_abs)
    razon_rango=None,           # C8 ES: muestra (y razon) dentro de [min, max]
    etf_horas=None,             # C8 ES: muestras de SPY solo con la hora NY del spot (ts - 900 s) en [desde, hasta)
    etf_reinicio=None,          # C8 ES: la ventana de 24 se reinicia a esta hora NY del tercer viernes de mar/jun/sep/dic (ex-dividendo)
    venc_reales=False,          # C10 ES: ademas de 'dias' envejecidos, el vencimiento real (16:00 NY de la fecha 'f') tiene que ser > t
)

# ES: constantes DECIDIDAS en atas/PythiaGexCinco/_diseno/DISENO_5_0.md §4 (27-10-2026). Las que no dependen del precio (isla 0,10, filtro
# 3 %, tiempos 900/600/300/1500 s, cantidades de muestras, piso 0,5 de la base, tasa) son las de la especificacion; los multiplicadores son
# los del contrato (ES 50 USD/pt; SPX y SPY 100). NO se lee constantes_medidas.json (eran PROPUESTAS; varias difieren de lo decidido).
C_ES = dict(C_NQ)
C_ES.update(
    radio_libro={"fut": 25.0, "ind": 20.0, "etf": 20.0},  # DISENO §4 "RADIO por libro: ES 25, SPX 20, SPY 20" (R = min(2 %, radio))
    radio_familia=20.0,         # DECISION DEL C#: SeleccionFam.RADIO_FAMILIA = 20 para FAM_MUROS y CONF (el DISENO no lo fija)
    zest={"fut": (0.5, 75.0, 12.5), "ind": (0.5, 75.0, 12.5), "etf": (0.05, 7.5, 1.0)},   # DISENO §4 (paso, semiancho, centro)
    zest_max=75.0,              # DISENO §4 ZEST_MAX 75
    conf_pts=3.0,               # DISENO §4 CONF_PTS 3 (corregido 01:40)
    fam_grilla=5.0,             # DISENO §4 FAM_GRILLA 5
    corr_umbral=8.0,            # libros.construir: "30 pts NQ / 8 ES" (la especificacion ya lo trae)
    base_piso=0.5,              # C7: robusta(x, 0,5) en pts de SPX (constantes_medidas.conversion_SPX_a_ES.regla)
    topn=6,                     # DECISION DEL C#: TopNCantidad = 6 (5.0 ReglasFam/Seleccion; es una cantidad, no una distancia)
    mult={"fut": 50.0, "ind": 100.0, "etf": 100.0},
    cota_base=dict(maximo=150.0, carry=0.034, tol_rel=0.25, tol_abs=3.0),   # DISENO §4 "carry r-q 3,4 %" y la cota de la base
    razon_rango=(9.95, 10.25),  # DISENO §4 "ES/SPY 9,95-10,25"
    etf_horas=("04:00", "20:00"),   # DISENO §4 "SPY: muestras solo 04:00-20:00 NY"
    etf_reinicio="04:00",       # DISENO §4 "reinicio el 3er viernes trimestral 04:00 NY"
    venc_reales=True,           # DISENO §4 "0DTE vencido de noche: excluido" (SPXW y SPY 16:00 NY; la SPX AM vence 09:30 NY: ver AVISO_AM)
)

PERFILES = {
    "NQ": dict(nombre="PythiaGex 4.1 Gamma Familia (NQ)", carpeta="PythiaGex4", instrumento="MNQZ6",
               extra=os.path.join(APP, "PythiaGex", "cboe-local"), log_cboe=os.path.join(APP, "pythiagex4-cboe.log"),
               fut=dict(id="NQ", viva="NQ"), ind=dict(id="NDX", archivo="NQ", log="NDX"), etf=dict(id="QQQ", archivo="QQQ", log="QQQ"),
               c=C_NQ, vencimientos={"U6": (2026, 9), "Z6": (2026, 12), "H7": (2027, 3), "M7": (2027, 6)}),
    "ES": dict(nombre="PythiaGex 5.0 Gamma ES (ES/MES)", carpeta="PythiaGex5", instrumento="MESZ6",
               extra=os.path.join(APP, "PythiaGex", "cboe-local"), log_cboe=os.path.join(APP, "pythiagex5-cboe.log"),
               fut=dict(id="ES", viva="ES"),
               ind=dict(id="SPX", archivo="ES", log="SPX", nota="cadena _SPX: raices SPX y SPXW; el 0DTE es la SPXW del dia; el tercer "
                                                             "viernes tambien vence la SPX AM-settled (09:30 NY): dos vencimientos el mismo dia"),
               etf=dict(id="SPY", archivo="SPY", log="SPY"), c=C_ES,
               vencimientos={"Z6": (2026, 12), "H7": (2027, 3), "M7": (2027, 6)}),
}
ROLES = ("fut", "ind", "etf")

# CatalogoFamilia.Calc (4.1 y 5.0, mismo orden; {F} libro del futuro, {I} indice, {E} ETF). Las 21 de la vista previa (preview_niveles.CALC)
# + TOPN (4.1.7). CARGA_* (4.1.7) NO se audita: no tiene especificacion (su horizonte y su regla estan solo en el C#).
CATALOGO = ["MUROS_{F}_vol", "MAJORS_{E}_oi", "CONF_vol", "MAJORS_{F}_vol", "MUROS_{F}_oi", "MUROS_{I}_vol", "MUROS_{I}_oi", "MUROS_{E}_vol",
            "MUROS_{E}_oi", "MAJORS_{F}_oi", "MAJORS_{I}_vol", "MAJORS_{I}_oi", "MAJORS_{E}_vol", "FAM_MUROS_vol", "FAM_MUROS_oi", "ZEST_{F}_vol",
            "ZEST_{F}_oi", "ZEST_{I}_vol", "ZEST_{E}_vol", "ZTP_{F}_vol", "ZTP_{I}_vol", "TOPN_{F}_vol", "TOPN_{F}_oi"]


def catalogo(perfil):
    return [x.format(F=perfil["fut"]["id"], I=perfil["ind"]["id"], E=perfil["etf"]["id"]) for x in CATALOGO]

DECISIONES_DEL_CSHARP = [
    "FUENTES de CBOE: la 4.1 suma la carpeta extra PythiaGex\\cboe-local (BajadorCboe.CarpetaExtra, 4.1.9 del 13-10) a la propia, con "
    "dedup por cadena_ts (gana la bajada mas temprana, como libros.fotos_cboe), pero la extra se lee SOLO AL ARRANCAR. La especificacion "
    "(libros.fotos_cboe) ya une varias carpetas; lo que es del C# es el CUANDO: el auditor modela que fotos conocia en cada minuto. Lo leido "
    "de la extra en un arranque llega hasta la ULTIMA foto que dice el log ('historia <libro>: ... a Y'), no hasta la hora de la linea "
    "(27-10 15:19: cboe_local escribio una foto mientras la 5.0 leia). Lo que la Familia DEBIA tener y no tuvo sale en 'CONV <libro> espec.'.",
    "ES (5.0, P2_SPX.md) BASE DE FORWARDS: sin base sincronizada (sin rueda, roll o fuera de cota) la 5.0 dibuja SPX con la base de forwards "
    "de la foto (error <= 8 ticks y dentro de las cotas). No esta en backtest_familia ni en el DISENO §4 (ahi el indice no se dibuja): si "
    "pasa, el auditor lo marca 'decision C#'.",
    "CIERRE DE VELA en vivo: el minuto T se calcula a T + 6 s y espera hasta T + 60 s el tick que cierra la vela m2 (MotorFamilia."
    "RetrasoMinutoS y EsperaCierreS). La especificacion (vista previa en vivo) usaba el ultimo tick a T + ~6 s; la del backtest, la cinta entera.",
    "CINTA: la 4.1 guarda la cinta compacta (ultimo tick por segundo, velas m2 de ticks y tramos escuchados). Un tick vale si [tick, ts] "
    "esta escuchado; una vela, si su balde se escucho entero; si no, la 4.1 usa la vela del grafico de ATAS, que no se guarda. "
    "La especificacion no tiene tramos (su cinta CSV tenia todos los ticks).",
    "ZERO ESTANDAR (C5) sin cache: la 4.1.8 lo recalcula cada minuto (ZeroCadaMinuto = true, MinuteroNq/ConversionNdx/ConversionQqq), que es "
    "lo que hacia la vista previa EN VIVO (su cache vivia dentro de la llamada de un minuto). Si el niv no coincide, el auditor prueba la "
    "cache de la especificacion (primer minuto de la misma foto y clave) y lo dice.",
    "TOPN_<fut>_vol/oi: la especificacion no tiene TOPN. Regla del texto del catalogo (CatalogoFamilia: 'los N strikes con mas |gamma neta| "
    "dentro del radio min(2 %, 100 pts), de mayor a menor, D1..DN'), N = 6 (ReglasFam.TopNCantidad), monto neto como MAJORS. Desempate a "
    "igual |gamma|: el de menor strike primero (SUPUESTO).",
    "ES (5.0) RADIO DE LA FAMILIA: FAM_MUROS y CONF usan 20 pts (SeleccionFam.RADIO_FAMILIA); el DISENO_5_0 §4 fija el radio por libro "
    "(ES 25, SPX 20, SPY 20) pero no el de la familia. En NQ es el RADIO 100 de la especificacion para todo.",
    "ES (5.0) TOPN: N = 6 (TopNCantidad), radio del libro ES (25), misma regla que en NQ.",
    "ES (5.0) INTERPRETACION DEL AUDITOR de las reglas del DISENO_5_0 §4 (el DISENO dice la regla, no donde se aplica): la cota de la base "
    "(0 < b <= 150 y |b - fut x 3,4 % x dias/365| <= max(25 %, 3)), las horas de SPY (spot NY 04:00-20:00) y el rango de la razon (9,95-10,25) "
    "se aplican A CADA MUESTRA (la que no cumple no entra en ninguna mediana) y a la base/razon del minuto; el reinicio trimestral corta la "
    "ventana de 24 de SPY a las 04:00 NY del tercer viernes de mar/jun/sep/dic; el vencido se excluye ademas por su fecha (16:00 NY). Si la "
    "5.0 lo aplica distinto, aparece como diferencia de CONV con el motivo.",
    "MODO CORREGIDO (default de la 4.1): sesion de CME en hora de NY (18:00 de la vispera a 17:00) y las horas fijas de C2/C7/C9/oi_fresco "
    "en hora de NY (DISENO_4_1 §A.8; ConversionQqq.Vigencia: congelada hasta las 09:30 NY siguientes). En horario de verano de EE.UU. es "
    "identico a la especificacion (UTC fijo); desde el 01-11 no. Vencimiento del contrato: tercer viernes 09:30 NY (velas.EXPIRA da igual "
    "para Z6). C2 'posferiado' con el calendario de NYSE (la especificacion: fijo '2026-09-08').",
]

LIMITE_VIVO_MS = 62000     # en vivo la 4.1 espera el tick que cierra la vela hasta T + 60 s (EsperaCierreS) y su hilo mira cada ~1 s
CALCULO_S = 6              # el minuto T se calcula a T + 6 s (MotorFamilia.RetrasoMinutoS)
TOL_PRECIO = 0.01          # pts del futuro
TOL_STRIKE = 0.005         # el strike tiene que ser el mismo (el niv lo guarda redondeado a 2)
TOL_MONTO_REL = 0.005      # 0,5 %
TOL_MONTO_ABS = 0.05       # el niv guarda el monto redondeado a 0,1 M: media unidad del ultimo decimal
TOL_CONV_BASE = 0.01       # pts de base: el precio es K + base, asi que es la tolerancia del precio convertido (0,01 pt)
TOL_CONV_PRECIO = 0.01     # razon: |dr| x K_etf <= 0,01 pt  <=>  |dr| <= 0,01 x r / fut (NQ 3e-7 relativo; ES 1,3e-6)
DIF_MINIMA = 1e-6          # por debajo de esto es la misma cuenta (redondeo del double); entre esto y la tolerancia: "dentro de tolerancia"


class FaltanDatos(Exception):
    pass


# ====================================================================================================== utilidades de tiempo y numeros
def ms_de(dt):
    """datetime UTC naive -> ms desde 1970 (exacto: aritmetica entera)."""
    d = dt - EPOCA
    return (d.days * 86400 + d.seconds) * 1000 + d.microseconds // 1000


def de_ms(ms):
    return EPOCA + timedelta(milliseconds=int(ms))


def de_clave(k):
    return EPOCA + timedelta(minutes=int(k))


def clave_de(t):
    return int((t - EPOCA).total_seconds() // 60)


def a_ny(dt_utc):
    return dt_utc.replace(tzinfo=UTC).astimezone(NY).replace(tzinfo=None)


def ny_a_utc(dt_ny):
    return dt_ny.replace(tzinfo=NY).astimezone(UTC).replace(tzinfo=None)


def utc_iso(s):
    """ISO con zona (o 'Z', o '\\u002B') -> datetime UTC naive. Acepta 7 decimales de .NET."""
    s = str(s).replace("\\u002B", "+").replace("Z", "+00:00")
    m = re.match(r"^(.*T\d\d:\d\d:\d\d)(\.\d+)?(.*)$", s)
    if m and m.group(2) and len(m.group(2)) > 7:
        s = m.group(1) + m.group(2)[:7] + m.group(3)
    d = datetime.fromisoformat(s)
    if d.tzinfo is not None:
        d = d.astimezone(UTC).replace(tzinfo=None)
    return d


def hm(dt):
    return dt.strftime("%H:%M")


def robusta(x, piso):
    """backtest_familia.robusta: mediana de los valores a <= max(3 MAD, piso) de la mediana; (mediana, n)."""
    x = np.asarray([v for v in x if v == v], float)
    if len(x) == 0:
        return float("nan"), 0
    med = float(np.median(x)); mad = float(np.median(np.abs(x - med)))
    b = x[np.abs(x - med) <= max(3.0 * mad, piso)]
    return (float(np.median(b)), len(b)) if len(b) else (med, len(x))


def nan(x):
    return x is None or not (x == x)


def f2(x, d=2):
    if x is None:
        return "-"
    if isinstance(x, str):
        return x
    if x != x:
        return "NaN"
    return ("%." + str(d) + "f") % x


# ====================================================================================================== sesion de CME
class Sesion:
    """La sesion de la Familia. corregida (default de la 4.1): 18:00 NY de la vispera a 17:00 NY (DECISION DEL C#, DISENO_4_1 §A.1);
    paridad (la especificacion): 22:00 UTC de la vispera a 21:00 UTC. En horario de verano son iguales."""

    def __init__(self, dia, corregida=True):
        self.dia = dia; self.corregida = corregida
        d = datetime.strptime(dia, "%Y-%m-%d")
        if corregida:
            self.ini = ny_a_utc(d - timedelta(days=1) + timedelta(hours=18))
            self.fin = ny_a_utc(d + timedelta(hours=17))
        else:
            self.ini = d - timedelta(hours=2); self.fin = d + timedelta(hours=21)

    @staticmethod
    def de(t_utc, corregida=True):
        if corregida:
            ny = a_ny(t_utc)
            d = (ny + timedelta(hours=6)).date()
            if d.weekday() == 5:
                d -= timedelta(days=1)
            elif d.weekday() == 6:
                d -= timedelta(days=2)
            return Sesion(d.strftime("%Y-%m-%d"), True)
        return Sesion((t_utc + timedelta(hours=2)).strftime("%Y-%m-%d"), False)

    def hora_fija(self, dia, h, m=0):
        """UTC de una hora fija del dia 'dia': hora de NY en modo corregido; en paridad la UTC del horario de verano (h + 4)."""
        d = datetime.strptime(dia, "%Y-%m-%d") if isinstance(dia, str) else datetime(dia.year, dia.month, dia.day)
        base = d + timedelta(hours=h, minutes=m)
        return ny_a_utc(base) if self.corregida else base + timedelta(hours=4)

    def dias_utc(self):
        out = []
        d = datetime(self.ini.year, self.ini.month, self.ini.day)
        while d < self.fin:
            out.append(d.strftime("%Y-%m-%d")); d += timedelta(days=1)
        return out


def feriados_bolsa(y):
    """Feriados de NYSE/CME-equity (para C2 'posferiado'; mismo calendario que la 4.1, TiempoFam.FeriadosDelAnio)."""
    def obs(d):
        return d - timedelta(days=1) if d.weekday() == 5 else (d + timedelta(days=1) if d.weekday() == 6 else d)

    def nesimo(m, dw, n):
        d = datetime(y, m, 1)
        while d.weekday() != dw:
            d += timedelta(days=1)
        return d + timedelta(days=7 * (n - 1))

    def ultimo(m, dw):
        d = datetime(y, m + 1, 1) - timedelta(days=1)
        while d.weekday() != dw:
            d -= timedelta(days=1)
        return d

    a = y % 19; b = y // 100; c = y % 100; dd = b // 4; e = b % 4; f = (b + 8) // 25; g = (b - f + 1) // 3
    h = (19 * a + b - dd - g + 15) % 30; i = c // 4; k = c % 4; l_ = (32 + 2 * e + 2 * i - h - k) % 7; mm = (a + 11 * h + 22 * l_) // 451
    pascua = datetime(y, (h + l_ - 7 * mm + 114) // 31, ((h + l_ - 7 * mm + 114) % 31) + 1)
    return {obs(datetime(y, 1, 1)), nesimo(1, 0, 3), nesimo(2, 0, 3), pascua - timedelta(days=2), ultimo(5, 0), obs(datetime(y, 6, 19)),
            obs(datetime(y, 7, 4)), nesimo(9, 0, 1), nesimo(11, 3, 4), obs(datetime(y, 12, 25))}


def es_posferiado(dia):
    d = datetime.strptime(dia, "%Y-%m-%d")
    a = d - timedelta(days=1)
    while a.weekday() >= 5:
        a -= timedelta(days=1)
    return a in feriados_bolsa(a.year) or a in feriados_bolsa(a.year + 1)


def vencimiento_contrato(perfil, codigo):
    """Tercer viernes del mes del contrato a las 09:30 NY, en UTC (Z6 = 2026-12-18 14:30 UTC, igual que velas.EXPIRA)."""
    y, m = perfil["vencimientos"][codigo]
    d = datetime(y, m, 1)
    while d.weekday() != 4:
        d += timedelta(days=1)
    return ny_a_utc(d + timedelta(days=14, hours=9, minutes=30))


# ====================================================================================================== la cinta (seg-<INSTR>-<dia>.bin)
class _Lector:
    """BinaryReader de .NET: enteros little-endian y strings con largo 7-bit."""
    __slots__ = ("b", "i")

    def __init__(self, b):
        self.b = b; self.i = 0

    def i32(self):
        v = int.from_bytes(self.b[self.i:self.i + 4], "little", signed=True); self.i += 4; return v

    def i64(self):
        v = int.from_bytes(self.b[self.i:self.i + 8], "little", signed=True); self.i += 8; return v

    def cadena(self):
        n = 0; s = 0
        while True:
            x = self.b[self.i]; self.i += 1; n |= (x & 0x7F) << s; s += 7
            if x < 0x80:
                break
        v = self.b[self.i:self.i + n].decode("utf-8"); self.i += n; return v

    def arr(self, dt, n):
        a = np.frombuffer(self.b, dtype=dt, count=n, offset=self.i); self.i += a.nbytes; return a


DT_SEG = np.dtype([("i", "<i4"), ("t", "<i8"), ("p", "<f8")])
DT_VELA = np.dtype([("B", "<i8"), ("O", "<f8"), ("H", "<f8"), ("L", "<f8"), ("C", "<f8"), ("TPri", "<i8"), ("TUlt", "<i8"), ("N", "<i4")])


class Cinta:
    """La cinta del futuro que guarda la Familia (AdaptadoresFamilia.CintaFamilia.SerializarDia, 'PG4SEG1'): por segundo el ultimo tick
    (casilla = techo(t_ms / 1000); gana el t mayor), velas m2 de ticks, tramos escuchados y sesiones. Solo lectura.

    precio(ts)  = backtest_familia.Precio: el ultimo tick <= ts si hay uno en los 120 s previos (y ts no pasa del ultimo tick); si no, la
                  vela m2 CERRADA que contiene ts, interpolada apertura -> cierre. Regla de la cinta de la 4.1 (DECISION DEL C#): el tick
                  vale si [tick, ts] esta escuchado y la vela si su balde se escucho entero; si no, la 4.1 uso la vela del grafico (no
                  guardada) y el precio sale 'no verificable'.
    cierre(ts)  = velas.cierre_conocido: el cierre de la ultima vela m2 CERRADA (hubo un tick de la sesion despues de su fin, hasta
                  'limite') con fin <= ts, dentro de la sesion; NaN si cerro hace mas de 4 h."""

    def __init__(self, carpeta, instrumento, dias, corregida=True):
        self.instrumento = instrumento; self.corregida = corregida
        self.seg_t = {}; self.seg_p = {}
        self.velas = {}
        self.tramos = []
        self.archivos = []
        for d in sorted(set(dias)):
            p = os.path.join(carpeta, "cinta", "seg-%s-%s.bin" % (instrumento, d))
            if os.path.exists(p):
                self._leer(p); self.archivos.append(os.path.basename(p))
        self._normalizar_tramos()
        todos = [t[t > 0] for t in self.seg_t.values()]
        self.todos_t = np.sort(np.concatenate(todos)) if todos else np.zeros(0, np.int64)
        self.ultimo_tick = int(self.todos_t[-1]) if len(self.todos_t) else 0

    def _leer(self, ruta):
        with open(ruta, "rb") as f:
            b = f.read()
        r = _Lector(b)
        magia = r.cadena()
        if not (magia.startswith("PG") and magia.endswith("SEG1")) or r.i32() != 1:
            raise FaltanDatos("cinta %s: formato desconocido (%s)" % (ruta, magia))
        instr = r.cadena()
        if instr.lower() != self.instrumento.lower():
            raise FaltanDatos("cinta %s es de %s, no de %s" % (ruta, instr, self.instrumento))
        num = r.i64()
        s = r.arr(DT_SEG, r.i32())
        T = self.seg_t.setdefault(num, np.zeros(86400, np.int64)); P = self.seg_p.setdefault(num, np.zeros(86400))
        for i, t, p in zip(s["i"].tolist(), s["t"].tolist(), s["p"].tolist()):
            if 0 <= i < 86400 and (T[i] == 0 or t > T[i]):
                T[i] = t; P[i] = p
        v = r.arr(DT_VELA, r.i32())
        for x in v.tolist():
            B = int(x[0]); nuevo = [x[1], x[2], x[3], x[4], int(x[5]), int(x[6]), int(x[7])]
            o = self.velas.get(B)
            if o is None:
                self.velas[B] = nuevo; continue
            if nuevo[4] < o[4]:
                o[4] = nuevo[4]; o[0] = nuevo[0]
            if nuevo[5] > o[5]:
                o[5] = nuevo[5]; o[3] = nuevo[3]
            o[1] = max(o[1], nuevo[1]); o[2] = min(o[2], nuevo[2]); o[6] = max(o[6], nuevo[6])
        for _ in range(r.i32()):
            a = r.i64(); b2 = r.i64()
            if b2 >= a:
                self.tramos.append((a, b2))
        for _ in range(r.i32()):
            r.cadena(); r.i64(); r.i64(); r.i64(); r.i64()     # sesiones: el ultimo tick de cada una se mide de los ticks

    def _normalizar_tramos(self):
        out = []
        for a, b in sorted(self.tramos):
            if out and a <= out[-1][1]:
                out[-1] = (out[-1][0], max(out[-1][1], b))
            else:
                out.append((a, b))
        self.tramos = out

    def escuchado(self, desde, hasta):
        for a, b in self.tramos:
            if a > desde:
                break
            if b >= hasta:
                return True
        return False

    def hay_tick(self, desde, hasta):
        """hay algun tick con desde <= t <= hasta? (exacto con las casillas: cada una guarda el ultimo tick de su segundo)."""
        if hasta < desde:
            return False
        i = int(np.searchsorted(self.todos_t, desde, side="left"))
        return i < len(self.todos_t) and int(self.todos_t[i]) <= hasta

    def ultimo_tick_hasta(self, ts, max_edad):
        s = -((-ts) // 1000); smin = -((-(ts - max_edad)) // 1000)
        k = s
        while k >= smin:
            dn = k // 86400
            T = self.seg_t.get(dn)
            if T is None:
                k = dn * 86400 - 1; continue
            i = k - dn * 86400
            t = int(T[i])
            if t != 0 and t <= ts:
                return t, float(self.seg_p[dn][i])
            k -= 1
        return None, float("nan")

    def vela(self, b, limite):
        """la vela m2 que abre en b si esta CERRADA a la hora 'limite' (ms; None = toda la cinta): hubo un tick de su sesion en
        [b + 120 s, limite] (velas_de_ticks / CintaFamilia.Elegir). Devuelve [o, h, l, c, t_pri, t_ult, n] o None."""
        v = self.velas.get(b)
        if v is None:
            return None
        ses = Sesion.de(de_ms(b), self.corregida)
        lim = ms_de(ses.fin) - 1 if limite is None else min(limite, ms_de(ses.fin) - 1)
        if not self.hay_tick(b + 120000, lim):
            return None
        return v

    def entera(self, b):
        return self.escuchado(b, b + 120000)

    def fin_tramo_antes(self, ts):
        """fin (ms) del ultimo tramo escuchado que termina antes de ts (o None)."""
        f = None
        for a, b in self.tramos:
            if b < ts:
                f = b
        return f

    def precio(self, ts):
        """(precio, fuente) en el instante ts (ms): fuente 'tick' | 'vela' | 'no verificable' (la 4.1 uso la vela del grafico) | None."""
        if self.ultimo_tick and ts <= self.ultimo_tick:
            t, p = self.ultimo_tick_hasta(ts, 120000)
            if t is not None and ts - t <= 120000 and self.escuchado(t, ts):
                return p, "tick"
        b = (ts // 120000) * 120000
        v = self.vela(b, None)
        if v is not None:
            if self.entera(b):
                return v[0] + (v[3] - v[0]) * (ts - b) / 120000.0, "vela"
            return float("nan"), "no verificable"
        if not self.escuchado(b, b + 120000):
            return float("nan"), "no verificable"
        return float("nan"), None

    def cierre(self, ts, ses, limite=None):
        """(cierre, apertura de la vela, entera) del minuto ts dentro de la sesion. limite = None: toda la cinta archivada (minuto
        rellenado al arrancar); limite = ts + 62 s: en vivo (EsperaCierreS)."""
        ini = ms_de(ses.ini)
        b = ((ts - 120000) // 120000) * 120000
        while b >= ini:
            if ts - (b + 120000) > 4 * 3600000:
                return float("nan"), None, False
            v = self.vela(b, limite)
            if v is not None:
                return v[3], b, self.entera(b)
            b -= 120000
        return float("nan"), None, False


# ====================================================================================================== la cadena y la cuenta (nucleo + backtest_familia)
class Cadena:
    """nucleo.Cadena: filas (n x 8: K, venc, oi_c, oi_p, iv_c, iv_p, vol_c, vol_p), dias por vencimiento relativos a 'generado'.
    fechas: la fecha 'f' de cada vencimiento (solo CBOE; el libro del futuro no la trae)."""
    __slots__ = ("filas", "dias", "spot", "es_fut", "generado", "fechas", "am")

    def __init__(self, filas, dias, spot, es_fut, generado, fechas=None, am=None):
        self.filas = np.asarray(filas, float).reshape(-1, 8) if len(filas) else np.zeros((0, 8))
        self.dias = np.asarray(dias, float); self.spot = float(spot or 0.0); self.es_fut = bool(es_fut); self.generado = generado
        self.fechas = list(fechas) if fechas else None
        self.am = list(am) if am else None


def tercer_viernes(y, m):
    d = datetime(y, m, 15)
    while d.weekday() != 4:
        d += timedelta(days=1)
    return d


def venc_real_utc(fecha, am=False):
    """C10 ES (DISENO_5_0 §4): el vencimiento real de una serie de CBOE de fecha 'f': 16:00 NY (SPXW y SPY, PM-settled), en UTC. La SPX
    mensual AM-settled del tercer viernes vence 09:30 NY, pero la cadena guardada la SUMA a la SPXW del mismo dia (cadena_atas.construir:
    una fila por (K, fecha); exposicion.parse_occ pone 16:00 a todas): no se puede separar (AVISO_AM)."""
    d = datetime.strptime(fecha, "%Y-%m-%d")
    return ny_a_utc(d + timedelta(hours=9, minutes=30) if am else d + timedelta(hours=16))


_ISQ = 1.0 / math.sqrt(2.0 * math.pi)


def gamma_bs(S, K, T, iv, r):
    """nucleo.gamma_bs: Black-Scholes con tasa r, sin dividendo."""
    S = np.asarray(S, float); K = np.asarray(K, float); T = np.asarray(T, float); iv = np.asarray(iv, float)
    ok = (S > 0) & (K > 0) & (T > 0) & (iv > 0)
    with np.errstate(divide="ignore", invalid="ignore"):
        v = iv * np.sqrt(T)
        d1 = (np.log(S / K) + (r + 0.5 * iv * iv) * T) / v
        g = np.exp(-0.5 * d1 * d1) * _ISQ / (S * v)
    return np.where(ok, g, 0.0)


def gamma76(F, K, T, iv):
    """nucleo.gamma76: Black-76 sin descuento (opciones sobre el futuro)."""
    F = np.asarray(F, float); K = np.asarray(K, float); T = np.asarray(T, float); iv = np.asarray(iv, float)
    ok = (F > 0) & (K > 0) & (T > 0) & (iv > 0)
    with np.errstate(divide="ignore", invalid="ignore"):
        v = iv * np.sqrt(T)
        d1 = (np.log(F / K) + 0.5 * iv * iv * T) / v
        g = np.exp(-0.5 * d1 * d1) * _ISQ / (F * v)
    return np.where(ok, g, 0.0)


def gam(es_fut, tasa):
    return gamma76 if es_fut else (lambda S, K, T, iv: gamma_bs(S, K, T, iv, tasa))


def envejecer(gen, ahora):
    """nucleo.envejecer (GammaHoyNucleo.Envejecer): entre 0 y 2 dias."""
    if gen is None:
        return 0.0
    return max(0.0, min(2.0, (ahora - gen).total_seconds() / 86400.0))


def filas_hoy(cad, ahora, piso_dias, venc_reales=False):
    """backtest_familia.filas_hoy: horizonte Hoy con los dias envejecidos (C10: el vencido queda afuera).
    venc_reales (ES, DISENO_5_0 §4): ademas, la serie tiene que vencer DESPUES de 'ahora' segun su fecha (16:00 NY). Se informa cuantas filas
    deja una regla y no la otra ('desacuerdo') y si el tercer viernes mezcla la SPX AM con la SPXW ('aviso_am')."""
    env = envejecer(cad.generado, ahora)
    dias_env = cad.dias - env
    vivo_real = None
    aviso_am = ""
    if venc_reales and cad.fechas and len(cad.fechas) == len(dias_env):
        am = cad.am if (cad.am and len(cad.am) == len(cad.fechas)) else [False] * len(cad.fechas)
        vivo_real = np.array([venc_real_utc(f, a) > ahora for f, a in zip(cad.fechas, am)], bool)
        fechas_am = {f for f, a in zip(cad.fechas, am) if a}
        for f, a in zip(cad.fechas, am):
            d = datetime.strptime(f, "%Y-%m-%d")
            if not a and f not in fechas_am and d == tercer_viernes(d.year, d.month):
                vispera = d - timedelta(days=1)
                while vispera.weekday() >= 5:
                    vispera -= timedelta(days=1)
                if ny_a_utc(vispera + timedelta(hours=16)) <= ahora < venc_real_utc(f):
                    aviso_am = ("serie %s (tercer viernes): la cadena guardada suma la SPX mensual AM (vence 09:30 NY) a la SPXW (16:00 NY) "
                                "con el vencimiento de las 16:00: no separable" % f)
    validos = dias_env[(dias_env >= 0) & (vivo_real if vivo_real is not None else True)]
    mas = float(validos.min()) if len(validos) else 0.0
    tope = max(1.0, mas + 0.01)
    F = cad.filas
    if len(F) == 0:
        return None
    v = F[:, 1].astype(int)
    ok = (v >= 0) & (v < len(dias_env))
    vi = np.clip(v, 0, max(0, len(dias_env) - 1))
    d = np.where(ok, dias_env[vi], -1.0)
    ok_spec = ok & (d >= 0) & (d <= tope)
    desacuerdo = 0
    if vivo_real is not None:
        ok = ok_spec & vivo_real[vi]
        desacuerdo = int((ok_spec != ok).sum())
    else:
        ok = ok_spec
    if not ok.any():
        return None
    F = F[ok]; d = d[ok]
    return dict(K=F[:, 0], T=np.maximum(d, piso_dias) / 365.0, oic=F[:, 2], oip=F[:, 3], ivc=F[:, 4], ivp=F[:, 5], volc=F[:, 6], volp=F[:, 7],
                env=env, tope=tope, mas=mas, n=int(ok.sum()), desacuerdo=desacuerdo, aviso_am=aviso_am)


def perfil_cp(fl, S, es_fut, tasa):
    """backtest_familia.perfil_cp: GEX por strike y por lado (x100 x S^2 x 1 %; C1 se aplica al sumar libros y en los montos)."""
    if fl is None or S <= 0:
        return None
    g = gam(es_fut, tasa)
    gc, gp = g(S, fl["K"], fl["T"], fl["ivc"]), g(S, fl["K"], fl["T"], fl["ivp"])
    esc = 100.0 * S * S * 0.01
    a_vc, a_vp = gc * fl["volc"] * esc, -gp * fl["volp"] * esc
    a_oc, a_op = gc * fl["oic"] * esc, -gp * fl["oip"] * esc
    aporta = (a_vc + a_vp != 0) | (a_oc + a_op != 0)
    if not aporta.any():
        return None
    ks, inv = np.unique(fl["K"][aporta], return_inverse=True)
    n = len(ks)

    def s(x):
        return np.bincount(inv, x[aporta], n)
    r = dict(K=ks, gvC=s(a_vc), gvP=s(a_vp), goC=s(a_oc), goP=s(a_op))
    r["gv"] = r["gvC"] + r["gvP"]; r["go"] = r["goC"] + r["goP"]
    return r


def zero_estandar(fl, S, es_fut, paso, semi, centro, tasa):
    """backtest_familia.zero_estandar (C5): la suma de todo el libro (horizonte Hoy, sin el filtro de 3 %) reevaluada en una grilla
    alrededor de S; el cruce de signo mas cercano a S, en el eje del libro. Devuelve (zero_vol, zero_oi)."""
    if fl is None or S <= 0:
        return float("nan"), float("nan")
    g = gam(es_fut, tasa)
    c0 = float(round(S / centro)) * centro
    n = int(round(semi / paso))
    xs = c0 + paso * np.arange(-n, n + 1)
    X = xs[:, None]
    gc = g(X, fl["K"][None, :], fl["T"][None, :], fl["ivc"][None, :]); gp = g(X, fl["K"][None, :], fl["T"][None, :], fl["ivp"][None, :])
    x2 = xs * xs
    cv = (gc * fl["volc"][None, :] - gp * fl["volp"][None, :]).sum(1) * x2
    co = (gc * fl["oic"][None, :] - gp * fl["oip"][None, :]).sum(1) * x2
    out = []
    for c in (cv, co):
        s = np.sign(c)
        idx = np.nonzero((s[:-1] * s[1:]) < 0)[0]
        if len(idx) == 0:
            out.append(float("nan")); continue
        z = xs[idx] + (xs[idx + 1] - xs[idx]) * (-c[idx]) / (c[idx + 1] - c[idx])
        out.append(float(z[np.argmin(np.abs(z - S))]))
    return out[0], out[1]


# ====================================================================================================== las reglas (las series)
def _arg(mask, val, mayor=True):
    idx = np.nonzero(mask)[0]
    if len(idx) == 0:
        return None
    return int(idx[np.argmax(val[idx])] if mayor else idx[np.argmin(val[idx])])


def radio_de(rol, c):
    """el radio en pts del futuro: el del libro (NQ: 100 los tres; ES: 25 / 20 / 20) o el de la familia (FAM y CONF; NQ 100, ES 20)."""
    return c["radio_familia"] if rol == "familia" else c["radio_libro"][rol]


def radio_r(b, c):
    """extremos_rebote._R: min(fut x 2 %, radio del libro) (NQ: min(2 %, 100 pts))."""
    return min(b.fut * c["radio_pct"], radio_de(b.rol, c))


def r_muros(b, oi, c):
    """extremos_rebote.f_muros: D1 'muro C' = argmax gvC (> 0) y D2 'muro P' = argmin gvP (< 0) en |Fut - fut| <= R."""
    m = np.abs(b.Fut - b.fut) <= radio_r(b, c)
    C = b.goC if oi else b.gvC; P = b.goP if oi else b.gvP
    out = []
    i = _arg(m & (C > 0), C, True)
    if i is not None:
        out.append(("D1", i, "C"))
    j = _arg(m & (P < 0), P, False)
    if j is not None:
        out.append(("D2", j, "P"))
    return out


def r_majors(b, oi, c):
    """extremos_rebote.f_majors: D1 'M+' = argmax neto (> 0), D2 'M-' = argmin neto (< 0) en R."""
    m = np.abs(b.Fut - b.fut) <= radio_r(b, c)
    g = b.go if oi else b.gv
    out = []
    i = _arg(m & (g > 0), g, True)
    if i is not None:
        out.append(("D1", i, "N"))
    j = _arg(m & (g < 0), g, False)
    if j is not None:
        out.append(("D2", j, "N"))
    return out


def r_topn(b, oi, c):
    """TOPN (DECISION DEL C#: texto de CatalogoFamilia + N de ReglasFam): los N de mayor |neto| en R, de mayor a menor (D1..DN).
    Desempate (SUPUESTO): el de menor strike primero."""
    g = b.go if oi else b.gv
    m = (np.abs(b.Fut - b.fut) <= radio_r(b, c)) & (g != 0)
    idx = np.nonzero(m)[0]
    if len(idx) == 0:
        return []
    o = idx[np.argsort(-np.abs(g[idx]), kind="stable")][: c["topn"]]
    return [("D%d" % (k + 1), int(i), "N") for k, i in enumerate(o)]


def cruces(Fut, g, isla):
    """backtest_familia.cruces (C3): cruces de signo entre strikes vecinos con g != 0, salteando las islas (signo distinto a sus dos
    vecinos y |g| < isla x el menor vecino; se evalua sobre la secuencia original y se quitan todas juntas)."""
    nz = np.nonzero(g != 0)[0]
    if len(nz) < 2:
        return []
    if isla > 0 and len(nz) >= 3:
        gg = g[nz]; s = np.sign(gg); a = np.abs(gg)
        mid = (s[1:-1] != s[:-2]) & (s[1:-1] != s[2:]) & (a[1:-1] < isla * np.minimum(a[:-2], a[2:]))
        keep = np.ones(len(nz), bool); keep[1:-1] = ~mid
        nz = nz[keep]
    out = []
    for i0, i1 in zip(nz[:-1], nz[1:]):
        if g[i0] * g[i1] < 0:
            out.append(float(Fut[i0] + (Fut[i1] - Fut[i0]) * (-g[i0]) / (g[i1] - g[i0])))
    return out


def r_ztp(b, oi, c):
    """backtest_familia.ztp: techo (menor cruce > fut) y piso (mayor cruce < fut) a <= RADIO (NQ 100 pts; ES el radio del libro)."""
    g = b.go if oi else b.gv
    zs = [z for z in cruces(b.Fut, g, c["isla"]) if abs(z - b.fut) <= radio_de(b.rol, c)]
    ar = [z for z in zs if z > b.fut]; ab = [z for z in zs if z < b.fut]
    return ([("D1", min(ar))] if ar else []) + ([("D2", max(ab))] if ab else [])


def r_zest(b, oi, c):
    """backtest_familia.zest: el zero estandar si existe y esta a <= 300 pts."""
    z = b.zo if oi else b.zv
    return [("Z", float(z))] if (z == z and abs(z - b.fut) <= c["zest_max"]) else []


class Fus:
    """backtest_familia.fusion (C1): perfil de la familia en USD por 1 % (NQ x 20, NDX/QQQ x 100 sobre 100), grilla de fam_grilla pts."""
    def __init__(self, bms, fut, c):
        self.fut = float(fut); self.rol = "familia"
        Fut = np.concatenate([np.round(b.Fut / c["fam_grilla"]) * c["fam_grilla"] for b in bms])
        ks, inv = np.unique(Fut, return_inverse=True)
        self.Fut = ks
        for k in ("gvC", "gvP", "goC", "goP"):
            x = np.concatenate([getattr(b, k) * (c["mult"][b.rol] / 100.0) for b in bms])
            setattr(self, k, np.bincount(inv, x, len(ks)))
        self.gv = self.gvC + self.gvP; self.go = self.goC + self.goP


def confluencia(pools, fut, c):
    """backtest_familia.confluencia: niveles de 2 o mas libros a <= conf_pts entre si; la raya es el promedio; a <= RADIO del futuro."""
    items = sorted((p, lb) for lb, ps in pools.items() for p in ps)
    out = []; i = 0
    while i < len(items):
        j = i
        while j + 1 < len(items) and items[j + 1][0] - items[i][0] <= c["conf_pts"]:
            j += 1
        grupo = items[i:j + 1]
        if len({lb for _, lb in grupo}) >= 2:
            p = float(np.mean([x for x, _ in grupo]))
            if abs(p - fut) <= c["radio_familia"]:
                out.append(("D%d" % (len(out) + 1), p))
            i = j + 1
        else:
            i += 1
    return out


# ====================================================================================================== fotos del libro del futuro (viva3)
_RX_TS_VIVA = re.compile(r'"ts":"([^"]+)"')
_RX_FUT_VIVA = re.compile(r'"futuro":([-0-9.eE]+)')


class FotoFut:
    __slots__ = ("ts", "futuro", "linea", "_cad", "n_crudas")

    def __init__(self, ts, futuro, linea, n_crudas):
        self.ts = ts; self.futuro = futuro; self.linea = linea; self._cad = None; self.n_crudas = n_crudas

    def cadena(self):
        """preview_niveles.foto_viva3: dias = los de TODAS las filas crudas redondeados a 4 (round de Python); una fila por (K, venc)
        con call y put; sin las de iv <= 0 o sin OI ni volumen."""
        if self._cad is None:
            r = json.loads(self.linea)
            filas = r.get("filas") or []
            dias = sorted(set(round(float(x[1]), 4) for x in filas)); por = {}
            for x in filas:
                K, di, call, oi, iv, vol = float(x[0]), round(float(x[1]), 4), float(x[2]) >= 0.5, float(x[3]), float(x[4]), float(x[7])
                if iv <= 0 or (oi <= 0 and vol <= 0):
                    continue
                e = por.setdefault((K, dias.index(di)), [K, dias.index(di), 0, 0, 0, 0, 0, 0])
                if call:
                    e[2], e[4], e[6] = oi, iv, vol
                else:
                    e[3], e[5], e[7] = oi, iv, vol
            self._cad = Cadena(list(por.values()), dias, self.futuro, True, self.ts)
            self.linea = None
        return self._cad


def fotos_fut(carpeta, raiz, ses):
    """PythiaGex<N>\\viva\\viva3-<raiz>-<dia UTC>.jsonl: ts dentro de la sesion (inclusive, como libros._ventana), dedup por ts (gana la de
    mas filas crudas, como fuentes_3_0.fotos_viva3). La cadena se arma recien cuando se usa."""
    vistos = {}
    for d in sorted(set([ses.ini.strftime("%Y-%m-%d")] + ses.dias_utc())):
        p = os.path.join(carpeta, "viva", "viva3-%s-%s.jsonl" % (raiz, d))
        if not os.path.exists(p):
            continue
        with open(p, encoding="utf-8", errors="replace") as fh:
            for l in fh:
                l = l.strip()
                if len(l) < 40 or not l.endswith("}"):
                    continue
                m = _RX_TS_VIVA.search(l, 0, 80); mf = _RX_FUT_VIVA.search(l, 0, 160)
                if not m or not mf:
                    continue
                try:
                    ts = datetime.strptime(m.group(1), "%Y-%m-%d %H:%M:%S"); fut = float(mf.group(1))
                except ValueError:
                    continue
                if not (ses.ini <= ts <= ses.fin) or fut <= 0 or '"filas":[]' in l:
                    continue
                n = l.count("],[") + 1
                v = vistos.get(ts)
                if v is not None and v.n_crudas >= n:
                    continue
                vistos[ts] = FotoFut(ts, fut, l, n)
    return sorted(vistos.values(), key=lambda x: x.ts)


# ====================================================================================================== fotos de CBOE (indice y ETF)
class FotoCboe:
    """una bajada de CBOE (preview_niveles.foto_cboe_de_json / libros.fotos_cboe). Las filas se releen del archivo cuando hacen falta."""
    __slots__ = ("generado", "ts", "t_dato", "spot", "oi_total", "origen", "ruta", "n_linea", "dias", "fechas", "am", "_cad")


class LectorGz:
    """lineas de un .jsonl.gz (miembros gzip concatenados), con una cache chica para releer filas."""
    def __init__(self, maximo=6):
        self.cache = {}; self.orden = []; self.maximo = maximo

    def lineas(self, ruta):
        if ruta in self.cache:
            return self.cache[ruta]
        partes = []
        try:
            with gzip.open(ruta, "rb") as fh:
                while True:
                    ch = fh.read(1 << 20)
                    if not ch:
                        break
                    partes.append(ch)
        except (OSError, EOFError):
            pass                                   # cola truncada (se corto a mitad de un miembro): lo leido sirve
        ls = b"".join(partes).split(b"\n")
        self.cache[ruta] = ls; self.orden.append(ruta)
        while len(self.orden) > self.maximo:
            self.cache.pop(self.orden.pop(0), None)
        return ls

    def soltar(self, ruta):
        self.cache.pop(ruta, None)
        if ruta in self.orden:
            self.orden.remove(ruta)


LECTOR = LectorGz()


def leer_cboe(carpetas, archivo, dias_cal, sesion_de):
    """libros.fotos_cboe sobre cada carpeta: por cada dia de calendario d las bajadas con 'generado' dentro de la sesion d (afuera queda la
    pausa de CME); dedup por cadena_ts DENTRO de cada carpeta (gana la bajada mas temprana). Devuelve {origen: {ts: FotoCboe}}."""
    ventanas = [sesion_de(d) for d in dias_cal]
    archivos = set()
    for d in dias_cal:
        dd = datetime.strptime(d, "%Y-%m-%d")
        archivos.update(((dd - timedelta(days=1)).strftime("%Y-%m-%d"), d))
    out = {}
    for origen, carpeta in carpetas:
        vistos = out.setdefault(origen, {})
        for d in sorted(archivos):
            p = os.path.join(carpeta, "cadena-%s-%s.jsonl.gz" % (archivo, d))
            if not os.path.exists(p):
                continue
            for n, l in enumerate(LECTOR.lineas(p)):
                if len(l) < 20:
                    continue
                try:
                    j = json.loads(l)
                    gen = utc_iso(j["generado"]); ts = str(j["cadena_ts"])
                except (ValueError, KeyError):
                    continue
                if not any(v.ini <= gen <= v.fin for v in ventanas):
                    continue
                if ts in vistos and vistos[ts].generado <= gen:
                    continue
                c = j.get("cadena") or {}
                filas = [x[:8] for x in c.get("filas", []) if len(x) >= 8]
                if not filas:
                    continue
                f = FotoCboe()
                f.generado = gen; f.ts = ts; f.origen = origen; f.ruta = p; f.n_linea = n; f._cad = None
                ut = c.get("ultimo_trade")
                try:
                    f.t_dato = ny_a_utc(datetime.fromisoformat(ut)) if ut else \
                        datetime.strptime(ts, "%Y-%m-%d %H:%M:%S") - timedelta(seconds=int(j.get("retraso_s") or 902))
                except ValueError:
                    f.t_dato = datetime.strptime(ts, "%Y-%m-%d %H:%M:%S") - timedelta(seconds=902)
                f.spot = float(c.get("spot_idx") or j.get("spot") or 0)
                arr = np.asarray(filas, float)
                f.oi_total = float(arr[:, 2].sum() + arr[:, 3].sum())
                f.dias = [float(v.get("dias") or 0) for v in c.get("vencimientos", [])]
                f.fechas = [str(v.get("f") or "") for v in c.get("vencimientos", [])]
                f.am = [bool(v.get("am")) for v in c.get("vencimientos", [])]
                vistos[ts] = f
            LECTOR.soltar(p)
            time.sleep(0.02)
    return out


def cadena_cboe(f):
    if f._cad is None:
        j = json.loads(LECTOR.lineas(f.ruta)[f.n_linea])
        c = j.get("cadena") or {}
        filas = [x[:8] for x in c.get("filas", []) if len(x) >= 8]
        f._cad = Cadena(filas, f.dias, f.spot, False, f.generado, f.fechas if all(f.fechas) else None, getattr(f, "am", None))
    return f._cad


def leer_muestras(carpeta, lid):
    """familia\\muestras-<libro>-<sesion>.jsonl de la 4.1: el precio que USO en cada muestra (la ultima linea por ts) y DESDE CUANDO lo tenia:
    la linea se agrega al calcular la muestra, asi que la mayor hora 'generado' de las lineas anteriores del archivo es una cota de cuando se
    guardo (una muestra de un hueco de ATAS se guarda recien en un arranque posterior, cuando hay velas del grafico). Se usa solo para las
    muestras que la cinta archivada no puede verificar, y como contraste. Devuelve {ts: (linea, cota)}."""
    out = {}
    for p in sorted(glob.glob(os.path.join(carpeta, "familia", "muestras-%s-*.jsonl" % lid))):
        cota = None
        try:
            with open(p, encoding="utf-8", errors="replace") as fh:
                for l in fh:
                    try:
                        j = json.loads(l)
                        g = utc_iso(j["generado"])
                    except (ValueError, KeyError):
                        continue
                    cota = g if cota is None or g > cota else cota
                    out[j["ts"]] = (j, cota)
        except OSError:
            pass
    return out


# ====================================================================================================== que sabia el indicador y cuando
class Conocimiento:
    """los arranques de la 4.1 (cada uno lee la carpeta extra de CBOE una sola vez) y sus corridas en vivo. Fuente: pythiagex4-cboe.log
    ('<iso>Z  historia <libro>: ...'); sin el log, los inicios de los tramos de la cinta."""

    def __init__(self, log_cboe, libros_log, cinta, desde, hasta):
        self.arranques = []; self.fuente = ""; self.extra_hasta = {}
        lineas = []
        if log_cboe and os.path.exists(log_cboe):
            rx = re.compile(r"^(\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d)Z\s+historia (\S+):(.*)$")
            rx_a = re.compile(r" a (\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d)")
            try:
                with open(log_cboe, encoding="utf-8", errors="replace") as fh:
                    for l in fh:
                        m = rx.match(l)
                        if m and m.group(2) in libros_log:
                            t = datetime.strptime(m.group(1), "%Y-%m-%dT%H:%M:%S")
                            if desde - timedelta(days=9) <= t <= hasta + timedelta(days=1):
                                self.arranques.append(t)
                                ma = rx_a.search(m.group(3))
                                lineas.append((t, m.group(2), datetime.strptime(ma.group(1), "%Y-%m-%dT%H:%M:%S") if ma else None))
            except OSError:
                pass
            self.fuente = os.path.basename(log_cboe)
        if not self.arranques:
            self.arranques = [de_ms(a) for a, b in cinta.tramos if desde - timedelta(days=9) <= de_ms(a) <= hasta + timedelta(days=1)]
            self.fuente = "inicios de los tramos de la cinta (sin log)"
        xs = []
        for t in sorted(self.arranques):
            if not xs or (t - xs[-1]).total_seconds() > 60:
                xs.append(t)
        self.arranques = xs
        # DECISION DEL C# (BajadorCboe.CargarHistoria): la carpeta extra se lee UNA vez, al arrancar; la linea "historia <libro>: ... de X a Y"
        # dice la ultima foto que quedo en memoria (Y = generado). Una foto de la extra con generado > Y no la tuvo ese arranque, aunque sea
        # anterior a la hora de la linea (medido 27-10 15:19: cboe_local escribio la foto de las 15:19:11 despues de que la 5.0 leyo su
        # archivo, entre las 15:19:10 y las 15:19:20).
        for t, lib, y in lineas:
            R = max((x for x in xs if x <= t), default=None)
            if R is not None and (t - R).total_seconds() <= 60 and y is not None:
                self.extra_hasta[(R, lib)] = y
        self.corridas = []
        for i, R in enumerate(xs):
            nxt = xs[i + 1] if i + 1 < len(xs) else datetime(9999, 1, 1)
            fin = None
            r = ms_de(R)
            for a, b in cinta.tramos:
                if a <= r + 120000 and b >= r - 1000:
                    fin = de_ms(b); break
            self.corridas.append((R, min(fin, nxt) if fin is not None else nxt))

    def corte(self, t):
        """(R, en_vivo): el arranque que calculo el minuto t. En vivo: el de su corrida (t + 6 s dentro de ella). Si no (ATAS cerrado o la
        corrida ya habia terminado), el minuto lo relleno el primer arranque posterior. (None, False) si no hay arranques conocidos."""
        c = t + timedelta(seconds=CALCULO_S)
        for R, fin in self.corridas:
            if R <= c < fin:
                return R, True
        for R in self.arranques:
            if R > t:
                return R, False
        return None, False


def _en_vivo(mi, cota, corte):
    """la muestra guardada se calculo en vivo en la corrida del arranque 'corte': su foto llego despues del arranque y la linea se guardo a
    <= 180 s de su 'generado' (si se guardo mucho despues, fue en un arranque posterior)."""
    try:
        g = utc_iso(mi["generado"])
    except (KeyError, ValueError):
        return False
    return g >= corte and (cota - g).total_seconds() <= 180


class EstadoFoto:
    """el estado de la conversion de una foto conocido a su 'generado' (backtest_familia.conversion), para una lista de fotos dada."""
    __slots__ = ("f", "t_spot", "precio", "fuente_precio", "vivo", "muestra", "modo", "base_dia", "noche_ref", "razon", "razon_contrato",
                 "razon_n", "oi_viejo", "congelada", "nb", "excluida", "fuentes")


def carry_teorico(perfil, contrato, t, fut):
    """C7 ES (DISENO_5_0 §4): base teorica = fut x 3,4 % x dias al vencimiento del futuro / 365 y su tolerancia max(25 %, 3 pts)."""
    cb = perfil["c"]["cota_base"]
    dias = (vencimiento_contrato(perfil, contrato) - t).total_seconds() / 86400.0
    teo = fut * cb["carry"] * dias / 365.0
    return teo, max(cb["tol_rel"] * teo, cb["tol_abs"])


def base_fuera_de_cota(perfil, contrato, t, fut, base):
    """'' si la base cumple la cota de ES (0 < base <= 150 y cerca del carry); si no, el motivo. En NQ no hay cota (la especificacion la
    descarta: 'cota de carry (no discrimina)')."""
    cb = perfil["c"]["cota_base"]
    if not cb or not (base == base):
        return ""
    if not (0.0 < base <= cb["maximo"]):
        return "base %.2f fuera de (0, %g]" % (base, cb["maximo"])
    if not (fut == fut):
        return ""
    teo, tol = carry_teorico(perfil, contrato, t, fut)
    if abs(base - teo) > tol:
        return "base %.2f lejos del carry teorico %.2f (tolerancia %.2f)" % (base, teo, tol)
    return ""


def ultimo_reinicio_etf(t_utc, hora):
    """C8 ES (DISENO_5_0 §4): el ultimo tercer viernes de mar/jun/sep/dic a 'hora' NY (ex-dividendo de SPY) anterior o igual a t."""
    hh, mm = (int(x) for x in hora.split(":"))
    mejor = None
    for yy in (t_utc.year - 1, t_utc.year):
        for m in (3, 6, 9, 12):
            inst = ny_a_utc(tercer_viernes(yy, m) + timedelta(hours=hh, minutes=mm))
            if inst <= t_utc and (mejor is None or inst > mejor):
                mejor = inst
    return mejor


def conversion(rol, fotos, cinta, perfil, ses_ref, contrato, muestras_ind, corte=None, sin_ind_desde=None):
    """backtest_familia.conversion sobre la lista 'fotos' (ordenada por generado). Indice (C7): muestra = precio(ts - 900 s) - spot con el
    spot vivo y la hora NY del spot entre 09:35 y 15:59 (dia habil); modo dia = mediana robusta de las ultimas 24 de la rueda (>= 5);
    noche = la ultima rueda con >= 20 (todas sus muestras) x el decaimiento del carry. ETF (C8): muestra = precio(ts - 900 s) / spot con el
    spot vivo (a cualquier hora); mediana robusta de las ultimas 24 (>= 5). El precio: cinta; si no se puede verificar, el que guardo la 4.1,
    solo si lo TENIA en ese momento: guardado antes del arranque 'corte', o calculado en vivo despues de el (linea guardada a <= 180 s de su
    'generado'); una muestra que se guardo recien en un arranque posterior no existia todavia (sin precio)."""
    c = perfil["c"]
    gen = [f.generado for f in fotos]
    ruedas = {}
    roll24 = []; roll24_f = []; roll_dia = None; roll_contrato = None
    oi_viejo = False; oi_prev = None
    est = []
    cont = {"tick": 0, "vela": 0, "indicador": 0, "sin precio": 0, "dif": 0, "dif_ej": []}
    # oi_fresco: en modo corregido el reloj es la hora de NY + 4 h contra las horas UTC del Python (13:30, 22:00, 03:00): DECISION DEL C#
    # (ConversionNdx: "horas del reloj (UTC en paridad, NY+4 h en corregido)"). En horario de verano es la hora UTC.
    reloj = (lambda x: hm(a_ny(x) + timedelta(hours=4))) if ses_ref.corregida else hm
    h_r, h_n, h_m = ("13:30", "22:00", "03:00")
    for i, f in enumerate(fotos):
        e = EstadoFoto(); e.f = f
        tsd = datetime.strptime(f.ts, "%Y-%m-%d %H:%M:%S")
        e.t_spot = tsd - timedelta(seconds=c["retraso"])
        p, fuente = cinta.precio(ms_de(e.t_spot))
        mi, cota = muestras_ind.get(f.ts, (None, None))
        if fuente in ("tick", "vela"):
            cont[fuente] += 1
            if mi is not None and mi.get("precio") is not None and abs(float(mi["precio"]) - p) > 1e-6:
                cont["dif"] += 1
                if len(cont["dif_ej"]) < 5:
                    cont["dif_ej"].append((f.ts, float(mi["precio"]), p, fuente))
        elif mi is not None and mi.get("precio") is not None and (corte is None or cota <= corte or _en_vivo(mi, cota, corte)) and \
                (sin_ind_desde is None or e.t_spot < sin_ind_desde):
            p = float(mi["precio"]); fuente = "indicador"; cont["indicador"] += 1
        else:
            p = float("nan"); cont["sin precio"] += 1
            if mi is not None and mi.get("precio") is not None:
                cont["guardado despues"] = cont.get("guardado despues", 0) + 1
        e.precio = p; e.fuente_precio = fuente; e.excluida = ""
        vivo = False; j = i - 1
        while j >= 0 and (gen[i] - gen[j]).total_seconds() <= c["vivo_ventana"]:
            if abs(fotos[j].spot - f.spot) > 1e-9:
                vivo = True; break
            j -= 1
        e.vivo = vivo
        ny = a_ny(e.t_spot)
        hmn = hm(ny)
        val = float("nan")
        if vivo and p == p and f.spot > 0:
            val = (p - f.spot) if rol == "ind" else (p / f.spot)
        # reglas de ES (DISENO_5_0 §4; en NQ no existen): la muestra que no cumple NO entra en ninguna mediana
        if val == val and rol == "ind" and c["cota_base"]:
            e.excluida = base_fuera_de_cota(perfil, contrato, e.t_spot, p, val)
        elif val == val and rol == "etf":
            if c["etf_horas"] and not (c["etf_horas"][0] <= hmn < c["etf_horas"][1]):
                e.excluida = "spot de SPY a las %s NY (fuera de %s-%s)" % (hmn, c["etf_horas"][0], c["etf_horas"][1])
            elif c["razon_rango"] and not (c["razon_rango"][0] <= val <= c["razon_rango"][1]):
                e.excluida = "razon %.4f fuera de %g-%g" % (val, c["razon_rango"][0], c["razon_rango"][1])
        if e.excluida:
            cont["excluidas ES"] = cont.get("excluidas ES", 0) + 1
            if len(cont.setdefault("excluidas_ej", [])) < 5:
                cont["excluidas_ej"].append((f.ts, e.excluida))
            val = float("nan")
        e.muestra = val
        if rol == "ind":
            en_hora = (c["base_hora"][0] <= hmn <= c["base_hora"][1]) and ny.weekday() < 5
            dia_ny = ny.strftime("%Y-%m-%d")
            if vivo and en_hora and val == val:
                rr = ruedas.setdefault(dia_ny, {"m": [], "contrato": contrato, "f": []})
                rr["m"].append(val); rr["f"].append(fuente)
                if roll_dia != dia_ny:
                    roll24 = []; roll24_f = []; roll_dia = dia_ny
                roll24.append(val); roll24 = roll24[-c["base_ultimas"]:]
                roll24_f.append(fuente); roll24_f = roll24_f[-c["base_ultimas"]:]
            b24, nb = robusta(roll24, c["base_piso"]) if (roll_dia == dia_ny and vivo and en_hora) else (float("nan"), 0)
            e.modo = "dia" if (vivo and en_hora and nb >= c["base_n_dia"]) else "noche"
            e.base_dia = b24 if e.modo == "dia" else float("nan"); e.nb = nb
            ref = None
            for dk in sorted(ruedas, reverse=True):
                if len(ruedas[dk]["m"]) >= c["base_n_rueda"]:
                    ref = dk; break
            if ref is not None:
                ba, _ = robusta(ruedas[ref]["m"], c["base_piso"])
                e.noche_ref = (ref, ba, ruedas[ref]["contrato"], len(ruedas[ref]["m"]))
            else:
                e.noche_ref = None
            fs_ = roll24_f if e.modo == "dia" else (ruedas[ref]["f"] if ref is not None else [])
            e.fuentes = {x: fs_.count(x) for x in set(fs_)}
        else:
            clave_roll = (contrato, ultimo_reinicio_etf(e.t_spot, c["etf_reinicio"]) if c["etf_reinicio"] else None)
            if roll_contrato != clave_roll:
                roll24 = []; roll24_f = []; roll_contrato = clave_roll
            if val == val:
                roll24.append(val); roll24 = roll24[-c["razon_ultimas"]:]
                roll24_f.append(fuente); roll24_f = roll24_f[-c["razon_ultimas"]:]
            e.fuentes = {x: roll24_f.count(x) for x in set(roll24_f)}
            r24, nr = robusta(roll24, c["razon_piso_rel"] * (float(np.median(roll24)) if roll24 else 0.0))
            if c["razon_rango"] and r24 == r24 and not (c["razon_rango"][0] <= r24 <= c["razon_rango"][1]):
                r24 = float("nan")
            e.razon = r24 if nr >= c["razon_n"] else float("nan"); e.razon_contrato = roll_contrato[0]; e.razon_n = nr
        # oi_fresco (la marca '(OI 2s)' de la meta 'o' de los libros de CBOE; en la especificacion es la sensibilidad 'fresco')
        g = f.generado
        hmu = reloj(g); hprev = reloj(fotos[i - 1].generado) if i else "12:00"
        if h_r <= hmu < h_n:
            oi_viejo = False
        elif (hmu >= h_n or hmu < h_m) and not oi_viejo and (h_r <= hprev < h_n or (i and (g - fotos[i - 1].generado).total_seconds() > 6 * 3600)):
            oi_viejo = True
        elif h_m <= hmu < h_r and oi_viejo and oi_prev and abs(f.oi_total - oi_prev) / oi_prev > 0.05:
            oi_viejo = False
        e.oi_viejo = oi_viejo
        if f.oi_total > 0:
            oi_prev = f.oi_total
        e.congelada = hm(a_ny(f.t_dato)) >= "15:59"
        est.append(e)
    return est, ruedas, cont


def base_ind(e, t, perfil, ses_ref, contrato, fut=float("nan")):
    """backtest_familia.base_ndx (C7): modo dia = b24; noche = ba x (EXP - t) / (EXP - cierre de la rueda de referencia). ES: la base del
    minuto ademas tiene que cumplir la cota (DISENO_5_0 §4); si no, no hay libro del indice."""
    b, txt = _base_ind(e, t, perfil, ses_ref, contrato)
    fuera = base_fuera_de_cota(perfil, contrato, t, fut, b)
    if fuera:
        return float("nan"), "cota C7 de ES: " + fuera + " (" + txt + ")"
    return b, txt


def _base_ind(e, t, perfil, ses_ref, contrato):
    if e.modo == "dia":
        return e.base_dia, "dia: mediana robusta de las ultimas %d muestras de la rueda" % e.nb
    ref = e.noche_ref
    if ref is None:
        return float("nan"), "sin rueda de referencia (>= 20 muestras)"
    dk, ba, contr, nm = ref
    if contr != contrato:
        return float("nan"), "rueda de otro contrato (roll)"
    exp = vencimiento_contrato(perfil, contr)
    t_close = ses_ref.hora_fija(dk, 16)          # 20:00 UTC de dk (paridad) o 16:00 NY (corregida)
    den = (exp - t_close).total_seconds()
    fac = (exp - t).total_seconds() / den if den > 0 else 1.0
    return ba * fac, "noche: rueda %s, %d muestras, mediana %.4f x carry %.8f" % (dk, nm, ba, fac)


def vigencia_cboe(e, c, ses_ref):
    """backtest_familia.vigencia_cboe (C9): 25 min desde generado; congelada: hasta la proxima apertura (13:30 UTC; 09:30 NY corregida)."""
    f = e.f
    h1 = f.generado + timedelta(seconds=c["vida_cboe"])
    if e.congelada:
        if ses_ref.corregida:
            gny = a_ny(f.generado)
            prox = ny_a_utc(datetime(gny.year, gny.month, gny.day, 9, 30))
            if prox <= f.generado:
                prox = ny_a_utc(datetime(gny.year, gny.month, gny.day) + timedelta(days=1, hours=9, minutes=30))
        else:
            prox = datetime(f.generado.year, f.generado.month, f.generado.day, 13, 30)
            if prox <= f.generado:
                prox += timedelta(days=1)
        h1 = max(h1, prox)
    return h1


# ====================================================================================================== C2: OI del libro del futuro con fecha
def oi_fut_viejo_hasta(fotos, ses, c):
    """backtest_familia.oi_nq_viejo_hasta: hasta que instante el OI del libro del futuro es el de DOS sesiones atras. Ventana del inicio de
    la sesion a +5 h 30 (22:00-03:30 UTC en verano); 'primera foto tardia' desde +3 h 55 (01:55 UTC); sin salto: hasta +4 h (02:00 UTC)."""
    ini = ses.ini; lim = ses.ini + timedelta(hours=5, minutes=30)
    dia = datetime.strptime(ses.dia, "%Y-%m-%d")
    if dia.weekday() == 0 or es_posferiado(ses.dia):
        return None, "lunes o posferiado: OI ya actualizado"
    noche = [f for f in fotos if ini <= f.ts <= lim]
    if not noche:
        return None, "sin fotos de noche"

    def mapa(f):
        out = {}
        cad = f.cadena()
        for x in cad.filas:
            venc = (f.ts + timedelta(days=float(cad.dias[int(x[1])]))).strftime("%m-%d")
            out[(x[0], venc, 1)] = x[2]; out[(x[0], venc, 0)] = x[3]
        return out
    prev = mapa(noche[0])
    for f in noche[1:]:
        cur = mapa(f)
        com = [k for k in cur if k in prev and (cur[k] > 0 or prev[k] > 0)]
        if len(com) >= c["c2"]["comunes"] and sum(1 for k in com if cur[k] != prev[k]) >= c["c2"]["frac"] * len(com):
            return f.ts, "salto de OI medido a las %s UTC" % f.ts.strftime("%H:%M:%S")
        prev = cur
    primera = noche[0].ts
    if ses.ini + timedelta(hours=3, minutes=55) <= primera < ses.ini + timedelta(hours=15, minutes=30):
        return None, "primera foto %s UTC (despues del salto habitual): OI supuesto actualizado" % primera.strftime("%H:%M")
    return ses.ini + timedelta(hours=4), "sin salto visible: suprimido hasta %s UTC (conservador)" % (ses.ini + timedelta(hours=4)).strftime("%H:%M")


def oi_vivo_suprime(ctx, t):
    """preview_niveles._oi_hasta_vivo (lo que corre EN VIVO): sin salto visto todavia y antes del limite (+5 h 30, 03:30 UTC en verano), el OI
    del libro del futuro se toma como de anteayer hasta el limite ('esperando el salto'). Solo cambia algo respecto de la version de backtest
    cuando la noche NO tuvo salto: un minuto calculado en vivo entre +4 h y +5 h 30 queda suprimido (la de backtest ya no suprime)."""
    if not ctx.oi_sin_salto:
        return False
    corte, en_vivo = ctx.conoc.corte(t)
    lim = ctx.ses.ini + timedelta(hours=5, minutes=30)
    return en_vivo and ctx.ses.ini <= t and t + timedelta(seconds=CALCULO_S) < lim


# ====================================================================================================== un libro en un minuto
class Libro:
    """un libro en un minuto, ya en precio del futuro (backtest_familia.BM)."""
    __slots__ = ("rol", "id", "fut", "S", "conv", "conv_txt", "Fut", "K", "gvC", "gvP", "gv", "goC", "goP", "go", "zv", "zo", "oi_ok",
                 "oi_fresco", "foto", "est", "foto_id", "dato", "congelada", "hasta", "fl", "env_b", "origen", "avisos")

    def strike_de(self, precio):
        return precio / self.conv if self.rol == "etf" else precio - self.conv

    def al_fut(self, K):
        return K * self.conv if self.rol == "etf" else K + self.conv


def armar_libro(rol, perfil, fl, S, fut, conv):
    c = perfil["c"]
    r = perfil_cp(fl, S, rol == "fut", c["tasa"])
    if r is None:
        return None
    b = Libro()
    b.rol = rol; b.id = perfil[rol]["id"]; b.fut = float(fut); b.S = S; b.conv = conv; b.fl = fl; b.avisos = []
    if fl.get("desacuerdo"):
        b.avisos.append("C10 ES: %d fila(s) que los 'dias' envejecidos dejan y el vencimiento real (16:00 NY) ya paso: excluidas" % fl["desacuerdo"])
    if fl.get("aviso_am"):
        b.avisos.append("AVISO_AM " + fl["aviso_am"])
    Fut = (r["K"] * conv) if rol == "etf" else (r["K"] + conv)
    m = np.abs(Fut - b.fut) <= b.fut * c["filtro_pct"]
    b.Fut = Fut[m]; b.K = r["K"][m]
    for k in ("gvC", "gvP", "gv", "goC", "goP", "go"):
        setattr(b, k, r[k][m])
    return b


def zero_de(b, perfil):
    paso, semi, centro = perfil["c"]["zest"][b.rol]
    zv, zo = zero_estandar(b.fl, b.S, b.rol == "fut", paso, semi, centro, perfil["c"]["tasa"])
    return (b.al_fut(zv) if zv == zv else zv), (b.al_fut(zo) if zo == zo else zo)


# ====================================================================================================== el contexto de una sesion
class Contexto:
    """todo lo que hace falta para recalcular cualquier minuto de una sesion (se carga una vez)."""

    def __init__(self, perfil, carpeta, dia, instrumento, opciones):
        self.perfil = perfil; self.carpeta = carpeta; self.opciones = opciones; self.c = perfil["c"]
        self.corregida = not opciones.get("paridad", False)
        self.ses = Sesion(dia, self.corregida)
        self.instrumento = instrumento
        self.contrato = instrumento[-2:]
        t0 = time.time()
        d1 = datetime.strptime(dia, "%Y-%m-%d")
        self.dias_cal = [(d1 - timedelta(days=k)).strftime("%Y-%m-%d") for k in range(7, -1, -1)]   # DiasHistoria = 7 de la 4.1
        dias_cinta = set()
        for d in self.dias_cal:
            s = Sesion(d, self.corregida)
            dias_cinta.update(s.dias_utc()); dias_cinta.add(s.ini.strftime("%Y-%m-%d"))
        # la cinta va con el nombre del INSTRUMENTO DEL GRAFICO (AdaptadoresFamilia: seg-<Instrumento>-<dia>.bin): en la 4.1 el grafico es
        # MNQZ6 (= el contrato del niv); en la 5.0 el grafico es la continua "MES" y el niv lleva el contrato sacado por fecha (MESZ6)
        candidatos = [instrumento] + ([instrumento[:-2]] if re.match(r"^[A-Z]+[FGHJKMNQUVXZ]\d$", instrumento) else [])
        for nombre in candidatos:
            self.cinta = Cinta(carpeta, nombre, sorted(dias_cinta), self.corregida)
            if self.cinta.archivos:
                break
        if not self.cinta.archivos:
            raise FaltanDatos("no hay cinta seg-{%s}-*.bin en %s" % (",".join(candidatos), os.path.join(carpeta, "cinta")))
        fin_tramos = max((b for a, b in self.cinta.tramos), default=0)
        self.horizonte = de_ms(max(fin_tramos, self.cinta.ultimo_tick))     # hasta donde llega la cinta guardada
        time.sleep(0.1)
        self.fotos_fut = fotos_fut(carpeta, perfil["fut"]["viva"], self.ses)
        self.ts_fut = np.array([np.datetime64(f.ts) for f in self.fotos_fut], dtype="datetime64[s]")
        time.sleep(0.1)
        extra = opciones.get("extra", perfil.get("extra"))
        self.carpetas_cboe = [("propia", os.path.join(carpeta, "cboe"))] + ([("extra", extra)] if extra and os.path.isdir(extra) else [])
        self.cboe = {}; self.muestras_ind = {}
        for rol in ("ind", "etf"):
            self.cboe[rol] = leer_cboe(self.carpetas_cboe, perfil[rol]["archivo"], self.dias_cal, lambda d: Sesion(d, self.corregida))
            self.muestras_ind[rol] = leer_muestras(carpeta, perfil[rol]["id"])
            time.sleep(0.1)
        self.conoc = Conocimiento(opciones.get("log_cboe", perfil.get("log_cboe")), (perfil["ind"]["log"], perfil["etf"]["log"]),
                                  self.cinta, self.ses.ini, self.ses.fin)
        self._conv = {}
        self.oi_hasta, self.oi_como = oi_fut_viejo_hasta(self.fotos_fut, self.ses, self.c)
        self.oi_sin_salto = self.oi_como.startswith("sin salto visible")
        self._corr = None
        self.seg_carga = time.time() - t0

    # -------------------------------------------------------------- lo conocido por el indicador
    def lista_cboe(self, rol, corte, sin_ind_desde=None, lim_extra=None):
        """lo que conocia el indicador al 'corte' (el arranque que calculo el minuto; None = todo): las fotos de la carpeta propia, las de la
        extra con generado <= corte, y los precios de muestra no verificables que ya tenia guardados. Dedup por cadena_ts entre carpetas:
        gana la bajada mas temprana (libros.fotos_cboe). Devuelve (estados, generados, ruedas, conteo)."""
        clave = (rol, corte, sin_ind_desde, lim_extra)
        if clave not in self._conv:
            todas = {}
            lim = self.conoc.extra_hasta.get((corte, self.perfil[rol]["log"]), corte) if corte is not None else None
            if lim_extra is not None:
                lim = lim_extra
            for origen, d in self.cboe[rol].items():
                for ts, f in d.items():
                    if origen == "extra" and lim is not None and f.generado > lim:
                        continue
                    if ts not in todas or f.generado < todas[ts].generado:
                        todas[ts] = f
            fotos = sorted(todas.values(), key=lambda f: f.generado)
            est, ruedas, cont = conversion(rol, fotos, self.cinta, self.perfil, self.ses, self.contrato, self.muestras_ind[rol], corte,
                                           sin_ind_desde)
            gen = np.array([np.datetime64(e.f.generado) for e in est], dtype="datetime64[s]")
            self._conv[clave] = (est, gen, ruedas, cont)
        return self._conv[clave]

    def ventana_muestras(self, rol, corte, foto_ts, lim_extra=None):
        """(estado, [estados]) de las muestras que entran en la mediana de la foto 'foto_ts' con el conocimiento 'corte': de dia las ultimas
        24 vivas en hora de la rueda en curso; de noche toda la rueda de referencia; ETF las ultimas 24 validas desde el ultimo reinicio."""
        est, gen, ruedas, cont = self.lista_cboe(rol, corte, None, lim_extra)
        k = next((i for i in range(len(est) - 1, -1, -1) if est[i].f.ts == foto_ts), None)
        if k is None:
            return None, []
        e = est[k]; c = self.c
        if rol == "ind":
            if e.modo == "dia":
                dia, n = a_ny(e.t_spot).strftime("%Y-%m-%d"), c["base_ultimas"]
            elif e.noche_ref:
                dia, n = e.noche_ref[0], None
            else:
                return e, []
            xs = []
            for x in est[:k + 1]:
                ny = a_ny(x.t_spot)
                en_hora = (c["base_hora"][0] <= hm(ny) <= c["base_hora"][1]) and ny.weekday() < 5
                if ny.strftime("%Y-%m-%d") == dia and x.vivo and x.muestra == x.muestra and en_hora:
                    xs.append(x)
            return e, (xs[-n:] if n else xs)
        r0 = ultimo_reinicio_etf(e.t_spot, c["etf_reinicio"]) if c["etf_reinicio"] else None
        xs = [x for x in est[:k + 1] if x.muestra == x.muestra and
              (not c["etf_reinicio"] or ultimo_reinicio_etf(x.t_spot, c["etf_reinicio"]) == r0)]
        return e, xs[-c["razon_ultimas"]:]

    # -------------------------------------------------------------- insumos
    def fut_de(self, t, vivo=True, arranque=None):
        """el cierre de la ultima vela m2 cerrada al calcular el minuto t: en vivo, con los ticks hasta t + 62 s; rellenado por un arranque
        posterior, con los ticks hasta ese arranque (None: toda la cinta)."""
        ts = ms_de(t)
        if vivo:
            return self.cinta.cierre(ts, self.ses, limite=ts + LIMITE_VIVO_MS)
        return self.cinta.cierre(ts, self.ses, limite=ms_de(arranque) if arranque is not None else None)

    def corr(self, hasta_t):
        """backtest_familia.minutos_nq: corrimiento de CONTRATO = mediana acumulada de (vela - futuro de la foto) si |med| > umbral."""
        if self._corr is None:
            hist = []
            for f in self.fotos_fut:
                v, b, _ = self.cinta.cierre(ms_de(f.ts + timedelta(seconds=120)), self.ses)
                if v == v and b is not None and (ms_de(f.ts) - (b + 120000)) <= 240000:
                    hist.append((f.ts, v - f.futuro))
            self._corr = hist
        h = [x for ts, x in self._corr if ts <= hasta_t]
        med = float(np.median(h)) if len(h) >= self.c["corr_min"] else 0.0
        return (med if abs(med) > self.c["corr_umbral"] else 0.0), med, len(h)

    def foto_fut(self, t):
        k = int(np.searchsorted(self.ts_fut, np.datetime64(t), side="right")) - 1
        if k < 0:
            return None, None
        f = self.fotos_fut[k]
        if (t - f.ts).total_seconds() >= self.c["vida_viva"]:
            return None, k
        return f, k

    def estado_cboe(self, rol, t, corte="auto", sin_ind_desde=None, lim_extra=None):
        if corte == "auto":
            corte = self.conoc.corte(t)[0]
        est, gen, ruedas, cont = self.lista_cboe(rol, corte, sin_ind_desde, lim_extra)
        k = int(np.searchsorted(gen, np.datetime64(t), side="right")) - 1
        if k < 0:
            return None, None, corte
        e = est[k]
        if t >= vigencia_cboe(e, self.c, self.ses):
            return None, e, corte
        return e, e, corte

    def oi_ok(self, t):
        if oi_vivo_suprime(self, t):
            return False
        return not (self.oi_hasta is not None and self.ses.ini <= t < self.oi_hasta)

    # -------------------------------------------------------------- un libro en el minuto t
    def libro(self, rol, t, fut=None, est=None, conv=None, corte="auto", sin_ind_desde=None, venc_spec=False, lim_extra=None):
        """el libro 'rol' en el minuto t. Sin argumentos, todo recalculado; con fut/est/conv se fuerzan esos insumos (diagnostico)."""
        if fut is None:
            R, vivo = self.conoc.corte(t)
            fut = self.fut_de(t, vivo, R)[0]
        if not (fut == fut):
            return None, "sin precio del futuro (ninguna vela m2 cerrada en la cinta en las 4 h previas: velas.cierre_conocido)"
        c = self.c
        if rol == "fut":
            f, k = (est, None) if est is not None else self.foto_fut(t)
            if f is None:
                return None, ("sin foto vigente del libro (la ultima tiene mas de %d s)" % c["vida_viva"]) if k is not None else "sin fotos del libro"
            if conv is None:
                conv = self.corr(t)[0]
            fl = filas_hoy(f.cadena(), t, c["piso_dias"])
            b = armar_libro(rol, self.perfil, fl, fut - conv, fut, conv)
            if b is None:
                return None, "perfil vacio"
            b.foto = f; b.est = None; b.foto_id = f.ts.strftime("%H:%M:%S"); b.dato = f.ts; b.congelada = False; b.origen = "viva3"
            b.oi_ok = self.oi_ok(t); b.oi_fresco = True
            b.hasta = f.ts + timedelta(seconds=c["vida_viva"])
            b.conv_txt = "corrimiento %.2f" % conv
            b.env_b = None
            b.zv, b.zo = zero_de(b, self.perfil)
            return b, ""
        if est is None:
            e, ult, corte = self.estado_cboe(rol, t, corte, sin_ind_desde, lim_extra)
            if e is None:
                if ult is None:
                    return None, "sin cadena de CBOE"
                return None, "cadena de CBOE vencida (C9: la ultima es de %s, vigente hasta %s)" % (
                    ult.f.generado.strftime("%m-%d %H:%M"), vigencia_cboe(ult, c, self.ses).strftime("%m-%d %H:%M"))
        else:
            e = est
        f = e.f
        if conv is None:
            if rol == "ind":
                conv, txt = base_ind(e, t, self.perfil, self.ses, self.contrato, fut)
                if not (conv == conv):
                    return None, "sin base (C7): " + txt
            else:
                conv = e.razon; txt = "razon: mediana robusta de %d muestras" % (e.razon_n or 0)
                if not (conv == conv) or e.razon_contrato != self.contrato:
                    return None, "sin razon (C8: menos de %d muestras vivas)" % c["razon_n"]
        else:
            txt = "la del indicador"
        S = (fut - conv) if rol == "ind" else (fut / conv)
        fl = filas_hoy(cadena_cboe(f), t, c["piso_dias"], venc_reales=(c["venc_reales"] and not venc_spec))
        b = armar_libro(rol, self.perfil, fl, S, fut, conv)
        if b is None:
            return None, "perfil vacio"
        b.foto = f; b.est = e; b.foto_id = f.ts[5:]; b.dato = f.t_dato; b.congelada = e.congelada; b.origen = f.origen
        b.oi_ok = True; b.oi_fresco = not e.oi_viejo
        b.hasta = vigencia_cboe(e, c, self.ses)
        b.conv_txt = txt
        b.env_b = int((t - f.generado).total_seconds() // 900)
        b.zv, b.zo = zero_de(b, self.perfil)
        return b, ""

    def candidatos_cache(self, rol, t, b, corte):
        """C5 CON la cache de la especificacion (cache_z por (foto, [bloque de 15 min,] round(S / centro))): el zero de los minutos
        anteriores de la MISMA foto con la misma clave (en una tanda, gana el primero)."""
        centro = self.c["zest"][rol][2]
        clave = (b.env_b, float(round(b.S / centro)))
        t0 = b.foto.ts if rol == "fut" else b.foto.generado
        m0 = datetime(t0.year, t0.month, t0.day, t0.hour, t0.minute) + (timedelta(minutes=1) if (t0.second or t0.microsecond) else timedelta(0))
        out = []
        tt = m0
        niv = getattr(self, "niv", None) or {}
        lid = self.perfil[rol]["id"]
        while tt < t:
            r = niv.get(clave_de(tt))
            m = meta_de(r, lid) if r else None
            fi = _num(r.get("f")) if r else None
            ci = _num(m.get("c")) if m else None
            bb, _ = self.libro(rol, tt, fut=fi, est=(b.foto if rol == "fut" else b.est), conv=ci, corte=corte)
            if bb is not None and (bb.env_b, float(round(bb.S / centro))) == clave:
                out.append((tt, bb.zv, bb.zo))
            tt += timedelta(minutes=1)
        return out


# ====================================================================================================== las series de un minuto
def nombre_serie(tipo, b_id, fuente):
    return "%s_%s_%s" % (tipo, b_id, fuente)


def series_minuto(ctx, libros):
    """{serie: [(etq, precio, monto, strike)]} = backtest_familia.conjuntos_minuto + preview_niveles._monto (monto = valor x mult/1e6, con
    mult = MULT[libro]/100; la familia, 1) y _actual (strike = round(round(precio, 2) - conv, 2), o / razon: strike_mostrado)."""
    out = _series_minuto(ctx, libros)
    for s, lv in out.items():
        rol = _rol_de_serie(ctx, s)
        if rol is None:
            continue
        b = libros[rol]
        out[s] = [(e, p, m, strike_mostrado(rol, p, b.conv) if k is not None else None) for e, p, m, k in lv]
    return out


def _series_minuto(ctx, libros):
    c = ctx.c
    out = {}
    pools = {"vol": {}, "oi": {}}
    for rol, b in libros.items():
        mult = c["mult"][rol] / 100.0
        for fu in ("vol", "oi"):
            oi = fu == "oi"
            if oi and not b.oi_ok:
                continue
            lv = []
            for etq, i, lado in r_muros(b, oi, c):
                v = ((b.goC if oi else b.gvC) if lado == "C" else (b.goP if oi else b.gvP))[i]
                lv.append((etq, float(b.Fut[i]), float(v) * mult / 1e6, float(b.K[i])))
            if lv:
                out[nombre_serie("MUROS", b.id, fu)] = lv
            g = b.go if oi else b.gv
            maj = [(etq, float(b.Fut[i]), float(g[i]) * mult / 1e6, float(b.K[i])) for etq, i, _ in r_majors(b, oi, c)]
            if maj:
                out[nombre_serie("MAJORS", b.id, fu)] = maj
            zt = r_ztp(b, oi, c)
            if zt:
                out[nombre_serie("ZTP", b.id, fu)] = [(etq, p, None, b.strike_de(p)) for etq, p in zt]
            ze = r_zest(b, oi, c)
            if ze:
                out[nombre_serie("ZEST", b.id, fu)] = [(etq, p, None, b.strike_de(p)) for etq, p in ze]
            if rol == "fut":
                tn = [(etq, float(b.Fut[i]), float(g[i]) * mult / 1e6, float(b.K[i])) for etq, i, _ in r_topn(b, oi, c)]
                if tn:
                    out[nombre_serie("TOPN", b.id, fu)] = tn
            pools[fu][b.id] = [p for _, p, _, _ in lv] + [p for _, p, _, _ in maj] + [p for _, p in zt]
    if libros:
        fut = next(iter(libros.values())).fut
        v = confluencia(pools["vol"], fut, c)
        if v:
            out["CONF_vol"] = [(etq, p, None, None) for etq, p in v]
        if all(r in libros for r in ROLES):
            F_ = Fus([libros[r] for r in ROLES], fut, c)
            for fu, oi in (("vol", False), ("oi", True)):
                if oi and not all(libros[r].oi_ok for r in ROLES):
                    continue
                lv = []
                for etq, i, lado in r_muros(F_, oi, c):
                    v2 = ((F_.goC if oi else F_.gvC) if lado == "C" else (F_.goP if oi else F_.gvP))[i]
                    lv.append((etq, float(F_.Fut[i]), float(v2) / 1e6, None))
                if lv:
                    out["FAM_MUROS_" + fu] = lv
    return out


# ====================================================================================================== el niv del indicador
def leer_niv(carpeta, dia, instrumento):
    """familia\\niv-<sesion>-<contrato>.jsonl (AlmacenNiveles): cabecera {"v","sesion","contrato","modo"} y una linea por minuto
    calculado con libros; si una clave se repite gana la ULTIMA (los minutos rehechos se agregan al final)."""
    p = os.path.join(carpeta, "familia", "niv-%s-%s.jsonl" % (dia, instrumento))
    if not os.path.exists(p):
        raise FaltanDatos("no existe %s" % p)
    cab = None; reg = {}
    with open(p, encoding="utf-8", errors="replace") as fh:
        for l in fh:
            l = l.strip()
            if not l:
                continue
            try:
                j = json.loads(l)
            except ValueError:
                continue
            if "v" in j and "k" not in j:
                cab = j; continue
            if "k" in j:
                reg[int(j["k"])] = j
    return cab, reg, p


def ultimo_niv(carpeta):
    xs = []
    for p in glob.glob(os.path.join(carpeta, "familia", "niv-????-??-??-*.jsonl")):
        m = re.match(r"niv-(\d{4}-\d{2}-\d{2})-([A-Za-z0-9]+)\.jsonl$", os.path.basename(p))
        if m:
            xs.append((os.path.getmtime(p), m.group(1), m.group(2)))
    if not xs:
        raise FaltanDatos("no hay niv-*.jsonl en %s" % os.path.join(carpeta, "familia"))
    xs.sort()
    return xs[-1][1], xs[-1][2]


# ====================================================================================================== la comparacion
SERIES_INSUMO = ("INSUMO", "LIBRO", "CONV", "FOTO", "OI", "CONGELADA")


class Fila:
    __slots__ = ("serie", "k", "etq", "ind", "rec", "dif", "ok", "motivo", "clase", "campo")

    def __init__(self, serie, k, etq, campo, ind, rec, dif, ok, motivo="", clase=""):
        self.serie = serie; self.k = k; self.etq = etq; self.campo = campo; self.ind = ind; self.rec = rec; self.dif = dif
        self.ok = ok; self.motivo = motivo; self.clase = clase


def _num(x):
    return None if x is None else float(x)


def _txt_nivel(p, m, s):
    return "%s K %s M %s" % (f2(p), f2(s), f2(m, 2) if m is not None else "-")


def strike_mostrado(rol, precio, conv):
    """preview_niveles: el nivel se guarda con el precio redondeado (round(p, 2)) y _actual calcula el strike de su libro desde ese precio:
    round(p - cv, 2) (futuro e indice) o round(p / cv, 2) (ETF). Por eso el strike del niv puede diferir 0,01 del strike real."""
    if conv is None or precio is None:
        return None
    p = round(precio, 2)
    return round(p / conv, 2) if rol == "etf" else round(p - conv, 2)


def comparar_niveles(serie, lv_ind, lv_rec):
    """compara las listas de una serie por etiqueta. Devuelve [Fila]: una 'ok' por nivel igual, o una por campo que difiere. El strike del
    recalculo ya viene como lo muestra la especificacion (strike_mostrado); el real va en el texto."""
    filas = []
    di = {}
    for a in lv_ind or []:
        di[a[1]] = (_num(a[0]), _num(a[2]) if len(a) > 2 else None, _num(a[3]) if len(a) > 3 else None)
    dr = {e: (p, m, s) for e, p, m, s in (lv_rec or [])}
    for e in sorted(set(di) | set(dr), key=lambda e: (len(e), e)):
        if e not in di:
            filas.append(Fila(serie, None, e, "nivel", "-", _txt_nivel(*dr[e]), None, False)); continue
        if e not in dr:
            filas.append(Fila(serie, None, e, "nivel", _txt_nivel(*di[e]), "-", None, False)); continue
        pi, mi, si = di[e]; pr, mr, sr = dr[e]
        es_zero = serie.startswith(("ZEST", "ZTP", "CONF"))
        ok_s = (si is None and sr is None) or (si is not None and sr is not None and abs(si - sr) <= (TOL_PRECIO if es_zero else TOL_STRIKE) + 1e-9)
        ok_p = pi is not None and pr is not None and abs(pi - pr) <= TOL_PRECIO + 1e-9
        ok_m = (mi is None and mr is None) or (mi is not None and mr is not None and abs(mi - mr) <= max(TOL_MONTO_REL * abs(mr), TOL_MONTO_ABS) + 1e-9)
        if ok_s and ok_p and ok_m:
            filas.append(Fila(serie, None, e, "ok", _txt_nivel(pi, mi, si), _txt_nivel(pr, mr, sr), (pr - pi), True)); continue
        if not ok_s:
            filas.append(Fila(serie, None, e, "strike", f2(si), f2(sr), (sr - si) if (si is not None and sr is not None) else None, False))
        if not ok_p:
            filas.append(Fila(serie, None, e, "precio", f2(pi), f2(pr), (pr - pi) if (pi is not None and pr is not None) else None, False))
        if not ok_m:
            filas.append(Fila(serie, None, e, "monto", f2(mi, 1), f2(mr, 3), (mr - mi) if (mi is not None and mr is not None) else None, False))
    return filas


def meta_de(reg, lid):
    for m in reg.get("m") or []:
        if m.get("l") == lid:
            return m
    return None


def estados_del_indicador(ctx, rol, t, meta, corte, maximo=6):
    """las fotos que pudo haber usado el indicador segun su meta 'd' (libro del futuro: ts de la foto, unica; CBOE: t_dato, que pueden compartir
    varias bajadas -la cadena congelada de noche, o la propia y la de cboe-local-), de la mas nueva a la mas vieja con generado <= t."""
    try:
        d = utc_iso(meta["d"])
    except (KeyError, ValueError, TypeError):
        return []
    if rol == "fut":
        return [f for f in ctx.fotos_fut if abs((f.ts - d).total_seconds()) < 0.5][:1]
    out = []; vistos = set()
    for cc in ((corte,) + ((None,) if corte is not None else ())):
        est, gen, _, _ = ctx.lista_cboe(rol, cc)
        for e in reversed(est):
            if e.f.generado <= t and abs((e.f.t_dato - d).total_seconds()) < 0.5 and e.f.ts not in vistos:
                out.append(e); vistos.add(e.f.ts)
                if len(out) >= maximo:
                    return out
    return out


def contraste_especificacion(ctx, rol, t, k, b, ci, corte):
    """La conversion de CBOE contra lo que el indicador DEBIA saber. Dos vara (la especificacion no dice como se juntan las fotos en vivo):
      DISENO (default): la carpeta propia mas la extra (cboe-local) LLENANDO EL HUECO de ATAS apagado: toda foto de la extra generada antes de
        la primera bajada propia del arranque (para eso existe la extra: BajadorCboe.OpcionesCboe.CarpetaExtra, 4.1.9). Lo que falte aca
        es un DEFECTO del indicador (p. ej. la carrera al arrancar: la foto se escribio mientras leia).
      ESTRICTA (--espec-estricta): TODAS las fotos con generado <= t de las dos carpetas (backtest_familia: 'la foto con hora de
        disponibilidad <= ese minuto'; la vista previa en vivo lee el ultima-*.json de cboe-local cada 15 s). Aca tambien aparece lo que la
        Familia no lee POR DISENO (cboe-local en vivo: baja el mismo sello antes que la carpeta propia).
    Si con esa vara la base/razon da otra cosa (mas alla de la tolerancia) y con lo que sabia el indicador coincide: fila 'CONV <libro>
    espec.' con las muestras que entran en una lista y no en la otra, y por que."""
    lid = ctx.perfil[rol]["id"]
    tol = (TOL_CONV_PRECIO * abs(ci) / b.fut if b.fut > 0 else 0.0) if rol == "etf" else TOL_CONV_BASE
    if abs(b.conv - ci) > tol + 1e-9:
        return []                     # ya difiere con el modelo: lo explica la fila CONV
    lim_ind = ctx.conoc.extra_hasta.get((corte, ctx.perfil[rol]["log"]), corte)
    estricta = bool(ctx.opciones.get("espec_estricta"))
    if estricta:
        lim_v, corte_v, vara = None, None, "ESTRICTA (todas las fotos con generado <= t)"
    else:
        propias = [f.generado for f in ctx.cboe[rol].get("propia", {}).values() if f.generado >= corte]
        lim_v = min(propias) if propias else t
        corte_v = corte
        vara = "de DISENO (la extra llena el hueco de ATAS apagado hasta la primera bajada propia, %s UTC)" % lim_v.strftime("%H:%M:%S")
        if lim_v <= lim_ind:
            return []
    bs, porque = ctx.libro(rol, t, fut=b.fut, corte=corte_v, lim_extra=lim_v)
    if bs is None:
        return [Fila("CONV %s espec." % lid, k, "base" if rol == "ind" else "razon", "conversion", f2(ci, 4 if rol == "ind" else 6), "-", None,
                     False, "con la vara %s no hay libro: %s" % (vara, porque), "insumo")]
    if abs(bs.conv - ci) <= tol + 1e-9:
        return []
    e_m, w_m = ctx.ventana_muestras(rol, corte, b.est.f.ts)
    e_s, w_s = ctx.ventana_muestras(rol, corte_v, bs.est.f.ts, lim_v)
    ts_m = {x.f.ts for x in w_m}; ts_s = {x.f.ts for x in w_s}

    def desc(x, de_vara):
        txt = "ts %s (%s, generada %s UTC, spot %s, precio del futuro %s por %s, muestra %s)" % (
            x.f.ts[11:], "cboe-local" if x.f.origen == "extra" else "carpeta propia", x.f.generado.strftime("%H:%M:%S"), f2(x.f.spot),
            f2(x.precio), x.fuente_precio, f2(x.muestra, 4))
        if de_vara and x.f.origen == "extra" and x.f.generado > lim_ind:
            txt += (": el indicador NO la tenia: la carpeta extra se lee solo al arrancar y el arranque de las %s leyo hasta la foto "
                    "generada a las %s (log de CBOE); esta se escribio despues" % (corte.strftime("%H:%M:%S"), lim_ind.strftime("%H:%M:%S")))
        return txt
    solo_s = [desc(x, True) for x in w_s if x.f.ts not in ts_m]
    solo_m = [desc(x, False) for x in w_m if x.f.ts not in ts_s]
    if solo_s or solo_m:
        mot = "vara %s: la mediana tiene %d muestra(s) que el indicador no: %s%s" % (
            vara, len(solo_s), "; ".join(solo_s) or "-", ("; y el indicador tiene %d que esta no (sale de la ventana de 24): %s" % (
                len(solo_m), "; ".join(solo_m))) if solo_m else "")
    else:
        mot = ("vara %s: mismo conjunto de muestras; cambia el orden o 'vivo' por la hora de las bajadas (cboe-local baja el mismo sello "
               "antes o despues que la carpeta propia)" % vara)
    efecto = abs(bs.conv - ci) * (b.fut / ci if rol == "etf" else 1.0)
    mot += ". Efecto: todas las rayas de %s quedan corridas %.3f pts en el futuro" % (lid, efecto)
    return [Fila("CONV %s espec." % lid, k, "base" if rol == "ind" else "razon", "conversion", f2(ci, 4 if rol == "ind" else 6),
                 f2(bs.conv, 4 if rol == "ind" else 6), bs.conv - ci, False, mot, "insumo")]


def auditar_ausente(ctx, k):
    """un minuto que el niv NO tiene (preview: 'if not bk: continue', el minuto sin libros no se anota): coincide si el recalculo tampoco
    tiene ningun libro; si el recalculo tiene alguno, es una diferencia (el indicador no dibujo nada y debia)."""
    t = de_clave(k)
    corte, en_vivo = ctx.conoc.corte(t)
    fut, bvela, entera = ctx.fut_de(t, en_vivo, corte)
    ins = dict(t=t, fut=fut, fut_archivo=fut, f_ind=None, libros_ind=[], libros={}, corte=corte, en_vivo=en_vivo, ausente=True)
    tiene = []
    for rol in ROLES:
        b, porque = ctx.libro(rol, t, corte=corte)
        ins["libros"][rol] = (b, porque)
        if b is not None:
            tiene.append(ctx.perfil[rol]["id"])
    if not tiene:
        motivo = "ninguno de los dos tiene libros (%s)" % "; ".join("%s: %s" % (ctx.perfil[r]["id"], ins["libros"][r][1]) for r in ROLES)
        return [Fila("MINUTO sin niv", k, "-", "libros", "no anotado", "sin libros", None, True, motivo, "")], ins
    if corte is None:
        mot, cl = "ningun arranque del indicador cubre este minuto (no lo calculo nunca)", "insumo"
    elif nan(fut):
        mot, cl = "sin precio del futuro en la cinta", "insumo"
    else:
        mot, cl = ("el indicador no anoto el minuto (%s) y el recalculo tiene %s: el indicador no dibujo nada y debia" % (
            "en vivo, arranque %s" % corte.strftime("%m-%d %H:%M:%S") if en_vivo else "rellenado por el arranque %s" % corte.strftime("%m-%d %H:%M:%S"),
            ", ".join(tiene)), "sin explicar")
    return [Fila("MINUTO sin niv", k, "-", "libros", "no anotado", ",".join(tiene), None, False, mot, cl)], ins


def auditar_minuto(ctx, k, reg, filtro_series=None, diagnostico=True):
    """recalcula el minuto k y lo compara con el registro del indicador. Devuelve (filas, insumos)."""
    t = de_clave(k)
    perfil = ctx.perfil
    corte, en_vivo = ctx.conoc.corte(t)
    fut, bvela, entera = ctx.fut_de(t, en_vivo, corte)
    fut_a, bvela_a, entera_a = ctx.fut_de(t, not en_vivo, corte)      # la otra regla (para el motivo)
    ins = dict(t=t, fut=fut, fut_archivo=fut_a, f_ind=_num(reg.get("f")), libros_ind=list(reg.get("b") or []), libros={}, corte=corte, en_vivo=en_vivo)
    filas = []
    fi = ins["f_ind"]
    # 1) el precio del futuro (cierre de la ultima vela m2 cerrada)
    if fi is None or nan(fut) or abs(fi - fut) > 1e-9:
        if fi is not None and not nan(fut_a) and abs(fi - fut_a) <= 1e-9:
            mot = ("el indicador uso la vela de las %s: %s" % (de_ms(bvela_a).strftime("%H:%M"),
                   "cerrada con ticks posteriores a T+60 s (el minuto se relleno en un arranque, no en vivo)" if en_vivo else
                   "la regla en vivo (ticks hasta T+60 s): el minuto se calculo en vivo y no en el arranque estimado"))
        elif nan(fut) and nan(fut_a):
            mot = ("la cinta archivada no tiene velas de ese tramo (el indicador no estaba escuchando: lo relleno al arrancar con las velas del "
                   "grafico, que no se guardan): el precio del futuro NO es verificable")
        elif bvela is not None and not entera:
            mot = "la vela %s no se escucho entera (reinicio de ATAS): el indicador uso la del grafico, que no se guarda" % de_ms(bvela).strftime("%H:%M")
        else:
            mot = "vela m2 %s en vivo (ticks hasta T+60 s); con la cinta entera %s" % (de_ms(bvela).strftime("%H:%M") if bvela is not None else "-", f2(fut_a))
        filas.append(Fila("INSUMO fut", k, "-", "precio del futuro", f2(fi), f2(fut), (fut - fi) if (fi is not None and not nan(fut)) else None,
                          False, mot, "insumo"))
    else:
        filas.append(Fila("INSUMO fut", k, "-", "precio del futuro", f2(fi), f2(fut), 0.0, True))
    libros = {}
    for rol in ROLES:
        b, porque = ctx.libro(rol, t, corte=corte)
        if b is not None:
            libros[rol] = b
        ins["libros"][rol] = (b, porque)
    # 2) libros presentes, foto y conversion
    for rol in ROLES:
        lid = perfil[rol]["id"]
        en_ind = lid in ins["libros_ind"]; en_rec = rol in libros
        m = meta_de(reg, lid)
        b = libros.get(rol)
        if en_ind != en_rec:
            filas.append(Fila("LIBRO " + lid, k, "-", "presente", "si" if en_ind else "no", "si" if en_rec else "no (%s)" % ins["libros"][rol][1],
                              None, False, "", ""))
            continue
        if not en_rec:
            continue
        ci = _num(m.get("c")) if m else None
        if rol == "etf":
            tol = TOL_CONV_PRECIO * abs(ci) / b.fut if (ci is not None and b.fut > 0) else 0.0
            okc = ci is not None and abs(b.conv - ci) <= tol + 1e-9
            fila = Fila("CONV " + lid, k, "razon", "conversion", f2(ci, 6), f2(b.conv, 6), (b.conv - ci) if ci is not None else None, okc)
            chica = ci is not None and abs(b.conv - ci) > DIF_MINIMA * 1e-3 * abs(ci)
        else:
            okc = ci is not None and abs(b.conv - ci) <= TOL_CONV_BASE + 1e-9
            fila = Fila("CONV " + lid, k, "base" if rol == "ind" else "corr", "conversion", f2(ci, 4), f2(b.conv, 4),
                        (b.conv - ci) if ci is not None else None, okc)
            chica = ci is not None and abs(b.conv - ci) > DIF_MINIMA
        if okc and chica:
            fila.clase = "tol"
            fila.motivo = "dentro de tolerancia (efecto en el precio %.4f pt <= 0,01)" % (
                abs(b.conv - ci) * (b.fut / ci if rol == "etf" else 1.0))
            e = b.est
            if rol == "ind" and e is not None and e.modo == "noche" and e.noche_ref and e.noche_ref[1]:
                fac = b.conv / e.noche_ref[1]
                fila.motivo += ("; de noche base = mediana de la rueda x carry: la del indicador implica una mediana de rueda %.4f contra %.4f "
                                "recalculada (rueda %s, %d muestras): otro conjunto de muestras en ese arranque (probable: el precio de "
                                "una muestra rearmado distinto del guardado en muestras-*.jsonl, no verificable), no la formula del carry" % (ci / fac, e.noche_ref[1], e.noche_ref[0],
                                                                                                         e.noche_ref[3]))
        e = b.est
        if e is not None and getattr(e, "fuentes", None):
            nv = e.fuentes.get("indicador", 0); tot = sum(e.fuentes.values())
            if nv:
                if fila.ok and fila.clase != "tol":
                    fila.clase = "nv"
                fila.motivo = ((fila.motivo + "; ") if fila.motivo else "") + (
                    "%d de las %d muestras de %s usan el precio del futuro GUARDADO por el indicador (vela del grafico, no esta en la cinta): "
                    "la cuenta coincide pero ese insumo NO es verificable" % (nv, tot, "la mediana" if (rol == "etf" or e.modo == "dia")
                                                                             else "la rueda %s" % (e.noche_ref[0] if e.noche_ref else "?")))
        filas.append(fila)
        if rol != "fut" and corte is not None and ci is not None:
            filas.extend(contraste_especificacion(ctx, rol, t, k, b, ci, corte))
        for av in (b.avisos or []):
            filas.append(Fila("AVISO " + lid, k, "-", "dato", "-", "-", None, True, av, "aviso"))
        if m:
            try:
                di = utc_iso(m["d"])
            except (KeyError, ValueError):
                di = None
            okd = di is not None and abs((b.dato - di).total_seconds()) <= 0.5
            filas.append(Fila("FOTO " + lid, k, "-", "dato", di.strftime("%m-%d %H:%M:%S") if di else "-", b.dato.strftime("%m-%d %H:%M:%S"), None,
                              okd, "" if okd else "otra foto", "" if okd else "insumo"))
            if "o" in m:
                oi_i = bool(m.get("o")); oi_r = b.oi_ok if rol == "fut" else b.oi_fresco
                filas.append(Fila("OI " + lid, k, "-", "oi_ok" if rol == "fut" else "oi_fresco", str(int(oi_i)), str(int(oi_r)), None, oi_i == oi_r,
                                  ctx.oi_como if (rol == "fut" and oi_i != oi_r) else "", "" if oi_i == oi_r else "insumo"))
            if "z" in m:
                okz = bool(m.get("z")) == bool(b.congelada)
                filas.append(Fila("CONGELADA " + lid, k, "-", "congelada", str(m.get("z")), str(int(b.congelada)), None, okz, "", "" if okz else "insumo"))
    # 3) las series
    rec = series_minuto(ctx, libros)
    ind = reg.get("s") or {}
    cat = catalogo(perfil)
    nombres = [n for n in cat if (n in ind or n in rec) and (not filtro_series or n in filtro_series)]
    for s in nombres:
        for f in comparar_niveles(s, ind.get(s), rec.get(s)):
            f.k = k; filas.append(f)
    # 4) motivo de cada diferencia
    malas = [f for f in filas if not f.ok and not f.serie.endswith("espec.") and (not f.clase or f.serie.startswith(("CONV", "LIBRO")))]
    if malas and diagnostico:
        clasificar(ctx, k, t, reg, libros, ins, malas, ind, corte)
    for f in filas:
        if not f.ok and not f.clase:
            f.clase = "sin explicar"
    return filas, ins


def _rol_de_serie(ctx, serie):
    for rol in ROLES:
        lid = ctx.perfil[rol]["id"]
        if "_%s_" % lid in serie or serie.endswith(" " + lid):
            return rol
    return None


def clasificar(ctx, k, t, reg, libros, ins, malas, ind, corte):
    """Para cada serie que difiere: (a) se rehace con los insumos del indicador (su fut, su foto, su conversion, su OI); si coincide, el
    MOTIVO es el insumo que difiere; (b) ZEST: se prueba la cache de C5 de la especificacion; (c) si con los mismos insumos sigue distinto,
    'formula'. Para la conversion (CONV) se busca la causa en las muestras (las que la 4.1 tenia y no se pueden verificar)."""
    perfil = ctx.perfil
    fi = ins["f_ind"]
    candidatos = {}
    for rol in ROLES:
        lid = perfil[rol]["id"]
        m = meta_de(reg, lid)
        if m is None:
            continue
        lst = []
        alternativas = []
        for e in estados_del_indicador(ctx, rol, t, m, corte):
            b, _ = ctx.libro(rol, t, fut=fi, est=e, conv=_num(m.get("c")), corte=corte)
            if b is not None:
                if rol == "fut":
                    b.oi_ok = bool(m.get("o", 1))
                lst.append(b)
            if rol != "fut" and ctx.c.get("venc_reales"):
                # C10 ES: la misma foto con la regla de la especificacion sola ('dias' envejecidos, sin la fecha real del vencimiento)
                bs, _ = ctx.libro(rol, t, fut=fi, est=e, conv=_num(m.get("c")), corte=corte, venc_spec=True)
                if bs is not None and b is not None and bs.fl["n"] != b.fl["n"]:
                    bs.origen = "regla dias"
                    alternativas.append(bs)
        lst += alternativas
        if lst:
            candidatos[rol] = lst
    forzados = {r: v[0] for r, v in candidatos.items()}
    rec_f = series_minuto(ctx, forzados) if forzados else {}
    por_serie = {}
    for f in malas:
        por_serie.setdefault(f.serie, []).append(f)
    for s, fs in por_serie.items():
        rol = _rol_de_serie(ctx, s)
        dif_ins = []
        if fi is not None and not nan(ins["fut"]) and abs(fi - ins["fut"]) > 1e-9:
            dif_ins.append("precio del futuro %s (indicador) contra %s (cinta)" % (f2(fi), f2(ins["fut"])))
        elif fi is not None and nan(ins["fut"]):
            dif_ins.append("precio del futuro %s (indicador): la cinta no tiene esa vela" % f2(fi))
        for r in ([rol] if rol else list(ROLES)):
            b = libros.get(r); bf = forzados.get(r); lid = perfil[r]["id"]
            if b is None or bf is None:
                if (b is None) != (bf is None):
                    dif_ins.append("libro %s: %s" % (lid, ("el indicador lo tenia y el recalculo no (%s)" % ins["libros"][r][1]) if b is None
                                                     else "el recalculo lo tiene y el indicador no"))
                continue
            if b.foto is not bf.foto:
                dif_ins.append("foto %s distinta (indicador %s, recalculo %s)" % (lid, bf.foto_id, b.foto_id))
            if abs(b.conv - bf.conv) > (1e-9 * abs(bf.conv) if r == "etf" else 1e-9):
                dif_ins.append("conversion %s %s (indicador) contra %s" % (lid, f2(bf.conv, 6), f2(b.conv, 6)))
            if r == "fut" and b.oi_ok != bf.oi_ok:
                dif_ins.append("OI de %s con fecha (C2): indicador %d, recalculo %d" % (lid, int(bf.oi_ok), int(b.oi_ok)))
        if s.startswith(("CONV", "LIBRO")):
            if s.startswith("CONV") and rol in libros:
                ok, mot = motivo_conversion(ctx, rol, t, reg, libros[rol], corte)
                for f in fs:
                    f.clase = "insumo" if ok else "sin explicar"; f.motivo = mot
            else:
                m_ = meta_de(reg, perfil[rol]["id"]) if rol else None
                fwd = bool(m_ and "FORWARDS" in str(m_.get("t") or "").upper())
                for f in fs:
                    f.clase = "decision C#" if fwd else "insumo"
                    f.motivo = ("el indicador uso la base de FORWARDS (P2_SPX.md: respaldo cuando no hay base sincronizada; NO esta en "
                                "backtest_familia ni en el DISENO_5_0 §4, donde sin base sincronizada el indice no se dibuja): '%s'" % (
                                    str(m_.get("t"))[:120]) if fwd else
                                "; ".join(dif_ins) or "el indicador %s el libro y el recalculo %s" % (f.ind, f.rec))
            continue
        con_insumos = comparar_niveles(s, ind.get(s), rec_f.get(s))
        otra = None
        if not all(x.ok for x in con_insumos) and rol in candidatos and len(candidatos[rol]) > 1:
            for bb in candidatos[rol][1:]:
                alt = comparar_niveles(s, ind.get(s), series_minuto(ctx, dict(forzados, **{rol: bb})).get(s))
                if all(x.ok for x in alt):
                    con_insumos = alt; otra = bb; break
        if all(x.ok for x in con_insumos):
            if otra is not None and otra.origen == "regla dias":
                dif_ins.append("el indicador CONTO la serie ya vencida (%d filas mas): uso los 'dias' envejecidos sin la fecha real del "
                               "vencimiento (16:00 NY); el DISENO_5_0 §4 pide excluirla" % (otra.fl["n"] - forzados[rol].fl["n"]))
            elif otra is not None:
                dif_ins.append("el indicador uso OTRA bajada con la misma hora de dato (%s, generado %s: envejece distinto)" % (
                    otra.foto_id, otra.foto.generado.strftime("%H:%M:%S")))
            for f in fs:
                f.clase = "insumo" if dif_ins else "insumo?"
                f.motivo = "con los insumos del indicador COINCIDE" + (": " + "; ".join(dif_ins) if dif_ins else " (sin un insumo distinto identificado)")
            continue
        if s.startswith("ZEST") and rol is not None:
            b = forzados.get(rol) or libros.get(rol)
            if b is not None:
                cands = ctx.candidatos_cache(rol, t, b, corte)
                oi = s.endswith("_oi")
                vi = None
                for a in ind.get(s) or []:
                    vi = _num(a[0])
                hit = next(((tt, (zo if oi else zv)) for tt, zv, zo in cands if vi is not None and not nan(zo if oi else zv)
                            and abs((zo if oi else zv) - vi) <= TOL_PRECIO + 1e-9), None)
                if hit:
                    for f in fs:
                        f.clase = "cache C5"
                        f.motivo = ("coincide con el zero del minuto %s (misma foto y clave de la cache de C5 de la especificacion)%s" % (
                            hit[0].strftime("%H:%M"), "; ademas: " + "; ".join(dif_ins) if dif_ins else ""))
                    continue
        if dif_ins:
            for f in fs:
                f.clase = "insumo+?"; f.motivo = "insumos distintos (%s) y con los del indicador TAMPOCO coincide" % "; ".join(dif_ins)
        else:
            for f in fs:
                f.clase = "formula"; f.motivo = "MISMOS insumos y distinto resultado: " + "; ".join(
                    "%s %s ind %s rec %s" % (x.etq, x.campo, x.ind, x.rec) for x in con_insumos if not x.ok)


def motivo_conversion(ctx, rol, t, reg, b, corte):
    """por que difiere la base (C7) o la razon (C8): cuenta las muestras de la lista usada por origen del precio y prueba con TODAS las fotos
    de la carpeta extra (otra regla de conocimiento). Devuelve (explicado, texto)."""
    lid = ctx.perfil[rol]["id"]
    m = meta_de(reg, lid)
    ci = _num(m.get("c")) if m else None
    if ci is None or rol == "fut":
        return False, "sin dato del indicador"
    tol = (TOL_CONV_PRECIO * abs(ci) / b.fut if b.fut > 0 else 0.0) if rol == "etf" else TOL_CONV_BASE
    e = b.est
    est, gen, ruedas, cont = ctx.lista_cboe(rol, corte)
    txt = ["muestras de la lista usada: %d por tick, %d por vela, %d con el precio del indicador (no verificables), %d sin precio, "
           "%d verificables distintas de las del indicador%s" % (cont["tick"], cont["vela"], cont["indicador"], cont["sin precio"], cont["dif"],
                                                               (" (p. ej. %s)" % cont["dif_ej"][:2]) if cont["dif_ej"] else "")]
    if m and m.get("t"):
        txt.append("indicador: '%s'" % m["t"][:110])
    if rol == "ind":
        if e.modo == "noche" and e.noche_ref:
            txt.append("recalculo: rueda %s con %d muestras (mediana %.4f)" % (e.noche_ref[0], e.noche_ref[3], e.noche_ref[1]))
        elif e.modo == "dia":
            txt.append("recalculo: modo dia con %d muestras" % e.nb)
    else:
        txt.append("recalculo: %d muestras" % (e.razon_n or 0))
    variantes = []
    if corte is not None:
        variantes.append((None, "con TODO lo archivado (fotos de la carpeta extra y precios guardados despues): el corte de conocimiento "
                                "estimado no es el que tuvo el indicador en ese minuto"))
        for R in ctx.conoc.arranques:
            if R != corte and abs((R - t).total_seconds()) < 36 * 3600:
                variantes.append((R, "con lo que conocia el arranque de las %s (no el de las %s)" % (R.strftime("%m-%d %H:%M:%S"), corte.strftime("%H:%M:%S"))))
    for cc, nombre in variantes:
        bb, _ = ctx.libro(rol, t, fut=b.fut, corte=cc)
        if bb is not None and abs(bb.conv - ci) <= tol + 1e-9:
            return True, "coincide %s: " % nombre + " | ".join(txt)
    # el hueco de ATAS anterior al arranque: al calcular ese minuto la 4.1 todavia no tenia la vela del grafico para esas muestras
    if corte is not None:
        fin = ctx.cinta.fin_tramo_antes(ms_de(corte))
        if fin is not None:
            desde = de_ms(fin)
            bb, _ = ctx.libro(rol, t, fut=b.fut, corte=corte, sin_ind_desde=desde)
            if bb is not None and abs(bb.conv - ci) <= tol + 1e-9:
                return True, ("coincide sin los precios de las muestras con ts - 900 s despues de las %s (fin de la cinta antes del arranque de "
                              "las %s): cuando se calculo este minuto el indicador todavia no tenia la vela del grafico para ellas (la guardo despues): "
                              % (desde.strftime("%H:%M:%S"), corte.strftime("%H:%M:%S"))) + " | ".join(txt)
    # la ventana de la mediana tiene muestras vivas sin precio verificable ni guardado (vela del grafico de ATAS, que no se guarda)
    est, gen, ruedas, cont = ctx.lista_cboe(rol, corte)
    try:
        i = next(k for k in range(len(est) - 1, -1, -1) if est[k] is e)
    except StopIteration:
        i = None
    if i is not None:
        nv = 0; validas = 0
        for k in range(i, -1, -1):
            x = est[k]
            if rol == "ind" and a_ny(x.t_spot).strftime("%Y-%m-%d") != a_ny(e.t_spot).strftime("%Y-%m-%d"):
                break
            if x.vivo and x.fuente_precio == "no verificable":
                nv += 1
            if x.muestra == x.muestra:
                validas += 1
                if validas >= (ctx.c["razon_ultimas"] if rol == "etf" else ctx.c["base_ultimas"]):
                    break
        if nv:
            return True, ("NO VERIFICABLE: en la ventana de la mediana hay %d muestra(s) viva(s) sin precio en la cinta guardada ni en las muestras "
                          "del indicador (vela del grafico de ATAS en un reinicio): el indicador pudo tenerla y el auditor no: " % nv) + " | ".join(txt)
    return False, " | ".join(txt)


# ====================================================================================================== el reporte
def reporte(ctx, resultados, detalle, salida, encabezado_extra=""):
    perfil = ctx.perfil
    L = []
    L.append("=" * 140)
    L.append("%s — %s" % (VERSION, perfil["nombre"]))
    L.append("datos: %s (solo lectura) | sesion %s [%s, %s) UTC %s | contrato %s | prioridad %s | carga %.1f s" % (
        ctx.carpeta, ctx.ses.dia, ctx.ses.ini.strftime("%m-%d %H:%M"), ctx.ses.fin.strftime("%m-%d %H:%M"),
        "(NY, corregida)" if ctx.corregida else "(paridad UTC)", ctx.instrumento, PRIORIDAD, ctx.seg_carga))
    L.append("cinta: %s (%d tramos escuchados) | %s: %d fotos viva3 | C2: %s" % (", ".join(ctx.cinta.archivos), len(ctx.cinta.tramos),
                                                                               perfil["fut"]["id"], len(ctx.fotos_fut), ctx.oi_como))
    for rol in ("ind", "etf"):
        d = ctx.cboe[rol]
        est, gen, ruedas, cont = ctx.lista_cboe(rol, None)
        L.append("CBOE %s: %s | muestras (con todo lo archivado): %d por tick, %d por vela, %d con el precio guardado por el indicador (velas del "
                 "grafico: NO VERIFICABLE), %d sin precio; verificables distintas de las guardadas por el indicador: %d%s" % (
                     perfil[rol]["id"], ", ".join("%s %d fotos" % (o, len(x)) for o, x in d.items()), cont["tick"], cont["vela"],
                     cont["indicador"], cont["sin precio"], cont["dif"], (" (p. ej. %s)" % cont["dif_ej"][:3]) if cont["dif_ej"] else ""))
    L.append("arranques del indicador (%s): %s" % (ctx.conoc.fuente, ", ".join(x.strftime("%m-%d %H:%M:%S") for x in ctx.conoc.arranques
                                                                           if x >= ctx.ses.ini - timedelta(hours=30))))
    est, gen, ruedas, cont = ctx.lista_cboe("ind", None)
    if ruedas:
        L.append("ruedas C7 de %s (con todas las fotos): %s" % (perfil["ind"]["id"], ", ".join("%s %d muestras, mediana robusta %.4f" % (
            dk, len(v["m"]), robusta(v["m"], ctx.c["base_piso"])[0]) for dk, v in sorted(ruedas.items())[-3:])))
    if encabezado_extra:
        L.append(encabezado_extra)
    L.append("-" * 140)
    todas = [f for fs, ins in resultados for f in fs]
    L.append("INSUMOS POR MINUTO (indicador / recalculado)")
    for fs, ins in resultados:
        t = ins["t"]
        corte = ins["corte"]
        if ins.get("ausente"):
            L.append("  %s UTC (%s NY) NO ANOTADO en el niv | fut recalculado %s | %s" % (t.strftime("%m-%d %H:%M"), a_ny(t).strftime("%H:%M"),
                     f2(ins["fut"]), " | ".join("%s %s" % (ctx.perfil[r]["id"], "si" if ins["libros"][r][0] is not None else
                                                             "no (%s)" % ins["libros"][r][1][:60]) for r in ROLES)))
            continue
        partes = ["%s UTC (%s NY) %s | fut %s/%s" % (t.strftime("%m-%d %H:%M"), a_ny(t).strftime("%H:%M"),
                                                    ("en vivo, arranque %s" if ins["en_vivo"] else "rellenado por el arranque %s") % (
                                                        corte.strftime("%m-%d %H:%M:%S") if corte else "?"),
                                                    f2(ins["f_ind"]), f2(ins["fut"]))]
        for rol in ROLES:
            b, porque = ins["libros"][rol]
            lid = perfil[rol]["id"]
            conv_i = next((f.ind for f in fs if f.serie == "CONV " + lid), None)
            if b is None:
                partes.append("%s %s/no (%s)" % (lid, "si" if lid in ins["libros_ind"] else "no", porque)); continue
            partes.append("%s %s %s/%s foto %s%s" % (lid, "razon" if rol == "etf" else ("base" if rol == "ind" else "corr"), conv_i if conv_i else "-",
                                                      f2(b.conv, 6 if rol == "etf" else 4), b.foto_id, "" if rol == "fut" else " (%s)" % b.origen))
        L.append("  " + " | ".join(partes))
    L.append("-" * 140)
    res = {}
    for f in todas:
        if f.serie.startswith("AVISO"):
            continue
        d = res.setdefault(f.serie, {"min": set(), "mal": set(), "clases": {}, "tol": 0})
        d["min"].add(f.k)
        if not f.ok:
            d["mal"].add(f.k)
            d["clases"][f.clase] = d["clases"].get(f.clase, 0) + 1
        elif f.clase == "tol":
            d["tol"] += 1
        elif f.clase == "nv":
            d["nv"] = d.get("nv", 0) + 1
    L.append("%-26s %8s %8s %8s   %s" % ("serie", "minutos", "iguales", "%", "diferencias por motivo (filas)"))
    for s in sorted(res, key=lambda s: (0 if s.startswith(SERIES_INSUMO) else 1, s)):
        d = res[s]
        n = len(d["min"]); malos = len(d["mal"])
        txt = ", ".join("%s: %d" % kv for kv in sorted(d["clases"].items()))
        if d["tol"]:
            txt = (txt + "; " if txt else "") + "iguales dentro de tolerancia (dif > 0): %d" % d["tol"]
        if d.get("nv"):
            txt = (txt + "; " if txt else "") + "iguales con un insumo NO verificable (precio de muestra guardado por el indicador): %d" % d["nv"]
        L.append("%-26s %8d %8d %7.1f%%   %s" % (s, n, n - malos, 100.0 * (n - malos) / n if n else float("nan"), txt or "-"))
    avisos = [f for f in todas if f.serie.startswith("AVISO")]
    if avisos:
        L.append("-" * 140)
        L.append("AVISOS DE DATO (no cambian el veredicto; dicen que parte del dato crudo no se puede separar o que una regla de ES actuo)")
        vistos = set()
        for f in avisos:
            if (f.serie, f.motivo) in vistos:
                continue
            vistos.add((f.serie, f.motivo))
            L.append("  desde %s %-12s %s" % (de_clave(f.k).strftime("%m-%d %H:%M"), f.serie, f.motivo))
    L.append("-" * 140)
    L.append("TODAS LAS FILAS (indicador / recalculado / diferencia / estado y MOTIVO)" if detalle else
             "DIFERENCIAS (indicador / recalculado / diferencia / MOTIVO; tambien las iguales dentro de tolerancia con dif > 0)")
    for f in todas:
        if f.serie.startswith("AVISO"):
            continue
        if f.ok and not detalle and f.clase not in ("tol", "nv"):
            continue
        L.append("  %s %-20s %-4s %-10s ind %-30s rec %-30s dif %-9s [%s] %s" % (
            de_clave(f.k).strftime("%m-%d %H:%M"), f.serie, f.etq, f.campo, f.ind, f.rec, f2(f.dif, 4) if f.dif is not None else "-",
            {"tol": "igual, dentro de tolerancia", "nv": "igual, insumo NO verificable"}.get(f.clase, "igual") if f.ok else f.clase, f.motivo))
    L.append("-" * 140)
    L.append("DECISIONES DEL C# (no estan en la especificacion; se modelaron para poder comparar al centavo):")
    for d in DECISIONES_DEL_CSHARP:
        L.append("  - " + d)
    malas = [f for f in todas if not f.ok]
    if not malas:
        codigo = 0
    elif all(f.clase in ("insumo", "cache C5", "decision C#") for f in malas):
        codigo = 1
    else:
        codigo = 2
    L.append("CODIGO %d: %s" % (codigo, {0: "todo coincide", 1: "diferencias solo por insumos del minuto (explicadas: con los del indicador coincide)",
                                        2: "hay diferencias con los mismos insumos o sin explicar"}[codigo]))
    texto = "\n".join(L)
    print(texto)
    if salida:
        with open(salida, "a", encoding="utf-8") as fh:
            fh.write(texto + "\n")
    return codigo, res


# ====================================================================================================== perfil: constantes
SOLO_ES = ("cota_base", "razon_rango", "etf_horas", "etf_reinicio")      # None en NQ a proposito (no existen ahi)


def cargar_perfil(nombre, ruta_constantes=None):
    """el perfil con sus constantes (las de arriba). --constantes <json> pisa claves EXACTAS de C_NQ/C_ES (p. ej. {"radio_libro": {"fut": 25,
    "ind": 20, "etf": 20}, "zest": {"etf": [0.05, 7.5, 1.0]}}) para probar otra regla; el reporte lo dice."""
    p = dict(PERFILES[nombre]); p["c"] = dict(PERFILES[nombre]["c"]); p["c"]["zest"] = dict(p["c"]["zest"])
    p["c"]["radio_libro"] = dict(p["c"]["radio_libro"])
    avisos = []
    if ruta_constantes:
        try:
            j = json.load(open(ruta_constantes, encoding="utf-8"))
            for k, v in j.items():
                if k not in p["c"]:
                    avisos.append("AVISO: constante desconocida %r (ignorada)" % k); continue
                if isinstance(p["c"][k], dict) and isinstance(v, dict):
                    for kk, vv in v.items():
                        p["c"][k][kk] = tuple(vv) if isinstance(vv, list) else vv
                else:
                    p["c"][k] = tuple(v) if isinstance(v, list) else v
            avisos.append("AVISO: constantes PISADAS desde %s (no son las decididas): %s" % (ruta_constantes, ", ".join(sorted(j))))
        except Exception as e:  # noqa: BLE001
            avisos.append("AVISO: no pude leer %s (%s)" % (ruta_constantes, e))
    faltan = [k for k, v in p["c"].items() if v is None and k not in SOLO_ES] + \
             ["zest." + r for r, v in p["c"]["zest"].items() if v is None] + ["radio." + r for r, v in p["c"]["radio_libro"].items() if v is None]
    return p, avisos, faltan


# ====================================================================================================== autoprueba del perfil ES
def autoprueba_es(carpeta):
    """Ejercita las reglas que SOLO tiene el perfil ES (la validacion en NQ no las recorre) con las cadenas reales de SPX y SPY que ya hay en
    <carpeta>/cboe y numeros a mano: radio por libro, zero estandar de ES, cota C7, rango y horas de C8, reinicio trimestral y vencido por
    fecha. No necesita la cinta del MES ni el niv (sirve antes de que la 5.0 corra): el precio del futuro se arma con una base o razon fija.
    Devuelve 0 si todo da lo esperado."""
    perfil, _, _ = cargar_perfil("ES")
    c = perfil["c"]
    fallas = []

    def ok(cond, txt):
        print(("  ok    " if cond else "  FALLA ") + txt)
        if not cond:
            fallas.append(txt)
    print("autoprueba del perfil ES (%s) sobre %s" % (VERSION, carpeta))
    ok(venc_real_utc("2026-10-27") == datetime(2026, 10, 27, 20, 0), "vencimiento real 27-10 = 20:00 UTC (16:00 NY, horario de verano)")
    ok(venc_real_utc("2026-11-02") == datetime(2026, 11, 2, 21, 0), "vencimiento real 02-11 = 21:00 UTC (16:00 NY, horario de invierno)")
    ok(venc_real_utc("2026-11-20", am=True) == datetime(2026, 11, 20, 14, 30), "SPX AM del 20-11 (marca 'am' de la 5.0) = 14:30 UTC (09:30 NY)")
    ok(tercer_viernes(2026, 11) == datetime(2026, 11, 20) and tercer_viernes(2026, 12) == datetime(2026, 12, 18), "terceros viernes 20-11 y 18-12")
    ok(ultimo_reinicio_etf(datetime(2026, 12, 18, 9, 30), "04:00") == datetime(2026, 12, 18, 9, 0) and
       ultimo_reinicio_etf(datetime(2026, 12, 18, 8, 59), "04:00") == datetime(2026, 9, 18, 8, 0),
       "reinicio trimestral de SPY a las 04:00 NY (18-09 08:00 UTC y 18-12 09:00 UTC)")
    t = datetime(2026, 10, 27, 15, 0)
    teo, tol = carry_teorico(perfil, "Z6", t, 7720.0)
    dias = (vencimiento_contrato(perfil, "Z6") - t).total_seconds() / 86400.0
    ok(abs(teo - 7720.0 * 0.034 * dias / 365.0) < 1e-9 and 35 < teo < 40,
       "carry teorico ES 27-10 15:00 UTC con fut 7720 y %.2f dias: %.2f (tolerancia %.2f)" % (dias, teo, tol))
    ok(base_fuera_de_cota(perfil, "Z6", t, 7720.0, 34.0) == "" and base_fuera_de_cota(perfil, "Z6", t, 7720.0, 37.7) == "",
       "bases medidas 34,0 y 37,7 dentro de la cota")
    ok(base_fuera_de_cota(perfil, "Z6", t, 7720.0, 60.0) != "" and base_fuera_de_cota(perfil, "Z6", t, 7720.0, -1.0) != "" and
       base_fuera_de_cota(perfil, "Z6", t, 7720.0, 151.0) != "", "bases 60, -1 y 151 fuera de la cota")
    ok(base_fuera_de_cota(PERFILES["NQ"], "Z6", t, 29000.0, 400.0) == "", "NQ no tiene cota (la especificacion la descarta)")

    class _Ctx:
        pass
    ctx = _Ctx(); ctx.c = c; ctx.perfil = perfil
    hoy = datetime.now(UTC).replace(tzinfo=None)
    for rol in ("ind", "etf"):
        lid = perfil[rol]["id"]
        d = leer_cboe([("propia", os.path.join(carpeta, "cboe"))], perfil[rol]["archivo"],
                      [(hoy - timedelta(days=k)).strftime("%Y-%m-%d") for k in (1, 0)], lambda dd: Sesion(dd, True))
        fotos = sorted(d.get("propia", {}).values(), key=lambda f: f.generado)
        if not fotos:
            ok(False, "%s: no hay cadenas en %s" % (lid, os.path.join(carpeta, "cboe"))); continue
        f = fotos[-1]
        cad = cadena_cboe(f)
        tt = f.generado + timedelta(seconds=60)
        conv = 35.0 if rol == "ind" else 10.08
        S = f.spot
        fut = S + conv if rol == "ind" else S * conv
        fl = filas_hoy(cad, tt, c["piso_dias"], venc_reales=True)
        b = armar_libro(rol, perfil, fl, S, fut, conv)
        ok(b is not None, "%s: libro armado con la foto %s, spot %.2f, fut supuesto %.2f (%d filas en el horizonte Hoy; vencimientos %s)" % (
            lid, f.ts, S, fut, fl["n"] if fl else 0, ",".join((cad.fechas or [])[:3])))
        if b is None:
            continue
        b.oi_ok = True; b.oi_fresco = True
        b.zv, b.zo = zero_de(b, perfil)
        ser = series_minuto(ctx, {rol: b})
        rad = c["radio_libro"][rol]
        for nom in ("MUROS_%s_vol" % lid, "MUROS_%s_oi" % lid, "MAJORS_%s_vol" % lid, "ZTP_%s_vol" % lid):
            lv = ser.get(nom) or []
            ok(all(abs(p - fut) <= rad + 1e-9 for e, p, m, k in lv), "%s: %s dentro de +-%g pts del futuro" % (
                nom, " | ".join("%s %.2f (K %s, M %s)" % (e, p, f2(k), f2(m, 1)) for e, p, m, k in lv) or "sin niveles", rad))
        zs = ser.get("ZEST_%s_vol" % lid) or []
        paso, semi, centro = c["zest"][rol]
        ok(all(abs(p - fut) <= c["zest_max"] for e, p, m, k in zs), "ZEST_%s_vol %s (grilla %g / +-%g / centro %g en el eje del libro; "
           "<= %g pts del futuro)" % (lid, " ".join("%.2f" % p for e, p, m, k in zs) or "sin cruce a <= 75", paso, semi, centro, c["zest_max"]))
        if cad.fechas:
            v0 = venc_real_utc(cad.fechas[0])
            n_v0 = int((cad.filas[:, 1] == 0).sum())
            fl2 = filas_hoy(cad, v0 + timedelta(seconds=30), c["piso_dias"], venc_reales=True)
            quedan = int((fl2["T"] > 0).sum()) if fl2 else 0
            ok(fl2 is None or n_v0 == 0 or fl2["n"] <= len(cad.filas) - n_v0,
               "%s: 30 s despues del vencimiento real de la serie %s (%d filas) ya no cuenta (quedan %d filas de las otras)" % (
                   lid, cad.fechas[0], n_v0, quedan))

    class _Cinta:
        def precio(self, ts):
            return 7720.0, "tick"
    fs = []
    # pares de fotos a 5 min (la segunda es 'viva': el spot cambio); hora NY del spot = generado (ts - 900 s) - 4 h
    for hh, spot in ((3, 766.0), (5, 766.1), (12, 766.2), (13, 766.3), (14, 766.4), (15, 700.0), (16, 766.5), (19, 766.6), (21, 766.7), (24, 766.8)):
        for mm, ds in ((0, -0.05), (5, 0.0)):
            x = FotoCboe(); x.generado = datetime(2026, 10, 27, 0, 0) + timedelta(hours=hh, minutes=mm)
            x.ts = (x.generado + timedelta(minutes=15)).strftime("%Y-%m-%d %H:%M:%S")
            x.spot = spot + ds; x.oi_total = 1.0; x.t_dato = x.generado; x.origen = "prueba"; x.dias = []; x.fechas = []; x.am = []
            fs.append(x)
    est, _, cont = conversion("etf", fs, _Cinta(), perfil, Sesion("2026-10-27", True), "Z6", {}, None)
    excl = [e.excluida for e in est if e.excluida]
    # vivas: las de hh:05. Fuera de 04:00-20:00 NY: 23:05, 01:05 y 20:05 NY; la de spot 700 (razon 11,03) cae por el rango: 4 excluidas
    ok(len(excl) == 4 and cont.get("excluidas ES", 0) == 4, "C8 de SPY: %d muestras excluidas por las reglas de ES: %s" % (len(excl), "; ".join(excl)))
    r = [e.razon for e in est if e.razon == e.razon]
    ok(len(r) > 0 and all(9.95 <= x <= 10.25 for x in r), "C8 de SPY: razon con >= 5 muestras validas %s" % (" ".join("%.4f" % x for x in r) or "-"))
    print("autoprueba ES: %s" % ("TODO OK" if not fallas else "%d FALLA(S)" % len(fallas)))
    return 0 if not fallas else 2


# ====================================================================================================== main
def _minuto(s):
    s = s.strip().upper().replace("Z", "")
    for fmt in ("%Y-%m-%dT%H:%M", "%Y-%m-%d %H:%M", "%Y-%m-%dT%H:%M:%S"):
        try:
            return datetime.strptime(s, fmt)
        except ValueError:
            pass
    raise argparse.ArgumentTypeError("minuto UTC como 2026-10-26T15:30Z")


def main(argv=None):
    ap = argparse.ArgumentParser(
        description="Auditor independiente de la Familia (PythiaGex 4.1 NQ / 5.0 ES): recalcula desde los datos crudos lo que el indicador "
                    "anoto en familia\\niv-*.jsonl y lo compara serie por serie (strike, precio convertido, monto, base y razon).",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="CODIGOS DE SALIDA" + __doc__.split("CODIGOS DE SALIDA")[1])
    ap.add_argument("--perfil", choices=sorted(PERFILES), default="NQ", help="NQ = 4.1 (PythiaGex4: NQ/NDX/QQQ); ES = 5.0 (PythiaGex5: ES/SPX/SPY)")
    g = ap.add_mutually_exclusive_group(required=True)
    g.add_argument("--minuto", type=_minuto, help="un minuto UTC, p. ej. 2026-10-26T15:30Z")
    g.add_argument("--ultimo", action="store_true", help="el ultimo minuto anotado en el niv mas reciente")
    g.add_argument("--desde", type=_minuto, help="inicio del rango UTC (con --hasta)")
    g.add_argument("--autoprueba-es", action="store_true", help="probar las reglas que solo tiene el perfil ES con las cadenas de SPX/SPY que ya "
                                                               "hay en PythiaGex5\\cboe (sirve antes de que la 5.0 corra)")
    ap.add_argument("--hasta", type=_minuto, help="fin del rango UTC (incluido)")
    ap.add_argument("--cada", type=int, default=30, help="muestreo del rango en minutos (default 30)")
    ap.add_argument("--minutos", help="(con --desde/--hasta) lista explicita de minutos UTC separados por coma, en vez de --cada")
    ap.add_argument("--series", help="solo estas series, separadas por coma")
    ap.add_argument("--detalle", action="store_true", help="imprimir tambien las filas que coinciden (default con --minuto/--ultimo: la "
                                                           "comprobacion de UN minuto muestra cada nivel indicador/recalculado)")
    ap.add_argument("--solo-diferencias", action="store_true", help="con --minuto/--ultimo, imprimir solo las diferencias")
    ap.add_argument("--salida", help="agregar el reporte a este archivo (fuera de %%APPDATA%%\\ATAS)")
    ap.add_argument("--sin-diagnostico", action="store_true", help="no rehacer con los insumos del indicador para clasificar el motivo")
    ap.add_argument("--datos", help="carpeta de datos (default %%APPDATA%%\\ATAS\\PythiaGex4 o 5 segun el perfil)")
    ap.add_argument("--extra", help="carpeta extra de CBOE (default %%APPDATA%%\\ATAS\\PythiaGex\\cboe-local; \"\" = no leerla)")
    ap.add_argument("--log-cboe", help="log de bajadas de la 4.1/5.0 para los arranques (default pythiagex4-cboe.log / pythiagex5-cboe.log)")
    ap.add_argument("--instrumento", help="contrato del grafico (default: el del niv)")
    ap.add_argument("--constantes", help="JSON que PISA constantes del perfil (claves exactas de C_NQ/C_ES) para probar otra regla; "
                                         "default: las decididas (NQ: especificacion; ES: DISENO_5_0 §4)")
    ap.add_argument("--espec-estricta", action="store_true", help="el contraste 'CONV <libro> espec.' con TODAS las fotos de CBOE con generado <= t "
                                                                  "(backtest_familia / vista previa en vivo) en vez de la vara de diseno")
    ap.add_argument("--paridad", action="store_true", help="sesion y horas fijas en UTC (modo paridad de la especificacion) en vez de NY")
    a = ap.parse_args(argv)
    if a.desde and not a.hasta:
        ap.error("--desde necesita --hasta")
    if a.autoprueba_es:
        return autoprueba_es(a.datos or os.path.join(APP, PERFILES["ES"]["carpeta"]))
    perfil, avisos, faltan = cargar_perfil(a.perfil, a.constantes)
    for x in avisos:
        print(x)
    if faltan:
        print("AVISO: faltan constantes del perfil %s (TODO): %s. No se audita sin ellas (regla: no inventar)." % (a.perfil, ", ".join(faltan)))
        return 3
    carpeta = a.datos or os.path.join(APP, perfil["carpeta"])
    if a.salida and os.path.abspath(a.salida).lower().startswith(os.path.abspath(APP).lower()):
        print("la salida no puede ir dentro de %APPDATA%\\ATAS (solo lectura)"); return 3
    try:
        if a.ultimo:
            dia, instr = ultimo_niv(carpeta)
            instr = a.instrumento or instr
            cab, regs, ruta = leer_niv(carpeta, dia, instr)
            claves = [max(regs)]
        else:
            t0 = a.minuto or a.desde
            t1 = a.minuto or a.hasta
            ses = Sesion.de(t0, not a.paridad)
            if Sesion.de(t1, not a.paridad).dia != ses.dia:
                print("el rango cruza dos sesiones (%s y %s): auditar de a una" % (ses.dia, Sesion.de(t1, not a.paridad).dia)); return 3
            instr = a.instrumento
            if not instr:
                ps = [p for p in glob.glob(os.path.join(carpeta, "familia", "niv-%s-*.jsonl" % ses.dia))
                      if re.match(r"niv-\d{4}-\d{2}-\d{2}-[A-Za-z0-9]+\.jsonl$", os.path.basename(p))]
                if not ps:
                    raise FaltanDatos("no hay niv de la sesion %s en %s" % (ses.dia, os.path.join(carpeta, "familia")))
                instr = re.match(r"niv-\d{4}-\d{2}-\d{2}-(.+)\.jsonl$", os.path.basename(sorted(ps)[0])).group(1)
            dia = ses.dia
            cab, regs, ruta = leer_niv(carpeta, dia, instr)
            if a.minutos:
                claves = sorted(set(clave_de(_minuto(x)) for x in a.minutos.split(",") if x.strip()))
            else:
                claves = list(range(clave_de(t0), clave_de(t1) + 1, max(1, a.cada if not a.minuto else 1)))
            ses_ = Sesion(dia, not a.paridad)
            claves = [k for k in claves if ses_.ini <= de_clave(k) < ses_.fin]
            if not claves:
                raise FaltanDatos("ningun minuto del rango cae en la sesion %s" % dia)
        opciones = dict(paridad=a.paridad, espec_estricta=a.espec_estricta)
        if a.extra is not None:
            opciones["extra"] = a.extra
        if a.log_cboe is not None:
            opciones["log_cboe"] = a.log_cboe
        ctx = Contexto(perfil, carpeta, dia, instr, opciones)
        ctx.niv = regs
        # HORIZONTE: la cinta se guarda a disco cada ~5 min (AdaptadoresFamilia: hilo 'cinta-familia'); un minuto en vivo necesita los ticks
        # hasta T + 60 s. Los minutos posteriores todavia no se pueden auditar (el niv y la viva3 ya estan, la cinta no).
        lim = ctx.horizonte - timedelta(milliseconds=LIMITE_VIVO_MS)
        if a.ultimo:
            ok_k = [k for k in regs if de_clave(k) <= lim]
            if not ok_k:
                raise FaltanDatos("ningun minuto del niv esta cubierto por la cinta guardada (llega hasta %s UTC)" % ctx.horizonte.strftime("%H:%M:%S"))
            k_ult = max(regs)
            claves = [max(ok_k)]
            if claves[0] != k_ult:
                print("AVISO: el ultimo minuto del niv es %s UTC pero la cinta guardada llega hasta %s UTC (la Familia la guarda cada pocos minutos): audito el "
                      "ultimo minuto cubierto, %s UTC" % (de_clave(k_ult).strftime("%H:%M"), ctx.horizonte.strftime("%H:%M:%S"),
                                                          de_clave(claves[0]).strftime("%H:%M")))
        else:
            fuera = [k for k in claves if de_clave(k) > lim]
            if fuera:
                print("AVISO: %d minuto(s) desde %s UTC quedan afuera: la cinta guardada llega hasta %s UTC (la Familia la guarda cada pocos minutos; reintentar "
                      "en unos minutos)" % (len(fuera), de_clave(min(fuera)).strftime("%H:%M"), ctx.horizonte.strftime("%H:%M:%S")))
                claves = [k for k in claves if de_clave(k) <= lim]
            if not claves:
                raise FaltanDatos("ningun minuto pedido esta cubierto por la cinta guardada (llega hasta %s UTC)" % ctx.horizonte.strftime("%H:%M:%S"))
    except FaltanDatos as e:
        print("NO SE PUEDE AUDITAR:", e); return 3
    filtro = set(x.strip() for x in a.series.split(",")) if a.series else None
    resultados = []
    for k in claves:
        if k in regs:
            resultados.append(auditar_minuto(ctx, k, regs[k], filtro, not a.sin_diagnostico))
        else:
            resultados.append(auditar_ausente(ctx, k))
        time.sleep(0.02)
    detalle = a.detalle or ((a.minuto is not None or a.ultimo) and not a.solo_diferencias)
    codigo, _ = reporte(ctx, resultados, detalle, a.salida, "niv: %s (cabecera %s)" % (ruta, cab))
    return codigo


if __name__ == "__main__":
    sys.exit(main())
